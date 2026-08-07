# TASK-PRODUCTION-CONTROL-001 change-010

## Task Identity Gate

- purposeIdentity: 양식 카탈로그 초기화 직후 `수정`을 누르면 후행 선택 동기화가 편집 상태를 다시 닫는 경쟁을 제거한다.
- canonicalTask: `TASK-PRODUCTION-CONTROL-001`
- taskType: `BUGFIX`
- branch: `fix/task-production-control-001-edit-race-010`
- baseHead: `8d6ae914e8f748430337a5dd0ad79e7565730733`
- instructionChainRead: `true`
- roadmapExpectedTaskId: Teams SSO·새 manifest 신규 기능 기획
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- samePurposeMatchCount: `1`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`
- approvalSource: 반복된 Frontend CI P2를 `Change 010` 최소 수정·검증·게시한 뒤 문서 PR을 병합하라는 사용자 승인

## 확인된 결함

- `applyCatalog`가 선택 Item·현재 양식·편집 행을 이미 초기화한 뒤, `selectedProductTypeId` 효과가 같은 version을 다시 `chooseVersion`으로 전달했다.
- `chooseVersion`은 항상 `editing=false`로 바꾸므로, 초기 응답 직후 사용자가 `수정`을 누른 실행 순서에서는 후행 효과가 `저장` 버튼을 제거할 수 있었다.
- 동일한 제조 양식 저장 테스트가 로컬에서는 통과하지만 GitHub Frontend CI에서 두 번 같은 위치로 실패해 병합 차단 P2로 확정했다.

## 포함 범위

- 선택된 현재 version이 실제로 달라질 때만 Item/domain 선택 동기화를 수행한다.
- CI에서 실패한 제조 양식 `수정 → 저장` 경로에 편집 상태 유지 assertion을 추가한다.
- Implementation report·Roadmap·사용자 검수 체크리스트에 Finding과 검증·게시 상태를 동기화한다.

## 보존할 불변조건

- Item 또는 제조/생산계획 domain을 실제로 바꾸면 기존처럼 새 현재 양식으로 전환하고 편집 중 변경사항을 닫는다.
- 저장·취소·양식 생성 뒤의 편집 종료와 feedback 계약을 유지한다.
- Backend, API, DB, migration, 권한, 양식 내용과 화면 디자인은 변경하지 않는다.

## 검증 계약

- `FormTemplateManagementPage.test.tsx` 집중 회귀
- Frontend lint, typecheck, 전체 unit, production build
- 격리 Full-Stack 양식 관리 browser에서 desktop `수정 → 두 animation frame 이후 저장 유지`와 기존 390px overflow 회귀
- PR 표준 CI와 merge 후 main CI

## Finding

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `CI-FRONTEND-FORM-TEMPLATE-001` | P2 | `RESOLVED` | 동일 version의 중복 선택 동기화가 빠른 편집 진입을 취소해 CI와 실제 빠른 조작에서 저장 버튼이 사라질 수 있다. | 같은 version이면 후행 초기화를 생략했고 집중 unit `3/3`·연속 `10/10`·Frontend 전체 `177/177`·lint·typecheck·build를 통과했다. Full-Stack·표준 CI는 게시 Gate에서 확인한다. |
| `CI-FULLSTACK-FORM-TEMPLATE-FIXTURE-001` | P2 | `RESOLVED` | 첫 PR Full-Stack 격리 DB의 기본 Item에는 현재 제조 양식이 없어 회귀가 `수정`을 기다리며 시간 초과했다. 제품 수정 실패가 아니라 테스트 준비조건 누락이었다. | 현재 양식이 없으면 UI로 한 번 생성하고 reload한 뒤 `수정 → 저장 유지`를 검증하도록 fixture를 self-contained하게 만들었다. |
