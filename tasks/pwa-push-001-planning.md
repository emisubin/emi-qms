# TASK-PWA-PUSH-001 — PWA 모바일 푸시 알림 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/pwa-push-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 모바일 현장 사용자는 설치형 EMI PMS PWA가 닫혀 있으면 인앱 알림을 볼 수 없고, Teams·메일을 확인하거나 앱을 다시 열어야 한다.
- 대상 사용자·역할: EMI PMS 일반 사용자(본인 기기 구독 관리·본인 알림 수신), 제조·품질 모바일 사용자(닫힌 앱 상태에서 긴급 Pending·업무 알림 수신), System Administrator(delivery 실패 확인과 승인된 재처리만).
- 정상 흐름: 설치형 PWA에서 사용자 버튼 행동으로 권한 요청 → 허용 시 기기 구독 저장 → 새 인앱 알림 생성 시 같은 알림의 Web Push delivery 생성 → 기기 알림 표시 → 선택 시 인증·권한 확인 뒤 해당 인앱 알림 상세로 이동.
- 예외·복구 흐름: 미지원 브라우저/미설치 PWA/권한 거절·차단/만료 구독을 각각 구분해 안내하고, 브라우저에서 차단한 경우 앱이 직접 되돌릴 수 없음을 안내한다. 만료·반복 실패 구독은 서버가 자동 비활성화한다. 푸시 실패는 인앱 알림과 업무 transaction에 영향을 주지 않는다.
- 확정한 정책과 명시적 제외(Round 1 결정 5개 모두 `A`):
  1. 권한 요청은 설치형 PWA 첫 로그인 후 1회 사전 안내 + 설정 화면 상시 진입점. 사용자 버튼 행동으로만 브라우저 권한 요청을 시작한다.
  2. 끄는 범위는 기기 구독 단위 on/off만. 켠 기기는 인앱 알림 전체를 푸시로 받는다(이벤트별 선택 없음).
  3. 현재 기기만 사용자가 관리하고 서버가 만료·해지·반복 실패 구독을 자동 정리한다(원격 기기 관리 화면 없음).
  4. 푸시 내용은 인앱 알림 제목 수준 요약만, 클릭 시 해당 인앱 알림 상세로 이동.
  5. 구독 활성 이후의 새 알림만 대상(소급 발송 없음), Web Push 전용 kill switch와 dry-run 선검증.
  - 제외: offline app shell cache·background sync, Teams·메일 정책 재설계, 인앱보다 자세한 내용 노출, 소급 푸시, 무동의 권한 요청, 실제 운영 key 생성·provider 발송·Persistent UAT migration·Azure 교체.
- planning으로 넘긴 비차단 미결정 사항: interview 기준 없음. 이 planning에서 새로 식별한 비차단 결정 2건은 16장에 기록한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

지원되는 Android·iPhone 설치형 EMI PMS PWA에서 사용자가 푸시를 허용하면, 앱이 닫혀 있어도 본인의 인앱 업무 알림을 기기 알림으로 받고 눌러서 해당 알림 상세로 바로 이동할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 Microsoft 365 인증 뒤 웹 또는 설치형 PWA에서 인앱 알림 목록으로 업무 알림을 확인한다. 인앱 알림 외 채널은 Teams Activity와 메일이다.
- 앱이 닫힌 상태에서는 긴급 Pending, 새 업무 배정, 프로젝트 주요 변경을 앱을 다시 열기 전까지 알 수 없어 시간 손실과 대응 지연이 발생한다.
- 현재 우회 방식은 Teams·메일 확인 또는 주기적으로 앱을 여는 것이다. PWA 설치 안내에는 모바일 푸시가 “준비 중”으로 표시되어 있다.
- 이 기능이 없으면 제조·품질 등 모바일 우선 사용자가 현장에서 긴급 알림을 놓친다. `TASK-TEAMS-PWA-001`과 `TASK-PRIVACY-NOTICE-001`은 Web Push를 명시적으로 이 별도 `NEW_FEATURE`로 이관했다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| EMI PMS 일반 사용자 | 푸시 사전 안내 확인, 권한 허용, 상태 확인, 현재 기기 구독 해제 | 본인 구독 상태와 기존 본인 알림 범위 | 본인 현재 기기의 구독 상태만 |
| 제조·품질 모바일 사용자 | 닫힌 PWA 상태에서 푸시 수신, 클릭으로 알림 상세 진입 | 기존 담당 업무·프로젝트 권한 범위 | 기존 업무 처리 권한만 |
| System Administrator | Web Push delivery 상태·실패 확인, 기존 정책 범위의 재처리, kill switch 운영 | 기존 알림 delivery 관리 범위 | 채널 설정과 정책 허용 재처리만 |

