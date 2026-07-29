Baseline is complete. I've verified: the interview gate is confirmed (`COMPLETED_CONFIRMED`, `userConfirmed: true`, 0 blocking decisions), the Roadmap records the 2026-07-18 experiment reorder approval for TASK-014A, TASK-013A creates the `SalesSettlementCompleted` work-item skeleton and `logistics_delivery_results` relation but no settlement record/screen exists, no endpoint ever sets project status `Completed`, latest migration is `0036`, and the pending list (`Closed` terminal), permission (`logistics.ship` pattern), notification, and operation-receipt patterns are all established. Below is the single Fable first planning draft.

---

# TASK-014A — 영업 정산·세금계산서·프로젝트 완료 기획안

> 상태: Draft
> 작성 단계: Codex 내용 review 전 Fable 1차 기획
> 목적: 납품 완료 이후 18단계 마지막(영업 정산·세금계산서 발행·프로젝트 최종 완료)의 구현 계약을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/014a-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: TASK-013A는 모든 active panel 납품 확정 시 idempotency key `logistics:project:{projectId}:sales-settlement`로 영업 담당 `SalesSettlementCompleted` 내 업무 한 건과 `DeliveryCompleted` project stage event까지만 생성한다. 정산 record, 세금계산서 입력 화면, 완료 조건의 원자 검증, 프로젝트 `Completed` 전이가 없어 18단계 마지막이 시스템 밖에 남는다.
- 대상 사용자·역할: 영업 정·부 담당(정산 조회·입력·완료), 생산관리·품질·제조·물류·조회 역할(상태 조회와 완료 알림 수신), Pending 조치 담당자(완료를 막는 open Pending 조치), System Administrator(우회 없는 조회·감사).
- 정상 흐름: 모든 active panel 납품 → 정산 skeleton → 영업 담당자 정산 화면 진입 → 세금계산서 발행 정보 draft 저장 → 완료 조건(전 panel 납품·open Pending 0·발행 완료) 확인 → 최종 완료 → settlement·work item·project·workflow event·인앱 알림을 한 transaction에서 완료.
- 예외·복구 흐름: 미납품 active panel, open Pending, 세금계산서 미발행, 취소·삭제·보류·완료된 project, stale version, scope·권한 불일치는 서버가 안정적인 한글 오류로 차단한다. 동일 operation 재시도는 replay, 경쟁은 한 건만 성공한다. 완료 뒤 직접 되돌리기는 제공하지 않는다.
- 확정한 정책과 명시적 제외: 18단계 순서, 모든 active panel 납품, open Pending 0건, 세금계산서 발행 완료, Backend authoritative, project scope, forward-only·idempotent 완료, 완료 이력 보존. 국세청·ERP·회계·전자세금계산서 연동, 파일 업로드·OCR·PDF/Excel, 수금·채권·복수 청구, 재오픈·정정, 실제 provider, Persistent UAT, 대표 repo·`main`·게시는 제외.
- planning으로 넘긴 비차단 미결정 사항: 세금계산서 최소 필드, 정산 mutation 권한, queue/detail 배치, 완료 알림 recipient, 완료 뒤 정정 정책 — 사용자 standing rule에 따라 아래 Fable 권장안으로 확정해 구현하고 잔여 확장은 16장에 남긴다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

영업 담당자가 납품 완료된 프로젝트의 세금계산서 발행 정보를 기록하고, 서버가 전 panel 납품·open Pending 0건·발행 완료를 한 transaction에서 재검증해 정산·내 업무·프로젝트를 정확히 한 번 최종 완료하며 관련 담당자에게 인앱 완료 알림을 남긴다.

## 2. 배경과 해결할 업무 문제

