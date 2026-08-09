The baseline is fully revalidated. I have everything needed for the primary planning draft: the interview is `COMPLETED_CONFIRMED` with 0 blocking decisions, the Roadmap's product Next Gate is exactly this task, and I confirmed the current implementation directly — web login is MSAL redirect-only (which fails inside the Teams iframe), teams-js is used only for deep-link context, the Teams manifest template still carries the old `PMS` names, the web manifest/index.html carry `EMI QMS`, there is no Service Worker or `beforeinstallprompt` handling, and the old names remain in backend mail/PDF/Excel strings and in the PWA asset/manifest validation scripts.

Below is the single Fable primary planning draft for the runner to record.

---

# TASK-TEAMS-PWA-001 — Teams SSO·Teams 앱·웹 PWA 통합 경험 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/teams-pwa-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: Teams 앱에서 화면이 열리지 않거나 로그인 버튼이 작동하지 않아 Teams는 알림 확인용으로만 쓰이고, Teams(`PMS`)·설치 아이콘(`EMI QMS`)·열린 화면(`EMI 프로젝트 통합관리시스템`)의 이름이 서로 달라 하나의 제품으로 보이지 않는다.
- 대상 사용자·역할: EMI PMS 일반 사용자, 승인 대기 사용자, System Administrator. 모두 기존 앱 역할·프로젝트 접근 범위를 그대로 사용한다.
- 정상 흐름: Teams context 감지 → silent SSO → 앱 권한 확인 → 업무 화면. 웹·설치형 앱은 기존 MSAL 로그인을 유지한다.
- 예외·복구 흐름: silent SSO 실패 시 Teams 안 대화형 인증(팝업) → 그래도 실패하면 "브라우저에서 열기" 안내. 최종 fallback으로 기존 웹 로그인과 Teams Activity 알림 전용 운영을 항상 보존한다.
- 확정한 정책과 명시적 제외:
  - 모든 사용자-facing 이름 칸은 `EMI PMS`로 단일화. 한국어 전체 이름 `EMI 프로젝트 통합관리시스템`(붙여쓰기)과 영문 의미 `EMI Project Management System`은 설명 문구에서 사용. Teams developer name은 `EMI`.
  - 짧은 설명: `EMI 프로젝트 업무와 알림을 한 곳에서 확인합니다.` / 전체 설명·웹 설치 설명: `프로젝트 생성부터 생산관리, 구매, 제조, 품질, 물류와 정산까지 연결하는 EMI 프로젝트 통합관리시스템(EMI PMS)입니다.`
  - 이름 교체 범위는 전 표면 일괄: 화면·Teams manifest·웹 설치·브라우저 제목에 더해 메일 발신자 표시명·본문 머리글, Teams 개인 알림 대체 제목, IQC·품질검사 PDF 문서 정보, 휴일 일괄 등록 Excel 머리글까지 포함.
  - Teams tab 안에서는 Teams에 로그인된 조직 계정으로 고정하고 계정 선택·로그아웃 UI를 숨긴다. 웹 표면의 세션 기억·계정 선택 동작은 바꾸지 않는다.
  - PWA는 Service Worker·오프라인 cache 없이 설치 경험까지만 제공. Android·PC는 `beforeinstallprompt` 기반 설치 동작, iPhone은 홈 화면 수동 추가 안내.
  - 아이콘은 흰 바탕의 빨간 EMI 로고로 통일.
  - 명시적 제외: Web Push(별도 후속 `NEW_FEATURE`), 알림 수신자·발송 정책 재설계, 신규 Teams Bot·DM·채널, `Emi.Qms` 내부 solution·namespace rename, 실제 Entra·Teams Admin Center·Azure 운영 변경, 이 기획 단계에서의 구현·DB migration·운영 배포.
