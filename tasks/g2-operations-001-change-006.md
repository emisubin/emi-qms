# TASK-G2-OPERATIONS-001 Change 006 — 조별 막대 연결·출근표 셀 클릭 표현

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_VALIDATED_USER_REVIEW_PENDING`
- userInstruction: `생산 그래프 막대끼리 연결 강화하고 출근현황표는 표 안에 버튼 디자인 만들지말고 그냥 표 자체를 클릭하면 열리게 해줘.`
- userInstructionDate: 2026-08-19
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- implementationApproved: true
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false

## 1. 적용 해석

- `생산 그래프 막대끼리 연결 강화`는 조별 생산 그래프에서 오전조 위에 오후조가 누적되는 막대를 하나의 연속된 일 생산 막대로 더 명확히 보이게 하는 요청으로 적용한다.
- 오전·오후 접점의 stroke와 둥근 모서리를 제거하고, 두 segment 전체를 감싸는 얇은 외곽선을 추가한다.
- 생산·납품·재고 그래프의 생산·납품은 서로 다른 지표의 나란한 막대이므로 서로 붙여 하나의 값처럼 오해하게 만들지 않는다.
- 출근표는 button semantics와 keyboard·focus 접근성을 보존하되 시각적인 button border·background·boxed icon은 제거한다. 합계 header와 각 날짜 숫자 cell 전체가 평면적인 표 영역으로 보이면서 클릭된다.

## 2. 보존 범위

- Change 005의 pastel color mapping, 축·단위·날짜 filter·tooltip·화면 읽기용 표를 유지한다.
- 출근표의 기본 오전·오후 합계와 조별 독립 disclosure 상태를 유지한다.
- API·DB·권한·재고 계산·관리 화면은 변경하지 않는다.
- 표 내부 scroll만 허용하고 그래프·페이지 가로 scroll은 만들지 않는다.

## 3. 검증 계획

- 조별 막대 segment의 접점·outer outline DOM 계약 검사
- 출근 합계 header·숫자 cell의 button semantics, 무장식 class와 양쪽 펼침·접힘 회귀 검사
- Frontend lint·typecheck·G2 집중·전체 unit·production build·diff check
- local desktop 1440·mobile 390에서 누적 막대 연결, table-like 클릭 표현과 overflow 확인

## 4. 다음 Gate

구현과 자동·browser 검증 뒤 현재 local G2 홈을 사용자 검수로 전달한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 5. 구현·검증 결과

- 조별 생산 graph의 오전·오후 segment를 날짜별 rounded clip 안에 함께 배치하고 segment stroke를 제거해 접점을 틈 없이 연결했다.
- 각 누적 막대 전체에 pastel blue outer outline을 추가해 하나의 일 생산 막대라는 grouping을 강화했다.
- 출근 합계 header와 날짜별 숫자 button에서 persistent border·background·boxed icon을 제거하고 cell 전체를 채우는 평면 interaction으로 바꿨다.
- hover·expanded row highlight와 keyboard focus outline은 유지해 발견 가능성과 접근성을 보존했다.
- G2 집중 `5/5`, Frontend 전체 31 files `223/223`, lint error 0, typecheck, production build와 diff check를 통과했다.
- local live browser에서 누적 막대 31개 segment group·outline, 기본 합계 행 2개, 숫자 cell disclosure를 확인했다.
- 현재 상태는 `사용자 검수 대기`이며 Git 게시·Persistent UAT·Azure 공개배포는 실행하지 않았다.
