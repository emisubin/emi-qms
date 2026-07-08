# TASK-NOTIFY-003 구현 보고서

## 1. 목적

Teams Activity Feed actual 발송을 EMI 프로젝트 통합관리시스템의 인앱 알림 원본 구조에 연결하고, TeamsChannel / TeamsActivity / Mail 외부 채널을 `notification_deliveries`로 추적한다.

또한 관리자가 개인 알림, 채널 공지, 업무 배정 알림을 수동으로 queue에 등록하고, 실패/대기 발송 상태를 운영 화면에서 확인·제외·재시도할 수 있게 한다.

## 2. 구현 범위

- 인앱 `notifications` 원본 구조 보강
- `notification_recipients` 기반 개인 수신자 추적
- `notification_deliveries` 기반 TeamsChannel / TeamsActivity / Mail 발송 이력
- TeamsActivity actual provider
- TeamsActivity `topic.source=text` + Teams deep link `topic.webUrl`
- `/teams/activity` Teams tab 화면
- `/teams/activity/notifications/{notificationId}` 사용자-facing 알림 상세
- 로그인/권한/returnUrl fallback
- notification detail read 처리
- 관련 업무/관련 프로젝트 link
- 관리자 수동 알림 발송 3모드
  - 개인 알림
  - 채널 공지
  - 업무 배정
- 수동 업무 배정 시 실제 `work_items` 생성
- queue 기반 수동 발송
- display snapshot/detail
- admin handling: acknowledge, dismiss, pending retry
- HTTPS local Teams test 실행 지원
- SOP와 유저매뉴얼 작성

## 3. 제외 범위

- Teams manifest/icon repo 포함
- 운영 URL 확정 및 운영 Teams 앱 재배포
- `projectCreated` Teams activityType 추가
- 사용자별 알림 설정 UI
- Teams DM/Bot 구현
- 실패 delivery 강제 성공 처리
- delivery row hard delete
- Teams 채널 멤버십 동기화
- 운영 Webhook/SMTP/Graph secret 저장

## 4. DB / Migration 0023~0027

- `0023_teams_activity_delivery_channel.sql`
  - `notification_deliveries.channel`에 `TeamsActivity`를 추가한다.
- `0024_notification_delivery_admin_handling.sql`
  - `admin_handling_status`, `admin_handled_at_utc`, `admin_handled_by_user_id`, `admin_handling_note`를 추가한다.
- `0025_notification_delivery_display_snapshot.sql`
  - 제목, 메시지, 프로젝트, 업무, 수신자, 채널 대상, 수동 알림 종류, correlation id snapshot을 추가한다.
- `0026_notification_delivery_manual_payload.sql`
  - queue worker가 수동 알림을 렌더링할 수 있도록 `manual_payload_json`, 요청자, 요청시각을 추가한다.
- `0027_notification_access_scope_and_manual_work_items.sql`
  - `notifications.visibility_scope`, `source_kind`, `work_item_id`, `manual_requested_by_user_id`를 추가한다.
  - `RecipientOnly`, `Authenticated`, `AdminOnly` 접근 범위를 구분한다.

기존 main 반영 migration `0001~0022`는 수정하지 않았다.

## 5. 인앱 알림 원본 구조

- 모든 알림은 `notifications`를 원본으로 한다.
- 개인 알림은 `notification_recipients`로 수신자를 표현한다.
- 외부 채널 발송은 `notification_deliveries`에 기록한다.
- 외부 발송 실패와 무관하게 인앱 알림 원본은 남는다.
- 사용자-facing 알림 상세는 notification id 기준으로 조회한다.
- 관리자 delivery 상세는 delivery id 기준으로 별도 조회한다.

## 6. 알림 접근권한 정책

- 개인 알림: `notification_recipients.user_id = current user`인 수신자만 조회 가능하다.
- 채널 공지: 현재 시스템은 Teams 채널 멤버십을 알지 못하므로 `visibility_scope=Authenticated`로 두고 로그인된 active 사용자가 조회 가능하다.
- 관리자 delivery 상세: System Administrator 전용 admin route로 분리한다.
- 미로그인 상태는 로그인 화면을 보여주고 returnUrl로 상세 복귀한다.
- 권한 없음은 403 안내, 대상 없음은 404 안내를 빈 화면 없이 표시한다.

## 7. TeamsActivity actual 구조

- Graph `sendActivityNotification`을 사용한다.
- 권한은 `TeamsActivity.Send` 기준이다.
- 수신자는 active EntraId 사용자이고 `entra_object_id`가 필요하다.
- 운영 기본값에서 사용자별 `InstalledAppId`는 요구하지 않는다.
- `InstalledAppId` + `entityUrl` 방식은 diagnostic/fallback 용도로만 유지한다.
- manifest에 없는 activityType은 사용하지 않는다.

