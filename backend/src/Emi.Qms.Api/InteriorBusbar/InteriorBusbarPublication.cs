using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api.InteriorBusbar;

public sealed record InteriorBusbarPublicPhoto(string Side, string ContentType, byte[] Content);
public sealed record InteriorBusbarPublicSnapshot(Guid ProductId, string Number, string WorkerName,
    DateTimeOffset ManufacturedAtUtc, string Token, int Revision, bool Cancelled,
    IReadOnlyList<InteriorBusbarPublicPhoto> Photos);

public sealed record InteriorBusbarPublicationOptions(bool Enabled, Uri? PublicBaseUrl, Uri? BlobEndpoint, string SasToken)
{
    public static InteriorBusbarPublicationOptions Load(IConfiguration configuration)
    {
        const string section = "InteriorBusbar:Publication:";
        var raw = configuration[section + "Enabled"];
        if (!string.IsNullOrWhiteSpace(raw) && !bool.TryParse(raw, out _))
            throw new InvalidOperationException("제품 공개 설정 Enabled 값이 잘못되었습니다.");
        var enabled = bool.TryParse(raw, out var parsed) && parsed && !ReviewSafeMode.IsEnabled(configuration);
        var publicUrl = ReadHttps(configuration[section + "PublicBaseUrl"]);
        var blob = ReadHttps(configuration[section + "BlobEndpoint"]);
        var sas = (configuration[section + "SasToken"] ?? string.Empty).TrimStart('?');
        if (enabled && (publicUrl is null || blob is null || string.IsNullOrWhiteSpace(sas)))
            throw new InvalidOperationException("제품 공개 저장소 설정이 완성되지 않았습니다.");
        if (blob is not null && !Regex.IsMatch(blob.Host, @"\A[a-z0-9]{3,24}\.blob\.core\.windows\.net\z"))
            throw new InvalidOperationException("제품 공개 저장소는 Azure Blob endpoint여야 합니다.");
        if (blob is not null && blob.AbsolutePath != "/")
            throw new InvalidOperationException("BlobEndpoint에는 저장소 기본 주소만 지정하세요.");
        if (enabled)
        {
            var account = blob!.Host.Split('.')[0];
            if (publicUrl!.AbsolutePath != "/" || !Regex.IsMatch(publicUrl.Host,
                "\\A" + Regex.Escape(account) + @"\.z[0-9]+\.web\.core\.windows\.net\z"))
                throw new InvalidOperationException("공개 주소는 같은 별도 저장소의 정적 웹사이트 기본 주소여야 합니다.");
        }
        return new(enabled, publicUrl, blob, sas);
    }

    private static Uri? ReadHttps(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("제품 공개 주소는 query 없는 HTTPS 주소여야 합니다.");
        return uri;
    }

    public string GetPublicUrl(string token)
    {
        ValidateToken(token);
        return new Uri(PublicBaseUrl ?? throw new InvalidOperationException("공개 주소가 설정되지 않았습니다."), $"p/{token}.html").AbsoluteUri;
    }
    public static void ValidateToken(string token)
    {
        if (!Regex.IsMatch(token, @"\A[0-9a-f]{64}\z")) throw new ArgumentException("잘못된 제품 공개 코드입니다.", nameof(token));
    }
}

