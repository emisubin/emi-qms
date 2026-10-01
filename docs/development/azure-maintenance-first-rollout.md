# 최초 유지보수 기능 도입: 1회 중단 배포

사용자가 명시적으로 승인한 최초 1회 대체 절차다. 대화 사전 공지, 조회·로그인을 포함한 일시 중단, 구 처리 종료, migration·교체·검증·재개를 수행한다. 이후 배포는 기존 `deploy-azure-pilot-release.sh`의 공지·팝업·저장 제한을 그대로 사용한다. 이 runner는 일반 workflow에 연결하지 않는다.

## 실행 전

- 승인된 exact main SHA와 required CI를 확인하고 Backend/Frontend digest image를 먼저 게시한다. 이 runner는 build·push·merge하지 않는다.
- 유지보수 Manual job은 미리 준비한다. 기존 Backend의 Production 환경과 Key Vault secret 참조, runtime 연결, 기존 migration identity/연결을 재사용한다. identity·registry·secret 권한은 job 준비 담당자가 검증한다. container 이름은 job 이름과 같아야 하며 저장된 `Maintenance__*` 환경값은 없어야 한다.
- job 실행은 CLI 분기에서 종료하므로 웹 서버와 hosted worker를 시작하지 않는다. `--maintenance-complete`는 runtime 연결과 Directory·청주·오산 exact ledger를 검증한다.
- actor는 두 사업장에 존재하는 활성 사용자여야 한다. 구 worker의 provider 처리·lease 불확정 건이 있다면 사전에 확인한다. 임의 상태 reset이나 실제 시험 발송은 하지 않는다.
- 새 release image의 `--deployment-drain-check`를 기존 migration Job에서 사용한다. 세 DB에 대해 다른 client session·prepared transaction·처리 중/결과 불명확 provider 작업이 없음을 읽기 전용으로 검사한다. 연결/조회 실패도 차단하며 남은 작업이나 lease를 자동 삭제하지 않는다. pgAdmin 및 DB 중계 접속도 점검 전에 닫는다. 배포 창에는 다른 운영자·자동화의 수동 Job/DB 접속을 금지한다.
- restore/PITR 기준선과 승인된 영향 범위를 확인한다. 이전 image는 migration 이후 복구 수단으로 가정하지 않는다.

## 입력과 실행

`scripts/bootstrap-azure-maintenance.sh`를 실행한다. 필수 환경값은 다음과 같다. 실제 식별자·공지 내용은 추적 파일에 넣지 않는다.

```
FIRST_MAINTENANCE_ROLLOUT_APPROVED=true
BUSINESS_SCHEMA_SEPARATION_APPROVED=false # C/O0131 구조 변경을 명시 승인한 실행만 true
ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256= # 승인된 과거 오산 메일 1건만; 기본은 예외 없음
SOURCE_SHA
AZURE_SUBSCRIPTION_ID AZURE_RESOURCE_GROUP ACR_LOGIN_SERVER PUBLIC_HOSTNAME
RECOVERY_POSTGRES_SERVER_NAME
BACKEND_APP_NAME FRONTEND_APP_NAME MIGRATION_JOB_NAME MAINTENANCE_JOB_NAME
BACKEND_RELEASE_IMAGE FRONTEND_RELEASE_IMAGE
MAINTENANCE_RELEASE_ID MAINTENANCE_ACTOR_USER_ID MAINTENANCE_TITLE MAINTENANCE_BODY
MAINTENANCE_STARTS_AT_UTC MAINTENANCE_EXPECTED_ENDS_AT_UTC
```

이미지는 해당 registry의 `pms-backend@sha256:…`, `pms-frontend@sha256:…`만 허용한다. 시각은 timezone을 포함하며 종료 예정은 실행 시점보다 뒤여야 한다. `FIRST_ROLLOUT_POLL_ATTEMPTS` 기본값 90, 간격 기본값 10초다. 테스트 실행파일 교체는 별도 test flag와 synthetic hostname을 동시에 요구한다.

