# TASK-G2-OPERATIONS-001 Change 018 — 조별 통합 hover와 KPI 시각·기준일 보정

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

- 조별 생산 누적 막대 위의 일일 총 생산량 숫자를 제거한다. 오전·오후 segment 내부 숫자는 유지한다.
- 오전 또는 오후 segment를 별도 hover 대상으로 취급하지 않고 날짜별 누적 막대 전체를 하나의 hover 대상으로 사용한다.
- 통합 hover는 같은 안내 상자에 `오전: n대`, `오후: n대`, `전체: n대`를 표시한다.
- 총 생산 평균선이 segment 내부 숫자를 가리지 않도록 막대·숫자보다 아래 layer에 그리고 색을 순수 파랑으로 변경한다.
- 모든 KPI 카드의 왼쪽 굵은 강조선을 제거한다.
- 오전조·오후조 KPI 배경은 그래프 막대와 같은 연한 파랑·진한 파랑 gradient를 사용한다.
- 재고 부족분은 선택 기간 마지막 날짜가 아니라 서울 기준 오늘 날짜의 `재고목표 - 재고`로 계산한다. 오늘 자료가 없으면 계산하지 않는다.
- 재고 부족분 `i` 안내는 마지막 KPI 아래로 잘리지 않도록 해당 아이콘 위쪽·왼쪽 방향으로 표시한다.

## 2. 구현 결정

- 오전·오후 SVG segment의 직접 pointer handler를 제거하고 누적 막대 전체 높이를 덮는 투명 단일 hit rect를 추가한다.
- 공용 SVG tooltip은 기존 한 줄 항목과 조별 생산 3줄 항목을 모두 지원하며 나머지 그래프 hover 계약은 유지한다.
- 총 생산 평균선과 넓은 hit line을 날짜별 막대보다 먼저 렌더링해 막대·segment 수치가 항상 위에 보이게 한다.
- 평균선은 `#2563eb` 파랑 점선, 오전·오후 KPI는 막대 gradient와 같은 색상 stop을 사용한다.
- 오늘 기준은 Backend 홈 응답의 `today`와 같은 날짜의 일별 자료를 사용하며 홈 날짜 filter와 무관하게 유지한다.

## 3. 보존 범위

- 평균 산식, 일 생산목표, 오전·오후 segment 내부 값, 날짜 filter와 mobile 5일 내부 scroll을 유지한다.
- Backend·DB·migration·권한·입력 화면은 변경하지 않는다.
- 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 4. 검증 결과

- `G2Pages.test.tsx`에서 막대 위 합계 label 제거, 통합 hit rect와 3줄 hover, 평균선의 막대 이전 draw order, 오늘 기준 부족분 안내를 확인했다.
- Frontend 전체 unit `226/226`, typecheck, lint error `0`, production build를 통과했다. 기존 Fast Refresh warning 1건과 대형 chunk warning은 유지된다.
- local Desktop 화면에서 합계 label `0`, 입력된 날짜별 통합 hit area, 순수 파랑 평균선, 평균선→막대 draw order, KPI 왼쪽 border `1px`, 막대와 동일한 오전·오후 KPI gradient를 확인했다.
- 재고 부족분 안내는 오늘 날짜를 명시하고 위쪽 배치가 적용됐으며 page 가로 overflow는 `0`이다.

## 5. 다음 Gate

자동 검증과 local 검수 runtime 반영을 완료했으며 사용자 검수를 받는다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.
