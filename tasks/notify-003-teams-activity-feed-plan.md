# TASK-NOTIFY-003 Teams Activity Feed 개인별 알림 기획

## 1. 목적

Teams 개인별 알림을 DM이 아니라 Activity Feed 방식으로 제공하기 위한 후속 TASK 기획 문서다. 이번 문서는 사전 조사와 설계 기준을 정리하며 실제 구현, 권한 요청, manifest 생성은 수행하지 않는다.

## 2. 왜 Teams DM 대신 Activity Feed인지

- 업무 알림은 사용자별로 확인해야 하지만, 일반 채팅 DM은 개인 채팅방을 과도하게 늘릴 수 있다.
- Activity Feed는 Teams 안에서 사용자별 알림 UX에 더 가깝고, 업무 시스템 알림의 출처를 Teams 앱으로 일관되게 표시할 수 있다.
- Teams 앱 manifest의 activity type과 template을 통해 알림 유형을 구조화할 수 있다.
- 향후 예정일 임박, 지연, 긴급 차단, 재검사 요청 같은 이벤트를 activity type별로 관리하기 쉽다.

## 3. 필요한 Microsoft/Teams 준비물

- Teams 앱 이름: EMI 프로젝트 통합관리시스템
- Teams 앱 아이콘
- Teams app manifest
- Teams Admin Center 조직 앱 업로드 권한
- 테스트 tenant와 테스트 사용자
- Graph Teams activity notification 권한
- 관리자 동의
- 사용자별 Entra object id 또는 UPN 매핑
- 운영 배포 전 보안/감사 정책 확인

## 4. Teams 앱 manifest 필요성

Activity Feed 알림은 임의 텍스트 POST만으로 끝나지 않는다. Teams 앱 manifest에 activity type, display template, deep link 정책을 정의해야 한다.

manifest에는 최소 다음 항목이 필요하다.

- 앱 이름과 설명
- 앱 아이콘
- valid domains
- activityTypes
- activity display template
- deep link target

이번 TASK에서는 manifest 파일을 생성하지 않는다. 실제 manifest 작성과 Teams Admin Center 업로드는 TASK-NOTIFY-003 구현 단계에서 수행한다.

## 5. activityType 후보

- `workItemCreated`
- `urgentBlocking`
- `reinspectionRequested`
- `dueSoon`
- `overdue`
- `escalated`

각 activity type은 인앱 알림 또는 work item 이벤트와 1:1 또는 N:1로 매핑한다. Pending List가 구현된 후에는 pending 관련 activity type 추가를 검토한다.

## 6. Graph 권한 후보

후속 구현 단계에서 Microsoft Graph Teams activity notification API 권한을 검토한다.

검토 후보:

- Teams activity notification send 계열 권한
- 사용자 또는 Teams 앱 설치 상태 조회 권한, 필요한 경우
- 앱 설치/배포 관련 권한, 필요한 경우

정확한 권한명과 consent 방식은 TASK-NOTIFY-003 시작 시 Microsoft Graph 최신 문서를 기준으로 재확인한다.

## 7. 사용자 매핑 방식

기본 식별자는 TASK-INFRA-001에서 추가된 Entra 사용자 정보를 사용한다.

- 우선 후보: `qms_users.entra_object_id`
- 보조 표시 정보: `qms_users.email`
- 자동 병합 금지: dev user와 Entra user는 이메일이 같아도 자동 병합하지 않는다.
- Activity Feed 실제 발송 대상은 EntraId 사용자로 제한한다.
- dev user persona는 dry-run 또는 테스트 표시용으로만 사용한다.

## 8. Backend 구조 예상

예상 구성:

- `NotificationDispatcher`
- `NotificationDeliveryStore`
- `TeamsActivityFeedHandler`
- `ITeamsActivityFeedClient`
- `TeamsActivityMessageRenderer`
- Graph token provider
- 사용자 Entra object id resolver

인앱 알림은 계속 원본이다. Activity Feed 실패는 업무 흐름을 중단하지 않고 `notification_deliveries`에 실패 상태로 기록한다.

## 9. notification_deliveries channel 확장안

후속 구현 시 channel 후보:

- `TeamsActivityFeed`

상태와 retry 정책은 TASK-NOTIFY-001과 동일하게 유지한다.

- `Pending`
- `Sent`
- `Failed`
- `Suppressed`
- `Disabled`
- `DryRunSent`

중복 억제는 recipient + channel + delivery_type + dedupe_key 기준으로 유지한다.

## 10. 수동 검수 항목

- Teams 앱이 조직 앱으로 업로드되는지
- 테스트 사용자에게 앱 설치 또는 접근이 가능한지
- Activity Feed 알림이 사용자 Teams에 표시되는지
- activity type별 문구와 deep link가 의도대로 보이는지
- 권한 없는 사용자나 approval pending 사용자가 업무 데이터 deep link에 접근하지 못하는지
- 실패 시 `notification_deliveries`에 실패가 기록되는지
- retry가 업무 흐름을 막지 않는지

## 11. 제외 범위

- 이번 TASK-NOTIFY-001에서 Activity Feed 실제 구현
- Graph TeamsActivity.Send 권한 요청 또는 사용
- Teams 앱 manifest 파일 생성
- Teams Admin Center 업로드
- Teams DM 구현
- Pending List 구현
- 예정일 에스컬레이션 구현
- 개인별 알림 선호도 UI

## 12. 리스크와 의사결정 필요 항목

- Teams 앱 manifest와 activity template 설계가 필요하다.
- 조직 앱 배포 권한과 관리자 승인 절차가 필요하다.
- Graph 권한 범위와 관리자 동의 방식 확인이 필요하다.
- 사용자가 Teams 앱을 설치해야 하는지, 조직 앱으로 자동 배포 가능한지 확인해야 한다.
- Activity Feed API는 Teams 앱과 연결되므로 단순 Webhook보다 검수 절차가 길 수 있다.
- deep link는 권한 서버 Policy와 충돌하지 않아야 한다.
- 모바일 Teams에서 알림 표시가 데스크톱과 다를 수 있어 별도 검수가 필요하다.
