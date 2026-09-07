namespace Emi.Qms.Api.OsanProjects;

public static class OsanProjectInputNormalizer
{
    public const int TitleMaxLength = 200;
    public const int ProjectCodeMaxLength = 80;
    public const int CustomerNameMaxLength = 200;
    public const int ReferenceNumberMaxLength = 100;
    public const int ProductNameMaxLength = 100;
    public const int QuantityMax = 500;

    public static (NormalizedCreateOsanProjectInput? Input, IReadOnlyDictionary<string, string[]> Errors) Normalize(
        CreateOsanProjectRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var title = RequiredText(request.Title, nameof(request.Title), TitleMaxLength, errors);
        var projectCode = RequiredText(request.ProjectCode, nameof(request.ProjectCode), ProjectCodeMaxLength, errors);
        var customerName = RequiredText(request.CustomerName, nameof(request.CustomerName), CustomerNameMaxLength, errors);
        var poNumber = OptionalText(request.PoNumber, nameof(request.PoNumber), ReferenceNumberMaxLength, errors);
        var workOrderNumber = OptionalText(request.WorkOrderNumber, nameof(request.WorkOrderNumber), ReferenceNumberMaxLength, errors);
        var productName = RequiredText(request.ProductName, nameof(request.ProductName), ProductNameMaxLength, errors);

        if (request.DeliveryDate is null)
        {
            errors[nameof(request.DeliveryDate)] = ["납기일을 입력해 주세요."];
        }

        if (request.Quantity is null or < 1 or > QuantityMax)
        {
            errors[nameof(request.Quantity)] = [$"수량은 1 이상 {QuantityMax} 이하의 정수로 입력해 주세요."];
        }

        if (request.OperationId == Guid.Empty)
        {
            errors[nameof(request.OperationId)] = ["새 요청 식별자가 필요합니다."];
        }

        if (errors.Count > 0
            || title is null
            || projectCode is null
            || customerName is null
            || request.DeliveryDate is null
            || productName is null
            || request.Quantity is null)
        {
            return (null, errors);
        }

        return (new NormalizedCreateOsanProjectInput(
            title,
            projectCode,
            customerName,
            poNumber,
            workOrderNumber,
            request.DeliveryDate.Value,
            productName,
            request.Quantity.Value,
            request.OperationId), errors);
    }

    private static string? RequiredText(
        string? value,
        string field,
        int maxLength,
        IDictionary<string, string[]> errors)
    {
        var trimmed = TrimToNull(value);
        if (trimmed is null)
        {
            errors[field] = ["필수 입력값입니다."];
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors[field] = [$"{maxLength}자 이하로 입력해 주세요."];
            return null;
        }

        return trimmed;
    }

    private static string? OptionalText(
        string? value,
        string field,
        int maxLength,
        IDictionary<string, string[]> errors)
    {
        var trimmed = TrimToNull(value);
        if (trimmed is not null && trimmed.Length > maxLength)
        {
            errors[field] = [$"{maxLength}자 이하로 입력해 주세요."];
            return null;
        }

        return trimmed;
    }

    private static string? TrimToNull(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
