# TASK-013A — 물류 포장·출발·납품 완료 2차 기획 (실험 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-013A`
- authoringModel: `FABLE_5`

이 문서는 `tasks/013a-interview.md`(확인 완료 interview), `tasks/013a-planning.md`(Fable 1차 기획 전문), `tasks/013a-review.md`(Codex 내용 review 전문), `tasks/013a-change-001.md`(exact target 승인)와 현재 Repository 구현·tests를 직접 다시 읽고 작성한 TASK-013A의 최종 구현 source of truth다. 1차 기획의 유지 판정 내용을 보존하고 review의 추가·보류·제거 권고와 Finding resolution 전부를 구현 가능한 계약으로 통합했다. 1차 기획과 review 원문은 수정하지 않고 판단 이력으로 보존한다. 공통 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 따르며 여기에 복사하지 않는다. 이 문서는 experiment branch 구현·검증·local commit까지만 다루며 대표 repo·`main` merge(승인 0/3)·push·PR·Persistent UAT·실제 provider에 대한 어떤 승인도 부여하지 않는다.

## 1. 목표와 해결할 업무 문제

물류 담당자가 품질 완료 panel을 flat Packing Unit으로 묶어 필수 포장사진과 함께 확정하고, 상차사진·출발일과 거래명세서 서명본을 거쳐 panel별 납품 완료까지 순서대로 처리하면, 시스템이 다음 물류 업무와 영업 정산 skeleton을 정확히 한 번 자동 생성한다.

- 현재 `QualityInspectionStore`는 전진검수/FAT 최종 합격 시 idempotency key `quality:panel:{panelId}:packing`으로 panel target `PackingCompleted` 내 업무를 LogisticsPrimary→LogisticsSecondary→Sales→System Administrator fallback으로 생성하고 panel coarse stage를 `InspectionCompleted`까지만 전진시킨다. 그 이후를 받는 물류 record·화면이 없다.
- 이 skeleton 업무는 `WorkflowStore.LinkUrlForWorkItem`의 fallback(프로젝트 workflow 요약)으로만 연결되고 generic 내 업무 완료 차단 목록에 없어 증빙 없는 완료 우회가 가능하다.
- panel–포장 mapping, 포장·상차 사진, 출발일, 거래명세서 서명본이 시스템에 없어 Roadmap 17장 물류 기준과 TASK-014A 완료 조건의 근거를 만들 수 없다.

## 2. 범위

### 포함

- 품질 완료 panel의 포장 대기 queue와 same-project flat Packing Unit 생성·draft 수정·취소·확정
- panel의 active unit 이중 소속 금지(DB partial unique)와 project별 unit 순번 직렬화
- 필수 포장사진, 필수 상차사진·출발일, 필수 거래명세서 서명본(JPEG/PNG/PDF)
- same-project unit 단위 출발·납품 batch와 선택 unit 전체 panel의 원자 확정
- panel별 다음 업무 즉시 생성, 모든 active panel finalized relation 집계 기반 project stage event·영업 정산 skeleton exactly-once
- `logistics.ship` + project scope + 다중 대상 전체에 대한 담당 교집합 서버 권한
- 물류 3 stage의 generic 내 업무 전이 차단과 `/logistics` deep link
- operation fingerprint/replay, expectedVersion, row lock·unique constraint 동시성 방어
- open project/panel Pending 차단, cancellation·approved permanent purge 정합
- 확정 record·mapping·증빙 append-only DB trigger
- 모바일 우선 adaptive `/logistics` 화면과 desktop queue/detail composition
- additive migration `0036`, Backend·Frontend·isolated E2E 검증, desktop·390px screenshot

### 제외 (review 보류·제거 확정)

