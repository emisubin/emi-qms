# TASK-014A — 영업 정산·세금계산서·프로젝트 완료 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 `TASK-014A`를 기획하기 위한 interview source of truth다. 사용자는 이 `experiment/*` worktree에서 사용자-facing interview와 중간 승인을 생략하고 Fable 권장안을 채택해 `Fable 1차 기획 → Codex 내용 review → review 기반 Fable 2차 기획 → Codex 구현·검증·페이지별 screenshot·local commit`까지 연속 진행하도록 명시했다. 아래에는 Product Roadmap, `TASK-USER-FLOW-001`, `TASK-007A/007B`, `TASK-013A`의 납품 완료·영업 정산 skeleton과 실제 Repository에서 확인된 계약만 기록한다. 미확정 세금계산서 입력 항목·정산 mutation 권한·완료 뒤 정정 정책은 Fable의 비차단 권장안 대상으로 남긴다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-014A`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-014A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 모든 active panel 납품 완료 뒤 영업 담당자가 세금계산서 발행을 기록하고 open Pending이 0건임을 확인해 프로젝트를 최종 완료하며, 완료 이력과 관련 부서 알림을 한 번만 남긴다.
- Root Finding 또는 정책 결정: `TASK-013A`는 `SalesSettlementCompleted` project work skeleton을 exactly-once 생성하지만 정산 record·전용 입력 화면·완료 조건의 원자 검증·프로젝트 완료 이력이 없다. generic 내 업무 완료로는 납품·Pending·세금계산서 조건을 함께 보장할 수 없다.
- 변경·검증 경계: 현재 experiment 계보의 additive migration·Backend·Frontend·isolated PostgreSQL·synthetic data·desktop/390px screenshot만 포함한다.
- 보존할 불변조건: 18단계 `납품 완료 → 영업 정산 대기 → 세금계산서 발행 완료 → 프로젝트 완료`, 모든 active panel 납품, open Pending 0건, Backend 권한·project scope, forward-only·idempotent completion, 완료 이력 보존, 실제 provider 차단, 대표 repo·main·Persistent UAT 불변.
- 예상 산출물: Fable 1차 planning 원문, Codex 내용 review, Fable 2차 planning 원문, 구현·자동 검증·desktop/mobile screenshot·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

Roadmap의 canonical `TASK-014A` 한 건 외에 같은 목적의 Task 문서·local/remote branch·worktree·open/merged PR은 0건이다. `TASK-013A`는 모든 active panel 납품 뒤 영업 정산 skeleton까지만 구현해 이번 목적과 중복되지 않는다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-18
- 실행 형태: 현재 실험 worktree에서 직전 `TASK-013A` 다음 기능을 즉시 진행
- workflow: Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit
- 승인 대체: 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다.
- 모바일 원칙: PC 화면 축소가 아니라 영업 담당자가 확인·입력·완료하는 순서로 재구성하고, 작은 글씨·도형으로 핵심 밀도를 높이며 좌상단 숨김 메뉴와 다양한 도형을 유지한다.
- 안전 예외: Repository 충돌, secret·개인정보 노출, 18단계 순서·모든 panel 납품·open Pending 0건·완료 이력·권한 불변조건 위반은 fast-track으로 우회하지 않고 blocking decision으로 반환한다.
- 게시 경계: push·PR·merge 미승인, main merge 승인 `0/3`.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 standing experiment 규칙과 Roadmap·013A 계약 기록. 미확정 정책은 Fable 권장안 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 모든 active panel의 납품이 완료되면 영업 담당자의 `SalesSettlementCompleted` 내 업무 skeleton 한 건이 생기지만, 세금계산서 발행 기록과 프로젝트 최종 완료를 처리할 domain 화면·record·transaction이 없다.
- 해결할 문제: 영업 담당자가 납품 완료 근거, open Pending과 세금계산서 상태를 한 화면에서 확인하고 조건을 모두 충족한 경우에만 프로젝트를 최종 완료해야 한다.
- 현재 우회 방식: generic 내 업무 완료 또는 프로젝트 상태 변경으로 개별 처리하면 미납품·미종결 Pending·세금계산서 미발행 상태를 우회할 수 있고 감사 이력이 분리된다.
- 성공했을 때 사용자가 할 수 있는 일: 자신이 담당한 정산 대기 프로젝트를 찾고 세금계산서 발행 정보를 기록한 뒤, 서버가 납품·Pending·version을 재검증해 프로젝트와 정산 업무를 원자적으로 완료하고 관련 부서에 인앱 완료 알림을 남긴다.
- 하지 않을 경우 영향: 18단계 마지막이 시스템 밖에 남고 프로젝트가 실제 완료 조건과 무관하게 Active 상태로 계속 남거나 잘못 Completed로 바뀔 수 있다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 영업 정·부 담당 | 정산 대기 조회, 세금계산서 발행 기록, 조건 확인, 프로젝트 완료 | 기존 project access와 자신이 담당한 정산 업무 | 자신이 영업 담당이거나 current settlement work assignee인 project | actor·시각·invoice projection·operation·완료 근거 |
| 생산관리·품질·제조·물류 | 허용 프로젝트의 정산·완료 상태 조회 | 기존 project access | 정산 mutation 불가 | 완료 알림과 read-only 상태 확인 |
| Pending 담당자 | 완료를 막는 open Pending 조치 | 기존 Pending scope | 기존 Pending 계약 안에서만 | 정산 화면은 Pending을 임의 종결하지 않음 |
| System Administrator·조회 역할 | 기준·감사 조회 | 기존 정책 | 영업 업무 mutation 무제한 우회 금지 | 서버 authorization 유지 |

