using Xunit;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.BusinessUnits;

namespace Emi.Qms.Api.Tests;

public sealed class OsanDepartmentPermissionsTests
{
    [Theory]
    [InlineData("sales", true, false)]
    [InlineData("production-planning", true, false)]
    [InlineData("manufacturing", false, true)]
    [InlineData("quality", false, true)]
    [InlineData("design", false, false)]
    [InlineData("procurement", false, false)]
    [InlineData("materials", false, false)]
    [InlineData("logistics", false, false)]
    [InlineData("readonly", false, false)]
    public void OsanDepartmentDeterminesInputAndAllProjectsAreReadable(string department, bool create, bool progress)
    {
        var profile = Profile(department);
        var result = OsanDepartmentPermissions.Apply(profile, BusinessUnitCodes.Osan);
        Assert.True(result.HasPermission(QmsPermissions.ProjectReadAll));
        Assert.Equal(create, result.HasPermission(QmsPermissions.ProjectCreate));
        Assert.Equal(progress, result.HasPermission(QmsPermissions.ManufacturingUpdate));
        Assert.Same(profile, OsanDepartmentPermissions.Apply(profile, BusinessUnitCodes.Cheongju));
    }

    [Fact]
    public void InactiveOrUnapprovedProfilesDoNotGainPermissions()
    {
        var profile = Profile("quality");
        var inactive = profile with { User = profile.User with { IsActive = false }, Permissions = [] };
        var unapproved = profile with { Roles = [], Permissions = [] };
        Assert.Same(inactive, OsanDepartmentPermissions.Apply(inactive, BusinessUnitCodes.Osan));
        Assert.Same(unapproved, OsanDepartmentPermissions.Apply(unapproved, BusinessUnitCodes.Osan));
        Assert.Same(profile, OsanDepartmentPermissions.Apply(profile, null));
    }

    [Fact]
    public void AdministratorRetainsAllPermissionsRegardlessOfDepartment()
    {
        var profile = Profile("administration") with { Roles = [new Role(Guid.NewGuid(), QmsRoles.SystemAdministrator, "Admin")] };
        var result = OsanDepartmentPermissions.Apply(profile, BusinessUnitCodes.Osan);
        Assert.True(result.HasPermission(QmsPermissions.ProjectCreate));
        Assert.True(result.HasPermission(QmsPermissions.ManufacturingUpdate));
    }

    private static UserAuthorizationProfile Profile(string department) => new(
        new QmsUser(Guid.NewGuid(), "synthetic", "Synthetic", department, true),
        new Department(Guid.NewGuid(), department, department),
        [new Role(Guid.NewGuid(), "read-only", "Read only")],
        SeedIdentityData.Permissions.Where(p => new[] { QmsPermissions.ProjectRead, QmsPermissions.ProjectCreate, QmsPermissions.ManufacturingUpdate }.Contains(p.Code)).ToArray(), []);
}
