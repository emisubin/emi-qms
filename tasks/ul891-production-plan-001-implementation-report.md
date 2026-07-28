# TASK-UL891-PRODUCTION-PLAN-001 구현 보고서

## 상태

- taskType: `NEW_FEATURE`
- branch: `experiment/task-home-002-personalized-shell`
- 기준 HEAD: `a7651b5c266d73be48e76861a02910435c1371fe`
- 구현·자동 검증: 완료
- 사용자 검수: `대기 — 마지막 일괄 검수`
- local commit: `보류 — 기존 미커밋 WIP와 핵심 파일이 중첩되어 독립 commit 불가`
- push·PR·merge·Persistent UAT·실제 provider: 미승인·미적용
- main merge 승인: `0/3`
- Finding: Open P0 `0`, P1 `0`, P2 `0`

## 1. 해결한 업무 문제

UL891은 하나의 프로젝트 안에 여러 세트 사양과 여러 실물 세트가 있고, 각 세트는 다시 개별 패널로 구성된다. 기존 생산계획은 프로젝트 한 벌뿐이라 세트마다 다른 계획 기간·담당 인력·코멘트를 기록하거나 한 세트의 실제 진행만 따로 볼 수 없었다.

이번 구현은 생산계획 항목과 실적 연결은 프로젝트 공통 구조로 한 벌만 유지하면서, 실제 실물 세트 인스턴스마다 계획 기간·담당자·필요 인원·생산관리 코멘트를 따로 저장한다. 생산관리 탭의 한 범위 선택이 생산계획표와 계획·실적 일정표를 동시에 전환한다.

## 2. 포함·제외 범위

포함:

- UL891 세트형 LinkedV1 프로젝트의 전체/실물 세트별 생산계획 조회·입력
- 세트별 제조·LQC·OQC·전진검수·FAT·포장·출발·납품 패널 실적 집계
- 구매·자재·IQC 프로젝트 공통 근거 표시
- 세트 추가·취소 lifecycle, CAS, audit, 완료 판정 정합
- 기존 프로젝트 backfill migration `0064`
- PC tab·다수 세트 select·390px 한 열 화면

제외:

- 비-UL891, 평면 UL891와 Legacy 생산계획 변경
- 세트별 판매단가·납기·BOM·원가
- 신규 외부 알림·Teams·메일
- 세트별 Excel 신규 출력
- 대표 repo·`main`·Persistent UAT·실제 provider

## 3. 전체 아키텍처

- 공통 구조: 기존 `project_production_plan_items`와 `project_production_plan_connections`를 유지한다.
- 세트 overlay: `project_production_plan_set_scopes`와 `project_production_plan_set_item_values`에 실물 세트별 입력값만 저장한다.
- 전체 조회: 활성 세트들의 가장 이른 시작일·가장 늦은 종료일·담당자 차이·인원 합계를 읽기 전용으로 집계한다.
- 세트 조회: 선택 세트의 overlay를 공통 항목에 합성하고, 패널 실적은 그 세트의 active panel만 계산한다.
- 완료 판정: 모든 활성 세트 × 모든 필수 계획 항목의 시작·종료가 입력되어야 완료다. 취소 세트와 inactive 항목은 제외한다.

## 4. Backend·DB·Migration·API 영향

- migration `0064_ul891_set_production_plans.sql`은 additive다. 두 overlay 테이블, FK·unique·날짜·인원 check와 index를 추가한다.
- 기존 Ul891Set+LinkedV1 프로젝트의 공통 입력값을 모든 기존 active/cancelled 세트에 복사한다. 원본 item·connection·audit는 삭제하지 않는다.
- `GET /api/projects/{projectId}/production-planning?setInstanceId=...`에 `isSetScoped`, `scopes`, `selectedScope`를 추가했다.
- `PATCH /api/projects/{projectId}/production-planning/set-scopes/{setInstanceId}`는 해당 세트의 값만 CAS·audit와 함께 저장한다.
- 프로젝트 공통 PATCH와 기존 Excel import가 UL891 세트 일정을 우회 변경하지 못하도록 field/row 오류로 차단한다.
- 프로젝트 생성·세트 사양 추가·수량 증가 transaction 안에서 scope를 멱등 생성한다.
- Workflow, 프로젝트 목록, Home KPI와 생산계획 목록의 완료 판정을 같은 모델 분기로 맞췄다.

