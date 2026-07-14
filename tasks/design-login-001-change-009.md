# TASK-DESIGN-LOGIN-001 Change 009 — 로그인 화면 단위 승격

## 1. 상태와 승인

- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION`
- 사용자 검수: 완료 — TASK-DESIGN-LOGIN-001 전체 체크리스트 확인 완료
- 사용자 승인: 최신 `origin/main` 기반 clean promotion branch와 bounded worktree 생성, 로그인 화면 fixed allowlist 이식
- 추가 사용자 승인: 2026-07-15 stage·commit·push·PR·merge와 5174 Frontend 반영을 일괄 승인

## 2. Task Identity Gate

- proposedTaskId: `TASK-DESIGN-LOGIN-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-DESIGN-LOGIN-001`
- roadmapNextGate: `LOGIN_SCREEN_PROMOTION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-DESIGN-LOGIN-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

Purpose identity:

- 업무 목표: 5176에서 사용자 검수가 끝난 로그인 화면만 최신 main의 기능 기준선에 화면 단위로 승격한다.
- Root Finding 또는 정책 결정: 디자인 실험 branch 전체를 merge하지 않고 검수 완료 화면의 fixed allowlist만 이식한다.
- 변경·검증 경계: Frontend 인증 화면·관련 test·Figma asset·Task 추적 문서만 이식하고 최신 main 기능을 보존한 채 Frontend 전체 검증과 privacy-safe browser 검증을 수행한다.
- 보존할 불변조건: Microsoft 365 로그인, 로그인 상태 유지, silent token 재인증, Microsoft provider 계정 선택, 기존 인증 정책과 최신 main의 비로그인 기능을 보존한다.
- 예상 산출물: clean promotion worktree의 미커밋 allowlist diff, 전체 Frontend 검증 결과, 독립 Codex 검증과 게시 승인용 handoff.

검색 범위:

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 3. Bounded promotion worktree

- 목적: 실행 중인 5174·5176와 기존 작업 worktree를 보존하면서 최신 main에 로그인 화면 allowlist를 충돌 없이 이식·검증한다.
- owner: `TASK-DESIGN-LOGIN-001` Change 009 promotion session
- 기준 remote ref: `origin/main`
- 기준 SHA: `6f3eaf7a1eecd698f9fe9603e170452e70a64e8a`
- source worktree alias: `TASK_DESIGN_LOGIN_EXPERIMENT_WORKTREE`
- source branch/HEAD: `feat/task-design-login-001-entra-shell` / `bce88188608557464ffcdfcd696d8fda86e98053`
- promotion worktree alias: `TASK_DESIGN_LOGIN_PROMOTION_WORKTREE`
- promotion branch: `feat/task-design-login-001-login-promotion`
- 예상 종료 시점: 이식·전체 Frontend 검증·독립 검증을 마치고 사용자에게 게시 승인 handoff를 제공할 때
- cleanup 경계: worktree clean, process 미사용, open PR 없음과 commit reachable을 확인한 뒤 별도 승인 범위에서만 제거한다. branch 자동 삭제와 강제 제거를 하지 않는다.

## 4. Fixed allowlist

- `frontend/src/App.tsx`
- `frontend/src/auth.ts`
- `frontend/src/main.tsx`
- `frontend/src/styles.css`
- `frontend/src/assets/emi-logo.png`
- `frontend/src/assets/microsoft-logo.png`
- `frontend/src/assets/auth-ellipse-66.svg`
- `frontend/src/assets/auth-ellipse-67.svg`
- `frontend/tests/auth.test.tsx`
- `frontend/playwright.auth-shell.config.ts`
- `frontend/e2e/auth-shell/auth-shell.spec.ts`
- `frontend/e2e/auth-shell/loading.html`
- `frontend/e2e/auth-shell/loading.tsx`
- `tasks/design-login-001.md`
- `tasks/design-login-001-change-001.md`
- `tasks/design-login-001-change-002.md`
- `tasks/design-login-001-change-003.md`
- `tasks/design-login-001-change-004.md`
- `tasks/design-login-001-change-005.md`
- `tasks/design-login-001-change-006.md`
- `tasks/design-login-001-change-007.md`
- `tasks/design-login-001-change-008.md`
- `tasks/design-login-001-change-009.md`
- `tasks/design-login-001-implementation-report.md`
- `docs/00-product-roadmap.md`
- `docs/development/design-screen-promotion.md`

