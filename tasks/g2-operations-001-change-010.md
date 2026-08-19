# TASK-G2-OPERATIONS-001 Change 010 — graph 숫자 밀도·재고 축·표 강조

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_VALIDATED_USER_REVIEW_PENDING`
- userInstructionDate: 2026-08-19
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- implementationApproved: true
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false

## 1. 사용자 지정 graph 계약

- 두 graph의 x축 날짜 숫자, y축 tick 숫자와 data value 숫자를 graph 크기에 맞게 한 단계 작게 표시한다.
- 선택 기간에 포함된 날짜는 간격 생략 없이 1일부터 마지막 날짜까지 x축에 모두 표시한다. 31일이 있는 달은 31일도 표시한다.
- 재고선 point는 작은 채움형 circle로 표시한다. 일반·예상 재고는 pastel red 계열, 실사 입력 날짜는 pastel blue 채움형을 유지한다.
- 생산·납품·재고 graph의 오른쪽 재고축은 `0, 20, 40, 60, 80, 100, 120, 140, 160, 180`으로 고정한다.
- fixed range 밖 재고값은 기존처럼 plot 경계에 clamp하고 실제 값은 label·tooltip·표에 보존한다.

## 2. 생산 현황표 재고행 계약

- 재고행의 빨간 글씨 강조를 제거한다.
- 출근 현황표 `오전·오후 전체 합계`와 같은 구조로 행 전체에 굵은 상단선과 pastel background를 적용하되 재고 의미에 맞는 연한 빨강 계열을 사용한다.
- 일반 재고 글씨는 기본 짙은 글씨를 사용하고 음수 재고의 기존 위험 색상은 유지한다.

## 3. 보존 범위

- Change 009의 확대 graph, 전 날짜 수치 label, 실사 blue 구분, hover tooltip·기본 cursor와 home card 순서를 유지한다.
- 생산·출근표의 값·disclosure, G2 API·DB·권한·계산은 변경하지 않는다.
- graph와 page 가로 scroll은 만들지 않는다.

## 4. 검증 계획

- fixed right axis `0~180/20`, 모든 날짜 label, small filled point와 class contract unit 검사
- 생산표 재고행 class·출근 전체 합계행 회귀 검사
- Frontend lint·typecheck·G2 집중·전체 unit·production build·diff check
- local desktop 1440·mobile 390에서 숫자 크기·날짜 밀도·point·재고행 design·overflow 확인

## 5. 다음 Gate

구현과 자동·browser 검증 뒤 현재 local G2 홈을 사용자 검수로 전달한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 6. 구현·검증 결과

- graph x축·y축·막대·재고·조별 수치 글꼴을 축소하고, 두 graph 모두 선택 기간의 날짜 label을 생략 없이 표시했다.
- 생산·납품·재고 graph 오른쪽 축을 `0~180/20`으로 변경했다.
- 재고 point는 일반·예상 `r=2.5`, 실사 `r=3.2`의 채움형 pastel red·blue circle로 변경했다.
- 생산 현황표 재고행은 일반 글씨를 유지하면서 `2px` pastel red 상단선과 연한 빨강 배경을 적용했다. 음수 재고 위험 글씨는 유지한다.
- G2 집중 unit `5/5`, Frontend 전체 unit `223/223`, typecheck, lint error 0, production build와 `git diff --check`를 통과했다.
- local synthetic August 화면에서 각 graph 날짜 label `31/31`, 재고 point `31`, 실사 point `3`, 오른쪽 축 `0·20·…·180`, desktop/mobile page overflow `0`과 재고행 computed style을 확인했다.
