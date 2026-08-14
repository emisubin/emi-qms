namespace Emi.Qms.Api.Identity;

public static class DepartmentIdentityPolicy
{
    private static readonly IReadOnlyDictionary<string, string> DefaultRoleCodes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["administration"] = QmsRoles.SystemAdministrator,
            ["sales"] = QmsRoles.Sales,
            ["design"] = QmsRoles.Design,
            ["production-planning"] = QmsRoles.ProductionPlanning,
            ["procurement"] = QmsRoles.Procurement,
            ["materials"] = QmsRoles.Materials,
            ["manufacturing"] = QmsRoles.Manufacturing,
            ["quality"] = QmsRoles.Quality,
            ["logistics"] = QmsRoles.Logistics,
            ["readonly"] = QmsRoles.ReadOnly
        };

    public static string? GetDefaultRoleCode(string departmentCode)
    {
        return DefaultRoleCodes.GetValueOrDefault(departmentCode);
    }

    public static string? GetFormTemplateDomain(string departmentCode)
    {
        return departmentCode switch
        {
            "quality" => "Quality",
            "production-planning" => "ProductionPlanning",
            _ => null
        };
    }
}
