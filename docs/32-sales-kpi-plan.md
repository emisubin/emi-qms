# TASK-SALES-KPI-001 — 영업 연간 매출·목표 KPI 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-SALES-KPI-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/sales-kpi-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/sales-kpi-001-planning.md` (판단 이력으로 보존, 수정 없음)
- codexReviewSource: `tasks/sales-kpi-001-review.md` (`CONDITIONAL_KEEP_WITH_RESOLUTIONS`, Finding 6건 전부 본 문서에 반영)
- approvalChange: `tasks/sales-kpi-001-change-001.md` (`fableSecondPlanningApproved: true`, target `docs/32-sales-kpi-plan.md`)
- planningApproved: true — Change 001의 experiment fast-track 범위
- implementationApproved: true — 본 문서의 blocking decision 0 조건, local experiment commit까지만

이 문서는 TASK-SALES-KPI-001의 최종 구현 source of truth다. 1차 기획의 유지 확정 내용을 보존하고 Codex review의 추가·보류·제거와 Finding resolution을 구현 가능한 계약으로 통합했다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 참조하며 복사하지 않는다. 이 문서는 대표 repo·`main` merge·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다.

## 1. 한 줄 목표

영업 사용자가 `영업` 메뉴와 영업팀 Home에서 연도·통화를 선택해 12개월 확정 매출과 월별 목표를 한 그래프로 비교하고, 누적 매출·목표 누계·달성률·잔여 목표를 금액 KPI로 즉시 판단할 수 있다.

## 2. 확정 기준선과 해결할 업무 문제

- 영업 담당자는 현재 프로젝트별 판매금액(`Project.SalesAmount.Read` 보유자만 표시)과 프로젝트별 정산 화면을 개별로 열어 월별 실적·목표·달성률을 시스템 밖에서 수기 합산한다.
- 월별 목표 데이터는 현재 DB에 존재하지 않으며, 값을 꾸며내지 않고 honest lifecycle로 신규 도입한다.
- 현재 Home API의 영업 분기는 정수 count 카드 3개(담당 진행, 14일 내 납기, 정산 대기)만 제공한다(`HomeMetricsStore`의 `sales` 분기).
- 확정 매출 근거는 `Completed` 상태 `sales_settlements`의 `invoice_issued_date`(migration `0037` check constraint로 발행일 필수)와 해당 프로젝트의 `projects.sales_amount`·`currency_code`다.
- interview 확정 정책: 서버 집계·권한 authoritative, `Project.SalesAmount.Read` 민감정보 경계 유지, 다른 부서 Home 불변, 통화 혼합 합산 금지, 완료 정산·프로젝트 원본 read-only, synthetic evidence만 사용.

## 3. 대상 사용자와 권한 (Review resolution 반영)

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 영업 사용자 (`sales` role) | 연간 KPI·그래프·월 상세 조회 | 기존 project access scope + `Project.SalesAmount.Read` | 없음 — 조회 전용 |
| System Administrator | 전체 영업 KPI 조회, 월별 목표 관리 | 전체 | 월별 목표만 (신규 `Sales.Target.Manage`) |
| 다른 부서 | 없음 (메뉴 비노출) | 없음 — 서버 403 | 없음 |

- **`SALES-KPI-TARGET-LEAST-PRIVILEGE` (P1) resolution 확정**: 신규 permission `Sales.Target.Manage`는 v1에서 `system-administrator` role에만 seed한다. `sales` role에는 seed하지 않으며 영업 사용자는 목표를 조회만 한다. 1차 기획의 sales 전원 seed 안은 제거됐다. 영업 부서장 목표 관리가 필요해지면 명시적 sales-target manager binding을 별도 change로 다룬다.
- `Project.SalesAmount.Read`의 기존 부여 범위(migration `0002`: `sales`·`system-administrator`)는 변경하지 않는다.
- 조회 정책은 기존 `QmsPolicies.ProjectSalesAmountRead`(`PermissionRequirement` 패턴) + 기존 `ProjectAccessScope`(`Project.Read.All` 또는 project key 목록)를 서버에서 강제한다. UI 숨김은 보조 수단이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 연간 KPI 조회

