# TASK-WORKFLOW-CONTINUITY-001 — 프로젝트 입력 연속성·품질 재검사 자동 인계 실험 입력

- taskType: `NEW_FEATURE`
- interviewOwner: `WAIVED_BY_USER_FOR_EXPERIMENT`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 사용자가 실제 업무 담당자 관점으로 프로젝트를 처음부터 끝까지 입력하다 IQC 부적합 Pending에서 더 진행하지 못한 사용자 검수 실패와 함께 요청한 연속성 개선을 고정한다. 사용자는 이 `experiment/*` branch에서 사용자-facing interview와 중간 승인을 생략하고 `Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot → local commit`까지 이어가도록 명시했다. 사용자 지시는 확정 입력이며, 비차단 세부 선택만 Fable의 Repository 근거 권장안을 자동 채택한다. 대표 repo·GitHub `main`·Persistent UAT·실제 Teams/Mail provider·push·PR·merge는 제외한다.

## 1. Task Identity Gate

- proposedTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-WORKFLOW-CONTINUITY-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_VALIDATION_FAILURE_AND_EXPLICIT_CHANGE`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 실제 담당자가 생산계획 이후 설계·구매를 병행하고, 자재 도착이 즉시 IQC로 인계되며, 부적합 Pending 조치 완료가 품질 재검사 업무·알림으로 자동 연결되고 재검사 합격이 Pending·IQC·전체 흐름을 한 번에 정합하게 갱신하도록 한다.
- Root Finding 또는 정책 결정: 자재 IQC Pending은 일반 품질 Pending의 수동 종결 보호에 포함되지 않아 Pending만 Closed가 되고 receipt는 `FailedBlocked`, IQC attempt는 Failed인 채 남는다. Pending `ReinspectionRequested` 전이와 재검사 attempt·내 업무·알림 생성도 별도 버튼으로 끊어져 있어 사용자가 중간에서 멈춘다. IQC 내 업무 deep link는 실제 검사 페이지가 아닌 프로젝트 전체 흐름 fallback으로 연결된다.
- 변경·검증 경계: 기존 `TASK-007A`, `TASK-008A`, `TASK-009A`, `TASK-012A`, `TASK-E2E-FULL-SUITE-001`의 다음 change로 결함·화면 수정을 추적하고, 신규 자동 재검사 handoff·알림·상태 정합성만 이 Task의 신규 능력으로 기획한다. 사용자가 함께 요청한 `TASK-QR-001` 선택 발급·inline preview는 기존 QR Task의 change로 추적하되 하나의 실제 lifecycle 검증에 포함한다.
- 보존할 불변조건: workflow 완료 순서 18단계, 설계·구매 선행 활성화와 stage 완료의 분리, 담당 부서만 쓰기·전 부서 조회, 품질 재검사 합격만 검사 연계 Pending 종결, 인앱 원본·내 업무 authoritative, 동일 이벤트 중복 0, append-only audit, 활성 QR 하나·opaque token, 기존 migration 수정 금지, `main` 불변.
- 예상 산출물: Fable 1차 planning, Codex review, Fable 2차 planning, Backend·Frontend·tests, desktop/mobile 및 단계별 screenshot, 관련 기존 Task change·Implementation report·ledger/Roadmap 동기화와 local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR의 local/remote ref와 기존 기록

`TASK-007A`, `TASK-008A`, `TASK-009A`, `TASK-012A`, `TASK-QR-001`, `TASK-E2E-FULL-SUITE-001`은 각각 개별 기능 완료 원장이고 이번 사용자 검수에서 드러난 교차 단계 자동 복구 능력을 canonical purpose로 갖지 않는다. 같은 목적의 Task·local/remote branch·worktree·기록된 PR은 0건이다. Canonical Roadmap next gate와는 다르지만 사용자가 experiment에서 이 사용자 검수 실패와 구체 변경을 즉시 구현하도록 명시했으므로 explicit override를 기록한다.

## 2. 사용자가 확정한 업무 결과

1. 프로젝트 상세의 첫 진입은 설계가 아니라 `전체 흐름`이다.
2. 생산관리 담당자가 생산계획과 담당자를 확정하면 설계와 구매가 함께 `내 업무`에 들어간다. 두 업무는 병행하되 workflow stage 완료 순서는 바꾸지 않는다.
3. 구매 완료 조건은 구매 업무가 실제로 끝났음을 판정하되 자재 도착·IQC 결과처럼 다음 부서 소유 단계까지 중복 요구하지 않는다. 정확한 필수 field 조합은 Fable 권장안과 Codex review로 확정한다.
4. 프로젝트 상세 자재 탭 안에 `입고 관리`와 `키팅 관리` 하위 탭을 두고, 입고 예정·도착·IQC·확정·잔량을 품목별로 한눈에 비교한다.
5. 자재 담당자가 `도착 등록`을 완료하면 별도 `IQC 요청` 버튼 없이 같은 transaction에서 IQC 검사 대기·품질 내 업무·인앱 알림으로 자동 인계한다.
6. 품질 IQC 내 업무의 `이동`은 해당 검사 attempt가 선택된 `/quality/iqc` 화면으로 이동한다.
7. 모든 활성 품질검사에서 부적합 최종화에는 사진 또는 근거가 필요하다. 기존 snapshot·감사·첨부 계약을 보존하는 최소 증빙 모델을 사용한다.
8. 검사 연계 Pending의 조치 UI는 `조치 시작`, 처리 내용 입력, `조치 완료`만 제공한다. `조치 완료`는 일반 종결이 아니라 재검사 요청을 뜻한다.
9. `조치 완료`와 같은 transaction 또는 멱등 orchestration에서 새 품질 재검사 attempt, 품질 정·부 담당자 인앱 알림과 내 업무를 만든다. 사용자가 별도 `재검사 요청` 버튼을 누르지 않는다.
10. Pending comment와 상태 변경 history는 한 chronological activity timeline으로 보여 주고, 처리 내용과 actor/time/status evidence를 잃지 않는다.
11. 검사에서 생성된 Pending은 attempt·report·receipt 또는 panel에 계속 연결된다. 일반 Pending 화면에서 임의 Closed로 우회할 수 없다.
12. 재검사 합격은 검사 attempt, receipt 또는 panel stage, linked Pending, workflow stage와 관련 work item을 원자적으로 갱신한다. 재검사 부적합은 같은 Pending을 재사용하고 새로운 중복 Pending을 만들지 않는다.
13. 전체 흐름에서 IQC·후속 품질 상태는 최신 persisted 검사 facts로 일관되게 계산하며 종결 뒤 `미시작`으로 회귀하지 않는다.
14. 패널 QR 화면은 발급 가능한 패널을 체크해 한 번에 발급할 수 있고, 체크 동작이 실제 eligibility와 일치하며, `보기`는 선택한 행 바로 아래에 펼쳐진다. 인쇄용 선택과 발급 정책은 사용자에게 중복 action으로 보이지 않게 설계한다.

## 3. 확인된 Repository 기준선과 Root Finding

- `PanelQrManager`는 checkbox를 `hasActiveQr`에만 활성화해 미발급 eligible panel을 선택할 수 없고 preview를 목록 끝에 한 번 렌더한다.
- 프로젝트 상세는 `initialSection={view.section ?? 'panels'}`라 첫 진입이 설계 탭이다.
- 생산계획 완료 후 기본 다음 업무는 설계 하나만 만들며, 확정 문서의 설계·구매 동시 선행 활성화와 다르다.
- 구매 완료는 required template match 또는 품목명 존재만으로 완료될 수 있어 활성 품목의 수량·단위·공급구분·예정일 완결성이 부족하다.
- `RegisterArrivalAsync`는 `Arrived`에서 commit하고 `RequestIqcAsync`를 별도 호출해야 한다.
- IQC work item target은 `Inspection` attempt이지만 `WorkflowStore.LinkUrlForWorkItem`에 IQC 분기가 없어 project workflow fallback으로 간다.
- `PendingStore.IsQualityInspectionPendingAsync`는 `panel_quality_inspection_attempts`만 검사하고 `material_iqc_attempts`를 제외해 IQC Pending 수동 Closed 우회가 가능하다.
- `ReinspectionRequested` Pending 전이와 material/panel 재검사 attempt·work item·notification 생성이 서로 다른 API 행동으로 끊겨 있다.
- 상세 IQC와 panel 품질검사는 template의 선택적 사진 규칙만 적용하며 부적합 전체에 대한 공통 사진/근거 gate가 없다.

## 4. Fable이 확정할 비차단 정책

아래 결정은 사용자에게 다시 묻지 않는다. Fable 1차가 선택지·trade-off·권장안을 제시하고 Codex review를 거친 Fable 2차 기획이 blocking decision 0으로 확정하면 자동 채택한다.

1. 구매 완료의 최소 필수 field와 사급/도급 차이. 단, 자재 도착·IQC·입고 확정은 구매 완료 조건에서 제외한다.
2. QR 선택 발급의 batch atomicity·상한·재시도와 이미 발급된 패널의 처리 방식.
3. 부적합 근거의 최소 모델: 사진 1장 이상 또는 구조화 근거 text 허용 조건, 기존 legacy attempt 처리.
4. Pending `조치 완료`의 자동 재검사 생성과 quality primary/secondary 할당·알림 idempotency.
5. 재검사 부적합 반복 시 같은 Pending lifecycle을 다시 열거나 같은 open Pending을 유지하는 정확한 상태 처리.
6. Pending activity timeline에서 comment와 status history의 ordering·filter·작성 UX.
7. 기존 `IQC 요청`·`재검사 요청` endpoint와 과거 `Arrived`/`FailedBlocked` row의 호환·복구 방식.

## 5. 정상·예외·복구 흐름

- 정상: 생산계획 확정 → 설계·구매 병행 업무 → 구매 완료 → 자재 도착 등록 → IQC 자동 요청 → 품질 검사 → 합격이면 입고 확정, 부적합이면 linked Pending → 조치 시작·처리 내용·조치 완료 → 품질 정·부 재검사 업무/알림 → 재검사 합격 → Pending·IQC·workflow 정합 갱신.
- 중복·동시성: 도착, QR batch 발급, Pending 조치 완료와 재검사 요청을 반복·동시 호출해도 receipt당 활성 요청, panel당 활성 QR, Pending당 해당 재검사 attempt/work item/notification이 하나로 수렴한다.
- validation: 부적합 증빙 부족, 구매 기본정보 부족, stale version, 권한·담당 불일치와 invalid state transition은 서버에서 안정적으로 차단하고 사용자가 고칠 field/action으로 focus한다.
- 복구: 기존 `Arrived`는 legacy IQC 요청 route로 복구 가능하고 기존 `FailedBlocked + ReinspectionRequested`는 멱등 재검사 handoff로 복구 가능하다. 이미 잘못 Closed된 IQC Pending의 자동 backfill은 destructive migration 대신 명시적 운영/fixture 복구 경계를 Fable이 권장한다.
- 부분 실패: 상태, attempt, work item, notification, event는 transaction 경계 안에서 함께 성공하거나 rollback한다. 실제 Teams/Mail 전송 성공은 포함하지 않고 기존 outbox 계약을 보존한다.

## 6. UX·접근성·모바일

- 자재 하위 탭은 PC 표를 그대로 축소하지 않고 모바일에서는 `입고 관리`의 다음 행동·지연·IQC 상태와 `키팅 관리`의 패널 준비 상태를 각각 compact card로 제공한다.
- QR 선택은 checkbox label·disabled reason·선택 수·발급 결과를 명시하고 keyboard/focus와 44px touch target을 보존한다. inline preview는 행과 aria-controls로 연결한다.
- Pending 화면은 상태 select나 임의 종결 버튼을 숨기고 현재 가능한 하나의 행동만 보여 준다. timeline은 상태·comment·검사 근거를 시간순으로 읽을 수 있어야 한다.
- IQC deep link는 attempt를 직접 선택하고, 권한이 없으면 같은 검사 내용을 조회 전용으로 보여 주며 잘못된 attempt는 안전한 empty/error로 처리한다.
- desktop과 390px에서 page-level horizontal overflow 0, loading·empty·error·success·conflict 복구, screen reader announcement를 검증한다.

## 7. 포함·제외 범위

### 포함

- 위 14개 사용자 확정 결과
- additive API 또는 schema가 필요한 경우 다음 migration 번호
- Backend authoritative 권한·CAS·transaction·idempotency·audit
- 기존 Task change 문서, unit/integration/Full-Stack E2E와 desktop/mobile screenshot
- 실제 역할 기반 프로젝트 전체 lifecycle에서 부적합→조치→재검사→합격→최종 완료 검증

### 제외

- 실제 Teams/Mail provider 호출, Persistent UAT runtime/migration
- QR public domain·실제 프린터·물리 부착 검증
- 품질 template의 현업 content 확정, object storage·바이러스 검사 신규 도입
- 모든 과거 운영 데이터의 자동 backfill·파괴적 보정
- 대표 repo·`main`·push·PR·merge

## 8. 성공 기준

- 실제 담당자가 별도 IQC 요청·재검사 요청 우회 없이 도착부터 재검사 합격까지 끊김 없이 진행한다.
- 품질 Pending을 수동 종결해 검사 상태만 남기는 우회가 서버와 UI에서 불가능하다.
- 조치 완료 1회로 품질 정·부 알림과 단일 내 업무가 생성되고 deep link가 해당 attempt를 연다.
- 재검사 합격 후 receipt/panel·Pending·work item·workflow가 모두 완료 사실을 보여 주며 새로고침 뒤 `미시작`으로 회귀하지 않는다.
- 구매 완료는 최소 업무 완결성을 보장하면서 자재·IQC 단계를 중복 차단하지 않는다.
- 발급 가능한 패널 선택 QR batch 발급과 행 inline preview가 keyboard·mobile에서도 동작한다.
- Backend 전체, Frontend lint/typecheck/unit/build, fresh/existing isolated migration, 실제 역할 Full-Stack lifecycle와 screenshot 검증을 통과한다.

## 9. 승인·안전 경계

- planningApprovedForExperiment: `true`
- implementationApprovedForExperiment: `true` — Fable 2차 기획의 blocking decision 0 조건
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 10. Fable 확인용 요약

- 해결할 문제: 실제 사용자 입력이 IQC Pending에서 끊기고 검사·Pending·업무·알림·workflow가 서로 다른 상태로 남는다.
- 확정 범위: 사용자 14개 요구, 설계·구매 병행 활성화, 자동 IQC·재검사 handoff, 증빙 gate, Pending 단순 UI/timeline, 자재 하위 탭, QR batch·inline preview.
- 불변조건: 담당 권한, 단계 완료 순서, 품질 합격만 검사 Pending 종결, transaction·idempotency·audit, main·Persistent UAT·provider 불변.
- Deferred 비차단 결정: 4장의 7개 항목은 Fable 권장안 자동 채택.
- Fable 판정: `COMPLETED_CONFIRMED`.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`
