# TASK-AUTH-HARDEN-001 SOP

## 1. 문서 목적

마지막 canonical active System Administrator 보호의 정상 동작을 진단하고 controlled UAT를 안전하게 수행하는 운영 절차다.

## 2. 적용 범위

사용자 비활성화, System Administrator role 제거, 삭제 예약·bulk delete와 purge 방어에 적용한다. 직접 SQL 변경 절차가 아니다.

## 3. Canonical administrator 확인

개인정보 안전 aggregate로 active EntraId, deletion/purge timestamp null, canonical role assignment 조건을 모두 적용한 count만 확인한다. Dev persona와 승인 대기 user는 제외한다.

## 4. 정상 last-admin 오류

마지막 administrator를 비활성화·role 제거·삭제하려 하면 HTTP 400과 업무 message가 반환되는 것이 정상이다. 다른 administrator를 먼저 활성화한 뒤 요청을 다시 수행한다.

## 5. 동시 요청의 정상 결과

마지막 두 administrator에 대한 감소 요청이 동시에 들어오면 한 요청은 commit되고 다른 요청은 최신 count를 확인해 domain error로 거부될 수 있다. 둘 다 성공해 count 0이 되는 결과는 장애다.

## 6. Lock 순서

지원 mutation은 target user row, canonical role row, count, mutation 순서를 사용한다. 운영자가 별도 role-first SQL transaction을 열어 application mutation과 경쟁시키지 않는다.

## 7. Lock wait 진단

Lock wait는 짧은 동시 관리자 mutation에서 발생할 수 있다. Boolean/count와 duration bucket만 기록한다. PID, query, user ID와 SQL 원문은 출력하지 않는다.

## 8. Cancellation·timeout 대응

요청 cancellation이나 DB timeout을 last-admin 오류로 해석하지 않는다. Partial state가 0인지 read-only aggregate로 확인하고, 원인 해소 전 자동 retry나 user/role 강제 수정은 하지 않는다.

## 9. 삭제 예약 전 확인

Canonical active administrator count가 2 이상인지 확인한다. 대상이 실제 System Administrator인지 여부와 별개로 application endpoint만 사용하고 direct SQL을 금지한다.

## 10. Bulk delete

Bulk delete는 항목별 기존 transaction을 사용한다. 각 administrator 감소 항목은 같은 guard를 통과한다. 실패 item을 강제 성공으로 바꾸거나 DB에서 직접 제외하지 않는다.

## 11. 즉시 purge 전 확인

정상 삭제 예정 row는 canonical set 밖이어야 한다. Active administrator가 purge 대상으로 보이거나 count 0이 예상되면 purge worker를 중단하고 데이터 무결성 Finding으로 보고한다.

## 12. Background purge

Due purge는 stable target ordering과 기존 batch transaction을 사용한다. Last-admin guard rejection이나 DB 오류가 있으면 batch rollback을 확인하고 row를 임의 삭제하지 않는다.

## 13. Rollback 원칙

Code rollback은 guard와 두 store를 같은 승인 release로 되돌린다. Migration/data rollback은 없다. 이미 성공한 user/role mutation은 별도 업무 승인 없이 복원하지 않는다.

## 14. Controlled UAT 사전 gate

- PR merge와 사용자 승인
- Persistent ledger compatible
- canonical active administrator count 확인
- Pending/Processing과 runtime health 확인
- Development/Review-safe ownership과 rollback runtime 확보
- 실제 관리자 mutation 대상·순서의 별도 사용자 승인

## 15. Controlled UAT 절차

1. Read-only before snapshot을 fixed projection으로 기록한다.
2. 실제 mutation 없이 last-admin rejection API를 검증할 수 없으면 실행하지 않는다.
3. 승인된 synthetic/controlled Entra administrator pair에서만 서로 다른 감소 요청을 동시에 수행한다.
4. 성공 1 이하, rejection 1 이상, committed active count 1 이상을 확인한다.
5. Authorization·approval pending·Dev persona와 Review-safe 상태를 확인한다.
6. Read-only after snapshot과 before를 비교한다.

실제 UAT user/role mutation은 별도 명시 승인 없이는 수행하지 않는다.

## 16. 장애 판정

다음은 즉시 중단 조건이다.

- committed active administrator count 0
- 두 경쟁 감소 요청 모두 성공
- partial user/role/deletion state
- unexpected deadlock 반복
- HTTP 400 shape 변경
- Persistent UAT·runtime의 설명되지 않는 변화

## 17. 개인정보 안전 출력

Boolean, integer, fixed enum과 route alias만 기록한다. 실제 이름, email, user/role ID, raw DB row, API body, SQL과 lock key는 출력하지 않는다.

## 18. 금지사항

- Persistent user/role direct SQL 변경
- 마지막 administrator 강제 비활성화·삭제
- lock 해제를 위한 backend/PostgreSQL 강제 종료
- DB restart, drop, truncate, volume 삭제
- 사용자 승인 없는 retry·restore·role 보정
- 사용자 검수 전 Ready 전환·merge

## 19. 사용자 검수 체크리스트

- [x] last-admin 오류의 업무 의미 확인
- [x] 경쟁 요청 중 하나가 거부되는 정상 결과 확인
- [x] canonical predicate와 Dev/승인 대기 제외 확인
- [x] HTTP 400과 화면 변경 없음 확인
- [x] direct SQL 금지 이해
- [x] controlled UAT 별도 승인 이해

## 20. 변경 이력

| 일자 | 변경 |
| --- | --- |
| 2026-07-13 | TASK-AUTH-HARDEN-001 구현·자동 검증 및 사용자 검수 대기 절차 작성 |
| 2026-07-13 | 사용자 검수 완료와 PR #36 squash merge 승인 반영 |
