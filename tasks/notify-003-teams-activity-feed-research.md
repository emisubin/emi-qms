# TASK-NOTIFY-003 Teams Activity Feed 사전 조사 보고서

조사일: 2026-07-06

## 1. 조사 목적

TASK-NOTIFY-003은 Teams 개인별 실제 알림을 Activity Feed 방식으로 구현하기 전 사전 조사 단계다.

이번 조사 목적은 다음과 같다.

- Microsoft Teams Activity Feed 알림의 공식 요구사항을 확인한다.
- Teams 앱/manifest, Graph 권한, 앱 설치/배포, 사용자 매핑 요구사항을 정리한다.
- 기존 `notifications`, `notification_recipients`, `notification_deliveries`, NOTIFY-002 에스컬레이션 구조와의 연결 방식을 제안한다.
- 실제 구현 전 사용자가 준비해야 할 Microsoft 365/Teams 관리자 작업을 명확히 한다.

이번 문서는 실제 구현, manifest 파일 생성, migration 생성, Graph API 호출, 권한 요청을 수행하지 않는다.

## 2. 현재 알림 시스템 요약

현재 확정된 구조는 다음과 같다.

- 인앱 `notifications`와 `notification_recipients`가 모든 알림의 원본이다.
- 외부 발송은 `notification_deliveries`에 기록한다.
- 외부 발송 실패는 업무 흐름을 중단하지 않는다.
- TASK-NOTIFY-001에서 TeamsChannel Webhook, Gmail SMTP Mail, Daily Digest, retry/dedupe/batch 기반이 구현됐다.
- TASK-NOTIFY-002에서 `work_items.due_date` 기반 L0~L3 에스컬레이션과 `work_item_escalations`가 구현됐다.
- L0/L1/L2의 개인 Teams 알림 의도는 현재 `TeamsDirectMessage` dry-run delivery로 남아 있다.
- TeamsChannel Webhook은 채널 공지/긴급 공유 용도이며, 개인별 업무 알림 채널로 고정하지 않는다.
- Teams Activity Feed actual provider는 아직 없다.

현재 delivery channel:

- `TeamsChannel`
- `TeamsDirectMessage`
- `Mail`

현재 delivery type에는 `WorkItemCreated`, `UrgentBlocking`, `DailyDigest`, `DueSoonL0`, `OverdueL1`, `OverdueL2`, `OverdueL3` 등이 포함되어 있다.

## 3. Teams Activity Feed 개념

Microsoft Teams Activity Feed 알림은 사용자의 Teams 활동 피드와 toast에 앱 기반 알림을 표시하는 Graph 기반 기능이다.

공식 문서 기준으로 Activity Feed는 다음 용도에 적합하다.

- 새 업무 배정 같은 news/update
- 협업 이벤트
- 기한 임박/초과 같은 reminder
- 긴급 조치가 필요한 alert

우리 시스템에서는 다음 용도가 적합하다.

- 내 업무 생성
- 예정일 임박 L0
- 예정일 초과 L1/L2
- 긴급/차단 알림
- 재검사 요청

Teams DM/Bot proactive message와 달리, Activity Feed는 Teams 앱을 알림 출처로 사용하고 사용자가 앱별 알림 설정을 조정할 수 있다. 따라서 개인별 업무 알림에는 Activity Feed를 우선 검토하는 기존 roadmap 결정과 일치한다.

## 4. Microsoft Graph API 요구사항

공식 문서 기준 주요 API는 다음과 같다.

- 개인 사용자 대상: `POST /users/{userId | user-principal-name}/teamwork/sendActivityNotification`
- 여러 사용자 대상: `POST /teamwork/sendActivityNotificationToRecipients`
- 팀/채팅 컨텍스트 대상 API도 있으나, 이번 시스템의 개인 업무 알림은 사용자 대상 API가 우선이다.

개인 사용자 대상 요청 구성:

- `topic`
- `activityType`
- `previewText`
- `templateParameters`
- 선택적으로 `teamsAppId`
- 선택적으로 `chainId`

성공 응답:

- 개인 사용자 대상 API: `204 No Content`
- bulk API: `202 Accepted`

권한:

- Delegated: `TeamsActivity.Send`
- Application: `TeamsActivity.Send.User` 또는 `TeamsActivity.Send`
- 공식 문서상 `TeamsActivity.Send.User`는 resource-specific consent(RSC)를 사용한다.

중요 요구사항:

- Teams 앱 manifest의 `webApplicationInfo`에 Microsoft Entra app id가 있어야 한다.
- 알림 수신자가 Teams 앱을 개인 scope, team, chat 중 하나에 설치하고 있어야 한다.
- templated notification은 manifest `activities.activityTypes` 선언이 필요하다.
- `systemDefault` activity type은 자유 텍스트 테스트에 사용할 수 있으나 public developer preview 제약이 있다. 운영 MVP는 명시 activity type을 선언하는 방식이 더 안전하다.
- `topic.source=text`를 쓰면 `topic.webUrl`이 필요하다.
- `previewText`는 Teams에서 앞 150자 중심으로 표시된다.
- bulk API token은 만료까지 45분 이상 남아야 하며, 그렇지 않으면 `412 Precondition Failed`가 발생할 수 있다.

오류 후보:

- 사용자 미존재 또는 잘못된 user id/UPN
- Teams 앱 미설치
- 권한/consent 부족
- manifest에 activity type 미선언
- topic 형식 오류
- 같은 scope에 같은 Entra app id를 공유하는 Teams 앱 중복 설치
- throttling
- token 만료 임박

참고 공식 문서:

- [Send activity feed notifications to users in Microsoft Teams](https://learn.microsoft.com/en-us/graph/teams-send-activityfeednotifications)
- [userTeamwork: sendActivityNotification](https://learn.microsoft.com/en-us/graph/api/userteamwork-sendactivitynotification?view=graph-rest-1.0)
- [teamwork: sendActivityNotificationToRecipients](https://learn.microsoft.com/en-us/graph/api/teamwork-sendactivitynotificationtorecipients?view=graph-rest-1.0)
- [teamworkActivityTopic resource type](https://learn.microsoft.com/en-us/graph/api/resources/teamworkactivitytopic?view=graph-rest-1.0)
- [Best practices for Teams activity feed notifications](https://learn.microsoft.com/en-us/graph/teams-activity-feed-notifications-best-practices)

## 5. Teams app manifest 요구사항

Teams Activity Feed는 Teams 앱과 연결된다. 단순 Graph POST만으로 완료되지 않는다.

필요 항목:

- Teams 앱 이름: `EMI 프로젝트 통합관리시스템`
- manifest id
- version
- developer 정보
- app display name
- short/long description
- icons
- valid domains
- `webApplicationInfo`
  - `id`: Microsoft Entra app client id
  - `resource`: 앱 리소스 URI
- `activities`
  - activity type
  - description
  - template text
  - optional icon id
- 필요 시 personal app/static tab/deep link target

활동 유형 선언:

- 전통적인 templated notification은 manifest에 activity type을 선언한다.
- `templateParameters`는 manifest activity template의 변수명과 맞아야 한다.
- 예: `workItemCreated` template이 `{actor}님이 {workItemTitle} 업무를 배정했습니다` 형태라면 API 요청의 `templateParameters`에 `workItemTitle`이 필요하다.

앱 설치:

- 수신 사용자가 Teams 앱을 설치해야 Activity Feed 알림을 받을 수 있다.
- 개인 알림 MVP는 personal scope 설치를 우선한다.
- 설치 여부는 Graph `GET /users/{user-id | user-principal-name}/teamwork/installedApps`로 조회할 수 있다.
- 사용자별 앱 설치 자동화는 `POST /users/{user-id | user-principal-name}/teamwork/installedApps`가 있으나 별도 앱 설치 권한이 필요하므로 MVP에서는 수동 설치/관리자 배포를 우선 검토한다.

Teams 앱과 Entra 앱 관계:

- Teams app id와 Microsoft Entra app client id는 다르다.
- manifest `webApplicationInfo.id`는 Entra client id다.
- Graph request의 `teamsAppId`는 Teams 앱 ID이며, 같은 Entra app id를 공유하는 Teams 앱이 여러 개 설치된 경우 disambiguation에 필요하다.
- 공식 문서는 같은 scope에 같은 Entra app id를 공유하는 여러 Teams 앱을 피하라고 권고한다.

참고 공식 문서:

- [Microsoft 365 app manifest schema reference](https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/?view=m365-app-1.29)
- [Upload your app in Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/deploy-and-publish/apps-upload)
- [List apps installed for user](https://learn.microsoft.com/en-us/graph/api/userteamwork-list-installedapps?view=graph-rest-1.0)
- [Install app for user](https://learn.microsoft.com/en-us/graph/api/userteamwork-post-installedapps?view=graph-rest-1.0)

## 6. Graph 권한 / 관리자 동의

권한 선택지는 다음과 같다.

### 선택지 A. 기존 Notifications 앱에 TeamsActivity 권한 추가

장점:

- 기존 Mail/notification 발송 전용 앱 구조와 유사하다.
- 서버 발송용 client credentials 구조를 재사용하기 쉽다.
- Web/API 로그인 앱과 분리되어 권한 책임이 명확하다.

단점:

- Notifications 앱이 Mail optional, Teams Activity까지 함께 담당하게 되어 권한 범위가 커진다.
- Teams 앱 manifest의 `webApplicationInfo.id`와 Notifications 앱 client id 관계를 명확히 맞춰야 한다.

평가:

- 현실적인 MVP 추천안이다.
- 단, Teams 앱 manifest와 Entra app id 관계를 문서화하고, 권한 동의/앱 배포를 같은 앱 기준으로 검수해야 한다.

### 선택지 B. Teams Activity 전용 앱 등록 생성

장점:

- 최소 권한 원칙이 가장 명확하다.
- Teams Activity 권한과 Teams 앱 manifest 연결이 단순하다.
- 후속 감사/회수/교체가 쉽다.

단점:

- 새 앱 등록, secret/certificate 관리, 환경설정이 추가된다.
- 관리자 동의와 배포 준비물이 늘어난다.

평가:

- 운영 장기 구조로 가장 깔끔하다.
- 초기 MVP에서 관리자 준비가 가능하면 이 선택지가 가장 안전하다.

### 선택지 C. 기존 API 앱에 TeamsActivity 권한 추가

장점:

- 서버 API에서 바로 Graph 호출하기 쉽다.

단점:

- API 보호용 앱과 외부 발송 권한이 섞인다.
- 로그인/API 권한과 알림 발송 권한의 책임이 불명확해진다.
- 최소 권한 원칙에 불리하다.

평가:

- 추천하지 않는다.

추천:

- 단기 MVP: 선택지 B가 가능하면 Teams Activity 전용 앱 등록을 생성한다.
- 준비가 어렵다면 선택지 A로 Notifications 앱을 확장한다.
- 선택지 C는 제외한다.

관리자 권한:

- Teams Admin Center에서 custom app 업로드/허용/배포는 Teams Administrator 또는 상위 역할이 필요하다.
- Graph application permission 관리자 동의는 tenant 정책에 따라 Global Administrator, Privileged Role Administrator, Cloud Application Administrator, Application Administrator 등 승인 가능 범위가 달라질 수 있다.
- RSC consent가 필요한 경우 Teams 앱 manifest의 RSC permission 선언과 설치/consent 흐름을 함께 확인해야 한다.

## 7. 사용자 매핑 전략

현재 `qms_users`에는 INFRA-001 이후 다음 필드가 있다.

- `entra_object_id`
- `email`
- `auth_provider`

현재 Entra 로그인 변환 구조는 Microsoft claim의 object id를 `qms_users.entra_object_id`로 저장하고, `preferred_username`/email/UPN 계열 값을 `qms_users.email`에 저장한다.

추천 매핑:

- 1순위: `qms_users.entra_object_id`
- 2순위: Microsoft UPN
- email은 표시/메일 발송 fallback으로만 사용

이유:

- 회사 메일이 Gmail/하이웍스/외부 주소일 수 있어 `qms_users.email`이 Microsoft UPN과 다를 수 있다.
- Graph 사용자 대상 API는 `{userId | user-principal-name}`를 받으므로 Entra object id가 가장 안정적이다.
- dev user는 `auth_provider='Dev'`, `entra_object_id=null`, `email=null` 구조이므로 Activity Feed actual 대상이 될 수 없다.

추가 컬럼 필요성:

- MVP는 `entra_object_id` 우선으로 충분하다.
- 운영에서 Microsoft UPN과 업무 이메일이 분리되는 것이 확정되면 `microsoft_user_principal_name` 또는 `teams_user_id` 컬럼 추가를 검토한다.
- Teams app installation id를 캐시하려면 후속으로 `teams_app_installations` 또는 `qms_user_external_accounts` 같은 별도 테이블을 검토할 수 있다.

Dev mode 정책:

- dev user는 TeamsActivity actual 발송 대상에서 제외한다.
- dev user delivery는 `DryRunSent` 또는 `Suppressed`로 기록한다.
- actual smoke는 EntraId actual user 1~2명으로만 수행한다.

## 8. notification_deliveries 확장안

Teams Activity Feed는 기존 `notification_deliveries` 구조에 자연스럽게 붙일 수 있다.

추천 확장:

- channel 신규 값: `TeamsActivity`
- 기존 `TeamsDirectMessage`는 유지하되 후속으로 deprecated/dry-run 의도 채널로 정리한다.
- NOTIFY-002 L0/L1/L2가 생성하는 `TeamsDirectMessage` dry-run delivery를 구현 단계에서 `TeamsActivity` actual delivery로 전환한다.
- TeamsChannel Webhook fallback은 계속 설정값으로만 허용한다.

필요 migration 후보:

- `notification_deliveries.channel` check constraint에 `TeamsActivity` 추가
- 필요 시 `delivery_type`은 기존 값을 재사용한다.
  - `WorkItemCreated`
  - `UrgentBlocking`
  - `DueSoonL0`
  - `OverdueL1`
  - `OverdueL2`
  - `OverdueL3`
- 신규 failure code는 schema 변경 없이 `error_code`에 저장 가능하다.

failure code 후보:

- `TeamsActivityUserNotFound`
- `TeamsActivityAppNotInstalled`
- `TeamsActivityPermissionDenied`
- `TeamsActivityInvalidActivityType`
- `TeamsActivityInvalidTopic`
- `TeamsActivityThrottled`
- `TeamsActivityTokenExpiring`
- `TeamsActivityUnknownError`

재사용 가능 항목:

- retry 최대 3회
- dedupe key
- `next_attempt_at_utc`
- status transition
- provider message id 또는 Graph request id 저장

주의:

- Graph `204 No Content`는 Graph accepted 의미이며, 사용자가 실제 피드에서 확인했는지는 별도 수동 검수 대상이다.
- Teams 앱 미설치/권한 부족은 재시도해도 해결되지 않는 설정 오류일 수 있으므로 retry 가능/불가 분류가 필요하다.

## 9. activityType 후보

### workItemCreated

- 용도: 내 업무 생성/배정 알림
- 대상 이벤트: Workflow handoff, work item created
- previewText 예시: `새 업무가 배정되었습니다: {workItemTitle}`
- templateParameters:
  - `workItemTitle`
  - `projectTitle`
  - `stageName`
- 딥링크 대상: 프로젝트 상세 또는 내 업무 상세
- 우선순위: 1

### overdue

- 용도: 예정일 초과 L1/L2 개인 알림
- 대상 이벤트: `OverdueL1`, `OverdueL2`
- previewText 예시: `예정일이 지난 업무가 있습니다: {workItemTitle}`
- templateParameters:
  - `workItemTitle`
  - `projectTitle`
  - `dueDate`
  - `level`
- 딥링크 대상: 해당 work item 또는 프로젝트 상세
- 우선순위: 1

### urgentBlocking

- 용도: 긴급/차단 알림
- 대상 이벤트: Blocking/Critical notification
- previewText 예시: `긴급 조치가 필요합니다: {notificationTitle}`
- templateParameters:
  - `notificationTitle`
  - `projectTitle`
  - `severity`
- 딥링크 대상: 관련 프로젝트/알림
- 우선순위: 1

### dueSoon

- 용도: 예정일 직전 영업일 L0 알림
- 대상 이벤트: `DueSoonL0`
- previewText 예시: `내일 예정 업무가 있습니다: {workItemTitle}`
- templateParameters:
  - `workItemTitle`
  - `projectTitle`
  - `dueDate`
- 딥링크 대상: 해당 work item 또는 프로젝트 상세
- 우선순위: 2

### reinspectionRequested

- 용도: 재검사 요청 알림
- 대상 이벤트: 품질/검사 재요청
- previewText 예시: `재검사 요청이 등록되었습니다: {projectTitle}`
- templateParameters:
  - `projectTitle`
  - `inspectionType`
- 딥링크 대상: 품질/검사 화면
- 우선순위: 2

### escalated

- 용도: L2/L3 등 단계 상승 알림을 일반화
- 대상 이벤트: escalation level change
- previewText 예시: `업무 지연 에스컬레이션이 발생했습니다: {projectTitle}`
- templateParameters:
  - `projectTitle`
  - `level`
  - `workItemTitle`
- 딥링크 대상: 관리자 escalation 조회 또는 프로젝트 상세
- 우선순위: 3

### projectCompleted

- 용도: 프로젝트 완료/정산 단계 알림
- 대상 이벤트: project completion
- previewText 예시: `프로젝트가 완료되었습니다: {projectTitle}`
- templateParameters:
  - `projectTitle`
- 딥링크 대상: 프로젝트 상세
- 우선순위: 3

초기 MVP 추천 activityType은 3개 이하로 제한한다.

추천 MVP:

- `workItemCreated`
- `overdue`
- `urgentBlocking`

L0 `dueSoon`은 NOTIFY-002와 직접 연결되지만, MVP 첫 검수에서는 `overdue`보다 긴급도가 낮다. 다만 L0까지 함께 검수하려면 `dueSoon`을 포함하고 `urgentBlocking`을 후순위로 미룰 수 있다.

## 10. 알림 매트릭스 제안

| 알림 유형 | InApp | TeamsChannel | TeamsActivity | Mail |
| --- | --- | --- | --- | --- |
| 내 업무 생성 | 즉시 | 없음 | 개인 알림 | 일일 요약 |
| 참조 알림 | 즉시 | 없음 | 없음 | 일일 요약 |
| 긴급/차단 | 즉시 | 채널 게시 | 개인 알림 | 즉시 |
| 재검사 요청 | 즉시 | 없음 | 개인 알림 | 없음 |
| 예정일 D-1 L0 | 즉시 | 없음 | 개인 알림 | 없음 |
| 예정일 초과 L1 | 즉시 | 선택 fallback | 개인 알림 | 즉시 |
| 예정일 초과 L2 | 즉시 | 선택 fallback | 부담당/생산관리 개인 알림 | 없음 |
| 예정일 초과 L3 | 즉시 | 없음 | 선택 | 생산관리/영업 메일 |
| 프로젝트 완료 | 즉시 | 없음 | 영업 담당 개인 알림 | 증빙 성격 메일 검토 |

정책 보정:

- TeamsChannel은 개인 알림 대체 수단으로 하드코딩하지 않는다.
- L1/L2의 TeamsChannel fallback은 운영 설정값이 true인 경우만 사용한다.
- TeamsActivity actual이 도입되면 NOTIFY-002의 TeamsDirectMessage dry-run 생성 지점을 TeamsActivity delivery 생성으로 전환한다.
- Mail은 L1/L3와 Daily Digest 기준을 유지한다.

## 11. 구현 범위 선택지

### 범위 A. 사전 기반만

포함:

- `TeamsActivity` channel type 후보 문서화
- Graph client interface 설계
- manifest 초안 문서
- dry-run만

장점:

- 관리자 준비가 없어도 구현 가능
- 실패 위험 낮음

단점:

- 사용자 실제 수신 검수가 불가능
- NOTIFY-002 dry-run 상태를 해소하지 못함

### 범위 B. 실제 Activity Feed MVP

포함:

- Teams app manifest 작성
- activityType 1~3개
- Graph TeamsActivity provider
- Entra user actual send
- dev user dry-run/suppressed
- admin test endpoint
- `notification_deliveries` integration
- 앱 설치 여부 확인

장점:

- 개인별 Teams actual 알림을 검수할 수 있음
- NOTIFY-002의 L0/L1/L2 dry-run을 실제 채널로 전환 가능

단점:

- Teams Admin Center, Graph permission, app install 준비가 선행되어야 함
- manifest와 Entra app id 불일치 시 실패 가능성이 큼

### 범위 C. 운영 배포까지

포함:

- 조직 앱 카탈로그 업로드
- 앱 setup/permission policy
- 대상 사용자 전체 배포
- 운영 검수
- 설치 상태 모니터링

장점:

- 운영 적용까지 마무리

단점:

- 관리자/총무/Microsoft 365 운영 협의가 필요
- 구현 TASK 하나로는 범위가 큼

## 12. 추천 구현 전략

추천은 범위 B다.

단, 다음 준비가 완료된 경우에만 actual까지 진행한다.

- Teams 앱 manifest 작성에 필요한 앱 이름/아이콘/도메인 확정
- Teams Admin Center에서 custom app 업로드 또는 테스트 사용자 sideload 허용
- Teams Activity 전용 앱 등록 또는 Notifications 앱 확장 결정
- `TeamsActivity.Send.User` 또는 필요한 Graph 권한 consent 준비
- 테스트 EntraId 사용자 1~2명 준비
- 테스트 사용자에게 앱 설치 완료

준비가 미흡하면 범위 A로 시작해 dry-run/interface/migration만 구현하고 actual은 후속 검수로 남긴다.

구현 방향:

- `NotificationDeliveryChannels.TeamsActivity` 추가
- `ITeamsActivityClient` 추가
- `TeamsActivityChannelHandler` 추가
- `TeamsActivityMessageRenderer` 추가
- Graph token acquisition은 기존 Graph Mail optional 구조와 분리 또는 공통 `GraphClientCredentialsTokenProvider`로 추출
- `qms_users.entra_object_id` 없는 사용자는 actual 제외
- Graph request-id/client-request-id를 `provider_message_id` 또는 error metadata에 저장
- `TeamsDirectMessage`는 당분간 legacy dry-run channel로 유지
- NOTIFY-002 L0/L1/L2 생성 정책은 설정으로 전환한다.
  - `Notifications:TeamsActivity:Enabled`
  - `Notifications:TeamsActivity:DryRun`
  - `Notifications:TeamsActivity:TeamsAppId`
  - `Notifications:TeamsActivity:UseForEscalationPersonalNotifications`

## 13. 사용자 준비물

사용자 또는 Microsoft 365/Teams 관리자가 준비할 항목:

- Teams Admin Center 접근 가능 여부
- custom Teams app 업로드 허용 여부
- 조직 앱 카탈로그 업로드 가능 여부
- Teams 앱 이름: `EMI 프로젝트 통합관리시스템`
- Teams 앱 아이콘 2종
  - color icon
  - outline icon
- 앱 설명 문구
- 앱에서 사용할 공개 HTTPS 도메인
- Teams app manifest 검수/패키징
- Graph 권한 추가 대상 앱 결정
  - 추천: Teams Activity 전용 앱 등록 또는 Notifications 앱
- 관리자 동의
- RSC permission을 사용할 경우 manifest와 consent 흐름 검수
- 테스트 사용자 1~2명
- 테스트 사용자 Entra object id 또는 UPN
- 테스트 사용자에게 Teams 앱 설치
- 실제 Activity Feed 수신 확인 방법

## 14. Microsoft 관리자 요청 문구

아래 문구를 Microsoft 365/Teams 관리자에게 전달할 수 있다.

```
EMI 프로젝트 통합관리시스템에서 Teams 개인별 업무 알림을 Activity Feed 방식으로 검수하려고 합니다.

요청 사항:
1. 조직 내 테스트용 custom Teams app 업로드 또는 테스트 사용자 sideload를 허용해 주세요.
2. Teams 앱 이름은 "EMI 프로젝트 통합관리시스템"입니다.
3. 테스트 사용자 1~2명에게 해당 Teams 앱을 personal scope로 설치할 수 있게 해 주세요.
4. Graph Activity Feed 알림 발송을 위해 TeamsActivity.Send.User 또는 필요한 최소 권한을 검토하고 관리자 동의를 진행해 주세요.
5. 가능하면 Teams Activity 전용 앱 등록을 별도로 생성하고, 해당 client id를 Teams app manifest webApplicationInfo.id와 맞춰 주세요.
6. 테스트 사용자 Entra object id 또는 UPN 확인을 지원해 주세요.

주의:
- Teams 1:1 DM 또는 Bot proactive message가 아니라 Teams Activity Feed 알림 검수입니다.
- 실제 secret/client secret/token은 문서나 메일에 평문 공유하지 않고 별도 secret 전달 절차를 사용합니다.
```

## 15. 예상 migration 후보

예상 migration:

- `0023_teams_activity_delivery_channel.sql`

변경 후보:

- `notification_deliveries.channel` check constraint 확장
  - 기존: `TeamsChannel`, `TeamsDirectMessage`, `Mail`
  - 추가: `TeamsActivity`
- 필요 시 Teams app installation cache table
  - 초기 MVP에서는 생략 가능

추가 테이블 후보:

- `qms_user_teams_app_installations`
  - `user_id`
  - `teams_app_id`
  - `teams_app_installation_id`
  - `checked_at_utc`
  - `status`

초기 MVP에서는 설치 상태를 매번 Graph 조회하거나 admin test endpoint에서 확인하는 방식으로 시작하고, 캐시는 후속 최적화로 둘 수 있다.

## 16. 예상 backend service 후보

후속 구현 후보:

- `TeamsActivityChannelHandler`
- `ITeamsActivityClient`
- `GraphTeamsActivityClient`
- `TeamsActivityNotificationRenderer`
- `TeamsActivityRecipientResolver`
- `TeamsActivityInstallationChecker`
- `TeamsActivityOptions`
- `GraphClientCredentialsTokenProvider`
- `NotificationDeliveryChannels.TeamsActivity`

관리자/테스트 API 후보:

- `POST /api/admin/notification-deliveries/test-teams-activity`
- `GET /api/admin/teams-activity/installations?userId=...`

테스트 API 권한:

- System Administrator only

## 17. 테스트 전략

Backend tests:

- `TeamsActivity` channel value migration
- Graph request payload 생성
- `topic.source=text`일 때 `webUrl` 필수 검증
- `activityType`별 `templateParameters` 생성
- `previewText` 150자 내 요약 또는 길이 제한 정책
- dev user는 actual 제외/dry-run
- Entra user는 `entra_object_id` 우선 사용
- email fallback은 UPN이 확실할 때만 사용
- app not installed 실패 코드 매핑
- permission denied 실패 코드 매핑
- invalid activity type 실패 코드 매핑
- throttling은 retry 대상
- `204 No Content` -> `Sent`
- bulk `202 Accepted` -> `Sent` 또는 accepted marker
- NOTIFY-002 L0/L1/L2가 `TeamsActivity`로 전환되는지
- TeamsChannel fallback이 설정 true일 때만 생성되는지
- non-admin test endpoint 403

UAT/manual:

- 테스트 EntraId 사용자에게 Teams 앱 설치
- admin test endpoint로 Activity Feed 1건 발송
- Teams Activity Feed 실제 표시 확인
- toast 표시 여부 확인
- Teams personal tab 클릭 확인
- `/teams/activity`에서 최근 알림, 내 미완료 업무, 상세 안내 표시 확인
- `notification_deliveries` status 확인
- Graph request-id/client-request-id 저장 확인

CI:

- backend tests
- migration tests
- authorization tests
- secret/PII scan

## 18. 위험 요소

- Teams 앱이 설치되지 않은 사용자에게는 알림이 실패할 수 있다.
- manifest `webApplicationInfo.id`와 Graph token 앱 client id가 다르면 실패하거나 설치 조회가 비정상일 수 있다.
- `systemDefault`는 public developer preview 제약이 있으므로 운영 MVP에는 부적합할 수 있다.
- Activity type template과 request `templateParameters`가 불일치하면 실패한다.
- 회사 메일과 Microsoft UPN이 다르면 email fallback이 잘못된 사용자로 향할 수 있다.
- Teams Admin Center 정책이 custom app 업로드/설치를 막을 수 있다.
- 모바일 딥링크 동작은 desktop과 차이가 있을 수 있으므로 별도 수동 검수가 필요하다.
- 사용자당 20 notifications/minute 초과 시 throttling될 수 있다.
- Activity Feed 보관 기간은 Teams 정책상 30일 기준이다.
- Graph 204/202는 Teams UI 표시 완료를 보장하지 않으므로 수동 검수와 run diagnostics가 필요하다.

## 19. 사용자 결정 필요 항목

구현 전 결정 필요:

1. Graph 권한을 붙일 앱 등록
   - Teams Activity 전용 앱
   - 기존 Notifications 앱
2. Teams 앱 manifest를 이번 TASK에서 생성할지 여부
3. Teams 앱 아이콘과 표시 설명
4. 초기 MVP activityType
   - 추천: `workItemCreated`, `overdue`, `urgentBlocking`
5. `systemDefault`를 테스트에만 사용할지 여부
6. 테스트 사용자 범위
7. Teams 앱 설치 방식
   - 수동 설치
   - Teams Admin Center 배포
   - Graph 설치 API
8. `TeamsDirectMessage` dry-run을 언제 `TeamsActivity`로 전환할지
9. TeamsChannel fallback을 L1/L2에서 계속 옵션으로 둘지
10. Microsoft UPN 별도 컬럼 추가 여부

## 20. 후속 구현 프롬프트 작성 전 체크리스트

후속 구현 프롬프트 전에 확인할 것:

- Teams app manifest 작성 가능 여부
- Teams 앱 package 업로드 가능 여부
- Teams Activity Graph 권한 승인 가능 여부
- 테스트 사용자 personal app 설치 완료 여부
- Entra object id/UPN 확인 완료 여부
- 앱 등록 선택 결정 완료 여부
- 초기 activityType 1~3개 확정
- 앱 딥링크 URL 정책 확정
- actual smoke에서 사용할 테스트 알림 제목/본문 확정
- secret 전달 방식 확정

후속 구현 프롬프트에는 다음 금지사항을 유지해야 한다.

## 23. 2026-07-07 actual 성공 후 표시 UX 보강 메모

Teams Activity Feed actual 발송은 entityUrl topic 방식으로 성공했다. 이후 사용자가 확인한 표시 UX 이슈는 다음과 같다.

- Teams Activity Feed 목록에서 긴 본문이 잘린다.
- 알림 클릭 시 EMI Teams 앱은 열리지만 personal tab content 영역이 빈 화면으로 보인다.

보강 방향:

- Activity Feed 목록에는 `previewText` 150자 이내의 짧은 요약만 보낸다.
- `workItemAssigned`는 manifest의 `{taskName}`과 맞춰 `templateParameters.taskName`을 사용한다.
- 긴 상세 본문은 Teams Activity payload가 아니라 앱 화면에서 보여준다.
- Teams personal tab content route는 `/teams/activity`를 사용한다.
- `/teams/activity`는 Graph Activity Feed를 읽지 않고, EMI 시스템의 기존 알림/내 업무 API를 재사용한다.

테스트 manifest의 personal tab 권장 URL:

```json
"contentUrl": "https://localhost:5174/teams/activity",
"websiteUrl": "https://localhost:5174/teams/activity"
```

운영 배포 전에는 운영 도메인의 `/teams/activity` URL로 교체해야 한다.

- Teams DM/Bot proactive message 구현 금지
- TeamsChannel을 개인 알림 대체로 하드코딩 금지
- secret/token/client secret 출력 금지
- `.env`/appsettings에 실제 secret commit 금지
- UAT DB drop/truncate 금지
- Docker volume 삭제 금지

## 21. 코드 기반 구현 반영

TASK-NOTIFY-003 foundation 단계에서 다음 코드 기반을 추가했다.

- `TeamsActivity` delivery channel 추가
- `0023_teams_activity_delivery_channel.sql` migration 후보 추가
- `TeamsActivityChannelHandler`
- `ITeamsActivityClient`
- `GraphTeamsActivityClient`
- `TeamsActivityNotificationRenderer`
- `POST /api/admin/notification-deliveries/test-teams-activity`
- `Notifications:TeamsActivity:*` 설정 skeleton
- NOTIFY-002 개인 Teams 알림을 `TeamsActivity` delivery로 전환할 수 있는 `TeamsPersonalChannelStrategy` 설정

승인 전 기본 동작:

- Development/UAT skeleton 기준 `Enabled=true`, `DryRun=true`
- 실제 Graph 호출 없음
- dev user dry-run 가능
- actual mode에서 EntraId 사용자와 `entra_object_id`가 필요

승인 후 필요한 값:

- `Notifications__TeamsActivity__TenantId`
- `Notifications__TeamsActivity__ClientId`
- `Notifications__TeamsActivity__ClientSecret`
- `Notifications__TeamsActivity__TeamsAppId`, 권장
- `Notifications__TeamsActivity__TopicWebUrl`
- `Notifications__TeamsActivity__DryRun=false`

actual 검수 전제:

- Teams 앱 manifest가 Teams Admin Center 또는 테스트 사용자에게 설치되어 있어야 한다.
- manifest의 activityTypes와 설정값이 일치해야 한다.
- Graph TeamsActivity 권한과 관리자 동의가 완료되어야 한다.
- 실제 Teams Activity Feed 표시 여부는 Graph 204와 별도로 사용자 수동 확인이 필요하다.

## 22. 2026-07-07 actual smoke 사전 검수 결과

총무팀 승인과 Graph TeamsActivity 권한 승인 이후 foundation branch에서 actual smoke를 수행했다.

- ADMIN-001 merge 이후 main migration 최신 번호가 `0022`가 되었으므로 TeamsActivity migration 후보를 `0023_teams_activity_delivery_channel.sql`로 조정했다.
- `.env.notify-local`의 TeamsActivity actual 필수 key는 모두 configured 상태였다.
- TenantId는 기존 Entra/Microsoft 로그인 tenant와 같은 회사 tenant를 사용한다. 원문 값은 출력하거나 문서화하지 않는다.
- 박수빈 EntraId active 사용자 row를 수신자로 확인했다. email과 Entra object id는 masked 기준으로만 검수했다.
- Graph token 획득과 `sendActivityNotification` actual 호출은 수행됐다.
- localhost 계열 `TopicWebUrl`은 Graph에서 `Invalid 'webUrl'`로 거부된다. `topic.webUrl`은 `https://teams.microsoft.com/l/...` 형식의 Teams deep link가 필요하다.
- Teams deep link로 보정 후 actual smoke는 `TeamsActivityAppNotInstalled`로 실패했다.
- 최종 correlation id: `ED0A0BEB`
- Graph request id: `db0464fd-584f-4265-9fc1-633ff40f2b3b`
- `notification_deliveries`에는 `TeamsActivityAppNotInstalled`가 기록됐다.

원인 분류:

- 코드/Graph 권한/client credential 문제보다는 수신자 Teams 앱 설치 상태 문제로 분류한다.
- Teams 앱 승인과 권한 동의만으로는 충분하지 않고, 수신자에게 Teams 앱이 설치되어 있어야 Activity Feed actual 알림이 성공한다.

후속 actual 성공 검수:

- 박수빈 계정에 EMI 프로젝트 통합관리시스템 Teams 앱을 설치한다.
- 동일 endpoint `POST /api/admin/notification-deliveries/test-teams-activity`를 재시도한다.
- 성공 기준은 Graph 204, `notification_deliveries.status=Sent`, Teams Activity Feed 알림 수신 확인이다.

## 23. 2026-07-07 AppNotInstalled 재진단

박수빈 Teams 계정에 앱이 설치되어 있다는 사용자 확인이 있었고 현재 권한은 `TeamsActivity.Send`이므로, `TeamsActivityAppNotInstalled`를 RSC authorization 누락보다 Graph payload / 사용자 id / Teams app id / topic 방식 정합성 문제 후보로 재분류했다.

확인 결과:

- Graph actual 호출은 token 획득과 `sendActivityNotification` endpoint까지 도달했다.
- Graph 오류 본문은 수신자의 installed applications 안에서 지정한 Teams application을 찾지 못했다는 의미였고, 내부 error code는 `TeamsActivityAppNotInstalled`로 기록됐다.
- 설치 앱 목록 확인을 위해 `GET /users/{userId}/teamwork/installedApps?$expand=teamsAppDefinition`를 client credentials로 조회했으나, 현재 Notifications 앱에는 Teams app installation read 권한이 없어 403으로 차단됐다.
- 따라서 현재 앱 권한만으로는 설치된 앱의 installation id, manifest id, `webApplicationInfo.id`, activity type, version을 확정할 수 없다.

원인 후보:

- manifest `id`와 `Notifications__TeamsActivity__TeamsAppId`가 다르다.
- manifest `webApplicationInfo.id`와 `Notifications__TeamsActivity__ClientId`가 다르다.
- 사용자가 설치한 앱이 이전 manifest version이다.
- 현재 actual 권한은 `TeamsActivity.Send`이므로 `TeamsActivity.Send.User` RSC authorization 누락은 1차 원인으로 보지 않는다.
- `topic.source=text` 방식에서 `teamsAppId` 식별만으로는 설치 앱을 안정적으로 찾지 못해 installed app entity URL topic이 필요하다.
- text topic 방식에서 `topic.webUrl`이 `https://teams.microsoft.com/l/...` Teams deep link가 아니면 Graph가 topic을 거부한다.
- 박수빈 qms user의 Entra object id가 실제 Teams 사용자와 다르다.

구현 보강:

- admin test endpoint에 선택 필드 `installedAppId`를 추가했다.
- `installedAppId`가 있으면 공식 예시에 가까운 `topic.source=entityUrl`과 `users/{userId}/teamwork/installedApps/{installationId}` topic을 사용한다.
- entityUrl topic에서는 설치 앱 URL이 앱을 특정하므로 `teamsAppId`를 생략해 env `TeamsAppId` 불일치 여부와 installed app entity 식별을 분리한다.
- `installedAppId`가 없으면 기존 `topic.source=text` fallback을 유지한다.
- text topic actual 요청에서는 Teams deep link가 아닌 `topic.webUrl`을 Graph 호출 전에 `TeamsActivityInvalidTopic`으로 차단한다.
- Graph 오류 메시지와 provider metadata에는 token, app id, object id 원문을 남기지 않는다.

사용자/관리자 확인 필요:

- Graph Explorer 또는 관리자 권한 도구에서 박수빈 사용자 기준 installedApps 목록을 조회한다.
- EMI 프로젝트 통합관리시스템 앱의 installation id를 확인한다.
- 설치된 manifest id가 TeamsActivity `TeamsAppId` 설정과 같은지 확인한다.
- 설치된 manifest `webApplicationInfo.id`가 TeamsActivity `ClientId` 설정과 같은지 확인한다.
- `workItemAssigned` activity type과 `TeamsActivity.Send.User` RSC authorization 선언이 포함된 최신 manifest로 앱을 재업로드/재설치한다.
- 정합성이 확인되면 admin test endpoint에 `installedAppId`를 넣어 entityUrl topic 방식으로 actual smoke를 재시도한다.

## 24. 2026-07-07 3채널 통합 smoke 결과

TeamsActivity installed app entity URL 방식 actual 성공 이후, 최종 리뷰 전 UAT에서 프로젝트 생성 알림 예시로 TeamsChannel, TeamsActivity, Mail 3채널 actual smoke를 수행했다.

공통 테스트 알림:

- 제목: `[테스트] 프로젝트 생성 알림`
- 프로젝트명: `TASK-NOTIFY-003 통합 알림 테스트`
- 공통 content correlation id: `N003-A96A8613`
- 실제 프로젝트 row 생성 없음
- 모든 채널 메시지에 실제 업무 알림이 아니라는 문구 포함

결과:

| Channel | 대상 | status | delivery id | error_code |
| --- | --- | --- | --- | --- |
| TeamsChannel | 설정된 Teams 테스트 채널 게시, 개인 DM 아님 | Sent | `3c9c3cc2-e280-4491-8122-4facdb9ba9d8` | 없음 |
| TeamsActivity | 박수빈 EntraId 사용자 Activity Feed | Sent | `07da3020-276a-41ba-8a90-9178d1133e0c` | 없음 |
| Mail | `.env.notify-local` TestRecipientEmail 수신자 | Sent | `f9cded9d-e78f-4d67-a4fd-18b8f6735e4d` | 없음 |

확인 사항:

- TeamsActivity는 `topic.source=entityUrl`과 installed app entity URL topic으로 전송했다.
- TeamsActivity previewText는 `프로젝트 생성 알림 테스트`처럼 짧게 유지한다.
- TeamsChannel은 채널 게시 용도이며 개인 DM이 아니다.
- Gmail SMTP Mail provider actual send를 유지한다.
- UAT `/teams/activity`는 HTTP 200이고 최근 알림/내 미완료 업무/안내 문구가 표시된다.
- Teams manifest personal tab `contentUrl`과 `websiteUrl`은 `/teams/activity`를 가리키도록 수동 배포본에서 확인해야 한다.

남은 수동 확인:

- 사용자가 Teams 채널 게시, Activity Feed 표시/클릭, Gmail 수신함을 UI에서 확인한다.
- 운영 배포 전 `contentUrl`과 `websiteUrl`은 운영 도메인의 `/teams/activity`로 교체한다.

## 25. 2026-07-08 알림 표시 UX / 알림발송상태 관리 보강 결정

최종 리뷰 전 사용자 검수에서 Teams Activity 클릭 후 Teams 앱 오른쪽 영역이 빈 화면처럼 보이는 문제와, 실패/대기 delivery를 관리자가 확인 처리할 수 없는 문제가 확인됐다.

결정 사항:

- Teams Activity personal tab route는 `/teams/activity`를 기준으로 한다.
- manifest 수동 배포본의 `contentUrl`과 `websiteUrl`은 테스트에서 `https://localhost:5174/teams/activity`, 운영에서는 운영 도메인의 `/teams/activity`로 설정한다.
- `/teams/activity`는 인증 확인 전, API 실패, 데이터 없음 상태에서도 한글 안내/empty state를 표시한다.
- Teams Activity Feed 목록에는 짧은 previewText만 표시하고, 최근 알림/내 업무 맥락은 `/teams/activity`에서 제공한다.
- 알림발송상태는 발송 상태와 관리자 처리 상태를 분리한다.
- 신규 migration `0024_notification_delivery_admin_handling.sql`로 `notification_deliveries`에 관리자 처리상태 컬럼을 추가한다.
- 관리자 처리상태는 `Open`, `Acknowledged`, `Dismissed`로 제한한다.
- `Failed`와 `Pending` 중 처리상태가 `Open` 또는 null인 건만 대시보드 count에 포함한다.
- 확인/제외 처리된 delivery는 대시보드 실패/대기 count에서 제외하지만, 원래 발송 상태와 delivery row는 보존한다.
- 대기 건 재발송은 `next_attempt_at_utc`를 현재 시각으로 당겨 worker 재처리를 유도하는 범위로 제한한다.
- 실패 건 강제 성공 처리, delivery hard delete, 발송 실패 수동 재처리 UI는 이번 범위에서 제외한다.
- 알림발송상태 페이지는 URL query filter뿐 아니라 내부 탭/선택칸으로 상태를 전환할 수 있어야 한다.
- 에스컬레이션 상태 페이지도 내부 탭으로 Active/L0/L1/L2/L3/Resolved/Cancelled를 전환할 수 있어야 한다.
- 관리자 표는 header/body alignment를 공통 class 기준으로 맞춘다.

관리자 API 보강 후보:

- `GET /api/admin/notification-deliveries?status=&handlingStatus=&channel=&deliveryType=`
- `POST /api/admin/notification-deliveries/acknowledge`
- `POST /api/admin/notification-deliveries/dismiss`
- `POST /api/admin/notification-deliveries/retry`

표시 필드 기준:

- 알림 제목, 메시지 요약, 수신자, 마스킹 이메일, 프로젝트, 업무, 단계, 채널/유형 label, 상태 label, 오류 코드, 오류 메시지, 대기 사유, 관리자 조치 안내, 처리상태, 처리자, 처리시각, 처리 메모를 표시한다.
- ManualTest처럼 notification 원본이 없는 delivery도 fallback title/recipient/project로 추적 가능하게 표시한다.
- secret, token, webhook URL, provider raw response 전체는 표시하지 않는다.

UAT smoke 기준:

- `/teams/activity` HTTP 200, 빈 화면 아님, 최근 알림/내 업무 영역 표시, 모바일 overflow 없음, console error 없음.
- 알림발송상태 페이지는 미처리 실패/미처리 대기 탭, 선택 확인/제외/재발송 버튼, 조치 안내를 표시한다.
- 에스컬레이션 상태 페이지는 L0/L1/L2/L3 내부 탭과 안내 문구를 표시한다.

사용자 검수 체크리스트:

- [ ] Teams 채널에 `[테스트] 프로젝트 생성 알림` 게시 확인
- [ ] Teams Activity Feed에 짧은 preview 알림 표시
- [ ] Teams Activity Feed 알림 클릭 시 EMI 앱 오른쪽 화면이 빈 화면이 아님
- [ ] `/teams/activity`에서 최근 알림 표시
- [ ] `/teams/activity`에서 내 미완료 업무 표시
- [ ] 메일함에 `[테스트] 프로젝트 생성 알림` 수신 확인
- [ ] 알림발송상태 페이지에서 알림 제목 확인 가능
- [ ] 알림발송상태 페이지에서 대상 수신자 확인 가능
- [ ] 알림발송상태 페이지에서 프로젝트/업무 정보 확인 가능
- [ ] 실패 알림에서 오류 코드와 조치 안내 확인 가능
- [ ] 실패 알림 선택 후 확인 처리 가능
- [ ] 확인 처리된 실패 알림은 대시보드 실패 count에서 제외됨
- [ ] 대기 알림에서 다음 시도 시각/대기 사유 확인 가능
- [ ] 대기 알림 선택 후 재발송 가능
- [ ] 대기 알림 선택 후 확인/제외 처리 가능
- [ ] 확인/제외 처리된 대기 알림은 대시보드 대기 count에서 제외됨
- [ ] 알림발송상태 페이지 내부 탭/상태 선택으로 화면 전환 가능
- [ ] 에스컬레이션 페이지 내부 탭/level 선택으로 화면 전환 가능
- [ ] 권한 매트릭스/관리자 표 header와 데이터 정렬이 맞음
- [ ] 조회성 페이지에는 범위 밖 삭제/상태 강제변경 버튼 없음
- [ ] 모바일 overflow 없음
- [ ] Console 오류 없음

## 26. 2026-07-08 알림발송상태 추적 정보 / 관리자 수동 발송 설계 보강

최종 리뷰 전 검수에서 Mail actual row가 실제 수신자에게 도착했는데도 알림발송상태에서 `수신자 미등록`으로 보이는 문제가 확인됐다. TeamsActivity와 Mail ManualTest row도 실제 테스트 의도와 관계없이 `수동 발송`으로만 표시됐다.

원인 판단:

- ManualTest delivery는 notification 원본을 만들지 않는 안전한 테스트 경로라 `notification_id`, `notification_recipient_id`가 null일 수 있다.
- Mail TestRecipientEmail fallback은 provider 발송에는 사용됐지만 delivery row에 표시용 snapshot으로 저장되지 않았다.
- TeamsActivity test recipient도 qms user id는 저장됐지만 ManualTest 제목/프로젝트/correlation 추적 snapshot이 부족했다.
- TeamsChannel은 채널 게시 대상이고 개인 수신자가 아니므로 recipient join이 없는 것을 `수신자 미등록`으로 표시하면 안 된다.

결정:

- 신규 migration `0025_notification_delivery_display_snapshot.sql`을 추가한다.
- `notification_deliveries`에 표시/추적 snapshot을 직접 저장한다.
- snapshot 컬럼은 표시용 title/message/project/work item/recipient/channel target/manual kind/correlation id로 제한한다.
- secret, token, webhook URL, installedAppId, provider raw payload 전체는 저장하지 않는다.
- 신규 수동/테스트 발송부터 snapshot을 정확히 저장하고, 기존 row는 join/fallback으로 가능한 범위에서 표시한다.

snapshot 컬럼:

- `display_title`
- `display_message`
- `display_project_name`
- `display_work_item_title`
- `display_recipient_name`
- `display_recipient_email`
- `display_recipient_kind`
- `display_channel_target`
- `manual_notification_kind`
- `correlation_id`

수신자 표시 우선순위:

- TeamsActivity: qms user 표시명 > snapshot 표시명 > snapshot email 마스킹 > `Activity Feed 수신자`
- Mail: snapshot email 마스킹 > qms user email 마스킹 > TestRecipientEmail snapshot > `메일 수신자 미등록`
- TeamsChannel: snapshot channel target > `Teams 채널`

ManualTest 표시 우선순위:

- `display_title` 우선
- `manual_notification_kind=ProjectCreated`이면 `프로젝트 생성 알림`
- 둘 다 없으면 `수동 발송`

관리자 수동 발송 기능:

- 신규 화면: `/admin/system/send-notification`
- 신규 API: `POST /api/admin/notification-deliveries/send-manual`
- 선택 채널: TeamsChannel, TeamsActivity, Mail
- 수동 발송은 실제 프로젝트 row, workflow event를 생성하지 않는다.
- 각 채널별 delivery row를 만들고, display snapshot과 `correlation_id`를 저장한다.
- 채널별 결과는 개별 성공/실패로 반환한다.
- TeamsActivity는 installedAppId 기반 `entityUrl` topic 방식을 사용한다.
- ProjectCreated 수동 알림은 현재 manifest에 `projectCreated` activityType이 없으면 `workItemAssigned`를 사용한다. 추후 manifest activityType 확장은 별도 검토한다.

후속 검수 기준:

- 관리자 페이지에서 `알림 수동 발송` 메뉴가 보여야 한다.
- 프로젝트 생성 알림을 3채널로 발송할 수 있어야 한다.
- 알림발송상태에서 TeamsChannel/TeamsActivity/Mail row 모두 같은 `correlation_id`로 추적 가능해야 한다.
- Mail row는 실제 수신자 snapshot을 표시해야 하며 `수신자 미등록`으로 보이면 안 된다.
- TeamsChannel row는 `Teams 채널` 또는 채널 target으로 표시해야 한다.

사용자 검수 체크리스트:

- [ ] 관리자 페이지에 `알림 수동 발송` 메뉴가 보임
- [ ] 알림 유형에서 `프로젝트 생성 알림` 선택 가능
- [ ] 제목/프로젝트명/본문 입력 가능
- [ ] Teams 채널 / Teams Activity / Mail 채널 선택 가능
- [ ] Teams Activity 수신자로 박수빈 선택 가능
- [ ] Mail 수신자로 박수빈 또는 테스트 이메일 선택 가능
- [ ] 발송 후 채널별 결과가 표시됨
- [ ] Teams 채널에 프로젝트 생성 알림 게시 확인
- [ ] Teams Activity Feed에 짧은 preview 알림 표시
- [ ] Mail 수신함에 프로젝트 생성 알림 수신 확인
- [ ] 알림발송상태에서 TeamsChannel row가 `프로젝트 생성 알림`으로 보임
- [ ] 알림발송상태에서 TeamsActivity row가 `프로젝트 생성 알림`으로 보임
- [ ] 알림발송상태에서 Mail row가 `프로젝트 생성 알림`으로 보임
- [ ] Mail row에서 수신자가 박수빈 또는 실제 수신 이메일로 표시됨
- [ ] TeamsActivity row에서 수신자가 박수빈으로 표시됨
- [ ] TeamsChannel row에서 대상이 Teams 채널로 표시됨
- [ ] 세 채널 row를 correlation id로 함께 추적 가능
- [ ] 수신자 미등록으로 잘못 표시되지 않음
- [ ] ManualTest가 무조건 수동발송으로만 보이지 않음
- [ ] Console 오류 없음
- [ ] 모바일 overflow 없음

## 28. 2026-07-08 수동 발송 action feedback / 상태 표시 결정

최종 리뷰 전 추가 검수에서 다음 UX 정책을 확정했다.

- 수동 알림 발송은 queue 저장 방식으로 고정한다.
- 따라서 UI에서 동기/비동기 선택 또는 `발송 방식` 칸을 제공하지 않는다.
- 수동 발송 버튼 label은 `발송`으로 한다.
- 버튼 클릭 후 서버에 Pending delivery 저장이 완료될 때까지만 기다리고, 실제 provider 발송 완료는 기다리지 않는다.
- 발송 요청 저장 중/성공/실패 메시지는 발송 버튼 바로 아래 또는 위에 표시한다.
- 성공 시 짧은 안내 후 알림발송상태 화면으로 이동한다.
- 실패 시 이동하지 않고 버튼 근처에 오류를 표시한다.
- 이번 PR에서 추가·수정한 관리자 action 영역은 `ActionFeedback` 패턴으로 버튼 근처에 처리 상태를 표시한다.
- 입력 필드 validation 오류는 기존처럼 해당 입력칸 아래에 표시한다.

알림발송상태 표시 정책:

- `Sent`는 `발송 완료`로만 표시한다.
- `DryRunSent`는 `Dry-run 처리`로만 표시한다.
- `Suppressed`는 `발송 제외`, `Disabled`는 `채널 비활성`으로 표시한다.
- `미처리` handling badge는 `Failed`와 `Pending`에만 표시한다.
- 발송 완료 row가 bulk 확인/제외 action에서 skipped 되더라도 row 상태 label은 `발송 완료`로 유지한다.

후속:

- 이번 PR 범위 밖의 기존 업무 화면 전체 저장/삭제 UX까지 강제 변경하지 않는다.
- 기존 업무 화면의 action feedback 전면 정리는 별도 UX 정리 TASK에서 처리한다.

사용자 검수 체크리스트:

- [ ] 수동 알림 발송 화면에 `발송 방식` 칸이 없음
- [ ] 수동 알림 발송 버튼명이 `발송`으로 표시됨
- [ ] 발송 클릭 시 버튼 근처에 진행 상태가 표시됨
- [ ] 발송 성공 시 버튼 근처에 접수 메시지가 표시됨
- [ ] 발송 성공 후 이전 페이지 또는 알림발송상태 페이지로 이동함
- [ ] 발송 실패 시 버튼 근처에 오류가 표시되고 이동하지 않음
- [ ] correlation id가 발송 화면에 표시되지 않음
- [ ] 메일 제목에 correlation id가 없음
- [ ] 발송 완료 알림 상태가 `발송 완료`로만 표시됨
- [ ] 발송 완료 row에 `미처리`가 붙지 않음
- [ ] 발송 완료 row 확인/제외 시 건너뜀 결과는 나오더라도 상태는 발송 완료로 유지됨
- [ ] 알림발송상태 bulk action 결과가 버튼 근처에 표시됨
- [ ] 사용자/부서/휴일 주요 저장/삭제/복구 action도 버튼 근처에 처리 상태가 표시됨
- [ ] field validation error는 입력칸 아래에 표시됨
- [ ] Console 오류 없음
- [ ] 모바일 overflow 없음

## 29. 2026-07-08 자동 알림 양식 점검 결과

자동 알림 생성/발송 경로를 최종 리뷰 전 다시 점검했다.

점검 대상:

- 긴급/차단 알림
- 업무 생성/업무 배정 알림
- 에스컬레이션 L0/L1/L2/L3
- 일일 업무 요약
- 관리자 수동 발송/ManualTest
- Mail / TeamsChannel / TeamsActivity 렌더러
- `notification_deliveries` display snapshot

확인 및 반영:

- 자동 Mail 제목을 `[알림 유형] 제목` 형식으로 맞춘다.
- 자동 Mail 본문과 TeamsChannel 본문을 수동 발송과 같은 필드 순서로 맞춘다.
- TeamsActivity는 `알림 유형, 제목`과 짧은 내용 요약을 사용한다.
- TeamsActivity previewText는 150자 이하로 제한한다.
- Daily Digest는 공통 헤더/제목 구조를 사용하고, 기존 digest section은 `내용` 영역에 유지한다.
- correlation id는 외부 수신 메시지에 표시하지 않고 내부 추적값으로만 저장한다.
- 자동 delivery도 display snapshot을 저장하여 알림발송상태 목록/상세에서 제목, 유형, 프로젝트, 수신자, 본문을 추적할 수 있게 한다.
- TeamsChannel Adaptive Card에는 raw delivery enum, webhook URL, token, correlation id를 노출하지 않는다.

후속 검토:

- manifest에 `projectCreated` activityType을 추가할지는 운영 알림 유형 확장 시 별도 결정한다.
- 사용자별 채널 preference와 더 큰 알림 설정 UI는 NOTIFY 후속 TASK로 분리한다.
- 실제 실패 강제 재처리 UI는 이번 범위가 아니며, 대기 재시도/확인/제외 처리까지만 포함한다.

사용자 검수 체크리스트:

- [ ] Teams 채널에 알림이 지정 양식으로 표시됨
- [ ] Teams Activity Feed에 짧은 preview가 표시됨
- [ ] Teams Activity 클릭 시 `/teams/activity`가 빈 화면이 아님
- [ ] 메일 제목이 `[알림 유형] 제목` 형식임
- [ ] 메일 본문이 지정 양식과 일치함
- [ ] 자동 알림과 수동 알림 양식이 일치함
- [ ] 알림발송상태 목록에는 제목만 표시됨
- [ ] 알림 상세에서 알림 유형/프로젝트/제목/내용/발송시각 확인 가능
- [ ] 수신자가 정확히 표시됨
- [ ] 수신자 미등록으로 잘못 표시되지 않음
- [ ] 발송 실패 확인 처리 가능
- [ ] 발송 대기 재발송 가능
- [ ] 수동 알림 발송에서 기존 프로젝트/기타 선택 가능
- [ ] Teams Activity 다중 수신 가능
- [ ] Mail 다중 수신 가능
- [ ] 발송 버튼 근처에 처리 상태 표시
- [ ] 발송 완료 row에 미처리 표시 없음
- [ ] Console 오류 없음
- [ ] 모바일 overflow 없음

## 27. 2026-07-08 수동 발송 UX / 상세 추적 구조 결정

최종 리뷰 전 추가 검수에서 다음 결정사항을 확정했다.

- 수동 알림 발송은 기존 프로젝트 선택 또는 `기타` 선택을 지원한다.
- `기타`는 프로젝트 업무가 아닌 운영 공지/테스트 알림을 위한 선택지이며, 실제 프로젝트 row를 생성하지 않는다.
- correlation id는 내부 자동 추적값이므로 발송 화면과 사용자 수신 메시지에는 노출하지 않는다.
- correlation id는 DB에 저장하고 상세 페이지의 내부 추적값 영역에서만 확인한다.
- TeamsActivity와 Mail은 다중 수신자를 지원하되, 추적을 위해 수신자별 delivery row를 생성한다.
- 수동 발송 API는 provider 동기 호출을 하지 않고 `Pending` delivery를 생성한다.
- `NotificationDispatcher` worker가 Pending manual delivery를 처리한다.
- `notification_deliveries`에 `manual_payload_json`, `manual_requested_by_user_id`, `manual_requested_at_utc`를 추가하는 `0026_notification_delivery_manual_payload.sql`을 둔다.

메시지 양식:

- Mail 제목은 `[알림 유형] 제목`이다.
- Mail 본문과 TeamsChannel 본문은 같은 필드 순서를 사용한다.
  - `EMI 프로젝트 통합관리시스템 알림`
  - `알림 유형`
  - `프로젝트명`
  - `제목`
  - `내용`
  - `발송시각`
  - `끝.`
- TeamsActivity는 `알림 유형, 제목`과 짧은 `내용`으로 구성한다.
- ProjectCreated 수동 알림은 manifest에 없는 `projectCreated`를 사용하지 않고 `workItemAssigned` activityType을 사용한다.

알림발송상태 UX:

- 목록의 `알림` 칸은 제목만 표시한다.
- 긴 문장, 전체 내용, correlation id는 목록에 표시하지 않는다.
- 제목 클릭 시 `/admin/system/notification-deliveries/{id}` 상세 페이지로 이동한다.
- 상세 페이지는 구분, 알림 유형, 프로젝트명, 제목, 내용, 발송시각, 채널, 수신자, 상태, 오류/대기 사유, 내부 추적값을 표시한다.

사용자 검수 체크리스트:

- [ ] 수동 알림 발송 화면에서 기존 프로젝트 선택 가능
- [ ] 수동 알림 발송 화면에서 기타 선택 가능
- [ ] correlation id가 발송 화면에 표시되지 않음
- [ ] Teams Activity 수신자 다중 선택 가능
- [ ] Mail 수신자 다중 선택 가능
- [ ] 메일 제목이 `[알림 유형] 제목` 형식임
- [ ] 메일 제목에 correlation id가 없음
- [ ] 메일 본문이 지정 양식과 일치함
- [ ] Teams 채널 게시 본문이 메일 양식과 일치함
- [ ] Teams Activity 알림이 `알림 유형, 제목 / 내용` 형식으로 보임
- [ ] 발송 버튼 클릭 후 오래 기다리지 않고 발송 요청 접수로 전환됨
- [ ] 이전 페이지로 돌아가기 기능이 있음
- [ ] 알림발송상태 목록의 알림 칸에는 제목만 표시됨
- [ ] 알림 row/제목 클릭 시 상세 페이지로 이동
- [ ] 상세 페이지에서 알림 유형/프로젝트명/제목/내용/발송시각 확인 가능
- [ ] 상세 페이지에서 구분이 `관리자 수동 발송`으로 짧게 표시됨
- [ ] 알림발송상태에서 Mail 수신자가 정확히 표시됨
- [ ] 알림발송상태에서 TeamsActivity 수신자가 정확히 표시됨
- [ ] 알림발송상태에서 TeamsChannel 대상이 정확히 표시됨
- [ ] 수신자 미등록으로 잘못 표시되지 않음
- [ ] ManualTest가 무조건 수동발송으로만 보이지 않음
- [ ] Console 오류 없음
- [ ] 모바일 overflow 없음
