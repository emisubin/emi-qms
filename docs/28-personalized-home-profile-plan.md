# TASK-HOME-002 — 개인화 Home·프로필 셸 2차 기획 (구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-HOME-002`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/home-002-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/home-002-planning.md`
- codexReviewSource: `tasks/home-002-review.md`
- approvalChangeSource: `tasks/home-002-change-001.md`

이 문서는 experiment fast-track에서 Fable 1차 기획과 Codex 내용 review를 모두 읽고 review resolution을 반영한 최종 구현 source of truth다. 1차 기획과 review 원문은 판단 이력으로 보존하며 수정하지 않는다. 이 문서는 experiment local 구현·검증·commit까지만 대상으로 하고, 대표 repo·`main`·push·PR·merge(`0/3`)·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 따르고 여기에 복사하지 않는다.

## 1. 한 줄 목표

모든 active 업무 화면 오른쪽 위에서 로그인한 자신의 사진·부서·이름과 계정 메뉴(사진 변경·제거, 로그아웃)를 사용할 수 있고, Home 첫 화면에서 자신의 부서 핵심 지표와 다음 행동을 바로 확인할 수 있다.

## 2. 해결할 업무 문제와 대상 사용자

- 현재 desktop `topbar`는 자재 shortcut·검수 사용자 전환·개발 사용자 select·로그아웃이 점유하고, 실제 로그인 사용자의 사진·부서·이름은 어느 화면에도 표시되지 않는다. 모바일에서는 `상태` sheet 안에 사용자명이 텍스트로만 노출된다.
- 검수·개발 전환 control이 실제 로그인 사용자 정보와 같은 영역에서 경쟁해 actual 계정과 effective 검수 persona를 혼동할 여지가 있다.
- Desktop 왼쪽 sidebar는 viewport 높이를 완전히 사용하지 않고, 상단 자재 shortcut은 navigation 자재 메뉴와 중복이다.
- Home은 HOME-001 공통 4개 widget(내 업무·프로젝트 병목·Pending·알림)만 제공해 부서별 첫 판단을 지원하지 못한다.

대상 사용자: active 로그인 사용자 전원, 승인 대기 Entra 사용자, System Administrator(검수 사용자 전환 포함), Development mode 사용자.

## 3. Identity 모델 — actual과 effective의 명시적 분리 (review F2 반영)

"로그인된 사용자"와 "검수 전환으로 현재 보고 있는 사용자"는 다른 개념이며 이 구분을 UI·API·테스트 전 계층에 고정한다.

| 표면 | 기준 identity | 근거 |
| --- | --- | --- |
| 셸 identity badge·account popover/sheet의 사진·부서·이름 | `actualUser` | 계정 표면은 실제 로그인 계정을 나타낸다 |
| profile photo `GET`/`PUT`/`DELETE` 대상 | `ActualUserId` claim, 없으면 `UserId` claim | 검수 전환·dev persona로 타인 사진 변경 불가 |
| Home 인사말·부서 지표 | `effectiveUser`의 부서·permission·project access scope | 업무 화면은 현재 권한 관점을 따른다 |
| 검수 전환 중 안내 | 기존 `test-user-banner` 유지 + account menu에 actual 계정 기준임을 명시, effective persona는 "현재 검수 화면"으로 보조 표기 | persona 혼동 방지 |

권한 불변조건: 사진 mutation의 대상 사용자는 서버가 claim에서만 결정하며 request body·query·route로 대상 user id를 받지 않는다. Home 지표는 서버가 effective 부서·permission으로 필터링하고 범위 밖 count·제목·다음 행동을 노출하지 않는다.

## 4. 포함 범위 (review `유지`+`추가` resolution 반영)

1. 공통 셸 identity surface: desktop topbar 오른쪽과 mobile app bar에 actual 사용자의 avatar(사진/이니셜)·부서 표시명·이름.
2. Desktop account popover: avatar trigger, 큰 avatar(사진 변경 trigger), 이름·부서·(email 있을 때만), 사진 제거 보조 action, 로그아웃. 바깥 클릭·Escape 닫기와 trigger focus 복귀. 별도 modal로 확장하지 않는다.
3. Mobile account sheet: 기존 오른쪽 `상태` trigger를 avatar trigger로 교체하고 `MobileSheet` 재사용. 계정 card(사진·이름·부서), 사진 변경·제거, 알림 설정 진입, 로그아웃. API/DB 상태 정보는 계정 주목도를 해치지 않는 보조 영역으로 축소 유지한다.
4. 본인 profile photo lifecycle: 업로드·교체·제거 self-scope API, 서버 최종 검증, 실패·취소 시 기존 사진 보존, 이니셜 avatar fallback.
5. dev 사용자·검수 사용자 전환 select를 desktop sidebar 맨 아래와 mobile drawer footer의 구분된 검수 영역으로 이동. 노출 조건(`isDevMode`, `canUseAdminTestUserSwitch`)과 전환 동작은 변경하지 않는다.
6. Desktop sidebar viewport full-height: 위 brand~아래 검수 footer 고정, 내부 navigation scroll. mobile은 기존 drawer 구조 유지.
7. topbar 중복 자재 shortcut 제거. 왼쪽/모바일 navigation의 자재 메뉴가 canonical 진입점이다.
8. Home 상단 부서 지표 영역: effective 부서당 최대 3개 핵심 metric card와 allowlisted destination 이동. 기존 4개 widget 보존.
9. reference 기반 밝은 compact 셸·Home layout을 EMI red·white로 번역: 얇은 top header, full-height 밝은 sidebar, 옅은 브랜드색 pill active nav, 얇은 border·낮은 shadow·compact control. reference의 파란색은 복제하지 않는다.
10. 로그아웃은 기존 MSAL/dev 계약(`onLogout`)을 그대로 재사용한다.

