# TASK-UAT-001 Change 002 — Entra HTTPS 로그인 보안 게이트 복구

## 1. Task Identity Gate

- proposedTaskId: `TASK-UAT-001 Change 002`
- taskType: `SECURITY_HARDENING`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-UAT-001`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `PASS_REUSE`

## 2. 사용자 승인과 게시 경계

- 사용자는 2026-07-29에 확인된 로그인 전환 P1 전체 해결을 명시 승인했다.
- 사용자는 기존 `experiment/*` 워크트리에서 구현한 뒤 `main`으로 병합하는 방향을 선택했다.
- 2026-07-29 사용자는 향후 `main` merge 승인 기준을 자동 검증·사용자 검수 뒤 명시적 승인 `1회`로 변경했다. 정책 변경 메시지 자체는 이번 변경의 merge 실행 승인이 아니므로 현재 `mergeApproved: false`다.
- local experiment 구현·검증과 commit까지 포함한다. push, PR, `main` merge, 실제 외부 알림 발송과 Persistent UAT DB 초기화·삭제는 포함하지 않는다.

## 3. 확인된 P1

### `SEC-DEP-FRONTEND-20260729`

- Frontend dependency audit가 Critical 0, High 5를 보고했다.
- 영향 경로는 ESLint/TypeScript ESLint의 `brace-expansion`·`js-yaml`과 Vite toolchain의 `postcss`다.
- 고유 보안 권고는 4종이며 로그인 토큰을 다루는 Development frontend를 전환하기 전에 patched dependency로 갱신하고 전체 회귀를 통과해야 한다.

### `UAT-AUTH-CONFIG-20260729`

- local approved env에는 기본 tenant/client key만 있고 Frontend MSAL scope와 Backend audience key가 직접 채워지지 않았다.
- 기존 `.env.example`도 기본 tenant/client 두 값만 안내하므로 manual HTTPS UAT가 그 계약을 안전하게 확장하지 못한다.
- 표준 identifier URI와 delegated scope를 기본값으로 파생하되 명시적 환경값이 있으면 우선하고, tenant/client가 없거나 HTTPS가 아니면 fail-closed로 중단해야 한다.

### `UAT-AUTH-RUNTIME-DRIFT-20260729`

- HTTPS wrapper가 공통 startup script를 호출하지만 공통 script가 Backend와 Frontend 인증 모드를 항상 `Dev`로 덮어썼다.
- 현재 공식 frontend도 HTTPS가 아니라 HTTP로 실행돼 Change 001의 HTTPS-only Microsoft 로그인 계약과 다르다.
- HTTPS wrapper는 EntraId를 기본 인증 모드로 선택하고 공통 script는 `Dev`와 `EntraId`를 명시적으로 구분해야 한다.

### `UAT-AUTH-ACTUAL-CONFIG-20260729`

- 실험 워크트리의 기본 `.env` tenant/client는 예시 placeholder였고 Microsoft authorize endpoint가 `AADSTS900021`로 tenant를 거절했다.
- 후속 privacy-safe 재조사에서 대표 저장소의 ignored `.env.entra-local`에 실제 Backend·Frontend Entra 설정이, `.env.notify-local`에 Teams·메일 설정이 그대로 남아 있음을 확인했다.
- 처음 조사에서 정확한 `.env` 파일명만 검색해 `.env.*`를 누락한 것이 “실제 값 없음” 오판의 원인이다. 실제 설정은 Git merge로 삭제된 적이 없다.
- 세 local env는 실험 워크트리로 복사하고 mode `600`, Git ignored와 source byte equality를 확인했다. Client/tenant/credential 원문은 출력하거나 문서화하지 않았다.
- actual provider는 로드하지 않았으며 Microsoft 로그인과 인증된 API 호출을 검수하기 전까지 Entra 전환을 완료로 판정하지 않는다.

### `UAT-AUTH-REDIRECT-20260729`

- Chrome의 실제 Microsoft 오류 화면은 `AADSTS50011`을 반환했다.
- Frontend가 요청한 회신 주소는 `http://localhost:5174/auth/callback`이었고, Entra SPA에 등록된 주소는 `https://localhost:5174`였다.
- 대표 clone과 실험 worktree의 ignored local env 회신 주소를 등록값과 일치시켰고, 5174 local Entra 설정은 이 exact HTTPS origin 이외의 값이나 추가 path를 시작 전에 거절한다.

### `UAT-AUTH-DATABASE-20260729`

- 회신 주소 수정 뒤 access token의 서명과 claims 생성은 성공했으나 `/api/me`가 PostgreSQL `28P01`로 실패했다.
- 원인은 복구한 local env의 DB password와 현재 healthy PostgreSQL container의 실제 시작 password가 달랐기 때문이다.
- candidate Backend는 DB나 role password를 변경하지 않고, 이미 실행 중인 승인된 PostgreSQL container에서 현재 password를 값 비출력으로 해석해 사용한다.

### `UAT-AUTH-JIT-CONCURRENCY-20260729`

- DB 연결 복구 직후 `/api/me`, `/api/runtime-mode`, `/health/ready`가 동시에 같은 Entra 사용자를 JIT 생성하면서 `development_user_key` unique constraint 충돌이 1회 발생했다.
- 같은 Entra object ID의 JIT upsert를 transaction-scoped advisory lock으로 직렬화했다.
- 8개 동시 생성 회귀를 추가했고 Identity infrastructure 17/17에서 unique constraint failure 없이 같은 사용자 ID 하나만 반환함을 확인했다.

### `UAT-AUTH-FAIL-CLOSED-20260729`

- 익명 `/api/runtime-mode`가 환경·DB·worker·provider 준비 상태를 노출했고, 전역 `FallbackPolicy`가 없어 새 API에서 authorization metadata를 빠뜨리면 익명 접근이 열릴 수 있었다.
- 모든 metadata 미지정 endpoint에 인증된 운영 사용자 정책을 적용하는 `FallbackPolicy`를 추가했다.
- `/api/runtime-mode`는 Microsoft 365 인증과 운영 사용자 확인을 통과해야 조회할 수 있다. 일반 운영 화면도 `mutationAllowed`를 사용하므로 관리자 전용으로 제한하지는 않는다.
- 익명 health endpoint는 `/health/live`, `/health/ready` 두 개만 유지하고 ready 응답은 `ready`/`not_ready` 외 내부 migration·환경·worker·provider 원인을 반환하지 않는다.
- endpoint metadata 회귀 검사는 익명 허용 경로가 두 health endpoint뿐인지, 모든 `/api/*` endpoint가 authorization metadata를 갖는지 자동 확인한다.

## 4. 변경 범위

- Frontend audit High를 없애는 최소 dependency·lockfile 변경
- `dev-uat-start.sh`의 명시적 `Dev`/`EntraId` mode 선택과 fail-closed validation
- approved env file 경로를 값 노출 없이 선택할 수 있는 runtime 입력
- Entra tenant/client에서 기본 Backend audience와 Frontend API scope 파생
- HTTPS wrapper의 EntraId 기본값과 notification env opt-out 보존
- auth-only configuration check 경로와 shell/auth regression
- 예시 all-zero identifier 거절과 기존 UAT DB를 변경하지 않는 runtime handover option
- Entra SPA 등록값과 local redirect URI exact-match 검증
- 실행 중인 PostgreSQL과 candidate Backend의 read-only credential handover
- 동시 Entra JIT 사용자 생성의 unique constraint 경쟁 조건 제거
- 전역 fail-closed authorization fallback, runtime 상태 API 인증과 공개 health 응답 최소화
- 익명 endpoint allowlist와 전체 `/api/*` authorization metadata 회귀 검사
- Change 002 구현·검증 결과를 기존 implementation report와 SOP에 반영

## 5. 보존할 불변조건

- invalid Bearer token은 Dev identity로 fallback하지 않는다.
- EntraId mode에서 `X-Dev-User` 인증은 비활성이다.
- Development/Testing 이외 환경의 Dev 인증 fail-fast를 유지한다.
- actual tenant/client/object ID, token, credential, connection string과 private key를 출력·추적하지 않는다.
- 기존 미추적 사용자 screenshot을 수정·stage하지 않는다.
- experiment 사용자 검수 runtime 42983/41166과 DB를 P1 dependency/script 검증 때문에 변경하지 않는다.
- 실제 Teams/Mail/Teams Activity provider는 활성화하거나 발송하지 않는다.
- Persistent UAT DB는 drop, truncate, reset하지 않는다.

## 6. 검증 계획

- `pnpm audit` Critical/High 0
- Frontend lint, typecheck, unit, build
- Backend vulnerable package audit와 auth/authorization tests
- shell syntax와 auth configuration check의 Dev 성공, Entra HTTPS 성공, Entra HTTP 거절, 설정 누락 거절
- isolated Entra HTTPS candidate에서 root, health proxy, 로그인 화면, 익명 401과 invalid Bearer 401
- 공식 5174/5081 handover 전후 DB aggregate·PostgreSQL restart·provider 상태 불변
- trusted HTTPS 5174 성공, HTTP 5174 실패, 로그인 redirect action 확인
- Git diff·allowlist·secret/PII·미추적 사용자 파일 재확인

## 7. 구현 및 검증 결과

- Frontend audit: Critical 0, High 5에서 전체 severity 0으로 보정했다.
- Frontend: lint 통과(error 0, 기존 warning 1), typecheck 통과, unit 142/142, build 통과했다.
- Backend NuGet vulnerable package audit: 0건이다.
- Auth configuration self-test: Dev 성공, Entra HTTPS 성공, Entra HTTP 거절, 누락 설정 거절, placeholder 거절, 잘못된 mode 거절을 모두 통과했다.
- Auth configuration self-test는 기존 local key 호환과 identifier 불일치 차단까지 포함해 8/8 통과했다.
- 격리 Entra candidate와 5174 handover에서 root/health와 익명·invalid bearer·Dev header 차단을 확인했다.
- 첫 handover는 기본 `.env` placeholder 때문에 `AADSTS900021`에서 중단됐다. 이후 기존 `.env.entra-local`을 복구하고 기존 `AzureAd__*`·`VITE_AZURE_*` key를 안전하게 해석하는 startup 계약을 추가했다.
- 실제 Chrome 재현에서 `AADSTS50011` 회신 주소 불일치와 DB password drift를 차례로 확인해 수정했다.
- 최종 Chrome 검수에서 Microsoft token 검증, `/api/me`, 홈과 주요 초기 조회 API가 모두 200이었고 인증 오류 없이 Dashboard가 표시됐다.
- Entra JIT 동시성 회귀 2/2와 Identity infrastructure 전체 17/17을 isolated PostgreSQL에서 통과했다.
- 보안 권고 P2 보완으로 전역 `FallbackPolicy`, 인증된 `/api/runtime-mode`, 최소 공개 health 응답을 적용했다.
- P2 대상 authorization·review-safe·worker 회귀 65/65와 Frontend unit 142/142, typecheck, build를 통과했다.
- 격리 PostgreSQL을 사용한 Backend 전체 회귀 435/435를 통과했고 테스트 DB와 container를 종료 시 삭제했다.
- experiment 검수 runtime 42983/41166은 Dev 인증으로 다시 열어 사용자 검수 경로를 보존했다.
- 실제 provider 발송, migration, seed, data reset은 실행하지 않았다.

현재 상태는 `실제 Microsoft 로그인·인증 API·Dashboard 복구 완료 / 사용자 화면 최종 확인 가능`이다. Commit, push, PR과 `main` merge는 별도 요청·승인 전까지 실행하지 않는다.

## 8. Rollback

- dependency 문제가 있으면 Change 002 dependency 파일만 되돌리고 기존 lockfile 기준으로 재설치한다.
- Entra runtime 시작이 실패하면 공식 Entra runtime을 검수 완료로 표시하지 않고 experiment Dev 검수 runtime 42983/41166을 사용한다.
- DB migration·data rollback은 수행하지 않는다. Change 002는 schema/data 변경을 만들지 않는다.
