# TASK-UAT-HANDOVER-003 — Notification delivery reliability UAT handover

## 1. 목적

Notification delivery claim/lease migration 0028과 maintenance worker gate가 병합된 최신 main을 Persistent UAT에 통제 적용하고 Development·Review-safe runtime을 안전하게 교체한다.

## 2. 배경과 선행 Task

- TASK-NOTIFY-REL-001: atomic claim, lease, fencing, attempt audit, Processing UX
- TASK-UAT-MAINTENANCE-001: purge enable gate와 세 mutation worker의 조건부 DI
- TASK-UAT-HANDOVER-002: 개인정보 안전 Review-safe runtime handover

초기 preflight는 purge worker를 명시적으로 끌 수 없어 중단됐다. Maintenance gate 병합 후 preflight부터 다시 수행했다.

## 3. 포함 범위

- latest main branch/runtime 기준선 정렬
- fresh secure backup과 isolated restore rehearsal
- historical ledger에서 공식 runner migration 0028 rehearsal·fault rollback
- Persistent UAT migration 0028 적용
- Review-safe 5190/5092 최신 main 전환
- Development Phase A all-workers-disabled 검증
- 정상 Development 5081/5174 복구와 장시간 무변경 관찰
- privacy-safe desktop/390px 화면 검증
- 5종 산출물과 Draft PR

## 4. 제외 범위

- Task automation이 만드는 신규 테스트 알림 발송
- backup restore
- migration 또는 runtime source 수정
- escalation starvation 수정
- candidate·backup·worktree 정리
- PR Ready 전환과 merge

## 5. Phase 1 — backup과 rehearsal

Development writer를 통제 종료하고 write session/transaction 0을 확인했다. Repository 밖 secure 위치에 `fresh-pre-0028` custom-format backup을 생성해 directory mode 700, file mode 600, checksum을 확인했다. Isolated tmpfs PostgreSQL restore와 historical ledger migration rehearsal 결과는 canonical/live/legacy 28/29/1이었다. 의도적 ledger 기록 실패 fixture에서는 0028 schema와 marker가 모두 rollback됐다.

## 6. Phase 2A — migration과 Review-safe

공식 `DatabaseMigrationRunner`로 Persistent UAT에 0028을 적용했다. 결과는 canonical 28, live 29, approved legacy 1, missing/unknown 0이다. Claim column 4개, delivery constraint 3개, attempt table·constraint·index를 확인했고 기존 aggregate·delivery status·attempt count·timestamp는 불변이었다.

최신 main Review-safe 5092/5190은 read-only, mutation 423, migration/seed/worker/provider disabled 상태로 기동됐다. Obsolete Review Candidate 5191/5093은 통제 종료했고 Notification Candidate 5192/5094와 Maintenance Candidate 5595는 유지했다.

## 7. Phase A — all workers disabled

Task-owned loopback backend에서 migration·seed·delivery·escalation·purge·digest·provider를 모두 비활성화했다. 두 관찰 구간에서 claim, attempt, escalation, purge와 provider-call-start 변화는 0이었다.

## 8. Phase 2B — 정상 Development 복구

정상 configuration은 delivery worker와 purge worker를 활성화하고 escalation worker는 현재 canonical 설정상 비활성화한다. Escalation starvation은 후속 TASK-NOTIFY-ESC-001 범위이므로 임의 활성화하지 않았다. Dedupe-window 후보는 기존 unique index와 `ON CONFLICT DO NOTHING`을 반영하면 실제 insert 가능 0이었다.

Frontend 없는 임시 backend 10분, 공식 5081 backend 단독 10분, 공식 HTTPS 5174와 함께 확장 관찰을 수행했다. 첫 공식 관찰의 약 35분 시점에 `Manual / ManualTest / TeamsActivity / Pending` 1건이 생성되어 자동 fail-stop이 동작했다. 사용자가 직접 실행한 의도적 수동 발송임을 확인했으므로 `UNEXPECTED_MANUAL_DELIVERY_DELTA`를 `AUTHORIZED_USER_ACTIVITY`로 재분류했다. 제품/runtime isolation 결함과 data cleanup 대상은 아니다.

