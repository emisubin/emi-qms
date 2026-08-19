# TASK-G2-OPERATIONS-001 — G2 일일 운영관리 Codex 기획 검토

- reviewType: `CODEX_CONTENT_PRODUCT_REVIEW`
- reviewTarget: [g2-operations-001-codex-planning.md](g2-operations-001-codex-planning.md)
- reviewTargetAuthor: `CODEX`
- reviewReason: `USER_EXPLICIT_FABLE_FALLBACK`
- interviewSource: [g2-operations-001-interview.md](g2-operations-001-interview.md)
- repositoryTaskBaseline: `28991aecbeaeeeff6e636f002761825b666d7a5e`
- latestOriginMainObserved: `f00f64112ef1dfca5e9d15c9a45dc2e430d9983b`
- reviewStatus: `APPROVED_FOR_IMPLEMENTATION`
- planningApproved: true
- implementationApproved: true
- 작성일: 2026-08-18

## 1. 종합 판정

**기획 승인 요청 가능 — 구현은 아직 시작 불가**

Codex 기획안은 사용자가 확인한 Fable Round 6 계약을 의미 있게 바꾸지 않고, 신규 G2 영역을 기존 PMS 데이터와 분리한 최소 업무 단위로 구성했다. 권한, 입력 단위, 예상값, 자동 재고, 실사 경계, 목표 이력과 홈 표시가 하나의 구현 계약으로 이어지며 현재 추가로 받아야 할 blocking 제품 결정은 없다.

다만 다음은 구현 시작 전 필수 gate다.

1. 사용자가 이 기획안과 아래 review resolution을 승인해야 한다.
2. 구현 시작 시 최신 `origin/main`으로 기준선을 갱신하고 instruction chain·Task Identity Gate를 다시 통과해야 한다.
3. migration 번호와 최근 생산관리·운영 권한 변경을 최신 코드에서 다시 대조해야 한다.

## 2. 사용자 문제와 제품 방향 검토

### 사용자 문제

요청의 핵심은 복잡한 생산계획 시스템이 아니라, G2가 매일 반복해서 입력하는 소수의 숫자를 빠르게 남기고 월간 흐름을 한 화면에서 확인하는 것이다. 기획안은 프로젝트·품목·작업지시·개인 근태로 범위를 확대하지 않고 날짜별 숫자 관리에 집중한다.

### 기대 결과

- 오전·오후 담당자가 서로 기다리지 않고 각 생산량을 기록할 수 있다.
- 영업·물류·제조가 자기 담당 값을 같은 날짜 축에서 관리한다.
- 실사 기준점을 통해 단순 자동 재고의 오차가 영구 누적되지 않는다.
- 관리자는 생산·납품·재고·출근의 월간 흐름을 별도 보고서 없이 파악한다.

### Roadmap 정합성

G2는 기존 PMS 프로젝트 workflow와 연결되지 않는 별도 data domain이므로 현재 QMS·생산관리 범위와 목적 identity가 겹치지 않는다. Roadmap에 기록된 명시적 병렬 기획 승인과 일치한다. 손익관리를 후속 `NEW_FEATURE`로 분리한 것도 이번 단순 운영관리의 출시 가능성을 높인다.

## 3. 기능별 판단

| 분류 | 기능 | 판단과 근거 |
| --- | --- | --- |
| 유지 | G2 부모 메뉴와 홈·생산/출하·출근 3개 하위 메뉴 | 사용자의 업무 구조와 직접 일치하며 화면 책임이 명확하다. |
| 유지 | 날짜별 오전조·오후조 생산량 독립 저장과 일괄 저장 | 서로 다른 담당자 입력과 한 사람의 일괄 입력을 동시에 만족하는 핵심 기능이다. |
| 유지 | 일일 납품량 단일 값 | 조 구분이 필요 없다는 확정 계약을 보존한다. |
| 유지 | 출근 4개 원본과 오전·오후·하루 합계 | 개인 근태로 확대하지 않고 필요한 운영 인원만 집계한다. |
| 유지 | 날짜 기준 미래 예상 표시 | 별도 forecast workflow 없이 단순성을 유지한다. |
| 유지 | 자동 재고와 실사 checkpoint | 사용자가 별도 재고 화면을 원하지 않는다는 계약을 가장 직접적으로 구현한다. |
| 유지 | 적용 시작일별 생산목표·재고목표 | 과거 그래프 왜곡 없이 목표 변경을 표현한다. |
| 유지 | 모든 활성 사용자 조회, 부서별 변경 | 업무 공유성과 책임 분리를 함께 만족한다. |
| 추가 | metric별 version과 mixed permission 원자 검사 | 오전·오후 별도 입력 시 값 유실 및 부분 저장을 막는 필수 구현 안전장치다. |
| 추가 | 빈 값과 0의 구분 | 입력 누락을 실적 0으로 오인하지 않으면서 재고 계산만 이어가기 위해 필요하다. |
| 추가 | 실사 편집의 키보드 동작과 화면 읽기용 표 | 그래프 점만 클릭해야 하는 접근성·모바일 사용 문제를 예방한다. |
| 추가 | 음수 재고를 숨기지 않고 경고 | 자동 0 보정이 입력 누락이나 부족 재고를 감추는 것을 막는다. |
| 보류 | 예상값이 날짜 도래 후에도 미정정인지 알려주는 별도 확인 상태 | 사용자는 단일 최종값과 마지막 수정 정보만 선택했다. 별도 상태는 업무 절차가 필요하므로 후속 사용성 개선으로 둔다. |
| 보류 | Excel, 알림, 첨부, 기존 데이터 연동 | 현재 일일 숫자 입력·조회 목표에 필요하지 않으며 운영 복잡도를 크게 높인다. |
| 제거 | G2 손익관리 placeholder | 빈 메뉴는 사용할 수 없는 기능으로 오해하게 한다. 영업팀 전용 별도 신규 기능에서 권한과 계산 원본을 함께 기획해야 한다. |

