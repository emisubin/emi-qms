namespace Emi.Qms.Api.Identity;

public static class SeedIdentityData
{
    public static readonly IReadOnlyList<Department> Departments =
    [
        new(new Guid("10000000-0000-0000-0000-000000000001"), "administration", "Administration"),
        new(new Guid("10000000-0000-0000-0000-000000000002"), "sales", "Sales"),
        new(new Guid("10000000-0000-0000-0000-000000000003"), "production-planning", "Production Planning"),
        new(new Guid("10000000-0000-0000-0000-000000000004"), "manufacturing", "Manufacturing"),
        new(new Guid("10000000-0000-0000-0000-000000000005"), "quality", "Quality"),
        new(new Guid("10000000-0000-0000-0000-000000000006"), "logistics", "Logistics"),
        new(new Guid("10000000-0000-0000-0000-000000000007"), "readonly", "Read Only"),
        new(new Guid("10000000-0000-0000-0000-000000000008"), "design", "Design")
    ];

    public static readonly IReadOnlyList<Role> Roles =
    [
        new(new Guid("20000000-0000-0000-0000-000000000001"), QmsRoles.SystemAdministrator, "System Administrator"),
        new(new Guid("20000000-0000-0000-0000-000000000002"), QmsRoles.Sales, "Sales User"),
        new(new Guid("20000000-0000-0000-0000-000000000003"), QmsRoles.ProductionPlanning, "Production Planning User"),
        new(new Guid("20000000-0000-0000-0000-000000000004"), QmsRoles.Manufacturing, "Manufacturing User"),
        new(new Guid("20000000-0000-0000-0000-000000000005"), QmsRoles.Quality, "Quality User"),
        new(new Guid("20000000-0000-0000-0000-000000000006"), QmsRoles.Logistics, "Logistics User"),
        new(new Guid("20000000-0000-0000-0000-000000000007"), QmsRoles.ReadOnly, "Read Only User"),
        new(new Guid("20000000-0000-0000-0000-000000000008"), QmsRoles.Design, "Design User")
    ];

    public static readonly IReadOnlyList<Permission> Permissions =
    [
        new(new Guid("30000000-0000-0000-0000-000000000001"), QmsPermissions.ProjectRead, "Read projects"),
        new(new Guid("30000000-0000-0000-0000-000000000002"), QmsPermissions.ProjectManage, "Manage project basics"),
        new(new Guid("30000000-0000-0000-0000-000000000003"), QmsPermissions.ProjectAccessAll, "Access every project"),
        new(new Guid("30000000-0000-0000-0000-000000000004"), QmsPermissions.ProductionPlan, "Manage production plans"),
        new(new Guid("30000000-0000-0000-0000-000000000005"), QmsPermissions.ManufacturingUpdate, "Update manufacturing records"),
        new(new Guid("30000000-0000-0000-0000-000000000006"), QmsPermissions.QualityInspect, "Record quality inspection"),
        new(new Guid("30000000-0000-0000-0000-000000000007"), QmsPermissions.QualityApprove, "Approve quality release"),
        new(new Guid("30000000-0000-0000-0000-000000000008"), QmsPermissions.LogisticsShip, "Manage packing and shipping"),
        new(new Guid("30000000-0000-0000-0000-000000000009"), QmsPermissions.UsersManage, "Manage users and roles"),
        new(new Guid("30000000-0000-0000-0000-000000000010"), QmsPermissions.ProjectReadAll, "Read all projects"),
        new(new Guid("30000000-0000-0000-0000-000000000011"), QmsPermissions.ProjectSalesAmountRead, "Read project sales amounts"),
        new(new Guid("30000000-0000-0000-0000-000000000012"), QmsPermissions.ManufacturingWorkTimeRead, "Read manufacturing work time"),
        new(new Guid("30000000-0000-0000-0000-000000000013"), QmsPermissions.ProjectCreate, "Create sales projects"),
        new(new Guid("30000000-0000-0000-0000-000000000014"), QmsPermissions.ProjectUpdate, "Update sales projects"),
        new(new Guid("30000000-0000-0000-0000-000000000015"), QmsPermissions.ProjectHold, "Hold sales projects"),
        new(new Guid("30000000-0000-0000-0000-000000000016"), QmsPermissions.ProjectCancel, "Cancel sales projects"),
        new(new Guid("30000000-0000-0000-0000-000000000017"), QmsPermissions.ProjectDelete, "Soft delete sales projects"),
        new(new Guid("30000000-0000-0000-0000-000000000018"), QmsPermissions.ProjectDeletedRead, "Read soft deleted projects"),
        new(new Guid("30000000-0000-0000-0000-000000000019"), QmsPermissions.PanelInfoUpdate, "Update panel information"),
        new(new Guid("30000000-0000-0000-0000-000000000020"), QmsPermissions.AuditReadAll, "Read all audit history")
    ];

    public static readonly IReadOnlyList<QmsProject> Projects =
    [
        new(new Guid("40000000-0000-0000-0000-000000000001"), "demo-project-alpha", "DEMO-24001", "Demo Project Alpha"),
        new(new Guid("40000000-0000-0000-0000-000000000002"), "demo-project-beta", "DEMO-24002", "Demo Project Beta")
    ];

