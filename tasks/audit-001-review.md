# TASK-AUDIT-001 — Codex 기획 내용 Review

- reviewTarget: `tasks/audit-001-planning.md`
- interviewSource: `tasks/audit-001-interview.md`
- reviewOwner: `CODEX`
- reviewStatus: `COMPLETED`
- recommendation: `APPROVED_WITH_RESOLUTION`
- planningApproved: true
- implementationApproved: true
- userDecisionRequiredCount: 0

## 1. 결론

사용자 문제와 제품 방향은 타당하다. 로그인 사실과 실제 저장 변경을 같은 행위자·시각·대상 기준으로 연결하고, 관리자가 화면과 Excel로 확인하게 하는 기능은 Roadmap 추적 항목 48을 직접 해결한다. `Audit.Read.All`, 선택 Excel, KST 기간 조회, 기존 감사 화면 관례와 신뢰 프록시 설정도 재사용할 수 있다.

다만 Fable 기획안을 그대로 구현하면 전체성·불변성·로그인 정확성에 빈틈이 생긴다. Repository에는 기획안이 요약한 4종보다 훨씬 많은 전용 이력 원장이 있고, 일부 기존 이력은 도메인 삭제 lifecycle에 포함된다. 또 성공 audit 실패 처리, 대화형 로그인 중복 방지, correlation lifecycle과 값 projection이 구현 계약으로 충분히 고정되지 않았다. 아래 resolution을 반영하는 조건으로 승인하는 것이 권장안이다. Fable 원문은 수정하지 않고 이 review가 구현 resolution을 별도로 고정한다.

### 계약 우선순위와 승인 gate

- 사용자 확인을 마친 `tasks/audit-001-interview.md`가 제품 정책의 최상위 source다.
- 사용자가 이 review의 resolution을 승인하면 Repository 대조에서 새로 발견한 구현 경계는 review가 Fable planning을 보완한다. Review가 interview의 확정 결정을 바꾸지는 않는다.
- 사용자는 한 답변으로 아래 3개 정책 결정, planning 승인과 구현 착수 승인을 함께 할 수 있다. 승인 전에는 코드·DB·runtime·Git mutation을 시작하지 않는다.
- Roadmap 재정렬은 Identity Gate에 이미 명시 승인으로 기록됐으며 이번에 다시 승인받을 항목이 아니다. Roadmap 문서 반영은 구현 산출물이다.
- PR·main merge와 exact main Azure release는 구현·독립 검증 결과를 본 뒤 해당 게시 실행에 대한 별도 gate로 유지한다.

## 2. 제품 방향 검토

| 분류 | 항목 | 판단과 근거 |
| --- | --- | --- |
| 유지 | System Administrator 전용 통합 조회·상세·선택 Excel | 사용자 문제를 직접 해결하고 기존 `Audit.Read.All` 권한과 선택 export 기반을 재사용할 수 있다. |
| 유지 | 성공 mutation과 audit의 동일 transaction | 실제 저장과 원장이 어긋나지 않게 하는 핵심 불변조건이다. 성공 audit가 실패하면 업무 transaction도 rollback하는 fail-closed가 필요하다. |
| 유지 | 실패 시도는 입력값 없이 fixed metadata만 기록 | validation 전 원문과 비밀값이 무기한 원장에 들어가는 것을 막는다. |
| 유지 | 앱이 확정 가능한 로그인 경계만 기록 | Entra 비밀번호·MFA 실패를 앱이 추측하지 않으므로 근거 수준이 명확하다. |
| 추가 | 대상 mutation coverage matrix | Backend에는 transaction을 직접 관리하는 store가 35개이고 mutation endpoint가 여러 도메인에 분산돼 있다. 모든 대상·제외·전용 원장 병행 여부를 고정 목록으로 만들어야 누락을 검증할 수 있다. |
| 추가 | 신규 통합 원장을 배포 이후 전체 감사의 canonical source로 사용 | 기존 도메인 원장은 유지하되, 대상 업무 mutation은 신규 원장에도 같은 transaction으로 기록한다. 그래야 도메인별 보존·삭제 차이와 schema 차이에도 전체 원장이 유지된다. |
| 추가 | DB 수준 append-only 강제 | 신규 사건·field 테이블에 update/delete 차단 trigger와 runtime role의 최소 권한을 적용하고 migration test로 고정한다. |
| 추가 | 로그인·correlation lifecycle | MSAL의 대화형 `LOGIN_SUCCESS`만 시작 사건으로 사용하고 서버 idempotency, actor 소유 검증, 저장 위치·회전·로그아웃 정리를 고정한다. |
| 추가 | 안전한 field projection catalog | 고정 scalar·enum·날짜·수량은 정규화된 DB 확정값을 기록한다. rich text·comment·HTML·대용량 자유문은 원문 대신 변경 여부·길이 등 안전 metadata만 기록하고, token·header·cookie·binary는 항상 제외한다. |
| 보류 | Entra sign-in log/Graph 연동 | 앱 밖 실패를 수집하는 별도 외부 연동이므로 v1에서 제외한다. |
| 보류 | 보존량 archive·purge | 사용자가 v1 무기한 append-only를 확정했다. 저장량 관찰 뒤 별도 Task로 다룬다. |
| 제거 | HTTP middleware만으로 before/after를 만드는 안 | 확정 DB 값과 transaction 일치를 보장할 수 없어 채택할 수 없다. |
| 제거 | logout `sendBeacon` 전제 | 현재 bearer 인증 API는 Authorization header가 필요하다. 요청 속성을 바꿔야 하는 경우에는 인증 header를 넣은 `fetch(..., { keepalive: true })`가 맞다. |

