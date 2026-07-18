# TASK-NOTIFY-005 — 사용자별 알림 설정 2차 기획 (구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-NOTIFY-005`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/notify-005-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/notify-005-planning.md`
- codexReviewSource: `tasks/notify-005-review.md` (`RESOLVED_FOR_SECOND_PLANNING`)
- approvalChangeSource: `tasks/notify-005-change-001.md` (`fableSecondPlanningTarget: docs/26-notification-preferences-plan.md`)

이 문서는 experiment fast-track의 최종 구현 source of truth다. 1차 기획의 유지 권고를 보존하고 Codex review의 추가·제거·보류 resolution을 모두 반영했다. 이 문서는 experiment branch의 local 구현·검증·commit까지만 계약하며 대표 repo 반영, `main` merge, push·PR, Persistent UAT migration·runtime handover, 실제 provider 발송, 게시 승인을 부여하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/validation-matrix.md`, `docs/development/privacy-safe-evidence.md`를 따르고 이 문서에 복사하지 않는다.

## 1. 한 줄 목표

사용자가 인앱 원본과 필수 업무 알림을 그대로 유지한 채, 실제로 연결된 소음성 외부 알림(자동 단계 업무 생성 Teams 개인, 예정일 임박 D-1 Teams 개인, 일일 업무 요약 메일)의 수신 여부를 직접 확인·변경·기본값 복원할 수 있게 한다.

## 2. 해결할 업무 문제와 대상 사용자

- 모든 허용 외부 알림(Teams 개인·메일)이 사용자 구분 없이 동일하게 생성되어 소음이 생기고, 단순 opt-out을 추가하면 필수 알림 누락 위험이 생긴다.
- 사용자는 event별 외부 채널 수신 여부를 시스템 안에서 조정할 수 없어 Teams·메일 클라이언트에서 개별적으로 줄이거나 운영자에게 수동 요청하며, 감사 이력이 남지 않는다.
- 대상: Active 일반 사용자(본인 설정), System Administrator(지원용 조회와 명시적 대리 변경). 승인 대기·비활성 사용자는 대상이 아니다.

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| Active 일반 사용자 | 본인 설정 조회, 허용 항목 변경, 기본값 복원 | 본인 effective 설정과 공개 taxonomy | 본인 계정의 opt-out 허용 항목만 |
| System Administrator | 지원용 조회, 명시적 대리 변경, 대리 기본값 복원 | active 사용자의 effective 설정과 taxonomy | 대상 사용자의 opt-out 허용 항목만. 잠긴 항목은 관리자도 해제 불가 |

- Backend가 본인 범위·관리자 범위·잠금을 authoritative하게 강제한다. UI 비활성화는 보조 수단이다.
- 관리자 API는 기존 `QmsPolicies.AdminUsersRead`(`users.manage` 기반, 기존 admin notification mutation과 동일 경계)를 재사용한다. 신규 permission 문자열·권한 능력을 추가하지 않는다. (1차 §16 결정 5 — review 대조로 확정)
- System Administrator도 잠긴 필수 항목을 우회하지 못한다(Roadmap 3.5 권한 원칙).

## 3. 확정 v1 Taxonomy (review §5 채택)

v1 화면·API·gate는 실제 event coverage가 확인된 다음 조합으로 제한한다. 내부 key는 기존 `NotificationDeliveryTypes` + `NotificationDeliveryChannels` 상수 조합이고, 사용자-facing 이름·설명·잠금 metadata는 코드에 고정 정의한다(DB 저장 없음). 조회 응답에 taxonomy 버전 문자열을 포함한다.

| 사용자-facing event | 내부 delivery type | channel | 기본값 | 사용자 변경 | 근거 |
| --- | --- | --- | --- | --- | --- |
| 자동 단계 업무 생성 | `WorkItemCreated` | Teams 개인 | ON | 가능 | 업무는 인앱·내 업무에 남고 자동 외부 개입만 조절 |
| 예정일 임박 D-1 | `DueSoonL0` | Teams 개인 | ON | 가능 | 사전 예고 성격 |
| 일일 업무 요약 | `DailyDigest` | Mail | ON | 가능 | 요약·소음 조절 가치가 큼 |
| 긴급/차단 | `UrgentBlocking` | Mail | ON | 잠금 | 업무상 필수 |
| 예정일 초과 L1 | `OverdueL1` | Teams 개인·Mail | ON | 잠금 | 즉시 에스컬레이션 |
| 예정일 초과 L2 | `OverdueL2` | Teams 개인 | ON | 잠금 | 상위 수신자 개입 |
| 예정일 초과 L3 | `OverdueL3` | Mail | ON | 잠금 | 생산관리·영업 필수 에스컬레이션 |

표시·범위 규칙:

- `WorkItemCreated`의 사용자-facing 이름은 "자동 단계 업무 생성"으로 고정하고, 화면에 "관리자가 직접 보낸 알림·업무 배정은 이 설정과 무관하게 발송됩니다"를 명시한다. (`MANUAL_WORK_ASSIGNMENT_SCOPE_AMBIGUITY` resolution)
- `ProjectCompletion`은 event 연결이 미확인이므로 v1 taxonomy·UI·API에서 제외한다. 잠금 항목으로도 노출하지 않는다. 실제 event coverage Task 이후 별도 추가한다. (`PROJECT_COMPLETION_DEAD_CONTROL` resolution)
- 인앱과 `UrgentBlocking`+TeamsChannel(통합 채널 공지)은 toggle을 만들지 않고 화면 상단에 "인앱 알림은 항상 저장되고 통합 채널 공지는 조직 공지로 유지됩니다"를 설명한다.
- `ReferenceDigest`는 현재 개별 delivery가 아니라 DailyDigest content이므로 별도 toggle을 만들지 않는다.
- `ManualTest`와 관리자 수동 발송(`Personal`/`ChannelNotice`/`WorkAssignment`), 테스트 발송은 preference 대상에서 제외한다.
- 잠금 항목의 Teams 개인 채널 표기는 운영 설정(`TeamsPersonalChannelStrategy`)에 따른 DM/Activity 차이를 사용자에게 노출하지 않고 "Teams 개인 알림"으로 통일한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 본인 설정 변경과 재확인

1. 사용자가 알림 페이지 링크 또는 모바일 "앱 상태와 계정" sheet에서 "알림 설정"으로 진입한다.
2. 시스템이 event group card로 각 항목의 상태, 잠금 사유, "모두 기본값"/"내가 변경함" 구분을 보여준다.
3. 사용자가 "일일 업무 요약"과 "예정일 임박 D-1"을 끄고 저장한다.
4. 서버가 검증·저장 후 최신 effective 설정과 새 version을 반환하고, 저장 버튼 인접에 성공 안내가 표시된다.
5. 이후 신규 DailyDigest 메일과 L0 Teams delivery는 `Suppressed`(`SuppressedByUserPreference`)로 원장에 남고 발송되지 않는다. 재로그인 후에도 설정이 유지된다.

### 시나리오 B — 잠금 항목과 기본값 복원

1. 긴급/차단 메일과 L1~L3 항목은 잠금 control로 표시되고 "업무상 필수 알림은 해제할 수 없습니다" 사유가 텍스트로 제공된다.
2. 사용자가 "기본값 복원"을 실행하면(expectedVersion 포함) 서버가 override를 제거하고 audit를 남긴 뒤 기본 상태와 새 version을 반환한다.
3. 이미 기본값 상태에서 복원을 실행하면 version·audit 변화 없이 `changed=false`로 응답하고 "이미 기본값입니다" 안내를 표시한다.

### 시나리오 C — 관리자 지원 대리 변경

1. 관리자가 관리자 사용자 관리 화면의 대상 사용자 row에서 "알림 설정" action으로 지원 view를 연다.
2. 잠금 항목과 사용자 선택 항목, 기본값 여부가 구분 표시된다. v1은 변경 이력 목록·요약을 노출하지 않는다.
3. 관리자가 opt-out 허용 항목만 변경해 저장하면 actor(관리자)·대상 사용자·전후 값·version이 audit에 기록된다.
4. 비활성·삭제 대상 사용자는 조회·변경이 안정 오류로 차단된다.

### 시나리오 D — 동시 저장·복원 충돌

1. 같은 사용자의 두 세션(또는 본인과 관리자)이 설정을 편집한다.
2. 먼저 저장한 쪽이 성공하고 version이 증가한다.
3. 나중 저장·복원은 stale expectedVersion으로 409 차단된다. 서버 값을 자동으로 덮어쓰지 않고 "다른 곳에서 설정이 변경되었습니다. 다시 불러온 뒤 저장해 주세요" 안내와 "다시 불러오기" 행동을 제공하며 로컬 편집값은 유지한다.

## 5. 업무 규칙과 불변조건

- 인앱 알림 원본과 `notification_recipients` 생성은 preference와 무관하게 전량 보존한다.
- 잠금 조합(`UrgentBlocking`+Mail, `OverdueL1/L2/L3` 전 채널)과 taxonomy에 없는 조합이 payload에 포함되면 전체 저장을 원자적으로 거부한다. 일부만 적용하지 않는다.
- preference gate는 새 delivery 생성 시점의 다음 세 경로에만 적용한다: 자동 `WorkItemCreated` Teams DM insert, `DailyDigest` Mail insert, escalation `L0` Teams 개인 insert. urgent 두 경로(TeamsChannel·Mail), L1~L3, 관리자 수동·테스트 발송, 이미 생성된 delivery의 상태·재시도·attempt lineage(TASK-NOTIFY-004 계약)는 변경하지 않는다.
- opt-out delivery는 row를 생략하지 않고 `Suppressed` + 안정 코드 `SuppressedByUserPreference`, `suppressed_at_utc` 설정, `attempt_count=0`, `next_attempt_at_utc=null`, provider-call-start audit 0으로 삽입해 원장·dedupe·운영 추적을 보존한다.
- override가 없는 사용자와 기본값 복원 사용자는 현재 channel matrix와 완전히 동일하게 동작한다(기존 사용자 호환).
- 저장·복원과 audit insert는 한 transaction이다. 이 action에서 외부 provider를 호출하지 않는다. delivery 생성 gate는 별도 worker transaction에서 최신 committed preference를 읽는다.
- 저장·복원 모두 expectedVersion이 필수이며 mismatch는 409다. effective override 집합이 요청 전후 동일한 no-op은 version·audit를 변경하지 않고 `changed=false`를 반환한다. (`PREFERENCE_NOOP_AUDIT_NOISE` resolution)
- 본인 API는 인증된 현재 사용자 id만 사용하고 request body의 user id를 받지 않는다.
- audit는 append-only이며 수정·삭제하지 않는다. v1 UI는 audit를 조회하지 않는다(`ADMIN_AUDIT_UI_SCOPE_DRIFT`는 후속 Task).
- ReviewSafe mode에서는 기존 HTTP mutation guard가 최종 차단하고 UI는 mutation control을 비활성화한다.
- 사용자 삭제 lifecycle은 기존 `qms_users` FK·purge 관례를 따르고 새 정책을 만들지 않는다.

## 6. 데이터 모델과 Migration `0041`

review resolution에 따라 책임이 다른 3개 신규 테이블을 additive migration `0041`(다음 번호, 기존 `0040`까지 무수정)로 추가한다. 기존 테이블·데이터·번호는 변경하지 않는다. 컬럼명은 구현 시 Repository 관례에 맞춰 확정하되 아래 구조 계약을 유지한다.

| 테이블(동등 명칭) | 책임 | 핵심 구조 |
| --- | --- | --- |
| user notification preference profile | 사용자별 version과 저장 직렬화 | user_id unique(FK `qms_users`), version 단조 증가, 시각. 첫 저장 race는 `insert ... on conflict do nothing` 후 `select ... for update`로 직렬화 |
| user notification preferences (sparse override) | opt-out 명시값만 저장 | user_id + delivery_type + channel unique, is_enabled, 시각. 허용 조합만 존재 가능(check 또는 서버 검증) |
| user notification preference audit | append-only 변경 이력 | fixed-field row: actor_user_id, target_user_id, action(`Save`/`Reset`/`AdminSave`/`AdminReset`), delivery_type, channel, old_value, new_value, resulting_version, 시각. JSON 자유 형식 금지 |

상태 전이:

```text
기본값(override 0행) → 저장(profile lock → expectedVersion 비교 → 차이 있으면 override 교체 + 항목별 audit + version+1)
override 상태 → 기본값 복원(동일 lock·비교 → override 삭제 + audit + version+1) → 기본값
no-op 저장/복원 → version·audit 불변, changed=false
```

rollback은 additive forward-fix 원칙을 따른다. 이 migration의 Persistent UAT 적용은 이 계약에 포함되지 않는 별도 승인 대상이다.

## 7. API 계약

모든 오류는 안정 코드와 사용자 행동이 가능한 한글 메시지를 반환하고 raw SQL·내부 식별자·stack trace를 노출하지 않는다.

| Endpoint | 권한 | 동작 |
| --- | --- | --- |
| `GET /api/my/notification-preferences` | `RequireAuthorization()` (기존 `/api/my/...` 관례) | taxonomy(버전 포함) + 항목별 effective 값·잠금·override 여부 + version |
| `PUT /api/my/notification-preferences` | 동일 | 변경 가능 조합의 전체 교체 + expectedVersion. 성공 시 최신 effective·version·changed 반환 |
| `POST /api/my/notification-preferences/reset` | 동일 | expectedVersion 필수 기본값 복원. no-op이면 changed=false |
| `GET /api/admin/users/{userId}/notification-preferences` | `QmsPolicies.AdminUsersRead` | active 사용자 한정 조회. 대상 없음(404)과 비활성(안정 코드 409/400 계열)을 구분 |
| `PUT /api/admin/users/{userId}/notification-preferences` | 동일 | 대리 변경. actor·대상 분리 audit |
| `POST /api/admin/users/{userId}/notification-preferences/reset` | 동일 | 대리 기본값 복원. expectedVersion 필수 |

오류 계약: 잠금·비지원 조합 400(항목 단위 코드 포함, 전체 거부), 권한 위반 403, 대상 없음 404, stale version 409. 409 응답은 최신 값을 자동 적용하지 않는다.

## 8. 화면·UX 계약

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 피드백 |
| --- | --- | --- | --- | --- |
| 내 알림 설정 (신규 view, `App.tsx` route switch) | 알림 페이지 링크 + 모바일 "앱 상태와 계정" sheet action (2개 진입점) | 상단 고정 설명(인앱 항상 저장·통합 채널 공지 유지·수동 발송 제외), v1 taxonomy card, 잠금 사유 텍스트, "모두 기본값"/"내가 변경함" 구분 | 허용 항목 toggle, 저장, 다시 불러오기, 기본값 복원 | `useActionFeedback` scoped feedback(저장·복원 별도 scope)을 action 인접 표시, `aria-live`, 중복 submit 차단, 409 시 다시 불러오기 안내 |
| 관리자 알림 설정 지원 (신규 view) | 관리자 사용자 관리의 대상 사용자 row "알림 설정" action | 대상 사용자의 effective 설정, 잠금/사용자 선택/기본값 구분. audit 목록·요약 없음 | 대리 변경 저장, 대리 기본값 복원 | 동일 action 인접 feedback과 권한·대상 상태 오류 안내 |

- loading/error/success와 stale(409)·권한(403)·조회 실패를 구분한다. empty 대신 "모두 기본값" 상태를 표시한다.
- 390px·Teams narrow: PC 표 축소판이 아닌 event group card 세로 배치, page-level horizontal overflow 0.
- 접근성: toggle label/role, 잠금 사유 텍스트 연결, 첫 오류 focus, screen reader 안내.
- raw enum 값을 화면에 노출하지 않고 사용자-facing 이름만 사용한다.

## 9. 구현 범위

### 포함

- migration `0041` 3-table additive 구조와 catalog·fresh·existing 검증
- taxonomy 코드 정의, preference store, 본인 3종·관리자 3종 endpoint, DI·endpoint 등록
- 자동 `WorkItemCreated` Teams DM·`DailyDigest` Mail·escalation `L0` Teams 개인 insert의 preference gate(Suppressed 기록)
- append-only fixed-field audit
- 내 알림 설정 view(2개 진입점)와 관리자 지원 view, `useActionFeedback` 재사용
- §11 검증과 desktop·390px screenshot, 5종 산출물, local commit

### 명시적 제외 (불변)

- 인앱 notification 원본 opt-out·읽음 계약 변경, 법적·업무상 필수 알림 해제
- `UrgentBlocking`(TeamsChannel·Mail)·`OverdueL1/L2/L3`·관리자 수동/테스트 발송의 동작 변경
- `ProjectCompletion` 설정 노출(후속 event coverage Task로 이관)
- 관리자 화면의 audit 조회 UI(후속 Task), 조직/부서 default 정책 편집, taxonomy 관리자 편집, quiet hours, 설정 import/export
- 신규 외부 채널, provider 신뢰성·terminal Failed 재처리 재구현, 이미 생성된 delivery 취소·삭제·재작성
- 신규 permission 문자열, 운영 URL·manifest·secret 변경
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 발송·게시

## 10. Migration·UAT·runtime·provider 경계

- Persistent UAT: 이번 계약은 isolated PostgreSQL만 사용한다. Persistent UAT migration 적용·runtime handover는 별도 사용자 승인 대상이다.
- 외부 발송: 실제 Teams/Mail/Graph 호출을 검증에 사용하지 않는다. fake/dry-run provider만 사용한다.
- runtime 교체: 없음.
- Git: experiment branch local commit까지만. `main` merge는 분리된 승인 3회 전까지 금지 상태를 유지한다.

## 11. 검증 계획

- Backend: Release build, preference store/API filtered tests, 전체 regression(공통 contract 영향 시).
  - Authorization matrix: 본인/타인/관리자/비인증, active/비활성 대상, 잠금·비지원 조합 400과 서버 403.
  - Concurrency: 겹친 저장·복원 stale 409, 첫 저장 profile race 직렬화, no-op 시 version·audit 불변, audit 단일 기록.
  - Delivery 생성 matrix(fake/dry-run): opt-out 전후 — 자동 WorkItem DM·DailyDigest·L0은 `Suppressed`(`SuppressedByUserPreference`, attempt 0, provider call 0), urgent·L1~L3·수동 발송·TeamsChannel·인앱은 불변, override 없는 사용자는 기존과 동일.
- Migration: catalog 전체 suite + fresh DB·existing DB apply.
- Frontend: lint·typecheck·unit·build, desktop·390px에서 loading/error/success·잠금·overflow 0.
- isolated Full-Stack E2E: 저장 → 재조회(재로그인 동등) → 기본값 복원 → Suppressed 생성 확인.
- 사용자 검수: 두 화면의 desktop·390px 페이지별 screenshot으로 "사용자 검수 대기" 전환. 자동 검증 완료와 사용자 검수 완료를 분리 관리하고 증빙은 privacy-safe projection만 사용한다.

## 12. 권장 개발 순서 (review §8 채택)

1. `0041` profile·override·audit migration과 catalog/fresh/existing 검증
2. taxonomy contracts·store와 본인/관리자 get-save-reset, version/no-op/audit/authorization tests
3. 자동 WorkItem·DailyDigest·L0 생성 gate와 locked matrix 회귀 tests
4. Frontend API types와 개인 설정 route/card UX·action feedback
5. 관리자 사용자 row 진입과 지원 view
6. desktop·390px·isolated Full-Stack E2E, screenshot, 5종 산출물과 local commit

## 13. 완료 기준과 중단 조건

완료 기준:

- 저장·재로그인 유지·기본값 복원·no-op 불변·stale 409가 서버 강제로 동작한다.
- 잠금·범위·수동 발송 제외 불변조건이 테스트로 증명되고 기존 사용자 기본 delivery가 무변화다.
- §11 자동 검증 전부 통과(미실행 항목은 이유와 함께 기록), P0/P1/P2 open 0.
- 5종 산출물 상태·위치 추적, 사용자 검수 대기 전환, local commit 완료.

중단 조건:

- 기존 delivery·escalation 계약과의 충돌, migration catalog 위반, 신규 권한 필요성 발견, 문서·구현의 의미 있는 충돌이 나타나면 구현을 중단하고 위치·영향·선택지를 보고한다. 이 경우 fast-track으로 우회하지 않는다.

## 14. 결정 이력 정리

1차 기획의 사용자 결정 5건은 experiment standing instruction과 review resolution에 따라 다음으로 확정한다: (1) 조직/부서 default 정책 편집 — 후속 Task, (2) taxonomy 관리자 편집 — 후속 Task, (3) import/export·quiet hours — 후속 Task, (4) `ProjectCompletion` — 잠금 노출이 아니라 v1 제외로 확정, (5) 관리자 권한 — `AdminUsersRead` 재사용 확정. Review Finding 중 P2 3건은 이 계약의 §3·§5·§8에 반영되어 해소됐고, P3 `ADMIN_AUDIT_UI_SCOPE_DRIFT`는 후속 Task로 이관한다. 안전·정책상 미해결 blocking decision은 없다.

openBlockingDecisionCount: 0
