# TASK-010A — 패널별 키팅·제조 내 업무 연결 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 `TASK-010A`를 기획하기 위한 interview source of truth다. 사용자는 이 `experiment/*` worktree에서 사용자-facing interview와 중간 승인 없이 Fable 권장안을 채택해 `Fable 1차 기획 → Codex 내용 review → review 기반 Fable 2차 기획 → Codex 구현·검증·페이지별 screenshot·local commit`까지 연속 진행하도록 명시했다. 아래에는 Roadmap과 완료된 실험 `TASK-008A`·`008B`·`009A`에서 확정된 계약만 기록하며, 패널별 키팅의 최소 완료 단위·일괄 처리·중복 방지 같은 미확정 정책은 Fable의 비차단 권장안 대상으로 남긴다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-010A`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-010A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: IQC 합격 뒤 입고 확정된 자재를 패널별 제조 투입 가능 상태로 전환하고 제조 담당자의 패널 단위 내 업무를 정확히 한 번 생성한다.
- Root Finding 또는 정책 결정: 현재 008A·009A는 구매품목·도착 건의 입고 확정까지만 관리하며 패널과 자재 준비 상태를 연결하는 데이터·화면·transaction이 없어 제조 인수인계가 끊긴다.
- 변경·검증 경계: 현재 experiment 계보의 additive migration·Backend·Frontend·isolated PostgreSQL·synthetic data·desktop/390px screenshot만 포함한다.
- 보존할 불변조건: 입고 확정 상태가 authoritative, 패널 단계 전진-only, 제조 업무 idempotency, 자재 권한과 project access 서버 강제, append-only 감사, 실제 provider 차단, 대표 repo·main·Persistent UAT 불변.
- 예상 산출물: Fable 1차 planning 원문, Codex 내용 review, Fable 2차 planning 원문, 구현·자동 검증·desktop/mobile screenshot·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

