# TASK-NOTIFY-005 — 사용자별 알림 설정 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/notify-005-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`
- fastTrackSource: `USER_EXPLICIT_EXPERIMENT_RULE`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 모든 허용 외부 알림(Teams 개인·메일)이 사용자 구분 없이 동일하게 생성되어 소음이 생기고, 반대로 단순 opt-out을 추가하면 필수 알림 누락 위험이 생긴다. 사용자는 event별 외부 채널 수신 여부를 시스템 안에서 조정할 수 없고 감사 이력도 남지 않는다.
- 대상 사용자·역할: Active 일반 사용자(본인 설정 조회·변경·기본값 복원), System Administrator(지원용 조회와 명시적 대리 변경). 승인 대기·비활성 사용자는 대상이 아니다.
- 정상 흐름: 설정 진입 → event group별 채널 상태 확인 → 변경 가능한 외부 채널 선택 → 저장 → 서버 재조회 결과와 action 인접 성공 안내 확인.
- 예외·복구 흐름: 필수·비지원 조합, stale version, 권한 위반은 서버가 차단하고 해당 설정 근처에 한글 오류와 복구 행동을 표시한다. 저장 전 취소·서버 기준 다시 불러오기·기본값 복원을 제공하고, 실패 시 로컬 편집값을 유지한다.
- 확정한 정책과 명시적 제외: 인앱 원본 opt-out 금지, 필수 알림 해제 금지, Teams 통합 채널 공지 개인 opt-out 금지, 기존 사용자 기본 delivery 호환 유지, Backend authoritative. 신규 외부 채널, quiet hours, 이미 생성된 delivery 변경, provider 신뢰성 재구현, Excel export/import, 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 제외한다.
- planning으로 넘긴 비차단 미결정 사항: 조직 단위 default 정책 편집, taxonomy 관리자 편집, 설정 import/export, quiet hours. Interview 표 7의 5개 정책 선택은 experiment standing instruction에 따라 Fable 권장안을 자동 채택한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

사용자가 인앱 원본과 필수 업무 알림을 그대로 유지한 채, 허용된 event group의 Teams 개인·메일 외부 알림 수신 여부를 직접 확인·변경·기본값 복원할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 인앱 알림·내 업무는 완비되어 있고, 외부 delivery는 `notification_deliveries` 원장 기반으로 dispatcher(긴급 Teams 통합 채널·긴급 메일·업무 생성 Teams DM·일일 요약 메일)와 escalation worker(L0~L3)가 코드·운영 설정이 정한 channel matrix로 일괄 생성한다.
- 사용자별 조정 수단이 시스템에 없어 Teams·메일 클라이언트에서 개별적으로 알림을 줄이거나 운영자에게 수동 요청하며, 이 우회는 event 단위 선택이 불가능하고 이력이 남지 않는다.
- 소음이 누적되면 긴급/차단 같은 중요 알림의 가시성이 떨어지고, 운영자 수동 대응은 정책 drift와 추적 누락을 만든다.
- 이번 Task는 Roadmap 5.4 `TASK-NOTIFY-005`의 canonical 목적과 일치하며, 선행인 TASK-NOTIFY-004(claim/lease·retry·attempt lineage)는 완료 상태다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| Active 일반 사용자 | 본인 알림 설정 조회, 허용 항목 변경, 기본값 복원 | 본인 effective 설정과 공개 taxonomy | 본인 계정의 opt-out 허용 항목만 |
| System Administrator | 지원용 설정 조회, 명시적 대리 변경, 대리 기본값 복원 | active 사용자의 effective 설정과 taxonomy | 대상 사용자의 opt-out 허용 항목만 (잠긴 항목은 관리자도 해제 불가) |

- Backend가 본인 범위, 관리자 범위, 필수 항목 잠금을 authoritative하게 강제한다. UI 비활성화는 보조 수단이다.
- 관리자 조회·대리 변경은 기존 admin notification 경로가 사용하는 `QmsPolicies.AdminUsersRead`(`users.manage` 기반) 재사용을 권장한다. 신규 권한 능력을 만들지 않는다(§16 결정 5).
- System Administrator도 잠긴 필수 항목을 우회하지 못한다(Roadmap 3.5 권한 원칙).

