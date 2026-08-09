# TASK-TEAMS-PWA-001 — Teams SSO·Teams 앱·웹 PWA Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 5
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 사용자와 진행하는 deep-interview를 round별로 고정한다. Codex는 Fable 질문과 사용자 답변을 전달·기록하지만 업무 질문을 대신 만들거나 답하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 최초 요청 기록 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 5 | 제품 결정 답변 없음. 현재 구현을 구체적으로 설명하고 기술 용어를 쉬운 말로 풀어 같은 질문을 다시 요청 | Fable Round 2 질문 재작성 |
| 2 | `QUESTIONS_REQUIRED` | 5 | 대기 | 사용자 제품 결정 답변 기록 |
| 3 | `QUESTIONS_REQUIRED` | 4 | 대기 | 모바일 푸시·모바일 필수 검증 답변 기록 |
| 4 | `QUESTIONS_REQUIRED` | 4 | 대기 | 공식 이름의 정확한 표시 계약 답변 기록 |
| 5 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | Round 5 요약 승인 완료 | Fable primary planning |

- Round 1 Fable 원문: [teams-pwa-001-interview-round-1-fable.md](teams-pwa-001-interview-round-1-fable.md)
- Round 2 Fable 원문: [teams-pwa-001-interview-round-2-fable.md](teams-pwa-001-interview-round-2-fable.md)
- Round 3 Fable 원문: [teams-pwa-001-interview-round-3-fable.md](teams-pwa-001-interview-round-3-fable.md)
- Round 4 Fable 원문: [teams-pwa-001-interview-round-4-fable.md](teams-pwa-001-interview-round-4-fable.md)
- Round 5 Fable 원문: [teams-pwa-001-interview-round-5-fable.md](teams-pwa-001-interview-round-5-fable.md)

### Round 1 사용자 피드백

- 기존 다섯 질문의 제품 선택에는 아직 답하지 않았다.
- 현재 구현 상황을 제대로 파악한 근거를 먼저 구체적으로 설명하고, 사용자가 이해하기 쉬운 표현으로 같은 질문을 다시 제시해 달라고 요청했다.
- 프로그래밍 용어는 정확한 용어를 먼저 쓴 뒤 쉬운 뜻과 실제 사용자 영향을 이어서 설명해야 한다.
- 이 피드백은 질문 표현과 설명 방식에 관한 것이며 기존 확정 정책이나 미결정 선택을 바꾸지 않는다.

### Round 2 사용자 답변

1. Teams SSO 실패 복구는 `A`를 선택했다: silent SSO → Teams 안 대화형 인증 → 외부 브라우저 안내 순서다.
2. Teams 안의 계정은 `A`를 선택했다: Teams에 로그인된 조직 계정으로 고정한다.
3. 이름은 모든 사용자 표면에서 통일한다. 공식 짧은 이름은 `EMI PMS`, 영문 의미는 `EMI Project Management System`, 한국어 전체 이름은 `EMI 프로젝트 통합관리 시스템`으로 확정했다. 현재 사용자에게 보이는 `QMS`, `EMI QMS`, `프로젝트 통합관리시스템`, `EMI 프로젝트 통합관리시스템` 등 다른 이름은 모두 새 이름 체계로 수정한다. 내부 개발용 이름은 사용자에게 보이지 않는 코드 내부에만 남길 수 있으며, 사용자·일반 관리자 등 사용자 표면에는 노출되면 안 된다.
4. PWA는 `A`를 선택했다. Android 모바일에서는 설치 동작을 최대한 간단하게 제공하고, iPhone에서는 수동으로 홈 화면에 추가하는 방법을 안내한다. 추가 요구로, 설치한 PWA가 지원하는 푸시 알림을 기존 EMI PMS 인앱 알림과 모바일 푸시 알림으로 연동하고 싶다고 했다.
5. 주 사용 환경은 제조·품질 부서는 모바일 우선이고, 나머지 부서는 PC 우선이다. 검증 우선순위와 rollout은 이 실제 사용 비중을 반영해야 한다.

### Round 2 답변에서 확인이 더 필요한 경계

- `4A`의 기존 의미는 Service Worker 없는 설치 경험이었지만, 새로 요청한 Web Push는 일반적으로 Service Worker와 push subscription lifecycle을 필요로 한다. 오프라인 cache는 제외한 채 푸시 전용 Service Worker만 둘지 Fable이 후속 질문해야 한다.
- 모바일 푸시의 수신자·발송 시점은 기존 인앱 알림과 동일하게 시작할지, 사용자별 opt-in·기기별 구독·알림 클릭 이동·권한 거절 복구를 어떻게 할지 아직 확정되지 않았다.
- 제조·품질 모바일 우선이라는 답변을 Android·iPhone 모두의 필수 검증으로 해석할지, 실제 조직 기기 비중에 따라 단계적으로 적용할지 Fable이 확인해야 한다.

