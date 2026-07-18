# TASK-HOME-002 — 개인화 Home·프로필 셸 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전 (experiment fast-track 1차 기획)
> 목적: 공통 셸의 로그인 사용자 identity·계정 메뉴·프로필 사진과 부서별 Home 핵심 지표의 구현 계약을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/home-002-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- fastTrackSource: `USER_EXPLICIT_EXPERIMENT_RULE`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 공통 상단이 개발 사용자 selector·중복 자재 shortcut에 점유되어 실제 로그인 사용자 맥락(사진·부서·이름)이 어느 화면에서도 즉시 보이지 않고, 로그아웃·본인 사진 변경이 계정 메뉴로 묶여 있지 않다. Home은 공통 4개 widget만 있어 부서별 첫 판단을 지원하지 못한다.
- 대상 사용자·역할: active 로그인 사용자 전원, 승인 대기 Entra 사용자, System Administrator(검수 사용자 전환 포함), Development mode 사용자.
- 정상 흐름: 페이지 진입 → 오른쪽 위 identity 표시 → avatar 선택 → account menu → 사진 변경 또는 로그아웃 / Home 진입 → 본인 부서 핵심 지표 → metric 선택 시 원본 업무 화면 이동.
- 예외·복구 흐름: 사진 없음·load 실패 시 이니셜 avatar fallback, 업로드 실패 시 기존 사진 보존과 action 인접 오류·재시도, 사진·지표 부분 실패가 navigation과 기존 widget을 막지 않음, identity 전환 시 늦은 응답 폐기.
- 확정한 정책과 명시적 제외: 기존 HOME-001 4개 widget 보존, dev selector의 sidebar/drawer footer 재배치, 중복 자재 top shortcut 삭제(왼쪽/모바일 자재 메뉴 유지), Desktop sidebar full-height, reference 기반 밝은 compact 셸을 EMI red·white로 적용. 제외: 전체 업무 화면 정보 구조 재설계, Graph 사진 동기화, 관리자 대리 사진 변경, 실제 provider·운영 storage·Persistent UAT·대표 repo·`main`·push·PR·merge.
- planning으로 넘긴 비차단 미결정 사항: profile photo storage·lifecycle 상세, 부서 지표 source 선택, mobile account UI 상세 — 모두 이 문서의 권장안 자동 채택 대상이며, 운영 승격 관련 결정만 사용자 결정 항목으로 남긴다(16장).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

모든 업무 화면 오른쪽 위에서 로그인한 자신의 사진·부서·이름과 계정 메뉴(사진 변경·로그아웃)를 사용할 수 있고, Home 첫 화면에서 자신의 부서 핵심 지표와 다음 행동을 바로 확인할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 desktop `topbar`에는 자재 shortcut 버튼, 검수 사용자 전환 select, 개발 사용자 select, 로그아웃 버튼이 나열되어 있고 실제 로그인 사용자의 이름·부서·사진은 표시되지 않는다. 모바일에서는 `상태` trigger로 여는 `MobileSheet` 안에 사용자명이 텍스트로만 노출된다.
- 검수·개발용 전환 control이 실제 로그인 사용자 정보와 같은 영역에서 경쟁해, System Administrator가 actual 계정과 effective 검수 persona를 혼동할 여지가 있다.
- Desktop 왼쪽 `app-sidebar`는 viewport 높이를 완전히 사용하지 않으며, 상단 자재 shortcut은 navigation의 자재 메뉴와 중복이다.
- Home(`HomePage.tsx`)은 내 업무·프로젝트 병목·Pending·알림 공통 4개 widget만 제공해, "우리 부서가 지금 가장 먼저 볼 수치"에 답하지 못한다. 부서 사용자는 각 업무 화면에 직접 들어가 확인하는 우회를 쓴다.
- 이 기능이 없으면 사용자별 로그인 맥락 확인·로그아웃·사진 변경 경로가 분산된 채 남고, Home은 모든 부서에 동일한 화면으로 유지된다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| Active 로그인 사용자 | identity 확인, account menu, 본인 사진 업로드·교체·제거, 로그아웃 | 본인 `/api/me` projection, 본인 부서 허용 지표 | 본인 profile photo만 (self-scope 서버 강제) |
| 승인 대기 Entra 사용자 | 본인 identity 확인, 로그아웃 | 본인 profile만 (업무 데이터 차단 유지) | 없음 — v1에서는 사진 변경 비허용 (권장안, 7장) |
| System Administrator (검수 사용자 전환 중 포함) | actual 계정과 effective persona 구분 확인, 시스템 운영 지표 확인 | actual/effective projection, 시스템 운영 지표 | 본인 actual profile photo만 — effective persona 사진 변경 불가 |
| Development mode 사용자 | synthetic profile 확인, sidebar/drawer footer의 dev selector 사용 | 선택한 dev persona | dev DB 안 synthetic photo만, 실제 Entra profile 생성 없음 |

