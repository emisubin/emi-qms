# TASK-AZURE-DEPLOY-001 Change 027

## 상태

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- sourceTask: `TASK-G2-OPERATIONS-001 Change 001~020`
- 기준 원격 `main`: `ee54c3c377ac70e1a49cddda90afa593192cc25e`
- 사용자 승인: 2026-08-19 원격 `main` 병합과 Azure 공개배포 명시 승인
- CI 보정 승인: 2026-08-19 Frontend job 제한시간 `20분 → 35분` 명시 승인
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`
- 상태: `PUBLICATION_APPROVED / PR_CI_PENDING / AZURE_RELEASE_PENDING`

## 게시 대상

1. 독립 G2 홈·생산/출하 관리·제조 인원 출근 관리 화면
2. 역할별 G2 권한·API·metric별 동시성 제어
3. additive migration `0081_g2_operations.sql`
4. 예상값 날짜 도래 초기화 forward-fix `0082_g2_forecast_expiry.sql`
5. Change 003~020의 월간표·그래프·모바일·KPI·필수 수량·서울 날짜·조회 순서 보정
6. 관련 Backend·Frontend·Full-Stack 회귀와 Task 산출물

## 제외·보존 경계

- 관리자 입력·수정 이력과 영업팀 손익관리는 별도 `NEW_FEATURE`로 유지한다.
- 기존 프로젝트·생산계획·제조·물류·근태 원본과 연결하거나 소급 변경하지 않는다.
- 기존 운영 인증, Front Door 익명 차단, Web Push·Teams·메일 활성 상태와 Key Vault 참조를 보존한다.
- 사용자 직접 화면 검수는 대기 상태를 유지하며 완료로 기록하지 않는다.
- migration은 additive forward-fix로 적용하고, 장애 시 기존 schema를 제거하지 않은 채 직전 application image로 복구한다.

## 게시·배포 순서

1. 최신 원격 `main`의 Workflow Continuity Change 018과 Azure Change 026을 보존해 G2 branch를 통합한다.
2. Backend 전체, Frontend lint·typecheck·unit·build, isolated Full-Stack과 migration 검증을 통과한다.
3. 승인된 파일만 명시적으로 push하고 Ready PR의 필수 `CI Gate`를 통과한다.
4. PR을 squash merge하고 exact 40자 최신 `main` SHA를 확인한다.
5. `Azure Pilot Release (Manual)`을 exact latest `main` SHA와 두 confirmation으로 실행한다.
6. 변경 분류에서 migration·Backend·Frontend가 모두 대상인지 확인한다.
7. migration 성공 뒤 Backend·Frontend revision, 공개 health와 익명 root·API 차단을 확인한다.
8. 실제 결과를 Implementation report, SOP, Roadmap과 user validation checklist에 후속 동기화한다.

## Finding

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `G2-PUBLICATION-AUTH-PROJECTION-001` | P2 | `RESOLVED` | 게시 사전 확인에서 인증 성공 boolean보다 상세한 CLI metadata가 transient terminal output에 포함됐다. Repository·PR·운영에는 기록되지 않았고 credential 원문은 노출되지 않았다. | 해당 출력을 증빙에서 폐기하고 이후 GitHub 확인을 boolean·SHA·PR 번호·검사 상태 count로 제한해 Gate를 재실행했다. |
| `G2-PUBLICATION-CI-TIMEOUT-001` | P2 | `REMEDIATION_IN_PROGRESS` | PR Frontend job에서 lint·typecheck·unit·build는 통과했지만 Playwright browser 설치 중 20분 job timeout이 2회 발생해 E2E와 CI Gate가 완료되지 못했다. 제품 코드 실패는 확인되지 않았다. | 사용자 승인에 따라 Frontend job 제한시간만 35분으로 조정하고 같은 PR에서 전체 CI를 재검증한다. |

현재 Open P0/P1/P2는 `0/0/1`이다.

## 최신 원격 `main` 통합 검증

| 검증 | 결과 |
| --- | --- |
| Backend Release build | warning/error `0/0` |
| Backend 전체 격리 회귀 | `549/549 PASS` |
| Frontend lint·typecheck·unit·build | error `0`, `230/230 PASS`, build `PASS` |
| G2 isolated Full-Stack | `1/1 PASS` |
| Production migration image | fresh/existing 재적용 `PASS`, ledger `82 Exact` |
| Azure release 입력·rollback mock | `PASS` |
| Bicep compile·Portal template·static validation | `PASS/PASS/PASS` |
| 격리 DB·container·network와 임시 image cleanup | `PASS` |
| CI workflow 보정 정적 회귀 | `scripts/test-main-pr-ci.sh` `PASS` |