1. 영업 사용자가 좌측/모바일 drawer의 `영업` 메뉴를 연다.
2. 시스템이 현재 연도(Asia/Seoul)와 서버가 결정한 기본 통화의 12개월 확정 매출·목표 aggregate와 KPI를 계산해 반환한다.
3. 사용자는 월 확정 매출 막대와 월 목표 표시를 비교하고 KPI 카드를 확인한 뒤 연도·통화를 전환한다. 전환 시 그래프·KPI·상세가 같은 통화로 함께 바뀐다.

### 시나리오 B — 월별 근거 확인과 프로젝트 이동

1. 사용자가 그래프 아래에서 특정 월을 선택한다.
2. 시스템이 해당 월의 완료 정산 근거 목록(프로젝트 코드·명·발행일·금액)을 **선택 월에 한정한 bounded 조회**로 반환한다.
3. 사용자는 행을 눌러 기존 프로젝트 정산 화면(`/projects/<id>/settlement`)으로 이동한다.

### 시나리오 C — 월별 목표 입력 (System Administrator 전용)

1. `Sales.Target.Manage` 보유 사용자(v1: System Administrator)가 `목표 관리` 영역을 열고 연도·통화를 선택한다.
2. 12개월 금액 grid를 입력·수정하고 저장하면 서버가 음수·통화 형식·연도/월 범위·stale version을 검증하고 단일 transaction으로 반영하며 변경 월마다 append-only audit event를 남긴다.
3. 성공 시 action 근처 성공 feedback과 갱신된 그래프, 실패 시 field 오류 또는 version 충돌(409) 안내와 새로고침 유도를 표시한다. 권한 없는 사용자에게는 `목표 관리` 영역 자체를 표시하지 않고 서버도 403을 반환한다.

### 시나리오 D — 영업팀 Home

1. 영업 부서 사용자가 Home에 진입한다.
2. `Project.SalesAmount.Read` 보유 시 같은 `/api/sales/kpi` aggregate 기반 compact 12개월 그래프, 핵심 금액 KPI, `정산 대기` 카드가 표시된다. Home은 현재 KST 연도와 서버 기본 통화 규칙을 그대로 사용한다.
3. `영업 KPI 전체 보기` link는 현재 표시 중인 `year`·`currency` query를 보존한 채 `/sales`로 이동하므로 두 화면의 수치가 항상 일치한다.
4. **권한이 없으면 count 카드로 조용히 fallback하지 않는다.** 금액 영역을 숨기고 명시적 권한 안내 상태를 표시한다(`SALES-KPI-HOME-QUERY-DRIFT`·제거 항목 resolution). 다른 부서 Home은 기존 구성 그대로다.

### 시나리오 E — 권한 없음

1. 다른 부서 사용자에게는 `영업` 메뉴가 보이지 않고, URL 직접 진입 시 서버가 403을 반환한다.
2. 화면은 권한 부족 상태를 조회 오류와 구분해 안내한다.

## 5. 기능 요구사항

### 필수

- [ ] additive migration `0043` — 월별 목표 테이블, append-only 목표 audit 테이블 + guard trigger, `Sales.Target.Manage` permission을 `system-administrator` role에만 seed
- [ ] `GET /api/sales/kpi` — 연도·통화별 12개월 zero-fill 확정 매출·목표, KPI 요약, 사용 가능 연도·통화 목록과 서버 결정 기본 통화, 금액 미입력 건수, 분리된 파이프라인 요약을 authoritative로 반환. **월별 근거 목록은 포함하지 않는다**
- [ ] `GET /api/sales/kpi/months/{month}` — 선택 월의 완료 정산 근거 bounded 목록
- [ ] `GET /api/sales/targets` + `PUT /api/sales/targets` — 12개월 batch 저장, 월별 version CAS, 전체 rollback-on-conflict, 변경 월만 audit
- [ ] `영업` navigation 항목과 `/sales` workspace(연도/통화 control, inline SVG combo chart, KPI 카드, 월별 상세, 관리자용 목표 관리 grid)
- [ ] 영업 부서 Home의 compact graph·핵심 KPI 재구성(같은 endpoint 재사용, query 보존 deep link, 권한 이상 시 명시적 안내, 다른 부서 Home 불변)
- [ ] 권한 matrix·집계 정확성·KPI 산식 semantics·동시성·bounded 상세 테스트, Frontend lint/typecheck/unit/build, isolated Full-Stack E2E, desktop/390px synthetic screenshot

### 선택

- [ ] 목표 미입력 월의 관리자용 입력 유도 empty-state 안내

