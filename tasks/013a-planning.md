# TASK-013A — 물류 포장·출발·납품 완료 기획안

> 상태: Draft
> 작성 단계: Codex 내용 review 전 Fable 1차 기획
> 목적: 품질 완료 이후 15~17단계 물류 흐름(포장·출발·납품)의 구현 계약을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/013a-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: TASK-012A는 품질 최종 합격 시 panel target `PackingCompleted` 내 업무(skeleton)까지만 생성한다. 실제 포장 구성·Packing Unit·필수 사진·출발·납품 record와 전용 화면이 없어 15~17단계가 종이·사진첩·메신저로 시스템 밖에 남는다.
- 대상 사용자·역할: 물류 정·부 담당(입력·확정), 영업 정·부 담당(납품 상태·증빙 조회와 정산 업무 수신), 생산관리·품질·제조·조회 역할(조회 전용), Pending 조치 담당자(차단 이슈 조치), System Administrator(우회 없는 조회·감사).
- 정상 흐름: 품질 최종 합격 → panel `PackingCompleted` skeleton → Packing Unit 구성·포장사진 → 포장 확정 → 출발 업무 → 상차사진·출발일 → 출발 확정 → 납품 업무 → 거래명세서 서명본 → 납품 확정 → 모든 active panel 완료 시 영업 정산 skeleton exactly-once.
- 예외·복구 흐름: 품질 미완료 panel, 타 project panel 혼합, 이중 소속, 빈 구성, 필수 증빙 누락, 단계 순서 위반, open Pending, stale version, scope 불일치는 서버가 한글 오류로 차단한다. 확정 전 draft는 bounded 수정 가능, 확정 뒤 append-only. 재시도는 operation fingerprint replay로 중복을 만들지 않는다.
- 확정한 정책과 명시적 제외: Backend authoritative, project scope, panel 전진-only, 필수 증빙, finalized append-only, open Pending 차단, 모든 active panel 기반 project event. 운송사·GPS·외부 portal, 전자서명 생성·문서 생성, 재포장·반품·부분 납품 고도화, Excel export, TASK-014A 정산 상세, Persistent UAT·provider·게시는 제외.
- planning으로 넘긴 비차단 미결정 사항: Packing Unit 상세 필드, 사진·서명본 개수·크기 운영 상향, 운영 storage·retention, 정정·재포장·부분 납품 정책 (16장 참조).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

물류 담당자가 품질 완료 panel을 Packing Unit으로 묶어 필수 포장사진과 함께 확정하고, 상차사진·출발일과 거래명세서 서명본을 거쳐 panel별 납품 완료까지 순서대로 처리하면 시스템이 다음 물류 업무와 영업 정산 skeleton을 정확히 한 번 자동 생성한다.

## 2. 배경과 해결할 업무 문제

- 현재 `QualityInspectionStore`는 전진검수/FAT 합격 시 idempotency key `quality:panel:{panelId}:packing`으로 `PackingCompleted` 내 업무를 LogisticsPrimary→Secondary→fallback 순으로 생성하고 panel coarse stage를 `InspectionCompleted`까지만 올린다. 그 이후를 받는 물류 record·화면이 없다.
- `PackingCompleted` panel 업무의 link는 `WorkflowStore.LinkUrlForWorkItem`의 fallback인 프로젝트 workflow 요약으로만 연결되고, 제조·품질과 달리 generic 내 업무 완료 차단 목록(`TransitionWorkItemAsync`)에 포함되지 않아 증빙 없는 완료 우회가 가능하다.
- 어떤 panel이 어느 포장에 들어갔는지, 무엇이 상차·출발·납품됐는지의 mapping·사진·서명본·일시가 시스템에 없어 Roadmap 17장 물류 기준(포장사진 필수, 상차사진 필수, 거래명세서 서명본 필수, panel 단위·일괄 처리)과 TASK-014A 완료 조건의 근거를 만들 수 없다.
- 이 기능이 없으면 18단계 중 15~17단계가 수기 관리로 남고, 영업 정산(18단계)으로의 자동 인계가 불가능하다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 물류 정·부 담당 | 포장 queue 확인, Packing Unit 구성·사진·확정, 출발·납품 처리 | 기존 project access scope | `logistics.ship` + 해당 project의 물류 담당 또는 active 물류 work assignee인 record |
| 영업 정·부 담당 | 포장·출발·납품 상태와 증빙 조회, 정산 skeleton 업무 수신 | 기존 project access scope | 물류 mutation 불가 |
| 생산관리·품질·제조·조회 역할 | 허용 project의 물류 진행 상태 조회 | 기존 project access scope | 물류 mutation 불가 |
| Pending 조치 담당자 | 물류 차단 이슈 조치·상태 변경 | 기존 Pending scope | 기존 Pending 계약 안에서만 |
| System Administrator | 기준·이력·증빙 조회 | 전체 | 물류 업무 입력 무제한 우회 금지 (기존 관리자 원칙 유지) |