- 운송사·기사·차량·GPS·송장·택배 API·외부 고객 portal
- 전자서명 생성, 거래명세서 문서/PDF 생성, OCR·바코드·QR scan
- 포장방식 기준정보 관리자, 자동 규격·중량 계산, 계층형 box/pallet/container
- 부분 출발·부분 납품, unit 분할·병합, 재포장·정정·출발 취소·반품
- Excel export, 신규 외부 알림 채널·실제 provider delivery
- TASK-014A 세금계산서·프로젝트 완료 상세 record 선구현
- Persistent UAT migration·write·runtime handover, 대표 repo·`main`·push·PR·merge

다음 방식은 review 제거 판정에 따라 구현하지 않는다: 일부 panel assignee 권한만으로 다중 대상 일괄 확정, 서로 다른 project unit 혼합 batch, 출발 확정에서 coarse stage `ShipmentCompleted` 전진, generic 내 업무 완료 우회, 다음 담당자 부재 상태의 finalize, 확장자·클라이언트 MIME만의 evidence 신뢰, 정상 API의 finalized 수정·삭제.

## 3. 역할과 권한 계약

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 물류 정·부 담당 | 포장 queue, unit 구성·사진·확정, 출발·납품 처리 | 기존 project access scope | 아래 mutation 교집합 규칙 충족 시 |
| 영업 정·부 담당 | 물류 상태·증빙 조회, 정산 skeleton 수신 | 기존 project access scope | 물류 mutation 불가 |
| 생산관리·품질·제조·조회 역할 | 허용 project의 물류 상태 조회 | 기존 project access scope | 물류 mutation 불가 |
| Pending 조치 담당자 | 물류 차단 이슈 조치 | 기존 Pending scope | 기존 Pending 계약 안에서만 |
| System Administrator | 기준·이력·증빙 조회 | 전체 | 물류 업무 입력 무제한 우회 금지 |

다중 대상 mutation 권한 교집합(`013A-MULTI-TARGET-AUTHORIZATION` resolution):

1. 모든 물류 mutation은 `logistics.ship` 권한과 대상 project의 access scope를 항상 요구한다.
2. 해당 project의 `LogisticsPrimary` 또는 `LogisticsSecondary` 배정자는 그 project의 물류 operation을 수행할 수 있다.
3. 물류 배정자가 아닌 actor는 이번 요청이 영향을 주는 모든 panel의 현재 stage active 물류 내 업무 assignee가 본인인 경우에만 수행할 수 있다. 선택 대상 중 panel 하나라도 이 조건을 충족하지 않으면 전체 요청을 rollback하고 한글 오류를 반환한다.
4. System Administrator·다른 부서·일부 panel assignee의 우회를 허용하지 않는다. 권한 검증은 finalize transaction 안에서 lock된 최신 상태로 재수행한다.
5. 조회와 evidence download는 기존 project 조회 권한(`QmsPermissions.ProjectRead`) + project scope로 분리하고, scope 밖 식별자는 404로 비노출한다.

## 4. 데이터 모델 계약 — additive migration `0036`

현재 latest는 `0035_panel_quality_inspections`다. `database/migrations/0036`(additive 1건)은 기존 migration을 수정·번호 재사용하지 않고, 기존 panel에 물류 record를 backfill하지 않는다. 테이블·컬럼 최종 명명은 기존 convention에 맞춰 조정할 수 있으나 아래 계약 의미는 고정한다.

