namespace Emi.Qms.Api.ProductionPlanning;

public static class ProductionPlanningDomain
{
    public const string NotPlanned = "NotPlanned";
    public const string Planning = "Planning";
    public const string Planned = "Planned";

    public static readonly IReadOnlyList<string> Responsibilities =
    [
        "SalesPrimary",
        "SalesSecondary",
        "DesignPrimary",
        "DesignSecondary",
        "ProductionPlanningPrimary",
        "ProductionPlanningSecondary",
        "ProcurementPrimary",
        "ProcurementSecondary",
        "MaterialsPrimary",
        "MaterialsSecondary",
        "ManufacturingPrimary",
        "ManufacturingSecondary",
        "LogisticsPrimary",
        "LogisticsSecondary",
        "QualityIQC",
        "QualityIQCSecondary",
        "QualityLQC",
        "QualityLQCSecondary",
        "QualityOQC",
        "QualityOQCSecondary",
        "QualityCustomerInspection",
        "QualityCustomerInspectionSecondary"
    ];

    public static readonly IReadOnlySet<string> CanonicalProductTypeCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "UL67",
        "UL891",
        "UL508A",
        "IEC",
        "LLP",
        "RPP"
    };

    public static string StatusLabel(string status)
    {
        return status switch
        {
            NotPlanned => "미등록",
            Planning => "작성 중",
            Planned => "계획 완료",
            _ => "미등록"
        };
    }

    public static string ResponsibilityLabel(string responsibilityType)
    {
        return responsibilityType switch
        {
            "SalesPrimary" => "영업 정",
            "SalesSecondary" => "영업 부",
            "DesignPrimary" => "설계 정",
            "DesignSecondary" => "설계 부",
            "ProductionPlanningPrimary" => "생산관리 정",
            "ProductionPlanningSecondary" => "생산관리 부",
            "ProcurementPrimary" => "구매 정",
            "ProcurementSecondary" => "구매 부",
            "MaterialsPrimary" => "자재 정",
            "MaterialsSecondary" => "자재 부",
            "ManufacturingPrimary" => "제조 정",
            "ManufacturingSecondary" => "제조 부",
            "LogisticsPrimary" => "물류 정",
            "LogisticsSecondary" => "물류 부",
            "QualityIQC" => "IQC 정",
            "QualityIQCSecondary" => "IQC 부",
            "QualityLQC" => "LQC 정",
            "QualityLQCSecondary" => "LQC 부",
            "QualityOQC" => "OQC 정",
            "QualityOQCSecondary" => "OQC 부",
            "QualityCustomerInspection" => "전진검수/FAT 정",
            "QualityCustomerInspectionSecondary" => "전진검수/FAT 부",
            "Procurement" => "구매 담당자",
            "ProductionPlanning" => "생산관리 담당자",
            "Manufacturing" => "제조 담당자",
            "Quality" => "품질 담당자",
            "Logistics" => "물류 담당자",
            _ => responsibilityType
        };
    }

    public static string RoleForResponsibility(string responsibilityType)
    {
        return responsibilityType switch
        {
            "SalesPrimary" or "SalesSecondary" => "sales",
            "DesignPrimary" or "DesignSecondary" => "design",
            "ProductionPlanningPrimary" or "ProductionPlanningSecondary" => "production-planning",
            "ProcurementPrimary" or "ProcurementSecondary" => "procurement",
            "MaterialsPrimary" or "MaterialsSecondary" => "materials",
            "ManufacturingPrimary" or "ManufacturingSecondary" => "manufacturing",
            "LogisticsPrimary" or "LogisticsSecondary" => "logistics",
            "QualityIQC" or "QualityIQCSecondary"
                or "QualityLQC" or "QualityLQCSecondary"
                or "QualityOQC" or "QualityOQCSecondary"
                or "QualityCustomerInspection" or "QualityCustomerInspectionSecondary" => "quality",
            "Procurement" => "procurement",
            "ProductionPlanning" => "production-planning",
            "Manufacturing" => "manufacturing",
            "Quality" => "quality",
            "Logistics" => "logistics",
            _ => ""
        };
    }

    public static string? LegacyResponsibilityAlias(string responsibilityType)
    {
        return responsibilityType switch
        {
            "ProcurementPrimary" => "Procurement",
            "ProductionPlanningPrimary" => "ProductionPlanning",
            "ManufacturingPrimary" => "Manufacturing",
            "QualityIQC" => "Quality",
            "LogisticsPrimary" => "Logistics",
            _ => null
        };
    }

    public static string CalculateStatus(Guid? productTypeId, IReadOnlyList<ProductionPlanItemResponse> items)
    {
        if (productTypeId is null)
        {
            return NotPlanned;
        }

        var required = items.Where(item => item.IsRequired).ToList();
        if (required.Count > 0 && required.All(item => item.PlannedDate is not null))
        {
            return Planned;
        }

        return Planning;
    }
}