## 3. Finding과 resolution

### AUDIT-PLAN-F01 — 기존 감사 자산 범위 과소 산정

- severity: `P1`
- status: `APPROVED_RESOLUTION`
- finding: 기획안은 기존 원장을 4종으로 요약하지만 실제 migration과 store에는 `project_audit_events`, `project_workflow_events`, `pending_history`, `material_receipt_events`, `panel_manufacturing_events`, `form_template_audit_events`, `sales_monthly_target_audit_events`, `user_profile_photo_audit_events`, `panel_qr_events` 등 다수의 전용 이력 자산이 있다.
- impact: 기존 원장과 신규 원장의 분담이 불명확하면 같은 변경이 중복 표시되거나, 반대로 전용 원장에만 남은 변경이 통합 화면에서 빠질 수 있다. 일부 `project_audit_events`는 프로젝트 purge 경로에서 삭제되므로 기존 원장만 재사용하면 신규 원장의 무기한 보존 계약을 충족하지 못한다.
- resolution: 배포 이후 신규 통합 원장을 로그인·성공 변경·실패 시도의 canonical source로 삼는다. 권한 거부는 interview 결정대로 기존 `authorization_audit_events`가 canonical source다. 기존 전용 원장·화면·쓰기 경로는 호환과 도메인 기능을 위해 그대로 유지하고, 대상 사용자 mutation은 신규 원장에도 같은 transaction으로 병행 기록한다. 통합 화면은 두 canonical source를 공통 projection으로 보여주며, 과거 전용 이력을 전체 이력처럼 소급 합성하지 않는다.

### AUDIT-PLAN-F02 — 전체 mutation coverage 증명 부재

- severity: `P1`
- status: `APPROVED_RESOLUTION`
- finding: Repository에는 transaction을 직접 소유하는 store가 35개이고, endpoint·store별 validation·conflict·no-op 계약이 서로 다르다. “약 40개 store에 기계적으로 연결”만으로는 빠진 경로를 찾을 수 없다.
- impact: 일부 화면만 기록되는 감사 사각이 생기면 Task의 핵심 목표가 실패한다.
- resolution: 구현 전에 `도메인 → endpoint → store method → 성공 event/action/field projection → 실패 종류 → worker 여부 → 테스트` coverage matrix를 코드 또는 Task 산출물의 고정 registry에서 생성한다. 인증된 업무 mutation 전체와 명시적 제외를 1:1로 열거하고, registry와 실제 mutation endpoint의 차이를 실패시키는 contract test를 추가한다.

Coverage 판정은 다음으로 고정한다.

