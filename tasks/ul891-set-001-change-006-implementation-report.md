# TASK-UL891-SET-001 Change 006 구현 보고 — 프로젝트 상세 현재 단계·품질 Pending 표시 보정

## 해결한 업무 문제

프로젝트 상세 품질 탭은 종결된 과거 Pending 연결까지 현재 Pending처럼 표시했고, 실제 OQC 부적합으로 차단된 패널에는 아직 진행할 수 없는 전진검수·FAT를 `대기`로 함께 표시했다. 제조·품질·물류의 현재 단계 값은 내부에서 일부 계산하고도 목록에 별도 열이 없어 사용자가 핵심정보를 다시 해석해야 했다.

## 요청별 구현 결과

1. 품질 검사 queue는 종결되지 않은 Pending만 현재 `pendingId·pendingNumber·actionDepartmentCode`로 projection한다. 과거 종결 연결은 검사 이력에 남지만 현재 차단 표시에는 사용하지 않는다.
2. 열린 Pending 또는 최신 부적합 검사가 있으면 해당 품질 단계를 최우선 현재 단계로 표시한다.
3. OQC Pending은 `OQC 부적합 · Pending 조치 대기`로 표시하고, 전진검수·FAT 대기 문구를 숨긴다.
4. OQC 합격 뒤 전진검수와 필수 FAT가 함께 열리면 현재 품질 단계는 `전진검수 · FAT`로 표시한다.
5. 제조 탭의 핵심정보 오른쪽에 `제조 단계` 열을 추가했다. 착수 전, 첫 미완료 제조 항목, 중단 항목, 제조 완료를 구분한다.
6. 품질 탭에 `품질 단계` 열을 추가해 LQC·OQC·전진검수·FAT·병행 단계·품질 완료를 구분한다.
7. 물류 탭에 `물류 단계` 열을 추가해 포장·출발·납품·물류 완료 중 현재 또는 다음 단계를 표시한다.
8. 390px 모바일 카드에도 같은 부서 단계 필드를 추가하고 표를 단순 축소하지 않는 기존 적응형 구조를 유지했다.

## 기술적 결정

- 현재 Pending projection은 `attempt.linked_pending_issue_id` 자체가 아니라 열린 `pending_issues` join 결과를 사용한다. 과거 연결 ID가 현재 차단처럼 재사용되지 않으면서 원본 attempt·Pending 이력은 보존된다.
- 품질 표시는 `차단 단계 → 전체 완료 → 현재 검사 → OQC 합격 뒤 병행 단계` 순으로 결정한다. 미래 단계 대기 문구보다 현재 차단 원인을 우선한다.
- 제조 단계는 실행 상세의 첫 미완료 step 이름을 사용한다. 목록에 별도 제조 단계 사본을 저장하지 않아 실행 데이터와 표시가 어긋나지 않는다.
- 물류 단계는 완료된 단계 집합에서 첫 미완료 단계를 찾고, 열린 Pending은 해당 Pending stage를 우선한다.

## 변경 파일

- Backend: `backend/src/Emi.Qms.Api/QualityInspections/QualityInspectionStore.cs`
- Backend test: `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`
- Frontend: `frontend/src/App.tsx`, `frontend/src/styles.css`
- Frontend test: `frontend/tests/App.test.tsx`
- Task·governance: Change 006 계약, 본 구현 보고, 사용자 검수 체크리스트, Product Roadmap, 실험 완료 원장

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `UL891-006-F01` | P1 | Resolved | 품질 queue가 종결된 Pending의 연결 ID를 현재 상태로 반환해 OQC 합격 패널이 Pending으로 오표시됐다. | 열린 Pending join 결과만 현재 Pending ID·번호로 반환하고 종결 후 null이 되는 회귀를 추가했다. |
| `UL891-006-F02` | P1 | Resolved | OQC가 부적합이어도 품질 핵심정보가 전진검수·FAT 대기를 항상 붙여 현재 조치 대상을 흐렸다. | 차단 검사를 최우선으로 표시하고 OQC 합격 전에는 후속 단계 대기 문구를 만들지 않는다. |
| `UL891-006-F03` | P2 | Resolved | 제조·품질·물류의 `stage` 값이 목록 UI에 노출되지 않아 현재 위치를 핵심정보에서 추론해야 했다. | Desktop 5열과 Mobile 단계 필드를 추가하고 부서별 현재 단계 계산을 실제 실행 데이터에 맞췄다. |

Open P0/P1/P2: `0/0/0`.

## 검증

- Backend Release build: 경고 0, 오류 0.
- Backend 집중 회귀: LQC·OQC 재검사 종결 뒤 현재 Pending projection 제거 `2/2` 통과.
- Backend 전체 회귀: `423/423` 통과.
- Frontend 전체 unit: 19 files, `128/128` 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend typecheck: 통과.
- Frontend production build: 통과, 기존 500kB 초과 chunk warning 유지.
- 고정 Backend 실제 데이터 projection: 지정 프로젝트 1번 패널 `OQC Passed + current Pending 없음`, 2번 패널 `OQC Failed + open Pending 있음` 일치.
- 고정 Frontend 1280px: 품질 단계 header 표시, 1번 패널 Pending 미표시·`전진검수 · FAT`, 2번 패널 Pending·OQC 표시, 후속 단계 대기 미표시, horizontal overflow 없음.
- 고정 Frontend 1280px: 제조·물류 단계 header와 전체 패널 단계 값 표시, horizontal overflow 없음.
- 고정 Frontend 390px: 제조·품질·물류 단계 필드 표시, 지정 1·2번 패널 상태 우선순위 일치, horizontal overflow 없음.
- 고정 Frontend·Backend live/ready: HTTP 200.
- `git diff --check`: 통과.

분리 Codex 검증 session은 현재 사용자 요청 없는 agent 생성을 금지하는 실행 규칙 때문에 만들지 않았다. 대신 최종 diff·API projection·desktop/mobile 표시와 자동 회귀를 같은 session에서 read-only 재검토했다.

## 개인정보·secret 검토

- 실제 runtime 검증 결과는 일치 여부·건수·boolean만 기록했다.
- 보고서에는 실제 프로젝트명·고객명·사용자명·UUID·업무 원문·token·Authorization header를 기록하지 않았다.
- 실제 Teams·메일 provider를 호출하지 않았다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 고정 runtime 확인 완료.
- 사용자 검수 상태: `사용자 검수 대기`.
- 대표 repo·`main`·Persistent UAT·실제 provider는 승인 범위 밖이다.

## Rollback·forward-fix

- 코드·문서는 Change 006 변경분만 되돌린다.
- 검사 attempt·Pending 이력과 고정 검수 DB 데이터는 삭제하거나 되돌리지 않는다.
- 현재 Pending projection을 다시 변경해야 하면 종결 이력 조회와 현재 차단 표시를 별도 필드로 유지한다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `기술적 결정`, `Rollback·forward-fix` |
| User manual | 본 문서·체크리스트에 포함 | `요청별 구현 결과`, Change 006 사용자 검수 체크리스트 |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/ul891-set-001-change-006-user-validation-checklist.md` |

## 변경·게시 경계

- local experiment 구현·검증과 고정 검수 runtime 확인만 수행했다.
- 사용자 승인에 따라 변경을 현재 누적 experiment checkpoint에 포함했다.
- push·PR·대표 repo·`main`·Persistent UAT·실제 provider는 수행하지 않았다.
- `main` merge 승인: `0/3`.
