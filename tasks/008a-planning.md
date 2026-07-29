All baseline verification is done. The interview gate is satisfied (`COMPLETED_CONFIRMED`, `userConfirmed: true`, 0 open blocking decisions), and I revalidated the Roadmap, the current receipts implementation, the Pending module on this branch (including the `pending_issues.target_type` support for `ProcurementItem` that the interview flagged as a design detail), workflow stages, permissions, and the frontend receipts page. Below is the single primary planning draft.

---

# TASK-008A — 자재 도착·분할 입고·IQC 요청·입고 확정 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/008a-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 구매품목 입고가 `ReceiptCompleted` boolean·완료일·비고 한 번의 저장으로만 기록되어 분할 도착, 누적/잔량, IQC 요청, 부적합 차단과 적합품 입고 확정을 단계별로 추적할 수 없다.
- 대상 사용자·역할: 자재 담당(도착 등록·정정·마감·입고 확정), 품질 IQC 담당(최소 적합/부적합 판정), 구매·생산관리(조회), Read-only·System Administrator(허용 범위 조회, 업무 mutation 우회 금지).
- 정상 흐름: 품목 선택 → 도착 수량·일자·메모 등록 → 누적/잔량 재계산 → 도착 건별 IQC 요청 → 품질 적합/부적합 판정 → 적합 건 입고 확정, 부적합 건 Pending 차단.
- 예외·복구 흐름: 0 이하 수량, 주문 수량 초과(수량이 있는 품목), 미래 도착일, 사유 누락, 허용되지 않은 상태 전이는 서버 차단. 정정·취소는 hard delete 없이 사유 필수 event로 처리. 경쟁 mutation은 transaction·row lock·version으로 보호.
- 확정한 정책과 명시적 제외: interview 결정 1~7 (nullable 주문 수량·단위, 도착 건 단위 IQC·확정, 최소 IQC 판정, 부적합 Pending 자동 생성·참조, legacy backfill·derived boolean, Pending `ReinspectionRequested` 이상에서만 재요청하는 단방향 gate, 건당 open Pending 최대 1). 제외: 상세 IQC 성적서·사진·PDF(TASK-009A), 키팅(TASK-010A), 사급(TASK-008B), Excel import/export, 실제 provider, Persistent UAT, 대표 repo·GitHub `main` 반영.
- planning으로 넘긴 비차단 미결정 사항: 구매 Excel 수량·단위 열 추가 시점, 상세 IQC 계약(TASK-009A), 키팅·사급·실제 provider·Persistent UAT 적용 시점. 본 planning에서 부적합 자동 Pending의 초기 담당 배정 정책을 사용자 결정 항목으로 추가했다(16장).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

### 선행 의존성 재검증 결과 (현재 branch 기준)

- `/api/materials/receipts`는 GET 목록과 bulk PATCH(`ProcurementReceiptBulkUpdateRequest`, `ExpectedRowVersion` 낙관적 잠금, field-level audit insert)만 제공한다. 구매품목 model(`ProcurementItemSnapshot`)에 주문 수량·단위 필드는 없다.
- TASK-007A Pending List는 이 branch에 구현되어 있다: migration `database/migrations/0029_pending_list_foundation.sql`, `backend/src/Emi.Qms.Api/Pending/` 모듈, forward-only 전이(`Registered → ActionRequested → InProgress → ReinspectionRequested → Closed`), version·transaction·idempotency 보호, work item·인앱 알림 연결.
- `pending_issues` 테이블은 `target_type` check 제약에 `ProcurementItem`을 이미 허용한다. 현재 API 생성 경로는 `Project` target만 사용하므로, 부적합 자동 생성은 schema 변경 없이 내부 생성 경로에서 `ProcurementItem` target을 사용할 수 있다.
- Workflow에는 `MaterialArrived → IQC → ReceiptConfirmed → KittingCompleted` stage, 부서·responsibility 매핑(`MaterialsPrimary`/`QualityIQC` 등)과 stage 한글명이 정의되어 있다. 프로젝트 집계(`project_work_status`)는 구매 완료 후 `MaterialArrived`까지 계산하며 자재 단계 이후 판정은 아직 없다.
- 권한 기반: `MaterialReceipt.Update` permission·policy는 존재한다. `quality.inspect` permission은 seed에 존재하지만 이를 사용하는 endpoint policy는 아직 없다(신규 policy 필요).
- Roadmap 실행 큐의 canonical 순서는 TASK-007A가 선행이나, 이 실험 branch는 TASK-007A 구현을 포함하며 Task Identity Gate에 `explicitRoadmapOverrideApproved: true`가 기록되어 있다.

