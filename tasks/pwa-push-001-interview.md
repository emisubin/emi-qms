# TASK-PWA-PUSH-001 — PWA 모바일 푸시 알림 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 2
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 사용자와 진행하는 deep-interview를 round별로 고정한다. Codex는 Fable 질문과 사용자 답변을 전달·기록하지만 업무 질문을 대신 만들거나 답하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 기존 확정 정책과 후속 Web Push 경계 기록 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 5 | 1A, 2A, 3A, 4A, 5A 확정. 질문이 너무 어렵고 눈에 들어오지 않으므로 쉬운 질문 방식으로 변경 요청 | Fable Round 2 요약 또는 추가 질문 생성 |
| 2 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | Fable 확인용 요약이 맞다고 사용자 확인 | Fable primary planning |

- Round 1 Fable 원문: [pwa-push-001-interview-round-1-fable.md](pwa-push-001-interview-round-1-fable.md)
- Round 2 Fable 원문: [pwa-push-001-interview-round-2-fable.md](pwa-push-001-interview-round-2-fable.md)

### Round 1 사용자 답변

1. 푸시 권한 요청 시점·진입점은 `A`를 선택했다. 설치형 PWA 첫 로그인 후 앱 내 사전 안내를 한 번 보여주고, 사용자가 버튼을 누를 때만 브라우저 권한 요청을 시작한다. 설정 화면에는 상시 진입점을 둔다.
2. 사용자가 끌 수 있는 범위는 `A`를 선택했다. 기기 구독 단위 on/off만 제공하며, 켠 기기는 인앱 알림 전체를 푸시로 받는다.
3. 여러 기기 관리와 lifecycle은 `A`를 선택했다. 현재 기기만 사용자가 관리하고, 서버가 만료·해지·반복 실패 구독을 자동 비활성화한다.
4. 푸시 내용과 클릭 이동은 `A`를 선택했다. 인앱 알림 제목 수준의 요약을 표시하고, 선택하면 해당 인앱 알림 상세로 이동한다.
5. 적용·rollout은 `A`를 선택했다. 구독 활성 이후의 새 알림만 대상으로 하고, Web Push 전용 kill switch와 dry-run 선검증을 둔다.

### Round 1 질문 방식 피드백

- Fable 질문이 너무 어렵고 길어 전혀 눈에 들어오지 않는다고 평가했다.
- 이후 질문과 확인 요약은 현재 동작과 사용자가 보게 될 결과를 먼저 쉬운 한국어로 짧게 설명한다.
- 긴 Repository 분석·기술 배경·큰 비교표는 꼭 필요한 내용만 남긴다.
- 프로그래밍 용어가 필요하면 정확한 용어를 먼저 쓴 뒤 바로 쉬운 뜻과 사용자 영향을 한 문장으로 풀어 설명한다.
- 질문은 한눈에 답할 수 있게 짧은 선택지와 명확한 권장안을 사용한다.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 사용자는 Microsoft 365 인증 뒤 EMI PMS 웹 또는 설치형 PWA를 사용한다. 업무 알림은 인앱, Teams Activity와 메일 delivery channel로 만들어지며, PWA 설치 안내에는 모바일 푸시가 준비 중이라고 표시된다.
- 해결할 문제: 모바일 현장 사용자는 앱을 열기 전에는 인앱 알림을 볼 수 없다. 설치형 PWA가 닫혀 있어도 같은 업무 알림을 기기 알림으로 받고 해당 업무로 바로 이동할 수 있어야 한다.
- 현재 우회 방식: Teams Activity 또는 메일을 보거나 직접 EMI PMS를 열어 인앱 알림 목록을 확인한다.
- 성공했을 때 사용자가 할 수 있는 일: 지원되는 Android·iPhone 설치형 PWA에서 명시적으로 푸시 권한을 허용하고, 기존 인앱 알림과 같은 범위의 모바일 푸시를 기기별로 수신하며, 선택 시 관련 인앱 알림 또는 업무 화면으로 이동한다.
- 하지 않을 경우 영향: 제조·품질 등 모바일 우선 사용자는 긴급 Pending, 새 업무 배정과 프로젝트 주요 변경을 앱을 다시 열기 전까지 놓칠 수 있다.

## 2. 이미 확정된 알림 정책

- PWA 푸시는 인앱 알림과 일치시킨다. 인앱 알림이 생성되지 않는 메일 전용·Teams 전용 event를 PWA에 별도로 만들지 않는다.
- 같은 bulk action에서 패널별 `work_items`가 여러 개 생겨도 인앱 알림을 한 건으로 묶고, PWA도 같은 한 건으로 보낸다.
- 일반 업무 할당, 프로젝트 생성·납기·상태 변경, 프로젝트 물류 완료, Pending 생성·종결·재검사 등 인앱 알림의 확정 수신자·시점은 기존 정책 Task가 authoritative하다.
- 일반 Pending과 긴급 Pending, 재검사 요청과 재조치 요청은 인앱 알림 대상이므로 PWA 푸시 대상에도 포함된다.
- Stage 18 영업 최종완료는 영업팀 전체 메일 전용이므로 PWA 대상이 아니다.
- 조용한 시간은 두지 않고 알림 event가 발생하면 즉시 처리한다.
- 인앱 알림은 자동 삭제·숨김 없이 보관한다.
- 열린 업무의 `due_date`는 원래 업무 일정과 동기화하고, 정확한 일정 근거가 없으면 비워 둔다. 완료된 업무의 마감일·이력은 고정한다.

