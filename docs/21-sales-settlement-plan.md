# TASK-014A — 영업 정산·세금계산서·프로젝트 완료 2차 기획 (실험 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-014A`
- authoringModel: `FABLE_5`

이 문서는 `tasks/014a-interview.md`(확인 완료 interview), `tasks/014a-planning.md`(Fable 1차 기획 전문), `tasks/014a-review.md`(Codex 내용 review 전문), `tasks/014a-change-001.md`(exact target 승인)와 현재 Repository 구현·tests를 직접 다시 읽고 작성한 TASK-014A의 최종 구현 source of truth다. 1차 기획의 유지 판정 내용을 보존하고 review의 유지·추가·보류·제거 권고와 Finding resolution 9건 전부를 구현 가능한 계약으로 통합했다. 1차 기획과 review 원문은 수정하지 않고 판단 이력으로 보존한다. 공통 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 따르며 여기에 복사하지 않는다. 이 문서는 experiment branch 구현·검증·local commit까지만 다루며 대표 repo·`main` merge(승인 0/3)·push·PR·Persistent UAT·실제 provider에 대한 어떤 승인도 부여하지 않는다.

## 1. 목표와 해결할 업무 문제

영업 담당자가 납품 완료된 프로젝트의 세금계산서 발행 정보를 프로젝트 맥락의 정산 화면에서 기록하면, 서버가 모든 active panel 납품·project 전체 open Pending 0건·발행일 기록을 한 transaction에서 재검증해 정산 record·정산 내 업무·프로젝트 상태·workflow event·인앱 완료 알림을 정확히 한 번 최종 완료한다.

- `LogisticsStore`의 납품 확정은 모든 active panel의 납품 판정으로 idempotency key `logistics:project:{projectId}:sales-settlement`의 `SalesSettlementCompleted` 내 업무 한 건과 `DeliveryCompleted` project stage event까지만 생성한다. 그 이후를 받는 정산 record·화면·transaction이 없다.
- 이 skeleton 업무는 `WorkflowStore.LinkUrlForWorkItem`의 fallback(프로젝트 workflow 요약)으로만 연결되고 generic 내 업무 완료 차단 목록(`TransitionWorkItemAsync`)에 없어 세금계산서·Pending·납품 조건 없는 완료 우회가 가능하다.
- `projects.status='Completed'`는 목록·진행률·병목·삭제 차단 등 조회 로직 전반에서 참조되지만 이를 설정하는 endpoint가 없어 프로젝트가 실제 완료 조건과 무관하게 영구히 Active로 남는다.
- 현재 `UpdateProjectAsync`는 Completed 상태를 차단하지 않고 `ChangePanelCountAsync`는 Cancelled만 차단하므로, 완료 개념을 추가하는 순간 완료 뒤 기본정보·면수 변경으로 완료 조건이 사후에 깨질 수 있다. 이 lifecycle fence는 이번 Task의 필수 범위다(review `014A-POST-COMPLETION-MUTATION`).

## 2. 범위

### 포함

- project당 1건 정산 record(Draft→Completed forward-only)와 세금계산서 발행일(필수)·번호·메모(선택 bounded) projection
- 프로젝트 맥락 정산 detail 조회(완료 조건 집계·draft·version·완료 가능 여부·완료 후 read-only)
- draft 저장(upsert·version)과 최종 완료의 원자 transaction: settlement `Completed`, 정산 내 업무 완료, `SalesSettlementCompleted` StageCompleted event exactly-once, `projects.status='Completed'`+완료 actor/시각, audit event, 인앱 완료 알림
- active panel 1개 이상 + Finalized 납품 relation 기준의 납품 완료 판정과 target type 무관 project 전체 non-`Closed` Pending 0건 검증
- 모든 Pending INSERT 경로에 적용되는 DB project lifecycle fence와 project-first lock order
- Completed 프로젝트의 기본정보 수정·면수 변경·일반 상태 mutation 서버 차단(기존 Completed 삭제 차단 유지)
- `sales.settle` 신규 permission(sales 업무 role 한정) + project scope + 영업 담당/current assignee actor 교집합
- `SalesSettlementCompleted`의 generic 내 업무 전이 차단과 project-context deep link
- operation fingerprint/replay, expectedVersion, row lock, stable 409 동시성 방어와 완료 후 immutability trigger
- 프로젝트 취소·승인된 permanent purge와 정산 데이터의 정합
- 프로젝트 상세 진입 + 내 업무 deep link 기반 모바일 우선 adaptive 정산 화면과 desktop project-context composition
- additive migration `0037`, Backend·Frontend·isolated Full-Stack E2E 검증, desktop·390px screenshot