저장된 Job template에는 `Database__MigrationTarget`, `Database__BootstrapTarget`, `Database__BusinessSchemaSeparationApproved`, `Database__RecoveryPostgresHost`, `DeploymentDrain__*`, `Maintenance__*`를 두지 않는다. 대소문자와 .NET 설정 구분자 표기를 정규화해 중복·영구 저장된 실행값을 거부한다. 실행마다 명시적으로 전달하며 기존 secret 참조와 자원 크기는 보존한다. 임의 진단 파일은 더 이상 사용하지 않는다.

`BUSINESS_SCHEMA_SEPARATION_APPROVED`는 기본 false이고 잘못된 문자열은 거부한다. true도 선택한 청주/오산의 정확한0131 트랜잭션에만 적용된다. Directory·역할 준비·종료 확인에는 false를 전달한다. SQL 직접 실행의 승인값과 별개로 runner가 실행 승인값을 덮어쓰므로 예전 연결 옵션에 남은 승인값으로 우회할 수 없다. common0001~0130과 이미 적용한0131 재실행은 기존 원장을 따른다.

## 실행 순서와 증거

1. subscription·Single revision·immutable baseline·ready 상태·공개 `200/401/401`·Manual job과 실행 중복을 확인한다. 복구 helper의 preflight로 PostgreSQL resource ID/FQDN/Ready·보관기간 14일 이상·복구 하한·사용 가능한 Full 백업을 확인한다. `arm`에서 현재 Job execution 이력을 고정한다.
2. frontend, backend 순으로 active revision을 deactivate하고 모든 revision의 replica가 0인지 확인한다. 단순 scale-to-zero를 사용하지 않는다. Azure는 SIGTERM 이후 제한 시간 내 종료되지 않는 컨테이너를 강제 종료할 수 있으므로 replica 0은 provider 처리의 성공을 보장하지 않는다.
3. 양 앱 replica 0 확인 후 기존 migration Job을 새 image의 읽기 전용 종료 확인 모드로 DIRECTORY→CHEONGJU→OSAN 실행한다. 마지막 execution의 **Azure `endTime`**을 drain 완료 기준으로 삼는다. 아래 복구 checkpoint에서 그 시각 뒤 완료된 Full/Automatic 백업을 기다린 뒤 D/C/O drain을 다시 실행하고 백업을 재검증한다. 모든 단계가 통과해야 D/C/O migration을 각각 한 execution씩 실행하며, 첫 변경 요청 직전에 경계를 기록한다. 실패·시간 초과·시작/조회 응답 불명확 시 migration을 실행하지 않고 기존 revision을 복구한다. 최초 도입 drain은 점검 표가 없거나 Idle/Completed인 경우만 허용한다. 응답이 불확정인 변경을 자동 재실행하지 않는다.
4. 새 digest의 유지보수 prepare/activate를 실행한다. 영구 공지는 중단 중 생성되어 재개 시 조회된다. 일반 공지 팝업은 켜지 않는다.
5. Backend, Frontend image만 교체하고 각각 exact ready revision을 확인한다. Frontend 교체 시 조회·로그인은 재개될 수 있으며 저장은 유지보수 gate가 막는다. 공개 보안 smoke 후 complete가 exact ledger를 검사하고 저장을 재개한다.
6. 출력된 private 임시 폴더의 `baseline.json`·`events.jsonl`·`recovery.json`으로 실행 SHA·이전 image/revision·job execution을 확인한다. baseline은 secret 값 없이 secretRef만 보존한다. 실행별 유지보수 payload는 성공·실패 모두 즉시 제거한다. 증거 폴더는 자동 삭제하지 않으며 필요 기간 보관 후 소유자가 정리한다.

Azure CLI 2.88.0의 `start_containerappjob_execution_yaml`은 `JobExecutionTemplate` 형식의 YAML/JSON을 받는다. runner는 기존 job template을 보존하고 image·args·release 환경만 덮어써 임시 JSON으로 전달한다. secret 값을 조회하거나 출력하지 않는다.

## 실패와 재개

