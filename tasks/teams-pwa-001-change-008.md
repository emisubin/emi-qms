# TASK-TEAMS-PWA-001 Change 008 — 로고 운영 rollout 상태 동기화

- taskType: `DOCS_GOVERNANCE`
- approvalSource: `USER_EXPLICIT_LOGO_AND_PUBLIC_DEPLOY`
- approvalDate: `2026-08-10`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- branch: `fix/task-teams-pwa-001-rollout-docs`
- baseSha: `37dd619685e6447fc867d213d1f63692c6cd8c62`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`

## 목적과 범위

Change 007과 `TASK-DESIGN-LOGIN-001 Change 010`의 실제 Git 게시·CI·Azure 운영 release 결과를 canonical 문서에 동기화한다. 제품 source, Backend, DB, migration, runtime configuration, Teams package와 PWA icon은 변경하지 않는다.

## 확인된 결과

- 로그인 로고·모바일 로그인 PR #83은 원격 `main`에 squash merge됐고 merge SHA CI가 성공했다.
- 내부 공통 로고 PR #84는 PR CI Frontend·Backend·Full-Stack `3/3` 성공 뒤 원격 `main`에 squash merge됐다.
- merge SHA `37dd619685e6447fc867d213d1f63692c6cd8c62`의 main CI는 첫 Full-Stack 실행에서 기존 Excel download event가 30초를 넘긴 1건을 제외한 `56/57`이 통과했다. 같은 시나리오의 격리 재실행 `1/1`과 실패 job 전체 재실행이 통과해 제품 결함이 아닌 일시적 browser event 지연으로 확인했다.
- 승인형 Azure release run `31354814082`가 exact main SHA로 Backend·Frontend immutable image를 게시하고 migration, Backend revision, Frontend revision과 public security를 모두 `PASS`로 완료했다.
- 배포 뒤 public health는 `200`, 익명 root·`/api/me`는 `401/401`이며, 브라우저는 app shell보다 먼저 Microsoft 365 로그인으로 이동했다.
- 인증 후 실제 PC·iPhone·Android에서 로그인·내부 로고를 눈으로 확인하는 항목은 사용자 운영 검수로 남긴다.

## Finding Gate

- Open P0/P1/P2: `0/0/0`
- 기존 GitHub Actions Node/Azure CLI annotation과 Frontend large bundle warning은 제품 기능을 차단하지 않는 기존 P3 유지보수 항목이다.

## 변경 allowlist

- `tasks/teams-pwa-001-change-008.md`
- `tasks/teams-pwa-001-implementation-report.md`
- `tasks/teams-pwa-001-user-validation-checklist.md`
- `tasks/design-login-001.md`
- `tasks/design-login-001-implementation-report.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`
