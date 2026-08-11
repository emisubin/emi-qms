# TASK-PWA-PUSH-001 — Codex 기획 검토

- reviewedPlanning: `tasks/pwa-push-001-planning.md`
- reviewOwner: `CODEX`
- reviewDate: `2026-08-11`
- reviewStatus: `REVIEW_COMPLETE`
- implementationApproved: `true` — `tasks/pwa-push-001-change-001.md`

## 결론

Fable 기획의 핵심 방향은 유지한다. 설치형 PWA에서만 사용자 행동으로 권한을 받고, 실제 인앱 알림을 source of truth로 삼아 기기별 Web Push를 만들며, 최소 Service Worker·기본 비활성·dry-run·kill switch로 기존 채널과 분리하는 범위가 사용자 문제에 가장 직접적이다.

다만 그대로 구현하면 분실 기기 해제, 관리자 지원 화면의 현재 기기 오인, 인앱 수신자와 Web Push 수신자의 drift, 기존 delivery unique index 충돌 가능성이 남는다. 아래 resolution을 구현 계약에 추가한 뒤 착수해야 한다.

## 사용자 문제·제품 방향 검토

- 모바일 우선인 제조·품질 사용자가 앱을 닫은 상태에서 긴급 업무를 놓치는 문제가 명확하고, PWA 푸시는 이를 직접 해결한다.
- Teams·메일을 대체하지 않고 인앱 알림의 파생 채널로 두는 방향은 기존 알림 구조와 맞다.
- 이벤트별 푸시 설정, 원격 기기 목록, offline cache를 제외한 것은 이번 목적에 비해 과도한 운영 부담을 막는 적절한 범위 제한이다.
- 기존 알림 정책 정비가 수신자·발송 시점을 바꾸므로, Web Push가 현재 `source_kind` 목록을 따로 복사하면 곧바로 불일치한다. 최종 인앱 가시성·수신자 계약을 공통 기준으로 사용해야 한다.

## 유지

1. **인앱 알림과 동일한 파생 채널**: 인앱 알림이 실제로 보이는 사용자에게만 푸시를 만든다. 메일 전용·Teams 전용 event는 제외한다.
2. **기기별 delivery**: 구독 기기별 성공·실패·만료와 재처리를 분리해 추적한다.
3. **최소 Service Worker**: `push`와 `notificationclick`만 처리하고 fetch·offline cache·background sync는 넣지 않는다.
4. **사용자 행동 기반 권한 요청**: 설치형 PWA 첫 로그인 뒤 1회 안내, 버튼을 누른 경우에만 브라우저 권한을 요청한다.
5. **기존 알림 상세로 이동**: 잠금 화면에는 인앱 제목 수준만 표시하고, 클릭 뒤 기존 인증·권한 검사를 거쳐 `/teams/activity/notifications/{id}`로 이동한다.
6. **안전한 운영 기본값**: `Enabled=false`, `DryRun=true`, Web Push 전용 kill switch를 유지한다.

## 추가

### 1. 분실 기기용 전체 해제 수단 — P1, 사용자 결정 필요

현재 선택한 “현재 기기만 관리”만 구현하면 휴대폰을 분실했을 때 새 기기에서 예전 기기의 푸시를 끌 수 없다. Web Push 구독은 자동으로 빨리 만료된다고 보장할 수 없고, 잠금 화면에 업무 제목이 계속 표시될 수 있다.

기기 목록·이름 관리까지 만들 필요는 없다. 최소 안전장치로 본인 설정에 **`모든 기기의 푸시 연결 해제`** 버튼 하나를 추가하는 것을 권장한다. 이 버튼은 본인의 활성 구독을 모두 비활성화하고, 각 기기에서 다시 허용하기 전까지 푸시를 보내지 않는다. 관리자에게 개별 구독 원문이나 현장 설정 권한을 주지 않는다.

- 사용자 결정: 2026-08-11 `전체 해제` 확정.
- 확정 동작: 실행한 사용자의 모든 활성 Web Push 구독을 비활성화한다. 보유 중인 휴대폰·태블릿은 각 기기에서 다시 켜야 한다. Microsoft 365 session 자체의 원격 로그아웃은 이번 Task에 포함하지 않는다.

### 2. 실제 인앱 가시성과 동일한 수신자 계산 — P1

현재 인앱 조회는 `notification_recipients`의 개인 수신자뿐 아니라 `visibility_scope='Authenticated'`인 전체 사용자 알림도 보여준다. Web Push 생성은 특정 `source_kind`나 Teams delivery 목록을 하드코딩하지 않고 다음 실제 가시성 계약을 한 곳에서 재사용해야 한다.

