# TASK-013A — 물류 포장·출발·납품 1차 기획 Codex 내용 Review

> Review 대상: `tasks/013a-planning.md` Fable 5 원문
>
> Review 성격: 사용자 문제·제품 방향·Roadmap·실제 Repository·구현 경계 1회 검토
> 결과: 2차 Fable 기획 전 필수 보정 — 아래 resolution을 최종 구현 계약에 반영

## 1. 총평

품질 완료 panel을 flat Packing Unit으로 묶고, 포장사진·상차사진·거래명세서 서명본을 단계별 필수 증빙으로 받아 포장 → 출발 → 납품을 전진시키는 방향은 TASK-013A의 핵심 문제와 Roadmap에 맞는다. panel별 즉시 다음 업무 인계, 모든 active panel 기반 project event·영업 정산 skeleton, 확정 기록 append-only, 모바일 현장 행동 중심 화면도 유지할 가치가 크다. 외부 운송·전자서명 생성·부분 납품·반품·정산 상세를 보류한 범위 역시 MVP에 적절하다.

다만 1차 기획의 “물류 담당 또는 active assignee” 권한을 다중 panel/unit operation에 그대로 적용하면 사용자가 맡지 않은 panel의 업무까지 묶어 완료할 수 있다. 출발·납품 일괄 선택의 동일 project·unit 원자성, draft membership 경쟁, 증빙 content 검증, 다음 담당자 부재 시 rollback, cancellation/purge 경계도 더 구체적이어야 한다. 기존 panel coarse stage에는 출발 단계가 없으므로 이를 억지로 추가하거나 `ShipmentCompleted`를 출발 완료로 사용하면 납품 완료 의미가 깨진다. 아래 resolution을 반영하면 실험 구현 source로 사용할 수 있다.

## 2. 기능 판단

### 유지

- 품질 최종 합격에서 생성된 panel `PackingCompleted` skeleton을 출발점으로 하는 15~17단계 흐름
- 같은 project panel을 하나의 flat Packing Unit으로 묶고 panel의 active unit 중복 소속을 금지하는 모델
- 포장사진 1장 이상, 상차사진 1장 이상과 출발일, 서명본 1개 이상을 각 확정의 필수 조건으로 두는 정책
- panel별 다음 업무 즉시 생성과 모든 active panel 기반 project stage event·영업 정산 skeleton exactly-once
- draft 한정 수정, finalized record·mapping·evidence append-only와 operation fingerprint replay
- open panel/project Pending, 선행 단계, stale version, 타 project 혼합을 Backend에서 차단하는 원칙
- `/logistics` 한 화면 안의 포장·출발·납품 stage switch, 모바일 행동 재구성, desktop 관리 composition
- 외부 운송·GPS·전자서명 생성·부분 납품·정정·반품·정산 상세·Persistent UAT·게시 제외

### 추가

1. **다중 대상 전체에 대한 mutation 권한 교집합**
   - `logistics.ship`와 project scope를 항상 요구한다. 프로젝트 `LogisticsPrimary/Secondary` 배정자는 해당 project의 물류 operation을 수행할 수 있고, 그렇지 않은 active work assignee는 선택한 모든 panel의 현재 stage 업무를 자신이 배정받은 경우에만 수행할 수 있다.
   - panel 하나라도 조건을 충족하지 않으면 전체 요청을 rollback한다. System Administrator·다른 부서·일부 panel assignee의 우회를 허용하지 않는다.
   - 조회와 evidence download는 `projects.read` + project scope로 분리하고, scope 밖 식별자는 404로 비노출한다.

2. **Packing Unit membership과 번호의 동시성 경계**
   - draft 생성·panel 추가/제거 때 project와 panel row를 안정된 순서로 lock하고, active panel·품질 완료·open `PackingCompleted` 업무·동일 project·다른 active unit 미소속을 재검증한다.
   - unit 번호는 project별 별도 counter 또는 advisory/row lock으로 중복 없이 부여한다. nullable cancellation을 이용한 불완전한 unique가 아니라 active membership을 DB partial unique index로 방어한다.
   - finalized/cancelled unit의 핵심 필드·mapping은 정상 API에서 수정하지 못하게 trigger로 보호한다. 빈 draft는 허용하되 finalize는 panel 1개 이상이어야 한다.