Rollback은 destructive down migration 대신 application forward-fix를 사용한다. 운영 적용 전 snapshot/backup과 fresh·upgrade rehearsal이 필요하며 이번 Task에서는 Persistent UAT에 적용하지 않았다.

## 5. Frontend·UI·UX 영향

- 생산관리 탭의 생산계획표·일정표 위에 `전체 / 세트별` 범위 선택을 한 번만 배치했다.
- PC에서 활성 세트 12개 이하는 tab, 13개 이상은 select를 사용한다.
- 모바일은 select와 한 열 카드로 단순화한다.
- `생산계획 수정`은 `계획 구조`와 `세트 일정`을 분리한다. 전자는 공통 항목·실적 연결, 후자는 선택 세트의 기간·담당자·인원·코멘트만 수정한다.
- scope 전환은 abort/request fence로 늦은 응답이 현재 세트를 덮지 못하게 했다.
- 취소 세트는 이력을 보존하고 조회 전용으로 표시한다.

## 6. 권한·Workflow·기존 기능 영향

- 기존 `ProductionPlanUpdate` 권한을 재사용한다. 권한 확대는 없다.
- 세트 일정 저장 후 기존 생산계획 업무 sync와 설계·구매 인계 규칙을 그대로 호출한다.
- 패널의 제조·품질·물류 처리 단위와 Pending 계약은 변경하지 않는다.
- 비-세트형·Legacy 프로젝트의 기존 생산계획 API와 화면은 유지한다.

## 7. Excel·PDF·첨부 영향

- 신규 세트별 Excel, PDF와 첨부 기능은 추가하지 않았다.
- 기존 프로젝트 단위 Excel은 비-세트 프로젝트에서 유지한다.
- UL891 세트형 LinkedV1의 일정은 세트 일정 화면만 authoritative하므로 기존 Excel preview/apply에서 우회 입력을 차단한다.

## 8. 주요 변경 파일

- `database/migrations/0064_ul891_set_production_plans.sql`: overlay schema와 backfill
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningContracts.cs`: scope 계약
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningEndpointExtensions.cs`: scope GET/PATCH
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningStore.cs`: scope 저장·집계·실적·CAS·audit
- `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`, `Ul891Sets/Ul891SetStore.cs`: lifecycle hook
- `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`, `Home/HomeMetricsStore.cs`: 완료 판정 정합
- `frontend/src/App.tsx`, `api.ts`, `projects.ts`, `styles.css`: 전체/세트 UI와 입력 분리
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`, `Ul891SetApiTests.cs`: schema·독립 저장·우회 차단 회귀

## 9. 실행한 테스트와 결과

- Backend 전체: `430/430` 통과, Release, 7분 6초
- UL891 세트 lifecycle 집중: `1/1` 통과
- Dashboard/list/workflow 영향 집중: `4/4` 통과
- Frontend 전체: `22 files / 140 tests` 통과
- Frontend lint: 오류 `0`, 기존 `main.tsx` Fast Refresh warning `1`
- Frontend production build: 통과
- migration: fresh PostgreSQL과 `0063 → 0064` upgrade schema 검증 통과
- 고정 runtime: Backend live/ready OK, Frontend HTTP 200
- browser: 전체 14면 ↔ 세트 7면, 세트별 독립 일정, 전체 기간 집계, desktop·390px, warning/error `0`

## 10. 사용자 검수 결과와 남은 항목

자동 브라우저 검수에서는 1번 세트 기간을 `2026-08-03~05`로 저장하고 2번 세트의 `2026-07-14~17`이 유지되는지 확인했다. 전체 범위는 `2026-07-14~08-05`로 집계됐다. 모바일에서는 1번 세트의 7면 분모와 같은 계획 기간이 한 열 카드로 표시됐다.

사용자 직접 검수는 아직 완료되지 않았다. 세트별 담당자·필요 인원·코멘트 입력, 13개 이상 세트 선택, 취소 세트 read-only는 checklist에서 마지막 일괄 검수한다.

## 11. 기술적 결정과 검토한 대안

