# TASK-SITE-ACCESS-001 — 사이트 접속 이력 운영 SOP

> 상태: Local implementation·자동·독립 검증 완료 / 사용자 검수·Git 게시·Persistent UAT·Azure 배포 미승인

## 적용 기준

- 현재 구현 후보·기준 SHA·검증 결과·Finding은 [Implementation report](site-access-001-implementation-report.md)를 기준으로 한다.
- 기능 계약은 [Change 001](site-access-001-change-001.md), 최신 main 통합·migration 번호·게시 경계는 [Change 002](site-access-001-change-002.md)을 기준으로 한다.
- 환경별 Azure 대상·명령·승인·보안·복구 절차는 [Azure deployment SOP](azure-deploy-001-sop.md)를 사용한다. 이 문서는 사이트 접속 원장의 추가 점검만 정의한다.
- 검수 증빙은 [Privacy-safe Evidence](../docs/development/privacy-safe-evidence.md)를 따른다.
- 구현 후보가 uncommitted 상태이므로 현재 독립 검증은 같은 recovery worktree의 변경이 유지될 때만 유효하다. 코드·migration이 달라지면 자동·독립 검증을 다시 실행한다.

## 운영 원칙

- 사이트 접속 이력은 기존 로그인·로그아웃 및 데이터 변경 감사 원장과 분리해 저장하고, `전체 감사 이력`에서 통합 조회한다.
- 저장 신호는 인증된 사용자의 페이지 최초 진입, 새로고침 또는 다른 화면 진입에만 보낸다. 클릭·키 입력·모든 HTTP 요청·주기적 heartbeat는 수집하지 않는다.
- 같은 사용자와 같은 브라우저 client가 마지막 활동 후 30분 미만에 보낸 신호는 같은 접속 행으로 묶는다. 정확히 30분 이상이면 새 행이다.
- 30분과 신호 순서는 클라이언트 시각이 아니라 DB가 lock을 얻은 뒤 잡은 수신 시각으로 판단한다.
- 마지막 활동은 페이지 진입 또는 새로고침 신호 시각이다. 실제 근무 시작·종료나 체류 시간을 뜻하지 않는다.
- 직접 로그아웃으로 닫힌 행은 다시 열지 않는다. 이후 인증된 신호는 시간 간격과 무관하게 새 행을 만든다.
- `30분 경과`는 조회 시 계산하는 표시 상태이며 종료 시각을 추정 저장하지 않는다. 늦게 도착한 신호도 client timestamp 없이 DB 수신 순서로 처리한다.
- 접속 기록 실패는 화면 진입·이동·로그아웃을 막지 않는 best-effort 보조 기록이다.
- 조회 권한은 기존 `Audit.Read.All`만 사용한다. 새 관리자 권한을 만들지 않는다.

## 배포 전 확인

1. 대상 환경의 현재 migration 원장과 Backend·Frontend 배포 SHA를 기록한다.
2. G2 migration `0084_g2_delivery_target_defect.sql` 다음의 `0085_site_access_sessions.sql`이 아직 적용되지 않았는지 확인한다. 이미 같은 번호가 있으면 재실행하지 말고 적용 원문의 digest와 migration 원장을 대조하며, 내용이 다르면 배포를 중단한다.
3. Runtime 역할이 새 원장 테이블에는 조회만 가능하고, 쓰기는 승인된 DB 함수 실행으로만 가능한지 확인한다.
4. 실제 Entra 설정과 `AuthenticatedIdentity` 정책이 기존 운영 계약과 같은지 확인한다.
5. 배포 승인, 운영 DB migration 승인과 Azure 공개배포 승인을 각각 확인한다.

## 적용 순서

1. 운영 백업·복구 가능 상태를 확인한다.
2. additive migration `0085_site_access_sessions.sql`을 적용한다.
3. migration 원장, 접속 기록 시작 시각, 새 테이블·함수·trigger·권한을 확인한다.
4. Backend를 배포하고 readiness가 정상인지 확인한다.
5. Frontend를 배포하고 정적 자산과 보안 점검을 완료한다.
6. synthetic 운영 계정으로 첫 접속, 새로고침, 다른 화면 진입, 명시적 로그아웃을 한 번씩 확인한다.
7. 감사 조회 권한이 없는 역할은 403이고 감사 관리자는 접속 행·상세·선택 Excel을 볼 수 있는지 확인한다.