3. **출발·납품 batch의 동일 project·unit 원자성**
   - 출발 batch는 같은 project의 `Finalized`이면서 아직 출발되지 않은 unit 1개 이상만 포함한다. batch 확정 시 선택 unit 전체와 소속 panel 전체가 원자적으로 Departed가 된다.
   - 납품 batch는 같은 project의 Departed unit 1개 이상만 포함하며, MVP에서는 선택 unit의 active panel 전체를 원자적으로 Delivered 처리한다. unit 일부 panel 납품은 부분 납품 기능으로 보류한다.
   - unit은 finalized 출발 batch와 finalized 납품 batch 각각 최대 한 곳에만 속하도록 unique constraint를 둔다. 서로 다른 project unit 혼합과 cancelled/inactive panel 끼워 넣기를 막는다.

4. **coarse panel stage와 세부 물류 상태 분리**
   - 기존 `panel_placeholders.workflow_stage`는 `InspectionCompleted → PackingCompleted → ShipmentCompleted`만 사용한다. `PackingCompleted`는 포장 확정, `ShipmentCompleted`는 납품 확정에서만 전진시킨다.
   - 출발 상태는 finalized departure record에서 파생한다. coarse stage에 신규 Departure 값을 추가하거나 출발 확정에서 `ShipmentCompleted`로 올리지 않는다.
   - stage queue·last-panel 판정은 coarse stage만 믿지 않고 finalized unit/departure/delivery 관계를 authoritative source로 사용한다.

5. **domain transaction만 업무와 단계를 전이**
   - `target_type='Panel'`인 `PackingCompleted`, `DepartureProcessed`, `DeliveryCompleted` 업무는 generic `/api/my-work/{id}/start|complete|cancel`을 conflict로 차단한다.
   - 포장/출발/납품 확정 transaction이 현재 업무 완료, record finalize, evidence 결속, panel stage 전진, 다음 업무·인앱 notification 생성을 함께 처리한다. 어느 하나라도 실패하면 모두 rollback한다.
   - deep link는 `/logistics?stage=packing|departure|delivery&project=...&panel=...`으로 연결한다.

6. **담당자 해석 실패와 마지막 panel 인계 rollback**
   - 포장 확정 전에 다음 출발 담당자, 출발 확정 전에 다음 납품 담당자를 모든 panel에 대해 해석한다. 마지막 납품에서는 SalesPrimary→Secondary→허용된 fallback 담당자를 해석한다.
   - 담당자가 하나라도 없으면 현재 record 확정·업무 완료·panel stage 전진·project event를 수행하지 않고 한글 conflict를 반환한다.
   - 정산 skeleton은 `target_type='Project'`, stage `SalesSettlementCompleted`, key `logistics:project:{projectId}:sales-settlement` 한 건이며 TASK-014A 상세 record를 미리 만들지 않는다.

7. **project stage event의 명확한 source와 순서**
   - 취소되지 않은 active panel 전체가 finalized packing membership을 가지면 `PackingCompleted`, 전체가 finalized departure membership을 가지면 `DepartureProcessed`, 전체가 finalized delivery membership을 가지면 `DeliveryCompleted` event를 project row lock 아래 exactly-once 기록한다.
   - 각 event는 source batch/unit identity와 bounded correlation만 보존하고 사진·서명 원문을 복제하지 않는다. 마지막 납품 transaction에서 `DeliveryCompleted` event와 정산 skeleton을 함께 생성한다.
   - project stage event는 panel별 다음 업무 생성을 대신하지 않는다.