권한 불변조건: 사진 업로드·제거의 대상 사용자는 항상 claim의 actual user id로 서버가 결정하며, request body·query로 대상 사용자를 받지 않는다. Home 지표는 effective user의 부서·permission으로 서버가 필터링한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 계정 확인과 사진 변경 (Desktop)

1. 사용자가 아무 업무 화면에서 오른쪽 위 avatar(사진 또는 이니셜)·부서명·이름을 확인하고 avatar를 누른다.
2. 시스템이 account popover(사진, 이름, 부서, 로그아웃, 사진 변경)를 연다. Escape·바깥 클릭으로 닫히고 focus는 trigger로 복귀한다.
3. 사용자가 popover의 사진 영역을 눌러 파일을 선택하면 client 검증(형식·크기) 후 업로드되고, 성공 시 셸의 avatar가 즉시 갱신된다. 실패 시 기존 사진이 유지되고 popover 안에 오류·재시도가 표시된다.

### 시나리오 B — 부서 핵심 지표에서 업무 진입 (Home)

1. 자재 부서 사용자가 Home에 진입한다.
2. 시스템이 기존 4개 widget 위에 부서 지표 영역(예: 도착 등록 대기, 입고 확정 대기, 키팅 대기)을 permission-aware로 표시한다.
3. 사용자가 metric card를 누르면 해당 원본 업무 화면으로 이동해 바로 처리를 시작한다.

### 시나리오 C — Mobile 계정 sheet와 검수 영역 분리

1. 모바일 사용자가 상단 app bar 오른쪽의 avatar trigger를 누른다.
2. 시스템이 `MobileSheet` 기반 account sheet(사진·이름·부서 card, 사진 변경, 알림 설정 진입, 로그아웃)를 연다.
3. 개발 사용자·검수 사용자 전환은 account sheet가 아니라 좌측 mobile drawer 하단의 검수 영역에 있으며 실제 로그인 card와 시각적으로 분리된다.

### 시나리오 D — 부서 없음·승인 대기·SA

1. 부서가 없거나 매핑 불가한 사용자는 부서 지표 영역 없이 공통 Home만 본다. 임의 다른 부서 지표를 보여 주지 않는다.
2. 승인 대기 사용자는 identity surface와 로그아웃만 사용하고 업무 데이터·지표는 기존 차단을 유지한다.
3. System Administrator는 업무 부서를 가장하지 않고 시스템 운영 지표(예: 승인 대기 사용자 수, 발송 실패·대기 delivery 수)를 본다.

## 5. 기능 요구사항

### 필수

