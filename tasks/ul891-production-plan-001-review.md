# TASK-UL891-PRODUCTION-PLAN-001 — Codex 내용·구현 경계 Review

- reviewSource: `tasks/ul891-production-plan-001-planning.md`
- reviewStatus: `RESOLVED_FOR_SECOND_PLANNING`
- taskType: `NEW_FEATURE`
- openBlockingDecisionCount: `0`
- Open P0/P1/P2: `0/0/0`

## 1. 총평

1차 기획의 핵심 사용자 가치와 제품 방향은 유지한다. UL891 생산의 실제 분리 단위는 같은 사양을 공유하는 묶음이 아니라 서로 다른 시기에 제조·품질·출하될 수 있는 실물 세트 인스턴스다. 따라서 실물 세트별 독립 계획, 전체 read-only aggregate, 취소 세트 이력 보존, 패널 원본 실적의 세트 필터라는 권장안은 현재 UL891·부분출하 계약과 일치한다.

다만 1차 기획의 권장 DB안처럼 `project_production_plan_items` 자체를 세트별로 복제하면 실적 연결 FK, 기존 항목 unique index, 프로젝트 전용 항목·연결 편집과 모든 기존 목록/Excel/Workflow query의 의미가 동시에 바뀐다. 특히 연결은 `production_plan_item_id`를 직접 참조하므로 “세트 항목은 연결을 복제하지 않고 재사용”한다는 문장만으로는 구현되지 않는다. 이 부분은 별도 세트 scope/value overlay로 바꿔야 한다.

또한 기존 LinkedV1 프로젝트는 프로젝트 안에서 항목명·필수 여부·순서·실적 연결 자체를 수정할 수 있다. `전체`를 완전 조회 전용으로 바꾸면 이 기존 능력이 사라지므로, 프로젝트 공통 계획 구조 편집과 세트별 일정 값 편집을 명확히 분리해 보존해야 한다.

## 2. 기능 분류

| 분류 | 항목 | 판단 |
| --- | --- | --- |
| 유지 | 실물 세트 인스턴스별 독립 계획 | 같은 사양의 세트도 실제 착수·출하 시점이 달라 사용자 문제를 직접 해결한다. |
| 유지 | 전체 scope를 활성 세트 aggregate로 제공 | 이중 계획 입력을 없애고 전체 상황과 세트 상세를 같은 기준으로 비교할 수 있다. |
| 유지 | 패널 귀속 실적의 세트 필터 | 제조·LQC·OQC·전진검수·FAT·물류의 기존 개별 패널 원자를 정확히 재사용한다. |
| 유지 | 프로젝트 공통 source 배지 | 구매·자재·IQC에 세트 귀속이 없다는 현재 데이터 한계를 숨기지 않는다. |
| 유지 | 취소 세트 read-only 이력과 활성 집계 제외 | UL891 취소·발주 회수·감사 계약과 맞는다. |
| 추가 | 프로젝트 구조와 세트 값의 별도 저장 모델 | 실적 연결과 기존 항목 identity를 복제하지 않으면서 일정만 독립시킨다. |
| 추가 | 프로젝트 공통 구조 편집 보존 | 기존 프로젝트별 항목·필수 여부·순서·실적 연결 수정 능력을 제거하지 않는다. |
| 추가 | 전체 aggregate의 staffing/comment 표시 규칙 | 8열 표가 세트별 값 차이를 숨기지 않도록 결정적 표시가 필요하다. |
| 추가 | Workflow·목록·Excel의 모델별 완료 판정 | 현재 query 중 일부는 여전히 `planned_date`를 기준으로 하므로 set scope만 추가하면 완료 판정이 어긋난다. |
| 보류 | 세트별 Excel export | 이번 사용자 문제는 web 입력·조회이며 기존 export 범위를 확대할 근거가 부족하다. |
| 제거 | 세트별 `project_production_plan_items` 복제 | 연결 FK·unique·기존 query 의미를 깨고 같은 구조를 불필요하게 중복한다. |
| 제거 | 세트형 프로젝트에서 프로젝트 공통 구조 편집까지 차단 | 기존 승인 기능 회귀다. 일정 값과 구조 편집을 분리해야 한다. |

## 3. Repository 대조 Finding과 resolution

### `UPP-001-F01` — 세트별 plan item 복제와 실적 연결 불일치

