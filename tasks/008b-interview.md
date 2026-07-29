# TASK-008B — 사급 자재 제공·입고·잔량 추적 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- fastTrackMode: `EXPERIMENT_TWO_PASS`

이 문서는 Fable 5가 `TASK-008B`를 기획하기 위한 interview source of truth다. 사용자는 이 `experiment/*` branch에서 사용자-facing interview와 중간 승인 왕복 없이 Fable 권장안을 채택해 `1차 기획 → Codex review → review 기반 2차 기획 → Codex 구현·검증·screenshot·local commit`까지 진행하도록 명시했다. Fable은 아래 확정 기준과 실제 Repository를 바탕으로 비차단 정책의 권장안을 선택해 1차 기획에 명시한다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-008B`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-008B`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: `TASK-008A`의 구매품목·도착 건·분할 입고 데이터 모델을 재사용해 고객이 제공하는 사급 자재의 제공 예정량, 실제 입고량, 잔량과 이력을 프로젝트·구매품목 단위로 추적한다.
- Root Finding 또는 정책 결정: 현재 자재 흐름은 일반 구매품과 사급품을 구분하지 않아 누가 공급해야 하는지, 고객 제공 예정량 대비 얼마가 도착했고 얼마가 남았는지를 현장 화면과 감사 이력에서 명확히 설명할 수 없다.
- 변경·검증 경계: additive 사급 구분·정책·projection과 기존 008A 입고 흐름의 재사용, Backend 권한·transaction·migration, Frontend desktop·모바일 전용 UX, isolated DB·E2E를 검증한다. 외부 공급망·고객 포털과 008A 모델 재구현은 제외한다.
- 보존할 불변조건: 18단계 순서, 서버 권한 authoritative, `receipt_completed` 단일 진실, forward-only 도착·IQC·입고확정, Pending 차단, 기존 일반 구매품 동작·audit·migration 불변, Persistent UAT·실제 provider·대표 repo·GitHub `main` 무변경.
- 예상 산출물: Fable 1차 planning, Codex 내용 review, review 기반 Fable 2차 planning, 구현·자동 검증·desktop/mobile screenshot·implementation report와 local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자의 experiment fast-track 지시, Roadmap TASK-008B 범위와 완료된 TASK-008A 구현 계약을 확정 source로 기록. 비차단 정책은 Fable 권장안 자동 채택 | Fable 1차 planning 진행 |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 구매품목은 일반 구매와 고객 사급을 구분하지 않고 동일한 자재 도착·IQC·입고 확정 화면에서 추적한다.
- 해결할 문제: 사급품의 공급 책임, 고객 제공 예정량, 실제 누적 입고량과 잔량, 부족·초과와 제공 이력을 별도 의미로 확인할 수 없다.
- 현재 우회 방식: 품목명·업체명·비고에 사급 여부와 수량 상황을 자유 형식으로 적어야 한다.
- 성공했을 때 사용자가 할 수 있는 일: 구매 또는 자재 담당자가 품목을 사급으로 명확히 분류하고 제공 예정량을 등록하며, 자재 담당자가 기존 분할 입고·IQC 흐름을 그대로 사용해 누적 입고와 잔량을 확인한다.
- 하지 않을 경우 영향: 고객 제공 지연과 자재 부족을 일반 구매 지연과 구분하지 못하고, 생산 준비 판단·담당자 인수인계·감사 이력이 모호해진다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 구매 담당 | 구매품목의 일반 구매/사급 구분과 제공 예정 기준 입력 | 접근 가능한 프로젝트 구매품목 | 기존 구매 편집 권한 안의 사급 기준 필드 | 구분·수량 변경 audit |
| 자재 담당 | 사급품 도착 등록, IQC 요청, 적합품 입고 확정, 잔량 확인 | 접근 가능한 프로젝트 자재 | 기존 `MaterialReceipt.Update` 흐름 | 도착·상태·수량 audit |
| 품질 IQC 담당 | 사급품 IQC 요청 확인과 최소 판정 | 접근 가능한 프로젝트 IQC | 기존 `quality.inspect` 판정 | 부적합 Pending·재검사 계약 보존 |
| 생산관리·Read-only | 사급 제공 상태·잔량·차단 조회 | 기존 역할별 범위 | 업무 mutation 없음 | 공급 책임과 지연 상태 식별 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 구매품목을 사급으로 지정하고 제공 예정 기준 입력 → 기존 008A 도착 건 등록 → 누적·잔량 확인 → IQC 요청·판정 → 입고 확정 → 도착 마감.
- validation 실패: 수량·단위 pair, 0 이하 수량, 주문/제공 예정량 초과, 상태에 맞지 않는 구분 변경과 권한 없는 mutation을 서버에서 차단한다.
- 동시 처리·중복: 기존 `MaterialsStore`의 품목 row lock, version, 수량 합계와 transaction owner를 재사용해 경쟁 도착과 구분 변경이 잔량 invariant를 깨지 않게 한다.
- 취소·재시도·복구: 기존 008A의 `Arrived` 상태 사유 필수 취소와 이후 forward-only 전이를 유지한다. 사급 구분 변경 가능 시점과 감사 방식은 Fable 권장안으로 고정한다.
- 부분 실패와 rollback: 사급 기준 변경과 audit, 도착·IQC·Pending 연결은 기존 transaction 경계를 깨지 않는다. migration은 additive forward-fix를 사용한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 기존 `project_procurement_items`, `material_receipts`, `material_iqc_attempts`, `material_receipt_events`를 재사용하고 사급 구분·공급 책임 표시·제공 예정 기준과 derived 잔량만 추가한다.
- 상태 전이: 사급 여부는 구매품목의 공급 유형이고, 도착 건 상태는 기존 `Arrived → IqcRequested → Passed/FailedBlocked → Confirmed`를 그대로 사용한다.
- 보존·감사·삭제: 공급 유형과 제공 예정 기준의 변경은 추적하고 도착·IQC 원장을 hard delete하지 않는다.
- attachment·Excel·PDF: 외부 증빙 첨부, 상세 IQC 사진·PDF와 Excel 확장은 이번 Task에서 제외한다.
- 외부 연동·notification: 고객 포털·ERP·SCM·자동 이메일/Teams provider 발송은 제외한다. 기존 인앱 업무 원본이 필요한지 Fable이 최소안을 권고한다.
- migration·기존 데이터: 기존 품목은 일반 구매로 호환하고 TASK-008A `0030`은 수정하지 않는다. 사급 data는 신규 additive migration으로만 추가하며 Persistent UAT에는 적용하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 기존 구매 편집에서 공급 유형·사급 기준을 입력하고 `/materials/receipts`에서 사급 badge, 제공 예정량·누적 입고·잔량과 다음 행동을 확인한다.
- loading·empty·error·success feedback: 기존 action 위치와 공통 feedback 패턴을 재사용하고 경쟁 충돌 시 최신 상태를 다시 불러오도록 안내한다.
- 접근성·390px·Teams narrow: 모바일은 PC 표 축소가 아니라 사급 badge·핵심 수량·상태·다음 action을 우선한 카드와 bottom sheet를 사용한다.
- UAT와 rollout: isolated DB·synthetic browser만 사용한다. Persistent UAT·대표 runtime handover는 제외한다.
- rollback과 운영자 대응: experiment branch를 채택하지 않으면 대표 repo 영향은 없다. migration 적용 뒤 문제는 destructive down이 아니라 additive forward-fix 대상으로 계획한다.

