# TASK-SITE-ACCESS-001 — Implementation report

> 상태: PR #116 main 병합·Azure 공개배포·자동 공개 확인 완료 / 사용자 화면·Excel 검수 대기 / Persistent UAT 미적용
> 작업 기준선: `220d1201c9dbb881fb3e5c5061871fb943c7961b`
> 작업공간: 대표 clone과 분리된 recovery worktree, detached HEAD
> 검증된 제품 통합 commit: `6ca27d5f2552eb367279f2899b872f82cd03fccb`
> 독립 1차 검증 artifact commit: `b514e236728741e39905c6424d3a3acaf48061cd`
> 문서 P2 1차 보정 commit: `aea583611cc6df79d523d752eed78c2f6f98db05`
> 문서 P2 독립 재확인 PASS commit: `0274756ab300827d62ee385a83d66773d346b6ca`

최초 local 검증 후보를 현재 공개본과 같은 최신 원격 `main`에 통합했다. 공개본의 G2 migration `0084_g2_delivery_target_defect.sql`과 Roadmap 추적 `97`을 보존하고, 사이트 접속은 Change 002에 따라 migration `0085`와 추적 `98`로 교정했다. 통합 제품 commit에서 Backend·Frontend 전체 회귀와 사이트 접속·G2 Full-Stack을 다시 통과했으며, 이 report·최신 screenshot만 뒤따르는 종료 artifact다.

## 해결한 업무 문제

기존 감사 원장은 새 Microsoft 대화형 로그인과 저장·권한 사건은 보여주지만, 인증이 유지된 상태로 PMS를 다시 연 사용자의 실제 사이트 접속은 보여주지 못했다. 이번 구현은 페이지 최초 진입·새로고침·다른 화면 진입을 제한된 신호로 받아 같은 사용자·같은 브라우저 client·30분 활동 창을 접속 한 행으로 기록하고, 관리자가 기존 `전체 감사 이력`과 선택 Excel에서 확인할 수 있게 한다.

마지막 활동은 페이지 신호 시각일 뿐 실제 체류·근무시간이 아니다. 이 해석 경계를 관리자 화면과 Excel에 함께 표시한다.

## 승인·Source of Truth

- Task Identity Gate: [site-access-001-identity-gate.md](site-access-001-identity-gate.md), `PASS_CREATE`·명시적 Roadmap 순서 변경 승인
- Fable interview: [site-access-001-interview.md](site-access-001-interview.md), `COMPLETED_CONFIRMED`
- Fable planning: [site-access-001-planning.md](site-access-001-planning.md)
- Codex review: [site-access-001-review.md](site-access-001-review.md), open blocking decision `0`
- 승인된 구현 계약: [site-access-001-change-001.md](site-access-001-change-001.md), 명시적 로그아웃 종료 권장안 A
- 최신 main 통합·게시 계약: [site-access-001-change-002.md](site-access-001-change-002.md), migration `0085`·G2 보존·원격 main 병합 승인

Fable planning은 원문 보존 규칙 때문에 승인 전 metadata와 checkbox를 수정하지 않았다. 실제 planning·implementation 승인은 Codex review와 Change 001이 canonical 기록이다.

## 포함·제외 범위

### 포함

- additive migration `0085_site_access_sessions.sql`
- actor+browser client+30분 활동 창의 접속 세션과 1회성 명시적 로그아웃 종료
- 19개 고정 메뉴 코드, 최초 방문 순서의 중복 없는 누적
- 서버·DB 권위 시각, actor snapshot, IP·browser/OS family와 앱 접근 결과
- 인증 사용자 signal/end API와 관리자 통합 목록·summary·상세
- 별도 사이트 접속 coverage와 시간 해석 안내
- 전체 감사 이력 desktop/mobile 표시와 기존 선택 Excel 확장
- 단위·PostgreSQL·전체 회귀·격리 Full-Stack·1440px/390px 검증
- Task 종료 문서와 Roadmap 갱신

### 제외

- 과거 접속 소급 생성
- timer heartbeat, 클릭·키 입력·모든 HTTP 요청
- URL·query·검색어·프로젝트·업무 식별자 저장
- Entra sign-in log 연동과 신규 권한·알림 채널
- Azure 공개배포
- Persistent UAT·운영 DB migration·실제 provider mutation

## 구현 결과

### 데이터 원장

