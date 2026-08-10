# TASK-TEAMS-PWA-001 구현 보고 — Teams 실행 화면·웹 PWA 설치 경험·브랜드 통일

상태: `Change 001~003·007·009 원격 main 병합·Azure 운영 rollout 완료 / Change 010 local 구현·게시 대기 / 실제 Android 재검수 대기`

## 기준선과 승인 범위

- instructionChainRead: `true`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- canonicalTaskId: `TASK-TEAMS-PWA-001`
- branch: `feat/task-teams-pwa-001-experience`
- baseSha: `914a109e170f4e1c3ce34fb1faa4216c1b4fcf1c`
- planning: `tasks/teams-pwa-001-planning.md`
- Codex review: `tasks/teams-pwa-001-review.md`
- approved resolution: `tasks/teams-pwa-001-change-001.md`, `tasks/teams-pwa-001-change-002.md`, `tasks/teams-pwa-001-change-003.md`, `tasks/teams-pwa-001-change-007.md`, `tasks/teams-pwa-001-change-008.md`, `tasks/teams-pwa-001-change-009.md`, `tasks/teams-pwa-001-change-010.md`
- 포함: Teams 정적 launcher, launcher-only Easy Auth 예외 artifact, PWA 설치 UX, 전 사용자 표면 `EMI PMS` 브랜드 문자열, 관련 자동·브라우저 검증
- 원래 구현 제외: 실제 Azure·Entra·Teams Admin Center·catalog·운영 revision 변경, NAA·OBO·신규 token/session, Web Push·Service Worker·DB migration, 알림 수신자·발송 시점 변경. Change 007의 운영 revision 교체만 후속 사용자 승인으로 실행했다.

## 해결한 업무 문제

Teams tab은 iframe 안에서 기존 전체 화면 MSAL redirect를 실행하려 했고, 운영 Azure Easy Auth는 React SPA가 시작되기 전에 app shell과 bundle을 인증으로 차단한다. 따라서 SPA 안에 Teams SSO 코드를 추가하는 것만으로는 blank 화면과 눌리지 않는 로그인 버튼을 안정적으로 해결할 수 없었다.

이번 구현은 Teams를 기존 Activity Feed 알림 채널과 실행 진입점으로 유지한다. 개인 tab에는 업무 구조·핵심 bundle·API data를 싣지 않는 작은 정적 화면만 표시하고, 사용자가 누르면 Microsoft 365 인증으로 보호된 EMI PMS 웹/PWA를 새 창으로 연다. 이로써 익명 bundle 차단을 유지하면서 iframe redirect 문제를 피한다.

동시에 Teams `PMS`, 웹 설치 `EMI QMS`, 화면 `EMI 프로젝트 통합관리시스템`으로 흩어졌던 이름을 모든 사용자-facing 이름 칸에서 `EMI PMS`로 통일했다. Android·PC는 지원되는 브라우저 설치 prompt를 제공한다. iPhone은 Safari와 타 브라우저를 구분해 현재 브라우저의 설치 메뉴를 먼저 사용하고, 메뉴가 없을 때 root 주소를 복사해 Safari에서 설치하는 복구 안내를 제공한다.

Change 007에서는 로그인 화면용 가로 로고와 로그인 후 공통 shell 로고가 같은 asset을 공유하던 문제를 분리한다. 로그인은 사용자가 지정한 4x 가로 logo, 로그인 뒤 모든 페이지의 공통 desktop/mobile shell은 지정 4x 내부 logo를 사용한다. wireframe의 구조와 흑백 색상은 유지하고 지정 logo에만 원본 색상 예외를 적용한다.

