# TASK-AZURE-DEPLOY-001 Change 031 — 오산 1단계 등록 전용 공개 배포

> 최신 작업(2026-09-29): 기존 backend 1개를 유지하는 청주·오산 업무/DB 구조 분리. 확정 SQL과 새 합성 DB에서의 검증 결과는 문서 끝에 기록한다. 아래 2026-09-07 배포 승인·상태는 당시 이력이며 이번 구조 변경의 운영 적용 승인이 아니다. 추가 승인 범위의 배포 연결 코드·합성 검증·독립 검토까지 완료했고 로컬 커밋으로 보관한다. 사용자 검수·원격 반영·운영 적용은 미실행이며 운영 DB는 변경하지 않았다.

## 상태

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- roadmapOverrideSource: `USER_EXPLICIT_2026-09-07_OSAN_PHASE1_AZURE_DEPLOY`
- gateStatus: `PASS_REUSE`
- sourceTasks: `TASK-OSAN-ISOLATION-001`, `TASK-OSAN-ACCESS-001`, `TASK-OSAN-PROJECT-001`
- sourceBranch: `feat/task-osan-project-001-project-registration`
- sourceBaseline: `574cea66f602eb65eb1d110801d331151731b0a6`
- initialSourceHead: `b62e5aebdb1b857c11c25f10ae708c869bfdd2ac`
- productionDeploymentApproved: `true`
- gitPublicationApproved: `true`
- mainMergeApproved: `false`
- selectorUserValidation: `PENDING`
- status: `LOCAL_VALIDATION_COMPLETE_AWAITING_DRAFT_PR_CI`

## 승인과 목적

사용자는 2026-09-07 오산 Task 1~3의 로그인·사업부 해석, 소속 없음·local profile pending gate, 총괄 소속 관리, 선택 사업부 local 역할 관리와 오산 프로젝트 등록·목록·상세를 Azure 공개 환경에 배포하도록 명시했다. 이 승인은 read-only 사전 점검, 배포용 코드·기록 작성, 로컬 검증·commit, remote branch·PR·CI 준비와 승인된 Azure 배포 조작을 포함한다. 대표 `main`의 exact merge는 별도 명시 승인이 없으므로 현재 Root 지침의 마지막 병합 gate로 남긴다.

Roadmap의 다음 제품 순서는 Task 4이지만 사용자가 Task 4·5보다 먼저 Task 1~3만 공개 배포하도록 명시해 이번 Change에 한해 partial sequence override를 적용한다. 별도 rollout Task를 만들지 않고 기존 `TASK-AZURE-DEPLOY-001`을 재사용한다.

## 알려진 제약과 사용자 검수 상태

- 일반 사용자가 청주·오산 소속을 동시에 부여받으면 사업부를 전환할 수 없는 문제는 사용자가 이번 배포에서 보정을 보류했다. 1단계 운영에서는 일반 사용자에게 active membership을 정확히 한 곳만 부여한다. 총괄 관리자의 다중 소속은 기존 계약대로 허용한다.
- `TASK-OSAN-ACCESS-001 Change 002`의 selector 숨김 보정은 자동 test와 desktop/mobile 시각 증빙이 완료됐지만 사용자 검수 완료 기록은 없다. 상태를 `USER_VALIDATION_PENDING`으로 유지한다.
- `TASK-OSAN-PROJECT-001 Change 005` 사용자 검수는 완료됐다.

## Implementation Direction Brief

### 현재 동작과 root cause

운영 PostgreSQL에는 기존 단일 업무 DB만 있고 Directory·Osan DB가 없다. 운영 workload는 legacy `QmsDatabase` connection과 DB secret 3개만 연결하며 migration·role bootstrap job도 단일 DB 설정이다. 새 제품 코드는 세 논리 DB와 각각 분리된 runtime/migrator 역할·secret을 요구하므로 현재 배포 정의로 image만 교체하면 Production startup 또는 migration이 fail-closed된다.

### 권장 구현과 순서

1. 기존 단일 업무 DB를 청주 canonical DB로 그대로 사용한다. DB rename·copy나 기존 데이터 이동을 하지 않는다.
2. 같은 Flexible Server에 빈 Directory·Osan DB를 추가한 뒤 세 DB마다 admin/migration/runtime connection을 별도 Key Vault secret으로 둔다. 기존 DB secret 3개는 청주 연결로 재사용해 직전 legacy image rollback 경로를 보존한다.
3. workload와 secret-scope RBAC를 세 DB에 맞게 확장한다. Backend는 runtime 3개, migration job은 migration 3개, role bootstrap은 9개를 읽는다. membership backfill job은 Directory·Cheongju migration connection과 private 승인 ID 목록만 읽는다.
4. role bootstrap → Directory `0001/0002`와 Cheongju·Osan business `0001..0087` migration → 승인 계정 Cheongju membership backfill → Backend digest → Frontend digest 순서를 release script에서 fail-closed로 고정한다.
5. public 활성 전 세 DB identity·exact ledger, bounded role negative probe, backup/restore readiness, Backend readiness를 확인한다. 외부 provider는 청주 기존 설정을 유지하되 Osan provider·escalation·deletion worker는 비활성으로 유지한다.
6. 공개 전후 익명 `200/401/401`, 청주 회귀, 제한된 오산 계정과 등록 준비를 확인한다. 승인된 정정 경로가 없으므로 fake 운영 프로젝트는 만들지 않는다.

### exact allowlist

- `.github/workflows/azure-pilot-images.yml`
- `.github/workflows/ci.yml`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitConfiguration.cs`
- `backend/src/Emi.Qms.Api/BusinessUnits/BusinessUnitMembershipBackfillRunner.cs`
- `backend/tests/Emi.Qms.Api.Tests/BusinessUnitIsolationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/PublicDeploymentSecurityTests.cs`
- `frontend/e2e/full-stack/business-unit-access.full-stack.spec.ts`
- `frontend/e2e/full-stack/iqc-digital-report.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-adaptive-navigation.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-compact-workspaces.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-first-experience.full-stack.spec.ts`
- `frontend/e2e/full-stack/osan-project-registration.full-stack.spec.ts`
- `frontend/e2e/full-stack/pending-list.full-stack.spec.ts`
- `frontend/e2e/full-stack/project-registration.full-stack.spec.ts`
- `frontend/e2e/mock-ui/osan-project-registration.spec.ts`
- `frontend/playwright.full-stack.config.ts`
- `frontend/src/App.tsx`
- `frontend/src/QrScanLandingPage.tsx`
- `infrastructure/azure-pilot/identity-access.bicep`
- `infrastructure/azure-pilot/identity-access.json`
- `infrastructure/azure-pilot/identity-access.parameters.example.json`
- `infrastructure/azure-pilot/workloads.bicep`
- `infrastructure/azure-pilot/workloads.json`
- `infrastructure/azure-pilot/workloads.parameters.example.json`
- `infrastructure/azure-pilot/README.md`
- `scripts/deploy-azure-pilot-release.sh`
- `scripts/test-azure-pilot-release.sh`
- `scripts/validate-azure-pilot-artifacts.sh`
- `docs/00-product-roadmap.md`
- `tasks/azure-deploy-001-change-031.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `tasks/osan-pilot-001-rollout-handoff.md`
- `tasks/osan-isolation-001-implementation-report.md`
- `tasks/osan-access-001-implementation-report.md`
- `tasks/osan-project-001-implementation-report.md`

### 불변조건과 제외 범위

- Directory/Cheongju/Osan은 같은 server·서로 다른 database·runtime role·migration role·secret을 사용하고 다른 DB로 fallback하지 않는다.
- Business identity 계약은 `0086_business_unit_database_identity`, Directory identity 계약은 `0001_business_unit_directory`로 유지하고 최신 ledger 번호와 섞지 않는다.
- 기존 청주/G2 데이터·provider 설정·public security·single revision·Backend 최대 replica 1을 보존한다.
- 오산 progress mutation·auto-complete·dashboard·Pending/hold/cancel/deleted/Excel과 외부 provider·worker를 활성화하지 않는다.
- migration은 additive이며 down migration·운영 DB overwrite·fake 업무 레코드를 금지한다.
- `latest` tag를 만들지 않고 exact current-main SHA와 immutable image digest만 사용한다.
- Persistent UAT, 실제 외부 provider 시험, Task 4·5 제품 구현, 일반 사용자 dual-membership 결함 보정은 제외한다.

### 관찰 가능한 완료 조건

- 세 DB가 각각 기대 이름·역할·identity로 결속되고 Directory ledger `0001..0002`, 두 business ledger `0001..0087`이 Exact다.
- 기존 청주 aggregate와 G2 기능이 보존되고, 오산 DB의 실제 업무 프로젝트 수는 초기 `0`이다.
- 승인된 현재 계정은 Directory에 연결되고 총괄 또는 정확히 한 사업부 membership과 선택 사업부 local role로 접근한다.
- 오산에서 프로젝트 create/list/detail만 사용할 수 있고 이후 단계 기능은 노출·mutation되지 않는다.
- Backend·Frontend가 새 digest에서 Ready이고 공개 health `200`, 익명 root/API `401/401`이다.
- 직전 Backend·Frontend immutable image가 기록돼 application rollback 가능하며 additive DB 변경은 forward-fix 대상으로 남는다.

### 검증과 증거

- Backend 전체 Release suite, 다중 DB isolation/access/project 집중 suite, migration `0087` upgrade·repeat, public deployment security suite.
- Frontend 전체 test·typecheck·lint·production build, selector desktop/mobile evidence의 기존 artifact와 상태 대조.
- Azure artifact static validation, Bicep compile·tracked ARM 동등성, release script 정상·각 stage 실패·rollback mock.
- PR required CI와 exact head 확인. merge 뒤 main CI와 release run의 exact SHA/digest를 기록한다.
- Azure mutation 전 server/PITR/network/DB 목록, 앱/job mode, secret reference·RBAC projection, 기존 revision/digest를 privacy-safe로 기록한다.
- 실제 실행 후 DB별 identity·ledger·role negative probe와 aggregate, public security, Cheongju/Osan 접근을 privacy-safe count/status로만 남긴다.
- Microsoft 365 실제 사용자 검수와 첫 실제 프로젝트 등록을 직접 관찰하지 못하면 완료로 쓰지 않는다.

### parent 반환 조건

source-of-truth 충돌, destructive operation, 기존 데이터 불일치, 실제 provider 권한 필요, branch protection/environment gate 또는 exact `main` merge 승인이 필요하면 의존 stage를 중단하고 현재 recoverable state와 필요한 한 가지 결정을 보고한다. 한 stage가 실패하면 downstream을 중단하고 Osan을 disabled/isolated로 두며 청주 health를 재확인한다.

## 로컬 검증 결과

