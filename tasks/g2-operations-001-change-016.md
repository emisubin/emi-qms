# TASK-G2-OPERATIONS-001 Change 016 — 예상값 날짜 도래 자동 초기화와 관리표 구분행 제거

- taskType: `POLICY_DECISION`
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

## 1. 사용자 지정 데이터 계약

- 서울 날짜 기준 오늘보다 미래인 생산·납품·출근 입력값은 저장 시 예상값으로 식별한다.
- 해당 날짜가 되면 `예상` 표시는 날짜 기준으로 자동 해제하고, 미리 입력한 예상 수량·인원 숫자는 빈 값으로 초기화한다.
- 적용 항목은 오전·오후 생산, 일일 납품, 오전·오후 EMI·도급 출근의 7개 metric이다.
- 예상 `0`도 실제 0으로 전환하지 않고 같은 규칙으로 초기화한다.
- 당일 또는 과거 날짜에 새로 입력한 값은 실제값이므로 자동 초기화하지 않는다.
- 생산/출하 관리와 제조 인원 출근 관리 월간표는 날짜 header의 미래 `예상` 표시만 유지하고 중복 `구분` 행은 모두 제거한다.

## 2. 구현 결정

- additive migration `0082_g2_forecast_expiry.sql`로 `g2_daily_metrics.is_forecast`를 추가한다.
- migration 적용 당시 서울 오늘보다 미래이고 수량이 있는 기존 행만 예상값으로 backfill한다. 오늘·과거 값과 빈 행은 실제/빈 값으로 보존한다.
- 미래 수량 저장은 `is_forecast=true`, 미래 빈 값 또는 오늘·과거 저장은 `false`로 기록한다.
- 서울 날짜가 도래한 뒤 첫 G2 조회 또는 저장에서 `is_forecast=true AND work_date<=today`인 행만 원자적으로 `quantity=null`, `is_forecast=false`로 바꾸고 version을 증가시킨다.
- 행을 물리 삭제하지 않고 수량을 빈 값으로 바꿔 nullable 원본, metric별 CAS와 수정 충돌 방어를 유지한다. 사용자 화면과 재고 계산에서는 삭제된 숫자와 동일하게 빈 값으로 처리한다.
- 별도 상시 scheduler는 추가하지 않는다. 사용자가 G2를 처음 조회하는 시점보다 늦게 화면에 남아 보이는 구간 없이 해당 요청 안에서 먼저 초기화한 후 응답·재고를 계산한다.

## 3. 보존 범위

- 재고 실사와 일 생산목표·재고목표는 예상 자동 초기화 대상이 아니다.
- 과거·오늘 실제값, 실사 checkpoint, 재고 계산 공식, 역할별 field permission과 metric별 CAS를 유지한다.
- 과거 예상 snapshot과 예상 대비 실적은 보존하지 않는다.
- 관리자 입력·수정 이력 조회는 별도 `NEW_FEATURE`로 유지한다.
- 원격 `main`, Persistent UAT와 Azure 공개배포는 변경하지 않는다.

## 4. 검증 결과

- fresh migration과 `0081 → 0082` forward migration에서 예상 marker backfill·constraint·partial index를 확인했다.
- 미래 생산·납품·출근과 예상 `0`을 저장한 뒤 TimeProvider의 서울 날짜를 하루 진행시켜, 첫 조회에서 전부 빈 값·version 증가가 되는지 확인했다.
- 같은 날짜에 다시 입력한 실제값과 실제 `0`은 후속 조회에서도 유지됨을 확인했다.
- Backend 전체 `548/548`, Release build warning/error `0/0`을 통과했다.
- Frontend 전체 `226/226`, typecheck, lint error `0`·기존 warning `1`, production build를 통과했다.
- local 검수 화면에서 생산/출하 표 행 `오전 생산·오후 생산·생산 합계·납품·재고`, 제조 출근 표 기본행 `오전 합계·오후 합계·하루 총원`만 남고 두 표 모두 `구분` row `0`, 미래 날짜 header `예상` 유지를 확인했다.
- local 검수 DB에 `0082`를 적용했고 기존 미래 입력은 예상 marker로 backfill됐으며 만료된 예상 marker `0`건을 privacy-safe count로 확인했다.

## 5. 다음 Gate

자동 검증과 local 검수 runtime 반영을 완료했으며 사용자 검수를 받는다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.
