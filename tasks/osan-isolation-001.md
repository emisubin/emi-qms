# TASK-OSAN-ISOLATION-001 — 사업부 소속·DB·백그라운드 작업 분리 기반

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- status: `USER_VALIDATION_COMPLETE_RUNTIME_VALIDATION_DEFERRED`
- independentVerificationStatus: `IMPLEMENTATION_AND_EVIDENCE_CLEARANCE`
- userValidationStatus: `COMPLETE`
- userValidationSource: `USER_EXPLICIT_APPROVAL_2026-09-06`
- userValidationSourceNote: 사용자가 2026-09-06 “승인.”으로 직전에 제시된 오산 Task 1 사용자 검수 항목을 명시적으로 승인했다.
- parentTask: `TASK-OSAN-PILOT-001`
- implementationApproved: true
- isolatedTestRuntimeApproved: true
- productionRuntimeMutationApproved: false
- gitPublicationApproved: false
- deferredRuntimeValidationTask: `TASK-OSAN-VALIDATION-001`
- 선행조건: 충족 — 사용자 승인으로 공통 소속 DB·로그인·DB 접근·자동 작업·로컬 구현·격리 테스트 범위 확정. [Change 001](osan-isolation-001-change-001.md), [잔여 테스트 안정성 마무리](osan-isolation-001-change-002.md)와 [패키징 검사 lifecycle 보정](osan-isolation-001-change-003.md)을 참조한다. 후속 Task의 업무 review resolution은 해당 Task로 유지한다.

## 목적과 계약

신뢰할 수 있는 소속 판별부터 모든 DB 접근까지 사업부 경계를 만들고 청주 기존 계약을 보존한다.

[승인된 업무 기획](osan-pilot-001-planning.md), [독립 review resolution](osan-pilot-001-review.md), [상위 Task·순서](osan-pilot-001.md)를 함께 따른다. 이 파일은 별도 신규 인터뷰나 기획 재작성 요청이 아니다. 기획 문서화 승인은 실제 구현·runtime 실행 승인이 아니다.

## 포함 범위

- 동일 PostgreSQL 서버의 청주·오산 업무 DB와 최소 공통 membership directory 모델, additive migration·기존 청주 membership 연결 절차를 정의한다.
- 인증 identity → 허용 사업부 → local 권한 profile 순서와 요청별 불변 context를 구현한다. profile을 읽을 DB를 판별하기 전에 기존 단일 DB 사용자 생성을 실행하지 않는다.
- DatabaseConnectionStringProvider와 모든 소비자의 request/background/health/migration/bootstrap 연결 경로를 명시적으로 분리한다. singleton에 전역 현재 사업부를 저장하지 않는다.
- 업무 DB별 runtime·migrator·bootstrap role/CONNECT/schema 권한, pooling·audit·lease·idempotency key의 사업부 경계를 검증한다.
- 알림 발송·에스컬레이션·삭제 worker가 사업부별로 실행되게 하며 오산 외부 발송은 enqueue/dispatch 양쪽에서 비활성화한다.
- 기존 첨부·다운로드·export·G2 등 공유 API의 데이터 scope도 함께 검증하고 오산에서 비활성인 capability의 직접 접근을 차단한다.

## 조사·변경 경계

Backend DatabaseConnectionStringProvider/Program/Authorization/Identity/DB consumers/Notifications/AdminDeletion, database migrations, 인프라 설정 계약과 격리 fixtures.

실행 시 최신 instruction chain·Task identity·Roadmap·branch/runtime 상태를 읽고 exact 파일 allowlist와 검증 명령을 고정한다. 기존 WIP가 남은 현 branch에서 제품 개발을 자동 시작하거나 사용자의 WIP를 정리하지 않는다.

## 완료 기준

- [x] 동일한 entity ID를 가진 두 업무 DB에서 조회·변경·다운로드·worker 대상 교차 0.
- [x] 소속 없음·권한 없음·알 수 없는 DB·directory 장애·schema mismatch에서 임의 DB fallback 0.
- [x] 같은 서버의 DB별 접속 계정으로 다른 업무 DB 접근 거부; 공용 CONNECT 기본값 포함.
- [x] 기존 청주 사용자 ID·업무 행·G2 보존, fresh 세 DB와 기존 청주 DB additive 경로 통과.
- [x] 병렬 요청·worker 한쪽 실패·취소 후 context 잔류 없음; 오산 외부 provider 호출 0.

위 항목은 승인된 synthetic 환경의 자동 검증 결과다. Backend 575/575, 집중 격리 2/2와 image migration 86개 반복 적용은 통과했다. Frontend 최초 248/250과 Full-Stack 최초 63/64 이력을 보존하며, Change 002 보정 뒤 Frontend 전체 252/252와 정확한 Full-Stack 시나리오 3/3이 통과했다. 사용자는 2026-09-06 별도의 직전 검수 항목을 명시적으로 승인했다. [구현 보고서](osan-isolation-001-implementation-report.md)의 사용자 검수 완료와 실제 결과·미실행 범위를 함께 읽는다. Change 003 Docker 동적 검증과 운영 검증 완료를 뜻하지 않는다.

## 다음 Task에 전달할 내용

소속 resolver·business-unit context·DB 연결/worker 계약과 compatibility 검사 결과를 Task 2에 전달한다. 운영 credential·실제 Azure DB는 만들지 않는다.

실제 구현 결과·SOP·사용자 안내·검수 checklist·Roadmap 상태는 [구현 보고서](osan-isolation-001-implementation-report.md)에서 추적한다. 제품 구현은 별도 feature worktree에 보존했으며 canonical clone에는 오산 상태 문서와 보고서만 동기화한다. Fresh GPT-6의 구현·최종 evidence 검토와 Change 002 독립 검토를 통과했고 사용자 검수는 `USER_EXPLICIT_APPROVAL_2026-09-06` 근거로 완료됐다. `OSAN-TEST-TIMING`은 해결됐다. Change 003의 패키징 검사 lifecycle 코드는 정적 검사와 fresh GPT-6 코드 검토를 통과했지만 실제 Docker 정상·실패·TERM cleanup 증거는 없다. 사용자의 추적 이관 지시에 따라 이 P2는 `TASK-OSAN-VALIDATION-001`로 넘기고 Task 2 선행 차단에서는 내렸다. 기존 검사 container와 local image의 legacy 자원 2개는 사용자가 직접 삭제할 예정이며 P3 `USER_MANUAL_ACTION_PLANNED`, 결과 대기 상태다. 다음 제품 Gate는 `TASK-OSAN-ACCESS-001` 구현 승인이다. 모든 Git·운영 gate는 Root 지침을 따른다.
