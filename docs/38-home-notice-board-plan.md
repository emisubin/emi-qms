# TASK-NOTICE-BOARD-001 — Home 공지사항 게시판 2차 기획(최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-NOTICE-BOARD-001`
- authoringModel: `FABLE_5`
- draftKind: `EXPERIMENT_SECOND_PLANNING`
- interviewSource: `tasks/notice-board-001-interview.md`
- firstPlanningSource: `tasks/notice-board-001-planning.md`
- codexReviewSource: `tasks/notice-board-001-review.md`
- approvalChangeSource: `tasks/notice-board-001-change-001.md`
- reviewFindingResolvedCount: `6/6`
- planningApprovedForExperiment: `true` (change-001 기준, experiment fast-track 한정)
- implementationApprovedForExperiment: `true` (change-001 기준, blocking decision 0 조건)
- mainMergeApproved: `false` / persistentUatApproved: `false` / externalProviderApproved: `false`

이 문서는 `experiment/*` fast-track 계약에 따라 확인 완료 interview, Fable 1차 기획, Codex 내용 review를 모두 다시 읽고 작성한 TASK-NOTICE-BOARD-001의 최종 구현 source of truth다. 1차 기획(`tasks/notice-board-001-planning.md`)과 Codex review(`tasks/notice-board-001-review.md`)는 수정하지 않고 판단 이력으로 보존하며, 두 문서와 이 문서가 다르게 읽히는 지점은 이 문서를 따른다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 참조하며 여기에 복사하지 않는다. 이 문서는 게시(main merge)·Persistent UAT·실제 provider에 대한 어떤 승인도 부여하지 않는다.

## 1. 한 줄 목표

로그인한 승인 완료 active 사용자가 Home 하단에서 전사 공지사항의 최신 글을 읽고, 같은 위치에서 관리자 요청 없이 직접 공지를 작성하며, 작성자·부서·작성 시각이 보존된 전체 게시판에서 본인 글을 정리(soft delete)할 수 있다.

## 2. 해결할 업무 문제(확정 기준선)

- Home 하단 wide widget(`frontend/src/HomePage.tsx`의 `프로젝트 병목`)은 `listProjects(pageSize=5)`를 읽는 병목 Top 5에 고정돼 있어 프로젝트 수가 늘수록 대표성이 낮아지고, 같은 정보는 프로젝트 목록·상세에서도 확인할 수 있다.
- 조직 공통 공지는 시스템 관리자의 수동 `ChannelNotice` 발송뿐이며 일반 사용자가 직접 작성하는 경로가 없다. 이 경로는 unread 알림·Teams delivery와 결합되어 게시판 용도로 재사용할 수 없다(Review 결론: 제거 확정).
- 사용자는 Home 상단 부서 KPI와 중앙 내 업무·Pending·알림을 유지하고 하단 병목 widget만 누구나 입력 가능한 공지사항 게시판으로 교체하도록 직접 지시했다(Roadmap Decision Log 2026-07-21, 우선순위 1.4B).

## 3. 확정 범위

### v1 포함(확정)

- Home 하단 wide slot을 공지사항 widget으로 교체: 최신 5건, 제목·작성자 이름/부서·작성 시각·본문 1줄 preview, `공지 전체 보기`·`공지 작성` 진입점
- 전용 게시판 화면: 최신순 목록(20건 pagination)·상세·작성 form
- 작성자 본인 soft delete(원문·이력 보존, 물리 삭제 금지)
- additive migration `0052` 신규 공지 table(현재 최신은 `0051_optional_kitting_manufacturing_release`로 확인)
- 전용 `/api/notices` 목록·상세·작성·본인 삭제 API — default authorization policy로 인증·승인·active 강제
- 작성 재시도 멱등 처리: client-generated `requestId` + `(author_user_id, request_id)` unique
- server-side 작성자 snapshot(이름·현재 부서)과 server-generated 목록 preview
- desktop·390px 한 열 card 구성, page-level horizontal overflow 0, 접근 가능한 오류·성공 안내

### 보류(후속 Task 후보 — v1에서 구현하지 않음)