mutation·조회·사진/서명본 download 모두 서버 authorization policy와 project scope로 강제하고 UI 숨김을 보안 수단으로 사용하지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 모바일 포장 확정

1. 물류 담당자가 내 업무의 `포장 · <패널명>` deep link 또는 전역 `물류` 메뉴로 포장 stage queue에 진입한다.
2. 같은 project의 품질 완료 panel 1개 이상을 선택해 Packing Unit draft를 만들고, 포장사진 1장 이상을 촬영·업로드한다.
3. `포장 확정`을 누르면 한 transaction에서 unit이 Finalized되고, 소속 panel의 `PackingCompleted` 내 업무가 완료되며 panel coarse stage가 `PackingCompleted`로 전진하고, panel별 `출발 처리` 내 업무가 즉시 생성된다.
4. 화면은 확정 결과와 다음 단계(출발)를 action 근처에 표시한다.

### 시나리오 B — 출발 일괄 처리

1. 담당자가 출발 stage에서 포장 확정된 unit 1개 이상을 선택한다.
2. 상차사진 1장 이상과 출발일을 입력해 `출발 확정`을 누른다.
3. 한 transaction에서 출발 record가 Finalized되고 선택 unit의 모든 panel이 Departed 상태가 되며 panel별 `납품 완료` 내 업무가 생성된다. 모든 active panel이 출발하면 `DepartureProcessed` project stage event가 기록된다.

### 시나리오 C — 납품 완료와 영업 정산 인계

1. 담당자가 납품 stage에서 출발 완료 unit을 선택하고 거래명세서 서명본(JPEG/PNG/PDF) 1개 이상을 업로드해 `납품 확정`을 누른다.
2. 한 transaction에서 panel별 납품 record가 Finalized되고 panel coarse stage가 `ShipmentCompleted`로 전진한다.
3. 모든 active panel이 Delivered가 되는 확정에서만 `DeliveryCompleted` project stage event와 영업 정·부 담당 대상 `세금계산서, 완료 처리` 내 업무(정산 skeleton)가 정확히 한 번 생성된다.

### 시나리오 D — 차단과 재시도

1. panel 또는 project에 open(Closed 아님) Pending이 있으면 해당 panel이 포함된 포장·출발·납품 확정을 서버가 사유와 함께 차단한다.
2. network 재시도 시 동일 operationId·fingerprint replay가 기존 성공 결과를 반환해 record·업무·event가 중복 생성되지 않는다.
3. stale version 제출은 409 한글 안내로 차단되고 화면이 최신 상태를 다시 불러온다.

## 5. 기능 요구사항

### 필수

- [ ] 품질 완료(open `PackingCompleted` 업무 보유) panel의 포장 대기 queue 조회
- [ ] 같은 project panel 1개 이상을 묶는 flat Packing Unit 생성·draft 수정·취소, project별 unit 순번 부여
- [ ] panel은 취소되지 않은 unit 한 곳에만 소속 (DB unique 제약 + transaction 검증)
- [ ] 포장사진 필수(unit당 1장 이상) 업로드·삭제(draft 한정)와 포장 확정
- [ ] 포장 확정 시 skeleton 업무 완료, panel stage 전진, panel별 출발 업무 생성 — 단일 transaction
- [ ] 출발: 확정 unit 1개 이상 선택, 상차사진 필수·출발일 필수, 출발 확정과 panel별 납품 업무 생성
- [ ] 납품: 출발 완료 unit 선택, 거래명세서 서명본(JPEG/PNG/PDF) 필수, panel별 납품 record 확정
- [ ] 모든 active panel 기준 `PackingCompleted`/`DepartureProcessed`/`DeliveryCompleted` project stage event와 영업 정산 skeleton exactly-once
- [ ] `logistics.ship` + project scope + 물류 담당/active assignee 서버 권한, 조회·download 포함
- [ ] `PackingCompleted`/`DepartureProcessed`/`DeliveryCompleted` panel 업무의 generic 내 업무 완료 차단과 전용 화면 안내
- [ ] open Pending(대상 panel 또는 project, Closed 아님) 시 확정 차단
- [ ] 선행 단계·순서 위반·빈 구성·타 project 혼합·stale version·중복 확정 차단
- [ ] operation fingerprint/replay 테이블과 확정 증빙 append-only DB trigger
- [ ] 내 업무 deep link의 물류 전용 화면 연결 (`LinkUrlForWorkItem` 확장)
- [ ] 모바일 우선 adaptive 물류 화면과 desktop 관리/조회 composition, 단계별 이력 조회