8. **evidence 보안·validation·불변성**
   - 포장·상차 사진은 JPEG/PNG magic byte를 검증하고 각 5MB, record당 1~5장으로 제한한다. 서명본은 JPEG/PNG/PDF magic byte, 각 10MB, record당 1~3개로 제한하며 확장자·클라이언트 MIME만 신뢰하지 않는다.
   - bounded file name, normalized MIME, byte size, sha256, 필수 alt text(사진)와 actor/time을 저장한다. operation receipt에는 파일명·설명·원문 bytes를 복제하지 않는다.
   - draft evidence만 추가·삭제할 수 있고 finalized owner의 evidence·mapping은 trigger로 update/delete를 차단한다. download는 `private, no-store`와 scope를 적용한다.

9. **idempotency·version·replay의 action별 계약**
   - 모든 mutation에 operationId와 payload fingerprint를 적용하되 evidence upload의 fingerprint에는 content sha256을 포함하고 raw bytes는 receipt에 저장하지 않는다.
   - 같은 operationId·같은 fingerprint는 bounded 성공 projection을 replay하고, 다른 fingerprint는 409로 차단한다. expectedVersion은 draft owner와 확정 대상 batch에 적용한다.
   - finalize 경쟁은 owner/project/panel/current work rows를 lock하고 DB unique constraint를 최종 방어선으로 사용한다.

10. **Pending·취소·purge 정합**
    - finalize 직전에 대상 project와 선택 panel의 `status <> 'Closed'` Pending을 다시 검사한다. unrelated project의 Pending이나 이미 Closed인 Pending은 차단하지 않는다.
    - project/panel 취소는 draft unit/batch와 미완료 물류 업무만 terminal 정리하고 finalized evidence와 history를 보존한다. finalized unit에 속한 panel을 취소하더라도 기존 확정 record를 재작성하지 않으며 이후 active-panel 집계에서만 제외한다.
    - approved permanent project purge는 신규 receipt/evidence/batch mapping/unit을 FK 역순으로 삭제하고 immutability trigger는 `emi_qms.project_purge`에서만 예외를 허용한다.

11. **화면 상태와 모바일 정보 우선순위**
    - queue는 `오늘 할 일 수 → 차단/지연 → 선택 대상 → 증빙 → 확정` 순서로 구성하고 모바일에서는 선택 stage 하나의 핵심 정보만 한 column에 보인다. desktop의 모든 표·history를 모바일 첫 화면에 복제하지 않는다.
    - mobile stage control은 compact segmented/타원 pill, 핵심 수량은 원형, 선택 unit은 둥근 사각, finalized record는 각진 사각으로 의미를 구분한다. 좌상단 숨김 메뉴를 유지하고 하단 고정 nav·page horizontal scroll을 추가하지 않는다.
    - 업로드 중, 빈 queue, 조회 오류, 권한 부족, Pending 차단, stale conflict, 성공과 다음 행동을 서로 다른 상태로 표시한다. 확정 action은 sticky bottom bar가 아닌 내용 흐름 안에 둔다.

### 보류

- Packing Unit 포장방식 기준정보, 자동 규격·중량·계층형 box/pallet/container
- object storage·retention 전환과 운영 증빙 용량 상향
- 부분 출발·부분 납품, unit 분할·병합, 재포장·정정·출발 취소·반품
- 운송사·기사·차량·GPS·송장·외부 고객 portal·전자서명 생성
- 거래명세서/PDF 생성, OCR·QR·barcode, Excel export
- TASK-014A 세금계산서·프로젝트 완료 상세
- Persistent UAT, 실제 provider, 대표 repo, push, PR, merge

### 제거

- 선택 대상 중 한 panel의 assignee 권한만으로 다중 panel 업무를 일괄 완료하는 방식
- 서로 다른 project unit을 하나의 출발·납품 batch에 섞는 방식
- 출발 확정에서 coarse panel stage를 `ShipmentCompleted`로 올리는 방식
- generic 내 업무 완료로 필수 증빙·domain transaction을 우회하는 방식
- 다음 물류·영업 담당자가 없어도 현재 record만 finalize하는 방식
- 파일 확장자·클라이언트 MIME만으로 evidence를 신뢰하는 방식
- finalized record·mapping·evidence를 정상 API에서 수정·삭제하는 방식
- TASK-014A 상세 정산 record를 이번 Task에서 선구현하는 방식

