using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.ReviewSafe;

namespace Emi.Qms.Api.InteriorBusbar;

// Deliberately not a record: diagnostic ToString must not print credentials.
public sealed class InteriorBusbarEcountOptions
{
    public bool Enabled { get; private init; }
    public string Environment { get; private init; } = "";
    public string CompanyCode { get; private init; } = "";
    public string UserId { get; private init; } = "";
    public string ApiKey { get; private init; } = "";
    public int SessionIdleMinutes { get; private init; }
    public string? IoType { get; private init; }

    public static InteriorBusbarEcountOptions Load(IConfiguration configuration)
    {
        const string prefix = "InteriorBusbar:Ecount:";
        var raw = configuration[prefix + "Enabled"];
        if (raw is not null && !bool.TryParse(raw, out _)) Invalid("Enabled");
        var enabled = bool.TryParse(raw, out var requested) && requested && !ReviewSafeMode.IsEnabled(configuration);
        var environment = configuration[prefix + "Environment"] ?? "";
        if ((enabled || environment.Length > 0) && environment is not ("Test" or "Production")) Invalid("Environment");
        string Read(string name, int max)
        {
            var value = configuration[prefix + name] ?? "";
            if ((enabled && string.IsNullOrWhiteSpace(value)) || value.Length > max || value.Any(char.IsControl)) Invalid(name);
            return value;
        }
        var idleRaw = configuration[prefix + "SessionIdleMinutes"];
        var idle = 0;
        if (idleRaw is not null && (!int.TryParse(idleRaw, NumberStyles.None, CultureInfo.InvariantCulture, out idle) || idle is < 1 or > 1440))
            Invalid("SessionIdleMinutes");
        if (enabled && idle == 0) Invalid("SessionIdleMinutes");
        var ioType = configuration[prefix + "IoType"]?.Trim();
        if (string.IsNullOrEmpty(ioType)) ioType = null;
        if (ioType is not null && (ioType.Length != 2 || ioType.Any(char.IsControl))) Invalid("IoType");
        return new()
        {
            Enabled = enabled, Environment = environment, CompanyCode = Read("CompanyCode", 6),
            UserId = Read("UserId", 30), ApiKey = Read("ApiKey", 50), SessionIdleMinutes = idle, IoType = ioType
        };
    }

    private static void Invalid(string name) => throw new InvalidOperationException($"InteriorBusbar:Ecount:{name} 설정을 확인하세요.");
}

internal interface IInteriorBusbarEcountClient
{
    bool HasSession { get; }
    Task<bool> AuthenticateAsync(CancellationToken cancellationToken);
    Task<BusbarEcountResult> SendAsync(BusbarEcountAttempt attempt, CancellationToken cancellationToken);
}

