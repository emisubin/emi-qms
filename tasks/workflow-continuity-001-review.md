# TASK-WORKFLOW-CONTINUITY-001 — Fable 1차 기획 Codex 내용 Review

- reviewTarget: `tasks/workflow-continuity-001-planning.md`
- reviewOwner: `CODEX`
- reviewStatus: `COMPLETED`
- reviewedAt: `2026-07-21`
- openBlockingDecisionCount: 0
- recommendedDisposition: `REVISE_ONCE_IN_FABLE_SECOND_PLANNING_THEN_IMPLEMENT`

## 1. 총평

Fable 1차 기획은 사용자가 실제 입력 중 겪은 핵심 실패를 정확히 짚었다. 특히 `Pending 조치 완료 = 재검사 handoff`, `검사 연계 Pending 종결 = 재검사 합격 transaction에서만`, 설계·구매의 병행 활성화와 stage 완료 순서 분리, 자재 도착과 IQC 요청의 원자 결합은 유지해야 한다. 이 네 계약이 이번 작업의 사용자 가치다.

다만 구매 완료 조건이 현재 구매 입력 계약과 충돌하고, QR batch의 부분 성공안이 기존 선택 인쇄의 all-or-nothing 안전 계약과 달라 사용자에게 혼합 결과를 만든다. IQC deep link query 이름도 현재 생성되는 `request`와 달리 `attempt`로 적혔다. 아래 resolution을 Fable 2차 기획에 반영하면 blocking decision은 0이며 구현할 수 있다.

## 2. 기능별 판단

| 기능 | 판단 | Review resolution |
| --- | --- | --- |
| 프로젝트 기본 `전체 흐름` | 유지 | `initialSection` fallback만 `workflow`로 바꾸고 explicit section query는 보존한다. |
| 생산계획 후 설계·구매 병행 업무 | 유지 | stage event/완료 순서는 그대로 두고 구매 `Requested` work item·정/부 알림만 설계와 함께 멱등 생성한다. |
| 구매 완료 조건 강화 | 수정 | 아래 3장의 실제 저장 계약에 맞춘다. 이미 Completed인 stage는 회귀시키지 않는다. |
| 자재 `입고 관리`·`키팅 관리` 하위 탭 | 유지 | 프로젝트 상세의 한 자재 탭 안에서 읽기/담당 수정 route를 분리하고, 모바일은 각 하위 탭별 compact card를 사용한다. |
| 도착→IQC 자동 요청 | 유지 | `RegisterArrivalAsync`의 기존 transaction 안에서 상세 IQC attempt·work item·정/부 알림·event까지 만든다. 기존 `RequestIqcAsync`는 과거 Arrived row 복구용으로만 유지한다. |
| IQC 내 업무 deep link | 유지·정정 | Backend와 현재 알림이 이미 쓰는 `/quality/iqc?request={attemptId}`를 canonical로 사용한다. Frontend route/parser/navigation이 `request`를 보존하고 해당 attempt를 선택한다. |
| 부적합 사진/근거 gate | 유지 | 신규 저장소 없이 최종화 시 `사진 ≥1` 또는 충분한 구체 사유를 서버가 검증한다. 상세 IQC·panel 검사와 신규 legacy 판정 모두 적용하되 과거 finalized snapshot은 변경하지 않는다. |
| Pending 조치 UI 단순화 | 유지 | 검사 연계 Pending만 `조치 시작`, 처리 내용, `조치 완료`로 제한한다. 일반 수동 Pending의 기존 lifecycle은 보존한다. |
| 조치 완료 자동 재검사 handoff | 유지 | Pending 상태, attempt/report, work item, 정/부 알림, history를 하나의 DB transaction에 묶는다. Frontend 순차 API 호출은 금지한다. |
| 재검사 부적합 반복 | 유지 | 새 Pending을 만들지 않고 같은 Pending을 내부 전용으로 `InProgress`로 돌리고 Pending 업무도 다시 `InProgress`로 연다. 사용자용 임의 역전이는 노출하지 않는다. |
| comments+history timeline | 유지 | 저장 원장은 그대로 두고 detail 응답 또는 Frontend projection에서 UTC timestamp·stable tie-breaker로 하나의 시간순 timeline을 만든다. 별도 comments 섹션은 제거한다. |
| 전체 흐름 상태 정합성 | 유지 | IQC는 receipt/attempt 최신 facts, panel 품질은 active panel별 최신 attempt와 StageCompleted event를 함께 검증한다. Closed Pending 자체를 합격 근거로 사용하지 않는다. |
| QR 선택 batch 발급 | 수정 | 아래 4장의 all-or-nothing 사전 검증과 멱등 처리로 확정한다. |
| QR 행 inline preview | 유지 | 선택 행 바로 다음 sibling에 렌더하고 `aria-expanded/controls`, 닫기 focus 복귀와 object URL 해제를 검증한다. |

