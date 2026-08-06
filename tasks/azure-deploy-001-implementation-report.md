# TASK-AZURE-DEPLOY-001 Implementation Report — 20일 Azure 시범 배포

## Change 017 — 운영 외부 알림 Worker 활성화

### 승인 범위와 실행 결과

- 사용자는 기존 대기 알림이 한꺼번에 발송될 수 있음을 확인하고 Notification Dispatcher 활성화를 명시적으로 승인했다.
- worker만 켜면 Mail·Teams Activity가 `Disabled/DryRunSent`로 종결되므로 기존 Bicep의 `enableExternalNotifications` 계약과 동일하게 Dispatcher·Teams Activity·Mail 다섯 actual flag를 함께 활성화했다.
- 활성화 전 Backend 후보 `1`, 최대 replica `1`, Teams/Gmail Key Vault binding·값, Gmail SMTP `587/StartTls`, 현재 disabled/dry-run 기준선을 확인했다.
- 새 Backend revision은 `Ready`이며 실제 외부 알림 설정 readback이 exact다.
- 활성화 뒤 확인된 최신 수동 Teams Activity `6`건과 Mail `3`건은 모두 attempt `1`, `Sent`다.
- Open delivery는 `Pending 0`, `Processing 0`, `Failed 0`이다. 과거 관리자 `Dismissed` Mail `2`건은 worker claim 대상이 아니므로 기존 제외 상태를 보존했다.
- Notification delivery status·attempt 이외 DB schema·migration·업무 data와 Frontend·ClamAV·Front Door·Entra·Teams Channel webhook은 변경하지 않았다.

### 검증 결과

| 검증 | 결과 |
| --- | --- |
| Task Identity·Roadmap Gate | `PASS_REUSE`, `UAT_RUNTIME` |
| activation script syntax·ShellCheck·diff check | `PASS` |
| confirmation guard | 누락 시 exit `64`, mutation `0` |
| Provider preflight | `PASS` |
| Runtime mutation | 다섯 actual flag exact |
| Backend latest revision | `Ready`, max replica `1` |
| 최신 수동 Teams Activity | `6/6 Sent`, 각 attempt `1` |
| 최신 수동 Mail | `3/3 Sent`, 각 attempt `1` |
| Open delivery | Pending/Processing/Failed 각 `0` |
| 새 synthetic 알림·수동 DB 보정 | `0` |

### Provider Gate 판정

- Teams Activity provider 처리: `PASS`
- Gmail SMTP provider 처리: `PASS`
- Worker·backlog 처리: `PASS`
- 사용자 Teams client·메일함 수신 확인: `PASS` (2026-08-06 사용자 확인)
- Teams SSO·새 manifest: 별도 후속 `NEW_FEATURE`

Source와 문서 변경은 local branch에만 있으며 commit·push·PR·merge는 별도 사용자 승인 전까지 수행하지 않는다.

## Change 016 — 운영 Teams Activity 1건 Provider Smoke

### 승인 범위와 실행 결과

- 사용자는 Teams 앱 설치 완료 뒤 알림 테스트를 Teams SSO·새 manifest보다 먼저 수행하도록 승인했다.
- 운영 Notification Dispatcher를 켜면 기존 notification 원본에서 다수 delivery가 생성될 수 있으므로 활성화하지 않았다. 운영 Backend에 이미 주입된 동일 Teams client credential·manifest·public link 설정을 읽어 현재 Azure 로그인 bootstrap 관리자 1명에게 합성 `generalNotification`을 직접 1회 전송했다.
- 합성 payload는 실제 사용자 이름·프로젝트·Pending·업무 원문을 포함하지 않는다. Gmail·TeamsChannel과 실제 업무 event는 생성하지 않았다.
- Microsoft Graph는 실제 요청을 HTTP `204`로 수락했다. Provider 상태는 `SENT`, 안정 코드는 `TEAMS_ACTIVITY_GRAPH_ACCEPTED`다.
- 사용자의 최초 client 미표시 보고 뒤 재발송하지 않고 대상을 재검증했다. 사용자 제공 계정과 실제 수신 object는 같은 Entra 사용자였다.
- 앱 자격증명의 개인 설치 목록 조회는 최소 권한 경계 때문에 Graph `403`이었고, 추가 권한이나 관리자 동의는 적용하지 않았다.
- 기존 로그인 상태의 Teams web Activity Feed에서는 합성 알림의 exact 제목과 preview가 모두 표시됐다. Teams server 렌더링은 완료됐으며 desktop Teams 표시만 사용자 새로고침 확인으로 남긴다.

### 안전 상태와 검증

| 검증 | 결과 |
| --- | --- |
| Task Identity·Roadmap Gate | `PASS_REUSE`, `UAT_RUNTIME` |
| Shell syntax·ShellCheck·Git diff check | `PASS` |
| 명시 actual flag 누락 negative path | exit `64`, provider 호출 `0` |
| 대상·payload | bootstrap 관리자 `1`, synthetic, 업무 data `0` |
| Microsoft Graph actual 호출 | `1`, HTTP `204`, `SENT` |
| 대상 Entra 사용자 일치 | `true` |
| 개인 설치 목록 read-only 진단 | Graph `403`, 권한 변경 `0` |
| Teams web Activity Feed | exact 제목·preview 표시 `PASS` |
| Runtime 설정 변경 | `0` |
| Dispatcher / Mail | disabled 유지 / disabled 유지 |
| DB·migration·업무 row·container revision | 변경 `0` |

첫 도구 실행은 macOS 기본 Bash가 소문자 변환 확장 문법을 지원하지 않아 provider 호출 전에 중단됐다. POSIX 호환 `tr` 방식으로 보정한 뒤 syntax·ShellCheck·negative guard를 다시 통과했고, actual Graph 호출은 이후 정확히 1회만 수행했다.

### Provider Gate 판정

- Teams provider credential·Graph 권한·공개 manifest activity type·설치 사용자 대상 actual API 수락: `PASS`
- Teams web 실제 표시: `PASS`
- Desktop Teams 표시: `PENDING_USER_REFRESH_CONFIRMATION`
- Gmail actual smoke: 이번 사용자 요청 범위 밖, `OPEN`
- 운영 자동 event별 검수와 Teams SSO·새 manifest: 별도 후속 범위

Source와 문서 변경은 local branch에만 있으며 commit·push·PR·merge는 별도 사용자 승인 전까지 수행하지 않는다.

## Change 015 — 공개 Frontend Entra 사전 인증

### 승인 범위와 구현 상태

- 사용자는 별도 시험환경·Teams manifest 재승인 없이 운영 `pms`에 사전 인증을 직접 적용하도록 승인했다.
- Teams tab이 server-directed Entra redirect와 호환되지 않으면 tab을 사용하지 않고 Teams Activity 알림 전용으로 유지한다.
- Frontend Container Apps authConfig는 `/health/live`만 제외하고 `RedirectToLoginPage`, single-tenant Entra, HTTPS와 Front Door Standard proxy convention을 사용한다.
- 사전 인증용 client secret은 별도 Key Vault secret과 Frontend identity의 secret-scope RBAC로만 주입한다.
- Backend bearer·역할 권한, DB·migration·image·actual notification provider는 변경하지 않는다.

### 현재 상태

- Source 구현: `COMPLETE_LOCAL`
- 자동 검증: `PASS`
- Azure 운영 적용: `COMPLETE`
- 사용자 로그인 검수: `PASS`
- Git 게시: `PENDING_USER_APPROVAL`

### 자동·운영 검증

| 검증 | 결과 |
| --- | --- |
| Bicep compile·ARM JSON 동등성·Azure artifact validator | `PASS` |
| Public deployment security 집중 test | `42/42 PASS` |
| Backend Release build | `PASS`, warning `0`, error `0` |
| Frontend lint·typecheck·unit·production build | lint error `0`·기존 warning `1`, typecheck `PASS`, `175/175 PASS`, build `PASS` |
| Backend 전체 test | 로컬에서 10분간 결과 없이 장시간 대기해 수동 중단. 실패 출력과 orphan process는 `0`; 직접 영향 test `42/42`와 최종 main CI 기준선은 통과 상태 |
| Changed-file allowlist·PII·secret·generated artifact | `PASS`, 15개 허용 파일, 실제 식별자·email·credential·migration·Frontend source diff `0` |
| Entra·Key Vault·Frontend secret binding·secret-scope RBAC | 각 대상 `1`, single-tenant |
| Easy Auth readback | enabled / `RedirectToLoginPage` / `Standard` / 제외 경로 `1` |
| 익명 비브라우저 root·asset·manifest·API / health | `401 / 401 / 401 / 401 / 200` |
| 익명 브라우저 | Easy Auth 인증 화면, PMS root·bundle reference 없음 |
| 실제 허용 계정 | PMS root·asset load, 프로젝트·관리자 메뉴 접근 성공 |
| Backend·DB·migration·image·actual provider | 변경 없음 |

