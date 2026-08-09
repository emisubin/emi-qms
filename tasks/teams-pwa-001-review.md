# TASK-TEAMS-PWA-001 — Codex 기획 검토

- reviewTarget: `tasks/teams-pwa-001-planning.md`
- reviewType: `PRODUCT_AND_REPOSITORY_FEASIBILITY`
- reviewStatus: `CHANGES_REQUIRED_BEFORE_IMPLEMENTATION`
- planningApproved: false
- implementationApproved: false
- reviewedBy: `CODEX`
- reviewedAt: `2026-08-07`

## 1. 결론

Fable 기획의 제품 방향은 맞다. `EMI PMS` 이름 통일, Teams 조직 계정 고정, 웹 로그인 동작 보존, 단계적 인증 복구, PWA 설치 경험과 Web Push 분리는 모두 유지할 가치가 있다.

그러나 현재 기획 그대로는 구현을 시작하면 안 된다. 운영 Frontend의 Azure Container Apps Easy Auth가 `/health/live`를 제외한 HTML·JavaScript·PWA asset을 인증 전에 차단한다. Teams NAA 코드는 SPA가 먼저 로드된 뒤에야 실행되므로, Teams webview가 Easy Auth 세션을 만들지 못하는 현재 증상에서는 NAA 코드까지 도달하지 못할 수 있다. 기획은 이 두 인증 계층이 함께 동작한다고 가정했지만 실제 진입 계약을 정의하지 않았다.

따라서 판정은 다음과 같다.

- 제품 기획: `CONDITIONAL_GO`
- 바로 구현: `NO_GO`
- 구현 전 필수 resolution: 현재와 동일한 Easy Auth 사전 인증 상태에서 Teams desktop·web·mobile이 SPA 시작점까지 도달하는지 확인하고, 도달하지 못하면 익명 bundle 차단을 보존하는 별도 Teams 진입 구조를 먼저 기획한다.

## 2. 사용자 문제와 Roadmap 정합성

- Roadmap의 다음 제품 Gate가 Teams SSO·새 manifest 기획이므로 Task 순서는 맞다.
- 사용자가 겪은 “Teams 앱 화면이 안 나올 때가 있고 로그인 버튼이 눌리지 않는다”는 현재 `loginRedirect` 중심 SPA 인증과 iframe/webview의 조합으로 설명되는 부분이 있다.
- 다만 운영에서는 SPA 앞에 별도 Easy Auth가 있으므로 증상 원인을 앱 코드 하나로 단정할 수 없다. `Easy Auth 진입 실패`와 `SPA가 열린 뒤 MSAL redirect 실패`를 분리해 재현해야 한다.
- PWA 설치와 이름 통일은 Teams 인증과 독립적으로 구현·검증할 수 있어 사용자 가치가 명확하다.

## 3. 유지·추가·보류·제거 권고

### 유지

1. 모든 사용자 이름 칸을 `EMI PMS`로 통일하고 한국어 전체 이름과 영문 의미는 설명에만 사용한다.
2. Teams에서는 Teams 조직 계정으로 고정하고 계정 선택·로그아웃을 숨긴다. 일반 웹의 세션 기억·다중 계정 동작은 보존한다.
3. silent SSO → Teams 안 대화형 인증 → 외부 브라우저 안내 순서를 유지한다.
4. Backend bearer·앱 역할·승인 대기·프로젝트 접근 권한을 최종 판정으로 유지한다.
5. Web Push·Service Worker·DB migration을 별도 후속 신규 기능으로 분리한다.
6. 기존 Teams Activity 10개 event type, 수신자, 발송 시점과 deep link identity를 바꾸지 않는다.
7. 모바일 제조·품질 사용자를 위해 Android·iPhone 설치·실행·진입을 필수 사용자 검수로 둔다.

### 추가

1. **사전 인증 진입 Gate**
   - 운영과 동일한 `Require authentication + RedirectToLoginPage`에서 Teams tab이 `index.html`과 main bundle을 받기 전에 어떤 응답을 받는지 desktop·web·mobile별로 확인한다.
   - SPA가 로드되지 않으면 NAA 구현을 진행하지 않는다.
   - 익명 shell·bundle 차단을 해제하는 임시 우회는 허용하지 않는다.

