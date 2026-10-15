using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.MesIntegration;
using Emi.Qms.Api.BusinessUnits;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class MesProbeTests
{
    private const string Proxy = "synthetic-proxy-token-0123456789-abcd";
    private const string Ip = "203.0.113.10";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1800000000);
    private static Dictionary<string, string?> Config(RSA key) => new()
    {
        ["MesProbe:Enabled"] = "true", ["MesProbe:ProxyToken"] = Proxy,
        ["MesProbe:RequireCompanyIp"] = "true", ["MesProbe:CompanyIp"] = Ip,
        ["MesProbe:PublicKey"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
        ["MesProbe:DeviceId"] = Convert.ToHexStringLower(SHA256.HashData(key.ExportSubjectPublicKeyInfo())),
        ["DevAuthentication:Enabled"] = "true", ["DevelopmentData:SeedEnabled"] = "false",
        ["Database:ApplyMigrationsOnStartup"] = "false"
    };
    private static byte[] Body(RSA key, DateTimeOffset now) => JsonSerializer.SerializeToUtf8Bytes(
        new MesProbeRequest(Convert.ToHexStringLower(SHA256.HashData(key.ExportSubjectPublicKeyInfo())),
            Guid.NewGuid(), now.ToUnixTimeSeconds(), MesProbeProtocol.Message), MesProbeProtocol.Json);
    private static string Sign(RSA key, byte[] body) => Convert.ToBase64String(key.SignData(MesProbeProtocol.SigningBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    [Theory]
    [InlineData("valid", true)]
    [InlineData("wrong-ip", false)]
    [InlineData("forged-ip-list", false)]
    [InlineData("missing-ip", false)]
    [InlineData("bad-proxy", false)]
    [InlineData("disabled", false)]
    [InlineData("revoked-key", false)]
    [InlineData("wrong-key", false)]
    [InlineData("expired", false)]
    [InlineData("future", false)]
    [InlineData("changed-body", false)]
    [InlineData("malformed-signature", false)]
    [InlineData("invalid-ip-mode", false)]
    [InlineData("explicit-ip-off", true)]
    [InlineData("review-safe", false)]
    public void VerificationRequiresAllConditions(string scenario, bool expected)
    {
        using var key = RSA.Create(2048);
        var config = Config(key);
        var body = Body(key, scenario == "expired" ? Now.AddMinutes(-3) : scenario == "future" ? Now.AddMinutes(3) : Now);
        var signature = Sign(key, body);
        var ip = Ip;
        var proxy = Proxy;
        switch (scenario)
        {
            case "wrong-ip": ip = "203.0.113.11"; break;
            case "forged-ip-list": ip = Ip + ", 203.0.113.11"; break;
            case "missing-ip": ip = ""; break;
            case "bad-proxy": proxy = "forged"; break;
            case "disabled": config["MesProbe:Enabled"] = "false"; break;
            case "revoked-key": config["MesProbe:PublicKey"] = ""; break;
            case "wrong-key": using (var other = RSA.Create(2048)) signature = Sign(other, body); break;
            case "changed-body": body = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).Replace("PROBE-v1", "PROBE-v2")); break;
            case "malformed-signature": signature = "not-base64"; break;
            case "invalid-ip-mode": config["MesProbe:RequireCompanyIp"] = ""; break;
            case "explicit-ip-off": config["MesProbe:RequireCompanyIp"] = "false"; ip = "203.0.113.11"; break;
            case "review-safe": config["ReviewSafe:Enabled"] = "true"; break;
        }
        var actual = MesProbeProtocol.Verify(body, signature, new ConfigurationBuilder().AddInMemoryCollection(config).Build(), proxy, ip, Now);
        Assert.Equal(expected, actual is not null);
    }

    [Fact]
    public async Task ActualEndpointAuthenticatesEchoesAndRejectsReplayWithoutEmployeePrivilege()
    {
        var ct = TestContext.Current.CancellationToken;
        using var key = RSA.Create(2048);
        var receipts = new FakeReceipts();
        using var factory = QmsWebApplicationFactory.Create("Testing", Config(key), true,
            configureTestServices: s => s.AddSingleton<IMesProbeReceipts>(receipts));
        using var client = factory.CreateClient();
        var body = Body(key, DateTimeOffset.UtcNow);
        HttpRequestMessage Request(string path = MesProbeProtocol.Path)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent(body) };
            req.Content.Headers.ContentType = new("application/json");
            req.Headers.Add("X-Mes-Signature", Sign(key, body));
            req.Headers.Add("X-Mes-Proxy-Token", Proxy);
            req.Headers.Add("X-Mes-Client-IP", Ip);
            return req;
        }
        using var request = Request();
        using var response = await client.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<MesProbeReceipt>(ct);
        Assert.NotNull(receipt);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(body)), receipt.Sha256);
        Assert.Equal(MesProbeProtocol.Message, receipt.Message);
        using var replay = Request();
        using var replayResponse = await client.SendAsync(replay, ct);
        Assert.Equal(HttpStatusCode.Conflict, replayResponse.StatusCode);
        using var forged = Request(); forged.Headers.Remove("X-Mes-Proxy-Token");
        using var forgedResponse = await client.SendAsync(forged, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, forgedResponse.StatusCode);
        Assert.Equal(2, receipts.Calls);
        using var employee = new HttpRequestMessage(HttpMethod.Post, MesProbeProtocol.Path) { Content = JsonContent.Create(new { }) };
        employee.Headers.Add("X-Dev-User", "dev-admin");
        using var employeeResponse = await client.SendAsync(employee, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, employeeResponse.StatusCode);
        using var query = Request(MesProbeProtocol.Path + "?anything=1");
        using var queryResponse = await client.SendAsync(query, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, queryResponse.StatusCode);
        using var business = Request("/api/g2/nonconformances");
        using var businessResponse = await client.SendAsync(business, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, businessResponse.StatusCode);
    }

    [Fact]
    public async Task DefaultDisabledAndOversizedBodyNeverTouchReceipts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var key = RSA.Create(2048);
        var config = Config(key); config.Remove("MesProbe:Enabled");
        var receipts = new FakeReceipts();
        using var factory = QmsWebApplicationFactory.Create("Testing", config, true,
            configureTestServices: s => s.AddSingleton<IMesProbeReceipts>(receipts));
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(MesProbeProtocol.Path, new StringContent(new string('x', 3000), Encoding.UTF8, "application/json"), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, receipts.Calls);
    }
    private sealed class FakeReceipts : IMesProbeReceipts
    {
        private readonly HashSet<Guid> used = [];
        public int Calls { get; private set; }
        public Task<bool> ConsumeAsync(MesProbeVerified request, CancellationToken ct)
        { lock (used) { Calls++; return Task.FromResult(used.Add(request.Request.RequestId)); } }
    }
}

