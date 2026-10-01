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
BACKEND_APP_NAME FRONTEND_APP_NAME MIGRATION_JOB_NAME MAINTENANCE_JOB_NAME
BACKEND_RELEASE_IMAGE FRONTEND_RELEASE_IMAGE
MAINTENANCE_RELEASE_ID MAINTENANCE_ACTOR_USER_ID MAINTENANCE_TITLE MAINTENANCE_BODY
MAINTENANCE_STARTS_AT_UTC MAINTENANCE_EXPECTED_ENDS_AT_UTC
```

이미지는 해당 registry의 `pms-backend@sha256:…`, `pms-frontend@sha256:…`만 허용한다. 시각은 timezone을 포함하며 종료 예정은 실행 시점보다 뒤여야 한다. `FIRST_ROLLOUT_POLL_ATTEMPTS` 기본값 90, 간격 기본값 10초다. 테스트 실행파일 교체는 별도 test flag와 synthetic hostname을 동시에 요구한다.

저장된 Job template에는 `Database__MigrationTarget`, `Database__BootstrapTarget`, `Database__BusinessSchemaSeparationApproved`, `DeploymentDrain__*`, `Maintenance__*`를 두지 않는다. 대소문자와 .NET 설정 구분자 표기를 정규화해 중복·영구 저장된 실행값을 거부한다. 실행마다 명시적으로 전달하며 기존 secret 참조와 자원 크기는 보존한다. 임의 진단 파일은 더 이상 사용하지 않는다.

`BUSINESS_SCHEMA_SEPARATION_APPROVED`는 기본 false이고 잘못된 문자열은 거부한다. true도 선택한 청주/오산의 정확한0131 트랜잭션에만 적용된다. Directory·역할 준비·종료 확인에는 false를 전달한다. SQL 직접 실행의 승인값과 별개로 runner가 실행 승인값을 덮어쓰므로 예전 연결 옵션에 남은 승인값으로 우회할 수 없다. common0001~0130과 이미 적용한0131 재실행은 기존 원장을 따른다.

## 실행 순서와 증거

1. subscription·Single revision·immutable baseline·ready 상태·공개 `200/401/401`·Manual job과 실행 중복을 확인한다.
2. frontend, backend 순으로 active revision을 deactivate하고 모든 revision의 replica가 0인지 확인한다. 단순 scale-to-zero를 사용하지 않는다. Azure는 SIGTERM 이후 제한 시간 내 종료되지 않는 컨테이너를 강제 종료할 수 있으므로 replica 0은 provider 처리의 성공을 보장하지 않는다.
3. 양 앱 replica 0 확인 후 기존 migration Job을 새 image의 읽기 전용 종료 확인 모드로 DIRECTORY→CHEONGJU→OSAN 한 번씩 실행한다. 모두 Succeeded인 경우에만 구조 변경으로 진행한다. 실패·시간 초과·시작/조회 응답 불명확 시 migration을 실행하지 않고 구 revision을 복구한다. 이 최초 도입 검사에서는 점검 표가 없거나 Idle/Completed인 경우만 허용한다. 이후 D/C/O migration도 각각 한 execution씩 실행하며, 첫 변경 실행 요청 직전에 경계를 기록한다. 응답이 불확정해도 자동 재실행하지 않는다.
4. 새 digest의 유지보수 prepare/activate를 실행한다. 영구 공지는 중단 중 생성되어 재개 시 조회된다. 일반 공지 팝업은 켜지 않는다.
5. Backend, Frontend image만 교체하고 각각 exact ready revision을 확인한다. Frontend 교체 시 조회·로그인은 재개될 수 있으며 저장은 유지보수 gate가 막는다. 공개 보안 smoke 후 complete가 exact ledger를 검사하고 저장을 재개한다.
6. 출력된 private 임시 폴더의 `baseline.json`과 `events.jsonl`로 실행 SHA·이전 image/revision·job execution을 확인한다. baseline은 secret 값 없이 secretRef만 보존한다. 실행별 유지보수 payload는 성공·실패 모두 즉시 제거한다. 증거 폴더는 자동 삭제하지 않으며 필요 기간 보관 후 소유자가 정리한다.

Azure CLI 2.88.0의 `start_containerappjob_execution_yaml`은 `JobExecutionTemplate` 형식의 YAML/JSON을 받는다. runner는 기존 job template을 보존하고 image·args·release 환경만 덮어써 임시 JSON으로 전달한다. secret 값을 조회하거나 출력하지 않는다.

## 실패와 재개

- migration 시작 요청 전 실패: 구 backend/frontend revision을 다시 활성화하고 readiness·공개 보안을 확인한다.
- migration 시작 요청 이후 실패: frontend/backend의 모든 active revision을 중단하고 zero replica를 재확인한다. 이전 image rollback과 DB 역migration은 하지 않는다. 중단 검증 실패도 증거에 남긴다.
- 실패 시 기존 execution, 세 DB ledger, 두 maintenance 상태를 확인한 뒤 승인 범위의 forward fix를 정한다. 이 runner를 그대로 재실행하지 않는다. prepare는 동일 release ID에 대해 중복 오류를 내므로 부분 완료를 성공으로 간주하지 않는다.
- 최종 성공 후 두 사업장의 공지 내용과 완료 상태, 사용자 로그인·관련 화면을 확인하고 실제 중단·재개 시각 및 남은 검수를 Task에 기록한다.

관련 원칙: [배포 SOP](../../tasks/azure-deploy-001-sop.md), [Azure 종료 수명주기](https://learn.microsoft.com/en-us/azure/container-apps/application-lifecycle-management).

## 구조 분리 이후 일반 release 연결

일반 수동 workflow의 `approve_business_schema_separation`는 기본 false다. [확정0131 정리 범위](../../database/README.md#cheongjuosan-database-isolation)에 따라 청주27표/projects7열, 오산153표/projects24열/알림3열과 불필요 함수·연결·오산 독립 sequence2개, 오산 권한28개·해당 역할 연결·미사용 부스바 역할1개 제거를 실행할 때에만 별도 운영 승인 범위에 맞춰 선택한다. 이 선택은 이미지 게시·운영 배포 승인과 별개이며 로컬 코드 구현 승인이 실제 운영 실행 승인을 대체하지 않는다.

일반 release는 양 사업부 점검을 같은 release ID로 활성화한 뒤 양 앱의 모든 revision/replica를 멈춘다. 동일한 읽기 전용 CLI를 D/C/O별로 실행하되 `DeploymentDrain__RequireMaintenance=true`로 두 업무 DB의 해당 release 상태가 Active/Delayed인지 확인한다. Directory는 업무용 점검 표를 조회하지 않는다. 최초 도입만 false를 사용한다. 검사 통과 뒤 명시 대상별 역할 준비/구조 변경을 진행한다. DB 변경 시작 이후에는 구 image를 자동 재기동하지 않고 중단 상태에서 승인된 보정을 결정한다. Job 결과가 불명확하면 기존 execution부터 확인하며 무조건 재실행하지 않는다.

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

2026-10-01의 준비 계획이다. 사용자 직접 검수는 명시적으로 생략됐으며 required CI·자동 검증은 유지한다. 이번 승인은 준비까지다. 실제 공지·main 병합·이미지 게시·운영 구조 변경·앱 교체는 최종 실행 단계에 남는다.

- 시작 기준 T0는 기술 준비와 최종 실행 승인이 끝난 뒤 공지한 실제 중단 시작시각이다. 별도 시간대 답변이 없으므로 가장 이른 가능한 시점을 기본안으로 두되, 지금 중단을 예약하거나 시각을 확정하지 않는다. **60분을 작업 창의 계획 예산**으로 잡는다. 이는 완료 보장이 아니며, 지연 시 상태와 다음 판단 시각을 알리고 검증 없이 저장을 재개하지 않는다.
- 중단 전에 정확한 main SHA/필수 CI, 검증한 두 image digest, 기존 revision/image·설정·세 연결 secret의 버전, Manual job 중복 없음, 백업 보관/복구 연습 유효성, 승인된 메일 snapshot, 공지·실행값을 준비한다. 기존 backend 한 개와 현재 자원량을 유지한다. pgAdmin·중계·수동 DB 연결을 닫고 다른 운영자/자동화의 접속을 멈춘다.
- T0 이후 순서는 대체 사전 공지 확인 → frontend/backend 모든 replica 종료 → D/C/O 읽기 전용 drain → 복구 기준시각 기록 → D/C/O migration → 점검 공지 준비/활성화 → 새 backend/frontend readiness·보안 검사 → exact ledger 확인·저장 재개다. 첫 migration 요청 전에 복구 기준이 확보되지 않거나 drain이 실패하면 DB 변경을 시작하지 않는다.
- 복구 기준시각은 세 DB의 쓰기가 멈추고 기존 처리가 종료된 이후이면서 **첫 migration 요청 이전**인 UTC 시각으로 정한다. Azure의 사용 가능한 PITR 범위에 해당 시각이 들어왔는지 최종 단계에서 확인하고, 확인할 수 없으면 중단한다. 오늘 확인한 일일 backup 완료시각 자체를 배포 직전 복구시각으로 대체하지 않는다. DB 변경 뒤에 `latest` 복구를 선택하면 변경 후 상태가 복원될 수 있으므로 기록한 custom 시각을 사용한다.

**실행 준비의 미완료 선행조건:** 현재 최초 전환 runner는 drain 후 바로 migration을 시작하며 위 복구시각 기록·확인 checkpoint가 없다. 따라서 이 계획을 근거로 현재 runner를 그대로 실행하면 안 된다. 최종 운영 실행 준비에서 해당 checkpoint와 실패 시 migration 미시작을 구현·시험하고 독립 검토를 마친 뒤에만 T0를 확정한다. Azure에서 확인 가능한 복구 범위의 증거도 먼저 정해야 하며, 일정 시간 대기나 일일 backup 성공만으로 임의의 직전 시각 복원을 증명했다고 간주하지 않는다. 이번 1~3번 범위는 메일 예외 구현·검수 생략·계획 작성이며 이 실행기 보완과 실제 운영 전환까지 완료한 것은 아니다.

| 실패 경계 | 실행할 복구 |
| --- | --- |
| 첫 migration 요청 전 | DB를 바꾸지 않았음을 확인하고 기존 backend/frontend revision을 재개한 뒤 readiness·공개 보안을 확인한다. |
| 첫 migration 요청 이후, 실행 결과 불명확 포함 | 앱 중단을 유지한다. 기존 execution·세 DB ledger/identity·점검 상태를 먼저 확인하고 승인 범위의 forward fix를 선택한다. runner 전체 재실행·구 image 재기동·역migration으로 추측 복구하지 않는다. |
| DB를 원래 시점으로 복원해야 하는 경우 | 기록한 custom 시각으로 별도 PostgreSQL 서버에 PITR한다. 청주·오산·Directory 세 DB를 같은 시점으로 확인하고 schema/원장·보존 수량·사업부 권한/연결·private network·설정·secret 참조를 검증한다. 복원 DB에 맞는 이전 앱 버전과 연결을 함께 전환한 뒤 공개 보안과 로그인/핵심 기능을 확인한다. 기존 운영 서버는 덮어쓰거나 삭제하지 않는다. |

PITR은 새 서버를 생성하므로 임시 비용과 연결 전환이 수반된다. 이 준비에서 새 유료 자원을 만들지 않았고, 실제 복구 실행 시 필요한 대상·비용 범위를 최종 운영 승인에 포함한다. 복구 시간은 별도이며 60분 배포 창 안에 완료된다고 보장하지 않는다. 재개 전까지 사용자 쓰기를 막아 복구시각 이후의 업무 데이터 유실을 피한다. 재개 후 문제가 발견되면 이후 입력분을 따로 보존·대조할 계획 없이 과거 시점으로 전환하지 않는다.

Microsoft 근거: [PostgreSQL backup/PITR 및 복원 후 확인 항목](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore). 새 서버 생성, custom 시각, 원본 미덮어쓰기, 네트워크·서버 설정 재확인 조건을 반영했다. 이번 관측값과 검증 증거는 [Change 031](../../tasks/azure-deploy-001-change-031.md)의 최신 기록을 따른다.
