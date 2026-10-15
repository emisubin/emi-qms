# MES 중계 노트북 등록 및 PMS 시험 전송

현재 단계는 MES 계정 없이 고정 시험 데이터의 인증·수신·응답 대조를 확인하는 도구다. MES 조회·쓰기, 실제 업무 데이터 전송, 상시 서비스는 포함하지 않는다.

## 노트북 담당자

1. Windows x64 노트북에서 배포 ZIP을 모두 풀고 `EMI-MES-DeviceProbe.exe`를 관리자 권한으로 실행한다. 별도 .NET 설치는 필요 없다.
2. `1`을 선택한다. TPM의 machine RSA2048 서명키를 만들고 실제 서명 가능 여부를 확인한다. 기존 키가 있으면 재사용한다. 소프트웨어 키 대체는 하지 않는다.
3. 같은 폴더의 `device-registration.json`을 PMS 담당자에게 전달한다. 공개키와 식별자만 들어 있으며 개인키는 내보내지 않는다.
4. PMS 담당자가 공개키 등록과 서버 활성화를 완료했다고 안내한 뒤 회사망에서 다시 실행하여 `2`를 선택한다. 표시된 PMS 주소를 확인하고 Enter를 누른다.
5. 성공은 서버 수신 데이터의 요청 번호·고정 메시지·본문 해시·수신 시각까지 일치했다는 의미다. 단순 health 접속 성공과 다르다. 실패하면 화면의 상태를 담당자에게 전달한다.

`pms-settings.json`에는 HTTPS origin만 둔다. 실제 설정과 기기 등록 파일은 Git 제외 대상이다. 프로그램 폴더 삭제는 TPM 키 삭제가 아니다. 키 폐기는 별도 승인 작업이다.

## 등록과 보안 경계

지정 노트북에서 관리자가 실행한 프로그램의 공개키를 별도 전달받아 등록한다. 임의 사용자의 등록 파일을 자동 승인하지 않는다. 이 구현은 원격 TPM attestation을 제공하지 않는다. 서버는 등록 공개키 서명을 검증하며, TPM 출처는 이 통제된 등록 절차로 확인한다.

서버는 기본 비활성이다. 등록 키 서명, 회사 공인 IP, 신뢰 프록시 토큰, ±120초 시각, 고정 메시지와 요청 형식을 모두 확인한다. 기존 직원 권한은 부여하지 않는다. 회사 IP는 회사 인터넷 출구 확인이며 특정 AP의 물리 위치 증명은 아니다. IP가 변해도 자동으로 제한을 풀지 않는다.

동일 요청은 청주 DB의 고유키로 한 번만 소비한다. 재전송 방지 정보는 최소 10분 보존하고 다음 유효 요청에서 만료 정보를 정리한다. 업무 데이터나 MES 비밀번호는 저장하지 않는다.

## 운영 활성화 담당자

공개 배포는 별도 승인 범위다. 현재 Windows TPM 실기기 실행과 공개 경로의 헤더 전달 검증은 대기 상태다.

- 청주 business migration `0136_mes_probe_receipts.sql`을 기존 승인된 배포 절차로 적용한다. 새 DB는 만들지 않는다.
- 등록 파일의 SPKI SHA256과 DeviceId 일치를 확인한다.
- workloads/identity-access의 `enableMesProbe`는 기본 false다. 활성화 시 등록 공개키, DeviceId, 회사 공인 IP를 배포 입력으로 제공한다. 실제 주소를 저장소에 기록하지 않는다.
- backend에는 `MesProbe:Enabled`, `DeviceId`, `PublicKey`, `CompanyIp`, `RequireCompanyIp=true`, `ProxyToken`을 설정한다. ProxyToken은 기존 Key Vault origin 검증 secret을 참조하며 조건부로 해당 secret만 읽기 허용한다.
- frontend와 backend 이미지를 함께 반영한다. EasyAuth 예외는 활성화된 단일 `/integrations/mes/probe` 경로뿐이다. nginx는 검증된 Front Door 경유 요청에서 proxy token과 X-Azure-SocketIP 기반 IP를 덮어쓴다. backend ingress는 내부 전용을 유지한다.
- 등록 노트북·회사망 성공 및 회사망 외부 거부를 확인한다. 일반 직원 인증/업무 경로도 확인한다.
- 차단은 Enabled=false로 처리한다. IP 제한 해제는 별도 명시 결정 후 RequireCompanyIp=false로만 처리하며 TPM 서명 조건은 유지한다.

## 구현 근거

- [Microsoft CNG key storage providers](https://learn.microsoft.com/en-us/windows/win32/seccertenroll/cng-key-storage-providers): Platform Crypto Provider.
- [Microsoft TPM key attestation](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/component-updates/tpm-key-attestation): 원격 키 출처 증명은 별도 기능이다.
- [Azure Front Door HTTP headers](https://learn.microsoft.com/en-us/azure/frontdoor/front-door-http-headers-protocol): SocketIP의 의미와 프록시 헤더 경계.
