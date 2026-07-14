# TASK-DESIGN-LOGIN-001 Implementation report

## 1. 결과

- 상태: Change 008 구현·자동·독립 검증·사용자 검수 완료 / Change 009 fixed allowlist 이식·자동·독립 검증 완료 / 게시·5174 반영 승인, 실행 중
- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION`
- 승인 계약: [Task와 사용자 검수 checklist](design-login-001.md), [Change 001](design-login-001-change-001.md), [Change 002](design-login-001-change-002.md), [Change 003](design-login-001-change-003.md), [Change 004](design-login-001-change-004.md), [Change 005](design-login-001-change-005.md), [Change 006](design-login-001-change-006.md), [Change 007](design-login-001-change-007.md), [Change 008](design-login-001-change-008.md), [Change 009](design-login-001-change-009.md)
- 게시 상태: 2026-07-15 사용자가 stage·commit·push·PR·merge와 5174 Frontend 반영을 일괄 승인해 실행 중이다.
- Runtime 상태: 기존 Development·Review-safe runtime을 종료·재시작·교체하지 않았다.
- 최종 범위: red/white flexible panel이 viewport를 채우고 각 panel 내부 Figma reference content를 등비 반응형으로 유지하는 PC Desktop 기본 로그인·loading 화면. Mobile/390px는 Change 001에 따라 제외했다.

### Change 009 화면 단위 승격

- 최신 `origin/main` `6f3eaf7a1eecd698f9fe9603e170452e70a64e8a`에서 `feat/task-design-login-001-login-promotion` bounded worktree를 생성했다.
- 로그인 제품 경로의 최신 main overlap 0을 확인한 뒤 fixed allowlist만 이식했다. 제품 코드·asset·unit/E2E fixture 12개는 5176 사용자 검수본과 hash가 일치한다.
- Roadmap은 source 전체 복사가 아니라 디자인 관련 projection만 선택 이식해 최신 main의 HTTPS-only 5174·PR #48 기록과 기존 governance 상태를 보존했다.
- auth browser config는 promotion worktree를 실제 검사하도록 환경변수 port와 기본 5187 격리 실행, 기존 server 재사용 기본 false를 적용했다. 이 변경은 제품 runtime configuration이 아니라 allowlist 안의 test configuration이다.
- 이식본에서 lint error 0·기존 warning 1, typecheck, unit 66/66, build, auth browser 12/12와 mock UI 1/1을 통과했다.
- 5187 검증 server는 Playwright 종료와 함께 종료됐다. 5174·5176, Backend·Review-safe·PostgreSQL runtime은 종료·재시작·교체하지 않았다.
- stage·commit·push·PR·merge와 5174 Frontend 반영은 2026-07-15 사용자 승인에 따라 실행한다.

## 2. 해결한 문제와 구현 경계

첫 구현은 기존 기능 보존과 390px 해석을 위해 Figma에 없는 안내·helper·다른 계정 action을 로그인 화면에 표시했다. Change 001은 이 해석을 폐기하고 Desktop 로그인 화면을 Figma node의 실제 element만으로 구성하도록 범위를 좁혔다. Change 004에서 사용자가 안내 문구 1개를 명시 승인해 예외로 다시 추가했다. Change 005에서는 계정 선택을 Microsoft provider 화면에 위임하고 loading을 기본 로그인과 같은 화면으로 통일했다. Change 006에서는 loading의 login control을 제거하고 브랜드 빨간색 회전 indicator로 교체했다. Change 007에서는 로그인 상태 유지 component set의 기본/Variant 2를 직접 읽어 클릭 상태의 icon·text color까지 일치시키고 인증 action 도달 가능성을 audit했다. Change 008에서는 사용자 검수에서 확인된 Done icon의 시각 오프셋만 checkbox 정중앙으로 보정했다.

다음 결과를 구현했다.

- 로그인 화면에는 EMI logo, 제품명, Microsoft logo, `LOGIN`, 로그인 상태 유지 checkbox만 표시한다.
- 명시 승인된 `회사 Microsoft 365 계정으로 로그인해 주세요.` 안내 문구 1개를 추가하고, 그 외 보안 helper와 `다른 계정으로 로그인` action은 Frontend 인증 UI에서 제거했다.
- Figma metadata의 panel/title/logo/button/checkbox frame 좌표와 크기를 1440×810 browser에서 고정했다.
- Ellipse 68을 Figma의 실제 pattern fill 원형 위치·크기·불투명도로 이동했다.
- red/white panel이 PC viewport 전체를 채우고 각 panel의 reference content만 등비 축소·확대해 디자인 밖 letterbox 없이 모든 요소와 비율을 유지한다.
- Figma 원본 ellipse 66/67 SVG, logo asset, 10% white overlay, radius와 shadow를 사용했다.
- login base는 Figma frame fill `#DA2127`로 고정하고 white glass shape와 authentication shape의 stacking을 그대로 반영했다.
- MSAL 초기화·interactive login·cached token·`/api/me` 확인 중에는 기본 로그인과 같은 logo/title/Microsoft logo/안내/배경 canvas를 유지한다.
- Loading 중 `LOGIN`과 checkbox는 렌더링하지 않고 이전 두 control 영역 중앙에 `#DA2127` 원형 indicator 1개를 800ms linear infinite로 회전시킨다.
- 재인증·오류·설정 누락·접근 제한 상태는 기존 복구 정보와 fail-safe shell을 유지한다.

