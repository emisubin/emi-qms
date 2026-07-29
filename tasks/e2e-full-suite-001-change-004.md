# TASK-E2E-FULL-SUITE-001 change-004

## Gate

- instructionChainRead: true
- taskType: P2_REMEDIATION
- canonicalTaskId: TASK-E2E-FULL-SUITE-001
- gateStatus: PASS_REUSE
- sourceBranch: experiment/task-home-002-personalized-shell
- mainMutationAllowed: false
- remoteMutationAllowed: false

## 사용자 확정 변경

1. 18단계 workflow는 `전체 흐름` 탭 안에서만 표시하고 시각적 진행 보드로 정리한다.
2. 생산관리 탭은 계획 항목표와 캘린더를 담당자보다 먼저 표시한다.
3. 읽기 전용 담당자 카드는 부서명·정·부 담당자가 한 행에서 읽히는 압축 구조로 바꾼다.
4. 생산관리 개발 계정으로 전체 흐름과 생산관리 탭을 desktop 기준으로 캡처하고 UX를 재평가한다.

## 불변조건

- workflow 집계·상태·권한·수정 API는 변경하지 않는다.
- 생산계획 수정은 기존 담당 권한과 Active project gate를 유지한다.
- 대표 repo, `main`, Persistent UAT, push, PR과 merge는 제외한다.
