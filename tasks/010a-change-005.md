# TASK-010A change-005

## Task Identity Gate

- purposeIdentity: 전체 흐름 8번의 표시·상태·업무 생성 계약을 현재 제조 요청 정책과 일치시킨다.
- canonicalTask: `TASK-010A`
- taskType: `BUGFIX`
- branch: `experiment/task-home-002-personalized-shell`
- baseHead: `48fe8cd78293`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- approvalSource: 사용자의 “권장 구현 순서대로 구현하자. 시작해” 지시

## 포함 범위

1. 8번 표시를 `생산관리 / 제조 요청`으로 통일한다.
2. 최초 입고 확정 시 8번을 진행 중으로 전환한다.
3. 생산관리에는 `제조 투입 검토·요청`, 자재에는 `키팅 검토(선택)` 업무와 알림을 한 번만 생성한다.
4. 패널별 제조 요청이 진행되면 부분 완료, 전체 활성 패널이 요청되면 완료로 계산한다.
5. 선택 키팅 완료는 제조 요청 단계의 완료 조건으로 사용하지 않는다.

## 보존할 불변조건

- 키팅은 선택 업무이며 제조를 차단하지 않는다.
- 무자재 프로젝트의 생산관리 제조 요청을 허용한다.
- 동일 프로젝트의 후속 입고가 업무·알림을 중복 생성하지 않는다.
- 대표 Repository, `main`, Persistent UAT와 실제 provider를 변경하지 않는다.

## 검증

- workflow 상태 계산·재조정 테스트
- 최초 입고 확정 업무·알림 멱등성 테스트
- frontend 전체 흐름 표시 테스트