- 작성 후 edit와 revision history: silent edit 감사 위험 때문에 v1 제외. 오작성은 본인 soft delete 후 새 글 작성으로 정정한다. 향후 edit 도입 시 row version/CAS와 append-only revision을 함께 설계한다(`NOTICE-EDIT-AUDIT`, P3).
- System Administrator의 타인 글 삭제·복구·moderation: 전사 작성 권한과 moderation 권한은 별개 정책이며 별도 사용자 결정이 필요하다. 운영 승격 전 moderation 정책을 별도 Task 후보로 Roadmap에서 추적한다(`NOTICE-MODERATION-GAP`, P3).
- 공지 고정·검색·분류·댓글·반응·첨부·읽음 확인 통계·공지 알림 opt-in: 각각 별도 `NEW_FEATURE`.
- 게시판 Excel/PDF 내보내기: 현재 사용자 문제에 불필요.

### 제거(구현 금지)

- `ChannelNotice`/`notifications` 경로 재사용(unread·Teams delivery 오염)
- Home widget 안에 목록·작성·상세·pagination을 모두 넣는 inline-only 구성
- Frontend만의 권한 판단, client-supplied 작성자·부서 snapshot, HTML/Markdown 렌더링·auto-link
- 별도 audit table과 `0042`식 append-only guard trigger(v1 lifecycle에 과도 — 1차 기획 16절 4번 선택지 B는 채택하지 않음)

### 명시적 제외(무변경 보존)

- Home 상단 부서 KPI·영업 KPI panel·중앙 내 업무/Pending/알림 widget의 데이터 소스·배치·deep link
- 프로젝트 목록·상세의 병목 집계와 상태 계산(`listProjects` API 자체와 기존 소비자 무수정)
- 공지 작성·삭제에 따른 내 업무·인앱 unread·Teams·메일 자동 발송
- 대표 repo·`main`·Persistent UAT migration/runtime handover·실제 provider·push·PR·merge

## 4. 대상 사용자와 권한

| 사용자/역할 | 조회 | 작성 | 삭제 |
| --- | --- | --- | --- |
| 승인 완료 active 내부 사용자 전체(부서 무관) | 삭제되지 않은 전체 공지 | 가능 | 본인 글만(soft delete) |
| System Administrator | 일반 사용자와 동일(v1 특례 없음) | 동일 | 본인 글만 |
| 승인 대기·비활성 사용자, 미인증 요청 | 차단 | 차단 | 차단 |

- 신규 role·permission을 만들지 않는다. 기존 default authorization policy(`RequireAuthenticatedUser` + `OperationalUserRequirement`, `backend/src/Emi.Qms.Api/Authorization/AuthorizationServiceCollectionExtensions.cs`)를 endpoint group의 `.RequireAuthorization()`으로 재사용한다. Repository에서 이 정책이 승인 대기·비활성 사용자를 차단함을 확인했다.
- 검수 사용자 전환(`X-Qms-Test-User`) 중 작성·삭제는 기존 mutation 계약과 동일하게 effective 사용자(`qms.user_id` claim)를 행위자로 기록한다. `backend/src/Emi.Qms.Api/Pending/PendingEndpointExtensions.cs`의 `GetActor` 패턴을 따른다.
- client는 작성자·부서 정보를 요청 body로 전달할 수 없고, 전달해도 서버가 무시하지 않고 계약 위반으로 취급하지 않도록 identity field 자체를 요청 계약에서 제외한다.

## 5. 핵심 사용자 시나리오

### A — Home에서 최신 공지 확인

1. 사용자가 로그인 후 Home에 진입하면 상단 KPI·중앙 widget은 기존과 동일하게 표시되고, 하단 wide slot에 최신 공지 5건(제목, 작성자 이름·부서, 작성 시각, 1줄 preview)이 표시된다.
2. 공지 항목을 누르면 게시판 화면의 해당 글 상세로 이동하고, `공지 전체 보기`로 게시판 목록으로 이동한다.
3. 공지 조회 실패는 해당 widget에만 오류·`다시 시도`를 표시하고 상단·중앙 widget을 차단하지 않는다(기존 widget failure isolation 유지).

### B — 공지 작성(멱등)

1. 사용자가 Home widget 또는 게시판의 `공지 작성`을 누르면 작성 form이 열리고, 이 시점에 client가 새 `requestId`(UUID)를 생성한다.
2. 제목·본문 입력 후 등록하면 저장 중 버튼이 비활성화되고, 서버는 validation·snapshot 기록 후 저장한다.
3. 응답 손실·network 오류 뒤 같은 form에서 재시도하면 같은 `requestId`가 재전송되고, 서버는 `(author_user_id, request_id)` unique로 기존 글을 그대로 반환해 중복 공지를 만들지 않는다.
4. 저장 성공 시 form 근처에 성공 안내를 표시하고 게시판 목록을 즉시 갱신한다. Home 재진입(또는 Home widget reload) 시 새 공지가 최신 목록에 보인다. 인앱 알림·내 업무·외부 발송은 발생하지 않는다.

