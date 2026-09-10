# DESIGN-LOGIN-001 Change 012 — 인증·승인 대기 공통 화면과 자동 로그인

- 상태: 구현·개발 검증·독립 검토 완료. 디자인 사용자 확정, 동작 사용자 검수·원격 반영·배포 미실행.
- 승인: 사용자가 PC/모바일 시안 및 ‘인증이 필요합니다.’/‘로그인’, 일반 승인 대기 ‘관리자 승인을 기다리고 있습니다.’/‘로그아웃’을 확정하고 화면 적용과 추가 로그인 클릭 문제 수정을 요청했다.
- 기준선: origin/main `2a1e8df`, branch `codex/login-gate-alignment`, `/private/tmp/emi-login-gate-alignment`. 기존 다른 작업의 WIP와 runtime은 보존한다.
- 범위: 기존 로그인 배경·로고·배치의 실제 공용 구성 재사용. 일반 Entra 승인 대기 화면은 동일 공용 화면 사용. 관리자의 사업부 복구/관리 경로 및 개발자 전환은 기존대로 유지한다.
- 원인: Easy Auth 사전 인증 뒤에도 SPA의 MSAL cache가 없으면 앱이 수동 LOGIN 화면에서 멈췄다. MSAL 초기화 완료 후 계정 없음/interaction-required에서 redirect를 한 번 자동 시작한다. 기존 scope·목적지·감사 correlation을 유지한다.
- 보호: 탭별 redirect 시도 표식을 요청 전에 기록하고 성공 토큰 확보 뒤에 해제한다. 실패·취소·provider 복귀로 반복하지 않는다. 명시 로그아웃은 자동 복원을 억제하고 다음 수동 로그인으로 해제한다. 복수 계정과 일반 오류는 수동 복구. 저장소 사용 불가 시 자동 이동하지 않는다. Easy Auth·Backend bearer·권한·DB 및 승인 정책은 변경하지 않는다.
- 완료 조건: 확정 시안과 PC·390px 등 좁은 화면 상태 일치, 정상 자동 연결/실패/중복/로그아웃 반례 테스트, 기존 승인 사용자 진입 및 일반 미승인 업무 접근 차단, lint·typecheck·build, 독립 auth 검토.
- 시안 전용 `frontend/design-preview`는 로컬 확인용이며 제품 build/commit에 포함하지 않는다. 실제 기능 검증은 합성 provider·API를 사용한다. 실제 Microsoft 계정/운영 검증은 미실행 상태로 구분한다.


## 결과와 검증 (2026-09-11)

- 공용 `AuthStatusScreen`으로 확정 시안 적용. 자동 인증 중 기존 로딩, 인증 필요 ‘인증이 필요합니다.’/‘로그인’, 일반 승인 대기 ‘관리자 승인을 기다리고 있습니다.’/‘로그아웃’. 실패·계정 선택 필요는 짧은 안내로 구분한다.
- `vitest run tests/auth.test.tsx tests/app.test.tsx`: **125 PASS**. StrictMode 자동 redirect1회, 시도/로그아웃 표식 후 복귀, 수동 재시도, 오류, 복수 계정, 저장소 장애, 정상 계정 복원, selected/no_membership 승인 대기와 업무 조회 미발생·로그아웃 검증. jsdom의 기존 document navigation 미지원 출력이 있으나 실패 없음.
- `playwright test --config playwright.auth-shell.config.ts status.spec.ts` (AUTH_SHELL_PORT=5294): **28 PASS**. loading/reauth/pending/error를 1920×1080, 1440×810, 1280×720, 1024×768, 1440×600, 390×844, 412×915에서 검증. 문구/버튼/키보드 focus/겹침/가로 overflow/pageerror 확인. `frontend/test-results/status-*`에 합성 PNG 생성, parent가 desktop pending 및 mobile reauth/error를 직접 열어 시안 일치 확인. 처음 선택한 5194는 사용 중이라 해당 runtime을 변경하지 않고 빈5294 사용.
- frontend lint **오류0**, 기존 `main.tsx` Fast Refresh warning1. frontend build(TypeScript 포함) **PASS**, 기존 큰 bundle warning 유지. 새 테스트의 미지원 `exact` 타입 옵션1건은 제거 후 build PASS. 제품 코드 변경 없이 테스트 타입만 보정.
- 독립 검토 `/root/login_flow_review`: GPT-6-astra/high 요청, 실제 모델 metadata **NOT_REPORTED**. 이전 읽기 전용 설계 검토자에게 전체 누적 WIP 재검토 요청(구현 참여 없음). 계약·구현 품질 **GO**, 차단 Finding 없음. 실제 Backend pending 응답/관리자 복구 경계 및 desktop1440 pending/iPhone390 error PNG 대조. 제품 hash prefix App.tsx `d1415281`, auth.ts `8dc44d00`, auth-figma.css `5442c393`.
- 서버 권한·DB·provider 변경 없음. 기존 bearer와 /api/me 권한 분기를 그대로 사용하며 실제 Microsoft 왕복 인증은 미실행. 사용자 동작 검수·원격 push/PR/merge·공개배포는 미실행. 실제 계정 자동 복귀 및 운영 화면 확인을 완료로 표현하지 않는다.
- 로컬 시안 서버5193은 동일 작업공간의 실제 공용 화면을 렌더링하도록 갱신했고, 기존 시안 경로는 유지한다. 제품 배포 번들에는 시안/테스트 entry가 포함되지 않는다.