다음 불변조건을 유지했다.

- `LOGIN`은 기존 `loginRequest`를 사용한다.
- 로그인 상태 유지 preference와 MSAL cache 재생성 경로를 변경하지 않는다.
- silent token 확인, interaction-required 재인증, 조건부 액세스·MFA 의미를 변경하지 않는다.
- 다른 계정 선택은 Microsoft 로그인 화면의 `다른 계정 사용` provider UX를 사용하며 Frontend의 중복 action과 사용되지 않는 전용 request는 제거했다.
- Backend, API, DB, migration, dependency, lockfile와 runtime configuration을 변경하지 않는다.
- 기존 history P2와 Finding gate의 상태·Task 파일을 변경하지 않는다.

## 3. Figma source와 확인 결과

- File/node: `2zikjJ8SpFXm50dH3PE4Cy` / `1:175` (`로그인 페이지 최종`)
- 기준 frame: 1440×810
- 재확인: design context, metadata, variables, screenshot, asset URL과 Code Connect
- Variable definitions: 0
- Figma element: EMI logo, title, Microsoft logo, `LOGIN`, unchecked 로그인 상태 유지 checkbox와 배경 장식
- Ellipse 68 Plugin API: `x=-538.5`, `y=-468`, `876×876`, visible pattern paint, opacity `0.33`, rectangular tile, scaling factor `0.75`
- Frame fill: solid `#DA2127`, opacity 1
- Background glass shape: `x=-6`, `1446×810`, white opacity `0.1`, glass radius `23.25`
- White authentication shape: `x=776`, `664.5×810`, left radius `51`, drop shadow `-5.25/-1.5/43.05`, black opacity `0.28`
- Code Connect: Organization/Enterprise plan의 Dev 또는 Full seat가 없어 조회 불가. Repository에도 기존 `*.figma.*` mapping이 없다.
- Assets: Figma 제공 EMI/Microsoft logo와 ellipse 66/67 SVG를 Frontend asset으로 포함했다. Ellipse 68 asset API는 pattern fill이 빠진 빈 circle을 반환하므로 CSS pattern layer로 표현했다.
- Mobile: 별도 frame이 없고 Change 001에서 명시적으로 제외했다.

Code Connect 제한은 시각 계약 확인을 막지 않았다. design context, metadata, screenshot과 원본 asset을 함께 대조했으므로 구현 blocker나 제품 Finding으로 분류하지 않았다.

Change 007에서 Figma Plugin API로 instance `1:187`가 component set `1:160`의 `속성 1=베리언트2`임을 확인했다. 기본 `1:161`은 white·`#737373`·icon 0, Variant 2 `1:166`은 `#DA2127`·white Done icon·`#282828`이며 최종 화면 scale의 icon geometry는 `13.5×13.5`다. Change 008 사용자 검수 지시에 따라 icon asset·크기는 유지하고 CSS background positioning area의 `50% 50%`에 중앙 정렬한다.

