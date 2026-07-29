# TASK-NOTICE-BOARD-001 — Home 공지사항 게시판 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/notice-board-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`
- draftKind: `EXPERIMENT_FIRST_PLANNING`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: Home 하단이 `listProjects(pageSize=5)` 기반 프로젝트 병목 Top 5에 고정돼 있어 프로젝트 수가 늘수록 홈 고정 공간의 대표성이 떨어지고, 일반 사용자가 직접 작성하는 공용 게시판 능력이 없다.
- 대상 사용자·역할: 로그인 완료·승인 완료·active 상태의 전체 내부 사용자. 별도 부서 제한 없음.
- 정상 흐름: Home 하단에서 최신 공지를 읽고 → 같은 위치의 진입점에서 공지를 작성하며 → 작성자·부서·작성 시각을 확인한다.
- 예외·복구 흐름: 비승인·비활성·미인증 요청은 서버에서 차단, validation 실패는 field 단위 한글 안내, 중복 submit 차단, Home widget 부분 실패는 독립 error·retry.
- 확정한 정책과 명시적 제외: Home 상단 부서 KPI·중앙 내 업무/Pending/알림 유지, 프로젝트 병목 집계 자체는 프로젝트 목록·상세에 유지, 공지 작성이 내 업무·인앱 unread·Teams·메일을 자동 생성하지 않음, 첨부·사진·댓글·반응·읽음 통계 제외, 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge 제외.
- planning으로 넘긴 비차단 미결정 사항: interview 4절의 7개 항목(v1 동작 범위, 표시 정책, 작성 권한, 데이터 lifecycle, 입력 계약, 알림 경계, 조회 정책). 본 문서 12절과 16절에서 선택지·권장안을 제시하며, experiment standing instruction에 따라 Codex review 뒤 Fable 2차 기획에서 권장안을 자동 채택한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

로그인한 승인 완료 active 사용자가 Home 하단에서 전사 공지사항의 최신 글을 읽고, 같은 위치에서 별도 관리자 요청 없이 직접 공지를 작성하며, 작성자·부서·작성 시각이 보존된 전체 게시판을 열람할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 Home 하단 `프로젝트 병목` widget(`frontend/src/HomePage.tsx`의 wide `HomeWidget`)은 `listProjects(pageSize=5)`를 읽어 프로젝트 5건의 병목만 보여준다. 프로젝트가 늘수록 Top 5의 대표성이 낮아지고, 이 정보는 프로젝트 목록·상세에서도 동일하게 확인할 수 있어 홈 고정 공간의 가치가 중복된다.
- 조직 공통 공지는 현재 시스템 관리자의 수동 `ChannelNotice` 발송(`notifications` + TeamsChannel delivery, `backend/src/Emi.Qms.Api/Notifications/`)뿐이며, 일반 사용자가 직접 작성할 수 있는 경로가 없다. 이 경로는 외부 provider 설정과 unread 알림 계약에 묶여 있어 게시판 용도로 재사용하면 불필요한 발송·unread가 생긴다.
- 우회 방식은 외부 메신저·구두 전달이며 시스템 안에 작성자·시각 이력이 남지 않는다.
- 이 기능이 없으면 전사 공통 정보(설비 점검, 일정 안내, 사내 공지 등)가 업무 시스템 밖에서 유통되고, Home의 고정 공간은 갈수록 가치가 낮은 정보에 쓰인다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 승인 완료 active 내부 사용자 전체(부서 무관) | 공지 목록·상세 조회, 공지 작성, 본인 글 삭제(soft delete, 권장안 채택 시) | 삭제되지 않은 전체 공지 | 신규 공지 작성, 본인 작성 글 삭제 |
| 승인 대기(`approval_pending`)·비활성(`inactive`) 사용자 | 없음 | 없음(기본 정책이 차단) | 없음 |
| 미인증 요청 | 없음 | 없음 | 없음 |
| System Administrator | 일반 사용자와 동일(v1) | 동일 | 동일. 타인 글 관리(moderation)는 v1 제외·후속 결정 |

- 신규 permission·role을 만들지 않는다. 기존 default authorization policy(`RequireAuthenticatedUser` + `OperationalUserRequirement`, `backend/src/Emi.Qms.Api/Authorization/AuthorizationServiceCollectionExtensions.cs`)가 승인 대기·비활성 사용자를 이미 차단하므로 조회·작성 모두 이 정책을 사용한다.
- 검수 사용자 전환(`X-Qms-Test-User`) 중 작성은 기존 mutation 계약과 동일하게 effective 사용자(`qms.user_id` claim)를 작성자로 기록한다. Pending 생성의 `GetActor` 패턴(`backend/src/Emi.Qms.Api/Pending/PendingEndpointExtensions.cs`)과 같다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — Home에서 최신 공지 확인

