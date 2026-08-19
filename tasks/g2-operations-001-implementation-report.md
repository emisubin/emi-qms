# TASK-G2-OPERATIONS-001 — G2 일일 운영관리 Implementation Report

## 1. 상태와 기준선

| 항목 | 결과 |
| --- | --- |
| Task | `TASK-G2-OPERATIONS-001` |
| Task 유형 | `APPROVED_FEATURE_IMPLEMENTATION` |
| Branch | `feat/task-g2-operations-001` |
| 구현 기준 | [Codex 기획안](g2-operations-001-codex-planning.md), [Codex review](g2-operations-001-codex-review.md), [Change 001](g2-operations-001-change-001.md), [Change 002](g2-operations-001-change-002.md), [Change 003](g2-operations-001-change-003.md), [Change 004](g2-operations-001-change-004.md), [Change 005](g2-operations-001-change-005.md), [Change 006](g2-operations-001-change-006.md), [Change 007](g2-operations-001-change-007.md), [Change 008](g2-operations-001-change-008.md), [Change 009](g2-operations-001-change-009.md), [Change 010](g2-operations-001-change-010.md), [Change 011](g2-operations-001-change-011.md), [Change 012](g2-operations-001-change-012.md), [Change 013](g2-operations-001-change-013.md), [Change 014](g2-operations-001-change-014.md), [Change 015](g2-operations-001-change-015.md), [Change 016](g2-operations-001-change-016.md), [Change 017](g2-operations-001-change-017.md), [Change 018](g2-operations-001-change-018.md), [Change 019](g2-operations-001-change-019.md), [Change 020](g2-operations-001-change-020.md) |
| 기준 SHA | 최신 원격 `main` 통합 commit `e1eb8b0` |
| 자동 검증 | 완료 |
| 사용자 검수 | local 합성 데이터 검수 서버 실행 / `사용자 검수 대기` |
| Commit / Push / PR / Merge | local commit 완료 / 원격 게시·merge 승인·실행 대기 |
| Persistent UAT / Azure 공개배포 | Azure 공개배포 승인·실행 대기 / 별도 Persistent UAT 미실행 |

이 문서는 feature branch의 코드 구현과 isolated 자동 검증 결과를 기록한다. 사용자는 2026-08-19 원격 `main` 병합과 Azure 공개배포를 명시 승인했으며, 실제 Git·CI·운영 결과는 [Azure Change 027](azure-deploy-001-change-027.md)에 따라 후속 기록한다.

## 2. 해결한 업무 문제와 범위

G2가 매일 관리하는 생산·납품·제조 출근 숫자를 기존 프로젝트 workflow와 섞지 않고 빠르게 입력하며, 월간 생산·납품·재고·목표·출근 흐름을 한 화면에서 확인할 수 있게 했다.

포함 범위는 다음과 같다.

- 왼쪽 `G2` 부모 메뉴와 홈·생산/출하 관리·제조 인원 출근 관리 세 화면
- 오전조·오후조 생산, 일일 납품, 오전·오후 EMI/도급 출근의 7개 독립 원본 값
- 과거·오늘·미래 날짜 입력, 서울 날짜 기준 미래 `예상` 표시와 날짜 도래 시 예상 숫자 자동 초기화
- 조회 시 계산하는 자동 재고, 오늘/과거 실사 checkpoint, 음수 재고 경고
- 적용 시작일별 일 생산목표·재고목표
- 역할별 조회·변경 권한, metric별 CAS와 mixed permission 원자 거부
- desktop 1440px·mobile 390px, 화면 읽기용 그래프 표, 키보드 접근 가능한 실사 dialog

제외 범위는 손익관리 placeholder와 기능, 기존 PMS 데이터 연결, 개인 근태, 예상 대비 실적, 납품목표, Excel/PDF/첨부/알림, 운영 적용과 Git 게시다. 손익관리는 영업팀 전용 별도 `NEW_FEATURE`로 추적한다.

## 3. 아키텍처와 기술적 결정

### Database와 migration

additive migration `0081_g2_operations.sql`이 G2 원본 schema와 권한을 추가하고, forward-fix `0082_g2_forecast_expiry.sql`이 미래 예상값 식별과 날짜 도래 초기화를 지원한다.

- `g2_daily_metrics`: 날짜·metric별 nullable 현재 값, version, 생성·수정자와 시각
- `g2_inventory_counts`: 날짜별 실사 현재 값과 version
- `g2_targets`: 목표 유형·적용 시작일별 현재 값과 version
- `G2.Read` 등 permission 6개와 승인된 10개 역할 조회·부서별 변경 matrix
- `g2_daily_metrics.is_forecast`, 예상값·수량 정합 constraint와 만료 조회용 partial index

기존 migration은 수정하지 않았다. `0081` 적용 뒤 결함이 발견되면 migration 파일을 되돌려 수정하지 않고 다음 additive forward-fix migration을 사용한다.

### Backend, API와 동시성

`/api/g2` 아래 월간 홈·일별 범위 조회, 생산/납품·출근·실사·목표 mutation을 추가했다. 날짜·metric별 PostgreSQL transaction advisory lock과 expected version을 사용한다. 같은 metric 경쟁은 먼저 완료된 한 건만 성공하고 나머지는 `409`, 다른 metric 경쟁은 모두 보존된다. 한 요청에 허용되지 않은 field가 섞이면 DB 접근 전에 전체 `403`으로 거부한다.

재고는 일별 결과를 저장하지 않고 가장 가까운 이전 실사와 생산·납품 원본으로 조회 시 계산한다. 실사 날짜는 계산값을 대체하고, 이전 날짜 정정은 다음 실사를 넘지 않는다. 원본의 빈 값과 0은 구분하되 재고 공식에서만 빈 값을 0으로 계산한다.

미래 생산·납품·출근 숫자는 저장 시 예상값으로 식별한다. 서울 날짜가 도래한 뒤 첫 G2 조회 또는 저장은 해당 날짜까지 남은 예상 숫자만 `null`로 바꾸고 version을 증가시킨 뒤 응답과 재고를 계산한다. 예상 `0`도 초기화하며, 당일 다시 입력한 값과 실제 `0`은 예상값이 아니므로 유지한다. 행 자체를 삭제하지 않아 nullable 현재값과 metric별 CAS를 보존한다.

### Frontend와 UI·UX

