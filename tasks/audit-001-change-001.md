# TASK-AUDIT-001 Change 001 — 승인된 감사 원장 구현 계약

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- changeStatus: `APPROVED_FOR_IMPLEMENTATION`
- instructionChainRead: true
- userInstructionDate: 2026-08-28
- canonicalTaskId: `TASK-AUDIT-001`
- supersededBy: `tasks/audit-001-change-002.md` — 당시 `commitApproved: false`는 Change 002의 명시적 local commit 승인으로 대체됨
- approvalSource: `USER_EXPLICIT`
- userApprovalExact: `승인.`
- planningSource: `tasks/audit-001-planning.md`
- reviewSource: `tasks/audit-001-review.md`
- implementationApproved: true
- commitApproved: false
- pushApproved: false
- pullRequestApproved: false
- mergeApproved: false
- persistentUatApproved: false
- azureDeploymentApproved: false
- branch: `feat/task-audit-001-access-change-audit`
- baseSha: `7371d9e7224c3786f9b0efe3b2b88dfe9b88cd50`

## 1. 사용자 승인 범위

- Fable planning의 기능 목표·포함/제외 범위와 Codex review F01~F09 resolution을 구현한다.
- G2를 포함한 인증 사용자의 durable 업무·관리 mutation 전체를 신규 통합 원장에 기록한다.
- 기존 전용 원장과 쓰기 경로는 보존하고, 배포 이후 신규 통합 원장을 로그인·성공 변경·실패 시도의 global canonical source로 사용한다.
- 고정 정규화 scalar·enum·날짜·수량만 exact before/after로 저장한다. rich text·comment·HTML·대용량 자유문은 변경 여부와 bounded 길이 metadata만 저장한다.
- 관리자 전용 통합 조회·상세·선택 Excel, additive migration, isolated 자동 검증, 별도 독립 검증까지 포함한다.

## 2. 구현 allowlist

- `database/migrations/`: 신규 append-only 원장, field child, coverage metadata, index·trigger와 selected export kind의 additive migration.
- `backend/`: audit contracts·projection guard·actor/correlation·transaction recorder·실패 classifier·coverage registry·관리자 query/Excel endpoint와 포함 mutation 연결.
- `frontend/`: 대화형 MSAL 로그인·명시적 로그아웃 correlation, 관리자 전체 감사 이력 route/page/API와 선택 Excel.
- `scripts/`, Backend/Frontend/E2E tests: coverage 누락, 성공·no-op·rollback, privacy guard, append-only, 권한, migration, desktop·390px 검증에 필요한 범위.
- `tasks/`, `docs/00-product-roadmap.md`: 구현 보고·SOP·user manual·checklist·Roadmap/Decision Log 동기화.

## 3. 개인정보·보안 carve 및 descope ledger

감사 후보는 서버 소유 고정 allowlist로만 구성하고 request·response·exception·header·cookie·attachment·domain object를 직렬화하지 않는다. 최종 schema allowlist와 forbidden-field/value guard를 통과하지 못하면 audit append를 거절하며 raw fallback은 없다.

| 절대 제외 | 안전한 대체 기록 |
| --- | --- |
| 비밀번호 | fixed server-owned reason code |
| access·ID·refresh token | fixed server-owned reason code |
| Authorization header | fixed server-owned reason code |
| cookie·session·CSRF 값 | fixed server-owned reason code |
| request body | 명시적으로 허용된 정규화 business scalar before/after 또는 metadata-only |
| failed validation 입력값 | fixed validation reason code |
| attachment binary/body·multipart body/header | bounded filename·byte size·fixed action metadata |
| rich text·comment·HTML·대용량 자유문 원문·excerpt·hash | changed 여부와 bounded before/after length |

- IP는 로그인 사건에서만 기존 trusted-proxy boundary가 도출한 값만 허용한다. client-supplied forwarding header를 직접 복사하지 않는다.
- actor snapshot은 표시명·부서만 허용하고 다른 profile field는 포함하지 않는다.
- guard는 password/token/header/cookie/body/validation/attachment/free-text marker의 저장 부재를 전 컬럼·직렬화 표현에서 검증한다.

