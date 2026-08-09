Baseline is fully refreshed. Below is the requested deep-interview Round 1 artifact.

---

# TASK-TEAMS-PWA-001 — Deep Interview Round 1 (Fable 5)

## Repository 기준선 확인

interview 문서(`tasks/teams-pwa-001-interview.md`)와 현재 구현을 대조해 다음을 확인했다.

- 웹 인증은 MSAL Browser 기반 Entra 로그인이며 세션 기억 여부(localStorage/sessionStorage), 다중 계정 감지와 silent token 갱신·interaction-required 판정이 이미 구현되어 있다 (`frontend/src/auth.ts`). Backend는 JWT Bearer와 앱 내부 역할을 authoritative하게 검증한다.
- Teams JS SDK는 Teams context 추정(`isLikelyTeamsContext`), context 초기화와 Activity 알림 화면 라우팅에만 사용되고 `authentication.getAuthToken` 기반 Teams SSO 흐름은 없다 (`frontend/src/App.tsx`). Teams tab에서 로그인이 필요하면 별도 인증 fallback 화면(`TeamsActivityAuthFallback`)을 표시한다.
- Teams manifest v1.19 template의 표시명은 short `PMS`/full `프로젝트 통합관리시스템`, 개인 static tab, `identity` 권한, Activity resource-specific 권한, `webApplicationInfo`와 10개 activity type이 있다 (`infrastructure/teams/manifest.template.json`).
- 웹 표면 이름은 web manifest `name: EMI 프로젝트 통합관리시스템`/`short_name: EMI QMS` (`frontend/public/manifest.webmanifest`), 브라우저 title·application-name·apple-mobile-web-app-title `EMI QMS` (`frontend/index.html`), 앱 내부 header·로그인 화면 `EMI 프로젝트 통합관리시스템` (`frontend/src/App.tsx`)으로 서로 다르다.
- PWA는 standalone manifest·any/maskable/Apple touch icon까지만 있고 Service Worker·오프라인 cache·설치 안내 UX는 없다. Roadmap Decision Log(2026-08-04)는 브랜드 자산 교체 시 “기존 Service Worker·offline cache 제외 정책”을 보존한다고 기록했다.
- 공개 Frontend 앞에는 Entra 사전 인증이 적용되어 익명 shell·bundle·PWA asset 접근이 차단된다(Roadmap Decision Log 2026-08-06). Roadmap 다음 Gate는 “Teams SSO·새 manifest 신규 기능 기획”으로 이 Task와 일치한다.

아래 5개 질문은 모두 Teams·웹 두 표면의 진입·인증·브랜드 계약을 확정하는 서로 연결된 결정이다.

### 질문 1 — Teams tab에서 SSO가 실패하면 사용자를 어떤 순서로 복구시키는가

- 필요한 이유: 이번 Task의 핵심 문제가 “Teams 앱 화면이 열리지 않거나 로그인 버튼이 작동하지 않는다”이다. Teams silent SSO는 최초 동의(consent), 조건부 액세스·MFA 재인증, 게스트·계정 상태 문제로 실패할 수 있고, 실패 시 사용자 경험을 확정해야 상태 화면·fallback 구현 범위가 정해진다.
- 답변이 바꾸는 범위: Teams tab 인증 상태 머신(초기화→silent SSO→대화형 인증→외부 브라우저), `TeamsActivityAuthFallback` 화면의 대체·유지 여부, 실패 상태별 안내 문구와 UAT 시나리오.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 단계적 fallback: silent SSO → tab 안 대화형 인증 → 외부 브라우저 안내 | silent 실패 시 tab 안에서 대화형 인증(팝업)을 한 번 시도하고, 그래도 실패하면 “브라우저에서 열기” 버튼으로 웹 로그인 경로 제공 | 대부분의 실패(동의·MFA)를 Teams 안에서 해결. 최후 수단으로 항상 동작하는 웹 경로 보장 | 상태 화면과 검증 시나리오가 가장 많음 |
| B. silent 실패 시 즉시 외부 브라우저로 전환 | tab 안 대화형 인증 없이 바로 브라우저 열기 안내 | 구현 단순, Teams 클라이언트별 팝업 차이 회피 | 동의 1회·MFA만 필요한 사용자도 매번 Teams를 이탈해 “같은 제품” 경험 목표가 약화됨 |
| C. 현행 유지: 실패 시 Activity 알림 전용 안내 | Teams는 알림 확인용으로만 쓰고 업무는 웹 안내 | 변경 최소 | 이번 Task의 해결 대상 문제를 그대로 남김 |

