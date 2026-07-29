# TASK-011A — 제조 작업 시작·체크·종료·중단 2차 기획 (구현 계약)

> 이 문서는 `tasks/011a-interview.md`(확인 완료 interview), `tasks/011a-planning.md`(Fable 1차 기획 전문), `tasks/011a-review.md`(Codex 내용 review 전문)와 현재 Repository 구현·테스트를 모두 다시 읽고 작성한 TASK-011A의 authoritative implementation contract다. Review의 유지 항목은 보존하고, 추가·보류·제거 권고와 Finding resolution 전부를 이 계약에 통합했다. 이 문서는 게시, `main` merge, Persistent UAT 또는 실제 provider 승인을 부여하지 않는다.

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-011A`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/011a-interview.md`
- firstPlanningSource: `tasks/011a-planning.md`
- codexReviewSource: `tasks/011a-review.md`
- approvalChangeSource: `tasks/011a-change-001.md`
- branchScope: `experiment/task-011a-manufacturing-work` — local experiment commit까지만

## 1. 한 줄 목표

제조 담당자가 모바일에서 키팅 완료된 패널의 제조 작업을 시작하고 고정 4단계를 체크한 뒤 종료하며, 작업 불가 시 조치 담당 부서가 연결된 `ManufacturingStop` Pending으로 즉시 전환하고, 각 panel 완료마다 LQC skeleton 업무가 지연 없이 생성된다.

## 2. 배경과 결정 요약

- 현재 상태(Repository에서 재확인): 키팅 완료 시 `PanelKittingStore`가 panel target·`ManufacturingWork` stage의 내 업무를 idempotency key `kitting:panel:{panelId}:manufacturing`으로 생성하지만, 업무 link는 `/materials/kitting?...`으로 돌아가고 `panel_placeholders.workflow_stage`를 전진시키는 코드는 없다. 제조 실행 시간·단계·체크·중단 데이터와 전용 화면·transaction이 존재하지 않는다.
- 1차 기획에서 유지된 방향: panel당 active execution 1건, 고정 최소 단계 snapshot, append-only 시작·체크·중단·재개·종료 event, `ManufacturingStop`·`Urgent` Pending, 전진-only stage, 권한 조건부 전역 `제조` 진입 + 내 업무 deep link, additive `0034` migration, 전용 Manufacturing module과 `/manufacturing/work` 화면, 모바일 행동 중심 adaptive UX.
- Codex review가 요구해 이 계약에 반영한 필수 보정:
  1. generic `POST /api/my-work/{id}/start|complete|cancel`이 panel `ManufacturingWork` 업무를 checklist와 무관하게 전이하는 우회를 차단한다 (`011A-DIRECT-WORK-BYPASS`).
  2. 시작/종료 transaction이 execution·panel stage·정확한 panel 제조 업무 status를 원자 동기화한다 (`011A-WORK-EXECUTION-DIVERGENCE`).
  3. 중단은 bounded 사유 enum + 필수 조치 담당 부서 + 부서 내 assignee를 받고, 기존 `PendingStore.CreateAsync`(Project target 고정·자체 transaction — 코드로 재확인)를 호출하지 않고 transaction-safe 경로로 Panel target Pending을 원자 생성한다 (`011A-PENDING-TARGET-DEPARTMENT`).
  4. 제조 종료된 panel마다 panel target LQC skeleton 업무를 exactly-once 생성하고, project `ManufacturingWork/StageCompleted` event는 마지막 active panel에서만 1회 생성한다 (`011A-PANEL-LQC-HANDOFF`).
  5. operation replay는 operation id + action + payload fingerprint + 성공 response projection 저장으로 계약화한다 (`011A-REPLAY-CONTRACT`).
  6. active execution이 있는 panel/project 취소는 terminal `Cancelled` 정리 또는 명시 차단으로 처리하고 purge 정합을 보정한다 (`011A-CANCELLATION-LIFECYCLE`).
  7. `Manufacturing.WorkTime.Read`는 현재 `system-administrator`·`sales`에만 있으므로(migration `0002`로 재확인) 제조 담당자 자신의 operational time 노출과 cross-user 시간 보고 권한을 분리한다 (`011A-WORKTIME-VISIBILITY`).
  8. checklist는 `시작 확인`을 제거한 `작업지시·도면 확인 → 자재·부품 확인 → 제조 작업 수행 → 자체 확인` 4단계로 고정한다 (`011A-CHECKLIST-REDUNDANCY`).
