# TASK-SALES-KPI-001 — 영업 연간 매출·목표 KPI 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전 (experiment fast-track 1차 기획)
> 목적: 영업 전용 연간 매출·목표 KPI workspace와 영업팀 Home graph의 구현 방향 확정

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/sales-kpi-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- sourceTask: `TASK-SALES-KPI-001`
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 영업 담당자가 월별 매출 실적·목표·달성률을 한 화면에서 비교할 수 없어 프로젝트별 판매금액과 개별 정산을 수기로 합산한다.
- 대상 사용자·역할: 영업 사용자(조회), 영업 부서장(조회 + 목표 관리 필요성 판단), System Administrator(전체 조회·운영 지원), 다른 부서(판매금액 비노출 유지).
- 정상 흐름: 영업 메뉴 진입 → 현재 연도 12개월 매출/목표 그래프와 금액 KPI 로드 → 연도·통화 선택 → 월별 비교 → 필요 시 관련 프로젝트로 이동.
- 예외·복구 흐름: 조회 실패 재시도, 목표 mutation 실패 시 기존 값 유지와 field/action feedback, 음수·통화 불일치·중복 월·stale version 서버 차단.
- 확정한 정책과 명시적 제외: 서버 집계·권한 authoritative, `Project.SalesAmount.Read` 민감정보 경계 유지, 다른 부서 Home 불변, 통화 자동 환산·회계/ERP 연동·forecast AI·완료 정산 원본 변경·다른 부서 권한 확대·대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge 제외.
- planning으로 넘긴 비차단 미결정 사항: 매출 인식 기준, 목표 관리 주체·이력 방식, 복수 통화 처리, 영업 메뉴 노출 기준, Home 밀도, 월 상세 drilldown 정도. 모두 fast-track standing rule에 따라 이 문서의 Repository 근거 권장안을 자동 채택한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

영업 사용자가 영업 메뉴와 영업팀 Home에서 연도·통화를 선택해 12개월 확정 매출과 월별 목표를 한 그래프로 비교하고, 누적 매출·목표·달성률·잔여 목표를 금액 KPI로 즉시 판단할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 영업은 프로젝트 목록의 판매금액(`Project.SalesAmount.Read` 보유자만 표시)과 프로젝트별 정산 화면을 개별로 열어 확인한다.
- 월별·연간 합산, 목표 대비 판단은 시스템 밖 수기 계산에 의존해 시간 손실과 계산 오류가 발생한다.
- 월별 목표 데이터는 현재 DB에 존재하지 않아 목표 대비 실적 관리 자체가 시스템에서 불가능하다.
- 현재 Home API의 영업 분기는 정수 count 카드 3개(담당 진행, 14일 내 납기, 정산 대기)만 제공하며 금액 정보가 없다.
- 이 기능이 없으면 연간 추세·이번 달 실적·잔여 목표 판단이 계속 시스템 밖에서 이루어지고 근거 추적이 남지 않는다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 영업 사용자 (`sales` role) | 연간 KPI·그래프·월별 근거 조회, 목표 입력 | 기존 project access scope + `Project.SalesAmount.Read` | 월별 목표만 (신규 `Sales.Target.Manage`) |
| System Administrator | 전체 영업 KPI 조회·운영 지원, 목표 입력 | 전체 (`Project.Read.All` + `Project.SalesAmount.Read` 보유) | 월별 목표만 |
| 다른 부서 | 없음 (메뉴 비노출) | 없음 — 서버 403 | 없음 |

- `Project.SalesAmount.Read`는 migration `0002` 기준 `sales`·`system-administrator` role에만 부여되어 있으며 이 Task는 부여 범위를 바꾸지 않는다.
- 목표 관리 권한은 신규 permission code `Sales.Target.Manage`로 분리해 `sales`·`system-administrator`에 seed한다(권장 근거는 12장). 조회 권한과 mutation 권한을 분리해 이후 role/permission 편집 기능이 생기면 재부여만으로 좁힐 수 있다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 연간 KPI 조회

