# TASK-UAT-NOTIFY-ESC-001: Escalation 공정 순환 Persistent UAT 통제 적용

## 1. 목적

PR #34로 병합된 에스컬레이션 공정 순환과 후보별 오류 격리를 Persistent UAT에서 통제 검증하고 최신 main Development runtime에 적용한다.

## 2. 배경

`TASK-NOTIFY-ESC-001`은 첫 100건 고정 window가 뒤쪽 후보를 굶기는 문제와 후보 한 건의 오류가 poll 전체를 중단하는 문제를 코드에서 해결했다. 이 Task는 해당 코드를 실제 Persistent UAT에 연결하되, 후보가 0인 안전한 시점에 worker 등록·poll·provider 차단 및 runtime 전환을 검증한다.

## 3. 선행조건

- `TASK-NOTIFY-ESC-001` 사용자 검수와 PR #34 병합 완료
- latest main과 전용 Task/runtime worktree 일치
- Persistent ledger canonical/live/approved legacy `28/29/1`
- Pending/Processing `0/0`, active escalation `0`
- L0/L1/L2/L3와 신규 escalation·notification·delivery 후보 모두 `0`
- Review-safe 5190/5092 정상, PostgreSQL restart count `0`

## 4. 포함 범위

- Phase A read-only 후보 forecast
- Phase B escalation-only temporary evaluator와 no-op poll 2회
- 소실된 screen session에 대한 exact-process ownership 재검증과 단일 graceful TERM
- Preview 5185 maintenance 격리
- Phase C latest-main Development 5081/5174 통제 활성화
- escalation·delivery·purge worker 정상 정책과 provider configuration 복원
- backend 단독 poll 2회, frontend 공개 후 추가 poll 1회
- 개인정보 안전 desktop/390px read-only browser smoke
- Persistent aggregate·provider-call-start·backup·runtime 불변 비교

## 5. 제외 범위

- synthetic 업무·예정일·알림 생성
- 실제 테스트 알림 발송 또는 사용자 mutation
- migration·schema·runtime source·API·UI 변경
- Persistent backup restore·삭제·덮어쓰기
- Preview 재기동, Candidate·branch·worktree·backup 정리
- `TASK-AUTH-HARDEN-001` 구현

## 6. Phase A

Persistent UAT를 read-only projection으로 조사했다. eligible L0/L1/L2/L3, 신규 escalation·notification·delivery, Pending, Processing, active escalation이 모두 0이어서 controlled evaluator를 실행할 수 있다고 판정했다.

## 7. Phase B

최신 main detached runtime에서 escalation worker만 활성화하고 delivery·purge·digest·migration·seed·upsert와 실제 provider를 차단했다. 첫 poll과 300초 뒤 두 번째 poll 모두 후보·실패·DB delta·provider-call-start delta가 0이었다.

## 8. Ownership 예외

Development backend의 screen session이 종료 과정에서 소실됐지만 listener process의 연속성, 시작 시각, executable, command, cwd alias, socket ownership, singleton과 다른 runtime 분리를 모두 다시 확인했다. 정확한 기존 process 하나에 SIGTERM을 한 번만 전송했고 30초 이내 graceful 종료됐다. 광범위 종료와 SIGKILL은 사용하지 않았다.

## 9. Phase C

Preview 5185 ownership을 확인한 뒤 maintenance 격리로 종료했다. latest-main backend 5081은 migration·seed·upsert 없이 escalation·delivery·purge worker 각 1개와 canonical provider configuration으로 기동했다. Backend 단독 poll 2회와 frontend 5174 공개 후 추가 poll 1회에서 예상하지 않은 DB 변화, Pending/Processing, provider-call-start와 실제 provider 호출이 모두 0이었다.

## 10. 검증 제한

Live candidate가 0인 시점의 no-op controlled activation이다. 101/200/201 공정 순환, 후보 오류 뒤 tail 진행과 동시 evaluator 중복 0은 `TASK-NOTIFY-ESC-001`의 isolated PostgreSQL 증빙을 재사용한다. 외부 전달 계약은 at-least-once이며 exactly-once가 아니다.

## 11. Persistent UAT 보호

- ledger `28/29/1`, missing/unknown `0/0`
- Pending/Processing `0/0`, active escalation `0`
- 핵심 aggregate와 핵심 timestamp delta `0`
- provider-call-start delta `0`
- PostgreSQL restart count `0`, container와 persistent volume 불변
- fresh backup size·mode·checksum 불변, restore `0`

## 12. Runtime 최종 상태

- Development frontend/backend 5174/5081: UP
- Review-safe frontend/backend 5190/5092: UP, read-only, mutation·worker·provider disabled
- Preview 5185: maintenance 격리로 DOWN
- Notification/Maintenance Candidate: 보존

## 13. Rollback

예상하지 않은 후보, DB delta, provider 호출, worker 중복 또는 Review-safe 장애가 발생하면 exact ownership을 확인한 Development 5174/5081만 graceful 종료하고 Review-safe를 유지한다. 생성된 row를 임의 수정·삭제·제외하지 않으며 backup restore는 별도 승인 없이는 수행하지 않는다.

## 14. 5종 산출물 상태

- Checklist 작성됨
- 자동 검증 완료
- 사용자 검수 완료
- PR #35 squash merge 승인
- 미체크 사용자 검수 항목 0
- 신규 기능 개발 No-Go 유지

## 15. 사용자 검수 체크리스트

- [x] Development 5174 접속과 주요 조회 화면이 정상임을 확인
- [x] Review-safe 5190이 계속 정상임을 확인
- [x] escalation·delivery·purge worker가 정상 정책으로 각 1개임을 이해
- [x] live candidate가 0인 no-op 적용이라는 제한을 이해
- [x] backend 단독 poll 2회와 frontend 이후 poll 1회에서 DB delta가 0임을 확인
- [x] Pending/Processing이 0/0임을 확인
- [x] provider configuration은 복원됐지만 실제 호출은 0임을 확인
- [x] desktop과 390px read-only smoke 결과에 동의
- [x] Preview 5185가 maintenance 격리로 DOWN임을 이해
- [x] Persistent ledger 28/29/1과 PostgreSQL restart 0을 확인
- [x] backup restore·삭제·덮어쓰기가 없었음을 확인
- [x] at-least-once이며 exactly-once가 아님을 이해
- [x] SOP를 반복 실행할 수 있음을 확인
- [x] User manual을 이해
- [x] 다음 코드 P2가 TASK-AUTH-HARDEN-001임을 확인
- [x] 신규 기능 개발 No-Go 유지에 동의
