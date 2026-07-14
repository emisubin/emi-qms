# TASK-DESIGN-LOGIN-001 — Entra 로그인 공통 디자인 shell

## 1. 상태와 승인 기준

- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION`
- 승인 source: 2026-07-14 사용자 요청의 Figma node와 명시된 구현·검증 범위
- Planning/Review: 별도 Fable planning 대상 아님. 사용자가 같은 요청에서 구현 범위와 mutation 경계를 승인함
- Implementation: 완료 — 승인된 Frontend 범위만 변경
- 자동 검증: Change 008 완료 — Variant 2 Done icon `50% 50%` 중앙 정렬과 기존 회귀 검증 통과
- 독립 Codex 검증: Change 008·Change 009 완료 — 분리된 read-only session PASS, 현재 P0/P1/P2/P3 `0/0/0/0`, 해결된 P2 1
- 사용자 검수: 완료 — 전체 체크리스트 확인 완료
- 화면 단위 승격: Change 009 fixed allowlist 이식·자동·독립 검증 완료 / 게시·5174 반영 승인, 실행 중
- Commit·Push·PR·Merge: 2026-07-15 사용자 승인, 실행 중
- Development·Review-safe·Persistent UAT: 변경 없음
- 사용자 검수 preview: `http://127.0.0.1:5176/` ACTIVE — task-owned synthetic Entra Desktop 시각 검수 전용

## 2. Task Identity Gate

- proposedTaskId: `TASK-DESIGN-LOGIN-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-GOV-HISTORY-REWRITE-001`
- roadmapNextGate: `SUPPORT_COMPLETION_THEN_FINDING_REEVALUATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-DESIGN-LOGIN-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_CREATE`

Purpose identity:

- 업무 목표: 기존 Entra 로그인 동작을 보존하면서 승인된 Figma 디자인을 인증 상태 공통 shell로 구현한다.
- Root Finding 또는 정책 결정: History Support 대기 중 이 Task의 독립적인 Frontend 구현을 병렬 진행하도록 사용자가 명시 승인했다.
- 변경·검증 경계: Frontend 인증 UI, 관련 unit test, Figma asset, Task 종료 문서와 Roadmap만 변경하고 Frontend 전체 검증과 privacy-safe browser 비교를 수행한다.
- 보존할 불변조건: Microsoft 365 기본 로그인, 로그인 상태 유지, silent token 재인증과 기존 Entra 조건부 액세스·MFA·cache 정책을 보존한다. 다른 계정 선택은 Microsoft 로그인 화면의 provider UX를 사용하고 우리 Frontend에는 별도 action·request를 두지 않는다.
- 예상 산출물: 공통 auth shell, Figma와 일치하는 Desktop 로그인 화면, 관련 test, implementation report, Roadmap update와 사용자 검수 checklist.

검색 범위:

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 3. Figma 계약

- File: `2zikjJ8SpFXm50dH3PE4Cy`
- Node: `1:175` (`로그인 페이지 최종`)
- 기준 화면: 1440×810
- 구조: EMI red brand area + rounded white authentication panel
- 확인 항목: `get_design_context`, variable definitions, screenshot, asset URLs, metadata와 Code Connect
- Variables: node-scoped definition 0
- Code Connect: 현재 계정에 Organization/Enterprise의 Dev 또는 Full seat가 없어 조회 불가. 제품 코드에 기존 `*.figma.*` mapping도 없음
- Ellipse 68: Plugin API 직접 확인 결과 `x=-538.5`, `y=-468`, `876×876`, pattern opacity `0.33`, scaling factor `0.75`
- Frame/background: Plugin API 직접 확인 결과 base `#DA2127`, white 10% glass shape `-6/0/1446×810`, glass radius `23.25`
- White authentication shape: `776/0/664.5×810`, left radius `51`, shadow `-5.25/-1.5/43.05`, black opacity `0.28`
- Mobile: Change 001 사용자 승인에 따라 제외. Figma에 없는 390px 해석과 auth 전용 mobile browser project를 두지 않는다.

## 4. 포함 범위

