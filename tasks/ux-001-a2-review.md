# TASK-UX-001 A2 — Codex 내용·제품 방향 Review

- reviewTarget: `tasks/ux-001-a2-planning.md`
- reviewOwner: `CODEX`
- reviewRound: `1/1`
- canonicalTaskId: `TASK-UX-001`
- completedSliceExcluded: `A1`
- implementationSourceAfterReview: `Fable 2차 기획 docs/31-action-feedback-a2-plan.md`
- mainMergeApproval: `0/3`

## 1. 결론

1차 기획의 문제 정의와 핵심 방향은 유지한다. A1의 `useActionFeedback`·`ActionFeedback`·구조화 오류 안내를 생산계획·구매·자재·패널·선택 Excel에 재사용하는 것은 Roadmap A2와 정확히 일치한다. 다만 1차안은 모든 재조회에 generation guard를 확장하고 focus helper를 hook에 넣는 등 공통 기반을 과도하게 키울 여지가 있고, 저장 후 복귀 feedback 전달·Excel blob/trigger 구분·field 오류 연결의 실제 구현 경계가 아직 모호하다.

2차 기획은 아래 resolution을 반영해 **Frontend-only bounded adoption**으로 확정하는 것이 권장안이다. 신규 API·DB·migration·업무 상태·권한은 필요하지 않으며 현재 확인된 blocking decision은 없다.

## 2. 사용자 문제와 기대 결과

### 유지

- 사용자가 action 위치에서 loading/success/error/partial과 다음 행동을 확인한다.
- 저장·apply·판정 실패 시 입력값·선택·필터를 보존한다.
- client/server field 오류를 요약과 field에 함께 표시하고 첫 invalid control로 focus한다.
- 동일 scope의 중복 submit과 관련 bulk/row conflict를 막는다.
- 모바일 390px에서 PC 표 축소판이 아닌 기존 adaptive card/section 안에 feedback을 둔다.

### 추가

- 저장 성공 뒤 `onBack()`으로 editor가 unmount되는 흐름은 성공을 editor 안에서 잠깐 보여 주는 것으로 완료되지 않는다. 부모 dashboard가 보유하는 **one-shot contextual feedback callback**으로 전달하고, 다음 action 또는 명시적 새로고침 때 reset한다.
- screen reader 사용자는 요약 alert와 각 field의 여러 `role=alert`가 동시에 반복 낭독되지 않아야 한다. 요약은 `role=alert`, field 설명은 stable id + `aria-describedby`를 기본으로 하고 필요 시 `aria-live` 중복을 피한다.

## 3. 제품 방향·Roadmap 정합성

- Roadmap과 완료 원장은 A1을 완료 slice로, A2를 현재 첫 번째 `READY_FOR_PLANNING` 범위로 명시한다. 순서 일치이며 별도 override는 필요 없다.
- A2는 기존 업무를 더 안전하게 수행하게 하는 UX 능력이다. API·권한·상태 전이를 확장하지 않는다는 1차안은 제품 방향과 맞다.
- HOME Change 002에서 확정한 “전 부서 조회, 담당 부서만 입력” 불변조건을 유지해야 한다. feedback 적용을 이유로 읽기 전용 사용자의 mutation control을 새로 활성화하지 않는다.
- 선택 Excel 20개 화면 계약은 이미 완료됐다. A2는 공통 export action 결과 UX만 바꾸며 row cap·workbook·selection authorization을 다시 설계하지 않는다.

## 4. 현재 Repository 대조

확인된 사실:

- `useActionFeedback`은 ref 기반 동기 busy fence, exact/prefix conflict, refresh boolean에 따른 partial, 403/404/409 guidance를 이미 제공한다.
- `ActionFeedback`은 `role=status/alert`, `aria-live`, error/partial focus를 이미 제공한다.
- `FormErrorSummary`, `FieldErrorMessage`, `focusField`, `handleFormError`, `fieldErrorsFromApiError`가 이미 있으므로 새 form framework는 필요 없다.
- 대상 editor와 Excel dialog에는 `isSaving`·`isPreviewing`·`isApplying`이 있으나 하단 message와 `successMessage()` 또는 `includes('했습니다')` tone 판정이 남아 있다.
- `ExcelExportAction`은 React state만으로 busy를 막고 blob 수신·object URL·anchor click을 한 try/catch에 묶는다. 422/429 특화 안내는 이 component에만 존재한다.
- `SelectedExportTray`는 공통 component이므로 한 번의 안전한 변경으로 완료된 선택 export 화면 전체에 feedback 계약을 전파할 수 있다.

## 5. 기능 분류

### 유지

1. A1 공통 feedback 계약 재사용.
2. 대상 화면의 문자열 tone 판정 제거.
3. 동일 action 중복 차단과 관련 scope conflict.
4. mutation 성공 + 후속 refresh 실패의 partial 구분.
5. field error 요약·첫 invalid focus·`aria-describedby`.
6. 선택 Excel blob 수신과 client trigger 단계 구분.
7. desktop·390px synthetic browser 검증.

### 추가

1. `focusFirstFieldError(errors, orderedFields?)` 같은 DOM helper를 form layer에 둔다. `useActionFeedback`은 feedback/busy만 담당하고 DOM field 탐색 책임을 받지 않는다.
2. `fieldErrorId(field)`를 도입해 dynamic row field도 안정적인 id를 갖게 하고 해당 input/select/textarea에만 명시적으로 `aria-describedby`를 연결한다.
3. parent/editor 계약은 `onSaved(feedback)` 또는 동등한 callback으로 한정한다. App 전역 toast/store·session storage·route state를 추가하지 않는다.
4. Excel은 server blob을 받은 뒤 download trigger 실패를 별도 catch로 `partial` 처리하고 object URL revoke를 `finally`로 보장한다. 422/429 문구는 export 전용 mapper에서 보존한다.
5. button double activation이 React rerender 전에 들어와도 막히도록 export는 A1의 ref fence 또는 component-local ref를 사용한다.
6. post-mutation refresh가 실제로 있는 parent dashboard·materials/IQC 목록에만 generation guard를 적용한다. 단순 최초 조회와 무관 목록 전체를 재작성하지 않는다.

