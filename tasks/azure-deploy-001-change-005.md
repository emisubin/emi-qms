# TASK-AZURE-DEPLOY-001 Change 005 — Active workload readiness 보정

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `ACTIVE_WORKLOAD_READINESS`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## 승인과 기준선

- canonicalTask: `TASK-AZURE-DEPLOY-001`
- changeApproved: `true`
- approvalSource: `USER_EXPLICIT_DIRECT_AZURE_EXECUTION`
- 승인일: 2026-08-03
- 기준 branch: `origin/main`
- 작업 branch: `fix/task-azure-deploy-001-runtime-readiness`
- 사용자 검수: `대기`
- 공개 traffic: `비활성 유지`
- 외부 알림: `비활성 유지`

## Purpose identity

- 업무 목표: PITR 복구 검증 뒤 활성화한 Backend와 Frontend가 Container Apps probe를 통과해 안정적으로 실행되게 한다.
- Root Finding 또는 정책 결정: Backend probe의 Host 계약 누락과 Frontend Nginx map hash 용량 부족이 실제 Azure revision 재시작을 유발했다.
- 변경·검증 경계: Azure 전용 Nginx template, Backend probe, 생성 ARM JSON과 배포 정적 검증만 보정하고 제품 기능·DB schema·권한·알림 정책은 변경하지 않는다.
- 보존할 불변조건: Backend·ClamAV private ingress, Front Door ID+origin token 이중 검증, migration Exact·PITR 선행 Gate, public traffic·external notification fail-closed.
- 예상 산출물: readiness 보정 코드, 생성 ARM JSON, 정적·Nginx·실제 Azure revision 검증과 privacy-safe Implementation report.

## 검색 범위

- [x] `tasks/`의 canonical Task·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open PR과 현재 배포 branch

## Root Finding

### `AZURE-BACKEND-PROBE-HOST-001` — P1

Backend는 Production에서 허용된 public Host만 받도록 fail-closed다. Container Apps HTTP probe가 내부 기본 Host를 사용해 `/health/live`가 `400`을 반환했고, 정상 시작한 Backend revision이 startup probe 실패로 반복 종료됐다.

### `AZURE-FRONTEND-NGINX-MAP-001` — P1

Frontend는 Front Door ID와 별도 origin token을 Nginx `map` key로 비교한다. 64자 token을 기본 map hash bucket에 넣으면 Nginx configuration 생성 뒤 시작 검사가 실패해 revision이 즉시 종료됐다.

## 승인된 수정 범위

1. Backend startup·liveness·readiness probe에 `Host: publicHost`를 명시한다.
2. Nginx HTTP context의 `map_hash_bucket_size`를 `128`로 고정한다.
3. Bicep 생성 ARM JSON을 compiler 결과와 구조적으로 일치시킨다.
4. 정적 validator가 세 Backend probe Host header와 Nginx bucket 설정을 회귀 검사하게 한다.
5. 새 Frontend image와 workload template을 배포한 뒤 replica ready, restart 안정성과 health/origin 차단을 privacy-safe projection으로 검증한다.

## 제외 범위

- 제품 화면·API·업무 workflow 변경
- DB migration·data 수정 또는 secret 회전
- Edge·DNS·TLS 선행 활성화
- Teams·Gmail 실제 발송
- HA·WAF·Blob 도입

## 변경 Allowlist

- `infrastructure/azure-pilot/nginx.conf.template`
- `infrastructure/azure-pilot/workloads.bicep`
- `infrastructure/azure-pilot/workloads.json`
- `scripts/validate-azure-pilot-artifacts.sh`
- `tasks/azure-deploy-001-change-005.md`
- `tasks/azure-deploy-001-implementation-report.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `docs/00-product-roadmap.md`

## 완료 기준

- Bicep compile·생성 ARM JSON 구조 동등성·Git whitespace 검증이 통과한다.
- 64자 synthetic origin token을 사용한 Nginx configuration test가 통과한다.
- Backend 세 probe가 public Host header를 포함한다.
- Backend·Frontend·ClamAV replica가 ready이고 신규 revision의 재시작이 증가하지 않는다.
- Frontend direct `/health/live`는 `200`, direct 업무 route는 `403`을 유지한다.
- public traffic과 external notification은 다음 Gate 전까지 비활성이다.
- 실제 identifier·hostname·email·secret·connection string과 raw log가 tracked 파일 또는 보고 출력에 없다.