## 5. 보존·금지 경계

- 5174·5176 Frontend와 5081·5092 Backend, 5190 Review-safe, 5432 PostgreSQL runtime을 종료·재시작·교체하지 않는다.
- Backend·API·DB·migration·runtime configuration·dependency·lockfile를 변경하지 않는다.
- `TASK-GOV-HISTORY-REWRITE-001`, `TASK-GOV-FINDING-GATE-001` 파일과 기존 P2 상태를 변경하지 않는다.
- stage·commit·push·PR·merge와 5174 runtime 반영을 수행하지 않는다.

## 6. 승격 검증 Gate

- 최신 main과 source 실험본의 동일 경로 변경을 먼저 비교하고, 최신 main 기능을 덮어쓰지 않도록 파일별로 이식한다.
- 변경 경로가 fixed allowlist 안에만 있는지 확인한다.
- Frontend lint·typecheck·unit·build와 로그인 Desktop browser·mock UI 검증을 실행한다.
- 개인정보 안전 projection으로 runtime 미변경과 결과를 기록한다.
- 구현 session과 분리된 Codex read-only 검증이 승인 계약·diff·test·Finding gate를 확인한다.
- 검증 완료 뒤에도 게시와 5174 적용은 별도 사용자 승인 전까지 보류한다.

## 7. 실행 결과

- 최신 `origin/main` 기준 SHA와 promotion branch HEAD가 `6f3eaf7a1eecd698f9fe9603e170452e70a64e8a`로 일치한 상태에서 worktree를 생성했다.
- 최신 main과 source 기준점 사이의 로그인 제품 경로 overlap은 0개였다. 제품 코드·asset·unit/E2E fixture 12개는 사용자 검수본과 hash가 일치한다.
- `docs/00-product-roadmap.md`는 source 파일 전체를 복사하지 않고 디자인 관련 capability·queue·Task·tracking·Decision Log만 이식했다. 최신 main의 HTTPS-only 5174와 PR #48 기록은 보존했다.
- `frontend/playwright.auth-shell.config.ts`는 실행 중인 5176을 잘못 재사용하지 않도록 `AUTH_SHELL_PORT`를 지원하고 기본 격리 포트 5187, 기존 server 재사용 기본 false로 승격 검증 전용 보정을 적용했다.
- Frontend lint: error 0, 기존 Fast Refresh warning 1
- Frontend typecheck: PASS
- Frontend unit: 66/66 PASS
- Frontend build: PASS, 기존 chunk-size warning 유지
- promotion auth browser: 기본 로그인 6/6 + Loading 6/6 = 12/12 PASS
- mock UI smoke: 1/1 PASS
- 5187 검증 server는 test 종료와 함께 종료됐고 5174·5176 및 Backend·DB runtime을 재시작·교체하지 않았다.
- 독립 검증 중 게시 후보 문서 2개에서 local home-directory 절대 경로 3회를 P2로 발견했다. stage 전 모두 symbolic worktree alias로 치환하고 privacy scan을 다시 통과했다.
- stage·commit·push·PR·merge와 5174 Frontend 반영은 2026-07-15 사용자 승인에 따라 실행한다.
- 독립 Codex read-only 검증: PASS, 현재 P0/P1/P2/P3 `0/0/0/0`, 해결된 P2 1
- 다음 Gate: 고정 allowlist 게시·CI 확인, 5174 Frontend-only 반영·검증, squash merge 실행
