# UAT-VERIFY-001 SOP

## 1. 검증 목적

Persistent UAT를 변경하지 않고 migration, schema, aggregate, 권한, 알림 운영 수치, Review-safe 방어와 주요 화면을 반복 검증한다.

## 2. 환경 구분

| 환경 | 기본 주소 | 용도 |
| --- | --- | --- |
| Development | 5174 / 5081 | 저장·수정·worker가 필요한 별도 UAT |
| Preview | 5185 | frontend 비교 보조 |
| Review-safe | 5190 / 5092 | 공식 read-only 통합 검증 |
| Candidate | 5191 / 5093 | rollback 비교 |
| Full-Stack E2E | 자동 할당 | 전용 tmpfs PostgreSQL |

Review-safe 검증 중 Development, Preview, Candidate, PostgreSQL을 종료하거나 재시작하지 않는다.

## 3. 사전 Git 확인

1. `AGENTS.md`, 완료 정책, Roadmap과 선행 Task 문서를 읽는다.
2. `git worktree list`, local/remote main, open PR, stash를 확인한다.
3. 검증 전용 branch/worktree가 최신 origin/main에서 clean하게 생성됐는지 확인한다.
4. 기존 dirty worktree와 중단 branch는 수정하지 않는다.
5. runtime HEAD가 다르면 commit SHA가 아니라 backend/frontend/config/script/migration file diff를 확인한다.

## 4. Runtime tree 확인

- current Review-safe runtime file diff가 최신 main 대비 0인지 확인한다.
- 문서 전용 차이는 별도 기록한다.
- runtime diff가 있으면 최신 main 성공으로 표시하지 않고 controlled handover를 요청한다.

## 5. Review-safe health 확인

Response body 전체를 출력하지 않고 다음 field만 projection한다.

- mode = ReviewSafe
- mutationAllowed = false
- backgroundWorkersEnabled = false
- externalProvidersEnabled = false
- databaseReadOnly = true
- migrationExecutionEnabled = false
- migrationLedgerStatus = Exact 또는 CompatibleWithApprovedLegacy
- live/ready = 200

Connection string, credential, identifier와 전체 JSON은 출력하지 않는다.

## 6. Migration / schema 확인

1. `database/migrations/*.sql` basename을 정렬해 canonical set을 만든다.
2. prefix 중복·누락, version 중복, expected latest를 확인한다.
3. `schema_migrations`는 read-only session에서 전체 set을 비교한다.
4. approved legacy 외 unknown extra와 missing canonical이 0인지 확인한다.
5. information_schema와 pg_catalog로 lifecycle, notification delivery, access scope, FK/index/check constraint를 확인한다.
6. DDL, migration apply, marker 수정·삭제는 절대 수행하지 않는다.

## 7. 데이터 aggregate 확인

다음 table의 row count와 fixed status count만 기록한다.

- projects, work_items
- notifications, notification_recipients, notification_deliveries
- work_item_escalations
- qms_users, departments, system_holidays
- schema_migrations

DB session에는 `default_transaction_read_only=on`과 검증 전용 `application_name`을 사용한다. 사용자명, 프로젝트명, 제목, 본문, 이메일, GUID/ID는 출력하지 않는다.

## 8. 참조 무결성 확인

- project/work item/user orphan
- status와 completed/cancelled timestamp 모순
- notification recipient/delivery orphan와 link mismatch
- RecipientOnly 무수신자 후보
- escalation orphan/duplicate/closed-active/resolution mismatch
- deletion request/schedule/purge/pre-delete state mismatch

Nonzero 결과는 원문 대신 category/count와 synthetic 여부만 추가 분류한다. 데이터는 수정하지 않는다.

## 9. Notification / dashboard 확인

1. delivery status, channel, handling, retry, attempt bucket을 aggregate한다.
2. error는 recipient/auth/configuration/transport/other fixed category로만 분류한다.
3. dashboard Failed/Pending와 detail open row count를 비교한다.
4. active escalation total과 L0~L3 합계를 비교한다.
5. 실제 오류 메시지, recipient, provider ID는 출력하지 않는다.

## 10. 권한 확인

- Administrator admin GET 200
- 일반/업무 역할 admin GET 403
- RecipientOnly recipient 200, nonrecipient 403과 list 미노출
- Authenticated notice active user 200
- AdminOnly fixture가 있으면 admin 200/general 403
- 다른 사용자의 work item detail 403 또는 404 비노출

동적 ID는 내부 변수로만 쓰고 stdout/report에 남기지 않는다. Mutation이나 read 처리 endpoint는 호출하지 않는다.

## 11. Mutation 차단 확인

Dummy path를 대상으로 POST/PUT/PATCH/DELETE와 unsafe method override를 보낸다.

- expected HTTP: 423
- expected code: `UatReviewReadOnly`
- body는 fixed field와 message equality boolean만 projection

