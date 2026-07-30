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

    public static bool SaveSerialPortSettings(SerialPortSettings settings)
    {
        return UpdateSection("SerialPort", node =>
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

    public static bool SavePrinterSettings(PrinterSettings settings)
    {
        return UpdateSection("Printer", node =>
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

    public static bool SaveStationSettings(StationSettings settings)
    {
        return UpdateSection("Station", node =>
        {
            node["QrPrefix"] = settings.QrPrefix;
            node["SiteCode"] = settings.SiteCode;
            node["LineCode"] = settings.LineCode;
            node["MachineCode"] = settings.MachineCode;
            node["SerialDigits"] = settings.SerialDigits;
            node["EmergencySerialStart"] = settings.EmergencySerialStart;
        });
    }

    public static bool SaveCentralSyncSettings(CentralSyncSettings settings)
    {
        return UpdateSection("CentralSync", node =>
        {
            node["Enabled"] = settings.Enabled;
            node["ConnectionString"] = settings.ConnectionString;
            node["SerialBlockSize"] = settings.SerialBlockSize;
            node["SyncIntervalSeconds"] = settings.SyncIntervalSeconds;
            node["BatchSize"] = settings.BatchSize;
        });
    }

    /// <summary>
    /// Reads appsettings.json as a mutable JSON tree, applies <paramref name="apply"/> to the
    /// named section (creating it if missing), and writes the file back out formatted.
    /// The return value tells the caller whether persistence succeeded. The in-memory settings
    /// object is already updated by the caller, so a write failure only affects future restarts.
    /// </summary>
    private static bool UpdateSection(string sectionName, Action<JsonObject> apply)
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
            return true;
        }
        catch
        {
            return false;
        }
    }
}
