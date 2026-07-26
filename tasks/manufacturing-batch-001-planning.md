# TASK-MANUFACTURING-BATCH-001 — 다중 패널 조립 단계 일괄 완료 기획안 (1차)

> 상태: Draft
> 작성 단계: Codex review 및 Fable 2차 기획 전
> 목적: `experiment/*` fast-track에서 제조 다중 패널 조립 일괄 완료와 프로젝트 전체 흐름 표시명 4건의 구현 계약을 확정하기 위한 1차 기획

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/manufacturing-batch-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 현장에서는 여러 패널의 조립을 한꺼번에 끝내지만, 시스템에서는 패널을 하나씩 열어 같은 단계를 반복 확인해야 한다. 제조팀이 기존 선택 Excel용 패널 checkbox로 여러 패널을 선택해 조립 단계를 한 번에 완료할 수 있어야 한다.
- 대상 사용자·역할: 제조 정·부 담당자(제조 mutation 권한 보유자). 그 외 사용자는 조회 전용을 유지한다.
- 정상 흐름: 제조 작업 화면에서 한 프로젝트의 패널 여러 개를 기존 checkbox로 선택 → `선택 패널 조립 완료` 실행 → 대상 패널의 조립 의미 단계가 한 transaction으로 기록된다.
- 예외·복구 흐름: 권한·scope 부족, 시작 전·완료·중단(Pending) 패널, 이미 조립 완료된 패널, stale version, 동일 operation 재시도는 서버가 재검증해 행동 가능한 한글 사유로 차단하거나 결과를 replay한다.
- 확정한 정책과 명시적 제외: 프로젝트 전체 흐름의 네 표시명을 정확히 변경하되 내부 stage code·완료 계산은 유지한다. 제조 실행 전체 일괄 시작·종료, 중단·재개 일괄 처리, 품질·물류 batch, 선택 Excel 계약 변경, 완료 실행 되돌리기, 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 제외한다.
- planning으로 넘긴 비차단 미결정 사항: interview 5장 7개 항목(조립 단계 식별, 선행 단계 처리, batch 원자성, 비대상 패널 처리, 사후 상세 입력과 audit 경계, operation id·잠금·경쟁, UI 배치·접근성). standing instruction에 따라 사용자에게 재질문하지 않고 이 문서의 권장안 → Codex review → Fable 2차 기획으로 확정한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

제조 담당자가 한 프로젝트의 여러 패널을 기존 checkbox로 선택해 조립 의미 단계를 한 번의 action으로 완료하고, 프로젝트 전체 흐름의 네 단계 표시명이 사용자가 확정한 문구로 보인다.

## 2. 배경과 해결할 업무 문제

- 현재 제조 담당자는 `제조 작업` 화면(`frontend/src/ManufacturingPage.tsx`)에서 패널을 하나씩 chip으로 선택하고, 단건 `check-step` API로 첫 미완료 단계만 순서대로 확인한다. 4단계 기본 template(작업지시·도면 확인 → 자재·부품 확인 → 제조 작업 수행 → 자체 확인) 기준으로 12면 프로젝트의 조립을 기록하려면 패널 전환과 클릭을 수십 회 반복해야 한다.
- 이 화면의 패널 checkbox와 `SelectedExportTray`(`frontend/src/SelectedExcelExport.tsx`)는 현재 선택 Excel 내보내기 전용이며 제조 mutation에는 연결되어 있지 않다.
- 근거 Finding: `TASK-E2E-FULL-SUITE-001 change-006`의 `MULTI-PANEL-REPETITIVE-INPUT-FRICTION` P3. `TASK-011A`는 복수 panel batch 실행을 명시적으로 제외했다.
- 이 기능이 없으면 물리적 batch 작업과 디지털 입력 단위가 어긋나 입력 지연·누락·후행 LQC/OQC 인계 지연이 계속된다.
- 함께 요청된 표시명 변경: 프로젝트 상세 `프로젝트 전체 흐름`에서 `자재 / 키팅 완료 (선택)` → `자재 / 제조 요청`, `물류 / 포장 완료` → `물류 / 포장`, `물류 / 납품 완료` → `물류 / 납품`, `영업 / 세금계산서 완료` → `영업 / 세금계산서`.

