# TASK-UAT-NOTIFY-ESC-001 Implementation Report

## 1. 목적과 범위

PR #34의 escalation fair ordering과 candidate failure isolation을 Persistent UAT의 최신 main Development runtime에 통제 적용했다. 제품 source, migration, API, UI, dependency와 script는 변경하지 않았다.

## 2. 기존 위험

Development runtime은 실제 provider configuration과 세 mutation worker를 함께 사용할 수 있다. 후보 forecast 없이 escalation worker를 켜면 예상하지 않은 escalation·notification·delivery 및 외부 호출이 발생할 수 있으므로 Phase A/B/C로 분리했다.

## 3. Phase A read-only forecast

- ledger canonical/live/approved legacy: `28/29/1`
- Pending/Processing: `0/0`
- active escalation: `0`
- eligible L0/L1/L2/L3: `0/0/0/0`
- 신규 escalation/notification/delivery insert 가능: `0/0/0`
- unknown writer/write transaction: `0/0`

Forecast는 `BusinessDayCalculator`와 기존 L0~L3·recipient 정책을 재사용했고 실제 row 원문은 출력하지 않았다.

## 4. Phase B no-op evaluator

최신 main detached runtime을 frontend 없는 loopback backend로 기동했다. Escalation만 enabled였고 delivery·purge·digest·migration·seed·upsert와 actual provider는 disabled였다. 약 300초 간격의 poll 2회 결과는 다음과 같다.

- selected/evaluated/failure: `0/0/0`
- escalation insert/update delta: `0/0`
- notification/recipient/delivery/attempt delta: `0/0/0/0`
- provider-call-start/actual provider delta: `0/0`
- PostgreSQL restart delta: `0`

## 5. Ownership 예외 처리

기존 Development backend의 screen session이 소실돼 최초 fail-closed로 중단했다. 사용자의 예외 승인을 받은 뒤 listener process가 최초 기준선과 동일하고 재사용되지 않았음을 process continuity, start/elapsed time, executable, command, cwd alias, socket ownership, singleton 및 runtime 분리로 재검증했다. 정확한 process 하나에 SIGTERM을 한 번만 전송했고 정상 종료됐다. `killall`, `pkill`, SIGKILL은 사용하지 않았다.

## 6. Preview maintenance 격리

5185 listener의 process continuity, Vite command type, repository worktree alias, session ancestry, singleton과 타 runtime 분리를 확인했다. 정확한 process에 graceful 종료를 수행했으며 다른 runtime process 변화는 0이었다. Preview는 자동 재기동하지 않았다.

## 7. Phase C configuration

- environment: Development
- migration execution: disabled
- Development seed: disabled
- master-data startup upsert: 미사용
- escalation/delivery/purge: enabled
- Daily Digest: disabled
- TeamsActivity/TeamsChannel/Mail: configured and actual-enabled
- `.env.notify-local`: allowlist literal parser만 사용, source/eval 미사용
- frontend: backend 단독 검증 뒤 기동

Development의 `/api/runtime-mode`는 non-ReviewSafe에서 migration/provider 가능성을 나타내는 보수적 capability field이며 startup override 자체를 증명하지 않는다. 실제 startup command의 명시적 override, migration·seed log count 0, ledger·aggregate 불변을 함께 authoritative evidence로 사용했다.

## 8. Backend 단독 관찰

5081은 listener/process/singleton, live/ready 200/200과 worker별 effective boolean을 통과했다. Startup 직후 첫 escalation poll과 약 300초 뒤 두 번째 poll 동안 candidate·failure·escalation·notification·recipient·delivery·attempt·purge/delete·provider-call-start delta가 모두 0이었다. Worker/backend duplicate와 worker/provider failure도 0이었다.

## 9. Frontend 및 추가 poll

HTTPS strict-port 5174를 5081 proxy로 기동했다. Root, projects, my-work, notifications, Teams Activity, admin, delivery monitor, escalation monitor와 manual-send route 9개를 desktop과 390px에서 read-only로 확인했다.

- route load: `9/9`, narrow `9/9`
- blank page: `0`
- page-level overflow: `0`
- console error: `0`
- Processing label/attempt marker: present

Backend elapsed가 세 번째 300초 escalation poll 시점을 초과한 뒤 최종 snapshot을 비교했다. 추가 poll에서도 candidate와 DB/provider delta는 0이었다.