### 제외 (review 보류·제거 확정)

- 전역 `영업 정산` 메뉴와 별도 전역 정산 queue 화면·API (대기 탐색은 기존 `내 업무` 재사용)
- System Administrator에 대한 `sales.settle` 부여
- 국세청·ERP·회계·전자세금계산서·결제·수금 외부 연동
- 세금계산서 파일 업로드·OCR·전자서명·PDF/Excel 생성
- 매출원가·마진·수금·채권·분할 청구·복수 세금계산서·통화 계산
- 완료 뒤 재오픈·세금계산서 취소·수정발행·프로젝트 재활성화 (별도 정책 Task로 보류)
- 신규 외부 알림 채널·실제 provider delivery, 프로젝트 완료의 Teams/Mail event 연결(Roadmap 6.5.2.2 후속 유지)
- Persistent UAT migration·write·runtime handover, 대표 repo·`main`·push·PR·merge

다음 방식은 review 판정에 따라 구현하지 않는다: delivery result 존재 count만의 납품 판정, active panel 0건의 공허한 완료, project/panel target으로 제한한 Pending 검사, generic 내 업무 완료를 통한 정산 종료, 완료 뒤 project 기본정보·면수·상태 mutation 허용, 알림 payload에 invoice 번호·Pending 원문 복제, 관리자 mutation permission 부여.

## 3. 역할과 권한 계약

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 영업 정·부 담당 | 정산 조건 확인, 세금계산서 draft 저장, 최종 완료 | 기존 project access scope | 아래 mutation 교집합 규칙 충족 시 |
| 생산관리·품질·제조·물류·조회 역할 | 허용 project의 정산·완료 상태 조회, 완료 알림 수신 | 기존 project access scope | 정산 mutation 불가 |
| Pending 조치 담당자 | 완료를 막는 open Pending 조치 | 기존 Pending scope | 기존 Pending 계약 안에서만. 정산 화면은 Pending을 임의 종결하지 않음 |
| System Administrator | 기준·감사·정산 상태 조회 | 기존 전체 조회 권한 | `sales.settle` 미보유로 정산 mutation은 permission 단계에서 403 |

정산 mutation 권한 교집합(`014A-ADMIN-LEAST-PRIVILEGE` resolution 반영):

1. 모든 정산 mutation은 신규 `sales.settle` permission과 대상 project의 access scope를 항상 요구한다. `sales.settle`은 additive migration에서 `sales` 업무 role에만 seed하고 System Administrator에는 부여하지 않는다. 관리자 조회는 기존 전체 project read/audit 권한으로 충분하다.
2. actor는 해당 project의 `SalesPrimary` 또는 `SalesSecondary` 배정자이거나, open(`Requested`/`InProgress`) `SalesSettlementCompleted` 내 업무의 current assignee여야 한다. 이 교집합은 endpoint policy가 아니라 store transaction 안에서 project row lock 뒤 최신 상태로 재검증한다.
3. 정산 detail 조회는 기존 project 조회 권한(`QmsPermissions.ProjectRead`) + project scope로 분리하고, scope 밖 식별자는 404로 비노출한다.
4. UI 숨김을 보안 수단으로 사용하지 않으며 어떤 역할도 store 검증을 우회하지 않는다.

## 4. 데이터 모델 계약 — additive migration `0037`

현재 latest는 `0036_logistics_execution`이다. `database/migrations/0037`(additive 1건)은 기존 migration을 수정·번호 재사용하지 않고, 기존 Active 프로젝트를 Completed로 backfill하지 않는다. 테이블·컬럼 최종 명명은 기존 convention에 맞춰 조정할 수 있으나 아래 계약 의미는 고정한다.

