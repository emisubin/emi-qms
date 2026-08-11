# TASK-PWA-PUSH-001 — Implementation report

> 상태: local 구현·자동 검증 완료 / Git 게시·Azure runtime 적용 승인 / 실제 push service·실기기 검수 대기
> branch: `feat/task-pwa-push-001-web-push`
> 기준선: `origin/main` `30a0c2970611f76cee0c96ebb8f0e6472d7e7aee`

## 해결한 업무 문제

모바일 우선인 제조·품질 사용자가 EMI PMS를 닫은 동안 인앱 업무 알림을 놓치던 문제를 해결하기 위해, 인앱에서 실제로 볼 수 있는 알림을 사용자가 허용한 각 휴대폰·태블릿에 PWA 푸시로 파생하는 구조를 구현했다. 여러 기기 동시 로그인·수신, 현재 기기 해제와 분실 대응용 전체 기기 연결 해제를 함께 제공한다.

## 포함·제외 범위

- 포함: `tasks/pwa-push-001-planning.md`, Codex review와 `change-001`의 승인 계약.
- 제외: 실제 운영 VAPID key 생성·보관, 외부 push service actual 호출, Persistent UAT/Azure DB·runtime·배포, Microsoft 365 원격 로그아웃, 이벤트별 푸시 선택, 기기 목록·이름 관리, offline cache·background sync.

## 구현 결정

- 인앱 알림의 `RecipientOnly`와 `Authenticated` 가시성을 직접 사용하고 `AdminOnly`는 현재 인앱 계약대로 제외했다. Teams·메일 preference나 source kind 목록을 복제하지 않는다.
- 승인된 활성 사용자만 대상으로 하며 역할이 아직 없는 Entra 승인 대기 사용자, 비활성 사용자와 삭제 예약 사용자는 제외한다.
- 활성 구독 기기마다 delivery 한 건을 만들어 기존 claim/lease·attempt·generation·재처리 원장을 그대로 사용한다.
- 구독을 다시 켠 경우 이전 세대의 전송 결과가 새 구독을 끄지 못하도록 구독 세대 번호를 delivery에 고정한다.
- 기존 Teams·메일 고유 index는 보존하고 Web Push만 `notification + subscription + delivery type`으로 중복을 막는다.
- 구독 활성 시각 이전 알림은 생성 대상에서 제외한다. 채널 중지 중에도 활성 구독에 대한 `Disabled` 원장을 남겨 재활성화 뒤 소급 발송하지 않는다.
- 404/410 및 영구 4xx는 구독을 비활성화하고 terminal 처리한다. 429·일시 5xx·network timeout은 기존 retry limit을 따른다.
- payload는 인앱 제목, 일반 안내 문구, 알림 상세 경로와 중복 방지 tag만 포함한다. endpoint·암호화 key와 업무 상세는 넣지 않는다.
- 본인 설정 화면만 브라우저 기기를 제어한다. 관리자 사용자 지원 화면에서는 해당 section을 렌더링하지 않는다.
- 브라우저의 로컬 구독 해제가 실패해도 서버의 현재 기기·전체 기기 해제 결과를 기준으로 처리하고 다시 시도할 수 있는 안내를 남긴다.
- 정상 로그아웃은 현재 기기 구독 비활성화를 best-effort로 수행한 뒤 로컬 구독을 해제한다. 다른 기기와 Microsoft 365 원격 session은 건드리지 않는다.
- 계정 비활성화·삭제 예약 시 모든 활성 구독을 잠그고 해제한다. 계정을 복구해도 푸시는 자동으로 다시 켜지지 않는다.
- 실제 외부 프로토콜 호출 직전에만 provider call 감사 표시를 남기며, 사전 검증 실패와 dry-run은 외부 호출로 기록하지 않는다.
- 관리자 모니터의 Web Push `Sent`는 사용자 수신 완료가 아니라 `푸시 서비스 접수`로 표시한다.
- Service Worker는 `push`와 `notificationclick`만 처리하며 fetch·cache·sync를 포함하지 않는다.

