# TASK-PRODUCTION-CONTROL-001 Change 007 — 계획표 가독성과 일정 날짜 축

- taskType: `BUGFIX`
- instructionChainRead: `true`
- canonicalTask: `TASK-PRODUCTION-CONTROL-001`
- taskIdentityGate: `PASS_REUSE`
- roadmapSequenceMatch: `true`
- branch: `experiment/task-home-002-personalized-shell`
- baseHead: `a7651b5c266d`
- 범위: 현재 실험 worktree의 생산관리 탭 조회 UI, 회귀 테스트와 종료 문서
- 제외: 업무 기능·상태 전이·API·DB 변경, 대표 repo·`main`, Persistent UAT, 실제 provider, push·PR·merge

## 사용자 검수 실패

1. `계획 대비 실적` 표 헤더의 검은 배경 때문에 헤더 글자를 읽기 어렵다.
2. `계획·실적 일정표`의 막대 위에 날짜 위치 기준이 없어 계획·실적 기간을 직관적으로 해석하기 어렵다.

## 원인

- `.production-control-plan-head`가 검은 배경과 흰 글자를 직접 사용해 현재 흑백 와이어프레임의 밝은 표 구조와 충돌한다.
- 일정표는 전체 시작·종료일만 표시하고 막대 track에 대응하는 날짜 축과 기준선은 렌더링하지 않는다.

## 승인 범위와 불변조건

1. 표 헤더는 밝은 중립 배경, 검은 글자와 선으로 표시한다.
2. 일정표 상단에 전체 기간에서 자동 산출한 최대 6개의 날짜 기준을 표시한다.
3. 날짜 축과 각 일정 행은 같은 위치 비율로 세로 기준선을 표시한다.
4. 계획·실적 막대의 날짜 계산, 원본 데이터, 상태·권한·저장 방식은 바꾸지 않는다.
5. 긴 기간은 날짜 라벨을 간격 조정해 과밀 표시하지 않는다.
6. 좁은 화면에서도 날짜 축과 막대가 같은 너비를 사용하고 가로 overflow를 만들지 않는다.

## 검증

- 일정표 날짜 축이 첫 날짜, 마지막 날짜와 중간 날짜를 표시하는지 확인한다.
- 날짜 축 기준선과 계획·실적 막대가 같은 track에서 표시되는지 확인한다.
- 표 헤더의 실제 계산 스타일이 밝은 배경·검은 글자인지 확인한다.
- Frontend 전체 unit, lint, typecheck, build와 desktop·390px 브라우저 검수를 수행한다.
