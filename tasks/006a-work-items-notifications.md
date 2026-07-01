# TASK-006A: Work Items and Notifications Foundation

## 목적

통합정보시스템의 18단계 업무 프로세스를 공통 workflow 기반으로 정의하고, 단계 완료 시 다음 담당자의 내 업무와 참조/긴급 알림을 자동 생성할 수 있는 기반을 만든다.

## 구현 범위

- 18단계 표준 workflow stage 정의
- 프로젝트별 workflow event 저장
- 내 업무 데이터 모델과 API
- 알림 데이터 모델과 API
- 프로젝트별 담당자 responsibility 확장
- 담당자 fallback 규칙
- workflow orchestrator 기반 서비스
- 기존 구현 화면의 최소 자동 연결
  - 프로젝트 생성 완료
  - 패널정보 저장 완료
  - 생산계획 저장 완료
  - 구매정보 저장 완료
- `/my-work` 내 업무 화면
- `/notifications` 알림 화면
- 프로젝트 상세 workflow 요약
- 공통 메뉴에 내 업무/알림 추가

## 제외 범위

- 실제 Microsoft 365 로그인
- 실제 Teams/Email 알림 발송
- QR 이미지 생성/출력
- Pending List 상세 기능
- 부적합 상세 조치 기능
- IQC/LQC/OQC/FAT 체크리스트 입력 화면
- 제조 체크리스트
- 제조 시작/종료 화면
- 자재 도착/IQC/입고 확정 실제 상세 화면
- 키팅 완료 화면
- 물류 포장/출발/납품 화면
- 세금계산서 발행 화면
- 검사성적서 PDF 출력
- 공통 파일 업로드 모듈
- 관리자 기준정보 전체 페이지
- ECOUNT 연동
- PWA Service Worker

## 18단계 표준 프로세스

| 순서 | 부서 | 단계명 | 내부 code | 선택 |
|---:|---|---|---|---|
| 1 | 영업 | 프로젝트 생성 | SalesProjectCreated | N |
| 2 | 생산관리 | 생산계획·담당자 | ProductionPlanning | N |
| 3 | 설계 | 제품명·사이즈 | DesignPanelInfo | N |
| 4 | 구매 | 구매정보 | ProcurementInfo | N |
| 5 | 자재 | 자재 도착 | MaterialArrived | N |
| 6 | 품질 | 수입검사 | IQC | N |
| 7 | 자재 | 입고 확정 | ReceiptConfirmed | N |
| 8 | 자재 | 키팅 완료 | KittingCompleted | N |
| 9 | 제조 | 제조 작업 | ManufacturingWork | N |
| 10 | 품질 | LQC | LQC | N |
| 11 | 제조 | 제조 완료 | ManufacturingCompleted | N |
| 12 | 품질 | 자체검수 | OQC | N |
| 13 | 품질 | 전진검수 | CustomerInspection | N |
| 14 | 품질 | FAT 선택 | FAT | Y |
| 15 | 물류 | 포장 완료 | PackingCompleted | N |
| 16 | 물류 | 출발 처리 | DepartureProcessed | N |
| 17 | 물류 | 납품 완료 | DeliveryCompleted | N |
| 18 | 영업 | 세금계산서·완료 | SalesSettlementCompleted | N |

FAT 필요 여부는 후속 TASK에서 프로젝트별 설정으로 확장한다. 이번 TASK에서는 optional flag만 둔다.

## 내 업무 / 참조 알림 / 긴급 알림

- 내 업무: 사용자가 실제로 처리해야 하는 업무. 단계 완료 시 다음 단계 담당자에게 자동 생성된다.
- 참조 알림: 직접 처리할 필요는 없지만 알아야 하는 정보. 정담당자에게 업무가 생성될 때 부담당자 또는 관련 부서에 생성된다.
- 긴급/차단 알림: 부적합, PUNCH LIST, 제조 중단, 필수 입력 누락, 재검사 필요 등 다음 단계가 막히는 상황을 표현한다. 이번 TASK에서는 타입과 저장 구조만 준비한다.