## 4. Interview 계약 대조

| 확정 계약 | 기획 반영 | 판정 |
| --- | --- | --- |
| 모든 활성 사내 사용자 조회 | G2 전체 `G2.Read` | 일치 |
| 생산·출근: 제조/영업 | field permission과 API 강제 | 일치 |
| 납품: 영업/물류 | field permission과 API 강제 | 일치 |
| 목표·실사: 제조/영업/관리자 | 별도 manage permission | 일치 |
| 오전·오후 생산을 각각 또는 한꺼번에 입력 | metric 단위 저장 + batch 요청 | 일치 |
| 미래 값은 예상, 나중에 최종값으로 교체 | 날짜 기반 표시, 과거 예상 snapshot 없음 | 일치 |
| 출근 4개 값과 단순 합계 | 파생식으로 처리 | 일치 |
| 실사는 자동 재고의 불변 기준점 | 다음 실사 전날까지만 재계산 | 일치 |
| 실사 입력은 홈 그래프에서, 미래 불가 | 홈 dialog와 서버 날짜 검사 | 일치 |
| 목표 변경 이력과 적용 시작일 보존 | target effective-date 행 | 일치 |
| 홈의 두 그래프와 출근표 | 세 구성 모두 명시 | 일치 |
| 손익관리는 나중에 | 이번 scope에서 제거, 후속 추적 | 일치 |

## 5. Finding

### `G2-PLAN-REV-001` — 빈 값과 0의 이중 의미

- severity: `P2`
- status: `RESOLVED_IN_CODEX_PLAN`
- 원인: 사용자가 값을 입력하지 않은 날에도 자동 재고는 0으로 계산해 이어져야 한다.
- 영향: 화면까지 빈 값을 0으로 바꾸면 미입력과 실제 무생산·무납품을 구분할 수 없다.
- resolution: 원본과 관리 화면은 `null`과 `0`을 구분하고, 재고 domain 계산에서만 `null => 0`을 적용하도록 기획에 고정했다.
- 검증: API serialization, 관리 화면, 재고 계산 unit test에서 각각 확인한다.

### `G2-PLAN-REV-002` — 오전·오후 분리 입력의 값 유실 가능성

- severity: `P2`
- status: `RESOLVED_IN_CODEX_PLAN`
- 원인: 날짜 하나를 넓은 행 전체로 저장하면 오전 담당자와 오후 담당자의 동시 수정이 서로의 값을 덮어쓸 수 있다.
- 영향: 일일 생산량과 재고 계산이 잘못된다.
- resolution: 날짜·metric별 version과 제공된 필드만 갱신하는 CAS 저장 계약을 추가했다.
- 검증: 같은 metric racing은 한 건만 성공하고, 서로 다른 metric racing은 둘 다 보존되는지 확인한다.

### `G2-PLAN-REV-003` — 생산·납품 혼합 권한의 부분 저장 위험

- severity: `P2`
- status: `RESOLVED_IN_CODEX_PLAN`
- 원인: 생산과 납품은 같은 화면이지만 허용 부서가 다르다.
- 영향: 클라이언트 제어만 믿거나 field별로 순차 저장하면 권한 없는 값이 저장되거나 요청 일부만 반영될 수 있다.
- resolution: 서버가 제공된 모든 필드의 권한과 version을 먼저 검사하고 하나의 transaction으로 원자 저장하도록 고정했다.
- 검증: 제조 persona가 생산+납품을 함께 보내면 전체 `403`이고 생산도 변경되지 않는지 확인한다.

### `G2-PLAN-REV-004` — 실사 graph interaction의 접근성