Rollback 명령과 auth-only 복구 경계는 확인했다. 정상 보안 게이트를 다시 여는 실제 rollback은 수행하지 않았다.

### Validation Matrix

| 검증 | 적용 여부 | 결과 | 근거/미실행 이유 |
| --- | --- | --- | --- |
| 최소 검증 | 적용 | `PASS` | instruction chain·Task gate, diff check, Bicep/ARM, secret·PII·allowlist 확인 |
| 영향 회귀 | 적용 | `PASS` | public deployment security `42/42`, 실제 익명·로그인 runtime smoke |
| 전체 pipeline | 부분 적용 | `PENDING_PUBLISH_GATE` | Frontend 전체와 Backend build는 통과. Backend 전체 test는 로컬 장시간 대기로 미완료했으며 commit·PR 전 재실행 대상 |
| 사용자 검수 | 적용 | `PASS` | 실제 허용 계정의 PMS root·프로젝트·관리자 메뉴 접근 확인 |

### Finding

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `ANON-FRONTEND-BUNDLE-001` | P2 | `RESOLVED_RUNTIME` | app-level 로그인 전 shell·bundle이 익명 요청에 제공됐다. | 운영 Easy Auth를 적용하고 익명 비브라우저 `401`, 브라우저 인증 화면의 PMS root·bundle 부재와 실제 로그인 성공으로 종결했다. |
| `TEAMS-PREAUTH-COMPAT-001` | Policy | `RISK_ACCEPTED_BY_USER` | Teams iframe에서 Entra redirect가 실패할 수 있다. | 실패 시 Teams tab을 중단하고 Activity 알림만 사용한다. |

Open P0/P1/P2 Finding은 `0`이다. Source는 local branch에만 있으며 commit·push·PR·merge는 별도 사용자 승인 전까지 수행하지 않는다.

## Change 014 — 공개 API Host allowlist와 관리자 로그인 Gate 종결

### 실행과 현재 상태

- Change 013 branch를 PR #72로 원격 `main`에 squash merge했고, 최종 main CI와 Backend·Frontend immutable image 게시 workflow가 성공했다.
- 최신 Frontend image를 Azure revision에 적용해 provisioning `Succeeded`, running, latest revision ready와 traffic 100%를 확인했다.
- Change 013의 Nginx route 보정 뒤 공개 API가 `400`인 원인은 Backend Host filtering이었다. Frontend proxy의 HTTP Host는 Backend 내부 FQDN인데 `AllowedHosts`는 공개 hostname만 허용하고 있었다.
- Backend runtime은 공개 hostname과 exact 내부 Backend FQDN 두 개만 허용하도록 보정했다. readiness `200`, 익명 `/api/me`·`/api/runtime-mode` `401`, 공개 root·PWA `200`, direct origin 차단과 TLS hostname 일치를 확인했다.
- 같은 exact-host 계약을 managed environment `defaultDomain`에서 결정적으로 구성하도록 Bicep·ARM JSON·정적 test에 반영했다. wildcard와 임의 hostname은 허용하지 않는다.
- 현재 Entra 계정이 bootstrap 관리자 목록에 포함되고 실제 관리자 메뉴와 사용자 관리 화면에 접근함을 확인했다. 목록의 순서는 권한 우선순위가 아니므로 secret 재정렬은 하지 않았다.
- Backend·Frontend·ClamAV는 모두 running·ready다. Teams Activity와 Gmail actual provider는 disabled·dry-run을 유지했다. DB·migration은 변경하지 않았다.

### 자동·운영 검증

| 검증 | 결과 |
| --- | --- |
| Change 013 PR CI와 최종 main CI | `PASS` |
| 최종 main Backend·Frontend immutable image 게시 | `PASS` |
| Frontend revision 교체와 Backend exact-host revision | `PASS`, latest ready / traffic 100% |
| 공개 HTTP redirect·HTTPS root·PWA·readiness | `307 / 200 / 200 / 200` |
| 익명 보호 API | `/api/me`·`/api/runtime-mode` `401` |
| Direct origin 보호 | Frontend 업무 route `403`, Backend direct ingress 차단 |
| Managed TLS hostname | `PASS` |
| 실제 비상 관리자 로그인·관리자 전용 화면 접근 | `PASS` |

### Finding과 resolution

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `AZURE-BACKEND-HOST-FILTER-001` | P1 | `RESOLVED_RUNTIME` | 내부 route 도달 뒤 HTTP Host가 Backend 내부 FQDN인데 `AllowedHosts`는 공개 hostname만 허용해 API와 readiness가 `400`이었다. | public host와 exact internal Backend host 두 개만 허용해 public API를 복구하고 Bicep·ARM JSON·test에 고정했다. |
| `CI-FULLSTACK-FLAKE-001` | P2 | `RESOLVED` | Change 013 PR·main CI의 서로 다른 실행에서 기존 E2E 한 건씩이 시간 제한으로 간헐 실패했다. | 실패 spec 집중 실행이 각각 성공했고 최종 전체 main CI가 통과했다. 범위 밖 test 수정은 하지 않았다. |
| `PRIVACY-EVIDENCE-DOM-001` | P2 | `RESOLVED` | 실제 로그인 화면 확인 중 일시적 도구 출력에 표시 이름이 포함됐으나 tracked·staged artifact에는 기록되지 않았다. | 원문을 폐기하고 역할·메뉴·접근 성공 여부만 fixed boolean projection으로 남겼다. |

Open P0/P1/P2 Finding은 `0`이다. 공개 API·로그인 Gate는 완료됐으며 다음 운영 Gate는 Teams 승인·설치와 Teams/Gmail actual provider 검수다.

## Change 013 — 공개 Front Door 전환과 API origin routing 보정

### 실행과 현재 상태

- Front Door domain validation·deployment·provisioning `Succeeded`, managed certificate·TLS 1.2와 hostname 일치를 확인했다.
- 공개 HTTP→HTTPS redirect, HTTPS root·PWA manifest·icon `200`, Frontend direct origin 업무 route `403`, 보안 header를 확인했다.
- Backend·Frontend·ClamAV는 `3/3 Running`, provisioning `Succeeded`다. 외부 Teams Activity·Mail은 disabled·dry-run을 유지했다.
- Entra API·SPA client 분리, 공개 SPA redirect 등록, `access_as_user` delegated scope를 확인했다.
- 공개 `/api/me`, `/api/runtime-mode`, `/health/ready`가 모두 `404`여서 Frontend Nginx의 내부 Backend routing host 오류를 확인했다.
- Nginx HTTP `Host`를 Backend ingress FQDN으로, 원래 공개 주소는 `X-Forwarded-Host`로 분리했다. Azure artifact compile·static validation, 관련 보안 test와 실제 운영 build argument를 사용한 Frontend image local build가 통과했다.

### Finding과 다음 Gate

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `AZURE-FRONTEND-BACKEND-HOST-001` | P1 | `RESOLVED_LOCAL` | Frontend proxy가 공개 hostname을 내부 HTTP routing host로 사용해 Backend API가 모두 `404`였다. 정적 화면은 열리지만 로그인·업무 기능을 사용할 수 없었다. | Host/X-Forwarded-Host 분리와 artifact·security test·운영형 image build로 source 결함을 해소했다. immutable Frontend image 교체와 API smoke는 runtime Gate로 계속한다. |
| `AZURE-AFD-DOMAIN-VALIDATION-001` | External gate | `RESOLVED` | DNS exact match 뒤 Azure validation 처리가 Pending이었다. | domain·deployment·provisioning 완료, managed certificate·TLS hostname 검증으로 종결했다. |

Open P0/P1/P2 Finding은 `0`이다. 새 Frontend image 적용과 API smoke 전에는 공개 배포 완료 또는 provider 활성화를 주장하지 않는다.

