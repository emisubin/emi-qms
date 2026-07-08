# TASK-NOTIFY-003 운영 SOP

## 1. 문서 목적

이 문서는 EMI 프로젝트 통합관리시스템의 알림 운영 절차를 정의한다. 운영자는 이 SOP를 기준으로 수동 알림 발송, 업무 배정 알림, 발송 상태 모니터링, 장애 대응, Teams local test를 수행한다.

## 2. 적용 범위

- 인앱 알림
- Teams Activity Feed 개인 알림
- Teams 채널 공지
- Mail 알림
- 관리자 수동 발송
- 알림발송상태 모니터링
- 발송 실패/대기 처리
- HTTPS local Teams test

운영 Teams manifest 배포, 운영 secret 관리, Teams 채널 멤버십 동기화는 별도 운영 절차와 함께 관리한다.

## 3. 용어 정의

- 인앱 알림: 시스템 안의 `notifications` 원본 알림.
- 외부 알림: TeamsActivity, TeamsChannel, Mail 같은 외부 채널 발송.
- TeamsChannel: Teams 채널에 게시하는 Webhook/Adaptive Card 알림.
- TeamsActivity: Teams Activity Feed에 표시되는 개인 알림.
- Mail: SMTP provider로 발송되는 메일 알림.
- notification: 알림 원본 row.
- notification_recipient: 개인 알림 수신자 row.
- notification_delivery: 외부 채널 발송 이력 row.
- visibility_scope: 알림 상세 접근 범위. `RecipientOnly`, `Authenticated`, `AdminOnly`.
- queue: 발송 요청을 `Pending` delivery로 저장하고 worker가 처리하는 구조.
- retry: 대기 delivery의 다음 시도 시각을 앞당겨 worker가 다시 처리하게 하는 운영 조치.

## 4. 역할과 책임

- System Administrator
  - 수동 알림 발송
  - 알림발송상태 모니터링
  - 실패/대기 확인 처리
  - 운영 설정 이상 여부 1차 확인
- 일반 사용자
  - Teams Activity, Mail, 인앱 알림 확인
  - 알림 상세 읽음 처리
  - 관련 업무 처리
- 총무/Teams 관리자
  - Teams 앱 승인/배포
  - Teams 권한/앱 설치 정책 확인
  - 운영 manifest URL 관리
- 시스템 운영자
  - backend worker 실행 상태 확인
  - env/secret 주입
  - SMTP/Webhook/Graph 오류 대응

## 5. 알림 구조 원칙

- 인앱 `notifications`가 모든 알림의 원본이다.
- 개인 알림은 `notification_recipients`로 수신자를 기록한다.
- TeamsActivity / TeamsChannel / Mail은 `notification_deliveries`로 추적한다.
- 개인 알림 상세는 수신자만 조회한다.
- 채널 공지는 현재 Teams 멤버십을 동기화하지 않으므로 로그인된 active 사용자에게 공개한다.
- 외부 발송 실패가 발생해도 인앱 알림 원본은 유지한다.

## 6. 알림 발송 운영 절차

### 6.1 개인 알림 발송

1. 관리자 페이지에서 `알림 수동 발송`으로 이동한다.
2. 발송 유형을 `개인 알림`으로 선택한다.
3. 제목과 내용을 입력한다.
4. Teams Activity 또는 Mail 중 하나 이상을 선택한다.
5. Teams Activity 수신자는 active EntraId 사용자만 선택한다.
6. Mail 수신자는 사용자 또는 이메일을 입력한다.
7. `발송`을 누른다.
8. 버튼 근처의 접수 메시지를 확인한다.
9. 알림발송상태에서 channel별 `Pending` 또는 `Sent` 결과를 확인한다.

### 6.2 채널 공지 발송

1. 발송 유형을 `채널 공지`로 선택한다.
2. 제목과 내용을 입력한다.
3. Teams 채널 게시가 설정되어 있는지 확인한다.
4. `발송`을 누른다.
5. 알림발송상태에서 TeamsChannel delivery를 확인한다.
6. 채널 공지 상세는 로그인된 active 사용자가 조회 가능하다.

### 6.3 업무 배정 발송

1. 발송 유형을 `업무 배정`으로 선택한다.
2. 기존 프로젝트를 선택한다.
3. 업무 제목과 업무 내용을 입력한다.
4. 담당자를 한 명 이상 선택한다.
5. 필요하면 업무 단계와 예정일을 입력한다.
6. 외부 채널 Teams Activity 또는 Mail을 선택할 수 있다.
7. 외부 채널을 선택하지 않아도 인앱 알림과 내 업무는 생성된다.
8. `발송`을 누른다.
9. 담당자 내 업무 목록에 업무가 생성됐는지 확인한다.

## 7. 업무 배정 알림 처리 절차

1. 업무 배정 알림 발송 후 알림발송상태 상세를 연다.
2. notification id와 work_item 연결이 있는지 확인한다.
3. 수신자 계정으로 내 업무 목록을 확인한다.
4. 알림 상세에서 `관련 업무 보기` 버튼을 확인한다.
5. 관련 프로젝트가 있으면 `관련 프로젝트 보기` 버튼을 확인한다.
6. 업무가 아닌 개인 알림/채널 공지에서는 work_item이 생성되지 않아야 한다.

## 8. 알림발송상태 모니터링 절차