    public static readonly IReadOnlyList<QmsUser> Users =
    [
        new(new Guid("50000000-0000-0000-0000-000000000001"), "dev-admin", "Dev System Administrator", "administration", true),
        new(new Guid("50000000-0000-0000-0000-000000000002"), "dev-sales", "Dev Sales User", "sales", true),
        new(new Guid("50000000-0000-0000-0000-000000000003"), "dev-production", "Dev Production Planning User", "production-planning", true),
        new(new Guid("50000000-0000-0000-0000-000000000004"), "dev-manufacturing", "Dev Manufacturing User", "manufacturing", true),
        new(new Guid("50000000-0000-0000-0000-000000000005"), "dev-quality", "Dev Quality User", "quality", true),
        new(new Guid("50000000-0000-0000-0000-000000000006"), "dev-logistics", "Dev Logistics User", "logistics", true),
        new(new Guid("50000000-0000-0000-0000-000000000007"), "dev-viewer", "Dev Read Only User", "readonly", true),
        new(new Guid("50000000-0000-0000-0000-000000000008"), "dev-no-role", "Dev User Without Role", "readonly", true),
        new(new Guid("50000000-0000-0000-0000-000000000009"), "dev-disabled", "Dev Disabled User", "readonly", false),
        new(new Guid("50000000-0000-0000-0000-000000000010"), "dev-design", "Dev Design User", "design", true)
    ];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [QmsRoles.SystemAdministrator] =
            [
                QmsPermissions.ProjectRead,
                QmsPermissions.ProjectManage,
                QmsPermissions.ProjectAccessAll,
                QmsPermissions.ProjectReadAll,
                QmsPermissions.ProjectSalesAmountRead,
                QmsPermissions.ProductionPlan,
                QmsPermissions.ManufacturingUpdate,
                QmsPermissions.ManufacturingWorkTimeRead,
                QmsPermissions.QualityInspect,
                QmsPermissions.QualityApprove,
                QmsPermissions.LogisticsShip,
                QmsPermissions.UsersManage,
                QmsPermissions.ProjectDeletedRead,
                QmsPermissions.AuditReadAll
            ],
            [QmsRoles.Sales] =
            [
                QmsPermissions.ProjectRead,
                QmsPermissions.ProjectManage,
                QmsPermissions.ProjectReadAll,
                QmsPermissions.ProjectCreate,
                QmsPermissions.ProjectUpdate,
                QmsPermissions.ProjectHold,
                QmsPermissions.ProjectCancel,
                QmsPermissions.ProjectDelete,
                QmsPermissions.ProjectDeletedRead,
                QmsPermissions.ProjectSalesAmountRead,
                QmsPermissions.ManufacturingWorkTimeRead,
                QmsPermissions.PanelInfoUpdate
            ],
            [QmsRoles.Design] =
            [
                QmsPermissions.ProjectRead,
                QmsPermissions.ProjectReadAll,
                QmsPermissions.PanelInfoUpdate
            ],
            [QmsRoles.ProductionPlanning] =
            [
                QmsPermissions.ProjectRead,
                QmsPermissions.ProjectReadAll,
                QmsPermissions.ProductionPlan,
                QmsPermissions.PanelInfoUpdate
            ],
            [QmsRoles.Manufacturing] =
            [
                QmsPermissions.ProjectRead,
                QmsPermissions.ProjectReadAll,
                QmsPermissions.ManufacturingUpdate
            ],
            [QmsRoles.Quality] =
            [
                QmsPermissions.ProjectRead,
                QmsPermissions.ProjectReadAll,
                QmsPermissions.QualityInspect,
                QmsPermissions.QualityApprove
            ],
            [QmsRoles.Logistics] =
            [
                QmsPermissions.ProjectRead,
                QmsPermissions.ProjectReadAll,
                QmsPermissions.LogisticsShip
            ],
            [QmsRoles.ReadOnly] =
            [
                QmsPermissions.ProjectRead,
                QmsPermissions.ProjectReadAll
            ]
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> UserRoles =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["dev-admin"] = [QmsRoles.SystemAdministrator],
            ["dev-sales"] = [QmsRoles.Sales],
            ["dev-production"] = [QmsRoles.ProductionPlanning],
            ["dev-manufacturing"] = [QmsRoles.Manufacturing],
            ["dev-quality"] = [QmsRoles.Quality],
            ["dev-logistics"] = [QmsRoles.Logistics],
            ["dev-viewer"] = [QmsRoles.ReadOnly],
            ["dev-no-role"] = [],
            ["dev-disabled"] = [QmsRoles.ReadOnly],
            ["dev-design"] = [QmsRoles.Design]
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> UserProjectAccess =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["dev-admin"] = [],
            ["dev-sales"] = ["demo-project-alpha"],
            ["dev-production"] = ["demo-project-alpha", "demo-project-beta"],
            ["dev-manufacturing"] = ["demo-project-alpha"],
            ["dev-quality"] = ["demo-project-alpha", "demo-project-beta"],
            ["dev-logistics"] = ["demo-project-beta"],
            ["dev-viewer"] = ["demo-project-alpha"],
            ["dev-no-role"] = ["demo-project-alpha"],
            ["dev-disabled"] = [],
            ["dev-design"] = []
        };
}
