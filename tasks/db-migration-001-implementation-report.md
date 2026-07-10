# TASK-DB-MIGRATION-001 Implementation Report

## 1. 목적

Review-safe UAT가 migration latest 하나가 아니라 canonical/live ledger 전체 집합과 필수 schema를 검증하도록 보강하고, 승인된 legacy marker를 데이터 변경 없이 감사 이력으로 보존한다.

## 2. 배경

UAT-VERIFY-001 선행 조사에서 repository 27개와 live ledger 28개가 달랐지만 기존 5092는 두 latest가 같다는 이유로 ready 200을 반환했다. 이 false-ready를 P2로 분류하고 UAT-VERIFY를 clean 상태로 중단했다.

## 3. 기존 false-ready 위험

- canonical migration 누락이 latest 아래에 있으면 탐지 실패
- unknown extra marker가 있어도 탐지 실패
- 잘못된 rename이나 WIP marker를 승인된 history로 오판
- marker와 실제 schema가 불일치해도 ready 가능
- 검수자가 Persistent UAT를 merged repository와 동일하다고 잘못 판단

## 4. Historical marker 기원

Git history의 현재 reachable main에는 legacy filename이 없다. 그러나 reflog의 NOTIFY-003 WIP commit `72fd280…` tree에는 `0020_teams_activity_delivery_channel.sql`이 있으며 blob은 `da97e5d…`다. ADMIN-001 병합 후 rebase commit `c0cb734…`에는 canonical ADMIN `0020~0022`와 legacy TeamsActivity `0020`이 동시에 존재했고, 후속 NOTIFY-003 commit에서 TeamsActivity 파일이 `0023`으로 번호 조정됐다.

현재 canonical `0023`도 같은 blob `da97e5d…`를 사용한다. 즉 SQL은 동일하고 filename marker만 merge 전후 다르다.

## 5. 기존 repository/live 상태

| 항목 | Repository | Persistent UAT |
| --- | ---: | ---: |
| Canonical count | 27 | 27 모두 존재 |
| Total ledger count | 27 | 28 |
| Latest | 0027 | 0027 |
| Approved legacy | 없음 | `0020_teams_activity_delivery_channel` 1건 |
| Canonical successor 0023 | 존재 | 존재 |
| TeamsActivity constraint | canonical SQL | canonical 값 집합과 일치 |

Live ledger row, schema, 업무 data는 수정하지 않았다.

## 6. 구현 범위

- validated migration catalog
- full-set ledger inspector
- code-reviewed approved legacy policy
- canonical TeamsActivity schema probe
- Review-safe readiness/runtime contract
- candidate 전용 code-reviewed application name
- frontend의 요약 diagnostic
- catalog와 DB fixture tests

## 7. 제외 범위

- migration SQL 변경/추가
- live marker 삭제/rename/reconciliation
- Development startup의 ledger gate 확대
- checksum 저장
- UAT-VERIFY-001 재개
- 외부 provider smoke

## 8. 전체 아키텍처

`database/migrations` → `DatabaseMigrationCatalog` → `DatabaseMigrationRunner`와 `MigrationLedgerInspector`가 같은 snapshot을 사용한다. Inspector는 live set을 읽고 compatibility policy와 schema probe를 적용한다. `ReviewSafeStatusService`는 DB read-only/application name/ledger 결과를 결합해 runtime endpoint와 readiness를 만든다. Frontend는 runtime response의 요약만 banner에 표시한다.

## 9. Migration catalog

- filename regex와 numeric prefix 파싱
- ordinal sort
- duplicate version/prefix 차단
- `0001`부터 prefix gap 차단
- expected count/latest 계산
- catalog 오류는 stable `migration_catalog_invalid` 또는 unavailable로 변환

Runner도 이 catalog를 사용하므로 catalog가 비정상이면 새 migration 적용 전에 실패한다.

## 10. Ledger inspector

live `schema_migrations.version` 전체를 ordinal로 읽고 canonical set과 비교한다. missing/unexpected는 최대 20건만 응답해 크기를 제한한다. Exact/Compatible/Mismatch/Unavailable 상태와 stable reason을 함께 반환한다.