| 개념 | 계약 | 보존·감사 |
| --- | --- | --- |
| project 정산 record | project당 1건(unique project_id), status `Draft/Completed/Cancelled` check, invoice_issued_date(date, Completed 시 not null), invoice 번호(선택, trim 후 1~64자), 메모(선택, 최대 500자), version ≥ 1, 생성·수정 actor/시각, 완료 actor/시각(Completed 시 not null) | Completed row의 UPDATE/DELETE를 trigger로 차단(승인된 purge guard 경로만 예외), hard delete는 승인된 purge만 |
| 정산 operation receipt | operation_id PK, project/settlement 참조, action, actor, payload_fingerprint(sha256 hex), bounded result_projection jsonb, created_at | append-only, invoice 원문·Pending 원문 비복제 |
| projects 완료 metadata | `completed_at_utc`, `completed_by_user_id` nullable 컬럼 추가 | 완료 이력 보존, 덮어쓰기 금지 |
| Pending lifecycle fence | `pending_issues` BEFORE INSERT trigger: 대상 project row를 `FOR UPDATE`로 읽고 `Completed`/`Cancelled`/삭제된 project면 안정적 오류로 거부 | 모든 Pending 생성 경로(수동·자재·제조 중단·품질 등) 공통 적용 |
| permission seed | `sales.settle` permission 추가, role_permissions는 `sales` role에만 부여 | 관리자 read-only 권한 매트릭스에 자동 표시 |

Check 제약: `status='Completed'`이면 invoice_issued_date·완료 actor/시각 not null. `Cancelled`는 프로젝트 취소·삭제 정합 처리에서만 사용하는 terminal 상태이며 Completed에서 전이하지 않는다. fresh DB와 기존 migration 전체 적용 DB 모두에서 검증한다.

## 5. Backend API·transaction 계약

`/api/projects/{projectId}/settlement` project-context endpoint 3종을 신규 `Sales` 영역(contracts·store·endpoints)으로 구현하고 `Program.cs`에 등록한다. 전역 정산 queue endpoint는 만들지 않는다.

| Endpoint | 역할 | 권한 |
| --- | --- | --- |
| GET `…/settlement` | 완료 조건 집계(active panel 수, 납품 완료 수, open Pending aggregate 수), 정산 draft·version, 완료 가능 여부, 완료 결과 read-only projection | `ProjectRead` policy + project scope |
| PUT `…/settlement/draft` | 발행일·번호·메모 draft upsert, expectedVersion 검증 | `sales.settle` policy + scope + 3장 actor 교집합 |
| POST `…/settlement/complete` | operationId·payload fingerprint·expectedVersion·최종 invoice projection을 받아 저장·검증·완료를 단일 transaction으로 수행 | `sales.settle` policy + scope + 3장 actor 교집합 |

Transaction·동시성 계약:

1. lock order는 project-first로 고정한다: project row `FOR UPDATE` → settlement row lock → 조건 조회 → mutation. Pending INSERT fence trigger도 같은 project row를 lock하므로 completion과 Pending 생성은 어떤 순서로 와도 직렬화된다(`014A-PENDING-COMPLETION-RACE` resolution). 완료가 먼저면 이후 Pending insert는 Completed를 보고 실패하고, Pending이 먼저면 완료가 새 open Pending을 보고 409로 실패한다.
2. project 상태가 `Active`가 아니거나(OnHold/Cancelled/Completed) 삭제된 경우 draft 저장·완료를 모두 안정적인 한글 409/404로 차단한다.
3. 같은 operationId + 같은 fingerprint는 저장된 결과를 replay하고, 같은 operationId + 다른 fingerprint는 409로 충돌한다. stale expectedVersion은 409와 최신 상태 재조회 안내로 수렴한다. 동시 완료 경쟁은 정확히 1 success / 1 conflict다.
4. invoice validation: 발행일은 해당 project 생성일(Asia/Seoul 기준 date) 이상, Asia/Seoul 기준 오늘 이하만 허용한다(`014A-INVOICE-DATE-BOUNDARY` resolution). 번호는 trim 후 빈 문자열 금지·최대 64자, 메모 최대 500자를 server와 client 양쪽에서 bounded 검증한다.
5. 완료 transaction은 다음을 모두 포함해 전부 성공하거나 전부 rollback한다: settlement 최종 invoice 반영·`Completed` 전이(actor/시각), open `SalesSettlementCompleted` 내 업무 `Completed` 처리, `project_workflow_events`의 `SalesSettlementCompleted` StageCompleted exactly-once insert, `projects.status='Completed'`+완료 metadata, 기존 audit event 패턴의 `ProjectCompleted` 기록, 8장 인앱 알림 insert, operation receipt insert.