`/g2`, `/g2/operations`, `/g2/attendance` route를 기존 navigation shell에 연결했다. 홈 첫 그래프는 생산·납품 나란한 막대와 재고·재고목표 선을 함께 표시한다. 생산은 파스텔 파랑, 납품은 파스텔 주황, 재고·예상재고는 빨강 실선·점선, 재고목표는 파랑 점선이다. 모든 flow 계열은 숫자가 큰 값이 더 높게 보이는 공통 2단계 scale과 좌우 tick `0·20·40·60·100·140·180`을 사용한다. `0~60`은 plot 높이의 `70%`, `60~180`은 `30%`를 사용하되 화면의 압축 안내 문구와 `//` marker는 제거했다. 실제·예상 재고와 재고목표 아래에는 white halo를 그리고, 생산·납품 막대 위 수량은 작은 글꼴과 근접 값 세로 분리로 겹침을 막는다. 둘째 그래프의 오전조는 연한 파랑, 오후조는 진한 파랑, 일 생산목표는 빨강 점선이며 왼쪽 축은 `0~60/10`이다. 오전·오후 segment는 하나의 top-rounded clip과 outer outline 안에서 맞닿고 모든 막대 바닥은 굵은 `0` baseline에 연결된다. 막대 위 합계 숫자는 표시하지 않고 오전·오후 segment 내부 숫자를 유지한다. 어느 segment에 올려도 날짜별 단일 hit area가 오전·오후·전체 생산량을 한 안내 상자에 표시한다. 선택 범위의 총 생산 평균은 막대·숫자보다 먼저 그리는 순수 파랑 점선과 hover로 표시해 수치를 가리지 않는다. 재고는 모든 날짜의 작은 채움 point와 수량을 표시하며, 실사 날짜 point·수량은 pastel blue와 넓은 투명 hit area로 구분해 `실사 0대`도 정확히 안내한다. 토요일·일요일·활성 한국 공휴일은 graph 날짜와 모든 G2 가로표의 해당 날짜 열 글자를 빨간색으로 표시한다. 막대와 선 hover는 cursor 변경 없이 날짜·항목·수량 tooltip을 제공한다. Desktop은 그래프를 왼쪽, 선택 범위의 생산·납품·재고 평균과 오전·오후 생산 평균 KPI를 오른쪽에 배치한다. 모든 KPI의 왼쪽 굵은 강조선은 제거하고 오전·오후 KPI 배경은 막대와 같은 파랑 gradient를 사용한다. 재고 부족분만 Backend의 서울 기준 오늘 날짜 `재고목표 - 재고`를 사용하며 음수는 `0대`, 오늘 자료가 없으면 `—`로 표시한다. `i` hover·focus는 불투명한 pastel violet 안내로 계산식과 기준일을 재고 부족분 카드 안에서만 표시해 그래프와 다른 KPI를 가리지 않는다. Mobile은 그래프 frame·왼쪽/오른쪽 축·grid를 한 화면에 고정하고 가운데 날짜 data layer만 첫 화면 5일 기준으로 넓혀 내부 가로 drag를 제공하며, KPI는 아래 2열로 재배치한다. 두 그래프 모두 같은 수치를 제공하는 화면 읽기용 표를 유지한다.

관리 화면은 제공한 field만 저장하고 빈 값 삭제와 실제 0을 구분한다. 미래 날짜에는 `예상` 안내를 표시하며, 값마다 마지막 수정자·시각을 보여준다. 홈의 필수 실사·목표 수량은 공백을 `0`으로 바꾸지 않고 저장을 차단하며 실제 `0`은 계속 허용한다. 홈 월·목표일과 두 관리 화면의 초기 입력일은 Backend와 같은 `Asia/Seoul` 날짜를 사용한다. 월 이동은 effect 한 곳에서 한 번만 조회하고 요청 sequence로 오래된 응답을 무시하며, 관리 화면의 같은 달 날짜 변경은 받은 월 자료를 재사용한다. 홈 생산표·출근표와 두 관리 화면의 월간 표는 항목을 행, 날짜를 열로 통일했고, 두 관리표의 중복 `구분` 행은 제거해 미래 날짜 header에만 `예상`을 표시한다. 홈 목표 관리는 생산 현황 바로 위에 있고, 생산표는 오전·오후 생산·생산 합계·납품과 자동 재고를 보여준다. 재고행은 빨간 글씨 대신 출근 전체 합계행과 같은 굵은 상단선·행 배경 구조에 pastel red 색을 적용한다. 홈의 공용 날짜 범위는 두 그래프와 두 visible table에 함께 적용되고, 각 관리 표도 독립 날짜 범위를 제공한다. 홈과 제조 인원 출근 관리 월간표는 오전·오후 합계를 기본 표시하고 평면적인 왼쪽 header 또는 날짜별 합계 숫자 cell로 해당 조의 EMI·도급 인원을 독립적으로 펼친다. 홈은 두 조를 단순 합산한 `오전·오후 전체 합계`, 관리 화면은 `하루 총원`을 항상 표시한다. button semantics·keyboard focus는 보존하지만 persistent button chrome은 표시하지 않는다. 모바일의 넓은 월간 표는 카드 내부에서만 스크롤되고 페이지 전체 가로 overflow는 0이다.

### 검토한 대안

- 날짜별 넓은 행 하나는 오전·오후 담당자 동시 입력 유실 때문에 제외하고 metric별 행을 선택했다.
- 일별 재고 materialization은 과거 정정과 실사 경계 불일치 위험 때문에 제외하고 조회 시 계산을 선택했다.
- 하나의 `G2.Update` permission은 제조 사용자의 납품 변경 누수 때문에 제외하고 업무 능력별 permission 6개를 사용했다.
- 손익관리 빈 메뉴는 사용할 수 있는 기능으로 오해할 수 있어 만들지 않았다.

## 4. 실제 변경 파일

| 영역 | 파일과 역할 |
| --- | --- |
| Migration | `database/migrations/0081_g2_operations.sql`, `0082_g2_forecast_expiry.sql` — 독립 G2 schema·permission matrix와 예상값 식별·날짜 도래 초기화 |
| Backend 신규 | `backend/src/Emi.Qms.Api/G2/*` — contracts, 재고 계산, store, endpoint |
| Backend 연동 | authorization/identity 정책 파일, `Program.cs` — permission·policy·DI·route 등록 |
| Backend 검증 | `G2OperationsTests.cs`, `PostgreSqlMigrationTests.cs` — 재고 경계·권한·원자 거부·migration catalog |
| Frontend 신규 | `g2.ts`, `G2Charts.tsx`, `G2DataViews.tsx`, `useG2DateRange.ts`, `useG2Holidays.ts`, `G2HomePage.tsx`, `G2ManagementPages.tsx` — type·그래프·가로표·날짜·공휴일 필터·홈·관리 화면 |
| Frontend 연동 | `App.tsx`, `api.ts`, `styles.css`, `design-system/wireframe.css` — 메뉴·route·API client·responsive style·G2 pastel 예외 |
| Frontend 검증 | `G2Pages.test.tsx`, `G2Navigation.test.tsx`, `g2-operations.full-stack.spec.ts` |
| 종료 산출물 | `docs/00-product-roadmap.md`, 이 보고서와 Task 사용자 검수 screenshot |

Excel/PDF/첨부파일 영향은 `N/A`다. 해당 기능과 저장소를 추가하거나 변경하지 않았다. 기존 프로젝트·생산계획·물류·근태 workflow 영향도 `N/A`이며 G2 테이블과 API는 독립 영역이다.

## 5. 실행한 검증과 결과

