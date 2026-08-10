# TASK-TEAMS-PWA-001 Change 007 — 로그인 후 공통 화면 지정 로고 적용

- taskType: `BUGFIX`
- planningApproved: `true`
- implementationApproved: `true`
- approvalSource: `USER_EXPLICIT_LOGO_AND_PUBLIC_DEPLOY`
- approvalDate: `2026-08-10`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- branch: `fix/task-teams-pwa-001-internal-logo`
- baseSha: `4529886f1c8d03eeefa80959887d5fd56c3a968f`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- samePurposeMatchCount: `1`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`

## Task Identity Gate

- 업무 목표: 로그인 뒤 모든 업무 페이지가 공유하는 데스크톱 sidebar, 모바일 app bar와 모바일 menu drawer에 사용자가 지정한 4x 내부 logo를 표시한다.
- root Finding: 공통 application shell이 로그인 화면용 가로형 로고를 함께 사용하고, Graphite wireframe의 전역 grayscale·black background 규칙이 지정 브랜드 원색을 제거한다.
- 변경 경계: 로고 binary를 별도 asset으로 추가하고 공통 shell 세 지점만 그 asset을 사용하며, 해당 로고에만 원색·투명 배경 예외를 둔다.
- 보존 불변조건: 로그인 화면은 Change 010의 지정 4x 가로 logo, 나머지 화면은 흑백 wireframe, Backend·DB·migration·Teams manifest·PWA icon·알림 계약은 모두 유지한다.
- 예상 산출물: Frontend source·CSS·desktop/mobile browser regression, Implementation report·Roadmap·사용자 검수 checklist 갱신.
- 동일 목적 검색: 기존 `TASK-TEAMS-PWA-001` 브랜드 통일 범위 한 건을 재사용한다. 동일 목적 branch와 open PR은 없다.
- Change 번호: 다른 작업 공간에 보존된 미게시 Change 004~006과의 ID 충돌을 피하기 위해 007을 사용한다. 그 WIP 내용은 이번 branch에 복사하거나 수정하지 않는다.

## 사용자 계약

1. 로그인 화면은 사용자가 지정한 4x 가로 logo를 그대로 사용한다.
2. 로그인 후 모든 페이지는 공통 shell의 세 surface에서 사용자가 지정한 4x 내부 logo를 사용한다.
3. 기존 흑백 wireframe layout, border, typography와 control 색은 바꾸지 않는다.
4. 지정 로고만 원본 빨간색·흰색 pixel을 유지하고 grayscale filter 또는 검정 배경을 적용하지 않는다.
5. 변경·검증 뒤 별도 PR을 원격 `main`에 병합하고 최신 main을 기존 승인형 Azure 공개 release로 배포한다.

## 변경 allowlist

- `frontend/src/assets/emi-logo-internal.png`
- `frontend/src/App.tsx`
- `frontend/src/design-system/wireframe.css`
- `frontend/e2e/mock-ui/panel-kitting-smoke.spec.ts`
- `frontend/e2e/mock-ui/project-registration-smoke.spec.ts`
- `tasks/teams-pwa-001-change-007.md`
- `tasks/teams-pwa-001-implementation-report.md`
- `tasks/teams-pwa-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 검증 계약

- 원본과 tracked asset의 byte equality·SHA-256 일치
- 로그인 화면 logo natural size `4265×604`, 내부 shell logo natural size `3796×1378`
- 내부 logo의 computed filter `none`, background transparent
- desktop sidebar, 390px mobile app bar와 menu drawer에서 동일 asset 사용
- 기존 로그인 desktop/mobile regression, Frontend lint·typecheck·unit·build, mock UI·CI
- page-level horizontal overflow 0, 정상 경로 console error와 non-aborted request failure 0
- 전체 mock UI에서 중복 가능한 empty-state action과 상단 action을 구분하는 안정적인 프로젝트 등록 test selector
- Backend·DB·migration·dependency·environment·Teams/PWA package 변경 0

## Finding Gate

- Open P0/P1/P2: `0/0/0`
- 사용자 실제 운영 PC·iPhone·Android 화면 확인은 공개 release 뒤 checklist로 유지하며 자동 검증 성공으로 대체하지 않는다.
