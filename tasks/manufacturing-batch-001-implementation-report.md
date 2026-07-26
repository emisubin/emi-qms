# TASK-MANUFACTURING-BATCH-001 제조 조립 일괄 완료 구현 보고

## 상태

- taskType: `NEW_FEATURE`
- branch: `experiment/task-home-002-personalized-shell`
- implementation: `완료`
- automaticValidation: `완료 — 영향 범위·Frontend 전체·isolated Full-Stack`
- userValidation: `대기 — 마지막 일괄 검수`
- commit: `완료 — 사용자 승인에 따라 선행 Task와 함께 누적 experiment checkpoint에 포함`
- push / PR / merge: `미승인·미실행`
- main merge approval: `0/3`
- Persistent UAT / 실제 provider / 대표 repo 영향: `없음`

## Task 목적·기획 source

제조팀이 여러 패널을 실제로 한 번에 조립한 뒤 패널마다 같은 입력을 반복하지 않도록, 기존 선택 Excel의 checkbox를 그대로 이용해 **조립 단계 한 항목만** 일괄 확인한다. 조립 전·후 다른 제조 단계는 그대로 남는다. 프로젝트 전체 흐름의 네 단계는 사용자 지정 문구로 단순화하되 내부 stage code·진행률·업무·알림 의미는 바꾸지 않는다.

초기 구현 source는 Fable 2차 기획 [42-manufacturing-batch-assembly-plan.md](../docs/42-manufacturing-batch-assembly-plan.md)이며, 사용자의 의미 정정 [Change 002](manufacturing-batch-001-change-002.md)가 선행 단계 자동 완료 부분을 대체하는 최신 계약이다. Change 002는 사용자 지시에 따라 Claude/Fable 호출 없이 Codex가 직접 구현했다. [1차 기획](manufacturing-batch-001-planning.md), [Codex review](manufacturing-batch-001-review.md), [fast-track·사용량 기록](manufacturing-batch-001-change-001.md)은 판단 이력으로 분리 보존했다.

## 해결한 업무 문제

- 같은 프로젝트의 여러 패널을 한 번에 조립했을 때 패널별 조립 단계를 반복 클릭해야 했다.
- 사용자는 Excel 내보내기용으로 이미 선택한 패널을 제조 작업에서 다시 선택해야 했다.
- 기존 전체 흐름 문구는 `완료`와 `(선택)`을 포함해 실제 담당자의 다음 행동을 빠르게 읽기 어려웠다.
- 다중 변경에서 일부 패널만 저장되면 현장 실제 작업과 시스템 기록이 달라질 위험이 있었다.

## 포함·제외 범위

포함:

- 같은 프로젝트의 기존 선택 checkbox·전체선택·선택 Excel과 제조 일괄 action의 선택 상태 공유
- 제조 진행 중이고 조립 미완료인 패널만 처리 대상으로 분류
- immutable 제조 template version의 `item_code='MANUFACTURING'`으로 조립 의미 단계 식별
- 선택한 각 패널의 조립 단계 한 건만 한 transaction에서 확인
- 조립 전 미입력 단계와 조립 후 자체검사 상태 보존
- 대상 전체 성공 또는 전체 rollback, expected version 충돌 차단, operation replay
- 새로 확인한 단계마다 기존 `StepChecked` event·actor/time·execution version 증가
- batch operation과 조립 단계 event의 감사 상관관계
- Desktop·390px 공통 확인 sheet와 대상·제외·단계 수·제외 사유
- 전체 흐름 표시명 `자재 / 제조 요청`, `물류 / 포장`, `물류 / 납품`, `영업 / 세금계산서`

제외:

- 제조 시작 전 패널 자동 시작
- 제조 자체검사·제조 전체 완료·LQC/OQC 인계의 일괄 처리
- 작업시간 소급 입력, 완료 정정·되돌리기, 품질·물류의 다중 패널 처리
- Backend stage code·seed 이름·선택 단계 정책, 내 업무·알림 제목 변경
- Persistent UAT migration·runtime handover, 실제 provider, 대표 repo·`main`, push·PR·merge

## 전체 아키텍처와 영향

### DB·Migration

- additive `0056_manufacturing_assembly_batch.sql`이 batch operation receipt를 보관한다.
- `panel_manufacturing_events.batch_operation_id` nullable FK로 기존 단건 event는 그대로 두고 batch event만 묶는다.
- 프로젝트 영구 삭제 순서에 batch operation 삭제를 추가해 새 restrict FK와 기존 purge가 충돌하지 않게 했다.
- destructive down migration은 제공하지 않는다. 운영 적용이 필요하면 별도 UAT Task에서 backup과 forward-fix를 승인받아야 한다.