2. **검증된 Teams context 판정**
   - 현재 `window.self !== window.top`이면 모두 Teams로 보는 heuristic은 다른 iframe도 Teams로 오인한다.
   - `teamsApp.initialize()`와 `getContext()` 성공을 authoritative signal로 사용하고, URL/referrer는 초기 후보 신호로만 사용한다.

3. **Teams 우선 bootstrap 순서**
   - Microsoft 공식 NAA 계약은 TeamsJS 초기화 후 MSAL을 초기화하도록 요구한다.
   - 현재 `main.tsx`는 Teams 초기화 전에 일반 `PublicClientApplication`을 만든다. Teams host에서는 `createNestablePublicClientApplication`, 일반 웹에서는 기존 `PublicClientApplication`을 선택하는 bootstrap 경계가 필요하다.

4. **NAA 전용 API 제약 반영**
   - NAA에서는 현재 웹 코드가 쓰는 `loginRedirect`, `logoutRedirect`, `setActiveAccount`를 그대로 사용할 수 없다.
   - Teams에서는 `acquireTokenSilent`/`acquireTokenPopup`과 Teams context의 계정 단서로 계정을 고정하고, 웹에만 기존 redirect·logout·active-account 동작을 남긴다.

5. **외부 브라우저 fallback의 업무 위치 보존**
   - 브라우저로 전환할 때 현재 알림·프로젝트·업무 경로를 보존한다.
   - token, tenant/client identifier나 민감한 context 원문은 URL에 넣지 않는다.

6. **PWA 설치 상태 계약**
   - `beforeinstallprompt`가 실제 발생한 Android/Chromium·PC에서만 앱 내부 설치 버튼을 활성화한다.
   - event가 없는 브라우저에는 브라우저 메뉴를 이용한 수동 설치 안내를 제공한다.
   - iPhone은 Share → 홈 화면에 추가 안내를 제공하고, `display-mode: standalone`이면 안내를 숨긴다.
   - 설치 완료·Teams 내부에서는 설치 안내를 숨긴다.

7. **브랜드 문자열 재유입 방지**
   - UI, manifest, HTML metadata, 메일 기본값·본문, Teams 대체 제목, PDF metadata, Excel header를 검사한다.
   - 과거 알림 원문과 내부 namespace·asset 경로·개발 문서 이력은 사용자에게 노출되지 않는 한 일괄 rename하지 않는다.
   - Azure 운영 환경변수의 실제 발신자 표시명은 rollout 검수에서 별도로 확인한다.

8. **Teams 아이콘 규격 예외**
   - Teams color icon은 사용자 결정대로 흰 배경의 빨간 EMI 로고를 사용한다.
   - Teams outline icon은 플랫폼 규격상 32×32 투명 배경의 흰색 outline이어야 한다. 빨간 로고·흰 배경을 그대로 복제하지 않는다.

### 보류

1. NAA token prefetch와 manifest `nestedAppAuthInfo`는 보류한다. 사용하려면 manifest 1.22 이상, runtime token request와 정확히 일치하는 client id·redirect URI·scope가 필요하다.
2. `teams-js authentication.getAuthToken()`을 Backend가 추가 audience로 직접 수용하는 방식은 보류한다. 현재 Backend token 계약과 Entra app registration 경계를 넓히므로 NAA 실패 시 자동 fallback으로 사용하지 않는다.
3. Web Push, 최소 Service Worker, subscription DB와 사용자별 푸시 정책은 확정대로 후속 Task에 둔다.
4. 실제 Entra app registration, Teams Admin Center catalog, Azure runtime 변경과 운영 발신자 설정은 별도 rollout 승인에 둔다.

### 제거 또는 정정

1. “NAA를 넣으면 Backend 변경 없이 Teams SSO가 완성된다”는 가정을 제거한다. token audience는 보존 가능하지만, SPA가 Easy Auth 앞단을 통과할 수 있는지가 먼저다.
2. “Teams color/outline 모두 흰 바탕 빨간 로고”라는 표현을 제거한다. outline은 Teams 규격을 따른다.
3. “Android·PC에서는 항상 앱 내부 설치 버튼이 열린다”는 보장을 제거한다. `beforeinstallprompt`는 브라우저가 installability 조건을 만족한다고 판단할 때만 제공된다.
4. “추가 로그인 없이 진입”은 정상 경로의 목표로 표현하고, 최초 동의·MFA·조건부 액세스에서는 대화형 인증이 필요하다는 예외를 함께 표시한다.

