# TASK-WORKFLOW-CONTINUITY-001 Change 014 구현 보고

상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`

## 구현 결과

1. 프로젝트 상세 기본정보와 모바일 상단 진행률은 전체 흐름 응답의 `progressPercent`를 동일하게 사용한다.
   - 전체 흐름이 정상 로드되면 상세 기본정보와 전체 흐름의 값이 항상 같다.
   - 전체 흐름을 불러오지 못한 경우에만 기존 프로젝트 응답 진행률을 fallback으로 표시한다.
2. 구매 완료 판정을 확정 정책에 맞췄다.
   - 공통 필수: 활성 품목 1개 이상, 발주품목명, 공급구분, 입고예정일.
   - 일반 구매품: 업체명·발주일 필수, 발주수량·단위 선택.
   - 사급 자재: 제공 예정 수량·단위 필수, 업체명·발주일 선택.
   - required template가 있으면 모든 필수 row의 실제 저장·확정 match를 추가로 요구한다.
   - 자재 도착·IQC·입고 확정은 구매 완료 조건에 포함하지 않는다.
3. 프로젝트 목록·상세 SQL의 구매 현재 단계 판정도 전체 흐름과 같은 공급유형별 필수 입력 조건을 사용한다.
4. 기존 `StageCompleted` 구매 이벤트가 있는 프로젝트는 새 판정으로 회귀하지 않는다.

## 변경 파일

- Backend: `ProjectStore.cs`, `WorkflowStore.cs`
- Frontend: `App.tsx`
- Tests: `ProjectRegistrationApiTests.cs`, `ProcurementApiTests.cs`, `App.test.tsx`
- Task·governance: Change 014 계약, 본 구현 보고, 사용자 검수 체크리스트, Product Roadmap, 실험 완료 원장

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-014-F01` | P1 | Resolved | 프로젝트 상세는 초기 4단계 계산값, 전체 흐름은 전체 필수 단계 계산값을 표시해 제조 이후 값이 달라졌다. | 상세 기본정보와 모바일 상단이 workflow 응답 진행률을 단일 원본으로 사용한다. |
| `WF-014-F02` | P1 | Resolved | 일반 구매품의 선택값인 발주수량·단위를 전체 흐름 완료 조건이 필수로 요구했다. | 일반 구매품은 업체명·발주일까지만 추가 필수로 두고 수량·단위를 완료 판정에서 제외했다. |
| `WF-014-F03` | P1 | Resolved | 프로젝트 요약 SQL은 품목명만으로 구매 완료를 판정해 전체 흐름보다 느슨했다. | 공급유형별 완결성과 required template match를 동일하게 적용했다. |
| `WF-014-F04` | P2 | Resolved | 프로젝트 요약 판정을 강화하면 과거 완료 이벤트가 있는 구매 단계가 회귀할 수 있었다. | 성공한 구매 `StageCompleted` 이벤트를 우선 인정해 기존 완료 비회귀 계약을 유지했다. |

Open P0/P1/P2: `0/0/0`.

## 검증

- Backend Release build: 경고 0, 오류 0.
- Backend 집중 회귀: 진행률·구매 required template `2/2` 통과.
- Backend 전체 회귀: `424/424` 통과.
- Frontend typecheck: 통과.
- Frontend 전체 unit: 19 files, `130/130` 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend production build: 통과, 기존 500kB 초과 chunk warning 유지.
- `git diff --check`: 통과.
- 고정 검수 runtime: Frontend root HTTP 200, Backend `/health/ready` status `ok`, database reachable. 기존 `42983/41166` server를 유지했다.
- 진행률 회귀 fixture는 프로젝트 응답 6%, workflow 응답 41%를 제공하고 상세 기본정보·전체 흐름에 41%가 함께 표시되는 것을 확인했다.
- 구매 회귀 fixture는 일반 구매품 수량·단위 없이 required 품목 일부 입력 시 `부분 완료`, 전체 입력 시 `완료`가 되는 것을 확인했다.

## 개인정보·secret 검토

- 모든 자동 검증은 격리된 synthetic DB와 비식별 fixture를 사용했다.
- 실제 프로젝트명·고객명·사용자명·UUID·업무 원문과 secret을 보고서에 기록하지 않았다.
- Persistent UAT, 실제 Teams·메일 provider와 대표 runtime을 호출하지 않았다.

## 사용자 검수와 남은 항목

- 자동 검증 완료.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- 대표 repo·`main`·Persistent UAT 반영은 승인 범위 밖이다.

## Rollback·forward-fix

- Frontend의 workflow 진행률 우선 선택과 Backend의 공급유형별 완료 조건을 Change 014 이전으로 되돌린다.
- DB schema·migration·기존 구매품목·workflow event를 변경하지 않았으므로 데이터 rollback은 없다.
- 기존 완료 이벤트는 수정하거나 삭제하지 않는다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `구현 결과`, `Rollback·forward-fix` |
| User manual | 체크리스트에 포함 | Change 014 사용자 검수 체크리스트 |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/workflow-continuity-001-change-014-user-validation-checklist.md` |

## 변경·게시 경계

- local experiment 코드·문서, 자동 검증과 고정 검수 runtime health 확인만 수행했다.
- 사용자가 commit을 지시하지 않아 변경은 미커밋 상태다.
- push·PR·대표 repo·`main`·Persistent UAT·실제 provider는 수행하지 않았다.
- `main` merge 승인: `0/3`.