## 3. 권장 상태·transaction 계약

```text
PackingCompleted work Requested
  --Packing Unit finalize + packing photo-->
     unit Finalized + packing work Completed + panel coarse PackingCompleted
     + DepartureProcessed work Requested

DepartureProcessed work Requested
  --departure batch finalize + loading photo + departure date-->
     departure Finalized + departure work Completed
     + DeliveryCompleted work Requested
     (panel coarse stage 변경 없음)

DeliveryCompleted work Requested
  --delivery batch finalize + signed evidence-->
     delivery Finalized + delivery work Completed + panel coarse ShipmentCompleted
     + [모든 active panel Delivered] DeliveryCompleted project event
     + SalesSettlementCompleted project work exactly-once
```

- 각 finalize는 동일 project의 대상 unit/panel/current work/actor scope를 lock·재검증하고 record·evidence·업무·stage·event·next handoff를 한 transaction으로 처리한다.
- draft unit/departure/delivery와 evidence는 versioned mutation을 허용하되 finalized/cancelled owner는 정상 API에서 변경하지 않는다.
- project stage event는 모든 active panel의 finalized domain relation을 집계한 결과이며 coarse panel stage만으로 추정하지 않는다.

## 4. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `013A-MULTI-TARGET-AUTHORIZATION` | P1 | `RESOLVED_FOR_REDRAFT` | 일부 panel assignee가 다중 대상 전체를 확정할 위험 | project 물류 배정 또는 선택 panel 전체 current-work assignee 교집합 검증 |
| `013A-MEMBERSHIP-RACE` | P1 | `RESOLVED_FOR_REDRAFT` | draft 동시 구성으로 panel 이중 소속·unit 번호 충돌 가능 | stable row lock + active membership partial unique + project별 번호 직렬화 |
| `013A-BATCH-PROJECT-BOUNDARY` | P1 | `RESOLVED_FOR_REDRAFT` | 출발·납품 batch의 project·unit 원자성 불명확 | 동일 project finalized unit만, unit 전체 panel 원자 확정, 단계별 unit unique |
| `013A-DIRECT-WORK-BYPASS` | P1 | `RESOLVED_FOR_REDRAFT` | generic work 완료가 evidence 없이 단계를 우회 가능 | 물류 3 stage generic transition 차단, domain finalize만 업무 전이 |
| `013A-HANDOFF-ROLLBACK` | P1 | `RESOLVED_FOR_REDRAFT` | 다음 담당자 부재 시 finalized record와 handoff 분리 가능 | 다음 물류/영업 담당자 선해석, 실패 시 전체 transaction rollback |
| `013A-COARSE-STAGE-SEMANTICS` | P2 | `RESOLVED_FOR_REDRAFT` | 기존 coarse stage에 출발 값이 없어 ShipmentCompleted 오용 가능 | 포장·납품에서만 coarse 전진, 출발은 finalized record에서 파생 |
| `013A-PROJECT-EVENT-SOURCE` | P2 | `RESOLVED_FOR_REDRAFT` | last-panel 집계 source가 모호하면 조기 project 완료 가능 | active panel + finalized domain relation 집계와 project lock |
| `013A-EVIDENCE-BOUNDARY` | P2 | `RESOLVED_FOR_REDRAFT` | content spoof·무권한 download·finalized 변조 위험 | magic-byte/size/count/scope/no-store/immutability trigger 계약 |
| `013A-REPLAY-CONCURRENCY` | P2 | `RESOLVED_FOR_REDRAFT` | upload/finalize 재시도와 경쟁에서 중복·stale 처리 불명확 | content hash fingerprint, bounded replay, version+lock+unique 방어 |
| `013A-CANCEL-PURGE` | P2 | `RESOLVED_FOR_REDRAFT` | soft cancel·approved purge가 append-only trigger와 충돌 가능 | draft/work terminal 정합, finalized 보존, purge flag·FK 역순 검증 |
| `013A-MOBILE-ACTION-PRIORITY` | P3 | `RESOLVED_FOR_REDRAFT` | desktop 이력/표를 모바일에 복제하면 현장 행동이 밀림 | stage별 one-column 행동 우선·도형 의미·좌상단 숨김 메뉴 |

