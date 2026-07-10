# TASK-UAT-HANDOVER-002 — Review-safe runtime controlled handover

## 1. 목적

PR #27에서 병합된 full migration ledger 검증 코드를 Persistent UAT의 공식 Review-safe 주소인 HTTPS 5190 / Backend 5092에 통제된 절차로 반영한다. Development, Preview, Candidate와 PostgreSQL을 유지하면서 runtime만 최신 main으로 교체한다.

## 2. 배경

PR #27 merge 뒤에도 기존 5190/5092는 latest migration 하나만 비교하는 PR #26 이전 runtime이었다. Candidate 5191/5093은 full ledger를 검증했지만 공식 Review-safe 주소는 아니므로 UAT-VERIFY-001을 재개하기 전에 merged main runtime handover가 필요했다.

첫 browser smoke에서 raw accessibility/DOM 상태가 출력돼 UAT 식별 텍스트가 노출되는 검증 절차 P2가 발생했다. 제품 runtime이나 Persistent UAT 데이터 결함은 아니며, 재개 시 raw DOM 방식을 폐기하고 boolean·integer·고정 enum만 허용하는 redacted harness로 교체했다.

## 3. 선행 Task

- TASK-UAT-002: Review-safe 다층 방어와 5190/5092 실행 경로
- TASK-DB-MIGRATION-001: full-set ledger, approved legacy policy와 schema probe
- PR #27 squash merge: `864589b1d06edbe6a00b10fc8ce47e0eec7cc858`
- 중단된 UAT-VERIFY-001: latest-only false-ready P2로 clean 상태 보존

## 4. Existing / Candidate / Main runtime

| 구분 | 주소 | source | 역할 |
| --- | --- | --- | --- |
| Existing | 5190 / 5092 | `task/uat-002-review-safe` | handover 전 latest-only runtime, rollback 기준 |
| Candidate | 5191 / 5093 | `task/db-migration-001` | PR #27 사전 검수와 비교 기준 |
| Main | 5190 / 5092 | detached `origin/main` | handover 후 공식 Review-safe runtime |

Candidate와 merged main의 전체 tree ID는 동일했다. Handover 후 Main runtime HEAD도 origin/main과 일치한다.

## 5. 개인정보 안전 browser validation

Repository 밖 `/tmp/emi-qms-redacted-browser-smoke`에 임시 harness를 두고 다음만 출력했다.

- route alias, HTTP status
- page/structure/banner/runtime/ledger boolean 또는 enum
- canonical/live/legacy count
- mutation control, console error, request failure, overflow count
- blank/target-not-found/API card boolean

DOM, accessibility snapshot, heading/table/card text, screenshot, response body, console message, storage/cookie는 출력하지 않았다. Harness는 schema allowlist를 통과한 record만 출력하며 실패 시 `OUTPUT_REDACTION_FAILED`만 반환한다.

## 6. 포함 범위

- Candidate/main tree 동일성 확인
- 개인정보 안전 desktop/390px route matrix
- Existing/Candidate 비식별 구조 비교
- rollback 정보와 process ownership 확인
- Existing 5190/5092만 종료하고 merged main 5190/5092 기동
- ledger 27/28/1, schema probe, DB read-only와 mutation 423 확인
- worker/provider 미실행과 Persistent UAT 전후 aggregate 비교
- 5종 산출물과 Draft PR

## 7. 제외 범위

- runtime source, migration SQL, dependency/lockfile 또는 script 변경
- live ledger row 수정과 Persistent UAT write
- Development 5174/5081, Preview 5185, Candidate 5191/5093 재시작
- 실제 Teams Activity, Teams Channel, Mail 발송
- UAT-VERIFY-001 재개와 본 PR merge

## 8. Runtime tree 비교

- origin/main HEAD: `864589b1d06edbe6a00b10fc8ce47e0eec7cc858`
- Candidate branch tree와 origin/main tree: 동일
- detached main runtime HEAD: origin/main과 동일
- runtime/config/script/migration/dependency 차이: 0

## 9. Rollback

Existing의 canonical screen, PID file, log, certificate path, backend proxy와 worktree ownership을 기록했다. 새 runtime이 실패하면 새 5190/5092만 ownership 확인 후 종료하고 기존 `uat-002-review-safe` script로 복귀한다. Development, Preview, Candidate와 PostgreSQL은 rollback 대상이 아니다.

## 10. Candidate 검증

- live/ready: 200/200
- mode: ReviewSafe
- ledger: CompatibleWithApprovedLegacy, canonical/live/legacy 27/28/1
- DB read-only: true
- workers/providers/migration: disabled
- POST/PUT/PATCH/DELETE/method override: 423
- desktop 11/11, 390px 11/11
- console/request/overflow/target-not-found: 0
- output negative guard: synthetic 5/5 차단

## 11. Current 종료

PID file, listener, cwd, command와 screen이 모두 `uat-002-review-safe` 소유임을 다시 확인한 뒤 frontend 5190, backend 5092만 정상 종료했다. 5174/5081, 5185, 5191/5093와 PostgreSQL PID/health는 유지됐다.

## 12. Main runtime 기동

`uat-review-runtime-main` detached worktree의 canonical HTTPS script로 backend 5092와 frontend 5190을 기동했다.

