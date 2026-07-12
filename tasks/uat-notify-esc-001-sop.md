# TASK-UAT-NOTIFY-ESC-001 SOP

## 1. 목적

Escalation fair-ordering 코드를 Persistent UAT에 안전하게 적용하고 Development runtime을 통제 활성화하는 반복 실행 절차다.

## 2. 사전 조건

1. Repository instruction chain과 validation/privacy 문서를 읽는다.
2. main과 전용 runtime source가 일치하고 worktree가 clean인지 확인한다.
3. Review-safe live/ready, PostgreSQL health/restart와 backup checksum을 확인한다.
4. Pending/Processing, active escalation, eligible L0~L3, 신규 escalation·notification·delivery, due purge·digest·즉시 delivery, unknown writer와 write transaction이 모두 0인지 확인한다.

하나라도 0이 아니면 worker를 활성화하지 않는다.

## 3. Phase A: read-only forecast

- `BusinessDayCalculator`와 canonical L0~L3 정책을 사용한다.
- recipient/channel forecast는 count만 출력한다.
- DB unique와 `ON CONFLICT` 계약을 모두 반영한 실제 insert 가능 수를 사용한다.
- 실제 row·식별자·제목·수신자를 출력하지 않는다.

## 4. Phase B: temporary evaluator

1. Development frontend/backend ownership을 확인하고 정확한 process만 graceful 종료한다.
2. writer session과 active write transaction이 0인지 두 번 확인한다.
3. 최신 main loopback backend에서 escalation만 enabled로 기동한다.
4. Delivery, purge, digest, migration, seed, upsert와 actual provider를 disabled로 둔다.
5. 첫 poll과 300초 뒤 두 번째 poll을 관찰한다.
6. candidate, failure, escalation/notification/recipient/delivery/attempt, provider-call-start delta가 모두 0인지 확인한다.
7. evaluator ownership을 확인하고 graceful 종료한다.

## 5. Ownership 예외

Session이 소실되면 즉시 fail-closed한다. 사용자 예외 승인 후에만 최초 PID continuity, start time, executable/command type, cwd alias, socket ownership, singleton과 타 runtime 분리를 모두 확인한다. 정확한 process에 SIGTERM 한 번만 사용하고 30초 안에 종료되지 않으면 추가 신호 없이 중단한다.

## 6. Preview maintenance 격리

5185의 listener, command type, cwd alias, session ancestry, process continuity와 타 runtime 분리를 확인한다. 모두 일치할 때만 graceful 종료한다. 이 Task에서는 Preview를 재기동하지 않는다.

## 7. Phase C: backend

직접 latest-main binary를 사용한다. 일반 startup script처럼 migration·seed·upsert가 포함될 수 있는 경로는 사용하지 않는다.

- escalation/delivery/purge: enabled
- digest/migration/seed: disabled
- startup master upsert: 미사용
- providers: canonical Development configuration
- `.env.notify-local`: allowlist literal parser, source/eval 금지

Listener 1, backend 1, worker 각 1, live/ready 200, duplicate 0을 확인한다.

## 8. Backend 단독 관찰

Frontend와 Preview가 내려간 상태로 첫 escalation poll과 300초 이후 두 번째 poll을 관찰한다. Candidate/failure/DB/provider/purge delta와 worker error가 모두 0이어야 한다.

## 9. Phase C: frontend

Backend-only gate 통과 후 HTTPS strict-port 5174를 5081 proxy로 기동한다. 주요 route는 GET/read-only로만 확인한다. 저장·수정·완료·수동 발송·retry·exclude를 수행하지 않는다.

## 10. Browser 검증

Desktop과 390px에서 route load, main/navigation structure, blank page, page overflow와 console error를 boolean/count로 확인한다. Raw DOM, innerText, API body, console message와 screenshot을 출력하지 않는다.

## 11. 추가 poll과 최종 비교

Frontend 공개 후 escalation poll 1회를 더 관찰한다. Baseline과 다음을 비교한다.

- ledger와 핵심 table count
- notification/delivery/attempt/escalation/audit max timestamp
- Pending/Processing와 active escalation
- provider-call-start
- PostgreSQL container/volume/restart
- backup size/mode/checksum
- Review-safe health

## 12. 성공 기준

Poll 3회 모두 candidate/failure/DB/provider delta 0, worker 각 1, Development·Review-safe 정상, PostgreSQL restart 0, backup 불변이어야 한다.

## 13. 중단과 rollback

예상하지 않은 변화가 있으면 exact ownership을 확인한 5174/5081을 graceful 종료한다. Review-safe를 유지하고 Preview는 임의 재기동하지 않는다. DB row를 수정·삭제·제외하거나 backup을 restore하지 않는다.

## 14. 개인정보 안전 출력

허용: boolean, integer, fixed enum, route alias, 상태 코드, count.

금지: credential·URL·수신자·사용자·프로젝트·업무·알림 원문, UUID/row, raw DB/API/DOM/log, screenshot, GitHub 개인 metadata.

## 15. 사용자 검수

- Development와 Review-safe 접속
- worker effective 상태
- live candidate 0 제한
- DB/provider delta 0
- desktop/390px smoke
- Preview DOWN 정책
- backup restore 0
- at-least-once 제한

## 16. 변경 이력

- 2026-07-13: Phase A/B/C controlled activation 절차 최초 작성