## 4. 실제 구현

### Desktop 로그인 exact geometry

1440×810 browser에서 다음 frame을 Figma metadata와 동일하게 고정했다.

| Element | x | y | width | height |
| --- | ---: | ---: | ---: | ---: |
| White panel | 776 | 0 | 664.5 | 810 |
| Title frame | 885 | 208.5 | 447 | 28.5 |
| EMI logo | 219.75 | 372 | 329.563 | 46.656 |
| Microsoft logo | 1050 | 306.75 | 116.469 | 148.219 |
| `LOGIN` button | 987 | 510 | 243 | 48.75 |
| Checkbox group | 1045 | 567 | 126 | 32 |
| 승인 안내 문구 | 885 | 472 | 447 | 18 |
| Ellipse 68 pattern | -538.5 | -468 | 876 | 876 |

제목의 보이는 글자 폭 보정은 내부 span에만 적용해 title frame 자체의 bounding box를 바꾸지 않는다.

### PC flexible panel 반응형

- 로그인 root와 red/white flexible grid가 viewport 전체를 채운다.
- Figma divider `776/1440`을 기준으로 brand panel `53.8888889%`, authentication panel `46.1111111%`를 유지한다.
- 각 panel surface는 viewport 전체 높이까지 연장되므로 16:9가 아닌 창에도 디자인 밖 letterbox가 없다.
- panel 내부의 brand `776×810`, authentication `664×810` reference content는 `min(viewportWidth / 1440, viewportHeight / 810)` 배율로 가운데 배치한다.
- resize listener가 창 크기 변경 때 scale, panel radius와 shadow를 다시 계산한다.
- 내부 element는 reference pixel 좌표를 유지하므로 개별 재배치나 왜곡 없이 모두 보인다.

### 인증 공통 shell

- 로그인·loading·재인증·오류·설정 누락·접근 제한 상태를 고정 state로 구분한다.
- 기본 로그인과 loading은 `data-auth-layout=login`의 동일 exact canvas를 사용한다.
- loading에는 `aria-busy=true`, `role=status`, `aria-label=로그인 확인 중`을 적용하고 `LOGIN`·checkbox 대신 빨간 회전 indicator 1개를 표시한다.
- 설정 누락은 Microsoft redirect가 불가능한 경우를 위한 기존 fail-safe 안내로 보존하고 새 Figma variant를 만들지 않는다.
- 로그인 화면에서는 Change 004로 승인된 안내 message 1개만 전달하고 helper·secondary action은 렌더링하지 않는다.

### Mobile 제외

- Auth 전용 mobile CSS를 제거했다.
- Task 전용 Playwright 구성에서 390px project를 제거했다.
- 다른 제품 화면에 존재하는 기존 mobile style은 변경하지 않았다.

## 5. 수정한 파일

