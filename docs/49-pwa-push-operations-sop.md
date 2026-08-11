# EMI PMS PWA 푸시 운영 절차

## 목적과 기본 상태

PWA 푸시는 기존 인앱 알림을 사용자가 허용한 휴대폰·태블릿에 추가로 전달한다. 인앱 알림, Teams Activity와 메일의 수신 정책은 변경하지 않는다.

- 기본값: `Enabled=false`, `DryRun=true`
- 운영 VAPID key와 실제 외부 발송은 별도 승인 전 활성화하지 않는다.
- 푸시 장애가 업무 저장이나 인앱 알림 생성을 되돌리지 않는다.

필수 운영 설정:

- `Notifications:WebPush:Subject`: 운영 연락처 URI. 일반적으로 `mailto:` 주소를 사용한다.
- `Notifications:WebPush:PublicKey`: 브라우저에 제공할 VAPID 공개키.
- `Notifications:WebPush:PrivateKey`: 실제 발송 모드에서만 secret 저장소로 주입하는 VAPID 비밀키.
- `Notifications:WebPush:AllowedEndpointHostSuffixes`: 실제 사용하는 브라우저 Push 서비스의 신뢰 호스트 suffix만 허용한다. 내부 도메인, IP 주소 또는 임의 host를 추가하지 않는다.
- `Notifications:WebPush:MaxActiveDevicesPerUser`: 한 계정이 연결할 수 있는 최대 기기 수. 기본값은 10이며 허용 범위는 1~100이다.

VAPID key 형식, 허용 host 목록 또는 기기 상한이 올바르지 않으면 Backend 시작 검증이 실패한다. 구독 등록 시에도 허용되지 않은 endpoint와 잘못된 암호화 key를 거부한다.

## 최초 활성화 순서

1. 운영 DB backup·migration 상태를 확인하고 additive migration `0074_web_push_subscriptions`를 적용한다.
2. VAPID public/private key를 승인된 secret 저장소에서 생성·보관한다. private key를 Repository, 문서, 화면, 일반 로그에 기록하지 않는다.
3. Backend 환경값에 Subject, PublicKey, 허용 endpoint host와 기기 상한을 넣고 `Enabled=true`, `DryRun=true`로 시작한다. PrivateKey는 실제 발송 전환 전까지 주입하지 않아도 된다.
4. Backend를 교체하고 migration ledger가 `Exact`, latest `0074_web_push_subscriptions`인지 확인한다.
5. 검수 계정의 설치형 PWA에서 기기 푸시를 켜고, 새 인앱 알림에 기기별 `DryRunSent` delivery가 생기는지 확인한다.
6. 실제 push service 호출은 별도 승인과 실기기 검수 계획을 받은 뒤에만 `DryRun=false`로 전환한다.

## 운영 확인

- 관리자 알림 발송 모니터의 `PWA 푸시` 행은 기기별 한 행이다. `푸시 서비스 접수`는 브라우저 Push 서비스가 요청을 받았다는 뜻이며 사용자 수신·열람을 뜻하지 않는다.
- 기기는 endpoint가 아니라 `PWA 기기 XXXXXXXX` 형식의 비식별 표시로만 확인한다.
- 404/410과 영구 4xx는 만료·잘못된 구독으로 비활성화한다. 429, 일시적 5xx, network timeout은 기존 worker 재시도 정책을 따른다.
- 사용자가 `모든 기기 연결 해제`를 실행하면 그 사용자의 푸시만 모두 꺼진다. Microsoft 365 로그인은 원격 해제되지 않는다.
- 계정을 비활성화하거나 삭제 예약하면 해당 계정의 모든 푸시 구독도 사유와 함께 비활성화된다. 계정을 복구해도 예전 기기 구독은 자동으로 다시 켜지지 않으며 사용자가 보유 기기에서 직접 다시 허용해야 한다.
- 발송 대기 중 기기 연결이 해제·재등록되면 이전 연결 세대의 delivery는 새 연결로 넘겨 보내지 않고 `발송 제외` 처리한다.

## 장애·중지·복구

1. 오발송 또는 provider 장애 시 Web Push `Enabled=false`로 채널을 중지한다. 인앱·Teams·메일은 계속 동작한다.
2. 실패 delivery의 상태·오류 코드·attempt를 확인하되 endpoint와 암호화 key를 조회하거나 복사하지 않는다.
3. 채널을 다시 켜도 중지 기간의 과거 알림은 소급 발송하지 않는다. 새 알림부터 처리한다.
4. migration 적용 뒤 schema를 삭제해 되돌리지 않는다. application rollback 또는 새 additive migration의 forward-fix를 사용한다.

## 보안 주의

- 구독 endpoint, `p256dh`, `auth`, VAPID private key는 secret이다.
- 네트워크 오류 로그에도 구독 endpoint 원문을 남기지 않는다. 관리자 화면은 비식별 기기 표지만 사용한다.
- 분실 기기는 EMI PMS의 `모든 기기 연결 해제`와 별도로 회사 Microsoft 365 보안 절차에서 세션을 해제한다.
- 푸시 잠금 화면에는 제목 수준 정보만 표시한다. 상세 업무 내용은 로그인·권한 확인 뒤 EMI PMS 알림 상세에서 확인한다.
