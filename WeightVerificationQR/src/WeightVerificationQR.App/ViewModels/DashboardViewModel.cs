using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public enum DashboardDisplayState { Idle, Live, Pass, Fail }

public class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IProductRepository _productRepository;
    private readonly IWeighRecordRepository _weighRecordRepository;
    private readonly ISerialPortService _serialPortService;
    private readonly IPrinterService _printerService;
    private readonly IWeighingEngine _weighingEngine;
    private readonly SerialPortSettings _serialSettings;
    private readonly SessionContext _session;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _autoReturnTimer;

    public DashboardViewModel(
        IProductRepository productRepository,
        IWeighRecordRepository weighRecordRepository,
        ISerialPortService serialPortService,
        IPrinterService printerService,
        IWeighingEngine weighingEngine,
        SerialPortSettings serialSettings,
        SessionContext session,
        ILogger<DashboardViewModel> logger)
    {
        _productRepository = productRepository;
        _weighRecordRepository = weighRecordRepository;
        _serialPortService = serialPortService;
        _printerService = printerService;
        _weighingEngine = weighingEngine;
        _serialSettings = serialSettings;
        _session = session;
        _logger = logger;

        Products = new ObservableCollection<Product>();
        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        SelectProductCommand = new RelayCommand<Product>(SelectProduct);
        SimulateWeighingCommand = new AsyncRelayCommand(SimulateWeighingAsync, () => SelectedProduct is not null);

        _serialPortService.WeightReceived += OnWeightReceived;
        _serialPortService.ConnectionStatusChanged += (_, status) => MachineStatus = status;
        _printerService.PrinterStatusChanged += (_, status) => PrinterStatus = status;
        _weighingEngine.WeighingCompleted += OnWeighingCompleted;
        _weighingEngine.CurrentOperator = _session.CurrentUser?.FullName ?? "Unknown";

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => CurrentDateTime = DateTime.Now;
        _clockTimer.Start();

        // After a PASS/FAIL screen is shown, automatically return to the live weighing
        // view so the operator can place the next kit without touching anything.
        _autoReturnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _autoReturnTimer.Tick += (_, _) =>
        {
            _autoReturnTimer.Stop();
            DisplayState = DashboardDisplayState.Live;
        };

        _ = LoadProductsAsync();
        _ = RefreshTodayCountsAsync();
    }

    public ObservableCollection<Product> Products { get; }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            SetProperty(ref _selectedProduct, value);
            _weighingEngine.CurrentProduct = value;
            ((AsyncRelayCommand)SimulateWeighingCommand).NotifyCanExecuteChanged();

            // Switching products should never carry over a stale reading from the previous
            // product - clear the live display so the operator can't mistake it for a fresh weigh.
            LiveWeight = 0m;
            IsStable = false;
            if (DisplayState != DashboardDisplayState.Idle)
                DisplayState = DashboardDisplayState.Live;
        }
    }

    private decimal _liveWeight;
    public decimal LiveWeight { get => _liveWeight; set => SetProperty(ref _liveWeight, value); }

    private bool _isStable;
    public bool IsStable { get => _isStable; set => SetProperty(ref _isStable, value); }

    private DashboardDisplayState _displayState = DashboardDisplayState.Idle;
    public DashboardDisplayState DisplayState { get => _displayState; set => SetProperty(ref _displayState, value); }

    private WeighRecord? _lastRecord;
    public WeighRecord? LastRecord { get => _lastRecord; set => SetProperty(ref _lastRecord, value); }

    private WeighRecord? _lastPrintedQr;
    public WeighRecord? LastPrintedQr { get => _lastPrintedQr; set => SetProperty(ref _lastPrintedQr, value); }

    private ConnectionStatus _machineStatus = ConnectionStatus.Disconnected;
    public ConnectionStatus MachineStatus { get => _machineStatus; set => SetProperty(ref _machineStatus, value); }

    private ConnectionStatus _printerStatus = ConnectionStatus.Disconnected;
    public ConnectionStatus PrinterStatus { get => _printerStatus; set => SetProperty(ref _printerStatus, value); }

    private ConnectionStatus _databaseStatus = ConnectionStatus.Connected;
    public ConnectionStatus DatabaseStatus { get => _databaseStatus; set => SetProperty(ref _databaseStatus, value); }

    private int _totalPassToday;
    public int TotalPassToday { get => _totalPassToday; set => SetProperty(ref _totalPassToday, value); }

    private int _totalFailToday;
    public int TotalFailToday { get => _totalFailToday; set => SetProperty(ref _totalFailToday, value); }

    private DateTime _currentDateTime = DateTime.Now;
    public DateTime CurrentDateTime { get => _currentDateTime; set => SetProperty(ref _currentDateTime, value); }

    private decimal _simulatedWeightInput = 1.030m;
    public decimal SimulatedWeightInput { get => _simulatedWeightInput; set => SetProperty(ref _simulatedWeightInput, value); }

    public string OperatorName => _session.CurrentUser?.FullName ?? "Unknown";

    public IAsyncRelayCommand ConnectCommand { get; }
    public ICommand SelectProductCommand { get; }
    public IAsyncRelayCommand SimulateWeighingCommand { get; }

    private async Task LoadProductsAsync()
    {
        var products = await _productRepository.GetAllAsync();
        Products.Clear();
        foreach (var p in products) Products.Add(p);
        if (Products.Count > 0) SelectedProduct = Products[0];
    }

    private void SelectProduct(Product? product) => SelectedProduct = product;

    private async Task ConnectAsync()
    {
        try
        {
            await _serialPortService.ConnectAsync(_serialSettings);
            DisplayState = DashboardDisplayState.Live;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to weighing machine on {Port}", _serialSettings.PortName);
        }
    }

    /// <summary>
    /// Lets an operator or evaluator exercise the full PASS/FAIL/QR/print pipeline without a
    /// physical scale connected — types a weight, this method feeds it into WeighingEngine
    /// exactly as a real "stable" reading from the serial port would. Useful for demos, UAT,
    /// and training before the actual weighing indicator is wired up on the production floor.
    /// </summary>
    private async Task SimulateWeighingAsync()
    {
        if (SelectedProduct is null) return;

        LiveWeight = SimulatedWeightInput;
        IsStable = true;
        DisplayState = DashboardDisplayState.Live;

        await _weighingEngine.ProcessStableWeightAsync(SimulatedWeightInput);
    }

    private async void OnWeightReceived(object? sender, WeightReadingEventArgs e)
    {
        // Marshal back to the UI thread since this event fires from the SerialPort background thread.
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            LiveWeight = e.WeightKg;
            IsStable = e.IsStable;

            if (e.IsStable && DisplayState == DashboardDisplayState.Live)
            {
                await _weighingEngine.ProcessStableWeightAsync(e.WeightKg);
            }
        });
    }

    private async void OnWeighingCompleted(object? sender, WeighingCompletedEventArgs e)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            LastRecord = e.Record;
            DisplayState = e.Record.Result == WeighResult.Pass ? DashboardDisplayState.Pass : DashboardDisplayState.Fail;

            if (e.Record.Result == WeighResult.Pass)
                LastPrintedQr = e.Record;

            _autoReturnTimer.Stop();
            _autoReturnTimer.Start();
        });

        await RefreshTodayCountsAsync();
    }

    private async Task RefreshTodayCountsAsync()
    {
        var (pass, fail) = await _weighRecordRepository.GetTodayCountsAsync();
        TotalPassToday = pass;
        TotalFailToday = fail;
    }

    public void Dispose()
    {
        _serialPortService.WeightReceived -= OnWeightReceived;
        _weighingEngine.WeighingCompleted -= OnWeighingCompleted;
        _clockTimer.Stop();
        _autoReturnTimer.Stop();
    }
}
