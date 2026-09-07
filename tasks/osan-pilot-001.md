# TASK-OSAN-PILOT-001 — 오산 시범 운영 기획과 개발 로드맵

- taskType: `NEW_FEATURE`
- status: `TASK_3_IMPLEMENTED_AWAITING_BATCHED_USER_VALIDATION`
- planningApproved: true
- planningApprovalScope: `USER_APPROVED_CONVERSATION_PLAN`
- reviewResolutionApproved: false
- implementationApproved: false
- 작성일: 2026-09-06

## 목적과 문서

오산의 프로젝트 등록·7단계 진행 관리·전체 현황판을 청주와 분리된 업무 DB에서 시범 운영한다. 이 상위 Task는 기획·승인·의존성 조정만 소유한다. 제품 변경은 아래 하위 Task가 각각 소유하며 상위 Task에서 중복 구현하지 않는다.

- [확인된 요구사항과 승인 이력](osan-pilot-001-interview.md)
- [기획안](osan-pilot-001-planning.md)
- [독립 검토와 resolution](osan-pilot-001-review.md)
- [문서화 작업 보고·검수·산출물 추적](osan-pilot-001-implementation-report.md)
- [Task 1 구현·자동 검증 보고](osan-isolation-001-implementation-report.md)
- [Task 1 잔여 테스트 안정성 마무리](osan-isolation-001-change-002.md)
- [Task 1 패키징 검사 lifecycle 보정](osan-isolation-001-change-003.md)
- [제품 로드맵](../docs/00-product-roadmap.md)

## 승인 범위

최신 승인: 사용자는 2026-09-06 “승인.”으로 Task 1의 8개 오산 검수 항목을 확인했고, 이어 “다음작업 시작해”로 Task 2 구현을 승인했다. Task 1의 `userValidationStatus`는 `COMPLETE`, source는 `USER_EXPLICIT_APPROVAL_2026-09-06`이다. Task 2는 로컬 구현·자동 검증·독립 제품 품질 검토를 통과해 commit `2e29938f754f3d95444df2b341a921cfd1fca43f`로 고정했으며, 사용자는 화면 검수를 마지막 일괄 검수로 이관했다. 이어 2026-09-07 “task3 구현 승인. 시작해”와 “승인.”으로 Task 3 구현과 전용 branch/worktree 생성을 승인했다. 사용자는 앞으로 승인된 구현이 품질 Gate를 통과하면 별도 확인 없이 local commit하도록 지시했다. [Task 1 Change 001](osan-isolation-001-change-001.md), [Task 1 구현 보고](osan-isolation-001-implementation-report.md), [Task 2 구현 보고](osan-access-001-implementation-report.md), [Task 3 Change 001](osan-project-001-change-001.md)을 함께 따른다. 상위의 implementationApproved=false는 전체 오산 기능의 포괄 구현 승인이 없다는 뜻이다. Task 4 이후 구현, Docker·운영·push·PR·merge까지 승인한 것으로 소급하지 않는다.

사용자: “오케이 좋아. 지금한 오산 기획 승인. 문서화 하고 task 단위로 나눠서 개발 로드맵까지 작성해줘.”

직전 대화에서 DB 분리는 같은 PostgreSQL 서버 안에 청주·오산 업무 DB를 각각 만드는 것으로 재확인했다. 위 요청은 기존 작업과 병행하는 기획 문서화·Task 분해·Roadmap 등록의 명시적 실행 근거다. 제품 구현, runtime 변경, Azure 자원·DB 생성, 실제 provider, commit·push·PR·merge·정리 승인은 아니다. 기존 지침 수정 WIP를 보존하고 현재 branch에 문서만 추가한다.

승인된 대화 기획과 이번 문서의 구현 권장안을 구분한다. 독립 review resolution과 아직 구현되지 않은 기술 세부안은 사용자가 이미 검수한 결과로 표시하지 않는다.

## Task Identity Gate

아래 공통 projection과 Task별 표를 함께 적용한다. 이 gate는 이번 문서 등록에 한정하며 각 구현 시작 시 최신 상태에서 재실행한다.

