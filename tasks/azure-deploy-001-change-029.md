# TASK-AZURE-DEPLOY-001 Change 029 — 최신 main 전체 통합 공개 release

## 상태

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- roadmapSequenceMatch: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`
- sourceTasks: `TASK-SITE-ACCESS-001`, `TASK-G2-OPERATIONS-002 Change 003`
- productionDeploymentApproved: `true`
- approvalSource: `USER_EXPLICIT_2026-09-02_FULL_CURRENT_MAIN`
- sourceSha: `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`
- azureReleaseRun: `33577473523`
- status: `PUBLIC_RELEASE_COMPLETE`

## 승인과 실행 배경

Change 028의 최초 실행 `33576014266`은 exact source를 검증한 뒤 배포 범위가 G2 Backend·Frontend뿐 아니라 이미 `main`에 병합된 사이트 접속 migration `0085`까지 포함한다는 사실을 확인했다. 당시 사이트 접속의 Azure 승인이 없었으므로 Environment 승인 전에 취소했고, image·DB·revision·public traffic mutation은 없었다.

사용자는 이어서 현재 `main` 전체 공개배포를 명시 승인했다. 이 승인으로 PR #116의 사이트 접속 이력과 migration `0085`, PR #117의 G2 전일 실적 재고 변경을 exact current-main 한 묶음으로 배포했다.

## 배포 기준과 범위

- 사이트 접속: PR #116, merge SHA `a8bb000dbbbe7d307bf1de96259917b750460497`, main CI `33493357909`
- G2 재고: PR #117, merge SHA `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`, PR CI `33573894506`, main CI `33575957041`
- 실제 운영 source: `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`
- 적용 순서: additive migration `0085_site_access_sessions.sql` → Backend → Frontend → public security smoke
- 보존: 기존 Entra 인증, Front Door 익명 차단, Web Push·Teams·메일 활성 설정, Key Vault 참조, 기존 업무 데이터와 G2 원본 데이터
- 제외: local Persistent UAT, 실제 외부 알림 시험 발송, 과거 사이트 접속 소급, 운영 G2 원본 수정

## 실행 결과

Azure release `33577473523`은 source·확인값·배포 artifact 검증, Backend·Frontend image 병렬 게시, migration과 두 application revision 교체를 순서대로 통과했다.

| 항목 | 결과 |
| --- | --- |
| source validation | `PASS` |
| Frontend image | `PASS` |
| Backend image | `PASS` |
| migration `0085` | `PASS` |
| Backend revision | `PASS` |
| Frontend revision | `PASS` |
| workflow public security | `PASS` |
| 별도 health | `200` |
| 별도 익명 root·`/api/me` | `401/401` |
| 인증된 G2 2026-08-28 재고 | `6대` |
| 사이트 접속 coverage·summary | 표시됨 / 1건 이상 |

## Finding

### AZURE-CHANGE-029-SCOPE-001

- 등급: `P1`
- 상태: `RESOLVED`
- 원인·영향: Change 028의 문서상 범위는 migration 없음이었지만 실제 current-main에는 PR #116의 미배포 migration `0085`가 포함돼 있었다. 그대로 승인하면 미승인 기능을 운영 적용할 수 있었다.
- 해소: 최초 run을 Environment 승인 전에 취소해 운영 mutation을 0으로 유지하고, 사용자에게 전체 범위를 보고한 뒤 current-main 전체 배포 승인을 받아 새 run으로 실행했다.

### AZURE-CHANGE-029-PRIVACY-PROJECTION-001

- 등급: `P2`
- 상태: `RESOLVED`
- 원인·영향: 취소된 첫 run의 Environment Gate를 확인하는 과정에서 GitHub actor metadata가 포함된 raw DOM snapshot이 transient tool output에 한 차례 노출됐다. credential·secret과 tracked/staged/PR artifact 유입은 0이다.
- 해소: 해당 snapshot을 증빙에서 폐기하고, 최종 성공 run은 GitHub API·CLI의 fixed boolean/count/status projection만으로 승인·검증했다. 제품·운영 문서에는 원문을 기록하지 않았다.

## Rollback·forward-fix

- migration `0085`는 additive 감사 원장이므로 schema·row를 삭제하지 않는다.
- application 장애는 직전 검증 image로 rollback하거나 current-main forward-fix를 배포한다.
- 사이트 접속 DB 함수·guard·권한 문제는 다음 번호의 additive migration으로 수정한다.
- G2 rollback 시 재고 표시가 종전 당일 마감 기준으로 돌아가므로 운영 판단을 중지하고 Change 003 image 복구 또는 forward-fix를 우선한다.

## 현재 Gate

- Open P0/P1/P2: `0/0/0`
- Git main 병합: PR #116·#117 완료
- Azure 공개배포: 완료
- G2 공개 read-only 확인: 완료
- 사이트 접속 자동 공개 확인: 완료
- 사용자 사이트 접속 화면·Excel 직접 검수: 대기
- Persistent UAT: 미적용

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | release 결과 동기화 | `tasks/azure-deploy-001-implementation-report.md` |
| SOP | exact current-main·release 기준 동기화 | `tasks/azure-deploy-001-sop.md` |
| User manual | 기존 승인형 GitHub release 절차 재사용, 사용자 동선 변경 없음 | `infrastructure/azure-pilot/README.md` |
| Roadmap update | 3.3M·6.2·6.5·추적 97·98·Decision Log 동기화 | `docs/00-product-roadmap.md` |
| User validation checklist | 자동 공개 확인과 사용자 잔여 검수 분리 | `tasks/azure-deploy-001-user-validation-checklist.md` |
