using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private readonly IWeighRecordRepository _weighRecordRepository;
    private readonly IReportService _reportService;

    public ReportsViewModel(IWeighRecordRepository weighRecordRepository, IReportService reportService)
    {
        _weighRecordRepository = weighRecordRepository;
        _reportService = reportService;

        Records = new ObservableCollection<WeighRecord>();
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;

        SearchCommand = new AsyncRelayCommand(SearchAsync);
        ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync);

        _ = SearchAsync();
    }

    public ObservableCollection<WeighRecord> Records { get; }

    private DateTime? _fromDate;
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }

    private DateTime? _toDate;
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }

    private string _productFilter = string.Empty;
    public string ProductFilter { get => _productFilter; set => SetProperty(ref _productFilter, value); }

    private string _operatorFilter = string.Empty;
    public string OperatorFilter { get => _operatorFilter; set => SetProperty(ref _operatorFilter, value); }

    private string _qrIdFilter = string.Empty;
    public string QrIdFilter { get => _qrIdFilter; set => SetProperty(ref _qrIdFilter, value); }

    private int _totalCount;
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }

    private int _passCount;
    public int PassCount { get => _passCount; set => SetProperty(ref _passCount, value); }

    private int _failCount;
    public int FailCount { get => _failCount; set => SetProperty(ref _failCount, value); }

    private double _passPercent;
    public double PassPercent { get => _passPercent; set => SetProperty(ref _passPercent, value); }

    private double _failPercent;
    public double FailPercent { get => _failPercent; set => SetProperty(ref _failPercent, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand ExportExcelCommand { get; }
    public IAsyncRelayCommand ExportPdfCommand { get; }

    private async Task SearchAsync()
    {
        var results = await _weighRecordRepository.SearchAsync(
            fromDate: FromDate,
            toDate: ToDate,
            productName: string.IsNullOrWhiteSpace(ProductFilter) ? null : ProductFilter,
            qrId: string.IsNullOrWhiteSpace(QrIdFilter) ? null : QrIdFilter,
            operatorName: string.IsNullOrWhiteSpace(OperatorFilter) ? null : OperatorFilter);

        Records.Clear();
        foreach (var r in results) Records.Add(r);

        TotalCount = results.Count;
        PassCount = results.Count(r => r.Result == WeighResult.Pass);
        FailCount = results.Count(r => r.Result == WeighResult.Fail);
        PassPercent = TotalCount == 0 ? 0 : Math.Round(PassCount * 100.0 / TotalCount, 1);
        FailPercent = TotalCount == 0 ? 0 : Math.Round(FailCount * 100.0 / TotalCount, 1);
    }

    private async Task ExportExcelAsync()
    {
        if (Records.Count == 0) { StatusMessage = "No records to export."; return; }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"WeighReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };
        if (dialog.ShowDialog() != true) return;

        var bytes = await _reportService.ExportToExcelAsync(Records.ToList());
        await File.WriteAllBytesAsync(dialog.FileName, bytes);
        StatusMessage = $"Excel report saved to {dialog.FileName}";
    }

    private async Task ExportPdfAsync()
    {
        if (Records.Count == 0) { StatusMessage = "No records to export."; return; }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF Document (*.pdf)|*.pdf",
            FileName = $"WeighReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
        };
        if (dialog.ShowDialog() != true) return;

        var title = $"Weigh Verification Report ({FromDate:dd-MM-yyyy} to {ToDate:dd-MM-yyyy})";
        var bytes = await _reportService.ExportToPdfAsync(Records.ToList(), title);
        await File.WriteAllBytesAsync(dialog.FileName, bytes);
        StatusMessage = $"PDF report saved to {dialog.FileName}";
    }
}
