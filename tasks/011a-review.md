# TASK-011A — 제조 작업 시작·종료·중단 1차 기획 Codex 내용 Review

> Review 대상: `tasks/011a-planning.md` Fable 5 원문
> Review 성격: 사용자 문제·제품 방향·Roadmap·실제 Repository·구현 경계 1회 검토
> 결과: 2차 Fable 기획 전 필수 보정 — 아래 resolution을 최종 구현 계약에 반영

## 1. 총평

패널별 active execution 1건, 고정 최소 단계 snapshot, append-only 시작·체크·중단·재개·종료 event, `ManufacturingStop` Pending과 모바일 행동 중심 화면을 채택한 방향은 적절하다. 상세 자주순차표·품질 성적서·template 관리·프로젝트 일괄 입력을 보류한 것도 미확정 현업 정책을 거짓 정밀도로 구현하지 않으면서 Roadmap 9단계의 실제 공백을 메우는 좋은 범위다. 별도 전역 `제조` workspace와 내 업무 deep link가 같은 화면으로 수렴하는 안도, 독립 workspace가 이미 있는 `자재`에 키팅을 합친 010A Change 002와 충돌하지 않는다.

다만 1차 기획대로 바로 구현하면 기존 generic 내 업무 API로 체크리스트를 우회 완료할 수 있고, 제조 시작과 panel stage만 바뀐 채 원본 제조 업무 status가 Requested에 남을 수 있다. 또한 Roadmap이 확정한 “조치 담당 부서”가 기존 Pending의 사용자 assignee로 축소되고, panel target Pending을 현재 public `PendingStore.CreateAsync`로는 원자 생성할 수 없다. 제조를 끝낸 panel마다 LQC가 시작돼야 하는데 마지막 프로젝트 panel까지 기다리게 한 것도 패널 단위 흐름과 맞지 않는다. 이 네 가지 P1과 cancellation·시간 노출·operation replay의 lifecycle 계약을 2차 기획에서 고치면 구현 가능하다.

## 2. 기능 판단

### 유지

- 키팅 완료 + 활성 panel `ManufacturingWork` 업무를 제조 실행 시작 전제로 재검증
- panel당 active execution 1건과 `BeforeManufacturing → ManufacturingInProgress → ManufacturingCompleted` 전진-only stage
- execution 생성 시 고정된 소수의 ordered step snapshot, 단계 체크·시작/종료 actor/time
- 같은 execution에 append-only stop/resume event와 활성 제조 중단 Pending 1건
- 재개는 연결 Pending이 `Closed`일 때만 허용하고 stage 번호를 후퇴시키지 않는 정책
- 마지막 active panel 완료에서 project `ManufacturingWork/StageCompleted` event exactly-once
- `0034` additive migration, 전용 Manufacturing Backend module과 `/manufacturing/work` Frontend page
- 권한 조건부 전역 `제조` menu + 내 업무 deep link, 모바일 행동 중심·desktop 진행 조회
- 상세 자주순차표·검사 데이터·template 관리·첨부·실제 provider·Persistent UAT·게시 제외

### 추가

1. **generic 내 업무 전이 우회 차단**
   - 현재 `POST /api/my-work/{id}/start|complete|cancel`은 assigned user만 맞으면 panel `ManufacturingWork`를 checklist와 무관하게 바로 InProgress·Completed·Cancelled로 바꿀 수 있다.
   - `target_type='Panel' AND workflow_stage_code='ManufacturingWork'`는 generic transition에서 conflict로 차단하고 `/manufacturing/work` 화면을 안내한다. 제조 domain API만 start·complete 상태를 바꿀 수 있어야 한다.
   - Frontend 내 업무 카드의 generic 시작·완료 action도 이 업무에는 표시하지 않고 제조 화면 진입 action만 제공한다.

2. **원본 제조 업무 status와 execution의 원자 동기화**
   - 시작 transaction은 execution 생성·Started event·panel stage 전진과 함께 정확한 `kitting:panel:{panelId}:manufacturing` 업무를 Requested→InProgress로 바꾸고 `started_at_utc`를 같은 시각으로 고정한다.
   - 종료 transaction은 그 panel 업무만 Completed로 바꾼다. project의 다른 panel 제조 업무를 일괄 완료하지 않는다.
   - active execution과 원본 업무가 불일치하면 silent 보정하지 말고 conflict/Finding으로 차단한다.

