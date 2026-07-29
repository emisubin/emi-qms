# TASK-UL891-PRODUCTION-PLAN-001 — UL891 세트별 생산계획 2차 기획 (최종 구현 계약)

> 상태: 2차 기획 — 구현 착수용 최종 계약
> 목적: 확인된 interview, Fable 1차 기획, Codex 내용 review의 resolution을 하나의 구현 가능한 계약으로 확정한다

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-UL891-PRODUCTION-PLAN-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/ul891-production-plan-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/ul891-production-plan-001-planning.md` (판단 이력으로 보존, 수정하지 않음)
- reviewSource: `tasks/ul891-production-plan-001-review.md` (`RESOLVED_FOR_SECOND_PLANNING`, Open P0/P1/P2 `0/0/0`)
- approvalChange: `tasks/ul891-production-plan-001-change-001.md` (experiment fast-track 2차 기획·구현 승인 기록)

이 문서는 이 실험 Task의 최종 구현 source of truth다. 1차 기획의 사용자-facing 방향과 8개 권장 선택은 유지하고, Codex review의 Finding `UPP-001-F01`~`F07`과 resolution을 모두 반영해 확정한다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 따르며 이 문서에 복사하지 않는다. 이 문서는 local experiment 구현만 승인 범위로 하며 대표 repo·GitHub `main`·push·PR·merge·Persistent UAT·실제 provider 게시를 부여하지 않는다 (`main` merge 승인 `0/3` 유지).

## 1. 한 줄 목표

생산관리 담당자가 UL891 세트형 프로젝트의 생산관리 탭에서 `전체`와 실물 세트별 scope를 전환하며, 프로젝트 공통 계획 구조는 한 벌로 유지한 채 세트별 계획 기간·담당자·필요 인원·코멘트를 독립 저장하고, 해당 세트 패널의 자동 실적·일정 막대와 바로 비교할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- `TASK-PRODUCTION-CONTROL-001`의 LinkedV1 모델은 프로젝트당 계획 한 벌(`project_production_plans`의 project unique)이며, `TASK-UL891-SET-001`의 `세트 사양 → 실물 세트 인스턴스 → 개별 패널` 계층과 연결되어 있지 않다.
- 여러 세트가 서로 다른 시기에 제조·품질·출하되어도 생산계획표와 계획·실적 일정표는 프로젝트 전체 값 하나만 보여, 어느 세트가 예정 대비 빠르거나 늦는지 시스템에서 알 수 없다.
- 현재 우회 방식은 생산관리 담당자의 별도 문서 수기 관리이며, 패널 수가 커질수록 누락·중복 위험이 커진다.
- 이 기능이 없으면 부분출하·세트 취소가 있는 UL891 프로젝트의 세트별 진척 비교·지연 감지가 시스템 밖으로 나간다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 생산관리 정·부 담당자 | scope 전환, 계획 구조 편집(기존), 세트별 일정 값 저장(신규) | 전체 aggregate와 모든 세트 scope | 프로젝트 공통 계획 구조·연결·담당자 지정(기존 계약)과 활성 세트 scope의 일정 값 |
| 다른 부서 사용자 | scope 전환과 조회 | 전체 aggregate와 모든 세트 scope | 없음 (기존 read-only 계약 유지) |
| 시스템(파생) | 세트 패널 원본 실적 projection | 선택 세트의 active 패널 | 없음 (자동 실적은 저장하지 않고 조회 시 파생) |

신규 권한 능력은 만들지 않는다. 기존 `ProductionPlanUpdate` 정책과 프로젝트 read 권한, history의 기존 audit read 정책을 그대로 재사용한다. 서버 권한이 최종 기준이다.

## 4. 확정 결정 요약

### 4.1 유지된 1차 기획 권장 선택 (interview 3장 8개 비차단 선택)

| 번호 | 결정 대상 | 확정 내용 |
| ---: | --- | --- |
| 1 | 세트 계획 원자 | 실물 세트 인스턴스별 독립 계획. 같은 사양의 인스턴스도 계획은 독립이다 |
| 2 | 전체 scope 의미 | 활성 세트 계획의 read-only aggregate. 같은 내용을 이중 입력하지 않는다 |
| 3 | 초기값·추가 세트 | 프로젝트 소유 구조 snapshot 한 벌을 공유하고, 새 세트 scope의 일정 값은 빈 상태로 시작한다 |
| 4 | 실적 source scope | 패널 귀속 source는 선택 세트의 active 패널만 필터, 구매·자재·IQC는 `프로젝트 공통` 표시 |
| 5 | 취소 세트 | 선택기 별도 group의 read-only 이력 보존, 활성 집계·완료 판정 제외 |
| 6 | 기존 세트형 프로젝트 | migration backfill로 기존 공통 계획 값을 각 세트 scope에 복사 (데이터 무손실) |
| 7 | 많은 세트 tab UX | desktop은 `전체`+세트 chip tab(가로 스크롤, active 12개까지), 초과·390px는 검색/select형 |
| 8 | workflow 완료 판정 | 모든 활성 세트의 필수 계획 항목 기간 입력 = 프로젝트 계획 완료. 취소 세트 제외 |

### 4.2 Review resolution으로 변경·확정된 사항

| Finding | 결정 |
| --- | --- |
| `UPP-001-F01` | 1차 기획의 “`project_production_plan_items`에 세트 컬럼 추가/복제” 안은 채택하지 않는다. `project_production_plan_connections.production_plan_item_id`가 item row를 직접 참조하므로, 공통 item·connection은 구조 원본으로 불변 유지하고 신규 `project_production_plan_set_scopes` + `project_production_plan_set_item_values` overlay가 세트별 값만 저장한다 (7장) |
| `UPP-001-F02` | `전체` 조회는 aggregate이되, 기존 프로젝트별 항목명·필수 여부·순서·실적 연결 편집 능력은 `계획 구조` 편집으로 보존한다. 세트별 값은 `세트 일정` 편집(전용 value-only endpoint)으로 분리한다 (8장, 12장) |
| `UPP-001-F03` | 계획 완료 판정을 모델별로 명시하고 화면·프로젝트 목록·전체 흐름·Home 지표·Excel이 같은 판정 의미를 사용한다 (10장) |
| `UPP-001-F04` | 초기 생성과 후속 세트 추가가 공용 helper `EnsureSetPlanScopeAsync`를 같은 transaction에서 호출해 scope 생성 누락을 막는다 (11장) |
| `UPP-001-F05` | 전체 8열 표의 담당자·필요 인원·코멘트는 결정적 축약 규칙(`세트별 상이`, `총 N명 · 일부 미입력`, `세트별 코멘트 있음`)과 행 펼침 상세를 사용한다 (9.2장) |
| `UPP-001-F06` | source catalog를 서버에서 `ProjectCommon`/`SetPanel`로 고정 분류하고, 세트 scope에는 `evidenceScope=ProjectCommon` 배지, 전체 aggregate에는 target identity dedupe를 적용한다 (9.1장) |
| `UPP-001-F07` | backfill은 기존 Ul891Set LinkedV1의 모든 instance(취소 포함) scope를 만들고 현재 공통 item의 일정·담당자·필요 인원·코멘트를 복사한다. 기능 도입 이후 추가된 세트는 빈 값으로 시작한다 (7.3장) |
| 보류 | 세트별 Excel export는 이번 범위에서 제외하고 후속 필요성 판단으로 남긴다 |

## 5. 핵심 사용자 시나리오

### 시나리오 A — 세트별 계획 수립과 비교

1. 생산관리 담당자가 UL891 세트형(LinkedV1) 프로젝트 상세의 생산관리 탭에 진입한다. 기본 scope는 `전체`다.
2. scope selector에서 세트(예: `사양명 #2`)를 선택하면 생산계획표와 계획·실적 일정표가 함께 그 세트 scope로 바뀌고, 선택 세트의 사양명·인스턴스 번호·active 패널 수가 표시된다.
3. 담당자가 `세트 일정` 편집에서 그 세트의 항목별 계획 기간·담당자·필요 인원·코멘트를 입력하고 저장하면, 해당 세트의 value·scope revision·audit이 한 transaction으로 기록되고 다른 세트·프로젝트 공통 구조·master 양식은 변하지 않는다.
4. 제조·품질·물류 부서가 기존 화면에서 그 세트의 패널 업무를 처리하면, 세트 scope의 자동 실적·진행률·실적 막대가 조회 시 갱신된다.