- [ ] 모든 active 업무 route 공통 셸에 로그인 사용자 사진·부서명·이름 표시 (desktop topbar 오른쪽 / mobile app bar).
- [ ] avatar 사진 없음·load 실패 시 이름 기반 이니셜 avatar fallback.
- [ ] Desktop account popover: 사진(변경 trigger), 이름, 부서, 로그아웃. keyboard 접근, Escape·바깥 클릭 닫기, focus 복귀.
- [ ] Mobile account sheet: `MobileSheet` 재사용, 동일 기능 + 44px touch target.
- [ ] 본인 profile photo 업로드·교체·제거 self-scope API와 server-side 검증(JPEG/PNG, 5MB 이하, content 검사), 실패·취소 시 기존 사진 보존.
- [ ] 로그아웃은 기존 MSAL/dev 로그아웃 계약(`onLogout`)을 그대로 재사용.
- [ ] dev user select·검수 사용자 전환을 desktop sidebar 맨 아래와 mobile drawer footer의 구분된 검수 영역으로 이동. 운영 비노출 조건(`isDevMode`, `canUseAdminTestUserSwitch`) 유지.
- [ ] Desktop sidebar viewport full-height(위 brand~아래 검수 영역), topbar의 중복 자재 shortcut 제거(navigation 자재 메뉴 유지).
- [ ] Home 상단 부서 지표 영역: effective 부서별 핵심 metric card와 원본 route deep link, 기존 4개 widget 보존.
- [ ] 부서 없음·허용 지표 없음이면 지표 영역 비표시, SA는 시스템 운영 지표 표시.
- [ ] identity 전환(dev/검수 전환) 시 이전 사진·지표의 늦은 응답 폐기 (기존 `requestContextKey`·generation guard 패턴).
- [ ] reference 기반 밝은 compact 셸·Home layout을 EMI red·white로 적용 (얇은 top header, full-height 밝은 sidebar, pill active nav, 얇은 border·낮은 shadow).

### 선택

- [ ] avatar 이미지 client-side 축소(정사각 crop/resize) 후 업로드 — MOBILE-001 사진 압축 유틸 재사용 가능 시.
- [ ] account popover에 email 표시(값이 있을 때만).

### 명시적 제외

- [ ] 모든 업무 페이지 정보 구조·기능 재설계.
- [ ] 관리자 대리 사진 변경, 사용자 directory/gallery.
- [ ] Microsoft Graph 프로필 사진 동기화와 신규 Graph permission.
- [ ] 실제 provider 발송, 운영 storage service, Persistent UAT migration·runtime handover.
- [ ] 대표 repo·`main`·push·PR·merge.
- [ ] 기존 HOME-001 widget의 사용자 설정·예측·추천.

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 공통 셸 identity surface | 모든 업무 화면 | avatar(사진/이니셜), 부서 표시명, 이름 | avatar 클릭 → account menu | 사진 load 실패 시 조용히 이니셜 fallback |
| Desktop account popover | identity avatar | 큰 avatar, 이름·부서·(email), 사진 변경, 로그아웃 | 사진 선택·업로드, 로그아웃, 닫기 | 업로드 중 중복 차단, popover 내 인접 성공·오류·재시도 |
| Mobile account sheet | mobile app bar avatar | 위와 동일 + 알림 설정 진입 | 위와 동일 | `useActionFeedback` 계약과 동일한 구조화 feedback |
| Desktop sidebar footer 검수 영역 | 상시(조건 노출) | `검수 사용자 전환`, `개발 사용자` select | persona 전환 | 전환 시 Home/목록 초기화(기존 동작 유지) |
| Mobile drawer footer 검수 영역 | mobile drawer 하단 | 동일 | 동일 | 실제 로그인 card와 구분된 시각 스타일 |
| Home 부서 지표 영역 | Home 상단 | 인사말(이름·부서), metric card(제목, count, 상태 tone, 다음 행동) | card 클릭 → 원본 업무 화면 | 영역 단위 loading/empty/error 분리, 실패가 하단 widget을 막지 않음 |

확인할 UX 항목:

- 사용자가 현재 상태를 이해할 수 있는가 — actual/effective가 다른 검수 모드에서는 기존 `test-user-banner`를 유지하고 account menu에 actual 계정 기준임을 명시한다.
- 다음 행동이 명확한가 — metric card는 count와 함께 행동 문구(예: “도착 등록 대기 n건”)와 deep link를 가진다.
- 저장·변경 결과가 action 근처에 보이는가 — 사진 업로드 feedback은 popover/sheet 내부에 표시한다(UX-001 A1 계약 준수).
- 권한 부족·검수 전용·오류 상태가 명확한가 — ReviewSafe 읽기 모드에서는 사진 변경 control을 비활성화하고 이유를 표시하되 서버 차단이 최종 기준이다.
- 좁은 화면에서도 핵심 행동이 가능한가 — 390px에서 overflow 0, account sheet·drawer footer 44px target, reduced motion 존중.

