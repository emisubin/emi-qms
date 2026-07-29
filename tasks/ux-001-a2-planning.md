# TASK-UX-001 A2 — 업무 화면 Action Feedback 확대 기획안 (Fable 1차 planning)

> 상태: Draft
> 작성 단계: Codex 내용 review 전 (experiment fast-track 1차 기획)
> 목적: A1 공통 action feedback 계약을 A2 업무 화면으로 확대하기 위한 구현 전 기획

- taskId: `TASK-UX-001` (canonical) / slice `A2`
- taskType: `NEW_FEATURE` (experiment fast-track)
- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/ux-001-a2-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- identityGateSource: `tasks/ux-001-a2-change-001.md` (`gateStatus: PASS_REUSE`)
- completedSliceExcluded: `A1` (`EXPERIMENT_SLICE_COMPLETE` — 재구현 금지)

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/validation-matrix.md`, `docs/development/privacy-safe-evidence.md`를 따르며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: A2 업무 화면(생산계획·구매·자재·패널·선택 Excel)은 저장·검사·업로드·내보내기 결과를 화면별 문자열 message와 분산 상태로 표시해, 어느 action이 처리 중인지·어떤 field를 고쳐야 하는지·mutation 성공과 refresh/download 실패의 구분을 action 위치에서 파악하기 어렵다.
- 대상 사용자·역할: 생산관리·구매·자재/품질·설계 담당과 조회·내보내기 사용자. 기존 권한·조회 scope·mutation 범위를 그대로 사용하며 새 권한·역할·업무 상태를 만들지 않는다.
- 정상 흐름: action 실행 → 해당 scope만 busy/disabled → action 인접 성공 안내 → 필요한 목록/summary만 재조회 → 선택·필터·스크롤 context 유지.
- 예외·복구 흐름: field validation 실패는 요약+field 오류와 첫 invalid field focus, `aria-describedby`·live announcement 연결. 동일 scope 중복 submit 차단, bulk/row·preview/apply 양방향 conflict 차단. mutation 성공 뒤 refresh/download 실패는 `partial`로 안내하고 실패로 되돌려 표시하지 않는다. 403/404/409/network는 A1 구조화 guidance 재사용.
- 확정한 정책과 명시적 제외: Backend 권한·validation·audit·상태 전이 보존, A1 hook/component/guidance 재사용, feedback 비영속, migration 없음(발견 시 중단), 신규 notification event·실제 provider 제외, A1 재구현·Excel column picker·row cap 변경·전역 toast store·전체 `StateMessage` 재설계·대표 repo·`main`·Persistent UAT·push·PR·merge 제외.
- planning으로 넘긴 비차단 미결정 사항: 전역 toast store 재검토, 관리자·Pending 등 다른 화면의 전면 확대(16장 참조).
- Interview 선택·결정 4건(공통 계약 재사용, 요약+field 오류·첫 invalid focus, Excel 성공 기준 = server blob 수신, 위험 순서 vertical slice)은 experiment standing instruction에 따라 Fable 권장안이 자동 채택됐다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 planning·구현 승인이 아니다.

## 1. 한 줄 목표

생산계획·구매·자재·패널·선택 Excel 화면에서 사용자가 실행한 저장·preview/apply·검사·내보내기 action의 처리 중·성공·부분 성공·실패와 다음 행동을 action 인접 위치에서 즉시 확인하고, 첫 field 오류로 이동하며, 선택·필터·context를 잃지 않고 안전하게 재시도할 수 있게 한다.

## 2. 배경과 해결할 업무 문제 (현재 구현 대조 결과)

Planning 전 재검증에서 확인한 현재 구현 사실은 다음과 같다.

- 상태 판정이 문자열 포함 검사다: 편집 화면들은 하단 단일 `message`를 `successMessage()`(“저장했습니다/다운로드했습니다” 포함 검사)로, 자재 workspace는 `message.includes('했습니다')`로 성공/오류 tone을 판정한다. A1에서 제거한 `UX-A1-TONE-AND-GUIDANCE` 유형 결함이 A2 화면에 남아 있다.
- feedback 위치가 action에서 멀다: 생산계획·구매·자재·패널 편집 화면은 페이지 최하단 `role="alert"` 문단 하나에 결과를 표시한다. Excel dialog는 오류를 error-text 한 곳에만 표시한다.
- 저장 성공 안내가 없다: 생산계획·구매·자재 입고·패널 편집의 `save()`는 성공 시 즉시 `onBack()`으로 이동해 성공 안내가 어디에도 남지 않고, 복귀 화면 목록 재조회 실패는 사용자에게 보이지 않는다(mutation 성공·refresh 실패의 partial 구분 부재).
- field 오류 연결이 불완전하다: `FormErrorSummary`(요약, 클릭 시 `focusField`)와 `FieldErrorMessage`는 존재하지만, 검증 실패 시 첫 invalid field 자동 focus가 없고 input과 오류 문구의 `aria-describedby` 연결이 없다. 요약은 클릭해야만 이동한다.
- 중복·충돌 차단이 부분적이다: `isSaving`·`isPreviewing`·`isApplying` 등 개별 flag는 있지만, 같은 데이터를 바꾸는 action 사이 conflict(예: 편집 화면 저장 vs Excel 적용, preview 진행 중 apply 등)는 공통 계약으로 차단되지 않는다.
- 선택 Excel export의 성공 기준이 뭉쳐 있다: `ExcelExportAction`은 server blob 수신과 client download trigger를 하나의 try로 묶어 성공/실패 2단계 tone만 가진다. blob 수신 후 client 예외는 전체 실패로 표시될 수 있다(interview 결정 ③과 불일치).
- stale response guard가 없다: A1 두 화면 밖의 목록/summary 재조회에는 generation guard가 없어 응답 역전 시 오래된 데이터가 화면을 덮을 수 있다.

이대로 두면 모바일 현장 사용자의 중복 제출·오입력 가능성이 남고, keyboard·screen reader 사용자는 결과와 오류 위치를 놓친다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 생산관리 담당 | 생산계획 저장, 담당자/일정 입력, 양식 다운로드, Excel preview/apply | 기존 project scope | 기존 `production-planning.update` 범위 |
| 구매 담당 | 구매 정보 저장, Excel preview/apply | 기존 project scope | 기존 구매 mutation 범위 |
| 자재·품질 담당 | 자재 도착·입고 확정·키팅·IQC 판정 관련 action | 기존 project scope | 기존 담당 mutation 범위 (타 부서 조회만 유지) |
| 설계 담당 | 패널 정보 저장, Excel preview/apply | 기존 project scope | 기존 패널 mutation 범위 (목포장 size validation 유지) |
| 조회·내보내기 사용자 | 선택 row Excel 내보내기 | 기존 조회 scope | export 생성만 (server-side selection·권한 재검증 유지) |

권한·validation·409 충돌 판정은 서버 응답이 최종 기준이다. Frontend busy 잠금은 UX 방어일 뿐 서버 정책을 대체하지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 생산계획 저장 성공과 복귀

1. 생산관리 담당이 예정일·담당자를 입력하고 `저장`을 누른다.
2. 저장 버튼 scope만 busy·`저장 중`으로 잠기고, 같은 프로젝트의 Excel 업로드·양식 다운로드 등 동일 데이터 action이 함께 차단된다.
3. 성공 시 기존 흐름대로 상세로 복귀하되, 복귀 화면 contextual region에 프로젝트 제목을 포함한 성공 안내가 남는다. 복귀 화면 재조회 실패 시 `partial`(처리 완료 + 새로고침 안내)로 표시한다.

### 시나리오 B — field validation 실패

1. 구매 담당이 필수 항목을 비운 채 저장한다.
2. 요약(`FormErrorSummary`)과 각 field 아래 한글 오류가 함께 표시되고, 첫 invalid field로 focus가 자동 이동한다.
3. 각 input은 `aria-describedby`로 자기 오류 문구와 연결되고, live region이 오류 발생을 announcement한다. 입력값·선택·filter는 보존된다.

### 시나리오 C — Excel preview/apply

1. 설계 담당이 패널 Excel을 업로드해 preview를 실행한다. preview 진행 중 apply와 편집 화면 저장은 차단된다(양방향).
2. apply 성공 후 부모 화면 재조회가 실패하면 mutation 실패로 되돌려 표시하지 않고 partial과 `새로고침` 안내를 표시한다.
3. 409(버전 충돌)면 A1 guidance mapper의 “목록을 새로고침한 뒤 다시 확인” 안내와 focus 이동을 재사용한다.

### 시나리오 D — 선택 Excel 내보내기

1. 사용자가 여러 row를 선택하고 `선택 Excel 내보내기`를 누른다. 선택·필터는 유지된다.
2. server blob 수신까지가 성공 기준이다. blob 수신 후 client download trigger 예외는 전체 실패가 아니라 partial(파일 생성 완료 + 재시도 안내)로 구조화한다.
3. 422/429는 기존 hint 문구를 유지하면서 A1 tone 구조로 표시한다.

### 시나리오 E — 자재·IQC action

1. 자재 담당이 도착 등록 또는 입고 확정을 실행하면 해당 row/action scope만 busy가 된다.
2. IQC 판정 저장 성공·실패·부적합 Pending 등록 결과가 action 인접에 구조화 tone으로 표시되고, 목록 재조회는 기존 선택·filter를 보존하는 preserve mode와 generation guard를 따른다.

## 5. 기능 요구사항

### 필수

- [ ] A1 `useActionFeedback`·`ActionFeedback`·`actionErrorMessage`를 A2 화면에서 재사용하고, 필요한 최소 확장만 추가한다.
- [ ] 대상 화면의 저장/preview/apply/판정/다운로드 action에 scope busy, 구조화 tone(`loading/success/error/partial`), action 인접 placement를 적용하고 문자열 포함 tone 판정(`successMessage`, `includes('했습니다')` 등)을 대상 화면에서 제거한다.
- [ ] field validation 실패 시 요약+field 오류 병행, 첫 invalid field 자동 focus, `aria-describedby` 연결, live announcement를 제공한다(client 검증과 서버 400 `ApiError.errors` 모두).
- [ ] 동일 scope 중복 submit 차단과 같은 데이터를 바꾸는 action 간 양방향 conflict(저장↔Excel apply, preview↔apply, bulk↔row)를 선언적으로 차단한다.
- [ ] mutation 성공 + 재조회/다운로드 trigger 실패를 `partial`로 구분하고 서버 mutation rollback 정책은 변경하지 않는다.
- [ ] 저장 성공 후 화면 이동이 있는 흐름에서 성공 결과가 소실되지 않도록 복귀 화면 contextual 안내를 보존한다(16장 결정 3의 권장안).
- [ ] 목록/summary 재조회에 stale response(generation) guard와 선택·filter·스크롤 context 보존(preserve mode)을 적용한다.
- [ ] 선택 Excel export의 성공 기준을 server blob 수신으로 확정하고 client trigger 예외를 partial/error로 구조화한다.
- [ ] desktop·390px에서 loading/success/error/partial과 page-level horizontal overflow 0을 검증하고 페이지별 대표 synthetic screenshot을 확보한다.

### 선택

- [ ] 중복 상단 banner가 있는 화면은 contextual region 하나로 통합한다(무관 redesign 금지, 배치 보조 스타일만).

### 명시적 제외

- [ ] A1 내 업무·알림 화면 재구현 또는 계약 변경
- [ ] 업무 규칙·권한·상태 전이·API 기능·DB·migration의 신규 능력
- [ ] Excel column picker·multi-sheet·row cap·workbook 내용 변경
- [ ] 알림 delivery 재처리·사용자 preference·실제 provider 호출
- [ ] 전역 toast store, 전체 `StateMessage` 재설계, 무관 화면 redesign
- [ ] 대표 repo·`main`·Persistent UAT·push·PR·merge

## 6. 화면·UX 기획

| 화면(군) | 진입 경로 | 대상 action | 성공/실패 피드백 |
| --- | --- | --- | --- |
| 생산계획 수정(+Excel dialog) | 프로젝트 상세 → 생산계획 수정 | 저장, 양식 다운로드, Excel preview/apply | 저장: busy·field focus·복귀 contextual 성공/partial. 다운로드: blob 기준 success/partial. dialog: preview/apply 인접 tone |
| 구매 수정·현황(+Excel dialog) | 구매 dashboard → 수정/업로드 | 저장, Excel preview/apply | 위와 동일 계약. preview↔apply·저장↔apply 양방향 차단 |
| 자재 입고·입고 확정·키팅·IQC | 자재 workspace, 키팅, IQC 성적서 | 도착 등록, 입고 확정, 키팅 처리, IQC 판정 저장 | row/action 인접 tone, bulk/row conflict, preserve refresh + generation guard |
| 패널 정보 수정(+Excel dialog) | 프로젝트 상세 → 패널 정보 | 저장(목포장 size validation 유지), Excel preview/apply | field 오류 focus·`aria-describedby`, 복귀 contextual 성공/partial |
| 공통 선택 Excel export | 각 목록 화면 `SelectedExportTray` | 선택 내보내기 | server blob 성공 기준, client trigger 예외 partial, 선택 보존 |

placement 규칙은 A1 이중 규칙을 재사용한다: loading·행이 살아 있는 error는 row/action 인접, 화면 이동·행 제거로 맥락이 사라질 수 있는 success/partial은 대상 label을 포함해 contextual region 1곳에 보존한다. contextual region은 화면당 1개 `ActionFeedback` 기반 영역이며 전역 toast가 아니다. 모바일은 action과 feedback을 같은 card/section 안에 배치하고 390px·Teams narrow에서 page-level overflow 0을 유지한다.

## 7. 업무 규칙과 불변조건

- Backend 권한·validation·업무 상태 전이·audit·export contract(server-side selection·권한 재검증·row cap)는 변경하지 않는다.
- 같은 scope 진행 중 중복 submit은 차단되고, 같은 데이터를 바꾸는 action conflict는 양방향으로 차단된다.
- mutation 성공은 이후 refresh/download trigger 실패로 실패 표시되지 않는다(partial 구분).
- 과거 재조회 응답이 최신 화면을 덮지 않는다(generation guard).
- 실패·partial에서 입력값·선택·filter·스크롤 context를 잃지 않는다.
- A1 두 화면(`/my-work`, `/notifications`), 알림 preference 화면, `SelectedExportTray`/`useSelectedRows` busy 계약은 동작 호환을 유지하며 회귀로만 검증한다.
- raw status code·내부 enum·stack trace를 사용자 문구에 노출하지 않는다(`sanitizeUserMessage`·guidance mapper 계약 유지).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| action feedback state | scope별 `{ tone, message }` 화면 session 상태 | 기존(A1) 재사용 | 영구 저장 없음, audit 불변 |
| busy scope 집합 | 진행 중 action scope와 conflict 선언 | 기존(A1) 재사용·scope naming만 확장 | 없음 |
| field error map | `errors: Record<field, message>` | 기존 재사용 | 없음 |
| refresh generation | 화면별 stale-response guard 번호 | A1 패턴을 대상 화면에 확장 | 없음 |

```text
idle → loading → success | error | partial → (다음 action 시작·명시적 닫기·수동 새로고침) → idle
```

제품 DB 개념·상태 전이는 추가하지 않는다. 시간 기반 자동 소멸은 A1 결정대로 사용하지 않는다.

## 9. API·Backend 고려사항

- Backend authoritative 규칙: 권한(403), 대상 존재(404), 버전/상태 충돌(409), field validation(400 + `ApiError.errors`), export 재검증(422)·rate limit(429)은 모두 기존 서버 응답을 그대로 사용한다.
- 필요한 조회·mutation: 신규 endpoint 없음. 기존 저장/preview/apply/판정/export API만 사용한다.
- transaction·동시성·idempotency: 서버 계약 불변. Frontend는 UX 방어(중복 submit·conflict 차단)만 추가한다.
- audit trail: 변경 없음. 수정사유(reason) 등 기존 입력 계약을 유지한다.
- 외부 provider 영향: 없음.
- 중단 조건: 기존 API 오류 계약이 field 연결·guidance 구분에 부족해 Backend 보정이 필요하거나 migration·신규 능력 필요가 발견되면 임의 진행하지 않고 blocking decision으로 보고한다.

## 10. Frontend 고려사항

- 재사용 대상(존재 확인 완료): `useActionFeedback`(busy ref 동기 갱신, scopes/prefixes conflict, `run`의 refresh boolean·partial, `actionErrorMessage` 403/404/409 mapper), `ActionFeedback`(role/`aria-live`/`focusOnAttention`), `FormErrorSummary`·`FieldErrorMessage`·`fieldError`·`focusField`, `handleFormError`·`fieldErrorsFromApiError`·`sanitizeUserMessage`, `LoadState`/`toLoadError`/`StateMessage`, `useSelectedRows`·`SelectedExportTray`.
- 최소 확장(신규 능력이 아니라 기존 계약의 일반화):
  - scope naming을 A2로 확장(예: `production-plan:<projectId>:save`, `procurement:excel:preview` 등 prefix conflict 활용).
  - 검증 실패 시 첫 invalid field 자동 focus helper — 기존 `focusField`를 field 순서 기준으로 재사용.
  - field input과 `FieldErrorMessage`의 `aria-describedby` id 연결(기존 component에 id 부여).
  - `ExcelExportAction`의 성공 판정을 blob 수신/trigger 2단계로 분리하고 tone을 A1 4-tone으로 정렬(기존 422/429 hint 문구 유지).
  - 화면 이동을 수반하는 저장 성공의 contextual 안내 전달(부모 화면 보유 feedback state 또는 콜백 인자; 전역 store 도입 금지).
- loading/empty/error/success: 목록 조회는 기존 `LoadState`/`StateMessage` 패턴 유지, post-mutation 재조회만 preserve mode + generation guard로 전환한다.
- 접근성: error `role=alert`/assertive, 그 외 `role=status`/polite, keyboard·focus order·label 회귀 유지.
- 390px/mobile/narrow: 기존 CSS variable과 `.action-feedback[data-tone=…]` 스타일 재사용, 배치 보조 스타일만 추가. page-level overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: A1 화면과 badge 재조회 계약 불변. 저장 완료로 생성되는 내 업무/알림 흐름은 서버 소관으로 불변.
- 권한/관리자: 메뉴·permission 계약 불변. 관리자 화면 확대는 명시적 제외(비차단 결정으로 이월).
- Excel/PDF/첨부: workbook 내용·서버 export 계약 불변, 결과 UX만 변경.
- Teams/Mail: 영향 없음.
- 삭제·복구/감사: 영향 없음.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | A1 `useActionFeedback` 계약을 화면별로 채택하고 최소 확장(첫 invalid focus, `aria-describedby`, export 2단계 판정, 복귀 contextual 안내)만 추가 | 검증된 계약 재사용, 화면당 diff 최소, A1 회귀 위험 낮음 | 화면 수가 많아 vertical slice 순서 관리 필요 |
| B | 화면별 임시 message 개선(문구·색상만 보정) | 당장 diff 최소 | 문자열 판정·중복 submit·partial 미해결, 계약 분산 지속 — interview 결정 ①과 불일치 |
| C | 전역 feedback/toast store 도입 후 일괄 전환 | 장기 일관성 | 신규 아키텍처 위험, A1 결정(전역 toast 미도입)과 충돌, 범위 팽창 |

권장 구현 순서(interview 결정 ④ — mutation·bulk 위험 순서 vertical slice):

1. 구매: Excel preview/apply(다량 row 일괄 mutation, 최고 위험) + 구매 저장.
2. 패널: Excel preview/apply + 저장(목포장 size validation·중복 패널명 흐름 유지).
3. 생산계획: 저장·담당자/일정 field 오류 focus, 양식 다운로드, Excel dialog.
4. 자재: 도착 등록·입고 확정·키팅·IQC 판정(row/bulk conflict·preserve refresh).
5. 공통 선택 Excel export 2단계 판정 + 전 화면 회귀·screenshot.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic 검증과 local browser만 사용한다.
- migration 필요 여부: 없음. 필요성이 발견되면 구현을 중단하고 blocking decision으로 보고한다.
- 외부 발송/실제 데이터 영향: 없음.
- runtime 교체 여부: 없음. rollback은 Frontend 변경 revert로 충분하며 API 계약 보정이 필요해지면 중단한다.
- 추가 사용자 승인 필요 작업: push·PR·merge·Persistent UAT·대표 runtime 반영·`main` merge(분리 승인 3회 전 금지). fast-track은 experiment local commit까지만 승인한다.

## 14. 검증 계획

- 최소 테스트: Frontend lint·typecheck·unit·build. `useActionFeedback` 확장 unit, 대상 화면별 성공/오류/partial/중복 submit/conflict/첫 invalid focus/`aria-describedby`/generation guard mock UI test.
- 영향 영역 회귀: A1 내 업무·알림 feedback test, `SelectedExportTray`·`useSelectedRows` 선택 보존, 기존 Excel dialog flow test. 가능한 isolated Full-Stack E2E(action feedback, mobile-first, selected-export 관련 spec) — 실행 불가 시 이유와 함께 미실행으로 기록한다.
- PR/CI: `N/A` — push·PR 미승인 experiment local 범위.
- 사용자 검수: 페이지별 desktop·390px 대표 synthetic screenshot으로 `사용자 검수 대기 — 마지막 일괄 검수` 전환. 자동 검증과 사용자 검수 상태를 분리 기록한다.

## 15. 완료 기준

- 기능/권한/데이터: 대상 화면 모든 action이 scope 잠금·구조화 tone·conflict·partial 계약을 충족하고 Backend/API/DB/migration/provider diff 0.
- UX: 요약+field 오류·첫 invalid focus·`aria-describedby`·live announcement, action 인접/contextual placement, 선택·filter·context 보존, desktop·390px overflow 0.
- 자동 테스트: 14장 항목 통과. 미실행 항목은 이유와 함께 기록하고 성공으로 표기하지 않는다.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist의 상태·위치를 canonical 정책대로 추적한다.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- PR 상태: `N/A` — 게시 미승인.

## 16. 미결정 사항 (사용자 결정 필요, 모두 비차단)

| 번호 | 질문 | 선택지 | 권장안 | 사용자 결정 |
| ---: | --- | --- | --- | --- |
| 1 | 저장 성공 후 화면 이동 흐름의 성공 안내 위치 | ⓐ 기존 `onBack()` 이동 유지 + 복귀 화면 contextual 성공/partial 안내 ⓑ 저장 후 현재 화면 유지 + 인접 성공 표시 | ⓐ — 기존 업무 이동 흐름을 바꾸지 않으면서 결과 소실만 해소 | 대기 (standing rule 시 권장안 채택) |
| 2 | 전역 toast store 도입 재검토 | 도입 / 미도입 유지 | 미도입 유지 — A1 결정과 일관, A2도 화면 scope로 충분 | 대기 |
| 3 | 관리자·Pending 등 잔여 화면 전면 확대 | 후속 slice로 분리 / A2에 포함 | 후속 분리 — A2 위험 순서 범위 고정 | 대기 |

## 17. 예상 변경 범위 (확정 allowlist가 아니라 조사 대상)

- Backend: 없음.
- Frontend: `frontend/src/useActionFeedback.ts`(최소 확장), `frontend/src/App.tsx`의 생산계획/구매/자재 입고/패널 편집 page·4개 Excel dialog·`ActionFeedback`/`FormErrorSummary`/`FieldErrorMessage` 보강, `frontend/src/MaterialsWorkspace.tsx`, `frontend/src/PanelKittingPage.tsx`, `frontend/src/IqcReportWorkspace.tsx`, `frontend/src/ExcelExportAction.tsx`, `frontend/src/SelectedExcelExport.tsx`(회귀 중심), `frontend/src/styles.css` 배치 보조.
- DB/Migration: 없음.
- Tests/Scripts: `frontend/tests/App.test.tsx`, `frontend/tests/useActionFeedback.test.tsx`, 대상 workspace test, 관련 Full-Stack E2E spec.
- Docs: `tasks/ux-001-a2-*` 산출물, 구현 완료 후 Roadmap·실험 완료 원장 상태 갱신.

## 18. Roadmap 연결

- 선행 Task: `TASK-UX-001` A1(완료 slice, 재구현 금지), `TASK-EXPORT-001/002`, `TASK-HOME-002`(현재 branch 기준선).
- 후속 Task: 관리자/Pending 화면 확대(결정 3), `TASK-EXPORT-001` column picker 후속, 승격·통합·UAT Task.
- 현재 Go/No-Go: Roadmap 실행 큐 1순위 `TASK-UX-001 A2`·`Next Gate: FABLE_2_PASS_PLANNING`과 일치 — Go(구현은 2차 기획·blocking 0 확인 후).
- 별도 Task로 분리할 항목: 전역 design token(`DESIGN-000`), 기존 P3 backlog(chunk 분할 등)는 이번 범위에 포함하지 않는다.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-19 | experiment standing instruction — 인터뷰·중간 승인 생략, Fable 권장안 자동 채택, 대표 repo·`main`·Persistent UAT·provider·push·PR·merge 제외 | fast-track interview 자동 확정과 이 1차 기획 작성 |

## 20. Codex 구현 지시문 초안 (2차 기획·승인 후 사용)

1. `useActionFeedback` 최소 확장과 unit test(첫 invalid focus helper, `aria-describedby` id 계약, export 2단계 판정 지원)를 먼저 고정한다.
2. 12장 위험 순서대로 화면별 vertical slice를 진행한다: 각 slice에서 문자열 tone 판정 제거 → scope busy·conflict 선언 → row/contextual placement → field 오류 focus/announcement → preserve refresh + generation guard → mock UI test.
3. 저장 후 이동 흐름은 결정 1의 확정안을 따르고, 서버 계약 부족·migration 필요·문서-구현 충돌 발견 시 즉시 중단하고 blocking decision으로 보고한다.
4. 전 slice 완료 후 A1·export·모바일 회귀, desktop·390px synthetic screenshot, implementation report와 5종 산출물, experiment local commit까지만 수행한다.

## 21. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 3