### 시나리오 B — 구조 편집과 세트 추가·취소·전체 확인

1. 담당자가 `계획 구조` 편집에서 항목을 추가하면 같은 transaction에서 모든 active 세트 scope에 빈 value row가 생기고, 실적 연결은 공통으로 한 벌만 늘어난다.
2. 발주 증가로 세트 인스턴스가 추가되면 시스템이 새 세트 scope를 만들고(일정 값은 빈 상태), 기존 세트 계획은 바꾸지 않는다.
3. 어떤 세트가 취소되면 그 세트 scope는 read-only 이력으로 남고 전체 aggregate·완료 판정에서 제외된다.
4. `전체` scope에서는 항목별 계획 기간 envelope, dedupe된 실적, 세트별 상세 펼침을 확인하며, 모든 활성 세트가 완료 기준을 충족할 때만 `계획 완료`가 된다.

## 6. 업무 규칙과 불변조건

- 세트 계획의 원자는 실물 세트 인스턴스(`ul891_set_instances`)다. 같은 사양의 인스턴스라도 계획 값은 독립이다.
- 프로젝트 공통 `project_production_plan_items`와 `project_production_plan_connections`는 항목 identity·이름·필수 여부·순서·실적 연결의 유일한 source of truth이며 세트 수와 무관하게 한 벌이다. 세트별 item soft delete는 제공하지 않는다.
- Ul891Set LinkedV1 프로젝트에서 공통 item의 일정·담당자·필요 인원·코멘트 값은 세트 값의 대체 입력 경로가 아니다. 기존 공통 PATCH와 Excel 일괄 적용이 이 값을 변경하려 하면 field/row-level 오류로 거부한다.
- `전체` scope는 활성 세트 계획의 read-only aggregate이며 독립 편집 대상이 아니다.
- 한 세트의 값 저장은 다른 세트 scope·공통 구조·master 양식을 변경하지 않는다. master 현재 양식은 `0060` 이후 제자리 수정되므로 clone·표시 기준은 항상 프로젝트 snapshot이다.
- 패널 귀속 실적(제조 단계·LQC·OQC 최종 합격·전진검수·FAT·포장·출발·납품)은 선택 세트의 active 패널 원본에서만 파생하며 수동 수정하지 않는다.
- 구매·자재·IQC처럼 세트 귀속이 없는 source를 특정 세트의 독립 실적으로 표시하지 않고, 전체 aggregate에서 세트 수만큼 중복 집계하지 않는다.
- 취소 세트의 값·audit은 hard delete하지 않고 read-only로 보존하며 활성 집계·완료 판정에서 제외한다.
- 프로젝트 계획 완료는 모든 활성 세트 scope × 필수 active 항목의 계획 기간이 입력될 때만 성립한다. active 세트가 0개인 비정상 구조는 완료로 간주하지 않는다.
- 세트 scope 저장은 value row·scope revision·audit을 한 transaction으로 기록하고 실패 시 전체 rollback한다. 같은 세트 stale 수정만 409로 차단하고 다른 세트 저장을 막지 않는다.
- 비-UL891·평면(FlatPanel) UL891·Legacy 모델 프로젝트의 기존 계약(단일 프로젝트 계획, Excel 흐름, workflow sync)은 회귀하지 않는다.

