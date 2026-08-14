# TASK-PWA-PUSH-001 — 사용자 검수 체크리스트

> 상태: 자동 검증·운영 실기기 사용자 검수 완료
> 환경: Azure 운영(`Enabled=true`, `DryRun=false`), VAPID Key Vault 참조

## 자동 검증

- [x] 한 사용자가 여러 기기 구독을 갖고 각 기기별 delivery가 만들어지는 schema다.
- [x] `RecipientOnly`·`Authenticated`의 실제 인앱 가시성과 일치하고 `AdminOnly`, 비활성·삭제 예약·승인 대기 사용자는 제외된다.
- [x] 구독 활성 전 과거 알림은 소급 delivery를 만들지 않는다.
- [x] 구독을 다시 켠 뒤 이전 세대의 늦은 전송 결과가 현재 구독을 비활성화하지 않는다.
- [x] dry-run은 외부 push service를 호출하지 않는다.
- [x] 외부 호출 감사 표시는 사전 검증 뒤 실제 프로토콜 호출 직전에만 기록되고 endpoint 원문은 로그에 남지 않는다.
- [x] 404/410 영구 오류와 429/5xx/timeout 재시도 오류를 구분한다.
- [x] 구독 API 응답·푸시 payload·관리자 기기 표시에 endpoint와 암호화 key 원문이 없다.
- [x] 본인 알림 설정에만 기기 제어가 있고 관리자 사용자 지원 화면에는 없다.
- [x] 브라우저 Service Worker·로컬 구독 해제가 실패해도 서버 기준 현재 기기·전체 기기 해제와 재시도가 가능하다.
- [x] 계정 비활성화·삭제 예약은 모든 구독을 해제하고 계정 복구만으로 자동 재활성화하지 않는다.
- [x] 관리자 전송 모니터는 Web Push `Sent`를 사용자 수신 완료가 아닌 `푸시 서비스 접수`로 표시한다.
- [x] 최소 Service Worker에 `push`·`notificationclick`만 있고 fetch cache·background sync가 없다.
- [x] Frontend 전체 단위 검사, typecheck와 production build가 통과했다.
- [x] Isolated Full-Stack dry-run 검증이 통과했다.
- [x] Backend 전체 회귀 `513/513`이 실패·건너뜀 없이 통과했다.

## 실제 기기 사용자 검수

- [x] Android 설치형 EMI PMS에서 사용자 선택으로 푸시를 켜고 실제 알림을 받았다.
- [x] iPhone 홈 화면 설치형 EMI PMS에서 사용자 선택으로 푸시를 켜고 실제 알림을 받았다.
- [x] 서로 다른 3개 활성 사용자 계정에서 Teams와 PWA 알림을 모두 받았다.
- [x] 푸시를 누르면 로그인·권한 확인 뒤 정확한 인앱 알림 상세로 이동했다.
- [x] 활성 기기마다 인앱 알림과 연결된 Web Push delivery가 생성되고 provider가 접수했다.
- [x] PWA 설치·알림 허용은 직원 자율이며, 미등록·미허용 사용자는 인앱 알림을 유지하고 PWA만 받지 않는 정책을 사용자가 확인했다.

다중 기기 해제·로그아웃·OS 차단 복구는 자동·격리 검증으로 완료했다. 실제 분실 기기나 의도적인 운영 장애를 만드는 destructive 검수는 수행하지 않았고 운영 절차로 보존한다.

## 판정

- 자동 검증 판정: PASS
- 사용자 검수 판정: 완료
- Git 게시·Azure migration/runtime 적용: PR #103, exact main SHA `58c089993587deea30513cb6edee0b8396a1d474`, release `31786040822` 완료
- 실제 외부 발송·운영 key·실기기 수신: 완료
- 직원별 PWA 등록: 중앙 완료 조건이 아닌 사용자 선택 사항