## 실제 변경 영역

- DB: `database/migrations/0074_web_push_subscriptions.sql`
- Backend: `Notifications/WebPush*`, options·delivery contracts/store·Program·migration ledger probe
- Frontend: `webPush.ts`, `WebPushSettings.tsx`, `webPushLogout.ts`, 전용 Service Worker, 내 알림 설정·로그아웃·설치 안내·개인정보 안내
- Tests: Web Push handler/API/DB/migration, frontend 설정·Service Worker·PWA/개인정보 회귀, isolated Full-Stack spec
- Docs: Roadmap, SOP, User manual, 이 report와 user validation checklist

## 검증 결과

| 검증 | 상태 | 결과/경계 |
| --- | --- | --- |
| Backend build | PASS | warning/error 0 |
| Web Push handler 집중 검증 | PASS | 10/10, provider actual 호출 0 |
| Backend API·가시성·계정 lifecycle·migration 집중 검증 | PASS | 7/7 |
| Backend 전체 회귀 | PASS | 513/513, 실패 0, 건너뜀 0, 21분 8초 |
| Frontend 푸시 설정 집중 검증 | PASS | 10/10 |
| Frontend 전체 단위 회귀 | PASS | 63 suites, 210/210 |
| Frontend typecheck | PASS | 오류 0 |
| Frontend lint | PASS with existing warning | 오류 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend production build | PASS | build 성공; 기존 large bundle warning 유지 |
| Isolated Full-Stack | PASS | 1/1, 2개 구독→2개 dry-run delivery·현재/전체 해제·Service Worker·self/admin UI |
| Desktop/mobile 화면 검증 | PASS | 1440px·390px 흑백 wireframe 확인 |
| 실제 Android/iPhone 수신 | PENDING | 검증용 VAPID key와 외부 발송 별도 승인 필요 |

## 개인정보·secret 검토

- endpoint, `p256dh`, `auth`는 DB 외 API 응답·화면·로그·Task 산출물에 반환하지 않는다.
- 허용된 Push service host만 등록·발송하고 발송 직전에도 endpoint를 재검사한다. 예외 객체와 endpoint 원문은 로그에 남기지 않는다.
- 관리자 표시는 구독 UUID의 짧은 비식별 값만 사용한다.
- 운영 private key는 설정 contract만 추가했고 Repository나 문서에 값이 없다.
- 테스트는 `example.test`와 synthetic key 문자열만 사용한다.

## Finding과 잔여 위험

- Open P0/P1/P2: 0/0/0.
- 실제 브라우저 push service는 승인 경계 때문에 호출하지 않았다. 자동 검증은 dry-run과 fake protocol client로 영구/일시 오류 분류를 확인했다.
- 푸시 서비스의 `Sent`는 접수이며 실제 사용자 열람 확인이 아니다.
- 현재 범위는 원격 기기 목록을 제공하지 않는다. 분실 시 모든 기기 푸시를 일괄 해제하고 Microsoft 365 session은 회사 보안 절차로 별도 처리한다.

## Rollback·복구

- 게시·운영 적용 전에는 branch 변경을 게시하지 않으면 제품 영향이 없다.
- 운영 전환 후에는 Web Push `Enabled=false`로 새 외부 발송을 즉시 중지할 수 있으며 인앱·Teams·메일은 유지된다.
- migration `0074`는 additive다. 적용 뒤 schema 삭제 rollback을 하지 않고 application rollback 또는 forward-fix migration을 사용한다.

## 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | `docs/49-pwa-push-operations-sop.md` |
| User manual | 완료 | `docs/50-pwa-push-user-manual.md` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` 3.3F·Decision Log |
| User validation checklist | 자동 검증 완료 / 실기기 대기 | `tasks/pwa-push-001-user-validation-checklist.md` |
