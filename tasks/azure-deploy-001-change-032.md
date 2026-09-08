# TASK-AZURE-DEPLOY-001 Change 032 — 오산 1단계 운영 공개 배포 실행

## 1. 승인·Gate·기준선

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- approvalSource: `USER_EXPLICIT_2026-09-08_AZURE_PUBLIC_DEPLOY`
- productionDeploymentApproved: `true`
- databaseProvisioningMigrationApproved: `true`
- runtimeHandoverApproved: `true`
- mainMergeStatus: `COMPLETED_BEFORE_CHANGE`
- sourceMainSha: `b405a9cb653aa56b1049a7e7595a2e232044b42d`
- sourcePrHead: `26289cdb4bc67b7612e0c6142107ddeeddbbc2e3`
- sourceCiRun: `34166105345`
- hotfixPr: `#122`
- hotfixMainSha: `08c5366ff6ccfe34d4945b974e25c8ef93ee1121`
- hotfixCiRun: `34186728030`
- hotfixReleaseRun: `34188740914`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH_ONLY`
- implementationOwnerObserved: `NOT_REPORTED`
- gpt6ReviewProhibitedByUser: `true`
- status: `CHANGE_008_LOCAL_VALIDATED_AWAITING_PR_CI_REDEPLOY`

사용자는 이미 병합된 오산 Task 1~3와 사용자 접근 Change 003~006을 Azure 운영에 공개 배포하라고 명시했다. 기존 Change 031의 부분 순서 override와 phase-1 범위를 유지하며 별도 rollout Task를 만들지 않는다. 전체 회귀는 exact merged source의 필수 CI에서 통과했으므로 로컬에서 반복하지 않는다.

## 2. Purpose identity와 worktree

- 업무 목표: 기존 Cheongju 운영·G2·provider를 보존하면서 Directory와 Osan 논리 DB를 추가하고 로그인 승인, 사업부 해석·관리, Osan 프로젝트 create/list/detail만 공개한다.
- Root Finding: 현재 운영은 단일 Cheongju 연결과 직전 image라 세 DB·통합 승인 계약을 실행할 수 없다.
- 변경·검증 경계: 운영 backup/restore, DB·role·secret-scope RBAC·manual job, migration·backfill, immutable image와 Backend/Frontend handover, 공개 보안·Cheongju/Osan 검증 및 배포 기록.
- 보존할 불변조건: 기존 운영 DB를 Cheongju로 그대로 유지, DB fallback 금지, identity contract Directory `0001`/Business `0086`, additive migration, 일반 사용자 한 사업부 이하, 지정 총괄만 다중 소속, 기존 provider·worker 설정과 업무 데이터 보존.
- boundedWorktree: workspace root의 `emi-azure-prod-032`
- branch: `fix/task-azure-deploy-001-osan-phase1-release`
- base: exact `origin/main` `b405a9cb653aa56b1049a7e7595a2e232044b42d`
- owner: `TASK-AZURE-DEPLOY-001 Change 032 / GPT-5.6 Sol xhigh`
- expectedEnd: 운영 배포·공개 검증·privacy-safe 기록 완료 뒤
- cleanupBoundary: process 미사용, clean, commit reachable와 후속 기록 게시 상태를 확인한 뒤 승인 범위에서만 정리한다.

## 3. 실행 순서와 중단 조건

1. Azure·GitHub·PostgreSQL과 현재 public release를 privacy-safe read-only projection으로 재확인한다.
2. 14일 PITR와 per-database logical backup·별도 복구 rehearsal로 세 DB layout의 실제 복구 가능성을 확인한다.
3. 기존 업무 DB를 Cheongju로 유지한 채 같은 PostgreSQL server에 빈 Directory·Osan DB, 분리된 admin/migrator/runtime secret·role과 manual job만 준비한다. Public app revision 변경이 보이면 중단한다.
4. role bootstrap → Directory `0001..0003`와 Cheongju·Osan business `0001..0087` migration → 기존 승인 계정의 Cheongju membership backfill 순서로 실행한다.
5. DB 이름·same-server·identity·ledger·role 최소 권한과 no-fallback을 확인한 뒤 exact main SHA image를 build/publish하고 immutable digest를 기록한다.
6. Backend digest와 readiness·routing·security를 먼저 확인하고 Frontend digest와 readiness를 적용한 뒤 public health/security, Cheongju regression, 제한된 Osan registration readiness를 확인한다.
7. 어느 단계든 실패하면 downstream을 중단한다. DB 실패는 기존 public app과 Cheongju를 유지하고, app 실패는 직전 immutable image로 되돌린다. Additive migration은 down하지 않고 forward-fix한다.

## 4. 운영 계약

- Backend runtime은 Directory/Cheongju/Osan runtime 연결 3개, migration은 3개, bootstrap은 admin/migration/runtime 9개를 사용한다.
- 기존 legacy `QmsDatabase` admin/migration/runtime secret은 Cheongju와 직전 image rollback을 위해 유지한다.
- Directory latest ledger는 `0003`, 두 business latest ledger는 `0087`; expected identity contract 상수는 Directory `0001_business_unit_directory`, Business `0086_business_unit_database_identity`다.
- 기존 active/system-admin identity를 식별자 원문 없이 집계하고 idempotent backfill로 Cheongju membership을 보존한다. 지정 overall admin 입력은 private secret으로만 전달한다.
- 오산 notification/escalation/admin-deletion worker와 외부 provider는 off다. Cheongju 기존 notification·provider 설정은 보존한다.
- 가짜 운영 사용자·프로젝트·업무 레코드를 만들지 않는다.

## 5. Exact allowlist

배포 실행은 병합된 workflow·SOP·script를 사용한다. 사전 점검 또는 실제 Azure validation에서 배포를 막는 필수 결함이 확인되면 승인된 phase-1 범위의 최소 보정만 적용한다. 결과 기록과 확인된 배포 결함 보정의 allowlist는 다음과 같다.

- `tasks/azure-deploy-001-change-032.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `tasks/osan-pilot-001-rollout-handoff.md`
- `tasks/osan-access-001-implementation-report.md`
- `tasks/osan-project-001-implementation-report.md`
- `docs/00-product-roadmap.md`
- `.github/workflows/azure-pilot-images.yml`
- `scripts/deploy-azure-pilot-release.sh`
- `scripts/test-azure-pilot-release.sh`
- `infrastructure/azure-pilot/workloads.bicep`
- `infrastructure/azure-pilot/workloads.json`
- `scripts/validate-azure-pilot-artifacts.sh`