- 포함: 인증 사용자의 명시적 입력으로 durable 업무·관리 상태가 실제 바뀐 create/update/delete/transition, 첨부 metadata, bulk/import의 실제 반영 행, G2 mutation.
- 별도 기존 사건으로 포함: Excel export 성공과 권한 거부. 신규 성공 변경 event로 중복 세지 않는다.
- no-op: 저장 요청이 성공했어도 DB 확정값이 바뀌지 않으면 성공 변경 사건을 만들지 않는다.
- 제외: preview·조회, 알림 읽음 표시, web push 기기 등록, worker·scheduler·자동 만료, provider 내부 처리, 404와 5xx.
- bulk/import: request parent 사건 1건과 실제로 바뀐 대상·field child를 같은 transaction으로 기록한다. 전부 rollback되면 어느 것도 남지 않는다.
- 완료 gate: registry에 포함된 모든 경로의 성공·no-op·rollback test와 실제 mutation endpoint 대비 누락 0을 확인하기 전에는 release 후보로 만들지 않는다. 제외 추가는 implementation convenience로 허용하지 않고 review resolution 밖 제품 결정으로 보고한다.

### AUDIT-PLAN-F03 — 성공 audit의 전달 보장 미확정

- severity: `P1`
- status: `APPROVED_RESOLUTION`
- finding: 기획안은 같은 transaction을 요구하지만 audit insert 실패 때 업무 저장도 실패할지 명시하지 않는다.
- resolution: 성공 mutation audit는 fail-closed다. allowlist projection 또는 audit insert가 실패하면 업무 transaction 전체를 rollback하고 안전한 5xx로 처리한다. 로그인·로그아웃과 이미 거절된 저장 시도의 별도 사건은 원 업무 결과를 바꾸지 않는 best-effort로 두되, 기록 실패는 구조화된 운영 오류로 남긴다.

### AUDIT-PLAN-F04 — 로그인 사건과 correlation lifecycle 불완전

- severity: `P1`
- status: `APPROVED_RESOLUTION`
- finding: 현재 Frontend는 MSAL redirect 후에도 silent token 취득과 저장 account 복원을 같은 경로로 처리한다. 단순 `/api/me` 호출을 로그인으로 세면 새로고침·StrictMode·멀티탭에서 중복된다.
- resolution: MSAL event API의 `LOGIN_SUCCESS`이며 interaction type이 Redirect/Popup인 경우만 대화형 로그인 시작으로 인정한다. 한 interaction을 하나의 사건으로 기록하고 `authenticationOutcome=Succeeded`, `appAccessOutcome=Allowed|ApprovalPending|Inactive`로 앱 결과를 함께 둬 성공과 거부를 이중 집계하지 않는다. 서버가 opaque correlation과 idempotency receipt를 발급하며 actor 소유를 검증한다. correlation은 선택한 MSAL cache 수명과 맞는 browser storage에 account별로 보관해 reload 후에도 이어가고, 명시적 logout 때 인증 header를 포함한 keepalive fetch로 종료 사건을 보낸 뒤 지운다. correlation 부재는 mutation 기록 실패가 아니라 null 연결로 남긴다.

### AUDIT-PLAN-F05 — 값 projection과 무기한 보존의 privacy 경계 부족

- severity: `P1`
- status: `APPROVED_RESOLUTION`
- finding: field allowlist만으로는 자유문·HTML·대용량 값에 비밀이나 불필요한 개인정보가 들어가는 것을 막지 못한다. 실패 사유가 자유 문자열이면 입력값이 우회 유입될 수 있다.
- resolution: 서버 registry가 field별 `ExactScalar`, `MetadataOnly`, `Excluded` projection을 명시한다. exact는 DB에서 다시 읽은 정규화 scalar만 허용하고 크기·형식 bound를 둔다. comment·본문·HTML·rich text·대용량 값은 원문을 저장하지 않고 변경 여부·길이 등 metadata만 기록한다. 실패 사건은 fixed reason code와 서버 소유 안전 문구만 사용한다. Excel은 기존 `ExcelWorkbookBuilder`의 formula-prefix 방어를 재사용한다.

### AUDIT-PLAN-F06 — append-only 강제 수준 부족

- severity: `P2`
- status: `APPROVED_RESOLUTION`
- finding: API를 만들지 않는 것만으로는 DB 직접 update/delete를 막지 못한다.
- resolution: 신규 audit tables에 update/delete 차단 trigger를 추가하고 fresh·upgrade migration test에서 직접 update/delete가 거절되는지 검증한다. 과거 migration은 수정하지 않는다.

### AUDIT-PLAN-F07 — 실패 사건 공통 경계의 구현 가능성