## 6. 완료 조건 계약

완료 시 서버가 project lock 이후 재검증하는 조건은 다음 세 가지이며 detail 조회 projection도 같은 판정을 사용한다.

1. 납품 완료(`014A-DELIVERY-SOURCE-OF-TRUTH`·`014A-ZERO-PANEL-VACUOUS-COMPLETION` resolution): active panel 수가 1개 이상이고, `NOT EXISTS`로 검사한 미납품 active panel이 0건이어야 한다. 납품 판정은 delivery result row 존재만 세지 않고 Finalized `DeliveryCompleted` batch — active batch-unit — active packing-unit-panel membership — delivery result의 결합으로 각 active panel의 납품 완료를 판정한다. active panel 0건인 프로젝트는 별도 조건으로 완료를 차단한다.
2. open Pending 0건(`014A-PENDING-SCOPE` resolution): target type 제한 없이 `pending_issues`에서 해당 project의 status가 `Closed`가 아닌 row 전체를 차단 대상으로 센다. UI·API에는 aggregate count와 Pending 목록의 project filter 이동 링크만 제공하고 Pending 제목·본문 원문을 정산 API 응답·알림에 복제하지 않는다.
3. 세금계산서 발행 완료: settlement record에 4장 boundary를 통과한 발행일이 기록되어 있어야 한다(완료 요청에 포함된 최종 projection 저장 후 판정).

## 7. Completed lifecycle fence 계약

`014A-POST-COMPLETION-MUTATION` resolution으로 다음을 이번 Task에서 함께 구현한다.

- `UpdateProjectAsync`: project 편집 snapshot을 lock으로 읽은 뒤 `Completed`면 안정적인 한글 409를 반환한다.
- `ChangePanelCountAsync`: 기존 `Cancelled` 차단에 `Completed` 차단을 추가한다.
- 상태 mutation: hold/resume/cancel의 기존 allowed source status 집합이 `Completed`를 포함하지 않음을 테스트로 고정하고, `ChangeStatusAsync`가 lock 뒤 상태를 확인하는 기존 구조를 유지한다. Completed 삭제 차단(기존)과 Completed→재활성 부재를 회귀 테스트로 보존한다.
- `TransitionWorkItemAsync`: generic start/complete/cancel 차단 목록에 `SalesSettlementCompleted`를 추가하고 전용 화면 안내(destination은 project 정산 route)를 반환한다.
- 프로젝트 취소: `ChangeStatusAsync`의 기존 Cancel hook 패턴으로 draft settlement를 `Cancelled` terminal 처리한다(완료된 settlement는 프로젝트 취소 자체가 불가능하므로 발생하지 않는다).
- 승인된 permanent purge: purge guard 아래 FK 역순에 settlement·operation receipt를 추가한다. Completed 프로젝트는 삭제·purge가 차단되므로 purge는 Draft/Cancelled settlement만 만난다.

## 8. Workflow·알림 통합 계약

- 내 업무 deep link: `LinkUrlForWorkItem`에 `SalesSettlementCompleted` + Project target 분기를 추가해 project-context 정산 route로 연결한다. 전역 메뉴·전역 queue는 추가하지 않는다(`014A-NAVIGATION-SCOPE` resolution). 대기 탐색은 기존 `내 업무`의 project target 업무 목록을 재사용한다.
- 완료 알림(`014A-NOTIFICATION-BOUNDARY` resolution): 완료 transaction 안에서 기존 `notifications` 인앱 원본만 생성한다. recipient는 해당 project의 active distinct `project_assignees` 사용자와 sales owner 중 actor를 제외한 집합으로 제한하고, recipient별 unique idempotency key로 중복 0을 보장한다. payload는 프로젝트 완료의 고정 category·제목·deep link만 담고 invoice 번호·메모·Pending 원문을 복제하지 않는다. 외부 채널 delivery row와 실제 provider 발송은 생성·실행하지 않는다.
- `DeliveryCompleted`까지의 기존 물류 계약(skeleton 생성·stage event)은 변경하지 않는다.