Roadmap의 canonical `TASK-010A` 한 건 외에 같은 목적의 Task 문서·local/remote branch·worktree·PR은 0건이다. 기존 `KittingCompleted` workflow stage와 화면용 문구는 실제 키팅 기능이 아니라 TASK-006A의 기반 계약이며, `TASK-008A`·`009A`는 키팅을 명시적으로 제외했으므로 ID 충돌이 아니다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-17
- 실행 형태: 현재 실험 worktree에서 다음 미착수 기능을 즉시 진행
- workflow: Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit
- 승인 대체: 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다.
- 안전 예외: Repository 충돌, secret·개인정보 노출, 입고 확정·수량 무결성 또는 stage 전진-only 위반은 fast-track으로 우회하지 않고 blocking decision으로 반환한다.
- 게시 경계: push·PR·merge 미승인, main merge 승인 `0/3`.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 standing experiment 규칙, Roadmap 확정사항과 008A·009A 계약을 기록. 미확정 정책은 Fable 권장안 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 자재 담당은 구매품목별 도착·IQC·입고 확정을 완료할 수 있지만 그 자재가 어느 패널의 제조 투입 준비를 충족했는지 기록하거나 제조팀에 패널 단위로 넘길 수 없다.
- 해결할 문제: 자재 담당이 실제 활성 패널을 기준으로 키팅 준비 상태를 확인하고 일부 패널 또는 여러 패널을 완료 처리하며, 완료한 패널마다 제조 담당자의 내 업무가 중복 없이 생성되어야 한다.
- 현재 우회 방식: 입고 확정과 제조 단계 사이를 시스템 밖에서 구두·메신저로 전달하며 `panel_placeholders.workflow_stage`는 계속 `BeforeManufacturing`에 머문다.
- 성공했을 때 사용자가 할 수 있는 일: 모바일에서 프로젝트와 준비 대상 패널을 열어 핵심 자재 준비 조건을 확인하고 선택 패널을 키팅 완료하며, 제조 담당자는 자신의 내 업무에서 해당 패널 제조 화면으로 이어지는 준비 업무를 확인한다.
- 하지 않을 경우 영향: 입고 확정 자재가 있어도 제조 투입 가능 여부와 담당 인수인계가 감사 가능한 데이터로 남지 않고, 동일 패널 제조 업무가 수동으로 중복 생성될 수 있다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 자재 정·부 담당 | 키팅 queue 조회, 패널별 준비/완료, 선택 패널 일괄 완료 | 기존 project access와 `MaterialReceipt.Update` 범위 | 활성 패널의 키팅 상태만 변경 | actor·시각·선택 패널·완료 전제 snapshot·event 보존 |
| 제조 정·부 담당 | 키팅 완료 패널과 생성된 제조 내 업무 조회 | 기존 project access와 제조 권한 범위 | 키팅 mutation 불가 | 업무 target·idempotency·handoff 근거 조회 |
| 생산관리·구매·품질 | 프로젝트의 입고/키팅 요약 조회 | 기존 project access 범위 | 키팅 mutation 불가 | 내부 식별자·원문 민감정보 비노출 |
| Read-only·System Administrator | 승인된 조회·감사 | 기존 정책 범위 | 업무 mutation 우회 금지 | 서버 authorization과 감사 이력 유지 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 프로젝트 자재 입고 조건 충족 → 자재 키팅 queue 활성화 → 패널 선택 → 서버 전제조건·version 재검증 → 패널 키팅 완료 event 고정 → 패널당 제조 내 업무 1건 생성 → 완료 결과와 다음 행동 표시.
- validation 실패: 활성·패널정보·입고 준비 조건 미충족, 이미 완료, 선택 0건, stale version, 권한·project scope 불일치를 서버가 안정적인 한글 오류로 차단한다.
- 동시 처리·중복: 패널별 unique 완료 record와 idempotency key, row lock 또는 optimistic version으로 동시 단건/일괄 요청이 제조 업무와 event를 중복 생성하지 않게 한다.
- 취소·재시도·복구: network 실패 후 동일 요청을 재시도해도 완료 결과는 하나다. 완료된 stage를 되돌리거나 record를 삭제하지 않으며, 잘못된 완료 정정 정책은 이번 Task에서 임의 추가하지 않는다.
- 부분 실패와 rollback: 일괄 선택은 사용자에게 성공·실패가 혼재하지 않도록 transaction 전체 성공 또는 전체 rollback을 권장 후보로 둔다. migration 적용 후 rollback은 기존 migration 수정·drop이 아니라 forward-fix다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: panel kitting state 또는 immutable completion record, version, completed actor/time, readiness snapshot, batch/correlation metadata, panel manufacturing work item.
- 상태 전이: 패널은 키팅 대기/진행/완료를 표현하되 최종 `KittingCompleted`는 전진-only다. `panel_placeholders.workflow_stage`의 기존 제조 상태와 충돌하지 않는 authoritative 연결 방식을 Fable이 정한다.
- 보존·감사·삭제: 완료 기록과 생성된 제조 업무는 hard delete·덮어쓰기하지 않는다. cancellation된 panel·deleted/on-hold project 처리와 과거 완료 조회는 Fable 권장안 대상으로 둔다.
- attachment·Excel·PDF: 이번 Task에는 첨부·사진·Excel·PDF를 추가하지 않는다.
- 외부 연동·notification: 기존 인앱 내 업무가 원본이다. 제조 업무와 필요 최소한의 기존 인앱 handoff만 고려하고 Teams/Mail/Activity 실제 delivery는 생성하지 않는다.
- migration·기존 데이터: 현재 latest `0032` 다음 additive migration을 사용한다. 기존 패널을 완료로 추정 backfill하지 않으며, 008A·008B·009A migration과 입고 상태 machine은 수정하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 기존 자재 영역에서 `패널 키팅` 진입을 제공하고 프로젝트별 준비 요약·패널 card·선택 action을 구성한다. 내 업무 deep link가 동일 화면의 대상 패널로 연결되어야 한다.
- loading·empty·error·success feedback: queue loading/empty/error/success 구분, 선택 수·완료 전제·다음 제조 handoff를 action 가까이 표시하고 중복 submit을 차단한다.
- 접근성·390px·Teams narrow: PC table 축소가 아닌 한 열 카드와 선택 mode, 글씨·도형은 compact하되 44px touch target, 좌측 상단 숨김 메뉴·page-level overflow 0을 유지한다.
- UAT와 rollout: isolated synthetic PostgreSQL·provider disabled만 사용한다. Persistent UAT migration·runtime handover는 미실행한다.
- rollback과 운영자 대응: 적용 전에는 local branch 폐기로 종료할 수 있다. migration 적용 후에는 additive forward-fix와 완료 record/work item 보존으로 복구한다.

## 6. 포함·제외 범위

### 포함

- 활성 패널별 키팅 readiness와 완료 상태 조회
- 일부 패널 선택과 프로젝트 내 여러 패널 일괄 키팅 완료
- 입고 확정 전제조건의 서버 authoritative 재검증
- 패널 키팅 완료 record/event와 제조 내 업무 exactly-once 생성
- 자재·제조·조회 역할 authorization와 project access scope
- 모바일 우선 adaptive 키팅 화면, desktop composition과 deep link
- additive migration, transaction·concurrency·authorization·Frontend·isolated E2E 검증

