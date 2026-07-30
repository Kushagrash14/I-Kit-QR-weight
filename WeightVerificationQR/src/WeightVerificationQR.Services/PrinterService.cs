using System.Diagnostics;
using System.IO.Ports;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;
using WindowsPrinterSettings = System.Drawing.Printing.PrinterSettings;

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
    public string LastErrorMessage { get; private set; } = string.Empty;

    public IReadOnlyList<string> GetInstalledPrinterNames()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        try
        {
            return WindowsPrinterSettings.InstalledPrinters
                .Cast<string>()
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

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
        sb.AppendLine($"^FDQA,{Escape(record.QrPayload)}^FS");

        // Human-readable text block to the right of the QR
        var textX = (int)(widthDots * 0.42);
        sb.AppendLine($"^FO{textX},25^A0N,22,22^FD{Escape(record.KitNumber)}^FS");
        sb.AppendLine($"^FO{textX},55^A0N,20,20^FD{Escape(record.LabelSizeText)}^FS");
        sb.AppendLine($"^FO{textX},85^A0N,20,20^FD{Escape(record.LabelLengthText)}^FS");
        sb.AppendLine($"^FO{textX},115^A0N,20,20^FD{Escape(record.LabelMaterialText)}^FS");
        sb.AppendLine($"^FO{textX},145^A0N,18,18^FD{Escape(record.ModelCode)} | {record.WeightKg:0.000} kg^FS");

        sb.AppendLine("^XZ");
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("^", "").Replace("~", "");

    public async Task<bool> PrintLabelAsync(WeighRecord record, PrinterSettings settings)
    {
        LastErrorMessage = string.Empty;

        if (settings.ConnectionMode == PrinterConnectionMode.BarTender)
        {
            try
            {
                var ok = await PrintViaBarTenderAsync(record, settings);
                SetStatus(ok ? ConnectionStatus.Connected : ConnectionStatus.Error);
                return ok;
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
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
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            SetStatus(ConnectionStatus.Error);
            return false;
        }
    }

    /// <summary>
    /// Prints via Seagull BarTender, mirroring backend-correct/bartender.js: checks live printer
    /// status first, then tries the BarTender REST API (if method is "api"/"auto"), and falls back
    /// to invoking bartend.exe directly with a CSV data source and the configured .btw template
    /// (if method is "cmd"/"auto"). Named data and CSV columns map the stored QR payload,
    /// generated kit number, and product-specific label text into the .btw template.
    /// </summary>
    private async Task<bool> PrintViaBarTenderAsync(WeighRecord record, PrinterSettings settings)
    {
        var apiUrl = string.IsNullOrWhiteSpace(settings.BarTenderApiUrl) ? "http://localhost:5159/api" : settings.BarTenderApiUrl.TrimEnd('/');
        var printerName = settings.BarTenderPrinterName?.Trim() ?? string.Empty;
        var method = NormalizeBarTenderMethod(settings.BarTenderPrintMethod);
        if (string.IsNullOrWhiteSpace(printerName))
        {
            LastErrorMessage = "No printer is selected. Select an installed Windows printer queue in Printer Settings.";
            return false;
        }

        // Strict connectivity check: don't accept the print job if BarTender reports the printer offline.
        if (method is "api" or "auto")
        {
            try
            {
                var statusRes = await _httpClient.GetAsync($"{apiUrl}/status?printer={Uri.EscapeDataString(printerName)}");
                if (statusRes.IsSuccessStatusCode)
                {
                    var statusText = await ReadPrinterStatusAsync(statusRes);
                    if (IsOfflineStatus(statusText))
                    {
                        LastErrorMessage = $"BarTender reports printer status '{statusText}'.";
                        return false;
                    }
                }
            }
            catch
            {
                // API can be unavailable in CMD/auto mode. The command path is checked below.
            }
        }

        if (method is "api" or "auto")
        {
            try
            {
                var payload = new
                {
                    Printer = printerName,
                    NamedDataSources = BuildBarTenderData(record),
                    Variables = BuildBarTenderData(record)
                };

                var response = await _httpClient.PostAsJsonAsync($"{apiUrl}/print", payload);
                if (response.IsSuccessStatusCode)
                    return true;

                LastErrorMessage = $"BarTender API print failed with HTTP {(int)response.StatusCode}.";
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"BarTender API print failed: {ex.Message}";
            }
        }

        if (method is "cmd" or "auto")
            return await RunBarTenderCmdAsync(record, settings, printerName);

        return false;
    }

    private async Task<bool> RunBarTenderCmdAsync(
        WeighRecord record,
        PrinterSettings settings,
        string printerName)
    {
        var exePath = ResolveBarTenderExePath(settings);
        var labelPath = ResolveExistingBarTenderLabelPath(settings);
        if (exePath is null)
        {
            LastErrorMessage = "BarTender executable was not found. Set BARTENDER.EXE PATH in Printer Settings.";
            return false;
        }

        if (!File.Exists(labelPath))
        {
            LastErrorMessage = $"BarTender template was not found: {labelPath}";
            return false;
        }

        if (!PrinterRawSpooler.IsPrinterReady(printerName, out var printerStatus))
        {
            LastErrorMessage = $"Printer '{printerName}' is not ready: {printerStatus}";
            return false;
        }

        var dataDir = Path.Combine(Path.GetTempPath(), "WeightVerificationQR", "BarTender");
        Directory.CreateDirectory(dataDir);

        var dataPath = Path.Combine(dataDir, $"wvqr-{Guid.NewGuid():N}.csv");
        var csv = BuildBarTenderCsv(record);
        File.WriteAllText(dataPath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in BuildBarTenderArguments(labelPath, dataPath, printerName))
                psi.ArgumentList.Add(argument);

            using var process = Process.Start(psi);
            if (process is null)
            {
                LastErrorMessage = "Windows could not start BarTender.";
                return false;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                LastErrorMessage = "BarTender did not complete the print command within 30 seconds.";
                return false;
            }

            if (process.ExitCode != 0)
            {
                LastErrorMessage = $"BarTender exited with code {process.ExitCode}.";
                return false;
            }

            return true;
        }
        finally
        {
            try { File.Delete(dataPath); } catch { }
        }
    }

    private static object BuildBarTenderData(WeighRecord record) => new
    {
        QRCode = record.QrPayload,
        SAPCode = record.KitNumber,
        Description = record.LabelSizeText,
        Size = record.LabelSizeText,
        Length = record.LabelLengthText,
        Material = record.LabelMaterialText,
        ModelName = record.ProductName,
        ModelCode = record.ModelCode,
        CommandCode = record.CommandCode,
        Weight = record.WeightKg.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
        PrintDate = record.RecordDate.ToString("ddMMyy"),
        Site = record.SiteCode,
        Line = record.LineCode,
        Machine = record.MachineCode,
        SerialNumber = record.DailySerialNumber.ToString("D6")
    };

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

    internal static string ResolveExistingBarTenderLabelPath(
        PrinterSettings settings,
        string? baseDirectory = null)
    {
        var applicationDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        var configuredPath = ResolveBarTenderLabelPath(settings, applicationDirectory);
        if (File.Exists(configuredPath))
            return configuredPath;

        var labelsDirectory = Path.Combine(applicationDirectory, "Labels");
        var configuredFileName = Path.GetFileName(configuredPath);
        if (!string.IsNullOrWhiteSpace(configuredFileName))
        {
            var packagedSameName = Path.Combine(labelsDirectory, configuredFileName);
            if (File.Exists(packagedSameName))
                return Path.GetFullPath(packagedSameName);
        }

        if (Directory.Exists(labelsDirectory))
        {
            var firstPackagedTemplate = Directory
                .EnumerateFiles(labelsDirectory, "*.btw", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            if (firstPackagedTemplate is not null)
                return Path.GetFullPath(firstPackagedTemplate);
        }

        return configuredPath;
    }

    internal static string[] BuildBarTenderArguments(string labelPath, string dataPath, string printerName) =>
    [
        $"/AF={labelPath}",
        $"/D={dataPath}",
        $"/PRN={printerName}",
        "/P",
        "/X"
    ];

    internal static string BuildBarTenderCsv(WeighRecord record) =>
        "QRCode,SAPCode,Description,Size,Length,Material,ModelName,ModelCode,CommandCode,Weight,PrintDate,Line,SerialNumber\r\n" +
        string.Join(
            ',',
            CsvCell(record.QrPayload),
            CsvCell(record.KitNumber),
            CsvCell(record.LabelSizeText),
            CsvCell(record.LabelSizeText),
            CsvCell(record.LabelLengthText),
            CsvCell(record.LabelMaterialText),
            CsvCell(record.ProductName),
            CsvCell(record.ModelCode),
            CsvCell(record.CommandCode),
            CsvCell(record.WeightKg.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)),
            CsvCell(record.RecordDate.ToString("ddMMyy")),
            CsvCell(record.LineCode),
            CsvCell(record.DailySerialNumber.ToString("D6")));

    private static string CsvCell(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    public async Task<bool> TestConnectionAsync(PrinterSettings settings)
    {
        LastErrorMessage = string.Empty;
        SetStatus(ConnectionStatus.Connecting);

        try
        {
            if (settings.ConnectionMode == PrinterConnectionMode.BarTender)
            {
                var method = NormalizeBarTenderMethod(settings.BarTenderPrintMethod);
                var exePath = ResolveBarTenderExePath(settings);
                var labelPath = ResolveExistingBarTenderLabelPath(settings);
                var printerName = settings.BarTenderPrinterName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    LastErrorMessage = "No printer is selected. Select an installed Windows printer queue.";
                    SetStatus(ConnectionStatus.Error);
                    return false;
                }

                var queueReady = PrinterRawSpooler.IsPrinterReady(printerName, out var queueStatus);
                var cmdReady = exePath is not null &&
                               File.Exists(labelPath) &&
                               queueReady;
                var apiReady = false;
                var apiStatus = string.Empty;

                if (method is "api" or "auto")
                {
                    try
                    {
                        var apiUrl = string.IsNullOrWhiteSpace(settings.BarTenderApiUrl)
                            ? "http://localhost:5159/api"
                            : settings.BarTenderApiUrl.TrimEnd('/');
                        var response = await _httpClient.GetAsync(
                            $"{apiUrl}/status?printer={Uri.EscapeDataString(printerName)}");
                        if (response.IsSuccessStatusCode)
                        {
                            apiStatus = await ReadPrinterStatusAsync(response);
                            apiReady = !IsOfflineStatus(apiStatus);
                        }
                    }
                    catch (Exception ex)
                    {
                        apiStatus = ex.Message;
                        apiReady = false;
                    }
                }

                var connected = method switch
                {
                    "api" => apiReady,
                    "auto" => apiReady || cmdReady,
                    _ => cmdReady
                };
                if (!connected)
                {
                    LastErrorMessage = method switch
                    {
                        "api" => $"BarTender API/printer is unavailable: {apiStatus}",
                        "auto" => BuildBarTenderReadinessError(exePath, labelPath, queueReady, queueStatus, apiStatus),
                        _ => BuildBarTenderReadinessError(exePath, labelPath, queueReady, queueStatus, null)
                    };
                }
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
                    LastErrorMessage = $"Could not connect to {settings.IpAddress}:{settings.Port} within 3 seconds.";
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

            var ready = PrinterRawSpooler.IsPrinterReady(settings.WindowsPrinterName, out var statusMessage);
            if (!ready)
                LastErrorMessage = statusMessage;
            SetStatus(ready ? ConnectionStatus.Connected : ConnectionStatus.Error);
            return ready;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            SetStatus(ConnectionStatus.Error);
            return false;
        }
    }

    private static async Task SendOverNetworkAsync(byte[] bytes, PrinterSettings settings)
    {
        using var client = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(settings.IpAddress, settings.Port, timeout.Token);
        using var stream = client.GetStream();
        await stream.WriteAsync(bytes, timeout.Token);
        await stream.FlushAsync(timeout.Token);
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

    private static string NormalizeBarTenderMethod(string? method)
    {
        var normalized = method?.Trim().ToLowerInvariant();
        return normalized is "api" or "cmd" or "auto" ? normalized : "auto";
    }

    private static async Task<string> ReadPrinterStatusAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("status", out var statusProp)
            ? statusProp.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool IsOfflineStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Contains("offline") ||
               normalized.Contains("error") ||
               normalized.Contains("not connected") ||
               normalized.Contains("unavailable") ||
               normalized == "paused";
    }

    private static string BuildBarTenderReadinessError(
        string? exePath,
        string labelPath,
        bool queueReady,
        string queueStatus,
        string? apiStatus)
    {
        var errors = new List<string>();
        if (exePath is null)
            errors.Add("BarTender executable not found");
        if (!File.Exists(labelPath))
            errors.Add($"template not found at '{labelPath}'");
        if (!queueReady)
            errors.Add($"printer queue is not ready ({queueStatus})");
        if (!string.IsNullOrWhiteSpace(apiStatus))
            errors.Add($"API status: {apiStatus}");
        return string.Join("; ", errors);
    }
}
