using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public class PrinterSettingsViewModel : ViewModelBase
{
    private readonly IPrinterService _printerService;
    private readonly IDatabaseBackupService _backupService;
    private readonly PrinterSettings _printerSettings;

    public PrinterSettingsViewModel(IPrinterService printerService, IDatabaseBackupService backupService, PrinterSettings printerSettings)
    {
        _printerService = printerService;
        _backupService = backupService;
        _printerSettings = printerSettings;

        InstalledPrinters = new ObservableCollection<string>(_printerService.GetInstalledPrinterNames());
        PrintMethods = ["auto", "cmd", "api"];

        BarTenderApiUrl = _printerSettings.BarTenderApiUrl;
        BarTenderPrinterName = ResolveInitialPrinterName(_printerSettings.BarTenderPrinterName);
        BarTenderExePath = _printerSettings.BarTenderExePath;
        BarTenderLabelPath = NormalizeTemplatePath(_printerSettings.BarTenderLabelPath);
        BarTenderPrintMethod = _printerSettings.BarTenderPrintMethod;

        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        SaveCommand = new RelayCommand(Save);
        RefreshPrintersCommand = new RelayCommand(RefreshInstalledPrinters);
        BrowseTemplateCommand = new RelayCommand(BrowseTemplate);
        BackupNowCommand = new AsyncRelayCommand(BackupNowAsync);
    }

    public ObservableCollection<string> InstalledPrinters { get; }
    public IReadOnlyList<string> PrintMethods { get; }

    private string _barTenderApiUrl = string.Empty;
    public string BarTenderApiUrl { get => _barTenderApiUrl; set => SetProperty(ref _barTenderApiUrl, value); }

    private string _barTenderPrinterName = string.Empty;
    public string BarTenderPrinterName { get => _barTenderPrinterName; set => SetProperty(ref _barTenderPrinterName, value); }

    private string _barTenderExePath = string.Empty;
    public string BarTenderExePath { get => _barTenderExePath; set => SetProperty(ref _barTenderExePath, value); }

    private string _barTenderLabelPath = string.Empty;
    public string BarTenderLabelPath { get => _barTenderLabelPath; set => SetProperty(ref _barTenderLabelPath, value); }

    private string _barTenderPrintMethod = "auto";
    public string BarTenderPrintMethod { get => _barTenderPrintMethod; set => SetProperty(ref _barTenderPrintMethod, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public IAsyncRelayCommand TestConnectionCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RefreshPrintersCommand { get; }
    public ICommand BrowseTemplateCommand { get; }
    public IAsyncRelayCommand BackupNowCommand { get; }

    private async Task TestConnectionAsync()
    {
        var ok = await _printerService.TestConnectionAsync(BuildSettings());
        StatusMessage = ok
            ? "Printer connection successful."
            : $"Printer connection failed: {_printerService.LastErrorMessage}";
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(BarTenderPrinterName))
        {
            StatusMessage = "Select the exact installed printer queue before saving.";
            return;
        }

        var updated = BuildSettings();
        _printerSettings.PrinterType = PrinterType.Zebra;
        _printerSettings.ConnectionMode = PrinterConnectionMode.BarTender;
        _printerSettings.BarTenderApiUrl = updated.BarTenderApiUrl;
        _printerSettings.BarTenderPrinterName = updated.BarTenderPrinterName;
        _printerSettings.BarTenderExePath = updated.BarTenderExePath;
        _printerSettings.BarTenderLabelPath = updated.BarTenderLabelPath;
        _printerSettings.BarTenderPrintMethod = updated.BarTenderPrintMethod;
        AppSettingsFileWriter.SavePrinterSettings(_printerSettings);
        StatusMessage = "Printer settings saved and will persist across restarts.";
    }

    private void RefreshInstalledPrinters()
    {
        var selectedPrinter = BarTenderPrinterName;
        InstalledPrinters.Clear();
        foreach (var printerName in _printerService.GetInstalledPrinterNames())
            InstalledPrinters.Add(printerName);

        var exactMatch = InstalledPrinters.FirstOrDefault(
            name => string.Equals(name, selectedPrinter, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
            BarTenderPrinterName = exactMatch;
        else if (string.IsNullOrWhiteSpace(selectedPrinter) && InstalledPrinters.Count == 1)
            BarTenderPrinterName = InstalledPrinters[0];

        StatusMessage = InstalledPrinters.Count == 0
            ? "No Windows printer queues were found."
            : $"{InstalledPrinters.Count} installed printer queue(s) found.";
    }

    private void BrowseTemplate()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select BarTender Label Template",
            Filter = "BarTender templates (*.btw)|*.btw|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            BarTenderLabelPath = dialog.FileName;
    }

    /// <summary>Also exposed here since Printer Settings is a natural place for the DB-status card in a single-Admin-screen flow.</summary>
    private async Task BackupNowAsync()
    {
        var path = await _backupService.BackupNowAsync();
        StatusMessage = string.IsNullOrEmpty(path)
            ? $"Backup failed: {_backupService.DatabaseStatusText}"
            : $"Backup created: {path}";
    }

    private PrinterSettings BuildSettings() => new()
    {
        PrinterType = PrinterType.Zebra,
        ConnectionMode = PrinterConnectionMode.BarTender,
        IpAddress = _printerSettings.IpAddress,
        Port = _printerSettings.Port,
        ComPort = _printerSettings.ComPort,
        WindowsPrinterName = _printerSettings.WindowsPrinterName,
        LabelWidthMm = _printerSettings.LabelWidthMm,
        LabelHeightMm = _printerSettings.LabelHeightMm,
        BaudRate = _printerSettings.BaudRate,
        DpiSetting = _printerSettings.DpiSetting,
        BarTenderApiUrl = BarTenderApiUrl,
        BarTenderPrinterName = BarTenderPrinterName,
        BarTenderExePath = BarTenderExePath,
        BarTenderLabelPath = BarTenderLabelPath,
        BarTenderPrintMethod = BarTenderPrintMethod
    };

    private static string NormalizeTemplatePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return @"Labels\Template.btw";

        if (!Path.IsPathRooted(configuredPath) || File.Exists(configuredPath))
            return configuredPath;

        var labelsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Labels");
        var sameName = Path.Combine(labelsDirectory, Path.GetFileName(configuredPath));
        if (File.Exists(sameName))
            return Path.Combine("Labels", Path.GetFileName(sameName));

        var firstTemplate = Directory.Exists(labelsDirectory)
            ? Directory.EnumerateFiles(labelsDirectory, "*.btw").FirstOrDefault()
            : null;
        return firstTemplate is null
            ? configuredPath
            : Path.Combine("Labels", Path.GetFileName(firstTemplate));
    }

    private string ResolveInitialPrinterName(string configuredPrinterName)
    {
        var exactMatch = InstalledPrinters.FirstOrDefault(
            name => string.Equals(name, configuredPrinterName?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
            return exactMatch;

        return string.IsNullOrWhiteSpace(configuredPrinterName) && InstalledPrinters.Count == 1
            ? InstalledPrinters[0]
            : configuredPrinterName?.Trim() ?? string.Empty;
    }
}