배포 정의·script의 범위 내 결함 보정이 필요하면 변경 전에 이 allowlist와 Finding을 갱신한다. Secret, 실제 identifier, 실제 사용자·프로젝트·업무 원문, `.env`, local parameter와 backup artifact는 tracked/staged하지 않는다.

## 6. 완료 증거

- source SHA, workflow run, image digest 존재 여부와 active revision의 SHA 일치
- backup/PITR 상태, logical backup·별도 restore 결과와 aggregate 일치
- DB 수, DB별 migration applied/expected·identity exact, bounded role allow/deny 결과
- backfill 대상/완료/오류 aggregate와 일반 dual-membership 위반 0
- Backend·Frontend Ready/Running과 public health `200`, 익명 root/API `401/401`, direct origin 차단
- Cheongju 주요 aggregate unchanged, Osan project count와 create 권한 준비 상태, phase-1 제외 route·worker·provider disabled
- 실제 Microsoft 365 로그인과 첫 실제 프로젝트 입력을 직접 관찰하지 못하면 사용자 확인 대기로 유지

## 7. 실행 중 증거와 Finding

- 14일 server PITR 상태를 재확인했고, VNet 내부 임시 작업으로 기존 Cheongju DB의 custom-format logical dump를 별도 scratch DB에 복구했다. schema table `148`, migration ledger `85`, 전체 table row count 비교가 일치했으며 scratch DB와 임시 작업은 제거했다.
- 기존 active Entra 사용자 `23`, active system-admin `3`을 PII 원문 없이 집계해 backfill·overall-admin private input으로 저장했다. 임시 inventory 작업은 제거했다.
- Directory·Osan용 새 연결 secret `6`과 backfill 입력 secret `2`를 만들었고 기존 Cheongju secret은 변경하지 않았다.
- secret-scope RBAC what-if는 `Create 12 / Deploy 14 / Modify 0 / Delete 0`이었고 신규 assignment `12`를 적용·검증했다. 기존 수동 assignment 이름 하나는 runtime adoption parameter로 보존했다. vault 직접 범위의 Key Vault Secrets User assignment는 `0`이다.
- DB/jobs 전용 deployment what-if는 처음 `Create 3 / Modify 2 / Delete 0`, public app mutation `0`이었다. Directory·Osan DB와 기존 bootstrap·migration job 반영은 성공했으나, 새 membership backfill job 이름 `business-unit-membership-backfill`이 Azure Container Apps job의 32자 제한을 1자 초과해 해당 리소스만 실패했다. 이 시점에 public image는 변경되지 않았다.
- phase-1 배포 blocker를 해소하기 위해 job resource/container/workflow 참조를 29자인 `business-unit-member-backfill`로 통일했다. 배포 artifact 정적 검증은 PASS했다. 보정 후 what-if `Create 1 / Modify 2 / Delete 0`, public app mutation `0`으로 적용했고 사용자 DB `3`, manual job `3`, public image unchanged를 확인했다.
- GitHub OIDC의 `Container Apps Jobs Contributor`는 세 작업의 exact resource scope에서 각각 1개임을 확인했다. 기존 migration assignment를 기준으로 부족한 `2`개만 추가했고 resource-group scope assignment는 `0`이다.

