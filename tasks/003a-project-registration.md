# TASK-003A: Sales Project Registration and Panel Placeholder Management

## 목적

영업이 프로젝트를 등록하고 면수만큼 제품·패널 Placeholder를 생성·관리할 수 있는 최소 업무 흐름을 구현한다. 모든 활성 내부 사용자는 프로젝트와 패널을 조회할 수 있고, 영업만 프로젝트 업무 쓰기를 수행한다.

## 범위

- 프로젝트 등록, 목록, 상세, 수정
- 면수 증가와 감소
- 면수 기준 패널 Placeholder 자동 생성
- 프로젝트 보류, 보류 해제, 취소, 재활성
- 패널 Placeholder 목록과 기본 상세 조회
- 프로젝트 감사이력 조회
- 판매금액 권한별 응답 필터링
- React 화면과 Backend/Frontend/E2E 테스트

## 제외 범위

- 설계 패널명·사이즈 입력과 Excel 업로드
- 실제 QR 생성, QR 스캔, 역할별 QR 이동
- 구매, 생산계획, 제조, 품질, 물류, 알림
- 프로젝트 진행률 계산
- Microsoft Entra ID, PWA Service Worker, Azure 배포

## 사용자 역할

- Sales: 프로젝트 등록·수정·면수 변경·보류·해제·취소·재활성 가능, 판매금액 조회 가능
- System Administrator: 전체 조회와 판매금액 조회 가능, 프로젝트 업무 쓰기 불가
- Production Planning, Manufacturing, Quality, Logistics, Read Only: 전체 조회 가능, 판매금액 미조회, 프로젝트 업무 쓰기 불가

## 입력항목

필수:

- CustomerName / 고객사
- Item
- ProjectCode / PJT Code
- ProjectTitle / PJT Title
- PanelCount / 면수
- DeliveryDate / 납기일
- SalesOwnerUserId / 영업담당자

선택:

- SalesAmount / 판매금액
- CurrencyCode / 통화
- DeliveryLocation / 납품장소

## 상태

- Active
- OnHold
- Cancelled
- Completed

신규 프로젝트는 Active로 생성한다. TASK-003A에서는 Completed 전환을 구현하지 않는다.

## 시작조건

- `0001`, `0002`, `0003` migration이 적용되어 있어야 한다.
- 개발/테스트 환경에서는 명시적으로 활성화된 개발용 인증을 사용할 수 있다.
- 프로젝트 생성·수정 요청 사용자는 `Project.Create` 또는 `Project.Update` 등 해당 쓰기 Permission을 가진 Sales 사용자여야 한다.

## 완료조건

- Sales가 필수값으로 프로젝트를 등록하면 프로젝트와 패널 Placeholder가 하나의 transaction으로 생성된다.
- ProjectTitle은 정규화 기준으로 전체 시스템에서 유일하다.
- PanelCount만큼 P01, P02 형식의 패널이 생성된다.
- 목록, 상세, 패널, 감사이력 API와 화면이 동작한다.
- 판매금액은 Sales와 System Administrator에게만 응답된다.
- 지정된 Backend, Frontend, E2E 검증이 통과한다.

## 차단조건

- 필수값 누락, 잘못된 날짜, 잘못된 금액, 잘못된 통화 형식
- ProjectTitle 정규화 중복
- SalesOwnerUserId가 활성 Sales 사용자가 아님
- 권한 없는 사용자 쓰기 요청
- 잘못된 상태 전이
- Cancelled 프로젝트의 PanelCount 변경
- 면수 감소 시 취소할 활성 패널 선택 수 불일치 또는 다른 프로젝트 패널 주입
- PanelCount가 1 미만이거나 프로젝트 활성 패널 최대값 500을 초과함

## 권한

- 조회: `Project.Read.All` 또는 제한 계정의 `UserProjectAccess`
- 생성: `Project.Create`
- 수정·면수 변경·보류 해제·재활성: `Project.Update`
- 보류: `Project.Hold`
- 취소: `Project.Cancel`
- 판매금액 조회: `Project.SalesAmount.Read`

