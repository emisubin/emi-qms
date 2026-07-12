# TASK-UAT-HANDOVER-003 SOP

## 1. 목적과 적용 범위

Notification delivery migration과 최신 runtime을 Persistent UAT에 안전하게 적용하고 반복 검증하는 운영 절차다. 실제 발송, backup restore와 환경 정리는 별도 승인 범위다.

## 2. 환경 구분

- Development: 5174/5081, normal writable runtime
- Review-safe: 5190/5092, read-only와 mutation 423
- Notification Candidate: 5192/5094, isolated DB
- Maintenance Candidate: 5595, isolated DB

## 3. 사전 Git·runtime 확인

Root/Backend/Frontend/Scripts AGENTS, Roadmap, 종료 정책, validation/privacy 문서를 읽는다. main, runtime worktree와 source tree가 일치하고 worktree가 clean인지 확인한다. PID file, listener, cwd, command와 screen ownership을 함께 확인한다.

## 4. Maintenance 진입

Frontend를 먼저 종료하고 backend를 종료한다. Write-capable application session과 진행 중 write transaction을 두 번 확인해 0이 아니면 중단한다. Review-safe와 Persistent PostgreSQL은 종료하지 않는다.

## 5. Fresh backup

Repository 밖 secure directory에 기존 파일과 다른 custom-format backup을 만든다. Directory 700, file 600, non-empty와 SHA-256을 확인한다. 경로·내용·credential은 보고하지 않는다.

## 6. Restore rehearsal

동일 PostgreSQL major의 isolated network/container/tmpfs를 만든다. Fresh backup을 restore하고 ledger·aggregate가 Persistent pre-migration snapshot과 일치하는지 확인한다. Persistent DB에는 restore하지 않는다.

## 7. Historical ledger migration rehearsal

Latest main 공식 runner를 사용한다. Migration만 true로 하고 seed/upsert, delivery/escalation/purge/digest/provider를 false로 둔다. 결과는 canonical/live/legacy 28/29/1, missing/unknown 0이어야 한다.

## 8. Fault rollback rehearsal

Task-owned isolated clone에서 ledger 기록 실패를 주입한다. Runner 실패 후 marker, claim columns와 attempt table이 없어야 하며 기존 aggregate가 같아야 한다. Repository SQL은 수정하지 않는다.

## 9. Live migration

Pending, Processing, digest, escalation candidate, due purge, actual insertable delivery, provider-start와 write transaction이 모두 0일 때만 실행한다. 공식 runner 성공 뒤 executor를 즉시 종료하고 listener/process 0을 확인한다.

## 10. Schema·ledger 확인

- canonical/live/legacy 28/29/1
- missing/unknown 0/0
- claim columns 4
- delivery claim constraints 3
- attempt table, FK·unique·check와 indexes
- migration 전후 aggregate/status/timestamp 불변

## 11. Review-safe 기동

Migration·seed·worker/provider를 false로 두고 latest main 5092/5190을 기동한다. DB read-only, mutation false, 423, readiness 200과 CompatibleWithApprovedLegacy 28/29/1을 확인한다.

## 12. Development Phase A

Frontend 없는 loopback backend에서 세 mutation worker, digest/provider, migration/seed를 false로 둔다. 두 observation interval에서 claim/attempt/escalation/purge/provider 변화가 0인지 확인하고 종료한다.

## 13. Normal configuration gate

Normal provider 설정값은 출력하지 않고 configured/missing boolean만 확인한다. Dedupe-window candidate만 보지 말고 DB unique indexes와 `ON CONFLICT DO NOTHING`을 반영한 실제 insert 가능 count가 0인지 확인한다.

Canonical configuration에서 delivery와 purge는 true, escalation은 false다. 후속 remediation 전 escalation을 임의 true로 만들지 않는다.

## 14. 임시 Phase B

Frontend 없는 loopback backend에 normal configuration을 주입하되 migration/seed는 false로 고정한다. 10분 동안 Pending/Processing/Failed, attempts/provider-start, notification/delivery/escalation/purge audit과 core/timestamp delta를 검사한다. 이상 시 즉시 임시 backend만 종료한다.

## 15. 공식 Development 복구

