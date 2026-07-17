# TASK-008A — Deep Interview Round 1 (Fable 5)

## Repository 기준선 확인

interview 문서(`tasks/008a-interview.md`)와 현재 구현을 대조해 다음을 확인했다.

- 현재 `/api/materials/receipts`는 GET 목록과 bulk PATCH만 제공하며, PATCH는 `ReceiptCompleted` boolean, 완료일시, 완료 비고, `ExpectedRowVersion` 낙관적 잠금만 다룬다 (`backend/src/Emi.Qms.Api/Procurement/ProcurementEndpointExtensions.cs`, `ProcurementStore.UpdateMaterialReceiptsAsync`).
- 구매품목 model에는 **주문 수량·단위 필드가 존재하지 않는다** (`backend/src/Emi.Qms.Api/Procurement/ProcurementDomain.cs`의 `ProcurementItemSnapshot`, TASK-004A 문서의 구매 입력항목). interview 4장의 "기존 구매품목과 주문 수량"은 현재 구현과 충돌하므로 blocking decision으로 기록한다.
- workflow에는 `MaterialArrived → IQC → ReceiptConfirmed → KittingCompleted` stage와 담당 responsibility가 이미 정의되어 있다 (`backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`).
- Roadmap 12장은 자재 흐름 4단계(도착→IQC 요청→입고 확정→키팅)를 확정했고, 상세 IQC 성적서는 `TASK-009A`, Pending List 공통 모듈은 `TASK-007A`로 분리되어 있으며 두 Task 모두 미구현이다. 이 실험은 `explicitRoadmapOverrideApproved: true`로 순서 재정렬이 기록되어 있다.

아래 5개 질문은 모두 이번 Task의 상태·수량 계약을 확정하는 서로 연결된 blocking 결정이다.

### 질문 1 — 잔량·초과 검증의 기준이 되는 주문 수량을 어디서 얻는가

- 필요한 이유: interview 3장은 "주문 수량 초과" 서버 차단과 누적/잔량 재계산을 요구하지만, 현재 구매품목에는 수량·단위 필드가 없어 기준값이 존재하지 않는다.
- 답변이 바꾸는 범위: DB migration 범위(구매품목 테이블 확장 여부), 구매 화면·Excel import 영향 여부, 서버 validation 규칙, 잔량 표시 UX.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 구매품목에 nullable 주문 수량·단위를 additive로 추가 | 구매(또는 자재)가 직접 입력 화면에서 수기 입력. 값이 없으면 초과 검증 skip, 잔량 `-` 표시 | 잔량·초과 차단이 서버 authoritative로 성립. 기존 데이터·Excel 계약 불변(수기 입력만) | 구매품목 테이블 migration과 구매 직접 입력 화면 소폭 확장 필요 |
| B. 주문 수량 없이 도착 수량 누적만 기록 | 잔량·초과 검증 없이 도착 이력·누적만 표시 | migration·구매 화면 영향 최소 | interview가 요구한 초과 차단·잔량 추적 불가. TASK-008B(사급 잔량)의 기반 약화 |
| C. 자재가 도착 등록 시점에 품목별 기준 수량을 별도 입력 | 신규 자재 상태 테이블에 기준 수량 저장 | 구매 화면 무변경 | 주문 정보의 주인이 구매인데 자재가 이중 입력. 구매 정보와 불일치 위험 |

- 권장안: **A**. nullable additive 컬럼이므로 기존 구매정보·Excel 계약을 깨지 않고, 수량이 입력된 품목부터 잔량·초과 차단이 점진 적용된다. 구매 Excel 열 추가는 이번 Task에서 제외하고 수기 입력만 포함한다.

### 질문 2 — 도착 등록과 IQC 요청의 단위, '도착완료' 판정 방식은 무엇인가