## 7. 업무 규칙과 불변조건

- 사진 mutation 대상은 항상 claim 기반 actual user 본인이다. 검수 사용자 전환·dev persona로 타인 사진을 변경할 수 없다(impersonation 확장 금지).
- 승인 대기 Entra 사용자는 v1에서 사진 변경이 비허용이다(권장안 채택: 승인 전 계정의 mutation 표면을 최소로 유지, 승인 후 자동으로 사용 가능). 업무 데이터 차단은 기존 정책을 유지한다.
- Home 지표는 effective 사용자의 부서·permission 밖 count·제목·다음 행동을 노출하지 않는다. 부서 없음·매핑 불가 시 다른 부서 지표로 대체하지 않는다.
- 기존 HOME-001 4개 widget, permission redaction, 동일 URL 적응형 계약, 18단계 업무 규칙, Backend authoritative 원칙을 변경하지 않는다.
- 로그아웃·인증 정책(MSAL cache·dev auth)은 우회·변경하지 않는다.
- 사진 업로드는 외부 provider 발송·Graph 호출을 만들지 않는다.
- 운영(Production) 환경에서 dev selector·검수 전환 control은 기존 조건에 따라 노출되지 않으며 이번 재배치가 노출 조건을 바꾸지 않는다.
- 사진 교체·제거는 append-only audit을 남기고, 사용자 purge 시 사진과 함께 정리된다(기존 관리자 삭제 lifecycle에 통합).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 사용자 identity projection | display name, email, department code, actual/effective 분리 | 기존 (`/api/me`) | 변경 없음. 부서 표시명 추가는 additive |
| 부서 표시명 | department code → 사용자용 한글 표시명 | 기존 code + 신규 표시 mapping | 서버 projection에 추가 권장 (9장) |
| profile photo | 사용자당 현재 사진 1장 (binary, mime, byte size, content hash, 갱신 시각·주체) | 신규 테이블 | 교체·제거 append-only audit, purge 연동 |
| photo audit | 업로드/교체/제거 행위 기록 | 신규 (기존 fixed-field audit 패턴 재사용) | append-only, 값 원본 저장 없음(hash·크기만) |
| 부서 지표 | 부서별 핵심 count와 대상 route의 read-only aggregate | 신규 endpoint, 기존 데이터 재사용 | 저장 없음 (조회 시 계산) |

```text
사진 없음(이니셜) → 업로드 중(기존 사진 유지) → 사진 있음 → 교체/제거 → (제거 시) 사진 없음
```

- 사진은 기존 검사 사진 패턴(`panel_quality_report_photos`의 PostgreSQL bytea 저장)과 동일하게 DB bytea로 저장한다. 별도 파일 storage service를 도입하지 않으므로 Roadmap 추적 항목 73(첨부 storage·backup·restore 정책)의 미확정 상태와 충돌하지 않고, 기존 DB backup 경계 안에 있다. 운영 승격 시 보존·purge 정책 확정은 사용자 결정으로 남긴다(16장).
- 교체는 사용자당 1행 upsert로 처리하고 동시 업로드는 row lock 또는 atomic upsert로 마지막 승자만 남긴다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: self-scope 사진 mutation, 파일 형식·크기·content 검증, 부서·permission 기반 지표 필터링, 승인 대기 사용자의 사진 변경 차단, ReviewSafe mutation 차단.
- 필요한 조회와 mutation:
  - `GET /api/me` — 기존 재사용. actual/effective principal projection에 부서 표시명과 사진 유무(또는 사진 version/hash)를 additive로 추가한다. 기존 소비자 호환을 확인한다.
  - 본인 사진 조회 `GET` — 이미지 bytes 반환, 없으면 404/204. content hash 기반 ETag·cache header로 교체 즉시 반영과 캐시를 함께 처리한다.
  - 본인 사진 업로드/교체 `PUT`(또는 `POST`) — multipart 또는 raw body, actual user claim self-scope.
  - 본인 사진 제거 `DELETE`.
  - Home 부서 지표 조회 `GET`(read-only aggregate) — effective 사용자의 부서·permission으로 서버가 metric 목록을 결정해 fixed contract(metric id, 한글 label, count, tone, 대상 route key)로 반환한다.
