# TASK-NOTIFY-005 — Fable 1차 기획 Codex 내용·제품 Review

## 1. Review 결론

- reviewStatus: `RESOLVED_FOR_SECOND_PLANNING`
- sourcePlanning: `tasks/notify-005-planning.md`
- taskType: `NEW_FEATURE`
- openBlockingDecisionCount: 0
- implementationApprovedByReview: false

1차 기획의 핵심 방향인 **고정 taxonomy + sparse user override + 서버 effective projection + delivery 생성 시 Suppressed 원장 기록**은 사용자 문제와 Roadmap 방향에 맞다. 인앱 원본과 필수 알림을 보존하면서 소음성 외부 알림만 사용자가 조절할 수 있고, 기존 사용자는 override가 없으면 현재 동작을 그대로 유지한다.

다만 현재 초안 그대로 구현하면 no-op 저장이 감사 이력을 오염시키고, 실제 event coverage가 없는 항목을 화면에 노출하며, 관리자 화면이 정의되지 않은 audit 조회까지 포함할 수 있다. 또한 자동 업무 생성과 관리자 수동 업무 배정의 사용자-facing 이름을 분리하지 않으면 preference 적용 범위를 오해할 수 있다. 아래 resolution을 2차 기획에 반영하면 blocking decision 없이 구현 가능하다.

## 2. 사용자 문제와 기대 결과

### 유지

- 사용자는 인앱 알림을 끄지 않고, 허용된 Teams 개인·메일 알림만 조절한다.
- 긴급/차단·상위 에스컬레이션처럼 업무상 필수인 외부 알림은 사용자와 관리자 모두 해제할 수 없다.
- 기본값 복원과 재로그인 후 유지가 있어야 하며, 저장 결과는 action 근처에서 확인한다.
- 관리자 대리 변경은 사용자 지원 목적의 최소 범위로 제한하고 actor·대상·전후 값을 감사한다.

### 추가

- 화면의 설정 명칭은 내부 delivery type이 아니라 실제 적용 범위를 설명해야 한다. `WorkItemCreated`는 “자동 단계 업무 생성 Teams 알림”처럼 표시해 관리자 수동 업무 배정 알림이 preference를 우회한다는 점을 사용자가 오해하지 않게 한다.
- “모두 기본값”과 “내가 변경함”을 구분해, 현재 ON/OFF만 보여 주는 것보다 사용자가 override 존재 여부를 이해할 수 있게 한다.

## 3. 제품 방향·Roadmap 정합성

### 유지

- Roadmap 6장의 채널 역할과 확정 matrix를 바꾸지 않는다.
- 인앱은 모든 알림의 원본이며 TeamsChannel 공지는 개인 preference 대상이 아니다.
- `TASK-NOTIFY-004`의 claim/lease·retry·attempt lineage·at-least-once 계약을 재구현하지 않는다.
- Relative follow-up 순서 `TASK-NOTIFY-004 → TASK-UX-001 → TASK-NOTIFY-005`와 맞고, 이번 실행은 experiment override이므로 canonical `TASK-007A` Gate를 바꾸지 않는다.

### 보류

- 조직/부서 default, taxonomy 관리자 편집, quiet hours, 설정 import/export는 별도 Task로 유지한다.
- 자동 event coverage 자체의 확대는 이번 preference Task에 섞지 않는다.

## 4. 기능 분류와 Resolution