## 5. 제외·보류 범위 (review `보류`+`제거` resolution 반영)

제외(이번 Task에서 구현하지 않음):

- 모든 업무 페이지의 정보 구조·기능 재설계. 전면 시각 수정 범위는 공통 셸과 Home layout이다.
- 승인 대기 인증 gate 재설계. 승인 대기 Entra 사용자는 기존 access 화면의 identity·로그아웃 계약을 유지하고 profile mutation은 서버에서 차단한다. "모든 페이지"는 active 업무 shell route를 뜻한다(review F6).
- Frontend의 부서별 목록 API 병렬 조합.
- 관리자 대리 사진 변경, 사용자 directory/gallery.
- Microsoft Graph 프로필 사진 동기화와 신규 Graph permission.
- 실제 provider 발송, 운영 storage service, Persistent UAT migration·runtime handover, 대표 repo·`main`·push·PR·merge.
- 기존 HOME-001 widget의 사용자 설정·예측·추천.

보류(후속 판단, 이번 완료 조건 아님):

- client-side crop/resize·EXIF 처리 — 현재 Repository에 공통 사진 압축/crop 구현이 확인되지 않아 v1은 `object-fit: cover` 표시와 서버 한도만 적용한다(review 판정 채택. 1차 기획의 선택 항목을 v1 범위에서 내린다).
- photo audit의 관리자 조회 UI.
- 운영 storage·retention·backup 결정(9장 사용자 결정 항목).

## 6. Profile photo 구현 계약

### 6.1 저장 모델 (`0042` additive migration)

- 신규 사용자 사진 테이블: 사용자당 현재 사진 1행. binary(bytea), MIME, byte size, content hash, opaque version, 갱신 시각·행위자. `qms_users(id)` 참조는 `on delete cascade`.
- 신규 append-only photo audit 테이블: action(Upload/Replace/Remove), content hash, byte size, MIME, actor user id, 시각만 기록한다. 사진 bytes·원본 파일명·email은 기록하지 않는다. 일반 사진 제거에서는 audit을 보존하고, 사용자 purge 시에는 개인정보 lifecycle에 따라 사용자와 함께 cascade 삭제한다(review F4).
- purge guard 계약(review F4): `AdminScheduledDeletionService`의 참조 탐색이 photo owner/actor column 때문에 purge를 영구히 보류하지 않도록, 두 photo 테이블은 self-owned cascade 예외로 설계하고 이를 테스트로 증명한다.
- `database/migrations/0042` additive 1건. 기존 migration 수정·번호 재사용 없음. fresh DB와 기존 isolated DB 모두 apply 검증. Persistent UAT 적용은 이 Task 범위가 아니다.

### 6.2 검증 (review F3 반영 — "완전 decode"를 주장하지 않는다)

- 신규 공통 profile image validator는 MIME declaration을 신뢰하지 않고 bytes로 JPEG/PNG signature와 최소 구조·종료 marker, 합리적 image dimension을 판정한다. 별도 decoder package를 추가하지 않는 한 구현·보고에서 "완전 pixel decode 검증"이라고 표현하지 않는다.
- 상한은 기존 검사 사진 규칙과 동일한 5MB, 허용 형식 JPEG/PNG. 유효하지 않은 파일은 저장 전 400 validation과 행동 가능한 한글 메시지로 거부한다.
- client는 형식·크기를 선검사해 빠른 feedback을 주되 서버 검증이 최종 기준이다.

### 6.3 API

