using Emi.Qms.Api.Identity;
namespace Emi.Qms.Api.InteriorBusbar;

public sealed record BusbarAccess(bool Projects, bool Planning, bool Production, bool Purchases, bool Administration)
{
    public bool Any => Projects || Planning || Production || Purchases || Administration;

    public static BusbarAccess For(UserAuthorizationProfile profile, bool overallAdministrator)
    {
        var admin = overallAdministrator || profile.Roles.Any(r => r.Code is InteriorBusbarEndpointExtensions.ManagerRole or QmsRoles.SystemAdministrator);
        // Team ownership follows the current department, not a stale or separately assigned department role.
        var department = profile.Department?.Code;
        return new(admin || department == "sales", admin || department == "production-planning",
            admin || department == "manufacturing", admin || department == "production-planning", admin);
    }

    public bool Allows(string? route) => route switch
    {
        "/api/interior-busbar/projects" or "/api/interior-busbar/shipments" or
        "/api/interior-busbar/projects/import/preview" or "/api/interior-busbar/projects/import/apply" => Projects,
        "/api/interior-busbar/plans" => Planning,
        "/api/interior-busbar/purchases" or "/api/interior-busbar/receipts" or
        "/api/interior-busbar/purchases/import/preview" or "/api/interior-busbar/purchases/import/apply" => Purchases,
        "/api/interior-busbar/products" or "/api/interior-busbar/products/{id:guid}" or
        "/api/interior-busbar/products/{id:guid}/photos/{side}" or
        "/api/interior-busbar/products/{id:guid}/publication/retry" => Production,
        "/api/interior-busbar/product-families" or "/api/interior-busbar/materials" or "/api/interior-busbar/workers" or
        "/api/interior-busbar/boms" or "/api/interior-busbar/settings" or
        "/api/interior-busbar/adjustments" or "/api/interior-busbar/ledger/{id:guid}/reverse" or
        "/api/interior-busbar/products/{id:guid}/cancel" or "/api/interior-busbar/ecount/resume" or
        "/api/interior-busbar/ecount-jobs/{id:guid}/retry" or "/api/interior-busbar/ecount-jobs/{id:guid}/reconcile" => Administration,
        _ => false
    };
}
