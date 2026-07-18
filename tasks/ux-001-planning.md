# TASK-UX-001 — 기존 업무 화면 Action Feedback UX 확대 A1 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전 (experiment fast-track 1차 기획)
> 목적: 내 업무·알림 화면의 action 인접 feedback 계약을 확정하기 위한 기획 문서

- taskType: `NEW_FEATURE`
- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/ux-001-interview.md`
- interviewUserConfirmed: true
- fastTrackSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 내 업무·알림의 mutation action 결과가 화면 상단 문자열로만 표시되고, 진행 중 중복 실행이 일부만 차단되며, 성공·오류 tone 판정이 문자열 포함 여부에 의존한다.
- 대상 사용자·역할: 업무 담당 사용자(내 업무 시작·완료·이동), 알림 수신 사용자(개별 읽음·전체 읽음·이동). 신규 권한·역할 없음.
- 정상 흐름: action 실행 → 해당 control만 진행 중/비활성 → action 근처 성공 표시 → 목록·badge 재조회 → 다음 행동 선택.
- 예외·복구 흐름: validation 실패 시 오류 tone·한글 원인·다음 행동을 action 근처에 표시하고 focus 이동. 실패 시 현재 tab·selection 유지 후 재시도 가능. mutation 성공 뒤 refresh 실패는 partial로 구분.
- 확정한 정책과 명시적 제외: Backend 권한·업무 규칙·audit 보존, 중복 submit 차단, 접근 가능한 feedback, isolated 검증만 사용. A2 전면 확대, 신규 API/DB/migration/provider, 대표 repo·`main`·Persistent UAT·push·PR·merge 제외.
- 실험 standing instruction으로 자동 채택된 권장안: ① 최소 hook+component 구조, ② mutation 성공/refresh 실패를 구분하는 partial 안내, ③ 동일 행 잠금 + bulk action은 관련 scope 잠금.
- planning으로 넘긴 비차단 미결정 사항: A2 대상 화면별 확대 순서, 전역 toast store 필요성.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

내 업무·알림 화면에서 사용자가 실행한 action의 처리 중·성공·실패·부분 성공과 다음 행동을 action 바로 근처에서 즉시 확인하고, 같은 요청을 중복 제출하지 않고 안전하게 재시도할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

현재 Repository에서 확인한 사실은 다음과 같다.

- 공통 `ActionFeedback` component가 이미 존재한다(`frontend/src/App.tsx`, tone `neutral | loading | success | error | partial`, error는 `role="alert"`, 그 외 `role="status"`). `.action-feedback[data-tone=…]` CSS도 `frontend/src/styles.css`에 이미 있다.
- 그러나 내 업무(`MyWorkPage`)와 알림(`NotificationsPage`)은 이 component를 쓰지 않고 화면 상단의 단일 `message` 문자열을 `message.includes('실패') || message.includes('권한')` 문자열 판정으로 success/error class를 정한다.
- `ActionFeedback`을 쓰는 다른 화면들도 `message.includes('없습니다')` 같은 문자열 포함 판정으로 tone을 추론하고, `PendingPage`는 raw `.action-feedback` markup을 별도로 복제한다. 즉 contract가 화면별로 분산되어 있다(Roadmap TASK-UX-001의 주요 위험과 일치).
- 내 업무의 이동 action은 `movingWorkItemId`로 해당 버튼만 잠그지만, `작업 완료` action과 알림의 `읽음`·`전체 읽음`은 진행 중 잠금이 전혀 없어 중복 submit이 가능하다.
- mutation 성공 뒤 `load()` 재조회가 실패하면 성공 message와 별개로 목록이 오류 상태로 바뀌어, 사용자가 mutation 자체가 실패한 것으로 오인할 수 있다.
- `aria-live` 명시 선언은 없고, `ActionFeedback`의 `role="status"/"alert"`가 제공하는 암시적 live region이 유일한 announcement 경로이나 두 대상 화면은 이를 쓰지 않는다.

이 상태를 유지하면 모바일 현장 사용자가 action 결과를 놓치거나 같은 요청을 반복하고, keyboard·screen reader 사용자는 결과와 오류 위치를 일관되게 파악하기 어렵다. 현재 우회 방식은 상단 메시지를 찾아보거나 목록 갱신을 기다렸다가 다시 실행하는 것이다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 업무 담당 사용자 | 내 업무 시작·완료·업무 화면 이동 | 기존 본인 업무 scope | 기존 허용 action만 |
| 알림 수신 사용자 | 개별 읽음·전체 읽음·상세/업무 이동 | 기존 본인 알림 scope | 기존 읽음 상태만 |

새 권한, 역할, API 업무 능력을 추가하지 않는다. Backend를 권한·validation의 authoritative source로 유지하며, 이 Task의 Backend 범위는 기존 오류 응답이 한글 원인 메시지로 표면화되는지 확인하는 read-only 대조뿐이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 내 업무 완료 성공

1. 사용자가 내 업무 목록의 특정 업무 행에서 `작업 완료`를 누른다.
2. 해당 행의 action 버튼들이 즉시 비활성화되고 진행 중 label(예: `완료 처리 중`)이 표시된다. 다른 행의 action은 계속 사용할 수 있다.
3. 성공하면 해당 행 근처에 성공 tone feedback이 `role="status"`로 표시되고 목록·badge가 재조회된다. 현재 tab과 선택 상태는 유지된다.

### 시나리오 B — 알림 전체 읽음 실패와 재시도

1. 사용자가 `전체 읽음`을 누르고, 진행 중에는 `전체 읽음`과 개별 `읽음` 버튼이 함께 잠긴다(관련 scope 잠금).
2. 요청이 실패하면 `전체 읽음` 버튼 근처에 오류 tone feedback이 `role="alert"`로 표시되고 focus가 feedback으로 이동한다. 원인과 다음 행동(다시 시도)이 한글로 안내된다.
3. 사용자는 현재 tab·선택 상태를 잃지 않고 같은 action을 다시 시도한다.

### 시나리오 C — mutation 성공, refresh 실패 (partial)

1. 사용자가 `읽음`을 누르고 mutation은 성공했으나 직후 목록 재조회가 실패한다.
2. 시스템은 실패 tone이 아니라 partial tone으로 "읽음 처리는 완료됐고 최신 목록을 불러오지 못했습니다. 새로고침을 눌러 주세요."류의 안내를 action 근처에 표시한다.
3. 사용자가 `새로고침`으로 최신 상태를 다시 불러온다.

## 5. 기능 요구사항

### 필수

- [ ] A1 공통 feedback contract: tone(`loading | success | error | partial`), 한글 message, 다음 행동 안내, `role="status"/"alert"` 암시적 live region, 오류 시 focus 이동을 하나의 재사용 가능한 hook+component 구조로 정의한다.
- [ ] 성공·오류 판정을 문자열 포함 검사 대신 try/catch 기반 구조적 판정으로 전환한다(내 업무·알림 두 화면 한정).
- [ ] 내 업무: `이동/시작`, `작업 완료` action에 행 단위 busy 잠금과 진행 중 label을 적용하고 feedback을 해당 행(모바일 card / desktop table row) 근처에 표시한다.
- [ ] 알림: 개별 `읽음`은 행 단위 잠금, `전체 읽음`은 자기 자신 + 개별 `읽음` 버튼의 관련 scope 잠금을 적용하고 feedback을 각 action 근처에 표시한다.
- [ ] mutation 성공 후 재조회 실패를 partial tone으로 구분하고 재조회 재시도 다음 행동을 안내한다.
- [ ] 실패 시 현재 tab·selection·화면 context를 유지한다.
- [ ] loading·empty·error·authorization denied·target-not-found 상태의 다음 행동 구분을 두 화면에서 보강한다(기존 `LoadState`/`StateMessage` 유지 활용).
- [ ] desktop과 390px에서 page-level horizontal overflow 0을 유지한다.

### 선택

- [ ] 성공 feedback의 자동 소멸 또는 명시적 dismiss(구현 단순성을 해치지 않는 범위에서, 기본은 다음 action 실행 시 reset).
- [ ] 상단 기존 message 영역을 완전히 제거하는 대신 화면 전역 결과(예: 전체 읽음 성공)에만 남길지 여부의 정리.

### 명시적 제외

- [ ] A2 생산계획·구매·자재·패널·Excel 화면 전면 확대(기존 문자열 판정 call site는 회귀만 보장하고 이번에 수정하지 않는다)
- [ ] 업무 규칙·권한·API·DB·migration의 신규 능력
- [ ] 알림 delivery 재처리, 사용자별 알림 설정, 실제 Teams/Mail/Activity provider 호출
- [ ] 전역 toast store 도입
- [ ] 대표 repo·`main`·Persistent UAT·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 내 업무 | `/my-work` | KPI, tab, 프로젝트별 업무 그룹(모바일 card / desktop table) | 이동/시작, 작업 완료, 선택 export | 해당 행 근처 tone별 feedback, 진행 중 label, 실패 시 focus 이동 |
| 알림 | `/notifications` | 요약, tab, 프로젝트별 알림 그룹 | 상세, 이동, 읽음, 전체 읽음 | 개별 행 근처 feedback + 전체 읽음은 header action 근처 feedback |

확인할 UX 항목:

- 진행 중 상태는 label 변경 + `disabled`로 표현하고, 어느 action이 처리 중인지 행 단위로 식별 가능해야 한다.
- 다음 행동(다시 시도, 새로고침, 권한 문의)이 message에 포함되어야 한다.
- 저장·변경 결과가 action 근처에 보이고 상단으로 시선 이동을 강요하지 않아야 한다.
- 권한 부족(403)·대상 없음(404)·network 오류를 서로 다른 한글 안내로 구분한다(기존 API client 오류 메시지 계약 확인 범위).
- 모바일 card layout에서 feedback이 card 내부에 들어가고 390px에서 overflow가 없어야 한다. 모바일을 PC 축소판으로 만들지 않는다.

## 7. 업무 규칙과 불변조건

- Backend 권한·업무 상태 전이·읽음 기록·audit은 어떤 경우에도 Frontend feedback 상태로 대체·우회되지 않는다.
- 같은 action scope의 진행 중 중복 submit은 차단한다. 서로 다른 행의 action은 병렬 실행을 허용한다(자동 채택 권장안 ③).
- mutation 성공 사실은 이후 refresh 실패로 인해 실패로 표시되지 않는다(자동 채택 권장안 ②).
- feedback은 화면 session 상태이며 영구 저장하지 않고, 기존 인앱 알림 원본과 deep link를 변경하지 않는다.
- 기존 선택 Excel export(`SelectedExportTray`/`useSelectedRows`)의 busy 계약과 충돌하지 않아야 한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `ActionFeedbackTone` | `neutral/loading/success/error/partial` | 기존 (App.tsx) | 없음 (UI 전용) |
| action feedback state | scope key별 `{ tone, message }` 구조화 상태 | 신규 (Frontend hook) | 영구 저장 없음 |
| action busy state | scope key(예: `work:<id>`, `notification:<id>`, `notifications:all`)별 진행 중 여부 | 신규 (Frontend hook) | 영구 저장 없음 |
| work item / notification | 업무·알림 도메인 데이터 | 기존 | Backend 기존 audit 유지, 변경 없음 |

```text
idle → loading → success | error | partial → (다음 action 실행 또는 dismiss/reset) → idle
```

제품 DB 개념 추가는 없다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 업무 시작·완료 전이 허용 여부, 알림 읽음 권한·scope. 변경 없음.
- 필요한 조회와 mutation: 기존 `listMyWorkItems`/`getMyWorkSummary`/`startMyWorkItem`/`completeMyWorkItem`, `listNotifications`/`getNotificationSummary`/`markNotificationRead`/`markAllNotificationsRead`만 사용. 신규 endpoint 없음.
- 권한·validation: 기존 계약 유지. 403/404/validation 오류가 사용자에게 구분 가능한 한글 메시지로 도달하는지 read-only로 확인하고, 계약 보정이 필요하면 blocking decision으로 중단·보고한다(임의 수정 금지).
- transaction·동시성·idempotency: 서버 측 변경 없음. 중복 submit 차단은 Frontend 방어이며 서버의 기존 idempotent 동작(이미 읽음/이미 완료)을 대체하지 않는다.
- audit trail: 변경 없음.
- 외부 provider 영향: 없음. 알림 읽음 처리는 delivery 로직을 건드리지 않는다.

## 10. Frontend 고려사항

- route/component: `frontend/src/App.tsx`의 `MyWorkPage`, `NotificationsPage`가 중심. 공통 구조는 기존 `ActionFeedback` component를 확장(필요 시 focus용 ref/tabIndex 추가)하고, 신규 `useActionFeedback`(가칭) hook 파일을 `useSelectedRows.ts`와 같은 최상위 패턴으로 추가한다.
- hook 계약(권장): `run(scopeKey, mutationFn, { successMessage, errorFallback, refresh })` 형태로 ① scope 진행 중이면 무시, ② loading 설정, ③ mutation try/catch로 tone 구조 판정, ④ refresh 실패를 partial로 분리, ⑤ error 시 feedback focus 이동까지 담당한다.
- loading/empty/error/success: 목록 조회는 기존 `LoadState<T>`/`toLoadError`/`StateMessage` 패턴을 유지하고, action feedback과 목록 상태를 혼합하지 않는다.
- 접근성: error `role="alert"`, 그 외 `role="status"`(암시적 `aria-live`), 진행 중 버튼 `disabled` + label 변경, 오류 시 feedback으로 programmatic focus, keyboard 순서 유지.
- 390px/mobile/narrow pane: 모바일 card 내부 feedback 배치, Teams narrow pane 포함 page-level overflow 0 유지, 기존 CSS variable과 `.action-feedback` tone 스타일 재사용.
- 기존 화면(App.tsx의 다른 `ActionFeedback` call site, `PendingPage` 복제 markup)은 이번에 수정하지 않는다. 단, 공통 component signature를 바꿀 경우 기존 call site의 호환을 유지해야 한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 이동·완료·읽음 흐름과 badge 재조회(`onBadgeRefresh`)를 그대로 유지한다.
- 권한/관리자: 영향 없음.
- Excel/PDF/첨부: `/my-work`·`/notifications`의 선택 Excel export는 회귀 검증만 한다.
- Teams/Mail: `/teams/activity` 화면은 이번 범위가 아니며 회귀만 확인한다.
- 삭제·복구/감사: 영향 없음.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 신규 `useActionFeedback` hook + 기존 `ActionFeedback` component 확장, 두 화면만 적용 | 계약이 한 곳에 모임, 행 단위 병렬 busy를 자연스럽게 표현, A2 확대의 기반 | hook 설계 범위를 절제하지 않으면 A1이 커질 위험 |
| B | component만 재사용하고 각 화면이 busy/tone 상태를 개별 구현 | 초기 diff 최소 | 문자열 판정·중복 로직이 화면별로 재생산되어 현재 문제를 반복 |
| C | 전역 toast/feedback store 도입 | 모든 화면에 즉시 일괄 적용 가능 | action 인접 표시 원칙과 충돌, 범위 팽창, A1 목적 초과 |

권장안은 A다. Interview에서 실험 standing instruction으로 자동 채택되었고, Roadmap의 "화면별 임시 구현으로 contract 분산 방지" 위험 항목과 정합한다. C의 전역 toast 필요성은 deferred 비차단 결정으로 남긴다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic 환경과 local browser만 사용한다.
- migration 필요 여부: 없음. 필요성이 발견되면 구현을 중단하고 blocking decision으로 보고한다.
- 외부 발송/실제 데이터 영향: 없음. provider 호출·신규 notification event 없음.
- runtime 교체 여부: 없음. Frontend-only 변경이며 rollback은 Frontend 되돌림으로 충분하다.
- 추가 사용자 승인 필요 작업: push·PR·merge·Persistent UAT·provider는 이 Task에서 승인되지 않았다. local experiment commit만 허용된다(`commitApproved: true`).

## 14. 검증 계획

- 최소 테스트: `corepack pnpm --dir frontend run lint` / `run typecheck` / `test` / `run build` (Validation Matrix Frontend 최소 검증).
- 신규 unit: hook의 중복 submit 차단, scope별 병렬 busy, success/error 구조 판정, partial 분리, reset. `frontend/tests/App.test.tsx`에 내 업무 완료 action 진행 중 disabled·행 인접 feedback·실패 시 tab 유지, 알림 읽음/전체 읽음 잠금·partial 시나리오를 추가한다.
- 영향 영역 회귀: 사용자 UX 변경 기준으로 desktop/390px의 loading/empty/error/success·console/request failure 확인. 기존 `frontend/e2e/full-stack/mobile-first-experience.full-stack.spec.ts`(`/my-work`, `/notifications` 390px)와 `all-pages-selected-export.full-stack.spec.ts` 등 관련 isolated Full-Stack E2E 회귀 실행.
- PR/CI: 이번 Task는 push·PR 미승인이므로 local 검증까지만 수행하고 상태를 기록한다.
- 사용자 검수: 두 화면의 desktop·390px screenshot(정상·진행 중·실패·partial 상태 포함 가능 범위)으로 검수 대기 상태 전환. Persistent UAT 증빙은 사용하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 두 화면의 모든 대상 action이 행/scope 단위 잠금과 tone별 인접 feedback을 갖고, API·권한·업무 전이·audit 계약 변경이 0이다.
- UX: 문자열 포함 tone 판정이 두 화면에서 제거되고, 오류 focus 이동·진행 중 label·다음 행동 안내가 동작하며 390px overflow 0이다.
- 자동 테스트: 위 최소·신규·회귀 테스트 통과. 실패·미실행 항목은 이유와 함께 기록한다.
- 5종 산출물: implementation report와 나머지 산출물 상태·위치를 `docs/12-task-completion-policy.md` 기준으로 추적한다.
- 사용자 검수 상태: `사용자 검수 대기`로 전환(자동 검증 완료와 구분).
- PR 상태: N/A — 이 실험 Task는 local commit까지만 승인되었다.

중단 조건: 기존 API 오류 계약이 화면 구분 안내에 부족해 Backend 보정이 필요해지는 경우, migration·신규 능력 필요가 발견되는 경우, 문서와 구현의 의미 있는 충돌이 발견되는 경우 — 구현을 진행하지 않고 blocking decision으로 보고한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | A2 확대 시 대상 화면(생산계획·구매·자재·패널·Excel)의 적용 순서 | 업무 빈도 순 / 위험(중복 submit 노출) 순 / 화면 묶음별 일괄 | 대기 (A1 검수 후) |
| 2 | 전역 toast store의 도입 필요성 | 도입하지 않음(현행 인접 feedback 유지) / A2에서 보조 채널로 도입 | 대기 (A1 검수 후) |

두 항목 모두 interview에서 명시적으로 deferred된 비차단 결정이며 A1 구현을 차단하지 않는다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 없음 (오류 응답 계약 read-only 확인만).
- Frontend: `frontend/src/App.tsx`(`MyWorkPage`, `NotificationsPage`, `ActionFeedback`), 신규 `frontend/src/useActionFeedback.ts`(가칭), 필요 시 `frontend/src/styles.css`의 인접 배치 보조 스타일.
- DB/Migration: 없음.
- Tests/Scripts: `frontend/tests/App.test.tsx`, 신규 hook unit test, 관련 Full-Stack E2E 회귀(spec 수정은 회귀 보장에 필요한 최소 범위).
- Docs: `tasks/ux-001-*` 산출물, Roadmap TASK-UX-001 상태 갱신(구현 완료 후).

## 18. Roadmap 연결

- 선행 Task: TASK-NOTIFY-004 완료(Decision Log 2026-07-10의 B→A→C 순서), 공통 feedback contract 확정은 이 planning이 수행.
- 후속 Task: A2 업무 화면 확대(별도 Task/change), TASK-NOTIFY-005 사용자별 알림 설정.
- 현재 Go/No-Go: 실행 큐 5.3 `TASK-UX-001`은 "A1 planning 후 A2 분리" 조건이며, change-001에 `explicitRoadmapOverrideApproved: true`와 `PASS_REUSE`가 기록되어 있다.
- 별도 Task로 분리할 항목: A2 전면 확대, 전역 toast 정책, `PendingPage` 등 기존 화면의 문자열 판정 정리.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| (experiment fast-track) | 인터뷰·중간 확인 없이 권장안 자동 채택, 대표 repo·`main`·Persistent UAT·provider·push·PR·merge 제외 | interview round 0 자동 확정, 권장안 ①②③을 planning 입력으로 채택 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

이 초안은 experiment fast-track 절차상 Codex review와 Fable 2차 기획(`docs/25-action-feedback-a1-plan.md`) 이후에만 구현 입력이 된다.

1. `frontend/src/useActionFeedback.ts`(가칭)에 scope key 기반 busy/feedback hook을 추가한다: 진행 중 scope 재실행 무시, mutation try/catch 구조 판정, refresh 실패 partial 분리, error 시 feedback focus 이동, reset.
2. `ActionFeedback` component에 focus 이동을 위한 최소 확장(ref/tabIndex)을 추가하되 기존 call site 호환을 유지한다.
3. `MyWorkPage`: 상단 `message` 문자열 판정을 제거하고 이동/시작·작업 완료 action에 행 단위 hook 적용, 행 인접 feedback 배치(모바일 card·desktop table 모두), 실패 시 tab·selection 유지 확인.
4. `NotificationsPage`: 개별 읽음 행 단위 잠금, 전체 읽음 관련 scope 잠금, header/행 인접 feedback 배치, partial 시나리오 처리.
5. 기존 다른 화면의 `ActionFeedback` 사용부와 Backend·API·DB는 수정하지 않는다.
6. Frontend 최소 검증(lint/typecheck/test/build), 신규 unit test, 관련 Full-Stack E2E 회귀, desktop·390px screenshot을 수행하고 결과를 implementation report에 기록한다.
7. 변경은 experiment branch local commit까지만 수행한다.

---

- `planningStatus: DRAFT`
- `implementationApproved: false`
- `userDecisionRequiredCount: 2`
