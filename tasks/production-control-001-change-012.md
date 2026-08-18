# TASK-PRODUCTION-CONTROL-001 Change 012 — 프로젝트 생산계획 항목명 교체 저장 안정화

## Task Identity Gate

- proposedTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: 후속 제품 기능
- roadmapNextGate: 사용자 지정 오류 수정
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 프로젝트 생산계획에서 최종 항목명은 유효하지만 이름 맞교환 또는 삭제할 이름 재사용 때문에 저장 도중 일시적 중복이 생기는 오류를 제거한다.
- Root Finding 또는 정책 결정: 서버가 기존 활성 항목명을 유지한 채 행을 순서대로 갱신해 partial unique index와 중간 상태에서 충돌했다.
- 변경·검증 경계: 생산계획 저장 transaction과 해당 Backend 회귀만 변경한다. 화면·권한·실적 계산·DB schema는 변경하지 않는다.
- 보존할 불변조건: 최종 활성 항목명 중복은 계속 거부하고, 삭제 이력·row version·audit·프로젝트 snapshot·UL891 세트 계약을 유지한다.
- 예상 산출물: 항목명 임시 격리 후 최종값 적용, 잔여 DB 충돌의 사용자용 validation 변환, 이름 맞교환·삭제 후 재사용·반복 저장 회귀.

## 승인 계약

- 사용자는 2026-08-18 조사 결과에서 정리한 1번 수정의 구현을 명시 승인했다.
- 운영 DB·runtime, 원격 게시·병합·배포는 이번 구현 범위에 포함하지 않는다.
- 사용자는 자동검증 결과와 사용자 검수 미완료 상태를 보고받은 뒤 2026-08-18 Change 012·013, 품질 Change 007과 Infra Change 001의 원격 `main` 병합·Azure 공개배포를 명시 승인했다. 사용자 운영 검수는 배포 후 Gate로 남긴다.

## 구현 방향

1. 저장 대상이 있을 때 현재 활성 행의 순번뿐 아니라 항목명도 사용자가 입력할 수 없는 내부 임시값으로 한 transaction 안에서 격리한다.
2. 삭제 행은 비활성화하면서 원래 항목명을 복원하고, 유지 행과 신규 행은 정규화된 최종 이름을 적용한다.
3. 최종 활성 이름 중복 validation은 기존과 동일하게 저장 전에 수행한다.
4. 같은 unique constraint가 예상 밖 데이터 조합으로 다시 발생하면 전역 500으로 노출하지 않고 생산계획 항목 오류로 반환한다.

## 검증 계약

- 기존 두 행의 항목명 맞교환 저장 성공
- 삭제 예정 행보다 먼저 처리되는 행이 삭제될 이름을 재사용해도 저장 성공
- 같은 최종 내용을 다시 저장해도 성공
- 최종 활성 항목명이 실제로 중복되면 기존 400 validation 유지
- 생산계획 Backend 집중 회귀와 Release build 통과

## Finding

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `PC-C012-ACTIVE-NAME-TRANSIENT-001` | P1 | `RESOLVED` | 유효한 최종 payload도 행 단위 갱신 중 기존 이름과 일시 충돌해 transaction이 롤백되고 사용자는 일반 재시도 오류만 받았다. | 같은 transaction에서 기존 활성 이름을 사용자 입력 불가 임시값으로 격리한 뒤 삭제·수정·추가의 최종 이름을 적용했다. 이름 맞교환·삭제 후 재사용·반복 저장 회귀를 통과했고, 해당 unique constraint의 잔여 충돌도 구체 validation으로 변환했다. |