Change 009에서는 운영 모바일에서 Microsoft 365 인증 뒤 홈으로 바로 진입하고 PWA 안내가 보이지 않던 결함을 보정한다. 설치 event 수신은 MSAL 초기화보다 앞에서 시작하되 자동 안내는 access-token gate와 업무 shell이 준비된 뒤에만 연다. Android는 설치 event를 기다리는 동안에도 안내와 비활성 설치 버튼을 먼저 표시하고 event가 도착하면 같은 버튼을 활성화한다. iPhone 절차는 그대로 유지하며 `나중에` 기억은 영구가 아닌 현재 탭 session으로 제한한다.

Change 010에서는 Azure Easy Auth로 보호된 운영 manifest가 credential 없이 요청돼 Android Chrome의 설치 가능 판정이 완료되지 않던 결함을 보정한다. same-origin manifest link에도 `crossorigin="use-credentials"`를 명시해 인증된 manifest 요청을 보장하고, 같은 속성이 빠지면 PWA asset contract가 실패하게 한다. 팝업 정책과 설치 버튼 제어 로직은 변경하지 않는다.

## 전체 아키텍처와 영향

```text
Teams Activity 또는 개인 tab
          ↓
익명 허용 정적 launcher (HTML + 작은 JS + EMI icon)
          ↓ 사용자 클릭 / 같은 origin 경로만 허용
Azure Easy Auth로 보호된 EMI PMS 웹·설치 PWA
          ↓
기존 MSAL 로그인 → 기존 Backend bearer·역할·프로젝트 권한
```

- Frontend: root를 `PwaInstallProvider`로 감싸 설치 event를 MSAL 초기화부터 수신하고, 업무 shell이 인증 준비 완료 신호를 보낸 뒤에만 모바일 자동 안내를 연다. 로그인·계정 영역의 수동 재진입, iOS, Android, standalone, embedded 상태를 분리한다.
- Teams: manifest의 이름·설명·tab URL만 launcher 계약으로 변경한다. Activity type 10개, RSC 권한, `webApplicationInfo` identity는 그대로다.
- Azure: Easy Auth `excludedPaths`에 launcher HTML·script와 실제 사용하는 192px icon만 추가한다. root, main bundle, PWA manifest와 API는 예외가 아니다.
- Backend: 메일·Teams 대체 제목·PDF metadata·휴일 Excel처럼 사용자에게 보이는 제품명 문자열만 바꾼다. API·권한·workflow·notification event는 변경하지 않는다.
- DB/Migration: `N/A` — 저장 모델과 상태 전이가 없어 migration을 추가하지 않았다.
- 첨부파일: `N/A` — 첨부 저장·검사·다운로드 계약을 변경하지 않았다.
- PDF/Excel: 내용·양식 구조는 유지하고 metadata Author와 Excel 첫 머리글의 제품명만 `EMI PMS`로 교체했다.

## 기술적 결정과 검토한 대안

| 결정 | 채택 이유 | 보류·폐기한 대안 |
| --- | --- | --- |
| Teams는 Activity + 정적 launcher | Easy Auth의 익명 bundle 차단과 Teams iframe 제약을 동시에 보존한다. | React SPA 안의 NAA만으로 Teams SSO 완성 |
| 보호된 웹을 새 창에서 열기 | 기존 MSAL·MFA·조건부 액세스·권한을 그대로 재사용한다. | `getAuthToken` + Backend OBO, 신규 cookie bridge |
| launcher-only 익명 예외 | Teams tab은 열리지만 업무 구조·API·manifest는 인증 전에 노출하지 않는다. | root/index.js 전체 익명 허용 |
| 설치 UX만 제공 | 홈 화면·작업 표시줄 접근성을 높이면서 별도 알림 정책·DB를 만들지 않는다. | Service Worker·offline cache·Web Push 동시 구현 |
| 모든 이름 칸 `EMI PMS` | Teams·설치 아이콘·브라우저·앱 화면을 하나의 제품으로 인식하게 한다. | 표면별 `PMS`·`EMI QMS`·한국어 이름 혼용 |
| 인증 완료 뒤 모바일 안내 | 익명 app shell 차단을 유지하면서 로그인 직후 설치 행동을 먼저 제시한다. | Easy Auth 우회, 로그인 전 자동 popup |
| Android 준비형 설치 버튼 | Chrome의 native 설치 정책을 존중하면서 안내는 즉시 표시하고 prompt 준비 뒤 한 번 클릭으로 확인창을 연다. | 무단 강제 설치, event가 올 때까지 안내 자체를 숨김 |
| 인증된 manifest 요청 | Easy Auth 보호를 낮추지 않고 same-origin manifest 요청에 현재 사용자 credential을 포함한다. | manifest 익명 예외 추가, 설치 버튼 강제 활성화 |
| session 단위 닫기 | 같은 탭의 반복 방해는 막되 미설치 사용자가 이후 새 session에서 안내를 다시 받을 수 있다. | 영구 `localStorage` 숨김 |