- 권장안: **A**. Teams SSO의 알려진 실패 유형(최초 동의, 조건부 액세스 재인증)은 tab 안 대화형 인증으로 해결 가능한 경우가 많고, 최종 fallback으로 기존 웹 로그인을 보존하면 rollback 요구(“기존 웹 로그인과 Activity 알림 전용 운영으로 복귀 가능”)와도 일치한다.

### 질문 2 — Teams 안에서는 어떤 계정을 쓰게 하고, 웹 세션 계정과 다르면 어떻게 하는가

- 필요한 이유: 웹은 세션 기억 선택과 다중 계정 감지·계정 선택이 있지만, Teams tab은 Teams에 로그인된 조직 계정이 이미 존재한다. interview 5장의 “중복 session과 account 전환 기준”이 미확정이라 인증 상태 저장·계정 UI 노출 범위를 정할 수 없다.
- 답변이 바꾸는 범위: Teams tab에서의 계정 선택·로그아웃·세션 기억 UI 노출 여부, MSAL cache와 Teams SSO 결과의 우선순위, 계정 불일치 시 안내 흐름, 관련 테스트 시나리오.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. Teams 계정 강제 | Teams tab에서는 Teams 로그인 조직 계정만 사용. 계정 선택·로그아웃 UI는 숨기고, 기존 웹 세션 계정과 다르면 Teams 계정으로 전환 | Teams 표준 SSO 기대와 일치. 계정 혼동·중복 세션 문제 원천 차단 | 한 기기에서 다른 계정으로 업무하던 사용자는 웹 브라우저를 써야 함 |
| B. 웹과 동일한 계정 선택 허용 | Teams 안에서도 계정 선택·전환 UI 유지 | 예외적 다중 계정 사용자 수용 | SSO 의미 약화, Teams 계정과 앱 계정 불일치 상태가 상시 존재해 권한·감사 혼선 위험 |

- 권장안: **A**. Teams SSO의 목적 자체가 “Teams에 로그인한 조직 계정으로 자연스럽게 진입”이며, Backend 권한은 계정 기준으로 authoritative하게 검증되므로 Teams 표면에서 계정 선택지를 없애는 편이 안전하다. 웹 표면의 기존 세션 기억·계정 선택 동작은 변경하지 않는다.

### 질문 3 — `EMI PMS` 표시명을 어느 표면까지 적용하고, 전체 이름(full name)은 무엇으로 하는가

- 필요한 이유: 표시명 `EMI PMS`는 확정됐지만 적용 표면이 미확정이다. 현재 Teams manifest(short `PMS`), web manifest(`EMI QMS`/`EMI 프로젝트 통합관리시스템`), 브라우저 title(`EMI QMS`), 앱 내부 header·로그인 화면(`EMI 프로젝트 통합관리시스템`)이 서로 달라, 어디까지 바꿔야 “하나의 제품” 목표가 달성되는지 확정해야 한다. Teams manifest와 web manifest는 짧은 이름 외에 전체 이름·설명도 요구한다.
- 답변이 바꾸는 범위: Teams manifest name/description, web manifest name/short_name, `index.html` title·meta, 앱 내부 header·로그인 화면·알림 화면 제목 문자열과 이를 검증하는 기존 테스트들의 변경 범위.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 모든 사용자 표면 통일: 짧은 이름 `EMI PMS`, 전체 이름 `EMI PMS — 프로젝트 통합관리시스템`(예시) | Teams·web manifest·브라우저 title·앱 내부 header·로그인 화면을 모두 새 이름 체계로 통일 | 설치 아이콘·tab·화면 어디서나 같은 제품명. 이번 Task 목표와 정확히 일치 | 이름을 검증하는 기존 테스트·E2E 다수 수정 필요 |
| B. 설치 표면만 통일 | Teams manifest·web manifest·title만 `EMI PMS`로 바꾸고 앱 내부 header 문자열은 유지 | 변경·회귀 범위 최소 | 아이콘은 `EMI PMS`인데 열면 다른 이름이 보여 통일 목표가 부분 달성에 그침 |

- 권장안: **A**. 다만 전체 이름(full name)과 한 줄 설명은 사용자 확정이 필요하다: 짧은 이름 `EMI PMS`와 함께 쓸 전체 이름을 알려 달라(예: `EMI PMS — 프로젝트 통합관리시스템`, 또는 한국어 전체명 유지 등). 내부 `Emi.Qms` solution·namespace는 확정대로 변경하지 않는다.

