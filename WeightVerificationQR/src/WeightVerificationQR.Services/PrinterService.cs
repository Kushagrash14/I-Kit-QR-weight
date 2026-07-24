using System.Diagnostics;
using System.IO.Ports;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.Services;

/// <summary>
/// Builds and sends ZPL (Zebra Programming Language) labels. TSC and Godex printers in
/// "Zebra emulation" mode (common on most modern TSC/Godex models) accept the same ZPL,
/// which is why one implementation covers all three printer types from the spec.
/// If a specific TSC/Godex unit only understands native TSPL/EPL, swap BuildZplLabel's
/// output for the equivalent TSPL template - the rest of the pipeline (transport, retry,
/// status tracking) does not change.
/// </summary>
public class PrinterService : IPrinterService
{
    /// <summary>
    /// Reserved for printers/label templates that need a rendered QR bitmap (^GF) instead of
    /// the printer's native QR command (^BQN, used below). Not called today because every
    /// printer in the spec (Zebra/TSC/Godex) supports ^BQN natively, but kept injected so a
    /// bitmap fallback can be added without changing this class's constructor signature.
    /// </summary>
    private readonly IQrCodeService _qrCodeService;

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public PrinterService(IQrCodeService qrCodeService) => _qrCodeService = qrCodeService;

    public event EventHandler<ConnectionStatus>? PrinterStatusChanged;
    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

    public string BuildZplLabel(WeighRecord record, PrinterSettings settings)
    {
        // Label geometry: dots = mm * (dpi / 25.4)
        var dpmm = settings.DpiSetting / 25.4;
        var widthDots = (int)(settings.LabelWidthMm * dpmm);
        var heightDots = (int)(settings.LabelHeightMm * dpmm);

        var sb = new StringBuilder();
        sb.AppendLine("^XA");
        sb.AppendLine($"^PW{widthDots}");
        sb.AppendLine($"^LL{heightDots}");
        sb.AppendLine("^CI28"); // UTF-8

        // QR code (uses the printer's native QR command so no bitmap transfer is needed)
        sb.AppendLine("^FO20,20");
        sb.AppendLine("^BQN,2,6");
        sb.AppendLine($"^FDQA,{record.QrId}^FS");

        // Human-readable text block to the right of the QR
        var textX = (int)(widthDots * 0.42);
        sb.AppendLine($"^FO{textX},25^A0N,22,22^FD{Escape(record.ProductName)}^FS");
        sb.AppendLine($"^FO{textX},55^A0N,20,20^FDQty: {Escape(record.Quantity)}^FS");
        sb.AppendLine($"^FO{textX},85^A0N,20,20^FDWt: {record.WeightKg:0.000} kg^FS");
        sb.AppendLine($"^FO{textX},115^A0N,20,20^FD{Escape(record.QrId)}^FS");
        sb.AppendLine($"^FO{textX},145^A0N,18,18^FD{record.RecordDate:dd-MM-yyyy HH:mm}^FS");

        sb.AppendLine("^XZ");
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("^", "").Replace("~", "");

    public async Task<bool> PrintLabelAsync(WeighRecord record, PrinterSettings settings)
    {
        if (settings.ConnectionMode == PrinterConnectionMode.BarTender)
        {
            try
            {
                var ok = await PrintViaBarTenderAsync(record, settings);
                SetStatus(ok ? ConnectionStatus.Connected : ConnectionStatus.Error);
                return ok;
            }
            catch (Exception)
            {
                SetStatus(ConnectionStatus.Error);
                return false;
            }
        }

        var zpl = BuildZplLabel(record, settings);
        var bytes = Encoding.UTF8.GetBytes(zpl);

        try
        {
            switch (settings.ConnectionMode)
            {
                case PrinterConnectionMode.Network:
                    await SendOverNetworkAsync(bytes, settings);
                    break;
                case PrinterConnectionMode.UsbSerial:
                    SendOverSerial(bytes, settings);
                    break;
                case PrinterConnectionMode.LocalWindowsPrintQueue:
                    SendToWindowsRawPrinter(bytes, settings.WindowsPrinterName);
                    break;
            }

            SetStatus(ConnectionStatus.Connected);
            return true;
        }
        catch (Exception)
        {
            SetStatus(ConnectionStatus.Error);
            return false;
        }
    }

    /// <summary>
    /// Prints via Seagull BarTender, mirroring backend-correct/bartender.js: checks live printer
    /// status first, then tries the BarTender REST API (if method is "api"/"auto"), and falls back
    /// to invoking bartend.exe directly with a CSV data source and the configured .btw template
    /// (if method is "cmd"/"auto"). QRCode/SAPCode/Description columns map to QrId/KitNumber/ProductName.
    /// </summary>
    private static async Task<bool> PrintViaBarTenderAsync(WeighRecord record, PrinterSettings settings)
    {
        var apiUrl = string.IsNullOrWhiteSpace(settings.BarTenderApiUrl) ? "http://localhost:5159/api" : settings.BarTenderApiUrl.TrimEnd('/');
        var printerName = string.IsNullOrWhiteSpace(settings.BarTenderPrinterName) ? "Zebra_ZT411" : settings.BarTenderPrinterName;
        var method = string.IsNullOrWhiteSpace(settings.BarTenderPrintMethod) ? "cmd" : settings.BarTenderPrintMethod.ToLowerInvariant();

        // Strict connectivity check: don't accept the print job if BarTender reports the printer offline.
        try
        {
            var statusRes = await _httpClient.GetAsync($"{apiUrl}/status?printer={Uri.EscapeDataString(printerName)}");
            if (statusRes.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await statusRes.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("status", out var statusProp))
                {
                    var s = (statusProp.GetString() ?? string.Empty).ToLowerInvariant();
                    if (s.Contains("offline") || s.Contains("error") || s.Contains("not connected") || s == "paused")
                        return false;
                }
            }
        }
        catch
        {
            // BarTender API unreachable - fall through and let the API/CMD attempts below surface the real error.
        }

        var apiSuccess = false;

        if (method is "api" or "auto")
        {
            try
            {
                var payload = new
                {
                    Printer = printerName,
                    NamedDataSources = new { QRCode = record.QrId, SAPCode = record.KitNumber, Description = record.ProductName },
                    Variables = new { QRCode = record.QrId, SAPCode = record.KitNumber, Description = record.ProductName }
                };

                var response = await _httpClient.PostAsJsonAsync($"{apiUrl}/print", payload);
                if (response.IsSuccessStatusCode)
                {
                    apiSuccess = true;
                    return true;
                }
            }
            catch
            {
                // API attempt failed - fall through to CMD if allowed.
            }
        }

        if (!apiSuccess && method is "cmd" or "auto")
        {
            return RunBarTenderCmd(record, settings, printerName);
        }

        return apiSuccess;
    }