| 개념 | 계약 | 보존·감사 |
| --- | --- | --- |
| Packing Unit | project별 순번(unique(project, 순번)), status `Draft/Finalized/Cancelled`, version, 선택 비고·규격·중량 bounded text, 생성·확정 actor/시각 | Finalized 후 핵심 필드 immutable trigger |
| Unit–panel membership | unit–panel row에 active 여부를 표현하는 컬럼을 두고 unit status 변경과 같은 transaction에서 동기화, `panel당 active membership 최대 1건`을 partial unique index로 DB에서 강제 | Finalized unit의 mapping immutable trigger |
| 포장 사진 | unit당 1~5장, JPEG/PNG, 각 최대 5MB, bounded 파일명 패턴·normalized MIME·byte_size=octet_length·sha256·필수 alt_text·actor/시각 | Finalized owner에서 insert/update/delete trigger 차단 |
| 출발 batch | same-project, status `Draft/Finalized/Cancelled`, version, finalize 시 출발일 필수, batch–unit mapping, `unit당 finalized 출발 batch 최대 1` partial unique | Finalized 후 immutable |
| 상차 사진 | batch당 1~5장, 포장 사진과 동일 계약 | 동일 |
| 납품 batch | same-project, status `Draft/Finalized/Cancelled`, version, batch–unit mapping(`unit당 finalized 납품 batch 최대 1` partial unique), finalize 시 선택 unit의 active panel별 납품 result row 기록 | Finalized 후 immutable |
| 거래명세서 서명본 | batch당 1~3개, JPEG/PNG/PDF, 각 최대 10MB, bounded 파일명·MIME·size·sha256·actor/시각 | 동일 |
| 물류 operation receipt | operation_id PK, action, project/unit/batch/panel 참조, actor, payload_fingerprint(sha256 hex), bounded result_projection jsonb, created_at | append-only, 파일 원문 bytes·파일명·설명 비복제 |

추가 DB 계약:

- unit 순번은 project row lock 또는 전용 counter로 직렬화해 중복 없이 부여한다(`013A-MEMBERSHIP-RACE`). nullable 컬럼 기반의 불완전한 unique를 사용하지 않는다.
- immutability trigger는 0035의 `guard_finalized_*` 패턴을 준용하고 `emi_qms.project_purge = 'on'`에서만 예외를 허용한다.
- approved permanent project purge 절차에 신규 테이블을 FK 역순으로 추가하고 migration/purge test로 검증한다.
- fresh DB와 existing DB(0035까지 적용) 모두에서 catalog·ledger·schema compatibility를 검증한다.

## 5. 상태 모델 계약

```text
PackingCompleted work Requested
  --unit finalize(사진 1+ / panel 1+)-->
     unit Finalized + packing work Completed + panel coarse 'PackingCompleted'
     + panel별 DepartureProcessed work Requested

DepartureProcessed work Requested
  --departure batch finalize(상차사진 1+ / 출발일)-->
     batch Finalized + departure work Completed
     + panel별 DeliveryCompleted work Requested
     (panel coarse stage 변경 없음 — 출발 상태는 finalized record에서 파생)

DeliveryCompleted work Requested
  --delivery batch finalize(서명본 1+)-->
     batch Finalized + panel별 delivery result + delivery work Completed
     + panel coarse 'ShipmentCompleted'
     + [모든 active panel Delivered] 'DeliveryCompleted' project event
       + SalesSettlementCompleted project work exactly-once
```

- coarse `panel_placeholders.workflow_stage`는 기존 enum 값만 사용한다: 포장 확정에서 `PackingCompleted`, 납품 확정에서 `ShipmentCompleted`로만 기존 case-순서 전진-only update를 준용해 전진한다. 신규 coarse 값을 추가하지 않고 출발 확정은 coarse stage를 변경하지 않는다(`013A-COARSE-STAGE-SEMANTICS`).
- panel별 세부 물류 상태(`PackingRequested → Packed → DepartureRequested → Departed → DeliveryRequested → Delivered`)는 mutable 컬럼 없이 finalized unit/departure/delivery relation과 batch draft membership에서 파생한다. stage queue·last-panel 판정은 coarse stage가 아니라 finalized domain relation을 authoritative source로 사용한다.
- 전진-only: 실패·Pending·취소가 이미 확정된 단계를 후퇴시키지 않는다.

## 6. 업무·transaction 계약

### 6.1 포장