## 8. 최초 phase-1 공개 배포 결과

- Exact main `b405a9cb653aa56b1049a7e7595a2e232044b42d`, release run `34181334545`로 role bootstrap→Directory `0001..0003`/business `0001..0087` migration→Cheongju membership backfill→Backend→Frontend를 완료했다.
- 배포 당시 Directory는 ledger `3`, identity `1`, identity row `23`, active membership `23`, overall `3`, ordinary dual membership `0`이었다. Cheongju는 ledger `87`, identity `1`, active user `23`, project `3`, 기존 G2 aggregate `137`을 유지했고 Osan은 ledger `87`, identity `1`, active user·project `0`이었다.
- 별도 PITR server에서 세 DB ledger·identity·privacy-safe aggregate 일치를 확인한 뒤 owned restore server와 검증 job을 정리했다. Runtime role은 세 DB `3/3` allow와 cross-DB `6/6` deny를 통과했다.
- Backend `sha256:ad3cba144629f8ad23cc6b1fa915cffcffb1e6d7f76a5023ec0c6cc06b84d367` / revision `backend--0000038`, Frontend `sha256:204be70a6741447da9b09685412f79e0dc95a967e43bf0323e9bf56f9e11b808` / revision `frontend--0000027`이 각각 Healthy·traffic 100%다.
- 공개 `https://pms.emiinc.co.kr` health와 익명 인증 차단, direct origin 차단, 기존 Cheongju 관리자 로그인·데이터 aggregate, Osan registration-only route와 worker/provider off를 확인했다. Synthetic 운영 project는 만들지 않았다.

## 9. Change 007 승인된 운영 결함 보정

사용자는 공개 화면에서 이름·계정 ID 누락, 총괄 지정 입력 부재와 총괄의 Osan membership·selector 누락을 확인하고 수정부터 PR·main merge·Azure 재배포까지 승인했다. Canonical change는 `TASK-OSAN-ACCESS-001 Change 007`이다.

- Directory `0004`를 additive 적용하고 기존 overall 세 명의 Osan local profile을 먼저 준비한 뒤 두 membership을 멱등 backfill한다. 예상 privacy-safe 결과는 overall 수 유지, Osan active profile/membership 증가, ordinary dual `0`이다.
- 배포 정의는 기존 migration identity의 Osan migration secret-scope를 backfill job에서 참조한다. 새 DB·서버·vault-scope 권한과 기존 Cheongju 데이터 변경은 없다.
- Hotfix 순서는 migration→backfill dry-run/result→Backend→Frontend→공개 UI/API/DB aggregate 검증이다. 현 운영 digest 두 개를 rollback point로 고정하고 schema는 down하지 않는다.
- Local 집중 검증은 Backend compile, Directory `0004` fresh/existing, 통합 승인 3-DB, Frontend targeted/typecheck, mock/actual 3-DB smoke와 Azure artifact 검증이 통과했다. 최종 전체 회귀는 PR CI 한 번을 대기한다.

## 10. Change 007 hotfix 공개 결과