## 시행착오 및 폐기한 접근

1. Fable primary planning은 NAA를 권장했지만 Codex review에서 Easy Auth가 SPA보다 먼저 동작해 NAA 코드에 도달하지 못할 수 있다는 P1을 확인했다. 사용자 승인 Change 001에서 NAA를 제거하고 정적 launcher 구조로 해소했다.
2. 첫 실제 브라우저 검증에서 launcher가 UUID version/variant까지 제한해 합성·과거 형식의 유효한 알림 ID를 홈으로 보냈다. 외부 URL을 허용하지 않는 안전 경계는 유지하면서 canonical UUID 모양 전체를 받도록 수정했다.
3. iPhone 반복 안내 E2E의 초기 script가 매 reload마다 localStorage를 지워 실제 지속 동작을 잘못 실패로 판정했다. 검수 fixture를 고쳐 닫기 상태가 reload 뒤 유지되는 제품 동작을 확인했다.
4. 최초 Teams·설치 안내 디자인은 넓은 빨간 면, 빨간 왼쪽 강조선과 빨간 그림자를 사용해 최신 Graphite wireframe과 어긋났다. 사용자 Change 002에 따라 로고만 브랜드 예외로 유지하고 흰 표면·검정 버튼·중성 회색·1px 경계로 통일했다.
5. Change 002의 첫 iPhone browser screenshot에서 절차 문장 안의 강조어가 CSS grid item으로 분리돼 줄바꿈이 깨지는 것을 확인했다. 각 절차 문장을 하나의 content span으로 묶고 검정 focus outline을 명시해 수정했다.
6. 최초 iPhone 안내는 모든 iOS 브라우저에 Safari 절차만 표시했다. iOS 16.4 이상에서 타 브라우저도 `홈 화면에 추가`를 제공할 수 있는 공식 계약을 대조하고, 현재 브라우저 우선 → root 주소 복사 → Safari 복구 순서로 Change 003을 구현했다.

## 주요 변경 파일

| 영역 | 위치 | 역할 |
| --- | --- | --- |
| PWA 상태·UI | `frontend/src/PwaInstallExperience.tsx`, `frontend/src/pwa-install.ts`, `frontend/src/App.tsx`, `frontend/src/main.tsx`, `frontend/src/styles.css` | 인증 준비 gate, 이른 설치 event 수신, session 단위 닫기, iPhone·Android 구분 안내, 로그인·계정 재진입점, Graphite wireframe 적용 |
| 정적 Teams 진입 | `frontend/public/teams-launcher.html`, `frontend/public/teams-launcher.js` | 핵심 bundle 없이 보호된 웹·알림 상세를 새 창으로 여는 제한된 launcher |
| 브랜드 metadata | `frontend/index.html`, `frontend/public/manifest.webmanifest` | 브라우저·설치 앱 이름과 설명 통일 |
| Teams package | `infrastructure/teams/manifest.template.json` | `EMI PMS` 명칭·설명과 launcher tab URL, 기존 Activity 계약 보존 |
| Azure auth artifact | `infrastructure/azure-pilot/workloads.bicep`, `workloads.json`, `README.md` | launcher-only Easy Auth 익명 예외와 운영 설명 |
| Backend 사용자 출력 | `backend/src/Emi.Qms.Api/Notifications/`, PDF renderer 2개, 휴일 Excel parser, appsettings | 메일·Teams·PDF·Excel 표시명 통일 |
| 검증 | `frontend/tests/pwa-install.test.tsx`, `frontend/e2e/mock-ui/teams-pwa-experience.spec.ts`, 관련 기존 tests, `scripts/test-pwa-assets.sh`, `scripts/test-teams-manifest-package.sh`, `scripts/validate-azure-pilot-artifacts.sh` | 설치 상태·1440/390 화면·manifest·icon·Bicep·브랜드 회귀 |

