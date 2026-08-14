# TASK-PWA-PUSH-001 Change 002 — 운영 검수 완료와 Azure 재배포 보존

## Gate projection

- proposedTaskId: `TASK-PWA-PUSH-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-PWA-PUSH-001`
- roadmapNextGate: `OPERATIONAL_CLOSEOUT`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-PWA-PUSH-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 실제 Android·iPhone PWA 수신 검수 완료 상태를 문서에 반영하고, Azure 전체 workload를 다시 배포해도 현재 Web Push 활성 설정과 VAPID Key Vault 참조가 보존되게 한다.
- Root Finding 또는 정책 결정: 운영 runtime은 Web Push 실발송이 활성화됐지만 tracked Azure 배포 정의와 Task 문서는 아직 비활성·시험 모드를 기준으로 한다.
- 변경·검증 경계: PWA 운영 문서·Roadmap·Azure Bicep/ARM·보안 검증만 변경한다. 운영 secret 원문·사용자별 구독·알림 정책·DB·application source는 변경하지 않는다.
- 보존할 불변조건: 인앱 알림이 수신자와 내용의 source of truth이고, PWA 미설치·권한 미허용 사용자는 푸시만 받지 못한다. 나중에 활성화하면 그 이후 새 인앱 알림부터 받고 과거 알림은 소급 발송하지 않는다.
- 예상 산출물: 운영 상태 문서, Web Push Key Vault secret-scope RBAC와 Backend environment binding, 재생성 ARM JSON, Azure artifact·보안 회귀 결과.

## 사용자 승인과 운영 정책

2026-08-12 사용자는 운영 PWA 테스트 성공을 확인하고 다음을 승인·확정했다.

1. 실제 운영은 `Enabled=true`, `DryRun=false`로 유지한다.
2. VAPID 공개키·비밀키는 Azure Key Vault에 보관하고 Backend만 secret resource scope로 읽는다.
3. 직원별 PWA 설치와 알림 허용은 자율이다. 중앙 등록·미등록자 독촉·강제 설치 기능을 만들지 않는다.
4. PWA를 설치하지 않았거나 알림을 허용하지 않은 사용자는 인앱 알림은 정상적으로 보지만 PWA 푸시는 받지 못한다.
5. 사용자가 나중에 PWA를 설치하고 푸시를 켜면 활성화 이후 생성되는 새 인앱 알림부터 PWA로 받는다. 과거 알림은 소급 발송하지 않는다.
6. Teams·메일·인앱 알림의 기존 수신자와 발송 시점은 변경하지 않는다.

## 포함 범위

- PWA Implementation report·SOP·사용자 안내·검수 checklist·Roadmap 운영 상태 동기화
- Azure `foundation`, `identity-access`, `workloads`의 VAPID secret 이름·Backend 전용 secret-scope RBAC·Container App secret reference
- 기존 운영에서 수동 생성된 Frontend access-gate·Web Push role assignment를 삭제 없이 인수하는 선택 role-name parameter와 `what-if` gate
- `enableExternalNotifications=true`일 때 Web Push도 `Enabled=true`, `DryRun=false`가 되도록 기존 운영 toggle에 연결
- ARM JSON 재생성과 Azure artifact·Backend 공개 배포 보안 회귀

## 제외 범위

- 실제 VAPID key 값 조회·출력·회전
- 직원별 설치 현황 관리, 강제 설치, 관리자 원격 구독 목록
- 인앱·Teams·메일·PWA 수신자 정책 변경
- 운영 DB·migration·Container App·Key Vault 값·RBAC의 즉시 mutation
- 직원별 설치 강제, 알림 수신자 정책 변경과 운영 비밀값 회전

## 게시 승인

- 2026-08-14 사용자가 Change 002의 원격 `main` 병합과 Azure 공개배포를 명시 승인했다.
- 게시 대상은 추적 Azure 배포 정의와 운영 상태 문서이며, 운영 VAPID 원문·사용자별 구독·알림 업무 데이터는 포함하지 않는다.

## 완료 Gate

1. tracked 파일과 검증 출력에 VAPID key·endpoint·구독 key 원문이 없다.
2. Backend identity만 두 VAPID secret에 `Key Vault Secrets User`를 갖는다.
3. workload 정의가 Web Push 활성·실발송과 두 secret reference를 모두 포함한다.
4. Bicep과 tracked ARM JSON이 일치한다.
5. Azure artifact validator와 Public Deployment Security 집중 회귀가 통과한다.
6. 실제 운영 Backend의 현재 Web Push 활성·Key Vault binding·ready 상태는 변경 없이 유지된다.
7. 실제 운영 입력을 사용한 identity-access `what-if`에서 role assignment Create/Delete가 `0/0`이다.
8. Open P0/P1/P2가 `0/0/0`이다.

## 게시 결과

- PR #103 필수 CI를 통과해 exact main SHA `58c089993587deea30513cb6edee0b8396a1d474`에 병합했다.
- Azure release `31786040822`가 migration·Backend·Frontend와 public security를 모두 `PASS`로 완료했다.
- 재배포 뒤 Backend는 `Enabled=true`, `DryRun=false`이고 공개키·비밀키 Key Vault secret reference를 유지하며 latest revision과 ready revision이 일치하고 Running이다.
- 운영 secret 원문·사용자별 구독·인앱·Teams·메일 수신자 정책은 변경하지 않았다.