- 기존 `audit_events` 제약과 Login/Logout 의미를 바꾸지 않고 `site_access_sessions`를 별도 추가했다.
- `site_access_coverage_state`가 기능 적용 이후의 별도 기록 시작 시각을 보존한다.
- DB 함수가 transaction advisory lock을 획득한 뒤 `clock_timestamp()`로 현재 시각을 잡는다. 호출자는 관측 시각을 전달할 수 없다.
- 마지막 활동이 현재 시각보다 엄격히 30분 미만일 때만 기존 행을 사용하므로 정확히 30분은 새 행이다.
- trigger는 identity·환경 snapshot 변경, 시간 역행, 메뉴 제거·재정렬, 종료 후 변경과 모든 delete를 차단한다.
- Runtime 역할은 테이블 DML이 없고 security-definer 함수 실행으로만 기록한다.

### Backend

- `POST /api/audit/site-access/signals`은 `AuthenticatedIdentity` principal에서 actor·앱 접근 결과·접속 환경을 확정하고 고정 메뉴 코드만 DB 함수에 넘긴다.
- `POST /api/audit/site-access/end`은 actor·session·일회성 receipt를 검증해 명시적 로그아웃을 idempotent하게 종료한다.
- signal/end route는 업무 mutation 감사 registry의 명시적 제외 항목으로 등록해 자기 자신을 다시 감사하지 않는다.
- 관리자 목록·상세·summary·선택 ID 조회는 Global·Authorization·SiteAccess 세 원본을 통합한다.
- 접속 상태는 조회 시 `최근 활동`, `30분 경과`, `직접 로그아웃`으로 계산한다.
- 선택 Excel에 최초·마지막 활동, 종료·상태·접속 메뉴·앱 접근 결과·접속 환경·별도 coverage·해석 안내를 추가했다.

### Frontend

- 같은 origin의 탭이 공유하는 localStorage UUID를 browser client로 사용한다. Web Locks 지원 환경은 생성 구간을 직렬화하고, 미지원 환경은 IndexedDB readwrite transaction으로 최초 생성을 직렬화한다. 두 공유 저장 방식이 모두 거부되면 현재 문서 실행 동안 안정적인 in-memory ID로 기록을 계속한다.
- 모든 `View`를 exhaustive switch로 19개 고정 메뉴 코드에 매핑한다. 주소·query·업무 식별자는 전송하지 않는다.
- 사용자 준비 뒤 초기 화면·새로고침·새 `View` 진입마다 signal을 queue로 보낸다.
- signal과 explicit end는 각각 1.5초 deadline 안에서 best-effort로 끝나며, 멈춘 기록 요청도 실제 로그아웃을 막지 않는다.
- 전체 감사 이력에 사이트 접속 KPI·필터·별도 coverage·시간 안내·최초/마지막 활동·접속 메뉴 요약·상세 환경을 추가했다.
- 기존 mobile card와 fixed detail panel을 재사용해 390px page-level horizontal overflow `0`을 보존했다.

## 변경 파일

### 제품 코드·migration

- `database/migrations/0085_site_access_sessions.sql`
- `backend/src/Emi.Qms.Api/Audit/AuditContracts.cs`
- `backend/src/Emi.Qms.Api/Audit/AuditEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/Audit/AuditMutationRegistry.cs`
- `backend/src/Emi.Qms.Api/Audit/AuditStore.cs`
- `backend/src/Emi.Qms.Api/DataExports/SelectedExcelExportService.cs`
- `backend/src/Emi.Qms.Api/DatabaseRuntimePrivilegeManager.cs`
- `frontend/src/siteAccess.ts`
- `frontend/src/api.ts`
- `frontend/src/App.tsx`
- `frontend/src/audit.ts`
- `frontend/src/AuditPage.tsx`

### 검증

