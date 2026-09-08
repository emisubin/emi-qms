# 오산 시범 운영 — 기존 Azure 배포 Task로의 인계

## 2026-09-07 phase 1 partial rollout override

사용자는 후속 Task 4~6보다 먼저 완료된 Task 1~3만 Azure에 배포해 실제 프로젝트 기준정보 입력을 시작하도록 명시했다. 운영 Change는 기존 `TASK-AZURE-DEPLOY-001 Change 031`이며 새 rollout Task를 만들지 않는다.

- 포함: Directory/사업부 해석, no-membership·local-profile-pending gate, 총괄 membership과 선택 사업부 local role 관리, Osan 프로젝트 create/list/detail.
- 제외: 진행 mutation, 7단계 자동 완료, dashboard, Pending/hold/cancel/deleted/Excel, Osan external provider와 worker.
- 기존 생산 DB는 Cheongju로 그대로 유지하고 Directory·Osan DB만 같은 server에 추가한다. DB 준비·PITR rehearsal 뒤 serving을 연결한다.
- 일반 사용자의 dual-membership 전환 결함은 사용자 승인으로 보류했다. Phase 1 일반 사용자는 membership 한 곳만 부여한다.
- Selector Change 002와 Change 003·004의 통합 승인·compact 표·자동 진입·청주 전용 관리 동선은 exact-head 3-DB 검수 뒤 사용자가 2026-09-08 `다음작업 승인.`으로 수락했다.
- 기존 Draft PR #121의 같은 head branch non-force 갱신과 최종 remote CI 1회가 승인됐다. CI 뒤 exact `main` merge 승인 전에서 멈춘다.
- Exact main merge 전에는 Azure mutation을 시작하지 않는다. 병합 뒤에도 DB/role/identity/migration → restore → Backend → Frontend → public/Cheongju/Osan 순서를 지킨다.

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- sourceTask: `TASK-OSAN-PILOT-001`
- taskType: `UAT_RUNTIME`
- status: `CHANGE_003_004_USER_VALIDATED_AWAITING_PR_CI`
- reuseExistingTask: true
- productionDeploymentApproved: true
- migrationExecutionApproved: true
- gitPublicationApproved: true
- mainMergeApproved: false
- selectorUserValidation: `COMPLETED`
- integratedUserApprovalValidation: `COMPLETED`
- automaticBusinessEntryValidation: `COMPLETED`
- latestUserApprovalSource: `USER_EXPLICIT_2026-09-08_NEXT_TASK_APPROVED`

## 목적과 기존 Task 재사용

[오산 기획](osan-pilot-001-planning.md)의 운영 개통은 기존 Azure 배포 Task의 책임이다. 같은 목적의 TASK-OSAN-ROLLOUT 같은 별도 Task를 만들지 않는다. 현재 마지막 확인 change는 [Change 030](azure-deploy-001-change-030.md)이며, 실행 시 최신 identity gate를 확인해 그 시점의 다음 change 번호를 사용한다. 지금 다음 번호를 예약하거나 과거 배포 승인을 재사용하지 않는다.

## 선행조건

[통합 검증 Task](osan-validation-001.md) 통과, 기획·review resolution·코드 검수, 필요한 사용자 검수, 해당 Git 게시·main 병합과 운영 개통 범위의 명시 승인. 한 메시지가 해당 범위를 명시하면 동일 승인을 반복 요청하지 않는다.

## 개통 범위

- 실제 기존 PostgreSQL 서버의 capacity·backup·network·identity·secret 참조를 read-only 점검하고 확인 결과로 필요한 자원 변경만 확정한다.
- 같은 서버에 오산 업무 DB와 승인된 최소 공통 directory, 계정·권한·schema를 준비한다. 새 PostgreSQL 서버는 이 기획의 기본 범위가 아니다.
- 기존 청주 DB/사용자 ID/업무 데이터와 G2를 보존하고 확인된 기존 계정의 membership만 연결한다.
- exact source의 호환 가능한 app/migration을 승인된 순서로 적용한다. 오산 capability는 준비 완료 전 비활성 상태를 유지한다.
- 지정 총괄·오산 관리자의 실제 소속/권한을 승인된 private 입력으로 연결하고 오산 외부 provider는 계속 비활성화한다.
- 청주 확인 → 제한된 오산 사용자 확인 → 개통의 순서로 진행한다. 실제 사용자를 대신해 검수 완료로 기록하지 않는다.

