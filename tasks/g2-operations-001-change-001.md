# TASK-G2-OPERATIONS-001 Change 001 — 기획·검토 승인과 구현 시작

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- changeStatus: `APPROVED_FOR_IMPLEMENTATION`
- userInstruction: `승인. 구현 진행해.`
- userInstructionDate: 2026-08-18
- planningSource: [g2-operations-001-codex-planning.md](g2-operations-001-codex-planning.md)
- reviewSource: [g2-operations-001-codex-review.md](g2-operations-001-codex-review.md)
- planningApproved: true
- reviewResolutionApproved: true
- implementationApproved: true
- openBlockingDecisionCount: 0
- roadmapSequenceMatch: true
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- reuseExistingTask: true
- gateStatus: `PASS_REUSE`
- instructionChainRead: true
- implementationBaselineBeforeRefresh: `28991aecbeaeeeff6e636f002761825b666d7a5e`
- latestOriginMainObserved: `d8c60ffe1317907eb5543ad785abf10b058e64e9`
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false

## 1. 승인 범위

사용자는 Codex 기획안과 검토 resolution을 승인하고 구현 진행을 명시했다. 구현은 다음 범위로 제한한다.

- G2 부모 메뉴와 홈·생산/출하 관리·제조 인원 출근 관리 세 화면
- G2 독립 일별 생산·납품·출근 data와 적용일별 목표·재고 실사 schema
- 자동 재고 계산과 실사 checkpoint 경계
- 모든 활성 사용자 조회와 제조·영업·물류·System Administrator의 확정 field 권한
- metric별 동시성, mixed permission transaction, ReviewSafe 차단
- desktop·390px 접근 가능한 그래프·표·입력·오류 feedback
- Backend·Frontend·migration·권한·동시성·격리 full-stack 검증
- Task 종료 산출물과 사용자 검수 checklist

## 2. 제외 범위

- 영업팀 전용 손익관리와 navigation placeholder
- 기존 프로젝트·생산계획·제조·물류·근태 data 연동
- 개인별 출근 기록, 예상 대비 실적, 납품목표
- Excel·PDF·첨부·알림
- Persistent UAT migration·runtime handover와 운영 data mutation
- local commit, push, PR, merge, branch/worktree cleanup와 Azure 공개배포

## 3. 구현 시작 Reuse Gate

- purpose identity는 기존 `TASK-G2-OPERATIONS-001`과 1건으로 확정했다.
- 같은 목적의 추가 local/remote branch와 PR은 0건이다.
- Roadmap의 명시적 병렬 진행 승인과 사용자의 이번 구현 승인이 일치한다.
- 현재 G2 worktree는 Task 기획 문서가 미커밋 상태이고 기준 branch가 최신 main보다 뒤이므로 문서를 이름 있는 Task 전용 보존점으로 보호한 뒤 같은 branch를 최신 `origin/main`에 재기준선화한다.
- 최신 main 재기준선화 뒤 Root·Backend·Frontend·Scripts instruction file이 달라지면 구현 전에 instruction chain을 다시 읽는다.
- 최신 main의 현재 마지막 migration은 `0080_item_manufacturing_snapshot_backfill.sql`이며 G2는 재기준선화 후 중복 여부를 확인해 다음 additive 번호를 사용한다.

## 4. Changed-file allowlist

- `database/migrations/<next>_g2_operations.sql`
- `backend/src/Emi.Qms.Api/G2/**`
- `backend/src/Emi.Qms.Api/Authorization/QmsPolicies.cs`
- `backend/src/Emi.Qms.Api/Authorization/AuthorizationServiceCollectionExtensions.cs`
- `backend/src/Emi.Qms.Api/Identity/QmsPermissions.cs`
- `backend/src/Emi.Qms.Api/Identity/SeedIdentityData.cs`
- `backend/src/Emi.Qms.Api/Program.cs`
- `backend/tests/Emi.Qms.Api.Tests/*G2*`
- G2 권한·migration 회귀에 필요한 기존 backend test 파일의 최소 변경
- `frontend/src/App.tsx`
- `frontend/src/api.ts`
- `frontend/src/styles.css`
- `frontend/src/g2*`, `frontend/src/G2*`
- `frontend/tests/*G2*`, navigation 회귀에 필요한 기존 Frontend test 파일의 최소 변경
- `frontend/e2e/full-stack/g2-operations.full-stack.spec.ts`
- `docs/00-product-roadmap.md`
- `tasks/g2-operations-001-*`

allowlist 밖 개선, dependency·lockfile·environment·certificate·공통 runtime 설정 변경은 수행하지 않는다.

## 5. 검증 계약

- Backend Release build와 G2·권한·migration 관련 tests
- Frontend lint·typecheck·unit·build
- 기존 migration diff 0, fresh/existing isolated DB apply, catalog·번호 검사
- 역할별 allow/deny와 서버 403
- 같은 metric 경쟁·다른 metric 병렬 저장·mixed permission atomic rollback
- 실사 경계·빈 값/0·미래 예상·목표 적용일 계산
- desktop·390px loading/empty/error/success·keyboard·overflow·console/request smoke
- isolated Full-Stack E2E
- privacy-safe evidence, secret/PII, generated artifact와 allowlist 검사

## 6. 다음 Gate

1. 최신 `origin/main` 재기준선화와 source 재대조
2. 승인 범위 구현
3. 구현 세션과 논리적으로 분리한 read-only Codex 독립 검증
4. Implementation report·SOP·User manual·user validation checklist·Roadmap 동기화
5. 사용자 직접 검수
6. 별도 Git 게시·Persistent UAT·공개배포 승인