- `LogisticsStore`의 납품 확정은 `WillCompleteDeliveryAsync`로 모든 active panel의 `logistics_delivery_results` 보유를 판정해 영업 정산 skeleton을 exactly-once 생성하지만, 그 이후를 받는 정산 domain이 없다.
- `SalesSettlementCompleted` 업무는 `WorkflowStore.LinkUrlForWorkItem`의 fallback(`/projects/{projectId}?section=workflow`)으로만 연결되고, generic 내 업무 완료 차단 목록(`TransitionWorkItemAsync`)에 포함되지 않아 세금계산서·Pending·납품 조건 없이 업무만 완료하는 우회가 가능하다.
- `projects.status = 'Completed'`는 목록·진행률·병목·삭제 차단 등 조회 로직 전반에서 참조되지만 이를 설정하는 endpoint가 없다. hold/resume/cancel만 존재해 프로젝트가 실제 완료 조건과 무관하게 영구히 Active로 남는다.
- 세금계산서 발행 여부·발행일·완료 actor/시각의 감사 근거가 시스템에 없어 Roadmap 18장 완료 기준(모든 패널 납품, 발행 완료 체크, open Pending 0건)을 증빙할 수 없다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 영업 정·부 담당 | 정산 대기 queue 확인, 세금계산서 draft 저장, 최종 완료 | 기존 project access scope | 신규 `sales.settle` + 해당 project의 SalesPrimary/SalesSecondary 또는 open 정산 업무의 current assignee |
| 생산관리·품질·제조·물류·조회 역할 | 허용 project의 정산·완료 상태 조회, 완료 알림 수신 | 기존 project access scope | 정산 mutation 불가 |
| Pending 조치 담당자 | 완료를 막는 open Pending 조치 | 기존 Pending scope | 기존 Pending 계약 안에서만. 정산 화면은 Pending을 임의 종결하지 않음 |
| System Administrator | 기준·감사 조회 | 전체 | 담당 아닌 프로젝트의 정산 mutation은 store 담당 검증에서 차단 (기존 관리자 원칙 유지) |

권장 권한(interview 선택 3): `logistics.ship`·`manufacturing.update`와 같은 domain 전용 permission 패턴을 따라 신규 `sales.settle`을 additive migration으로 추가하고 `sales`·`system-administrator` role에 부여한다. `projects.manage`/`Project.Update` 재사용은 프로젝트 기본정보 권한과 최종 완료 권한을 구분할 수 없어 배제한다. mutation은 endpoint policy(`sales.settle`) + project scope + store transaction 내부의 영업 담당/current assignee 교집합을 모두 통과해야 한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 세금계산서 기록과 최종 완료 (모바일)

1. 영업 담당자가 내 업무의 `세금계산서, 완료 처리` deep link 또는 전역 `영업 정산` 메뉴로 정산 화면에 진입한다.
2. 화면 상단에서 완료 조건(납품 완료 panel 수/전체, open Pending 수, 세금계산서 상태)을 확인한다.
3. 세금계산서 발행일(필수)과 번호·메모(선택)를 입력해 draft 저장하거나 바로 최종 완료를 진행한다.
4. `프로젝트 최종 완료`를 누르면 변경 불가 경고 확인 뒤 한 transaction에서 정산 Completed, 정산 업무 완료, `SalesSettlementCompleted` stage event, project `Completed`, 관련 담당자 인앱 알림이 함께 확정된다.
5. 화면은 완료 결과와 “완료 후 수정 불가” 상태를 action 근처에 표시한다.

### 시나리오 B — 조건 미충족 차단과 조치 이동

1. open Pending이 남은 프로젝트에서 최종 완료를 시도하면 서버가 사유와 함께 409로 차단한다.
2. 화면의 Pending 이동 링크(`/pending/{id}` 또는 project filter)로 이동해 담당자가 조치·종결한다.
3. 정산 화면으로 돌아와 최신 조건을 다시 불러오고 완료를 재시도한다.

### 시나리오 C — 동시성·재시도

1. 두 영업 담당자가 동시에 완료를 시도하면 row lock·version으로 한 건만 성공하고 나머지는 최신 상태 재조회를 안내받는다.
2. network 재시도의 동일 operationId·fingerprint는 기존 성공 응답을 replay하고 record·event·알림·업무 완료가 중복 생성되지 않는다.
3. generic 내 업무 완료로 `SalesSettlementCompleted`를 종료하려는 시도는 전용 화면 안내와 함께 409로 차단된다.

