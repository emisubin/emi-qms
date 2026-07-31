# TASK-AZURE-PILOT-001 Task Identity Gate

- proposedTaskId: `TASK-AZURE-PILOT-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 전환 Task와 rollback 승인`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-AZURE-PILOT-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 한 달간의 회사 파일럿 전에 서비스 선정과 무관한 게시·인증·migration P1을 닫고, Azure 서비스 선정이 필요한 운영 항목은 별도 정책 결정으로 분리한다.
- Root Finding 또는 정책 결정: `OPS-PILOT-001`·`OPS-PILOT-003`과 독립 migration P1을 해소하고, `OPS-PILOT-002`·`OPS-PILOT-004`는 사용자의 서비스 선정 보류를 후속 Gate로 고정해 `TASK-UAT-001`의 `NO_GO_EXTERNAL` 운영 handover를 이어받는다.
- 변경·검증 경계: GitHub 게시 후보, 분리 Entra Production 설정 계약, 독립 migration 실행·검증과 배포 전 점검을 포함한다. 특정 Azure hosting·managed DB·WAF·SIEM 선정과 IaC, 실제 사용자 트래픽 전환, 운영 업무 데이터 입력, Teams·메일 실발송과 `main` merge는 제외한다.
- 보존할 불변조건: Development UAT·experiment runtime과 DB 무변경, Production Entra·Host·TLS·WAF·rate limit·fail-closed upload 유지, migration additive·forward-fix, secret 원문 비추적, 사용자 검수 전 Draft 게시.
- 예상 산출물: 분리 Entra Production 구성, 독립 migration 실행 경로, 서비스 중립 사전점검, 사용자 검수 checklist, Implementation report와 Roadmap 갱신.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## Gate 판단

`TASK-UAT-001` Change 004~006은 애플리케이션·Compose 방어선과 로컬 Entra 검수까지 완료했지만 actual hosting·managed DB·restore·SIEM·WAF를 명시적으로 별도 운영 전환 Task에 남겼다. Roadmap 6.1도 Task ID 미정의 운영 전환을 현재 `Next Gate`로 지정한다. 같은 purpose의 기존 Task·branch·PR은 없고 사용자가 P1 전체 해결을 승인해 새 canonical Task를 생성했다. 이후 사용자가 Azure 서비스는 미선정 상태이므로 서비스 선택이 필요한 작업을 이번 Task에서 제외하라고 명시해 provider-specific 범위를 제거했다.