다른 사용자의 구독 조회·변경 API는 만들지 않는다. 관리자도 개별 사용자의 구독 endpoint 원문을 볼 수 없다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 처음 켜기

1. 사용자가 설치형 PWA(standalone 표시 모드)에서 로그인하면 “푸시 알림 켜기” 사전 안내가 한 번 표시된다.
2. 사용자가 버튼을 누르면 브라우저 권한 요청이 뜨고, 허용하면 시스템이 이 기기의 구독을 저장하고 “켜짐” 상태를 보여준다.
3. 안내를 닫으면 다시 자동으로 뜨지 않으며, 알림 설정 화면에서 언제든 켤 수 있다.

### 시나리오 B — 수신과 이동

1. 사용자의 새 인앱 알림이 생성되면 시스템이 같은 알림의 Web Push delivery를 만들어 활성 구독 기기로 발송한다.
2. 앱이 닫혀 있어도 기기에 알림 제목 수준 요약이 표시된다.
3. 사용자가 알림을 누르면 앱이 열리고 인증·권한 확인 뒤 해당 인앱 알림 상세 화면으로 이동한다.

### 시나리오 C — 끄기와 차단 복구

1. 사용자가 알림 설정 화면에서 현재 기기의 푸시를 끄면 구독이 해제되고 이후 이 기기로는 푸시가 오지 않는다.
2. 사용자가 브라우저/OS 설정에서 알림을 차단한 경우, 설정 화면은 “브라우저에서 차단됨 — 기기 설정에서 해제 필요”를 안내하고 앱이 강제로 되돌리지 않는다.
3. 만료되거나 반복 실패하는 구독은 서버가 자동 비활성화하고, 사용자는 설정 화면에서 다시 켤 수 있다.

## 5. 기능 요구사항

### 필수

- [ ] 푸시 수신 전용 최소 Service Worker: `push` 표시와 `notificationclick` 이동만 처리하고 fetch 가로채기·offline cache를 포함하지 않는다.
- [ ] 설치형 PWA 감지 기반 1회 사전 안내와 사용자 버튼 행동으로만 시작하는 브라우저 권한 요청.
- [ ] 알림 설정 화면(`/notification-settings`)의 상시 진입점: 지원 여부/설치 필요/미결정/켜짐/브라우저 차단/오류 상태별 안내와 켜기·끄기.
- [ ] Backend 기기별 구독 저장소(additive migration): 본인 구독만 등록·해제, endpoint·key는 secret 취급.
- [ ] `WebPush` delivery channel: 기존 delivery worker(claim/lease·재시도·수동 재처리)와 동일한 실패 격리로 발송하고, 구독 활성 이후 생성된 새 인앱 알림만 대상으로 한다.
- [ ] 인앱 알림과 동일한 수신자·발송 시점·그룹화(인앱 한 건 = 푸시 한 건, bulk 묶음 유지)와 같은 기기 중복 표시 억제.
- [ ] 푸시 payload는 인앱 알림 제목 수준 요약과 알림 식별자만 포함하고, 클릭 시 기존 인앱 알림 상세 경로로 이동한다.
- [ ] 만료(HTTP 404/410)·반복 실패 구독의 자동 비활성화와 비활성 사용자 발송 제외.
- [ ] Web Push 전용 kill switch(`Enabled`)와 dry-run 기본값: 기본 비활성·dry-run으로 배포하고 실제 발송은 별도 승인 경계로 유지한다.
- [ ] 개인정보·이용 안내 문안의 푸시 관련 내용 동기화.

### 선택

- [ ] 설정 화면에 마지막 구독 변경 시각 등 privacy-safe 상태 표시.
- [ ] 관리자 delivery 모니터의 Web Push 채널 라벨·안내 문구 보강.

### 명시적 제외