### C — 작성 실패와 복구

1. 빈 제목, 길이 초과, 공백만 있는 본문 등은 서버가 400과 field 단위 한글 메시지로 거부한다.
2. Frontend는 오류를 해당 field에 연결하고 첫 오류로 focus를 이동하며 `aria-live` 안내를 제공한다. 입력값은 유지된다.
3. 수정 후 재등록하면 정상 저장된다(새 제출 시도는 새 `requestId`를 사용하고, 같은 시도의 network 재시도만 같은 `requestId`를 재사용한다).

### D — 본인 글 삭제

1. 작성자가 게시판 상세에서 본인 글의 `삭제`를 실행하고 확인한다.
2. 서버가 작성자 본인 여부를 검증하고 `deleted_at_utc`·`deleted_by_user_id`를 기록하는 soft delete를 수행한다. 행과 원문은 보존된다.
3. 목록·Home widget·상세에서 해당 글이 사라진다. 같은 작성자의 반복 삭제는 멱등 성공, 타인 글 삭제는 403, 존재하지 않는 글은 404다.

## 6. 업무 규칙과 불변조건

- Backend가 조회·작성·삭제 허용과 입력 validation의 authoritative source다. Frontend 숨김·비활성화는 보조 수단이다.
- 승인 대기·비활성·미인증 사용자는 조회·작성·삭제 모두 서버에서 차단된다(default policy 재사용, 완화 금지).
- 공지 경로는 `notifications`, `notification_recipients`, `work_items`, delivery, 외부 provider에 어떤 row·호출도 만들지 않는다. `NotificationDispatcher`·delivery worker를 호출하지 않는다.
- 작성자 snapshot(이름·현재 부서)은 insert 시점에 서버가 `qms_users`와 현재 부서를 조회해 같은 transaction/statement로 저장하며, 이후 부서 이동·이름 변경에도 바뀌지 않는다. 부서가 없는 허용 계정은 nullable snapshot으로 저장하고 화면에 `부서 미지정`을 표시한다.
- 공지 행은 물리 삭제하지 않는다. 삭제는 soft delete뿐이며 원문·작성/삭제 identity·시각이 행에 보존된다.
- Home 상단·중앙 widget과 프로젝트 병목 집계 로직, 기존 `ChannelNotice` 수동 발송 기능은 변경하지 않는다.
- 기존 migration은 수정하지 않고 additive `0052` 하나만 추가하며 Persistent UAT에 적용하지 않는다.
- ReviewSafe의 공통 mutation 차단을 우회하지 않는다. 검증은 기존 owned validation runtime 또는 isolated E2E만 사용하고 canonical 5174/5081·Persistent UAT handover를 수행하지 않는다.

## 7. 데이터 모델과 lifecycle

신규 table(가칭 `notice_posts`, migration `0052`). 의미 계약은 확정이고 세부 컬럼명·SQL 형태는 구현 조사에서 기존 convention에 맞춰 확정한다.

| 필드(의미) | 계약 |
| --- | --- |
| id | PK (uuid) |
| 제목/본문 | trim 후 저장, 제목 1~100자·본문 1~2000자, 개행 보존 plain text |
| 작성자 FK | `qms_users` 참조, server-side effective 사용자 |
| 작성자 이름·부서명 snapshot | insert 시점 서버 조회, 부서 snapshot은 nullable |
| request_id | client-generated UUID, `(author_user_id, request_id)` unique |
| 작성 시각 | UTC, 서버 기록 |
| 삭제 시각·삭제자 FK | nullable, soft delete 시 기록 |

```text
Published(작성 완료) → (작성자 본인 삭제) Deleted(soft, 원문·이력 보존)
```

- v1에는 수정이 없으므로 row version/CAS를 두지 않는다. 삭제는 `id + 미삭제 조건`의 조건부 atomic update로 처리한다.
- 조회 정렬은 `created_at_utc DESC, id DESC`로 고정하고 미삭제 행 대상 목록 조회를 지원하는 index를 추가한다.
- 별도 audit table·guard trigger는 만들지 않는다(3절 제거 확정).
- migration 검증은 fresh `0001→0052`와 기존 `0051→0052` upgrade를 모두 수행한다.

