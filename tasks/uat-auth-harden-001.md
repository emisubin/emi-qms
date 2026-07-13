# TASK-UAT-AUTH-HARDEN-001 — Last administrator controlled UAT

## 1. 상태

- Task 유형: `UAT_RUNTIME`
- 기획 경로: Codex-only
- 기준 main: `41ce6047ced2`
- Phase A/B: 자동 검증 완료
- Phase C/D: runtime 적용·자동 검증 완료
- 사용자 검수: 완료
- PR #40: Ready 전환·squash merge 승인
- Persistent live user/role/deletion mutation: `NO_GO`
- 신규 기능 개발: 남은 P2 Gate가 있어 `NO_GO`

## 2. 목적

PR #36과 PR #37로 병합된 마지막 active System Administrator 동시성 보호를 실제 HTTP와 PostgreSQL transaction 경계에서 재검증하고, Persistent identity mutation 없이 최신 main Development runtime에 적용한다.

## 3. 보호 불변조건

Canonical active System Administrator는 active EntraId 사용자이면서 deletion requested, scheduled hard delete와 purge blocked marker가 없고 canonical System Administrator role assignment가 존재하는 사용자다.

- 모든 성공 commit 뒤 canonical active administrator 수는 1명 이상이어야 한다.
- 성공 mutation은 완전 commit되고 거부·실패 mutation은 완전 rollback돼야 한다.
- 서로 다른 target의 감소 요청도 canonical role row serialization boundary를 공유해야 한다.
- 정상 삭제 lifecycle의 authoritative 감소 boundary는 삭제 예약 transaction이다.
- Purge 전용 predicate는 malformed lifecycle state의 physical delete를 막는 defense-in-depth다.
- 외부 공개 오류는 기존 HTTP 400 단일 `message` shape를 유지한다.
- Direct SQL은 application 보호 범위 밖이며 운영상 금지한다.

## 4. 포함 범위

- Privacy-safe Collector·Aggregator·Projector qualification
- Persistent UAT read-only identity snapshot
- Task-owned PostgreSQL과 synthetic identity의 실제 HTTP 경쟁 검증
- Cancellation·failure·immediate purge·due purge·20회 stress
- Temporary latest-main ReviewSafe backend
- Official latest-main Development 5081/5174 handover
- 정상 escalation·delivery·purge worker와 provider configuration 복원
- Desktop·390px read-only browser smoke
- Persistent 전후 digest와 aggregate 비교
- 5종 산출물과 사용자 검수 완료 Draft PR

## 5. 제외 범위

- Persistent UAT 사용자 비활성화, role 제거, 삭제 예약 또는 purge
- 임시 administrator·break-glass·bootstrap 실행
- Direct SQL, backup restore와 Persistent 데이터 보정
- Migration·schema·API·Frontend source 변경
- Review-safe 5190/5092 교체 또는 재시작
- Preview·Candidate·backup·기존 branch/worktree 정리
- 사용자 승인 전 PR Ready 전환과 merge
- 다음 P2 Task 실행

## 6. 실행 전 기준선

| 항목 | 기준선 |
| --- | --- |
| Persistent ledger | canonical/live/approved legacy `28/29/1` |
| Canonical role | 1 |
| Canonical active Entra administrator | 1 |
| Pending/Processing | `0/0` |
| PostgreSQL | healthy, restart 0 |
| Development source | PR #36·#37 이전 source |
| Review-safe | 5190/5092 healthy, read-only |
| Proven break-glass administrator | false |

## 7. Phase A — Evidence와 Persistent baseline

- Multi-file Collector·Aggregator·Projector qualification: PASS
- Release build end-to-end projection: PASS
- Filename/path/raw content projection: 0
- Persistent identity digest와 관리자 aggregate를 원문 식별자 없이 고정 projection으로 확보
- Persistent write transaction과 unknown writer: `0/0`

## 8. Phase B — Isolated full-path 결과

- Sequential last-admin HTTP 400과 unauthorized HTTP 403 계약 유지
- Shared canonical role lock 경계: 6/6
- 대표 경쟁 결과: 성공 7, 안전 거부 5, 최소 final active 1
- Invariant violation·partial update·duplicate assignment·unexpected deadlock: 모두 0
- Cancellation·transaction failure rollback: PASS
- Immediate purge guard와 transaction rollback: PASS
- Due purge guard·경쟁·whole-batch rollback: PASS
- 20회 stress: violation·partial update·deadlock·server error 0
- Persistent connection·provider credential·actual provider call: 0