### 선택

- [ ] Packing Unit 비고·규격·중량 선택 입력 (최소 text 필드, 관리자 기준정보화는 후속)
- [ ] 출발·납품 화면에서 unit 내 panel 목록 펼침 조회

### 명시적 제외

- [ ] 운송사·기사 계정, 외부 고객 portal, GPS·차량·송장·택배 API
- [ ] 전자서명 생성, 거래명세서 문서/PDF 생성, OCR·바코드·QR scan
- [ ] 포장방식 기준정보 관리자, 자동 규격·중량 계산, pallet/container 최적화
- [ ] 포장 해제·재포장·출발 취소·납품 정정·반품·분할 납품 고도화
- [ ] Excel export, 신규 외부 알림 채널·실제 provider delivery
- [ ] 영업 세금계산서·프로젝트 완료 상세(TASK-014A)
- [ ] Persistent UAT migration·write·runtime handover, 대표 repo·`main`·push·PR·merge

## 6. 화면·UX 기획

전역 메뉴에 `물류` 한 개를 추가하고(TASK-010A Change 002의 `자재` 공통 진입 패턴 준용), 내부에서 포장·출발·납품 stage switch로 이동한다. route는 `/logistics`와 `stage`·`project`·`panel`·`unit` query 조합의 deep link를 사용한다.

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 물류 stage queue | 전역 `물류`, 내 업무 deep link | 단계 switch, project·panel·unit 카드, 상태 도형, 차단 사유 | stage 전환, 대상 선택 | loading/empty/error 구분, 차단 사유를 카드에 표시 |
| 포장 구성(draft) | queue에서 panel 선택 | 선택 panel, unit 순번, 사진 목록 | panel 추가/제거, 사진 촬영·업로드·삭제, 확정 | 업로드 중·확정 완료·다음 단계 안내, 중복 submit 차단 |
| 출발 처리 | stage switch `출발` | 확정 unit 목록, panel 수, 상차사진, 출발일 | unit 다중 선택, 사진·날짜 입력, 확정 | 확정 결과와 생성된 다음 업무 안내 |
| 납품 완료 | stage switch `납품` | 출발 완료 unit, 서명본 목록 | unit 선택, 서명본 업로드, 확정 | panel별 완료 결과, 모든 panel 완료 시 정산 인계 안내 |
| 단계 이력 | 각 화면 내 이력 탭/펼침 | 확정 record, actor·시각, 증빙 썸네일/링크 | 조회, 증빙 열람 | 권한 없는 대상 비노출 |

모바일 원칙(interview 확정): PC 축소가 아닌 현장 행동 재구성 — `오늘 할 일/단계 → panel 선택 → 증빙 촬영 → 확인` one-column 흐름, 44px touch target, 작은 보조 글씨와 원형(단계)·타원(상태 badge)·둥근 사각(선택 카드)·각진 사각(확정 record) 도형 의미 구분, 좌상단 숨김 메뉴, page-level horizontal overflow 0. 기존 `AdaptiveLayoutProvider`/`useAdaptiveLayout`과 `MobileSheet` 패턴을 재사용한다. desktop은 project queue·unit 구성 table·단계 현황·이력을 함께 배치한다.

확인할 UX 항목: 현재 단계·차단 사유 이해 가능, 다음 행동 명확, 저장 결과가 action 근처, 권한 부족·조회 전용·오류 구분, 390px·Teams narrow에서 핵심 행동 가능.

## 7. 업무 규칙과 불변조건

