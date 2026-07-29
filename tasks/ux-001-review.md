# TASK-UX-001 A1 — Codex 내용·제품 방향 Review

## 1. Review 범위와 결론

- reviewSource: `tasks/ux-001-planning.md`
- interviewSource: `tasks/ux-001-interview.md`
- taskType: `NEW_FEATURE`
- reviewOwner: `CODEX`
- reviewRound: 1
- productDirectionVerdict: `KEEP_WITH_REQUIRED_RESOLUTIONS`
- openBlockingDecisionCount: 0

Fable 1차 기획의 핵심 방향은 유지한다. 내 업무·알림은 현장 사용 빈도가 높고 mutation 결과가 상단 문자열에만 나타나 현재 모바일 우선 제품 방향과 맞지 않는다. A1을 두 화면으로 한정하고 공통 hook+component를 만든 뒤 A2를 분리하는 순서도 Roadmap의 범위 팽창 방지 의도와 맞다.

다만 현재 기획 그대로 구현하면 성공한 행이 active tab에서 사라져 “행 근처 성공 feedback”도 함께 사라지고, fire-and-forget 재조회와 병렬 action의 응답 순서가 최신 화면을 덮을 수 있다. 2차 기획은 아래 P1/P2 resolution을 반드시 최종 계약에 포함해야 한다.

## 2. 사용자 문제와 기대 결과 검토

사용자가 원하는 결과는 단순히 색이 있는 메시지를 추가하는 것이 아니다. action을 누른 바로 그 맥락에서 다음 네 가지를 알아야 한다.

1. 지금 처리 중인지
2. 실제 mutation이 성공했는지
3. 최신 목록 재조회만 실패한 것인지
4. 실패 뒤 다시 누를지, 새로고침할지, 권한을 확인할지

1차 기획은 이 문제를 정확히 잡았다. 특히 `partial`을 mutation 실패와 분리한 점, 문자열 포함 검사 대신 구조화된 tone을 쓰는 점, 같은 action 중복 submit을 차단하는 점은 유지할 가치가 높다.

## 3. 기능 분류

### 유지

| 기능 | 판단 | 근거 |
| --- | --- | --- |
| A1을 내 업무·알림 두 화면으로 한정 | 유지 | 사용자 가치가 높고 A2 전체 화면 확장보다 검증·rollback 경계가 작다. |
| scope key 기반 hook + 기존 `ActionFeedback` 확장 | 유지 | 화면별 문자열 판정과 busy 중복을 줄이고 A2 재사용 기반이 된다. |
| 구조화된 `loading/success/error/partial` | 유지 | mutation 결과와 refresh 결과를 정확히 구분한다. |
| 동일 action 중복 submit 차단 | 유지 | 모바일 반복 tap과 중복 요청 위험을 직접 낮춘다. |
| 다른 행 action의 제한적 병렬 허용 | 유지 | 화면 전체 잠금보다 업무 흐름이 빠르다. 단, collection refresh 순서 보호가 필수다. |
| 오류 focus와 screen-reader announcement | 유지 | 시각적 feedback만으로 해결되지 않는 접근성 문제를 다룬다. |

### 추가

| 기능 | 판단 | 근거 |
| --- | --- | --- |
| 성공 행 제거 뒤에도 남는 contextual summary feedback | 추가 | `Requested` 업무 완료, `unread` 알림 읽음은 성공 즉시 active tab에서 행이 사라져 row feedback만으로 결과를 보여 줄 수 없다. |
| post-mutation refresh의 stale-while-refresh | 추가 | 기존 ready 목록을 유지해야 partial·오류 feedback과 재시도 맥락이 사라지지 않는다. |
| page load request generation guard | 추가 | 서로 다른 행 action이 병렬 완료될 때 늦은 과거 refresh가 최신 결과를 덮지 않게 한다. |
| bulk/row conflict contract | 추가 | `전체 읽음`과 개별 `읽음`이 동시에 실행되면 관련 scope 잠금 계약이 깨진다. 양방향 차단이 필요하다. |
| stable error guidance mapper | 추가 | 403/404/409/network/기타를 문자열 검색 없이 다음 행동과 연결해야 한다. 기존 `ApiError` status와 message를 재사용한다. |
| 명시적 feedback lifecycle | 추가 | 성공/partial은 다음 action 또는 사용자의 닫기/새로고침까지 유지하고, 오류는 재시도 전까지 유지해야 결과가 순간적으로 사라지지 않는다. |

