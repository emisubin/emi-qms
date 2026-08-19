# TASK-G2-OPERATIONS-001 Change 009 — graph 상시 수치·홈 표 구성 보강

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

- 두 graph SVG가 desktop card의 넓은 가로 공간을 더 사용하도록 최대 폭을 늘린다.
- 재고선은 값이 있는 모든 날짜에 pastel red circle과 수량 label을 선 위에 표시한다.
- 실사 입력 날짜의 재고 point와 수량 label만 pastel blue로 구분한다. 실사 point는 같은 날짜의 일반 재고 point를 대체한다.
- 생산·납품 막대는 값이 있으면 기간 길이와 무관하게 각 막대 위에 수량을 표시한다.
- 조별 생산 막대는 합계 label 대신 오전 수량을 오전 segment 안에, 오후 수량을 오후 segment 안에 표시한다.
- Change 008의 hover tooltip은 유지하되 graph element에서 `help` cursor로 바뀌지 않게 한다.

## 2. 사용자 지정 home 구성 계약

- 목표 관리 card를 생산 현황표 바로 위로 이동한다.
- 생산 현황표의 재고 행은 값과 왼쪽 header를 pastel red 글씨로 표시한다. 음수 경고 의미는 유지한다.
- 제조 인원 출근 현황표 마지막에 오전 합계와 오후 합계를 단순 합산한 `오전·오후 전체 합계` 행을 추가한다.
- 기존 오전·오후 합계 disclosure와 EMI·도급 상세 펼침은 유지한다.

## 3. 보존 범위

- fixed axes, pastel series mapping, 실적·예상 선, flat baseline, 날짜 filter와 tooltip clipping contract를 유지한다.
- G2 API·DB·권한·재고·출근 합계 계산은 변경하지 않는다.
- page 가로 overflow를 만들지 않고 월간 표는 card 내부 scroll만 유지한다.

## 4. 검증 계획

- 모든 날짜 재고 point·label, 실사 blue 구분, 생산·납품 상단 label, 조별 segment 내부 label unit 검사
- 목표 관리→생산 현황 DOM 순서, 재고 행 class, 전체 합계 행과 기존 disclosure 회귀 검사
- Frontend lint·typecheck·G2 집중·전체 unit·production build·diff check
- local desktop 1440·mobile 390에서 graph 폭·label 겹침·tooltip cursor·표 위치·overflow 확인

## 5. 다음 Gate

구현과 자동·browser 검증 뒤 현재 local G2 홈을 사용자 검수로 전달한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 6. 구현·검증 결과

- graph SVG 최대 폭을 `960px`에서 `1180px`로 늘려 1440px desktop에서 실제 약 `1090px` 폭으로 표시했다.
- 재고 값이 있는 31일 전체에 pastel red point·수량을 표시하고, 실사 3일은 pastel blue point·수량으로 대체했다.
- 생산·납품 62개 막대 상단에 각 수량을 표시하고 조별 62개 segment 안에는 오전·오후 수량을 각각 표시했다.
- graph hover tooltip을 유지하면서 막대·선·point의 computed cursor가 기본 `auto`임을 확인했다.
- 목표 관리 card를 생산 현황 바로 앞으로 이동하고, 생산표 재고행을 pastel red로 표시하며 출근표에 `오전·오후 전체 합계` 행을 추가했다.
- 공통 Graphite monochrome cascade가 재고행 색을 덮는 문제를 G2 semantic table accent 예외로 보정했다.
- G2 집중 2 files `5/5`, Frontend 전체 31 files `223/223`, lint error 0, typecheck, production build와 diff check를 통과했다.
- local live browser desktop 1440·mobile 390에서 graph 폭, 모든 label 수, 목표→생산표 순서, 재고·전체합계 computed color, tooltip·cursor와 page overflow 0을 확인했다.
- 현재 상태는 `사용자 검수 대기`이며 Git 게시·Persistent UAT·Azure 공개배포는 실행하지 않았다.
