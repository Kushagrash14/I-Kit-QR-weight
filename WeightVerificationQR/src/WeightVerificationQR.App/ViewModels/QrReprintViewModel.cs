using CommunityToolkit.Mvvm.Input;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public class QrReprintViewModel : ViewModelBase
{
    private readonly IWeighRecordRepository _weighRecordRepository;
    private readonly IPrinterService _printerService;
    private readonly PrinterSettings _printerSettings;

    public QrReprintViewModel(
        IWeighRecordRepository weighRecordRepository,
        IPrinterService printerService,
        PrinterSettings printerSettings)
    {
        _weighRecordRepository = weighRecordRepository;
        _printerService = printerService;
        _printerSettings = printerSettings;

        SearchCommand = new AsyncRelayCommand(SearchAsync);
        ReprintCommand = new AsyncRelayCommand(
            ReprintAsync,
            () => FoundRecord is
            {
                Result: WeighResult.Pass,
                QrGenerated: true
            } record &&
            !string.IsNullOrWhiteSpace(record.QrPayload));
    }

    private string _qrIdInput = string.Empty;
    public string QrIdInput { get => _qrIdInput; set => SetProperty(ref _qrIdInput, value); }

    private WeighRecord? _foundRecord;
    public WeighRecord? FoundRecord
    {
        get => _foundRecord;
        set { SetProperty(ref _foundRecord, value); ((AsyncRelayCommand)ReprintCommand).NotifyCanExecuteChanged(); }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand ReprintCommand { get; }

    private async Task SearchAsync()
    {
        StatusMessage = string.Empty;
        FoundRecord = null;

        if (string.IsNullOrWhiteSpace(QrIdInput))
        {
            StatusMessage = "Enter a Kit / QR ID to search.";
            return;
        }

        var record = await _weighRecordRepository.GetByQrIdAsync(QrIdInput.Trim());
        if (record is null)
        {
            StatusMessage = "No record found for that QR ID.";
            return;
        }

        FoundRecord = record;

        if (record.Result != WeighResult.Pass)
            StatusMessage = "This record is a FAIL - no QR label was ever generated for it.";
        else if (!record.QrGenerated || string.IsNullOrWhiteSpace(record.QrPayload))
            StatusMessage = "This PASS record has no stored QR payload and cannot be reprinted safely.";
    }

    private async Task ReprintAsync()
    {
        if (FoundRecord is null) return;

        try
        {
            var printed = await _printerService.PrintLabelAsync(FoundRecord, _printerSettings);
            if (printed)
            {
                FoundRecord.ReprintCount++;
                await _weighRecordRepository.UpdateAsync(FoundRecord);
                StatusMessage = $"Label reprinted for {FoundRecord.KitNumber} (reprint #{FoundRecord.ReprintCount}).";
            }
            else
            {
                StatusMessage = $"Reprint failed: {_printerService.LastErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reprint error: {ex.Message}";
        }
    }
}