## Change 012 — Front Door 기존 토큰 재검증

### 실행과 현재 상태

- Azure validation token과 가비아 TXT, Front Door endpoint와 CNAME이 각각 `1/1 exact match`임을 fixed boolean projection으로 확인했다.
- Route는 custom domain 1개 연결, enabled, HTTPS only, provisioning `Succeeded`다.
- 기존 token을 유지하는 empty PATCH 재검증을 실행했다. token·DNS·application revision·external notification은 변경하지 않았다.
- authoritative 재조회에서 domain validation은 `Pending`, managed certificate·route deployment는 `NotStarted`다. Approved 전에는 TLS·public smoke를 진행하지 않는다.

### Finding과 재개 조건

| ID | 등급 | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `AZURE-AFD-ENDPOINT-PROJECTION-001` | P2 | `RESOLVED` | 존재를 확인하지 않은 endpoint 이름으로 read해 Azure 오류에 subscription identifier가 표시됐다. tracked artifact에는 남지 않았다. | 원문 폐기, endpoint list fixed projection 뒤 정확한 resource만 조회하도록 보정 |
| `AZURE-AFD-WAIT-CONDITION-001` | P2 | `RESOLVED` | CLI wait의 빈 종료를 Approved로 오판할 수 있었다. | 즉시 direct show로 Pending을 확인해 TLS mutation 전 중단했고, exact enum·deployment 상태 동시 판정으로 고정 |
| `AZURE-AFD-DOMAIN-VALIDATION-001` | External gate | `RESOLVED_CHANGE_013` | DNS exact match와 재검증 요청 뒤 Azure validation service가 Pending을 유지했다. | 2026-08-06 domain·managed TLS 완료와 hostname 검증을 확인했다. |

재개 조건은 domain state `Approved`다. 이후 managed TLS·route deployment, Front Door `200`, 직접 origin `403`과 인증서 hostname을 검증한다.

## Change 011 — migration 0069와 최신 앱 교체

### 승인 범위와 실행 결과

- 사용자 승인에 따라 최종 main Backend image로 migration job을 교체해 실행하고 migration `0069`, expected/applied `69/69`, ledger `Exact`를 확인했다.
- Backend와 Frontend를 최종 main digest로 교체했다. 두 workload 모두 latest revision과 latest ready revision이 일치하고 `Healthy`, replica `1`이다.
- ClamAV는 변경하지 않았고 세 workload는 `3/3 Running`, single revision 100% 상태다.
- Backend의 외부 Teams Activity는 `Enabled=false`, `DryRun=true`를 유지했고 `PersonalChannelStrategy=TeamsActivity`를 확인했다.
- Front Door domain `Pending`, deployment `NotStarted`, public traffic·actual provider 비활성 상태를 보존했다.

### Finding과 resolution

| ID | 등급 | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `AZURE-TEAMS-STRATEGY-CONFIG-001` | P2 | `RESOLVED_RUNTIME` | 첫 image-only Backend update가 이전 runtime env를 보존해 새 개인 Activity 전략 값이 없었다. 외부 알림은 disabled·dry-run이라 실제 발송 영향은 없었다. | 누락된 값만 추가하고 최종 Backend revision의 Healthy·ready와 세 알림 설정을 재확인했다. |
| `PRIVACY-RUNTIME-LOG-PROJECTION-002` | P2 | `RESOLVED` | streaming job log 명령의 projection이 적용되지 않아 임시 도구 출력에 system log와 execution alias가 표시됐다. PII·secret·업무 원문과 tracked artifact는 없었다. | 원문을 폐기하고 이후 증빙을 status·count·boolean으로 제한했다. raw streaming log 명령은 후속 검증에서 사용하지 않는다. |

Open P0/P1/P2는 `0`이다. Direct origin HTTP smoke는 실행 환경 정책상 재실행하지 않았고 기존 origin 보호 설정과 Front Door·public traffic 비활성 상태를 유지했다.

### Rollback과 다음 Gate

- 기존 Backend·Frontend digest는 rollback 기준으로 보존했다. application 문제 시 직전 digest로 다시 교체하며 additive migration `0069`는 되돌리지 않고 forward-fix한다.
- 다음 Gate는 Front Door validation·managed TLS, Entra 운영 주소, Teams 관리자 승인·설치와 Teams/Gmail actual provider 검수다.

## Change 010 — 배포 상태 동기화와 최신 main 이미지 게시

### 실행 전 실제 상태

- Change 009 PR #70은 원격 `main`에 merge됐고 main CI 3종이 성공했다.
- Azure Backend·Frontend ACR image와 serving revision은 Change 009 이전 `main`을 사용하고 있으며 DB는 migration `0068 Exact`다.
- Change 009를 포함한 main SHA의 Backend·Frontend ACR tag는 각각 0개다.
- DNS validation TXT는 가비아 권한 네임서버 `3/3`과 공용 resolver `4/4`에서 Azure 요구값과 일치하고, CNAME은 Azure 자체 진단에서 `validated=true`다.
- Front Door는 domain validation `Pending`, managed TLS·route deployment `NotStarted`다. 기존 token을 유지한 재검증 요청은 접수됐고 DNS record는 변경하지 않는다.
- Teams `1.0.4`는 사용자가 관리자 승인 요청을 제출했으며 승인·조직 catalog·설치·actual Activity는 대기다.

### 승인 범위와 실행 순서

1. Roadmap·Implementation report·SOP·사용자 checklist를 실제 merge·Azure·DNS·Teams 상태에 맞춘다.
2. 문서 전용 검증 뒤 전용 branch에서 commit·push·PR·merge한다.
3. 문서 merge로 만들어진 최종 main SHA를 기존 `Azure Pilot Images (Manual)` workflow에 전달한다.
4. Backend·Frontend image 2개의 workflow 성공, immutable SHA tag와 digest 형식을 확인한다.
5. migration `0069`, Container Apps revision, Front Door·traffic과 actual provider는 다음 Gate로 보존한다.

### 검증 계획과 게시 경계

| 검증 | 적용 | 기준 |
| --- | --- | --- |
| 문서 최소 검증 | 적용 | diff check, Markdown local link·heading, secret/PII, allowlist |
| 코드·migration·dependency diff | 적용 | `0`이어야 함 |
| PR CI | 적용 | 최신 head 기준 표준 CI 성공 |
| Image workflow | 문서 merge 뒤 적용 | 최종 main SHA, Backend·Frontend 2개, mutable latest 없음 |
| Azure revision·DB·traffic | 제외 | 사용자 요청 범위 밖, 다음 Gate |

### 다음 Gate

최신 main image 게시 뒤 migration job을 새 Backend digest로 교체해 `0069 Exact`를 확인하고, 이후 Backend·Frontend revision을 교체한다. Front Door `Approved`·managed TLS 배포, Entra 운영 주소, Teams 관리자 승인·설치와 event별 actual provider 검수는 그 다음 순서다.

## Change 009 — 공개 Teams 알림 유형과 자동 발송 연결

### 확정한 기존 수신자·발송 시점

| 업무 event | Teams 수신자 | 발송 시점 |
| --- | --- | --- |
| 프로젝트 생성 | 프로젝트 생성 참조 대상인 8개 운영 부서의 활성 사용자 | 생성 workflow transaction 성공 직후 |
| 프로젝트 납기 변경 | 활성 영업 담당자와 현재 프로젝트 담당자 | 납기 변경 transaction 성공 직후 |
| 프로젝트 상태 변경 | 활성 영업 담당자와 현재 프로젝트 담당자 | 상태 변경 transaction 성공 직후 |
| 일반 단계 업무 생성 | 생성된 work item의 정담당자 | work item 생성 직후 |
| 긴급·차단 | 기존 인앱 수신자 개인과 기존 통합 Teams 채널 | 차단 또는 `Critical` 알림 생성 직후 |
| 재검사 요청 | 기존 재검사 work item 알림 수신자 | 재검사 요청 transaction 성공 직후 |
| 예정일 임박·초과 | 기존 L0~L2 개인 수신자 | 기존 에스컬레이션 event 발생 시점 |
| 프로젝트 완료 | 활성 영업 담당자 | 영업 정산 완료 transaction 성공 직후 |

### 실제 구현

