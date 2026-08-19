# TASK-G2-OPERATIONS-001 Change 002 — 합성 데이터 검수 서버 실행

- taskType: `UAT_RUNTIME`
- changeStatus: `LOCAL_VALIDATION_RUNTIME_ACTIVE`
- userInstruction: `데이터 입력해놓고 검수 서버 띄워봐`
- userInstructionDate: 2026-08-19
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- localValidationApproved: true
- persistentUatApproved: false
- azureDeploymentApproved: false
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false

## 1. 승인 범위

사용자 직접 검수를 위해 현재 G2 feature branch의 Frontend·Backend와 전용 local PostgreSQL을 실행하고 합성 G2 데이터를 입력한다. 운영·Persistent UAT DB, 실제 사용자·프로젝트 data, 외부 provider와 Azure 공개배포는 변경하지 않는다.

## 2. 검수 환경

| 항목 | 값 |
| --- | --- |
| Frontend | `http://127.0.0.1:42983/g2` |
| Backend | `http://127.0.0.1:41166` |
| Database container | `emi-qms-g2-validation` |
| Database | `emi_qms_experiment_validation_41164` |
| Source branch | `feat/task-g2-operations-001` |
| Source base | `4a220d446b1fb71604c4289f1cf7d85eec41712d` |

실제 provider worker와 발송 채널은 모두 비활성화한다. 개발용 고정 역할과 합성 숫자만 사용한다.

## 3. 입력 데이터

- 2026년 8월 1일~31일 생산·납품·출근 7개 원본 값
- 일 생산목표 적용일 2개, 재고목표 적용일 2개
- 재고 실사 3일: 8월 1일·10일·19일
- 서울 기준 미래 12일은 화면에서 `예상`으로 표시
- 생산은 제조, 납품은 물류, 목표·실사는 영업 개발 역할로 입력해 수정자 구분을 확인할 수 있게 구성

## 4. 확인 결과

- Backend home API `200`, 8월 날짜 `31`개
- 미래 예상 날짜 `12`개
- 실사 `3`개, 생산·재고 목표 적용일 각 `2`개
- G2 홈의 두 그래프와 출근 현황 표시
- 생산/출하 입력 화면에 기존 값 표시
- 출근 관리 입력 4개 표시
- 손익관리 메뉴 없음
- browser console error `0`

## 5. Finding

| ID | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `G2-LOCAL-DB-PORT-001` | P3 | `RESOLVED` | 기존 검수용 PostgreSQL container는 host port를 공개하지 않아 현재 worktree Backend가 `5432`로 연결되지 못했다. | 기존 DB·volume을 변경하지 않고 Task 전용 `emi-qms-g2-validation` container를 `127.0.0.1:5432`에 생성해 fresh migration과 합성 data를 적용했다. |
| `G2-LOCAL-EVIDENCE-002` | P3 | `RESOLVED` | 최종 HTTP 두 주소 확인에서 출력 억제 option이 첫 요청에만 적용되어 정적 Frontend HTML이 transient 검증 출력에 포함됐다. 실제 사용자·업무 data와 credential은 포함되지 않았다. | 해당 출력을 증빙에서 폐기하고 각 요청에 출력 억제를 개별 적용해 status-only projection으로 다시 확인했다. |

## 6. 다음 Gate

1. 사용자가 열린 local G2 화면을 직접 검수한다.
2. 실패 항목은 기존 Task의 다음 change 또는 확인된 결함 BUGFIX로 처리한다.
3. 검수 종료 뒤 Task 소유 runtime·전용 DB 정리는 사용자 요청 또는 게시 단계 handover에서 수행한다.
4. Commit·push·PR·merge·Persistent UAT·Azure 공개배포는 별도 승인을 유지한다.