- 본인 사진 조회 `GET`: 이미지 bytes 반환, 없으면 404 또는 204. content hash 기반 `ETag`와 private cache header 사용(review F7).
- 본인 사진 업로드/교체 `PUT`(또는 기존 convention에 맞는 mutation verb): 단일 transaction의 atomic upsert로 동시 요청은 마지막 승자만 남긴다.
- 본인 사진 제거 `DELETE`.
- 공통 정책: `AuthenticatedIdentity` + active 계정 검사. 승인 대기 Entra 사용자(v1)와 ReviewSafe 모드에서는 mutation을 서버에서 차단한다. 대상 사용자는 3장의 claim 규칙으로만 결정한다. 외부 provider 발송·알림 생성 없음.

### 6.4 `/api/me` additive projection (review F1 반영)

- 기존 `department`(code) 필드는 호환성을 위해 유지한다.
- top-level과 `actualUser`/`effectiveUser` principal projection 각각에 `departmentName`을 additive로 추가한다. `UserAuthorizationProfile.Department`에 이미 표시명이 있어 신규 조회 없이 projection만 확장한다.
- 사진 cache invalidation을 위해 bytes가 아닌 opaque `profilePhotoVersion`(content hash 기반)만 additive로 추가한다.
- 기존 소비자(test fixture 포함) 호환을 확인하고 함께 갱신한다.

## 7. Home 부서 지표 구현 계약 (review F5 반영)

- 신규 read-only aggregate endpoint 1개와 신규 `HomeMetricsStore`(이름은 기존 convention에 맞춤). Frontend가 부서별 목록 API를 병렬 조합하는 방식은 금지한다.
- 서버는 effective 사용자의 부서·permission·project access scope로 현재 부서 경로의 bounded query만 실행하고, 부서당 최대 3개 metric을 하나의 fixed contract(metric id, 한글 label, count, tone, `destinationKey`)로 반환한다. 기존 목록 payload를 재조합하지 않는다.
- 서버는 arbitrary URL을 반환하지 않는다. `destinationKey`는 allowlist이며 Frontend가 기존 `View` 전환으로 변환한다.
- 최소 부서 mapping (구현 시 기존 store·집계 정의를 재사용해 확정):
  - administration/System Administrator: 승인 대기 사용자, 발송 실패, active escalation — 업무 부서를 가장하지 않는다.
  - sales: 담당 active 프로젝트, 임박 납기, 정산 대기
  - design: 패널 정보 미완료 프로젝트, 패널 정보 미완료 panel, 설계 단계 진행 중
  - production-planning: 계획 미등록, 계획 중, 담당 미지정
  - procurement: 입고 예정 대기, 입고 지연, 입고 완료
  - materials: 도착 등록 대기, IQC 대기, 키팅 대기 panel
  - manufacturing: 제조 대기, 진행 중, 차단
  - quality: 검사 대기, 재검/조치 대기, 판정 완료
  - logistics: 포장 대기, 출발 대기, 배송 완료 대기
  - readonly/부서 없음/허용 metric 없음: 부서 지표 영역 비표시. 다른 부서 지표로 대체하지 않는다.
- 기존 HOME-001 4개 widget과 permission redaction은 재구현·변경하지 않는다. 지표 영역 실패는 영역 단위 error로 격리하고 navigation·기존 widget을 막지 않는다.

## 8. Frontend 구현 계약

- 수정 중심: `App.tsx`(topbar·`AppNavigation`·`AppMobileNavigation`·`MobileSheet` 구성), `HomePage.tsx`, `api.ts`, 공통 shell/Home CSS. 신규 component는 identity badge, account popover/sheet, 부서 지표 영역으로 제한하고 기존 flat 구조·naming을 따른다.
- 사진 업로드는 숨은 file input을 label/button으로 연결한다. 업로드·제거는 UX-001 A1 `useActionFeedback` 계약(중복 submit 차단, action 인접 loading/성공/오류·재시도, 오류 focus, `aria-live`)을 재사용하고 feedback을 popover/sheet 내부에 표시한다.
- ReviewSafe 모드에서는 업로드·제거 control을 disabled하고 이유를 표시하되 서버 차단을 최종 기준으로 유지한다(review F7).
- avatar bytes를 object URL로 표시하는 경우 effect cleanup에서 revoke하고, dev/검수 identity 전환 시 기존 `requestContextKey`·generation guard 패턴으로 늦은 사진·지표 응답을 폐기한다(review F7).
- 사진·지표는 각각 독립 loading/empty/error/success 상태로 관리한다(`WidgetState` 패턴 재사용). 사진 load 실패는 조용히 이니셜 avatar로 fallback한다. 이니셜 avatar에는 이름 대체 텍스트를 둔다.
- 접근성: popover는 `aria-expanded`·`aria-controls`, Escape·바깥 클릭 닫기, focus 복귀. sheet·drawer는 기존 focus trap 패턴을 재사용한다. 390px에서 page-level overflow 0, account sheet·drawer footer 44px touch target, reduced motion 시 전환 애니메이션 축소.
- 디자인: 기존 CSS variable·EMI red·white 브랜드 자산을 재사용하고, reference의 구성 원칙(얇은 header, full-height 밝은 sidebar, pill active, 낮은 shadow, 정돈된 grid/card)만 가져온다. 공식 제품명 표기를 유지한다.