## 7. 데이터 모델과 migration

### 7.1 신규 overlay 테이블 (migration `0064`, additive)

`project_production_plan_set_scopes`

- `id`, `production_plan_id` FK(`project_production_plans`), `set_instance_id` FK(`ul891_set_instances`)
- `row_version` (세트별 CAS 단위, `>= 1`), created/updated actor·time
- unique `(production_plan_id, set_instance_id)`
- plan과 set instance의 같은 프로젝트 소속은 DB FK만으로 완전히 표현하기 어려우므로 서버 validation과 migration orphan 검증을 함께 둔다.

`project_production_plan_set_item_values`

- `id`, `set_scope_id` FK, `production_plan_item_id` FK(`project_production_plan_items`)
- `planned_start_date`, `planned_end_date` (시작 ≤ 종료 check — `0058`의 기간 check와 동일 의미)
- `assigned_user_id` FK(`qms_users`), `required_headcount` (1~999 check — `0063`과 동일), `note`
- `row_version`, created/updated time
- unique `(set_scope_id, production_plan_item_id)`

기존 `project_production_plans`, `project_production_plan_items`, `project_production_plan_connections`의 schema는 변경하지 않는다. 1차 기획 후보 B의 “item 테이블에 세트 컬럼 추가”와 후보 A·C는 채택하지 않는다 (F01 resolution, 연결 FK·unique index·기존 query 의미 보존).

