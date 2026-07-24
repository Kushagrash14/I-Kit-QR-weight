using WeightVerificationQR.Core.Models;
using WeightVerificationQR.Services;
using Xunit;

namespace WeightVerificationQR.Services.Tests;

public class PrinterServiceZplTests
{
    private static WeighRecord SampleRecord() => new()
    {
        KitNumber = "KIT202607110001",
        ProductName = "I Kit 12 mm & 6 mm EPE",
        Quantity = "100 Nos",
        WeightKg = 1.032m,
        Result = WeighResult.Pass,
        QrId = "KIT202607110001",
        RecordDate = new DateTime(2026, 7, 11, 14, 30, 0)
    };

    private static PrinterSettings SampleSettings() => new()
    {
        LabelWidthMm = 50,
        LabelHeightMm = 30,
        DpiSetting = 203
    };

    [Fact]
    public void BuildZplLabel_StartsAndEndsWithZplFrameCommands()
    {
        var service = new PrinterService(new QrCodeService());
        var zpl = service.BuildZplLabel(SampleRecord(), SampleSettings());

        Assert.StartsWith("^XA", zpl.TrimStart());
        Assert.Contains("^XZ", zpl);
    }

    [Fact]
    public void BuildZplLabel_ContainsQrCommandAndKitNumber()
    {
        var service = new PrinterService(new QrCodeService());
        var zpl = service.BuildZplLabel(SampleRecord(), SampleSettings());

        Assert.Contains("^BQN", zpl);              // QR barcode command
        Assert.Contains("KIT202607110001", zpl);   // Kit number appears (in QR data and human-readable text)
    }

    [Fact]
    public void BuildZplLabel_ContainsFormattedWeight()
    {
        var service = new PrinterService(new QrCodeService());
        var zpl = service.BuildZplLabel(SampleRecord(), SampleSettings());

        Assert.Contains("1.032", zpl);
    }

    [Fact]
    public void BuildZplLabel_StripsCaretAndTildeFromProductNameToAvoidBreakingZplSyntax()
    {
        var record = SampleRecord();
        record.ProductName = "Weird^Name~Test";
        var service = new PrinterService(new QrCodeService());

        var zpl = service.BuildZplLabel(record, SampleSettings());

        Assert.Contains("WeirdNameTest", zpl);
        Assert.DoesNotContain("Weird^Name~Test", zpl);
    }

    [Fact]
    public void BuildZplLabel_LargerLabelSize_ProducesLargerPageDimensions()
    {
        var service = new PrinterService(new QrCodeService());
        var small = service.BuildZplLabel(SampleRecord(), SampleSettings());
        var big = service.BuildZplLabel(SampleRecord(), new PrinterSettings { LabelWidthMm = 100, LabelHeightMm = 60, DpiSetting = 203 });

        // A 100mm label should render a larger ^PW (print width in dots) than a 50mm one.
        Assert.NotEqual(small, big);
    }
}