- severity: P1
- 상태: `RESOLVED_FOR_SECOND_PLANNING`
- 원인: `project_production_plan_connections.production_plan_item_id`는 실제 plan item row를 직접 참조한다. 세트별 item row를 추가하면서 연결을 복제하지 않으면 세트 item은 연결이 없고, 연결까지 복제하면 source 정의와 audit가 세트 수만큼 중복된다.
- 영향: 세트 계획의 자동 실적이 모두 `연결 안 됨`이 되거나, 연결 편집 시 일부 세트만 다른 구조를 갖게 된다.
- resolution: 프로젝트 공통 `project_production_plan_items`와 connections를 구조 원본으로 그대로 둔다. 신규 `project_production_plan_set_scopes`와 `project_production_plan_set_item_values`가 세트별 기간·담당자·필요 인원·코멘트만 overlay한다. 세트 조회는 공통 item+connection에 선택 scope 값을 합성한다.

### `UPP-001-F02` — 기존 프로젝트별 항목·연결 편집 능력 소실

- severity: P1
- 상태: `RESOLVED_FOR_SECOND_PLANNING`
- 원인: 1차 기획은 `전체`를 read-only aggregate로 정의했지만 현재 LinkedV1 수정은 항목 추가·삭제·이름·필수·순서·connections까지 프로젝트별로 바꿀 수 있다.
- 영향: 세트 기능 도입이 기존 승인 기능을 제거한다.
- resolution: 조회의 `전체`는 aggregate로 유지하되 입력 화면은 `계획 구조`와 `세트 일정`을 분리한다. 기존 PATCH는 프로젝트 공통 구조·connections·담당자 지정에 유지한다. 구조 항목을 추가하면 같은 transaction에서 모든 active set scope에 빈 value row를 만들고, 비활성화하면 value 이력은 보존하되 조회·완료 분모에서 제외한다. 세트 값 저장은 전용 value-only endpoint를 사용한다.

### `UPP-001-F03` — 기존 Workflow 완료 query의 날짜 모델 불일치

- severity: P1
- 상태: `RESOLVED_FOR_SECOND_PLANNING`
- 원인: `WorkflowStore`의 생산계획 집계는 현재 `planned_date`를 직접 집계한다. LinkedV1은 `planned_start_date/planned_end_date`를 사용하고 세트형은 overlay 값을 사용하게 된다.
- 영향: 화면에서 모든 세트 계획을 입력해도 전체 흐름이 미완료로 남거나 반대로 너무 일찍 완료될 수 있다.
- resolution: 모델별 완료 SQL을 명시한다. Legacy는 기존 `planned_date`, 일반 LinkedV1은 공통 item의 시작·종료, Ul891Set LinkedV1은 모든 active set scope×필수 active item의 시작·종료를 사용한다. 취소 세트는 제외하고 active 세트가 0개인 비정상 구조는 완료로 간주하지 않는다. `ProductionPlanningStore` 목록·요약과 `WorkflowStore`가 같은 판정 helper/SQL 의미를 사용하도록 회귀 테스트한다.

### `UPP-001-F04` — 초기 생성과 후속 세트 추가 hook 차이

- severity: P1
- 상태: `RESOLVED_FOR_SECOND_PLANNING`
- 원인: 초기 프로젝트는 `Ul891SetStore.CreateInitialStructureAsync` 뒤에 `ProjectStore.CreateInitialProductionControlSnapshotAsync`가 실행되지만, 후속 사양 추가·수량 증가는 `Ul891SetStore`의 별도 transaction에서 instance를 만든다.
- 영향: 초기 세트만 계획 scope가 있거나, 후속 세트만 scope 생성이 누락될 수 있다.
- resolution: 공용 internal helper `EnsureSetPlanScopeAsync(connection, transaction, projectId, setInstanceId, actorId, ...)`를 ProductionPlanning 영역에 둔다. 초기 프로젝트는 plan snapshot 생성 직후 모든 instance에 호출하고, AddSpec/Increase는 각 instance 생성 직후 같은 transaction에서 호출한다. plan이 Legacy이면 명시적 no-op다. replay는 기존 UL891 operation transaction과 unique constraint로 수렴한다.

### `UPP-001-F05` — 전체 8열 표의 세트별 staffing/comment 축약 모호성

- severity: P2
- 상태: `RESOLVED_FOR_SECOND_PLANNING`
- 원인: 세트별 담당자·필요 인원·코멘트가 서로 다를 때 전체 행 한 칸에 단일 값을 표시할 수 없다.
- 영향: 전체 화면이 특정 세트 값을 전체 값처럼 오해하게 만들 수 있다.
- resolution: 전체 aggregate는 담당자가 모두 같으면 이름, 다르면 `세트별 상이`; 필요 인원은 입력된 active 세트 합계를 `총 N명`으로 표시하되 미입력 세트가 있으면 `N명 · 일부 미입력`; 코멘트는 단일 값으로 합치지 않고 값이 있으면 `세트별 코멘트 있음`, 없으면 `-`로 표시한다. 전체 행의 세트별 상세 펼침에서 각 값을 확인한다.