- planning으로 넘긴 비차단 미결정 사항: 후속 Web Push Task의 발송 대상 알림 유형·기본값, 사용자별 푸시 설정·해제 불가 범위, 권한 요청 시점·거절 복구·기기별 구독 lifecycle, 푸시 클릭 deep link, 푸시 전용 최소 Service Worker·구독 저장소·migration·네 번째 delivery channel. 이 다섯 항목은 16절에 그대로 전달한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

사용자가 Teams 안에서는 Teams에 로그인된 조직 계정으로 추가 로그인 없이 EMI PMS 업무 화면에 진입하고, 웹에서는 EMI PMS를 설치형 앱으로 설치·실행하며, 모든 표면에서 같은 `EMI PMS` 이름·아이콘·권한으로 업무를 이어간다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 공개 웹 주소에서 Microsoft 365 로그인을 거쳐 시스템을 사용하고, Teams는 개인 tab과 Activity Feed 알림으로 접근한다.
- 시간 손실·혼선 지점:
  - 현재 웹 로그인은 전체 페이지 이동 방식(`loginRedirect` — 로그인 페이지로 화면 전체를 이동시키는 방식)만 사용한다. Teams tab은 앱을 iframe(다른 화면 안에 끼워 넣은 창) 안에서 열기 때문에 이 방식이 차단되거나 실패해, Teams 안 로그인 버튼이 작동하지 않는 증상으로 나타난다.
  - Teams SDK는 현재 알림 deep link의 문맥 추출에만 사용되고 Teams SSO token 취득 흐름이 없다.
  - 이름이 표면마다 다르다: Teams manifest는 `PMS`/`프로젝트 통합관리시스템`, 웹 설치 이름은 `EMI QMS`, 화면 제목은 `EMI 프로젝트 통합관리시스템`.
- 현재 우회 방식: Teams tab이 실패하면 Teams는 Activity 알림 확인용으로만 쓰고 실제 업무는 웹 브라우저에서 연다.
- 이 기능이 없을 때의 영향: Teams 알림에서 업무 화면으로 이동할 때마다 로그인·화면 전환을 반복하고, 서로 다른 이름 때문에 같은 제품임을 인지하기 어렵다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| EMI PMS 일반 사용자 | Teams tab 또는 설치형 PWA에서 로그인 없이(또는 최소 확인으로) 진입해 업무 수행 | 기존 앱 역할·프로젝트 접근 범위 | 기존 권한이 허용하는 업무만 |
| 승인 대기 사용자 | Teams·PWA에서 인증 뒤 기존 승인 대기 안내 확인 | `/api/me`와 본인 프로필 범위 | 없음 |
| System Administrator | 사용자 역할과 운영 상태 확인. Teams manifest package 산출물 확인 | 기존 관리자 권한 범위 | 기존 관리 기능 범위. 실제 Teams/Entra 배포는 별도 승인 |

권한 신설·확대는 없다. Backend JWT Bearer와 앱 내부 역할 검증이 계속 authoritative하다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — Teams 개인 tab 진입 (정상)

1. 사용자가 Teams에서 EMI PMS 개인 tab을 연다.
2. 시스템이 Teams context를 감지하고 초기화 상태를 표시한 뒤 silent SSO로 Teams 조직 계정의 token을 얻는다.
3. 앱 권한(`/api/me`) 확인 후 기존 업무 화면이 그대로 열린다. 계정 선택·로그아웃 버튼은 Teams 안에서 표시하지 않는다.

### 시나리오 B — Teams SSO 실패와 단계적 복구

1. 최초 동의·MFA·조건부 액세스 등으로 silent SSO가 실패한다.
2. 시스템이 Teams 안 대화형 인증(팝업 창 인증)을 한 번 시도하도록 버튼과 함께 안내한다.
3. 팝업 인증도 실패하면 "브라우저에서 열기" 안내를 표시하고, 기존 웹 로그인 경로로 업무를 이어갈 수 있게 한다. 각 단계는 서로 다른 화면 상태(초기화·SSO 시도·권한 확인·실패 안내)로 구분된다.