- [ ] offline app shell cache, background sync, 일반 offline mode
- [ ] 알림 종류별 on/off, 사용자 전체 일시 중지, 원격 기기 관리 화면
- [ ] 과거 알림 소급 발송, 무동의 권한 요청
- [ ] 인앱 알림보다 자세한 업무 내용을 payload에 싣는 것
- [ ] Teams·메일 수신자·발송 정책 변경
- [ ] 실제 운영 VAPID key 생성, 실제 provider 발송, Persistent UAT migration, Azure runtime 교체

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 푸시 사전 안내(1회) | 설치형 PWA 첫 로그인 후 | 푸시로 받게 되는 내용 요약, 켜는 방법 | “푸시 켜기” 또는 “나중에” | 허용 → “켜짐” 표시 / 거절·차단 → 복구 안내 |
| 알림 설정 내 푸시 섹션 | `/notification-settings` | 이 기기의 상태: 미지원/설치 필요/미결정/켜짐/브라우저 차단/오류 | 켜기, 끄기, 다시 시도 | 저장 결과를 행동 근처에 즉시 표시(기존 Action Feedback 계약) |
| 기기 알림(OS) | 푸시 수신 시 | 인앱 알림 제목 수준 요약 | 탭 | 앱 열림 → 인증·권한 확인 → 알림 상세 이동 |
| PWA 설치 안내(기존) | 기존 `PwaInstallExperience` | “준비 중” 문구를 실제 지원 상태로 갱신 | 기존과 동일 | 기존과 동일 |

확인할 UX 항목:

- 상태 6종(미지원/설치 필요/미결정/켜짐/차단/오류)이 한 문장 안내와 함께 구분되는가.
- 브라우저 차단 상태에서 “앱에서 해제 불가, 기기 설정 필요”가 명확한가.
- 390px·현재 흑백 Graphite wireframe에서 왼쪽 강조 rail 없이 켜기/끄기/복구 행동이 한눈에 보이는가.
- iPhone은 홈 화면 설치 후에만 지원됨을 쉬운 말로 안내하는가.

## 7. 업무 규칙과 불변조건

- 인앱 알림이 알림 내용·수신자·그룹화의 source of truth다. Web Push는 인앱 알림이 생성된 경우에만, 같은 수신자에게, 같은 한 건으로 생성한다. 인앱 알림이 만들어지지 않는 메일 전용·Teams 전용 event(예: Stage 18 영업 최종완료 메일)는 푸시하지 않는다.
- 사용자별 알림 preference 등으로 인앱 알림이 생성되지 않으면 푸시도 생성되지 않는다(파생 채널 원칙).
- 구독 활성 시점 이후 생성된 새 인앱 알림만 발송 대상이다. 소급 발송·재동기화를 하지 않는다.
- 푸시 provider 실패·비활성화는 인앱 알림 생성과 업무 transaction을 되돌리지 않으며, Web Push 채널 중지 시에도 인앱·Teams·메일은 계속 동작한다.
- Backend 권한이 authoritative다. 푸시 payload는 제목 수준 요약과 알림 식별자만 담고, 상세 내용은 클릭 후 인증·권한 확인을 거쳐 기존 화면에서 보여준다.
- 구독 endpoint·암호화 key는 secret으로 취급한다. API 응답·화면·로그·Task 산출물에 원문을 남기지 않는다.
- 사용자는 본인 기기 구독만 등록·해제할 수 있다. 계정 비활성 사용자는 발송 대상에서 제외한다.
- 같은 알림이 재시도·구독 갱신을 거쳐도 같은 기기에 중복 표시되지 않아야 한다.
- 조용한 시간 없이 알림 event 발생 즉시 처리한다(기존 확정 정책).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 인앱 알림·수신자 | 알림 내용·수신자 원본 | 기존 | 변경 없음, 자동 삭제·숨김 없음 유지 |
| Notification delivery | 채널별 발송 원장(claim/lease·attempt lineage·재처리 generation) | 기존 + `WebPush` 채널 값 추가 | 기존 attempt·generation 감사 유지 |
| Web Push 구독 | 사용자·기기별 endpoint·암호화 key·활성 상태·마지막 성공/실패·비활성 사유 | 신규(additive migration, 예상 `0074`) | endpoint·key secret 취급, 생성·해제·자동 비활성 시각 기록 |
| Web Push 채널 설정 | `Enabled`(kill switch)·`DryRun`·VAPID 설정 | 신규 설정(기존 `NotificationOptions` 패턴) | 기본 `Enabled=false`·`DryRun=true` |

