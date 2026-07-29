# TASK-008A — 자재 도착·분할 입고·IQC 요청·입고 확정 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 2
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 `TASK-008A`를 기획하기 위한 interview source of truth다. 이 실험 branch에서는 사용자의 기존 지시에 따라 질문 왕복을 생략하고 Fable이 제시한 권장안을 자동 채택한 뒤 확인용 요약을 다시 Fable에 검증시킨다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-008A`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-008A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 구매품목을 한 번의 완료 체크가 아니라 실제 도착 이력, 분할 수량, IQC 요청과 적합품 입고 확정 흐름으로 추적한다.
- Root Finding 또는 정책 결정: 현재 `/materials/receipts`는 구매품목의 `receipt_completed`와 완료일·비고만 수정하므로 Roadmap 5~7단계의 자재 도착, IQC 요청, 입고 확정을 구분하지 못하고 부분 도착·잔량·부적합 차단을 표현할 수 없다.
- 변경·검증 경계: 기존 구매품목을 기준으로 additive 입고 이력과 상태 계약을 설계하고 Backend 권한·transaction·migration, Frontend desktop·모바일 적응형 흐름, isolated DB·E2E를 검증한다. 상세 IQC 체크리스트·사진·PDF와 키팅은 제외한다.
- 보존할 불변조건: 18단계 순서, 서버 권한 authoritative, Pending의 forward-only 차단, 기존 구매정보·workflow·audit 보존, 기존 migration 불변, Persistent UAT write 금지, 실제 외부 발송 금지, 대표 repo·GitHub main 무변경.
- 예상 산출물: Fable interview 원문, Fable 1차 planning, Codex review, review 기반 Fable 2차 planning, 구현·자동 검증·페이지별 screenshot·implementation report와 local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 사용자 원요청과 실험 자동 진행 지침 기록 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 5 | Fable 권장안 1-A·2-A·3-A·5-A 자동 채택. 질문 4는 현재 실험 branch에 TASK-007A 코드와 migration이 존재하는 사실을 반영해 Fable 재검토 요청 | Fable 확인용 요약 또는 보정 질문 생성 |
| 2 retry 전 | `QUESTIONS_REQUIRED` | 3 | Fable 권장안 6-A·7-A·8-A 자동 채택. 첫 출력은 질문 번호가 6~8로 생성돼 runner contract가 저장을 거부했으며 Repository artifact는 없음 | 동일 round 번호로 확인용 요약 재호출 |
| 2 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | 추가 blocking 결정 없음. 실험 사전 지시를 사용자 확인 source로 적용 | Fable 1차 planning 진행 |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 자재 담당자가 구매품목별 입고 완료 여부, 완료일과 비고를 한 번에 저장한다.
- 해결할 문제: 분할 도착, 누적 도착 수량, 잔량, IQC 요청 여부, 부적합 차단과 적합품 입고 확정을 단계별로 추적할 수 없다.
- 현재 우회 방식: 완료 체크와 자유 형식 비고에 여러 의미를 함께 기록한다.
- 성공했을 때 사용자가 할 수 있는 일: 자재 담당자는 도착 건을 수량별로 추가하고 IQC를 요청하며, 검사 결과에 따라 사용 가능 수량을 입고 확정한다. 관련 사용자는 품목별 현재 상태와 다음 행동을 확인한다.
- 하지 않을 경우 영향: 후속 IQC·키팅·제조 업무가 실제 수량과 품질 차단 상태 없이 단일 boolean에 의존해 인수인계와 감사가 불명확해진다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 자재 담당 | 도착 등록·정정, IQC 요청, 적합품 입고 확정 | 접근 가능한 프로젝트 구매품목 | 자재 흐름 mutation | 수량·상태·정정 사유 audit |
| 품질 IQC 담당 | IQC 요청 확인과 결과 연결 | 배정 또는 접근 가능한 프로젝트 | 상세 IQC는 후속 Task, 이번 Task는 연결 계약만 | 부적합은 Pending과 차단 원칙 보존 |
| 구매·생산관리 | 도착·잔량·차단 조회 | 역할별 프로젝트 범위 | 이번 Task에서는 조회 중심 | 업무 인수인계 추적 |
| Read-only·System Administrator | 허용 범위 조회 | 기존 정책 범위 | 업무 mutation 우회 금지 | 감사 조회 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 구매품목 선택 → 도착 수량·일자·메모 등록 → 누적/잔량 재계산 → IQC 요청 → 적합 결과 확인 → 입고 확정.
- validation 실패: 0 이하 수량, 주문 수량 초과, 미래 도착일, 필수 사유 누락과 허용되지 않은 상태 전이를 서버에서 차단한다.
- 동시 처리·중복: 동일 품목의 경쟁 도착 등록과 IQC/확정 mutation이 수량 invariant를 깨지 않도록 transaction·lock·version/idempotency를 적용한다.
- 취소·재시도·복구: 확정 이력을 hard delete하지 않고 정정/취소 event와 사유로 복구한다. 상세 정책은 Fable 권장안으로 확정한다.
- 부분 실패와 rollback: 도착 이력·audit·workflow/업무 연결은 같은 transaction 경계로 처리하고 migration은 additive forward-fix를 사용한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 기존 구매품목과 주문 수량, 신규 자재 도착 이력, 누적/잔량 derived state, IQC 요청·입고 확정 상태.
- 상태 전이: 구매정보 준비 → 미도착/부분도착/도착완료 → IQC 대기/검사 중/부적합 차단/적합 → 입고 확정.
- 보존·감사·삭제: 도착·정정·상태 전이는 append-only 감사 대상으로 유지하고 직접 삭제를 금지한다.
- attachment·Excel·PDF: 상세 IQC 사진·성적서 PDF는 `TASK-009A`, 자재 입고 Excel import/export는 이번 Task 제외 후보로 둔다.
- 외부 연동·notification: 인앱 업무 원본과 IQC 요청 handoff만 검토하고 실제 Teams/Mail/Activity provider 호출은 제외한다.
- migration·기존 데이터: 기존 `receipt_completed` 데이터를 신규 상태로 보존·매핑할 정책과 rollback/forward-fix를 Fable이 권고한다. Persistent UAT 적용은 제외한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 기존 `/materials/receipts`에서 품목별 상태·누적/잔량·다음 행동을 표시하고 도착 등록은 모바일 bottom sheet 또는 전용 action surface로 제공한다.
- loading·empty·error·success feedback: 등록 action 가까이에 표시하고 경쟁 충돌 시 최신 상태 재조회와 재시도를 안내한다.
- 접근성·390px·Teams narrow: 모바일은 PC 표 축소가 아닌 프로젝트/품목 card, 단계 indicator, 44px action, numeric input과 sticky primary action을 사용한다.
- UAT와 rollout: isolated DB·synthetic browser만 사용하고 Persistent UAT와 대표 runtime handover는 제외한다.
- rollback과 운영자 대응: experiment branch를 채택하지 않으면 코드 rollback이며 신규 migration은 forward-fix 계획을 문서화한다.

## 6. 포함·제외 범위

### 포함

- 구매품목 단위 도착 등록과 분할 입고
- 누적 도착·잔량·도착 상태
- IQC 요청 handoff와 입고 확정 gate
- 상태·수량·정정 audit와 경쟁 mutation 보호
- Desktop 관리형 화면과 모바일 전용 자재 현장 화면
- isolated migration·Backend·Frontend·E2E·browser 검증

### 제외

- 상세 IQC 체크리스트·필수 사진·판정 UI·PDF snapshot (`TASK-009A`)
- 패널별 키팅·제조 업무 생성 (`TASK-010A`)
- 사급 자재 (`TASK-008B`)
- 자재 Excel import/export
- 실제 provider, Persistent UAT, runtime handover
- 대표 repo, GitHub `main`, push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 잔량·초과 검증 기준 수량 | A: 구매품목 nullable 주문 수량·단위 / B: 누적만 / C: 자재 기준 수량 이중 입력 | A | A 자동 채택. 구매품목 직접 입력에 nullable 수량·단위를 추가하고 Excel은 이번 Task에서 제외 | No |
| 2 | 도착·IQC 요청 단위와 도착 마감 | A: 도착 건별 IQC+명시적 마감 / B: 품목 전체 1회 / C: 누적 자동 마감 | A | A 자동 채택. 도착 건별 검사·입고와 품목 derived 상태를 사용 | No |
| 3 | TASK-009A 전 최소 IQC 결과 | A: 품질 담당 적합/부적합·사유 / B: 자재 대리 체크 / C: gate 연기 | A | A 자동 채택. 상세 체크리스트·사진 없이 최소 품질 판정만 포함 | No |
| 4 | 부적합 차단과 Pending 연결 | Round 1은 TASK-007A 미구현을 전제로 자체 blocked 상태만 권장 | 실제 Pending 재사용 | actual source 재검토 결과를 반영해 부적합 판정 transaction에서 구매품목 대상 Pending을 자동 생성하고 도착 건이 참조 | No |
| 5 | legacy `receipt_completed` 이행 | A: legacy backfill+신규 단일 진실 / B: 병행 / C: 초기화 | A | A 자동 채택. 기존 완료값을 호환 backfill하고 boolean은 신규 상태 derived 값으로 유지 | No |
| 6 | 차단 도착 건의 재검사 재개 | A: Pending이 재검사 요청 이상이면 IQC 재요청 / B: Pending이 자재 상태 자동 변경 / C: 재개 불가 | A | A 자동 채택. 단방향 gate로 도메인 결합을 제한 | No |
| 7 | 입고 확정 차단과 중복 Pending | A: 도착 건 단위 gate+건당 open Pending 최대 1 / B: 품목 전체 차단 | A | A 자동 채택. 적합 분할분은 확정 가능하고 open Pending은 재사용 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 단일 입고 완료 boolean을 분할 도착·IQC 요청·적합품 입고 확정 흐름으로 일반화한다.
- 권장 범위: 구매품목 nullable 주문 수량·단위, 도착 건별 분할 입고·IQC, 품질 최소 판정, legacy 완료 backfill·단일 진실, 부적합 Pending 자동 생성, Pending 재검사 상태를 통한 단방향 재개 gate.
- 확정한 정책: Fable 권장안 1-A·2-A·3-A·5-A·6-A·7-A·8-A, 서버 권한·transaction·audit·기존 18단계·실험 격리 보존.
- 명시적 제외: 상세 IQC·키팅·사급·Excel·실제 provider·Persistent UAT·main 반영.
- Deferred 비차단 결정: 구매 Excel 수량 열, 상세 IQC·사진·PDF, 키팅·사급·실제 provider·Persistent UAT.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 자재 담당자가 실제 도착 수량과 잔량을 추적하고 IQC 요청·입고 확정의 다음 행동을 수행한다.
- 권한·데이터 불변조건: 서버 권한, 수량 무결성, append-only audit, Pending 차단과 기존 구매정보를 보존한다.
- 자동 검증: Backend build·targeted/전체 tests, migration fresh/existing isolated apply, Frontend lint/typecheck/unit/build, isolated E2E.
- 사용자 검수: desktop·390px의 자재 목록, 도착 등록, IQC 대기/입고 확정 상태를 페이지별 screenshot으로 확인한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

확인 source: 사용자는 이 실험 branch에서 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 기획·review·재기획·구현하고 결과물을 보여주도록 지시했다. 이번 요청은 Fable 1차 기획, Codex review, review 기반 Fable 2차 기획과 Codex 구현까지 연속 진행하도록 명시한다.