## 9. Frontend·UX 계약

- route: `App.tsx`의 기존 project 하위 route 패턴(정규식 match)으로 project-context 정산 route를 추가하고, 프로젝트 상세의 `정산·완료` action/section과 내 업무 deep link를 primary entry로 사용한다. desktop/mobile 모두 같은 route를 사용하며 전역 메뉴 항목은 추가하지 않는다.
- 신규 정산 페이지 component와 type·API client를 013A 물류 페이지의 adaptive 패턴을 준용해 구현한다.
- 화면 구성: `완료 조건(납품 panel 수/전체·open Pending 수와 이동 링크·발행 상태) → 세금계산서 입력(발행일 필수·번호·메모, draft 저장) → 최종 확인(변경 불가 경고·완료 버튼)`의 모바일 390px 한 열 흐름과 desktop project-context composition. 완료된 프로젝트는 같은 route에서 read-only 결과(발행일·완료 actor/시각·조건 충족 요약)를 표시한다.
- feedback: loading/empty/error/success 구분, draft 저장·완료 결과를 action 근처 표시, 중복 submit 차단, 409 한글 안내 후 최신 상태 재조회, error focus와 `aria-live`.
- 접근성·모바일 원칙: 44px 핵심 touch target, 작은 보조 글씨와 원형·타원·각진/둥근 사각형의 의미별 사용, 좌상단 숨김 메뉴 유지(하단 고정 메뉴 없음), page-level horizontal overflow 0, Teams narrow 검증.

## 10. Review Finding resolution

| ID | Severity | 상태 | 이 계약의 resolution과 검증 위치 |
| --- | --- | --- | --- |
| `014A-PENDING-COMPLETION-RACE` | P1 | `RESOLVED_IN_PLAN` | 5장 project-first lock + 4장 `pending_issues` INSERT fence trigger. 검증: 완료 vs Pending insert 동시성 test(양방향 순서), trigger 단위 PostgreSQL test |
| `014A-POST-COMPLETION-MUTATION` | P1 | `RESOLVED_IN_PLAN` | 7장 `UpdateProjectAsync`·`ChangePanelCountAsync`·상태 mutation Completed 409. 검증: 완료 후 edit/panel/hold/cancel/삭제 차단 API test |
| `014A-DELIVERY-SOURCE-OF-TRUTH` | P2 | `RESOLVED_IN_PLAN` | 6장 Finalized batch·active membership 결합 + `NOT EXISTS` 판정. 검증: draft/cancelled owner가 섞인 fixture의 완료 차단 test |
| `014A-PENDING-SCOPE` | P2 | `RESOLVED_IN_PLAN` | 6장 target type 무관 project 전체 non-`Closed` 차단. 검증: 비 project/panel target open Pending fixture의 완료 차단 test |
| `014A-NAVIGATION-SCOPE` | P2 | `RESOLVED_IN_PLAN` | 8·9장 project detail + 내 업무 deep link 고정, 전역 메뉴·queue 미구현. 검증: Frontend unit·E2E의 진입 경로와 메뉴 부재 확인 |
| `014A-ADMIN-LEAST-PRIVILEGE` | P2 | `RESOLVED_IN_PLAN` | 3·4장 `sales.settle`을 sales role에만 seed. 검증: migration seed test와 System Administrator mutation 403 API test |
| `014A-INVOICE-DATE-BOUNDARY` | P2 | `RESOLVED_IN_PLAN` | 5장 project 생성일 ≤ 발행일 ≤ Asia/Seoul 오늘, 번호·메모 bounded. 검증: 경계값(전일·당일·미래·생성일 이전) validation test |
| `014A-ZERO-PANEL-VACUOUS-COMPLETION` | P2 | `RESOLVED_IN_PLAN` | 6장 active panel ≥ 1 별도 조건. 검증: active panel 0건 프로젝트 완료 차단 test와 detail projection test |
| `014A-NOTIFICATION-BOUNDARY` | P2 | `RESOLVED_IN_PLAN` | 8장 bounded recipient·category/deep link only·provider 미생성. 검증: recipient 집합·중복 0·payload 원문 부재·delivery row 0 test |

