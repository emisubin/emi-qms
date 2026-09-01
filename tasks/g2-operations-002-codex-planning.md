# TASK-G2-OPERATIONS-002 — G2 납품 목표·불량·홈 시뮬레이션 Codex 기획안

- taskType: `NEW_FEATURE`
- authoringModel: `CODEX`
- planningMode: `USER_EXPLICIT_CODEX_DIRECT`
- planningStatus: `APPROVED`
- planningApproved: true
- implementationApproved: true
- userDecisionRequiredCount: 0
- 작성일: 2026-09-01

## 1. 목표

기존 G2 일일 운영에 납품 목표와 불량 수량을 정식 데이터로 추가한다. 홈 생산 현황표에서는 저장하지 않는 임시 예상값을 바로 입력해 같은 화면의 그래프·표·재고를 즉시 시뮬레이션한다.

## 2. 데이터 계약

- `불량`: 날짜별 nullable 0 이상 정수 원본 metric이다. 생산/출하 관리에서 제조·영업·System Administrator가 저장·정정한다.
- `납품 목표`: 적용 시작일별 0 이상 정수 target이다. 기존 목표 관리에서 제조·영업·System Administrator가 저장·정정한다.
- 미래 불량값은 기존 생산·납품과 같은 예상값으로 저장하고 서울 날짜 도래 시 자동 초기화한다.
- 재고 공식은 `전일 재고 + 오전 생산 + 오후 생산 - 납품 - 불량`이다. 실사 날짜는 기존처럼 계산값 대신 실사값을 사용하고 이전 정정의 영향을 차단한다.
- 기존 metric별 CAS, 빈 값과 실제 0 구분, target 적용 시작일·version 계약을 재사용한다.

## 3. 홈 계약

- 첫 그래프 바로 아래에 생산 현황표를 배치한다.
- 행 순서는 `오전 생산 → 오후 생산 → 생산 합계 → 납품 목표 → 납품 → 불량 → 재고`다.
- 오전 생산·오후 생산·납품·불량 셀은 홈에서 숫자를 임시 입력할 수 있다.
- 홈 입력은 브라우저 메모리에만 존재하고 API 저장을 호출하지 않는다. 월 이동·새 조회·새로고침 시 폐기된다.
- 임시값은 생산 합계, 두 그래프, KPI와 실사 checkpoint 기반 재고에 즉시 반영된다.
- 납품 목표는 첫 그래프에 파스텔 주황 점선과 hover 정보로 표시한다.
- 생산 현황표와 출근 현황표의 미래 예상 날짜 값은 파란색으로 표시하되, 휴일 날짜 열의 빨간 글자 규칙을 우선한다.

## 4. 관리 화면과 모바일

- 생산/출하 관리 입력과 월간 표에 불량을 추가한다.
- 월간 생산 표에는 납품 목표와 불량, 불량 반영 재고를 표시한다.
- 모바일 홈·월간 입력·출근 현황의 시작일/종료일 date input을 좁게 만들고 목표 적용 시작일 date input도 축소한다.
- 페이지 전체 가로 overflow를 만들지 않고 표·그래프의 기존 내부 scroll 계약을 유지한다.

## 5. 제외 범위

- 홈 임시값 저장·공유·감사 이력
- 납품 목표 대비 실적 KPI와 알림
- 불량 사유·유형·품목·프로젝트 연결
- 관리자 통합 입력/수정 이력과 손익관리
- 운영 배포와 실데이터 변경

## 6. 구현 순서

1. additive migration `0084`로 기존 check constraint에 `Defect`와 납품 목표 유형을 추가한다.
2. Backend contract·store·재고 계산·endpoint·권한 검증을 확장한다.
3. Frontend type/API/관리 입력/홈 임시 시뮬레이션/그래프/표/CSS를 반영한다.
4. migration fresh/existing, 권한, 미래값 초기화, 재고 checkpoint, Frontend unit/build와 desktop/390px를 검증한다.
5. Implementation report·Roadmap·검수 checklist를 실제 결과로 갱신한다.

## 7. 완료 기준

- 납품 목표와 불량이 권한·CAS·migration 계약대로 저장된다.
- 불량이 실사 경계를 보존하며 자동 재고에서 차감된다.
- 홈 임시 입력은 네트워크 저장 없이 즉시 그래프·표·KPI·재고를 갱신하고 재조회 시 사라진다.
- 납품 목표 주황 점선, 새 표 행·순서, 예상 파란 글자와 휴일 빨간 글자가 표시된다.
- 모바일 date input이 축소되고 390px page overflow가 없다.
- Open P0/P1/P2가 없다.
