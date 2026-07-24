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
    public string BarTenderPrinterName { get; set; } = "ZDesigner ZT231-300dpi ZPL";
    public string BarTenderExePath { get; set; } = string.Empty;
    public string BarTenderLabelPath { get; set; } = @"Labels\Template.btw";

    /// <summary>"api", "cmd", or "auto" (try API first, then direct BarTender command fallback).</summary>
    public string BarTenderPrintMethod { get; set; } = "cmd";

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
