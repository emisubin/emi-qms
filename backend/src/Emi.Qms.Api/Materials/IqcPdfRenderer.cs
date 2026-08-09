using System.Security.Cryptography;
using System.Text.Json;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Emi.Qms.Api.Materials;

public sealed class IqcPdfRenderer
{
    private const string FontHash = "194018e6b2b293a7964f037b25c0249ce1418bc9ab3c971060a03aa57861e252";
    private static readonly object FontResolverLock = new();
    private readonly string fontPath;

    public IqcPdfRenderer(IWebHostEnvironment environment)
    {
        fontPath = Path.Combine(environment.ContentRootPath, "Assets", "Fonts", "NotoSansKR-Variable.ttf");
        if (!File.Exists(fontPath))
        {
            throw new InvalidOperationException("Bundled IQC PDF font is missing.");
        }
        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fontPath))).ToLowerInvariant();
        if (!string.Equals(actualHash, FontHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bundled IQC PDF font hash does not match provenance.");
        }
        lock (FontResolverLock)
        {
            GlobalFontSettings.FontResolver ??= new BundledKoreanFontResolver(fontPath);
        }
    }

    public byte[] Render(string snapshotText, IReadOnlyList<IqcPdfPhoto> photos)
    {
        using var snapshot = JsonDocument.Parse(snapshotText);
        var root = snapshot.RootElement;
        var finalizedAt = DateTimeOffset.Parse(
            root.GetProperty("finalizedAtUtc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        using var document = new PdfDocument();
        document.Info.Title = "IQC 디지털 검사성적서";
        document.Info.Subject = "승인 시점 불변 IQC snapshot";
        document.Info.Author = "EMI PMS";
        document.Info.Creator = "PDFsharp-6.2.4";
        document.Info.CreationDate = finalizedAt.UtcDateTime;
        document.Info.ModificationDate = finalizedAt.UtcDateTime;

        var regular = new XFont("Noto Sans KR", 9.5, XFontStyleEx.Regular);
        var small = new XFont("Noto Sans KR", 8, XFontStyleEx.Regular);
        var title = new XFont("Noto Sans KR", 19, XFontStyleEx.Regular);
        var heading = new XFont("Noto Sans KR", 11, XFontStyleEx.Regular);
        var accent = XColor.FromArgb(40, 82, 73);
        var soft = XColor.FromArgb(239, 246, 242);
        var ink = XColor.FromArgb(31, 43, 40);

        PdfPage page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        XGraphics graphics = XGraphics.FromPdfPage(page);
        var y = 42d;

        void NextPageIfNeeded(double needed)
        {
            if (y + needed <= page.Height.Point - 42)
            {
                return;
            }
            graphics.Dispose();
            page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            graphics = XGraphics.FromPdfPage(page);
            y = 42;
        }

        void Text(string value, XFont font, double x, double width, XBrush brush, double lineHeight = 14)
        {
            foreach (var line in Wrap(value, Math.Max(12, (int)(width / Math.Max(5, font.Size * 0.72)))))
            {
                NextPageIfNeeded(lineHeight);
                graphics.DrawString(line, font, brush, new XRect(x, y, width, lineHeight), XStringFormats.TopLeft);
                y += lineHeight;
            }
        }

        graphics.DrawRoundedRectangle(new XSolidBrush(accent), 36, 34, page.Width.Point - 72, 58, 12, 12);
        graphics.DrawString("EMI · QUALITY RECORD", small, XBrushes.White, new XRect(52, 45, 260, 14), XStringFormats.TopLeft);
        graphics.DrawString("IQC 디지털 검사성적서", title, XBrushes.White, new XRect(52, 60, 430, 28), XStringFormats.TopLeft);
        y = 112;

        var projectLine = $"{root.GetProperty("projectCode").GetString()} · {root.GetProperty("projectTitle").GetString()}";
        Text(projectLine, heading, 42, 510, new XSolidBrush(ink), 18);
        Text($"품목  {Value(root, "orderItem", "발주품목 미입력")}", regular, 42, 510, XBrushes.Black);
        Text($"검사  {root.GetProperty("attemptNumber").GetInt32()}차 · {TranslateResult(root.GetProperty("result").GetString())} · 양식 v{root.GetProperty("templateVersion").GetInt32()}", regular, 42, 510, XBrushes.Black);
        Text($"검사자  {root.GetProperty("finalizedBy").GetString()} · 확정  {finalizedAt:yyyy-MM-dd HH:mm} UTC", small, 42, 510, XBrushes.DimGray);
        y += 10;

        graphics.DrawRoundedRectangle(new XSolidBrush(soft), 42, y, 510, 38, 8, 8);
        graphics.DrawString("종합 판정 사유", small, new XSolidBrush(accent), new XRect(54, y + 7, 120, 12), XStringFormats.TopLeft);
        graphics.DrawString(root.GetProperty("reason").GetString() ?? "-", regular, new XSolidBrush(ink), new XRect(54, y + 19, 486, 16), XStringFormats.TopLeft);
        y += 56;

        Text("검사 항목", heading, 42, 510, new XSolidBrush(accent), 18);
        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            NextPageIfNeeded(56);
            var top = y;
            graphics.DrawRoundedRectangle(XPens.LightGray, XBrushes.White, 42, top, 510, 48, 6, 6);
            var order = item.GetProperty("displayOrder").GetInt32();
            var label = item.GetProperty("label").GetString() ?? "검사 항목";
            var result = item.GetProperty("responseType").GetString() == "Check"
                ? TranslateCheck(NullableString(item, "checkResult"))
                : Value(item, "textValue", "입력 없음");
            graphics.DrawEllipse(new XSolidBrush(accent), 52, top + 11, 25, 25);
            graphics.DrawString(order.ToString(System.Globalization.CultureInfo.InvariantCulture), small, XBrushes.White, new XRect(52, top + 16, 25, 12), XStringFormats.TopCenter);
            graphics.DrawString(label, regular, new XSolidBrush(ink), new XRect(88, top + 8, 350, 16), XStringFormats.TopLeft);
            graphics.DrawString(result, regular, new XSolidBrush(accent), new XRect(438, top + 8, 100, 16), XStringFormats.TopRight);
            var note = NullableString(item, "note");
            if (!string.IsNullOrWhiteSpace(note))
            {
                graphics.DrawString(note, small, XBrushes.DimGray, new XRect(88, top + 27, 445, 14), XStringFormats.TopLeft);
            }
            y += 56;
        }

        var photoById = photos.ToDictionary(photo => photo.PhotoId);
        var snapshotPhotos = root.GetProperty("photos").EnumerateArray().ToList();
        if (snapshotPhotos.Count > 0)
        {
            y += 6;
            Text("사진 증빙", heading, 42, 510, new XSolidBrush(accent), 18);
        }
        foreach (var metadata in snapshotPhotos)
        {
            var photoId = metadata.GetProperty("photoId").GetGuid();
            if (!photoById.TryGetValue(photoId, out var photo))
            {
                throw new InvalidOperationException("Snapshot photo evidence is missing.");
            }
            NextPageIfNeeded(250);
            var label = metadata.GetProperty("altText").GetString() ?? "IQC 사진";
            Text(label, regular, 42, 510, new XSolidBrush(ink));
            using var imageStream = new MemoryStream(photo.Content, writable: false);
            using var image = XImage.FromStream(imageStream);
            var maxWidth = 510d;
            var maxHeight = 210d;
            var scale = Math.Min(maxWidth / image.PixelWidth, maxHeight / image.PixelHeight);
            scale = Math.Min(scale, 1);
            var width = image.PixelWidth * scale;
            var height = image.PixelHeight * scale;
            graphics.DrawImage(image, 42, y, width, height);
            y += height + 18;
        }

        graphics.Dispose();
        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static IEnumerable<string> Wrap(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
        {
            yield return "";
            yield break;
        }
        for (var index = 0; index < value.Length; index += maxChars)
        {
            yield return value.Substring(index, Math.Min(maxChars, value.Length - index));
        }
    }

    private static string TranslateResult(string? value) => value == "Passed" ? "합격" : "부적합";

    private static string TranslateCheck(string? value) => value switch
    {
        "Pass" => "적합",
        "Fail" => "부적합",
        "NotApplicable" => "해당없음",
        _ => "미입력"
    };

    private static string Value(JsonElement element, string propertyName, string fallback)
        => NullableString(element, propertyName) ?? fallback;

    private static string? NullableString(JsonElement element, string propertyName)
    {
        var property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetString();
    }

    private sealed class BundledKoreanFontResolver(string path) : IFontResolver
    {
        private readonly byte[] fontBytes = File.ReadAllBytes(path);

        public byte[] GetFont(string faceName)
            => faceName == "NotoSansKR" ? fontBytes : throw new InvalidOperationException("Unknown bundled font face.");

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            => new("NotoSansKR", mustSimulateBold: isBold, mustSimulateItalic: isItalic);
    }
}
