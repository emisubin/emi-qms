# TASK-AUDIT-001 Change 002 — 감사 acceptance 계약 완화 승인

- taskType: `POLICY_DECISION`
- changeStatus: `APPROVED_FOR_CONTRACT_UPDATE_AND_LOCAL_COMMIT`
- instructionChainRead: true
- userInstructionDate: 2026-08-28
- canonicalTaskId: `TASK-AUDIT-001`
- approvalSource: `USER_EXPLICIT`
- userApprovalExact: `승인`
- approvedQuestion: `정확한 전체 경로·관계 목록과 중앙 transaction 대표 검증을 완료 기준으로 인정하고, 모든 409를 Conflict로 기록하는 완화안을 승인한 뒤 독립 검증과 local commit까지 진행할지`
- planningSource: `tasks/audit-001-planning.md`
- priorChange: `tasks/audit-001-change-001.md`
- coverageRegistry: `tasks/audit-001-coverage-registry.md`
- implementationApproved: true
- acceptanceContractRelaxationApproved: true
- independentReverificationApproved: true
- commitApproved: true
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false
- branch: `feat/task-audit-001-access-change-audit`
- baseSha: `7371d9e7224c3786f9b0efe3b2b88dfe9b88cd50`

## 1. 승인된 P1 acceptance 계약

- 인증 mutation endpoint 전체 `185`개를 runtime endpoint catalog와 exact method+route registry로 대조하고, 포함 `156`·명시적 제외 `29`로 빠짐없이 분류한다.
- schema relation 전체 `145`개를 추적 `94`·명시적 제외 `51`로 빠짐없이 분류한다.
- 신규·삭제 endpoint 또는 relation으로 missing·stale 항목이 생기면 startup 또는 contract test를 fail-closed로 실패시킨다.
- 중앙 request context·middleware·PostgreSQL trigger의 성공 commit, accepted no-op, rollback, audit append 실패 rollback, append-only, privacy projection, runtime 최소 권한을 실제 PostgreSQL 대표 fixture로 검증한다.
- local 동시 mutation 50건에서 업무 row·성공 parent·field child 연결이 각각 50건인지 검증한다.
- 위 exact catalog와 중앙 transaction 대표 검증을 v1 acceptance 완료 기준으로 확정한다. 포함 route 156개 각각에 성공·accepted no-op·rollback을 반복 실행하는 1:1 matrix는 요구하지 않는다.
- 이 계약은 모든 route의 개별 업무 규칙을 실행 증명했다고 주장하지 않는다. 실제 route별 동작은 기존 endpoint/store 회귀와 향후 변경 PR의 해당 기능 테스트가 계속 책임진다.

## 2. 승인된 P2 실패 분류 계약

- `400/422`는 `Validation`, `409/412`는 `Conflict`로 기록한다.
- v1에서는 `Duplicate`를 독립적으로 확정하거나 표시하지 않는다. 중복으로 발생한 409도 `Conflict`로 보수 분류한다.
- endpoint 이름·action 이름·문자열 문구로 `Duplicate`를 추정하지 않는다.
- 알려지지 않은 route의 실패 분류는 fail-closed로 거절한다.
- 중복을 별도로 보여주는 server-owned typed signal은 현재 acceptance와 release blocker에서 제거한다. 이후 필요하면 기존 Task의 승인된 change 또는 별도 정책 결정으로만 추가한다.

## 3. 검증·게시 경계

- 기존 제품 범위는 넓히지 않는다. 독립 검증에서 승인된 acceptance와 실제 구현의 불일치를 발견하면 이를 해소하는 최소 source/test 보정과 계약·coverage registry·구현 보고서·Roadmap 동기화를 포함한다.
- 구현 세션과 분리된 Codex가 승인 완화 계약, 실제 code/test evidence, privacy guard와 Git diff를 read-only로 재검증한다.
- 독립 재검증이 PASS이고 Open P0/P1/P2가 `0/0/0`일 때만 현재 branch에 local commit을 만든다.
- Push·PR·main merge·Persistent UAT migration/runtime handover·Azure release는 이번 승인에 포함하지 않는다.
- 사용자 공개 운영 검수는 별도 게시·운영 적용 승인 뒤 수행한다.

## 4. 다음 Gate

계약 문서 동기화 → 독립 read-only 재검증 → local commit → 사용자에게 결과 보고 순서로 진행한다. 이후 Push·PR·main merge·Persistent UAT·Azure release는 별도 명시적 승인을 받는다.
