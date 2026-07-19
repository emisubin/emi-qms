# TASK-SALES-KPI-001 — 영업 연간 매출·목표 KPI Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 현재 `experiment/*` branch에서 사용자가 요청한 영업 전용 KPI 기능의 interview source of truth다. 사용자는 사용자-facing interview·중간 승인 없이 `Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·페이지별 screenshot·local commit`까지 이어가도록 명시했다. 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 push·PR·merge는 제외한다.

## Task Identity Gate

- proposedTaskId: `TASK-SALES-KPI-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `OPTIONAL_EXPERIMENT_FOLLOW_UP`
- roadmapNextGate: `OPTIONAL_EXPERIMENT_FOLLOW_UP`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-SALES-KPI-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 영업 사용자가 영업 메뉴와 홈에서 1년 월별 매출액과 목표를 한 그래프로 비교하고, 금액 중심 KPI로 현재 실적·달성률·잔여 목표를 빠르게 판단한다.
- Root Finding 또는 정책 결정: 현재 Home API는 부서별 정수 count 카드 세 개만 제공하고 영업 전용 메뉴가 없다. 프로젝트에는 판매금액이, 완료된 영업 정산에는 세금계산서 발행일이 있지만 월별 목표 데이터와 연간 집계 화면이 없다.
- 변경·검증 경계: additive migration, Backend authoritative aggregate·권한, Frontend 영업 workspace·Sales Home 재사용, isolated PostgreSQL, synthetic desktop/390px evidence만 포함한다.
- 보존할 불변조건: `Project.SalesAmount.Read` 민감정보 경계, project access scope, 완료 정산의 append-only 성격, 통화 혼합 금지, 다른 부서 홈 KPI 불변, 대표 repo·main·Persistent UAT·provider 불변.
- 예상 산출물: Fable 1차 planning, Codex review, Fable 2차 planning, migration·API·UI·tests, desktop/mobile screenshots, 종료 문서와 local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

동일 목적의 Task·branch·worktree·open/merged PR은 확인되지 않았다. 기존 `TASK-HOME-002`는 부서별 count KPI와 공통 shell까지만 완료했으며, 신규 영업 메뉴·월별 목표 data·금액 집계를 포함하지 않는다. 사용자의 이번 명시 요청을 Roadmap 순서 override로 기록한다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-19
- 요청: 영업팀이 영업 메뉴를 눌렀을 때 월별 매출액과 목표를 동시에 보는 1년 그래프를 메인으로 표시하고 금액 KPI 카드를 구성한다. 영업팀 홈 KPI도 같은 메인 그래프로 바꾼다.
- 승인 대체: 비차단 제품 선택은 Fable 권장안을 자동 채택한다.
- 게시 경계: local experiment commit만 승인. `main` merge 승인 `0/3`.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 영업 담당자는 프로젝트별 판매금액과 개별 정산을 각각 열어 보지만, 월별 실적·목표·달성률을 한 화면에서 비교할 수 없다.
- 해결할 문제: 연간 추세, 이번 달 실적, 누적 목표 대비 부족분과 완료된 매출 근거를 빠르게 파악해야 한다.
- 성공 결과: 연도를 선택해 12개월 매출/목표 그래프와 금액 KPI를 보고, 영업 메뉴의 상세 화면과 영업팀 Home에서 같은 기준값을 확인한다.
- 하지 않을 경우 영향: 프로젝트 목록의 개별 금액을 수기로 합산하고 목표 대비 판단이 시스템 밖에서 이루어진다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 불변조건 |
| --- | --- | --- | --- | --- |
| 영업 사용자 | 연간 매출·목표·금액 KPI 조회 | 기존 project scope 및 판매금액 권한 | 조회 전용 | 다른 통화 자동 합산 금지 |
| 영업 부서장 | 위 조회와 월별 목표 관리 필요성 판단 | 영업 범위 | 목표 입력 권한은 Fable 권장안 | 변경 actor·시각 감사 |
| System Administrator | 전체 영업 KPI 조회·운영 지원 | 전체 | 목표 관리 필요성은 Fable 권장안 | 민감정보 audit |
| 다른 부서 | 기존 홈·업무 화면 유지 | 판매금액 권한 없음 | 없음 | 영업금액 노출 금지 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 영업 메뉴 진입 → 현재 연도 12개월 그래프와 KPI 로드 → 연도 선택 → 월별 실적/목표 비교 → 필요 시 관련 프로젝트로 이동.
- 실적 기준 후보: 완료된 `sales_settlements.invoice_issued_date`의 월과 `projects.sales_amount`를 사용하면 실제 완료 정산과 금액 근거가 연결된다. Fable은 draft/active project를 예측 매출에 포함할지, 카드에 별도로 구분할지 권장한다.
- 목표 입력: production 목표가 현재 DB에 없으므로 값을 꾸며내지 않는다. 신규 목표의 등록 주체·상태·version·audit와 empty state는 Fable 권장안으로 정한다.
- validation: 음수·허용 범위 초과·통화 불일치·중복 월·stale version을 서버가 차단한다.
- 복구: 조회 실패는 재시도, 목표 mutation 실패는 기존 값을 유지하고 field/action feedback을 제공한다.

## 4. Data·integration·lifecycle

- 기존 data: `projects.sales_amount`, `projects.currency_code`, 완료된 `sales_settlements.invoice_issued_date`, `projects.completed_at_utc`, project responsibility/scope.
- 신규 data 후보: 연도·월·통화별 목표 금액, version, actor/time. 실제 최소 모델은 Fable 권장안.
- 집계: Asia/Seoul 월 경계, 12개월 zero-fill, 완료 정산 기준, 허용 통화별 분리. 숫자는 API가 authoritative하게 계산한다.
- 보존·감사: 목표 변경은 overwrite audit 또는 version history 중 최소 안전안을 택한다. 완료 정산과 프로젝트 원본은 수정하지 않는다.
- Excel·외부 연동: 이번 범위에 회계/ERP 연동, 환율 변환, 예측 AI, PDF는 포함하지 않는다. 기존 선택 내보내기 전역 계약을 새 목록에 적용할 필요는 Fable이 판단한다.
- migration: latest migration 다음 additive migration만 사용하고 existing/fresh DB를 검증한다.

## 5. UX와 운영 적용

- 영업 메뉴 메인: 상단 연도/통화 control, 월별 매출 막대와 목표 선 또는 비교 가능한 12개월 chart, 금액 KPI cards, 월별 상세 목록/근거.
- 영업팀 Home: count 카드 세 개 대신 같은 annual aggregate의 compact main graph와 핵심 KPI를 우선 배치한다. 다른 부서 Home은 기존 구성 유지.
- 디자인: 사용자가 제공한 WITHUS 참고 이미지처럼 흰 배경, 얇은 divider, 절제된 그림자, compact controls, 선명한 파란 강조, 낮은 높이의 KPI card와 정돈된 grid를 사용한다.
- 모바일: PC 축소가 아니라 KPI 요약 → 그래프 → 월 상세 순서의 한 열 구성, 390px overflow 0, 읽기 가능한 축/범례와 44px 핵심 touch target을 유지한다.
- loading·empty·error: skeleton/empty/재시도와 목표 미등록을 0 목표로 오인하지 않는 표시가 필요하다.

## 6. 포함·제외 범위

### 포함

- 영업 전용 navigation과 연간 KPI workspace
- 12개월 매출액·목표 동시 그래프
- 매출 누계·목표 누계·달성률·잔여 목표 등 금액 중심 KPI
- 영업팀 Home의 동일 aggregate compact graph 재사용
- 월별 목표의 honest data lifecycle에 필요한 최소 관리·권한·audit
- 서버 권한·scope·통화·월 경계·concurrency 검증
- desktop/390px adaptive UI, synthetic screenshot와 자동 테스트

### 제외

- 환율 자동 환산, 회계/ERP/은행/국세청 연동
- 원가·마진·수금·채권·forecast AI
- 완료 정산 원본 변경, 기존 project scope 확대
- 다른 부서의 판매금액 열람 권한 확대
- 대표 repo·main·Persistent UAT·실제 provider·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 요청 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 매출 인식 기준 | 생성/납기일은 예상치, 완료 정산 발행일은 확정 근거 | 확정 매출과 예상 파이프라인을 섞지 않는 기준 권장 | Fable 권장안 자동 채택 | No |
| 2 | 목표 관리 주체 | 영업 전원은 과도, 관리자만은 현업 지연 | 최소 권한·감사 가능한 주체 권장 | Fable 권장안 자동 채택 | No |
| 3 | 복수 통화 | 단순 합산은 잘못, 환산은 외부 기준 필요 | 통화별 분리와 통화 selector 권장 | Fable 권장안 자동 채택 | No |
| 4 | 영업 메뉴 노출 | 모든 사용자 노출은 금액 유출, 숨김은 기존 메뉴 정책과 차이 | `Project.SalesAmount.Read` 기반 노출/403 권장 | Fable 권장안 자동 채택 | No |
| 5 | Home 밀도 | 전체 workspace 복제는 과밀, count 카드만은 목적 미달 | compact chart + 핵심 카드 + 상세 이동 권장 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 월별 매출·목표·달성률을 수기 합산하지 않고 영업 메뉴와 Home에서 같은 기준으로 본다.
- 권장 범위: 완료 정산 기반 12개월 aggregate, honest target data, 통화별 graph/KPI, Sales Home compact reuse.
- 확정 정책: 서버 집계·권한 authoritative, 민감금액 권한 유지, 다른 부서 Home 불변, synthetic evidence만 사용.
- Deferred 비차단 결정: 목표 수정 권한·history, 예상 파이프라인 카드, 월 상세 drilldown 정도.
- Fable 판정: `COMPLETED_CONFIRMED`.

## 9. 성공 기준

- 영업 사용자가 12개월 매출/목표와 금액 KPI를 한 화면에서 확인한다.
- 영업팀 Home과 영업 메뉴의 같은 연도·통화 수치가 일치한다.
- 권한 없는 사용자는 영업 금액 aggregate를 얻지 못한다.
- fresh/existing migration, Backend build·권한/집계/concurrency tests, Frontend lint·typecheck·unit·build, isolated E2E, desktop/390px screenshot이 통과한다.

## 10. 사용자 확인

- [x] experiment 사용자-facing interview를 생략한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] open blocking decision 0이다.
- [x] 2차 기획 뒤 구현·검증·screenshot·local commit까지 승인한다.
- [x] main·대표 repo·Persistent UAT·provider·게시를 제외한다.