확인된 사실(비차단 관찰): 18단계 stage 18의 DB 표시명은 `세금계산서·완료`(`database/migrations/0010_work_items_notifications.sql`)라서 현재 화면 문자열은 `영업 / 세금계산서·완료`로 렌더링된다. 문자열 일치가 아니라 `stageCode` 기준으로 매핑하면 목표 문구는 동일하게 달성되므로 blocking decision으로 올리지 않는다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 제조 정·부 담당자 | 선택 패널 조립 일괄 완료 | 접근 가능한 프로젝트의 제조 queue | `ManufacturingUpdate` 정책 + project scope + `canMutate` 패널의 진행 중 실행 |
| 그 외 부서·조회 사용자 | 결과 조회 | 기존 `ProjectRead` scope | 없음(조회 전용 안내 유지) |
| 프로젝트 상세 열람자 | 전체 흐름 표시 확인 | 기존과 동일 | 없음(표시 문구만 변경) |

Backend가 최종 권한·scope를 강제한다. Frontend의 버튼 노출·disabled 처리는 안내일 뿐 authoritative가 아니다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 정상 일괄 완료

1. 제조 담당자가 제조 작업 화면에서 프로젝트를 열고 패널 checkbox(또는 전체선택)로 진행 중 패널 여러 개를 선택한다.
2. 선택 tray 인접의 `선택 패널 조립 완료 (n면)` action을 누르고 확인 sheet에서 대상 패널 목록을 확인한 뒤 실행한다.
3. 서버가 권한·scope·프로젝트/패널 활성 상태·실행 상태·조립 단계·version을 전부 재검증하고, 한 transaction으로 각 패널의 조립 의미 단계(필요 시 선행 미완료 단계 포함)를 순서대로 확인 처리한다.
4. 화면이 갱신되어 각 패널의 진행 단계 수와 성공 피드백(`n면의 조립 단계를 완료했습니다`)이 action 인접에 표시된다.

### 시나리오 B — 혼합 선택(제외 사전 안내)

1. 담당자가 전체선택으로 완료·중단·시작 전 패널까지 포함해 선택한다.
2. 화면이 대상 가능 패널 수와, 제외되는 패널의 사유(`이미 조립 완료`, `시작 전`, `중단 Pending`, `실행 완료` 등)를 action 인접에 표시한다.
3. 실행 시 대상 가능 패널 목록만 서버로 보내고, 서버는 그 목록을 다시 전부 검증해 하나라도 부적격이면 전체를 409/422로 거부하고 부적격 패널 display code와 사유를 반환한다.

### 시나리오 C — 경쟁·stale·재시도

1. 다른 담당자가 먼저 일부 패널의 단계를 진행하면 version 불일치로 batch 전체가 conflict되고, 화면은 `다른 사용자가 먼저 변경했습니다` 안내와 함께 최신 상태를 다시 불러온다.
2. 네트워크 오류 후 같은 payload 재시도는 같은 operation id로 저장된 결과를 중복 event 없이 replay하고, 같은 operation id에 다른 payload를 쓰면 conflict다.

## 5. 기능 요구사항

### 필수

