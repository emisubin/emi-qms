# TASK-CALENDAR-001 구현 보고서

## 1. 목적

TASK-CALENDAR-001의 목적은 EMI 프로젝트 통합관리시스템의 공휴일/영업일 기준을 통일하고, 생산계획 캘린더와 TASK-NOTIFY-002 예정일 기반 에스컬레이션이 같은 날짜 계산 기준을 사용하게 하는 것이다.

핵심 목표는 다음과 같다.

- 토요일/일요일/대한민국 공휴일/대체공휴일/임시공휴일/회사휴일을 비영업일로 계산
- 생산계획 캘린더 휴일 표시 보정
- BusinessDayCalculator 기반 공통 계산 구조 마련
- System Administrator가 휴일 데이터를 직접 보강할 수 있는 최소 관리 기능 제공
- 공식 공휴일 API service key가 없어도 Excel/manual 방식으로 운영 휴일 데이터를 유지할 수 있게 함

## 2. 구현 범위

이번 TASK에서 구현한 범위는 다음과 같다.

- `system_holidays.holiday_type` 확장
- `system_holidays.note` 확장
- `BusinessDayCalculator`
- `/api/calendar/business-days`
- System Administrator 전용 Admin Holiday API
- Admin Holiday 관리 화면
- 휴일 Excel 양식 다운로드
- 휴일 Excel 업로드 preview/apply
- 생산계획 캘린더 business-days API 연동
- TASK-NOTIFY-002 연결 원칙 문서화

## 3. 제외 범위

이번 TASK에서 제외한 범위는 다음과 같다.

- 공식 공휴일 API service key 운영 sync 활성화
- 국가공휴일 자동 sync scheduler
- 회사 자체 근무일 지정
- TASK-NOTIFY-002 에스컬레이션 worker
- Teams Activity Feed 실제 구현
- Teams DM 구현
- Pending List 구현

## 4. DB/Migration

신규 migration은 두 개다.

- `database/migrations/0017_system_holiday_types_business_calendar.sql`
- `database/migrations/0018_admin_calendar_holiday_management.sql`

`0017`은 `system_holidays.holiday_type`을 추가하고 기존 데이터를 `National`, `Substitute`, `Temporary`, `Company` 중 하나로 분류할 수 있게 했다. 기존 데이터는 기본적으로 `National`로 보존하며, 이름/source에서 분류 가능한 일부 데이터만 보정한다.

`0018`은 관리자 휴일 관리와 Excel 등록을 위해 `system_holidays.note`를 추가하고, 연도/유형 조회 인덱스를 추가했다.

삭제 정책은 hard delete가 아니라 `is_active=false` 비활성화다. 비활성 휴일은 `/api/calendar/business-days` 계산에서 제외된다. 기존 `0001~0016` migration은 수정하지 않았다.

## 5. Backend 주요 파일

- `backend/src/Emi.Qms.Api/Calendar/BusinessCalendarContracts.cs`: 휴일 유형, business-days 응답 DTO, holiday DTO 정의
- `backend/src/Emi.Qms.Api/Calendar/BusinessDayCalculator.cs`: 영업일 계산 공통 로직
- `backend/src/Emi.Qms.Api/Calendar/BusinessCalendarStore.cs`: `system_holidays`를 읽어 기간별 calendar 응답 생성
- `backend/src/Emi.Qms.Api/Calendar/BusinessCalendarEndpointExtensions.cs`: `/api/calendar/business-days`
- `backend/src/Emi.Qms.Api/Calendar/AdminCalendarHolidayContracts.cs`: 관리자 휴일/Excel DTO
- `backend/src/Emi.Qms.Api/Calendar/AdminCalendarHolidayStore.cs`: 관리자 휴일 CRUD, Excel apply 저장 로직
- `backend/src/Emi.Qms.Api/Calendar/AdminCalendarHolidayEndpointExtensions.cs`: System Administrator 전용 휴일 API
- `backend/src/Emi.Qms.Api/Calendar/CalendarHolidayExcelParser.cs`: Excel template 생성, preview 파싱, row validation
- `backend/src/Emi.Qms.Api/Program.cs`: Calendar store/parser DI 등록과 endpoint mapping

Backend tests:

- `backend/tests/Emi.Qms.Api.Tests/BusinessDayCalculatorTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/ProductionPlanningApiTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`

## 6. Frontend 주요 파일

- `frontend/src/projects.ts`: business calendar, admin holiday, Excel preview/apply 타입 정의
- `frontend/src/api.ts`: calendar/admin holiday API client 추가
- `frontend/src/App.tsx`: Admin Holiday page, route, navigation, 생산계획 calendar 연동
- `frontend/src/styles.css`: holiday badge, admin holiday form, 모바일 grid 보정
- `frontend/tests/App.test.tsx`: 관리자 휴일 화면, Excel preview/apply UI 회귀 테스트

## 7. 휴일 정책

영업일 정책은 다음 기준으로 구현했다.

- 토요일은 비영업일
- 일요일은 비영업일
- `National`은 비영업일
- `Substitute`는 비영업일
- `Temporary`는 비영업일
- `Company`는 비영업일
- `is_active=false`인 휴일은 계산에서 제외

주말은 `system_holidays` 데이터가 없어도 비영업일이다. 휴일 데이터는 `DateOnly` 기준으로 처리해 UTC/local 변환으로 하루가 밀리는 문제를 피한다.

## 8. BusinessDayCalculator

`BusinessDayCalculator`는 후속 에스컬레이션 로직에서 재사용할 공통 계산기다.

주요 메서드:

- `IsWeekend`
- `IsHoliday`
- `IsCompanyHoliday`
- `IsBusinessDay`
- `AddBusinessDays`
- `SubtractBusinessDays`
- `GetPreviousBusinessDay`
- `GetNextBusinessDay`
- `CountBusinessDays`
- `Describe`

TASK-NOTIFY-002는 L0/L2/L3 날짜 계산을 이 계산기로 수행해야 한다. NotificationEscalationWorker에 별도 주말/공휴일 계산을 하드코딩하지 않는다.

## 9. Admin Holiday Management

관리자 휴일 API는 System Administrator 전용이다.

Endpoint:

- `GET /api/admin/calendar/holidays?year=YYYY`
- `POST /api/admin/calendar/holidays`
- `PUT /api/admin/calendar/holidays/{id}`
- `DELETE /api/admin/calendar/holidays/{id}`

Validation:

- 날짜 필수
- 휴일명 필수
- 휴일유형은 `National`, `Substitute`, `Temporary`, `Company` 중 하나
- 같은 날짜와 같은 휴일유형의 활성 휴일 중복 방지

Frontend 관리 화면은 연도 선택, 목록, 추가, 수정, 비활성화, 유형 badge, Excel 다운로드/업로드 preview/apply를 제공한다.

## 10. Excel 양식/Preview/Apply

Excel 양식 컬럼은 다음 네 가지다.

- 날짜
- 휴일명
- 휴일유형
- 비고

Preview는 다음을 검증한다.

- 날짜 형식
- 휴일명 필수
- 휴일유형 허용값
- 파일 내 날짜/유형 중복
- 기존 DB row 존재 여부

Apply 정책:

- 저장 가능한 행만 반영
- 기존 동일 날짜/유형은 update
- 신규 날짜/유형은 insert
- 오류 행은 skip

Frontend에서도 오류 행이 있어도 저장 가능한 행이 있으면 apply할 수 있게 보정했다.

## 11. Production Planning Calendar

생산계획 캘린더는 `/api/calendar/business-days` 응답을 기준으로 휴일을 표시한다.

표시 기준:

- 주말 표시
- `National`, `Substitute`, `Temporary`, `Company` 휴일 표시
- 휴일명/title 제공
- 회사휴일 별도 스타일 적용
- 기존 sticky 생산단계 열과 날짜 column width 유지

UAT browser smoke에서 관리자 휴일 화면과 생산관리 화면의 page-level fatal error 및 console error가 없음을 확인했다.

## 12. Tests

실행한 주요 검증:

- `git diff --check`
- `actionlint .github/workflows/ci.yml`
- backend Release build
- backend 전체 test
- Calendar targeted tests
- Migration tests
- Authorization 포함 backend 전체 test
- frontend lint
- frontend typecheck
- frontend unit test
- frontend build
- mock UI smoke
- Full-Stack E2E
- UAT DB persistence
- dev-uat-start 재실행
- Docker Compose config
- PostgreSQL healthy
- UAT Backend `/health/live`
- UAT Backend `/health/ready`
- UAT Frontend HTTP 200
- UAT browser smoke
- secret/PII scan

테스트 결과는 완료 보고와 PR 본문에 실제 실행 결과 기준으로 기록한다.

## 13. UAT 검수 결과

UAT DB는 삭제하거나 truncate하지 않았다. Docker volume도 삭제하지 않았다.

확인 결과:

- schema latest: `0018_admin_calendar_holiday_management`
- `/api/calendar/business-days` 정상 응답
- 평일 Company holiday 등록 시 `isBusinessDay=false`
- 해당 holiday 비활성화 후 평일은 다시 `isBusinessDay=true`
- 주말은 holiday row 없이도 비영업일 유지
- 관리자 휴일 화면 접근 smoke 정상
- 생산관리 화면 smoke 정상

## 14. 보안/Secret

이번 TASK는 secret/env 값을 코드나 문서에 저장하지 않는다.

확인 기준:

- `.env` 파일 미포함
- `.env.entra-local` 미포함
- `.env.notify-local` 미포함
- SMTP password 미포함
- Gmail app password 미포함
- Teams Webhook URL 미포함
- token/client secret 미포함
- raw stack trace 사용자 노출 없음

## 15. 후속 TASK 연결

후속 연결:

- TASK-NOTIFY-002: BusinessDayCalculator 기반 예정일 에스컬레이션
- Calendar sync 후속: 공식 공휴일 API service key 운영 연동
- ADMIN 후속: 운영 기준정보 관리 고도화
- TASK-NOTIFY-003: Teams Activity Feed 개인별 알림

NOTIFY-002 구현 전 결정 필요:

- 생산계획/구매 예정일을 `work_items.due_date`로 동기화할 범위
- L0를 예정일의 직전 영업일로 확정할지 여부
- due_date 없는 work item 제외 정책 최종 확정

## 16. 알려진 제한사항

- UAT의 대한민국 공휴일 데이터가 부족하면 관리자 Excel/manual 등록으로 보강해야 한다.
- 공식 공휴일 API service key는 아직 운영 연동되지 않았다.
- 회사 자체 근무일 지정은 구현하지 않았다.
- 관리자 휴일 UI는 최소 기능이며, 고급 감사/승인 workflow는 후속 범위다.

## 17. 운영 적용 전 체크리스트

- 연간 대한민국 공휴일 Excel 등록
- 대체공휴일/임시공휴일 검수
- 회사휴일 등록
- 생산계획 캘린더 휴일 표시 확인
- 공식 API service key 사용 여부 결정
- NOTIFY-002 전 `work_items.due_date` 정책 확정
- 운영 DB migration 적용 전 백업
- Production/Staging secret/env 점검