| 파일 | 변경 |
| --- | --- |
| `frontend/src/App.tsx` | 인증 공통 shell, Figma-only login content, Loading control 제거·상태 indicator, 다른 계정 UI 제거 |
| `frontend/src/auth.ts` | 사용되지 않는 Frontend 전용 `accountSwitchLoginRequest` 제거, 기본 prompt 없는 login request 유지 |
| `frontend/src/main.tsx` | MSAL 초기화 중 공통 loading shell 표시 |
| `frontend/src/styles.css` | Figma Desktop exact geometry, Ellipse 68 pattern 위치, 등비 canvas, 공통 auth style |
| `frontend/src/assets/emi-logo.png` | Figma EMI logo asset |
| `frontend/src/assets/microsoft-logo.png` | Figma Microsoft logo asset |
| `frontend/src/assets/auth-ellipse-66.svg` | Figma background ellipse asset |
| `frontend/src/assets/auth-ellipse-67.svg` | Figma background ellipse asset |
| `frontend/tests/auth.test.tsx` | Figma-only login content와 인증 계약 회귀 test |
| `frontend/playwright.auth-shell.config.ts` | task 전용 1920/1440/1280/1024와 short/narrow PC window browser matrix |
| `frontend/e2e/auth-shell/auth-shell.spec.ts` | 기본 로그인·loading privacy-safe 구조·normalized geometry·resize·overflow·console/request projection |
| `frontend/e2e/auth-shell/loading.html` | loading 화면 전용 isolated browser fixture |
| `frontend/e2e/auth-shell/loading.tsx` | `AuthInitializationScreen`을 실제 CSS와 함께 렌더링하는 browser entry |
| `tasks/design-login-001.md` | 승인 계약, allowlist와 사용자 검수 checklist |
| `tasks/design-login-001-change-001.md` | Desktop-only 수정 계약 |
| `tasks/design-login-001-change-002.md` | Ellipse 68와 PC 등비 반응형 수정 계약 |
| `tasks/design-login-001-change-003.md` | viewport 전체를 채우는 PC flexible panel 수정 계약 |
| `tasks/design-login-001-change-004.md` | 안내 문구와 Figma 배경·shape 연결부 수정 계약 |
| `tasks/design-login-001-change-005.md` | Microsoft provider 계정 선택 경계, loading 동일 geometry와 화면 단위 승격 보류 계약 |
| `tasks/design-login-001-change-006.md` | Loading control 제거·회전 indicator와 승격 SOP 계약 |
| `tasks/design-login-001-change-007.md` | 로그인 상태 유지 Variant 2와 인증 action 도달 가능성 audit 계약 |
| `tasks/design-login-001-change-008.md` | Variant 2 Done icon만 checkbox 중앙에 배치하는 보정 계약 |
| `tasks/design-login-001-change-009.md` | 최신 main 화면 단위 승격·bounded worktree·fixed allowlist 계약 |
| `tasks/design-login-001-implementation-report.md` | 구현·검증·Finding·rollback 원장 |
| `docs/00-product-roadmap.md` | 승인된 범위 변경, Task 상태와 추적·Decision Log |
| `docs/development/design-screen-promotion.md` | 5176 구현·검수와 최신 main 화면 단위 승격 운영 기준 |

## 6. 실행한 검증과 결과

| 검증 | 결과 |
| --- | --- |
| Figma context/metadata/variables/screenshot/assets 재조회 | PASS |
| Figma Code Connect 조회 | 제한 확인 — Dev/Full seat 필요, 비차단 |
| Frontend lint | PASS, error 0 / 기존 Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend unit 전체 | PASS, 66/66 |
| Frontend production build | PASS, 기존 chunk-size warning 유지 |
| Auth shell browser — 기본 로그인·loading × PC 1920×1080, 1440×810, 1280×720, 1024×768, 1440×600, 651×708 | PASS, 12/12 |
| Live resize — 1440×810 → 1024×768 | PASS |
| Mock UI smoke | PASS, 1/1 |
| Figma screenshot pixel 비교 | PASS, 수치 기록 완료 |
| Backend/API/DB/migration/dependency diff | 0 |

Unit 실행 중 redirect 계약 test가 jsdom의 cross-document navigation 미구현 메시지를 출력했지만 assertion과 test 66개는 모두 통과했다. 이는 기존 test environment 제한이며 신규 실패로 분류하지 않았다.

## 7. Privacy-safe browser 증빙

실제 사용자·회사·tenant·token을 사용하지 않고 synthetic Entra 설정과 isolated Frontend server를 사용했다. Browser output은 다음 fixed projection만 남겼다.

| Viewport | content scale | panel coverage | inner canvases | unexpected content | overflow H/V | console/request |
| --- | ---: | --- | --- | ---: | --- | --- |
| 1920×1080 | 1.333333 | 100% | fully visible | 0 | 0/0 | 0/0 |
| 1440×810 | 1 | 100% | fully visible | 0 | 0/0 | 0/0 |
| 1280×720 | 0.888889 | 100% | fully visible | 0 | 0/0 | 0/0 |
| 1024×768 | 0.711111 | 100% | fully visible | 0 | 0/0 | 0/0 |
| 1440×600 | 0.740741 | 100% | fully visible | 0 | 0/0 | 0/0 |
| 651×708 | 0.452083 | 100% | fully visible | 0 | 0/0 | 0/0 |