Latest main backend를 5081에 single instance로 기동해 10분 단독 관찰한다. 통과 후 HTTPS strict-port frontend 5174를 5081 proxy로 기동한다. 일반 start script가 migration/master upsert/seed를 수행하므로 handover 중에는 backend/frontend를 직접 기동한다.

## 16. Browser 검증

Desktop과 390px에서 fixed route alias를 사용한다. Status, structure, Processing label, attempt history, blank/target-not-found, console, non-aborted request와 overflow만 boolean/count로 출력한다. DOM/text/screenshot/API body는 출력하지 않는다.

## 17. 확장 관찰

최소 65분 동안 30초 간격으로 확인한다. Purge 다음 1시간 interval을 포함한다.

- backend/frontend/review-safe health
- listener PID 불변과 중복 0
- Pending/Processing/Failed
- attempts/provider-start
- notification/delivery/escalation/purge audit
- core/status/timestamp digest
- PostgreSQL health/restart

Delta가 발생하면 Development frontend와 backend만 ownership 확인 후 종료하고 Review-safe를 유지한다.

사용자가 관찰 중 mutation을 수행했다고 명시적으로 확인한 경우에도 행을 임의 수정하지 않는다. 먼저 source kind, delivery type/channel/status, attempt·claim과 parent/delivery duplicate를 boolean/count/fixed enum으로 분류한다. 정확히 승인된 단일 활동이고 제품/runtime 결함이 아니며 unrelated delta가 0일 때만 `AUTHORIZED_USER_ACTIVITY`로 기록하고 정상 worker lineage 처리를 별도 승인받아 재개한다.

유효 관찰 시간은 temporary configuration 증빙과 공식 runtime 안정성 증빙을 분리한다. 공식 backend/frontend에서 모든 gate가 통과한 구간만 안정성 시간으로 인정한다. 전체 시간을 처음부터 반복할 필요가 없다는 승인이 있어도 다음 purge interval 증빙이 없으면 재개한 runtime에서 그 interval 1회를 추가 관찰한다.

## 18. Rollback과 장애 대응

- Runtime 실패: Development만 종료, Review-safe 유지
- Migration 실패: transaction rollback 확인
- Migration 성공 후 schema/data 이상: writer 차단, restore 금지, 사용자 결정 요청
- 승인되지 않은 provider-start 발생: Development 즉시 종료, row 처리·retry 금지
- 승인된 사용자 delivery가 정상 retry 뒤 Failed: Development 종료, 행 수정 없이 보고

## 19. 개인정보 안전 출력

Boolean, count, fixed enum, route alias와 digest만 허용한다. 사용자·프로젝트·알림·recipient·row ID, credential, raw DB/API/DOM/console과 GitHub 개인 metadata는 출력하지 않는다.

## 20. 금지사항

- Persistent DB reset/drop/truncate 또는 PostgreSQL restart
- Approved legacy marker 변경
- Migration SQL 수정
- Task automation 신규 테스트 알림 발송
- Backup 삭제·덮어쓰기·restore
- Candidate/worktree 정리
- 사용자 승인 전 PR Ready/merge

## 21. 사용자 검수 체크리스트

- [x] Development 5174/5081 정상
- [x] Review-safe 5190/5092 정상
- [x] Processing/attempt UI 이해 가능
- [x] 승인된 사용자 활동의 fail-stop·재분류·단일 Sent lineage 확인
- [x] Ledger 28/29/1 확인
- [x] Pending/Processing과 unrelated provider call 0 확인
- [x] Backup과 candidate 보존 확인
- [x] At-least-once 제한 이해
- [x] 다음 TASK-NOTIFY-ESC-001 순서 확인

현재 상태: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #33 squash merge 승인 / 미체크 항목 0.

## 22. 변경 이력

| 일자 | 변경 |
| --- | --- |
| 2026-07-12 | Phase 1·2A·2B controlled handover 절차 최초 작성 |
| 2026-07-12 | 사용자 승인 활동 분류, fail-stop 재개와 유효 관찰 합산 규칙 추가 |
| 2026-07-12 | 사용자 검수 완료와 PR #33 squash merge 승인 반영 |
