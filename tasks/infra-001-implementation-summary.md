# TASK-INFRA-001 구현 요약

## 1. 목적

TASK-INFRA-001은 EMI 프로젝트 통합관리시스템의 운영 인증 기반을 Microsoft 365 / Entra ID 기준으로 전환하기 위한 작업이다.

- Microsoft 365 로그인
- EntraId 기반 운영 인증
- 기존 Dev 인증 보존
- 신규 Entra 사용자 승인 대기
- 최소 사용자 관리 화면
- System Administrator 검수 사용자 전환
- 로그인 상태 유지와 새로고침 복원

## 2. 인증 구조

- Frontend는 MSAL React를 사용한다.
- Backend는 JWT Bearer + Microsoft.Identity.Web으로 Entra access token을 검증한다.
- Backend 인증 scheme은 policy scheme forward selector 구조다.
- `Authorization: Bearer`가 있으면 Bearer 인증을 우선한다.
- Bearer token이 invalid이면 `X-Dev-User`로 fallback하지 않는다.
- `X-Dev-User`는 Dev mode 전용이다.
- `X-Qms-Test-User`는 EntraId로 로그인한 System Administrator의 검수 사용자 전환 전용이다.

## 3. 환경 모드

인증 모드는 Dev mode와 EntraId mode로 나뉜다.

- Dev mode: 기존 개발 사용자 selector와 `X-Dev-User` header를 사용한다.
- EntraId mode: MSAL access token과 Bearer header를 사용한다.
- Dev 인증은 Development/Testing에서만 허용한다.
- Admin user switch는 Development/Testing/UAT에서만 허용한다.
- Production/Staging에서 Dev 인증 또는 Admin user switch가 활성화되면 startup fail-fast 대상이다.

주요 설정 key는 다음과 같다. 실제 값은 문서와 코드에 기록하지 않는다.

- `Authentication:Mode`
- `DEV_AUTHENTICATION_ENABLED`
- `AdminUserSwitch:Enabled`
- `ADMIN_USER_SWITCH_ENABLED`
- `AzureAd:TenantId`
- `AzureAd:ClientId`
- `AzureAd:Audience`
- `AzureAd:ValidAudience`
- `AzureAd:Instance`
- `Authentication:BootstrapAdminEmails`
- `VITE_AUTH_MODE`
- `VITE_AZURE_TENANT_ID`
- `VITE_AZURE_CLIENT_ID`
- `VITE_AZURE_API_SCOPE`
- `VITE_AZURE_REDIRECT_URI`

## 4. Backend 주요 구현 파일

- `backend/src/Emi.Qms.Api/Program.cs`
  - 인증/권한 미들웨어 연결
  - Dev 인증, Dev seed, Admin user switch, Entra 설정 fail-fast 적용
  - `AdminUserSwitchGuardMiddleware` 등록

- `backend/src/Emi.Qms.Api/Authorization/AuthorizationServiceCollectionExtensions.cs`
  - policy scheme forward selector 구성
  - Bearer 우선 정책
  - Dev mode에서만 `X-Dev-User` scheme 사용
  - Microsoft.Identity.Web JWT Bearer 등록

- `backend/src/Emi.Qms.Api/Authorization/QmsAuthenticationMode.cs`
  - Dev / EntraId mode 판정
  - EntraId mode 설정 누락 fail-fast

- `backend/src/Emi.Qms.Api/Authorization/EntraClaimsTransformation.cs`
  - Entra token claim에서 `oid`, display name, email 후보를 읽는다.
  - DB 사용자 JIT 생성 및 조회
  - 기존 server policy가 사용하는 `qms.*` claim 부착
  - 승인 대기/비활성 사용자 권한 claim 차단
  - 검수 사용자 전환 시 actual/effective user claim 분리

- `backend/src/Emi.Qms.Api/Authorization/AdminUserSwitchGuardMiddleware.cs`
  - `X-Qms-Test-User` 요청 차단 조건 강제
  - Bearer 인증 성공 후에만 검수 사용자 전환 허용
  - 일반 사용자, 승인 대기 사용자, 비활성 사용자, invalid persona 차단

