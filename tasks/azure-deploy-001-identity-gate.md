# TASK-AZURE-DEPLOY-001 Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `Azure 서비스 선정과 provider-specific 운영 전환 Scope Review`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 승인된 20일 Azure 시범 운영 구성을 실제 배포할 수 있는 provider-specific 코드와 운영 절차로 전환한다.
- Root Finding 또는 정책 결정: `OPS-PILOT-002`와 `OPS-PILOT-004`의 hosting·managed DB·storage·observability·edge·rollback 미구현을 해소해야 한다. 사용자는 비용 관련 작업을 직접 수행하며, Codex는 비용 발생 가능 작업 전에 생성 대상과 예상 비용을 먼저 보고하고 중단한다.
- 변경·검증 경계: 비용 없는 로컬 IaC·Container Apps 호환 image·Teams manifest·preflight·runbook 구현과 정적 검증을 포함한다. Azure provider 등록, resource 생성, image push, migration 실행, DNS·traffic 전환과 실제 provider 발송은 비용 사전 보고 전에는 제외한다.
- 보존할 불변조건: Entra SPA/API 앱 분리, Backend·ClamAV 비공개, Production fail-closed upload, one-shot migration, secret 비추적, PostgreSQL 첨부 현행 구조와 worker 단일 실행 보장.
- 예상 산출물: Azure 배포 template, Azure 전용 Production image 구성, Teams manifest package builder, 비용 중단선이 있는 배포·복구 SOP, 로컬 검증 결과와 유료 실행 직전 checklist.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## Gate 판단

`TASK-AZURE-PILOT-001`은 서비스 중립 준비까지 완료했고 Roadmap의 다음 Gate는 provider-specific 운영 전환이다. `TASK-AZURE-DEPLOY-001`에서 Azure 서비스 선정이 이미 진행됐으며, 사용자가 20일 시범 배포 구성을 승인하고 배포 시작을 명시했다. 새 Task를 만들지 않고 같은 canonical Task의 실행 phase로 재사용한다. 미완료 Fable interview 원문은 보존하지만 사용자의 명시적 Fable 제외 결정에 따라 구현 source로 사용하지 않는다.
