# TASK-NOTIFY-001 구현 보고서

## 1. 목적

TASK-NOTIFY-001은 기존 인앱 알림을 모든 알림의 원본으로 유지하면서 Teams/Mail 외부 delivery 계층을 추가하는 작업이다.

핵심 목적은 다음과 같다.

- 인앱 `notifications` / `notification_recipients` 구조 보존
- 외부 발송 이력을 `notification_deliveries`로 분리
- Teams 통합 채널 게시 기반 마련
- 메일 발송 provider 구조 마련
- dry-run/actual 발송을 설정으로 분리
- 외부 발송 실패가 업무 흐름을 중단하지 않도록 처리
- 후속 에스컬레이션과 Activity Feed 개인별 알림의 기반 확보

## 2. 구현 범위

이번 TASK에 포함된 범위는 다음과 같다.

- `notification_deliveries` 신규 테이블
- `NotificationDispatcher`
- `NotificationDeliveryWorker`
- `NotificationDeliveryStore`
- Teams Webhook Channel
- Adaptive Card payload
- Mail provider 구조
  - DryRun
  - Gmail SMTP
  - Microsoft Graph optional
- Daily Digest 07:30 구조
- retry / dedupe / batch 기반
- System Administrator 전용 delivery 조회 API
- System Administrator 전용 test-mail API
- Teams Activity Feed 후속 기획 문서

## 3. 제외 범위

이번 TASK에서 제외한 범위는 다음과 같다.

- Teams Activity Feed 실제 구현
- Teams DM 실제 구현
- Graph Teams activity notification 권한 사용
- Teams 앱 manifest 생성
- NOTIFY-002 예정일 기반 에스컬레이션
- Pending List 구현
- 개인별 알림 설정 UI
- 발송 실패 수동 재처리 UI
- 대시보드형 알림 운영 UI
- Hiworks SMTP 기본 발송 경로
- Microsoft Graph Mail.Send 기본 발송 경로

## 4. 전체 아키텍처

알림 구조는 다음 책임으로 분리된다.

- `notifications`: 인앱 알림 원본
- `notification_recipients`: 인앱 알림 수신자 및 읽음 상태
- `notification_deliveries`: Teams/Mail 외부 발송 이력
- `NotificationDispatcher`: delivery 생성과 due delivery dispatch 조율
- `NotificationDeliveryWorker`: 설정 기반 background dispatch loop
- `INotificationChannelHandler`: 채널별 발송 처리
- `NotificationMessageRenderer`: 외부 채널 메시지 렌더링

외부 채널 발송은 업무 저장 흐름과 분리된다. Teams/Mail 발송 실패는 workflow, 내 업무, 인앱 알림 생성을 중단하지 않는다.

## 5. DB/Migration

신규 migration은 `database/migrations/0016_notification_delivery_channels.sql`이다.

주요 변경:

- `notification_deliveries` 테이블 추가
- `notifications`, `notification_recipients`, `qms_users`, `projects`, `work_items`와 연결 가능한 외래키 구조
- channel/status/delivery_type check constraint
- dedupe와 retry 조회를 위한 인덱스
- notification + recipient + channel + delivery_type 기준 중복 방지

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

상태값:

- `Pending`
- `Sent`
- `Failed`
- `Suppressed`
- `Disabled`
- `DryRunSent`

채널:

- `TeamsChannel`
- `TeamsDirectMessage`
- `Mail`

delivery type:

- `WorkItemCreated`
- `ReferenceDigest`
- `UrgentBlocking`
- `DailyDigest`
- `ProjectCompletion`
- `ManualTest`

기존 `0001~0015` migration은 수정하지 않았다.

## 6. Backend 주요 파일

