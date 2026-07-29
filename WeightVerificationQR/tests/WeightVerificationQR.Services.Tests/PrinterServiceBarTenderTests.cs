using WeightVerificationQR.Core.Models;
using WeightVerificationQR.Services;
using Xunit;

namespace WeightVerificationQR.Services.Tests;

public class PrinterServiceBarTenderTests
{
    [Fact]
    public void ResolveBarTenderLabelPath_UsesApplicationDirectoryForRelativeTemplate()
    {
        var settings = new PrinterSettings { BarTenderLabelPath = @"Labels\Template.btw" };
        var baseDirectory = Path.Combine(Path.GetTempPath(), "wvqr-app");

        var resolved = PrinterService.ResolveBarTenderLabelPath(settings, baseDirectory);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(baseDirectory, "Labels", "Template.btw")),
            resolved);
    }

    [Fact]
    public void BuildBarTenderArguments_UsesActiveFormatDataPrinterPrintAndExitSwitches()
    {
        var arguments = PrinterService.BuildBarTenderArguments(
            @"C:\App\Labels\Template.btw",
            @"C:\Temp\wvqr-print.csv",
            "ZDesigner ZT231-300dpi ZPL");

        Assert.Contains(@"/AF=C:\App\Labels\Template.btw", arguments);
        Assert.Contains(@"/D=C:\Temp\wvqr-print.csv", arguments);
        Assert.Contains("/PRN=ZDesigner ZT231-300dpi ZPL", arguments);
        Assert.Contains("/P", arguments);
        Assert.Contains("/X", arguments);
    }

    [Fact]
    public void BuildBarTenderCsv_MapsDynamicFieldsAndEscapesQuotes()
    {
        var record = new WeighRecord
        {
            QrId = "QR-1001",
            KitNumber = "SAP-2002",
            ProductName = "EPE \"Gray\""
        };

        var csv = PrinterService.BuildBarTenderCsv(record);

        Assert.Equal(
            "QRCode,SAPCode,Description\r\n\"QR-1001\",\"SAP-2002\",\"EPE \"\"Gray\"\"\"",
            csv);
    }
}
