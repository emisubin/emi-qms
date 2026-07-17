# TASK-011A — 제조 작업 시작·종료·중단 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 `TASK-011A`를 기획하기 위한 interview source of truth다. 사용자는 이 `experiment/*` worktree에서 사용자-facing interview와 중간 승인 없이 Fable 권장안을 채택해 `Fable 1차 기획 → Codex 내용 review → review 기반 Fable 2차 기획 → Codex 구현·검증·페이지별 screenshot·local commit`까지 연속 진행하도록 명시했다. 아래에는 Roadmap, 사용자 흐름 baseline, 완료된 실험 `TASK-007A`·`010A`와 실제 Repository에서 확인된 계약만 기록한다. 미확정 제조 표시·입력 항목과 LQC handoff 경계는 Fable의 비차단 권장안 대상으로 남긴다. 대표 repo, GitHub `main`, Persistent UAT, 실제 provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-011A`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-011A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 키팅 완료로 생성된 패널별 제조 업무를 제조 담당자가 모바일에서 시작·체크·종료하고, 작업 불가 시 제조 중단 Pending을 등록해 생산관리와 조치 담당자에게 연결한다.
- Root Finding 또는 정책 결정: 현재 패널별 `ManufacturingWork` 내 업무는 생성되지만 link가 키팅 조회 화면으로 돌아가며, 제조 실행 시간·단계·체크·중단·감사 데이터를 입력할 전용 화면과 transaction이 없다.
- 변경·검증 경계: 현재 experiment 계보의 additive migration·Backend·Frontend·isolated PostgreSQL·synthetic data·desktop/390px screenshot만 포함한다.
- 보존할 불변조건: 키팅 완료가 제조 시작 전제, 패널 stage 전진-only, 서버 권한·project scope, 제조 업무 exactly-once, Pending append-only 감사, 품질 상세·실제 provider 차단, 대표 repo·main·Persistent UAT 불변.
- 예상 산출물: Fable 1차 planning 원문, Codex 내용 review, Fable 2차 planning 원문, 구현·자동 검증·desktop/mobile screenshot·implementation report·local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