public static class InteriorBusbarPublicPage
{
    public static byte[] Render(InteriorBusbarPublicSnapshot product)
    {
        InteriorBusbarPublicationOptions.ValidateToken(product.Token);
        static string H(string value) => WebUtility.HtmlEncode(value);
        var local = TimeZoneInfo.ConvertTime(product.ManufacturedAtUtc, TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));
        var body = product.Cancelled ? "<section class=stopped><h1>제품 정보 제공이 중지되었습니다.</h1></section>" :
            $"<p class=eyebrow>INTERIOR BUSBAR</p><div class=title-line><h1>제품 제조정보</h1></div><dl class=info><div><dt>제조일</dt><dd>{local:yyyy-MM-dd}</dd></div><div><dt>제조시간</dt><dd>{local:HH:mm:ss}<span class=timezone>한국 시간</span></dd></div><div><dt>작업자</dt><dd>{H(product.WorkerName)}</dd></div></dl><div class=section-head><h2>제품 사진</h2><span>앞면 · 뒷면</span></div><div class=photos>";
        if (!product.Cancelled)
        {
            foreach (var side in new[] { "front", "back" })
            {
                var photo = product.Photos.Single(p => p.Side == side);
                if (photo.ContentType is not ("image/jpeg" or "image/png") || photo.Content.Length == 0)
                    throw new InvalidOperationException("공개 사진 형식이 잘못되었습니다.");
                var label = side == "front" ? "앞면" : "뒷면";
                var index = side == "front" ? "01" : "02";
                body += $"<figure><figcaption><span class=index>{index}</span>{label}</figcaption><div class=photo><img alt=\"{label} 사진\" src=\"data:{photo.ContentType};base64,{Convert.ToBase64String(photo.Content)}\"></div></figure>";
            }
            body += "</div>";
        }
        const string prefix = """
<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex,nofollow,noarchive"><meta name="referrer" content="no-referrer"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'"><title>EMI · 제품 제조정보</title><style>
*{box-sizing:border-box}body{margin:0;background:#f5f6f7;color:#20252b;font-family:-apple-system,BlinkMacSystemFont,"Apple SD Gothic Neo","Malgun Gothic",sans-serif;line-height:1.5;-webkit-font-smoothing:antialiased}header{background:white;border-bottom:1px solid #e6e8eb} .header-inner{max-width:1000px;margin:auto;padding:21px 32px;display:flex;align-items:center;justify-content:space-between}.brand{font-size:27px;font-weight:900;letter-spacing:-1.6px;color:#dc2626;line-height:1}.brand span{font-size:11px;letter-spacing:0;color:#747b83;font-weight:500;border-left:1px solid #ddd;margin-left:15px;padding-left:15px;vertical-align:middle}.header-label{font-size:12px;color:#828891}main{max-width:1000px;margin:auto;padding:38px 32px 24px}.eyebrow{font-size:12px;font-weight:700;color:#bb242b;letter-spacing:1.6px;margin:0 0 9px}.title-line{display:flex;align-items:end;justify-content:space-between;gap:16px;margin-bottom:25px}h1{font-size:34px;letter-spacing:-1px;line-height:1.2;margin:0;font-variant-numeric:tabular-nums}.product-label{font-size:12px;color:#727a83;margin:0 0 8px}.sample{font-size:11px;border:1px solid #e1e4e8;background:#fff;padding:5px 9px;border-radius:5px;color:#737b83;white-space:nowrap}.info{display:grid;grid-template-columns:1.2fr 1fr 1fr;margin:0 0 35px;background:white;border:1px solid #e4e7eb;border-radius:12px;padding:23px 0}.info>div{padding:0 25px;border-right:1px solid #e9ebee}.info>div:last-child{border:0}dt{color:#7b828a;font-size:12px;margin-bottom:8px}dd{font-size:17px;font-weight:650;margin:0;overflow-wrap:anywhere}.timezone{font-weight:400;font-size:10px;color:#89919b;margin-left:5px}.section-head{display:flex;align-items:baseline;justify-content:space-between;margin:0 0 14px}h2{font-size:18px;letter-spacing:-.5px;margin:0}.section-head span{font-size:12px;color:#8a9097}.photos{display:grid;grid-template-columns:1fr 1fr;gap:18px}figure{border:1px solid #e0e4e8;margin:0;border-radius:12px;overflow:hidden;background:white}figcaption{padding:16px 18px;display:flex;align-items:center;gap:9px;font-size:14px;font-weight:700}.index{font-size:10px;font-weight:600;color:#a0a7ae;letter-spacing:1px}.photo{background:#edf0f2;border-top:1px solid #edf0f2}.photo img{display:block;width:100%;height:auto;aspect-ratio:1.6;object-fit:contain}.photo-note{padding:10px 18px;color:#9298a0;font-size:11px;border-top:1px solid #edf0f2}.notice{font-size:12px;color:#858c94;margin:18px 0 36px}footer{border-top:1px solid #e0e4e8;padding:19px 0;display:flex;justify-content:space-between;font-size:11px;color:#9298a0}footer strong{color:#626a73;font-weight:600}.design-note{font-size:11px;color:#939ba3;text-align:center;margin:15px 0 0}@media(max-width:600px){.header-inner{padding:19px 20px}.brand{font-size:25px}.brand span{margin-left:11px;padding-left:11px}.header-label{display:none}main{padding:27px 20px 15px}.eyebrow{font-size:10px;letter-spacing:1.3px}.title-line{align-items:center;margin-bottom:22px}h1{font-size:28px}.sample{font-size:10px;padding:4px 6px}.info{padding:18px 0;margin-bottom:28px}.info>div{padding:0 13px}dt{font-size:11px}dd{font-size:14px}.timezone{display:block;margin:3px 0 0;font-size:10px}.photos{grid-template-columns:1fr;gap:16px}h2{font-size:17px}figcaption{padding:13px 16px}.photo-note{padding:8px 16px}.notice{font-size:11px;margin-bottom:27px}footer{font-size:10px}}

.photos{margin-bottom:32px}.photo img{aspect-ratio:auto;object-fit:contain}.stopped{background:white;border:1px solid #e4e7eb;border-radius:12px;padding:28px;margin-bottom:32px}.stopped h1{font-size:24px}
</style></head><body><header><div class="header-inner"><div class="brand">EMI<span>이엠아이</span></div><div class="header-label">제품 정보 조회</div></div></header><main>
""";
        return Encoding.UTF8.GetBytes(prefix + body + "<footer><strong>주식회사 이엠아이</strong><span>제품 제조 정보</span></footer></main></body></html>");
    }
}

