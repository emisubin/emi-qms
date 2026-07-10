# UAT-VERIFY-001 User Manual

## 1. UAT 통합 검증이란 무엇인가

현재 검수 환경의 화면, 데이터 수, 권한, 알림 상태와 안전장치가 서로 맞는지 확인하는 절차다. 이번 검증은 데이터를 저장하거나 지우지 않고 조회만 한다.

## 2. 왜 Review-safe 화면을 사용하는가

Review-safe는 실수로 저장·삭제·발송 버튼을 눌러도 서버와 DB가 변경을 막는 검수 전용 환경이다. Development UAT는 실제 저장 검수용이므로 통합 기준선 확인에는 Review-safe를 사용한다.

## 3. 접속 주소

- 공식 Review-safe: `https://localhost:5190`
- Teams Activity 웹 화면: `https://localhost:5190/teams/activity`
- 관리자 화면: `https://localhost:5190/admin`
- 알림 발송 상태: `https://localhost:5190/admin/system/notification-deliveries`
- 에스컬레이션 상태: `https://localhost:5190/admin/system/work-item-escalations`

5191은 rollback 비교용 Candidate다. 일반 검수는 5190에서 한다.

## 4. 어떤 화면을 확인하는가

- 프로젝트 목록
- 내 업무
- 알림과 Teams Activity
- 관리자 dashboard
- 사용자·부서·휴일
- 알림 발송 상태
- 에스컬레이션 상태
- 수동 알림 발송 화면의 비활성화 상태

## 5. 조회할 수 있는 항목

- 목록, 상세, 검색, 필터, 정렬
- 프로젝트·업무 연결 정보
- 알림 상태와 관리자 처리 상태
- Dashboard Failed/Pending 수
- Active escalation 수와 L0~L3 구분
- Migration 호환 상태

## 6. 할 수 없는 작업

Review-safe에서는 저장, 수정, 삭제, 복구, 업무 완료/취소, 읽음 처리, 발송, retry, 확인/제외 처리, upload/import가 차단된다. 버튼이 비활성화되고 직접 API 요청도 423으로 거절된다.

## 7. Dashboard 숫자 확인 방법

1. 관리자 dashboard의 Failed와 Pending 숫자를 확인한다.
2. 알림 발송 상태 목록에서 처리되지 않은 Failed/Pending 필터 수와 비교한다.
3. Active escalation 숫자를 에스컬레이션 목록과 비교한다.
4. 차이가 있으면 숫자와 화면 alias만 기록하고 실제 알림 제목이나 수신자는 복사하지 않는다.

현재 자동 기준선은 open Failed 0, open Pending 0, active escalation 0으로 일치한다.

## 8. 알림 실패·대기 의미

- Failed: 외부 채널 발송이 완료되지 않은 기록
- Pending: worker 또는 재시도 시각을 기다리는 기록
- Acknowledged: 운영자가 장애 근거를 확인한 기록
- Dismissed: 검수 목록에서 noise로 분류한 기록

현재 Failed 20건은 모두 Acknowledged 또는 Dismissed 상태라 dashboard open Failed에는 포함되지 않는다. 이번 검증에서는 상태를 바꾸지 않는다.

## 9. 에스컬레이션 의미

- L0: 예정일 임박
- L1: 예정일 초과
- L2: 초과 후 추가 확인 단계
- L3: 상위 확인 단계

현재 active escalation은 0이다. 과거 resolved 기록은 보존된다.

## 10. 테스트 데이터와 실제 데이터 구분

자동 검증은 제목·본문을 보고서에 복사하지 않고 test 표식이 있는 row 수만 집계한다. 후보라는 이유만으로 자동 삭제하지 않는다. 실제 업무인지 test인지 운영자가 별도 승인 절차에서 확인한다.

## 11. Migration 27/28/1 의미

- Canonical 27: 현재 repository가 요구하는 migration 27개
- Live 28: DB에 기록된 전체 marker 28개
- Legacy 1: 과거 UAT 적용 이력으로 승인·보존된 marker 1개

모든 canonical migration이 있고 추가 marker가 승인된 한 건뿐이며 schema probe도 통과했다. 데이터가 손상됐다는 뜻이 아니다.

## 12. 정상 기준

- Review-safe banner와 27/28/1 호환 안내가 보임
- 주요 목록과 상세가 빈 화면 없이 열림
- 개인 알림은 수신자만 볼 수 있음
- 관리자 화면은 관리자만 볼 수 있음
- mutation 버튼이 비활성화됨
- Console 오류와 page-level 가로 overflow가 없음
- Dashboard와 상세 목록 숫자가 일치함

## 13. 오류 발견 시 기록 방법

다음 정보만 기록한다.

- 화면 alias
- HTTP status 또는 fixed error code
- 발생 여부 boolean
- count 또는 overflow pixel
- desktop/390px 구분

사용자명, 프로젝트명, 알림 제목·본문, 이메일, ID, 화면 전체 HTML, screenshot은 첨부하지 않는다.

## 14. 데이터 정리 권장 방식

- test Failed/Pending: 근거 보존은 Acknowledged, noise는 Dismissed
- 완료된 test work item: Completed
- 중단된 test work item: Cancelled
- notification/recipient 원본: 추적용 보존
- synthetic 사용자·부서·휴일: hard delete하지 않고 승인된 lifecycle 사용

이번 Task에서는 정리하지 않는다. 별도 `TASK-UAT-DATA-001` 승인이 필요하다.

## 15. FAQ

### Failed가 20건인데 Dashboard가 0인 이유는 무엇인가

20건 모두 운영자가 Acknowledged 또는 Dismissed로 분류했기 때문이다. 원래 Failed 상태는 감사 이력으로 유지된다.

### 버튼이 고장 난 것인가

아니다. Review-safe에서 변경 작업을 의도적으로 막은 상태다.

### Live 28이 잘못된 것인가

아니다. 27개 canonical과 승인된 과거 marker 1개가 합쳐진 호환 상태다.

### 검수 화면에 실제 정보가 있는데 보고서에는 왜 없는가

개인정보와 업무정보를 보호하기 위해 보고서에는 boolean, count, fixed status만 남긴다.

### 정리가 필요하면 어떻게 하는가

검수 결과를 바탕으로 별도 데이터 정리 Task를 승인한다. 이 화면에서 임의 삭제하지 않는다.

## 16. 하면 안 되는 작업

- Review-safe를 Development 저장 환경으로 오인
- 실제 외부 알림 발송
- test data 임의 삭제·완료·취소
- DB나 migration marker 직접 수정
- screenshot, DOM/API body, 실제 이름·제목·ID를 검증 보고에 첨부
- PostgreSQL 또는 기존 runtime 재시작

## 17. 사용자 검수 체크리스트

- [x] 5190 접속과 banner 정상
- [x] 27/28/1 호환 상태 이해
- [x] 프로젝트·업무·알림·관리자 조회 정상
- [x] Dashboard와 상세 목록 숫자 일치
- [x] 개인 알림 접근 범위 정상
- [x] 저장·삭제·발송 action 차단
- [x] 표 정렬, Console, 390px overflow 정상
- [x] 실제 화면 원문이 보고서에 노출되지 않음
- [x] 데이터 정리 권장안 이해
- [x] SOP와 이 Manual 이해 가능
- [x] 신규 기능 No-Go 유지 확인

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #29 병합 승인**

검수 증빙: 검수 사용자 A / 2026-07-11 / Current Review-safe 5190와 본 User manual / 주요 조회·수치·권한·잠금 UX·표 정렬·모바일·데이터 정리 권장안 검수와 PR #29 병합 승인.
