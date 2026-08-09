# TASK-TEAMS-PWA-001 Change 003 — iPhone 타 브라우저 설치 복구 안내

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- planningApproved: `true`
- implementationApproved: `true`
- approvalSource: `USER_EXPLICIT_RECOMMENDED_OPTION`
- approvalDate: `2026-08-09`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- branch: `feat/task-teams-pwa-001-experience`
- baseSha: `914a109e170f4e1c3ce34fb1faa4216c1b4fcf1c`
- roadmapSequenceMatch: `true`

## 사용자 문제와 공식 기준

- 현재 구현은 iPhone 여부만 판정하고 Chrome·Edge 등으로 접속한 사용자에게도 Safari 화면 기준 절차를 바로 표시한다.
- iOS·iPadOS 16.4 이상에서는 타 브라우저도 구현 여부에 따라 공유 메뉴에 `홈 화면에 추가`를 제공할 수 있으므로 무조건 Safari로 옮기면 불필요한 단계가 생긴다.
- 웹 페이지가 iOS에서 Safari를 안정적으로 강제 실행하는 표준 방식은 없으므로 주소 복사와 명시적 수동 이동이 안전한 fallback이다.

## 구현 계약

1. iPhone Safari와 iPhone 타 브라우저를 사용자 agent 단서로 구분한다.
2. Safari에는 `공유 → 홈 화면에 추가 → 웹 앱으로 열기 → 추가` 절차를 표시한다.
3. 타 브라우저에서는 먼저 현재 브라우저 공유 메뉴의 `홈 화면에 추가`를 확인하도록 안내한다.
4. 메뉴가 없을 때 사용할 `PMS 주소 복사` 버튼과 `Safari 열기 → 붙여넣기 → 설치` 절차를 제공한다.
5. 복사 주소는 현재 업무 deep link나 query가 아닌 동일 origin의 EMI PMS root로 제한한다.
6. Clipboard 실패 시 사용자에게 주소창에서 직접 복사하도록 오류 안내를 제공한다.
7. Change 002의 Graphite 흑백 wireframe, 설치 event·dismissal·standalone·embedded 조건과 권한·인증 계약을 유지한다.

## 제외 범위

- 비표준 Safari custom URL scheme과 Safari 강제 실행
- Android·PC 설치 정책 변경
- Service Worker·Web Push·DB·Backend·Azure·Teams provider 변경

## 검증 계약

- iPhone Safari 안내와 iPhone 타 브라우저 안내 분리 unit test
- root 주소 복사 성공·실패 feedback unit/browser test
- Safari·타 브라우저 iPhone 390px와 Android 390px 가로 overflow 0
- Frontend lint·typecheck·unit·build·mock UI 전체 통과
