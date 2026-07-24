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
    [StructLayout(LayoutKind.Sequential)]
    private struct DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
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
                    var ok = WritePrinter(hPrinter, unmanagedBytes, bytes.Length, out _);
                    return ok;
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
}