### 제외

- 제조 체크리스트·작업 시작/종료·제조 중단 (`TASK-011A`)
- BOM, 패널별 자재 소요량 master, 창고 위치·재고 차감·생산 불출
- 완료 stage 되돌리기·관리자 강제 정정·재키팅 workflow
- 첨부·사진·Excel·PDF와 신규 외부 알림 채널
- Persistent UAT migration·write·runtime handover와 실제 provider
- 대표 repo·GitHub main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | `부분 키팅`의 최소 의미 | 패널 일부 선택 완료는 현재 데이터로 정확히 표현 가능. 한 패널 내부 자재별 부분 키팅은 BOM·소요량 연결 없이는 거짓 정밀도가 됨 | 현재 데이터로 증명 가능한 패널 선택 단위 완료를 우선하고 패널 내부 allocation은 제외하는 방향 검토 | Fable 권장안 자동 채택 | No |
| 2 | 키팅 readiness 전제 | 하나의 도착 건 확정은 빠르지만 누락 위험, 모든 프로젝트 품목 `receipt_completed`는 보수적이지만 제조 준비 의미가 명확함 | 활성 필수 구매품목 전체의 arrivals closed·confirmed derived 완료를 서버가 확인하는 방향 검토 | Fable 권장안 자동 채택 | No |
| 3 | 키팅 상태 model | 패널 컬럼만 추가하면 단순하지만 batch·감사·동시성 근거가 약함. 별도 completion/event는 추적성이 높음 | panel당 immutable completion + versioned projection의 최소 조합을 검토 | Fable 권장안 자동 채택 | No |
| 4 | 일괄 완료 transaction | 부분 성공은 대량 작업은 빠르지만 재선택·감사 UX가 복잡함. all-or-nothing은 결과가 명확함 | 선택 패널 전부 재검증 후 단일 transaction 처리 방향 검토 | Fable 권장안 자동 채택 | No |
| 5 | 제조 내 업무 생성 | 프로젝트 1건은 패널 추적이 약함. 패널별 1건은 제조 흐름·deep link·중복 방지와 일치함 | `target_type=Panel`, 패널별 stable idempotency key, 제조 정→부→Sales→Admin fallback을 검토 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 입고 확정된 자재와 패널 제조 투입 준비 사이에 키팅 상태·화면·제조 handoff가 없음.
- 권장 범위: 현재 데이터로 증명 가능한 패널 선택 단위 키팅, 보수적인 프로젝트 자재 readiness, immutable 완료와 패널별 제조 내 업무, 모바일 우선 일괄 선택 UX.
- 확정한 정책: 키팅은 패널 단위, 완료 시 제조 내 업무 생성, 별도 생산 불출 없음, Backend 권한 authoritative, stage 전진-only와 중복 방지.
- 명시적 제외: 제조 실행·BOM/재고 차감·되돌리기·첨부·외부 provider·Persistent UAT·게시.
- Deferred 비차단 결정: 실제 BOM·패널별 소요량·창고 재고·정정/재키팅 운영 정책.
- Fable 판정: `COMPLETED_CONFIRMED` — 사용자 명시적 experiment interview waiver에 따른 planning 입력 상태.

## 9. 성공 기준

- 업무 결과: 자재 담당이 모바일에서 준비된 패널 일부 또는 여러 건을 키팅 완료하고 제조 담당자가 패널별 내 업무를 중복 없이 받는다.
- 권한·데이터 불변조건: mutation/read 서버 권한+scope, 입고 readiness 재검증, 완료 전진-only, batch 원자성, panel completion/work item/event exactly-once.
- 자동 검증: migration fresh/existing, Backend build·전체/권한/transaction/concurrency tests, Frontend lint·typecheck·unit·build, isolated E2E, desktop·390px·Teams narrow overflow 0.
- 사용자 검수: synthetic 페이지별 screenshot을 보고하되 사용자 직접 검수 완료로 표시하지 않는다.

## 10. 사용자 확인

- [x] 사용자 standing rule로 interview 질문 왕복과 중간 승인을 생략한다.
- [x] Roadmap·008A·009A에서 확정된 업무 문제·역할·불변조건을 planning 입력으로 사용한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] Repository 충돌·입고/수량 무결성·stage 전진-only·secret/개인정보 위험은 fast-track으로 우회하지 않는다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] open blocking decision 0인 경우에만 1차 planning을 시작한다.

확인 source: 사용자는 이 실험 worktree의 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 1차 기획·Codex review·Fable 2차 기획·Codex 구현하고 결과물을 보여주도록 반복 명시했다.
