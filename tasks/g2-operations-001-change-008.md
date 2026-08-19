# TASK-G2-OPERATIONS-001 Change 008 — graph hover 안내·가로선 대비

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

- 생산·납품 막대, 오전·오후조 막대에 pointer를 올리면 날짜·항목·수량을 간단한 graph 내부 tooltip으로 표시한다.
- 재고·예상 재고·재고 목표·일 생산목표 선에 pointer를 올리면 pointer 위치에 가장 가까운 날짜의 항목·수량을 같은 tooltip 형식으로 표시한다.
- tooltip은 plot 가장자리에서 잘리지 않도록 좌우·상하 위치를 보정한다.
- 기존 SVG title과 화면 읽기용 표는 유지해 tooltip 표시 여부와 무관하게 원본 수치를 확인할 수 있게 한다.
- 두 graph의 가로 grid line은 현재보다 조금 더 진하게 표시하되 data series보다 앞서 보이지 않게 유지한다.

## 2. 보존 범위

- Change 005~007의 pastel color, 실적·예상 선 구분, fixed axis, stack 연결, flat baseline과 날짜 filter를 유지한다.
- visible 생산·출근표 interaction, G2 API·DB·권한·재고 계산은 변경하지 않는다.
- graph와 page 가로 scroll은 만들지 않는다.

## 3. 검증 계획

- 막대 hover와 선 hover의 날짜·항목·수량 tooltip contract unit 검사
- grid color·stroke width style contract 검사
- Frontend lint·typecheck·G2 집중·전체 unit·production build·diff check
- local desktop 1440·mobile 390에서 tooltip clipping, graph 대비와 overflow 확인

## 4. 다음 Gate

구현과 자동·browser 검증 뒤 현재 local G2 홈을 사용자 검수로 전달한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 5. 구현·검증 결과

- 생산·납품·오전조·오후조 막대에 날짜·항목·수량을 표시하는 즉시형 SVG tooltip을 추가했다.
- 재고·예상 재고·재고 목표·일 생산목표 선 위에 넓은 투명 hit path를 겹쳐 선을 따라 이동할 때 가장 가까운 날짜의 수치를 표시한다.
- tooltip은 plot 좌우·상하 경계에 따라 위치를 자동 보정하며 기존 SVG title과 화면 읽기용 표를 유지했다.
- 가로 grid line을 `#c5d1df`, `1.2px`, `3 5` dash로 한 단계 진하게 조정했다.
- G2 집중 2 files `5/5`, Frontend 전체 31 files `223/223`, lint error 0, typecheck, production build와 diff check를 통과했다.
- local live browser desktop 1440에서 막대 tooltip·grid computed style·page overflow를, mobile 390에서 선 tooltip·plot clipping 없음·page overflow 0을 확인했다.
- 현재 상태는 `사용자 검수 대기`이며 Git 게시·Persistent UAT·Azure 공개배포는 실행하지 않았다.