## 10. Persistent UAT 최종 비교

- ledger `28/29/1`, missing/unknown `0/0`
- Pending/Processing `0/0`, active escalation `0`
- 핵심 table count delta `0`
- notification/delivery/attempt/escalation/admin audit max timestamp delta `0`
- provider-call-start delta `0`
- PostgreSQL container·volume identity 불변, restart `0`
- fresh backup size·mode 600·checksum 불변, restore `0`

## 11. Forecast 시행착오

초기 immediate-delivery forecast는 dedupe 시간창만 적용한 상한값 `8`을 반환했다. 이 값은 DB unique index와 `ON CONFLICT DO NOTHING` 계약을 포함하지 않아 실제 insert 가능 수가 아니었다. 동일한 read-only 입력에 전체 unique/dedupe 계약을 적용해 실제 insert 가능 `0`을 확인한 뒤에만 runtime을 기동했다. 상한값을 gate로 사용한 접근은 폐기했다.

## 12. 공정성 증빙 재사용

Live candidate가 0이므로 101/200/201 tail 진행을 Persistent UAT에서 재생성하지 않았다. `TASK-NOTIFY-ESC-001` isolated PostgreSQL 결과인 101/200 2 poll, 201 3 poll, candidate failure 뒤 tail 진행과 동시 evaluator 중복 escalation·notification·delivery 0을 재사용했다.

## 13. 보안과 개인정보

GitHub 개인 metadata, credential, provider URL, 수신자·사용자·프로젝트·업무·알림 원문, DB row, raw API/DOM/log와 screenshot을 출력하지 않았다. Browser와 DB 결과는 boolean·integer·fixed enum으로만 기록했다.

## 14. 테스트 결과

| 검증 | 적용 | 결과 | 근거 |
| --- | --- | --- | --- |
| Phase A forecast | 적용 | 통과 | candidate와 insert 가능 수 0 |
| Phase B escalation-only poll | 적용 | 통과 | poll 2회, DB/provider delta 0 |
| Phase C backend/frontend poll | 적용 | 통과 | backend-only 2회 + frontend 이후 1회 |
| Backend Release build·전체 test | 적용 | 통과 | 346/346 |
| Frontend lint·typecheck·unit·build | 적용 | 통과 | unit 61/61 포함 |
| Mock UI smoke | 적용 | 통과 | 1/1 |
| Isolated Full-Stack E2E | 적용 | 통과 | 16/16, tmpfs DB 정리 완료 |
| actionlint | 적용 | 통과 | workflow 오류 0 |
| desktop/390px privacy-safe smoke | 적용 | 통과 | route 9/9, blank/overflow/console error 0 |
| Persistent aggregate/provider/backup | 적용 | 통과 | count·timestamp·provider delta 0, backup 불변 |
| 사용자 검수 | 적용 | 통과 | 현재 대화에서 checklist 전체와 PR #35 squash merge 승인 확인 |

## 15. 제한사항

- Live candidate 0인 no-op activation이다.
- 실제 provider configuration은 복원했지만 테스트 발송은 수행하지 않았다.
- 외부 전달은 at-least-once이며 exactly-once가 아니다.
- Preview 5185는 maintenance 격리로 DOWN이다.

## 16. Rollback

설명되지 않는 DB/provider delta가 발생하면 정확한 ownership의 Development 5174/5081만 graceful 종료하고 Review-safe를 유지한다. Persistent row 조정과 backup restore는 별도 승인 없이는 수행하지 않는다.

## 17. 해결한 업무 문제

코드에서 검증된 starvation 보정을 실제 UAT runtime에 안전하게 반영하면서, 후보가 없는 시점에도 worker registration·poll cadence·runtime ownership·provider 차단 근거를 운영자가 재현할 수 있게 했다.

## 18. 후속 Task

- 사용자 검수 완료와 PR #35 squash merge 승인
- 다음 코드 P2: `TASK-AUTH-HARDEN-001`
- 신규 기능 개발: No-Go 유지

## 19. 사용자 검수 상태

Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #35 squash merge 승인 / 미체크 항목 0.

## 20. 주요 파일

- `docs/00-product-roadmap.md`
- `tasks/uat-notify-esc-001.md`
- `tasks/uat-notify-esc-001-implementation-report.md`
- `tasks/uat-notify-esc-001-sop.md`
- `tasks/uat-notify-esc-001-user-manual.md`
