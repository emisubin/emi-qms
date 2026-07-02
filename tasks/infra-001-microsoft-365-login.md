# TASK-INFRA-001: Microsoft 365 로그인 / 사용자·역할 운영 전환

## 목적

EMI 프로젝트 통합관리시스템의 운영 인증 기반을 Microsoft 365 로그인으로 전환한다.

Frontend는 MSAL React를 사용하고, Backend는 Microsoft.Identity.Web 기반 JWT Bearer 검증을 사용한다. Entra ID는 신원 확인만 담당하며, 부서/역할/권한은 앱 내부 DB와 서버 Policy에서 관리한다.

## 구현 범위

- Backend 인증 scheme 구성
  - Bearer token 우선
  - Bearer token이 없고 dev auth가 허용된 환경에서만 X-Dev-User 사용
  - invalid Bearer는 X-Dev-User로 fallback하지 않음
- Microsoft.Identity.Web JWT Bearer 설정
- Entra claim 기반 DB 사용자 조회 및 기존 `qms.*` claim 부착
- qms_users DB 확장
  - `entra_object_id`
  - `email`
  - `auth_provider`
- EntraId 최초 로그인 JIT 사용자 생성
- 승인 대기 정책
- bootstrap admin email 기반 system-administrator 자동 부여
- System Administrator 전용 최소 사용자 관리 화면
- System Administrator 전용 비운영 검수 사용자 전환
- 로그인 상태 유지 preference와 MSAL cacheLocation 분리
- Dev 인증과 기존 E2E 자산 보존
- `/api/me` 응답 확장
- roadmap Decision Log 및 추적 항목 갱신

## 제외 범위

- Teams/메일 알림 발송
- Microsoft Graph Mail.Send, Teams DM, channel message
- NotificationDispatcher 채널 handler
- 예정일 에스컬레이션
- Entra 그룹 claim 또는 App Role claim 기반 권한
- 권한 matrix 재설계
- 정식 관리자 기준정보 전체 화면
- 부서 master 관리
- 역할 master 관리
- 업무 이관 자동화
- dev user 담당 데이터를 실계정으로 자동 이관
- Pending List
- 검사/제조/물류/정산 기능

## 인증 구조

인증 모드는 `Authentication:Mode` 또는 `AUTH_MODE`로 구분한다.

- `Dev`
  - Development/Testing 환경에서만 허용
  - 기존 `X-Dev-User` header 기반 dev 인증 유지
  - 기존 E2E와 mock UI smoke는 Dev 모드로 실행
- `EntraId`
  - Microsoft 365 로그인
  - Frontend MSAL React가 access token 획득
  - Backend가 Bearer token을 Microsoft.Identity.Web JWT Bearer로 검증

Forward selector 기준:

1. `Authorization: Bearer`가 있으면 JWT Bearer 사용
2. Bearer가 없고 `X-Dev-User`가 있으면 Dev 인증 사용
3. 둘 다 없으면 현재 인증 모드 기본 scheme 사용

보안 조건:

- Bearer와 X-Dev-User가 동시에 있으면 Bearer 우선
- invalid Bearer는 X-Dev-User로 fallback하지 않음
- Development/Testing 외 환경에서 Dev 모드는 startup fail
- EntraId 모드에서 AzureAd 필수 설정 누락 시 startup fail

## DB 변경

신규 migration:

- `database/migrations/0015_microsoft_365_identity.sql`

변경:

- `qms_users.entra_object_id text null`
- `qms_users.email text null`
- `qms_users.auth_provider text not null default 'Dev'`
- 기존 user backfill: `auth_provider='Dev'`
- `entra_object_id` nullable unique
- `email`은 unique로 만들지 않음
- `department_id`는 EntraId 승인 대기 사용자를 위해 nullable 허용
- `auth_provider in ('Dev', 'EntraId')` check constraint

## JIT 사용자 생성 정책

EntraId 최초 로그인 시 `oid` claim 기준으로 `qms_users`에 사용자를 생성한다.

생성값:

- `entra_object_id = oid`
- `display_name = name`
- `email = preferred_username` 또는 `email`
- `auth_provider = 'EntraId'`
- `is_active = true`
- role 없음
- `department_id = null`

재로그인 시:

- `display_name` 변경분 동기화
- `email` 변경분 동기화
- `entra_object_id`는 변경하지 않음

정책:

- 이메일이 기존 dev user와 같아도 자동 병합하지 않음
- 식별자는 email이 아니라 `entra_object_id`
- 동시 최초 로그인은 `entra_object_id` unique/upsert로 방어

## 승인 대기 정책

별도 승인 상태 컬럼은 만들지 않는다.

승인 대기 판정:

```text
auth_provider = 'EntraId'
AND is_active = true
AND active role count = 0
```

승인 대기 해소:

- active role 1개 이상 부여
- `department_id`는 승인 대기 해소 조건이 아님

승인 대기 사용자가 접근 가능한 것:

- `/api/me`
- 본인 프로필 정보
- 승인 대기 안내 화면
- 로그아웃
- 정적 리소스

