# TASK-AZURE-DEPLOY-001 Change 015 — 공개 Frontend Entra 사전 인증

## Task Identity Gate

- proposedTaskId: `TASK-AZURE-DEPLOY-001`
- taskType: `SECURITY_HARDENING`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `TEAMS_APPROVAL_AND_PROVIDER_SMOKE`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 공개 Front Door 주소에서 인증 전에는 Frontend shell·JavaScript·PWA asset을 제공하지 않는다.
- Root Finding 또는 정책 결정: SPA client 로그인 전 정적 bundle이 익명 요청에 제공되는 구조를 Entra 사전 인증으로 강화한다.
- 변경·검증 경계: Container Apps Easy Auth, single-tenant Entra web app, Key Vault secret-scope RBAC, 익명·인증 브라우저와 rollback smoke
- 보존할 불변조건: Backend bearer·역할 권한, Front Door origin 검증, DB·migration, 알림 provider disabled/dry-run
- 예상 산출물: Bicep·ARM JSON·보안 test·운영 SOP·검수 기록과 실제 운영 auth config

## 승인과 범위

- approvalSource: `USER_EXPLICIT_PRODUCTION_PREAUTH_NO_TEST_ENV_TEAMS_NOTIFICATION_ONLY_FALLBACK`
- 승인일: 2026-08-06
- 포함: 운영 `pms` Frontend에 Entra 사전 인증을 직접 적용하고 익명 bundle 차단·기존 로그인·rollback을 검증한다.
- 제외: 시험환경·별도 Teams manifest, DB·migration·업무기능, Backend 권한 축소, 실제 Teams·Gmail 발송
- Teams 정책: Teams 탭에서 사전 인증 호환 문제가 발생하면 탭 사용을 중단하고 Teams는 Activity 알림 전용으로 유지한다.

## 구현 계약

1. Frontend Container App의 모든 업무·정적 asset 요청은 인증 전 `RedirectToLoginPage`로 처리한다. 비브라우저 요청은 `401`, 브라우저 요청은 PMS shell·bundle이 없는 Easy Auth 인증 화면으로 응답할 수 있다.
2. Container Apps·Front Door probe에 필요한 `/health/live`만 사전 인증에서 제외한다.
3. Entra 등록은 single-tenant web application으로 분리하고 secret 원문은 Key Vault에만 둔다.
4. Front Door의 표준 forwarded host/proto를 사용해 공개 hostname callback을 보존한다.
5. Easy Auth 통과 뒤에도 Backend bearer token과 업무 역할 권한을 계속 검증한다.
6. 장애 시 auth platform을 비활성화해 기존 app-level 로그인으로 즉시 rollback하며 DB·image·revision은 변경하지 않는다.

## 검증 계획

1. Bicep compile, ARM JSON 동등성, shell validator와 public deployment security test
2. 운영 적용 전 Azure account·resource·현재 auth 상태 privacy-safe projection
3. 익명 root·대표 JavaScript·manifest가 `401` 또는 PMS shell·bundle 없는 인증 화면이고 health만 성공하는지 확인
4. 실제 허용 계정 로그인 뒤 app shell·API·관리자 화면 접근 확인
5. direct origin 보호, Backend bearer 권한과 external provider disabled 상태 확인
6. rollback 명령 dry contract와 적용 후 auth config readback 확인

## Finding

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `ANON-FRONTEND-BUNDLE-001` | P2 | `RESOLVED_RUNTIME` | Frontend가 app-level 로그인 전에 정적 shell과 bundle을 제공해 익명 사용자가 화면·route 구조를 분석할 수 있었다. | Container Apps Entra 사전 인증을 운영에 적용했다. 익명 비브라우저 root·asset·manifest·API는 `401`, 브라우저 요청은 PMS root·asset 참조가 없는 인증 화면, health는 `200`이다. 실제 허용 계정은 인증 뒤 프로젝트·관리자 메뉴에 접근했다. |
| `TEAMS-PREAUTH-COMPAT-001` | Policy | `RISK_ACCEPTED_BY_USER` | Teams tab iframe은 server-directed login redirect와 호환되지 않을 수 있다. | 문제 발생 시 Teams tab을 사용하지 않고 기존 Activity 알림 전용으로 운영한다. |

## 구현·운영 검증 결과

- Bicep compile·ARM JSON 동등성·Azure artifact validator: `PASS`
- Public deployment security 집중 test: `42/42 PASS`
- Entra app: single-tenant `1`, service principal `1`, 조직 전체 동의 요청 없음
- Key Vault·Frontend secret binding·secret-scope RBAC: 각각 `1`
- Auth config: enabled, `RedirectToLoginPage`, `Standard`, HTTPS, 제외 경로 `1`
- 익명 비브라우저 root·대표 asset·manifest·API: `401`; health: `200`
- 익명 브라우저: Easy Auth 인증 화면이며 PMS root·bundle reference 없음
- 실제 허용 계정: PMS root·asset load, 프로젝트·관리자 메뉴 접근 성공
- Backend·DB·migration·container image·actual notification provider: 변경 없음
- Rollback: 실행 절차와 auth-only 경계를 확인했으며 보안 게이트를 다시 여는 실제 rollback은 수행하지 않았다.

## 게시·운영 경계

- 구현 branch의 commit·push·PR은 별도 Git 게시 승인 전까지 자동 수행하지 않는다.
- 원격 `main` merge는 해당 병합에 대한 명시적 승인 전까지 수행하지 않는다.
- 운영 auth mutation은 이 Change에서 사용자가 명시적으로 승인했다.

## Rollback

- Frontend auth platform을 비활성화하고 `AllowAnonymous`로 되돌리면 기존 app-level Entra 로그인 흐름이 복구된다.
- Entra 등록과 Key Vault secret은 rollback 직후 삭제하지 않고 재검증이 끝날 때까지 보존한다.
- DB·migration·container image·Backend revision 변경은 없다.
