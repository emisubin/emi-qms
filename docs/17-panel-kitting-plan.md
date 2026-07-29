# TASK-010A — 패널별 키팅 완료·제조 내 업무 생성 2차 기획 (구현 계약)

> 상태: 구현 source of truth (experiment two-pass 2차 기획)
> 목적: Fable 1차 기획(`tasks/010a-planning.md`)의 승인된 방향과 Codex 내용 review(`tasks/010a-review.md`)의 모든 resolution을 하나의 구현 가능한 최종 계약으로 통합한다.

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-010A`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/010a-interview.md`
- firstPlanningSource: `tasks/010a-planning.md`
- codexReviewSource: `tasks/010a-review.md`
- approvalChangeSource: `tasks/010a-change-001.md`
- fastTrackMode: `EXPERIMENT_TWO_PASS`

이 문서는 `experiment/task-010a-panel-kitting` branch 한정 구현 계약이다. 대표 repo, GitHub `main`, push·PR·merge, Persistent UAT migration·write·runtime handover, 실제 provider 발송은 어떤 것도 승인하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 따르며 여기에 복사하지 않는다.

## 1. 한 줄 목표

자재 담당자가 모바일에서 입고 준비가 끝난 프로젝트의 패널을 선택해 한 번에 키팅 완료하고, 제조 담당자가 패널별 내 업무를 중복 없이 받아 제조 투입 가능 상태를 시스템 데이터로 확인할 수 있게 한다.

## 2. 배경과 결정 요약

008A~009A 실험 계보는 구매품목별 도착·IQC·입고 확정과 trigger로 보호되는 derived `receipt_completed` 단일 진실(migration `0030`~`0032`)까지 구현했다. 18단계의 8번(키팅 완료)은 workflow stage catalog(`KittingCompleted`, 자재 담당, 다음 단계 `ManufacturingWork`)와 `work_items.target_type='Panel'` 기반만 존재하고 실제 데이터·화면·mutation·제조 handoff가 없다. `panel_placeholders.workflow_stage`의 CHECK 제약(`0007`)에는 키팅 값이 없으므로 키팅 완료는 별도 불변 record로 표현한다.

1차 기획에서 유지가 확정된 방향(모두 이 계약에 포함):

- 부분 키팅 = 프로젝트 활성 패널 선택 단위 완료, BOM·패널 내부 자재 allocation 제외
- 모든 active 구매품목의 derived `receipt_completed=true`를 요구하는 보수적 readiness
- 패널당 불변 완료 record와 기존 `work_items`·`project_workflow_events`·인앱 notification 재사용
- 패널당 제조 내 업무 1건과 stable idempotency key, batch all-or-nothing transaction
- `panel_placeholders.workflow_stage` 불변, 제조 시작 전이는 `TASK-011A` 위임
- readiness 최초 충족 시 자재 “패널 키팅” 내 업무 생성, 마지막 활성 패널 완료 시 stage event·묶음 인앱 알림
- `/materials/kitting` 모바일 우선 queue·패널 카드·일괄 선택, `0033` additive migration, Persistent UAT·provider·대표 repo 제외

Codex review resolution으로 이번 계약에서 바뀐 것(1차 기획 대비):

| Review Finding | 이 계약의 확정 |
| --- | --- |
| `010A-WORKFLOW-DUPLICATE` (P1) | `WorkflowStore.CompleteStageAsync` 호출 금지. 키팅 store가 같은 transaction 안에서 StageCompleted event 최대 1건과 패널별 제조 업무만 직접 생성(9장) |
| `010A-RETRY-AMBIGUITY` (P1) | client 생성 `operationId` 기반 batch identity. 동일 operation+동일 payload는 저장된 성공 결과 replay, 그 외 재사용·중복은 conflict(8·9장) |
| `010A-ORPHAN-HANDOFF` (P1) | 제조 assignee를 해석하지 못하면 completion 포함 전체 rollback과 조치 가능한 한글 오류(9장) |
| `010A-READ-SCOPE` (P1) | queue·mutation 모두 permission + 기존 `ProjectAccessScope` 프로젝트 접근 범위를 함께 강제, scope 밖은 일관된 not-found(4장) |
| `010A-PURGE-LIFECYCLE` (P2) | soft-delete 보존과 승인된 permanent purge 정리를 구분. purge transaction이 키팅 batch/completion을 명시적으로 삭제하고 회귀 테스트 추가(11장) |
| `010A-READINESS-HOOK` (P2) | 공통 readiness predicate 1개를 queue·mutation·transition hook이 공유. `ConfirmAsync`·`CloseArrivalsAsync` 두 경로 모두에서 false→true 전환 시 키팅 업무 생성, queue·mutation은 업무 유무와 무관(10장) |
| `010A-CANCELLED-PANEL-WORK` (P2) | 패널 취소 경계에서 open 제조 업무를 Cancelled로 전환(불가하면 안전 차단 + Finding), 완료 record는 보존(11장) |
| `010A-STAGE-RACE` (P2) | mutation 초기 project row `FOR UPDATE` 직렬화와 기존 event 존재 확인으로 stage event·묶음 알림 exactly-once(9장) |
| `010A-MODULE-BOUNDARY` (P3) | 전용 `PanelKittingStore`/contracts/endpoints와 `PanelKittingPage`/전용 type·API helper로 분리, 기존 대형 자재 파일에 누적 금지(12장) |