public interface IInteriorBusbarPublicationSink
{
    Task PublishAsync(string token, byte[] html, CancellationToken cancellationToken);
}

public sealed class AzureInteriorBusbarPublicationSink : IInteriorBusbarPublicationSink, IDisposable
{
    // This private client deliberately has no request-URL logger: storage SAS is a secret.
    private readonly HttpClient client;
    private readonly InteriorBusbarPublicationOptions options;
    public AzureInteriorBusbarPublicationSink(InteriorBusbarPublicationOptions options)
        : this(options, new SocketsHttpHandler { AllowAutoRedirect = false }) { }
    internal AzureInteriorBusbarPublicationSink(InteriorBusbarPublicationOptions options, HttpMessageHandler handler)
    {
        this.options = options;
        client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    }
    public async Task PublishAsync(string token, byte[] html, CancellationToken cancellationToken)
    {
        if (!options.Enabled) throw new InvalidOperationException("제품 외부 게시가 비활성화되어 있습니다.");
        InteriorBusbarPublicationOptions.ValidateToken(token);
        var address = new Uri(options.BlobEndpoint!, $"$web/p/{token}.html?{options.SasToken}");
        // Conditional writes fence even a timed-out older upload still executing at Azure.
        using var head = new HttpRequestMessage(HttpMethod.Head, address);
        head.Headers.TryAddWithoutValidation("x-ms-version", "2023-11-03");
        using var current = await client.SendAsync(head, cancellationToken);
        if (current.StatusCode != HttpStatusCode.NotFound && !current.IsSuccessStatusCode)
            throw new InvalidOperationException("제품 공개 저장소 확인에 실패했습니다.");
        using var request = new HttpRequestMessage(HttpMethod.Put, address);
        if (current.StatusCode == HttpStatusCode.NotFound) request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
        else if (current.Headers.ETag is { } etag) request.Headers.IfMatch.Add(etag);
        else throw new InvalidOperationException("제품 공개 저장소의 버전 정보를 확인할 수 없습니다.");
        request.Headers.TryAddWithoutValidation("x-ms-version", "2023-11-03");
        request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
        request.Headers.TryAddWithoutValidation("x-ms-blob-cache-control", "no-cache, no-store, must-revalidate");
        request.Content = new ByteArrayContent(html);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html") { CharSet = "utf-8" };
        request.Content.Headers.ContentMD5 = MD5.HashData(html);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("제품 페이지 게시에 실패했습니다. 공개 저장소 설정을 확인해 주세요.");
    }
    public void Dispose() => client.Dispose();
}

