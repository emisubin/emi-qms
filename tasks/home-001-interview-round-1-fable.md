Baseline is established: TASK-HOME-001 depends on the TASK-007B bottleneck aggregate (additive `bottleneck` on project list/detail with permission-aware server ordering) and the TASK-MOBILE-001 bottom-tab shell (core tabs 내 업무·프로젝트·Pending·알림 + 더보기), the current `/` landing renders the project list with a Teams-context branch, and shell summary APIs for requested work count and unread notification count already exist. Below is the requested round 1 artifact.

# TASK-HOME-001 — Fable 5 Deep Interview Round 1

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 1

기준선 요약: 실험 branch에는 TASK-007B의 병목 aggregate(`GET /api/projects`·상세의 additive `bottleneck`, lifecycle → open Pending → 단계 rank → 납기일 서버 정렬, `Pending.Read` 기반 노출 제어)와 TASK-MOBILE-001의 하단 tab shell(핵심 tab 내 업무·프로젝트·Pending(권한 시)·알림 + 더보기 sheet)이 이미 구현되어 있다. 현재 `/` 진입 기본 화면은 프로젝트 목록이며 Teams context에서는 `/`가 Teams Activity로 분기한다. shell은 내 업무 요청 건수와 읽지 않은 알림 수 summary 조회를 이미 사용한다. 아래 5개 질문은 모두 Home MVP의 범위·계약을 확정하는 blocking 결정이며 서로 연결되어 있다.

### 질문 1 — MVP widget 구성을 어디까지 포함할지

- 필요한 이유: 인터뷰 문서는 "현재 source data 기반 widget"을 포함 범위로 정했지만 구체적인 widget 목록이 없다. 현재 data source·권한 계약·deep link가 모두 성립하는 후보는 내 업무 요약, 프로젝트 병목 Top-N, open Pending 요약, 읽지 않은 알림 4종이다.
- 답변이 바꾸는 범위: Frontend 구현 범위, widget별 loading·empty·error 상태 수, E2E·screenshot 검수 matrix, TASK-007B aggregate 재사용 범위.
- 선택지 비교:
  1. **A안 — 4종 고정: 내 업무 요약, 프로젝트 병목 Top-N, open Pending 요약(권한 보유 시), 읽지 않은 알림 요약.** 장점: 네 영역 모두 기존 API·권한 계약·원본 화면이 이미 있어 신규 data 개념이 없고, "분산된 조망"이라는 문제 정의를 한 화면에서 충족한다. 단점: widget 4종만큼 상태·검증 matrix가 커진다.
  2. **B안 — 2종 시작: 내 업무 요약 + 프로젝트 병목 Top-N만 먼저 활성화.** 장점: 최소 구현으로 widget-slot 구조를 검증한다. 단점: Pending·알림 조망이 빠져 기존 메뉴 순차 이동 문제가 절반만 해소되고, 남은 2종을 위한 후속 round가 사실상 필수가 된다.
  3. **C안 — 4종 + 생산관리·구매 등 dashboard별 추가 widget.** 장점: 역할별 조망이 넓어진다. 단점: dashboard별 요약 API가 없어 신규 집계가 필요하고 MVP 범위와 "source data 없는 widget 제외" 원칙을 벗어난다.
- 권장안: **A안.** widget-slot 구조의 목적(단계적 활성화)을 유지하면서 현재 성립하는 source data만 사용하고, 추가 widget은 해당 기능 Task 완료 후 slot에 후속 활성화한다.

### 질문 2 — Home 진입 경로와 로그인 직후 기본 화면

- 필요한 이유: 해결할 문제가 "로그인 직후 한 화면 조망"인데, 현재 `/` 기본 화면은 프로젝트 목록이다. Home을 어떤 URL로 두고 기본 진입을 바꿀지가 URL·bookmark 계약과 회귀 범위를 정한다.
- 답변이 바꾸는 범위: view routing(`initialViewFromLocation`·`pathForView`)의 변경 폭, 프로젝트 목록 경로 신설 여부, Teams context 분기 보존 검증, 기존 deep link 회귀 테스트 범위.
- 선택지 비교:
  1. **A안 — 신규 `/home` route를 추가하고 `/` 진입 기본 화면을 Home으로 전환. 프로젝트 목록에는 별도 경로(예: `/projects`)를 부여하고 Teams context의 `/` → Teams Activity 분기는 유지.** 장점: "로그인 직후 조망"이라는 문제 정의를 직접 충족하고 Home이 진입점으로서 실제 사용된다. 단점: `/` bookmark 의미가 바뀌고 프로젝트 목록 경로 신설로 기존 목록·상세 deep link 회귀 확인이 필요하다.
  2. **B안 — 신규 `/home` route와 메뉴 항목만 추가하고 `/` 기본 화면은 프로젝트 목록 유지.** 장점: 기존 URL 계약 무변경으로 위험이 가장 작다. 단점: 로그인 직후 조망이 사용자의 수동 이동에 의존해 Task의 기대 결과가 약해진다.
  3. **C안 — `/`와 프로젝트 목록 계약은 그대로 두고 로그인 성공 직후에만 1회 `/home`으로 이동.** 장점: URL 계약 보존과 로그인 직후 조망을 절충한다. 단점: 이미 로그인된 세션의 `/` 재진입은 여전히 프로젝트 목록이라 진입 동선이 이원화되고 동작 설명이 복잡해진다.