3. **조치 담당 부서와 panel target Pending 계약**
   - 제조 중단 사유 코드는 `Material`, `Staffing`, `WorkUnavailable`, `Other`의 bounded enum으로 고정하고 설명을 별도 저장한다.
   - Roadmap 확정사항대로 `actionDepartmentCode`를 필수로 받고, 선택한 부서의 active `Pending.Manage` 사용자만 optional/required assignee 후보가 되게 한다. “귀책부서” 표현은 사용하지 않는다.
   - 기존 `PendingStore.CreateAsync`는 target을 강제로 Project로 쓰고 자체 transaction을 열므로 그대로 호출하지 않는다. 동일 connection/transaction을 받는 transaction-safe helper를 추출하거나 Manufacturing store 안에서 기존 validation·history·assignment artifacts 계약을 재사용해 `target_type='Panel'`, `target_id=panelId`, `ManufacturingStop`, `Urgent`를 원자 생성한다.
   - Pending list/detail response에 privacy-safe panel context와 조치 담당 부서를 노출하고, 기존 Project target Pending의 호환성을 유지한다.

4. **panel별 LQC skeleton handoff와 project stage 분리**
   - 제조 종료된 panel마다 같은 transaction에서 panel target `LQC` skeleton 업무를 exactly-once 생성해 해당 panel의 품질 검사가 다른 panel 완료를 기다리지 않게 한다. 검사 record·양식·사진은 만들지 않는다.
   - project `ManufacturingWork/StageCompleted` event와 project-level 진행률 전환은 마지막 active panel에서만 exactly-once 처리한다.
   - quality assignee를 해석하지 못하면 제조 완료만 고정하지 말고 사용자가 조치 가능한 오류로 전체 rollback한다. LQC 업무 link는 012A 전까지 프로젝트 workflow fallback을 허용한다.

5. **operation replay의 저장 단위**
   - 모든 mutation은 `operationId`, action, execution/panel identity, bounded payload fingerprint와 성공 response projection을 저장한다.
   - 같은 operation+같은 action/payload는 기존 성공을 replay하고, 같은 operation의 다른 payload는 conflict다. 다른 operation으로 이미 완료된 step·stop·resume·complete를 요청하면 현재 상태를 포함한 안정적인 conflict를 반환한다.
   - 단순 event operation unique만으로는 payload reuse와 성공 replay를 구분하기 어려우므로 event 또는 별도 operation receipt에 fingerprint·result를 명시한다.

6. **panel/project cancellation lifecycle**
   - 제조 시작 전 panel 취소는 기존처럼 open 제조 업무를 Cancelled로 전환한다.
   - active execution이 있는 panel/project 취소는 stage를 후퇴시키지 않으면서 execution을 terminal `Cancelled`로 전환하고 `Cancelled` event를 append하거나, 현재 취소 mutation을 명시적으로 차단한다. 열린 execution을 InProgress/Blocked로 방치하는 방식은 허용하지 않는다.
   - 완료 execution은 정상 업무에서 불변이고 soft-delete 뒤에도 이력을 보존한다. approved permanent purge에서는 operation/event/step/execution을 FK 순서로 명시 정리한다.

7. **작업 시간 노출 권한의 실제 계약**
   - 기존 `Manufacturing.WorkTime.Read`는 현재 Sales·System Administrator에만 있고 제조 역할에는 없다. 제조 담당자 자신의 실행 시각까지 이 permission 때문에 숨기면 현장 UX가 깨진다.
   - 제조 담당자는 `manufacturing.update` + project scope 안에서 자신이 수행하는 panel의 operational timestamps를 볼 수 있다. 타 사용자·cross-panel 상세 시간 보고는 기존 `Manufacturing.WorkTime.Read`를 유지한다. 이번 Task에서 permission을 묵시적으로 확대하지 않는다.

8. **고정 최소 단계 문구 보정**
   - 시작 action과 중복되는 `시작 확인` step은 제거하고 `작업지시·도면 확인`, `자재·부품 확인`, `제조 작업 수행`, `자체 확인` 4단계로 고정한다.
   - step은 순서대로 한 번만 check 가능하고 uncheck·수정 API는 만들지 않는다. 실제 자주순차표 template가 확정되면 신규 execution snapshot부터 교체한다.

