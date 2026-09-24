using System.IO.Compression;
using System.Security.Cryptography;

namespace Emi.Qms.Api.Notices;

public static class NoticeAttachmentValidator
{
    public const int MaximumFileBytes = 10 * 1024 * 1024;
    public const int MaximumAttachments = 5;

    private static readonly IReadOnlyDictionary<string, string[]> ExtensionsByMime =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["application/pdf"] = [".pdf"],
            ["application/zip"] = [".zip"],
            ["application/x-hwp"] = [".hwp"],
            ["application/msword"] = [".doc"],
            ["application/vnd.ms-excel"] = [".xls"],
            ["application/vnd.ms-powerpoint"] = [".ppt"],
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"],
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = [".pptx"]
        };

    public static NoticeAttachmentValidationResult Validate(string? fileName, byte[] content, bool osan = false)
    {
        var safeName = SanitizeFileName(fileName);
        if (safeName is null)
        {
            return NoticeAttachmentValidationResult.Invalid("파일명은 1~180자여야 합니다.");
        }
        if (content.Length < 1 || content.Length > (osan ? 20 * 1024 * 1024 : MaximumFileBytes))
        {
            return NoticeAttachmentValidationResult.Invalid(osan ? "파일은 개별 20MB 이하여야 합니다." : "파일은 개별 10MB 이하여야 합니다.");
        }

        var contentType = DetectContentType(content);
        if (contentType is null && osan) contentType = DetectAdditionalDocument(fileName, content);
        if (contentType is null)
        {
            return NoticeAttachmentValidationResult.Invalid(osan ? "JPEG, PNG, PDF, Office, HWP 또는 ZIP 형식의 정상 파일을 등록해 주세요." : "PDF, JPEG, PNG, DOCX, XLSX 또는 PPTX 파일만 등록할 수 있습니다.");
        }

        var extension = Path.GetExtension(safeName);
        if (!ExtensionsByMime[contentType].Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return NoticeAttachmentValidationResult.Invalid("파일 확장자와 실제 파일 형식이 일치하지 않습니다.");
        }

        return NoticeAttachmentValidationResult.Valid(
            safeName,
            contentType,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
    }

    private static string? DetectAdditionalDocument(string? name, byte[] content)
    {
        var extension = Path.GetExtension(name ?? "").ToLowerInvariant();
        if(extension == ".zip")
        {
            try
            {
                using var archive = new ZipArchive(new MemoryStream(content, false), ZipArchiveMode.Read);
                if(archive.Entries.Count > 10000) return null;
                long total = 0;
                foreach(var entry in archive.Entries)
                {
                    if(entry.FullName.Replace('\\','/').Split('/').Contains("..") || entry.FullName.StartsWith('/')) return null;
                    total += entry.Length;
                    if(total > 500L*1024*1024) return null;
                }
                return "application/zip";
            }
            catch(InvalidDataException) { return null; }
        }
        // Legacy Office/HWP use a compound document container. Require its header and format stream name.
        ReadOnlySpan<byte> compound = [0xd0,0xcf,0x11,0xe0,0xa1,0xb1,0x1a,0xe1];
        if(content.Length < 512 || !content.AsSpan().StartsWith(compound)) return null;
        var marker = extension switch { ".hwp"=>"FileHeader", ".doc"=>"WordDocument", ".xls"=>"Workbook", ".ppt"=>"PowerPoint Document", _=>null };
        if(marker is null || content.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes(marker)) < 0) return null;
        return extension switch { ".hwp"=>"application/x-hwp", ".doc"=>"application/msword", ".xls"=>"application/vnd.ms-excel", _=>"application/vnd.ms-powerpoint" };
    }

    private static string? SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace('\\', '/');
        var name = normalized[(normalized.LastIndexOf('/') + 1)..].Trim();
        name = new string(name.Where(character => !char.IsControl(character)).ToArray());
        return name.Length is >= 1 and <= 180 ? name : null;
    }

    private static string? DetectContentType(byte[] content)
    {
        ReadOnlySpan<byte> pdf = [0x25, 0x50, 0x44, 0x46, 0x2D];
        if (content.AsSpan().StartsWith(pdf))
        {
            return "application/pdf";
        }
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return "image/jpeg";
        }
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (content.AsSpan().StartsWith(png))
        {
            return "image/png";
        }

        return DetectOpenXmlContentType(content);
    }

    private static string? DetectOpenXmlContentType(byte[] content)
    {
        if (content.Length < 4 || content[0] != 0x50 || content[1] != 0x4B)
        {
            return null;
        }

        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            var typesEntry = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.Equals("[Content_Types].xml", StringComparison.Ordinal));
            if (typesEntry is null || typesEntry.Length is < 1 or > 1024 * 1024)
            {
                return null;
            }
            string contentTypes;
            using (var reader = new StreamReader(typesEntry.Open()))
            {
                contentTypes = reader.ReadToEnd();
            }
            if (HasEntry(archive, "word/document.xml")
                && contentTypes.Contains(
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
                    StringComparison.Ordinal))
            {
                return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }
            if (HasEntry(archive, "xl/workbook.xml")
                && contentTypes.Contains(
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
                    StringComparison.Ordinal))
            {
                return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }
            if (HasEntry(archive, "ppt/presentation.xml")
                && contentTypes.Contains(
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
                    StringComparison.Ordinal))
            {
                return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
            }
        }
        catch (InvalidDataException)
        {
            return null;
        }

        return null;
    }

    private static bool HasEntry(ZipArchive archive, string fullName)
        => archive.Entries.Any(entry => entry.FullName.Equals(fullName, StringComparison.Ordinal));
}

public sealed record NoticeAttachmentValidationResult(
    bool IsValid,
    string? FileName,
    string? ContentType,
    string? Sha256,
    string? Error)
{
    public static NoticeAttachmentValidationResult Valid(string fileName, string contentType, string sha256)
        => new(true, fileName, contentType, sha256, null);

    public static NoticeAttachmentValidationResult Invalid(string error)
        => new(false, null, null, null, error);
}
