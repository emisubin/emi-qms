# TASK-TEAMS-PWA-001 — Task Identity Gate

- proposedTaskId: `TASK-TEAMS-PWA-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `TEAMS_SSO_NEW_MANIFEST_PLANNING`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 웹과 Teams 안에서 같은 `EMI PMS`로 인식하고, Teams에서는 별도 로그인 버튼 문제 없이 조직 계정으로 진입하며, 웹에서는 설치 가능한 PWA로 사용할 수 있는 통합 사용자 경험을 기획한다.
- Root Finding 또는 정책 결정: 기존 웹 Entra 로그인·Frontend 사전 인증·Teams Activity Feed·Teams manifest·PWA 정적 자산은 존재하지만 Teams tab SSO와 설치형 PWA의 사용자 흐름은 기획·구현되지 않았다. 사용자는 공식 표시명을 `EMI PMS`로 변경하고 Teams·PWA 아이콘을 흰 바탕의 빨간 EMI 로고로 통일했다.
- 변경·검증 경계: Teams SSO와 manifest, 웹 PWA 설치 경험, 사용자 표시명·브랜드 metadata, 기존 웹 인증과 Teams Activity deep link의 연결을 기획한다. 실제 provider·운영 Entra·Teams catalog·Azure runtime 변경과 구현은 포함하지 않는다.
- 보존할 불변조건: Backend bearer·앱 역할 권한이 authoritative하며 Entra 조건부 액세스·MFA를 우회하지 않는다. 기존 Activity Feed event identity와 실제 알림 발송 계약을 보존한다. token·tenant/client identifier·secret은 추적 파일에 기록하지 않는다. 기존 품질 기능 dirty WIP와 검수 runtime을 변경하지 않는다.
- 예상 산출물: Fable deep-interview 원문과 canonical interview, Fable primary planning, Codex 내용 review, 이후 구현 범위·검증·rollout·rollback 계약.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## Gate 근거

- Roadmap은 Teams SSO·새 manifest를 Azure 배포 이후의 별도 `NEW_FEATURE` Next Gate로 지정한다.
- `TASK-INFRA-001`은 일반 웹 Microsoft 365 로그인, `TASK-NOTIFY-003`은 Teams Activity Feed, `TASK-AZURE-DEPLOY-001`은 기존 manifest·정적 PWA 자산과 운영 배포를 완료한 선행 기반이다.
- 완료된 선행 Task들은 Teams tab SSO와 설치형 PWA 사용자 흐름을 명시적으로 제외하거나 후속 신규 기능으로 남겼다.
- 동일 목적의 Task ID, planning, branch, worktree와 PR은 확인되지 않았다.
- 현재 canonical clone의 품질 기능 dirty WIP와 별도 검수 runtime을 보존하기 위해 최신 `origin/main` 기준의 bounded temporary planning worktree를 사용한다.
