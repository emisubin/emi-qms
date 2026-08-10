# TASK-TEAMS-PWA-001 사용자 검수 체크리스트

상태: `Change 001~003·007·009·010 원격 main 병합·Azure 운영 rollout 완료 / Android 최종 사용자 검수 완료 / 기타 운영 표면 검수 대기`

- 검수 대상: EMI PMS 일반 사용자, System Administrator
- 운영 적용: Change 007·009·010 Git 게시와 Azure 운영 rollout 완료. Change 010은 PR #88, 원격 `main` merge SHA `5300b4646b2ea8bba0a43e953fea58e66caa2016`, Azure release `31366150022`로 운영에 반영됐다. Teams package/catalog는 변경하지 않는다.
- 개인정보 안전: 실제 계정 식별자·token·알림 본문을 이 문서에 기록하지 않는다. 결과는 역할, 날짜, 환경과 PASS/FAIL만 기록한다.

## 자동 검증 완료

- [x] Teams manifest의 short/full/tab 이름은 `EMI PMS`, developer name은 `EMI`다.
- [x] 기존 Teams Activity type 10개, RSC `TeamsActivity.Send.User`, `webApplicationInfo` identity와 권한이 유지된다.
- [x] Teams tab은 `/teams-launcher.html`만 열고 React 업무 bundle·PWA manifest·Service Worker를 불러오지 않는다.
- [x] launcher는 같은 origin의 홈 또는 검증된 알림 ID 상세 경로만 새 창으로 연다.
- [x] Easy Auth 익명 예외는 launcher HTML·작은 JavaScript·192px 브랜드 icon과 기존 health에 한정된다.
- [x] root app shell·업무 bundle·PWA manifest·API의 기존 사전 인증 계약은 변경하지 않는다.
- [x] Android·Chromium 설치 prompt는 `beforeinstallprompt` 수신 후에도 사용자 버튼 클릭 전에는 실행되지 않는다.
- [x] 모바일 자동 안내는 Microsoft 인증·access-token gate와 업무 shell 준비 전에는 열리지 않고 준비 뒤에만 열린다.
- [x] Android 안내는 설치 event를 기다리지 않고 먼저 열리며, 설치 버튼은 준비 전 비활성·event 수신 뒤 활성으로 바뀐다.
- [x] iPhone Safari는 `공유 → 홈 화면에 추가 → 웹 앱으로 열기 → 추가` 절차를 표시한다.
- [x] iPhone 타 브라우저는 현재 브라우저 설치 메뉴를 먼저 안내하고, 없을 때 root `PMS 주소 복사 → Safari 붙여넣기` 복구 절차를 표시한다.
- [x] 안내를 닫으면 현재 탭 session에서는 반복되지 않고 새 browser session에서는 미설치 상태라면 다시 안내하며 계정/로그인 화면에서 수동으로 다시 열 수 있다.
- [x] standalone 또는 Teams embedded 표면에서는 PWA 설치 안내를 숨긴다.
- [x] 브라우저·앱·Teams·메일·Teams 대체 제목·PDF metadata·휴일 Excel 머리글의 사용자 표시명이 `EMI PMS` 계약을 따른다.
- [x] PWA와 Teams color icon은 흰 바탕 빨간 EMI 로고이며 Teams outline icon은 플랫폼 규격을 유지한다.
- [x] Teams·iPhone·Android 안내는 흰 표면·검정 버튼·중성 회색·1px 경계를 사용하고 장식용 왼쪽 강조선·색상 그림자가 없다.
- [x] launcher desktop 1440px와 mobile 390px, iPhone·Android 설치 안내 390px에서 가로 overflow가 없다.
- [x] Change 009 Frontend 전체 unit `187/187`, PWA 집중 `10/10`, browser 집중 `4/4`, lint·typecheck·production build가 통과한다.
- [x] Change 009 PR #86과 merge SHA main CI의 Frontend·Backend·Full-Stack이 각각 `3/3` 통과했다.
- [x] Azure release `31361630803`이 exact main SHA 검증·Backend/Frontend image 게시·migration gate·운영 revision 교체를 통과했다.
- [x] 배포 뒤 health `200`, 익명 root·`/api/me` `401/401`로 서비스 상태와 Microsoft 365 사전 인증 경계를 확인했다.
- [x] DB·migration·알림 발송 정책·실제 provider 데이터는 변경하지 않았다.
- [x] Change 010에서 Easy Auth 보호 manifest link에 `crossorigin="use-credentials"`를 추가하고 PWA asset contract·집중 unit `10/10`·전체 unit `187/187`·lint·typecheck·production build를 통과했다.