- 18단계 순서 보존: `품질 완료(13/14) → 포장(15) → 출발(16) → 납품(17) → 영업 정산(18)`. panel별 물류 상태는 `PackingRequested → Packed → DepartureRequested → Departed → DeliveryRequested → Delivered` 전진-only이며 실패·Pending으로 후퇴하지 않는다.
- Packing Unit은 같은 project의 품질 완료 panel만 포함하고, panel은 취소되지 않은 unit 정확히 한 곳에만 속한다. 빈 unit은 확정할 수 없다.
- 필수 증빙 없는 확정 금지: 포장사진(unit당 1+), 상차사진(출발 record당 1+)·출발일, 거래명세서 서명본(납품 record당 1+).
- 확정 record·panel mapping·증빙은 append-only로 보존하고 hard delete·덮어쓰기하지 않는다. draft 상태에서만 bounded 수정을 허용한다. approved permanent project purge(`emi_qms.project_purge`) 경로만 기존 정책대로 예외다.
- 대상 panel(target `Panel`) 또는 project(target `Project`)의 Closed가 아닌 Pending이 있으면 그 panel이 포함된 물류 확정을 차단한다.
- panel별 다음 내 업무는 확정 즉시 생성하고, project stage event와 영업 정산 skeleton은 모든 active panel 완료 시 exactly-once로만 생성한다. 동일 event 재실행이 업무·알림을 중복 생성하지 않는다.
- generic 내 업무 시작/완료로 물류 단계를 우회할 수 없다.
- 프로젝트/panel 취소 시 진행 중 draft·미완료 물류 업무만 terminal 정리하고 확정 증빙은 보존한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| Packing Unit | project별 순번을 갖는 flat 포장 단위 (`Draft/Finalized/Cancelled`, version) | 신규 | Finalized 후 구성·핵심 필드 immutable |
| Unit–panel mapping | unit 소속 panel, active 중복 방지 unique | 신규 | Finalized unit의 mapping immutable |
| 포장 사진 | unit별 JPEG/PNG bytea, sha256·byte_size·alt_text | 신규 (0035 photo 계약 준용) | Finalized 후 immutable trigger |
| 출발 record | 선택 unit 묶음, 출발일, 상차사진, `Draft/Finalized` | 신규 | Finalized 후 immutable |
| 납품 record | 선택 unit의 panel별 납품 결과, 서명본 evidence(JPEG/PNG/PDF) | 신규 | Finalized 후 immutable |
| 물류 operation | operationId·action·payload fingerprint·result projection replay | 신규 (`panel_quality_operations` 준용) | append-only |
| panel coarse stage | `panel_placeholders.workflow_stage` 전진 (`PackingCompleted`, `ShipmentCompleted`) | 기존 | 기존 case-순서 전진-only update 준용 |
| 내 업무/알림/project event | `work_items` unique idempotency_key, `project_workflow_events`, 인앱 notification 원본 | 기존 | 기존 계약 유지 |

```text
PackingRequested → (unit 확정) Packed → (출발 대상 선택) DepartureRequested → (출발 확정) Departed
→ (납품 대상 선택) DeliveryRequested → (납품 확정) Delivered → [모든 active panel Delivered] 영업 정산 skeleton
```

panel별 세부 물류 상태는 별도 mutable 컬럼 없이 확정 record·mapping에서 파생하는 것을 권장한다(이중 상태 drift 방지). 테이블·컬럼 최종 명명은 Codex 구현 조사에서 기존 convention에 맞춰 조정할 수 있다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 7장 불변조건 전부. UI 상태를 신뢰하지 않는다.
- 필요한 조회와 mutation(신규 `Logistics` 영역, 기존 endpoint/composition·store 분리 convention 준용):
  - 조회: stage별 queue(포장 대기 panel, 출발 대기 unit, 납품 대기 unit), unit/record 상세·이력, 사진·서명본 download
  - mutation: unit 생성/panel 추가·제거/사진 추가·제거/포장 확정/unit 취소(draft), 출발 record 생성·사진·확정, 납품 record 생성·서명본·확정 — 모든 mutation에 `operationId`, `expectedVersion` 포함
- 권한·validation: `logistics.ship` policy + `ProjectAccessScope` + LogisticsPrimary/Secondary 또는 active work assignee. 실패는 안정적 status와 한글 메시지(404로 scope 밖 식별자 비노출 패턴 유지).
- transaction·동시성·idempotency: 확정 action은 record·mapping·panel stage·업무 완료/생성·event를 한 transaction에서 처리한다. `for update` row lock, partial unique index(활성 unit 소속 1곳, 단계별 active record 1개), `work_items.idempotency_key`(`logistics:panel:{panelId}:departure`, `logistics:panel:{panelId}:delivery`, `logistics:project:{projectId}:sales-settlement`)와 operation fingerprint replay로 중복을 막는다. 동시 확정 경쟁 테스트를 포함한다.
- audit trail: actor·시각·operation projection·append-only 증빙과 `project_workflow_events` 기록. 정산 skeleton exactly-once의 최종 방어선은 work item unique key다.
- 외부 provider 영향: 없음. 기존 인앱 work item·notification 원본만 생성하고 실제 Teams/Mail/Activity 발송은 실행하지 않는다.
- 파일 계약: JPEG/PNG magic byte 검증은 기존 패턴을 재사용하고 PDF(`%PDF`) sniff를 서명본에만 추가한다. 최소안 — 사진 각 5MB·단계당 1~5장, 서명본 각 10MB·1~3개.

