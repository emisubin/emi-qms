# TASK-G2-OPERATIONS-001 Change 011 — 휴일 강조·graph 축과 날짜 밀도 보정

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

- 두 graph의 모든 x축 날짜 글씨를 한 단계 더 줄이고 baseline과 날짜 label 사이 간격을 좁힌다.
- 토요일·일요일·활성 한국 공휴일은 두 graph의 날짜 label과 모든 G2 가로표 날짜 header에서 빨간색으로 표시한다.
- 공휴일은 기존 `system_holidays` read API의 활성 `KR` data를 재사용하고, 조회 실패가 G2 본문을 막지 않게 주말 표시는 계속 유지한다.
- 가로표 날짜 header의 과거·오늘 `실적` 보조 문구는 제거하고 미래 `예상` 문구는 유지한다.
- 생산·납품 graph 왼쪽 축은 `0~80/20`, 오른쪽 재고축은 `0~180/20`을 유지한다.

## 2. 검수 data 계약

- 2026년 8월 일 생산목표는 모든 날짜 `50대`로 다시 입력한다.
- 공식 월력요항에 따라 local 검수 DB의 2026-08-15 광복절과 2026-08-17 광복절 대체공휴일을 활성 한국 공휴일로 입력한다.
- 기존 Excel 기반 생산·납품·자동 재고, 출근 인원과 재고목표는 변경하지 않는다.

## 3. 보존 범위

- G2 API·DB schema·권한·재고 계산·입력 workflow는 변경하지 않는다.
- graph tooltip·기본 cursor·전체 날짜·값 label·가로 overflow 0과 표 내부 scroll을 유지한다.
- 원격 `main`, Persistent UAT, Azure 공개배포와 실제 운영 data는 변경하지 않는다.

## 4. 검증 계획

- weekend·holiday helper, graph red label, table red header·실적 제거, `0~80/20` 축 unit 검사
- Frontend lint·typecheck·G2 집중·전체 unit·production build·diff check
- local desktop 1440·mobile 390에서 날짜 크기·간격·주말/공휴일 red·표 header·목표 50·overflow 확인
- API 재조회로 8월 일 생산목표 전 날짜 `50`, Excel production/delivery/inventory 보존 확인

## 5. 공식 기준

- 한국천문연구원·우주항공청 `2026년 월력요항`: 2026-08-15~17은 광복절·일요일·광복절 대체공휴일 3일 연휴다.

## 6. 다음 Gate

구현과 자동·browser 검증 뒤 local G2 홈을 사용자 검수로 전달한다. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.

## 7. 구현·검증 결과

- graph 날짜 label을 desktop `7px`, mobile `8px`로 줄이고 baseline과의 SVG 간격을 `18`로 좁혔다.
- UTC 요일과 기존 활성 한국 공휴일 API를 하나의 helper로 결합해 두 graph와 홈·관리 화면의 모든 G2 가로표에 같은 빨간 날짜 규칙을 적용했다.
- 공통 wireframe table header 규칙이 G2 휴일 색상을 덮지 않도록 명시적 semantic exception을 추가했다.
- 생산·납품 graph 왼쪽 축을 `0~80/20`으로 조정하고 8월 일 생산목표를 전 날짜 `50대`로 다시 입력했다.
- local 검수 DB에 2026-08-15 광복절과 2026-08-17 광복절 대체공휴일을 입력했다. 사용자 제공 workbook의 생산·납품·자동 재고 수치와 기존 출근·재고목표가 변경 전후 일치함을 API projection으로 확인했다.
- Frontend G2 집중 `6/6`, 전체 `224/224`, lint error `0`, typecheck, production build를 통과했다.
- desktop 1440에서 graph red 날짜 `22`개(두 graph 각 11), 홈 표 red header `22`개(두 표 각 11), 날짜 label `62/62`, x축 font `7px`, baseline 간격 `18`, page overflow `0`을 확인했다. mobile 390에서도 red 날짜와 page overflow `0`을 확인했다.
- 사용자 검수 runtime은 Frontend `http://127.0.0.1:42983/g2`, Backend `http://127.0.0.1:41166`에서 유지한다.