- `RecipientOnly`: `notification_recipients`에 포함된 활성 사용자
- `Authenticated`: 인앱에서 실제로 볼 수 있는 모든 활성 사용자
- `AdminOnly`: 현재 인앱 목록 접근 계약과 동일한 경우에만 대상. 현재 목록 API가 노출하지 않는다면 푸시도 보내지 않음

Teams·메일 preference는 인앱 알림 생성 여부를 바꾸지 않으므로 Web Push 대상 계산에 사용하지 않는다.

### 3. 관리자 지원 화면에서 기기 제어 숨김 — P1

`NotificationPreferencesPage`는 내 설정과 관리자가 다른 사용자의 설정을 지원하는 화면을 함께 사용한다. 현재 브라우저의 Push subscription은 로그인한 실제 기기의 것이므로 `targetUserId`가 있는 관리자 지원 화면에 푸시 켜기·끄기를 표시하면 관리자 기기를 다른 사용자에게 연결할 위험이 있다.

푸시 상태·권한·구독 mutation은 `/notification-settings`의 본인 화면에서만 제공한다. 관리자 지원 화면에서는 delivery preference만 유지하고 푸시 기기 제어를 숨긴다.

### 4. 기기별 delivery용 DB 고유성 계약 — P2

현재 `notification_deliveries`에는 `(notification_id, recipient_user_id, channel, delivery_type)` unique index가 있어 같은 사용자의 여러 기기 delivery를 그대로 넣을 수 없다. additive migration은 privacy-safe 구독 식별자를 delivery에 연결하고, Web Push만 `notification + subscription` 단위로 고유하게 만드는 조건부 index·dedupe 계약을 추가해야 한다. 기존 Teams·메일 unique index 의미는 보존한다.

정확한 table·column 이름은 구현 조사에서 확정하되, 구독 endpoint·key를 dedupe key나 관리자 표시값으로 사용하지 않는다.

### 5. 발송 결과의 의미와 실패 분류 — P2

브라우저 push service가 요청을 접수한 `Sent`는 사용자가 실제로 보거나 읽었다는 뜻이 아니다. 관리자 화면 문구는 `푸시 서비스 접수` 수준으로 표시해야 한다.

- 404/410: 구독 소멸로 즉시 비활성화
- 429/일시적 5xx·timeout: 기존 worker 재시도
- 영구 4xx·잘못된 구독: 비활성 또는 영구 실패

“반복 실패”라는 모호한 기준만 두지 말고 provider 응답 분류와 기존 retry limit을 계약으로 고정한다.

### 6. 1회 안내의 저장 범위 — P2

“나중에”를 누른 뒤 다시 자동으로 띄우지 않는 상태는 해당 기기 브라우저의 `localStorage`에 저장한다. `sessionStorage`를 사용하면 새 탭·새 실행 때 다시 떠서 확정 정책과 어긋난다. 사용자는 알림 설정에서 항상 다시 진입할 수 있다.

## 보류

- 기기 이름·전체 기기 목록·개별 원격 해제 UI
- 이벤트 종류별 푸시 on/off와 사용자 전체 일시 중지 스위치
- offline cache·background sync·일반 offline mode
- PC Web Push를 모바일과 같은 필수 사용자 검수 대상으로 확대하는 것
- 운영 VAPID key·실사용자 발송·Persistent UAT·Azure 교체

## 제거·수정

1. Planning 7장의 “사용자별 알림 preference 등으로 인앱 알림이 생성되지 않으면” 문구는 제거한다. 현재 preference는 Teams·메일 delivery를 제어하며 인앱 알림 자체를 끄지 않는다. Web Push는 실제 인앱 가시성으로만 판단한다.
2. migration 번호 `0074`는 예상값으로만 취급하고 구현 계약에 고정하지 않는다. 선행 알림 정책 구현이 migration을 추가할 수 있으므로 최신 `origin/main`에 rebase한 뒤 다음 연속 번호를 사용한다.
3. “반복 실패 구독 자동 정리”는 위 5번의 명확한 영구·일시 오류 분류로 대체한다.

## Fable 미결정 2건에 대한 Codex 권고

1. **관리자 발송 모니터**: 기기 단위 행을 유지한다. 실패·재처리를 정확히 다룰 수 있고, 기기 표시는 endpoint가 아닌 비식별 짧은 식별자와 등록 시각만 사용한다.
2. **검증용 VAPID key**: 비운영 검증용 key를 별도로 생성해 실기기 검수까지 수행하는 안을 권장한다. private key는 Repository·Task 문서·로그에 두지 않고 승인된 secret 저장소/환경값에만 둔다. 실제 브라우저 push service 호출은 그 검수 단계의 별도 승인 뒤 수행한다.