- instructionChainRead: true
- instructionConflictCount: 0
- planningOwner: `GPT_6_ASTRA_HIGH`
- implementationOwner: `GPT_5_6_SOL_XHIGH`
- verificationOwner: `FRESH_GPT_6_ASTRA_HIGH`
- roadmapExpectedTaskId: `NONE` — 기존 큐에 오산 목적 항목 없음; 기존 Task의 검수·운영 관찰은 보존
- roadmapNextGate: `OSAN_PLANNING_REGISTRATION`
- roadmapSequenceMatch: false
- samePurposeMatchCount: 0
- reuseExistingTask: false
- explicitRoadmapOverrideApproved: true
- experimentStandingInstructionApplies: false
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`
- branch: `fix/task-gov-codex-002-instruction-clarity`
- HEAD / local origin/main: `574cea66f602eb65eb1d110801d331151731b0a6`
- 작업공간: 기존 canonical clone; branch 전환·worktree 생성 없음
- 적용 지침: Root AGENTS, 종료 정책, Validation Matrix, Privacy-safe Evidence. Backend·Frontend 지침은 구현 경계 조사에 참조. 이번 write 경로에 하위 AGENTS 없음. scripts 변경 없음. experiment 원장 gate 비적용.
- 검색: tasks·Roadmap/Decision Log의 사업부 DB 분리·오산 진행 관리 목적, local/remote refs·worktree, open/closed/merged PR 최대 300개 필터에서 동일 목적 후보 0. 단순 회사 footer·기존 제조·G2는 목적이 다르다.

| proposedTaskId = canonicalTaskId | 유형 | 고유 목적 / 변경 경계 |
| --- | --- | --- |
| TASK-OSAN-PILOT-001 | NEW_FEATURE | 승인된 기획·Task 분해·문서 검토만; 제품 구현 없음 |
| TASK-OSAN-ISOLATION-001 | APPROVED_FEATURE_IMPLEMENTATION | 사업부 소속 판별과 DB·worker·migration 분리 기반 |
| TASK-OSAN-ACCESS-001 | APPROVED_FEATURE_IMPLEMENTATION | 소속·권한 관리 및 총괄 사업부 전환 화면 |
| TASK-OSAN-PROJECT-001 | APPROVED_FEATURE_IMPLEMENTATION | 오산 8개 입력·수량별 대상·공통 양식 생성 |
| TASK-OSAN-PROGRESS-001 | APPROVED_FEATURE_IMPLEMENTATION | 오산 진행 기록·일괄 처리·포장 시 자동 완료 |
| TASK-OSAN-DASHBOARD-001 | APPROVED_FEATURE_IMPLEMENTATION | 오산 전체 프로젝트 집계·검색·상세 연결 |
| TASK-OSAN-VALIDATION-001 | UAT_RUNTIME | 격리 환경 통합 검증·복구 rehearsal·사용자 검수 준비 |
| TASK-AZURE-DEPLOY-001 (재사용) | UAT_RUNTIME | 승인된 기존 Azure 서버의 오산 개통·운영 확인 |

마지막 배포 행은 공통 신규 생성 projection의 예외다. 동일 배포 목적 1건을 확인했으므로 samePurposeMatchCount=1, reuseExistingTask=true, gateStatus=PASS_REUSE이며, 이번에는 인계 문서만 작성한다. 실행 시 최신 다음 change 번호와 실제 운영 승인을 확인한다. 다른 하위 Task의 등록은 PASS_CREATE이고 구현 시작 승인은 아니다.

보존할 불변조건: 청주 데이터·권한·18단계 업무·G2 보존, 사업부 간 데이터 접근 차단, 오산 중단·펜딩 없음, 실제 사용자 검수와 자동 검증 구분, Git·운영 승인 보존.

## 개발 순서

| 순서 | Task | 선행조건 | 완료 산출물 | 현재 상태 |
| ---: | --- | --- | --- | --- |
| 1 | [데이터 분리 기반](osan-isolation-001.md) | 충족 — Change 001의 사용자 승인 | 신뢰할 소속 판별·DB 라우팅·worker 분리·격리 테스트 | USER_VALIDATION_COMPLETE_RUNTIME_VALIDATION_DEFERRED |
| 2 | [사용자·사업부 전환](osan-access-001.md) | 1 | 소속/권한 관리·전환·상태 초기화·권한 테스트 | IMPLEMENTED_AWAITING_BATCHED_USER_VALIDATION |
| 3 | [프로젝트 등록](osan-project-001.md) | 1, 2 | 8개 입력·공통 양식·수량별 대상·생성 테스트 | IMPLEMENTED_AWAITING_BATCHED_USER_VALIDATION |
| 4 | [진행 관리](osan-progress-001.md) | 3 | 개별/일괄 단계 기록·자동 완료·동시성 테스트 | PLANNED |
| 5 | [전체 현황판](osan-dashboard-001.md) | 4 | 집계 API·대시보드·모바일 검증 | PLANNED |
| 6 | [통합 검증](osan-validation-001.md) | 1~5 | 격리 Full-Stack·청주 회귀·복구 절차·사용자 checklist | PLANNED |
| 7 | [운영 적용 인계](osan-pilot-001-rollout-handoff.md) — TASK-AZURE-DEPLOY-001 재사용 | 6·사용자 검수·해당 Git/운영 승인 | 개통 결과·운영 점검·rollback 확인 | PLANNED_HANDOFF |

순차 개발을 기본으로 한다. 선행 Task는 자기 경계의 테스트까지 완료하며 Task 6으로 테스트를 전부 미루지 않는다. Task 1은 제품 구현·최종 evidence 독립 검토, UI 테스트 안정성 보정과 사용자 검수를 통과했다. Change 003의 패키징 검사 lifecycle 동적 검증은 이 실행환경에서 반복 차단됐고 사용자가 추적 항목으로 넘기도록 정정했다. 실제 증거가 없다는 P2는 `TASK-OSAN-VALIDATION-001`과 Azure·Persistent UAT 개통 전 Gate에 유지한다. Task 2는 로컬 구현·자동 검증과 fresh GPT-6 High 제품 품질 검토를 통과해 commit `2e29938`로 고정했고 사용자 화면 검수는 마지막 일괄 검수로 이관했다. Task 3은 Backend 582/582, Frontend 291/291, mock browser 1/1과 실제 3개 DB full-stack 1/1을 통과했고 parent review P2 네 건과 fresh verifier P2 세 건을 모두 보정했다. Fresh GPT-6 High 독립 재검증과 document-only 재확인은 open P0/P1/P2/P3 `0/0/0/0`, `GO`로 끝났다. 허용 범위 local commit을 자동 수행하며 다음 제품 Gate는 Task 4 구현 승인이다. 기존 legacy 검사 자원 2개 정리는 사용자가 직접 수행할 예정인 P3 `USER_MANUAL_ACTION_PLANNED`, 결과 대기 상태이며 다음 제품 Task의 선행조건이 아니다. push·PR·merge·Persistent UAT·provider·운영 적용은 별도 승인이다.

## 상태 갱신 원칙

독립 검토는 완료했고 내용 명확화 P2 2건을 프로젝트/진행 Task에 반영했다. 오입력 안내 P3 1건은 통합 검증·개통 준비로 연결했다. 상세 원문과 최신 상태는 review 문서를 따른다. Task 1의 기술 권장안·구현과 사용자 검수는 승인됐으며, 후속 업무 review resolution과 제품 구현 승인은 별도다.

하위 Task는 PLANNED → IMPLEMENTATION_APPROVED → IN_PROGRESS → AUTOMATED_VALIDATION_COMPLETE → USER_VALIDATION_PENDING/COMPLETE를 실제 근거로 갱신한다. Git 게시·운영 적용은 별도 필드다. 순서표의 등록이나 기획 승인만으로 구현·배포 완료 상태를 부여하지 않는다.
