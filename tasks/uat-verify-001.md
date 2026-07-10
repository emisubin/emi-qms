# UAT-VERIFY-001 — Persistent UAT 통합 기준선 검증

## 1. 목적

최신 main과 공식 Review-safe UAT를 기준으로 migration, schema, 핵심 데이터, 권한, 알림 운영 수치, UI/UX와 데이터 보존 상태를 하나의 read-only 기준선으로 검증한다. 데이터 정리나 제품 수정 없이 다음 P2 remediation을 시작할 수 있는 근거를 만든다.

## 2. 배경

최초 UAT-VERIFY-001은 repository canonical migration 27개와 live ledger 28개의 차이를 latest-only readiness가 놓치는 P2 때문에 중단됐다. TASK-DB-MIGRATION-001이 full-set 비교와 approved legacy policy를 구현했고 TASK-UAT-HANDOVER-002가 이를 공식 5190/5092 runtime으로 인계했다. 이 재실행은 해당 선행조건을 최신 main에서 처음부터 다시 확인한다.

## 3. 선행 Task

- TASK-UAT-001: HTTPS Development UAT 안정화
- TASK-E2E-ISOLATION-001: Persistent UAT와 E2E PostgreSQL 물리 격리
- TASK-FRONTEND-SEC-001 및 TASK-UAT-HANDOVER-001: patched frontend 보안 runtime
- TASK-UAT-002: Review-safe 다층 read-only 방어
- TASK-DB-MIGRATION-001: full migration ledger와 approved legacy schema probe
- TASK-UAT-HANDOVER-002: 최신 main Review-safe 5190/5092 controlled handover

## 4. 포함 범위

- Git tree와 실행 runtime file 정합성
- repository 0001~0027 catalog와 live ledger full-set 비교
- critical column, FK, index, check constraint 확인
- 10개 핵심 table aggregate와 참조 무결성
- notification delivery, dashboard, escalation, deletion lifecycle 정합성
- System Administrator, 일반 사용자, 업무 역할 사용자, 비수신자 접근 범위
- Review-safe runtime, mutation 423, DB read-only, worker/provider 차단
- 개인정보 안전 GET API 및 desktop/390px browser matrix
- isolated backend/frontend/mock UI/Full-Stack E2E
- 검증 전후 Persistent UAT snapshot

## 5. 제외 범위

- Persistent UAT row 변경과 테스트 데이터 정리
- runtime source, migration, dependency, lockfile 또는 script 변경
- 기존 runtime 또는 PostgreSQL 재시작
- 실제 Teams Activity, Teams Channel, Mail 발송
- 알려진 notification reliability, escalation starvation, auth concurrency P2 구현
- 운영 배포와 신규 기능 개발

## 6. Runtime 정합성

- 검증 branch 기준: `b1b2e2d9eecaa0e0cd181cd21d0adf45df48fcd3`
- local main, origin/main, remote main: 일치
- 공식 Review-safe worktree HEAD: `864589b1d06edbe6a00b10fc8ce47e0eec7cc858`
- 최신 main과의 차이: TASK-UAT-HANDOVER-002 문서 5개뿐
- backend/frontend/script/migration runtime diff: 0
- 5190/5092 runtime은 최신 main과 기술적으로 동등

## 7. Migration 검증

- repository canonical: 27개, 0001~0027, prefix 중복·누락 0
- expected/latest: `0027_notification_access_scope_and_manual_work_items`
- live ledger: 28개
- canonical missing: 0
- unknown extra: 0
- approved legacy: `0020_teams_activity_delivery_channel` 1개
- canonical successor: `0023_teams_activity_delivery_channel`
- 상태: `CompatibleWithApprovedLegacy`, ready 200
- migration checksum guard 부재는 기존 P3로 유지

## 8. Schema 검증

- deletion lifecycle column: 15/15
- notification delivery 0024~0026 핵심 column: 17/17
- notification 0027 access/link column: 4/4
- TeamsActivity channel constraint compatibility: true
- critical check constraint: 9/9
- critical index: 9/9
- catalog/live schema mismatch: 0