## 사용자 후속 설명 반영 — 2026-08-11

- 한 사용자가 본인 휴대폰과 검사용 태블릿 등 여러 기기에 동시에 로그인할 수 있어야 한다.
- 푸시를 허용한 로그인 기기는 모두 같은 인앱 알림을 받아야 한다.
- 따라서 기기별 subscription과 기기별 delivery는 사용자 선택이 아니라 위 업무 요구를 충족하기 위한 필수 구현 조건으로 확정한다.
- 현재 앱 로그아웃은 해당 기기의 MSAL/Microsoft session만 종료하며, 서버에는 다른 기기를 원격 로그아웃시키는 app session registry가 없다.
- 이번 Web Push 범위에서는 현재 기기 로그아웃 전에 해당 기기의 푸시 subscription을 비활성화할 수 있다. 분실 기기의 Microsoft 365 session 자체를 다른 기기에서 강제 종료하는 것은 Web Push 연결 해제와 다른 보안 기능이며 별도 범위다.
- 비운영 VAPID key는 사용자-facing 정책이 아니라 실기기에서 실제 푸시 수신을 검증하기 위한 기술 설정이다. key 생성과 보관 방식을 Codex 권고안으로 계획하되, 외부 push service 실제 호출 직전에만 별도 실행 승인을 받는다.

## 권장 개발 순서

1. 기존 알림 정책 Task에서 인앱 event·수신자·그룹화를 먼저 최종 구현한다.
2. 최신 통합 기준선에서 인앱 가시성을 재사용하는 Web Push 대상 query와 구독·delivery schema를 구현한다.
3. kill switch·dry-run·기기별 dedupe·provider 오류 분류를 Backend 테스트로 고정한다.
4. 본인 전용 구독 API와 분실 기기 전체 해제 수단을 구현한다.
5. 최소 Service Worker, 첫 1회 안내와 본인 알림 설정 화면을 현재 Graphite wireframe으로 구현한다. 관리자 지원 화면에는 기기 제어를 노출하지 않는다.
6. 개인정보 안내와 운영 문서를 동기화하고 Backend·Frontend·격리 Full-Stack 검증을 진행한다.
7. 별도 승인 뒤 비운영 key로 Android·iPhone 실기기 수신·클릭·해제 검수를 수행한다.

## Review Finding

| ID | 심각도 | Finding | Resolution |
| --- | --- | --- | --- |
| `PWA-PUSH-R01` | P1 | 현재 기기만 해제하면 분실 기기 푸시를 중단할 수 없다. | `RESOLUTION_CONFIRMED` — 본인용 모든 기기 푸시 연결 해제를 추가한다. |
| `PWA-PUSH-R02` | P1 | 공유 설정 컴포넌트의 관리자 지원 화면에서 현재 기기 구독을 잘못 연결할 수 있다. | 본인 route에서만 푸시 제어를 렌더링·호출한다. |
| `PWA-PUSH-R03` | P1 | 특정 delivery/source 목록을 복사하면 최종 인앱 수신자 정책과 drift한다. | 인앱 가시성 계약을 직접 재사용하고 선행 알림 정책 통합 뒤 구현한다. |
| `PWA-PUSH-R04` | P2 | 기존 unique index는 사용자 다기기 delivery를 허용하지 않는다. | 구독 식별자를 포함한 Web Push 전용 고유성·dedupe migration을 추가한다. |
| `PWA-PUSH-R05` | P2 | `Sent`·반복 실패 표현이 실제 수신과 실패 유형을 구분하지 못한다. | 접수 상태 문구와 영구/일시 오류 분류를 고정한다. |
| `PWA-PUSH-R06` | P2 | session 단위 안내 억제는 “1회 안내” 정책과 다르다. | 기기 localStorage 기반 1회 안내로 고정한다. |

Open Finding: P0 `0`, P1 `3`, P2 `3`.

## 승인 전 필요한 resolution

- 사용자 결정 완료: 분실 기기 대응용 `모든 기기의 푸시 연결 해제`를 최소 안전장치로 추가한다.
- 확정된 요구: 여러 기기에 동시에 로그인하고, 푸시를 허용한 모든 기기에서 같은 알림을 수신한다. 이에 따라 기기 단위 delivery를 채택한다.
- 기술 resolution: 비운영 검증용 VAPID key 사용을 권고안으로 채택하되 실제 push service 호출은 별도 승인 전까지 실행하지 않는다.
- 나머지 P1/P2는 사용자 선택을 넓히지 않는 필수 구현·검증 조건으로 planning resolution에 추가한다.

제품 미결정 사항은 없다. 이 planning과 review resolution에 대한 사용자의 구현 승인이 있기 전에는 구현을 시작하지 않는다.