- [ ] 제조 작업 화면의 기존 패널 checkbox·전체선택 선택 집합을 batch 대상 선택으로 재사용한다(선택 Excel 기능·계약은 그대로 유지).
- [ ] 선택 패널 조립 의미 단계 일괄 완료 API 1개: 단일 프로젝트 범위, `ManufacturingUpdate` 정책, project access scope 재검증.
- [ ] 서버 사전검증: 프로젝트·패널 활성, 실행 `InProgress`, 중단 Pending 없음, 조립 단계 식별 가능, 조립 단계 미완료, 패널별 expected version 일치.
- [ ] 한 transaction의 전부 성공/전부 실패, 결정적 잠금 순서, 패널·단계별 `StepChecked` event와 actor/time 기록, execution version 증가.
- [ ] batch operation id 멱등 replay와 payload fingerprint 불일치 conflict.
- [ ] 성공·실패·제외 사유의 action 인접 한글 피드백(성공 요약, 부적격 패널 display code 나열).
- [ ] 프로젝트 전체 흐름 네 표시명을 `stageCode` 기준으로 정확히 변경(`KittingCompleted` → `자재 / 제조 요청`(`(선택)` 접미사 미표시), `PackingCompleted` → `물류 / 포장`, `DeliveryCompleted` → `물류 / 납품`, `SalesSettlementCompleted` → `영업 / 세금계산서`). 내부 stage code, `is_optional`, 진행률·완료 계산은 변경하지 않는다.
- [ ] desktop과 390px에서 선택·전체선택·선택 Excel·조립 batch action이 구분되고 page-level horizontal overflow가 없다.

### 선택

- [ ] 실행 이력(timeline)에서 같은 batch operation으로 확인된 단계를 식별할 수 있는 표시(예: 이벤트 라벨 접미사). 2차 기획에서 비용 대비 채택 여부 확정.

### 명시적 제외

- [ ] 제조 실행 전체 일괄 시작·완료, 중단·재개 일괄 처리
- [ ] LQC/OQC/전진검수/FAT·물류의 다중 패널 batch
- [ ] 선택 Excel 계약·column picker·workbook 형식 변경
- [ ] 제조 template 관리 UI·운영 양식 content 재설계
- [ ] 완료 제조 실행의 되돌리기·기록 삭제·관리자 강제 정정
- [ ] 조립 이후의 작업시간·상세값 사후 보정 입력 모델(현 데이터 모델에 없음, Deferred)
- [ ] 실제 provider, 대표 repo·`main`, Persistent UAT, push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 제조 작업 (desktop) | 전역 제조 메뉴 → 프로젝트 선택 | 기존 tray 아래에 제조 전용 batch action bar: 대상 가능 n면 / 제외 m면과 사유 요약 | checkbox 선택 → `선택 패널 조립 완료 (n면)` → 확인 sheet에서 대상 목록 확인 → 실행 | 기존 `manufacturing-feedback` 패턴(`role="status"`)으로 성공 요약·오류 사유, 실패 시 부적격 패널 display code 나열 |
| 제조 작업 (390px) | 동일 URL 적응형 | action bar가 tray 아래 세로 적층, 확인은 기존 `MobileSheet` 재사용 | 동일 | 동일 |
| 프로젝트 상세 전체 흐름 | 프로젝트 상세 → Workflow | 네 단계 표시명 변경, 나머지 14단계·상태·진행률 불변 | 조회 | 해당 없음 |

확인할 UX 항목:

- Excel용 선택과 제조 batch가 같은 선택 집합을 쓰므로, 두 action이 시각적으로 명확히 분리되어야 한다(선택 tray는 Excel, 그 아래 제조 action bar는 mutation임을 라벨로 구분).
- batch 실행 전 확인 단계(sheet)에서 대상·제외 패널을 모두 보여줘 오조작을 막는다.
- 권한 없는 사용자는 batch action이 보이지 않거나 조회 전용 안내를 유지한다(기존 `manufacturing-readonly-note` 패턴).
- 저장 중에는 기존 mutation 직렬화 fence(`mutationInFlightRef`)와 동일하게 선택·action을 잠근다.
- disabled 사유는 색상만이 아니라 텍스트로 제공한다(접근성).

## 7. 업무 규칙과 불변조건