1. Teams v1.19 manifest의 Activity type을 `projectCreated`, `projectDeliveryDateChanged`, `projectStatusChanged`, `workItemAssigned`, `urgentPending`, `reinspectionRequested`, `deadlineApproaching`, `deadlineOverdue`, `projectCompleted`, `generalNotification` 10개로 고정했다.
2. Teams로 보내지 않는 일일 요약 type을 제거하고 수동 일반 알림은 `generalNotification`으로 통합했다.
3. 프로젝트 생성·납기 변경·상태 변경·일반 업무 배정·긴급/차단·재검사·프로젝트 완료가 기존 `notification_recipients`를 재사용해 중복 없는 개인 Activity delivery를 만든다. 긴급/차단의 기존 통합 채널 delivery는 유지한다.
4. 프로젝트 납기·상태 변경은 업무 transaction 안에서 인앱 원본과 기존 담당자 수신자를 함께 기록한다. provider 실패가 업무 transaction을 되돌리지 않는 outbox 계약은 유지한다.
5. 제목 문자열 추정을 제거하기 위해 프로젝트 생성·납기 변경·상태 변경·재검사·완료의 `source_kind`를 명시했다. migration `0069`는 기존 constraint 허용값에 이 5개 값만 추가하며 table·column·기존 row를 변경하지 않는다.
6. 공개 Azure Backend의 일반 개인 알림 채널을 `TeamsActivity`로 설정했다. 에스컬레이션 worker 활성화, L0~L3 조건·기간·추가 수신자 정책은 변경하지 않았다.
7. 기존 공개 manifest identity·web resource·운영 hostname과 브랜드 icon을 보존하고 version만 `1.0.4`로 증가한 handoff ZIP을 생성했다.

### 자동 검증

| 검증 | 결과 |
| --- | --- |
| Backend build | `PASS`, 경고 0 / 오류 0 |
| 최종 전체 Backend 회귀 | `485/485 PASS` |
| Notification delivery 전체 | `98/98 PASS` |
| 프로젝트 생성·납기·상태·재검사·migration 집중 검증 | `8/8 PASS` |
| Teams package 정상·negative test | `2/2 PASS` |
| `1.0.4` package entry·version·10개 exact type·`packageName`/`dailyDigest` 부재 | `PASS` |
| Microsoft Activity type 길이 제한·예약값 검사 | `PASS`, 최대 type 26 / template 48 |
| Azure artifact 정적 검증 | `PASS` |
| 신규·수정 C# 영역 formatter와 `git diff --check` | `PASS`; 기존 파일의 비변경 formatter debt는 범위 밖으로 보존 |

### 산출물과 다음 Gate

- 운영 handoff ZIP: `/Users/parksubin/Downloads/emi-qms-teams-1.0.4-public-notifications.zip`
- Azure 적용 순서: migration `0069` 적용 → Backend 새 revision 교체 → Teams Admin Center에 `1.0.4` update → 앱 설치·동의 상태 확인 → event별 actual Activity smoke.
- Teams Admin Center의 `1.0.4` 승인 요청은 사용자가 제출했다. 관리자 승인 완료·조직 catalog 표시·앱 설치·Graph actual 발송 검수는 대기다.
- 실제 Azure migration·revision과 Graph actual 발송은 이 local 변경에서 실행하지 않았다.
- 상세 에스컬레이션 정책은 후속 기획으로 남기며, 이번 변경은 기존 수신자·event 시점과 선언된 Activity type만 보존한다.

## Change 008 — Teams v1.19 manifest schema 보정

### 확인된 Finding

| ID | 등급 | 상태 | 원인 | 해결 |
| --- | --- | --- | --- | --- |
| `TEAMS-MANIFEST-SCHEMA-001` | P1 | `RESOLVED_LOCAL` | Change 006 template이 v1.19 schema에 정의되지 않은 최상위 `packageName`을 포함해 Teams Admin Center의 `additionalProperties: false` 검증에서 거부됨 | `packageName` 제거, package 회귀 test 추가, 공식 v1.19 JSON Schema 전체 검증을 통과한 `1.0.3` handoff ZIP 생성 |

### 실제 구현

1. Teams v1.19 template에서 미정의 최상위 `packageName`을 제거했다.
2. package test가 생성된 manifest의 `packageName` 부재를 확인해 동일 회귀를 차단한다.
3. 기존 `1.0.2` package의 manifest ID·Activity client·web resource·운영 hostname을 보존하고 version만 `1.0.3`으로 올린 별도 ZIP을 생성했다.
4. resource-specific Activity 권한, activity type 6개, icon과 package entry는 변경하지 않았다.

### 자동 검증

| 검증 | 결과 |
| --- | --- |
| Teams package 정상·negative test | `2/2 PASS` |
| Microsoft Teams v1.19 공식 JSON Schema 전체 검증 | `PASS` |
| 미정의·필수 누락 최상위 속성 | `0/0` |
| package entry | `3/3 PASS` |
| manifest·Activity identity와 운영 hostname 보존 | `4/4 PASS` |
| Activity permission·type count | `PASS` / `6` |
| package icon과 canonical asset byte equality | `2/2 PASS` |

### 미실행과 다음 Gate

- 실제 Teams Admin Center의 `1.0.3` 재등록과 조직 catalog 표시 확인은 사용자 검수 대기다.
- actual Activity Feed·Gmail 발송은 provider Gate 전까지 비활성으로 유지한다.
- Front Door domain validation·managed TLS는 새 DNS TXT의 외부 반영 뒤 별도 runtime Gate에서 계속한다.

## Change 007 — 문서 상태 동기화와 최신 main 이미지 게시

### 실행 전 상태와 승인 경계

- Change 006 PR #68: squash merge 완료
- 원격 `main`: `496b88b793c0514fefdc3ee7a09c252201b8eda9`, PR·main CI 성공
- ACR: Backend·Frontend repository 2개 / 현재 `main` SHA tag 0개
- Azure Container Apps: 기존 Change 005 revision 3/3 ready / 현재 `main` image 적용 0개
- manual job: bootstrap·migration 마지막 실행 성공 / 현재 `main` image 적용 0개
- Azure DB: `67 Exact` / Repository expected `0068`
- Front Door: DNS CNAME·TXT와 validation token 일치 / domain validation `Pending` / route·TLS deployment `NotStarted`
- 사용자 승인: 문서 동기화 PR을 `main`에 병합한 뒤 그 최신 full SHA로 Backend·Frontend ACR image 게시
- 제외: Container Apps revision, migration `0068`, Edge·TLS, public traffic과 actual provider 변경

문서 병합으로 만들어지는 최신 `main` SHA를 GitHub `Azure Pilot Images (Manual)` workflow의 source로 고정한다. Image 게시 결과는 workflow 성공 여부, digest 형식과 ACR의 두 SHA tag 존재 여부만 privacy-safe projection으로 확인한다.

## Change 006 — Teams·PWA 브랜드 자산과 통합 main handover

### 현재 상태

- 기준 원격 main: `496b88b793c0514fefdc3ee7a09c252201b8eda9` — Change 006 PR #68 squash merge·main CI 완료
- 사용자 제공 EMI PNG: canonical brand source로 보존
- Teams package: 신규 브랜드 icon과 유효한 manifest를 포함한 `1.0.2` handoff artifact 생성 완료
- PWA: web manifest·192·512·maskable·Apple touch·favicon 생성 및 Frontend build 포함 확인
- Service Worker·offline cache: 범위 밖, 추가 0건
- Azure image·migration `0068`·revision handover: 최신 main image 게시 승인 / migration·revision 적용은 후속 runtime 단계
- DNS·Front Door: TXT·CNAME 공개 조회 확인 / custom domain validation `Pending`, route·TLS deployment `NotStarted`
- Public traffic·actual provider 발송: 비활성 유지

### 해결한 Finding

