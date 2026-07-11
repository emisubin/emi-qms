# TASK-UAT-MAINTENANCE-001 SOP

## 1. 문서 목적

Migration handover 전 mutation worker를 명시적으로 정지하고 상태를 반복 검증하는 절차다.

## 2. Mutation worker 종류

Delivery는 claim/provider/delivery 상태, Escalation은 due workflow 상태, Purge는 삭제 lifecycle row를 변경한다.

## 3. Delivery worker gate

`Notifications__Dispatch__Enabled=false`이면 worker가 등록되지 않는다.

## 4. Escalation worker gate

`Notifications__Escalation__Enabled=false`이면 worker가 등록되지 않는다.

## 5. Purge worker gate

`AdminDeletionPurge__Enabled=false`이면 worker가 등록되지 않으며 direct worker도 purge service를 호출하지 않는다.

## 6. 기본 활성 상태

Purge 기본값은 true다. 정상 Development에서는 기존 즉시 실행과 1시간 interval을 유지한다.

## 7. Maintenance Phase A 설정

Application migration과 seed를 false로 두고 세 worker flag, Daily Digest와 실제 provider channel을 false로 명시한다. `.env.notify-local`은 로드하지 않는다.

## 8. Runtime status 확인

`/api/runtime-mode`에서 delivery, escalation, purge와 aggregate worker boolean이 모두 false인지 projection한다. 응답 전체를 출력하지 않는다.

## 9. Worker registration 확인

Hosted service inventory에 세 worker type이 없어야 한다. 등록 후 idle 상태는 Phase A 성공으로 간주하지 않는다.

## 10. Two-interval observation

Worker 미등록을 먼저 확인한 후 두 개의 고정 observation interval에서 aggregate snapshot을 비교한다. Row 원문은 출력하지 않는다.

## 11. Due candidate 불변 확인

Isolated synthetic due delivery, escalation, purge candidate의 count와 상태가 동일해야 한다. Claim, attempt, escalation 생성과 delete는 0이어야 한다.

## 12. Phase A 종료

Handover gate가 끝나기 전 Phase A backend를 정상 worker 설정으로 임의 전환하지 않는다.

## 13. 정상 Development 복구

Ownership을 확인한 뒤 기존 운영 option을 복원하고 single backend로 재기동한다. Purge true, delivery/escalation은 승인된 기존 설정을 사용한다.

## 14. ReviewSafe와 차이

ReviewSafe는 worker뿐 아니라 mutation API와 DB write도 차단한다. Phase A는 normal DB connection을 사용하므로 사용자 mutation을 maintenance 절차로 별도 차단한다.

## 15. Persistent UAT 적용 전 주의

이 SOP만으로 migration을 적용하지 않는다. HANDOVER-003의 backup, restore rehearsal, Pending/Processing/due gate가 모두 필요하다.

## 16. Backup 재생성 원칙

기존 backup은 evidence로 보존하고 migration 직전에 mode 600의 fresh custom backup을 생성해 isolated restore를 다시 검증한다.

## 17. 장애 대응

- worker projection/DI 불일치: startup 중단
- false인데 query 발생: Phase A 실패, backend 종료
- malformed option: 설정을 true 또는 false로 명시
- Persistent UAT 변화: handover 재개 금지

## 18. Rollback

이 코드의 rollback은 commit revert/forward-fix다. Persistent DB restore는 이 Task에서 수행하지 않는다.

## 19. 개인정보 안전 출력

Boolean, count, status code와 route alias만 기록한다. 사용자·업무·알림·row·credential 원문은 출력하지 않는다.

## 20. 금지사항

- Persistent UAT migration/write/restart
- 기존 runtime restart
- backup 삭제/열람/restore
- 실제 provider 호출
- worker false 상태를 등록된 idle worker로 대체

## 21. 사용자 검수 체크리스트

- [x] purge 기본 true 이해
- [x] false 시 DI 미등록과 query 0 확인
- [x] ReviewSafe에서 세 worker 미등록 확인
- [x] Phase A projection false 확인
- [x] due synthetic 후보 불변 확인
- [x] Persistent UAT와 기존 runtime 무변경 확인
- [x] backup 보존/fresh backup 정책 이해
- [x] HANDOVER-003 재개 순서 이해
- [x] 신규 기능 No-Go 확인

현재 상태: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #31 병합 승인 / 미체크 항목 0.

## 22. 변경 이력

| 일자 | 변경 |
| --- | --- |
| 2026-07-11 | 최초 작성, purge gate와 Phase A 절차 반영 |
| 2026-07-11 | 사용자 검수 완료와 PR #31 squash merge 승인 반영 |