Roadmap의 canonical `TASK-011A` 한 건 외에 같은 목적의 Task 문서·local/remote branch·worktree·open PR은 0건이다. `TASK-006A`의 `ManufacturingWork` workflow skeleton과 `TASK-010A`의 패널별 제조 내 업무는 이번 Task의 선행 기반이며 실제 제조 실행 기능이 아니므로 ID 충돌이 아니다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-17
- 실행 형태: 현재 실험 worktree에서 다음 미착수 기능을 즉시 진행
- workflow: Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit
- 승인 대체: 비차단 제품 선택은 Fable의 Repository 근거 권장안을 자동 채택한다.
- 모바일 원칙: PC 화면을 줄인 반응형이 아니라 현장 사용자의 핵심 행동을 재구성한 적응형 화면, 작은 글씨·도형으로 핵심 정보 밀도 확보, 좌상단 숨김 메뉴, 다양한 도형을 사용한다.
- 안전 예외: Repository 충돌, secret·개인정보 노출, 키팅 완료 전제·stage 전진-only·Pending 감사 무결성 위반은 fast-track으로 우회하지 않고 blocking decision으로 반환한다.
- 게시 경계: push·PR·merge 미승인, main merge 승인 `0/3`.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 standing experiment 규칙, Roadmap·user-flow·007A·010A 계약 기록. 미확정 정책은 Fable 권장안 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 키팅 완료 시 panel target의 `ManufacturingWork` 내 업무가 제조 담당자에게 생성되지만 전용 제조 입력 화면 없이 키팅 화면으로 연결된다. 패널의 `workflow_stage`는 `BeforeManufacturing`에 머물며 제조 시작·종료·중단 이력이 없다.
- 해결할 문제: 제조 담당자가 자기 업무에서 대상 패널로 바로 들어가 현재 작업 단계와 진행 상태를 보고, 한 손으로 시작·체크·종료하며 작업 불가 상황은 제조 중단 Pending으로 즉시 전환해야 한다.
- 현재 우회 방식: 실제 제조 진행을 시스템 밖의 종이 자주순차표·구두·메신저로 관리하고 프로젝트 화면에는 결과 stage만 수동으로 해석한다.
- 성공했을 때 사용자가 할 수 있는 일: 모바일에서 오늘 해야 할 패널을 선택해 작업을 시작하고 최소 체크 항목을 완료한 뒤 종료하며, 중단 시 원인·설명·조치 담당자를 등록해 Pending 상세로 이어간다. 생산관리·조회 역할은 desktop에서 패널별 진행과 차단 상태를 본다.
- 하지 않을 경우 영향: 키팅 뒤 제조 착수 여부·실제 시간·중단 원인이 감사 가능한 데이터로 남지 않고, LQC handoff와 프로젝트 병목이 현장 사실을 반영하지 못한다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 제조 정·부 담당 | 제조 queue 조회, 패널 작업 시작·체크·종료, 중단 등록·재개 | 기존 project access와 배정된 제조 업무 | 키팅 완료 panel의 활성 제조 실행만 변경 | actor·시각·상태 전이·입력 snapshot·operation 보존 |
| 생산관리 정·부 담당 | 프로젝트·패널 진행/중단 조회, 제조 중단 조치 연결 확인 | 기존 project access | 제조 실행 mutation 불가, Pending의 기존 허용 조치만 가능 | 참조 알림과 진행 근거 조회 |
| Pending 조치 담당자 | 제조 중단 사유 확인·조치·상태 전이 | 기존 Pending scope | 기존 Pending 계약 안의 배정·상태·댓글 | append-only history와 expected version |
| 품질·자재·영업·조회 역할 | 허용된 프로젝트 제조 상태 조회 | 기존 project access | 제조 mutation 불가 | 내부 민감 식별자 비노출 |
| System Administrator | 기준·이력 조회 | 기존 관리자 정책 | 제조 업무 입력 무제한 우회 금지 | 서버 authorization과 감사 이력 유지 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 키팅 완료·제조 업무 생성 → 제조 queue/deep link 진입 → 패널 제조 시작 → 최소 단계 체크 → 종료 전 서버 재검증 → 제조 작업 완료·panel stage 전진·기존 업무 완료 → 다음 단계 handoff.
- validation 실패: 키팅 미완료, 취소/보류 프로젝트·panel, 제조 업무 미배정, 이미 완료, 활성 중단 존재, 필수 체크 미완료, stale version, 권한·project scope 불일치를 서버가 안정적인 한글 오류로 차단한다.
- 동시 처리·중복: panel별 하나의 활성 manufacturing execution과 client operation id 또는 expected version, row lock으로 중복 시작·종료·중단이 event·업무·Pending을 중복 생성하지 않게 한다.
- 취소·재시도·복구: network 실패 뒤 동일 요청을 재시도하면 기존 성공 결과를 반환한다. 완료를 되돌리거나 시간을 덮어쓰지 않는다. 중단 조치 뒤 재개 조건과 동일 execution 연속 여부는 Fable 권장안 대상으로 둔다.
- 부분 실패와 rollback: 작업 종료, stage 전진, 제조 업무 완료와 다음 handoff는 단일 transaction이거나 전부 rollback한다. 제조 중단 상태·Pending·차단 참조도 한 transaction 경계로 검토한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: panel manufacturing execution, execution stage/check item snapshot, 시작·종료 actor/time, version/operation, stop event와 Pending 연결. 영구 template 관리 UI는 제외하고 고정 최소 MVP snapshot만 검토한다.
- 상태 전이: `BeforeManufacturing → ManufacturingInProgress → ManufacturingCompleted`는 전진-only다. 제조 중단은 stage 번호를 후퇴시키지 않고 활성 execution의 blocked 상태와 Pending으로 표현한다.
- 보존·감사·삭제: 시작·체크·종료·중단 record를 hard delete·덮어쓰기하지 않는다. 취소 panel/project lifecycle과 purge FK 정합을 구현하며 과거 완료는 조회 가능하게 한다.
- attachment·Excel·PDF: 상세 자주순차표 파일, 사진, Excel, PDF와 template 관리 UI는 이번 최소 MVP에서 제외 후보로 둔다.
- 외부 연동·notification: 기존 인앱 내 업무·알림이 원본이다. 제조 중단은 `ManufacturingStop`, `Urgent` Pending과 생산관리 참조를 기존 delivery outbox에 기록하되 실제 Teams/Mail/Activity provider는 실행하지 않는다.
- migration·기존 데이터: 현재 latest `0033` 다음 additive migration을 사용한다. 기존 panel을 시작/완료로 추정 backfill하지 않으며 기존 workflow·Pending·키팅 migration을 수정하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 별도 전역 `제조` 메뉴 또는 내 업무 deep link의 정확한 역할을 Fable이 권장한다. 모바일은 queue → panel focus → 시작/체크/종료·중단이 한 화면에 이어지고, desktop은 프로젝트·패널 진행 요약과 작업 상세를 함께 본다.
- loading·empty·error·success feedback: queue loading/empty/error/success, 저장 중·완료·차단, stale conflict와 다음 행동을 action 가까이 표시하고 중복 submit을 차단한다.
- 접근성·390px·Teams narrow: PC table 축소가 아닌 compact card·stepper·큰 핵심 action, 44px touch target, 작은 정보 글씨, 좌측 상단 숨김 메뉴·page-level overflow 0을 유지한다. 원형·타원형·각진/둥근 직사각형·정사각형을 기능적 시각 언어로 사용한다.
- UAT와 rollout: isolated synthetic PostgreSQL·provider disabled만 사용한다. Persistent UAT migration·runtime handover는 미실행한다.
- rollback과 운영자 대응: 적용 전에는 local branch 폐기로 종료할 수 있다. migration 적용 후에는 additive forward-fix와 실행/Pending/work item 보존으로 복구한다.

