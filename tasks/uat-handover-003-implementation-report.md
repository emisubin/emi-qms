# TASK-UAT-HANDOVER-003 Implementation Report

## 1. 목적과 배경

TASK-NOTIFY-REL-001의 claim/lease·attempt audit과 TASK-UAT-MAINTENANCE-001의 worker gate를 Persistent UAT에 적용하고 최신 main runtime으로 전환했다. 최초 handover preflight는 purge disable gate 부재 P2로 중단됐고 해당 remediation 병합 후 처음부터 재수행했다.

## 2. 포함·제외 범위

포함 범위는 backup/rehearsal, live 0028 migration, Review-safe 전환, Phase A, 정상 Development 복구, 장시간 관찰과 문서화다. Task automation이 만드는 신규 알림 발송, backup restore, migration/runtime code 변경, escalation starvation 수정과 환경 정리는 제외했다. 관찰 중 사용자가 직접 실행한 단일 수동 발송은 사용자 확인 후 보존된 기존 delivery의 정상 처리만 승인받았다.

## 3. 기준선

- latest main과 handover/runtime worktree source diff: 0
- Persistent UAT pre-migration ledger: canonical/live/legacy 27/28/1
- Development 5174/5081: maintenance 진입 시 통제 종료
- Review-safe 5190/5092: 기존 read-only runtime
- Pending/Processing/digest/escalation/purge/immediate insert risk: 0

## 4. Phase 1 backup

Write-capable application session과 write transaction이 두 차례 모두 0인 뒤 `fresh-pre-0028` backup을 생성했다. Secure directory/file mode는 700/600이고 checksum이 일치한다. 기존 backup은 덮어쓰지 않았다.

## 5. Restore와 migration rehearsal

동일 PostgreSQL major의 isolated tmpfs container에 fresh backup을 restore했다. Restore aggregate는 live pre-migration snapshot과 일치했다. 공식 runner 적용 후 ledger는 28/29/1, claim columns 4, delivery constraints 3, attempt constraints 8, indexes 4였다.

별도 clone에서 ledger insert failure를 주입했다. Runner 실패 후 0028 marker 0, claim columns 0, attempt table 0과 기존 aggregate 불변을 확인했다. Repository migration SQL은 수정하지 않았다.

## 6. Live migration 0028

Worker·digest·provider와 seed를 비활성화한 latest main process가 공식 `DatabaseMigrationRunner`를 호출했다. Migration SQL과 marker는 같은 transaction이었다. 적용 후 executor listener/process는 0이다.

첫 executor 시도는 이전 `net8.0` artifact 경로를 사용해 application 시작 전에 실패했다. DB marker/schema 변화 0을 확인하고 실제 최신 `net10.0` Release artifact로 재실행했다.

## 7. Live ledger와 schema

| 검증 | 결과 |
| --- | --- |
| canonical/live/legacy | 28/29/1 |
| missing/unknown | 0/0 |
| claim columns | 4/4 |
| delivery constraints | 3/3 |
| attempt FK·unique·check | 8 |
| claim/attempt indexes | 4 |
| handover 직후 attempts/provider-start | 0/0 |

Core aggregate, delivery status, 기존 attempt count와 delivery timestamp는 migration 전후 동일했다.

## 8. Review-safe 전환

최신 main backend 5092와 HTTPS frontend 5190을 canonical session으로 기동했다. Runtime은 ReviewSafe, DB read-only, mutation false, worker/provider false, migration false이며 readiness 200과 CompatibleWithApprovedLegacy 28/29/1을 반환했다. POST/PUT/PATCH/DELETE/method override는 모두 423이었다.

Privacy-safe desktop/390px 8개 결과에서 blank, structure, banner, diagnostic, Processing, console, non-aborted request와 overflow 실패는 0이었다.

## 9. Maintenance Phase A

Normal writable connection을 쓰되 migration/seed와 세 mutation worker, digest, provider를 false로 둔 temporary backend를 실행했다. 두 관찰 구간에서 aggregate·delivery timestamp·attempt·provider-start·Processing·escalation·purge 변화가 없었고 종료 후 listener는 0이었다.

초기 orchestration은 blocking `screen -DmS` 옵션 때문에 관찰 단계에 진입하지 못했다. Owned process를 종료하고 canonical `screen -dmS` 방식으로 다시 실행했다. Persistent DB 변화는 없었다.