- `backend/tests/Emi.Qms.Api.Tests/AuditInfrastructureTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
- `frontend/tests/siteAccess.test.ts`
- `frontend/tests/App.navigation.test.tsx`
- `frontend/tests/AuditPage.test.tsx`
- `frontend/e2e/full-stack/site-access-audit.full-stack.spec.ts`
- [Desktop 1440px](site-access-001-screenshots/01-site-access-audit-desktop-1440.png)
- [Mobile 목록 390px](site-access-001-screenshots/02-site-access-audit-mobile-390.png)
- [Mobile 상세 390px](site-access-001-screenshots/03-site-access-detail-mobile-390.png)

### Task·운영 문서

- `tasks/site-access-001-identity-gate.md`
- `tasks/site-access-001-interview.md`와 Fable round 원문 5개
- `tasks/site-access-001-planning.md`
- `tasks/site-access-001-review.md`
- `tasks/site-access-001-change-001.md`
- `tasks/site-access-001-change-002.md`
- 본 Implementation report
- [SOP](site-access-001-sop.md)
- [User manual](site-access-001-user-manual.md)
- [사용자 검수 checklist](site-access-001-user-validation-checklist.md)
- `docs/00-product-roadmap.md`

## 검증 결과

| 검증 | 상태 | 결과·경계 |
| --- | --- | --- |
| Backend Release build | PASS | warning/error `0/0` |
| Backend audit/export/privilege 집중 검증 | PASS | `41/41` |
| Backend audit infrastructure·site PostgreSQL·global store | PASS | `14/14`, 격리 PostgreSQL |
| Backend 전체 회귀 | PASS | 최종 통합 tree `570/570`, skip `0`, 19분 54초 |
| Frontend 전체 회귀 | PASS | 최종 통합 tree `248/248` |
| Frontend typecheck | PASS | type error `0` |
| Frontend lint | PASS | error `0`, 기존 `main.tsx` Fast Refresh warning `1` |
| Frontend production build | PASS | build 완료, 기존 large bundle warning |
| 사이트 접속 격리 Full-Stack | PASS | `1/1`, API·관리자 UI·선택 Excel, Web Locks 미지원과 localStorage 쓰기 차단 각각에서 두 동시 탭의 동일 client ID 수렴·isolated PostgreSQL 생성·삭제 확인 |
| 공개 G2 격리 Full-Stack | PASS | `1/1`, G2 권한·동시 입력·불량 차감 재고·반응형 UI와 격리 PostgreSQL 생성·삭제 확인 |
| API 권한·선택 Excel | PASS | 익명 signal `401`, 일반 사용자 감사 조회 `403`, 관리자 조회·상세·Excel 성공, formula `0` |
| 화면 | PASS | 1440px desktop, 390px 목록·상세, page overflow `0` |
| diff whitespace | PASS | `git diff --check` |
| 독립 검증 | PASS | 제품·통합 PASS. `0274756ab300827d62ee385a83d66773d346b6ca` 문서 재확인에서 `SITE-ACCESS-FINAL-F01` RESOLVED, Open P0/P1/P2 `0/0/0`, local GO |

실제 사용자·운영 DB·외부 provider는 사용하지 않았다. Full-Stack은 synthetic 계정과 격리된 일회용 PostgreSQL만 사용했고 종료 시 database·container·network가 제거됐다.

외부 규격 대조도 완료했다. [HTML Standard Web Storage](https://html.spec.whatwg.org/multipage/webstorage.html)는 localStorage가 origin의 공유 저장소이면서 정책에 따라 `SecurityError`가 날 수 있음을 명시하고, [W3C Web Locks API](https://www.w3.org/TR/web-locks/)는 같은 storage bucket의 window·worker 간 exclusive lock을 정의한다. [W3C Indexed Database API 3.0](https://www.w3.org/TR/IndexedDB-3/)은 scope가 겹치는 readwrite transaction을 동시에 시작하지 않고 나중 transaction이 앞선 변경을 보도록 정의한다. [PostgreSQL Date/Time Functions](https://www.postgresql.org/docs/current/functions-datetime.html)는 `clock_timestamp()`가 호출 순간의 실제 시각임을, [PostgreSQL Advisory Locks](https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS)는 transaction-level lock이 transaction 종료 시 자동 해제됨을 확인하는 근거로 사용했다.

### 전체 검증 재현 명령

- `dotnet build backend/Emi.Qms.sln --configuration Release`
- `bash scripts/e2e-backend-tests.sh`
- `corepack pnpm --dir frontend test`
- `corepack pnpm --dir frontend typecheck`
- `corepack pnpm --dir frontend lint`
- `corepack pnpm --dir frontend build`
- `bash scripts/e2e-full-stack.sh site-access-audit.full-stack.spec.ts`
- `bash scripts/e2e-full-stack.sh g2-operations.full-stack.spec.ts`

## 검증 중 발견하고 보정한 Finding

| Finding | 등급 | 상태 | 보정 |
| --- | --- | --- | --- |
| SITE-ACCESS-IV-F01 멈춘 signal/end가 로그아웃을 무기한 대기시킬 수 있음 | P1 | RESOLVED | signal chain과 end 전체에 1.5초 단일 deadline, never-resolving 회귀 추가 |
| SITE-ACCESS-IV-F02 API·권한·Excel·desktop/mobile을 한 흐름으로 증명하는 acceptance 부재 | P1 | RESOLVED | 격리 Full-Stack 시나리오와 3개 screenshot 추가 |
| SITE-ACCESS-IV-F03 Web Locks 미지원 첫 탭 동시 생성이 다른 client ID를 반환할 수 있음 | P2 | RESOLVED | 고정 대기 방식 대신 IndexedDB transaction으로 최초 생성을 직렬화하고 실제 두 탭 동시 회귀 추가 |
| SITE-ACCESS-IV-F04 호출자 관측 시각이 30분 경계·감사 신뢰성에 영향을 줄 수 있음 | P2 | RESOLVED | timestamp 요청·함수 인자 제거, DB 권위 시각과 29/30/31분 검증 |
| SITE-ACCESS-T01 advisory lock 대기 전에 잡은 DB 시각이 동시 요청에서 역행할 수 있음 | P1 | RESOLVED | `clock_timestamp()`를 lock 획득 뒤로 이동하고 동시 신호 20개 검증 |
| SITE-ACCESS-T02 접속 coverage 이전 synthetic boundary fixture가 통합 목록에 포함된다는 잘못된 test 기대 | P2 | RESOLVED | 과거 접속 무소급 계약에 맞게 기대값을 정정하고 boundary 함수 검증은 유지 |
| SITE-ACCESS-T03 Test request init의 TypeScript nullable 오류 | P2 | RESOLVED | 명시적 존재 assertion 뒤 request body 검사 |
| SITE-ACCESS-FC01 브라우저 정책이 localStorage getter·write 또는 Web Locks storage bucket을 거부하면 signal이 생기지 않거나 client ID가 호출마다 달라질 수 있음 | P2 | RESOLVED | Web Locks 실패 시 IndexedDB transaction, 모든 공유 저장소 실패 시 같은 문서의 stable in-memory ID, 차단 storage 회귀 추가 |
| SITE-ACCESS-DOC01 문서만 읽으면 후보 기준·공통 배포 SOP·derived timeout·in-memory fallback 수명을 추측해야 함 | P2 | RESOLVED | report·Change·Azure SOP·증빙 기준 link와 DB 수신 순서·종료 불변·현재 문서 실행 범위 fallback을 명시 |
| SITE-ACCESS-IV2-F01 접속 메뉴가 상세에만 있고 desktop 목록·mobile card 요약에 없음 | P2 | RESOLVED | 목록 열과 mobile card에 최초 3개 메뉴와 초과 개수 요약 추가, 상세의 전체 순서 유지 |
| SITE-ACCESS-IV2-F02 Web Locks 미지원 fallback이 고정 75ms 안의 수렴에 의존함 | P2 | RESOLVED | IndexedDB readwrite transaction get-or-create로 교체하고 Web Locks를 끈 실제 두 탭 동시 Full-Stack 검증 추가 |
| SITE-ACCESS-FC02 Web Locks는 가능하지만 localStorage 쓰기만 거부되면 IndexedDB 이전에 탭별 임시 ID로 종료함 | P2 | RESOLVED | lock callback이 쓰기 실패를 반환하면 IndexedDB로 계속하고, localStorage 접근을 차단한 실제 두 탭 회귀 추가 |
| SITE-ACCESS-INTEGRATION-F01 최신 공개 G2가 migration `0084`를 이미 사용해 최초 사이트 접속 후보와 충돌 | P1 | RESOLVED | 공개 G2 `0084`를 보존하고 미게시 사이트 접속 migration을 `0085`로 교정, fresh·forward 포함 Backend `570/570` 재검증 |
| SITE-ACCESS-INTEGRATION-F02 공개 G2와 미게시 사이트 접속 Roadmap 항목이 추적 `97`을 동시에 사용 | P2 | RESOLVED | 공개 G2 추적 `97`을 보존하고 사이트 접속을 다음 빈 번호 `98`로 교정, 현행 Roadmap·report·Change 002 동기화 |
| SITE-ACCESS-INTEGRATION-F03 공개 G2 Roadmap 상태가 병합 승인에 머물러 실제 PR #115·Azure 공개 상태와 불일치 | P2 | RESOLVED | exact main `220d1201c9dbb881fb3e5c5061871fb943c7961b` 기준 실행 큐·추적·Decision Log를 실제 상태로 동기화 |
| SITE-ACCESS-FINAL-F01 Roadmap 3.3M·report·checklist의 게시 승인·독립 검증·artifact commit 상태가 불일치 | P2 | RESOLVED | 게시 승인·미실행, 독립 제품 PASS와 artifact 상태를 동기화하고 `aea5836`에서 남은 stale Gate 문장까지 교정. `0274756ab300827d62ee385a83d66773d346b6ca` read-only 재확인 PASS |

복구 전 임시 worktree가 환경 전환 중 사라졌고 저장된 commit은 없었다. private Fable transcript에서 원문 문서를 byte-for-byte로 복원하고 승인된 계약을 새 격리 worktree에서 재구현했다. 대표 clone의 사용자 WIP는 수정하지 않았으며, 복구본 전체를 다시 build·test·Full-Stack 검증 대상으로 삼았다.

## 개인정보·secret 검토

- 테스트와 화면 증빙은 synthetic 사용자명·고정 UUID만 사용한다.
- 실제 이름·회사 이메일·UPN·전화번호·token·cookie·Authorization header·provider secret을 새 문서와 화면에 기록하지 않는다.
- API는 browser client ID와 고정 메뉴 코드만 Frontend body에서 받는다. actor·시각·IP·browser/OS·앱 접근 결과는 서버가 확정한다.
- 관리자 화면·Excel에 접속 환경이 보이지만 Task 증빙에는 실제 운영 row를 복사하지 않는다.
- 변경 artifact secret pattern·이메일 pattern scan은 match `0`이다. Roadmap 추가 행도 실제 사용자 식별자·secret이 없음을 확인했다.

## 원격 main·Azure 공개배포

- 사이트 접속 기능은 PR #116으로 원격 `main` `a8bb000dbbbe7d307bf1de96259917b750460497`에 병합됐고 main CI `33493357909`을 통과했다.
- 후속 G2 PR #117까지 포함한 exact current-main `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`을 사용자가 전체 공개배포 승인했다.
- Azure release `33577473523`에서 additive migration `0085`, Backend, Frontend, public security가 모두 `PASS`다.
- 공개 읽기 전용 확인에서 health `200`, 익명 root·`/api/me` `401/401`, 전체 감사 이력의 사이트 접속 coverage와 양수 summary를 확인했다.
- 실제 사용자 원문·접속 row·IP·DOM은 증빙에 기록하지 않았다. 사용자 목록·상세·선택 Excel·직접 로그아웃 화면 검수는 계속 대기한다.
- Persistent UAT와 과거 접속 소급, 실제 외부 알림 시험 발송은 실행하지 않았다.

## Rollback·forward-fix

- 사이트 접속 변경은 PR #116과 Azure release `33577473523`으로 운영에 반영됐다. Application rollback 또는 forward-fix 시에도 적용된 감사 원장과 migration ledger를 보존한다.
- migration `0085`는 additive이고 감사 원장은 삭제 대상이 아니다. 운영 적용 후 schema나 행을 역삭제하지 않는다.
- Application 문제는 직전 호환 Backend/Frontend로 rollback하거나 forward-fix한다.
- DB 함수·guard·권한 문제는 다음 번호의 additive migration으로 수정한다.
- rollback·재배포 중에도 기존 Login/Logout·변경·권한 원장과 접속 원장 데이터를 보존한다.

## 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | main·Azure·자동 공개 확인 결과 동기화 | 본 문서 |
| SOP | 운영 적용 기준 동기화 | [site-access-001-sop.md](site-access-001-sop.md) |
| User manual | 작성 | [site-access-001-user-manual.md](site-access-001-user-manual.md) |
| Roadmap update | PR #116·Azure 결과 동기화 | `docs/00-product-roadmap.md` 3.3M·추적 98·Decision Log |
| User validation checklist | 자동 공개 확인 완료·사용자 화면 검수 대기 | [site-access-001-user-validation-checklist.md](site-access-001-user-validation-checklist.md) |

## 현재 Gate

- Open P0/P1/P2: `0/0/0`
- 사용자 화면 검수: 대기
- Commit/Push/PR/Merge: PR #116·exact main `a8bb000dbbbe7d307bf1de96259917b750460497` 완료
- Azure 공개배포: exact current-main `58daf6d8bfe333cb00e343a3fcc13ee4f3358183`, run `33577473523` 완료
- Persistent UAT: 미실행·미승인
- 다음 Gate: 사용자 사이트 접속 목록·상세·선택 Excel·직접 로그아웃 화면 검수
