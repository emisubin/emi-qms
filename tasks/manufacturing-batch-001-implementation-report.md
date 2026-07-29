# TASK-MANUFACTURING-BATCH-001 제조 단계 선택 일괄 완료 구현 보고

## 상태

- taskType: `NEW_FEATURE` 본체 + `BUGFIX` Change 002·003
- branch: `experiment/task-home-002-personalized-shell`
- latestChange: `Change 003 — 모든 제조 단계 일괄 완료`
- implementation: `완료`
- automaticValidation: `완료`
- userValidation: `대기 — 마지막 일괄 검수`
- latestChangeCommit: `e6f3fa6 — 누적 실험 checkpoint에 포함`
- push / PR / merge: `미승인·미실행`
- main merge approval: `2/3 — 3차 승인 전 merge 금지`
- Persistent UAT / 실제 provider / 대표 repo 영향: `없음`

## 최신 업무 계약

제조 담당자는 같은 프로젝트의 여러 패널을 기존 선택 Excel checkbox로 선택하고, 제조 양식에 등록된 **모든 단계 중 완료할 단계 한 건**을 골라 한 번에 완료할 수 있다. 선택한 단계 앞뒤의 다른 제조 단계는 그대로 남으며, 제조 실행 전체 완료·LQC/OQC 인계도 자동으로 수행하지 않는다.

초기 Fable 2차 기획 [42-manufacturing-batch-assembly-plan.md](../docs/42-manufacturing-batch-assembly-plan.md)은 조립 중심의 최초 판단 이력이다. 사용자 정정 [Change 002](manufacturing-batch-001-change-002.md)는 선행 단계 자동 완료를 제거했고, 최신 [Change 003](manufacturing-batch-001-change-003.md)은 “한 단계만”을 “조립 단계만”으로 잘못 좁힌 해석을 대체한다. Change 002·003은 사용자 지시에 따라 Claude/Fable 추가 호출 없이 Codex가 구현했다.

## 원인과 해결

### 확인된 원인

1. 제조 양식에 `General / Assembly` 역할을 저장하고 `Assembly`는 한 항목만 허용했다.
2. 일괄 API는 immutable template의 조립 의미 항목 또는 project snapshot의 `Assembly` 역할을 찾아 그 단계만 완료했다.
3. 따라서 앞뒤 단계가 보존되는 안전성은 맞았지만, 조립이 아닌 작업지시·배선·자체확인 같은 단계는 일괄 처리할 수 없었다.

### 해결 결과

1. 제조 양식의 사용자 계약에서 `일반/조립` 구분을 제거했다. 양식은 순서와 단계명만 관리한다.
2. 새 API `POST /api/manufacturing/executions/step-batch`는 대상 단계의 순번과 단계명을 받는다.
3. Frontend는 선택 패널의 제조 단계 목록을 합쳐 단계 선택 dropdown을 제공한다.
4. 선택한 단계가 존재하고 미완료인 패널만 처리 대상으로 분류하며, 이미 완료했거나 같은 순번의 이름이 다른 패널은 사유와 함께 제외한다.
5. 서버는 전달된 모든 패널에서 같은 순번·같은 이름의 미완료 단계가 있는지 다시 검증한 뒤 한 transaction으로 전부 성공 또는 전부 rollback한다.
6. 각 패널에는 선택 단계 `StepChecked` event 한 건과 execution version `+1`만 기록한다.

## 포함·제외 범위

포함:

- 제조 양식의 `일반/조립` 입력·조회·validation 제거
- 모든 제조 단계의 선택 일괄 완료
- 기존 패널 checkbox·전체선택·선택 Excel과 선택 상태 공유
- 권한, project scope, active panel, `InProgress`, open Pending, expected version 검증
- 단계 순번+표시명 일치 검증
- 전부 성공/전부 실패 transaction, operation replay, event correlation
- Desktop·390px 단계 선택 sheet와 대상·제외 사유
- 기존 프로젝트·실행·단계·완료 이력 보존

제외:

