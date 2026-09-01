# TASK-SITE-ACCESS-001 — Task Identity Gate

- proposedTaskId: `TASK-SITE-ACCESS-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AUDIT-001`
- roadmapNextGate: `POST_DEPLOYMENT_INTERACTIVE_LOGIN_VALIDATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-SITE-ACCESS-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: System Administrator가 새 Microsoft 인증뿐 아니라 기존 인증 세션으로 PMS에 들어온 사용자의 사이트 접속을 별도 사건으로 확인한다.
- Root Finding 또는 정책 결정: `TASK-AUDIT-001`은 대화형 로그인만 기록하고 silent token·restored account·자동 세션 갱신은 의도적으로 제외하므로, 유지 세션을 통한 실제 사이트 접속을 확인할 수 없다. 사용자는 로그인 기록이 아니라 사이트 접속 기록이 필요하다고 명시했고 별도 신규 기능 구현과 Roadmap 순서 변경을 승인했다.
- 변경·검증 경계: 사이트 접속 세션의 시작·활동·종료 또는 만료 기준, 중복 방지, 관리자 조회·Excel 표시, additive migration/API/Frontend 계측과 isolated 검증을 기획 대상으로 한다. 확정 범위는 Fable deep-interview와 사용자 요약 확인 뒤 결정한다.
- 보존할 불변조건: 기존 대화형 `Login`·명시적 `Logout` 의미, `Audit.Read.All`, append-only와 과거 소급 금지, 기존 업무 권한, 비밀번호·token·Authorization header·cookie·raw request/response 미기록, 개인정보 최소화와 기존 운영 인증을 보존한다.
- 예상 산출물: Fable interview·planning 원문, Codex review, 승인된 구현, additive migration과 Backend/Frontend/tests, Implementation report·SOP·User manual·Roadmap update·user validation checklist.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 검색 결과와 판정

- 기존 `TASK-AUDIT-001`은 직접 Microsoft 인증과 데이터 변경 감사가 목적이며 자동 세션 갱신·restored account를 명시적으로 제외한다. 이번 사이트 접속 세션은 신규 사용자 능력·신규 사건 lifecycle이므로 기존 Change로 재사용하지 않는다.
- 같은 목적의 Task·branch·worktree·PR은 확인되지 않았다.
- 사용자 원문 `추가구현 승인`은 신규 기능 착수 의사로 기록했고, 후속 질문에 대한 사용자 원문 `네 승인`으로 기존 `TASK-AUDIT-001`의 공개 로그인 재검수보다 이 신규 기능을 먼저 진행하는 Roadmap 순서 변경을 명시 승인했다.
- 따라서 `explicitRoadmapOverrideApproved=true`로 `PASS_CREATE`한다.
