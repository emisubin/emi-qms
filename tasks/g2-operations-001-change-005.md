# TASK-G2-OPERATIONS-001 Change 005 — 그래프 파스텔 색상·생산표 재고·합계 숫자 펼침

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

## 1. 사용자 지정 표현 계약

### 생산·납품·재고 그래프

- 생산 막대: 파스텔 주황색
- 납품 막대: 파스텔 파란색
- 재고: 파스텔 빨간색 실선
- 예상 재고: 같은 빨간색 점선
- 재고 목표: 파스텔 파란색 점선

### 조별 생산 그래프

- 오전조: 연한 파란색
- 오후조: 상대적으로 진한 파란색
- 일 생산목표: 파스텔 빨간색 점선

### 홈 표

- 생산 현황표에서 `일 생산목표` 행을 제거하고 `재고` 행을 추가한다.
- 출근 현황표는 왼쪽 오전·오후 합계 header button뿐 아니라 각 날짜의 합계 숫자 button을 눌러도 같은 조의 EMI·도급 행을 펼치거나 접는다.

## 2. 보존 범위

- 그래프 data·축·단위·예상 판정·tooltip·화면 읽기용 표와 날짜 filter는 변경하지 않는다.
- 출근표의 기본 오전·오후 합계 2개 행과 조별 독립 disclosure를 유지한다.
- G2 API·DB·migration·권한·재고 계산과 관리 화면은 변경하지 않는다.
- 그래프·페이지 가로 scroll은 만들지 않고 표 내부 scroll만 유지한다.

## 3. 검증 계획

- graph SVG gradient·line style과 범례의 지정 색상 계약 검사
- 생산 현황표의 `재고` 행 존재와 `일 생산목표` 행 부재 검사
- 출근표 왼쪽 header와 날짜별 합계 숫자에서 동일한 오전/오후 독립 펼침·접힘 검사
- Frontend lint·typecheck·G2 집중·전체 unit·production build·diff check
- local 검수 server desktop 1440·mobile 390에서 시각·interaction·overflow 확인

## 4. 다음 Gate

구현과 자동·browser 검증 뒤 현재 local G2 홈을 사용자 검수로 전달한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 5. 구현·검증 결과

- 생산·납품·재고 graph와 조별 생산 graph의 SVG gradient·line·legend를 사용자 지정 pastel mapping으로 변경했다.
- 공통 Graphite rule의 `[class*='chart']` grayscale filter가 G2 색상을 제거하는 `G2-GRAPH-GRAYSCALE-CASCADE-001`을 확인하고, G2 chart layer만 명시적 color exception으로 분리했다.
- 생산 현황표에서 `일 생산목표`를 제거하고 자동 계산 `재고`를 추가했다.
- 출근표의 각 날짜별 오전·오후 합계 숫자를 button으로 바꾸고 왼쪽 header와 동일한 독립 disclosure 상태를 공유하게 했다.
- G2 집중 `5/5`, Frontend 전체 31 files `223/223`, lint error 0, typecheck, production build와 diff check를 통과했다.
- desktop 1440·mobile 390 live browser에서 실제 pastel 색상, 생산표 재고 행, 숫자와 header 양쪽 펼침을 확인했다.
- 현재 상태는 `사용자 검수 대기`이며 Git 게시·Persistent UAT·Azure 공개배포는 실행하지 않았다.