- 비차단 선택은 interview standing rule과 review 5절의 자동 채택안을 따른다. 사용자가 확정하지 않은 새 정책은 만들지 않았다.

## 3. 포함·제외 범위

### 포함

- 키팅 완료 + 활성 panel `ManufacturingWork` 업무 기반 제조 queue·상세 API와 `/manufacturing/work` 화면 (project access scope 적용)
- panel당 active execution 1건의 시작·단계 체크·종료와 시작/종료/체크 actor·시각 audit
- 시작/종료 transaction의 execution·panel stage·panel 제조 업무 원자 동기화와 generic 내 업무 전이 차단
- `BeforeManufacturing → ManufacturingInProgress → ManufacturingCompleted` 전진-only stage
- bounded 사유 enum·설명·필수 조치 담당 부서·부서 내 assignee를 가진 제조 중단과 Panel target `ManufacturingStop`·`Urgent` Pending의 단일 transaction 생성
- 연결 Pending `Closed` 후 같은 execution의 append-only 재개
- panel 완료마다 panel target LQC skeleton 업무 exactly-once, 마지막 active panel에서 project stage event exactly-once
- operation receipt(action·fingerprint·result projection) 기반 성공 replay·conflict와 expected version stale 차단
- panel/project cancellation의 execution terminal 정리와 approved permanent purge 정합
- additive `0034` migration, 전용 Manufacturing Backend module, 모바일/desktop adaptive Frontend, isolated 검증과 desktop·390px screenshot

### 제외

- LQC/OQC/FAT 검사 record·성적서·사진·PDF·품질 화면 (`TASK-012A`)
- 영구 제조 template 관리·version·activation UI (`TASK-ADMIN-002`)
- 현업 미회신 상세 자주순차표 전체 항목과 화면 표시/팝업/저장-only 구분 확정
- 프로젝트 단위 일괄 시작·종료와 복수 panel batch operation
- 제조 완료 정정·재작업·관리자 강제 완료·record/event 수정·삭제 API
- 사진·QR·Excel·첨부·신규 외부 알림 채널·실제 provider delivery
- Persistent UAT migration·write·runtime handover, 대표 repo·GitHub `main`·push·PR·merge

## 4. 사용자·권한·접근 범위

| 역할 | 행동 | 계약 |
| --- | --- | --- |
| 제조 정·부 담당 (`manufacturing.update`) | queue/상세 조회, 시작·체크·종료·중단·재개 | `ManufacturingUpdate` policy + project access scope. 자신이 수행하는 panel의 시작·종료·체크·중단 시각(operational timestamps)은 이 권한 범위에서 조회한다 |
| 생산관리 정·부 담당 | 진행·중단 조회, Pending 조치 연결 확인 | 제조 mutation 불가. Pending은 기존 `Pending.Manage` 계약만 |
| Pending 조치 담당자 | 중단 Pending 조치·상태 전이·댓글 | 기존 Pending 상태 모델·expected version·append-only history 그대로 |
| 품질·자재·영업·조회 역할 | 허용 프로젝트의 제조 상태 조회 | project read scope. cross-user 작업 시간 상세 보고는 기존 `Manufacturing.WorkTime.Read`(현재 `system-administrator`·`sales`) 유지 |
| System Administrator | 기준·이력 조회 | 제조 mutation은 동일 policy·검증 통과 필요. 업무 입력 무제한 우회 금지 |

- 이번 Task에서 permission을 신설·확대·재배정하지 않는다. `Manufacturing.WorkTime.Read`의 역할 조정이 필요하면 별도 `POLICY_DECISION`이다.
- queue/상세/mutation 모두 기존 project scope helper(`has_read_all` 또는 `project_key` any)를 적용하고, scope 밖 식별자는 존재 여부를 노출하지 않는 not-found로 처리한다.
- mutation은 transaction lock 후 inactive user/project/panel/work item을 재검증한다.

## 5. 핵심 사용자 시나리오

### 시나리오 A — 모바일 정상 제조 수행

