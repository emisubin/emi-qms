# TASK-ADMIN-001 Change 001

- userValidationStatus: `COMPLETE`
- publicationApproved: `true`
- approvalSource: `USER_EXPLICIT_ALL_VALIDATED_MAIN_MERGE_AND_PUBLIC_DEPLOYMENT`

## Task Identity Gate

- proposedTaskId: `TASK-ADMIN-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-PROJECT-PENDING-001`
- roadmapNextGate: `PRIORITY_3_IMPLEMENTATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-ADMIN-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 관리자 홈을 실제 조치가 필요한 항목 중심으로 정리하고 승인 대기 사용자만 바로 확인할 수 있게 한다.
- Root Finding 또는 정책 결정: 승인 대기 수는 정확했지만 카드가 전체 사용자 목록으로 이동해 대상을 식별하기 어려웠고, 완료 발송·일일 요약·최근 기준정보 변경 KPI는 현재 관리자 홈에서 조치 가치가 낮았다.
- 변경·검증 경계: 관리자 홈 응답·화면, 승인 대기 사용자 조회 필터, 관련 Backend·Frontend 테스트와 사용자 검수 문서만 변경한다.
- 보존할 불변조건: 일반 사용자 관리 목록은 전체 사용자를 유지한다. 알림 발송 완료 기록, 일일 요약 기능, 기준정보 변경 이력 메뉴·데이터는 삭제하지 않는다. 기존 관리자 권한 정책을 유지한다.
- 예상 산출물: 관리자 홈 KPI 정리, `/admin/users?filter=approval-pending`, 자동 검증, 사용자 검수 체크리스트, 구현 보고서와 Roadmap 상태 동기화.

## 승인된 변경 범위

1. 관리자 홈에서 `발송 완료`, `마지막 일일 요약`, `최근 기준정보 변경` KPI 카드를 제거한다.
2. 제거된 카드의 집계값은 관리자 홈 API 응답에서도 제외한다.
3. `승인 대기 사용자` 카드의 버튼은 `/admin/users?filter=approval-pending`으로 이동한다.
4. 승인 대기 목록에는 활성 Microsoft Entra 사용자 중 역할이 하나도 없는 사용자만 표시한다.
5. 승인 대기 사용자가 없으면 `현재 승인 대기 중인 사용자가 없습니다.`를 표시한다.
6. 일반 관리자 메뉴에서 사용자 관리를 열면 기존처럼 모든 사용자를 표시한다.

## 제외 범위

- 알림 발송 완료 기록이나 발송 상태 상세 화면 삭제
- Daily Digest 기능·worker·발송 기록 삭제
- 기준정보 변경 이력 메뉴·API·데이터 삭제
- 승인·부서·역할 정책 변경
- DB schema와 migration 변경

## 검증 계획

- Backend Release build와 관리자 API 집중 테스트
- Frontend lint, typecheck, 전체 unit test와 production build
- 승인 대기 카드 이동·목록 축소·제거 KPI 부재 화면 검증
- 일반 사용자 관리 전체 목록 회귀 확인
- privacy-safe diff와 Finding gate 확인

## 산출물

- [Implementation report](admin-001-change-001-implementation-report.md)
- [사용자 검수 체크리스트](admin-001-change-001-user-validation-checklist.md)
- [Product Roadmap](../docs/00-product-roadmap.md)