System Administrator는 판매금액 조회만 가능하며 프로젝트 업무 쓰기 Permission은 부여하지 않는다.

## 면수와 동시성

- 한 프로젝트의 활성 패널 최대값은 500이다.
- 제조 일괄작업 최대 50면은 제조 TASK의 작업 묶음 제한이며, 프로젝트 활성 패널 최대값 500과 별도이다.
- 취소된 패널은 활성 패널 수 상한 계산에서 제외한다.
- 취소 후 새 패널을 만들 수 있으므로 SequenceNumber 자체는 500으로 제한하지 않는다.
- 면수 증가·감소, 보류, 해제, 취소, 재활성은 transaction 안에서 프로젝트 row를 먼저 `FOR UPDATE`로 잠그고 최신 상태와 활성 패널 수를 다시 검증한다.
- 면수 변경 요청은 화면에서 확인한 `ExpectedActivePanelCount`를 함께 전송한다.
- 서버가 프로젝트 row를 잠근 뒤 재계산한 활성 패널 수와 `ExpectedActivePanelCount`가 다르면 `409 Conflict`를 반환한다.
- 상태 전이는 잠긴 최신 상태에서만 검증하며, 동시에 들어온 요청이 이미 처리된 최신 상태와 맞지 않으면 `409 Conflict`를 반환한다.

## 감사이력

다음을 `project_audit_events`에 기록한다.

- 프로젝트 생성
- 프로젝트 필드 수정
- 면수 증가
- 패널 생성
- 면수 감소
- 패널 취소
- 프로젝트 보류
- 보류 해제
- 프로젝트 취소
- 프로젝트 재활성

저장 항목은 EntityType, EntityId, ProjectId, Action, FieldName, OldValue, NewValue, Reason, ChangedByUserId, ChangedAtUtc, CorrelationId이다. 인증 토큰, 쿠키, 비밀번호, 전체 요청 Body는 저장하지 않는다. 판매금액 변경 이력은 `Project.SalesAmount.Read` 권한이 있는 사용자에게만 반환한다.

## API

- `GET /api/projects`
- `POST /api/projects`
- `GET /api/projects/{projectId}`
- `PATCH /api/projects/{projectId}`
- `POST /api/projects/{projectId}/change-panel-count`
- `POST /api/projects/{projectId}/hold`
- `POST /api/projects/{projectId}/resume`
- `POST /api/projects/{projectId}/cancel`
- `POST /api/projects/{projectId}/reactivate`
- `GET /api/projects/{projectId}/panels`
- `GET /api/projects/{projectId}/panels/{panelId}`
- `GET /api/projects/{projectId}/audit-history`

## 화면

- ProjectListPage
- ProjectCreatePage
- ProjectDetailPage
- ProjectEditPage
- PanelPlaceholderDetailPage

주요 컴포넌트는 ProjectForm, ProjectStatusBadge, ProjectActionMenu, PanelPlaceholderList, PanelCountChangeDialog, 상태변경 Dialog, AuditHistory, SalesAmountField이다.

## 테스트 완료조건

- Backend: 등록, validation, 중복, 권한, 민감정보, 면수 변경, 상태전이, 감사이력, migration 테스트
- Frontend: 권한별 버튼과 판매금액 표시, validation, conflict, 등록 후 상세 이동, 패널 목록, 상태 dialog, 감사이력 렌더링
- E2E: Sales 등록부터 패널 생성, 중복 차단, 제조 역할 조회, 판매금액/수정 버튼 미표시, 면수 증가, 보류 상태 표시까지 확인
- Mock UI browser smoke test는 API route mock을 사용해 화면 상태를 빠르게 검증한다.
- Full-stack E2E는 전용 임시 PostgreSQL DB, 실제 ASP.NET Core Backend, 실제 React Frontend, Playwright를 함께 실행하며 API route mock을 사용하지 않는다.