### Backend·API·권한

- `POST /api/manufacturing/executions/assembly-batch`
- 권한: 기존 `ManufacturingUpdate`
- 요청: operation id, project id, panel id·execution id·expected version 목록
- 검증: active project·project scope·active panel·`InProgress` execution·open Pending 없음·현재 version·조립 단계 유일 식별·미완료
- 처리: project → panel id 정렬 execution → step 순서로 잠근 뒤 전 대상을 검증하고, 실패가 없을 때만 각 패널의 조립 step 한 건·event·version·receipt를 commit한다.
- replay는 최초 저장한 완료 패널 수와 단계 수를 반환하며 event를 중복 생성하지 않는다.
- batch 뒤 execution은 `InProgress`로 남는다. 조립 뒤 자체검사·완료는 기존 패널별 흐름을 그대로 사용한다.

### Frontend·UI·UX

- 제조 화면의 `SelectedExportTray`와 같은 선택 집합을 사용한다.
- Excel action과 `조립 단계 일괄 완료` action을 별도 영역으로 보여 동일 선택이 다른 결과를 낸다는 점을 구분한다.
- 시작 전·중단·완료·조립 완료·단계 식별 불가 패널은 사유와 함께 이번 처리에서 제외한다.
- 확인 sheet에 선택 수, 처리 가능 수, 조립 처리 단계 수, 제외 수와 정확한 대상을 표시한다.
- `선택 패널 조립 단계 완료`와 “다른 제조 단계는 유지” 안내로 전체 제조 완료와 구분한다.
- 성공하면 선택을 비우고 queue/detail을 새로 읽는다. 실패하면 선택을 유지한다.
- 좁은 화면에서는 같은 기능을 한 열 sheet와 패널별 목록으로 재배치한다.
- 전체 흐름 명칭은 `stageCode` 기반 표시 override로만 변경해 내부 workflow와 다른 화면 문구의 의미를 보존한다.

### Excel·PDF·첨부·기존 workflow

- 선택 Excel의 checkbox·전체선택·내보내기 동작은 유지한다.
- Excel/PDF/첨부 파일 형식 변경은 없다.
- 조립 batch는 제조 완료, LQC/OQC, 패널 workflow stage, 제조 업무 완료를 직접 바꾸지 않는다.

## 실제 변경 파일과 역할

