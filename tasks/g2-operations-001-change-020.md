# TASK-G2-OPERATIONS-001 Change 020 — 입력 안전성·서울 날짜·조회 순서 보정

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

## 1. 사용자 승인 범위

- 값이 비어 있는 재고 실사와 목표를 `0`으로 저장하지 않는다.
- G2의 초기 날짜·월을 Backend와 같은 서울 날짜 기준으로 계산한다.
- 홈 월 이동과 관리 화면 날짜 이동에서 중복 조회와 오래된 응답의 덮어쓰기를 막는다.
- 관리자 입력·수정 이력은 사용자의 명시적 지시에 따라 이번 수정에서 제외하고 별도 `NEW_FEATURE` 상태를 유지한다.

## 2. 구현 결정

- 필수 수량인 재고 실사·목표는 공백을 명시적으로 거부하고, 값이 비어 있으면 저장 버튼도 비활성화한다. 실제 숫자 `0`은 계속 유효하다.
- 브라우저의 현지 날짜가 아니라 `Asia/Seoul` 날짜를 반환하는 공용 함수를 사용해 홈 월·목표 적용일·두 관리 화면 입력일을 초기화한다.
- 월 조회는 상태 변경 effect 한 곳에서만 시작하고, 요청 sequence를 부여해 가장 최근 요청만 화면 상태를 갱신하게 한다.
- 같은 달 안에서 입력 날짜만 바뀔 때는 이미 받은 월 자료를 재사용하며, 다른 달로 이동할 때만 한 번 조회한다.
- 저장 중에는 홈 월 이동과 관리 화면 날짜 이동을 막아 저장 후 재조회가 다른 달 자료를 덮어쓰지 않게 한다.

## 3. 보존할 불변조건

- 빈 값과 실제 `0`을 구분하는 생산·납품·출근 nullable 계약은 유지한다.
- 서울 날짜 도래 시 예상값 초기화, 당일 실제값 보존, 실사 checkpoint와 자동 재고 계산은 변경하지 않는다.
- 역할별 field permission, metric별 CAS, mixed permission 원자 거부를 유지한다.
- 그래프·KPI·표·날짜 filter·공휴일 표시·mobile 내부 drag 디자인은 변경하지 않는다.
- 손익관리와 관리자 입력·수정 이력, 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 4. 검증 계획

- 빈 실사·목표 저장 button과 실제 `0` 저장 회귀를 Frontend unit으로 고정한다.
- UTC 날짜가 서울에서 다음 날이 되는 경계와 월 경계를 날짜 단위 test로 검증한다.
- 같은 달 날짜 변경은 추가 조회 `0`, 다른 달 이동은 조회 `1`, 늦게 끝난 이전 월 응답은 무시하는 회귀를 추가한다.
- Frontend 전체 lint·typecheck·unit·build, Backend 전체·G2/migration 영향 검사와 isolated Full-Stack을 다시 실행한다.
- 현재 local 검수 runtime에서 홈·생산/출하·제조 출근 화면과 빈 필수 입력 비활성 상태를 확인한다.

## 5. 다음 Gate

구현과 자동 검증을 완료했으며 사용자 검수로 돌아간다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 6. 검증 결과

- G2 targeted Frontend `12/12`, Frontend 전체 `230/230`, typecheck, lint error `0`, production build를 통과했다.
- Backend source 변경이 없는 상태에서 같은 검토 흐름의 isolated 전체 `548/548` 결과를 유지하고, G2·migration 영향 검사 `64/64`를 다시 통과했다.
- isolated Full-Stack `1/1`에서 실제 `0` 입력 가능, 빈 목표·실사 저장 button 비활성, Backend 서울 오늘과 관리 화면 초기 날짜 일치, 기존 권한·CAS·재고·mobile 390px를 확인했다.
- local 검수 화면에서 빈 목표·실사 저장 button 비활성, `0` 입력 후 활성, page overflow `0`을 확인했다. 저장은 실행하지 않아 기존 검수 data를 변경하지 않았다.