Review의 제거 판정 6건(무조건 conflict 재시도, generic stage completion 호출, assignee 없는 silent 성공, 무범위 queue, “절대 삭제 없음” 과장 문구, 대형 파일 직접 확장, canceled panel open 업무 방치)은 이 계약에 포함되지 않는다.

## 3. 포함·제외 범위

### 포함

- 프로젝트별 키팅 readiness 요약과 활성 패널 대기/완료 조회(queue)
- operationId 기반 단건·일괄 키팅 완료(all-or-nothing, 성공 replay)
- 패널당 불변 완료 record·batch record와 readiness snapshot(bounded aggregate)
- 패널당 제조 내 업무 exactly-once 생성과 assignee 필수 rollback
- readiness 전환 hook의 자재 “패널 키팅” 업무 생성, 마지막 패널 stage event·묶음 인앱 알림
- 패널 취소 시 open 제조 업무 정리, permanent purge 정합과 회귀 테스트
- 전용 Backend module·Frontend page, `0033` additive migration, Backend·Frontend·isolated E2E 검증과 desktop·390px screenshot

### 제외

- 제조 체크리스트·작업 시작/종료·중단(`TASK-011A`)과 `panel_placeholders.workflow_stage` 전이·제약 변경
- BOM·패널별 자재 소요량·패널 내부 부분 키팅·창고 재고 차감·생산 불출
- 키팅 완료 되돌리기·재키팅·관리자 강제 정정
- 기존 ready 프로젝트에 키팅 업무를 소급 생성하는 운영 reconcile(보류 — Persistent 적용 시 별도 판단)
- 첨부·사진·Excel·PDF, 외부 notification delivery·신규 채널
- `0030`~`0032` migration과 008A·008B·009A 상태 machine 수정
- Persistent UAT write·migration 적용·runtime handover, 대표 repo·`main`·push·PR·merge

## 4. 사용자·권한·접근 범위

| 사용자/역할 | 행동 | 강제 경계 |
| --- | --- | --- |
| 자재 담당 | queue 조회, 패널 선택, 단건·일괄 완료 | mutation은 `MaterialReceiptUpdate` policy + project 접근 scope. 활성 프로젝트·활성 패널의 완료 생성만 |
| 제조 담당 | 완료 패널·자기 제조 내 업무 조회 | 같은 읽기 scope. mutation은 UI 숨김이 아니라 서버에서 차단 |
| 생산관리·구매·품질 | 프로젝트 입고/키팅 요약 조회 | 인증 + `ProjectRead` policy + scope |
| Read-only·System Administrator | 승인된 조회·감사 | 업무 mutation 우회 금지(policy 요구사항 동일 적용) |

- 신규 permission·policy를 추가하지 않는다.
- queue·mutation 모두 기존 `ProjectAccessScope`(`Project.Read.All` claim 또는 사용자별 project key 목록, `ProjectStore` 적용 방식) 규약을 재사용한다. permission 존재만 확인하는 무범위 조회를 금지한다.
- scope 밖 또는 존재하지 않는 project/panel ID는 구분 없이 일관된 not-found로 응답해 ID 추측으로 존재 여부가 노출되지 않게 한다.
- 응답·오류·snapshot에 내부 row identifier·assignee 원문·고객/품목명 원문을 노출하지 않는다. 패널은 사용자-facing `display_code`로 표기한다.

