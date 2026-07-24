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
    private readonly PrinterSettings _printerSettings; // resolved from Settings screen / config at startup
    private readonly ILogger<WeighingEngine> _logger;

    public WeighingEngine(
        IWeighRecordRepository weighRecordRepository,
        IPrinterService printerService,
        PrinterSettings printerSettings,
        ILogger<WeighingEngine> logger)
    {
        _weighRecordRepository = weighRecordRepository;
        _printerService = printerService;
        _printerSettings = printerSettings;
        _logger = logger;
    }

    public event EventHandler<WeighingCompletedEventArgs>? WeighingCompleted;

    public Product? CurrentProduct { get; set; }
    public string CurrentOperator { get; set; } = string.Empty;

    public async Task ProcessStableWeightAsync(decimal weightKg)
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
            WeightKg = weightKg,
            Result = result,
            FailReason = reason,
            OperatorName = CurrentOperator,
            RecordDate = DateTime.Now
        };

        if (result == WeighResult.Pass)
        {
            record.KitNumber = await _weighRecordRepository.GenerateNextKitNumberAsync(CurrentProduct.CodePrefix);
            record.QrId = record.KitNumber;
            record.QrGenerated = true;
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