1. 영업 사용자가 좌측/모바일 drawer의 `영업` 메뉴를 연다.
2. 시스템이 현재 연도(Asia/Seoul)·기본 통화의 12개월 확정 매출·목표 aggregate와 KPI를 서버에서 계산해 반환한다.
3. 사용자는 막대(월 확정 매출)·목표 표시(월 목표)를 비교하고, 누적 매출·목표·달성률·잔여 목표 카드를 확인한 뒤 연도·통화를 전환한다.

### 시나리오 B — 월별 근거 확인과 프로젝트 이동

1. 사용자가 그래프 아래 월별 상세에서 특정 월을 선택한다.
2. 시스템이 해당 월에 세금계산서가 발행된 완료 정산 목록(프로젝트 코드·명·발행일·금액)을 표시한다.
3. 사용자는 행을 눌러 기존 프로젝트 정산 화면(`/projects/<id>/settlement`)으로 이동한다.

### 시나리오 C — 월별 목표 입력

1. `Sales.Target.Manage` 보유 사용자가 `목표 관리` 영역을 열고 연도·통화를 선택한다.
2. 12개월 금액 grid를 입력·수정하고 저장하면 서버가 음수·통화 형식·stale version을 검증하고 한 transaction으로 반영하며 변경 월마다 append-only audit event를 남긴다.
3. 성공 시 action 근처에 성공 feedback과 갱신된 그래프, 실패 시 field 오류 또는 version 충돌 안내와 새로고침 유도를 표시한다.

### 시나리오 D — 영업팀 Home

1. 영업 부서 사용자가 Home에 진입한다.
2. 기존 count 카드 3개 대신 같은 `/api/sales/kpi` aggregate 기반 compact 12개월 그래프와 핵심 금액 KPI, `정산 대기` 카드가 표시된다.
3. `영업 KPI 전체 보기`로 영업 workspace로 이동하며, 두 화면의 같은 연도·통화 수치는 항상 일치한다(같은 endpoint 재사용).

### 시나리오 E — 권한 없음

1. 다른 부서 사용자에게는 `영업` 메뉴가 보이지 않고, URL 직접 진입 시 서버가 403을 반환한다.
2. 화면은 권한 부족 상태를 오류와 구분해 안내한다. 다른 부서 Home은 기존 구성 그대로다.

## 5. 기능 요구사항

### 필수

- [ ] 신규 additive migration으로 월별 목표 테이블과 append-only 목표 audit 테이블, `Sales.Target.Manage` permission seed 추가
- [ ] `GET /api/sales/kpi` — 연도·통화별 12개월 zero-fill 확정 매출, 월별 목표, 누적 KPI, 사용 가능 연도·통화 목록, 월별 근거 목록을 서버 authoritative로 반환
- [ ] `GET`/`PUT` 목표 관리 API — 12개월 batch 저장, version CAS, 검증, audit
- [ ] `영업` navigation 항목과 `/sales` workspace(연도/통화 control, SVG combo chart, KPI 카드, 월별 상세, 목표 관리)
- [ ] 영업 부서 Home의 compact graph·핵심 KPI 재구성 (같은 endpoint 재사용, 다른 부서 Home 불변)
- [ ] 권한 matrix(조회/목표/무권한/403)·집계 정확성(월 경계·zero-fill·통화 분리·scope)·동시성 테스트, Frontend lint/typecheck/unit/build, isolated Full-Stack E2E, desktop/390px synthetic screenshot

### 선택

- [ ] 예상 파이프라인 카드(진행 중 프로젝트 판매금액 합계, 그래프와 분리 표시)
- [ ] 목표 미입력 월의 입력 유도 empty-state 안내

### 명시적 제외

