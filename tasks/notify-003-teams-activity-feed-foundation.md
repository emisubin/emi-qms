# TASK-NOTIFY-003 Teams Activity Feed 코드 기반 구현 문서

## 1. 목적

Teams Activity Feed 개인별 알림을 실제 Graph 권한 승인 전에도 안전하게 검증할 수 있는 코드 기반을 추가한다.

목표는 다음과 같다.

- `notification_deliveries`에 `TeamsActivity` channel을 추가한다.
- dry-run/fake 동작을 기본으로 유지한다.
- 승인 후 env/secret 값만 주입하면 Microsoft Graph `sendActivityNotification` actual 호출을 수행할 수 있게 한다.
- 기존 TeamsChannel Webhook, Gmail SMTP Mail, Daily Digest, NOTIFY-002 에스컬레이션을 깨뜨리지 않는다.

## 2. 승인 전 구현 범위

포함:

- `TeamsActivity` delivery channel
- Graph Teams Activity client 구조
- Teams Activity renderer
- Teams Activity channel handler
- System Administrator 전용 test endpoint
- dry-run 동작
- EntraId 사용자 매핑 정책
- `TeamsDirectMessage` dry-run legacy 경로 유지
- NOTIFY-002 개인 Teams 알림을 `TeamsActivity`로 전환할 수 있는 설정 기반 구조

제외:

- 실제 Graph TeamsActivity 호출 검수
- Teams 앱 manifest 파일 commit
- Teams 앱 아이콘 commit
- Teams 1:1 DM
- Bot proactive message
- TeamsChannel Webhook 변경
- Gmail SMTP 변경
- due_date 동기화 정책 변경

## 3. 신규 migration

신규 migration:

- `database/migrations/0023_teams_activity_delivery_channel.sql`

내용:

- `notification_deliveries.channel` check constraint에 `TeamsActivity`를 추가한다.
- 기존 `TeamsChannel`, `TeamsDirectMessage`, `Mail` 값은 유지한다.
- 기존 delivery rows를 변경하지 않는다.

## 4. 설정값

설정 경로:

- `Notifications:TeamsActivity`

주요 설정:

- `Enabled`
- `DryRun`
- `AuthorityHost`
- `BaseUrl`
- `Scope`
- `TenantId`
- `ClientId`
- `ClientSecret`
- `TeamsAppId`
- `TopicWebUrl`
- `UseUserPrincipalNameFallback`
- `RequireEntraUser`
- `PersonalChannelStrategy`
- `ActivityTypes`

기본 정책:

- 기본 appsettings는 `Enabled=false`, `DryRun=true`
- Development/UAT skeleton은 `Enabled=true`, `DryRun=true`
- actual 발송은 `DryRun=false`와 Graph 필수 설정이 모두 있을 때만 수행한다.
- 실제 `TenantId`, `ClientId`, `ClientSecret`, `TeamsAppId` 값은 env/secret으로만 주입한다.

## 5. Teams 앱 manifest / activityType 연결

이번 TASK에서 manifest 파일은 repo에 넣지 않는다.

코드에서 사용하는 activityType 기본값:

- `workItemAssigned`
- `deadlineApproaching`
- `deadlineOverdue`
- `urgentPending`
- `dailyDigest`
- `projectCompleted`

MVP actual 검수 우선순위:

1. `workItemAssigned`
2. `deadlineOverdue`
3. `urgentPending`

manifest에 선언되지 않은 activityType은 test endpoint에서 400으로 차단한다.

## 6. Graph 권한

공식 API:

- `POST /users/{userId | user-principal-name}/teamwork/sendActivityNotification`

권한 후보:

- Application: `TeamsActivity.Send.User`
- Application: `TeamsActivity.Send`
- Delegated: `TeamsActivity.Send`

`TeamsActivity.Send.User`는 resource-specific consent(RSC)를 사용한다. 실제 승인과 consent 방식은 총무팀/Microsoft 365 관리자 확인 후 진행한다.

## 7. 사용자 매핑

정책:

- actual 대상은 `auth_provider='EntraId'` 사용자다.
- Graph user id는 `qms_users.entra_object_id`를 우선 사용한다.
- `qms_users.email`은 Microsoft UPN과 다를 수 있으므로 `UseUserPrincipalNameFallback=true`일 때만 fallback으로 사용한다.
- dev user는 actual TeamsActivity 대상이 아니다.
- dry-run에서는 dev user도 payload 검증 대상으로 사용할 수 있다.
- actual 모드에서 Entra object id가 없으면 `Suppressed` 처리한다.

## 8. dry-run / actual 전환

Dry-run:

- 실제 Graph 호출 없음
- handler가 payload를 생성하고 `DryRunSent`를 반환한다.
- 승인 전 UAT 검증 기본 모드다.

Actual:

- `Notifications:TeamsActivity:Enabled=true`
- `Notifications:TeamsActivity:DryRun=false`
- `TenantId`, `ClientId`, `ClientSecret`, `TopicWebUrl` 설정 필요
- `TeamsAppId`는 설정되어 있으면 Graph request에 포함한다.
- 수신 사용자는 EntraId actual user여야 한다.

## 9. sendActivityNotification payload

payload 구조:

- `topic`
  - `source`: `text`
  - `value`: 업무/프로젝트 제목
  - `webUrl`: 시스템 deep link
- `activityType`
- `previewText`
- `templateParameters`
- `teamsAppId`, 설정값이 있으면 포함

`topic.source=text` 사용 시 `webUrl`은 필수다. 상대 link는 `TopicWebUrl` 기준으로 절대 URL로 만든다.

표시 UX 정책:

- Activity Feed 목록의 `previewText`는 150자 이내의 짧은 한글 요약만 사용한다.
- 긴 업무 설명이나 테스트 상세 본문은 Activity Feed 목록에 그대로 넣지 않는다.
- `workItemAssigned`는 manifest template parameter `{taskName}`과 맞추기 위해 `taskName`만 전달한다.
- 상세 안내와 최근 알림/내 업무 맥락은 Teams tab route `/teams/activity`에서 표시한다.

## 10. Admin test endpoint

Endpoint:

- `POST /api/admin/notification-deliveries/test-teams-activity`

권한:

- System Administrator only

요청 후보:

- `recipientUserId`
- `activityType`
- `title`
- `message`
- `linkUrl`

정책:

- `recipientUserId`는 명시하는 것을 권장한다.
- activityType이 설정상 선언된 값이 아니면 400으로 차단한다.
- dev user 대상 dry-run은 가능하다.
- actual 모드에서 dev user는 suppressed된다.
- 응답에는 원문 secret/token 없이 masked recipient와 status를 반환한다.

## 11. NOTIFY-002 연결

기본 동작은 기존과 동일하다.

- L0/L1/L2 개인 Teams 알림은 기존 `TeamsDirectMessage` dry-run delivery를 유지한다.
- `Notifications:Escalation:TeamsPersonalChannelStrategy=TeamsActivity`를 설정하면 `TeamsActivity` delivery로 생성할 수 있다.
- 기본값은 `TeamsDirectMessageDryRun`이다.
- actual 자동 적용은 사용자 승인/검수 후 별도 결정한다.

## 12. 수동 검수 항목

승인 후 확인할 항목:

- Teams 앱 manifest 업로드
- Teams 앱 activityTypes 선언 확인
- 테스트 사용자에게 Teams 앱 설치
- Graph 권한/admin consent 완료
- `.env.notify-local` 또는 secret에 TeamsActivity actual 설정 입력
- `Notifications__TeamsActivity__TopicWebUrl`은 `https://teams.microsoft.com/l/...` 형식의 Teams deep link로 설정
- test endpoint actual 호출
- Teams Activity Feed 수신 확인
- `notification_deliveries.status=Sent` 확인
- 실패 시 `error_code` 확인

## 13. 승인 후 필요한 env 값

실제 값은 commit하지 않는다.

필수:

- `Notifications__TeamsActivity__Enabled=true`
- `Notifications__TeamsActivity__DryRun=false`
- `Notifications__TeamsActivity__TenantId`
- `Notifications__TeamsActivity__ClientId`
- `Notifications__TeamsActivity__ClientSecret`
- `Notifications__TeamsActivity__TopicWebUrl`

권장:

- `Notifications__TeamsActivity__TeamsAppId`
- `Notifications__TeamsActivity__UseUserPrincipalNameFallback=false`
- `Notifications__TeamsActivity__RequireEntraUser=true`

