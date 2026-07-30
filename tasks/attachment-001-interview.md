# TASK-ATTACHMENT-001 — Pending 조치 사진 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `WAIVED_BY_USER_FOR_EXPERIMENT`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

사용자는 모든 종류의 Pending 조치 완료 시 사진을 선택적으로 첨부하고, 품질 재검사 화면에서 최초 부적합 근거·사진과 조치 내용·사진을 같은 자리에서 확인하며 검사 항목을 판정하도록 요청했다. 이 experiment branch의 standing instruction에 따라 사용자-facing interview와 중간 승인을 생략하고 Fable 권장안을 자동 채택한다.

## Task Identity Gate

- proposedTaskId: `TASK-ATTACHMENT-001`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `첨부·사진 storage/검역/보존/backup·restore`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-ATTACHMENT-001`
- reuseExistingTask: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `첨부·사진 storage/검역/보존/backup·restore`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: Pending 조치자가 조치 완료 근거 사진을 남기고, 품질 재검사자가 원 부적합 근거·조치 내용·각 사진을 한눈에 확인한다.
- Root Finding: Pending은 text comment와 history만 저장하며 조치 사진 입력 UI가 정책 보류 안내로 남아 있다. 기존 IQC·패널 품질 사진은 검사 report 전용이라 Pending 조치 근거의 lifecycle을 표현하지 못한다.
- 변경 경계: Pending 조치 사진의 bounded persistence/API/권한/UI, 재검사 evidence 조회, 보존·백업·복구 계약과 자동 검증을 포함한다.
- 보존할 불변조건: 기존 IQC·LQC·OQC 실패 report·사진은 수정하지 않는다. 사진이 없어도 조치 완료는 가능하다. JPEG/PNG만 허용하고 장당 5MB 이하로 제한한다. 확정된 조치 사진은 append-only 근거로 보존한다.
- 예상 산출물: Fable 1차 기획, Codex review, Fable 2차 기획, additive migration, Backend/Frontend, migration·권한·파일 검증·재검사 회귀 테스트, 구현 보고서.

## 확정된 사용자 정책

1. 조치 사진은 선택 입력이다.
2. JPEG·PNG만 허용하고 장당 최대 5MB다.
3. 최초 부적합 근거·사진, 조치 내용·사진, 재검사 판정 UI를 위에서 아래로 한 흐름으로 표시한다.
4. 원본 사진은 덮어쓰거나 교체하지 않고 이력 근거로 남긴다.
5. 조치부서와 품질부서 외 다른 부서는 Pending을 조회하고 코멘트를 작성할 수 있지만 조치 사진 확정 권한은 조치 담당자에게만 둔다.
6. 대표 Repository, `main`, Persistent UAT, 실제 provider, push·PR·merge는 포함하지 않는다.

## Fable이 확정할 비차단 정책

1. 기존 IQC·패널 품질의 DB byte 저장·MIME 탐지·SHA-256·5MB 제한을 공통화할지 Pending 전용 bounded table로 재사용할지.
2. Pending 한 건 및 조치 회차당 사진 수·총 용량 상한.
3. 조치 시작 중 임시 업로드와 조치 완료 원자 확정, 실패·재시도·동시성 처리.
4. 삭제 허용 시점과 확정 뒤 append-only 규칙.
5. 읽기 권한과 content endpoint의 프로젝트 접근·Pending 접근 scope.
6. 백업·복구 기준, 보존 기간과 삭제된 프로젝트 lifecycle 연계.
7. 기존 text-only Pending과 기존 재검사에 대한 migration/backfill 방식.

## 성공 기준

- 조치 담당자는 조치 완료 전에 사진을 선택적으로 추가·삭제하고, 조치 완료와 동시에 사진 snapshot을 확정한다.
- 재검사 담당자는 원 부적합 근거·사진과 조치 내용·사진을 같은 화면에서 확인한다.
- 허용되지 않은 형식, MIME 위장, 5MB 초과, 권한 없는 업로드·조회, 확정 뒤 삭제가 서버에서 차단된다.
- 동일 요청 재시도와 동시 요청이 중복 사진·중복 이력을 만들지 않는다.
- 기존 text-only Pending과 기존 검사 사진·PDF는 깨지지 않는다.
- Open P0/P1/P2가 0이며 main·Persistent UAT·실제 provider는 변경하지 않는다.

## 승인·안전 경계

- planningApprovedForExperiment: `true`
- implementationApprovedForExperiment: `true` — Fable 2차 기획의 blocking decision 0 조건
- localCommitApproved: `false` — 이번 사용자 요청에 commit 지시는 없음
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