- [ ] 환율 자동 환산, 통화 혼합 합산, 회계/ERP/은행/국세청 연동
- [ ] 원가·마진·수금·채권·forecast AI, PDF 보고서
- [ ] 완료 정산·프로젝트 원본 변경, 기존 project scope·판매금액 권한 확대
- [ ] 월별 상세 목록의 선택 Excel export 편입 (기존 EXPORT-001 registry 20개 화면 계약 불변; 필요 시 후속 change)
- [ ] 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 영업 KPI workspace (`/sales`) | `영업` 메뉴, Home `영업 KPI 전체 보기` | 연도/통화 selector, 12개월 매출 막대 + 목표 선/marker, KPI 카드(이번 달·누적 매출·목표 누계·달성률·잔여 목표), 선택 시 파이프라인 카드, 월별 상세 목록 | 연도·통화 전환, 월 선택, 프로젝트 정산 화면 이동, 목표 관리 열기 | skeleton loading, 조회 실패 재시도, 권한 부족 구분 안내 |
| 목표 관리 영역 (workspace 내) | `/sales` 안 `목표 관리` (권한자만) | 연도·통화별 12개월 금액 grid, 월별 최근 수정 정보 | 금액 입력·저장 | 저장 성공/field 오류/version 충돌을 action 근처 표시, 첫 오류 focus, `aria-live` |
| 영업팀 Home | Home 진입 (부서=영업) | compact 12개월 그래프, 핵심 KPI 2~3개, `정산 대기` 카드, 상세 이동 link | 상세 이동 | 기존 Home widget loading/empty/error 패턴 재사용 |

확인할 UX 항목:

- 목표 미등록 월은 `목표 미등록`으로 표시하고 0 목표와 시각적으로 구분한다. 달성률은 연 목표 합계가 있을 때만 계산·표시한다.
- 디자인은 사용자가 제공한 WITHUS 참고 방향(흰 배경, 얇은 divider, 절제된 그림자, compact control, 파란 강조, 낮은 KPI 카드, 정돈된 grid)과 HOME-002 compact reference design을 따른다.
- 그래프는 외부 라이브러리 없이 inline SVG로 구현하고(현재 frontend에 chart 의존성 없음), 축·범례·값을 `aria` 텍스트와 표 형태 대체 표현으로 제공한다.
- 390px: KPI 요약 → 그래프 → 월 상세 한 열 구성, page-level horizontal overflow 0, 핵심 touch target 44px 유지.
- 통화 전환 시 모든 숫자(그래프·KPI·상세)가 같은 통화로 함께 바뀌며 혼합 표시가 없다.

## 7. 업무 규칙과 불변조건

- 확정 매출은 `Completed` 상태 영업 정산의 세금계산서 발행일(Asia/Seoul 기준 월)에 해당 프로젝트의 판매금액을 귀속해 집계한다. `Completed` 정산은 발행일이 항상 존재한다(`0037` check constraint).
- 판매금액 또는 통화가 미입력인 완료 정산 프로젝트는 합산하지 않고 `금액 미입력 n건`으로 분리 고지한다. 값을 추정하거나 0으로 꾸며내지 않는다.
- 모든 합계·달성률·잔여 목표는 단일 통화 안에서만 계산한다. 서로 다른 통화는 절대 합산하지 않는다.
- 집계·KPI 계산은 Backend가 authoritative하며 Frontend는 표시만 담당한다.
- 조회는 `Project.SalesAmount.Read` + 기존 project access scope(`Project.Read.All` 또는 project key 목록)를 서버에서 강제한다. UI 숨김은 보조 수단이다.
- 목표 mutation은 `Sales.Target.Manage`만 허용하고 음수·비ISO-4217 통화·중복 (연도, 월, 통화)·stale version을 서버가 차단한다. 목표 변경은 append-only audit event(변경 전/후 금액, actor, 시각)를 남긴다.
- 완료 정산·프로젝트·기존 audit 원본은 읽기만 하며 수정하지 않는다.
- soft-delete된 프로젝트(`deleted_at_utc` not null)는 집계에서 제외한다.
- 다른 부서 Home KPI와 운영 메뉴 조회 계약(HOME-002 Change 002)은 변경하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 프로젝트 판매금액·통화 | `projects.sales_amount numeric(18,2)`, `currency_code`(ISO 4217) | 기존 | 읽기 전용 |
| 완료 영업 정산 | `sales_settlements` — Completed는 immutable, 발행일 필수 | 기존 | 읽기 전용 |
| 월별 매출 목표 | 신규 테이블(권장명 `sales_monthly_targets`): 연도, 월(1~12), 통화, 금액(`numeric(18,2)` ≥ 0), version, 최초/최종 actor·시각, unique(연도, 월, 통화) | 신규 | 현재값 1행 overwrite + version CAS |
| 목표 변경 감사 | 신규 append-only 테이블(권장명 `sales_monthly_target_audit_events`): 대상 연도·월·통화, 이전/이후 금액, action, actor, 시각, `0042`식 append-only trigger | 신규 | append-only, 삭제 금지 |
| 연간 KPI aggregate | 저장하지 않는 파생값 — 조회 시 서버 계산 | 신규(계산) | 저장·캐시 없음 |