- Backend PID: 95246
- Frontend PID: 95280
- screen: `emi-qms-uat-review-backend`, `emi-qms-uat-review-frontend`
- cwd: detached main runtime의 backend/frontend
- Vite: 7.3.6
- proxy: 5190 → 5092

## 13. Ledger 상태

- status: `CompatibleWithApprovedLegacy`
- canonical/live/approved legacy: 27/28/1
- missing/unknown: 0/0
- schema compatible: true
- ledger ready: true
- live legacy marker는 삭제·수정하지 않음

## 14. DB read-only

Runtime endpoint는 `databaseReadOnly=true`를 반환했고 targeted integration test는 session/pool/explicit transaction write가 모두 실패함을 검증했다. Persistent UAT에서는 SELECT aggregate만 수행했다.

## 15. Mutation / worker / provider 차단

- unsafe HTTP method와 method override: 423 `UatReviewReadOnly`
- mutation worker startup evidence: 0
- actual provider call evidence: 0
- runtime flags: workers/providers/migration disabled
- delivery row 신규 생성: 0

## 16. Redacted desktop/mobile 검증

| 대상 | Route | 성공 | Console | Request | Overflow | Enabled mutation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Candidate desktop | 11 | 11 | 0 | 0 | 0 | 0 |
| Candidate 390px | 11 | 11 | 0 | 0 | 0 | 0 |
| Main desktop | 11 | 11 | 0 | 0 | 0 | 0 |
| Main 390px | 11 | 11 | 0 | 0 | 0 | 0 |

Handover 전 Existing/Candidate 차이는 승인된 migration diagnostic뿐이었다. Handover 후 Main/Candidate desktop/mobile 구조 차이는 0이다.

## 17. Persistent UAT 전후

전후 aggregate는 동일했다.

| 항목 | 전 | 후 |
| --- | ---: | ---: |
| schema_migrations | 28 | 28 |
| canonical / legacy | 27 / 1 | 27 / 1 |
| projects | 22 | 22 |
| work_items | 37 | 37 |
| notifications | 90 | 90 |
| notification_recipients | 164 | 164 |
| notification_deliveries | 93 | 93 |
| work_item_escalations | 2 | 2 |
| qms_users / departments / holidays | 14 / 12 / 6 | 14 / 12 / 6 |

Delivery status, maximum timestamp, container, volume, restart count도 동일했다.

## 18. Findings

- 기존 Finding: 검증 절차 P2. Raw DOM 출력 방식 폐기와 output allowlist guard로 해결.
- 제품 runtime 코드 Finding: 없음.
- 신규 미해결 P0/P1/P2: 없음.
- P3: migration checksum guard는 기존 후속 항목 유지.

## 19. 제한사항

- Candidate는 사용자 검수와 UAT-VERIFY 완료 전 rollback 비교용으로 유지한다.
- 전체 DB 장기 불변은 Development worker와 공유하므로 보장하지 않는다. 본 handover 관찰 구간의 aggregate만 동일했다.
- 이 Task는 코드 변경이 아닌 runtime handover와 증빙 문서 Task다.

## 20. 후속 Task

1. TASK-UAT-HANDOVER-002 사용자 검수와 merge
2. UAT-VERIFY-001 최신 main에서 처음부터 재실행
3. TASK-NOTIFY-REL-001
4. TASK-NOTIFY-ESC-001
5. TASK-AUTH-HARDEN-001
6. TASK-GOV-002

## 21. 5종 산출물 상태

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/uat-handover-002-implementation-report.md` | 작성 완료 |
| SOP | `tasks/uat-handover-002-sop.md` | 작성 완료 |
| User manual | `tasks/uat-handover-002-user-manual.md` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 반영 완료 |
| User validation checklist | 이 문서 22절 및 각 운영 문서 | Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #28 병합 승인 |

## 22. 사용자 검수 체크리스트

- [x] `https://localhost:5190` 접속 정상
- [x] Review-safe banner 표시
- [x] Migration 호환 상태 표시
- [x] Canonical 27 / Live 28 / Legacy 1 표시
- [x] 프로젝트·업무·알림·관리자 조회 정상
- [x] 검색·필터·정렬·상세 이동 정상
- [x] mutation action disabled
- [x] Mutation API 423
- [x] DB read-only
- [x] Worker/provider 미실행
- [x] Current 5190과 Candidate 5191 기능·구조 일치
- [x] Console 오류 없음
- [x] 390px/Teams narrow pane overflow 없음
- [x] 실제 사용자·프로젝트·알림 원문이 검증 보고에 노출되지 않음
- [x] Development 5174/5081 유지
- [x] Preview 5185 유지
- [x] Persistent UAT 데이터 유지
- [x] Legacy marker 미삭제
- [x] SOP 실행 가능
- [x] User manual 이해 가능
- [x] UAT-VERIFY-001이 다음에 처음부터 재실행됨을 이해

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #28 병합 승인**

검수 증빙: 검수 사용자 A / 2026-07-11 / Current Review-safe 5190, Candidate 5191 및 5종 산출물 / 주요 조회 화면·Review-safe banner·Migration 호환 상태·mutation action 비활성화·Candidate 구조 동등성·개인정보 안전 정책·SOP·User manual 검수 완료와 PR #28 병합 승인. Mutation API 423, DB read-only, worker/provider 미실행, console/request error 0, 390px overflow 0, runtime·Persistent UAT 보존은 자동 증빙을 함께 사용했다.
