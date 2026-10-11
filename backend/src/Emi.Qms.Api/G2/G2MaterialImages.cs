using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Emi.Qms.Api.G2;

/// <summary>Images from the same approved blank template offered for download.</summary>
internal static class G2MaterialImages
{
    private static readonly Lazy<IReadOnlyDictionary<string, byte[]>> Images = new(Load);
    internal static IReadOnlyDictionary<string, byte[]> All => Images.Value;

    private static IReadOnlyDictionary<string, byte[]> Load()
    {
        using var stream = typeof(G2MaterialImages).Assembly.GetManifestResourceStream("G2MaterialTemplate.xlsx")
            ?? throw new InvalidOperationException("G2 material template is not packaged.");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        XNamespace sheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace drawingNs = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
        XNamespace artNs = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XDocument Read(string path)
        {
            using var entry = (zip.GetEntry(path) ?? throw new InvalidDataException("Missing template part.")).Open();
            return XDocument.Load(entry);
        }
        Dictionary<string, string> Relationships(string path)
        {
            var slash = path.LastIndexOf('/');
            var directory = path[..(slash + 1)];
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var rel in Read(directory + "_rels/" + path[(slash + 1)..] + ".rels").Root!.Elements())
            {
                if ((string?)rel.Attribute("TargetMode") == "External") continue;
                var target = (string?)rel.Attribute("Target") ?? throw new InvalidDataException("Missing template relationship.");
                var resolved = new Uri(new Uri("https://template.invalid/" + path), target).AbsolutePath.TrimStart('/');
                if (!resolved.StartsWith("xl/", StringComparison.Ordinal)) throw new InvalidDataException("Invalid template path.");
                result.Add((string)rel.Attribute("Id")!, resolved);
            }
            return result;
        }
        var sheet = Read("xl/workbook.xml").Descendants(sheetNs + "sheet").Single(x => (string?)x.Attribute("name") == "양식");
        var path = Relationships("xl/workbook.xml")[(string)sheet.Attribute(relNs + "id")!];
        var sheetRelations = Relationships(path);
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var positions = new Dictionary<int, int>();
        foreach (var drawing in Read(path).Descendants(sheetNs + "drawing"))
        {
            var drawingPath = sheetRelations[(string)drawing.Attribute(relNs + "id")!];
            var images = Relationships(drawingPath);
            // Preserve anchor order, matching the approved catalog preparation script.
            foreach (var anchor in Read(drawingPath).Root!.Elements())
            {
                var rowNode = anchor.Element(drawingNs + "from")?.Element(drawingNs + "row");
                var blip = anchor.Descendants(artNs + "blip").FirstOrDefault();
                if (rowNode is null || blip is null) continue;
                var row = int.Parse(rowNode.Value, CultureInfo.InvariantCulture) + 1;
                if (row < 4 || row > 95) continue;
                var imagePath = images[(string)blip.Attribute(relNs + "embed")!];
                var extension = Path.GetExtension(imagePath).ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg")) throw new InvalidDataException("Unsupported template image.");
                var index = positions.GetValueOrDefault(row) + 1;
                positions[row] = index;
                using var source = zip.GetEntry(imagePath)!.Open();
                using var bytes = new MemoryStream();
                source.CopyTo(bytes);
                result.Add($"item-{row - 3:00}-{index}{extension}", bytes.ToArray());
            }
        }
        return result;
    }
}
