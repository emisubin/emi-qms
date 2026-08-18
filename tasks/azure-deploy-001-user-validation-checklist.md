# TASK-AZURE-DEPLOY-001 사용자 검수 체크리스트

## Change 026 — 프로젝트 전체 흐름 상태 전용 표시 운영 release

- [x] PR #108의 Backend·Frontend·Full-Stack·Workflow Validation과 필수 `CI Gate`가 최종 통과했다.
- [x] exact main SHA `51aba7e97a2d1fee0f9ee4b82a3f89d514171acf`의 main CI run `32197258001`이 검증된 PR tree를 재사용해 통과했다.
- [x] Azure release run `32197298425`에서 Backend·Frontend image를 병렬 생성하고 변경 revision을 교체했다.
- [x] migration 변경이 없어 DB migration을 실행하지 않았고 기존 업무 데이터와 알림 설정을 보존했다.
- [x] release의 public security smoke와 별도 익명 root `401` 확인이 통과했다.
- [ ] 공개 프로젝트 상세의 전체 흐름 상단과 단계 카드에 업무 건수가 없고 Requested 단계가 `업무 요청됨`으로 보이는지 사용자가 확인한다.

## Change 025 — 부서장별 양식 권한·PWA 운영 정의 통합 release

- [x] PR #103의 Backend·Frontend·Full-Stack·Workflow Validation과 필수 `CI Gate`가 모두 통과했다.
- [x] exact main SHA `58c089993587deea30513cb6edee0b8396a1d474`의 Azure release run `31786040822`가 성공했다.
- [x] migration `0078` 실행이 성공한 뒤 Backend·Frontend latest revision과 ready revision이 각각 일치하고 Running이다.
- [x] 공개 health `200`, 익명 root·`/api/me` `401/401`을 확인했다.
- [x] Web Push `Enabled=true`, `DryRun=false`와 공개키·비밀키 Key Vault secret reference가 재배포 뒤에도 유지됐다.
- [x] 미완성 공통 매뉴얼 작업, 운영 secret 원문, 사용자별 구독과 업무 data는 변경하지 않았다.

## Change 022 후속 — Web Push 운영 검수와 재배포 보존

- [x] 운영 Web Push가 `Enabled=true`, `DryRun=false`이며 Backend latest revision과 ready revision이 일치하고 Running 상태다.
- [x] VAPID 공개키·비밀키는 Key Vault secret reference로만 Backend에 연결되고 원문이 Repository·문서·검증 출력에 노출되지 않는다.
- [x] Backend identity의 두 VAPID secret exact-scope 역할 `2/2`, vault-scope 역할 `0`을 확인했다.
- [x] iPhone·Android를 포함한 검수 사용자 3명이 PWA와 Teams 알림을 받고 알림 선택 시 인앱 상세로 이동하는 것을 확인했다.
- [x] 직원별 PWA 설치·알림 허용은 자율이며 중앙 등록률을 배포 완료 조건으로 사용하지 않는다.
- [x] 향후 Azure workload 전체 재배포에서 `enableExternalNotifications=true`를 사용하면 Web Push 활성·실발송과 VAPID Key Vault 참조가 유지된다.
- [x] 현재 운영의 기존 role assignment 이름을 비추적 입력으로 전달한 identity-access `what-if`에서 role assignment Create/Delete `0/0`을 확인했다.

## Change 020 — 지정 로그인·내부 로고 main 운영 release

- [x] 로그인 PR #83과 내부 공통 로고 PR #84가 원격 `main`에 squash merge됐고 각각의 PR CI가 성공했다.
- [x] 최종 main SHA `37dd619685e6447fc867d213d1f63692c6cd8c62`의 Frontend·Backend·Full-Stack CI가 실패 job 재실행을 포함해 최종 `3/3` 성공했다.
- [x] 승인형 운영 release run `31354814082`가 exact main SHA의 Backend·Frontend immutable image를 게시했다.
- [x] migration, Backend revision, Frontend revision과 public security가 모두 `PASS`다.
- [x] 배포 뒤 public health `200`, 익명 root·`/api/me` `401/401`, app shell 전 Microsoft 365 로그인 redirect를 확인했다.
- [ ] 인증 뒤 실제 PC·iPhone·Android에서 지정 로그인·내부 로고를 눈으로 확인한다.

## Change 019 — Azure 정상 revision 상태 판정 보정