| 검증 | 적용 | 결과 | 근거·미실행 이유 |
| --- | --- | --- | --- |
| Backend Release build | 적용 | 통과, warning/error `0/0` | `dotnet build backend/Emi.Qms.sln --configuration Release --nologo` |
| Backend 전체 회귀 | 적용 | 최신 원격 `main` 통합 기준 `549/549` 통과 | disposable PostgreSQL과 전체 API tests |
| G2·migration 영향 검사 | 적용 | `64/64` 통과 | migration 전체 class + G2 tests, fresh DB apply·`0081→0082` forward apply와 두 번 적용 |
| Frontend lint | 적용 | error 0, 기존 Fast Refresh warning 1 | `pnpm --dir frontend lint` |
| Frontend typecheck | 적용 | 통과 | `pnpm --dir frontend typecheck` |
| Frontend 전체 unit | 적용 | 31 files, `230/230` 통과 | `pnpm --dir frontend test` |
| Frontend production build | 적용 | 통과, 기존 대형 chunk warning 유지 | `pnpm --dir frontend build` |
| Isolated Full-Stack | 적용 | `1/1` 통과 | 실제 HTTP/browser, disposable PostgreSQL, 권한·CAS·2200년 미래·desktop/mobile |
| Production migration image | 적용 | fresh/existing 각 1회와 재적용 통과, ledger `82 Exact` | 임시 Production Backend image로 `0081`·`0082` 포함 전체 migration을 두 번 적용한 뒤 image·DB·container·network 정리 |
| Visual QA | 적용 | 통과 | 기존 privacy-safe synthetic [Desktop 1440](g2-operations-001-screenshots/01-g2-home-desktop-1440.png)·[Mobile 390](g2-operations-001-screenshots/02-g2-operations-mobile-390.png)와 Change 011~020 비식별 projection. Change 015 latest desktop은 SVG `720×340` 2개, scale note·axis break `0/0`, 생산·납품 수량 label 겹침 `0`, color filter 해제, 생산표 납품행 `1`, page overflow `0`을 확인했다. Mobile layout unit은 좌우 축·frame 고정 layer와 내부 scroll layer를 분리하고 31일 data width `620%`, 첫 plot viewport 날짜 `5`개와 확대된 모바일 수치 글꼴을 확인했다. Change 016 live 화면은 두 관리표 `구분` row `0`, 미래 `예상` header 유지를 확인했다. Change 017 live 화면은 graph/KPI desktop 2열과 KPI 6개를 확인했다. Change 018은 막대 위 합계 label 제거, 통합 hit area, 순수 파랑 평균선·하위 draw order, KPI 강조선 제거·조별 gradient, 오늘 기준 부족분 안내와 page overflow `0`을 확인했다. Change 019는 재고 부족분 안내가 `208×74` 카드 안쪽의 `194×60` 불투명 overlay이며 네 방향 포함·page overflow `0`임을 local Desktop에서 확인했다. Change 020은 빈 목표·실사 저장 비활성, `0` 입력 활성과 page overflow `0`을 local Desktop에서 확인하고, 서울 날짜·월별 단일 조회·최신 응답 우선은 unit·Full-Stack fixed projection으로 검증했다. 사용자 제공 숫자가 보이는 임시 screenshot은 육안 확인 직후 저장소 밖 임시 경로에서 제거했다. |
| CI | 실행 대기 | `PENDING` | 게시·merge 승인 완료, Ready PR 생성 뒤 최신 head 필수 CI 확인 |
| Persistent UAT | 미실행 | `N/A` | 명시적 제외·미승인, 운영 DB/runtime 미변경 |
| Azure 공개 URL | 미실행 | `N/A` | 공개배포 미승인 |
| Local 사용자 검수 runtime | 적용 | 실행 중 | Frontend `42983`, Backend `41166`, 전용 PostgreSQL `emi-qms-g2-validation` |

Full-Stack은 같은 metric 동시 요청 `1 success / 1 conflict`, 서로 다른 metric 동시 저장 `2 success`, 제조의 생산+납품 mixed request 전체 `403`, 물류의 생산 `403`·납품 성공, 미래 실사 `400`, 미래 예상·목표 적용·재고 계산을 실제 DB에서 검증했다.

최종 `origin/main` 갱신은 Product Roadmap과 생산관리 Task 문서만 변경했으며 runtime·migration·test source 변경은 없었다. G2 branch를 SHA `4a220d446b1fb71604c4289f1cf7d85eec41712d`에 다시 맞추고 Roadmap에 양쪽 상태를 보존했으며, 앞서 완료한 코드 검증의 실행 기준에는 변화가 없음을 확인했다.