### 시나리오 C — Teams Activity 알림 deep link

1. 사용자가 Teams Activity 알림을 누른다.
2. tab이 열리면서 기존 subEntityId 기반 알림 상세 라우팅이 그대로 동작하되, 인증은 시나리오 A/B의 SSO 흐름을 먼저 거친다.
3. 알림 상세 또는 해당 업무 화면이 표시된다.

### 시나리오 D — 웹 PWA 설치·실행

1. Android·PC 사용자가 브라우저에서 설치 가능 신호(`beforeinstallprompt`)가 있으면 앱 안의 설치 안내에서 설치 버튼을 눌러 브라우저 설치 dialog를 연다.
2. iPhone 사용자는 같은 안내 영역에서 Share 메뉴 → `홈 화면에 추가` 순서의 수동 설치 방법을 본다.
3. 설치된 앱은 `EMI PMS` 이름과 흰 바탕 빨간 EMI 로고 아이콘으로 홈 화면·작업 표시줄에 표시되고, 실행하면 기존 MSAL 로그인·세션 기억 동작으로 진입한다.

### 시나리오 E — 이름 통일

1. 사용자가 어떤 표면(Teams 앱·tab, 설치 앱, 브라우저 제목, 로그인·상단 화면, 알림 메일, Teams 알림 대체 제목, IQC·품질검사 PDF, 휴일 Excel 양식)을 보더라도 이름 칸은 `EMI PMS`다.
2. 설명 문구에서만 `EMI 프로젝트 통합관리시스템`·`EMI Project Management System`을 사용한다.

## 5. 기능 요구사항

### 필수

- [ ] Teams context 감지 시 silent SSO → Teams 안 대화형 인증 → 외부 브라우저 안내의 3단계 인증 흐름
- [ ] Teams tab에서 계정 선택·로그아웃 UI 숨김(조직 계정 고정), 웹 표면 동작 불변
- [ ] Teams manifest 계약 갱신: short/full name·tab 이름 `EMI PMS`, developer name `EMI`, 확정 설명 문구, 기존 `webApplicationInfo`·activity type·권한 보존
- [ ] web manifest·index.html·화면 제목 등 모든 사용자-facing 이름 칸 `EMI PMS` 단일화와 확정 설명 문구 적용
- [ ] Android·PC `beforeinstallprompt` 기반 설치 동작과 iPhone 수동 추가 안내 UX
- [ ] 흰 바탕 빨간 EMI 로고 기반 아이콘 세트(any/maskable/Apple touch/favicon, Teams color/outline) 통일
- [ ] 메일 발신자 표시명·본문 머리글, Teams 개인 알림 대체 제목, IQC·품질검사 PDF 문서 정보, 휴일 Excel 머리글의 옛 이름 교체
- [ ] 기존 Teams Activity deep link 라우팅·알림 event identity·수신자·발송 정책 보존
- [ ] 이름·아이콘·manifest 검증 스크립트(`scripts/test-pwa-assets.sh`, `scripts/validate-azure-pilot-artifacts.sh`, `scripts/build-teams-manifest-package.sh`)의 기대값을 새 이름 계약과 동기화

### 선택

- [ ] Teams tab 안에서 설치 안내를 숨기는 표면별 노출 규칙(Teams 안에서는 설치 안내가 무의미)
- [ ] 옛 이름 재유입을 막는 사용자-facing 문자열 검사(테스트 또는 스크립트)

### 명시적 제외

