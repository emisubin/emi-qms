# TASK-CALENDAR-001 공휴일 / 영업일 계산 기준 보정

## 1. 문제 배경

생산계획 캘린더에서 휴일 전체가 표시되지 않는 문제가 확인됐다. EMI 프로젝트 통합관리시스템의 업무 기준은 토요일, 일요일, 대한민국 정부 지정 공휴일, 대체공휴일, 임시공휴일, 회사 자체 휴일을 모두 비영업일로 보는 것이다.

TASK-NOTIFY-002 예정일 기반 에스컬레이션도 같은 영업일 계산을 사용해야 하므로, 생산계획 화면 전용 로직이 아니라 공통 Business Calendar 기준을 마련했다.

## 2. 생산계획 캘린더 휴일 누락 원인

확인 결과 UAT `system_holidays`에는 2026년 활성 휴일이 소수만 존재했다. 데이터 자체가 부족하면 생산계획 캘린더는 전체 대한민국 공휴일을 표시할 수 없다.

기존 화면은 `/api/system/holidays` 결과를 프론트에서 직접 해석했다. 이 방식은 주말, 국가공휴일, 회사휴일, 향후 에스컬레이션의 영업일 계산 기준이 분리될 위험이 있었다.

또한 생산계획 읽기 화면에서 개발 사용자 키가 없으면 휴일 조회를 생략하는 조건이 있어, EntraId 모드에서는 휴일 표시가 누락될 수 있었다.

UTC/local 날짜 변환으로 하루가 밀리는 현상은 주요 원인으로 확인되지 않았다. 생산계획 캘린더는 `YYYY-MM-DD` 값을 UTC DateOnly 방식으로 파싱하고 있었다.

## 3. 회사 휴일 정책

이번 TASK에서 반영한 기준은 다음과 같다.

- 토요일은 비영업일이다.
- 일요일은 비영업일이다.
- 대한민국 정부 지정 공휴일은 비영업일이다.
- 대체공휴일은 비영업일이다.
- 임시공휴일도 국가 지정 휴일이면 비영업일이다.
- 회사 자체 휴일은 비영업일이다.
- 회사 자체 근무일 지정은 이번 TASK 범위에서 제외한다.

## 4. 국가공휴일 / 대체공휴일 / 회사휴일 기준

`system_holidays`에 `holiday_type`을 추가해 휴일 유형을 구분한다.

지원 유형:

- `National`: 국가공휴일
- `Substitute`: 대체공휴일
- `Temporary`: 임시공휴일
- `Company`: 회사휴일

기존 데이터는 기본적으로 `National`로 보존하고, 이름 또는 source에 따라 대체공휴일, 임시공휴일, 회사휴일로 분류 가능한 경우 보정한다.

## 5. BusinessDayCalculator 구조

백엔드에 공통 계산기를 추가했다.

주요 기능:

- `IsWeekend(date)`
- `IsHoliday(date)`
- `IsCompanyHoliday(date)`
- `IsBusinessDay(date)`
- `AddBusinessDays(date, days)`
- `SubtractBusinessDays(date, days)`
- `GetPreviousBusinessDay(date)`
- `GetNextBusinessDay(date)`
- `CountBusinessDays(start, end)`

계산 기준은 `DateOnly`와 `system_holidays` 데이터다. 같은 날짜에 여러 휴일 row가 있으면 하나의 날짜 설명으로 병합하며, 회사휴일, 임시공휴일, 대체공휴일, 국가공휴일 순서로 대표 유형을 정한다.

## 6. 생산계획 캘린더 보정 내용

생산계획 캘린더는 이제 `/api/calendar/business-days` 응답을 사용한다.

응답은 기간 내 각 날짜에 대해 다음 정보를 제공한다.

- 날짜
- 주말 여부
- 휴일 여부
- 회사휴일 여부
- 영업일 여부
- 휴일명
- 휴일 유형

프론트는 이 응답을 기준으로 주말과 휴일을 표시한다. 회사휴일은 별도 클래스를 부여해 국가공휴일과 구분할 수 있게 했다.

## 7. NOTIFY-002 연결

TASK-NOTIFY-002 예정일 기반 에스컬레이션은 이 BusinessDayCalculator를 사용해야 한다.

권장 기준:

- L0 D-1은 예정일의 직전 영업일로 계산한다.
- L1은 예정일이 지났고 아직 미완료인 경우 발생한다.
- L2는 예정일 이후 +2영업일 미조치 기준이다.
- L3는 예정일 이후 +3영업일 미조치 기준이다.

NotificationEscalationWorker 또는 후속 서비스에 별도 날짜 계산 로직을 하드코딩하지 않는다.

`work_items.due_date`가 없는 업무는 예정일 기반 에스컬레이션 대상에서 제외하는 것이 안전하다. 생산계획/구매 예정일을 `work_items.due_date`로 동기화할지는 TASK-NOTIFY-002 구현 전 사용자 결정이 필요하다.

