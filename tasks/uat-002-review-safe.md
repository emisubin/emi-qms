# TASK-UAT-002 — Review-safe UAT

## 1. 목적

Development UAT와 별도로 persistent UAT 데이터를 안전하게 조회하는 Review-safe UAT를 제공한다. Review-safe UAT는 감사, 기준선 확인, 데이터 상태 검토와 사용자 조회 검수에 사용하며 Development UAT를 대체하지 않는다.

## 2. 배경과 해결하는 P2

기존 Development UAT는 migration, startup upsert, background worker, 외부 delivery와 mutation API가 활성화된 개발 환경이다. 이 환경을 읽기 검수에 그대로 사용하면 오조작이나 누락된 서버 권한 검사로 데이터 또는 외부 시스템이 변경될 수 있다. 이번 Task는 frontend 안내만이 아니라 startup, hosted service, provider, HTTP, DB session을 겹쳐 차단하는 별도 실행 모드로 이 P2를 해소한다.

## 3. Development UAT와 Review-safe UAT

| 구분 | Development UAT | Review-safe UAT |
|---|---|---|
| 목적 | 저장·수정·worker·외부 delivery 개발 검수 | 조회·검색·필터·상세 검수 |
| Frontend / Backend | HTTPS 5174 / HTTP 5081 | HTTPS 5190 / HTTP 5092 |
| Persistent UAT DB | 읽기·쓰기 | session-level read-only |
| migration / seed / upsert | 기존 정책에 따라 실행 | 실행 금지 |
| mutation worker | 실행 | 미등록 |
| 외부 provider | 기존 설정에 따라 실행 | 미등록·호출 금지 |
| API mutation | 허용 | 서버에서 fail-closed 차단 |

## 4. 포함 범위

- backend authoritative runtime mode와 상태 조회 API
- Production 오설정 차단과 기본값 `false`
- startup migration/seed/upsert 차단
- notification delivery, escalation, purge 등 mutation worker 미등록
- Teams Activity, Teams Channel, Mail actual provider 미등록
- `POST`, `PUT`, `PATCH`, `DELETE`와 method override 차단
- Entra GET 인증 경로의 JIT 사용자 생성 차단
- PostgreSQL session read-only와 review 전용 `application_name`
- schema mismatch를 포함한 Review-safe readiness
- frontend 전역 banner, mutation control 비활성화와 runtime mode 조회 실패 시 fail-closed UX
- HTTPS Review-safe 실행 script와 Development mode 회귀 검증
- persistent UAT 전후 snapshot과 provider 호출 0 검증

## 5. 제외 범위

- migration 추가 또는 기존 migration 변경
- dependency/lockfile 변경
- Development UAT의 worker/provider 정책 변경
- Review-safe 전용 DB role 또는 persistent DB schema 변경
- 실제 외부 알림 smoke, notification/work item 테스트 데이터 생성
- UAT DB drop/truncate/reset, Docker volume 변경
- 신규 업무 기능, delivery claim/lease, escalation starvation, 마지막 관리자 동시성 보강

## 6. Mutation inventory

### 6.1 Startup mutation

- `DatabaseMigrationRunner`: repository migration 적용 및 `schema_migrations` 기록
- `DevelopmentIdentitySeeder`: 개발 사용자/부서/역할/권한과 bootstrap 사용자 upsert
- startup SQL 경로에 포함된 master-data, role/permission, holiday 및 data repair 성격의 upsert
- Entra `IClaimsTransformation` → `DbIdentityStore.GetOrCreateEntraProfileAsync`: 첫 인증 GET에서도 JIT 사용자 생성 가능

Review-safe에서는 모두 실행하지 않는다. 특히 Entra GET은 기존 사용자 조회만 허용하고 미등록 사용자를 생성하지 않는다.

### 6.2 Background mutation

