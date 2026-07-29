# TASK-PRODUCTION-CONTROL-001 — Deep Interview Round 1 (Fable 5)

- interviewRound: 1
- roundTheme: 데이터 모델·저장 전략·legacy 호환 (interview §12 항목 1, 2, 6, 7, 10)
- baselineSources: 누적 interview 문서, identity gate, change-001, Product Roadmap 생산관리 기준, `database/migrations/0009_production_planning_assignees.sql`, `database/migrations/0011_procurement_required_items.sql`, `database/migrations/0032_iqc_digital_reports.sql`, `database/migrations/0034_manufacturing_execution.sql`, `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningStore.cs`, `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`

이번 round는 additive migration 설계를 차단하는 데이터 모델 결정 5개를 묶었다. 부서별 실적 계산식(§12 항목 3·4·5)과 계획 baseline revision(항목 9)은 round 2, 표·Gantt 정보 밀도(항목 8)는 이후 round에서 질문한다.

확인된 현재 구현 기준선:

- `production_plan_template_steps`는 `sequence_number`·`step_name`·`is_required`만 갖고 stable semantic code 열이 없다. 프로젝트 snapshot(`project_production_plan_items`)은 nullable `template_step_id`, `step_name_snapshot`, 단일 `planned_date`, `note`, `row_version`을 저장한다.
- 구매 필수 항목 template는 Item text code로, IQC 디지털 report 항목은 `^[A-Z0-9_]{2,40}$` 형식의 `item_code`로 이미 semantic code 패턴을 사용한다. 반면 제조 실행 단계 snapshot(`panel_manufacturing_execution_steps`)은 순번·이름만 저장한다.
- 생산계획 완료 판정은 여러 곳(`WorkflowStore`, `ProjectStore`, `HomeMetricsStore`, `ProductionPlanningStore` 요약)에서 `is_required and planned_date is not null` 집계로 계산한다.
- 18단계 workflow 판정은 저장된 projection 없이 조회 시 SQL 집계로 파생하는 것이 현재 repository의 대표 패턴이다.

---

### 질문 1 — Item별 milestone 정의·snapshot·계획 기간의 additive schema 형태

**필요한 이유**: 확정 모델(stable semantic code + template version + 프로젝트 snapshot + 계획 시작·종료)을 현재 schema가 지원하지 않는다. 기존 표를 확장할지 병렬 신규 표를 만들지에 따라 migration, Backend store, 화면 조회 계약 전체가 갈린다.

**답변이 바꾸는 범위**: 신규 migration의 표 구성, 기존 생산계획 조회·수정 API의 재사용 정도, backfill 대상.

**선택지**:

- **A. 기존 표 확장(권장)** — `production_plan_template_steps`에 stable `step_code`를 추가하고, `project_production_plan_items`에 `step_code_snapshot`, `plan_start_date`, `plan_end_date`와 source binding 종류 열을 additive로 추가한다. 부서 연결 정의는 semantic code 기준의 작은 binding catalog(표 또는 Backend registry, 질문 2와 연동)로 둔다.
  - 장점: 기존 optimistic version·audit·Excel·조회 경로를 그대로 재사용하고 migration이 최소다. custom 항목(연결 안 됨) 보존이 자연스럽다.
  - 단점: 한 표가 계획 입력과 identity를 함께 담아 열이 늘어난다.
- **B. 병렬 신규 milestone 표** — 기존 표는 그대로 두고 `milestone 정의 + 프로젝트 milestone snapshot + 계획 기간` 표를 새로 만들어 조회 시 병합한다.
  - 장점: 기존 계약 무변경, 개념 분리가 깨끗하다.
  - 단점: 같은 계획 항목이 두 모델에 존재해 drift·이중 표시 위험이 있고 backfill·동기화 규칙이 추가로 필요하다.

**권장**: A. 기존 `template_step_id`+`step_name_snapshot` 구조가 이미 snapshot 계약을 갖고 있어 확장이 안전하고, repository의 additive migration 원칙과 일치한다.

### 질문 2 — 기본 milestone catalog와 semantic code를 고정하는 방식