### Codex 공식 플랫폼 자료 확인

- Chrome의 `beforeinstallprompt`는 설치 조건을 충족할 때 발생하며, 앱의 설치 버튼처럼 사용자가 직접 누른 동작에서 브라우저 설치 dialog를 열 수 있다. 앱이 사용자 확인 없이 설치를 완료하는 것은 아니다. 근거: Chrome for Developers `Native App Install Prompt`.
- iOS·iPadOS 16.4 이상은 Share 메뉴의 `Add to Home Screen`으로 웹앱을 수동 추가할 수 있고, manifest의 `display`가 `standalone` 또는 `fullscreen`이면 홈 화면 아이콘에서 웹앱으로 실행된다. 근거: WebKit `Web Push for Web Apps on iOS and iPadOS`.
- iOS·iPadOS의 Web Push는 홈 화면에 추가된 웹앱을 대상으로 지원되며 사용자가 알림 권한을 허용해야 한다. Android/Chromium과 iOS 모두 수신된 Web Push는 Service Worker가 처리한다.
- W3C Push API는 push message가 Service Worker로 전달되고 push subscription이 service worker registration과 연결되는 계약을 정의한다. 따라서 오프라인 shell cache는 제외하면서 push event 처리만 담당하는 최소 Service Worker를 두는 것은 기술적으로 분리 가능한 후보안이다.
- 공식 자료:
  - https://developer.chrome.com/blog/app-install-banners-native
  - https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/
  - https://www.w3.org/TR/push-api/

### Round 3 사용자 답변

1. 모바일 Web Push는 `B`를 선택했다. 현재 `TASK-TEAMS-PWA-001`에서는 Teams SSO·이름 통일·PWA 설치 경험을 먼저 완성하고, Web Push는 바로 이어지는 별도 `NEW_FEATURE`로 분리한다.
2. 어떤 알림을 모바일 푸시로 보낼지는 후속 Web Push 기획에서 다시 확정한다. 현재 Task에서 선택하지 않는다.
3. 푸시 권한 요청 시점·거절 복구·기기별 구독 정책은 후속 Web Push 기획에서 다시 확정한다. 현재 Task에서 선택하지 않는다.
4. 모바일 필수 검증은 `B`를 선택했다. Android·iPhone 설치형 웹앱의 설치·실행·진입을 모두 필수로 검증한다. Teams 모바일 앱은 확인 항목으로 유지한다. PC 부서는 Teams 데스크톱·웹과 PC PWA 설치를 필수로 검증한다.

### 후속 Task로 넘긴 비차단 결정

- 모바일 Web Push의 발송 대상 알림 유형과 기본값
- 사용자별 푸시 설정과 업무상 필수 알림의 해제 불가 범위
- 푸시 권한 요청 시점, 권한 거절·차단 복구와 기기별 구독 lifecycle
- 푸시 알림 선택 시 인앱 알림 상세·업무 화면으로 이동하는 deep link 정책
- 푸시 수신 전용 최소 Service Worker, 구독 저장소, migration과 네 번째 알림 delivery channel

### Round 4 사용자 답변

