# TASK-G2-OPERATIONS-001 Change 003 — 가로형 표·그래프 가독성·날짜 필터

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_VALIDATED_USER_REVIEW_PENDING`
- userInstruction: `표는 전부 가로로 바꾸고, 그래프는 가독성이 너무 떨어져. 잘 보이게 해주고, 그래프 밑으로 커서 내리는 거 없애줘. 또, 그래프나 표 날짜 필터 걸 수 있게 해줘.`
- userInstructionDate: 2026-08-19
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- implementationApproved: true
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false

## 1. 확인한 원인

- G2 그래프 SVG에 `min-width`와 wrapper `overflow-x: auto`가 있어 좁은 화면에서 그래프 아래 가로 scrollbar가 생긴다.
- 한 달 31일을 단일 최대값·0 눈금만으로 표시해 중간 값 비교가 어렵고, 작은 화면에서 날짜·계열 구분이 약하다.
- 홈과 두 관리 화면의 visible table은 날짜가 세로 행으로 배치되어 사용자가 원하는 가로 날짜 흐름과 다르다.
- 월 전체 자료를 불러오지만 화면에서 표시 기간을 줄이는 날짜 filter가 없다.

## 2. 승인 구현 범위

- 홈 출근표, 생산/출하 월간표, 출근 월간표를 날짜가 열에 놓이는 가로형 표로 전환
- 표 첫 열을 항목명으로 고정하고 날짜 열을 가로 탐색 가능하게 구성
- 홈에 시작일·종료일 공통 filter를 추가해 두 그래프와 출근표에 같은 범위 적용
- 생산/출하·출근 관리의 월간표에 각각 시작일·종료일 filter 추가
- 그래프 자체 가로 scrollbar와 강제 최소 너비 제거
- 반응형 SVG, 단계형 축 눈금, 계열 색·선·예상 영역·날짜 label을 보강
- 필터된 날짜가 적을 때 막대 수치를 직접 표시

## 3. 보존 범위

- G2 API·DB·migration·권한·CAS·재고 계산·예상 판정은 변경하지 않는다.
- 입력 날짜 선택과 저장 workflow, 목표·실사 수정 workflow를 유지한다.
- 표만 필요한 가로 overflow는 표 내부에 한정하고 페이지 전체 overflow는 만들지 않는다.
- 손익관리와 기존 PMS data 연결은 계속 제외한다.

## 4. 검증

- Frontend lint·typecheck·G2 unit·전체 unit·production build
- desktop과 390px에서 그래프 scrollbar 없음, chart 구조·축·범례 확인
- 홈 공통 날짜 filter가 두 그래프·출근표에 함께 적용되는지 확인
- 관리 화면 표 filter와 날짜 가로 열 구조 확인
- page-level overflow, console error와 기존 입력·권한 회귀 확인

## 5. 다음 Gate

구현과 자동·browser 검증 뒤 현재 local 검수 server를 갱신해 같은 G2 홈 URL로 사용자에게 다시 전달한다. Git 게시·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 6. 구현·검증 결과

- 모든 visible G2 월간 표를 날짜 열·항목 행 가로형으로 통일했다.
- 홈 공용 날짜 범위와 관리 화면별 표 날짜 범위를 추가했다.
- 그래프 계열색·축 제목·단계형 눈금·예상 영역·짧은 기간 값 label을 보강하고 그래프 가로 scrollbar를 제거했다.
- 화면 읽기용 표의 intrinsic width가 페이지 overflow를 만들던 `G2-CHART-SR-TABLE-OVERFLOW`를 clipped wrapper로 해소했다.
- 초기 effect가 사용자의 첫 날짜 변경을 전체 기간으로 되돌릴 수 있던 `G2-DATE-FILTER-RESET-RACE`를 reset key 변경 시에만 초기화하도록 해소했다.
- Frontend lint error 0, typecheck 통과, 31 files `223/223`, production build와 `git diff --check`를 통과했다.
- local live browser에서 desktop 1440·mobile 390, 홈·생산/출하·출근관리 가로표·filter·그래프를 재확인했다. 그래프 `scrollWidth = clientWidth`, page overflow 0이며 표 내부 scroll만 유지된다.
- 현재 상태는 `사용자 검수 대기`이며 Git 게시·Persistent UAT·Azure 공개배포는 실행하지 않았다.