### 질문 4 — 웹 PWA 범위는 “설치형 경험”까지인가, Service Worker·오프라인까지인가

- 필요한 이유: interview 포함 후보의 “웹 PWA 설치·실행 사용자 경험”이 어디까지인지에 따라 기획 범위가 크게 달라진다. Roadmap Decision Log는 지금까지 Service Worker·offline cache 제외 정책을 보존해 왔고, 공개 Frontend의 Entra 사전 인증은 익명 asset 접근을 차단하므로 오프라인 정적 cache와 충돌 소지가 있다.
- 답변이 바꾸는 범위: Service Worker·cache·업데이트 정책 포함 여부, 설치 안내 UX(브라우저별 안내 화면) 포함 여부, UAT 검증 항목과 위험 범위.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 설치형 경험까지만: manifest·이름·아이콘 정비 + 앱 내 설치 안내 UX, Service Worker 없음 | 설치 가능 조건을 정비하고, 설치를 지원하는 브라우저에는 안내(설치 유도)·iOS는 수동 추가 안내 제공 | 기존 제외 정책·Entra 사전 인증과 충돌 없음. 위험 대비 사용자 가치가 큼 | 오프라인 실행·웹 push는 계속 불가 |
| B. Service Worker·오프라인 cache 포함 | 오프라인 shell cache와 업데이트 흐름까지 신규 설계 | 완전한 PWA 경험 | 기존 제외 정책 변경 필요. 사전 인증·보안 검사와의 상호작용, stale bundle·업데이트 실패 등 새 위험 축이 추가되어 이번 Task 범위가 크게 팽창 |
| C. manifest·이름·아이콘 정비만 | 설치 안내 UX 없이 브라우저 기본 설치 UI에만 의존 | 최소 범위 | 사용자가 설치 방법을 몰라 “설치형 앱 실행” 목표 달성이 불확실. 특히 iOS는 안내 없이는 사실상 미노출 |

- 권장안: **A**. 기존 Service Worker 제외 정책을 바꾸지 않으면서 “EMI PMS를 설치형 앱으로 실행한다”는 사용자 목표를 달성하는 최소·안전 범위다. 오프라인·웹 push가 실제로 필요해지면 별도 `NEW_FEATURE`로 분리한다.

### 질문 5 — 필수 검증 클라이언트와 실제 배포(rollout) 순서를 어떻게 확정하는가

- 필요한 이유: Teams tab SSO는 Teams desktop·web·mobile 클라이언트별 동작 차이가 있고, PWA 설치는 브라우저·OS별(Edge/Chrome, Android, iOS) 경험이 다르다. 또한 새 manifest의 Teams catalog 업로드와 Entra 설정 변경은 이번 Task에서 제외된 실제 운영 변경이라, 무엇을 UAT 필수로 삼고 어떤 순서로 별도 승인·배포할지 성공 기준에 고정해야 한다.
- 답변이 바꾸는 범위: planning의 자동 검증·사용자 검수 계획, UAT 필수 클라이언트 목록, rollout 단계(웹 배포 → Entra/Teams catalog 반영)와 rollback 기준.

| 선택지 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 주 사용 표면 필수 + 단계적 rollout | Teams desktop·web과 데스크톱 Edge/Chrome PWA 설치, 모바일 390px·Teams narrow를 UAT 필수로 하고, 웹 배포·검증 뒤 Teams catalog/Entra 반영을 별도 승인 단계로 순서화. Teams mobile·iOS 설치는 확인 항목으로 포함하되 차단 기준은 아님 | 실패 시 기존 웹 로그인·Activity 전용 운영으로 즉시 복귀 가능. 검증 부담이 현실적 | Teams mobile·iOS 고유 문제는 늦게 발견될 수 있음 |
| B. 전 클라이언트 동시 필수 | Teams desktop·web·mobile, Windows·Android·iOS 설치를 모두 UAT 차단 기준으로 검증 후 일괄 반영 | 표면별 문제를 사전에 모두 확인 | 검증·환경 준비 부담이 커 rollout이 지연되고, 실기기 의존 항목이 차단 기준이 됨 |

- 권장안: **A**. 기존 rollback 요구(웹 로그인·Activity 전용 운영 보존)와 결합하면 단계적 rollout이 안전하다. 실제 Teams catalog·Entra·운영 배포는 확정대로 이번 기획 산출물의 별도 승인 단계로만 남긴다. 사용자·조직에서 Teams mobile 또는 iOS 사용 비중이 높다면 알려 달라 — 그 경우 해당 표면을 필수로 승격한다.

---

- interviewStatus: QUESTIONS_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
