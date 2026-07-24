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

    public MachineSettingsViewModel(ISerialPortService serialPortService, SerialPortSettings serialSettings)
    {
        _serialPortService = serialPortService;
        _serialSettings = serialSettings;

        AvailablePorts = new ObservableCollection<string>(_serialPortService.GetAvailablePorts());

        PortName = _serialSettings.PortName;
        BaudRate = _serialSettings.BaudRate;
        StableReadingCount = _serialSettings.StableReadingCount;
        StabilityToleranceKg = _serialSettings.StabilityToleranceKg;
        ResetWeightThresholdKg = _serialSettings.ResetWeightThresholdKg;

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
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

    private ConnectionStatus _lastTestResult = ConnectionStatus.Disconnected;
    public ConnectionStatus LastTestResult { get => _lastTestResult; set => SetProperty(ref _lastTestResult, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public ICommand RefreshPortsCommand { get; }
    public IAsyncRelayCommand TestConnectionCommand { get; }
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
        var updated = BuildSettings();
        _serialSettings.PortName = updated.PortName;
        _serialSettings.BaudRate = updated.BaudRate;
        _serialSettings.StableReadingCount = updated.StableReadingCount;
        _serialSettings.StabilityToleranceKg = updated.StabilityToleranceKg;
        _serialSettings.ResetWeightThresholdKg = updated.ResetWeightThresholdKg;
        AppSettingsFileWriter.SaveSerialPortSettings(_serialSettings);
        StatusMessage = "Machine settings saved and will persist across restarts.";
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