## 검증 결과

| 검증 | 결과 |
| --- | --- |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend production build | PASS — 기존 대형 chunk warning 유지 |
| Frontend 전체 unit | PASS — `26 files`, `187/187` |
| PWA 집중 unit | PASS — 인증 준비 gate·좁은 desktop 제외·사용자 클릭 prompt·iPhone Safari·타 브라우저 복사 성공/실패·Android 준비 전/후·session 닫기·standalone 숨김 `10/10` |
| Backend Release build | PASS — 경고 0, 오류 0 |
| Backend 전체 test | PASS — `486/486` |
| PWA asset·launcher contract | PASS — `1/1` |
| Teams manifest package | PASS — package/schema 계약 `2/2` |
| Azure artifact | PASS — Bicep compile, portal template, static validation |
| Mock UI 전체 | PASS — 기존 주요 화면과 신규 Teams/PWA browser smoke `8/8` |
| Browser 집중 | PASS — launcher 1440/390, iPhone Safari·타 브라우저·Android 준비 전/후 안내 390, root 주소 복사, deep link·session dismiss persistence·가로 overflow 0 (`4/4`) |
| Graphite 시각 계약 | PASS — 안내 표면의 장식용 왼쪽 rail 0, 색상 그림자 0, 1px 경계·가로 overflow 0; 실제 EMI logo만 브랜드 색 예외 |
| Git diff | PASS — `git diff --check` 오류 0 |
| Isolated Full-Stack E2E | N/A — API·DB·migration·업무 workflow 계약 변경이 없으며 Backend 전체·Frontend 전체·Mock UI로 영향 경계를 검증했다. |
| Persistent UAT | N/A — 실제 runtime·DB 적용은 승인 범위 밖이고 application data 변경도 없다. |
| actionlint | N/A — `.github/workflows` 변경이 없다. |

Change 007과 Change 009 Azure 운영 release는 완료했다. Teams catalog 변경과 Android/iPhone 실제 기기·운영 메일/PDF/Excel 육안 검수는 이번 자동 release 성공으로 대체하지 않는다.

### Change 010 local 검증

| 검증 | 결과 |
| --- | --- |
| PWA asset contract | PASS — 인증된 manifest link 포함 `1/1` |
| PWA 집중 unit | PASS — `10/10` |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend 전체 unit | PASS — `26 files`, `187/187` |
| Frontend production build | PASS — 기존 대형 chunk warning 유지 |
| Backend·DB·migration·Teams | N/A — 변경 없음 |
| Git 게시·Azure 운영 | 대기 — 사용자 게시·배포 승인 범위 밖 |
| 실제 Android Chrome | 대기 — 운영 반영 뒤 사용자 재검수 필요 |

### Change 009 게시·운영 검증