선택:

- `Notifications__Escalation__TeamsPersonalChannelStrategy=TeamsActivity`

## 14. 테스트 계획

자동 테스트:

- migration channel constraint
- dry-run handler
- actual Entra user mapping
- dev user suppressed
- renderer payload
- Graph 204 -> Sent
- Graph 403/404/429 error mapping
- admin test endpoint 권한
- admin test endpoint dry-run
- escalation strategy TeamsActivity 전환

수동 검수:

- 승인 전 dry-run smoke
- 승인 후 actual Graph smoke

## 15. 남은 제한사항

- Teams 앱 manifest와 아이콘은 repo에 포함하지 않는다.
- Teams app installation 확인/자동 설치 API는 이번 foundation에서 구현하지 않는다.
- Graph 204는 Graph 요청 성공 의미이며, Teams UI 표시 완료는 사용자 수동 확인이 필요하다.

## 16. 2026-07-07 actual smoke 결과

총무팀 승인과 Graph TeamsActivity 권한 승인이 완료된 뒤 UAT에서 actual smoke를 수행했다.

- WIP branch를 ADMIN-001 merge 이후 최신 `main` 기준으로 rebase했다.
- ADMIN-001이 `0020~0022` migration을 사용하므로 TeamsActivity migration은 `0023_teams_activity_delivery_channel.sql`로 조정했다.
- `.env.notify-local`의 TeamsActivity 필수 값은 모두 configured 상태로 확인했다.
- `Notifications__TeamsActivity__TenantId`는 기존 Entra tenant 설정과 같은 회사 tenant 값을 사용한다. 원문 값은 문서와 로그에 남기지 않는다.
- UAT latest migration은 `0023_teams_activity_delivery_channel`까지 적용됐다.
- 검수 사용자 A EntraId active 사용자 row를 수신자로 사용했다. 보고에는 qms user id와 masked 정보만 사용한다.
- 최초 actual smoke는 `topic.webUrl`이 localhost 계열 URL이라 Graph HTTP 400 `InvalidTopic`으로 실패했다.
- `topic.webUrl`은 Microsoft Graph 요구에 따라 `https://teams.microsoft.com/l/...` 형식의 Teams deep link여야 한다. ignored `.env.notify-local`의 `TopicWebUrl`은 Teams deep link 형식으로 보정했다.
- 재시도 결과 Graph actual 호출은 수행됐지만, 수신자에게 Teams 앱이 설치되어 있지 않아 `TeamsActivityAppNotInstalled`로 실패했다.
- 최종 smoke correlation id: `ED0A0BEB`
- Graph request id: `db0464fd-584f-4265-9fc1-633ff40f2b3b`
- `notification_deliveries`에는 `channel=TeamsActivity`, `status=Failed`, `attempt_count=1`, `error_code=TeamsActivityAppNotInstalled`로 기록됐다.

다음 actual 성공 검수 전제:

- 검수 사용자 A Teams 클라이언트 또는 Teams Admin Center 정책으로 EMI 프로젝트 통합관리시스템 Teams 앱을 사용자에게 설치한다.
- 설치 후 같은 admin test endpoint를 재실행한다.
- Graph 204가 반환되면 `notification_deliveries.status=Sent`, `sent_at_utc`와 provider request id를 확인한다.
- 사용자는 Teams Activity Feed에서 `TASK-NOTIFY-003 Teams Activity actual test` 알림 수신 여부와 클릭 동작을 확인한다.

## 17. 2026-07-07 AppNotInstalled 진단 보강

사용자가 검수 사용자 A Teams 계정에 앱 설치를 확인했고 현재 권한은 `TeamsActivity.Send`이므로, `TeamsActivityAppNotInstalled`를 단순 미설치나 RSC authorization 누락으로 확정하지 않고 Graph payload / 사용자 id / Teams app id / topic 방식 정합성 문제로 재진단했다.

진단 결과:

- Graph `sendActivityNotification`은 실제 endpoint까지 도달했고, Graph 응답 본문 기준으로 `TeamsActivityAppNotInstalled`로 분류됐다.
- 현재 Notifications 앱 권한으로 `GET /users/{userId}/teamwork/installedApps?$expand=teamsAppDefinition` 조회를 시도했으나, 설치 앱 읽기 권한 부족으로 403이 반환됐다.
- 따라서 현재 앱 토큰만으로는 검수 사용자 A 사용자에게 설치된 Teams 앱의 installation id, manifest id, `webApplicationInfo.id`, manifest version을 확인할 수 없다.
- 현재 actual 검수 권한은 `TeamsActivity.Send`이므로 `TeamsActivity.Send.User` RSC authorization 누락은 1차 원인으로 보지 않는다.
- manifest `id`는 `.env.notify-local`의 `Notifications__TeamsActivity__TeamsAppId`와 일치해야 한다.
- manifest `webApplicationInfo.id`는 `.env.notify-local`의 `Notifications__TeamsActivity__ClientId`와 일치해야 한다.
- `activityTypes`에는 actual smoke에서 사용한 `workItemAssigned`가 선언되어 있어야 한다.
- Microsoft Graph user activity notification 예시는 `topic.source=entityUrl`과 `users/{userId}/teamwork/installedApps/{installationId}` topic을 사용한다. 기존 `topic.source=text` 방식은 fallback으로 유지하되, 설치 앱 식별을 명시하기 위해 installed app entity URL topic을 사용할 수 있도록 foundation을 보강했다.
- text topic 방식의 `topic.webUrl`은 Graph에서 허용하는 `https://teams.microsoft.com/l/...` Teams deep link만 actual 요청에 사용하도록 보강했다. 임의 app URL이나 localhost URL은 Graph 호출 전에 `TeamsActivityInvalidTopic`으로 차단한다.

보강 내용:

- admin test endpoint request에 `installedAppId`를 선택 입력으로 받을 수 있게 했다.
- `installedAppId`가 제공되면 `topic.source=entityUrl`, `topic.value=https://graph.microsoft.com/v1.0/users/{userId}/teamwork/installedApps/{installationId}` payload를 생성한다.
- entityUrl topic은 설치 앱 URL 자체가 앱을 특정하므로 `teamsAppId`를 payload에서 생략한다. 이렇게 해야 env `TeamsAppId` 불일치 가능성과 installed app entity 식별 검증을 분리할 수 있다.
- `installedAppId`가 없으면 기존 `topic.source=text`와 Teams deep link `topic.webUrl` 방식을 유지한다.
- Graph 오류 메시지는 token, object id, app id 원문 없이 내부 error code와 관리자용 한글 메시지로만 저장한다. `TeamsActivityAppNotInstalled` 메시지는 앱 미설치뿐 아니라 TeamsAppId 불일치 또는 설치된 manifest 버전 불일치 가능성을 안내한다.

actual smoke 재시도 전제:

- Graph installedApps 조회 또는 Teams Admin Center/Graph Explorer로 검수 사용자 A 사용자에게 설치된 EMI 프로젝트 통합관리시스템 앱의 installation id를 확인한다.
- 설치된 manifest id와 `Notifications__TeamsActivity__TeamsAppId`가 일치하는지 확인한다.
- 설치된 manifest `webApplicationInfo.id`와 `Notifications__TeamsActivity__ClientId`가 일치하는지 확인한다.
- `activityTypes.workItemAssigned`와 RSC `TeamsActivity.Send.User` 선언/승인이 확인된다.
- 위 조건이 확인되면 admin test endpoint에 `installedAppId`를 포함해 entityUrl topic 방식으로 actual smoke를 1회 재시도한다.

## 18. 2026-07-07 actual 성공 후 표시 UX 보강

entityUrl topic 방식 actual smoke가 성공한 뒤 Teams Activity Feed 표시 UX를 보강했다.

- Teams Activity `previewText`는 activityType별 짧은 요약으로 생성한다.
- manual actual test는 `UAT Teams Activity 실제 발송 테스트`처럼 짧은 문구만 표시한다.
- 긴 상세 본문은 Activity Feed 목록이 아니라 Teams tab 화면에서 확인하도록 분리한다.
- Frontend에 Teams personal tab 전용 route `/teams/activity`를 추가했다.
- `/teams/activity`는 기존 `GET /api/notifications`, `GET /api/my-work`, `GET /api/my-work/summary`를 재사용해 최근 내 알림, 내 미완료 업무, 상세 안내를 표시한다.
- Teams 앱 manifest 파일과 아이콘은 repo에 포함하지 않는다.