## 11. Legacy compatibility policy

`MigrationLedgerCompatibilityPolicy`는 다음 한 건만 허용한다.

- legacy: `0020_teams_activity_delivery_channel`
- successor: `0023_teams_activity_delivery_channel`
- introducedByTask: `TASK-DB-MIGRATION-001`
- probe: `notification_deliveries_channel`

환경변수나 appsettings로 marker를 추가할 수 없다. similar name, 두 번째 extra, successor 누락은 모두 mismatch다.

## 12. Schema probe

`pg_constraint`, `pg_class`, `pg_namespace`, `pg_get_constraintdef` SELECT로 `ck_notification_deliveries_channel` 값을 읽는다. 네 channel set이 정확히 일치할 때만 compatible하다. Persistent UAT write probe는 하지 않았고, 실패 fixture의 constraint 변경은 전용 E2E DB에서만 수행했다.

## 13. Readiness/health

Review-safe ready 200은 다음 결합 조건이다.

- transaction read-only on
- application name이 코드 승인 목록과 일치
- ledger ready
- schema compatible

Missing/unknown/successor/schema/catalog/DB 오류는 503이다. Historical UAT는 warning 성격의 `CompatibleWithApprovedLegacy`이지만 검수 가능하므로 ready 200이다.

## 14. Runtime endpoint

추가한 안전한 diagnostic:

- `migrationLedgerStatus`
- expected/actual count
- missing/unexpected/approved legacy 목록
- schema compatible
- ledger ready

Connection string, DB password, credential, provider 설정은 응답하지 않는다.

## 15. Development mode 영향

- ReviewSafe=false이면 기존 migration/startup/worker/provider 등록이 유지된다.
- Runner는 같은 catalog의 canonical 파일을 기존 transaction 방식으로 적용한다.
- Fresh E2E DB는 27개 canonical marker를 기록하고 `Exact`다.
- Development readiness에 unknown legacy 정책을 새로 강제하지 않았다.

## 16. Test fixtures

| Fixture | Ledger status | HTTP ready |
| --- | --- | ---: |
| Fresh exact | Exact | 200 |
| Historical compatible | CompatibleWithApprovedLegacy | 200 |
| Unknown extra | Mismatch | 503 |
| Missing canonical | Mismatch | 503 |
| Legacy successor missing | Mismatch | 503 |
| Legacy schema mismatch | Mismatch | 503 |
| Similar unapproved name | Mismatch | 503 |
| Duplicate prefix | MigrationCatalogInvalid | 시작 전 실패 |
| Missing prefix | MigrationCatalogInvalid | 시작 전 실패 |

모든 DB fixture는 실행별 E2E container/network/tmpfs에서 수행하고 cleanup 후 잔여 자원 0을 확인했다.

## 17. Persistent UAT candidate 결과

- URL: `https://localhost:5191`, backend `http://127.0.0.1:5093`
- application name: `emi-qms-uat-review-migration-candidate`
- status: `CompatibleWithApprovedLegacy`
- Canonical/Live/Legacy: 27/28/1
- missing/unknown: 0/0
- schema compatible: true
- ready: 200
- POST/PUT/PATCH/DELETE/method override: 423 `UatReviewReadOnly`
- root/Teams/Admin/API/health: 200
- 390px overflow: 0
- console/non-aborted request error: 0/0

Current 5190/5092와 Development 5174/5081, Preview 5185는 종료·재시작하지 않았다.

Persistent UAT before/after aggregate는 모두 동일했다.

| Table | Before | After |
| --- | ---: | ---: |
| schema_migrations | 28 | 28 |
| projects | 22 | 22 |
| work_items | 37 | 37 |
| notifications | 89 | 89 |
| notification_recipients | 163 | 163 |
| notification_deliveries | 92 | 92 |
| work_item_escalations | 2 | 2 |
| qms_users | 14 | 14 |
| departments | 12 | 12 |
| system_holidays | 6 | 6 |

Delivery status와 max timestamp도 before/after 동일했고 PostgreSQL container ID, volume, restart count 0이 유지됐다.

## 18. 테스트 결과