## 6. 시행착오와 Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `G2-PLAN-REV-001` | P2 | `RESOLVED` | 빈 값과 실제 0을 혼동하면 실적 의미가 바뀜 | nullable 원본 + 재고 계산에서만 0, API/UI/E2E 검증 |
| `G2-PLAN-REV-002` | P2 | `RESOLVED` | 오전·오후 동시 입력 유실 가능성 | metric별 row/version/advisory lock + 경쟁 E2E |
| `G2-PLAN-REV-003` | P2 | `RESOLVED` | mixed permission 일부 저장 위험 | 모든 field 선검사 + 단일 transaction + 전체 403 |
| `G2-PLAN-REV-004` | P2 | `RESOLVED` | 그래프 marker만으로 실사 편집 시 접근성 저하 | 동등한 날짜 button, focus dialog, Escape, 대체 표 |
| `G2-PLAN-REV-006` | P2 | `RESOLVED` | 기획 기준 branch와 최신 main의 차이로 migration·권한 계약이 달라질 위험 | 최신 `origin/main`에 재기준선화하고 `0081` 번호·권한 seed·navigation을 다시 대조 |
| `G2-PRIVACY-EVIDENCE-001` | P2 | `RESOLVED` | 최초 공개 root 확인의 transient 출력이 허용 projection보다 상세했음 | 원문을 증빙에서 폐기하고 boolean·status·SHA fixed projection으로 Gate를 처음부터 재실행; Repository artifact와 운영 mutation 없음 |
| `G2-FUTURE-DATE-CAP` | P2 | `RESOLVED` | 초기 구현의 2100년 상한이 미래 제한 없음 계약과 충돌 | 상한 제거, DateOnly 범위까지 허용, 2200년 E2E |
| `G2-GRAPH-DUAL-AXIS` | P2 | `RESOLVED` | 생산과 재고 규모 차이에서 단일 축이면 작은 값 판독이 어려움 | 막대 왼쪽 축·재고 오른쪽 축과 `대` 단위 추가 |
| `G2-INPUT-HYDRATION-RACE` | P2 | `RESOLVED` | 월 자료가 처음 표시된 직후 즉시 입력하면 늦은 기준값 동기화가 사용자 입력을 되돌릴 수 있음 | 입력 기준값을 paint 전 layout effect로 동기화, 고부하 전체 Frontend 재실행 통과 |
| `G2-PLAN-REV-005` | P3 | `RESOLVED` | 날짜가 지나도 예상값 미정정 여부를 별도 상태로 알 수 없어 예상 숫자가 실적으로 오인될 수 있음 | Change 016에서 저장 당시 미래 수량을 식별하고 서울 날짜 도래 후 첫 G2 요청에서 예상 숫자만 빈 값으로 초기화 |
| `G2-MIGRATION-TEST-ZERO-ROW` | P3 | `RESOLVED` | 사용자 0명인 순수 migration DB에서 `INSERT … SELECT`가 0행이 되어 constraint 검사가 무효 | PostgreSQL catalog constraint 직접 검사로 보정 |
| `G2-E2E-USER-SWITCH-ROUTE` | P3 | `RESOLVED` | 개발 persona 전환 시 기존 앱이 기본 route로 이동해 UI assertion 위치가 달라짐 | 전환 뒤 G2 deep link를 다시 열도록 E2E fixture 보정 |
| `G2-LOCAL-DB-PORT-001` | P3 | `RESOLVED` | 기존 검수 DB container가 host port를 공개하지 않아 local Backend가 연결되지 못함 | 기존 DB·volume 대신 Task 전용 PostgreSQL container를 `127.0.0.1:5432`에 생성해 검수 runtime을 기동 |
| `G2-LOCAL-EVIDENCE-002` | P3 | `RESOLVED` | 최종 다중 HTTP 확인에서 출력 억제가 두 번째 요청에 적용되지 않아 정적 Frontend HTML이 transient 출력됨 | 실제 사용자·업무 data·credential이 없음을 확인하고 원문을 증빙에서 폐기한 뒤 status-only projection으로 재실행 |
| `G2-CHART-SR-TABLE-OVERFLOW` | P2 | `RESOLVED` | 화면 읽기용 표에 `sr-only`를 직접 적용하면 table intrinsic width가 남아 그래프와 페이지에 가로 스크롤이 생김 | 표를 1px clipped wrapper 안에 두고 desktop 1440·mobile 390에서 그래프 `scrollWidth = clientWidth`, page overflow 0 재검증 |
| `G2-DATE-FILTER-RESET-RACE` | P2 | `RESOLVED` | 날짜 filter hook의 초기 effect가 사용자의 첫 범위 변경 직후 실행되면 전체 기간으로 되돌릴 수 있음 | reset key가 실제로 바뀔 때만 범위를 초기화하도록 제한하고 G2 filter 회귀·전체 Frontend를 재실행 |
| `G2-ATTENDANCE-DENSITY-001` | P2 | `RESOLVED` | 출근표가 EMI·도급·합계·하루 총원을 항상 모두 보여 합계를 빠르게 비교하기 어려움 | 오전·오후 합계 2개 행을 기본으로 하고 접근 가능한 독립 disclosure로 각 조 세부 행을 제공 |
| `G2-GRAPH-VISUAL-HIERARCHY-001` | P2 | `RESOLVED` | 단색 막대와 직선 중심 그래프가 계열과 실적·예상 구간의 시각적 위계를 충분히 만들지 못함 | 색상 gradient·둥근 막대·plot 배경·예상 label·재고 point·chip 범례를 적용하고 desktop/mobile 재검수 |
| `G2-GRAPH-GRAYSCALE-CASCADE-001` | P2 | `RESOLVED` | 공통 Graphite selector가 `chart` class를 가진 모든 ancestor에 grayscale filter를 적용해 지정한 pastel 색상이 실제 화면에서 회색으로 보임 | G2 chart card·wrapper·legend·SVG를 승인된 color exception으로 분리하고 desktop/mobile screenshot에서 실제 색상 재검수 |
| `G2-KPI-TOOLTIP-CASCADE-001` | P2 | `RESOLVED` | 재고 부족분 `i` 안내를 바깥쪽에 띄우면 그래프를 가리고, 공통 Graphite inline·monochrome normalization이 안내의 위치 기준과 불투명 배경을 제거함 | 안내를 KPI 카드 내부 overlay로 전환하고 label·tooltip을 G2 semantic exception에 포함한 뒤 카드 내부 포함·불투명 색상·page overflow 0을 live 검증 |
| `G2-STACK-SEAM-001` | P2 | `RESOLVED` | 오전·오후 누적 segment가 각자 둥근 모서리와 stroke를 가져 접점이 분리된 두 막대처럼 보임 | 두 segment를 하나의 rounded clip에 넣고 접점 stroke를 제거한 뒤 전체 outer outline으로 grouping 강화 |
| `G2-ATTENDANCE-BUTTON-CHROME-001` | P2 | `RESOLVED` | 출근 합계 cell의 border·background와 boxed `+`가 표 안의 별도 button 군으로 보여 표 자체를 누르는 인상을 약화함 | button semantics는 유지하면서 chrome을 제거하고 cell 전체 hit area·row hover·focus outline으로 전환 |
| `G2-AXIS-CONTRACT-001` | P2 | `RESOLVED` | data별 nice scale이 달마다 축 범위를 바꿔 사용자가 지정한 고정 비교 기준과 충돌 | 생산·납품·재고 공통 `0~180` 2단계 fixed scale과 조별 `0~60/10` fixed scale, 범위 밖 clamp 적용 |
| `G2-BAR-BASELINE-FLOAT-001` | P2 | `RESOLVED` | 막대 아래쪽까지 둥근 clip을 사용해 `0` axis와 시각적 틈이 생기고 바닥에서 떠 보임 | 위만 둥근 path·평평한 바닥과 굵은 공통 baseline으로 production·delivery·stack 모두 연결 |
| `G2-CHART-HOVER-DETAIL-001` | P2 | `RESOLVED` | 기존 SVG title은 막대·일부 점에만 있고 선 구간에서 날짜별 값을 즉시 확인하기 어려움 | 막대별 tooltip과 선별 넓은 hit path·pointer 위치 날짜 계산·plot 경계 보정을 추가하고 desktop/mobile에서 실제 hover 검증 |
| `G2-GRAPH-PERSISTENT-VALUE-001` | P2 | `RESOLVED` | 긴 월간 기간에는 주요 값 label을 숨겨 pointer 없이 날짜별 생산·납품·재고·조별 값을 바로 비교하기 어려움 | graph 폭 확대, 전 날짜 재고 point·label, 모든 생산·납품 상단 label과 조별 segment 내부 label을 적용하고 1440/390에서 검증 |
| `G2-TABLE-ACCENT-CASCADE-001` | P2 | `RESOLVED` | 공통 Graphite `color: … !important`가 생산표 재고행의 승인된 pastel red를 실제 화면에서 검정으로 덮음 | wireframe 마지막 cascade에 G2 재고·전체합계 semantic accent 예외를 추가하고 computed color를 재검증 |
| `G2-GRAPH-NUMERIC-DENSITY-001` | P2 | `RESOLVED` | 월간 전체 수치와 날짜를 동시에 표시할 때 기존 글자 크기와 날짜 생략이 비교 밀도를 떨어뜨림 | graph 숫자를 축소하고 두 graph 모두 선택 기간 전체 날짜를 표시하며 재고축을 `0~180/20`으로 고정해 1440/390에서 검증 |
| `G2-INVENTORY-ROW-ACCENT-001` | P2 | `RESOLVED` | 생산표 재고의 빨간 글씨 강조가 출근 전체 합계행의 행 단위 강조와 시각 언어가 달랐음 | 기본 짙은 글씨와 pastel red 배경·2px 상단선으로 바꾸고 음수 위험 색상만 유지 |
| `G2-CHANGE-003-TEST-ASSERTION-001` | P3 | `RESOLVED` | 가독성 개선으로 축의 최대 tick과 예상 표시 개수가 바뀌어 기존 문구 assertion이 실패 | nice-axis 기대값을 갱신하고 가로표·공용 날짜 필터 회귀 검사를 추가한 뒤 전체 `223/223` 통과 |
| `G2-CALENDAR-RED-DAY-001` | P2 | `RESOLVED` | G2 graph와 표가 주말·공휴일을 공통 규칙으로 구분하지 않아 휴무일 생산 흐름을 빠르게 읽기 어려움 | UTC 주말 판정과 기존 활성 한국 공휴일 API를 결합한 shared helper를 적용하고 2026년 8월 graph·표를 검증 |
| `G2-TABLE-HOLIDAY-CASCADE-001` | P2 | `RESOLVED` | 공통 wireframe의 고특이도 table header 규칙이 G2 휴일의 빨간 글씨·배경을 실제 화면에서 덮음 | 공통 selector에서 `g2-red-day`를 semantic exception으로 제외하고 computed `#dc2626/#fff1f2`를 재검증 |
| `G2-GRAPH-X-LABEL-DENSITY-001` | P2 | `RESOLVED` | 월 31개 날짜 label이 graph와 멀고 상대적으로 커 plot과 날짜의 대응이 약해 보임 | desktop `7px`·mobile `8px`, baseline 간격 `18`로 조정하고 1440/390에서 전체 날짜·overflow를 재검증 |
| `G2-FLOW-SERIES-OVERLAP-001` | P2 | `RESOLVED` | 하나의 plot에서 생산·납품 막대와 재고·목표선이 교차해 계열과 상시 수량을 동시에 읽기 어려움 | 선형 좌우 축과 단일 plot을 유지하면서 막대 폭·opacity를 줄이고 실제·예상·목표선 white halo와 재고 label collision 보정을 적용 |
| `G2-HOLIDAY-COLUMN-STYLE-001` | P2 | `RESOLVED` | 휴일 header 배경 채우기만으로는 같은 날짜의 세로 값 연결이 약하고 표 면적이 과하게 강조됨 | 배경을 흰색으로 복원하고 모든 G2 가로표 휴일 열의 header·body·합계 interaction·예상 글자를 red로 통일 |
| `G2-DUAL-SCALE-ORDERING-001` | P2 | `RESOLVED` | 독립 `0~80`·`0~180` 축에서 생산 50이 재고 90보다 높게 보여 절대 수량의 크기 순서가 뒤집힘 | 모든 flow 계열에 동일한 `0~60=70%`, `60~180=30%` 단조 증가 scale·공통 tick·break 안내를 적용 |
| `G2-HOLIDAY-HEADER-BORDER-001` | P2 | `RESOLVED` | 휴일 header를 공통 wireframe selector에서 제외하면서 아래쪽 dark border도 함께 빠져 날짜별 header 선이 끊김 | G2 휴일 header semantic rule에 평일과 동일한 `1px solid` bottom border를 명시하고 desktop/mobile computed style 재검증 |
| `G2-ATTENDANCE-MANAGEMENT-DISCLOSURE-001` | P2 | `RESOLVED` | 제조 인원 출근 관리 월간표는 EMI·도급을 항상 표시해 홈의 합계 우선 disclosure와 동작이 달랐고 월간 비교 밀도가 낮음 | 관리 월간표도 오전·오후 합계 기본 표시와 header·날짜별 숫자 양쪽의 독립 disclosure를 적용하고 unit·desktop/mobile live 검증 |
| `G2-SCALE-ANNOTATION-CLUTTER-001` | P2 | `RESOLVED` | 공통 scale의 압축 안내와 `//` marker가 graph 판독보다 시각적 잡음을 늘림 | 단조 scale과 tick은 유지하고 visible note·marker만 제거 |
| `G2-FLOW-LABEL-COLLISION-001` | P2 | `RESOLVED` | 생산·납품 막대 수량이 가까운 값과 인접 날짜에서 겹쳐 읽기 어려움 | Desktop 글꼴 축소와 같은 날짜 근접 값 세로 분리, live overlap `0`; Mobile은 5일 window의 넓은 막대와 확대 글꼴 사용 |
| `G2-PHYSICAL-ZERO-TOOLTIP-001` | P2 | `RESOLVED` | 실사 point보다 선 hit layer가 위에 있어 blue point hover가 일반 재고로 보이고 0 실사 의미가 약해질 수 있음 | line hit 뒤에 넓은 physical hit circle을 배치하고 `physicalCount.quantity`로 `실사 0대` 표시 |
| `G2-MOBILE-CHART-FRAME-001` | P2 | `RESOLVED` | 전체 SVG를 넓히면 축·frame까지 이동·확대되어 좌우 수량 기준을 한 화면에서 볼 수 없음 | Mobile fixed frame·좌우 axis·grid와 내부 날짜 scroll layer를 분리하고 첫 화면 5일·넓은 막대·큰 숫자를 적용 |
| `G2-MOBILE-CHART-COLOR-CASCADE-001` | P2 | `RESOLVED` | 새 chart ancestor가 공통 monochrome filter 대상이 되어 pastel series가 회색으로 보임 | stage·scroll·fixed frame·content SVG 전 layer를 G2 color exception에 포함하고 blue/orange·filter 해제를 확인 |
| `G2-HOME-DELIVERY-ROW-001` | P2 | `RESOLVED` | 홈 생산 현황표에 graph의 납품 수량이 없어 표만으로 생산·납품·재고를 함께 비교할 수 없음 | 날짜별 `납품` row를 추가하고 공용 filter·휴일 열 styling을 재사용 |
| `G2-MANAGEMENT-KIND-ROW-001` | P2 | `RESOLVED` | 생산/출하와 제조 출근 월간표의 날짜 header가 이미 미래 `예상`을 표시하는데 별도 구분행이 같은 정보를 반복함 | 두 관리표의 구분행을 제거하고 날짜 header 예상 표시와 입력 form 예상 안내를 유지 |
| `G2-FORECAST-EXPIRY-IDENTITY-001` | P2 | `RESOLVED` | 날짜만으로 예상 숫자를 삭제하면 당일 실제값까지 지울 수 있고 기존 schema에는 저장 당시 예상 여부가 없음 | `0082`의 `is_forecast` marker, 기존 미래값 backfill과 조건부 원자 초기화로 예상값만 비우고 당일 실제값·실제 0·CAS 보존 |
| `G2-HOME-KPI-CONTEXT-001` | P2 | `RESOLVED` | 그래프만으로 선택 기간 평균과 최신 재고 부족 규모를 즉시 계산해야 하고 조별 일일 합계를 읽기 어려움 | 선택 범위 평균선·graph별 KPI panel과 날짜별 통합 hover, 부족분 공식 hover·focus를 추가 |
| `G2-SHIFT-HOVER-KPI-POLISH-001` | P2 | `RESOLVED` | 막대 위 합계와 평균선이 segment 수치를 가리고 조별 segment hover가 일일 비교를 분리하며, 부족분의 미래 기준·아래쪽 안내가 오해와 clipping을 만듦 | 막대 위 합계 제거, 날짜별 단일 3줄 hover, 파랑 평균선의 하위 draw layer, 오늘 기준 부족분과 위쪽 안내, KPI 강조선·조별 gradient 보정 |
| `G2-REQUIRED-QUANTITY-ZERO-001` | P1 | `RESOLVED` | 홈 필수 실사·목표 수량의 빈 문자열이 JavaScript 숫자 변환에서 `0`이 되어, 특히 재고 checkpoint를 의도하지 않은 0으로 저장할 수 있음 | 공백 명시 거부·저장 button 비활성·실제 `0` 활성과 unit·Full-Stack 회귀를 추가 |
| `G2-CLIENT-SEOUL-DATE-001` | P2 | `RESOLVED` | Backend는 서울 날짜를 쓰지만 Frontend 초기 날짜·월은 기기 현지 날짜를 사용해 다른 timezone·월 경계에서 조회·입력일이 달라질 수 있음 | `Asia/Seoul` 공용 날짜 함수로 홈·목표·두 관리 화면 초기값을 통일하고 UTC→서울 월 경계 회귀와 Full-Stack 오늘 일치를 검증 |
| `G2-MONTH-LOAD-SEQUENCE-001` | P2 | `RESOLVED` | 월 이동의 직접 조회와 effect 조회가 중복되고 늦은 이전 응답이 최신 월 자료를 덮을 수 있으며 같은 달 날짜 변경도 월 전체를 다시 읽음 | 월별 effect 단일 조회·request sequence 최신 응답 우선·같은 달 자료 재사용·저장 중 탐색 차단과 요청 횟수·역전 회귀를 추가 |
| `G2-PUBLICATION-DB-ENV-001` | P3 | `RESOLVED` | 게시 전 첫 Backend 전체 실행이 local 검수 DB 환경을 상속해 인증 단계에서 실패했으며 제품 assertion을 실행하지 못함 | 이 실행을 성공 증빙에서 제외하고 Repository의 격리 스크립트로 새 disposable PostgreSQL을 만든 뒤 동일 source 전체 `549/549`와 cleanup을 확인 |
| `G2-ADMIN-HISTORY-001` | P3 | `DEFERRED_NEW_FEATURE` | 현재 schema는 최신 수정자·시각만 보존하며 관리자용 before/after 이력은 별도 제품 능력임 | 사용자가 Change 020에서 명시적으로 제외. append-only 정책·권한·조회 UX를 별도 `NEW_FEATURE`로 기획할 때 재개 |