manifest personal tab 권장값:

```json
"contentUrl": "https://localhost:5174/teams/activity",
"websiteUrl": "https://localhost:5174/teams/activity"
```

운영 전에는 위 URL을 운영 도메인의 `/teams/activity`로 교체해야 한다.

## 19. 2026-07-07 3채널 통합 smoke 결과

최종 리뷰 전 UAT에서 프로젝트 생성 알림 예시를 사용해 TeamsChannel, TeamsActivity, Mail 3채널 actual smoke를 수행했다.

공통 테스트 알림:

- 제목: `[테스트] 프로젝트 생성 알림`
- 프로젝트명: `TASK-NOTIFY-003 통합 알림 테스트`
- 내용: `EMI 프로젝트 통합관리시스템 프로젝트 생성 알림 3채널 최종 검수입니다. 실제 업무 알림이 아닙니다.`
- 공통 content correlation id: `N003-A96A8613`
- 실제 프로젝트 row는 생성하지 않았다.

UAT runtime 주의사항:

- `.env.notify-local`은 ignored 파일이며 원문 값을 문서에 남기지 않는다.
- `scripts/dev-uat-start.sh`는 기본적으로 `.env`만 source하므로 TeamsActivity/Mail/TeamsChannel actual 검수 전에는 `.env.notify-local` 값을 안전하게 export한 상태에서 UAT backend를 시작해야 한다.
- `.env.notify-local`은 공백이 포함된 값이 있을 수 있으므로 단순 shell `source` 대신 quoted export 방식으로 주입했다.

Smoke 결과:

| Channel | 대상 | delivery_type | status | delivery id | error_code |
| --- | --- | --- | --- | --- | --- |
| TeamsChannel | 설정된 Teams 테스트 채널 게시, 개인 DM 아님 | UrgentBlocking | Sent | `3c9c3cc2-e280-4491-8122-4facdb9ba9d8` | 없음 |
| TeamsActivity | 검수 사용자 A EntraId 사용자 Activity Feed | ManualTest | Sent | `07da3020-276a-41ba-8a90-9178d1133e0c` | 없음 |
| Mail | `.env.notify-local` TestRecipientEmail 수신자 | ManualTest | Sent | `f9cded9d-e78f-4d67-a4fd-18b8f6735e4d` | 없음 |

검증 결과:

- 세 delivery 모두 `attempt_count=1`, `sent_at_utc` 있음, `provider_message_id` 있음으로 확인했다.
- TeamsActivity는 installed app entity URL topic 방식으로 Graph 204 성공 후 `Sent` 처리됐다.
- TeamsActivity Activity Feed 목록에는 짧은 previewText `프로젝트 생성 알림 테스트`를 사용한다.
- TeamsChannel은 기존 Webhook handler를 통해 실제 채널 게시로 처리됐다.
- Mail은 Gmail SMTP provider actual send로 처리됐다.
- UAT frontend `/teams/activity`는 HTTP 200이며 desktop/mobile browser smoke에서 최근 알림, 내 미완료 업무, 안내 문구가 표시되고 page-level horizontal overflow와 console error가 없었다.

사용자 수동 확인 항목:

- Teams 테스트 채널에서 `[테스트] 프로젝트 생성 알림` 게시와 correlation id를 확인한다.
- Teams Activity Feed에서 프로젝트 생성 알림 테스트 문구를 확인한다.
- Activity Feed 알림 클릭 시 Teams personal tab contentUrl이 `/teams/activity`를 가리켜 오른쪽 앱 영역이 빈 화면이 아닌지 확인한다.
- Gmail 수신함에서 테스트 메일 제목/본문과 correlation id를 확인한다.

최종 리뷰 진입 판단:

- TeamsChannel actual smoke 성공
- TeamsActivity actual smoke 성공
- Mail actual smoke 성공
- notification_deliveries 상태 정상
- secret/token/manifest/icon repo 포함 없음
- migration 번호는 `0023_teams_activity_delivery_channel.sql` 유지
- 사용자 Teams/Gmail UI 수동 확인만 남아 있으며 P0/P1/P2 blocker는 발견하지 못했다.

## 20. 2026-07-08 최종 리뷰 전 알림 표시 UX / 발송 상태 관리 보강

사용자 검수에서 Teams Activity 클릭 후 Teams 앱 오른쪽 영역이 빈 화면처럼 보이는 문제와, 알림발송상태 페이지에서 실패/대기 건을 추적하고 정리할 수 없는 문제가 확인되어 최종 리뷰 전 보강했다.

Teams tab 보강:

- `/teams/activity`는 인증 확인 전, API 실패, 빈 데이터 상태에서도 항상 한글 안내/empty state를 렌더링한다.
- Teams iframe 또는 root `/` 진입이 감지되면 Teams Activity 전용 화면으로 안내한다.
- 최근 내 알림, 내 미완료 업무, 상세 안내, 시스템 홈 이동 버튼을 표시한다.
- desktop/mobile smoke에서 HTTP 200, 빈 화면 아님, page-level horizontal overflow 없음, console error 없음으로 확인했다.

Teams manifest 수동 배포 권장값:

```json
"contentUrl": "https://localhost:5174/teams/activity",
"websiteUrl": "https://localhost:5174/teams/activity"
```

운영 배포 전에는 위 값을 운영 도메인의 `/teams/activity`로 교체한다. manifest와 icon 파일은 repo에 포함하지 않는다.

알림발송상태 관리 보강:

- 신규 migration `0024_notification_delivery_admin_handling.sql`을 추가했다.
- `notification_deliveries`에 관리자 처리상태 컬럼을 추가했다.
  - `admin_handling_status`: `Open`, `Acknowledged`, `Dismissed`
  - `admin_handled_at_utc`
  - `admin_handled_by_user_id`
  - `admin_handling_note`
- 실제 발송 상태(`Pending`, `Sent`, `Failed` 등)는 그대로 유지하고, 관리자 확인/제외 상태를 분리한다.
- 대시보드 실패 count는 `status=Failed`이면서 처리상태가 `Open` 또는 null인 건만 집계한다.
- 대시보드 대기 count는 `status=Pending`이면서 처리상태가 `Open` 또는 null인 건만 집계한다.
- 확인/제외 처리된 실패·대기 건은 대시보드 count에서 제외되지만 delivery row를 삭제하거나 발송 성공으로 바꾸지 않는다.

관리자 action:

- `POST /api/admin/notification-deliveries/acknowledge`
- `POST /api/admin/notification-deliveries/dismiss`
- `POST /api/admin/notification-deliveries/retry`

정책:

- 실패 건 확인/제외는 발송 상태를 변경하지 않는다.
- 대기 건 재발송은 `next_attempt_at_utc`를 현재 시각으로 당겨 worker가 다음 주기에 처리하도록 한다.
- retry endpoint는 `attempt_count`를 직접 증가시키지 않는다.
- `GetDueDeliveriesAsync`는 확인/제외 처리된 delivery를 worker 대상에서 제외해 관리자가 정리한 건이 계속 재시도되지 않게 한다.
- delivery row hard delete, 실패 건 강제 성공 처리, 수동 재처리 UI는 구현하지 않는다.

표시 보강:

- 알림 제목, 메시지 요약, 수신자 표시명/마스킹 이메일, 프로젝트, 업무, 단계, 채널/유형 한글 label, 상태 label, 오류 코드, 오류 메시지, 대기 사유, 관리자 조치 안내를 내려준다.
- notification 원본이 없는 ManualTest row는 `[테스트] 수동 발송`, `수신자 미등록`, `프로젝트 없음` 같은 fallback으로 표시한다.
- 오류 코드별 한글 조치 안내를 제공한다.
- Pending row에는 worker 대기, 재시도 대기, 채널 비활성 등 가능한 대기 사유를 표시한다.
- raw secret, token, webhook URL, provider raw response 전체는 표시하지 않는다.

화면 보강:

- 알림발송상태 페이지에 내부 탭을 추가했다.
  - 전체
  - 미처리 실패
  - 미처리 대기
  - 발송 완료
  - 확인됨
  - 제외됨
  - Dry-run/비활성/제외