- `backend/src/Emi.Qms.Api/Identity/DbIdentityStore.cs`
  - EntraId 사용자 DB 조회/JIT 생성
  - `entra_object_id` 기준 식별
  - bootstrap admin role 부여
  - 사용자/역할/권한/profile 조회

- `backend/src/Emi.Qms.Api/Identity/HybridIdentityStore.cs`
  - Dev user는 기존 in-memory store 경로 유지
  - EntraId user는 DB store 경로 사용

- `backend/src/Emi.Qms.Api/Identity/UserAdministrationStore.cs`
  - 최소 사용자 관리 화면용 store
  - EntraId 사용자 역할/부서/활성 상태 수정
  - Dev user read-only 표시
  - 마지막 active System Administrator 보호

- `backend/src/Emi.Qms.Api/Identity/IdentityEndpointExtensions.cs`
  - `/api/me`
  - `/api/admin/users`
  - `/api/admin/users/{userId}`

- `database/migrations/0015_microsoft_365_identity.sql`
  - EntraId identity column 추가
  - auth provider 제약 및 index 추가

## 5. Frontend 주요 구현 파일

- `frontend/src/auth.ts`
  - MSAL 설정
  - tenant-specific authority
  - login request / account switch request
  - MSAL cacheLocation 결정
  - cached account 복원 helper
  - `acquireTokenSilent` helper

- `frontend/src/main.tsx`
  - EntraId mode에서 MSAL instance 생성
  - 로그인 상태 유지 preference에 따라 MSAL cacheLocation 적용

- `frontend/src/api.ts`
  - Dev mode: `X-Dev-User`
  - EntraId mode: `Authorization: Bearer`
  - 검수 사용자 전환 시 `X-Qms-Test-User`
  - interaction-required token 오류를 재로그인 안내로 매핑

- `frontend/src/App.tsx`
  - Entra 로그인 화면
  - 로그인 상태 유지 체크박스
  - cached account 복원 auth gate
  - `/api/me` 상태별 화면 분기
  - 승인 대기/권한 없음 안내 화면
  - 최소 사용자 관리 화면
  - 검수 사용자 전환 UI

- `frontend/src/identity.ts`
  - `/api/me` actual/effective user 응답 타입

- `frontend/tests/auth.test.tsx`
  - 인증 mode, header, MSAL cache, account 복원, silent token 분기 테스트

## 6. DB 변경

신규 migration은 `0015_microsoft_365_identity.sql`이다.

`qms_users`에 추가된 column:

- `entra_object_id text null`
- `email text null`
- `auth_provider text not null default 'Dev'`

제약 및 index:

- `auth_provider in ('Dev', 'EntraId')` check constraint
- `entra_object_id` nullable unique index
- `auth_provider` index
- `email` non-unique index

정책:

- 기존 row는 `auth_provider='Dev'`로 backfill된다.
- email은 unique가 아니다.
- dev user와 Entra user는 email이 같아도 자동 병합하지 않는다.
- 기존 user/role/permission/project data는 보존한다.

## 7. JIT 사용자 생성 정책

- Entra 사용자는 token의 `oid` 기준으로 식별한다.
- 최초 로그인 시 `qms_users`에 EntraId user를 생성한다.
- 재로그인 시 display name/email 후보 값을 동기화한다.
- email은 식별자가 아니며 자동 병합에 사용하지 않는다.
- `entra_object_id` unique 제약과 upsert로 동시 최초 로그인 중복 생성을 방어한다.

## 8. 승인 대기 정책

승인 대기 판정:

```text
auth_provider='EntraId'
AND is_active=true
AND active role count == 0
```

- `department_id`는 승인 대기 해소 조건이 아니다.
- role이 1개 이상이면 department가 없어도 승인 대기가 아니다.
- 비활성 사용자는 승인 대기가 아니라 접근 차단 대상이다.
- 승인 대기 사용자는 `/api/me`, 로그인/로그아웃, 승인 대기 안내 화면만 사용할 수 있다.
- 승인 대기 사용자는 프로젝트, 내 업무, 알림, 생산관리, 구매, 자재, 관리자 화면 등 업무 데이터 API에 접근할 수 없다.
- 승인 대기 사용자의 `/api/me`는 `approvalPending=true`, `roles=[]`, `permissions=[]` 기준이다.

