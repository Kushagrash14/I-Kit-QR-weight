using QRCoder;
using WeightVerificationQR.Core.Interfaces;

namespace WeightVerificationQR.Services;

/// <summary>
/// Generates a PNG QR code from the structured payload prepared by SerialNumberService.
/// </summary>
public class QrCodeService : IQrCodeService
{
    public byte[] GenerateQrPng(string payload, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