- 채널 선택과 delivery type 선택을 추가했다.
- dashboard link의 query filter는 유지하되, 페이지 내부에서 같은 화면 안에서 전환할 수 있다.
- 실패/대기 row 선택 체크박스와 선택 확인 처리, 선택 제외 처리, 선택 재발송 버튼을 추가했다.
- 에스컬레이션 상태 페이지에 내부 탭을 추가했다.
  - 전체
  - 진행 중
  - L0 예정일 임박
  - L1 예정일 초과
  - L2 +2영업일
  - L3 +3영업일
  - 해소됨
  - 취소됨
- 관리자 표는 공통 정렬 class를 사용해 header/body alignment를 맞췄다.

UAT 확인:

- UAT latest migration은 `0024_notification_delivery_admin_handling`까지 적용됐다.
- `/teams/activity`: HTTP 200, 빈 화면 아님, 최근 알림/내 미완료 업무 영역 표시, 모바일 overflow 없음, console error 없음.
- `/admin/system/notification-deliveries?status=Failed&handlingStatus=Open`: HTTP/API 200, 내부 탭/선택 action/조치 안내 표시, target-not-found 없음.
- `/admin/system/work-item-escalations?status=Active&level=L0`: HTTP/API 200, 내부 탭/안내 표시, target-not-found 없음.

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

## 21. 2026-07-08 알림발송상태 추적 정보 / 관리자 수동 발송 보강

사용자 검수에서 Mail actual 발송이 정상 도착했는데도 알림발송상태에서 `수신자 미등록`으로 보이고, TeamsActivity/Mail ManualTest row가 `수동 발송`으로만 표시되는 문제가 확인됐다.

원인:

- 기존 `test-mail` / `test-teams-activity` / TeamsChannel 테스트 경로는 provider에는 실제 수신자와 제목을 넘겼지만, `notification_deliveries`에는 표시용 수신자/제목 snapshot을 저장하지 않았다.
- ManualTest delivery는 notification 원본이 없는 경우가 많아 `recipient_user_id`, `notification_recipient_id`, `notification_id`가 null일 수 있다.
- 관리자 목록 UI는 원본 notification 또는 recipient join 결과가 없으면 `수신자 미등록` / `수동 발송` fallback을 표시했다.

DB/Migration:

- 신규 migration `0025_notification_delivery_display_snapshot.sql`을 추가했다.
- `notification_deliveries`에 표시/추적용 snapshot 컬럼을 추가했다.
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
- snapshot에는 실제 발송 추적에 필요한 제목/수신자/채널 대상/correlation id만 저장하고, secret, token, webhook URL, provider raw payload는 저장하지 않는다.

표시 정책:

- `display_title`이 있으면 알림 제목으로 우선 표시한다.
- `manual_notification_kind=ProjectCreated`는 `프로젝트 생성 알림`으로 표시한다.
- ManualTest라도 `manual_notification_kind` 또는 `display_title`이 있으면 `수동 발송`으로만 표시하지 않는다.
- Mail은 실제 발송 대상 email을 `display_recipient_email`에 저장하고 UI에는 마스킹된 수신자를 표시한다.
- TeamsActivity는 qms user 표시명/email snapshot을 저장해 Activity Feed 수신자를 추적한다.
- TeamsChannel은 개인 수신자가 아니므로 `수신자 미등록`이 아니라 `Teams 채널` 또는 채널 target을 표시한다.
- 기존 row는 snapshot이 없을 수 있으므로 가능한 join/fallback으로 표시하고, 신규 수동/테스트 발송 row부터 정확한 추적 정보가 저장된다.

관리자 수동 알림 발송:

- 신규 route: `/admin/system/send-notification`
- 신규 API: `POST /api/admin/notification-deliveries/send-manual`
- 관리자 메뉴 위치: 관리자 > 운영 > 알림 수동 발송
- 입력 항목:
  - 알림 유형: 프로젝트 생성 알림, 업무 배정 알림, 긴급 알림, 일반 공지
  - 제목
  - 프로젝트명
  - 본문
  - 채널 선택: Teams 채널, Teams Activity, Mail
  - Teams Activity 수신자: active EntraId 사용자
  - Mail 수신자: 사용자 선택 또는 이메일 직접 입력, 없으면 TestRecipientEmail fallback
  - correlation id
- 실제 프로젝트 row 또는 workflow event는 생성하지 않는다.
- 채널별 delivery row를 생성하고 snapshot/correlation id를 저장한다.
- 채널별 성공/실패를 개별 결과로 반환하며, 한 채널 실패가 전체 API 500이 되지 않게 한다.

TeamsActivity 수동 발송 정책:

- installedAppId 기반 `entityUrl` topic 방식을 유지한다.
- ProjectCreated 수동 알림은 현재 manifest에 `projectCreated` activityType이 없을 수 있으므로 MVP에서는 manifest에 선언된 `workItemAssigned`를 사용한다.
- 추후 manifest에 `projectCreated` activityType을 추가할지 별도 검토한다.

검수 기준:

- 3채널 수동 발송 smoke는 `notificationKind=ProjectCreated`, 제목 `[테스트] 프로젝트 생성 알림`, 프로젝트명 `TASK-NOTIFY-003 통합 알림 테스트`로 수행한다.
- 알림발송상태에서 TeamsChannel/TeamsActivity/Mail row 모두 `프로젝트 생성 알림` 또는 display title로 추적 가능해야 한다.
- Mail row는 `수신자 미등록`이 아니라 실제 수신자 snapshot 기반 마스킹 이메일 또는 사용자명으로 표시되어야 한다.
- 세 채널 row는 같은 `correlation_id`로 묶어 확인할 수 있어야 한다.

사용자 검수 체크리스트:

- [ ] 관리자 페이지에 `알림 수동 발송` 메뉴가 보임
- [ ] 알림 유형에서 `프로젝트 생성 알림` 선택 가능
- [ ] 제목/프로젝트명/본문 입력 가능
- [ ] Teams 채널 / Teams Activity / Mail 채널 선택 가능
- [ ] Teams Activity 수신자로 검수 사용자 A 선택 가능
- [ ] Mail 수신자로 검수 사용자 A 또는 테스트 이메일 선택 가능
- [ ] 발송 후 채널별 결과가 표시됨
- [ ] Teams 채널에 프로젝트 생성 알림 게시 확인
- [ ] Teams Activity Feed에 짧은 preview 알림 표시
- [ ] Mail 수신함에 프로젝트 생성 알림 수신 확인
- [ ] 알림발송상태에서 TeamsChannel row가 `프로젝트 생성 알림`으로 보임
- [ ] 알림발송상태에서 TeamsActivity row가 `프로젝트 생성 알림`으로 보임
- [ ] 알림발송상태에서 Mail row가 `프로젝트 생성 알림`으로 보임
- [ ] Mail row에서 수신자가 검수 사용자 A 또는 실제 수신 이메일로 표시됨
- [ ] TeamsActivity row에서 수신자가 검수 사용자 A으로 표시됨
- [ ] TeamsChannel row에서 대상이 Teams 채널로 표시됨
- [ ] 세 채널 row를 correlation id로 함께 추적 가능
- [ ] 수신자 미등록으로 잘못 표시되지 않음
- [ ] ManualTest가 무조건 수동발송으로만 보이지 않음
- [ ] Console 오류 없음
- [ ] 모바일 overflow 없음

## 25. 2026-07-08 Teams 앱 로컬 HTTPS 실행 설정

Teams 앱 manifest v1.0.1은 personal tab `contentUrl` / `websiteUrl`을 `https://localhost:5174/teams/activity`로 사용한다. Teams 클라이언트 안에서 오른쪽 앱 영역을 정상 표시하려면 로컬 frontend도 HTTPS로 실행되어야 한다.

반영 사항:

- Vite dev server는 기본 HTTP 동작을 유지한다.
- `VITE_DEV_HTTPS=true`일 때만 로컬 인증서/키를 읽어 HTTPS dev server로 실행한다.
- 기본 인증서 경로는 `.certs/localhost.pem`, `.certs/localhost-key.pem`이다.
- 인증서/키는 `.gitignore` 대상이며 repo에 commit하지 않는다.
- HTTPS frontend에서는 API를 absolute `http://localhost:5081`로 직접 호출하지 않고, same-origin `/api`, `/health` 요청을 Vite proxy가 backend `http://127.0.0.1:5081`로 전달한다.
- `scripts/dev-uat-start-teams-https.sh`는 Teams 로컬 검수 전용 wrapper이다.
- 기존 `scripts/dev-uat-start.sh` 기본 실행은 HTTP `http://127.0.0.1:5174`를 유지한다.
- UAT DB drop/truncate 및 Docker volume 삭제는 수행하지 않는다.

