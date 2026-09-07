# Frontend AGENTS.md

이 파일은 `frontend/` 아래 작업에 적용되며 Root [AGENTS.md](../AGENTS.md)를 보완한다.

## 구조와 Backend 계약

- React·TypeScript의 기존 component, route, API client와 test convention을 따른다.
- Backend runtime mode, 권한, validation과 mutation 차단을 authoritative source로 취급한다.
- API contract를 추측하거나 화면 상태만으로 서버 정책을 우회하지 않는다.
- raw enum, 내부 식별자, SQL/stack trace와 개발자 전용 경로를 사용자 화면에 노출하지 않는다.
- API type 변경은 기존 소비자의 호환성을 확인하고 runtime response와 test fixture를 함께 갱신한다.
- unrelated UI redesign, dependency update와 lockfile 변경을 기능 수정에 섞지 않는다.

## Action Feedback와 상태 UX

- mutation action은 loading 중 중복 submit을 차단한다.
- 성공·실패 feedback은 사용자가 실행한 action 근처에 표시하고 다음 행동을 안내한다.
- field validation은 가능한 경우 해당 field와 연결하며 첫 오류 focus와 `aria-live` 등 접근 가능한 안내를 제공한다.
- loading, empty, error, authorization denied와 target-not-found를 서로 구분한다.
- ReviewSafe와 fail-closed 상태에서는 mutation control을 비활성화하고 이유를 표시하되 서버 차단을 최종 기준으로 유지한다.

## 접근성, 반응형과 오류

- keyboard 접근, label/role, focus order와 screen-reader feedback을 회귀 검증한다.
- 390px viewport와 Teams narrow pane에서 page-level horizontal overflow가 없어야 한다.
- 표는 header/body 정렬과 action/status/date/number column의 안정성을 확인하고 필요한 경우 table 내부 scroll을 사용한다.
- 정상 경로에서 console error, non-aborted request failure와 blank page가 없어야 한다.
- 실제 UAT 검증은 raw DOM, text, screenshot 또는 console 원문을 출력하지 않고 [Privacy-safe Evidence](../docs/development/privacy-safe-evidence.md)를 따른다.

## 기존 화면과 동일하게 만드는 변경

- 사용자가 기존 화면과 같게 만들라고 지시하면 대상 화면에 비슷한 markup·class를 복제하지 않고, 가능한 한 원본 화면이 호출하는 실제 React 표시 component와 composition을 공용화해 양쪽이 직접 사용하게 한다. 업무 명칭·값·허용 action의 차이만 명시적 prop 또는 adapter로 전달한다.
- 구현 전에 원본과 대상에서 의도적으로 달라야 하는 업무 기능을 기록한다. 승인된 차이 이외의 page section, 간격, 테두리, 표·card 구조, 반응형 전환과 interaction 차이는 동일성 결함으로 취급한다.
- class 문자열, DOM 단위 test와 기존 screenshot 한쪽만으로 디자인 동일성을 판정하지 않는다. 현재 branch의 원본·대상을 같은 viewport·확대율·비교 가능한 합성 data로 함께 capture하고, 공용 영역의 실제 screenshot을 눈으로 비교한 기록이 있어야 완료로 보고한다.
- 원본과 대상의 기능 범위가 달라 전체 화면 pixel 비교가 불가능하면 비교 범위와 제외 기능을 screenshot 전에 고정한다. 결과 보고에서는 공용 영역의 동일성과 의도된 기능 차이를 구분하며 전체 화면이 같다고 과장하지 않는다.
- 이 절은 파일 변경, runtime mutation, Git 게시·merge 또는 운영 적용의 승인 범위를 확대하지 않는다. 현재 Task의 승인 출처, exact allowlist와 상위 지침의 승인 경계를 그대로 따른다.

## Frontend 검증

- lint, typecheck, unit, build와 UI smoke의 적용 범위는 [Validation Matrix](../docs/development/validation-matrix.md)를 따른다.
- 사용자-facing 변경은 desktop과 390px에서 loading/empty/error/success, 권한과 disabled action을 검증한다.
- browser artifact는 실패 분석에 꼭 필요한 isolated synthetic 환경에서만 만들고 tracked/staged 여부를 검사한다.
