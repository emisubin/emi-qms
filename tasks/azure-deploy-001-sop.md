# TASK-AZURE-DEPLOY-001 SOP — 20일 시범 배포

## 1. 현재 판정

- Local deployment code: 구현·자동 검증 완료
- Azure resource: 생성 안 함
- 공개 traffic: 전환 안 함
- 실제 Teams·Gmail: 발송 안 함
- 비용 발생 단계: 사용자 실행 대기

## 2. 비용 Gate

사용자는 Azure Portal에서 아래 항목을 확인한 뒤 실제 생성 여부를 결정한다.

| 서비스 | 시범 사양 | 필요한 이유 |
| --- | --- | --- |
| Front Door Standard | 1 profile, custom rate limit | 최종 hostname·managed TLS·origin 보호와 단일 진입점 |
| Container Apps | Consumption, Front 0.25/0.5, API 1/2, ClamAV 2/4 | 세 프로젝트의 작은 사용량에서 idle 비용을 줄이고 ClamAV 메모리를 보장 |
| PostgreSQL | Burstable B2s, 32 GB, PITR 14일, HA 없음 | 시범 입력량에는 충분하고 1시간 복구 rehearsal로 HA 비용을 보류 |
| ACR | Basic | Backend·Frontend image digest 보관 |
| Azure Files | 5 GB | ClamAV signature 재시작 보존 |
| Key Vault | Standard | DB·Gmail·Teams secret 비추적 주입 |
| Log Analytics | 1 GB/day cap, 30일 | Container와 보안 로그 수집 비용 상한 |
| Application Insights | workspace based | 추후 application telemetry 연결 지점 |

실제 실행 직전 20일 예상 비용과 무료 credit 잔액을 다시 확인한다. Budget 알림은 100, 150, 180달러 지점에 둔다.

## 3. 사용자 입력값

실제 값은 tracked 문서가 아니라 Portal과 ignored `*.local.json`에만 둔다.

- 최종 public hostname
- Entra tenant, API client, SPA client, API audience와 verified domain
- Teams activity client, catalog app, manifest external identifier
- 비상 관리자 두 명
- 경보 수신자 alias
- PostgreSQL 관리자 password
- Front Door origin random token
- Gmail username/app password
- Teams activity client secret

## 4. 배포 Gate

Git에 검증된 배포 코드를 먼저 merge한다. 이 merge는 Azure resource 생성이나 public traffic 활성화를 의미하지 않는다.

1. Foundation 생성과 Key Vault 입력
2. image digest 고정
3. Inactive workload 생성
4. migration job Exact
5. PITR restore 60분 이내와 aggregate·ledger 검증
6. Active workload readiness
7. DNS·TLS·Front Door origin 차단
8. Entra login과 관리자 승인 workflow
9. Teams manifest 조직 catalog update와 개인 설치
10. actual Teams·Gmail smoke

한 단계가 실패하면 다음 단계로 넘어가지 않는다.

DB 복구, edge·인증과 actual provider smoke는 `PRE_TRAFFIC_GATE`다. Git merge 후에 실행하되, 세 Gate가 모두 PASS가 되기 전에는 public traffic과 external notification을 활성화하지 않는다.

## 5. 개인정보 안전 증빙

보고에는 다음 aggregate만 남긴다.

- resource 종류와 개수
- 배포 성공/실패
- image digest 존재 여부
- migration expected/applied count와 Exact 여부
- restore 소요시간과 aggregate 일치 여부
- HTTP status와 security header 존재 여부
- 로그인 성공 사용자 수, 승인 대기 사용자 수
- Teams·Gmail test 성공/실패

hostname, email, tenant/client identifier, token, secret, connection string, 실제 업무명과 첨부 원문은 기록하지 않는다.