구독 상태 전이:

```text
미지원/미설치/권한 미결정 → (사용자 버튼 → 권한 허용) → 구독 활성
구독 활성 → (사용자 해제) → 해제됨
구독 활성 → (endpoint 만료·반복 실패·계정 비활성) → 자동 비활성(사유 기록)
권한 거절/브라우저 차단 → (기기 설정에서 해제 후 재시도) → 구독 활성
```

Delivery 상태는 기존 enum(`Pending → Processing → Sent/Failed/Suppressed/Disabled/DryRunSent`)을 그대로 사용한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 구독 소유권(본인만), 발송 대상 결정(활성 구독·활성 사용자), 채널 kill switch·dry-run, 만료·반복 실패 자동 비활성화.
- 필요한 조회와 mutation: 본인 현재 기기 구독 등록(멱등 upsert), 해제, 상태 조회, VAPID 공개 key 제공. 관리자용 신규 API는 기존 delivery 조회·재처리 범위를 넘지 않는다.
- 권한·validation: 기존 인증·authorization 위에 본인 구독만 접근. endpoint 형식·key 존재 검증. 구독 API 응답에 endpoint·key 원문을 반환하지 않는다.
- transaction·동시성·idempotency: 기존 delivery worker의 claim/lease·attempt lineage·재처리 generation 불변조건을 그대로 따른다. 같은 기기 중복 억제는 delivery dedupe key와 Service Worker 알림 tag를 함께 사용한다.
- audit trail: 구독 생성·해제·자동 비활성 시각과 사유, delivery attempt·재처리 이력은 기존 원장 패턴을 재사용한다.
- 외부 provider 영향: 표준 Web Push protocol(브라우저 push service)로 발송한다. 실제 발송은 이 Task 범위에서 dry-run까지만 검증하고, 운영 VAPID key·실발송은 별도 승인 경계다. Web Push protocol 서버 구현(라이브러리 추가 여부 포함)은 Codex 구현 조사에서 확정한다.

재사용 대상(조사 기준): `INotificationChannelHandler` + `NotificationDeliveryChannels`(`backend/src/Emi.Qms.Api/Notifications/NotificationChannelHandlers.cs`, `NotificationDeliveryContracts.cs`), `NotificationOptions`의 `Enabled/DryRun` 패턴, `NotificationDeliveryStore`의 채널별 delivery 생성·worker 처리, `NotificationLinkBuilder.BuildNotificationDetailUrl`(알림 상세 딥링크). Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 확정하지 않는다.

## 10. Frontend 고려사항