## 실패·복구와 완료 증빙

OSAN-REVIEW-003의 오입력 신고·담당자·변경 금지 안내를 [통합 검증 Task](osan-validation-001.md)에서 인수한다. 실제 정정/재개는 사용자 권한과 기록 보존 계약의 별도 승인 없이는 수행하지 않는다.

한 DB 실패를 다른 DB로 fallback하지 않는다. 오산 개통 실패 시 오산 진입/작업을 차단하고 청주 호환성을 확인한다. directory 도입 전 코드로 되돌릴 수 있는지와 실제 migration 호환성을 사전에 검증하며 단순 image rollback만으로 복구된다고 가정하지 않는다.

데이터 복구는 승인된 대상 DB에 한정한다. 전체 서버 복구본을 운영 청주·오산에 일괄 덮어쓰지 않는다. source·configuration·DB compatibility·worker ownership·rollback을 기존 배포 SOP와 연결해 확인한다.

완료 증빙은 실제 적용 source, DB별 ledger/권한·health projection, 청주 회귀, 오산 1/N 대상 전체 완료, 외부 provider 비활성, 사용자 검수와 후속 관찰이다. Secret·개인 식별자·업무 원문은 tracked 기록에 넣지 않는다.

## Change 031 로컬 준비 결과

Backend `582/582`, Frontend `297/297`, mock browser `13/13`, Full-Stack `66/66`과 Bicep·ARM·release mock·workflow 정적 검증을 통과했다. 일반 Full-Stack `64`건과 별도 3-DB business-unit/Osan 시나리오 `2`건을 CI에서도 같은 경계로 실행하도록 정렬했다. 실제 Azure mutation은 없으며 Draft PR·필수 CI 다음에 selector 사용자 검수와 exact `main` merge 승인 Gate가 남는다.

위 수치는 PR #121 exact head `11c1185ea9c550022e3f70d106e06a1c6bc517b1`의 Change 031 역사적 결과다. Change 003·004의 새 증거로 재사용하지 않는다. Change 003·004 사용자 검수는 완료됐으며 다음 순서는 기록 commit → 최신 `origin/main` 정합성 확인 → 승인된 non-force push로 PR #121 갱신 → 최종 remote CI/회귀 1회 → exact `main` merge 승인 대기다. Exact `main` merge 전에는 Azure와 운영 DB를 변경하지 않는다.

## 2026-09-08 phase-1 공개와 Change 007 hotfix

PR #121은 exact main `b405a9cb653aa56b1049a7e7595a2e232044b42d`로 병합됐고 release run `34181334545`에서 Directory/Cheongju/Osan migration, 기존 Cheongju membership backfill과 Backend·Frontend 공개 전환이 완료됐다. Directory `0001..0003`, 두 business `0001..0087`, 14일 PITR·logical/PITR restore, 세 DB role/no-fallback과 공개 보안 검증이 통과했다. 오산 external provider/worker는 계속 off이며 synthetic 운영 project는 만들지 않았다.

운영 사용자 검수에서 기존 계정 표시와 총괄의 두 사업부 접근이 불완전한 결함을 확인했다. `TASK-OSAN-ACCESS-001 Change 007`은 이름·계정 ID의 identity-bound 병합, 한 저장의 총괄 지정, 복수·마지막 총괄 보호, 두 local System Administrator profile과 membership, 실제 selector를 보정한다. PR #122와 exact main `08c5366ff6ccfe34d4945b974e25c8ef93ee1121`을 release run `34188740914`로 배포했고 Directory `0004`, 총괄 `3`명의 양 사업부 backfill, Backend·Frontend와 public smoke가 통과했다. 일반 사용자의 한 사업부 규칙, DB 분리와 Cheongju 데이터는 유지된다.

후속 운영 검수의 부서 이동 권한 잔존·승인 readiness 불일치·System Administrator permission 누락은 Access Change 008로 보정한다. Business additive `0088` 뒤 readiness-aware backfill repair와 새 Backend·Frontend를 적용한다. Local 집중 migration/3-DB/UI는 통과했고 exact PR head CI 한 번과 운영 dry-run aggregate를 다음 Gate로 둔다. 기존 revision `39/28`, Cheongju 데이터/provider와 Osan worker off를 rollback·보존 기준으로 유지한다.