## 5. 기능 요구사항

### 필수

- [ ] open `SalesSettlementCompleted` 업무 기반 정산 대기 queue 조회 (project scope 적용, 완료 조건 요약 포함)
- [ ] project별 정산 detail 조회: 납품 완료 panel 집계, open Pending 수와 이동 링크, 세금계산서 상태, version, 완료 가능 여부
- [ ] 정산 record draft 저장: 발행일·번호·메모 bounded 입력, upsert, version 검증
- [ ] 최종 완료: 모든 active panel의 `logistics_delivery_results` 보유, project 대상 및 active panel 대상 Pending 중 `Closed` 아님 0건, 세금계산서 발행일 존재, project 상태 Active·미삭제를 서버가 한 transaction에서 재검증
- [ ] 완료 transaction의 원자 구성: settlement `Completed`(actor·시각), 현재 정산 업무 `Completed`, `project_workflow_events`의 `SalesSettlementCompleted` StageCompleted exactly-once, `projects.status='Completed'`(+완료 actor·시각), 관련 담당자 인앱 알림, audit event
- [ ] operation receipt(fingerprint/replay)와 settlement version·row lock 기반 동시성·중복 방지
- [ ] 완료된 settlement·완료 이력 immutability (0036 trigger 패턴 준용)
- [ ] `sales.settle` + project scope + 영업 담당/current assignee 서버 권한
- [ ] `SalesSettlementCompleted`의 generic 내 업무 완료 차단(`TransitionWorkItemAsync` 확장)과 hold/resume/cancel·삭제 경로의 Completed 보호 유지 확인
- [ ] 내 업무 deep link의 정산 전용 화면 연결 (`LinkUrlForWorkItem` 확장)
- [ ] 프로젝트 취소·삭제·승인된 purge와 draft 정산 데이터의 정합 (draft terminal 처리, purge FK 역순 포함)
- [ ] 모바일 우선 adaptive 정산 화면(완료 조건 → 세금계산서 입력 → 최종 확인 한 열)과 desktop queue/detail composition

### 선택

- [ ] queue 카드의 납기일·고객사 등 기존 project 요약 표시
- [ ] 완료된 프로젝트의 정산 read-only 조회 뷰

### 명시적 제외

- [ ] 국세청·ERP·회계·전자세금계산서·결제·수금 외부 연동
- [ ] 세금계산서 파일 업로드·OCR·전자서명·PDF/Excel 생성
- [ ] 매출원가·마진·수금·채권·분할 청구·복수 세금계산서·통화 계산
- [ ] 완료 뒤 재오픈·세금계산서 취소·수정발행·프로젝트 재활성화
- [ ] 신규 외부 알림 채널·실제 provider delivery (프로젝트 완료의 Teams/Mail event 연결은 Roadmap 6.5.2.2 후속 유지)
- [ ] Persistent UAT migration·write·runtime handover, 대표 repo·`main`·push·PR·merge

## 6. 화면·UX 기획

전역 메뉴에 `영업 정산` 진입(013A `물류` 패턴 준용)을 추가하고 route는 `/sales/settlement`와 `project` query deep link를 사용한다. 내 업무 deep link는 `/sales/settlement?project={projectId}`로 확장한다(권장안, interview 선택 4: user-flow의 영업 프로젝트 맥락과 내 업무 deep link를 보존하는 최소 queue/detail 구성).

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 정산 대기 queue | 전역 `영업 정산`, 내 업무 deep link | 대기 프로젝트 카드(납품 완료·Pending·발행 상태 도형) | 프로젝트 선택 | loading/empty/error 구분 |
| 정산 detail — 완료 조건 | queue에서 선택 | 납품 panel 수/전체, open Pending 수·이동 링크, 발행 상태, 다음 행동 | Pending 이동, 새로고침 | 조건별 충족/미충족 도형과 사유 |
| 세금계산서 입력 | detail 내 섹션 | 발행일, 번호, 메모, draft 저장 상태 | 입력·draft 저장 | 저장 성공/오류를 action 근처 표시, 중복 submit 차단 |
| 최종 완료 확인 | detail 하단 | 조건 요약, 변경 불가 경고 | 확인 후 완료 | 완료 success, stale/Pending/미납품 409 한글 안내, error focus·`aria-live` |

