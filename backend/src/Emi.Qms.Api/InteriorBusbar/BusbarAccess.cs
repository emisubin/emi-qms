using Emi.Qms.Api.Identity;
namespace Emi.Qms.Api.InteriorBusbar;

public sealed record BusbarAccess(bool Projects, bool Planning, bool Production, bool Purchases, bool Administration)
{
    public bool MastersRead { get; init; }
    public bool MastersWrite { get; init; }
    public bool ManageMasterPermissions { get; init; }
    public bool Inspection { get; init; }
    public bool Any => Inspection || MastersWrite || Projects || Planning || Production || Purchases || Administration;

    public static BusbarAccess For(UserAuthorizationProfile profile, bool overallAdministrator)
    {
        var manage = overallAdministrator || profile.Roles.Any(r => r.Code == QmsRoles.SystemAdministrator);
        var admin = overallAdministrator || profile.Roles.Any(r => r.Code is InteriorBusbarEndpointExtensions.ManagerRole or QmsRoles.SystemAdministrator);
        // Team ownership follows the current department, not a stale or separately assigned department role.
        var department = profile.Department?.Code;
        return new(admin || department == "sales", admin || department == "production-planning",
            admin || department == "manufacturing", admin || department == "production-planning", admin) { Inspection = department == "quality", MastersRead = manage, MastersWrite = manage, ManageMasterPermissions = manage };
    }

    public bool Allows(string? route) => route switch
    {
        "/api/interior-busbar/products/{id:guid}/inspection" => Inspection,
        "/api/interior-busbar/projects" or "/api/interior-busbar/projects/{id:guid}/delete" or "/api/interior-busbar/projects/{id:guid}/restore" or "/api/interior-busbar/shipments" or
        "/api/interior-busbar/projects/import/preview" or "/api/interior-busbar/projects/import/apply" => Projects,
        "/api/interior-busbar/plans" or "/api/interior-busbar/plans/{id:guid}/delete" or "/api/interior-busbar/plans/{id:guid}/restore" => Planning,
        "/api/interior-busbar/purchases" or "/api/interior-busbar/purchases/{id:guid}/delete" or "/api/interior-busbar/purchases/{id:guid}/restore" or "/api/interior-busbar/receipts" or
        "/api/interior-busbar/purchases/import/preview" or "/api/interior-busbar/purchases/import/apply" => Purchases,
        "/api/interior-busbar/labels/printed" or "/api/interior-busbar/labels/attached" or
        "/api/interior-busbar/products" or "/api/interior-busbar/products/{id:guid}" or
        "/api/interior-busbar/products/{id:guid}/photos/{side}" or
        "/api/interior-busbar/products/{id:guid}/photo-preview" or
        "/api/interior-busbar/products/{id:guid}/publication/retry" => Production,
        "/api/interior-busbar/product-families" or "/api/interior-busbar/product-families/{id:guid}/delete" or "/api/interior-busbar/product-families/{id:guid}/restore" or
        "/api/interior-busbar/materials" or "/api/interior-busbar/materials/{id:guid}/delete" or "/api/interior-busbar/materials/{id:guid}/restore" or
        "/api/interior-busbar/workers" or "/api/interior-busbar/workers/{id:guid}/delete" or "/api/interior-busbar/workers/{id:guid}/restore" or
        "/api/interior-busbar/boms" or "/api/interior-busbar/boms/{id:guid}/delete" or "/api/interior-busbar/boms/{id:guid}/restore" or "/api/interior-busbar/settings" => MastersWrite,
        "/api/interior-busbar/master-access" => ManageMasterPermissions,
        "/api/interior-busbar/adjustments" or "/api/interior-busbar/ledger/{id:guid}/reverse" or
        "/api/interior-busbar/products/{id:guid}/cancel" or "/api/interior-busbar/ecount/resume" or
        "/api/interior-busbar/ecount-jobs/{id:guid}/retry" or "/api/interior-busbar/ecount-jobs/{id:guid}/reconcile" => Administration,
        _ => false
    };
}