- 권장안: **A안.** Home의 존재 이유가 기본 진입 조망이므로 실험 branch에서 `/` 전환과 프로젝트 목록 경로 분리를 함께 검증하는 것이 가장 정직한 MVP 검증이다. Teams context 분기와 기존 상세 deep link는 회귀 테스트로 보존을 증명한다.

### 질문 3 — 모바일 하단 tab에서 Home의 위치

- 필요한 이유: TASK-MOBILE-001 실험은 핵심 tab을 내 업무·프로젝트·Pending(권한 시)·알림 4개 + 더보기로 확정했다. Home을 모바일 핵심 navigation에 추가하려면 이 구성과의 충돌을 결정해야 한다.
- 답변이 바꾸는 범위: `AppMobileNavigation`의 tab 구성, 390px touch target·overflow 검증 대상, MOBILE-001 실험 검수 결과와의 정합성.
- 선택지 비교:
  1. **A안 — Home을 첫 번째 핵심 tab으로 추가해 최대 5 tab + 더보기.** 장점: 모바일에서 Home 재진입 동선이 항상 보이고 현재 위치 표시·권한 파생 규칙을 그대로 재사용한다. 390px에서 6개 버튼도 폭·44px 기준을 충족할 수 있다. 단점: tab당 폭이 좁아져 label 축약과 touch target 실측 E2E 재검증이 필요하다.
  2. **B안 — Home은 tab에 넣지 않고 기본 진입 + 상단 브랜드 영역 tap으로만 이동.** 장점: 검수 완료된 4 tab 구성을 보존한다. 단점: Home 재진입 동선의 발견성이 낮아 모바일에서 Home이 사실상 1회성 화면이 된다.
  3. **C안 — 알림을 더보기로 옮기고 Home을 넣어 4 tab 유지.** 장점: tab 수 불변. 단점: 읽지 않은 알림 badge가 항상 보이는 현재 계약이 후퇴하고 MOBILE-001 검수 취지와 충돌한다.
- 권장안: **A안.** Home이 조망 진입점이라는 목적상 모바일에서도 상시 접근이 필요하며, 폭·44px 기준은 MOBILE-001의 기존 bounding-box E2E 방식으로 재검증한다.

### 질문 4 — widget 데이터 조회 방식: 기존 API 재사용 대 신규 aggregate endpoint

- 필요한 이유: "한 widget 실패가 다른 widget을 차단하지 않아야 한다"는 확정 요구가 조회 구조를 결정한다. Backend 신규 surface 여부가 이 Task의 최대 변경 경계다.
- 답변이 바꾸는 범위: Backend 변경 유무, 권한 projection 처리 위치, widget별 독립 재시도 구현 방식, 테스트 계층(Backend unit 대 Frontend unit·E2E).
- 선택지 비교:
  1. **A안 — widget별로 기존 API를 독립 병렬 호출: 내 업무 summary, 알림 summary, `GET /api/projects`의 서버 정렬 상위 N(`bottleneck` 재사용), `GET /api/pending` 집계.** 장점: Backend 무변경으로 007B의 권한·정렬 계약을 그대로 상속하고, widget 독립 실패·재시도가 구조적으로 자연 충족된다. 단점: Home 진입 시 병렬 호출 4건이 발생한다(현재 shell도 summary 2건을 이미 병렬 호출).
  2. **B안 — 신규 read-only `GET /api/home` aggregate endpoint 1개로 통합.** 장점: 호출 1건과 payload 최적화. 단점: 신규 Backend surface에 권한 projection과 부분 실패 의미를 중복 구현해야 하고, 한 응답 실패가 전체 widget을 차단하는 구조적 위험이 생겨 확정 요구와 어긋난다.
- 권장안: **A안.** MVP는 mutation·persistence·신규 endpoint 없이 Frontend 조합만으로 성립하며, 실측 성능 문제가 확인될 때만 후속 Task에서 aggregate endpoint를 검토한다.

### 질문 5 — 권한 없는 widget과 전체 empty Home의 처리 정책

- 필요한 이유: Pending widget은 `Pending.Read` 없는 사용자에게 노출하면 안 되고(007B는 count·정렬 노출까지 권한으로 차단했다), 표시할 widget이 하나도 없는 사용자의 Home 동작도 정의가 필요하다.
- 답변이 바꾸는 범위: widget 노출 규칙, 권한별 E2E matrix, empty Home 안내 UX, 접근성 검증 대상.
- 선택지 비교:
  1. **A안 — 권한 없는 widget은 완전히 숨김(기존 `navigationItems` 권한 파생 규칙과 동일). 표시할 widget이 0개면 안내 문구와 현재 허용된 메뉴로의 링크를 보여준다.** 장점: 007B의 권한 노출 차단 계약과 일관되고 존재 암시 leak이 없다. 단점: 사용자별 Home 구성이 달라져 검수 시 역할별 확인이 필요하다.
  2. **B안 — 권한 없는 widget도 잠금 상태 카드로 표시.** 장점: Home layout이 역할과 무관하게 일정하다. 단점: 권한 없는 사용자에게 해당 업무 영역의 존재·활동을 암시해 007B에서 해소한 `007B-PENDING-LEAK` 계약과 충돌하고 화면 소음이 된다.
- 권장안: **A안.** Backend가 authoritative인 기존 권한 계약을 presentation에서 그대로 따르고, 역할별 노출 차이는 synthetic 역할 전환 E2E와 screenshot으로 검증한다.

---

- interviewStatus: QUESTIONS_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