### `UPP-001-F06` — 공통 source의 진행률 의미

- severity: P2
- 상태: `RESOLVED_FOR_SECOND_PLANNING`
- 원인: 구매·자재·IQC는 set relation이 없으므로 모든 세트에 동일 evidence가 나타난다.
- 영향: 사용자가 세트별 실적으로 오해하거나 전체 aggregate에서 중복 집계할 수 있다.
- resolution: source catalog를 `ProjectCommon`과 `SetPanel`로 서버 고정 분류한다. 세트 scope에서는 공통 evidence에 `evidenceScope=ProjectCommon`을 붙이고 표·근거에 배지를 표시한다. 세트 진척에는 공통 prerequisite로 한 번 포함하되, 전체 aggregate에서는 target identity로 dedupe해 세트 수만큼 중복하지 않는다. 패널 source만 set filter를 적용한다.

### `UPP-001-F07` — 기존 세트형 프로젝트 backfill과 신규 세트 초기값

- severity: P2
- 상태: `RESOLVED_FOR_SECOND_PLANNING`
- 원인: 기존 프로젝트의 프로젝트 단위 일정 값을 보존해야 하지만 기능 도입 뒤 추가한 새 세트가 과거 일정 값을 자동 상속하면 새 주문 계획을 잘못 확정할 수 있다.
- 영향: 기존 화면 값 손실 또는 신규 세트의 의도치 않은 완료 판정.
- resolution: migration backfill은 기존 Ul891Set+LinkedV1의 모든 instance scope를 만들고 현재 공통 item 일정·staffing·comment를 각각 복사한다. 취소 instance도 이력용 scope를 만든다. migration 이후 새 세트는 공통 구조만 연결하고 value는 빈 값으로 생성한다. Legacy 세트 프로젝트는 프로젝트 단위 계획을 유지한다.

## 4. 권장 데이터 모델

기존 item/connection schema를 바꾸지 않는 additive overlay를 사용한다.

### `project_production_plan_set_scopes`

- `id`
- `production_plan_id` FK
- `set_instance_id` FK
- `row_version` (scope CAS)
- created/updated actor·time
- unique `(production_plan_id, set_instance_id)`
- plan과 set instance가 같은 project인지 DB 단독 FK로 완전히 표현하기 어려우므로 server validation과 migration orphan 검증을 함께 둔다.

### `project_production_plan_set_item_values`

- `id`
- `set_scope_id` FK
- `production_plan_item_id` FK
- `planned_start_date`, `planned_end_date`
- `assigned_user_id`, `required_headcount`, `note`
- `row_version`
- created/updated time
- unique `(set_scope_id, production_plan_item_id)`
- 날짜·필요 인원은 기존 `0063`과 같은 check를 적용한다.

프로젝트 공통 item은 항목 identity·이름·필수 여부·순서·connections의 source of truth다. 세트 value는 해당 item의 일정 입력만 가진다. 세트별 item soft delete를 제공하지 않는다.

## 5. 권장 API·상태 계약

1. `GET /api/projects/{projectId}/production-planning?setInstanceId=...`
   - additive `scopes`, `selectedScope`, `isSetScoped`.
   - set 미지정이면 전체 aggregate, 지정하면 해당 active/cancelled scope.
   - 비-set 프로젝트는 query가 없을 때 기존 byte-shape 의미를 유지하고 잘못된 set query는 422.
2. 기존 `PATCH /api/projects/{projectId}/production-planning`
   - 공통 item structure/connections, project notes, assignees를 계속 담당한다.
   - Ul891Set LinkedV1에서는 공통 item의 일정·staffing·comment를 세트 값으로 오인하지 않게 변경을 차단하거나 구조 전용 field만 허용한다.
3. 신규 `PATCH /api/projects/{projectId}/production-planning/set-scopes/{setInstanceId}`
   - 계획 기간·담당자·필요 인원·코멘트만 받는다.
   - expected scope row version과 각 value row version을 검증하고 단일 transaction 저장.
   - 항목명·필수 여부·순서·connections·assignees는 받지 않는다.
4. history는 entity/field label에 세트 표시명을 포함하고 raw internal identifier를 사용자 메시지에 노출하지 않는다.

## 6. 전체 aggregate 계산

