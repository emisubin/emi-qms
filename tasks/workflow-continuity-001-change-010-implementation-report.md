# TASK-WORKFLOW-CONTINUITY-001 Change 010 구현 보고 — 실데이터 흐름 집계와 누락 인계 재조정

## 해결한 업무 문제

프로젝트 전체 흐름이 프로젝트 단위 완료 event에 지나치게 의존해 일부 패널의 실제 완료 사실을 놓쳤다. 그 결과 LQC 합격 패널이 있어도 LQC는 `미시작`, 생산관리 제조 투입 요청을 해도 선택형 키팅은 `미시작`, 새 자동 인계 로직 적용 전에 완료한 제조+LQC 패널은 OQC 업무 없이 남았다.

이번 Change는 활성 구매품목·패널의 실제 저장 사실을 18단계 흐름에 집계하고, 일부 완료를 별도 상태로 표시하며, 과거 누락된 품질 후속 업무를 담당자가 검사함을 열 때 멱등 복구한다.

## 요청별 구현 결과

1. `LQC`: 최신 패널별 검사 회차를 집계한다. 활성 패널 일부가 합격이면 `부분 완료`, 전부 합격이면 `완료`, 최신 회차 부적합이면 `차단`이다.
2. `키팅 완료 (선택)`: 패널별 `자재 키팅 완료 OR 생산관리 제조 투입 요청`을 준비 완료 조건으로 사용한다. 일부 패널이면 `부분 완료`, 모든 활성 패널이면 `완료`다.
3. 생산관리 제조 투입 요청과 자재 키팅 완료 transaction은 모든 활성 패널이 유효 조건을 충족하는 순간 프로젝트 키팅 완료 event를 한 번만 생성한다.
4. 제조·제조 완료·OQC·전진검수·FAT·포장·출발·납품도 활성 패널별 실제 실행·최신 검사·물류 결과를 집계한다.
5. 자재 도착·IQC·입고 확정은 활성 구매품목과 입고 건의 실제 상태를 집계한다.
6. `PartiallyCompleted` 상태와 `부분 완료` 라벨을 Backend 계약·PC/Mobile workflow 카드에 추가했다.
7. 품질 검사함 진입 시 제조 완료+최신 LQC 합격인데 확인/OQC가 누락된 패널, OQC 이후 전진검수·필수 FAT, 최종 품질 이후 포장 누락을 점검한다.
8. 재조정 업무·정/부 알림은 기존 결정적 idempotency key를 재사용한다. 고정 검수 DB에서 첫 실행은 누락 OQC 1건을 복구했고 두 번째 실행은 0건이었다.
9. 과거 합격 뒤 최신 재검사가 진행 중·부적합인 경우 과거 합격을 사용하지 않도록 `latest passed` 조회를 최신 회차 판정으로 바로잡았다.

## 기술적 결정과 검토한 대안

- 채택: 전체 완료 event는 audit·프로젝트 완료 증빙으로 유지하고 화면 상태는 실데이터에서 파생한다. 부분 진행을 event 개수로 추측하지 않는다.
- 채택: 별도 migration이나 일회성 SQL backfill 대신 권한 있는 품질 사용자가 검사함을 열 때 bounded reconciliation API를 한 번 호출한다. 기존 검수·향후 누락 모두 같은 경로로 복구하며 반복 호출은 멱등이다.
- 채택: 키팅은 필수 gate가 아니므로 실제 키팅과 생산관리 제조 투입을 동등한 패널 준비 신호로 집계한다. 키팅 자체 완료 응답의 의미는 실제 키팅 수량으로 유지한다.
- 폐기: 완료된 work item 하나만 보고 프로젝트 단계를 완료 처리하는 방식. 다른 활성 패널의 미진행을 숨기므로 사용하지 않았다.
- 폐기: GET workflow에서 자동으로 DB를 수정하는 방식. 조회와 mutation을 분리하기 위해 재조정은 명시적 POST로 두었다.

## 아키텍처와 영향

- Backend/Workflow: 실데이터 CTE 집계, `PartiallyCompleted`, 상태 라벨, 선택형 키팅 완료 event helper.
- Backend/Quality: `/api/quality/inspections/reconcile`, 최신 회차 판정, OQC·후속 검사·포장 누락 복구.
- Backend/Manufacturing·Materials: 제조 투입 또는 키팅 완료 뒤 유효 키팅 완료 조건 재평가.
- Frontend: 품질 검사함 최초 진입의 비차단 재조정 호출, 복구/담당자 누락 안내, PC/Mobile 부분 완료 스타일.
- DB/Migration: 새 schema나 migration 없음. 기존 table과 unique idempotency key만 사용한다.
- 권한: 재조정은 `quality.inspect` 권한과 기존 project scope를 적용한다. 읽기 전용 사용자는 호출하지 않는다.
- 외부 알림: 실제 Teams/Mail provider를 호출하지 않는다. 인앱 정·부 알림만 기존 writer로 생성한다.
- Excel/PDF/첨부: 직접 변경 없음. 기존 품질 PDF·첨부 계약 회귀만 전체 test에서 확인한다.

## 변경 파일

