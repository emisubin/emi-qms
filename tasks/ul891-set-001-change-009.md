# TASK-UL891-SET-001 Change 009 — 버전 없는 현재 설계와 위치 기반 반복 사양

## Task Identity Gate

- proposedTaskId: `TASK-UL891-SET-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `TASK-AZURE-DEPLOY-001_CHANGE_005_PUBLISH_AND_WORKLOAD_READINESS`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UL891-SET-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: UL891 설계를 일반 아이템처럼 현재 값 하나를 계속 수정·저장하고, 한 세트 안에서 같은 사양의 패널이 여러 위치에 반복될 수 있게 한다.
- Root Finding 또는 정책 결정: 사용자에게 불필요한 V1·Draft·Published·새 수정본 개념과 `code` 고유 제약이 노출된다. 구성 code를 물리 패널 identity로 사용해 동일 사양 반복이 새 패널 생성으로 오인되고 취소 이력까지 현재 설계에 표시된다.
- 변경·검증 경계: UL891 현재 설계 저장 계약, 세트 내 위치 identity, 반복 사양, 현재 활성 패널 projection과 프로젝트 생성·사양 추가 입력을 보정한다.
- 보존할 불변조건: 실제 활성 패널의 영구 ID·번호·제조/품질/물류/QR 이력, 취소 패널 감사 이력, 세트 인스턴스와 수량 변경, 비-UL891 설계 흐름, 권한·CAS·audit를 유지한다.
- 예상 산출물: additive migration과 backfill, 현재 설계 API·Backend store, version/code 없는 조회·수정 UI, 중복 사양·기존 42면 검수 회귀, 자동·브라우저 검증과 구현 보고.

## 사용자 승인 계약

1. 사용자 화면과 정상 업무 흐름에서 V1, Draft, Published, Superseded, 임시저장, 새 수정본 만들기, 저장된 버전 적용을 제거한다.
2. 일반 아이템과 같이 `수정 → 저장 → 다시 수정`으로 같은 현재 설계를 계속 편집한다.
3. `code`는 사용자가 입력하거나 이해해야 할 업무 값이 아니다.
4. 세트 구성의 identity는 사양 문자열이 아니라 세트 안의 고정 위치다.
5. 서로 다른 위치에 같은 패널명·치수 등 동일 사양을 반복 입력할 수 있다. 예: `A-B-C-D-C-B-F`.
6. 위치의 설계 값만 바꾸는 저장은 물리 패널을 새로 만들거나 취소하지 않고 기존 패널 identity를 보존한다.
7. 위치를 실제로 추가·삭제할 때만 각 세트에 물리 패널을 추가·취소하며 번호는 재사용하지 않는다.
8. 현재 화면에는 활성 위치와 활성 물리 패널만 표시하고 취소 이력은 감사·과거 데이터로 보존한다.
9. 기존 검수 프로젝트의 현재 활성 패널 수는 42로 유지한다. 과거 취소 12개 때문에 54면처럼 보이지 않게 한다.

## 실행 경계

- implementationApproved: `true`
- migrationApproved: `true` — 신규 additive migration과 isolated DB 검증만 포함
- persistentUatApproved: `false`
- runtimeHandoverApproved: `false`
- actualProviderApproved: `false`
- commitApproved: `true`
- pushApproved: `true`
- pullRequestApproved: `true`
- mergeApproved: `true`
- publicationApprovalSource: `USER_EXPLICIT_2026-08-04`

## 검증 계약

- 반복 사양 저장, 같은 위치 재수정, 물리 패널 identity·활성 수 보존을 검증한다.
- 위치 추가·삭제 때만 패널 생성·취소가 일어나며 downstream 이력과 번호를 재사용하지 않는지 검증한다.
- 기존 프로젝트 backfill에서 활성 패널은 활성 위치에만 연결되고 취소 패널은 현재 조회에서 제외되는지 검증한다.
- UL891 생성·사양 추가에서 code 없이 패널 수를 입력하며 비-UL891 생성·설계가 회귀하지 않는지 검증한다.
- desktop·390px에서 단일 현재 설계 수정·저장과 버전/code 미노출을 검증한다.
