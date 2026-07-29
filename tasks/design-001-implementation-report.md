# DESIGN-001 실험 구현 보고서

## 작업 상태

- Task: `DESIGN-001 이후 화면 통일`의 실험 구현
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- branch: `experiment/task-007a-pending-list`
- base SHA: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`
- currentStage: 구현 및 자동 검증 완료, 사용자 화면 검수 대기
- Git 게시 상태: 현재 실험 branch의 local checkpoint Commit 승인됨 · Push 미완료 · PR 미완료 · Merge 미완료
- Main merge approval: `0/3`

사용자는 실험 worktree에서 Roadmap 순서를 벗어난 디자인 구현을 바로 진행하고, 로그인 화면을 기준으로 모든 업무 화면을 통일한 뒤 페이지별 스크린샷을 보고하도록 명시했다. 이 변경은 새로운 업무 능력이나 상태 전이를 추가하지 않는 시각 구현이며, 대표 repository와 GitHub `main`에는 반영하지 않는다.

## 목적과 불변조건

### 목적

- 기존 로그인 화면의 EMI red, white surface, dotted texture, rounded geometry와 여백 체계를 업무 화면 전체에 확장한다.
- Desktop sidebar와 mobile navigation이 같은 브랜드 언어를 사용하도록 한다.
- 프로젝트, Pending, 생산, 구매, 자재, 알림, 관리자와 Teams 화면의 기존 기능·권한·데이터 계약은 유지한다.

### 불변조건

- API, DB schema, 권한, workflow와 업무 동작을 디자인 변경 때문에 수정하지 않는다.
- 로그인 화면의 기존 레이아웃과 인증 흐름은 변경하지 않는다.
- 대표 repository, local `main`, `origin/main`을 변경하지 않는다.
- local experiment commit은 2026-07-16 사용자 요청으로 승인되었다. Push, PR, merge는 별도 승인 전 수행하지 않는다.
- `main` merge는 사용자의 명시적 승인 3회 이상 전에는 수행하지 않는다.

## 구현 내용

### 공통 시각 토큰

- EMI red 계열, canvas/surface/line/ink/muted, shadow와 radius 토큰을 공통 CSS 변수로 정리했다.
- 버튼, 입력창, select, textarea, focus ring과 상태 badge를 동일한 control 언어로 맞췄다.
- content card, KPI card, table head/row, dialog와 filter surface를 white/red 기반으로 통일했다.

### App shell

- Desktop sidebar에 EMI 로고, `PROJECT OPERATIONS`, `WORKSPACE` hierarchy를 추가했다.
- Sidebar에 로그인 화면의 red gradient와 dotted texture를 확장했다.
- Topbar를 white rounded surface, red rail, soft radial accent로 변경했다.
- 상단 제품명을 `EMI 프로젝트 통합관리시스템`으로 통일했다.

### Responsive

- 860px 이하에서 sidebar를 compact red navigation grid로 전환한다.
- status strip, page surface, cards, forms와 tables의 기존 mobile alternative를 동일한 토큰으로 통일했다.
- 390×844 평가 viewport에서 검수한 화면은 horizontal overflow가 없었다.
- `prefers-reduced-motion`에서는 shell button transition을 제거한다.

## 변경 파일

- `frontend/src/App.tsx`: shell brand lockup과 topbar 제품명
- `frontend/src/styles.css`: 공통 디자인 토큰, shell, component, page, responsive theme
- `tasks/design-001-screenshots/*.jpg`: 개인정보 없는 격리 DB의 synthetic data로 생성한 검수 화면 42장
- `tasks/design-001-implementation-report.md`: 본 구현·검증 원장

현재 worktree에는 이 디자인 변경 전에 구현된 `TASK-007A` Pending 기능 변경도 함께 존재한다. 본 보고서는 그 기존 기능 변경을 DESIGN-001의 변경으로 재분류하지 않는다.

## 화면 증빙

모든 캡처는 provider가 비활성화된 전용 임시 DB와 current worktree runtime에서 생성했다. 실제 사용자·고객·프로젝트 정보는 포함하지 않았다.

### 로그인 기준

- [00 로그인 기준 Desktop](design-001-screenshots/00-login-baseline-desktop.jpg)
- [40 로그인 Mobile 390](design-001-screenshots/40-login-mobile-390.jpg)

### 프로젝트와 패널

- [01 프로젝트 목록 Desktop](design-001-screenshots/01-project-list-desktop.jpg)
- [02 프로젝트 등록 Desktop](design-001-screenshots/02-project-create-desktop.jpg)
- [03 프로젝트 상세 Desktop](design-001-screenshots/03-project-detail-desktop.jpg)
- [04 프로젝트 수정 Desktop](design-001-screenshots/04-project-edit-desktop.jpg)
- [28 패널 포함 프로젝트 상세 Desktop](design-001-screenshots/28-project-detail-with-panels-desktop.jpg)
- [29 패널 정보 수정 Desktop](design-001-screenshots/29-panel-information-edit-desktop.jpg)
- [30 패널 상세 Desktop](design-001-screenshots/30-panel-detail-desktop.jpg)
- [31 패널 상세 Mobile 390](design-001-screenshots/31-panel-detail-mobile-390.jpg)
- [32 프로젝트 목록 Mobile 390](design-001-screenshots/32-project-list-mobile-390.jpg)

### 내 업무와 Pending

- [05 내 업무 Desktop](design-001-screenshots/05-my-work-desktop.jpg)
- [06 Pending 등록 Desktop](design-001-screenshots/06-pending-create-desktop.jpg)
- [07 Pending 목록 Desktop](design-001-screenshots/07-pending-list-desktop.jpg)
- [08 Pending 상세 Desktop](design-001-screenshots/08-pending-detail-desktop.jpg)
- [33 내 업무 Mobile 390](design-001-screenshots/33-my-work-mobile-390.jpg)
- [34 Pending 목록 Mobile 390](design-001-screenshots/34-pending-list-mobile-390.jpg)

### 생산·구매·자재

- [09 생산계획 Dashboard Desktop](design-001-screenshots/09-production-dashboard-desktop.jpg)
- [10 구매 Dashboard Desktop](design-001-screenshots/10-procurement-dashboard-desktop.jpg)
- [12 생산계획 설정 Desktop](design-001-screenshots/12-production-settings-desktop.jpg)
- [13 생산계획 수정 Desktop](design-001-screenshots/13-production-edit-desktop.jpg)
- [14 구매 담당자 화면 Desktop](design-001-screenshots/14-procurement-manager-desktop.jpg)
- [15 구매 설정 Desktop](design-001-screenshots/15-procurement-settings-desktop.jpg)
- [16 구매 수정 Desktop](design-001-screenshots/16-procurement-edit-desktop.jpg)
- [17 자재 입고 Desktop](design-001-screenshots/17-materials-desktop.jpg)
- [35 생산계획 Mobile 390](design-001-screenshots/35-production-dashboard-mobile-390.jpg)
- [36 구매 Mobile 390](design-001-screenshots/36-procurement-dashboard-mobile-390.jpg)
- [38 자재 Mobile 390](design-001-screenshots/38-materials-mobile-390.jpg)

### 알림·Teams

- [11 알림 Desktop](design-001-screenshots/11-notifications-desktop.jpg)
- [37 알림 Mobile 390](design-001-screenshots/37-notifications-mobile-390.jpg)
- [41 Teams Activity Desktop](design-001-screenshots/41-teams-activity-desktop.jpg)

### 관리자

- [18 관리자 Dashboard Desktop](design-001-screenshots/18-admin-dashboard-desktop.jpg)
- [19 사용자 관리 Desktop](design-001-screenshots/19-admin-users-desktop.jpg)
- [20 부서 관리 Desktop](design-001-screenshots/20-admin-departments-desktop.jpg)
- [21 공휴일 관리 Desktop](design-001-screenshots/21-admin-calendar-desktop.jpg)
- [22 권한 매트릭스 Desktop](design-001-screenshots/22-admin-permissions-desktop.jpg)
- [23 기준정보 변경 이력 Desktop](design-001-screenshots/23-admin-change-logs-desktop.jpg)
- [24 업무 시작·완료 이력 Desktop](design-001-screenshots/24-admin-work-history-desktop.jpg)
- [25 알림 수동 발송 Desktop](design-001-screenshots/25-admin-send-notification-desktop.jpg)
- [26 알림 발송 상태 Desktop](design-001-screenshots/26-admin-deliveries-desktop.jpg)
- [27 에스컬레이션 상태 Desktop](design-001-screenshots/27-admin-escalations-desktop.jpg)
- [39 관리자 Dashboard Mobile 390](design-001-screenshots/39-admin-dashboard-mobile-390.jpg)

## 검증

| 검증 | 결과 |
| --- | --- |
| `pnpm typecheck` | PASS |
| `pnpm lint` | PASS · error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| `pnpm test -- --run` | PASS · 2 files, 66 tests |
| `pnpm build` | PASS · 기존 500 kB 초과 chunk warning 유지 |
| Browser console error | PASS · 로그인 0건, app 0건 |
| Desktop visual | PASS · 1440×1000 계열 캡처 |
| Mobile visual | PASS · 390×844 평가 viewport, 가로 overflow 없음 |
| Screenshot format | PASS · JPEG 42개 |

## 미실행·제약

- 삭제된 프로젝트 상세, 알림 delivery 상세와 Teams notification 상세는 전용 임시 DB에 해당 record가 없어 별도 record를 만들지 않았다. 공통 shell, surface, card, table와 responsive 토큰은 동일하게 적용된다.
- 자동 검증 완료는 사용자 시각 검수 완료를 의미하지 않는다.
- local experiment checkpoint commit은 사용자 승인을 받아 본 변경과 함께 생성한다. Push, PR, merge는 실행하지 않는다.

## Rollback

DESIGN-001만 되돌릴 때는 commit 전 현재 diff에서 `frontend/src/App.tsx`의 brand lockup/topbar 변경, `frontend/src/styles.css`의 DESIGN-001 theme block, 본 보고서와 `tasks/design-001-screenshots/`만 제외한다. 기존 TASK-007A Pending 구현은 별도 변경이므로 함께 제거하지 않는다.

## Finding

- `DESIGN001-F01` · P3 · OPEN: production bundle main chunk가 500 kB를 초과한다. 본 디자인 변경 전부터 존재한 구조적 경고이며 기능·시각 검수를 차단하지 않는다. 후속 bundle splitting Task에서 다룬다.
- `DESIGN001-F02` · P3 · OPEN: `frontend/src/main.tsx`에 Fast Refresh warning 1건이 있다. error는 아니며 본 변경 범위 밖이다.
- `DESIGN001-F03` · P3 · OPEN: data-dependent detail route 3종은 screenshot record가 없다. 공통 theme는 적용됐지만 실제 데이터가 생긴 뒤 후속 화면 검수 후보로 남긴다.

## 게시 판정

- DESIGN-001 사용자 검수: 대기
- Commit: 사용자 승인 완료 · 현재 실험 branch checkpoint에 포함
- Push/PR: 사용자 별도 승인 대기
- Merge: `NO_GO` · 사용자 승인 `0/3`, 대표 repository와 GitHub main 미변경
