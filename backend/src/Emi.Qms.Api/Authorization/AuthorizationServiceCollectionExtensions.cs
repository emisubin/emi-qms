using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Emi.Qms.Api.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddQmsAuthorizationFoundation(
        this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = DevelopmentAuthenticationDefaults.Scheme;
                options.DefaultChallengeScheme = DevelopmentAuthenticationDefaults.Scheme;
                options.DefaultForbidScheme = DevelopmentAuthenticationDefaults.Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                DevelopmentAuthenticationDefaults.Scheme,
                _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(QmsPolicies.ProjectRead, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(
                    new PermissionRequirement(QmsPermissions.ProjectRead),
                    new ProjectAccessRequirement());
            });

            options.AddPolicy(QmsPolicies.ProjectReadAll, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectReadAll));
            });

            options.AddPolicy(QmsPolicies.ProjectManage, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectManage));
            });

            options.AddPolicy(QmsPolicies.ProjectCreate, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectCreate));
            });

            options.AddPolicy(QmsPolicies.ProjectUpdate, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectUpdate));
            });

            options.AddPolicy(QmsPolicies.ProjectHold, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectHold));
            });

            options.AddPolicy(QmsPolicies.ProjectCancel, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectCancel));
            });

            options.AddPolicy(QmsPolicies.ProjectDelete, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectDelete));
            });

            options.AddPolicy(QmsPolicies.ProjectDeletedRead, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectDeletedRead));
            });

            options.AddPolicy(QmsPolicies.PanelInfoUpdate, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.PanelInfoUpdate));
            });

            options.AddPolicy(QmsPolicies.AuditReadAll, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.AuditReadAll));
            });

            options.AddPolicy(QmsPolicies.ProcurementPlanUpdate, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProcurementPlanUpdate));
            });

            options.AddPolicy(QmsPolicies.MaterialReceiptUpdate, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.MaterialReceiptUpdate));
            });

            options.AddPolicy(QmsPolicies.ManufacturingUpdate, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ManufacturingUpdate));
            });

            options.AddPolicy(QmsPolicies.ProjectSalesAmountRead, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProjectSalesAmountRead));
            });

            options.AddPolicy(QmsPolicies.ManufacturingWorkTimeRead, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ManufacturingWorkTimeRead));
            });

            options.AddPolicy(QmsPolicies.AdminUsersRead, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.UsersManage));
            });
        });

        services.AddSingleton<IIdentityStore, InMemoryIdentityStore>();
        services.AddSingleton<IAuthorizationAuditLogger, AuthorizationAuditLogger>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, ProjectAccessAuthorizationHandler>();

        return services;
    }
}