승인 대기 사용자가 접근 불가능한 것:

- 프로젝트
- 내 업무
- 알림
- 생산관리
- 구매
- 자재
- 관리자 화면
- 기타 업무 데이터 API

## dev auth 보존 정책

- 기존 dev user key와 `X-Dev-User` header는 유지한다.
- Dev 인증은 Development/Testing에서만 허용한다.
- Dev user와 EntraId user는 자동 병합하지 않는다.
- Dev user는 최소 사용자 관리 화면에서 읽기 전용으로 표시한다.
- 기존 full-stack E2E와 mock UI smoke는 Dev 모드로 유지한다.

## 검수 사용자 전환 정책

검수 사용자 전환은 로그인 우회가 아니다.

- actual user는 Microsoft 365로 로그인한 실제 EntraId 사용자다.
- effective user는 검수 목적으로 선택한 기존 dev persona다.
- system-administrator 역할을 가진 active EntraId 사용자만 사용할 수 있다.
- 승인 대기 또는 비활성 사용자는 사용할 수 없다.
- `AdminUserSwitch:Enabled=true`로 명시된 Development/Testing/UAT 환경에서만 허용한다.
- Production/Staging에서 활성화되면 startup fail한다.
- EntraId mode에서는 `X-Qms-Test-User` header를 사용한다.
- Dev mode의 `X-Dev-User`와 혼동하지 않는다.
- 허용 대상은 `dev-admin`, `dev-sales`, `dev-production`, `dev-procurement`, `dev-materials`, `dev-manufacturing`, `dev-quality`, `dev-logistics`, `dev-viewer`다.
- 실제 Entra 사용자 간 impersonation은 구현하지 않는다.
- 기존 Policy는 effective user의 `qms.*` claim을 기준으로 계속 동작한다.
- actual user 정보는 별도 claim과 `/api/me` 응답에 보존한다.

## 로그인 상태 유지 정책

- 로그인 상태 유지는 MSAL cache와 `acquireTokenSilent` 기준으로 제공한다.
- 기본값은 로그인 상태 유지 checked다.
- checked이면 MSAL `cacheLocation=localStorage`, unchecked이면 `sessionStorage`를 사용한다.
- 앱은 로그인 상태 유지 preference만 저장한다.
- access token, id token, refresh token, Authorization header 값을 앱 코드에서 직접 저장하지 않는다.
- MFA, 조건부 액세스, sign-in frequency는 Microsoft Entra 정책을 따른다.

## 최소 사용자 관리 화면 범위

System Administrator 전용 최소 화면을 추가한다.

포함:

- 사용자 목록
- Dev / EntraId 구분
- 승인 대기 표시
- 비활성 표시
- 부서 표시
- 역할 표시
- EntraId 사용자 부서 지정
- EntraId 사용자 역할 지정
- EntraId 사용자 활성/비활성 토글

제외:

- 권한 matrix 편집
- 부서 master 관리
- 역할 master 관리
- Item 관리
- 공휴일 관리
- 전체 감사 이력
- 업무 이관

보호 정책:

- 마지막 active System Administrator 비활성화 차단
- 마지막 active System Administrator의 system-administrator role 제거 차단
- 서버에서 강제
- 오류는 한글로 반환

## 테스트 계획

Backend:

- Dev 인증 기존 흐름
- Bearer 우선, invalid Bearer no fallback
- Entra 설정 fail-fast
- Development/Testing Dev 모드 Entra 설정 누락 허용
- Claims transformation/JIT 생성
- 승인 대기 permission 미부여
- role 부여 후 승인 대기 해소
- bootstrap admin
- Admin user switch 허용/거부 조건
- `/api/me` actual/effective user 응답
- 마지막 active System Administrator 보호
- migration 0015 적용

Frontend:

- Dev mode selector 표시
- EntraId mode selector 미표시
- Dev mode X-Dev-User header
- EntraId mode Authorization Bearer header
- EntraId admin user switch header
- 로그인 상태 유지 checkbox와 MSAL cacheLocation
- 승인 대기 화면
- 최소 사용자 관리 화면
- Dev user read-only

E2E:

- 기존 full-stack E2E 유지
- 기존 mock UI smoke 유지
- E2E는 Dev mode로 실행

## 수동 테스트 항목

- 실제 Microsoft 365 계정 로그인
- 실제 Entra token validation
- 실제 앱 등록 redirect URI 동작
- 실제 bootstrap admin email 최초 로그인
- 실제 승인 대기 화면
- 실제 역할 부여 후 권한 반영
- 실제 system-administrator 검수 사용자 전환
- 로그인 상태 유지 새로고침/브라우저 재시작 검수
- 마지막 System Administrator 보호 UI 흐름

## 실테넌트 필요로 자동 테스트하지 못하는 항목

- Microsoft 365 실계정 로그인
- 실제 Entra token 서명 검증
- 테넌트 redirect URI 설정 검증
- Graph 앱 등록/권한 승인
- 실제 bootstrap admin 이메일 매칭