| 분류 | 기능 | 판단·근거 | 2차 기획 Resolution |
| --- | --- | --- | --- |
| 유지 | sparse override + effective projection | 기존 사용자 기본 동작과 taxonomy 확장 안정성을 함께 보존 | override가 없으면 현재 matrix 그대로 반환·생성 |
| 유지 | opt-out 시 `Suppressed` 원장 기록 | 미발송 이유·dedupe·운영 추적을 보존 | `SuppressedByUserPreference`, `suppressed_at_utc`, provider call 0 |
| 유지 | version token과 transaction audit | lost update와 감사 누락 방지 | profile row를 lock한 뒤 expectedVersion 비교 |
| 유지 | 본인 + 최소 관리자 지원 UI | 사용자 자율성과 지원 가능성 제공 | 기존 `AdminUsersRead` 경계 안에서 active 사용자만 대리 변경 |
| 추가 | reset에도 expectedVersion 필수 | 두 세션의 stale reset이 최신 설정을 지우는 문제 방지 | 본인·관리자 reset request에 expectedVersion 포함, mismatch 409 |
| 추가 | no-op 저장·reset 무효화 | 같은 설정 재저장이 version·audit를 불필요하게 증가시키는 문제 방지 | effective override 집합이 동일하면 version/audit 불변, `changed=false` 반환 |
| 추가 | 자동 업무 생성/수동 업무 배정 표시 분리 | 수동 발송은 preference 제외인데 동일 “업무 배정” 명칭이면 사용자 기대와 충돌 | 사용자-facing 설명에 “자동 단계 업무 생성”을 명시하고 수동 발송 제외 안내 |
| 추가 | migration 3-table 최소 구조 허용 | profile version, sparse override, append-only audit의 책임이 다름 | profile·override·audit 3개 신규 테이블을 허용하고 기존 테이블은 수정하지 않음 |
| 추가 | audit는 fixed-field change row | JSON 자유 형식은 비교·privacy·검증 비용이 큼 | actor/target/action/type/channel/old/new/version/time의 정규화 row 사용 |
| 제거 | 연결되지 않은 `ProjectCompletion` 설정 노출 | Roadmap은 event 연결을 미확인으로 표시하며 사용자가 바꿔도 효과가 없는 control이 됨 | v1 taxonomy UI/API에서 제외. 실제 event coverage Task 뒤 별도 추가 |
| 제거 | 관리자 화면의 “최근 audit 요약” | 1차 API 목록에 audit read 계약이 없고 MVP 지원 범위를 키움 | audit는 DB에 보존하되 v1 UI에 history list·summary를 노출하지 않음 |
| 보류 | 관리자 전역 정책·taxonomy 편집 | 신규 권한·운영 정책과 migration lifecycle을 요구 | 별도 Task |

## 5. 권장 v1 Taxonomy

| 사용자-facing event | 내부 delivery type | channel | 기본값 | 사용자 변경 | 이유 |
| --- | --- | --- | --- | --- | --- |
| 자동 단계 업무 생성 | `WorkItemCreated` | Teams 개인 | ON | 가능 | 업무는 인앱에 남고 자동 외부 개입만 조절 |
| 예정일 임박 D-1 | `DueSoonL0` | Teams 개인 | ON | 가능 | 사전 예고 성격 |
| 일일 업무 요약 | `DailyDigest` | Mail | ON | 가능 | 요약·소음 조절 가치가 큼 |
| 긴급/차단 | `UrgentBlocking` | Mail | ON | 잠금 | 업무상 필수 |
| 예정일 초과 L1 | `OverdueL1` | Teams 개인·Mail | ON | 잠금 | 즉시 에스컬레이션 |
| 예정일 초과 L2 | `OverdueL2` | Teams 개인 | ON | 잠금 | 상위 수신자 개입 |
| 예정일 초과 L3 | `OverdueL3` | Mail | ON | 잠금 | 생산관리·영업 필수 에스컬레이션 |

인앱과 TeamsChannel은 이 표의 사용자 toggle로 만들지 않고, 화면 상단에 “인앱 알림은 항상 저장되고 통합 채널 공지는 조직 공지로 유지됩니다”라고 설명한다. `ReferenceDigest`는 현재 개별 delivery 생성이 아니라 DailyDigest content이므로 별도 toggle로 만들지 않는다. `ManualTest`와 모든 관리자 수동 발송은 제외한다.

## 6. 실제 Repository 대조

### Backend

- `NotificationDeliveryStore.CreateImmediateDeliveriesAsync`는 urgent TeamsChannel, urgent Mail, 제목 패턴 기반 automatic WorkItem Teams DM을 별도 insert한다. 변경 가능한 gate는 automatic WorkItem Teams DM 한 곳이고 urgent 두 경로는 필수/조직 경로로 그대로 둔다.
- `CreateDailyDigestDeliveriesIfDueAsync`는 사용자별 Mail row를 만들므로 opt-out 시 row를 생략하지 않고 `Suppressed`로 삽입해야 한다.
- `WorkItemEscalationStore.InsertDeliveriesForRecipientAsync`는 L0~L3 delivery를 직접 만든다. L0 Teams 개인만 preference를 적용하고 L1~L3는 기존 matrix를 유지한다.
- Provider handler·claim query·completion·attempt table은 변경할 필요가 없다.
- 기존 `QmsPolicies.AdminUsersRead`는 `users.manage` 기반 admin user·notification mutation에 이미 사용된다. 신규 permission을 추가하지 않는 결정은 Repository 관례와 맞다.

### DB·동시성