- [ ] Web Push·Service Worker·오프라인 cache·background sync (후속 `NEW_FEATURE`)
- [ ] 알림 수신자·발송 시점·에스컬레이션 정책 재설계
- [ ] 신규 Teams Bot·DM·채널 기능
- [ ] `Emi.Qms` solution·namespace·내부 파일명 일괄 rename (내부 개발용 이름은 코드 내부에만 유지)
- [ ] 실제 Entra 앱 등록 변경, Teams Admin Center catalog 반영, Azure 운영 배포·설정값 반영 (별도 승인 rollout)

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| Teams 진입 gate | Teams 개인 tab, Activity deep link | 초기화 중 → SSO 시도 중 → 권한 확인 중 상태와 `EMI PMS` 브랜드 | 대기, 실패 시 안내 버튼 | 상태별 문구 분리, 무한 빈 화면 없음 |
| Teams 대화형 인증 안내 | silent SSO 실패 | 실패 이유의 쉬운 설명과 `Microsoft 365 인증` 버튼 | 팝업 인증 실행 | 성공 시 업무 화면, 실패 시 다음 단계 안내 |
| 외부 브라우저 안내 | 팝업 인증 실패 | "브라우저에서 열기" 안내와 링크 | 브라우저에서 기존 웹 로그인 | 기존 웹 흐름으로 업무 계속 |
| 승인 대기 안내 | 인증 성공, 앱 미승인 | 기존 승인 대기 안내(변경 없음) | 대기 | 기존과 동일 |
| PWA 설치 안내 | 웹·설치 전 브라우저 | 플랫폼별 설치 방법(Android·PC 버튼 / iPhone 수동 절차), `EMI PMS` 이름·아이콘 | 설치 버튼 클릭 또는 수동 절차 수행 | 설치 dialog 표시, 설치 후 안내 숨김/완료 표시 |
| 로그인·상단 shell | 기존 로그인·헤더 | `EMI PMS` 제목과 확정 설명 문구 | 기존과 동일 | 기존과 동일 |

확인할 UX 항목:

- Teams 진입의 각 상태(초기화·SSO·권한 확인·실패)를 사용자가 구분해 이해할 수 있는가?
- 실패 화면마다 다음 행동(팝업 인증, 브라우저에서 열기)이 하나의 명확한 버튼으로 보이는가?
- Teams tab에서 계정 선택·로그아웃이 보이지 않고, 웹에서는 기존과 동일한가?
- iPhone 수동 설치 안내가 그림 없이도 따라 할 수 있게 단계로 표시되는가?
- Teams narrow pane과 390px에서 진입 gate·설치 안내가 가로 overflow 없이 동작하는가?

## 7. 업무 규칙과 불변조건

- Backend JWT Bearer 검증과 앱 내부 역할·프로젝트 접근 권한이 유일한 authoritative 판정이다. Teams SSO는 로그인 편의만 바꾸고 권한을 바꾸지 않는다.
- Entra 조건부 액세스·MFA를 우회하지 않는다. silent SSO가 안 되는 계정은 반드시 대화형 인증을 거친다.
- Teams tab 안에서는 Teams 조직 계정만 사용한다. 다른 계정으로 전환하는 경로를 만들지 않는다.
- token 원문·tenant/client identifier를 앱 DB나 추적 파일에 저장하지 않는다.
- 기존 Teams Activity Feed의 10개 activity type과 event identity, 수신자·발송 정책, deep link 계약(`subEntityId` 기반)을 변경하지 않는다.
- 옛 이름(`QMS`, `EMI QMS`, `PMS` 단독, `프로젝트 통합관리시스템` 이름 칸 사용)은 사용자 표면에 남기지 않는다. `Emi.Qms` 내부 이름은 사용자에게 노출되지 않는 코드 내부에만 남는다.
- Service Worker 파일을 추가하지 않는다(후속 Web Push Task의 경계).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| Entra identity·앱 사용자·역할 | 인증·권한의 원천 | 기존 재사용 | 기존 JIT·승인 대기·audit 보존 |
| Teams 진입 상태 | 초기화→SSO 시도→권한 확인→준비/실패 안내 | 신규 (Frontend 화면 상태만) | DB 저장 없음 |
| PWA 설치 안내 상태 | 설치 가능 신호 수신·설치 완료·수동 안내 표시 여부 | 신규 (Frontend local 상태만) | DB 저장 없음 |
| 이름·설명 문구 | 사용자-facing 브랜드 문자열 | 기존 값 교체 | 알림 delivery의 과거 발송 이력 원문은 소급 수정하지 않음 |

