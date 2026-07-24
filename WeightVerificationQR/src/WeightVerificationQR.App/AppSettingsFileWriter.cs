using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App;

/// <summary>
/// The Settings screens edit SerialPortSettings/PrinterSettings objects that are registered
/// as DI singletons, so changes take effect immediately for the running session (Save/Test
/// buttons work right away). This class additionally persists those same values back into
/// appsettings.json so they're still there the next time the app starts - without it, a
/// restart would silently revert to whatever shipped in appsettings.json originally.
/// </summary>
public static class AppSettingsFileWriter
{
    private static string SettingsFilePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public static void SaveSerialPortSettings(SerialPortSettings settings)
    {
        UpdateSection("SerialPort", node =>
        {
            node["PortName"] = settings.PortName;
            node["BaudRate"] = settings.BaudRate;
            node["DataBits"] = settings.DataBits;
            node["Parity"] = settings.Parity;
            node["StopBits"] = settings.StopBits;
            node["StableReadingCount"] = settings.StableReadingCount;
            node["StabilityToleranceKg"] = settings.StabilityToleranceKg;
            node["ResetWeightThresholdKg"] = settings.ResetWeightThresholdKg;
            node["PollIntervalMs"] = settings.PollIntervalMs;
        });
    }

    public static void SavePrinterSettings(PrinterSettings settings)
    {
        UpdateSection("Printer", node =>
        {
            node["PrinterType"] = settings.PrinterType.ToString();
            node["ConnectionMode"] = settings.ConnectionMode.ToString();
            node["IpAddress"] = settings.IpAddress;
            node["Port"] = settings.Port;
            node["ComPort"] = settings.ComPort;
            node["BaudRate"] = settings.BaudRate;
            node["WindowsPrinterName"] = settings.WindowsPrinterName;
            node["LabelWidthMm"] = settings.LabelWidthMm;
            node["LabelHeightMm"] = settings.LabelHeightMm;
            node["DpiSetting"] = settings.DpiSetting;
            node["BarTenderApiUrl"] = settings.BarTenderApiUrl;
            node["BarTenderPrinterName"] = settings.BarTenderPrinterName;
            node["BarTenderExePath"] = settings.BarTenderExePath;
            node["BarTenderLabelPath"] = settings.BarTenderLabelPath;
            node["BarTenderPrintMethod"] = settings.BarTenderPrintMethod;
        });
    }

    /// <summary>
    /// Reads appsettings.json as a mutable JSON tree, applies <paramref name="apply"/> to the
    /// named section (creating it if missing), and writes the file back out formatted.
    /// Any write failure (locked file, missing permissions, etc.) is swallowed here — the
    /// in-memory settings object was already updated by the caller, so the running session is
    /// unaffected either way; only persistence across a restart is at risk, which the caller
    /// surfaces via its own StatusMessage if needed.
    /// </summary>
    private static void UpdateSection(string sectionName, Action<JsonObject> apply)
    {
        try
        {
            var path = SettingsFilePath;
            var json = File.Exists(path) ? File.ReadAllText(path) : "{}";
            var root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();

            if (root[sectionName] is not JsonObject section)
            {
                section = new JsonObject();
                root[sectionName] = section;
            }

            apply(section);

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, root.ToJsonString(options));
        }
        catch
        {
            // Best-effort persistence only - see remarks above. Nothing to recover here;
            // the current session already has the new values in memory.
        }
    }
}
