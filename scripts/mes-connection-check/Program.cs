using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Npgsql;

Console.OutputEncoding = Encoding.UTF8;
if (args.SequenceEqual(new[] { "--self-test" })) return await Checks.SelfTest();
if (args.Length != 0 || Console.IsInputRedirected)
{
    Console.WriteLine("직접 실행해 주세요. 비밀번호를 명령줄이나 파일로 전달하지 않습니다.");
    return 2;
}
var results = new List<string>();
void Result(string text) { results.Add(text); Console.WriteLine(text); }
var exitCode = 1;
try
{
    Console.WriteLine("EMI MES 연결 확인 · 1회 검사\nMES 업무 데이터 조회·수정 및 PMS 데이터 전송은 하지 않습니다.");
    var configPath = Path.Combine(AppContext.BaseDirectory, "connection-settings.json");
    var config = File.Exists(configPath)
        ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(configPath)) ?? throw new InvalidDataException()
        : new Settings();
    Checks.Validate(config);
    Console.WriteLine($"MES: {config.Host}:{config.Port} / {config.Database} / {config.Username}");
    Console.WriteLine($"PMS: {config.PmsOrigin}\n설정이 다르면 종료하고 connection-settings.json을 수정해 주세요.");
    Console.Write("이 주소로 연결을 검사하려면 Enter, 취소하려면 Q: ");
    if (!string.IsNullOrEmpty(Console.ReadLine())) return 2;

    var mesOk = false;
    using (var tcp = new TcpClient())
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await tcp.ConnectAsync(config.Host, config.Port, timeout.Token);
            Result("MES 네트워크: 성공 (DB 로그인은 아직 확인 전)");
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            Result("MES 네트워크: 실패 · 회사망 연결, 주소, 포트를 확인해 주세요.");
            goto PmsCheck;
        }
    }
    Console.WriteLine("MES 암호화: 1=TLS 필수(기본), 2=인증서까지 검증, 3=서버 지원 시 TLS");
    Console.WriteLine("3번은 TLS 미지원 시 암호화 없이 접속합니다. 기존 DBeaver 설정을 확인한 경우에만 선택하세요.");
    Console.Write("선택 [1]: ");
    var mode = Console.ReadLine() switch
    {
        "" or "1" => SslMode.Require,
        "2" => SslMode.VerifyFull,
        "3" => SslMode.Prefer,
        _ => throw new InvalidDataException()
    };
    Console.Write("MES 비밀번호 (화면·파일에 표시되지 않음): ");
    var password = ReadPassword();
    try
    {
        var settings = new NpgsqlConnectionStringBuilder
        {
            Host = config.Host, Port = config.Port, Database = config.Database,
            Username = config.Username, Password = password, SslMode = mode,
            Timeout = 10, CommandTimeout = 5, Pooling = false,
            PersistSecurityInfo = false, IncludeErrorDetail = false,
            ApplicationName = "EMI-MES-ConnectionCheck",
            Options = "-c default_transaction_read_only=on -c statement_timeout=5000"
        };
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.OpenAsync(deadline.Token);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        mesOk = Equals(await command.ExecuteScalarAsync(deadline.Token), 1);
        Result(mesOk ? "MES 로그인·SELECT 1: 성공" : "MES 로그인: 응답 확인 실패");
        Result(mode == SslMode.Prefer ? "MES TLS 암호화: 협상 결과 미확인 (암호화 없는 접속 허용 설정)" : "MES TLS 암호화: 사용");
        Result(mode == SslMode.VerifyFull ? "MES 서버 인증서: 검증함" : "MES 서버 인증서: 검증하지 않음 (진단용 설정)");
    }
    catch (PostgresException ex) { Result($"MES 로그인: 실패 · SQLSTATE {ex.SqlState} · {Checks.DbError(ex.SqlState)}"); }
    catch (Exception ex) when (ex is NpgsqlException or OperationCanceledException or System.Security.Authentication.AuthenticationException)
    { Result("MES 로그인: 실패 · 연결 시간 초과 또는 TLS/인증 통신 실패. DBeaver SSL 설정과 함께 확인해 주세요."); }
    finally { password = string.Empty; }