1. 사용자가 로그인 후 Home에 진입한다.
2. 시스템이 상단 부서 KPI·중앙 내 업무/Pending/알림 widget을 기존과 동일하게 표시하고, 하단 wide slot에 최신 공지 목록(제목, 작성자 이름·부서, 작성 시각, 본문 1줄 preview)을 표시한다.
3. 사용자는 공지 항목을 눌러 상세를 열거나 `공지 전체 보기`로 게시판 화면으로 이동한다.

### 시나리오 B — 공지 작성

1. 사용자가 Home 공지 widget 또는 게시판 화면의 `공지 작성` 버튼을 누른다.
2. 제목·본문을 입력하고 등록한다. 시스템은 field validation을 수행하고 저장 중 중복 submit을 차단한다.
3. 저장 성공 시 작성 UI 근처에 성공 안내를 표시하고 목록을 갱신한다. 새 공지에는 작성 시점의 작성자 이름·부서가 함께 저장·표시된다. 인앱 알림·내 업무·외부 발송은 발생하지 않는다.

### 시나리오 C — 작성 실패와 복구

1. 사용자가 제목을 비우거나 허용 길이를 초과한 본문으로 등록을 시도한다.
2. 시스템이 400과 field 단위 한글 메시지를 반환하고, Frontend는 해당 field에 오류를 연결하고 첫 오류로 focus를 이동하며 `aria-live` 안내를 제공한다.
3. 사용자가 입력을 고쳐 다시 등록하면 정상 저장된다. 저장 요청 중 network 오류가 나면 입력값을 유지한 채 재시도 안내를 표시한다.

### 시나리오 D — 본인 글 삭제(권장안 채택 시)

1. 작성자가 게시판 상세에서 본인 글의 `삭제`를 실행하고 확인한다.
2. 시스템이 작성자 본인 여부를 서버에서 검증하고 soft delete(삭제 시각·삭제자 기록)로 처리한다. 행은 물리 삭제하지 않는다.
3. 목록·Home widget에서 해당 글이 사라지고, 이력은 DB에 보존된다. 타인 글의 삭제 요청은 403으로 차단된다.

## 5. 기능 요구사항

### 필수

- [ ] Home 하단 `프로젝트 병목` widget을 공지사항 widget으로 교체(상단·중앙 widget과 deep link는 무변경)
- [ ] 공지 persistence: additive migration `0052`(신규 table), 기존 migration 무수정
- [ ] Backend API: 공지 목록(최신순 pagination)·상세·작성. 기본 정책으로 인증·승인·active 강제, field validation은 안정적 status와 한글 메시지
- [ ] 작성자 identity 보존: 작성자 user id FK + 작성 시점 이름·부서 snapshot + 작성 시각(UTC)
- [ ] 공지 작성이 인앱 알림·내 업무·Teams·메일·`notifications` row를 생성하지 않음
- [ ] Frontend: Home 최신 공지 widget(독립 loading/empty/error/retry), 전체 게시판 화면(목록·상세·작성), 중복 submit 차단과 접근 가능한 오류 안내
- [ ] desktop과 390px에서 page-level horizontal overflow 0

### 선택

- [ ] 작성자 본인 soft delete(12절 권장안 — 2차 기획에서 확정)
- [ ] 게시판 목록의 추가 page 탐색(더 보기 또는 page 이동)

### 명시적 제외

