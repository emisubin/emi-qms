# TASK-OSAN-ACCESS-001 — 사업부 사용자 관리와 총괄 전환

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- status: `IMPLEMENTED_AWAITING_BATCHED_USER_VALIDATION`
- parentTask: `TASK-OSAN-PILOT-001`
- implementationApproved: true
- implementationApprovalSource: `USER_EXPLICIT_2026-09-06_NEXT_TASK_START`
- localBaselineApprovalSource: `USER_EXPLICIT_2026-09-06_APPROVED`
- implementationBranch: `feat/task-osan-access-001-membership-switching`
- implementationBaseline: `670b2ea`
- runtimeMutationApproved: false
- gitPublicationApproved: false
- 선행조건: 충족 — TASK-OSAN-ISOLATION-001 제품 구현·자동 검증·사용자 검수 완료, 미완료 Docker lifecycle 동적 검증은 TASK-OSAN-VALIDATION-001로 이관. 사용자가 2026-09-06 “다음작업 시작해”로 이 Task 구현을 승인했고, 이어서 Task 1 local 기준선 commit과 Task 2 branch/worktree 생성을 승인했다.

## 목적과 계약

관리자가 소속과 업무 권한을 나누어 관리하고, 지정 총괄만 안전하게 사업부를 전환하게 한다.

Canonical clone의 `tasks/osan-pilot-001-planning.md`, `tasks/osan-pilot-001-review.md`, `tasks/osan-pilot-001.md`를 승인된 업무 기획·독립 review resolution·상위 Task 순서의 source로 함께 따른다. 이 임시 제품 worktree에는 기존 canonical 문서 WIP를 복제하지 않는다. 이 파일은 별도 신규 인터뷰나 기획 재작성 요청이 아니다. 기획 문서화 승인은 실제 구현·runtime 실행 승인이 아니다.

## 포함 범위

- 총괄의 사용자 사업부 지정과 사업부 관리자의 자기 사업부 local 권한 관리 API·화면을 연결한다.
- 기존 관리자 자동 총괄 승격 없이 별도 명시 designation을 사용한다. 총괄 전환 권한과 업무 변경 권한은 구분한다.
- 신규·미소속·미승인 사용자는 업무 접근 없이 대기 안내를 받는다. 기존 부서와 사업부를 같은 값으로 재사용하지 않는다.
- 오산 shell에 진행 관리 명칭과 필요한 메뉴만 표시하고, G2·중단·펜딩 등 비활성 route/API 접근을 차단한다.
- 탭별 사업부 context, 변경 요청 완료 전 전환 방지, 이전 요청 취소/무효화와 account+business-unit cache reset을 구현한다.
- 프로젝트 조회 scope·생성자 접근 연결·추가 접근 부여 절차를 기존 권한 모델로 명시하며 생성 권한으로 진행 변경 권한을 자동 부여하지 않는다.

## 조사·변경 경계

Backend membership/authorization/admin endpoints, Frontend App/auth/api/menu/cache와 관리자 화면.

실행 시 최신 instruction chain·Task identity·Roadmap·branch/runtime 상태를 읽고 exact 파일 allowlist와 검증 명령을 고정한다. 기존 WIP가 남은 현 branch에서 제품 개발을 자동 시작하거나 사용자의 WIP를 정리하지 않는다.

[Change 001 구현 방향서](osan-access-001-change-001.md)가 현재 구현 계약과 Task 전용 branch 전환 전 상태를 기록한다.

## 완료 기준

- [x] 미승인·일반·업무 권한자·사업부 관리자·총괄의 list/detail/write allow/deny matrix.
- [x] 일반 사용자의 다른 사업부 header/URL 변조, 총괄의 local 업무 권한 없는 변경 거부.
- [x] 실제 synthetic 3개 DB와 한 browser run에서 청주·오산 독립 context, 느린 이전 응답, 전환 중 저장 잠금과 membership 회수를 검증한다.
- [x] App의 실제 Entra 로그아웃 action을 mocked MSAL로 실행해 저장 선택 삭제, outstanding read 취소·generation 무효화와 `logoutRedirect` 연결을 검증한다.
- [x] 실제 PostgreSQL에서 자기 소속 동시 변경과 두 총괄의 교차 대상 변경을 lock barrier로 겹쳐 실행하고 deadlock 없이 직렬화된 membership·감사를 검증한다.
- [x] Backend/frontend occupied port는 자원 생성 전 거부하고, bootstrap/migration 뒤 backend startup 실패 주입은 생성한 3 DB·6 role·Compose/process/temp file을 trap으로 정리한다.
- [x] 일반 미소속 사용자는 `/api/me` 결과로 승인 대기 상태를 먼저 확정하고 runtime·업무 API를 호출하지 않으며, 마지막 소속 회수 뒤에도 한 번만 context를 무효화하고 안정된 대기 화면에 머문다.
- [x] 청주 기존 메뉴·권한·개발용 테스트 사용자 전환과 총괄 기능 혼동 없음.

실제 Microsoft 365 provider redirect를 포함한 end-to-end logout은 이 Change의 실제 provider 금지 경계에 따른 `N/A` 항목이다. 전용 browser harness에서 실행한 것으로 표현하지 않는다.

## 다음 Task에 전달할 내용

사용자·사업부 bootstrap 입력 방법과 권한 matrix를 문서화하고 Task 3에 인증된 오산 생성/진행 actor 계약을 전달한다.

실제 구현 결과·SOP·사용자 안내·검수 checklist·Roadmap 상태는 이 Task의 구현 보고에서 추적한다. Fresh GPT-6 High 검증에서 확인된 implicit context, ReviewSafe no-write, legacy identity label, membership lock ordering, Entra logout wiring, combined three-DB browser/harness cleanup과 미소속 runtime remount loop Finding을 Change 001 안에서 보정했고, 최종 독립 검증은 제품 P0/P1/P2 `0/0/0`으로 `GO`를 판정했다. Privacy-safe desktop/mobile screenshot도 다시 생성해 직접 확인했다. 사용자는 2026-09-07 Task 2 사용자 검수를 오산 개발 마지막 일괄 검수로 미루고 Task 3 진행을 승인했다. Task 2 local commit은 같은 메시지에서 승인됐으며 push·PR·merge·운영 적용은 승인되지 않았다. 모든 품질·Git·운영 gate는 Root 지침을 따른다.