## 4. 제품·transaction 불변조건

- 성공 mutation audit는 동일 transaction의 fail-closed다. audit projection·append 실패 시 업무 mutation 전체가 rollback된다.
- 로그인·로그아웃·이미 거절된 저장 시도 audit는 원래 operation 결과를 바꾸지 않는 best-effort이며, 실패는 구조화된 운영 오류로 남긴다.
- DB 확정값이 바뀌지 않은 accepted no-op에는 성공 변경 사건을 만들지 않는다.
- bulk/import는 parent request 1건과 실제 변경 target·field child를 같은 transaction에 기록하며 전체 rollback이면 audit도 0건이다.
- worker·scheduler·자동 만료·provider 내부 처리, 조회/preview, 알림 읽음, web push 기기 등록, 404·5xx는 신규 업무 변경/실패 원장에서 제외한다.
- 권한 거부는 기존 `authorization_audit_events`, Excel export 성공은 기존 `data_export_events`가 canonical이며 신규 성공 변경으로 중복 기록하지 않는다.
- 신규 원장은 update/delete 차단 trigger와 runtime 최소 권한으로 append-only를 강제하고 purge 경로를 만들지 않는다.
- migration 적용 시각을 `coverage_started_at_utc`로 기록하고 과거 이력을 추정·소급 생성하지 않는다.

## 5. 로그인·correlation 계약

- MSAL `LOGIN_SUCCESS` 중 Redirect/Popup 대화형 사건만 로그인으로 기록한다. silent token과 restored account는 로그인 사건을 만들지 않는다.
- 한 로그인 사건에 `authenticationOutcome=Succeeded`와 `appAccessOutcome=Allowed|ApprovalPending|Inactive`를 함께 저장해 이중 집계하지 않는다.
- 서버가 actor 소유를 검증한 opaque correlation·idempotency receipt를 발급한다. correlation은 MSAL cache와 맞는 account-scoped 수명으로 유지하고 로그아웃 때 인증 header가 있는 `fetch(..., { keepalive: true })`로 종료 사건을 보낸 뒤 제거한다.
- correlation 부재는 business mutation 실패 사유가 아니며 null 연결로 기록한다.

## 6. Coverage·검증 Gate

- `domain → endpoint → store method → 성공 action/field projection → 실패 분류 → worker 여부 → test` registry를 source of truth로 만든다.
- 실제 mutation endpoint와 registry 간 누락·미등록 exclusion이 있으면 contract test를 실패시킨다.
- 모든 포함 경로의 성공·no-op·rollback, 실패 분류, privacy adversarial fixture, append-only, 관리자 권한, migration fresh/upgrade, Excel, desktop·390px와 isolated Full-Stack을 통과하기 전 release 후보로 만들지 않는다.
- 구현 세션과 분리된 read-only Codex 검증이 승인 계약·diff·테스트·Finding gate를 재검토한다.

## 7. 게시·운영 경계

- 이 승인으로 허용되는 범위는 전용 branch의 구현·isolated 검증과 승인 범위의 local commit까지다.
- Push·PR·main merge·Persistent UAT mutation·Azure release는 현재 승인에 포함하지 않는다.
- 구현·자동 검증·독립 검증·사용자 검수 결과를 보고한 뒤 게시 및 운영 배포 실행 승인을 별도로 받는다.
- application rollback 시 신규 audit table과 원장은 보존하고 forward-fix한다.

## 8. 현재 기준선

- 최신 `origin/main`과 Task branch HEAD는 모두 `7371d9e7224c3786f9b0efe3b2b88dfe9b88cd50`으로 일치한다.
- 최신 migration은 `0082_g2_forecast_expiry.sql`이며 신규 migration 후보는 `0083`이다.
- Task 전용 worktree만 변경하며 사용자의 canonical dirty workspace는 수정·정리·재시작하지 않는다.

## 9. 다음 Gate

coverage registry·schema·공용 recorder부터 구현한 뒤 도메인별 mutation 연결, 관리자 화면·Excel, 전체 자동 검증과 독립 검증으로 진행한다. 결과를 본 뒤 사용자 검수와 게시·Azure release 승인을 별도로 요청한다.