- [ ] Home 상단 부서 KPI·중앙 내 업무/Pending/알림 widget 변경
- [ ] 프로젝트 목록·상세의 병목 집계 제거 또는 상태 계산 변경(`ProjectBottleneckDomain` 무수정)
- [ ] 공지 작성 시 내 업무·인앱 unread·Teams·메일 자동 발송과 `ChannelNotice` 경로 연결
- [ ] 첨부파일·사진·댓글·반응·읽음 확인 통계·고정(pin)·검색
- [ ] 타인 글 수정·삭제와 관리자 moderation 화면
- [ ] 대표 repo·`main`·Persistent UAT migration/runtime handover·실제 provider·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| Home 공지 widget(하단 wide slot) | 로그인 후 Home | 최신 공지 5건: 제목, 작성자 이름·부서, 작성 시각, 본문 1줄 preview | 공지 클릭(상세), `공지 전체 보기`, `공지 작성` | widget 독립 loading/empty/error·`다시 시도`(기존 `HomeWidget` 상태 계약 재사용) |
| 공지 게시판(목록) | Home widget `공지 전체 보기` | 최신순 공지 목록과 pagination, 총 건수 | 공지 선택, page 탐색, `공지 작성` | loading/empty/error 구분, empty 시 첫 공지 작성 유도 |
| 공지 상세 | 목록·widget에서 선택 | 제목, 본문 전문(개행 유지), 작성자 이름·부서, 작성 시각 | 목록으로 복귀, (권장안) 본인 글 삭제 | 삭제 확인 후 성공 안내, 타인 글 삭제 시도는 서버 403 안내 |
| 공지 작성 form | widget·게시판의 `공지 작성` | 제목·본문 입력, 길이 안내 | 입력 후 등록, 취소 | 저장 중 버튼 비활성, field 오류 연결·첫 오류 focus·`aria-live`, 성공 시 action 근처 안내와 목록 갱신 |

확인할 UX 항목:

- 사용자가 현재 상태를 이해할 수 있는가 — widget과 게시판의 loading/empty/error를 기존 Home 상태 문구 패턴으로 구분한다.
- 다음 행동이 명확한가 — 빈 목록에서도 `공지 작성` 진입점이 항상 보인다.
- 저장·변경 결과가 action 근처에 보이는가 — 작성 form 내부에 성공·실패 feedback을 표시한다.
- 권한 부족·오류 상태가 명확한가 — 403은 위젯 공통 권한 안내 문구 패턴(`widgetError`)을 따른다.
- 좁은 화면에서도 핵심 행동이 가능한가 — 390px에서 목록·작성·상세를 한 열 card로 배치하고 PC 표를 축소하지 않는다.

## 7. 업무 규칙과 불변조건

- Backend가 조회·작성·삭제 허용과 입력 validation의 authoritative source다. Frontend 숨김·비활성화는 보조 수단이다.
- 승인 대기·비활성·미인증 사용자는 조회·작성 모두 서버에서 차단된다(기존 default policy 재사용, 완화 금지).
- 공지 작성·삭제는 `work_items`, `notifications`, delivery, 외부 provider에 어떤 row도 만들지 않는다.
- 작성자 identity와 작성 시각은 보존한다. 작성 시점의 이름·부서 snapshot은 이후 부서 이동·이름 변경에도 바뀌지 않는다.
- 공지 행은 물리 삭제하지 않는다(soft delete 채택 시 삭제 이력 보존, 미채택 시 삭제 자체 없음).
- Home 상단·중앙 widget의 데이터 소스·배치·deep link와 프로젝트 병목 집계 로직은 변경하지 않는다.
- 기존 migration은 수정하지 않고 `0052` additive만 추가하며 Persistent UAT에는 적용하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 공지 글(가칭 `notice_posts`) | 제목, 본문, 작성자 FK(`qms_users`), 작성자 이름·부서명 snapshot, 작성 시각 UTC | 신규(migration `0052`) | 행 보존, 물리 삭제 금지 |
| 삭제 표시(soft delete) | 삭제 시각·삭제자 FK. 목록·상세에서 제외 | 신규(같은 table의 nullable 열) | 삭제 후에도 원문·이력 보존 |
| 작성자 identity | `qms.user_id` claim 기반 effective 사용자 | 기존 계약 재사용 | 검수 전환 중에도 effective 사용자로 기록 |

```text
Published(작성 완료) → (작성자 본인 삭제 시) Deleted(soft, 이력 보존)
```