- 제조 시작 전 패널 자동 시작
- 선택 단계 앞뒤의 자동 완료
- 제조 실행 전체 완료, 제조 업무 완료, LQC/OQC·workflow stage 자동 전진
- 완료 정정·되돌리기, 작업시간 소급 입력
- 품질·물류의 다중 패널 일괄 처리
- 대표 repo·`main`, Persistent UAT, 실제 provider, push·PR·merge

## 데이터·호환성 결정

- 새 migration은 추가하지 않았다.
- 과거 migration의 `step_role` 컬럼과 `panel_manufacturing_assembly_batch_operations` 테이블명은 이미 생성된 project snapshot·receipt 호환을 위해 물리적으로 유지한다.
- 신규 제조 양식 항목은 내부 호환 기본값 `General`로만 저장하며, UI·API 응답·일괄 판정에서는 이 값을 사용하지 않는다.
- 기존 `Assembly` 값은 과거 기록으로 보존하지만 새 일괄 처리 결과에 영향을 주지 않는다.
- receipt payload fingerprint에는 대상 단계 순번·표시명이 포함되므로 같은 operation id를 다른 단계에 재사용할 수 없다.

## Backend·API

- Endpoint: `POST /api/manufacturing/executions/step-batch`
- 요청: operation id, project id, 대상 단계 순번·표시명, panel id·execution id·expected version 목록
- 권한: 기존 `ManufacturingUpdate`
- 원자성: project 잠금 → panel id 정렬 execution·step 잠금 → 전체 검증 → 선택 step만 update
- replay: 같은 operation id·동일 payload는 저장 결과 replay, 다른 payload는 `409`
- 안전 오류: 단계 없음/이름 변경, 이미 완료, Pending, 진행 중 아님, stale version을 panel display code와 함께 안내
- queue: project 상세 조회에서 각 패널의 제조 단계 순번·이름·완료 여부를 반환한다. 프로젝트 목록 조회에서는 payload를 늘리지 않도록 빈 목록을 반환한다.

## Frontend·UX

- `제조 단계 일괄 완료` 영역에서 `완료할 단계 선택`을 누른다.
- 확인 sheet에서 `n단계 · 단계명 (m면 가능)` 형식으로 단계를 고른다.
- 선택·처리 가능·선택 단계·제외 수를 한 줄 KPI로 표시한다.
- “선택한 제조 단계 한 건만 완료하며 앞뒤 단계는 유지”됨을 명시한다.
- 성공하면 선택을 비우고 queue/detail을 다시 읽는다. 실패하면 선택을 유지한다.
- 제조 양식 화면의 헤더는 `No. / 제조 항목 / 관리`만 남고, 등록한 모든 단계가 일괄 완료 대상임을 안내한다.

## 실제 변경 파일

- `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingContracts.cs`
- `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingStore.cs`
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionControlTemplateContracts.cs`
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionControlTemplateStore.cs`
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningContracts.cs`
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningStore.cs`
- `frontend/src/ManufacturingPage.tsx`
- `frontend/src/ProductionControlTemplateWorkspace.tsx`
- `frontend/src/api.ts`
- `frontend/src/manufacturing.ts`
- `frontend/src/productionControlTemplates.ts`
- `frontend/src/projects.ts`
- `frontend/src/styles.css`
- 관련 Backend·Frontend·Full-Stack tests와 Task 문서

## 검증 결과

- Backend Release build: 성공, warning `0`, error `0`
- Backend 집중 integration: `3/3`
  - 제조 단계 일괄 API가 양식의 모든 단계를 각각 처리
  - 선택 단계 외 다른 단계 유지
  - 권한, replay, event correlation, 혼합 부적격 rollback
  - 제조 양식 현재값 저장과 새 프로젝트 snapshot 회귀
- Backend 전체 Release 회귀: `427/427`, 실패 `0`
- Frontend typecheck: 성공
- Frontend lint: error `0`, 기존 `main.tsx` Fast Refresh warning `1`
- Frontend 집중: `7/7`
- Frontend 전체: `22 files`, `140/140`
- Frontend production build: 성공, 기존 large chunk warning
- isolated Full-Stack: `1/1`
  - 2개 패널에서 2단계만 일괄 완료
  - 1·3·4단계 미완료 유지
  - event `2건`, version 패널별 `+1`, execution `InProgress`
  - Desktop·390px horizontal overflow `0`