- 포장 대기 queue: active panel 중 open(`Requested/InProgress`) `PackingCompleted` 업무를 보유하고 active unit membership이 없는 panel.
- draft mutation(unit 생성, panel 추가/제거, 사진 추가/제거): project·대상 panel row를 안정된 순서로 lock하고 panel Active, 동일 project, 품질 완료(open `PackingCompleted` 업무), 다른 active unit 미소속, draft 상태, expectedVersion을 재검증한다. 빈 draft는 허용하되 finalize는 panel 1개 이상·사진 1장 이상을 요구한다.
- 포장 확정 transaction: 권한 교집합·Pending·증빙·membership·version 재검증 → 모든 소속 panel의 다음 출발 담당자(LogisticsPrimary→LogisticsSecondary→Sales→System Administrator 기존 fallback) 선해석 → unit Finalized, 소속 panel의 `PackingCompleted` 업무 Completed, panel coarse `PackingCompleted` 전진, panel별 `DepartureProcessed` 업무 생성(key `logistics:panel:{panelId}:departure`)과 기존 계약의 참조 알림, `PackingCompleted` project event 집계 시도, operation receipt 기록 — 전부 한 transaction.

### 6.2 출발

- 출발 batch에는 같은 project의 `Finalized`이며 finalized 출발 batch에 속하지 않은 unit만 1개 이상 포함한다. 서로 다른 project unit 혼합과 cancelled/inactive panel 포함을 차단한다(`013A-BATCH-PROJECT-BOUNDARY`).
- 출발 확정 transaction: 권한 교집합·Pending·상차사진 1+·출발일·unit 상태·version 재검증 → 모든 영향 panel의 다음 납품 담당자 선해석 → batch Finalized, 선택 unit 전체와 소속 active panel 전체가 원자적으로 Departed 파생 상태가 되고, panel별 `DepartureProcessed` 업무 Completed, panel별 `DeliveryCompleted` 업무 생성(key `logistics:panel:{panelId}:delivery`), `DepartureProcessed` project event 집계 시도, receipt 기록. coarse stage는 변경하지 않는다.

### 6.3 납품

- 납품 batch에는 같은 project의 Departed unit만 1개 이상 포함한다. MVP에서는 선택 unit의 active panel 전체를 원자적으로 Delivered 처리하며 unit 일부 panel 납품은 부분 납품 기능으로 보류한다.
- 납품 확정 transaction: 권한 교집합·Pending·서명본 1+·unit 상태·version 재검증 → 이번 확정으로 모든 active panel이 Delivered가 되는 경우 SalesPrimary→SalesSecondary→기존 허용 fallback의 정산 담당자 선해석 → batch Finalized, panel별 납품 result row 기록, panel별 `DeliveryCompleted` 업무 Completed, panel coarse `ShipmentCompleted` 전진, last-panel인 경우 같은 transaction에서 `DeliveryCompleted` project event와 `SalesSettlementCompleted` project 업무(target_type `Project`, key `logistics:project:{projectId}:sales-settlement`) exactly-once 생성, receipt 기록. TASK-014A 상세 정산 record는 만들지 않는다.

### 6.4 공통 규칙

- 다음 담당자(출발·납품·정산)가 한 명이라도 해석되지 않으면 현재 finalize의 record 확정·업무 완료·stage 전진·event를 수행하지 않고 전체 rollback 후 한글 conflict를 반환한다(`013A-HANDOFF-ROLLBACK`).
- `target_type='Panel'`인 `PackingCompleted`/`DepartureProcessed`/`DeliveryCompleted` 업무는 generic `/api/my-work/{id}/start|complete|cancel`(`TransitionWorkItemAsync`)에서 conflict로 차단하고 `/logistics` 전용 화면을 안내한다(`013A-DIRECT-WORK-BYPASS`).
- project stage event는 취소되지 않은 active panel 전체가 해당 단계의 finalized relation(포장 membership / 출발 membership / 납품 result)을 갖는 경우에만 project row lock 아래 not-exists 검사로 exactly-once 기록한다. event에는 source batch/unit identity와 bounded correlation만 보존하고 증빙 원문을 복제하지 않는다. event는 panel별 다음 업무 생성을 대신하지 않는다(`013A-PROJECT-EVENT-SOURCE`). 정산 skeleton의 최종 방어선은 `work_items.idempotency_key` unique다.
- 워크플로 알림은 기존 인앱 work item·notification 원본 계약만 사용하고 실제 Teams/Mail/Activity provider를 실행하지 않는다.