- v1에는 수정(edit)이 없으므로 row version CAS가 필요 없다. 삭제는 `id + 미삭제 조건`의 atomic update로 처리하고, 이미 삭제된 글은 멱등적으로 성공 또는 명시적 409 중 하나로 확정한다(2차 기획에서 확정, 권장: 멱등 성공).
- 별도 audit table은 v1에서 만들지 않는다. 행 자체가 append-only에 가깝고 삭제자·삭제 시각이 열로 남는다. `0042`식 append-only guard trigger는 선택 항목으로 2차 기획에서 확정한다(16절 4번).
- 내부 컬럼명·SQL 형태는 후보이며 구현 조사에서 확정한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 인증·승인·active 강제(default policy), 제목·본문 길이·공백 validation, 작성자 snapshot 기록, 본인 글만 삭제, 알림·내 업무 미생성.
- 필요한 조회와 mutation(후보): `GET /api/notices`(최신순, `page`/`pageSize`), `GET /api/notices/{id}`, `POST /api/notices`, (권장안 채택 시) `DELETE /api/notices/{id}`.
- 권한·validation: endpoint group에 `.RequireAuthorization()`만 사용(신규 policy·permission 없음). validation 실패는 field 단위 한글 메시지의 400, 타인 글 삭제는 403, 없는 글은 404. `NotificationPreferenceEndpointExtensions`·`PendingEndpointExtensions`의 기존 형태를 따른다.
- transaction·동시성·idempotency: 작성은 단일 insert(단일 statement, 별도 transaction 불필요), 삭제는 조건부 atomic update. 중복 submit은 Frontend 차단 + 서버는 각 요청을 독립 insert로 처리하므로 정확히 한 번을 주장하지 않는다(연타로 인한 이중 등록은 Frontend 잠금이 기본 방어이고, 기존 제조 화면의 ref fence 패턴을 재사용한다).
- audit trail: 작성자·작성 시각·삭제자·삭제 시각이 행에 보존된다.
- 외부 provider 영향: 없음. `NotificationDispatcher`·delivery worker 경로를 호출하지 않는다.
- 구현 위치 후보: `backend/src/Emi.Qms.Api/Notices/`(Contracts, Store, EndpointExtensions)로 기존 영역별 directory convention을 따르고, store는 `PendingStore`와 같은 Npgsql data source 패턴을 사용한다.

## 10. Frontend 고려사항

- route/component: `App.tsx`의 `View` union에 게시판 view(예: `{ kind: 'notice-board'; noticeId?: string }`)를 추가하고, 신규 `NoticeBoardPage.tsx`(목록·상세·작성)와 `HomePage.tsx`의 공지 widget으로 구성한다. `api.ts`에 `listNotices`/`getNotice`/`createNotice`(/`deleteNotice`)를 추가하고 type은 신규 `notices.ts`에 둔다.
- loading/empty/error/success: Home widget은 기존 `HomeWidget` + `WidgetState` + generation ref 패턴을 그대로 재사용한다(`loadProjects`를 `loadNotices`로 교체). 게시판 화면은 기존 `LoadState` 구분을 따른다.
- 공통 Action Feedback: 작성·삭제는 저장 중 버튼 비활성·중복 submit 차단, action 근처 성공/실패 안내, field 오류 연결과 첫 오류 focus, `aria-live` 안내를 제공한다.
- 접근성: 목록은 의미 있는 heading·list 구조, 상세 본문은 개행 보존 plain text 렌더링(HTML 미해석·auto-link 없음), keyboard 접근과 focus 이동을 회귀 확인한다.
- 390px/mobile/narrow pane: PC 표 없이 한 열 card 목록을 사용하고 최신 공지와 `공지 작성` 행동을 같은 열에 배치한다. page-level horizontal overflow 0.
- `HomePage.tsx`에서 `listProjects` import·호출을 제거하되 `api.ts`의 `listProjects` 자체와 프로젝트 화면 소비자는 무변경.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 프로젝트 병목 집계·프로젝트 목록·내 업무·알림 요약은 무변경. Home 하단 slot의 소비 API만 교체된다.
- 권한/관리자: 신규 permission 없음. System Administrator 특례 없음(v1). 관리자 moderation은 후속 결정으로 분리.
- Excel/PDF/첨부: 해당 없음(v1 제외).
- Teams/Mail: 연결하지 않음. 기존 `ChannelNotice` 수동 발송 기능은 그대로 유지되며 게시판과 독립이다.
- 삭제·복구/감사: soft delete 채택 시 원문 보존. 복구 UI는 v1 제외.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 신규 `notice_posts` + 전용 `/api/notices` + Home widget 교체 + 전용 게시판 view. 작성·목록·상세 + 본인 soft delete | 외부 발송·unread 계약과 완전 분리, 기존 Home widget·CRUD 패턴 재사용으로 비용 낮음, 잘못 쓴 글의 즉시 정정 경로 존재, 이력 보존 | 신규 table·view 추가. moderation 부재 시 부적절 글은 작성자 자율 정리에 의존 |
| B | Home widget 안에서만 목록·작성·inline 상세(전용 게시판 view 없음) | 화면 수 최소 | 공지가 쌓이면 Home에서 과거 글 접근 불가, pagination·상세 UX가 widget에 몰려 Home이 비대해짐 |
| C | 기존 `ChannelNotice`/`notifications` 경로 재사용 | 신규 table 없음 | 관리자 전용 발송 계약·unread·Teams delivery와 섞여 interview가 금지한 외부 발송·unread 오염 위험. 기준선에서 이미 배제 |

