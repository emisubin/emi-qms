# TASK-NOTIFY-003 구현 보고서

## 1. 목적

Teams Activity Feed actual 발송을 기존 인앱/TeamsChannel/Mail delivery 계층에 연결하고, 관리자가 알림 발송 상태를 추적·처리·수동 발송할 수 있게 한다.

## 2. 구현 범위

- Teams Activity Feed actual provider
- installedAppId 기반 `entityUrl` topic 발송
- `/teams/activity` Teams tab 전용 화면
- TeamsChannel / TeamsActivity / Mail 3채널 발송 검수
- 관리자 알림발송상태 목록/상세
- 실패/대기 알림 확인 처리, 제외 처리, 대기 재시도
- 관리자 수동 알림 발송
- 수동 발송 queue 저장 방식
- TeamsActivity / Mail 다중 수신자
- notification delivery display snapshot
- 수동/자동 알림 양식 통일

## 3. 제외 범위

- Teams manifest/icon repo 포함
- 운영 URL 확정 및 운영 Teams 앱 재배포
- `projectCreated` manifest activityType 추가
- 사용자별 알림 설정 UI
- 실패 delivery 강제 성공 처리
- delivery row hard delete
- Teams DM/Bot 구현
- 실제 프로젝트 row 생성 또는 workflow event 생성

## 4. DB / Migration

- `0023_teams_activity_delivery_channel.sql`
  - `notification_deliveries.channel`에 `TeamsActivity`를 추가한다.
  - ADMIN-001의 0020~0022 이후 번호로 재정렬했다.
- `0024_notification_delivery_admin_handling.sql`
  - `admin_handling_status`, `admin_handled_at_utc`, `admin_handled_by_user_id`, `admin_handling_note`를 추가한다.
  - 실패/대기 dashboard count는 미처리 기준으로 계산한다.
- `0025_notification_delivery_display_snapshot.sql`
  - 제목, 메시지, 프로젝트, 업무, 수신자, 채널 대상, manual kind, correlation id snapshot을 추가한다.
- `0026_notification_delivery_manual_payload.sql`
  - 수동 발송 worker 렌더링용 `manual_payload_json`, 요청자, 요청시각을 추가한다.
- 기존 main 반영 migration 0001~0022는 수정하지 않았다.
- 이전 WIP `0020_teams_activity_delivery_channel.sql`은 ADMIN-001 migration 번호와 충돌하지 않도록 제거하고 0023으로 대체했다.

## 5. TeamsActivity Actual 구조

- Graph `sendActivityNotification`을 사용한다.
- 수신자는 EntraId 사용자여야 하며 `entra_object_id`가 필요하다.
- actual 발송은 `installedAppId` + `entityUrl` topic을 사용한다.
- text topic 방식은 Graph installed app 식별 불일치 가능성이 있어 fallback 성격으로만 유지한다.
- TeamsActivity previewText는 150자 이하로 제한한다.

## 6. installedAppId / entityUrl

Graph installedApps 진단에서 설치 앱의 `externalId`와 manifest id, `authorization.clientAppId`와 webApplicationInfo id가 일치함을 확인한 뒤, topic을 다음 구조로 발송한다.

- `topic.source = entityUrl`
- `topic.value = https://graph.microsoft.com/v1.0/users/{user}/teamwork/installedApps/{installation}`
- `teamsAppId`와 `topic.webUrl`은 entityUrl 방식에서 생략한다.

실제 id 값은 env/Graph secret과 함께 보고서에 원문으로 남기지 않는다.

## 7. `/teams/activity`

- Teams Activity 클릭 후 빈 화면이 보이지 않도록 전용 route를 추가했다.
- 최근 알림, 내 미완료 업무, empty/auth/API 실패 안내를 표시한다.
- Teams narrow pane에서 page-level horizontal overflow가 없도록 구성했다.
- manifest 권장값은 `contentUrl`과 `websiteUrl` 모두 `/teams/activity`이다.

## 8. 3채널 알림

- TeamsChannel: Webhook/Adaptive Card 기반 채널 게시
- TeamsActivity: Graph Activity Feed 개인 알림
- Mail: Gmail SMTP actual provider
- 수동/자동 알림 모두 같은 display snapshot과 delivery 이력 구조를 사용한다.

## 9. 관리자 수동 알림 발송

