Baseline verification is complete and all seven interview decisions are recorded as non-blocking, so round 2 is the confirmation summary. Below is the round 2 artifact.

---

# TASK-008A — Deep Interview Round 2 (Fable 5)

## Repository 기준선 재확인

누적 interview(`tasks/008a-interview.md`)의 결정 1~7을 현재 branch 구현과 표적 대조해 다음을 확인했다.

- 구매품목 model(`backend/src/Emi.Qms.Api/Procurement/ProcurementDomain.cs`의 `ProcurementItemSnapshot`)에는 여전히 주문 수량·단위 필드가 없고 `ReceiptCompleted` boolean·완료일시·완료자·비고만 있다. 결정 1(A: nullable 주문 수량·단위 additive 추가)과 결정 5(A: legacy backfill 후 boolean derived 유지)의 전제가 유효하다.
- `/api/materials/receipts`는 GET 목록과 bulk PATCH(`ProcurementReceiptBulkUpdateRequest`, `ExpectedRowVersion` 낙관적 잠금)만 제공한다. 결정 5의 "기존 boolean 직접 편집 경로를 신규 상태 mutation으로 대체" 범위가 실제 계약과 일치한다.
- Round 1에서 blocking으로 기록했던 TASK-007A 미구현 전제는 해소됐다. 현재 branch에는 Pending List가 구현되어 있다(`backend/src/Emi.Qms.Api/Pending/`, migration `0029_pending_list_foundation.sql`, `tasks/007a-implementation-report.md`). `PendingStatuses`에 `Registered → ActionRequested → InProgress → ReinspectionRequested → Closed` forward-only 전이와 `Nonconformance` 유형, open Pending 집계, version·transaction·idempotency 보호가 존재하므로 결정 4(부적합 판정 transaction에서 Pending 자동 생성·도착 건 참조), 결정 6(Pending `ReinspectionRequested` 이상에서만 IQC 재요청 허용하는 단방향 gate), 결정 7(도착 건 단위 gate·건당 open Pending 최대 1·재사용)이 실제 계약 위에서 성립한다.
- 현재 Pending 엔터티는 프로젝트 단위이며 구매품목 참조 필드는 없다. 결정 4는 도착 건이 Pending을 참조하는 방향이므로 Pending schema 변경 없이 성립하고, 구매품목 연결 표시 방식은 planning의 설계 상세로 처리한다(비차단).
- Workflow에는 `MaterialArrived → IQC → ReceiptConfirmed → KittingCompleted` stage와 담당 responsibility가 이미 정의되어 있어 품목 derived 상태와 stage 완료 판정을 연결할 수 있다.
- Roadmap 12장(자재 4단계), 13장(IQC 최소 판정과 부적합 Pending), 15~16장(Pending 상태·재검사 요청 흐름), 23장 TASK-008A 항목(분할 입고 일반화, IQC 요청, 입고 확정, migration 별도 승인)과 interview 범위가 충돌하지 않는다. TASK-007A 선행 의존성은 이 branch의 구현으로 충족되고, 순서 재정렬은 Task Identity Gate에 `explicitRoadmapOverrideApproved: true`로 기록되어 있다.

새로운 blocking 질문은 없다. 아래 확인용 요약을 제시한다.

## 확인용 요약

### 해결할 업무 문제

자재 담당자가 구매품목 입고를 단일 완료 boolean·비고로만 기록해 분할 도착, 누적/잔량, IQC 요청, 부적합 차단과 적합품 입고 확정을 추적할 수 없다. 이번 Task는 Roadmap 18단계 중 5(자재 도착)·6(수입검사 최소 판정)·7(입고 확정)을 도착 건 단위 이력과 상태 계약으로 구현한다.

### 확정한 정책 (결정 1~7)

