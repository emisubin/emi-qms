# TASK-UAT-AUTH-HARDEN-001 Implementation Report

## 1. 목적과 배경

PR #36의 last-active-System-Administrator transaction guard와 PR #37의 purge defense-in-depth REDESIGN을 actual HTTP/PostgreSQL 경계에서 재검증하고, Persistent identity mutation 없이 최신 main Development runtime으로 통제 전환했다.

## 2. 기준선과 실제 범위

- 실행 source: `41ce6047ced2`
- Persistent ledger: `28/29/1`
- Canonical active Entra administrator: 1
- Development: 병합 전 source의 5174/5081
- Review-safe: 기존 5190/5092, read-only
- Persistent live identity mutation 승인: 없음

Source·test·migration·dependency·script는 변경하지 않았다. 이 PR의 변경은 종료 산출물 5개뿐이다.

## 3. 아키텍처 영향

| 영역 | 영향 |
| --- | --- |
| Backend source/API | 변경 없음. PR #36·#37 병합 코드를 runtime에 적용 |
| Frontend source/UI | 변경 없음. latest-main bundle만 공식 5174로 전환 |
| DB/Migration | schema·ledger 변경 없음 |
| Authorization | 기존 HTTP 400/403와 canonical predicate 유지 |
| Worker/provider | Official Development 정상 정책 복원, actual call 0 |
| Excel/PDF/첨부 | N/A — 관련 source와 workflow 변경 없음 |

## 4. Privacy-safe evidence boundary

Collector는 subprocess 첫 byte부터 stdout/stderr를 private artifact로 수집했다. Aggregator는 file을 내부 순회해 category count만 fixed schema로 만들고, Projector는 exact key/type/enum과 short SHA를 검증한 뒤 projection 자체를 다시 검사했다.

- Synthetic multi-file qualification: PASS
- Actual Release build end-to-end qualification: PASS
- Filename prefix·private path·raw content projection: 0
- Raw artifact tracked/staged/retained: `0/0/0`

## 5. Phase A Persistent snapshot

실제 identity를 출력하지 않고 canonical predicate count, assignment/deletion aggregate, admin log count/max timestamp와 deterministic digest를 확보했다.

- Canonical role/active administrator: `1/1`
- Orphan/duplicate assignment: `0/0`
- Write transaction/unknown writer: `0/0`
- Pending/Processing: `0/0`
- PostgreSQL restart: 0

## 6. Phase B isolated HTTP와 PostgreSQL

Task-owned PostgreSQL 16, private network, tmpfs와 synthetic identity만 사용했다. Persistent connection과 실제 provider credential은 주입하지 않았다.

### 6.1 HTTP와 authorization

- 유일 administrator 비활성화·role 제거·삭제 예약: HTTP 400, final active 1
- Active administrator 2명 중 단일 감소: 성공, final active 1
- Unauthorized mutation: HTTP 403
- Response property count: 1
- SQL/query/stack/lock/internal identity 노출: 0

### 6.2 Cross-target concurrency

- Canonical role-row waiter boundary: 6/6
- 대표 aggregate: 성공 7, 안전 거부 5
- Minimum final active administrator: 1
- Invariant violation: 0
- Partial update: 0
- Duplicate role assignment: 0
- Unexpected deadlock/serialization failure: 0
- Response-shape error: 0

### 6.3 Failure와 purge

- Role-lock wait cancellation: rollback, lock/session leak 0
- User/role/deletion transaction failure: partial state 0
- Malformed immediate purge guard: deletion 0, final active 1
- Immediate purge transaction failure: failure point 도달, 전체 rollback
- Malformed due purge guard: deletion 0, final active 1
- Due purge와 role 제거 경쟁: final active 1 이상, deadlock 0
- Due purge transaction failure: failure point 도달, whole-batch rollback
- 20회 stress: invariant violation·partial update·unexpected deadlock·server error 0

