# TASK-AZURE-DEPLOY-001 Change 028 — G2 전일 실적 기반 재고 공개 release

## 상태

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- sourceTask: `TASK-G2-OPERATIONS-002 Change 003`
- 기준 원격 `main`: `a8bb000dbbbe7d307bf1de96259917b750460497`
- 사용자 승인: 2026-09-02 수정·원격 main 병합·Azure 공개배포 명시 승인
- mainMergeApprovalCount: `1`
- productionDeploymentApproved: `true`
- 상태: `SUPERSEDED_BY_CHANGE_029 / PUBLIC_RELEASE_COMPLETE`

Change 028의 G2 게시 대상은 PR #117로 main에 병합됐다. 실제 current-main에 사이트 접속 migration `0085`가 함께 포함돼 있어 최초 release를 운영 mutation 전에 취소했고, 사용자의 전체 current-main 배포 승인과 실제 결과는 [Change 029](azure-deploy-001-change-029.md)가 canonical source다.

## 게시 대상

1. 2026-08-28부터 표시 날짜의 재고에 전일 생산·납품·불량을 반영하는 Backend 계산
2. 월 첫날·부분 범위 조회에서 전일 metric과 마지막 실사 경계를 정확히 재구성하는 조회
3. 홈 저장 없는 임시 입력이 입력 날짜의 다음 날 재고부터 반영되는 Frontend 계산
4. Backend·Frontend·실제 PostgreSQL·격리 Full-Stack 회귀와 Task 산출물

## 제외·보존 경계

- migration·schema와 운영 G2 원본 생산·납품·불량·실사 데이터는 변경하지 않는다.
- 2026-08-27까지의 기존 재고 계산은 유지한다.
- 기존 인증, Front Door 익명 차단, 외부 알림과 Key Vault 참조를 보존한다.
- Persistent UAT는 적용하지 않는다.
- 실사 날짜 재고가 우선이며 그 날짜의 실적은 다음 날짜 재고에 반영한다.

## 게시·배포 순서

1. Change 003의 독립 검증과 Open P0/P1/P2 `0/0/0`을 확인한다.
2. 승인 파일만 commit·push하고 Ready PR 필수 CI를 통과한다.
3. 승인된 PR을 원격 `main`에 병합하고 exact 40자 main SHA를 확인한다.
4. `Azure Pilot Release (Manual)`을 exact main SHA와 image push·production deploy 두 확인값으로 실행한다.
5. 변경 분류에서 database migration 제외, Backend·Frontend 포함을 확인한다.
6. 공개 health·익명 접근 차단과 인증된 G2 2026-08-28 재고 `6대`를 read-only로 확인한다.
7. 실제 PR·main·Azure run·공개 결과를 Implementation report, SOP와 Roadmap에 후속 동기화한다.

## Rollback

Migration과 데이터 변경이 없으므로 장애 시 직전 검증 Backend·Frontend image로 함께 복구한다. rollback 뒤에는 재고가 다시 당일 마감 기준으로 표시되므로 운영 판단을 중지하고 Change 003 image 복구 또는 additive forward-fix를 우선한다.
