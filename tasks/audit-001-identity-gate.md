# TASK-AUDIT-001 — Task Identity Gate

- proposedTaskId: `TASK-AUDIT-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `OPERATIONS_OBSERVATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-AUDIT-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 운영 사용자의 로그인 성공·실패·로그아웃과 인증된 사용자의 데이터 변경을 관리자 전용 감사 원장으로 연결해 누가 언제 무엇을 변경했는지 조회·Excel 내보내기할 수 있게 한다.
- Root Finding 또는 정책 결정: 현재 Azure HTTP 요청 집계와 일부 업무 테이블의 최종 수정자만으로는 로그인과 전체 변경의 행위자를 확정할 수 없다. 사용자는 전체 field-level audit 추적 항목을 현재 운영 관찰보다 우선하도록 순서 변경을 명시 승인했다.
- 변경·검증 경계: Microsoft Entra와 앱 인증 경계, append-only 감사 원장, 관리자 조회·Excel, additive migration, 격리 테스트, GitHub PR·main 병합과 exact main Azure 공개배포까지 포함한다. 실제 credential 원문과 기존 업무 데이터 원문은 증빙에 출력하지 않는다.
- 보존할 불변조건: 비밀번호·token·Authorization header·attachment binary/body는 기록하지 않는다. Backend authorization을 authoritative source로 유지하고 기존 audit 원장·업무 이력·알림·인증·외부 provider·운영 데이터를 삭제하거나 덮어쓰지 않는다.
- 예상 산출물: Fable interview·planning, Codex review, Backend·Frontend·migration·tests, 관리자 감사 조회·Excel, Implementation report 안의 SOP·user manual·checklist, Roadmap update, PR·CI·Azure release와 privacy-safe 운영 검증.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## Gate 근거

- 기존 `TASK-ADMIN-001`은 관리자 기준정보 변경과 업무 timestamp 조회만 포함하고 전체 field-level audit를 명시적으로 제외한다.
- 기존 `TASK-NOTIFY-AUDIT-001`은 알림 설정 감사 원장 조회만 포함하며 전체 field-level audit를 후속으로 둔다.
- Roadmap 추적 항목 48은 전체 field-level audit를 미확정 `Audit 후속`으로 관리한다.
- 사용자는 2026-08-27 현재 운영 관찰보다 이 Task를 우선하고 구현부터 공개배포까지 진행하도록 Roadmap 재정렬을 명시 승인했다.

## Procedure Finding

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `PRIVACY_AUTH_STATUS_RAW_OUTPUT` | P2 | `RESOLVED` | Fable 인증 확인 명령이 허용 projection 밖 계정 metadata를 terminal에 출력했다. Repository·Task artifact에는 저장되지 않았지만 privacy-safe evidence 절차를 위반했다. | 실제 값을 재인용하지 않고 폐기했다. 이후 인증 확인은 `loggedIn`, `authMethod`, `apiProvider`, `subscriptionType` fixed projection만 허용하며 Task privacy/secret scan과 publication gate에서 재확인한다. |