### 보류

1. 관리자·Pending·설정 화면 전면 확대.
2. 전역 toast/store.
3. 모든 목록 query의 공통 cache/generation framework.
4. 모든 form component를 새 디자인 시스템으로 교체.
5. 선택 Excel의 column picker·multi-sheet·파일 내용 변경.

### 제거

1. `useActionFeedback` 자체에 DOM focus helper를 넣는 방안.
2. 모든 A2 화면의 모든 재조회에 generation guard를 일괄 추가하는 해석.
3. 20개 선택 export 화면별로 같은 screenshot을 반복 생성하는 요구. 공통 component 자동 회귀 + 업무군별 대표 desktop/mobile 증빙이면 충분하다.
4. editor 저장과 modal apply가 구조상 동시에 실행될 수 없는 곳까지 인위적인 cross-component lock framework를 만드는 방안. 실제 동시 가능 action만 conflict로 선언한다.

## 6. Review Finding과 Resolution

| ID | 중요도 | Finding | Resolution |
| --- | --- | --- | --- |
| `UX-A2-FOCUS-RESPONSIBILITY` | P1 planning | focus helper를 feedback hook에 넣으면 DOM 탐색과 비동기 action orchestration 책임이 결합된다 | helper를 form layer/App utility에 두고 hook은 수정 최소화 |
| `UX-A2-RETURN-FEEDBACK-LOSS` | P1 planning | 성공 직후 `onBack()`이면 editor feedback state가 unmount되어 사용자에게 보이지 않는다 | 부모 dashboard one-shot contextual feedback callback을 명시 |
| `UX-A2-EXPORT-STAGE-AMBIGUITY` | P1 planning | blob 수신과 client trigger를 분리하라고 했지만 object URL 정리·422/429 guidance·rerender 전 double activation 계약이 빠졌다 | 단계별 try/catch/finally, export 전용 mapper, ref fence를 명시 |
| `UX-A2-GENERATION-SCOPE` | P2 planning | 모든 목록 재조회 guard로 읽히면 범위가 과도하게 확장된다 | 변경 action 뒤 preserve refresh가 있는 흐름만 적용 |
| `UX-A2-LIVE-DUPLICATION` | P2 planning | summary와 모든 field가 동시에 alert이면 반복 announcement 가능성이 있다 | summary alert + field describedby 기본, 단일 contextual ActionFeedback 사용 |
| `UX-A2-EVIDENCE-EXPANSION` | P2 planning | 공통 export 적용을 20개 페이지별 screenshot으로 해석하면 사용자 가치 없이 증빙만 팽창한다 | 공통 unit regression + 업무군별 대표 desktop/mobile 증빙으로 한정 |

위 Finding은 2차 기획 입력에서 모두 resolution 가능하며 구현을 막는 미확정 정책은 아니다.

## 7. 권장 개발 순서

1. 공통 최소 기반: export stage helper/ref fence, field id·첫 오류 focus helper, 필요한 test.
2. 구매·패널 Excel dialog + editor 저장: preview/apply 위험과 dynamic field error를 먼저 고정.
3. 생산계획 editor/dialog: 일정·담당자 dynamic field와 parent return feedback.
4. 자재 입고·IQC·키팅: 실제 동시 가능한 row/bulk scope와 preserve refresh만 적용.
5. 공통 선택 Excel: component 한 번 수정 후 대표 소비 화면과 기존 20개 screen registry 회귀.
6. A1 내 업무·알림 회귀, desktop·390px browser, 전체 Frontend validation.

각 vertical slice는 현재 mutation 권한·API payload·onBack navigation을 보존해야 한다. `App.tsx`가 이미 큰 P3는 이번 A2에서 구조 분할 사유로 사용하지 않는다.

## 8. 검증 권고

- hook/export helper unit: same-tick double submit, exact/prefix conflict, 422/429 guidance, blob success + trigger failure partial, URL revoke.
- form unit: client/server error map, first invalid focus, stable `aria-describedby`, dynamic row field.
- 화면 unit: 저장 success parent contextual feedback, error 입력/context 보존, apply success + refresh fail partial, read-only mutation 비활성.
- 회귀: A1 `/my-work`·`/notifications`, 선택 export registry, existing mock UI.
- browser: 생산계획·구매·자재·패널·선택 export 업무군의 대표 상태를 desktop/390px에서 검증하되 개인정보 없는 synthetic data만 사용한다.
- Backend/API/DB/migration/provider diff 0을 확인한다. 계약 부족이 발견되면 구현을 중단한다.

## 9. 2차 기획 지시

Fable 2차 기획은 1차 기획의 목표·권한·제외 범위를 유지하면서 이 review의 `유지/추가/보류/제거`와 6개 Finding resolution을 완전한 구현 계약에 반영해야 한다. 특히 다음을 명시한다.

- `useActionFeedback`은 책임을 늘리지 않고 기존 busy/feedback orchestration을 재사용한다.
- field focus/description helper는 form layer에 둔다.
- editor 성공 결과는 부모 contextual callback으로 보존한다.
- Excel은 blob/trigger 단계, ref fence, URL cleanup, 422/429 guidance를 보존한다.
- generation guard는 post-mutation preserve refresh로 제한한다.
- screenshot은 업무군별 대표 증빙으로 제한한다.

openBlockingDecisionCount: 0
