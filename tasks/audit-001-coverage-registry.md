# TASK-AUDIT-001 mutation coverage registry

- status: `APPROVED_CATALOG_AND_CENTRAL_TRANSACTION_ACCEPTANCE / INDEPENDENT_REVERIFICATION_PASS`
- approved acceptance change: `tasks/audit-001-change-002.md`
- canonical executable registry: `backend/src/Emi.Qms.Api/Audit/AuditMutationRegistry.cs`
- canonical tracked-relation registry: `database/migrations/0083_global_access_change_audit.sql`
- verification: `AuditMutationCoverageTests.Registry_ExactlyCoversEveryMutationEndpoint`, `AuditInfrastructureTests.MigrationRelationCoverage_ExactlyClassifiesEverySchemaRelation`
- measured catalog: mutation endpoint `185`, included `156`, explicit endpoint exclusion `29`; schema relation `145`, tracked business relation `94`, explicit relation exclusion `51`

## 1. Coverage contract

ASP.NET runtime endpoint catalog의 모든 `POST`, `PUT`, `PATCH`, `DELETE` route를 exact method+route key로 고정한다. 실제 catalog와 registry 사이에 신규 누락 또는 제거된 stale route가 하나라도 있으면 application startup과 contract test가 실패한다. 포함 route는 server-owned safe endpoint name이 반드시 있어야 한다.

포함 route는 개별 store 이름을 반복 등록하지 않는다. 해당 HTTP request의 actor·actual actor·request correlation·login correlation·domain·safe action을 request scope에 설정하고, 그 request가 연 94개 업무 relation의 모든 실제 row 변경을 DB trigger가 같은 transaction에서 기록한다. 따라서 coverage matrix의 store/recorder 열은 전 route에서 `existing store method → qms_audit_capture_row_change()`로 동일하다.

| 업무영역 | 포함 route | 성공 recorder | field projection | 실패 분류 | worker |
| --- | ---: | --- | --- | --- | --- |
| Administration | 22 | 기존 store transaction + DB row trigger | fixed catalog | 400/422 Validation, 409/412 Conflict | 제외 |
| G2 | 5 | 동일 | 동일 | 동일 | 제외 |
| Logistics | 8 | 동일 | 동일 | 동일 | 제외 |
| Manufacturing | 10 | 동일 | 동일 | 동일 | 제외 |
| Materials | 8 | 동일 | 동일 | 동일 | 제외 |
| Notices | 5 | 동일 | 동일 | 동일 | 제외 |
| Notifications | 4 | 동일 | 동일 | 동일 | 제외 |
| Operations(form/template/sales/profile 등) | 23 | 동일 | 동일 | 동일 | 제외 |
| Pending | 11 | 동일 | 동일 | 동일 | 제외 |
| Procurement | 3 | 동일 | 동일 | 동일 | 제외 |
| ProductionPlanning | 9 | 동일 | 동일 | 동일 | 제외 |
| Projects | 28 | 동일 | 동일 | 동일 | 제외 |
| Quality | 17 | 동일 | 동일 | 동일 | 제외 |
| Workflow | 3 | 동일 | 동일 | 동일 | 제외 |
| 합계 | 156 | request parent 1건 + 실제 row/field child | exact/metadata/excluded | fixed server reason만 | request context 없는 worker 0건 |

156개 exact route 목록은 executable registry의 `KnownMutationRouteKeys - ExcludedMutationRouteKeys`가 source of truth다. 문서에 별도 복제해 drift를 만들지 않는다.

## 2. 명시적 제외 29개

| 분류 | 수 | route | canonical 처리 또는 제외 이유 |
| --- | ---: | --- | --- |
| 로그인 lifecycle | 2 | `/api/audit/sessions/interactive-login`, `/api/audit/sessions/logout` | 일반 mutation이 아니라 전용 Login/Logout 사건 |
| Excel export | 3 | selected data export, form-template export, project selected export | 기존 `data_export_events`가 canonical |
| preview·read-style POST | 9 | calendar/procurement/production-planning/project import preview 6종, QR print/resolve, web-push current-status | durable 업무 변경이 아니거나 조회·생성 미리보기 |
| 알림 읽음 | 3 | notification 단건/전체/project 전체 읽음 | 저가치 기술 상태이며 제품 계약상 제외 |
| 알림 provider·관리 처리 | 7 | acknowledge, dismiss, reprocess, retry, manual send, mail test, Teams Activity test | 기존 delivery/attempt 원장과 provider 내부 처리 유지 |
| web-push 기기 상태 | 3 | subscription 등록, current/all deactivate | 기기 기술 상태이며 사람의 업무 데이터 변경에서 제외 |
| PDF 재처리 | 2 | inspection/IQC PDF retry | provider 내부 재처리이며 기존 상태 원장 유지 |

