# TASK-UAT-001 Implementation Report

## 1. 목적

HTTPS Development UAT를 Teams Activity 검수에 계속 사용할 수 있도록 frontend port, process ownership, protocol readiness, notification environment와 startup master-data 원자성을 안정화한다.

## 2. 배경

HTTP/HTTPS UAT가 5174를 교체 사용하는 구조에서 Vite fallback port, protocol 오판, 다른 process 종료와 master-data 부분 성공 가능성이 P2로 식별됐다. 원래 UAT-001 WIP와 실행 중인 HTTPS server는 보존하고 최신 main 기반 별도 worktree에서 통합했다.

WIP는 외부 임시 patch와 SHA-256으로 백업했다. 최신 main 적용 결과 원래 4개 파일은 동일했으며 conflict는 없었다. 최종 리뷰에서 process cwd prefix가 sibling 경로까지 허용하는 경계 문제를 발견해 integration worktree에만 보강했다.

## 3. 해결한 P2

1. 5174 strict port
2. listener/PID/screen process ownership
3. HTTP/HTTPS protocol readiness
4. frontend port 설정의 script/Vite 일치
5. HTTPS notification dotenv loading
6. master-data transaction/rollback

## 4. 구현 범위

- Vite env port validation과 strict port
- HTTP/HTTPS mode별 URL과 반대 protocol 검사
- frontend/backend listener, PID file와 screen session ownership
- notification namespace literal dotenv parser와 configured/missing report
- link BaseUrl runtime fallback
- 두 master-data block의 `ON_ERROR_STOP` transaction
- persistent UAT와 isolated E2E 회귀
- Task 산출물과 Roadmap

## 5. 제외 범위

Review-safe mode, dependency upgrade, notification concurrency/retry, escalation starvation, 마지막 관리자 동시성, Git history 개인정보, 실제 외부 발송 smoke, 신규 기능은 포함하지 않는다.

Migration, Backend runtime, API, 권한, Workflow 계산, Excel/PDF/첨부파일 로직은 변경하지 않았다. UAT startup이 기존 additive migration과 master upsert를 실행하는 동작은 유지한다.

## 6. 수정 파일

- `frontend/vite.config.ts`: env dev port와 `strictPort`
- `scripts/dev-uat-start.sh`: port/protocol/ownership/readiness/dotenv/transaction
- `scripts/dev-uat-start-teams-https.sh`: 5174와 HTTPS wrapper env 정합성
- `tasks/uat-001-https-dev-stability.md`: Task 정의와 checklist
- 이 implementation report
- `tasks/uat-001-sop.md`
- `tasks/uat-001-user-manual.md`
- `docs/00-product-roadmap.md`

Dependency/lockfile, migration, E2E isolation file, backend runtime source와 generated file은 변경하지 않았다.

## 7. Strict port와 process ownership

Vite는 `VITE_DEV_SERVER_PORT`를 정수 TCP port로 검증하고 `server.port`에 사용한다. Config와 CLI 모두 strict port를 사용하며 Manual UAT는 5174 이외 값을 시작 전에 거부한다.

Listener process는 cwd와 command를 함께 검사한다. Repository path 판정은 단순 prefix가 아니라 root 자체 또는 `/` 경계를 가진 실제 하위 경로만 허용한다. Screen session도 동일한 boundary를 적용한다. Unexpected process는 종료하지 않으며, startup 성공 후 실제 listener PID를 PID file에 기록한다.

5184 점유 테스트에서 unrelated process는 유지됐고 Vite는 non-zero로 종료했으며 5185 fallback listener는 생성되지 않았다.

## 8. HTTP/HTTPS readiness

- HTTP 5184: HTTP root 200, HTTPS 실패
- HTTPS 5184: 신뢰된 certificate로 HTTPS root/health proxy 200, HTTP 실패
- Live HTTPS 5174: root, Teams Activity, admin, health proxy, `/api/me`, `/api/projects` 200
- Backend 5081: live/ready 200

Startup은 frontend root와 health proxy를 확인하고 HTTPS mode에서는 Teams Activity도 확인한다. 반대 protocol이 성공하면 mismatch로 중단한다.

## 9. Dotenv loading

`.env.notify-local`은 shell source/eval 없이 줄 단위로 읽는다. 유효한 key 중 `Notifications__` namespace만 export하고 값은 한 쌍의 outer quote만 제거한 뒤 literal로 전달한다. 특수문자 probe는 command를 실행하지 않았고 unrelated key는 export되지 않았다.

현재 설정은 dispatch와 TeamsChannel, TeamsActivity, Mail actual 조건이 구성돼 있다. 실제 값은 검사 출력이나 문서에 기록하지 않았다. Links BaseUrl key가 없으면 현재 frontend mode URL을 runtime fallback으로 사용한다.

## 10. Worker/runtime 확인

- Backend listener process: 1개
- Frontend listener process: 1개
- `NotificationDeliveryWorker`: 등록
- `NotificationEscalationWorker`: 등록
- `AdminDeletionPurgeWorker`: 등록
- Daily Digest: 별도 worker가 아니라 notification delivery dispatcher 흐름
- Pending delivery: 0
- Failed delivery: 20
- Sent delivery: 59
- Active escalation: 0, Resolved 2

신규 notification/delivery를 생성하거나 실제 provider를 호출하지 않았다.

## 11. Master-data transaction

Workflow stage와 production-planning schema/master block에 각각 `psql -v ON_ERROR_STOP=1`, `BEGIN`, `COMMIT`을 적용했다. SQL 업무 내용은 유지했다.