현재 G2 승인 범위의 Open P0/P1/P2/P3는 `0/0/0/0`이다. 관리자 입력·수정 이력은 사용자 명시 제외에 따른 별도 `DEFERRED_NEW_FEATURE`이며 이번 게시 범위 Finding으로 계산하지 않는다.

## 7. 개인정보·secret과 artifact 검토

테스트와 기존 tracked screenshot은 disposable DB, 고정 dev persona와 synthetic 숫자만 사용했다. Change 011 local runtime에는 사용자가 제공한 workbook 숫자를 입력했지만 숫자가 보이는 임시 screenshot은 육안 확인 직후 제거했고, 문서에는 합계 보존 여부와 화면 구조 검증 projection만 기록했다. 실제 사용자·회사 계정·고객·프로젝트 원문, credential, connection string, token, 실제 운영 API/DB body를 문서나 화면 증빙에 기록하지 않았다.

`.env`, 인증서, lockfile, 실제 provider 설정은 변경하지 않았다. Playwright `test-results`, video, trace와 build `dist`는 Git 변경에 포함되지 않는다. Task screenshot은 이름·계정·원문 없이 숫자 화면만 담은 사용자 검수 자료다.

## 8. SOP

### 일일 운영 입력

1. 생산 담당자는 `G2 → 생산/출하 관리`에서 날짜를 고르고 자기 조 생산량만 또는 오전·오후를 함께 입력한다.
2. 영업·물류 담당자는 같은 화면에서 하루 전체 납품량을 입력한다.
3. 제조·영업 담당자는 `G2 → 제조 인원 출근 관리`에서 오전·오후 EMI·도급 인원을 입력한다.
4. 미래 날짜를 입력하면 `예상` 안내를 확인한다. 날짜가 도래하면 미리 입력한 예상 숫자는 자동으로 빈 값이 되므로 당일 실제 값을 새로 입력한다.
5. `다른 사용자가 먼저 변경` 메시지는 덮어쓰지 말고 최신 값을 다시 확인한 뒤 재입력한다.

