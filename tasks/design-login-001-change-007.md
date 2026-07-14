# TASK-DESIGN-LOGIN-001 Change 007 — 로그인 상태 유지 Variant 2와 인증 action audit

## 1. 사용자 요청

- Figma 로그인 페이지의 `로그인 상태 유지` component Variant 2를 확인해 클릭 후 시각 상태를 일치시킨다.
- `다른 아이디로 로그인`이 Frontend 코드에서 기능적으로 삭제됐는지, UI만 숨겼는지 확인한다.
- 로그인 페이지에서 구현은 남아 있으나 버튼 부재 또는 다른 이유로 사용할 수 없는 인증 기능을 조사해 보고한다.
- 현재 로그인 화면 승격은 이 Change 구현·자동·독립 검증·사용자 재검수와 별도 승격 승인 전까지 `NO_GO`다.

## 2. Change Identity

- canonicalTaskId: `TASK-DESIGN-LOGIN-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- gateStatus: `PASS_REUSE`
- purpose: 승인된 Figma checkbox variant 전환 구현과 로그인 action 접근성·도달 가능성 audit
- 변경 경계: Frontend checkbox style·browser test와 Task 문서만 변경
- 보존: Entra request/cache 의미, Microsoft provider 계정 선택, Loading indicator, Backend·API·DB·migration·dependency·runtime configuration

## 3. Figma 재확인

- File/node: `2zikjJ8SpFXm50dH3PE4Cy` / 로그인 화면 `1:175`
- 로그인 상태 유지 instance: `1:187`, `속성 1=베리언트2`
- Component set: `1:160` (`Frame 30`), variant property `속성 1`
- 기본 variant: `1:161`, 흰 checkbox, `#737373` border·text, check icon 없음
- Variant 2: `1:166`, `#DA2127` checkbox, white `Done` icon, text `#282828`
- 원본 component 크기: checkbox `25×25`, group `168×42`
- 로그인 화면 scale 적용 크기: checkbox `18.75×18.75`, stroke `0.75`, radius `5`, icon `13.5×13.5`, icon offset `3/3`
- 최종 로그인 화면 instance의 default는 Variant 2이며 Repository preference도 별도 저장값이 없으면 `true`다.
- Design context·screenshot·metadata와 Figma Plugin API read-only 조회를 사용했다. Code Connect context는 Dev/Full seat 제한으로 계속 조회 불가하며 구현 blocker가 아니다.

## 4. 구현 계약

- 미선택은 기본 variant와 같이 white background, `#737373` border·text, icon 0으로 표시한다.
- 클릭해 선택하면 Variant 2와 같이 `#DA2127` background, Figma `Done` white icon, `#282828` text로 바뀐다.
- Figma 제공 `Done` PNG를 self-contained CSS data asset으로 포함하고 `13.5×13.5`, `3px 3px`에 배치한다.
- checkbox group·responsive geometry와 실제 `checked` semantics를 유지한다.
- 선택값은 기존 `emi-auth-remember-session` preference와 MSAL `localStorage`/`sessionStorage` 선택 계약을 그대로 사용한다.
- 새 button, helper 또는 account-switch UI를 추가하지 않는다.

## 5. 다른 계정 로그인 audit

Frontend 전용 account-switch 기능은 UI만 숨긴 상태가 아니라 다음 source 경계까지 삭제됐다.

- `accountSwitchLoginRequest` 없음
- `prompt: select_account` 없음
- `loginWithDifferentAccount` handler 없음
- `onAccountSwitch` prop 연결 없음
- `다른 계정으로 로그인` button 렌더링 없음

남아 있는 일반 `loginRequest`는 scope만 전달하며 `LOGIN`과 조건부 재인증 action이 함께 사용한다. Microsoft 로그인 화면의 `다른 계정 사용`은 provider가 제공하므로 우리 Frontend 전용 account-switch 기능 삭제와 충돌하지 않는다.

## 6. 로그인 기능 도달 가능성 audit

| 기능 | Frontend 구현 | 접근 경로 | 판정 |
| --- | --- | --- | --- |
| Microsoft 365 기본 로그인 | 있음 | 정상 로그인 화면 `LOGIN` | 사용 가능 |
| 로그인 상태 유지 | 있음 | 정상 로그인 화면 checkbox | 사용 가능 |
| Microsoft 다른 계정 사용 | 우리 전용 구현 없음 | `LOGIN` 뒤 Microsoft provider 화면 | provider에서 사용 가능 |
| cached account 복원 | 있음 | 화면 진입 시 자동 | button이 필요 없는 자동 기능 |
| silent token 확인 | 있음 | account 복원 후 자동 | button이 필요 없는 자동 기능 |
| 재인증 | 있음 | 만료·interaction-required·복수 cached account 상태에서만 button 표시 | 조건부 사용 가능 |
| 로그아웃 | 있음 | 인증 완료 후 topbar·승인 대기·접근 제한 화면 | 로그인 전에는 session이 없어 미표시, 인증 후 사용 가능 |
| 설정 누락 복구 | fail-safe 안내만 있음 | configuration state | Entra 설정 없이는 redirect 불가하므로 login action 없음 |
| 개발 사용자 선택 | Dev mode 전용 | Dev runtime topbar | Entra 로그인 기능이 아니며 Production/Entra에서 의도적으로 미표시 |
| 관리자 검수 사용자 전환 | 조건부 구현 | 인증 후 Development System Administrator와 허용 runtime | 로그인 기능이 아니며 정상 사용자에게 의도적으로 제한 |

현재 정상 로그인 화면에서 구현만 남고 모든 상태에서 접근 불가능한 orphan authentication action은 0이다.

## 7. 검증 계약

- Frontend lint, typecheck, unit 전체와 production build
- 기본·Variant 2의 background, border, icon, icon geometry, text color와 preference 저장 browser 검증
- PC 6개 viewport에서 checkbox 클릭 후 Variant 2 일치
- 기존 로그인·Loading browser matrix와 mock smoke 회귀
- account-switch symbol·prompt·handler·button source count 0
- privacy-safe allowlist·secret·generated artifact·문서 link 검사
- 구현 session과 분리된 read-only 검증

## 8. 제외 범위

- Figma 파일 수정 또는 Code Connect mapping 생성
- Microsoft provider 화면 변경
- 새로운 계정 전환 기능
- 인증 정책·Backend·API·DB·migration·dependency·runtime configuration
- runtime 재시작, commit·push·PR·merge·worktree 정리
