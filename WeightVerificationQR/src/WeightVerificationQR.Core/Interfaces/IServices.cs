using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Core.Interfaces;

/// <summary>Raised every time a raw reading arrives from the scale.</summary>
public class WeightReadingEventArgs : EventArgs
{
    public decimal WeightKg { get; init; }
    public bool IsStable { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public interface ISerialPortService : IDisposable
{
    event EventHandler<WeightReadingEventArgs>? WeightReceived;
    event EventHandler<ConnectionStatus>? ConnectionStatusChanged;

    ConnectionStatus Status { get; }

    /// <summary>Lists all COM ports currently visible to Windows.</summary>
    IEnumerable<string> GetAvailablePorts();

    Task ConnectAsync(SerialPortSettings settings);
    void Disconnect();
}

public interface IQrCodeService
{
    /// <summary>Generates a PNG byte array for the given payload (the Kit Number only).</summary>
    byte[] GenerateQrPng(string payload, int pixelsPerModule = 10);
}

public interface IPrinterService
{
    event EventHandler<ConnectionStatus>? PrinterStatusChanged;
    ConnectionStatus Status { get; }

    /// <summary>Builds the ZPL label content for a PASS record.</summary>
    string BuildZplLabel(WeighRecord record, PrinterSettings settings);

    /// <summary>Sends the label to the physical printer using the configured connection mode.</summary>
    Task<bool> PrintLabelAsync(WeighRecord record, PrinterSettings settings);

    Task<bool> TestConnectionAsync(PrinterSettings settings);
}

public class WeighingCompletedEventArgs : EventArgs
{
    public WeighRecord Record { get; init; } = null!;
}

public interface IWeighingEngine
{
    event EventHandler<WeighingCompletedEventArgs>? WeighingCompleted;

    Product? CurrentProduct { get; set; }
    string CurrentOperator { get; set; }

    /// <summary>Feeds one stable reading into the engine, which evaluates, saves, and (if PASS) prints.</summary>
    Task ProcessStableWeightAsync(decimal weightKg);
}

public interface IReportService
{
    Task<byte[]> ExportToExcelAsync(List<WeighRecord> records);
    Task<byte[]> ExportToPdfAsync(List<WeighRecord> records, string reportTitle);
}

public interface IPasswordHasher
{
    (string hash, string salt) HashPassword(string plainPassword);
    bool Verify(string plainPassword, string hash, string salt);
}

public interface IDatabaseBackupService
{
    Task<string> BackupNowAsync();
    string DatabaseStatusText { get; }
}

public interface ICentralSyncStore
{
    bool IsEnabled { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<SerialNumberBlock?> TryAllocateSerialBlockAsync(
        int blockSize,
        CancellationToken cancellationToken = default);
    Task UpsertWeighRecordAsync(
        WeighRecord record,
        CancellationToken cancellationToken = default);
}

public interface ISerialNumberService
{
    Task<SerialNumberAllocation> GetNextAsync(CancellationToken cancellationToken = default);
    string BuildQrId(
        long serialNumber,
        decimal weightKg,
        DateTime timestamp);
}

public interface IOfflineSyncService
{
    string StatusText { get; }
    void Start();
    Task StopAsync();
    Task SyncNowAsync(CancellationToken cancellationToken = default);
}