## 5. 핵심 사용자 시나리오

### 시나리오 A — 일괄 키팅 완료와 제조 handoff

1. 자재 담당이 `/materials/kitting`(또는 “패널 키팅” 내 업무 deep link)에서 접근 가능한 프로젝트의 readiness 요약(전체 active 품목 대비 입고 완료 수)과 키팅 대기/완료 패널 수를 본다.
2. 준비 완료 프로젝트에서 패널 카드 일부 또는 전체를 선택하고 `선택 패널 키팅 완료`를 누른다. client는 이 제출에 대해 `operationId` 하나를 생성하고 재시도에 재사용한다.
3. 서버가 한 transaction에서 project lock → replay/충돌 판정 → panel lock·재검증 → readiness 재평가 → 제조 assignee 해석 → batch·completion·패널별 제조 업무·(마지막 패널이면) stage event·키팅 업무 완료·묶음 알림을 처리한다. 하나라도 실패하면 전체 rollback이다.
4. 화면은 action 인접에 “N개 패널 키팅 완료 · 제조 내 업무 N건 생성”을 표시하고, 제조 담당자는 내 업무에서 `제조 작업 · {패널 표시 코드}` 업무를 확인한다.

### 시나리오 B — 부분 키팅과 마지막 패널

1. 일부 패널만 완료하면 남은 패널은 계속 대기로 표시되고 이후 별도 operation으로 완료한다. 완료 패널은 선택 대상에서 제외되고 완료 badge·actor·시각이 표시된다.
2. 마지막 활성 패널이 완료되는 batch에서 `KittingCompleted` StageCompleted event 1건, “패널 키팅” 업무 완료, 생산관리·제조 참조 묶음 인앱 알림 1건이 같은 transaction으로 기록된다.

### 시나리오 C — 재시도·동시성 복구

1. network 유실 후 같은 선택을 같은 `operationId`로 재요청하면 서버는 write 없이 저장된 성공 결과(완료 패널 수·생성 업무 수)를 반환하고, 화면은 “이미 처리된 요청입니다” 성격의 성공 안내를 보여준다.
2. 같은 `operationId`에 다른 payload, 또는 새 operation에 이미 완료된 패널이 포함되면 전체 conflict와 최신 상태 재조회 안내를 반환한다.
3. 같은 패널을 포함한 두 batch가 동시에 실행되면 project·panel lock과 unique 제약으로 한쪽만 커밋되고, 마지막 패널을 동시에 주장해도 stage event·알림은 1건이다.

### 시나리오 D — 차단·lifecycle 경로

1. readiness 미충족·활성 아님·패널정보 미완료·scope 밖·권한 없음은 각각 구분된 안정적 한글 오류(scope 밖은 not-found)로 차단된다.
2. 제조 담당자를 끝내 해석하지 못하면 “제조 담당자를 지정한 뒤 다시 시도해 주세요” 오류와 함께 아무것도 커밋되지 않는다.
3. 키팅 완료 패널이 이후 취소되면 완료 record는 보존되고 open 제조 업무만 취소된다. soft-delete·보류·취소 프로젝트는 신규 mutation이 차단되고 기존 기록은 조회 보존된다. 승인된 permanent purge는 프로젝트 소유 키팅 데이터를 함께 정리한다.

## 6. 업무 규칙과 불변조건

- 입고 확정 상태와 derived `receipt_completed`·보호 trigger·`0030`~`0032` 상태 machine은 authoritative이며 변경·우회하지 않는다. 키팅은 이를 읽기만 한다.
- 키팅 완료는 활성 프로젝트의 활성·패널정보 완료 패널에만, readiness 충족 시에만 가능하고 서버가 transaction 안에서 전부 재검증한다.
- 키팅 완료는 전진-only다. 정상 업무와 soft-delete 경계에서 completion·batch의 update/delete API를 제공하지 않는다. 승인된 permanent purge는 별도 관리자 lifecycle로 구분하며 “영구 보존”으로 과장하지 않는다.
- 패널당 completion 1건·제조 내 업무 1건, operation당 batch 1건·묶음 알림 1건, 프로젝트당 `KittingCompleted` StageCompleted event 1건이 exactly-once 불변조건이다.
- 일괄 완료는 all-or-nothing이다. 부분 커밋·부분 성공 응답을 만들지 않는다.
- 제조 내 업무 없는 키팅 완료(고아 handoff)를 어떤 경로로도 만들지 않는다.
- 권한·scope는 서버 policy로 강제하고 System Administrator도 업무 mutation을 우회하지 않는다.
- 취소 패널은 queue·선택에서 제외한다. “모든 활성 패널 완료” 판정은 completion 없는 활성 패널이 0건인 상태를 뜻한다. 마지막 미완료 활성 패널이 취소로 사라지는 경우 stage event는 다음 키팅 batch가 없으면 생성되지 않는다 — 이 한계는 구현 report에 P3/backlog로 기록하고 이번 범위에서 패널 취소 경계에 stage 판정을 추가하지 않는다.

