# TASK-PRIVACY-NOTICE-001 Change 004 — 목차·footer link·logo 검수 보정

## 승인 기록

- approvalSource: `USER_EXPLICIT_VALIDATION_REVISION`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- implementationApproved: `true`
- localValidationRuntimeApproved: `true`
- commitApproved: `false`
- pushApproved: `false`
- pullRequestApproved: `false`
- mergeApproved: `false`
- productionDeploymentApproved: `false`

## 사용자 수정 지시

1. 개인정보 안내 목차는 클릭 뒤 검은 배경이나 선택 상태를 표시하지 않고 대상 section으로 부드럽게 이동만 한다.
2. 개인정보 안내 진입점은 회사 정보와 같은 글꼴·크기의 텍스트 형태로 footer 맨 오른쪽 아래에 배치한다. 별도 버튼 외형은 사용하지 않는다.
3. desktop sidebar, mobile top bar와 mobile menu의 EMI PMS logo를 누르면 `홈` 메뉴와 동일하게 홈 route로 이동한다.

## 변경 허용 범위

- `PrivacyNoticePage` 목차 state·표현과 관련 test/style
- 공통 회사 정보 footer의 개인정보 안내 link 표현과 관련 test/style
- 기존 logo 표면의 홈 이동 action과 관련 test/style
- Change 004 자동·browser 검증, implementation report·manual·checklist 갱신

## 제외 범위

- 개인정보 안내 문안·보유 기간·연락 창구 변경
- Backend·API·DB·migration·권한·외부 provider
- 기존 5174·Persistent UAT·Azure·운영 배포
- commit·push·PR·merge·branch/worktree 정리

## 기준선

- branch: `feat/task-privacy-notice-001-main-integration`
- base: `origin/main` SHA `9a25157f0b8d1e78ad5392acf336ebf3c0f61b64`
- instructionChainRead: `true` — 같은 Task 연속 turn이며 base·instruction drift 없음
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`

## 구현·검증 결과

- 목차의 React 선택 state, `aria-current`, black active style와 section focus outline을 제거하고 smooth scroll·hash·programmatic focus는 유지했다.
- footer 진입점은 회사 정보와 같은 font family·size를 상속하고 border·background·button padding이 없는 우측 하단 text control로 보정했다.
- desktop sidebar, mobile top bar와 mobile drawer logo를 홈 route에 연결했고 drawer logo는 이동 뒤 menu를 닫는다.
- Change 관련 test `2 files / 83 tests`, Frontend 전체 test `27 files / 194 tests`, typecheck, lint, build와 diff check가 통과했다.
- desktop·390px Browser에서 black tab state 0, footer text style·우측 정렬, 세 logo surface의 홈 이동, horizontal overflow 0을 확인했다.
- 사용자 검수, commit·push·PR·merge와 운영 적용은 계속 미승인이다.