9. **scope·assignee·응답 최소화**
   - queue/detail/mutation 모두 기존 project scope helper를 적용하고 scope 밖 식별자 추측은 일관되게 not-found 처리한다.
   - 제조 mutation은 `ManufacturingUpdate`만으로 System Administrator가 우회하지 않으며, inactive user/project/panel/work item을 transaction lock 뒤 재검증한다.
   - 응답과 operation snapshot에는 합성 가능한 상태·count·bounded enum만 저장하고 고객·업무 원문을 JSON으로 복제하지 않는다.

### 보류

- 현업 회신 전 상세 자주순차표, 화면 항상 표시/팝업/저장-only 전체 항목
- 프로젝트 단위 일괄 시작·종료와 복수 panel batch operation
- 제조 완료 정정·재작업·관리자 강제 완료·event 삭제
- LQC/OQC/FAT 검사 record·성적서·사진·PDF와 012A 품질 화면
- 제조 template version·activation·관리 UI
- 사진·QR·Excel·외부 provider, Persistent UAT·대표 repo 게시

### 제거

- generic 내 업무 start/complete/cancel로 panel 제조 업무를 직접 전이하는 방식
- execution 시작 때 원본 제조 업무를 Requested로 남기는 방식
- 조치 담당 부서를 사용자 assignee 하나로 대체하는 방식
- 현재 `PendingStore.CreateAsync`를 별도 transaction으로 호출하는 방식
- 마지막 project panel이 끝날 때까지 완료된 panel의 LQC 업무 생성을 미루는 방식
- 제조 역할 자신의 operational time을 `Manufacturing.WorkTime.Read` 부재만으로 숨기는 방식
- 시작 action과 의미가 겹치는 `시작 확인` checklist step
- active execution을 둔 채 panel/project만 취소하는 방식

## 3. 권장 상태·transaction 계약

```text
work item / execution / panel:
  ManufacturingWork Requested + no execution + BeforeManufacturing
    --start transaction-->
  ManufacturingWork InProgress + execution InProgress + ManufacturingInProgress

  execution InProgress --stop transaction-->
  execution Blocked + stop event + active ManufacturingStop Pending(Urgent, Panel)

  Pending Closed + execution Blocked --resume transaction-->
  execution InProgress + resume event

  all 4 steps checked + execution InProgress --complete transaction-->
  execution Completed + ManufacturingWork Completed + ManufacturingCompleted
    + panel LQC skeleton work exactly once
    + if last active panel: project ManufacturingWork StageCompleted exactly once

  active execution --approved panel/project cancellation boundary-->
  execution Cancelled + event + open ManufacturingWork Cancelled
```

- 각 mutation은 project/panel/execution row lock, scope·상태·expected version·operation receipt 재검증 뒤 하나의 DB transaction으로 처리한다.
- 중단 Pending 생성·history·assignment work/notification과 execution link는 같은 transaction이다.
- 완료 stage는 되돌리지 않는다. permanent purge는 별도 관리자 lifecycle로 구분한다.

## 4. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `011A-DIRECT-WORK-BYPASS` | P1 | `RESOLVED_FOR_REDRAFT` | generic 내 업무 complete가 checklist·stage·handoff를 우회 | panel ManufacturingWork generic transition 차단, domain API만 전이 |
| `011A-WORK-EXECUTION-DIVERGENCE` | P1 | `RESOLVED_FOR_REDRAFT` | execution/panel만 시작하면 원본 업무가 Requested에 남음 | start/complete에서 해당 panel work status를 동일 transaction으로 동기화 |
| `011A-PENDING-TARGET-DEPARTMENT` | P1 | `RESOLVED_FOR_REDRAFT` | 기존 Pending create는 Project target이고 조치 담당 부서가 없음 | transaction-safe Panel Pending + 필수 actionDepartmentCode + 기존 history/artifact 재사용 |
| `011A-PANEL-LQC-HANDOFF` | P1 | `RESOLVED_FOR_REDRAFT` | last-panel-only LQC는 먼저 끝난 panel의 품질 handoff를 지연 | panel 완료마다 panel LQC skeleton, project event만 last-panel |
| `011A-REPLAY-CONTRACT` | P2 | `RESOLVED_FOR_REDRAFT` | event operation unique만으로 payload reuse·성공 replay 구분 불충분 | fingerprint·result projection operation receipt 계약 |
| `011A-CANCELLATION-LIFECYCLE` | P2 | `RESOLVED_FOR_REDRAFT` | panel 취소 뒤 active execution 고아 가능 | terminal Cancelled event 또는 취소 차단, purge 회귀 |
| `011A-WORKTIME-VISIBILITY` | P2 | `RESOLVED_FOR_REDRAFT` | 기존 time-read permission을 그대로 적용하면 제조 담당자의 자기 시각도 안 보임 | own operational view와 cross-user report permission 분리 |
| `011A-CHECKLIST-REDUNDANCY` | P3 | `RESOLVED_FOR_REDRAFT` | `시작 확인`은 별도 시작 action과 중복 | 네 단계 명칭을 작업지시/자재/수행/자체 확인으로 교체 |