## 7. 데이터 모델과 migration `0033`

| 개념 | 구조(계약 수준) | 보존·감사 |
| --- | --- | --- |
| `panel_kitting_batches` | id PK, project_id FK(restrict), bounded `operation_id`(unique), actor user FK, 정렬된 panel set의 fingerprint(hash), 결과 projection(완료 패널 수·생성 업무 수·프로젝트 완료 여부), readiness snapshot(active 품목 수·완료 품목 수·검증 시각·predicate version 등 bounded 값), created_at_utc | append-only. replay·conflict 판정과 감사의 근거 |
| `panel_kitting_completions` | id PK, batch FK(restrict), project_id, panel_id FK(restrict) + unique(panel_id), completed_by, completed_at_utc | append-only. 패널당 1건이 exactly-once 최종 방어선 |
| 제조 내 업무 | 기존 `work_items` 재사용: `target_type='Panel'`, `target_id=panel`, stage `ManufacturingWork`, idempotency key `kitting:panel:{panelId}:manufacturing` | 기존 unique key·상태 lifecycle |
| “패널 키팅” 업무 | 기존 `work_items`: target Project, stage `KittingCompleted`, `MaterialsPrimary`, key `materials:kitting:{projectId}` | 기존 idempotency 패턴 |
| stage event | 기존 `project_workflow_events` `StageCompleted`(stage `KittingCompleted`) insert 규약 재사용 | append-only, 존재 확인 후 최대 1건 |
| 묶음 알림 | 기존 notifications insert 규약, key에 `operation_id` 포함 | 인앱 원본만, delivery 없음 |

```text
batch:   (없음) --operationId+panel set--> 단일 transaction 성공 --> Completed(불변)
           ├─ 동일 operation+동일 payload 재요청 --> 저장된 성공 결과 replay(write 없음)
           ├─ 동일 operation+다른 payload --> Conflict
           └─ 검증·assignee·동시성 실패 --> 커밋된 row 없음
panel:   (대기: completion 없음) --> Completed(불변)          ← 전진-only
제조 업무: (없음) --같은 transaction--> Requested,  Requested/InProgress --패널 취소--> Cancelled
stage:   마지막 활성 패널 완료 --project lock+기존 event 확인--> StageCompleted 최대 1건
```

Migration `0033`(additive 1건, 현재 최신 `0032` 이후): 위 2개 table과 CHECK·unique·FK·index만 추가한다. 기존 migration·trigger·`panel_placeholders.workflow_stage` CHECK는 수정하지 않고, 기존 패널 완료 추정 backfill이 없다. snapshot에는 aggregate 수치·시각·version만 저장하고 고객·품목명 원문을 복제하지 않는다. rollback은 forward-fix 원칙으로 문서화한다.

## 8. API 계약

구체 클래스·컬럼·SQL 문형은 기존 convention에 맞춰 구현 시 확정하되, 아래 동작 계약은 고정이다.