- 패널별 execution·단계·actor/time audit를 보존한다. batch도 단계마다 개별 `StepChecked` event를 남기며 시각은 항상 `now()`, actor는 실제 요청자다. 소급 시각 입력은 없다.
- 단계 확인은 전진-only·순서대로 원칙을 유지한다. batch가 이 원칙을 우회하지 않는다(권장안 R2 참조).
- batch 조립 완료는 execution 전체 완료(`Complete`), 제조 업무(work item) 완료, LQC 생성·OQC 인계, panel workflow stage 전이를 대신하지 않는다. `자체 확인`과 `제조 완료`는 기존 단건 흐름 그대로다.
- 중단 Pending이 열린 실행은 batch 대상이 될 수 없다(기존 `InProgress`만 단계 확인 가능 규칙 유지).
- 프로젝트 비활성·패널 취소·scope 밖 프로젝트는 서버가 차단한다.
- 같은 operation id + 같은 payload 재시도는 결과 replay, 다른 payload는 conflict(기존 `panel_manufacturing_operations`·release operation 계약과 동일 의미).
- 표시명 변경은 표시 계층에서만 수행한다. `workflow_stages.stage_code`·`stage_name`(DB)·`is_optional`·집계·내 업무 제목 생성 로직은 변경하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `panel_manufacturing_executions` | 패널별 실행, version, `template_version_id` | 기존 | 변경 없음(version 증가만) |
| `panel_manufacturing_execution_steps` | 순서·표시명 snapshot, 확인자·확인시각 | 기존 | batch가 행 update + event 기록 |
| `panel_manufacturing_events` | `StepChecked` 등 실행 event | 기존 | batch 단계마다 1건씩 |
| 조립 의미 단계 식별 | 실행의 `template_version_id` → `manufacturing_step_template_items`에서 `item_code = 'MANUFACTURING'`인 항목의 `display_order`를 `sequence_number`에 대응 | 기존 데이터로 파생(권장안 R1) | 신규 저장 없음 |
| batch operation replay | batch 1건의 operation id·프로젝트·패널 집합·fingerprint·결과 projection | 신규 테이블(additive `0056`, `panel_manufacturing_release_operations` 패턴 재사용) | append-only |

