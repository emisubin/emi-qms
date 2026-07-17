# TASK-010A — 패널별 키팅·제조 내 업무 연결 1차 기획 Codex 내용 Review

> Review 대상: `tasks/010a-planning.md` Fable 5 원문
> Review 성격: 사용자 문제·제품 방향·Roadmap·실제 Repository·구현 경계 1회 검토
> 결과: 2차 Fable 기획 전 필수 보정 — 아래 resolution을 최종 구현 계약에 반영

## 1. 총평

현재 데이터로 증명 가능한 `패널 선택 단위 키팅`을 채택하고, BOM·패널별 소요량·재고 차감을 제외한 판단은 적절하다. 모든 활성 구매품목의 derived `receipt_completed`를 readiness로 재사용하고, 패널별 불변 완료 record와 제조 내 업무를 같은 transaction에서 생성하는 방향도 EMI의 핵심 가치인 부서 간 handoff를 정확히 구현한다. 모바일 queue → 프로젝트 → 패널 선택이라는 흐름과 제조 체크리스트를 TASK-011A로 남긴 경계도 유지한다.

다만 1차 기획대로 바로 구현하면 네 가지 핵심 결함이 생긴다. 첫째, 기존 `WorkflowStore.CompleteStageAsync`를 재사용하면 마지막 패널 완료 때 패널별 제조 업무 외에 프로젝트 단위 제조 업무가 하나 더 생길 수 있다. 둘째, 동일 batch의 network 재시도를 `이미 완료` conflict로 돌려주면 데이터 중복은 막아도 사용자는 첫 요청의 성공 여부를 복구할 수 없다. 셋째, append-only 완료 FK가 기존 관리자 permanent purge를 막거나 반대로 cascade가 문서의 “영구 보존” 문구와 충돌할 수 있다. 넷째, 제조 담당자를 끝내 해석하지 못했는데도 키팅 완료를 고정하면 제조 인수인계가 없는 고아 완료가 된다. 이 항목을 2차 기획에서 명시적인 transaction·lifecycle 계약으로 고치면 구현 가능하다.

## 2. 기능 판단

### 유지

- 부분 키팅을 “프로젝트 활성 패널 일부 선택 완료”로 정의하고 패널 내부 자재 allocation을 제외
- 모든 활성 구매품목의 derived `receipt_completed`가 true인 보수적 readiness
- 패널당 불변 완료 record, 기존 `work_items`·`project_workflow_events`·인앱 notification 재사용
- 패널당 제조 내 업무 1건과 stable idempotency key
- batch all-or-nothing transaction과 완료 stage 전진-only
- `panel_placeholders.workflow_stage`를 변경하지 않고 제조 시작 전이를 TASK-011A에 위임
- cancellation panel·비활성 프로젝트 mutation 차단, 기존 완료 기록은 soft-delete 이후에도 조회 보존
- `/materials/kitting` 모바일 우선 queue·패널 카드·일괄 선택과 desktop 별도 composition
- 0033 additive migration, 기존 0030~0032·Persistent UAT·provider·대표 repo 제외

### 추가

1. **동일 operation 재시도의 성공 replay 계약**
   - 완료 요청은 client가 생성한 bounded `operationId`를 포함한다. 서버는 project·actor·정렬된 panel set과 결합한 batch identity를 저장한다.
   - 같은 operation과 같은 payload의 재요청은 기존 성공 결과(완료 패널 수·제조 업무 수)를 반환한다. 같은 operation에 다른 payload는 conflict다.
   - 서로 다른 operation이 이미 완료된 패널을 포함하면 전체 conflict다. 이렇게 해야 중복 방지와 사용자의 network 복구를 동시에 만족한다.
   - migration은 최소 `panel_kitting_batches` + `panel_kitting_completions` 또는 동등한 unique 구조를 허용한다. 단순 완료 record만으로 같은 요청 replay와 다른 요청 conflict를 구분할 수 없다.

2. **프로젝트 단위 직렬화와 stage event exactly-once**
   - mutation transaction 초기에 project row를 `FOR UPDATE`로 잠그고 프로젝트 상태·scope를 확인한다. 같은 프로젝트의 두 batch가 마지막 패널 여부를 동시에 true로 판단하지 못하게 한다.
   - 마지막 패널 완료 시 `KittingCompleted/StageCompleted` event는 기존 event 존재 여부를 같은 lock 아래 확인해 최대 1건만 생성한다.
   - 기존 `WorkflowStore.CompleteStageAsync`를 호출하지 않는다. 이 method는 다음 stage의 프로젝트 단위 업무를 자동 생성하므로 패널별 제조 업무와 중복된다. 키팅 store가 event와 패널별 업무를 transaction 안에서 직접 생성하되 기존 SQL helper 규약만 재사용한다.