- route: `/admin/system/send-notification`
- 알림 유형, 기존 프로젝트/기타, 제목, 내용, 채널, 수신자를 입력한다.
- TeamsActivity와 Mail은 다중 수신자를 지원한다.
- TeamsChannel은 설정된 채널 대상으로 게시한다.
- correlation id는 화면에 표시하지 않고 backend에서 자동 생성한다.
- 발송 버튼은 `발송`이며, 버튼 근처에 처리 상태를 표시한다.

## 10. Queue 처리

- 수동 발송 API는 provider actual 완료까지 기다리지 않는다.
- 채널/수신자별 `Pending` delivery row를 생성하고 즉시 응답한다.
- `NotificationDispatcher` worker가 Pending delivery를 처리한다.
- worker가 꺼져 있으면 알림발송상태에서 대기 사유로 확인한다.

## 11. 수동/자동 알림 양식

Mail 제목:

```text
[알림 유형] 제목
```

Mail/TeamsChannel 본문:

```text
EMI 프로젝트 통합관리시스템 알림

알림 유형:
프로젝트명:

제목:
내용:

발송시각:

끝.
```

TeamsActivity:

```text
알림 유형, 제목
내용
```

Daily Digest는 공통 본문 헤더를 사용하고, 기존 digest section을 내용 영역에 유지한다. correlation id는 제목/본문/Activity preview에 노출하지 않는다.

## 12. Display Snapshot / Detail

- 목록은 제목 중심으로 짧게 표시한다.
- 상세 route는 `/admin/system/notification-deliveries/{id}`이다.
- 상세에는 구분, 알림 유형, 프로젝트명, 제목, 내용, 발송시각, 채널, 수신자, 상태, 오류/대기 사유, 처리상태, 내부 추적값을 표시한다.
- 자동 알림 생성 경로도 display snapshot을 채운다.

## 13. Admin Handling

- 실패/대기 delivery에 대해 확인 처리와 제외 처리를 지원한다.
- 대기 delivery는 `next_attempt_at_utc`를 현재로 당겨 재시도 대기열에 올릴 수 있다.
- 실제 발송 상태는 임의로 성공 처리하지 않는다.
- Sent/DryRunSent/Suppressed/Disabled row에는 `미처리` badge를 붙이지 않는다.

## 14. Tests / UAT

실행한 주요 검증:

- `git diff --check`
- `actionlint .github/workflows/ci.yml`
- backend Release build
- backend 전체 test
- Notification/Admin targeted tests
- Migration tests
- frontend lint/typecheck/unit/build
- frontend unit test
- mock UI smoke
- Full-Stack E2E 16개
- UAT backend `/health/live`, `/health/ready`
- UAT frontend HTTP 200
- UAT `/teams/activity` smoke
- UAT 알림발송상태 smoke
- secret/PII scan

UAT latest migration은 `0026_notification_delivery_manual_payload`까지 확인했다.

## 15. 사용자 검수 체크리스트

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

## 16. 보안 / Secret

- `.env`, `.env.notify-local`, `.env.entra-local`은 commit 대상이 아니다.
- Teams manifest/icon은 repo에 포함하지 않는다.
- TenantId, ClientId, ClientSecret, installedAppId, webhook URL, SMTP password, Graph token은 문서/로그/보고서에 원문으로 남기지 않는다.
- provider raw response 전체를 사용자 화면에 노출하지 않는다.

## 17. 운영 적용 전 체크리스트

- Teams manifest `contentUrl`/`websiteUrl`을 운영 URL의 `/teams/activity`로 교체
- Teams 앱 조직 배포 및 사용자 설치 상태 재확인
- Graph TeamsActivity 권한/관리자 동의 재확인
- Gmail SMTP 운영 적합성 또는 공식 발송 수단 전환 검토
- Teams Webhook 운영 채널/운영 secret 분리
- NotificationDispatcher worker 실행/재시도 정책 운영 검수
- 알림발송상태에서 실패/대기 처리 운영 절차 안내

## 18. 알려진 제한사항 / 후속

- `projectCreated` activityType은 manifest에 아직 없으므로 프로젝트 생성 수동 알림은 `workItemAssigned` activityType을 사용한다.
- 사용자별 알림 채널 preference UI는 없다.
- 실패 delivery 강제 성공 처리 UI는 없다.
- delivery row hard delete는 하지 않는다.
- Teams manifest/icon 패키지는 운영자가 별도 관리한다.
