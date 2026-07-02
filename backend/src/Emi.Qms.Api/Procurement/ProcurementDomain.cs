using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Emi.Qms.Api.Procurement;

public static partial class ProcurementDomain
{
    public const long MaxExcelFileSizeBytes = 10 * 1024 * 1024;
    public const long MaxExcelMultipartRequestBytes = 11 * 1024 * 1024;
    public const int MaxExcelRows = 2000;
    public const string DuplicateFileMessage = "이미 적용된 구매 Excel 파일입니다. 수정된 파일을 다시 업로드해 주세요.";
    public const string StaleVersionMessage = "다른 사용자가 구매정보를 수정했습니다. 화면을 새로고침한 후 다시 시도해 주세요.";

    public static string? TrimToNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static string NormalizeProjectKey(string? value)
    {
        var normalized = WhiteSpaceRegex().Replace(value?.Trim() ?? "", " ");
        return normalized.ToUpperInvariant();
    }

    public static string? BuildRowMatchKey(
        string? orderItem,
        string? technicalOwner,
        DateOnly? orderDate,
        DateOnly? expectedReceiptDate,
        string? standardLeadTime)
    {
        var text = string.Join("|", new[]
        {
            NormalizeProjectKey(orderItem),
            NormalizeProjectKey(technicalOwner),
            orderDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            expectedReceiptDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            NormalizeProjectKey(standardLeadTime)
        });

        if (string.IsNullOrWhiteSpace(text.Replace("|", "", StringComparison.Ordinal)))
        {
            return null;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string BuildDDayText(DateOnly? expectedReceiptDate, DateOnly today)
    {
        if (expectedReceiptDate is null)
        {
            return "-";
        }

        var days = expectedReceiptDate.Value.DayNumber - today.DayNumber;
        return days switch
        {
            > 0 => $"D-{days.ToString(CultureInfo.InvariantCulture)}",
            0 => "D-Day",
            _ => $"예정일 {Math.Abs(days).ToString(CultureInfo.InvariantCulture)}일 경과"
        };
    }

    public static bool TryParseReceiptCompleted(string? raw, out bool? value)
    {
        var normalized = NormalizeProjectKey(raw);
        if (string.IsNullOrEmpty(normalized))
        {
            value = null;
            return true;
        }

        if (normalized is "Y" or "YES" or "TRUE" or "1" or "완료")
        {
            value = true;
            return true;
        }

        if (normalized is "N" or "NO" or "FALSE" or "0" or "미완료")
        {
            value = false;
            return true;
        }

        value = null;
        return false;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}

internal sealed record ProcurementItemSnapshot(
    Guid ItemId,
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    DateOnly? ProjectDeliveryDate,
    int SequenceNumber,
    string? SourceProjectText,
    string? SourceProjectCodeText,
    string? StandardLeadTime,
    string? OrderItem,
    string? SupplierName,
    string? TechnicalOwner,
    DateOnly? OrderDate,
    DateOnly? ExpectedReceiptDate,
    string? IssueNote,
    bool ReceiptCompleted,
    DateTimeOffset? ReceiptCompletedAtUtc,
    Guid? ReceiptCompletedByUserId,
    string? ReceiptCompletedByUserName,
    string? ReceiptCompletionNote,
    int RowVersion,
    int? SourceExcelRowNumber,
    int? SourceGroupSequence,
    string? RowMatchKey,
    string Status);

internal sealed record ProcurementProjectSnapshot(
    Guid ProjectId,
    string ProjectTitle,
    string ProjectCode,
    string ProjectKey,
    DateOnly? DeliveryDate,
    string Status,
    DateTimeOffset? DeletedAtUtc);

internal sealed record ParsedProcurementExcelFile(
    string FileSha256,
    int TotalRows,
    IReadOnlyList<ParsedProcurementExcelRow> Rows,
    IReadOnlyList<string> FileErrors);

internal sealed record ParsedProcurementExcelRow(
    int ExcelRowNumber,
    int SourceGroupSequence,
    string? SourceProjectText,
    string? SourceProjectCodeText,
    string? StandardLeadTime,
    string? OrderItem,
    string? SupplierName,
    string? TechnicalOwner,
    DateOnly? OrderDate,
    DateOnly? ExpectedReceiptDate,
    string? ShipmentText,
    string? IssueNote,
    bool? ReceiptCompleted,
    bool IsSkipped,
    IReadOnlyList<string> ErrorMessages)
{
    public string? RowMatchKey => ProcurementDomain.BuildRowMatchKey(
        OrderItem,
        TechnicalOwner,
        OrderDate,
        ExpectedReceiptDate,
        StandardLeadTime);
}
