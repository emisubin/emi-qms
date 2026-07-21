# TASK-WORKFLOW-CONTINUITY-001 Change 002 — 구매·자재·IQC 품목 추적 보정

## 1. 분류와 실행 기준

- taskType: `P2_REMEDIATION`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `8631594b2dcb31de5ed5dd187df43393bba6a2fa`
- canonicalTask: `TASK-WORKFLOW-CONTINUITY-001`
- purposeIdentity: 기존 구매→자재 도착→도착분별 IQC 연속 흐름을 같은 품목 기준으로 쉽게 입력·조회할 수 있게 보정한다.
- instructionChainRead: true
- taskIdentityGate: `PASS_REUSE`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- overrideSource: 사용자가 현재 experiment branch에서 이번 수정필요사항 전체를 하나의 Task로 승인 없이 바로 구현하라고 명시했다.
- planningOwner: `CODEX`
- fableInvocationRequired: false
- fableInvocationCount: 0
- fableWaiverSource: 사용자가 완전 신규 기능이 아니므로 Fable 없이 진행하라고 명시했다.

이번 수정필요사항은 별도 Task 여섯 개로 쪼개지 않고 `TASK-WORKFLOW-CONTINUITY-001 Change 002` 하나로 구현·검증·완료 보고한다.

## 2. 확인된 현재 상태와 원인

- 자재 도착 저장은 이미 같은 transaction 안에서 IQC attempt·품질 업무·담당자 알림을 생성한다.
- 구매 품목과 자재 입고는 `project_procurement_items.id`를 같은 identity로 사용하므로 data 연동은 되어 있다.
- 그러나 프로젝트 자재 탭은 품목 요약과 각 도착 회차를 독립 카드로 펼쳐 한 품목의 전체 이력을 읽기 어렵다.
- 프로젝트 품질 탭은 LQC·OQC·입회검사·FAT만 읽어 IQC가 보이지 않는다.
- 구매 화면은 공급 유형을 한 목록에 섞어 표시한다.
- 일반 구매품의 `orderQuantity`·`orderUnit`은 API와 Frontend가 모두 강제로 제거·거부한다.

## 3. 승인된 변경 범위

### 구매

- 프로젝트 구매 조회와 구매 수정 화면을 `도급 구매품`과 `사급 자재` 탭으로 나눈다.
- 각 탭에 품목 건수를 표시하고 빈 상태를 공급 유형별로 안내한다.
- 일반 구매품도 발주 수량·단위를 선택 입력할 수 있게 한다.
- 수량과 단위는 함께 입력하고 수량은 양수, 단위는 1~20자로 제한한다.
- 기존 일반 구매품의 수량 없음은 허용해 과거 data와 첫 도착 입력 흐름을 보존한다.
- 도착 이력이 있으면 누적 도착 수량보다 작게 변경할 수 없고 단위를 변경할 수 없다.

### 자재

- 기존 `입고 관리/키팅 관리` 구분은 유지한다.
- 입고 관리는 구매 품목별 한 행으로 구성한다.
- 행을 클릭하거나 keyboard로 열면 바로 아래에 도착 일자·수량·메모·상태와 연결 IQC 회차·판정·Pending을 시간순으로 표시한다.
- 구매 공급 유형·업체·입고 예정일·발주량과 자재 누계가 같은 행에 보이게 한다.

### 품질

- 프로젝트 품질 탭 안에 `수입검사(IQC)`와 `후속검사` 구분을 추가한다.
- IQC는 프로젝트 구매 품목·도착분과 연결해 요청·판정·성적서·Pending 이력을 표시한다.
- 기존 LQC·OQC·입회검사·FAT 조회·수정 진입은 유지한다.

### 도착분별 IQC 연동 검증

- 한 품목에 두 번 도착을 등록하면 서로 다른 receipt와 IQC attempt가 각각 생성되어야 한다.
- 각 attempt는 품질 담당 정·부의 내 업무·인앱 알림을 중복 없이 한 건씩 만든다.
- 도착·IQC 업무·알림은 기존 transaction 경계를 유지한다.

## 4. 불변조건과 제외 범위

- 구매·자재·IQC는 복제 row가 아니라 동일한 `project_procurement_items.id`를 사용한다.
- 기존 optimistic concurrency, 수정사유, 감사, IQC evidence, Pending·재검사 계약을 유지한다.
- 구매 완료 조건과 전체 workflow 단계 정의는 바꾸지 않는다.
- DB migration은 추가하지 않는다.
- 대표 repo·`main`, push·PR·merge, Persistent UAT와 실제 Teams/Mail provider는 변경하지 않는다.
- mainMergeApprovalCount: `0/3`

## 5. 검증 계획

- Backend: 일반 구매 수량 신규 저장·변경·제약, 두 도착분의 distinct IQC attempt/work item/notification 통합 테스트.
- Frontend unit: 공급 유형 탭, 일반 구매 수량 request, 자재 inline 이력, 품질 IQC 구분.
- Full-stack: 구매 수량 입력 → 두 도착 등록 → 자재 행 확장 → 품질 IQC 이력까지 실제 화면 검증.
- desktop와 390px에서 overflow·keyboard·empty/loading/error 상태를 확인한다.
- 전체 backend/frontend 회귀 검증을 수행한다.

## 6. 실행·Git 경계

- implementationApproved: `true`
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- commitApproved: `true` — experiment local commit만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 7. 완료 결과

- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- Backend 전체 isolated PostgreSQL: `412/412`
- Frontend lint: error `0`, 기존 warning `1`
- Frontend unit: `113/113`
- Frontend production build: 통과
- 신규 구매→분할 도착→자재 inline 이력→프로젝트 품질 IQC Full-Stack: `1/1`
- 실제 역할 stress lifecycle: `1/1`, panel `12`, Pending `6`, 최종 workflow `18/18`
- desktop·390px screenshot: `6장`
- Open P0/P1/P2: `0/0/0`
- 사용자 검수: 마지막 일괄 검수 대기
- 구현 보고서: [Change 002 구현 보고서](workflow-continuity-001-change-002-implementation-report.md)