## 10. Normal Development 설정

Canonical normal configuration은 delivery=true, purge=true, escalation=false, provider configuration complete다. Escalation은 다음 TASK-NOTIFY-ESC-001의 starvation 수정 전까지 false를 유지한다. Migration과 seed는 handover runtime에서 false로 강제했다.

Dedupe-window만 계산한 8개 후보는 기존 unique indexes와 `ON CONFLICT DO NOTHING`을 반영하면 실제 insert 가능 0이었다. 이 보정 후 normal worker activation gate를 통과했다.

## 11. 임시 Phase B

Frontend 없는 loopback backend는 single process, delivery=true, purge=true, escalation=false였다. 10분·20 interval 동안 Pending/Processing/Failed/attempt/provider-start/notification/delivery/escalation/purge audit과 core/timestamp digest가 기준선과 일치했다. Migration·seed·worker/provider error log는 0이었다.

첫 launch는 zsh 환경에서 notify-local load condition이 적용되지 않아 delivery=false로 시작됐다. 즉시 owned process를 종료하고 DB delta 0을 확인한 뒤 Bash canonical parser로 재기동했다.

## 12. 공식 Development 5081/5174

동일 effective configuration으로 backend 5081을 기동해 frontend 없이 10분 관찰했다. 이후 HTTPS strict-port frontend 5174를 5081 proxy로 기동했다. PID file/listener/cwd는 latest main runtime worktree와 일치한다.

Desktop/390px 22개 route 결과에서 status, structure, blank, target-not-found, Processing label, attempt history, console, non-aborted request와 overflow 실패는 모두 0이었다. Navigation abort는 실제 request failure에서 제외해 별도 count로 기록했다.

첫 확장 관찰의 약 35분 시점에 notification·delivery·Pending이 각각 1 증가해 자동으로 5174/5081을 종료했다. 비식별 분류는 `Manual / ManualTest / TeamsActivity / Pending`, attempt·claim·provider-start는 0이었다. 사용자가 직접 실행한 의도적 수동 발송임을 확인해 original failure code `UNEXPECTED_MANUAL_DELIVERY_DELTA`를 `AUTHORIZED_USER_ACTIVITY`로 재분류했다. Product defect, runtime isolation defect와 data cleanup required는 모두 false다.

재개 후 해당 delivery는 단일 claim과 attempt로 Sent가 됐다. Attempt 1번의 claim, provider-start, completion과 Sent outcome이 일치했고 parent notification duplicate delta, delivery duplicate delta, unrelated attempt delta와 unrelated provider call delta는 모두 0이었다.

재개한 5174에서는 desktop·390px 각각 fixed route 11개를 다시 확인했다. Page load, main/navigation structure, blank, target-not-found, Processing label, console error와 page-level overflow 실패는 모두 0이었다. 기존 상세 attempt history 검증 결과도 runtime source가 동일하므로 유효하다.

## 13. 확장 관찰

첫 실행의 temporary backend 10분은 configuration 증빙으로만 구분했다. 공식 backend 단독 10분과 frontend 포함 35분, 합계 45분은 health·PID·DB gate가 통과한 공식 runtime 유효 관찰로 인정했다. 전체 65분을 처음부터 재실행하지 않았다. 다만 이전 공식 관찰에는 purge의 다음 1시간 interval이 없었으므로 재개한 동일 latest-main runtime에서 30초 간격 122회, 3,660초를 추가 관찰했다. 다음 purge interval이 경과했고 notification/delivery/status/attempt/provider-start/escalation/purge audit/core digest, runtime ownership, Review-safe health와 PostgreSQL restart gate는 모두 불변이었다.

## 14. Worker/provider 결과

- delivery worker: normal configuration true, single backend
- escalation worker: canonical configuration false
- purge worker: true, 즉시 실행과 재개 후 다음 interval 포함 관찰
- provider configuration: loaded/configured, 값 미출력
- authorized provider call: 사용자 확인 delivery 1건
- unrelated provider call: 0
- delivery semantics: at-least-once; exactly-once 아님

## 15. Persistent UAT 보호

PostgreSQL container restart 0, volume 유지, ledger 28/29/1과 approved legacy marker를 보존했다. 설명 가능한 사용자 발송의 notification·delivery·attempt·Sent 변화 외 table/status/timestamp 변화는 없었다. Backup restore는 수행하지 않았다.