```text
TeamsContextDetected → SsoSilentInProgress → (성공) AppAuthorized → Ready
                                   ↓ 실패
                        InteractiveAuthOffered → (성공) AppAuthorized → Ready
                                   ↓ 실패
                        ExternalBrowserGuide (기존 웹 로그인으로 종료)
```

신규 DB 테이블·컬럼·migration은 필요하지 않은 것으로 판단한다. 구현 조사에서 달라지면 additive 원칙과 별도 승인 경계를 먼저 보고한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 기존 bearer 검증, 역할·프로젝트 접근, 승인 대기 판정. 이번 Task에서 인증·권한 계약 변경 없음이 기본 방향이다.
- 필요한 조회와 mutation: 신규 endpoint 없음. 기존 `/api/me`·업무 API를 그대로 사용한다.
- 권한·validation: 변경 없음.
- transaction·동시성·idempotency: 해당 없음(문자열·정적 자산·Frontend 인증 흐름 중심).
- audit trail: 기존 로그인·authorization audit 로직을 변경하지 않는다.
- 외부 provider 영향: 발송 정책·경로 변경 없음. 다음의 사용자-facing 문자열만 교체한다.
  - `backend/src/Emi.Qms.Api/Notifications/NotificationOptions.cs` — 메일 발신자 표시명 기본값
  - `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryStore.cs` — 시스템 표시명과 메일 본문 머리글
  - `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryEndpointExtensions.cs` — 테스트·수동 발송 문구
  - `backend/src/Emi.Qms.Api/Notifications/TeamsActivityClient.cs` — Teams 개인 알림 대체 제목
  - `backend/src/Emi.Qms.Api/Materials/IqcPdfRenderer.cs`, `backend/src/Emi.Qms.Api/QualityInspections/QualityInspectionPdfRenderer.cs` — PDF 문서 정보(Author)
  - `backend/src/Emi.Qms.Api/Calendar/CalendarHolidayExcelParser.cs` — 휴일 일괄 등록 Excel 머리글
  - `appsettings.json`·`appsettings.Development.json`의 발신자 표시명 기본값. 운영 환경 설정값의 실제 반영은 별도 승인 rollout이다.

Teams SSO token 수용 방식(12절 후보 A/B)에 따라 Backend audience 설정 조사가 필요할 수 있으나, 권장안 A(NAA)는 기존 token 계약을 그대로 유지한다. Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 추가로 확정하지 않는다.

## 10. Frontend 고려사항