3. **제조 담당자 해석 실패 시 전체 rollback**
   - `ManufacturingPrimary → ManufacturingSecondary → legacy Manufacturing → SalesPrimary → SalesSecondary → System Administrator`이면서 `manufacturing.update`를 가진 active 사용자를 한 명도 찾지 못하면 키팅 완료를 고정하지 않는다.
   - 서버는 “제조 담당자를 지정한 뒤 다시 시도해 주세요”처럼 사용자가 조치 가능한 오류를 반환한다. 제조 work item은 핵심 handoff이므로 notification recipient 누락과 달리 생략 가능한 side effect가 아니다.

4. **프로젝트 접근 scope 전체 적용**
   - queue는 `ProjectRead` permission뿐 아니라 `Project.Read.All` 또는 사용자의 project assignment scope를 적용한다.
   - mutation은 `MaterialReceiptUpdate`와 project scope를 함께 검증하고, project/panel ID 추측으로 존재 여부가 노출되지 않도록 scope 밖은 일관되게 not-found 처리한다.
   - 제조 사용자는 같은 읽기 scope에서 키팅 완료와 연결된 자기 업무를 볼 수 있지만 mutation UI·API는 서버에서 차단한다.

5. **readiness의 exact SQL 경계**
   - 프로젝트는 active procurement item이 1건 이상이어야 한다. 대상은 `project_procurement_items.status='Active'` 전체이며 Purchased·CustomerSupplied를 모두 포함한다.
   - 모든 대상 item의 derived `receipt_completed=true`를 요구한다. individual receipt count나 IQC report 상태를 다시 계산해 두 번째 진실을 만들지 않는다.
   - queue와 mutation은 같은 readiness predicate를 공유한다. mutation은 project lock 뒤 다시 평가한다.
   - readiness가 false→true로 전환되는 경로는 `ConfirmAsync`와 `CloseArrivalsAsync` 둘 다 점검한다. 전환 시 자재 키팅 업무를 idempotent하게 생성하며, 기존 ready 데이터는 queue에서 누락되지 않고 키팅 mutation이 업무 존재 여부와 무관하게 동작해야 한다.

6. **soft-delete와 permanent purge lifecycle 구분**
   - 프로젝트 soft-delete·보류·취소와 패널 cancel은 신규 mutation을 차단하되 completion·batch·work item을 보존한다.
   - 기존 관리자 permanent purge는 이미 work item·panel을 실제 삭제하는 별도 lifecycle이다. 신규 FK가 purge를 깨지 않도록 purge transaction에서 batch/completion을 명시적으로 정리하거나 기존 project-owned cascade 정책과 정합시킨다.
   - “append-only”는 정상 업무·soft-delete 경계에서 update/delete API가 없다는 뜻으로 고정하고, 승인된 permanent purge까지 영구 보존한다고 과장하지 않는다. purge 회귀 테스트를 추가한다.

7. **패널 취소 후 고아 제조 업무 방지**
   - 키팅 완료 뒤 패널이 취소되면 완료 record는 보존하되 아직 Requested/InProgress인 해당 제조 work item은 기존 패널 취소 transaction 또는 동등한 후속 경계에서 Cancelled로 전환한다. Completed 업무는 덮어쓰지 않는다.
   - 이 보정이 현재 panel cancellation owner와 의미 있게 충돌하면 키팅 완료 패널 취소를 안전하게 차단하고 후속 정책 Finding으로 남긴다. 열린 제조 업무를 그대로 방치하는 안은 허용하지 않는다.

8. **모듈·화면 경계**
   - 이미 큰 `MaterialsStore.cs`와 `MaterialsWorkspace.tsx`에 키팅 전체를 누적하지 않는다. `PanelKittingStore`·contracts/endpoints와 `PanelKittingPage.tsx`·전용 type/API helper로 분리한다.
   - mobile global navigation은 기존 좌측 상단 숨김 menu를 유지한다. 완료 action은 페이지 내부 sticky/in-flow control일 수 있지만 bottom navigation을 다시 만들지 않는다.
   - 모바일은 compact 한 열 카드, 44px hit target, 선택 수·완료 영향 표시를 유지하고 desktop은 summary rail + 패널 grid로 구성해 모바일 확대판이 되지 않게 한다.