- 권한·validation: `AuthenticatedIdentity` + active·비승인대기 검사. 사진은 JPEG/PNG magic byte·decode 가능성·5MB 상한(기존 검사 사진 규칙과 동일)을 서버에서 검증하고, 실패는 안정적 status·한글 메시지로 반환한다.
- transaction·동시성·idempotency: 사진 upsert는 단일 transaction, 동시 요청은 atomic upsert로 수렴. 지표 조회는 read-only라 idempotent. 기존 store들의 connection/transaction 패턴을 따른다.
- audit trail: 업로드/교체/제거를 append-only로 기록(행위자=actual user, content hash·byte size·시각). NOTIFY-005의 fixed-field audit 패턴을 참조한다.
- 외부 provider 영향: 없음. 알림·Teams·메일을 생성하지 않는다.
- migration: `database/migrations/0042` additive 1건(사진 + audit 테이블). 기존 migration 수정·번호 재사용 없음. fresh DB와 기존 isolated DB 모두 검증한다. 사용자 purge 경로(`AdminScheduledDeletionService`)에 사진 정리를 통합하고 purge guard 불변조건을 훼손하지 않는다.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 확정하지 않는다. 위 이름은 방향이며 구현 세션이 기존 convention에 맞춘다.

## 10. Frontend 고려사항

- route/component: `App.tsx`의 `topbar`·`AppNavigation`·`AppMobileNavigation`·`MobileSheet`와 `HomePage.tsx`를 중심으로 수정한다. 신규 component는 identity badge, account popover/sheet, 부서 지표 영역 정도로 제한하고 기존 flat 구조·naming을 따른다.
- loading/empty/error/success: 사진과 지표는 각각 독립 상태로 관리하고 실패가 navigation·기존 widget을 막지 않는다. `HomePage.tsx`의 `WidgetState`·generation guard 패턴을 재사용한다.
- 공통 Action Feedback: 사진 업로드·제거는 UX-001 A1의 `useActionFeedback` 계약(중복 submit 차단, action 인접 feedback, 오류 focus, `aria-live`)을 재사용한다.
- 접근성: popover는 trigger `aria-expanded`·`aria-controls`, Escape·바깥 클릭 닫기, focus trap 없이 focus 복귀. sheet·drawer는 기존 focus trap 패턴 재사용. 이니셜 avatar에 이름 대체 텍스트.
- 390px/mobile/narrow pane: page-level overflow 0, account sheet full-height 대응, drawer footer 검수 영역 44px target, reduced motion 시 popover/sheet 전환 애니메이션 축소.
- 디자인: 기존 CSS variable·EMI red·white 브랜드 자산을 재사용하고 reference의 파란색 계열은 EMI red·soft red로 치환한다. 시각 변경 범위는 공통 셸과 Home layout으로 한정한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 부서 지표는 내 업무 summary·병목·Pending·procurement dashboard·production-planning summary 등 기존 집계 데이터와 008A~014A store 데이터를 read-only로 재사용한다. 알림 badge·기존 widget 동작은 변경하지 않는다.
- 권한/관리자: `/api/me`의 roles·permissions·`canUseAdminTestUserSwitch`를 그대로 사용한다. SA 시스템 운영 지표는 admin dashboard의 기존 집계 개념을 재사용한다.
- Excel/PDF/첨부: 영향 없음. 검사 사진 계약은 변경하지 않고 검증 규칙만 준용한다.
- Teams/Mail: 영향 없음(발송 생성 0 검증 포함).
- 삭제·복구/감사: 사용자 삭제 예정·purge lifecycle에 사진 정리를 통합하고, 사진 변경 audit은 관리자 이력 조회의 후속 확장 대상으로 남긴다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A. 부서 지표: 신규 read-only aggregate endpoint 1개 (권장) | 서버가 부서·permission으로 metric 목록을 계산해 fixed contract로 반환 | 단일 호출, 권한 필터가 서버에 집중, 화면 payload 과다 호출 없음, 부서 추가가 계약 안에서 확장 | Backend 신규 endpoint·테스트 필요 |
| B. 부서 지표: 기존 화면 API 병렬 호출 | Frontend가 부서별로 기존 목록/summary API를 조합 | Backend 무변경 | 부서마다 다른 다건 호출, 목록 payload 과다, 권한 redaction을 Frontend가 조립하는 위험 — interview의 “무차별 병렬 호출 금지”와 충돌 |
| C. 사진 저장: DB bytea 테이블 (권장) | 기존 검사 사진과 동일한 in-DB binary | 검증·backup·purge가 기존 경계 안, 추적 항목 73 미확정과 충돌 없음, 실험 격리 용이 | 대용량 확장성 한계 — avatar 1장·5MB 상한이라 실측 위험 낮음 |
| D. 사진 저장: 파일시스템/외부 storage | 별도 storage 경로 도입 | 대용량 유리 | 미확정 storage·backup·restore 정책(추적 73)을 선결해야 해 blocking, 실험 범위 초과 |
| E. Mobile 계정 UI: `MobileSheet` account sheet (권장) | 기존 sheet 재사용 + drawer footer 검수 영역 분리 | 기존 focus·접근성 계약 재사용, desktop popover 단순 축소 회피 | 기존 `상태` sheet와 역할 정리 필요(계정 요소를 account sheet로 이관) |

