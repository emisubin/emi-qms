# TASK-DESIGN-LOGIN-001 Change 003 — viewport 전체를 채우는 PC 반응형 패널

## 1. 승인과 상태

- 승인 source: 2026-07-14 사용자 수정 요청
- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION` 유지
- 대상: Figma node `1:175`의 Desktop 로그인 화면
- 구현 승인: 사용자가 같은 요청에서 전체 canvas letterbox를 제거하고 일반적인 웹사이트의 PC 반응형 방식으로 수정하도록 명시 승인함
- Mobile/390px: 계속 제외
- Commit·Push·PR·Merge: 별도 승인 전 금지

## 2. 증상과 확인된 원인

- Change 002는 1440×810 canvas 전체를 `min(viewportWidth / 1440, viewportHeight / 810)`으로 축소하고 root의 빨간 배경 위에 가운데 배치했다.
- 16:9가 아닌 창에서는 canvas 밖의 위·아래 또는 좌·우 영역이 root의 빨간색으로 노출됐다.
- 이 영역은 Figma의 red/white 두 패널 구조에 속하지 않으므로 사용자는 디자인에 없는 빨간 여백으로 인식했다.
- MDN과 web.dev의 responsive guidance를 대조한 결과, 고정 페이지 전체를 letterbox 처리하기보다 flexible Grid/Flex container가 viewport를 채우고 각 content가 container 안에서 적응하는 방식이 일반적인 기준이다.

## 3. 포함 범위와 구현 계약

- 로그인 root는 viewport 전체를 채운다.
- Figma의 visible divider `x=776/1440`을 기준으로 red brand panel `53.8888889%`, white authentication panel `46.1111111%`의 flexible grid를 사용한다.
- 두 panel은 viewport의 전체 높이와 전체 너비를 빈틈없이 덮는다. 따라서 16:9가 아닌 창에서도 디자인 밖 letterbox 색상을 만들지 않는다.
- 각 panel 내부에는 Figma 기준 좌표용 reference canvas를 둔다.
  - Brand content canvas: `776×810`
  - Authentication content canvas: `664×810`
- 내부 canvas는 공통 `min(viewportWidth / 1440, viewportHeight / 810)` 배율로 panel 중앙에 배치한다.
- panel surface는 남는 공간까지 각각 Figma의 red/white로 연장하고, 로고·장식·title·button·checkbox는 비율과 reference 좌표를 유지한다.
- white panel의 radius와 shadow도 공통 배율로 조정한다.
- Figma 비포함 login content 0 계약과 Ellipse 68의 위치·pattern 계약을 유지한다.

## 4. 검증 계약

- 1920×1080, 1440×810, 1280×720, 1024×768에서 기존 Desktop geometry를 검증한다.
- 높이가 짧은 1440×600 PC 창과 현재 검수 창에 대응하는 651×708 좁은 PC 창을 추가 검증한다.
- 모든 viewport에서 다음을 fixed projection으로 확인한다.
  - red/white panels가 viewport를 100% 덮음
  - reference content canvas가 각 panel 안에 완전히 보임
  - title, EMI logo, Microsoft logo, `LOGIN`, checkbox, Ellipse 68 normalized geometry 일치
  - Figma 비포함 content 0
  - horizontal/vertical overflow 0
  - console error/request failure 0
- Frontend lint, typecheck, unit, build와 mock UI smoke를 재실행한다.

## 5. 제외 범위와 불변조건

- Mobile/390px layout과 검증은 계속 제외한다.
- Microsoft 365 로그인, 로그인 상태 유지, silent token·재인증과 다른 계정 request 정의를 변경하지 않는다.
- Backend, API, DB, migration, dependency, lockfile와 runtime configuration을 변경하지 않는다.
- 기존 Development·Review-safe runtime을 종료·재시작·교체하지 않는다.
- 사용자 검수와 독립 Codex 검증은 완료로 추정하지 않는다.