- `GET /api/materials/kitting` — 인증 + `ProjectRead` policy + `ProjectAccessScope`. 프로젝트별 readiness 요약(전체/완료 active 품목 수, 준비 여부), 활성 패널의 대기/완료 상태(완료 시 actor 표시명·시각·batch correlation), 선택 가능 여부. `projectId` query로 상세 조회. deleted 프로젝트 제외.
- `POST /api/materials/kitting/complete` — `MaterialReceiptUpdate` policy + scope. 요청: `operationId`(bounded, 필수), `projectId`, `panelIds`(1건 이상). 응답(성공·replay 동일 형태): `operationId`, 완료 패널 수, 생성된 제조 업무 수, 프로젝트 키팅 완료 여부, replay 여부. 내부 identifier·assignee 원문 미노출.
- 오류 계약: 검증 실패는 field-level 한글 400(“입고가 완료되지 않은 구매품목이 있습니다”, “이미 키팅 완료된 패널이 포함되어 있습니다: {표시 코드}”, “제조 담당자를 지정한 뒤 다시 시도해 주세요” 등), 경합·operation 충돌은 409와 재조회 안내, scope 밖·미존재는 일관된 not-found.
- 기존 `/api/materials/` 자재 endpoints의 응답 계약은 additive 필드 외 변경하지 않는다.

## 9. Transaction·동시성·idempotency 계약

mutation은 하나의 DB transaction에서 다음 순서로 처리한다.

1. project row `FOR UPDATE` — 존재·scope·활성(미삭제·미보류·미취소) 확인. 같은 프로젝트의 batch·마지막 패널 판정을 직렬화한다.
2. `operation_id` 조회 — 동일 operation이 있으면 project·actor·panel set fingerprint를 비교해 동일 payload면 저장된 결과를 반환(추가 write 없음), 다르면 conflict로 종료한다.
3. 선택 panel rows `FOR UPDATE` — 프로젝트 소속·Active·`panel_info_completed`·completion 미존재를 재검증한다. 하나라도 위반이면 전체 rollback.
4. readiness predicate 재평가(10장) — 미충족이면 전체 rollback.
5. 제조 assignee 해석 — 기존 workflow 배정 fallback(`ManufacturingPrimary` → `ManufacturingSecondary` → legacy `Manufacturing` 역할 배정)과 `manufacturing.update` 보유 active 사용자, 영업 정·부·System Administrator fallback 순서의 기존 `ResolveAssigneeAsync` convention을 재사용한다. 어떤 active 후보도 없으면 completion을 포함해 전체 rollback한다. silent 생략을 금지한다.
6. batch insert(결과 projection·snapshot 포함) → panel별 completion insert(unique(panel_id)가 최종 방어선) → panel별 제조 work item insert(`on conflict (idempotency_key) do nothing`, 기존 SQL helper 규약).
7. 완료 후 활성 패널 중 completion 없는 패널이 0건이면: 같은 lock 아래 기존 `StageCompleted`(stage `KittingCompleted`) event 존재를 확인하고 없을 때만 event 1건을 직접 insert하며, `materials:kitting:{projectId}` 업무를 기존 완료 helper 규약으로 Completed 처리하고, 생산관리·제조 참조 묶음 인앱 알림 1건(idempotency key에 `operation_id` 포함)을 생성한다.
8. commit. 모든 실패 경로는 전체 rollback이며 부분 상태를 남기지 않는다.

`WorkflowStore.CompleteStageAsync`는 호출하지 않는다. 이 method는 자체 connection/transaction을 열고 다음 stage의 프로젝트 단위 제조 업무(`project:{projectId}:stage:ManufacturingWork:work:ManufacturingPrimary`)를 생성하므로 패널별 업무와 중복되고 키팅 transaction과 원자성이 깨진다. 프로젝트 단위 `ManufacturingWork` 업무를 이 Task의 어떤 경로에서도 생성하지 않는다.

## 10. Readiness predicate와 전환 hook

- 단일 predicate: 프로젝트에 `project_procurement_items.status='Active'` 품목이 1건 이상 존재하고, 그 전체(Purchased·CustomerSupplied 모두)의 `receipt_completed=true`. individual receipt·IQC report 상태를 재계산해 두 번째 진실을 만들지 않는다.
- queue 표시와 mutation 재검증이 같은 predicate 구현 하나를 공유한다.
- 전환 hook: `MaterialsStore.ConfirmAsync`와 `CloseArrivalsAsync` 두 transaction 모두에서 derived projection 갱신 뒤 프로젝트 readiness가 false→true로 전환되면 “패널 키팅” 업무(`materials:kitting:{projectId}`, stage `KittingCompleted`, `MaterialsPrimary` 배정, `/materials/kitting?project=` deep link)를 idempotent하게 생성한다. Confirm 경로만 보는 구현을 금지한다.
- queue는 업무 존재 여부와 무관하게 readiness가 충족된 접근 가능 프로젝트를 canonical하게 나열하고, mutation도 업무 존재를 전제하지 않는다. `0033` 이전에 이미 ready였던 프로젝트의 업무 소급 생성은 보류 범위다(3장).