1. 제조 담당자가 내 업무의 `제조 작업 · <패널>` deep link 또는 좌상단 숨김 메뉴의 `제조`로 진입한다. deep link면 해당 패널에 focus한다.
2. `작업 시작`을 누르면 서버가 키팅 완료·활성 project/panel·Requested 상태의 panel 제조 업무·중복 실행 없음을 lock 하에 재검증하고, 단일 transaction으로 execution(InProgress)·4단계 snapshot·Started event를 생성하며 panel stage를 `ManufacturingInProgress`로, 해당 panel 업무를 `InProgress`(`started_at_utc` 동일 시각)로 전진시킨다.
3. 담당자가 `작업지시·도면 확인 → 자재·부품 확인 → 제조 작업 수행 → 자체 확인`을 순서대로 체크한다. 각 체크는 순서 위반·중복을 서버가 차단하고 actor·시각과 StepChecked event를 남긴다.
4. `작업 종료`를 누르면 서버가 4단계 전체 체크·비중단 상태·expected version을 재검증하고, 단일 transaction으로 execution `Completed`, panel stage `ManufacturingCompleted`, 해당 panel 업무 `Completed`, panel target LQC skeleton 업무 exactly-once 생성을 처리한다. 프로젝트의 마지막 active panel이면 같은 transaction에서 project `ManufacturingWork/StageCompleted` event 1회와 생산관리 참조 알림을 추가한다.
5. 완료 상태와 다음 패널 안내가 action 근처에 표시된다.

### 시나리오 B — 제조 중단과 조치 연결

1. 작업 불가 상황에서 `제조 중단`을 누르면 중단 sheet가 사유 구분(`Material`·`Staffing`·`WorkUnavailable`·`Other`), 설명, 필수 조치 담당 부서, 부서 내 조치 담당자(선택)를 입력받는다.
2. 서버가 단일 transaction으로 execution을 `Blocked`로 전환하고 Stopped event를 append하며, `target_type='Panel'`·`target_id=panelId`·`ManufacturingStop`·`Urgent` Pending을 생성해 execution에 연결한다. 기존 Pending 계약대로 Created history를 남기고, assignee 지정 시 `ActionRequested` 상태·조치 업무(`PendingAction`)·차단 알림(`Blocking`/`Critical`, `/pending/{id}` link)까지 같은 transaction에서 생성한다. 생산관리 담당자에게 참조 알림을 남긴다. 외부 delivery는 기존 인앱 원본·outbox 기록까지이며 실제 provider는 실행하지 않는다.
3. Blocked 상태에서는 체크·종료가 차단되고 화면이 Pending 상세 링크와 재개 조건을 안내한다.
4. 조치 담당자가 Pending을 종결(`Closed`)하면 제조 담당자가 `작업 재개`를 누른다. 서버가 연결 Pending `Closed`·active stop 존재를 검증하고 Resumed event를 append하며 execution을 `InProgress`로 되돌린다. stage 번호는 어떤 경로에서도 후퇴하지 않는다.

### 시나리오 C — 재시도·동시성 복구

1. network 유실 후 같은 요청을 재시도하면 동일 operation id + 동일 action/payload fingerprint에 대해 저장된 성공 projection을 replay한다.
2. 같은 operation id를 다른 action/payload에 재사용하면 안정적인 한글 conflict를 반환한다.
3. 다른 operation으로 이미 처리된 step 체크·중단·재개·종료를 요청하면 현재 상태를 포함한 conflict를 반환한다. 두 담당자가 동시에 시작하면 partial unique와 lock으로 한 건만 성공하고 나머지는 conflict다.
4. stale `expectedVersion`은 "최신 내용을 다시 불러와 주세요" conflict와 재조회 안내로 처리한다.

### 시나리오 D — 차단·lifecycle 경로

1. 키팅 미완료, 취소/보류 project·panel, 제조 업무 미생성 또는 이미 완료, Blocked 중 종료, 순서 밖 체크, scope 밖 접근을 서버가 각각 구분된 한글 오류로 차단한다.
2. generic 내 업무 API로 panel `ManufacturingWork` 업무를 start/complete/cancel하려 하면 conflict와 함께 제조 화면 경로를 안내한다.
3. 제조 시작 전 panel 취소는 기존 010A 경로대로 open 제조 업무를 `Cancelled`로 전환한다. active execution이 있는 panel/project 취소는 같은 취소 transaction에서 execution을 terminal `Cancelled`로 전환하고 Cancelled event를 append하며 open 제조 업무를 `Cancelled`로 정리한다. 열린 execution을 InProgress/Blocked로 방치하지 않는다. 연결된 open `ManufacturingStop` Pending은 기존 Pending 계약의 상태 모델을 따르며 자동 종결하지 않는다.
4. 완료 execution은 불변이고 soft-delete 뒤에도 조회 가능하다. approved permanent purge는 operation receipt → event → step → execution 순서로 FK 정합을 지켜 정리한다(`0033` completion/batch 선행 정리 방식과 동일 계층).