여섯 viewport의 기본 로그인과 Loading, 총 12개 browser case를 다시 검증했다. 각 기본 로그인 case는 저장값을 지운 뒤 미선택 기본 variant의 white·`#737373`·icon 0을 확인하고 checkbox를 클릭해 Variant 2의 `#DA2127` fill·red border·white Done icon·`13.5×13.5`·`50% 50%` 중앙 정렬·`#282828` text·preference `true`를 확인했다. Loading은 button 0·checkbox 0·indicator 1, indicator red/animation과 `307.625/530/48.75×48.75` reference geometry, `aria-busy=true`를 모두 만족했다. 두 상태 모두 공통 title/logo/안내/배경 canvas, Figma connection contract, panel coverage 100%, overflow 0과 console/request failure 0을 유지했다.

동일 viewport·unchecked state의 Figma screenshot과 Chromium screenshot을 pixel-level로 비교한 결과는 다음과 같다.

- Mean absolute error: `1.2497`
- 완전 동일 pixel: `69.3100%`
- RGB channel당 차이 2 이하: `84.2046%`
- RGB channel당 차이 8 이하: `97.7912%`
- Figma에 없는 승인 안내 영역 제외: MAE `1.1303`, RGB channel당 차이 8 이하 `97.9200%`

Figma export와 Chromium은 text/vector anti-aliasing과 image resampling engine이 달라 raw bitmap의 100% 동일성을 보장할 수 없다. 이 Task는 표시 element, frame geometry와 스타일 계약의 exact match를 자동 검증하며, raw pixel 수치는 과장 없이 별도 기록한다.

Screenshot과 비교 artifact는 `/tmp`에서만 사용했고 tracked/staged 산출물에 포함하지 않았다. 기존 Development·Review-safe runtime에는 browser navigation이나 source 교체를 수행하지 않았다.

## 8. 시도 후 보정한 접근

- 첫 구현의 390px 해석과 다른 계정 secondary action은 Change 001에서 승인 범위 밖으로 변경되어 제거했다.
- CSS 근사 ellipse를 Figma 원본 SVG로 교체했다.
- 제목 글자 폭 보정이 title frame bounding box까지 확장하는 문제를 browser test가 발견했다. transform을 내부 span으로 이동해 보이는 결과와 exact frame을 함께 만족시켰다.
- Figma background overlay와 Chromium 합성 결과를 비교해 Figma screenshot의 실제 red surface와 일치하도록 browser render 값을 보정했다.
- Figma의 Ellipse 68 SVG가 빈 circle로 내려오는 원인을 Plugin API로 확인했다. 실제 node의 pattern paint geometry와 opacity를 CSS circle layer로 옮겨 사각형 dot mask를 제거했다.
- 첫 PC scale 구현은 기존 auth 54:46 grid column과 transform 기준 때문에 canvas가 왼쪽으로 밀렸다. 로그인 viewport를 단일 grid로 분리하고 canvas를 absolute center 기준으로 바꿔 모든 viewport에서 중앙 정렬을 고정했다.
- 첫 responsive browser matrix 실패는 제품 오류가 아니라 projection을 소수점 3자리로 자른 뒤 5자리 정밀도로 비교한 test 오류였다. projection 정밀도를 6자리로 보정한 뒤 3/3 통과했다.
- Change 002의 전체 canvas contain 방식은 16:9가 아닌 창에 root의 빨간 letterbox를 노출했다. Change 003에서 viewport를 채우는 red/white flexible panel과 panel-relative reference content로 분리해 제거했다.
- Change 003 첫 browser run의 5건 실패는 제품 오류가 아니라 CSS Grid 서브픽셀 분배 약 0.01px를 0.005px보다 엄격하게 비교한 test 오차였다. 실제 fixed projection은 모든 viewport에서 panel coverage 100%, normalized geometry 일치, overflow 0이었고 tolerance를 0.05px로 보정한 뒤 6/6 통과했다.
- Change 004에서 Figma Plugin API로 frame fill `#DA2127`, glass shape와 white authentication shape의 stacking을 재확인했다. 우측 grid 뒤의 white surface를 red frame으로 교체해 rounded corner 밖과 shadow 뒤에 red가 이어지도록 수정했다.
- 안내 문구 첫 browser run은 공통 `max-width: 360px`이 447px reference width를 제한해 6건 실패했다. 안내 전용 max-width를 해제한 뒤 text·geometry와 전체 background/connection contract가 6/6 통과했다.
- Change 005 첫 unit 재실행은 기본 로그인 test가 loading과 동일해진 title을 즉시 찾아 state 전환 전에 assertion해 1건 실패했다. 최종 `data-auth-state=login`을 기다리도록 timing assertion을 보정한 뒤 전체 66/66이 통과했다.
- Change 006 첫 unit 재실행은 interactive-login loading test 1곳이 Change 005의 disabled control 계약을 계속 기대해 1건 실패했다. 새 승인 계약에 맞춰 button·checkbox 부재와 status indicator를 검증하도록 보정한다.
- Change 006 첫 browser 재실행은 회전 중인 indicator 자체의 bounding box가 회전 각도에 따라 달라져 4건 실패했다. 고정 48.75×48.75 status wrapper 안의 pseudo-element만 회전하도록 분리해 시각 회전과 responsive geometry를 함께 고정했다.

