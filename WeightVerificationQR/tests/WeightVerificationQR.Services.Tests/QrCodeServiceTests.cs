using WeightVerificationQR.Services;
using Xunit;

namespace WeightVerificationQR.Services.Tests;

public class QrCodeServiceTests
{
    [Fact]
    public void GenerateQrPng_ProducesNonEmptyPngBytes()
    {
        var service = new QrCodeService();
        var bytes = service.GenerateQrPng("KIT202607110001");

        Assert.NotEmpty(bytes);

        // PNG file signature: 89 50 4E 47 0D 0A 1A 0A
        byte[] pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.Equal(pngSignature, bytes[..8]);
    }

    [Fact]
    public void GenerateQrPng_DifferentPayloads_ProduceDifferentImages()
    {
        var service = new QrCodeService();
        var a = service.GenerateQrPng("KIT202607110001");
        var b = service.GenerateQrPng("KIT202607110002");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GenerateQrPng_LargerPixelsPerModule_ProducesLargerImage()
    {
        var service = new QrCodeService();
        var small = service.GenerateQrPng("KIT202607110001", pixelsPerModule: 4);
        var large = service.GenerateQrPng("KIT202607110001", pixelsPerModule: 20);

        Assert.True(large.Length > small.Length);
    }
}