정확한 mutation permission이 기존 `projects.update`인지 신규 최소 권한인지, SalesPrimary·SalesSecondary·current work assignee의 교집합을 어떻게 둘지는 Fable이 현재 permission matrix와 기존 domain pattern을 근거로 권장한다.

## 3. 정상·예외·복구 흐름

- 정상 흐름: 모든 active panel 납품 완료 → 정산 skeleton → 영업 담당자 정산 화면 진입 → 세금계산서 발행 정보 저장 → open Pending 0건 확인 → 최종 완료 → settlement/work item/project를 함께 완료 → 관련 부서 인앱 완료 알림.
- validation 실패: 미납품 active panel, open project/panel Pending, 세금계산서 미발행, 이미 취소·삭제·보류·완료된 project, stale version, scope·권한 불일치를 서버가 안정적인 한글 오류로 차단한다.
- 동시 처리·중복: 동일 operation 재시도는 같은 결과를 반환하고 다른 actor 또는 stale version 경쟁은 한 건만 성공한다. 완료 알림·event·work item completion은 exactly-once다.
- 취소·재시도·복구: draft 단계의 세금계산서 정보는 bounded하게 저장·수정할 수 있다. 완료 전 오류는 최신 상태를 다시 읽고 재시도한다. 완료 뒤 직접 되돌리기·hard delete는 이번 Task에서 제공하지 않는다.
- 부분 실패와 rollback: invoice 기록, 완료 조건 재확인, settlement 확정, 현재 work item 완료, project `Completed`, workflow event와 인앱 notification 원본은 한 transaction에서 모두 성공하거나 모두 rollback한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: project settlement record, invoice issued projection, version, completed actor/time, operation receipt. 실제 필드와 draft/final 상태는 Fable 권장안으로 정한다.
- 최종 완료 판정: 활성 panel 전부가 TASK-013A의 납품 finalized relation을 가져야 하고, project 또는 active panel target의 Pending 중 `Closed`가 아닌 것이 0건이어야 하며, invoice issued가 true여야 한다.
- 보존·감사·삭제: 완료된 정산과 project 완료 event는 덮어쓰기·hard delete하지 않는다. 승인된 permanent project purge만 기존 purge 경계와 FK 정합을 보강한다.
- attachment·Excel·PDF: 세금계산서 외부 파일·전자발행·국세청 연동·Excel·PDF 생성은 이번 범위가 아니다. 번호·발행일·메모 등 text projection의 최소 필드는 Fable이 권장한다.
- 외부 연동·notification: 기존 인앱 notification 원본만 생성하며 Teams/Mail/Activity 실제 provider는 실행하지 않는다.
- migration·기존 데이터: current latest `0036` 다음 additive migration을 사용한다. 기존 납품·Pending·project migration을 수정하거나 기존 Active project를 Completed로 backfill하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 정산은 확정된 user-flow대로 영업의 프로젝트 맥락에 배치한다. 내 업무의 `SalesSettlementCompleted` deep link가 해당 프로젝트 정산 화면을 연다. 목록/전용 workspace 필요성은 Fable 권장안으로 정한다.
- 핵심 정보: 납품 완료 panel 수, open Pending 수와 이동 링크, 세금계산서 상태, 최종 완료 조건, 변경 불가 경고와 다음 행동을 우선한다.
- loading·empty·error·success feedback: 대기 목록 loading/empty/error, draft 저장, stale·Pending·미납품 차단, 최종 완료 success를 action 가까이 표시하고 중복 submit을 막는다.
- 접근성·390px·Teams narrow: PC 표 축소가 아닌 `완료 조건 → 세금계산서 입력 → 최종 확인` 한 열 흐름, 44px 핵심 touch target, 작은 보조 글씨, 좌상단 숨김 메뉴와 page-level overflow 0을 유지한다. 원형·타원형·각진/둥근 직사각형·정사각형을 의미에 맞게 사용한다.
- UAT와 rollout: isolated synthetic PostgreSQL·provider disabled만 사용한다. Persistent UAT migration·runtime handover는 미실행한다.

## 6. 포함·제외 범위

### 포함

- 모든 active panel 납품 기반 영업 정산 대기 queue/detail
- 세금계산서 발행 완료의 최소 text/date projection과 draft 저장
- project/panel open Pending 0건과 납품 완료 조건의 서버 재검증
- 정산·work item·project·workflow event·인앱 완료 알림의 원자적 exactly-once 완료
- 영업 담당/current work assignee·최소 permission·project scope 서버 권한
- generic 내 업무 완료와 일반 프로젝트 상태 변경을 통한 우회 차단
- version·operation fingerprint·row lock·idempotency와 완료 이력 보존
- 모바일 우선 adaptive 정산 화면과 desktop 관리/조회 composition
- additive migration, authorization·transaction·concurrency·Frontend·isolated E2E 검증

