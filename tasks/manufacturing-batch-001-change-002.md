# TASK-MANUFACTURING-BATCH-001 Change 002 — 조립 단계만 일괄 완료

## 사용자 수정 지시

사용자는 기존 구현이 조립 단계 이전의 미입력 제조 단계까지 완료 처리해 중간 단계가 사라지는 문제를 지적했다. 요구 기능은 제조 전체 완료나 조립까지의 누적 완료가 아니라, 선택한 여러 패널의 **조립 단계 한 항목만** 일괄 완료하는 기능이다.

사용자는 이 수정에서 Claude/Fable을 사용하지 말고 Codex가 직접 처리하도록 명시했다.

## 분류와 우선순위

- canonicalTaskId: `TASK-MANUFACTURING-BATCH-001`
- taskType: `BUGFIX`
- change: `002`
- rootFinding: `MFG-BATCH-PREDECESSOR-OVERCOMPLETION`
- severity: `P1`
- implementationApproved: `true` — 사용자 직접 수정 지시
- fableInvocation: `0`
- mainMergeApprovalCount: `0/3`

## 수정 계약

1. batch는 각 대상 execution에서 semantic item code `MANUFACTURING`에 대응하는 조립 snapshot step 한 건만 확인한다.
2. 조립 전 단계와 조립 후 자체검사는 현재 상태를 그대로 유지한다.
3. 조립 단계가 먼저 완료된 비연속 상태를 허용한다. 이후 기존 단건 입력은 아직 미완료인 첫 단계부터 계속 진행하고, 모든 제조 단계가 완료되기 전에는 제조 완료할 수 없다.
4. 한 패널마다 `StepChecked` event 한 건과 execution version `+1`만 기록한다.
5. 확인 화면과 성공 안내는 “조립 단계만 완료하고 다른 제조 단계는 유지”됨을 명시한다.
6. 버튼은 `선택 패널 조립 단계 완료`로 표시해 제조 또는 패널 전체 완료로 오해하지 않게 한다.
7. 기존 원자 처리·권한·project scope·expected version·replay·audit correlation은 유지한다.
8. 전체 흐름 네 표시명 변경은 그대로 유지한다.

## 검증 기준

- 2패널 batch 결과는 조립 step 2건, batch event 2건, 각 execution version `1→2`다.
- 두 패널의 조립 전 단계와 자체검사는 모두 미완료다.
- 이후 미완료 단계를 순서대로 단건 확인하면 제조 완료와 패널별 LQC 인계가 정상 동작한다.
- 혼합 부적격 payload는 기존처럼 전체 rollback한다.
- Desktop·Mobile 확인 화면에서 “조립 단계만 완료” 안내가 보인다.

## 게시 경계

- 현재 experiment worktree 안에서만 수정한다.
- 대표 repo·`main`, push, PR, merge, Persistent UAT, 실제 provider를 변경하지 않는다.
- 선행 Task mixed WIP와 공유 파일이 있어 안전한 exact commit 전까지 `COMMIT_PENDING`을 유지한다.