| ID | 등급 | 상태 | 원인 | 해결 |
| --- | --- | --- | --- | --- |
| `AZURE-TEAMS-BRAND-001` | P2 | `RESOLVED_MAIN` | Teams package builder가 제품 브랜드가 아닌 임시 사각형 도형을 color·outline icon으로 생성 | 사용자 제공 EMI 원본 기반 192x192 color·32x32 white/transparent outline icon을 추적하고 builder가 그대로 패키징 |
| `AZURE-PWA-ASSET-001` | P2 | `RESOLVED_MAIN` | PWA 운영 요구사항은 확정됐지만 web manifest와 설치 icon 연결이 없음 | standalone manifest, any·maskable icon, Apple touch·favicon과 HTML metadata 추가 |
| `TEAMS-PACKAGE-JSON-001` | P1 | `RESOLVED_MAIN` | 기존 외부 handoff `1.0.1` package의 manifest JSON 구문 오류로 catalog update가 차단됨 | builder가 렌더링한 유효 JSON과 최종 hostname, 기존 Teams·activity identity를 보존한 `1.0.2` package 재생성 |
| `PRIVACY-PROJECTION-001` | P2 | `RESOLVED` | 첫 package 진단 실패가 내부 command 예외에 실제 비밀이 아닌 identifier를 포함 | 출력물을 폐기하고 subprocess stderr·argument를 노출하지 않는 fixed projection으로 다시 실행. tracked 파일·사용자 보고 노출 0건 |

### 실제 구현

1. `assets/branding/emi-logo.png`에 사용자 제공 원본을 byte-for-byte로 보존했다.
2. Teams color icon은 192x192 white background와 중앙 120x120 safe area의 red EMI mark, outline icon은 32x32 transparent background의 white EMI mark로 생성했다.
3. Teams builder의 임시 raster 생성기를 제거하고 추적된 icon copy와 package byte equality 검증으로 바꿨다.
4. PWA manifest는 `EMI 프로젝트 통합관리시스템`·`EMI QMS`, root scope/start, standalone display와 white/red theme를 사용한다.
5. 192·512 any icon, maskable 512 icon, Apple touch 180과 favicon 32를 동일 원본에서 파생했다.
6. Azure artifact validator가 Teams·PWA 자산을 required file과 정적 test로 검사한다.
7. 실제 최종 hostname과 기존 Teams app·activity identity를 보존하고 version만 `1.0.2`로 올린 외부 handoff ZIP을 생성했다.

### 자동 검증

| 검증 | 결과 |
| --- | --- |
| Teams package 정상·negative test | `2/2 PASS` |
| Teams icon 공식 규격 | color `192x192`·critical bounds `120x44` inside safe area / outline `32x32`·white/transparent `PASS` |
| PWA manifest·icon dimension·purpose·HTML link test | `1/1 PASS` |
| Azure artifact static validation | `PASS` |
| Shell syntax·Git whitespace | `PASS` |
| Frontend lint | `PASS`, 기존 warning `1` |
| Frontend typecheck | `PASS` |
| Frontend unit | `25 files / 175 PASS` |
| Frontend Production build | `PASS`, 기존 large chunk warning 유지 |
| Build output PWA asset 존재 | `6/6 PASS` |
| Local Production preview manifest·icon HTTP | `7/7 status 200`, manifest MIME과 image MIME 정상 |
| Teams production handoff package | entry `3/3`, JSON valid, version `1.0.2`, final domain·identity 보존 `PASS` |

### 미실행과 다음 Gate

- 실제 Teams Admin Center catalog update·사용자 설치·activity 발송은 public runtime과 provider Gate 뒤 수행한다.
- PWA 설치 UI는 최신 Frontend image가 Azure revision에 반영된 뒤 최종 hostname에서 검수한다.
- 최신 main image 게시 뒤 migration `0068`, Container Apps revision 교체, TLS·Entra·provider 검수 순서를 유지한다.

## Change 005 — Active workload readiness 보정

### 현재 상태

- DB role bootstrap: `PASS`
- Migration: canonical `67`, ledger `Exact`
- PITR restore rehearsal: `PASS`, 60분 목표 이내, 임시 restore resource 정리 완료
- Active workloads: `PASS` — Container Apps 3/3 provisioning `Succeeded`, running, latest revision ready
- Public traffic·external notification: `비활성 유지`

### 확인된 Finding과 로컬 보정

| ID | 등급 | 상태 | 원인 | 로컬 보정 |
| --- | --- | --- | --- | --- |
| `AZURE-BACKEND-PROBE-HOST-001` | P1 | `RESOLVED` | Production Host allowlist와 Container Apps probe 기본 Host 불일치로 startup probe `400` | Backend 세 HTTP probe에 `Host: publicHost`를 추가하고 실제 Backend revision readiness 확인 |
| `AZURE-FRONTEND-NGINX-MAP-001` | P1 | `RESOLVED` | 64자 origin token이 Nginx 기본 map hash bucket을 초과 | `map_hash_bucket_size 128`을 적용하고 실제 Frontend revision readiness 확인 |

### 로컬 검증

| 검증 | 결과 |
| --- | --- |
| Azure artifact static validation·Bicep 4종 compile | `PASS` |
| `workloads.bicep` 생성 결과와 tracked ARM JSON 구조 동등성 | `PASS` |
| 64자 synthetic origin token Nginx configuration test | `PASS` |
| Backend 세 probe Host header count | `3` |
| Shell syntax·Git whitespace | `PASS` |

2026-08-04 read-only 재확인에서 세 workload의 provisioning·running·latest ready를 확인해 두 readiness P1을 `RESOLVED`로 닫았다. Backend·ClamAV는 내부 ingress, Frontend만 external ingress다. Front Door custom-domain route는 생성됐지만 domain validation은 `Pending`, deployment는 `NotStarted`이므로 public traffic·TLS·실제 provider 검수는 여전히 열리지 않았다.

## 현재 상태

- Task 유형: `UAT_RUNTIME` / Change 005 `BUGFIX`
- 기준 SHA: `origin/main`
- 작업 branch: `fix/task-azure-deploy-001-runtime-readiness`
- Main deployment artifact: `Change 005까지 GitHub main 병합 완료`
- Azure deployment artifact: `Change 005 image·revision readiness 확인 완료`
- 비용·Budget Gate: `사용자 확인 완료`
- Azure resource와 비용 발생 작업: `실행 중 / 사용자 승인 완료`
- Public traffic: `미전환`
- 사용자 검수: `Change 003·Change 004 완료 / Change 005 기술 검증 완료 / public traffic 업무 검수 대기`
- Commit / Push / PR / Merge: `Change 005 원격 main 병합 완료`

Change 004 준비물을 사용해 Foundation, OIDC·ACR 권한, image 게시, secret-scope RBAC, DB role bootstrap, migration과 PITR restore rehearsal을 완료했다. Change 005는 실제 workload activation에서 발견된 두 readiness P1을 보정했고 세 workload의 latest revision readiness까지 확인했다. 현재 Backend·Frontend image는 이번 제품 기준선 통합 전 Azure main source를 사용하므로, 통합 완료 뒤의 image 재게시·재배포는 별도 다음 단계다. DNS·TLS, 실제 provider 발송과 public traffic 전환은 아직 완료로 주장하지 않는다.

## Change 004 — Portal ARM JSON과 수동 GitHub image 게시

### 해결한 Finding

| ID | 등급 | 상태 | Root cause | 해결 |
| --- | --- | --- | --- | --- |
| `AZURE-PORTAL-ARTIFACT-001` | P2 | `RESOLVED_LOCAL` | Bicep 원본만 있어 Azure Portal `로드 파일`에서 쓸 ARM JSON이 없음 | Bicep 생성 ARM JSON 4개를 추적하고 generator metadata 제외 구조 동등성 검증 추가 |
| `AZURE-WEB-IMAGE-PUBLISH-001` | P2 | `RESOLVED_LOCAL` | GitHub에는 일반 CI만 있고 터미널 없는 ACR image 게시 경로가 없음 | Environment 승인·명시 확인·main ancestry guard·OIDC·immutable SHA/digest 기반 수동 workflow 추가 |

### 실제 구현