## 7. Evidence 보안·validation 계약

- 포장·상차 사진: JPEG(FFD8FF)/PNG signature magic byte 검증, 각 최대 5MB, owner당 1~5장.
- 거래명세서 서명본: JPEG/PNG magic byte 또는 PDF(`%PDF`) sniff, 각 최대 10MB, batch당 1~3개. 확장자·클라이언트 MIME만 신뢰하지 않는다.
- 저장 필드: bounded 파일명 패턴, normalized MIME, `byte_size = octet_length(content)` check, sha256, 사진 필수 alt_text, actor·시각. bytea MVP를 유지하고 object storage 전환은 보류 항목이다.
- draft owner에서만 evidence 추가·삭제를 허용하고 finalized owner의 evidence·mapping insert/update/delete는 trigger로 차단한다(purge flag 예외).
- download는 조회 권한+scope 검증과 `Cache-Control: private, no-store`를 적용하고 scope 밖 식별자는 404로 비노출한다.
- operation receipt·result projection에 파일 원문 bytes·파일명·설명을 복제하지 않는다(`013A-EVIDENCE-BOUNDARY`).

## 8. Idempotency·version·동시성 계약

- 모든 mutation은 `operationId`와 payload fingerprint를 요구한다. evidence upload fingerprint에는 content sha256을 포함하되 raw bytes는 receipt에 저장하지 않는다.
- 같은 operationId·같은 fingerprint는 bounded 성공 projection을 replay하고, 같은 operationId·다른 fingerprint는 409로 차단한다.
- `expectedVersion`은 draft owner(unit·batch)와 finalize 대상에 적용하고 stale이면 409 한글 안내를 반환한다.
- finalize 경쟁은 owner/project/panel/current work rows의 `for update` lock으로 직렬화하고, active membership·단계별 unit unique·work item idempotency key 등 DB unique constraint를 최종 방어선으로 사용한다(`013A-REPLAY-CONCURRENCY`, `013A-MEMBERSHIP-RACE`).
- 동시 draft 구성, 동시 finalize, 재시도 replay를 Backend 동시성 테스트로 검증한다.

## 9. Pending·취소·purge 정합

- 각 finalize 직전 같은 transaction에서 대상 project(target `Project`)와 이번 요청의 모든 영향 panel(target `Panel`)의 `status <> 'Closed'` Pending을 재검사하고 존재 시 차단한다. 무관한 project의 Pending과 이미 Closed인 Pending은 차단하지 않는다.
- project/panel 취소는 draft unit/batch와 미완료 물류 업무만 기존 취소 계약대로 terminal 정리하고 finalized record·evidence·history를 보존한다. finalized unit 소속 panel이 취소돼도 기존 확정 record를 재작성하지 않으며 이후 active-panel 집계에서만 제외한다.
- approved permanent project purge는 신규 receipt/evidence/batch mapping/membership/unit을 FK 역순으로 삭제하고, immutability trigger 예외는 `emi_qms.project_purge` 설정에서만 동작함을 테스트로 확인한다(`013A-CANCEL-PURGE`).

## 10. API 계약

신규 `Logistics` 영역(contracts·store·endpoints 분리, 기존 convention 준용):

- 조회: stage별 queue(포장 대기 panel / 출발 대기 unit / 납품 대기 unit), unit·batch 상세와 단계 이력, evidence download. 모두 조회 권한+scope, scope 밖 404.
- mutation: unit 생성·panel 추가/제거·사진 추가/제거·확정·취소(draft), 출발 batch 생성·unit 추가/제거·사진 추가/제거·확정·취소(draft), 납품 batch 생성·unit 추가/제거·서명본 추가/제거·확정·취소(draft). 전부 `operationId`+`expectedVersion` 필수.
- 오류 계약: validation은 안정적 status와 field 연결 한글 메시지, 권한 실패·scope 밖은 기존 비노출 패턴, 순서·상태·stale·replay 불일치는 409. raw SQL·stack trace·내부 식별자 비노출.
- `WorkflowStore`: `LinkUrlForWorkItem`에 물류 3 stage의 `/logistics?stage=packing|departure|delivery&project=...&panel=...` deep link 분기 추가, `TransitionWorkItemAsync` 차단 목록 확장. 신규 권한 코드는 추가하지 않고 기존 `logistics.ship` seed를 재사용한다.

