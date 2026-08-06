# TASK-AZURE-DEPLOY-001 Change 018 — 승인형 GitHub 운영 배포 연결

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `APPROVAL_GATED_GITHUB_AZURE_RELEASE`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_REMOTE_MAIN_AZURE_APPROVAL_GATED_CD`
- 승인일: 2026-08-06
- 기준 commit: `3f6cda221b28d4bc974f5daddbfd2aace53023b9` (`origin/main`과 동일)
- 작업 branch: `fix/task-azure-deploy-001-release-workflow-018`
- 사용자는 원격 `main`을 Azure 운영 배포 원본으로 연결하되 운영 배포 전 명시 승인을 유지하는 권장 순서를 승인했다.

## Purpose identity

- 업무 목표: 승인된 `main` commit을 한 번의 수동 운영 release로 image 게시, migration, Backend·Frontend revision 교체와 상태 확인까지 연결한다.
- Root Finding: 기존 GitHub workflow는 immutable image 두 개만 ACR에 게시하며 migration과 Container Apps 교체를 수행하지 않는다. 또한 현재 private Repository의 Environment에는 필수 검토자 규칙이 없어 문서의 “Environment 승인” 표현과 실제 설정이 일치하지 않는다.
- 변경·검증 경계: 기존 OIDC·Environment secret·`main` ancestry guard를 유지하고, `workflow_dispatch`의 full SHA·명시 확인을 운영 승인으로 사용한다. migration job 성공 뒤에만 single-revision Backend·Frontend image를 순서대로 교체하고 Azure readiness와 익명 public health/security 상태를 검사한다.
- 보존할 불변조건: 자동 `push` 배포, mutable `latest`, Azure client secret, tracked 실제 resource name·hostname·identifier·secret, DB 수동 수정·삭제, Bicep 전체 재배포, Front Door·Entra·Teams·Gmail 설정, 알림 수신자·에스컬레이션 정책과 앱 기능은 변경하지 않는다.
- 예상 산출물: fail-closed release workflow와 입력·runtime guard, synthetic mock 검증, 운영자 절차, rollback/forward-fix 계약과 GitHub Environment 비식별 변수 목록.

## 구현 계약

1. workflow는 `workflow_dispatch`로만 실행하고 `main`에 포함된 full 40자 SHA, image 게시 확인과 migration·앱 교체 확인을 모두 요구한다.
2. OIDC로 Azure에 로그인하고 SHA tag의 Backend·Frontend image만 게시한다. mutable tag와 client secret을 사용하지 않는다.
3. GitHub Environment secret은 기존 값을 재사용하고 실제 resource 이름은 tracked 파일이 아닌 Environment variable로 주입한다.
4. 배포 전 대상 resource exact 존재, single revision mode, 현재 image와 public health 기준선을 확인한다.
5. migration job image를 새 Backend image로 갱신하고 새 execution의 `Succeeded`를 확인하기 전에는 Backend·Frontend를 변경하지 않는다.
6. Backend를 먼저 갱신해 latest revision이 Healthy/Running인지 확인한 뒤 Frontend를 갱신한다. single revision의 기존 traffic 보존 계약을 유지한다.
7. 최종 public health `200`, 익명 root·API `401`과 현재 image SHA 일치를 확인한다.
8. migration은 additive forward-fix 원칙을 유지한다. application revision 실패 시 직전 image로 best-effort rollback하고 결과를 stable code로 기록한다.

## 제외 범위

- 이 변경을 게시한 직후 실제 운영 release 실행
- GitHub 요금제 변경 또는 private Repository 필수 검토자 기능 구매
- Foundation·identity-access·workloads·edge Bicep 자동 적용
- infrastructure what-if·자동 traffic split·blue/green label 정책
- migration 생성 또는 기존 migration 수정
- Teams SSO·manifest·알림 정책 변경
- branch·worktree 정리

## 검증 계획

1. workflow syntax/actionlint와 pinned action·permission·trigger·concurrency를 검사한다.
2. 입력 guard의 정상/잘못된 SHA/`main` 미포함/확인 누락/resource 변수 오류를 synthetic fixture로 검증한다.
3. release script를 mock Azure/HTTP 경계에서 정상 migration→Backend→Frontend 순서, migration 실패 시 app mutation 0, Backend/Frontend 실패 rollback, health/security 실패를 검증한다.
4. Azure artifact validator, shell syntax·ShellCheck, Public Deployment Security 집중 test와 diff/PII/secret/allowlist를 검사한다.
5. 실제 GitHub Environment에는 secret 값 출력 없이 필요한 변수명만 등록하고, workflow 게시 뒤 실제 운영 release는 별도 명시 실행으로 남긴다.

## 종료 산출물 위치

| 산출물 | 위치 | 예정 상태 |
| --- | --- | --- |
| Implementation report | `tasks/azure-deploy-001-implementation-report.md` Change 018 section | 구현 뒤 갱신 |
| SOP | `tasks/azure-deploy-001-sop.md` | 운영 실행·실패 복구 절차 갱신 |
| User manual | `infrastructure/azure-pilot/README.md` | GitHub 운영 배포 절차 갱신 |
| Roadmap update | `docs/00-product-roadmap.md` | Change 018 상태·Decision Log 갱신 |
| User validation checklist | `tasks/azure-deploy-001-user-validation-checklist.md` | 자동 검증과 실제 release 분리 |

## 현재 구현·구성 결과

- Source 구현과 로컬 자동 검증: `PASS`
- Public Deployment Security 집중 test: `42/42 PASS`
- GitHub Environment variable: `4/4`, 실제 값 비출력
- OIDC identity: 기존 ACR `AcrPush`와 Backend·Frontend·migration exact resource 역할 `3/3`, 넓은 Contributor 추가 `0`
- 실제 운영 release: `NOT_RUN`, Change 018 원격 `main` 게시 뒤 별도 명시 실행
