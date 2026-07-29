# TASK-UAT-001 User Manual

## 1. UAT란 무엇인가

UAT는 사용자가 실제 화면과 업무 흐름을 검수하는 개발 환경이다. EMI Development UAT는 저장·수정과 승인된 알림 검수가 가능하며 data가 계속 유지된다. 자동 테스트가 종료 후 data를 버리는 E2E와 다르다.

## 2. HTTP 개발 모드는 언제 사용하는가

Change 001 이후 HTTP 5174는 사용하지 않는다. 로그인, 일반 화면, API 기능과 알림 검수를 모두 HTTPS Development UAT에서 수행한다.

## 3. HTTPS Development UAT는 언제 사용하는가

로그인, 일반 기능, Teams Activity Feed, Teams tab과 HTTPS deep link를 한 주소에서 확인할 때 사용한다.

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

1. 주소가 `https://localhost:5174`인지 확인한다.
2. 5174 listener와 backend 5081 health를 확인한다.
3. Startup terminal의 첫 실패 메시지를 확인한다.
4. 다른 process가 port를 사용 중이면 임의 종료하지 않는다.
5. PostgreSQL container나 volume을 reset하지 않는다.

## 8. ERR_SSL_PROTOCOL_ERROR 대응

HTTPS 주소에 HTTP server가 떠 있을 때 자주 발생한다. `https://localhost:5174`와 현재 시작 script가 일치하는지 확인한다. Certificate 오류를 `--insecure` 옵션으로 정상 처리하지 말고 local certificate trust를 확인한다.

## 9. 5174 port 충돌의 의미

5174는 UAT frontend 전용 고정 port다. 다른 process가 이미 사용하면 Vite는 5175 같은 다른 port로 이동하지 않고 실패한다. Startup script도 repository 소유가 아닌 process를 종료하지 않는다.

## 10. HTTP와 HTTPS를 동시에 쓸 수 없는 이유

두 protocol이 같은 5174를 사용하기 때문이다. 같은 port에 HTTP server와 HTTPS server를 동시에 둘 수 없다. Change 001 이후에는 HTTPS만 운영하며 HTTP mode로 전환하지 않는다.

## 11. Teams 앱 화면 확인 방법

1. HTTPS Development UAT를 시작한다.
2. `https://localhost:5174/teams/activity`가 열리는지 확인한다.
3. Teams 앱에서도 Activity tab과 deep link를 확인한다.
4. 실제 알림 발송은 승인된 수신자와 채널이 있을 때만 수행한다.

최초 자동 검증에서는 실제 알림을 발송하지 않았다. 2026-07-14 별도 승인 검수에서는 기존 terminal audit를 보존하고 신규 ManualTest Teams Activity 1건만 실제 발송했다.

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
- HTTP 5174 시작 또는 HTTP/HTTPS server 동시 실행 시도

## 15. FAQ

### Vite가 5175로 자동 이동하나요?

아니다. Strict port가 적용돼 5174를 사용할 수 없으면 시작에 실패한다.

### PID file이 오래됐으면 그 PID를 종료하나요?

아니다. 실제 listener의 cwd와 command를 다시 확인한다. Startup 성공 후 PID file을 실제 listener PID로 갱신한다.

### `.env.notify-local`을 직접 source해도 되나요?

안 된다. Startup script의 literal parser를 사용해야 하며 설정 값은 출력하지 않는다.

### HTTPS에서는 실제 알림이 항상 발송되나요?

Development actual 설정이 구성돼 있어도 delivery 생성과 발송은 업무 동작과 사용자 승인에 따라 발생한다. 최초 자동 검증은 새 발송을 만들지 않았고, 이후 별도 승인 범위에서 신규 ManualTest Teams Activity 1건만 Microsoft Graph로 실제 처리했다.

### Review-only로 확인할 수 있나요?

현재 UAT-001은 Development mode다. Read-only Review-safe mode는 `TASK-UAT-002`에서 별도로 구현한다.

### 자동 테스트가 통과하면 Task가 완료인가요?

아니다. Checklist 작성, 자동 검증과 사용자 검수 완료는 서로 다른 상태다. TASK-UAT-001은 각 상태를 구분해 기록한 뒤 사용자 검수를 완료했다.

## 16. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 완료`.

이 상태는 최초 Task의 역사적 검수 결과다. 현재 Change 001 검수 상태는 다음 17장을 따른다.

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

## 17. Change 001 사용자 검수

상태: `자동 검증 완료`, `사용자 검수 완료`, `Microsoft Graph actual 발송 검수 완료`, `Teams client 수신 검수 완료`, `PR #48 squash merge 승인`.

- [x] `https://localhost:5174` 실제 Microsoft 365 로그인
- [x] 로그인 상태 유지와 재인증
- [x] 알림 목록과 Teams Activity 기존 항목 조회
- [x] 승인된 신규 ManualTest Teams Activity 1건만 실제 발송
- [x] 실제 외부 delivery 검수 전 Backend 5081 Notification Delivery Worker 활성 상태 확인
- [x] Teams Activity channel actual 활성화 후 Microsoft Graph `Sent`
- [x] 기존 `TeamsActivityDisabled` terminal 2건 audit 보존
- [x] Teams client Activity Feed에서 신규 알림 표시 확인
- [x] HTTP 5174가 열리지 않음
- [x] Design preview 5176 유지
- [x] Review-safe 5190 유지

Design experiment worktree는 자동 동기화되지 않는다. 디자인 변경을 독립 commit으로 고정한 뒤 main 변경을 디자인 branch에 merge하면 디자인을 유지하면서 나머지 변경을 가져올 수 있다. 같은 줄의 충돌은 수동 검수해야 하며, commit·merge는 별도 승인 전 수행하지 않는다.

