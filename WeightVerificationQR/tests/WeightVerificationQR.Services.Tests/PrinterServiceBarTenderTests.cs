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
    public void ResolveExistingBarTenderLabelPath_FallsBackToPackagedTemplate()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), $"wvqr-app-{Guid.NewGuid():N}");
        var labelsDirectory = Path.Combine(baseDirectory, "Labels");
        Directory.CreateDirectory(labelsDirectory);
        var packagedTemplate = Path.Combine(labelsDirectory, "Template.btw");
        File.WriteAllText(packagedTemplate, "test");

        try
        {
            var settings = new PrinterSettings
            {
                BarTenderLabelPath = @"C:\OldMachine\Missing\Template.btw"
            };

            var resolved = PrinterService.ResolveExistingBarTenderLabelPath(settings, baseDirectory);

            Assert.Equal(Path.GetFullPath(packagedTemplate), resolved);
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void ResolveExistingBarTenderLabelPath_PrefersMatchingPackagedFileName()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), $"wvqr-app-{Guid.NewGuid():N}");
        var labelsDirectory = Path.Combine(baseDirectory, "Labels");
        Directory.CreateDirectory(labelsDirectory);
        File.WriteAllText(Path.Combine(labelsDirectory, "Template.btw"), "default");
        var matchingTemplate = Path.Combine(labelsDirectory, "New Document.btw");
        File.WriteAllText(matchingTemplate, "selected");

        try
        {
            var settings = new PrinterSettings
            {
                BarTenderLabelPath = @"C:\OldMachine\Labels\New Document.btw"
            };

            var resolved = PrinterService.ResolveExistingBarTenderLabelPath(settings, baseDirectory);

            Assert.Equal(Path.GetFullPath(matchingTemplate), resolved);
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void BuildBarTenderArguments_UsesActiveFormatDataPrinterPrintAndExitSwitches()
    {
        var arguments = PrinterService.BuildBarTenderArguments(
            @"C:\App\Labels\Template.btw",
            @"C:\Temp\wvqr-print.csv",
            "Plant Label Printer");

        Assert.Contains(@"/AF=C:\App\Labels\Template.btw", arguments);
        Assert.Contains(@"/D=C:\Temp\wvqr-print.csv", arguments);
        Assert.Contains("/PRN=Plant Label Printer", arguments);
        Assert.Contains("/P", arguments);
        Assert.Contains("/X", arguments);
    }

    [Fact]
    public void BuildBarTenderCsv_MapsDynamicFieldsAndEscapesQuotes()
    {
        var record = new WeighRecord
        {
            QrPayload = "KIT=P-O-290726-1064-000002|MODEL=ODU-A",
            KitNumber = "P-O-290726-1064-000002",
            ProductName = "ODU Model A",
            LabelSizeText = "5/8\" & 3/8\"",
            LabelLengthText = "3 Meter",
            LabelMaterialText = "EPE",
            ModelCode = "ODU-A",
            CommandCode = "P",
            WeightKg = 1.064m,
            RecordDate = new DateTime(2026, 7, 29),
            LineCode = "O",
            DailySerialNumber = 2
        };

        var csv = PrinterService.BuildBarTenderCsv(record);

        Assert.Contains("\"KIT=P-O-290726-1064-000002|MODEL=ODU-A\"", csv);
        Assert.Contains("\"P-O-290726-1064-000002\"", csv);
        Assert.Contains("\"5/8\"\" & 3/8\"\"\"", csv);
        Assert.Contains("\"1.064\"", csv);
        Assert.EndsWith("\"O\",\"000002\"", csv);
    }
}