- 필요한 이유: 분할 도착에서 IQC 요청을 "도착 건(lot) 단위"로 할지 "품목 누적 단위"로 할지에 따라 데이터 모델(도착 이력 ↔ IQC 요청의 관계)과 상태 전이가 달라진다. 또한 주문 수량이 null일 수 있으므로(질문 1) '도착완료' 자동 판정이 항상 가능하지 않다.
- 답변이 바꾸는 범위: 도착 이력·IQC 요청 테이블 관계, 품목 상태 전이(미도착/부분도착/도착완료), MaterialArrived stage 완료 판정, 모바일 화면의 행동 단위.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 도착 건 단위 IQC 요청 + 품목 '도착 마감'은 자재의 명시적 선언 | 각 도착 등록 건마다 IQC 요청 가능. 주문 수량이 있으면 누적 도달 시 마감을 제안하되 확정은 자재가 선언 | 부분 도착분 선검사·선입고가 가능해 실무 흐름과 일치. 수량 null 품목도 마감 가능 | 도착 건별 상태 관리로 모델 복잡도 증가 |
| B. 품목 단위 IQC 요청 (도착 마감 후 1회) | 품목 도착이 끝난 뒤 품목 전체를 한 번에 IQC 요청 | 모델 단순 | 부분 도착분을 먼저 검사·입고할 수 없어 분할 입고 일반화 목적과 충돌 |
| C. 누적 수량 기준 자동 도착완료 | 누적 ≥ 주문 수량이면 자동 도착완료 | 입력 최소화 | 주문 수량 null이면 판정 불가. 초과·오입력 시 자동 전이가 오히려 정정 부담 |

- 권장안: **A**. Roadmap 5~7단계는 구매품목 단위 업무이지만 분할 입고 일반화가 이번 Task의 핵심이므로 도착 건 단위 IQC 요청·입고 확정을 지원하고, 품목 수준 상태(미도착/부분도착/도착완료)는 도착 건들의 derived + 자재의 명시적 마감으로 계산한다.

### 질문 3 — TASK-009A 이전에 IQC 적합/부적합 결과는 누가 어떻게 기록하는가

- 필요한 이유: 입고 확정 gate는 "IQC 적합"을 요구하지만 상세 IQC 체크리스트·사진·판정 UI는 `TASK-009A`로 제외됐다. 이번 Task에서 적합/부적합을 기록할 최소 계약이 없으면 입고 확정 gate를 서버에서 강제할 수 없다.
- 답변이 바꾸는 범위: 품질 담당의 이번 Task mutation 권한 여부, IQC 요청→결과 데이터 계약, 입고 확정 서버 validation, TASK-009A가 대체할 인터페이스 경계.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 최소 IQC 결과 기록 포함 | 품질 IQC 담당이 요청 건에 적합/부적합 + 사유만 기록 (체크리스트·사진 없음). TASK-009A가 이 결과 기록을 상세 성적서로 대체 | 입고 확정 gate가 서버 authoritative로 성립. 18단계 6번(품질) 담당 경계 보존 | 품질 화면 최소 1개 action 추가로 범위 소폭 확대 |
| B. 자재가 'IQC 적합 확인' 체크로 대리 기록 | 품질 mutation 없이 자재가 적합 여부를 입력 | 이번 Task 범위 최소 | 검사 주체(품질)와 기록 주체(자재)가 분리돼 감사·권한 원칙 훼손. TASK-009A 전환 시 데이터 의미 재해석 필요 |
| C. IQC gate 자체를 TASK-009A로 연기 | 이번 Task는 도착 등록·마감까지만, 입고 확정은 도착만 요구 | 가장 작음 | Roadmap 5~7단계 중 6~7단계가 빠져 Task 목적(입고 확정 gate) 미달 |

- 권장안: **A**. 결과 기록을 "요청 건당 적합/부적합/사유"의 최소 계약으로 한정하면 TASK-009A와 중복되지 않고, 부적합 차단과 입고 확정 gate를 서버에서 강제할 수 있다.

### 질문 4 — TASK-007A Pending List가 없는 상태에서 부적합 차단을 어떻게 표현하는가