## 1. 한 줄 목표

자재 담당자가 구매품목별 도착을 수량 단위로 나누어 등록하고, 도착 건별 IQC 요청·품질 판정·부적합 Pending 차단을 거쳐 적합 분할분만 입고 확정할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 자재 담당자는 `/materials/receipts`에서 품목별 입고 완료 체크·완료일·비고를 일괄 저장한다.
- 분할 도착·잔량·IQC 진행 여부·부적합 여부를 표현할 수 없어 자유 형식 비고에 여러 의미를 함께 적는 우회가 발생한다.
- Roadmap 18단계 중 5(자재 도착)·6(수입검사)·7(입고 확정)이 하나의 boolean으로 뭉개져 품질 차단 없이 완료 처리될 수 있다.
- 방치하면 후속 키팅(TASK-010A)·제조가 실제 사용 가능 수량과 품질 차단 상태를 모른 채 단일 boolean에 의존하고, 인수인계·감사가 불명확해진다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 자재 담당 (`MaterialReceipt.Update`) | 도착 등록·정정·취소, 도착 마감 선언·해제, IQC 요청, 적합 건 입고 확정·확정 취소 | 접근 가능한 프로젝트의 구매품목·도착 건 | 자재 흐름 mutation 전체(사유 필수 정정 포함) |
| 품질 IQC 담당 (`quality.inspect`, 신규 policy) | IQC 요청 건 조회, 적합/부적합·사유 판정 | 접근 가능한 프로젝트의 IQC 요청 건 | 판정 기록만 (도착·확정 mutation 불가) |
| 구매·생산관리 | 품목 상태·누적/잔량·차단 조회 | 기존 역할별 프로젝트 범위 | 없음 (이번 Task 조회 중심) |
| Read-only·System Administrator | 허용 범위 조회 | 기존 정책 범위 | 업무 mutation 우회 금지 |

권한 검사는 UI 숨김이 아니라 서버 policy와 store 검증으로 강제한다. 판정 주체(품질)와 기록 주체가 분리되지 않도록 IQC 판정 endpoint는 품질 권한 전용으로 만든다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 분할 도착과 선검사·선입고

1. 자재 담당이 품목의 도착 등록 action에서 수량·도착일·메모를 입력한다 (모바일은 bottom sheet).
2. 시스템이 수량·도착일을 검증하고 누적 도착·잔량을 재계산해 품목 상태를 `부분도착`으로 갱신한다.
3. 자재 담당이 해당 도착 건의 IQC를 요청하면 품질 IQC 담당 내 업무가 생성된다.
4. 품질 담당이 적합 판정을 기록하면 자재 담당에게 입고 확정 내 업무가 생성되고, 자재 담당이 그 분할분만 입고 확정한다.
5. 잔여 수량이 도착하면 1~4를 반복하고, 도착이 끝나면 자재 담당이 도착 마감을 선언한다. 모든 유효 도착 건이 확정되면 품목 boolean(`receiptCompleted`)이 derived로 `true`가 된다.

### 시나리오 B — 부적합 차단과 재검사 재개

1. 품질 담당이 도착 건에 부적합·사유를 기록한다.
2. 같은 transaction에서 해당 구매품목 대상 Pending(`Nonconformance`)이 자동 생성되고 도착 건이 이를 참조하며 `차단` 상태가 된다. 차단 건은 입고 확정이 서버에서 거부된다.
3. 조치 흐름은 기존 Pending 화면에서 진행한다. Pending이 `ReinspectionRequested` 이상이 되었을 때만 자재 담당이 해당 도착 건의 IQC 재요청을 할 수 있다 (단방향 gate — Pending 상태 변경이 자재 상태를 자동으로 바꾸지 않는다).
4. 재검사 적합이면 시나리오 A의 4번으로 합류한다. 도착 건당 open Pending은 최대 1건이며 재요청 시 기존 open Pending을 재사용한다.