1. `foundation`, `identity-access`, `workloads`, `edge` ARM JSON을 Azure Portal 사용자 지정 템플릿 편집기의 `로드 파일`에 사용할 수 있게 생성했다.
2. validation은 JSON schema·Bicep generator·resource 존재를 확인하고, Bicep 재빌드 결과와 generator version·template hash를 제외한 구조를 비교한다.
3. GitHub image workflow는 `workflow_dispatch`로만 실행되고 `azure-pilot-image-publish` Environment 보호를 사용한다.
4. 사용자가 ACR 비용 확인 checkbox와 full 40자리 source SHA를 입력해야 하며, SHA가 `origin/main`에 포함되지 않으면 Azure 로그인 전에 실패한다.
5. Azure 인증은 GitHub OIDC 단기 token만 사용하고 workflow·문서에 Azure client secret 입력이 없다.
6. image 게시 identity는 ACR resource scope의 `AcrPush`만 갖도록 Portal 절차를 문서화했다.
7. Backend·Frontend는 `linux/amd64`, source SHA tag, SBOM과 최소 provenance로 build/push하고 mutable `latest`는 만들지 않는다.
8. 실제 registry·hostname·Entra identifier는 GitHub Environment secret에서만 읽으며 Azure CLI 기본 stdout을 끈다.
9. workflow 결과는 source SHA와 두 digest, workload 미배포 상태만 GitHub Summary에 기록한다.

### 자동 검증

| 검증 | 결과 |
| --- | --- |
| ARM JSON 4개 parse·resource·Bicep generator contract | `PASS` |
| Bicep 4종 재compile·ARM JSON 구조 동등성 | `PASS` |
| GitHub workflow actionlint | `PASS` |
| Workflow action full-SHA pin·trigger·permission·OIDC·Environment·provenance static invariant | `PASS` |
| Image 게시 입력 guard 정상·negative 5건 | `6/6 PASS` |
| 신규·변경 shell Bash syntax와 ShellCheck | `PASS` |

### 미실행과 경계

- GitHub Environment, Entra OIDC application/federated credential와 ACR `AcrPush` role은 실제 Azure resource 생성 뒤 사용자가 웹에서 설정한다.
- GitHub Actions 실제 run과 ACR image push는 비용이 발생할 수 있어 실행하지 않았다.
- ARM JSON은 Portal 업로드 가능 artifact지만 `검토 + 만들기`와 실제 deployment는 수행하지 않았다.
- Container Apps workload, DB role bootstrap, migration, restore, edge와 provider Gate는 이전 순서를 그대로 유지한다.

## Change 003 — P1 최소 권한 보정

### 해결한 Finding

| ID | 등급 | 상태 | Root cause | 해결 |
| --- | --- | --- | --- | --- |
| `AZURE-IDENTITY-001` | P1 | `RESOLVED_LOCAL` | 공개 Frontend, Backend와 migration이 하나의 managed identity와 vault-scope secret read를 공유 | 네 identity로 분리하고 Key Vault read를 secret resource scope 10개로 제한. vault-scope workload assignment 제거 |
| `AZURE-DB-ROLE-001` | P1 | `RESOLVED_LOCAL` | Backend와 migration이 관리자급 PostgreSQL 연결을 공유할 수 있었고 별도 runtime role 생성·검증 단계가 없음 | 관리자·`pms_migrator`·`pms_app` 연결 분리, manual bootstrap job과 최소 권한 reconciler, Production fail-closed connection policy 추가 |

### 실제 구현

1. Foundation이 Backend, Frontend, migration, database bootstrap user-assigned identity를 각각 만든다.
2. 신규 `identity-access.bicep`은 Backend 5개, Frontend 1개, migration 1개, database bootstrap 3개 조합에만 secret-scope `Key Vault Secrets User`를 부여한다.
3. Backend는 runtime DB와 실제 알림에 필요한 secret만, migration은 migration DB 하나만, Frontend는 origin token 하나만 참조한다.
4. `database-role-bootstrap` manual job은 관리자 DB secret을 사용해 두 제한 role을 idempotent하게 생성하고 password rotation을 반영한다.
5. `pms_migrator`는 DB connect·temporary, public schema create·usage와 migration object ownership을 가진다. role/database 관리 권한은 없다.
6. `pms_app`은 DB connect, public schema usage, 업무 table CRUD, sequence usage/select, trigger/function execute와 `schema_migrations` select만 가진다. schema·role·temporary table 생성과 ledger mutation은 거부된다.
7. migration 성공 뒤 migration role이 기존 table·sequence·자체 function과 default privilege를 runtime role에 다시 맞춘다.
8. Production database bootstrap/migration command는 세 연결의 TLS `VerifyFull`, 고정 username, 32자 이상·상호 다른 password와 동일 endpoint를 fail-closed로 검증한다.

### 자동 검증

| 검증 | 결과 |
| --- | --- |
| Bicep 4종 foundation/identity-access/workloads/edge compile | `PASS` |
| Azure artifact static invariant와 Teams package | `PASS` |
| Backend build, warning | `PASS`, `0` |
| P1 보안 집중 test | `42/42 PASS` |
| PostgreSQL 실제 role·권한 test | runtime 업무 CRUD 성공, schema·role·temporary·ledger mutation 거부 `PASS` |
| Backend 전체 격리 회귀 | `481/481 PASS`, 11분 45초 |
| Production Backend image | `PASS` |
| Production migration image fresh/existing | migration `67/67 Exact`, 두 실행 `PASS` |
| Backend NuGet known vulnerability | `0` |
| ShellCheck / Git whitespace | `PASS` |
| 변경·신규 파일 high-confidence secret pattern | `0` |
| 독립 diff 재검토: identity/secret matrix, API role fail-closed, migration/bootstrap 직렬화, 문서·parameter 정합성 | `PASS`, 신규 P0/P1/P2 `0` |
| 임시 DB·container·network cleanup | `PASS` |

### 미실행과 경계

- 실제 Azure identity, role assignment와 Key Vault access probe는 resource가 아직 없어 실행하지 않았다.
- 실제 Azure PostgreSQL role bootstrap, migration, PITR와 serving session user 확인은 비용·runtime Gate로 남는다.
- 이 변경은 Azure resource 생성, image push, DNS, traffic 또는 실제 Teams·Gmail 발송을 수행하지 않았다.
- 실제 배포에서는 Foundation → 8개 secret 입력 → secret-scope RBAC → inactive workload → DB role bootstrap → migration → restore → active workload → edge 순서를 지켜야 한다.

## 1. 해결한 업무 문제

20일 동안 세 프로젝트를 최종 hostname으로 시범 운영하기 위해 기존 service-neutral Production 준비물을 Azure의 실제 서비스 경계로 옮길 수 있는 배포 artifact가 필요했다. 동시에 사용자가 비용 관련 실행을 직접 담당하므로, local 준비와 Azure 생성 사이에 명시적인 중단선을 두어야 했다.

이번 변경으로 다음을 준비했다.

1. Front Door 뒤에서만 Frontend origin을 사용할 수 있는 이중 origin 검증
2. Container Apps의 동적 private network를 제한된 CIDR로 신뢰하는 Backend proxy 정책
3. Foundation, inactive workload, migration, restore, active workload와 edge를 분리한 Azure Bicep
4. migration-only job과 serving API의 restore verification gate 분리
5. PMS용 Teams manifest template과 재현 가능한 package builder
6. 실제 hostname, 계정, identifier와 secret을 Git에 남기지 않는 parameter·Key Vault 경계

## 2. 목적, 포함 범위와 제외 범위

### 포함

- Azure Front Door Standard, custom rate limit와 managed TLS 배포 정의
- Container Apps Consumption profile의 Frontend, Backend/worker와 ClamAV 정의
- PostgreSQL Flexible Server Burstable B2s, private network, 32 GB, auto-grow와 PITR 14일 정의
- ACR Basic, Azure Files 5 GB, Key Vault, Log Analytics와 workspace-based Application Insights 정의
- one-shot migration job과 traffic activation gate
- Azure 전용 Frontend image와 origin protection
- Backend trusted proxy network와 migration-only production preflight 보정
- Teams manifest, icon과 ZIP package 생성·검증
- local build, static validation, Bicep compile, migration image와 Backend 전체 회귀 검증

### 제외

- Azure provider 등록, resource group와 실제 resource 생성
- budget과 alert 생성
- ACR push와 실제 secret 입력
- 실제 PostgreSQL migration과 PITR restore rehearsal
- DNS, certificate, public traffic과 조직 Teams catalog 변경
- 실제 Teams·Gmail 발송
- Blob Storage 분리와 정식 운영 HA·Premium WAF 결정
- Git push, PR와 merge

제외 항목은 실패나 누락이 아니라 사용자가 직접 수행하기로 한 비용·운영 경계다.

## 3. 전체 아키텍처와 영향

### Frontend와 Edge

