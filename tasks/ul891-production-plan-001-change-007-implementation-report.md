# TASK-UL891-PRODUCTION-PLAN-001 Change 007 구현 보고 — 일정표 외곽·왼쪽 헤더 구분선 실선화

상태: `사용자 재검수 완료 / main 병합 승인`

## 구현 결과

- 날짜축 상단·좌우와 일정 본문 하단·좌우를 결합해 표 외곽 4면을 `1px #111` 일반 실선으로 고정했다.
- 날짜축과 각 일정 행의 왼쪽 항목 헤더 구분선을 `1px #111` 일반 실선으로 통일했다.
- 마지막 일정 행의 내부 하단선을 제거해 표 외곽 하단선과 중복되지 않게 했다.
- 좁은 화면에서 헤더가 위로 쌓일 때도 해당 구분선을 일반 실선으로 유지했다.
- 내부 얇은 날짜 점선, 굵은 주요 날짜 실선, 날짜 헤더 무선 상태와 계획·실적 막대는 변경하지 않았다.

## 검증

| 검증 | 결과 |
| --- | --- |
| 실행 중 검수 화면 | PASS — 외곽 4면과 왼쪽 헤더 구분선 solid, 내부 minor dashed·major solid |
| 격리 Full-Stack Chromium | PASS — `1/1` |
| Frontend typecheck·lint·build | PASS — lint error 0, 기존 Fast Refresh warning 1과 chunk warning 유지 |
| Git whitespace 검사 | PASS — `git diff --check` |

Open P0/P1/P2: `0/0/0`.

commit·push·PR·merge, 대표 repo·main·Persistent UAT·실제 provider는 실행하지 않았다.
