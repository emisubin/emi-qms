# TASK-CI-COST-001 Change 001 — 변경 영향 기반 CI와 선택적 Azure release

## Task Identity Gate

- proposedTaskId: `TASK-CI-COST-001 Change 001`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 관찰·별도 승인 제품 Task`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-CI-COST-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 작은 변경에도 Backend·Frontend·Full-Stack 전체 검사가 직렬로 반복되고, 이미 PR에서 검증한 같은 Git tree를 `main`에서 다시 검사하며, Azure가 변경되지 않은 image와 migration까지 매번 교체하는 시간을 줄인다.
- Root Finding: 최근 성공 코드 PR의 평균 wall time은 약 38분 42초였고 Backend와 Full-Stack의 heavy 구간이 직렬 연결됐다. 동일 tree의 PR 검증 뒤 `main`이 평균 약 19분 09초를 다시 사용했으며 Azure 수동 release도 두 image와 migration을 항상 실행했다.
- 변경 경계: 일반 CI 변경 분류·job 의존성·always-run Gate, `main`의 검증된 PR tree 재사용, Azure 수동 release의 변경 범위 판별·선택적 image/migration 교체와 관련 shell 회귀만 포함한다.
- 보존 불변조건: 모호하거나 읽기 실패인 분류는 전체 검사·전체 release로 되돌아간다. migration·인증·runtime·dependency·공통 contract는 Backend·Frontend·Full-Stack을 모두 통과한다. 운영 release는 최신 `main` SHA·명시 승인·OIDC·immutable digest·baseline·rollback·공개 `200/401/401` 검사를 유지한다.
- 예상 산출물: 공통 변경 범위 판별기, `main` PR 검증기, CI/Azure workflow 보정, 선택 release 회귀, Task·Roadmap·implementation report 갱신.

## 사용자 승인 계약

- 사용자는 현재 검사·평균 시간·절감 가능 시간 분석을 확인한 뒤 권장 적용 순서대로 구현을 명시 승인했다.
- 일반 CI는 Backend·Frontend를 영향 경계별로 실행하고, 고위험 변경에만 Full-Stack을 실행한다.
- Full-Stack은 Frontend 빠른 검증 뒤 시작하되 Backend heavy test와는 병렬 실행해 정상 코드 PR의 직렬 대기를 줄인다.
- 동일 tree의 `main` 중복 skip은 활성 Ruleset이 `CI Gate`를 필수 검사로 강제하고, squash/merge commit tree와 성공한 PR head tree가 같을 때만 허용한다. 하나라도 확인하지 못하면 skip하지 않는다.
- Azure는 마지막 성공한 `main` release부터 현재 source까지의 누적 diff를 기준으로 변경된 image만 만들고, migration 파일이 바뀐 경우에만 migration job을 실행한다. 기준 run을 확정하지 못하면 전체 release로 되돌아간다.
- 제품 코드·DB schema·migration 내용·Azure resource 사양은 변경하지 않는다.
- local 구현·자동 검증은 승인됐다. commit·push·PR·merge·실제 Azure release는 별도 승인 대상이다.

## 원격 설정 경계

- `main-pr-only` Ruleset에 GitHub Actions 앱의 `CI Gate` required status check를 추가하는 것이 첫 적용 순서다.
- GitHub 설정 UI에서 `Require status checks to pass`와 GitHub Actions 출처의 `CI Gate`를 선택하고 사용자 재인증 뒤 저장했다.
- Ruleset readback에서 enforcement `active`, required check `CI Gate`, GitHub Actions integration ID `15368`을 확인했다.
- 저장소 구현은 이후에도 Ruleset readback이 실패하거나 계약과 다르면 `main` 중복 skip을 자동 비활성화하므로, 원격 설정 drift가 검증 누락으로 이어지지 않는다.
