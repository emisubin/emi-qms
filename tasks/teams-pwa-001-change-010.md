# TASK-TEAMS-PWA-001 Change 010 — 인증된 Android PWA 매니페스트

- taskType: `BUGFIX`
- approvalSource: `USER_EXPLICIT_ANDROID_INSTALL_BUTTON_FIX`
- approvalDate: `2026-08-10`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- purposeIdentity: `Azure Easy Auth로 보호된 운영 Android Chrome에서 PWA 매니페스트를 인증 상태로 읽고 설치 가능 event를 받아 EMI PMS 설치 버튼을 활성화한다.`
- branch: `fix/task-teams-pwa-001-android-install-manifest`
- baseSha: `3c0db4779dac3a3c2bc3599369065b3886e6ab21`
- instructionChainRead: `true`
- taskIdentityGate: `PASS_REUSE`
- taskTypeSelected: `BUGFIX`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- implementationApproved: `true`
- publicationApproved: `false`

## 확인된 증상과 원인

1. iPhone 안내는 브라우저 설치 event와 무관한 자체 절차이므로 운영에서 정상 표시됐다.
2. Android `EMI PMS 설치` 버튼은 Chrome의 `beforeinstallprompt` event가 들어오기 전까지 비활성화된다.
3. 운영 `manifest.webmanifest`는 Azure Easy Auth로 보호돼 익명 요청에 `401`을 반환하지만, HTML의 manifest link에 credential 포함 지시가 없었다.
4. 인증이 필요한 Web App Manifest는 같은 origin이어도 `crossorigin="use-credentials"`로 연결해야 한다. Chrome이 manifest를 설치 가능 대상으로 처리하지 못하면 `beforeinstallprompt`가 발생하지 않아 버튼이 계속 비활성화된다.
5. 기존 자동 검증은 합성 `beforeinstallprompt` event만 주입해 UI 상태 전환을 검사했으므로 실제 인증된 manifest 연결 누락을 발견하지 못했다.

## 승인된 수정 범위

- `frontend/index.html`의 manifest link에 `crossorigin="use-credentials"`를 추가한다.
- PWA asset contract가 credential 포함 manifest link를 필수로 검사하게 한다.
- Frontend lint·typecheck·전체 unit·production build와 PWA asset 검사를 수행한다.
- 자동 안내 팝업의 session 정책, UI 문구·디자인, iPhone 절차와 Android 버튼 제어 로직은 변경하지 않는다.
- Backend·DB·migration·Azure Easy Auth 정책·Teams package·알림 기능은 변경하지 않는다.

## 변경 allowlist

- `frontend/index.html`
- `scripts/test-pwa-assets.sh`
- `tasks/teams-pwa-001-change-010.md`
- `tasks/teams-pwa-001-implementation-report.md`
- `tasks/teams-pwa-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 검증·게시 Gate

- PWA asset contract에서 credential 포함 manifest link 확인
- PWA 집중 unit `10/10`
- Frontend lint error 0, typecheck, 전체 unit, production build
- Git diff allowlist·`git diff --check`
- 게시 승인 뒤 PR/main CI와 Azure release
- 운영 Android Chrome에서 새 탭 Microsoft 인증 후 설치 버튼 활성화·native 설치 확인창 사용자 검수

## 보존할 불변조건

- root app shell·업무 bundle·PWA manifest·API의 Azure Easy Auth 사전 인증
- 기존 MSAL 로그인, Backend bearer, 역할·프로젝트 접근 권한
- 현재 탭 session 단위 자동 안내 닫기와 standalone·Teams embedded 숨김
- iPhone Safari·타 브라우저 설치 안내
- Teams Activity 10종·수신자·발송 시점·deep link
- Service Worker·offline cache·Web Push 보류 정책