### 목표와 실사

1. `G2 홈`에서 조회할 달을 선택한다.
2. 목표 종류·적용 시작일·수량을 저장한다. 새 정책은 새 적용일에 저장하고, 같은 적용일 오입력만 현재 row를 정정한다.
3. 실사일에는 그래프 아래 `실사 입력` 또는 기존 `n일 실사`를 열어 실제 확인 수량을 기록한다.
4. 미래 실사는 입력하지 않는다. 실사 수정·삭제 전에는 마지막 수정 정보를 확인한다.
5. 음수 재고 경고가 나오면 생산·납품 누락과 실사값을 확인하며 DB에서 계산값을 직접 수정하지 않는다.

### 장애·복구와 운영 적용 전 절차

- `403`: 담당 부서 권한을 확인한다. 화면 제어를 우회해 API를 재시도하지 않는다.
- `409`: 최신 월 자료를 다시 읽고 해당 metric의 현재 version으로 재시도한다.
- 재고선 없음: 선택한 기간 이전을 포함해 최초 실사를 등록한다.
- migration 장애: 적용된 `0081`·`0082`를 수정하지 않고 다음 additive forward-fix를 작성한다.
- 운영 적용 전에는 최신 `main` migration 번호·권한 matrix 재대조, fresh/existing apply, 전체 CI, Persistent UAT snapshot과 runtime handover 승인을 별도로 받는다.
- 배포 순서는 migration → Backend → Frontend이며 각 단계 ready 확인과 exact source SHA 고정이 필요하다. 이번 Task에서는 실행하지 않았다.

## 9. User manual

### G2 홈

- `홈 표시 기간`의 시작일·종료일을 바꾸면 두 그래프, 생산표와 제조 인원 출근표가 같은 기간으로 함께 좁혀진다. `전체 기간`으로 해당 월 전체를 복원한다.
- `생산 · 납품 · 재고`: 파란 생산·주황 납품 막대와 빨간 재고·파란 재고목표 선은 같은 `0~180` 2단계 축을 사용하며 숫자가 큰 항목은 항상 더 높게 표시된다. 압축 안내 문구와 `//` marker는 표시하지 않는다. 생산·납품 수량은 막대 위, 모든 날짜의 재고는 작은 빨간 채움 point·수량으로 표시하고 실사 날짜 point·수량만 파란색이다. 파란 point에 마우스를 올리면 0을 포함한 실사 원본값을 확인한다.
- 첫 그래프 오른쪽 KPI에서 선택 기간의 일일 생산·납품·재고 평균과 서울 기준 오늘 날짜의 재고 부족분을 확인한다. 부족분 옆 `i`에 마우스를 올리거나 keyboard focus하면 해당 KPI 카드 안에서 오늘 기준 `재고목표 - 재고` 계산식을 확인할 수 있다.
- 토요일·일요일·등록된 한국 공휴일은 graph 날짜와 모든 G2 표의 해당 날짜 열 전체 글자가 빨간색이다. 표 배경은 평일과 같고, 공휴일 이름은 표 날짜에 pointer를 올리면 확인할 수 있으며 미래 날짜만 `예상`으로 표시한다.
- `오전조 · 오후조 생산량`: 오전·오후 구간이 `0~60` 축의 하나의 연결된 일 생산 막대 안에 쌓이고 각 segment 안의 숫자로 조별 생산량을 확인한다. 막대 위 합계 숫자는 표시하지 않으며 막대 어느 위치에 올려도 오전·오후·전체가 한 안내 상자에 나온다. 파랑 점선은 선택 기간 총 생산 평균, 빨간 점선은 일 생산목표이며 오른쪽 KPI는 막대와 같은 파랑 계열로 오전·오후조 평균을 표시한다.
- 그래프 막대나 선에 마우스를 올리면 해당 날짜·항목·수량을 간단한 안내 상자에서 확인한다.
- `적용 시작일별 목표`: 생산 현황 바로 위에서 일 생산목표·재고목표를 관리한다.
- `생산 현황`: 날짜별 오전·오후 생산, 생산 합계, 납품과 pastel red 행 배경으로 강조한 자동 재고를 가로로 비교한다.
- 모바일 graph는 왼쪽·오른쪽 숫자 축과 graph frame을 한 화면에 고정한다. 가운데 날짜 영역에는 5일이 보이며 내부를 좌우로 drag해 나머지 날짜를 확인한다.
- `제조 인원 출근 현황`: 기본으로 오전 합계·오후 합계를 보고, 별도 button 모양 없이 왼쪽 합계 제목이나 날짜별 합계 숫자 cell을 누르면 해당 조의 EMI·도급 인원을 펼쳐 본다. 마지막 `오전·오후 전체 합계`는 두 조를 단순 합산하므로 같은 사람이 양쪽 조에 근무해도 숫자 입력 방식상 두 번 합산된다.