현재 Backend 5081은 Notification Delivery Worker만 활성이고 Escalation·Purge worker는 비활성이다. Teams Activity channel은 actual mode로 활성화돼 있다. 기존 `TeamsActivityDisabled` terminal 2건은 audit로 보존했고 신규 ManualTest delivery 1건은 두 번의 설정 누락 retry 뒤 같은 delivery의 세 번째 시도에서 Microsoft Graph `Sent`로 완료됐다. 사용자가 Teams client Activity Feed의 실제 알림 표시까지 확인했다.

## 18. Change 004 공개 서비스 보안 안내

상태: `Checklist 작성됨`, `자동 검증 완료`, `실제 운영 환경 검수 대기`.

- 평소 사용자는 기존과 같이 Microsoft 365로 로그인한다.
- 너무 많은 요청을 짧은 시간에 반복하면 `요청이 너무 많습니다` 안내가 나오며 잠시 후 다시 시도한다.
- 업로드 파일에서 악성코드가 발견되거나 안전 검사를 수행할 수 없으면 파일은 업무 데이터로 저장되지 않는다.
- 촬영 위치·기기 정보가 남은 이미지는 차단될 수 있다. 휴대폰에서 위치 정보를 제거하거나 새로 내보낸 뒤 다시 업로드한다.
- 운영 서비스는 등록된 HTTPS domain으로만 사용한다. IP 주소, HTTP 주소와 개발용 port로 접속하지 않는다.
- scanner, DB, Entra 또는 보안 모니터링 준비가 불완전하면 운영 서비스가 시작되지 않는 것이 정상이다.

실제 운영 검수에서는 로그인, 주요 조회·수정, 파일 upload, 차단 안내, rate-limit 복구와 로그 경보 수신을 확인한다. 실제 domain·certificate·Entra·managed DB·SIEM이 아직 전달되지 않았으므로 이 항목은 운영 전환 Task에서 완료한다.

자동 검증 완료 항목:

- [x] 잘못된 운영 설정과 만료 임박 인증서 시작 전 차단
- [x] Host·보안 header·rate limit·upload 안전 검사
- [x] Backend·Frontend·실제 스택 전체 회귀
- [x] Production container와 dependency 취약점 검사

실제 운영 환경 검수 대기 항목:

- [ ] 실제 HTTPS domain과 Microsoft 365 로그인
- [ ] managed DB TLS와 restore 증빙
- [ ] SIEM 경보 수신과 비상 관리자 접근
- [ ] 실제 주요 업무 조회·수정·upload·rate-limit 복구

## 19. Change 005 공유 PC와 업데이트 보안 안내

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 완료`.

- 로그인한 사용자의 업무 응답은 브라우저 cache에 보관하지 않도록 변경됐다.
- 화면 프로그램 파일은 빠른 표시를 위해 cache할 수 있지만, 새 버전이 배포되면 첫 HTML에서 최신 버전을 확인한다.
- 로그아웃 뒤 브라우저의 뒤로 가기를 눌러도 이전 업무 내용이 다시 보여서는 안 된다.
- 이 변경은 화면 기능, 입력 방식, 권한과 업무 data를 바꾸지 않는다.

사용자 검수:

- [x] Microsoft 365 로그인 후 Dashboard와 주요 업무 화면이 정상 표시됨
- [x] 조회·수정·파일 업로드·알림이 기존과 같이 동작함
- [x] 로그아웃 뒤 뒤로 가기에서 보호된 업무 내용이 다시 표시되지 않음
- [x] 새 배포 뒤 강제 새로고침 없이 최신 HTML과 정상 asset이 표시됨
- [ ] 실제 운영 domain 검수는 운영 전환 Task에서 별도로 완료함

## 20. Change 006 쉬운 사용자 검수

상태: `자동 검증 완료`, `사용자 검수 완료 — 2026-07-30`.

검수 주소: `https://localhost:5174`

아래 여섯 가지만 순서대로 확인한다. 테스트용 프로젝트를 사용하고 실제 고객 자료는 새로 올리지 않는다.

1. **로그인**
   - `LOGIN`을 누르고 회사 Microsoft 365 계정으로 로그인한다.
   - 로그인 뒤 오류 화면이 아니라 Home이 나오면 통과다.
2. **기본 화면**
   - Home의 숫자와 목록이 보이는지 확인한다.
   - 프로젝트 목록에서 프로젝트 하나를 열어 각 탭의 기존 데이터가 보이는지 확인한다.
3. **내 권한**
   - 본인 부서 업무에는 입력·수정 버튼이 보이는지 확인한다.
   - 다른 부서 업무는 조회만 되는지 확인한다.
4. **저장**
   - 테스트 프로젝트에서 본인이 담당하는 값 하나를 수정하고 저장한다.
   - 화면을 새로고침해도 저장한 값이 남으면 통과다.
5. **알림**
   - `알림`과 `내 업무`를 열어 목록이 정상 표시되는지 확인한다.
   - 새 실제 Teams·메일 발송은 이번 검수에서 만들지 않는다.
6. **로그아웃 보안**
   - 로그아웃한 뒤 브라우저의 `뒤로`를 누른다.
   - 방금 보던 업무 화면과 데이터가 다시 보이지 않고 로그인 화면이 나오면 통과다.

문제가 있으면 “몇 번에서 실패했는지 + 화면에 나온 문구”만 알려준다. 사용자·고객 이름, 이메일, token과 인증 화면 상세값은 채팅에 복사하지 않는다.

검수 결과: 사용자가 실제 Microsoft 365 로그인, 기본 화면, 권한별 조회·수정, 저장 유지, 알림·내 업무와 로그아웃 뒤 보호 화면 비노출을 완료했다고 확인했다.