public sealed class InteriorBusbarPublicationWorker(
    DatabaseConnectionStringProvider connections, InteriorBusbarPublicationOptions options,
    IInteriorBusbarPublicationSink sink, ILogger<InteriorBusbarPublicationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishNextAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("제품 공개 처리 대기 중입니다. 저장소 준비 상태를 확인하세요."); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<bool> PublishNextAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled) return false;
        var target = connections.BusinessUnits.Businesses.Single(t => t.Code == BusinessUnitCodes.Cheongju);
        await using var connection = new NpgsqlConnection(connections.GetConnectionString(target));
        await connection.OpenAsync(cancellationToken);
        if (connections.BusinessUnits.Enabled && !await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, target, cancellationToken))
            throw new BusinessUnitContextUnavailableException("busbar_publication_database_identity_mismatch");
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var mutex = new NpgsqlCommand("SET LOCAL lock_timeout='5s'; SELECT pg_advisory_xact_lock(9070090)", connection, transaction))
            await mutex.ExecuteNonQueryAsync(cancellationToken);
        InteriorBusbarPublicSnapshot snapshot;
        await using (var command = new NpgsqlCommand("""
            SELECT id,number,worker_name,manufactured_at_utc,public_token,revision,status
            FROM busbar_products WHERE publication_state='Pending' AND status IN ('Complete','Cancelled')
            AND manufactured_at_utc IS NOT NULL AND revision>published_revision ORDER BY manufactured_at_utc,id LIMIT 1 FOR UPDATE
            """, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return false;
            snapshot = new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetString(4), reader.GetInt32(5), reader.GetString(6) == "Cancelled", []);
        }
        var photos = new List<InteriorBusbarPublicPhoto>();
        if (!snapshot.Cancelled)
        {
            await using var photoCommand = new NpgsqlCommand("SELECT side,content_type,content FROM busbar_photos WHERE product_id=@id", connection, transaction);
            photoCommand.Parameters.AddWithValue("id", snapshot.ProductId);
            await using var reader = await photoCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) photos.Add(new(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<byte[]>(2)));
        }
        snapshot = snapshot with { Photos = photos };
        var published = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await sink.PublishAsync(snapshot.Token, InteriorBusbarPublicPage.Render(snapshot), timeout.Token);
            published = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { /* Only a fixed safe error is persisted; exception text can contain credentials. */ }
        await using var update = new NpgsqlCommand("""
            UPDATE busbar_products SET publication_state=@state, publication_error=@error,
            published_revision=CASE WHEN @published THEN @revision ELSE published_revision END
            WHERE id=@id AND revision=@revision
            """, connection, transaction);
        update.Parameters.AddWithValue("state", published ? "Published" : "Failed");
        update.Parameters.AddWithValue("error", published ? DBNull.Value : "외부 게시 실패. 설정을 확인한 후 다시 시도해 주세요.");
        update.Parameters.AddWithValue("published", published);
        update.Parameters.AddWithValue("revision", snapshot.Revision);
        update.Parameters.AddWithValue("id", snapshot.ProductId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}

public static class InteriorBusbarPublicationRegistration
{
    public static IServiceCollection AddInteriorBusbarPublication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(InteriorBusbarPublicationOptions.Load(configuration));
        services.AddSingleton<IInteriorBusbarPublicationSink, AzureInteriorBusbarPublicationSink>();
        services.AddHostedService<InteriorBusbarPublicationWorker>();
        return services;
    }
}