PmsCheck:
    using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 16 * 1024 };
    var pmsOk = await Checks.Pms(client, new Uri(new Uri(config.PmsOrigin), "/health/live"));
    Result(pmsOk ? "PMS HTTPS 상태 확인: 성공" : "PMS HTTPS 상태 확인: 실패 · 인터넷/주소/서버 응답 확인 필요");
    Result("PMS 연동 전용 인증·데이터 수신: 미구현, 이번 검사 대상 아님");
    Result("자동 실행 서비스·실제 데이터 동기화: 아직 설치/실행하지 않음");
    exitCode = mesOk && pmsOk ? 0 : 1;
}
catch { Result("검사 중단 · 설정 파일 또는 실행 환경을 확인해 주세요. 비밀번호·내부 오류 원문은 기록하지 않습니다."); }
Console.WriteLine("\n결과를 공유할 때는 아래 결과 파일만 전달해 주세요. 설정 파일과 비밀번호는 제외해 주세요.");
try
{
    var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EMI", "MesConnectionCheck");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, $"connection-result-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
    File.WriteAllLines(path, new[] { $"검사 시각: {DateTimeOffset.Now:O}" }.Concat(results), new UTF8Encoding(true));
    Console.WriteLine($"결과: {path}");
}
catch { Console.WriteLine("결과 파일 저장 실패. 위 결과를 복사해 주세요."); }
Console.Write("Enter를 누르면 종료합니다.");
Console.ReadLine();
return exitCode;

static string ReadPassword()
{
    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
        else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
    }
    Console.WriteLine();
    var value = new string(chars.ToArray());
    chars.Clear();
    return value;
}

sealed record Settings
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 5432;
    public string Database { get; init; } = "";
    public string Username { get; init; } = "";
    public string PmsOrigin { get; init; } = "";
}

static class Checks
{
    public static void Validate(Settings config)
    {
        if (Uri.CheckHostName(config.Host) == UriHostNameType.Unknown || config.Port is < 1 or > 65535
            || string.IsNullOrWhiteSpace(config.Database) || string.IsNullOrWhiteSpace(config.Username)
            || !Uri.TryCreate(config.PmsOrigin, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/"
            || uri.Query.Length != 0 || uri.Fragment.Length != 0) throw new InvalidDataException();
    }
    public static string DbError(string state) => state switch
    {
        "28P01" => "계정 또는 비밀번호 확인 필요",
        "28000" => "DB의 접속 허용 설정/인증 조건 확인 필요",
        "3D000" => "DB 이름 확인 필요",
        "53300" => "DB 접속 수 제한",
        _ => "DB 접속 조건 확인 필요"
    };
    public static async Task<bool> Pms(HttpClient client, Uri uri)
    {
        try
        {
            using var response = await client.GetAsync(uri);
            if (response.StatusCode != HttpStatusCode.OK) return false;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("status", out var status)
                && (status.GetString() == "healthy" || (status.GetString() == "ok"
                    && json.RootElement.TryGetProperty("name", out var name) && name.GetString() == "live"));
        }
        catch { return false; }
    }
    public static async Task<int> SelfTest()
    {
        var count = 0;
        foreach (var (status, body, expected) in new[]
        {
            (200, "{\"name\":\"live\",\"status\":\"ok\"}", true),
            (200, "{\"status\":\"healthy\"}", true),
            (200, "{\"status\":\"ok\"}", false),
            (200, "<html>login</html>", false),
            (200, "{\"name\":\"live\",\"status\":\"bad\"}", false),
            (200, "{}", false), (401, "{}", false), (302, "{}", false), (500, "{}", false)
        })
        {
            using var client = new HttpClient(new FakeHandler(status, body));
            if (await Pms(client, new Uri("https://example.invalid/health/live")) != expected) return 1;
            count++;
        }
        foreach (var origin in new[] { "http://example.invalid", "https://user:pass@example.invalid", "https://example.invalid/path", "https://example.invalid/?token=secret" })
        {
            try { Validate(new Settings { Host = "127.0.0.1", Database = "test", Username = "test", PmsOrigin = origin }); return 1; }
            catch (InvalidDataException) { count++; }
        }
        Validate(new Settings { Host = "127.0.0.1", Database = "test", Username = "test", PmsOrigin = "https://example.invalid" });
        Console.WriteLine($"Synthetic checks PASS: {count + 1}");
        return 0;
    }
    sealed class FakeHandler(int status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) });
    }
}
