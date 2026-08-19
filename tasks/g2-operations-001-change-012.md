# TASK-G2-OPERATIONS-001 Change 012 — graph 계열 겹침과 휴일 열 강조 보정

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

## 1. 사용자 지정 화면 계약

- 생산·납품·재고 graph는 하나의 plot과 현재 좌우 선형 축을 유지한다.
- 생산·납품 막대 폭을 줄이고 fill을 약하게 투명 처리한다.
- 재고 실선·예상 점선·재고목표 점선 아래에 흰색 분리 stroke를 두어 막대와 겹치는 구간에서도 선을 구분한다.
- 재고 수량 label이 같은 날짜의 생산·납품 상단 수량과 가까우면 위·아래 위치를 자동 보정한다.
- G2 가로표의 휴일 header 배경 채우기를 제거하고, 해당 날짜 열의 header·값·기호·예상·합계 interaction 글자를 모두 빨간색으로 표시한다.

## 2. 보존 범위

- 생산·납품 `0~80/20`, 재고 `0~180/20`, 조별 생산 `0~60/10` 선형 축을 유지한다.
- graph 상시 수치·tooltip·point·예상 영역·날짜·cursor·page overflow 0을 유지한다.
- 생산표 재고행과 출근 전체합계행의 행 단위 pastel 배경은 유지하되 휴일 열 글자색이 우선한다.
- API·DB·권한·재고 계산·입력 data, 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 3. 검증 계획

- graph halo·막대 폭·휴일 열 body cell 회귀 test
- Frontend G2 집중·전체 unit, lint, typecheck와 production build
- local desktop 1440·mobile 390에서 막대/선 교차, 휴일 배경 제거·열 전체 red, 축·overflow 확인

## 4. 다음 Gate

구현·자동·browser 검증 뒤 local G2 홈을 사용자 비교 검수로 유지한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 5. 구현·검증 결과

- 생산·납품 막대 폭을 월 전체 기준 약 20% 줄이고 fill opacity를 `0.86`으로 조정했다.
- 실제 재고·예상 재고·재고목표 선마다 white halo를 먼저 그린 뒤 본 선을 올려 막대와 교차하는 구간을 분리했다.
- 재고 수량이 같은 날짜의 막대 상단 수량과 가까우면 아래쪽으로 이동하는 label collision 보정을 추가했다.
- 모든 G2 가로표의 휴일 header 배경은 흰색으로 복원하고, 휴일 날짜 열의 header·body 값·합계 button·예상 label을 `#dc2626`으로 통일했다.
- G2 집중 `6/6`, Frontend 전체 `224/224`, lint error `0`, typecheck와 production build를 통과했다.
- desktop 1440에서 halo `3`, width `7px`, 막대 opacity `0.86`, 휴일 header `22`·휴일 body cell `77`, header background white·header/cell/button red, page overflow `0`을 확인했다. mobile 390에서도 동일 색상과 page overflow `0`을 확인했다.
- 검수 runtime은 Frontend `http://127.0.0.1:42983/g2`, Backend `http://127.0.0.1:41166`에서 유지한다.
