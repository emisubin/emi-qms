# TASK-PRODUCTION-CONTROL-001 Change 002 — 양식 관리 정보구조와 편집 화면 보정

## 1. 실행 기준

- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `f8eb6ce63ed8dfd89176d2755fe25b4a20df7d28`
- roadmapSequenceMatch: `true`
- source: 사용자 검수 실패

## 2. 재현 결과와 원인

1. `생산계획·Item별 제조 연결`이 기존 `양식 종류` 목록에 포함되지 않고 화면 상단의 별도 전환 메뉴로 구현되어, 사용자가 같은 양식 관리 기능으로 인식하기 어렵다.
2. 생산계획 양식은 각 계획 항목 아래에 모든 부서 실적 연결 항목을 동시에 펼친다. 항목 수만큼 같은 체크박스 묶음이 반복되어 순서, 현재 선택과 편집 대상을 구분하기 어렵다.
3. Item 선택, 버전 선택, 편집 동작과 항목 편집이 하나의 긴 세로 화면에 놓여 작업 순서가 명확하지 않다.

## 3. 변경 계약

- 기존 `양식 종류` 목록에 `Item별 제조 양식`, `생산계획·실적 연결`을 같은 1차 탐색 항목으로 추가한다.
- 별도 상단 전환 메뉴와 내부의 중복 양식 탭을 제거한다.
- 연결형 양식은 선택한 종류에 맞는 Item, 버전, 편집 화면만 표시한다.
- 생산계획 항목은 한 줄 요약 목록으로 표시하고, 사용자가 선택한 항목 하나만 펼쳐 이름·필수 여부·실적 연결을 편집한다.
- 제조 항목은 헤더가 있는 정렬된 표 형태로 표시한다.
- 기존 API, 권한, 버전 lifecycle, 프로젝트 snapshot과 실적 연결 규칙은 변경하지 않는다.

## 4. 검증 계약

- 양식 종류 접근성과 선택 상태를 Frontend 자동 테스트로 검증한다.
- 생산계획 항목이 기본적으로 접혀 있고 선택한 항목만 편집 영역을 표시하는지 검증한다.
- Frontend lint, 관련 테스트, 전체 Frontend 테스트와 build를 실행한다.
- 고정 검수 주소에서 관리자 PC 화면과 좁은 화면을 직접 확인한다.

## 5. 안전 경계

- implementationApproved: `true`
- implementationApprovalSource: `USER_EXPLICIT_FIX_REQUEST`
- commitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- backendContractChange: `false`
- persistentUatApproved: `false`
