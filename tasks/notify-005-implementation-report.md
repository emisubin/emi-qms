# TASK-NOTIFY-005 Implementation report — 사용자별 알림 설정

## 1. 요약과 상태

- 목적: 사용자가 인앱 원본·필수 알림을 보존하면서 소음성 외부 알림 3종을 직접 끄고 기본값으로 복원한다.
- 상태: experiment 구현·자동 검증·격리 UI 검증·local commit 완료 / 사용자 검수 대기
- 최종 계약: [Fable 2차 기획](../docs/26-notification-preferences-plan.md)
- Branch/base: `experiment/task-notify-005-preferences` / `5cd223c87700a33924f29875286c71a7b8967041`
- 대표 repo·`main`·Persistent UAT·actual provider: 미변경
- Merge 승인: `0/3`

## 2. 해결한 업무 문제

기존 delivery는 모든 활성 사용자에게 같은 외부 채널을 만들었고, 사용자는 반복성 알림을 줄일 방법이 없었다. 이번 구현은 `WorkItemCreated` Teams 개인, `DueSoonL0` Teams 개인, `DailyDigest` Mail만 선택 가능하게 하고 필수 알림·인앱 원본·관리자 수동 발송은 기존 계약으로 고정했다.

## 3. 구현 범위와 아키텍처

### DB·Migration

- Migration: `0041_user_notification_preferences.sql`
- Additive 3 tables: profile/version, sparse opt-out, fixed-field append-only audit
- 기존 migration·table 수정 없음
- sparse override는 지원 조합 3개와 `is_enabled=false`만 constraint로 허용한다.
- user lifecycle purge guard에 `target_user_id`를 추가해 audit FK가 안정적으로 삭제 보류에 포함되게 했다.

### Backend·API·권한

- 고정 taxonomy 7개와 사용자 변경 가능 3개를 코드에 정의했다.
- 본인 GET/PUT/reset과 관리자 GET/PUT/reset 6개 endpoint를 추가했다.
- 본인 범위는 claim user id만 사용하고 관리자는 기존 `QmsPolicies.AdminUsersRead`를 재사용한다.
- profile row lock과 expectedVersion으로 첫 저장 경쟁·동시 저장을 직렬화한다.
- no-op은 version/audit를 변경하지 않는다.
- locked/unsupported 400, inactive/stale 409, not found 404, forbidden 403을 구분한다.

### Delivery gate

- 자동 단계 업무 생성, 일일 요약, L0 개인 알림 생성 시 최신 committed preference를 읽는다.
- opt-out이어도 delivery row를 보존하고 `SuppressedByUserPreference`, suppressed time, attempt 0, next attempt null로 기록한다.
- urgent, L1~L3, TeamsChannel, 인앱, 관리자 수동·테스트와 기존 attempt/retry는 변경하지 않았다.

### Frontend·UI/UX

- `/notification-settings` 본인 화면과 `/admin/users/{id}/notification-settings` 관리자 지원 화면을 추가했다.
- 알림 화면, 모바일 `앱 상태와 계정`, 관리자 사용자 행에서 진입한다.
- 모바일은 한 열 compact card, desktop은 2열 card이며 원·정사각·각진/둥근/타원형 요소를 함께 사용했다.
- `useActionFeedback`로 저장·복원 scope, 중복 submit, 409 안내, aria-live를 구현했다.
- 390 viewport에서 숨김 switch의 전역 input width가 overflow를 만들던 결함을 수정해 page scroll width를 viewport 이하로 확인했다.

### Excel/PDF/첨부·Workflow 영향

- Excel/PDF/attachment: `N/A` — export·문서 생성·첨부 계약을 변경하지 않았다.
- Workflow: 단계·업무 생성·인앱 원본은 불변이고 신규 외부 delivery 생성 상태만 preference에 따라 Suppressed가 된다.

## 4. 기술적 결정과 검토한 대안

- 조직 default 대신 사용자 sparse opt-out만 저장해 기존 사용자 기본 동작을 보존했다.
- delivery row 생략 대신 Suppressed 원장을 남겨 dedupe·운영 추적을 보존했다.
- 자유 JSON audit 대신 항목별 fixed fields를 사용했다.
- `ProjectCompletion`은 실제 event coverage가 없어 dead control을 만들지 않고 제외했다.
- 관리자 audit 조회 UI는 v1에서 제외하고 저장 원장만 구현했다.

## 5. 시행착오 및 폐기한 접근

- 최초 WorkItem SQL patch가 인접 urgent Mail column list에 잘못 적용돼 filtered test가 SQL expression count 오류를 발견했다. urgent 경로를 원상 보존하고 WorkItem insert에만 상태 필드를 적용해 해결했다.
- 390px 화면에서 숨김 checkbox가 공통 input width를 상속해 page overflow가 발생했다. 1px clipped input과 mobile-specific page overflow rule로 해결하고 `bodyScrollWidth=375`, viewport override 390에서 재검증했다.
- 숨김 switch의 browser `setChecked`는 상태 변경 후 timeout을 반환했다. fresh DOM에서 실제 `끄기` 상태를 확인한 뒤 저장·복원 flow를 계속 검증했다.

## 6. 변경 파일과 역할