## 4. 핵심 사용자 시나리오

### 시나리오 A — 본인 설정 변경과 재확인

1. 사용자가 알림 페이지 또는 모바일 "앱 상태와 계정" sheet에서 "알림 설정"으로 진입한다.
2. 시스템이 event group별 카드로 인앱(항상 켜짐·잠금), Teams 개인, 메일의 현재 상태와 잠금 사유를 보여준다.
3. 사용자가 "일일 요약 메일"과 "예정일 임박(D-1) Teams 알림"을 끄고 저장한다.
4. 서버가 검증·저장 후 최신 effective 설정과 version을 반환하고, 저장 버튼 근처에 성공 안내가 표시된다.
5. 이후 dispatcher는 해당 사용자의 새 DailyDigest 메일과 L0 Teams delivery를 사용자 설정 사유의 Suppressed로 기록하고 발송하지 않는다. 재로그인 후에도 설정이 유지된다.

### 시나리오 B — 필수 항목 잠금과 기본값 복원

1. 사용자가 긴급/차단 메일을 끄려 하지만 해당 control은 잠금 상태이고 "업무상 필수 알림은 해제할 수 없습니다" 사유가 텍스트로 표시된다.
2. 사용자가 "기본값 복원"을 실행하면 서버가 본인 override를 제거하고 audit를 남긴 뒤 기본 상태를 반환한다.
3. 화면은 서버 재조회 결과로 갱신되고 복원 성공 안내가 action 근처에 표시된다.

### 시나리오 C — 관리자 지원 대리 변경

1. 관리자가 사용자 지원 요청을 받고 관리자 화면에서 대상 active 사용자의 알림 설정을 조회한다.
2. 정책상 잠긴 항목과 사용자 선택 항목이 구분 표시된다.
3. 관리자가 요청받은 opt-out 허용 항목만 변경해 저장하면, actor(관리자)·대상 사용자·전후 값이 audit에 기록된다.

### 시나리오 D — 동시 저장 충돌

1. 같은 사용자의 두 세션이 설정을 편집한다.
2. 먼저 저장한 세션이 성공하고 version이 증가한다.
3. 나중 세션의 저장은 stale version으로 409 차단되고, "다른 곳에서 설정이 변경되었습니다. 다시 불러온 뒤 저장해 주세요" 안내와 다시 불러오기 행동이 제공된다. 로컬 편집값은 유지된다.

## 5. 기능 요구사항

### 필수

- [ ] 코드 고정 event/channel taxonomy: 기존 `NotificationDeliveryTypes` + `NotificationDeliveryChannels` 조합을 안정적 내부 key로 쓰고, 사용자-facing group 이름·설명·잠금 정책 metadata를 분리 정의한다.
- [ ] opt-out 허용 조합(권장): `WorkItemCreated`+Teams 개인, `DueSoonL0`+Teams 개인, `DailyDigest`+Mail. 잠금 조합: `UrgentBlocking`+Mail, `OverdueL1/L2/L3`의 모든 채널, `ProjectCompletion`+Mail(§16 결정 4). 범위 제외: 인앱 원본, `UrgentBlocking`+TeamsChannel(통합 채널), `ManualTest`·관리자 수동 발송.
- [ ] sparse override 저장: 명시 override가 없는 사용자는 현재 channel matrix 그대로 동작한다(기존 사용자 호환).
- [ ] 본인 preference 조회 API(taxonomy + effective 설정 + 잠금 flag + version), 저장 API(version 필수, transaction 내 override 교체 + audit), 기본값 복원 API.
- [ ] 관리자 지원 조회·대리 변경 API(동일 잠금 규칙, actor·대상 audit).
- [ ] dispatcher·escalation의 외부 delivery 생성 지점에 effective preference gate 적용: opt-out된 조합은 `Suppressed` + 안정 코드 `SuppressedByUserPreference`로 기록한다(원장·dedupe 보존).
- [ ] 변경 전후·actor·대상·시각의 append-only audit(`authorization_audit_events`·`admin_master_change_logs`와 같은 기존 audit 테이블 관례를 따르는 신규 테이블).
- [ ] additive migration `0041`(신규 테이블만, 기존 테이블·데이터 변경 없음).
- [ ] 사용자 설정 화면(데스크톱·390px event group card)과 최소 관리자 지원 화면.