- route/component: 기존 단일 `App.tsx` 라우팅 유지. Teams 진입 gate와 설치 안내를 기존 auth shell·`TeamsActivityAuthFallback` 계보 위에 추가하고, `frontend/src/auth.ts`의 MSAL 구성을 Teams context 인지형으로 확장한다.
- loading/empty/error/success: 초기화·SSO 시도·권한 확인·실패 안내를 별도 상태로 표시한다. 기존 "인증 확인 전에도 빈 화면으로 남지 않는다" 원칙을 유지한다.
- 공통 Action Feedback: 대화형 인증·설치 버튼은 진행 중 중복 클릭을 차단하고 결과를 버튼 근처에 표시한다.
- 접근성: 안내 문구 `aria-live`, 버튼 focus 순서, 스크린리더 label을 기존 규칙대로 적용한다.
- 390px/mobile/narrow pane: Teams narrow pane과 390px에서 진입 gate·설치 안내의 page-level 가로 overflow 0을 유지한다.
- 이름 교체 지점: `frontend/index.html`(title·application-name·apple-mobile-web-app-title), `frontend/public/manifest.webmanifest`(name·short_name·description), `frontend/src/App.tsx`의 로그인·상단 제목, Teams 화면 제목, 수동 알림 기본 문구, `QMS` 치환 sanitizer(`App.tsx`·`api.ts`)와 상단 nav 라벨. 아이콘 파일 내부 경로(`/icons/emi-qms-*.png`)는 사용자 표면이 아니므로 유지 가능하다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: Activity deep link → 알림 상세 라우팅(`subEntityId` 추출)은 그대로 두고 앞단 인증만 SSO 흐름으로 바뀐다.
- 권한/관리자: JIT 사용자 생성·승인 대기·System Administrator 기능 변경 없음.
- Excel/PDF/첨부: 휴일 Excel 머리글과 IQC·품질검사 PDF 문서 정보의 이름 문자열만 교체. 양식 구조·데이터 불변.
- Teams/Mail: manifest 계약과 발신자 표시명·본문 머리글·대체 제목의 이름만 교체. 발송 채널·정책·event identity 불변.
- 삭제·복구/감사: 영향 없음.

## 12. 후보 구현안과 대안

### Teams SSO token 취득 방식

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A | MSAL 중첩 앱 인증(NAA, Nested App Authentication) — Teams 안에서 MSAL이 Teams 호스트를 중개자로 사용해 기존과 같은 API scope token을 조용히 발급 | 기존 Backend token 계약·audience 불변. 웹과 같은 MSAL 코드 계보 유지. Microsoft 권장 최신 방식 | Entra 앱 등록에 NAA 관련 설정 필요(운영 반영은 별도 승인). 구형 Teams 클라이언트 동작 확인 필요 |
| B | Teams JS SDK `authentication.getAuthToken()` — Teams가 `webApplicationInfo.resource` 대상 token을 직접 발급 | SDK 호출이 단순하고 manifest의 기존 `webApplicationInfo`를 바로 활용 | 발급 token의 audience/scope가 기존 웹 token과 달라 Backend 수용 설정 조사·확장이 필요. 인증 코드 경로가 웹과 이원화 |

권장안: **A (NAA)**. Backend를 authoritative layer로 유지하면서 token 계약을 바꾸지 않는 것이 이 Repository의 불변조건과 가장 잘 맞는다. 구현 조사에서 NAA가 현재 Teams 클라이언트·MSAL 버전에서 확정 불가로 판명되면 B로의 전환을 별도 보고한다. 두 후보 모두 실패 시 Teams 팝업 인증 → 외부 브라우저 안내의 확정된 fallback 순서를 공유한다.

### 설치 안내 노출 방식

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A | 로그인 화면·프로필(상단 메뉴) 영역에 설치 안내를 상시 배치하고, 설치 가능 신호가 있으면 버튼 활성 | 발견하기 쉽고 iPhone 수동 안내 자리도 자연스러움 | 이미 설치한 사용자에게 반복 노출될 수 있어 숨김 규칙 필요 |
| B | 설치 가능 신호가 있을 때만 일시적 배너로 노출 | 불필요한 노출 최소화 | iPhone은 신호가 없어 수동 안내 진입점이 사라짐 — 별도 진입점을 또 만들어야 함 |

권장안: **A**. iPhone(신호 없음)과 Android·PC(신호 있음)를 한 진입점에서 다룰 수 있고, 설치 완료·Teams 내부에서는 숨기는 규칙을 함께 둔다. 최종 배치는 16절 사용자 결정 항목이다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. DB 변경 없음.
- migration 필요 여부: 불필요 판단. 구현 조사에서 필요해지면 중단하고 별도 보고.
- 외부 발송/실제 데이터 영향: 실제 provider 발송 없음. 발송 문자열 기본값 변경은 코드·설정 기본값까지만이며, 운영 설정값 반영은 별도 승인 rollout이다.
- runtime 교체 여부: 없음. Azure 운영 image 반영은 별도 승인형 release다.
- 추가 사용자 승인 필요 작업: Entra 앱 등록 변경(NAA 설정 또는 SSO scope), Teams Admin Center catalog manifest 업로드·교체, Azure 운영 배포. rollout 순서는 웹 반영·검증 → Teams catalog/Entra 반영이며 각 단계가 별도 승인이다. 실패 시 기존 웹 로그인·Activity 알림 전용 운영으로 되돌린다.

