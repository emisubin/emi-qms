using System.Security.Cryptography;
using System.Text.Json;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Emi.Qms.Api.QualityInspections;

public sealed class QualityInspectionPdfRenderer
{
    private const string FontHash = "194018e6b2b293a7964f037b25c0249ce1418bc9ab3c971060a03aa57861e252";
    private static readonly object FontResolverLock = new();
    private readonly string fontPath;

    public QualityInspectionPdfRenderer(IWebHostEnvironment environment)
    {
        fontPath = Path.Combine(environment.ContentRootPath, "Assets", "Fonts", "NotoSansKR-Variable.ttf");
        if (!File.Exists(fontPath)) throw new InvalidOperationException("Bundled quality PDF font is missing.");
        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fontPath))).ToLowerInvariant();
        if (!string.Equals(actualHash, FontHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Bundled quality PDF font hash does not match provenance.");
        lock (FontResolverLock)
        {
            GlobalFontSettings.FontResolver ??= new BundledKoreanFontResolver(fontPath);
        }
    }

    public byte[] Render(string snapshotText)
    {
        using var snapshot = JsonDocument.Parse(snapshotText);
        var root = snapshot.RootElement;
        var finalizedAt = root.GetProperty("finalizedAtUtc").GetDateTimeOffset();
        using var document = new PdfDocument();
        document.Info.Title = $"{root.GetProperty("stageLabel").GetString()} 패널 검사성적서";
        document.Info.Subject = "승인 시점 불변 패널 품질검사 snapshot";
        document.Info.Author = "EMI QMS";
        document.Info.Creator = "PDFsharp-6.2.4";
        document.Info.CreationDate = finalizedAt.UtcDateTime;
        document.Info.ModificationDate = finalizedAt.UtcDateTime;

        var regular = new XFont("Noto Sans KR", 9.2, XFontStyleEx.Regular);
        var small = new XFont("Noto Sans KR", 7.8, XFontStyleEx.Regular);
        var title = new XFont("Noto Sans KR", 18, XFontStyleEx.Regular);
        var heading = new XFont("Noto Sans KR", 11, XFontStyleEx.Regular);
        var accent = XColor.FromArgb(44, 81, 73);
        var mint = XColor.FromArgb(235, 245, 239);
        var ink = XColor.FromArgb(28, 43, 38);

        PdfPage page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        XGraphics graphics = XGraphics.FromPdfPage(page);
        var y = 38d;

        void NextPage(double needed)
        {
            if (y + needed < page.Height.Point - 38) return;
            graphics.Dispose();
            page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            graphics = XGraphics.FromPdfPage(page);
            y = 38;
        }

        graphics.DrawRoundedRectangle(new XSolidBrush(accent), 34, 30, page.Width.Point - 68, 64, 10, 10);
        graphics.DrawString("EMI · PANEL QUALITY", small, XBrushes.White, new XRect(50, 43, 260, 14), XStringFormats.TopLeft);
        graphics.DrawString($"{root.GetProperty("stageLabel").GetString()} 검사성적서", title, XBrushes.White, new XRect(50, 59, 440, 26), XStringFormats.TopLeft);
        y = 110;
        graphics.DrawString($"{root.GetProperty("projectCode").GetString()} · {root.GetProperty("projectTitle").GetString()}", heading, new XSolidBrush(ink), new XRect(42, y, 510, 18), XStringFormats.TopLeft);
        y += 20;
        graphics.DrawString($"패널  {root.GetProperty("panelCode").GetString()} · {Nullable(root, "panelName") ?? "이름 미입력"}", regular, XBrushes.Black, new XRect(42, y, 510, 16), XStringFormats.TopLeft);
        y += 16;
        graphics.DrawString($"검사  {root.GetProperty("attemptNumber").GetInt32()}차 · {TranslateResult(root.GetProperty("result").GetString())}", regular, XBrushes.Black, new XRect(42, y, 510, 16), XStringFormats.TopLeft);
        y += 16;
        graphics.DrawString($"검사자  {root.GetProperty("finalizedBy").GetString()} · 확정 {finalizedAt:yyyy-MM-dd HH:mm} UTC", small, XBrushes.DimGray, new XRect(42, y, 510, 14), XStringFormats.TopLeft);
        y += 24;

        graphics.DrawRoundedRectangle(new XSolidBrush(mint), 42, y, 510, 42, 7, 7);
        graphics.DrawString("종합 판정", small, new XSolidBrush(accent), new XRect(54, y + 7, 100, 12), XStringFormats.TopLeft);
        graphics.DrawString(root.GetProperty("reason").GetString() ?? "-", regular, new XSolidBrush(ink), new XRect(54, y + 21, 482, 16), XStringFormats.TopLeft);
        y += 58;

        graphics.DrawString("검사 항목", heading, new XSolidBrush(accent), new XRect(42, y, 510, 18), XStringFormats.TopLeft);
        y += 24;
        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            NextPage(54);
            graphics.DrawRoundedRectangle(XPens.LightGray, XBrushes.White, 42, y, 510, 46, 6, 6);
            graphics.DrawEllipse(new XSolidBrush(accent), 52, y + 10, 24, 24);
            graphics.DrawString(item.GetProperty("displayOrder").GetInt32().ToString(), small, XBrushes.White, new XRect(52, y + 15, 24, 12), XStringFormats.TopCenter);
            graphics.DrawString(item.GetProperty("label").GetString() ?? "검사 항목", regular, new XSolidBrush(ink), new XRect(88, y + 8, 330, 15), XStringFormats.TopLeft);
            var response = item.GetProperty("responseType").GetString() == "Check"
                ? TranslateCheck(Nullable(item, "checkResult"))
                : Nullable(item, "textValue") ?? "입력 없음";
            graphics.DrawString(response, regular, new XSolidBrush(accent), new XRect(420, y + 8, 116, 15), XStringFormats.TopRight);
            var note = Nullable(item, "note");
            if (note is not null) graphics.DrawString(note, small, XBrushes.DimGray, new XRect(88, y + 27, 446, 12), XStringFormats.TopLeft);
            y += 54;
        }

        graphics.Dispose();
        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static string TranslateResult(string? value) => value == "Passed" ? "합격" : "부적합 / PUNCH";
    private static string TranslateCheck(string? value) => value switch
    {
        "Pass" => "적합",
        "Fail" => "부적합",
        "NotApplicable" => "해당없음",
        _ => "미입력"
    };
    private static string? Nullable(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
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