### 7.2 상태 흐름

```text
프로젝트 생성(Ul891Set + LinkedV1)
  → 공통 구조 snapshot 생성 + 모든 활성 세트 instance에 scope 생성(값 없음)
  → [계획 구조 편집] 공통 item·connection 변경, 항목 추가 시 모든 active scope에 빈 value row
  → [세트 일정 입력] scope 단위 value 저장 → scope revision 증가 + audit
  → [세트 추가] 새 instance에만 scope 생성(빈 값), 기존 세트 불변
  → [세트 취소] instance 상태 원본에 따라 read-only·활성 집계 제외 (scope 데이터 mutation 없음)
  → 모든 활성 scope × 필수 active 항목 기간 입력 → 프로젝트 '계획 완료'
```

### 7.3 기존 데이터 backfill (같은 migration `0064`)

- 대상: 기존 `structure_mode='Ul891Set'`이면서 plan `model_version='LINKED_V1'`인 프로젝트.
- 모든 세트 instance(Active·Cancelled 모두)에 scope를 만들고, 현재 공통 item의 `planned_start_date/planned_end_date`·`assigned_user_id`·`required_headcount`·비고(코멘트)를 각 scope value로 복사한다. 취소 instance scope는 이력 조회용이다.
- 기존 공통 item row·connection·audit는 삭제·수정하지 않는다. 사용자가 보던 값이 사라지지 않아야 한다.
- migration 이후 추가되는 세트는 빈 value로 시작하며 기존 세트의 완료 상태를 바꾸지 않는다 (F07).
- Legacy 모델의 Ul891Set 프로젝트와 비-UL891·평면 UL891은 backfill 대상이 아니며 기존 프로젝트 단위 계획을 유지한다.
- fresh DB 적용과 기존 `0063` DB upgrade를 모두 검증한다. additive·forward-fix 원칙을 지키고 기존 migration 번호를 수정하지 않는다.

## 8. API 계약

기존 route family를 확장하며 신규 최상위 route를 만들지 않는다.

1. `GET /api/projects/{projectId}/production-planning?setInstanceId=...`
   - additive 응답 필드: `scopes`(세트 label·사양명·사양 번호·인스턴스 번호·상태·active 패널 수·세트별 계획 입력 상태), `selectedScope`, `isSetScoped`.
   - `setInstanceId` 미지정: 세트형 프로젝트는 `전체` aggregate(9장 규칙)를, 비-세트 프로젝트는 기존 응답 의미를 그대로 반환한다(additive 필드 외 기존 형태 불변).
   - `setInstanceId` 지정: 해당 scope(취소 세트 포함, 취소는 read-only 표시)의 공통 item+connection에 scope value를 합성한 응답.
   - 다른 프로젝트/존재하지 않는 세트 identity, 비-세트 프로젝트의 set query는 422 field-level 오류.
2. 기존 `PATCH /api/projects/{projectId}/production-planning`
   - 계속 담당하는 것: 공통 item 구조(추가·이름·필수 여부·순서·비활성화), connections, 프로젝트 계획 노트, 부서별 담당자(assignee) 지정 — 기존 편집 계약 보존 (F02).
   - Ul891Set LinkedV1에서는 공통 item의 일정·담당자·필요 인원·코멘트 값 변경 요청을 field-level 오류로 거부한다(세트 값으로 오인 방지). 비-세트 프로젝트는 기존 동작 불변.
   - 구조 항목 추가 시 같은 transaction에서 모든 active scope에 빈 value row를 생성한다. 항목 비활성화 시 value 이력은 보존하고 조회·완료 분모에서 제외한다.
   - 기존 plan header `row_version` CAS는 구조 편집·legacy 경로용으로 유지한다.