**필요한 이유**: milestone code가 이름·순서에 흔들리지 않으려면 code의 출처와 관리 주체를 정해야 한다. 운영 template 내용 확정은 명시적 제외 범위이므로, 이번 Task에서 구조만 고정하는 방식을 골라야 한다.

**답변이 바꾸는 범위**: migration seed 내용, binding 정의의 저장 위치, 관리자 화면 포함 여부(범위 크기).

**선택지**:

- **A. migration seed + Backend binding registry(권장)** — 기본 catalog(예: 기존 default 4단계와 구매·자재·제조·품질·물류 연결형 milestone)의 semantic code를 IQC report 항목처럼 `^[A-Z0-9_]` 형식으로 migration에서 seed하고, code→부서 source 계산식 연결은 Backend의 deterministic registry로 고정한다. 관리자 편집 UI는 만들지 않는다.
  - 장점: 결정적이고 검증 가능하며 제외 범위(운영 내용 확정·관리자 기준정보화)를 침범하지 않는다. 기존 구매 필수 항목·IQC template의 seed 패턴과 일치한다.
  - 단점: catalog 변경에 migration이 필요하다.
- **B. 관리자 master-data 편집 포함** — catalog·binding을 관리자 화면에서 편집하게 만든다.
  - 장점: 운영 유연성.
  - 단점: 신규 권한·화면·audit 범위가 커지고, Roadmap의 “관리자 기준정보화” 후속 항목과 중복된다.
- **C. DB catalog 없이 Backend enum만** — code를 코드에만 정의한다.
  - 장점: 가장 단순.
  - 단점: template version·snapshot 계약과 어긋나고 Item별 차이를 표현하기 어렵다.

**권장**: A. 이번 Task는 identity 구조 고정까지만 담당하고 catalog 운영 관리는 기존 Roadmap 후속(관리자 기준정보화)에 남긴다.

### 질문 3 — 자동 실적 projection의 저장 전략

**필요한 이유**: 실적 시작·종료·진행률·차단 상태를 “source mutation마다 저장”할지 “조회 시 계산”할지에 따라 구매·자재·제조·품질·물류의 기존 mutation 경로를 건드리는 범위와 복구(reconciliation) 설계가 달라진다.

**답변이 바꾸는 범위**: 신규 projection 표의 유무, 부서 store 수정 범위, 부분 실패·backfill 흐름, 성능 특성.

**선택지**:

- **A. 조회 시 deterministic 파생(권장)** — 저장형 projection 없이 프로젝트 상세 조회 시 부서 원본 표에서 SQL로 실적·상태를 계산한다.
  - 장점: 원본과 항상 일치해 누락 refresh·재조정이 원천적으로 불필요하다. 18단계 workflow 판정이 이미 같은 패턴이라 검증 방법이 확립돼 있다. 부서 mutation 코드를 건드리지 않아 회귀 위험이 최소다.
  - 단점: 상세 조회 쿼리가 무겁다(단, 프로젝트 1건 범위로 한정된다).
- **B. mutation 시 저장형 projection 갱신** — 부서 원본 저장 transaction 이후 projection 표를 갱신하고 bounded reconciliation을 둔다.
  - 장점: 조회가 빠르고 이후 전사 일정 목록·KPI 확장이 쉽다.
  - 단점: 구매·자재·IQC·제조·품질·물류의 모든 mutation 경로에 refresh 연결이 필요해 누락·race 위험과 구현 범위가 크다.
- **C. hybrid** — 상세는 조회 계산, 목록·KPI만 저장 cache.
  - 장점: 균형.
  - 단점: 두 경로의 일관성 검증이 추가된다.

**권장**: A. 이번 범위는 프로젝트 상세 탭 단위 조회라 A로 충분하며, 실측 성능 문제가 확인되면 C로의 확장을 후속 Task로 분리한다.

### 질문 4 — legacy `planned_date`를 계획 시작·종료로 옮기는 보수적 규칙