- severity: `P2`
- status: `APPROVED_RESOLUTION`
- finding: 현재 endpoint는 exception, typed result, `ValidationProblem`, local `Safe` helper 등 여러 방식으로 실패를 반환해 전역 exception handler만으로 세 종류를 정확히 분류할 수 없다.
- resolution: request body를 읽거나 저장하지 않는 endpoint audit metadata와 typed failure classifier를 사용한다. route key·actor·domain·action·fixed reason만 HttpContext에 남기고, 각 endpoint의 최종 4xx 결과에서 validation/conflict/duplicate만 별도 연결로 기록한다. 401/403은 기존 authorization 원장, 404와 5xx는 신규 실패 원장에서 제외한다.

### AUDIT-PLAN-F08 — G2와 기존 전용 원장 경계

- severity: `P2`
- status: `APPROVED_RESOLUTION`
- finding: G2는 QMS와 purpose·data workspace가 분리돼 있지만 같은 인증 사용자와 운영 DB를 사용하며, 사용자는 인증 사용자의 업무 mutation 전체를 선택했다. 기존 전용 원장은 종류가 많고 보존·삭제 계약도 서로 다르다.
- recommendation: G2도 포함한다. 기존 전용 원장이 있는 업무 변경도 신규 통합 원장에 병행 기록해 배포 이후 한 canonical source를 만든다. 단순 읽음 표시·web push 기기 등록처럼 저가치 기술 상태는 업무 mutation에서 제외하고, 첨부·프로필 이미지는 binary 없이 확정 metadata만 포함한다.

### AUDIT-PLAN-F09 — schema·조회 시작점·부분 배포 경계

- severity: `P2`
- status: `APPROVED_RESOLUTION`
- finding: 기획안만으로는 삭제된 사용자 표시, 배포 이전 부분 이력, bulk 사건과 부분 계측 상태를 일관되게 설명하기 어렵다.
- resolution: 신규 원장은 사건 ID·발생 UTC·effective/actual actor ID와 당시 표시명·부서 snapshot·domain·action·대상 type/key·outcome·login correlation·request correlation을 고정하고, field child는 field code·projection kind·before/after 또는 metadata를 가진다. actor FK 삭제에 의존하지 않고 snapshot으로 표시를 보존한다. Migration 적용 시각을 `coverage_started_at_utc`로 저장하고 화면·Excel에 “이 시각 이후 전체 기록”을 표시하며, 통합 권한 거부도 같은 시각 이후만 합쳐 completeness를 보존한다. 개발 중 도메인별 부분 계측은 허용하지만 coverage 누락 0 전에는 배포하지 않고, application rollback 시 신규 table은 보존해 forward-fix한다.

## 4. Repository 사실 대조

| 기획 주장 | 대조 결과 | 판정 |
| --- | --- | --- |
| 최신 migration은 `0082`, 신규는 `0083+` | `0081_g2_operations.sql`, `0082_g2_forecast_expiry.sql`가 현재 마지막이다. 구현 시작 때 최신 `origin/main`을 다시 확인해야 한다. | 현재 기준 일치 |
| `Audit.Read.All`과 `QmsPolicies.AuditReadAll` 재사용 | permission·policy·system-administrator seed가 존재한다. | 일치 |
| 운영 인증은 stateless Entra bearer, `/api/me` 부트스트랩 | `AuthorizationServiceCollectionExtensions`, `IdentityEndpointExtensions`, Frontend MSAL 흐름에서 확인했다. | 일치 |
| trusted proxy 뒤 client IP 사용 가능 | `KnownProxies/KnownIPNetworks`, `ForwardLimit=1`, production validation과 `UseForwardedHeaders`가 이미 있다. 공개배포에서 실제 bounded network 설정을 다시 검증해야 한다. | 조건부 일치 |
| 기존 선택 Excel·formula 방어 재사용 | 최대 선택 행, allowlist column registry, quote prefix 방어가 있다. 신규 screen/kind만 확장하면 된다. | 일치 |
| 기존 감사 자산 4종 | migration과 store에 더 많은 도메인 audit/event/history 원장이 있다. | 불일치, F01 |

## 5. 외부 기술 사실 확인