## 7. Phase C temporary ReviewSafe runtime

기존 Development를 exact ownership으로 종료하고 latest-main backend를 frontend 없는 loopback ReviewSafe 모드로 Persistent UAT에 연결했다.

- live/ready 200/200
- ReviewSafe/databaseReadOnly: `true/true`
- Mutation allowed: false
- Mutation probe: HTTP 423
- Delivery·escalation·purge worker: false
- External provider: false
- Read-only GET: 4/4
- Identity digest·assignment·deletion·admin log delta: 0

검증 후 temporary process만 정상 종료했다.

## 8. Phase D official Development handover

최종 zero-risk gate 뒤 latest-main backend 5081과 HTTPS frontend 5174를 순서대로 기동했다.

- Backend/frontend listener: 각각 1
- Backend live/ready와 frontend root: 200
- Escalation·delivery·purge worker: 각각 1, duplicate 0
- Provider configuration: 3종 configured/enabled
- Provider-call-start와 delivery-attempt delta: 0/0
- Migration·seed·startup upsert 실행: 0
- Backend risk projection sampling: unchanged

Normal runtime의 `migrationExecutionEnabled=true` 필드는 startup 실행 flag가 아니라 capability projection이다. 실제 환경 flag가 false이고 ledger·startup 결과가 불변임을 함께 확인했다.

## 9. Browser smoke

Read-only navigation만 수행했다.

| 검증 | 결과 |
| --- | --- |
| Desktop 주요 route | 8/8 |
| Desktop page overflow | 0 |
| 390px 주요 route | 8/8 |
| 390px page overflow | 0 |
| Console error | 0 |
| 직접 HTTP route | 8/8 |

Browser runtime이 ResourceTiming response status를 제공하지 않아 request failure 세부 count는 N/A다. 대신 모든 대상 route의 직접 HTTP 200, mounted structure와 console error 0을 함께 확인했다.

## 10. Persistent 최종 비교

- Identity digest: unchanged
- Canonical active administrator: 1
- System Administrator assignment: unchanged
- Deletion/scheduled/purge-blocked count: unchanged
- Admin log count/max timestamp: unchanged
- Ledger: `28/29/1`
- Pending/Processing: `0/0`
- Due purge/eligible escalation: `0/0`
- Provider-call-start/delivery attempt delta: `0/0`
- PostgreSQL: healthy, restart 0
- Persistent user/role/deletion mutation: 0

Fresh pre-0028 backup은 mode 600·non-empty·checksum evidence 일치를 확인했으며 restore·삭제·덮어쓰기는 수행하지 않았다.

## 11. Review-safe와 환경 보존

- Review-safe 5190/5092: 200/200, DB read-only, mutation/worker/provider false
- Preview 5185: DOWN 유지
- Notification/Maintenance Candidate: healthy 유지
- Persistent PostgreSQL와 backup: 유지
- Existing branch/worktree: 정리하지 않음

## 12. 자동 검증

| 검증 | 적용 | 결과 |
| --- | --- | --- |
| Release build | Yes | PASS |
| Targeted auth/API | Yes | 21/21 |
| Isolated HTTP concurrency | Yes | PASS |
| Backend 전체 | Yes | 361/361 |
| Frontend lint/typecheck/unit/build | Yes | PASS / PASS / 61/61 / PASS |
| Full-Stack E2E | Yes | PASS, current suite 17 scenarios |
| actionlint·diff check | Yes | PASS |
| Persistent before/after | Yes | unchanged |
| Actual Persistent live mutation | No | Break-glass 미증명으로 `NO_GO` |

전체 format command의 기존 import-order 위반 9건은 범위 밖 P3 debt다. 이번 changed-file format 위반은 0이다.

## 13. 시행착오 및 보정