## 8. API 계약

구현 위치: `backend/src/Emi.Qms.Api/Notices/`(Contracts·Store·EndpointExtensions 신규)와 `Program.cs` 등록. store는 `PendingStore`류의 Npgsql data source 패턴, endpoint는 `MapGroup("/api/notices").RequireAuthorization()`을 사용한다.

| Endpoint | 계약 |
| --- | --- |
| `GET /api/notices?page&pageSize` | 미삭제 공지 최신순 목록. `page >= 1`, `1 <= pageSize <= 100`을 서버 검증(위반 시 400). Home은 `pageSize=5`, 게시판은 `20`. 응답 항목은 id, 제목, server-generated preview, 작성자 이름·부서 snapshot, 작성 시각과 총 건수·page 정보 |
| `GET /api/notices/{id}` | 본문 전문(개행 보존)을 포함한 상세. 미존재·삭제된 글은 404 |
| `POST /api/notices` | body: `requestId`, `title`, `body`만. validation 실패는 field 단위 한글 메시지 400, `requestId`가 유효한 UUID가 아니면 400. 성공 시 상세 반환. 같은 작성자의 같은 `requestId` 재시도는 기존 글을 반환하는 멱등 응답(중복 insert 없음) |
| `DELETE /api/notices/{id}` | 작성자 본인만. 본인 반복 삭제 멱등 성공, 타인 403, 미존재 404 |

- preview는 서버가 생성한다: 개행·연속 공백을 단일 공백으로 접은 plain text의 앞 100자(초과 시 절단, 말줄임 표시는 Frontend 담당). 목록 응답에 본문 전문을 싣지 않고 상세만 전문을 반환한다.
- 작성은 snapshot 조회를 포함한 단일 transaction/statement insert, 삭제는 조건부 atomic update다. 중복 submit의 1차 방어는 Frontend 잠금, 최종 방어는 request-id unique다.
- 오류 형태·한글 메시지 convention은 `PendingEndpointExtensions`·`NotificationPreferenceEndpointExtensions`의 기존 형태를 따른다.

## 9. Frontend 계약

- `frontend/src/App.tsx`의 `View` union에 `{ kind: 'notice-board'; noticeId?: string }`를 추가하고 기존 route 해석 패턴에 맞춰 게시판 경로를 연결한다. 신규 `frontend/src/NoticeBoardPage.tsx`(목록·상세·작성), 신규 `frontend/src/notices.ts`(type), `frontend/src/api.ts`에 기존 `fetchJson(developmentUserKey, …)` 패턴으로 `listNotices`/`getNotice`/`createNotice`/`deleteNotice`를 추가한다.
- `frontend/src/HomePage.tsx`: 하단 wide `HomeWidget`(`프로젝트 병목`)의 data source·presentation만 공지로 교체한다. `projectsState`/`loadProjects`/`listProjects` import를 공지 상태·load로 대체하고, `visibleWidgetCount` 집계도 공지 상태로 치환한다. 상단·중앙 widget, empty 상태의 `프로젝트로 이동` 등 다른 소비자와 `api.ts`의 `listProjects` 자체는 무수정. 게시판 이동·공지 상세 이동을 위한 새 callback prop을 추가한다.
- 상태 계약: Home widget은 기존 `HomeWidget` + `WidgetState` + generation ref 패턴을 그대로 재사용한다(독립 loading/empty/error/`다시 시도`, 403은 기존 `widgetError` 권한 문구). 게시판 화면은 기존 `LoadState` 구분을 따르고, empty 목록에서도 `공지 작성` 진입점을 항상 표시한다.
- Action Feedback: 작성·삭제는 저장 중 버튼 비활성·중복 submit 차단(기존 ref fence 패턴), action 근처 성공/실패 안내, field 오류 연결·첫 오류 focus·`aria-live` 안내. 작성 성공 시 게시판 목록을 즉시 갱신하고 Home widget은 재진입·reload 시 최신을 반영한다.
- 렌더링·접근성: 본문은 개행 보존 plain text로만 렌더링(HTML 미해석·auto-link 없음), 의미 있는 heading·list 구조, keyboard 접근과 focus 이동 회귀 확인.
- 390px: PC 표를 축소하지 않고 목록·작성·상세를 한 열 card로 배치하며 최신 공지와 `공지 작성`을 같은 열에 둔다. page-level horizontal overflow 0. 스타일은 `frontend/src/styles.css`의 기존 Home·card convention을 재사용한다.

