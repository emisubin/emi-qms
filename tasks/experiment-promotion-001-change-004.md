# TASK-EXPERIMENT-PROMOTION-001 Change 004 — 활성 패널 화면 순번 정합

## Task Identity Gate

- proposedTaskId: `TASK-EXPERIMENT-PROMOTION-001 Change 004`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `UL891_INTEGRATED_CANDIDATE_VALIDATION_AND_PUBLICATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-EXPERIMENT-PROMOTION-001`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`

## 사용자 검수 Finding

- 사용자 증상: 프로젝트 면수는 42면인데 제조·품질·물류 탭의 마지막 `No`와 패널 코드가 52로 보여 52개 패널처럼 해석된다.
- 실제 상태: 세 탭의 활성 데이터 행과 KPI 분모는 모두 42개다. 과거 5세트×9면 생성 뒤 세트당 2면 제거, 7면 신규 세트 추가로 영구 패널 sequence/display code가 `P52`까지 발급됐고 취소 번호는 재사용하지 않는다.
- Root Finding: 공통 현재 패널 표의 `No` 열이 활성 목록의 화면 순번이 아니라 이력 불변 `panel.sequenceNumber`를 표시한다.
- 영향: 데이터·업무 상태는 맞지만 사용자가 현재 패널 수를 마지막 영구 번호로 오해한다.

## 승인 범위와 불변조건

- 제조·품질·물류 desktop 표의 `No`는 정렬된 활성 행 기준 `1..N`으로 연속 표시한다.
- `P01`·`P52` 같은 영구 `displayCode`, panel ID, QR·audit·workflow·취소 이력은 변경하지 않는다.
- 활성 패널 filtering, 정렬, KPI·진행률·mobile card와 Backend·DB·migration을 변경하지 않는다.
- 사용자 승인: 2026-08-04 `너의 수정 방향대로 수정해봐. 그리고 바로 메인에 전체 병합해 시간이 없다. 승인.`
- implementationApproved: `true`
- commitApproved: `true`
- pushApproved: `true`
- pullRequestApproved: `true`
- mergeApproved: `true`
- persistentUatApproved: `false`
- azureMutationApproved: `false`

## 검증 계약

1. 비연속 영구 번호 `1, 10, 19, 52`를 가진 활성 패널 4개가 제조·품질·물류 `No`에서 모두 `1, 2, 3, 4`로 표시되는지 unit test로 고정한다.
2. 마지막 행에서 영구 코드 `P52`가 유지되는지 확인한다.
3. 실제 42면 검수 프로젝트의 세 탭에서 행 수 42, `No` 1~42, 영구 코드 P52 보존을 확인한다.
4. Frontend lint·typecheck·unit·build, UL891 Full-Stack, diff·문서·민감값 검사를 통과한다.
5. Ready PR의 필수 CI 성공·mergeable을 확인한 뒤 원격 `main`에 병합한다.

## Rollback

- merge 전에는 branch를 게시하지 않거나 PR을 닫는다.
- merge 뒤에는 history rewrite 없이 revert PR 또는 forward-fix PR을 사용한다.
- 데이터·migration 변경이 없으므로 DB rollback은 적용 대상이 아니다.