## 9. 데이터 기준선

| Table | Count |
| --- | ---: |
| schema_migrations | 28 |
| projects | 22 |
| work_items | 37 |
| notifications | 90 |
| notification_recipients | 164 |
| notification_deliveries | 93 |
| work_item_escalations | 2 |
| qms_users | 14 |
| departments | 12 |
| system_holidays | 6 |

실제 사용자·프로젝트·업무·알림 원문과 row identifier는 수집 문서나 stdout에 남기지 않았다.

## 10. 참조 무결성

- orphan work item/project/user: 0
- completed/cancelled timestamp mismatch: 0/0
- duplicate work item idempotency key: 0
- orphan recipient/delivery: 0/0
- delivery-recipient link mismatch: 0
- work notification critical link mismatch: 0
- orphan/duplicate/closed active escalation: 0/0/0
- lifecycle request/schedule와 purge reason mismatch: 0

Open work item 중 due date가 없는 19건은 nullable schema와 현재 workflow 정책에 허용되는 후보로 분리했다. Critical integrity 위반으로 판정하지 않았다.

## 11. Notification 운영 상태

| 상태 | Count |
| --- | ---: |
| Sent | 60 |
| Failed | 20 |
| DryRunSent | 6 |
| Suppressed | 6 |
| Disabled | 1 |
| Pending | 0 |

Failed 20건은 Acknowledged 6건, Dismissed 14건으로 모두 관리자 처리돼 dashboard open Failed는 0이다. Channel은 TeamsActivity 19건, Mail 1건이다. Failed 중 synthetic 후보는 13건이며 실제 원문은 출력하지 않았다.

## 12. Dashboard 정합성

- dashboard Failed / detail open Failed: 0 / 0
- dashboard Pending / detail open Pending: 0 / 0
- dashboard active escalation / detail active escalation: 0 / 0
- L0/L1/L2/L3 active breakdown 합계: 0
- Daily Digest sent marker: 없음

## 13. 테스트·synthetic 후보

| 유형 | 후보 수 |
| --- | ---: |
| notification | 19 |
| work item | 3 |
| delivery | 41 |
| user | 0 |
| department | 1 |
| holiday | 3 |

추가 data-quality 후보는 synthetic RecipientOnly notification 무수신자 1건, historical delivery snapshot 미채움 30건, synthetic holiday pre-delete snapshot 미채움 1건이다. Runtime fallback과 접근 차단이 동작하고 핵심 업무 흐름을 막지 않으므로 P3/정리 후보로 분류한다. 삭제하지 않고 후속 `TASK-UAT-DATA-001`에서 Acknowledged/Dismissed, Completed/Cancelled, lifecycle 정책에 따라 별도 승인 후 처리한다.

## 14. 권한·접근 범위

- System Administrator admin GET: 200
- 일반 사용자와 업무 역할 사용자 admin GET: 403/403
- 일반/업무 역할 project list: 200/200
- RecipientOnly 수신자 detail/list: 200/노출
- RecipientOnly 비수신자 detail/list: 403/미노출
- Authenticated notice active 사용자 detail/list: 200/노출
- AdminOnly live fixture: 0건, 구현 분기와 authorization suite로 정적 확인
- 다른 사용자의 work item detail: 404로 비노출
- Review-safe read action mutation: server 423으로 차단

## 15. Review-safe live 검증

- mode: `ReviewSafe`
- mutationAllowed: false
- backgroundWorkersEnabled: false
- externalProvidersEnabled: false
- databaseReadOnly: true
- migrationExecutionEnabled: false
- application mode ledger: `CompatibleWithApprovedLegacy` 27/28/1
- live/ready: 200/200
- POST/PUT/PATCH/DELETE/method override: 423 `UatReviewReadOnly`

## 16. GET API/route matrix

