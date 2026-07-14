# TASK-DESIGN-LOGIN-001 Change 005 — Microsoft 계정 선택 경계와 로그인 loading 화면

## 1. 승인과 상태

- 승인 source: 2026-07-14 사용자 정책 결정과 수정 요청
- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION` 유지
- 구현 위치: 기존 5176 디자인 실험 worktree
- 화면 단위 승격 방식: 확정. Figma 화면이 완성되면 5176에서 구현·검수하고 최신 main 기준의 깨끗한 승격 branch로 화면 단위 게시한다.
- 현재 로그인 화면 승격: `NO_GO`. 이 Change 구현·자동 검증·독립 검증과 사용자 재검수 완료 전 commit·push·PR·merge를 수행하지 않는다.

## 2. 다른 계정 로그인 정책

- 우리 Frontend에는 `다른 계정으로 로그인` control을 만들지 않는다.
- 기본 `LOGIN`은 기존 prompt 없는 `loginRequest`를 유지하고 Microsoft 로그인 화면으로 이동한다.
- 계정 선택과 `다른 계정 사용` UX는 Microsoft 로그인 화면을 authoritative provider UI로 사용한다.
- Frontend에서 사용되지 않는 `accountSwitchLoginRequest`와 그 전용 test 계약은 제거한다.
- 이 결정은 Change 001~004의 `accountSwitchLoginRequest` 정의 보존 조항만 후속 대체한다. 기본 로그인·재인증·cache·보안 불변조건은 그대로 유지한다.
- Entra tenant, authority, scope, redirect, cache, 조건부 액세스, MFA와 로그인 상태 유지 의미는 변경하지 않는다.

## 3. Loading 화면 계약

- MSAL 초기화, interactive login 진행, cached account/token 확인과 `/api/me` 확인 중 화면은 일반 로그인 화면과 동일한 Desktop layout을 사용한다.
- EMI logo, 제품명, Microsoft logo, `LOGIN`, 로그인 상태 유지 checkbox, red/white panel, 장식, radius와 shadow의 위치·비율을 일반 로그인 화면과 동일하게 유지한다.
- Loading에서 달라지는 visible content는 안내 문구뿐이다.
  - 일반: `회사 Microsoft 365 계정으로 로그인해 주세요.`
  - Loading: `Microsoft 365 로그인 정보를 확인하고 있습니다.`
- Loading 동안 중복 인증과 cache 재생성을 막기 위해 `LOGIN`과 checkbox는 disabled로 유지하되 opacity와 geometry를 바꾸지 않는다.
- 별도 spinner나 Figma에 없는 loading element는 추가하지 않는다. `aria-busy`는 유지한다.

## 4. 별도 상태 경계

- Microsoft 로그인 화면으로 이동하기 위한 별도 `다른 계정` 또는 계정 누락 화면은 만들지 않는다.
- Entra runtime configuration이 실제로 누락되면 redirect 자체가 불가능하므로 기존 fail-safe 설정 안내는 삭제하지 않는다. 별도 Figma variant를 신규 설계하지 않고 기존 안전 상태로 보존한다.
- 재인증·오류·승인 대기와 접근 제한의 업무 의미, action과 복구 경로는 변경하지 않는다.

## 5. 검증과 불변조건

- 기본 로그인과 Loading의 element count, title, asset, button, checkbox, guidance 외 structure가 동일한지 unit test로 검증한다.
- Loading은 exact 로그인 layout marker, disabled controls, `aria-busy`, 안내 문구와 spinner 0을 검증한다.
- 기본 login request에 강제 `prompt`가 없고 Frontend의 다른 계정 control·전용 request가 제거됐는지 검증한다.
- Frontend lint, typecheck, unit, build, mock UI와 기존 PC auth browser matrix를 재실행한다.
- Backend, API, DB, migration, dependency, lockfile, runtime configuration과 기존 5174·5081·5092·5190·5432 runtime을 변경하지 않는다.
- 5176 process는 재시작하지 않고 Vite HMR로만 변경을 반영한다.
- `TASK-GOV-HISTORY-REWRITE-001`, `TASK-GOV-FINDING-GATE-001`과 기존 history P2 상태를 변경하지 않는다.
