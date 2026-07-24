using QRCoder;
using WeightVerificationQR.Core.Interfaces;

namespace WeightVerificationQR.Services;

/// <summary>
/// Generates QR codes containing only the unique Kit Number. All other details
/// (product, weight, operator, etc.) are looked up from the database when the QR
/// is scanned - keeping the QR payload small and the printed code robust.
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