- route/component: 신규 route 없이 기존 `/notification-settings`(`NotificationPreferencesPage`)에 푸시 섹션을 추가하고, 사전 안내는 기존 `PwaInstallExperience`의 standalone 감지 패턴을 재사용한 1회 안내 컴포넌트로 만든다. 클릭 이동은 기존 알림 상세 route `/teams/activity/notifications/{id}`를 재사용한다.
- Service Worker: `push`·`notificationclick`만 처리하는 최소 파일. 등록은 지원 브라우저 + 로그인 이후에만 수행하며, Vite dev/build에서의 제공 방식은 구현 조사에서 확정한다.
- loading/empty/error/success: 상태 6종(미지원/설치 필요/미결정/켜짐/차단/오류)을 구분하고 각 상태에 복구 행동을 붙인다.
- 공통 Action Feedback: 켜기/끄기 결과를 기존 A1 계약대로 행동 근처에 표시한다.
- 접근성·390px: 흑백 Graphite wireframe 유지, 버튼 터치 영역 44px, 좁은 화면에서 상태·행동이 세로 1열로 완결되게 한다.
- iOS 제약: 홈 화면 설치형에서만 Web Push가 가능함을 감지·안내한다(미설치 브라우저에서는 설치 안내로 연결).

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 인앱 알림 생성 지점·수신자·그룹화를 변경하지 않고 파생 delivery만 추가한다. 긴급 Pending·재검사 요청 등 인앱 대상 알림은 자동으로 푸시 대상이 된다.
- 권한/관리자: 기존 delivery 관리 화면·수동 재처리(`TASK-NOTIFY-REPROCESS-001` 계약)에 Web Push 채널이 자연스럽게 나타나는 수준으로 연결하고 새 관리자 권한을 만들지 않는다.
- Excel/PDF/첨부: 직접 영향 없음.
- Teams/Mail: 기존 채널·수신 정책을 변경하지 않는다. Web Push 중지 시에도 기존 채널은 유지된다.
- 삭제·복구/감사: 인앱 알림 보관 정책 불변. 구독은 사용자 해제·자동 비활성 이력을 남긴다.
- 개인정보 안내: `TASK-PRIVACY-NOTICE-001`의 정적 안내 문안에 푸시 수신·구독 데이터 항목을 동기화한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A | 수신자당 Web Push delivery 1건을 만들고 handler가 그 사용자의 활성 구독 전체에 순차 발송 | delivery 행 수가 적고 기존 채널과 행 단위가 같음 | 기기별 부분 실패·만료를 한 행에 섞어 기록해 재시도·자동 정리 판정이 모호해짐 |
| B | 발송 시점의 활성 구독(기기)당 delivery 1건 생성 | 기기별 성공·실패·만료·재시도를 기존 attempt 원장 그대로 정밀 추적, 404/410 자동 비활성화가 단순 | 다기기 사용자만큼 delivery 행 증가, 관리자 모니터에 기기 단위 행 표시 |

권장안: **B**. 기존 worker의 attempt lineage·재처리 generation 불변조건을 기기 단위로 그대로 재사용할 수 있고, 만료 구독 자동 정리와 같은 기기 중복 억제(dedupe key에 구독 식별 포함)가 정확해진다. 행 증가는 현재 사용자·기기 규모에서 수용 가능하다. 관리자 표시 방식은 16장 결정 1로 확인받는다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. Persistent UAT DB·runtime에 쓰지 않는다.
- migration 필요 여부: 있음. 구독 저장소용 additive migration 1건(예상 번호 `0074`, 기존 데이터 변경 없음, forward-fix 원칙). migration ledger 검증 테스트 갱신 포함.
- 외부 발송/실제 데이터 영향: 실제 브라우저 push service 발송은 기본 차단(`Enabled=false`·`DryRun=true`). 운영 VAPID key 생성·실발송·Azure release는 별도 사용자 승인 경계다.
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: 검증용(비운영) VAPID key 생성·사용(16장 결정 2), 실기기 검수 환경, 이후 운영 활성화·Azure release.

## 14. 검증 계획

- 최소 테스트: 구독 등록·해제·소유권 거부, kill switch·dry-run 분기, 구독 활성 이후 알림만 delivery 생성, 같은 기기 dedupe, 404/410·반복 실패 자동 비활성화, 비활성 사용자 제외, payload에 secret·상세 업무 내용 미포함 — 기존 `NotificationDeliveryTests` 패턴을 따른다.
- 영향 영역 회귀: 기존 InApp·Teams·메일 delivery 회귀, migration ledger 테스트(`PostgreSqlMigrationTests`) 갱신, 알림 설정 화면·알림 상세 딥링크 frontend 테스트, Web Push 중지 시 기존 채널 정상 동작.
- PR/CI: Validation Matrix에 따른 Backend/Frontend/Full-Stack 영향 검증. E2E는 dry-run·격리 DB로 수행하고 실제 push service를 호출하지 않는다.
- 사용자 검수: Android·iPhone 실기기 설치형 PWA에서 권한 허용, 앱 닫힘 상태 수신, 클릭 시 알림 상세 이동, 기기별 해제, 브라우저 차단 안내, 만료 복구를 체크리스트로 확인한다(검증용 key 승인 후).

## 15. 완료 기준

