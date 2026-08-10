# TASK-DESIGN-LOGIN-001 Change 010 — 모바일 로그인과 지정 로그인 로고

## 1. 승인과 Task Identity Gate

- 승인 source: 2026-08-10 사용자 요청 — iPhone·Android 로그인 화면 수정, 로그인 화면 `Asset 3@4x.png` 적용, 흑백 wireframe 유지, 구현·메인 반영·공개배포 승인
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-TEAMS-PWA-001`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-DESIGN-LOGIN-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

Purpose identity:

- 업무 목표: iPhone·Android에서 축소된 PC 로그인 화면 대신 모바일 전용 로그인 구성을 제공하고, 로그인 화면 로고를 사용자가 지정한 원본으로 교체한다.
- Root Finding: 기존 Change 001이 Mobile을 명시적으로 제외해 860px 이하에서도 1440×810 PC canvas가 축소되며 로그인 요소가 지나치게 작게 표시된다.
- 변경 경계: 로그인·Loading의 모바일 배치, 지정 로그인 로고 원본, auth unit/browser test와 Task 문서만 변경한다.
- 보존할 불변조건: Desktop 로그인 geometry, Microsoft 365 redirect·MFA·조건부 접근, 로그인 상태 유지, PWA 설치 안내, Backend·API·DB·migration을 변경하지 않는다.
- 예상 산출물: iPhone·Android 모바일 로그인·Loading 화면, 원본 로그인 로고, desktop/mobile 회귀 검증과 공개배포 가능한 commit.

## 2. 포함 범위

- `Asset 3@4x.png`의 byte를 변경하지 않고 로그인 로고 asset으로 사용한다.
- 860px 이하에서 흰 배경, 검정 선·버튼, 중성 회색 문구의 wireframe login card를 사용한다.
- 모바일에서는 장식용 red panel·ellipse·dot pattern을 숨기고 원본 logo의 red 색상만 유지한다.
- iPhone 390×844와 Android 412×915에서 login·loading, 터치 크기, safe-area, 세로 scroll과 가로 overflow를 검증한다.
- Desktop 1024px 이상에서 기존 Figma panel·geometry test를 그대로 유지한다.

## 3. 제외 범위

- 로그인 후 내부 페이지 로고 교체는 `TASK-TEAMS-PWA-001`의 별도 후속 Change로 처리한다.
- 인증 정책·MSAL request/cache·Azure Easy Auth·Backend·API·DB·migration·dependency를 변경하지 않는다.
- Teams manifest·catalog와 PWA icon file은 변경하지 않는다.

## 4. Bounded worktree

- 목적: 기존 canonical clone과 Teams manifest WIP를 보존하면서 최신 원격 main에서 모바일 로그인만 격리 구현한다.
- owner: `TASK-DESIGN-LOGIN-001 Change 010` 구현 session
- 기준 SHA: `6b76507020537f60e2d3d219f17b9d58d8e7d74f`
- branch: `fix/task-design-login-001-mobile-logo`
- worktree: `/private/tmp/emi-qms-mobile-login.cifoIK`
- 종료 시점: 자동·브라우저 검증, Git 게시와 main merge 완료 후 cleanup 가능 여부를 확인한다.

## 5. 완료 기준

- 지정 logo SHA-256가 source와 tracked asset에서 일치한다.
- Desktop auth browser matrix와 iPhone·Android login/loading browser 검증을 통과한다.
- 모바일에서 logo filter가 `none`이고 page horizontal overflow가 0이다.
- Frontend lint, typecheck, 전체 unit, production build와 `git diff --check`를 통과한다.
- Open P0/P1/P2가 0인 상태에서 게시·배포 단계로 진행한다.