실제 업무 row를 가리키는 ID를 사용하지 않는다.

## 12. 개인정보 안전 browser 검증

금지:

- DOM/innerText/textContent/outerHTML/accessibility 원문
- screenshot, trace, video
- response/request body와 console message 원문
- cookie/localStorage/sessionStorage
- 사용자·프로젝트·알림·업무 원문과 identifier

허용:

- fixed route alias
- HTTP status
- boolean/integer/fixed enum/count
- selector 존재와 geometry
- console/request error count
- page/table overflow pixel count

Output schema guard가 허용 key/string enum을 검사해야 한다. Synthetic email, GUID, HTML, long token, free-form string negative fixture가 모두 차단되지 않으면 중단한다.

## 13. Desktop / mobile 확인

Desktop과 390px에서 root, projects, my-work, notifications, Teams Activity, admin, users, holidays, deliveries, escalations, manual send와 fixed fallback을 확인한다.

기대:

- page/structure/banner true
- blank/target-not-found false
- enabled mutation control 0
- console/request error 0
- page overflow 0
- next-action guidance true

## 14. Table 정렬 확인

개인정보 없는 selector geometry로 다음을 확인한다.

- project list, my-work list
- delivery, escalation, permission, user, department, holiday table
- header/data column count
- left/width geometry mismatch
- mobile card 또는 table scroll fallback
- loading/empty/error contract

Wide table의 자체 scroll은 허용하지만 document page overflow는 0이어야 한다.

## 15. Snapshot 전후 비교

검증 전후 동일 aggregate와 최대 timestamp 변경 여부를 비교한다. Container ID, health, restart count, volume, runtime PID도 비교한다.

- Review-safe write 성공 0
- delivery 신규 생성 0
- provider call 증거 0
- 설명할 수 없는 변화 0

Development worker 자연 변화가 있으면 category와 attribution boolean만 기록한다. 설명할 수 없으면 P2로 중단한다.

## 16. 자동 테스트

1. git diff --check, actionlint
2. frozen install, audit
3. backend Release build와 전체 test
4. Review-safe/migration/authorization/notification targeted test
5. frontend lint/typecheck/unit/build
6. mock UI E2E
7. Full-Stack E2E 전용 container/network/tmpfs
8. E2E resource 잔여 0
9. Markdown/secret/PII/allowlist 검사

Persistent UAT를 E2E DB로 사용하지 않는다.

## 17. 테스트 데이터 후보 분류

- Failed/Pending evidence: Acknowledged 또는 Dismissed 권장
- 완료된 test work item: Completed 권장
- 중단된 test work item: Cancelled 권장
- notification/recipient 원본: 추적용 보존
- synthetic master data: hard delete 금지, 승인된 lifecycle 처리

실제 정리는 별도 `TASK-UAT-DATA-001` 승인 후 수행한다.

## 18. Finding 기록과 Go/No-Go

Finding에는 심각도, 위치, 문제, 조건, 영향, 근거, 최소 수정, 필요한 test, 권장 Task ID를 적는다.

- P0/P1: 즉시 중단
- 신규 P2: 완료/Commit/PR 금지, remediation Task 제안
- P3: Roadmap/backlog 연결
- 성공으로 확인하지 않은 항목은 미확인으로 표시

## 19. 장애 대응

- Runtime diff: handover 요청, 검증 중단
- Ready 503: ledger/schema/read-only fixed field만 확인하고 중단
- Output guard 실패: 원문을 출력하지 말고 failure code만 기록
- DB change: runtime/application attribution을 확인하고 원인 불명 시 P2
- E2E cleanup 실패: 전용 project만 ownership 확인 후 정리, Persistent resource는 건드리지 않음

## 20. 금지사항

- Persistent UAT write/drop/truncate/reset
- migration/ledger 변경
- runtime 재시작
- worker/provider 수동 실행
- 실제 외부 알림
- raw browser/API/DB data 출력
- `git add .`, `git add -A`, force push, main 직접 push

## 21. 사용자 검수 체크리스트

- [ ] 5190 접속과 Review-safe banner 정상
- [ ] Migration 27/28/1 호환 상태 이해
- [ ] 프로젝트·업무·알림·관리자 조회 정상
- [ ] Dashboard/detail count 일치
- [ ] 알림 recipient scope 정상
- [ ] Mutation action disabled와 API 423
- [ ] 주요 표 정렬과 mobile overflow 정상
- [ ] 개인정보 원문이 보고에 없음
- [ ] 데이터 정리 권장안 이해
- [ ] SOP 반복 실행 가능
- [ ] 신규 기능 No-Go 유지 확인

## 22. 변경 이력

| 날짜 | 변경 |
| --- | --- |
| 2026-07-11 | 최신 main full-ledger Review-safe 기준선 재검증 절차 작성 |
