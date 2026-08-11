# TASK-NOTIFY-POLICY-001 Change 003 — Full-Stack 정책 기대값 동기화

- changeType: `P2_REMEDIATION`
- changeDate: `2026-08-12`
- changeSource: `PR_CI_FINDING`
- sourcePullRequest: `99`
- productBehaviorChanged: `false`
- implementationApprovedByExistingPublicationScope: `true`

## 확인된 원인

PR의 첫 Full-Stack 실행은 `56/60`을 통과하고 기존 회귀 네 곳에서 실패했다. 네 실패 모두 제품 동작은 승인된 알림 정책대로였지만 테스트가 폐기된 정책을 계속 기대한 기준 drift였다.

1. 제조 업무 알림은 새 operation 기반 idempotency key를 사용하지만 기존 테스트가 과거 kitting notification key를 조회했다.
2. Pending은 Teams 공용 채널 신규 생성을 중단하고 개인 Teams Activity와 메일을 사용하지만 기존 테스트가 `TeamsChannel` 수량을 기대했다.
3. 프로젝트 생성은 모든 활성 사용자를 수신자로 하지만 기존 테스트가 System Administrator와 Viewer를 제외했다.
4. 18단계 영업 최종 완료는 사용자 인앱 수신자 없이 활성 영업부서 전체 메일 전용이지만 기존 테스트가 프로젝트 전체 외부 delivery 0건을 기대했다.
5. 제조 중단 테스트는 목록 첫 번째인 `administration`을 선택했는데, 신규 정책상 현장 업무를 대체할 부서장이 없어 정상적으로 생성이 차단됐다.

## 보정 범위

- 제품 Backend·Frontend 동작과 migration은 변경하지 않는다.
- 제조 알림은 현재 work item과 `WorkAssignment` 원본으로 식별한다.
- Pending 외부 채널 기대값은 `TeamsActivity`와 `Mail`로 맞춘다.
- 프로젝트 생성 알림은 모든 활성 개발 persona에게 표시되는지 확인한다.
- 18단계는 인앱 recipient 0명, Mail 이외 delivery 0건, 활성 영업부서 사용자 수만큼 Mail delivery가 생성되는지 확인한다.
- 제조 중단은 목록 순서에 의존하지 않고 활성 부서장이 있는 `quality` 부서를 명시적으로 선택한다. 요청 완료는 팝업 종료와 실제 Pending 링크 출현으로 판단한다.

## 검증 Gate

- 네 실패 spec의 집중 Full-Stack 검증을 통과한다.
- PR 최신 head의 전체 Full-Stack과 필수 `CI Gate`가 성공해야 병합한다.
- Open P0/P1/P2는 `0/0/0`이어야 한다.

## 집중 재검증

- 제조 작업 Full-Stack spec: `2/2 PASS` (`2026-08-12`)
- 제품 Backend·Frontend 동작 변경: 없음
