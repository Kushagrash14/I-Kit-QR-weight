using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using WeightVerificationQR.App;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public class MachineSettingsViewModel : ViewModelBase
{
    private readonly ISerialPortService _serialPortService;
    private readonly SerialPortSettings _serialSettings;
    private readonly StationSettings _stationSettings;
    private readonly CentralSyncSettings _centralSyncSettings;
    private readonly ICentralSyncStore _centralStore;
    private readonly IOfflineSyncService _offlineSyncService;

    public MachineSettingsViewModel(
        ISerialPortService serialPortService,
        SerialPortSettings serialSettings,
        StationSettings stationSettings,
        CentralSyncSettings centralSyncSettings,
        ICentralSyncStore centralStore,
        IOfflineSyncService offlineSyncService)
    {
        _serialPortService = serialPortService;
        _serialSettings = serialSettings;
        _stationSettings = stationSettings;
        _centralSyncSettings = centralSyncSettings;
        _centralStore = centralStore;
        _offlineSyncService = offlineSyncService;

        AvailablePorts = new ObservableCollection<string>(_serialPortService.GetAvailablePorts());

        PortName = _serialSettings.PortName;
        BaudRate = _serialSettings.BaudRate;
        StableReadingCount = _serialSettings.StableReadingCount;
        StabilityToleranceKg = _serialSettings.StabilityToleranceKg;
        ResetWeightThresholdKg = _serialSettings.ResetWeightThresholdKg;
        QrPrefix = _stationSettings.QrPrefix;
        SiteCode = _stationSettings.SiteCode;
        LineCode = _stationSettings.LineCode;
        MachineCode = _stationSettings.MachineCode;
        SerialDigits = _stationSettings.SerialDigits;
        EmergencySerialStart = _stationSettings.EmergencySerialStart;
        CentralSyncEnabled = _centralSyncSettings.Enabled;
        CentralConnectionString = _centralSyncSettings.ConnectionString;
        SerialBlockSize = _centralSyncSettings.SerialBlockSize;
        SyncIntervalSeconds = _centralSyncSettings.SyncIntervalSeconds;

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        TestCentralCommand = new AsyncRelayCommand(TestCentralAsync);
        SaveCommand = new RelayCommand(Save);
    }

    public ObservableCollection<string> AvailablePorts { get; }

    private string _portName = string.Empty;
    public string PortName { get => _portName; set => SetProperty(ref _portName, value); }

    private int _baudRate;
    public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }

    private int _stableReadingCount;
    public int StableReadingCount { get => _stableReadingCount; set => SetProperty(ref _stableReadingCount, value); }

    private decimal _stabilityToleranceKg;
    public decimal StabilityToleranceKg { get => _stabilityToleranceKg; set => SetProperty(ref _stabilityToleranceKg, value); }

    private decimal _resetWeightThresholdKg;
    public decimal ResetWeightThresholdKg { get => _resetWeightThresholdKg; set => SetProperty(ref _resetWeightThresholdKg, value); }

    private string _qrPrefix = string.Empty;
    public string QrPrefix { get => _qrPrefix; set => SetProperty(ref _qrPrefix, value); }

    private string _siteCode = string.Empty;
    public string SiteCode { get => _siteCode; set => SetProperty(ref _siteCode, value); }

    private string _lineCode = string.Empty;
    public string LineCode { get => _lineCode; set => SetProperty(ref _lineCode, value); }

    private string _machineCode = string.Empty;
    public string MachineCode { get => _machineCode; set => SetProperty(ref _machineCode, value); }

    private int _serialDigits;
    public int SerialDigits { get => _serialDigits; set => SetProperty(ref _serialDigits, value); }

    private long _emergencySerialStart;
    public long EmergencySerialStart { get => _emergencySerialStart; set => SetProperty(ref _emergencySerialStart, value); }

    private bool _centralSyncEnabled;
    public bool CentralSyncEnabled { get => _centralSyncEnabled; set => SetProperty(ref _centralSyncEnabled, value); }

    private string _centralConnectionString = string.Empty;
    public string CentralConnectionString { get => _centralConnectionString; set => SetProperty(ref _centralConnectionString, value); }

    private int _serialBlockSize;
    public int SerialBlockSize { get => _serialBlockSize; set => SetProperty(ref _serialBlockSize, value); }

    private int _syncIntervalSeconds;
    public int SyncIntervalSeconds { get => _syncIntervalSeconds; set => SetProperty(ref _syncIntervalSeconds, value); }

    private ConnectionStatus _lastTestResult = ConnectionStatus.Disconnected;
    public ConnectionStatus LastTestResult { get => _lastTestResult; set => SetProperty(ref _lastTestResult, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public ICommand RefreshPortsCommand { get; }
    public IAsyncRelayCommand TestConnectionCommand { get; }
    public IAsyncRelayCommand TestCentralCommand { get; }
    public ICommand SaveCommand { get; }

    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var p in _serialPortService.GetAvailablePorts()) AvailablePorts.Add(p);
        StatusMessage = $"{AvailablePorts.Count} port(s) detected.";
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            await _serialPortService.ConnectAsync(BuildSettings());
            LastTestResult = ConnectionStatus.Connected;
            StatusMessage = $"Connected successfully to {PortName}.";
        }
        catch (Exception ex)
        {
            LastTestResult = ConnectionStatus.Error;
            StatusMessage = $"Machine connection failed: {ex.Message}";
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(SiteCode) ||
            string.IsNullOrWhiteSpace(LineCode) ||
            string.IsNullOrWhiteSpace(MachineCode))
        {
            StatusMessage = "Site, line and machine codes are required.";
            return;
        }

        if (SerialDigits is < 1 or > 18 || EmergencySerialStart < 1)
        {
            StatusMessage = "Serial digits must be 1-18 and emergency serial start must be positive.";
            return;
        }

        var updated = BuildSettings();
        _serialSettings.PortName = updated.PortName;
        _serialSettings.BaudRate = updated.BaudRate;
        _serialSettings.StableReadingCount = updated.StableReadingCount;
        _serialSettings.StabilityToleranceKg = updated.StabilityToleranceKg;
        _serialSettings.ResetWeightThresholdKg = updated.ResetWeightThresholdKg;
        ApplyStationSettings();
        ApplyCentralSettings();
        AppSettingsFileWriter.SaveSerialPortSettings(_serialSettings);
        AppSettingsFileWriter.SaveStationSettings(_stationSettings);
        AppSettingsFileWriter.SaveCentralSyncSettings(_centralSyncSettings);
        _offlineSyncService.Start();
        StatusMessage = "Machine, station and offline-sync settings saved.";
    }

    private async Task TestCentralAsync()
    {
        ApplyCentralSettings();
        try
        {
            await _centralStore.InitializeAsync();
            await _offlineSyncService.SyncNowAsync();
            StatusMessage = $"Central database connected. {_offlineSyncService.StatusText}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Central database unavailable; local mode remains active. {ex.Message}";
        }
    }

    private void ApplyStationSettings()
    {
        _stationSettings.QrPrefix = QrPrefix;
        _stationSettings.SiteCode = SiteCode;
        _stationSettings.LineCode = LineCode;
        _stationSettings.MachineCode = MachineCode;
        _stationSettings.SerialDigits = SerialDigits;
        _stationSettings.EmergencySerialStart = EmergencySerialStart;
    }

    private void ApplyCentralSettings()
    {
        _centralSyncSettings.Enabled = CentralSyncEnabled;
        _centralSyncSettings.ConnectionString = CentralConnectionString;
        _centralSyncSettings.SerialBlockSize = Math.Max(1, SerialBlockSize);
        _centralSyncSettings.SyncIntervalSeconds = Math.Max(5, SyncIntervalSeconds);
    }

    private SerialPortSettings BuildSettings() => new()
    {
        PortName = PortName,
        BaudRate = BaudRate,
        StableReadingCount = StableReadingCount,
        StabilityToleranceKg = StabilityToleranceKg,
        ResetWeightThresholdKg = ResetWeightThresholdKg,
        DataBits = _serialSettings.DataBits,
        Parity = _serialSettings.Parity,
        StopBits = _serialSettings.StopBits,
        PollIntervalMs = _serialSettings.PollIntervalMs
    };
}
