# TASK-NOTIFY-001: Teams / 메일 알림 채널 확장

## 목적

기존 인앱 알림을 모든 알림의 원본으로 유지하면서, 그 위에 Teams와 메일 발송 이력 및 dispatch 계층을 추가한다. 이번 TASK는 `notification_deliveries`, dispatcher, provider interface, dry-run/fake provider를 먼저 구축하고, 초기 메일 actual 발송은 Gmail 전용 계정 SMTP 기준으로 준비한다.

사내 정책상 Hiworks SMTP와 Microsoft 365 Graph Mail.Send는 기본 발송 경로로 사용하지 않는다. Graph Mail provider는 Exchange Online 조직 또는 후속 선택지로 optional 유지한다.

## 구현 범위

- `notification_deliveries` 신규 테이블 추가
- `NotificationDispatcher` 기반 외부 delivery 생성
- Teams 통합 채널 Webhook provider
- Teams DM dry-run provider 및 확장점
- Mail dry-run provider 및 향후 Graph sendMail 확장점
- Gmail SMTP actual provider
- retry, dedupe, group key, daily digest 기본 구조
- System Administrator 전용 delivery 조회 API
- backend migration/service/API 테스트

## 제외 범위

- 실제 Graph Teams DM 호출
- Hiworks SMTP 기본 발송 경로
- Microsoft 365 Graph Mail.Send 기본 발송 경로
- Teams chat id 조회/생성
- ChatMessage.Send 권한 사용
- Graph Mail.Send 권한 값 저장
- Teams Activity Feed 실제 구현
- Graph TeamsActivity.Send 권한 사용
- Teams 앱 manifest 생성
- 예정일 에스컬레이션 자동화
- Pending List 구현
- 알림 설정 관리자 UI
- 개인별 알림 선호도 UI

## 기존 인앱 알림 원본 유지 원칙

- 기존 `notifications`와 `notification_recipients`는 인앱 알림 원본으로 유지한다.
- 외부 채널 발송 실패는 업무 흐름과 인앱 알림 생성에 영향을 주지 않는다.
- 외부 채널 상태는 `notification_deliveries`에만 기록한다.
- 기존 notification type/status constraint는 이번 TASK에서 대규모 변경하지 않는다.

## notification_deliveries 설계

주요 컬럼:

- `notification_id`
- `notification_recipient_id`
- `recipient_user_id`
- `project_id`
- `work_item_id`
- `channel`
- `delivery_type`
- `status`
- `attempt_count`
- `next_attempt_at_utc`
- `last_attempt_at_utc`
- `sent_at_utc`
- `suppressed_at_utc`
- `error_code`
- `error_message`
- `dedupe_key`
- `group_key`
- `provider_message_id`
- `created_at_utc`
- `updated_at_utc`

채널:

- `TeamsChannel`
- `TeamsDirectMessage`
- `Mail`

상태:

- `Pending`
- `Sent`
- `Failed`
- `Suppressed`
- `Disabled`
- `DryRunSent`

delivery type:

- `WorkItemCreated`
- `ReferenceDigest`
- `UrgentBlocking`
- `DailyDigest`
- `ProjectCompletion`
- `ManualTest`

## Dispatcher 구조

- `NotificationDispatcher`가 delivery 생성과 due delivery 발송을 조율한다.
- `NotificationDeliveryStore`가 DB 조회/생성/상태 전이를 담당한다.
- `NotificationDeliveryWorker`는 설정이 켜진 경우 주기적으로 dispatcher를 실행한다.
- `NotificationMessageRenderer`는 delivery payload를 한글 메시지로 렌더링한다.
- `INotificationChannelHandler`는 channel별 발송 구현을 분리한다.

## Teams channel 전략

- `TeamsChannel`은 Webhook provider 중심으로 구현한다.
- `Notifications:Teams:Enabled=true`, `DryRun=false`, `WebhookUrl` 설정이 모두 충족되면 실제 HTTP POST가 가능하다.
- Webhook URL이 없거나 disabled이면 delivery를 `Disabled` 또는 dry-run 상태로 기록한다.
- Webhook URL은 appsettings에 실제 값으로 저장하지 않는다.

## Mail 전략

- Mail provider는 `DryRun`, `Smtp`, `Graph` 3종을 둔다.
- `Provider=DryRun` 또는 `DryRun=true`이면 실제 발송 없이 `DryRunSent`로 기록한다.
- `Provider=Smtp`이면 Gmail 전용 계정 SMTP로 실제 발송한다.
- `Provider=Graph`는 Microsoft Graph sendMail optional provider로 유지한다.
- 기본 설정은 disabled/dry-run이며, actual 값은 env/secret으로만 주입한다.
- 사용자 email이 없으면 `Suppressed` 처리한다.
- 빈 daily digest는 생성하지 않는다.

### Gmail SMTP 정책

- 초기 알림 메일 발송은 Gmail 전용 계정 SMTP provider 기준이다.
- Gmail 전용 계정은 2단계 인증과 앱 비밀번호를 사용한다.
- Gmail 앱 비밀번호는 `.env.notify-local` 또는 배포 secret에만 저장한다.
- appsettings에는 실제 Gmail 계정, 앱 비밀번호, 수신자 이메일을 저장하지 않는다.
- Gmail은 초기/UAT/시범운영용이며, 장기 운영에서는 회사 승인 공식 발송 수단으로 전환을 검토한다.
- 테스트 발송은 System Administrator 전용 `test-mail` endpoint로 수행한다.
- 테스트 메일에는 correlation id를 제목, 본문, `x-emi-notification-test-id` header에 포함한다.