전용 E2E PostgreSQL에서 각 block의 commit 직전 존재하지 않는 relation 조회를 삽입했다. 두 실행 모두 non-zero로 종료됐고 workflow aggregate와 production-planning schema/data snapshot은 실행 전과 동일했다.

## 12. Persistent UAT와 isolated E2E

Latest main의 E2E isolation을 그대로 사용했다. Full-Stack E2E는 실행별 전용 container/network/tmpfs와 `emi_qms_e2e_*` DB를 사용했고 actual external provider는 Testing 설정에서 차단됐다. 종료 후 E2E container/network/volume 잔여는 0건이었다.

UAT-001은 `scripts/e2e-*`, E2E Compose, E2E spec을 수정하지 않는다.

## 13. DB persistence/schema 결과

Persistent UAT read-only snapshot:

- Database: `emi_qms_uat_005a`
- Migrations: 28, latest `0027_notification_access_scope_and_manual_work_items`
- Projects 22, work items 37
- Notifications 89, recipients 162, deliveries 92
- Escalations 2, users 14, departments 12, holidays 6
- Pending 0, Failed 20, Sent 59
- Active escalation 0

Full-Stack E2E 전후 container ID, restart count 0, schema와 위 핵심 count/delivery status는 동일했다. Persistent volume도 유지됐다.

## 14. 테스트 결과

| 검사 | 결과 |
| --- | --- |
| `git diff --check`, actionlint | 통과 |
| Bash 3.2 syntax / shellcheck | 통과 |
| Port, ownership, dotenv, transaction helper | 통과 |
| HTTP/HTTPS 5184 protocol readiness | 통과 |
| Backend Release build | warning/error 0 |
| Migration targeted | 16/16 |
| Authorization targeted | 47/47 |
| Notification targeted | 62/62 |
| Backend 전체 | 295/295 |
| Frontend unit | 57/57 |
| Mock UI | 1/1 |
| Full-Stack E2E | 16/16 |
| Browser console | error 0 |
| Narrow overflow | main/Teams/admin 0 |

기존 warning은 Fast Refresh 1건, frontend chunk-size와 Playwright color 환경 warning이다. 신규 warning과 test failure는 없다.

## 15. 보안/secret

- `.env`, `.env.notify-local`, certificate/private key 내용 미출력
- Actual credential, webhook URL, token, Authorization header 미기록
- Tracked env/certificate, migration, dependency/lockfile 변경 없음
- Notify parser는 namespace allowlist와 literal export 사용
- 문서에는 역할명과 집계만 기록하고 사용자/업무 row의 식별값을 기록하지 않음

## 16. 제한사항

- 현재 live HTTPS server는 원래 WIP로 시작됐으며 integration source와 startup/runtime 의미가 같다. Integration에서 추가된 path boundary는 다음 startup의 ownership 판정만 더 엄격하게 한다.
- 현재 5174 session을 보존하기 위해 실제 HTTP↔HTTPS 전환은 5184 격리 Vite로 검증했다. 5174 mode 전환은 사용자 checklist에 남긴다.
- 저장·수정과 actual external notification 발송은 자동 검증에서 실행하지 않았다.
- Frontend dependency security는 별도 P2다.

Rollback은 merge 전 branch commit을 되돌리는 방식이다. Startup 보호를 제거하면 기존 위험이 복원되므로 운영 문제는 forward-fix를 우선한다. DB/migration 변경이 없어 DB rollback은 적용 대상이 아니다.

## 17. 후속 Task

1. `TASK-FRONTEND-SEC-001`
2. `TASK-UAT-002`
3. `UAT-VERIFY-001`
4. `TASK-NOTIFY-REL-001`
5. `TASK-NOTIFY-ESC-001`
6. `TASK-AUTH-HARDEN-001`
7. `TASK-GOV-002`

## 18. 해결한 업무 문제

개발자가 HTTPS Teams 검수와 일반 HTTP 개발 사이를 전환할 때 잘못된 port/protocol 또는 다른 process를 정상 UAT로 오인할 가능성을 줄였다. Master-data startup 실패도 전체 rollback돼 부분 적용 상태를 남기지 않는다.

## 19. 기술적 결정과 검토한 대안

- 선택: HTTP/HTTPS 모두 5174 고정 + strict port
- 대안: mode별 별도 port — Teams manifest/deep link와 운영 안내가 분산돼 폐기
- 선택: cwd boundary + command를 결합한 ownership
- 대안: PID file만 신뢰 — stale/reused PID 위험 때문에 폐기
- 선택: literal dotenv parser + namespace filter
- 대안: `source`/`eval` — shell execution 위험 때문에 금지
- 선택: PostgreSQL transaction + `ON_ERROR_STOP`
- 대안: statement별 idempotency만 의존 — 중간 실패의 부분 성공을 막지 못해 폐기

## 20. 시행착오 및 폐기한 접근

- `git apply --3way`가 integration index에 tracked 변경을 stage해 지정 3개만 명시적으로 unstage했다. Working content는 원래 WIP와 동일하게 유지됐다.
- 최초 patch header 경로 추출식이 binary diff 형식과 맞지 않아 실패했다. 생성 파일과 원래 status를 확인한 뒤 `diff --git a/... b/...` 전용 추출식으로 재검증했다.
- 실제 5174 전환 테스트는 실행 중 UAT를 재시작하므로 폐기하고 5184 격리 protocol/occupancy 검사로 대체했다.
- 단순 cwd prefix ownership은 sibling 경로를 허용해 path boundary helper로 교체했다.

