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
- 사용자 검수 대기: 로그인 상태 유지·재인증, 기존 알림·Teams Activity 조회
- 실제 외부 delivery 검수 완료: 신규 ManualTest Teams Activity 1건이 Microsoft Graph provider `Sent`
- 사용자 검수 완료: Teams client Activity Feed의 실제 표시 확인
- 게시 승인: commit, push, PR과 merge까지 사용자 승인
- Change contract/checklist: [Change 001](uat-001-change-001.md)
- SOP: [이 Task SOP 0장](uat-001-sop.md#0-현재-운영-계약--change-001)
- User manual: [이 Task User Manual 17장](uat-001-user-manual.md#17-change-001-사용자-검수)
- Roadmap: [Product Roadmap](../docs/00-product-roadmap.md#task-uat-001-https-development-uat-안정화)
- Commit, push, PR과 merge: 미실행