확인할 UX 항목: 390px·Teams narrow에서 PC 표 축소가 아닌 `완료 조건 → 입력 → 최종 확인` 한 열 흐름, 44px 핵심 touch target, 작은 보조 글씨와 원형·타원·각진/둥근 사각형의 의미별 사용, 좌상단 숨김 메뉴 유지, page-level horizontal overflow 0, 완료 뒤 read-only 상태의 명확한 표시.

## 7. 업무 규칙과 불변조건

- 최종 완료 조건: 모든 active panel의 납품 finalized relation 보유 AND project 대상·active panel 대상 Pending 중 `Closed` 아닌 것 0건 AND 세금계산서 발행일 기록. 셋 중 하나라도 미충족이면 서버가 완료를 차단한다.
- 프로젝트 완료는 forward-only·exactly-once다. 완료된 settlement·stage event·완료 이력은 덮어쓰기·hard delete하지 않으며, 재오픈·정정은 이번 Task에서 제공하지 않는다.
- OnHold·Cancelled·Completed·삭제된 프로젝트에서는 정산 draft 저장과 완료를 모두 차단한다. 기존 상태 전이 계약(hold/resume/cancel의 source status 집합, Completed 삭제 차단)은 변경하지 않는다.
- generic 내 업무 완료·일반 프로젝트 상태 변경으로 정산 조건을 우회할 수 없다.
- Backend가 권한·조건·동시성의 authoritative layer다. UI 숨김을 보안 수단으로 사용하지 않고 System Administrator도 담당 검증을 우회하지 않는다.
- 알림·업무 완료·event는 완료 transaction과 함께 모두 성공하거나 모두 rollback하며 idempotency key로 중복을 방지한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| project 정산 record | project당 1건, 세금계산서 발행일·번호·메모, version, 완료 actor·시각 | 신규 | 완료 후 immutability trigger, hard delete는 승인된 purge만 |
| 세금계산서 projection | 발행일(필수)·번호(선택 bounded)·메모(선택 bounded) text/date 최소안 | 신규 (권장안, interview 선택 2) | 발행 여부는 발행일 존재로 판정 |
| 정산 operation receipt | operationId·fingerprint·응답 replay | 신규 (0036 패턴 준용) | append-only |
| project 완료 이력 | `projects.status='Completed'` + 완료 actor·시각 컬럼, `project_workflow_events` StageCompleted, audit event | 기존 확장 | 덮어쓰기 금지 |
| 납품 완료 판정 | `logistics_delivery_results` 기반 active panel 집계 | 기존 재사용 | 읽기 전용 |
| open Pending 판정 | `Closed` 아닌 Pending 집계 | 기존 재사용 | 읽기 전용 |
| 완료 알림 | 기존 `notifications` 인앱 원본, recipient별 idempotency | 기존 재사용 | 실제 provider 미발송 |

```text
(정산 skeleton 업무 생성됨)
  → 정산 record 없음/Draft (발행 정보 bounded 저장·수정 가능)
  → Completed (조건 재검증 + 원자 완료, 이후 불변)
  → [프로젝트 취소·삭제 시에만] Draft → Cancelled (terminal, finalized 이력 없음)
```

