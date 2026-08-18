# TASK-QUALITY-OPERATING-MODEL-001 Change 007 — 기존 프로젝트 구매품 구분 입력 허용

## Task Identity Gate

- proposedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- taskType: `POLICY_DECISION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: 후속 제품 기능
- roadmapNextGate: 사용자 지정 오류 수정
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 기능 도입 전에 생성된 `AllReceipts` 프로젝트에서도 구매정보의 구매품 구분을 선택·저장할 수 있게 한다.
- Root Finding 또는 정책 결정: 구매품 구분 metadata와 IQC routing 정책을 같은 조건으로 묶어, 기존 프로젝트에서는 Frontend가 catalog를 불러오지 않고 선택창도 비활성화했다.
- 변경·검증 경계: 구매 직접 입력과 Excel 입력의 구분 metadata, 관련 Frontend 표시와 Backend validation만 변경한다. 프로젝트의 IQC routing 정책·기존 도착분·검사 이력은 변경하지 않는다.
- 보존할 불변조건: `AllReceipts`는 구분을 저장해도 모든 도착분이 기존 전역 상세 IQC로 이동한다. `CategoryBased`는 구분 필수와 저장 시점 IQC snapshot을 유지한다. 도착 이력이 생긴 뒤 구분 변경은 두 정책 모두 차단한다.
- 예상 산출물: 모든 프로젝트의 활성 구분 선택, 기존 프로젝트에서는 선택을 선택사항으로 유지, 직접 입력·Excel·도착 routing 회귀.

## 승인 계약

- 사용자는 2026-08-18 조사 결과에서 정리한 2번 수정의 권장안을 명시 승인했다.
- 과거 구매품·도착분·IQC attempt와 확정 성적서를 소급 변경하지 않는다.
- 운영 DB·runtime, 원격 게시·병합·배포는 이번 구현 범위에 포함하지 않는다.
- 사용자는 자동검증 결과와 사용자 검수 미완료 상태를 보고받은 뒤 2026-08-18 Change 007을 생산계획·운영 검수 권한 변경과 함께 원격 `main` 병합·Azure 공개배포하도록 명시 승인했다. 사용자 운영 검수는 배포 후 Gate로 남긴다.

## 구현 방향

1. 구매정보 수정 화면은 프로젝트 IQC 정책과 무관하게 활성 구매품 구분 catalog를 불러온다.
2. 구분 선택창은 모든 프로젝트에서 활성화한다. `CategoryBased`만 필수이고 `AllReceipts`는 기존 빈 값을 허용한다.
3. Backend는 `AllReceipts`에도 사용자가 선택한 활성 구분의 ID·코드·표시명을 저장한다. 함께 저장되는 IQC 설정 snapshot은 metadata이지만 도착 routing은 프로젝트 정책을 우선해 계속 기존 상세 IQC를 사용한다.
4. 직접 입력과 Excel 모두 같은 구분 선택·보존·도착 이력 이후 변경 금지 규칙을 사용한다.
5. migration은 추가하지 않는다. 기존 nullable 구분 snapshot 열을 사용한다.

## 검증 계약

- `AllReceipts` 기존 프로젝트에서 신규 구매품 구분 선택·저장·재조회 성공
- 같은 프로젝트의 도착 등록은 선택한 구분 설정과 관계없이 기존 `Detailed` IQC 유지
- 도착 이력 이후 다른 구분 변경은 400 validation 또는 conflict로 차단
- 구분이 없는 기존 행의 일반 수정은 계속 허용
- `CategoryBased` 구분 필수·snapshot 비소급 회귀 유지
- PC·390px 구매 수정 화면에서 구분 선택 가능

## Finding

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `QOM-C007-LEGACY-CATEGORY-LOCK-001` | P1 | `RESOLVED` | 기존 프로젝트는 구매품 구분 catalog를 로드하지 않고 select를 비활성화해 구매정보 분류를 입력할 수 없었다. | 모든 프로젝트에 활성 구분 catalog와 선택창을 제공하고, 기존 프로젝트에서는 선택사항으로 저장하도록 metadata와 IQC routing 조건을 분리했다. 직접 입력·Excel 저장·기존 상세 IQC routing·도착 후 변경 차단 회귀를 통과했다. |