- 로그인·MSAL 초기화/토큰 확인 loading·재인증·인증 오류·설정 누락 화면의 공통 shell
- Figma의 EMI/Microsoft brand asset 사용
- Desktop 1440×810의 Figma element만 표시하고 metadata 좌표·크기·색상·radius·shadow를 고정
- red/white 두 panel이 PC viewport를 항상 채우고 각 panel 내부 reference content만 등비 축소·확대해 작은 창에서도 모든 디자인 요소와 비율을 유지
- Ellipse 68을 실제 Figma 원형 경계와 pattern opacity로 렌더링
- 로그인 화면은 Change 004 승인 안내 1개를 제외한 Figma 비포함 content를 표시하지 않음
- Change 004에서 명시 승인된 `회사 Microsoft 365 계정으로 로그인해 주세요.` 안내 문구 1개를 Microsoft logo와 `LOGIN` 사이에 표시
- Change 006에서 Loading의 공통 shell과 안내 문구를 유지하고 `LOGIN`·checkbox 대신 빨간 회전 indicator 1개 표시
- Change 007에서 로그인 상태 유지 기본/Variant 2의 border·fill·Done icon·text color와 클릭 preference 전환을 Figma component set에 맞춤
- Change 008에서 Variant 2 Done icon의 크기와 나머지 style은 유지하고 checkbox 정중앙에 배치
- 화면 단위 승격 운영 기준을 `docs/development/design-screen-promotion.md`에 고정
- 기존 Microsoft 365 로그인, 로그인 상태 유지와 재인증 동작 회귀 test. 다른 계정 선택은 Microsoft 로그인 화면에 위임하고 사용되지 않는 Frontend 전용 request를 제거
- Frontend lint, typecheck, unit, build와 isolated Desktop browser 비교

## 5. 제외 범위

- 인증 정책, MSAL request/cache 의미, Backend, API, DB, migration과 runtime configuration 변경
- Development·Review-safe runtime 종료·재시작 또는 source 교체
- Persistent UAT 접근·write와 실제 provider 발송
- `TASK-GOV-HISTORY-REWRITE-001`, `TASK-GOV-FINDING-GATE-001` 파일 변경
- dependency/lockfile 변경
- Mobile/390px auth layout과 browser 검증
- Commit, push, PR와 merge

## 6. 변경 allowlist

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

## 7. Bounded worktree

- 목적: 기존 Development·Review-safe source tree와 governance WIP를 고정한 채 Frontend 디자인 구현을 격리한다.
- owner: `TASK-DESIGN-LOGIN-001` 구현 session
- 기준 SHA: `bce88188608557464ffcdfcd696d8fda86e98053`
- branch: `feat/task-design-login-001-entra-shell`
- 종료 시점: 자동 검증과 사용자 handoff 완료 시 cleanup 가능 여부를 다시 확인한다.
- cleanup 경계: commit reachable, worktree clean, process 미사용과 open PR 없음 확인 전 제거하지 않는다. Branch 자동 삭제는 하지 않는다.

Change 009 promotion worktree:

- 목적: 5174·5176 runtime과 실험본을 보존하면서 검수 완료 로그인 화면만 최신 main에 이식·검증한다.
- owner: `TASK-DESIGN-LOGIN-001` Change 009 promotion session
- 기준 SHA: `6f3eaf7a1eecd698f9fe9603e170452e70a64e8a`
- branch: `feat/task-design-login-001-login-promotion`
- worktree alias: `TASK_DESIGN_LOGIN_PROMOTION_WORKTREE`
- 종료 시점: 이식·전체 Frontend 검증·독립 검증과 사용자 게시 승인 handoff 완료 시 cleanup 가능 여부를 다시 확인한다.
- cleanup 경계: commit reachable, worktree clean, process 미사용과 open PR 없음 확인 뒤 별도 승인 범위에서만 제거한다. Branch 자동 삭제는 하지 않는다.

## 8. 사용자 검수 체크리스트

