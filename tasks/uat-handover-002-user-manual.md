# TASK-UAT-HANDOVER-002 User Manual — Review-safe runtime 교체 확인

## 1. Review-safe runtime 교체란 무엇인가

Review-safe 주소는 그대로 두고 그 안에서 실행되는 프로그램을 최신 main 버전으로 안전하게 바꾸는 작업이다. 조회 전용 보호 정책과 데이터는 그대로 유지한다.

## 2. 5190/5092와 5191/5093의 차이

- 5190/5092: 사용자가 공식 검수에 사용하는 Current Review-safe
- 5191/5093: 교체 전후를 비교하고 rollback을 돕는 Candidate

두 환경 모두 Persistent UAT를 읽지만 저장·삭제·발송은 차단된다.

## 3. 개인정보를 검증 출력에 표시하지 않는 이유

화면에는 실제 업무 데이터가 있을 수 있다. 자동 검증은 정상 여부만 확인하면 되므로 이름, 프로젝트, 알림 문구나 화면 캡처를 보고서에 옮기지 않는다. 결과는 성공 여부와 건수만 남긴다.

## 4. Canonical 27 / Live 28 / Legacy 1 의미

- Canonical 27: 현재 repository가 요구하는 migration 27개
- Live 28: UAT DB에 남은 적용 이력 28개
- Legacy 1: 동일한 과거 SQL이 merge 전 이름으로 기록된 승인 이력 1개

현재 데이터가 손상됐다는 의미가 아니다. Unknown 또는 missing 기록은 Ready에서 차단된다.

## 5. 접속 주소

- Current Review-safe: `https://localhost:5190`
- Candidate: `https://localhost:5191`
- Development UAT: `https://localhost:5174`
- Preview: `https://localhost:5185`

## 6. 사용자가 확인할 화면

- 메인, 프로젝트, 내 업무, 알림
- Teams Activity 웹 화면
- 관리자 홈, 사용자, 휴일
- 알림 발송 상태, 에스컬레이션, 수동 발송 화면

실제 저장·발송 버튼은 실행하지 않는다.

## 7. 가능한 작업

- 목록과 상세 조회
- 검색, 필터, 정렬
- 메뉴와 tab 이동
- 관리자 dashboard와 이력 확인

## 8. 불가능한 작업

- 생성, 저장, 수정, 삭제, 복구
- 업무 시작/완료/취소
- 알림 읽음, 발송, retry, 확인, 제외
- 사용자·부서·휴일 변경
- import/upload/apply

## 9. Migration 호환 상태

Banner에 repository와 호환된다는 안내와 27/28/1이 표시돼야 한다. `Mismatch` 또는 Ready 실패라면 검수를 멈추고 운영자에게 알린다.

## 10. Ready 실패 의미

서버가 꺼졌다는 뜻만은 아니다. DB read-only, migration 이력 또는 필수 schema를 안전하게 확인할 수 없어서 검수를 차단했다는 뜻일 수 있다. Marker를 직접 고치거나 migration을 실행하지 않는다.

## 11. 데이터가 보존되는 이유

- DB connection 자체가 read-only
- 변경 API가 423으로 차단
- migration/seed/worker/provider 미실행
- legacy marker 삭제 금지
- handover 전후 aggregate 비교

## 12. Rollback 의미

새 runtime에 문제가 있으면 DB를 되돌리지 않고 새 5190/5092 process만 종료한 뒤 이전 Review-safe runtime을 다시 실행하는 것이다. Development와 Candidate는 계속 유지된다.

## 13. FAQ

### 5191을 계속 사용해야 하나요?

아니다. 일반 검수 주소는 5190이다. 5191은 비교와 rollback용으로 잠시 유지한다.

### 화면 내용이 검증 보고에 복사되나요?

아니다. 자동 검증은 boolean, count와 fixed status만 기록한다.

### 28개를 27개로 줄여야 하나요?

아니다. 승인된 과거 marker는 감사 이력으로 보존한다.

### 저장이 필요하면 어떻게 하나요?

승인된 Development UAT 5174 절차를 사용한다. 5190의 잠금을 우회하지 않는다.

## 14. 하면 안 되는 작업

- DB/volume/container reset
- migration marker 삭제·수정
- Review-safe 저장/발송 우회
- 화면 캡처나 실제 데이터 원문을 공개 문서에 첨부
- `.env`, token 또는 certificate key 공유
- Candidate/Development process 임의 종료

## 15. 사용자 검수 체크리스트

- [x] 5190 메인과 주요 화면이 열린다.
- [x] Review-safe banner와 Compatible 27/28/1이 보인다.
- [x] 조회·검색·필터·정렬·상세 이동이 가능하다.
- [x] 저장·수정·삭제·발송 action이 잠겨 있다.
- [x] API 423과 DB read-only 설명을 이해한다.
- [x] 5191과 기능·구조 차이가 없다.
- [x] console 오류와 모바일 overflow가 없다.
- [x] 자동 검증 보고에 실제 화면 원문이 없다.
- [x] Development 5174/5081과 Preview 5185가 유지된다.
- [x] Persistent UAT와 legacy marker가 유지된다.
- [x] SOP와 rollback 의미를 이해한다.
- [x] UAT-VERIFY-001이 다음에 처음부터 실행됨을 이해한다.

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #28 병합 승인**

검수 증빙: 검수 사용자 A / 2026-07-11 / Current Review-safe 5190, Candidate 5191 및 본 User manual / 주요 조회 화면·호환 상태·잠금 UX·Candidate 구조 동등성·개인정보 안전 설명·SOP와 rollback 의미 검수 완료 및 PR #28 병합 승인. API 423, DB read-only, console·mobile, runtime·Persistent UAT 보존은 자동 증빙을 함께 사용했다.