1. 관리자 페이지에서 `알림 발송 상태`로 이동한다.
2. 상태 탭으로 전체, 미처리 실패, 미처리 대기, 발송 완료, 확인됨, 제외됨을 확인한다.
3. 채널 필터로 TeamsChannel, TeamsActivity, Mail을 구분한다.
4. row 제목을 클릭해 상세를 확인한다.
5. 실패 건은 오류 코드와 조치 안내를 확인한다.
6. 대기 건은 다음 시도 시각과 worker 상태를 확인한다.

### 8.1 실패 확인 처리

1. 실패 탭에서 대상 row를 선택한다.
2. `확인 처리` 또는 `제외 처리`를 누른다.
3. 버튼 근처의 처리 결과를 확인한다.
4. 확인/제외 처리된 실패 건은 dashboard 실패 count에서 제외된다.
5. 실제 발송 상태는 변경하지 않는다.

### 8.2 대기 재발송

1. 대기 탭에서 대상 row를 선택한다.
2. `재발송`을 누른다.
3. 다음 시도 시각이 현재 시각 이하로 조정된다.
4. worker가 다음 주기에 처리한다.
5. attempt_count는 worker 처리 시 증가한다.

### 8.3 대기 확인/제외 처리

1. 오래된 pending 또는 의도적으로 제외할 대기 row를 선택한다.
2. 확인/제외 처리한다.
3. dashboard 대기 count에서 제외된다.
4. delivery row는 삭제하지 않는다.

## 9. 장애 대응 절차

### 9.1 TeamsActivity 실패

- `TeamsActivityPermissionDenied`: Graph 권한과 관리자 동의를 확인한다.
- `TeamsActivityAppNotInstalled`: Teams 앱 설치/앱 정책을 확인한다.
- `TeamsActivityInvalidActivityType`: manifest activityTypes를 확인한다.
- `TeamsActivityInvalidTopic`: Teams deep link, validDomains, HTTPS URL을 확인한다.
- `TeamsActivityUserNotFound`: Entra object id와 사용자 상태를 확인한다.
- 429/5xx: 일시 오류 가능성이 있으므로 retry 상태를 확인한다.

### 9.2 Mail 실패

- `RecipientEmailMissing`: 수신자 이메일을 확인한다.
- `SmtpAuthenticationFailed`: SMTP 계정과 앱 비밀번호를 확인한다.
- `SmtpConnectionFailed`: SMTP host/port/TLS 설정을 확인한다.
- `SmtpSendFailed`: SMTP 서버 응답과 수신자 주소를 확인한다.

### 9.3 TeamsChannel 실패

- Teams Webhook URL 설정 여부를 확인한다.
- Power Automate/Teams action 실행 결과를 확인한다.
- Adaptive Card payload 오류를 확인한다.
- Webhook URL은 로그/보고서에 원문 출력하지 않는다.

### 9.4 Pending 장기 대기

- backend worker가 실행 중인지 확인한다.
- `Notifications:Dispatch:Enabled` 설정을 확인한다.
- `next_attempt_at_utc`가 미래인지 확인한다.
- admin handling status가 확인/제외인지 확인한다.
- worker 중단이면 backend를 재기동한다.

## 10. HTTPS Teams local test 절차

1. `mkcert`가 없으면 설치한다.
2. 로컬 CA를 설치한다.
3. `.certs/localhost.pem`, `.certs/localhost-key.pem`을 생성한다.
4. 인증서/키는 commit하지 않는다.
5. `scripts/dev-uat-start-teams-https.sh`를 실행한다.
6. `https://localhost:5174/teams/activity`가 열리는지 확인한다.
7. Teams manifest local test URL은 `https://localhost:5174/teams/activity`를 사용한다.
8. HTTPS frontend의 `/api` 호출은 Vite proxy로 backend `http://localhost:5081`에 전달된다.

## 11. 보안 주의사항

- `.env`, `.env.notify-local`, `.env.entra-local`은 commit하지 않는다.
- 인증서/키는 commit하지 않는다.
- Teams manifest/icon은 repo에 commit하지 않는다.
- SMTP password, Teams Webhook URL, Graph token, Authorization header, ClientSecret은 출력하지 않는다.
- 개인 알림 상세는 권한 검사 없이 공개하지 않는다.
- 사용자 URL에는 token/secret/correlation id를 넣지 않는다.

## 12. 운영 전 체크리스트

- [ ] 운영 Teams manifest URL 교체
- [ ] Teams deep link base URL 운영 도메인 적용
- [ ] Teams org catalog app id 확인
- [ ] Graph TeamsActivity.Send 권한/동의 확인
- [ ] 운영 Teams Webhook secret 주입
- [ ] 운영 SMTP secret 주입
- [ ] Production dev auth 비활성
- [ ] AdminUserSwitch 비활성
- [ ] worker 실행 상태 모니터링
- [ ] 운영 알림 테스트 계획 수립

## 13. 사용자 검수 체크리스트

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

## 14. 롤백/복구 방안

- 발송 실패 row는 삭제하지 않고 확인/제외 처리한다.
- Pending 장기 대기 건은 worker 설정 확인 후 retry한다.
- 잘못 보낸 채널 공지는 Teams 채널에서 운영자가 별도 공지로 정정한다.
- 잘못 생성한 업무는 기존 업무 취소/상태 변경 절차를 사용한다.
- DB drop/truncate 또는 Docker volume 삭제로 복구하지 않는다.
- 실제 secret 유출 의심 시 즉시 Webhook/SMTP/Graph secret을 회전한다.

## 15. 변경 이력

| 날짜 | 변경 내용 |
| --- | --- |
| 2026-07-08 | TASK-NOTIFY-003 final SOP 작성 |
