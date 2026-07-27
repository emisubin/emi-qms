# TASK-WORKFLOW-CONTINUITY-001 Change 016 구현 보고

상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`

## 해결한 업무 문제

1. 포장까지는 패널을 선택할 수 있었지만 출발·납품 queue가 Packing Unit 한 건으로 합쳐져 같은 포장 묶음의 모든 패널을 함께 처리해야 했다.
2. Backend batch 요청과 active unique 기준도 `unitId`여서 화면만 패널 행으로 바꾸면 선택하지 않은 패널까지 함께 출발·납품되는 구조였다.
3. 물류 이력·프로젝트 흐름·정산·UL891 월별 출하 근거가 기존 batch–unit 관계를 직접 읽어 패널 부분 출하를 정확히 구분할 수 없었다.

## 구현 결과

1. 출발·납품 대기 목록을 패널별 한 행으로 바꿨다.
   - 같은 Packing Unit에 여러 패널이 있어도 원하는 패널만 선택할 수 있다.
   - 선택 행에는 패널 코드·패널명과 소속 Packing Unit을 함께 표시한다.
   - 선택하지 않은 패널은 현재 업무와 상태를 유지한 채 같은 단계 대기 목록에 남는다.
2. 출발·납품 batch가 정확한 패널 membership을 저장하도록 migration `0057`을 추가했다.
   - `logistics_batch_panels`가 batch, Packing Unit, panel과 단계의 관계를 보존한다.
   - 활성 단계 중복은 `(panel_id, stage_code)` unique index로 차단한다.
   - 기존 unit 기반 batch는 해당 unit의 기존 패널을 모두 선택한 기록으로 자동 backfill한다.
   - `logistics_batch_units`는 포장 묶음 관계와 과거 API 호환을 위해 유지한다.
3. Frontend는 새 요청에 `panelIds`를 전송한다.
   - 출발과 납품 모두 선택한 패널만 draft에 담는다.
   - 내 업무의 `panel` deep link로 진입하면 해당 패널만 미리 선택된다.
   - 임시 draft를 다시 열어도 선택 개수를 panel membership으로 표시한다.
4. 확정과 후속 인계를 패널 범위로 제한했다.
   - 출발 확정은 선택 패널의 출발 업무만 완료하고 해당 패널의 납품 업무만 생성한다.
   - 납품 확정은 선택 패널만 `ShipmentCompleted`와 납품 결과로 기록한다.
   - 프로젝트의 모든 활성 패널이 납품된 마지막 순간에만 영업 정산 업무를 한 번 생성한다.
5. 물류 이력·프로젝트 전체 흐름·영업 정산·월별 발행요청·UL891 출하일 집계도 exact panel membership을 읽도록 통일했다.

## 호환성과 불변조건

- 같은 batch에 다른 프로젝트 패널을 섞을 수 없다.
- 포장 완료 전 출발, 출발 완료 전 납품은 계속 차단한다.
- 열린 프로젝트/패널 Pending, 담당 권한, 필수 증빙, expectedVersion과 operation replay 계약은 유지한다.
- 확정된 membership과 evidence는 append-only trigger로 보호한다.
- 기존 `unitIds` 요청은 호환 경로로 유지하며, 아직 해당 단계가 남아 있는 unit의 개별 패널을 선택한다.
- 기존 finalized unit 기반 기록은 migration backfill 뒤 이전과 같은 전체 패널 기록으로 조회된다.

## 전체 영향

- DB: additive migration `0057_logistics_batch_panels`.
- Backend: 물류 queue·draft·batch 생성/변경/취소/확정·이력·purge와 물류 결과 소비자.
- Frontend: 물류 출발·납품 패널 선택과 batch 요청.
- API: 신규 요청은 `panelIds`; 기존 `unitIds`는 nullable 호환 필드로 유지.
- 권한·Pending·증빙·알림 provider: 정책 변경 없음.
- 대표 repo·`main`·Persistent UAT·실제 provider: 변경 없음.

## 변경 파일

- Migration
  - `database/migrations/0057_logistics_batch_panels.sql`
- Backend
  - `backend/src/Emi.Qms.Api/Logistics/LogisticsContracts.cs`
  - `backend/src/Emi.Qms.Api/Logistics/LogisticsStore.cs`
  - `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`
  - `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`
  - `backend/src/Emi.Qms.Api/Sales/SalesBillingRequestStore.cs`
  - `backend/src/Emi.Qms.Api/Sales/SalesSettlementStore.cs`
  - `backend/src/Emi.Qms.Api/Ul891Sets/MonthlyBillingStore.cs`
  - `backend/src/Emi.Qms.Api/Ul891Sets/Ul891SetStore.cs`
- Frontend
  - `frontend/src/logistics.ts`
  - `frontend/src/LogisticsPage.tsx`
- Tests
  - `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
  - `backend/tests/Emi.Qms.Api.Tests/ProjectRegistrationApiTests.cs`
  - `backend/tests/Emi.Qms.Api.Tests/Ul891SetApiTests.cs`
  - `frontend/tests/LogisticsPage.test.tsx`
  - `frontend/e2e/full-stack/logistics-execution.full-stack.spec.ts`