- `database/migrations/0056_manufacturing_assembly_batch.sql`: batch receipt·event correlation schema
- `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingContracts.cs`: batch API 계약과 queue 조립 상태
- `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingEndpointExtensions.cs`: 권한이 적용된 batch endpoint
- `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingStore.cs`: 의미 단계 식별, 원자 처리, replay, 감사 event
- `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`: 새 restrict FK를 고려한 영구 삭제 순서
- `backend/tests/Emi.Qms.Api.Tests/ManufacturingAssemblyBatchApiTests.cs`: queue 식별·권한·원자성·replay·조립 단일 단계 범위
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`: `0056` fresh schema 검증
- `frontend/src/manufacturing.ts`, `frontend/src/api.ts`: Frontend 계약·API client
- `frontend/src/ManufacturingPage.tsx`: 선택 공유·대상 분류·확인·실행·feedback
- `frontend/src/App.tsx`: 전체 흐름 네 표시명 override
- `frontend/src/styles.css`: Desktop·Mobile 일괄 action·확인 sheet
- `frontend/tests/ManufacturingPage.test.tsx`, `frontend/tests/App.test.tsx`: 선택 흐름과 정확 문구
- `frontend/e2e/full-stack/manufacturing-work.full-stack.spec.ts`: 실제 DB를 이용한 두 패널 batch와 후속 LQC 인계

## 기술적 결정과 검토한 대안

1. 조립은 label 문자열이나 고정 3번째 단계가 아니라 실행에 고정된 template version의 semantic item code로 찾는다. 관리자가 label·순서를 바꿔도 다른 단계를 잘못 완료하지 않는다.
2. Frontend는 선택 중 처리 불가 패널을 설명과 함께 제외하고, 서버에 보낸 대상은 전부 성공 또는 전부 실패시킨다. 부분 성공 API보다 현장 기록과 재시도 의미가 명확하다.
3. 사용자 의미 정정에 따라 batch는 조립 단계 한 건만 확인한다. 조립 전·후 미입력 단계는 그대로 남아 나중에 기존 순서 입력으로 보완할 수 있다.
4. 기존 단건 operation table에 여러 execution을 억지로 넣지 않고 batch 전용 receipt와 nullable event FK를 추가했다.
5. 별도 desktop modal 대신 기존 접근성·focus 계약을 가진 `MobileSheet`를 Desktop과 Mobile에서 공통 사용한다.

## 시행착오 및 폐기한 접근

- 첫 isolated E2E에서 Mobile에서 의도적으로 숨긴 선택 요약 문구의 `visible`을 단언해 실패했다. 실제 기능 문제가 아니라 viewport별 표현 차이였으며, 성공 뒤 두 checkbox가 해제됐는지를 직접 확인하도록 테스트를 수정했다.
- 초기 계약에서 “조립 단계까지”를 누적 완료로 해석해 선행 제조 단계도 확인했다. 사용자가 중간 단계가 사라지는 문제를 지적해 Change 002에서 조립 단계 한 항목만 확인하도록 수정했다.
- Change 002 뒤 Frontend 전체 첫 실행에서 범위 밖 알림 감사 화면의 비동기 응답이 늦어 `128/129`가 됐다. 해당 test 단독 `1/1`과 전체 재실행 `129/129`가 통과해 제품 변경 없이 일시적 test timing으로 확인했다.
- fixed label·sequence로 조립을 찾는 안은 template 변경 시 잘못된 단계를 처리할 수 있어 폐기했다.
- batch에서 제조 완료와 LQC 인계를 함께 실행하는 안은 패널별 자체검사 계약을 우회하므로 제외했다.

## 실행한 검증과 결과

- Backend Release build: 성공, warning `0`, error `0`
- Backend batch integration: `1/1` 성공
  - queue 조립 단계 projection, 영업 역할 `403`, 2패널·조립 단계 2건 확인
  - 조립 전 단계와 자체검사 미완료 유지
  - event correlation 2건, version `1→2`, replay 중복 없음
  - 이미 조립 완료+신규 패널 혼합 요청 `409`와 신규 패널 전체 rollback
- Backend migration + batch 영향 테스트: `2/2` 성공
- Frontend lint: error `0`, 기존 `main.tsx` Fast Refresh warning `1`
- Frontend typecheck: 성공
- Frontend unit 전체: `19 files`, `129/129` 성공
- Frontend production build: 성공, 기존 chunk-size warning
- isolated Full-Stack 제조 batch: `1/1` 성공
  - 2패널 batch 뒤 조립 step만 완료되고 다른 단계는 미완료인 DB 상태 확인
  - 남은 단계와 제조 완료는 패널별로 유지
  - 패널별 LQC 업무 `2건` 생성
  - Desktop·390px horizontal overflow `0`
- Backend 전체 Release 회귀: Change 002 반영 후 `424/424` 성공, `8분 21초`
- 고정 검수 runtime: Frontend root·proxy ready `200`, Backend live·ready `200`, `0056` 적용 로그와 신규 endpoint authorization route 확인

## 시각 증빙

- [Desktop 1440 확인 sheet](manufacturing-batch-001-screenshots/01-assembly-batch-confirm-desktop-1440.png)
- [Mobile 390 확인 sheet](manufacturing-batch-001-screenshots/02-assembly-batch-confirm-mobile-390.png)
- [Mobile 390 완료·선택 해제](manufacturing-batch-001-screenshots/03-assembly-batch-success-mobile-390.png)

모든 증빙은 synthetic project·role·panel만 사용하며 개인정보·고객정보·secret을 포함하지 않는다.

## Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `MFG-BATCH-PARTIAL-WRITE` | P1 | `RESOLVED` | 다중 패널 중 일부만 저장되면 실제 작업과 기록이 불일치 | 대상 전체 검증 뒤 단일 transaction commit, 혼합 부적격 rollback 테스트 |
| `MFG-BATCH-SEMANTIC-STEP` | P1 | `RESOLVED` | label·고정 순서 추측은 다른 제조 단계를 완료할 수 있음 | immutable template version의 `MANUFACTURING` item code와 snapshot sequence 대조 |
| `MFG-BATCH-AUDIT-CORRELATION` | P2 | `RESOLVED` | 단계 event만으로 batch 단위를 직접 추적하기 어려움 | operation receipt와 nullable event FK |
| `MFG-BATCH-REPLAY` | P2 | `RESOLVED` | 재시도로 단계 event·version이 중복될 수 있음 | operation id·payload fingerprint·저장 projection replay |
| `MFG-BATCH-PURGE-FK` | P2 | `RESOLVED` | 새 restrict FK가 프로젝트 영구 삭제를 막을 수 있음 | event 삭제 뒤 batch receipt를 삭제하도록 purge 순서 보강 |
| `MFG-BATCH-MOBILE-HIDDEN-SUMMARY-ASSERTION` | P3 | `RESOLVED` | E2E가 Mobile에서 숨긴 요약 문구의 가시성을 잘못 기대 | 실제 성공 결과인 checkbox 선택 해제를 단언 |
| `MFG-BATCH-PREDECESSOR-OVERCOMPLETION` | P1 | `RESOLVED` | “조립 단계까지”를 누적 완료로 구현해 미입력 중간 단계가 사라짐 | Change 002에서 semantic 조립 step 한 건만 처리하고 다른 단계 미완료를 Backend·Full-Stack에서 단언 |

Open P0/P1/P2: `0/0/0`.

## 개인정보·secret 검토

- 코드·문서·screenshot에는 synthetic identifier와 역할명만 사용했다.
- 실제 사용자 이름, 이메일, tenant/client id, token, password, webhook, Authorization header를 기록하지 않았다.
- 실제 Teams·메일 provider를 호출하지 않았다.

## SOP — 실험 검수와 복구

1. Repository root의 `사용자-검수-서버-실행.command`를 실행한다.
2. Frontend `http://127.0.0.1:42983`, Backend `http://127.0.0.1:41166/health`를 확인한다.
3. 제조 역할로 제조 화면을 열고 같은 프로젝트에서 조립 전 패널 2개 이상을 선택한다.
4. 선택 Excel은 그대로 동작하는지 확인하고 `선택 패널 조립 단계 완료`를 누른다.
5. 확인 sheet의 대상·제외·조립 처리 단계 수와 “다른 제조 단계 유지” 안내를 확인한 뒤 저장한다.
6. 완료 뒤 선택이 해제되고 각 패널은 `InProgress`이며 조립 전·후 다른 단계가 남아 있는지 확인한다.