// The worker owns serialization, persistent rate limits and failure pauses. This adapter
// never retries, logs raw provider data, or authenticates as a side effect of sending.
internal sealed class InteriorBusbarEcountClient(InteriorBusbarEcountOptions options, TimeProvider timeProvider, HttpClient client, ILogger<InteriorBusbarEcountClient>? logger = null)
    : IInteriorBusbarEcountClient
{
    private string? session;
    private string? host;
    private DateTimeOffset lastSuccess;
    public bool HasSession => options.Enabled && session is not null && host is not null
        && timeProvider.GetUtcNow() < lastSuccess.AddMinutes(options.SessionIdleMinutes);

    internal string AuthenticationStage { get; private set; } = "NotStarted";

    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
    {
        session = null;
        host = null;
        if (!options.Enabled) return false;
        AuthenticationStage = "ZoneRequest";
        try
        {
            var prefix = options.Environment == "Test" ? "sboapi" : "oapi";
            using var zoneResponse = await PostAsync($"https://{prefix}.ecount.com/OAPI/V2/Zone", new { COM_CODE = options.CompanyCode }, cancellationToken);
            AuthenticationStage = "ZoneEnvelope";
            var zoneData = Envelope(zoneResponse.RootElement);
            AuthenticationStage = "ZoneHost";
            var zone = zoneData.GetProperty("ZONE").GetString();
            if (zone is null || zone.Length is < 1 or > 2 || zone.Any(c => !char.IsAsciiLetter(c))
                || zoneData.GetProperty("DOMAIN").GetString() != ".ecount.com") return false;
            var resolvedHost = $"{prefix}{zone.ToLowerInvariant()}.ecount.com";
            AuthenticationStage = "LoginRequest";
            using var loginResponse = await PostAsync($"https://{resolvedHost}/OAPI/V2/OAPILogin", new
            {
                COM_CODE = options.CompanyCode, USER_ID = options.UserId, API_CERT_KEY = options.ApiKey,
                LAN_TYPE = "ko-KR", ZONE = zone
            }, cancellationToken);
            AuthenticationStage = "LoginEnvelope";
            if (loginResponse.RootElement.TryGetProperty("Error", out var loginError)
                && loginError.ValueKind == JsonValueKind.Object
                && loginError.TryGetProperty("Code", out var errorCode))
            {
                var codeText = errorCode.ValueKind == JsonValueKind.Number ? errorCode.GetRawText()
                    : errorCode.ValueKind == JsonValueKind.String ? errorCode.GetString() : null;
                AuthenticationStage = codeText switch
                {
                    "201" => "LoginInvalidKey", "204" => "LoginKeyEnvironment", "205" => "LoginIpNotAllowed",
                    "20" or "99" => "LoginAccountRejected", "21" or "24" or "25" => "LoginAccessBlocked",
                    "22" or "23" => "LoginTimeRestricted", "98" => "LoginAccountLocked",
                    _ => "LoginProviderError"
                };
                return false;
            }
            var login = Envelope(loginResponse.RootElement);
            AuthenticationStage = "LoginCode";
            // Report only shapes of documented fields, never their values.
            logger?.LogWarning("Ecount login response shape: Code={CodeType}, Datas={DataType}, FlatCompany={FlatCompany}, FlatUser={FlatUser}, FlatSession={FlatSession}",
                login.TryGetProperty("Code", out var loginCode) ? loginCode.ValueKind.ToString() : "Missing",
                login.TryGetProperty("Datas", out var loginData) ? loginData.ValueKind.ToString() : "Missing",
                login.TryGetProperty("COM_CODE", out _), login.TryGetProperty("USER_ID", out _), login.TryGetProperty("SESSION_ID", out _));
            // The manual's Result table puts identity/session directly under Data;
            // its legacy example wraps them in Datas with Code=00. Both still need
            // the successful envelope and exact company/user/session proof below.
            var nested = login.TryGetProperty("Datas", out var data);
            if (nested || login.TryGetProperty("Code", out _))
            {
                if (!login.TryGetProperty("Code", out var resultCode)
                    || resultCode.ValueKind != JsonValueKind.String || resultCode.GetString() != "00")
                {
                    // A short protocol code is diagnostic, never log the response Message.
                    var code = resultCode.ValueKind == JsonValueKind.String ? resultCode.GetString() : null;
                    var safeCode = code is { Length: > 0 and <= 10 }
                        && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? code : "Unrecognized";
                    logger?.LogWarning("Ecount login rejected with protocol code {Code}", safeCode);
                    return false;
                }
            }
            AuthenticationStage = "LoginData";
            if (!nested) data = login;
            AuthenticationStage = "LoginToken";
            var token = data.GetProperty("SESSION_ID").GetString();
            AuthenticationStage = "LoginCompany";
            if (data.GetProperty("COM_CODE").GetString() != options.CompanyCode) return false;
            AuthenticationStage = "LoginUser";
            if (data.GetProperty("USER_ID").GetString() != options.UserId) return false;
            AuthenticationStage = "LoginSession";
            if (string.IsNullOrWhiteSpace(token) || token.Length > 1024 || token.Any(char.IsControl)) return false;
            host = resolvedHost;
            session = token;
            lastSuccess = timeProvider.GetUtcNow();
            AuthenticationStage = "Succeeded";
            return true;
        }
        catch (Exception)
        {
            // Provider exceptions may include credentials, URL query strings, or response data.
            return false;
        }
        finally
        {
            // Only local constant stage names: never log provider text, exceptions or credentials.
            if (AuthenticationStage != "Succeeded")
                logger?.LogWarning("Ecount authentication stopped at {Stage}", AuthenticationStage);
        }
    }

    public async Task<BusbarEcountResult> SendAsync(BusbarEcountAttempt attempt, CancellationToken cancellationToken)
    {
        if (!HasSession) return new("Unknown");
        try
        {
            using var payload = JsonDocument.Parse(attempt.Payload);
            var p = payload.RootElement;
            if (attempt.Kind is not ("Order" or "Sale")) return new("Unknown");
            var ioDate = DateOnly.ParseExact(p.GetProperty("ioDate").GetString()!, "yyyyMMdd", CultureInfo.InvariantCulture);
            var ioType = p.TryGetProperty("ioType", out var frozenIoType) ? frozenIoType.GetString() : options.IoType;
            if (ioType is not null && (ioType.Length != 2 || ioType.Any(char.IsControl))) return new("Unknown");
            var row = new Dictionary<string, object?>
            {
                ["EMP_CD"] = p.GetProperty("employeeCode").GetString(),
                ["UPLOAD_SER_NO"] = "1",
                ["IO_DATE"] = ioDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                ["CUST"] = p.GetProperty("customerCode").GetString(), ["WH_CD"] = p.GetProperty("warehouseCode").GetString(),
                ["PJT_CD"] = p.GetProperty("commonProjectCode").GetString(), ["PROD_CD"] = p.GetProperty("productCode").GetString(),
                ["QTY"] = p.GetProperty("quantity").GetDecimal(), ["PRICE"] = p.GetProperty("unitPrice").GetDecimal(),
                ["SUPPLY_AMT"] = p.GetProperty("supplyAmount").GetDecimal(), ["VAT_AMT"] = p.GetProperty("vatAmount").GetDecimal()
            };
            if (ioType is not null) row["IO_TYPE"] = ioType;
            var order = attempt.Kind == "Order";
            if (order)
            {
                row["U_MEMO2"] = p.GetProperty("workOrderNumber").GetString();
                row["TIME_DATE"] = DateOnly.ParseExact(p.GetProperty("dueDate").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }
            var body = new Dictionary<string, object> { [order ? "SaleOrderList" : "SaleList"] = new[] { new { BulkDatas = row } } };
            var path = order ? "SaleOrder/SaveSaleOrder" : "Sale/SaveSale";
            using var response = await PostAsync($"https://{host}/OAPI/V2/{path}?SESSION_ID={Uri.EscapeDataString(session!)}", body, cancellationToken);
            LogSaveShape(response.RootElement);
            var result = ParseSave(response.RootElement);
            if (result.State == "Succeeded") lastSuccess = timeProvider.GetUtcNow();
            return result;
        }
        catch (Exception)
        {
            // A lost or malformed response does not prove that the ERP rejected the row.
            return new("Unknown");
        }
    }

    private void LogSaveShape(JsonElement root)
    {
        // Only fixed field names, JSON types, bounded counts and booleans. Never response text.
        static JsonElement Field(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : default;
        static string Shape(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Array => $"Array({Math.Min(e.GetArrayLength(), 100)})",
            JsonValueKind.Number when e.TryGetInt32(out var n) && n is >= 0 and <= 100 => $"Number({n})",
            _ => e.ValueKind.ToString()
        };
        var data = Field(root, "Data");
        var details = Field(data, "ResultDetails");
        var detail = details.ValueKind == JsonValueKind.Array && details.GetArrayLength() > 0 ? details[0] : default;
        logger?.LogInformation("Ecount save schema Status={Status} Data={Data} Success={Success} Failure={Failure} Details={Details} IsSuccess={IsSuccess} Errors={Errors} Slips={Slips}",
            Shape(Field(root,"Status")), Shape(data), Shape(Field(data,"SuccessCnt")), Shape(Field(data,"FailCnt")),
            Shape(details), Shape(Field(detail,"IsSuccess")), Shape(Field(detail,"Errors")), Shape(Field(data,"SlipNos")));
    }

    private static JsonElement Envelope(JsonElement root)
    {
        var status = root.GetProperty("Status");
        var succeeded = status.ValueKind == JsonValueKind.Number
            ? status.TryGetInt32(out var code) && code == 200
            : status.ValueKind == JsonValueKind.String && status.GetString() == "200";
        if (!succeeded || (root.TryGetProperty("Error", out var error) && error.ValueKind != JsonValueKind.Null))
            throw new InvalidOperationException("이카운트 응답 확인 필요");
        return root.GetProperty("Data");
    }

    private static BusbarEcountResult ParseSave(JsonElement root)
    {
        var data = Envelope(root);
        var success = data.GetProperty("SuccessCnt").GetInt32();
        var failure = data.GetProperty("FailCnt").GetInt32();
        var details = data.GetProperty("ResultDetails");
        if (details.ValueKind != JsonValueKind.Array || details.GetArrayLength() != 1) return new("Unknown");
        var detail = details[0];
        var ok = detail.GetProperty("IsSuccess").GetBoolean();
        var errors = detail.GetProperty("Errors");
        if (errors.ValueKind != JsonValueKind.Array) return new("Unknown");
        var slips = data.GetProperty("SlipNos");
        if (success == 1 && failure == 0 && ok && errors.GetArrayLength() == 0
            && slips.ValueKind == JsonValueKind.Array && slips.GetArrayLength() == 1)
        {
            var slip = slips[0].GetString();
            if (!string.IsNullOrWhiteSpace(slip) && slip.Length <= 200 && !slip.Any(char.IsControl)) return new("Succeeded", slip);
        }
        if (success == 0 && failure == 1 && !ok && errors.GetArrayLength() > 0 && errors.EnumerateArray().All(IsValidationError)
            && (slips.ValueKind == JsonValueKind.Null || (slips.ValueKind == JsonValueKind.Array && slips.GetArrayLength() == 0)))
            return new("Failed");
        return new("Unknown");
    }

    private static bool IsValidationError(JsonElement error) => error.ValueKind == JsonValueKind.Object
        && error.TryGetProperty("ColCd", out var column) && column.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(column.GetString())
        && error.TryGetProperty("Message", out var message) && message.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(message.GetString());

    private async Task<JsonDocument> PostAsync(string url, object body, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        cancellationToken = timeout.Token;
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Default), Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("이카운트 응답 확인 필요");
        // Bound untrusted responses without keeping raw response bodies in persistent storage.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > 65536) throw new InvalidOperationException("이카운트 응답 확인 필요");
            buffer.Write(chunk, 0, count);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }
}