### 명시적 제외

- [ ] 환율 자동 환산, 통화 혼합 합산, 회계/ERP/은행/국세청 연동
- [ ] 원가·마진·수금·채권·forecast AI, 수주확률 기반 파이프라인 고도화, 목표 변경 승인 workflow, KPI 알림, PDF 보고서 (Review 보류 항목 — 후속 기능)
- [ ] 월 상세 목록의 선택 Excel export — 기존 EXPORT-001 registry 20개 화면 계약을 암묵적으로 확장하지 않고 후속 export change로 보류
- [ ] `Sales.Target.Manage`의 `sales` role seed (Review 제거 항목)
- [ ] 완료 정산·프로젝트 원본 변경, 기존 project scope·판매금액 권한 확대
- [ ] 실제 확정 매출·목표가 없을 때 synthetic production 값 seed — production/persistent DB에는 어떤 예시 데이터도 넣지 않는다
- [ ] 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider

## 6. 업무 규칙과 불변조건

- 확정 매출은 `Completed` 정산의 `invoice_issued_date`를 Asia/Seoul 기준 월로 귀속해 해당 프로젝트의 `sales_amount`를 집계한다. `SalesSettlementStore`의 기존 `SeoulTimeZone` 상수·`at time zone 'Asia/Seoul'` 패턴을 재사용한다.
- 판매금액 또는 통화 미입력인 완료 정산 프로젝트는 합산하지 않고 `금액 미입력 n건`으로 분리 고지한다. 값을 추정하거나 0으로 꾸며내지 않는다.
- 모든 합계·달성률·잔여 목표는 단일 통화 안에서만 계산한다. 서로 다른 통화는 절대 합산하지 않는다.
- **KPI 산식 확정 (`SALES-KPI-MISSING-TARGET-SEMANTICS` resolution)**:
  - 목표 누계는 목표가 등록된 월만 합산하고 `registeredTargetMonthCount`를 함께 반환한다.
  - 목표가 하나도 없으면 달성률·잔여 목표를 `0`으로 계산하지 않고 nullable(`목표 미등록`)로 반환한다. 달성률은 등록된 목표 누계가 0보다 클 때만 계산한다.
  - 잔여 목표 = `max(목표 누계 − 확정 매출 누계, 0)`, 초과 달성액 = `max(확정 매출 누계 − 목표 누계, 0)`으로 분리해 의미가 뒤집히지 않게 한다.
  - 0원 목표와 목표 미등록(null)은 데이터·API·화면 모두에서 구분한다.
- **파이프라인 분리 (`SALES-KPI-PIPELINE-CONFLATION` resolution)**: 진행 중 프로젝트 판매금액 합계는 별도 `예상 파이프라인` 카드로만 표시하고, 그래프·달성률·잔여 목표 등 모든 달성 산식과 색·label·계산 영역에서 분리한다.
- **입력 validation 경계 확정**: `year`는 `2000..2100`, `month`는 `1..12`, `currency`는 기존 프로젝트와 동일한 ISO-4217 3자리 uppercase 형식(`ProjectInputNormalizer.NormalizeCurrencyCode` 규칙과 일치)만 허용한다. 목표 금액은 `numeric(18,2)` 범위의 0 이상 값이다.
- 목표 mutation은 `Sales.Target.Manage`만 허용하고 음수·비ISO-4217 통화·중복 (연도, 월, 통화)·stale version을 서버가 차단한다. 목표 변경은 append-only audit event(변경 전/후 금액, action, actor, 시각)를 남긴다.
- 집계·KPI 계산은 Backend가 authoritative하며 Frontend는 표시만 담당한다.
- 완료 정산·프로젝트·기존 audit 원본은 읽기만 하며 수정하지 않는다. soft-delete 프로젝트(`deleted_at_utc` not null)는 집계에서 제외한다.
- 다른 부서 Home KPI와 운영 메뉴 조회 계약(HOME-002 Change 002)은 변경하지 않는다. Backend Home API(`HomeMetricsStore`) 계약도 변경하지 않고 영업 Home의 Frontend 구성만 바꾼다.

