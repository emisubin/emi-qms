# TASK-UAT-002 Implementation Report

## 1. 목적

Persistent UAT 데이터를 조회하되 startup write, background mutation, 외부 delivery, HTTP mutation과 DB write를 겹쳐 차단하는 Review-safe UAT를 구현한다. Development UAT는 기존 write/worker/provider 검수 환경으로 유지한다.

## 2. 배경

기존 Development UAT는 실제 업무 흐름 검수를 위해 migration, seed/upsert, mutation API, worker와 외부 delivery를 사용할 수 있다. 기준선 감사와 조회 검수에는 이 기능이 오조작 위험이므로, frontend 버튼 숨김이 아닌 backend authoritative mode와 DB read-only까지 포함한 별도 runtime이 필요했다.

## 3. 기존 위험

- Review 목적이어도 startup migration과 development seed가 실행될 수 있음
- hosted worker가 delivery/escalation/purge 상태를 변경할 수 있음
- actual provider credential과 client가 process에 로드될 수 있음
- UI를 우회한 직접 mutation API 호출 가능
- application guard 누락 시 DB write 가능
- Entra 첫 GET 인증에서도 JIT 사용자 upsert 가능
- schema가 뒤처진 DB를 자동 복구하면 Review가 데이터를 변경함

## 4. 구현 범위

- `ReviewSafe:Enabled`/`REVIEW_SAFE_ENABLED` runtime mode와 Production 오설정 차단
- `/api/runtime-mode`와 Review-safe readiness
- startup migration/seed, mutation worker, actual provider 미등록
- unsafe HTTP method와 method override의 423 차단
- Entra JIT 대신 기존 사용자 read-only 조회
- PostgreSQL `default_transaction_read_only=on`, 전용 `application_name`
- frontend banner, fail-closed API client, mutation action disabled UX
- HTTP/HTTPS Review-safe startup script
- backend/frontend/DB/browser/Development 회귀 테스트

## 5. 제외 범위

- migration 또는 DB role/schema 변경
- dependency/lockfile 변경
- Development UAT 정책 변경
- 실제 외부 알림 smoke와 테스트 데이터 생성
- Review-safe를 production deployment mode로 사용하는 기능
- claim/lease, escalation starvation, 마지막 관리자 동시성 및 신규 업무 기능

Excel/PDF/첨부파일 포맷 영향은 없다. import/upload/apply action은 Review-safe에서 차단된다.

## 6. 전체 아키텍처

Review-safe는 다음 순서로 방어한다.

1. 명시적 mode와 허용 환경 검증
2. startup mutation skip/direct-call guard
3. mutation hosted service와 actual provider DI 미등록
4. auth/endpoint 실행 전 unsafe HTTP method 423 차단
5. GET 인증의 Entra JIT write 제거
6. 모든 review DB connection의 session read-only 강제
7. DB read-only/schema/DI 정책을 readiness와 runtime endpoint로 노출
8. frontend가 backend state를 조회해 banner/action disabled를 제공

서버/DB 차단이 보안 기준이며 frontend는 사용자 실수 방지 계층이다.

### 6.1 수정 파일

- Backend mode/guard/status: `Program.cs`, `appsettings.json`, `ReviewSafe/*`, health response
- DB/startup/identity: connection provider, migration runner, development seeder, Entra claims transformation, identity store
- 조회 회귀 수정: `ProductionPlanning/SystemHolidayStore.cs`의 nullable date parameter typing
- Backend tests: Review-safe targeted test, migration/DB integration, holiday range regression, test factory/config isolation
- Frontend: `App.tsx`, `api.ts`, `styles.css`, `App.test.tsx`
- Scripts: Review-safe HTTP 공통 startup, HTTPS wrapper
- Docs: Task 정의, report, SOP, user manual, Product Roadmap

삭제 파일, migration, dependency/lockfile 변경은 없다.

## 7. Runtime mode

- 기본값: `false`
- 허용 환경: `Development`, `UAT`
- 그 밖의 환경에서 `true`: startup 실패
- 누락/조회 실패: Review-safe로 추정하지 않으며 frontend mutation은 fail-closed
- 응답: mode, reviewSafe, mutation/worker/provider/DB/migration 상태, environment, expected/actual migration만 제공
- connection string, credential, identifier 원문은 제공하지 않음

