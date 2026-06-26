using System.Globalization;
using System.Text.RegularExpressions;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.PanelInformation;

public static partial class PanelInformationDomain
{
    public const int PanelNameMaxLength = 200;
    public const decimal MaxDimensionMm = 100000m;
    public const decimal InchToMm = 25.4m;
    public const int MaxExcelRows = 500;
    public const long MaxExcelFileSizeBytes = 10 * 1024 * 1024;
    public const long MaxExcelMultipartRequestBytes = 11 * 1024 * 1024;
    public const int MaxExcelZipEntries = 2000;
    public const long MaxExcelUncompressedBytes = 50 * 1024 * 1024;
    public const long MaxExcelEntryUncompressedBytes = 20 * 1024 * 1024;
    public const int MaxExcelWorksheets = 20;
    public const int MaxExcelColumns = 64;
    public const int MaxExcelCells = 35000;
    public const int MaxExcelHeaderSearchRows = 20;
    public const string StaleVersionMessage = "다른 사용자가 패널정보를 수정했습니다. 화면을 새로고침한 후 다시 시도해 주세요.";

    public static readonly IReadOnlySet<string> InputUnits = new HashSet<string>(StringComparer.Ordinal)
    {
        "Mm",
        "Inch"
    };

    public static string? NormalizePanelName(string? value)
    {
        var trimmed = ProjectInputNormalizer.TrimToNull(value);
        return trimmed is null ? null : trimmed;
    }

    public static string? NormalizeDuplicateName(string? value)
    {
        var trimmed = NormalizePanelName(value);
        return trimmed is null
            ? null
            : WhitespaceRegex().Replace(trimmed, " ").ToUpper(CultureInfo.InvariantCulture);
    }

    public static string PanelNumber(int sequenceNumber)
    {
        return FormattableString.Invariant($"No.{sequenceNumber}");
    }

    public static string DisplayName(int sequenceNumber, string? panelName)
    {
        return panelName is null
            ? $"{PanelNumber(sequenceNumber)} · 패널명 미정"
            : $"{PanelNumber(sequenceNumber)} · {panelName}";
    }

    public static bool IsPanelInfoCompleted(
        string? packagingMethod,
        string? panelName,
        decimal? widthMm,
        decimal? heightMm,
        decimal? depthMm)
    {
        if (panelName is null || packagingMethod is null)
        {
            return false;
        }

        var hasAllSize = widthMm is not null && heightMm is not null && depthMm is not null;
        var hasNoSize = widthMm is null && heightMm is null && depthMm is null;

        return packagingMethod switch
        {
            "WoodenCrate" => hasAllSize,
            "StretchWrap" or "HeavyDutyBox" => hasAllSize || hasNoSize,
            _ => false
        };
    }

    public static bool IsQrEligible(
        bool projectDeleted,
        string projectStatus,
        string panelStatus,
        string? panelName)
    {
        return !projectDeleted
            && projectStatus == "Active"
            && panelStatus == "Active"
            && panelName is not null;
    }

    public static decimal ConvertToMm(decimal value, string inputUnit)
    {
        var converted = inputUnit == "Inch" ? value * InchToMm : value;
        return decimal.Round(converted, 3, MidpointRounding.AwayFromZero);
    }

