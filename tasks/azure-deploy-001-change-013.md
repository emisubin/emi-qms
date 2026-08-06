# TASK-AZURE-DEPLOY-001 Change 013 — 공개 Front Door 전환과 API origin routing 보정

## Task gate

- instructionChainRead: `true`
- taskType: `UAT_RUNTIME`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `Front Door validation·managed TLS → Entra → provider 검수`
- roadmapSequenceMatch: `true`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`
- executionStatus: `IN_PROGRESS`

## 승인과 범위

- approvalSource: `USER_EXPLICIT_PUBLIC_AZURE_DEPLOYMENT`
- 승인일: 2026-08-06
- 포함: Approved domain·managed TLS 확인, 공개 Front Door·PWA·origin 보호 smoke, 공개 API origin routing 결함 보정, 보정 image 검증과 적용 준비, Entra 운영 주소 검증
- 제외: 업무 DB reset·drop, 실제 Teams·Gmail 발송, 알림 escalation 정책 변경, QOM 기능 branch의 push·merge·Azure 반영

## 실행 기준선

1. Front Door custom domain validation·deployment·provisioning은 모두 완료됐고 managed certificate와 TLS 1.2가 적용됐다.
2. 공개 HTTPS root·PWA manifest·icon은 `200`, HTTP는 HTTPS redirect, 직접 Frontend origin root는 `403`이었다.
3. Backend·Frontend·ClamAV는 `3/3 Running`, provisioning은 모두 `Succeeded`였다.
4. 공개 `/api/me`, `/api/runtime-mode`, `/health/ready`는 모두 `404`여서 application login Gate는 통과하지 못했다.

## 확인된 원인과 최소 보정

- Frontend Nginx가 내부 Backend Container App으로 proxy할 때 HTTP `Host`를 공개 hostname으로 덮어썼다.
- 같은 Container Apps environment 안에서 TLS 대상은 Backend ingress FQDN이지만 HTTP routing host가 공개 hostname이라 내부 Backend application route에 도달하지 못했다.
- HTTP `Host`는 `BACKEND_FQDN`, application이 신뢰할 원래 공개 주소는 `X-Forwarded-Host: PUBLIC_HOST`로 분리한다.
- Front Door ID·origin verification token, direct origin `403`, forwarded client IP, public HTTPS와 Backend `AllowedHosts` 계약은 보존한다.

## 검증 계획

1. Azure artifact static validation과 관련 Backend security test
2. Frontend Azure image local build
3. 검증된 source를 원격 main에 반영한 뒤 immutable image 게시·Frontend revision 교체
4. 공개 root·PWA `200`, 직접 origin `403`, 익명 보호 API `401`, Backend readiness `200`, TLS·security header 확인
5. Entra 운영 redirect·origin 확인 후 로그인 검수

## Finding

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `AZURE-FRONTEND-BACKEND-HOST-001` | P1 | `RESOLVED_LOCAL` | 공개 정적 화면은 열리지만 Frontend proxy의 내부 routing host 오류로 모든 Backend API가 `404`여서 로그인과 업무 기능을 사용할 수 없었다. | Nginx Host/X-Forwarded-Host 역할을 분리하고 artifact·security test·운영형 image build를 통과했다. image 교체 뒤 public API smoke는 runtime Gate로 계속한다. |

Open P0/P1/P2 Finding은 `0`이다. 새 image의 public API smoke 전 공개 배포 완료 판정과 provider 활성화를 금지한다.
