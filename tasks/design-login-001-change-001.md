# TASK-DESIGN-LOGIN-001 Change 001 — Desktop Figma exact match

## 1. 승인과 상태

- 승인 source: 2026-07-14 사용자 수정 요청
- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION` 유지
- 대상: Figma node `1:175`의 Desktop 1440×810 로그인 화면
- Mobile: 제외. Figma에 없는 390px 해석과 검증을 이 Change에서 제거한다.
- 구현 승인: 사용자가 같은 요청에서 구체적인 수정과 실행을 명시 승인함
- Commit·Push·PR·Merge: 별도 승인 전 금지

## 2. 확인된 차이와 root cause

- 현재 로그인 화면에는 Figma에 없는 안내 문구, 보안 helper와 `다른 계정으로 로그인` action이 표시된다.
- Primary action 문구가 Figma의 `LOGIN`이 아니라 `Microsoft 365 로그인`이다.
- 제목·Microsoft logo·button·checkbox의 세로 위치가 Figma 절대 좌표와 다르다.
- 왼쪽 장식은 Figma 원본 SVG 대신 CSS 근사 효과를 사용한다.
- 기존 구현은 첫 승인 범위의 기능 보존·공통 shell·390px 해석을 함께 만족시키느라 Figma 단일 Desktop frame 밖의 정보를 추가했다.

## 3. 기대 동작과 포함 범위

- Desktop 1440×810에서 Figma metadata의 위치·크기·색상·radius·shadow·타이포그래피를 그대로 적용한다.
- 로그인 화면에는 Figma에 존재하는 EMI logo, 제목, Microsoft logo, `LOGIN`, 로그인 상태 유지 checkbox만 표시한다.
- Figma에 없는 로그인 안내, helper와 다른 계정 action은 Frontend 인증 UI에 표시하지 않는다.
- 다른 계정 request 정의 자체는 인증 계약에 남기되 사용자 action으로 렌더링하지 않는다.
- 로그인 상태 유지의 실제 checked 상태와 callback은 유지한다. Figma screenshot 비교에서는 unchecked state를 사용한다.
- Figma 원본 ellipse SVG를 사용하고 Background Shape의 10% white overlay를 재현한다.
- Auth 전용 mobile CSS와 390px browser project를 제거한다.

## 4. 제외 범위와 불변조건

- loading·재인증·오류·설정 누락의 복구 안내 문구와 상태 전이는 로그인 화면과 다른 상태이므로 유지한다.
- 기본 로그인은 기존 `loginRequest`, cache location과 silent token·재인증 정책을 유지한다.
- Backend, API, DB, migration, dependency, lockfile와 runtime configuration을 변경하지 않는다.
- 기존 Development·Review-safe runtime을 종료·재시작·교체하지 않는다.
- History/Finding governance Task 파일과 기존 P2 상태를 변경하지 않는다.

## 5. 검증 기준

- Figma `get_design_context`, metadata, variables와 1440×810 screenshot을 다시 조회한다.
- Desktop browser에서 Figma 요소 외 login content가 0인지 확인한다.
- Figma metadata 좌표와 DOM bounding box를 고정 허용오차 안에서 비교한다.
- 동일 viewport·unchecked state screenshot을 Figma 원본과 pixel-level로 비교하고 차이를 기록한다.
- Auth unit, Frontend lint/typecheck/unit/build와 Desktop isolated browser를 통과한다.
- 사용자 검수와 독립 Codex 검증은 완료로 추정하지 않는다.