review의 제거 판정(전역 정산 menu/queue, 관리자 `sales.settle`)과 보류 판정(완료 뒤 재오픈·수정발행)은 2장 제외 범위에 반영했고, 유지 판정(record 분리, 발행일 필수·번호/메모 선택, 전용 permission, actor 교집합, 알림 인앱 한정, generic 차단, append-only)은 1차 기획 내용 그대로 보존했다.

## 11. 검증 계획

- Migration: fresh DB와 기존 migration 전체 적용 DB의 `0037` 적용, schema·constraint·trigger·permission seed 검증(`PostgreSqlMigrationTests` 패턴).
- Backend 필수 test: draft 저장·수정·version, 완료 성공 경로(settlement·work item·event·project 상태·알림·receipt 원자성), 6장 세 조건 각각의 차단, OnHold/Cancelled/Completed/삭제 차단, scope 밖 404, 비담당 sales·System Administrator·타 부서 차단, 동일 operation replay·다른 fingerprint 충돌, 동시 완료 1 success/1 conflict, 완료 vs Pending insert race 양방향, generic 전이 차단, 완료 후 mutation fence 전체, settlement immutability trigger, 프로젝트 취소·purge 정합, 알림 recipient·idempotency.
- 영향 영역 회귀: Backend Release 전체 test, 물류(013A)·Pending·workflow·프로젝트 관련 기존 test, Frontend lint·typecheck·unit·build.
- Full-Stack E2E: isolated PostgreSQL에서 납품 확정 → 내 업무 deep link → 정산 화면 → draft → 최종 완료 → project Completed·read-only·알림 확인, 390px 포함.
- 사용자 검수 증빙: 페이지별 desktop·390px(및 Teams narrow) screenshot을 생성하되 사용자 검수 완료로 표시하지 않는다. 실제 사용자·프로젝트 원문 대신 synthetic data와 익명 역할명만 사용한다.

## 12. 완료 기준과 중단 조건

- 기능/권한/데이터: 2장 포함 범위 전부 구현, 5~8장 계약 위반 0, 10장 Finding 9건의 검증 위치 전부 통과.
- UX: 9장 화면·모바일 원칙 충족, page-level overflow 0.
- 산출물: implementation report·SOP·user manual·Roadmap experiment 상태 갱신·user validation checklist의 상태·위치 추적, `사용자 검수 대기`로 종료, local experiment commit까지만 수행.
- 중단 조건: Repository 충돌, secret·개인정보 노출, 18단계 순서·모든 panel 납품·open Pending 0건·완료 이력·권한 불변조건 위반이 의심되면 fast-track으로 우회하지 않고 blocking decision으로 중단·보고한다.
- 이 문서는 push·PR·merge(`main` 승인 0/3)·Persistent UAT·실제 provider·게시에 대한 승인을 부여하지 않는다.

## 13. 후속 경계 (Deferred)

- 완료 뒤 정정·재오픈·수정발행 정책 (별도 NEW_FEATURE/POLICY Task)
- 세금계산서 확장 필드와 외부 회계·전자세금계산서 연동 (Roadmap 추적 21)
- 프로젝트 완료의 Teams/Mail 외부 채널 event 연결 (Roadmap 6.5.2.2)
- 영업 정산 Excel export (TASK-EXPORT-001)

## 14. Codex 구현 순서

1. additive `0037` migration: settlement·operation receipt·projects 완료 metadata·immutability trigger·`pending_issues` INSERT lifecycle fence·`sales.settle` sales-only seed. fresh/existing DB 검증.
2. Backend `Sales` domain: project-first lock, actor/scope 교집합, detail/draft/complete, 6장 세 조건, fingerprint replay·version·stable 409.
3. 완료 lifecycle fence: `ProjectStore` Completed mutation 차단, `WorkflowStore` generic 차단·deep link, cancel/purge 정합.
4. project-context Frontend: 완료 조건 → invoice 입력 → 최종 확인 한 열과 desktop composition, completed read-only.
5. authorization·concurrency·race·fresh/existing migration·Full-Stack E2E 검증.
6. desktop·390px screenshot, implementation report·5종 산출물, local experiment commit.

openBlockingDecisionCount: 0
