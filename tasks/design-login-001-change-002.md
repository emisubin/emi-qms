# TASK-DESIGN-LOGIN-001 Change 002 — Ellipse 68와 PC 등비 반응형

## 1. 승인과 상태

- 승인 source: 2026-07-14 사용자 수정 요청
- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION` 유지
- 대상: Figma node `1:175`의 Desktop 로그인 화면
- 구현 승인: 사용자가 같은 요청에서 Ellipse 68 위치 보정과 PC 반응형 실행을 명시 승인함
- Mobile: 계속 제외
- Commit·Push·PR·Merge: 별도 승인 전 금지

## 2. 확인된 원인

- Figma node `1:181` (`Ellipse 68`)은 `x=-538.5`, `y=-468`, `876×876`의 ellipse다.
- Figma Plugin API 직접 확인 결과 이 node는 visible pattern fill을 사용하며 paint opacity는 `0.33`, tile type은 `RECTANGULAR`, scaling factor는 `0.75`다.
- Figma asset API가 반환한 Ellipse 68 SVG에는 pattern fill이 포함되지 않아 빈 circle로 내려온다.
- 기존 Frontend는 빈 SVG와 별도로 `x=7`, `y=6`, `336×360`의 사각형 dot mask를 사용했다. 따라서 dot pattern의 origin은 맞았지만 Ellipse 68의 실제 원형 위치·경계·불투명도와 달랐다.
- 기존 로그인 canvas는 `min-width: 1440px`, `min-height: 810px`라 작은 PC 창에서 전체 화면이 잘렸다.

## 3. 포함 범위

- 기존 사각형 dot mask를 Figma Ellipse 68의 실제 `-538.5, -468, 876×876` 원형 layer로 이동한다.
- 24px dot origin을 Figma screenshot과 맞추고 pattern opacity 0.33을 적용한다.
- 빈 Ellipse 68 SVG img를 제거하고 CSS pattern layer가 node `1:181`을 표현하도록 한다.
- 1440×810 reference canvas의 내부 좌표와 비율은 그대로 유지한다.
- PC viewport에 대해 `min(viewportWidth / 1440, viewportHeight / 810)` scale을 적용해 전체 canvas를 가운데 등비 축소·확대한다.
- 1920×1080, 1440×810, 1280×720, 1024×768에서 전체 canvas visible, 비율 유지, overflow 0과 normalized geometry를 검증한다.

## 4. 제외 범위와 불변조건

- Mobile/390px layout과 검증은 계속 제외한다.
- 로그인 화면의 Figma-only content 계약을 유지한다.
- 기본 로그인, 로그인 상태 유지, silent token·재인증과 다른 계정 request 정의를 변경하지 않는다.
- Backend, API, DB, migration, dependency, lockfile와 runtime configuration을 변경하지 않는다.
- 기존 Development·Review-safe runtime을 종료·재시작·교체하지 않는다.

## 5. 완료 기준

- Figma Ellipse 68의 geometry와 pattern paint를 다시 확인한다.
- reference viewport 1440×810에서 기존 Figma element geometry와 screenshot 비교 수치가 유지되거나 개선된다.
- 작은 PC viewport에서 canvas 전체가 viewport 안에 들어오고 letterbox 외 clipping이 0이다.
- resize 후에도 모든 element의 normalized geometry와 aspect ratio가 유지된다.
- Frontend lint/typecheck/unit/build, Desktop auth browser matrix와 mock UI smoke를 통과한다.
- 사용자 검수와 독립 Codex 검증은 완료로 추정하지 않는다.