## 8. Startup mutation 차단

Review-safe이면 startup migration 조건을 평가해도 runner를 호출하지 않고, `DatabaseMigrationRunner.ApplyAsync` 직접 호출도 예외로 차단한다. `DevelopmentIdentitySeeder.IsEnabled`는 false이며 direct seed 호출도 예외다. DB가 repository schema보다 뒤처지면 자동 migration하지 않고 readiness 503과 `schema_mismatch`를 반환한다.

## 9. Worker 차단

Review-safe DI에는 다음 mutation hosted service를 등록하지 않는다.

- `NotificationDeliveryWorker`
- `NotificationEscalationWorker`
- `AdminDeletionPurgeWorker`

Daily Digest/retry는 delivery dispatcher 내부 경로이므로 worker 미등록으로 함께 차단된다. 실제 process를 5분 관찰했으며 worker startup log와 delivery/escalation/purge 변화가 없었다.

## 10. Provider 차단

Review-safe DI에는 Teams Channel, Teams Activity, Mail handler와 actual Graph/SMTP/Webhook client를 등록하지 않는다. 공휴일 목록 GET의 store 의존성은 외부 HTTP를 사용하지 않는 Review-safe 전용 provider로 대체한다. Review script는 `.env.notify-local`을 읽지 않고 provider/identity secret 변수를 제거한 뒤 모든 delivery 설정을 disabled로 명시한다. Dry-run delivery도 DB write이므로 생성하지 않는다.

## 11. HTTP mutation middleware

- 허용: GET, HEAD, OPTIONS
- 차단: POST, PUT, PATCH, DELETE
- method override: unsafe override도 차단
- 응답: `423 Locked`, `UatReviewReadOnly`, 고정 한글 안내
- 적용 위치: CORS 다음, authentication/authorization/endpoint 실행 전

실제 5092에서 query string/content body가 있는 대표 호출을 포함해 네 method가 모두 423이었다. 최초 수동 curl 반복문은 zsh의 unquoted `?` glob 때문에 요청이 실행되지 않았고, URL을 quote한 동일 검증으로 교정해 4/4 차단을 확인했다.

## 12. DB read-only

`DatabaseConnectionStringProvider`가 review mode에서 모든 connection string에 다음 값을 적용한다.

- application name: `emi-qms-uat-review`
- options: `default_transaction_read_only=on`

별도 임시 테스트 DB에서 SELECT 성공, INSERT/UPDATE/DELETE 실패, explicit transaction 실패, pool 재사용 후 read-only 유지, write row 0을 확인했다. 실제 Review runtime status도 DB read-only를 `true`로 보고했다. persistent UAT schema/user는 변경하지 않았다.

## 13. Readiness/health

- live: process가 살아 있으면 200
- ready: DB 연결, session read-only, review application name, expected/actual latest migration 일치가 모두 필요
- mismatch/unreachable/not configured: stable reason과 503
- non-review Development ready 응답 계약은 기존 `database` 필드를 유지
- runtime mode와 ready는 같은 status service를 사용해 모순을 방지

## 14. Frontend UX

- 모든 shell 화면에 고정 Review-safe banner 표시
- runtime mode 확인 중/실패도 mutation fail-closed
- API client가 unsafe request를 전송 전에 423으로 거부
- mutation keyword와 file input 기반 공통 guard가 button을 disabled 처리하고 이유 title/ARIA 표시
- navigation, tab, 검색, 필터, 정렬, 상세 이동은 유지
- 서버 middleware와 DB read-only가 누락 UI의 최종 방어

주요 11 route에서 banner/조회/heading을 확인했고, 관리자 사용자·휴일·delivery·수동 발송 action은 disabled와 이유 표시를 확인했다.

## 15. Scripts

- `scripts/dev-uat-review-start.sh`: HTTP 또는 공통 startup
- `scripts/dev-uat-review-start-teams-https.sh`: HTTPS wrapper
- backend 5092, frontend 5190, strict port
- 현재 healthy persistent PostgreSQL과 canonical UAT DB만 읽음
- migration/seed/upsert/compose up을 실행하지 않음
- `.env`는 DB allowlist key만 literal parsing, `.env.notify-local`은 읽지 않음
- unrelated port/session이 있으면 종료하지 않고 실패

