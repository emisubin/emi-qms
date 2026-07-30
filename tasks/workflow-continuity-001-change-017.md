# TASK-WORKFLOW-CONTINUITY-001 change-017

## Task Identity Gate

- purposeIdentity: 기존 Pending 생성·재검사·업무 이동 계약에서 확인된 누락과 잘못된 표시를 수정한다.
- canonicalTask: `TASK-WORKFLOW-CONTINUITY-001`
- taskType: `BUGFIX`
- branch: `experiment/task-home-002-personalized-shell`
- baseHead: `48fe8cd78293`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- approvalSource: 사용자의 “권장 구현 순서대로 구현하자. 시작해” 지시

## 포함 범위

1. 일반 IQC 업무의 긴급 오표시를 제거하고, 실제 Pending 재검사 업무만 긴급으로 표시한다.
2. Pending 생성 시 조치 담당자 외 생산관리·영업·관련 품질 담당자에게 중복 없는 인앱 알림을 생성한다.
3. 조치 담당자에게만 실행 업무를 유지하고 참조 부서에는 조회 알림만 보낸다.
4. 자재 입고 확정 등 내 업무의 이동 버튼이 실제 대상 업무를 연다.
5. 기존 미완료 업무·알림의 누락을 멱등적으로 재조정한다.

## 보존할 불변조건

- 권한 없는 사용자의 입력 권한을 확대하지 않는다.
- 동일 이벤트의 중복 업무·알림을 만들지 않는다.
- 실제 Teams·메일 provider를 호출하지 않는다.
- 대표 Repository, `main`, Persistent UAT와 실제 provider를 변경하지 않는다.

## 검증

- backend 관련 단위·통합 테스트
- frontend My Work 이동·표시 테스트
- 전체 backend/frontend 회귀 테스트
