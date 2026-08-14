# TASK-ADMIN-003 Change 002 — 부서장별 양식 관리 범위 정합화

## Task Identity Gate

- proposedTaskId: `TASK-ADMIN-003`
- taskType: `POLICY_DECISION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 관찰·별도 승인 제품 Task`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-ADMIN-003`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 부서장 지정과 실제 양식 관리 메뉴·수정 권한을 사용자가 확정한 부서별 운영 책임에 맞춘다.
- Root Finding 또는 정책 결정: 기존에는 품질 일반 사용자에게 구매품 구분 관리가 노출되고 제조 부서장에게 제조 양식 binding이 부여됐다. 반대로 생산관리 부서장은 생산계획 양식만 관리해 제조 양식과 생산계획 연결을 한 책임자가 함께 조정할 수 없었다.
- 변경·검증 경계: 기존 양식관리 binding 정책, LQC 운영 상태 권한, 구매품 구분 관리 권한, 생산관리 양식의 유효 domain, 양식 관리 메뉴·catalog 표시와 기존 binding 데이터 보정만 포함한다.
- 보존할 불변조건: System Administrator 전체 관리, 한 부서 복수 부서장, 부서 이동·부서장 해제 즉시 권한 회수, 기존 양식·프로젝트 snapshot 불변, 서버 권한 강제, audit·동시성 계약 유지.
- 예상 산출물: 권한 코드, additive migration, 부서별 Frontend 표시, 회귀 테스트, implementation report·Roadmap 갱신.

## 사용자 승인 계약

- System Administrator의 현재 전체 양식 관리 상태를 유지한다.
- 품질 부서장은 IQC·LQC·OQC 양식, 구매품별 IQC 설정과 구매품 구분을 표시·수정하고 LQC 운영 상태도 변경한다.
- 품질 부서장에게 Item별 제조 양식과 생산계획·실적 연결은 표시하지 않는다.
- 제조 부서장과 일반 품질 사용자에게 양식 관리 메뉴를 표시하지 않고 양식 관리 mutation도 허용하지 않는다.
- 생산관리 부서장은 생산계획·실적 연결과 Item별 제조 양식을 표시·수정한다.
- 기타 부서장은 양식 관리 메뉴와 권한을 가지지 않는다.
- 기존 기능 범위의 권한 정책 변경이므로 Fable 신규 기획은 적용하지 않는다.
- 구현·자동 검증과 사용자 검수를 완료했다. 2026-08-14 사용자가 원격 `main` 병합과 Azure 공개배포를 명시 승인했다.
- PR #103 필수 CI를 통과해 exact main SHA `58c089993587deea30513cb6edee0b8396a1d474`에 병합했고 Azure release `31786040822`에서 migration `0078`→Backend→Frontend와 공개 보안 검사를 완료했다.