| 검증 | 결과 |
| --- | --- |
| Git 게시 | PASS — PR #86 squash merge, 원격 `main` merge SHA `e6a446268b0ce9aa7f9492af1e0bd4eb1a76191b` |
| PR CI | PASS — Frontend·Backend·Full-Stack `3/3` |
| main CI | PASS — run `31360415559`, Frontend·Backend·Full-Stack `3/3` |
| Azure 운영 release | PASS — run `31361630803`, exact main SHA 검증·Backend/Frontend immutable image 게시·migration gate·운영 revision 교체 완료 |
| 공개 상태·보안 | PASS — health `200`, 익명 root·`/api/me` `401/401` |
| Teams·DB·migration 변경 | N/A — Change 009에 Teams package, Backend, DB와 신규 migration diff가 없다. |

### Change 007 검증

| 검증 | 적용 여부 | 결과 | 근거/미실행 이유 |
| --- | --- | --- | --- |
| 지정 asset byte equality | 적용 | PASS | 원본과 tracked Asset 3·4 각각의 byte equality와 SHA-256 일치를 확인했다. |
| Frontend 최소 검증 | 적용 | PASS | lint 0 error·기존 warning 1, typecheck, unit `183/183`, production build가 통과했다. |
| desktop·390px browser | 적용 | PASS | 공통 desktop sidebar, mobile app bar·drawer가 natural size `3796×1378`, filter none·transparent background를 사용하고 가로 overflow 0을 유지했다. |
| 로그인 회귀 | 적용 | PASS | desktop 5종과 iPhone 390·Android 412의 login/loading `12/12`, natural size `4265×604`, console/request failure 0을 확인했다. |
| CI 전체 | 적용 | PASS | PR #84 Frontend·Backend·Full-Stack `3/3`, merge SHA main CI 실패 job 재실행 포함 최종 `3/3`이 통과했다. |
| Backend·DB·migration | 적용 대상 아님 | N/A | 공통 shell logo와 CSS만 변경하며 server/data contract diff가 0이다. |
| 운영 공개 검수 | 적용 | PASS | Azure release `31354814082`에서 migration·Backend·Frontend·public security가 PASS. 배포 뒤 health `200`, 익명 root·API `401/401`, Microsoft 365 선인증 redirect를 확인했다. |

## Finding과 잔여 위험

