# TASK-DB-MIGRATION-001 — Migration ledger 전체 집합 검증

## 1. 목적

Repository migration 전체 집합과 live `schema_migrations` 전체 집합을 비교하고, 코드 리뷰로 승인된 과거 marker만 schema 동등성 확인 후 호환 처리한다. latest version 하나만 같은 false-ready를 제거하면서 Persistent UAT의 감사 이력은 수정하지 않는다.

## 2. 배경과 발견된 P2

UAT-VERIFY-001 시작 시 repository에는 `0001~0027` 27개가 있지만 Persistent UAT ledger에는 같은 27개와 `0020_teams_activity_delivery_channel`이 함께 있어 28개임을 확인했다. 기존 `ReviewSafeStatusService`는 expected/latest와 actual/latest만 비교하므로 두 latest가 모두 `0027_notification_access_scope_and_manual_work_items`이면 전체 집합 불일치에도 ready 200을 반환했다.

이 false-ready는 unknown migration, 누락 migration, 과거 rename 또는 schema drift를 검수 가능한 상태로 오판할 수 있는 P2다.

## 3. Historical marker 조사

- Git reflog에 남은 NOTIFY-003 WIP commit `72fd280…`에서 `0020_teams_activity_delivery_channel.sql`을 복원했다.
- legacy 파일 blob은 `da97e5d…`이며 현재 canonical `0023_teams_activity_delivery_channel.sql` blob과 동일하다.
- ADMIN-001이 canonical `0020~0022`를 사용하게 된 뒤 NOTIFY-003 문서와 최종 commit에서 TeamsActivity migration 번호를 `0023`으로 변경했다.
- live ledger에는 canonical `0020_admin_master_data_management`와 canonical successor `0023_teams_activity_delivery_channel`이 모두 있다.
- live channel constraint는 `TeamsChannel`, `TeamsDirectMessage`, `TeamsActivity`, `Mail`의 canonical 집합과 일치한다.

따라서 legacy marker는 삭제 대상이 아니라 동일 SQL이 merge 전 번호로 먼저 적용됐다는 감사 이력이다.

## 4. Canonical migration catalog

`DatabaseMigrationCatalog`는 `database/migrations/*.sql`을 유일한 source of truth로 사용한다.

- file basename을 version으로 사용
- ordinal filename 정렬
- 4자리 numeric prefix 파싱
- filename/version/prefix 중복 차단
- `0001`부터 연속 prefix인지 확인
- expected count와 latest version 계산
- `DatabaseMigrationRunner`와 Review-safe ledger inspector가 같은 snapshot 재사용

현재 canonical 결과는 27개, latest `0027_notification_access_scope_and_manual_work_items`다. version 목록을 별도 상수로 복제하지 않는다.

## 5. Ledger 상태 모델

| 상태 | 의미 | Ready |
| --- | --- | --- |
| `Exact` | canonical set과 live set이 정확히 같고 schema probe 통과 | 200 |
| `CompatibleWithApprovedLegacy` | canonical 전부와 승인 legacy만 있고 successor/schema probe 통과 | 200 |
| `Mismatch` | missing, unknown extra, successor 누락, schema 불일치, catalog invalid | 503 |
| `Unavailable` | DB 또는 catalog 읽기 불가 | 503 |

상세 reason은 `migration_ledger_missing`, `migration_ledger_unexpected`, `migration_ledger_legacy_successor_missing`, `migration_ledger_legacy_schema_mismatch`, `migration_ledger_schema_mismatch`, `migration_catalog_invalid`, `migration_ledger_unavailable`을 사용한다.

## 6. Approved legacy policy

허용 정책은 환경변수나 appsettings가 아니라 `MigrationLedgerCompatibilityPolicy` 코드에 고정한다.

| Legacy | Canonical successor | 도입 Task | Required probe |
| --- | --- | --- | --- |
| `0020_teams_activity_delivery_channel` | `0023_teams_activity_delivery_channel` | TASK-DB-MIGRATION-001 | `notification_deliveries` channel constraint |

허용 조건은 canonical 27개 전부 존재, successor 존재, extra가 정확히 legacy 1개, unknown extra 0, schema probe 성공이다. 유사 이름이나 추가 marker는 허용하지 않는다.

## 7. Schema compatibility probe

system catalog의 `ck_notification_deliveries_channel` 정의를 조회해 허용 channel 집합이 정확히 다음 네 값인지 확인한다.

- `TeamsChannel`
- `TeamsDirectMessage`
- `TeamsActivity`
- `Mail`

Persistent UAT에서는 SELECT만 사용했다. write 가능 여부 검증과 schema mismatch fixture는 전용 tmpfs E2E PostgreSQL에서만 수행했다.

## 8. Readiness와 runtime 응답

Review-safe ready는 다음을 모두 만족해야 한다.

- DB session read-only
- code-reviewed application name 일치
- worker/provider/migration 비활성 상태
- ledger `Exact` 또는 `CompatibleWithApprovedLegacy`
- schema compatibility probe 성공

