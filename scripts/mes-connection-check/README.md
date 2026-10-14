# MES 노트북 연결 진단 (1단계)

Windows x64용 1회 실행 프로그램. 서비스 설치·자동 시작·업무 동기화 기능은 아직 없다.

1. ZIP을 노트북에 옮기고 폴더 전체를 압축 해제한다.
2. `EMI-MES-ConnectionCheck.exe`를 실행한다. .NET 별도 설치 및 관리자 실행은 필요 없다.
3. 대상 주소를 확인하고 Enter를 누른다.
4. MES TCP 연결 성공 후 DBeaver와 맞는 TLS 모드를 선택한다. 기본은 TLS 필수이며 자동으로 평문으로 재접속하지 않는다. Require는 암호화만 요구하고 인증서는 검증하지 않는다. VerifyFull은 인증서와 호스트를 검증한다. Prefer는 명시 선택한 경우만 사용하며 TLS 미지원 시 평문 접속을 허용한다. 운영 연동의 TLS 정책은 별도 확정한다.
5. MES 비밀번호를 직접 입력한다. 화면에 글자나 별표가 표시되지 않는다. 비밀번호는 파일/결과/명령줄에 저장하지 않지만 검사 동안 프로세스 메모리에는 존재한다.
6. 결과 파일은 `%LOCALAPPDATA%\EMI\MesConnectionCheck`에 저장한다. 결과 파일만 공유하고 설정 파일/비밀번호는 공유하지 않는다.

`connection-settings.json`은 다음 형태다. 실제 대상 정보가 있는 파일은 Git에 넣지 않는다.

```json
{"Host":"127.0.0.1","Port":5432,"Database":"example","Username":"example","PmsOrigin":"https://example.invalid"}
```

- MES: TCP 검사 후 읽기 전용 기본 트랜잭션/5초 statement timeout으로 접속하고 `SELECT 1`만 실행. 테이블/업무 데이터/권한 구조는 조회하지 않는다. 연결 10초, 전체 DB 15초 제한.
- PMS: 지정한 HTTPS origin의 `/health/live`만 GET. 인증서 검증 유지, 리다이렉트 미추적. HTTP 200과 JSON frontend `status=healthy` 또는 backend `name=live`, `status=ok`를 확인한다. HTML 로그인 응답·401·302는 성공 처리하지 않는다.
- PMS 도달 성공은 에이전트 인증·데이터 전송 가능을 뜻하지 않는다. 해당 인증/API는 아직 미구현이다.
- 정상 종료코드 0은 두 진단 성공만 의미한다. 실패 1, 입력 취소/잘못된 호출 2.
- 프로그램은 임의 쿼리, DB 쓰기, 패키지 설치, 방화벽·전원 설정 변경을 지원하지 않는다.
- Windows에서 실행하지 못하면 OS 버전과 프로세서 종류를 확인한다. 현재 Windows x64 빌드이며 ARM64 전용 패키지는 별도 준비가 필요하다.

개발 검증: `dotnet run --project MesConnectionCheck.csproj -- --self-test`
Windows 패키지: `dotnet publish MesConnectionCheck.csproj -c Release -r win-x64 --self-contained true -o <output> -p:DebugType=None`
공식 근거: https://www.npgsql.org/doc/security.html , https://learn.microsoft.com/en-us/dotnet/core/deploying/