- 필요한 이유: Roadmap은 부적합품을 Pending List로 연결하도록 확정했지만 Pending 공통 모듈은 미구현이고 이번 실험은 순서를 재정렬해 먼저 진행한다. 부적합 차단 상태의 소유 위치를 정해야 상태 모델과 후속 TASK-007A 연결 계약이 확정된다.
- 답변이 바꾸는 범위: 품목·도착 건 상태 enum(부적합 차단 포함 여부), 재검사 요청 흐름의 이번 Task 포함 여부, TASK-007A와의 인터페이스.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 자재 상태 모델 안에 '부적합 차단' 상태만 보유, Pending 연동은 연결점만 설계 | 부적합 결과 시 해당 건이 Blocked 상태가 되어 입고 확정 불가. Pending record 생성은 TASK-007A 이후 연결 | TASK-007A 범위 중복 없음. forward-only 차단 불변조건 보존 | 조치·재검사 요청 흐름은 이번 Task에서 화면 없이 상태 정정(사유 필수)으로만 처리 |
| B. 최소 Pending record를 이번 Task에서 선구현 | 부적합 시 간이 Pending 엔터티 생성 | 차단·조치 추적이 즉시 가능 | TASK-007A 공통 모듈과 이중 구현·migration 충돌 위험. Roadmap 결정(별도 Task) 위반 소지 |

- 권장안: **A**. 부적합 차단은 이번 Task의 상태로 소유하고, Pending 유형·조치 담당 등 공통 모듈 요소는 TASK-007A 계약으로 남긴다. 재검사 요청(부적합 조치 흐름 16장)은 명시적 제외로 기록한다.

### 질문 5 — 기존 `receipt_completed` 데이터와 기존 입고 완료 편집 경로를 어떻게 이행하는가

- 필요한 이유: 기존 품목에는 boolean 완료값·완료일·비고만 있고, 구매 화면과 `/materials/receipts` bulk PATCH가 지금도 이 값을 수정한다. 신규 상태 모델과 구 boolean이 공존하면 두 개의 진실이 생긴다.
- 답변이 바꾸는 범위: migration 매핑 규칙, 기존 bulk PATCH API·구매 화면 입고 완료 열의 유지/차단 여부, 기존 audit·workflow 판정의 회귀 영향.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. legacy 확정 매핑 + 신규 상태를 단일 진실로 전환 | `receipt_completed=true` 품목은 '입고 확정 완료'로 매핑하고 수량 null의 legacy 표시 도착·확정 이력 1건을 backfill. 이후 boolean은 신규 상태에서 derive하고 기존 편집 경로는 신규 흐름으로 대체 | 단일 진실 유지. 기존 완료 품목이 후속 키팅(TASK-010A) 기준으로 그대로 인정됨 | 기존 화면의 boolean 체크 UX 변경 필요. backfill migration 검증 필요 |
| B. 병행 유지 | 기존 boolean 경로 유지, 신규 상태는 새 등록부터 별도 관리 | 회귀 위험 최소 | 품목마다 두 상태가 충돌 가능(완료 boolean=true인데 신규 상태 미도착). 감사·인수인계 혼선이 목적 자체를 훼손 |
| C. 초기화 | 기존 완료값을 신규 상태로 매핑하지 않고 재입력 요구 | 매핑 로직 불필요 | 기존 업무 기록 사실상 소실. 이력 보존 원칙 위반 |

- 권장안: **A**. additive migration(신규 테이블 + backfill)으로 기존 컬럼·기존 migration을 변경하지 않고, `receipt_completed`는 derived/호환 필드로 유지해 기존 조회 계약(구매 dashboard 집계 등)의 회귀를 막는다. 구 bulk PATCH의 boolean 직접 편집은 신규 상태 mutation으로 대체하고 정정은 사유 필수 event로 처리한다.

---

- interviewStatus: QUESTIONS_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