## 4. 핵심 Finding

| ID | 우선순위 | 상태 | Finding | 필요한 resolution |
| --- | --- | --- | --- | --- |
| `TPWA-R01` | P1 | `OPEN_BLOCKING` | Easy Auth가 SPA보다 앞에서 모든 shell·asset을 차단한다. SPA 내부 NAA만으로 현재 Teams blank/login 실패를 해결한다고 보장할 수 없다. | 운영 동형 Teams 진입 probe. SPA 미도달이면 보안 경계를 보존하는 별도 진입 설계 승인 |
| `TPWA-R02` | P1 | `OPEN_BLOCKING` | 현재 manifest `webApplicationInfo.id`는 Activity 전용 app client를 사용하고 Frontend MSAL은 별도 SPA client를 사용할 수 있다. NAA prefetch 또는 manifest SSO metadata를 추가하면 두 app registration의 역할 충돌 가능성이 있다. | 실제 app registration 역할을 privacy-safe하게 대조. 기존 Activity 발송 계약을 깨지 않는 NAA 설정 확정 |
| `TPWA-R03` | P2 | `RESOLVED_IN_REVIEW` | 현재 app bootstrap은 TeamsJS보다 MSAL을 먼저 만들고 NAA가 지원하지 않는 redirect/logout/active-account API를 사용한다. | host별 auth adapter와 Teams-first initialization을 구현 계약에 추가 |
| `TPWA-R04` | P2 | `RESOLVED_IN_REVIEW` | `isLikelyTeamsContext`가 모든 iframe을 Teams로 취급한다. | TeamsJS 초기화·context 성공 기반 판정으로 교체 |
| `TPWA-R05` | P2 | `RESOLVED_IN_REVIEW` | Service Worker가 없는 상태에서 custom install event가 모든 Chromium 환경에서 발생한다고 보장할 수 없다. | event 기반 버튼 + 플랫폼별 수동 설치 fallback을 완료 기준으로 사용 |
| `TPWA-R06` | P2 | `RESOLVED_IN_REVIEW` | Teams outline icon을 일반 컬러 아이콘과 같은 방식으로 만들면 manifest/store 규격에 맞지 않는다. | 192×192 color와 32×32 white/transparent outline을 별도 생성·검증 |

P1 두 건은 코드 작성 전 해소해야 한다. 이는 새 업무 정책 질문이 아니라 현재 보안·Entra 구조를 확인하는 기술 선행조건이다.

## 5. 권장 기술 선택

### Teams token 방식

기획의 A안인 MSAL NAA를 유지한다. 현재 `@azure/msal-browser` 5.16.0은 NAA 최소 지원 버전보다 높고, NAA는 기존 API scope token을 직접 요청할 수 있어 Backend audience를 늘리지 않는 방향과 맞는다.

단, 다음 조건을 모두 만족할 때만 구현한다.

- Teams tab이 Easy Auth를 거쳐 SPA bootstrap까지 도달한다.
- NAA용 SPA client와 기존 Activity용 `webApplicationInfo.id` 관계를 확인해 Activity 발송을 깨지 않는다.
- TeamsJS 초기화가 MSAL 초기화보다 먼저 수행된다.
- Teams에서는 redirect/logout/setActiveAccount를 사용하지 않는다.

조건이 충족되지 않으면 B안을 자동 채택하지 않고 중단한다.

### PWA 설치 안내 배치

기획의 A안을 유지하되 “상시 강제 팝업”이 아니라 다음처럼 제한한다.

- 모바일 브라우저 최초 진입에는 현재 디자인을 따르는 닫을 수 있는 설치 안내를 제공한다.
- 로그인 화면과 계정/프로필 영역에는 언제든 다시 열 수 있는 설치 진입점을 둔다.
- Android·PC는 install event가 있을 때만 한 번 누르는 설치 버튼을 보여 준다.
- iPhone은 수동 절차를 보여 준다.
- Teams와 이미 설치된 standalone 환경에서는 숨긴다.

## 6. 권장 개발 순서