| 검증 | 결과 |
| --- | --- |
| `git diff --check` | 통과 |
| actionlint | 통과 |
| Backend Release build | 통과, warning 0 |
| Backend 전체 | 311/311 통과 |
| Ledger targeted | 16/16 통과 |
| Frontend lint | error 0, 기존 Fast Refresh warning 1 |
| Frontend typecheck | 통과 |
| Frontend unit | 59/59 통과 |
| Frontend build | 통과, 기존 chunk-size warning |
| Mock UI | 1/1 통과 |
| Full-Stack E2E | 16/16 통과 |
| Candidate browser | 통과 |

## 19. Secret/PII

실제 사용자 이름, 회사 이메일/UPN, tenant/client/object id, password, token, Webhook, SMTP, certificate/private key를 출력하거나 tracked file에 기록하지 않았다. Candidate는 `.env.notify-local`을 읽지 않았고 actual provider 설정을 disabled로 강제했다.

## 20. Rollback

- merge 전: candidate 5191/5093만 종료하고 current 5190/5092 유지
- merge 후 handover 전: current runtime을 공식 검수 환경으로 유지
- handover 실패: candidate와 current 비교 후 frontend/backend 둘 다 통제된 절차로 이전 runtime 복귀
- 어떤 경우에도 live ledger row를 수정하지 않음

## 21. 제한사항

- migration checksum은 기록하지 않는다.
- 현재 5190/5092는 latest-only 코드이므로 controlled handover가 필요하다.
- 5191/5093은 사용자 검수 전 유지한다.
- Development worker가 같은 DB를 사용하므로 aggregate 자연 변화 가능성을 source/timestamp로 분리해야 한다.

## 22. 후속 Task

1. 사용자 검수와 Draft PR merge 결정
2. Review-safe 5190/5092 controlled handover
3. UAT-VERIFY-001 clean branch에서 처음부터 재검증
4. TASK-NOTIFY-REL-001
5. TASK-NOTIFY-ESC-001
6. TASK-AUTH-HARDEN-001
7. TASK-GOV-002

## 23. 해결한 업무 문제

검수 화면의 ready가 단순히 가장 최신 marker 이름만 비교해 과거 WIP나 누락을 숨기던 문제를 제거했다. 기존 UAT의 감사 이력과 업무 data를 보존하면서도 repository와 호환되는 이유를 사용자에게 설명할 수 있게 했다.

## 24. 기술적 결정과 검토한 대안

| 결정 | 대안 | 선택 이유 |
| --- | --- | --- |
| 파일 기반 단일 catalog | 27개 상수 hardcode | source drift 방지 |
| exact code policy | env allowlist | 운영자가 임의 승인하는 우회 방지 |
| marker + schema probe | marker 이름만 비교 | 실제 schema 동등성 확인 |
| legacy row 보존 | row delete/rename | 감사 이력과 금지사항 준수 |
| 503 fail-closed | warning만 표시 | unknown/missing 검수 오판 방지 |
| frontend 요약 표시 | version 전체 노출 | 사용자 이해와 내부 상세 최소화 균형 |

## 25. 시행착오 및 폐기한 접근

- reachable main history만 조회하면 legacy 파일이 보이지 않았다. 삭제된 것으로 단정하지 않고 reflog의 WIP commit을 찾아 동일 blob을 검증했다.
- latest-only 비교를 count 비교로만 바꾸는 안은 unknown 1개와 missing 1개가 상쇄될 수 있어 폐기했다.
- arbitrary application name 설정은 readiness 검사를 자기 선언으로 약화시킬 수 있어 두 code-reviewed 값만 허용했다.
- Persistent UAT에서 channel insert를 시도하는 안은 read-only 원칙 때문에 폐기하고 system catalog SELECT + isolated write fixture로 분리했다.

## 26. 사용자 검수 결과와 남은 항목

- Checklist 작성됨
- 자동 검증 완료
- 사용자 검수 대기
- PR 병합 승인 대기

남은 사용자 항목은 5191 화면, 27/28/1 의미, legacy 보존, mismatch 503 증빙, SOP/User manual 이해다. 사용자 검수 완료로 임의 표시하지 않았다.