1. 한국어 전체 이름은 `B`를 선택했다. 공식 표기는 `EMI 프로젝트 통합관리시스템`으로 붙여 쓴다.
2. 이름 배치는 `B`를 선택했다. Teams 앱·탭, 웹 설치 앱, 브라우저 제목, 로그인·상단 화면 등 모든 사용자-facing 이름 칸은 `EMI PMS`로 단일화한다. 한국어 전체 이름 `EMI 프로젝트 통합관리시스템`과 영문 의미 `EMI Project Management System`은 설명 문구에서 사용한다. Teams developer name은 별도 정정이 없으므로 제안값 `EMI`를 사용한다.
3. 설명은 `A`를 선택했다. 짧은 설명은 `EMI 프로젝트 업무와 알림을 한 곳에서 확인합니다.`로 하고, 전체 설명·웹 설치 설명은 질문 1의 붙여쓰기 확정에 맞춰 `프로젝트 생성부터 생산관리, 구매, 제조, 품질, 물류와 정산까지 연결하는 EMI 프로젝트 통합관리시스템(EMI PMS)입니다.`로 확정한다.
4. 이름 교체 범위는 `A`를 선택했다. Teams·웹 설치·브라우저·앱 화면뿐 아니라 메일 발신자 표시명·본문, Teams 알림 대체 제목, PDF 문서 정보, Excel 양식 머리글 등 사용자에게 보이는 옛 이름을 이번 Task에서 전부 `EMI PMS` 이름 체계로 교체한다. 알림 event·수신자·발송 정책은 변경하지 않고 운영 설정값의 실제 반영은 별도 승인 rollout 경계를 유지한다.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 사용자는 공개 웹 주소에서 Microsoft 365 로그인을 거쳐 시스템을 사용한다. Teams 앱에는 개인 tab과 Activity Feed 알림이 있고, 웹에는 standalone web manifest와 설치 아이콘이 있지만 Service Worker·오프라인 cache는 없다.
- 해결할 문제: Teams 앱 화면이 열리지 않거나 로그인 버튼이 작동하지 않는 경우가 있어 Teams 안의 인증 흐름을 SSO로 정식화해야 한다. Teams 앱과 웹 PWA의 이름·아이콘·진입 경험도 하나의 제품으로 정리해야 한다.
- 현재 우회 방식: Teams tab이 실패하면 Teams는 Activity 알림용으로만 사용하고 실제 업무 화면은 웹 브라우저에서 연다.
- 성공했을 때 사용자가 할 수 있는 일: Teams 안에서는 조직 계정으로 자연스럽게 EMI PMS에 진입하고, 웹에서는 EMI PMS를 설치형 앱으로 실행하며, 두 표면에서 같은 브랜드와 권한으로 업무를 이어간다.
- 하지 않을 경우 영향: Teams tab 로그인 실패와 웹/Teams 명칭 차이가 계속되고, 사용자는 알림에서 업무 화면으로 이동할 때 불필요한 로그인·전환을 반복할 수 있다.

## 2. 사용자 확정 입력

- 공식 사용자 표시명은 이제 `EMI PMS`로 고정한다.
- 웹 PWA와 Teams 앱의 컬러 아이콘은 EMI 빨간색 로고와 흰색 바탕을 사용한다.
- Teams SSO, 새 Teams manifest와 웹 PWA를 함께 기획한다.
- 내부 `Emi.Qms` solution·namespace를 변경하라는 요청은 아니다.

### 인터뷰 설명 방식

- 질문을 만들기 전에 현재 Repository의 실제 구현·화면·운영 상태를 충분히 확인한다.
- 질문마다 현재 무엇이 구현되어 있고 무엇이 없는지 구체적으로 설명하되, 사용자가 업무 관점에서 이해하기 쉬운 표현을 사용한다.
- 프로그래밍 용어가 필요하면 기술 용어를 먼저 정확히 제시한 뒤, 바로 이어서 쉬운 말로 뜻과 사용자 영향까지 설명한다.
- 구현 방식 선택지는 기술 차이만 나열하지 않고 실제 사용 흐름, 장점, 불편과 운영 영향을 함께 비교한다.

## 3. 현재 Repository 기준선

- Frontend는 MSAL Browser/React로 Entra access token을 받고 Backend는 JWT Bearer와 앱 내부 역할을 authoritative하게 검증한다.
- 공개 Frontend 앞에는 Entra 사전 인증이 적용되어 익명 shell·bundle 접근이 차단된다.
- Teams JavaScript SDK는 현재 tab context 초기화와 Activity 화면 적응에 사용되지만 Teams SSO token 취득 흐름은 없다.
- Teams manifest v1.19에는 개인 static tab, `identity`, Activity Feed resource-specific permission, `webApplicationInfo`, 10개 activity type이 있다.
- PWA에는 web manifest, standalone 표시, theme/background color와 any/maskable/Apple touch/favicon이 있지만 Service Worker·오프라인 cache·background sync·web push는 없다.
- 기존 Teams Activity 실제 발송과 사용자 수신은 완료된 선행 기능이며 이번 기획에서 event identity를 임의 변경하지 않는다.

## 4. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| EMI PMS 일반 사용자 | Teams 또는 설치형 PWA에서 로그인하고 본인 권한의 업무 수행 | 기존 앱 역할·프로젝트 접근 범위 | 기존 권한이 허용하는 업무만 | 기존 Backend authorization·audit 보존 |
| 승인 대기 사용자 | 인증 뒤 승인 대기 안내 확인 | `/api/me`와 본인 프로필 범위 | 없음 | 기존 JIT·승인 대기 정책 보존 |
| System Administrator | 사용자 역할과 운영 상태 확인 | 기존 관리자 권한 범위 | 기존 관리 기능 범위 | 실제 Teams/Entra 배포는 별도 승인 |

## 5. 정상·예외·복구 흐름