## 8. text topic + Teams deep link

TeamsActivity payload 기본 구조:

- `topic.source = text`
- `topic.value = 알림 제목`
- `topic.webUrl = https://teams.microsoft.com/l/entity/{TeamsCatalogAppId}/home?...`
- Graph payload의 `teamsAppId`는 기본 생략한다.

Teams deep link:

- app id는 `TeamsCatalogAppId`를 우선 사용한다.
- entity id는 manifest static tab의 `home`을 사용한다.
- deep link의 `webUrl` query는 `https://localhost:5174/teams/activity/notifications/{notificationId}` 같은 인앱 알림 상세 URL을 가리킨다.
- `context.subEntityId = notification:{notificationId}`를 포함한다.
- correlation id, token, secret, installedAppId는 사용자 URL에 넣지 않는다.

## 9. `/teams/activity` 및 notification detail

- `/teams/activity`는 최근 내 알림, 내 미완료 업무, 안내/empty/auth/API 실패 상태를 표시한다.
- `/teams/activity/notifications/{notificationId}`는 알림 상세를 표시한다.
- Teams가 기본 tab contentUrl `/teams/activity`를 열면 TeamsJS context의 `subEntityId`를 읽어 상세 route로 이동한다.
- TeamsJS context를 읽지 못하면 전체 알림 목록으로 fallback한다.

## 10. 로그인/권한/returnUrl

- 미로그인 상태에서 상세 route에 접근하면 로그인 안내를 표시한다.
- 로그인 성공 후 기존 URL로 복귀한다.
- 권한 없는 사용자는 "해당 알림에 접근 권한이 없습니다." 안내를 본다.
- 존재하지 않는 알림은 "알림을 찾을 수 없습니다." 안내를 본다.

## 11. TeamsChannel / Mail / Activity 3채널

- TeamsChannel은 Webhook/Adaptive Card 기반 채널 게시다.
- TeamsActivity는 Graph Activity Feed 개인 알림이다.
- Mail은 Gmail SMTP actual provider다.
- 세 채널 모두 `notification_deliveries`에 channel, status, attempt, provider marker, snapshot을 기록한다.

## 12. 관리자 수동 알림 발송 3모드

- 개인 알림
  - TeamsActivity / Mail만 선택한다.
  - TeamsChannel 선택을 강제하지 않는다.
  - 수신자별 `notification_recipients`와 delivery를 생성한다.
- 채널 공지
  - TeamsChannel 게시만 수행한다.
  - `visibility_scope=Authenticated` notification을 생성한다.
  - 개인 수신자 선택을 요구하지 않는다.
- 업무 배정
  - 기존 프로젝트와 담당자를 선택한다.
  - 담당자별 실제 `work_items`를 생성한다.
  - 관련 notification과 recipient를 생성한다.
  - TeamsActivity / Mail 외부 발송은 선택 사항이다.

## 13. 업무 배정 수동 발송과 work_item 생성

- 업무 배정 모드는 실제 업무 생성을 전제로 한다.
- 업무가 아닌 개인 알림/채널 공지는 `work_item`을 생성하지 않는다.
- 생성된 업무는 담당자의 내 업무 목록에 표시된다.
- 알림 상세에서 관련 업무와 프로젝트로 이동할 수 있다.

## 14. queue 처리

- 수동 발송 API는 provider 완료까지 기다리지 않는다.
- 서버는 notification/delivery를 `Pending`으로 저장하고 즉시 응답한다.
- worker가 `Pending` delivery를 처리한다.
- `attempt_count`, `sent_at_utc`, `error_code`, `next_attempt_at_utc`를 delivery에 기록한다.

## 15. 수동/자동 알림 양식

- Mail 제목: `[알림 유형] 제목`
- Mail/TeamsChannel 본문:
  - EMI 프로젝트 통합관리시스템 알림
  - 알림 유형
  - 프로젝트명
  - 제목
  - 내용
  - 발송시각
  - 끝.
- TeamsActivity:
  - `알림 유형, 제목`
  - previewText는 내용 요약 150자 이하
- correlation id는 제목/본문/preview에 표시하지 않는다.

## 16. display snapshot / detail

- delivery row는 표시용 snapshot을 가진다.
- 목록은 제목 중심으로 표시한다.
- 상세는 알림 유형, 프로젝트, 제목, 내용, 발송시각, 채널, 수신자, 상태, 오류/대기 사유, 내부 추적값을 표시한다.
- 수신자가 없는 TeamsChannel row는 `Teams 채널`로 표시하고 `수신자 미등록`으로 표시하지 않는다.

## 17. admin handling / retry / ack / dismiss