## 10. Frontend 고려사항

- route/component: `App.tsx` view router에 `logistics` view와 `/logistics` 경로 추가, 신규 `LogisticsPage`(stage switch 내장). 기존 `QualityInspectionsPage`·`ManufacturingPage`의 queue→상세→확정 구조와 API client 패턴 재사용.
- loading/empty/error/success: stage별 queue와 각 mutation에서 구분 표시, 업로드 진행 상태, 확정 후 다음 단계 안내.
- 공통 Action Feedback: mutation 중 중복 submit 차단, 실패 사유를 action 근처에 한글로 표시, stale(409) 시 재조회 유도.
- 접근성: label/role, 첫 오류 focus, `aria-live` 안내, keyboard 접근.
- 390px/mobile/narrow pane: 6장 모바일 원칙 적용, page-level horizontal overflow 0, Teams narrow 검증.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: `PackingCompleted` skeleton 업무를 물류 확정으로 완료 처리, 다음 업무·참조 알림은 기존 fallback 규칙(Primary→Secondary→Sales→Administrator) 재사용, `LinkUrlForWorkItem`에 물류 stage 분기 추가, `TransitionWorkItemAsync` 차단 목록에 물류 3 stage 추가.
- 권한/관리자: 기존 `logistics.ship` seed·역할 재사용, 신규 권한 코드 추가 없음. 관리자 이력은 기존 업무 시작/완료 이력 화면 계약을 따른다.
- Excel/PDF/첨부: 사진·서명본은 0035 bytea evidence 계약 준용. Excel·PDF 생성 없음.
- Teams/Mail: 인앱 원본만. provider coverage 확대는 범위 밖.
- 삭제·복구/감사: project soft delete·purge 기존 계약에 신규 테이블 FK 역순 정합만 보강. 확정 증빙은 취소 흐름에서도 보존.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | flat Packing Unit + 혼합 일괄(포장 unit 중심, 출발 unit 선택, 납품 panel별 record) + 파생 panel 상태 | Roadmap 3.2 데이터 단위와 일치, panel 추적과 포장 관계 모두 보존, 상태 drift 없음 | 신규 테이블 수가 상대적으로 많음 |
| B | panel별 단독 record만 (unit 없음) | 구조 단순 | 일괄 포장·포장번호 mapping 표현 불가, Roadmap 17장 위반 |
| C | unit 계층(box→pallet→container)과 단계별 unit 상태 컬럼 | 표현력 최대 | 미확정 요구에 과잉, 상태 이중화 drift 위험, MVP 범위 초과 |

권장안 A는 interview 7장 선택 1·2·6의 자동 채택 권장안과 일치하며, Repository의 0035 검사 계약(attempt·operation·immutability)을 최대 재사용한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL·disposable E2E DB·synthetic data만 사용한다.
- migration 필요 여부: `database/migrations/0036`(additive) 1건. 기존 migration 수정·번호 재사용 금지, 기존 panel에 물류 record backfill 금지, fresh/existing DB 모두 검증.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 비활성 유지.
- runtime 교체 여부: 없음. 현재 experiment 계보 local 검증만.
- 추가 사용자 승인 필요 작업: push·PR·merge·main 반영(승인 0/3), Persistent UAT 적용, 운영 storage 정책 전환.

## 14. 검증 계획

