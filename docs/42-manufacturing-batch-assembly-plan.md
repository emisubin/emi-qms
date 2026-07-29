# TASK-MANUFACTURING-BATCH-001 — 다중 패널 조립 단계 일괄 완료 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-MANUFACTURING-BATCH-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/manufacturing-batch-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/manufacturing-batch-001-planning.md` (판단 이력으로 보존, 수정하지 않음)
- codexReviewSource: `tasks/manufacturing-batch-001-review.md` (`GO_TO_SECOND_PLANNING`, `blockingDecisionCount: 0`)
- approvalChangeSource: `tasks/manufacturing-batch-001-change-001.md` (`fableSecondPlanningApproved: true`)
- planningRole: 이 문서가 이 실험 Task의 최종 구현 source of truth다. 1차 기획·Codex review와 충돌하면 이 문서를 따른다.

이 문서는 1차 기획의 유지 권고를 보존하고 Codex review의 추가·보류·제거 8+3+4건과 change-001 4장의 resolution 9건을 모두 확정 계약으로 반영했다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/validation-matrix.md`, `docs/development/privacy-safe-evidence.md`를 따르며 이 문서에 복사하지 않는다. main merge·대표 repo 승격·Persistent UAT 적용·실제 provider·push·PR·게시 승인은 이 문서가 부여하지 않는다.

## 1. 한 줄 목표

제조 정·부 담당자가 제조 작업 화면에서 한 프로젝트의 여러 패널을 기존 선택 Excel용 checkbox로 선택해 조립 의미 단계까지의 미완료 단계를 한 transaction으로 일괄 확인하고, 프로젝트 상세 `프로젝트 전체 흐름`의 네 단계 표시가 사용자가 확정한 정확한 문구로 보인다.

## 2. 확정 배경 (interview·review 기준선)

- 사용자 확정 문제: 현장에서는 여러 패널의 조립을 한꺼번에 끝내지만, 시스템은 패널을 하나씩 열어 단건 `check-step`으로 같은 단계를 반복 확인해야 한다. 근거 Finding은 `TASK-E2E-FULL-SUITE-001 change-006`의 `MULTI-PANEL-REPETITIVE-INPUT-FRICTION`(P3)이며, `TASK-011A`는 복수 panel batch 실행을 명시적으로 제외했다.
- 사용자 확정 표시명 4건: `자재 / 키팅 완료 (선택)` → `자재 / 제조 요청`, `물류 / 포장 완료` → `물류 / 포장`, `물류 / 납품 완료` → `물류 / 납품`, `영업 / 세금계산서 완료` → `영업 / 세금계산서`. 내부 stage code·`is_optional`·완료 계산은 바꾸지 않는다.
- Repository 확인 사실: 18단계 seed에서 `SalesSettlementCompleted`의 DB `stage_name`은 `세금계산서·완료`(`database/migrations/0010_work_items_notifications.sql`)다. 따라서 문자열 치환이 아니라 `stageCode` 기준 override로만 목표 문구를 달성한다. `(선택)` 접미사는 `frontend/src/App.tsx`의 workflow board 렌더링 한 곳(`stage.isOptional` 분기)에서만 붙는다.
- interview 5장의 비차단 정책 7건은 standing instruction에 따라 1차 기획 권장안 R1~R7을 제시했고, Codex review가 전부 유지·보강 판정했다. 이 문서 5~9장이 그 최종 확정값이며 사용자에게 재질문하지 않는다. blocking decision은 0이다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 행동 | 강제 계층 |
| --- | --- | --- |
| 제조 정·부 담당자 (`ManufacturingUpdate` 정책 + project scope + 패널별 `canMutate`) | 선택 패널 조립 일괄 완료 | Backend가 권한·scope·상태를 전부 재검증. Frontend 노출·disabled는 안내일 뿐 authoritative가 아니다 |
| 그 외 부서·조회 사용자 | 결과 조회만 | 기존 `ProjectRead` scope. batch action bar 미노출, 기존 `manufacturing-readonly-note` 유지 |
| 프로젝트 상세 열람자 | 전체 흐름 표시 확인 | 표시 문구만 변경, 데이터·권한 불변 |

신규 권한·역할은 만들지 않는다.

## 4. 핵심 시나리오 (확정)

### A. 정상 일괄 완료
1. 제조 작업 화면에서 프로젝트를 열고 패널 checkbox·전체선택으로 진행 중 패널 여러 개를 선택한다.
2. 선택 Excel tray 바로 아래의 제조 전용 batch action bar에서 `선택 패널 조립 완료 (n면)`을 누른다.
3. 확인 sheet(기존 `MobileSheet`, desktop·mobile 공통)가 대상 패널 수, 함께 확인될 단계 수 합계, 제외 패널 수·사유와 “조립 단계까지의 미완료 선행 단계도 같은 시각·요청자로 함께 확인됩니다” 안내를 보여준다.
4. 실행하면 서버가 전부 재검증 후 한 transaction으로 각 패널의 선행 미확인 단계부터 조립 의미 단계까지 순서대로 확인 처리하고, 단계마다 `StepChecked` event와 version 증가를 남긴다.
5. 성공 피드백 `n면의 조립 단계를 완료했습니다 (단계 확인 k건).`이 표시되고 queue·상세가 새로 로드되며 선택은 비워진다.

### B. 혼합 선택 (Frontend 사전 제외 + 서버 전체 재검증)
1. 전체선택으로 시작 전·중단·완료·이미 조립 완료·식별 불가 패널까지 선택해도, action bar가 대상 가능 n면과 제외 m면·사유를 텍스트로 보여준다.
2. 실행 payload에는 대상 가능 패널만 담는다. 서버는 전송된 목록을 다시 전부 검증해 하나라도 부적격이면 부분 반영 없이 전체를 거부하고, 부적격 패널 display code와 고정 사유를 최대 20건 + 나머지 count로 반환한다.
3. 실패 후 선택 집합은 유지해 사용자가 사유를 보고 수정할 수 있게 한다.

### C. 경쟁·stale·재시도
1. 다른 사용자가 먼저 일부 패널을 진행하면 version 불일치로 batch 전체가 409이고, 기존 문구(`다른 사용자가 먼저 변경했습니다…`) 계열 안내 후 최신 상태를 다시 불러온다.
2. 같은 operation id + 같은 payload 재시도는 저장된 결과 projection을 중복 event 없이 replay(`replayed: true`)한다. 같은 operation id + 다른 payload·다른 요청자는 conflict다.

## 5. 조립 의미 단계 식별 계약 (R1 확정, review 추가 3)

- 실행의 immutable `panel_manufacturing_executions.template_version_id` → 그 version의 `manufacturing_step_template_items`에서 `item_code = 'MANUFACTURING'`인 항목의 `display_order`를 snapshot step `sequence_number`에 대응해 파생한다. 신규 저장 없음. label 문자열·“3번째 고정” 매칭은 채택하지 않는다(review 제거 2).
- 식별이 유효한 조건: `template_version_id`가 null이 아니고, 그 version에 `MANUFACTURING` item이 정확히 1개 있으며, 실행 snapshot에 그 `display_order`와 같은 `sequence_number`의 step이 정확히 1개 있고, snapshot step 수가 version item 수와 같다.
- 하나라도 어긋나면 추측하지 않고 reason code `ASSEMBLY_STEP_UNRESOLVED`로 제외(Frontend)·거부(Backend)한다.
- Active version 불변조건과 `on delete restrict`(`TASK-ADMIN-002`)가 join 안정성을 보장한다.

## 6. 선행 단계·원자성·비대상 처리 계약 (R2~R5 확정)

- 선행 단계(R2-A): 조립 단계 전의 미확인 단계는 같은 batch에서 순서대로 함께 확인한다. 단계별 event를 개별 기록하고 확인 sheet·성공 피드백에 함께 확인 사실을 명시한다. 조립만 건너뛰어 기록하는 순서 위반은 없다. `자체 확인`(SELF_CHECK) 이후 단계는 절대 확인하지 않는다.
- 원자성(R3): 전송된 payload는 전부 성공/전부 실패다. 부분 성공 API는 만들지 않는다(review 제거 3).
- 비대상(R4): Frontend가 queue 데이터로 사전 분류·사유 표시 후 대상만 전송하고, 서버가 전량 재검증한다. 시작 전 패널의 자동 시작은 금지한다 — 제조 시작은 LQC 업무 생성 부작용이 있는 별도 단건 action이다(review 유지 5).
- 사후 상세 입력(R5): batch는 실제 요청자·`now()`만 기록한다. 소급 시각·작업시간·상세값 보정 모델은 만들지 않고 Deferred로 남긴다(review 보류 1).
- batch는 execution 전체 완료(`Completed`), work item 완료, panel workflow stage 전이, LQC 생성·OQC 인계를 어느 것도 수행하지 않는다(review 제거 4). batch 뒤 execution status는 계속 `InProgress`다.

## 7. 데이터 모델과 additive migration `0056`

기존 migration은 수정하지 않는다. 미커밋 `0055` 다음의 additive `0056` 1건만 추가한다.

### 7.1 신규 table `panel_manufacturing_assembly_batch_operations`

| column | 계약 |
| --- | --- |
| `operation_id uuid primary key` | 클라이언트 생성 operation id |
| `project_id uuid not null references projects(id) on delete restrict` | 단일 프로젝트 범위 |
| `requested_by_user_id uuid not null references qms_users(id) on delete restrict` | 실제 요청자 |
| `panel_ids uuid[] not null` | panel id 오름차순 정렬 저장, `cardinality between 1 and 500` |
| `payload_fingerprint text not null` | `^[0-9a-f]{64}$` check (기존 fingerprint 계약과 동일) |
| `completed_panel_count integer not null` | `= cardinality(panel_ids)` check |
| `checked_step_count integer not null` | `>= completed_panel_count` check (패널당 최소 조립 단계 1건) |
| `created_at_utc timestamptz not null default now()` | append-only |

`project_id, created_at_utc desc` index를 추가한다. 기존 단건 `panel_manufacturing_operations`는 action별 단일 execution identity 전제이므로 재사용하지 않고 unique·fingerprint 계약을 건드리지 않는다(review §4).

### 7.2 event correlation (review 추가 1)

- `panel_manufacturing_events`에 `batch_operation_id uuid null references panel_manufacturing_assembly_batch_operations(operation_id)`를 추가한다.
- batch가 만든 모든 `StepChecked` event에 operation id를 기록하고, 기존 단건 event는 null을 유지한다. `batch_operation_id is not null` partial index를 추가한다.
- 1차 기획의 선택 항목이던 “timeline 이벤트 라벨 접미사”는 이 correlation FK가 감사 요구를 충족하므로 채택하지 않는다(확정).
- transaction 내 삽입 순서: 전량 검증 → 확인 예정 단계 수 확정 → operation row insert → 단계 update·event insert. FK는 같은 transaction에서 즉시 검증되므로 operation row를 먼저 넣는다.

### 7.3 검증

`backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs` 계열로 fresh PostgreSQL에 `0056`까지 적용을 검증한다. Persistent UAT에는 적용하지 않는다.

## 8. Backend API 계약 (확정)

### 8.1 조회 additive 필드

`ManufacturingPanelResponse`에 `AssemblyStepChecked: bool?`를 추가한다(`backend/src/Emi.Qms.Api/Manufacturing/ManufacturingContracts.cs`). `null` = 실행 없음 또는 5장 식별 불가, `false` = 조립 단계 미확인, `true` = 확인됨. `ListAsync`가 template item join으로 계산한다. 이 필드는 Frontend 사전 안내용이며 서버 재검증을 대체하지 않는다.

### 8.2 mutation `POST /api/manufacturing/executions/assembly-batch`

- 권한: `QmsPolicies.ManufacturingUpdate` + 기존 `Mutate` helper(`ManufacturingEndpointExtensions`)의 actor·scope 처리 재사용.
- 요청: `{ operationId, projectId, panels: [{ panelId, executionId, expectedVersion }] }`.
- 구조 검증(400): operation id·project id 비어 있음, panels 1~500 초과(`MaxReleasePanelCount = 500` 재사용), empty Guid, panelId 또는 executionId 중복, `expectedVersion < 1`.
- fingerprint: 기존 `Fingerprint` helper로 `"AssemblyBatch" | projectId | panelId 오름차순의 panelId:executionId:expectedVersion 목록`을 SHA-256 hex로 계산한다.
- transaction 순서(결정적 잠금, review 추가 5): `LockProjectAsync`(scope 포함 `for update`) → 신규 operation table replay 확인 → 프로젝트 `Active` 확인 → panel id 오름차순 정렬 후 각 execution을 `LockExecutionAsync`·`LockStepsAsync`로 잠금 → 전 대상 검증 통과 후에만 update.
- 대상별 재검증(하나라도 실패 시 전체 rollback): execution이 요청한 project·panel에 속함, 패널 활성, 실행 `InProgress`(중단 Pending의 `Blocked`·완료·취소·시작 전 자동 배제), `expectedVersion == snapshot.Version`, 5장 조립 단계 식별 가능, 조립 단계 미확인.
- 고정 reason code: `NOT_FOUND_IN_PROJECT`, `NOT_IN_PROGRESS`, `BLOCKED_PENDING`, `STALE_VERSION`, `ASSEMBLY_STEP_UNRESOLVED`, `ALREADY_ASSEMBLED`. 409 응답 문구는 부적격 패널을 `display code(한글 사유)` 형식으로 최대 20건 나열하고 나머지는 `외 n면`으로 요약한다. 내부 ID·SQL·stack은 노출하지 않는다(review 추가 7).
- 적용(review 추가 4): 각 execution에서 미확인 단계를 `sequence_number` 순서로 조립 단계까지 확인 처리한다. 단계마다 `checked_by_user_id = actor`, `checked_at_utc = now()` update + `batch_operation_id`를 포함한 `StepChecked` event 1건 + `IncrementVersionAsync` 1회. 최종 version은 `expectedVersion + 새로 확인한 단계 수`로 단건 `check-step`과 같은 optimistic concurrency 의미를 유지한다.
- 응답: `{ operationId, projectId, completedPanelCount, checkedStepCount, replayed }`. replay는 저장된 projection 값으로만 응답하고 현재 상태를 재계산하지 않는다(review 추가 2). operation id 재사용 시 project·actor·fingerprint가 하나라도 다르면 기존 문구 계열의 409 conflict다.
- 예외: `UniqueViolation`은 기존 패턴대로 409로 변환한다. 알림·work item·workflow stage mutation 없음.

## 9. Frontend 계약 (확정)

### 9.1 표시명 4건 (presentation-only)

- `frontend/src/App.tsx`의 `displayWorkflowStageLabel`에 `stageCode` override를 추가한다: `KittingCompleted → 제조 요청`, `PackingCompleted → 포장`, `DeliveryCompleted → 납품`, `SalesSettlementCompleted → 세금계산서`. 이 함수는 workflow board(현재 단계·다음 예정·18단계 목록)에서만 쓰이므로 영향이 그 화면으로 한정된다.
- workflow 단계 목록의 `(선택)` 접미사 렌더링 지점에서 override 대상인 `KittingCompleted`는 접미사를 표시하지 않는다. `isOptional` 데이터·진행률·완료 계산·`stage.statusLabel`은 불변이다.
- `displayWorkflowStageName`(내 업무·알림 목록 공유)과 Backend `workflow_stages` seed·stage_name은 변경 금지다(review §4). 제조 화면의 `키팅 완료/키팅 미보고` 참고 문구도 이번 범위가 아니다.

### 9.2 제조 batch UI

- `frontend/src/ManufacturingPage.tsx`: `SelectedExportTray` 바로 아래 제조 전용 batch action bar를 추가한다. tray는 Excel, action bar는 제조 mutation임을 라벨로 구분한다(R7). `canMutate` false면 bar를 렌더링하지 않는다.
- 분류: 선택된 패널 중 `status === 'InProgress' && assemblyStepChecked === false && canMutate`만 대상. 제외 사유 텍스트: `시작 전`(Ready), `중단 Pending`(Blocked), `실행 완료`(Completed), `취소`(Cancelled), `이미 조립 완료`, `조립 단계 식별 불가`. 사유는 색상이 아니라 텍스트로 제공한다(접근성).
- 버튼 `선택 패널 조립 완료 (n면)`은 대상 1면 이상이면 활성화하고, 대상 0이면 disabled 사유를 텍스트로 병기한다.
- 확인 단계는 기존 `MobileSheet`를 desktop·mobile 공통 재사용한다(별도 desktop modal 금지, review 제거 1). sheet 내용: 대상 n면, 함께 확인될 단계 수 합계 k건, 제외 m면·사유, “조립 단계까지의 미완료 선행 단계도 같은 시각·요청자로 함께 확인됩니다”(review 추가 8). `triggerRef`로 focus 복귀를 유지한다.
- 실행은 기존 `mutate` helper(`mutationInFlightRef`·`savingAction`·operation receipt·`refreshAfterMutation`)를 재사용하며 별도 동시 실행 state를 만들지 않는다(review §4). 성공 시 `manufacturingSelection.clear()`, 실패 시 선택 유지(review 추가 6). 선택 집합의 visible-id 재동기화는 현재 `useSelectedRows` 계약을 그대로 따른다.
- 피드백은 기존 `manufacturing-feedback`(`role="status"`)로 성공 요약·실패 사유(부적격 display code 나열)를 표시한다.
- `frontend/src/api.ts`·`frontend/src/manufacturing.ts`에 additive 요청/응답 타입과 client 함수를 추가하고, `frontend/src/styles.css`에 action bar 스타일을 추가한다. 390px에서 bar는 세로 적층이고 page-level horizontal overflow는 0이어야 한다.

## 10. 업무 규칙·불변조건 (보존 확인)

- 패널별 execution·단계·actor/time audit 보존: batch도 단계마다 개별 event·실제 요청자·`now()`를 기록한다.
- 단계 확인 전진-only·순서대로 원칙 유지, `자체 확인`·`제조 완료`·LQC 병행·OQC 인계는 기존 단건 계약 그대로.
- 중단 Pending 열린 실행의 입력 차단, 프로젝트 비활성·패널 취소·scope 밖 차단, stale version 차단 유지.
- 같은 operation id + 같은 payload replay / 다른 payload conflict 계약 유지.
- 선택 Excel 계약·column picker·workbook 불변(선택 집합 read-only 공유).
- 표시명은 Frontend 표시 계층에서만 변경. DB `stage_name`·`is_optional`·집계·내 업무 제목 생성 불변.

## 11. 명시적 제외 (확정)

- 제조 실행 전체 일괄 시작·완료, 중단·재개 일괄 처리, 시작 전 패널 자동 시작
- LQC/OQC/전진검수/FAT·물류·설계의 다중 패널 batch (review 보류 2)
- 부분 성공 API, batch에서의 제조 전체 완료·LQC/OQC 인계 (review 제거 3·4)
- 작업시간·실제 완료시각 소급 입력과 사후 상세값 보정 (Deferred, review 보류 1)
- 완료 실행 되돌리기·기록 삭제·관리자 강제 정정 (review 보류 3)
- 선택 Excel 계약 변경, template 관리 UI·운영 양식 content 재설계
- 대표 repo·`main`, push·PR·merge, Persistent UAT migration/runtime handover, 실제 provider

## 12. 예상 변경 allowlist

- Backend: `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingContracts.cs`, `ManufacturingEndpointExtensions.cs`, `ManufacturingStore.cs`
- DB: `database/migrations/0056_manufacturing_assembly_batch.sql` (파일명은 이 취지의 additive 1건)
- Frontend: `frontend/src/ManufacturingPage.tsx`, `frontend/src/manufacturing.ts`, `frontend/src/api.ts`, `frontend/src/App.tsx`, `frontend/src/styles.css`
- Tests: `backend/tests/Emi.Qms.Api.Tests/`(신규 batch 테스트·migration 테스트), `frontend/tests/ManufacturingPage.test.tsx`, `frontend/tests/App.test.tsx`, `frontend/e2e/full-stack/manufacturing-work.full-stack.spec.ts`
- Docs: Product Roadmap 실험 상태, `docs/27-experiment-task-ledger.md`, Task 산출물 문서

`frontend/src/App.tsx` 등에는 선행 Task의 미커밋 WIP가 있으므로, local commit은 exact allowlist·hunk를 기존 WIP와 안전하게 분리할 수 있을 때만 수행한다(change-001 경계). 사용자 변경을 함께 commit하거나 정리하지 않는다.

## 13. 검증 계획

- Backend 최소 테스트: 2면 이상 batch 성공(선행 단계 포함, 패널별 시작 위치 상이·event/version 증가 수 검증), 혼합 부적격 전체 거부(시작 전·완료·중단·이미 조립 완료·식별 불가·프로젝트 불일치), 권한 없음·scope 밖, stale version, 같은 operation replay·payload/actor 불일치 conflict, 1~500 상한·중복 검증, 부적격 20건 초과 시 count 요약, batch 후 execution `InProgress` 유지·자체 확인/완료/LQC/OQC 미전진, event `batch_operation_id` 상관관계.
- Migration: fresh PostgreSQL에 `0056`까지 적용.
- Frontend 단위: batch bar의 대상/제외 분류·사유, 확인 sheet 내용, 성공 시 선택 clear·실패 시 유지, 조회 전용 미노출, mutation fence; `App.test.tsx`에서 네 표시명 정확 문구와 `KittingCompleted`의 `(선택)` 미표시, 다른 optional 단계 접미사·내 업무 표시 회귀.
- 회귀: Backend 전체 테스트, Frontend lint·typecheck·unit·build.
- isolated Full-Stack: `manufacturing-work.full-stack.spec.ts` 확장 — 여러 패널 batch 조립 → 나머지 자체 확인·제조 완료 단건 → 기존 LQC/OQC 인계 보존 확인. Persistent UAT와 분리된 전용 DB만 사용한다.
- 사용자 검수: 고정 검수 runtime(Frontend `http://127.0.0.1:42983`, Backend `http://127.0.0.1:41166`)에서 desktop·390px로 batch 실행과 네 표시명을 확인하고, 페이지별 desktop/mobile screenshot을 privacy-safe 증빙으로 남긴다. 증빙은 `docs/development/privacy-safe-evidence.md` projection을 따른다.