```text
목표 없음(미등록) → 목표 등록(Draft 없음, 즉시 유효) → 금액 수정(version+1, audit append) → (삭제 대신 0 또는 수정으로 관리)
```

목표 행 삭제는 v1 범위에서 제공하지 않는다(0원 목표와 미등록을 구분 유지하기 위해 별도 삭제 API가 필요해지면 후속 change로 다룬다).

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 월 귀속(Asia/Seoul, `SalesSettlementStore`의 기존 timezone 상수 패턴 재사용), 12개월 zero-fill, 통화 분리, KPI 산식(누적 매출·목표 누계·달성률·잔여 목표), 권한·scope 필터, 목표 검증·CAS.
- 필요한 조회와 mutation:
  - `GET /api/sales/kpi?year=&currency=` — 월별 확정 매출·목표 12개, KPI 요약, 사용 가능 연도·통화 목록, 월별 근거 목록(프로젝트 코드·명·발행일·금액), 금액 미입력 건수, 선택 시 파이프라인 요약. 정책: `Project.SalesAmount.Read` requirement + 기존 `ProjectAccessScope` 재사용.
  - `GET /api/sales/targets?year=&currency=` — 현재 목표 값·version 목록. 정책: 조회와 동일 requirement.
  - `PUT /api/sales/targets` — 연도·통화 + 12개월 금액·expectedVersion batch 저장. 정책: 신규 `Sales.Target.Manage`.
- 권한·validation: endpoint 정책은 기존 `PermissionRequirement`/`QmsPolicies` 패턴을 따른다. 검증 실패는 안정적 status(400/403/404/409)와 한글 field 메시지로 반환하고 raw SQL·내부 식별자를 노출하지 않는다.
- transaction·동시성·idempotency: 목표 batch 저장은 단일 transaction에서 월별 version CAS로 처리하고 하나라도 stale이면 전체 rollback 후 409를 반환한다. 같은 값 재저장은 version을 올리지 않고 audit도 남기지 않는다(중복 event 방지).
- audit trail: 변경된 월만 append-only event를 남기며 `0042`의 guard trigger 패턴을 재사용한다.
- 외부 provider 영향: 없음. 알림·Teams·메일 발송을 추가하지 않는다.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태는 확정하지 않으며 위 이름은 권장명이다.

## 10. Frontend 고려사항