## 6. 업무 규칙과 불변조건

1. 키팅 완료(`panel_kitting_completions` 존재)와 활성 panel `ManufacturingWork` 업무가 제조 시작의 서버 측 전제조건이며 mutation마다 lock 하에 재검증한다.
2. panel `workflow_stage`는 `BeforeManufacturing → ManufacturingInProgress → ManufacturingCompleted` 전진-only다. 후퇴 UPDATE는 존재하지 않는다.
3. panel당 활성(비terminal) execution은 최대 1건이다(DB partial unique). execution 상태는 `InProgress → Blocked ⇄ InProgress → Completed`이며 `Cancelled`는 승인된 취소 lifecycle에서만 진입하는 terminal이다.
4. panel `ManufacturingWork` 업무의 상태 전이는 제조 domain transaction만 수행한다. generic 내 업무 start/complete/cancel은 이 업무에 대해 conflict를 반환한다. active execution과 원본 업무 상태가 불일치하면 silent 보정하지 않고 conflict로 차단하고 Finding으로 기록한다.
5. step은 snapshot 순서대로 한 번만 체크할 수 있고 uncheck·수정 API는 없다. 종료는 4단계 전체 체크 + `InProgress` 상태 + expected version 일치를 요구한다.
6. 시작·체크·중단·재개·종료·취소는 append-only event로 남기고 hard delete·덮어쓰기하지 않는다.
7. execution당 active stop은 1건이며(unique/lock 보장) 활성 `ManufacturingStop` Pending과 1:1로 연결된다. 재개는 연결 Pending이 `Closed`일 때만 허용한다.
8. 중단 사유는 `Material`·`Staffing`·`WorkUnavailable`·`Other` bounded enum이고 설명은 별도 저장한다. 조치 담당 부서(`departments.code` 참조)는 필수이며, assignee는 해당 부서의 active `Pending.Manage` 사용자만 후보다. "귀책부서" 표현은 어디에도 사용하지 않는다.
9. panel 완료마다 panel target LQC skeleton 업무를 exactly-once 생성한다(idempotency key `manufacturing:panel:{panelId}:lqc`). 검사 record·양식·사진 데이터는 만들지 않는다. LQC quality assignee(`QualityLQC → QualityLQCSecondary → Quality` role fallback, `quality.inspect` 권한 확인 — 010A의 제조 담당 resolve 패턴 재사용)를 해석하지 못하면 종료 transaction 전체를 rollback하고 담당자 지정을 안내하는 한글 오류를 반환한다.
10. project `ManufacturingWork/StageCompleted` event는 마지막 active panel 완료 transaction에서 project row lock + 기존 event 확인으로 정확히 1회만 생성한다. project의 다른 panel 제조 업무를 일괄 완료하지 않는다.
11. generic `WorkflowStore.CompleteStageAsync`는 자체 connection/transaction을 열므로 호출하지 않고, LQC 업무·참조 알림 생성을 종료 transaction 안에 inline 구현한다.
12. 모든 mutation은 operation receipt에 operation id·action·execution/panel identity·bounded payload fingerprint·성공 response projection을 저장한다. 동일 operation+동일 payload는 성공 replay, 동일 operation+다른 payload는 conflict다.
13. 응답과 receipt projection에는 상태·count·bounded enum·display code 수준만 담고 고객·업무 원문을 JSON으로 복제하지 않는다. 내부 민감 식별자·개인 metadata를 노출하지 않는다.
14. System Administrator를 포함한 모든 mutation이 동일한 policy·scope·검증을 통과한다.

## 7. 데이터 모델과 migration `0034`

additive migration `database/migrations/0034_manufacturing_execution.sql` 1건. 기존 `0001~0033`을 수정하지 않고 번호를 재사용하지 않으며 기존 panel의 시작/완료 추정 backfill을 하지 않는다.