- [x] 첫 운영 release가 `BASELINE_NOT_READY`로 중단됐고 migration·Backend·Frontend mutation이 모두 `0`인지 확인했다.
- [x] 실제 두 앱이 single revision, latest ready, provisioning `Succeeded`, health `Healthy`, running state `RunningAtMaxScale`인지 read-only로 확인했다.
- [x] `Running`과 `RunningAtMaxScale`만 허용하고 `Stopped`, `ScaleToZero`, `Degraded`, `Unknown`은 mutation 전에 차단하는 mock 회귀를 통과했다.
- [x] Change 019 source를 commit·push·PR·CI·원격 `main`에 게시했다. (PR #79, 2026-08-07)
- [x] 최신 `main` full SHA로 운영 release를 다시 실행해 migration·Backend·Frontend·공개 보안 검사를 모두 통과했다. (run `31145661267`, 2026-08-07)

## Change 018 — 승인형 GitHub 운영 release 연결

- [x] Change 015~017이 PR #74와 원격 `main`에 병합되고 CI 3종이 모두 성공했다.
- [x] Workflow가 자동 `push` 배포 없이 `workflow_dispatch`, `main` 최신 full SHA와 image·운영 배포 확인 두 개를 모두 요구한다.
- [x] Migration 성공 전 앱 변경 `0`, Backend→Frontend 순서, 실패 시 직전 image rollback을 synthetic Azure/HTTP test로 확인했다.
- [x] Mutable `latest`, Azure client secret, subscription/resource group 범위 `Contributor`와 tracked 실제 resource 이름이 없다.
- [x] Actionlint, shell syntax·ShellCheck, 입력·release mock test, Bicep compile·ARM JSON 동등성, Azure artifact와 공개 배포 보안 집중 검증을 통과했다.
- [x] GitHub Environment variable 네 개와 OIDC identity의 exact resource 역할 네 개를 실제 값 노출 없이 설정·재확인했다.
- [x] Change 018 source를 commit·push·PR·CI·원격 `main`에 게시했다.
- [x] 별도 명시 실행에서 최신 `main` SHA로 운영 release를 시작했고, 정상 기준선 판정 누락을 migration·앱 mutation 전에 안전하게 발견했다. 최종 성공은 Change 019에서 계속한다.

## Change 017 — 운영 외부 알림 Worker 활성화

- [x] 기존 대기 알림 일괄 발송과 worker 활성화 사용자 승인을 확인했다.
- [x] Teams/Gmail credential·SMTP/TLS·단일 Backend·disabled/dry-run 기준선 preflight를 통과했다.
- [x] Dispatcher·Teams Activity·Mail 다섯 actual flag를 활성화하고 latest Backend revision Ready를 확인했다.
- [x] 최신 수동 Teams Activity `6`건과 Mail `3`건이 모두 attempt `1`, `Sent`인지 확인했다.
- [x] Open Pending·Processing·Failed가 모두 `0`인지 확인했다.
- [x] 기존 관리자 `Dismissed` Mail `2`건은 제외 상태를 보존했다.
- [x] 사용자가 최신 Teams Activity와 메일이 실제 client·메일함에 도착했는지 확인했다. (`2026-08-06`)

## Change 016 — 운영 Teams Activity 1건 Provider Smoke

- [x] Teams 앱의 관리자 승인·조직 catalog 설치 완료를 사용자 보고로 확인했다.
- [x] 현재 Azure 로그인 사용자가 bootstrap 관리자이고 수신자 1명으로 고정되는지 확인했다.
- [x] 실제 업무 data가 없는 synthetic `generalNotification`을 Microsoft Graph로 정확히 1회 보냈다.
- [x] Graph HTTP `204`, provider `SENT`, 안정 코드 `TEAMS_ACTIVITY_GRAPH_ACCEPTED`를 확인했다.
- [x] Notification Dispatcher·Mail을 활성화하지 않았고 Runtime env·DB·migration·업무 row·container revision을 변경하지 않았다.
- [x] 사용자 제공 계정과 실제 수신 대상이 같은 Entra 사용자임을 확인했다.
- [x] 동일 알림을 재발송하지 않고 Teams web Activity Feed에서 exact 제목과 preview 표시를 확인했다.
- [ ] Desktop Teams에서 최신 수동 Activity가 표시되는지 사용자가 확인한다.

## Change 015 — 공개 Frontend Entra 사전 인증

- [x] 시험환경 없이 운영 `pms`에 직접 적용하고 Teams tab 호환 실패 시 Activity 알림 전용으로 유지하는 사용자 정책을 확인했다.
- [x] single-tenant Entra web application과 Key Vault secret을 준비했다.
- [x] Frontend secret-scope RBAC와 Container Apps Easy Auth를 적용했다.
- [x] 익명 비브라우저 root·JavaScript·PWA manifest·API가 `401`이고 `/health/live`만 `200`이다.
- [x] 익명 브라우저에는 PMS root·bundle reference가 없는 Easy Auth 인증 화면만 표시된다.
- [x] 실제 허용 계정이 Frontend와 기존 Backend 권한으로 정상 로그인하고 프로젝트·관리자 메뉴에 접근한다.
- [x] 별도 Teams tab 검수 없이 호환 실패 시 Activity 알림 전용으로 운영하는 사용자 정책을 기록했다.
- [x] auth platform 비활성 rollback 명령과 범위를 확인했다. 정상 게이트를 여는 실제 rollback은 수행하지 않았다.

## Change 014 — 공개 API Host allowlist와 관리자 로그인

- [x] Change 013 PR #72를 원격 `main`에 병합하고 PR·main CI 성공을 확인했다. (`2026-08-06`)
- [x] Change 013 포함 immutable Frontend image를 게시·교체하고 latest revision readiness를 확인했다.
- [x] 공개 hostname과 exact Backend internal hostname만 허용해 `/health/ready` `200`, 익명 `/api/me`·`/api/runtime-mode` `401`을 확인했다.
- [x] HTTP→HTTPS, root·PWA `200`, direct Frontend origin `403`, Backend direct ingress 차단과 TLS hostname 일치를 확인했다.
- [x] 현재 비상 관리자 계정이 bootstrap 목록에 포함되고 실제 관리자 메뉴·사용자 관리 화면에 접근함을 확인했다.
- [x] bootstrap 관리자 목록 순서는 권한 우선순위가 아니며 포함된 계정은 동일한 System Administrator 권한을 받으므로 secret을 재정렬하지 않았다.
- [ ] 두 번째 비상 관리자 계정도 실제 로그인해 System Administrator 접근을 확인한다.

## Change 009 — 공개 Teams 알림 유형과 자동 발송 연결

- [x] Change 009 PR #70을 원격 `main`에 merge하고 PR·main CI 성공을 확인했다. (`2026-08-05`)
- [x] 원래 계획의 프로젝트·업무·Pending·재검사·완료 수신자와 event 발생 시점을 Backend 자동 delivery에 연결했다.
- [x] manifest를 공개 운영용 10개 Activity type으로 갱신하고 `dailyDigest`를 제거했다.
- [x] `packageName`이 없고 기존 identity·hostname·권한·icon을 보존한 `1.0.4` package를 생성했다.
- [x] migration `0069`가 기존 row를 보존하면서 새 event source 5개만 허용하는 것을 검증했다.
- [x] Backend build, 전체 `485/485`·알림 `98/98` 회귀, 관련 업무·migration 집중 테스트, package와 Azure artifact 정적 검증을 통과했다.
- [x] Azure DB에 migration `0069`를 적용하고 `69/69 Exact`, execution `Succeeded`를 확인했다. (`2026-08-05`)
- [x] 새 Backend revision을 배포하고 `Notifications__TeamsActivity__PersonalChannelStrategy=TeamsActivity`를 확인했다. 외부 알림은 disabled·dry-run을 유지했다. (`2026-08-05`)
- [x] Teams Admin Center에 `1.0.4` package 승인 요청을 제출했다. (`2026-08-05`, 사용자 보고)
- [x] Teams 관리자 승인 완료 뒤 조직 catalog 표시와 사용자 앱 설치 상태를 사용자 보고로 확인했다.
- [ ] 프로젝트 생성·납기 변경·상태 변경·업무 배정·긴급/차단·재검사·프로젝트 완료 actual Activity를 각 1건 검수한다.
- [ ] 에스컬레이션 세부 조건·단계·추가 수신자는 배포 후 별도 기획한다.

## Change 008 — Teams v1.19 manifest schema 보정

- [x] v1.19 schema에 정의되지 않은 최상위 `packageName`을 제거했다.
- [x] 생성 package의 `packageName` 부재를 package test로 고정했다.
- [x] Microsoft Teams v1.19 공식 JSON Schema 전체 검증을 통과했다.
- [x] 기존 manifest·Activity identity, 운영 hostname, 권한·activity type과 icon을 보존한 `1.0.3` package를 생성했다.
- [x] `1.0.3` 별도 등록은 공개 알림을 포함한 `1.0.4` 승인 요청으로 대체했다. 조직 catalog 표시는 1.0.4 승인 뒤 확인한다.

## Change 006 — Teams·PWA 브랜드 자산

- [x] 사용자 제공 EMI PNG를 canonical brand source로 보존했다.
- [x] Teams 192x192 color·32x32 outline icon이 제공 원본을 사용하고 package test를 통과했다.
- [x] PWA 192·512·maskable·Apple touch·favicon과 web manifest가 Frontend build에 포함됐다.
- [x] Service Worker·offline cache·앱 내부 UI 재디자인이 추가되지 않았다.
- [x] 최종 hostname과 기존 Teams·activity identity를 보존한 `1.0.2` package가 유효한 JSON으로 생성됐다.
- [x] Change 006 PR #68을 원격 main에 squash merge하고 PR·main CI 성공을 확인했다. (2026-08-05)
- [x] Change 007 main의 Backend·Frontend image를 ACR에 게시했다.
- [x] Change 007 image와 migration `0068`을 Azure revision에 반영했다.
- [x] Change 010 문서 merge 뒤 Change 009 포함 최종 main Backend·Frontend image를 ACR에 게시했다. (`2026-08-05`)
- [x] 최종 main Backend·Frontend image를 Azure revision에 적용하고 두 revision의 Healthy·latest ready를 확인했다. (`2026-08-05`)
- [x] 최종 hostname에서 PWA 설치 icon·이름·standalone 실행과 실제 iPhone·Android 푸시 수신을 확인했다.
- [ ] Change 009의 공개 알림 `1.0.4` package를 update하고 테스트 사용자에게 새 icon이 표시되는지 확인한다.

## Change 005 — Active workload readiness

- [x] Backend probe Host 보정과 Nginx 64자 token configuration의 로컬 자동 검증이 통과했다.
- [x] Change 005 Frontend image와 workload revision이 실제 Azure에 반영됐다.
- [x] Backend·Frontend·ClamAV가 모두 provisioning `Succeeded`, running, latest revision ready다. (read-only 재확인, 2026-08-04)
- [x] Frontend direct health는 `200`, direct 업무 route는 `403`이다. (read-only 재확인, 2026-08-04)
- [x] Front Door custom-domain route는 생성됐지만 deployment `NotStarted`이며 public traffic과 external notification은 Edge·인증 Gate 전까지 비활성이다. (read-only 재확인, 2026-08-04)

## Change 003 로컬 P1 보안 변경

- [x] Backend·Frontend·migration·DB bootstrap identity 분리 구조와 secret 접근표를 확인했다.
- [x] 관리자·migration·runtime PostgreSQL 연결 분리와 `pms_app` 최소 권한 검증 결과를 확인했다.
- [x] 실제 Azure 자원 생성·비용·traffic·외부 발송은 이번 검수에 포함되지 않음을 확인했다.
- [x] Change 003 사용자 검수를 완료하고 commit·push·PR·CI·`main` merge를 승인했다. (2026-08-02)

## Azure 생성 전

- [x] 20일 예상 비용과 남은 credit을 확인했다. (사용자 확인, 2026-08-02)
- [x] Budget 알림 3단계를 사용자가 직접 설정했다. (사용자 확인, 2026-08-02)
- [x] 실제 hostname·identifier·email·secret이 Change 010 Git diff에 없음을 확인했다.
- [ ] DB 관리자·`pms_migrator`·`pms_app` password가 서로 다르고 각각 32자 이상이다.

## Change 004 — 터미널 없는 Portal·GitHub 준비

- [x] ARM JSON 4개가 JSON parse와 Bicep 재compile 구조 동등성 검사를 통과했다.
- [x] GitHub 수동 image workflow가 actionlint, ShellCheck와 정상·negative 입력 검사를 통과했다.
- [x] 기존 image workflow가 `workflow_dispatch`, 비용 확인 checkbox와 `main` 포함 full SHA만 허용했다. Change 018부터는 실행 시점 최신 `main` SHA와 운영 배포 확인까지 요구한다.
- [x] Azure client secret, mutable `latest` tag와 자동 Container Apps 배포가 없음을 자동 확인했다.
- [x] Azure Portal `로드 파일`용 JSON 4개와 GitHub 웹 실행 절차를 사용자가 확인하고 main 병합을 승인했다. (2026-08-02)
- [x] 실제 GitHub Environment·OIDC·ACR 권한을 설정하고 Change 007 image workflow 성공으로 확인했다.
- [x] 실제 GitHub Actions image push를 승인된 Change 007 main SHA로 성공시켰다.

## 배포와 복구

- [x] Migration job이 Exact로 끝나기 전 public traffic이 열리지 않았다.
- [x] PITR restore가 60분 이내에 끝났고 migration ledger와 aggregate가 맞았다.
- [x] Backend와 ClamAV에 public ingress가 없고 Frontend만 external ingress다. (read-only 재확인, 2026-08-04)
- [x] 가비아 권한 DNS `3/3`과 공용 resolver `4/4`에서 validation TXT가 일치하고 Azure CNAME 진단이 성공했다. (`2026-08-05`)
- [x] 기존 validation token을 유지한 empty PATCH 재검증을 요청하고 TXT·CNAME `1/1 exact match`를 다시 확인했다. (`2026-08-05`)
- [x] Front Door domain validation·deployment·provisioning과 managed TLS가 완료됐다. (`2026-08-06`)
- [x] Front Door URL·PWA는 열리고 Container App 원본 URL은 health 이외 403이다. (`2026-08-06`)
- [x] TLS certificate가 최종 hostname과 일치한다. (`2026-08-06`)
- [x] Change 013 Frontend image 교체 뒤 `/health/ready`는 200, 익명 `/api/me`는 401이다. (`2026-08-06`)
- [ ] Key Vault 전체 scope의 workload secret-read role이 0이다.
- [ ] 이전 단일 runtime identity를 배포한 이력이 있다면 남은 vault-scope role assignment와 미사용 identity를 정리했다.
- [ ] Backend·Frontend·migration·DB bootstrap identity가 서로 다르고 접근표 밖 secret read가 거부된다.
- [ ] DB role bootstrap job이 성공한 뒤에만 migration job을 실행했다.
- [ ] Backend DB session은 `pms_app`, migration DB session은 `pms_migrator`로 확인된다.
- [ ] `pms_app`의 업무 CRUD는 성공하고 schema·role·temporary table 생성과 migration ledger 변경은 거부된다.

## 로그인과 권한

- [x] Entra API·SPA client가 분리되고 공개 SPA redirect와 `access_as_user` scope가 등록됐다. (`2026-08-06`)
- [x] 현재 비상 관리자 계정이 System Administrator로 로그인해 관리자 화면에 접근할 수 있다. (`2026-08-06`)
- [ ] 나머지 비상 관리자 계정도 System Administrator로 로그인할 수 있다.
- [ ] 처음 로그인한 일반 사용자는 역할 승인 전 조회·입력이 불가능하다.
- [ ] 관리자가 역할을 부여한 뒤 해당 부서 권한만 사용할 수 있다.
- [ ] 로그아웃 뒤 protected API와 화면이 다시 열리지 않는다.

## 실제 업무

- [ ] 세 프로젝트를 생성하고 부서별 조회·입력·첨부가 정상이다.
- [ ] ClamAV 정상 파일 허용, 위험·검사 불가 파일 차단을 확인했다.
- [ ] Excel·PDF·QR 다운로드가 정상이다.
- [ ] DB·첨부 증가량과 API·ClamAV memory를 매일 확인했다.

## 알림

- [ ] In-app 알림과 내 업무가 정상이다.
- [ ] 새 Teams manifest가 조직 catalog에서 PMS 이름과 최종 주소로 열린다.
- [x] Teams activity Graph actual 1건은 `204`로 수락됐고 Teams web Activity Feed에 표시됐다.
- [x] Worker 활성화 뒤 최신 Teams Activity `6`건과 Mail `3`건이 provider `Sent` 상태다.
- [x] Teams client와 실제 메일함에서 최신 알림 수신을 확인했다. (`2026-08-06`)
- [ ] 중복 발송과 실제 업무 원문 과다 노출이 없다.

## 운영 종료

- [ ] 20일간 비용, 장애, 응답시간, DB·첨부 증가량을 기록했다.
- [ ] 정식 운영 사양과 HA/WAF/Blob 여부를 실측값으로 다시 결정했다.
- [ ] 시범 데이터를 정식 운영에 유지할지 최종 확인했다.
