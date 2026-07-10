# TASK-DB-MIGRATION-001 SOP — Migration ledger 검증

## 1. 문서 목적

운영자가 Fresh DB와 Historical UAT의 migration ledger를 반복 검증하고, Exact/Compatible/Mismatch를 구분하며, marker를 임의 수정하지 않고 안전하게 대응하도록 안내한다.

## 2. Migration ledger란 무엇인가

Repository의 `database/migrations/*.sql`은 적용해야 할 canonical 목록이고 DB의 `schema_migrations`는 실제 적용 이력이다. 정상 검증은 latest 한 건이 아니라 두 전체 집합과 필수 schema를 비교한다.

## 3. 상태 해석

### Exact

- canonical과 live set이 정확히 동일
- schema probe 통과
- Review-safe ready 200

### Approved legacy compatible

- canonical 전체 존재
- code-reviewed legacy만 추가 존재
- canonical successor 존재
- schema probe 통과
- Review-safe ready 200
- marker는 감사 이력으로 보존

### Mismatch

- missing canonical
- unknown extra
- successor 누락
- schema probe 실패
- catalog prefix/version 이상
- Review-safe ready 503

### Unavailable

- DB 또는 catalog 읽기 실패
- Review-safe ready 503

## 4. 사전 확인

1. Development 5174/5081, Preview 5185, current Review-safe 5190/5092 상태를 기록한다.
2. PostgreSQL container ID, health, restart count, persistent volume을 기록한다.
3. 작업 branch와 worktree가 main이 아닌지 확인한다.
4. `.env.notify-local`을 로드하지 않는다.
5. DB query는 read-only transaction 또는 Review-safe connection만 사용한다.

## 5. Runtime mode 확인

Candidate 기준:

```bash
curl -fsS http://127.0.0.1:5093/api/runtime-mode | jq '{
  mode,
  ready,
  migrationLedgerStatus,
  expectedMigrationCount,
  actualMigrationCount,
  missingMigrations,
  unexpectedMigrations,
  approvedLegacyMigrations,
  migrationSchemaCompatible,
  migrationLedgerReady
}'
```

Connection string, password 또는 provider credential을 출력하지 않는다.

## 6. Readiness 확인

```bash
curl -i http://127.0.0.1:5093/health/ready
```

- Exact/Compatible이며 모든 방어 조건 충족: 200
- Mismatch/Unavailable: 503
- 503을 우회하거나 ready를 수동으로 변경하지 않는다.

## 7. Missing/extra 확인

Runtime response의 `missingMigrations`와 `unexpectedMigrations`를 확인한다. version은 migration 식별자이며 secret이 아니지만, 업무 보고에는 필요한 항목만 기록한다.

- missing 1건 이상: 중단
- unexpected가 승인 marker 외 1건 이상: 중단
- 유사 이름: 승인 marker로 추정하지 않음

## 8. Schema probe 확인

`migrationSchemaCompatible=true`인지 확인한다. 현재 probe는 `notification_deliveries` channel constraint가 canonical 네 값을 정확히 허용하는지 system catalog SELECT로 검증한다.

Persistent UAT에서 INSERT로 확인하지 않는다. write 검증은 E2E 전용 DB에서만 수행한다.

## 9. Fresh DB 확인

1. 전용 E2E PostgreSQL container/network/tmpfs를 시작한다.
2. `emi_qms_e2e_*` DB를 사용한다.
3. canonical migrations를 적용한다.
4. `Exact`, expected=actual=27, ready 200을 확인한다.
5. E2E 자원을 project 단위로 정리하고 잔여 0을 확인한다.

Persistent UAT container를 fallback으로 사용하지 않는다.

## 10. Historical UAT 확인

현재 승인 기준:

- Canonical 27
- Live 28
- Approved legacy 1
- Missing 0
- Unknown 0
- Successor 0023 존재
- Schema compatible true

이 조건이 모두 맞을 때만 `CompatibleWithApprovedLegacy`다.

## 11. Unknown marker 대응