## 8. Teams Activity Feed 고려사항

Teams Activity Feed 개인별 알림은 TASK-NOTIFY-003 후속 범위다.

TASK-NOTIFY-002는 에스컬레이션 채널을 TeamsChannel Webhook으로 하드코딩하지 않아야 한다. 개인 알림 의도와 채널 게시를 분리하고, 이후 `TeamsActivity` 채널 handler로 교체하거나 확장할 수 있게 `notification_deliveries`와 channel abstraction을 사용한다.

현재 실제 발송 가능한 Teams 채널은 TeamsChannel Webhook이다. 개인별 Teams Activity Feed actual 발송은 이번 TASK에서 구현하지 않는다.

## 9. 관리자 휴일 관리 구현

보완 작업에서 System Administrator 전용 휴일 관리 기능을 추가했다.

Backend API:

- `GET /api/admin/calendar/holidays?year=YYYY`
- `POST /api/admin/calendar/holidays`
- `PUT /api/admin/calendar/holidays/{id}`
- `DELETE /api/admin/calendar/holidays/{id}`

삭제는 실제 delete가 아니라 `is_active=false` 비활성화로 처리한다. 기존 휴일 이력과 연간 등록 흔적을 보존하기 위함이다.

관리 대상 유형은 `National`, `Substitute`, `Temporary`, `Company` 네 가지다. 같은 날짜와 같은 휴일유형의 활성 휴일은 중복 등록할 수 없다.

Frontend에는 System Administrator 메뉴에 `휴일` 화면을 추가했다. 화면은 연도별 목록, 단건 등록, 수정, 비활성화, Excel 양식 다운로드, Excel 업로드 미리보기, 저장 가능한 행 반영을 제공한다.

## 10. Excel 연간 휴일 등록

관리자가 1년에 한 번 연간 휴일을 등록할 수 있도록 Excel 양식과 preview/apply API를 추가했다.

Endpoint:

- `GET /api/admin/calendar/holidays/template`
- `POST /api/admin/calendar/holidays/preview`
- `POST /api/admin/calendar/holidays/apply`

Excel 컬럼:

- 날짜
- 휴일명
- 휴일유형
- 비고

Preview는 날짜 형식 오류, 휴일유형 오류, 파일 내 날짜/유형 중복, 기존 DB 중복 여부를 보여준다. Apply는 저장 가능한 행만 반영한다. 기존 동일 날짜/유형은 update하고, 새로운 날짜/유형은 insert한다.

대한민국 공휴일 목록을 코드에 장기 하드코딩하지 않는다. 운영 데이터는 관리자 Excel/manual/API 입력으로 `system_holidays`에 보존한다.

## 11. 신규 migration

보완 작업에서 `0018_admin_calendar_holiday_management.sql`을 추가했다.

변경 내용:

- `system_holidays.note` nullable 컬럼 추가
- 연도/유형 조회용 `ix_system_holidays_year_type_lookup` 인덱스 추가

기존 `0001~0017` migration은 수정하지 않았다.

## 12. 테스트 결과

실행 결과:

- `git diff --check`: 통과
- `actionlint .github/workflows/ci.yml`: 통과
- backend Release build: 통과
- backend 전체 test: 251 passed
- BusinessDayCalculator 단위 테스트: 통과
- Business Calendar API 테스트: 통과
- Admin holiday API 테스트: 통과
- Admin holiday Excel template/preview/apply 테스트: 통과
- migration 0017/0018 적용 테스트: 통과
- frontend lint: 통과, 기존 `frontend/src/main.tsx` fast-refresh warning 1건 유지
- frontend typecheck: 통과
- frontend unit test: 52 passed
- frontend build: 통과
- mock UI smoke: 1 passed
- Full-Stack E2E: 16 passed
- Docker Compose config: `infrastructure/docker-compose.yml` 기준 통과
- UAT backend `/health/live`, `/health/ready`: 200
- UAT frontend HTTP: 200
- UAT DB latest migration: `0018_admin_calendar_holiday_management`
- UAT browser smoke: API/User 오류 패턴 없음, console error 0건
- secret/PII scan: 실제 secret 원문 미포함, placeholder/설정 키명만 감지

## 13. 남은 결정사항

- 대한민국 공식 공휴일 데이터를 어떤 운영 절차로 매년 채울지 결정이 필요하다. 현재 공식 API sync 구조는 있으나 서비스 키가 없으면 동기화되지 않는다.
- 공식 공휴일 API service key를 준비하면 자동 sync 또는 관리자 sync 절차를 후속으로 활성화할 수 있다.
- NOTIFY-002에서 생산계획/구매 예정일을 `work_items.due_date`로 동기화할지 결정이 필요하다.
- L0 D-1을 “전날 calendar day”가 아니라 “직전 영업일”로 확정할지 사용자 확인이 필요하다.
- 회사 자체 근무일 지정은 이번 TASK에서 제외했다. 필요 시 후속 검토한다.
