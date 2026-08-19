# TASK-G2-OPERATIONS-001 Change 017 — 조별 총생산 평균선과 그래프 KPI 보강

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

- 오전조·오후조 누적 생산 막대 위에 날짜별 일일 총 생산량을 표시한다.
- 선택한 홈 날짜 범위의 일일 총 생산량 평균을 조별 생산 그래프의 가로선으로 표시하고 hover에서 정확한 평균값을 확인할 수 있게 한다.
- 생산·납품·재고 그래프 오른쪽에는 일일 생산 평균, 일일 납품 평균, 일일 재고 평균과 재고 부족분 KPI를 표시한다.
- 조별 생산 그래프 오른쪽에는 오전조 일일 생산 평균과 오후조 일일 생산 평균 KPI를 표시한다.
- 재고 부족분은 선택 기간의 마지막 계산 가능 날짜에서 `재고목표 - 재고`로 계산하고, 부족하지 않으면 `0대`로 표시한다. `i` 안내에 기준 날짜와 공식을 표시한다.
- 평균은 선택한 날짜 범위에서 입력되지 않은 값을 제외하고 실제 `0`은 포함한다.
- 그래프 SVG의 `720×340` 좌표계, 축·막대·선·모바일 5일 내부 탐색 계약은 변경하지 않는다. Desktop에서는 그래프를 왼쪽, KPI를 오른쪽에 배치하고 좁은 화면에서는 KPI를 그래프 아래 2열로 재배치한다.

## 2. 구현 결정

- 평균과 부족분은 홈 응답에 포함된 현재 날짜 범위 자료로 Frontend에서 파생하며 별도 저장값·API·migration을 추가하지 않는다.
- 조별 총 생산 평균선은 일 생산목표 빨간 점선과 구분되는 파스텔 청록 점선과 white halo를 사용한다.
- 총 생산 평균선은 기존 선 hover hit layer를 재사용하고 화면 읽기용 표에도 평균값을 추가한다.
- KPI는 색 계열을 graph series와 맞추고 재고 부족분의 `i`는 pointer hover와 keyboard focus에서 같은 설명을 제공한다.
- 공통 Graphite grayscale 규칙이 KPI와 새 평균선 색을 제거하지 않도록 G2 승인 색상 예외 범위에 새 container를 포함한다.

## 3. 보존 범위

- Backend·DB·migration·권한·재고 계산·입력 화면은 변경하지 않는다.
- 일 생산목표·재고목표, 실사, 예상값 날짜 도래 초기화와 표 동작을 유지한다.
- 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 4. 검증 결과

- `G2Pages.test.tsx`에서 총 생산 label, 평균선·hover, KPI 값·공식 안내와 날짜 filter 재계산을 확인했다.
- Frontend 전체 unit `226/226`, typecheck, lint error `0`, production build를 통과했다. 기존 Fast Refresh warning 1건과 대형 chunk warning은 유지된다.
- local Desktop 검수 화면에서 그래프 좌표계와 색상을 유지한 채 graph/KPI가 두 열로 배치되고 KPI 6개, 입력된 날짜의 총 생산 label과 평균선 hover layer가 표시됨을 privacy-safe projection으로 확인했다.
- 검수 화면의 page 가로 overflow가 없고, 좁은 화면 CSS는 graph frame·내부 5일 scroll을 유지하면서 KPI를 아래 2열로 전환한다.

## 5. 다음 Gate

자동 검증과 local 검수 runtime 반영을 완료했으며 사용자 검수를 받는다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.