    public static string FormatDecimal(decimal? value)
    {
        return value is null
            ? ""
            : value.Value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

public sealed record NormalizedPanelInformationBulkUpdateInput(
    string? Reason,
    IReadOnlyList<NormalizedPanelInformationUpdateItem> Panels);

public sealed record NormalizedPanelInformationUpdateItem(
    Guid PanelId,
    int ExpectedPanelInfoVersion,
    bool PanelNameChanged,
    string? PanelName,
    bool SizeChanged,
    decimal? WidthMm,
    decimal? HeightMm,
    decimal? DepthMm,
    string? SizeInputUnit,
    decimal? OriginalWidth,
    decimal? OriginalHeight,
    decimal? OriginalDepth);

public static class PanelInformationRequestValidator
{
    public static (NormalizedPanelInformationBulkUpdateInput? Input, ProjectValidationResult Validation) ValidateBulkUpdate(
        PanelInformationBulkUpdateRequest request)
    {
        var validation = new ProjectValidationResult();
        var reason = OptionalReason(request.Reason, validation);
        var panels = request.Panels ?? [];
        if (panels.Count == 0)
        {
            validation.Add(nameof(request.Panels), "저장할 패널을 1개 이상 선택해야 합니다.");
        }

        var duplicatePanelIds = panels
            .Where(panel => panel.PanelId is not null)
            .GroupBy(panel => panel.PanelId!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicatePanelIds.Count > 0)
        {
            validation.Add(nameof(request.Panels), "같은 패널을 한 요청에 중복 입력할 수 없습니다.");
        }

        var normalizedItems = new List<NormalizedPanelInformationUpdateItem>();
        for (var index = 0; index < panels.Count; index++)
        {
            var panel = panels[index];
            var fieldPrefix = $"Panels[{index}]";
            if (panel.PanelId is null || panel.PanelId == Guid.Empty)
            {
                validation.Add($"{fieldPrefix}.PanelId", "패널 선택값이 필요합니다.");
                continue;
            }

            if (panel.ExpectedPanelInfoVersion is null || panel.ExpectedPanelInfoVersion < 0)
            {
                validation.Add($"{fieldPrefix}.ExpectedPanelInfoVersion", "패널정보 버전이 필요합니다.");
                continue;
            }

            var panelNameChanged = panel.PanelNameUpdate?.IsChanged == true;
            var panelName = panelNameChanged
                ? PanelInformationDomain.NormalizePanelName(panel.PanelNameUpdate?.Value)
                : null;
            if (panelNameChanged && panelName is not null && panelName.Length > PanelInformationDomain.PanelNameMaxLength)
            {
                validation.Add($"{fieldPrefix}.PanelNameUpdate.Value", $"패널명은 최대 {PanelInformationDomain.PanelNameMaxLength}자까지 입력할 수 있습니다.");
                continue;
            }

            var sizeChanged = panel.SizeUpdate?.IsChanged == true;
            var sizeInputUnit = (string?)null;
            NormalizedPanelSize? size = null;
            if (sizeChanged)
            {
                if (panel.SizeUpdate?.Clear == true)
                {
                    size = new NormalizedPanelSize(null, null, null);
                }
                else
                {
                    var sizeUnitValidation = new ProjectValidationResult();
                    sizeInputUnit = NormalizeInputUnit(panel.SizeUpdate?.InputUnit, sizeUnitValidation, requireWhenMissing: false);
                    foreach (var error in sizeUnitValidation.Errors.Values.SelectMany(value => value))
                    {
                        validation.Add($"{fieldPrefix}.SizeUpdate.InputUnit", error);
                    }

                    size = NormalizeSize(
                        panel.SizeUpdate?.Width,
                        panel.SizeUpdate?.Height,
                        panel.SizeUpdate?.Depth,
                        sizeInputUnit,
                        $"{fieldPrefix}.SizeUpdate",
                        validation);
                    if (size is null)
                    {
                        continue;
                    }
                }
            }

            normalizedItems.Add(new NormalizedPanelInformationUpdateItem(
                panel.PanelId.Value,
                panel.ExpectedPanelInfoVersion.Value,
                panelNameChanged,
                panelName,
                sizeChanged,
                size?.WidthMm,
                size?.HeightMm,
                size?.DepthMm,
                sizeInputUnit,
                panel.SizeUpdate?.Width,
                panel.SizeUpdate?.Height,
                panel.SizeUpdate?.Depth));
        }

        if (validation.HasErrors)
        {
            return (null, validation);
        }

        return (new NormalizedPanelInformationBulkUpdateInput(reason, normalizedItems), validation);
    }

    public static string? NormalizeInputUnit(
        string? value,
        ProjectValidationResult validation,
        bool requireWhenMissing)
    {
        var trimmed = ProjectInputNormalizer.TrimToNull(value);
        if (trimmed is null)
        {
            if (requireWhenMissing)
            {
                validation.Add("InputUnit", "치수 입력 시 단위 선택이 필요합니다.");
            }

            return null;
        }

        var normalized = trimmed.Equals("mm", StringComparison.OrdinalIgnoreCase)
            ? "Mm"
            : trimmed.Equals("inch", StringComparison.OrdinalIgnoreCase)
                ? "Inch"
                : trimmed;
        if (!PanelInformationDomain.InputUnits.Contains(normalized))
        {
            validation.Add("InputUnit", "입력 단위는 Mm 또는 Inch만 허용됩니다.");
            return null;
        }

        return normalized;
    }

    public static string? OptionalReason(string? value, ProjectValidationResult validation)
    {
        var reason = ProjectInputNormalizer.TrimToNull(value);
        if (reason is not null && reason.Length > ProjectInputNormalizer.ReasonMaxLength)
        {
            validation.Add("Reason", $"사유는 최대 {ProjectInputNormalizer.ReasonMaxLength}자까지 입력할 수 있습니다.");
            return null;
        }

        return reason;
    }

    public static NormalizedPanelSize? NormalizeSize(
        decimal? width,
        decimal? height,
        decimal? depth,
        string? inputUnit,
        string fieldPrefix,
        ProjectValidationResult validation)
    {
        var suppliedCount = new[] { width, height, depth }.Count(value => value is not null);
        if (suppliedCount == 0)
        {
            return new NormalizedPanelSize(null, null, null);
        }

        if (suppliedCount != 3)
        {
            validation.Add($"{fieldPrefix}.Size", "W/H/D는 모두 비우거나 모두 입력해야 합니다.");
            return null;
        }

        if (inputUnit is null)
        {
            validation.Add("InputUnit", "치수 입력 시 단위 선택이 필요합니다.");
            return null;
        }

        var widthMm = NormalizeDimension(width!.Value, inputUnit, $"{fieldPrefix}.Width", validation);
        var heightMm = NormalizeDimension(height!.Value, inputUnit, $"{fieldPrefix}.Height", validation);
        var depthMm = NormalizeDimension(depth!.Value, inputUnit, $"{fieldPrefix}.Depth", validation);
        return widthMm is null || heightMm is null || depthMm is null
            ? null
            : new NormalizedPanelSize(widthMm, heightMm, depthMm);
    }

    private static decimal? NormalizeDimension(
        decimal value,
        string inputUnit,
        string fieldName,
        ProjectValidationResult validation)
    {
        if (value <= 0)
        {
            validation.Add(fieldName, "치수는 0보다 커야 합니다.");
            return null;
        }

        var mm = PanelInformationDomain.ConvertToMm(value, inputUnit);
        if (mm > PanelInformationDomain.MaxDimensionMm)
        {
            validation.Add(fieldName, "치수는 100000mm 이하만 허용됩니다.");
            return null;
        }

        return mm;
    }
}

public sealed record NormalizedPanelSize(decimal? WidthMm, decimal? HeightMm, decimal? DepthMm);