```text
실행 InProgress + 조립 단계 미확인
  → (batch) 선행 미확인 단계부터 조립 단계까지 순서대로 확인 + 단계별 event + version 증가
  → 실행 InProgress + 조립 단계 확인됨 (자체 확인·제조 완료는 기존 단건 흐름)
실패(권한/scope/상태/식별 불가/stale/경쟁) → 전체 rollback, 부분 반영 없음
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 권한(`QmsPolicies.ManufacturingUpdate`), project access scope, 프로젝트·패널 활성, 실행 상태, 조립 단계 식별·미완료, 패널별 expected version, 멱등성.
- 필요한 조회와 mutation:
  - 조회: 기존 제조 queue 응답(`ManufacturingPanelResponse`)에 additive 필드 추가 제안 — 조립 단계 확인 여부와 식별 가능 여부(예: `assemblyStepChecked: bool?`, null=식별 불가). Frontend 사전 안내용이며 서버 재검증을 대체하지 않는다.
  - mutation: `POST /api/manufacturing/executions/assembly-batch`(경로명은 구현 시 확정) — `{ operationId, projectId, panels: [{ panelId, executionId, expectedVersion }] }`.
- 권한·validation: `ValidateRelease`와 같은 요령으로 빈 목록·중복·empty Guid·상한(release와 동일 500 제안)을 검증하고, 부적격 패널은 display code를 나열한 한글 사유로 반환한다.
- transaction·동시성·idempotency: `LockProjectAsync`로 프로젝트 잠금 → operation replay 확인 → 패널을 id 정렬 순서로 execution row lock(`LockExecutionAsync`/`LockStepsAsync` 재사용) → 전 대상 검증 통과 후에만 update. 전부 성공/전부 실패. 멱등 기록은 release operation 패턴의 신규 테이블 1건(패널 배열·정렬 fingerprint 포함).
- audit trail: 단계별 `StepChecked` event(actor·시각), execution version 증가, batch operation row. 기존 단건과 구분 가능한 근거는 operation 테이블이 담당한다.
- 외부 provider 영향: 없음. 알림·내 업무 생성 없음(조립 단계 확인은 기존 단건에서도 알림을 만들지 않는다).

Repository 조사로 확인한 재사용 대상: `ManufacturingStore`의 `MutateExistingAsync` 구성요소(잠금·replay·version 검증), `ReleaseAsync`의 batch 검증·잠금 순서·operation replay 패턴, `ManufacturingEndpointExtensions.Mutate` 권한 helper. 최종 클래스·컬럼명은 구현 세션에서 확정한다.

## 10. Frontend 고려사항

- route/component: `frontend/src/ManufacturingPage.tsx`에 batch action bar와 확인 sheet 추가. 선택 집합은 기존 `useSelectedRows` 재사용. API client 함수는 `frontend/src/api.ts`·`frontend/src/manufacturing.ts` 계약 추가.
- loading/empty/error/success: 기존 `mutate` helper의 직렬화·저장 중 안내·성공/오류 feedback 패턴을 batch에도 적용. 실행 후 queue·상세 동시 refresh.
- 공통 Action Feedback: 기존 `manufacturing-feedback`(`role="status"`) 유지, 실패 시 부적격 패널 사유를 같은 영역에 표시.
- 접근성: 버튼 disabled 사유 텍스트 제공, 확인 sheet는 기존 `MobileSheet` focus 관리 재사용.
- 390px/mobile/narrow pane: action bar 세로 적층, overflow 0. 표시명 변경은 `frontend/src/App.tsx`의 `displayWorkflowStageName`/`displayWorkflowStageLabel`(및 `(선택)` 접미사 렌더링 지점) `stageCode` 매핑으로 처리한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: batch는 work item·알림·workflow stage를 건드리지 않는다. LQC 병행·OQC 인계는 기존 `Complete` 경로 그대로다.
- 권한/관리자: `ManufacturingUpdate` 정책 재사용, 신규 권한 없음.
- Excel/PDF/첨부: 선택 Excel 계약 불변. 같은 선택 집합을 읽기만 한다.
- Teams/Mail: 영향 없음.
- 삭제·복구/감사: 기존 event·operation 감사 모델의 연장. 신규 삭제 능력 없음.
- Template 관리(`TASK-ADMIN-002`): Active version 불변·`on delete restrict` 덕분에 실행의 `template_version_id` join으로 item code를 안전하게 파생할 수 있다.

## 12. 후보 구현안과 대안 (interview 5장 비차단 정책 7건)

### R1. 조립 의미 단계 식별

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 실행 `template_version_id` → 해당 version의 `item_code='MANUFACTURING'` 항목 `display_order`를 step `sequence_number`에 대응해 파생 | schema 변경 없음, Active version 불변으로 안정, 단일 진실 | `template_version_id`가 null인 과거 실행·`MANUFACTURING` code가 없는 version은 식별 불가 → 해당 패널은 사유와 함께 부적격 처리 |
| B | `0056`에서 step snapshot에 `item_code` 컬럼 추가 + backfill | snapshot 완결성 | backfill도 결국 A와 같은 join에 의존, 변경 면적 증가 |
| C | label 문자열·고정 순서(3번째) 매칭 | 구현 단순 | 표시명 변경·template 개편에 취약, 오완료 위험 |

권장: A. 식별 불가 실행은 자동 추측하지 않고 명시적 사유로 제외한다.

### R2. 선행 단계 미완료 패널 처리

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 선행 미확인 단계부터 조립 단계까지 순서대로 함께 확인 처리(단계별 event 개별 기록, UI에 명시 안내) | 현장 실제(“작업은 한꺼번에 수행”)와 일치, 전진-only·순서 불변조건 보존 | 선행 단계 확인 기록이 batch 시점으로 남음 — event·operation으로 추적 가능하나 개별 확인과 구분해 안내 필요 |
| B | 선행 단계 미완료 패널은 제외 | 기록 보수성 최대 | 현장 사용성 급감, 결국 단건 반복으로 회귀 |

권장: A. 확인 sheet와 성공 피드백에 “선행 단계도 함께 확인 처리됩니다”를 명시한다. 조립만 건너뛰어 기록하는 방식(순서 위반)은 채택하지 않는다.

### R3. batch 원자성

권장: 전부 성공/전부 실패 + 실행 전 사용자 확인 단계. `ReleaseAsync`의 전례(부적격 display code 나열 후 전체 거부)와 동일한 계약이라 예측 가능하고 부분 반영 위험이 없다. 서버 부분 성공 계약은 채택하지 않는다.

### R4. 비대상 패널(완료·중단·시작 전·이미 조립 완료) 처리

권장: Frontend가 queue 데이터(상태 + R1 파생 필드)로 대상 가능/제외를 사전 분류해 제외 사유를 보여주고, 서버에는 대상 가능 목록만 전송한다. 서버는 전송된 목록을 전부 재검증해 하나라도 부적격이면 전체 거부한다. 시작 전 패널의 자동 시작(LQC 생성 부작용 발생)은 포함하지 않는다.

### R5. 사후 상세 입력과 audit 경계

권장: batch는 실제 actor·현재 시각만 기록하고 소급 시각·상세값 입력 모델은 만들지 않는다. “상세 입력은 나중에”는 현 데이터 모델에서 `자체 확인`·`제조 완료`·품질 단계를 이후 개별 수행하는 것으로 충족되며, 작업시간 보정 같은 신규 능력은 명시적 제외(Deferred)로 남긴다.

### R6. operation id·fingerprint·잠금

권장: 신규 batch operation 테이블(additive `0056`) 1건에 operation id·프로젝트·정렬된 패널 배열·fingerprint(정렬된 panelId|executionId|expectedVersion 목록 기반)·결과 projection을 저장한다. 잠금은 프로젝트 row → 패널 id 오름차순 execution row 순서로 고정해 교착을 방지한다. 재시도 replay와 payload 불일치 conflict는 기존 계약 문구를 재사용한다.

### R7. UI 배치·가시성·접근성

권장: 선택 tray(Excel)는 그대로 두고 바로 아래 제조 전용 action bar를 추가한다. 버튼 라벨에 대상 면수를 표기하고, 제외 사유는 텍스트로 병기한다. 실행 전 `MobileSheet` 확인 단계를 desktop·mobile 공통으로 사용한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL·disposable Full-Stack DB에서만 검증한다. 고정 검수 runtime(Frontend `http://127.0.0.1:42983`, Backend `http://127.0.0.1:41166`)은 기존 갱신 정책을 따른다.
- migration 필요 여부: batch operation replay 테이블용 additive `0056` 1건(권장안 채택 시). 기존 migration 수정·번호 재사용 없음. 조립 단계 식별은 R1-A 채택 시 schema 변경 없음.
- 외부 발송/실제 데이터 영향: 없음.
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo·Persistent UAT는 기존 미승인 상태 유지. local commit은 기존 dirty WIP(현재 `frontend/src/App.tsx` 등 다수 미커밋 변경)와 겹치므로, 표시명 변경이 `App.tsx`를 수정하는 특성상 exact allowlist를 기존 WIP와 안전하게 분리할 수 있을 때만 commit한다(interview `localCommitApproved` 조건).