### 선택

- [ ] 알림 페이지 상단에서 설정 화면으로의 바로가기 링크.
- [ ] 관리자 조회 화면에서 잠금/사용자 선택/기본값 상태의 요약 뱃지.

### 명시적 제외

- [ ] 인앱 notification 원본 opt-out·읽음 계약 변경
- [ ] 법적·업무상 필수 알림 해제, Teams 통합 채널 공지 개인 opt-out
- [ ] 신규 외부 채널, quiet hours, 조직 단위 default 정책 편집, taxonomy 관리자 편집
- [ ] 이미 생성된 delivery의 취소·삭제·재작성, terminal Failed 재처리
- [ ] provider 신뢰성 재구현, 운영 URL·manifest·secret 변경
- [ ] 설정 Excel export/import
- [ ] 대표 repo·`main`·Persistent UAT·실제 provider 발송·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 내 알림 설정 (신규 view, `App.tsx` route switch에 추가) | 모바일 "앱 상태와 계정" sheet의 계정 영역 + 알림 페이지 링크 | event group card별 인앱(항상 켜짐)·Teams 개인·메일 상태, 잠금 항목의 disabled 사유 텍스트, 기본값 여부 | 허용 채널 toggle, 저장, 취소(다시 불러오기), 기본값 복원 | `useActionFeedback` scoped feedback을 저장·복원 버튼 인접에 표시, `aria-live`, 409 stale 시 다시 불러오기 안내 |
| 관리자 알림 설정 지원 (기존 admin-users 흐름에서 진입하는 최소 화면) | 관리자 사용자 관리에서 대상 사용자 선택 | 대상 사용자의 effective 설정, 잠금/사용자 선택 구분, 최근 변경 audit 요약 | 대리 변경 저장, 대리 기본값 복원 | 동일한 action 인접 feedback과 권한 오류 안내 |

확인할 UX 항목:

- 인앱이 항상 원본으로 남는다는 설명이 화면 상단에 있는가?
- 잠긴 항목은 왜 잠겼는지(긴급/에스컬레이션 필수 등) 텍스트로 이해되는가?
- 저장·복원 결과가 action 근처에 보이고 중복 submit이 차단되는가?
- stale·권한·조회 실패가 각각 구분된 한글 안내와 복구 행동을 갖는가?
- 390px·Teams narrow에서 PC 표 축소판이 아닌 card 구조로 page-level overflow 0을 유지하는가?

## 7. 업무 규칙과 불변조건

- 인앱 알림 원본과 `notification_recipients` 생성은 preference와 무관하게 전량 보존한다.
- 잠금 조합은 사용자·관리자 누구도 해제할 수 없고 서버가 400 계열의 안정 코드로 차단한다.
- opt-out은 새 delivery "생성 시점"에만 적용한다. 이미 생성된 delivery의 상태·재시도·attempt lineage 계약(TASK-NOTIFY-004)은 변경하지 않는다.
- 관리자 수동 발송(`Personal`/`ChannelNotice`/`WorkAssignment`)과 테스트 발송은 명시적 관리자 행동이므로 preference gate를 적용하지 않는다.
- override가 없는 사용자와 기본값 복원 사용자는 현재 channel matrix와 동일하게 동작한다.
- preference 저장과 audit insert는 한 transaction이다. 외부 provider 호출은 이 action에서 발생하지 않는다.
- 같은 사용자의 겹친 저장은 version token으로 stale을 차단해 lost update와 중복 audit를 방지한다.
- Suppressed 기록은 발송 안 함의 원인(사용자 설정)을 관리자 추적 화면에서 구분할 수 있어야 한다.
- 사용자 삭제 lifecycle은 기존 `qms_users` 참조 관례(FK + 기존 purge 정책)를 따르고 새 정책을 만들지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| event/channel taxonomy | delivery_type+channel 내부 key와 사용자-facing group·잠금 metadata. 코드 고정, DB 저장 안 함 | 신규(코드) | 버전 문자열을 조회 응답에 포함 |
| user_notification_preferences (동등 명칭) | 사용자별 sparse override: user_id, delivery_type, channel, is_enabled, 시각. (user_id, delivery_type, channel) unique | 신규 테이블 | 최신 상태만 유지, 이력은 audit로 |
| preference version 상태 | 사용자별 저장 직렬화와 stale 차단용 version(사용자당 1 row, 첫 저장 시 생성, `for update`) | 신규 테이블 또는 동일 테이블 내 동등 장치 | version 단조 증가 |
| preference audit | append-only: actor, 대상 사용자, 변경 전후 조합, action(SAVE/RESET/ADMIN_SAVE/ADMIN_RESET), 시각 | 신규 테이블 | 수정·삭제 금지 |
| notification_deliveries | 기존 원장. opt-out 시 `Suppressed` + `SuppressedByUserPreference` | 기존 (schema 변경 없음) | 기존 계약 유지 |