- PR #122 exact head `3d337c69bb225e324fc8a2339e18f68420d0c63d`의 CI run `34186728030`에서 Backend 전체, Frontend 전체, Full-Stack `66/66`과 required CI Gate가 통과했다. 승인된 squash merge의 exact main은 `08c5366ff6ccfe34d4945b974e25c8ef93ee1121`이다.
- Release run `34188740914`는 Directory `0004` migration, 기존 총괄 local-first backfill, Backend, Frontend와 public security smoke를 순서대로 완료했다. Backend는 `sha256:35bf38c3c3997c1106885efd0b8c85ebd251cb148d24a48003c25205b2409175` / `backend--0000039`, Frontend는 `sha256:ed83bf8d075b3cac04a740218e6652074534661a9216387aa8444bcd0d42794d` / `frontend--0000028`이며 각각 Healthy·traffic 100%다.
- Privacy-safe post-deploy projection은 Directory ledger `4`, identity contract `1`, active identity `23`, active membership `26`, overall `3`, overall dual membership `3`, ordinary dual `0`이다. Cheongju는 ledger `87`, identity contract `1`, active user `23`, System Administrator `3`, project `3`, G2 aggregate `137`로 불변이다. Osan은 ledger `87`, identity contract `1`, active user·System Administrator `3`, project `0`이다.
- Owned read-only aggregate job은 성공 뒤 삭제했다. Public health `200`, 익명 root/API `401/401`, 이름·계정 ID가 채워진 사용자 `23`행, 총괄 `3`명, 같은 행의 총괄 checkbox, 우측 상단 selector와 청주↔오산 전환을 실제 로그인 세션에서 확인했다. Osan은 관리자 navigation 없이 project list와 입력 form이 열렸고 synthetic record는 저장하지 않았다.
- 일반 사용자 실제 Microsoft 365 계정의 selector 미노출, 같은 행 저장의 실제 운영 mutation, 첫 실제 Osan project 저장은 사용자 검수로 남긴다. 마지막 총괄 차단·일반 사용자 단일 membership·원자성은 exact head CI와 post-deploy aggregate로 확인했다.
- Application rollback point는 최초 phase-1 Backend/Frontend digest와 revision `backend--0000038` / `frontend--0000027`이다. Directory `0004`는 additive로 유지하며 down migration을 하지 않는다.

## 11. Change 008 접근 정합성 후속 hotfix

- 운영 검수에서 부서 이동 뒤 관리 파생 역할·부서장 잔존, Directory membership과 local readiness의 승인 상태 불일치, System Administrator의 후속 permission 누락을 확인했다. 사용자는 세 결함의 제품 보정, privacy-safe 운영 repair, PR·main merge와 Azure 재배포를 명시 승인했다.
- Access Change 008은 Business additive `0088`, 역할 assignment source, 공통 readiness, stale operation retry와 readiness-aware 기존 backfill repair를 사용한다. Directory migration과 identity contract 상수는 바꾸지 않는다.
- Local 집중 검증은 compile/typecheck, migration `1/1`, 격리 3-DB `1/1`, compact UI mock `3/3`이 통과했다. 전체 회귀는 PR exact head의 required CI 한 번만 수행한다.
- 재배포 순서는 운영 dry-run aggregate → Business `0088` 양 DB → backfill repair → Backend → routing/security/권한 smoke → Frontend → 실제 로그인 UI smoke다. 예상 밖 identity 대상·삭제·권한 확대가 나오면 해당 단계만 중단하고 현재 revision `39/28`을 유지한다.

## 12. Change 008 병합·DB 준비와 inspection gate

- PR #123은 exact head `f947ae352ba7dc11475cc1e6feda24dcbb810df1`, CI `34204857666` 전체 PASS 뒤 승인 범위에서 squash merge했다. 배포 source는 exact main `d1e7d1fb20fa8976843b441ba7b5e46b54723aee`다.
- Database prepare-only run `34207947257`는 Backend image `sha256:300a6632c650c0f8bec481db923a6305540978918d8673adb142e7dcf6f2f823`을 게시하고 migration을 완료했다. Backend/Frontend public revision과 repair backfill은 이 단계에서 SKIPPED되어 rollback point `39/28`을 유지한다.
- 기존 release에는 repair dry-run mode가 없어 action-time gate의 숫자 증거를 만들 수 없었다. 사용자 승인 범위의 최소 보정으로 기존 manual backfill job을 one-off rollback-only inspection으로 실행하는 workflow 입력을 추가하며, 새 secret·role assignment·DB·영구 job 변경은 만들지 않는다.
- Inspection 결과는 승인 identity, overall, membership activate/deactivate, default-role normalization, managed-role removal, department-head reset 수만 기록한다. Job 실패 또는 marker 누락은 repair apply와 app handover를 막는다.

