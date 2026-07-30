using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Emi.Qms.Api.Security;

public sealed class RequestRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;
    public int ReadRequestsPerMinute { get; set; } = 3000;
    public int MutationRequestsPerMinute { get; set; } = 600;
    public int UploadRequestsPerMinute { get; set; } = 60;
    public int HealthRequestsPerMinute { get; set; } = 600;
}

public static class RateLimitingConfiguration
{
    public static IServiceCollection AddQmsRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var limits = configuration
            .GetSection(RequestRateLimitOptions.SectionName)
            .Get<RequestRateLimitOptions>()
            ?? new RequestRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var (category, permitLimit) = SelectLimit(context, limits);
                var identity = context.User.FindFirstValue("oid")
                    ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"{category}:{identity}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, permitLimit),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                            .ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Emi.Qms.Api.Security.RateLimit")
                    .LogWarning(
                        "Request rate limit exceeded for {Method} {Path}.",
                        context.HttpContext.Request.Method,
                        context.HttpContext.Request.Path);

                await Results.Problem(
                        title: "요청이 너무 많습니다.",
                        detail: "잠시 후 다시 시도해 주세요.",
                        statusCode: StatusCodes.Status429TooManyRequests)
                    .ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }

    private static (string Category, int PermitLimit) SelectLimit(
        HttpContext context,
        RequestRateLimitOptions limits)
    {
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            return ("health", limits.HealthRequestsPerMinute);
        }

        if (context.Request.HasFormContentType
            && context.Request.ContentType?.StartsWith(
                "multipart/form-data",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return ("upload", limits.UploadRequestsPerMinute);
        }

        if (!HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method))
        {
            return ("mutation", limits.MutationRequestsPerMinute);
        }

        return ("read", limits.ReadRequestsPerMinute);
    }
}