## 9. 미실행 검증과 사용자 Gate

- 실제 Entra tenant 로그인: 실제 계정·tenant 접근과 runtime mutation이 승인 범위 밖이므로 미실행
- Persistent UAT: 접근·write·runtime 교체가 승인 범위 밖이므로 미실행
- Backend·Full-Stack E2E: Backend/API/DB 계약 변경이 없는 Frontend-only auth shell이므로 미실행
- Mobile/390px: Change 001에서 명시적으로 제외
- 실제 Desktop 사용자 검수: 전체 체크리스트 완료
- 분리된 Codex read-only 검증: Change 008·Change 009 PASS. Change 009는 allowlist 26/26, source hash 12/12, Roadmap baseline, 인증 불변조건, runtime·privacy·Finding gate를 재검증해 현재 P0/P1/P2/P3 `0/0/0/0`, 해결된 P2 1로 판정

## 10. Finding과 제한

- P0: 0
- P1: 0
- 신규 P2: 0
- P3: 0
- 해결된 P2: promotion 문서의 local home-directory 절대 경로 3회를 독립 검증에서 발견해 stage 전 symbolic worktree alias로 치환하고 privacy scan을 재통과했다.
- 외부 제한: Figma Code Connect 조회는 seat 제한으로 불가했지만 design context·metadata·screenshot·assets로 계약을 확인해 구현을 차단하지 않는다.
- 사용자 검수 listener: `http://127.0.0.1:5176/` ACTIVE. task-owned screen, bounded worktree cwd, strict port, synthetic Entra 설정과 HTTP 200을 재확인했다.
- 기존 history P2: 범위 밖이며 상태를 변경하지 않았다.

Change 009 자동·독립 검증을 통과했고 2026-07-15 Git 게시·5174 Frontend 반영·merge가 승인되어 게시 절차를 실행한다.

## 11. 승인 계약 대비 차이

- 최초 승인 대비 승인된 Change 001: Mobile/390px 제거, Figma 비포함 login content와 다른 계정 action 숨김
- Change 002: Ellipse 68 pattern 위치 보정과 PC viewport 전체 canvas 등비 축소·확대
- Change 003: 전체 canvas letterbox를 폐기하고 red/white panel이 viewport를 채우는 flexible PC layout으로 변경
- Change 004: 안내 문구 1개를 명시 승인 예외로 추가하고 Figma `#DA2127` base·glass·white shape 연결부를 정확히 반영
- Change 005: 화면 단위 승격 방식은 확정하되 현재 로그인 화면 승격은 보류. 다른 계정 선택은 Microsoft provider UI에 위임하고 Frontend 중복 action·전용 request 제거. Loading은 기본 로그인과 동일 geometry에서 안내만 변경
- Change 006: Loading의 `LOGIN`·checkbox를 제거하고 그 영역에 빨간 회전 indicator 1개를 표시. 화면 단위 승격 절차를 별도 canonical SOP로 문서화
- Change 007: Figma 로그인 상태 유지 Variant 2의 red fill·white Done icon·dark text와 클릭 preference 전환 구현. Frontend 전용 account switch 완전 삭제와 남은 인증 action 도달 가능성 audit
- Change 008: Done icon asset·크기와 checkbox의 나머지 style·기능은 유지하고 background position만 수평·수직 중앙으로 보정
- Change 009: 최신 main clean promotion branch/worktree에 fixed allowlist만 선택 이식하고, 5176을 재사용하지 않는 격리 auth browser 설정으로 전체 Frontend 검증
- 인증 정책·Backend·API·DB·migration·runtime configuration 변경: 0
- dependency·lockfile 변경: 0
- 승인 밖 Git mutation: 0

