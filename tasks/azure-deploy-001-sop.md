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
- `pms_migrator` 전용 32자 이상 password
- `pms_app` 전용 32자 이상 password
- Front Door origin random token
- Gmail username/app password
- Teams activity client secret

## 4. 배포 Gate

Git에 검증된 배포 코드를 먼저 merge한다. 이 merge는 Azure resource 생성이나 public traffic 활성화를 의미하지 않는다.

1. Foundation 생성과 Key Vault 8개 secret 입력
2. Backend·Frontend·migration·DB bootstrap identity의 secret-scope RBAC 10개 적용 및 전파 확인. 이전 단일 runtime identity를 배포한 적이 있으면 incremental deployment에 남은 vault-scope role assignment를 Portal에서 제거하고 0건을 확인
3. image digest 고정
4. Inactive workload와 두 manual job 생성
5. DB role bootstrap job 성공: `pms_migrator`·`pms_app` 생성과 최소 권한 probe
6. migration job Exact와 runtime object privilege 재조정
7. PITR restore 60분 이내와 aggregate·ledger 검증
8. Active workload readiness
9. DNS·TLS·Front Door origin 차단
10. Entra login과 관리자 승인 workflow
11. Teams manifest 조직 catalog update와 개인 설치
12. actual Teams·Gmail smoke

한 단계가 실패하면 다음 단계로 넘어가지 않는다.

DB 역할 검수에서는 Backend 연결 사용자가 `pms_app`, migration 연결 사용자가 `pms_migrator`인지 확인한다. `pms_app`은 업무 CRUD는 성공해야 하지만 `CREATE TABLE`, `CREATE ROLE`, `schema_migrations` INSERT와 database temporary privilege는 모두 거부돼야 한다. Key Vault 검수에서는 vault scope role assignment가 0이고 각 identity가 접근표에 없는 secret을 읽지 못해야 한다.

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