Review 기준 Open P0/P1/P2는 `0/0/0`이다. 이는 Fable 2차 기획이 위 resolution을 반영한다는 조건부 판정이며 아직 코드 구현 완료 판정이 아니다.

## 5. 자동 채택할 비차단 결정

| 항목 | 채택안 | 근거 |
| --- | --- | --- |
| Packing 구조 | same-project flat unit, project별 순번, optional 메모·규격·중량 text | Roadmap 최소 단위 충족, 계층형 포장 과잉 방지 |
| 출발·납품 범위 | same-project unit batch, 선택 unit 전체 panel 원자 처리 | 일괄 현장 행동과 panel 추적을 함께 보존 |
| panel 상태 | finalized relation 파생 + coarse는 포장/납품만 전진 | 기존 stage enum 의미 보존, 이중 상태 drift 방지 |
| 증빙 | bytea MVP, 사진 1~5×5MB, 서명 1~3×10MB | 기존 0035 계약 재사용, 운영 storage 결정 보류 |
| 담당 권한 | project 물류 정·부 또는 선택 대상 전체 active assignee | 편의와 최소 권한을 함께 만족 |
| 정산 인계 | project target skeleton 1건 | TASK-014A 책임을 침범하지 않음 |
| 모바일 | stage → 대상 → 증빙 → 확정 one-column | PC 축소가 아닌 현장 핵심 작업 우선 |

## 6. 2차 기획이 고정할 최소 구현 계약

1. additive `0036`은 unit/membership, departure/delivery batch와 unit mapping, evidence, operation receipt, immutability trigger·unique/index를 추가하고 기존 migration을 수정하지 않는다.
2. 모든 mutation은 `logistics.ship` + scope + project 물류 배정 또는 대상 전체 current-work assignee를 transaction 안에서 검증한다.
3. 포장·출발·납품은 동일 project의 unit 전체 panel을 원자 처리하고 generic work 완료 우회를 차단한다.
4. coarse panel stage는 포장 `PackingCompleted`, 납품 `ShipmentCompleted`만 사용하며 출발은 domain record에서 파생한다.
5. evidence는 bounded content sniff·hash·scope·no-store·append-only 계약을 적용한다.
6. 다음 담당자 부재는 현재 finalize 전체를 rollback하고, last-panel event·Sales skeleton을 project lock과 unique key로 exactly-once 생성한다.
7. Pending, cancellation, permanent purge, replay/stale/concurrency를 migration·Backend test로 검증한다.
8. `/logistics`는 모바일 stage별 행동 화면과 desktop queue/detail composition, deep link·action feedback을 구현한다.
9. fresh/existing migration, targeted/전체 Backend, Frontend unit/lint/typecheck/build, isolated Full-Stack E2E와 desktop·390px screenshot을 수행한다.

## 7. 권장 구현 순서

1. `0036` schema·constraint·immutability·purge migration tests
2. Backend Logistics contracts/store/endpoints와 generic work guard·deep link
3. 포장 → 출발 → 납품 transaction과 handoff/project event/Sales skeleton
4. evidence upload/download·replay/version/concurrency·authorization tests
5. `/logistics` adaptive Frontend·menu·API client·component test
6. Backend/Frontend 전체 회귀와 isolated Full-Stack E2E
7. desktop·390px screenshot, 5종 종료 산출물, Roadmap experiment 상태, local commit

## 8. 판정

위 resolution을 반영한 Fable 2차 기획을 구현 source of truth로 사용하면 실험 branch 구현은 `GO`다. 이 판정은 대표 repo, push, PR, merge 또는 Persistent UAT 승인이 아니다.