- Microsoft의 MSAL Browser event 문서는 `LOGIN_SUCCESS`와 interaction type을 제공하고 `addEventCallback`으로 이벤트를 구독할 수 있다고 명시한다. 따라서 silent token 성공과 대화형 login 성공을 구분할 수 있다: <https://learn.microsoft.com/en-us/entra/msal/javascript/browser/events>
- ASP.NET Core 공식 문서는 `X-Forwarded-*`를 known proxy/network에서만 신뢰하도록 권고하며, 현재 Repository 설정도 이를 강제한다: <https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer>
- `sendBeacon`은 임의 request property 변경이 필요한 경우 적합하지 않고, MDN은 그런 경우 `fetch`의 `keepalive` 사용을 안내한다. 현재 API는 bearer Authorization header가 필요하므로 logout 기록은 keepalive fetch가 맞다: <https://developer.mozilla.org/en-US/docs/Web/API/Navigator/sendBeacon>, <https://developer.mozilla.org/en-US/docs/Web/API/Request/keepalive>

## 6. SSOT 일관성 audit

감사 범위와 보존 정책을 `interview`, Fable planning, Roadmap 추적 항목 48, 기존 audit migration/store에서 두 가지 검색 방식(정책 문구 검색과 실제 audit write/table 검색)으로 대조했다.

| 위치 | 종류 | 판정·조치 |
| --- | --- | --- |
| `tasks/audit-001-interview.md` | 사용자 결정 canonical source | 로그인·성공/실패·첨부·보존·제외 정책의 기준으로 유지 |
| `tasks/audit-001-planning.md` | Fable 구현 기획 원문 | 원문 유지. Repository 대조 차이는 이 review resolution으로 참조 |
| `docs/00-product-roadmap.md` 추적 항목 48 | 제품 우선순위·상태 | 현재 `미확정`이라 사용자 재정렬 승인과 Task 상태를 구현 산출물에서 갱신 필요 |
| 기존 migration/store의 전용 원장 | 기존 runtime truth | 삭제·통합하지 않는다. 신규 원장은 배포 이후 로그인·성공 변경·실패 시도의 canonical source, 기존 권한 거부 원장은 권한 거부의 canonical source로 정의 |

지금은 read-only audit만 수행했다. Planning 원문과 기존 원장을 합치거나 삭제하는 mutation은 제안하지 않는다.

## 7. 권장 개발 순서

1. 사용자 결정 3건을 확정하고 planning/review resolution·구현 착수를 승인한다.
2. 최신 `origin/main`, instruction chain과 migration 번호를 다시 확인한다.
3. mutation coverage registry와 안전 projection catalog를 먼저 만들고 누락 검사를 고정한다.
4. additive migration으로 신규 append-only 원장·index·trigger·export kind를 추가한다.
5. login/correlation과 공용 transaction recorder를 구현한다.
6. 대상 store를 도메인 단위로 연결하고 각 단위마다 성공·rollback·실패·no-op 회귀를 통과시킨다.
7. 관리자 통합 조회·상세·Excel과 desktop/390px UI를 구현한다.
8. 전체 자동 검증과 독립 검증 뒤 사용자 검수·게시·exact main Azure release gate를 순서대로 진행한다.

## 8. 사용자 결정과 권장 resolution

| 결정 | 권장안 |
| --- | --- |
| G2 포함 여부 | 포함. 같은 인증 사용자에 의한 업무 mutation이며 “전체” 계약의 사각을 없앤다. |
| 기존 전용 원장이 있는 변경 | 기존 원장은 유지하고 신규 통합 원장에도 병행 기록. 신규 원장을 배포 이후 global canonical source로 사용한다. |
| 자유문·대용량 값 | 원문 before/after 대신 변경 여부·길이 등 metadata만 기록. 고정 scalar의 정규화 before/after는 그대로 기록한다. |

이 세 권장안과 review resolution 전체를 사용자가 승인하면 `userDecisionRequiredCount: 0`, planning 승인과 구현 착수 승인으로 기록한다. 이미 승인된 Roadmap 재정렬을 다시 묻지 않는다. PR·main merge·Azure release의 실행 승인은 구현·독립 검증 결과를 본 뒤 별도 gate로 유지한다.

### 사용자 승인 기록

- 승인일: 2026-08-28
- 사용자 원문: `승인.`
- 승인 범위: 위 세 권장안, F01~F09 resolution, planning과 구현 착수.
- 승인에서 제외: PR 생성·main merge·Persistent UAT mutation·Azure release 실행. 이 항목들은 구현·독립 검증 결과 이후 별도 Gate로 유지한다.

## 최종 상태

- reviewStatus: `COMPLETED`
- recommendation: `APPROVED_WITH_RESOLUTION`
- planningApproved: true
- implementationApproved: true
- userDecisionRequiredCount: 0