## 9. 미결정·후속 사용자 결정 항목 (비차단)

| 번호 | 항목 | 상태 |
| ---: | --- | --- |
| 1 | 운영 승격 시 첨부·사진 storage·backup·restore·보존/purge 정책 (Roadmap 추적 항목 73) | 대기 — canonical 승격·Persistent UAT 전 사용자 확정 필요. 이번 experiment는 사용자당 1행·5MB 상한의 bounded DB row로 한정해 이 blocker와 충돌하지 않는다 |
| 2 | Microsoft Graph 프로필 사진 동기화 도입 여부 | 대기 — 별도 `NEW_FEATURE` planning 필요 |

두 항목 모두 이번 experiment 구현을 차단하지 않으며 fast-track standing rule의 권장안 자동 채택 범위 밖(운영·외부 연동 결정)이라 사용자 결정으로 남긴다.

## 10. 안전 경계

- Persistent UAT 영향 없음. isolated synthetic DB·runtime에서만 검증한다.
- migration은 `0042` additive 1건뿐이며 isolated fresh/기존 DB 검증만 수행한다.
- 실제 provider 발송·Teams/Mail 생성 0을 검증에 포함한다.
- runtime 교체 없음. Development 5174/5081 계약 유지.
- Git 경계: experiment local commit만 승인(change-001). push·PR·merge 미승인, `main` merge 승인 `0/3`. 이 문서는 게시·merge·Persistent UAT 승인을 부여하지 않는다.
- 운영 환경의 dev selector·검수 전환 비노출 조건은 재배치 후에도 동일해야 한다.

## 11. 권장 구현 순서 (review 채택)

1. `0042` additive migration, profile photo store·validator·API, `/api/me` additive projection(departmentName·profilePhotoVersion), purge cascade·guard 예외 테스트.
2. Home metrics fixed contract·store·endpoint와 부서/permission/access-scope 테스트.
3. Frontend API/types, actual-user identity badge, desktop popover, mobile account sheet, upload/remove feedback, object URL cleanup.
4. selector를 sidebar/drawer footer 검수 영역으로 이동, 중복 자재 shortcut 제거, full-height compact shell CSS.
5. Home 부서 지표 영역 + reference 기반 compact layout, 기존 HOME-001 widget 회귀 확인.
6. Backend·Frontend 전체 검증, isolated fresh/기존 migration apply, desktop/390px synthetic screenshot, privacy-safe 보고.

## 12. 검증 계획과 완료 기준

검증:

- Backend: 사진 업로드/교체/제거의 성공·형식·크기·구조 위반·비인가·승인 대기 차단·ReviewSafe 차단·actual-claim self-scope·동시 upsert 수렴·purge cascade/guard, 지표 endpoint의 부서별 반환·permission/access-scope 필터·부서 없음·SA 경로·destinationKey allowlist. 전체 test suite 통과.
- Frontend: identity badge fallback, popover/sheet 접근성(Escape·바깥 클릭·focus 복귀·focus trap), 업로드 feedback·중복 차단, 부서 지표 상태 분기, selector 재배치 후 전환 동작, actual/effective 표시 분리. lint·typecheck·unit·build 통과.
- Full-Stack: 격리 환경 E2E smoke(셸·Home 진입·계정 메뉴), migration 41+1개 fresh apply, actual provider 발송 0.
- 시각 증빙: desktop·390px screenshot — identity surface, popover/sheet, sidebar/drawer footer 검수 영역, Home 부서 지표, 부서 없음·SA 경로. Privacy-safe Evidence 규칙 준수.

완료 기준:

- 4장 포함 범위 전부 구현되고 3장 identity 불변조건·6~8장 계약이 자동 테스트로 증명된다.
- Desktop·390px overflow 0, 44px target, reduced motion, 이니셜 fallback 확인.
- Open P0/P1/P2 없음. P3는 backlog 연결.
- 5종 종료 산출물(implementation report·SOP·user manual·Roadmap update·user validation checklist) 상태·위치 추적 가능, 실험 완료 원장 갱신.
- user validation checklist는 `사용자 검수 대기 — 마지막 일괄 검수`로 기록하고 사용자 검수 완료로 표기하지 않는다.
- allowlist staging 후 experiment local commit까지만 수행한다.

중단 조건: 사진 저장이 추적 항목 73과 충돌하는 방식으로만 가능해지는 경우, purge guard·last-administrator 보호 등 기존 안전 불변조건과의 충돌이 발견되는 경우, `/api/me` additive 호환이 유지되지 않는 경우 — 구현을 중단하고 blocking decision으로 보고한다.

openBlockingDecisionCount: 0