- migration 시작 요청 전 실패: 구 backend/frontend revision을 다시 활성화하고 readiness·공개 보안을 확인한다.
- migration 시작 요청 이후 실패: frontend/backend의 모든 active revision을 중단하고 zero replica를 재확인한다. 이전 image rollback과 DB 역migration은 하지 않는다. 중단 검증 실패도 증거에 남긴다.
- 실패 시 기존 execution, 세 DB ledger, 두 maintenance 상태를 확인한 뒤 승인 범위의 forward fix를 정한다. 이 runner를 그대로 재실행하지 않는다. prepare는 동일 release ID에 대해 중복 오류를 내므로 부분 완료를 성공으로 간주하지 않는다.
- 최종 성공 후 두 사업장의 공지 내용과 완료 상태, 사용자 로그인·관련 화면을 확인하고 실제 중단·재개 시각 및 남은 검수를 Task에 기록한다.

관련 원칙: [배포 SOP](../../tasks/azure-deploy-001-sop.md), [Azure 종료 수명주기](https://learn.microsoft.com/en-us/azure/container-apps/application-lifecycle-management).

## 구조 분리 이후 일반 release 연결

일반 수동 workflow의 `approve_business_schema_separation`는 기본 false다. [확정0131 정리 범위](../../database/README.md#cheongjuosan-database-isolation)에 따라 청주27표/projects7열, 오산153표/projects24열/알림3열과 불필요 함수·연결·오산 독립 sequence2개, 오산 권한28개·해당 역할 연결·미사용 부스바 역할1개 제거를 실행할 때에만 별도 운영 승인 범위에 맞춰 선택한다. 이 선택은 이미지 게시·운영 배포 승인과 별개이며 로컬 코드 구현 승인이 실제 운영 실행 승인을 대체하지 않는다.

일반 release는 양 사업부 점검을 같은 release ID로 활성화한 뒤 양 앱의 모든 revision/replica를 멈춘다. 동일한 읽기 전용 CLI를 D/C/O별로 실행하되 `DeploymentDrain__RequireMaintenance=true`로 두 업무 DB의 해당 release 상태가 Active/Delayed인지 확인한다. Directory는 업무용 점검 표를 조회하지 않는다. 최초 도입만 false를 사용한다. 일반 release도 공지/activate 완료 뒤 `arm`, 최초 drain, 복구 checkpoint, 최종 drain·백업 재검증을 거친 뒤 명시 대상별 역할 준비/구조 변경을 진행한다. DB 변경 시작 이후에는 구 image를 자동 재기동하지 않고 중단 상태에서 승인된 보정을 결정한다. Job 결과가 불명확하면 기존 execution부터 확인하며 무조건 재실행하지 않는다.

종료 검사는 해당 시점의 DB 상태를 확인한다. 외부 메일/EC 서비스에서 실제 처리가 끝났다고 단정하거나 진행 중 작업의 성공/실패를 추측하지 않는다. 기본값은 모든 불명확 발송을 차단하며, 일반 관리자 확인·목록 제외나 다음 재시도의 성공만으로 해제되지 않는다. 실제 시험 발송이나 임의 reset을 하지 않는다. 검사와 migration 사이 새 접속을 막는 운영 통제도 유지한다.

### 승인된 과거 오산 메일 1건의 배포 예외

2026-10-01 사용자는 기존 불명확 메일 1건의 추가 조사를 종결하고, 그 1건만 배포 차단에서 제외하도록 승인했다. 원래 attempt/outcome/delivery를 수정·삭제하거나 Sent로 바꾸는 결정이 아니다. 사용자 직접 검수도 이 배포 준비 범위에서 명시적으로 생략했다. 새 불명확 발송, 진행 중 작업, 청주 외부 처리 결과, 접속·DB identity·점검 상태 검사는 유지한다.

- `ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256`는 선택적인 소문자 hex 64자리 하나이며, 목록·와일드카드·잘못된 형식은 Azure 조작 전에 거부한다. 최초 전환과 일반 release 모두 **OSAN의 drain 실행에만** `DeploymentDrain__AcceptedHistoricalOsanMailAttemptSha256`로 전달한다. 다른 DB·migration·역할 준비·웹앱에는 전달하지 않고 Job 영구 template에 저장하지 않는다.
- 일반 workflow의 값은 `azure-pilot-image-publish` Environment의 `PMS_ACCEPTED_HISTORICAL_OSAN_MAIL_ATTEMPT_SHA256` secret에서 받는다. 준비 단계에서는 비공개 실행 파일로 보관하고 실제 운영 설정 반영은 최종 배포 범위에서 수행한다. 값·실제 내부 식별자를 Git/공지/일반 로그에 넣지 않는다. 이후 실행에서도 동일 승인값을 재사용하며 새로운 값으로 자동 갱신하지 않는다.
- SHA-256 입력은 UTF-8이고 마지막 개행 없는 LF 결합이다. 순서는 `osan-mail-deployment-exception-v1`, 실제 DB 이름, `OSAN`, attempt UUID, delivery UUID, attempt 번호, generation, outcome, provider 시작시각, 완료시각, channel, delivery status다. UUID는 소문자 D 형식, 숫자는 10진수, 시각은 UTC `yyyy-MM-ddTHH:mm:ss.ffffffZ`다. 수신자·제목·본문·credential은 읽거나 포함하지 않는다.
- 일치하더라도 Failed/Mail이고 provider 시작·완료 시각이 존재하며, provider ID·sent 시각·claim token·다음 재시도 시각이 없어야 한다. delivery의 attempt 번호와 generation은 해당 attempt와 같아야 한다. 불명확 attempt가 두 개 이상이면 무조건 차단한다. 확인값 불일치나 상태 변경·새 시도는 예외가 되지 않는다.
- 검사 자체는 read-only transaction이고 승인 이력을 별도 배포 준비 기록에 남긴다. 일치 시 비식별 메시지 한 건만 로그에 남기며, 실제 메일의 발송 여부를 확정한 것으로 표현하지 않는다.

구현 근거: [Azure Container Apps Job 실행별 template override](https://learn.microsoft.com/en-us/azure/container-apps/jobs#start-a-job-execution-on-demand), [PostgreSQL 트랜잭션 한정 set_config](https://www.postgresql.org/docs/current/functions-admin.html#FUNCTIONS-ADMIN-SET).

## 이번 구조 분리의 배포 창과 복구 계획

2026-10-01 사용자는 코드·DB 구조 병합과 공개배포를 승인하고 실행 시점은 “지금”으로 확정했다. 사용자 직접 검수는 명시적으로 생략됐으며 required CI·자동 검증은 유지한다. 실제 진행 상태는 Change031 최신 기록이 소유한다. 운영02028에는 공지·점검 기능이 있고 Sep25 최초 도입 예외는 이미 사용했으므로 아래 정상 공지 순서를 따른다.

- 시작 기준 T0는 기술 준비 뒤 공지한 실제 중단 시작시각이다. **60분을 작업 창의 계획 예산**으로 잡는다. 이는 완료 보장이 아니며, 지연 시 상태와 다음 판단 시각을 알리고 검증 없이 저장을 재개하지 않는다. 자동 Full 방식은 최근 완료 패턴에 맞는 창이 필요하다. 지금 실행에는 아래 검증된 논리 사본 방식을 명시적으로 선택한다.
- 중단 전에 정확한 main SHA/필수 CI, 검증한 두 image digest, 기존 revision/image·설정·세 연결 secret의 버전, Manual job 중복 없음, 백업 보관/복구 연습 유효성, 승인된 메일 snapshot, 공지·실행값을 준비한다. 기존 backend 한 개와 현재 자원량을 유지한다. pgAdmin·중계·수동 DB 연결을 닫고 다른 운영자/자동화의 접속을 멈춘다.
- 순서는 공지 게시/별도 팝업 준비 성공 → main 병합/검증된 이미지 준비 → T0에 점검 활성화 → frontend/backend 모든 replica 종료 → D/C/O 읽기 전용 drain → 선택한 방식의 복구 증거 확보 → D/C/O drain 재검사·동일 백업 재검증 → D/C/O migration → 새 backend/frontend readiness·보안 검사 → exact ledger 확인·저장 재개다. 첫 migration 요청 전에 복구 기준이 확보되지 않거나 drain이 실패하면 DB 변경을 시작하지 않는다.
- 기본 Azure Full 방식의 기준은 **최초 drain의 Azure `endTime`보다 뒤에 완료되어 Azure backup list에 표시된 Full/Automatic 백업의 정확한 `completedTime`**이다. helper는 원본 시각 문자열을 보존한다. `earliestRestoreDate`는 복원 하한 확인에만 쓰며 현재시각·임의 대기·과거 일일 백업만으로 최신 복구 가능성을 추정하지 않는다. `latest` 대신 선택한 백업의 완료시각으로 full backup 복원을 요청한다. 논리 사본 방식은 이 시각 조건을 무시하지 않고 별도의 실제 복구 증거로 대체한다.

### 복구 checkpoint의 실행 계약

- `scripts/azure-recovery-checkpoint.py`를 최초 runner와 일반 migration release가 공통 사용한다. 정상 앱 교체만 하는 release에는 DB checkpoint를 요구하지 않는다.
- preflight에서 명시한 subscription/resource group/server와 실제 FQDN을 대조한다. `Database:RecoveryPostgresHost`를 실행별로 주입하여 D/C/O drain과 실제 migration·role bootstrap의 Runtime/Migration/Administrator 연결도 같은 서버를 가리키게 한다. 선택적으로 실행하는 membership backfill 역시 실행 override로 같은 host를 받고 첫 DB 접속 전에 사용 대상 Migration 연결 세 개를 검증한다. 빈 값·다른 host는 DB 쓰기 전에 차단하고 일반 웹앱/영구 Job template에는 이 실행값을 저장하지 않는다.
- `arm` 뒤에는 두 앱의 모든 revision 비활성·replica 0과 resource group 내 모든 Manual Job을 매회 확인한다. 기준선에 없던 실행은 이미 Succeeded/Failed/Stopped여도 거부한다. 이 runner가 받은 최초 drain 3개와 최종 drain 3개의 정확한 execution ID만 예외이며, 재사용·누락도 차단한다. 배포 창 동안 다른 운영자·자동화의 수동 DB/Job 접속 금지는 계속 필요하다.
- 기본 Azure 방식의 후보 백업은 정확한 원본 서버에 속한 Full/Automatic만 허용한다. 미래 시각·잘못된 타입/ID·부분 페이지·중복·조회 실패는 차단한다. 두 방식 모두 마지막 drain 뒤에도 같은 백업과 복구 범위·앱/Job 상태를 재검증한 후에만 첫 DB 변경을 요청한다.
- 대기는 `RECOVERY_CHECKPOINT_TIMEOUT_SECONDS` 기본 900초, 허용 1~1800초이며 `RECOVERY_CHECKPOINT_POLL_SECONDS` 기본 30초, 허용 1~60초다. 공지한 종료 예정시각이 먼저 오면 그 시각까지만 기다린다. 조건이 안 되면 DB migration을 시작하지 않고 기존 서비스 복구를 검증한다. 작업 창에 맞는 자동 백업이 없으면 배포 창을 다시 정한다. 자동 백업의 실행/완료를 이 runner가 보장하지 않는다.
- Azure 조회는 요청별 전체30초와 checkpoint 종료시각 안에서 명확한 일시 오류(429/500/502/503/504 코드 또는 transport timeout)만 최대3회 시도한다. 지수 대기와 노출된 `Retry-After`를 지키며, 인증·대상 없음·분류 불가·응답 형식 오류는 통과시키지 않는다. 최종 실패의 명령 종류·시각·횟수·정수 종료값·고정 오류 분류만 private checkpoint에 남기며 원문 오류·환경값은 저장하지 않는다. 감시와 복원 작업이 동시에 실패하면 복원 작업의 고정 실패 코드도 보존하고 gate는 닫힌 상태로 유지한다. 근거: [Microsoft 일시 오류 처리](https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults), [Resource Manager 요청 제한](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/request-limits-and-throttling).
- 원본 서버는 Burstable이므로 지원하지 않는 on-demand snapshot을 강행하지 않는다. Azure 방식은 백업 조회만 하며 새 백업·서버·유료 자원을 생성하지 않는다. 이 방식의 **Azure에 복구 가능한 백업이 표시됨**과 아래 논리 방식의 실제 로컬 복구 성공은 다른 증거다.
- private `recovery.json`은 SHA/release/server/앱 binding, 기준 Job 실행 이력, Azure drain 종료시각, 선택 백업 ID/완료시각, 검증시각을 저장한다. 디렉터리 0700·파일 0600과 원자적 교체를 사용하며 저장 실패도 차단한다. secret·connection string·메일 승인값·업무 row는 저장하지 않는다. 최초 runner의 실행 폴더는 보존된다. 공개 저장소의 일반 workflow는 OpenSSL 3의 CMS AES-256-GCM·RSA-OAEP(SHA-256)로 암호화한 `recovery.p7m` 하나만 Actions artifact로 14일 보존한다. 수신자 인증서의 주체/발급자 이름도 envelope에 넣지 않도록 `-keyid`를 사용한다. 평문 JSON·상위 폴더를 게시하거나 암호화 실패 시 평문으로 대체하지 않는다. preflight에서도 암호화를 검증하고 최종 verified 기록의 암호화·저장 성공 후에만 DB 변경을 허용한다. runner 자체 손실에는 artifact 게시를 보장할 수 없으므로 실제 운영자는 실행 중 비공개 증거의 보관 가능성도 확인한다.
- 일반 workflow는 `azure-pilot-image-publish` Environment의 `PMS_POSTGRES_SERVER_NAME` 및 `PMS_RECOVERY_EVIDENCE_CERTIFICATE_PEM` 변수를 읽는다. 후자는 Subject Key Identifier를 가진 RSA 2048비트 이상의 공개 인증서이며 공개 인증서만 전달한다. 대응 private key는 운영자가 별도 비공개 보관하고 실제 키로 합성 기록을 복호화할 수 있음을 실행 전에 확인한다. 인증서 누락/오류·지원되지 않는 OpenSSL·암호화/저장 실패는 앱 정지 전에 또는 DB 변경 전에 차단한다. 최초 로컬 runner는 기존 비공개 JSON 보관만 사용해도 된다. 이번 준비에서 운영 변수는 변경하지 않았으며 최종 실행 설정에 실제 원본 서버 이름을 지정한다. release Job 제한은 기존 작업 30분에 기본 backup 대기 15분을 더한 45분이며, 사용자가 공지한 작업 창과 별개다.

구현·합성 실패 검사·독립 검토 결과는 Change031 최신 기록을 따른다. 실제 backup 후보 선택·복원 연습·main 병합·운영 전환은 아직 실행한 것으로 기록하지 않는다.

### 자동 백업을 기다리지 않는 논리 복구 증거

- 기본값은 `RECOVERY_EVIDENCE_KIND=AzureAvailableFullBackup`이다. 로컬 정상 runner에서만 명시적으로 `VerifiedLogicalDatabaseBackup`과 `RECOVERY_LOGICAL_BACKUP_CONFIG`를 함께 설정한다. config는 Git 밖 0700 폴더의0600 JSON이며 임의 명령/검증기를 지정할 수 없다. 동일 source/release/server, 세 DB 및 runtime 역할, 고정 PG16 image ID, loopback 중계 포트, TLS root certificate, 전용 사본 폴더와 키를 지정한다. 키나 credential은 로그·Actions·Git에 보내지 않는다. config hash도 checkpoint binding에 묶어 실행 도중 바뀌면 거부한다.
- frontend를 통한 기존 중계는 앱 중단 시 끊긴다. 이번 실행은 같은 environment의 기존 ClamAV 앱에 시간 제한이 있는 단일 exec/SSH relay를 사용한다. PostgreSQL host/5432만 허용하고 DB TLS를 끝까지 검증한다. DB credential은 로컬 client만 보유하며 ClamAV 설정·identity·public ingress를 바꾸지 않는다. 이 통로 외 pgAdmin/수동 DB 연결은 종료한다. 중계가 살아 있어도 실제 DB 세션은 최초·최종 drain 때0이어야 한다.
- helper는 최초 drain 뒤 고정된 `postgres-logical-recovery.py`를 호출한다. 세 DB를 PG16 도구로 dump하고 globals는 `--no-role-passwords`로 수집한다. 같은 PG16의 단일 network-none/tmpfs cluster에 **실제 보관할 암호문을 복호화하여** owner/ACL을 유지한 채 복구한다. 복구 오류, DB identity/원장·schema/owner/ACL·표 데이터·sequence·large object·runtime DB 접근 차이는 모두 실패다. source의 client는읽기 전용이며 백업 전후와 최종 drain까지 serving 앱 정지와 Job 이력 고정을 유지한다.
- Azure의 OID10 bootstrap 역할을 원본에서 확인하여 로컬 cluster도 동일하게 초기화한다. 복구용 globals에서 이미 생성된 그 역할의 `CREATE ROLE` 정확히 한 문장만 제외한다. 원본 globals는 그대로 보관한다. Azure 전용 `pg_signal_autovacuum_worker`의 관리 역할 GRANT는 upstream PG16에 없어, 확인된 한 문장이 한 번만 등장하고 세 DB schema에 의존성이 없을 때만 로컬 검증에서 제외한다. 제외 문장·원본 hash·사유는 암호화 manifest에 남기며 다른 권한 오류는 허용하지 않는다. 따라서 이 검증은 해당 플랫폼 차이를 명시한 업무 DB 복구이며 Azure 관리 기능까지 동일하다는 의미가 아니다. 관측된 `/mnt/pg_tmp`는 로컬 tmpfs로만 준비하며 다른 tablespace 경로는 거부한다.
- 같은 조건식도 PostgreSQL 재파싱 뒤 괄호 표현이 달라질 수 있다. 별도로 읽은 원본 schema SQL을 격리 cluster에 적용해 PostgreSQL 자체가 만든 canonical schema를 기대값으로 보관하고, 실제 전체 복구 결과와 비교한다. 원본 SQL과 데이터 dump는 그대로 보존하며 운영 원본의 변경 여부는 raw schema와 데이터 proof로 다시 검사한다. SQL 괄호·제약조건을 임의 삭제하거나 문자열 차이를 통째 무시하지 않는다. 빈 schema 검증 DB 제거와 전체 복구는 소유·image·network가 확인된 로컬 cluster 안에서만 수행한다.
- 복구 작업에는 helper의 남은 배포 창과 driver의 최대 예산 중 작은 값을 적용한다. source client와 restore container는 실행별 이름·소유 label로 추적하며, 시간 초과 이후에도 별도의 제한된 정리 예산으로 해당 실행 자원만 제거하고 부재를 확인한다.
- 큰 사본은 단일 CMS 메시지의 대용량 처리 한계를 피하도록64MiB 조각으로 나눈다. 각 조각은 기존 AES256GCM/RSA-OAEP CMS로 암호화하고, 사본 ID·조각 순서/개수·전체/조각 크기를 암호화된 조각 안에도 넣어 대조한다.64MiB 이하의 manifest 등은 기존 단일 CMS를 유지한다. 전체 사본은 여전히 단일 private 파일이며 전체 SHA256과 암호화 manifest에 결합된다. 분할 사본은 원시 `openssl cms -decrypt` 한 번으로 열 수 없고 이 driver의 분할 복호화가 필요하다. 누락·재배열·중복·다른 사본 조각·변조·잘림·후행 데이터와 크기 제한 초과는 거부한다.
- 복원 검증은 원본 접속 계정과 bootstrap 계정의 기본 search_path가 같다고 가정하지 않는다. identity·migration 원장·점검 상태의 위치는 `public`으로 명시하고, 각 표·sequence는 catalog에서 읽은 정확한 schema와 이름으로 조회한다. 역할·기본 경로를 바꾸거나 검증 항목을 제외해 일치시키지 않는다.
- 논리 사본 생성 실패 시 고정 driver의 형식에 맞는 오류 코드만 private0600 및 암호화 checkpoint의 `logicalBackupFailureCode`에 남긴다. 다른 예외는 `UNEXPECTED_FAILURE`, 일반 실행 출력은 `LOGICAL_BACKUP_FAILED`이며 원문 오류나 인증정보는 기록하지 않는다. 실패한 checkpoint는 candidate/verified로 승격하지 않으므로 사본 없이 DB 변경을 진행할 수 없다.
- 논리 사본은 DB 구조/업무 데이터의 복구 수단이다. Azure 서버/방화벽/관리 identity나 role 비밀번호를 백업한 것으로 해석하지 않는다. 기존 서버 설정·Key Vault 참조/버전을 별도로 보존하며 실제 복구 시 해당 설정과 인증정보를 다시 결합한다. 외부 첨부 객체는 이번 구조 변경 대상이 아니며 기존 저장소를 유지한다.
- 암호화 사본, 복구 결과 manifest, SHA·release·원본 서버·drain 시각·파일 hash를 개인 사본 폴더에 보존한다. 첫 DB 변경 직전 동일 암호문/manifest와 앱·Job·세 DB 종료 상태를 다시 확인한다. 누락·변조·다른 실행 사본·복호화/복구 오류·대상 불일치에는 migration을 허용하지 않는다. 평문 임시 사본과 소유 복구 cluster는 성공/실패 모두 정리하고 암호화 사본은 보존한다.
- 02028 공지는 old CLI execution 한 번으로 청주·오산을 함께 준비한다. 이후 새 정상 runner는 `MAINTENANCE_PREPARED=true`, `MAINTENANCE_PREPARATION_IMAGE=<검증된 새 backend digest>`를 사용해 두 사업부의 동일 공지를 검증한다. old 이미지를 새 target loop에 반복 호출하거나 사전 공지 뒤 기본 workflow를 실행하면 중복 준비/활성화가 되므로 사용하지 않는다.

| 실패 경계 | 실행할 복구 |
| --- | --- |
| 첫 migration 요청 전 | DB를 바꾸지 않았음을 확인하고 기존 backend/frontend revision을 재개한 뒤 readiness·공개 보안을 확인한다. |
| 첫 migration 요청 이후, 실행 결과 불명확 포함 | 앱 중단을 유지한다. 기존 execution·세 DB ledger/identity·점검 상태를 먼저 확인하고 승인 범위의 forward fix를 선택한다. runner 전체 재실행·구 image 재기동·역migration으로 추측 복구하지 않는다. |
| DB를 원래 시점으로 복원해야 하는 경우 | Azure 방식은 기록한 Full 백업 `completedTime`을 사용한다. 논리 방식은 동일 중단 구간의 검증된3DB 사본·globals를 사용한다. 별도 승인된 대상에 복원하여 schema/원장·보존 수량·사업부 권한/연결·private network·서버 설정·기존 Key Vault 인증 참조를 검증한다. 복원 DB에 맞는 이전 앱 버전과 연결을 함께 전환한 뒤 공개 보안과 로그인/핵심 기능을 확인한다. 기존 운영 서버를 덮어쓰거나 삭제하지 않는다. |

PITR은 새 서버를 생성하므로 임시 비용과 연결 전환이 수반된다. 이 준비에서 새 유료 자원을 만들지 않았고, 실제 복구 실행 시 필요한 대상·비용 범위를 최종 운영 승인에 포함한다. 복구 시간은 별도이며 60분 배포 창 안에 완료된다고 보장하지 않는다. 재개 전까지 사용자 쓰기를 막아 복구시각 이후의 업무 데이터 유실을 피한다. 재개 후 문제가 발견되면 이후 입력분을 따로 보존·대조할 계획 없이 과거 시점으로 전환하지 않는다.

Microsoft 근거: [PostgreSQL backup/PITR 및 복원 후 확인 항목](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore). 새 서버 생성, 선택한 시각, 원본 미덮어쓰기, 네트워크·서버 설정 재확인 조건을 반영했다. 이번 관측값과 검증 증거는 [Change 031](../../tasks/azure-deploy-001-change-031.md)의 최신 기록을 따른다.

복구시각 선택 근거: [Azure Full 백업 목록과 completedTime 복원](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/how-to-restore-full-backup), [사용 가능한 백업 목록 API](https://learn.microsoft.com/en-us/rest/api/postgresql/backups-automatic-and-on-demand/list-by-server?view=rest-postgresql-2025-08-01).

암호화 근거와 복호화: [OpenSSL CMS 공식 문서](https://docs.openssl.org/3.0/man1/openssl-cms/). 운영자는 private 작업 폴더에서 `openssl cms -decrypt -binary -inform DER -in recovery.p7m -recip recipient.pem -inkey recipient.key -out recovery.json`의 종료 성공을 확인한 뒤 JSON을 읽는다. 실제 키 준비·설정·복호화 확인은 이번 합성 검증에 포함되지 않는다.