## 담당자 구조

기존 `project_assignees`를 부서별 테이블로 나누지 않고 `responsibility_type`을 확장한다.

품질 제외 부서는 정/부 2명:

- SalesPrimary
- SalesSecondary
- DesignPrimary
- DesignSecondary
- ProductionPlanningPrimary
- ProductionPlanningSecondary
- ProcurementPrimary
- ProcurementSecondary
- MaterialsPrimary
- MaterialsSecondary
- ManufacturingPrimary
- ManufacturingSecondary
- LogisticsPrimary
- LogisticsSecondary

품질은 단계별 담당자:

- QualityIQC
- QualityLQC
- QualityOQC
- QualityCustomerInspection

TASK-005A 기존 responsibility(`Procurement`, `ProductionPlanning`, `Manufacturing`, `Quality`, `Logistics`)는 기존 데이터 호환을 위해 유지한다.

## 자동 업무 생성 규칙

업무 요청은 수동 버튼이 아니라 단계 완료 event에 의해 자동 생성된다. 같은 이벤트가 반복 실행되어도 `idempotency_key`로 중복 생성하지 않는다.

- 프로젝트 생성 완료 → Item별 최신 생산계획 단계와 구매 필수 항목 skeleton 생성, 생산관리 담당자 내 업무, 전체 부서 참조 알림
- 생산계획 입력 완료 → 설계 담당자와 구매 담당자 내 업무
- 제품명·사이즈 입력 완료 → 구매 담당자 내 업무
- 구매정보 입력 완료 → 자재 담당자 내 업무, 생산관리/제조 참조 알림

후속 상세 단계는 stage만 정의하고 실제 페이지 연동은 후속 TASK에서 진행한다.

## fallback 규칙

내 업무 생성 시 담당자 결정 순서:

1. 해당 responsibility primary 또는 단계 담당자
2. 없거나 비활성이면 secondary
3. 그래도 없으면 SalesPrimary
4. SalesPrimary도 없거나 비활성이면 SalesSecondary
5. 그래도 없으면 System Administrator

품질 단계 매핑:

- IQC → QualityIQC
- LQC → QualityLQC
- OQC → QualityOQC
- 전진검수/FAT → QualityCustomerInspection

fallback으로 결정된 경우 알림/업무 설명에 담당자 누락 정보를 포함한다.

## API 목록

- `GET /api/my-work`
- `GET /api/my-work/summary`
- `GET /api/my-work/assigned-projects`
- `GET /api/my-work/{workItemId}`
- `POST /api/my-work/{workItemId}/start`
- `POST /api/my-work/{workItemId}/complete`
- `POST /api/my-work/{workItemId}/cancel`
- `GET /api/notifications`
- `GET /api/notifications/summary`
- `POST /api/notifications/{notificationId}/read`
- `POST /api/notifications/read-all`
- `GET /api/projects/{projectId}/workflow`
- `GET /api/workflow/stages`
- `GET /api/procurement/settings/required-items`
- `PATCH /api/procurement/settings/required-items/{itemCode}`

## 화면 목록

- `/my-work`: 전체/시작 전/진행 중/완료/담당 프로젝트 탭, 내 업무 KPI, 담당 프로젝트 KPI, 프로젝트별 업무 그룹, 담당 프로젝트 목록, 시작/완료, 프로젝트 이동
- `/notifications`: 전체/읽지 않음/읽음 탭, 프로젝트별 알림 그룹, 참조/긴급 알림, 읽음 처리, 프로젝트 이동
- 프로젝트 상세 workflow 요약: 18단계 상태, 현재/다음 단계, 생성 업무 수
- `/procurement/settings`: Item별 필수 구매 항목 설정. Item별 최신 설정 1개만 유지하며, 이후 새 프로젝트 생성 시 구매정보 기본 row와 구매정보 완료 여부 판단 기준으로 사용한다. 기존 프로젝트 구매 row는 자동 변경하지 않는다.
- 공통 메뉴: 내 업무, 프로젝트, 생산관리, 구매, 자재, 알림