## 3. 구매 완료 조건 Resolution

Fable 1차의 “모든 활성 품목 수량+단위 필수”는 현재 코드와 충돌한다. `ProcurementStore`는 일반 구매품(`Purchased`)의 발주 수량·단위를 구매 화면에서 입력하면 validation으로 거부하고, 첫 도착 등록에서 입력하도록 확정돼 있다. 따라서 구매 완료를 수량·단위에 묶으면 모든 일반 구매 프로젝트가 구매 단계에서 영구 차단된다.

권장 완료 조건은 다음 AND다.

1. 활성 구매품목이 1개 이상이다.
2. 모든 활성 품목 공통: 발주품목명, 유효한 공급구분, 입고예정일이 있다.
3. 일반 구매품(`Purchased`): 업체명과 발주일이 있다. 수량·단위는 이 단계에서 요구하지 않는다.
4. 사급품(`CustomerSupplied`): 제공 예정 수량>0과 단위가 있다. 업체명·발주일은 요구하지 않는다.
5. 해당 ITEM code의 required template가 있으면 모든 required row가 active confirmed 품목과 match한다.
6. 자재 도착·IQC·입고 확정은 후속 부서 단계이므로 구매 완료 조건에 포함하지 않는다.
7. 이미 `Completed`인 procurement stage/event는 새로운 계산으로 회귀시키지 않는다.

이 조건은 구매가 제공해야 할 handoff data를 보장하면서 현재 사급/일반 구매 입력 책임을 침범하지 않는다.

## 4. QR batch Resolution

Fable 1차의 패널별 부분 성공 응답은 사용자가 “발급 가능한 패널만 선택해 한 번에 발급”하려는 목표에 비해 운영 확인 비용이 크다. 기존 QR 인쇄판도 선택 전체를 서버가 재검증하고 하나라도 stale이면 전체 실패한다. 같은 안전 모델을 발급에 적용한다.

- 요청: 같은 project의 distinct panel 1~50개.
- 서버 사전 검증: project/panel active, panel name 존재, 요청 count 일치, project scope와 issue 권한.
- 이미 활성 QR이 있는 패널은 idempotent success로 인정하고 새 token을 만들지 않는다.
- 검증 실패가 하나라도 있으면 발급 0건으로 rollback하고 선택 수·현재 가능 수를 안내한다.
- 모두 유효하면 아직 미발급인 패널을 한 transaction에서 발급하고 기존/신규 수를 응답한다.
- Frontend 선택 집합은 `qrEligible || hasActiveQr`로 구성하되 primary action은 미발급 eligible가 포함되면 `선택 QR 발급`, 전부 발급된 선택이면 `선택 인쇄판`이다. 두 action이 같은 의미로 동시에 보이지 않게 한다.

## 5. 재검사 Transaction Resolution

순환 DI를 만들지 않기 위해 Frontend orchestration이나 별도 commit 두 번을 사용하지 않는다. `PendingStore.TransitionAsync`가 보유한 connection/transaction 안에서 material/panel별 내부 helper를 호출한다. helper는 기존 store의 static/internal SQL 경로를 재사용할 수 있다.

### Material IQC

- linked failed attempt/receipt를 `FOR UPDATE`하고 receipt가 `FailedBlocked`, Pending이 `InProgress`인지 확인한다.
- 다음 attempt number의 Detailed attempt, IQC work item, 정담당 배정 알림, 부담당 참조 알림, receipt `IqcRequested`, material event를 만든다.
- work item/notification은 결정적 idempotency key로 중복 0.
- 과거 `FailedBlocked + ReinspectionRequested` row는 기존 endpoint가 같은 helper를 멱등 호출하는 복구 경로로 유지한다.

### Panel quality

- linked failed attempt와 active template를 잠그고 다음 attempt/report를 만든다.
- work item은 호출자가 아니라 stage별 quality primary/fallback 담당자에게 `Requested`로 배정하고 secondary는 참조 알림을 받는다.
- 기존 `RequestReinspectionAsync`는 같은 helper를 호출하는 멱등 legacy 경로로 바꾸며 별도 두 번째 attempt를 만들지 않는다.

### 반복 부적합·합격