- `backend/src/Emi.Qms.Api/Program.cs`: notification 서비스와 endpoint 등록
- `backend/src/Emi.Qms.Api/appsettings.json`: notification 기본 disabled/dry-run 설정
- `backend/src/Emi.Qms.Api/appsettings.Development.json`: 개발 환경 notification 설정
- `backend/src/Emi.Qms.Api/Notifications/NotificationOptions.cs`: 설정 바인딩 모델
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryContracts.cs`: channel/status/type 상수와 DTO
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryStore.cs`: delivery 생성, 조회, 상태 전이, digest 후보 조회
- `backend/src/Emi.Qms.Api/Notifications/NotificationDispatcher.cs`: delivery 생성과 발송 orchestration
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryWorker.cs`: background dispatch worker
- `backend/src/Emi.Qms.Api/Notifications/NotificationChannelHandlers.cs`: Teams/Mail channel handler와 메시지 renderer
- `backend/src/Emi.Qms.Api/Notifications/ConfiguredMailClient.cs`: Mail provider router
- `backend/src/Emi.Qms.Api/Notifications/SmtpMailClient.cs`: SMTP actual provider
- `backend/src/Emi.Qms.Api/Notifications/GraphMailClient.cs`: Graph Mail optional provider
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryEndpointExtensions.cs`: admin delivery 조회와 test-mail endpoint
- `backend/tests/Emi.Qms.Api.Tests/NotificationDeliveryTests.cs`: notification delivery 관련 테스트
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`: migration 0016 적용 검증

## 7. Teams Channel 구현

Teams 통합 채널은 Webhook 기반으로 구현했다.

구현 기준:

- `Notifications:Teams:Enabled=true`
- `Notifications:Teams:DryRun=false`
- `Notifications:Teams:WebhookUrl` 설정이 있을 때 actual POST 가능
- Webhook URL은 env/secret으로만 주입
- appsettings와 문서에는 실제 URL을 저장하지 않음
- HTTP payload는 Adaptive Card root JSON
- Content-Type은 `application/json`

Adaptive Card root는 다음 필드를 포함한다.

- `$schema`
- `type=AdaptiveCard`
- `version`
- `body`
- 제목 `TextBlock`
- 본문 `TextBlock`
- 주요 정보 `FactSet`

`Sent`는 Webhook/Power Automate endpoint가 요청을 수락했다는 의미다. 실제 Teams 채널 표시 여부는 Power Automate run history와 Teams 채널 화면으로 확인해야 한다.

Teams actual posting은 사용자가 Teams 채널 표시까지 검수 완료했다고 전달했다.

## 8. Mail 구현

Mail provider는 세 가지 경로를 지원한다.

- `DryRun`: 실제 발송 없이 `DryRunSent` 기록
- `Smtp`: Gmail SMTP actual 발송
- `Graph`: Microsoft Graph sendMail optional provider

초기/UAT/시범운영 actual 메일 발송 경로는 Gmail 전용 계정 SMTP다. 사내 정책상 Hiworks SMTP와 Microsoft Graph Mail.Send는 기본 발송 경로로 사용하지 않기로 결정했다.

Gmail SMTP 구현:

- MailKit 기반 SMTP client
- StartTls / SslOnConnect / None 지원
- SMTP 인증
- sender display name 지원
- HTML/text body 지원
- correlation id subject/body/header 포함
- `x-emi-notification-test-id` header 지원
- 성공 시 `Sent`
- 인증 실패, 연결 실패, sender/recipient reject, rate limit, transient failure를 error code로 분류

Graph provider:

- Notifications 앱 client credentials 구조
- scope는 Graph `.default`
- `sendMail` 202 Accepted 처리
- request-id/client-request-id 기록 가능
- 기본 발송 경로가 아니라 optional provider로 유지

Test-mail endpoint:

- `POST /api/admin/notification-deliveries/test-mail`
- System Administrator 전용
- recipient 생략 시 `Notifications:Mail:TestRecipientEmail` fallback 사용
- 실제 원문 이메일 대신 masked sender/recipient 반환
- correlation id 반환
- provider/status 반환

Gmail SMTP actual smoke는 사용자가 실제 메일 수신까지 검수 완료했다고 전달했다.

## 9. Daily Digest

Daily Digest 구조는 다음 기준으로 구현했다.

- 기본 시각: 07:30
- 기본 timezone: Asia/Seoul
- 수신자별 개인화
- 내용이 없으면 미발송
- 미완료 내 업무
- 최근 생성된 내 업무
- 읽지 않은 참조 알림 요약
- 각 항목에 시스템 딥링크 포함 가능

Pending List는 아직 구현되지 않았으므로 오픈 Pending 항목은 이번 TASK에서 제외했다.

## 10. Retry/Dedupe/Batch

Retry:

- 최대 3회
- 실패 시 `next_attempt_at_utc` 설정
- 최종 실패는 `Failed`
- 외부 발송 실패는 업무 흐름 중단 없음

Dedupe:

- 동일 recipient + channel + delivery type + dedupe key 기준
- 기본 window는 24시간

Batch:

- group key와 batch window 구조를 마련
- 기본 batch window는 120초
- 대량 grouping 고도화는 후속 TASK에서 확장 가능

## 11. 관리자 조회 API

관리자 조회 API:

- `GET /api/admin/notification-deliveries`
- System Administrator 전용
- 서버 policy로 권한 강제

조회 내용:

- channel
- status
- deliveryType
- recipient
- createdAtUtc
- sentAtUtc
- attemptCount
- errorCode
- errorMessage
- providerMessageId 존재 여부

민감정보는 응답과 로그에 원문 노출하지 않는 방향으로 처리한다.

## 12. 설정값

주요 설정 prefix는 `Notifications`다.

Dispatch:

- `Notifications:Dispatch:Enabled`
- `Notifications:Dispatch:WorkerIntervalSeconds`
- `Notifications:Dispatch:RetryCount`
- `Notifications:Dispatch:DedupeWindowHours`
- `Notifications:Dispatch:BatchWindowSeconds`

Daily Digest:

- `Notifications:DailyDigest:Enabled`
- `Notifications:DailyDigest:Time`
- `Notifications:DailyDigest:TimeZone`

Teams:

- `Notifications:Teams:Enabled`
- `Notifications:Teams:DryRun`
- `Notifications:Teams:WebhookUrl`

Mail:

- `Notifications:Mail:Enabled`
- `Notifications:Mail:DryRun`
- `Notifications:Mail:Provider`
- `Notifications:Mail:SenderAddress`
- `Notifications:Mail:SenderDisplayName`
- `Notifications:Mail:TestRecipientEmail`
- `Notifications:Mail:SaveTestMailToSentItems`

SMTP:

- `Notifications:Mail:Smtp:Host`
- `Notifications:Mail:Smtp:Port`
- `Notifications:Mail:Smtp:Security`
- `Notifications:Mail:Smtp:Username`
- `Notifications:Mail:Smtp:Password`
- `Notifications:Mail:Smtp:TimeoutSeconds`

Graph optional:

- `Notifications:Graph:TenantId`
- `Notifications:Graph:ClientId`
- `Notifications:Graph:ClientSecret`

실제 값은 `.env` 또는 배포 secret으로만 주입한다.

## 13. 보안 원칙

- Teams Webhook URL은 secret으로 취급
- Gmail 계정과 앱 비밀번호는 secret으로 취급
- SMTP password 원문 출력 금지
- Graph client secret/token 원문 출력 금지
- Authorization header 원문 출력 금지
- appsettings에는 실제 외부 채널 secret 저장 금지
- Git tracked file에 실제 외부 채널 secret 저장 금지
- 완료 보고와 문서에는 이메일 원문 대신 masked 값만 사용
- 외부 발송 실패는 업무 흐름을 중단하지 않음

## 14. 테스트 결과

실행한 검증:

- `git diff --check`
- `actionlint .github/workflows/ci.yml`
- backend Release build
- backend 전체 test
- Notification targeted tests
- Migration tests
- Authorization tests
- frontend lint
- frontend typecheck
- frontend unit test
- frontend build
- mock UI smoke
- Full-Stack E2E
- seed 격리 A/B/C/D
- UAT DB persistence
- Docker Compose config
- PostgreSQL healthy
- UAT Backend `/health/live`
- UAT Backend `/health/ready`
- UAT Frontend HTTP 200
- secret/PII scan

검증 결과:

- backend 전체 test: 통과
- Notification targeted tests: 통과
- Migration tests: 통과
- Authorization tests: 통과
- frontend lint/typecheck/unit/build: 통과
- mock UI smoke: 통과
- Full-Stack E2E: 통과
- seed A/B/C/D: 통과
- UAT DB persistence: 통과
- Teams actual smoke: backend 기준 `Sent`, 사용자 채널 표시 검수 완료로 전달받음
- Gmail SMTP actual smoke: backend 기준 `Sent`, 사용자 메일 수신 검수 완료로 전달받음
- secret/PII scan: 실제 secret 미포함 확인

## 15. 수동 검수 결과

사용자 검수 완료로 전달받은 항목:

- Teams Webhook actual posting 후 Teams 채널 표시
- Gmail SMTP actual 발송 후 메일 수신

Codex가 확인한 항목:

- UAT backend/frontend health
- PostgreSQL healthy
- `notification_deliveries` row 상태
- Teams delivery `Sent`
- Gmail SMTP delivery `Sent`
- error code 없음
- sent timestamp 존재
- provider marker 존재

## 16. 후속 TASK 연결

- TASK-NOTIFY-002: 예정일 기반 에스컬레이션
- TASK-NOTIFY-003: Teams Activity Feed 개인별 알림
- TASK-ADMIN-001: 알림 설정 UI, delivery 상태 UI, 수동 재처리 UI
- TASK-007A: Pending List 기반 긴급/차단 알림 연결

## 17. 알려진 제한사항

- Teams 통합 채널 게시는 팀원 개인 배너 알림을 보장하지 않는다.
- `Sent`는 provider endpoint 수락 의미이며 실제 사용자 화면 표시/메일함 도착 보장은 provider별로 별도 확인이 필요하다.
- Gmail SMTP는 초기/UAT/시범운영용이다.
- 장기 운영에서는 회사 승인 공식 발송 수단 전환을 검토해야 한다.
- 운영용 Teams Webhook은 UAT/test Webhook과 분리해 재발급해야 한다.
- Teams Activity Feed는 후속 TASK에서 Teams 앱 manifest와 Graph 권한을 포함해 별도 구현한다.

## 18. 운영 적용 전 체크리스트

- 운영용 Teams Webhook 재발급
- Webhook URL secret/env 주입
- Gmail 전용 계정 2단계 인증 활성화
- Gmail 앱 비밀번호 secret 관리
- Gmail 발송량 제한 검토
- 스팸/차단 정책 검토
- 장기 운영 발송 수단 검토
- `Notifications:Dispatch:Enabled` 운영값 확인
- `Notifications:Teams:Enabled` / `DryRun` 운영값 확인
- `Notifications:Mail:Provider` / `DryRun` 운영값 확인
- 관리자 delivery 조회 권한 확인
- Activity Feed 권한과 Teams 앱 manifest 후속 검토