1. readiness 503 상태를 유지한다.
2. marker의 exact version, 최초 적용 경위, SQL 원본, canonical successor를 조사한다.
3. live schema를 SELECT로 probe한다.
4. 근거가 충분하면 별도 Task와 code review로 policy를 추가한다.
5. 근거가 부족하면 DB owner/사용자 결정을 요청한다.

## 12. Marker 삭제 금지

다음은 금지한다.

- `delete from schema_migrations`
- version `update`
- 수동 `insert`
- repository에 legacy SQL 파일 재추가
- 기존 migration rename/edit

Marker 정리는 실제 schema를 바꾸지 않더라도 감사 이력을 훼손한다.

## 13. Reconciliation 승인 절차

새 legacy 정책은 다음 근거를 모두 요구한다.

1. historical SQL 또는 신뢰 가능한 Git object
2. canonical successor
3. SQL 의미 비교
4. live schema probe
5. isolated Exact/Compatible/Mismatch fixtures
6. 사용자/DB owner 승인
7. code-reviewed exact policy

환경변수 allowlist는 사용하지 않는다.

## 14. Candidate runtime 실행

Candidate는 별도 5093/5191, 별도 screen/PID/log를 사용한다. 다음을 강제한다.

- ReviewSafe enabled
- `ReviewSafe:DatabaseApplicationName=emi-qms-uat-review-migration-candidate`
- DB default transaction read-only
- migration/seed/worker/provider disabled
- HTTPS strict port
- Backend proxy 5093
- trusted ignored localhost certificate

현재 5190/5092를 종료하지 않는다.

## 15. Handover와 rollback

사용자 검수·merge 후 별도 controlled handover에서 current 5190/5092를 merged runtime으로 전환한다. 실패 시 새 process만 종료하고 기존 process를 다시 확인한다. PostgreSQL과 Development runtime은 재시작하지 않는다.

## 16. 장애 대응

| 증상 | 조치 |
| --- | --- |
| `migration_catalog_invalid` | filename/prefix diff 조사, SQL 수정 없이 중단 |
| `migration_ledger_missing` | 적용 이력·schema 조사, 자동 migration 금지 |
| `migration_ledger_unexpected` | unknown marker 기원 조사, 임의 승인 금지 |
| `migration_ledger_legacy_successor_missing` | successor 적용 여부 조사, marker 삭제 금지 |
| `migration_ledger_legacy_schema_mismatch` | schema catalog 증빙 수집, live DDL 금지 |
| `migration_ledger_unavailable` | DB/catalog 접근 상태 확인, credential 출력 금지 |

## 17. 보안 주의사항

- `.env`, password, token, Webhook, SMTP, certificate/key 원문을 출력하지 않는다.
- actual provider를 실행하지 않는다.
- 사용자/이메일/UPN/object id를 문서에 기록하지 않는다.
- candidate log에 connection string이 없는지 확인한다.

## 18. 금지사항

- Persistent UAT write/drop/truncate/reset
- PostgreSQL restart/volume 삭제
- migration SQL 변경/추가
- current runtime 종료
- external notification
- force push/main 직접 push

## 19. 사용자 검수 체크리스트

- [x] 5191 접속 및 banner 확인
- [x] Compatible 상태 확인
- [x] Canonical 27 / Live 28 / Legacy 1 이해
- [x] marker 보존 확인
- [x] unknown/missing 503 증빙 확인
- [x] DB read-only와 mutation 423 확인
- [x] Persistent UAT data 유지 확인
- [x] SOP 반복 실행 가능
- [x] UAT-VERIFY 재개 순서 확인

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #27 병합 승인**

검수 증빙: 검수 사용자 A / 2026-07-10 / Candidate Review-safe UAT 5191와 본 SOP / 직접 화면·절차 검수 및 병합 승인. Fixture·DB·worker/provider 항목은 자동 증빙을 함께 사용했다.

## 20. 변경 이력

| 날짜 | 버전 | 내용 |
| --- | --- | --- |
| 2026-07-10 | 1.0 | TASK-DB-MIGRATION-001 최초 작성 |
| 2026-07-10 | 1.1 | 사용자 검수 완료와 PR #27 병합 승인 반영 |