- [x] Desktop 1440×810에서 Figma의 red/white surface, 장식, EMI logo, Microsoft logo, title, `LOGIN`과 checkbox 위치 확인
- [x] 1920×1080, 1280×720, 1024×768, 1440×600과 좁은 PC 창에서 red/white panel이 화면 전체를 채우고 디자인 밖 빨간 여백이 보이지 않는지 확인
- [x] 창을 줄이거나 늘려도 내부 로고·장식·title·button·checkbox의 비율이 유지되고 모두 보이는지 확인
- [x] 기본 red surface가 `#DA2127`이고 white 10% glass layer가 적용되는지 확인
- [x] white shape의 둥근 왼쪽 모서리 밖으로 red surface가 이어지고 shadow가 red 영역으로 자연스럽게 퍼지는지 확인
- [x] `회사 Microsoft 365 계정으로 로그인해 주세요.` 안내가 Microsoft logo와 `LOGIN` 사이에 보이는지 확인
- [x] 왼쪽 상단 dot pattern이 Ellipse 68 원형 경계 안에서 Figma와 같은 위치로 보이는지 확인
- [x] 승인 안내 외 Figma에 없는 helper·secondary action·`다른 계정으로 로그인` action이 보이지 않는지 확인
- [x] Microsoft 로그인 화면에서 provider의 `다른 계정 사용` 경로를 이용할 수 있고 우리 화면에는 중복 action이 없는지 확인
- [x] `LOGIN`이 기존 Microsoft 365 기본 로그인 request를 사용하는지 확인
- [x] 로그인 상태 유지 checkbox가 선택·해제되고 기존 preference callback을 유지하는지 확인
- [x] checkbox 미선택 시 white·`#737373`, 선택 시 `#DA2127`·white Done icon·`#282828` 문구로 바뀌는지 확인
- [x] 선택 상태의 white Done icon이 checkbox 수평·수직 중앙에 보이는지 확인
- [x] Loading에서 일반 로그인 화면의 logo·title·Microsoft logo·안내·배경 geometry가 유지되는지 확인
- [x] Loading 중 `LOGIN`과 checkbox가 보이지 않고, 기존 control 영역 중앙에 빨간 원형 indicator 1개가 회전하는지 확인
- [x] 재인증·오류·설정 누락 fail-safe가 빈 화면 없이 기존 복구 의미를 유지하는지 확인
- [x] 자동 검증과 privacy-safe browser 결과 확인
- [x] 분리된 Codex read-only 검증 결과 확인
- [x] 게시 경계(Commit·Push·PR·Merge)를 별도로 결정

## 9. Findings

- P0: 0
- P1: 0
- 신규 P2: 0
- P3: 0
- 기존 history P2: 이 Task 범위 밖이며 상태와 파일을 변경하지 않는다.

## 10. 자동 검증 projection

- Frontend lint: error 0, 기존 Fast Refresh warning 1
- Frontend typecheck: PASS
- Frontend unit: 66/66 PASS, 인증 대상 test 15/15 PASS
- Frontend build: PASS, 기존 chunk-size warning 유지
- PC Desktop browser: 기본 로그인 6/6 + loading 6/6 = 12/12 PASS — 1920×1080, 1440×810, 1280×720, 1024×768, 1440×600, 651×708
- 1440×810 → 1024×768 live resize: PASS
- Mock UI smoke: 1/1 PASS
- Normalized geometry: 기본 title/EMI logo/Microsoft logo/LOGIN/checkbox/Ellipse 68와 Loading indicator 고정 좌표 일치
- Responsive panel coverage: 모든 viewport에서 red/white panel viewport 100% coverage, inner canvases fully visible
- 승인된 login guidance: count 1, exact text·normalized geometry 일치
- Loading guidance: count 1, `Microsoft 365 로그인 정보를 확인하고 있습니다.` exact text·normalized geometry 일치
- 기본 로그인은 button·checkbox를 유지하고 Loading은 button 0·checkbox 0·빨간 회전 indicator 1, `aria-busy=true`
- 로그인 상태 유지: 기본/Variant 2 style 일치, 6개 PC viewport 클릭 후 Done icon `50% 50%` 중앙 정렬과 preference `true` 전환
- 그 외 Figma 비포함 login content: 0
- Figma background/connection contract: base `#DA2127`, glass white 10%, white shape/radius/shadow 6/6 일치
- PC matrix horizontal/vertical overflow: 모두 0px
- Browser console error/request failure: 0/0
- Screenshot pixel projection: 안내 문구 포함 MAE 1.2497, exact pixel 69.3100%, channel당 차이 8 이하 97.7912%. Figma에 없는 승인 안내 영역 제외 시 MAE 1.1303, channel당 차이 8 이하 97.9200%
- Development·Review-safe runtime identity: 변경 없음
- Backend·API·DB·migration·runtime configuration: 변경 0

상세 구현·검증·미실행 항목·rollback은 [Implementation report](design-login-001-implementation-report.md)에 기록한다.