Review 기준 Open P0/P1/P2는 `0/0/0`이다. 이는 Fable 2차 기획이 위 resolution을 반영한다는 조건부 판정이며 아직 코드 구현 완료 판정이 아니다.

## 5. 자동 채택할 비차단 결정

| 항목 | 채택안 | 근거 |
| --- | --- | --- |
| 실행 단위 | panel당 active execution 1건 + ordered step snapshot | Roadmap의 panel·제조 단계 입력 단위와 동시성 경계 일치 |
| 최소 checklist | 작업지시·도면 / 자재·부품 / 제조 수행 / 자체 확인 | 상세 template 없이도 완료 근거를 남기는 교체 가능한 최소값 |
| 중단 | 같은 execution의 stop/resume + active Pending 1건 | stage 후퇴 없이 원인·조치·재개 이력을 연결 |
| 조치 연결 | 사유 enum + 조치 담당 부서 필수 + 부서 내 assignee | Roadmap 확정 용어·실제 후속 행동 보존 |
| 품질 handoff | panel LQC skeleton per completion, 상세 데이터 없음 | panel 흐름을 지연하지 않으며 012A 경계 보존 |
| project stage | 마지막 active panel에서 event 1건 | project 집계와 panel 진행을 분리 |
| 진입 구조 | 별도 제조 workspace + 내 업무 deep link | 현장 queue와 개인 action을 같은 화면에 수렴 |
| 모바일 UI | compact queue·stepper·in-flow action + 좌상단 global menu | PC 축소가 아닌 현장 행동 중심 adaptive 원칙 |

## 6. 2차 기획이 고정할 최소 구현 계약

1. additive `0034`는 execution·step snapshot·append-only event/operation receipt·stop Pending link·action department·constraint/index를 추가하며 기존 migration을 수정하지 않는다.
2. generic work item transition은 panel `ManufacturingWork`를 차단하고, 제조 domain start/complete가 work item·execution·panel stage를 원자 동기화한다.
3. stop은 bounded reason·설명·action department·assignee를 받아 기존 Pending history/assignment artifacts와 함께 Panel target Urgent Pending을 동일 transaction에서 생성한다.
4. resume은 연결 Pending Closed와 active stop을 확인하고 같은 execution을 이어간다. active stop 1건을 unique/lock으로 보장한다.
5. complete는 4단계 체크·비중단·version을 검증하고 panel LQC skeleton을 exactly-once 생성한다. 마지막 panel에서만 project StageCompleted event를 추가한다.
6. 동일 operation+payload는 성공 replay, operation reuse+다른 payload는 conflict다. scope 밖은 not-found다.
7. active execution cancellation과 approved permanent purge의 lifecycle을 처리하며 정상 완료 record/event는 수정·삭제 API를 제공하지 않는다.
8. Frontend는 전용 제조 page를 모바일/desktop composition으로 분리하고 generic 내 업무 action을 제조 화면 진입으로 대체한다.
9. migration fresh/existing, direct-work bypass, scope·role, start/check/stop/resume/complete replay·stale·concurrency, panel LQC, last-panel event, cancellation/purge와 기존 Pending·010A 회귀를 검증한다.

## 7. 권장 구현 순서

1. `0034` schema·constraint·pending panel/action-department·purge migration tests
2. generic work item bypass guard와 Manufacturing transaction store
3. start/check/stop/resume/complete operation replay·scope·assignee·handoff
4. panel/project cancellation·permanent purge 회귀 보정
5. `/manufacturing/work` API·전용 Frontend page·menu/deep link
6. Backend targeted/전체, Frontend unit/lint/build, isolated Full-Stack E2E
7. desktop·390px screenshot, implementation report·5종 산출물·local commit

## 8. 판정

위 resolution을 반영한 Fable 2차 기획을 구현 source of truth로 사용하면 실험 branch 구현은 `GO`다. 이 판정은 대표 repo, push, PR, merge 또는 Persistent UAT 승인이 아니다.