- `git diff --check`: 통과

## 시각 증빙

- [Desktop 1440 단계 선택 확인 sheet](manufacturing-batch-001-screenshots/04-step-batch-confirm-desktop-1440.png)
- [Mobile 390 단계 선택 확인 sheet](manufacturing-batch-001-screenshots/05-step-batch-confirm-mobile-390.png)
- [Mobile 390 완료·선택 해제](manufacturing-batch-001-screenshots/06-step-batch-success-mobile-390.png)

증빙은 isolated synthetic project·panel·role만 사용하며 실제 고객·개인정보·secret을 포함하지 않는다.

## Finding gate

| Finding | Severity | 상태 | 해소 |
| --- | --- | --- | --- |
| `MFG-BATCH-PREDECESSOR-OVERCOMPLETION` | P1 | `RESOLVED` | Change 002에서 대상 한 단계 외 앞뒤 단계를 변경하지 않도록 수정 |
| `MFG-BATCH-ASSEMBLY-ONLY-MISINTERPRETATION` | P1 | `RESOLVED` | Change 003에서 역할 구분을 제거하고 모든 제조 단계를 선택 가능하게 수정 |
| `MFG-BATCH-CROSS-TEMPLATE-MISMATCH` | P1 | `RESOLVED` | 순번과 표시명을 서버에서 함께 대조하고 불일치 payload 전체 rollback |
| `MFG-BATCH-PARTIAL-WRITE` | P1 | `RESOLVED` | 전체 사전검증 뒤 단일 transaction commit |
| `MFG-BATCH-REPLAY` | P2 | `RESOLVED` | 대상 단계가 포함된 fingerprint와 저장 projection replay |
| `MFG-BATCH-AUDIT-CORRELATION` | P2 | `RESOLVED` | 기존 nullable batch operation FK 유지 |

Open P0/P1/P2: `0/0/0`.

## SOP — 실험 검수와 복구

1. Repository root의 `사용자-검수-서버-실행.command`를 실행한다.
2. Frontend `http://127.0.0.1:42983`, Backend `http://127.0.0.1:41166/health`를 확인한다.
3. 제조 역할로 같은 프로젝트의 제조 진행 패널 2개 이상을 선택한다.
4. `완료할 단계 선택`을 누르고 제조 단계 dropdown에서 원하는 단계 한 건을 고른다.
5. 처리 가능·제외 패널과 “다른 단계 유지” 안내를 확인한 뒤 저장한다.
6. 선택한 단계만 완료되고 execution이 계속 `InProgress`인지 확인한다.

오류 시 선택은 유지된다. stale·Pending·이미 완료·단계 불일치 안내를 확인하고 최신 상태를 다시 불러온 뒤 재시도한다. Change 003은 schema를 바꾸지 않았으므로 복구는 현재 실험 코드를 이전 reachable experiment commit으로 되돌리는 방식이며, 대표 repo나 `main`을 수정하지 않는다.

## User manual — 제조 담당자 사용법

1. `제조` 메뉴에서 프로젝트를 연다.
2. `패널 선택 작업`을 누른다.
3. Excel 내보내기와 같은 checkbox로 함께 처리할 패널을 선택한다.
4. `완료할 단계 선택`을 누른다.
5. dropdown에서 실제로 끝낸 제조 단계를 선택한다.
6. 대상·제외 사유를 확인하고 `n면 단계명 완료`를 한 번 누른다.
7. 성공 뒤 선택이 해제되고, 선택 단계 외 다른 제조 단계는 기존처럼 개별 또는 다음 일괄 처리에서 입력한다.

## 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | Change 003 반영 완료 |
| SOP | 이 문서 `SOP — 실험 검수와 복구` | 작성 완료 |
| User manual | 이 문서 `User manual — 제조 담당자 사용법` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` | Change 003 기준 동기화 |
| User validation checklist | [manufacturing-batch-001-user-validation-checklist.md](manufacturing-batch-001-user-validation-checklist.md) | 자동 검증 완료·사용자 검수 대기 |