## 13. Inspection 실행 실패와 최소 보정

- PR #124 exact head `4482938eca69cccd3c747a0cbbdb742e4d7885ec`의 CI `34212325496`이 전체 PASS했고, 승인된 squash merge의 exact main은 `f7fe9f5355deea3d19562969653339124ac1eb6e`다.
- 첫 운영 inspection run `34215200743`은 one-off job execution 생성 전에 `MEMBERSHIP_BACKFILL_INSPECTION_FAILED`로 중단됐다. Public Backend/Frontend와 repair data는 변경되지 않아 기존 revision `39/28`을 유지한다.
- Azure CLI에 전달하는 leading-dash container argument가 option으로 다시 해석되지 않도록 `--args=<value>` 형태로 고정한다. Release mock은 이 exact argument shape를 요구해 동일 회귀를 차단한다.
- 보정 범위는 release script와 mock test뿐이다. 새 resource·secret·RBAC·영구 job 설정은 만들지 않으며, exact main CI 뒤 rollback-only inspection을 다시 실행한다.

## 14. Inspection one-off 환경 보존 보정

- PR #125 보정은 exact main `3c859f56359d1fc9c305a51ffd0f162cdfb86f27`에 반영됐고, 두 번째 inspection run `34216520695`는 새 image build와 exact inspect argument 전달 뒤 앱 시작 단계에서 중단됐다. Public revision과 repair data mutation은 `0`이다.
- Privacy-safe Log Analytics 확인 결과, one-off container override가 기존 manual job의 환경 변수 `31`개를 상속하지 않아 execution의 환경 변수가 `0`개가 됐다. 이 때문에 실제 job template에 존재하는 BusinessUnits 3-DB 설정 대신 사용하지 않는 legacy 단일 DB 연결 검증 경로가 선택됐다.
- Inspection 시작 전 기존 job template에서 값 환경과 secret reference, CPU, memory를 읽고 필수 3-DB migration 연결·승인 identity 입력이 모두 있는지 검증한다. One-off execution에는 이 template을 복사하고 exact immutable image와 inspect argument만 바꾼다. 원문 secret·identity는 출력하거나 tracked file에 기록하지 않는다.
- Template 환경 누락·형식 오류는 `MEMBERSHIP_BACKFILL_INSPECTION_CONFIGURATION_INVALID`로 repair·handover 전에 중단한다. Release mock은 환경·resource 보존과 누락 fail-closed를 직접 검증한다. 영구 job update, DB write, public app handover는 이 보정에 포함되지 않는다.

## 15. 현재 Directory 총괄 designation 기준 보정

- PR #126 exact head `0fd3c1c622a7c73544a111df57bb06f49baec00f`의 CI `34219166432`가 통과했고, 승인된 squash merge의 exact main은 `a76d5014fe47c7cfcc8a2bf935c05d0d772cb6cf`다.
- 세 번째 inspection run `34219334527`은 one-off execution에 환경 변수 `31`, secret reference `5`, CPU·memory와 exact inspect argument를 모두 보존했다. 다음 application gate에서 현재 active membership `2`개인 identity가 최초 bootstrap overall 목록에는 없다는 이유로 ordinary로 오분류되어 중단됐고, 운영 data·public revision mutation은 `0`이다.
- 총괄은 통합 사용자 관리에서 추가·해제할 수 있으므로 최초 bootstrap secret은 빈 Directory의 최초 seed에만 사용한다. Directory에 active overall designation이 하나 이상 있으면 현재 Directory 상태를 권한 source of truth로 사용해 동적으로 추가된 총괄과 해제된 총괄을 그대로 반영한다.
- Inspection marker는 effective overall, configured bootstrap overall, active Directory overall 수를 따로 기록한다. 식별자 원문은 기록하지 않는다. 격리 3-DB 단일 Fact는 bootstrap 목록이 비워진 뒤에도 현재 active overall의 두 membership과 권한이 유지되고 audit가 중복되지 않음을 검증했다.
- 집중 검증은 Backend Release compile warning/error `0/0`, 3-DB Fact `1/1`, release mock, Bash syntax·ShellCheck와 Azure artifact static validation이 모두 PASS했다. Exact PR head의 required CI 통과 전 운영 inspection을 다시 실행하지 않는다.