- 계획 시작: 해당 공통 item의 active set value 중 가장 이른 시작일.
- 계획 종료: 해당 공통 item의 active set value 중 가장 늦은 종료일.
- 계획 입력 상태: 모든 active set에 필수 날짜가 있으면 완료, 일부만 있으면 부분 입력.
- 실제 기간·진행률: 공통 source는 target identity로 1회, 패널 source는 모든 active set의 active panel identity로 dedupe한 기존 계산.
- 담당자: 모두 같은 사용자면 이름, 둘 이상이면 `세트별 상이`, 전부 미입력이면 `-`.
- 필요 인원: 입력값 합계. 일부 미입력 여부를 별도 boolean으로 반환해 `총 N명 · 일부 미입력`을 표시.
- 코멘트: 구체 내용을 합치지 않고 `hasSetComments` boolean으로 `세트별 코멘트 있음` 표시. 행 펼침은 세트 label·기간·담당자·인원·코멘트를 보여 준다.
- 취소 scope는 전체 계산에서 제외하지만 scope selector의 `취소 세트` group에서 조회한다.

## 7. Frontend 정보 구조

- 프로젝트 상세 생산관리 탭의 계획표와 일정표 위에 공통 scope selector를 한 번 배치한다. 두 영역마다 서로 다른 tab state를 만들지 않는다.
- Desktop:
  - 최대 12개 active scope는 `전체` + 세트 tab을 가로 스크롤로 표시한다.
  - 13개 이상이면 `전체` tab과 검색 가능한 세트 선택을 함께 제공한다.
  - 사양명·사양 번호·실물 세트 번호·active 패널 수를 selector와 scope summary에 표시한다.
- 390px:
  - `전체/세트별` 분기와 native/select 계열 한 열 선택을 사용한다.
  - 계획표는 기존 카드, 일정표는 기존 실제 가로 막대를 유지한다.
- 입력:
  - `계획 구조`에서는 공통 item·connection을 기존 방식으로 편집한다.
  - `세트 일정`에서는 선택한 active set의 기간·담당자·필요 인원·코멘트만 편집한다.
  - 취소 set은 조회만 가능하고 이유를 안내한다.
- scope fetch는 request fence를 사용해 늦은 이전 scope 응답이 현재 scope를 덮지 못하게 한다.

## 8. 구현 순서

1. migration `0064`: 두 overlay table·constraint·index·existing Ul891Set LinkedV1 backfill·fresh/upgrade test.
2. ProductionPlanning scope helper·read projection·set filtered evidence·aggregate/status·set value PATCH.
3. ProjectStore 초기 생성과 Ul891SetStore AddSpec/Increase hook. Cancel은 데이터 mutation 없이 상태 원본을 따라 read-only/aggregate 제외.
4. WorkflowStore·ProductionPlanning 목록/요약·Excel apply의 model-aware 완료/차단 정합.
5. Frontend scope selector·조회 aggregate/set·구조/세트 입력 분리·desktop/390px.
6. isolated Full-Stack과 전체 회귀·시각 증빙·문서·local commit.

## 9. 검증 보강

- 같은 item 구조와 connection이 세트 수와 무관하게 한 벌인지 검증한다.
- 세트 A value 저장이 세트 B scope revision·value를 바꾸지 않고, 같은 세트 stale 저장만 409인지 검증한다.
- 공통 structure item 추가 시 모든 active scope value가 빈 상태로 생성되고 연결은 한 벌만 늘어나는지 검증한다.
- existing backfill은 기존 공통 계획 값을 active/cancelled scope에 복사하고 base row·connection·audit를 삭제하지 않는지 검증한다.
- 신규 세트는 빈 value로 시작하고 기존 세트 완료 상태를 바꾸지 않는지 검증한다.
- active 세트 전부 입력 전 Workflow 완료가 아니며 취소 뒤 남은 active 세트 기준으로 재계산되는지 검증한다.
- 전체 aggregate에서 공통 source가 세트 수만큼 중복되지 않고, 세트 scope에는 `프로젝트 공통`으로 표시되는지 검증한다.
- 비-UL891·FlatPanel·Legacy·일반 LinkedV1과 기존 Excel이 회귀하지 않는지 검증한다.

## 10. 2차 기획에 전달할 결론

- 1차 기획의 사용자-facing 방향과 8개 권장 선택은 유지한다.
- DB 후보 B의 “같은 item table에 set column 추가”만 제거하고 별도 scope/value overlay로 교체한다.
- 전체 aggregate와 세트 일정 입력 외에 기존 프로젝트 공통 구조·connections 편집 능력을 반드시 보존한다.
- Workflow·목록·Excel 완료 판정까지 같은 모델 분기 기준으로 맞춘다.
- 위 resolution을 반영하면 blocking decision은 0이며 Fable 2차 기획과 local experiment 구현으로 진행할 수 있다.