9. **응답·감사 snapshot 최소 계약**
   - 성공 응답은 `operationId`, 완료 패널 수, 새로 생성된 제조 업무 수, project completion 여부만 제공하고 내부 assignee·row identifier 원문을 노출하지 않는다.
   - readiness snapshot은 aggregate count와 검증 시각, predicate version 같은 bounded 값만 저장한다. 고객·품목명 원문을 JSON snapshot에 복제하지 않는다.
   - batch notification은 operation당 1건이고 manufacturing actionable work item과 구분한다. 외부 delivery는 생성하지 않는다.

### 보류

- BOM·패널별 자재 소요량·패널 내부 부분 키팅과 재고 차감
- 잘못된 완료의 정정·재키팅·관리자 강제 정정
- 제조 체크리스트·작업 시작/종료·제조 중단과 실제 제조 deep link
- 기존 ready 프로젝트에 키팅 내 업무를 소급 생성하는 운영 reconcile. queue·mutation 사용성은 보장하되 Persistent 적용 시 별도 rollout 판단
- 첨부·사진·Excel·PDF와 외부 notification delivery
- Persistent UAT migration·runtime handover와 대표 repo 게시

### 제거

- network 재시도를 무조건 “이미 완료” conflict로 반환하는 방식
- 마지막 패널 완료에서 `WorkflowStore.CompleteStageAsync`를 그대로 호출하는 방식
- 제조 assignee가 없어도 completion만 성공시키는 silent fallback
- `ProjectRead` permission 존재만 확인하는 무범위 queue
- 정상 업무 append-only와 관리자 permanent purge를 같은 “절대 삭제 없음”으로 표현하는 문구
- 대형 `MaterialsStore.cs`·`MaterialsWorkspace.tsx`에 모든 키팅 코드를 직접 추가하는 방식
- canceled panel의 open 제조 업무를 그대로 남기는 방식

## 3. 권장 상태·전이 계약

```text
readiness:
  active procurement item >= 1
  AND every active item.receipt_completed = true

batch:
  (없음) --operationId + panel set--> Processing --single transaction--> Completed
     ├─ same operation + same payload replay ---------------------------> Completed result
     ├─ same operation + different payload ----------------------------> Conflict
     └─ validation/assignee/concurrency failure ------------------------> no committed row/result

panel kitting:
  (없음) --batch completion--> Completed (정상 업무에서 불변)

manufacturing work:
  (없음) --same transaction--> Requested
  Requested/InProgress --panel cancelled--> Cancelled

project kitting stage:
  last active panel completed --project lock--> StageCompleted event 최대 1건
```

- batch와 completion, panel별 manufacturing work item, stage event, 묶음 notification은 한 DB transaction에서 모순 없이 커밋한다.
- 같은 operation replay는 write 없이 저장된 성공 projection을 반환한다.
- 완료 stage는 되돌리지 않는다. permanent purge는 별도 관리자 lifecycle로 구분한다.

## 4. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `010A-WORKFLOW-DUPLICATE` | P1 | `RESOLVED_FOR_REDRAFT` | generic stage completion은 프로젝트 단위 ManufacturingWork를 추가해 패널 업무와 중복 가능 | generic method 호출 금지, project lock 아래 event 직접 1회 + panel work만 생성 |
| `010A-RETRY-AMBIGUITY` | P1 | `RESOLVED_FOR_REDRAFT` | 첫 요청 성공 뒤 응답 유실 시 retry conflict라 사용자가 성공 여부를 복구 못함 | operationId·batch identity·동일 payload 성공 replay |
| `010A-ORPHAN-HANDOFF` | P1 | `RESOLVED_FOR_REDRAFT` | assignee 해석 실패를 무시하면 키팅 완료됐지만 제조 업무 없음 | manufacturing assignee 없으면 transaction 전체 rollback |
| `010A-READ-SCOPE` | P1 | `RESOLVED_FOR_REDRAFT` | permission만 확인하면 scope 밖 프로젝트·패널 노출 가능 | queue/mutation 모두 project scope helper 적용 |
| `010A-PURGE-LIFECYCLE` | P2 | `RESOLVED_FOR_REDRAFT` | completion FK/immutability가 기존 permanent purge를 깨거나 문서와 실제 삭제가 충돌 | soft-delete 보존과 approved purge 정리 구분, purge regression |
| `010A-READINESS-HOOK` | P2 | `RESOLVED_FOR_REDRAFT` | readiness transition 경로가 Confirm만 보면 CloseArrivals 경로에서 키팅 업무 누락 | 공통 predicate·transition hook, queue는 업무 유무와 무관하게 canonical |
| `010A-CANCELLED-PANEL-WORK` | P2 | `RESOLVED_FOR_REDRAFT` | 완료 패널 취소 뒤 open 제조 업무가 남으면 실행 불가 업무가 계속 노출 | open work cancel 또는 안전 차단, 완료 record 보존 |
| `010A-STAGE-RACE` | P2 | `RESOLVED_FOR_REDRAFT` | concurrent batch가 모두 마지막 패널로 판단하면 stage event·notification 중복 | project row lock + existing event check + operation idempotency |
| `010A-MODULE-BOUNDARY` | P3 | `RESOLVED_FOR_REDRAFT` | 기존 대형 자재 store/workspace 확장은 회귀·모바일 수정 비용 증가 | 전용 Backend store/contracts/endpoints와 Frontend page 분리 |

