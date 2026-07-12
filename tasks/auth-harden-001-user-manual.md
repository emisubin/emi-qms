# TASK-AUTH-HARDEN-001 User Manual

## 1. 무엇을 보호하나요?

시스템을 관리할 수 있는 active System Administrator가 실수나 동시 요청으로 모두 없어지는 상황을 막는다.

## 2. 왜 필요한가요?

관리자 두 명을 거의 동시에 비활성화하거나 역할 제거·삭제하면, 기존 사전 확인만으로는 두 요청이 모두 성공할 가능성이 있었다.

## 3. 화면이 바뀌나요?

아니다. 사용자 관리 화면과 API response 구조는 그대로다.

## 4. 어떤 요청이 보호되나요?

- 사용자 비활성화
- System Administrator role 제거
- 삭제 예약과 bulk delete
- 즉시·자동 purge의 방어 경로

## 5. 동시 변경 시 무엇이 보이나요?

안전하게 처리할 수 있는 한 요청만 성공하고, 나머지 요청에는 마지막 System Administrator를 변경할 수 없다는 안내가 표시될 수 있다.

## 6. 오류가 나오면 어떻게 하나요?

먼저 다른 active Entra 사용자를 System Administrator로 지정하고 저장이 완료된 것을 확인한 뒤 기존 관리자를 변경한다.

## 7. 승인 대기 사용자는 관리자로 계산되나요?

아니다. Active Entra 사용자이며 실제 System Administrator role이 있고 삭제·purge 상태가 아닌 사용자만 계산한다.

## 8. Dev persona는 계산되나요?

아니다. Development 검수 persona는 read-only 정책을 유지하며 canonical administrator count에서 제외한다.

## 9. 삭제 예정 복구 정책이 바뀌나요?

아니다. 기존 삭제 예약, 보류, purge와 복구 일정은 그대로다.

## 10. 인증 정책이 바뀌나요?

아니다. Microsoft Entra 로그인, 승인 대기, bootstrap과 권한 정책은 변경하지 않는다.

## 11. 동시에 한 요청이 거부되면 데이터가 일부만 바뀌나요?

아니다. 사용자, role과 삭제 상태는 같은 database transaction에서 commit되거나 모두 rollback된다.

## 12. Database migration이 필요한가요?

아니다. 기존 canonical System Administrator role row를 lock으로 재사용한다.

## 13. 사용자가 해야 할 작업

화면 설정 변경은 없다. 관리자 교체 시 새 관리자를 먼저 활성화하고 role 저장 완료를 확인한다.

## 14. 운영자에게 문의할 상황

- active System Administrator가 있는데도 계속 last-admin 오류가 표시됨
- 두 동시 감소 요청이 모두 성공한 것으로 보임
- 사용자 active 상태와 role 표시가 서로 맞지 않음
- lock timeout이나 일반 server 오류가 반복됨

## 15. 제한사항

Application의 지원 화면/API를 보호한다. DBA direct SQL을 허용하는 기능이 아니며 user/role 직접 SQL 변경은 금지한다.

## 16. Persistent UAT 적용 상태

현재 Draft PR은 code와 isolated 자동 검증 단계다. Persistent UAT user/role data와 runtime은 변경하지 않았다. Controlled UAT는 별도 승인이 필요하다.

## 17. FAQ

### 동시에 새 관리자를 추가하고 기존 관리자를 제거할 수 있나요?

가능하지만 commit 순서에 따라 기존 관리자 제거가 먼저 거부될 수 있다. 모든 성공 commit 뒤 최소 한 명은 남는다.

### 오류가 나면 자동으로 다시 시도하나요?

아니다. 새 retry 정책은 추가하지 않았다. 최신 관리자 상태를 확인한 뒤 사용자가 다시 결정한다.

### 실제 UAT 관리자 데이터를 변경했나요?

아니다. Isolated synthetic PostgreSQL에서만 mutation test를 수행했다.

## 18. 사용자 검수 체크리스트

- [ ] 화면 변경 없음 확인
- [ ] 마지막 administrator 보호 목적 이해
- [ ] 동시 변경 시 한 요청이 거부될 수 있음을 이해
- [ ] 새 administrator를 먼저 지정하는 절차 확인
- [ ] 승인 대기·Dev persona 정책 불변 확인
- [ ] transaction rollback 의미 이해
- [ ] direct SQL 금지 이해
- [ ] Persistent UAT controlled 적용이 별도 승인임을 확인

현재 상태: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기.
