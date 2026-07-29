# TASK-UX-001 A1 — Action Feedback UX 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-UX-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/ux-001-interview.md`
- firstPlanningSource: `tasks/ux-001-planning.md`
- codexReviewSource: `tasks/ux-001-review.md`
- approvalChangeSource: `tasks/ux-001-change-001.md`
- reviewVerdictConsumed: `KEEP_WITH_REQUIRED_RESOLUTIONS`
- requiredFindingResolutionCount: 7 / 7 반영

이 문서는 experiment fast-track 2차 기획이며 TASK-UX-001 A1의 최종 구현 source of truth다. 1차 기획과 Codex review는 판단 이력으로 보존하고 수정하지 않는다. 이 문서는 main merge, push, PR, Persistent UAT, 실제 provider 발송, 게시 승인을 부여하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/validation-matrix.md`, `docs/development/privacy-safe-evidence.md`를 따르고 이 문서에 복사하지 않는다.

## 1. 목표와 확정 기준선

### 1.1 한 줄 목표

내 업무(`/my-work`)·알림(`/notifications`) 화면에서 사용자가 실행한 action의 처리 중·성공·실패·부분 성공과 다음 행동을 action 맥락 안에서 즉시 확인하고, 같은 요청을 중복 제출하지 않고 안전하게 재시도할 수 있게 한다.

### 1.2 Interview·1차 기획에서 유지되는 확정 사항

- 대상은 A1 두 화면뿐이다: 내 업무의 `이동/시작`·`작업 완료`, 알림의 개별 `읽음`·`전체 읽음`.
- 신규 권한·역할·API 업무 능력·DB 개념·migration·provider 호출은 없다. Backend는 권한·업무 전이·읽음 기록·audit의 authoritative layer로 불변이다.
- 구조: scope key 기반 공통 hook + 기존 `ActionFeedback` component 확장(권장안 ①).
- mutation 성공과 refresh 실패를 `partial`로 구분한다(권장안 ②).
- 동일 행/scope 잠금 + bulk action 관련 scope 잠금, 서로 다른 행의 제한적 병렬 허용(권장안 ③).
- 성공·오류 tone은 문자열 포함 검사가 아니라 try/catch와 `ApiError.status` 구조 판정으로 정한다.
- feedback은 화면 session 상태이며 영구 저장하지 않는다.
- 검증은 isolated synthetic 환경과 local browser만 사용하고, 이 Task의 Git 범위는 experiment branch local commit까지다.

### 1.3 Review 반영 요약 (유지/추가/보류/제거)

- 유지: 두 화면 한정 A1, hook+component 구조, 구조화 tone, 중복 submit 차단, 제한적 병렬, 접근성 focus/announcement.
- 추가(이 문서 3~5장에 계약으로 통합): contextual summary feedback, stale-while-refresh, request generation guard, bulk/row 양방향 conflict, `ApiError` 기반 guidance mapper, 명시적 feedback lifecycle.
- 보류(이번 구현 금지): A2 전면 확대, `StateMessage` 전역 재설계, Pending·관리자 등 기존 문자열 tone call site 정리, 전역 toast store.
- 제거(계약에서 삭제): 성공 feedback의 시간 기반 자동 소멸, 두 list 화면의 인위적 target-not-found 상태 추가. 1차 기획의 해당 선택 요구사항은 이 계약에서 무효다.

## 2. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 업무 담당 사용자 | 내 업무 시작·완료·업무 화면 이동 | 기존 본인 업무 scope | 기존 허용 action만 |
| 알림 수신 사용자 | 개별 읽음·전체 읽음·상세/업무 이동 | 기존 본인 알림 scope | 기존 읽음 상태만 |

권한·validation·409 충돌 판정은 서버 응답을 최종 기준으로 하며, Frontend busy 잠금은 UX 방어일 뿐 서버 idempotent 동작을 대체하지 않는다.

## 3. 공통 Frontend 계약

### 3.1 상태 모델

```text
idle → loading → success | error | partial → (다음 action 실행 또는 명시적 닫기/새로고침) → idle
```

- tone은 기존 `ActionFeedbackTone`(`neutral | loading | success | error | partial`)을 재사용한다.
- feedback lifecycle: 시간 기반 자동 소멸을 사용하지 않는다. success/partial은 같은 scope의 다음 action 시작, 사용자 명시적 닫기 또는 manual 새로고침까지 유지한다. error는 동일 action 재시도 시작까지 유지한다. (`UX-A1-ROW-FEEDBACK-DISAPPEARS`·review 제거 항목 resolution의 일부)

### 3.2 신규 hook: `useActionFeedback` (파일 `frontend/src/useActionFeedback.ts`)

`useSelectedRows.ts`와 같은 최상위 단일 파일 패턴으로 추가한다. 계약:

- busy state: scope key(`work:<workItemId>`, `notification:<notificationId>`, `notifications:all`)별 진행 중 집합. 같은 scope가 진행 중이면 `run` 재호출을 무시한다.
- conflict 규칙: caller가 conflict scope 집합을 선언할 수 있다. `notifications:all` 진행 중에는 모든 `notification:<id>`가 차단되고, 임의의 `notification:<id>` 진행 중에는 `notifications:all`이 차단된다(양방향, `UX-A1-BULK-ROW-CONFLICT` resolution).
- `run(scope, mutationFn, { successMessage, guidance, refresh })` 실행 순서: ① scope/conflict 진행 중이면 무시 → ② 해당 scope feedback을 loading으로 설정 → ③ `mutationFn` try/catch → ④ 실패면 3.4의 mapper로 error tone·다음 행동 산출 → ⑤ 성공이고 `refresh`가 있으면 refresh 결과를 boolean으로 받아 실패 시 `partial`, 성공 시 `success`로 확정 → ⑥ error/partial이면 지정된 stable feedback anchor로 focus 이동, success는 focus를 강제하지 않는다(`UX-A1-FOCUS-ANCHOR`).
- `reset(scope)`과 전체 reset을 제공한다. feedback state는 scope별 `{ tone, message }` 구조이며 화면 밖으로 영속되지 않는다.
- 이 hook은 mutation 성공 사실을 refresh 실패로 격하하지 않는다(`UX-A1-REFRESH-FIRE-AND-FORGET`의 hook 측 절반).

### 3.3 `ActionFeedback` component 확장 (App.tsx)

- 기존 props(`message`, `tone`)와 기존 call site 호환을 유지한다.
- stable focus target을 추가한다: `tabIndex={-1}` + 전달 가능한 ref(또는 동등한 focus 위임). role 계약 유지 — error는 `role="alert"`, 그 외 `role="status"`(암시적 live region).
- 오류/partial focus는 row가 제거·rerender되어도 살아 있는 contextual anchor를 대상으로 한다. row 내부 요소로의 focus는 해당 row가 화면에 남아 있는 경우에만 사용한다(`UX-A1-FOCUS-ANCHOR` resolution).

### 3.4 오류 tone·다음 행동 mapper

문자열 포함 판정을 두 화면에서 제거하고 다음 구조 판정으로 대체한다(`UX-A1-TONE-AND-GUIDANCE` resolution). 기존 `frontend/src/api.ts`의 `ApiError`(`status`, `message`, `errors`)를 재사용한다.

| 판정 입력 | tone | 다음 행동 안내(한글) |
| --- | --- | --- |
| `ApiError.status === 403` | error | 권한이 없음을 알리고 담당자/관리자 확인을 안내 |
| `ApiError.status === 404` | error | 대상이 이미 처리되었거나 없음을 알리고 `새로고침` 안내 |
| `ApiError.status === 409` | error | 이미 처리 중이거나 상태가 바뀌었음을 알리고 `새로고침` 후 재확인 안내 |
| 그 외 `ApiError` | error | 서버 메시지가 안전한 한글이면 원인으로 보존하고 재시도 안내 |
| 비 `ApiError`(network 등) | error | 연결 문제를 알리고 재시도 안내 |
| mutation 성공 + refresh 실패 | partial | 처리 완료 사실 + 최신 목록 미갱신 + `새로고침` 안내 |

raw status code, stack trace, 내부 enum은 사용자 문구에 노출하지 않는다.

### 3.5 목록 refresh 계약 (두 화면 공통)

- `load` 결과 반환: 각 화면의 `load`는 성공/실패를 호출자에게 반환하도록 변경한다(`UX-A1-REFRESH-FIRE-AND-FORGET` resolution). 최초 load·manual 새로고침·tab 전환은 기존처럼 loading state로 진입한다.
- stale-while-refresh: post-mutation refresh는 기존 `ready` 목록을 유지한 채 수행하고, 실패해도 목록을 error state로 덮지 않는다. 실패는 action feedback의 `partial`로만 표현한다.
- request generation guard: 화면별 단조 증가 generation(또는 동등한 stale-response guard)을 두고, 마지막으로 시작된 refresh의 응답만 state를 갱신한다. 과거 응답 도착은 무시한다(`UX-A1-REFRESH-RESPONSE-RACE` resolution).
- selection 보존: 실패·partial refresh에서는 기존 ready data와 현재 선택을 보존한다. 성공 refresh로 visible ID가 줄어든 경우에만 기존 `useSelectedRows`의 visible 교집합 계약대로 사라진 ID가 정리된다(`UX-A1-SELECTION-REGRESSION` resolution).
- badge 재조회(`onBadgeRefresh`) 호출 시점은 기존과 동일하게 유지한다.

### 3.6 Feedback placement 이중 규칙

`UX-A1-ROW-FEEDBACK-DISAPPEARS` P1 resolution으로, placement를 다음과 같이 확정한다.

- loading·해당 row가 살아 있는 error: row/card 인접 표시.
- success·partial(성공한 행이 active tab 조건에서 제거될 수 있는 action): 항목 식별 label(업무 제목 또는 알림 제목)을 포함해 page contextual region(목록 상단의 feedback 영역)에 보존한다.
- `전체 읽음`: header action group 인접 region을 사용한다.
- contextual region은 화면당 1개의 `ActionFeedback` 기반 영역으로 하며 전역 toast가 아니다.

## 4. 화면별 확정 UX 계약

### 4.1 내 업무 `/my-work`

- `이동/시작`: 해당 행만 busy(`work:<id>`), 진행 중 label(현행 `이동 중` 유지 가능). 시작 mutation 실패는 행 인접 error + focus. 시작 성공 뒤에는 화면 이동이 일어나므로 별도 성공 banner를 강제하지 않고, 시작 성공·화면 이동 실패를 mutation 실패로 표현하지 않는다.
- `작업 완료`: 해당 행 action 전체 잠금 + `완료 처리 중` label. 오류는 행 인접 error + focus. 성공은 `Requested`/`InProgress` tab에서 행이 사라질 수 있으므로 contextual region에 업무 제목 포함 성공 안내를 보존한다.
- post-mutation refresh 실패: 기존 목록·tab·선택을 보존하고 contextual `partial` + `새로고침` 안내.
- 서로 다른 행의 `작업 완료`·`이동/시작`은 병렬 허용하되 3.5 generation guard로 목록 정합을 보장한다.
- 상단 기존 `message` 문자열 영역과 `includes('실패')` 판정은 제거한다.

### 4.2 알림 `/notifications`

- 개별 `읽음`: 해당 행 busy(`notification:<id>`) + 진행 중 label. 오류는 행 인접 error + focus. 성공/partial은 `unread` tab에서 행이 제거되므로 알림 제목을 포함해 contextual region에 보존한다.
- `전체 읽음`: header action group 인접 feedback(`notifications:all`). 진행 중에는 자신과 모든 개별 `읽음`을 차단하고, 개별 `읽음` 진행 중에는 `전체 읽음`을 차단한다(양방향).
- `상세`·`이동`은 mutation scope가 아니므로 읽음 busy로 차단하지 않는다. `새로고침`은 manual load로 항상 허용한다. 같은 button row의 disabled label과 focus order를 유지해 accidental double activation을 방지한다.
- 상단 기존 `message` 문자열 영역과 문자열 판정은 제거한다.

### 4.3 공통 상태·접근성·모바일

- 목록 조회는 기존 `LoadState<T>`/`toLoadError`/`StateMessage` 패턴을 유지하고, 두 화면의 retry/empty/error 안내 문구만 최소 보강한다. `StateMessage` 전역 재설계와 다른 화면 계약 변경은 금지(보류). 인위적 target-not-found 상태 추가도 금지(제거).
- keyboard 접근, label/role, focus order, screen-reader announcement를 회귀 유지한다.
- desktop과 390px·Teams narrow pane에서 page-level horizontal overflow 0. 모바일 card 내부에 feedback이 들어가며 PC 축소판 layout을 만들지 않는다. 기존 CSS variable과 `.action-feedback[data-tone=…]` 스타일을 재사용하고 배치 보조 스타일만 추가한다.

## 5. 업무 규칙과 불변조건

- Backend 권한·업무 상태 전이·읽음 기록·audit·인앱 알림 원본·deep link는 변경하지 않는다.
- 같은 scope의 진행 중 중복 submit은 차단하고, bulk/row conflict는 양방향으로 차단한다.
- mutation 성공은 이후 refresh 실패로 실패 표시되지 않는다.
- 과거 refresh 응답이 최신 목록을 덮지 않는다.
- 실패·partial에서 사용자의 tab·selection·화면 context를 잃지 않는다.
- 기존 `SelectedExportTray`/`useSelectedRows` busy 계약, 기존 `ActionFeedback` call site, `/teams/activity` 화면과 선택 Excel export는 동작 호환을 유지하며 회귀로만 검증한다.

## 6. 변경 범위

### 포함 (changed-file 조사 대상)

- `frontend/src/useActionFeedback.ts` (신규 hook)
- `frontend/src/App.tsx` — `MyWorkPage`, `NotificationsPage`, `ActionFeedback` 확장, 두 화면의 `load` 결과 반환·stale guard
- `frontend/src/styles.css` — contextual region·배치 보조 스타일 최소 추가
- `frontend/tests/App.test.tsx`와 신규 hook unit test
- 회귀 보장에 필요한 최소 범위의 관련 Full-Stack E2E spec
- `tasks/ux-001-*` 산출물과 구현 완료 후 Roadmap TASK-UX-001 상태 갱신

### 제외 (이번 구현 금지)

- A2 생산계획·구매·자재·패널·Excel 화면 확대, Pending·관리자 등 기존 문자열 tone call site 정리
- `StateMessage` 전역 재설계, 전역 toast store, 성공 자동 소멸
- Backend/API/DB/migration/신규 endpoint/신규 notification event/실제 provider 호출
- 대표 repo·`main`·push·PR·merge·Persistent UAT 변경

## 7. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic E2E와 local browser만 사용한다.
- migration: 없음. 필요성이 발견되면 구현을 중단하고 blocking decision으로 보고한다.
- 외부 발송·실제 데이터 영향: 없음.
- runtime 교체: 없음. rollback은 Frontend 변경 되돌림으로 충분하다.
- Git: experiment branch local commit만 승인(`commitApproved: true`). push·PR·merge·main은 미승인 상태를 유지하며, main merge는 분리된 승인 3회 전 금지다.

중단 조건: 기존 API 오류 계약이 4장 안내 구분에 부족해 Backend 보정이 필요한 경우, migration·신규 능력 필요 발견, 문서·구현의 의미 있는 충돌 발견 — 임의 진행하지 않고 blocking decision으로 보고한다.

## 8. 구현 순서 (Codex 구현 계약)

1. `useActionFeedback` hook: busy/conflict/run·refresh 결과 contract와 unit test(중복 submit 무시, bulk/row 양방향 차단, success/error/partial 판정, reset, focus 위임).
2. `ActionFeedback` stable focus target 확장과 기존 call site 호환 확인.
3. 내 업무: `load` 결과 반환·stale-while-refresh·generation guard → `작업 완료`/`이동·시작` action 전환 → row+contextual placement와 문자열 판정 제거.
4. 알림: `load` 결과 반환·stale guard → 개별/전체 읽음 conflict → header/row/contextual placement와 문자열 판정 제거.
5. `frontend/tests/App.test.tsx`: success/error/partial, 행 제거 뒤 contextual feedback 잔존, tab·selection 보존, 병렬 refresh race, bulk/row conflict 검증 추가.
6. Frontend 최소 검증(`corepack pnpm --dir frontend run lint`/`run typecheck`/`test`/`run build`)과 관련 isolated Full-Stack E2E 회귀(`mobile-first-experience`, `all-pages-selected-export` 포함 영향 spec).
7. desktop·390px의 정상/진행 중/오류/partial synthetic screenshot 2페이지 확보, implementation report와 5종 산출물 상태 기록, experiment local commit.

## 9. 검증과 완료 기준

- 자동 검증: 8장 6항 전부 통과. 미실행 항목은 이유와 함께 기록하고 성공으로 표기하지 않는다.
- 기능 완료: 두 화면 모든 대상 action이 scope 잠금·구조 판정 tone·이중 placement·focus 계약을 충족하고, P1 3건(`UX-A1-ROW-FEEDBACK-DISAPPEARS`, `UX-A1-REFRESH-FIRE-AND-FORGET`, `UX-A1-REFRESH-RESPONSE-RACE`)과 P2 4건(`UX-A1-BULK-ROW-CONFLICT`, `UX-A1-TONE-AND-GUIDANCE`, `UX-A1-FOCUS-ANCHOR`, `UX-A1-SELECTION-REGRESSION`)의 resolution이 코드와 테스트로 확인된다.
- 불변 확인: Backend/API/DB/migration/provider diff 0, 기존 화면 회귀 통과, 390px overflow 0.
- 사용자 검수: 페이지별 screenshot으로 `사용자 검수 대기` 상태 전환. 자동 검증 완료와 사용자 검수 완료를 구분해 기록한다.
- 산출물: implementation report 포함 5종 산출물 상태·위치를 canonical 종료 정책 기준으로 추적한다.

## 10. Deferred 비차단 결정 (구현 비차단)

| 번호 | 항목 | 처리 |
| ---: | --- | --- |
| 1 | A2 대상 화면별 확대 순서 | A1 검수 후 같은 canonical Task의 다음 change에서 사용자 결정 |
| 2 | 전역 toast store 필요성 | A1에서는 도입하지 않음 확정, 재검토는 A2 시점 사용자 결정 |

두 항목 모두 interview에서 명시적으로 deferred된 비차단 결정이며 이 계약의 구현을 차단하지 않는다. 추가 Fable revise·review loop는 만들지 않는다.

openBlockingDecisionCount: 0