1. **선행 확인**: Easy Auth + Teams tab 진입과 Entra app-registration 역할을 read-only로 대조한다.
2. **브랜드 계약 테스트**: 정확한 이름·설명·사용자 노출 경계를 먼저 테스트로 고정한다.
3. **Host bootstrap 분리**: TeamsJS 초기화·context 확인 뒤 Teams NAA 또는 일반 웹 MSAL을 선택한다.
4. **Teams auth state machine**: silent → popup → browser fallback, 계정 고정, deep-link 보존을 구현한다.
5. **PWA 설치 UX**: install event·iPhone 수동 안내·standalone/Teams 숨김을 기존 디자인 system으로 구현한다.
6. **브랜드와 아이콘 적용**: manifest, HTML, UI, mail/PDF/Excel 기본 문자열과 플랫폼별 아이콘을 갱신한다.
7. **자동 검증**: web auth 회귀, Teams 상태 전이, PWA 상태, manifest schema/package, icon dimension/pixel, Backend 출력과 옛 이름 검사를 실행한다.
8. **분리 검증과 사용자 검수**: 코드 검증 후 Android·iPhone·PC, Teams desktop/web을 검수한다. Teams mobile은 비차단 확인으로 둔다.
9. **별도 rollout**: 웹 배포·검증 뒤 Entra/Teams catalog 변경을 승인받아 적용한다. 실제 Teams SSO가 성공하기 전에는 Task를 완료로 표시하지 않는다.

## 7. 검증 보강안

### 자동 검증

- 일반 웹: 기존 remember-session, 단일·다중 계정, redirect 로그인·logout 회귀
- Teams: TeamsJS init 성공/실패, silent 성공, interaction-required, popup 성공/실패, browser fallback, 계정 고정, 승인 대기
- context: 일반 top-level, 일반 iframe, Teams web/desktop context의 오인·누락 방지
- deep link: Activity의 notification/detail 경로가 인증 전후와 외부 브라우저 fallback에서 보존
- PWA: install event 없음/있음, prompt accept/dismiss, `appinstalled`, iOS guide, standalone/Teams 숨김
- manifest/package: schema validation, 10개 activity type 불변, RSC permission 불변, unresolved placeholder 0
- icon: PWA 180/192/512 및 maskable, Teams color 192×192, outline 32×32와 투명/흰색 규격
- 이름: 사용자 surface의 금지 이름 scan과 `EMI PMS` exact 문자열 assertion
- Backend: mail sender/body, Teams fallback title, 두 PDF metadata, holiday Excel header 회귀

### 사용자 검수

- Android Chrome 설치·실행·로그인·제조/품질 진입
- iPhone Safari 홈 화면 추가·standalone 실행·로그인·제조/품질 진입
- PC Chrome/Edge 설치·실행과 일반 부서 화면 진입
- Teams desktop/web silent SSO, MFA/동의 popup, Activity deep link, browser fallback
- Teams mobile은 화면 진입과 알림 deep link를 비차단 확인

## 8. 공식 플랫폼 근거

- Microsoft Teams NAA: https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/authentication/nested-authentication
- MSAL nested initialization: https://learn.microsoft.com/en-us/entra/msal/javascript/browser/initialization
- Azure Container Apps Easy Auth: https://learn.microsoft.com/en-us/azure/container-apps/authentication
- Teams icon schema: https://learn.microsoft.com/en-us/microsoft-365/extensibility/schema/root-icons
- Chrome installability change: https://developer.chrome.com/blog/update-install-criteria
- Chrome install prompt: https://developer.chrome.com/blog/app-install-banners-native
- iOS/iPadOS Home Screen web app: https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/

## 9. 사용자 승인 시 확정할 resolution

Planning 승인 시 다음을 함께 승인 대상으로 삼는다.

1. Teams token 방식은 NAA 우선으로 하되 `TPWA-R01`·`TPWA-R02` 선행 확인 실패 시 구현을 중단한다.
2. PWA 설치 안내는 모바일 최초 안내 + 로그인/프로필 재진입점으로 하고, install event가 없는 환경은 수동 안내로 복구한다.
3. Teams outline icon은 플랫폼 규격 예외를 적용한다.
4. Web Push와 실제 provider/runtime 변경은 이번 구현에서 제외한다.

현재는 위 resolution이 사용자에게 제시되기 전이므로 `planningApproved=false`, `implementationApproved=false`를 유지한다.
