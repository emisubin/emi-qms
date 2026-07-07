using Emi.Qms.Api.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Emi.Qms.Api.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddQmsAuthorizationFoundation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var hasAzureAdConfiguration = QmsAuthenticationModePolicy.HasRequiredAzureAdConfiguration(configuration);

        var authenticationBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = QmsAuthenticationSchemes.Auto;
                options.DefaultChallengeScheme = QmsAuthenticationSchemes.Auto;
                options.DefaultForbidScheme = QmsAuthenticationSchemes.Auto;
            })
            .AddPolicyScheme(QmsAuthenticationSchemes.Auto, "QMS authentication", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var currentAuthenticationMode = QmsAuthenticationModePolicy.Resolve(
                        context.RequestServices.GetRequiredService<IHostEnvironment>(),
                        context.RequestServices.GetRequiredService<IConfiguration>());
                    var authorization = context.Request.Headers.Authorization.ToString();
                    if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return QmsAuthenticationSchemes.EntraBearer;
                    }

                    if (currentAuthenticationMode == QmsAuthenticationMode.Dev
                        && context.Request.Headers.ContainsKey(DevelopmentAuthenticationDefaults.UserHeader))
                    {
                        return DevelopmentAuthenticationDefaults.Scheme;
                    }

                    return currentAuthenticationMode == QmsAuthenticationMode.Dev
                        ? DevelopmentAuthenticationDefaults.Scheme
                        : QmsAuthenticationSchemes.EntraBearer;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                DevelopmentAuthenticationDefaults.Scheme,
                _ => { });

        if (hasAzureAdConfiguration)
        {
            authenticationBuilder.AddMicrosoftIdentityWebApi(
                jwtBearerOptions =>
                {
                    var audience = configuration["AzureAd:Audience"];
                    var validAudience = configuration["AzureAd:ValidAudience"];
                    var clientId = configuration["AzureAd:ClientId"];
                    jwtBearerOptions.TokenValidationParameters.ValidAudiences = new[]
                    {
                        audience,
                        validAudience,
                        clientId,
                        string.IsNullOrWhiteSpace(clientId) ? null : $"api://{clientId}"
                    }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
                },
                identityOptions =>
                {
                    identityOptions.Instance = configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/";
                    identityOptions.TenantId = configuration["AzureAd:TenantId"] ?? "";
                    identityOptions.ClientId = configuration["AzureAd:ClientId"] ?? "";
                    identityOptions.Domain = configuration["AzureAd:Domain"];
                },
                QmsAuthenticationSchemes.EntraBearer,
                subscribeToJwtBearerMiddlewareDiagnosticsEvents: false);
        }
        else
        {
            authenticationBuilder.AddScheme<AuthenticationSchemeOptions, UnavailableBearerAuthenticationHandler>(
                QmsAuthenticationSchemes.EntraBearer,
                _ => { });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AuthenticatedIdentity", policy =>
            {
                policy.RequireAuthenticatedUser();
            });
            options.DefaultPolicy = new AuthorizationPolicyBuilder(QmsAuthenticationSchemes.Auto)
                .RequireAuthenticatedUser()
                .AddRequirements(new OperationalUserRequirement())
                .Build();

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

            options.AddPolicy(QmsPolicies.ProductionPlanUpdate, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.ProductionPlanUpdate));
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

            options.AddPolicy(QmsPolicies.AdminHistoryRead, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(QmsPermissions.AdminHistoryRead));
            });
        });

        services.AddSingleton<InMemoryIdentityStore>();
        services.AddSingleton<DbIdentityStore>();
        services.AddSingleton<IIdentityStore, HybridIdentityStore>();
        services.AddSingleton<IUserAdministrationStore, UserAdministrationStore>();
        services.AddHttpContextAccessor();
        services.AddTransient<IClaimsTransformation, EntraClaimsTransformation>();
        services.AddSingleton<IAuthorizationAuditLogger, AuthorizationAuditLogger>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, ProjectAccessAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, OperationalUserAuthorizationHandler>();

        return services;
    }
}