로컬 인증서 생성 예:

```bash
brew install mkcert
mkcert -install
mkdir -p .certs
mkcert -key-file .certs/localhost-key.pem -cert-file .certs/localhost.pem localhost 127.0.0.1 ::1
```

Teams HTTPS UAT 실행:

```bash
scripts/dev-uat-start-teams-https.sh
```

검수 URL:

- `https://localhost:5174/teams/activity`

운영 전 주의:

- manifest의 `contentUrl` / `websiteUrl`은 운영 배포 도메인으로 교체해야 한다.
- Teams manifest JSON과 icon 파일은 repo에 추가하지 않는다.

사용자 검수 체크리스트:

- [ ] `https://localhost:5174/teams/activity`가 SSL 오류 없이 열림
- [ ] Teams 앱 오른쪽 화면이 빈 화면이 아님
- [ ] 최근 알림 영역이 표시됨
- [ ] 내 미완료 업무 영역이 표시됨
- [ ] HTTPS frontend에서 API mixed content 오류가 없음
- [ ] 기존 `http://localhost:5174` UAT 실행이 유지됨
- [ ] 인증서/키 파일이 repo에 포함되지 않음

## 23. 2026-07-08 수동 발송 action feedback / 상태 표시 보강

최종 리뷰 전 추가 검수에서 수동 발송 화면의 불필요한 `발송 방식` 칸과, action 결과가 화면 상단에만 표시되는 문제가 확인됐다.

반영 결정:

- 수동 알림 발송은 queue 방식으로 고정하므로 UI에서 `발송 방식` 입력/표시 칸을 제거한다.
- 수동 발송 버튼명은 `발송`으로 단순화한다.
- 발송 버튼 클릭 시 provider actual 완료를 기다리지 않고 서버에 Pending delivery 저장까지만 기다린다.
- 발송 요청 저장 중/성공/실패 상태는 발송 버튼 바로 아래에 표시한다.
- 발송 요청 성공 후에는 짧은 안내를 보여준 뒤 알림발송상태 화면으로 이동한다.
- 사용자/부서/휴일/알림발송상태 등 이번 PR의 관리자 action 영역은 `ActionFeedback` 방식으로 버튼 근처에 처리 상태를 표시한다.
- 기존 입력값 검증 오류는 계속 입력칸 아래에 표시한다.
- Sent/DryRunSent/Suppressed/Disabled 같은 완료/종결 상태 row에는 `미처리` handling badge를 붙이지 않는다.
- Failed/Pending row에만 처리상태 `미처리/확인됨/제외됨`을 표시한다.
- 발송 완료 row를 bulk 확인/제외 처리 대상으로 선택해도 skip 결과는 허용하지만 row 상태는 `발송 완료`로 유지한다.

후속 적용 필요:

- 이번 PR 밖의 기존 업무 입력 화면 전체까지 action feedback을 일괄 개편하지는 않았다.
- 운영 적용 전 공통 저장/삭제 버튼이 많은 기존 업무 화면은 별도 UX 정리 TASK에서 같은 원칙을 확대 적용한다.

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

## 24. 2026-07-08 자동/수동 알림 양식 통일

최종 리뷰 전 자동 알림 경로를 다시 점검하면서 자동 알림의 Mail/TeamsChannel/TeamsActivity 양식이 관리자 수동 발송 양식과 다르게 표시될 수 있음을 확인했다.

반영 결정:

- 자동 알림과 수동 알림은 같은 외부 표시 원칙을 사용한다.
- Mail 제목은 `[알림 유형] 제목` 형식으로 통일한다.
- Mail 본문과 TeamsChannel 본문은 아래 순서를 사용한다.
  - `EMI 프로젝트 통합관리시스템 알림`
  - `알림 유형`
  - `프로젝트명`
  - `제목`
  - `내용`
  - `발송시각`
  - `끝.`
- TeamsActivity는 `알림 유형, 제목`을 template parameter로 사용하고, previewText는 내용 요약을 150자 이하로 사용한다.
- correlation id는 제목/본문/TeamsActivity preview에 노출하지 않는다.
- correlation id는 `notification_deliveries` 내부 추적값으로만 저장하고, 알림 상세의 고급/내부 정보 영역에서만 확인한다.
- 자동 delivery 생성 시에도 display snapshot을 채운다.
  - `display_title`
  - `display_message`
  - `display_project_name`
  - `display_work_item_title`
  - `display_recipient_name`
  - `display_recipient_email`
  - `display_recipient_kind`
  - `display_channel_target`
- Daily Digest는 aggregate 알림이므로 공통 본문 상단 구조를 사용하되, 내용 영역에는 기존 일일 요약 section을 유지한다.
- TeamsChannel Adaptive Card에는 Webhook URL, raw enum, correlation id를 표시하지 않는다.

알림 유형 label:

- `WorkItemCreated`: 업무 배정 알림
- `DueSoonL0`: 예정일 임박 알림
- `OverdueL1/L2/L3`: 예정일 초과 알림
- `UrgentBlocking`: 긴급 알림
- `DailyDigest`: 일일 업무 요약
- `ReferenceDigest`: 참조 알림
- `ProjectCompletion`: 프로젝트 완료 알림
- `Custom`: 일반 알림

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

## 22. 2026-07-08 수동 발송 UX / 상세 추적 구조 보강

최종 리뷰 전 사용자 검수에서 수동 알림 발송 화면이 실제 운영자가 쓰기에는 아직 동기 발송 중심이고, 수신자/프로젝트/상세 추적 UX가 부족하다는 문제가 확인됐다.

반영 결정:

- 수동 알림 발송 화면은 기존 프로젝트 목록에서 프로젝트를 선택할 수 있게 한다.
- 프로젝트 업무가 아닌 알림을 위해 프로젝트 선택에 `기타` 옵션을 제공한다.
- `기타` 선택 시 프로젝트명/구분을 직접 입력하고, 실제 프로젝트 row는 생성하지 않는다.
- correlation id는 내부 추적값이므로 발송 화면, 메일 제목, Teams 채널 본문, Teams Activity preview에 표시하지 않는다.
- correlation id는 DB에 계속 저장하고 알림발송상태 상세의 내부 추적값 영역에서만 확인한다.
- TeamsActivity와 Mail은 다중 수신자를 지원한다.
- 추적성을 위해 TeamsActivity/Mail은 수신자별 `notification_deliveries` row를 생성한다.
- 수동 발송 API는 provider 완료를 기다리지 않고 `Pending` delivery row를 생성한 뒤 즉시 `발송 요청 접수`를 반환한다.
- 기존 `NotificationDispatcher` worker가 Pending manual delivery를 처리한다.

신규 migration:

- `0026_notification_delivery_manual_payload.sql`
- `notification_deliveries`에 worker 렌더링용 수동 payload 컬럼을 추가했다.
  - `manual_payload_json`
  - `manual_requested_by_user_id`
  - `manual_requested_at_utc`
- payload에는 알림 유형, 제목, 프로젝트명, 내용, 요청시각만 저장한다.
- secret, token, webhook URL, installedAppId, provider raw payload는 저장하지 않는다.

수동 발송 양식:

- Mail 제목: `[알림 유형] 제목`
- Mail 본문:
  - `EMI 프로젝트 통합관리시스템 알림`
  - `알림 유형`
  - `프로젝트명`
  - `제목`
  - `내용`
  - `발송시각`
  - `끝.`
- TeamsChannel은 Mail과 같은 본문 양식을 사용한다.
- TeamsActivity는 `알림 유형, 제목` / `내용` 형식으로 짧게 보낸다.
- ProjectCreated 수동 알림은 manifest에 없는 `projectCreated` activityType을 쓰지 않고 `workItemAssigned`를 사용한다.

알림발송상태:

- 목록의 `알림` 칸에는 제목만 표시한다.
- 유형과 프로젝트명은 별도 짧은 텍스트로 표시한다.
- 내용 전문, 오류 상세, 내부 추적값은 상세 페이지에서 확인한다.
- 상세 route는 `/admin/system/notification-deliveries/{id}`이다.
- 상세 페이지에는 구분, 알림 유형, 프로젝트명, 제목, 내용, 발송시각, 채널, 수신자, 상태, 오류/대기 사유, 처리상태, 내부 추적값을 표시한다.

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

