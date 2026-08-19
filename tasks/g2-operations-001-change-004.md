# TASK-G2-OPERATIONS-001 Change 004 — 홈 생산표·출근 합계 펼침·그래프 시각 개선

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_VALIDATED_USER_REVIEW_PENDING`
- userInstruction: `홈에 생산표도 하나 해주고, 제조 출근현황은 오전 오후 합계만 나오게 해주고 그걸 눌렀을 때 펼쳐지면서 emi와 도급인원을 볼 수 있게해줘. 그리고 그래프 디자인이 너무 투박해서 가독성이 떨어져. 그래프 디자인 좀 잘 보이는 걸로 색깔 입혀도 되니까 진행해봐.`
- userInstructionDate: 2026-08-19
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- implementationApproved: true
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false

## 1. 확인한 현재 상태

- 홈에는 생산 그래프와 화면 읽기용 숨김 표는 있지만 사용자가 직접 보는 생산 현황 표가 없다.
- 제조 인원 출근표가 EMI·도급·합계·하루 총원을 항상 모두 노출해 한눈에 합계를 확인하기 어렵다.
- 그래프 계열 구분은 가능하지만 단색 막대·직선 중심이라 카드 안에서 시각적 위계와 데이터 강조가 약하다.

## 2. 승인 구현 범위

- 홈 공용 날짜 범위를 그대로 따르는 가로형 `생산 현황` 표 추가
- 생산표에 오전 생산·오후 생산·생산 합계·일 생산목표 표시
- 제조 인원 출근표 기본 행을 오전 합계·오후 합계로 축소
- 오전·오후 합계 행의 명시적 button을 누르면 해당 조의 EMI·도급 행을 독립적으로 펼치거나 접는 disclosure 제공
- 그래프 plot 배경, 색상 gradient, 둥근 막대, 선·점, 예상 구간과 범례 시각 위계 개선
- 기존 축·단위·tooltip·화면 읽기용 표와 반응형 무스크롤 계약 유지

## 3. 보존 범위

- G2 API·DB·migration·권한·CAS·재고·목표 계산은 변경하지 않는다.
- 홈 날짜 filter가 두 그래프와 모든 visible table에 동일하게 적용되는 계약을 유지한다.
- 생산/출하·출근관리 입력 화면과 월간 표는 변경하지 않는다.
- 표 내부 가로 scroll만 허용하고 그래프와 페이지 가로 scroll은 만들지 않는다.

## 4. 검증 계획

- 생산표 행·날짜 filter 적용 unit test
- 출근 합계 기본 노출·오전/오후 독립 펼침·접힘·접근성 상태 unit test
- graph gradient·plot·data point DOM 계약과 화면 읽기용 표 회귀 test
- Frontend lint·typecheck·전체 unit·production build·diff check
- local 검수 server desktop 1440·mobile 390에서 색상·가독성·표 disclosure·page overflow 확인

## 5. 다음 Gate

구현과 자동·browser 검증 뒤 현재 local 검수 server의 G2 홈을 갱신해 사용자 검수로 전달한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 6. 구현·검증 결과

- 홈 공용 날짜 범위를 따르는 가로형 `생산 현황` 표를 추가하고 오전·오후·합계·일 생산목표를 표시했다.
- 출근표는 기본 오전 합계·오후 합계 2개 행만 표시하고 각 합계 button으로 EMI·도급 행을 독립적으로 펼치거나 접게 했다.
- 그래프에 파랑·코랄/주황·초록·보라/빨강 계열 gradient, 둥근 막대, plot 배경, 예상 구간 label과 재고 point를 적용했다.
- G2 집중 `5/5`, Frontend 전체 31 files `223/223`, lint error 0, typecheck, production build와 diff check를 통과했다.
- local live browser의 desktop 1440·mobile 390에서 생산표 4개 행, 출근표 기본 2개 행, 오전/오후 독립 disclosure와 그래프 2개를 확인했다. 그래프·페이지 가로 scroll은 만들지 않고 표 내부 scroll만 유지했다.
- 현재 상태는 `사용자 검수 대기`이며 Git 게시·Persistent UAT·Azure 공개배포는 실행하지 않았다.