권장안(interview 선택 1): 회계형 다단계 상태 대신 위 최소 forward-only 상태를 사용한다. 권장안(interview 선택 5): 완료는 append-only로 닫고 정정·재오픈은 별도 정책 Task로 보류한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 7장 전체. 조건 집계·권한·version·replay를 모두 store transaction 내부에서 재검증한다.
- 필요한 조회와 mutation: 정산 queue 조회, project 정산 detail 조회, draft 저장(upsert), 최종 완료. 완료 요청은 최종 invoice projection·expectedVersion·operationId를 함께 받아 저장·검증·완료를 한 transaction으로 처리한다.
- 권한·validation: 신규 policy(`sales.settle` 매핑, `QmsPolicies`/`AuthorizationServiceCollectionExtensions` 패턴) + project scope + store 내부 영업 담당/current assignee 교집합. 발행일 형식·미래 한도, 번호·메모 길이 bounded 검증과 안정적인 한글 오류.
- transaction·동시성·idempotency: project·settlement row lock(013A finalize의 project→owner lock 순서 준용), settlement version, operation fingerprint replay, work item·event·notification idempotency key. 경쟁은 1 success / 1 conflict(409)로 수렴한다.
- audit trail: 기존 `InsertAuditEventAsync` 패턴으로 `ProjectCompleted` audit event, settlement record의 actor·시각, stage event note를 남긴다.
- 외부 provider 영향: 없음. 인앱 notification 원본만 생성하며 Teams/Mail/Activity 실제 발송 경로는 실행하지 않는다.

## 10. Frontend 고려사항

- route/component: `App.tsx` pathname routing에 `/sales/settlement` 추가, 신규 `SalesSettlementPage.tsx`와 `frontend/src/sales.ts` type, `api.ts` client 확장. 기존 `LogisticsPage` adaptive 패턴을 준용한다.
- loading/empty/error/success: queue·detail 각각 구분, draft 저장·최종 완료의 성공·오류를 action 근처에 표시, 완료 후 read-only 상태 전환.
- 공통 Action Feedback: 중복 submit 차단, error focus, `aria-live`, 409 한글 안내 후 최신 상태 재조회.
- 접근성: 44px touch target, 도형·색상 병행 표기, 키보드 이동.
- 390px/mobile/narrow pane: 한 열 `완료 조건 → 입력 → 최종 확인` 흐름, 좌상단 숨김 메뉴 유지, page-level overflow 0, Teams narrow 검증.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 정산 업무 완료·deep link·`LinkUrlForWorkItem`·generic 차단은 `WorkflowStore`, 완료 알림은 기존 `notifications` 인앱 원본 패턴(제조·Pending store의 transaction 내 insert 준용). 완료 알림 recipient 권장안(interview 선택 6): 해당 project `project_assignees`의 distinct 사용자 중 actor 제외 전원에게 참조 알림 1건씩, recipient별 idempotency로 중복 0.
- 권한/관리자: 신규 permission은 관리자 read-only 권한 매트릭스에 자동 표시된다. role/permission 편집 UI는 기존대로 후속.
- Excel/PDF/첨부: 없음. 영업 정산 Excel export는 TASK-EXPORT-001 후속.
- Teams/Mail: Roadmap 6.5.2의 `프로젝트 완료` 행은 인앱 즉시 + 메일(증빙) 발송이 목표이나 event 연결은 `미확인` 상태다. 이번 Task는 인앱 원본까지만 생성하고 외부 채널 event 연결은 6.5.2.2 후속으로 유지한다.
- 삭제·복구/감사: Completed 프로젝트 삭제 차단은 기존 유지. 프로젝트 취소 시 draft 정산 terminal 처리(`ChangeStatusAsync`의 기존 CancelProject* hook 패턴), 승인된 purge의 FK 역순에 정산 테이블 추가.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 신규 `Sales` settlement domain(store/endpoints) + project당 1건 정산 record + 완료를 정산 domain의 단일 transaction으로 구현 | 013A와 동일한 검증·replay·immutability 패턴 재사용, 조건·완료·알림의 원자성 보장, generic 우회 차단이 자연스러움 | 신규 영역 추가 비용 |
| B | 기존 project status 변경 endpoint에 `complete` action만 추가하고 세금계산서는 project 컬럼으로 저장 | 구현 최소 | 정산 draft·version·replay·감사 근거가 약하고 조건 검증이 project store에 흩어짐. 발행 정보 수정 이력이 프로젝트 수정과 섞임 |
| C | 정산을 generic 내 업무 완료에 조건 검사만 덧붙여 처리 | 화면 추가 없음 | 세금계산서 입력·draft·알림·프로젝트 완료를 한 transaction으로 묶기 어렵고 18단계 마지막의 전용 UX·모바일 원칙을 충족하지 못함 |