## Change 007 자동 검증

- [x] 로그인 화면은 지정 4x 가로 logo의 natural size `4265×604`를 유지한다.
- [x] 로그인 뒤 desktop sidebar, 390px mobile app bar와 menu drawer는 지정 4x 내부 logo의 natural size `3796×1378`을 사용한다.
- [x] 내부 logo만 원본 색상, `filter: none`, 투명 배경이며 나머지 화면은 기존 흑백 wireframe을 유지한다.
- [x] desktop·390px에서 logo가 잘리지 않고 page-level horizontal overflow가 없다.
- [x] Frontend lint·typecheck·unit·build와 auth shell·집중 mock UI가 통과했고, PR #84 CI와 merge SHA main CI의 Frontend·Backend·Full-Stack이 최종 `3/3` 통과했다.
- [x] Backend·DB·migration·dependency·Teams manifest·PWA icon 변경이 없다.
- [x] Azure release `31354814082`가 migration·Backend·Frontend·public security를 통과했고 health `200`, 익명 root·API `401/401`, Microsoft 365 선인증 redirect를 확인했다.

## 사용자 검수

### 1. PC 웹·설치 앱

- [ ] 보호된 운영 주소를 열었을 때 인증 전에는 핵심 앱 화면이나 JavaScript 파일이 바로 내려오지 않는지 확인한다.
- [ ] Microsoft 365 로그인 뒤 브라우저 탭·로그인 화면·상단 이름이 모두 `EMI PMS`인지 확인한다.
- [ ] 로그인 화면에는 지정 4x 가로 logo, 로그인 뒤 모든 화면의 왼쪽 공통 메뉴에는 지정 4x 내부 logo가 원본 색상으로 표시되는지 확인한다.
- [ ] Chrome 또는 Edge에서 로그인 화면/계정 영역의 `EMI PMS 설치` 안내를 열 수 있는지 확인한다.
- [ ] 브라우저가 설치를 지원하는 경우 설치 버튼 한 번으로 설치 확인창이 열리는지 확인한다.
- [ ] 설치 후 시작 메뉴·작업 표시줄 이름과 아이콘이 `EMI PMS`, 흰 바탕 빨간 EMI 로고인지 확인한다.
- [ ] 설치 앱에서 기존 Microsoft 365 로그인, 권한과 주요 업무 화면이 웹과 동일한지 확인한다.

### 2. Android

- [x] `Android 설치 안내`가 제품 공통 흑백 wireframe으로 표시되고 빨간색은 EMI logo에만 사용되는지 확인한다.
- [x] Android Chrome에서 Microsoft 365 로그인을 마친 직후 홈 업무보다 먼저 닫을 수 있는 설치 안내가 표시되는지 확인한다.
- [x] Chrome이 설치 기능을 준비 중이면 설치 버튼이 회색 비활성으로 보이고 준비가 끝나면 같은 버튼이 활성화되는지 확인한다.
- [x] Change 010 운영 반영 뒤 새 Chrome 탭에서 Microsoft 인증을 완료하고 `EMI PMS 설치` 버튼이 활성화되는지 확인한다.
- [x] `EMI PMS 설치`를 누르면 브라우저 설치 확인창이 열리는지 확인한다.
- [x] 설치를 취소해도 로그인 화면 또는 계정 영역에서 안내를 다시 열 수 있는지 확인한다.
- [x] 설치 후 홈 화면 이름·아이콘과 standalone 실행이 정상인지 확인한다.
- [x] 제조·품질 주요 화면이 390px 안에서 가로로 잘리지 않는지 확인한다.
- [x] 로그인 뒤 상단 app bar와 전체 업무 menu의 logo가 모두 지정 4x 내부 logo 원본 색상으로 보이는지 확인한다.

### 3. iPhone

