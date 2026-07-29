# TASK-EXPERIMENT-PROMOTION-001 Task Identity Gate

- proposedTaskId: `TASK-EXPERIMENT-PROMOTION-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `EXPERIMENT_PROMOTION_INTEGRATION_UAT`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-EXPERIMENT-PROMOTION-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 완료된 experiment 계보를 사용자 최종 검수 완료 상태로 고정하고, 빈 공식 DB 기준으로 전체 migration과 회귀를 재검증한 뒤 GitHub PR을 통해 `main`에 승격한다.
- Root Finding 또는 정책 결정: 사용자가 기존 대표·실험 데이터 보존이 필요 없음을 확정했고, 서로 분리된 세 번째 `main` 병합 승인을 제공했다. Persistent UAT DB는 저장소 불변조건에 따라 삭제하지 않고 보존 격리하며 같은 공식 이름의 새 빈 DB로 교체한다.
- 변경·검증 경계: 실험 완료 원장과 Roadmap·승격 산출물, 기존 product commit 계보, migration `0001`~`0064`, Backend·Frontend·isolated Full-Stack·GitHub CI, 공식 UAT와 experiment 검수 DB/runtime.
- 보존할 불변조건: `main` 직접 개발·push 금지, 기존 Persistent UAT DB와 volume 보존, 실제 provider 비활성, secret·개인정보 비출력, 기존 main migration 수정 금지, PR CI 통과 전 merge 금지.
- 예상 산출물: Task 계약, 구현 보고서·SOP·사용자 설명·검수 체크리스트, 빈 DB migration·runtime 증빙, Ready PR와 merged `main`.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## Gate 상태

`PASS_CREATE`

Roadmap과 실험 완료 원장은 완료된 기능을 재구현하지 않고 별도 승격·통합·UAT Task로 처리하도록 지정한다. 같은 목적의 기존 Task·branch·open PR은 없으며, 사용자의 데이터 초기화·최종 검수 완료·세 번째 병합 승인으로 현재 Next Gate와 일치한다.