- 채택: 공통 항목+연결 한 벌, 세트 값 overlay. 연결 정의 중복과 양식 drift를 막는다.
- 제거: 세트마다 생산계획 item 전체 복제. 항목·연결 변경 시 세트 수만큼 동기화해야 해 운영 비용이 크다.
- 보류: 세트별 Excel. 현재 화면 편집과 전체 집계가 우선이며 별도 export 정책이 필요하다.

## 12. 시행착오 및 폐기한 접근

초기 자동 브라우저 날짜 `fill`은 native date input의 React change event를 재현하지 못해 화면 DOM만 바뀌었다가 원래 값으로 돌아왔다. 이를 제품 저장 성공으로 오인하지 않고, 실제 API 저장·재조회와 Backend 통합 테스트로 세트 독립 저장을 재검증했다. 사람의 native date 입력 계약은 표준 controlled input `onChange`로 유지하며 사용자 최종 검수 항목에 남겼다.

Fable 1차 기획의 세트별 item 복제안은 Codex review에서 연결 FK·unique·기존 구조 편집 계약을 깨는 것으로 판단해 폐기했고, Fable 2차 기획에서 overlay로 확정했다.

## 13. 개인정보·secret 검토

- screenshot과 문서에는 개발 역할 사용자와 테스트 프로젝트만 사용했다.
- 실제 이메일·UPN·사번·tenant/client/object ID·secret·token·webhook·Authorization header를 기록하지 않았다.
- 외부 provider와 Persistent UAT mutation은 실행하지 않았다.

## 14. Known issue·잔여 위험

- Open P0/P1/P2: 없음.
- P3 `UL891-PP-P3-01`: 활성 세트 13개 이상에서 검색형 autocomplete가 아니라 native select를 사용한다. 실제 대규모 세트 사용성 측정 후 개선한다.
- Git packaging blocker `UL891-PP-GIT-01`: `App.tsx`, ProductionPlanning Store, ProjectStore, Roadmap 등 핵심 파일에 이전 미커밋 WIP가 함께 있어 이번 Task만 독립 commit하면 기반이 누락된다. reset·부분 commit·누적 commit을 임의 수행하지 않고 보류한다.

## 15. SOP

1. 프로젝트 상세 → 생산관리 → 생산계획에서 `전체` 또는 실물 세트를 선택한다.
2. 수정하려면 `생산계획 수정`을 누른다.
3. 항목명·필수 여부·실적 연결은 `계획 구조`에서 수정한다.
4. 세트별 기간·담당자·필요 인원·코멘트는 `세트 일정`에서 실물 세트를 선택해 수정한다.
5. 저장 후 해당 세트와 전체 범위를 각각 다시 열어 독립값과 집계를 확인한다.
6. 운영 적용은 migration backup·fresh/upgrade rehearsal·고정 UAT 확인 후 별도 승인으로 진행한다.

## 16. User manual

- `전체`: 입력 화면이 아니라 모든 활성 세트의 요약이다.
- `세트 사양 N · M번 세트`: 실제 제작되는 한 세트의 계획이다.
- 생산계획표와 일정표는 항상 같은 선택 범위를 보여준다.
- 구매·자재·IQC는 프로젝트 공통 업무이므로 세트 화면에서도 `프로젝트 공통` 근거로 보인다.
- 제조 이후 업무는 선택 세트에 들어 있는 패널만 계산한다.
- 취소 세트는 과거 계획을 볼 수 있지만 수정할 수 없다.

## 17. 복구·forward-fix

- code rollback이 필요하면 이번 Task 변경 commit이 생긴 뒤 해당 commit을 revert한다. 현재는 독립 commit이 없어 파일 단위 reset을 금지한다.
- migration `0064` 적용 후에는 테이블을 삭제하지 않는다. application을 이전 read path로 되돌리거나 후속 migration으로 정정한다.
- 세트 값 이상 시 base plan을 직접 수정하지 않고 해당 scope/value와 audit를 기준으로 forward-fix한다.

## 18. 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | 이 문서 15장 |
| User manual | 완료 | 이 문서 16장 |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` |
| User validation checklist | 자동 검증 완료 / 사용자 검수 대기 | `tasks/ul891-production-plan-001-user-validation-checklist.md` |

최종 Task 상태는 `구현·자동 검증 완료 / 사용자 검수 대기 / local commit 보류`다. 독립 commit이 생기기 전에는 종료 정책의 `EXPERIMENT_COMPLETE / BATCHED_FINAL`로 과장하지 않는다.