## 9. Bootstrap admin

- 설정 key는 `Authentication:BootstrapAdminEmails`다.
- 이메일 비교는 trim + lowercase normalized 기준이다.
- 실제 이메일 값은 문서에 기록하지 않는다.
- 설정된 bootstrap admin email의 EntraId 사용자가 로그인하면 `system-administrator` role을 부여한다.
- 신규 Entra row뿐 아니라 기존 Entra row에 role이 없는 경우에도 bootstrap 조건을 다시 확인한다.
- 이미 role이 있으면 중복 부여하지 않는다.

## 10. 최소 사용자 관리

- System Administrator 전용 화면이다.
- 서버 policy로 `users.manage` 권한을 강제한다.
- EntraId 사용자만 역할/부서/활성 상태를 수정할 수 있다.
- Dev user는 목록에 표시되지만 read-only다.
- 마지막 active System Administrator를 비활성화하거나 role을 제거할 수 없다.
- 부서 master, 역할 master, 권한 matrix, 전체 관리자 기준정보 기능은 포함하지 않는다.
- 정식 사용자 관리 고도화는 TASK-ADMIN-001 범위다.

## 11. 검수 사용자 전환

- EntraId로 로그인한 System Administrator만 사용할 수 있다.
- 비운영 환경(Development/Testing/UAT)에서 명시적으로 활성화된 경우만 허용한다.
- Production/Staging에서는 활성화하면 안 된다.
- header는 `X-Qms-Test-User`다.
- 대상은 기존 dev persona다.
- 실제 Entra user impersonation은 구현하지 않는다.
- 권한 판단은 effective user 기준 `qms.*` claim을 사용한다.
- 실제 로그인 사용자는 actual user claim으로 보존한다.
- 감사/이력에서 actual/effective user를 더 정교하게 분리하는 작업은 후속 검토 대상이다.

## 12. 로그인 상태 유지

- 로그인 화면에 `로그인 상태 유지` 체크박스를 제공한다.
- 기본값은 checked다.
- checked이면 MSAL `cacheLocation=localStorage`를 사용한다.
- unchecked이면 MSAL `cacheLocation=sessionStorage`를 사용한다.
- 앱 코드는 token, Authorization header, refresh token을 직접 저장하지 않는다.
- 새로고침 시 MSAL cached account를 복원하고 active account로 설정한다.
- 이후 `acquireTokenSilent`로 API token을 다시 획득하고 `/api/me`를 확인한다.
- Microsoft Entra 조건부 액세스, MFA, sign-in frequency가 재인증을 요구하면 앱은 이를 우회하지 않는다.

## 13. /api/me 응답 구조

`/api/me`는 Dev mode와 EntraId mode 모두에서 동작한다.

주요 field:

- `userId`
- `displayName`
- `email`
- `authProvider`
- `isActive`
- `approvalPending`
- `department`
- `roles`
- `permissions`
- `projectAccess`
- `isTestUserSwitch`
- `testUserKey`
- `canUseAdminTestUserSwitch`
- `actualUser`
- `effectiveUser`

검수 사용자 전환이 없는 경우 actual/effective user는 동일하다.

## 14. 보안 원칙

- client secret을 사용하지 않는다.
- token 원문을 출력하지 않는다.
- Authorization header 원문을 출력하지 않는다.
- 실제 tenant/client/scope/email 값은 Git 추적 파일에 저장하지 않는다.
- Dev 인증은 운영/스테이징에서 차단한다.
- invalid Bearer는 `X-Dev-User`로 fallback하지 않는다.
- 업무 데이터 접근은 server policy로 강제한다.
- 승인 대기/비활성 사용자는 업무 데이터 API에 접근할 수 없다.
- 마지막 active System Administrator 보호를 서버에서 강제한다.

