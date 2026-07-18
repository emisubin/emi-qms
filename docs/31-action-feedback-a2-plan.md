# TASK-UX-001 A2 — 업무 화면 Action Feedback 확대 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-UX-001-A2`
- authoringModel: `FABLE_5`
- canonicalTaskId: `TASK-UX-001`
- completedSliceExcluded: `A1`
- interviewSource: `tasks/ux-001-a2-interview.md`
- firstPlanningSource: `tasks/ux-001-a2-planning.md`
- codexReviewSource: `tasks/ux-001-a2-review.md`
- approvalChangeSource: `tasks/ux-001-a2-change-001.md`
- reviewVerdictConsumed: `KEEP_WITH_REQUIRED_RESOLUTIONS` (Frontend-only bounded adoption)
- requiredFindingResolutionCount: 6 / 6 반영

이 문서는 experiment fast-track 2차 기획이며 TASK-UX-001 A2의 최종 구현 source of truth다. 1차 기획과 Codex review는 판단 이력으로 보존하고 수정하지 않는다. 이 문서는 main merge, push, PR, Persistent UAT, 실제 provider 발송, 게시 승인을 부여하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/validation-matrix.md`, `docs/development/privacy-safe-evidence.md`를 따르고 이 문서에 복사하지 않는다.

## 1. 목표와 확정 기준선

### 1.1 한 줄 목표

생산계획·구매·자재·패널·선택 Excel 화면에서 사용자가 실행한 저장·preview/apply·검사·내보내기 action의 처리 중·성공·부분 성공·실패와 다음 행동을 action 인접 위치에서 즉시 확인하고, 첫 field 오류로 이동하며, 선택·필터·입력 context를 잃지 않고 안전하게 재시도할 수 있게 한다.

### 1.2 Interview·1차 기획에서 유지되는 확정 사항

- 대상은 A2 화면군뿐이다: 생산계획 수정(+Excel dialog·양식 다운로드), 구매 수정·Excel dialog, 자재 도착·입고 확정·키팅·IQC 판정, 패널 정보 수정(+Excel dialog), 공통 선택 Excel export. A1 내 업무·알림은 재구현하지 않고 회귀로만 보호한다.
- 신규 권한·역할·API 업무 능력·DB 개념·상태 전이·migration·provider 호출은 없다. Backend가 권한·validation(400 `ApiError.errors`)·충돌(409)·audit의 authoritative layer로 불변이다.
- A1 공통 계약을 재사용한다: `useActionFeedback`(ref 동기 busy fence, exact/prefix conflict, refresh boolean에 따른 `partial`, `actionErrorMessage` 403/404/409 guidance), `ActionFeedback`(role/`aria-live`/`focusOnAttention`), `idle → loading → success | error | partial` 상태 모델, 시간 기반 자동 소멸 없음, feedback 비영속.
- field 오류는 요약+field 병행 표시와 첫 invalid field focus로 처리한다.
- 선택 Excel 성공 기준은 server blob 수신까지다. client trigger 예외는 전체 실패가 아니라 구조화된 `partial`이다.
- 검증은 isolated synthetic 환경과 local browser만 사용하고, Git 범위는 experiment branch local commit까지다.

### 1.3 Review 반영 요약 (유지/추가/보류/제거)

- 유지: A1 계약 재사용, 대상 화면의 문자열 tone 판정(`successMessage`, `includes('했습니다')`) 제거, 동일 scope 중복 차단과 관련 conflict, mutation 성공+후속 refresh 실패의 `partial`, field 요약·첫 invalid focus·`aria-describedby`, Excel blob/trigger 단계 구분, desktop·390px synthetic 검증.
- 추가(이 문서 3~5장에 계약으로 통합): form layer의 `focusFirstFieldError`·`fieldErrorId` helper, 부모 dashboard one-shot contextual feedback callback, Excel 단계별 try/catch/finally·object URL revoke 보장·export 전용 422/429 guidance·rerender 전 double activation을 막는 ref fence, post-mutation preserve refresh 흐름에 한정한 generation guard, 요약 alert + field describedby로 중복 낭독 방지.
- 보류(이번 구현 금지): 관리자·Pending·설정 화면 전면 확대, 전역 toast/store, 모든 목록 query의 공통 cache/generation framework, form component 전면 교체, 선택 Excel column picker·multi-sheet·파일 내용 변경.
- 제거(계약에서 삭제): `useActionFeedback` 내부에 DOM focus helper를 넣는 방안, 모든 재조회에 generation guard를 일괄 추가하는 해석, 20개 선택 export 화면별 반복 screenshot, 구조상 동시에 실행될 수 없는 action까지 잠그는 인위적 cross-component lock framework. 1차 기획의 해당 표현(예: “editor 저장↔Excel apply 양방향 차단” 일반화, “목록/summary 재조회 전반의 generation guard”)은 이 계약의 4~5장 정의로 대체된다.

### 1.4 Review Finding resolution 매핑

| Finding | 이 계약의 resolution 위치 |
| --- | --- |
| `UX-A2-FOCUS-RESPONSIBILITY` (P1) | 3.3장 — focus/description helper는 form layer(App utility)에 두고 `useActionFeedback`은 수정하지 않거나 호출부 재사용에 그친다 |
| `UX-A2-RETURN-FEEDBACK-LOSS` (P1) | 3.5장 — editor 성공은 부모 one-shot contextual feedback callback으로 보존한다 |
| `UX-A2-EXPORT-STAGE-AMBIGUITY` (P1) | 3.4장 — blob/trigger 단계 try/catch/finally, URL revoke, export 전용 mapper, ref fence |
| `UX-A2-GENERATION-SCOPE` (P2) | 3.6장 — post-mutation preserve refresh가 실제로 있는 흐름에만 guard |
| `UX-A2-LIVE-DUPLICATION` (P2) | 3.3장 — 요약 단일 `role=alert` + field는 `aria-describedby` 기본, 화면당 contextual live region 1개 |
| `UX-A2-EVIDENCE-EXPANSION` (P2) | 7장 — 업무군별 대표 desktop/390px 증빙으로 한정 |

## 2. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 생산관리 담당 | 생산계획 저장, 담당자/일정 입력, 양식 다운로드, Excel preview/apply | 기존 project scope | 기존 `production-planning.update` 범위 |
| 구매 담당 | 구매 정보 저장, Excel preview/apply | 기존 project scope | 기존 구매 mutation 범위 |
| 자재·품질 담당 | 자재 도착·입고 확정·키팅·IQC 판정 | 기존 project scope | 기존 담당 mutation 범위 |
| 설계 담당 | 패널 정보 저장(목포장 size validation 유지), Excel preview/apply | 기존 project scope | 기존 패널 mutation 범위 |
| 조회·내보내기 사용자 | 선택 row Excel 내보내기 | 기존 조회 scope | export 생성만 |

HOME Change 002의 “전 부서 조회, 담당 부서만 입력” 불변조건을 유지한다. feedback 적용을 이유로 읽기 전용 사용자의 mutation control을 새로 활성화하지 않는다. 권한·validation·409 판정은 서버 응답이 최종 기준이며 Frontend busy 잠금은 UX 방어일 뿐이다.

## 3. 공통 Frontend 계약

### 3.1 상태 모델과 lifecycle

```text
idle → loading → success | error | partial → (같은 scope 다음 action 시작·명시적 닫기·수동 새로고침) → idle
```

- tone은 기존 `ActionFeedbackTone`을 재사용한다. 시간 기반 자동 소멸은 사용하지 않는다.
- feedback은 화면 session 상태이며 영구 저장하지 않는다. 대상 화면에서 `successMessage()`·`includes('했습니다')` 등 문자열 포함 tone 판정을 제거하고 try/catch와 `ApiError` 구조 판정으로 대체한다.

### 3.2 `useActionFeedback` 재사용과 scope naming

- hook의 책임을 늘리지 않는다. DOM 탐색·field focus를 hook에 추가하지 않는다(`UX-A2-FOCUS-RESPONSIBILITY`). 기존 `run`/`isBusy`/`hasBusyPrefix`/`feedbackFor`/`reset`과 `conflicts`(scopes/prefixes) 계약을 그대로 사용한다.
- A2 scope naming(prefix conflict 활용):
  - 생산계획: `production-plan:<projectId>:save`, `production-plan:<projectId>:template-download`, dialog는 `production-plan:<projectId>:excel-preview` / `:excel-apply`
  - 구매: `procurement:<projectId>:save`, dialog는 `procurement:excel:preview` / `procurement:excel:apply`
  - 자재: `material:<receiptItemId>:arrival` / `:confirm`, 키팅 `kitting:<panelId>`·`kitting:bulk`, IQC `iqc:<targetId>:decision`
  - 패널: `panel-info:<projectId>:save`, dialog는 `panel-info:<projectId>:excel-preview` / `:excel-apply`
- conflict는 실제로 동시에 실행 가능한 action에만 선언한다(review 제거 4). modal dialog가 열려 있어 부모 editor action이 구조상 실행될 수 없는 조합에는 인위적 lock을 만들지 않는다. 최소 확정 conflict:
  - 같은 dialog의 preview↔apply 양방향
  - 같은 editor button-row의 저장·다운로드·dialog 열기(같은 scope prefix로 동일 tick 중복 활성 차단)
  - 자재·키팅의 bulk↔row 양방향(exact/prefix)

### 3.3 field 오류·접근성 계약 (form layer)

- `fieldErrorId(field)`: normalized field path에서 파생한 안정적 DOM id를 반환하는 App utility. dynamic row field(`items[3].plannedDate` 등)도 안정 id를 갖는다.
- `FieldErrorMessage`는 `id={fieldErrorId(field)}`를 갖는 설명 텍스트로 확장하고, 오류가 있는 input/select/textarea에만 `aria-describedby={fieldErrorId(field)}`와 `aria-invalid`를 명시적으로 연결한다. field 단위 `role=alert` 반복 낭독을 만들지 않는다(`UX-A2-LIVE-DUPLICATION`).
- `FormErrorSummary`는 화면당 단일 `role=alert` 요약으로 유지한다(클릭 시 기존 `focusField` 이동 보존).
- `focusFirstFieldError(errors, orderedFields?)`: form layer helper. client validation 실패와 서버 400 `fieldErrorsFromApiError` 매핑 직후 호출해 DOM 순서 기준 첫 invalid control로 focus·scroll한다. 기존 `focusField` selector 계약(`[name]`/`[data-field]`, CSS.escape)을 재사용한다.
- action 결과 live region은 화면당 1개의 `ActionFeedback` 기반 contextual region으로 제한한다. error는 `role=alert`/assertive, 그 외 `role=status`/polite. raw status code·내부 enum·stack trace는 사용자 문구에 노출하지 않는다(`sanitizeUserMessage` 계약 유지).

### 3.4 선택 Excel export 단계 계약 (`ExcelExportAction`)

`UX-A2-EXPORT-STAGE-AMBIGUITY` resolution으로 다음을 확정한다.

- 단계 1 — server export 호출: 실패 시 export 전용 mapper로 error tone. 기존 422(`unprocessableEntityHint` 결합)·429(잠시 후 재시도) 특화 문구를 보존하고, 그 외는 A1 `actionErrorMessage` 계열 guidance로 정렬한다.
- 단계 2 — client download trigger: blob 수신 후 object URL 생성·anchor click을 별도 try/catch로 감싼다. 실패는 `partial`(파일 생성은 완료 + 다시 시도 안내)이며 전체 실패로 표시하지 않는다. object URL revoke는 `finally`로 보장한다.
- 성공: 기존 성공 문구(0건 파일 안내 포함)를 유지하되 tone을 4-tone 구조(`success | error | partial` + 진행 중)로 정렬하고 `aria-live=polite` 표시를 유지한다.
- double activation: React state rerender 전 재클릭도 막히도록 component-local ref fence(A1 busy ref 패턴과 동등)를 추가한다. `onBusyChange`·`disabled`·`disabledReason`·`scopeLabel` 등 기존 props 계약과 `SelectedExportTray`/`useSelectedRows` 선택·busy 계약은 호환 유지한다.
- 이 component 한 번의 변경으로 기존 선택 export 소비 화면 전체에 전파되므로 server 계약(row cap·server-side selection·권한 재검증)은 일절 변경하지 않는다.

### 3.5 저장 성공 후 화면 이동: 부모 one-shot contextual feedback

`UX-A2-RETURN-FEEDBACK-LOSS` resolution으로 다음을 확정한다.

- 기존 `onBack()` 이동 흐름과 mutation payload·navigation 구조를 보존한다(1차 기획 결정 1의 권장안 ⓐ 확정).
- editor는 성공 시 bare `onBack()` 대신 `onSaved(feedback)` 또는 동등한 one-shot callback으로 대상 label(프로젝트 제목 등)을 포함한 성공 사실을 부모에 전달한 뒤 이동한다.
- 부모 화면은 이 one-shot feedback을 자신의 contextual `ActionFeedback` region에 표시한다. 복귀 후 부모 재조회가 성공하면 `success`, 실패하면 mutation을 실패로 되돌리지 않고 `partial`(처리 완료 + `새로고침` 안내)로 표시한다.
- one-shot feedback은 부모의 다음 action 시작·수동 새로고침 시 reset되며, 전역 toast/store·session storage·route 전역 상태를 도입하지 않는다.
- Excel dialog의 `onApplied` → 부모 reload 흐름도 같은 규칙을 따른다: apply 성공 후 reload 실패는 `partial`이다.

### 3.6 목록 refresh·generation guard 경계

`UX-A2-GENERATION-SCOPE` resolution으로 guard 적용 범위를 다음으로 한정한다.

- 적용 대상: 변경 action 뒤 기존 ready 목록·선택을 유지한 채 재조회(preserve refresh)하는 흐름 — 자재 workspace·IQC 목록·키팅 목록의 post-action 재조회, apply 후 remount 없이 reload하는 부모 dashboard.
- 적용 제외: 최초 mount 조회, route 전환으로 remount되는 화면의 load, 변경 action과 무관한 목록. 모든 목록 query의 공통 framework를 만들지 않는다.
- preserve refresh는 실패해도 목록을 error state로 덮지 않고 action feedback의 `partial`로만 표현한다. 실패·partial에서 기존 ready data·선택·filter를 보존하며, 성공 refresh의 visible ID 축소만 기존 `useSelectedRows` 교집합 계약대로 처리한다.

### 3.7 Placement 이중 규칙과 모바일

- loading·해당 row/control이 살아 있는 error: action 인접(row/card/button-row 옆) 표시.
- 화면 이동·행 제거로 맥락이 사라질 수 있는 success/partial: 대상 label을 포함해 화면당 1개 contextual region에 보존.
- dialog 내부 action(preview/apply)은 dialog의 action bar 인접에 표시한다.
- 대상 화면 하단의 기존 단일 `message` 문단과 중복 banner는 제거하거나 contextual region 하나로 통합한다.
- 모바일 390px·Teams narrow: action과 feedback을 기존 adaptive card/section 안에 배치하고 page-level horizontal overflow 0을 유지한다. 기존 CSS variable과 `.action-feedback[data-tone=…]` 스타일을 재사용하고 배치 보조 스타일만 추가한다.

## 4. 화면별 확정 UX 계약

### 4.1 생산계획 수정(+Excel dialog)

- `저장`: `production-plan:<projectId>:save` busy·`저장 중` label. client 검증 실패 시 요약+field 오류·`focusFirstFieldError`. 서버 400은 `fieldErrorsFromApiError` 매핑 후 동일 처리. 403/404/409는 A1 guidance. 성공은 3.5 one-shot callback으로 부모 contextual 성공/partial.
- `Excel 양식 다운로드`: blob 수신 성공 기준, client trigger 실패는 `partial`. Item 미확정 등 사전 조건 안내는 error가 아닌 기존 안내 문구를 contextual region에서 neutral/error 구조로 표시.
- dialog `Preview`/`저장 가능한 항목 적용`: preview↔apply 양방향 conflict, 오류는 dialog action bar 인접 error, apply 성공 후 부모 reload 실패는 `partial`. `reasonRequired`·`applyDisabledReason` 등 기존 업무 규칙 표시는 불변.

### 4.2 구매 수정·Excel dialog

- `저장`: `procurement:<projectId>:save` — 생산계획과 동일한 field 오류·guidance·one-shot 계약.
- dialog: `procurement:excel:preview` / `:apply` 양방향 conflict, 파일 미선택 등 사전 조건 안내, apply 성공+reload 실패 `partial`. 프로젝트 매칭 selection·저장 가능/불가 목록 등 기존 preview 표시는 불변.

### 4.3 자재 도착·입고 확정·키팅·IQC

- 도착 등록·입고 확정: row action scope busy, action 인접 구조화 tone, `includes('했습니다')` 판정 제거.
- 키팅: bulk↔row 양방향 conflict(`kitting:bulk` ↔ `kitting:<panelId>` prefix), 부분/일괄 처리 결과를 action 인접·contextual 이중 규칙으로 표시.
- IQC 판정: 판정 사유 등 client 검증 실패는 field 인접 오류·focus, 저장 성공(합격/부적합 Pending 등록)은 기존 문구를 구조화 tone으로 표시. 부적합 Pending 생성 등 업무 흐름·서버 계약은 불변.
- 목록 재조회는 3.6 preserve refresh + generation guard를 적용한다.

### 4.4 패널 정보 수정(+Excel dialog)

- `저장`: `panel-info:<projectId>:save` — 목포장 size validation·중복 패널명 확인 흐름 등 기존 업무 규칙을 유지한 채 field 오류 연결·첫 invalid focus·one-shot 성공/partial만 추가.
- dialog preview/apply: 4.1과 동일 계약.

### 4.5 공통 선택 Excel export

- 3.4 단계 계약을 `ExcelExportAction` 한 곳에서 구현하고 `SelectedExportTray` 소비 화면은 자동 전파·회귀로 검증한다. 선택·filter 보존과 `onBusyChange` 계약 불변. 20개 화면 registry는 unit/회귀로 확인하고 화면별 재설계는 하지 않는다.

## 5. 업무 규칙과 불변조건

- Backend 권한·validation·업무 상태 전이·audit·export contract(server-side selection·권한 재검증·row cap)·수정사유(reason) 입력 계약은 변경하지 않는다.
- 같은 scope 진행 중 중복 submit은 동일 tick 포함 차단되고, 3.2에 선언된 실제 동시 가능 conflict만 양방향 차단된다.
- mutation 성공은 이후 refresh/download trigger 실패로 실패 표시되지 않는다(`partial` 구분). 서버 rollback 정책은 변경하지 않는다.
- 3.6 적용 범위의 과거 재조회 응답이 최신 화면을 덮지 않는다.
- 실패·partial에서 입력값·선택·filter·스크롤 context를 잃지 않는다.
- 읽기 전용 사용자의 mutation control을 새로 활성화하지 않는다.
- A1 `/my-work`·`/notifications`, 알림 preference 화면, 기존 `ActionFeedback` call site, `SelectedExportTray`/`useSelectedRows` 계약은 동작 호환을 유지하며 회귀로만 검증한다.
- `App.tsx` 규모(P3)는 이번 A2에서 구조 분할 사유로 사용하지 않는다.

## 6. 변경 범위

### 포함 (changed-file 조사 대상)

- `frontend/src/App.tsx` — 생산계획/구매/자재 입고/패널 편집 page와 4개 Excel dialog의 feedback 전환, `FieldErrorMessage`/`FormErrorSummary` id·describedby 보강, `fieldErrorId`·`focusFirstFieldError` utility, one-shot callback 배선
- `frontend/src/ExcelExportAction.tsx` — 단계 계약·ref fence·export mapper
- `frontend/src/MaterialsWorkspace.tsx`, `frontend/src/PanelKittingPage.tsx`, `frontend/src/IqcReportWorkspace.tsx` — 대상 action 전환과 preserve refresh guard
- `frontend/src/SelectedExcelExport.tsx` — 회귀 중심(필요 시 props 전달 최소 변경)
- `frontend/src/useActionFeedback.ts` — 원칙적으로 무변경. 불가피한 비파괴 확장만 허용하며 기존 A1 test 계약을 깨지 않는다
- `frontend/src/styles.css` — contextual/배치 보조·partial tone 스타일 최소 추가
- `frontend/tests/App.test.tsx`, `frontend/tests/useActionFeedback.test.tsx`(회귀)와 신규 helper/화면 unit test, 관련 isolated Full-Stack E2E spec
- `tasks/ux-001-a2-*` 산출물과 구현 완료 후 Roadmap·실험 완료 원장 상태 갱신

### 제외 (이번 구현 금지)

- A1 두 화면 재구현·계약 변경, 관리자·Pending·설정 화면 전면 확대, 기존 문자열 tone call site 중 A2 대상 외 화면 정리
- 전역 toast/store, 전체 `StateMessage` 재설계, 공통 query cache/generation framework, form component 전면 교체
- Backend/API/DB/migration/신규 endpoint/신규 notification event/실제 provider 호출, Excel column picker·multi-sheet·row cap·workbook 내용 변경
- 대표 repo·`main`·push·PR·merge·Persistent UAT 변경

## 7. 검증 계획과 증빙 경계

- hook/export helper unit: 동일 tick double submit 차단, exact/prefix conflict, 422/429 guidance 보존, blob 성공+trigger 실패 `partial`, object URL revoke.
- form unit: client/서버 오류 매핑, 첫 invalid focus, dynamic row field의 안정 `fieldErrorId`·`aria-describedby`, 요약 단일 alert.
- 화면 unit/mock UI: 저장 성공 → 부모 one-shot contextual feedback, error 시 입력·context 보존, apply 성공+reload 실패 `partial`, preview↔apply·bulk↔row conflict, preserve refresh generation guard, 읽기 전용 사용자 mutation 비활성 유지.
- 회귀: A1 `/my-work`·`/notifications` feedback test, 선택 export screen registry, 기존 mock UI 전체(`corepack pnpm --dir frontend run lint` / `run typecheck` / `test` / `run build`).
- isolated Full-Stack E2E: action-feedback·mobile-first·selected-export 관련 영향 spec. 실행 불가 시 이유와 함께 미실행으로 기록하고 성공으로 표기하지 않는다.
- browser 증빙(`UX-A2-EVIDENCE-EXPANSION` resolution): 생산계획·구매·자재·패널·선택 export 5개 업무군의 대표 상태(loading/success/error/partial·field 오류)를 desktop·390px synthetic data로만 촬영한다. 20개 export 화면별 반복 screenshot은 만들지 않는다. page-level overflow 0을 assertion으로 확인한다.
- Backend/API/DB/migration/provider diff 0을 확인한다.

## 8. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic E2E와 local browser만 사용한다.
- migration: 없음. 필요성이 발견되면 구현을 중단하고 blocking decision으로 보고한다.
- 외부 발송·실제 데이터 영향: 없음. 개인정보 없는 synthetic data만 증빙에 사용한다.
- runtime 교체: 없음. rollback은 Frontend 변경 revert로 충분하다. API 계약 보정 필요가 발견되면 중단한다.
- Git: experiment branch local commit까지만 승인. push·PR·merge·대표 repo·`main` 미승인 유지, `main` merge는 분리된 승인 3회 전 금지(`0/3`).

중단 조건: 기존 API 오류 계약이 field 연결·guidance 구분에 부족한 경우, migration·신규 능력 필요 발견, 문서·구현의 의미 있는 충돌 발견 — 임의 진행하지 않고 blocking decision으로 보고한다.

## 9. 구현 순서 (Codex 구현 계약)

1. 공통 최소 기반: `ExcelExportAction` 단계 계약·ref fence·export mapper, `fieldErrorId`·`focusFirstFieldError`와 unit test.
2. 구매·패널: Excel dialog preview/apply conflict·partial + editor 저장·dynamic field 오류·one-shot callback.
3. 생산계획: editor 저장·양식 다운로드·dialog와 부모 return feedback.
4. 자재: 도착·입고 확정·키팅·IQC — 실제 동시 가능한 row/bulk scope와 preserve refresh guard만 적용.
5. 공통 선택 Excel: component 전파 확인, 대표 소비 화면과 기존 20개 screen registry 회귀.
6. A1 두 화면 회귀, desktop·390px 대표 browser 증빙, 전체 Frontend validation, implementation report와 5종 산출물 상태 기록, experiment local commit.

각 vertical slice는 현재 mutation 권한·API payload·onBack navigation 구조를 보존한다.

## 10. 완료 기준

- 기능: 대상 화면 모든 action이 scope 잠금·구조 판정 tone·이중 placement·field 오류 focus/describedby·partial 계약을 충족하고, 6개 review Finding resolution이 코드와 테스트로 확인된다.
- 불변 확인: Backend/API/DB/migration/provider diff 0, A1·export·기존 화면 회귀 통과, desktop·390px overflow 0.
- 사용자 검수: 업무군별 대표 screenshot으로 `사용자 검수 대기 — 마지막 일괄 검수` 전환. 자동 검증 완료와 사용자 검수 완료를 분리해 기록한다.
- 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist의 상태·위치를 canonical 종료 정책 기준으로 추적한다.

## 11. Deferred 비차단 결정 (구현 비차단)

| 번호 | 항목 | 처리 |
| ---: | --- | --- |
| 1 | 저장 성공 후 안내 위치 | standing instruction과 review resolution에 따라 ⓐ(`onBack()` 유지 + 부모 one-shot contextual 안내)로 이 계약에서 확정 |
| 2 | 전역 toast store | A2에서도 미도입 확정. 재검토는 이후 사용자 요청 시 별도 결정 |
| 3 | 관리자·Pending 등 잔여 화면 확대 | A2 범위에서 제외하고 후속 slice/Task로 분리. 사용자 요청 시 별도 planning |

세 항목 모두 interview에서 명시적으로 deferred된 비차단 결정이며, 1번은 fast-track standing instruction이 허용하는 권장안 채택으로 Repository 근거(현행 `onBack()` 이동 계약 보존)와 trade-off를 남기고 확정했다. 2차 기획 뒤 추가 Fable revise·review loop는 만들지 않는다.

openBlockingDecisionCount: 0