## 14. 검증 계획

- 최소 테스트(Backend): batch 성공(2면 이상), 선행 단계 포함 확인, 부적격 혼합 전체 거부(시작 전·완료·중단·이미 조립 완료·식별 불가), scope 밖·권한 없음, stale version conflict, 같은 operation replay·다른 payload conflict, 상한·중복 검증. fresh PostgreSQL에 `0056`까지 적용(`backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs` 계열).
- 최소 테스트(Frontend): `frontend/tests/ManufacturingPage.test.tsx`에 대상/제외 분류·확인 sheet·성공/실패 피드백·조회 전용 미노출, `frontend/tests/App.test.tsx`에 네 표시명(정확 문구·`(선택)` 미표시) 단언.
- 영향 영역 회귀: Backend 전체 test, Frontend lint·typecheck·unit·build, isolated Full-Stack `frontend/e2e/full-stack/manufacturing-work.full-stack.spec.ts` 확장(batch 조립 → 단건 자체 확인 → 완료 → LQC/OQC 인계 유지 확인).
- PR/CI: 이번 Task 범위에서는 local 검증까지. 게시 gate는 별도 승인.
- 사용자 검수: 고정 검수 runtime에서 desktop·390px로 batch 실행과 네 표시명을 직접 확인. 페이지별 desktop/mobile screenshot을 Task 산출물로 남긴다.

