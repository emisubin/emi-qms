# Task Identity Gate

새 Task·branch·worktree·planning 파일을 만들기 전에 작성하는 fixed projection이다. 값은 실제 Repository와 Git/GitHub 상태를 확인한 결과만 사용한다.

- proposedTaskId: `<TASK-ID>`
- taskType: `<fixed enum>`
- instructionChainRead: `false`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `<TASK-ID | NONE>`
- roadmapNextGate: `<fixed enum or canonical Task ID>`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `<TASK-ID | NONE | AMBIGUOUS>`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `BLOCKED`

## Purpose identity

- 업무 목표:
- Root Finding 또는 정책 결정:
- 변경·검증 경계:
- 보존할 불변조건:
- 예상 산출물:

## 검색 범위

- [ ] `tasks/`의 Task·planning·review·change·implementation report
- [ ] Product Roadmap 실행 큐·추적 항목·Decision Log
- [ ] Local/remote branch와 worktree
- [ ] Open/merged PR

## Gate 상태

허용 값은 다음과 같다.

- `PASS_REUSE`: 같은 목적의 canonical Task 하나를 재사용하며 Roadmap 순서도 일치하거나 명시적 override가 승인됐다.
- `PASS_CREATE`: 같은 목적이 없고 Roadmap 순서가 일치하거나 명시적 override가 승인돼 새 Task를 만들 수 있다.
- `BLOCKED_SEQUENCE`: Roadmap의 현재 Task 또는 Next Gate와 일치하지 않는다.
- `BLOCKED_AMBIGUOUS`: 같은 목적 후보가 둘 이상이거나 canonical Task를 확정할 수 없다.
- `BLOCKED_ID_COLLISION`: 제안한 Task ID가 기존의 다른 목적에 이미 사용 중이다.
- `BLOCKED_INCOMPLETE`: 검색 범위나 source of truth를 충분히 확인하지 못했다.

`PASS_REUSE`이면 새 Task ID를 만들지 않고 `canonicalTaskId`의 다음 `change-###`를 사용한다. `PASS_REUSE`와 `PASS_CREATE` 외에는 Fable 호출, 새 Task·branch·worktree·planning 파일 작성을 시작하지 않는다.