- route/component: `App.tsx` navigation에 `영업` 항목(`canReadSalesAmount` gate), `View`에 `sales-kpi` 추가, `pathForView`/URL parser에 `/sales` 연결, 신규 `SalesKpiPage.tsx`(기존 페이지 단위 component 패턴), `api.ts`에 조회·저장 client 추가.
- loading/empty/error/success: 기존 Home widget과 workspace 패턴(skeleton, 재시도, 403 구분 메시지)을 재사용한다. 데이터 0과 목표 미등록, 권한 부족, 조회 실패를 서로 구분한다.
- 공통 Action Feedback: 목표 저장은 UX-001 A1/A2의 구조화 action feedback 계약(처리 중 중복 submit 차단, 성공/부분 실패/실패, 첫 오류 focus)을 따른다.
- 접근성: chart에 role·`aria` 요약과 표 대체 표현, keyboard로 월 선택 가능, selector label 연결.
- 390px/mobile/narrow pane: 한 열 재배치, overflow 0, 44px touch target, `useAdaptiveLayout` 재사용. Home compact graph는 모바일에서 KPI 우선 표시.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 월별 근거 행에서 기존 프로젝트 정산 화면으로 deep link. 내 업무·알림 생성은 추가하지 않는다.
- 권한/관리자: 신규 permission 1개를 seed migration으로 추가하며 관리자 권한 매트릭스 read-only 화면에는 자동 반영된다(권한 데이터 기반).
- Excel/PDF/첨부: 이번 범위 제외. 선택 export registry(20개 화면) 계약을 변경하지 않는다.
- Teams/Mail: 영향 없음.
- 삭제·복구/감사: soft-delete 프로젝트 집계 제외, 목표 감사는 신규 append-only 테이블로 self-contained.
- Home: `HomeMetricsStore`의 영업 분기 count 카드는 유지하되 영업 Home 화면 구성에서 `정산 대기`만 KPI 열에 재배치하고, 금액 그래프·KPI는 `/api/sales/kpi`를 직접 호출해 표시한다(권한이 없으면 기존 count 카드 fallback). Backend Home 계약 파괴 없이 Frontend 구성만 바꾸는 것을 권장한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 확정 매출 = 완료 정산 발행월 (권장) | `Completed` 정산의 `invoice_issued_date` 월에 `projects.sales_amount` 귀속. 진행 프로젝트는 별도 파이프라인 카드로만 표시 | 확정 근거(immutable 정산)와 1:1 연결, 예상치와 혼합 없음, 월 귀속 항상 가능 | 정산 완료 전 매출이 그래프에 없음 → 파이프라인 카드로 보완 |
| B. 생성일/납기일 기준 예상 매출 포함 | Active 프로젝트 금액을 납기월에 예측 귀속 | 미래 추세 표시 | 확정·예상 혼합으로 잘못된 실적 판단 위험, 인터뷰의 “섞지 않는 기준” 요구와 충돌 |
| C. 목표 version history 테이블 | 목표를 매 변경마다 새 row로 append | 완전한 시계열 | 조회·유효값 판정 복잡, v1 요구(현재값+감사) 대비 과설계 |
| D. 목표 overwrite + append-only audit (권장) | 현재값 1행 + 변경 event 감사 | 최소 안전안, `0042` 검증된 패턴 재사용 | 시점별 목표 재구성은 audit 조회로만 가능 |
| E. 외부 chart 라이브러리 | recharts 등 도입 | 구현 속도 | 신규 dependency·lockfile 변경, frontend 지침의 unrelated dependency 원칙과 충돌 |
| F. inline SVG chart (권장) | 의존성 없는 자체 SVG combo chart | 의존성 0, 디자인 토큰·접근성 직접 제어 | 구현 공수 소폭 증가 |