| 신규 객체 | 핵심 내용 |
| --- | --- |
| `panel_manufacturing_executions` | project/panel FK, status(`InProgress`,`Blocked`,`Completed`,`Cancelled`), started_by/at, completed_by/at, cancelled_by/at, active stop Pending FK(nullable), version. 비terminal 상태의 panel당 partial unique index |
| `panel_manufacturing_execution_steps` | execution FK, sequence(1~4), step 명칭 snapshot, checked_by/at(nullable). 순서·단일 체크 constraint |
| `panel_manufacturing_events` | execution FK, event_type(`Started`,`StepChecked`,`Stopped`,`Resumed`,`Completed`,`Cancelled`), step FK(nullable), pending FK(nullable), 사유 enum·설명(중단), actor, created_at. append-only |
| `panel_manufacturing_operations` | operation id unique, action, execution/panel identity, payload fingerprint, 성공 response projection, actor, created_at |
| `pending_issues` 확장 | 조치 담당 부서 컬럼(nullable, `departments.code` 참조) 추가. 기존 Project/ProcurementItem target 행 호환 유지. `ManufacturingStop` 신규 행은 부서 필수를 응용 계층+제약으로 강제 |

- step snapshot 고정값: `작업지시·도면 확인`, `자재·부품 확인`, `제조 작업 수행`, `자체 확인` (execution 생성 시 복사, template 테이블 없음. 현업 template 확정 시 신규 execution snapshot부터 교체).
- LQC skeleton 업무는 기존 `work_items` 재사용: `target_type='Panel'`, `workflow_stage_code='LQC'`, `responsibility_type='QualityLQC'`, idempotency key `manufacturing:panel:{panelId}:lqc`.
- purge 경로는 위 신규 테이블을 FK 역순으로 정리하도록 기존 project purge transaction을 보강한다.

## 8. API 계약

전용 Manufacturing module(contracts/endpoints/store 분리, `0030~0033` module 패턴)로 구현한다. 이름은 기존 관례에 맞춰 Codex가 확정하되 아래 의미를 유지한다.

| Endpoint | 권한 | 내용 |
| --- | --- | --- |
| `GET /api/manufacturing/queue` | project read scope | 프로젝트별 제조 대상 panel(키팅 완료·업무 존재), execution 상태·진행 단계·중단 여부, 완료/잔여 count |
| `GET /api/manufacturing/panels/{panelId}` | project read scope | 패널 실행 상세: step snapshot·체크 상태, event 타임라인, 활성 Pending 연결, operational timestamps(4장 노출 규칙 적용) |
| `POST /api/manufacturing/executions/start` | `ManufacturingUpdate` + scope | `operationId`, `projectId`, `panelId` |
| `POST /api/manufacturing/executions/{id}/check-step` | 동일 | `operationId`, `stepId`, `expectedVersion` |
| `POST /api/manufacturing/executions/{id}/stop` | 동일 | `operationId`, `reasonCode`, `description`, `actionDepartmentCode`(필수), `assigneeUserId`(선택), `expectedVersion` |
| `POST /api/manufacturing/executions/{id}/resume` | 동일 | `operationId`, `expectedVersion` |
| `POST /api/manufacturing/executions/{id}/complete` | 동일 | `operationId`, `expectedVersion` |
| 조치 담당 부서·담당자 후보 조회 | 기존 Pending 조회 계약 재사용 | 부서 목록과 부서 내 active `Pending.Manage` 사용자. 기존 endpoint 재사용 가능하면 신설하지 않는다 |

- generic `POST /api/my-work/{id}/start|complete|cancel`은 `target_type='Panel' AND workflow_stage_code='ManufacturingWork'`에 대해 conflict(한글 안내 + 제조 화면 경로)를 반환하도록 보강한다.
- Pending list/detail 응답에 privacy-safe panel context(표시 코드 수준)와 조치 담당 부서를 추가하고 기존 target 호환을 유지한다.
- 모든 오류는 안정적인 한글 메시지·기존 validation/conflict/not-found 응답 형태를 따른다.

## 9. Transaction·동시성·idempotency 계약