## 정상 동작 확인

- 같은 브라우저의 여러 탭에서 같은 사용자가 30분 미만으로 활동하면 한 행에 메뉴가 최초 방문 순서로 한 번씩 쌓인다.
- 다른 브라우저·기기·프로필은 별도 접속 행으로 기록된다.
- 정확히 30분 이상 활동 간격이 생기면 새 행이 생성된다.
- 로그아웃하면 현재 접속 행에 종료 시각과 `직접 로그아웃` 상태가 한 번만 기록된다.
- 로그아웃 없이 30분 이상 새 신호가 없으면 조회 시 `30분 경과`로 표시된다.
- 승인 대기·비활성 사용자처럼 인증은 됐지만 앱 접근 결과가 다른 경우도 해당 결과 snapshot이 보존된다.
- 접속 기록 범위가 기존 변경·인증 기록 범위와 별도로 표시된다.

## 장애 대응

### 접속 이력이 전혀 생기지 않음

1. Browser 개발자 도구에서 접속 signal이 401·403·5xx인지 확인한다.
2. 401이면 인증 session과 Entra callback을 먼저 확인한다.
3. 403이면 요청 사용자가 아니라 endpoint 정책·운영 설정 drift를 확인한다. signal은 `AuthenticatedIdentity` 계약이다.
4. 5xx이면 Backend log와 DB 함수 실행 권한·migration 적용 여부를 확인한다.
5. 화면 사용은 계속 가능해야 한다. 기록 장애 때문에 업무 API나 로그아웃을 중단하지 않는다.
6. Backend의 `Emi.Qms.Api.Audit.SiteAccess` category와 `Best-effort site access` 오류를 확인한다. 이 Task는 새 alert·dashboard를 추가하지 않았으므로 반복 오류는 별도 운영 알림 change로 다룬다.

### 같은 접속이 여러 행으로 보임

1. 사용자와 브라우저 client가 같은지 확인한다.
2. 마지막 활동 사이가 정확히 30분 이상인지 확인한다.
3. 브라우저 저장소 초기화, 시크릿 창, 다른 브라우저 프로필 또는 기기 사용 여부를 확인한다.
4. Web Locks를 사용할 수 없으면 IndexedDB transaction으로 여러 탭의 최초 client 생성을 직렬화한다. localStorage와 IndexedDB가 모두 차단되면 접속 누락 방지를 위해 현재 문서 실행 동안만 유지되는 임시 client를 사용할 수 있으며, 이때는 새로고침 뒤 새 행이 생길 수 있다.
5. 원장을 병합·삭제하지 않는다. 재현 가능한 제품 결함이면 forward-fix로 처리한다.

### 로그아웃 종료가 비어 있음

1. 해당 행이 실제 명시적 로그아웃인지, 창 닫기·session 만료인지 구분한다.
2. 명시적 로그아웃 요청이 1.5초 안에 완료되지 않아도 실제 로그아웃은 계속 진행하는 것이 정상 계약이다.
3. Backend·DB 장애를 복구한 뒤 이후 접속으로 재검증한다. 과거 종료 시각을 추정해 소급 입력하지 않는다.

## Rollback·forward-fix

- migration은 additive이고 접속 원장은 감사 증빙이므로 운영 적용 후 테이블·행을 삭제하거나 기존 migration을 되돌리지 않는다.
- Frontend 또는 Backend 문제가 있으면 직전 호환 application으로 rollback하거나 신규 forward-fix를 배포한다.
- 기록 신호만 긴급 중지해야 하면 Frontend 배포를 직전 버전으로 되돌리되 기존 로그인·변경 감사와 원장 데이터는 보존한다.
- schema·함수·권한 결함은 다음 번호의 additive migration으로 고친다.
- 복구 뒤 synthetic 사용자로 신호·조회·상세·Excel·로그아웃을 다시 확인한다.

## 금지 사항

- 접속 행의 수동 update·delete
- 실제 사용자 이름·이메일·UPN·원본 IP를 Task 문서나 채팅 증빙에 복사
- URL·query·프로젝트 ID·검색어를 메뉴 코드 대신 저장
- 마지막 활동을 실제 근무시간이나 체류시간으로 해석
- 게시·운영 DB·Azure 승인 없이 이 SOP의 적용 절차 실행