**필요한 이유**: 기존 프로젝트에는 단일 `planned_date`만 있다. 이관 규칙이 정해져야 backfill migration과 기존 화면·집계 호환을 설계할 수 있다.

**답변이 바꾸는 범위**: backfill migration 내용, 기존 프로젝트의 캘린더·표 초기 표시, 질문 5의 판정 호환 결과.

**선택지**:

- **A. 시작=종료=`planned_date` 복사(권장)** — 값이 있는 항목은 계획 시작·종료를 같은 날로 채우고, 원본 `planned_date` 열은 삭제하지 않고 보존한다. semantic code backfill은 알려진 default 단계명(정규화된 exact match)만 수행하고, 매칭 실패 항목은 `수동 항목 / 연결 안 됨`으로 보존한다.
  - 장점: 정보 손실·추측이 없고 “같은 날 시작·종료 허용” 확정 정책과 일치한다. 기존 완료 판정이 그대로 유지된다(질문 5-A와 결합 시).
  - 단점: 실제 작업 기간보다 짧은 1일 막대로 보일 수 있다(사용자가 이후 수정 가능).
- **B. 종료일만 `planned_date`로 채우고 시작은 비움** — 예정일을 마감으로 해석한다.
  - 장점: 마감 의미 보존.
  - 단점: 기간 미완성 상태가 대량 생겨 `계획 미입력` 상태·완료 판정과 충돌한다.
- **C. 새 필드는 모두 비우고 legacy 값은 참고 표시만** — 재입력을 요구한다.
  - 장점: 가장 보수적.
  - 단점: 기존 입력이 사실상 무효가 되어 재입력 부담과 완료 판정 회귀가 크다.

**권장**: A. 추측 없는 유일한 무손실 규칙이며 질문 5의 호환 전략과 맞물린다.

### 질문 5 — 생산계획 완료 stage 판정과 새 기간 입력 필수성의 호환

**필요한 이유**: 현재 workflow 2단계(생산계획) 완료와 홈·목록 KPI는 여러 조회에서 `필수 항목의 planned_date 입력 완료`로 판정한다. 계획 기간 도입 후 이 판정 기준을 어떻게 할지는 18단계 판정 의미에 닿는 결정이라 사용자 확정이 필요하다.

**답변이 바꾸는 범위**: workflow·홈 KPI·생산계획 요약의 판정 SQL 수정 여부, 기존 완료 프로젝트의 판정 유지 여부, 수정 화면의 필수 입력 규칙.

**선택지**:

- **A. 판정 기준을 “필수 항목 모두 계획 시작·종료 입력”으로 교체(권장)** — 판정 위치 전체를 새 기간 기준으로 일관 교체한다. 질문 4-A(시작=종료 backfill)를 함께 채택하면 기존 완료 프로젝트의 판정 결과가 변하지 않는다.
  - 장점: 판정과 입력 모델이 하나로 유지되고 이중 기준이 없다. 18단계 순서·전체 진행률 공식은 그대로다.
  - 단점: 판정 SQL 수정이 여러 조회에 걸친다(회귀 테스트 필요). 질문 4에서 B/C를 고르면 기존 완료 프로젝트가 미완료로 되돌아가는 회귀가 생긴다.
- **B. 기존 `planned_date` 판정 유지** — 새 기간은 판정과 무관한 부가 정보로 둔다.
  - 장점: 판정 회귀 위험 0.
  - 단점: 기간을 입력하지 않아도 stage가 완료돼 일정 상태(`계획 미입력`)와 workflow 판정이 어긋나고, 단일 예정일 입력 UI를 계속 유지해야 한다.
- **C. 과도기 dual 판정** — legacy `planned_date` 또는 기간 입력 중 하나면 완료로 본다.
  - 장점: 점진 전환.
  - 단점: 이중 기준이 장기 잔존하고 화면·테스트 복잡도가 커진다.

**권장**: A(질문 4-A 채택을 전제). 판정 의미(“필수 계획 항목의 계획 입력 완료”)는 보존하면서 입력 모델만 기간으로 통일하는 가장 일관된 경로다.

---

- interviewStatus: QUESTIONS_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
