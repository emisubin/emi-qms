# TASK-008A 자재 입고 기획 — 자재 도착·분할 입고·IQC 요청·입고 확정 (Fable 2차 기획)

- primaryDraftStatus: `DRAFT_FOR_USER_REVIEW`
- sourceTask: `TASK-008A`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/008a-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`, open blocking 결정 0)
- firstDraftSource: `tasks/008a-planning.md` (Fable 1차 기획 원문, 수정하지 않음)
- reviewSource: `tasks/008a-review.md` (Codex 내용 review, resolution 7건)
- approvalSource: `tasks/008a-change-001.md` (사용자 명시 요청으로 이 문서를 primary draft target으로 승인)

## 1. 문서 목적과 위치

이 문서는 `TASK-008A`의 두 번째 Fable 기획 전문이다. `tasks/008a-planning.md`의 방향을 유지하되, `tasks/008a-review.md`의 유지·추가·보류·제거 판단과 7개 resolution, `tasks/008a-change-001.md`의 필수 반영사항을 최종 구현 계약으로 통합한다. 사용자 workflow(`Fable 기획 → Codex review → Fable 2차 기획 → Codex 코딩`)에 따라 1차 기획과 의도적으로 분리된 별도 문서이며, 어느 쪽도 다른 쪽을 덮어쓰지 않는다.

이 문서 자체는 게시 승인이나 구현 승인을 부여하지 않는다. 승인 값과 Git 경계(commit·push·PR·merge·migration 적용)는 `tasks/008a-change-001.md`와 Roadmap의 별도 승인 절차를 따른다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 참조하며 여기에 복사하지 않는다.

## 2. 확정된 기준선

### 2.1 Interview 확정 사항 (결정 1~7)

- 구매품목에 nullable 주문 수량·단위를 직접 입력으로 추가한다. Excel은 이번 Task에서 제외한다.
- 도착 건별로 IQC 요청·판정·입고 확정을 수행하고, 품목 도착 상태는 derived 값과 자재 담당의 명시적 도착 마감으로 관리한다.
- 상세 체크리스트·사진 없이 품질 담당의 최소 적합/부적합·사유 판정만 포함한다 (상세 IQC는 `TASK-009A`).
- 부적합 판정 transaction에서 구매품목 대상 Pending(`Nonconformance`)을 자동 생성하고 도착 건 흐름이 이를 참조한다.
- legacy `receipt_completed=true`를 호환 backfill하고 boolean은 신규 상태의 derived 값으로 전환한다 (단일 진실).
- 연결 Pending이 `ReinspectionRequested` 이상일 때만 IQC 재요청을 허용하는 단방향 gate를 사용한다.
- 확정 차단은 도착 건 단위이며, 도착 건당 open Pending은 최대 1건이고 open Pending은 재사용한다.

### 2.2 Codex review resolution (2차 기획에서 고정)

| Finding | 이 문서의 반영 |
| --- | --- |
| `008A-SINGLE-TRUTH-WRITERS` (P1) | `receipt_completed`의 모든 direct writer를 조사·차단한다 — 7장 |
| `008A-ATOMIC-INTEGRATION` (P1) | Materials store를 transaction owner로 고정하고 transaction-aware Pending helper를 추가한다 — 9장 |
| `008A-REPEAT-NONCONFORMANCE` (P2) | 도착 건 단일 Pending FK를 폐기하고 IQC attempt별 nullable Pending 참조로 반복 cycle을 보존한다 — 8·9장 |
| `008A-PENDING-CLOSURE` (P2) | 재검사 적합 판정과 Pending `Closed`·history·work item 종결을 같은 transaction에서 처리한다 — 9장 |
| `008A-QUANTITY-UNIT` (P2) | `numeric(18,3)`, 수량·단위 pair invariant, 품목 단위 상속, legacy 예외를 명시한다 — 6장 |
| `008A-BACKWARD-TRANSITIONS` (P2) | in-place 수량 정정, IQC 이후 취소, 확정 취소, 마감 해제를 제거한다. 취소는 `Arrived`에서만 사유 필수로 허용한다 — 5장 |
| `008A-WORK-ITEM-TARGET` (P2) | `Inspection`/`ProcurementItem` work item target과 도착 건별 idempotency key를 사용한다 — 10장 |

### 2.3 Review에서 자동 채택된 비차단 결정

- 부적합 자동 Pending은 `Registered`·`Urgent`·미배정으로 생성하고, 담당 배정은 기존 Pending 화면의 배정 흐름을 사용한다.
- 주문 단위는 master taxonomy 없이 trim된 자유 입력 1~20자로 제한한다.
- 주문 수량 도달 시 도착 마감을 제안만 하고 자동 마감하지 않는다 — 마감은 자재 담당의 명시적 행동이다.
- 재검사 적합 시 연결 Pending을 자동 `Closed` 처리한다.
- 기존 Excel apply의 입고 완료 변경 시도는 silent ignore가 아니라 validation error로 거부한다.

### 2.4 선행 의존성 (현재 실험 branch 기준)

- TASK-007A Pending List가 이 branch에 구현되어 있다: forward-only 전이(`Registered → ActionRequested → InProgress → ReinspectionRequested → Closed`), version·transaction·idempotency 보호, work item·인앱 알림 연결.
- `pending_issues.target_type`은 `ProcurementItem`을 이미 허용하므로 부적합 자동 생성은 schema 변경 없이 내부 생성 경로에서 사용할 수 있다.
- 기존 `/api/materials/receipts`는 GET 목록과 bulk PATCH(낙관적 잠금, field audit)만 제공하며 구매품목에 주문 수량·단위 필드는 없다.
- Workflow에 `MaterialArrived → IQC → ReceiptConfirmed` stage와 부서·responsibility 매핑이 정의되어 있다.
- `MaterialReceipt.Update` permission·policy는 존재하고, `quality.inspect` permission은 seed에 있으나 endpoint policy는 신규로 필요하다.
- canonical Roadmap 순서 대비 실험 branch의 순서 재정렬은 Task Identity Gate에 `explicitRoadmapOverrideApproved: true`로 기록되어 있다. 대표 repo의 canonical 상태는 변경하지 않는다.

## 3. 한 줄 목표

자재 담당자가 구매품목별 도착을 수량 단위로 나누어 등록하고, 도착 건별 IQC 요청·품질 판정·부적합 Pending 차단을 거쳐 적합 분할분만 입고 확정할 수 있게 한다.

## 4. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 자재 담당 (`MaterialReceipt.Update` policy) | 도착 등록, `Arrived` 상태 취소(사유 필수), 도착 마감, IQC 요청·재요청, 적합 건 입고 확정 | 접근 가능한 프로젝트의 구매품목·도착 건 | 자재 흐름 mutation 전체 |
| 품질 IQC 담당 (`quality.inspect` 기반 신규 policy) | IQC 요청 건 조회, 적합/부적합·사유 판정 | 접근 가능한 프로젝트의 IQC 요청 건 | 판정 기록만 — 도착·확정 mutation 불가 |
| 구매·생산관리 | 품목 상태·누적/잔량·차단 조회, 주문 수량·단위 입력(기존 구매 편집 권한 범위) | 기존 역할별 프로젝트 범위 | 구매 필드 편집 외 자재 mutation 없음 |
| Read-only·System Administrator | 허용 범위 조회 | 기존 정책 범위 | 업무 mutation 우회 금지 |

권한은 UI 숨김이 아니라 서버 policy와 store 검증으로 강제한다. 판정 주체(품질)와 자재 mutation 주체(자재)는 endpoint policy 수준에서 분리한다.

## 5. 핵심 사용자 시나리오

### 시나리오 A — 분할 도착과 선검사·선입고

1. 자재 담당이 품목의 도착 등록 action에서 수량·도착일·메모를 입력한다 (모바일은 bottom sheet).
2. 시스템이 수량·도착일을 검증하고 누적 도착·잔량을 재계산해 품목 상태를 `부분도착`으로 갱신한다.
3. 자재 담당이 해당 도착 건의 IQC를 요청하면 품질 IQC 담당의 내 업무가 생성된다.
4. 품질 담당이 적합 판정을 기록하면 자재 담당에게 입고 확정 내 업무가 생성되고, 자재 담당이 그 분할분만 입고 확정한다.
5. 잔여 수량이 도착하면 1~4를 반복한다. 주문 수량이 채워지면 시스템이 도착 마감을 제안하고, 자재 담당이 명시적으로 마감을 선언한다. 모든 유효 도착 건이 `Confirmed`이고 마감이 선언되면 `receiptCompleted`가 derived로 `true`가 된다.

### 시나리오 B — 부적합 차단과 재검사 재개

1. 품질 담당이 도착 건에 부적합·사유를 기록한다.
2. 같은 transaction에서 해당 구매품목 대상 Pending(`Nonconformance`, `Registered`·`Urgent`·미배정)이 생성되거나 기존 open Pending이 재사용되고, 해당 IQC attempt가 이를 참조하며 도착 건은 `FailedBlocked`가 된다. 차단 건의 입고 확정은 서버가 거부한다.
3. 조치는 기존 Pending 화면에서 진행한다. 연결 Pending이 `ReinspectionRequested` 이상일 때만 자재 담당이 IQC 재요청을 할 수 있다. Pending 상태 변경이 자재 상태를 자동으로 바꾸는 역방향 callback은 없다.
4. 재검사 적합 판정 transaction에서 해당 Pending이 `Closed`로 전이되고 history·work item이 함께 종결된다. 이후 시나리오 A의 4번으로 합류한다.
5. 재검사에서 다시 부적합이면: open Pending이 있으면 재사용하고, 이전 Pending이 이미 `Closed`면 새 Pending을 생성해 과거 cycle 이력을 보존한다. 동일 시점의 open Pending은 도착 건당 최대 1건이다.

### 시나리오 C — 오입력 취소와 동시성

1. 자재 담당이 `Arrived` 상태(IQC 요청 전)의 수량 오입력을 발견하면 사유 필수 취소 후 새 도착 건으로 다시 등록한다. in-place 수량 수정은 제공하지 않는다.
2. IQC 요청 이후의 도착 건 취소, 입고 확정 취소, 도착 마감 해제는 이번 MVP에서 제공하지 않는다 — 18단계 forward-only와 append-only 감사 해석을 보존한다.
3. 주문 수량이 있는 품목에서 유효 도착 수량 합이 초과하면 서버가 한글 validation 오류를 action 인접에 반환한다.
4. 두 사용자가 같은 도착 건을 동시에 처리하면 version 불일치 쪽이 409를 받고 최신 상태 재조회를 안내받는다.

## 6. 데이터·상태 모델

### 6.1 도착 건 상태 전이 (forward-only)

```text
도착 건: Arrived → IqcRequested → Passed → Confirmed
                         ↘ FailedBlocked → ReinspectionRequested → IqcRequested