### 시나리오 C — 정정·오류·동시성

1. 자재 담당이 수량 오입력을 발견하면 사유 필수 정정 event로 수량을 고친다. 이력은 append-only로 남는다.
2. 주문 수량이 있는 품목에서 누적+신규 수량이 초과하면 서버가 검증 오류를 action 인접에 한글로 반환한다.
3. 두 사용자가 같은 도착 건을 동시에 처리하면 version 불일치 쪽이 409를 받고 최신 상태 재조회를 안내받는다.

## 5. 기능 요구사항

### 필수

- [ ] 구매품목 nullable 주문 수량·단위 (구매 직접 입력 화면에서 수기 입력, Excel 제외)
- [ ] 도착 건 등록·사유 필수 정정·취소, 품목별 누적 도착·잔량 계산
- [ ] 품목 도착 상태 derived (`미도착`/`부분도착`/`도착완료`)와 자재의 명시적 도착 마감 선언·해제(사유 필수)
- [ ] 도착 건 단위 IQC 요청과 품질 담당의 최소 판정(적합/부적합·사유)
- [ ] 부적합 판정 transaction에서 구매품목 대상 Pending 자동 생성(도착 건이 참조, 건당 open 최대 1, 재사용)
- [ ] Pending `ReinspectionRequested` 이상에서만 허용되는 IQC 재요청 단방향 gate
- [ ] 적합 도착 건 단위 입고 확정과 사유 필수 확정 취소, `receiptCompleted` boolean의 derived 전환
- [ ] legacy `receipt_completed=true` 품목의 수량 null 표시 backfill migration
- [ ] IQC 요청→품질 내 업무, 적합 판정→자재 입고 확정 내 업무의 idempotent 자동 생성 (인앱 원본만)
- [ ] 서버 validation(0 이하·초과·미래 도착일·사유 누락·전이 위반)과 transaction·row lock·version 보호
- [ ] Desktop 관리형 화면과 390px 모바일 card·bottom sheet UX

### 선택

- [ ] 도착 등록 시 구매·생산관리 참조 인앱 알림 (원본만, 발송 없음)
- [ ] 품목 목록의 주문 수량 대비 진행 bar 표시

### 명시적 제외

- [ ] 상세 IQC 체크리스트·필수 사진·판정 UI·PDF snapshot (TASK-009A)
- [ ] 키팅 완료·제조 내 업무 생성 (TASK-010A)
- [ ] 사급 자재 (TASK-008B)
- [ ] 구매·자재 Excel import/export 계약 변경 (주문 수량 열 포함)
- [ ] 실제 Teams/Mail/Activity provider 발송, Persistent UAT write, runtime handover
- [ ] 대표 repo·GitHub `main`의 push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 자재 입고 현황 (기존 `/materials/receipts` 개편) | 기존 자재 메뉴 | 프로젝트별 품목 card/table: 주문 수량·단위, 누적/잔량(수량 없으면 `-`), 도착 상태 badge, 도착 건 목록·건별 상태, 다음 행동 | 도착 등록, 정정·취소, 도착 마감, IQC 요청, 입고 확정 | action 인접 성공/오류 메시지, 409 시 재조회 안내 |
| 도착 등록/정정 입력 | 품목 card의 primary action | 수량(numeric input)·단위, 도착일, 메모, 정정 시 사유 | 저장/취소 | field 단위 한글 오류, 첫 오류 focus |
| IQC 대기 목록 (신규, 품질용) | 품질 메뉴 (신규 최소 화면) | 요청된 도착 건: 프로젝트·품목·수량·요청일 | 적합/부적합·사유 판정 | 판정 결과 인접 표시, 부적합 시 Pending 링크 |
| Pending 상세 (기존) | 차단 도착 건의 Pending 링크 | 기존 TASK-007A 화면 재사용 | 기존 조치·재검사 요청 흐름 | 기존 계약 유지 |

확인할 UX 항목:

- 품목 상태·누적/잔량·차단 여부가 목록에서 즉시 이해되는가?
- 각 상태에서 다음 행동(등록→요청→판정→확정) 버튼이 하나로 명확한가?
- 저장·판정 결과가 해당 도착 건 근처에 보이는가?
- 권한 없음(품질 판정 vs 자재 mutation)·차단 상태·version 충돌이 구분되어 안내되는가?
- 390px·Teams narrow에서 card 전환, 44px action, sticky primary action, page-level horizontal overflow 0을 지키는가?

## 7. 업무 규칙과 불변조건

- 18단계 순서(5 도착 → 6 IQC → 7 입고 확정)를 도착 건 단위에서도 우회할 수 없다: IQC 요청 없는 판정, 적합 판정 없는 확정, 차단 건 확정은 서버가 거부한다.
- 수량 무결성: 도착 수량 > 0, 주문 수량이 있으면 유효(미취소) 도착 수량 합 ≤ 주문 수량, 도착일은 미래 불가.
- append-only 이력: 도착·정정·취소·요청·판정·확정·마감은 event로 남기고 hard delete·이력 덮어쓰기를 금지한다. 정정·취소·마감 해제·확정 취소는 사유 필수.
- Pending 결합은 단방향이다: 자재→Pending 자동 생성과 상태 조회만 있고, Pending 상태 변경이 자재 도착 건 상태를 자동 변경하지 않는다. 도착 건당 open Pending 최대 1건.
- `receipt_completed`는 신규 상태의 derived 값이며 직접 편집 경로를 남기지 않는다 (단일 진실).
- 기존 Pending forward-only 전이, 기존 구매정보·Excel 계약, 기존 migration 번호·내용, workflow·audit 계약을 변경하지 않는다.
- 동일 이벤트 재실행이 내 업무·알림·Pending을 중복 생성하지 않는다 (idempotency key).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 구매품목 주문 수량·단위 | nullable additive 컬럼, 구매 화면 수기 입력 | 기존 테이블 확장 | 기존 구매 field audit 재사용 |
| 자재 도착 건 | 품목당 N건: 수량·도착일·메모·상태·version·legacy 표시 | 신규 테이블 | append-only, 정정·취소 event 사유 필수 |
| IQC 요청·판정 | 도착 건당 요청 이력: 요청자·요청일·결과·사유·판정자 (재요청 시 새 요청 row) | 신규 테이블 | append-only, 도착 건당 open 요청 최대 1 |
| 도착 건 event 이력 | 등록·정정·취소·요청·판정·확정·마감의 감사 원장 | 신규 테이블 | append-only, 변경 전/후·사유·행위자·시각 |
| 품목 도착 마감 | 자재의 명시적 선언 (시각·행위자) | 기존 테이블 nullable 컬럼 | 해제 시 사유 필수 event |
| `receipt_completed` | 신규 상태 derived 호환 필드 (기존 조회·dashboard 회귀 방지) | 기존 컬럼 의미 전환 | backfill migration에서 1회 정합화 |
| Pending 참조 | 도착 건 → `pending_issues` FK, target_type `ProcurementItem` 사용 | 기존 Pending 재사용 | 기존 Pending history 계약 유지 |

```text
[도착 건] 등록 → IQC요청 → 적합 ──────→ 입고확정
              ↑         ↘ 부적합차단(Pending 참조)
              └─ 재요청(연결 Pending이 ReinspectionRequested 이상일 때만)
   (등록·적합 상태에서 사유 필수 정정/취소, 확정 상태에서 사유 필수 확정 취소)

[품목 derived] 미도착 → 부분도착 → 도착완료(자재의 명시적 마감)
receiptCompleted := 도착 마감 ∧ 유효 도착 건 ≥ 1 ∧ 모든 유효 도착 건 = 입고확정
```

