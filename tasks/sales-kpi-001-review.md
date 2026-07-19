# TASK-SALES-KPI-001 — Codex 기획 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/sales-kpi-001-planning.md`
- reviewScope: 사용자 문제·제품 방향·Repository 경계·권한·데이터 lifecycle·UX
- reviewedAt: `2026-07-19`
- overallDecision: `CONDITIONAL_KEEP_WITH_RESOLUTIONS`

## 1. 총평

월별 확정 매출과 목표를 한 그래프로 비교하고 같은 aggregate를 영업팀 Home에 재사용하는 방향은 사용자 문제에 직접 대응한다. 특히 완료된 영업 정산의 `invoice_issued_date`와 프로젝트 `sales_amount`를 연결한 확정 매출 기준, 통화별 분리, 목표 미등록과 0원 구분은 유지할 가치가 높다.

다만 1차 기획의 `sales` role 전원 목표 수정은 사용자가 요청하지 않은 권한 확대이며 조직 목표의 무단 변경 위험이 있다. 또한 화면 범위가 목표 편집·월 상세·예상 파이프라인까지 커진 만큼 KPI 정의와 빈 값 처리, Home과 workspace의 query 동기화를 최종 계약에 더 명확히 고정해야 한다.

## 2. 기능 판단

### 유지

- 완료된 정산의 세금계산서 발행월을 확정 매출 기준으로 사용한다.
- 12개월 zero-fill, 통화 selector, 통화 혼합 합산 금지, 금액 미입력 건수 분리를 유지한다.
- 영업 workspace의 main graph와 영업팀 Home compact graph가 같은 Backend aggregate를 사용한다.
- inline SVG와 표 형태 대체 표현, desktop compact layout과 390px 한 열 구성을 유지한다.
- 월별 목표는 별도 현재값 + append-only audit, CAS batch 저장으로 관리한다.
- 월 상세에서 완료 정산 근거와 기존 정산 화면 deep link를 제공한다.

### 추가

- 목표 수정 권한은 v1에서 `System Administrator`로 한정한다. 영업 사용자는 조회만 한다. 부서장 목표 관리가 필요하면 명시적 sales-target manager binding을 별도 change로 추가한다.
- 목표가 하나도 없는 경우 달성률·잔여 목표를 `0`으로 계산하지 않고 `목표 미등록`으로 반환한다. 일부 월만 등록된 경우 KPI의 목표 누계는 등록 월만 합산하고 등록 월 수를 함께 반환한다.
- 잔여 목표는 `max(목표-매출, 0)`, 초과 달성액은 `max(매출-목표, 0)`으로 분리해 의미가 뒤집히지 않게 한다.
- year는 `2000..2100`, month는 `1..12`, currency는 기존 프로젝트에서 사용하는 3자리 uppercase code와 동일한 validation을 적용한다. API 상세 목록은 월별 bounded 결과만 반환한다.
- 예상 파이프라인 금액은 확정 매출과 색·label·계산 영역을 분리하며 달성률에는 포함하지 않는다.
- Home은 현재 KST 연도와 workspace 기본 통화 선택 규칙을 사용하고, 상세 이동 시 동일 year/currency query를 보존한다.

### 보류

- 환율 환산, 수주확률 기반 forecast, 목표 변경 승인 workflow, KPI 알림은 후속 기능으로 보류한다.
- 월 상세 선택 Excel export는 기존 20개 화면 registry를 이번 Task에서 암묵적으로 확장하지 않고 후속 export change로 보류한다.

### 제거

- `Sales.Target.Manage`를 `sales` role 전체에 seed하는 안은 제거한다.
- 권한 없는 영업 Home에서 기존 영업 count 카드로 조용히 fallback하는 안은 제거한다. 권한 상태가 비정상이면 금액을 숨기고 명시적 권한 안내를 보여야 한다.

## 3. Finding과 Resolution

| ID | Severity | Finding | 최종 2차 기획 Resolution |
| --- | --- | --- | --- |
| `SALES-KPI-TARGET-LEAST-PRIVILEGE` | P1 | 1차 기획은 영업 role 전원에게 조직 목표 mutation을 부여해 사용자 요청을 넘어선다. | 목표 mutation은 System Administrator만 허용하고 sales는 조회 전용으로 고정한다. |
| `SALES-KPI-MISSING-TARGET-SEMANTICS` | P2 | 목표 미등록·일부 등록·0원 목표일 때 KPI 산식이 충분히 고정되지 않았다. | 등록 월 수와 nullable 달성률을 반환하고 잔여/초과액을 분리한다. |
| `SALES-KPI-HOME-QUERY-DRIFT` | P2 | Home과 workspace가 같은 endpoint라도 기본 통화·연도 선택이 달라질 수 있다. | 서버가 기본 통화와 available options를 결정하고 Home deep link가 query를 보존한다. |
| `SALES-KPI-PIPELINE-CONFLATION` | P2 | 진행 프로젝트 금액을 매출 카드에 섞으면 확정 실적 오인이 생긴다. | 별도 `예상 파이프라인` 카드로만 표시하고 모든 달성 산식에서 제외한다. |
| `SALES-KPI-UNBOUNDED-DETAIL` | P2 | 연간 응답에 모든 근거를 함께 실으면 프로젝트 수에 따라 응답이 비대해질 수 있다. | aggregate는 12개월만 반환하고 월 상세는 선택 월 endpoint 또는 bounded projection으로 분리한다. |
| `SALES-KPI-MIGRATION-ORDER` | P2 | ADMIN-002도 현재 latest 다음 번호를 제안해 두 Task가 같은 migration 번호를 사용할 수 있다. | 영업 KPI를 `0043`, ADMIN-002를 `0044`로 순차 고정한다. |

## 4. 권장 개발 순서

1. `0043` 목표·audit·admin-only permission migration과 DB constraints/append-only trigger.
2. Backend aggregate·월 상세·목표 batch API, 권한·scope·통화·월 경계 tests.
3. 영업 workspace graph/KPI/목표 관리와 adaptive UX.
4. 영업팀 Home compact graph 재사용과 다른 부서 Home 회귀.
5. isolated full-stack·screenshot·문서화.

## 5. 최종 구현 계약에 필요한 항목

- `openBlockingDecisionCount: 0`
- 위 Finding 6건의 resolution을 요구사항과 test 위치에 통합
- 실제 확정 매출·목표가 없을 때 synthetic production 값을 seed하지 않음
- representative repo·`main`·Persistent UAT·provider·push·PR·merge 제외
