Baseline re-verified on the current branch: the `/` Teams-context branch, the mobile bottom-tab shell with core tabs (내 업무·프로젝트·Pending·알림), the parallel shell summary calls (requested work count, unread notification count), and the backend bottleneck aggregate all exist as round 1 assumed. All five blocking decisions are recorded as adopted in the canonical interview file, every interview section is filled, and no blocking decision remains — so round 2 is the confirmation summary, not further questions.

---

# TASK-HOME-001 — Fable 5 Deep Interview Round 2 (확인용 요약)

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 2

기준선 재확인: round 2 시작 전에 최신 interview 문서와 현재 branch를 다시 대조했다. `/` 진입은 프로젝트 목록이 기본이고 Teams context에서는 Teams Activity로 분기하며, 모바일 하단 tab shell은 핵심 tab 내 업무·프로젝트·Pending(권한 시)·알림 + 더보기 구성으로 존재한다. shell은 내 업무 요청 건수와 읽지 않은 알림 수 summary를 이미 병렬 조회하고, 프로젝트 목록·상세의 additive `bottleneck` aggregate와 권한 인지 서버 정렬(TASK-007B)이 구현되어 있다. round 1의 5개 blocking 결정은 모두 권장안(1-A~5-A)으로 채택되어 interview 문서에 기록되었고, 문서와 구현 사이에 새 충돌은 발견되지 않았다. 추가 blocking 질문은 남아 있지 않으므로 이번 round는 질문 대신 확인용 요약을 제출한다.

## 확인용 요약

### 해결할 업무 문제

로그인 직후 현재 역할에서 확인해야 할 내 업무, 프로젝트 병목, open Pending, 읽지 않은 알림이 화면별로 분산되어 있어 사용자가 메뉴를 순차 이동하며 우선순위를 파악해야 한다. Home은 이 조망을 한 화면으로 모으고 각 원본 화면으로의 진입점을 제공한다.

### 확정한 범위와 정책 (round 1 결정 1~5)

1. **MVP widget 4종 고정** — 내 업무 요약, 프로젝트 병목 Top-N, open Pending 요약(권한 보유 시), 읽지 않은 알림 요약. 모두 기존 API·권한 계약·원본 화면이 있는 항목만 사용한다.
2. **진입 경로** — 신규 `/home` route를 추가하고 `/` 진입 기본 화면을 Home으로 전환한다. 프로젝트 목록은 `/projects`로 분리하고, Teams context의 `/` → Teams Activity 분기와 기존 상세 deep link는 회귀 테스트로 보존을 증명한다.
3. **모바일 위치** — Home을 첫 번째 핵심 tab으로 추가해 최대 5 tab + 더보기 구성으로 하고, 390px 폭·44px touch target을 기존 bounding-box E2E 방식으로 재검증한다.
4. **조회 방식** — 신규 Backend endpoint 없이 widget별로 기존 API를 독립 병렬 호출한다(내 업무 summary, 알림 summary, `GET /api/projects` 서버 정렬 상위 N의 `bottleneck` 재사용, Pending 집계). 한 widget 실패가 다른 widget과 navigation을 차단하지 않고 widget별 재시도를 제공한다.
5. **권한과 empty** — 권한 없는 widget은 완전히 숨긴다(기존 navigation 권한 파생 규칙·007B 노출 차단 계약과 동일). 표시할 widget이 0개면 안내 문구와 현재 허용된 메뉴 링크를 보여준다.

### 명시적 제외

- source data가 없는 예측 widget
- 신규 mutation, Backend persistence, migration, 신규 endpoint, 실제 provider 발송
- 시각 브랜드 전면 개편과 canonical 환경 변경

### Data·lifecycle·audit

신규 data 개념은 widget slot presentation뿐이며 업무 data는 기존 source를 재사용한다. 상태 전이, 신규 persistence, attachment, 신규 notification, migration은 없다. 기존 API·permission·원본 route가 계속 authoritative source이고 기존 권한·audit 계약을 그대로 유지한다.

### 검증과 성공 기준

- 자동 검증: 역할별 widget 노출·숨김, 부분 실패 독립성, empty/error 상태, desktop·390px, page overflow 0, deep link 회귀, keyboard/heading/link 접근성 계약.
- 사용자 검수: isolated synthetic E2E와 screenshot으로 widget 가치·밀도·순서를 확인한다.
- rollback: 실험 branch를 대표 Repository에 적용하지 않는 것으로 충분하다.

### Planning으로 넘기는 deferred 비차단 결정

다음 항목은 범위·계약을 바꾸지 않는 presentation 세부이므로 blocking 질문으로 만들지 않고 planning의 사용자 결정 항목으로 전달한다.

- 병목 widget의 Top-N 기본값(권장: 서버 정렬 상위 5)
- Home widget 기본 표시 순서(권장: 내 업무 → 프로젝트 병목 → Pending → 알림)
- 갱신 정책(권장: 진입 시 조회 + widget별 수동 재시도, 자동 polling 없음)

### 사용자 확인 요청

interview 문서 10장 체크리스트(업무 문제, 역할·권한, 흐름, 포함·제외, blocking 잔여 없음, planning 입력 동의)를 확인해 주시기 바란다. 확인이 기록되어 `COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`이 되면 다음 호출에서 planning 초안을 작성한다.

---

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