## 6. 포함·제외 범위

### 포함

- 구매품목의 사급 구분과 공급 책임 표시
- 사급 제공 예정 수량·단위, 누적 입고·잔량 projection
- 기존 TASK-008A 분할 도착·IQC·입고 확정·Pending 계약 재사용
- 사급 기준 변경 audit와 서버 권한·수량 무결성
- Desktop 관리 화면과 모바일 전용 현장 카드
- isolated migration·Backend·Frontend·E2E·browser 검증

### 제외

- TASK-008A data model·상태 machine 재구현
- 고객 포털, ERP·SCM, 외부 공급망 API와 고객 직접 입력
- 상세 IQC 체크리스트·사진·PDF, 키팅, Excel import/export
- 일반 구매품의 기존 업무 정책 변경
- 실제 provider, Persistent UAT, runtime handover
- 대표 repo, GitHub `main`, push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 사급 공급 유형·예정량·잔량·변경 가능 시점과 지연 표시의 구체 계약 | Fable이 실제 008A schema·화면·권한을 비교해 최소 범위 권장안을 선택 | Fable 1차 기획의 권장안 | 사용자의 experiment fast-track 지시에 따라 권장안 자동 채택 | No |
| 2 | 사급품 인앱 업무·알림 추가 여부 | 기존 업무 원본 재사용과 신규 알림의 운영 부담 비교 | 신규 외부 delivery 없이 최소 handoff 권장 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 일반 구매와 사급을 구분하고 고객 제공 예정량 대비 실제 입고·잔량을 기존 008A 원장 위에서 추적한다.
- 권장 범위: additive 공급 유형·사급 기준·derived 수량과 audit, 기존 도착·IQC·Pending transaction 재사용, desktop/mobile 전용 표시.
- 확정한 정책: Fable의 비차단 권장안을 자동 채택하되 서버 권한·forward-only·단일 진실·기존 일반 구매 호환·실험 격리는 변경하지 않는다.
- 명시적 제외: 외부 공급망·고객 포털·상세 IQC·키팅·Excel·실제 provider·Persistent UAT·main 반영.
- Deferred 비차단 결정: 외부 고객 협업, 증빙 첨부, Excel, 운영 handover.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 사용자가 사급품의 공급 책임과 제공 예정량, 누적 입고·잔량, IQC·입고 상태를 일반 구매품과 혼동 없이 확인한다.
- 권한·데이터 불변조건: 서버 권한, 수량 pair·합계, `receipt_completed` derived projection, Pending·감사 이력과 기존 일반 구매 동작을 보존한다.
- 자동 검증: Backend build·targeted/전체 tests, migration fresh/existing isolated apply, Frontend lint/typecheck/unit/build, isolated Full-Stack E2E.
- 사용자 검수: desktop·390px의 구매 편집·자재 페이지를 페이지별 screenshot으로 확인한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성하는 권장안을 planning 입력과 실험 구현 기준으로 사용하는 데 동의한다.

확인 source: 사용자는 이 실험 branch에서 신규 작업을 인터뷰 없이 권장안으로 바로 `Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현`하고 결과물을 보여주도록 명시했다.