### 생산/출하 관리

- 빈 칸은 미입력이고 `0`은 실제 0대다.
- 오전 또는 오후 한 칸만 바꾸면 나머지 값은 유지된다.
- 제조는 생산만, 물류는 납품만, 영업과 관리자는 허용된 전체 field를 수정한다.
- 마지막 수정자·시각은 각 입력 아래에서 확인한다.
- 미래에 입력한 예상 숫자는 서울 날짜가 도래한 뒤 첫 G2 조회에서 빈 값으로 초기화되며, 당일 새로 입력한 실제값과 실제 `0`은 유지된다.
- `입력 현황 표시 기간`으로 `구분` 행이 없는 아래 가로형 월간 표의 날짜 열을 좁혀 본다. 미래 날짜는 header에만 `예상`으로 표시된다.

### 제조 인원 출근 관리

- 오전 EMI, 오전 도급, 오후 EMI, 오후 도급을 입력한다.
- 오전 합계·오후 합계·하루 총원은 입력 중 즉시 계산된다.
- 미래 날짜는 예상으로 표시되고 날짜 도래 시 미리 입력한 예상 인원이 빈 값으로 초기화되므로 실제 인원을 새로 입력한다.
- 월간표는 오전·오후 합계와 하루 총원을 먼저 보여준다. 왼쪽 합계 제목이나 날짜별 합계 숫자를 누르면 해당 조의 EMI·도급 행만 펼치고 다시 누르면 접는다.
- `출근 현황 표시 기간`으로 `구분` 행이 없는 아래 가로형 월간 표의 날짜 열을 좁혀 본다. 미래 날짜는 header에만 `예상`으로 표시된다.

### 목표·실사 관리

- 목표는 홈에서 보고 있는 달 안의 적용 시작일을 고른다. 과거·미래 달은 이전·다음 달 버튼으로 이동한다.
- 실사·목표 수량은 필수다. 빈 상태에서는 저장할 수 없고 실제 수량 `0`을 입력하면 저장할 수 있다.
- 실사는 오늘 또는 과거만 가능하다. 과거 달 실사는 해당 달로 이동해 입력한다.
- 실사값은 자동 재고의 확인 기준점이며, 그 이전 정정이 다음 실사 이후 재고를 바꾸지 않는다.

## 10. User validation checklist

상태: `사용자 검수 대기`

2026-08-19 사용자의 요청으로 [Change 002](g2-operations-001-change-002.md)의 local 검수 서버를 실행했다. 사용자가 제공한 G2 workbook의 2026년 8월 생산·납품 숫자를 반영하고 기존 검수 출근·재고목표를 유지했으며, 일 생산목표는 전 날짜 `50대`, 공식 광복절·대체공휴일을 local calendar에 입력했다. Frontend `http://127.0.0.1:42983/g2`를 사용자 검수 화면으로 열어 두었다.

### 자동 검증 완료

- [x] G2 부모 아래 홈·생산/출하·제조 인원 출근 세 메뉴만 표시
- [x] 모든 활성 역할 조회와 제조·영업·물류·관리자 field 권한
- [x] 오전/오후 개별 저장, 일괄 저장, 0 저장, 빈 값 보존
- [x] 같은 metric 409와 다른 metric 병렬 보존
- [x] 미래 예상과 2200년 입력, 미래 실사 차단
- [x] 실사 checkpoint, 다음 실사 경계, 음수 재고
- [x] 목표 적용 시작일과 두 그래프의 축·단위·대체 표
- [x] 모든 보이는 G2 월간 표를 날짜 열·항목 행 가로형으로 통일
- [x] 홈 공용 날짜 필터와 관리 화면별 표 날짜 필터
- [x] 그래프 축·색상·눈금·예상 영역·짧은 기간 값 표시와 그래프 가로 스크롤 제거
- [x] 홈 생산표와 오전·오후·합계·재고 행
- [x] 출근표 기본 오전·오후 합계와 조별 EMI·도급 독립 펼침·접힘
- [x] 그래프 gradient·둥근 막대·plot 배경·예상 label·재고 point·chip 범례
- [x] 사용자 지정 pastel 계열 mapping과 Graphite grayscale 예외
- [x] 출근표 왼쪽 header와 날짜별 합계 숫자 양쪽 disclosure
- [x] 조별 생산 오전·오후 segment의 seamless clip·outer outline
- [x] 출근 합계 cell의 persistent button chrome 제거와 full-cell hit area
- [x] 생산·납품·재고 공통 `0~180` 2단계 축과 조별 `0~60/10` fixed axis
- [x] 생산 파랑·납품 주황 mapping과 모든 막대의 flat bottom·강한 baseline
- [x] 막대·선 날짜별 hover tooltip과 한 단계 진한 가로 grid line
- [x] 확대 graph와 전 날짜 재고 point·label, 생산·납품 상단 label, 조별 segment 내부 label
- [x] 실사 날짜의 pastel blue point·수량과 graph cursor 모양 유지
- [x] 목표 관리→생산 현황 순서, pastel red 재고행, 출근 오전·오후 전체 합계행
- [x] 모든 graph 전체 날짜 label·축소 수치·작은 채움 재고 point와 생산표 재고행 pastel red 배경·상단선
- [x] graph 날짜 label 축소·baseline 간격 축소와 주말·공휴일 graph/표 pastel red 표시
- [x] 표 날짜 header의 과거·오늘 `실적` 제거·미래 `예상` 유지와 8월 일 생산목표 전 날짜 `50대`
- [x] 단일 graph·선형 축을 유지한 생산·납품 막대 축소·opacity와 재고·목표선 white halo
- [x] 휴일 표 header 배경 제거와 해당 날짜 열 header·값·합계 interaction 전체 red text
- [x] `0~60=70%`, `60~180=30%` 공통 단조 scale·좌우 tick 일치·break marker와 안내
- [x] 휴일 header의 평일 동일 `1px` dark bottom border 복구
- [x] 제조 인원 출근 관리 월간표의 오전·오후 합계 기본 표시와 EMI·도급 독립 펼침·접힘
- [x] desktop 1440과 mobile 390, page overflow 0·표 내부 스크롤만 유지
- [x] 두 관리표의 중복 `구분` 행 제거와 미래 날짜 header `예상` 유지
- [x] `0082` 예상 marker backfill·날짜 도래 자동 초기화·예상 `0` 삭제·당일 실제값 보존
- [x] 조별 생산 날짜별 통합 hover·총 생산 평균선과 평균 hover 안내
- [x] 두 graph 우측 KPI 6개·재고 부족분 공식 안내·날짜 filter 재계산·좁은 화면 2열 재배치
- [x] 조별 막대 위 합계 제거·날짜별 단일 3줄 hover·평균선 하위 layer와 순수 파랑 색상
- [x] KPI 왼쪽 강조선 제거·조별 막대 동일 gradient·오늘 기준 부족분·안내 위치 상향
- [x] 재고 부족분 `i` 안내를 KPI 카드 내부 불투명 overlay로 제한하고 graph·다른 KPI 겹침 제거
- [x] 빈 실사·목표 저장 차단과 실제 `0` 허용
- [x] 홈·목표·관리 화면 초기 날짜의 서울 기준 통일
- [x] 월 이동 단일 조회·최신 응답 우선과 같은 달 자료 재사용
- [x] 최신 원격 `main` 통합 기준 Backend 전체 549, Frontend 전체 230, isolated Full-Stack과 Production migration image 통과

