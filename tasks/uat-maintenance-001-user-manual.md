# TASK-UAT-MAINTENANCE-001 User Manual

## 1. Maintenance worker-disable gate란 무엇인가

서버 점검 중 자동 처리 프로그램이 데이터를 바꾸지 않도록 worker를 시작하지 않는 안전장치다.

## 2. 왜 필요한가

Migration과 runtime 교체 사이에 자동 알림, 에스컬레이션 또는 삭제 처리가 실행되면 전후 상태를 정확히 비교하기 어렵다.

## 3. 알림 worker와 purge worker의 차이

알림 worker는 발송 대기와 기한 알림을 처리한다. Purge worker는 승인된 삭제 예약 시각이 지난 기준정보를 정리한다.

## 4. 기본 상태가 활성인 이유

정상 Development에서는 기존 자동 처리를 유지해야 하므로 purge 기본값은 활성이다.

## 5. 유지보수 중 비활성화하는 이유

점검 구간에는 자동 변경을 0으로 만들어 migration 전후 데이터 비교와 rollback 판단을 명확하게 한다.

## 6. 비활성 상태 확인 방법

운영자는 runtime 상태에서 delivery, escalation, purge worker가 모두 false인지 확인한다.

## 7. 정상 화면과 health 확인 방법

Backend live/ready가 200이고 조회 API가 정상이어야 한다. Worker false는 서버 장애가 아니라 의도한 maintenance 상태다.

## 8. 유지보수 중 데이터가 자동 변경되지 않는 이유

세 worker가 DI에 등록되지 않고 purge worker 자체에도 두 번째 차단이 적용된다.

## 9. Review-safe와 Maintenance Phase A 차이

Review-safe는 화면과 DB까지 읽기 전용이다. Phase A는 최신 Development runtime을 normal DB connection으로 점검하지만 사용자 저장 작업은 하지 않는다.

## 10. 다시 정상 모드로 돌리는 절차

운영자가 Pending/Processing과 due 후보가 안전한지 확인한 뒤 기존 worker 설정으로 single backend를 재기동한다.

## 11. Backup이 다시 필요한 이유

기존 backup 이후 시간이 지날 수 있으므로 실제 migration 직전에 최신 backup을 새로 만들고 restore 가능성을 다시 확인한다.

## 12. 오류 메시지 의미

- option 오류: true 또는 false가 아닌 설정
- worker 상태 불일치: 실제 등록과 화면 상태가 다름
- health 실패: DB 또는 runtime 준비 상태 확인 필요

## 13. 하면 안 되는 작업

Worker를 수동 실행하거나 Persistent UAT를 수정·초기화하지 않는다. Backup을 열거나 삭제하지 않고 실제 외부 알림 smoke를 만들지 않는다.

## 14. FAQ

### Worker가 false이면 화면도 열리지 않나요?

아니다. 조회와 health는 정상 동작한다.

### Purge를 끄면 예약 데이터가 사라지나요?

아니다. 처리만 잠시 멈추고 기존 예약 row는 유지한다.

### 이번 Task에서 migration 0028이 적용되나요?

아니다. HANDOVER-003 재개 후 별도 gate를 통과해야 한다.

### 기존 backup을 바로 사용하나요?

Evidence로 보존하지만 실제 migration 직전 fresh backup을 다시 만든다.

## 15. 사용자 검수 체크리스트

- [x] 기본 purge 활성 의미 이해
- [x] maintenance에서 세 worker false 확인
- [x] due 후보 불변 의미 이해
- [x] Review-safe와 Phase A 차이 이해
- [x] Persistent UAT와 0028이 변하지 않음 확인
- [x] backup 재생성 정책 이해
- [x] 다음 TASK-UAT-HANDOVER-003 확인
- [x] 신규 기능 No-Go 유지 확인

현재 상태: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #31 병합 승인 / 미체크 항목 0.
