# TASK-AZURE-DEPLOY-001 Change 017 — 운영 외부 알림 Worker 활성화

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `EXTERNAL_NOTIFICATION_ACTIVATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_WORKER_ACTIVATION_AND_EXISTING_BACKLOG_SEND`
- 승인일: 2026-08-06
- 기준 commit: `86137539aa2f22ba8c6ebf742ba8b6f44d03c091` (`origin/main`과 동일)
- 작업 branch: `fix/task-azure-deploy-001-preauth-015`
- 사용자는 기존 대기 알림이 한꺼번에 외부 발송되는 것을 명시적으로 허용했다.

## Purpose identity

- 업무 목표: 운영 수동 Mail·Teams Activity와 기존 자동 알림 대기열을 실제 외부 provider로 처리한다.
- Root condition: 수동 발송은 delivery를 `Pending`으로 저장하지만 운영 Notification Dispatcher와 Mail·Teams Activity가 disabled/dry-run이라 시도 횟수 `0`으로 남았다.
- 변경 경계: 운영 Backend 1개의 `Notifications:Dispatch`, `Notifications:TeamsActivity`, `Notifications:Mail` actual 설정과 그 결과로 생성되는 새 revision만 변경한다.
- 보존할 불변조건: Teams 채널 webhook, Teams Direct Message, Gmail/Teams credential 값, DB schema·migration·업무 row, Frontend·ClamAV·Front Door·Entra 사전 인증은 변경하지 않는다. 실제 주소·이름·identifier·secret을 출력하거나 tracked 파일에 기록하지 않는다.
- 예상 산출물: provider 준비 상태·새 Backend revision readiness·대기열 처리 결과의 privacy-safe 집계와 실패 시 채널별 rollback 판정.

## 실행 계약

1. 운영 Backend 후보가 정확히 1개이고 최대 replica가 1인지 확인한다.
2. Teams client credential과 Gmail username/app-password Key Vault binding·값이 모두 존재하며, Teams는 앞선 Graph actual `204`·web 렌더링 검증을 통과한 상태여야 한다.
3. 현재 Dispatcher·Mail·Teams Activity가 disabled/dry-run인 안전 기준선에서만 활성화한다.
4. `Dispatch.Enabled=true`, `TeamsActivity.Enabled=true`, `TeamsActivity.DryRun=false`, `Mail.Enabled=true`, `Mail.DryRun=false`만 변경한다. Mail provider는 기존 `Smtp`, host·port·TLS 설정을 유지한다.
5. 기존 `Pending`과 활성화 시 생성되는 즉시 delivery가 전송될 수 있음을 승인 범위로 고정한다. Provider 호출을 임의로 재실행하거나 중복 synthetic 알림을 추가하지 않는다.
6. 새 revision readiness를 확인하고 Mail·Teams Activity의 상태·attempt·안정 오류 코드만 모니터링한다.
7. 새 revision이 Ready가 아니거나 credential/provider 구성 오류가 반복되면 해당 채널 actual을 다시 disabled/dry-run으로 내리고 원인을 보고한다. 이미 수락된 외부 메시지는 취소하지 않는다.

## 제외 범위

- Teams Channel webhook·Teams Direct Message 활성화
- 새 synthetic Mail·Teams Activity 생성
- 알림 수신자·양식·에스컬레이션 정책 변경
- DB row 수동 수정·삭제·재처리 generation 생성
- Teams SSO·새 manifest 기획·구현
- Git commit·push·PR·merge

## 검증 계획

1. 활성화 전 Backend·secret binding·provider config·안전 기준선을 fixed projection으로 확인한다.
2. 새 Backend revision의 provisioning·health·replica readiness를 확인한다.
3. Runtime readback에서 다섯 actual flag가 exact인지 확인한다.
4. 최신 Mail·Teams Activity delivery의 `Pending/Processing/Sent/Failed/Disabled/DryRunSent`, attempt와 provider call 여부를 privacy-safe 집계로 확인한다.
5. Frontend·ClamAV·DB schema·migration·업무 data·Front Door·Entra 설정 비변경과 승인된 notification delivery 상태·attempt 갱신만 발생했는지 확인한다.

## 실행 결과

- activation preflight: `PASS`
- 운영 변경: 다섯 actual flag exact, 새 Backend revision `Ready`
- Teams/Gmail credential binding: 각 required secret 존재·값 검증 `PASS`
- 최신 수동 Teams Activity: 확인된 `6`건 모두 attempt `1`, `Sent`
- 최신 수동 Mail: 확인된 `3`건 모두 attempt `1`, `Sent`
- Open delivery 상태: `Pending 0`, `Processing 0`, `Failed 0`
- 사용자 실제 수신 확인: Teams Activity와 메일 모두 `PASS` (2026-08-06 사용자 확인)
- 과거 Mail `2`건은 이미 관리자 `Dismissed` 상태였으므로 worker claim 대상에서 제외된 상태를 보존했다.
- Teams Channel webhook·Frontend·ClamAV·DB schema·migration·업무 data·Front Door·Entra 설정은 변경하지 않았다. Notification delivery status·attempt는 승인된 worker 처리 결과로 갱신됐다.
- 새 synthetic 알림·재처리 generation·DB 수동 수정은 만들지 않았다.

## Rollback

- 전체 중단: Dispatcher를 `false`, Mail·Teams Activity를 `Enabled=false`, `DryRun=true`로 되돌린 새 Backend revision을 만든다.
- 채널별 provider 장애: Dispatcher는 유지하되 실패 채널만 disabled/dry-run으로 되돌릴 수 있다.
- rollback 뒤 남은 `Pending`은 삭제하거나 자동 재처리하지 않고 별도 사용자 판단 대상으로 유지한다.