## 14. 검증 계획

- 최소 테스트: Frontend unit(인증 gate 상태 전이, Teams context 감지, 이름 문자열), Backend unit(발신자 표시명·PDF·Excel 문자열 교체 회귀), `scripts/test-pwa-assets.sh`·`scripts/validate-azure-pilot-artifacts.sh` 기대값 동기화 후 통과.
- 영향 영역 회귀: 기존 auth shell·`TeamsActivityAuthFallback`·알림 deep link 라우팅 테스트, e2e auth-shell 제목 assertion 갱신, 알림 delivery 관련 Backend 테스트.
- PR/CI: 기존 CI 전체 통과. 세부 범위는 Validation Matrix를 따른다.
- 사용자 검수(차단 기준): Android·iPhone 설치형 웹앱의 설치·실행·진입 필수 통과, PC 부서용 Teams 데스크톱·웹과 PC PWA 설치 필수 통과, Teams 모바일 앱은 확인 항목. Teams SSO 실검증은 Entra·catalog 반영이 별도 승인 뒤에만 가능하므로 rollout 단계 checklist로 분리하고 완료로 가장하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 3단계 SSO fallback·계정 고정·설치 경험·이름 통일이 구현되고 기존 권한·알림 event identity·승인 대기 흐름이 회귀 없이 보존된다.
- UX: 진입 gate 상태 분리, Teams narrow·390px overflow 0, 실패 화면마다 명확한 다음 행동.
- 자동 테스트: 위 14절 항목 전부 통과. 옛 이름이 사용자-facing 문자열 검증에서 검출되지 않는다.
- 5종 산출물: `docs/12-task-completion-policy.md`의 상태·위치 추적을 따른다.
- 사용자 검수 상태: Android·iPhone·PC 필수 검증 통과 전에는 완료로 판정하지 않는다.
- PR 상태: 사용자 검수 대기 중에는 Draft 유지, merge는 별도 승인.
- 중단 조건: 신규 migration·DB 변경이 필요해지는 경우, NAA·getAuthToken 모두 기존 token 계약 보존이 불가능한 경우, Teams manifest 변경이 기존 Activity 발송 계약과 충돌하는 경우 — 구현을 멈추고 blocking decision으로 보고한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | Teams SSO token 취득 방식 | A: MSAL NAA(권장) / B: teams-js `getAuthToken` 직접 수용 | 대기 |
| 2 | PWA 설치 안내 진입점 배치 | A: 로그인·프로필 영역 상시 안내(권장, 설치 후·Teams 내 숨김) / B: 신호 발생 시 일시 배너 | 대기 |
| 3 | Web Push 발송 대상 알림 유형과 기본값 | 후속 Web Push `NEW_FEATURE`에서 결정 | 후속 Task |
| 4 | 사용자별 푸시 설정과 해제 불가 범위 | 후속 Web Push `NEW_FEATURE`에서 결정 | 후속 Task |
| 5 | 푸시 권한 요청 시점·거절 복구·기기별 구독 lifecycle | 후속 Web Push `NEW_FEATURE`에서 결정 | 후속 Task |
| 6 | 푸시 클릭 시 deep link 정책 | 후속 Web Push `NEW_FEATURE`에서 결정 | 후속 Task |
| 7 | 푸시 전용 최소 Service Worker·구독 저장소·migration·네 번째 delivery channel | 후속 Web Push `NEW_FEATURE`에서 결정 | 후속 Task |