- Backend Release 전체 suite `582/582 PASS`, 실패·skip `0`.
- Frontend unit `297/297 PASS`, mock Chromium `13/13 PASS`, typecheck·production build PASS, lint error `0`과 기존 warning `1`.
- Full-Stack은 단일 업무 DB 회귀 `64/64 PASS`, Directory·Cheongju·Osan 전용 business-unit access `1/1 PASS`, Osan create/list/detail·Cheongju 불변 `1/1 PASS`로 최종 `66/66 PASS`다. 각 전용 실행에서 세 DB, 여섯 bounded role, exact migration과 자원 cleanup을 확인했다.
- Bicep compile·tracked ARM 구조 동등성·Azure artifact 정적 검증, release 정상/실패/DB-only/rollback mock, workflow actionlint, shell syntax, main PR CI·CI gate·change scope와 diff check가 모두 PASS다.
- 첫 `55/66` 진단 실행은 서로 다른 DB topology를 같은 harness에 넣은 문제, 개발 사용자 전환 뒤 shell remount를 반영하지 못한 mobile test, QR resolve가 runtime mode 확정 전에 시작되는 실제 race를 드러냈다. 특수 두 시나리오는 전용 3-DB harness로 분리하고 CI에서 별도로 실행했으며, stale UI test를 현재 공용 화면·selector 계약에 맞췄다. QR 화면은 runtime mode가 준비된 뒤에만 resolve하도록 제품 코드 두 파일을 보정했다.
- Draft PR #121의 첫 CI run `34104864066`은 Backend 일반 job이 disposable DB를 안전 harness 이름으로 표시하지 않아 581개 통과 뒤 전용 3-DB test 1개가 fail-closed했다. CI service DB 이름을 `emi_qms_e2e_*` 경계로 정렬해 전체 582개를 같은 격리 runner에서 실행하도록 보정했으며, 수정 head의 필수 CI를 다시 통과해야 merge gate로 이동한다.
- 운영 Azure mutation, image push, DB/secret/RBAC 변경, migration과 app revision 교체는 `0`건이다. 다음 단계는 Draft PR 필수 CI이며, selector 사용자 검수와 exact `main` merge 승인 전에는 운영 mutation을 시작하지 않는다.


---

## 2026-09-29 후속 DB 조사·업무 분리 기록

아래는 같은 Change를 이어 온 조사·분리 작업 기록이다. 위의 2026-09-07 배포 승인·상태는 당시 이력이며 이번 분리 작업의 운영 변경 승인으로 확대하지 않는다. 현재 범위는 아래 최신 구현 시작 기록을 따른다.

# 기존 PMS 서버를 통한 Mac DB 접속 — Change 031

현행 구현은 마지막 「공유 SSH 통로로 재구성」 절을 따른다. 그 이전 절은 최초 구현과 실패 보정의 이력이다.

## 최초 구현 이력

- 목적: 새 Azure 자원 없이 Mac의 pgAdmin에서 운영 PostgreSQL에 연결하여 사용자가 직접 학습한다.
- 승인: 사용자 2026-09-28 기존 자원 연결 검증 요청 및 검증 결과에 대한 “시작하자”. Mac 연결 도구 준비와 비로그인 TLS 검사까지 진행한다. 운영 데이터 수정·배포·공개 포트·유료 자원 생성·원격 게시 승인은 포함하지 않는다.
- 기준선: `10d8b0bcb5618a2e055c69cf9de2a9e3e7c0f5ab`, `fix/task-gov-codex-002-instruction-clarity`. 기존 WIP는 제외.
- 변경: `scripts/pms-db-tunnel.sh`, `scripts/pms-db-tunnel.py`, 합성 소켓 검증 `scripts/test-pms-db-tunnel.py`. Azure CLI 내부 exec WebSocket을 이용하며 공식 port-forward 제품이 아니다. 서버 패키지/파일/config 변경 없이 세션 내 `stty`, 시간제한 `nc`만 실행한다.
- 고정 경계: PMS 구독·환경·frontend·DB 호스트, IPv4 loopback `127.0.0.1:15432`, 최대 8개 연결, 2시간 도구 수명. 원격 프로세스에도 시간 제한 및 10분 네트워크 대기 제한. 비밀값/DB 내용 로깅 없음. DB 권한을 우회하거나 read-only로 강제하지 않는다.
- 사용: `bash scripts/pms-db-tunnel.sh`, 종료 Control+C. `--check`는 PostgreSQL SSLRequest와 인증서/호스트명 검증 후 종료하며 DB 로그인은 하지 않는다.
- pgAdmin: Connection host `127.0.0.1`, port `15432`, maintenance DB 청주 `emi_qms` / 오산 `emi_qms_osan`, username `pmsdbadmin`, password는 사용자 직접 입력. Parameters의 SSL mode `require`, Connection timeout `120`, SSH Tunnel 끔. 연결 준비 후 사용. 실제 데이터 수정은 pgAdmin 저장 시 운영 DB에 반영된다.
- 검증 완료: Bash/AST syntax, 5개 합성 테스트(바이너리 왕복·8개 동시 연결과 9번째 거부·포트 충돌 시 소유자 보존·종료·원격 실패·수명 만료·원격 준비 중 종료·stderr 분리). Mac sandbox의 socket bind 제한으로 첫 시도 실패 후 승인된 로컬 소켓 시험으로 5개 PASS.
- 실제 연결: `--check` TLS1.3 인증서/호스트명 검증 PASS, 사용자 정상 실행의 준비 메시지 PASS, PTY Control+C exit0 및 종료 후 15432 포트 닫힘 PASS. 비밀번호 사용·DB 로그인·데이터 조회/변경 없음. 현재 시험 연결 모두 종료.
- 독립 검토: `review_db_tunnel`, 요청 모델 `gpt-6-astra/high`, 실제 관측 모델 `NOT_REPORTED`; GO, Open P0/P1/P2 없음. reviewer는 구문 및 프레임 테스트를 직접 확인했고 나머지 socket 테스트는 reviewer sandbox bind 제한 때문에 재실행하지 못함. 작성 측 테스트와 실제 TLS 증거를 구분해 검토. Python 구현 SHA256 `dc26bd784e4941e1faa2b1be4bd33f24a93879745a86a88b7181d579c517c580`.
- 상태: 로컬 도구 구현·검증·검토 이력은 아래에 보존. 사용자 표 탐색에서 600초 관리 접속 제한을 확인했으므로 안정적인 실사용 완료로 보지 않는다. 운영 배포·자원 변경·원격 게시 없음.
- 한계: Azure CLI 내부 구현 호환성, 재배포/콘솔 종료/시간 제한 시 pgAdmin 재접속 필요. 새 고정비 자원은 없지만 기존 Consumption 사용량 증가는 0원 보장 불가.

## 사용자 접속 실패 보정 — 2026-09-28

- 사용자는 잘못된 DB 이름 `emi_pms_osan`의 FATAL 응답 이후 올바른 이름으로 수정했지만 연결 종료 오류를 보고했다. 앱은 Running, DB는 Ready였고 로컬 리스너도 실행 중이었다.
- 실제 재현: 사용자 통로의 비로그인 TLS 검사 1/3 성공, 2/3 연결 종료. 원격 exec 시작을 좁혀 관측한 5회 중 3회 HTTP429, 2회 PostgreSQL SSLRequest 수락. 기존 도구는 429를 즉시 연결 종료로 처리했다.
- 보정: 429에 한해서 `Retry-After`를 존중하거나 15/30/60초 fallback, 공유 cooldown으로 연결 준비를 재시도한다. 활성 DB 통신·SQL은 재시도하지 않는다. 다음 시도 시작 예산은 최대75초이며 진행 중 SDK 호출까지 강제 종료하는 전체 timeout이 아니다. 도구 종료 시 cooldown 중단. 안전한 실패 이유 표시. pgAdmin/자체 TLS 검사 대기시간120초.
- 증거: 총9개 합성 시험 PASS. 수정본 별도 loopback에서 비로그인 실제 TLS1.3 인증서 검증 3/3 PASS(3.8/1.8/2.7초); 시험 통로 종료. 이 3회에는 실제429가 재발하지 않았으며429 처리는 합성 응답으로 검증했다. 사용자 기존 리스너는 임의 종료하지 않았다.
- 독립 재검토: 동일 reviewer의 영향 diff 검토 GO, Open P0/P1/P2 없음. 새 throttle 시험4개와 추가 헤더/상태 반례를 reviewer가 직접 검증. 구현 SHA256 `6bafb8a2af6a03dc60d2ff327b51969524bc0d1d5f2485bf008d7b7dfdd43a9d`.
- 사용자 다음 행동: 기존 터미널 Control+C → 같은 명령으로 재실행 → pgAdmin timeout120으로 Save. 실제 로그인·표 탐색 성공은 사용자 확인 대기. DB 비밀번호/실데이터 접근·서버 설정 변경·배포·원격 게시 없음.

## 지속 오류의 실제 사용자 증거 — 2026-09-28

- 사용자: Save 이후 표를 열 때 비밀번호 창이 다시 나오고 접속 실패. 터미널은 `약 600초 뒤 다시 시도` 뒤 `Azure 접속 제한이 계속됩니다` 반복. 정상 EOF 표시와 별개로 실제 접속 제한이다.
- 현재 구조는 pgAdmin의 각 DB 연결마다 Azure 관리 exec 세션을 연다. 600초 Retry-After는75초 시도 예산과120초 pgAdmin timeout을 넘는다. 단순히75초를 늘리는 것으로 실사용 안정성을 입증하지 못한다. 제한의 정확한 서버 측 quota 종류/수치는 미확인.
- 이번 좁은 수정은 표시만 보정: 이미 성립한 통로의 EOF를 무조건 실패라고 부르지 않으며, 실제 실패는 별도 표시. 예산을 넘는600초 대기는 자동 재시도한다고 잘못 안내하지 않고 이번 접속 종료와 남은 대기 시간을 명시한다. 기존 연결/재시도 동작 변경 없음.
- 합성11개 시험 PASS(이전9개 + EOF 중립 표시 및 오류/비밀 로그 분리). 실제 Azure 추가 반복 시험은 중단하여 접속 제한에 영향을 더하지 않음. 사용자 리스너 임의 종료 없음.
- 남은 일: 사용자가 터미널에서 중지하고 제한 시간이 지난 뒤 재접속할 수는 있으나 해결 보장 아님. 현재 도구를 안정적인 상시 pgAdmin 경로로 권하지 않는다. 별도 연결 구조 검토가 필요하며 새 자원·서버 배포 승인으로 확대하지 않는다.

## 공유 SSH 통로로 재구성 — 2026-09-28

