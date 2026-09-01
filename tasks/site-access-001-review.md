# TASK-SITE-ACCESS-001 — Codex 기획 Review

- reviewStatus: `RESOLVED`
- taskType: `NEW_FEATURE`
- planningSource: [site-access-001-planning.md](site-access-001-planning.md)
- interviewSource: [site-access-001-interview.md](site-access-001-interview.md)
- implementationApproval: `USER_APPROVED`
- openBlockingDecisionCount: `0`

## 결론

Fable 기획의 권장안 A를 유지한다. 사이트 접속은 기존 대화형 로그인 사건과 다른 사용자 능력이므로 별도 세션 원장을 두고, 전체 감사 이력에서 세 원본(Global·Authorization·SiteAccess)을 통합 조회하는 방향이 제품 문제와 Repository 구조에 가장 잘 맞는다.

사용자는 planning 16장의 미결정 사항에 대해 권장안 A, 즉 접속 행의 1회성 명시 로그아웃 종료 필드를 승인했다. 따라서 구현을 차단하는 결정은 없다.

## 기능별 판단

| 분류 | 항목 | 판단 근거 |
| --- | --- | --- |
| 유지 | 사용자+브라우저 client+30분 활동 창의 접속 1행 | 새로고침·탭·페이지 이동을 행마다 쌓지 않으면서 유지 세션 접속을 확인하는 핵심 가치다. |
| 유지 | 마지막 활동과 중복 없는 메뉴의 제한적 갱신 | 사용자가 선택한 한 행 묶음과 메뉴 이력 요구를 충족한다. DB trigger로 허용 필드를 한정해야 한다. |
| 유지 | 명시적 로그아웃 1회 종료 확정 | 대화형 로그인 없이 유지 세션으로 접속한 경우에도 만료와 직접 로그아웃을 구분할 수 있다. |
| 유지 | 기존 감사 화면·Audit.Read.All·선택 Excel 재사용 | 별도 관리자 메뉴와 권한을 늘리지 않고 기존 운영 습관을 보존한다. |
| 추가 | 브라우저 client ID의 탭 간 공유와 동시 최초 생성 수렴 | 사용자만 기준으로 묶으면 다른 기기 접속이 합쳐지고, 탭별 ID면 같은 브라우저가 갈라진다. localStorage 공유 UUID와 Web Locks/수렴 fallback이 필요하다. |
| 추가 | 서버·DB 권위 시각과 정확한 30분 경계 | 클라이언트 시각을 받지 않고 DB 함수가 잠금 획득 뒤 현재 시각을 잡아야 clock 조작과 동시 요청 역행을 막을 수 있다. 정확히 30분은 새 접속으로 판정한다. |
| 추가 | 두 원장의 별도 coverage 시작과 시간 해석 안내 | 기존 감사 시작과 신규 접속 시작은 다르다. 마지막 활동은 페이지 신호일 뿐 실제 근무시간이 아니라는 문구를 화면과 Excel에 표시해야 한다. |
| 추가 | 고정 메뉴 코드의 exhaustive Frontend 매핑·Backend 검증 | URL·query·프로젝트/업무 식별자가 원장에 들어가지 않도록 모든 View를 19개 고정 코드로 축약한다. |
| 추가 | 접속 신호·종료의 bounded best-effort | 기록 서버가 멈춰도 화면 이동과 로그아웃을 차단하지 않아야 한다. timer heartbeat·무한 retry는 두지 않는다. |
| 보류 | Entra sign-in log 연동, 클릭·키 입력·모든 HTTP 수집, 운영 분석 대시보드 | 현재 문제 해결에 필요 없고 개인정보·비용·운영 복잡성을 크게 늘린다. |
| 제거 | 클라이언트가 관측 시각을 요청 body 또는 DB 함수 인자로 전달하는 안 | 시간 경계와 감사 신뢰성을 호출자가 조작할 수 있어 허용하지 않는다. |

## Repository 대조 Finding과 resolution

### SITE-ACCESS-R01 — 별도 원장이 필요함

- severity: `P1`
- status: `RESOLVED`
- 근거: 기존 `audit_events` check는 Login 이외 사건에 IP·browser·OS·app access 값을 금지한다.
- resolution: 기존 migration과 Login/Logout 계약을 수정하지 않고 additive `0084_site_access_sessions.sql`을 사용한다.

### SITE-ACCESS-R02 — 세션 identity와 동시성

- severity: `P1`
- status: `RESOLVED`
- 원인/영향: 사용자만 또는 탭만 기준으로 묶으면 기기별 접속 분리나 같은 브라우저 통합이 깨진다.
- resolution: actor+브라우저 공유 UUID를 key로 사용하고 DB advisory transaction lock으로 단일 active row와 메뉴 누적을 원자화한다.

### SITE-ACCESS-R03 — 마지막 활동의 오해 가능성

- severity: `P1`
- status: `RESOLVED`
- 원인/영향: 페이지 진입 신호 사이 시간은 실제 작업·근무 시간을 증명하지 않는다.
- resolution: 별도 coverage와 “실제 근무시간이 아님” 안내를 관리자 화면·Excel에 고정한다.

### SITE-ACCESS-R04 — 메뉴 원문 유출 위험

- severity: `P1`
- status: `RESOLVED`
- 원인/영향: 주소 전체나 query를 저장하면 업무 식별자·검색어가 감사 원장에 남을 수 있다.
- resolution: 19개 고정 코드만 API로 보내고 Backend와 DB가 각각 allowlist를 검증한다.

### SITE-ACCESS-R05 — 기록 장애가 업무를 막을 위험

- severity: `P1`
- status: `RESOLVED`
- 원인/영향: 대기 중인 신호나 종료 요청을 무제한 기다리면 실제 로그아웃이 멈출 수 있다.
- resolution: 신호와 종료를 짧은 deadline으로 제한하고 실패를 UI 흐름과 분리한다.

### SITE-ACCESS-R06 — 종료 저장 방식

- severity: `P2`
- status: `RESOLVED`
- resolution: 사용자가 권장안 A를 승인했다. 비어 있는 종료 시각을 명시적 로그아웃으로 한 번만 채우며 이후 행은 불변이다.

### SITE-ACCESS-R07 — 승인 대기·비활성 identity

- severity: `P2`
- status: `RESOLVED`
- resolution: 신호 endpoint는 업무 권한이 아닌 기존 `AuthenticatedIdentity`를 사용하고, app access 결과를 actor snapshot과 함께 저장한다. 조회는 계속 `Audit.Read.All`만 허용한다.

### SITE-ACCESS-R08 — 기존 감사 조회와 Excel 통합

- severity: `P2`
- status: `RESOLVED`
- resolution: 기존 list/detail/summary/export contract에 SiteAccess 원본을 추가하고, 기존 Global·Authorization 사건과 mutation audit 의미는 변경하지 않는다.

## 권장 구현 순서

1. additive migration과 DB 함수·guard·권한
2. Backend signal/end와 통합 조회·상세
3. Frontend browser identity·신호 queue·exhaustive View 매핑
4. 관리자 목록·상세·모바일·선택 Excel
5. 29/30/31분, 동시성, 권한, 멈춘 요청, full-stack·desktop/390 검증

## 승인된 제외 범위

- 과거 접속 backfill
- timer heartbeat
- 클릭·키 입력·모든 HTTP request
- URL·query·업무 식별자 저장
- 신규 권한·관리자 메뉴·알림 채널
- Persistent UAT, 실제 운영 DB, Azure 배포, Git 게시