오류 시 선택은 유지된다. stale·중단·이미 완료 안내를 확인하고 새로고침한 뒤 다시 선택한다. `0056`이 아직 운영에 적용되지 않았으므로 현재 rollback은 코드·migration을 임의로 내리는 것이 아니라 실험 runtime을 중지하고 이전 reachable experiment commit으로 복구하는 방식이다. 운영 승격 뒤 schema 문제가 생기면 down migration 대신 backup 확인 후 별도 forward-fix migration을 사용한다.

## User manual — 제조 담당자 사용법

1. `제조` 메뉴에서 프로젝트를 선택한다.
2. Excel 내보내기 때 쓰는 패널 checkbox로 실제 조립을 끝낸 패널을 선택한다. 헤더 checkbox로 현재 프로젝트 패널 전체를 선택할 수도 있다.
3. `조립 단계 일괄 완료` 영역에서 처리 가능·제외 수를 확인한다.
4. `선택 패널 조립 단계 완료`를 누르고, 조립 단계만 완료되며 다른 단계는 유지된다는 안내와 대상 목록을 확인한다.
5. `n면 조립 단계 완료`를 한 번 누른다. 성공하면 선택이 자동으로 해제된다.
6. 조립 전 미입력 단계와 조립 후 자체검사·제조 완료는 기존처럼 각 패널에서 입력한다.

시작 전·중단·완료·이미 조립 완료 패널은 일괄 처리 대상에서 제외된다. 실패하면 선택이 남으므로 안내 사유를 해소한 뒤 다시 시도한다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 privacy-safe screenshot은 완료했다.
- 사용자 직접 검수는 [사용자 검수 체크리스트](manufacturing-batch-001-user-validation-checklist.md) 기준으로 마지막 일괄 검수 대기다.
- Persistent UAT·실제 provider·대표 repo·`main`은 미적용이다.
- 같은 파일에 있던 선행 Task WIP와 함께 exact allowlist·충돌·privacy 검토를 거쳐 누적 experiment checkpoint에 포함했다.

## 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | 이 문서 `SOP — 실험 검수와 복구` | 작성 완료 |
| User manual | 이 문서 `User manual — 제조 담당자 사용법` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` TASK-MANUFACTURING-BATCH-001 row·section·Decision Log | 구현·local commit 완료 상태 기록 |
| User validation checklist | [manufacturing-batch-001-user-validation-checklist.md](manufacturing-batch-001-user-validation-checklist.md) | 자동 검증 완료·사용자 검수 대기 |