## 11. 삭제·취소·purge lifecycle

- soft-delete·보류·취소 프로젝트: 신규 키팅 mutation 차단, completion·batch·work item·event 보존, 기존 조회 정책 범위에서 열람.
- 패널 취소: `ProjectStore`의 기존 패널 취소 transaction 경계에 “해당 패널의 `Requested/InProgress` 상태 `ManufacturingWork` 패널 업무를 Cancelled로 전환”을 추가한다. Completed 업무와 completion record는 변경하지 않는다. 이 보정이 현재 취소 owner와 의미 있게 충돌하면 키팅 완료 패널의 취소를 안전하게 차단하고 후속 정책 Finding으로 기록한다. open 업무 방치는 허용하지 않는다.
- permanent purge: `0033` FK는 `on delete restrict`이므로 기존 `PurgeDeletedProjectIdsAsync`(project 소유 row를 명시 삭제하는 관리자 purge transaction)에 `panel_kitting_completions`·`panel_kitting_batches` 삭제를 `panel_placeholders`·`project_procurement_items`보다 앞서 추가하고 purge 회귀 테스트를 넣는다.
- purge 회귀에서 `0030`~`0032`의 기존 restrict FK(자재 원장·IQC 성적서 계열)로 인한 선행 purge 실패가 확인되면 이는 이번 Task 범위 밖의 선행 계보 Finding으로 분리 기록한다. TASK-010A는 자신의 신규 table이 purge를 깨지 않음을 보장하고 기존 동작을 악화시키지 않는다.

## 12. Frontend 계약

- 전용 module: `PanelKittingPage.tsx`(신규 page component)와 전용 type·API helper(`panelKitting.ts` 계열)를 추가한다. `MaterialsWorkspace.tsx`·`materials.ts`에 키팅 로직을 누적하지 않으며, `App.tsx`에는 `/materials/kitting` pathname route·자재 menu 연결만 추가한다.
- deep link: `?project=`로 프로젝트 focus, `?panel=`로 대상 패널 강조. “패널 키팅” 업무와 제조 업무 설명의 link가 이 화면으로 연결된다(제조 입력 화면은 `TASK-011A`가 대체).
- operationId: 제출 시 client가 UUID를 생성하고 같은 제출의 재시도에 재사용한다. 새 선택 변경은 새 operation이다. replay 응답은 성공과 동일한 요약으로 표시한다.
- 상태 구분: loading / 준비 프로젝트 없음(empty) / error / 준비 미충족 사유(미완료 품목 수) / 선택 mode(선택 수·완료 영향 안내) / 저장 중(중복 submit 차단) / 성공 요약 / conflict 재조회 안내 / 권한·scope 없음.
- 모바일(390px·Teams narrow): compact 한 열 카드 선택, 44px hit target, 기존 좌측 상단 숨김 global menu 유지, bottom navigation 신설 금지. 완료 action은 페이지 내부 sticky/in-flow control 허용. page-level horizontal overflow 0.
- desktop: 모바일 확대판이 아닌 별도 composition — readiness summary rail + 패널 grid·선택 요약.
- 접근성: checkbox 명시 label, 색상 외 상태 텍스트, keyboard 접근, 첫 오류 focus·`aria-live` 기존 Action Feedback 계약 유지.

## 13. 기존 기능 연결

- 알림: operation당 묶음 인앱 참조 알림 1건(생산관리·제조 참조, Roadmap 6.5.5 묶음 원칙)을 actionable 제조 업무와 구분해 생성한다. Teams/Mail/Activity delivery·신규 채널·신규 알림 유형은 만들지 않는다.
- Workflow: 프로젝트 상세 workflow 집계는 기존 StageCompleted event 기반 표시를 그대로 사용한다(추가 계산 로직 변경 없음).
- Excel/PDF/첨부: 영향 없음.
- 권한/관리자: 신규 권한 없음. 관리자도 mutation 정책 동일 적용.

## 14. 검증 계획

