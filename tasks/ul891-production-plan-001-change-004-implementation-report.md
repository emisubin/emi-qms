# TASK-UL891-PRODUCTION-PLAN-001 Change 004 구현 보고 — 일정표 날짜 세로선 표시 복구

상태: `사용자 재검수 완료 / main 병합 승인`

## 원인

- 기간별 날짜선 계산과 DOM 생성은 정상이며 검수 프로젝트에는 3개 row × 23개 날짜선, 총 69개가 있었다.
- DESIGN-000 wireframe의 `.app-shell :where(span, i, em, b)` 규칙이 inline element 배경을 `transparent !important`로 초기화했다.
- Gantt 계획·실적 막대는 이미 정보성 예외였지만 날짜선은 예외에 포함되지 않아 실제 computed background가 투명이었다.
- 기존 E2E는 날짜선 개수만 확인해 투명 상태를 통과시켰다.

## 구현

1. 날짜축 tick 선을 wireframe 정보성 표시 예외로 등록해 `#8f8f8f`로 표시한다.
2. 본문 보조 날짜선은 1px `#c8c8c8`, 주요 날짜선은 2px `#8f8f8f`로 표시한다.
3. 계획 흰색·실적 검은색 막대와 기간별 일/주/월 선 생성 로직은 유지한다.
4. Full-Stack E2E에서 날짜축·주요선·보조선의 computed `background-color`를 각각 검증한다.

## 변경 파일

- `frontend/src/design-system/wireframe.css`
- `frontend/e2e/full-stack/ul891-user-corrections.full-stack.spec.ts`
- `tasks/ul891-production-plan-001-change-004.md`
- 본 구현 보고서와 사용자 검수 체크리스트
- `docs/00-product-roadmap.md`

## 검증

| 검증 | 결과 |
| --- | --- |
| 실행 중 검수 화면 computed style | PASS — axis `rgb(143,143,143)`, major `rgb(143,143,143)`, minor `rgb(200,200,200)` |
| 격리 Full-Stack Chromium | PASS — `1/1`, 실제 computed color 포함 |
| Frontend unit 기준선 | PASS — 직전 Change 003 `145/145`, 이번 변경은 CSS·E2E only |
| Frontend typecheck·lint·build | PASS — lint error 0, 기존 Fast Refresh warning 1과 chunk warning 유지 |
| Git whitespace 검사 | PASS — `git diff --check` |

## Finding

| Finding ID | 심각도 | 상태 | Resolution |
| --- | --- | --- | --- |
| `UL891-PLAN-C004-F01` | P2 | RESOLVED | wireframe 전역 reset에서 날짜선을 정보성 예외로 지정했다. |
| `UL891-PLAN-C004-F02` | P2 | RESOLVED | DOM 존재가 아닌 computed color 회귀를 추가했다. |

Open P0/P1/P2: `0/0/0`.

## 경계

- Backend·DB·migration·권한·workflow는 변경하지 않았다.
- 격리 Full-Stack DB/container/network는 제거됐다. 5175/5082 사용자 검수 서버는 유지한다.
- commit·push·PR·merge, 대표 repo·main·Persistent UAT·실제 provider는 실행하지 않았다.