Runtime mode에는 credential 없이 status, expected/actual count, latest, missing/unexpected/approved legacy 목록, schema/ledger ready 여부를 제공한다. 목록은 응답 크기 제한을 둔다.

## 9. Fresh DB와 Historical UAT 정책

- Fresh DB: canonical migration만 적용되어 `Exact`, 27/27, ready 200
- Historical UAT: canonical 27개와 승인 marker 1개가 있어 `CompatibleWithApprovedLegacy`, 27/28/1, ready 200
- unknown/missing/successor/schema mismatch: `Mismatch`, ready 503
- live marker는 삭제·수정·추가하지 않는다.

## 10. 포함 범위

- catalog/ledger inspector/compatibility policy
- Review-safe readiness와 runtime contract
- 최소 frontend diagnostic
- approved legacy candidate 5191/5093
- isolated fixture와 전체 회귀
- 5종 산출물

## 11. 제외 범위

- 기존 또는 신규 SQL migration
- live `schema_migrations` reconciliation
- Persistent UAT data 수정
- Development startup의 unknown marker 정책 확대
- UAT-VERIFY-001 데이터·권한·UI 기준선 검증 재개
- 외부 알림 발송

## 12. Persistent UAT 보호

- PostgreSQL container/volume/restart count 유지
- ledger count 28와 legacy marker 유지
- 핵심 table aggregate before/after 비교
- candidate application name을 분리하고 DB session read-only 강제
- mutation API 423, worker/provider 미등록

Development worker의 자연 변화 가능성은 candidate write와 구분하며 전체 DB 불변을 과장하지 않는다.

## 13. 테스트 matrix와 결과

| Fixture | 결과 |
| --- | --- |
| Fresh exact | `Exact`, ready 200 |
| Historical compatible | `CompatibleWithApprovedLegacy`, ready 200 |
| Unknown extra | `Mismatch`, ready 503 |
| Missing canonical | `Mismatch`, ready 503 |
| Legacy successor missing | `Mismatch`, ready 503 |
| Legacy schema mismatch | `Mismatch`, ready 503 |
| Similar unapproved name | `Mismatch`, ready 503 |
| Duplicate prefix | `migration_catalog_invalid` |
| Missing prefix | `migration_catalog_invalid` |

자동 검증: backend 311/311, frontend unit 59/59, mock UI 1/1, Full-Stack E2E 16/16, candidate route/browser/mutation 검증 통과.

## 14. Rollback

병합 전에는 candidate 5191/5093만 종료하면 기존 5190/5092가 그대로 유지된다. 병합 후 handover 문제가 있으면 기존 Review-safe branch/runtime으로 되돌리고 candidate를 비교 기준으로 유지한다. live ledger를 rollback 수단으로 수정하지 않는다.

## 15. 제한사항과 남은 위험

- migration SQL checksum guard는 이번 범위가 아니며 후속 P3다.
- 승인 marker 추가는 새 코드 리뷰와 schema probe가 필요하다.
- current 5190/5092는 latest-only logic이므로 merge 후 controlled handover 전에는 이 Task의 ledger 검증 runtime으로 간주하지 않는다.
- Git의 unreachable object 정리 경고는 history rewrite/prune 금지 때문에 이번 Task에서 처리하지 않는다.

## 16. 후속 Task

1. TASK-DB-MIGRATION-001 사용자 검수와 merge
2. Review-safe 5190/5092 controlled handover
3. UAT-VERIFY-001 처음부터 재실행
4. TASK-NOTIFY-REL-001
5. TASK-NOTIFY-ESC-001
6. TASK-AUTH-HARDEN-001
7. TASK-GOV-002

## 17. 5종 산출물 상태

| 산출물 | 경로 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/db-migration-001-implementation-report.md` | 작성 완료 |
| SOP | `tasks/db-migration-001-sop.md` | 작성 완료 |
| User manual | `tasks/db-migration-001-user-manual.md` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 작성 완료 |
| User validation checklist | 이 문서와 각 산출물 | 작성 완료 |

상태 구분: Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기.

## 18. 사용자 검수 체크리스트

- [ ] Candidate https://localhost:5191 접속 정상
- [ ] Review-safe banner 표시
- [ ] Runtime migration 상태가 호환으로 표시됨
- [ ] Canonical 27 / Live 28 / Legacy 1의 의미를 이해함
- [ ] 현재 UAT 데이터가 손상됐다는 의미가 아님을 이해함
- [ ] Legacy marker가 삭제되지 않았음을 확인
- [ ] Unknown extra/missing fixture가 readiness 503으로 차단됨
- [ ] Historical UAT가 approved legacy 상태로 ready 200임
- [ ] DB read-only 유지
- [ ] Mutation API 423
- [ ] Worker/provider 미실행
- [ ] Persistent UAT 데이터 유지
- [ ] SOP 실행 가능
- [ ] User manual 이해 가능
- [ ] UAT-VERIFY-001이 merge/handover 후 재개된다는 점 확인
- [ ] 전체 신규 기능 No-Go 유지 확인
