using System.Globalization;
using System.Text.RegularExpressions;

namespace Emi.Qms.Api.Projects;

public static partial class ProjectInputNormalizer
{
    public static readonly IReadOnlySet<string> PackagingMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        "WoodenCrate",
        "StretchWrap",
        "HeavyDutyBox"
    };

    public const int CustomerNameMaxLength = 200;
    public const int ItemMaxLength = 100;
    public const int ProjectCodeMaxLength = 80;
    public const int ProjectTitleMaxLength = 200;
    public const int DeliveryLocationMaxLength = 300;
    public const int ReasonMaxLength = 500;

    public static string? TrimToNull(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    public static string NormalizeProjectTitle(string value)
    {
        return WhitespaceRegex()
            .Replace(value.Trim(), " ")
            .ToUpper(CultureInfo.InvariantCulture);
    }

    public static string NormalizeDisplayTitle(string value)
    {
        return WhitespaceRegex().Replace(value.Trim(), " ");
    }

    public static string? NormalizeCurrencyCode(string? value)
    {
        var trimmed = TrimToNull(value);
        return trimmed?.ToUpper(CultureInfo.InvariantCulture);
    }

    public static string FormatPanelDisplayCode(int sequenceNumber)
    {
        return sequenceNumber <= 99
            ? FormattableString.Invariant($"P{sequenceNumber:00}")
            : FormattableString.Invariant($"P{sequenceNumber}");
    }

    public static string FormatAuditValue(object? value)
    {
        return value switch
        {
            null => "",
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal amount => amount.ToString("0.##", CultureInfo.InvariantCulture),
            DateTimeOffset dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

public sealed class ProjectValidationResult
{
    private readonly Dictionary<string, string[]> errors = new(StringComparer.Ordinal);

    public bool HasErrors => errors.Count > 0;
    public IReadOnlyDictionary<string, string[]> Errors => errors;

    public void Add(string field, string message)
    {
        errors[field] = [message];
    }
}

public sealed record NormalizedCreateProjectInput(
    string CustomerName,
    string Item,
    string ProjectCode,
    string ProjectTitle,
    string ProjectTitleNormalized,
    int PanelCount,
    DateOnly DeliveryDate,
    Guid SalesOwnerUserId,
    string PackagingMethod,
    decimal? SalesAmount,
    string? CurrencyCode,
    string? DeliveryLocation);

public sealed record NormalizedUpdateProjectInput(
    string CustomerName,
    string Item,
    string ProjectCode,
    string ProjectTitle,
    string ProjectTitleNormalized,
    DateOnly DeliveryDate,
    Guid SalesOwnerUserId,
    string PackagingMethod,
    decimal? SalesAmount,
    string? CurrencyCode,
    string? DeliveryLocation,
    string Reason);

public sealed record NormalizedPanelCountChangeInput(
    int PanelCount,
    int ExpectedActivePanelCount,
    IReadOnlyList<Guid> CancelPanelIds,
    string Reason);

public sealed record NormalizedDeleteProjectInput(string Reason, string ConfirmProjectTitleNormalized);

public static partial class ProjectRequestValidator
{
    public static (NormalizedCreateProjectInput? Input, ProjectValidationResult Validation) ValidateCreate(
        CreateProjectRequest request)
    {
        var validation = new ProjectValidationResult();
        var customerName = RequiredText(request.CustomerName, nameof(request.CustomerName), ProjectInputNormalizer.CustomerNameMaxLength, validation);
        var item = RequiredText(request.Item, nameof(request.Item), ProjectInputNormalizer.ItemMaxLength, validation);
        var projectCode = RequiredText(request.ProjectCode, nameof(request.ProjectCode), ProjectInputNormalizer.ProjectCodeMaxLength, validation);
        var projectTitle = RequiredText(request.ProjectTitle, nameof(request.ProjectTitle), ProjectInputNormalizer.ProjectTitleMaxLength, validation);
        var panelCount = RequiredPanelCount(request.PanelCount, nameof(request.PanelCount), validation);
        var deliveryDate = RequiredDate(request.DeliveryDate, nameof(request.DeliveryDate), validation);
        var salesOwnerUserId = RequiredGuid(request.SalesOwnerUserId, nameof(request.SalesOwnerUserId), validation);
        var packagingMethod = RequiredPackagingMethod(request.PackagingMethod, nameof(request.PackagingMethod), validation);
        var salesAmount = ValidateSalesAmount(request.SalesAmount, validation);
        var currencyCode = ValidateCurrencyCode(request.CurrencyCode, salesAmount, validation);
        var deliveryLocation = OptionalText(request.DeliveryLocation, nameof(request.DeliveryLocation), ProjectInputNormalizer.DeliveryLocationMaxLength, validation);

        if (validation.HasErrors
            || customerName is null
            || item is null
            || projectCode is null
            || projectTitle is null
            || panelCount is null
            || deliveryDate is null
            || salesOwnerUserId is null
            || packagingMethod is null)
        {
            return (null, validation);
        }

        return (
            new NormalizedCreateProjectInput(
                customerName,
                item,
                projectCode,
                ProjectInputNormalizer.NormalizeDisplayTitle(projectTitle),
                ProjectInputNormalizer.NormalizeProjectTitle(projectTitle),
                panelCount.Value,
                deliveryDate.Value,
                salesOwnerUserId.Value,
                packagingMethod,
                salesAmount,
                currencyCode,
                deliveryLocation),
            validation);
    }

    public static (NormalizedUpdateProjectInput? Input, ProjectValidationResult Validation) ValidateUpdate(
        UpdateProjectRequest request)
    {
        var validation = new ProjectValidationResult();
        var customerName = RequiredText(request.CustomerName, nameof(request.CustomerName), ProjectInputNormalizer.CustomerNameMaxLength, validation);
        var item = RequiredText(request.Item, nameof(request.Item), ProjectInputNormalizer.ItemMaxLength, validation);
        var projectCode = RequiredText(request.ProjectCode, nameof(request.ProjectCode), ProjectInputNormalizer.ProjectCodeMaxLength, validation);
        var projectTitle = RequiredText(request.ProjectTitle, nameof(request.ProjectTitle), ProjectInputNormalizer.ProjectTitleMaxLength, validation);
        var deliveryDate = RequiredDate(request.DeliveryDate, nameof(request.DeliveryDate), validation);
        var salesOwnerUserId = RequiredGuid(request.SalesOwnerUserId, nameof(request.SalesOwnerUserId), validation);
        var packagingMethod = RequiredPackagingMethod(request.PackagingMethod, nameof(request.PackagingMethod), validation);
        var salesAmount = ValidateSalesAmount(request.SalesAmount, validation);
        var currencyCode = ValidateCurrencyCode(request.CurrencyCode, salesAmount, validation);
        var deliveryLocation = OptionalText(request.DeliveryLocation, nameof(request.DeliveryLocation), ProjectInputNormalizer.DeliveryLocationMaxLength, validation);
        var reason = RequiredText(request.Reason, nameof(request.Reason), ProjectInputNormalizer.ReasonMaxLength, validation);

        if (validation.HasErrors
            || customerName is null
            || item is null
            || projectCode is null
            || projectTitle is null
            || deliveryDate is null
            || salesOwnerUserId is null
            || packagingMethod is null
            || reason is null)
        {
            return (null, validation);
        }

        return (
            new NormalizedUpdateProjectInput(
                customerName,
                item,
                projectCode,
                ProjectInputNormalizer.NormalizeDisplayTitle(projectTitle),
                ProjectInputNormalizer.NormalizeProjectTitle(projectTitle),
                deliveryDate.Value,
                salesOwnerUserId.Value,
                packagingMethod,
                salesAmount,
                currencyCode,
                deliveryLocation,
                reason),
            validation);
    }

    public static (NormalizedPanelCountChangeInput? Input, ProjectValidationResult Validation) ValidatePanelCountChange(
        ChangePanelCountRequest request)
    {
        var validation = new ProjectValidationResult();
        var panelCount = RequiredPanelCount(request.PanelCount, nameof(request.PanelCount), validation);
        var expectedActivePanelCount = RequiredPanelCount(
            request.ExpectedActivePanelCount,
            nameof(request.ExpectedActivePanelCount),
            validation);
        var reason = RequiredText(request.Reason, nameof(request.Reason), ProjectInputNormalizer.ReasonMaxLength, validation);
        var cancelPanelIds = request.CancelPanelIds?.Distinct().ToList() ?? [];

        if (request.CancelPanelIds is not null && cancelPanelIds.Count != request.CancelPanelIds.Count)
        {
            validation.Add(nameof(request.CancelPanelIds), "취소할 패널은 중복 선택할 수 없습니다.");
        }

        if (validation.HasErrors || panelCount is null || expectedActivePanelCount is null || reason is null)
        {
            return (null, validation);
        }

        return (new NormalizedPanelCountChangeInput(panelCount.Value, expectedActivePanelCount.Value, cancelPanelIds, reason), validation);
    }

    public static (string? Reason, ProjectValidationResult Validation) ValidateReason(ProjectStatusChangeRequest request)
    {
        var validation = new ProjectValidationResult();
        var reason = RequiredText(request.Reason, nameof(request.Reason), ProjectInputNormalizer.ReasonMaxLength, validation);
        return (reason, validation);
    }

    public static (NormalizedDeleteProjectInput? Input, ProjectValidationResult Validation) ValidateDelete(
        DeleteProjectRequest request)
    {
        var validation = new ProjectValidationResult();
        var reason = RequiredText(request.Reason, nameof(request.Reason), ProjectInputNormalizer.ReasonMaxLength, validation);
        var confirmProjectTitle = RequiredText(
            request.ConfirmProjectTitle,
            nameof(request.ConfirmProjectTitle),
            ProjectInputNormalizer.ProjectTitleMaxLength,
            validation);

        if (validation.HasErrors || reason is null || confirmProjectTitle is null)
        {
            return (null, validation);
        }

        return (new NormalizedDeleteProjectInput(reason, ProjectInputNormalizer.NormalizeProjectTitle(confirmProjectTitle)), validation);
    }

    private static string? RequiredText(
        string? value,
        string fieldName,
        int maxLength,
        ProjectValidationResult validation)
    {
        var trimmed = ProjectInputNormalizer.TrimToNull(value);
        if (trimmed is null)
        {
            validation.Add(fieldName, "필수 입력값입니다.");
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            validation.Add(fieldName, $"최대 {maxLength}자까지 입력할 수 있습니다.");
            return null;
        }

        return trimmed;
    }

    private static string? OptionalText(
        string? value,
        string fieldName,
        int maxLength,
        ProjectValidationResult validation)
    {
        var trimmed = ProjectInputNormalizer.TrimToNull(value);
        if (trimmed is not null && trimmed.Length > maxLength)
        {
            validation.Add(fieldName, $"최대 {maxLength}자까지 입력할 수 있습니다.");
            return null;
        }

        return trimmed;
    }

    private static int? RequiredPanelCount(int? value, string fieldName, ProjectValidationResult validation)
    {
        if (value is null)
        {
            validation.Add(fieldName, "필수 입력값입니다.");
            return null;
        }

        if (value < 1)
        {
            validation.Add(fieldName, "1 이상의 정수여야 합니다.");
            return null;
        }

        if (value > ProjectDomainRules.MaxPanelsPerProject)
        {
            validation.Add(fieldName, $"1 이상 {ProjectDomainRules.MaxPanelsPerProject} 이하의 정수여야 합니다.");
            return null;
        }

        return value;
    }

    private static DateOnly? RequiredDate(DateOnly? value, string fieldName, ProjectValidationResult validation)
    {
        if (value is null)
        {
            validation.Add(fieldName, "필수 날짜입니다.");
        }

        return value;
    }

    private static string? RequiredPackagingMethod(string? value, string fieldName, ProjectValidationResult validation)
    {
        var trimmed = ProjectInputNormalizer.TrimToNull(value);
        if (trimmed is null)
        {
            validation.Add(fieldName, "포장방식은 필수 선택값입니다.");
            return null;
        }

        if (!ProjectInputNormalizer.PackagingMethods.Contains(trimmed))
        {
            validation.Add(fieldName, "허용되지 않은 포장방식입니다.");
            return null;
        }

        return trimmed;
    }

    private static Guid? RequiredGuid(Guid? value, string fieldName, ProjectValidationResult validation)
    {
        if (value is null || value == Guid.Empty)
        {
            validation.Add(fieldName, "필수 선택값입니다.");
            return null;
        }

        return value;
    }

    private static decimal? ValidateSalesAmount(decimal? value, ProjectValidationResult validation)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 0)
        {
            validation.Add(nameof(CreateProjectRequest.SalesAmount), "0 이상의 금액이어야 합니다.");
            return null;
        }

        if (value > 9999999999999999.99m)
        {
            validation.Add(nameof(CreateProjectRequest.SalesAmount), "입력 가능한 금액 범위를 초과했습니다.");
            return null;
        }

        return decimal.Round(value.Value, 2);
    }

    private static string? ValidateCurrencyCode(
        string? value,
        decimal? salesAmount,
        ProjectValidationResult validation)
    {
        var currencyCode = ProjectInputNormalizer.NormalizeCurrencyCode(value);
        if (salesAmount is not null && currencyCode is null)
        {
            validation.Add(nameof(CreateProjectRequest.CurrencyCode), "판매금액 입력 시 통화가 필요합니다.");
            return null;
        }

        if (currencyCode is not null && !CurrencyRegex().IsMatch(currencyCode))
        {
            validation.Add(nameof(CreateProjectRequest.CurrencyCode), "통화는 ISO 4217 형식의 3자리 대문자여야 합니다.");
            return null;
        }

        return currencyCode;
    }

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyRegex();
}
