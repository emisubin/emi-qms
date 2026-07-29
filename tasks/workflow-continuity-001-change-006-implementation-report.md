# TASK-WORKFLOW-CONTINUITY-001 Change 006 구현 보고 — 품질 Pending 해제 원자성

## 상태 전이

1. 최초 부적합 판정은 검사 회차 `Failed`와 Pending 생성·담당 업무를 같은 transaction에서 기록한다.
2. Pending 조치 완료는 Pending을 닫지 않고 품질 재검사 회차·내 업무·알림을 만든다.
3. 품질 재검사가 적합이면 재검사 회차 `Passed`, 연결 Pending `Closed`, 차단 업무 종결과 다음 담당자 인계를 같은 transaction에서 수행한다.
4. 다시 부적합이면 새 Pending을 만들지 않고 같은 Pending을 `InProgress`로 되돌려 담당 업무·알림을 재개한다.

## 회귀 검증

- IQC 도착분 엔진과 패널 품질 엔진을 각각 isolated Full-Stack E2E로 검증했다.
- 패널 품질 E2E는 Pending 수가 처음부터 끝까지 `1`인지 확인해 중복 생성을 차단했다.
- 재검사 시작자·시각 누락으로 적합 확정이 실패하던 root cause를 수정하고 동일 시나리오를 재실행해 통과했다.

## Finding·경계

- Open P0/P1/P2: `0/0/0`.
- 조치 완료만으로 Pending을 임의 종결하지 않는 기존 사용자 확정 정책을 유지한다.
- 실제 Teams·Mail provider는 호출하지 않고 격리 outbox 계약만 보존한다.