| Finding | 심각도 | 상태 | 원인·영향 | Resolution·후속 |
| --- | --- | --- | --- | --- |
| `TPWA-R01` | P1 | `RESOLVED` | Easy Auth가 SPA보다 앞서 NAA code에 도달하지 못할 수 있었다. | Teams tab에서 SPA를 시작하지 않는 launcher로 해소했다. |
| `TPWA-R02` | P1 | `RESOLVED` | Activity app과 SPA app identity가 다른 상태에서 NAA metadata를 추가하면 기존 알림을 깨뜨릴 수 있었다. | NAA·prefetch·SSO metadata를 추가하지 않고 Activity identity를 보존했다. |
| `TPWA-R03` | P2 | `RESOLVED` | NAA는 현재 redirect/logout 기반 웹 MSAL과 bootstrap 계약이 달랐다. | NAA를 범위에서 제거하고 웹 MSAL을 변경하지 않았다. |
| `TPWA-R04` | P2 | `RESOLVED` | 일반 iframe을 Teams로 오인할 수 있는 heuristic이 있었다. | Teams tab이 SPA를 열지 않아 이 heuristic을 인증 판정에 사용하지 않는다. |
| `TPWA-R05` | P2 | `RESOLVED` | 모든 Chromium 환경에서 설치 event를 보장할 수 없다. | event 기반 설치 버튼과 브라우저 메뉴 수동 안내 fallback을 함께 제공한다. |
| `TPWA-R06` | P2 | `RESOLVED` | Teams outline icon은 일반 color icon과 규격이 다르다. | 기존 32×32 흰 outline·투명 배경과 192×192 color icon을 각각 검증한다. |
| `TPWA-I01` | P2 | `RESOLVED` | launcher의 알림 UUID 검사 범위가 불필요하게 좁아 상세 위치가 홈으로 떨어질 수 있었다. | 외부 URL 차단은 유지하고 canonical UUID 모양 전체를 허용해 browser 회귀를 추가했다. |
| `TPWA-D01` | P2 | `RESOLVED` | 최초 안내 디자인의 빨간 면·왼쪽 강조선·그림자가 최신 Graphite wireframe과 충돌했다. | Teams·iPhone·Android 안내를 흰 표면·검정 제어·중성 회색·1px 경계로 통일하고 로고만 브랜드 예외로 유지했다. |
| `TPWA-IOS01` | P2 | `RESOLVED` | iPhone 타 브라우저 사용자에게 Safari 화면 기준 절차만 표시해 설치 진입점과 화면이 어긋날 수 있었다. | Safari/타 브라우저 안내를 분리하고 현재 브라우저 설치 우선, root 주소 복사와 Safari fallback을 제공했다. |
| `TPWA-PUSH-001` | P3 | `BACKLOG` | 모바일 push는 권한·수신 정책·구독 lifecycle·DB·Service Worker가 필요한 새 채널이다. | 별도 `NEW_FEATURE` deep-interview에서 재확정한다. |
| `TPWA-BRAND-007` | P2 | `RESOLVED` | 로그인과 공통 app shell이 같은 logo asset·grayscale 규칙을 공유해 사용자 지정 내부 logo의 형태와 원색을 보존할 수 없었다. | auth/internal asset import를 분리하고 공통 shell의 지정 logo에만 transparent·filter none 예외와 browser regression을 추가했다. |
| `TPWA-CI-007` | P2 | `RESOLVED` | PR 전체 mock UI에서 프로젝트가 비어 있을 때 상단과 empty-state에 같은 `신규 프로젝트` action이 생겨 기존 test selector가 두 요소를 구분하지 못했다. | 제품 UI는 변경하지 않고 기존 smoke가 상단 첫 action을 명시하도록 두 진입 selector를 한정했다. |
| `TPWA-CI-008` | P2 | `RESOLVED` | 느린 CI에서 품질 판정 API의 첫 오류 응답과 dialog 오류 문구 반영이 기존 1초 test 대기를 넘겨 간헐적으로 실패했다. | 제품 로직은 유지하고 API 호출 관찰과 오류 문구 반영을 각각 최대 5초 기다리는 test-only 동기화로 안정화했다. |
| `TPWA-MOBILE-009` | P2 | `RESOLVED` | Android 자동 안내가 설치 event에 종속되고 닫기가 영구 저장돼 Microsoft 로그인 뒤 설치 안내를 놓칠 수 있었다. | provider를 MSAL보다 위로 이동하고 인증 준비 gate·Android 준비형 버튼·session 단위 닫기 회귀를 추가했다. |
| `TPWA-ANDROID-INSTALL-010` | P1 | `MITIGATED_LOCAL / PUBLICATION_PENDING` | Easy Auth 보호 manifest에 credential 포함 연결이 없어 Android Chrome이 설치 event를 제공하지 못하고 버튼이 계속 비활성화됐다. | manifest link에 `use-credentials`를 추가하고 asset contract로 고정했다. 원격 main·운영 반영과 실제 Android 재검수가 남았다. |

Open P0/P1/P2: `0/1/0` — Change 010 게시·운영 Android 재검수 전까지 P1을 닫지 않는다.

## 개인정보·secret 검토

- 코드·문서·테스트에는 합성 GUID와 `example.org`만 사용했다.
- 실제 사용자 이름·이메일, tenant/client/object id, token, secret, Authorization header와 알림 원문을 기록하지 않았다.
- Change 001~003 구현 검증에서는 실제 provider·Azure·Teams catalog·DB mutation을 실행하지 않았다. Change 007은 사용자 승인 뒤 기존 승인형 workflow로 Azure revision만 교체했으며 업무 DB row와 provider 발송은 변경하지 않았다.
- browser screenshot은 합성 local 화면만 `/tmp/emi-pms-teams-pwa-001`에 만들었고 repository에는 저장하지 않았다.