- Failed/Pending만 관리자 처리 상태를 가진다.
- Sent row에는 `미처리`를 붙이지 않는다.
- Failed/Pending open 상태만 dashboard count에 포함한다.
- Acknowledged/Dismissed는 dashboard count에서 제외한다.
- Pending retry는 provider 상태를 강제로 바꾸지 않고 다음 시도 시각을 당긴다.

## 18. HTTPS local Teams test

- Teams 앱 local tab은 HTTPS가 필요하다.
- Vite HTTPS dev server 설정과 `/api` proxy를 추가했다.
- `scripts/dev-uat-start-teams-https.sh`는 backend `http://localhost:5081`, frontend `https://localhost:5174` 조합으로 실행한다.
- 인증서/키는 `.certs/`에 두며 repo에 commit하지 않는다.
- localhost manifest는 해당 PC 전용이다. 다른 사용자 검수에는 Dev Tunnel/ngrok/운영 도메인이 필요하다.

## 19. Tests / UAT

확인한 자동 테스트:

- `git diff --check`
- `actionlint .github/workflows/ci.yml`
- backend Release build
- backend 전체 test
- Notification targeted tests
- Migration tests
- frontend lint
- frontend typecheck
- frontend unit test
- frontend build
- mock UI smoke
- Full-Stack E2E
- secret/PII scan

UAT 확인:

- HTTPS UAT health/live, health/ready 200
- `https://localhost:5174/teams/activity` 200
- `https://localhost:5174/teams/activity/notifications/{id}` route 200
- 채널 공지 smoke: notification 생성, delivery 생성, `visibility_scope=Authenticated`, TeamsChannel Sent
- 개인 TeamsActivity smoke: notification/recipient/delivery 연결, TeamsActivity Sent
- 업무 배정 smoke: 실제 work_item 생성, notification `work_item_id`/`project_id` 연결, 담당자 내 업무 DB 반영
- 개인 알림 비수신자 403 확인

## 20. 사용자 검수 체크리스트

- [ ] 채널 공지 알림 상세는 로그인된 사용자면 접근 가능
- [ ] 개인 TeamsActivity 알림 상세는 수신자만 접근 가능
- [ ] 개인 Mail 알림 상세는 수신자만 접근 가능
- [ ] 비수신자는 개인 알림 상세 접근 차단
- [ ] 관리자 수동 발송 시 인앱 알림 생성
- [ ] 알림 상세에서 읽음 처리 가능
- [ ] 업무 배정 수동 발송 시 실제 내 업무 생성
- [ ] 생성된 업무가 수신자의 내 업무에 표시
- [ ] 알림 상세에서 관련 업무 보기 가능
- [ ] 알림 상세에서 관련 프로젝트 보기 가능
- [ ] 수동 발송 화면에서 개인 알림/채널 공지/업무 배정 모드 구분
- [ ] 개인 알림 모드에서 Teams 채널 선택을 강제하지 않음
- [ ] 채널 공지 모드에서 개인 수신자 선택을 강제하지 않음
- [ ] 업무 배정 모드에서 담당자/업무 정보 입력 가능
- [ ] TeamsActivity와 Mail이 notification_id/recipient_id로 추적됨
- [ ] TeamsChannel이 notification_id로 추적됨
- [ ] 로그인 안 된 상태에서 상세 접근 시 로그인 화면 표시
- [ ] 로그인 후 원래 상세로 복귀
- [ ] 권한 없는 사용자는 접근 권한 없음 표시
- [ ] SOP 문서가 작성됨
- [ ] 유저매뉴얼 문서가 작성됨
- [ ] Console 오류 없음
- [ ] 모바일/Teams narrow pane overflow 없음

## 21. 보안 / secret

- `.env`, `.env.notify-local`, `.env.entra-local`은 commit하지 않는다.
- 인증서/키, Teams manifest/icon은 commit하지 않는다.
- SMTP password, Teams Webhook URL, Graph token, Authorization header, ClientSecret은 문서/로그/보고서에 원문 출력하지 않는다.
- 사용자-facing URL에는 token/secret/correlation id를 넣지 않는다.

## 22. 운영 적용 전 체크리스트

- [ ] 운영 Teams manifest `contentUrl`/`websiteUrl`을 운영 HTTPS URL로 변경
- [ ] Teams deep link base URL을 운영 URL로 변경
- [ ] Teams org catalog app id 확인
- [ ] TeamsActivity.Send 권한과 관리자 동의 확인
- [ ] 운영 Webhook URL을 secret으로 주입
- [ ] SMTP 계정/앱 비밀번호를 secret으로 주입
- [ ] Production/Staging dev auth와 AdminUserSwitch 비활성 확인
- [ ] Teams 앱 배포/설치 정책 확인
- [ ] `projectCreated` activityType 추가 여부 결정
- [ ] 사용자별 알림 설정 UI 후속 검토