## 14. 완료 기준과 중단 조건

완료 기준:

- 제조 담당자가 같은 프로젝트의 2개 이상 eligible 패널을 기존 checkbox로 선택해 조립 단계까지 필요한 step을 원자적으로 일괄 확인할 수 있다.
- 서버가 권한·scope·상태·식별·version·멱등성을 전부 재검증하고, 실패 시 부분 반영 없이 행동 가능한 한글 사유(고정 reason code 기반, ≤20건 + count)를 반환한다.
- 같은 operation replay는 저장 projection으로 응답하고 중복 event가 없다. batch event는 `batch_operation_id`로 연결되고 단건 event는 null이다.
- batch 뒤 execution은 `InProgress`이며 실행 완료·work item·LQC/OQC·workflow stage가 전진하지 않는다.
- 네 표시가 정확 문구로 보이고 `KittingCompleted` 접미사가 숨겨지며 내부 stage code·`is_optional`·진행률·내 업무 표시는 회귀 검증으로 불변이다.
- desktop·390px에서 선택·전체선택·Excel tray·batch action bar가 구분되고 horizontal overflow 0이다.
- 13장 자동 검증 전체 통과, Open P0/P1/P2 = 0, 5종 종료 산출물 상태·위치 추적, user validation checklist는 `사용자 검수 대기 — 마지막 일괄 검수`.

중단 조건: Repository source 간 의미 있는 충돌, 기존 보안·권한·workflow 불변조건을 깨지 않고는 구현 불가한 상황, allowlist 밖 변경 필요, PII/secret 노출, 대표 repo·`main`·Persistent UAT·실제 provider 경계 필요 시 구현을 멈추고 보고한다.

## 15. Codex 구현 순서 (review §5 채택)

1. additive `0056`(batch operation table + event correlation)과 migration 테스트
2. Backend contracts·endpoint·store: 구조 검증, 잠금 순서, replay, 단계별 update/event/version, queue additive 필드
3. Backend 성공·권한·scope·혼합 부적격·stale·replay·payload mismatch·경쟁 테스트
4. Frontend additive 타입/API, 선택 분류·batch action bar·확인 sheet·피드백·선택 동기화
5. workflow 표시명 `stageCode` override와 단위 테스트
6. isolated Full-Stack에서 batch → 단건 자체 확인 → 제조 완료 → LQC/OQC 인계 보존 검증
7. desktop·390px screenshot, 전체 regression, Implementation report·Roadmap·원장 갱신과 고정 검수 runtime 확인

openBlockingDecisionCount: 0
