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

- `database/migrations/0020_teams_activity_delivery_channel.sql`

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

- 총무팀/Microsoft 365 승인 전 actual 발송은 수행하지 않는다.
- Teams 앱 manifest와 아이콘은 repo에 포함하지 않는다.
- Teams app installation 확인/자동 설치 API는 이번 foundation에서 구현하지 않는다.
- Graph 204는 Graph 요청 성공 의미이며, Teams UI 표시 완료는 사용자 수동 확인이 필요하다.