### 보류

| 기능 | 판단 | 근거 |
| --- | --- | --- |
| A2 생산계획·구매·자재·패널·Excel 전면 확대 | 보류 | A1 contract와 시각·접근성 검수 후 같은 canonical Task의 다음 change로 진행한다. |
| `StateMessage` 전역 재설계 | 보류 | 두 list page의 retry/empty/error 안내만 최소 보강하고 다른 화면 계약은 건드리지 않는다. |
| Pending·관리자 등 기존 문자열 tone call site 정리 | 보류 | 현재 A1 사용자 문제와 직접 관계없고 changed-file 범위를 크게 늘린다. |
| 전역 toast store | 보류 | action 인접 feedback 원칙을 흐리고 A1에 전역 상태를 도입할 이유가 없다. |

### 제거

| 기능 | 판단 | 근거 |
| --- | --- | --- |
| 성공 feedback 자동 소멸 | 제거 | 시간 기반 소멸은 모바일·screen reader 사용자가 결과를 놓칠 수 있고 테스트도 불안정해진다. 다음 action/reset 또는 명시적 닫기로 관리한다. |
| 두 화면에서 target-not-found 상태를 억지로 추가 | 제거 | collection list의 404는 정상 제품 흐름이 아니며, 상세 target-not-found 계약은 이번 A1 범위가 아니다. |

## 4. Required Finding resolutions

| ID | Severity | 상태 | 원인·영향 | 2차 기획 resolution |
| --- | --- | --- | --- | --- |
| `UX-A1-ROW-FEEDBACK-DISAPPEARS` | P1 | `REQUIRED` | 업무 완료·알림 읽음 성공 뒤 active tab에서 행이 제거돼 row feedback도 즉시 사라진다. 사용자는 성공 여부를 놓친다. | loading/error는 row/card 인접, 성공/partial은 항목 식별 label을 포함한 page contextual region에도 보존한다. 전체 읽음은 header action region을 사용한다. |
| `UX-A1-REFRESH-FIRE-AND-FORGET` | P1 | `REQUIRED` | 현재 `load()`는 Promise 결과를 호출자에게 반환하지 않아 mutation 성공/refresh 실패를 partial로 판정할 수 없다. | `load`가 성공/실패를 반환하고 post-mutation mode에서는 기존 ready data를 보존한다. 최초/manual load와 action refresh를 구분한다. |
| `UX-A1-REFRESH-RESPONSE-RACE` | P1 | `REQUIRED` | 서로 다른 행 action의 병렬 refresh가 응답 순서에 따라 최신 목록을 과거 응답으로 덮을 수 있다. | page별 request generation 또는 동등한 stale-response guard를 적용하고 마지막 시작 refresh만 state를 갱신한다. |
| `UX-A1-BULK-ROW-CONFLICT` | P2 | `REQUIRED` | 전체 읽음과 개별 읽음이 서로의 진행 상태를 모르면 관련 scope 잠금이 비대칭이다. | 전체 읽음 중 모든 읽음 action 차단, 개별 읽음 진행 중 전체 읽음 차단. 상세·이동·새로고침의 허용 여부도 명시한다. |
| `UX-A1-TONE-AND-GUIDANCE` | P2 | `REQUIRED` | 문자열 포함 여부로 tone을 추론하면 한글 문구 변경 때 성공/오류가 뒤집힌다. | try/catch + `ApiError.status` 기반으로 tone과 403/404/409/network/기타 다음 행동을 구조화한다. 서버 메시지는 안전한 한글일 때 원인으로 보존한다. |
| `UX-A1-FOCUS-ANCHOR` | P2 | `REQUIRED` | error focus 대상이 row 제거·rerender로 사라질 수 있고 단순 `role`만으로 focus 이동은 되지 않는다. | feedback component가 stable focus target(`tabIndex=-1` + ref)을 제공하고 오류/partial은 살아 있는 contextual anchor로 focus한다. 성공은 focus를 강제하지 않는다. |
| `UX-A1-SELECTION-REGRESSION` | P2 | `REQUIRED` | post-mutation reload가 visible ID를 바꾸면 현재 선택 export selection이 hook effect에서 정리될 수 있다. 실패·partial에서 선택을 유지한다는 계약과 충돌한다. | 실패·partial refresh에서는 기존 ready data/selection을 보존하고, 성공 refresh에서 사라진 ID만 기존 `useSelectedRows` 계약대로 정리한다. |