- 사용자 요청: 반복되는 600초 접속 제한이 생기지 않도록 연결 도구를 제대로 보정하고 인터넷의 기존 구현·공식 자료를 참고한다. 같은 기존 PMS 서버·Mac 접속 범위의 로컬 구현과 제한된 비로그인 연결 검증을 이어간다.
- 원인에 맞춘 변경: 도구 실행 시 Azure 관리 exec WebSocket을 한 번 열고, 표/DB 연결은 같은 SSH transport의 `direct-tcpip` 채널로 처리한다. 표를 추가로 열거나 채널이 실패해도 새 Azure exec를 만들거나 SQL/통신 내용을 자동 재전송하지 않는다. 이 숫자는 **exec 세션 수**이며 시작 시 구독·앱·replica·인증 정보를 읽는 개별 관리 API 요청까지 한 번이라는 뜻은 아니다.
- 구현: Mac의 기존 Azure CLI Python/Paramiko와 Go `golang.org/x/crypto/ssh`를 사용한다. 목적지는 컴파일된 PMS DB 호스트:5432만 허용하고 사용자·임시 키·SSH 서버 키를 검증한다. shell/SFTP/remote-forward는 거절한다. loopback15432·최대16연결·2시간 수명. 동일 도구의 중복 실행 차단, 429 Retry-After는 로컬 timestamp로 보존하고 대기 종료 전 재요청하지 않는다.
- 원격 임시 실행 경계: 기존 frontend의 비공개 `mktemp` 디렉터리에 실행 파일·임시 host key·공개 client key를 전달한다. 패키지 설치·공개 listener·설정/이미지/배포·새 자원·DB 권한/데이터 변경은 없다. 전송 파일 hash 확인, 디렉터리0700/키0600. 정상 종료는 인증된 빈 `pms-close@emi.local` 요청으로 helper를 종료하고 shell EXIT trap이 해당 디렉터리만 지운다. 네트워크가 갑자기 끊겨 정상 종료 요청을 전달하지 못하면 시간 제한이 최종 정리를 담당한다. 강제 종료/컨테이너 재시작 등 모든 상황에서 즉시 정리를 보장한다는 뜻은 아니다.
- 빌드: `scripts/build-pms-db-relay.sh`; 공식 Go 이미지 digest 고정, 모듈 go.sum 고정, linux/amd64 정적 binary 생성. `.build/`는 Git 제외이며 실행 전 소스·바이너리 SHA256 일치를 확인한다. 사용자 Mac에서는 빌드 완료 상태다.
- 참고: [SSH 연결 프로토콜 RFC4254 §5.3/7.2](https://www.rfc-editor.org/rfc/rfc4254.html), [Paramiko Transport](https://docs.paramiko.org/en/stable/api/transport.html), [Go SSH server API](https://pkg.go.dev/golang.org/x/crypto/ssh#NewServerConn), [Microsoft ACA console](https://learn.microsoft.com/en-us/azure/container-apps/container-console). 표준 SSH 라이브러리의 다중 채널/EOF 규칙을 따른다. Azure exec 위에 연결한 부분은 이 프로젝트의 통합이며 Microsoft가 공식 지원하는 DB port-forward 제품으로 표현하지 않는다.
- 로컬 검증: Go 단위·race PASS(40회 순차/12개 동시 채널,16제한,인증·목적지·shell 거절,양방향 EOF,수명/취소,3개 활성 채널 종료). Python 합성28개 PASS(기존21개, 종료3개, 업로드 ACK2개, Base64 복원/손상 거부2개). 실제 nginx 이미지·uid101·network none 컨테이너에서 생산용 Linux binary 업로드→raw PTY→SSH→Mac listener 통합 PASS: 12개 동시+40회 순차,1MiB 포함 바이트 일치, 잘못된 목적지/shell 거절, 임시 디렉터리 정리 확인. 이 시험은 실제 Azure/DB에 접근하지 않았다.
- 시험 중 발견·보정: Docker TTY의 기본 이탈 키가 ELF 바이트와 충돌하고, 이탈 키의 첫 바이트가 ACK 단위 끝에 있으면 보류되어 교착하는 것을 재현했다. 공식 [Moby attach 구현](https://github.com/moby/moby/blob/master/daemon/internal/stream/attach.go)과 [EscapeProxy](https://github.com/moby/term/blob/master/proxy.go)에서 확인했다. 시험은 로컬 Docker Engine exec API를 사용하되, 실제 제품도 같은 종류의 터미널 제어 해석을 피하도록 업로드와 SSH 바이트를 Base64로 감싸 전송한다. 내부 연결/인증/채널은 계속 표준 SSH이다. 콘솔 제공자가 PTY를 유지할 수 있어 단순 소켓 종료만으로 remote helper가 즉시 끝나지 않는 경우를 재현하고 명시적 인증 종료 요청과 3초 대기 상한을 추가했다.
- 실제 Azure 연결 검증·독립 검토: 아래 최종 결과에 기록한다. 사용자 pgAdmin 로그인·표 탐색·편집 검수는 여전히 사용자 확인 사항이며 개발자가 비밀번호를 가져오거나 실제 데이터를 대신 수정하지 않는다.

- 실제 Azure 초기 시도2회: 관리 exec와 upload-ready는 성공했으나 bulk binary 전송 후 SSH-ready가 오지 않아 종료했다. 429 재발 증거는 없었고 DB 로그인/질의는 없었다. 이 실패를 성공으로 계산하지 않는다. 이후 gzip+Base64 업로드,4KiB마다 수신 ACK,1KiB 입력 frame,전체 업로드180초 상한,SSH Base64 line framing을 보정하고 다시 검증 중이다.

- 최종 전송 보정 검증: Python28개 PASS, Go 단위·race PASS(추가 `armor_test.go`:1MiB 임의 바이트·분할·CRLF·EOF·크기 제한·손상 거부·1/2byte 즉시 flush). 기본 TTY detach 처리 환경의 Docker Engine exec API를 통해 최종 Linux 바이너리 업로드→SSH→12동시+40순차 왕복·1MiB·경계 거절·원격 임시 디렉터리 제거까지 PASS. Azure에 그대로 이식했다고 가정하지 않고 실제 연결 결과는 별도로 남긴다.
- 최종 실제 Azure 검증: 최종 도구로 관리 exec 세션1개를 만들고 업로드/SSH 준비 성공 → 인증서·호스트명 검증을 동반한 TLS1.3 동시8회 PASS →45초 유휴 후 같은 통로에서 순차8회 PASS. 도중 exec 세션 수는1 유지, 시험 exit0. DB 로그인·비밀번호·질의·수정 없음. 시험 소유 로컬 listener/SSH 종료. 이어 별도 읽기 전용 exec1회로 `/tmp/pms-db-relay.*` 디렉터리 개수만 확인하여 **0개** 관측. 이 정리 확인용 exec는 앞의 데이터 통로 세션1개와 별도이다.
- 최종 독립 검토: 새 `review_shared_db_tunnel`, 요청 `gpt-6-astra/high`, 관측 모델 `NOT_REPORTED`. 누적 기준선+허용 WIP 및 마지막 전송 보정 검토 GO, Open P0/P1/P2/P3 없음. reviewer는 최종 Framing/SharedTransport15개를 직접 실행 PASS했고, 전체28개·Go/race·Docker·실제Azure는 작성 측 증거와 코드를 구분하여 검토했다. Python SHA256 `bbbf98460a3340d11cb66bc6fa093a3749364e495b62a6d32ffe1ffd27c07290`, Go main `ffe6c51f694172dc218f10197245b790544afe914646c0e84338405b06767074`.
- 현재 상태: 구현·자동 검증·독립 검토 완료, 사용자 pgAdmin 로그인/다중 표 탐색 검수 대기. 재개 방법은 기존 도구 터미널 Control+C → `bash scripts/pms-db-tunnel.sh` → 「연결 준비 완료」 확인 후 pgAdmin 기존 등록 서버로 재접속. 호스트127.0.0.1·포트15432·DB명/SSL 설정은 그대로이다. 사용자의 기존 프로세스는 임의 종료하지 않았다.
- 한계: 시작 시 Azure 관리 서비스가 이미 제한 중이면 Retry-After까지 기다려야 한다. 2시간 만료·앱 재시작·통로 단절 때는 사용자 재접속이 필요하다. 이 구현이 플랫폼 장애나 상시 연결 안정성을 보장한다는 뜻은 아니다. 새 VM/VPN/공개 포트·유료 자원·운영 배포·DB 내용 변경·push/PR/merge 없음. 완료 변경만 로컬 commit한다.

## 사용자 표 조회 확인 및 보류 이슈 — 2026-09-28

- 사용자 확인: pgAdmin의 View/Edit Data → First 100 Rows로 표를 열었다고 보고했다. 로그인 후 단일 표 조회 성공 확인으로 기록하며, 다중 표 탐색·수정/저장 검수까지 완료한 것으로 확대하지 않는다.
- 추적 항목 `OSAN-G2-001` (OPEN / 조사 보류): 사용자가 오산 DB라고 인식한 접속에서 G2 관련 데이터가 보인다고 보고했다. 사용자가 원인 확인은 나중에 진행하고 DB 학습을 먼저 이어가기로 했다.
- 아직 미확인: 해당 화면의 실제 연결 DB 이름·schema·table 이름, G2를 식별한 column, 데이터 유입 시점/경로. 사용자 관측을 기록한 것이며 오산 운영 DB에 잘못 유입되었다는 원인/결론은 확정하지 않는다. 실데이터·화면 복사본은 보관하지 않는다.
- 조사 재개 시: 사용자가 열었던 연결과 테이블을 먼저 특정하고, 해당 사업부의 데이터 계약 및 초기 적재/이관 경로와 대조한다. 실제 데이터 삭제·수정으로 이어가지 않는다. 지금은 원인 조사나 주기적 모니터링을 시작하지 않는다.
- 다음 학습: 현재 표의 table/column 이름을 바탕으로 행·열·기본키·NULL·다른 표와의 관계를 설명한다. 사용자가 화면을 직접 조작한다.

## 실제 청주·오산 테이블 비교 — 2026-09-29

- 사용자 요청: 청주 업무 표에 실제 데이터가 있는지, 오산 DB에 미사용 표가 많은 이유를 확인한다. 두 업무 DB runtime credential은 Azure Key Vault에서 메모리에만 수신하고, 기존 사용자 터널을 인증서/호스트명 검증 후 재사용했다. `default_transaction_read_only=on`, statement timeout8초/lock timeout1초로 메타데이터·COUNT·최신 시각만 조회했다. 자격증명·업무 행 원문·개인정보를 출력/기록하지 않았고 운영 데이터/구조/사용자 터널을 변경하지 않았다.
- 기준선 정정: 현재 작업 checkout은 운영보다 오래된 0085까지의 구조다. 이전 학습 안내가 최신 오산 구조를 반영하지 못했다. 실제 backend `backend--0000068`, image digest `eee3a695f98161af78d692f6d7bc89e278e96ed02f0c252162c63ecc900c8b6f`를 확인했고, 같은 로컬 image OCI revision label이 `02028f2739af3da197a047008f448b3f3e95d230`임을 확인하여 그 commit의 코드를 검토했다. branch/source 전환은 하지 않았다.
- 실제 DB 이름/identity: `emi_qms`=CHEONGJU, `emi_qms_osan`=OSAN. 양쪽 public 기본 테이블209개씩, 테이블 이름 집합과 열 이름/타입 구성 동일, 적용 migration 원장130개 동일(최종0130). 청주 비어 있지 않은 표102개/빈 표107개, 오산 비어 있지 않은 표70개/빈 표139개. 전체 COUNT 실패0. 조회 시점의 물리 행 수이며 논리삭제·비활성·시험/업무 데이터 구분 없이 계산했다.
- 공통 구조 생성 원인: 배포 버전 `DatabaseMigrationRunner`는 두 business target을 순회하면서 같은 `migrationCatalog.GetMigrationFiles()`를 적용한다. Directory는 별도 catalog다. 따라서 청주에도 오산 전용 표가 만들어져 있으며 반대도 같다. 공통 migration에는 양식·단계 등 기준정보 INSERT도 있어, 비어 있지 않은 기준정보 표를 곧바로 해당 업무의 실사용 증거로 볼 수 없다.
- 비교(청주/오산 행 수): projects3/361, panel_placeholders4/0, project_procurement_items9/0, project_production_plans3/0, project_production_plan_items18/0, material_receipts2/0, material_iqc_attempts1/0, panel_manufacturing_executions1/0, g2_daily_metrics246/0, g2_inventory_counts4/0, g2_targets17/0. pending_issues·panel_quality_inspection_attempts·panel_quality_reports·logistics_packing_units·logistics_batches·sales_settlements는 양쪽0. 모든 청주 업무가 사용 중이라고 확대하지 않는다.
- 오산 실제 저장처(청주/오산): osan_project_targets0/361, osan_project_target_steps0/2527, osan_progress_photos0/781, osan_stage_records0/814, osan_stage_issues0/15, osan_stage_work_requests0/10. 배포 코드 `OsanProjectStore`는 projects와 이 오산 대상/단계 표를 사용한다. `panel_placeholders`를 오산 실습 대상으로 안내한 것은 잘못된 기준이었다.
- 최근 시각 예: 청주 g2_daily_metrics updated 최대2026-09-28T23:01:33Z, 오산 projects updated 최대2026-09-29T00:53:43.686921Z, 오산 target_steps updated 최대2026-09-29T01:08:50.317726Z. 이는 저장 timestamp의 최대값으로 지속 유입 감시나 모든 데이터의 실업무 여부를 입증하지 않는다.
- OSAN-G2-001 조사 재개 결과: 오산 G2 전용 표는0행. 이름/title/key/code/number 중 G2 문구가 있는 projects11건은 모두 project_profile=Osan, 논리삭제0, 오산 생성 operation11건과 ProjectCreated event11건 존재. 생성 범위2026-09-10T10:09:55.175704Z~2026-09-18T08:00:51.130373Z. 청주는 같은 조건0건. 청주 G2 실적이 오산에 복제됐다는 증거는 발견하지 않았다. 해당 11개 프로젝트의 업무상 등록 이유·입력 경로(개별/엑셀)·내용 적절성은 미확인으로 OPEN 유지한다.
- 전체 projects profile은 청주 Cheongju3건(논리삭제1), 오산 Osan361건(논리삭제7). 물리 행 수와 화면의 활성 프로젝트 수를 혼동하지 않는다.
- 결과: 공통 테이블 구조에 사업부별 업무 데이터가 분리되는 현재 설계가 미사용 표의 직접 원인이다. 오산 전용 업무·공통 인증/공지/알림·기준정보와 청주용 표를 나누어 설명할 필요가 있다. 빈 표를 곧바로 영구 불필요/삭제 가능으로 판단하지 않으며 구조 정리나 migration 분리는 이번 조사에서 구현/승인하지 않았다.

## 사업부별 구조 정리 가능성 판단 — 2026-09-29

- 사용자 요청: 분리된 `emi_qms`와 `emi_qms_osan`이 같은 구조를 가질 필요가 있는지, 불필요한 표를 정리할 수 있는지 정확히 판단한다. 범위는 읽기 전용 조사와 설계 판단이다. 운영 DROP/ALTER/데이터 정정·구현·배포를 실행하지 않는다.
- 판정: **사업부별로 다른 구조를 운영할 수 있으며 정리할 실익이 있다. 현재 상태에서 빈 표를 일괄 삭제하는 것은 불가하다.** DB 간 구조 동일성은 Azure/PostgreSQL의 필수 조건이 아니라 현재 공통 migration과 공통 코드의 설계다. 기존 DB 안에서 가능하며 구조 분리 자체에 새 Azure 서버/DB 자원은 필요하지 않다. 전체 삭제 목록 확정 및 정리 후 동작 검증은 아직 수행하지 않았다.
- 근거 기준: 위에서 확인한 운영 revision/source `02028f2739af3da197a047008f448b3f3e95d230`을 임시 경로에 읽기용으로 추출했다. 현재 오래된 checkout을 운영 코드로 간주하지 않았다. 실제 오산 metadata는 public FK496개·view4개(의존 관계9개)·SQL/PLpgSQL 함수59개·사용자 trigger204개를 조회했다. public 외 사용자 테이블/뷰0개. 메타데이터 조회 전체 성공. runtime credential은 메모리/자식 stdin만 사용하고 읽기 전용·TLS 검증·기존 사용자 통로 보존 원칙을 유지했다.

### 구체적 분류와 발견 사항

| 범위 | 관측 및 판단 |
| --- | --- |
| 오산의 `g2_*` 4개 | 모두0행. 다른 표에서 이 그룹을 참조하는 FK0개, 오산 view 참조0개. 직접 C# 표 참조는 G2 전용 store에 한정되고 오산 capability에서 API 미허용. 우선 정리 후보. |
| 오산의 `busbar_*` 29개 | 업무 표27개0행, 설정2개 각1행. 설정 내용도 migration 초기값과 일치하는 것을 건수로 확인했다. 다른 표에서 이 그룹을 참조하는 FK0개, 오산 view 참조0개. API는 청주 전용이며 이카운트/게시 worker도 청주 target을 선택한다. 관련 함수·sequence까지 포함해 우선 정리 후보. |
| 오산 `projects`, `osan_*`, 사용자·권한·공지·알림·감사·유지보수 표 | 현재 기능과 이력 보존에 필요하다. 비어 있는 첨부/오류/이력 표도 있을 수 있으므로 COUNT0을 삭제 기준으로 삼지 않는다. |
| 오산의 청주 생산·조달·검사·물류·정산 표와 기준정보 | 업무상 미사용이 확인된 영역이지만 참조 연결을 분리한 다음 정리해야 한다. 이번 조사에서 이 전체 그룹의 최종 삭제 개수를 확정하지 않는다. |

- **확인된 우선 후보는33개이며 즉시 삭제 승인 목록은 아니다.** FK/view와 코드 참조를 검토한 후보 판정이다. 제거 후 실행 시험은 아직 하지 않았다. 공통 감사 함수 안의 G2 등 이름은 문자열 비교용 목록이며 해당 표를 직접 조회하는 SQL 의존성과 구분했다.
- **오산 알림의 청주 표 의존:** `WorkflowStore.GetNotificationsAsync`/상세 조회가 `work_items`, `workflow_stages`를 LEFT JOIN한다(운영 source `Workflow/WorkflowStore.cs:904,933-934,996-997`). `NotificationDeliveryStore`의 발송 대상 읽기 및 오산에서도 호출되는 WebPush 생성도 두 표 또는 `work_items`를 참조한다(`Notifications/NotificationDeliveryStore.cs:541-542,2113,2226`). 따라서 알림의 연결 값이 NULL이어도 표 자체가 없으면 쿼리가 실패한다. 실제 오산 notifications.work_item_id·generated_by_event_id 및 notification_deliveries.work_item_id의 비NULL 건수는 모두0이다. FK도 notifications→work_items/project_workflow_events, notification_deliveries→work_items로 남아 있다.
- **오산 프로젝트의 검사 양식 연결:** 실제 projects361건 모두 같은 기본 `lqc_template_version_id`를 갖는다. 해당 열은 NOT NULL/고정 DEFAULT이며 `panel_quality_template_versions`에 FK가 있다. `lqc_operational_snapshot`도 NOT NULL/default true다. migration0070에서 설정됐고 `OsanProjectStore.InsertProjectAsync:1001`는 이 열을 지정하지 않아 기본값을 사용한다. 검사 양식이 다시 `production_product_types`/`workflow_stages`를 참조하므로 빈 생산 업무 표만 보고 관련 기준정보까지 삭제할 수 없다. 오산에서 검사 양식 연결과 전용 보호 trigger/열을 제거하거나 선택값으로 바꾸는 별도 보정이 필요하다. 오산 프로젝트 원문이나 id는 기록하지 않았다.
- **청주에도 역방향 의존:** 공통 WebPush SQL은 청주에서도 `osan_notification_events`를 LEFT JOIN하고 `osan_notification_global_preferences`를 참조한다. 또한 실제 청주 qms_users에 `osan_assign_existing_customers_to_new_user` trigger가 활성화되어 있다(1개). 이 함수는 새 사용자 등록 때 osan_customer_assignment_versions/osan_customer_assignments/osan_customers를 사용한다. 따라서 청주의 `osan_*`를 일괄 삭제하면 알림 또는 사용자 등록이 깨질 수 있다. 기능별 SQL 분기와 이 trigger 제거를 먼저 설계해야 한다.

### 필요한 변경과 완료 경계

1. 공통 유지 대상과 사업부 전용 대상을 명시한다. 오산33개 후보부터 작은 범위로 진행하고, 연결이 많은 생산/검사 영역 및 청주 역방향 정리는 별도 단계로 확장한다. 사용자·권한 같은 공유 표 안의 기준정보 행 정리는 표 삭제와 다른 판단이며 자동으로 포함하지 않는다.
2. 공통 알림 SQL의 사업부 분기, projects의 불필요한 기본값/필수값/FK/trigger를 보정한다. 프로젝트 등록·수정·알림이 정리된 구조로 작동하게 만든다.
3. 이미 적용된0001~0130 파일과 원장은 보존한다. 새 migration에 사업부 identity를 확인하는 조건을 넣거나 공통/청주/오산 실행 목록을 명시적으로 나누고, runner·catalog·ledger 검사·health schema 검사를 함께 일치시킨다. 같은 migration 번호를 유지하면서 사업부별 SQL만 다르게 실행하는 방식도 가능하며 전체 history 재작성은 필요하지 않다. 현재 catalog는 번호 연속성을 검사하고 inspector는 공통 원장 전체를 기대하므로 과거 파일을 단순 삭제/이동하면 안 된다. 현재 health 검사는 모든 업무 표의 존재를 확인하지 않아 health 성공만으로 정리 성공을 판정할 수 없다.
4. 합성 데이터를 쓰는 전용 disposable DB에서 기존130단계→정리 upgrade와 새 DB 설치를 검증한다. 사업부별 사용자 등록·프로젝트 등록/수정·진행/사진·알림 조회/발송(fake provider)·감사 기록·권한 거부·잘못된 DB 거부 및 다음 migration을 확인한다. 실제 삭제 단계에서는 대상/잔존 행 조건, 복구 방법, 호환 앱 배포 순서를 확정하고 그 구체적 운영 변경 범위의 승인을 받는다.

- 정정할 오해: 이미 적용된 migration은 runner가 건너뛰므로 표를 지웠다고 재시작 때 무조건 다시 만들어지는 것은 아니다. 문제는 현재 실행 SQL과 앞으로의 공통 migration이 같은 표의 존재를 전제로 한다는 점이다.
- 외부 기준: [PostgreSQL dependency tracking](https://www.postgresql.org/docs/current/ddl-depend.html), [DROP TABLE](https://www.postgresql.org/docs/current/sql-droptable.html). CASCADE는 의존 view/FK 등을 제거할 수 있고 문자열 본문 함수의 내부 SQL 의존성을 모두 추적하지 않는다. 삭제 명령이 성공했다는 사실만으로 앱 정상 동작을 입증할 수 없다. 운영에서 DROP을 실행해 의존성을 시험하지 않았다.
- 현재 완료: 읽기 전용 가능성 판단·구체적 장애 지점 확인·우선 후보33개 식별. 미완료: 최종 전체 삭제 manifest, 코드/DDL 구현, disposable 검증, 운영 승인/적용. DB 내용/구조·앱 배포·유료 자원 변경 없음. 근거만 기존 Task에 기록한다.

## 전체 테이블 소유 확정안 — 2026-09-29

- 최신 사용자 원칙: 같은 서버를 사용하더라도 청주 frontend/backend가 오산 DB를 조회·수정하는 등 어떠한 업무/관리 작업도 하지 않고, 오산도 대칭으로 분리한다. 사업부별 독립 schema를 사용한다. 현재 단계는 표 확정부터이며 구현·운영 정리로 확대하지 않는다.
- [전체209개 표 소유 확정안](azure-deploy-001-change-031-table-ownership.md)에 표별 소유·용도·COUNT 스냅샷·제거 전 보정·Directory 및 실행 경계를 작성했다. 목표는 공통 이름29개(각 DB에 독립 존재), 청주 소유153개, 오산 소유27개다. 청주182개 유지/27개 제거 대상, 오산56개 유지·보존/153개 제거 대상. 오산56개 중2개는 현재 비활성인 개인 알림 설정11행 보존용이다. 최종 삭제 실행 목록이나 사용자 검수 완료로 표현하지 않는다.
- 신규 확인: 운영 revision/image는 앞선 조사와 동일. 현재 단일 backend에 두 업무 runtime connection이 모두 있고, 통합 사용자 관리의 양 DB 조회·프로필 변경, 전체 target health/worker/점검 잠금 경로가 있다. 일반 업무 요청의 선택 DB 경계와 프로그램 자체의 접속 능력 분리는 다른 문제다. 현재 데이터 유출이 발생했다는 조사 결과는 아니다.
- 소유 보정: `notice_popup_receipts`, `notice_setting_events`는 이름에 osan이 없어도 오산 전용 endpoint에서만 사용하므로 청주 제거 대상에 포함한다. `notice_reads`는 공통 유지. generic user_notification_preferences 계열은 오산 화면에서 제외되어도 API는 아직 허용되어 있으므로 오산에서 제거 전에 API 분기/종료가 필요하다.
- 보존 판단: migration0104가 개인 알림 제어 복귀를 위해 기존 설정을 보존한다고 명시한다. `osan_notification_preference_profiles`2행·`osan_notification_preferences`9행을 단순 미사용 데이터로 삭제하지 않는다. `busbar_ecount_employees`는0행·배포 C# 사용처 미발견으로 청주 소유 유지하되 청주 내부 후속 정리 항목임을 명시했다.
- 독립 검토: `review_table_ownership`, 요청 GPT-6-astra/high, 실제 관측 모델 NOT_REPORTED. 배포 소스·제공 metadata를 검토했고 공지2개 소유 보정과 현재 의존 조건을 반영한 목표안29/153/27을 타당하다고 판단했다. DB/삭제/접속 차단 실행 검증은 미수행이다.
- 완료: 표209개 누락/중복/집합·집계 직접 검산, 소유안·현재 접속 구조 조사·독립 검토. 다음은 사용자 표 범위 확정 후 세부 schema/실행 경계 설계다. 운영 DB·배포·유료 자원·사용자 터널 변경 없음. 현행 사용자 승인보다 오래된 통합 구조의 절차를 우선하지 않으며, 기존 총괄 접근 권한은 별도 공통 관리 책임/사업부 전환으로 보존하는 안을 제시했다.

## 표 소유 확정과 열·연결 설계 — 2026-09-29

- 사용자 “좋아.”에 따라 앞선 청주182개·오산56개 유지/보존 표 범위를 확정으로 기록했다. 다음 열·연결 설계를 진행하며 아직 제시하지 않은 열별 제거안의 확정이나 운영 삭제·배포 승인으로 확대하지 않는다.
- [같은 상세 명세 §7~9](azure-deploy-001-change-031-table-ownership.md#7-열-정리-검토안--2026-09-29)에 projects 전체43개 열의 판단과 연결 보정을 추가했다. 청주36개·오산19개 유지안이다. 오산은 대표 번호/제목·고객·납기·품명·수량·HOLD·상태·생성/수정/논리삭제 기록을 보존하며 청주 전용·중복 열24개를 정리한다. 청주는 오산6개 열과 profile만 제거하는 안이다.
- 추가 운영 조회는 기존 사용자 통로·TLS 검증·읽기 전용/짧은 timeout을 유지했다. 실제 업무 값 대신 NULL/서로 다른 값·중복 열 불일치·profile/status 개수와 제약/인덱스/trigger metadata만 확인했다. 청주3행·오산361행, 오산 Active305/Completed56, 별칭 불일치0, 코드361행/서로 다른 값337개다. 코드 중복 허용은 보존한다. 논리삭제7행과 실제 오산 단계 완료 기록도 보존한다. 집계 스냅샷이므로 실제 적용 직전 재확인한다.
- 오산 알림은 notifications14→12, notification_deliveries46→45로 청주 연결3개만 정리한다. 실제 오산 메일이 쓰는 manual_payload_json과 발송/재시도/기기 보호 계약은 유지한다. notice_posts17개·qms_users15개 열은 양쪽 유지하며 반대 사업부 부수 동작만 분리한다. 다른 공통 표의 전면 열 최소화는 포함하지 않는다.
- 독립 검토: 작성과 분리된 기존 review_table_ownership 재사용, 요청 GPT-6-astra/high·관측 NOT_REPORTED. 배포 소스와 제공 집계를 기준으로 검토했고 양쪽 권한 조회·item 응답 호환·코드/제목 인덱스 규칙·공지 열·메일 snapshot·BEFORE INSERT 발송 보호·단계 완료 기록 보존 조건을 반영했다. 실행 검증은 미수행이다.
- 현재 완료는 설계·문서화다. 남은 일은 실행 프로그램/credential/권한/공통 관리 배포 단위 상세화, 코드·추가 migration 구현, 합성 DB 검증, 구체적인 운영 변경 결정과 적용이다. 운영 DB/앱·유료 자원·사용자 터널 변경 없음. 원격 반영·배포도 하지 않았다.

## 실행 프로그램·접속 권한 분리 설계 — 2026-09-29

- 사용자는 앞선 열 정리안과 다음 접속/실행 설계 안내 뒤 “좋아.”로 동의했다. [실행 경계 설계](azure-deploy-001-change-031-runtime-boundary.md)에 현재 상태, 권고 구성, 접속/라우팅, 공통관리 이전, 운영 전환과 검증 범위를 정리했다. 구현·새 자원·운영 삭제·배포는 실행하지 않는다.
- 현재 Azure 앱은 backend1 vCPU/2 GiB, frontend0.25/0.5, ClamAV2/4이며 모두 같은 Consumption environment다. backend revision/image는 앞선 운영 기준과 동일하다. backend managed identity가 양 업무 및 Directory runtime 비밀값3개에 개별 Key Vault 읽기 권한을 갖는다. frontend에는 DB 비밀값 권한이 발견되지 않았다. Manual migration/bootstrap/backfill/maintenance job의 다중 target 설정도 확인했다.
- 실제 DB catalog 확인: pms_app/pms_osan_app/pms_directory_app 모두 자기 DB CONNECT만 true이며 상대2개는 false다. 세 역할에 superuser/CREATEROLE/CREATEDB/BYPASSRLS·직접 역할 상속·DB owner membership은 없다. Directory runtime은 SECURITY DEFINER 등록/관리 함수 EXECUTE가 있어 단순 조회 역할이 아니다. 추가 조회는 TLS·read-only·timeout을 유지했고 DB 원문/비밀값을 출력하지 않았다. 첫 조회는 Directory identity의 business_unit_code가 NULL인 정의를 검사 스크립트가 잘못 가정해 실패했으며 실제 코드에 맞춰 보정한 두 번째 조회는 성공했다. 실제 반대 DB 로그인 거부 시험은 미실행이다.
- 권고안: 기존 backend를 청주 전용으로 전환, 오산 및 공통 로그인/관리 Container App2개 추가. 기존 DB서버·VNet·environment·공통 입구·ClamAV 재사용. 업무 앱은 자기 DB + 사업부별 Directory reader만, 공통관리는 Directory 직접 접근 + 제한된 local 사용자관리 API만 허용한다. 비용은 Consumption 실행량에 따라 달라지며 무료나 현재와 동일 금액을 보장하지 않는다.
- 새 발견으로 표 범위 보완 제안: 서버 간 관리 요청의 commit 후 응답 유실/중복/지연을 처리하기 위해 각 업무 DB에 local 사용자관리 적용 기록표1개씩 추가. 수용하면182/56→183/57이지만 현재 확정 수로 바꾸지 않았다. Directory는 기존 operation 원장을 재사용한다. 프로필 변경과 local 기록을 같은 transaction에 묶고, 완료 확인 전 새 권한 Publish를 금지하는 조건을 명시했다.
- 독립 검토: 기존 review_table_ownership 재사용(요청 GPT-6-astra/high, 관측 NOT_REPORTED), 이번 코드/요구사항 기준. 로그인 쓰기 이전·snapshot 관리 조회·내부 인증·Directory Publish 순서·local 재시도/버전 보호·화면 전환 보존 조건을 반영했다. 실제 변경/거부/분산 실패/부하 시험은 아직 수행하지 않았다.
- 완료: 읽기 전용 실제 구성 확인과 구체적 실행 분리 설계. 운영 DB·앱·권한·자원·사용자 터널 변경, 원격 반영·배포 없음. 남은 범위는 추가 기록표 포함 여부의 확정, 최신 운영 코드 기반 구현·합성 검증·용량/비용 산정 및 구체적인 운영 전환이다.

## 기존 Container App 유지로 재검토 — 2026-09-29

- 최신 사용자 조건: 비용 때문에 Container App2개 추가안을 채택하지 않고 기존 앱을 그대로 쓰는 방향으로 재검토했다. [실행 설계 §10](azure-deploy-001-change-031-runtime-boundary.md#10-기존-앱-수용량을-기준으로-한-재검토--2026-09-29)의 A를 현재 설계로 삼고, 이전 §1~9는 과거 제안으로 표시했다. 앱/용량 증가나 frontend/ClamAV로의 무단 이동은 하지 않는다.
- A 후보는 backend1개·실행 컨테이너1개·.NET 프로세스1개에서 고정 업무 모듈/DB 접근 객체/경로/worker/catalog를 나누는 방식이다. DB 계정별 자기 DB CONNECT 경계는 유지되지만 프로세스는 양쪽 credential을 갖는다. 앞선 credential 격리와 같다고 표현하지 않는다. B는 같은 앱 안의 다중 컨테이너로서 프로세스 경계는 생기지만 app identity/secret·기존 청주 Blob 인증·추가 런타임 용량·내부 호출을 더 검토해야 하는 후보로 남겼다.
- 사용자에게 업무 경로/DB 구조 분리면 충분한지, 접속 정보까지 실행 프로그램별로 분리하는 것도 필수인지 선택을 요청했고 업무 경로/DB 구조 분리로 충분하다는 답변을 받았다. 아래 선택 기록에 따라 A로 확정했으며 기존 표/열 조사와 자원 유지 조건도 유효하다.
- A에서는 기존182/56 표 범위를 유지하는 최소안이 가능하므로 local 적용원장 추가183/57을 필수로 요구하지 않는다. local commit 후 Directory Publish 실패는 현행에도 존재하며, 동일 작업의 동시 재시도/과거 요청은 기존 복구 계약 보존 차원에서 검증해야 한다.
- 독립 검토: 기존 review_table_ownership, 요청 GPT-6-astra/high·관측 NOT_REPORTED. 배포 source를 기준으로 A의 고정 경계·보장 한계·원장 추가 판단을 검토했다. parent는 Microsoft 공식 다중 컨테이너/identity lifecycle/비밀값 문서를 확인했다. 운영 DB·Azure·코드 변경 없음, 플랫폼 격리/부하 시험 미수행.
- 이어서 사용자 답변: “업무 처리와 DB 구조를 분리하면 됨 — 기존 백엔드 1개 유지.” 동일 프로세스가 양쪽 credential을 보유한다는 설명을 들은 후 A를 선택했다. 현재 방향은 기존 앱/프로세스/용량 기준을 유지한 업무 모듈·고정 DB 연결·schema 분리다. 표 소유 문서의 실행 원칙도 이에 맞춰 갱신했다. B/Container App 추가/신규 내부 HTTP 관리 호출과 이를 이유로 한183/57 원장 추가는 이번 범위에서 제외하고182/56을 유지한다. 구현·실행 시험·운영 적용을 완료했다고 표현하지 않는다.
- 문서 검증: 전체209개 표 소유 목록과 projects43개 열 목록은 이전 확정/설계 기록과 동일하고 각 목록의 누락·중복이 없음을 확인했다. 분리 원칙·사용자관리·health·migration·프로필 판별 문구를 같은 프로세스의 업무 모듈 분리로 맞췄고 diff 형식 검사를 통과했다. 앱/DB 실행 시험은 수행하지 않았다.

## 업무 모듈·구조 분리 구현 시작 — 2026-09-29

- 사용자 “좋아. 시작해”에 따라 §10 A의 로컬 구현·합성 검증을 시작한다. 기존 앱/백엔드 프로세스/용량 유지, 청주182/오산56 및 projects36/19 목표, 사용자·알림·감사·사업부 전환 계약 보존이 범위다. 운영 적용·실데이터 삭제·push/PR/merge·유료 자원 생성은 실행 범위가 아니다.
- 기준은 최신 origin/main이자 확인한 운영 source `02028f2739af3da197a047008f448b3f3e95d230`. 기존 checkout의 다른 WIP를 보존하기 위해 `/private/tmp/emi-business-schema-separation`, `codex/business-schema-separation`에서 작업한다. 현행 v2 AGENTS/모델/검증/완료 정책은 기존 checkout과 동일함을 확인했다.
- 구현은 고정 API 경로/DB 객체/사업부별 작업 범위, 제거 표·열에 대한 SQL 의존 해제, 사업부별 추가 migration/catalog/권한 검사, 합성 DB의 업그레이드·새 설치 및 반대 DB 접근 반례를 포함한다. 구현 후 독립 검토를 수행한다.

### 로컬 구현 중간 상태 — 2026-09-29

- 구현 상태: WIP. 고정 `/cheongju/api`, `/osan/api`, `/access/api` 경로와 요청/worker별 고정 DB 객체를 추가했다. 경로와 사업부 선택값 충돌은 거부한다. 청주 전용 store는 청주 객체, 오산 전용 store는 오산 객체를 받고 공통 store는 한 번 정해진 요청 대상만 사용한다. Directory 사용자 관리는 같은 프로세스의 제한된 local 관리 인터페이스로 분리하며 동일 사용자 관리 요청은 Directory 연결의 advisory lock으로 직렬화한다. 전체 프로세스는 승인한 대로 양쪽 접속 정보를 보유한다.
- SQL 의존 보정: 오산 프로젝트/권한 응답에서 제거 예정 청주 열과 profile 참조를 없앴다. 오산 알림·발송·감사 SQL의 청주 표/열 의존, 청주 작업 알림/API·관리 worker 및 개발 샘플 입력을 분리했다. 오산 비활성 개인 알림 설정11행 보존, 실제 메일 payload와 단계 완료/사진 계약은 유지하는 구현이다. 실제 축소 DB에 대한 실행 검증은 아직 하지 못했다.
- 사업부별 migration catalog/원장/권한 검사와 명시 대상 bootstrap/migration/점검 명령을 추가했다. multi-DB 무대상 실행과 startup 자동 migration을 차단한다. 공통 health는 Directory와 사업부별 상태를 표시하고, 한 사업부 DB 문제만으로 준비된 다른 사업부까지 전체 차단하지 않도록 보정했다.
- 배포·시험 호출부도 명시 대상을 전달한다. 기존 Job을 대상별로 순차 실행하는 코드이며 Container App/Job을 새로 만들지 않는다. 일부 사업부만 안내/점검 활성화/완료된 후 실패해도 각 대상 실패 표시를 독립적으로 시도하며 Announced→Failed와 Failed 재요청 no-op을 지원한다. DB 관리 실행 전 두 앱의 active revision과 replica가 모두 종료되고 사전에 고정한 읽기 전용 transaction/provider drain 진단이 통과해야 한다. DB 관리 실행이 시작됐거나 시작 응답이 불명확하면 앱 중지 상태를 유지하고 수동 보정이 필요하도록 했다. 그 이전 실패만 기존 revision을 복원한다. 이 최초 구조 전환은 조회·로그인도 중단하는 적용 계획이므로 운영 실행 시 대상·시간·중단 범위 승인을 별도로 확정해야 하며 현재 실행 승인이 아니다. 실제 배포 명령은 실행하지 않았다. 최초 전환의 preparation/previous image가 새 대상 선택 CLI를 지원하는지 운영 계획에서 별도 확인해야 한다. 사전 읽기 전용 drain 진단 파일과 현행 CI/workflow 연결·운영 안내는 아직 준비하지 않았으며, 실제 적용 시 prerequisite이다. 현재 CI를 실행하면 이 필수 설정 없이 진행하지 못하도록 차단된다.
- 자동 승인 검토 차단: 확정 표/열을 제거하는 추가 SQL 파일 작성이 구체적인 파괴 범위·환경 승인 부족으로 거부됐다. 데이터를 archive로 옮기는 대안도 광범위한 지속 schema 변경이라는 같은 이유로 거부됐으며 이후 동일 행동을 재시도하지 않았다. 청주27표·projects7열, 오산153표·projects24열·알림3열에 대한 변경 파일 작성과 이번 실행 소유 합성 disposable DB에서만 검증하는 승인을 사용자에게 요청한 상태다. 운영 DB 적용 승인은 요청하거나 받은 것으로 간주하지 않는다.
- 따라서 `database/business-migrations`의 실제 SQL 파일은 없으며, Docker 이미지 구성과 runtime/catalog는 아직 배포 가능한 완성 상태가 아니다. 목표182/56표·36/19열, fresh/upgrade, 실제 반대 DB 로그인 거부, 전체 사용자 관리 동시/지연 재시도, 축소 schema 주요 CRUD/메일/출력 검증은 남아 있다. 기존 다중 DB 통합 fixture의 무대상 bootstrap/migration 및 기존 API URL도 새 명시 대상/경로에 맞춰 보정해야 한다.
- 자동 검증: backend 경계/catalog/SQL/worker 집중27개와 권한/health/audit57개 통과; frontend 경로/API20개 및 typecheck 통과. 추가 배포 보안/config 검사46개 중43개 통과,3개는 소유 시험용 PostgreSQL harness 미기동으로 선행조건 오류이며 통과로 기록하지 않는다. 이3개는 실제 DB 호출 전 중단됐다. release 모의51시나리오 및 최초점검 모의16시나리오 통과. 실제 production 전이 판정의 단위7개도 통과했다. stateful mock은 오산 prepare/activate/complete 중간 실패, 모든 replica 종료 전 DB 시작 거부, drain 실패, DB 경계 전 복구와 경계 후 중지 유지를 확인한다. 일반 release와 최초 rollout은 시작 응답 유실·Running 지속·상태 조회 불명확·실패 정리 작업의 Running 시 후속 maintenance job도 겹쳐 보내지 않고 앱 중지/수동 확인을 유지하며 늦은 완료 반례를 포함한다. 스크립트 문법과 diff 형식 검사 통과. 합성 DB 통합 검증·전체 회귀·Docker 이미지 빌드·운영 검증은 미실행이다.
- 증거(이번 실행 로컬 로그): `/private/tmp/business-schema-unit-tests.log`, `business-schema-api-tests.log`, `business-schema-security-tests.log`, `business-schema-frontend-tests.log`, `business-schema-frontend-types.log`, `business-schema-release-tests.log`, `business-schema-bootstrap-tests.log`. 범용 DB 생성/삭제·실제 provider·실데이터/비밀값 출력 없이 실행했다.
- 독립 검토: `review_db_tunnel`은 작성하지 않은 migration/bootstrap/catalog 범위를 검토하고 전체 대상 bootstrap P1을 지적했다. 선택 대상만의 검증·역할 갱신으로 보정 후 재검토에서 해당 P1 해소·추가 P0/P1/P2 없음을 확인했다. `business_schema_migrations`는 자신이 작성한 catalog를 제외한 업무 경계/SQL/Program을 검토했다. 감사 middleware helper, 공통 `/me`의 선택 헤더 경계, 오산 개발 샘플 SQL, 점검 대상 누락을 보정했으며 재검토에서 본체 보정은 확인했다. 후속 검토의 호출부 지적은 명시 대상 실행으로, 추가 구 앱 잔류/Announced 실패 전이/최초 전환의 부분 실패 P1 세 건은 앱 종료·drain·양쪽 실패 처리로 보정했다. 전이/최초 rollout 보정은 business_schema_migrations가 구현했고, 작성하지 않은 review_db_tunnel이 해당 범위와 일반 release를 재검토했고 종료 불명확 maintenance 실행과 fail cleanup의 경합 P1을 추가 지적했다. 일반 release는 불명확 시 양 앱 중지/수동 확인·후속 maintenance 실행 금지로 보정했고 좁은 재검토에서 P1 해소와 새 P0/P1/P2 없음을 확인했다. 최초 rollout도 같은 불명확/확정 실패 구분 및 후속 fail 중단을 적용했고, review_db_tunnel의 좁은 재검토에서 기존 P1 해소·잔여 제품 P1/P2 없음이 확인됐다. mock의 잘못된 상태 전이를 null 대신 실제 CLI의 terminal Failed로 표현하라는 참고 보완도 반영했다. 이는 검토한 코드 범위의 판정이며 전체 구현/운영 적용 완료가 아니다. 요청 모델은 각 agent 기존 설정 유지, 실제 모델은 NOT_REPORTED. 두 검토자는 읽기 전용으로 코드·제공 증거를 확인했으며 DB 실행을 대신하지 않는다.
- Git/환경: `codex/business-schema-separation`의 미커밋 변경으로 보존한다. 원래 checkout과 무관 WIP·실행 중 환경·pgAdmin 터널은 유지했다. local commit/push/PR/merge/배포·Azure 자원/권한·운영 데이터 변경은 없다. 구현 완료나 사용자 검수 완료로 표시하지 않는다.

### 시험용 DB 적용 명시 승인과 재개 — 2026-09-29

- 사용자 “승인”은 직전 요청의 **확정 표·열 변경 파일 작성 + 이번 실행이 새로 만든 합성 시험용 DB에서만 적용·검증**을 승인한다. 대상은 청주27표/projects7열, 오산153표/projects24열/알림3열 제거이며 목표182/56표 및 projects36/19열이다. 이 명시 승인으로 이전 자동 승인 검토의 범위/환경 확인 요구를 해소하고 같은 승인 범위에서 작업을 재개한다.
- 운영/Persistent UAT 데이터·DB·권한 변경, Azure 자원 생성/변경, 실제 provider, push/PR/merge/배포는 포함하지 않는다. 새 시험 환경은 저장소 e2e 소유 검사를 통과하는 별도 PostgreSQL container/tmpfs와 실행별 synthetic DB/role만 사용하며 종료 시 이번 실행 소유 자원만 정리한다. 기존 main migration0001~0130은 변경하지 않고 사업부별0131을 추가한다.
- 남은 검증: 실제 fresh/upgrade schema·보존 데이터·잘못된 대상 거부·사업부 CRUD/권한/알림·동시 관리 및 관련 자동 검증. 결과/잔여사항은 이 절에 갱신한다.

### 합성 DB 구조 분리 검증 진행 — 2026-09-29

- 위 명시 승인에 따라 `database/business-migrations/cheongju/0131_cheongju_business_schema.sql`과 `osan/0131_osan_business_schema.sql`을 추가했다. common0001~0130 파일은 변경하지 않았다. 명시적인 대상별 세션 승인값, DB identity, 예상 원본 표·열 집합, 제거 대상의 빈 상태 또는 정확한 초기값을 확인한 뒤 RESTRICT로 처리하며, runner의 파일+원장 단일 트랜잭션을 사용한다. 실제 운영/Persistent UAT에는 실행하지 않았다.
- 새 설치에서 청주182/오산56표와 projects36/19열을 확정 목록과 독립된 테스트 기대값으로 대조했다. 실제 반대 사업부 runtime 계정의 DB 로그인 거부를 확인했다. 기존0130→0131 전환에서는 유지 대상 표의 JSON 행을 전후 대조해 프로젝트·중복 코드·논리삭제 완료 상태·개인 알림 설정11행·기존 비NULL 메일 payload를 보존했다. 승인 누락·다른 identity·예상 밖 표/초기값/NULL 안내문·늦은 FK 의존 오류에서 중단하고209표/43열/130원장으로 되돌아오는 반례가 통과했다.
- 축소 구조에서 프로젝트 등록·단계 완료·알림 목록/상세/읽음·메일 payload/claim·감사·고객 관리·알림 설정·수신자별 엑셀 출력·사진 API·40MiB 업로드 보안 및 사업부/Directory 권한·동시 요청 경계를 확인했다. 추가된 fixed scope에 맞춰 기존 일부 시험의 공통 구조/무대상 실행 가정을 보정했다. 기존 단일 DB 오산 회귀 fixture와 실제 UI 연동 검증은 후속 실행 중이다.
- 독립 검토 `review_db_tunnel`은 SQL 초기값 guard 누락, nullable guidance/description의 NOT IN UNKNOWN 우회 P1을 지적했다. canonical 값/관계와 NULL-safe 보정 및 실제 변조 반례로 해결했고 최종 SQL P1/P2 없음. 병렬 WebPush 시험의 공유 List는 ConcurrentQueue로 보정했고, upgrade 메일 fixture의 고객 배정 누락도 수정해 비어 있지 않은 payload 보존을 실제 확인했다. 검토자는 SQL/부모 신규 시험을 작성하지 않았고 자신이 작성한 store/fixture 원본·이미지 harness는 판정에서 제외했다. 이미지/E2E harness 보정은 작성하지 않은 `business_schema_migrations`가 별도로 검토했다.
- 실제 로컬 production 이미지 시험은 common/directory/business 파일 동등성, 실제 packaged CLI의 D/C/O 명시 bootstrap/migrate, fresh/upgrade/replay 및182/56표·36/19열·알림/발송 열 수를 통과했다. 승인 누락 시 미처리 PostgresException 이후 시험 프로세스가 남아 있던 실행은 소유 PID/부모·container label을 확인해 중단·정리했다. `Program`의 migration CLI가 SQL 예외 본문 없이 허용된 거부 코드 또는 generic code/SQLSTATE만 기록하고 exit1로 정상 종료하도록 보정한 뒤 이미지 시험은 모두 통과했다. 시험용 container/image/DB/network/volume 잔존0을 harness가 확인했다.
- 확인 로그: `/private/tmp/business-schema-resume-build.log`(경고/오류0), `business-schema-integration.log`(fresh/upgrade2통과), `business-schema-boundary-integration.log`(초기13통과·3fixture실패는후속보정), `business-schema-guard-and-regression.log`(5통과·주입fixture1실패는후속보정), `business-schema-final-schema-tests.log`(fresh/WebPush통과·보존/guardfixture2실패는후속보정), **`business-schema-preservation-final.log`(보존/guard2통과)**, **`business-schema-image-final.log`(전체이미지검증통과·정리0)**. 중간 실패 로그를 최종 통과로 덮어 설명하지 않는다. 초기 UI 시험은 필요한Playwright실행파일 부재로 화면 실행 전에 실패해 임시경로 `/private/tmp/emi-schema-playwright`에 프로젝트 요구버전을 준비했다.

### 운영 배포 연결 범위 차단 — 2026-09-29

- 운영 연결의 남은 공백을 읽기 전용으로 확인했다. 현재 release 스크립트는 신뢰된 `AZURE_RELEASE_DRAIN_CHECK_FILE`을 요구하지만 workflow가 제공하지 않아 migration release가 먼저 차단된다. 또한 SQL0131의 세션 승인값을 workflow에서 해당 대상의 일회성 migration 실행으로 전달하는 연결도 저장소에 없다. 실제 Azure secret의 내용은 이 확인에서 읽지 않았다.
- 구체적인 다음 변경안은 **새 Container App 없이 기존 migration Job 재사용**이다. 새 읽기 전용 CLI로 D/C/O별 DB identity·기존 client session·사업부 점검 상태·미완료/결과불명확 발송을 확인하고, 모두 확정 성공한 뒤에만 migration으로 진행한다. 기본false인 구조 정리 승인 입력을 workflow에 두고 선택된C/O0131 트랜잭션에만 승인값을 설정한다. 기존 secret·DB role·job template에 영구 승인값을 저장하지 않는다. 운영 실행과 점검시간·복구계획 승인은 별도다.
- 위 **로컬 운영 연결 코드 변경**(Program/runner/workflow patch)이 자동 승인 검토에서 거부됐다. stated reason은 이번 명시 승인이 disposable DB 구현/검증에 한정되어 있고 production migration 실행·drain CLI·workflow 승인 입력은 범위 밖이라는 것이다. 거부된 patch는 적용되지 않았고 동일 행동을 우회/재시도하지 않았다. 통보 전 다른 agent가 작성한 미연결 `DeploymentDrainChecker` 초안1개도 작업트리에서 제거했다. 이 단계는 별도 명시 승인 전까지 미완료다.
- 현재 Azure 자원·운영 DB·실제 provider 변경, push/PR/merge/배포 없음. 기존 backend1개/비용 자원 기준을 유지하며 임시 worktree의 WIP 상태다. 전체 배포 준비 완료로 표시하지 않는다.

### 이번 승인 범위의 검증 결과 — 2026-09-29

- 청주182표/projects36열, 오산56표/projects19열의 별도 구조와 기존 backend 한 프로세스의 고정 업무 경로를 합성 환경에서 검증했다. 실제 Azure/운영/Persistent UAT DB에는 적용하지 않았다. 보존/거부/rollback 및 packaged CLI 검증 결과는 위 절의 최종 로그를 따른다.
- 기존 오산 업무 회귀44개는 최초41통과/3실패였다. Directory가 준비되지 않은 반려 fixture를 명시적 DIRECTORY+OSAN 준비로 보정하고, legacy 설정에서 typed Osan 접근을 허용하던 guard fixture를 현재 fail-closed 계약에 맞췄다. 읽기 전용·actor 누락의 DB 조회 전 거부 기대값은 유지했다. 실패3개만 재실행해3통과했으며 청주 DB는 해당 fixture에서 준비/사용하지 않는다. 증거: `/private/tmp/business-schema-osan-regression.log`, `business-schema-osan-corrections.log`.
- 실제 브라우저 오산 프로젝트 등록→목록→상세→진행과 청주 데이터 불변 검사1통과, 실제 사용자 승인→총괄 지정→탭별 사업부 유지/초기화1통과. 각각 `/private/tmp/business-schema-osan-full-stack-final.log`, `business-schema-access-full-stack-final.log`. 두 harness 모두 실행 소유 DB·역할·프로세스·Compose 정리를 확인했다.
- 사용자 관리 E2E에서 선택된 사업부의 실행 모드 요청까지 공통 `/access`로 보내면 다중 소속 총괄이 로컬 profile 없는 인증 문맥이 되어403으로 저장 UI가 차단됨을 재현했다. `runtime-mode`도 `/me`처럼 선택 후에는 고정 C/O 주소를 쓰도록 보정했다. 기본 인증 정책·저장 차단은 유지하며, common 사용자 관리 자체는 `/access`를 유지한다. 보정 후 실제 E2E가 통과했다.
- frontend의 옛 API 주소를 직접 비교하는 화면 fixture19개를 실제 고정 주소로 갱신했다. 로그인/사업부 접근63통과(독립 agent 실행), 청주 화면13개 suite64통과(`/private/tmp/business-schema-frontend-pages.log`), App/내비/G2/오산112개 중 최초110통과/2실패 후 오산13개만 재검증13통과(`business-schema-frontend-shell-regression.log`, `business-schema-osan-ui-unit-final.log`). 오산 마지막 case를 해당 suite의 초기화/정리 안으로 옮겼고 첫 목록 비동기 대기 상한을1초→5초로 맞췄다. 업무 결과 기대값은 제거하지 않았다. 고정 경로 단위3통과, 최종 TypeScript 검사와 diff 형식 검사 통과. jsdom의 scrollTo 미지원 메시지는 실제 브라우저 검증과 구분한다.
- 구현·합성 검증과 운영 배포 준비를 구분한다. 전체 최종 회귀·사용자 검수·운영 데이터 사전 점검·운영 구조 전환은 미실행이다. 운영 연결 코드의 남은 작업은 위 거부 기록의 구체적인 로컬 변경안이며, 같은 행동을 재시도하지 않았다. 브랜치 `codex/business-schema-separation`, worktree `/private/tmp/emi-business-schema-separation`의 미커밋 변경으로 보존한다. 원래 checkout·사용자 WIP·pgAdmin 터널은 변경하지 않았다.
- 최종 독립 재검토: `review_db_tunnel`은 Directory fixture P2 해소를 확인했고, 위 backend guard fixture·runtime-mode 경로·고정 주소 화면 fixture·E2E assertion의 좁은 diff에서 새 P1/P2를 발견하지 않았다. 작성자와 분리된 읽기 전용 코드 검토이며 실행 결과를 독립 재실행한 것으로 표현하지 않는다. 차단된 운영 연결 구현은 검토/변경 대상에서 제외했다.

### 운영 배포 연결 코드 명시 승인과 재개 — 2026-09-29

- 사용자 “승인. 시작해.”는 직전 요청의 **기존 배포 작업을 재사용하는 종료 확인/구조 변경 승인 연결 코드 작성과 새 합성 시험 환경 검증**을 승인한다. 앞선 자동 승인 검토의 범위 제한에 필요한 명시 승인이 충족되어 해당 로컬 구현을 재개한다. 실제 Azure 조작·유료 자원·운영/Persistent UAT 변경·실제 provider·push/PR/merge/배포는 포함하지 않는다.
- 범위: 읽기 전용 drain CLI와 기존 migration Job 호출, 기본 false인 구조 변경 승인 입력을 C/O0131 트랜잭션에만 전달, 최초 maintenance rollout/일반 release 실패 경계 및 운영 안내 연결. 새 Container App/DB 표/영구 승인 secret은 추가하지 않는다. 기존 소유 worktree에서 구현·관련 모의/합성 DB·packaged CLI 검증·독립 검토까지 진행하고 완료된 승인 범위만 local commit한다.


### 배포 연결 구현·검증 — 2026-09-29

- 단일 backend/기존 Job 구성에서 `--deployment-drain-check`를 D/C/O별로 한 번씩 실행한다. 두 앱의 모든 revision/replica 종료 뒤 migration 역할의 짧은 비풀링 연결과 읽기 전용 트랜잭션으로 DB 이름·역할·identity, 다른 client session, prepared transaction, 업무 DB의 점검 회차와 처리 중/결과 불명확 발송을 확인한다. Directory는 업무 표를 읽지 않고 오산은 청주 전용 EC 표를 읽지 않는다. 실패·불명확 결과에서 migration으로 진행하지 않는다. 일반 release는 같은 회차의 Active/Delayed, 최초 maintenance 도입은 점검 표 부재 또는 Idle/Completed를 요구한다.
- workflow의 구조 분리 승인은 기본 false다. 선택한 C/O0131 파일의 트랜잭션에만 `set_config(..., true)`로 설정하고 승인 누락/false는 연결 옵션의 과거 승인값을 덮어쓴다. 잘못된 승인 문자열은 연결 전에 거부한다. Directory·역할 준비·종료 확인은 false이며 이미 적용한0131 재실행은 다시 승인하지 않아도 기존 원장대로 동작한다. 저장된 template의 실행별 target/approval/drain/maintenance 설정과 중복 키는 대소문자·설정 구분자를 정규화해 거부한다. 기존 환경·secretRef·자원 크기는 보존한다.
- 운영 종료 확인 임의 파일 의존을 없애고 실제 release image와 기존 migration Job을 사용한다. 새 CLI의 Production operation 설정 오류는 고정 코드와 exit1로 종료하고 SQL·자격증명·예외 본문을 출력하지 않는다. 실행별 override는 Job template/secret에 영구 저장하지 않는다. 실제 운영 실행·공지·중단·이력 정정은 수행하지 않았다.
- 독립 reviewer `review_db_tunnel`의 P1 두 건(설정 키 표기 우회, 관리자 표시/다음 재시도 상태를 예전 provider 결과 확정으로 간주)과 P2 한 건(Production CLI 정책 오류의 미처리 예외)을 보정했다. 불명확 attempt가 남으면 관리자 확인/목록 제외 및 delivery의 Sent/Suppressed/Disabled/DryRunSent와 무관하게 계속 차단한다. 기존 제품에는 이 기록을 자동 해소하는 근거가 없으므로 새 운영 정정 정책을 임의 추가하지 않았다. 이번 시험에서 attempt outcome을 바꾸는 행위는 합성 fixture 준비일 뿐 운영 정정 기능이 아니다.
- 승인/보존/guard 실제 DB 집중 시험2개 PASS: `/private/tmp/business-schema-approval-upgrade-final.log`. 과거 session 승인값이 있어도 runner 승인 null/false/invalid일 때 기존0130 데이터와 원장을 보존하며, 승인 후0131 적용/보존 및 늦은 FK 오류 rollback을 확인했다. checker 집중 시험은 최종 session54454에서1개 PASS(실제 다른 runtime 역할 세션, identity/maintenance/provider 반례 포함), Release 빌드 성공과 소유 tmpfs DB/container/network 정리를 확인했다. prepared transaction 조회 코드는 존재하지만 prepared transaction을 생성하는 양성 반례는 이 실행에 포함하지 않았다.
- 현재까지 syntax/ShellCheck(`-x`), workflow actionlint, change-scope 분류 회귀 PASS. 최초 ShellCheck는 source 미탐색 SC1091로 실패했고 동일 source를 포함한 `-x` 실행으로 재검증했다. 모의 배포/packaged image 최종 결과와 독립 재검토는 아래에 기록한다. 전체 사용자 검수·최종 통합 회귀·원격 CI·운영 적용은 별도 미실행 상태다.
- 최종 실제 production 이미지 시험 PASS: `/private/tmp/business-schema-deployment-image-final.log`. common/directory/business packaged SQL 동등성, 명시 D/C/O bootstrap/migrate, fresh/upgrade/replay, 확정182/56표와36/19열 및 알림/발송 열 수를 검증했다. 추가로 실제 packaged CLI에서 정상 drain6회, 업무 DB 점검 상태 거부4회, Production 대상 누락/오타·선택 자격증명 누락·migration 대상 오타4회의 고정 오류/exit1을 확인했다. 소유 image/container/DB/Compose/network/volume/temp 잔존0. 운영 접속·비밀값 조회·외부 provider 호출은 없다.
- 최종 독립 재검토: 기존 `review_db_tunnel`을 재사용해 이번 연결 diff와 호출 경계를 다시 읽었고, 본인이 작성한 모의 harness2개는 독립 판정에서 제외했다. 제품/runner/Program/checker의 지적 사항 해소와 잔여 P1/P2 없음을 확인했다. `business_schema_migrations`는 본인 checker를 제외한 모의 harness2개와 안내 기록을 검토했고, 환경/secretRef 전체 보존 및 Maintenance 설정 별칭 반례 P2를 지적했다. 일반 template10항목·최초도입9항목의 정확한1회 보존 검사와 exact/mixed/colon 반례를 보강한 뒤 두 P2 해소·추가 P1/P2 없음으로 확인했다. 요청 모델은 기존 agent 설정 유지, 도구 관측 모델 NOT_REPORTED. 이 검토는 실제 diff/제공 로그의 읽기 전용 확인이며 독립 실행 재현을 했다고 표현하지 않는다.
- 커밋 대상160개 개별 경로를 대조했고 binary/기존 common·directory migration 변경0, private-key/token 패턴0을 확인했다. 새 SQL2개의 EOF 빈 줄만 최종 형식 검사에서 제거했다. 원래 checkout·다른 작업 WIP·공유 실행환경·pgAdmin 터널은 변경하지 않았다.
- 최종 동결본 모의 배포 시험 **일반90/90 + 최초도입55/55 PASS**, 합계145개: `/private/tmp/release-drain-mock-tests.log`, `/private/tmp/bootstrap-drain-mock-tests.log`. D/C/O 실패·응답 불명확·계속 실행 중이면 구조 변경을 시작하지 않고, 구조 변경 요청 전/후의 복구 경계와 한 번만 실행하는 계약, 환경/secretRef 보존, 승인 기본false/대상별 전달, reserved/duplicate 설정 및 출력 비밀값 차단을 확인했다. 중간 bootstrap 확장 실행은 macOS 대소문자 비구분 임시 폴더 충돌로 멈춰 순번 폴더로 보정했다. 일반 중간 실행1회의 무출력 종료는 원인을 확정하지 않았으며, 최종 동결본 재실행은90개 전체 통과했다.
- 완료 상태: 이번 승인 범위의 **로컬 구현·영향 자동 검증·독립 검토 완료**, `codex/business-schema-separation`에서 local commit으로 보관한다. 사용자 검수·전체 최종 회귀·원격 CI·push/PR/merge/운영 배포는 미실행이다. 기존 backend1개/1vCPU·2GiB·replica1 구성과 DB3개를 유지하며 Azure 리소스·비밀값·역할·운영 데이터 변경 및 추가 자원 생성은 없다. 운영 전환에는 실제 데이터 사전 점검, 불명확 발송 이력 판정, 복구·중단 계획과 해당 운영 실행 범위의 승인이 남아 있다.