1. 모든 mutation은 project → panel → execution 순 row lock 후 scope·상태·operation receipt·expected version을 재검증하고 하나의 DB transaction으로 처리한다.
2. 시작: execution+step snapshot+Started event+operation receipt insert, panel stage 전진, panel 업무 `Requested→InProgress`(`started_at_utc` 동일 시각) — 전부 성공 또는 전부 rollback.
3. 중단: execution `Blocked`+Stopped event+Panel target Pending(Created history, 필수 부서, assignee 시 `ActionRequested`+조치 업무+차단 알림)+execution link+참조 알림 — 단일 transaction. 기존 `PendingStore`의 validation·history·assignment artifacts 계약을 동일 connection/transaction을 받는 helper로 재사용한다(IQC 부적합의 transaction-safe helper 패턴).
4. 재개: 연결 Pending `Closed` 확인 후 Resumed event+`InProgress` 복귀.
5. 종료: 4단계 체크·비중단·version 검증 후 execution `Completed`, panel stage `ManufacturingCompleted`, panel 업무 `Completed`, panel LQC skeleton 업무 exactly-once. 마지막 active panel 판정은 lock 하에 active panel 수와 완료 execution 수를 재계산하고, project stage event는 기존 event 확인 후 1회만 insert한다.
6. replay: operation receipt 기준. unique violation race는 `0033`과 같이 안정적인 conflict로 변환한다.
7. 취소 lifecycle: 5.D.3 계약대로 취소 transaction 안에서 execution terminal 정리를 함께 처리한다.

## 10. Frontend 계약

- 신규 view kind와 pathname `/manufacturing/work`(query `project`·`panel`), `ManufacturingPage.tsx` + 전용 API/type 파일로 분리(`PanelKittingPage` 분리 패턴). `App.tsx`에 제조 mutation 권한 조건부 전역 `제조` 메뉴를 추가하고, 조회 전용 역할에는 desktop 진행 조회 진입만 허용되는 경우 mutation action을 렌더링하지 않는다.
- 신규 제조 업무 deep link 문구를 `/manufacturing/work?project=...&panel=...`로 변경하고(Backend `PanelKittingStore` 생성 문구), 내 업무 화면은 `ManufacturingWork` stage panel 업무의 링크를 제조 화면으로 매핑한다. 이 업무 카드에서는 generic 시작·완료·취소 action을 제거하고 `제조 화면에서 진행` action만 제공한다.
- 모바일(≤860px, `useAdaptiveLayout`): 프로젝트 가로 queue → panel focus card → 시작/체크/종료·중단이 한 흐름. 상태 도형 언어(시작 전=각진 사각, 진행 중=둥근 사각+원형 progress, 중단=타원 경고, 완료=원형 체크), 작은 글씨 보조 정보(시각·actor), 44px touch target, `MobileSheet` 기반 중단 입력(사유 enum·설명·부서·담당자), 좌상단 숨김 메뉴 유지, bottom navigation·page-level horizontal overflow 0.
- desktop: 프로젝트·패널 진행 요약(시작 전/진행 중/중단/완료), 실행 타임라인(시작·체크·중단·재개·종료), 활성 중단 Pending 링크.
- feedback: queue loading/empty/error, 저장 중 disable·중복 submit 차단, 성공/차단/stale conflict를 action 인접 표시, stale 시 재조회 action. 실패 후 동일 payload 재시도는 같은 operation id 유지, 입력 변경 시 새 operation id(`PanelKittingPage` operation receipt 패턴).
- 권한 부족·조회 전용·Blocked 상태는 버튼 비활성+사유 텍스트로 구분한다.

## 11. 기존 기능 연결

- 내 업무/알림: panel 제조 업무의 시작·완료가 제조 domain과 동기화되고, LQC skeleton 업무·중단 차단 알림·생산관리 참조 알림은 기존 `work_items`/`notifications`/outbox 계약을 재사용한다. LQC 업무 link는 012A 전까지 프로젝트 workflow 화면 fallback을 사용한다.
- Pending: 기존 상태 모델·expected version·history·comment·조치 업무 계약을 변경하지 않고 Panel target과 조치 담당 부서를 추가한다. 기존 Project/ProcurementItem target 행의 조회 호환을 유지한다.
- workflow: 진행률·현재 단계 계산은 기존 `project_workflow_events` 기반 로직을 그대로 사용하며 `ManufacturingWork` StageCompleted 1회 생성으로 LQC 단계로 이동한다.
- 010A 회귀: 키팅 완료·readiness hook·panel 취소 시 제조 업무 취소·purge 계약을 훼손하지 않고 확장한다. `010A-CANCEL-LAST-PANEL-STAGE` P3 보류 정책은 유지한다.
- Excel/PDF/첨부/Teams/Mail: 신규 없음.

## 12. 검증 계획

