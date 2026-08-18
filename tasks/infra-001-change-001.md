# TASK-INFRA-001 Change 001 — 운영 Entra 개발 검수 사용자 권한

## Task Identity Gate

- proposedTaskId: `TASK-INFRA-001`
- taskType: `POLICY_DECISION / APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: 후속 제품 기능
- roadmapNextGate: 사용자 지정 운영 검수 권한 보정
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-INFRA-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 지정된 실제 Entra 사용자만 공개 운영에서도 모든 기존 업무 화면의 입력·수정·저장을 검수할 수 있게 한다.
- 정책 경계: Dev 인증과 관리자 사용자 전환은 운영에서 계속 비활성화한다. 지정 사용자는 실제 Microsoft 365 인증을 통과한 활성 Entra 사용자여야 한다.
- 보안 경계: 실제 email은 tracked 파일에 기록하지 않고 Key Vault의 별도 allowlist secret으로만 주입한다. allowlist가 없거나 일치하지 않으면 현재 RBAC를 그대로 사용한다.
- 권한 범위: 기존 역할·permission의 합집합만 부여한다. ReviewSafe mutation 차단, 업무 상태 validation, 동시성, audit와 최종 관리자 보호는 우회하지 않는다.

## 승인 계약

- 사용자는 2026-08-18 정리된 5번 수정의 구현을 명시 승인했다.
- 실제 Key Vault 값 입력, Azure workload 교체와 공개배포는 별도 운영 승인 단계로 남긴다.
- 사용자는 자동검증 결과와 사용자 검수 미완료 상태를 보고받은 뒤 2026-08-18 실제 Key Vault 값 입력, 원격 `main` 병합과 Azure 공개배포를 명시 승인했다. 실제 이메일은 승인된 secret 입력에만 사용하고 tracked source에는 남기지 않는다.

## 구현 계약

1. `Authentication:DevelopmentOperatorEmails` allowlist에 포함된 활성 Entra 사용자만 개발 검수 운영자로 판정한다.
2. 해당 profile에는 현재 등록된 모든 역할과 permission을 요청 시점에 합성해 모든 기존 업무 policy를 통과시킨다.
3. 일반 사용자·일반 System Administrator·Dev persona에는 적용하지 않는다.
4. Frontend는 `/api/me`의 기존 역할·permission 응답을 사용하므로 별도 인증 우회 UI를 만들지 않는다.
5. Azure는 `development-operator-emails` Key Vault secret을 Backend identity에만 읽기 허용하고 Backend 환경변수로 연결한다.
   Container Apps 내부 secret reference는 20자 제한을 지키는 `development-ops`를 사용한다.
6. actual email, tenant·object ID와 secret 원문은 코드·문서·test artifact에 기록하지 않는다.

## 검증 계약

- 일치하는 활성 Entra 사용자의 전체 role·permission 합성
- 비일치·Dev·비활성·승인 대기 사용자의 권한 비확대
- 실제 endpoint 대표 mutation allow matrix와 일반 사용자 deny 유지
- Production에서 Dev auth·AdminUserSwitch 비활성 계약 유지
- Bicep·생성 ARM·secret-scope RBAC 동기화 검증

## Finding

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `INFRA-C001-OPERATOR-RBAC-GAP-001` | P1 | `RESOLVED` | 운영 System Administrator 역할만으로는 부서별 모든 업무 mutation permission이 없어 공개 운영 오류를 직접 재현·수정 검수할 수 없었다. | 별도 Key Vault allowlist와 실제 Entra profile의 기존 RBAC 합집합을 구현하고 비대상·claims·secret-scope 회귀를 추가했다. |