## 10. Review Finding Resolution 매핑

| Finding | Severity | 이 문서의 해소 위치 |
| --- | --- | --- |
| `NOTICE-CREATE-RETRY-DUPLICATE` | P2 | 5절 시나리오 B, 7절 request_id unique, 8절 POST 멱등 계약 |
| `NOTICE-AUTHOR-SNAPSHOT-TRUST` | P2 | 4절 identity field 제외, 6절 server-side snapshot 불변조건, 8절 body 계약 |
| `NOTICE-LIST-PAYLOAD` | P2 | 8절 server-generated preview 100자·상세만 전문 반환 |
| `NOTICE-DELETE-SEMANTICS` | P2 | 5절 시나리오 D, 8절 DELETE 멱등 성공/403/404 고정 |
| `NOTICE-EDIT-AUDIT` | P3 | 3절 보류 — v1 edit 제외, 후속 edit는 revision audit+CAS 포함 별도 기능 |
| `NOTICE-MODERATION-GAP` | P3 | 3절 보류 — 운영 승격 전 moderation 정책을 별도 Task 후보로 추적 |

Review의 `유지` 권고는 3·4·8·9절에 전부 보존했고, `제거` 판정 4건은 3절 제거 목록으로 고정했다.

## 11. 비차단 정책 확정 기록

Interview 4절의 7개 비차단 항목을 standing instruction에 따라 다음과 같이 확정한다. 모두 Repository 근거가 있고 기존 보안·권한·workflow 불변조건을 보존하므로 blocking decision이 아니다.

| 번호 | 항목 | 확정안 | 근거·trade-off |
| ---: | --- | --- | --- |
| 1 | v1 동작 범위 | 작성·목록·상세 + 작성자 본인 soft delete | 오작성 정정 경로 확보. edit·moderation은 감사·권한 정책이 별도로 필요해 보류(trade-off: 수정은 삭제 후 재작성으로 우회) |
| 2 | 표시 정책 | Home 최신 5건 + 1줄 preview + 전용 게시판 view | 기존 병목 Top 5와 동일한 widget 밀도, inline-only는 공지 증가 시 대표성 문제를 반복 |
| 3 | 작성 권한 | default policy만 사용, 검수 전환 중 effective 사용자를 작성자로 기록 | 기존 mutation 계약(`GetActor`)과 일관, 신규 permission 불필요 |
| 4 | 데이터 lifecycle | 작성자 FK + 이름·부서 snapshot + soft delete, audit table·trigger 없음 | v1에 edit·hard delete가 없어 행 보존으로 충분(trade-off: 향후 edit 도입 시 revision 설계 필요) |
| 5 | 입력 계약 | 제목 1~100자·본문 1~2000자, trim 후 빈 값 거부, 개행 보존 plain text, HTML 미해석·auto-link 없음 | XSS·렌더링 오염 차단, 기존 plain text convention과 일치 |
| 6 | 알림 경계 | 어떤 알림·내 업무·외부 발송도 생성하지 않음. opt-in은 별도 `NEW_FEATURE` | interview 성공 기준·명시적 제외와 일치, unread·Teams 오염 방지 |
| 7 | 조회 정책 | `created_at_utc DESC, id DESC` offset pagination, 서버 range 검증, 게시판 20건·Home 5건 | 기존 목록 계약과 일관, 현재 규모에서 keyset 불필요 |

## 12. Task 고유 안전 경계

- Persistent UAT 영향: 없음. `0052`는 isolated fresh PostgreSQL과 기존 `0051→0052` upgrade에서만 검증한다.
- migration: additive `0052` 1건, 기존 migration 무수정, forward-fix 원칙.
- 외부 발송/실제 데이터: 없음. provider 호출 없음, synthetic data만 사용.
- runtime 교체: 없음. 검증은 owned validation runtime·isolated E2E 범위이며 canonical runtime handover와 ReviewSafe 우회를 하지 않는다.
- 별도 사용자 승인 필요: push·PR·merge(main merge 승인 3회 경계 유지)·대표 repo 반영·Persistent UAT 적용·moderation 정책 확정.

## 13. 검증 계획