- 최소 테스트: migration fresh/existing, 포장·출발·납품 성공 경로, 필수 증빙·순서·혼합·이중 소속·빈 구성·Pending·stale 차단, replay 중복 방지, 동시 확정 경쟁, 권한·scope(물류 외 역할 mutation 차단, scope 밖 404), skeleton 완료·다음 업무·정산 exactly-once, generic 완료 차단.
- 영향 영역 회귀: Backend 전체 test, 품질(012A)·제조(011A)·workflow·Pending 관련 test, Frontend lint·typecheck·unit·build.
- PR/CI: 이번 fast-track은 local experiment commit까지만. isolated Full-Stack E2E에 품질 합격→포장→출발→납품→정산 skeleton 시나리오 추가.
- 사용자 검수: 페이지별 desktop·390px(및 Teams narrow) screenshot과 user validation checklist를 작성하되 사용자 검수 완료로 표시하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 5장 필수 요구사항 전부 구현, 7장 불변조건 위반 0, 서버 권한·scope 강제.
- UX: 6장 화면과 모바일 원칙 충족, page-level overflow 0, loading/empty/error/success 구분.
- 자동 테스트: 14장 계획 전부 통과, 미실행 항목은 이유와 함께 기록.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기`로 종료.
- PR 상태: 없음(local experiment commit only).

## 16. 미결정 사항

모두 비차단이며 구현은 아래 최소안으로 진행한다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | Packing Unit 상세 필드(포장방식·규격·중량)의 정식 구조와 관리자 기준정보화 | 최소 선택 text 유지 / 기준정보 연동 | 후속 결정 대기 (Roadmap 추적 20) |
| 2 | 단계별 사진·서명본 개수·크기 운영 상향 | 최소안(1~5장·5MB, 서명본 1~3개·10MB) 유지 / 운영 회신 반영 | 후속 결정 대기 |
| 3 | 운영 증빙 storage·retention (bytea 유지 vs 외부 storage 전환) | bytea 유지 / 별도 storage Task | 후속 결정 대기 |
| 4 | 정정·재포장·출발 취소·반품·부분 납품 정책 | 후속 NEW_FEATURE로 분리 | 후속 결정 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Logistics` 신규 영역(contracts·store·endpoints), `WorkflowStore`(link·generic 완료 차단), `Program.cs` 등록, purge 정합 지점
- Frontend: `LogisticsPage` 신규, `App.tsx` route/menu, API client, adaptive/공통 component 재사용
- DB/Migration: `database/migrations/0036` additive 1건
- Tests/Scripts: Backend 물류 targeted·migration·authorization·concurrency tests, Frontend unit, Full-Stack E2E 시나리오
- Docs: Roadmap 실험 상태 갱신, Task 산출물 5종

## 18. Roadmap 연결

- 선행 Task: TASK-012A(품질 완료·`PackingCompleted` skeleton) — 현재 experiment 계보에서 구현 완료.
- 후속 Task: TASK-014A(영업 정산·프로젝트 완료), TASK-EXPORT-001(Excel), 포장방식 기준정보 관리자.
- 현재 Go/No-Go: canonical queue의 TASK-013A는 `Dependency Pending`이나, 2026-07-17 실험 재정렬 승인으로 현재 experiment 계보 진행이 명시 승인됨. canonical 다음 `TASK-007A` Gate는 변경하지 않는다.
- 별도 Task로 분리할 항목: 16장 1~4, 물류 Excel export, 외부 운송 연동.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | 실험 fast-track standing rule로 interview 왕복·중간 승인 생략, 비차단 선택 Fable 권장안 자동 채택 | interview `COMPLETED_CONFIRMED`, 본 1차 기획 작성 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

1. `0036` additive migration: Packing Unit·mapping·사진, 출발·납품 record·evidence, 물류 operation 테이블과 immutability trigger·partial unique index를 0035 계약 준용으로 작성하고 fresh/existing DB를 검증한다.
2. Backend `Logistics` store/endpoints: 9장 조회·mutation을 단일 transaction·row lock·fingerprint replay·`logistics.ship`+scope+담당자 검증으로 구현한다.
3. Workflow 통합: skeleton 업무 완료, panel stage 전진, 다음 업무 3종 idempotency key, 모든 active panel 기준 stage event·정산 skeleton exactly-once, `LinkUrlForWorkItem` 물류 분기, `TransitionWorkItemAsync` 차단 목록 확장.
4. Frontend: `/logistics` adaptive 화면(포장·출발·납품 stage switch, 모바일 현장 흐름, desktop composition)과 deep link, 전역 메뉴 추가.
5. 검증: 14장 자동 테스트 전부, isolated Full-Stack E2E 물류 시나리오, 페이지별 desktop·390px screenshot, implementation report·5종 산출물, local experiment commit까지만 수행한다.
6. 안전: 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge를 건드리지 않고, 18단계 순서·전진-only·필수 증빙·append-only·Pending 차단 위반이 의심되면 blocking decision으로 중단·보고한다.

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 4