- severity: `P2`
- status: `RESOLVED_IN_CODEX_PLAN`
- 원인: 실사 입력을 그래프 marker 클릭에만 의존하면 키보드·좁은 화면 사용자가 조작하기 어렵다.
- 영향: 권한이 있어도 핵심 보정 기능을 사용할 수 없다.
- resolution: marker와 동등한 날짜별 접근 가능 동작, focus 가능한 dialog, action 인접 오류, `aria-live` feedback을 추가했다.
- 검증: desktop mouse, keyboard-only, 390px mobile smoke를 모두 수행한다.

### `G2-PLAN-REV-005` — 예상값의 날짜 도래 후 정정 누락

- severity: `P3`
- status: `BACKLOG`
- 원인: 예상 여부를 날짜로만 판단하고 과거 예상 snapshot·확인 상태를 보존하지 않는다.
- 영향: 담당자가 실제값으로 교체하지 않아도 날짜가 지나면 화면상 예상 표시는 사라진다.
- 현재 판단: 사용자가 선택한 단순 최종값 계약을 유지한다. 별도 확인 상태·알림을 이번 Task에 추가하면 신규 workflow가 된다.
- 완화: 마지막 수정 시각을 항상 표시하고 사용자 검수에서 날짜 전환 동작을 설명한다.
- 재검토 시점: 실제 운영 중 미정정 예상값이 반복될 때 별도 `NEW_FEATURE` 또는 승인된 change로 검토한다.

### `G2-PLAN-REV-006` — 기준 branch와 최신 main의 차이

- severity: `P2`
- status: `RESOLVED_AS_IMPLEMENTATION_PREREQUISITE`
- 원인: Task worktree 기준 SHA 이후 원격 main에 생산관리·운영 권한 관련 변경이 추가됐다.
- 영향: migration 번호, permission seed, navigation 또는 테스트 기준이 달라질 수 있다.
- resolution: 기획안에서 migration 번호를 고정하지 않고, 구현 시작 첫 단계에 최신 main 동기화와 instruction/identity gate 재수행을 필수화했다.
- 검증: 구현 세션 시작 보고에 최신 base SHA, clean worktree, 적용 instruction과 다음 migration 번호를 기록한다.

## 6. 과도한 범위와 운영 부담 검토

기획안은 다음 확장을 의도적으로 피하므로 MVP 범위가 유지된다.

- 사람별 근태·교대·근무시간 계산 없음
- 프로젝트·품목·생산계획 연결 없음
- 예상 대비 실적 분석 없음
- 납품목표 없음
- 자동 알림·승인 workflow 없음
- 일별 재고 결과 materialization 없음
- 손익 계산과 영업 전용 화면 없음

추가된 permission 6개와 세 테이블은 다소 세분화되어 보이지만, 한 화면 안의 부서별 field 권한과 동시 저장을 서버에서 안전하게 처리하기 위한 최소 구조다. 하나의 광범위한 `G2.Update`로 합치면 제조 사용자의 납품 변경 같은 권한 누수가 발생하므로 유지한다.

## 7. 권장 개발 순서

1. 최신 main 동기화·Task gate 재확인
2. schema와 permission seed
3. 순수 재고 계산·동시성·권한 backend tests
4. API와 관리 화면
5. 홈 그래프·출근표·실사 편집
6. desktop/mobile 접근성 및 full-stack 검증
7. Implementation report와 사용자 검수 handoff

가장 위험한 부분은 그래프 자체가 아니라 재고 경계 계산, mixed permission과 분리 입력 동시성이다. 따라서 화면부터 만드는 것보다 이 세 계약을 backend test로 먼저 고정하는 순서가 적절하다.

## 8. Review resolution

### 유지

- 사용자가 확인한 전체 업무 범위와 권한
- 단순 최종값·마지막 수정 정보 계약
- 실사를 기준점으로 한 자동 재고
- 두 그래프와 출근 현황표
- 기존 PMS와의 완전 분리

### 추가

- metric별 CAS와 mixed permission transaction
- 빈 값/0의 계산 경계
- 음수 재고 경고
- 접근 가능한 실사 편집과 그래프 대체 표
- 구현 시작 시 최신 main 재기준선 gate

### 보류

- 미정정 예상값 확인 workflow
- 손익관리, 연동, Excel, 알림

### 제거

- 사용할 수 없는 손익관리 placeholder
- 일별 자동 재고 수동 입력 화면
- 예상 대비 실적과 납품목표

## 9. 다음 Gate

이 review의 권고는 Codex 기획안에 이미 반영되어 있어 추가 문서 재작성 round는 필요하지 않다. 사용자가 `기획안과 Codex 검토 resolution 승인`을 명시하면 다음 별도 Codex 구현 세션에서 승인된 계약만 구현할 수 있다.

현재 상태는 다음과 같다.

- planning draft 작성: 완료
- Codex 내용·제품 review: 완료
- planning 승인: 대기
- 구현 승인: 대기
- 코드·DB 변경: 없음
- commit·push·PR·merge·배포: 없음