- Backend 최소 테스트(신규 notice API tests): 작성·목록·상세·삭제 성공 경로 / 승인 대기·비활성·미인증 차단 / field validation(빈 값·길이 초과·공백만·잘못된 requestId·page range) / 같은 requestId 재시도 멱등(단일 행 유지) / 타인 삭제 403·반복 삭제 멱등·미존재 404 / `notifications`·`work_items`·delivery row 미생성 / 정렬·pagination·preview·부서 없는 작성자 snapshot.
- Migration: `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`의 최신 version 단언(현재 `0051_optional_kitting_manufacturing_release` 2곳)을 `0052`로 갱신하고 fresh·upgrade 경로를 검증한다.
- Frontend: 신규 `NoticeBoardPage` unit test(목록·상세·작성·삭제·validation·중복 submit 차단), Home 공지 widget의 loading/empty/error/ready·retry, `frontend/tests/App.test.tsx`의 기존 `프로젝트 병목` 단언 교체와 상단·중앙 widget 무변경 회귀, lint/typecheck/unit/build 전체.
- 통합: isolated Full-Stack E2E spec(공지 조회→작성→멱등 재시도→삭제→Home 반영)과 desktop·390px screenshot. Backend 전체 test suite 회귀.
- PR/CI: fast-track 범위에서 local 검증으로 대체하며 push·PR·CI는 별도 승인 전 수행하지 않는다.
- 사용자 검수: `BATCHED_FINAL` — 마지막 일괄 검수 목록에 desktop·390px 공지 조회·작성·삭제와 Home 상단/중앙 무변경 확인 항목을 추가하고 user validation checklist로 추적한다(`사용자 검수 대기` 유지).

## 14. 완료 기준과 중단 조건

- 완료: 3절 v1 포함 범위가 6~9절 계약대로 동작하고, 13절 자동 검증 전부 통과, Open P0/P1/P2 = 0, 5종 종료 산출물(Implementation report·SOP·User manual·Roadmap update·User validation checklist)의 상태·위치 추적, 사용자 검수 상태 `사용자 검수 대기 — 마지막 일괄 검수` 기록, local experiment commit까지. push·PR·merge·게시·Persistent UAT는 완료 조건이 아니며 수행하지 않는다.
- 중단: 문서·구현의 의미 있는 충돌 발견 / Home 상단·중앙 계약을 깨지 않고 구현 불가 / 신규 권한·외부 연동·알림 생성이 필요해지는 범위 확장 / fast-track 제외 경계(대표 repo·`main`·Persistent UAT·provider)를 넘어야 하는 경우 — 즉시 중단하고 보고한다.

## 15. Codex 구현 지시문(권장 순서)

1. additive `0052`: 공지 table, `(author_user_id, request_id)` unique, 목록 index, FK를 생성하고 fresh `0001→0052`·기존 `0051→0052`를 검증한다. 기존 migration은 수정하지 않는다.
2. `backend/src/Emi.Qms.Api/Notices/` contract/store/endpoints: default policy `.RequireAuthorization()`, `GetActor`식 effective 사용자, server-side snapshot 단일 transaction insert, 8절 validation·멱등 create·조건부 soft delete·pagination·preview를 구현하고 `Program.cs`에 등록한다. 공지 경로에서 `notifications`·`work_items`·delivery·provider를 호출하지 않는다.
3. Frontend API/type(`api.ts`·`notices.ts`)과 `notice-board` view·`NoticeBoardPage`(목록·상세·작성·본인 삭제)를 9절 Action Feedback·접근성·390px 계약으로 구현한다.
4. `HomePage.tsx` 하단 wide widget의 data source·presentation만 공지로 교체하고(`listProjects` 소비 제거, 다른 소비자 무수정) 상단·중앙 회귀를 test로 고정한다.
5. Backend 전체·Frontend 전체·migration·isolated Full-Stack E2E·desktop/390px screenshot·5종 종료 산출물·Roadmap/완료 원장 갱신 후 local experiment commit까지 수행한다. push·PR·merge는 하지 않는다.

## 16. Roadmap 연결

- 선행: TASK-HOME-001/002 experiment scope(`EXPERIMENT_COMPLETE / BATCHED_FINAL`) — 충족.
- 현재: Roadmap 1.4B `Experiment Fast-Track In Progress`, 실험 완료 원장 우선순위 1 `IN_PROGRESS / FAST_TRACK`과 일치.
- 후속 분리 항목: 공지 첨부(첨부·사진 storage Task 이후), 관리자 moderation, 공지 알림 opt-in, 검색·고정·읽음 통계, 운영 전환.

openBlockingDecisionCount: 0