### 사용자 직접 확인 대기

- [ ] 왼쪽 G2 하위 메뉴 3개 명칭과 이동이 자연스러운지 확인
- [ ] 오전 담당자와 오후 담당자가 자기 생산량만 저장하기 편한지 확인
- [ ] 물류는 납품만, 제조는 생산·출근만 수정되는지 역할별 확인
- [ ] 홈 첫 그래프의 생산·납품·재고·재고목표와 좌우 축이 한눈에 이해되는지 확인
- [ ] 조별 생산 그래프의 오전·오후 누적과 일 생산목표가 이해되는지 확인
- [ ] 조별 통합 hover와 파랑 총 생산 평균선이 빨간 목표선과 자연스럽게 구분되는지 확인
- [ ] 두 그래프 오른쪽 KPI의 평균·재고 부족분이 한눈에 읽히고 `i` 계산 안내가 충분한지 확인
- [ ] 조별 막대 hover의 오전·오후·전체 3줄과 파랑 평균선이 segment 숫자를 가리지 않는지 확인
- [ ] 재고 부족분이 오늘 기준으로 이해되고 `i` 안내가 해당 KPI 카드 안에서 온전히 보이는지 확인
- [ ] 홈 날짜 범위를 바꿀 때 두 그래프·생산표·출근표가 함께 바뀌는지 확인
- [ ] 생산표의 오전·오후·합계·재고 순서가 보기 편한지 확인
- [ ] 출근표 왼쪽 제목과 합계 숫자 양쪽에서 EMI·도급 펼침이 자연스러운지 확인
- [ ] 제조 인원 출근 관리 화면의 월간표에서도 오전·오후 EMI·도급 펼침이 자연스러운지 확인
- [ ] 조별 생산 막대가 날짜마다 하나의 연결된 막대로 읽히는지 확인
- [ ] 출근 합계 행이 별도 button 군이 아니라 클릭 가능한 표 자체로 느껴지는지 확인
- [ ] 생산·납품·재고와 조별 graph의 고정 축 간격이 업무 비교에 맞는지 확인
- [ ] 모든 막대 바닥이 `0` 기준선에 안정적으로 붙어 보이는지 확인
- [ ] 막대·선에 마우스를 올렸을 때 날짜·항목·수량 안내가 빠르게 읽히고 가로선 대비가 적절한지 확인
- [ ] 넓어진 graph와 상시 수치 label이 한 달 데이터를 비교하기 편한지 확인
- [ ] 모든 날짜·축소 수치와 작은 채움 재고 point가 한 달 비교에 적절하고 실사 날짜의 파란 point·숫자가 자연스럽게 구분되는지 확인
- [ ] graph 날짜가 baseline 가까이 읽히고 주말·공휴일 빨간색이 graph와 표에서 자연스럽게 구분되는지 확인
- [ ] 막대와 재고·목표선 교차 구간이 white halo로 충분히 분리되고 막대 폭·투명도가 적절한지 확인
- [ ] 휴일 표가 배경 채우기 없이 해당 날짜 열 전체 red text로 자연스럽게 이어지는지 확인
- [ ] 공통 2단계 축에서 큰 숫자가 항상 더 높게 보이고 60 break 안내가 이해되는지 확인
- [ ] 휴일 날짜 아래 header border가 평일과 같은 선으로 자연스럽게 이어지는지 확인
- [ ] 생산표 재고행의 pastel red 배경·상단선이 출근 전체 합계행과 같은 종류의 강조로 느껴지는지 확인
- [ ] 목표 관리가 생산 현황 바로 위에 있고 출근 전체 합계행이 업무 순서에 맞는지 확인
- [ ] 실사 입력·수정·삭제와 `실사` 표시가 실제 업무 표현에 맞는지 확인
- [ ] 세 가로형 표의 날짜 열·항목 행 순서와 각 화면 날짜 필터가 편한지 확인
- [ ] 모바일 390px에서 글씨·표 내부 스크롤·입력 버튼 사용성이 편한지 확인

검수 증빙은 실제 사용자명이나 업무 원문 없이 날짜, 환경, 익명 역할명과 성공/실패만 기록한다.

## 11. Rollback, 남은 위험과 후속 경계

현재 G2 source는 local commit으로 고정됐고 원격 게시·운영 배포를 대기한다. `0081`·`0082` 적용 뒤 애플리케이션 rollback이 필요하면 세 G2 테이블, 예상 marker와 permission을 삭제하지 않고 이전 Backend/Frontend로 되돌려 데이터를 보존한다. schema 제거가 필요하면 별도 데이터 보존·destructive 승인과 forward migration이 필요하다.

남은 항목은 다음과 같다.

- 사용자 직접 검수
- local 검수 종료 뒤 Task 소유 runtime·전용 DB cleanup
- push·PR·필수 CI·merge
- exact `main` SHA 기준 Azure migration·Backend·Frontend 공개배포
- 배포 후 사용자 직접 화면 검수
- 영업팀 전용 손익관리 별도 `NEW_FEATURE`

## 12. 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 완료 | 이 문서 |
| SOP | 작성 완료 | 이 문서 8장 |
| User manual | 작성 완료 | 이 문서 9장 |
| Roadmap update | 자동 검증 완료·게시 및 Azure 승인·사용자 검수 대기로 갱신 | `docs/00-product-roadmap.md` 6.4와 Decision Log |
| User validation checklist | 작성됨·자동 검증 완료·사용자 검수 대기 | 이 문서 10장 |

코드 구현과 게시·Azure 승인은 완료했지만 사용자 검수·Git 게시·Azure 공개배포의 실제 실행 결과는 아직 완료로 처리하지 않는다.