재개 시 보존된 delivery만 정상 worker 정책으로 처리했다. 단일 claim과 attempt 1건, provider-start와 completion이 같은 lineage에 기록됐고 최종 상태는 Sent였다. Parent notification·delivery 중복, unrelated attempt와 unrelated provider call은 모두 0이다. 기존 공식 runtime 유효 관찰 45분을 인정하되 이전 증빙에 다음 purge interval이 없어서 재기동 후 1시간 interval을 추가 관찰했다.

## 9. Ledger와 schema 상태

- canonical/live/approved legacy: 28/29/1
- latest: `0028_notification_delivery_claim_lease`
- missing canonical / unknown extra: 0/0
- delivery attempt row: migration 직후 0, 승인된 사용자 delivery 처리 후 1
- 외부 전달 보장: at-least-once; exactly-once 아님

## 10. Persistent UAT와 backup 보호

PostgreSQL container restart 없이 기존 volume을 유지했다. Backup은 삭제·덮어쓰기·restore하지 않았고 isolated rehearsal evidence도 보존한다. 0028 적용 이후 down migration 대신 forward-fix를 사용한다.

## 11. Runtime 상태

| 환경 | 상태 | 비고 |
| --- | --- | --- |
| Development 5174/5081 | 최신 main 정상 모드 | HTTPS frontend, normal provider configuration |
| Review-safe 5190/5092 | 정상 | read-only, mutation 423, workers/providers disabled |
| Obsolete Review Candidate 5191/5093 | 종료 | 정리는 별도 승인 |
| Notification Candidate 5192/5094 | 유지 | isolated DB |
| Maintenance Candidate 5595 | 유지 | isolated DB |

## 12. Rollback과 forward-fix

- Migration 전 실패: 기존 ledger를 유지하고 runtime을 기동하지 않는다.
- Migration transaction 실패: marker/schema rollback을 확인한다.
- Migration 성공 후 runtime 실패: Development writer를 차단하고 latest-main forward-fix만 검토한다.
- 데이터 무결성 이상: Review-safe만 유지하며 backup restore는 별도 승인 없이는 수행하지 않는다.

## 13. Findings와 제한사항

- Escalation worker는 현재 normal configuration에서 false이며 starvation remediation 전 임의 활성화하지 않는다.
- Provider 성공 후 DB completion 전 crash에서는 중복 가능성이 남는다.
- 승인된 사용자 발송 1건의 provider call만 허용했으며 Task automation은 신규 발송을 만들지 않았다.
- Backup restore와 임시 자원 정리는 별도 승인 대상이다.

## 14. 5종 산출물 상태

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Task 정의·checklist | 이 문서 | 작성 완료 |
| Implementation report | [implementation report](uat-handover-003-implementation-report.md) | 작성 완료 |
| SOP | [SOP](uat-handover-003-sop.md) | 작성 완료 |
| User manual | [User manual](uat-handover-003-user-manual.md) | 작성 완료 |
| Roadmap update | [Product Roadmap](../docs/00-product-roadmap.md) | 반영 완료 |

현재 상태: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #33 squash merge 승인 / 미체크 항목 0.

## 15. 사용자 검수 체크리스트

- [x] https://localhost:5174 접속 정상
- [x] 프로젝트·내 업무·알림·Teams Activity·관리자 조회 정상
- [x] Processing filter와 발송 시도 이력 접근 가능
- [x] 사용자 수동 발송 1건이 `AUTHORIZED_USER_ACTIVITY`로 기록되고 단일 attempt로 Sent 처리됨을 확인
- [x] Pending·Processing이 0임을 확인
- [x] 승인된 delivery 1건 외 provider 호출이 0임을 확인
- [x] Review-safe https://localhost:5190 정상
- [x] Migration ledger 28/29/1 의미 이해
- [x] Persistent UAT와 backup이 보존됨을 확인
- [x] External delivery가 at-least-once임을 이해
- [x] SOP 실행 가능
- [x] User manual 이해 가능
- [x] 다음 Task가 TASK-NOTIFY-ESC-001임을 확인
- [x] 전체 신규 기능 개발 No-Go 유지 확인
