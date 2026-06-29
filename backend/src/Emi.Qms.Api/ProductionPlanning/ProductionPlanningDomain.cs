namespace Emi.Qms.Api.ProductionPlanning;

public static class ProductionPlanningDomain
{
    public const string NotPlanned = "NotPlanned";
    public const string Planning = "Planning";
    public const string Planned = "Planned";

    public static readonly IReadOnlyList<string> Responsibilities =
    [
        "Procurement",
        "ProductionPlanning",
        "Manufacturing",
        "Quality",
        "Logistics"
    ];

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
            "Procurement" => "procurement",
            "ProductionPlanning" => "production-planning",
            "Manufacturing" => "manufacturing",
            "Quality" => "quality",
            "Logistics" => "logistics",
            _ => ""
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