## 사용자 사용 방법

### 웹·PWA

1. 기존 EMI PMS 운영 주소를 열고 회사 Microsoft 365 계정으로 로그인한다.
2. 모바일 첫 안내 또는 로그인/계정 영역의 `EMI PMS 설치`를 누른다.
3. Android·지원 PC 브라우저는 설치 확인창에서 설치한다. 설치 event가 없으면 브라우저 메뉴의 `앱 설치` 또는 `홈 화면에 추가`를 사용한다.
4. iPhone Safari는 `공유 → 홈 화면에 추가 → 웹 앱으로 열기 → 추가` 순서로 설치한다. 다른 브라우저는 자체 공유 메뉴의 `홈 화면에 추가`를 먼저 사용하고, 메뉴가 없으면 `PMS 주소 복사` 후 Safari 주소창에 붙여넣어 같은 순서로 설치한다.
5. 설치 후 홈 화면·작업 표시줄의 `EMI PMS`를 열고 기존 권한으로 업무를 수행한다.

### Teams

1. Teams Activity 알림 또는 `EMI PMS` 개인 tab을 연다.
2. tab의 `EMI PMS 열기`를 누른다.
3. 보호된 새 창에서 필요하면 회사 Microsoft 365 인증을 완료한다.
4. Activity에서 진입했다면 선택한 알림 상세로, 개인 tab이면 EMI PMS 홈으로 이동한다.

## 운영 적용 SOP

1. 사용자 검수와 Git 게시 승인을 받은 뒤 branch를 게시하고 CI를 확인한다.
2. 새 main SHA로 승인형 GitHub→Azure release를 실행해 Backend·Frontend image를 교체한다. migration은 없으므로 ledger count가 기존과 동일해야 한다.
3. Azure Frontend auth config에서 launcher 3개 경로만 exact 제외되었고 root·asset·manifest·API는 여전히 인증되는지 확인한다.
4. 운영 host·Activity app identity로 Teams package를 생성하고 manifest schema·icon·10개 activity type을 다시 검증한다.
5. Teams Admin Center에 새 package를 올리고 지정 사용자 정책을 적용한다.
6. [사용자 검수 체크리스트](teams-pwa-001-user-validation-checklist.md)의 PC·Android·iPhone·Teams·출력물 항목을 수행한다.

Rollback은 이전 immutable Frontend/Backend revision과 이전 Teams package로 되돌리고, auth config의 launcher-only 세 경로를 제거하는 순서로 수행한다. DB migration이 없으므로 data rollback은 필요하지 않다. launcher 또는 auth config만 실패하면 application 기능을 되돌리지 않고 해당 layer만 forward-fix할 수 있다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 합성 desktop/390px browser 검증은 완료했다.
- Git Commit·Push·PR #84·#86·main merge와 Change 007·009 Azure 운영 release를 완료했다. Teams package와 catalog는 Change 009에서 변경하지 않았다.
- Change 009의 자동·CI·운영 공개 검증은 완료했다. Change 010 local 구현과 자동 검증도 완료했지만 원격 main·운영에는 아직 반영하지 않았다. 운영 반영 뒤 실제 Android에서 설치 버튼 활성화와 native 설치 확인창을 다시 확인해야 한다.
- 사용자 실제 PC·Android·iPhone에서 인증 후 로그인·공통 내부 logo를 눈으로 확인하는 기존 검수도 계속 대기다.
- Web Push는 정책을 다시 확정해야 하는 별도 신규 기능이다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | 본 문서 `운영 적용 SOP` |
| User manual | 포함됨 | 본 문서 `사용자 사용 방법` |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 자동·CI·운영 release 검증 완료 / 사용자 실제 기기 검수 대기 | `tasks/teams-pwa-001-user-validation-checklist.md` |
