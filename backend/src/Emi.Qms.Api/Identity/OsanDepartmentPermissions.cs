using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.Identity;

public static class OsanDepartmentPermissions
{
    public static UserAuthorizationProfile Apply(UserAuthorizationProfile profile, string? businessUnitCode)
    {
        // Apply only after the local profile has its existing approved read access.
        if (businessUnitCode != BusinessUnitCodes.Osan || !profile.User.IsActive
            || !profile.HasPermission(QmsPermissions.ProjectRead)) return profile;

        var codes = profile.Permissions.Select(permission => permission.Code).ToHashSet(StringComparer.Ordinal);
        codes.Add(QmsPermissions.ProjectReadAll);
        if (!profile.Roles.Any(role => role.Code == QmsRoles.SystemAdministrator))
        {
            codes.Remove(QmsPermissions.ProjectCreate);
            codes.Remove(QmsPermissions.ManufacturingUpdate);
            if (profile.Department?.Code is "sales" or "production-planning")
                codes.Add(QmsPermissions.ProjectCreate);
            if (profile.Department?.Code is "manufacturing" or "quality")
                codes.Add(QmsPermissions.ManufacturingUpdate);
        }
        return profile with
        {
            Permissions = profile.Permissions.Where(permission => codes.Contains(permission.Code))
                .Concat(SeedIdentityData.Permissions.Where(permission => codes.Contains(permission.Code)
                    && !profile.Permissions.Any(existing => existing.Code == permission.Code))).ToArray()
        };
    }
}