- Migration: `0034` fresh/existing 검증, constraint·partial unique·FK·purge 회귀.
- Backend integration: 시작·체크(순서·중복)·중단(부서 필수·assignee 후보 검증)·재개(Pending Closed 전 차단)·종료(체크 미완료·Blocked 차단) happy/차단 경로, generic my-work bypass 차단, panel LQC exactly-once, 마지막 panel event exactly-once(동시 종료 race 포함), operation replay/payload reuse conflict/stale version, scope·역할별 권한(제조/생산관리/조회/System Administrator), inactive 대상 재검증, cancellation·purge lifecycle, 기존 Pending·010A·workflow 전체 회귀.
- Frontend: 신규 페이지 unit(상태·feedback·operation id 유지/갱신·권한별 렌더링), 내 업무 링크 매핑·generic action 제거, 전체 unit·lint·typecheck·build.
- E2E: isolated 전용 DB·provider disabled의 Full-Stack 시나리오(시작→체크→중단→Pending 조치→재개→종료→LQC 업무 확인) spec 추가. 실행하지 못한 검증은 성공으로 기록하지 않는다.
- 증빙: synthetic 데이터 기반 `/manufacturing/work` queue·시작·체크·중단·재개·종료의 desktop·390px 페이지별 screenshot, 모바일 horizontal overflow 0 확인. 사용자 직접 검수 완료로 표시하지 않는다.

## 13. Task 고유 안전 경계

- Persistent UAT: migration·write·runtime handover 없음. isolated PostgreSQL·synthetic data만 사용.
- 실제 provider: 호출 0. delivery는 기존 outbox 기록까지.
- Git: 구현·검증·산출물 완료 후 현재 experiment branch local commit까지. push·PR·merge·대표 repo·`main` 반영 없음(`mainMergeApprovalCount 0/3`).
- rollback: 적용 전 local branch 폐기로 종료 가능. `0034` 적용 후에는 additive forward-fix와 execution/event/Pending/work item 이력 보존으로 복구한다.
- 중단 조건: 키팅 전제·전진-only·Pending 감사 무결성·secret/개인정보와 충돌하는 구현이 필요해지거나, 문서·구현의 의미 있는 충돌 또는 isolated 경계를 벗어나는 검증이 필요해지면 fast-track을 멈추고 blocking decision으로 보고한다.

## 14. 완료 기준과 구현 순서

완료 기준: 3장 포함 범위 구현, 6장 불변조건의 서버 강제, 12장 검증 통과(미실행은 이유와 함께 보고), 10장 UX 기준 충족, implementation report·SOP·user manual·user validation checklist(`사용자 검수 대기`)·Roadmap experiment 상태 갱신의 5종 산출물 추적, privacy/secret·diff·Finding gate 통과, local experiment commit.

구현 순서 (review 7절 채택):

1. `0034` schema·constraint·pending 확장·purge migration과 migration tests
2. generic my-work bypass guard와 Manufacturing transaction store
3. start/check/stop/resume/complete의 operation replay·scope·assignee·handoff
4. panel/project cancellation·permanent purge 회귀 보정
5. `/manufacturing/work` API·전용 Frontend page·menu/deep link
6. Backend targeted/전체, Frontend unit/lint/build, isolated Full-Stack E2E
7. desktop·390px screenshot, implementation report·5종 산출물·local commit

## 15. Deferred 비차단 사용자 결정

| 번호 | 항목 | 상태 |
| ---: | --- | --- |
| 1 | 제조 화면 항상 표시/팝업/저장-only 상세 항목과 실제 자주순차표 template (Roadmap 추적 6~9) | 현업 회신 후 후속. 이번 Task는 고정 4단계 snapshot |
| 2 | LQC 자동 요청 기준 상세와 012A 검사 handoff (Roadmap 추적 10) | `TASK-012A` planning에서 확정. 이번 Task는 panel LQC skeleton 업무까지 |
| 3 | 제조 template 관리·version·activation | `TASK-ADMIN-002` Deferred |
| 4 | `Manufacturing.WorkTime.Read` 역할 조정과 cross-user 시간 보고 화면 | 별도 `POLICY_DECISION` 후보. 이번 Task는 기존 배정 유지 |
| 5 | 프로젝트 단위 일괄 시작·종료 | 상세 항목 회신 후 후속 검토 |

openBlockingDecisionCount: 0
