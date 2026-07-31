using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Services;

public sealed class SerialNumberService : ISerialNumberService
{
    private readonly IWeighRecordRepository _repository;
    private readonly ICentralSyncStore _centralStore;
    private readonly CentralSyncSettings _syncSettings;
    private readonly StationSettings _stationSettings;
    private readonly ILogger<SerialNumberService> _logger;
    private static readonly SemaphoreSlim SequenceLock = new(1, 1);

    public SerialNumberService(
        IWeighRecordRepository repository,
        ICentralSyncStore centralStore,
        CentralSyncSettings syncSettings,
        StationSettings stationSettings,
        ILogger<SerialNumberService> logger)
    {
        _repository = repository;
        _centralStore = centralStore;
        _syncSettings = syncSettings;
        _stationSettings = stationSettings;
        _logger = logger;
    }

    public async Task<SerialNumberAllocation> GetNextAsync(
        CancellationToken cancellationToken = default)
    {
        await SequenceLock.WaitAsync(cancellationToken);
        try
        {
            var state = await _repository.GetSerialNumberStateAsync();
            if (state.NextSerial <= 0 || state.NextSerial > state.BlockEndSerial)
            {
                var centralBlock = await TryGetCentralBlockAsync(cancellationToken);
                if (centralBlock is not null)
                {
                    state.NextSerial = centralBlock.Start;
                    state.BlockEndSerial = centralBlock.End;
                }
            }

            long value;
            var fromCentral = state.NextSerial > 0 && state.NextSerial <= state.BlockEndSerial;
            if (fromCentral)
            {
                value = state.NextSerial++;
            }
            else
            {
                if (state.EmergencyNextSerial < _stationSettings.EmergencySerialStart)
                    state.EmergencyNextSerial = _stationSettings.EmergencySerialStart;

                value = state.EmergencyNextSerial++;
                _logger.LogWarning(
                    "Using emergency offline serial {Serial} for station {Site}/{Line}/{Machine}.",
                    value,
                    _stationSettings.SiteCode,
                    _stationSettings.LineCode,
                    _stationSettings.MachineCode);
            }

            var maxValue = Pow10(Math.Clamp(_stationSettings.SerialDigits, 1, 18)) - 1;
            if (value > maxValue)
                throw new InvalidOperationException(
                    $"Serial {value} exceeds configured {_stationSettings.SerialDigits}-digit capacity.");

            await _repository.UpdateSerialNumberStateAsync(state);
            return new SerialNumberAllocation(value, fromCentral);
        }
        finally
        {
            SequenceLock.Release();
        }
    }

    public string BuildKitNumber(
        string commandCode,
        string lineCode,
        int dailySerialNumber,
        decimal weightKg,
        DateTime timestamp)
    {
        if (dailySerialNumber is <= 0 or > 999_999)
            throw new InvalidOperationException(
                $"Daily label serial {dailySerialNumber} exceeds six-digit capacity.");

        var weightGrams = decimal.ToInt64(
            decimal.Round(weightKg * 1000m, 0, MidpointRounding.AwayFromZero));
        var weight = weightGrams.ToString("D4", CultureInfo.InvariantCulture);

        return string.Join(
            '-',
            Clean(commandCode, "P"),
            Clean(lineCode, "LINE"),
            timestamp.ToString("ddMMyy", CultureInfo.InvariantCulture),
            weight,
            dailySerialNumber.ToString("D6", CultureInfo.InvariantCulture));
    }

    public string BuildQrPayload(WeighRecord record)
    {
        var qrText = CleanPayload(record.QrText);
        var kitNumber = CleanPayload(record.KitNumber);
        return string.IsNullOrWhiteSpace(qrText)
            ? kitNumber
            : $"{qrText} {kitNumber}";
    }

    private async Task<SerialNumberBlock?> TryGetCentralBlockAsync(
        CancellationToken cancellationToken)
    {
        if (!_centralStore.IsEnabled)
            return null;

        try
        {
            return await _centralStore.TryAllocateSerialBlockAsync(
                Math.Max(1, _syncSettings.SerialBlockSize),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Central serial allocation unavailable; using local emergency range.");
            return null;
        }
    }

    private static string Clean(string value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var builder = new StringBuilder(source.Length);
        foreach (var ch in source)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToUpperInvariant(ch));
        }

        return builder.Length == 0 ? fallback : builder.ToString();
    }

    private static string CleanPayload(string? value) =>
        (value ?? string.Empty)
            .Replace("|", "/")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

    private static long Pow10(int exponent)
    {
        long value = 1;
        for (var i = 0; i < exponent; i++)
            value *= 10;
        return value;
    }
}