- `database/migrations/0041_user_notification_preferences.sql`: 3-table additive schema·constraints·indexes
- `backend/.../NotificationPreference*`: taxonomy, contracts, store, 6 endpoints
- `NotificationDeliveryStore.cs`, `WorkItemEscalationStore.cs`: 허용 3개 automatic delivery gate
- `Program.cs`: DI·endpoint mapping
- `AdminScheduledDeletionService.cs`: target audit reference lifecycle guard
- `NotificationDeliveryTests.cs`, `PostgreSqlMigrationTests.cs`: API·동시성·delivery·migration 회귀
- `frontend/src/NotificationPreferencesPage.tsx`, `notificationPreferences.ts`, `api.ts`: 설정 화면·type·API
- `frontend/src/App.tsx`, `styles.css`: route·진입점·adaptive UI
- `frontend/tests/NotificationPreferencesPage.test.tsx`: 본인/관리자 UI·save/reset contract

## 7. 실행한 검증과 결과

| 검증 | 결과 |
| --- | --- |
| Backend Release build | 성공, warning/error 0 |
| Preference + migration filtered tests | 31/31 성공 |
| Backend 전체 | 391/391 성공, 5분 21초 |
| Frontend lint | error 0, 기존 `main.tsx` fast-refresh warning 1 |
| Frontend unit | 101/101 성공 |
| Frontend typecheck/build | 성공; 기존 500KB chunk warning 유지 |
| Browser desktop/mobile | 본인·관리자 4화면 확인, console error/warn 0 |
| Browser interaction | 관리자 대리 save version 0→1, reset 1→2 성공 |
| Mobile overflow | 390 viewport override에서 document/body scroll width 375, horizontal overflow 0 |
| Isolation cleanup | DB drop 확인, Compose container/network 제거, runtime 종료 |

실제 확인 URL은 격리 실행 중 `http://127.0.0.1:25174`와 `http://127.0.0.1:25081`이었으며 보고 시점에는 종료됐다. Persistent UAT와 actual provider 검증은 승인 범위 밖이라 실행하지 않았다.

## 8. 개인정보·secret 검토

- screenshot과 문서는 seeded Dev 역할명·고정 테스트 UUID만 사용한다.
- 실제 사용자·고객·프로젝트 원문, credential, token, provider payload, raw API/DB body를 tracked 산출물에 기록하지 않았다.
- external provider는 disabled/dry-run이고 provider call 0을 검증했다.

## 9. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `PROJECT_COMPLETION_DEAD_CONTROL` | P2 | RESOLVED | event coverage 없는 toggle은 무효 설정이 됨 | v1 taxonomy에서 제외 |
| `PREFERENCE_NOOP_AUDIT_NOISE` | P2 | RESOLVED | 무변경 저장이 version/audit를 오염할 수 있음 | set equality 후 `changed=false`, version/audit 불변 |
| `MANUAL_WORK_ASSIGNMENT_SCOPE_AMBIGUITY` | P2 | RESOLVED | 자동/관리자 직접 업무 배정의 범위 혼동 | 사용자-facing 명칭·상단 설명과 gate 범위 고정 |
| `WORKITEM_INSERT_COLUMN_TARGET_DRIFT` | P2 | RESOLVED | 인접 urgent SQL column list 오적용 | filtered integration test로 발견, urgent 무변화·WorkItem만 수정 |
| `MOBILE_SWITCH_INPUT_OVERFLOW` | P2 | RESOLVED | 숨김 input 공통 width 상속 | clipped 1px input·specific mobile overflow rule·실화면 재검증 |
| `ADMIN_AUDIT_UI_SCOPE_DRIFT` | P3 | BACKLOG | v1에 이력 UI까지 포함하면 범위·운영 비용 증가 | Roadmap 추적 backlog의 감사 조회 UI 후속 항목으로 이관 |

Open P0/P1/P2: `0/0/0`. Risk acceptance 없음.

## 10. 사용자 검수 결과와 남은 항목

- 자동 검증: 완료
- 사용자 검수 checklist: 작성됨
- 사용자 직접 검수: 대기
- 확인 대상: [tasks/notify-005.md](notify-005.md)의 4개 screenshot과 checklist
- Persistent UAT migration/runtime, push/PR/merge, actual provider는 별도 승인 전까지 금지

## 11. Rollback·forward-fix

- 코드: experiment commit을 기준으로 후속 revert commit을 사용한다. 대표 branch에는 반영되지 않았다.
- DB: migration `0041`을 기존 환경에서 destructive rollback하지 않는다. schema/data 오류는 새 migration 번호로 forward-fix한다.
- preference row 제거는 기본값 복원 API를 사용하고 직접 delivery history를 삭제하지 않는다.

## 12. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | [tasks/notify-005-sop.md](notify-005-sop.md) |
| User manual | 완료 | [tasks/notify-005-user-manual.md](notify-005-user-manual.md) |
| Roadmap update | 완료 | [docs/00-product-roadmap.md](../docs/00-product-roadmap.md) TASK-NOTIFY-005·추적·Decision Log |
| User validation checklist | 사용자 검수 대기 | [tasks/notify-005.md](notify-005.md) |

## 13. Fable 사용량·cleanup

- 1차 planning 전/후: 5시간 19%/19%, 전체 주간 11%/11%, Fable 주간 21%/21%
- 2차 planning 전/후: 5시간 28%/28%, 전체 주간 11%/11%, Fable 주간 22%/22%
- 구현 종료: 5시간 40% 사용·60% 잔여, 전체 주간 12%·88% 잔여, Fable 주간 24%·76% 잔여
- Fable session cleanup: `FABLE_TASK_SESSION_CLEANED`, session 1·transcript 1 제거, missing 0
