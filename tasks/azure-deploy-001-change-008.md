# TASK-AZURE-DEPLOY-001 Change 008 — Teams v1.19 manifest schema 보정

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `Edge·DNS·TLS·Teams catalog·provider 검수`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_TEAMS_MANIFEST_SCHEMA_FIX`
- 승인일: 2026-08-05
- 기준 branch: `origin/main`
- 작업 branch: `fix/task-azure-deploy-001-teams-manifest-schema`
- 실제 Teams catalog 게시·provider 발송: `제외`
- Azure·DNS·TLS mutation: `제외`

## Purpose identity

- 업무 목표: Teams Admin Center가 Change 006 package를 Microsoft Teams manifest v1.19 schema로 정상 파싱하도록 한다.
- Root Finding: template이 v1.19 schema에 정의되지 않은 최상위 `packageName`을 포함해 `additionalProperties: false` 검증에서 거부된다.
- 변경·검증 경계: `packageName` 제거, 재발 방지 package test, 기존 package 구조와 정적 Azure artifact 회귀 검증을 포함한다.
- 보존할 불변조건: manifest ID·version 입력 방식, Teams Activity 권한·activity type, final hostname placeholder, web application identity, icon과 PWA 자산은 변경하지 않는다.
- 예상 산출물: v1.19 schema-compatible manifest package와 `packageName` 재유입을 차단하는 자동 검증.

## 승인된 수정 범위

1. `infrastructure/teams/manifest.template.json`의 최상위 `packageName`을 제거한다.
2. package test가 생성된 manifest에 `packageName`이 없음을 확인한다.
3. 기존 package entry·icon byte equality·manifest 계약과 Azure artifact validation을 재실행한다.
4. 실제 Teams Admin Center 재등록은 수정 package 게시 뒤 사용자 검수로 남긴다.

## 제외 범위

- Teams manifest ID·Entra identifier·resource-specific permission·activity type 변경
- Teams 조직 catalog 등록·설치·actual Activity Feed 발송
- 아이콘·PWA·Frontend·Backend·DB 변경
- Azure image·Container Apps·Front Door·DNS·TLS 변경

## 변경 Allowlist

- `infrastructure/teams/manifest.template.json`
- `scripts/test-teams-manifest-package.sh`
- `tasks/azure-deploy-001-change-008.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 완료 기준

- 생성된 manifest에 최상위 `packageName`이 없다.
- package가 `manifest.json`, `color.png`, `outline.png` 세 파일만 포함한다.
- 기존 v1.19 manifestVersion, Activity 권한·activity type, hostname·identity placeholder와 icon 계약이 유지된다.
- Teams package 정상·negative test, Azure artifact static validation, shell syntax와 `git diff --check`가 통과한다.
- Teams Admin Center 실제 재등록은 사용자 검수 대기로 명시한다.