1. **기준 수량**: 구매품목에 nullable 주문 수량·단위를 additive로 추가한다. 값이 있으면 초과를 서버에서 차단하고 잔량을 표시하며, 없으면 검증을 skip하고 잔량 `-`로 표시한다. 구매 Excel 열 추가는 제외한다.
2. **도착·IQC 단위**: 도착 건(분할분) 단위로 등록·IQC 요청·입고 확정을 수행한다. 품목 상태(미도착/부분도착/도착완료)는 도착 건들의 derived 값과 자재의 명시적 도착 마감 선언으로 계산한다.
3. **최소 IQC 결과**: 품질 IQC 담당이 요청 건당 적합/부적합과 사유만 기록한다. 상세 체크리스트·사진·PDF는 TASK-009A가 이 계약을 대체·확장한다.
4. **부적합 차단**: 부적합 판정 transaction에서 해당 구매품목 대상 Pending(`Nonconformance`)을 자동 생성하고 도착 건이 이를 참조하며 Blocked 상태가 된다.
5. **legacy 이행**: `receipt_completed=true` 품목은 수량 null의 legacy 표시 도착·확정 이력 1건으로 backfill하고, boolean은 신규 상태에서 derive하는 호환 필드로 유지한다. 기존 bulk PATCH의 boolean 직접 편집은 신규 상태 mutation으로 대체하고 정정은 사유 필수 event로 처리한다.
6. **재검사 재개**: 연결된 Pending이 `ReinspectionRequested` 이상일 때만 차단 도착 건의 IQC 재요청을 허용하는 단방향 gate를 적용한다. Pending 상태 변경이 자재 상태를 자동으로 바꾸지 않는다.
7. **확정 gate**: 입고 확정은 도착 건 단위로 차단·허용하고(적합 분할분은 확정 가능), 도착 건당 open Pending은 최대 1건으로 재사용한다.

### 흐름·권한·audit

- 정상 흐름: 품목 선택 → 도착 수량·일자·메모 등록 → 누적/잔량 재계산 → 도착 건별 IQC 요청 → 품질 적합/부적합 판정 → 적합 건 입고 확정, 부적합 건 Pending 차단.
- 서버 차단: 0 이하 수량, 주문 수량 초과(수량이 있는 품목), 미래 도착일, 필수 사유 누락, 허용되지 않은 상태 전이. 경쟁 mutation은 transaction·version/idempotency로 보호한다.
- 권한: 자재(도착·마감·확정), 품질 IQC(판정), 구매·생산관리(조회), Read-only·System Administrator(업무 mutation 우회 금지)를 서버 authoritative로 유지한다.
- 이력: 도착·정정·판정·확정·상태 전이는 append-only audit이며 hard delete하지 않고 사유 필수 정정/취소 event로 복구한다.

### Data·migration·UX·검증

- Migration: additive 신규 테이블(도착 이력·IQC 요청·상태)과 구매품목 nullable 수량·단위 컬럼, legacy backfill. 기존 migration 번호·내용 불변, forward-fix 원칙, Persistent UAT 미적용.
- UX: `/materials/receipts`에 품목별 상태·누적/잔량·다음 행동을 표시하고, 모바일(390px·Teams narrow)은 card·단계 indicator·44px action·numeric input·sticky primary action으로 전환한다.
- 검증: Backend build·targeted/전체 tests, migration fresh/existing isolated apply, Frontend lint/typecheck/unit/build, isolated E2E, desktop·390px 페이지별 synthetic screenshot. 실제 provider 발송과 Persistent UAT write는 없다.

### 명시적 제외

상세 IQC 체크리스트·사진·PDF(TASK-009A), 키팅·제조 업무 생성(TASK-010A), 사급 자재(TASK-008B), 자재/구매 Excel import·export, 실제 provider 발송, Persistent UAT, 대표 repo·GitHub `main` 반영.

### Deferred 비차단 결정 (planning 사용자 결정 항목으로 전달)

- 구매 Excel의 주문 수량·단위 열 추가 시점
- 상세 IQC 성적서·사진·PDF 계약(TASK-009A)
- 키팅·사급·실제 provider·Persistent UAT 적용 시점

## 사용자 확인 요청

위 요약이 정확하면 `tasks/008a-interview.md` 10장의 확인 항목을 체크하고 `interviewStatus: COMPLETED_CONFIRMED`, `userConfirmed: true`로 기록해 달라. 확인 후에만 planning을 시작한다.

---

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
