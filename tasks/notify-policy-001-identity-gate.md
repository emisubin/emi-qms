# TASK-NOTIFY-POLICY-001 Task Identity Gate

- proposedTaskId: `TASK-NOTIFY-POLICY-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001 Change 021`
- roadmapNextGate: `AZURE_RELEASE_IN_PROGRESS`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-NOTIFY-POLICY-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 현재 알림의 채널·수신자·발송 시점·소음 방지·운영 활성화 정책을 실제 구현과 일치시키고, 사용자 결정을 거쳐 필요한 보정을 구현한다.
- Root Finding 또는 정책 결정: Roadmap의 과거 event coverage와 현재 자동 Teams·메일 delivery가 어긋나며, Daily Digest·예정일 에스컬레이션·Teams 통합 채널은 구현 상태와 운영 활성 상태가 분리돼 있다. 일반 Pending 메일, 제조 중단 중복, Pending 종결, 프로젝트 완료 메일과 업무 담당자 fallback도 운영 결정을 요구한다.
- 변경·검증 경계: 먼저 기존 기능의 `POLICY_DECISION`을 확정한다. 승인된 보정만 Backend·Frontend·설정·문서·테스트에 반영한다. Web Push 또는 신규 provider는 별도 `NEW_FEATURE`로 분리한다.
- 보존할 불변조건: 인앱 알림은 원본으로 유지하고, 외부 provider 실패가 업무 transaction을 되돌리지 않으며, idempotency·retry·attempt lineage·수신자 권한·실제 provider 무발송 테스트 경계를 보존한다.
- 예상 산출물: 정책 원장, 승인된 구현·검증, Implementation report, SOP, User manual, Roadmap update와 user validation checklist.

## Task type 재분류

- 최초 분류: `POLICY_DECISION`
- 현재 분류: `NEW_FEATURE`
- 사유: 확정 정책에 복수 부서장 fallback 업무의 동기화 종료와 일정 원본 기반 `work_items.due_date` 자동 동기화라는 신규 사용자 업무 흐름·상태 전이가 포함됐다.
- Identity 결과: 업무 목적은 기존 알림 운영 정책 정합화와 동일하므로 canonical Task와 branch를 재사용한다.

## 검색 범위

- [x] `tasks/`의 TASK-NOTIFY-001~005, notify reliability·escalation·audit·reprocess 산출물
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

동일한 전체 운영 정책 정합화 목적의 canonical Task·branch·PR은 확인되지 않았다. 현재 Roadmap Gate와 병렬 진행하는 사용자의 명시적 승인에 따라 새 Task를 생성한다.