public sealed class MesProbeDatabaseTests
{
    [Fact]
    public async Task ParallelConnectionsConsumeOneReceiptAndRetainReplayAcrossConnections()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        var sql = await File.ReadAllTextAsync(Path.Combine(db.RepositoryRoot, "database/business-migrations/cheongju/0136_mes_probe_receipts.sql"), ct);
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator, sql, ct);
        var request = new MesProbeVerified(new MesProbeRequest(new string('a',64), Guid.NewGuid(), DateTimeOffset.UtcNow.ToUnixTimeSeconds(), MesProbeProtocol.Message), "synthetic");
        async Task<bool> Consume()
        {
            await using var connection = await db.OpenAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator, ct);
            return await MesProbeReceipts.ConsumeOnConnectionAsync(connection, request, ct);
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Consume()));
        Assert.Single(results, x => x);
        Assert.False(await Consume());
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator,
            "insert into mes_probe_receipts values ('" + new string('b',64) + "',gen_random_uuid(),now()-interval '1 minute')", ct);
        Assert.False(await Consume());
        await using var read = await db.OpenAsync(BusinessUnitCodes.Cheongju, BusinessUnitConnectionPurpose.Administrator, ct);
        await using var count = new Npgsql.NpgsqlCommand("select count(*) from mes_probe_receipts", read);
        Assert.Equal(1L, await count.ExecuteScalarAsync(ct));
        await using var osan = await db.OpenAsync(BusinessUnitCodes.Osan, BusinessUnitConnectionPurpose.Administrator, ct);
        await using var absent = new Npgsql.NpgsqlCommand("select to_regclass('public.mes_probe_receipts') is null", osan);
        Assert.Equal(true, await absent.ExecuteScalarAsync(ct));
    }
}