## Migration

`database/migrations/0010_work_items_notifications.sql` 추가.

- `workflow_stages`
- `project_workflow_events`
- `work_items`
- `notifications`
- `notification_recipients`
- `project_assignees.responsibility_type` constraint 확장

기존 0001~0009 migration은 수정하지 않는다.

`database/migrations/0011_procurement_required_items.sql` 추가.

- `procurement_required_item_templates`
- `procurement_required_item_template_rows`
- Item별 active 필수 구매 항목 template
- 구매정보 완료 판정용 기준 데이터

## 권한

- 내 업무 조회/시작/완료/취소: assigned user 본인만
- 알림 조회/읽음 처리: recipient 본인만
- workflow stage 조회: 활성 내부 사용자
- 프로젝트 workflow 조회: 프로젝트 읽기 권한이 있는 사용자
- System Administrator는 관리자 이력 조회 권한은 유지하되 업무 처리자로 우회하지 않는다.
- 기존 프로젝트/패널/생산계획/구매 권한은 변경하지 않는다.

## 테스트

- Migration 0010 적용
- workflow_stages 18개 seed
- work item 생성과 idempotency
- 내 업무 본인 조회/시작/완료 권한
- 알림 본인 조회/읽음 처리
- 담당자 fallback
- 기존 페이지 hook 중복 생성 방지
- Frontend 메뉴, 내 업무, 알림, 프로젝트 workflow 요약
- 구매 필수 항목 설정 권한/validation/workflow 구매 단계 판정
- Full-Stack E2E: 프로젝트 생성 후 내 업무/알림 확인
- UAT DB persistence
- seed 격리 A/B/C/D

## 사용자 검수 절차

- [ ] 메뉴에 내 업무가 있음
- [ ] 메뉴에 알림이 있음
- [ ] 내 업무 탭이 전체/시작 전/진행 중/완료/담당 프로젝트로 표시됨
- [ ] 알림이 프로젝트별로 묶여 표시됨
- [ ] 구매 필수 항목 설정 페이지가 있음
- [ ] 프로젝트 생성 후 다음 담당자 내 업무가 자동 생성됨
- [ ] 참조 대상자에게 알림이 생성됨
- [ ] 내 업무에서 시작/완료 처리 가능
- [ ] 알림 읽음 처리 가능
- [ ] 프로젝트 상세에서 18단계 workflow 요약이 보임
- [ ] 동일 작업 재저장 시 중복 업무가 생기지 않음
- [ ] PC table / Mobile card
- [ ] Console 오류 없음

## 후속 TASK

- TASK-006B 기존 페이지 18단계 연결 보강
- TASK-007A Pending List 공통 모듈
- TASK-008A 자재 도착 / IQC 요청 / 입고 확정
- TASK-009A 검사 체크리스트 / IQC 디지털 성적서 / PDF 출력
- TASK-010A 키팅 완료 / 제조 내 업무 생성
- TASK-011A 제조 체크리스트 / 작업 시작·종료
- TASK-012A LQC / OQC / 전진검수 / FAT
- TASK-013A 물류 / 납품 완료 / 영업 정산
- TASK-ADMIN-002 Work Item Activity Administration
  - 업무 생성 이력
  - 업무 시작 이력
  - 업무 완료 이력
  - 담당자 변경 이력
  - 프로젝트별 업무 처리 시간
  - 사용자별 업무 처리 내역
  - 이번 TASK에서는 `work_items.started_at_utc`와 `completed_at_utc` 데이터만 유지하고 관리자 업무 이력 화면은 구현하지 않는다.