## 11. Frontend·UX 계약

- `App.tsx` view router에 `/logistics` route와 전역 `물류` 메뉴(TASK-010A Change 002의 `자재` 공통 진입 패턴 준용)를 추가하고, 신규 `LogisticsPage`가 포장·출발·납품 stage switch를 내장한다. deep link query(`stage`·`project`·`panel`·`unit`)를 해석해 정확한 대상을 연다.
- 정보 우선순위(`013A-MOBILE-ACTION-PRIORITY`): `오늘 할 일 수 → 차단/지연 → 선택 대상 → 증빙 → 확정` 순서로 구성한다. 모바일은 선택 stage 하나의 핵심 정보만 one-column으로 보이고 desktop의 모든 표·history를 모바일 첫 화면에 복제하지 않는다.
- 모바일 도형 의미: stage control은 compact segmented/타원 pill, 핵심 수량은 원형, 선택 unit은 둥근 사각, finalized record는 각진 사각. 44px touch target, 작은 보조 글씨, 좌상단 숨김 메뉴 유지. 하단 고정 nav와 page horizontal scroll을 추가하지 않으며 확정 action은 sticky bottom bar가 아닌 내용 흐름 안에 둔다.
- 상태 구분: 업로드 중, 빈 queue, 조회 오류, 권한 부족, Pending 차단, stale conflict, 성공+다음 행동을 서로 다른 상태로 action 근처에 표시하고 mutation 중 중복 submit을 차단한다.
- desktop: project queue·unit 구성 table·단계 현황·이력 composition. 기존 `AdaptiveLayoutProvider`/`useAdaptiveLayout`·`MobileSheet`·API client 패턴과 `QualityInspectionsPage`/`ManufacturingPage`의 queue→상세→확정 구조를 재사용한다.
- 접근성: label/role, 첫 오류 focus, `aria-live`, keyboard 접근. 390px·Teams narrow에서 page-level horizontal overflow 0.

## 12. 검증 계약

- Migration: `0036` fresh DB·existing DB 적용, catalog·ledger·compatibility, purge FK 역순, immutability trigger·purge 예외 테스트.
- Backend targeted: 포장·출발·납품 성공 경로, 권한 교집합(물류 배정자/전체 assignee/일부 assignee 차단/타 부서·scope 밖 404), membership race·unit 번호 직렬화, batch project·unit 원자성, 필수 증빙·magic byte·크기·개수 차단, 담당자 부재 rollback, project event·정산 skeleton exactly-once(동시 last-panel 경쟁 포함), generic work 차단, Pending 차단, replay·fingerprint 불일치·stale version, cancellation 정합.
- 회귀: Backend 전체 test, 품질(012A)·제조(011A)·workflow·Pending 관련 test, Frontend lint·typecheck·unit·build.
- E2E: isolated Full-Stack E2E에 품질 합격 → 포장 → 출발 → 납품 → 정산 skeleton 시나리오 추가. Persistent UAT와 분리된 전용 DB만 사용.
- Screenshot·검수: 페이지별 desktop·390px(및 Teams narrow) screenshot, page-level overflow 0. user validation checklist를 작성하되 상태는 `사용자 검수 대기`로 종료하고 사용자 검수 완료로 표시하지 않는다.
- 산출물: implementation report·SOP·user manual·Roadmap experiment 상태 갱신·user validation checklist 5종의 위치·상태 추적, privacy/secret·diff·Finding gate 통과 후 local experiment commit까지만 수행한다.