## 15. 테스트 결과

최종 리뷰 시 실제 실행한 결과:

- `git diff --check`: 통과
- `actionlint .github/workflows/ci.yml`: 통과
- Backend Release build: 통과
- Backend 전체 test: 221 passed
- Authorization/Identity/Migration 관련 filter test: 69 passed
- Frontend lint: 통과
  - 기존 Fast Refresh warning 1건 있음
- Frontend typecheck: 통과
- Frontend unit test: 51 passed
- Frontend build: 통과
  - 기존 chunk size warning 있음
- mock UI smoke: 1 passed
- Full-Stack E2E: 16 passed
- seed 격리 A/B/C/D: 각 221 passed
- UAT DB persistence: 통과
- Docker Compose config: 통과
- PostgreSQL healthy: 확인
- UAT Backend `/health/live`: 200
- UAT Backend `/health/ready`: 200
- UAT Frontend HTTP: 200
- Secret/PII scan: 실제 비밀정보 없음
- 실제 Microsoft 로그인 수동 검수: 사용자 검수 완료로 전달받음

## 16. 수동 검수 결과

다음 항목은 사용자가 직접 검수 완료했다고 전달했다.

- 실제 Microsoft 365 로그인
- System Administrator bootstrap
- 프로젝트 화면 진입 및 조회
- 검수 사용자 전환 UI
- 로그인 상태 유지
- 새로고침 후 로그인 복원

Codex가 사용자의 Microsoft 계정으로 직접 로그인한 것은 아니다.

## 17. 후속 TASK와 연결

- TASK-NOTIFY-001
  - Teams / 메일 알림 채널 확장
  - Graph 권한과 발송 handler는 아직 구현하지 않았다.

- TASK-NOTIFY-002
  - 예정일 기반 에스컬레이션

- TASK-ADMIN-001
  - 정식 사용자 관리 UI
  - 부서/역할 master 관리
  - 권한 matrix 고도화

- dev user 실계정 이관 후속
  - dev user와 실제 Entra user 자동 병합은 금지다.
  - 담당 프로젝트/내 업무 이관은 별도 수동 절차 또는 후속 TASK로 다룬다.

## 18. 제외 범위

- Teams/메일 발송
- Graph `Mail.Send`
- Graph Teams DM/channel message
- Teams Webhook
- 예정일 에스컬레이션
- 권한 matrix 재설계
- Entra group claim 기반 권한
- Entra App Role claim 기반 권한
- 실제 Entra user impersonation
- Azure 구독/결제 설정
- Pending List
- 검사/제조/물류/정산 기능

## 19. 운영 적용 전 체크리스트

- 실제 Entra 앱 등록값은 Git 밖 secret/env로 관리한다.
- SPA redirect URI가 운영 frontend URL과 일치해야 한다.
- API scope가 frontend 요청 scope와 일치해야 한다.
- 필요한 admin consent를 완료해야 한다.
- `Authentication:BootstrapAdminEmails`를 운영 환경 secret로 설정해야 한다.
- Production/Staging에서는 `Authentication:Mode=EntraId`를 사용한다.
- Production/Staging에서는 `AdminUserSwitch:Enabled=false`여야 한다.
- Production/Staging에서는 Dev auth가 비활성화되어야 한다.
- 운영 전 CI와 E2E가 통과해야 한다.
- UAT DB를 drop/truncate하지 않는다.

## 20. 알려진 주의사항

- Microsoft 정책상 MFA 또는 재로그인이 다시 요구될 수 있다.
- 로그인 상태 유지 checked는 MSAL cache와 Microsoft SSO 정책 범위 안에서만 동작한다.
- localStorage에 저장되는 auth artifact는 MSAL 관리 범위이며 앱 코드가 token을 직접 저장하지 않는다.
- 운영에서는 검수 사용자 전환을 활성화하지 않는다.
- dev user와 실계정은 자동 병합하지 않는다.
- 실제 Microsoft Graph Teams/메일 권한은 후속 TASK 범위다.