권장안: A + C + E. Desktop account menu는 interview 확정안(avatar trigger + 이름/부서 요약 + 사진 변경 + logout)을 그대로 쓴다. 승인 대기 사용자 사진 변경은 v1 비허용을 권장 채택한다(7장 근거). 모두 fast-track standing rule의 권장안 자동 채택 범위다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic DB·runtime에서만 검증한다.
- migration 필요 여부: `0042` additive 1건(사진·audit). isolated/fresh DB 검증만 수행하며 Persistent UAT 적용은 별도 승인 대상.
- 외부 발송/실제 데이터 영향: 없음. actual provider 0 검증을 포함한다.
- runtime 교체 여부: 없음. Development 5174/5081 계약 유지.
- 추가 사용자 승인 필요 작업: push·PR·merge(`0/3`), Persistent UAT 적용, 운영 storage·보존 정책 확정, Graph 사진 동기화 도입. experiment local commit만 change-001로 승인되어 있다.

## 14. 검증 계획

- 최소 테스트: Backend — 사진 업로드/교체/제거의 성공·형식·크기·비인가·승인대기 차단·self-scope·동시 upsert·purge 연동, 지표 endpoint의 부서·permission별 반환·부서 없음·SA 경로. Frontend — identity badge fallback, popover/sheet 접근성 동작, 업로드 feedback, 부서 지표 상태 분기, dev selector 재배치 후 전환 동작.
- 영향 영역 회귀: Validation Matrix에 따른 Backend 전체 test, Frontend lint·typecheck·unit·build, 셸 변경 영향 확인용 Full-Stack E2E smoke(격리 환경). migration 41+1개 fresh apply 검증.
- PR/CI: 해당 없음(push·PR 미승인). local commit 전 동일 검증을 완료한다.
- 사용자 검수: desktop·390px screenshot(identity surface, popover/sheet, sidebar/drawer footer, Home 부서 지표, 부서 없음·SA 경로)을 privacy-safe로 남기고 user validation checklist는 `사용자 검수 대기 — 마지막 일괄 검수`로 기록한다.

## 15. 완료 기준