legacy backfill: `receipt_completed=true` 품목마다 수량 null·legacy 표시 도착 건 1건(입고확정 상태)과 도착 마감을 생성하고 기존 완료일·완료자·비고를 보존 매핑한다. `false` 품목은 도착 건 없이 `미도착`으로 시작한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 7장의 모든 불변조건 (수량·전이·gate·권한·중복 방지).
- 필요한 조회와 mutation (경로·이름은 조사 기반 후보이며 구현 시 기존 convention에 맞춘다):
  - GET `/api/materials/receipts` 확장 — 품목별 수량·누적/잔량·도착 상태·도착 건 목록·다음 행동.
  - POST 도착 등록, POST 도착 건 정정/취소, POST 품목 도착 마감/해제.
  - POST 도착 건 IQC 요청, POST IQC 판정(품질 전용), POST 입고 확정/확정 취소.
  - GET IQC 대기 목록 (품질 전용 policy).
  - 기존 bulk PATCH boolean 편집은 제거하고 frontend 사용처를 신규 mutation으로 대체한다 (derived 전환 후 이중 진실 차단).
- 권한·validation: 자재 mutation은 기존 `MaterialReceiptUpdate` policy, 판정·품질 조회는 `quality.inspect` 기반 신규 policy를 추가한다. 오류는 안정적 status와 한글 메시지로 반환한다.
- transaction·동시성·idempotency: 도착 건 row lock + version, 품목 수량 합 검증은 같은 transaction에서 lock 후 재계산, 부적합 판정+Pending 생성+알림은 단일 transaction, 내 업무·알림은 도착 건·요청 version 기반 idempotency key로 중복 방지 (TASK-007A `PendingStore` 패턴 재사용).
- audit trail: 신규 event 테이블 + 기존 procurement field audit(주문 수량·단위 편집)과 Pending history를 그대로 사용한다.
- 외부 provider 영향: 없음. 인앱 notification 원본·recipient만 생성하고 delivery queue·실제 발송은 만들지 않는다.
- 부적합 자동 Pending 생성은 기존 `POST /api/pending` 사용자 경로를 호출하지 않고 store 수준 내부 생성 경로를 사용하되, title·description 규칙과 `ProcurementItem` target을 적용하고 기존 검증·history 계약을 우회하지 않는다.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다.

## 10. Frontend 고려사항

- route/component: 기존 수동 router 유지. `MaterialReceiptsPage`를 상태 기반 화면으로 개편하고, 품질용 IQC 대기 화면 1개를 추가한다. Pending 상세는 기존 `/pending/{id}` deep link를 재사용한다.
- loading/empty/error/success: 기존 `LoadState`·`StateMessage` 패턴 재사용, 도착 건 단위 action 인접 피드백, 409 시 재조회 안내.
- 공통 Action Feedback: 중복 submit 차단, 첫 오류 focus, `aria-live` 안내, field 단위 한글 오류.
- 접근성: checkbox/버튼 label, role 구조, keyboard 접근 회귀 검증.
- 390px/mobile/narrow pane: 표 축소가 아닌 프로젝트/품목 card, 단계 indicator, 44px action, numeric input, sticky primary action, page-level horizontal overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: workflow stage(`MaterialArrived`/`IQC`/`ReceiptConfirmed`)의 담당 responsibility·fallback을 내 업무 생성에 재사용한다. 프로젝트 집계(`project_work_status`)의 자재 단계 반영은 도착 마감·확정 derived 값으로 확장하되 기존 판정 로직과 충돌하면 구현 전 보고한다.
- 권한/관리자: 신규 permission 추가 없이 기존 `MaterialReceipt.Update`·`quality.inspect`를 policy로 연결한다. System Administrator·Read-only의 업무 mutation 우회 금지를 유지한다.
- Excel/PDF/첨부: 계약 변경 없음. 구매 Excel의 주문 수량 열은 deferred 결정이다.
- Teams/Mail: 원본 인앱 알림만 생성, 채널 matrix 변경 없음.
- 삭제·복구/감사: hard delete 없음, 사유 필수 event 복구, 기존 procurement audit·Pending history 보존.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 신규 도착 건·IQC 요청·event 테이블 + 품목 derived 상태 + legacy backfill. IQC 판정은 별도 요청 테이블로 재요청 이력을 보존 | interview 결정 1~7과 정확히 일치. 단일 진실, TASK-008B/009A/010A의 기반 데이터 제공, Pending 계약 무변경 | 신규 테이블 3개와 화면 개편으로 이번 Task 변경량이 큼 |
| B | 도착 건 없이 품목 테이블에 상태·누적 컬럼만 추가 | 변경 최소 | 분할 도착·건별 IQC·부분 확정 표현 불가 — interview 결정 2·7 위반이므로 채택 불가 |
| C | IQC 결과를 도착 건 컬럼으로만 저장 (요청 테이블 없음) | 테이블 1개 절약 | 재요청 시 이전 판정·사유 이력이 덮여 append-only 원칙과 TASK-009A 대체 계약이 약해짐 |

