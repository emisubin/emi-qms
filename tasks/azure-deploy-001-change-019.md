# TASK-AZURE-DEPLOY-001 Change 019 — Azure 정상 revision 상태 판정 보정

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `APPROVAL_GATED_GITHUB_AZURE_RELEASE`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `EXISTING_AZURE_RUNTIME_CONTRACT`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_FIX_TEST_PUBLISH_MERGE_AND_RERUN_APPROVAL`
- 승인일: 2026-08-07
- 기준 commit: `345108afa36ead8c045c4f31df6ebafc044af1fa` (`origin/main`과 동일)
- 작업 branch: `fix/task-azure-deploy-001-running-state-019`

## 재현 결과와 Root Finding

- 첫 실제 운영 release run `31140991285`는 Backend·Frontend immutable image 게시 뒤 `azurePilotRelease=BASELINE_NOT_READY`로 중단됐다.
- 중단 시점은 migration job update/start와 Backend·Frontend revision 교체 전이므로 운영 DB·앱 runtime mutation은 `0`이다.
- 현재 Backend·Frontend는 single revision, latest revision ready, provisioning `Succeeded`, health `Healthy`이고 Azure가 정상 실행 상태를 `RunningAtMaxScale`로 반환한다.
- Release script는 `Running`만 정상으로 인정해 이 정상 기준선을 장애로 오판했다. Finding ID는 `AZURE-RELEASE-RUNNING-STATE-001`, 등급은 `P1`이다.

## 승인 구현 계약

1. Revision health는 기존처럼 exact `Healthy`여야 한다.
2. Running state는 exact `Running` 또는 Azure Container Apps의 정상 최대 scale 상태인 `RunningAtMaxScale`만 허용한다.
3. `Stopped`, `ScaleToZero`, `Degraded`, `Unknown`과 빈 값은 계속 `BASELINE_NOT_READY`로 fail-closed한다.
4. 거부 상태에서는 migration·Backend·Frontend mutation을 시작하지 않는다.
5. Mock 회귀에 기존 정상·실패·rollback 순서와 새 허용·거부 상태를 함께 고정한다.
6. 수정 source를 commit·push·PR·CI·원격 `main`에 병합한 뒤 최신 main full SHA로 실제 운영 release를 다시 실행한다.

## 제외 범위

- Migration, Backend·Frontend 제품 source, DB schema·data 변경
- Azure resource mode·scale 설정, Front Door·Entra·Teams·Gmail 구성 변경
- `RunningAtMaxScale` 이외의 새 상태를 추정해 허용하는 변경
- 자동 `push` 배포, mutable tag 또는 권한 확대

## 검증 계획

1. Shell syntax·ShellCheck와 release mock 전체 scenario를 통과한다.
2. `Running`과 `RunningAtMaxScale`은 migration → Backend → Frontend 순서로 성공한다.
3. `Stopped`, `ScaleToZero`, `Degraded`, `Unknown`은 exit `70`, stable code `BASELINE_NOT_READY`, mutation call `0`을 확인한다.
4. Azure artifact compile·정적 계약, workflow actionlint, public deployment security 집중 test와 diff·allowlist·privacy 검사를 통과한다.
5. PR·merge SHA CI를 확인하고 최신 `main` full SHA의 actual release가 migration·Backend·Frontend·public security `4/4 PASS`인지 검수한다.

## 현재 상태

- Root cause 재현: `PASS`
- 최소 source 보정: `COMPLETE_LOCAL`
- Mock 회귀: `11/11 PASS`
- Git 게시·원격 main 병합: `PENDING`
- 실제 운영 release 재실행: `PENDING`
