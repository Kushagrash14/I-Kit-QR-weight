using Microsoft.Extensions.Logging;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Services;

/// <summary>
/// The heart of the application. Receives a single stable weight reading and, with
/// zero manual intervention, decides PASS/FAIL, persists the record, and (on PASS only)
/// generates a QR code and sends the label to the printer.
/// </summary>
public class WeighingEngine : IWeighingEngine
{
    private readonly IWeighRecordRepository _weighRecordRepository;
    private readonly IPrinterService _printerService;
    private readonly ISerialNumberService _serialNumberService;
    private readonly StationSettings _stationSettings;
    private readonly PrinterSettings _printerSettings; // resolved from Settings screen / config at startup
    private readonly ILogger<WeighingEngine> _logger;
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    public WeighingEngine(
        IWeighRecordRepository weighRecordRepository,
        IPrinterService printerService,
        ISerialNumberService serialNumberService,
        StationSettings stationSettings,
        PrinterSettings printerSettings,
        ILogger<WeighingEngine> logger)
    {
        _weighRecordRepository = weighRecordRepository;
        _printerService = printerService;
        _serialNumberService = serialNumberService;
        _stationSettings = stationSettings;
        _printerSettings = printerSettings;
        _logger = logger;
    }

    public event EventHandler<WeighingCompletedEventArgs>? WeighingCompleted;

    public Product? CurrentProduct { get; set; }
    public string CurrentOperator { get; set; } = string.Empty;

    public async Task ProcessStableWeightAsync(decimal weightKg)
    {
        await _processingLock.WaitAsync();
        try
        {
            await ProcessStableWeightCoreAsync(weightKg);
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task ProcessStableWeightCoreAsync(decimal weightKg)
    {
        if (CurrentProduct is null)
        {
            _logger.LogWarning("Stable weight {Weight} received with no product selected.", weightKg);
            var orphanRecord = new WeighRecord
            {
                KitNumber = "N/A",
                ProductName = "(none selected)",
                WeightKg = weightKg,
                Result = WeighResult.Fail,
                FailReason = FailReason.NoProductSelected,
                OperatorName = CurrentOperator,
                Remarks = "Operator had not selected a product before placing the kit."
            };
            WeighingCompleted?.Invoke(this, new WeighingCompletedEventArgs { Record = orphanRecord });
            return;
        }

        var (result, reason) = CurrentProduct.Evaluate(weightKg);

        var record = new WeighRecord
        {
            ProductId = CurrentProduct.Id,
            ProductName = CurrentProduct.ProductName,
            Quantity = CurrentProduct.Quantity,
            CommandCode = CurrentProduct.CommandCode,
            ModelCode = CurrentProduct.ModelCode,
            LabelSizeText = CurrentProduct.LabelSizeText,
            LabelLengthText = CurrentProduct.LabelLengthText,
            LabelMaterialText = CurrentProduct.LabelMaterialText,
            WeightKg = weightKg,
            Result = result,
            FailReason = reason,
            OperatorName = CurrentOperator,
            RecordDate = DateTime.Now,
            SiteCode = _stationSettings.SiteCode,
            LineCode = CurrentProduct.LabelLineCode,
            MachineCode = _stationSettings.MachineCode
        };

        if (result == WeighResult.Pass)
        {
            var allocation = await _serialNumberService.GetNextAsync();
            record.SerialNumber = allocation.Value;
            record.DailySerialNumber = await _weighRecordRepository.GetNextDailyLabelSerialAsync(
                record.CommandCode,
                record.LineCode,
                record.RecordDate);
            record.KitNumber = _serialNumberService.BuildKitNumber(
                record.CommandCode,
                record.LineCode,
                record.DailySerialNumber,
                weightKg,
                record.RecordDate);
            record.QrId = record.KitNumber;
            record.QrPayload = _serialNumberService.BuildQrPayload(record);
            record.QrGenerated = true;
            if (!allocation.FromCentralBlock)
                record.Remarks = "Offline emergency serial; pending central synchronization.";
        }
        else
        {
            // FAIL records still get a traceable kit number, just no QR/label.
            record.KitNumber = await _weighRecordRepository.GenerateNextKitNumberAsync($"{CurrentProduct.CodePrefix}-REJ");
            record.Remarks = reason == FailReason.WeightBelowLimit
                ? "Weight Below Limit"
                : "Weight Above Limit";
        }

        await _weighRecordRepository.AddAsync(record);

        if (result == WeighResult.Pass)
        {
            try
            {
                var printed = await _printerService.PrintLabelAsync(record, _printerSettings);
                record.PrintedSuccessfully = printed;
                record.PrinterStatus = printed ? "Printed" : "Print Failed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Printing failed for kit {KitNumber}", record.KitNumber);
                record.PrintedSuccessfully = false;
                record.PrinterStatus = "Print Error";
            }

            await _weighRecordRepository.UpdateAsync(record);
        }

        _logger.LogInformation(
            "Kit {KitNumber} | {Product} | {Weight} kg | {Result}",
            record.KitNumber, record.ProductName, record.WeightKg, record.Result);

        WeighingCompleted?.Invoke(this, new WeighingCompletedEventArgs { Record = record });
    }
}