권장안은 A다. interview 기준선(2절)이 C를 배제했고, B는 “프로젝트가 늘수록 대표성이 떨어진다”는 문제를 공지 수 증가에서 반복한다. 본인 soft delete 포함 여부는 16절 1번의 사용자 결정 항목으로 남긴다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. `0052`는 isolated fresh PostgreSQL과 기존 `0051 → 0052` upgrade에서만 검증한다.
- migration 필요 여부: 필요. additive `0052` 1건(신규 table). 기존 migration 무수정, rollback은 forward-fix 원칙.
- 외부 발송/실제 데이터 영향: 없음. provider 호출·실제 데이터 접근 없음, 테스트는 synthetic data만 사용.
- runtime 교체 여부: 없음. Development 5174/5081 범위의 실험 검증만 수행한다.
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo·`main` 반영·Persistent UAT 적용·moderation 정책 확정. fast-track은 local experiment commit까지만 승인한다.

## 14. 검증 계획

- 최소 테스트: Backend — 공지 작성·목록·상세(+삭제) 성공 경로, 승인 대기·비활성·미인증 차단, field validation(빈 값·길이 초과·공백만), 타인 글 삭제 403, 알림/내 업무/`notifications` row 미생성 확인, 최신순·pagination. Frontend — Home 공지 widget의 loading/empty/error/ready와 retry, 작성 form validation·중복 submit 차단, 게시판 목록·상세 렌더링(신규 `NoticeBoardPage` unit test와 `App.test.tsx`의 기존 `프로젝트 병목` 단언 교체).
- 영향 영역 회귀: Home 상단 KPI·중앙 내 업무/Pending/알림 widget 무변경 회귀(기존 `App.test.tsx`·Home 관련 test), 프로젝트 목록·상세의 병목 표시 회귀, Backend 전체 test suite, migration test의 최신 version 단언을 `0052`로 갱신(`PostgreSqlMigrationTests`).
- PR/CI: fast-track 범위에서는 local 검증(Backend 전체, Frontend lint/typecheck/unit/build, fresh·existing migration, isolated Full-Stack E2E와 desktop/390px screenshot)으로 대체하고 push·PR·CI는 별도 승인 전 수행하지 않는다.
- 사용자 검수: `BATCHED_FINAL` — 마지막 일괄 검수 목록에 desktop·390px의 공지 조회·작성·(삭제)·Home 상단/중앙 무변경 확인 항목을 추가하고 user validation checklist로 추적한다.

## 15. 완료 기준

