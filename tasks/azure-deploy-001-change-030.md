# TASK-AZURE-DEPLOY-001 Change 030 — G2 당일 납품 재고 수식 공개 release

## 상태

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- roadmapSequenceMatch: `true`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `PASS_REUSE`
- sourceTask: `TASK-G2-OPERATIONS-002 Change 004`
- productionDeploymentApproved: `true`
- approvalSource: `USER_EXPLICIT_2026-09-02_G2_FORMULA_PUBLIC_RELEASE`
- productPr: `#119`
- sourceSha: `7a2c7f172a4a0e4b0e69a29c72ac205af1299c74`
- azureReleaseRun: `33589472932`
- status: `PUBLIC_RELEASE_COMPLETE`

## 승인과 목적

사용자는 2026-08-28부터 G2 재고에 아래 수식을 적용하고 원격 `main` 병합과 Azure 전체 공개배포까지 한 번에 진행하도록 명시 승인했다.

`재고(D) = 재고(D-1) + 생산(D-1) - 불량(D-1) - 납품(D)`

표시 날짜의 납품은 그 날짜 재고에서 바로 차감하고, 그 날짜 생산·불량은 다음 날짜 재고에 반영한다. 2026-08-27까지의 기존 수식과 실사 우선 규칙은 유지한다.

## 게시·배포 기준

- 제품 PR: `#119`, head `ed5d0035e45f3b9388e385144ea8a9ab0d346217`
- PR CI: `33587777592`, Backend·Frontend·Full-Stack·Workflow Validation·CI Gate `PASS`
- squash merge source: exact `main` `7a2c7f172a4a0e4b0e69a29c72ac205af1299c74`
- main CI: `33589432228`, 검증된 PR tree 재사용 `PASS`
- Azure release: `33589472932`
- 배포 범위: Backend·Frontend
- migration·schema·운영 G2 원본 데이터 mutation: `0`
- 제외: Persistent UAT, 실제 외부 알림 시험 발송

## 실행 결과

| 항목 | 결과 |
| --- | --- |
| source validation | `PASS` |
| Backend image | `PASS` |
| Frontend image | `PASS` |
| migration | `SKIPPED` |
| Backend revision | `PASS` |
| Frontend revision | `PASS` |
| workflow public security | `PASS` |
| 별도 health | `200` |
| 별도 익명 root·`/api/me` | `401/401` |
| 인증된 공개 G2 수식 | `PASS` |

공개 G2의 2026-08-28 값을 privacy-safe 숫자 projection으로 대조했다.

`전일 재고 2 + 전일 생산 34 - 전일 불량 0 - 당일 납품 30 = 기대 재고 6 = 표시 재고 6`

해당 날짜는 실사 override가 아니며, 새 수식과 표시 결과가 일치한다.

이 확인은 절단일의 실사 없는 공개 날짜 1건을 검증한 것이다. 이후 모든 운영일 입력의 현업 정확성을 대신하지 않으며 사용자 운영 관찰을 다음 Gate로 유지한다.

## Finding

- 신규 Open P0/P1/P2: `0/0/0`
- `GHA-AZURE-RUNNER-WARNINGS-001` / P3 / `BACKLOG`: 기존 action runner 유지보수 항목을 계속 추적한다. 이번 변경에서는 별도 보정하지 않았으며 release workflow는 성공했다.
- 실제 운영 G2 원본 데이터와 Persistent UAT를 수정하지 않았으므로 별도 데이터 보정 검증은 적용 대상이 아니다.

## Rollback·forward-fix

- application 장애 시 직전 검증된 Change 003 공개 image로 Backend·Frontend를 함께 되돌리고 G2 재고 판단을 중지한다.
- schema 변경이 없으므로 migration rollback은 없다.
- 수식 결함은 적용된 migration을 수정하지 않고 Backend·Frontend forward-fix와 같은 전체·부분·하루 단독 조회 회귀로 보정한다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | Change 030 결과 동기화 | `tasks/azure-deploy-001-implementation-report.md` |
| SOP | latest exact-main release 기준 동기화 | `tasks/azure-deploy-001-sop.md` |
| User manual | 변경 없음 — 기존 승인형 GitHub release 동선 재사용 | `infrastructure/azure-pilot/README.md` |
| Roadmap update | 6.2·6.5·추적 97·Decision Log 동기화 | `docs/00-product-roadmap.md` |
| User validation checklist | 자동 공개 검증 완료와 운영 관찰 분리 | `tasks/azure-deploy-001-user-validation-checklist.md` |