권장안: A + D + F. 목표 관리 주체는 신규 `Sales.Target.Manage`를 `sales`·`system-administrator`에 seed(조회·변경 권한 분리, 이후 좁히기 가능). 통화는 통화별 분리 + selector, 기본값은 해당 연도 확정 매출이 가장 큰 통화(없으면 KRW). 메뉴 노출은 `Project.SalesAmount.Read` 기반 UI gate + 서버 403. Home은 compact chart + 핵심 KPI + `정산 대기` + 상세 이동. 모두 fast-track standing rule의 자동 채택 대상이다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 disposable Full-Stack E2E DB만 사용한다.
- migration 필요 여부: additive `0043` 1개(목표·감사 테이블, permission seed). 기존 migration 수정 없음. fresh/existing DB 모두 검증한다.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 호출 금지 유지.
- runtime 교체 여부: 없음. 5174/5081 Development 계약 불변.
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo 반영·Persistent UAT 적용·`main` merge(승인 0/3). local experiment commit까지만 Change 001로 승인됨.

## 14. 검증 계획

- 최소 테스트: Backend Release build + 신규 Sales KPI/target 테스트(집계 정확성 — 월 경계·연도 경계·zero-fill·통화 분리·미입력 금액 분리·scope 필터, 권한 allow/deny matrix, 목표 검증·CAS 성공/충돌/동일값 재저장, audit append-only), migration catalog·fresh/existing 적용 테스트. Frontend lint(error 0)·typecheck·unit·build.
- 영향 영역 회귀: Home 관련 기존 테스트(영업 분기·타 부서 불변), navigation·URL parser 회귀, Backend 전체 suite(기준선 395)·Frontend 전체(기준선 104) 재실행.
- PR/CI: 이번 Task는 local commit까지만. isolated Full-Stack E2E(기준선 38 + 신규 시나리오: 완료 정산·목표 seed 후 `/sales`와 영업 Home 수치 일치, 무권한 403)와 cleanup을 수행한다.
- 사용자 검수: `BATCHED_FINAL` — desktop/390px synthetic screenshot(영업 workspace, 목표 관리, 영업 Home, 권한 없음 상태)을 증빙으로 남기고 사용자 직접 검수는 마지막 일괄 검수로 대기한다.

## 15. 완료 기준

- 기능/권한/데이터: 요구사항 필수 항목 전부 구현, 권한 없는 사용자 aggregate 접근 0, 통화 혼합 0, 목표 audit 누락 0.
- UX: desktop·390px에서 loading/empty/error/success·권한 상태 구분, overflow 0, 목표 미등록과 0 구분 표시.
- 자동 테스트: Backend·Frontend 전체와 신규 테스트, migration fresh/existing, isolated Full-Stack E2E 통과.
- 5종 산출물: implementation report·SOP·user manual·Roadmap/원장 update·user validation checklist의 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`로 기록하고 완료로 가장하지 않는다.
- PR 상태: N/A — local experiment commit만. 게시 승인 없음.
- 중단 조건: 문서·구현의 의미 있는 충돌 발견, 신규 blocking 정책 필요(예: 통화 목록 제한 정책 충돌), P0/P1 Finding, 기존 정산·프로젝트 계약 변경이 불가피해지는 경우 — 구현을 멈추고 보고한다.

## 16. 미결정 사항

fast-track standing rule에 따라 아래 비차단 항목은 본 문서의 권장안을 자동 채택하며, 사용자는 마지막 일괄 검수에서 재검토할 수 있다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 매출 인식 기준과 파이프라인 표시 | 완료 정산 발행월만(권장) / 예상치 혼합 | 권장안 자동 채택 예정 |
| 2 | 목표 관리 주체 | 신규 `Sales.Target.Manage`를 sales+admin seed(권장) / admin 전용 | 권장안 자동 채택 예정 |
| 3 | 복수 통화 기본값 | 연도 내 최대 확정 매출 통화, 없으면 KRW(권장) / 항상 KRW | 권장안 자동 채택 예정 |
| 4 | 영업 메뉴 노출 기준 | `Project.SalesAmount.Read` gate + 서버 403(권장) / 전체 노출 | 권장안 자동 채택 예정 |
| 5 | 영업 Home 구성 | compact chart + 핵심 KPI + 정산 대기 + 상세 이동(권장) / 그래프만 | 권장안 자동 채택 예정 |
| 6 | 월 상세 drilldown 정도 | 월 선택 시 완료 정산 목록 + 정산 화면 link(권장) / drilldown 없음 | 권장안 자동 채택 예정 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Sales/` 신규 KPI·target contracts/store/endpoints, `Program.cs` DI·endpoint 등록, `QmsPermissions`·정책 추가.
- Frontend: `frontend/src/App.tsx`(navigation·View·URL), 신규 `SalesKpiPage.tsx`와 chart component, `HomePage.tsx` 영업 분기, `api.ts`, `styles.css`.
- DB/Migration: `database/migrations/0043_*.sql` (additive).
- Tests/Scripts: `backend/tests/Emi.Qms.Api.Tests` 신규 테스트와 migration 테스트 갱신, frontend unit, Full-Stack E2E 시나리오.
- Docs: 종료 시 Roadmap·실험 완료 원장·5종 산출물.