## 9. Phase C — Temporary Persistent read-only runtime

Latest-main temporary backend를 ReviewSafe로 실행했다.

- live/ready: 200/200
- `ReviewSafe=true`, `databaseReadOnly=true`, `mutationAllowed=false`
- Mutation probe: HTTP 423
- Delivery·escalation·purge worker: 0
- Actual provider: 0
- Read-only GET: 4/4
- Persistent identity·assignment·deletion·admin log delta: 0

검증 뒤 temporary backend만 정상 종료하고 공식 Development는 별도 Phase D gate에서 기동했다.

## 10. Phase D — Official Development handover

- Development backend 5081: latest-main, live/ready 200/200
- Development HTTPS frontend 5174: 200
- Escalation·delivery·purge worker: 각각 1, duplicate 0
- Actual provider configuration: 3종 configured/enabled
- Provider-call-start와 delivery attempt delta: 0/0
- Migration·seed·startup upsert 실행: 0
- Desktop 주요 route: 8/8, overflow 0, console error 0
- 390px 주요 route: 8/8, overflow 0, console error 0
- 직접 HTTP route: 8/8
- Review-safe 5190/5092: healthy, read-only, mutation worker/provider false
- Preview 5185: maintenance 격리 상태인 DOWN 유지
- 기존 Candidate: healthy 유지

## 11. 최종 Persistent 상태

- Ledger: `28/29/1`
- Canonical active administrator: 1
- Pending/Processing: `0/0`
- Due purge·eligible escalation: `0/0`
- Identity digest: unchanged
- Assignment/deletion/admin log count와 max timestamp: unchanged
- PostgreSQL: healthy, restart 0
- Persistent user/role/deletion mutation: 0
- Backup restore·삭제·덮어쓰기: 0

## 12. Findings

- `EVIDENCE_SUMMARY_PATH_PREFIX_LEAK`: Collector/Aggregator/Projector 분리와 projection 재검사로 해결
- `PURGE_GUARD_PREDICATE_UNREACHABLE`: PR #37 REDESIGN과 due purge whole-batch rollback을 isolated full-path에서 재검증
- Normal runtime의 `migrationExecutionEnabled` projection은 실행 설정이 아니라 capability 의미다. 실제 startup flag, ledger와 log를 함께 확인해 migration 실행 0을 판정했다.
- Browser resource status projection은 지원되지 않아 직접 HTTP 8/8과 console error 0으로 보완했다.
- 기존 Task branch는 PR #37 squash merge 뒤 원 commit과 main이 충돌해 새 report branch에서 문서-only 게시를 진행한다.
- 기존 전체 format import-order 위반 9건은 범위 밖 P3 debt로 유지한다.
- 신규 P0/P1/P2: 0

## 13. Rollback 원칙

- Runtime 이상은 exact ownership의 신규 5174/5081만 정상 종료한다.
- Review-safe를 read-only fallback으로 유지한다.
- Persistent row를 임의 수정·삭제·강제 성공 처리하지 않는다.
- Backup restore와 Direct SQL은 자동 rollback으로 사용하지 않는다.
- Identity 손상 또는 관리자 접근 손실은 별도 break-glass·DB 복구 승인을 받는다.

## 14. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Task·검수 checklist | 이 문서 | 자동 검증·사용자 검수 완료 / PR #40 merge 승인 |
| Implementation report | `tasks/uat-auth-harden-001-implementation-report.md` | 작성 완료 |
| SOP | `tasks/uat-auth-harden-001-sop.md` | 작성 완료 |
| User manual | `tasks/uat-auth-harden-001-user-manual.md` | 작성 완료 |
| Roadmap | `docs/00-product-roadmap.md` | runtime 적용 상태 반영 |

## 15. 사용자 검수 체크리스트

- [x] Development 5174 주요 화면이 정상적으로 열리는지 확인
- [x] 관리자 사용자 화면에 기존 사용자·role 정보가 정상 표시되는지 read-only로 확인
- [x] Review-safe 5190이 계속 조회 전용으로 제공되는지 확인
- [x] 동시 감소 경쟁에서 마지막 administrator 요청이 거부되는 계약을 이해
- [x] Purge guard는 malformed lifecycle defense-in-depth라는 제한을 확인
- [x] Persistent live user/role/deletion mutation은 수행하지 않았음을 확인
- [x] Direct SQL과 자동 backup restore 금지에 동의
- [x] 사용자 검수 완료 후 PR Ready·merge 여부를 별도로 승인