```text
기본값(override 없음) → 사용자 override 저장(version+1, audit) → effective 계산(delivery 생성 gate)
override 상태 → 기본값 복원(override 삭제, version+1, audit) → 기본값
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 잠금 조합 차단, 본인/관리자 범위, version stale 차단, taxonomy에 없는 key 거부.
- 필요한 조회와 mutation:
  - `GET /api/my/notification-preferences` — taxonomy·effective 설정·잠금·version (`RequireAuthorization()`, 기존 `/api/my/teams-activity/...` 관례)
  - `PUT /api/my/notification-preferences` — 변경 가능 조합 전체 교체 + expectedVersion
  - `POST /api/my/notification-preferences/reset` — 기본값 복원
  - `GET/PUT/POST(reset) /api/admin/users/{userId}/notification-preferences` — `QmsPolicies.AdminUsersRead`, active 사용자 한정
- 권한·validation: 잠금 항목 포함 요청은 항목 단위 안정 코드로 400, 비활성 대상은 404/409 계열, 권한 위반 403. 한글 메시지에 복구 행동 포함.
- transaction·동시성·idempotency: 저장은 version row `for update` → 비교 → override 교체 → audit → version+1을 한 transaction으로 처리. 같은 payload 재전송은 새 version으로 저장되지만 delivery gate는 최신 상태만 읽으므로 부작용이 없다.
- audit trail: 기존 audit 테이블 관례(예: `admin_master_change_logs`)와 같은 append-only 구조. 응답에는 raw audit body 대신 요약 projection만 노출한다.
- 외부 provider 영향: 없음. delivery 생성 SQL(`NotificationDeliveryStore.CreateImmediateDeliveriesAsync`의 3개 insert, `CreateDailyDigestDeliveriesIfDueAsync`, `WorkItemEscalationStore.InsertDeliveriesForRecipientAsync`의 L0 경로)에 preference left join gate만 추가한다. handler·worker·claim/lease 계약은 변경하지 않는다.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다(위 명칭은 관례 기반 제안이다).

## 10. Frontend 고려사항

- route/component: `App.tsx`의 view switch에 `notification-settings`(가칭)와 관리자 지원 view를 추가. 기존 admin 페이지·MobileSheet 패턴 재사용.
- loading/empty/error/success: taxonomy 로딩, 저장 중, 저장 성공, stale(409)·권한(403)·조회 실패를 구분한다. empty는 발생하지 않는 대신 "모두 기본값" 상태 표시를 제공한다.
- 공통 Action Feedback: 기존 `useActionFeedback`/`actionErrorMessage`(`frontend/src/useActionFeedback.ts`)의 scoped feedback을 저장·복원 각각의 scope로 사용한다.
- 접근성: toggle의 label/role, 잠금 사유 텍스트 연결, 첫 오류 focus, `aria-live` 안내.
- 390px/mobile/narrow pane: event group card 세로 배치, page-level horizontal overflow 0, MobileSheet 진입 경로 유지.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 인앱 알림·내 업무 생성 로직은 변경 없음. 알림 페이지에서 설정 진입 링크만 추가.
- 권한/관리자: 기존 `QmsPolicies.AdminUsersRead`와 admin 사용자 관리 흐름 재사용. 신규 permission 문자열을 추가하지 않는다.
- Excel/PDF/첨부: N/A.
- Teams/Mail: delivery 생성 gate만 추가. `TeamsPersonalChannelStrategy`, dry-run, provider 설정 계약은 그대로 유지한다.
- 삭제·복구/감사: 사용자 FK는 기존 lifecycle을 따르고, preference audit는 append-only로 기존 감사 원칙(3.3 이력 보존)을 따른다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | sparse override 테이블 + 서버 effective projection + 생성 시점 Suppressed 기록 | 기존 사용자 무변경 호환, taxonomy 확장 안전, 원장·dedupe·관리자 추적 보존, migration 최소 | delivery 생성 SQL 3~4곳에 gate 추가 필요, Suppressed row 증가 |
| B | 전체 사용자 row seed(모든 조합 사전 생성) | 조회 단순 | seed migration 필요, taxonomy 변경 시 대량 drift·보정 비용, 기존 사용자 호환 위험 |
| C | opt-out 시 delivery row 자체 미생성 | 저장 공간 절약 | 발송 안 된 이유를 관리자·감사가 추적 불가, dedupe·에스컬레이션 판정 근거 약화 |

권장안은 A다. Interview 표 7의 권장안(고정 taxonomy, sparse override, version token, 최소 admin, 필수 잠금)과 일치하며 experiment standing instruction에 따라 planning 입력으로 자동 채택되었다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 fake/dry-run provider만 사용한다. Persistent UAT migration 적용·runtime handover는 별도 승인 대상이다.
- migration 필요 여부: 필요. `0041` additive(신규 테이블 2개 내외), 기존 테이블·번호 수정 없음. rollback은 additive forward-fix 원칙.
- 외부 발송/실제 데이터 영향: 없음. 실제 Teams/Mail/Graph 호출을 검증에 사용하지 않는다.
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: push·PR·merge, Persistent UAT 반영, 실제 provider 발송, `main` merge(분리 승인 3회 규칙).

## 14. 검증 계획

- 최소 테스트: Backend Release build + preference store/API filtered tests, Frontend lint·typecheck·unit·build.
- 영향 영역 회귀 (Validation Matrix 기준):
  - Authorization matrix: 일반 사용자 본인/타인, 관리자, 비활성 대상의 allow·deny와 잠금 항목 403/400.
  - Concurrency: 겹친 저장 stale 409, version 증가, audit 단일 기록.
  - Migration: catalog 전체 suite + fresh/existing DB apply.
  - Delivery 생성 matrix: opt-out 전후의 UrgentBlocking(불변)·WorkItemCreated·DailyDigest·L0(Suppressed)·L1~L3(불변)을 fake/dry-run으로 검증.
  - 관련 isolated Full-Stack E2E: 저장 → 재조회 → 기본값 복원 흐름.
- PR/CI: 이번 experiment 범위에서는 local commit까지만. push·PR·CI는 승인 경계 밖.
- 사용자 검수: 사용자 설정·관리자 지원 화면의 desktop·390px 페이지별 screenshot으로 검수 대기 전환. 자동 검증 완료와 사용자 검수 완료를 분리 관리한다.

## 15. 완료 기준

- 기능/권한/데이터: 저장·재로그인 유지·기본값 복원 동작, 잠금 강제, 기존 사용자 기본 delivery 무변화, preference·audit 원자 저장.
- UX: action 인접 feedback, 잠금 사유 표시, 390px overflow 0, 한글 오류·복구 안내.
- 자동 테스트: §14 항목 전부 통과. 미실행 항목은 이유와 함께 기록.
- 5종 산출물: `docs/12-task-completion-policy.md`에 따라 상태·위치 추적.
- 사용자 검수 상태: screenshot 기반 사용자 검수 대기로 종료(자동 완료로 표기하지 않음).
- PR 상태: 없음(local experiment commit만).

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 권장 | 사용자 결정 |
| ---: | --- | --- | --- | --- |
| 1 | 조직/부서 단위 default 정책 편집 | 이번 포함 / 후속 Task | 후속 Task (deferred 유지) | 대기 |
| 2 | taxonomy 관리자 편집 UI | 이번 포함 / 후속 Task | 후속 Task (코드 고정 유지) | 대기 |
| 3 | 설정 import/export와 quiet hours | 이번 포함 / 후속 Task | 후속 Task | 대기 |
| 4 | `ProjectCompletion` 메일의 opt-out 허용 여부 | 허용 / 잠금 | 잠금 (증빙 성격 + event 연결 미확인 상태에서 정책 선점 회피) | 대기 |
| 5 | 관리자 대리 변경 권한 경계 | 기존 `AdminUsersRead` 재사용 / 신규 정책 | 기존 정책 재사용 (신규 권한 능력 미도입) | 대기 |

5개 모두 비차단이다. experiment fast-track 2차 기획에서 standing instruction에 따라 권장안을 채택할 수 있다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Notifications/`(preference store·contracts·endpoint extensions 신규, `NotificationDeliveryStore`·`WorkItemEscalationStore` 생성 SQL gate), `Program.cs` DI·endpoint 등록.
- Frontend: `frontend/src/App.tsx`(신규 view·진입 링크), 필요 시 신규 컴포넌트 파일, `frontend/src/api.ts`.
- DB/Migration: `database/migrations/0041_*.sql` (additive).
- Tests/Scripts: `backend/tests/Emi.Qms.Api.Tests/`(preference API·authorization·concurrency·delivery matrix), `PostgreSqlMigrationTests`, frontend unit, Full-Stack E2E 시나리오.
- Docs: Roadmap 5.4·추적 52 상태 update, implementation report·SOP·user manual·validation checklist.