- Evidence summary가 multi-file prefix를 투영하던 문제를 Collector/Aggregator/Projector 분리로 제거했다.
- Parser가 `SystemExit`을 error로 잡던 조건, zsh 예약 변수, private directory mode와 cleanup을 보정했다.
- External HTTP harness의 Npgsql version, WebApplicationFactory content root와 synthetic development key를 보정했다.
- Worker-disable 환경 변수가 full test에 전파된 실행은 무효화하고 clean environment에서 361/361을 다시 확인했다.
- `nohup` temporary runtime 조기 종료를 foreground-owned session으로 교체했다.
- macOS Bash 3.2 호환성과 screen ownership ancestry projection을 보정했다.
- Browser DOM의 비표준 projection을 `children.length` 기반으로 교정했다.
- 기존 PR #37 branch를 origin/main에 merge하면 squash 원 commit과 충돌해 merge를 abort하고 origin/main 기반 report branch를 새로 사용했다.

모든 보정은 Task-owned harness·실행 방식 또는 게시 branch에 한정됐고 제품 source 추가 변경은 없었다.

## 14. 실행하지 않은 검증

- Persistent 유일 administrator 실제 비활성화·role 제거·삭제: 접근 손실 위험과 break-glass 미증명
- Persistent 임시 administrator 생성: identity/audit mutation 미승인
- Review-safe source 교체: 이 Task 범위 밖이며 기존 fallback 유지
- Backup restore: 자동 rollback 금지
- Direct SQL: application protection 우회이므로 운영상 금지

## 15. 해결한 업무 문제

병합된 concurrency guard가 isolated test만 통과한 상태에서 실제 Development runtime에는 반영되지 않았던 운영 Gate를 닫았다. Persistent identity를 위험하게 변경하지 않고도 transaction·HTTP·runtime·UI·데이터 불변성을 연결했다.

## 16. 기술적 결정과 검토한 대안

- Persistent live negative test 대신 synthetic actual HTTP를 authoritative 경쟁 증빙으로 사용했다.
- Persistent UAT는 mutation-free runtime handover와 digest 불변성만 검증했다.
- Review-safe는 최신화하지 않고 기존 read-only fallback으로 보존했다.
- Fixed time poll 대신 auth guard 특성에 맞춘 startup·read-only request·DB drain 기반 bounded observation을 사용했다.

## 17. 사용자 검수 결과와 남은 항목

자동 검증과 runtime handover는 완료했다. 사용자는 Phase A/B, Phase C/D와 5종 산출물 Draft PR 게시를 승인했다. 화면 read-only 사용자 검수와 PR Ready·merge 승인은 아직 대기다.

Persistent live identity mutation은 계속 `NO_GO`다. 재검토에는 인증 가능한 별도 Entra break-glass administrator, 복구 rehearsal와 별도 mutation 승인이 필요하다.

## 18. Rollback과 복구

Runtime 문제는 신규 5174/5081의 exact ownership을 확인해 정상 종료하고 Review-safe만 제공한다. Persistent row를 임의 수정하지 않으며 backup restore와 Direct SQL은 각각 별도 승인 없이는 수행하지 않는다. Source rollback이 필요하면 PR #36·#37 전체 guard 경계를 함께 검토하는 forward-fix 또는 명시적 revert Task로 분리한다.

## 19. 보안·PII

Tracked 문서에는 실제 사용자·email·ID, raw DB/API/DOM/log, credential, connection string와 provider target을 포함하지 않았다. Task-owned raw evidence 48개와 isolated container/network를 제거했고 tracked/staged/retained count는 0이다.

## 20. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Task·검수 checklist | `tasks/uat-auth-harden-001.md` | 자동 검증 완료 / 사용자 검수 대기 |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | `tasks/uat-auth-harden-001-sop.md` | 작성 완료 |
| User manual | `tasks/uat-auth-harden-001-user-manual.md` | 작성 완료 |
| Roadmap | `docs/00-product-roadmap.md` | controlled runtime 상태 반영 |