## 3. 현재 Repository 기준선

- `frontend/public/manifest.webmanifest`는 standalone 설치 metadata와 EMI 아이콘을 제공한다.
- `PwaInstallExperience`는 Android의 browser install prompt와 iPhone 수동 홈 화면 추가 안내를 제공한다.
- Service Worker, Push API·Notification API 사용, push event handler와 notification click handler는 없다.
- Backend에는 InApp, Mail, TeamsActivity, TeamsChannel delivery channel이 있지만 Web Push channel과 subscription store는 없다.
- 알림 delivery worker는 provider 실패를 업무 transaction과 분리하고 재시도·관리자 재처리를 지원한다. 새 푸시 채널도 이 불변조건을 보존해야 한다.
- 로그인·권한은 기존 Easy Auth/MSAL·Backend authorization을 그대로 사용하며, 푸시 payload 자체가 비인가 업무 상세를 노출하면 안 된다.
- `TASK-TEAMS-PWA-001`과 `TASK-PRIVACY-NOTICE-001`은 Web Push를 명시적으로 별도 신규 기능으로 이관했다.

## 4. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| EMI PMS 일반 사용자 | 본인 기기에서 푸시 권한 허용·상태 확인·기기 구독 해제, 본인 인앱 알림의 푸시 수신 | 본인 구독 기기와 기존 본인 알림 범위 | 본인 기기의 구독 상태만 | 권한 선택과 구독 변경 시점 추적, 다른 사용자 구독 조회 금지 |
| 제조·품질 모바일 사용자 | 설치형 PWA가 닫힌 상태에서도 긴급 Pending·업무 알림 확인 | 기존 담당 업무·프로젝트 권한 범위 | 기존 업무 처리 권한만 | Android·iPhone 실기기 검수 필수 |
| System Administrator | 기술적 실패·delivery 상태 확인과 승인된 재처리 | 기존 알림 delivery 관리 범위 | 정책이 허용한 재처리만 | 현장 수신 정책을 임의 변경하지 않음 |

## 5. 정상·예외·복구 흐름

- 정상 흐름: 설치형 PWA의 사용자 행동으로 권한 요청 → 허용 후 기기 구독 저장 → 인앱 알림 생성 → 같은 알림의 Web Push delivery 생성 → 브라우저 push 수신 → 기기 알림 표시 → 선택 시 인증·권한 확인 뒤 대상 화면 이동.
- validation 실패: 지원하지 않는 브라우저, PWA 미설치, 알림 권한 거절·차단, 만료되거나 잘못된 구독은 각각 다른 안내와 복구 방법이 필요하다.
- 동시 처리·중복: 사용자 한 명이 여러 기기를 가질 수 있고, 한 기기의 구독이 갱신되거나 같은 알림이 재시도돼도 동일 기기에 중복 표시되지 않아야 한다.
- 취소·재시도·복구: 사용자가 기기별 푸시를 해제할 수 있어야 하고, 브라우저 설정에서 차단한 경우 앱이 직접 해제할 수 없음을 안내해야 한다. 만료 subscription은 안전하게 비활성화해야 한다.
- 부분 실패와 rollback: 푸시 provider 실패는 인앱 알림과 업무 transaction을 되돌리지 않는다. 푸시 채널을 중지해도 기존 인앱·Teams·메일은 계속 동작해야 한다.

## 6. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 사용자·기기별 Web Push subscription, 권한·활성 상태, 마지막 성공·실패 시각, 비활성화 사유, 기존 notification delivery와의 연결.
- 상태 전이: 미지원/미설치/미결정 → 권한 요청 → 허용·구독 활성 또는 거절·차단 → 갱신·해제·만료 비활성.
- 보존·감사·삭제: 구독 endpoint·key는 secret 취급하며 일반 화면·로그·Task 산출물에 원문을 남기지 않는다. 사용자 계정 비활성화·기기 해제 때 발송 대상에서 제외한다.
- attachment·Excel·PDF: 직접 영향 없음.
- 외부 연동·notification: 표준 Web Push provider와 브라우저 push service를 네 번째 개인 delivery channel로 연결하는 후보다. 실제 provider credential·운영 발송은 별도 승인 경계다.
- migration·기존 데이터: additive migration이 필요하다. 기존 사용자·알림·delivery는 소급 푸시하지 않으며, 사용자가 허용한 이후 생성된 새 인앱 알림부터 적용하는 방향을 질문에서 확인한다.