- [ ] `iPhone 설치 안내`가 제품 공통 흑백 wireframe으로 표시되고 번호·문장이 한눈에 읽히는지 확인한다.
- [ ] iPhone Safari에서 Microsoft 365 로그인을 마친 직후 홈 업무보다 먼저 `공유 → 홈 화면에 추가 → 웹 앱으로 열기 → 추가` 안내가 표시되는지 확인한다.
- [ ] iPhone Chrome·Edge 등에서 현재 브라우저의 `홈 화면에 추가` 확인 안내가 먼저 표시되는지 확인한다.
- [ ] 타 브라우저에 해당 메뉴가 없을 때 `PMS 주소 복사`가 root 주소만 복사하고 Safari 붙여넣기 절차를 안내하는지 확인한다.
- [ ] Clipboard 권한이 거절되면 현재 주소창을 길게 눌러 복사하라는 복구 문구가 표시되는지 확인한다.
- [ ] 안내를 닫고 같은 탭에서 새로고침했을 때 반복되지 않고, 탭을 완전히 닫은 뒤 새 session으로 미설치 접속하면 다시 표시되는지 확인한다.
- [ ] 안내 절차대로 홈 화면에 추가한 뒤 이름·아이콘과 standalone 실행이 정상인지 확인한다.
- [ ] 설치 앱에서 Microsoft 365 로그인과 제조·품질 주요 화면을 사용할 수 있는지 확인한다.
- [ ] 지정 4x 로그인 logo와 로그인 뒤 상단·전체 업무 menu의 지정 4x 내부 logo가 구분되어 표시되는지 확인한다.

### 4. Teams 앱·Activity Feed

- [ ] Teams 실행 화면에 넓은 빨간 면·왼쪽 강조선·색상 그림자가 없고 흑백 wireframe으로 표시되는지 확인한다.
- [ ] 새 Teams package와 Azure launcher 예외가 적용된 뒤 개인 tab에 `EMI PMS` 실행 화면이 표시되는지 확인한다.
- [ ] `EMI PMS 열기`를 누르면 Teams iframe 안에 업무 화면을 억지로 띄우지 않고 보호된 새 창이 열리는지 확인한다.
- [ ] 미로그인 상태에서는 새 창에서 Microsoft 365 인증을 요구하고 로그인 뒤 본인 권한만 보이는지 확인한다.
- [ ] Teams Activity Feed 알림을 눌렀을 때 launcher를 거쳐 선택한 알림 상세 위치가 보존되는지 확인한다.
- [ ] 기존 10종 Activity 알림의 수신자와 발송 시점이 이전과 동일한지 확인한다.

### 5. 이름이 표시되는 출력물

- [ ] 실제 운영 메일의 발신자 표시명과 본문 머리글이 `EMI PMS 알림`인지 확인한다.
- [ ] 새 IQC·품질검사 PDF의 문서 정보 Author가 `EMI PMS`인지 확인한다.
- [ ] 새 휴일 일괄 등록 Excel의 첫 머리글이 `EMI PMS 휴일 일괄 등록`인지 확인한다.

## 결과 기록

| 날짜 | 환경 | 검수 역할 | 결과 | 증빙 유형 | 비고 |
| --- | --- | --- | --- | --- | --- |
| 대기 | Azure 운영 / PC·iPhone·Teams·출력물 | 역할명만 기록 | 대기 | 화면·실제 출력 확인 | 실제 계정·token·알림 원문 기록 금지 |
| 2026-08-10 | Azure 운영 / iPhone·Android | 사용자 | FAIL → Change 009 보정 | 실제 기기 화면 확인 | Microsoft 로그인 뒤 설치 안내가 자동 표시되지 않음 |
| 2026-08-10 | Azure 운영 / 자동 공개 검증 | Codex | PASS | CI·release·HTTP 상태 | PR #86·main CI `3/3`, release `31361630803`, health `200`, 익명 `401/401` |
| 2026-08-10 | Azure 운영 / Android Chrome | 사용자 | PASS | 실제 기기 최종 검수 | PR #88·release `31366150022` 반영 후 설치 안내·버튼 활성·native 확인창·standalone 정상 |

## 실패 시 처리

- 화면·설치·deep link 결함은 `TASK-TEAMS-PWA-001`의 다음 change 또는 확인된 `BUGFIX`로 재개한다.
- 신규 Web Push·알림 권한·기기 구독 정책이 필요하면 이 Task를 확대하지 않고 별도 `NEW_FEATURE`로 기획한다.
- 실제 Azure·Teams 설정이 원인이면 application code를 임의로 우회하지 않고 운영 rollout 단계에서 설정을 되돌리거나 forward-fix한다.
