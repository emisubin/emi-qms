# TASK-PWA-PUSH-001 Change 001 — 구현 승인과 Codex Review Resolution

- changeType: `APPROVED_FEATURE_IMPLEMENTATION`
- changeDate: `2026-08-11`
- sourcePlanning: `tasks/pwa-push-001-planning.md`
- sourceReview: `tasks/pwa-push-001-review.md`
- planningApproved: `true`
- implementationApproved: `true`
- approvalSource: `USER_EXPLICIT`
- instructionChainRead: `true`

## 사용자 승인

사용자는 Fable 기획 초안과 Codex 검토 보고를 확인한 뒤 `좋아 구현 시작해.`라고 명시해 구현을 승인했다.

추가로 다음 정책을 확정했다.

- 한 사용자는 휴대폰과 검사용 태블릿 등 여러 기기에 동시에 로그인할 수 있다.
- 푸시를 허용한 모든 기기는 같은 인앱 알림을 각각 수신한다.
- 분실 기기 대응은 본인 설정의 `모든 기기의 푸시 연결 해제`로 처리한다.
- 모든 기기 연결 해제는 Web Push 구독만 비활성화하며 Microsoft 365 로그인 세션의 원격 로그아웃은 포함하지 않는다.

## 구현 계약

1. Web Push는 실제 인앱 알림 가시성을 source of truth로 사용한다.
   - `RecipientOnly`: 명시된 활성 수신자
   - `Authenticated`: 인앱에서 볼 수 있는 모든 활성 사용자
   - `AdminOnly`: 현재 인앱 목록에 노출되지 않으므로 Web Push 제외
2. 활성 구독 기기마다 delivery 한 건을 만든다. 기존 Teams·메일 고유성 계약은 유지하고 Web Push만 notification+subscription 단위로 중복을 막는다.
3. 본인 `/notification-settings`에서만 현재 기기 푸시를 켜고 끌 수 있다. 관리자 지원 화면에는 기기 제어를 표시하지 않는다.
4. 현재 기기 로그아웃 전에 해당 기기의 구독을 best-effort로 비활성화한다. 다른 기기 로그인과 구독은 유지한다.
5. `모든 기기의 푸시 연결 해제`는 본인의 활성 구독을 모두 비활성화한다. 이후 각 기기에서 다시 켜야 한다.
6. 최소 Service Worker는 `push`와 `notificationclick`만 처리한다. fetch 가로채기, offline cache, background sync는 추가하지 않는다.
7. 푸시 payload에는 인앱 제목 수준 요약과 알림 상세 경로만 포함한다. endpoint·암호화 key·업무 상세를 노출하지 않는다.
8. Provider 결과는 다음처럼 분류한다.
   - 404/410 및 영구 4xx: 구독 비활성화와 terminal 처리
   - 429, 일시적 5xx, timeout: 기존 worker 재시도
   - `Sent`: 사용자 열람이 아니라 푸시 서비스 접수를 의미
9. 첫 사전 안내의 `나중에` 상태는 해당 기기 `localStorage`에 저장한다. 알림 설정에서는 항상 다시 진입할 수 있다.
10. 기본 운영 설정은 `Enabled=false`, `DryRun=true`다. 실제 외부 push service 호출과 운영 VAPID key·Azure·Persistent UAT 변경은 별도 승인 전 수행하지 않는다.

## 포함 범위

- additive Web Push subscription·delivery schema와 migration catalog/test
- 본인 구독 등록·현재 기기 해제·모든 기기 해제 API
- Web Push delivery 생성, dry-run, 기기별 dedupe와 영구/일시 오류 분류
- 최소 Service Worker, 로그인 후 1회 안내, 본인 알림 설정 화면
- 현재 기기 로그아웃 시 구독 해제
- 개인정보·이용 안내, Roadmap, Implementation report, SOP, 사용자 설명서와 검수 checklist

## 제외 범위

- 기존 인앱 event·수신자·그룹화 정책 재설계
- 이벤트별 푸시 설정과 원격 기기 목록·개별 원격 해제
- Microsoft 365 세션 원격 로그아웃
- 실제 외부 푸시 발송, 운영 key, Azure release, 운영·Persistent UAT migration/runtime handover
- offline cache, background sync, 일반 offline mode

## 검증 계약

- Backend: 구독 소유권·멱등 등록·현재/전체 해제·다기기 delivery·소급 방지·비활성 사용자 제외·dry-run·오류 분류·기존 채널 회귀
- Frontend: 지원/설치/권한/켜짐/차단/오류 상태, 사용자 행동 기반 권한 요청, 관리자 지원 화면 숨김, localStorage 1회 안내, logout 해제, Service Worker 클릭 경로
- Migration: fresh/upgrade 적용, ledger exact, 기존 notification delivery index·상태 계약 보존
- Full-Stack: 격리 DB와 실제 provider 차단 상태에서 등록→새 인앱 알림→기기별 dry-run delivery→해제 흐름