- 재검사 부적합: attempt/work item 완료, 동일 Pending을 내부 전용 `InProgress`로 복귀, Pending work item 재개, history에 검사 회차·사유 기록.
- 재검사 합격: 기존 close helper를 통해 attempt/receipt 또는 panel stage/Pending/work item/workflow event를 같은 transaction에서 완료.
- material과 panel 모두 일반 `Closed` transition은 항상 차단한다. `IsQualityInspectionPendingAsync`는 두 attempt table의 linked pending을 검사한다.

## 6. 증빙·UX Resolution

- `Failed` 최종화는 사진 1장 이상 또는 trim한 판정 근거 30자 이상 중 하나를 요구한다. 사용자가 입력한 단순 결과명·반복 문자는 Frontend hint로 막되 Backend 핵심 계약은 길이·snapshot 존재다.
- template item별 `requires_photo`는 별도 AND 조건으로 유지한다. 즉 특정 항목이 사진 필수이면 긴 text로 대체할 수 없다.
- Pending의 처리 내용은 status transition reason에 저장해 timeline에 바로 나타난다. 일반 협업 comment 입력은 timeline composer로 유지하되 별도 카드로 분리하지 않는다.
- `조치 완료` label이 곧 `재검사 요청` 상태임을 보조 설명하고, 성공 feedback은 실제 생성된 검사 업무를 기준으로 표시한다.

## 7. 추가·보류·제거

### 추가

- 자동 handoff가 quality primary/secondary를 찾지 못하면 Pending 전이도 rollback하고 담당자 설정 안내를 반환한다. 알림 없는 재검사 상태를 만들지 않는다.
- 도착 자동 IQC에서 품질 담당 부재 시 도착 자체를 저장할지 여부는 부분 상태를 만들 수 있으므로 rollback한다. 생산계획의 필수 정담당 계약상 정상 프로젝트에서는 발생하지 않아야 한다.
- batch QR response와 UI에는 `requested/newlyIssued/alreadyIssued`만 표시한다. 실패 시 rollback이므로 partial 실패 count는 없다.
- lifecycle E2E는 material IQC와 panel 품질 각각 부적합→조치→자동 재검사→합격을 한 번씩 검증한다.

### 보류

- timeline filter chip: 항목 수가 실제로 많다는 사용자 검수 근거 전까지 보류한다.
- 이미 잘못 Closed된 운영 IQC Pending 자동 backfill/재개 action: experiment fixture 복구와 문서화만 허용하고 운영 mutation은 별도 UAT/정책 Task로 분리한다.
- 실제 Teams/Mail 발송: 기존 outbox 생성과 provider 0건 경계를 유지한다.

### 제거

- Frontend의 일반 `IQC 요청` 버튼과 정상 흐름의 `재검사 요청` 버튼.
- 검사 연계 Pending의 `종결` action.
- QR batch의 부분 성공·실패 혼합 결과.
- 구매 완료에서 일반 구매품 수량·단위를 요구하는 안.
- `/quality/iqc?attempt=` 신규 query alias. 기존 `request` 하나를 canonical로 유지한다.

## 8. 변경 번호 Bookkeeping Resolution

Fable이 지적한 번호 충돌을 인정한다. 다음 사용 가능 번호로 정정한다.

- `TASK-007A Change 001`
- `TASK-008A Change 002`
- `TASK-009A Change 002`
- `TASK-012A Change 002`
- `TASK-QR-001 Change 002`
- `TASK-E2E-FULL-SUITE-001 Change 007`

## 9. 권장 구현 순서

1. 재현 tests: material IQC Pending 수동 Closed 우회, 재검사 알림/내 업무 누락, IQC deep link fallback, workflow 회귀를 먼저 실패로 고정.
2. Backend 종결 보호와 material/panel 재검사 transaction helper, 반복 부적합·합격 정합성.
3. 도착→IQC 자동 transaction과 legacy endpoint 호환.
4. 품질 증빙 gate와 deep link.
5. 생산계획 설계·구매 병행 활성화와 구매 완료 facts.
6. QR all-or-nothing batch API.
7. Frontend 기본 탭, 자재 하위 탭, Pending action/timeline, IQC deep link, QR 선택·inline preview.
8. targeted → 전체 Backend/Frontend → isolated lifecycle/QR E2E → desktop/390px screenshot.

## 10. Go/No-Go

- P0/P1/P2 open: 0 — 위 resolution을 2차 기획이 반영하는 조건.
- blocking decision: 0.
- 권장: `GO_FABLE_SECOND_PLANNING`.
- 구현 source of truth: Fable 2차 기획 `docs/37-workflow-continuity-plan.md`.
