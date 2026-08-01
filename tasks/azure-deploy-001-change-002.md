# TASK-AZURE-DEPLOY-001 Change 002 — Git 병합과 공개 Traffic Gate 분리

## 승인과 분류

- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- changeApproved: `true`
- approvalSource: `USER_CONFIRMED_MERGE_BEFORE_AZURE_RUNTIME`
- 승인일: 2026-08-01

## Root Finding

Change 001 Implementation report는 Azure 실제 runtime에서만 검증할 수 있는 DB 복구, DNS·TLS·Entra callback·origin 차단과 Teams·Gmail smoke를 P1으로 분류하고 Git merge까지 차단했다. 그러나 이 항목은 배포 코드의 확인된 결함이 아니라 Azure resource 생성 후에만 실행 가능한 pre-traffic operational gate다. Git에 보존된 검증 코드를 기준으로 배포하려는 순서와도 충돌했다.

## 확정 정책

1. Local automated validation을 통과한 Azure 배포 코드는 사용자의 별도 merge 승인 후 `main`에 반영할 수 있다.
2. `AZURE-RESTORE-001`, `AZURE-EDGE-AUTH-001`, `AZURE-PROVIDER-001`은 P1 Finding이 아닌 `PRE_TRAFFIC_GATE`로 관리한다.
3. 세 Gate가 모두 PASS가 되기 전에는 시범 서비스의 public traffic과 external notification을 활성화하지 않는다.
4. Git merge는 Azure resource 생성, image push, DNS, traffic 전환이나 실제 provider 발송을 승인하지 않는다.
5. 비용 관련 Azure 실행은 기존대로 사용자가 직접 수행한다.

## 검증 및 게시 Gate

- Open P0/P1/P2 code Finding: `0`
- `AZURE-APM-001`, `FRONTEND-BUNDLE-001`: Roadmap에 연결한 P3 backlog
- Git publication: 사용자가 `main` merge를 명시적으로 승인
- Public traffic: 세 `PRE_TRAFFIC_GATE` PASS 전까지 `NO_GO`
- Azure cost mutation: 이 change에 포함하지 않음