권장안은 A이며, C와의 차이(IQC 요청 테이블 분리)는 재검사 이력 보존과 TASK-009A의 상세 성적서 대체 경계를 위해 A에 포함한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated DB·synthetic 데이터만 사용하고 Persistent UAT write·migration 적용을 하지 않는다.
- migration 필요 여부: 필요. 다음 번호(현재 최신 `0029` 이후)의 additive migration 1건 — 구매품목 nullable 컬럼, 신규 테이블, legacy backfill. 기존 migration 번호·내용 불변, rollback은 forward-fix 원칙으로 문서화한다. 실 DB(운영·Persistent UAT) 적용은 이 planning 승인과 별개의 사용자 승인이 필요하다 (Roadmap 실행 큐의 "migration 별도 승인").
- 외부 발송/실제 데이터 영향: 없음. fake/dry-run 경계 유지, 인앱 원본만 생성.
- runtime 교체 여부: 없음. experiment branch 내 검증만 수행한다.
- 추가 사용자 승인 필요 작업: 실 DB migration 적용, 대표 repo 반영(push·PR·merge), 기존 bulk PATCH 제거가 아닌 유지가 필요하다고 판단될 경우의 정책 변경.

## 14. 검증 계획

- 최소 테스트: Backend Release build, 신규 store filtered tests(수량 검증, 전이, gate, Pending 자동 생성·재사용, idempotency, 동시성 409), migration catalog + fresh/existing isolated apply + backfill 정합(완료 품목 boolean 회귀 0).
- 영향 영역 회귀: 구매 목록·dashboard·Excel preview/apply의 `receiptCompleted` 소비처, workflow·내 업무·알림 filtered tests, 권한 allow/deny matrix(자재/품질/구매/Read-only/System Administrator), Pending 기존 E2E 회귀.
- PR/CI: 대표 repo PR 없음(실험 branch). Frontend lint/typecheck/unit/build와 isolated Full-Stack E2E(자재 흐름 신규 spec + 기존 spec)를 local에서 통과시킨다.
- 사용자 검수: desktop·390px에서 자재 목록, 도착 등록, IQC 요청/판정, 부적합 차단·Pending 연결, 입고 확정을 페이지별 synthetic screenshot으로 확인한다. 자동 검증 완료와 사용자 검수 완료를 별도 상태로 기록한다.

## 15. 완료 기준

- 기능/권한/데이터: 시나리오 A~C가 서버 authoritative로 동작하고 7장의 불변조건 위반 시도가 모두 차단·테스트된다.
- UX: 6장 확인 항목과 390px overflow 0을 충족한다.
- 자동 테스트: 14장 최소·영향 테스트 전체 PASS, 미실행 항목은 이유와 함께 기록.
- 5종 산출물: implementation report에 5종 상태·위치를 추적한다.
- 사용자 검수 상태: `사용자 검수 대기`로 handoff하고 완료로 표기하지 않는다.
- PR 상태: N/A — 실험 branch local commit까지가 범위다.