## 13. Review Finding resolution 반영표

| Finding | 반영 위치 |
| --- | --- |
| `013A-MULTI-TARGET-AUTHORIZATION` (P1) | 3장 권한 교집합, 6장 finalize 재검증 |
| `013A-MEMBERSHIP-RACE` (P1) | 4장 membership partial unique·순번 직렬화, 8장 lock 계약 |
| `013A-BATCH-PROJECT-BOUNDARY` (P1) | 4장 batch unique, 6.2·6.3 same-project·원자 확정 |
| `013A-DIRECT-WORK-BYPASS` (P1) | 6.4 generic 차단, 10장 WorkflowStore 확장 |
| `013A-HANDOFF-ROLLBACK` (P1) | 6.1~6.4 담당자 선해석·전체 rollback |
| `013A-COARSE-STAGE-SEMANTICS` (P2) | 5장 coarse 전진 규칙과 파생 상태 |
| `013A-PROJECT-EVENT-SOURCE` (P2) | 6.4 active panel finalized relation 집계·project lock |
| `013A-EVIDENCE-BOUNDARY` (P2) | 7장 evidence 계약 |
| `013A-REPLAY-CONCURRENCY` (P2) | 8장 idempotency·version·동시성 |
| `013A-CANCEL-PURGE` (P2) | 9장 Pending·취소·purge |
| `013A-MOBILE-ACTION-PRIORITY` (P3) | 11장 정보 우선순위·도형·행동 흐름 |

## 14. 자동 채택한 비차단 결정과 보류 항목

사용자 standing experiment 규칙에 따라 review 5장의 비차단 채택안을 그대로 적용한다: same-project flat unit·project별 순번·optional 메모/규격/중량 text, unit batch 전체 panel 원자 처리, finalized relation 파생 + coarse 포장/납품만 전진, bytea 증빙(사진 1~5×5MB, 서명본 1~3×10MB), project 물류 정·부 또는 대상 전체 active assignee 권한, project target 정산 skeleton 1건, stage→대상→증빙→확정 모바일 one-column.

후속 결정 대기(비차단, 구현은 위 최소안): ① Packing Unit 상세 필드·포장방식 기준정보화(Roadmap 추적 20), ② 증빙 개수·크기 운영 상향, ③ 운영 storage·retention 전환, ④ 정정·재포장·출발 취소·반품·부분 납품 정책(별도 NEW_FEATURE).

## 15. Codex 구현 순서

1. `0036` schema·constraint·partial unique·immutability trigger·purge 정합과 migration tests(fresh/existing)
2. Backend `Logistics` contracts/store/endpoints, generic work guard·deep link 분기
3. 포장 → 출발 → 납품 finalize transaction과 handoff·project event·정산 skeleton
4. evidence upload/download·replay/version/동시성·authorization tests
5. `/logistics` adaptive Frontend·전역 메뉴·API client·component test
6. Backend/Frontend 전체 회귀와 isolated Full-Stack E2E
7. desktop·390px screenshot, 5종 종료 산출물, Roadmap experiment 상태 갱신, local experiment commit

구현 중 18단계 순서, panel 전진-only, 필수 증빙, finalized append-only, Pending 차단, Repository 계약과의 의미 있는 충돌이나 secret/개인정보 위험이 발견되면 fast-track으로 우회하지 않고 blocking decision으로 중단·보고한다.

## 16. 완료 기준

- 2장 포함 범위 전부 구현, 2~9장 불변조건 위반 0, 서버 권한·scope 강제
- 11장 UX 계약 충족, page-level overflow 0, 상태 구분 완비
- 12장 자동 검증 전부 통과(미실행 항목은 이유와 함께 기록), Finding gate 통과
- 5종 산출물 추적 가능, 사용자 검수 상태 `사용자 검수 대기`
- Git: local experiment commit만. push·PR·merge·`main`(승인 0/3)·Persistent UAT·실제 provider 변경 0

openBlockingDecisionCount: 0
