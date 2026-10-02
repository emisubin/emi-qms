# TASK-AZURE-DEPLOY-001 Change 031 — 오산 1단계 등록 전용 공개 배포

> 현재 준비(2026-10-01): 복구 checkpoint의 최초/일반 DB 배포 경로 구현·격리 검증·독립 검토가 **GO**다. RECOVERY-CHECKPOINT-P2와 후속 서버/Job/증거 보정은 해소했다. 사용자 직접 검수는 **WAIVED**다. 동일 PR159의 새 head 필수 CI와 원격 상태는 [PR Checks](https://github.com/emisubin/emi-qms/pull/159/checks), 최신 검증/잔여 사항은 마지막 절을 따른다. 아래 요약은 각 시점의 이력이며 main 병합·운영0131·공개배포 완료를 뜻하지 않는다.

> 최신 작업(2026-09-30 기준정보 정리): **오산 권한28개·미사용 역할1개 정리의 로컬 구현과 추가 전체 검증 완료(GO), 운영 전환은 보류(NO-GO)**. 유지 권한7개와 기존 역할 연결·사용자 배정·업무/감사 데이터를 보존하며, 모르는 정의나 사용 중인 삭제 대상 역할은 전환을 중단한다. Backend 전체1032건의 초기 실패25건과 skip92건을 각각 보정 재검증/전용 DB 검사로 해소했고 일반 browser64건의 초기 실패5건도 보정 후 통과했다. 운영은 마지막 조회 C209/O209/D8이고 목표 C182/O56/D8은 미적용이다. 불명확 메일1건은 보낸편지함 일치 기록0건까지 확인했으나 미발송 확정은 아니다. Backend1개·자원량을 유지했고 운영/메일 변경·원격 게시·배포는 하지 않았다. 과거 승인 대기 상태는 최신 사용자 승인으로 해소됐으며 최종 근거와 남은 범위는 마지막 절을 따른다.

> 후속 최종 점검(2026-09-30): HEAD `9a86513`에서 표·권한·역할 및 요청 경계를 다시 대조했다. 새로 발견한 청주 부스바 worker2개의 exact ledger 검사 누락(P1)과 배포 승인 범위 설명 누락(P2)을 로컬 보정·독립 검토했다. 최종 backend 집중53/53, 부스바 관련 회귀35/35, frontend 요청 경계23/23 PASS다. 아래 최신 절이 후속 상태를 소유하며, 운영 전환 NO-GO는 유지한다.

> 운영 전환 준비 재개(2026-10-01): 최종 제품 source `49029e2`의 로컬 배포용 이미지 검증과 운영 읽기 전용 사전 점검을 완료했다. C209/O209/D8, 이전 원장130/130/4, 제거 대상의 빈 상태/초기값 및 오산 권한·미사용 역할 조건이 일치한다. 불명확 메일1건은 해당 날짜의 보낸편지함·전체메일·휴지통·스팸함에서도 일치 기록0건이며 상태는 정정하지 않았다. 기존 14일 백업과 2026-09-08 복구 검증 이력을 확인했다. 원격 branch/PR 없음, 운영 전환 NO-GO 유지. 아래 2026-10-01 절이 현재 준비 상태와 남은 판단을 소유한다.

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
- selectorUserValidation: `WAIVED_BY_USER_2026_10_01`
- status: `DRAFT_PR_DEPLOYMENT_PREPARATION`

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


### 실제 운영 DB 읽기 전용 사전점검 — 2026-09-30

- 사용자 요청: “좋아. 확인해봐. 꼼꼼하게 하나하나 다 검사해.” 직전 안내의 첫 단계인 **실제 운영 DB 사전점검**을 수행했다. 데이터 정정·구조 변경·배포·발송/재발송은 범위 밖이며 실행하지 않았다. 코드 수정도 하지 않았고 이 절에 확인 결과와 차단 사항만 기록한다.
- 관측 구간: 2026-09-30 09:50~09:57 KST. C/O/Directory runtime credential을 승인된 Key Vault에서 메모리에만 수신하고, 인증서/호스트명을 검증하는 TLS1.3 통로를 사용했다. 기존15432 사용자 listener가 없어 검증된 기존 도구의 조사 소유 임시 통로1개를 생성·재사용하고 정상 종료했다. 새 Azure 자원·영구 설정·DB 변경 없음. 통로의 정상 종료 요청/exit0을 확인했으며 원격 임시 디렉터리의 별도 재조회는 하지 않았다.
- 모든 정상 DB 조회에서 `default_transaction_read_only=on`, `transaction_read_only=on`, statement timeout8초/lock timeout1초를 확인했다. 운영 migration 파일/DO/DDL/consent GUC는 실행하지 않았다. canonical SQL의 조건식만 SELECT로 옮겨 평가했다. 출력/증거는 개수·상태·schema metadata·비식별 fingerprint이며 사용자/프로젝트/메일 원문과 비밀값은 보관하지 않았다.

| 점검 | 청주 | 오산 | Directory / 판정 |
| --- | --- | --- | --- |
| 실제 DB identity | emi_qms / CHEONGJU | emi_qms_osan / OSAN | emi_qms_directory / directory; 모두 일치 |
| public 기본 표 | 209 | 209 | 8; 총426표 COUNT 실패0 |
| 기존 migration 원장 | canonical130 정확일치 | canonical130 정확일치 | directory4 정확일치;0131 미적용 |
| projects 열 / 사전 목록 | 43 / 정확일치 | 43 / 정확일치 | 추가·누락0 |
| public 열 metadata 가시성 | 2,011/2,011 | 2,011/2,011 | 53/53; C/O 전체 관측 열 metadata 동일 |
| 비어 있지 않은 표 / 빈 표 | 102 / 107 | 70 / 139 | 8 / 0; RLS 적용0 |
| 제거 대상 표 | 27: 빈23 + 초기값4(41행) | 153: 빈134 + 초기값19(159행) | empty 및 canonical literal/관계 guard 전부 통과 |
| 제거 예정 프로젝트 열 조건 | 7열 조건 전부 통과 | normalized 열 조건496행 위반, 그 외 조건0 | 아래 P1-1 참조 |
| 제거 예정 알림 참조열 | 해당 없음 | notifications2열/deliveries1열 모두 NULL | 보존해야 할 참조값0 |
| 유지표→제거표 FK | projects.osan_customer_id 1개 | projects LQC/알림 work-item 등4개 | 전부 명시 제거 또는 제거열에 속한 FK; 예상 밖 종속 view0 |
| invalid constraint / index | 0 / 0 | 0 / 0 | Directory도0/0 |
| 권한 / 실제 다른 DB 로그인 | 자기 DB CONNECT만 허용 | 자기 DB CONNECT만 허용 | 3역할×다른2DB=6회 모두 database permission denied |
| 진행 중 발송 / EC 불확정 | 모두0 | 현재 Processing0, 과거 불명확 메일1건 | prepared transaction0, 대기 lock0 |

- 프로젝트 보존 기준: 청주3행(논리삭제1), 오산496행(Active424 중 논리삭제9, Completed72). 오산 targets496, 단계3472, 사진931, 단계기록916. 이전361행과의 증가는 운영 중 자연 변화이며 과거 행 수를 현재 정답으로 사용하지 않았다. 오산 중복 활성 project_code는16그룹/40행이고 기존 계약대로 보존 대상이다. 새 Osan retained registration/status check 위반0. 개인 알림 설정은 profile2+preference9=11행으로 기존 보존 계약과 일치하며 전행 fingerprint를 기록했다. 발송42,230행 중 manual_payload_json 비NULL29,822행의 보존 기준 fingerprint도 기록했다. 이는 전환 전 기준값이며 운영 전후 보존 확인은 아직 아니다.
- FK·view뿐 아니라 제거열의 pg_depend와 함수 본문 참조를 조회했다. C normal dependency147, O840의 목록에서 제거표/열에 딸린 constraint·view·trigger와 명시 제거 목록을 대조했다. generic audit 함수의 JSON 키/문자열 일치는 삭제 대상으로 오판하지 않았다. 다른 schema에 사용자 table/view 없음. 정적 catalog/text scan이 동적 SQL의 모든 경로를 증명하는 것은 아니다.

**전환을 막는 확인 사항**

1. **P1-1: 새 오산0131 검사 조건이 정상 운영 저장 계약과 불일치한다.** 모든496행의 `project_title_normalized`는 NULL이다. 과거361행 조사에서도 nonnull0이고, 운영기준02028f2의 OsanProjectStore는 해당 열을 명시적으로NULL로 저장한다. migration0087도 정규화 제목 unique index를 청주에만 적용한다. 그런데 새0131의695행은 제목을 정규화한 값과 일치해야 한다고 요구해 정상496행을 모두 거부한다. 데이터 정정 대상이 아니라 **로컬 전환 코드와 합성 fixture의 보정 대상**이다. 최소 방향은 이 열에 `is not null`이 있을 때만 중단하게 바꾸고, 정상NULL/예상 밖nonNULL 반례를 검증하는 것이다. 다른 복제열(name/title, number/code) 보호 조건은 유지한다. 이번 점검에서는 수정하지 않았다.
2. **P1-2: 오산에서 표 제거 후 실행 가능한 잔존 함수가 남는다.** `qms_record_site_access`와 `qms_end_site_access`는 현재 runtime 역할에 EXECUTE가 허용된 SECURITY DEFINER 함수이며 `site_access_sessions`를 참조한다. 새0131은 표만 제거하고 두 함수는 남긴다. 새 앱의 정상 Osan 경로는 IsOsan 검사로 해당 호출을 막지만 DB에 직접 호출 가능한 깨진 함수가 남는 정리 누락이다. 해당 함수2개와 전용 helper2개(`qms_site_access_guard_updates`, `qms_site_access_menu_codes_valid`)의 명시 제거/최종 부재 검사 및 나머지 제거표 전용 trigger helper 목록 검토가 필요하다. 실제 함수 호출이나 다른 DB 접근을 시험한 것은 아니며 운영 함수는 변경하지 않았다.
3. **운영 차단: 결과 불명확한 메일 호출1건이 남아 있다.** 2026-09-22 22:21 KST에 생성된 OsanWorkflow/Mail attempt가 `LeaseExpiredAfterProviderCallStarted`이다. delivery는Failed, provider message ID는 없고 후속Sent/현재Processing도 없다. 이 결과는 “메일이 안 나갔다”는 증거가 아니며 새 drain 정책상 차단된다. 외부 발송 결과와 정정 근거를 별도로 확인해야 한다. 임의 상태 변경·삭제·재전송은 하지 않았다.

**OSAN-G2-001 입력 경로 확인**

- 오산의 G2 전용 표4개는 모두0행이다. 이름/title/key/code/number 중G2 문구가 있는 projects11개는 모두Osan이고 오산 생성operation/ProjectCreated event가 각각11개 있다.
- audit_event_changes의 프로젝트Insert를 audit_events에 연결한 결과 **11개 모두 엑셀 등록 ApplyOsanProjectImport, 총3개 요청**이었다. Insert 감사 누락0. 생성기간은2026-09-10~09-18이며 현재Active10/Completed1이다. 후속 교집합 집계에서4개는 단계·사진 이력이 함께 있다.
- 따라서 “어떤 경로로 저장됐는지”는 확인 완료다. 청주 G2 실적 표가 오산으로 복사됐다는 증거는 없고, G2 문구가 들어간 프로젝트를 오산 엑셀 등록으로 저장한 것이다. 업무상 해당 프로젝트를 오산에 등록한 이유·내용의 적절성은 별도 업무 판단으로 남긴다. 실제 이름/내용/등록자 개인정보를 출력하거나 데이터를 변경하지 않았다. 과거 OPEN 기록 중 입력경로 미확인은 이 결과로 해소한다.

**운영 설정과 별도 확인 사항**

- Backend는 기존backend--0000068/02028f2 기준 image로 동일하고1vCPU/2GiB/min=max replica1, Frontend도 기존0000059이다. 앱은Running/Single revision이며 DB 자동 migration=false이다. 신규 코드는 아직 운영에 없다.
- 기존 migration/bootstrap/backfill/maintenance Job4개는 Manual/retry0/parallelism1/completion1/container1이고, 반환된 실행 목록에 현재Running 상태는 없었다. 저장 template에 이번 예약 target/approval/drain/maintenance 설정, 정규화 중복 키, 평문 DB connection setting은 없다. Job 저장image가 운영 backend와 다르지만 새 release는 실행별 정확한 새image를 덮어쓰는 계약이므로 기존image로 새CLI를 직접 실행하면 안 된다. 이번에는 Job을 실행하지 않았다.
- DB는PostgreSQL16/Ready, public accessDisabled. 백업보존14일, 반환된 earliest restore 시각2026-09-16T23:38:23Z, geo redundancy/HA 비활성이다. 백업 설정 관측은 실제 복원 시험 성공을 뜻하지 않는다. 실제 복구 준비 확인은 운영 전환 전 남아 있다.
- 오산에는 Mail Failed 누적15,821건(SmtpConnectionFailed9,317 / SmtpSendFailed6,467 / retry-limit37)이 있다. 최신 실패행 생성시각은2026-09-29 23:30 KST이다. 이는 별도 발송 운영 점검 대상이며, 이번 결과만으로 현재 SMTP가 고장이라고 단정하지 않는다. 청주Failed11건도 과거 누적 이력으로 관측했다. 실패행을 삭제하거나 재발송하지 않았다.
- 앱이 가동 중인 여러 시점의 autocommit 조회다. C/O에idle client session3/1개가 관측되어 현재 상태는 배포 drain 완료가 아니다. 실제 적용 창에서 앱·수동 접속을 종료한 후 다시 확인해야 한다. runtime 역할로 점검했으며 실제 migration 역할의 접속/권한, 운영 새image drain, 실제0131 DDL/rollback, 운영 데이터 전후 비교는 실행하지 않았다.
- 증거(비식별 임시 집계): `/private/tmp/pms-preflight-20260930-result.json`, `followup-result.json`, `final-result.json`, `cross-login-result.json`, `azure-result.json`(뒤4개도같은 `pms-preflight-20260930-` prefix). SELECT queryset의 오류0, 권한 음성시험6개 모두 DB권한거부 확인. 원문 SQL 오류/비밀값은 저장하지 않았다.
- 독립 검토: `business_schema_migrations`가 migration 계약·정상NULL 저장코드·함수 정리 누락을 읽기 전용으로 확인했고, `review_db_tunnel`이 실제 조회 코드/집계 및 G2 감사 경로를 대조해 **NO-GO**에 동의했다. 각 reviewer는 본인이 운영 조회를 실행한 것으로 표시하지 않으며 실제 모델은NOT_REPORTED. 마지막 권한 오류 분류와 G2 단계/사진 교집합은 부모 후속 조회에서 확인했다.
- **상태: 읽기 전용 사전점검 완료 / 운영 전환 보류.** 다음 작업은 정상 오산 계약에 맞는 로컬 guard·시험 보정, 잔존 함수 정리 보완, 불명확 메일 결과의 처리 근거 확인이다. 제품 코드·운영 DB·Azure 영구 설정·실제 provider·원격 Git은 변경하지 않았다. 이 점검 기록만 같은 branch의 별도 local documentation commit으로 보관한다.


### 사전점검 결함의 로컬 보정과 메일 기록 후속 확인 — 2026-09-30

- 사용자 “진행해”에 따라 앞 절 P1-1/P1-2의 로컬 코드·합성 시험 보정과 기존 메일 기록의 읽기 전용 후속 조사를 진행했다. `codex/business-schema-separation`, base `092db40`을 이어가며 common0001..0130은 변경하지 않았다. 새0131은 main/운영 미적용이므로 이 파일 안에서 보정했다. 운영 DB 정정·실제 발송/재발송·자원 변경·push/PR/merge/배포는 이번 범위가 아니다.
- **P1-1 해소:** OsanProjectStore의 기존 NULL 저장 계약에 맞춰 `project_title_normalized is not null`일 때 전환을 거부한다. 운영496행을 정정하지 않았다. 합성 upgrade fixture도 정상NULL로 보정했고, 예상 밖nonNULL 및 name/title·number/code 불일치는 각각 실제 migration 실행에서 거부함을 확인했다.
- **P1-2 해소 및 같은 누락 보완:** 오산에서 제거표 전용 함수41개(무인자 trigger37 + site-access 전용4)를 표 제거 뒤 정확한 signature와 RESTRICT로 제거한다. 청주에도 같은 종류의 누락인 `guard_osan_progress_append_only()` 1개를 추가 제거한다. 예상 밖 overload가 남으면 마지막 부재 검사에서 중단한다. 공용 감사 함수, 오산에서 사용하는 진행/알림 함수와 청주의 site-access 함수는 보존한다. 삭제 범위는 함수 전체나 이름 접두어에 대한 일괄 제거가 아니다.
- 소유관계 근거: `business_schema_migrations`의 독립 읽기 전용 검토가 common catalog의 함수 정의·호출과 기존 운영 trigger snapshot을 대조했다. O41/C1에 retained-table trigger caller0, 제품 코드의 다른 활성 호출0을 확인했다. Site-access의 제품 호출은 AuditStore의 청주 경로에만 남는다. 함수가 retained 표를 읽는 방향의 참조와, retained trigger가 해당 함수를 호출하는 방향을 구분했다. 정적/catalog 검토는 알 수 없는 동적 SQL 전체의 증명이 아니며 RESTRICT는 catalog에 기록된 예상 밖 종속성을 거부한다.
- 집중 검증 **7/7 PASS, 실패0/건너뜀0**: `/private/tmp/business-schema-preflight-fixes-tests.log`. 새 DB 및0130 기존 schema에서 실제 migration, 사업부별 표/열/함수 소유권, 보존 대상 전행 snapshot·개인설정11행·중복코드·완료/삭제 이력·manual payload 보존, 명시 승인 거부, migration 재실행을 확인했다. 청주 runtime 역할로 접속 기록 생성·종료가 성공했고 오산 runtime의 제거 함수 직접 호출2개는 `42883`으로 거부됐다. 예상 밖 retained trigger dependency와 overload를 포함한 실패 사례는 transaction rollback 후209표·43열·130원장 및 기존 함수 복원을 확인했다. 전용 합성 DB·tmpfs container/network의 실행 소유 범위를 정리했다.
- 독립 diff 검토: `review_db_tunnel`이 base092db40 대비 SQL2개+시험3개와 기존 계약/함수 소유관계/집중시험 로그를 읽고 **로컬 보정 GO, 추가 P1/P2 없음**을 반환했다. reviewer는 파일을 편집하거나 시험·운영 조회를 실행하지 않았다. 에이전트 설정은 상속되었고 실제 관측 모델은 NOT_REPORTED이다.
- 배포용 이미지 검증 **PASS**: `/private/tmp/business-schema-preflight-fixes-image.log`. build, business/directory catalog exact match, fresh apply, existing apply, reduced schemas 검증이 모두 통과했다. 전용 image/container/DB/Compose container/network/volume/임시 디렉터리 잔여 수가 모두0이며 wrapper exit0을 확인했다. 실제 운영 배포 성공을 뜻하지 않는다.

**불명확한 메일의 추가 근거와 한계**

- 기존 Azure Log Analytics(보존30일)의 2026-09-22 13:15~13:35 UTC backend console 로그3,174개를 서버 쪽에서 집계했다. 원문·수신 주소·제목/본문은 수집하지 않았다. 해당 구간은 revision0000063이며 `NotificationDeliveryFailed`/`NotificationDeliveryClaimLost`/SMTP 관련 문구는 각각0이다. 관련 문구가 없다는 것이 발송 성공 또는 실패의 증명은 아니다.
- 문제 attempt 생성은13:21:29.615241 UTC였다. 같은 revision의 앱 시작13:21:30.8867774, 종료13:21:31.6605807, 시작13:21:52.8367825, 종료13:22:02.7230956 UTC 기록을 확인했다. 시스템 이벤트에도 같은 구간 ContainerStarted/Terminated, ContainerAppUpdate/RevisionUpdate 및 Key Vault 동기화 실패·성공 등이 있다. 이 시간적 연관은 처리 중단 가능성을 뒷받침하지만 특정 worker와의 직접 연결 또는 단일 원인 확정은 아니다.
- 현재 backend의 SMTP Host 설정 하나만 조회해 `smtp.gmail.com`을 확인했다. 자격증명·주소는 조회하지 않았다. 현재 운영 source의 SMTP adapter는 SMTP 서버의 실제 응답 식별자를 보관하지 않고 성공 시 자체 `smtp-sent` 표식을 반환한다. 해당 파일의 마지막 변경은2026-09-11이나, 과거revision의 정확한 배포 image/source를 이번에 대조한 것은 아니다. 문제 attempt/delivery에는 그 표식도 없다는 기존 DB 조사 결과가 유지된다.
- **남은 운영 차단:** Gmail 발신 계정의 보낸편지/해당 서비스 발송 기록 등 외부 근거가 필요하다. 이번에 Gmail 계정에 접속하거나 외부 발송 결과를 확인하지 않았다. 기존 Azure/DB 기록만으로 성공·실패를 확정하지 않고 attempt의 상태·이력을 그대로 보존했다. drain의 불명확 발송 차단 조건도 완화하지 않았다. 발송 근거를 확보한 뒤 이력을 어떻게 판정·기록할지 별도로 확정해야 하며, 단순 관리자 확인 표시로 통과한다고 가정하지 않는다.
- 상태: 코드 결함의 로컬 보정/집중 시험/독립 검토 완료. 운영 전환은 불명확 발송 판정과 기존에 남은 복구 준비·실제 적용 창의 재점검·사용자 검수·원격 검증/명시 승인 후에 가능하다. backend1개 및 비용/자원 구성은 그대로이며 운영 DB/메일/영구 설정 변경은 없다.

### 발송 기록과 사업부 분리 전체 재감사 — 2026-09-30

**범위와 판정**

- 사용자 요청은 발송 기록을 포함하여 프론트엔드→백엔드→사업부 DB, 생성·참조·잔존 객체를 세세히 재검사하는 것이다. 기존 branch `codex/business-schema-separation`, 제품 기준 `07153a3`를 읽었으며 이번에는 제품 구현을 변경하지 않았다. 아래 합성 재현은 운영과 분리된 임시 DB에서만 수행했다.
- 허용된 구조는 backend1개, 일반 청주 업무→C, 일반 오산 업무→O, 공통 로그인·소속→D, 총괄 사용자관리의 명시적 C/O local profile 변경이다. 공통 관리·상태 점검이 여러 DB를 읽는 것 자체와, 일반 업무가 다른 사업부 DB를 사용하는 오류를 구분했다. 같은 프로세스에 세 접속 정보가 존재한다는 승인된 한계도 그대로다.
- **완료 판정 불가:** 아래 재현 결함3종과 배포 검증 공백, 기준정보 정리 범위가 남는다. 직전 보정의 집중 시험 통과를 전체 분리 완료로 확대하지 않는다. 추가 운영 변경·발송/재발송·DB 상태 정정·push/PR/merge/배포는 수행하지 않았다.

**운영 DB에서 직접 확인한 사실**

| 항목 | 청주 `emi_qms` | 오산 `emi_qms_osan` | Directory `emi_qms_directory` |
| --- | ---: | ---: | ---: |
| 현재 public 표 | 209 | 209 | 8 |
| 로컬 분리 목표 표 | 182 | 56 | 8 |
| 현재 view / sequence | 4 / 4 | 4 / 4 | 0 / 0 |
| 자기 DB runtime CONNECT | 허용 | 허용 | 허용 |
| 다른 두 업무/Directory DB CONNECT | 모두 거부 | 모두 거부 | 모두 거부 |

- PostgreSQL catalog와 세 runtime 역할을 읽기 전용으로 조회했다. 업무 DB는 위3개뿐이며 나머지 조회된 이름은 `azure_maintenance`, `azure_sys`, `postgres`, `template1`이다. 새 업무 DB가 추가로 생성된 흔적은 없다. `template1`은 catalog의 template 표시도 확인했다. Bicep은 기존 C를 재사용하며 D/O만 생성하고, 제품/migration 코드에서 운영 `CREATE DATABASE` 경로는 찾지 못했다.
- 세 DB 모두 업무용 schema는 public 하나이고 foreign server0이다. C/O extension은 plpgsql 및 uuid-ossp, D는 plpgsql이다. catalog에 dblink/FDW를 통한 별도 DB 연결은 없다. 이는 애플리케이션 프로세스의 여러 연결 보유와 다른 관찰이다.
- D의 현재8표는 runtime 직접 INSERT/UPDATE/DELETE 가능 표0개다. 그러나 D migrator의 **default ACL**에는 runtime SELECT와 함께 INSERT/UPDATE/DELETE가 실제로 남아 있다. 현재 표 권한과 향후 생성 표의 기본 권한을 구분했다.
- O의 `interior-busbar-manager` 역할은 현재 사용자 배정0, role_permission 연결0이다. 이 역할은 청주 부스바 정의이며 O 업무 코드에서 사용하지 않는다. 다른 역할 전체의 삭제 안전성을 이 한 건으로 추정하지 않는다.
- 증거: `/private/tmp/pms-preflight-20260930-mail-control-result.json`, `/private/tmp/pms-preflight-20260930-final-metadata-result.json`. 모든 연결에서 `transaction_read_only=on`, 예상 DB/역할 일치, query error0을 확인했다. 비밀값·메일 주소·제목/본문·개인 ID는 기록하지 않았다. 조회용으로 연 전용 연결 통로는 정상 종료했고 사용자 기존 연결은 변경하지 않았다.

**재현한 결함과 필요한 보정**

1. **P1: 공통 사용자관리가 실제 수정 대상 사업부의 점검 잠금을 지키지 않는다.** `DeploymentMaintenanceMiddleware.cs:24`는 선택 사업부가 없으면 통과하고, 있으면 그 사업부만 잠근다. `BusinessUnitAccessAdministrationStore.cs:201` 이후 affectedUnits의 local profile을 바꾸지만 각 대상의 maintenance lease를 확보하지 않는다. 합성 HTTP 요청에서 `/access/api/admin/user-access/...`와 `/cheongju/api/admin/user-access/...` 두 경로가 모두 O의 Active/Delayed/Failed 상태에서200을 반환하고 O 사용자 활성 상태와 D access_version을 변경했다(2경로×3상태). 총괄 관리자의 정상 권한을 가진 요청에서 재현했으며 무권한 사용자의 침입으로 표현하지 않는다. 운영 전환 중 쓰기 차단/배수 계약을 위반한다.
   - 실제 affectedUnits 전체의 lease를 정해진 순서로 한 번씩 확보하고, Directory 작업 시작 전부터 local 적용/Publish까지 유지해야 한다. 선택 사업부 middleware lease와 store lease를 단순 중첩하면 대기 중 exclusive lock 때문에 교착 가능성이 있으므로 해당 관리 endpoint에만 잠금 책임을 명확히 위임해야 한다.
   - 공통 관리 endpoint가 C/O URL에서도 실행되는 별칭도 남아 있다(`BusinessUnitRouteMiddleware.cs:36`). FE는 이미 `/access`를 사용한다. 공통 관리3종은 `/access` 전용으로 제한하고 `/me`·`/runtime-mode`의 사업부 경로는 보존하는 보정이 필요하다. URL 제한만으로 maintenance 우회가 해결되지는 않는다.
2. **P2: Directory에 새로 만드는 표가 runtime 직접 쓰기 권한을 상속한다.** `DatabaseRuntimePrivilegeManager.cs:78`의 bootstrap은 D에도 default DML을 주고, `:205` 이후 reconcile은 default SELECT를 추가하지만 이전 DML을 회수하지 않는다. 합성 D에서 모든 migration/reconcile을 마친 뒤 migrator로 새 표를 만들자 runtime INSERT→UPDATE→DELETE가 모두 성공했다. 현재8표의 직접 쓰기는 차단돼 있고, 새 표 생성 후 reconcile을 다시 실행하면 그 표의 현재 쓰기 권한은 회수되지만 잘못된 기본값은 계속 남는다. D bootstrap/reconcile 양쪽의 default table DML 회수와 실제 새 표 생성 반례 검사가 필요하다.
3. **P2: 오산에 청주 전용 번호 생성기2개가 남는다.** `pending_issue_number_seq`, `busbar_product_number_seq`는 표에 소유된 sequence가 아니므로 표 제거로 없어지지 않는다. 실제 O0131 적용 후 sequence는 필요한 `audit_event_changes_id_seq`와 위2개, 총3개이며 O runtime은 불필요2개에도 USAGE가 있었다. 목표는 C4/O1/D0이다. O0131에서 정확한2개를 RESTRICT로 제거하고 목록/권한을 확인해야 한다. 현재 번호 생성기가 다른 DB 데이터를 읽거나 쓰는 것은 아니다.

- 재현 소스 `/private/tmp/BusinessSchemaFullAuditProbe.cs`, 로그 `/private/tmp/business-schema-audit-reproduction-20260930.log`. 임시 partial test1개 안에서 위 모든 조건을 assertion으로 확인했다. **테스트 성공은 결함 재현 성공이며 제품 요구조건 통과가 아니다.** 첫 진단 실행은 누락 using으로 compile 실패했고 이를 시험 파일에서 보완한 다음 실행이1/1 성공했다. 두 실행 모두 전용 tmpfs DB/container/network를 정리했다. 제품 저장소에 복사한 임시 test는 원본과 동일함을 확인한 뒤 제거했으며 commit에 포함하지 않았다.

**정적 검토에서 확인한 배포·최소화 공백**

- **배포 직전 공개 backend 설정 검증 부족:** `deploy-azure-pilot-release.sh:200` 이후의 엄격한 split/connection 검사는 migration·maintenance job template에 적용된다. public backend template의 `BusinessUnits__Enabled=true`, 세 runtime secret 참조·대상 metadata를 같은 수준으로 확인하지 않는다. 최종 smoke(`:935`)도 live200/root401/api401이다. 앱은 의도적으로 legacy Qms 모드를 지원하므로 이 신호만으로3-DB serving 상태를 입증할 수 없다. 현재 운영이 legacy 모드라는 발견이 아니라, 잘못된 향후 template을 배포 전에 거부하는 장치가 빠진 것이다.
- **이미지 검증이 자동 게시 경로에 연결되지 않음:** `.github/workflows/azure-pilot-images.yml:230`은 build/push하고 실제 packaged catalog·fresh/upgrade 검증인 `test-business-unit-production-image.sh`를 workflow가 호출하지 않는다. maintenance bootstrap 시험도 자동 경로에 없다. 직전 로컬 이미지 시험은 통과했지만 이후 변경을 같은 검증으로 보호하지 못한다. 검증한 동일 이미지에 대한 게시/배포 gate가 필요하다. 이번에는 registry나 workflow를 실행하지 않았다.
- **객체 종류별 완료 검증 누락:** 기존 시험은 C/O exact 표 목록·projects 열과 일부 함수/notification 열 수를 검증하지만 sequence·view·trigger·FK·default ACL 전체 소유 계약, D exact8표를 일관되게 비교하지 않는다. packaged D 검사는 identity 확인 뒤 반환한다. 표 개수만 맞아도 불필요 객체가 남을 수 있다는 것이 이번 sequence 반례다. 독립 소유 기준에 필요한 객체/권한 검사를 추가해야 한다.
- **기준정보는 아직 사업부별 최소 집합이 아님:** 확정 소유표는 양쪽 departments10/permissions35/roles11/role_permissions111 보존을 명시한다. 따라서 O에 청주 전용 역할·권한 정의가 남아도 현재 migration은 이를 지운다고 약속하지 않는다. 확인된 미사용 부스바 역할을 포함하여 O 업무/관리 UI에 필요한 코드와 기존 배정을 대조한 정리표가 더 필요하다. 기존 사용자 배정/감사 이력을 훼손하는 일괄 삭제는 하지 않는다. 빈 `busbar_ecount_employees`와 O 개인 알림 설정11행도 이전 보존 계약의 예외이며, 빈 표=불필요로 판정하지 않는다.
- **현재 운영 문서가 구현과 다름:** Azure README의 prepare-only 절차 및 D0001..0002/C·O0001..0087 설명, database README의 세 DB 모두 ready 설명을 현행 target 실행/D0001..0004/C·O0001..0131 및 사업부별 degraded 상태 계약과 맞춰야 한다. 역사 Task의 당시 승인 기록을 지우는 것은 아니다.
- 추가 방어 권고: 대상 DB 이름의 상호 중복뿐 아니라 system/template 이름 override도 명시 거부하는 편이 낫다. 현재 운영 이름은 정상이며 잘못된 설정을 실제 실행해 보지는 않았다. C 전용 향후0132가 O catalog를 깨뜨린다는 우려는 코드 재검토로 기각했다. common1..130 고정 guard도 결함으로 세지 않는다.

**확인된 경계와 검증 범위**

- FE의 실제 네트워크 호출은 api.ts의 fetch2곳으로 모이고 일반 API는 고정 C/O URL, 총괄 관리는 access로 변환된다. 이미지/QR/Excel도 공통 인증·사업부 요청 경로를 사용한다. URL/헤더는 토큰 대기 전 선택을 보존하며, 사업부 전환 시 이전 읽기와 본문 소비 결과를 폐기하고 쓰기 중 전환을 막는다. 화면은 사업부 선택/권한 확정 후 업무 데이터를 요청한다. FE 직접 DB 연결은 찾지 못했다.
- backend276개 C# 파일의 연결/provider/module 참조, endpoint42파일, 업무 모듈/partial61파일의 SQL 및 공유 Identity·사용자관리·알림·감사·공지·사진·출력 경로를 독립 검토했다. 위 공통 관리 결함 외 일반 업무에서 다른 DB로 fallback하거나 제거 대상 표를 계속 사용하는 활성 경로는 찾지 못했다. 전체 C# 파일을 동일 깊이로 line-by-line 실행한 뜻은 아니다.
- worker는 고정 C/O scope로 실행하고 다른 사업부 실패를 대체 DB로 우회하지 않는다. split 모드의 migration/bootstrap은 target 필수, startup 자동 migration은 실행하지 않는다. health의 D/C/O 조회는 공통 상태 점검 계약이고 일반 업무의 교차 DB 조회가 아니다.
- C182/O56 exact 표, projects C36/O19, O notification12/delivery45 열, C에서 O27표/O에서 C153표 제거, O 제거함수41개(37+site-access4), 공통 audit 보존을 재대조했다. retained dependency는 RESTRICT로 중단한다. 공통0001..0130은 과거 upgrade 기반으로 보존하고 새 DB도 이력을 적용 후 해당0131로 축소한다. 원장에 과거 표 생성 이력이 남는 것과 현재 표가 남는 것은 다르다.
- 이번 재실행: backend boundary/architecture/catalog/SQL **36/36 PASS**, frontend API/route/deep-link **23/23 PASS**, 추가 진단1개에서 결함3종 재현. 로그는 `/private/tmp/business-schema-full-boundary-20260930.log`, `/private/tmp/business-schema-frontend-boundary-20260930.log`이다. 변경 없는 `07153a3`의 직전 packaged image PASS는 재사용했다. 전체 UI 수동 조작·전체 회귀·운영 쓰기·실제 provider 발송을 수행했다고 주장하지 않는다.
- 독립 검토는 `review_db_tunnel`(backend)과 `business_schema_migrations`(schema/infra/CI)이 읽기 전용으로 수행했다. 실제 관측 모델은 NOT_REPORTED이며 구현·운영 권한은 위임하지 않았다. Findings의 최종 우선순위는 parent가 실제 재현/현재 영향으로 정리했다. `review_db_tunnel`은 추가로 진단 소스/로그와 이번 기록의 주장–assert 연결을 검토해 추가 P1/P2 및 범위 과장 없음을 확인했다. 이 추가 검토에서 Gmail/운영 원자료를 독립 재조회한 것은 아니다.

**Gmail 발송 기록 확인**

- 현재 설정된 SMTP 발신 계정의 Gmail 보낸편지함을 EXAMINE(read-only)으로 열고 2026-09-22~23 검색 구간의1,036개 메일에서 날짜·수신자·제목·Message-ID 등 헤더만 읽었다. 본문은 조회하지 않았고 읽음 표시·발송·삭제를 변경하지 않았다. MIME 헤더와 공백/Unicode 정규화를 적용하여 DB의 당시 수신자 snapshot·제목과 메모리에서 비교했으며 원문은 저장하지 않았다.
- 불명확 attempt1건의 provider 호출 시작은2026-09-22 13:21:29.850572 UTC(한국22:21:29)이며 완료 표시는13:27:24.302712 UTC, attempt_no3, `LeaseExpiredAfterProviderCallStarted`, delivery Failed다. 동일 발송건의 앞선2회는 RetryScheduled였다.
- **문제 메일과 제목·수신자가 함께 일치하는 보낸편지 기록0건**이다. 같은 수신자만 맞는 것은32건, 제목이 맞는 것은0건이었다. 동일 검색/비교 방식으로 그 구간의 정상 Sent10건을 대조하여 **10/10 일치**했다. 검색이 전혀 작동하지 않은 경우와 구분했다.
- DB 전체에서는 같은 제목/수신자 조합의 delivery가2개이며 둘 다 Failed, sent_at/provider ID 없음이다. 따라서 제목·수신자 조합을 영구 고유 식별자로 간주하지 않는다. 문제 attempt의 실제 SMTP 식별자도 없어 provider receipt로 직접 대조하지 못한다.
- **결론은 ‘확인한 보낸편지함 구간에 일치 기록 없음’까지다.** 삭제된 메일·다른 보관함·수신자 측 기록을 검사하지 않았으므로 미발송을 확정하거나 발송 이력을 덮어쓰지 않았다. 기존 drain의 불명확 발송 차단은 유지되며 처리 결과/근거를 기록하는 별도 운영 결정을 거쳐야 한다.

**남은 순서:** 공통 관리 잠금/URL 경계 → D 기본 권한과 O sequence 보정 → 실제 객체/권한 반례 및 배포 설정·이미지 gate → 사업부별 기준정보 최소 목록 확정 → 메일 판정·복구 준비·적용 창 재점검 → 사용자 검수/원격 검증/명시 운영 승인 범위에서 전환. 이번 상태는 **감사 완료, 추가 보정 미착수, 운영 미적용**이다. 앱 수와 비용 구성은 변경하지 않았다.

### 전체 재감사 후 로컬 보정 시작 — 2026-09-30

- 사용자 “진행해”를 앞 절의 코드·검증·배포 검사 및 기준정보 정리 보정 지시로 받아 기존 `codex/business-schema-separation`, base `f03c21c`를 이어간다. 운영 데이터/메일 판정 변경·실제 발송·원격 게시·배포는 포함하지 않는다.
- 목표: 공통 사용자관리의 전체 대상 점검 잠금과 공통 URL, D future-table 최소 권한, O 잔존 객체, 실제 객체/권한 반례, serving 설정 및 검증한 이미지 게시 gate를 보완한다. 기준정보는 실제 업무 사용·배정·이력과 대조하여 필요한 범위만 남기며, 기존 부서/사용자 역할 체계의 변경은 별도 판단한다.
- 불변조건: backend1개/현재 자원량, C182/O56/D8, common0001..0130 불변, 업무·감사·개인 설정 보존, Directory 작업 번호/버전/복구 계약, 불명확 발송 차단 유지. 새0131은 main/운영 미적용 파일로 이 안에서 보정할 수 있다.
- 검증: 운영과 무관한 합성 DB에서 거부 시 무변경·정상/재시도·점검과 저장의 경쟁을 확인하고, schema/ACL 및 release mock/packaged image 검증 후 작성자와 분리된 검토를 받는다. 현재 구현 중이며 결과는 아래에 갱신한다.

**기준정보 삭제안의 추가 승인 대기**

- 자동 승인 검토가 아래 삭제를 포함하는 로컬 migration 파일 작성 패치를 거부했다. 기존의 포괄적인 구조 정리 지시만으로 권한28개·연결 행·역할1개의 영구 삭제 범위까지 명확히 승인됐다고 판단할 수 없다는 이유다. 거부된 패치는 적용되지 않았고 같은 삭제를 다른 방법으로 작성/실행하지 않는다. 점검 잠금·기본 ACL·sequence·배포 검사 등 독립 보정은 계속한다.
- 대상은 **오산 DB만**이다. 공통 baseline의 permissions35개 중 아래28개와 해당 permission_id의 role_permissions 연결을 제거하고, user_roles 및 role_permissions의 연결이 모두0인 `interior-busbar-manager` 역할1개를 제거하는 0131 로컬 migration 파일 작성이다. 이번에 요청하는 승인은 운영에 migration을 실행하는 승인이 아니다.
- 유지7개: `projects.read`, `Project.Read.All`, `Project.Create`, `Project.Update`, `Project.Delete`, `manufacturing.update`, `users.manage`. 이들의 기존 권한 연결, 부서10개·기본역할10개, 모든 사용자 역할 배정·업무·감사/과거 이력을 보존한다. 청주 DB는 이 정리 대상이 아니다.
- 영향: 이후 별도 승인된 운영 적용 시 제거된 권한 정의와 그 연결은 DB에서 사라지며, 복원하려면 백업/명시 복구가 필요하다. 현재 오산에서 도달 가능한 기능은 유지7개로 권한 검사가 충족됨을 코드 추적으로 확인했다. 기능의 권한 확대나 사용자의 역할 재배정은 하지 않는다.
- 적용 전 방어: known35의 id/code/name과 예상 부스바 역할 정의를 확인하고, 알 수 없는 권한 또는 부스바 역할의 사용자·권한 연결이 있으면 전체 전환을 중단한다. 합성 DB에서 기존7개 권한 연결·user_roles·업무/이력 보존, 반례 rollback, 개발 seed 재생성 방지를 확인한다.
- 제거 후보28개: `projects.manage`, `projects.access.all`, `production.plan`, `quality.inspect`, `quality.approve`, `logistics.ship`, `Project.SalesAmount.Read`, `Manufacturing.WorkTime.Read`, `Project.Hold`, `Project.Cancel`, `Project.Deleted.Read`, `PanelInfo.Update`, `Audit.Read.All`, `ProcurementPlan.Update`, `MaterialReceipt.Update`, `ProductionPlan.Update`, `admin-history.read`, `Pending.Read`, `Pending.Manage`, `sales.settle`, `Sales.Target.Manage`, `PendingType.Manage`, `G2.Read`, `G2.Production.Update`, `G2.Delivery.Update`, `G2.Attendance.Update`, `G2.Inventory.Manage`, `G2.Target.Manage`.
- common130 근거상 유지7개의 role_permissions는29개다. 현재 운영의 원래 연결 집합을 마음대로 재설정하지 않고 유지 대상의 기존 연결 자체를 보존한다. 일반 migration0001/0002/0003/0004/0005/0006/0008/0009/0020/0029/0037/0043/0045/0081이 known35 정의를 소유한다.

**독립 보정 완료와 검증 결과 — 2026-09-30**

- 공통 사용자관리: 실제 영향 사업부 전체의 maintenance lease를 Directory 작업 생성 전에 한 번씩 확보하고 local 적용·Directory Publish·응답 snapshot까지 유지한다. 이 endpoint만 middleware의 단일 사업부 lease 대신 store가 책임진다. C/O의 공통 관리 URL 별칭은 거부하고 사업부 `/me`·`/runtime-mode`는 유지했다. 소속 이동 뒤 재요청 및 과거 완료 작업 A→B→A 재요청은 원래 저장된 대상 집합으로 멱등성을 검증하며 현재 버전 반환/무변경을 확인한다.
- 권한과 잔존 객체: D bootstrap/reconcile 모두 migrator의 global/public future-table DML·sequence 기본 권한을 회수하고 SELECT만 유지한다. future-function PUBLIC EXECUTE도 global/public 양쪽에서 회수한다. 실제 새 객체 생성과 runtime 쓰기 실패로 확인했다. O0131은 독립 sequence `pending_issue_number_seq`, `busbar_product_number_seq`만 RESTRICT로 제거하며 C4/O1/D0 목록과 USAGE/SELECT 각각을 검사한다. 시스템/template DB 이름도 구성 시 거부한다.
- 객체 검사: backend 시험에서 D exact8표/7함수, C/O exact표·project/notification/delivery 열·sequence·view·반대 사업부 함수 부재를 검사한다. packaged 시험은 독립 ownership literal로 C182/O56/D8 표·C4/O1/D0 sequence·C0/O4/D0 view, foreign server/table0, 추가 업무 schema 없음, FK의 public 표 참조, 원장/identity 직접 쓰기 거부 및 D current/default ACL을 fresh/upgrade 각각의 최초 적용/재실행 후 확인한다. 전체 trigger/FK 정의를 한 벌의 snapshot으로 비교하는 검사는 아니며 기존 업무 제약·보존 시험과 함께 적용한다.
- 배포 경로: 공개 backend의 split 설정·세 runtime secret 참조·대상 이름/역할/identity를 변경 전과 ready revision에서 확인한다. 공지 준비만으로 serving 검증 성공을 표시하지 않는다. CI는 backend OCI archive를 한 번 빌드하고 hash로 연결한 Docker image의 packaged 시험 통과 후 동일 archive만 게시하도록 변경했다. bootstrap/OCI binding/borrowed image 소유권 시험도 자동 경로에 연결했다. 현재 README를 target별 실행·사업부별 readiness·점검 release 계약에 맞췄다.

| 검증 | 결과와 실제 범위 |
| --- | --- |
| Backend 집중·회귀 | 최종 제품 코드의 broad56에서50 PASS/6 FAIL. 실패6개는 reserved-name 시험 기대 key의 불필요한 `BusinessUnits:` prefix이며 해당 기대값만 고친 재실행6/6 PASS. 한 번의56/56 실행으로 기록하지 않는다. 잠금 거부 시 D/C/O 무변경, 단일/다중 소속, 정상 이동/재요청/과거 완료 재요청, 진행 중 Publish와 점검의 실제 경쟁, ACL/fresh/upgrade/boundary/catalog 검사가 포함된다. |
| 실제 로컬 배포 이미지 | 새 packaged exact 검사까지 반영한 최종 실행 PASS. fresh/upgrade 적용·재실행·drain·catalog/객체/권한 검사 성공. 해당 실행 소유 temp/container/image/DB/network/volume 잔여 모두0. |
| Release 설정 mock | 110/110 PASS. 실제 Azure release 실행은 아니다. |
| 최초 maintenance bootstrap mock | 55/55 PASS. 실제 운영 template 변경은 아니다. |
| 이미지 결속/소유권 | OCI config/layer/root 7가지 합성 경우 PASS(단일 unittest의7 subcase), borrowed image 실패/cleanup6/6 PASS. 실제 Skopeo→registry 게시/원격 CI는 미실행. |
| 정적 검사 | Bash syntax·ShellCheck·actionlint·diff 검사 및 Azure aggregate validator PASS. Bicep 파일 변경 없음, compile은 NOT_REQUESTED. |

- 로그: `/private/tmp/business-schema-audit-fixes-boundaries.log`, `/private/tmp/business-schema-reserved-name-tests.log`, `/private/tmp/business-schema-audit-fixes-image-exact.log`, `/private/tmp/business-schema-release-serving-tests.log`, `/private/tmp/business-schema-bootstrap-ci-tests.log`, `/private/tmp/business-schema-borrowed-image-tests.log`, `/private/tmp/business-schema-oci-binding-tests.log`, `/private/tmp/business-schema-azure-artifacts-debug.log`. 합성 fixture만 사용하고 실제 provider/운영 연결은 열지 않았다.
- 발견/실패 이력: 첫 집중 실행31개 중 재요청2개 실패를 제품 보정 후3/3 및 최종 broad에서 해소했다. 초기 시험 파일 using 누락은 compile 전 보정했다. reviewer가 과거 완료 작업의 대상 변경 및 sequence 권한 문자열의 OR 의미를 추가 지적하여 코드/시험을 고쳤다. Azure aggregate 첫 exit1은 원인 미확정이지만 상세 trace 재실행은 PASS이며 재현되지 않았다. 실패를 성공 횟수에 포함하지 않는다.
- 독립 검토: parent 작성 maintenance/route/config 및 `business_schema_migrations` 작성 ACL/sequence/tests는 작성과 분리된 `review_db_tunnel`이 `f03c21c` 대비 최종 backend/SQL/tests11파일과 위 결과를 검토하여 해당 범위 GO, 추가 P1/P2 없음으로 판정했다. 기존 reviewer 맥락을 재사용했으며 실제 관측 모델은 NOT_REPORTED다. `review_db_tunnel` 작성 CI/release/image/script/docs는 parent가 별도 검토했고 최신 packaged SQL assertion을 실제 이미지에서 실행했다. 자기 작성 부분을 독립 검토라고 세지 않는다.
- 프론트엔드 파일은 이번 보정에서 변경하지 않았다. 앞선 API/route/deep-link23/23 결과를 재사용하며 전체 UI 수동 검수·전체 최종 회귀를 새로 완료했다고 주장하지 않는다.

**남은 범위와 Git/운영 상태**

1. 위 오산 권한28개·연결 행·미사용 역할1개 삭제 파일 작성의 명시 승인 또는 보류 결정. 현재 migration/seeder의 기준정보35권한/11역할 보존은 그대로이며 전체 최소화 완료가 아니다.
2. 불명확 발송1건의 별도 처리 결정/근거 기록, 복구 준비·적용 창 재점검. 이 보정은 기존 drain 차단을 우회하거나 메일 상태를 변경하지 않는다.
3. 사용자 검수, 원격 required CI 및 실제 게시 경로 검증, 명시 승인된 운영 전환. 운영 C209/O209/D8과 앱1개를 변경하지 않았다.

이번 완료 변경은 기존 branch에서 로컬 commit 대상으로만 묶는다. `main`/사용자 원본 checkout의 WIP, push/PR/merge/배포는 변경하지 않는다. 로컬 코드 보정 GO와 전체 정리/운영 전환 NO-GO를 구분한다.

### 오산 기준정보 정리 승인과 추가 전체 검증 — 2026-09-30

- 사용자 명시 승인: **“승인. 작업 완료 후 다시 한번 전체적으로 추가 검증 진행해.”** 직전 답변에 명시한 오산 권한28개·해당 role_permissions 연결·미사용 `interior-busbar-manager` 역할1개 삭제의 로컬 파일 작성/합성 DB 검증을 승인했다. 앞 절 자동 승인 검토의 승인 부족 사유를 이 사용자 답변으로 해소했다. 운영 실행·발송/메일 상태 정정·원격 게시/병합/배포 승인은 확대하지 않는다.
- 기준선 `1f2fad4`, 기존 `codex/business-schema-separation` 작업 폴더 clean에서 재개했다. 목표는 O permissions7/roles10 및 기존7권한 연결·departments10·user_roles·업무/감사/설정 보존이다. C/D 및 backend1개/자원량, C182/O56/D8 표 계약은 유지한다.
- 담당 경계: 기존 `business_schema_migrations`가 O0131/seeder/backend 시험을 작성하고, parent가 packaged 검사/문서/검증을 수행한다. 작성과 분리된 `review_db_tunnel`이 유지 권한의 실제 endpoint 사용과 최종 diff를 검토한다. 기존 agent 맥락을 재사용하며 실제 관측 모델은 NOT_REPORTED다.
- 이번 변경을 포함하여 합성 DB의 fresh/upgrade·보존/rollback·권한 allow/deny·3-DB 경계/점검 경쟁, frontend 요청 경로, 실제 배포용 이미지와 release 검사를 추가 확인했다. 최종 결과와 제한은 아래와 같다.

**구현과 독립 검토**

- O0131은 기존35권한의 id/code/name을 정확히 검사한 뒤 승인된28권한과 그 연결만 제거한다. `interior-busbar-manager`는 정의가 예상과 같고 사용자 배정·권한 연결이 모두 없을 때만 제거한다. retained7의 기존 연결, 사용자 정의 역할/연결, 사용자 배정·부서·업무/감사/설정을 보존한다. 청주35권한/기본11역할은 그대로다.
- O 개발 seed에서 공통 청주 권한 seed를 제외하여 반복 실행해도 삭제된 권한을 재생성하지 않는다. packaged fresh/upgrade/reapply 검사에 C35:11:10:111/O7:10:10:29의 순수 합성 기준을 추가했다. 운영/사용자 정의 역할의 연결 개수를29개로 강제하는 검사가 아니다.
- 반례는 모르는 권한·이름 변경·부스바 역할 정의 변경·사용자 배정·권한 연결과 늦은 FK 실패를 실제로 실행하여 rollback을 확인한다. 정상 전환에서는 사용자 정의 역할과 유지 권한 연결을 넣고, 두 차례 seed 후 영업·품질·설계·관리자·사용자 정의 역할의 실제 profile 허용/거부를 검사한다.
- 추가 전체 검사에서 발견한 예전 테스트 준비 방식도 보정했다. G2/부스바 권한 HTTP 시험은 명시 청주 bootstrap과 청주0131까지 준비한다. trusted Osan 반례17개는 `/osan/api/...` 경로를 거쳐 정확한 capability 거부를 확인한다. ReviewSafe의 일반 Development 등록 시험은 실제 scoped notification handler를 scope 안에서 조회한다. 제품 권한·응답 기대를 완화하지 않았다.
- `review_db_tunnel`은 `1f2fad4` 대비 taxonomy/seeder/backend 검사의 고정 diff, parent의 packaged assertion·ReviewSafe 시험 보정, 추가 G2/부스바 준비/경로 보정을 독립 검토하여 GO, 추가 P1/P2 없음으로 판정했다. FE 호출 경로→고정 사업부 URL→endpoint 권한→DB/worker 연결도 다시 대조하여 새 교차 접근 경로를 찾지 못했다. trusted context 시험은 실제 Directory 인증/연결 추적을 대신하지 않으며, 이를 별도3-DB 브라우저 시험과 구분한다. 실제 관측 모델은 NOT_REPORTED다.

**추가 전체 검증 중 발견한 브라우저 시험 보정**

- 일반 full-stack의 QR 발급, Excel 패널 적용, 프로젝트 재활성 응답 대기와 영업 KPI mock이 옛 `/api/...` exact 경로를 사용했다. 현재 FE의 `/cheongju/api/...`로 맞췄으며 HTTP method·project ID·endpoint·성공 상태 및 DB/화면 검증은 유지했다. QR은 실제 발급 후 observer 대기에서 timeout이었음을 최종 stack으로 확인했다. 나머지도 전체 실행에서 동일 관찰/fixture 문제로 실패했고 수정본 재실행은 통과했다.
- G2는9/30 기본9월 표에서10/1 요소를 찾았다. UI로 실제 월을 이동하고 해당 월의 min/max와 날짜 필터를 확인한다. tomorrow/모레가 같은 월이면 기존 임시 예상 재고29 두 열을, 다른 월이면 첫 월의 열 부재와 다음 월의 재고32를 각각 확인한다. 주말/미래 색상 검사 사이에는 상세 상태를 명시적으로 접어 확정한다. UI 제품·API·계산·권한 기대는 변경하지 않았다.
- G2 첫 보충 실행은 좁힌2일 표의 가운데 정렬 오차1.015625가 기존1px 기준을 넘어서 실패했다. 실제 날짜 필터 검증 후 원래 월 전체 표로 돌아와 geometry와1440/390px 폭 검증을 수행하도록 보정했으며 tolerance/CSS는 그대로다. 좁힌 기간의 geometry까지 통과했다고 해석하지 않는다. 독립 검토에서 동일월 상세 상태·월말 전날 월 분리·월전체 폭 검증 유지 지적을 모두 보정한 뒤 최종 GO를 받았다.

| 이번 추가 검증 | 결과·범위 |
| --- | --- |
| 오산 기준정보·schema 집중 | 5/5 PASS. fresh/upgrade/store/guard, exact 정의, 보존·rollback, 사용자 정의 연결, seed 반복 및 실제 권한 profile 반례. |
| Backend 전체 | 1032건 중915 PASS/25 FAIL/92 skip,46분41초. 실패25건은 아래25/25 재검증으로 해소했고, skip92건은 모두 InteriorBusbar 계열이며 아래 전용 DB 실행의 통과 항목에 포함된다. 모든 대상의 최종 증거를 확보했지만 한 번에1032/1032 통과한 실행으로 기록하지 않는다. |
| Backend 실패 보정 재검증 | 25/25 PASS, skip0. G2 권한3/부스바 기존 역할4/오산 capability17/Development 등록1. 전체 검사와 다른 빌드 출력 폴더 및 임시 DB에서 실행하여 진행 중인 DLL/DB를 덮어쓰지 않았다. |
| 부스바 전용 DB 보충 | 별도 합성 `busbar_test`에서265건 중248 PASS/17 FAIL/skip0. 실패17은 위 경로 보정 재검증17/17로 해소했다. 첫 전체 검사의 전용 DB 미설정 skip은 이 실행으로 보충하며 skip을 PASS로 세지 않는다. |
| Frontend 전체 | Vitest76파일586/586 PASS. 실제3-DB 브라우저 실행에서 production frontend build도 PASS. |
| 일반 업무 브라우저 | 64건 중59 PASS/5 FAIL 후 영향받는5건 최종 재검증 PASS(QR1, G2최종1, Excel/재활성/KPI3). 한 번에64/64 통과한 실행으로 기록하지 않는다.12면 혼합자재·분할 입고·반복 Pending 완료 및18단계 부서별 프로젝트 전 과정 포함. 이 일반 suite는 legacy 단일 합성 DB이며 일부 명시 mock을 포함하므로 전부3-DB 실거래 검사라고 표현하지 않는다. |
| 사업부 전용 브라우저 | 오산 등록·목록·상세1/1, 공통 사용자관리/탭 소속/권한·격리 초기화1/1 PASS. 실제 C/O/D 임시 DB와 제한된 역할 사용. 오산 등록 뒤 C 프로젝트 행 수 불변, O 프로젝트+1·대상1·단계7을 직접 조회했다. |
| 실제 배포용 이미지 | 최종 taxonomy 포함 fresh/upgrade/reapply·drain·exact schema/객체/ACL/권한 집합 PASS. 해당 실행 소유 임시 자원 잔여0 확인. |
| 배포 절차 검사 | release mock110/110, maintenance bootstrap mock55/55, OCI binding7 subcase, borrowed image cleanup6/6 PASS. 실제 Azure/registry/원격 CI 실행은 아니다. |
| 정적 검사 | 변경 script Bash syntax·ShellCheck `-x`, 변경 browser4파일 ESLint, diff 검사 PASS. |

- 증거: `/private/tmp/business-schema-taxonomy-focused.log`, `/private/tmp/business-schema-final-backend-all.log`, `/private/tmp/business-schema-final-backend-recheck.log`, `/private/tmp/business-schema-final-busbar-tests.log`, `/private/tmp/business-schema-final-frontend-tests.log`, `/private/tmp/business-schema-final-general-browser.log`, `/private/tmp/business-schema-final-browser-recheck.log`, `/private/tmp/business-schema-final-g2-browser.log`, `/private/tmp/business-schema-final-fixed-route-browser.log`, `/private/tmp/business-schema-final-osan-browser-ready.log`, `/private/tmp/business-schema-final-access-browser.log`, `/private/tmp/business-schema-final-production-image.log`, `/private/tmp/business-schema-final-release-tests.log`, `/private/tmp/business-schema-final-bootstrap-tests.log`, `/private/tmp/business-schema-final-browser-lint.log`.
- 최초 오산 브라우저 실행은 테스트용 Chromium 미설치로 실행 전 실패했다. 프로젝트에 맞는 headless browser를 임시 폴더에 준비한 뒤 정상 재실행했다. 사용자 브라우저/설정은 변경하지 않았다. 일반/보충 브라우저의 임시 DB·process·Compose 자원은 각 wrapper cleanup을 통과했다. 새로 생성한 합성 screenshot/출력 workbook은 임시 증빙 폴더에 보관하고 tracked108개/untracked5개만 작업 폴더에서 원상 복구했다. 로그·browser cache·증빙 파일이 있는 임시 폴더 전체를 삭제했다고 주장하지 않는다.
- 위 일반 browser4파일의 최종 diff도 작성과 분리된 reviewer가 검토했다. 제품 UI를 변경한 작업이 아니며 모든 화면을 사람이 직접 검수했다는 뜻은 아니다. 사용자 검수와 원격 required CI는 별도 남아 있다.

**최종 판정과 남은 운영 범위**

- 승인된 로컬 기준정보 정리·추가 전체 검증은 완료(GO)다. 최종 전체 backend의 추가 실패는 위 보정 대상25건뿐이며 전용 DB로 보충한 skip92건 외의 미실행 항목은 없다. 최초 실행·보정·보충 결과를 구분해 기록했고 검증 기대를 느슨하게 바꾸지 않았다.
- 전체 검사 종료 뒤 이번 실행 소유 backend/browser/busbar/image 임시 환경이 정리됐음을 확인했다. 기존 `emi-qms-e2e-osan20260924` 컨테이너와 원본 checkout의 WIP는 유지했다. 변경16파일만 기존 `codex/business-schema-separation`의 로컬 commit 범위이며 push·PR·merge·배포는 하지 않는다.
- 운영 전환 NO-GO는 유지한다. 별도 운영 판단이 필요한 불명확 발송1건의 처리 결과/근거, 복구 준비·적용 창 점검, 사용자 검수 및 원격 required CI/실제 게시 경로 검증이 남아 있다. 이번 승인을 운영 DB 실행이나 발송/상태 정정 승인으로 확대하지 않았다.
- 따라서 현재 운영에 남은 C209/O209/D8 표나 기존 오산 권한 정의가 사라졌다고 표현하지 않는다. C182/O56/D8 및 O7권한/기본10역할은 검증된 전환 목표다. 앱 수·Azure 자원량·비용 구성은 변경하지 않았다.

### 최종 전수 점검과 발견 사항 보정 — 2026-09-30

- 사용자 요청: “좋아. 마지막으로 최종 점검 한번 해봐. 하나하나 꼼꼼하게”. 기존 branch의 clean HEAD `9a86513`을 기준으로 확정 목록·누적 구현·기존 검증 증거를 대조하고, 발견한 결함의 범위 내 로컬 보정과 합성 검증을 이어간다. 운영 DB·실제 provider·메일 상태·원격 게시·배포는 변경하지 않는다.
- 표 소유 전수 대조: 문서에서209개 이름을 독립 추출해 양0131의 최종 목록과 비교했다. C182/O56, 양쪽 독립29/C전용153/O전용27이 정확히 일치한다. 삭제 C27표는 빈표23/초기값검사4, O153표는 빈표134/초기값검사19로 모두 사전 데이터 검사가 있다. common0001..0130·Directory migrations·Azure workload 자원 정의는 기존 배포 기준 `02028f2` 대비 변경0이다.
- `final_schema_audit`은 새 맥락에서 표/열/함수/sequence·권한35→7·기본역할11→10·보존 snapshot·transaction/rollback·seed 반복·실제 기존 로그를 독립 확인하여 해당 범위 GO를 반환했다. `review_db_tunnel`은 기존 작성과 분리된 맥락에서 FE→route/capability→고정 DB, 공통관리 lease, 알림/파일/출력/worker·migration 경계를 재검토했다. 요청 모델은 신규 reviewer `gpt-6-astra/high`, 도구가 실제 모델을 반환하지 않아 관측값은 NOT_REPORTED다.
- **FINAL-P1-01 / 해소:** 청주 `InteriorBusbarEcountWorker`와 `InteriorBusbarPublicationWorker`는 identity만 확인하여 정상 marker·Idle 상태에서0131 미적용/원장 불일치여도 외부 처리와 DB 변경을 시작할 수 있었다. HTTP 및 일반 알림/에스컬레이션/삭제의 boundary 검사는 유지되어 있고 이 finding에 포함하지 않는다. 두 worker에 필수 validator를 주입하고, enabled 확인→청주 maintenance lease→multiDB exact identity/ledger 확인→기존 업무 connection/mutex/첫 변경/provider 순서로 보정했다. legacy·disabled 동작을 보존한다. 실제 C/O/D 합성 fixture의 pending 작업으로 missing/extra ledger×두 worker를 각각 실행해 provider 인증/전송/게시0과 C/O 업무 snapshot 불변을 확인했다. 정상 경우 인증·전송·게시 각1회, 청주 완료 상태와 오산 불변을 확인했다.
- **FINAL-P2-01 / 해소:** 배포 workflow의0131 승인 설명에 최신 오산 권한28개·해당 역할 연결·미사용 역할1개와 불필요 객체 정리가 빠져 있었다. workflow 설명과 database/Azure README·최초 전환 안내를 실제 삭제 범위와 맞췄다. 실행 gate·기본 false·운영 승인 범위는 바꾸지 않았고, 작성과 분리된 `final_schema_audit` 검토 GO 및 actionlint·diff 검사 PASS다.
- 보정 전 기준선의 이번 집중 실행: backend48/48 PASS(표/보존/rollback/권한/점검 경쟁/3-DB/외부 작업 drain), frontend3파일23/23 PASS(고정 URL·사업부 전환·늦은 응답·알림 링크). 두 worker ledger 누락은 기존48개가 잡지 못한 새 반례로 구분한다. 로그는 `/private/tmp/business-schema-last-audit-backend.log`, `/private/tmp/business-schema-last-audit-frontend-complete.log`이며 임시 DB/Compose cleanup 정상 종료를 확인했다. 첫 backend 시도는 sandbox의 Docker socket 접근 제한으로 실행 전 실패했고 정상 escalation 허용 후 재실행했다. 첫 frontend 선택은 실제1파일3건만 실행되어 정확한3파일을 지정한23건으로 보충했다.
- 결함 재현: 제품 보정 전 `/private/tmp/business-schema-last-workers-red-complete.log`에서 거부 반례4개가 모두 예외를 발생시키지 않아 FAIL, 정상1개 PASS, skip0이었다. 첫 예비 RED는 Ecount2반례를 재현했으나 정상 fixture의 등록자 표시명이30자를 넘어 전송이 보류됐다. 합성 이름만 기존 업무 계약에 맞게 보정하고, 두 worker를 독립 반례로 나눠 최종 RED5개를 실행했다. 제품 이름 제한·업무 기대값을 완화하지 않았다.
- 보정 후 최종 집중 검사: `/private/tmp/business-schema-last-audit-backend-fixed.log` **53/53 PASS, skip0, 1분33초**. 새 worker5개와 기존48개를 같은 최종 빌드로 확인했다. 별도 disposable `busbar_test`를 준비하여 같은 빌드의 EcountWorker/Publication/Lifecycle3클래스는 `/private/tmp/business-schema-last-worker-regression.log` **35/35 PASS, skip0**다. 외부 provider는 fake만 사용했고 두 실행의 소유 DB/container/network cleanup이 exit0으로 완료됐다.
- 독립 보정 검토: `review_db_tunnel`이 제품2파일·기존 ctor 시험3파일·신규 worker 경계시험1파일을 검토하여 GO, 추가 P1/P2 없음으로 판정했다. 기존 검사에 포함되지 않았던 두 worker의 거부/허용 분기를 실제 실행한 증거와 일치한다. P2 설명4파일은 별도 신규 reviewer가 확인했다. 최종 actionlint·diff 검사 PASS다.
- 최종 범위: 이번 변경은 기존 branch의 로컬11파일이다. DB schema·권한 목록·앱 수·Azure 자원량은 추가 변경하지 않았다. 전체 backend/frontend/browser suite와 배포용 이미지의 이전 실행 기록은 위 절에 보존하며, 이번에 전체 회귀나 새 image 게시 검증을 다시 실행했다고 표현하지 않는다. 남은 운영 전환 조건은 불명확 발송1건 처리 근거, 복구/적용 창, 사용자 검수, 최종 source의 원격 required CI·게시/이미지 검증 및 명시 운영 승인이다. 원본 checkout WIP·기존 runtime·운영 DB·메일 상태·push/PR/merge/배포는 변경하지 않았다.

### 운영 전환 준비와 최신 운영 사전 점검 — 2026-10-01

- 사용자 “시작해”에 따라 최종 코드 이미지 검증, 운영 읽기 전용 사전 점검, 불명확 발송 근거 확대 조회 및 원격 제출 준비를 이어갔다. 시작 source는 clean `49029e2`, 같은 `codex/business-schema-separation` worktree를 사용했다. 이번 변경은 준비/검증 기록뿐이며 제품 코드·DB·메일 상태·자원 설정은 변경하지 않았다.
- **최종 로컬 이미지 PASS:** `/private/tmp/business-schema-release-ready-image-20261001.log`에서 실제 production image build, business/Directory catalog exact, fresh·기존 DB 적용 및 reduced schema 검사가 모두 통과했다. harness가 이번 실행 소유 temp/container/image/DB/Compose/network/volume 잔여0을 확인했고 exit0으로 종료했다. registry 게시나 운영 배포 image 검증으로 확대하지 않는다.
- **운영 DB 426표 읽기 전용 재확인(08:32 KST):** C209/O209/D8 및 공통130/130·Directory4 원장이 이전 catalog와 정확히 일치한다. 표/프로젝트 열의 누락·추가0, 빈 표 조건 위반0, 허용 초기값·사업부 데이터 조건 위반0, O35권한 정의 차이0, 제거 역할의 사용자/권한 연결0이었다. C23/O134개의 빈 삭제 대상과 C4/O19개의 초기값 보유 삭제 대상을 재확인했다. 미검증 제약/invalid index·prepared transaction·대기 lock0이다. 19개 사전조건 SELECT와 모든 조회의 오류0이며 실제0131/DDL/consent 설정은 실행하지 않았다. 기존 일반 감사 함수3개의 문자열 참조는 전날 확인한 동일 정의 hash이며 신규 미설계 의존으로 판정하지 않는다.
- **경계·작업 상태:** runtime CONNECT 권한은 자기 DB3/3 허용·다른 DB6/6 거부로 유지된다. 이는 이번 권한 조회 결과이며 실제 다른 DB 로그인 반례 실행은 전날 증거와 구분한다. Directory 현재8표의 직접 DML 권한0, 미래 객체 default ACL 보정은 최종 로컬 코드에 있고 운영 미적용이다. C/O Processing delivery0, C 부스바 job/attempt InFlight·Unknown0, O 부스바 업무행0이다. 두 사업부 점검 상태는 Completed다. 과거 불명확 발송1건은 그대로이며 서비스 가동 중 다른 client session을 차단하는 실제 drain CLI를 실행한 것은 아니다.
- **메일 근거 확대(08:31 KST):** 기존 운영 발신 계정에 IMAP EXAMINE 및 BODY.PEEK 헤더만 사용했다. 2026-09-22 포함~09-24 미포함의 보낸편지함1036개, 전체메일1054개를 각각 확인했고 휴지통/스팸함은 해당 날짜0개였다. 두 큰 폴더는 중복되므로 합산한 고유 메일 수로 표현하지 않는다. 수신자+정규화 제목 일치0, 비교용 정상 Sent10건은 두 폴더에서10/10 일치한다. 불명확 시도는 9월22일22:21~22:27 KST, attempt3, provider ID 없음, 관련 같은 수신자/제목 delivery2건 모두 Failed·sent시각 없음이다. 메일 원문·수신자·제목·secret은 기록하지 않았다. 부재는 미발송 확정 근거가 아니며 발송/재발송·삭제·읽음 변경·상태 정정을 하지 않았다.
- **운영 Azure/공개 상태(08:25~08:35 KST):** 기존 Backend1개(1vCPU/2GiB, min/max1)와 Frontend가 Healthy/Running 상태다. 기존 manual Job4개의 조회 결과에 Running execution은 없고 영구 target/approval/drain/maintenance 설정·중복 설정0이다. PostgreSQL16/Ready/private network, storage32GiB/autogrow 및 백업14일을 확인했다. 조회된 자동 full backup14개 중 최신 완료는 2026-09-30 08:42 KST다. 공개 `/health/live`200, 익명 `/`401, `/api/me`401을 확인했다. 운영 source/revision과 workload 크기는 변경하지 않았다.
- **복구 준비 판단:** 운영 `Operations__Backup__RestoreVerifiedAtUtc`는 `2026-09-08T02:37:40Z`다. Change032의 세 DB PITR 복구/ledger·identity·aggregate 검증 및 임시 자원 정리 기록과 일치하며 현재 제품의90일 유효기간 안이다. 과거 검증된 복구 능력과 현재14일 백업을 확인한 것이며, 현재0130 데이터의 새 restore rehearsal이나0131 이후 복구를 시험한 것으로 표현하지 않는다. 새 유료 restore 서버를 자동으로 만들지 않았다. 실제 전환 직전에 데이터/원장 기준선, 사용 가능한 변경 전 복구 시점, 점검 창과 실패 시 복구 담당/절차를 확정해야 한다.
- **원격 제출 준비:** 읽기 전용 Git 조회에서 원격 `main`은 `02028f2739af3da197a047008f448b3f3e95d230`, 해당 작업 원격 branch·PR은 없다. 제출 대상은 `codex/business-schema-separation`→`main`, Draft 제목은 `사업부별 DB 구조와 처리 경계를 단일 백엔드에서 분리`다. 기존 PR 양식에 맞춘 본문을 `/private/tmp/business-schema-release-draft-pr-20261001.md`로 준비했다. 구현/검증 결과, 초기 실패와 보충 검사, 삭제 범위, DB 변경 이후 구 image로 되돌릴 수 없는 경계와 미해결 메일을 명시한다. 원격 required CI·main 병합·이미지 게시/배포는 아직 수행하지 않았다.
- **남은 판단:** 현재 drain 계약은 진행 중 작업뿐 아니라 모든 과거 `LeaseExpiredAfterProviderCallStarted`/provider 시작 후 `OwnershipLost`를 차단한다. 메일을 찾지 못했다는 이유로 거짓 Sent/미발송 상태를 쓰거나 이력을 삭제해 통과시킬 수 없다. 외부 결과 확인 또는 원래 불명확 이력을 보존하는 별도 건별 처리 정책의 명시 결정이 필요하며, 이번에 자동 예외/상태 정정 기능을 추가하지 않았다. 이 운영 판단과 별개로 사용자 검수·Draft PR/원격 CI 준비를 진행할 수 있다. 실제 DB 정리·점검시간·배포 및 해당 main 병합은 정확한 대상/행동의 승인 범위를 확인한다.
- 증거 파일은 `/private/tmp/pms-preflight-20261001-result.json`, `-final-metadata-result.json`, `-mail-folders-result.json`, `-azure-result.json`과 위 image log다(모두 첫 파일과 같은 prefix). credential은 자식 process stdin/메모리에만 전달하고 집계/메타데이터만0600 파일로 보관했다. 이번 조사 소유 임시 TLS 중계는 정상 종료했고 사용자 기존 중계·원본 checkout WIP는 유지했다. 첫 시스템 Python 연결 준비는 의존 모듈 부재로 연결 전에 종료됐으며 기존 도구가 지정한 Azure CLI Python으로 정상 수행했다. 이번 자동 승인 거부는 없었다.

### 원격 작업 브랜치·Draft PR·CI 명시 승인 — 2026-10-01

- 사용자는 직전의 “작업 브랜치를 GitHub에 올리고, 초안 PR을 만들어 자동 검사(CI)까지 진행” 요청에 **“시작해”**로 승인했다. 대상은 `emisubin/emi-qms`의 `codex/business-schema-separation`→`main` Draft PR이다. main 병합·운영 DB 정리·메일 상태 정정·이미지 게시/운영 배포는 이번 승인에 포함하지 않는다. 앞 절까지의 로컬 한정 승인을 이 원격 게시 승인으로 갱신하며 별도 재승인을 반복하지 않는다.
- 게시 전 clean `c85b20a`, 원격 main `02028f2739af3da197a047008f448b3f3e95d230`, 동일 원격 branch/PR 없음 및 누적181파일의 대상 경로·diff 검사를 확인했다. 실제 secret/token/private key의 고신뢰 패턴과 환경파일·dump·로그·생성물 경로 검사에서 발견0이다. 이는 제한된 패턴 검사이며 모든 비밀값 부재를 자동 증명한다고 표현하지 않는다.
- 기존 제품 코드와 독립 검토/검증 증거를 유지하고 이 승인 기록만 추가해 게시한다. CI는 PR에서 backend/frontend/full-stack/workflow 검사를 실행하며, Azure 배포는 별도 수동 workflow이므로 이번에 실행하지 않는다. 실제 원격 결과의 기준은 게시한 exact head와 연결된 PR Checks/Actions이며, 실패 시 해당 실행의 근거를 확인하고 승인된 범위의 보정·관련 검증을 진행한다. 사용자 검수와 운영 전환 미완료 조건은 유지한다.

### Draft PR 게시와 mock 브라우저 경로 보정 — 2026-10-01

- `codex/business-schema-separation`의 `6ab8fc5389e78e87cdbe88f77e9a97a4f5925f48`를 원격에 게시하고 [Draft PR159](https://github.com/emisubin/emi-qms/pull/159)를 생성·현재 대화에 연결했다. [최초 CI run](https://github.com/emisubin/emi-qms/actions/runs/36792522450)이 시작됐고 Change Classification·Workflow Validation은 통과했다. main의 classic branch protection은 없지만 별도 branch ruleset이 PR과 `CI Gate`를 필수로 요구함을 읽기 전용 API로 확인했다. 보호 설정은 변경하지 않았다.
- **CI-MOCK-P2-01 / 로컬 해소:** CI의 화면 검사 진행 중 mock UI6개 파일이 예전 `/api/...`를 응답 조회 키로 사용해 새 `/access/api/...`, `/cheongju/api/...`, `/osan/api/...` 요청을 받지 못하는 공백을 확인했다. 수정 전 공통 관리자 사례1개에서 초기 사용자 응답을 받지 못해 사업부 선택 UI가 나타나지 않는 실패를 실제 재현했다. 제품 코드·서버 경계를 예전 주소로 되돌리지 않았다.
- mock 공통 helper는 고정 URL·선택 헤더·fixture 허용 사업부가 일치하는지 먼저 검사한 뒤 기존 합성 응답 조회용 경로로 변환한다. 무접두사 주소·다른 사업부 주소·업무 요청의 `/access` 사용·선택 전 identity의 업무 URL을 거부하는4반례와 의도한3경로를 직접 확인했다. 개별 mock override와 response wait도 명시적인 청주/오산 주소로 변경했고 기존 payload·저장 결과·경쟁 요청·오류·화면 assertion을 유지한다. 이 fixture 검사를 실제 서버의 권한/DB 거부 시험으로 확대하지 않는다.
- 보정 전1FAIL(`/private/tmp/business-schema-pr159-mock-red.log`)→동일 사례1PASS(`-mock-green.log`)→전체 mock 브라우저 **71/71 PASS, retry0, 1분48초**(`-mock-all.log`, 모두 같은 prefix)다. 변경7개 TypeScript 파일 ESLint와 diff 검사도 통과했다. 최초 시험 이름 필터 실행은 사례0개여서 재현 증거에서 제외했다. 최초 pnpm lint 시도는 package-manager 사전 확인 오류로 실행되지 않았고 기존 설치된 ESLint를 직접 실행해 통과했다.
- 작성과 분리된 신규 `review_pr159_mock_routes`가 HEAD `6ab8fc5` 위의6개 spec과 신규 helper를 검토하여 GO/P1·P2 없음으로 판정했다. helper 검토 hash는 `2e3e2dcfb936fd622640571541568b7468e190817207af92dca914633415d0cb`이며 payload/경쟁/업무 기대값이 약화되지 않았음을 확인했다. 요청 모델은 `gpt-6-astra/high`, 실제 모델 식별값은 NOT_REPORTED다. 검토 당시 전체 suite는 진행 중이었고 완료 결과71/71은 이후 책임 실행 결과로 구분한다.
- 실행이 생성한 합성 screenshot14개는 조사 소유 임시 폴더에 보존하고 해당 tracked 원본만 복구했으며 시험용5173 listener 종료를 확인했다. 후속 게시 범위는 시험 코드7개와 이 기록1개다. 제품·DB migration·Azure 설정 변경0이며 기존 원격 게시 승인 범위에서 같은 PR에 반영한다. 수정된 exact head의 필수 CI 결과는 [PR159 Checks](https://github.com/emisubin/emi-qms/pull/159/checks)에서 확인하며 이전 head의 미완료 실행을 통과 근거로 사용하지 않는다. main 병합·운영 DB/메일 상태 변경·배포 및 사용자 검수는 여전히 미완료다.

### 과거 메일 1건 예외·검수 생략·배포/복구 준비 — 2026-10-01

- 사용자 결정: “발송여부 불명확한 메일 1건 그냥 잊어버려. 다음번에 또 나오면 그때 다시 얘기해보자.” 이후 제안한 1번(그 과거 1건만 배포 차단 제외), 2번(사용자 검수), 3번(배포 시간·복구 계획)에 대해 **“1,2,3까지 한번에 진행하자. 사용자 검수는 필요없어보임.”**이라고 명시 승인했다. 과거 1건은 조사 종결하며 새 발생 건만 다시 다룬다. 수동 사용자 검수는 이 범위에서 생략한 것이며 검수 실행/통과로 기록하지 않는다. 기존 같은 PR의 수정·필수 CI 갱신 승인은 유지한다. 4번 이후인 공지 게시·main 병합·이미지 게시·운영 DB 정리·앱 교체·유료 복구 자원 생성은 이번에 실행하지 않는다.
- 기준선: clean `f69b19d` 및 [CI 36793904518](https://github.com/emisubin/emi-qms/actions/runs/36793904518). Backend1037/1037(skip0), Frontend586/586, mock71/71, full-stack64+2/66, Workflow Validation 및 CI Gate를 포함한6 jobs 모두 PASS를 확인했고 PR 본문에 기록했다. 이 결과를 아래 제품 보정의 새 CI 결과로 재사용하지 않는다.
- 변경 계약: read-only drain이 승인된 **오산 과거 메일 1건의 정확한 SHA-256 snapshot**만 허용한다. 실제 DB 이름·OSAN·attempt/delivery ID·번호/generation·outcome·provider 시작/완료 시각·channel/status에 결합한다. Failed/Mail, 완료된 시도, provider ID·sent 시각·claim·다음 재시도 없음 및 현재 delivery의 시도/generation 일치가 필요하다. 불명확 기록이2건 이상이면 차단한다. 원래 발송 결과·관리 상태·업무/감사 이력과 표·권한 목록은 변경하지 않는다.
- 전달 경계: optional64자리 소문자 hex1개, 기본 예외 없음. 최초/일반 release의 OSAN drain 실행에만 전달하고 다른 DB·구조 변경·권한 준비·앱에는 전달하지 않는다. 영구 Job template의 예약 설정은 계속 거부한다. 실제 값은 비공개 준비 파일에 보관하며 승인된 운영 전환 시에만 일반 workflow의 Environment secret/최초 전환 env로 설정한다. 새로운 값을 자동 생성·갱신하거나 wildcard/list를 허용하지 않는다.
- 검증 진행: 원래 코드에서 정확한 승인 snapshot도 거부되는1FAIL을 재현했다(`/private/tmp/business-schema-historical-mail-red.log`). 초기 준비 실행은 스크립트 실행 비트 부재, 첫 fixture는 현행 오산 INSERT guard로 각각 실행 전/시험 준비에서 종료되어 재현 근거에서 제외했다. 역사 fixture를 합성 DB에서만 준비하고 INSERT guard를 복구한 뒤 실제 drain을 실행한다. 보정 후 기존 drain+신규 예외 통합2/2 PASS, skip0(`/private/tmp/business-schema-historical-mail-guards.log`). 신규 시험은 새/추가 attempt, Processing, 다른 DB, 잘못된 token, timestamp/outcome/provider ID/재시도·generation 변경, 다른 channel 및 Pending을 거부하고 원 이력 불변을 확인한다. 최초 전환59/59·일반 배포114/114 모의 시험 PASS다. 중간 일반 배포는 macOS Bash3의 빈 배열+nounset 오류가 발견되어 기존 호환 구문으로 보정 후114개를 통과했다. 해당 실행 소유 임시 DB/Compose 정리 완료, 실제 provider 호출0.
- 준비 범위: 운영 백업·앱/작업 상태와 세 DB의 최소 메타데이터를 읽기 전용으로 확인한다. 메일함·수신자·제목·본문 재조사는 하지 않고, 이미 승인된 과거 시각/종료 상태와 일치할 때만 비식별 snapshot을 준비한다. 배포 시각 선호를 비동기로 요청했으며 별도 답변이 없으면 기술 준비와 최종 실행 승인 후 가장 이른 시점을 시작 기준으로 삼는다. 현재는 실제 중단·공지·예약 실행을 하지 않는다. 복구 계획·최종 검증/검토 결과는 이어지는 기록에서 확정한다.
- 읽기 전용 준비 완료: 10/1 10:59 KST Azure 관측에서 양 앱 Running, 기존 backend 한 개, Manual job4개에 실행 중 작업0, PostgreSQL Ready/보관14일을 확인했다. 완료된 최신 backup은 10/1 08:42:42 KST이고 earliest restore는9/18 08:35:31 KST다. 기존 복구 연습 설정은9/8 02:37:40 UTC로 유지된다. 11:09 KST 세 DB를 `transaction_read_only=on`으로 확인한 표/원장은 C209/130, O209/130, D8/4다. 따라서 운영0131은 아직 적용되지 않았다.
- 과거 건 준비는 사전 승인된 종료 시각/상태의 유일한1건과 일치했고 eligibility 조건을 통과했다. digest만0600 비공개 파일 `/private/tmp/pms-preflight-20261001-acknowledged-result.json`에 보관했다. 실 식별자·메일 내용·credential은 Git/로그에 남기지 않았으며, 운영 값 설정/메일 상태 변경도 하지 않았다. 조사 소유 TLS 중계는 종료했다. 초기 조회 도구의 설치 client 차이와 backup CLI 인자 오류를 보정한 뒤 읽기만 수행했다.
- [배포 창·복구 계획](../docs/development/azure-maintenance-first-rollout.md#이번-구조-분리의-배포-창과-복구-계획)을 준비했다. 최종 실행 승인 후 가장 이른 T0/계획 예산60분이며 실제 예약·보장된 완료시간은 아니다. 쓰기 종료 후 첫 migration 전의 custom PITR 시각, DB 변경 전 구 revision 재개, 변경 요청 후 중단 유지·상태 확인·forward fix 또는 세 DB 동일시점 PITR, 유료 복구 자원/연결 변경의 실행 승인 경계를 기록했다. 사용자 직접 검수는 WAIVED이며 운영 사후 자동/업무 검증을 면제하지 않는다.
- 독립 검토: 신규 `review_historical_mail_exception`이 `f69b19d` 위9파일의 구현/전달 경계·실제 DB 시험·모의 시험 로그를 검토해 GO, P1/P2 없음으로 판정했다. 요청 모델 `gpt-6-astra/high`, 실제 모델 식별값 NOT_REPORTED다. 후속 계획 검토는 **RECOVERY-CHECKPOINT-P2**를 발견했다. 최초 runner에는 drain 뒤/첫 migration 전의 복구시각 기록·확인 checkpoint가 없으므로 이번 계획만으로 실행 준비 완료라고 할 수 없다. 문서에 현재 runner 실행 금지와 최종 실행 준비의 checkpoint 구현·실패 반례·독립 검토 및 실제 복구 범위 증거 확정을 선행조건으로 명시했다. 메일 예외 GO는 유지하며 이 운영 준비 finding은 미해소다. 새 exact head의 최종 required CI는 [PR159 Checks](https://github.com/emisubin/emi-qms/pull/159/checks)를 근거로 하며 기존 head의 PASS로 대체하지 않는다.
- 최종 로컬 이미지: 현재 제품 소스의 build/catalog exact/fresh/기존 DB 적용/reduced schema PASS 및 실행 소유 임시 자원 잔여0을 확인했다(`/private/tmp/business-schema-historical-mail-image-trace.log`). 첫 plain 실행은 무출력 exit1이며 원인 미확정이다. 상세 trace 재실행의 성공과 구분한다. actionlint·Bash syntax·ShellCheck·diff 검사는 PASS다. ShellCheck의 시험용 값 선언/할당 경고는 분리 구문으로 보정했고 최초 전환 모의 시험을 재실행했다.
- 마지막 모의 재검증59/59 PASS(`/private/tmp/business-schema-historical-mail-bootstrap-final.log`), Azure aggregate 정적 검사 PASS(`/private/tmp/business-schema-historical-mail-artifacts.log`, Bicep compile NOT_REQUESTED)를 확인했다. checkpoint finding에 의존하지 않는 메일 예외 변경만 기존 Draft PR에 게시하며 main 병합·운영 실행 NO-GO를 유지한다.
- 후속 reviewer는 두 문서의 실행 금지/선행조건 보정을 확인하고 메일 예외의 commit·동일 PR·새 CI는 GO를 유지했다. RECOVERY-CHECKPOINT-P2는 예외 수용하지 않았으며 운영 실행 차단 상태로 남는다. 새 P1/P2는 없다.

### 복구 기준 확인 단계 구현 — 2026-10-01

- 사용자 “시작해”는 직전 제안의 복구 checkpoint 구현·검증을 승인한다. 기존 PR159 수정·필수 CI 승인 및 사용자 직접 검수 생략은 유지한다. main 병합·운영 중단/DB 변경·백업/복원 생성·유료 자원·공개배포는 이번에 실행하지 않는다. 기준선 clean `aed9828`, 해당 [CI](https://github.com/emisubin/emi-qms/actions/runs/36805057230)는 Backend1038/Frontend586/mock71/full-stack64+2 및6 jobs PASS다.
- 목표는 RECOVERY-CHECKPOINT-P2 해소다. 최초 runner와 일반 release의 DB 변경 경로 모두 D/C/O drain 뒤 첫 DB 변경 요청 전에 같은 서버의 실제 완료된 Full/Automatic backup을 요구한다. Azure가 기록한 마지막 drain 종료시각보다 늦은 `completedTime` 자체를 복구 입력으로 고정한다. 임의 custom 시각, 단순5분 대기, earliest 시각만으로 최근 복구 가능성을 추정하지 않는다.
- 근거는 Microsoft [Full backup 복원](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/how-to-restore-full-backup)과 [백업 목록 API](https://learn.microsoft.com/en-us/rest/api/postgresql/backups-automatic-and-on-demand/list-by-server?view=rest-postgresql-2025-08-01)다. Burstable의 on-demand backup 미지원/자동 snapshot 일일 조건상 기존 자동 백업 주기에 맞춘 창이 필요하며60분 성공을 보장하지 않는다. 제한시간 내 미확보·응답/형식/대상 불일치·앱/Job 재기동·증거 저장 실패는 migration·권한/소속 변경 미실행과 기존 서비스 재개로 종료한다. 일반 release의 사전 공지/유지보수 상태 기록은 이 업무 구조 변경과 구분한다. 새 복원 서버에서 실제 복원이 성공했다는 증거와 구분한다.
- 구현 경계: 공통 read-only helper, 두 runner·관련 모의 시험, 실제 DB drain의 백업 서버 호스트 일치 검사, workflow 전달/검증·SOP/현재기록. 별도 reviewer `review_historical_mail_exception`이 기존 맥락에서 설계를 다시 읽고 위 근거/최소안을 제안했다. 요청 모델 gpt-6-astra/high, 실제 모델 NOT_REPORTED. 구현 후 고정diff/반례증거 독립검토를 다시 받는다.

- 구현: preflight → arm(운영 준비 Job 완료 뒤 실행 이력 고정) → 앱 비활성/replica0 → D/C/O drain의 Azure endTime → 이후 Full/Automatic 백업 선택 → D/C/O 최종 drain → 같은 백업/앱/Job 재검증·증거 저장 → DB 변경 순서다. 선택 백업의 원본 완료시각과 서버·release·SHA binding을 private evidence로 보존한다. 기본15분/최대30분 및 공지 종료시각 중 먼저 오는 한도에서 실패하면 변경 전 복구한다.
- 독립 검토 보정: (1) poll 사이 이미 끝난 새 Job도 baseline 밖이면 거부하고 runner 소유의 최초3+최종3 drain execution만 허용, (2) `Database:RecoveryPostgresHost`를 공통 연결 검사와 실제 migration/bootstrap/backfill에 결합했다. backfill은 실제 사용하는 Migration 연결3개를 첫 DB 접근 전에 검사한다. 실제 PostgreSQL fixture에서 bootstrap·migration·backfill 진입의 잘못된 host 거부 및 모든 purpose 검사를 포함한2 Facts 최종2/2 PASS(skip0), 소유 DB/Compose 정리 완료다. 앞선 두 P2의 코드 보정 검토는 GO다.
- 저장 경계 추가 관측: 저장소 `private=false`를 확인하여 평문 Actions artifact 계획을 철회했다. 일반 workflow는 보호 Environment 공개 인증서만 받아 OpenSSL3 CMS AES-256-GCM/RSA-OAEP-SHA256·key-id 형식으로 매 checkpoint를 암호화하고 `.p7m` 단일 파일만14일 보존한다. private key는 workflow에 보내지 않는다. 공개 인증서 누락/형식 오류·암호화·저장 실패는 변경을 막고 평문 fallback은 없다. 최초 로컬 runner는0700/0600 private JSON 보존을 유지한다. 실제 인증서/키 준비·복호화 확인 및 운영 변수 설정은 최종 실행 범위에 남기며 이번에 실키/운영 설정은 생성·변경하지 않았다.
- 검증 경계: 이 변경은 운영 read-only metadata(서버 Ready/14일 보관·Full/Automatic 필드·실제 Job endTime 모양) 확인과 합성/일회용 DB 시험만 수행한다. 실제 provider 발송·운영 앱 정지·backup/restore 생성0이다. `.github/workflows/ci.yml`의 배포 정책 검증 한도를20분, 일반 release 한도를45분으로 두어 추가 반례와 기본 백업 대기15분을 수용한다. first harness는 기존 CI에서1회, helper/일반 harness는 Azure aggregate에서1회 실행하도록 연결했다.

- 최종 로컬 근거: helper12/12 PASS(`/private/tmp/business-schema-recovery-unit.log`), 일반 runner133/133 PASS(`/private/tmp/business-schema-recovery-release-final.log`), 최초 runner74case PASS+새 terminal Job1case PASS 및 암호화 보정 후 정상/새 Job/증거 실패3case PASS. 최종 전체75case는 required CI가 실행한다. 실제 C# 진입점2/2(skip0), image build/catalog exact/fresh/existing/reduced schema PASS와 소유 임시 DB/container/image/network/volume 잔여0(`/private/tmp/business-schema-recovery-image.log`). scope·Bash syntax·ShellCheck·actionlint·diff PASS. Azure aggregate는 암호화 보정 전 PASS이며 최종 helper/일반133case 및 workflow syntax는 별도로 통과했고 새 head required CI에서 aggregate를 다시 실행한다.
- 최종 독립 판정: 기존 reviewer가 actual diff와 위 로그·SOP/CI를 읽고 요구 충족/품질 GO를 확인했다. 암호문 경계 GO 당시 일반 runner 완료 조건부였고 같은 코드133/133 PASS로 조건을 충족했다. helper hash `23f49731a586fe5291f26ceae59fb3191f0493ec15e6b5eec96ae8068b7742d6`, 일반 runner hash `49e217a39dd152c9e60b1fd84ed9dce3e9c82f7c60ed845bd3a031ad2c4f9add`. RECOVERY-CHECKPOINT-P2·새 terminal execution 누락·별도 Job 서버 binding·공개 평문 증거 문제는 예외 수용 없이 해소한다. 실제 모델 식별은 NOT_REPORTED다.
- Git/운영 상태: 이 완료 범위만 기존 branch/PR159에 commit·push하고 새 exact head의6개 required CI를 확인한다. 최신 원격 결과는 PR Checks/PR 본문이 소유하며 이전 head PASS를 새 head 결과로 사용하지 않는다. 사용자 검수 WAIVED, main 병합/공지·운영 중단/0131/이미지 게시·공개배포는 미실행이다. 남은 것은 최종 실행 창·실제 백업 후보 가용성·일반 workflow 암호화 키 준비 및 해당 운영 실행 범위다. 이번 수정으로 유료 자원이나 운영 값이 바뀌지 않았다.


### 배포 전 코드 품질 보정 — 2026-10-01

- 사용자 “보정해”는 직전 정밀 분석에서 확인한 3개 결함과 6개 정리 항목의 코드 보정을 승인한다. 기준선 `a5c9cb9010481d6a5caa2691390010771543eb6b`, 기존 PR159/같은 branch를 이어간다. 사용자는 공개배포를 퇴근 후로 미뤘다. 이번에는 코드·합성 검증·독립 검토·동일 PR 갱신만 수행하며 main 병합·운영 DB/앱 변경·실제 발송·유료 자원은 실행하지 않는다.
- 보정 범위: 느린 사업부 장애의 readiness 전파, 유지보수 중 인증 단계의 숨은 쓰기, QR/알림 외부 링크의 사업부 문맥, 미사용 UI/서버 함수, runtime 연결 수명, worker 중복 순회, 오산 감사 함수의 제거된 청주 표 규칙, 생성 응답 Location, 복구 checkpoint의 안전한 실패 진단.
- 불변조건: Directory/청주/오산 연결 및 서버 권한 고정, 기존 업무/UI·개인 알림 설정 보존, request 감사 주체의 혼입 금지, ReviewSafe 읽기 전용, 복구/배포 fail-closed, 공용 main migration 불변. 배포 전인 사업부0131만 범위 내 보정한다.
- 검증: 새 실패 반례를 실제 호출 경계에서 확인하고 관련 자동 검증 및 최종 head required CI를 수행한다. 이전 head CI36814790499의6 jobs PASS는 기준선 근거이며 이번 수정의 통과 증거로 재사용하지 않는다. 수동 사용자 검수 WAIVED는 유지한다.
- 좁은 병렬 구현은 현행 모델 정책에 따라 frontend 링크/미사용 UI와 health/worker/진단 정리를 `gpt-5.6-sol/high` 두 작업에 위임했다. parent는 인증/DB 연결·감사 함수·Location을 소유한다. 도구의 실제 모델 식별값은 NOT_REPORTED다. 최종 DB/공통 계약은 작성과 분리된 검토를 받는다.
- 구현: 세 DB readiness를 동시에 확인하고 전체4초 안에 결과를 반환한다. 정상 사업부는 다른 사업부의 지연 때문에 probe5초 제한을 넘기지 않는다. 인증은 기존 계정이 변하지 않았으면 Directory 쓰기를 생략하고, 동기화가 필요한 경우 Directory/해당 사업부 유지보수 공유 잠금을 commit까지 유지한다. 중단 중에는 기존 profile 조회만 허용하며 신규 등록·profile 생성은 재개 이후로 남긴다.
- 연결 수명: singleton provider가 고정 설정별 pool을 소유하고 각 store는 lease만 반환한다. 요청별 감사 주체/요청값이 startup 설정에 포함되면 기존 `Pooling=false`를 유지해 다른 요청에 섞이지 않는다. ReviewSafe 읽기 전용과 각 store의 DB identity/업무 경계는 유지한다. bootstrap·migration 등 일회성 관리 연결은 대상에서 제외했다.
- 정리: 사용되지 않는 UI2개·옛 오산 알림 분기와 서버 private helper를 제거하고, worker의 안쪽 중복 사업부 순회를 없앴다. 바깥 hosted service의 사업부별 실행·잠금·실패 격리는 유지한다. 오산0131의 감사 함수는 남는 표에 필요한 규칙만 남기고 기존 민감값 제외/감사 저장 규칙을 보존한다. 생성 응답의 상대 Location에도 원래 사업부 URL을 붙인다.
- 링크: 새 QR/메일/WebPush/Teams 주소와 Teams SDK 문맥에 원 사업부를 넣는다. 기존 청주 QR은 청주로 고정하고, 사업부 없는 옛 알림은 저장된 사업부를 임의 재사용하지 않고 서버가 허용한 사업부 선택을 받는다. 최초 독립 검토(`review_quality_corrections`, 요청 `gpt-6-astra/high`, 실제 NOT_REPORTED)의 P2-DEEPLINK-QUERY/SDK-RACE를 받아 notificationId 쿼리 우선순위와 지연 SDK의 화면 전환 순서를 보정했다. 해당 findings의 최종 해소 여부는 후속 검토에서 확인한다.
- 화면 확인: 합성 API만 연결한 별도 preview에서1440/390폭을 확인했다. 옛 notificationId 쿼리+저장된 오산 조건에서 선택 전 양쪽 알림 조회0, 청주 선택 뒤 청주 알림 조회만 발생했다. 기존 인증 공용 화면을 재사용했으며390폭2열 눌림을 알림 안내에만 적용되는 반응형1열 보정으로 해결했다. 선택/상세 screenshot을 직접 확인했고 가로 넘침이 없다. `/private/tmp/business-schema-quality-visual.log`와 `/private/tmp/pms-quality-visual/`은 합성 증거이며 Git에 넣지 않는다. 소유 preview/API/브라우저 탭은 종료했다.
- 중간 검증의 한계: 첫 backend 선별 실행은139 PASS/새 fixture1FAIL 뒤 장시간 응답이 없어 중단했다. HttpContextAccessor 시험 준비의 async 흐름을 보정했고, health 시험의 조기 취소 시 완료 신호가 빠질 수 있는 finally 범위도 고쳤다. 이 중단 실행을 전체 PASS로 기록하지 않는다. 현재 최종 소스 재빌드는 경고0/오류0이며 관련 반례를 제한시간·개별 결과 로그를 켜 다시 실행한다.
- 배포 모의 시험에서 반복된 실패는 실제 traceback으로 합성 checkpoint10초 예산 안의 Azure read timeout을 확인했다. 운영 기본900초/최대1800초는 유지하고 합성 정상 시나리오만60초, 의도 timeout 반례는1초로 유지한다. 고정 진단 코드 외 원문/credential을 출력하지 않는다. 최종 전체 재실행과 독립 검토는 진행 중이다. 첫 이미지 적용 시험은 실패하고 소유 자원 잔여0을 확인했으며 실패 원인을 추가 진단한다.
- 후속 독립 검토의 P2-TEAMS-STRICTMODE도 해소했다. SDK 진행 promise를 보존해 실제 개발 앱의 effect 재시작이 같은 결과를 구독하게 하며 화면 이탈·완료·오류 때 정리한다. 실제 StrictMode 지연 SDK/권한거부/빈문맥3/3, 타입/lint PASS이며 reviewer는 최종 App hash `e81a4ea78263bc1815d1d8ab3f8781a9a61123ca58f56a6ff890f4b18431f81c`를 읽고 구현 GO·미해소 P1/P2 없음으로 판정했다. 필수 검증/이미지 실행 조건과 운영 GO는 별개다.
- 프런트엔드 관련3파일 전체133/134 PASS의 유일한 기존 구매 편집10초 timeout은 해당 시험을 수정하지 않고 단독 재실행1/1 PASS로 확인했다(`/private/tmp/business-schema-quality-procurement-recheck.log`). 최종 head 전체 회귀는 required CI가 담당한다. Python helper14/14, 기존 실패 prepare-osan-failure 및 release case98 각각 PASS, Bash/ShellCheck/diff PASS다. 첫 이미지와 내부 로그를 보존한 재실행 모두 legacy130 fresh/existing/ledger 이후 invalid CLI 설정 거부 반례에서 종료했으며 구조 축소 적용 단계에는 도달하지 않았다. 두 실행 소유 자원 잔여0을 확인했고 해당 반례를 별도로 진단한다.
- 최종 backend 관련 회귀55/55 PASS(skip0,17.67분)를 확인했다(`/private/tmp/business-schema-quality-regressions-final.log`). 실제 세 DB에서 Active/Delayed/Failed 중 기존 인증 profile의 읽기만 허용·신규 등록/생성/수정0·Idle 재개·Directory/선택 DB 동기화와 drain 경쟁을 검증했다. 실제 backend PID 재사용/사업부별 pool 분리/두 감사 주체 불혼입/ReviewSafe UPDATE 거부/오산 감사 함수도 통과했다. readiness2개·Location4개·사업부별 알림 SQL/링크5개 및 Panel/QR 회귀를 포함한다. 전용 `qualityfix_20261001d` DB/컨테이너/네트워크 정리를 확인했다.
- 최종 고정 소스의 최초 전환75/75·일반 release133/133·helper14/14 PASS다(`/private/tmp/business-schema-quality-bootstrap-final.log`, `business-schema-quality-release-final.log`, `business-schema-quality-recovery-helper-final.log`). 실제 image의 원래 전체 순서/환경/네트워크에서 build/catalog exact/fresh/existing/reduced schemas PASS 및 임시 자원7종 잔여0을 확인했다(`/private/tmp/business-schema-quality-image-diagnostic-c.log`). 이전 두 실패의 정확한 원인은 미확정이며 성공 재실행을 원인 규명으로 확대하지 않는다.
- 재발 진단: image 시험 child는 잘못된 설정4종의 고정 이름·숫자 종료 코드만 내보내고, root는 두 고정 오류 문구/그4종/1~3자리 숫자에 정확히 맞는 최대1줄만 전달한다. 실제 root 실패 분기를 추출한 합성3case에서 정상 진단2종 전달·앞뒤 원문/비밀값 제거·알 수 없는 식별자 거부·exit1 유지를 확인했다. 두 script Bash/ShellCheck -x/diff PASS다. 마지막 변경은 실패 출력에만 한정되므로 같은 image 성공 경로를 다시 반복하지 않았다.
- 최종 독립 검토: `review_quality_corrections`가 실제 diff와 최종 로그를 읽어 local commit·동일 PR 갱신·새 head required CI 진행 GO, 잔여 P1/P2 없음으로 판정했다. root/child image harness hash는 각각 `48cd89f14eb8d77ebdca4b725cb7f5ca3729218d3e5001dbd236113eb27b4414` / `4594be77517e0d6f56f9df4c17e285cafdae12640c38d578c559867deff0fc3e`다. 요청 모델 `gpt-6-astra/high`, 실제 NOT_REPORTED. 기존 검수 WAIVED를 유지하며 이는 운영 승인이나 원격 CI 완료를 뜻하지 않는다.
- Git/공개 상태: 이 완료 보정만 `codex/business-schema-separation` 및 기존 Draft PR159에 반영하고 해당 최종 commit의 required CI를 확인한다. 최신 원격 결과는 PR Checks/PR 본문이 소유하며 이전 commit의 PASS로 대체하지 않는다. 사용자가 미룬 퇴근 후 공개배포, main 병합·운영 DB/앱 변경·실제 provider·유료 자원 생성은 실행하지 않았다.
- 원격 검사 보정: `d3d7f44`의 CI36831468545에서 새 StrictMode 시험의 `Promise<unknown>`이 mock 반환형과 맞지 않아 Frontend Typecheck가 실패했다. 앞의 로컬 `tsc --noEmit`은 project reference의 시험 파일까지 검사한 근거가 아니었다. CI와 같은 `tsc -b --noEmit`으로 동일 오류를 재현한 뒤 합성 Teams 응답 구조에서 promise 자료형을 추론하도록 보정했다. 제품 동작·assertion은 유지한다. 최종 `tsc -b --noEmit`·해당 파일 ESLint·Teams 문맥4/4 PASS(관련 없는89개는 필터 제외), diff PASS이며 작은 시험 자료형 수정은 직접 검증했다. 근거는 `/private/tmp/business-schema-quality-typecheck-red.log`, `business-schema-quality-typecheck-final.log`, `business-schema-quality-sdk-final.log`다. 새 head의 전체 required CI는 기존 PR에서 다시 확인한다.
- **READER-LIFETIME-P2 / 후속 보정:** `325da81`의 CI36832304777에서 frontend602/mock71/배포 절차는 PASS했지만 full-stack은44/64 PASS·20 FAIL이었다. 별도 검토자가 공유 pool과 기존 reader의 수동 종료+외부 `await using` 종료가 충돌하는 수명 문제를 확인했다. Npgsql10.0.3은 connector의 reader 객체를 재사용하므로, 먼저 닫은 reader가 다른 요청에서 재사용된 뒤 이전 요청의 마지막 Dispose가 새 요청을 닫을 수 있다. 7개 store의10곳을 좁은 `await using` 블록으로 바꾸어 한 번만 종료한다. 이 중 datasource 명령5곳이 직접적인 요청 간 경쟁 경로이며 explicit connection을 보유하는5곳도 같은 패턴을 정리했다. SQL·열·권한·반환·transaction/audit 순서는 유지하고 전체 backend source의 수동 reader Close/Dispose 잔여0을 확인했다.
- 재현 근거: 로컬 단독 모바일1/1 PASS 뒤 원격 선행 사례를 연속 실행하자 `WorkflowStore.GetMyWorkSummaryAsync`의 Dispose stack·500, 다른 `PendingTypeStore` reader의 IndexOutOfRange 및 worker의 NpgsqlOperationInProgress를 관측했다(`/private/tmp/business-schema-quality-ci-repro-d.log`). 3 PASS 뒤 내보내기 요청이 끝나지 않아 이번 소유 Playwright만 정상 중단했고, 이 실행은1중단/1미실행으로 보존한다. 추적·합성 화면은 `/private/tmp/business-schema-quality-reader-red-artifacts`에 보관했고16개 생성 screenshot의 tracked 원본을 복구했다. 앞선 로컬2회는 브라우저 녹화/실행 파일 준비 문제로 제품 시험 전 종료되어 재현 근거에서 제외한다. 각 소유 임시 DB/container/network를 정리했다.
- 새 회귀는 실제 HTTP API6개를24회 병렬 호출하고 해당 fixture에서만 pool2를 사용해 상세/요약과 인접 요청의 결과 격리를 검증한다. 제품 수정 전 바이너리 hash `ea0077f380f9c5555fb39213ad52ac3b2385260ee5380bf55eb8ce5986826359`를 유지한 test-only build에서2분 무응답으로 testhost가 중단됐다(`/private/tmp/business-schema-quality-reader-red.log`). 이를 완료된 assertion FAIL로 표현하지 않는다. 작성과 분리된 동일 reviewer가7개 store·새 시험/opt-in fixture를 검토해 코드 조건부 GO·새 P1/P2 없음으로 판정했으며, 수정 바이너리의 동일 회귀·E2E와 새 head required CI를 확인한 뒤 해소를 확정한다.
- 수정 후 동일 동시 조회 시험 **1/1 PASS(실제144 HTTP 조회,12초)** 및 같은 브라우저5개 **5/5 PASS(3.5분)**다(`/private/tmp/business-schema-quality-reader-green.log`, `business-schema-quality-reader-e2e-green.log`). 원 재현의 Unhandled API exception·NpgsqlOperationInProgress·IndexOutOfRange는 새 E2E 로그에서0이다. 각 임시 DB/container/network 정리, 합성 증거 보존 및28개 생성 screenshot 원본 복구를 확인했다. 전체 solution 빌드는 경고0/오류0이다(`business-schema-quality-reader-build-fixed-b.log`). 앞선 빌드는5분 뒤 비정상 종료했지만 진단0으로 원인이 미확정이며, 공유 build server를 사용하지 않는 독립 빌드가 통과한 사실과 구분한다. 전체 backend/frontend/E2E 결과는 이 보정의 새 exact head required CI에서 확정한다.
- 최종 독립 검토자는 위 빌드·동시 조회·동일 E2E의 실제 로그와 정리 결과를 읽어 코드 검토의 조건이 충족됐음을 확인했다. `READER-LIFETIME-P2`의 로컬 보정·검증은 해소됐으며 잔여 P1/P2 없음, local commit·동일 PR push·새 head CI 진행 GO다. 요청 모델 `gpt-6-astra/high`, 실제 모델 식별값 NOT_REPORTED이며 원격 CI 성공·main 병합·운영 실행과 구분한다.
- 후속 `58105c0`의 CI36837965374는 Frontend602/602·mock71/71·Workflow Validation PASS, full-stack63/64 PASS·구매 초기 잠금1FAIL이었다. 실패 화면은 ready 상태이며, 기존 시험이 첫 GET만 멈춰 StrictMode의 최신 GET을 통과시킬 수 있음을 실제 loader의 request ID 계약과 대조했다. 해당 fixture만 구매 탭 ready 확인→모든 edit GET 대기→실제 intercept 확인→기존4버튼 잠금 검사→대기 해제 순서로 보정했다. 최초 보정의 즉시 unroute는 실행 중 handler와 충돌해 로컬3/3 FAIL했으므로 `unrouteAll({ behavior: 'wait' })`로 종료를 기다리게 했다. 제품 구매 처리·기대값·재시도 설정은 변경하지 않았다.
- 최종 구매 fixture는 **3/3 PASS, retries0,44.6초**, ESLint·`tsc -b --noEmit`·diff PASS다(`/private/tmp/business-schema-quality-procurement-gate-final.log`). 잠금/준비 합성 화면을 직접 확인했고 시험 소유 DB/container/network 정리 완료를 확인했다. 앞선 CI 합성 증거와 cleanup 실패 로그는 `/private/tmp/business-schema-quality-reader-ci-artifacts`, `business-schema-quality-procurement-gate-green.log`, `business-schema-quality-procurement-gate-red-artifacts`에 보존했다. `fix_frontend_deeplinks`가 시험1파일을 보정했고 parent가 실제 diff·제품 loader·화면·재실행 결과를 검토했다. 같은 PR에서 마지막 커밋의 전체 required CI를 확인한다.

### 병합·공개배포 승인 및 즉시 복구 경로 보정 — 2026-10-01

- 사용자 “코드와 db 구조 병합하고 공개배포하자”로 PR159 main 병합, 검토된 C/O0131과 기존 단일 backend/frontend 교체를 승인했다. 직접 사용자 검수 WAIVED는 유지한다. 최종 제품 후보 `e92be46`의 [CI36841169873](https://github.com/emisubin/emi-qms/actions/runs/36841169873)는 required6 jobs 모두 성공, backend1048/1048(skip0), frontend602/602, mock UI71/71, full-stack64+사업부격리1+오산등록1 PASS다.
- 현장 기준선: 운영 backend/frontend는 `02028f2`, 0131 미적용이다. 공지·업데이트 팝업은 이미 운영 중이며 Sep25 최초 도입 예외는 소진됐다. 기존02028 CLI의 prepare를 한 execution에서 수행해 양쪽 공지 준비를 확인하고, 병합 후 새 image의 `verify-prepared`/사업부별 activate를 정상 runner로 연결한다. old CLI를 새 target loop에 반복 실행하거나 사전 공지 후 기본 workflow의 prepare를 재호출하지 않는다. 기존 Azure 계정의 local runner를 사용하고 미승인 GitHub identity 권한 추가는 하지 않는다. 독립 검토의 이 호환 경로 판정은 GO이며 실제 실행 성공과 구분한다.
- 자동 Full backup의 최근5개 완료는 한국시간 오전8:39–8:42이고, 서버는 Burstable PG16/보존14일이다. 기존 “drain 뒤 새 Full/Automatic 완료” 검사로는 지금 제한시간 내 배포할 수 없다. 시간 선택 질문에 사용자가 **“지금”**으로 확정했다. 자동 백업 시간 조건을 무시하는 대신 즉시 확보·검증 가능한 논리 복구 증거 방식을 추가한다. Azure full 방식은 기본값으로 보존한다. 근거: [Azure 백업·복원](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore), [PostgreSQL pg_dump](https://www.postgresql.org/docs/16/app-pgdump.html).
- 새 경로: 공지/팝업 → 서버 저장 제한 → 두 serving 앱 중단/replica0 → D/C/O drain → 동일 PG16 도구로 globals(비밀번호 제외)와3DB 전체 dump → 개인 폴더의 CMS AES256GCM/RSA-OAEP 암호화 사본 → 그 암호문을 복호화하여 network-none/tmpfs의 단일 일회용 PG16 cluster에 실제 복구 → schema/owner/ACL·ledger/identity·표 데이터/sequence/large object·역할별 DB 접근 비교 → 연결 종료/최종3DB drain·동일 암호문 재검증 → 첫 DB 변경이다. 인증정보는 기존 Key Vault와 연결해 보존하며 비밀번호 없는 globals만으로 인증 복구까지 증명했다고 하지 않는다. 기존 외부 첨부 저장소는 이번 DB 구조 변경에서 수정하지 않는다.
- 운영 연결 실증: 기존 ClamAV 앱은 같은 내부망에 있고 serving 앱 중단 대상과 별개다. 기존 단일 Azure exec/SSH relay를 이 앱에 한 번 열어 PostgreSQL TLS1.3 인증서를 검증하고 정상 종료했다. DB credential을 ClamAV에 배치하거나 새 자원·identity·네트워크 공개 설정을 만들지 않았다. 로컬 PG16 Docker 도구에서 Mac loopback 중계로 연결하는 합성 probe도 PASS다. Git 밖 개인 디렉터리에 전용 복구 키를 생성했고 합성내용 암호화/복호화 PASS, 비밀키 외부전송0이다.
- 구현 경계: parent는 기존 recovery helper의 explicit evidence mode, config/source/release/server/drain 결합·지속 quiet 검사·최종 검증과 연결 시험/분류/문서를 소유한다. `fix_backend_cleanup`는 고정 backup/restore module과 집중 시험만 소유한다. `review_quality_corrections`는 작성과 분리된 운영 호환성·복구 설계/최종 diff를 검토한다. 기존 context 재사용이며 실제 모델 식별값 NOT_REPORTED다. 일반 배포 로그에는 고정 상태만 출력하며 원문·사본·키는 Git/PR/CI artifact에 넣지 않는다.
- 현재 상태: 추가 복구 경로의 로컬 구현·합성 전체 복구 및 실제 Azure schema-only 호환 검증 완료. 공지 게시, main 병합, 운영 서비스 중단, 운영 DB 변경 및 새 image 배포는 아직 미실행이다. 해당 원격 required CI와 실제 중단 이후 복구 사본 확보가 남아 있다.
- 독립 검토에서 실제 helper→driver UTC 형식 불일치와 timeout 뒤 client/restore container 정리 누락을 발견하여 보정했다. helper가 만드는 config를 real parser에 전달하는 회귀는 실패 재현 뒤21/21 PASS이며 최종 verify에도120초/공지 종료 중 작은 deadline을 전달한다. driver는 실행별 client/restore 소유 확인·독립 정리 예산·전체 작업 deadline을 사용한다. 실제 Docker CLI timeout 반례와 TLS·3DB 복구16/16 PASS를 확보한 뒤 아래 실제 Azure 호환성 보정을 추가했다. 이전 성공을 새 보정 최종본의 증거로 대신하지 않는다.
- 실제 운영의 행 데이터를 내려받지 않는 사전 검사에서 3DB admin의 `pg_largeobject`/metadata SELECT 권한을 확인했다. 실제 엔진은16.15, 고정 로컬 도구는16.14이며 globals와3DB schema 읽기는 성공했다. 첫 로컬 globals 적용은 bootstrap grantor OID10 불일치로 실패했고, 원본 OID10 역할 보존·관측 temp tablespace tmpfs·정확한 Azure 관리 GRANT 한 문장의 명시적 호환 처리 뒤 globals와3DB schema 적용을 모두 통과했다. 원본 globals는 보존하고 그 플랫폼 권한의 다른 membership/schema 의존성이 있으면 실패한다. 이어 C의9개 CHECK가 같은 AND 조건을 다른 괄호로 출력하여 strict hash 비교에서 실패했다. 이를 원본 SQL의 PostgreSQL canonical roundtrip과 실제 full restore 비교로 보정했다. 최종 실제 Azure 구조 SQL과 별도 custom schema archive를 독립적으로 복구한 D/C/O 구조·owner·ACL 비교가 모두 PASS이며 해당 소유 cluster를 정리했다. 어떠한 단계도 운영 권한·구조·데이터를 변경하지 않았다.
- private 증거: `/private/tmp/business-schema-logical-boundary-red.log`, `business-schema-logical-boundary-final.log`, `business-schema-logical-recovery-tests-red.log`, `business-schema-logical-recovery-tests-green.log`, `/private/tmp/pms-business-schema-20261001-operating/schema-preflight.log`, `schema-preflight-green.log`. 암호화 키·credential·원본 globals/schema는 비공개 작업 폴더에만 두며 Git/CI에는 포함하지 않는다. 일반 Azure 계약 검사와 최초 도입 회귀75case는 PASS이고, 실제 최초 도입 runner를 운영에서 재사용한 것은 아니다.
- 최종 보정 검증: helper21/21 PASS(실제 driver parser, offset drain→UTC Z, 최종 verify deadline 포함), 고정 PG16 TLS source·암호화·독립 canonical 기대값·전체 복구·timeout 소유 정리16/16 PASS(24.260초). 실제 schema-only 결과는 `/private/tmp/pms-business-schema-20261001-operating/canonical-schema-preflight.log`다. 합성 full-data restore와 실제 운영 schema-only 결과를 구분하며, 운영 full-data backup/restore는 저장 제한·앱 중단·최초 drain 후 실행할 최종 checkpoint다.
- `review_quality_corrections`의 최종 통합 판정은 GO, 잔여 P1/P2 없음이다. 검토한 driver/test hash는 `989c6973`/`6b0728bf`, helper/test는 `1cad991a`/`e8cc54fa`다(요청 모델 `gpt-6-astra/high`, 실제 식별 NOT_REPORTED). 범위 내 local commit과 후속 PR 진행을 승인 경계에 맞는 것으로 확인했다. 이미 exact-head 전체 CI를 통과한 제품 PR159를 공지 성공 후 먼저 병합하고, 이9개 운영 복구 파일은 같은 Change031의 후속 PR에서 최신 required CI를 통과한 뒤 병합한다. 신규 driver 두 경로만 기존 Azure 검증 범주에 등록했으며 제품 경로의 검사 조건을 줄이지 않는다. 최종 main SHA의 OCI와 실제 image 검증은 그대로 수행한다.
- 20:40 KST 청주·오산 공지와 별도 팝업 게시를 읽기 전용으로 확인했다(Announced/version1/popup1, 정확한 본문·일정·notice 연결, 일반 공지 popup=false). 예정 창은21:09–22:09 KST이며 아직 서버 저장 제한/중단은 하지 않았다. PR159는20:42 KST `5e3ebce`로 병합됐다. 후속 PR160의 첫 CI36857135799는 변경 분류 assertion을 통과한 뒤 합성 Git 저장소 cleanup에서 `.git/objects: Directory not empty`로 실패하여 Azure 검증에 도달하지 못했다. 해당 일회용 저장소의 자동 maintenance/gc만 끄고 기존 assertion과 cleanup 실패 처리는 유지한다. 사용자 저장소·전역 Git 설정은 변경하지 않는다. 수정 후 같은 PR의 최신 required CI를 다시 확인한다.
- PR160 최신 CI36857533809와 병합 main9170950의 CI36858304579는 PASS다. PR160은20:54 KST 병합됐다. 운영 전환 전 main9170950으로 백엔드·프론트엔드 OCI를 생성·게시했고, 백엔드 실제 게시 원본 이미지의 fresh/existing migration·정확한 catalog·reduced schema 시험과 소유 임시 자원 잔여0을 확인했다. 제품 backend/frontend/database는 전체 CI 성공 후보e92be46과 diff0이다. PR159 병합 main의 별도 전체 재실행은 진행 중이며 이전 성공과 구분한다.
- 로컬 Docker29.6.2의 containerd image ID가 config digest 대신 manifest digest여서 기존 OCI verifier가 최초 binding을 거부했다. 원본 OCI를 재빌드하거나 검사를 완화하지 않고, 별도 private adapter로 immutable raw ID의 Docker save root/blob/config/순서별 layer hash를 검증한 뒤 config digest를 담은 별도 inspection에 기존 verifier를 적용했다. 실제 시험은 raw ID로 실행하고 시험 후 재export/reinspect/binding을 통과했으며 원본 OCI를 `--all --preserve-digests`로 게시해 Registry digest 일치를 확인했다. root/config/layer 누락/순서 변조4반례 거부 PASS, 독립 reviewer GO다. 이9170950 전용 로컬 adapter는 공용 verifier 변경이 아니며 후속 실행은 source를 다시 고정한다.
- **AZURE-EMPTY-SECRET-VALUE / 배포 전 발견:**21:09 KST 정상 release 첫 실행은 서비스 중단 이전 `BACKEND_SERVING_CONFIGURATION_INVALID`로 exit68이었다. 실제 Azure 응답의15개 비밀값 참조가 `secretRef`와 `value:""`를 함께 포함하는데, 검사기가 두 속성의 존재만으로 중복 설정을 판정했다. 기존 앱 Running/image 불변, activate/stop/migration 요청 전 종료를 확인했다. 비밀값을 조회하거나 운영 설정을 바꾸지 않았다. 정상 참조는 비어 있지 않은 이름과 value의 null/누락/빈 문자열만 허용하고, 실제 값과 참조가 함께 있거나 이름/설정이 누락되면 계속 거부하도록 보정한다.
- 새 실제 응답 형태를 합성 fixture 기본값으로 바꾸어 수정 전24번째 사례에서 expected1 대신 사전 검사68을 재현했다(`/private/tmp/business-schema-azure-empty-value-red.log`). 신규4반례는 실제 값+참조, 설정 누락, 빈 참조만 존재, 잘못된 참조 이름이다. 수정된 검사로 비공개 실제 Azure snapshot 전체 PASS, null/누락/빈 literal ref 호환3형태 PASS, Bash/ShellCheck/diff PASS다. 전체 배포 계약 결과와 독립 검토·새 PR required CI를 확인한 뒤 동일 공지를 사용해 배포 전 준비부터 재개하며, 중단·DB변경 이후 실행을 통째 재시도하는 절차로 확대하지 않는다.
- 보정 후 전체 정상 배포 runner 회귀는 exit0 PASS(`/private/tmp/business-schema-azure-empty-value-green.log`)이며 독립 reviewer가 두 script의 실제 diff와 반례·Task 기록을 검토해 새 P1/P2 없음, 코드 GO를 확인했다. 공용 프로그램/DB/migration은 변경하지 않았다. 실제 배포 재개는 이 보정의 최신 원격 CI/병합 뒤 최종 source의 image와 checkpoint를 사용한다.

### 실제 중단 검사 보정과 재개 준비 — 2026-10-01

- PR161은 main `4affefc851d507a537c99bb20ece327090ec6ea1`로 병합됐고 PR CI36860645410/main CI36861474488은 PASS다. PR159 병합 main의 전체 CI36857030390도 모든6개 job 성공을 확인했다. 4affefc의 실제 backend OCI는 fresh/existing/reduced schema 및 catalog·전후 이미지 결합 검사를 통과했다. backend `sha256:a74a6d9b7c42244e36c6f40bffd959a633d361fb5d93035294d95357798a51df`, frontend `sha256:ddc5f58718b632b6bbd8911383b5c939b8641834c055842fde0bfeadcf21687b`를 원본 digest 그대로 게시했다.
- 운영 공지의 오산 본문에 작성자가 추가한 `DB` 표현을 관측했다. 기존 양쪽 maintenance 본문을 정확한 이전값 조건과 잠금으로 일치시키고 청주 공지 수정 이력도 보존했다. 오산 게시글 자체는 재수정하지 않았다. 양쪽 Announced/version2/popup2 및 본문·회차 일치를 읽기 전용 검증한 뒤 실행했다.
- **AZURE-INACTIVE-REVISION / 해소:** 두 번째 배포는 activate, 기존 앱 중단, 최초 D/C/O drain 성공 뒤 checkpoint wait에서 `REVISION_LIST_INVALID`로 중단됐다. `az containerapp revision list`는 기본적으로 active만 반환하여 중단 후 빈 목록을 반환했다. `quiet`와 `stop_app`에 `--all`을 명시하고 빈 목록 거부는 유지한다. 실제 Azure128개 비활성 revision replica 조회는 최대8병렬로40.037초였으며 이는 조회 시간 측정이고 quiet 성공 증거는 아니다.
- helper는 모든 revision의 inactive 상태와 replica0을 검증한다. 조회를 최대8개 병렬로 하되 모든 future를 회수하고 한 건이라도 오류가 있으면 실패한다. 실제 CLI의 active-only 기본값을 mock에 반영한 수정 전21unit 실행에서20개 실패/오류를 재현했다. 보정 후 unit23/23, release141/141, bootstrap75/75 PASS다. 공지 종료시각을 상한으로 유지하며 최종 검증의 실행 예산만120→300초로 조정했다. 두 번의 실제 replica 조회만 약80초인 관측에 근거하며 검증 조건을 줄이지 않는다. 해당 deadline을 실제 driver로 전달하는 unit23/23 재검증 PASS다. 백업 감시 간격은 대기와 Azure 조회 시간의 합이며30초마다 완료된다고 표현하지 않는다.
- 실패 후 기존 frontend/backend02028 이미지와 active revision 각1개를 복구했다. 읽기 전용으로 정확한 기존 원장130/130/4, 표209/209/8 및 양쪽 Failed 회차·본문 일치를 확인했다. 이는 해당 구조·상태의 일치이며 전체 업무 데이터 불변 증명이 아니다. 운영0131·DDL·실제 full backup은 미실행이다. 기존 이미지의 complete Job은 실패하여 원인을 확인 중이며 저장 제한 해제를 완료했다고 기록하지 않는다.
- 작성과 분리된 기존 `review_quality_corrections`가 누적5파일 diff와23/141/75 증거를 확인해 GO, 새 P1/P2 없음으로 판정했다. final budget 보정과 재개 wrapper는 별도 영향 검토한다. source/checkpoint 실행 기록과 실제 운영 결과를 확인하기 전 배포 완료로 표현하지 않는다.
- 다음 실행은 제품과 운영 절차 source를 각각 고정한다. 이미지 source는 검증된4affefc를 유지하고 새 ops source는 이번 보정의 병합 main으로 고정한다. 두 Dockerfile의 모든 COPY 입력(backend source·3 migration tree, frontend 전체·root package/lock/workspace·nginx template)과 Dockerfile·관련 dockerignore·기타 build 설정을 비교해 동일할 때만 재사용한다. base image와 frontend build args는 기존 OCI에 고정돼 있다. `SOURCE_SHA`와 checkpoint binding은 image source이며 별도 ops SHA·script hash를 기록한다. 이전의 “매 ops 보정마다 새 main image” 실행 방법을 이 동등성 검사로 대체하며 제품 검증을 생략하지 않는다.
- 기존 공지는 보존하고, Failed 해제 후 새 회차의 popup-only 준비와 실제 소요시간을 반영한 안내 창을 사용한다. 원래21:09–22:09 창으로 장시간 후속 실행을 진행하지 않는다. 새 회차 역시 준비 확인→저장 제한→중단→drain→전체 암호화 backup/restore→최종 확인→migration→새 앱→정상 확인→complete를 지킨다.
- **22:09 KST 인계 상태:** PR162 head `2d5f23e8070a8c0643a9840a6b23920eaed728f5`의 CI36865445832는 분류·workflow·gate3개 성공이며 제품3개 job은 영향 범위 밖으로 skip이다. 최종6파일 독립 검토 GO다. 자동 승인 검토가 “이번 특정 PR의 별도 main 병합 승인·검수 확인 불가”를 이유로 merge 명령을 실행 전에 거부했다. 기존 배포 지시·검수 생략과 구분해 PR162 병합 승인을 사용자에게 요청했고, 거부를 우회하지 않았다. PR162는 OPEN/CLEAN이며 main은4affefc다.
- 구 이미지 완료의 최초 실행은 Azure terminal Failed였고 양쪽 Failed/기존 구조를 재확인했다. 진행 중 Job0 확인 후 별도 attempt의 진단1회 실행은 성공했다. 직접 로그 수집은 시작 직후 container 미준비404였지만 Azure execution 성공·container exit0과 별도 SQL의 양쪽 Completed, 정확한130/130/4 및209/209/8 일치를 확인했다. 첫 실패 원인은 확보된 로그만으로 확정하지 않는다. 저장 제한은 해제됐고 기존 공개 화면 재로딩도 정상이다. 실제 업무 저장 시험을 했다는 뜻은 아니다.
- 다음 회차 metadata와 popup-only 검증기·postdeploy 검증기를 private 폴더에 준비했으며 게시·activate는 하지 않았다. 새 wrapper는 image/ops source 분리뿐 아니라 실제 runner/helper/driver 바이트를 ops commit에 결속하고, prepare 전에 새 회차 UUID 및 이전 Completed 증거를 확인한다. 독립 검토 GO(hash `e786c20e4acc728550f99ceeb898808c418a96cd284e443e5e91b9e209d5ce0c`). 새 회차 시각은 아직 미정이며 승인 후 실제 준비시각에 맞춰 생성한다. DB migration·full backup·새 앱 배포는 여전히 미실행이다.
- **PR162 명시 승인·병합 및 새 회차 준비:** 사용자가 병합 승인 질문에 `승인`으로 답했다. 이에 PR162를22:12 KST main `312e84f3515dfa3332c40c892d1b52c330ceb1ef`로 병합했으며 main CI36867062274도 성공했다. 검토 head와 ops script diff0, image source4affefc와 전체 build 입력 diff0 및 실제 archive runner/helper/driver의 commit 바이트 일치를 확인했다. 원본 checkout WIP는 건드리지 않았다.
- 새 popup-only 회차는 기존02028 CLI 한 번으로 준비됐고 양쪽 Announced/version1/popup1, 정확한 회차·본문·22:28:38~23:43:38 KST, notice_id NULL을 독립 SQL로 확인했다. 영구 공지는 중복 게시하지 않았다. 이 준비 성공은 실제 사전 팝업을 눈으로 봤다는 의미가 아니다. 현행02028 및 후보4affefc `MaintenanceAnnouncement`는 `writeBlocked`일 때만 팝업/배너를 표시하므로 Announced 상태의 실제 홈에는 나타나지 않았다. 기존 공지는 홈에 보이는 것을 확인했고 실제 팝업은 activate 뒤 확인한다. 이번 DB 배포에 별도 UI 변경을 추가하지 않는다.
- **새 회차(v3) 중단·복구:** activate 뒤 공개 홈의 실제 업데이트 팝업과 저장 제한 안내를 확인했다. 두 앱의 전체 revision/replica 중단과 최초3DB drain은 성공했다. Directory/청주/오산 전체 dump 파일은 각각 약0.09/44.45/3411.54MiB까지 생성됐으나 논리 복구 driver가 canonical schema 생성 관측 이전에 실패했다. helper는 `LOGICAL_BACKUP_FAILED`만 남겼고 원인 코드는 보존하지 않았다. 검증된 암호화 backup/manifest 게시0, 운영0131·DDL·새 앱 적용0이며 평문 임시 파일과 실행 소유 Docker 자원은 정리됐다.
- 기존02028 앱을 복구하고 정확한 기존 원장130/130/4·표209/209/8·해당 Failed 회차를 확인한 뒤 구 이미지의 complete를1회 실행했다. 실행 성공 후 별도 SQL로3DB의 동일 구조와 청주/오산의 정확한 회차·본문·Completed를 확인하여 저장 제한 해제 완료로 판정했다. 이 검사는 구조/maintenance 상태이며 업무 데이터 전체 불변 증명이나 실제 저장 시험은 아니다.
- 읽기 전용 후속 진단에서 오산 전체 inventory27,180,400bytes, 정렬, bootstrap role, identity,9개 CONNECT 검사는 모두 PASS다. 단순 메모리 부족/전체 deadline 소진을 확정 원인으로 삼지 않는다. 전체 backup 재실행 없이 canonical 복구 준비를 좁혀 조사한다. 내부 원인 소실은 별도 결함이므로 고정 driver `RecoveryError` 코드만 형식 검증 후 private0600·암호화 checkpoint에 보존하고 일반 출력은 기존 generic 실패를 유지하도록 보정한다. 원문 예외·stderr·credential·경로는 보존하지 않는다. helper24/24 PASS(OpenSSL3); 미확정 원인 해소 및 실제 backup 성공과 구분한다.
- 진단 보정의 독립 검토는 GO, 새P1/P2 없음이다(기존 작성과 분리된 reviewer 맥락 재사용, 실제 모델 NOT_REPORTED). helper/unit hash는85a67785/e316d41e이며24/24 로그를 직접 확인했다. 이 GO는 실제 backup 재실행 준비 완료가 아니다. 압축 dump 크기만으로6GiB tmpfs/8GiB Docker 메모리 충분성을 판단할 수 없고, 최종300초의 해시·복호화·추출 소요시간도 별도 실측이 남아 있다.
- **CMS-LARGE-PAYLOAD / 원인 재현:** 후속 Docker API 이력에서 canonical3DB 생성·dump·drop·소유 확인이13:50:00.733Z에 끝나고 첫 full pg_restore 이전13:50:09.558Z에 정리된 것을 확인했다. 앞선 “canonical 생성 이전”은 파일을 관측하지 못한 추정이었으며 실제 실패 범위는 그 이후다. 실제 source DDL/local canonical/custom schema-only의3DB owner/ACL 비교도 재검증 PASS다. 읽기 전용 DB 크기는D8,829,975/C72,227,863/O3,534,699,543bytes, 합계약3.37GiB다.
- 실제 오산 dump와 같은3,577,258,967bytes 합성파일·동일 인증서/키를 이용한 로컬 시험에서 현행 CMS 암호화가5.3초에 `EVIDENCE_ENCRYPTION_FAILED`로 재현됐다. `-stream` 암호화는5.402초에 성공했지만 기존/stream 복호화 모두 실패해 단일 옵션 추가로 해결되지 않았다. [OpenSSL CMS 공식 문서](https://docs.openssl.org/3.3/man1/openssl-cms/)의 기본 전체 메모리 처리와 streaming BER 설명을 확인했다. 운영 데이터를 재복사하거나 전체 배포를 반복하지 않았다.
- `fix_backend_cleanup`는 driver와 전용 시험 두파일만 소유하여 큰 payload를 제한된 크기의 AES256GCM/RSA-OAEP CMS 조각으로 처리하도록 보정한다. 조각 순서·누락·중복·변조·다른 사본의 조각 혼합을 거부하고, 단일 게시 파일의 전체hash·암호화 manifest·실제 복호화 후 복구 검증을 유지한다. parent는 helper/문서, 작성과 분리된 reviewer는 최종 diff/증거를 검토한다. 실제 크기의 합성 roundtrip과 기존 합성 PG 전체 복구가 완료되기 전에는 재배포하지 않는다.
- 대용량 보정은64MiB 조각과 조각 내부에서 인증되는 사본 ID·순서/개수·전체/조각 길이를 사용한다. 기존 목적지 파일의 exclusive 생성 실패를 cleanup이 지우지 않는 반례도 추가했다. 최종 고정 로그에서 실제 OpenSSL3.6.3의3,577,258,967bytes 합성파일 암호화·복호화29.762초, 전체hash 비교 포함1test34.074초 PASS이며 단일 암호화 입력 최대67,109,378bytes다. 기본 suite는20개 중18 PASS/2 skip(대용량·Docker), TLS Docker 전체 suite는20개 중19 PASS/1 skip(대용량),20.761초다. 실제 대용량은 별도1test로 실행했다. 로그는 private `/private/tmp/business-schema-logical-recovery-{unit,large-cms,e2e}.log`다. 합성 TLS PostgreSQL3DB의 dump·암호화·격리 cluster 전체 복구·검증도 PASS다. 이는 운영 full-data 복구 성공과 다르며 실제 운영 사본으로6GiB tmpfs 복구와 마지막300초 검증을 완료해야 한다.
- 통합 독립 검토는 코드·SOP·후속v4 wrapper GO, 새P1/P2 없음이다. driver/test는ed485b15/dc6c6d6a, v4/fresh previous-Completed verifier는0753125e/acf278b5다. v4는 아직 게시/activate하지 않았고 새 회차 준비 직전 이전 Completed를 다시 확인한다. 이미지4affefc build입력 동일성·최신 ops main·실행3파일 바이트 일치·회차별attempt를 유지한다. 현재 공개 화면은 기존 업무 홈이며 점검/저장 제한 안내가 해제된 것을 확인했다. 이번 작업의 ClamAV 임시 relay도 정상 종료했다.
- **PR163 병합 승인 대기:** 최신 main312e84f 기준의 별도 branch `codex/business-schema-large-recovery`에 보정2개 commit(2fa6104/d4387f7)을 보존했다. PR163 head `d4387f73190281f58d33cafc0ad779f8684f987d`, required CI36875934831의 분류·workflow·gate3개 PASS(제품3개는 변경 영향 없음으로 skip), 독립 통합 검토 GO다. 자동 승인 검토가 해당 PR의 별도 main 병합 승인이 없다는 이유로 merge를 실행 전에 거부했다. 거부를 우회하지 않았으며 사용자에게 PR163 병합 및 기존 공개배포 재개 승인을 요청했다. main/운영 DB는 그대로이고 v4 준비·activate·backup·DDL·새 이미지 배포 모두 미실행이다.
- **2026-10-02 재개 승인:** 사용자가 PR163 병합과 최대한 빠른 공개배포를 `전체 승인`했다. PR163은06:56 KST main `360a0fef5096c8841385b2eeccbcc0dc7fc4c51c`로 병합됐다. main CI36931955798은 분류·Gate 성공, 나머지4 job skip이며 PR의 실제 workflow 검증 성공과 구분한다. 검토 head 대비실행 scripts diff0, image4affefc 전체build입력 diff0, ops archive3파일 commit 바이트 일치를 확인했다. 기존 WIP는 Task 기록만이며 원본 checkout은 건드리지 않았다.
- 사용자 ASAP 요청에 맞춰 v4 안내를 준비시각부터75분으로 조정했다(기존 준비+15분부터75분). 이2줄만 변경됐음을 이전hash와 대조한 독립 검토 GO이며 새wrapper hash는178651c3이다. 새ClamAV 단일relay TLS1.3이 정상 연결됐고, prepare 직전3DB 기존130/130/4·209/209/8과 이전 회차의 Completed를 새로 확인했다. v4 prepare1회는성공했으며 안내창은06:58:35~08:13:35 KST다. 실제 DB변경/backup/새앱배포 성공과 구분한다.
- **v4 실행 중(재실행 금지):** 양쪽 새Announced 회차·본문·시간·notice NULL을 SQL로 확인한 뒤 release를1회 시작했다. 공개 홈의 실제 팝업·저장 제한 안내도 확인했다. checkpoint `/var/folders/xs/syr004417s917jpzcv741wnc0000gn/T/pms-recovery-checkpoint.vJkDMA`는07:04:35 KST armed이며, 앱 전체 실행본 중단과 최초3DB drain이07:09:25까지 모두 성공했다.07:10부터 실제 backup을 진행 중이다. private 실행 기록은 `v4-release-attempt.json`/`v4-release-runner.log`이고 wrapper session23355, 소유relay49496, 로컬파일 읽기전용 observer56813을 이어서 확인한다. 현재 원격구조/새이미지 적용 완료로 기록하지 않는다.

- **v4 07:24 KST 중단:** 실제3DB dump·canonical schema·대용량 암호화/복호화·원본 byte hash 대조까지 통과하고 실제 전체 복원에 진입했다. checkpoint 감시의 Azure 읽기 요청 실패(`wait/AZURE_READ_FAILED`)로 후보/verified 전에 종료했다(exit79). worker 결과가 감시 예외에 가려졌으므로 전체 복원 성공/실패 원인은 별도 조사 중이다. 운영0131/새 image는 미실행이며 checkpoint는armed, 새 암호화 archive 미게시, 작업용 평문 정리됨. runner가 기존backend/frontend revision을복원했으며07:27~29 KST 읽기전용SQL로 C209/130, O209/130, D8/4 및 정확한v4 Failed 상태를 확인했다. 이번회차 Failed 해제 wrapper는 v4 config/notice/checkpoint UUID를 동일성 대조하고 적용직전 동일SQL·기존image/health를 다시 검사하도록 준비했다.
- **v4 기존 서비스 정상화:** exact v4 config/notice/checkpoint 회차 결속 누락을 독립 검토에서 찾아 수정한 wrapper511dcd74로 Failed→Completed를 적용했다(exit0). 적용직전 세DB 원장·표 수·회차Failed/본문을 다시확인했고, 적용뒤 양쪽Completed와 기존원장/표 수를확인했다. 공개health200, 인증전root/api401, 로그인후 실제업무홈 및 점검안내해제를 확인했다. 실제 schema/image 공개배포 완료와 구분한다.
- **확정된 v4 실패 원인과 보정:** 전체3DB pg_restore는 끝났고 Directory 복원 inventory 조회가 `public`을 포함하지 않는 bootstrap 계정의 search_path에서 한정하지 않은 `qms_database_identity`를 찾지 못했다. 3DB schema+Directory 자료만 사용하는 읽기전용/소유network-none 재현으로 실제 표8개·identity 존재·원본schema일치와 `RESTORED_INVENTORY_FAILED`를 확인했다. 운영 데이터 누락이 아니며 검증 SQL3곳의 schema 명시로 보정한다. Azure 실패 당시 CLI로그는 남아있지 않아429/500 등 원인은 미확정이다.
- helper는 식별된 일시 Azure 조회 오류만 제한 재시도하고 정형 진단을 private 증거에 보존하며 동시 감시/worker 실패도 함께 기록하도록 보정했다. 독립 검토가 timeout 직후 성공한 future를 실패 처리하는 경쟁을 찾아 결과 회수로 보정했고 해당 반례·main 진단 보존 회귀를 추가했다. 단위29PASS, 모의 release141PASS. 첫 단위 실행의 systemOpenSSL 환경 및 시험 mock의 None 처리 실패는 brewOpenSSL·시험 준비 보정 후 통과와 구분한다. driver 수정·소규모 실제 재현·합성 전체복구·최종 독립 검토·보정 원격CI/병합 및 새v5 실제 배포는 아직 진행 중이다.
- driver의3개 application relation은 public으로 한정했고 bootstrap search_path가 public을 제외하는 합성 조건에서 실제3DB backup→offline restore→inventory/proof→verify가 통과했다. unit20개 중18PASS/2SKIP, Docker/TLS20개 중19PASS/대용량명시시험1SKIP이며 소유임시자원0개다. driver/test hash는0f0e05df/0968282e이고 helper/test는154ea977/0ecd817a다. 보정후 Directory 실자료 소규모재현은 자동승인검토가 운영dump/globals의 구체적 로컬 목적지 승인 부족을 사유로 거부하여 미실행이다. 앞선 보정전 원인재현 성공과 구분하며 다른 명령·경로로 우회하지 않는다. 실제 새 자료 반출에 의존하지 않는 review/CI/원격반영 준비는 계속한다.
- 최종6파일 독립 검토 GO(새P1/P2없음)이며 위 hash가 유지된다. v5 준비는이전v4 Completed를fresh확인한뒤1회 성공했고 팝업 안내창은07:41:12~08:56:12 KST다. 같은helper로 실행한 실제 기존revision replica조회130개는모두성공했으며 앞선읽기오류의원인이확정됐다는뜻은아니다. 저장차단·서비스중단·DB변경은v5에서아직시작하지않았다.
- **PR164 원격 반영 완료:** head9cf5ccf의 CI36936941218은 분류·Workflow Validation·CI Gate3개 성공이며 제품3개는 영향 범위 밖으로 skip이다. 사용자의 전체 병합·공개배포 승인 범위에서07:50:53 KST mainfa9a2fc로 병합했다. 실제ops archive3파일은해당commit 바이트와일치하고 검토head대비scripts diff0, 검증image source4affefc대비전체build입력diff0이다. 새v5 양쪽Announced/회차·본문·시간·popup·notice NULL 대조PASS, 원래중계session49496은소유PID/명령확인후정상종료했다.
- **현재 대기 경계:** 거부된 추가실자료probe 또는같은반출목적을다른명령/배포경로로재시도하지않았다. 자동승인검토가요구한 구체적범위를 사용자에게명시했다: 청주·오산·Directory 전체자료와비밀번호제외역할정보를 본인Mac의 `/Users/parksubin/Documents/PMS-Recovery/2026-10-01-business-schema` 및 `/private/tmp/pms-business-schema-20261001-operating`에가져와 network-none 일회용Docker에서대조, 평문정리/암호문·키보관 후공개배포. 이확인이도착하기전새운영dump/restore·v5release를시작하지않는다. 현재기존서비스/저장기능정상, 운영C209/130·O209/130·D8/4이고새이미지미적용이다. private v5 wrapper/source-binding/config/notice/사후schema검증기는준비됐으며기존prepare attempt 재실행금지다.
- mainfa9a2fc의 CI36937486651도 success를확인했다. 최종운영기록은현재작업branch의Task WIP로보존했으며 원본checkout WIP는건드리지않았다.
- **2026-10-02 07:57 KST 명시 재승인:** 지정된 Mac의 복구/임시 폴더로3DB 자료와비밀번호제외역할정보를가져와 일회용격리Docker에서복원·검증하고 평문정리/암호문·키보관하는 범위를 설명한 질문에 사용자가 “아니 전부 승인이야. 앞으로 나오는 승인이 필요한 작업까지 전체 승인. … 빨리 배포해”라고 답했다. 앞선자료반출거부의구체적승인부족이해소됐다. 같은배포범위의백업·복원·검증·배포·보정은재질문하지않고진행하며 상위도구정책을우회하지않는다. v5 release attempt는아직없고 안내종료08:56:12 KST까지약59분이남아있어기존준비회차를이어간다.

- **v5 실제 소규모 복원 성공 / Azure 시작 응답 중단:** 명시 재승인 후 단일 소유 relay를 이어서 Directory 실자료 복원·원본/복원 schema 및 inventory·identity·CONNECT matrix 비교가 모두 PASS했다. bootstrap search_path의 public 제외 조건에서도 보정 검증을 통과했고 작업 소유 plaintext/container는 정리됐다. 이어진 v5 release는 첫 CHEONGJU verify-prepared의 시작 응답이 불명확해 exit79로 종료했다. checkpoint는prepared이고 activate·arm·전체backup·0131·새image 적용은 모두 미진입이다. runner가 기존 앱을 중단했으며 원attempt/checkpoint를 보존한다.
- 실행 이력과 Activity Log에 해당 새 실행은 관측되지 않았으나 미접수 확정으로 확대하지 않는다. 실제 candidate의 verify-prepared는 ReadAsync와 회차/본문/일정 비교 후 반환하는 읽기 전용 경로임을 독립 검토했다. 같은 정확 조건의 독립 진단1회 maintenance-134klyl은08:21:51~08:22:18 KST Succeeded였으며 시작exit0/오류출력0이다. 최초 시작 오류의 원인은 미확정이다. 기존 서비스/원장·정확회차를 확인해 old revision을 복구하고 정상 fail→complete로v5를 종료한 뒤 새로운75분v6창을 준비한다. 기존40분 시작조건을 낮추거나 attempt를 덮어쓰지 않는다.
- v5 복구/v6 연속 wrapper 독립 검토GO(새P1/P2없음)를 받은 뒤 상태 출력 라벨만 실값으로 보정했다. 기존 승인 범위에서 실행을 시작했다. v6는 별도UUID·exclusive attempt, 기존75분 안내창·시작시40분 초과 조건·mainfa9 실행3파일 결속을 유지하며 v5 결과를 덮어쓰지 않는다. 실제 모델 식별은NOT_REPORTED.
- **v5 복구 절차 보정:** 기존 backend--0000068/frontend--0000059 활성화와health200/root·api401은 성공했다. 이후 old02028 CLI의fail job maintenance-ezqo527이 terminal Failed가 됐다. 실제old Store는Announced→Failed를지원하지않고Active/Delayed/Completed만허용하는데현재Store와혼동했다. Azure 내부 고정분류 조회에서도 `release_maintenance_transition_invalid`1건으로확인했다. 첫wrapper 검토GO의버전별실구현대조가부족했으며 v6 prepare/backup/DDL은미실행이다. 원문로그로컬저장은자동승인검토가민감정보가능성으로거부하여수행하지않았고 원문을반환하지않는서버내고정오류분류/횟수조회로진단했다.
- 수정된복구는정확v5 Announced/기존3DB원장·표/구이미지health를다시확인한뒤, 검증된candidate4aff의사업부고정CLI로fail을각사업부1회실행하고Failed를SQL확인한다. 이어old02028의complete를1회실행하여기존원장readiness를검증하고양쪽Completed를확인한다. 운영DB직접상태수정이나시간연장용activate는쓰지않는다. 기존시도는보존하고새복구attempt를분리한다.
- **v5 정상 종료 / v6 본배포 시작:** 보정된42413576/f03eaae4 wrapper 독립 검토GO 후08:39 KST v5 양쪽Completed 및세DB 기존원장/표를확인했다(exit0). v6는08:39:34~09:54:34 KST 안내창으로prepare1회성공(exit0)했고양쪽정확회차·본문·시간·Announced·popup·notice NULL을SQL확인했다. 실제청주업무홈에서업데이트팝업/저장제한배너/버튼비활성화도확인했다. 본release는실행중이며checkpoint `/var/folders/xs/syr004417s917jpzcv741wnc0000gn/T/pms-recovery-checkpoint.g6xcXi` armed, 연결wrapper session82265가소유relay와후속검증을관리한다. 실제전체backup/0131/신이미지성공과구분한다.
- **v6 백업·실복원 성공, 감시429로 gate 중단:** 실제3DB dump(C약44MiB/O약3416MiB/D약0.1MiB), canonical구조, 암호화/복호화byte일치, 격리3DBfullrestore, 구조·데이터inventory·role/CONNECT proof대조 및원본불변재조회가driver create전체를통과했다. 사본3660323798bytes와manifest4508bytes가승인된복구폴더에CMS로게시됐고평문작업폴더는정리됐다. 그러나감시quiet가이전실행본약130개를매회8병렬개별조회하면서08:53:11 KST replica list429TooManyRequests3회실패했고, worker종료를기다린뒤09:16 waitgate를닫았다. checkpointg6xcXi는armed+정형azureReadFailure이며candidate/verified·0131·신이미지적용0, releaseexit79다. 기존앱자동복구후실패회차정상종료를수행한다.
- Microsoft공식revision list API는properties.replicas를현재실행pod수integer로정의한다. 실제backend전체70개응답도inactive69개0/oldactive1개1로확인했다. 반복개별조회 대신두앱의일괄조회로strictactivefalse/replicas정수0을검사하도록보정한다. 누락·bool·문자·음수·양수·활성화·새Job거부와drain/실복원gate를유지한다. 별도impl agent는helper/unit/mock만소유하며root는운영복구/Task, 독립reviewer는실diff/증거검토를담당한다. 과거백업의회차binding을바꾸거나실패checkpoint를수동verified로올리지않는다.
- v6 암호화manifest를비공개임시폴더에서복호화해회차/evidenceKind와3DB sourceProofSha256=restoreProofSha256를독립확인했다. 기록된create시작08:48:46/완료09:15:45 KST이며role비밀번호는복원하지않고인증정보재결합필요가명시돼있다. manifest평문은즉시정리했다. 복구wrapper8af35620/33a1b8fb는old완료CLI실행exit0, 후속SQL에서C209/130·O209/130·D8/4와정확v6 Completed를확인했고소유relay를정상종료했다. 새배포성공과구분하며원backup/CMS와실패증거는보존한다.

- **반복 감시 보정 검증 완료:** helper798358be/unit a80a4b5b/mock31e5040e 고정본에서 unit30/30 PASS 및 release141/141 PASS(exit0), Python syntax/diff-check PASS다. 별도 reviewer의 실제5파일·계약·공식 API/설치CLI pagination 검토는 GO, 새P1/P2 없음이다. `--all`은 nextLink를 끝까지 읽으며 앱당 CLI1회로 모든실행본을 검사한다(HTTP요청수가항상1회라는뜻아님). exact-head required CI·병합·새v7 backup/배포는 다음 상태로 남긴다. 사용자의 반복된전체승인범위에서같은작업의재승인은요청하지않는다.