1·2번은 권장안이 있는 비차단 결정으로, planning 승인 시 함께 확정할 수 있다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Notifications/NotificationOptions.cs`, `Notifications/NotificationDeliveryStore.cs`, `Notifications/NotificationDeliveryEndpointExtensions.cs`, `Notifications/TeamsActivityClient.cs`, `Materials/IqcPdfRenderer.cs`, `QualityInspections/QualityInspectionPdfRenderer.cs`, `Calendar/CalendarHolidayExcelParser.cs`, `appsettings*.json` 표시명 기본값
- Frontend: `frontend/src/auth.ts`, `frontend/src/App.tsx`(Teams 진입 gate·설치 안내·이름 문자열·sanitizer), `frontend/src/api.ts`(sanitizer), `frontend/index.html`, `frontend/public/manifest.webmanifest`, `frontend/public/icons/`(빨간 로고·흰 바탕 세트 교체), `@microsoft/teams-js`·MSAL 관련 dependency 조사
- DB/Migration: 없음(변경 발생 시 중단·보고)
- Tests/Scripts: `frontend/tests/`(App·auth), `frontend/e2e/auth-shell/`, `scripts/test-pwa-assets.sh`, `scripts/validate-azure-pilot-artifacts.sh`, `scripts/build-teams-manifest-package.sh`, `infrastructure/teams/manifest.template.json`과 Teams icon 자산
- Docs: Product Roadmap 상태 갱신, rollout·rollback SOP·user manual(설치 방법·Teams 진입 안내), 사용자 검수 checklist

## 18. Roadmap 연결

- 선행 Task: TASK-INFRA-001(웹 Microsoft 365 로그인), TASK-NOTIFY-003(Teams Activity Feed), TASK-AZURE-DEPLOY-001(운영 배포·기존 manifest·PWA 정적 자산) — 모두 완료.
- 후속 Task: 모바일 Web Push `NEW_FEATURE`(16절 3~7번), Teams catalog·Entra·운영 설정 반영 rollout(별도 승인), 운영 release(Azure image).
- 현재 Go/No-Go: Roadmap의 제품 다음 Gate가 "Teams SSO·새 manifest 기획"으로 이 Task와 일치한다. Open P0/P1 없음 전제의 기획 Go.
- 별도 Task로 분리할 항목: Web Push 일체, 실제 Entra·Teams Admin Center·Azure 운영 반영, encrypted backup 등 기존 별도 승인 항목.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-08-07 | Interview Round 5 요약 승인 | 이 planning의 0절 기준선으로 반영 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

사용자가 이 planning과 Codex review resolution을 승인한 뒤, 새 Codex 구현 세션은 다음 순서로 진행한다.

1. instruction chain·Task 문서를 다시 읽고 `APPROVED_FEATURE_IMPLEMENTATION`으로 시작한다. 이 planning의 5절 필수 항목과 13절 경계만 구현한다.
2. 이름·설명 계약을 한 곳(공유 상수 또는 검증 스크립트 기대값)에 고정하고 Frontend·Backend·manifest·스크립트를 그 계약에 맞춘다. 발송 이력 원문은 소급 수정하지 않는다.
3. Teams 진입 gate는 확정된 3단계 fallback과 상태 분리를 구현하고, 권장안 A(NAA)를 우선 조사한다. 기존 token 계약을 보존할 수 없으면 중단하고 보고한다.
4. Service Worker·DB migration·실제 provider 발송·운영 설정 반영을 포함하지 않는다.
5. 14절 자동 검증을 실행하고, Android·iPhone·PC 사용자 검수 checklist를 작성해 사용자 검수 대기 상태로 handoff한다. Teams SSO 실검증은 Entra·catalog rollout 승인 뒤의 별도 checklist로 남긴다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 7
