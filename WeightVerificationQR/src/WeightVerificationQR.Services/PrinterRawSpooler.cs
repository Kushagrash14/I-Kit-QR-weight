using System.Runtime.InteropServices;

namespace WeightVerificationQR.Services;

/// <summary>
/// Sends raw bytes directly to a printer's spool queue via winspool.drv, bypassing GDI.
/// This is the standard technique for pushing ZPL/TSPL/EPL through a Windows-installed
/// label printer driver (e.g. "ZDesigner GK420t") when using the "Generic / Text Only"
/// or manufacturer raw driver. Windows-only - guarded by OS checks at the call site.
/// </summary>
internal static class PrinterRawSpooler
{
    private const uint PrinterStatusPaused = 0x00000001;
    private const uint PrinterStatusError = 0x00000002;
    private const uint PrinterStatusPendingDeletion = 0x00000004;
    private const uint PrinterStatusPaperJam = 0x00000008;
    private const uint PrinterStatusPaperOut = 0x00000010;
    private const uint PrinterStatusManualFeed = 0x00000020;
    private const uint PrinterStatusPaperProblem = 0x00000040;
    private const uint PrinterStatusOffline = 0x00000080;
    private const uint PrinterStatusIoActive = 0x00000100;
    private const uint PrinterStatusBusy = 0x00000200;
    private const uint PrinterStatusPrinting = 0x00000400;
    private const uint PrinterStatusOutputBinFull = 0x00000800;
    private const uint PrinterStatusNotAvailable = 0x00001000;
    private const uint PrinterStatusWaiting = 0x00002000;
    private const uint PrinterStatusProcessing = 0x00004000;
    private const uint PrinterStatusInitializing = 0x00008000;
    private const uint PrinterStatusWarmingUp = 0x00010000;
    private const uint PrinterStatusTonerLow = 0x00020000;
    private const uint PrinterStatusNoToner = 0x00040000;
    private const uint PrinterStatusPagePunt = 0x00080000;
    private const uint PrinterStatusUserIntervention = 0x00100000;
    private const uint PrinterStatusOutOfMemory = 0x00200000;
    private const uint PrinterStatusDoorOpen = 0x00400000;
    private const uint PrinterStatusServerUnknown = 0x00800000;
    private const uint PrinterStatusPowerSave = 0x01000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PRINTER_INFO_6
    {
        public uint dwStatus;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFOA di);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [DllImport("winspool.drv", EntryPoint = "GetPrinterW", SetLastError = true)]
    private static extern bool GetPrinter(
        IntPtr hPrinter,
        uint level,
        IntPtr pPrinter,
        uint cbBuf,
        out uint pcbNeeded);

    public static bool IsPrinterReady(string printerName, out string statusMessage)
    {
        statusMessage = string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            statusMessage = "Windows printer queues are only available on Windows.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(printerName))
        {
            statusMessage = "Printer name is empty.";
            return false;
        }

        if (!OpenPrinter(printerName.Trim(), out var hPrinter, IntPtr.Zero))
        {
            statusMessage = $"Printer queue '{printerName}' was not found or could not be opened.";
            return false;
        }

        try
        {
            var size = (uint)Marshal.SizeOf<PRINTER_INFO_6>();
            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (!GetPrinter(hPrinter, 6, buffer, size, out _))
                {
                    // Some Zebra/Seagull drivers accept jobs but do not expose
                    // PRINTER_INFO_6. OpenPrinter already proved the queue exists.
                    statusMessage = "Installed; live status is unavailable from the printer driver.";
                    return true;
                }

                var status = Marshal.PtrToStructure<PRINTER_INFO_6>(buffer).dwStatus;
                var blockingStatus = status & (
                    PrinterStatusPaused |
                    PrinterStatusError |
                    PrinterStatusPendingDeletion |
                    PrinterStatusPaperJam |
                    PrinterStatusPaperOut |
                    PrinterStatusManualFeed |
                    PrinterStatusPaperProblem |
                    PrinterStatusOffline |
                    PrinterStatusOutputBinFull |
                    PrinterStatusNotAvailable |
                    PrinterStatusNoToner |
                    PrinterStatusUserIntervention |
                    PrinterStatusOutOfMemory |
                    PrinterStatusDoorOpen |
                    PrinterStatusServerUnknown);

                if (blockingStatus != 0)
                {
                    statusMessage = DescribeStatus(blockingStatus);
                    return false;
                }

                statusMessage = status == 0
                    ? "Ready"
                    : DescribeStatus(status);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    public static bool SendBytesToPrinter(string printerName, byte[] bytes)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Raw printer spooling requires Windows.");

        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            throw new InvalidOperationException($"Unable to open printer '{printerName}'.");

        try
        {
            var di = new DOCINFOA
            {
                pDocName = "WeightVerificationQR Label",
                pDataType = "RAW"
            };

            if (!StartDocPrinter(hPrinter, 1, ref di))
                throw new InvalidOperationException("StartDocPrinter failed.");

            try
            {
                StartPagePrinter(hPrinter);

                var unmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, unmanagedBytes, bytes.Length);
                    var ok = WritePrinter(hPrinter, unmanagedBytes, bytes.Length, out var written);
                    if (!ok || written != bytes.Length)
                        throw new InvalidOperationException(
                            $"Windows spooler accepted {written} of {bytes.Length} print bytes.");
                    return true;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(unmanagedBytes);
                    EndPagePrinter(hPrinter);
                }
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    private static string DescribeStatus(uint status)
    {
        var states = new List<string>();
        AddIf(PrinterStatusPaused, "Paused");
        AddIf(PrinterStatusError, "Error");
        AddIf(PrinterStatusPendingDeletion, "Pending deletion");
        AddIf(PrinterStatusPaperJam, "Paper jam");
        AddIf(PrinterStatusPaperOut, "Paper out");
        AddIf(PrinterStatusManualFeed, "Manual feed required");
        AddIf(PrinterStatusPaperProblem, "Paper problem");
        AddIf(PrinterStatusOffline, "Offline");
        AddIf(PrinterStatusIoActive, "I/O active");
        AddIf(PrinterStatusBusy, "Busy");
        AddIf(PrinterStatusPrinting, "Printing");
        AddIf(PrinterStatusOutputBinFull, "Output bin full");
        AddIf(PrinterStatusNotAvailable, "Not available");
        AddIf(PrinterStatusWaiting, "Waiting");
        AddIf(PrinterStatusProcessing, "Processing");
        AddIf(PrinterStatusInitializing, "Initializing");
        AddIf(PrinterStatusWarmingUp, "Warming up");
        AddIf(PrinterStatusTonerLow, "Toner low");
        AddIf(PrinterStatusNoToner, "No toner");
        AddIf(PrinterStatusPagePunt, "Page punt");
        AddIf(PrinterStatusUserIntervention, "User intervention required");
        AddIf(PrinterStatusOutOfMemory, "Out of memory");
        AddIf(PrinterStatusDoorOpen, "Door open");
        AddIf(PrinterStatusServerUnknown, "Server status unknown");
        AddIf(PrinterStatusPowerSave, "Power save");
        return states.Count == 0 ? $"Status 0x{status:X8}" : string.Join(", ", states);

        void AddIf(uint flag, string text)
        {
            if ((status & flag) != 0)
                states.Add(text);
        }
    }
}