- Task·governance
  - Change 016 계약, 본 구현 보고, 사용자 검수 체크리스트, Product Roadmap, 실험 완료 원장

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-016-F01` | P1 | Resolved | 출발·납품 queue와 API가 Packing Unit을 처리 단위로 사용해 부분 출하가 불가능했다. | queue target과 API authoritative membership을 panel로 변경했다. |
| `WF-016-F02` | P1 | Resolved | 기존 unit-stage unique와 unit 확장 조회가 선택하지 않은 패널까지 함께 확정했다. | panel-stage unique와 exact batch-panel relation으로 교체했다. |
| `WF-016-F03` | P1 | Resolved | 정산·월별 출하·전체 흐름 소비자가 batch–unit을 전체 패널로 확장했다. | 모든 상태 소비자를 batch–panel relation으로 통일했다. |
| `WF-016-F04` | P2 | Resolved | 기존 unit 기반 finalized 기록과 client 호환이 끊길 수 있었다. | migration backfill과 nullable `unitIds` 호환 경로를 유지했다. |

Open P0/P1/P2: `0/0/0`.

## 실행한 검증

- Backend Release build: 경고 0, 오류 0.
- Backend 집중 통합 회귀: migration·패널별 물류·UL891 월별 출하 `3/3` 통과.
- Backend 전체 회귀: `424/424` 통과.
- Frontend 집중 unit: 물류 패널 선택과 공통 입력 기준선 `5/5` 통과.
- Frontend 전체 unit: 21 files, `135/135` 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend typecheck: 통과.
- Frontend production build: 통과, 기존 500kB 초과 chunk warning 유지.
- Isolated Full-Stack E2E:
  - 같은 Packing Unit에 P01·P02를 포장.
  - P01만 출발·납품 후 P02가 출발 queue에 남는지 확인.
  - P02를 별도 출발·납품한 마지막 시점에만 영업 정산 업무가 생성되는지 확인.
  - 390px viewport horizontal overflow 0.
- 고정 검수 runtime:
  - migration latest `0057_logistics_batch_panels`.
  - Frontend root HTTP 200.
  - Backend `/health/ready` status `ok`, database reachable.
  - 물류 역할 desktop 화면 로드, browser console error 0.
- `git diff --check`: 통과.

## 미실행 검증과 이유

- 고정 검수 DB의 실제 출발·납품 입력: 현재 출발·납품 대기 데이터가 없어 기존 업무 데이터를 임의 생성하지 않았다. 동일 흐름은 실행별 격리 DB Full-Stack E2E로 검증했다.
- 실제 Teams·메일 발송: 실제 provider 승인 범위 밖이며 물류 단계의 기존 인앱 업무 계약만 보존했다.
- Persistent UAT 적용·실데이터 mutation: 사용자 승인 범위 밖이다.
- 사용자 수동 검수: 마지막 일괄 검수 정책에 따라 체크리스트로 남겼다.

## 개인정보·secret 검토

- 자동 통합·브라우저 검증은 synthetic data 또는 비식별 화면 구조만 기록했다.
- 실제 프로젝트·고객·사용자명, secret, connection string, 증빙 파일 원문을 보고서에 남기지 않았다.
- 실제 provider와 Persistent UAT를 호출하지 않았다.

## SOP·사용자 확인 방법

1. 고정 Frontend `http://127.0.0.1:42983`에서 물류 담당자로 들어간다.
2. 같은 Packing Unit에 패널 두 개 이상을 포장 완료한다.
3. `출발`에서 패널이 각각 한 행으로 표시되는지 확인하고 하나만 선택해 상차 사진·출발일을 저장한다.
4. 선택하지 않은 패널이 `출발` 대기 목록에 그대로 남고, 선택 패널만 `납품`에 나타나는지 확인한다.
5. `납품`에서도 일부 패널만 선택해 서명본을 저장한다.
6. 선택 패널만 납품 완료되고 나머지 패널의 출발·납품 상태와 증빙이 바뀌지 않는지 확인한다.

## Rollback·forward-fix

- 안전 복귀 기준은 작업 전 experiment checkpoint `2247643`이지만, 현재 작업에는 함께 보존 중인 DESIGN-000 Change 002 미커밋 변경이 있으므로 전체 worktree 강제 복귀를 사용하지 않는다.
- 코드 rollback이 필요하면 Change 016 allowlist만 역변경하고 migration `0057`은 additive schema로 남긴 뒤 구형 코드가 계속 읽는 `logistics_batch_units` 호환 관계를 유지한다.
- migration을 물리적으로 되돌리려면 신규 panel membership을 참조하는 코드부터 제거하고 별도 승인된 DB rollback 절차가 필요하다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `SOP·사용자 확인 방법`, `Rollback·forward-fix` |
| User manual | 본 문서·체크리스트에 포함 | `SOP·사용자 확인 방법`, Change 016 사용자 검수 체크리스트 |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/workflow-continuity-001-change-016-user-validation-checklist.md` |

## 변경·게시 경계

- local experiment 코드·문서, 자동 검증과 고정 검수 runtime migration·health 확인만 수행했다.
- Commit·push·PR·merge는 수행하지 않았다.
- 대표 repo·`main`·Persistent UAT·실제 provider는 변경하지 않았다.
- `main` merge 승인: `0/3`.
