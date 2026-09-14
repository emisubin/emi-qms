using System.Net;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.InteriorBusbar;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class InteriorBusbarEcountClientTests
{
    private const string Zone = """{"Status":"200","Error":null,"Data":{"ZONE":"A","DOMAIN":".ecount.com"}}""";
    private const string Session = "synthetic-session-is-deliberately-longer-than-fifty-characters+/=?&";
    private static string Login => JsonSerializer.Serialize(new
    {
        Status = "200", Error = (object?)null,
        Data = new { Code = "00", Datas = new { COM_CODE = "SYN001", USER_ID = "synthetic-user", SESSION_ID = Session } }
    });
    private const string Success = """{"Status":"200","Error":null,"Data":{"SuccessCnt":1,"FailCnt":0,"ResultDetails":[{"IsSuccess":true,"Errors":[]}],"SlipNos":["SYN-SLIP"]}}""";
    private const string Failure = """{"Status":"200","Error":null,"Data":{"SuccessCnt":0,"FailCnt":1,"ResultDetails":[{"IsSuccess":false,"Errors":[{"ColCd":"PROD_CD","Message":"synthetic private input"}]}],"SlipNos":[]}}""";

    private static InteriorBusbarEcountOptions Options(params (string key, string? value)[] changes)
    {
        var settings = new Dictionary<string, string?>
        {
            ["InteriorBusbar:Ecount:Enabled"] = "true", ["InteriorBusbar:Ecount:Environment"] = "Test",
            ["InteriorBusbar:Ecount:CompanyCode"] = "SYN001", ["InteriorBusbar:Ecount:UserId"] = "synthetic-user",
            ["InteriorBusbar:Ecount:ApiKey"] = "synthetic-api-key", ["InteriorBusbar:Ecount:SessionIdleMinutes"] = "10"
        };
        foreach (var (key, value) in changes) settings[key.Contains(':') ? key : "InteriorBusbar:Ecount:" + key] = value;
        return InteriorBusbarEcountOptions.Load(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private static BusbarEcountAttempt Attempt(string kind = "Order") => new(Guid.NewGuid(), Guid.NewGuid(), kind,
        """{"projectId":"00000000-0000-0000-0000-000000000001","projectName":"Synthetic","workOrderNumber":"SYN-WO","purchaseOrderNumber":"","productCode":"SYN-P","customerCode":"SYN-C","warehouseCode":"SYN-W","commonProjectCode":"SYN-PJT","quantity":3,"unitPrice":12345.123,"supplyAmount":37035.369,"vatAmount":3703.5369,"totalAmount":40738.9059,"ioDate":"20260915","dueDate":"2026-10-01","currency":"KRW","vatRate":0.1}""");

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 14, 16, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record Request(HttpMethod Method, Uri Uri, string Body);
    private sealed class Handler(params string[] responses) : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public Exception? Failure { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.Method, request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            if (Failure is not null) throw Failure;
            return new(Status) { Content = new StringContent(responses[Requests.Count - 1], Encoding.UTF8, "application/json") };
        }
    }

    [Theory]
    [InlineData("Test", "sboapi")]
    [InlineData("Production", "oapi")]
    public async Task VerifiedWireContractPreservesDecimalsAndMapsOrderAndSale(string environment, string prefix)
    {
        using var handler = new Handler(Zone, Login, Success, Success);
        using var http = new HttpClient(handler);
        var client = new InteriorBusbarEcountClient(Options(("Environment", environment)), new Clock(), http);
        Assert.True(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        Assert.True(client.HasSession);
        Assert.Equal("Succeeded", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        Assert.Equal("Succeeded", (await client.SendAsync(Attempt("Sale"), TestContext.Current.CancellationToken)).State);
        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Post, request.Method));
        Assert.Equal($"https://{prefix}.ecount.com/OAPI/V2/Zone", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("""{"COM_CODE":"SYN001"}""", handler.Requests[0].Body);
        Assert.Equal($"https://{prefix}a.ecount.com/OAPI/V2/OAPILogin", handler.Requests[1].Uri.AbsoluteUri);
        using var login = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal(5, login.RootElement.EnumerateObject().Count());
        Assert.Equal("synthetic-user", login.RootElement.GetProperty("USER_ID").GetString());
        Assert.Equal("synthetic-api-key", login.RootElement.GetProperty("API_CERT_KEY").GetString());
        Assert.Equal("ko-KR", login.RootElement.GetProperty("LAN_TYPE").GetString());
        Assert.Equal("A", login.RootElement.GetProperty("ZONE").GetString());
        for (var i = 2; i < 4; i++)
        {
            var order = i == 2;
            var request = handler.Requests[i];
            Assert.Equal($"https://{prefix}a.ecount.com/OAPI/V2/{(order ? "SaleOrder/SaveSaleOrder" : "Sale/SaveSale")}?SESSION_ID={Uri.EscapeDataString(Session)}", request.Uri.AbsoluteUri);
            using var json = JsonDocument.Parse(request.Body);
            Assert.Single(json.RootElement.EnumerateObject());
            var row = Assert.Single(json.RootElement.GetProperty(order ? "SaleOrderList" : "SaleList").EnumerateArray()).GetProperty("BulkDatas");
            Assert.Equal("1", row.GetProperty("UPLOAD_SER_NO").GetString());
            Assert.Equal("20260915", row.GetProperty("IO_DATE").GetString());
            Assert.Equal("SYN-C", row.GetProperty("CUST").GetString());
            Assert.Equal("SYN-W", row.GetProperty("WH_CD").GetString());
            Assert.Equal("SYN-PJT", row.GetProperty("PJT_CD").GetString());
            Assert.Equal("SYN-P", row.GetProperty("PROD_CD").GetString());
            Assert.Equal(3m, row.GetProperty("QTY").GetDecimal());
            Assert.Equal(12345.123m, row.GetProperty("PRICE").GetDecimal());
            Assert.Equal(37035.369m, row.GetProperty("SUPPLY_AMT").GetDecimal());
            Assert.Equal(3703.5369m, row.GetProperty("VAT_AMT").GetDecimal());
            Assert.False(row.TryGetProperty("U_MEMO1", out _));
            Assert.False(row.TryGetProperty("IO_TYPE", out _));
            Assert.Equal(order ? 12 : 10, row.EnumerateObject().Count());
            if (order)
            {
                Assert.Equal("SYN-WO", row.GetProperty("U_MEMO2").GetString());
                Assert.Equal("20261001", row.GetProperty("TIME_DATE").GetString());
            }
            else
            {
                Assert.False(row.TryGetProperty("U_MEMO2", out _));
                Assert.False(row.TryGetProperty("TIME_DATE", out _));
            }
        }
    }

    [Fact]
    public async Task ExplicitIoTypeIsSentAndSessionExpiresSinceLastSuccessfulOperation()
    {
        using var handler = new Handler(Zone, Login, Success);
        using var http = new HttpClient(handler);
        var clock = new Clock();
        var client = new InteriorBusbarEcountClient(Options(("IoType", "10")), clock, http);
        Assert.True(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        clock.Now = clock.Now.AddMinutes(9);
        Assert.Equal("Succeeded", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        using var json = JsonDocument.Parse(handler.Requests[2].Body);
        Assert.Equal("10", json.RootElement.GetProperty("SaleOrderList")[0].GetProperty("BulkDatas").GetProperty("IO_TYPE").GetString());
        clock.Now = clock.Now.AddMinutes(9);
        Assert.True(client.HasSession);
        clock.Now = clock.Now.AddMinutes(1);
        Assert.False(client.HasSession);
        Assert.Equal("Unknown", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledOrReviewSafeCannotMakeAnyHttpRequest(bool reviewSafe)
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler);
        var options = reviewSafe ? Options(("ReviewSafe:Enabled", "true")) : Options(("Enabled", "false"));
        var client = new InteriorBusbarEcountClient(options, new Clock(), http);
        Assert.False(options.Enabled);
        Assert.False(client.HasSession);
        Assert.False(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Unknown", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void DefaultIsDisabledAndOptionsDoNotPrintSecrets()
    {
        Assert.False(InteriorBusbarEcountOptions.Load(new ConfigurationBuilder().Build()).Enabled);
        Assert.DoesNotContain("synthetic-api-key", Options().ToString());
        Assert.Equal(230, Options(("SessionIdleMinutes", "230")).SessionIdleMinutes);
        Assert.Equal(1440, Options(("SessionIdleMinutes", "1440")).SessionIdleMinutes);
        Assert.Throws<InvalidOperationException>(() => Options(("UserId", new string('u', 31))));
        Assert.Throws<InvalidOperationException>(() => Options(("ApiKey", new string('k', 51))));
    }

    [Theory]
    [InlineData("Enabled", "")]
    [InlineData("Enabled", "yes")]
    [InlineData("Environment", null)]
    [InlineData("Environment", "Sandbox")]
    [InlineData("CompanyCode", "")]
    [InlineData("CompanyCode", "1234567")]
    [InlineData("UserId", "")]
    [InlineData("ApiKey", "")]
    [InlineData("SessionIdleMinutes", null)]
    [InlineData("SessionIdleMinutes", "0")]
    [InlineData("SessionIdleMinutes", "1441")]
    [InlineData("SessionIdleMinutes", "invalid")]
    [InlineData("IoType", "1")]
    public void InvalidConfigurationFailsClosedWithoutEchoingValues(string key, string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Options((key, value)));
        Assert.Contains("InteriorBusbar:Ecount:" + key, error.Message);
        Assert.DoesNotContain("synthetic-api-key", error.Message);
    }

    [Theory]
    [InlineData("A.evil", ".ecount.com")]
    [InlineData("A", ".ecount.com.evil")]
    [InlineData("A", "https://ecount.com")]
    [InlineData("A", ".ECOUNT.COM")]
    [InlineData("1", ".ecount.com")]
    [InlineData("", ".ecount.com")]
    [InlineData("ABC", ".ecount.com")]
    public async Task UntrustedZoneCannotChooseHost(string zone, string domain)
    {
        using var handler = new Handler(JsonSerializer.Serialize(new { Status = "200", Error = (object?)null, Data = new { ZONE = zone, DOMAIN = domain } }));
        using var http = new HttpClient(handler);
        var client = new InteriorBusbarEcountClient(Options(), new Clock(), http);
        Assert.False(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Unknown", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("Code", "99")]
    [InlineData("COM_CODE", "OTHER")]
    [InlineData("USER_ID", "other-user")]
    [InlineData("SESSION_ID", "")]
    public async Task RejectedLoginNeverSendsOrRetries(string key, string value)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(Login)!;
        if (key == "Code") json["Data"]![key] = value;
        else json["Data"]!["Datas"]![key] = value;
        using var handler = new Handler(Zone, json.ToJsonString());
        using var http = new HttpClient(handler);
        var client = new InteriorBusbarEcountClient(Options(), new Clock(), http);
        Assert.False(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        Assert.False(client.HasSession);
        Assert.Equal("Unknown", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        Assert.Equal(2, handler.Requests.Count);
    }

    public static IEnumerable<object[]> SaveResponses()
    {
        yield return [Success, "Succeeded"];
        yield return [Failure, "Failed"];
        yield return [Failure.Replace("\"SlipNos\":[]", "\"SlipNos\":null"), "Failed"];
        yield return ["not json", "Unknown"];
        yield return ["{}", "Unknown"];
        yield return [Success.Replace("\"Status\":\"200\"", "\"Status\":\"500\""), "Unknown"];
        yield return [Success.Replace("\"Error\":null", "\"Error\":{\"Message\":\"synthetic-private-data\"}"), "Unknown"];
        yield return [Success.Replace("\"FailCnt\":0", "\"FailCnt\":1"), "Unknown"];
        yield return [Success.Replace("\"SuccessCnt\":1", "\"SuccessCnt\":0"), "Unknown"];
        yield return [Success.Replace("\"Errors\":[]", "\"Errors\":[{}]"), "Unknown"];
        yield return [Success.Replace("\"Errors\":[]", "\"Errors\":null"), "Unknown"];
        yield return [Success.Replace("\"Errors\":[]", "\"Other\":[]"), "Unknown"];
        yield return [Success.Replace("\"IsSuccess\":true", "\"IsSuccess\":false"), "Unknown"];
        yield return [Success.Replace("\"SlipNos\":[\"SYN-SLIP\"]", "\"SlipNos\":[]"), "Unknown"];
        yield return [Success.Replace("\"SlipNos\":[\"SYN-SLIP\"]", "\"SlipNos\":[\"A\",\"B\"]"), "Unknown"];
        yield return [Success.Replace("\"SYN-SLIP\"", "\"\""), "Unknown"];
        yield return [Success.Replace("SYN-SLIP", new string('S', 201)), "Unknown"];
        yield return [Success.Replace("[{\"IsSuccess\":true,\"Errors\":[]}]", "[]"), "Unknown"];
        yield return [Failure.Replace("\"SlipNos\":[]", "\"SlipNos\":[\"SYN-SLIP\"]"), "Unknown"];
        yield return [Failure.Replace("[{\"ColCd\":\"PROD_CD\",\"Message\":\"synthetic private input\"}]", "[]"), "Unknown"];
        yield return [Failure.Replace("[{\"ColCd\":\"PROD_CD\",\"Message\":\"synthetic private input\"}]", "[null]"), "Unknown"];
        yield return [Failure.Replace("[{\"ColCd\":\"PROD_CD\",\"Message\":\"synthetic private input\"}]", "[{}]"), "Unknown"];
        yield return [Failure.Replace("[{\"ColCd\":\"PROD_CD\",\"Message\":\"synthetic private input\"}]", "[\"oops\"]"), "Unknown"];
        yield return [Failure.Replace("\"ColCd\":\"PROD_CD\"", "\"ColCd\":\"\""), "Unknown"];
        yield return [Failure.Replace("\"Message\":\"synthetic private input\"", "\"Message\":\"\""), "Unknown"];
    }

    [Theory]
    [MemberData(nameof(SaveResponses))]
    public async Task SaveOutcomeRequiresCompleteConsistentOneRowEvidence(string response, string expected)
    {
        using var handler = new Handler(Zone, Login, response);
        using var http = new HttpClient(handler);
        var client = new InteriorBusbarEcountClient(Options(), new Clock(), http);
        Assert.True(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        var result = await client.SendAsync(Attempt(), TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.State);
        Assert.Equal(expected == "Succeeded" ? "SYN-SLIP" : null, result.SlipNumber);
        Assert.DoesNotContain("synthetic private input", result.ToString());
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task HttpErrorsAreUnknownEvenWithSuccessfulBody(int status)
    {
        using var handler = new Handler(Zone, Login, Success);
        using var http = new HttpClient(handler);
        var client = new InteriorBusbarEcountClient(Options(), new Clock(), http);
        Assert.True(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        handler.Status = (HttpStatusCode)status;
        Assert.Equal("Unknown", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostResponseOrCancellationIsUnknownWithoutRetry(bool cancelled)
    {
        using var handler = new Handler(Zone, Login);
        using var http = new HttpClient(handler);
        var client = new InteriorBusbarEcountClient(Options(), new Clock(), http);
        Assert.True(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        handler.Failure = cancelled ? new OperationCanceledException("synthetic secret") : new HttpRequestException("synthetic secret");
        Assert.Equal("Unknown", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        Assert.Equal(3, handler.Requests.Count);
        Assert.False(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        Assert.False(client.HasSession);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("20")]
    public async Task FrozenDateAndIoTypeOverrideCurrentClockAndConfiguration(string? ioType)
    {
        using var handler = new Handler(Zone, Login, Success);
        using var http = new HttpClient(handler);
        var client = new InteriorBusbarEcountClient(Options(("IoType", "10")), new Clock(), http);
        Assert.True(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        var attempt = Attempt();
        var frozen = System.Text.Json.Nodes.JsonNode.Parse(attempt.Payload)!;
        frozen["ioDate"] = "20260101";
        frozen["ioType"] = ioType;
        Assert.Equal("Succeeded", (await client.SendAsync(attempt with { Payload = frozen.ToJsonString() }, TestContext.Current.CancellationToken)).State);
        using var json = JsonDocument.Parse(handler.Requests[2].Body);
        var row = json.RootElement.GetProperty("SaleOrderList")[0].GetProperty("BulkDatas");
        Assert.Equal("20260101", row.GetProperty("IO_DATE").GetString());
        if (ioType is null) Assert.False(row.TryGetProperty("IO_TYPE", out _));
        else Assert.Equal(ioType, row.GetProperty("IO_TYPE").GetString());
    }

    [Fact]
    public async Task OversizedResponseIsUnknown()
    {
        using var handler = new Handler(Zone, Login, new string(' ', 65537) + Success);
        using var http = new HttpClient(handler);
        var client = new InteriorBusbarEcountClient(Options(), new Clock(), http);
        Assert.True(await client.AuthenticateAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Unknown", (await client.SendAsync(Attempt(), TestContext.Current.CancellationToken)).State);
        Assert.Equal(3, handler.Requests.Count);
    }
}