Review 기준 Open P0/P1/P2는 `0/0/0`이다. 이는 Fable 2차 기획이 위 resolution을 반영한다는 조건부 판정이며 아직 코드 구현 완료 판정이 아니다.

## 5. 자동 채택할 비차단 결정

| 항목 | 채택안 | 근거 |
| --- | --- | --- |
| 부분 키팅 | 활성 패널 선택 단위 완료 | BOM 없는 상태에서 증명 가능한 최소 단위 |
| readiness | active procurement item 전체 derived 완료 | 기존 008A 단일 진실을 그대로 사용 |
| batch | all-or-nothing + operation replay | 대량 처리 결과와 network 복구를 모두 명확히 함 |
| completion | panel당 불변 record | stage 전진-only·감사·중복 방지 |
| 제조 업무 | panel당 1건, assignee 필수 | 실제 제조 실행 단위와 handoff 일치 |
| project stage | 마지막 active panel에서 event 1건 | 18단계 집계와 패널 진행을 연결 |
| 삭제 lifecycle | soft-delete 보존, approved permanent purge 정합 | 감사와 기존 관리자 lifecycle 둘 다 보존 |
| 모바일 UI | compact card 선택 + 좌측 상단 global menu 유지 | 현장 중심 adaptive 원칙과 standing 디자인 규칙 |

## 6. 2차 기획이 고정할 최소 구현 계약

1. additive `0033`은 idempotent batch와 panel completion을 구분할 최소 schema·constraint·index를 추가한다. 기존 migration·`panel_placeholders.workflow_stage`는 수정하지 않는다.
2. queue와 mutation은 같은 readiness predicate와 project scope를 사용하고, mutation은 project·panel row lock 뒤 재검증한다.
3. 요청은 bounded operationId를 포함한다. 동일 operation+payload는 성공 replay, operation reuse+다른 payload와 다른 operation의 완료 panel 중복은 conflict다.
4. panel completion·제조 assignee resolution·panel work item·last-panel stage event·kitting work completion·batch notification을 한 transaction에서 처리한다. assignee가 없으면 전부 rollback한다.
5. generic `WorkflowStore.CompleteStageAsync`를 호출하지 않고 project-level 중복 제조 업무를 만들지 않는다.
6. 정상 업무와 soft-delete에서는 completion을 immutable하게 보존하되 기존 permanent purge와 panel cancellation 회귀를 명시적으로 처리한다.
7. Backend와 Frontend를 전용 키팅 module/page로 분리하고 desktop·390px·Teams narrow에서 loading/empty/error/ready/selection/replay 상태를 검증한다.
8. migration fresh/existing, scope·role matrix, readiness, batch replay/conflict, concurrency, assignee failure, last-panel stage, cancellation/purge와 008A~009A 회귀를 검증한다.

## 7. 권장 구현 순서

1. `0033` batch/completion schema·constraint·purge lifecycle과 migration tests
2. `PanelKittingStore`의 scope/readiness/queue·operation replay·project lock transaction
3. readiness transition의 자재 키팅 work hook, panel manufacturing work·stage event·notification 연결
4. panel cancellation·permanent purge 회귀 보정
5. `/materials/kitting` API·전용 Frontend page·route/menu/deep link
6. Backend targeted/전체, Frontend unit/build, isolated Full-Stack E2E
7. desktop·390px screenshot, implementation report·5종 산출물·local commit

## 8. 판정

위 resolution을 반영한 Fable 2차 기획을 구현 source of truth로 사용하면 실험 branch 구현은 `GO`다. 이 판정은 대표 repo, push, PR, merge 또는 Persistent UAT 승인이 아니다.