- 현재 latest experiment migration은 `0040`이므로 다음 additive 번호 `0041`이 맞다.
- sparse override와 stale 차단을 안정적으로 구현하려면 사용자당 profile/version row, override row, append-only audit row가 각각 필요하다. 첫 동시 저장 race는 profile row `insert ... on conflict do nothing` 뒤 `select ... for update`로 직렬화한다.
- 사용자 설정 저장과 reset은 외부 provider를 호출하지 않으며, delivery 생성 gate는 별도 worker transaction에서 최신 committed preference를 읽는다.

### Frontend

- `App.tsx`는 hand-rolled `View`·route switch와 desktop navigation, 모바일 상태 sheet를 사용한다. 개인 설정은 별도 route로 만들고 알림 페이지 link + 모바일 상태 sheet action 두 진입점을 제공하는 것이 기존 구조에 맞다.
- `useActionFeedback`는 직전 A1 experiment에 존재하므로 저장·복원 scope, focus와 `aria-live`에 재사용한다.
- 관리자 사용자 페이지에서 대상 row의 “알림 설정” action으로 별도 지원 view를 열면 기존 사용자 관리 맥락을 유지할 수 있다.

## 7. 권한·예외·복구 Review

- 본인 API는 current user id만 사용하고 request body의 user id를 받지 않는다.
- 관리자 API는 active 사용자만 대상으로 하고 대상 없음과 비활성을 구분 가능한 안정 오류로 처리한다.
- 지원되지 않거나 잠긴 조합을 payload에 넣으면 전체 저장을 원자적으로 거부한다. 일부만 적용하지 않는다.
- 저장·reset 409는 최신 서버 값을 자동 덮어쓰지 않고 사용자가 “다시 불러오기”를 선택하게 한다.
- ReviewSafe에서는 기존 HTTP mutation guard가 최종 차단하며 UI도 mutation control을 비활성화한다.
- opt-out delivery는 `attempt_count=0`, `next_attempt_at_utc=null`, provider-call-start audit 0으로 유지한다.

## 8. 권장 개발 순서

1. `0041` profile·override·audit migration과 catalog/fresh/existing 검증
2. taxonomy contracts·store의 본인/관리자 get-save-reset, version/no-op/audit/authorization tests
3. automatic WorkItem·DailyDigest·L0 생성 gate와 locked matrix 회귀 tests
4. Frontend API types와 개인 설정 route/card UX·action feedback
5. 관리자 사용자 row 진입과 지원 view
6. desktop·390px·isolated Full-Stack E2E, screenshot, 5종 산출물과 local commit

## 9. Finding

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `PREFERENCE_NOOP_AUDIT_NOISE` | P2 | RESOLVED_IN_REVIEW | 1차 기획은 같은 payload 재저장도 version·audit를 증가시켜 실제 변경 이력을 흐릴 수 있음 | no-op이면 version·audit 불변 계약을 2차 기획에 반영 |
| `PROJECT_COMPLETION_DEAD_CONTROL` | P2 | RESOLVED_IN_REVIEW | 실제 event 연결 미확인인데 잠긴 설정을 노출하면 사용자가 효과를 오해 | v1 taxonomy에서 제거하고 event coverage 뒤 추가 |
| `MANUAL_WORK_ASSIGNMENT_SCOPE_AMBIGUITY` | P2 | RESOLVED_IN_REVIEW | WorkItemCreated opt-out과 관리자 수동 업무 배정 bypass의 표시명이 충돌 | “자동 단계 업무 생성”으로 표시하고 수동 발송 제외 안내 |
| `ADMIN_AUDIT_UI_SCOPE_DRIFT` | P3 | DEFERRED | 관리자 audit 요약은 read API·UX 계약 없이 범위를 확대 | audit DB 보존만 하고 UI 조회는 후속 Task |

Open P0/P1/P2는 0이다. P3는 후속 범위로 명시됐으며 현재 구현을 차단하지 않는다.

## 10. 2차 기획 지시

Fable 2차 기획은 1차 원문과 이 review 전체를 읽고 다음을 완전한 구현 계약에 반영한다.

- v1 taxonomy를 §5의 실제 event coverage로 제한
- reset expectedVersion과 no-op version/audit 불변
- profile·override·fixed-field audit 3-table additive `0041`
- automatic WorkItem·DailyDigest·L0만 preference gate 적용
- manual send·urgent·L1~L3·TeamsChannel·in-app 불변
- 개인 route의 두 진입점과 관리자 user-row 진입
- 관리자 audit UI 제거, 조직 default·taxonomy 편집·quiet hours Deferred
- blocking decision 0이면 experiment implementation 진행