API 16개 alias는 모두 expected access와 response shape가 일치했다. Admin denied role 2개도 403이었다. Frontend fixed route 13개는 desktop과 390px에서 각각 13/13 HTTP 200, shell/banner/diagnostic 구조가 확인됐다.

## 17. UI/UX 검증

- desktop/390px page overflow: 0
- console error: 0
- non-aborted request failure: 0
- blank page/target-not-found: 0
- enabled mutation control: 0
- project/list와 7개 주요 table alignment critical mismatch: 0
- admin table은 mobile에서 page overflow 없이 자체 scroll container를 사용
- loading/empty/error/next-action contract: 코드·browser·unit test로 확인
- screenshot, DOM/text/accessibility/console 원문: 생성·출력 0

## 18. 전후 snapshot

10개 핵심 table count와 delivery/notification/work-item 최대 시각 integer는 전후 동일했다. PostgreSQL container, volume, restart count 0, 7개 runtime PID도 유지됐다. Review-safe 검증에서 delivery 생성과 외부 provider call 증거는 0이다.

## 19. 자동 테스트

- git diff --check, actionlint, frozen install, audit 0
- backend Release build: warning/error 0
- Review-safe/migration/authorization/notification targeted: 141/141
- backend 전체: 311/311
- frontend lint: error 0, 기존 warning 1
- frontend typecheck: pass
- frontend unit: 59/59
- frontend build: pass, 기존 chunk warning 유지
- mock UI: 1/1
- Full-Stack E2E: 16/16, 전용 container/network/tmpfs, 잔여 resource 0
- redacted browser output negative guard: 5/5 차단

## 20. Findings와 Go/No-Go

- P0: 0
- P1: 0
- 신규 미해결 P2: 0
- P3: migration checksum guard, synthetic/historical data cleanup 후보
- UAT 통합 기준선: Go 후보
- P2 remediation: Go
- 신규 기능 개발: No-Go 유지

## 21. 후속 Task

1. UAT-VERIFY-001 사용자 검수와 merge
2. TASK-NOTIFY-REL-001
3. TASK-NOTIFY-ESC-001
4. TASK-AUTH-HARDEN-001
5. TASK-GOV-002
6. TASK-UAT-DATA-001은 별도 승인 후 수행

## 22. 5종 산출물 상태

| 산출물 | 경로 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/uat-verify-001-implementation-report.md` | 작성 완료 |
| SOP | `tasks/uat-verify-001-sop.md` | 작성 완료 |
| User manual | `tasks/uat-verify-001-user-manual.md` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 반영 완료 |
| User validation checklist | 이 문서 23절과 운영 문서 | Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기 |

## 23. 사용자 검수 체크리스트

- [ ] `https://localhost:5190` 접속 정상
- [ ] Review-safe banner 표시
- [ ] Migration 27/28/1 호환 상태 표시
- [ ] 프로젝트·내 업무·알림·관리자 조회 정상
- [ ] Dashboard Failed/Pending 수와 상세 목록 일치
- [ ] Active escalation 수와 상세 목록 일치
- [ ] RecipientOnly 알림 수신자만 조회 가능
- [ ] Authenticated channel notice를 active 사용자가 조회 가능
- [ ] 비수신자 개인 알림 접근 차단
- [ ] 저장·수정·삭제·발송 action disabled
- [ ] Mutation API 423
- [ ] DB read-only
- [ ] Worker/provider 미실행
- [ ] Test data 후보가 aggregate로 설명됨
- [ ] 주요 표 header/body 정렬 정상
- [ ] Console 오류 없음
- [ ] 390px/Teams narrow pane overflow 없음
- [ ] 개인정보 원문이 검증 보고에 노출되지 않음
- [ ] SOP 반복 실행 가능
- [ ] User manual 이해 가능
- [ ] 데이터 정리 권장안 이해
- [ ] UAT 기준선 Go와 전체 신규 기능 No-Go 차이 이해

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기**
