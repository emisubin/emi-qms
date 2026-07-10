# TASK-UAT-001 User Manual

## 1. UAT란 무엇인가

UAT는 사용자가 실제 화면과 업무 흐름을 검수하는 개발 환경이다. EMI Development UAT는 저장·수정과 승인된 알림 검수가 가능하며 data가 계속 유지된다. 자동 테스트가 종료 후 data를 버리는 E2E와 다르다.

## 2. HTTP 개발 모드는 언제 사용하는가

일반 화면과 API 기능 개발에는 HTTP Development UAT를 사용한다.

- 주소: `http://localhost:5174`
- 시작: `scripts/dev-uat-start.sh`

## 3. HTTPS Teams 개발 모드는 언제 사용하는가

Teams Activity Feed, Teams tab과 HTTPS deep link를 확인할 때 사용한다.

- 주소: `https://localhost:5174`
- 시작: `scripts/dev-uat-start-teams-https.sh`

HTTPS도 Review-only 환경이 아니다. 저장·수정과 worker가 동작하는 Development mode다.

## 4. HTTPS 서버를 직접 켜는 방법

Repository root에서 다음 명령을 실행한다.

```bash
scripts/dev-uat-start-teams-https.sh
```

Certificate 또는 local notification 설정이 없으면 startup이 이유를 설명하고 중단한다. 실제 설정 값은 화면이나 terminal에 출력하지 않는다.

## 5. 접속 주소

- 메인: `https://localhost:5174`
- Teams Activity: `https://localhost:5174/teams/activity`
- 관리자: `https://localhost:5174/admin`
- 알림 발송 상태: `https://localhost:5174/admin/system/notification-deliveries`
- 수동 알림 발송: `https://localhost:5174/admin/system/send-notification`

## 6. 서버가 정상인지 확인하는 방법

정상 화면에는 다음 특징이 있다.

- API 상태 `ok`
- Database 상태 `reachable`
- User 역할 표시
- “서버에 연결할 수 없습니다” 없음
- 프로젝트, 내 업무와 알림 화면 조회 가능
- Teams Activity와 관리자 화면이 열림

## 7. 서버에 연결할 수 없을 때

1. 주소가 HTTP인지 HTTPS인지 확인한다.
2. 5174 listener와 backend 5081 health를 확인한다.
3. Startup terminal의 첫 실패 메시지를 확인한다.
4. 다른 process가 port를 사용 중이면 임의 종료하지 않는다.
5. PostgreSQL container나 volume을 reset하지 않는다.

## 8. ERR_SSL_PROTOCOL_ERROR 대응

HTTPS 주소에 HTTP server가 떠 있을 때 자주 발생한다. `https://localhost:5174`와 현재 시작 script가 일치하는지 확인한다. Certificate 오류를 `--insecure` 옵션으로 정상 처리하지 말고 local certificate trust를 확인한다.

## 9. 5174 port 충돌의 의미

5174는 UAT frontend 전용 고정 port다. 다른 process가 이미 사용하면 Vite는 5175 같은 다른 port로 이동하지 않고 실패한다. Startup script도 repository 소유가 아닌 process를 종료하지 않는다.

## 10. HTTP와 HTTPS를 동시에 쓸 수 없는 이유

두 mode가 같은 5174를 사용하기 때문이다. 같은 port에 HTTP server와 HTTPS server를 동시에 둘 수 없다. Mode 전환은 현재 repository session을 확인한 뒤 한쪽을 안전하게 교체하는 방식이다.

## 11. Teams 앱 화면 확인 방법

1. HTTPS Development UAT를 시작한다.
2. `https://localhost:5174/teams/activity`가 열리는지 확인한다.
3. Teams 앱에서도 Activity tab과 deep link를 확인한다.
4. 실제 알림 발송은 승인된 수신자와 채널이 있을 때만 수행한다.

자동 검증에서는 실제 알림을 발송하지 않았다.

## 12. Data가 유지되는지 확인하는 방법

Startup 전후 프로젝트와 업무를 조회하고 PostgreSQL health, latest migration과 주요 count를 비교한다. UAT worker가 실행 중이면 notification이 자연 변경될 수 있으므로 E2E 또는 사용자 동작과 구분한다.

UAT DB를 삭제하거나 초기화해 확인하지 않는다.

## 13. E2E와 UAT의 차이

- UAT: persistent PostgreSQL, 사용자 검수 data 유지, Development worker/provider 설정
- E2E: 실행별 전용 PostgreSQL container/network/tmpfs, test 종료 후 cleanup, actual provider 차단

Full-Stack E2E가 UAT DB나 volume을 사용하면 안 된다.

## 14. 하면 안 되는 작업

- UAT DB drop/truncate/reset
- Persistent PostgreSQL stop/restart
- Docker persistent volume 삭제
- 테스트 data hard delete
- 다른 process 강제 종료
- Env/secret/certificate/private key 출력
- 승인 없는 실제 Teams/Mail/Channel 발송
- HTTP와 HTTPS server 동시 실행 시도

## 15. FAQ

### Vite가 5175로 자동 이동하나요?

아니다. Strict port가 적용돼 5174를 사용할 수 없으면 시작에 실패한다.

### PID file이 오래됐으면 그 PID를 종료하나요?

아니다. 실제 listener의 cwd와 command를 다시 확인한다. Startup 성공 후 PID file을 실제 listener PID로 갱신한다.

### `.env.notify-local`을 직접 source해도 되나요?

안 된다. Startup script의 literal parser를 사용해야 하며 설정 값은 출력하지 않는다.

### HTTPS에서는 실제 알림이 항상 발송되나요?

Development actual 설정이 구성돼 있어도 delivery 생성과 발송은 업무 동작과 사용자 승인에 따라 발생한다. 이번 자동 검증은 새 발송을 만들지 않았다.

### Review-only로 확인할 수 있나요?

현재 UAT-001은 Development mode다. Read-only Review-safe mode는 `TASK-UAT-002`에서 별도로 구현한다.

### 자동 테스트가 통과하면 Task가 완료인가요?

아니다. Checklist 작성, 자동 검증과 사용자 검수 완료는 서로 다른 상태다. TASK-UAT-001은 각 상태를 구분해 기록한 뒤 사용자 검수를 완료했다.

## 16. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 완료`.

- [x] HTTPS 메인과 Teams Activity에 접속 가능
- [x] 관리자와 수동 알림 화면에 접속 가능
- [x] 프로젝트·내 업무·알림을 조회 가능
- [x] 저장·수정 가능한 Development mode임을 이해함
- [x] Actual 알림은 별도 승인 범위에서만 검수하는 원칙을 이해함
- [x] 5174 충돌 시 다른 port로 이동하지 않음
- [x] 다른 process를 자동 종료하지 않음
- [x] 기존 UAT DB와 volume이 유지됨
- [x] E2E와 UAT가 분리됨
- [x] Console error와 narrow overflow가 없음
- [x] SOP를 따라 server를 직접 시작할 수 있음

검수 증빙: Task 승인자 / 2026-07-10 / PR #23 및 HTTPS Development UAT / 승인 / 현재 대화의 명시적 검수·병합 승인.
