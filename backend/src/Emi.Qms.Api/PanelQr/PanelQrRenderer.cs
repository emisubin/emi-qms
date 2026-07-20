using System.Text;
using QRCoder;

namespace Emi.Qms.Api.PanelQr;

public sealed class PanelQrRenderer
{
    public byte[] RenderSvg(string scanUrl)
    {
        using var data = QRCodeGenerator.GenerateQrCode(scanUrl, QRCodeGenerator.ECCLevel.Q);
        var renderer = new SvgQRCode(data);
        return Encoding.UTF8.GetBytes(renderer.GetGraphic(8));
    }

    public byte[] RenderPng(string scanUrl)
    {
        using var data = QRCodeGenerator.GenerateQrCode(scanUrl, QRCodeGenerator.ECCLevel.Q);
        var renderer = new PngByteQRCode(data);
        return renderer.GetGraphic(10);
    }
}
