using System.IO.Ports;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Services;

/// <summary>
/// Talks to the weighing indicator over RS232/USB-Serial.
/// Most industrial indicators (e.g. Avery Weigh-Tronix, Essae, Contech) stream ASCII frames
/// like "ST,GS,+001.045kg\r\n" continuously. This service parses the numeric weight out of
/// whatever comes in, tracks a rolling window of readings, and raises WeightReceived with
/// IsStable=true once N consecutive readings fall within tolerance of each other.
///
/// NOTE: The regex-based parser below covers the common "ST/US,GS/NT,value,unit" protocol
/// family. If your indicator uses a different frame format, adjust ParseWeight() only -
/// nothing else in the app needs to change because everything downstream depends on the
/// ISerialPortService interface, not this implementation.
/// </summary>
public class SerialPortService : ISerialPortService
{
    private SerialPort? _port;
    private SerialPortSettings _settings = new();
    private readonly Queue<decimal> _recentReadings = new();
    private readonly StringBuilder _buffer = new();
    private readonly object _lock = new();
    private bool _awaitingScaleReset;

    private static readonly Regex WeightPattern = new(
        @"(?<stable>ST|US)?[,\s]*(?<mode>GS|NT)?[,\s]*(?<sign>[+-])?(?<value>\d+(?:\.\d+)?)\s*(?<unit>kg|KG|g|G)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SerialPortService(SerialPortSettings? settings = null)
    {
        if (settings is not null)
            _settings = settings;
    }

    public event EventHandler<WeightReadingEventArgs>? WeightReceived;
    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

    public IEnumerable<string> GetAvailablePorts() => SerialPort.GetPortNames().OrderBy(p => p);

    public Task ConnectAsync(SerialPortSettings settings)
    {
        _settings = settings;
        SetStatus(ConnectionStatus.Connecting);

        try
        {
            _port?.Dispose();
            _port = new SerialPort(settings.PortName, settings.BaudRate)
            {
                Parity = Enum.Parse<Parity>(settings.Parity),
                DataBits = settings.DataBits,
                StopBits = Enum.Parse<StopBits>(settings.StopBits),
                ReadTimeout = 2000,
                WriteTimeout = 2000,
                NewLine = "\r\n"
            };
            _port.DataReceived += OnDataReceived;
            _port.ErrorReceived += (_, _) => SetStatus(ConnectionStatus.Error);
            _port.Open();

            lock (_lock)
            {
                _recentReadings.Clear();
                _buffer.Clear();
                _awaitingScaleReset = false;
            }
            SetStatus(ConnectionStatus.Connected);
        }
        catch (Exception)
        {
            SetStatus(ConnectionStatus.Error);
            throw;
        }

        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        if (_port is { IsOpen: true })
        {
            _port.DataReceived -= OnDataReceived;
            _port.Close();
        }
        SetStatus(ConnectionStatus.Disconnected);
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var chunk = _port!.ReadExisting();
            ProcessIncomingData(chunk);
        }
        catch (TimeoutException)
        {
            // Transient - the next poll will pick up remaining data.
        }
        catch (Exception)
        {
            SetStatus(ConnectionStatus.Error);
        }
    }

    internal void ProcessIncomingData(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;

        var completeLines = new List<string>();
        lock (_lock)
        {
            _buffer.Append(chunk);

            while (true)
            {
                var content = _buffer.ToString();
                var delimiterIndex = content.IndexOfAny(['\r', '\n']);
                if (delimiterIndex < 0) break;

                completeLines.Add(content[..delimiterIndex]);

                var removeLength = delimiterIndex + 1;
                while (removeLength < content.Length &&
                       (content[removeLength] == '\r' || content[removeLength] == '\n'))
                {
                    removeLength++;
                }

                _buffer.Remove(0, removeLength);
            }
        }

        foreach (var line in completeLines)
        {
            var match = WeightPattern.Match(line);
            if (!match.Success || !match.Groups["value"].Success) continue;

            if (!decimal.TryParse(
                    match.Groups["value"].Value,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                continue;
            }

            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            var weightKg = unit == "g" ? value / 1000m : value;

            if (match.Groups["sign"].Value == "-") weightKg = -weightKg;

            var indicatorSaysUnstable = match.Groups["stable"].Value.Equals(
                "US",
                StringComparison.OrdinalIgnoreCase);
            EvaluateStability(weightKg, indicatorSaysUnstable);
        }
    }

    private void EvaluateStability(decimal weightKg, bool indicatorSaysUnstable)
    {
        bool isStable;
        lock (_lock)
        {
            var resetThreshold = Math.Max(0m, _settings.ResetWeightThresholdKg);

            if (Math.Abs(weightKg) <= resetThreshold)
            {
                _awaitingScaleReset = false;
                _recentReadings.Clear();
                isStable = false;
            }
            else if (_awaitingScaleReset)
            {
                isStable = false;
            }
            else if (indicatorSaysUnstable)
            {
                _recentReadings.Clear();
                isStable = false;
            }
            else
            {
                var stableReadingCount = Math.Max(1, _settings.StableReadingCount);
                var tolerance = Math.Max(0m, _settings.StabilityToleranceKg);

                _recentReadings.Enqueue(weightKg);
                while (_recentReadings.Count > stableReadingCount)
                    _recentReadings.Dequeue();

                isStable = _recentReadings.Count >= stableReadingCount &&
                           (_recentReadings.Max() - _recentReadings.Min()) <= tolerance;

                if (isStable)
                {
                    _recentReadings.Clear();
                    _awaitingScaleReset = true;
                }
            }
        }

        WeightReceived?.Invoke(this, new WeightReadingEventArgs
        {
            WeightKg = weightKg,
            IsStable = isStable
        });
    }

    private void SetStatus(ConnectionStatus status)
    {
        Status = status;
        ConnectionStatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        Disconnect();
        _port?.Dispose();
    }
}