- Backend workflow: `backend/src/Emi.Qms.Api/Workflow/WorkflowContracts.cs`, `WorkflowStore.cs`
- Backend handoff: `backend/src/Emi.Qms.Api/Manufacturing/ManufacturingStore.cs`, `Materials/PanelKittingStore.cs`
- Backend quality: `backend/src/Emi.Qms.Api/QualityInspections/QualityInspectionContracts.cs`, `QualityInspectionEndpointExtensions.cs`, `QualityInspectionStore.cs`
- Backend tests: `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`, `PanelInformationApiTests.cs`
- Frontend: `frontend/src/App.tsx`, `QualityInspectionsPage.tsx`, `api.ts`, `qualityInspections.ts`, `styles.css`, `design-system/wireframe.css`
- Frontend tests: `frontend/tests/QualityInspectionsPage.test.tsx`
- Task·governance: `tasks/workflow-continuity-001-change-010.md`, 본 구현 보고, Change 010 사용자 검수 체크리스트, `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md`

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-010-F01` | P1 | Resolved | 부분 LQC 완료가 완료 event 없음 때문에 `미시작`으로 보였다. | 최신 패널별 실제 검사 회차 집계와 `PartiallyCompleted`를 추가했다. |
| `WF-010-F02` | P1 | Resolved | 생산관리 제조 투입 요청이 선택형 키팅 단계에 반영되지 않았다. | `키팅 완료 OR 제조 투입 요청`을 패널별 유효 준비 조건으로 집계하고 전체 충족 시 event를 멱등 생성한다. |
| `WF-010-F03` | P1 | Resolved | 새 자동 인계 배포 전 제조+LQC 완료 패널이 OQC 없이 잔류했다. | 품질 검사함 진입 시 scope 제한 재조정 API로 완료 확인·OQC 업무·정/부 알림을 복구한다. |
| `WF-010-F04` | P1 | Resolved | 과거 합격 회차를 조회해 최신 부적합·진행 회차를 우회할 수 있었다. | 최신 회차 하나를 먼저 고른 뒤 그 회차가 합격일 때만 통과로 인정한다. |
| `WF-010-F05` | P2 | Resolved | 설계 일부 완료 회귀 테스트가 새 `부분 완료` 정책 이전 값 `진행 중`을 요구했다. | 정책과 구현에 맞게 기존 기대값을 `PartiallyCompleted`로 동기화했다. |

Open P0/P1/P2: `0/0/0`.

## 시행착오 및 폐기한 접근

- 첫 전체 회귀에서 기존 설계 일부 완료 테스트 한 건이 실패했다. 기능 회귀가 아니라 새 상태 정책과 오래된 테스트 기대값의 불일치였고, 해당 테스트를 새 계약으로 갱신한 뒤 집중·전체 회귀를 재실행했다.
- 고정 Backend 포트를 재시작하려 했으나 runtime mutation 명령 경계에서 실행되지 않았다. 실제 source 수정 시각 뒤 감시 프로세스가 새 자식을 시작한 것을 process start time과 API 응답으로 확인해 불필요한 재기동 없이 최신 실행본을 검증했다.

## 검증

- Backend Release build: 경고 0, 오류 0.
- Backend 집중 회귀: workflow·quality reconciliation·kitting·manufacturing `3/3` 통과.
- Backend 영향 회귀: manufacturing·quality·kitting·workflow `24/24` 통과.
- Backend 전체 회귀: `421/421` 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend typecheck: 통과.
- Frontend 전체 unit: 19 files, `126/126` 통과.
- Frontend production build: 통과. 기존 500kB 초과 chunk warning 유지.
- 고정 검수 API:
  - 첫 재조정: OQC 누락 복구 1, 미지정 0.
  - 같은 재조정 반복: 전체 복구 0.
  - 4개 활성 프로젝트·18단계 조회: 허용하지 않은 상태 0.
  - 실제 검수 프로젝트: 키팅·제조·LQC·제조 완료 `부분 완료`, OQC `내 업무 생성됨`.
- 화면 검수: 고정 Frontend에서 Desktop 전체 흐름과 Mobile 390px DOM을 확인했고, 8~11단계의 상태가 모두 `PartiallyCompleted / 부분 완료`로 일치했다.

## 개인정보·secret 검토

- 보고서·체크리스트에는 실제 project UUID, 사용자 UUID, email, token, password, Authorization header를 기록하지 않았다.
- 채팅 시각 증빙은 local 검수 fixture 화면이며 실제 provider 전송은 없었다.
- 고정 DB 이름·credential은 기존 private runtime script 범위를 벗어나 새로 문서화하지 않았다.

## 사용자 검수 결과와 남은 항목

- 자동 검증 완료.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- 체크리스트: [Change 010 사용자 검수 체크리스트](workflow-continuity-001-change-010-user-validation-checklist.md).
- 대표 repo·`main`·Persistent UAT·실제 provider 검증은 승인 범위 밖으로 미실행했다.

## Rollback·forward-fix

- 아직 commit하지 않았으므로 이 Change 관련 allowlist 파일만 되돌릴 수 있다. 누적 dirty worktree 전체를 reset하지 않는다.
- 고정 검수 DB에서 생성된 OQC 업무·알림은 자동 복구의 의도된 검수 데이터다. 임의 삭제하지 않고 사용자 검수 fixture로 유지한다.
- 게시 뒤 결함 발견 시 migration rollback이 아니라 상태 파생·재조정 API를 forward-fix한다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `기술적 결정`, `Rollback·forward-fix`, 고정 검수 URL·재조정 동작 |
| User manual | 본 문서·체크리스트에 포함 | `요청별 구현 결과`, Change 010 사용자 검수 체크리스트 |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` TASK-WORKFLOW-CONTINUITY-001·Decision Log |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/workflow-continuity-001-change-010-user-validation-checklist.md` |

## 변경·게시 경계

- local experiment 변경과 고정 검수 runtime mutation만 수행했다.
- commit·stage·push·PR·대표 repo·`main`·Persistent UAT·실제 provider는 수행하지 않았다.
- `main` merge 승인: `0/3`.