3. 신규 `PATCH /api/projects/{projectId}/production-planning/set-scopes/{setInstanceId}`
   - 계획 기간·담당자·필요 인원·코멘트만 받는다. 항목명·필수 여부·순서·connections·assignees는 받지 않는다.
   - expected scope `row_version`과 항목별 value `row_version`을 검증하고 value·scope revision·audit을 단일 transaction으로 저장한다. 같은 세트 stale이면 409, 다른 세트와 무간섭.
   - scope 소속(프로젝트–plan–instance 일치), 취소·비활성 scope 쓰기 거부, 날짜 역전, 담당자 유효성, 필요 인원 1~999를 서버가 field-level 오류로 거부한다.
   - 권한은 기존 `ProductionPlanUpdate` 정책 재사용. 저장 성공 시 기존 `SyncStageWorkItemsAfterSaveAsync` 프로젝트 단위 sync를 유지하고 신규 알림·내 업무 종류를 만들지 않는다.
4. `GET .../history`
   - 세트 scope 변경은 entity/field label에 세트 표시명(사양명·인스턴스 번호)을 포함한다. 내부 raw identifier를 사용자 메시지에 노출하지 않는다. 기존 `project_audit_events` 재사용, 허용 entity type의 additive 확장.
5. Excel
   - 기존 프로젝트 단위 생산계획 Excel(가져오기 일괄 적용 포함) 계약은 비-세트 프로젝트에서 보존한다. Ul891Set LinkedV1 프로젝트의 일정 값 변경 행은 row-level 오류로 차단해 프로젝트 scope 값을 우회 수정하지 못하게 한다.
   - 신규 세트별 export는 만들지 않는다 (보류, 후속 판단).

## 9. 자동 실적과 전체 aggregate 계산

### 9.1 source 분류와 세트 필터 (F06)

- 서버가 source catalog를 고정 분류한다: `SetPanel` — MANUFACTURING_STEP_COMPLETED, LQC_PASSED, OQC_PASSED(패널별 최종 합격), CUSTOMER_INSPECTION_PASSED, FAT_PASSED, PACKED, DEPARTED, DELIVERED. `ProjectCommon` — PURCHASE_ORDERED, MATERIAL_RECEIPT_CONFIRMED, IQC_PASSED.
- 세트 scope 조회: `SetPanel` source는 기존 linked evidence 파생 SQL에 선택 세트의 active 패널(`panel_placeholders.set_instance_id`) 필터만 추가한다. `ProjectCommon` source는 필터하지 않고 `evidenceScope=ProjectCommon`을 응답에 붙여 표·근거에 `프로젝트 공통` 배지로 표시하며, 세트 진척에는 공통 prerequisite로 한 번 포함한다.
- 전체 aggregate: 공통 source는 target identity로 1회만, 패널 source는 모든 active 세트의 active 패널 identity로 dedupe해 세트 수만큼 중복하지 않는다.
- 자동 실적은 저장하지 않고 조회 시 파생하며 사용자가 수정하지 않는다.

### 9.2 전체 aggregate 표시 규칙 (F05)

- 계획 시작/종료: 해당 공통 item의 active 세트 value 중 가장 이른 시작일/가장 늦은 종료일.
- 계획 입력 상태: 모든 active 세트 입력 시 완료, 일부만 입력 시 부분 입력.
- 담당자: 모두 같으면 이름, 둘 이상이면 `세트별 상이`, 전부 미입력이면 `-`.
- 필요 인원: 입력된 active 세트 합계를 `총 N명`으로 표시하고, 미입력 세트 존재 여부를 별도 boolean으로 반환해 `총 N명 · 일부 미입력`으로 표시.
- 코멘트: 내용을 합치지 않고 `hasSetComments` boolean으로 `세트별 코멘트 있음`/`-` 표시.
- 전체 행의 세트별 상세 펼침에서 세트 label·기간·담당자·인원·코멘트를 확인한다.
- 취소 scope는 전체 계산에서 제외하되 scope selector의 `취소 세트` group에서 read-only 조회한다.

## 10. 완료 판정 정합 (F03)

계획 완료 판정을 모델별 단일 의미로 확정하고, 판정을 사용하는 모든 지점이 같은 helper/SQL 의미를 공유하도록 정렬한다.