- 기능/권한/데이터: 5장 필수 항목 전부 구현, self-scope·부서 필터·승인대기 차단이 서버 테스트로 증명, `0042` fresh apply 성공.
- UX: Desktop·390px overflow 0, focus 복귀, Escape/바깥 클릭 닫기, 44px target, reduced motion, 이니셜 fallback 확인.
- 자동 테스트: Backend·Frontend 전체 suite 통과, actual provider 발송 0.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist의 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수` (`EXPERIMENT_COMPLETE / BATCHED_FINAL` 후보).
- PR 상태: N/A — push·PR·merge 미승인, experiment local commit만.

중단 조건: 사진 저장이 추적 항목 73과 충돌하는 방식으로만 가능해지는 경우, purge guard·last-administrator 보호 등 기존 안전 불변조건과 충돌이 발견되는 경우, 기존 `/api/me` 소비자 호환이 additive로 유지되지 않는 경우 — 구현을 중단하고 blocking decision으로 보고한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 운영 승격 시 첨부·사진 storage·backup·restore·보존/purge 정책 (Roadmap 추적 항목 73) | DB bytea 유지 / 파일·외부 storage 전환 / 보존 기간·purge 기준 | 대기 — canonical 승격·Persistent UAT 전 확정 |
| 2 | Microsoft Graph 프로필 사진 동기화 도입 여부 | 미도입 유지 / 별도 NEW_FEATURE로 도입 | 대기 — 이번 범위에서 제외 확정, 도입 시 별도 planning |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Identity`(me projection·사진 endpoint·store), 신규 Home summary aggregate, `Admin` purge 연동, 관련 contract.
- Frontend: `App.tsx`(topbar·sidebar·drawer·MobileSheet 구성), `HomePage.tsx`(부서 지표 영역), `api.ts`, 공통 CSS(shell·Home layout), 신규 identity/account/지표 component.
- DB/Migration: `database/migrations/0042` additive.
- Tests/Scripts: Backend endpoint·동시성·purge 테스트, Frontend unit, E2E smoke 갱신.
- Docs: Roadmap 21~25장 갱신, 실험 완료 원장 항목 추가, Task 산출물.

## 18. Roadmap 연결

- 선행 Task: TASK-HOME-001, MOBILE-001/002, DESIGN-001, UX-001 A1, NOTIFY-005 — 모두 experiment 완료 상태이며 재구현하지 않는다.
- 후속 Task: TASK-UX-001 A2(권장 다음 범위, 이번 override로 뒤로 이동), 운영 storage 정책(추적 73), Graph 사진 동기화 후보, canonical 승격·통합 UAT Task.
- 현재 Go/No-Go: experiment fast-track 진행 Go — Roadmap override는 change-001에 `explicitRoadmapOverrideApproved: true`로 기록됨. 대표 repo·`main`·Persistent UAT·provider는 No-Go 유지.
- 별도 Task로 분리할 항목: 관리자 대리 사진 관리·사용자 directory, 사진 변경 audit의 관리자 조회 UI, HOME widget 사용자 설정.

## 19. Codex 구현 지시문 초안

이 절은 2차 기획·구현 세션을 위한 초안이며 구현 승인을 부여하지 않는다.

1. instruction chain gate 후 `experiment/task-home-002-personalized-shell`에서 시작하고 완료 원장·change-001 경계를 재확인한다.
2. Backend: `0042` additive migration(사진·audit) → self-scope 사진 GET/PUT/DELETE(검증·upsert·audit·purge 연동) → 부서 지표 aggregate endpoint(부서·permission 필터, fixed contract) → `/api/me` projection에 부서 표시명·사진 version additive 확장. 기존 store·transaction·validation convention을 따른다.
3. Frontend: identity badge·account popover(desktop)·account sheet(mobile, `MobileSheet` 재사용) → dev/검수 control을 sidebar 맨 아래·drawer footer 검수 영역으로 이동, topbar 자재 shortcut 제거 → sidebar full-height·reference 기반 셸/Home layout을 EMI red·white로 적용 → `HomePage.tsx` 상단 부서 지표 영역(`WidgetState`·generation guard·`useActionFeedback` 재사용).
4. 검증: Backend·Frontend 전체 suite, migration fresh apply, 격리 E2E smoke, desktop/390px screenshot, provider 발송 0, privacy-safe 증빙.
5. 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist(`사용자 검수 대기 — 마지막 일괄 검수`)와 완료 원장 갱신, allowlist staging 후 local commit만 수행한다.

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 2
