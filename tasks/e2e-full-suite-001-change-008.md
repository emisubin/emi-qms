# TASK-E2E-FULL-SUITE-001 Change 008 — 확정 정책 기준 E2E 갱신

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `83662623e32becfc4f41b085642af747c18f2ac3`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-E2E-FULL-SUITE-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 실제 담당자 18단계 E2E가 현재 확정된 구매 수량 입력, 선택 키팅, 제조 투입 요청, 품질 판정 단위, 프로젝트 상세 탭 구조를 그대로 검증하게 한다.
- Root Finding: 기존 E2E 일부가 도착 시 발주 수량 입력, 키팅 완료 즉시 제조 업무 생성, 삭제된 자재 탭과 전진검수·FAT 체크리스트를 기대해 현재 제품 정책과 어긋난다.
- 변경·검증 경계: 기존 synthetic/disposable full-stack 흐름의 입력·기대값·탭 캡처만 갱신한다. 제품 정책을 E2E에 맞춰 되돌리지 않는다.
- 보존할 불변조건: 역할별 실제 UI 입력, disposable PostgreSQL, 실제 provider 비호출, 중복 업무·알림 0건과 최종 정산 흐름을 유지한다.
- 예상 산출물: 현재 정책으로 끝까지 통과하는 18단계 full-stack E2E와 실패 증빙.

## 확정 E2E 계약

1. 구매팀이 일반 구매품의 발주 수량·단위를 입력하고 자재팀은 도착 수량만 입력한다.
2. 자재 도착은 IQC 업무를 자동 생성한다.
3. 키팅은 선택적 현황 공유이며 제조 업무를 직접 생성하지 않는다.
4. 생산관리의 `제조 투입 요청`이 제조 담당 업무를 생성한다.
5. OQC는 항목별 판정, 전진검수·FAT는 패널 통합 판정으로 입력한다.
6. 부적합 품질검사는 Pending으로 이동하고 재검사 적합 시 같은 Pending이 종결된다.
7. 프로젝트 상세 캡처 탭은 `전체 흐름·생산관리·설계·구매·제조·품질·물류`와 영업 사용자 전용 `영업`이다.
8. 프로젝트 상세 첫 진입은 `전체 흐름`이다.

## 실행 경계

- implementationApproved: `true`
- userValidationCompleted: `false`
- fableInvocationRequired: `false`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