## 7. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 프로젝트 판매금액·통화 | `projects.sales_amount numeric(18,2)`, `projects.currency_code`(ISO 4217) | 기존 | 읽기 전용 |
| 완료 영업 정산 | `sales_settlements` — `Completed`는 immutable trigger 보호, 발행일 필수 | 기존 | 읽기 전용 |
| 월별 매출 목표 | 신규 `sales_monthly_targets`: `target_year`(2000..2100), `target_month`(1..12), `currency_code`(`^[A-Z]{3}$`), `amount numeric(18,2) >= 0`, `version >= 1`, 최초/최종 actor·시각, `unique(target_year, target_month, currency_code)` | 신규 | 현재값 1행 overwrite + version CAS |
| 목표 변경 감사 | 신규 `sales_monthly_target_audit_events`: 대상 연도·월·통화, 이전/이후 금액, action(`Create`/`Update`), actor, 시각 + append-only guard trigger(`0042`·`0037` 검증된 패턴 재사용) | 신규 | append-only, 삭제 금지 |
| 연간 KPI aggregate | 저장하지 않는 파생값 — 조회 시 서버 계산 | 신규(계산) | 저장·캐시 없음 |

```text
목표 미등록(null) → 목표 등록(즉시 유효, version 1, audit Create) → 금액 수정(version+1, audit Update) → (삭제 대신 0원 또는 수정으로 관리)
```

- 목표 행 삭제는 v1 범위에서 제공하지 않는다(0원 목표와 미등록 구분 유지). 삭제 API가 필요해지면 후속 change로 다룬다.
- 같은 값 재저장은 version을 올리지 않고 audit도 남기지 않는다(중복 event 방지).
- **`SALES-KPI-MIGRATION-ORDER` resolution**: 이 Task의 migration은 `database/migrations/0043_sales_monthly_targets.sql`(권장명)로 고정하고, TASK-ADMIN-002는 `0044`를 사용한다. 현재 최신 `0042` 이후 additive만 추가하며 기존 migration은 수정하지 않는다.

## 8. API 계약 (Backend authoritative)

### `GET /api/sales/kpi?year=&currency=`

- 정책: `QmsPolicies.ProjectSalesAmountRead` + 기존 `ProjectAccessScope` 필터.
- query 생략 시 서버가 현재 KST 연도와 기본 통화를 선택한다. **기본 통화 규칙(서버 결정, `SALES-KPI-HOME-QUERY-DRIFT` resolution)**: 해당 연도 확정 매출이 가장 큰 통화, 없으면 `KRW`. 응답에 실제 적용된 `year`·`currency`를 echo한다.
- 응답(12개월 고정, bounded — `SALES-KPI-UNBOUNDED-DETAIL` resolution으로 월별 근거 목록 미포함):
  - `months[12]`: 월, 확정 매출(zero-fill), 목표 금액(nullable — 미등록은 null), 완료 정산 건수
  - `kpi`: 이번 달 확정 매출, 확정 매출 누계, 목표 누계(등록 월만), `registeredTargetMonthCount`, 달성률(nullable %), 잔여 목표(`max(목표−매출,0)`), 초과 달성액(`max(매출−목표,0)`)
  - `availableYears`, `availableCurrencies`, `defaultCurrency`
  - `missingAmountCount`(금액·통화 미입력 완료 정산 프로젝트 수)
  - `pipeline`: 진행(Active) 프로젝트의 동일 통화 판매금액 합계와 건수 — 달성 산식과 완전 분리
- validation: year `2000..2100`, currency `^[A-Z]{3}$` 위반은 400과 한글 field 메시지. 권한 없음은 403. raw SQL·내부 식별자 비노출.

### `GET /api/sales/kpi/months/{month}?year=&currency=`

- 정책: 위와 동일. `month`는 `1..12`.
- 응답: 해당 월 완료 정산 근거 목록(프로젝트 id·코드·명, 발행일, 금액)만 반환하는 bounded projection. 연간 응답 비대화를 방지한다.

### `GET /api/sales/targets?year=&currency=`

- 정책: `ProjectSalesAmountRead`(목표 값은 그래프에 이미 노출되는 조회 정보). 응답: 월별 현재 금액(nullable)과 version.

### `PUT /api/sales/targets`

