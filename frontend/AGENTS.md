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

## Frontend 검증

- lint, typecheck, unit, build와 UI smoke의 적용 범위는 [Validation Matrix](../docs/development/validation-matrix.md)를 따른다.
- 사용자-facing 변경은 desktop과 390px에서 loading/empty/error/success, 권한과 disabled action을 검증한다.
- browser artifact는 실패 분석에 꼭 필요한 isolated synthetic 환경에서만 만들고 tracked/staged 여부를 검사한다.
