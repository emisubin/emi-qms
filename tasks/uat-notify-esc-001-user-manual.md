# TASK-UAT-NOTIFY-ESC-001 User Manual

## 1. 무엇이 적용됐나요?

에스컬레이션 대상이 많아도 앞의 업무만 반복 확인하지 않고 뒤쪽 업무까지 공정하게 순환 평가하는 최신 서버가 Development UAT에 적용됐습니다. 후보 하나에서 오류가 나도 같은 poll의 다른 후보 평가를 계속합니다.

## 2. 업무 규칙도 바뀌었나요?

아닙니다. L0~L3 날짜, 영업일 계산, 수신자와 중복 방지 정책은 그대로입니다. 화면, API와 DB schema도 바뀌지 않았습니다.

## 3. 어떻게 안전하게 적용했나요?

먼저 읽기 전용 forecast로 현재 처리 후보가 0인지 확인했습니다. 이어서 actual provider와 다른 mutation worker를 끈 임시 서버에서 poll 2회를 검증했습니다. 마지막으로 공식 Development backend를 먼저 관찰하고, 안전 gate 통과 후 frontend를 열어 추가 poll을 확인했습니다.

## 4. 현재 환경

- Development: 5174/5081 정상
- Review-safe: 5190/5092 정상, 읽기 전용
- Preview 5185: maintenance 격리로 중지
- Candidate와 backup: 보존

## 5. Worker 상태

Development에서는 escalation·delivery·purge worker가 정상 정책으로 각 1개 실행됩니다. Review-safe에서는 mutation worker와 provider가 꺼져 있습니다.

## 6. 실제 알림이 발송됐나요?

아닙니다. Provider 설정은 정상 운영 검증을 위해 복원했지만 후보, Pending, Processing이 모두 0이었고 실제 provider 호출도 0이었습니다. 새로운 테스트 알림은 만들지 않았습니다.

## 7. 화면에서 확인할 항목

1. Development 5174 접속
2. 프로젝트·내 업무·알림·관리자 조회
3. 발송 모니터의 Processing 표시와 attempt 이력 영역
4. 에스컬레이션 모니터 조회
5. desktop과 좁은 Teams pane에서 가로 넘침이 없는지 확인

저장, 수정, 완료, 수동 발송, retry와 exclude는 검수 중 실행하지 않습니다.

## 8. Live candidate 0의 의미

이번 Persistent UAT 검증 시점에는 실제 에스컬레이션 후보가 없어서 no-op poll을 검증했습니다. 대량 후보의 공정 순환은 별도의 isolated DB에서 101/200/201건 시나리오로 이미 검증했습니다.

## 9. 데이터 보호

Ledger `28/29/1`, Pending/Processing `0/0`, 핵심 aggregate와 timestamp가 유지됐습니다. PostgreSQL은 재시작하지 않았고 backup도 restore·삭제·덮어쓰지 않았습니다.

## 10. 전달 보장 수준

외부 전달은 at-least-once입니다. Provider 성공 직후 DB 반영 전에 장애가 발생하면 재전달 가능성이 있으므로 exactly-once라고 보장하지 않습니다.

## 11. 문제가 보이면

예상하지 않은 알림, Pending/Processing, 중복, 화면 오류 또는 Review-safe 장애가 보이면 mutation을 수행하지 말고 운영자에게 알립니다. 운영자는 Development만 통제 종료하고 Review-safe를 유지합니다. DB row 조정과 backup restore는 별도 승인 대상입니다.

## 12. 사용자 검수 체크리스트

- [ ] Development 5174의 주요 조회 화면이 정상이다.
- [ ] Review-safe 5190이 정상이다.
- [ ] Processing/attempt 표시를 확인했다.
- [ ] desktop과 390px 화면에 가로 넘침이 없다.
- [ ] 실제 provider 호출이 0임을 확인했다.
- [ ] Persistent UAT와 backup이 보호됐음을 이해했다.
- [ ] live candidate 0 검증 제한을 이해했다.
- [ ] at-least-once 제한을 이해했다.
- [ ] Preview 5185가 maintenance 격리로 DOWN임을 이해했다.
- [ ] 다음 코드 P2가 TASK-AUTH-HARDEN-001임을 확인했다.
