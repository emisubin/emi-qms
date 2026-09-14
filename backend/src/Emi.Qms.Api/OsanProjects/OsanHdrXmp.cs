using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Emi.Qms.Api.OsanProjects;

internal static class OsanHdrXmp
{
    private static readonly byte[] Header = "http://ns.adobe.com/xap/1.0/\0"u8.ToArray();
    private const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private const string Apple = "http://ns.apple.com/HDRGainMap/1.0/";
    private const string Pixel = "http://ns.apple.com/pixeldatainfo/1.0/";
    private const string Adobe = "http://ns.adobe.com/hdr-gain-map/1.0/";
    private const string Container = "http://ns.google.com/photos/1.0/container/";
    private const string Item = "http://ns.google.com/photos/1.0/container/item/";

    internal static byte[]? Sanitize(ReadOnlySpan<byte> payload, int? gainMapLength)
    {
        if (!payload.StartsWith(Header)) return null;
        try
        {
            using var stream = new MemoryStream(payload[Header.Length..].ToArray());
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var document = XDocument.Load(reader);
            if (document.Root is null || document.Descendants().Take(513).Count() > 512 || document.Descendants().Any(e => e.Ancestors().Take(33).Count() > 32))
                throw new InvalidDataException("HDR metadata exceeds structural limits.");
            var root = Filter(document.Root, gainMapLength);
            if (root is null || !root.DescendantsAndSelf().Any(e => e.Name.NamespaceName is Apple or Pixel or Adobe or Container || e.Attributes().Any(a => a.Name.NamespaceName is Apple or Pixel or Adobe))) return null;
            var xml = Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
            return [.. Header, .. xml];
        }
        catch (XmlException) { throw new InvalidDataException("Invalid HDR metadata."); }
    }

    private static XElement? Filter(XElement element, int? gainMapLength)
    {
        var ns = element.Name.NamespaceName; var name = element.Name.LocalName;
        var structural = ns == "adobe:ns:meta/" && name == "xmpmeta"
            || ns == Rdf && name is "RDF" or "Description" or "Seq" or "li"
            || ns == Container && name is "Directory" or "Item";
        if (!structural && !AllowedName(element.Name)) return null;
        var result = new XElement(element.Name);
        foreach (var a in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
        {
            if (a.Name == XName.Get("about", Rdf)) { result.SetAttributeValue(a.Name, ""); continue; }
            if (a.Name == XName.Get("parseType", Rdf) && a.Value == "Resource") { result.SetAttributeValue(a.Name, "Resource"); continue; }
            if (AllowedValue(a.Name, a.Value)) result.SetAttributeValue(a.Name, a.Value);
        }
        if (gainMapLength is not null && result.Attribute(XName.Get("Semantic", Item))?.Value == "GainMap")
            result.SetAttributeValue(XName.Get("Length", Item), gainMapLength.Value);
        foreach (var child in element.Elements()) { var safe = Filter(child, gainMapLength); if (safe is not null) result.Add(safe); }
        if (!element.HasElements)
        {
            var value = element.Value.Trim();
            if (AllowedValue(element.Name, value) || ns == Rdf && name == "li" && Number(value)) result.Value = value;
        }
        return result.HasElements || result.HasAttributes || !string.IsNullOrEmpty(result.Value) ? result : null;
    }
    private static bool AllowedName(XName n) => n.NamespaceName switch
    {
        Apple => n.LocalName is "HDRGainMapVersion" or "HDRGainMapHeadroom",
        Pixel => n.LocalName is "AuxiliaryImageType" or "NativeFormat" or "StoredFormat",
        Adobe => n.LocalName is "Version" or "GainMapMin" or "GainMapMax" or "Gamma" or "OffsetSDR" or "OffsetHDR" or "HDRCapacityMin" or "HDRCapacityMax" or "BaseRenditionIsHDR",
        Item => n.LocalName is "Mime" or "Semantic" or "Length" or "Padding",
        _ => false
    };
    private static bool AllowedValue(XName n, string value)
    {
        if (!AllowedName(n)) return false;
        return n.LocalName switch
        {
            "AuxiliaryImageType" => value == "urn:com:apple:photo:2020:aux:hdrgainmap",
            "BaseRenditionIsHDR" => value is "True" or "False" or "true" or "false",
            "Mime" => value == "image/jpeg",
            "Semantic" => value is "Primary" or "GainMap",
            _ => Number(value)
        };
    }
    private static bool Number(string value) => value.Length <= 32 && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) && Math.Abs(n) <= 1e12;
}