권장안은 A다. interview의 6개 비차단 선택은 사용자 standing rule에 따라 본문(3·6·8·11장)의 권장안으로 확정해 구현한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 disposable Full-Stack E2E DB만 사용한다.
- migration 필요 여부: `database/migrations/0037`(additive) 1건 — 정산 record·operation receipt 테이블, immutability trigger, `sales.settle` permission/role 부여, projects 완료 actor·시각 컬럼. 기존 migration 수정·번호 재사용 금지, 기존 Active 프로젝트의 Completed backfill 금지.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 비활성 유지.
- runtime 교체 여부: 없음. 현재 experiment 계보 local 검증만.
- 추가 사용자 승인 필요 작업: push·PR·merge·`main` 반영(승인 0/3), Persistent UAT 적용, 외부 알림 채널 event 연결.

## 14. 검증 계획

- 최소 테스트: migration fresh/existing, draft 저장·수정, 완료 성공 경로, 미납품·open Pending·미발행·OnHold/Cancelled/Completed/삭제·stale version·scope·비담당(관리자 포함) 차단, 동일 operation replay와 다른 fingerprint 충돌, 동시 완료 1 success/1 conflict, generic 업무 완료 차단, 완료 알림 recipient·중복 0, 완료 후 settlement 변경 차단(trigger), 프로젝트 취소·purge 정합.
- 영향 영역 회귀: Backend Release 전체 test, 물류(013A)·Pending·workflow·프로젝트 상태 관련 test, Frontend lint·typecheck·unit·build.
- PR/CI: 이번 fast-track은 local experiment commit까지만. isolated Full-Stack E2E에 납품 확정 → 정산 queue → draft → 최종 완료 → project Completed·알림 시나리오를 추가한다.
- 사용자 검수: 페이지별 desktop·390px(및 Teams narrow) screenshot과 user validation checklist를 작성하되 사용자 검수 완료로 표시하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 5장 필수 요구사항 전부 구현, 7장 불변조건 위반 0, 서버 권한·scope·담당 교집합 강제.
- UX: 6장 화면과 모바일 원칙 충족, page-level overflow 0, loading/empty/error/success 구분.
- 자동 테스트: 14장 계획 전부 통과, 미실행 항목은 이유와 함께 기록.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기`로 종료. PR 상태: 없음(local experiment commit only).
- 중단 조건: Repository 충돌, secret·개인정보 노출, 18단계 순서·모든 panel 납품·open Pending 0건·완료 이력·권한 불변조건 위반이 의심되면 fast-track으로 우회하지 않고 blocking decision으로 중단·보고한다.

## 16. 미결정 사항

모두 비차단이며 구현은 본문 권장안으로 진행한다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 완료 뒤 정정·재오픈·수정발행 정책 | 별도 정책 NEW_FEATURE로 분리 / 미제공 유지 | 후속 결정 대기 |
| 2 | 세금계산서 확장 필드와 외부 회계·전자세금계산서 연동 | 최소 projection 유지 / 연동 Task 분리 | 후속 결정 대기 (Roadmap 추적 21) |
| 3 | 프로젝트 완료의 Teams/Mail 외부 채널 event 연결 | 인앱 원본만 유지 / 6.5.2 매트릭스대로 연결 | 후속 결정 대기 (Roadmap 6.5.2.2) |
| 4 | Completed 프로젝트의 일반 정보 수정 잠금 범위 | 현행 유지 / 완료 후 전체 read-only 강화 | 후속 결정 대기 |
| 5 | 영업 정산 Excel export | TASK-EXPORT-001 공통 구조에서 처리 | 후속 결정 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Sales`(또는 `Settlement`) 신규 영역 contracts·store·endpoints, `QmsPermissions`/`QmsPolicies`/`AuthorizationServiceCollectionExtensions`, `Program.cs`, `WorkflowStore`(link·generic 차단), `ProjectStore`(취소 hook·purge 정합)
- Frontend: `SalesSettlementPage` 신규, `App.tsx` route/menu, `api.ts`, `sales.ts`, `styles.css`
- DB/Migration: `database/migrations/0037` additive 1건
- Tests/Scripts: Backend targeted·migration·authorization·concurrency tests, Frontend unit, Full-Stack E2E 시나리오
- Docs: Roadmap 실험 상태 갱신, Task 산출물 5종