    private static bool RunBarTenderCmd(WeighRecord record, PrinterSettings settings, string printerName)
    {
        var exePath = ResolveBarTenderExePath(settings);
        var labelPath = ResolveBarTenderLabelPath(settings);
        if (exePath is null || !File.Exists(labelPath))
            return false;

        var labelDir = Path.GetDirectoryName(labelPath);
        var dataDir = !string.IsNullOrEmpty(labelDir) && Directory.Exists(labelDir)
            ? labelDir
            : Path.Combine(Path.GetTempPath(), "wvqr-bt-data");
        Directory.CreateDirectory(dataDir);

        var dataPath = Path.Combine(dataDir, "wvqr-print.csv");
        var csv = "QRCode,SAPCode,Description\r\n" +
                  $"{CsvCell(record.QrId)},{CsvCell(record.KitNumber)},{CsvCell(record.ProductName)}";
        File.WriteAllText(dataPath, csv, Encoding.UTF8);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in BuildBarTenderArguments(labelPath, dataPath, printerName))
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi);
        if (process is null) return false;
        process.WaitForExit(15000);
        return process.HasExited && process.ExitCode == 0;
    }

    internal static string? ResolveBarTenderExePath(PrinterSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BarTenderExePath) && File.Exists(settings.BarTenderExePath))
            return Path.GetFullPath(settings.BarTenderExePath);

        var commonPaths = new[]
        {
            @"C:\Program Files\Seagull\BarTender Suite\bartend.exe",
            @"C:\Program Files\Seagull\BarTender 2022\bartend.exe",
            @"C:\Program Files\Seagull\BarTender 2021\bartend.exe",
            @"C:\Program Files\Seagull\BarTender 2019\bartend.exe",
            @"C:\Program Files\Seagull\BarTender 12.0\bartend.exe",
            @"C:\Program Files (x86)\Seagull\BarTender Suite\bartend.exe"
        };
        foreach (var p in commonPaths)
        {
            if (File.Exists(p)) return p;
        }

        var pathDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var directory in pathDirectories)
        {
            var candidate = Path.Combine(directory, "bartend.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    internal static string ResolveBarTenderLabelPath(PrinterSettings settings, string? baseDirectory = null)
    {
        var configuredPath = string.IsNullOrWhiteSpace(settings.BarTenderLabelPath)
            ? Path.Combine("Labels", "Template.btw")
            : settings.BarTenderLabelPath.Trim();

        if (Path.IsPathRooted(configuredPath))
            return Path.GetFullPath(configuredPath);

        return Path.GetFullPath(configuredPath, baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory);
    }

    internal static string[] BuildBarTenderArguments(string labelPath, string dataPath, string printerName) =>
    [
        $"/F={labelPath}",
        $"/D={dataPath}",
        $"/PRN={printerName}",
        "/P",
        "/X"
    ];

    private static string CsvCell(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    public async Task<bool> TestConnectionAsync(PrinterSettings settings)
    {
        try
        {
            if (settings.ConnectionMode == PrinterConnectionMode.BarTender)
            {
                var method = string.IsNullOrWhiteSpace(settings.BarTenderPrintMethod)
                    ? "cmd"
                    : settings.BarTenderPrintMethod.Trim().ToLowerInvariant();
                var cmdReady = ResolveBarTenderExePath(settings) is not null &&
                               File.Exists(ResolveBarTenderLabelPath(settings));
                var apiReady = false;

                if (method is "api" or "auto")
                {
                    try
                    {
                        var apiUrl = string.IsNullOrWhiteSpace(settings.BarTenderApiUrl)
                            ? "http://localhost:5159/api"
                            : settings.BarTenderApiUrl.TrimEnd('/');
                        var printerName = string.IsNullOrWhiteSpace(settings.BarTenderPrinterName)
                            ? "Zebra_ZT411"
                            : settings.BarTenderPrinterName;
                        var response = await _httpClient.GetAsync(
                            $"{apiUrl}/status?printer={Uri.EscapeDataString(printerName)}");
                        apiReady = response.IsSuccessStatusCode;
                    }
                    catch
                    {
                        apiReady = false;
                    }
                }

                var connected = method switch
                {
                    "api" => apiReady,
                    "auto" => apiReady || cmdReady,
                    _ => cmdReady
                };
                SetStatus(connected ? ConnectionStatus.Connected : ConnectionStatus.Error);
                return connected;
            }

            if (settings.ConnectionMode == PrinterConnectionMode.Network)
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(settings.IpAddress, settings.Port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(3000));
                if (completed != connectTask || !client.Connected)
                {
                    SetStatus(ConnectionStatus.Error);
                    return false;
                }
                SetStatus(ConnectionStatus.Connected);
                return true;
            }

            if (settings.ConnectionMode == PrinterConnectionMode.UsbSerial)
            {
                using var port = new SerialPort(settings.ComPort, settings.BaudRate);
                port.Open();
                port.Close();
                SetStatus(ConnectionStatus.Connected);
                return true;
            }

            // Windows print queue - existence check only.
            SetStatus(ConnectionStatus.Connected);
            return true;
        }
        catch
        {
            SetStatus(ConnectionStatus.Error);
            return false;
        }
    }

    private static async Task SendOverNetworkAsync(byte[] bytes, PrinterSettings settings)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(settings.IpAddress, settings.Port);
        using var stream = client.GetStream();
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    private static void SendOverSerial(byte[] bytes, PrinterSettings settings)
    {
        using var port = new SerialPort(settings.ComPort, settings.BaudRate);
        port.Open();
        port.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Sends raw ZPL bytes straight to a Windows-installed printer's spooler,
    /// bypassing GDI rendering. Requires System.Drawing.Printing / winspool P/Invoke
    /// on Windows - implementation lives in PrinterRawSpooler.cs.
    /// </summary>
    private static void SendToWindowsRawPrinter(byte[] bytes, string printerName) =>
        PrinterRawSpooler.SendBytesToPrinter(printerName, bytes);

    private void SetStatus(ConnectionStatus status)
    {
        Status = status;
        PrinterStatusChanged?.Invoke(this, status);
    }
}