- 기능/권한/데이터: 승인 완료 active 사용자의 조회·작성이 동작하고 비승인·비활성·미인증이 서버에서 차단되며, 작성자·부서·시각 snapshot이 보존되고 공지 작성이 알림·내 업무·외부 delivery를 만들지 않는다.
- UX: desktop·390px에서 목록·작성·상세가 읽기 쉽고 page-level horizontal overflow 0, 독립 loading/empty/error·retry, 접근 가능한 오류 안내.
- 자동 테스트: Backend 전체, Frontend lint/typecheck/unit/build, fresh `0001→0052`·기존 `0051→0052` migration, isolated Full-Stack E2E, desktop/mobile screenshot 통과. Open P0/P1/P2 = 0.
- 5종 산출물: Implementation report·SOP·User manual·Roadmap update·User validation checklist의 상태·위치 추적(`docs/12-task-completion-policy.md`).
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`로 기록하고 완료로 바꾸지 않는다.
- PR 상태: 없음(local experiment commit까지).
- 중단 조건: 문서·구현의 의미 있는 충돌 발견, Home 상단·중앙 계약을 깨지 않고는 구현 불가한 경우, 신규 권한·외부 연동이 필요해지는 범위 확장, fast-track 제외 경계(대표 repo·`main`·Persistent UAT·provider)를 넘어야 하는 경우 — 즉시 중단하고 보고한다.

## 16. 미결정 사항

모두 비차단이며, experiment standing instruction에 따라 Codex review 뒤 Fable 2차 기획에서 권장안을 자동 채택한다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | v1 동작 범위 | A) 작성·목록·상세만 B) A + 작성자 본인 soft delete(권장 — 오작성 정정 경로 확보, 수정·moderation은 후속) C) B + 관리자 moderation | 대기(권장안 자동 채택 예정) |
| 2 | 표시 정책 | A) Home 최신 5건 + 1줄 preview + 전용 게시판 view(권장 — 기존 widget 규모·병목 Top 5와 동일한 밀도) B) Home 3건 + inline 상세만 | 대기(〃) |
| 3 | 작성 권한 | A) default policy만으로 전 사용자 허용, 검수 전환 중에는 effective 사용자를 작성자로 기록(권장 — 기존 mutation 계약과 일관) B) 검수 전환 중 작성 차단 C) 신규 permission 신설 | 대기(〃) |
| 4 | 데이터 lifecycle | A) 작성자 FK + 이름·부서 snapshot + soft delete, 별도 audit table 없음(권장 — v1에 수정이 없어 행 보존으로 충분) B) A + `0042`식 append-only guard trigger C) live join만(snapshot 없음) | 대기(〃) |
| 5 | 입력 계약 | A) 제목 1~100자·본문 1~2000자, trim 후 빈 값 거부, 개행 보존 plain text, HTML 미해석·auto-link 없음(권장) B) 더 긴 본문(4000자)과 auto-link | 대기(〃) |
| 6 | 알림 경계 | A) 어떤 알림·내 업무·외부 발송도 생성하지 않음, 향후 opt-in은 별도 `NEW_FEATURE`(권장 — interview 성공 기준과 일치) B) 인앱 unread만 생성 | 대기(〃) |
| 7 | 조회 정책 | A) 작성 시각 desc(+id desc tie-break) offset pagination, 게시판 page 20건, Home limit 5(권장 — 기존 목록 계약과 일관) B) keyset pagination | 대기(〃) |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Notices/`(신규 Contracts·Store·EndpointExtensions), `Program.cs` endpoint 등록
- Frontend: `frontend/src/HomePage.tsx`(하단 widget 교체), `frontend/src/App.tsx`(View union·화면 연결), 신규 `NoticeBoardPage.tsx`·`notices.ts`, `frontend/src/api.ts`, `frontend/src/styles.css`
- DB/Migration: `database/migrations/0052_*.sql`(additive 신규 table)
- Tests/Scripts: 신규 Backend notice API tests, `PostgreSqlMigrationTests`의 최신 version 단언 갱신, `frontend/tests/App.test.tsx` 병목 단언 교체·신규 page test, isolated Full-Stack E2E spec
- Docs: Roadmap 상태 갱신, 실험 완료 원장, Task 종료 산출물

## 18. Roadmap 연결

- 선행 Task: TASK-HOME-001/002(Home widget-slot·개인화 shell, `EXPERIMENT_COMPLETE / BATCHED_FINAL`), active 사용자 identity 계약 — 충족됨.
- 후속 Task: 첨부·사진 storage(공지 첨부는 그 뒤에만 고려), 관리자 moderation·공지 opt-in 알림(별도 `NEW_FEATURE`), 운영 전환.
- 현재 Go/No-Go: Roadmap 1.4B `Experiment Fast-Track In Progress`·Decision Log 2026-07-21 삽입 승인과 일치 — Go(단, 구현은 Codex review와 2차 기획 이후).
- 별도 Task로 분리할 항목: 공지 검색·고정·읽음 통계, 첨부, moderation, 알림 opt-in.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-21 | 사용자가 Home 상단·중앙 유지, 하단 병목 widget만 누구나 입력 가능한 공지 게시판으로 교체를 직접 지시(experiment fast-track) | 본 기획 전체 범위·제외 경계로 반영 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

Codex 구현 지시문 초안(2차 기획 확정 후 사용): additive `0052`로 공지 table을 생성하고, `Notices` 영역에 default policy 기반 목록·상세·작성(+채택 시 본인 soft delete) endpoint와 Npgsql store를 기존 Pending 패턴으로 구현한다. `HomePage.tsx`의 하단 wide widget을 `listProjects` 대신 공지 API로 교체하되 상단·중앙 widget과 `listProjects` 소비자는 수정하지 않는다. 게시판 view·작성 form은 기존 Action Feedback·접근성 계약을 따르고, 공지 경로에서 `notifications`·`work_items`·delivery를 호출하지 않는다. 검증은 14절, 경계는 13절을 따른다.

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 7