## 18. Roadmap 연결

- 선행 Task: TASK-013A(납품 완료·정산 skeleton) — 현재 experiment 계보에서 구현 완료. Pending List(0029) 기반 재사용.
- 후속 Task: TASK-EXPORT-001, 완료 정정 정책 Task, 외부 알림 event coverage 확대.
- 현재 Go/No-Go: canonical queue의 TASK-014A는 `Dependency Pending`이나 2026-07-18 실험 재정렬 승인으로 현재 experiment 계보 진행이 명시 승인됨. canonical 다음 `TASK-007A` Gate는 변경하지 않는다.
- 별도 Task로 분리할 항목: 16장 1~5.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-18 | 실험 fast-track standing rule로 interview 왕복·중간 승인 생략, 비차단 선택 Fable 권장안 자동 채택, TASK-013A 완료 뒤 “다음작업 시작” | interview `COMPLETED_CONFIRMED`, 본 1차 기획 작성 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

1. `0037` additive migration: project당 1건 정산 record(발행일·번호·메모·status·version·완료 actor/시각), 정산 operation receipt, 완료 후 immutability trigger, `sales.settle` permission과 `sales`·`system-administrator` role 부여, projects 완료 actor·시각 컬럼을 0036 계약 준용으로 작성하고 fresh/existing DB를 검증한다.
2. Backend 정산 store/endpoints: queue·detail 조회와 draft 저장·최종 완료를 단일 transaction·row lock(project→settlement 일관 순서)·version·fingerprint replay·`sales.settle`+scope+영업 담당/current assignee 검증으로 구현한다. 완료 조건은 `logistics_delivery_results` 전 active panel 집계, project·active panel 대상 non-`Closed` Pending 0건, 발행일 존재를 서버에서 재검증한다.
3. Workflow·프로젝트 통합: 정산 업무 완료, `SalesSettlementCompleted` StageCompleted event exactly-once, `projects.status='Completed'`+audit event, distinct project 담당자 인앱 완료 알림(idempotency, actor 제외)을 같은 transaction에 묶고, `LinkUrlForWorkItem`에 `/sales/settlement?project=` 분기와 `TransitionWorkItemAsync` 차단 목록을 확장하며, 프로젝트 취소 hook과 purge FK 역순에 정산 데이터를 추가한다.
4. Frontend: `/sales/settlement` adaptive 화면(완료 조건 → 세금계산서 입력 → 최종 확인 한 열 모바일 흐름, desktop queue/detail composition, 완료 후 read-only)과 deep link, 전역 메뉴를 추가한다.
5. 검증: 14장 자동 테스트 전부, isolated Full-Stack E2E 납품→정산→완료 시나리오, 페이지별 desktop·390px screenshot, implementation report·5종 산출물, local experiment commit까지만 수행한다.
6. 안전: 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge를 건드리지 않고, 기존 Active 프로젝트 backfill·기존 migration 수정 없이, 15장 중단 조건에 해당하면 blocking decision으로 중단·보고한다.

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 5