중단 조건: 기존 migration·Pending·workflow 계약과 additive로 해소할 수 없는 충돌 발견, Roadmap·문서와 구현의 의미 있는 충돌, backfill이 기존 완료 품목의 업무 의미를 바꾸는 경우 — 임의 선택하지 않고 보고 후 중단한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 부적합 자동 생성 Pending의 초기 담당 배정 | A: `Registered`·긴급·미배정으로 생성하고 배정은 기존 Pending 화면에서 수행 (권장 — 기존 생성 계약과 생산관리 관리 원칙 보존) / B: 구매 정담당 자동 배정 (배정 알림 즉시 생성되나 fallback·오배정 정정 부담) | 대기 |
| 2 | 구매 Excel의 주문 수량·단위 열 추가 시점 | A: 후속 Task / B: 추가 안 함 | 대기 (deferred) |
| 3 | 상세 IQC 성적서·사진·PDF 계약 | TASK-009A planning에서 결정 | 대기 (deferred) |
| 4 | 키팅(TASK-010A)·사급(TASK-008B)·실제 provider·Persistent UAT 적용 시점 | 각 Task 승인 시점에 결정 | 대기 (deferred) |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Procurement/` 확장 또는 신규 `Materials` 모듈(store·contracts·endpoints), `Authorization`의 신규 품질 판정 policy, `Pending/PendingStore.cs` 내부 생성 경로, `Workflow`·알림 연결, `Program.cs` 등록.
- Frontend: `frontend/src/App.tsx`(MaterialReceiptsPage 개편·IQC 화면·라우팅), `frontend/src/api.ts`, `frontend/src/styles.css`.
- DB/Migration: `database/migrations/`의 다음 번호 additive migration 1건 (컬럼·신규 테이블·backfill).
- Tests/Scripts: `backend/tests/Emi.Qms.Api.Tests/`(migration·store·authorization·동시성), `frontend/e2e/full-stack/` 자재 흐름 spec.
- Docs: Roadmap 상태 갱신과 implementation report (구현 Task에서).

## 18. Roadmap 연결

- 선행 Task: TASK-007A Pending List — 이 branch에 구현 완료(실험, canonical 미반영). 구매정보(TASK-004A 계열) 완료.
- 후속 Task: TASK-008B(사급, 이번 데이터 모델 재사용), TASK-009A(상세 IQC가 최소 판정 계약을 대체·확장), TASK-010A(입고 확정을 키팅 전제 조건으로 사용).
- 현재 Go/No-Go: canonical Roadmap 순서는 TASK-007A가 Next Gate이나, 실험 branch의 순서 재정렬 승인(`explicitRoadmapOverrideApproved: true`)이 Task Identity Gate에 기록되어 있다. 대표 repo 기준 canonical 상태는 변경하지 않는다.
- 별도 Task로 분리할 항목: 상세 IQC·사진·PDF, 키팅, 사급, Excel 수량 열, Pending 유형 관리자 화면.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | 실험 사전 지시에 따라 interview 요약 확인을 planning 입력으로 채택 | interview 결정 1~7을 0장·5장·7장·8장에 반영 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

이 초안은 planning·review 승인 후 새 Codex 구현 세션이 사용할 계약 요약이며, 승인 전 실행 지시가 아니다.

1. instruction chain gate를 수행하고 `taskType: APPROVED_FEATURE_IMPLEMENTATION`, branch 기준선을 보고한다.
2. 다음 번호 additive migration 1건: 구매품목 nullable 주문 수량·단위·도착 마감 컬럼, 도착 건·IQC 요청·event 테이블, legacy backfill. 기존 migration은 변경하지 않는다.
3. Backend: 8~9장의 상태 모델·validation·gate·transaction·idempotency를 store에 구현하고, 자재 mutation은 기존 `MaterialReceiptUpdate` policy, IQC 조회·판정은 `quality.inspect` 기반 신규 policy로 연결한다. 부적합 판정 transaction에서 `ProcurementItem` target Pending을 내부 생성 경로로 만들고 도착 건이 참조한다. 기존 bulk PATCH boolean 편집 경로를 신규 mutation으로 대체한다.
4. Frontend: `/materials/receipts` 상태 기반 개편과 품질 IQC 대기 화면을 desktop·390px에서 구현하고 기존 LoadState·Action Feedback 패턴을 재사용한다.
5. 검증: 14장 계획을 실행하고 실행/미실행을 분리해 implementation report에 기록한다. Persistent UAT write, 실제 provider 발송, 대표 repo 게시를 수행하지 않는다.
6. 미결정 사항 1(16장)은 사용자 결정 전 권장안 A의 기본값으로 두되, 결정이 기록되면 그 값을 따른다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 4