### 제외

- 국세청·ERP·회계·전자세금계산서·결제·수금 외부 연동
- 세금계산서 파일 업로드·OCR·전자서명·PDF/Excel 생성
- 매출원가·마진·수금·채권·분할 청구·복수 세금계산서·통화 계산
- 완료 뒤 재오픈·세금계산서 취소·수정발행·프로젝트 재활성화
- 신규 외부 알림 채널·실제 provider delivery
- Persistent UAT migration·write·runtime handover
- 대표 repo·GitHub main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 요청 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 정산 record 상태 | 단일 완료 checkbox는 간단하지만 draft·경쟁 복구가 약하고, 과도한 회계 상태는 범위가 크다 | Repository의 draft/final 패턴을 사용한 최소 forward-only 상태를 권장 | Fable 권장안 자동 채택 | No |
| 2 | 세금계산서 최소 필드 | 완료 여부만 저장하면 감사 근거가 약하고, 전체 회계정보는 과도하다 | 발행 여부·발행일과 bounded 식별/메모 중 필요한 최소안을 권장 | Fable 권장안 자동 채택 | No |
| 3 | mutation 권한 | `projects.update` 재사용은 넓을 수 있고 신규 권한은 운영 부담이 있다 | 기존 sales responsibility·work assignee와 서버 permission의 최소 교집합을 권장 | Fable 권장안 자동 채택 | No |
| 4 | 화면 진입 | 프로젝트 상세만 두면 대기 건 탐색이 어렵고 전역 메뉴는 user-flow 위치와 충돌할 수 있다 | 영업 프로젝트 맥락과 내 업무 deep link를 보존하면서 최소 queue/detail 구성을 권장 | Fable 권장안 자동 채택 | No |
| 5 | 완료 후 정정 | 직접 재오픈은 감사·후속 상태를 깨고 완전한 correction lifecycle은 범위가 크다 | 완료는 append-only, 정정·재오픈은 별도 정책 Task로 보류 | Fable 권장안 자동 채택 | No |
| 6 | 완료 알림 대상 | 모든 사용자는 과다 알림이고 영업만 알리면 관련 부서가 종료를 모른다 | 기존 project responsibility 중 관련 부서에 in-app reference 알림을 중복 없이 생성하는 최소안을 권장 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 납품 뒤 정산 skeleton만 있고 세금계산서·Pending·납품 조건을 함께 검증하는 최종 완료 domain flow가 없음.
- 권장 범위: project settlement record, 세금계산서 최소 projection, 완료 조건 집계, 원자적 project/work/event/notification 완료, 영업 프로젝트 맥락의 adaptive 화면.
- 확정한 정책: 모든 active panel 납품, 세금계산서 발행 완료, open Pending 0건, Backend authoritative, project scope, 완료 이력 보존.
- 명시적 제외: 회계/국세청 외부 연동, 파일/OCR/PDF/Excel, 수금·채권·복수 청구, 재오픈·정정, provider·Persistent UAT·게시.
- Deferred 비차단 결정: 세금계산서 최소 필드, 정산 permission, queue/detail 배치, 알림 recipient, 운영 retention·재오픈 정책.
- Fable 판정 요청: `COMPLETED_CONFIRMED` — 사용자 명시적 experiment interview waiver에 따른 planning 입력 상태.

## 9. 성공 기준

- 업무 결과: 영업 담당자가 납품 완료 프로젝트의 세금계산서 발행을 기록하고 open Pending 0건을 확인해 프로젝트를 한 번만 완료한다.
- 권한·데이터 불변조건: 서버 permission+scope+actor, 모든 panel 납품·open Pending 0·invoice 조건, generic 우회 차단, stale/concurrent/replay 안전, 완료 이력·notification 원자성.
- 자동 검증: migration fresh/existing, Backend build·전체/권한/transaction/concurrency tests, Frontend lint·typecheck·unit·build, isolated E2E, desktop·390px·Teams narrow overflow 0.
- 사용자 검수: synthetic 페이지별 screenshot을 보고하되 사용자 직접 검수 완료로 표시하지 않는다.

## 10. 사용자 확인

- [x] 사용자 standing rule로 interview 질문 왕복과 중간 승인을 생략한다.
- [x] Roadmap·USER-FLOW·013A에서 확정된 업무 문제·역할·불변조건을 planning 입력으로 사용한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] Repository 충돌·18단계 순서·납품/Pending/invoice 조건·완료 이력·권한·secret/개인정보 위험은 fast-track으로 우회하지 않는다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] open blocking decision 0인 경우에만 1차 planning을 시작한다.

확인 source: 사용자는 이 실험 worktree에서 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 1차 기획·Codex review·Fable 2차 기획·Codex 구현하고 결과물을 보여주도록 반복 명시했고, 2026-07-18 `TASK-013A` 완료 뒤 “다음작업 시작”을 요청했다.
