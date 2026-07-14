# TASK-DESIGN-LOGIN-001 Change 004 — 로그인 안내 문구와 Figma 배경·shape 연결부

## 1. 승인과 상태

- 승인 source: 2026-07-14 사용자 수정 요청
- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION` 유지
- 대상: Figma node `1:175`의 Desktop 로그인 화면
- 구현 승인: 사용자가 같은 요청에서 안내 문구 추가, Figma 기본 배경색 재확인과 white shape/red background 연결부 수정을 명시 승인함
- 안내 문구는 Change 001의 Figma-only content 계약에 대한 명시적 사용자 승인 예외다.
- Mobile/390px: 계속 제외
- Commit·Push·PR·Merge: 별도 승인 전 금지

## 2. Figma 재확인 결과

- 최상위 frame `1:175` fill: solid `#DA2127`, opacity 1
- background glass shape `1:179`:
  - `x=-6`, `y=0`, `1446×810`
  - white solid fill, opacity `0.1`
  - `GLASS` effect radius `23.25`
  - stacking index 2 — ellipse 66/67 위, EMI logo와 Ellipse 68 아래
- white authentication shape `1:182`:
  - `x=776`, `y=0`, `664.5×810`
  - white solid fill, opacity 1
  - top-left/bottom-left radius `51`, right radius `0`
  - drop shadow `x=-5.25`, `y=-1.5`, blur `43.05`, spread `0`, black opacity `0.28`
  - red frame과 glass shape 위에 겹치는 독립 rectangle
- white shape의 둥근 왼쪽 모서리 밖과 shadow 뒤에는 white container가 아니라 red frame/glass surface가 보여야 한다.

## 3. 확인된 차이와 수정

- 이전 login 전용 CSS는 frame 배경을 `#DB2227`로 보정하고 login grid 뒤를 white로 두었다.
- 이 구조에서는 white shape의 rounded corner 바깥이 흰색으로 이어질 수 있어 Figma stacking과 달랐다.
- login root와 responsive layout의 base를 Figma 원본 `#DA2127`로 변경한다.
- global glass layer를 responsive scale에 맞춰 왼쪽 `-6px`, white 10%, blur `23.25px`로 적용한다.
- white panel 자체만 white로 유지하고 panel 뒤 layout은 red로 두어 rounded corner 밖으로 red surface가 이어지게 한다.
- white shape visible width는 reference에서 `664.5px`, radius와 shadow는 공통 responsive scale로 유지한다.
- Ellipse 68 pattern은 Figma stacking에 맞춰 glass layer 위의 별도 reference canvas로 분리한다.

## 4. 안내 문구 계약

- 문구: `회사 Microsoft 365 계정으로 로그인해 주세요.`
- 위치: authentication reference canvas `x=109`, `y=472`, `447×18`
- style: 14px, medium, `#737373`, center
- Microsoft logo와 `LOGIN` button 사이에 배치한다.
- 안내 문구 외 Figma 비포함 helper, secondary action과 다른 계정 action은 계속 표시하지 않는다.

## 5. 검증 계약과 불변조건

- 6개 PC viewport에서 base color, glass fill, white shape fill, left radius, shadow와 panel continuity를 fixed projection으로 검증한다.
- 안내 문구 count 1, exact text와 normalized geometry를 검증한다.
- Frontend lint, typecheck, unit, build, mock UI와 auth browser matrix를 재실행한다.
- Microsoft 365 로그인, 로그인 상태 유지, silent token·재인증과 다른 계정 request 정의를 변경하지 않는다.
- Backend, API, DB, migration, dependency, lockfile와 runtime configuration을 변경하지 않는다.
- 기존 Development·Review-safe runtime을 종료·재시작·교체하지 않는다.
- 사용자 검수와 독립 Codex 검증은 완료로 추정하지 않는다.