## 6. 포함·제외 범위

### 포함

- 키팅 완료 panel과 제조 내 업무 기반 제조 queue·deep link
- panel과 최소 제조 단계 단위 시작·체크·종료, 실제 시작/종료 시각·actor·audit
- panel `workflow_stage`의 제조 시작·완료 전진-only 전이
- 제조 중단 사유·설명·조치 담당 연결, `ManufacturingStop` Pending·긴급/차단 참조
- 중단 조치 뒤 재개 또는 계속 진행의 명시적 최소 상태 계약
- 제조·조회 역할 authorization와 project access scope
- 모바일 우선 adaptive 제조 화면과 desktop 관리/조회 composition
- additive migration, transaction·idempotency·authorization·Frontend·isolated E2E 검증

### 제외

- LQC/OQC/FAT 상세 검사성적서·사진·PDF와 후속 품질 화면 (`TASK-012A`)
- 영구 제조 template 관리 UI·version activation (`TASK-ADMIN-002`)
- 현업 미회신 상세 자주순차표 전체 항목의 임의 확정
- 완료 stage 되돌리기·관리자 강제 정정·실행 record 삭제
- QR 공개 landing, 신규 외부 알림 채널, 실제 provider delivery
- Persistent UAT migration·write·runtime handover
- 대표 repo·GitHub main·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 미회신 제조 체크리스트 MVP | 빈 자유 입력은 빠르지만 상태 근거가 약함. 상세 전 항목 임의 구현은 운영 오해가 큼. 고정 최소 단계 snapshot은 교체 가능성과 완료 근거를 함께 가짐 | 시작 확인·도면/자재 확인·작업 수행·자체 확인 같은 소수의 일반 단계 snapshot을 사용하고 세부 template는 보류하는 방향 검토 | Fable 권장안 자동 채택 | No |
| 2 | 작업 단위와 동시성 | 프로젝트 단위는 현장 패널 추적이 약함. panel+step은 정확하지만 transaction이 늘어남 | panel execution 하나와 그 안의 ordered step snapshot, panel당 active execution 1건을 검토 | Fable 권장안 자동 채택 | No |
| 3 | 중단 후 재개 | 새 execution 생성은 이력이 명확하지만 반복 중단 시 분절됨. 같은 execution의 pause/resume은 누적 시간을 표현하기 쉬움 | 같은 execution에 append-only stop/resume event와 active Pending 1건을 연결하는 방향 검토 | Fable 권장안 자동 채택 | No |
| 4 | 제조 종료와 LQC handoff | LQC 상세 미구현 상태에서 새 검사 data를 만들면 012A 경계를 침범. 기존 workflow skeleton만 완료하면 handoff 가시성은 유지됨 | panel 제조 작업 종료와 기존 `ManufacturingWork` 업무 완료·stage 전진까지만 원자 처리하고 LQC 상세 생성은 보류하는 방향 검토 | Fable 권장안 자동 채택 | No |
| 5 | 진입 구조 | 별도 제조 전역 메뉴는 현장 queue 접근성이 높음. 내 업무 전용은 단순하지만 전체 queue가 약함 | 권한 조건부 `제조` 전역 진입과 패널별 내 업무 deep link를 같은 전용 화면에 연결하는 방향 검토 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 패널 키팅 뒤 생성된 제조 업무를 실제 시작·체크·종료·중단으로 수행할 데이터·API·모바일 화면이 없음.
- 권장 범위: panel execution, 소수의 일반 단계 snapshot, 전진-only stage, 기존 Pending 연결, 모바일 queue와 desktop progress.
- 확정한 정책: 키팅 완료 선행, 패널·제조 단계 입력, 시작/종료 시각, 제조 중단 Pending·조치 담당, 서버 권한 authoritative, 이력 보존.
- 명시적 제외: 품질 상세·전체 현업 자주순차표·template 관리·QR/첨부·외부 provider·Persistent UAT·게시.
- Deferred 비차단 결정: 실제 제조 표시/팝업/저장-only 상세 항목, 운영 template, LQC/OQC 상세 handoff.
- Fable 판정: `COMPLETED_CONFIRMED` — 사용자 명시적 experiment interview waiver에 따른 planning 입력 상태.

