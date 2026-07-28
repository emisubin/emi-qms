# TASK-PRODUCTION-CONTROL-001 Change 003 — 1:1 실적 연결과 단일 현재 양식

## 1. 실행 기준

- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `POLICY_DECISION`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `a7651b5`
- roadmapSequenceMatch: `true`
- source: 사용자 명시 정책 변경 및 구현 요청

## 2. 확인된 문제

1. 생산계획 항목마다 모든 실적 후보를 체크박스로 반복 표시해, 항목 수가 늘수록 화면이 길어지고 연결값을 한눈에 파악하기 어렵다.
2. 한 계획 항목에 여러 실적을 연결하는 기존 계약은 사용자가 확정한 `1개 계획 항목 : 1개 실적 데이터` 정책과 다르다.
3. 양식을 편집할 때 전체 항목을 다음 버전으로 복사하여 `v1`, `v2`, `v3`가 계속 누적된다.
4. 프로젝트에는 생성 당시 계획 항목·연결·제조 항목이 이미 별도 snapshot으로 복사되므로, 현재 양식의 전체 버전 복사본을 계속 보관하지 않아도 기존 프로젝트의 업무 데이터는 유지된다.

## 3. 변경 계약

- 생산계획 양식과 프로젝트 생산계획의 실적 연결을 체크박스 묶음에서 단일 드롭다운으로 변경한다.
- 각 활성 계획 항목은 실적 데이터 하나만 선택해야 한다.
- 제조 단계 또는 LQC 실적은 드롭다운 안에서 제조 항목까지 포함한 하나의 선택값으로 표시한다.
- 양식 관리 화면에서 버전 목록, 초안 복제, 활성화, 보관 동작을 제거한다.
- Item·업무 영역별 현재 양식 하나를 조회하고 `수정 → 저장/취소` 방식으로 직접 편집한다.
- 저장은 optimistic concurrency를 유지하고, 이후 생성되는 프로젝트부터 수정된 현재 양식을 snapshot으로 복사한다.
- 기존 프로젝트의 계획·연결·제조 snapshot은 변경하지 않는다.
- 기존 양식 버전 중 현재 양식만 전체 항목을 유지한다. 프로젝트가 참조하지 않는 이전 버전 행은 정리하고, 참조 중인 이전 버전은 FK 식별자만 남긴 뒤 중복 항목 payload를 제거한다.
- 기존 프로젝트에 여러 실적 연결이 있으면 조회 시 하나를 대표값으로 표시하고, 사용자가 해당 프로젝트 계획을 저장하는 시점에 1:1로 정리한다.

## 4. 검증 계약

- DB migration에서 현재 양식 단일화, 마스터 연결 1:1 제약과 이전 payload 정리를 검증한다.
- Backend에서 현재 양식 직접 저장, 동시 수정 충돌, 정확히 1개 연결 검증과 새 프로젝트 snapshot 불변조건을 검증한다.
- Frontend에서 버전 UI 제거, 수정/취소/저장 상태와 단일 드롭다운 연결을 검증한다.
- Backend·Frontend 관련 테스트, Frontend lint/build를 실행한다.
- 고정 검수 주소에서 관리자 PC 화면을 직접 확인한다.

## 5. 안전 경계

- implementationApproved: `true`
- implementationApprovalSource: `USER_EXPLICIT_POLICY_AND_FIX_REQUEST`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