## 21. 사용자 검수 결과와 남은 항목

- Checklist 작성됨
- 자동 검증 완료
- 사용자 검수 완료
- 검수 증빙: Task 승인자 / 2026-07-10 / PR #23 및 HTTPS Development UAT / 승인 / 현재 대화의 명시적 검수·병합 승인
- Actual 외부 알림 smoke: 미실행, 신규 발송 금지 결정에 따름
- 자동 검증에서 저장/수정과 actual 외부 알림 신규 smoke는 실행하지 않았으며 사용자 검수 결과와 구분함

5종 산출물:

- Implementation report: 이 문서
- SOP: [TASK-UAT-001 SOP](uat-001-sop.md)
- User manual: [TASK-UAT-001 User Manual](uat-001-user-manual.md)
- Roadmap update: [Product Roadmap](../docs/00-product-roadmap.md#task-uat-001-https-development-uat-안정화)
- User validation checklist: [Task 정의 18장](uat-001-https-dev-stability.md#18-사용자-검수-체크리스트), 작성됨 / 자동 검증 완료 / 사용자 검수 완료

## 22. Change 001 — HTTPS-only runtime 통합

### 해결한 업무 문제

HTTP 5174가 Development frontend를 점유해 Teams Activity와 HTTPS 알림 검수 주소가 사라진 protocol drift를 해소했다. 로그인과 알림을 별도 frontend로 나누지 않고 HTTPS 5174 하나로 통합했다. 과거 격리 검증이 남긴 비영구 DB port 세 개도 승인 범위에서 정리했다.

### 기술적 결정과 대안

- 선택: HTTPS 5174 하나에서 로그인·일반 기능·알림·Teams Activity 제공
- 대안: HTTP/HTTPS를 서로 다른 port에 상시 유지 — 사용자 주소와 Entra/Teams origin이 늘어나 폐기
- 선택: `VITE_API_BASE_URL`을 비우고 `/api`·`/health`를 Backend 5081로 same-origin proxy
- 대안: HTTPS browser가 5081을 직접 호출 — 현재 Backend CORS origin을 바꾸려면 Backend restart가 필요해 승인 경계를 벗어나므로 폐기
- 선택: HTTPS 5186 candidate 검증 후 frontend-only cutover
- 대안: 5174 즉시 교체 — rollback 판단이 약해 폐기

### 시행착오와 보정

첫 candidate는 환경 인자가 실행 명령 뒤에 붙어 Vite가 이를 root 경로로 해석했고 HTTP 404로 기동됐다. 기존 runtime에는 영향이 없었으며 candidate만 종료했다. 환경 인자를 command 앞에 삽입하도록 orchestration을 보정한 두 번째 candidate에서 trusted HTTPS와 proxy 검증을 통과했다.

### 실제 결과

- HTTPS 5174: root, notifications, Teams Activity, live/ready, runtime API 200
- HTTP 5174: 실패
- Desktop/390px browser 6/6: HTTPS, root와 로그인 action 확인, console/request error 0, horizontal overflow 0
- Backend 5081, Review-safe 5092/5190, design preview 5176 보존
- 최초 Frontend handover의 Backend 5081은 Development/ready, mutation·external provider capability 허용, background worker 비활성이었다.
- Persistent PostgreSQL running/healthy, restart count 0, named volume 보존
- obsolete isolated container/network 3/3 제거, 동적 port 51061/51642/55433 해제
- 제품 source, API, DB, migration, dependency와 lockfile 변경 0
- 최초 자동 검증에서는 실제 Entra 로그인, 저장·수정과 외부 알림 신규 발송 미실행

### 후속 Notification Delivery Worker handover

- 사용자 검수에서 `https://localhost:5174` Microsoft 365 로그인 성공을 확인했다.
- 승인 직전 privacy-safe aggregate는 Teams Activity Pending 1, due 1, attempt 0이며 다른 Pending channel은 0이었다.
- Backend 5081만 재기동해 `Notifications:Dispatch:Enabled=true`, `Notifications:Escalation:Enabled=false`, `AdminDeletionPurge:Enabled=false`를 적용했다.
- 재기동 후 runtime은 Development/ready, delivery worker enabled, Escalation·Purge disabled, external provider capability enabled다.
- 대상 delivery는 attempt 1회 후 `TeamsActivityDisabled`로 `Disabled`가 됐다. Graph provider 호출 전 configuration guard에서 종료했으므로 실제 외부 알림은 발송되지 않았다.
- 이후 같은 원인의 terminal 2건은 audit로 보존했고 상태를 변경하거나 재처리하지 않았다.
- 추가 승인으로 Backend 5081만 재기동해 Teams Activity channel을 actual mode로 활성화했다. Local ignored dotenv의 Graph credential과 Teams app 설정은 값 출력 없이 runtime에 전달했고 파일은 변경하지 않았다.
- 로그인된 HTTPS 5174 수동 발송 화면에서 Mail을 제외하고 Teams Activity 수신자 1명만 선택해 신규 ManualTest delivery 1건을 생성했다.
- 최초 channel restart에서 Graph credential 전달이 빠져 동일 delivery의 시도 1·2가 `TeamsActivityGraphConfigMissing`으로 retry 예약됐다. 5081만 보정 재기동한 뒤 시도 3이 `Sent`로 완료됐으며 신규 delivery 추가 생성은 0건이다.
- 최종 privacy-safe aggregate는 기존 `TeamsActivityDisabled` terminal 2건 보존, 신규 ManualTest `Sent` 1건, 전체 Pending/Processing 0/0이다. Provider `Sent`는 Microsoft Graph actual 요청 수락을 증명하고 사용자가 Teams client Activity Feed의 실제 표시까지 확인했다.
- Frontend 5174/5176, Review-safe 5190/5092, Persistent PostgreSQL health·restart 0·mount 2를 보존했다.

### 사용자 검수와 산출물

- 자동 검증 완료
- 사용자 검수 완료: 실제 Microsoft 365 로그인
- 사용자 검수 완료: 로그인 상태 유지·재인증, 기존 알림·Teams Activity 조회
- 실제 외부 delivery 검수 완료: 신규 ManualTest Teams Activity 1건이 Microsoft Graph provider `Sent`
- 사용자 검수 완료: Teams client Activity Feed의 실제 표시 확인
- 게시 승인: commit, push, PR과 merge까지 사용자 승인
- Change contract/checklist: [Change 001](uat-001-change-001.md)
- SOP: [이 Task SOP 0장](uat-001-sop.md#0-현재-운영-계약--change-001)
- User manual: [이 Task User Manual 17장](uat-001-user-manual.md#17-change-001-사용자-검수)
- Roadmap: [Product Roadmap](../docs/00-product-roadmap.md#task-uat-001-https-development-uat-안정화)
- 게시 기록: 승인된 문서 6개만 commit·push하고 Draft PR #48을 생성했다. 사용자가 잔여 검수 2건을 모두 확인하고 squash merge를 승인했으며, 최종 게시 상태는 PR을 source of truth로 확인한다.

## 23. Change 002 — Entra 로그인 보안 게이트 복구

### 목적과 변경

- Frontend dependency audit의 High 5건을 patched dependency와 lockfile로 제거했다.
- 공통 UAT startup이 HTTPS wrapper의 EntraId 선택을 Dev로 덮어쓰던 drift를 수정했다.
- tenant/client가 없거나 예시 all-zero identifier이거나 HTTPS가 아니면 값 노출 없이 fail-closed한다.
- 표준 Backend audience와 Frontend delegated API scope는 client identifier에서 파생하고 명시적 override를 우선한다.
- 기존 Persistent UAT DB를 변경하지 않고 runtime만 교체할 때 사용하는 `UAT_SKIP_DATABASE_SETUP` 보호 경로를 추가했다.
- 전역 authorization fallback을 인증된 운영 사용자 정책으로 고정해 metadata가 누락된 새 endpoint도 익명 접근을 fail-closed한다.
- `/api/runtime-mode`는 Microsoft 365 인증과 운영 사용자 확인을 요구하고, 공개 `/health/ready`는 내부 환경·DB 원인·worker·provider 상세를 제거한 최소 상태만 반환한다.
- 익명 허용 endpoint를 `/health/live`, `/health/ready`로 제한하고 모든 `/api/*` authorization metadata를 자동 검사한다.

### 자동 검증

| 검사 | 결과 |
| --- | --- |
| Frontend dependency audit | Critical/High/Moderate/Low 0 |
| Frontend lint/typecheck/build | 통과 |
| Frontend unit | 142/142 |
| Backend vulnerable package audit | 0건 |
| Shell syntax | 통과 |
| Auth configuration self-test | 8/8 |
| Entra candidate root/health | 통과 |
| 익명·invalid bearer·Dev header 차단 | 모두 401 |
| Entra JIT targeted concurrency | 2/2 |
| Identity infrastructure 전체 | 17/17 |
| P2 authorization/review-safe/worker 회귀 | 65/65 |
| Backend 전체 isolated PostgreSQL 회귀 | 435/435 |
| 익명 endpoint allowlist와 API authorization metadata | 통과 |

### 실제 로그인 설정 복구

- Microsoft authorize redirect까지는 정상 동작했다.
- 첫 조사는 실험 워크트리의 기본 `.env`만 확인해 예시 placeholder를 실제 기준으로 오인했고 authorize endpoint가 `AADSTS900021`로 tenant를 거절했다.
- 후속 조사에서 대표 저장소의 Git ignored `.env.entra-local`과 `.env.notify-local`에 기존 실제 설정이 그대로 있음을 확인했다. 새 worktree가 ignored local file을 공유하지 않는 것이 누락 원인이다.
- `.env`, `.env.entra-local`, `.env.notify-local`을 실험 워크트리로 byte-identical하게 복사하고 mode `600`, Git ignored 상태를 확인했다.
- Startup은 기존 local file의 `AzureAd__*`·`VITE_AZURE_*` identifier 조합을 해석하고 Backend/Frontend 불일치를 fail-closed하도록 보정했다. Notification local env는 명시적 actual 발송 Task가 아니면 기본적으로 로드하지 않는다.
- 실제 Chrome 오류는 `AADSTS50011`이었고 local env가 HTTP callback path를 보내는 반면 Entra SPA에는 exact HTTPS origin이 등록돼 있었다. 회신 주소를 `https://localhost:5174`에 맞추고 5174 local Entra config가 다른 protocol·origin·path를 거절하도록 보강했다.
- 회신 주소 수정 뒤 Microsoft token signature와 claims는 유효했지만 복구한 env의 DB password가 현재 PostgreSQL container와 달라 `/api/me`가 실패했다. Candidate Backend는 DB를 변경하지 않고 실행 중인 container의 현재 password를 값 비출력으로 사용하도록 수정했다.
- 첫 인증 API 여러 건이 같은 사용자를 동시에 JIT 생성해 unique constraint가 충돌할 수 있어 object ID별 transaction advisory lock을 추가했다. 8개 동시 요청과 Identity infrastructure 17건이 isolated PostgreSQL에서 통과했다.
- 식별자 원문, token, credential과 authorize URL은 문서·log·증빙에 기록하지 않았다.

최종 Chrome 검수에서 Microsoft 로그인, `/api/me`, home metrics, 알림·내 업무·Pending·공지 초기 조회가 모두 성공했고 Dashboard가 표시됐다. Change 002는 `실제 로그인과 인증 API 복구 완료 / 사용자 화면 최종 확인 가능`이다. 게시·merge는 별도 승인 전까지 실행하지 않는다.

후속 보안 권고 P2 검토에서 익명 runtime 상태 노출과 전역 fallback 부재를 확인했다. runtime 상태는 인증된 운영 사용자만 조회할 수 있고, 공개 health는 내부 운영 정보를 노출하지 않으며, 이후 API가 개별 authorization metadata를 실수로 빠뜨려도 전역 정책이 차단한다. 이 보완은 schema, migration, data와 실제 provider 설정을 변경하지 않는다.

### Runtime·data·provider 보존

- experiment 사용자 검수 주소 42983/41166은 Dev 인증으로 열었다.
- actual Teams/Mail/Teams Activity 발송은 실행하지 않았다.
- schema, migration, seed, data reset과 Persistent UAT DB 삭제는 실행하지 않았다.
- 향후 병합 승인 기준은 자동 검증·사용자 검수 뒤 명시적 1회다. 이번 정책 변경 메시지는 Change 002의 merge 실행 승인이 아니다.

## 24. Change 003 — 공개 배포 P0 노출 차단

### 해결한 보안 문제

- `SEC-PUBLIC-001` P0: HTTPS 5174 Vite Development server가 `/src`와 `/@fs`를 제공했고 `/@fs`로 Frontend 바깥의 Backend source, DB schema와 Repository instruction을 읽을 수 있었다.
- `SEC-PUBLIC-002` P0: 공통 Compose PostgreSQL port가 wildcard IPv4·IPv6 interface에 publish됐다.

### 구현

- Development와 preview server listener를 loopback으로 고정하고 CLI의 non-loopback override도 `configResolved` 단계에서 거절한다.
- Vite file-system allowlist를 Frontend와 workspace dependency root로 제한한다.
- 기본 `pnpm dev` command도 `127.0.0.1`을 사용한다.
- 공통 PostgreSQL Compose publish address를 `127.0.0.1:${DATABASE_PORT}:5432`로 제한한다.
- 기존 Persistent UAT PostgreSQL은 현재 credential을 값 비출력으로 승계하고 같은 named volume으로 container만 재생성했다.

### 자동·runtime 검증

| 검사 | 결과 |
| --- | --- |
| Frontend non-loopback Development 시작 | startup 전 거절 |
| Frontend non-loopback preview 시작 | startup 전 거절 |
| Loopback 후보 root / Frontend source | `200` / `200` |
| 후보 Backend source / DB schema / Repository instruction `/@fs` | `403` / `403` / `403` |
| 실제 HTTPS 5174 root / Backend source `/@fs` | `200` / `403` |
| Compose rendered PostgreSQL host IP | `127.0.0.1` |
| 실제 PostgreSQL listener | `127.0.0.1:5432`만 존재 |
| PostgreSQL health / restart count | `healthy` / `0` |
| Named volume | 기존 identity 보존 |
| DB aggregate 전후 | `13/2/0` 동일 |
| Backend live / ready | `200` / `200` |
| Frontend lint | error 0, 기존 warning 1 |
| Frontend typecheck / build | 통과 / 통과 |
| Frontend unit | 142/142 |
| `git diff --check`, Compose config | 통과 |

### 판정

- `SEC-PUBLIC-001` P0: `RESOLVED`
- `SEC-PUBLIC-002` P0: `RESOLVED`
- migration, seed, data reset과 실제 provider 발송은 실행하지 않았다.
- P1 운영 hosting·security header·Host/forwarded proxy·rate limit·upload quarantine·Production Entra·backup/monitoring이 남아 공개 배포 전체는 계속 `NO_GO`다.

## 25. Change 004 — 공개 배포 P1 방어선과 운영 준비 게이트

### 해결한 업무 문제

- `SEC-PUBLIC-003` P1: 보안 header와 Development server가 아닌 운영 정적 hosting 기준이 없었다.
- `SEC-PUBLIC-004` P1: Host와 `X-Forwarded-*` 신뢰 경계가 명시되지 않았다.
- `SEC-PUBLIC-005` P1: 사용자/IP 단위 요청 제한이 없어 반복·자동 공격의 비용을 제한하지 못했다.
- `SEC-PUBLIC-006` P1: multipart 파일이 malware 검사 없이 endpoint 저장 로직으로 들어갔다.
- `SEC-PUBLIC-007` P1: Production Entra/domain/redirect/CORS가 구조적으로 잘못돼도 startup을 시도할 수 있었다.
- `SEC-PUBLIC-008` P1: secret 주입, DB TLS, restore 검증, SIEM과 비상 관리자 준비가 운영 시작 조건이 아니었다.

### 기술적 결정과 검토한 대안

- 선택: Production startup에서 모든 운영 보안 조건을 한 번에 fail-closed 검증한다.
- 폐기: 누락 조건을 warning만 남김 — 공개 runtime이 불완전한 상태로 실행될 수 있어 사용하지 않았다.
- 선택: exact Host, exact HTTPS frontend origin과 exact trusted proxy IP 한 단계만 허용한다.
- 폐기: wildcard Host/CORS와 모든 proxy 신뢰 — Host header·scheme spoofing 경계가 사라져 사용하지 않았다.
- 선택: Backend의 사용자/IP rate limit과 Nginx IP limit을 함께 적용한다.
- 폐기: reverse proxy limit 하나만 사용 — 로그인 뒤 사용자 단위 분리가 없어 사용하지 않았다.
- 선택: multipart form을 endpoint보다 먼저 scan하고 malware·scanner 장애·민감 image metadata를 저장 전에 거절한다.
- 폐기: 저장 후 비동기 scan — 감염 파일이 storage와 다른 처리기로 노출되는 window가 생겨 사용하지 않았다.
- 선택: public TLS static frontend 하나만 publish하고 Backend·ClamAV를 internal network에 둔다.
- 폐기: Vite Development server와 Backend port 직접 공개 — Change 003의 로컬 전용 계약과 충돌해 사용하지 않았다.
- 선택: container secret file, `SSL Mode=VerifyFull`, 최근 restore 시각, TLS syslog SIEM과 비상 관리자 2명을 startup 조건으로 강제한다.

### 구현

- Backend: Production security policy, Key-per-file secret 주입, Host filtering, known-proxy forwarded header, HSTS·CSP 등 응답 header, 전역 category별 rate limit을 추가했다.
- Upload: ClamAV INSTREAM client, endpoint 전 middleware, 32MB file/34MB multipart boundary, fail-closed scanner 장애, JPEG EXIF·PNG metadata 차단을 추가했다.
- Frontend/Infrastructure: multi-stage production build, unprivileged Nginx TLS static hosting, same-origin API proxy, internal Backend·scanner network, TLS syslog, non-root/read-only container 경계를 추가했다.
- 운영 gate: 실제 domain·redirect, Entra 식별자, DB VerifyFull, 최근 90일 restore, SIEM sink와 관리자 2명이 없으면 Backend가 시작되지 않는다.
- 인증 회귀 보정: 인증이 필수가 된 `/api/runtime-mode`에 Development 사용자 header를 전달하고, Entra mode에서는 기존 Bearer token 경로를 유지한다.
- schema, migration, 업무 data와 API response contract는 변경하지 않았다.

### 자동 검증

| 검사 | 결과 |
| --- | --- |
| Production policy·Host·header·rate·upload targeted | 25/25 통과 |
| Backend Release build·전체 isolated PostgreSQL 회귀 | build 통과, 459/459 통과 |
| Frontend lint·typecheck·unit·build | error 0·기존 warning 1, typecheck 통과, 143/143, build 통과 |
| Mock UI·Full-Stack E2E | 4/4, 55/55 통과 |
| Production Compose·TLS script·diff·secret projection | 모두 통과 |
| Frontend/Backend dependency audit | 알려진 취약점 0 |
| Production Backend/Frontend/ClamAV image build·Scout | build 통과, Critical/High/Moderate/Low 모두 0 |
| Frontend 정적 image source map·TypeScript 포함 여부 | 0건 |
| 합성 31일 TLS·Host/header runtime | root 200, 필수 header 7종, 잘못된 Host 차단 |
| 합성 1일 TLS certificate | 시작 전 거절 |
| 실제 운영 domain·certificate·Entra·DB·SIEM | 미실행 — 운영값과 별도 handover 승인 없음 |

### 시행착오 및 폐기한 접근

- .NET 10에서 폐기된 forwarded network API와 Npgsql option을 첫 build가 오류로 잡아 새 API와 `VerifyFull` 단일 계약으로 보정했다.
- 익명 unknown endpoint를 사용한 upload middleware 첫 회귀는 전역 authorization fallback이 scan보다 먼저 차단했다. 실제 계약대로 인증 사용자 header를 사용해 scan 분기를 검증했다.
- Nginx unprivileged image의 runtime template 생성을 위해 read-only root를 유지하면서 `/etc/nginx/conf.d`만 tmpfs로 분리했다.
- Frontend production build argument가 단순 non-empty 검사만 통과할 수 있어 GUID·HTTPS·예약 domain fail-closed validation을 추가했다.
- 첫 Frontend image는 base image package의 취약점과 runtime `openssl` 누락을 확인했다. 보안 업데이트를 image build에 적용하고 TLS 검증에 필요한 최소 runtime package를 명시한 뒤 재검사에서 모든 심각도 0을 확인했다.
- 전체 E2E 첫 실행은 보호된 runtime-mode API가 Development 인증 header를 받지 못해 화면을 읽기 전용으로 잠갔다. endpoint를 익명으로 되돌리지 않고 Dev header를 전달하도록 보정했으며, targeted 3/3과 전체 55/55로 재검증했다.
- 장시간 Backend 전체 검사는 초기 출력이 적었지만 중단 상태로 추정하지 않고 최종 격리 실행을 완료해 459/459를 확정했다.
- 중단된 첫 E2E의 전용 PostgreSQL container/network가 Docker 재기동 뒤 Exited 상태로 남아, 전용 Compose project만 제거했다. Persistent UAT container는 삭제하거나 재생성하지 않았다.

### Finding closure

| Finding | 심각도 | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `SEC-PUBLIC-003` | P1 | `RESOLVED` | 보안 응답 header와 운영 정적 hosting 기준 부재 | CSP 등 header, HSTS, unprivileged Nginx TLS static hosting 추가 |
| `SEC-PUBLIC-004` | P1 | `RESOLVED` | Host·forwarded header 신뢰 경계 부재 | exact Host와 known proxy 1단계만 신뢰 |
| `SEC-PUBLIC-005` | P1 | `RESOLVED` | 자동·반복 요청 비용 제한 부재 | 사용자/IP 및 요청 category별 fixed-window limit 추가 |
| `SEC-PUBLIC-006` | P1 | `RESOLVED` | upload 저장 전 malware·metadata 검사 부재 | endpoint 전 ClamAV·metadata 검사와 scanner 장애 fail-closed 추가 |
| `SEC-PUBLIC-007` | P1 | `RESOLVED` | 잘못된 Entra/domain/redirect/CORS로 운영 시작 가능 | Production startup exact-value 검증 추가 |
| `SEC-PUBLIC-008` | P1 | `RESOLVED` | secret·DB TLS·restore·SIEM·비상 관리자 준비가 운영 조건이 아님 | key-per-file과 운영 준비 fail-closed gate 추가 |
| `SEC-PUBLIC-009` | P0 | `RESOLVED` | 최초 Production image package 취약점이 공개 image에 포함될 수 있었음 | 보안 업데이트 후 Backend·Frontend·ClamAV Scout 전 심각도 0 확인 |
| `SEC-PUBLIC-010` | P1 | `RESOLVED` | 인증된 runtime-mode 전환 뒤 Dev shell이 인증 header를 보내지 않아 모든 mutation이 잠김 | Dev header 전달 단위 회귀와 Full-Stack E2E 55/55 추가 |

Open Finding은 P0/P1/P2/P3 `0/0/0/0`이다.

### 개인정보·secret·runtime 영향

- 실제 tenant/client/object ID, token, password, connection string, certificate key, 사용자·프로젝트 원문을 출력하거나 추적하지 않았다.
- `.dockerignore`가 ignored env, certificate, secret, build/test artifact를 image build context에서 제외한다.
- 실제 provider 발송, migration, seed, data reset과 Persistent UAT runtime restart를 실행하지 않았다.
- Docker Desktop 재기동 이후 기존 local UAT frontend/backend와 Persistent PostgreSQL은 현재 중지 상태다. Change 004 자동 검증은 별도 격리 runtime에서 완료했으며, actual provider 설정이 연결된 local UAT는 별도 runtime 복구 승인 없이 재기동하지 않았다.

### 사용자 검수 결과와 남은 항목

- 사용자 검수 checklist: [User Manual 18장](uat-001-user-manual.md#18-change-004-공개-서비스-보안-안내)
- 상태: `Checklist 작성됨`, `자동 검증 완료`, `실제 운영 환경 검수 대기`
- 실제 운영 domain·certificate·Entra registration·managed DB·restore·SIEM receiver가 제공되면 별도 운영 전환 Task에서 handover와 rollback을 승인받아 검수한다.
- 코드 게시 품질 gate는 `GO`다. 이는 commit·push·PR·merge 승인이나 실제 공개 배포 승인이 아니다.
- 실제 공개 배포 gate는 `NO_GO_EXTERNAL`이다. 누락된 실제 운영값을 placeholder로 대체하거나 startup gate를 끄지 않는다.

### 5종 산출물

- Implementation report: 이 문서 25장
- SOP: [TASK-UAT-001 SOP Change 004](uat-001-sop.md#change-004-운영-보안-게이트)
- User manual/checklist: [TASK-UAT-001 User Manual 18장](uat-001-user-manual.md#18-change-004-공개-서비스-보안-안내)
- Roadmap update: [Product Roadmap TASK-UAT-001](../docs/00-product-roadmap.md#task-uat-001-https-development-uat-안정화)
- User validation checklist: 작성됨 / 자동 검증 완료 / 실제 운영 환경 검수 대기

## 26. Change 005 — 공개 배포 P2 Cache·header 상속·공급망 보정

### 해결한 업무 문제

- `SEC-PUBLIC-011` P2: 인증된 API 응답에 전역 cache 차단이 없어 공유 단말에 업무 응답이 남을 수 있었다.
- `SEC-PUBLIC-012` P2: Nginx asset location의 별도 `add_header`가 상위 보안 header 상속을 끊었다.
- `SEC-PUBLIC-013` P2: Production image, CI PostgreSQL과 GitHub Action이 가변 tag를 사용했다.

### 기술적 결정과 대안

- Backend 전 응답에 `private, no-store`를 적용했다. endpoint별 opt-in은 누락 가능성이 있어 폐기했다.
- HTML은 `no-cache`, fingerprint asset만 장기 cache한다. 모든 응답을 no-store로 만드는 방식은 보안 이점 없이 asset 성능을 잃어 폐기했다.
- Docker image는 tag+digest, Action은 full commit SHA로 고정했다. tag-only는 같은 source에서 다른 artifact가 실행될 수 있어 폐기했다.
- Backend final runtime은 Microsoft Azure Linux distroless-extra를 사용한다. 최초 Ubuntu runtime scan의 불필요한 package Finding을 줄이면서 .NET runtime 호환성을 합성 기동으로 확인했다.

### 구현

- Backend security middleware에 cache 차단 header를 추가했다.
- Nginx asset location의 header override를 제거하고 HTML/asset cache 기간만 분리했다.
- SDK·Backend runtime·Node·Nginx·ClamAV·TLS validator·CI PostgreSQL을 검증 digest로 고정했다.
- GitHub Action을 full SHA로 고정하고 최소 workflow 권한과 checkout credential 비저장을 적용했다.
- immutable external reference와 Nginx 상속 구조의 Repository 회귀를 추가했다.
- Frontend image의 가변 Alpine package 설치를 제거하고 인증서 만료·hostname·key 검사를 고정 one-shot validator로 분리했다.
- Production handover 문서에 digest 갱신, 전체 재검증과 rollback 절차를 추가했다.

### 자동 검증

| 검사 | 결과 |
| --- | --- |
| 공개 배포 보안 targeted | 27/27 통과 |
| Backend Release 전체 isolated PostgreSQL | 461/461 통과 |
| Frontend lint·typecheck·unit·build | error 0·기존 warning 1, 통과, 143/143, 통과 |
| Mock UI·Full-Stack E2E | 4/4, 55/55 통과 |
| Frontend audit·NuGet 취약 package | 전 심각도 0, 0건 |
| Production Compose·Actionlint | 통과 |
| Backend·Frontend Production image build | 통과 |
| Backend final image scan | Critical/High/Medium/Low `0/0/0/0` |
| Frontend final image scan | Critical/High/Medium/Low/Unspecified `0/0/0/2/2`; Unspecified 2건은 영향 binary 부재 |
| ClamAV pinned image scan | Critical/High/Medium/Low/Unspecified `0/0/0/2/2`; Unspecified 2건은 영향 binary 부재 |
| TLS validator image scan·실행 | 전 심각도 0, 만료·hostname·key 일치 검증 통과 |
| 합성 Backend final runtime | `/health/live` 200, cache·보안 header 통과 |
| 합성 Nginx TLS runtime | HTML·asset 200, cache 분리와 보안 header 통과 |

### 시행착오와 정리

- 첫 Backend 전체 검사는 중지된 기존 Persistent PostgreSQL 때문에 DB 의존 test가 연결 실패했다. Persistent 자원을 건드리지 않고 Task 전용 격리 DB로 재실행해 461/461을 확정했다.
- 최초 Ubuntu Backend runtime image는 Medium 17건을 포함했다. Microsoft 공식 distroless-extra runtime으로 최소화하고 합성 runtime을 기동해 기능과 header를 다시 확인했다.
- 기존 Nginx runtime은 Medium 4건이었다. 공식 최신 고정 image로 갱신해 Critical/High/Medium을 모두 제거했다.
- 최종 재현성 검토에서 Frontend image의 `apk add --upgrade`가 base digest 밖의 가변 입력임을 확인했다. Package 설치를 제거하고 고정 digest TLS validator를 one-shot prerequisite로 분리해 같은 source가 같은 final image를 만들도록 보정했다.
- Task 전용 PostgreSQL, Backend, Nginx container와 network는 검증 뒤 제거했다. Persistent PostgreSQL과 다른 runtime은 재시작·삭제하지 않았다.

### Finding closure

| Finding | 심각도 | 상태 | 해소 |
| --- | --- | --- | --- |
| `SEC-PUBLIC-011` | P2 | `RESOLVED` | 전역 private no-store와 자동 회귀 |
| `SEC-PUBLIC-012` | P2 | `RESOLVED` | asset header 상속 복구와 합성 TLS 검증 |
| `SEC-PUBLIC-013` | P2 | `RESOLVED` | image digest·Action full SHA 고정과 Repository 회귀 |
| `SEC-PUBLIC-014` | P3 | `BACKLOG` | Frontend·ClamAV image의 libxml2 Low 2건. 2026-07-29 scanner 기준 수정 버전 없음; 운영 handover·digest 갱신 전 재검사 |
| `SEC-PUBLIC-015` | P3 | `RESOLVED_NOT_AFFECTED` | `CVE-2026-11979`의 `xmlcatalog --shell`과 `CVE-2026-58055`의 `nghttpx`가 두 final image에 없음을 실행 검사로 확인 |

Open Finding은 P0/P1/P2/P3 `0/0/0/1`이다.

### 개인정보·runtime·게시 영향

- 실제 identifier, token, password, connection string, 인증서 key, 사용자·업무 원문을 출력하거나 추적하지 않았다.
- DB schema/data, 실제 provider, Persistent UAT와 운영 runtime을 변경하지 않았다.
- 코드 게시 품질 gate는 `GO`지만 commit·push·PR·merge는 승인되지 않아 실행하지 않았다.
- 실제 공개 배포는 운영 domain·certificate·Entra·managed DB·restore·SIEM handover 전까지 `NO_GO_EXTERNAL`이다.

### 사용자 검수와 5종 산출물

- Implementation report: 이 문서 26장
- SOP: [TASK-UAT-001 SOP 22장](uat-001-sop.md#22-change-005-cache공급망-운영-절차)
- User manual/checklist: [TASK-UAT-001 User Manual 19장](uat-001-user-manual.md#19-change-005-공유-pc와-업데이트-보안-안내)
- Roadmap update: [Product Roadmap TASK-UAT-001](../docs/00-product-roadmap.md#task-uat-001-https-development-uat-안정화)
- User validation checklist: `작성됨` / 자동 검증 완료 / 사용자 검수 대기