## 18. Roadmap 연결

- 선행 Task: TASK-014A(완료 정산 데이터), TASK-HOME-001/002(Home·shell), DESIGN-001·MOBILE-002(디자인·adaptive 기준) — 모두 experiment 완료 상태.
- 후속 Task: 목표 관리 권한 세분화(role/permission 편집 UI), 월별 상세 선택 export change, 환율·회계 연동은 별도 신규 기능.
- 현재 Go/No-Go: 인터뷰 `PASS_CREATE`·명시적 Roadmap override 승인 기록. blocking decision 0.
- 별도 Task로 분리할 항목: 파이프라인 고도화(예측·수주 단계), KPI 알림·에스컬레이션.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-19 | experiment fast-track 실행 지시, 비차단 선택 권장안 자동 채택, local commit까지 승인 | 본 1차 기획 전체 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

이 초안은 2차 기획 확정 뒤 구현 세션이 사용할 계약 요약이며, Codex review와 2차 기획에서 조정될 수 있다.

1. additive `0043` migration을 작성한다: `sales_monthly_targets`(unique 연도·월·통화, 금액 ≥ 0, ISO-4217 check, version ≥ 1)와 append-only audit 테이블 + guard trigger(`0042` 패턴), `Sales.Target.Manage` permission·`sales`/`system-administrator` role seed(`0037` seed 패턴). 기존 migration은 수정하지 않는다.
2. Backend `Sales` namespace에 KPI 조회 store/endpoint를 추가한다. Asia/Seoul 월 귀속, 12개월 zero-fill, 통화 분리, 금액 미입력 분리 집계, `ProjectAccessScope` 필터, `Project.SalesAmount.Read` 정책을 적용하고 KPI 산식을 서버에서 계산한다.
3. 목표 조회·batch 저장 endpoint를 추가한다. 단일 transaction, 월별 version CAS, 전체 rollback-on-conflict, 변경 월만 audit append, `Sales.Target.Manage` 정책.
4. Frontend에 `영업` navigation(`canReadSalesAmount` gate), `/sales` route와 `SalesKpiPage`(연도/통화 selector, inline SVG combo chart, KPI 카드, 월별 상세, 권한자용 목표 관리 grid)를 구현한다. UX-001 action feedback·접근성·390px 기준을 따른다.
5. 영업 부서 Home을 compact chart + 핵심 KPI + `정산 대기` + 상세 이동으로 재구성하되 같은 KPI endpoint를 재사용하고, 권한 없으면 기존 count 카드로 fallback하며 다른 부서 Home은 변경하지 않는다.
6. Validation Matrix에 따라 Backend/Frontend 전체 회귀, migration fresh/existing, isolated Full-Stack E2E, desktop/390px synthetic screenshot을 수행하고 5종 산출물과 local experiment commit(allowlist stage)까지만 진행한다. push·PR·merge·Persistent UAT·provider는 금지한다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 6