- `frontend/Dockerfile.azure`가 static Frontend를 빌드해 Nginx 8080으로 제공한다.
- `/health/live`만 origin health probe에 허용하고, 나머지는 Front Door ID와 별도 random origin token이 모두 일치해야 한다.
- Front Door가 전달한 client IP를 Backend rate-limit 판단에 유지한다.
- 기존 local·Docker-host Frontend image와 Compose 경로는 변경하지 않았다.

### Backend, API, 권한과 Workflow

- `ReverseProxy:KnownNetworks`를 추가해 bounded CIDR만 trusted proxy로 허용한다.
- `/0`, loopback과 잘못된 network 설정은 Production startup을 중단한다.
- migration-only mode는 초기 배포 전에 불가능한 restore rehearsal 확인만 건너뛴다. Entra, HTTPS, DB, malware scan 등 다른 Production security gate는 유지한다.
- API, 업무 권한, 상태 전이, 알림 수신자 정책과 사용자 Workflow는 변경하지 않았다.

### DB와 Migration

- 신규 application migration은 없다.
- 기존 migration ledger를 one-shot Azure Container Apps Job에서 Exact로 검증한다.
- 초기 migration 성공 후 PITR restore rehearsal을 통과하기 전 serving workload와 public traffic을 활성화하지 않는다.
- PostgreSQL은 private subnet만 사용한다.

### 첨부파일, Excel, PDF와 QR

- 시범 기간에는 기존 PostgreSQL 첨부 저장 계약을 유지한다.
- Excel, PDF와 QR 생성 코드는 변경하지 않았다.
- 업로드 malware scan은 ClamAV private TCP app에 연결하고 Production에서 fail-closed를 유지한다.

### UI·UX

- 제품 화면 변경은 없다.
- Teams 개인 tab은 최종 public hostname을 사용하는 새 PMS manifest로 package할 수 있다.

## 4. 기술적 결정과 검토한 대안

1. **Foundation / workload / edge 분리**
   - resource 생성, secret 입력, migration·restore와 traffic 전환을 한 번에 실행하지 않는다.
   - 실패 지점을 분리하고 비용 발생 전에 사용자가 각 단계를 확인할 수 있다.
2. **API replica 최대 1**
   - API process 안의 background worker 중복 실행을 방지한다.
   - replica 확장은 worker 분리 이후 정식 운영에서 다시 판단한다.
3. **ClamAV 2 vCPU / 4 GiB**
   - signature load와 동시 file scan의 memory 부족을 피하기 위한 시범 최소 여유다.
   - workload activation 전에는 replica 0으로 배포할 수 있다.
4. **Front Door Standard custom rate limit**
   - 외부 사용자가 없는 시범 기간에 Premium WAF 고정비를 보류한다.
   - managed WAF와 private origin은 정식 운영의 실측 기반 결정으로 남긴다.
5. **PostgreSQL B2s / HA 없음**
   - 세 프로젝트 시범량에 맞춘 사양이다.
   - 60분 복구 목표는 PITR rehearsal로 검증하며, 정식 운영 HA 선택은 실제 복구시간으로 결정한다.
6. **Application Insights resource와 connection string만 준비**
   - Container log는 Log Analytics에서 수집할 수 있다.
   - 현재 Backend에는 Application Insights SDK 기반 APM 계측이 없어 request trace 기능은 아직 활성화되지 않는다. 시범 운영 중 필요성이 확인되면 별도 P3 계측 작업으로 연결한다.

## 5. 시행착오 및 폐기한 접근

- Bicep의 최신 Container Apps schema에서 workload profile configuration 경로와 Front Door origin 속성이 달라 첫 compile 오류가 있었다. 현재 schema에 맞춰 수정하고 세 파일을 모두 다시 compile했다.
- 기본 local PostgreSQL에 직접 Backend 전체 test를 실행했을 때 local password 불일치로 test가 실패했다. application 결함으로 오판하지 않고 tmpfs PostgreSQL을 사용하는 격리 test runner로 재실행해 476개 전체 통과와 cleanup을 확인했다.
- 실제 Azure 상태를 확인하거나 provider를 등록하는 방식은 비용·운영 경계를 흐리므로 사용하지 않았다.

## 6. 실제 변경 파일

### Change 004

- `.github/workflows/azure-pilot-images.yml` — GitHub Environment 승인·OIDC·main ancestry guard 기반 수동 image 게시
- `infrastructure/azure-pilot/{foundation,identity-access,workloads,edge}.json` — Azure Portal 업로드용 ARM JSON
- `infrastructure/azure-pilot/README.md` — Portal·GitHub 웹 전용 설정과 실행 방법
- `scripts/validate-azure-image-publish-inputs.sh` — 비용 확인·source SHA·Azure/ACR/Entra 입력 fail-closed guard
- `scripts/test-azure-image-publish-inputs.sh` — 정상·negative 입력 검증
- `scripts/validate-azure-pilot-artifacts.sh` — ARM JSON 동등성·workflow 불변조건 검증
- `tasks/azure-deploy-001-change-004.md`와 종료 산출물 — 승인 범위·검증·남은 운영 Gate 추적

### Backend

- `backend/src/Emi.Qms.Api/Program.cs` — trusted network와 migration-only restore gate 연결
- `backend/src/Emi.Qms.Api/Security/ProductionSecurityPolicy.cs` — CIDR와 restore verification 정책
- `backend/src/Emi.Qms.Api/Security/TrustedProxyConfiguration.cs` — proxy network parsing
- `backend/src/Emi.Qms.Api/appsettings.json` — `KnownNetworks` 기본 구조
- `backend/tests/Emi.Qms.Api.Tests/PublicDeploymentSecurityTests.cs` — 정상·거부·migration-only security test

### Frontend와 Azure

- `frontend/Dockerfile.azure` — Azure Production Frontend image
- `infrastructure/azure-pilot/foundation.bicep` — network, registry, storage, observability, PostgreSQL, Front Door foundation
- `infrastructure/azure-pilot/workloads.bicep` — inactive/active application workload와 migration job
- `infrastructure/azure-pilot/edge.bicep` — origin, custom domain, route, TLS와 security policy
- `infrastructure/azure-pilot/nginx.conf.template` — origin verification과 proxy
- `infrastructure/azure-pilot/*.parameters.example.json` — synthetic parameter 예시
- `infrastructure/azure-pilot/README.md` — 배포 operator manual과 rollback

### Teams와 도구

- `infrastructure/teams/manifest.template.json` — PMS Teams app manifest 원본
- `scripts/build-teams-manifest-package.sh` — icon·manifest·ZIP 생성
- `scripts/test-teams-manifest-package.sh` — package와 negative validation
- `scripts/build-azure-pilot-images.sh` — local Backend·Frontend image build
- `scripts/validate-azure-pilot-artifacts.sh` — static invariant와 선택적 Bicep compile

### Task와 governance

- `.gitignore` — local secret parameter와 generated output 제외
- `docs/00-product-roadmap.md` — Task 상태와 비용 Gate
- `tasks/azure-deploy-001-identity-gate.md`
- `tasks/azure-deploy-001-change-001.md`
- `tasks/azure-deploy-001-sop.md`
- `tasks/azure-deploy-001-user-validation-checklist.md`
- `tasks/azure-deploy-001-implementation-report.md`

기존 Fable interview artifact는 사용자의 Codex-only 지시에 따라 이번 구현의 source로 사용하거나 수정하지 않았다.

## 7. 실행한 자동 검증

| 검증 | 결과 |
| --- | --- |
| Backend public deployment security tests | `37/37 PASS` |
| Backend 전체 격리 회귀 test | `476/476 PASS`, 14분 57초 |
| Backend Production image build | `PASS` |
| Production migration image fresh/existing apply | `PASS`, ledger `67 Exact` |
| Azure Frontend Production image build | `PASS` |
| Frontend origin smoke | health `200`, direct `403`, partial header `403`, trusted edge `200` |
| Bicep compile | foundation/identity-access/workloads/edge `PASS` |
| Azure artifact static validation | `PASS` |
| Teams manifest/package test | `2/2 PASS` |
| Shell syntax와 ShellCheck | `PASS` |
| Git whitespace validation | `PASS` |
| tracked 배포 산출물 email/private-key pattern scan | `0` |
| 격리 DB·container·network cleanup | `PASS` |

Frontend build에는 기존 large bundle warning이 있었으나 build 실패나 배포 artifact 오류는 없었다.

