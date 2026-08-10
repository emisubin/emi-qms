# TASK-TEAMS-PWA-001 Change 009 — 인증 후 모바일 PWA 설치 안내

- taskType: `BUGFIX`
- approvalSource: `USER_EXPLICIT_RECOMMENDED_FIX_AND_PUBLIC_DEPLOY`
- approvalDate: `2026-08-10`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- purposeIdentity: `모바일 브라우저 사용자가 Microsoft 365 인증을 마친 직후 PWA 설치 안내를 먼저 받고 Android에서는 지원 시 설치 확인창을 한 번의 버튼으로 열 수 있게 한다.`
- branch: `fix/task-teams-pwa-001-mobile-install-gate`
- baseSha: `22524ff2422128e54090af84ccde481c7ef4b47d`
- instructionChainRead: `true`
- taskIdentityGate: `PASS_REUSE`
- taskTypeSelected: `BUGFIX`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- implementationApproved: `true`
- publicDeploymentApproved: `true`

## 확인된 증상과 원인

1. 운영 Azure Easy Auth는 익명 방문자를 Microsoft 로그인으로 먼저 보낸 뒤 React 앱을 시작한다. 이 보안 동작은 정상이다.
2. 기존 PWA 자동 안내는 앱 인증 완료 상태를 직접 확인하지 않았고, Android에서는 브라우저의 `beforeinstallprompt` event가 먼저 도착해야만 안내가 열렸다. 따라서 로그인 직후 홈은 보이지만 설치 안내가 열리지 않을 수 있었다.
3. 설치 event listener도 MSAL 초기화 뒤에 생성되어 이른 event를 놓칠 여지가 있었다.
4. `나중에` 선택은 `localStorage`에 영구 저장돼 사용자가 설치하지 않았어도 다음 방문부터 자동 안내가 계속 숨겨졌다.

## 승인된 수정 범위

- PWA install provider를 MSAL 초기화 화면보다 위에 배치해 브라우저 설치 event를 가능한 이른 시점부터 수신한다.
- Microsoft 인증과 앱 access-token gate가 끝나 실제 업무 shell이 준비된 뒤에만 모바일 자동 안내를 연다.
- standalone/PWA 실행과 Teams embedded 표면에서는 기존처럼 자동 안내를 숨긴다.
- iPhone Safari·타 브라우저 안내 문구와 설치 절차는 변경하지 않는다.
- Android는 인증 완료 뒤 설치 event 유무와 관계없이 안내를 먼저 표시한다.
- Android `EMI PMS 설치` 버튼은 항상 표시하되 브라우저가 아직 설치 prompt를 준비하지 않았으면 비활성화하고 수동 메뉴 복구 안내를 함께 표시한다. event가 도착하면 같은 버튼을 즉시 활성화한다.
- 자동 안내 닫기 기억은 영구 저장이 아닌 현재 탭 session으로 제한한다. 같은 탭의 새로고침에서는 반복하지 않고 새 브라우저 session에서는 미설치 상태라면 다시 안내한다.
- 관련 unit·mobile browser 회귀를 갱신한다.

## 브라우저 제약과 사용자 약속

Android Chrome의 실제 앱 설치는 브라우저가 설치 가능성을 판정하고 native 확인창을 제공한 뒤 사용자가 최종 확인해야 한다. 웹사이트가 사용자 확인 없이 앱을 강제 설치할 수는 없다. EMI PMS는 인증 직후 안내를 먼저 열고, 설치 event가 준비되면 한 번의 `EMI PMS 설치` 버튼으로 Chrome 확인창을 연다.

## 보존할 불변조건

- Azure Easy Auth의 익명 app shell·bundle·API 차단
- 기존 MSAL 로그인, Backend bearer, 역할과 프로젝트 접근 권한
- Teams launcher·Activity type 10개·수신자·발송 시점·deep link
- PWA manifest·icon과 승인된 흑백 wireframe 디자인
- Service Worker·offline cache·Web Push 보류 정책
- Backend·DB·migration·업무 workflow 무변경

## 변경 allowlist

- `frontend/src/App.tsx`
- `frontend/src/main.tsx`
- `frontend/src/PwaInstallExperience.tsx`
- `frontend/src/pwa-install.ts`
- `frontend/src/styles.css`
- `frontend/tests/pwa-install.test.tsx`
- `frontend/tests/auth.test.tsx`
- `frontend/e2e/mock-ui/teams-pwa-experience.spec.ts`
- `frontend/e2e/auth-shell/auth-shell.spec.ts`
- `tasks/teams-pwa-001-change-009.md`
- `tasks/teams-pwa-001-implementation-report.md`
- `tasks/teams-pwa-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 검증·게시 Gate

- Frontend lint·typecheck·전체 unit·production build
- 인증 준비 전 자동 안내 없음, 인증 준비 뒤 모바일 자동 안내
- Android 설치 event 준비 전 비활성 버튼과 준비 뒤 활성 버튼
- iPhone Safari·타 브라우저 기존 절차, session 단위 닫기, standalone 숨김
- 390px Android·iPhone browser screenshot과 horizontal overflow 0
- explicit allowlist diff·privacy-safe evidence·open P0/P1/P2 0
- PR CI와 merge SHA main CI
- 승인형 Azure release로 exact main SHA를 배포한 뒤 health `200`, 익명 root·`/api/me` `401/401`
