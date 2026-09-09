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
        var body = product.Cancelled ? "<h1>제품 정보 제공이 중지되었습니다.</h1>" :
            $"<h1>제품 정보</h1><p class=number>{H(product.Number)}</p><dl><dt>제조일</dt><dd>{local:yyyy-MM-dd}</dd><dt>제조시간</dt><dd>{local:HH:mm:ss} (한국 시간)</dd><dt>작업자</dt><dd>{H(product.WorkerName)}</dd></dl>";
        if (!product.Cancelled)
        {
            foreach (var side in new[] { "front", "back" })
            {
                var photo = product.Photos.Single(p => p.Side == side);
                if (photo.ContentType is not ("image/jpeg" or "image/png") || photo.Content.Length == 0)
                    throw new InvalidOperationException("공개 사진 형식이 잘못되었습니다.");
                var label = side == "front" ? "앞면" : "뒷면";
                body += $"<figure><figcaption>{label}</figcaption><img alt=\"{label} 사진\" src=\"data:{photo.ContentType};base64,{Convert.ToBase64String(photo.Content)}\"></figure>";
            }
        }
        return Encoding.UTF8.GetBytes("<!doctype html><html lang=ko><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\"><meta name=robots content=\"noindex,nofollow,noarchive\"><meta name=referrer content=no-referrer><meta http-equiv=Content-Security-Policy content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>제품 정보</title><style>body{font:16px system-ui,sans-serif;color:#172334;background:#f3f5f8;margin:0;padding:24px}main{max-width:760px;margin:auto;background:white;padding:24px;border-radius:12px}h1{font-size:24px}.number{font-weight:700}dl{display:grid;grid-template-columns:100px 1fr;gap:12px}dt{color:#526070}dd{margin:0;overflow-wrap:anywhere}figure{margin:24px 0 0}figcaption{font-weight:600;margin-bottom:10px}img{display:block;width:100%;height:auto;border-radius:6px}@media(max-width:420px){body{padding:12px}main{padding:16px}}</style><main>" + body + "</main></html>");
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