- Migration: catalog 검증, fresh/existing isolated DB apply, `0033` 제약·unique 동작.
- Backend targeted tests:
  - 권한·scope matrix: 자재(scope 내/밖), 제조(조회만), `ProjectRead` 무권한, Read-only, System Administrator — mutation allow/deny와 scope 밖 not-found 일관성
  - readiness: active 품목 0건, 일부 미완료, 전체 완료(사급 포함), queue·mutation predicate 일치
  - batch: all-or-nothing rollback(취소 패널·패널정보 미완료·이미 완료·비활성 프로젝트 혼입), 동일 operation+동일 payload replay(write 0), 동일 operation+다른 payload conflict, 다른 operation의 완료 패널 중복 conflict
  - 동시성: 같은 패널 동시 batch(completion·제조 업무 각 1건), 마지막 패널 동시 batch(stage event·알림 1건), 이중 제출
  - assignee 실패: 제조·fallback 후보 전무 시 completion 0건 rollback과 한글 오류
  - transition hook: Confirm 경로·CloseArrivals 경로 각각 키팅 업무 1회 생성, 재실행 중복 0
  - lifecycle: 패널 취소 시 open 제조 업무 Cancelled·Completed 보존·completion 보존, permanent purge 회귀(키팅 table 정리 포함)
- 회귀: 008A 도착·IQC·입고 확정·마감, 008B supplyType, 009A 성적서 최종화 filtered tests, work item idempotency.
- Frontend: lint·typecheck·unit(상태 구분·operationId 재사용·선택 로직)·build.
- isolated Full-Stack E2E: 입고 확정 → queue 활성 → 일부 선택 완료 → 제조 내 업무 확인 → 잔여 완료 → stage 완료·알림 + replay 시나리오 + 기존 자재·품질 spec.
- 사용자 검수: queue·선택 mode·완료 결과·제조 내 업무 화면의 desktop·390px synthetic screenshot을 보고하되 사용자 검수 완료로 표시하지 않는다.

## 15. Task 고유 안전 경계

- Persistent UAT 영향 없음 — isolated PostgreSQL·synthetic 데이터만 사용.
- migration은 `0033` additive 1건, 실 DB 적용은 별도 사용자 승인.
- 외부 발송·실제 데이터 영향 없음, runtime 교체 없음.
- Git 경계: 구현·검증·screenshot·종료 산출물 완료 뒤 local experiment commit까지 승인(change 001). push·PR·merge 미승인, `main` merge 승인 `0/3`.

## 16. 완료 기준과 구현 순서

완료 기준: 5장 시나리오 A~D가 서버 authoritative로 동작하고 6장 불변조건 위반 시도가 모두 차단·테스트되며, 14장 검증 전체 PASS(미실행은 이유와 함께 기록), 008A~009A 회귀 0, 390px·Teams narrow overflow 0, 5종 산출물 상태·위치 추적, `사용자 검수 대기` handoff.

구현 순서(review 권장 채택):

1. `0033` batch/completion schema·제약·purge 정리와 migration tests
2. `PanelKittingStore`: scope·readiness·queue, operation replay·project/panel lock transaction, assignee 필수 rollback
3. readiness transition hook(Confirm·CloseArrivals), 패널 제조 업무·stage event·묶음 알림 연결
4. 패널 취소·permanent purge 회귀 보정
5. `/materials/kitting` endpoints·전용 Frontend page·route/menu/deep link
6. Backend targeted/전체, Frontend unit/build, isolated Full-Stack E2E
7. desktop·390px screenshot, implementation report·5종 산출물·local commit

구현 세션은 이 문서 밖의 범위를 추가하지 않고, 17장 deferred 항목을 임의 결정하지 않는다.

## 17. Deferred 비차단 사용자 결정

| 번호 | 항목 | 상태 |
| ---: | --- | --- |
| 1 | 실제 BOM·패널별 자재 소요량 master와 패널 내부 allocation | 대기 (현업 회신 후 별도 planning) |
| 2 | 잘못된 키팅 완료의 정정·재키팅·관리자 강제 정정 정책 | 대기 (별도 NEW_FEATURE) |
| 3 | 창고 위치·재고 차감·생산 불출 | 1차 시스템 제외 유지 |
| 4 | 기존 ready 프로젝트 키팅 업무 소급 reconcile | 보류 (Persistent 적용 시 rollout 판단) |
| 5 | `0033` 실 DB 적용·Persistent UAT handover·대표 repo 반영 시점 | 대기 (별도 승인 절차) |

---

openBlockingDecisionCount: 0