- 기능/권한/데이터: 필수 요구사항 전체 구현, 본인 구독만 접근, 인앱 알림 원본·기존 채널 불변, additive migration이 기존·fresh DB에서 검증됨.
- UX: 상태 6종 안내와 복구 행동, 390px·흑백 wireframe 준수, 설치 안내의 “준비 중” 문구 갱신.
- 자동 테스트: 위 최소·회귀 테스트와 CI 통과, dry-run 기반 E2E 통과.
- 5종 산출물: Implementation report, SOP, User manual, Roadmap update, User validation checklist의 상태·위치 추적.
- 사용자 검수 상태: 실기기 체크리스트는 검증용 key 승인 전까지 미완료 상태로 명시한다.
- PR 상태: 사용자 검수·게시 승인 전 Draft 유지.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 관리자 알림 발송 모니터에 Web Push를 기기(구독) 단위 행으로 표시해도 되는가 (권장안 B의 결과) | ① 기기 단위 행 그대로 표시(권장 — 실패·재처리를 기기별로 정확히 추적) / ② 사용자 단위로 묶어 표시(별도 집계 UI 추가 필요) | 대기 |
| 2 | 실기기 검수를 위한 검증용(비운영) VAPID key 생성·사용을 승인하는가 | ① 검증 전용 key를 만들어 실기기 검수까지 수행(권장 — 운영 key·실사용자 발송과 분리) / ② 이번 Task는 dry-run·자동 테스트까지만 하고 실기기 검수는 운영 준비 Task로 이관 | 대기 |

두 항목 모두 구현 착수를 막지 않는 비차단 결정이며, 결정 2는 실기기 검수 단계 전까지만 확정되면 된다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Notifications/`의 채널 상수·contract·options·delivery store·worker 연결, 신규 Web Push 구독 store·channel handler·구독 API endpoint.
- Frontend: 푸시 전용 Service Worker 신규 파일, Service Worker 등록·권한 상태 훅, `NotificationPreferencesPage` 푸시 섹션, 1회 사전 안내 컴포넌트, `PwaInstallExperience` 문구 갱신.
- DB/Migration: `database/migrations/0074_*.sql`(additive, 구독 저장소).
- Tests/Scripts: `NotificationDeliveryTests` 계열 확장, `PostgreSqlMigrationTests` ledger 갱신, frontend 상태 분기 테스트, dry-run Full-Stack E2E.
- Docs: 개인정보·이용 안내 푸시 문안, Roadmap 상태, SOP·User manual, user validation checklist.

## 18. Roadmap 연결

- 선행 Task: `TASK-NOTIFY-004`(delivery claim/lease·재시도), `TASK-NOTIFY-REPROCESS-001`(수동 재처리), `TASK-TEAMS-PWA-001`(PWA 설치 경험·Web Push 이관), `TASK-PRIVACY-NOTICE-001`(안내 문안) — 모두 필요한 기반이 구현·확정되어 있다.
- 후속 Task: 운영 VAPID key 발급·실발송 활성화·Azure release·Persistent UAT 반영(별도 `UAT_RUNTIME` 승인 경계).
- 현재 Go/No-Go: Roadmap의 현재 운영 Gate는 Azure release 계열이지만, Task Identity Gate에 사용자의 명시적 재정렬 승인(`explicitRoadmapOverrideApproved: true`)이 기록되어 `PASS_CREATE`로 판정되어 있다.
- 별도 Task로 분리할 항목: offline cache·background sync, 알림 종류별 on/off, 원격 기기 관리, 운영 rollout 일체.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-08-11 | Round 1 결정 5건 모두 A 확정, 쉬운 질문 방식 요청 | 0장 기준선과 5·7장 정책에 반영, 사용자 대면 문안은 쉬운 한국어 유지 |
| 2026-08-11 | Round 2 확인용 요약 승인(interview `COMPLETED_CONFIRMED`) | 이 planning 초안 작성의 입력으로 사용 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

Codex 구현 지시문 초안(승인 후 사용): 승인된 이 planning과 review resolution만 구현한다. `WebPush` 채널을 기존 `INotificationChannelHandler`·delivery worker 불변조건 위에 추가하고, additive migration `0074`로 구독 저장소를 만들며, 푸시 전용 최소 Service Worker와 `/notification-settings` 푸시 섹션·1회 사전 안내를 구현한다. 기본값은 `Enabled=false`·`DryRun=true`로 유지하고 실제 push service 발송·운영 key·Persistent UAT·runtime 교체를 수행하지 않는다. endpoint·key 원문을 API 응답·로그·산출물에 남기지 않는다.

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 2