## 12. 운영/SOP

- 화면 단위 디자인 구현·main 동기화·승격 Gate는 [디자인 화면 단위 승격 운영 기준](../docs/development/design-screen-promotion.md)을 따른다.
- 이 변경은 배포·runtime handover를 수행하지 않는다.
- 게시 승인을 받은 뒤에도 기존 Frontend 배포 절차만 사용하며 Backend·DB 단계는 없다.
- 이상 발생 시 이 Task의 Frontend source·asset·test 변경만 되돌린다. 인증 request/cache 정책이나 runtime configuration은 rollback 대상이 아니다.
- 현재 bounded worktree는 dirty하고 commit이 없으므로 제거하지 않는다. Commit reachable, clean, process 미사용과 open PR 없음이 확인된 뒤 별도 cleanup 승인을 따른다.

## 13. 사용자 안내

- 로그인 화면에는 Figma와 동일하게 `LOGIN`과 로그인 상태 유지 checkbox만 표시된다.
- `LOGIN`은 기존 Microsoft 365 로그인 동작을 실행한다.
- 다른 계정 선택은 Microsoft 로그인 화면의 `다른 계정 사용`을 이용하며 우리 화면에는 중복 action이 없다.
- `회사 Microsoft 365 계정으로 로그인해 주세요.` 안내가 Microsoft logo와 `LOGIN` 사이에 표시된다.
- 로그인 상태 유지 checkbox는 기존과 같은 의미로 동작한다.
- 로그인 상태 유지 checkbox는 미선택 시 기본 variant, 선택 시 Figma Variant 2의 빨간 checkbox·흰 Done icon·진한 문구로 바뀐다.
- 초기화·토큰·사용자 정보 확인 중에는 `Microsoft 365 로그인 정보를 확인하고 있습니다.` 안내와 빨간 회전 indicator가 표시되며 `LOGIN`과 checkbox는 보이지 않는다.
- Mobile 화면은 이번 Change의 검수 대상이 아니다.
- 작은 PC 창에서는 red/white panel이 화면 전체를 채우고, 내부 로그인 구성요소만 같은 비율로 축소되어 모두 보인다.

## 14. Rollback

1. 이 Task의 Frontend source, 네 asset, auth unit/browser test와 문서 diff만 식별한다.
2. 인증 request/cache 정책 파일, Backend, DB와 runtime configuration이 변경되지 않았음을 다시 확인한다.
3. 승인된 Git 절차로 Task diff를 revert한다.
4. Frontend lint, typecheck, unit, build와 auth browser PC 1920×1080/1440×810/1280×720/1024×768/1440×600/651×708을 재실행한다.
5. 기존 Development·Review-safe runtime은 별도 handover 승인 없이 재시작하지 않는다.

## 15. 종료 산출물 5종 추적

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | `docs/development/design-screen-promotion.md`와 이 문서 12장 | 작성 완료 / runtime 적용 없음 |
| User manual | 이 문서 13장 | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | Change 009 최신 main 선택 이식·자동·독립 검증 완료 / 게시·5174 반영 승인·실행 상태 반영 |
| Validation checklist | `tasks/design-login-001.md` 8장 | 사용자 전체 확인·Change 009 이식·자동·독립 검증 완료 / 게시·5174 반영 승인 |

Change 008 구현·자동·독립 검증과 사용자 검수, Change 009 이식·자동·독립 검증은 완료됐다. Git 게시·5174 Frontend 반영·merge는 2026-07-15 승인되어 실행 중이다.