### Graph Mail optional 정책

- Graph provider는 Exchange Online 조직 또는 후속 선택지로 optional 유지한다.
- Web/API 앱이 아니라 별도 Notifications 앱 client credentials 구조를 사용한다.
- Graph `202 Accepted`는 요청 접수 의미이며 실제 배달 완료 보장은 아니다.

## Dry-run/Fake provider 전략

- 기본 설정은 외부 발송 disabled/dry-run 중심이다.
- 테스트는 fake/dry-run provider로 수행한다.
- dry-run은 실제 Teams/Mail을 보내지 않고 `DryRunSent`로 기록한다.

## retry / dedupe / batch / digest 정책

- retry는 최대 3회다.
- 실패 시 `next_attempt_at_utc`를 설정한다.
- dedupe는 동일 recipient/channel/delivery type/dedupe key 기준 24시간 window를 기본으로 한다.
- batch/grouping window는 120초 기본이며 `group_key`에 기록한다.
- daily digest는 Asia/Seoul 기준 07:30 이후 하루 1회 생성한다.
- Pending List가 아직 없으므로 digest의 오픈 Pending 항목은 후속 TASK로 둔다.

## 설정값

- `Notifications:Dispatch:Enabled`
- `Notifications:Dispatch:WorkerIntervalSeconds`
- `Notifications:Dispatch:RetryCount`
- `Notifications:Dispatch:DedupeWindowHours`
- `Notifications:Dispatch:BatchWindowSeconds`
- `Notifications:DailyDigest:Enabled`
- `Notifications:DailyDigest:Time`
- `Notifications:DailyDigest:TimeZone`
- `Notifications:Teams:Enabled`
- `Notifications:Teams:DryRun`
- `Notifications:Teams:WebhookUrl`
- `Notifications:Mail:Enabled`
- `Notifications:Mail:DryRun`
- `Notifications:Mail:Provider`
- `Notifications:Mail:SenderUserId`
- `Notifications:Mail:SenderAddress`
- `Notifications:Mail:SenderDisplayName`
- `Notifications:Mail:TestRecipientEmail`
- `Notifications:Mail:SaveTestMailToSentItems`
- `Notifications:Mail:Smtp:Host`
- `Notifications:Mail:Smtp:Port`
- `Notifications:Mail:Smtp:Security`
- `Notifications:Mail:Smtp:Username`
- `Notifications:Mail:Smtp:Password`
- `Notifications:Mail:Smtp:TimeoutSeconds`

Gmail SMTP UAT 예시 키:

```text
Notifications__Mail__Provider=Smtp
Notifications__Mail__DryRun=false
Notifications__Mail__SenderAddress=<gmail sender>
Notifications__Mail__SenderDisplayName="EMI 프로젝트 통합관리시스템 알림"
Notifications__Mail__TestRecipientEmail=<test recipient>
Notifications__Mail__Smtp__Host=smtp.gmail.com
Notifications__Mail__Smtp__Port=587
Notifications__Mail__Smtp__Security=StartTls
Notifications__Mail__Smtp__Username=<gmail sender>
Notifications__Mail__Smtp__Password=<gmail app password>
```

실제 값은 문서, appsettings, Git tracked file에 쓰지 않는다.

## 테스트 계획

- migration 0016 적용
- delivery row 생성
- delivery 중복 방지
- dry-run Teams/Mail handler
- provider router: DryRun/Smtp/Graph 선택
- SMTP success/failure handling
- disabled channel 처리
- email 없는 Mail delivery suppressed 처리
- Blocking/Critical 알림 immediate delivery 생성
- Reference/Info 알림 daily digest 후보 처리
- 빈 daily digest 미발송
- daily digest 내용 생성
- retry 3회 및 실패 기록
- System Administrator delivery 조회 가능
- 권한 없는 사용자 delivery 조회 403

## 수동 검수 항목

- Teams Webhook URL 준비 시 실제 통합 채널 게시
- Gmail SMTP 앱 비밀번호 준비 시 실제 메일 수신
- 일일 요약 메일 실제 수신
- 외부 발송 실패 시 업무 흐름이 중단되지 않는지 확인
- Graph Mail.Send actual은 optional provider 선택 시에만 별도 검수

## 후속 TASK 연결

- TASK-NOTIFY-002: 예정일 기반 에스컬레이션
- TASK-NOTIFY-003: Teams Activity Feed 개인별 알림
- TASK-ADMIN-001: 알림 delivery 상태 UI/설정 UI 고도화 가능
- TASK-007A 이후: Pending List 기반 긴급/차단 알림 연결

## 실제 Graph/Teams 권한 준비 필요 항목

- Teams 통합 채널 Webhook URL
- Gmail 전용 계정
- Gmail 2단계 인증
- Gmail 앱 비밀번호
- Graph sendMail 발송 계정, optional provider 사용 시
- Mail.Send 권한 및 관리자 동의, optional provider 사용 시
- Teams Activity Feed용 Teams 앱 manifest와 Graph 권한, 후속 TASK에서 검토
- 운영 secret/env 주입 경로
