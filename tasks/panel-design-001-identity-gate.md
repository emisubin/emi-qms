# TASK-PANEL-DESIGN-001 — Task Identity Gate

- proposedTaskId: `TASK-PANEL-DESIGN-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-PANEL-DESIGN-001`
- roadmapNextGate: `CODEX_PLANNING_AND_IMPLEMENTATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-PANEL-DESIGN-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION_CODEX_OVERRIDE`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 일반 Item의 설계 담당자가 패널별 도번과 개별 W/H/D를 입력하고, 함께 출하되는 패널 묶음을 지정하며, 프로젝트 설계 탭에서 개별 정보와 W 합산 묶음 크기를 한눈에 확인한다.
- Root Finding 또는 정책 결정: 기존 `TASK-003B`는 Excel의 도번 열을 의도적으로 무시하고 패널 묶음 데이터 개념을 제외했다. 사용자는 구매품별 IQC·LSE TASK NO·부서 Pending 다음 우선순위로 이 후속 기능 구현을 명시했다.
- 변경·검증 경계: 일반 Item 패널정보의 저장·조회·Excel·감사·동시성, 설계 입력/조회 Desktop·390px UX, additive migration과 격리된 synthetic 검증을 포함한다. UL891 설계 모델과 실제 운영 DB·Azure runtime·외부 provider는 제외한다.
- 보존할 불변조건: UL891 패널 묶음 제외, 기존 프로젝트와 기존 패널정보 보존, 패널별 W/H/D 원본 유지, 묶음 W만 합산, 서버 권한·validation authoritative, 현재 흑백 wireframe·일반 테두리·강조선 금지, QR·제조·품질·물류 workflow 의미 무변경.
- 예상 산출물: 사용자 예외 지시에 따른 Codex planning, additive migration·Backend/Frontend 구현·자동 테스트, Implementation report, SOP/User manual 위치 추적, 사용자 검수 checklist와 Roadmap 갱신.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 검색 결과와 판정

- `TASK-003B`는 패널명·W/H/D와 Excel 기능의 기존 기준선이며 도번 저장과 패널 묶음을 명시적으로 제외한다.
- `TASK-UL891-SET-001`은 세트 기반 별도 설계 모델이며 사용자가 이번 묶음 기능에서 UL891을 명시적으로 제외했다.
- 관련 문서에는 이 기능이 `설계 도번·필수값·패널 묶음` 후속 작업으로만 남아 있고, 같은 목적의 Task·branch·worktree·open/merged PR은 없다.
- 최신 원격 main 자체의 다음 기본 Gate에는 아직 이름이 없지만, 사용자 승인 하에 진행 중인 `TASK-PROJECT-PENDING-001` Roadmap은 사용자 검수 뒤 우선순위 3 구현을 다음 Gate로 고정했고 사용자가 이번 turn에서 실행을 명시했다.
- 따라서 새 canonical Task `TASK-PANEL-DESIGN-001`을 `PASS_CREATE`로 확정한다.
- 사용자는 Task 생성 직후 `Fable 말고 Codex가 직접 기획하고 구현`하도록 명시했다. 이번 Task에 한해 Fable 호출 없이 Codex planning과 구현으로 진행하며 다른 Task의 기본 규칙은 변경하지 않는다.

## 격리 기준선

- branch: `feat/task-panel-design-001-grouping`
- base: `origin/main` SHA `af796547ffb260ae427932a4734894af23c21ae6`
- worktree: `/private/tmp/emi-qms-panel-design-001`
- 사유: 우선순위 1·2와 관리자 홈 수정의 사용자 검수본이 각각 다른 dirty worktree에 보존돼 있어 동시 write와 migration 충돌을 막아야 한다.
- 예상 종료: 기획·구현·자동 검증·사용자 검수 서버 handoff까지. Commit·push·PR·merge·공개배포와 worktree 정리는 별도 사용자 승인 대상이다.
