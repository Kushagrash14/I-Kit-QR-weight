namespace WeightVerificationQR.Core.Models;

public class SerialPortSettings
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";

    /// <summary>How many consecutive identical readings (within tolerance) count as "stable".</summary>
    public int StableReadingCount { get; set; } = 5;

    /// <summary>Max allowed fluctuation (kg) between readings to still be considered stable.</summary>
    public decimal StabilityToleranceKg { get; set; } = 0.002m;

    /// <summary>
    /// After a stable kit is accepted, the scale must fall to or below this weight before
    /// another kit can be accepted. This prevents one stationary kit from printing repeatedly.
    /// </summary>
    public decimal ResetWeightThresholdKg { get; set; } = 0.050m;

    /// <summary>Milliseconds between polls if the scale doesn't push data continuously.</summary>
    public int PollIntervalMs { get; set; } = 200;
}

public class PrinterSettings
{
    public PrinterType PrinterType { get; set; } = PrinterType.Zebra;
    public PrinterConnectionMode ConnectionMode { get; set; } = PrinterConnectionMode.BarTender;

    // Network printer
    public string IpAddress { get; set; } = "192.168.1.100";
    public int Port { get; set; } = 9100;

    // Serial/USB printer
    public string ComPort { get; set; } = "COM2";
    public int BaudRate { get; set; } = 9600;

    // Windows print queue (used when ConnectionMode = LocalWindowsPrintQueue)
    public string WindowsPrinterName { get; set; } = string.Empty;

    // BarTender (used when ConnectionMode = BarTender)
    public string BarTenderApiUrl { get; set; } = "http://localhost:5159/api";
    public string BarTenderPrinterName { get; set; } = string.Empty;
    public string BarTenderExePath { get; set; } = string.Empty;
    public string BarTenderLabelPath { get; set; } = @"Labels\Template.btw";

    /// <summary>"api", "cmd", or "auto" (try API first, then direct BarTender command fallback).</summary>
    public string BarTenderPrintMethod { get; set; } = "auto";

    public int LabelWidthMm { get; set; } = 50;
    public int LabelHeightMm { get; set; } = 30;
    public int DpiSetting { get; set; } = 203;
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = "Data Source=WeightVerificationQR.db";
    public string BackupFolderPath { get; set; } = @"C:\WeightVerificationQR\Backups";
    public bool AutoBackupEnabled { get; set; } = true;
    public int AutoBackupIntervalHours { get; set; } = 24;
}

public class StationSettings
{
    public string QrPrefix { get; set; } = "P";
    public string SiteCode { get; set; } = "S01";
    public string LineCode { get; set; } = "L01";
    public string MachineCode { get; set; } = "WM01";
    public int SerialDigits { get; set; } = 8;

    /// <summary>
    /// Emergency local range used only when no central block is available. Configure a
    /// different range per station. MachineCode still keeps the complete QR globally unique.
    /// </summary>
    public long EmergencySerialStart { get; set; } = 90_000_001;
}

public class CentralSyncSettings
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public int SerialBlockSize { get; set; } = 1000;
    public int SyncIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 100;
}