각 exact 제외 route도 executable registry에 1:1로 고정되어 있으며, 알려지지 않은 exclusion은 startup을 실패시킨다.

## 3. Projection catalog

| 종류 | 기록 |
| --- | --- |
| `ExactScalar` | boolean, 정수/소수, 날짜·시각, UUID, `table.column` 고정 allowlist의 status/state/type/priority/code/unit/currency/result/action/stage/kind/mode/source와 허용된 첨부 filename/MIME/byte size |
| `MetadataOnly` | 일반 text/json의 before/after 원문 없이 bounded 길이만 기록 |
| `Excluded` | password, token, authorization, cookie, secret, payload, request/response/exception/raw body, binary, content/data/hash와 첨부 body |

첨부 filename/MIME exact 허용은 attachment relation allowlist 안에서만 적용한다. 실패 사건은 request body·exception message 없이 fixed reason code와 fixed 한국어 요약만 기록한다. IP·browser/OS family는 Login 사건에만 저장한다.

## 4. Tracked relation과 transaction contract

- migration이 존재를 검증한 94개 relation 각각에 `AFTER INSERT OR UPDATE OR DELETE` trigger를 만든다.
- 전체 migration schema의 145개 relation을 tracked `94` 또는 사유가 고정된 exclusion `51`로 exact 분류한다. 신규·삭제 relation이 어느 쪽에도 없거나 stale이면 contract test가 실패한다.
- relation exclusion은 신규 global audit infrastructure, 기존 canonical ledger, provider/worker·생성 artifact, operation/import/idempotency, seed reference data로만 허용한다.
- request correlation별 성공 parent는 1건이며 실제 변경 field child가 하나도 없으면 parent도 만들지 않는다.
- audit append가 실패하면 동일 business transaction도 rollback한다.
- business transaction rollback이면 parent와 child도 함께 사라진다.
- request context가 없는 worker·scheduler·provider 처리는 신규 성공 원장을 만들지 않는다.
- runtime role은 audit table 직접 `INSERT/UPDATE/DELETE`가 없고 security-definer append 함수 실행과 조회만 허용된다.
- 신규 audit table은 update/delete trigger도 거부하며 purge endpoint가 없다.

## 5. Catalog·대표 execution 검증 결과

- endpoint exact registry: `185/185`, missing `0`, stale `0`
- included/excluded: `156/29`
- tracked relation trigger: `94/94`
- schema relation classification: `145/145` = tracked `94` + explicit exclusion `51`
- 구매 필수항목 기준정보: 누락 발견 뒤 template·row 2개를 tracked relation에 추가하고 exact item code·metadata-only 자유문을 실제 DB로 검증
- local concurrent mutation: 시작을 함께 연 50개 저장의 업무 row `50`, 성공 parent `50`, field child 연결 `50`
- 성공 commit, accepted no-op, rollback, forbidden value, append-only, runtime privilege, login idempotency·owner, 실패/권한 통합 query: 통과
- 전체 Backend regression: `567/567`
- PostgreSQL migration class: `59/59`
- Frontend regression: `235/235`
- independent read-only re-verification: `PASS`, Open P0/P1/P2 `0/0/0`, local release candidate `READY`
- local visual: desktop list/detail와 연결 로그인 확인, narrow viewport client width `375`, page scroll width `375`, mobile card `3`, desktop table display `none`

Change 002 승인에 따라 exact endpoint·relation catalog와 중앙 middleware/trigger의 실제 PostgreSQL transaction semantics를 v1 acceptance 기준으로 사용한다. 이 증빙은 분류 누락 0, 성공 commit·accepted no-op·rollback·privacy·append-only·runtime privilege와 local 동시 mutation 50건을 증명한다. 포함 route 156개 각각의 업무 규칙을 실행 증명하는 1:1 matrix로 해석하지 않으며, route별 업무 동작은 기존 endpoint/store 회귀와 향후 변경 PR의 해당 기능 테스트가 계속 책임진다.

## 6. 운영 변경 규칙

새 mutation endpoint 또는 schema relation을 추가하는 변경은 같은 PR에서 registry 분류와 contract test를 갱신해야 한다. 단순히 application이 실행되도록 route·relation을 제외 목록에 넣는 것은 허용되지 않으며, 제외에는 기존 canonical 원장 또는 durable 업무 변경이 아닌 근거가 필요하다. 400/422는 `Validation`, 409/412는 `Conflict`로 기록하며 endpoint·action 이름으로 `Duplicate`를 추정하지 않는다.
