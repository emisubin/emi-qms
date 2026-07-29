# TASK-WORKFLOW-CONTINUITY-001 Change 009 구현 보고 — 패널별 제조·LQC 병행과 품질·물류 연속 인계

## 요청별 구현 결과

1. 제조 시작과 같은 transaction에서 해당 패널의 LQC 업무·정/부 알림을 생성한다. LQC는 제조가 전부 끝날 때까지 기다리지 않고 제조 1단계부터 시작할 수 있다.
2. LQC Check 항목을 제조 단계 순서와 1:1로 연결했다. 현재 진행 중이거나 이미 확인한 제조 단계까지만 활성화하고 미래 단계는 API 저장·사진 연결·최종 판정과 PC/Mobile 화면에서 모두 차단한다.
3. 제조와 LQC 중 어느 쪽이 먼저 완료돼도 같은 패널의 두 조건이 모이는 순간 `ManufacturingCompleted` 감사 업무를 자동 완료하고 OQC 업무·정/부 알림을 멱등 생성한다. 기존 수동 `제조 완료 확인` UI는 제거했다.
4. OQC 합격 시 같은 패널의 전진검수 업무를 열고, FAT 필수 프로젝트이면 FAT도 같은 transaction에서 동시에 연다.
5. FAT 필수 프로젝트는 전진검수와 FAT가 모두 합격한 패널만 포장을 연다. 두 검사는 어느 순서로 완료해도 되며 먼저 끝난 검사는 다른 검사를 기다린다. FAT 비필수 프로젝트는 전진검수 합격으로 포장을 연다.
6. 품질 부적합·Pending·재검사 계약은 유지했다. 재검사 합격 뒤에도 같은 패널의 제조·병행 검사 완료 조건을 다시 계산하고, 먼저 끝난 병행 검사에서도 Pending을 정상 종결한다.
7. 최종 품질 조건이 완료된 패널에만 포장 업무를 생성한다. 기존 물류 모델이 이미 단일 패널 포장 단위와 단일 포장 단위별 출발·납품 증빙을 지원함을 확인하고, 두 패널을 서로 다른 포장·출발·납품 증빙으로 처리하는 회귀 시나리오를 추가했다.
8. 프로젝트 전체 workflow event는 모든 활성 패널 완료 시 집계하되 개별 패널의 OQC·포장·출발·납품 인계 조건에는 사용하지 않는다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-009-F01` | P1 | Resolved | LQC 업무가 제조 전체 완료 뒤 생성돼 제조 중 검사라는 현장 의미와 달랐다. | 제조 시작 transaction에서 LQC 업무·알림을 생성하고 제조 단계별 입력 가용성을 서버에서 계산한다. |
| `WF-009-F02` | P1 | Resolved | LQC 합격 뒤 제조 담당자의 수동 완료 확인을 요구해 OQC 인계가 누락·지연될 수 있었다. | 패널 제조 실행과 LQC 합격을 공동 조건으로 계산해 둘 중 나중 완료된 transaction이 OQC를 한 번만 연다. |
| `WF-009-F03` | P1 | Resolved | OQC→전진검수→FAT가 직렬이어서 전진검수와 FAT 병행이 불가능했다. | OQC 합격 시 전진검수와 필수 FAT를 동시에 생성하고, 두 합격을 포장 앞 합류 조건으로 사용한다. |
| `WF-009-F04` | P1 | Resolved | 프로젝트 전체 완료가 패널별 다음 단계 개방 조건처럼 보였고 부분출하 증빙 회귀가 부족했다. | 프로젝트 event는 집계로만 유지하고 두 패널을 개별 포장·출발·납품하는 E2E로 다른 패널을 기다리지 않음을 고정했다. |
| `WF-009-F05` | P2 | Resolved | LQC 양식에서 아직 시작하지 않은 제조 단계도 모두 입력할 수 있었다. | 미래 LQC 항목을 `제조 대기`로 표시하고 저장·사진·판정 API에서도 동일하게 거절한다. |

Open P0/P1/P2: `0/0/0`.

## 변경 파일

- Backend: `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingStore.cs`, `backend/src/Emi.Qms.Api/QualityInspections/QualityInspectionContracts.cs`, `backend/src/Emi.Qms.Api/QualityInspections/QualityInspectionStore.cs`
- Frontend: `frontend/src/ManufacturingPage.tsx`, `frontend/src/QualityInspectionsPage.tsx`, `frontend/src/qualityInspections.ts`, `frontend/src/styles.css`
- Tests: `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`, `frontend/e2e/full-stack/manufacturing-work.full-stack.spec.ts`, `quality-inspections.full-stack.spec.ts`, `logistics-execution.full-stack.spec.ts`, 두 lifecycle E2E 계약
- Task·governance: `tasks/workflow-continuity-001-change-009.md`, 본 구현 보고, `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md`

## 검증

- Backend 집중 회귀: 제조·물류 `2/2` 통과
- Backend 전체 회귀: `420/420` 통과
- Frontend 전체 unit: `19` files, `126/126` 통과
- Frontend typecheck·production build: 통과
- Frontend lint: error `0`, 기존 `src/main.tsx` Fast Refresh warning `1`
- Isolated Full-Stack E2E:
  - 제조 시작 즉시 LQC·단계 잠금·LQC 선완료 후 마지막 제조 완료 시 OQC 자동 생성: 통과
  - 기존 Aggregate Pending 재검사 순환: 통과
  - OQC 후 전진검수·필수 FAT 병행 및 포장 합류: 통과
  - 두 패널의 개별 포장·출발·납품·증빙과 최종 영업 인계 1회: 통과
  - 제조 화면 회귀: 통과
- Fresh isolated PostgreSQL은 migration `0054`까지 적용했고 E2E 종료 후 database·container·network를 삭제했다.

## 검수·게시 경계

- 사용자 수동 검수는 대기 중이다.
- local experiment 작업만 수행했으며 commit·stage·push·PR·대표 repo·`main`·Persistent UAT·실제 provider 호출은 하지 않았다.
- `main` merge 승인: `0/3`.
