# TASK-PRODUCTION-CONTROL-001 change-009

## Task Identity Gate

- purposeIdentity: 생산계획 공통 코멘트의 의미와 조회 위치를 명확히 한다.
- canonicalTask: `TASK-PRODUCTION-CONTROL-001`
- taskType: `BUGFIX`
- branch: `experiment/task-home-002-personalized-shell`
- baseHead: `48fe8cd78293`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- approvalSource: 사용자의 “권장 구현 순서대로 구현하자. 시작해” 지시

## 포함 범위

- 수정 화면 명칭을 `생산관리 전체 전달사항`으로 변경한다.
- 프로젝트 상세 생산관리 탭의 생산계획표 상단에 값이 있을 때만 조회 표시한다.
- 계획 항목별 생산관리 코멘트는 그대로 유지한다.

## 검증

- 저장·재조회
- 생산관리 탭 표시
- 빈 값일 때 불필요한 영역 미표시