## 16. 자동 테스트

| 검증 | 결과 |
| --- | --- |
| Phase 1 Backend Release build / 전체 tests | 성공 / 331개 |
| Migration·claim·maintenance targeted | 성공 / 14개 |
| Frontend lint/typecheck/unit/build | 성공 / unit 61개 |
| Isolated Full-Stack E2E | 성공 / 16개 |
| Fresh restore·historical migration·fault rollback | 성공 |
| Review-safe API/browser | 성공 |
| Phase A·temporary Phase B·official runtime | 성공 |
| 재개 Backend 전체 tests | 성공 |
| 재개 Frontend lint/typecheck/unit/build | 성공 / unit 61개 |
| 재개 mock UI / isolated Full-Stack E2E | 성공 / 1개 / 16개 |
| 재개 frontend audit high/critical | 0/0 |
| 공식 runtime 유효 관찰 + purge 다음 interval | 성공 / 기존 45분 + 재개 3,660초 |

Frontend lint의 기존 warning 1건과 build chunk warning은 신규 오류가 아니다.

## 17. 보안·개인정보

실제 사용자·프로젝트·알림·recipient·row·credential 원문을 출력하거나 tracked 문서에 기록하지 않았다. Browser와 DB evidence는 boolean/count/fixed alias/digest만 사용했고 screenshot·raw DOM·API body를 생성하지 않았다. Provider configuration은 configured/missing boolean으로만 확인했다.

## 18. Rollback·forward-fix

0028 적용 전에는 backup restore rehearsal을 수행했다. 적용 후 schema rollback 대신 forward-fix가 원칙이다. Runtime 이상 시 Development만 종료하고 Review-safe를 유지한다. Persistent backup restore는 별도 사용자 승인 없이는 수행하지 않는다.

## 19. 제한사항과 후속 Task

- Provider 성공/DB completion crash ambiguity가 남는다.
- Escalation starvation은 TASK-NOTIFY-ESC-001에서 해결한다.
- Candidate·backup·worktree 정리는 사용자 검수·merge 이후 별도 승인 대상이다.

다음 Task는 TASK-NOTIFY-ESC-001이며 전체 신규 기능 개발 No-Go를 유지한다.

## 20. 해결한 업무 문제

Persistent UAT를 초기화하지 않고 notification reliability schema와 runtime을 적용해 다중 worker 경쟁, stale lease와 attempt audit을 공식 검수 환경에서 사용할 수 있게 했다.

## 21. 기술적 결정과 검토한 대안

- 일반 start script 재사용 대신 backend/frontend 직접 기동: start script의 migration/master upsert/seed write를 피했다.
- DB restore 대신 forward-fix: 0028 적용 후 데이터 보존을 우선했다.
- Escalation 강제 활성화 대신 canonical false 유지: 후속 starvation remediation 범위를 침범하지 않았다.
- Dedupe-window count만으로 중단하지 않고 unique/ON CONFLICT까지 반영해 실제 insertability를 판정했다.

## 22. 시행착오 및 폐기한 접근

- 오래된 target-framework artifact 경로
- blocking screen 옵션
- zsh에서 normal env load를 시도한 방식
- dedupe-window 후보를 실제 insert 가능 후보로 간주한 초기 query
- 승인된 사용자 활동을 자동화 delta로 분류한 초기 관찰 판정

모두 persistent mutation 전에 또는 delta 0을 확인한 뒤 폐기했다.

## 23. 사용자 검수 결과와 남은 항목

Checklist 작성, 자동 검증과 사용자 직접 검수를 완료했다. 사용자는 Development·Review-safe, ledger 28/29/1, `AUTHORIZED_USER_ACTIVITY` 단일 Sent lineage, Pending/Processing 0/0, backup 보존과 at-least-once 제한을 확인하고 PR #33 squash merge를 승인했다. 미체크 사용자 항목은 0이다.

## 24. 5종 산출물

| 산출물 | 상태 |
| --- | --- |
| [Task 정의](uat-handover-003.md) | 작성 완료 |
| 이 Implementation report | 작성 완료 |
| [SOP](uat-handover-003-sop.md) | 작성 완료 |
| [User manual](uat-handover-003-user-manual.md) | 작성 완료 |
| [Roadmap](../docs/00-product-roadmap.md) | 반영 완료 |