## 18. Roadmap 연결

- 선행 Task: TASK-NOTIFY-004(완료), TASK-NOTIFY-003 provider/capability(완료), TASK-UX-001 A1 공통 feedback(experiment 구현·사용자 검수 대기).
- 후속 Task: 조직 단위 default 정책, taxonomy 편집, quiet hours, terminal Failed 수동 재처리(모두 Deferred).
- 현재 Go/No-Go: experiment fast-track 한정 Go. canonical queue의 `TASK-007A` Next Gate와 UX-001 검수 상태는 변경하지 않는다(change-001의 재정렬 승인 기록 준수).
- 별도 Task로 분리할 항목: §16의 1~3, 자동 event coverage 확장(긴급/차단 자동 Activity Feed 등).

## 19. Codex 구현 지시문 초안

1. `0041` additive migration으로 preference·audit(및 version 장치) 테이블을 추가하고 fresh/existing DB 검증을 통과시킨다. 기존 migration은 수정하지 않는다.
2. taxonomy를 코드 상수 + metadata로 정의하고, 본인 3종·관리자 3종 endpoint를 기존 endpoint extension 관례로 추가한다. 잠금·범위·stale 차단을 서버에서 강제하고 한글 오류 계약을 포함한다.
3. `NotificationDeliveryStore`의 immediate/digest insert와 `WorkItemEscalationStore`의 L0 insert에 preference left join gate를 추가해 opt-out 조합을 `Suppressed`/`SuppressedByUserPreference`로 기록한다. L1~L3·UrgentBlocking·TeamsChannel·수동 발송 경로는 변경하지 않는다.
4. Frontend에 사용자 설정 view(모바일 sheet·알림 페이지 진입)와 최소 관리자 지원 view를 추가하고 `useActionFeedback`를 재사용한다.
5. §14 검증 matrix를 실행하고 desktop·390px screenshot을 만든 뒤, implementation report·5종 산출물·local commit까지만 수행한다. push·PR·merge·Persistent UAT·실제 provider는 실행하지 않는다.

## 20. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-18 | experiment standing instruction에 따른 fast-track 1차 planning | 본 draft 작성. Codex 내용 review 1회 후 승인된 target으로 2차 기획 예정 |

## 21. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 5