## 9. 성공 기준

- 업무 결과: 제조 담당자가 모바일에서 키팅 완료 panel을 시작·체크·종료하고 중단 시 Pending으로 조치 흐름을 만든다.
- 권한·데이터 불변조건: mutation/read 서버 권한+scope, 키팅 완료 재검증, panel stage 전진-only, active execution·중단 Pending 중복 방지, record/work item/event transaction과 감사.
- 자동 검증: migration fresh/existing, Backend build·전체/권한/transaction/concurrency tests, Frontend lint·typecheck·unit·build, isolated E2E, desktop·390px·Teams narrow overflow 0.
- 사용자 검수: synthetic 페이지별 screenshot을 보고하되 사용자 직접 검수 완료로 표시하지 않는다.

## 10. 사용자 확인

- [x] 사용자 standing rule로 interview 질문 왕복과 중간 승인을 생략한다.
- [x] Roadmap·user-flow·007A·010A에서 확정된 업무 문제·역할·불변조건을 planning 입력으로 사용한다.
- [x] 비차단 선택은 Fable 권장안을 자동 채택한다.
- [x] Repository 충돌·키팅 전제·stage 전진-only·Pending 감사·secret/개인정보 위험은 fast-track으로 우회하지 않는다.
- [x] 대표 repo·main·Persistent UAT·provider·게시를 제외한다.
- [x] open blocking decision 0인 경우에만 1차 planning을 시작한다.

확인 source: 사용자는 이 실험 worktree의 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 1차 기획·Codex review·Fable 2차 기획·Codex 구현하고 결과물을 보여주도록 반복 명시했다.
