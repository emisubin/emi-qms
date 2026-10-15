using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.DeploymentMaintenance;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Emi.Qms.Api.MesIntegration;

public sealed record MesProbeRequest(string DeviceId, Guid RequestId, long SentAt, string Message);
public sealed record MesProbeReceipt(Guid RequestId, string Message, string Sha256, DateTimeOffset ReceivedAt);
public sealed record MesProbeVerified(MesProbeRequest Request, string Sha256);

public static class MesProbeProtocol
{
    public const string Scheme = "MesDeviceSignature";
    public const string Path = "/integrations/mes/probe";
    public const string Message = "EMI-MES-PROBE-v1";
    public const int MaxBody = 2048;
    public static readonly byte[] Prefix = Encoding.UTF8.GetBytes("EMI-MES-PROBE-v1\nPOST\n" + Path + "\n");
    public static byte[] SigningBytes(byte[] body) => [.. Prefix, .. body];
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, PropertyNameCaseInsensitive = false };

    public static MesProbeVerified? Verify(byte[] body, string signature, IConfiguration config,
        string proxyToken, string clientIp, DateTimeOffset now)
    {
        try
        {
            if (config["MesProbe:Enabled"] != "true" || ReviewSafeMode.IsEnabled(config)) return null;
            var secret = config["MesProbe:ProxyToken"] ?? "";
            if (secret.Length < 32 || proxyToken.Length != secret.Length
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(proxyToken))) return null;
            var ipMode = config["MesProbe:RequireCompanyIp"];
            if (ipMode is not ("true" or "false")) return null;
            if (ipMode == "true" && (!IPAddress.TryParse(clientIp, out var remote)
                || !IPAddress.TryParse(config["MesProbe:CompanyIp"], out var allowed)
                || !remote.Equals(allowed))) return null;
            if (body.Length is < 1 or > MaxBody || signature.Length > 512) return null;
            var request = JsonSerializer.Deserialize<MesProbeRequest>(body, Json);
            if (request is null || request.DeviceId != config["MesProbe:DeviceId"]
                || request.RequestId == Guid.Empty || request.Message != Message
                || request.SentAt < now.ToUnixTimeSeconds() - 120 || request.SentAt > now.ToUnixTimeSeconds() + 120) return null;
            var publicKey = Convert.FromBase64String(config["MesProbe:PublicKey"] ?? "");
            if (request.DeviceId != Convert.ToHexStringLower(SHA256.HashData(publicKey))) return null;
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out var consumed);
            if (consumed != publicKey.Length || rsa.KeySize != 2048) return null;
            if (!rsa.VerifyData(SigningBytes(body), Convert.FromBase64String(signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return null;
            return new MesProbeVerified(request, Convert.ToHexStringLower(SHA256.HashData(body)));
        }
        catch (Exception e) when (e is CryptographicException or FormatException or JsonException or ArgumentException) { return null; }
    }
}

public sealed class MesProbeAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration config, TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Path != MesProbeProtocol.Path || !HttpMethods.IsPost(Request.Method)
            || Request.QueryString.HasValue || Request.ContentLength > MesProbeProtocol.MaxBody
            || !Request.HasJsonContentType() || Request.Headers["Content-Encoding"].Count > 0)
            return AuthenticateResult.Fail("mes_probe_denied");
        static string Single(HttpRequest req, string name) => req.Headers[name].Count == 1 ? req.Headers[name].ToString() : "";
        var buffer = new byte[MesProbeProtocol.MaxBody + 1];
        var size = 0;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            while (size < buffer.Length)
            {
                var read = await Request.Body.ReadAsync(buffer.AsMemory(size), timeout.Token);
                if (read == 0) break;
                size += read;
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException) { return AuthenticateResult.Fail("mes_probe_denied"); }
        var verified = MesProbeProtocol.Verify(buffer[..size], Single(Request, "X-Mes-Signature"), config,
            Single(Request, "X-Mes-Proxy-Token"), Single(Request, "X-Mes-Client-IP"), time.GetUtcNow());
        if (verified is null) return AuthenticateResult.Fail("mes_probe_denied");
        Context.Features.Set(verified);
        var identity = new ClaimsIdentity(new[] { new Claim("mes_device", verified.Request.DeviceId) }, MesProbeProtocol.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), MesProbeProtocol.Scheme));
    }
}

public interface IMesProbeReceipts
{
    Task<bool> ConsumeAsync(MesProbeVerified request, CancellationToken ct);
}

public sealed class MesProbeReceipts(DatabaseConnectionStringProvider provider) : IMesProbeReceipts
{
    public async Task<bool> ConsumeAsync(MesProbeVerified verified, CancellationToken ct)
    {
        // A dedicated machine endpoint always targets Cheongju; never uses a client campus header.
        if (!provider.BusinessUnits.Enabled) throw new InvalidOperationException("mes_probe_business_configuration_required");
        var target = provider.BusinessUnits.GetBusiness(BusinessUnitCodes.Cheongju);
        await using var lease = await DeploymentMaintenanceLease.AcquireAsync(provider, [target], ct);
        if (lease is null) throw new InvalidOperationException("mes_probe_maintenance");
        await using var source = provider.RentDataSource(provider.GetConnectionString(target));
        await using var connection = await source.OpenConnectionAsync(ct);
        return await ConsumeOnConnectionAsync(connection, verified, ct);
    }

    public static async Task<bool> ConsumeOnConnectionAsync(NpgsqlConnection connection, MesProbeVerified verified, CancellationToken ct)
    {
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var prune = new NpgsqlCommand("delete from mes_probe_receipts where expires_at < now()", connection, tx);
        await prune.ExecuteNonQueryAsync(ct);
        await using var insert = new NpgsqlCommand("""
            insert into mes_probe_receipts(device_id, request_id, expires_at)
            values (@device, @request, greatest(now(), to_timestamp(@sent)) + interval '10 minutes')
            on conflict (device_id, request_id) do nothing returning request_id
            """, connection, tx);
        insert.Parameters.AddWithValue("device", verified.Request.DeviceId);
        insert.Parameters.AddWithValue("request", verified.Request.RequestId);
        insert.Parameters.AddWithValue("sent", (double)verified.Request.SentAt);
        var inserted = await insert.ExecuteScalarAsync(ct) is Guid;
        await tx.CommitAsync(ct);
        return inserted;
    }
}

public static class MesProbeEndpoints
{
    public static IServiceCollection AddMesProbe(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, MesProbeAuthenticationHandler>(MesProbeProtocol.Scheme, _ => { });
        services.AddScoped<IMesProbeReceipts, MesProbeReceipts>();
        return services;
    }
    public static IEndpointRouteBuilder MapMesProbe(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(MesProbeProtocol.Path, async (HttpContext context, IMesProbeReceipts receipts, TimeProvider time) =>
        {
            var verified = context.Features.Get<MesProbeVerified>();
            if (verified is null) return Results.Unauthorized();
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                if (!await receipts.ConsumeAsync(verified, context.RequestAborted))
                    return Results.Conflict(new { code = "mes_probe_replayed" });
                return Results.Ok(new MesProbeReceipt(verified.Request.RequestId, verified.Request.Message, verified.Sha256, time.GetUtcNow()));
            }
            catch (Exception e) when (e is NpgsqlException or InvalidOperationException)
            { return Results.Json(new { code = "mes_probe_unavailable" }, statusCode: 503); }
        }).RequireAuthorization(policy => policy.AddAuthenticationSchemes(MesProbeProtocol.Scheme)
            .RequireAuthenticatedUser().RequireClaim("mes_device"));
        return endpoints;
    }
}
