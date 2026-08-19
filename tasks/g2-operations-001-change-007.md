# TASK-G2-OPERATIONS-001 Change 007 — 고정 축·막대 색상·바닥 연결

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

### 생산·납품·재고 graph

- 생산 막대: 파스텔 파란색
- 납품 막대: 파스텔 주황색
- 왼쪽 생산·납품 축: `0, 20, 40, 60, 80, 100`
- 오른쪽 재고 축: `-70, -20, 30, 80, 130`
- 고정 축 범위 밖 값은 graph plot 경계에 clamp하되 실제 값은 SVG title·화면 읽기용 표와 visible 생산표에 그대로 유지한다.

### 조별 생산 graph

- 왼쪽 축: `0, 10, 20, 30, 40, 50, 60`
- 오전·오후 pastel blue mapping과 빨간 일 생산목표 점선은 유지한다.

### 공통 막대 바닥

- 모든 막대의 아래 모서리를 직각으로 만들어 `0` 기준선에 정확히 붙인다.
- 생산·납품 막대는 위 모서리만 둥글게 유지한다.
- 조별 누적 막대는 연결된 segment clip의 위 모서리만 둥글고 바닥은 평평하게 만든다.
- 모든 막대 graph의 `0` 기준선을 일반 axis보다 굵고 진하게 표시한다.

## 2. 보존 범위

- Change 005~006의 재고·예상·목표 선 스타일, 조별 segment 연결, 날짜 filter, tooltip과 대체 표를 유지한다.
- 출근표·생산표 interaction과 G2 API·DB·권한·재고 계산은 변경하지 않는다.
- graph와 page 가로 scroll은 만들지 않는다.

## 3. 검증 계획

- SVG 고정 tick·gradient·top-rounded path·flat baseline·stack clip contract unit 검사
- Frontend lint·typecheck·G2 집중·전체 unit·production build·diff check
- local desktop 1440·mobile 390에서 축 label, 색상, 바닥 접점과 overflow 확인

## 4. 다음 Gate

구현과 자동·browser 검증 뒤 현재 local G2 홈을 사용자 검수로 전달한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 5. 구현·검증 결과

- 생산 gradient를 pastel blue, 납품 gradient를 pastel orange로 교체하고 legend를 동기화했다.
- 생산·납품 왼쪽 fixed axis `0~100/20`, 재고 오른쪽 fixed axis `-70~130/50`, 조별 왼쪽 fixed axis `0~60/10`을 적용했다.
- fixed 재고 축 범위 밖 값은 plot 경계에 clamp하고 실제 수치는 title·대체 표·생산 현황표에 보존했다.
- 생산·납품 막대를 top-rounded path로 바꾸고 조별 clip·outline도 top-rounded path로 바꿔 모든 막대 바닥을 직각으로 `0` 기준선에 연결했다.
- 두 graph의 `0` baseline을 일반 axis보다 굵고 진하게 표시했다.
- G2 집중 `5/5`, Frontend 전체 31 files `223/223`, lint error 0, typecheck, production build와 diff check를 통과했다.
- local live browser desktop·mobile에서 fixed tick, 31개 production·delivery path, 31개 stack outline과 baseline 2개를 확인했다.
- 현재 상태는 `사용자 검수 대기`이며 Git 게시·Persistent UAT·Azure 공개배포는 실행하지 않았다.
