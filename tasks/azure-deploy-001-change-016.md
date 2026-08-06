# TASK-AZURE-DEPLOY-001 Change 016 — 운영 Teams Activity 1건 Provider Smoke

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `TEAMS_APPROVAL_AND_PROVIDER_SMOKE`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_TEAMS_NOTIFICATION_TEST_FIRST`
- 승인일: 2026-08-06
- 기준 commit: `86137539aa2f22ba8c6ebf742ba8b6f44d03c091` (`origin/main`과 동일)
- 작업 branch: `fix/task-azure-deploy-001-preauth-015`
- 선행 WIP: Change 015의 공개 Frontend 사전 인증 source 변경을 보존하고 같은 canonical Task에서 누적한다.

## Purpose identity

- 업무 목표: 사용자가 조직 catalog에서 설치한 공개 Teams 앱으로 운영 Activity Feed 알림 1건이 실제 수신 가능한지 검증한다.
- Root condition: Teams 앱 설치는 완료됐지만 공개 Azure의 Teams Activity 실제 provider smoke는 아직 수행되지 않았다.
- 변경·검증 경계: 현재 Azure 로그인 사용자와 bootstrap 관리자 일치, 운영 Backend의 Teams credential·manifest 설정, 합성 `generalNotification` 1건의 Microsoft Graph 수락 여부를 확인한다.
- 보존할 불변조건: Gmail과 Teams 채널은 발송하지 않는다. 운영 Notification Dispatcher와 자동 업무 event를 활성화하지 않는다. 실제 프로젝트·Pending·업무 row를 만들거나 변경하지 않는다. DB·migration·Frontend 사전 인증·container image는 변경하지 않는다.
- 예상 산출물: 1회 actual 호출의 privacy-safe 상태, Teams client 사용자 수신 확인 항목, 실패 시 안정 오류 코드와 후속 조건.

## 실행 계약

1. 대상은 현재 Azure CLI 로그인 사용자 1명으로 고정하고, 해당 주소가 bootstrap 관리자 목록에 포함되지 않으면 발송하지 않는다.
2. 운영 Backend 후보가 정확히 1개이고 Teams 필수 설정과 Key Vault secret binding이 모두 존재할 때만 진행한다.
3. Runtime의 Dispatcher·Teams Activity·Mail은 기존 disabled/dry-run 상태를 유지한다.
4. Graph payload는 실제 업무명·프로젝트·사용자 이름을 포함하지 않는 합성 `generalNotification`으로 고정한다.
5. `sendActivityNotification`은 재시도 없이 정확히 1회만 호출한다. 성공 기준은 Graph HTTP `204`다.
6. Provider `Sent`와 Teams client 실제 표시는 별도 검수한다. 사용자가 수신을 확인하기 전에는 client 표시 완료로 기록하지 않는다.
7. 수신 누락 보고가 있으면 알림을 재발송하지 않고 대상 Entra 사용자 일치, 개인 설치 조회 가능 여부와 Teams web 렌더링을 먼저 읽기 전용으로 진단한다.

## 제외 범위

- Teams SSO와 새 manifest 기획·구현·게시
- 실제 프로젝트 생성·납기·상태·업무·Pending·재검사·완료 event 생성
- Gmail·TeamsChannel actual 발송
- Notification Dispatcher·Daily Digest·Escalation worker 활성화
- DB·migration·container revision·Front Door·Entra 사전 인증 변경
- Git commit·push·PR·merge

## 변경 Allowlist

- `scripts/smoke-azure-teams-activity.sh`
- `tasks/azure-deploy-001-change-016.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 검증 계획

1. Shell syntax·ShellCheck와 실제 발송 flag 누락 negative 경로를 검증한다.
2. 발송 전 Backend 후보 `1`, current-user bootstrap match, 필수 설정·secret binding, Teams/Gmail/Dispatcher 안전 상태를 fixed projection으로 확인한다.
3. Microsoft Graph actual 호출 횟수 `1`, HTTP status와 안정 결과 코드만 기록한다.
4. Runtime env·revision·DB·migration·업무 data 비변경을 확인한다.
5. Graph `204` 뒤 사용자가 Teams client Activity Feed 표시를 직접 확인한다.

## 실행 결과와 후속 진단

- 실제 provider 호출은 합성 `generalNotification` 1건으로 끝났고 Graph HTTP `204`를 받았다.
- 사용자 제공 계정과 발송에 사용한 현재 Azure 계정은 같은 Entra 사용자로 확인됐다. 실제 주소·object identifier는 출력하거나 tracked 문서에 기록하지 않았다.
- 사용자의 최초 미표시 보고 뒤 동일 알림을 재발송하지 않았다.
- 앱 자격증명으로 개인 설치 목록을 읽는 진단은 Graph `403`으로 차단됐다. 설치 조회 권한을 추가하거나 관리자 동의를 변경하지 않았다.
- 기존 로그인 상태의 Teams web Activity Feed에서 합성 알림의 exact 제목과 preview가 모두 표시되는 것을 확인했다. Provider·manifest activity·Teams server 렌더링은 `PASS`이며, 최초 미표시는 desktop client의 동기화·새로고침 지연으로 분리한다.
- desktop Teams에서 같은 알림이 보이는지는 사용자 확인 항목으로 유지한다.

## Rollback

- Runtime 설정을 변경하지 않으므로 application rollback은 없다.
- 실패한 합성 알림은 재시도하지 않고 안정 오류 코드에 따라 설치·권한·manifest 정합성을 먼저 보정한다.
- 실제 provider 호출이 이미 수락된 경우 취소 API가 없으므로 같은 알림을 다시 만들지 않는다.