- 정책: 신규 `Sales.Target.Manage`(v1: System Administrator만 보유).
- 요청: 연도·통화 + 12개월의 `{month, amount(nullable 유지 불가 — 미등록 월은 요청에서 제외), expectedVersion(신규 등록은 null)}` batch.
- 처리: 단일 transaction, 월별 version CAS. 하나라도 stale이면 전체 rollback 후 409. 변경된 월만 audit append. 동일 값 재저장은 no-op.
- endpoint 등록·정책 연결은 기존 `PermissionRequirement`/`QmsPolicies`/`Program.cs` 패턴을 따른다. Backend 코드는 기존 `backend/src/Emi.Qms.Api/Sales/` namespace에 KPI·target용 contracts/store/endpoints를 추가한다.

## 9. 화면·UX 계약

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 영업 KPI workspace (`/sales`) | `영업` 메뉴, Home `영업 KPI 전체 보기`(query 보존) | 연도/통화 selector, 12개월 매출 막대 + 목표 선/marker combo chart, KPI 카드(이번 달·누적 매출·목표 누계·달성률·잔여 목표·초과 달성액), 분리된 예상 파이프라인 카드, 금액 미입력 n건 고지, 선택 월 상세 목록 | 연도·통화 전환, 월 선택, 프로젝트 정산 화면 이동, (관리자) 목표 관리 열기 | skeleton loading, 조회 실패 재시도, 403 권한 부족 구분 안내 |
| 목표 관리 영역 (workspace 내) | `/sales` 안 `목표 관리` — `Sales.Target.Manage` 보유자만 표시 | 연도·통화별 12개월 금액 grid, 월별 최근 수정 정보 | 금액 입력·저장 | UX-001 A1/A2 구조화 action feedback: 처리 중 중복 submit 차단, 저장 성공/field 오류/version 충돌을 action 근처 표시, 첫 오류 focus, `aria-live` |
| 영업팀 Home | Home 진입 (부서=영업) | compact 12개월 그래프, 핵심 금액 KPI 2~3개, `정산 대기` 카드, `영업 KPI 전체 보기` link | 상세 이동(year/currency query 보존) | 기존 Home widget loading/empty/error 패턴 재사용. 권한 이상 시 금액 숨김 + 명시적 권한 안내(조용한 count fallback 금지) |

UX 불변조건:

- 목표 미등록 월은 `목표 미등록`으로 표시하고 0원 목표와 시각적으로 구분한다. 달성률 카드는 목표 등록 월이 있을 때만 수치를 표시한다.
- 디자인은 WITHUS 참고 방향(흰 배경, 얇은 divider, 절제된 그림자, compact control, 파란 강조, 낮은 KPI 카드, 정돈된 grid)과 HOME-002 compact reference design을 따른다.
- 그래프는 외부 라이브러리 없이 inline SVG로 구현한다(현재 frontend에 chart 의존성 없음, 신규 dependency·lockfile 변경 금지). 축·범례·값은 `aria` 요약과 표 형태 대체 표현으로 제공하고 keyboard로 월 선택이 가능해야 한다.
- 확정 매출과 예상 파이프라인은 색·label을 분리하고 파이프라인을 그래프 막대에 섞지 않는다.
- 390px: KPI 요약 → 그래프 → 월 상세 한 열 구성, page-level horizontal overflow 0, 핵심 touch target 44px, `useAdaptiveLayout` 재사용. Home compact graph는 모바일에서 KPI 우선 표시.
- 통화 전환 시 그래프·KPI·상세의 모든 숫자가 같은 통화로 함께 바뀌며 혼합 표시가 없다.

## 10. Frontend 구현 계약

- `App.tsx`: navigation에 `영업` 항목(`canReadSalesAmount` gate — 기존 `permissions.includes('Project.SalesAmount.Read')` 재사용), `View`에 `sales-kpi` 추가, `pathForView`/URL parser에 `/sales` 연결(year/currency query 파싱 포함).
- 신규 `SalesKpiPage.tsx`(기존 페이지 단위 component 패턴)와 inline SVG chart component, `api.ts`에 KPI·월 상세·목표 조회/저장 client 추가, `styles.css` 확장.
- `HomePage.tsx`: 영업 부서 분기에서 `/api/sales/kpi` 직접 호출로 compact graph·KPI 표시, `정산 대기`는 기존 Home count 재사용, `영업 KPI 전체 보기`는 현재 year/currency query를 보존한 `/sales` 이동. 권한 이상 시 명시적 안내 상태. 다른 부서 분기는 변경하지 않는다.
- 데이터 0, 목표 미등록, 권한 부족, 조회 실패를 서로 구분해 표시한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 월 상세 행에서 기존 프로젝트 정산 화면으로 deep link. 내 업무·알림 생성은 추가하지 않는다.
- 권한/관리자: 신규 permission 1개(`Sales.Target.Manage`)를 seed migration으로 추가하며 관리자 권한 매트릭스 read-only 화면에는 데이터 기반으로 자동 반영된다.
- Excel/PDF/첨부: 범위 제외. 선택 export registry(20개 화면) 계약 불변.
- Teams/Mail/외부 provider: 영향 없음. 발송을 추가하지 않는다.
- 삭제·복구/감사: soft-delete 프로젝트 집계 제외, 목표 감사는 신규 append-only 테이블로 self-contained.