- 정상 흐름: Fable interview에서 확정한다.
- validation 실패: Fable interview에서 확정한다.
- 동시 처리·중복: 인증·manifest·PWA 설치 표면의 중복 session과 account 전환 기준을 확정해야 한다.
- 취소·재시도·복구: Teams SSO 실패, 조건부 액세스 재인증, Teams 외부 브라우저 전환과 PWA 설치 실패의 fallback을 확정해야 한다.
- 부분 실패와 rollback: Teams catalog/Entra 설정과 웹 배포가 어긋날 때 기존 Activity 알림 전용 운영과 일반 웹 로그인으로 되돌아갈 수 있어야 한다.

## 6. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 기본적으로 기존 Entra identity와 앱 내부 사용자·역할을 재사용한다. 신규 DB 상태 필요 여부는 기획에서 판단한다.
- 상태 전이: Teams context 감지, SSO 시도, 조건부 fallback, 앱 권한 확인과 PWA 설치 상태를 구분해야 한다.
- 보존·감사·삭제: token 원문을 앱 DB나 추적 파일에 저장하지 않는다. 기존 사용자·권한·감사 정책을 보존한다.
- attachment·Excel·PDF: 직접 영향 여부를 Fable이 판단한다.
- 외부 연동·notification: Teams manifest·Entra 설정·Teams JS SDK·기존 Activity deep link와 연결된다. 실제 provider 발송 정책 변경은 요청되지 않았다.
- migration·기존 데이터: 기존 사용자와 프로젝트는 보존한다. DB migration 필요 여부는 조사 결과로 남긴다.

## 7. UX와 운영 적용

- 진입 화면과 핵심 행동: Teams 개인 tab, Teams Activity deep link, 일반 웹, 설치형 PWA의 시작 화면과 로그인 전환을 통일한다.
- loading·empty·error·success feedback: Teams 초기화, SSO, 앱 권한 확인, 외부 브라우저 fallback과 PWA 설치 안내 상태를 분리해야 한다.
- 접근성·390px·Teams narrow: 기존 디자인 system을 따르고 Teams narrow pane과 390px에서 가로 overflow 없이 핵심 행동이 가능해야 한다.
- UAT와 rollout: Teams manifest 승인·catalog 배포, Entra 설정, 웹 배포와 PWA 설치 검수를 단계적으로 분리해야 한다.
- rollback과 운영자 대응: 기존 웹 로그인과 Teams Activity 알림 전용 운영을 안전한 fallback으로 보존한다.

## 8. 포함·제외 범위

### 포함 후보

- Teams tab SSO와 실패 fallback
- 새 Teams manifest 계약과 `EMI PMS` 표시명
- 웹 PWA 설치·실행 사용자 경험과 `EMI PMS` 표시명
- 흰 바탕의 빨간 EMI 로고 기반 컬러 아이콘
- 기존 Activity deep link·웹 인증·앱 역할 권한 연계
- UAT·rollout·rollback 계획

### 제외 후보

- 알림 수신자·발송 시점·에스컬레이션 정책 재설계
- 신규 Teams Bot·DM·채널 기능
- 내부 solution·namespace의 일괄 rename
- 실제 Entra·Teams Admin Center·Azure 운영 변경
- 구현·DB migration·운영 배포

## 9. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | Fable Round 1에서 생성 | Fable Round 1에서 생성 | Fable Round 1에서 생성 | 대기 | Yes |

## 10. Fable 확인용 요약

- 해결할 문제: Teams 안의 인증 실패와 웹/Teams 설치 표면의 분리된 제품 경험을 하나의 EMI PMS로 정리한다.
- 권장 범위: 미확정 — Fable interview 진행 전이다.
- 확정한 정책: 공식 표시명 `EMI PMS`; PWA·Teams 컬러 아이콘은 흰 바탕의 빨간 EMI 로고; Teams SSO·새 manifest·웹 PWA를 함께 기획한다.
- 명시적 제외: 구현, 실제 provider·runtime mutation, 내부 solution·namespace rename.
- Deferred 비차단 결정: 없음.
- Fable 판정: `QUESTIONS_REQUIRED`

## 11. 성공 기준

- 업무 결과: Teams와 설치형 웹에서 같은 EMI PMS로 진입하고 권한에 맞는 업무를 이어갈 수 있다.
- 권한·데이터 불변조건: 기존 Backend authorization, 앱 내부 역할, 승인 대기, 조건부 액세스·MFA와 알림 event identity를 보존한다.
- 자동 검증: planning에서 확정한다.
- 사용자 검수: planning에서 확정한다.

## 12. 사용자 확인

- 사용자 확인: 2026-08-07 — Round 5 요약 승인

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