## 23. 2026-07-08 HTTPS UAT worker / Teams Activity 상세 deep link 보강

HTTPS UAT에서 수동 발송한 TeamsActivity delivery가 `Pending` 상태로 남는 문제가 확인됐다. frontend HTTPS 자체가 발송을 막은 것이 아니라, HTTPS 전용 UAT wrapper가 backend 실행 시 ignored `.env.notify-local`을 로드하지 않아 TeamsActivity actual 및 dispatch 관련 설정이 backend 프로세스에 일관되게 전달되지 않는 문제가 원인이었다.

반영 내용:

- `scripts/dev-uat-start-teams-https.sh`는 `UAT_LOAD_NOTIFY_LOCAL_ENV=true`를 설정한다.
- `scripts/dev-uat-start.sh`는 해당 플래그가 켜진 경우 `.env.notify-local`을 backend 환경에 로드한다.
- env 파일은 `source`로 실행하지 않고, `KEY=VALUE` 형식만 export하는 안전한 dotenv parser로 읽는다.
- macOS 기본 bash 호환을 위해 `${VAR,,}` 문법은 사용하지 않고 `tr '[:upper:]' '[:lower:]'` 기반으로 정규화한다.
- HTTPS UAT에서도 `FRONTEND_ORIGIN`에 `https://localhost:5174`가 포함되도록 보정한다.
- worker 재기동 후 기존 Pending TeamsActivity delivery가 처리 대상에 포함되는 것을 확인했다.
- 최신 TeamsActivity 큐 항목은 Pending에 머물지 않고 Graph 호출 후 실패 상태로 전환됐다.
- 실패 원인은 worker 비활성이 아니라 Graph가 `.env.notify-local`의 `Notifications__TeamsActivity__InstalledAppId`를 유효한 설치 앱 id로 인정하지 않는 문제였다.
- Graph 오류 메시지에 installedAppId가 포함될 수 있으므로 API/DB 오류 메시지에서는 긴 설치 앱 식별자를 `[MASKED_ID]`로 마스킹한다.
- invalid installedAppId 오류는 `TeamsActivityInvalidInstalledAppId`로 분류한다.
- 현재 권한은 `TeamsActivity.Send`이므로 app-only token으로 `/users/{id}/teamwork/installedApps`를 조회해 자동 보정하는 것은 403으로 막힐 수 있다. 정확한 installedApps 최상위 `id`는 사용자가 Graph 진단 결과 기준으로 `.env.notify-local`에 넣어야 한다.

Teams Activity 상세 route:

- `/teams/activity/deliveries/{deliveryId}` route를 추가했다.
- 기존 admin delivery 상세 DTO를 재사용하되, 일반 사용자는 본인 TeamsActivity delivery만 조회하고 System Administrator/개발 admin은 UAT 검수를 위해 조회할 수 있게 했다.
- 상세 화면은 빈 화면 대신 알림 유형, 프로젝트명, 제목, 내용, 발송시각, 채널, 수신자, 상태, 오류/대기 사유, 내부 추적값을 표시한다.
- 조회 실패 또는 권한 불일치 시 한글 fallback과 `전체 알림으로 돌아가기` 버튼을 표시한다.
- 좁은 Teams pane에서 상세 grid와 오류 안내 문구가 page-level horizontal overflow를 만들지 않도록 보정했다.

deep link 제한:

- 현재 안정적으로 성공한 TeamsActivity actual 경로는 `topic.source=entityUrl` + `users/{entraObjectId}/teamwork/installedApps/{installedAppId}`이다.
- 이 방식은 Graph가 앱 설치 항목을 topic으로 삼아 Activity Feed 알림을 보낸다.
- `topic.webUrl`을 함께 넣어 delivery 상세 URL로 바로 이동시키는 방식은 Graph 400을 유발할 수 있어 기본 payload에는 넣지 않는다.
- 따라서 현재 구현은 상세 route 직접 접근을 지원하고, Activity Feed 클릭 시 빈 화면이 아니라 `/teams/activity` 또는 static tab 화면이 열리는 안정 경로를 유지한다.
- 정확히 특정 delivery 상세를 여는 Teams deep link는 운영 URL과 manifest static tab entityId 기준으로 후속 actual smoke가 필요하다.

manifest / localhost 제한:

- 현재 localhost manifest는 검수 사용자 A PC 로컬 검수용이다.
- 다른 사용자가 같은 Teams 앱을 열면 각자 자기 PC의 `localhost`를 바라본다.
- 다른 사용자에게 actual 검수를 확장하려면 Dev Tunnel, ngrok, 또는 운영 HTTPS 도메인이 필요하다.
- 로컬 manifest v1.0.1 권장값은 다음과 같다.
  - `contentUrl`: `https://localhost:5174/teams/activity`
  - `websiteUrl`: `https://localhost:5174/teams/activity`
- 운영 전에는 localhost를 실제 운영 도메인의 `/teams/activity`로 교체해야 한다.

사용자 검수 체크리스트:

- [ ] HTTPS UAT 실행 후 `https://localhost:5174/teams/activity`가 열림
- [ ] 수동 발송 후 TeamsActivity delivery가 Pending에서 Sent로 변경됨
- [ ] worker 처리 대기 상태가 계속 쌓이지 않음
- [ ] Teams Activity Feed 알림이 도착함
- [ ] Teams Activity 클릭 시 오른쪽 앱 화면이 빈 화면이 아님
- [ ] 알림 클릭 시 해당 알림 상세가 바로 표시되거나, 최근 알림이 강조 표시됨
- [ ] `/teams/activity/deliveries/{deliveryId}` 직접 접근 시 상세 표시
- [ ] 상세 화면에서 알림 유형/프로젝트/제목/내용/발송시각 확인 가능
- [ ] 전체 알림으로 돌아가기 버튼 동작
- [ ] 다른 사람이 localhost로 접속할 수 없다는 제한이 문서화됨
- [ ] Console 오류 없음
- [ ] 모바일/Teams narrow pane overflow 없음

## 24. 2026-07-08 인앱 알림 원본 / 외부 delivery 연동 구조 보강

최종 리뷰 전 다중 사용자 TeamsActivity actual 검수에서 사용자별 `InstalledAppId`를 운영 설정으로 관리하는 방식은 유지할 수 없다는 점을 확인했다. 알림 구조도 관리자 수동 발송 delivery snapshot만으로 추적하는 대신, 인앱 `notifications`를 원본으로 두고 외부 채널 delivery가 이를 참조하도록 정리했다.

구조 원칙:

- `notifications`와 `notification_recipients`가 모든 사용자 알림의 원본이다.
- `notification_deliveries`는 TeamsActivity, Mail, TeamsChannel 같은 외부 발송 채널의 이력이다.
- 자동 업무/에스컬레이션 알림은 기존처럼 notification 원본과 recipient를 만들고 delivery에 `notification_id`, `notification_recipient_id`, `work_item_id`를 연결한다.
- 관리자 수동 발송도 `notifications` row를 먼저 만들고, TeamsActivity/Mail 사용자 수신자에는 `notification_recipients` row를 만든다.
- TeamsChannel 또는 직접 이메일처럼 QMS user recipient가 없는 경우에도 delivery는 `notification_id`를 연결한다.
- 외부 발송 실패와 무관하게 인앱 알림 원본은 남는다.

TeamsActivity topic 정책:

- 운영 기본 topic은 사용자별 installed app id가 아니라 Teams catalog app id 기반 entityUrl이다.
- 기본 topic value:
  - `https://graph.microsoft.com/v1.0/appCatalogs/teamsApps/{TeamsCatalogAppId}`
- `.env.notify-local`에는 `Notifications__TeamsActivity__TeamsCatalogAppId`를 설정해야 한다.
- `Notifications__TeamsActivity__InstalledAppId`는 검수 사용자 A 단일 계정 검수처럼 진단/fallback 용도로만 유지한다.
- `TeamsCatalogAppId`는 Graph installedApps 응답의 `teamsApp.id`이며 manifest external id와 다르다.
- 현재 로컬 `.env.notify-local`에는 `TeamsCatalogAppId`가 아직 없어서 검수 사용자 A/검수 사용자 B catalog topic actual 재검증은 설정 후 다시 수행해야 한다.

알림 상세 route:

- 사용자용 notification 상세 route는 `/teams/activity/notifications/{notificationId}`이다.
- backend는 `GET /api/notifications/{notificationId}`를 제공한다.
- 현재 사용자가 해당 `notification_recipients` recipient이거나 관리자 권한이 있을 때만 조회된다.
- 조회 실패, 권한 없음, 인증 전 상태는 빈 화면 대신 한글 안내와 `전체 알림으로 돌아가기` 버튼을 표시한다.
- 알림 상세는 알림 유형, 프로젝트명, 제목, 내용, 생성시각, 읽음 상태, 관련 프로젝트/업무 링크, 내부 notification id를 표시한다.
- 관리자 delivery 상세 route `/admin/system/notification-deliveries/{id}`와 Teams delivery 상세 route `/teams/activity/deliveries/{deliveryId}`는 운영 추적/진단용으로 유지한다.

외부 알림 링크 정책:

- Mail/TeamsChannel/TeamsActivity의 사용자-facing link는 `notification_id` 기준 상세 route를 우선 사용한다.
- notification 원본이 없는 레거시/test delivery는 기존 link URL로 fallback한다.
- Mail/TeamsChannel 본문에는 `알림 상세 보기` 링크를 추가할 수 있다.
- TeamsActivity preview/template에는 notification id와 correlation id를 노출하지 않는다.
- 권한 검사는 frontend route가 아니라 `GET /api/notifications/{notificationId}` 서버 API에서 강제한다.
- localhost manifest는 본인 PC 전용이므로, 검수 사용자 B 등 다른 사용자 actual 검수에는 Dev Tunnel/ngrok/운영 HTTPS 도메인이 필요하다.

사용자 검수 체크리스트:

- [ ] 관리자 수동 발송 시 인앱 알림도 생성됨
- [ ] 자동 업무 알림은 인앱 알림과 업무가 연결됨
- [ ] TeamsActivity/Mail/TeamsChannel delivery가 notification_id로 추적됨
- [ ] Teams Activity 알림 클릭 후 알림 상세를 볼 수 있음
- [ ] 미로그인 상태에서는 로그인 화면이 먼저 표시됨
- [ ] 로그인 후 원래 알림 상세로 돌아옴
- [ ] 권한 없는 사용자는 알림/업무 상세 접근이 차단됨
- [ ] 알림 상세에서 관련 업무 보기 버튼이 동작함
- [ ] 알림 상세에서 관련 프로젝트 보기 버튼이 동작함
- [ ] 검수 사용자 A TeamsActivity 수신 성공
- [ ] 검수 사용자 B TeamsActivity 수신 성공
- [ ] 검수 사용자 B에게 별도 installedAppId를 수동 입력하지 않음
- [ ] 수동 발송 알림이 관리자 수동 발송으로 구분됨
- [ ] 외부 알림 제목/본문에 correlation id가 노출되지 않음
- [ ] Console 오류 없음
- [ ] 모바일/Teams narrow pane overflow 없음

## 26. 2026-07-08 최종 리뷰 전 UAT smoke / 문서화 반영

최종 PR 준비 전 synthetic/test 데이터 기준으로 수동 알림 3모드와 접근권한을 다시 확인했다.

확인 결과:

- HTTPS UAT backend `/health/live`, `/health/ready`: 200
- HTTPS UAT frontend `/teams/activity`: 200
- UAT latest migration: `0027_notification_access_scope_and_manual_work_items`
- 채널 공지 smoke:
  - `notifications` row 생성
  - TeamsChannel `notification_deliveries` row 생성
  - `visibility_scope=Authenticated`
  - worker 처리 후 TeamsChannel `Sent`
  - 로그인된 active dev user 상세 접근 200
  - 비로그인 API 접근 401
- 개인 알림 smoke:
  - `notifications` row 생성
  - `notification_recipients` row 생성
  - TeamsActivity delivery가 `notification_id` / `notification_recipient_id`와 연결
  - worker 처리 후 TeamsActivity `Sent`
  - 비수신자 상세 접근 403
  - EntraId 실제 수신자의 CLI 직접 인증은 UAT dev-auth 정책상 401이므로 실제 사용자는 Teams/Microsoft 로그인으로 확인해야 한다.
- 업무 배정 smoke:
  - 기존 테스트 프로젝트를 사용했다.
  - 실제 `work_items` row가 생성됐다.
  - notification이 `project_id`와 `work_item_id`를 가진다.
  - 담당자 내 업무 DB 반영을 확인했다.
  - 업무가 아닌 개인 알림/채널 공지에서는 work_item을 생성하지 않았다.

최종 문서 산출물:

- `tasks/notify-003-implementation-report.md`
- `tasks/notify-003-sop.md`
- `tasks/notify-003-user-manual.md`

사용자 검수 체크리스트:

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

## 26. 2026-07-08 인앱 알림 접근권한 / 수동 업무 배정 원본화 보강

최종 리뷰 전 검수에서 외부 알림 상세 접근권한과 수동 업무 배정의 실제 업무 생성 정책을 분리해 정리했다. Teams 채널 게시는 채널 멤버십을 QMS가 직접 알 수 없으므로 로그인된 active 사용자 접근으로 처리하고, TeamsActivity/Mail 개인 알림은 실제 `notification_recipients` 수신자만 접근할 수 있게 했다.

DB/migration:

- 신규 migration: `0027_notification_access_scope_and_manual_work_items.sql`
- `notifications.visibility_scope`
  - `RecipientOnly`: 개인 TeamsActivity/Mail/업무 알림
  - `Authenticated`: TeamsChannel 채널 공지
  - `AdminOnly`: 필요 시 관리자 전용 확장용
- `notifications.source_kind`
  - `Automatic`, `Manual`, `ChannelNotice`, `WorkAssignment`, `DailyDigest`, `Escalation`, `System`
- `notifications.work_item_id`
  - 업무 배정 수동 알림 및 업무 자동 알림 상세에서 관련 업무를 표시하기 위한 원본 연결
- `notifications.manual_requested_by_user_id`
  - 관리자 수동 발송 요청자 추적

접근권한 정책:

- 개인 알림은 현재 사용자에게 `notification_recipients` row가 있어야 `GET /api/notifications/{notificationId}` 조회가 가능하다.
- 채널 공지는 `visibility_scope=Authenticated`인 notification으로 저장하고, 로그인된 active 사용자가 조회할 수 있다.
- System Administrator 권한이라도 사용자-facing 개인 알림 상세를 무제한으로 열지 않는다. 관리자 추적은 별도 알림발송상태/admin delivery 상세 route에서 처리한다.
- 채널 공지 읽음 처리는 사용자가 처음 읽음 처리할 때 해당 사용자용 `notification_recipients` row를 lazy 생성한 뒤 `read_at_utc`를 기록한다.

관리자 수동 발송 3모드:

- 개인 알림
  - Teams Activity / Mail만 선택 가능
  - Teams 채널 선택은 숨김
  - `visibility_scope=RecipientOnly`
  - 사용자 수신자는 `notification_recipients`와 delivery `notification_recipient_id`로 연결
- 채널 공지
  - TeamsChannel 게시 전용
  - 개인 수신자 선택은 숨김
  - `visibility_scope=Authenticated`, `source_kind=ChannelNotice`
  - delivery는 `notification_id`를 참조하며 channel target은 Teams 채널로 표시
- 업무 배정
  - 기존 프로젝트, 담당자, 업무 단계, 업무 제목/내용을 입력
  - 담당자별 실제 `work_items` row를 생성
  - 담당자별 `notifications` + `notification_recipients`를 생성
  - 선택한 TeamsActivity/Mail delivery는 생성된 work item과 notification 원본을 참조
  - 외부 채널을 선택하지 않아도 인앱 알림과 내 업무 생성은 가능

알림 상세:

- `/teams/activity/notifications/{notificationId}`는 source kind, visibility scope, 관련 업무, 관련 프로젝트, 읽음 상태를 표시한다.
- 상세 화면에는 `읽음 처리`, `관련 업무 보기`, `관련 프로젝트 보기`, `전체 알림으로 돌아가기` 액션을 제공한다.
- 권한 없음/대상 없음/인증 전 상태는 빈 화면 대신 한글 안내로 처리한다.

검증 결과:

- backend Notification/Migration targeted tests: 79개 통과
- frontend typecheck: 통과
- frontend App/auth unit tests: 57개 통과
- 알림 수동 발송 개인 알림은 비수신자 403을 테스트했다.
- 채널 공지는 로그인된 다른 active 사용자의 상세 조회와 lazy read state 생성을 테스트했다.
- 업무 배정 수동 발송은 실제 `work_items` 생성, recipient notification 연결, 내 업무 목록 표시를 테스트했다.

사용자 검수 체크리스트:

- [ ] 채널 공지 알림 상세는 로그인된 사용자면 접근 가능
- [ ] 개인 TeamsActivity 알림 상세는 수신자만 접근 가능
- [ ] 개인 Mail 알림 상세는 수신자만 접근 가능
- [ ] 비수신자는 개인 알림 상세 접근이 차단됨
- [ ] 관리자 수동 발송 시 인앱 알림이 생성됨
- [ ] 외부 알림 상세에서 원본 인앱 알림 내용 확인 가능
- [ ] 알림 상세에서 읽음 처리 가능
- [ ] 읽음 처리 후 상태가 갱신됨
- [ ] 알림 상세에서 관련 업무 보기 가능
- [ ] 알림 상세에서 관련 프로젝트 보기 가능
- [ ] 업무 배정 수동 알림 발송 시 실제 내 업무가 생성됨
- [ ] 생성된 업무가 수신자의 내 업무에 표시됨
- [ ] 수동 발송 화면에서 개인 알림/채널 공지/업무 배정 모드가 구분됨
- [ ] 개인 알림 모드에서 Teams 채널 선택을 강제하지 않음
- [ ] 채널 공지 모드에서 개인 수신자 선택을 강제하지 않음
- [ ] 업무 배정 모드에서 담당자/업무 정보 입력 가능
- [ ] TeamsActivity와 Mail이 notification_id/recipient_id로 추적됨
- [ ] TeamsChannel이 notification_id로 추적됨
- [ ] 로그인 안 된 상태에서 상세 접근 시 로그인 화면 표시
- [ ] 로그인 후 원래 상세로 복귀
- [ ] 권한 없는 사용자는 접근 권한 없음 표시
- [ ] Console 오류 없음
- [ ] 모바일/Teams narrow pane overflow 없음

## 25. 2026-07-08 TeamsActivity text topic / 인앱 notification 원본화 최신 반영

이 섹션은 24장의 catalog topic 검토를 대체하는 최신 구현 기준이다. Microsoft Graph 문서 기준 `teamworkActivityTopic.source`는 `entityUrl` 또는 `text`를 지원하고, `text` topic에서는 `webUrl`이 필수다. 실제 UAT Graph 호출에서는 일반 `https://localhost:5174/teams/activity/notifications/{notificationId}` URL이 `Invalid 'webUrl'`로 거부되고 `https://teams.microsoft.com/l/...` 형식의 Teams deep link가 필요하다는 응답이 확인됐다.

최신 TeamsActivity 정책:

- 운영 기본은 `topic.source=text`이다.
- 사용자별 `InstalledAppId`는 필수 설정이 아니며 운영 기본 경로에서 사용하지 않는다.
- `InstalledAppId` + installedApps entityUrl 방식은 diagnostic/fallback 용도로만 유지한다.
- Graph payload의 `teamsAppId`는 기본 생략한다.
- text topic의 `topic.webUrl`은 Teams deep link로 생성한다.
- Teams deep link 형식은 `https://teams.microsoft.com/l/entity/{TeamsCatalogAppId}/home?webUrl={encoded notification detail URL}&label={encoded 알림상세}&context={encoded {"subEntityId":"notification:{notificationId}"}}`이다.
- Teams deep link의 `webUrl` query가 사용자-facing 인앱 알림 상세 URL `/teams/activity/notifications/{notificationId}`를 가리킨다.
- Teams manifest static tab `entityId`는 기본 `home`이며, `Notifications:TeamsActivity:TeamsStaticTabEntityId` 또는 기존 `DeepLinkEntityId`로 바꿀 수 있다.
- organization distribution 앱은 Teams deep link app id로 manifest external id가 아니라 Graph installedApps 응답의 Teams org catalog app id를 우선 사용한다.
- `Notifications:TeamsActivity:TeamsCatalogAppId`가 있으면 Teams deep link app id로 사용한다.
- `TeamsCatalogAppId`가 없으면 `TeamsManifestExternalId`, 기존 `ManifestId`, 기존 `TeamsAppId` 순서로 fallback한다.
- `Notifications:TeamsActivity:TeamsAppId`는 Graph payload `teamsAppId`가 아니라 레거시 Teams deep link URL 생성 fallback에 사용한다.
- Teams가 기본 contentUrl `/teams/activity`를 열면 frontend가 TeamsJS context의 `subEntityId=notification:{notificationId}`를 읽어 `/teams/activity/notifications/{notificationId}` 상세로 이동한다.
- `topic.webUrl`에 correlation id, installedAppId, token, secret을 넣지 않는다.

인앱 원본 구조:

- 관리자 수동 발송은 먼저 `notifications` row를 생성한다.
- TeamsActivity/Mail 사용자 수신자는 `notification_recipients`를 생성하고, delivery에 `notification_id`와 `notification_recipient_id`를 연결한다.
- TeamsChannel 또는 직접 이메일처럼 QMS user recipient가 없는 발송도 delivery는 `notification_id`를 가진다.
- 외부 발송 실패와 무관하게 인앱 notification 원본은 남는다.
- 사용자-facing 상세 API는 `GET /api/notifications/{notificationId}`이며 recipient 또는 관리자 권한을 서버에서 확인한다.
- `/teams/activity/notifications/{notificationId}`는 미로그인/권한 없음/대상 없음 상태를 빈 화면 없이 한글 안내로 처리한다.

UAT actual 결과:

- HTTPS UAT: `https://localhost:5174/teams/activity` 200
- worker: manual TeamsActivity Pending delivery를 처리해 Graph actual 호출 수행
- 일반 앱 상세 URL을 `topic.webUrl`로 넣은 text topic smoke: 검수 사용자 A/검수 사용자 B 모두 `TeamsActivityInvalidTopic`
- Teams deep link `topic.webUrl`로 전환 후 text topic smoke: 검수 사용자 A/검수 사용자 B 모두 `Sent`
- 검수 사용자 B에게 별도 installedAppId를 수동 입력하지 않았다.
- 생성된 TeamsActivity delivery는 notification 상세 API `GET /api/notifications/{notificationId}` 200으로 연결됐다.

실패/재시도 정책:

- `TeamsActivityAppNotInstalled`, `TeamsActivityInvalidTopic`, `TeamsActivityInvalidActivityType`, `TeamsActivityInvalidInstalledAppId`, `TeamsActivityPermissionDenied`, `TeamsActivityUserOrAppNotFound`는 설정/설치/권한성 오류로 보고 재시도 예약을 중단한다.
- `TeamsActivityInvalidTopic`의 관리자 조치 안내는 `Teams manifest validDomains와 webUrl 도메인을 확인하세요.`이다.
- `TeamsActivityAppNotInstalled`의 관리자 조치 안내는 `수신자의 Teams 앱 설치 상태 또는 Teams 앱 정책을 확인하세요.`이다.

사용자 검수 체크리스트:

- [ ] 관리자 수동 발송 시 인앱 알림도 생성됨
- [ ] 자동 업무 알림은 인앱 알림과 업무가 연결됨
- [ ] TeamsActivity/Mail/TeamsChannel delivery가 notification_id로 추적됨
- [ ] Teams Activity 알림 클릭 후 알림 상세를 볼 수 있음
- [ ] 미로그인 상태에서는 로그인 화면이 먼저 표시됨
- [ ] 로그인 후 원래 알림 상세로 돌아옴
- [ ] 권한 없는 사용자는 알림/업무 상세 접근이 차단됨
- [ ] 알림 상세에서 관련 업무 보기 버튼이 동작함
- [ ] 알림 상세에서 관련 프로젝트 보기 버튼이 동작함
- [ ] 검수 사용자 A TeamsActivity 수신 성공
- [ ] 검수 사용자 B TeamsActivity 수신 성공
- [ ] 검수 사용자 B에게 별도 installedAppId를 수동 입력하지 않음
- [ ] 수동 발송 알림이 관리자 수동 발송으로 구분됨
- [ ] 외부 알림 제목/본문에 correlation id가 노출되지 않음
- [ ] Console 오류 없음
- [ ] 모바일/Teams narrow pane overflow 없음