첫 실행에서 `.env`의 일반 Development DB 이름이 우선돼 canonical UAT guard가 중단했다. script가 `UAT_DATABASE_NAME` 또는 canonical 기본값만 사용하도록 수정했으며, 두 번째 실행은 정상 기동됐다. guard가 잘못된 DB로 진행하지 않은 fail-closed 증빙이기도 하다.

## 16. Development mode 회귀

- Review-safe false에서 기존 worker 3개와 notification handler 4개 등록 유지
- backend 전체 303개 test 성공
- frontend 기존 test와 Full-Stack E2E 성공
- 5174/5185/5081 PID 유지, 각 health 200
- Development UAT의 저장/worker/provider 정책을 변경하는 script 또는 configuration은 수정하지 않음

실제 Development write나 외부 발송은 본 Task에서 실행하지 않았다.

## 17. Persistent UAT 검증

전후 결과는 동일했다.

| 항목 | 전 | 후 |
|---|---:|---:|
| schema_migrations | 28 / latest 0027 | 28 / latest 0027 |
| projects | 22 | 22 |
| work_items | 37 | 37 |
| notifications | 89 | 89 |
| notification_recipients | 163 | 163 |
| notification_deliveries | 92 | 92 |
| work_item_escalations | 2 | 2 |
| qms_users | 14 | 14 |
| departments | 12 | 12 |
| system_holidays | 6 | 6 |

Delivery status도 Disabled 1, DryRunSent 6, Failed 20, Sent 59, Suppressed 6으로 동일했다. PostgreSQL container ID, persistent volume, restart count 0, Backend 5081 PID도 동일했다. Development worker의 자연 변화 가능성을 전제로 비교했으나 관찰 구간에는 변화가 없었다.

## 18. 테스트 결과

| 검증 | 결과 |
|---|---|
| git diff/check, actionlint, bash -n, shellcheck | 성공 |
| backend Release build | 성공, warning 0 |
| backend 전체 | 303/303 |
| Review-safe targeted | 7/7 |
| DB read-only/schema/JIT integration | 성공 |
| authorization/migration | 전체 backend suite에 포함, 성공 |
| frontend lint/typecheck/unit/build | 성공, 59/59 |
| mock UI | Playwright 1/1 성공 |
| Full-Stack E2E | isolated DB 16/16, cleanup 성공 |
| HTTPS Review-safe startup | 5092/5190 성공 |
| API/health/mutation | GET/health 200, mutation 423 |
| browser route/console | route 11개, console error 0 |
| 390px | 3개 route overflow 0 |
| persistent UAT snapshot | 전후 동일 |

기존 warning은 frontend Fast Refresh 1건과 production chunk-size 1건이다. 신규 dependency/runtime warning은 없다. PR #26 head `89990da` 기준 GitHub Actions Run `29082148114`의 Backend, Frontend와 Full-Stack E2E가 모두 성공했고, 이후 사용자 직접 검수도 완료됐다.

## 19. 보안/secret

- migration/dependency/env/certificate/manifest 변경 없음
- `.env.notify-local` 미로드
- credential/provider URL을 log, endpoint, 문서에 기록하지 않음
- tracked diff secret/PII scan은 최종 stage 전 재실행
- 실제 외부 provider call과 신규 delivery 0

## 20. 제한사항

- frontend mutation control 분류는 UX 보조이며 새 action 문구 추가 시 guard test 갱신이 필요하다.
- persistent UAT를 Development worker와 공유하므로 장시간 전체 DB 불변은 보장하지 않는다. Review application session과 delivery/log source로 구분한다.
- Review-safe는 별도 DB role이 아니라 session read-only를 사용한다. application code가 별도 비관리 connection을 만들면 정책 검토가 필요하다.
- TASK-UAT-002 사용자 검수는 완료됐으며 UAT-VERIFY-001 통합 검수는 아직 시작하지 않았다.

## 21. Rollback

1. Review-safe 5190/5092 session만 ownership을 확인해 종료한다.
2. Development 5174/5081과 PostgreSQL은 유지한다.
3. 코드 rollback은 본 Task commit revert 또는 forward-fix로 수행한다.
4. migration/DB schema가 없으므로 DB rollback은 없다.
5. mode 오설정은 `ReviewSafe:Enabled=false`로 되돌리되 Production true startup 실패를 우회하지 않는다.

