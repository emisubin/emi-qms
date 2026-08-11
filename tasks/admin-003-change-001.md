# TASK-ADMIN-003 Change 001 — 사용자 부서·역할·부서장 연결 보정

## Task Identity Gate

- proposedTaskId: `TASK-ADMIN-003`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 관찰·별도 승인 제품 Task`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-ADMIN-003`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 사용자 관리에서 표준 부서를 모두 선택하고, 부서를 선택하면 기본 역할을 자동 지정하며, 체크박스로 여러 부서장을 지정한다.
- Root Finding 또는 정책 결정: 운영 migration에는 표준 10개 중 3개 부서만 생성되고 개발 seed에만 10개가 있어 운영 목록이 불완전하다. 기존 양식관리에는 사용자·부서 단위 복수 관리자 binding이 있으나 사용자 관리와 연결되지 않았다.
- 변경·검증 경계: additive migration `0072`, 사용자 관리 API·화면, 기존 양식관리 binding 동기화, 관련 Backend·Frontend·migration 검증만 포함한다.
- 보존할 불변조건: 마지막 활성 System Administrator 보호, 복수 역할 지원, 한 부서 복수 부서장, 부서 이동 즉시 이전 양식 권한 차단, 기존 binding·audit 이력 보존, 기존 migration 불변.
- 예상 산출물: 구현 코드, additive migration, 자동 테스트, implementation report 안의 SOP·사용자 안내·검수 checklist, Roadmap 상태 갱신.

## 사용자 승인 계약

- 표준 부서 10개를 운영 DB schema에 보강하고 부서명을 한글로 통일한다.
- 사용자 관리에서 부서 선택 시 그 부서의 기본 역할을 자동 지정한다.
- 사용자별 `부서장` 체크박스를 제공하고 한 부서에 여러 명을 허용한다.
- 품질·제조·생산관리 부서장은 기존 양식관리 승인 권한을 함께 가진다.
- Fable 기획은 사용자의 명시 요청에 따라 적용하지 않는다.
- 구현·자동 검증은 승인됐다. push·PR·merge·Azure 운영 적용은 승인 범위 밖이다.