## 12. Review Finding Resolution 대조표

| Finding | Severity | 본 문서 반영 위치 |
| --- | --- | --- |
| `SALES-KPI-TARGET-LEAST-PRIVILEGE` | P1 | 3장 — `Sales.Target.Manage` admin-only seed, sales 조회 전용, 부서장 binding은 후속 change |
| `SALES-KPI-MISSING-TARGET-SEMANTICS` | P2 | 6·8장 — 등록 월 수 반환, nullable 달성률, 잔여/초과액 분리 산식, 0원과 미등록 구분 |
| `SALES-KPI-HOME-QUERY-DRIFT` | P2 | 4·8·9장 — 서버가 기본 통화·available options 결정, Home은 KST 현재 연도+서버 기본 통화, deep link query 보존 |
| `SALES-KPI-PIPELINE-CONFLATION` | P2 | 6·9장 — 파이프라인 별도 카드, 모든 달성 산식·그래프에서 제외 |
| `SALES-KPI-UNBOUNDED-DETAIL` | P2 | 8장 — 연간 응답은 12개월 aggregate만, 월 상세는 선택 월 bounded endpoint 분리 |
| `SALES-KPI-MIGRATION-ORDER` | P2 | 7장 — 영업 KPI `0043`, ADMIN-002 `0044` 순차 고정 |

Review의 유지 6개 항목(확정 매출 기준, zero-fill·통화 분리·미입력 분리, 동일 aggregate 재사용, inline SVG·adaptive, 목표 overwrite+append-only audit+CAS, 월 상세 deep link)은 1차 기획 내용 그대로 보존했다. 보류 항목(환율, forecast, 승인 workflow, KPI 알림, 월 상세 export)은 5장 제외 목록에, 제거 항목(sales 전원 목표 권한, 조용한 count fallback)은 3·4장에 반영했다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 disposable Full-Stack E2E DB만 사용한다.
- migration: additive `0043` 1개(목표·감사 테이블, admin-only permission seed). 기존 migration 수정 없음. fresh/existing DB 모두 검증하고 rollback은 forward-fix 원칙.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 호출 금지 유지. production 값 seed 금지 — 테스트 데이터는 isolated DB에만 생성한다.
- runtime 교체: 없음. 5174/5081 Development 계약 불변.
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo 반영·Persistent UAT 적용·`main` merge(승인 0/3). Change 001로 승인된 범위는 검증·screenshot·종료 문서 완료 뒤 local experiment commit까지다.

## 14. 검증 계획

- Backend 신규 테스트: 집계 정확성(Asia/Seoul 월 경계·연도 경계·zero-fill·통화 분리·금액 미입력 분리·scope 필터·soft-delete 제외), KPI semantics(목표 전무 → nullable 달성률, 일부 등록 → 등록 월만 누계 + `registeredTargetMonthCount`, 잔여/초과 분리), 권한 allow/deny matrix(조회/목표 mutation/무권한 403 — sales 사용자의 목표 저장 403 포함), 목표 검증·CAS(성공/충돌 409 전체 rollback/동일값 no-op), audit append-only trigger, 월 상세 bounded 응답, migration catalog·fresh/existing 적용.
- Frontend: lint(error 0)·typecheck·unit·build. chart·KPI·목표 grid·권한 상태 unit 테스트.
- 회귀: Home 기존 테스트(영업 분기 count 계약 유지·타 부서 불변), navigation·URL parser, Backend 전체 suite(기준선 395)·Frontend 전체(기준선 104) 재실행.
- isolated Full-Stack E2E(기준선 38 + 신규): 완료 정산·목표 seed 후 `/sales`와 영업 Home의 같은 연도·통화 수치 일치, deep link query 보존, 무권한 403, admin 목표 저장·충돌 시나리오, cleanup.
- 증빙: desktop/390px privacy-safe synthetic screenshot(영업 workspace, 목표 관리, 영업 Home, 권한 없음 상태). `docs/development/privacy-safe-evidence.md`의 projection 규칙을 따르고 raw body·식별자를 남기지 않는다.
- 사용자 검수: `BATCHED_FINAL` — checklist에 `사용자 검수 대기 — 마지막 일괄 검수`를 유지하고 완료로 가장하지 않는다.