## 22. 후속 Task

1. UAT-VERIFY-001
2. TASK-NOTIFY-REL-001
3. TASK-NOTIFY-ESC-001
4. TASK-AUTH-HARDEN-001
5. TASK-GOV-002
6. 실패 delivery 재처리
7. 사용자별 알림 설정

## 23. 해결한 업무 문제

조회 검수자가 실수로 저장·삭제·발송하거나 background process가 검수 중 데이터를 바꾸는 위험을 줄였다. Development 환경의 실제 기능 검수 능력은 유지하면서 감사/기준선 검토용 안전한 주소를 분리했다.

## 24. 기술적 결정과 검토한 대안

- UI read-only만 적용: 직접 API 우회가 가능해 폐기
- middleware만 적용: startup/worker/JIT/DB 우회가 남아 폐기
- 전용 DB role 생성: persistent schema 변경과 승인 범위가 필요해 이번 Task에서 제외
- session read-only: migration 없이 pool connection까지 적용 가능해 채택
- provider no-op: delivery row 생성 가능성이 있어 actual provider/worker 미등록을 채택
- schema mismatch 자동 migration: Review 원칙과 충돌해 readiness 실패를 채택

## 25. 시행착오 및 폐기한 접근

- WebApplicationFactory의 늦은 test configuration만으로 top-level DI 조건을 검증하면 worker가 기존 값으로 등록됐다. test host setting을 entrypoint 이전에도 제공하도록 factory를 보강했다.
- Review readiness 응답을 별도 shape로 만들면 기존 frontend의 `database.reason` 접근과 충돌했다. 기존 `database` 계약을 유지하고 review status를 추가했다.
- startup script가 일반 `.env`의 Development DB 이름을 상속하자 canonical UAT guard가 실행을 차단했다. 명시적 UAT 이름만 선택하도록 수정했다.
- 첫 수동 curl mutation loop는 zsh glob으로 실행되지 않았다. URL quoting 후 동일 검증을 다시 수행해 실제 결과만 증빙했다.
- 최종 build 교체 중 screen session 종료 뒤 backend child listener가 남았다. Startup script는 예상대로 occupied port에서 fail-closed했고, PID/cwd/command가 Review-safe process임을 다시 확인한 뒤 해당 listener만 정상 종료해 재기동했다. Development 5081/5174/5185에는 영향이 없었다.
- 최종 route 확장 확인에서 date range가 생략된 공휴일 GET이 PostgreSQL의 untyped null parameter 때문에 500을 반환했다. Date parameter를 명시적으로 typing하고 기존 production-planning test에 no-range 회귀를 추가했으며 실제 5190 GET 200, sync POST 423을 확인했다.
- 첫 Draft PR CI에서 mock UI fixture가 `/api/runtime-mode`를 제공하지 않아 frontend가 안전하게 fail-closed했고 “신규 프로젝트”가 disabled됐다. Production 문제가 아니라 test harness 계약 누락으로 판정해 Development runtime 응답을 fixture에 추가하고 mock UI smoke와 CI를 다시 검증했다.

## 26. 사용자 검수 결과와 남은 항목

상태:

- Checklist 작성됨
- 자동 검증 완료
- 사용자 검수 완료
- PR #26 병합 승인

검수 사용자 A는 2026-07-10 Review-safe UAT 5190에서 banner, 주요 조회 화면, 검색·필터·정렬·상세 이동, mutation action disabled와 이유, console·narrow pane, Development 5174/5081과 Preview 5185 유지, SOP와 User manual을 직접 확인하고 PR #26 병합을 승인했다. API 423, DB read-only, startup·worker·provider 차단과 외부 provider 호출 0은 자동 증빙을 함께 사용했다. UAT-VERIFY-001은 이번 승인 범위에 포함하지 않고 다음 Task로 유지한다.

## 27. 5종 산출물

| 산출물 | 경로 | 상태 |
|---|---|---|
| Implementation report | 본 문서 | 작성 완료 |
| SOP | `tasks/uat-002-sop.md` | 작성 완료 |
| User manual | `tasks/uat-002-user-manual.md` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 작성 완료 |
| User validation checklist | `tasks/uat-002-review-safe.md` 12절 및 각 운영 문서 | Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 |