- Legacy: 기존 `planned_date` 기준 유지.
- 일반 LinkedV1: 필수 active 공통 item의 `planned_start_date`와 `planned_end_date` 입력.
- Ul891Set LinkedV1: 모든 active 세트 scope × 필수 active 공통 item의 value 시작·종료 입력. 취소 세트 제외, active 세트 0개면 미완료.

정렬 대상 지점 (현재 구현에서 `planned_date` 단일 기준 또는 자체 기준을 쓰는 곳):

- `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`의 생산계획 단계 집계
- `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`의 프로젝트 목록 planned item 집계(2곳)
- `backend/src/Emi.Qms.Api/Home/HomeMetricsStore.cs`의 Home 지표 집계
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningStore.cs`의 목록·요약 판정
- Excel 관련 완료 표시 경로

화면, 프로젝트 목록, 전체 흐름, Home 지표와 Excel이 같은 판정으로 일치하는지 회귀 테스트한다. 비-세트 모델의 기존 판정 결과는 바뀌지 않아야 한다.

## 11. 생성·세트 추가·취소 lifecycle hook (F04)

- ProductionPlanning 영역에 공용 internal helper `EnsureSetPlanScopeAsync(connection, transaction, projectId, setInstanceId, actorId, ...)`를 둔다. plan이 없거나 Legacy이면 명시적 no-op, 이미 scope가 있으면 unique constraint로 수렴하는 idempotent 동작이다.
- 초기 생성: `ProjectStore`의 Ul891Set 생성 transaction에서 `CreateInitialProductionControlSnapshotAsync` 직후 모든 활성 instance에 helper를 호출한다.
- 후속 추가: `Ul891SetStore.AddSpecAsync`와 `IncreaseAsync`가 각 instance 생성 직후 같은 transaction에서 helper를 호출한다.
- 취소: `CancelInstancesAsync`는 계획 데이터를 mutation하지 않는다. scope의 read-only·집계 제외는 `ul891_set_instances.status` 원본을 조회 시 따른다.
- 세트 추가·취소의 기존 UL891 실행·발주·회수 계약은 변경하지 않는다.

## 12. Frontend 정보 구조와 UX

- 위치: 프로젝트 상세 생산관리 탭의 생산계획표(`ProductionControlLinkedPlanReadOnly`)와 계획·실적 일정표(`ProductionControlGantt`) 위에 공통 scope selector를 한 번 배치한다. 두 영역이 별도 tab state를 갖지 않고 항상 같은 scope로 함께 전환된다.
- scope 목록은 planning GET 응답의 additive `scopes` 필드로 수신한다. 이 화면에서 `getUl891SetStructure`를 추가 호출하지 않는다.
- Desktop:
  - active scope 12개 이하: `전체` + 사양 group의 세트 chip tab, 가로 스크롤.
  - 13개 이상: `전체` tab과 검색 가능한 세트 선택을 함께 제공.
  - selector와 scope summary에 사양명·사양 번호·실물 세트 번호·active 패널 수를 표시하고, 8열 생산계획표와 일정 날짜 축을 유지한다.
- 390px: `전체/세트별` 분기와 select 계열 한 열 선택, 세트 요약·계획 카드·실제 가로 막대 유지, page-level overflow 0.
- 입력 분리 (F02):
  - `계획 구조`: 공통 item·connection·assignee를 기존 방식으로 편집.
  - `세트 일정`: 선택한 active 세트의 기간·담당자·필요 인원·코멘트만 편집. `전체` scope에서는 편집 대신 세트 선택 안내.
  - 취소 세트는 read-only 배지와 사유 안내만 표시.
- feedback: scope별 loading·empty·error·success 구분, UX-001 A2의 구조화 feedback·field 오류 focus·중복 submit 잠금·stale(409) 안내와 다시 불러오기 재사용. scope 전환은 request fence로 늦은 이전 scope 응답이 현재 scope를 덮지 못하게 하고 프로젝트/scope key로 state를 재초기화한다(DESIGN-000 Change 003 패턴).
- 접근성: scope selector에 tab/select 역할과 선택 상태 aria 표기, 표·막대의 기존 role 유지.

## 13. 기존 기능과의 연결·비회귀 경계

- 프로젝트/업무/알림: workflow 생산계획 단계 완료 판정만 10장 기준으로 확장. 신규 알림·내 업무·Teams·메일 종류 없음. 기존 프로젝트 단위 work item sync 유지.
- 권한/관리자: 신규 정책·능력 없음.
- Excel/PDF/첨부: 기존 프로젝트 단위 Excel 계약 보존, 세트형 일정 값 변경 행만 row 오류 차단, 신규 세트별 export 없음.
- 삭제·복구/감사: 세트 취소는 기존 UL891 취소 계약을 따르고 계획 이력은 보존, hard delete 없음.
- 명시적 제외: 비-UL891·평면 UL891 생산계획 UX 변경, master 양식 version 정책 변경, 패널 제조·품질·물류 처리 단위 변경, 세트별 판매단가·납기·원가·BOM 입력, 신규 외부 연동.

## 14. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated DB와 고정 experiment 검수 runtime만 사용.
- migration: `0064` 1건 (7장). fresh와 기존 `0063` DB upgrade를 모두 검증. destructive 변경 없음.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider·worker 비활성 유지.
- runtime 교체: 없음. 고정 검수 runtime의 HMR/`dotnet watch` 갱신만.
- 게시 경계: 이 실험 branch의 local commit까지만. 대표 repo·`main`·push·PR·merge·Persistent UAT 제외, `main` merge 승인 `0/3` 유지.
- worktree: 현재 worktree에 생산계획·제조·양식 관련 미커밋 WIP가 존재한다. 구현 세션은 기존 WIP를 reset·정리하지 않고 이번 Task allowlist만 추가하며, 파일 overlap이 안전하지 않으면 commit을 보류하고 사유를 기록한다.

## 15. 구현 순서

1. migration `0064`: overlay 두 테이블·constraint·index·기존 Ul891Set LinkedV1 backfill. fresh/upgrade migration 테스트.
2. ProductionPlanning Backend: scope helper, scope 목록·세트 scope·aggregate read projection, 세트 필터 evidence와 `evidenceScope`, 공통 PATCH의 세트형 값 차단, 신규 set-scopes value PATCH·CAS·audit.
3. lifecycle hook: `ProjectStore` 초기 생성과 `Ul891SetStore` AddSpec/Increase의 같은 transaction scope 생성, Cancel의 무-mutation 확인.
4. 완료 판정 정렬: WorkflowStore·ProjectStore 목록·HomeMetricsStore·ProductionPlanning 목록/요약·Excel apply의 모델별 판정 단일화.
5. Frontend: 공통 scope selector, scope별 표·Gantt·요약, `계획 구조`/`세트 일정` 입력 분리, 취소 read-only, desktop/390px, request fence.
6. 검증·증빙·문서: isolated Full-Stack, 전체 회귀, desktop·390px privacy-safe screenshot, Implementation report·원장·Roadmap 동기화, local experiment commit(WIP overlap 확인 후).

## 16. 검증 계획

- 최소 테스트 (신규):
  - 세트 수와 무관하게 공통 item·connection이 한 벌인지, 구조 항목 추가 시 모든 active scope에 빈 value가 생기고 연결은 한 벌만 늘어나는지
  - 세트 A value 저장이 세트 B scope revision·value를 바꾸지 않고, 같은 세트 stale 저장만 409인지
  - scope 소속·취소 세트 쓰기·날짜 역전·담당자·필요 인원 field-level validation과 비-세트 프로젝트의 set query 422
  - 공통 PATCH의 세트형 일정 값 차단과 Excel row 오류 차단
  - 한 세트 패널의 제조 완료가 그 세트 scope에만 집계되는 세트 필터 실적, `ProjectCommon` 표시와 전체 dedupe
  - backfill이 기존 공통 값을 active/cancelled scope에 복사하고 base row·connection·audit를 보존하는지, 신규 세트가 빈 값으로 시작하고 기존 세트 완료 상태를 바꾸지 않는지
  - active 세트 전부 입력 전 workflow 미완료, 세트 취소 뒤 남은 active 세트 기준 재계산, active 0개 미완료
- 영향 영역 회귀: Backend 전체(`ProductionPlanningApiTests`, `ManufacturingAssemblyBatchApiTests` 포함), Frontend 전체(vitest), `PostgreSqlMigrationTests`의 fresh + `0063` upgrade, 비-UL891·평면 UL891·Legacy·일반 LinkedV1과 기존 Excel 비회귀, typecheck·lint·production build.
- PR/CI: 해당 없음 (local experiment only).
- 사용자 검수: isolated Full-Stack E2E 1본(UL891 세트 프로젝트 생성 → 세트 scope 계획 저장 → 한 세트 패널 실적 발생 → scope별 표시·전체 aggregate 확인) 후, 고정 검수 runtime의 desktop·390px privacy-safe screenshot과 함께 `BATCHED_FINAL` 일괄 검수 checklist로 handoff. 상태는 `사용자 검수 대기 — 마지막 일괄 검수`로 유지한다.

## 17. 완료 기준과 중단 조건

완료 기준:

- interview 8장 성공 기준 6항 전부: scope 동시 전환, 세트 간·master 불변, 세트 필터 실적과 공통 source 구분, 추가·취소·stale·권한·upgrade 무손실, 기존 프로젝트 비회귀, 전체 자동 검증 통과.
- 기존 프로젝트별 구조·연결 편집 능력이 세트 기능 도입 후에도 보존된다 (F02 회귀 기준).
- 완료 판정이 화면·목록·전체 흐름·Home·Excel에서 일치한다 (F03 회귀 기준).
- UX: desktop 8열 표·날짜 축 유지, 390px 한 열·overflow 0, loading/empty/error/stale/취소 read-only 구분.
- Open P0/P1/P2 `0/0/0`, 5종 산출물 상태·위치 추적, local commit은 WIP overlap 안전 확인 후 수행.

중단 조건: 문서·구현의 의미 있는 충돌(예: Excel 경로가 세트형 프로젝트에 Legacy 계획을 생성), migration이 기존 데이터에서 실패, fast-track 제외 경계(대표 repo·`main`·Persistent UAT·실제 provider·destructive 작업)를 넘어야 하는 경우 — 임의 우회 없이 blocking으로 보고하고 중단한다.

## 18. Codex 구현 지시문

1. `database/migrations/0064_*.sql`: `project_production_plan_set_scopes`·`project_production_plan_set_item_values` 생성(7.1장 constraint·check·unique), 기존 Ul891Set LinkedV1 backfill(7.3장), orphan 검증. fresh와 `0063` upgrade에서 `PostgreSqlMigrationTests` 검증.
2. `backend/src/Emi.Qms.Api/ProductionPlanning/` (Store·Contracts·Endpoints): `EnsureSetPlanScopeAsync` helper, GET의 `setInstanceId` query·`scopes`/`selectedScope`/`isSetScoped`·aggregate(9장), linked evidence 세트 필터와 `evidenceScope`, 공통 PATCH의 세트형 값 차단과 구조 항목 추가 시 active scope 빈 value 생성, 신규 `PATCH .../set-scopes/{setInstanceId}` value-only 저장·scope CAS·audit(8장), history 세트 label.
3. `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`와 `backend/src/Emi.Qms.Api/Ul891Sets/Ul891SetStore.cs`: 초기 생성·AddSpec·Increase의 같은 transaction scope 생성 hook, Cancel 무-mutation 확인(11장).
4. 완료 판정 정렬: `WorkflowStore`·`ProjectStore` 목록 집계·`HomeMetricsStore`·`ProductionPlanningStore` 목록/요약·Excel apply를 10장 모델별 판정으로 단일화하고 비-세트 모델 비회귀 테스트.
5. Frontend (`frontend/src/App.tsx` 생산관리 탭 영역, `frontend/src/api.ts`, 관련 type·`frontend/src/styles.css`): 공통 scope selector(12장 UX), scope별 표·Gantt·요약과 request fence·state 재초기화, `계획 구조`/`세트 일정` 편집 분리, 취소 read-only, desktop/390px.
6. 검증·마감: 16장 테스트 전부, isolated Full-Stack 1본, desktop·390px privacy-safe screenshot, Implementation report·검수 checklist·원장·Roadmap 동기화, allowlist 기반 local experiment commit(WIP overlap 안전 확인, 불안전 시 보류·사유 기록).

---

- 이 2차 기획은 review resolution을 모두 반영했으며, standing instruction에 따라 blocking decision 0으로 별도 사용자 확인 없이 Codex 구현으로 이어진다. 게시·`main` merge·Persistent UAT·실제 provider 승인은 부여하지 않는다.

openBlockingDecisionCount: 0