## 7. UX와 운영 적용

- 진입 화면과 핵심 행동: 기존 PWA 설치 안내의 `이용 안내`와 로그인 후 계정/알림 설정에서 푸시 상태·설정 진입점을 제공하는 후보다.
- loading·empty·error·success feedback: 지원 여부, PWA 설치 필요, 권한 미결정, 허용됨, 브라우저 차단, 구독 오류와 다시 시도를 구분해야 한다.
- 접근성·390px·Teams narrow: 현재 흑백 Graphite wireframe을 유지하며 왼쪽 강조 rail 없이 390px에서 권한·복구 행동이 한눈에 보여야 한다.
- UAT와 rollout: 실제 Android·iPhone 설치형 PWA에서 권한 허용, 수신, 클릭 이동, 기기별 해제와 만료 복구를 검수한다.
- rollback과 운영자 대응: Web Push delivery 생성만 중지하거나 provider 설정을 비활성화해 기존 채널을 보존할 수 있어야 한다.

## 8. 포함·제외 범위

### 포함 후보

- 푸시 수신 전용 최소 Service Worker와 알림 클릭 deep link
- 사용자 행동 기반 권한 요청·거절 복구·기기별 구독 lifecycle
- Backend subscription store·Web Push delivery channel·재시도·만료 처리
- 인앱 알림과 동일한 수신자·발송 시점·그룹화
- additive migration, privacy notice 동기화, Android·iPhone·PC 지원 브라우저 검증

### 제외 후보

- 오프라인 app shell cache, background sync와 일반 offline mode
- Teams·메일의 수신자·발송 정책 재설계
- 푸시에 인앱 알림보다 더 자세한 민감 업무 내용을 싣는 것
- 기존 알림을 소급해 한꺼번에 푸시하는 것
- 사용자 동의 없는 강제 브라우저 권한 요청
- 실제 운영 key 생성·provider 발송·Persistent UAT migration·Azure 교체

## 9. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 푸시 권한을 언제 요청할지 | A: 첫 로그인 뒤 1회 안내·설정 상시 진입 / B: 설정에서만 / C: 로그인마다 | A | A | No |
| 2 | 사용자가 PWA 푸시를 끌 수 있는 범위 | A: 기기 단위 전체 / B: 기기 + 사용자 전체 일시 중지 / C: 이벤트별 | A | A | No |
| 3 | 여러 기기·구독 해제 lifecycle | A: 현재 기기만 관리·서버 자동 정리 / B: 전체 기기 목록·원격 해제 | A | A | No |
| 4 | 푸시 내용과 클릭 이동 범위 | A: 인앱 제목 수준·알림 상세 / B: 일반 문구·알림 목록 / C: 업무 화면 직접 이동 | A | A | No |
| 5 | 신규 구독 적용 시점과 운영 rollout | A: 허용 뒤 새 알림·kill switch·dry-run / B: 최근 미확인 알림 소급 | A | A | No |

## 10. Fable 확인용 요약

- 해결할 문제: 모바일 우선 사용자가 설치형 EMI PMS를 열지 않은 상태에서도 본인의 인앱 업무 알림을 기기 알림으로 받고 바로 해당 업무로 이동한다.
- 권장 범위: 인앱 알림 source-of-truth를 그대로 쓰는 최소 Web Push channel, 푸시 전용 Service Worker, 기기별 opt-in 구독과 안전한 실패 격리.
- 확정한 정책: PWA 푸시는 인앱 알림과 일치, bulk 알림 그룹화 일치, 조용한 시간 없음, 인앱 없는 event를 PWA에 별도 생성하지 않음.
- 명시적 제외: offline cache/background sync, 무동의 권한 요청, 과거 알림 소급 푸시, 실제 provider·운영 mutation.
- Deferred 비차단 결정: 없음. 권한·사용자 설정·기기 lifecycle·payload/deep link·rollout을 interview에서 확정한다.
- Fable 판정: `QUESTIONS_REQUIRED`

## 11. 성공 기준

- 업무 결과: 지원되는 Android·iPhone 설치형 PWA에서 허용 이후 새 인앱 알림을 동일한 수신자·그룹으로 기기 푸시 수신하고 대상 화면으로 이동한다.
- 권한·데이터 불변조건: 기존 Backend 권한, 알림 수신자와 인앱 원본을 보존하고 구독 secret·민감 업무 내용을 노출하지 않는다.
- 자동 검증: 구독 상태·권한 분기·중복 억제·delivery 재시도·만료 정리·deep link와 기존 알림 회귀를 포함한다.
- 사용자 검수: Android·iPhone 실기기에서 권한 허용, 수신, 클릭 이동, 기기 해제와 차단 복구를 확인한다.

## 12. 사용자 확인

- 사용자 확인: 2026-08-11 — Round 2 확인용 요약 승인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

사용자 확인 후에만 다음 상태로 바꾼다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`
