using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;

Console.OutputEncoding = Encoding.UTF8;
if (!OperatingSystem.IsWindows()) { Console.WriteLine("Windows TPM 전용입니다."); return 2; }
return await RunWindows();

[SupportedOSPlatform("windows")]
static async Task<int> RunWindows()
{
    const string keyName = "EMI-PMS-MES-Probe-v1";
    const string route = "/integrations/mes/probe";
    const string marker = "EMI-MES-PROBE-v1";
    var provider = CngProvider.MicrosoftPlatformCryptoProvider;
    var code = 1;
    try
    {
        Console.WriteLine("EMI 중계 노트북 등록 / 시험 전송\n1: TPM 공개키 등록 파일 만들기\n2: PMS 시험 전송");
        Console.Write("선택: ");
        var action = Console.ReadLine();
        if (action is not ("1" or "2")) return 2;
        if (!CngKey.Exists(keyName, provider, CngKeyOpenOptions.MachineKey))
        {
            if (action != "1") { Console.WriteLine("먼저 1번으로 TPM 키를 생성하세요."); return 2; }
            using var created = CngKey.Create(CngAlgorithm.Rsa, keyName, new CngKeyCreationParameters
            {
                Provider = provider, KeyCreationOptions = CngKeyCreationOptions.MachineKey,
                KeyUsage = CngKeyUsages.Signing, ExportPolicy = CngExportPolicies.None,
                Parameters = { new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None) }
            });
        }
        using var key = CngKey.Open(keyName, provider, CngKeyOpenOptions.MachineKey);
        if (key.Provider != provider || key.ExportPolicy != CngExportPolicies.None)
            throw new CryptographicException();
        using var rsa = new RSACng(key);
        if (rsa.KeySize != 2048) throw new CryptographicException();
        var pub = rsa.ExportSubjectPublicKeyInfo();
        var device = Convert.ToHexStringLower(SHA256.HashData(pub));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        if (action == "1")
        {
            // Prove the locally generated key can actually sign before exporting public registration data.
            var challenge = RandomNumberGenerator.GetBytes(32);
            var proof = rsa.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!rsa.VerifyData(challenge, proof, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw new CryptographicException();
            var file = Path.Combine(AppContext.BaseDirectory, "device-registration.json");
            File.WriteAllText(file, JsonSerializer.Serialize(new { deviceId = device, publicKey = Convert.ToBase64String(pub), provider = provider.Provider }, json));
            Console.WriteLine($"TPM 키 생성·서명 확인 완료. 등록 파일: {file}\n이 파일에는 공개키만 있습니다. PMS 등록 담당자에게 전달하세요.");
            code = 0;
        }
        else
        {
            var config = JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pms-settings.json")), json)
                ?? throw new InvalidDataException();
            if (!Uri.TryCreate(config.Origin, UriKind.Absolute, out var origin) || origin.Scheme != "https"
                || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0)
                throw new InvalidDataException();
            Console.WriteLine($"대상 PMS: {origin.Host}\n회사망에 연결한 상태에서 Enter를 누르세요. 취소는 Q.");
            if (!string.IsNullOrEmpty(Console.ReadLine())) return 2;
            var id = Guid.NewGuid();
            var sent = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var body = JsonSerializer.SerializeToUtf8Bytes(new { deviceId = device, requestId = id, sentAt = sent, message = marker }, json);
            var prefix = Encoding.UTF8.GetBytes(marker + "\nPOST\n" + route + "\n");
            var signature = rsa.SignData([..prefix, ..body], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 8192 };
            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, route)) { Content = new ByteArrayContent(body) };
            req.Content.Headers.ContentType = new("application/json");
            req.Headers.Add("X-Mes-Signature", Convert.ToBase64String(signature));
            using var response = await http.SendAsync(req);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"전송 거부/실패: HTTP {(int)response.StatusCode}. 서버 활성화·공개키 등록·회사 IP·노트북 시각을 확인하세요.");
            }
            else
            {
                var receipt = await response.Content.ReadFromJsonAsync<Receipt>(json);
                if (receipt is null || receipt.RequestId != id || receipt.Message != marker
                    || receipt.Sha256 != Convert.ToHexStringLower(SHA256.HashData(body))
                    || Math.Abs((receipt.ReceivedAt - DateTimeOffset.UtcNow).TotalSeconds) > 180)
                    Console.WriteLine("실패: PMS 응답이 보낸 데이터와 일치하지 않습니다.");
                else { Console.WriteLine($"성공: 인증된 시험 데이터 수신·응답 대조 완료\n요청 번호: {id}\n서버 수신 시각: {receipt.ReceivedAt:O}"); code = 0; }
            }
        }
    }
    catch (Exception e) when (e is CryptographicException or UnauthorizedAccessException)
    { Console.WriteLine("TPM 키 생성/사용 실패. 관리자 실행, TPM 상태, 기존 키 접근권한을 확인하세요. 소프트웨어 키로 대체하지 않습니다."); }
    catch { Console.WriteLine("실패: 설정 파일·네트워크·TLS·서버 응답을 확인하세요. 내부 오류나 인증 정보는 출력하지 않습니다."); }
    Console.Write("Enter를 누르면 종료합니다."); Console.ReadLine();
    return code;
}
record ClientSettings(string Origin);
record Receipt(Guid RequestId, string Message, string Sha256, DateTimeOffset ReceivedAt);