취소: Arrived 상태에서만 Cancelled (사유 필수, 새 도착 건으로 재등록)
부적합: FailedBlocked에서 Pending open (attempt가 참조)
재검사 적합: Passed 전이 + 연결 Pending Closed를 같은 transaction에서 처리
```

- 품목 도착 상태는 `미도착 / 부분도착 / 도착마감`으로 표시한다.
- `receiptCompleted := 도착마감 ∧ 유효(미취소) 도착 건 ≥ 1 ∧ 모든 유효 도착 건 = Confirmed`.
- legacy backfill 건은 생성 시점부터 `Confirmed`이며 재편집 action을 제공하지 않는다.

### 6.2 수량·단위 계약

- 수량은 `numeric(18,3)`의 양수만 허용한다.
- 신규 비-legacy 품목은 주문 수량과 단위를 한 쌍으로 입력한다. 한쪽만 존재하는 상태는 서버와 DB constraint에서 모두 차단한다.
- 단위는 master 없이 trim된 자유 입력 1~20자로 제한하고, 도착 건은 품목 단위를 상속해 단위 혼합을 금지한다.
- legacy backfill 도착 건만 `quantity=null`, `unit=null`, `is_legacy=true`를 허용한다.
- 주문 수량이 있으면 유효 도착 수량 합 ≤ 주문 수량, 도착일은 미래 불가.

### 6.3 데이터 개념

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 구매품목 주문 수량·단위 | nullable additive 컬럼 pair, 구매 화면 수기 입력 | 기존 테이블 확장 | 기존 구매 field audit 재사용 |
| 자재 도착 건 | 품목당 N건: 수량·도착일·메모·상태·version·legacy 표시 | 신규 테이블 | append-only, `Arrived` 취소만 사유 필수 event |
| IQC 요청/판정 attempt | 도착 건당 attempt 이력: 요청자·요청일·결과·사유·판정자·nullable Pending 참조 | 신규 테이블 | append-only, 도착 건당 open 요청 최대 1, cycle별 Pending 이력 보존 |
| 도착 건 event 이력 | 등록·취소·요청·판정·확정·마감의 감사 원장 | 신규 테이블 | append-only, 변경 전/후·사유·행위자·시각 |
| 품목 도착 마감 | 자재 담당의 명시적 선언 (시각·행위자) | 기존 테이블 nullable 컬럼 | 해제 없음 (이번 MVP) |
| `receipt_completed` | 신규 상태의 derived 호환 필드 | 기존 컬럼 의미 전환 | backfill migration에서 1회 정합화, 이후 신규 store만 갱신 |
| Pending 참조 | IQC attempt → `pending_issues` (target_type `ProcurementItem`) | 기존 Pending 재사용 | 기존 Pending history 계약 유지 |

### 6.4 Migration과 legacy backfill

- 다음 번호 additive migration 1건(`0030`, 현재 최신 `0029` 이후): 구매품목 nullable 수량·단위·도착 마감 컬럼, 도착 건·IQC attempt·event 테이블, DB constraint, legacy backfill. 기존 migration 번호·내용은 변경하지 않는다.
- backfill: `receipt_completed=true` 품목마다 `quantity=null`·`is_legacy=true`·`Confirmed` 도착 건 1건과 도착 마감을 생성하고 기존 완료일·완료자·비고를 보존 매핑한다. `false` 품목은 도착 건 없이 `미도착`으로 시작한다. backfill 전후 완료 품목의 boolean 회귀는 0이어야 한다.
- rollback은 forward-fix 원칙으로 문서화한다. 실 DB(운영·Persistent UAT) 적용은 이 문서와 별개의 사용자 승인이 필요하다.

## 7. 단일 진실 writer 차단

`receipt_completed`는 신규 Materials store만 계산·갱신한다.

- writer inventory 대상: `/api/materials/receipts` bulk PATCH, 프로젝트 구매정보 PATCH, 구매 Excel preview/apply, 구매 row insert/update를 포함한 모든 기존 writer를 구현 시 조사한다.
- 모든 direct writer의 완료값 변경 입력은 silent ignore가 아니라 안정적인 validation error로 거부한다.
- 기존 bulk PATCH의 boolean 편집 경로는 신규 mutation으로 대체한다. boolean 편집과 신규 상태를 병행하는 compatibility write는 만들지 않는다.
- 기존 API response·dashboard·Excel 표시 필드는 호환 read projection으로 유지해 조회 회귀를 막는다.

## 8. 반복 부적합 cycle 보존

- 도착 건 테이블에 단일 `pending_issue_id` FK를 두지 않는다.
- 각 IQC attempt가 `pending_issue_id`를 nullable로 참조한다. 한 도착 건은 시간상 여러 Pending과 연결될 수 있으며, 동일 시점의 open Pending만 최대 1건이다.
- 재부적합 시 open Pending이 있으면 재사용하고, 이전 Pending이 `Closed`면 새 Pending을 생성해 과거 cycle을 보존한다.

## 9. Transaction·Pending 통합 계약

- 신규 Materials store가 도착·IQC·확정 transaction의 owner다. 도착 건 row lock + version, 품목 수량 합 검증은 같은 transaction에서 lock 후 재계산한다.
- `PendingStore`에 기존 검증·history·assignment artifact를 재사용하는 internal transaction-aware 생성/종결 helper를 추가한다. 별도 connection을 여는 기존 생성 메서드 호출이나 HTTP self-call은 금지한다.
- 부적합 판정 + Pending 생성/재사용 + work item·인앱 알림을 단일 transaction으로 처리한다.
- 재검사 적합 판정 + Pending `Closed` 전이 + history·work item 종결도 단일 transaction으로 처리한다 — 적합 자재인데 open Pending이 남는 모순을 차단한다.
- 상태 결합 방향은 Materials → Pending 단방향으로 유지하고 Pending → 자재 자동 callback은 만들지 않는다.
- Pending의 기존 forward-only 전이·history 계약은 변경하지 않는다.

## 10. 내 업무·알림·deep link

- IQC work item: `target_type='Inspection'`, `target_id=iqc_request_id`.
- 입고 확정 work item: `target_type='ProcurementItem'`, `target_id=procurement_item_id`, idempotency key에 도착 건 ID를 포함해 분할 건별 업무가 서로 덮이지 않게 한다.
- 각 업무는 기존 shell 안의 실제 action 위치로 이동한다: `/quality/iqc?request=…`, `/materials/receipts?receipt=…`, Pending은 기존 `/pending/{id}`.
- 업무·알림은 Materials transaction과 같은 connection에서 idempotency key로 생성하며, 인앱 원본·recipient만 만들고 delivery row·실제 발송은 만들지 않는다.

## 11. API·Backend 계약

신규 Materials API는 다음만 제공한다 (경로·이름은 구현 시 기존 convention에 맞춘다).

- GET 자재 입고 목록 확장 — 품목별 수량·단위, 누적/잔량, 도착 상태, 도착 건·attempt 목록, 차단·다음 행동.
- 주문 수량·단위 저장 (pair invariant, 기존 구매 필드 audit).
- POST 도착 등록, POST `Arrived` 상태 취소(사유 필수), POST 품목 도착 마감.
- POST IQC 요청·재요청 (재요청은 `ReinspectionRequested` 이상 gate).
- POST IQC 판정 — `quality.inspect` 기반 신규 policy 전용.
- POST 입고 확정 (적합 건만, 차단 건 거부).
- GET IQC 대기 목록 — 품질 전용 policy.

공통 규칙: 자재 mutation은 기존 `MaterialReceiptUpdate` policy, 오류는 안정적 status와 한글 메시지, version 충돌은 409, 동일 이벤트 재실행은 업무·알림·Pending을 중복 생성하지 않는다. Repository 조사 전 내부 클래스명·컬럼명·SQL 형태는 확정하지 않는다.

## 12. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 자재 입고 현황 (기존 `/materials/receipts` 개편) | 기존 자재 메뉴 | 프로젝트별 품목 card/table: 주문 수량·단위, 누적/잔량(수량 없으면 `-`), 도착 상태 badge, 도착 건 목록·건별 상태·차단 표시, 다음 행동 | 도착 등록, `Arrived` 취소, 도착 마감(수량 도달 시 제안), IQC 요청·재요청, 입고 확정 | action 인접 성공/오류, 409 시 재조회 안내 |
| 도착 등록 입력 | 품목 card의 primary action (모바일 bottom sheet) | 수량(numeric input)·상속 단위, 도착일, 메모 | 저장/취소 | field 단위 한글 오류, 첫 오류 focus |
| IQC 대기 목록 (신규, 품질용) | 품질 메뉴, `/quality/iqc?request=…` deep link | 요청된 도착 건: 프로젝트·품목·수량·요청일·재검사 여부 | 적합/부적합·사유 판정 | 판정 결과 인접 표시, 부적합 시 Pending 링크 |
| Pending 상세 (기존) | 차단 attempt의 Pending 링크 | 기존 TASK-007A 화면 재사용 | 기존 조치·재검사 요청 흐름 | 기존 계약 유지 |

확인할 UX 항목:

- 품목 상태·누적/잔량·차단 여부가 목록에서 즉시 이해되는가?
- 각 상태에서 다음 행동(등록→요청→판정→확정→마감) 버튼이 하나로 명확한가?
- 저장·판정 결과가 해당 도착 건 근처에 보이는가?
- 권한 없음(품질 판정 vs 자재 mutation)·차단 상태·version 충돌이 구분되어 안내되는가?
- 390px·Teams narrow에서 표 축소가 아닌 card 전환, 단계 indicator, 44px action, numeric input, sticky primary action, page-level horizontal overflow 0을 지키는가?

Frontend는 기존 수동 router, `LoadState`·`StateMessage`·공통 Action Feedback 패턴(중복 submit 차단, 첫 오류 focus, `aria-live`)을 재사용한다.

## 13. 포함·제외 범위

### 포함

- 구매품목 nullable 주문 수량·단위 pair와 수기 입력
- 도착 건 등록·`Arrived` 취소·도착 마감, 누적/잔량·품목 derived 상태
- 도착 건별 IQC 요청·재요청과 최소 적합/부적합·사유 판정
- 부적합 Pending 자동 생성·재사용·원자적 종결과 attempt별 참조
- 도착 건 단위 입고 확정과 `receipt_completed` derived 전환·전체 writer 차단
- IQC·입고 확정 내 업무와 deep link (인앱 원본만)
- `0030` additive migration과 legacy backfill
- Desktop 관리형 화면, 품질 IQC 대기 화면, 390px 모바일 card·bottom sheet

### 명시적 제외

- 상세 IQC 체크리스트·필수 사진·판정 UI·PDF snapshot (`TASK-009A`)
- 키팅 완료·제조 내 업무 생성 (`TASK-010A`)
- 사급 자재 (`TASK-008B`)
- 구매·자재 Excel import/export 계약 변경 (주문 수량·단위 열 포함) — 단, 기존 Excel의 입고 완료 변경 시도 차단은 포함
- reverse transition 전체: in-place 수량 수정, IQC 요청 후 취소, 입고 확정 취소, 도착 마감 해제
- 프로젝트 대표 단계·진행률 공식 재설계 (기존 호환 projection과 work item sync까지만, TASK-007B 병목 계약 불변)
- 평균 리드타임·lot/vendor 분석·bulk 도착 등록·barcode/QR
- 실제 Teams/Mail/Activity provider 발송, Persistent UAT write, runtime handover
- 대표 repo·GitHub `main`의 push·PR·merge

## 14. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated DB·synthetic 데이터만 사용한다.
- migration: `0030` additive 1건. 기존 migration 불변, rollback은 forward-fix 문서화, 실 DB 적용은 별도 사용자 승인.
- 외부 발송/실제 데이터 영향: 없음. 인앱 원본만 생성한다.
- runtime 교체: 없음. experiment branch 내 검증만 수행한다.
- 추가 사용자 승인 필요 작업: 실 DB migration 적용, 대표 repo 반영(push·PR·merge), Persistent UAT handover.

## 15. 검증 계획

- 최소 테스트: Backend Release build, 신규 store filtered tests(수량·pair invariant 검증, 상태 전이·gate, Pending 생성·재사용·원자 종결, idempotency, 동시성 409), migration catalog + fresh/existing isolated apply + backfill 정합(완료 품목 boolean 회귀 0).
- 영향 영역 회귀: 구매 목록·dashboard·Excel preview/apply의 `receiptCompleted` 소비처와 writer 차단, workflow·내 업무·알림 filtered tests, 권한 allow/deny matrix(자재/품질/구매/Read-only/System Administrator), 기존 Pending·구매·Excel E2E 회귀.
- PR/CI: 대표 repo PR 없음(실험 branch). Frontend lint/typecheck/unit/build와 isolated Full-Stack E2E(자재 흐름 신규 spec + 기존 spec)를 local에서 통과시킨다.
- 사용자 검수: desktop·390px에서 자재 목록, 도착 등록, IQC 요청/판정, 부적합 차단·Pending 연결, 재검사 재개, 입고 확정을 페이지별 synthetic screenshot으로 확인한다. 자동 검증 완료와 사용자 검수 완료는 별도 상태로 기록한다.

## 16. 권장 구현 순서

1. `0030` schema·backfill·DB constraint와 migration tests
2. Materials contracts·store·transaction-aware Pending helper
3. Materials/IQC endpoints·authorization·work item/notification 연결
4. 기존 receipt boolean writer 차단과 호환 read projection 회귀
5. Frontend API type·자재 화면·IQC 화면·deep link
6. Backend targeted/전체, Frontend unit/build, isolated E2E
7. desktop·390px screenshot, implementation report와 독립 검증

## 17. 완료 기준과 중단 조건

- 기능/권한/데이터: 시나리오 A~C가 서버 authoritative로 동작하고, 6~10장의 불변조건 위반 시도(초과 수량, gate 우회, 차단 건 확정, 권한 우회, 중복 생성, direct write)가 모두 차단·테스트된다.
- UX: 12장 확인 항목과 390px overflow 0을 충족한다.
- 자동 테스트: 15장 최소·영향 테스트 전체 PASS, 미실행 항목은 이유와 함께 기록한다.
- 산출물: implementation report에 5종 산출물 상태·위치를 추적한다.
- 사용자 검수 상태: `사용자 검수 대기`로 handoff하고 완료로 표기하지 않는다.
- PR 상태: N/A — 실험 branch local commit까지가 범위다.

중단 조건: 기존 migration·Pending·workflow 계약과 additive로 해소할 수 없는 충돌, Roadmap·문서와 구현의 의미 있는 충돌, backfill이 기존 완료 품목의 업무 의미를 바꾸는 경우, writer inventory에서 차단 불가능한 경로 발견 — 임의 선택하지 않고 보고 후 중단한다.

## 18. 미결정·deferred 사항

Blocking 결정은 없다. 아래는 명시적으로 deferred된 비차단 항목이다.

| 번호 | 항목 | 결정 시점 |
| ---: | --- | --- |
| 1 | 구매 Excel의 주문 수량·단위 열 추가 여부·시점 | 후속 Task 결정 대기 |
| 2 | 상세 IQC 성적서·사진·PDF 계약 | `TASK-009A` planning |
| 3 | 키팅(`TASK-010A`)·사급(`TASK-008B`)·실제 provider·Persistent UAT 적용 시점 | 각 Task 승인 시점 |

1차 기획의 미결정 1번(부적합 Pending 초기 담당)은 review에서 `Registered`·`Urgent`·미배정으로 채택되어 종결되었다.

## 19. Roadmap 연결

- 선행 Task: TASK-007A Pending List — 이 실험 branch에 구현 완료(canonical 미반영), 구매정보(TASK-004A 계열) 완료.
- 후속 Task: `TASK-008B`(이번 데이터 모델 재사용), `TASK-009A`(최소 판정 계약을 상세 IQC로 대체·확장), `TASK-010A`(입고 확정을 키팅 전제 조건으로 사용). `TASK-008A`와 `TASK-010A`는 Decision Log에 따라 별도 planning·구현·rollback 단위로 유지한다.
- Go/No-Go: `tasks/008a-review.md`는 이 2차 기획을 구현 source of truth로 사용하는 조건으로 실험 branch 구현 `GO`를 판정했다. 이는 대표 repo, push, PR, merge, Persistent UAT 승인이 아니다.

## 20. Codex 구현 지시문

이 지시문은 `tasks/008a-change-001.md`의 승인 경계 안에서 새 Codex 구현 세션이 사용할 계약 요약이다.

1. instruction chain gate를 수행하고 `taskType: APPROVED_FEATURE_IMPLEMENTATION`, branch 기준선을 보고한다.
2. `0030` additive migration 1건으로 6.3~6.4장의 schema·constraint·backfill을 구현한다. 기존 migration은 변경하지 않는다.
3. Backend: 6~11장의 상태 모델·pair invariant·gate·transaction owner·transaction-aware Pending helper·idempotency를 구현하고, 자재 mutation은 기존 `MaterialReceiptUpdate` policy, IQC 조회·판정은 `quality.inspect` 기반 신규 policy로 연결한다. 모든 기존 `receipt_completed` writer를 validation error로 차단하고 호환 read projection을 유지한다.
4. Frontend: `/materials/receipts` 상태 기반 개편, 품질 IQC 대기 화면, 업무 deep link를 desktop·390px에서 구현하고 기존 LoadState·Action Feedback 패턴을 재사용한다.
5. 검증: 15장 계획을 실행하고 실행/미실행을 분리해 implementation report에 기록한다. Persistent UAT write, 실제 provider 발송, 대표 repo 게시를 수행하지 않는다.
6. 18장 deferred 항목에 대한 임의 결정을 하지 않고, 13장 제외 범위(특히 reverse transition)를 추가하지 않는다.

---

- primaryDraftStatus: `DRAFT_FOR_USER_REVIEW`
- sourceTask: `TASK-008A`
- authoringModel: `FABLE_5`
- openBlockingDecisionCount: 0
- deferredUserDecisionCount: 3
- 이 문서는 게시·구현 승인을 스스로 부여하지 않는다. 승인 값은 `tasks/008a-change-001.md`를 따른다.