## 5. 권장 최종 UX 계약

### 내 업무

- `이동/시작`: 해당 행만 busy. 시작 mutation 실패는 행 내부 오류. 시작 성공 뒤 이동하므로 별도 성공 banner를 강제하지 않는다. 시작 성공·화면 이동 실패를 Frontend mutation 실패로 표현하지 않는다.
- `작업 완료`: 해당 행 action 잠금과 loading feedback. 오류면 행 내부 오류와 focus. 성공 뒤 행이 사라질 수 있으므로 페이지 목록 상단 contextual feedback에 업무 표시명과 성공 결과를 보존한다.
- post-mutation refresh 실패: 행을 지우지 않고 기존 목록을 보존하며 contextual `partial`에 `새로고침` 다음 행동을 표시한다.

### 알림

- 개별 `읽음`: 해당 행 busy. 오류는 행 내부, 성공/partial은 행 제거를 고려해 contextual region에도 보존한다.
- `전체 읽음`: header action group 안에 loading/success/error/partial을 표시한다. 진행 중에는 모든 개별 읽음을 차단하고, 개별 읽음 진행 중에는 전체 읽음을 차단한다.
- 상세·프로젝트 이동은 mutation scope가 아니므로 읽음 action과 분리하되, 같은 button row의 accidental double activation이 없도록 disabled label과 focus order를 유지한다.

## 6. 구현 순서 권고

1. 공통 hook의 상태·conflict·run/refresh 결과 contract와 unit test
2. `ActionFeedback`의 stable focus target과 기존 call site 호환
3. 내 업무: load result/stale guard → 완료/시작 action → contextual placement
4. 알림: load result/stale guard → 개별/전체 읽음 conflict → contextual placement
5. App unit에서 success/error/partial·행 제거·tab/selection·race 검증
6. Frontend 전체 validation과 isolated Full-Stack 관련 회귀
7. desktop·390px 정상/오류/partial synthetic screenshot과 implementation report

## 7. 범위·비용·대안 검토

Frontend-only A1로 유지하면 신규 API·migration·provider가 없고 rollback도 작은 편이다. 가장 큰 비용은 공통 hook 자체가 아니라 기존 `load()`를 결과 반환·stale guard·preserve-ready mode로 바꾸는 부분이다. 이를 생략하면 partial contract와 병렬 action 계약이 이름뿐인 기능이 되므로 P1으로 본다.

화면 전체를 한 번에 잠그는 대안은 구현이 단순하지만 현장 사용자의 여러 업무 처리 속도를 불필요하게 낮춘다. 반대로 제한 없는 병렬 action은 refresh race를 만든다. 따라서 “서로 다른 row mutation 허용 + collection refresh stale guard”가 현재 제품에 가장 적합하다.

## 8. 2차 Fable 기획 반영 요구

Fable 2차 기획은 다음을 완전한 최종 구현 계약으로 통합해야 한다.

- P1/P2 7건의 resolution
- row action과 row 제거 뒤 contextual feedback의 이중 placement 규칙
- 최초/manual load와 post-mutation refresh의 state 보존 차이
- notification bulk/row conflict와 page별 stale-response guard
- 구조화된 status/tone/next-action mapper
- 성공 자동 소멸 제거, A2·전역 `StateMessage`·toast 보류
- Backend/API/DB/migration/provider/main/게시 변경 0

추가 Fable/Codex loop는 만들지 않는다. 2차 기획의 `openBlockingDecisionCount: 0`이면 experiment fast-track에 따라 Codex 구현으로 이어간다.
