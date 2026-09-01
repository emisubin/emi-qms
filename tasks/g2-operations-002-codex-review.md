# TASK-G2-OPERATIONS-002 — Codex 내용·제품 검토

- reviewTarget: [g2-operations-002-codex-planning.md](g2-operations-002-codex-planning.md)
- reviewStatus: `APPROVED_FOR_IMPLEMENTATION`
- planningApproved: true
- implementationApproved: true
- 작성일: 2026-09-01

## 종합 판정

구현 가능하다. 납품 목표와 불량은 기존 G2 target·metric 구조에 자연스럽게 추가할 수 있고, 홈 임시 입력은 서버 저장과 분리된 파생 view model로 구현하면 정식 데이터와 혼동되지 않는다.

## 기능 판단

| 분류 | 항목 | 판단 |
| --- | --- | --- |
| 유지 | 정식 납품 목표 | 기존 적용 시작일별 target 계약을 재사용한다. |
| 유지 | 정식 불량 원본 | 생산·재고 운영에 필요한 최소 숫자이며 제조·영업·관리자 권한이 기존 생산 권한과 일치한다. |
| 유지 | 홈 저장 없는 임시 입력 | 조회·의사결정용 요구를 충족하면서 원본과 감사 의미를 오염시키지 않는다. |
| 추가 | 임시값 초기화 안내·초기화 동작 | 저장된 값으로 오인하는 위험을 낮춘다. |
| 추가 | 실사 checkpoint를 보존하는 Frontend 재고 재계산 | 임시값이 Backend 재고 규칙과 달라지는 것을 막는다. |
| 보류 | 납품 목표 달성률·불량률 KPI | 사용자가 요청하지 않았고 분모·평균 정책이 필요하다. |
| 제거 | 별도 불량 관리 메뉴 | 일일 숫자 한 개에 과도하며 생산/출하 화면이 사용자의 지정 위치다. |

## Finding과 resolution

### `G2-002-PLAN-001` — 홈 임시값과 정식 예상값 혼동

- severity: `P2`
- status: `RESOLVED_IN_PLAN`
- resolution: 입력 영역에 저장되지 않음과 재조회 시 초기화됨을 표시하고 저장 버튼을 만들지 않는다.

### `G2-002-PLAN-002` — 불량 차감의 실사 경계 불일치

- severity: `P2`
- status: `RESOLVED_IN_PLAN`
- resolution: Backend·Frontend 모두 실사 날짜는 실사값을 우선하고 불량은 다음 비실사일부터 차감한다.

### `G2-002-PLAN-003` — 별도 권한 과설계

- severity: `P3`
- status: `RESOLVED_IN_PLAN`
- resolution: 불량 변경 대상이 기존 생산 변경 대상과 완전히 같으므로 `G2.Production.Update`를 재사용하고 새 permission은 만들지 않는다.

## Review resolution

기획안을 그대로 구현한다. commit·push·PR·merge·운영 배포는 별도 승인 전 실행하지 않는다.