## 15. 완료 기준과 중단 조건

- 기능/권한/데이터: 5장 필수 항목 전부 구현, 권한 없는 사용자의 aggregate 접근 0, 통화 혼합 0, 목표 audit 누락 0, sales role의 목표 mutation 경로 0.
- UX: desktop·390px에서 loading/empty/error/success·권한 상태 구분, overflow 0, 목표 미등록과 0원 구분 표시, 파이프라인·확정 매출 시각 분리.
- 자동 테스트: Backend·Frontend 전체와 신규 테스트, migration fresh/existing, isolated Full-Stack E2E 통과.
- 산출물: `docs/12-task-completion-policy.md`의 5종 산출물(implementation report·SOP·user manual·Roadmap/실험 원장 update·user validation checklist) 상태·위치 추적.
- Git: local experiment commit(allowlist stage)까지만. PR 상태 N/A, 게시 승인 없음.
- 중단 조건: 문서·구현의 의미 있는 충돌, 신규 blocking 정책 필요(예: 허용 통화 목록 제한 충돌), P0/P1 Finding, 기존 정산·프로젝트·Home 계약 변경이 불가피해지는 경우, ADMIN-002가 `0043`을 선점한 상태 발견 — 구현을 멈추고 보고한다.

## 16. Codex 구현 지시문 (최종)

1. `database/migrations/0043_sales_monthly_targets.sql`을 작성한다: `sales_monthly_targets`(unique 연도·월·통화, year 2000..2100·month 1..12·`^[A-Z]{3}$`·금액 ≥ 0·version ≥ 1 check), `sales_monthly_target_audit_events` + append-only guard trigger(`0042`/`0037` 패턴), `Sales.Target.Manage` permission seed — **`system-administrator` role에만** 부여. 기존 migration은 수정하지 않고 fresh/existing을 검증한다.
2. Backend `Sales` namespace에 KPI 조회 store/endpoint를 추가한다: Asia/Seoul 월 귀속(기존 timezone 패턴 재사용), 12개월 zero-fill, 통화 분리, 금액 미입력 분리 집계, 서버 결정 기본 통화·available options, 확정 KPI 산식(등록 월 누계·nullable 달성률·잔여/초과 분리), 분리된 파이프라인 요약, `ProjectAccessScope` 필터와 `ProjectSalesAmountRead` 정책. 월 상세는 선택 월 bounded endpoint로 분리한다.
3. 목표 조회·batch 저장 endpoint를 추가한다: 단일 transaction, 월별 version CAS, 전체 rollback-on-conflict 409, 변경 월만 audit append, 동일값 no-op, `Sales.Target.Manage` 정책과 400/403/409 한글 field 응답.
4. Frontend에 `영업` navigation(`canReadSalesAmount` gate), `/sales` route(year/currency query 파싱)와 `SalesKpiPage`(연도/통화 selector, inline SVG combo chart, KPI 카드, 파이프라인 카드, 월 상세, 관리자용 목표 관리 grid)를 구현한다. UX-001 action feedback·접근성·390px 기준을 따른다.
5. 영업 부서 Home을 compact chart + 핵심 KPI + `정산 대기` + query 보존 상세 이동으로 재구성한다. 같은 KPI endpoint를 재사용하고, 권한 이상 시 금액을 숨기고 명시적 권한 안내를 표시하며(조용한 count fallback 금지), 다른 부서 Home과 Backend Home 계약은 변경하지 않는다.
6. Validation Matrix에 따라 14장의 신규·회귀·E2E·screenshot 검증을 수행하고, 5종 산출물과 Roadmap·실험 원장 update 후 allowlist 경로만 stage해 local experiment commit까지만 진행한다. push·PR·merge·Persistent UAT·실제 provider·`main` merge는 금지한다(승인 0/3).

---

openBlockingDecisionCount: 0