## 15. 완료 기준

- 기능/권한/데이터: 8장 성공 기준 전체 — 2개 이상 패널 일괄 완료, 서버 전면 재검증, 전부 성공/전부 실패, 멱등 replay, 실행 완료·LQC·OQC 임의 전진 없음, 표시명 4건 정확 일치·내부 계산 불변.
- UX: desktop·390px에서 선택·Excel·batch 구분, overflow 0, 행동 가능한 한글 피드백.
- 자동 테스트: 위 14장 항목 통과, Open P0/P1/P2 = 0.
- 5종 산출물: `docs/12-task-completion-policy.md`에 따라 위치·상태 추적.
- 사용자 검수 상태: `사용자 검수 대기`로 handoff(자동 검증과 분리 기록).
- PR 상태: 해당 없음(게시 미승인).

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| - | 없음 — interview 5장 7건은 standing instruction에 따라 본 문서 권장안(R1~R7)으로 제시했고 Codex review 후 Fable 2차 기획에서 확정한다 | 12장 참조 | 재질문 없음 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Manufacturing/`의 contracts·endpoints·store(신규 batch mutation, queue 응답 additive 필드)
- Frontend: `frontend/src/ManufacturingPage.tsx`, `frontend/src/manufacturing.ts`, `frontend/src/api.ts`, `frontend/src/App.tsx`(표시명 매핑), 관련 CSS
- DB/Migration: `database/migrations/0056_*`(batch operation 테이블, 권장안 채택 시)
- Tests/Scripts: `backend/tests/Emi.Qms.Api.Tests/`, `frontend/tests/ManufacturingPage.test.tsx`, `frontend/tests/App.test.tsx`, `frontend/e2e/full-stack/manufacturing-work.full-stack.spec.ts`
- Docs: Product Roadmap 실험 상태, 실험 Task 완료 원장, Task 산출물 문서

## 18. Roadmap 연결

- 선행 Task: `TASK-011A`(실행·단계·중단), `TASK-ADMIN-002`(template version snapshot), `TASK-EXPORT-001`(선택 집합 UI) — 모두 `EXPERIMENT_COMPLETE`, 재구현하지 않음.
- 후속 Task: 다른 부서 다중 패널 batch(품질·물류)는 별도 요청 시 별도 Task. 작업시간 등 사후 상세 입력 모델은 Deferred.
- 현재 Go/No-Go: 사용자 명시 요청 기반 `explicitRoadmapOverrideApproved: true`, gate `PASS_CREATE`. canonical `Next Gate`(첨부·사진 storage)는 변경하지 않는다.
- 별도 Task로 분리할 항목: 첨부·사진 storage, 운영 전환(원장 우선순위 1·2 유지).

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-24 | 사용자: 네 표시명 변경과 다중 패널 조립 일괄 완료 명시 요청(fast-track, 권장안 자동 채택) | 본 1차 기획 전체 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

(experiment 2-pass: 위 승인은 Codex review와 Fable 2차 기획의 blocking decision 0 조건으로 대체되며, main merge·Persistent UAT·게시 승인은 부여되지 않는다.)

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 0