## 8. 미실행 검증

| 검증 | 상태와 이유 |
| --- | --- |
| Azure Bicep `what-if`와 실제 create | 비용 관련 Azure 동작은 사용자 실행 범위 |
| Actual migration job과 ledger | Azure PostgreSQL 생성 전 실행 불가 |
| PITR restore 60분 목표 | 실제 PostgreSQL backup 생성 후 검증 가능 |
| DNS, managed TLS와 Front Door route | resource와 DNS 변경 전 검증 불가 |
| Entra 실제 login·승인 workflow | public callback과 실제 app 설정 전 검증 불가 |
| Teams 조직 catalog·activity | 실제 package parameter와 admin 게시 전 검증 불가 |
| Gmail 실제 발송 | Key Vault secret과 public workload 활성화 전 검증 불가 |
| 사용자 세 프로젝트 UAT | public runtime 전환 전 검증 불가 |

## 9. 개인정보와 Secret 검토

- 실제 hostname, 사용자 이름, 회사 email, tenant/client/object identifier와 secret을 tracked 배포 파일에 기록하지 않았다.
- parameter 예시는 `example.*`, synthetic GUID와 placeholder만 사용한다.
- 실제 값은 ignored `*.local.json`, Azure Portal secure parameter와 Key Vault에만 둔다.
- 검증 로그는 status, count와 aggregate만 남기도록 구성했다.

## 10. Finding과 Pre-traffic Gate

### Finding

| ID | 등급 | 상태 | 내용과 영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `AZURE-IDENTITY-001` | P1 | `RESOLVED_LOCAL` | workload identity와 Key Vault 권한 공유로 침해 범위가 전체 secret으로 확대될 수 있었다. | 네 identity와 secret-scope assignment 10개로 분리. 실제 Azure access probe는 pre-traffic에서 검증 |
| `AZURE-DB-ROLE-001` | P1 | `RESOLVED_LOCAL` | API와 migration의 DB 관리자 권한 공유 가능성이 있었다. | `pms_app`·`pms_migrator`·admin 분리와 실제 PostgreSQL negative privilege test 통과 |
| `AZURE-PORTAL-ARTIFACT-001` | P2 | `RESOLVED_LOCAL` | Portal 업로드용 ARM JSON이 없어 터미널 없는 Foundation 배포를 시작할 수 없었다. | 추적 JSON 4개와 Bicep 구조 동등성 검증 추가 |
| `AZURE-WEB-IMAGE-PUBLISH-001` | P2 | `RESOLVED_LOCAL` | 웹 수동 ACR image 게시 경로가 없었다. | OIDC·Environment 승인·main ancestry·immutable digest workflow 추가 |
| `AZURE-COST-GATE-001` | External gate | `RESOLVED` | 비용·credit·Budget 확인 뒤 승인된 Foundation·workload·DB·edge resource를 생성했다. | Budget 알림과 20일 시범 비용 모니터링 유지 |
| `AZURE-APM-001` | P3 | `BACKLOG` | Application Insights resource 정의는 있으나 Backend SDK APM 계측은 아직 없다. Log Analytics container log는 사용 가능하다. | 시범 운영에서 request trace 필요성을 확인한 뒤 별도 계측 |
| `FRONTEND-BUNDLE-001` | P3 | `BACKLOG` | Production build의 기존 large bundle warning이 유지된다. 기능 오류는 아니다. | 정식 운영 성능 점검에서 route chunk 분할 검토 |

Open P0/P1/P2 code Finding은 `0`이다. 두 P3는 Product Roadmap의 명시적 backlog에 연결한다.

### Pre-traffic operational gate

| ID | 상태 | Public 활성화 전 필수 검증 |
| --- | --- | --- |
| `AZURE-RESTORE-001` | `RESOLVED` | 실제 PITR restore가 60분 안에 완료되고 migration ledger·aggregate가 일치함 |
| `AZURE-EDGE-AUTH-001` | `RESOLVED` | DNS·managed TLS·Entra callback·Front Door 200·origin 403, readiness 200·익명 API 401과 실제 비상 관리자 로그인을 확인함 |
| `AZURE-PROVIDER-001` | `RESOLVED` | Worker 활성화 뒤 최신 Teams Activity `6`건·Mail `3`건이 모두 1회 시도로 `Sent`; Open Pending/Processing/Failed `0`; 사용자 client·메일함 실제 수신 확인 완료 |

이 세 Gate는 Change 002에 따라 Git merge를 막지 않지만 모두 PASS 전에 public traffic과 external notification 활성화를 금지한다.

## 11. Rollback과 복구

1. migration 또는 restore rehearsal 실패 시 workload를 inactive 상태로 유지하고 edge를 생성하지 않는다.
2. Frontend/API smoke 실패 시 새 image digest로 forward-fix하고 기존 digest를 보존한다.
3. Edge 전환 뒤 문제가 생기면 Front Door route를 비활성화하고 origin workload를 scale 0으로 내린다.
4. 데이터 손상이 의심되면 마지막 검증된 PITR restore server로 전환하며 원본 server는 즉시 삭제하지 않는다.
5. 시범 중단 시 비용 resource 삭제는 사용자가 usage, backup과 보존 정책을 확인한 뒤 수행한다.

## 12. 사용자 검수 결과와 남은 항목

- Checklist: `작성됨`
- 자동 검증: `완료`
- 사용자 검수: `Change 003~014의 구현·runtime 적용 승인 완료 / 현재 비상 관리자 로그인·관리자 화면 접근 확인`
- Azure resource: `생성·readiness 확인 완료`
- Public URL: `DNS·managed TLS·정적 화면·PWA·origin 403·readiness 200·익명 API 401 완료`
- 다음 Gate: 사용자가 명시적으로 순서를 변경해 Change 015~017 원격 `main` 게시 → 승인 게이트형 GitHub→Azure 배포 연결 → Teams SSO·새 manifest 기획 순으로 진행한다.
- 비용·장애·응답시간·DB·첨부 증가량은 20일 시범 기간 동안 계속 기록한다.

## 13. 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/azure-deploy-001-implementation-report.md` | Change 014 API·로그인 Gate와 exact-host source sync 반영 완료 |
| SOP | `tasks/azure-deploy-001-sop.md` | 공개 API 복구·exact-host 운영 절차 반영 완료 |
| User manual | `infrastructure/azure-pilot/README.md` | 기존 Portal·GitHub·rollback 절차 유지, 사용자 동선 변경 없음 |
| Roadmap update | `docs/00-product-roadmap.md` | Change 014 API·로그인 완료와 provider 다음 Gate 반영 완료 |
| User validation checklist | `tasks/azure-deploy-001-user-validation-checklist.md` | domain·TLS·PWA·origin·API·현재 관리자 로그인 완료 |

## 14. Git와 게시 상태

- Change 009: Commit·Push·PR·Merge 완료, 원격 main 반영 완료.
- Change 010: Commit·Push·PR·Merge와 최종 main Backend·Frontend image 게시 완료.
- Change 011: migration `0069 Exact`, Backend·Frontend 최신 revision 적용과 readiness 확인 완료. 문서 게시 미승인.
- Change 012: DNS exact match·기존 token empty PATCH 재검증 완료, Azure `Pending` 외부 대기. 문서 게시 미승인.
- Change 013: PR #72 원격 main squash merge·main CI·immutable image 게시·Frontend revision 적용 완료.
- Change 014: Backend exact-host runtime 적용과 공개 API·현재 관리자 로그인 확인 완료. 배포 원본·문서 동기화 branch의 commit·push·PR·merge 진행.
- Change 016: bootstrap 관리자 1명 대상 synthetic Teams Activity Graph actual 1회가 `204 Sent`. 대상 Entra 사용자 일치와 Teams web exact 제목·preview 렌더링을 확인했고 Runtime 설정·DB·업무 data는 변경하지 않았다. 이후 실제 worker 알림 수신 검수로 client 표시까지 확인했다.
- Change 017: 외부 알림 Worker·Teams Activity·Gmail SMTP actual 활성화, 최신 수동 Teams Activity `6/6 Sent`·Mail `3/3 Sent`, Open Pending/Processing/Failed `0`, latest Backend revision Ready, 사용자 Teams client·메일함 실제 수신 완료.
- 미포함: Teams SSO·새 manifest와 QOM branch 반영.
