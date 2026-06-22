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