- `NotificationDeliveryWorker`: Pending delivery claim, provider 호출, delivery/notification 상태 갱신과 Daily Digest 처리
- `NotificationEscalationWorker`: escalation 생성·갱신과 notification 생성
- `AdminDeletionPurgeWorker`: 삭제 예정 데이터 purge
- dispatcher 내부 retry/digest 처리

Review-safe DI graph에는 위 mutation worker를 등록하지 않는다.

### 6.3 External provider

- Teams Channel webhook handler/client
- Teams Activity Graph handler/client
- SMTP/Mail handler/client
- 공휴일 official API provider

Review-safe에서는 actual handler/client를 등록하지 않는다. 공휴일 목록 GET에 필요한 interface에는 외부 호출을 하지 않는 명시적 Review-safe provider를 등록한다. `.env.notify-local`과 credential을 로드하지 않으며 Dry-run delivery 생성도 DB mutation이므로 허용하지 않는다.

### 6.4 HTTP mutation

현재 endpoint mapping inventory는 `GET 55`, `POST 46`, `PATCH 11`, `PUT 2`, `DELETE 5`다. 업무 생성·수정·삭제, 상태 전환, 읽음 처리, retry/acknowledge/dismiss, 관리자 수동 발송, 사용자 승인/비활성화, 부서·휴일 관리, import/upload가 mutation 대상이다. Review-safe middleware는 경로나 content type과 무관하게 unsafe method를 기본 차단한다.

### 6.5 GET side effect

업무 GET endpoint 자체의 명시적 write는 확인되지 않았다. 다만 Entra claims transformation의 JIT upsert가 모든 첫 bearer-auth 요청 앞에서 실행될 수 있으므로 Review-safe 전용 read-only identity lookup으로 분기한다. 이 경로가 해소되지 않으면 Task를 완료하지 않는다.

## 7. 다층 방어 설계

1. **Startup:** migration, seed, upsert를 실행하지 않는다.
2. **DI/Worker:** mutation hosted service와 actual provider를 등록하지 않는다.
3. **HTTP:** unsafe method를 인증·endpoint 실행 전에 `423 Locked`로 차단한다.
4. **Identity:** GET 인증도 사용자 JIT write를 수행하지 않는다.
5. **Database:** 모든 review connection에 `default_transaction_read_only=on`을 적용한다.
6. **Readiness:** DB 연결, read-only, expected/actual schema, worker/provider 비활성 상태를 함께 확인한다.
7. **Frontend:** backend mode를 권위 있는 상태로 조회하고 mutation control을 비활성화한다. 상태 조회 실패도 fail-closed다.

## 8. 예상 수정 파일

- backend `ReviewSafe/` runtime option, middleware, status/readiness 구성
- `Program.cs`, DB connection/migration/identity 관련 최소 파일
- 공휴일 GET nullable date parameter typing과 Review-safe no-call provider
- Review-safe backend targeted tests
- `frontend/src/App.tsx`, `frontend/src/api.ts`, `frontend/src/styles.css`와 targeted tests
- `scripts/dev-uat-review-start.sh`
- `scripts/dev-uat-review-start-teams-https.sh`
- 본 Task의 implementation report, SOP, user manual, Roadmap

Migration, dependency/lockfile, Teams manifest/icon은 변경하지 않는다.

## 9. 테스트 계획과 결과

계획:

- shell syntax/static analysis와 Git diff 검사
- backend build/전체·migration·authorization·Review-safe targeted test
- runtime mode, mutation middleware, worker/provider registration, DB read-only, readiness mismatch test
- frontend lint/typecheck/unit/build와 Review-safe UX test
- isolated Full-Stack E2E와 Development mode regression
- HTTPS 5190/5092 startup, route/browser/390px smoke
- persistent UAT before/after snapshot과 review process write/provider call 0 확인
- secret/PII scan

결과:

- backend Release build: 성공, warning 0
- backend 전체 test: 303/303 성공
- Review-safe targeted/API/registration test: 7/7 성공
- DB read-only/schema mismatch/Entra read-only integration: 성공
- frontend lint/typecheck/unit/build: 성공, unit 59/59
- mock UI smoke: 1/1 성공
- 기존 warning: Fast Refresh 1건, production chunk-size 1건. 신규 warning 없음
- Full-Stack E2E: isolated PostgreSQL에서 16/16 성공, 전용 자원 cleanup 성공
- Review-safe HTTPS startup: 5092/5190 성공, runtime/readiness 일치
- 실제 API: GET 성공, POST/PUT/PATCH/DELETE와 method override 모두 423
- browser smoke: 주요 route 11개, 390px route 3개, console error 0, page-level overflow 0
- persistent UAT: 5분 관찰 전후 schema·핵심 9개 table·delivery status 동일, container ID/restart count 동일
- provider 실제 호출: registration/log/delivery snapshot 기준 0

사용자 검수는 검수 사용자 A가 2026-07-10 Review-safe UAT 5190에서 직접 수행했고, 조회 동작·mutation action 차단·SOP·User manual과 PR #26 병합을 승인했다. 자동 검증과 사용자 직접 검수 증빙은 별도 상태로 유지한다.

## 10. 남은 위험과 후속 Task

- UAT-VERIFY-001 통합 검증
- TASK-NOTIFY-REL-001 delivery claim/lease
- TASK-NOTIFY-ESC-001 escalation starvation
- TASK-AUTH-HARDEN-001 마지막 System Administrator 동시성
- TASK-GOV-002 Git history 개인정보 risk decision
- 실패 delivery 재처리, 사용자별 알림 설정

신규 기능 개발은 남은 P2가 해소될 때까지 No-Go다.

## 11. 5종 산출물 상태

| 산출물 | 경로 | 상태 |
|---|---|---|
| Implementation report | `tasks/uat-002-implementation-report.md` | 작성 완료 |
| SOP | `tasks/uat-002-sop.md` | 작성 완료 |
| User manual | `tasks/uat-002-user-manual.md` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 반영 완료 |
| User validation checklist | 본 문서 12절 | Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 |

## 12. 사용자 검수 체크리스트

- [x] `https://localhost:5190` 접속 가능
- [x] Review-safe banner가 모든 주요 화면에 표시됨
- [x] 프로젝트·업무·알림·관리자 데이터를 조회할 수 있음
- [x] 검색·필터·정렬·상세 이동 가능
- [x] 저장 버튼 disabled 및 이유 표시
- [x] 수정 버튼 disabled 및 이유 표시
- [x] 삭제·복구 버튼 disabled 및 이유 표시
- [x] 업무 시작·완료·취소 disabled
- [x] 읽음 처리 disabled
- [x] 수동 알림 발송 disabled
- [x] retry·확인·제외 처리 disabled
- [x] API 직접 `POST`/`PUT`/`PATCH`/`DELETE`가 서버에서 차단됨
- [x] Review backend DB session이 read-only임
- [x] migration/seed/master upsert가 실행되지 않음
- [x] notification/escalation/purge worker가 실행되지 않음
- [x] Teams/Mail/Channel 실제 발송 없음
- [x] Development UAT 5174는 기존대로 동작함
- [x] 5185 Preview가 유지됨
- [x] Console 오류 없음
- [x] 390px/Teams narrow pane overflow 없음
- [x] SOP를 따라 Review-safe 서버를 직접 실행할 수 있음
- [x] User manual이 비개발자도 이해 가능함

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #26 병합 승인**

검수 증빙: 검수 사용자 A / 2026-07-10 / HTTPS Review-safe UAT 5190 및 PR #26 문서 / 직접 화면·문서 검수와 병합 승인. API 423, DB read-only, startup·worker·provider 차단, Development/Preview/Persistent UAT 보존과 외부 provider 호출 0은 자동 증빙을 함께 사용했다.
