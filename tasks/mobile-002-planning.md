Baseline revalidation is complete. Key facts confirmed: layout mode today is a single `(max-width: 860px)` matchMedia hook (`useIsMobileViewport`, ~15 call sites) plus five 860px CSS blocks with zero pointer/hover/orientation queries anywhere in the frontend; view state is URL-pathname-based (`pathForView`/`initialViewFromLocation`/popstate); the bottom tab already carries the Home slot (6 slots max) from the TASK-HOME-001 experiment; the home widget dashboard has four independently failing widgets reusing existing summaries. Below is the single primary planning draft artifact.

# TASK-MOBILE-002 — 모바일 우선 적응형 화면 체계 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/mobile-002-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 현재 모바일 화면 다수가 PC 정보구조를 축소·재배치한 형태라서, 모바일이 최우선인 현장 사용자가 한 손으로 다음 행동을 빠르게 판단·실행하기 어렵다. 이를 현장 행동 중심의 모바일 전용 composition으로 교체한다.
- 대상 사용자·역할: 현장 업무 사용자, 관리·조회 사용자, Read-only·System Administrator. 모두 기존 역할·권한 범위를 그대로 사용하며 모바일 mode가 권한을 확대하거나 우회 action을 만들지 않는다.
- 정상 흐름: 같은 URL 로그인 → viewport·pointer·hover·orientation·safe-area 능력 판별 → 모바일 전용 또는 desktop composition 선택 → 기존 route와 API로 조회·처리.
- 예외·복구 흐름: 기존 API validation·화면별 error contract를 유지하고 모바일에서는 action 인접 feedback으로 표시한다. mode 재판별 중 동일 업무 맥락(현재 view·입력 상태)을 보존하고, 기존 route/deep link·browser history를 유지한다. 신규 aggregate·API 없이 section별 부분 실패를 독립 표시한다. 실험 branch를 채택하지 않는 것이 rollback이다.
- 확정한 정책과 명시적 제외 (interview 결정 1-B·2-A·3-A·4-A):
  - Layout mode 판별(1-B): viewport 주신호 — 860px 이하 모바일 composition, 861px 이상 desktop composition. pointer·hover 보조신호 — coarse pointer이거나 hover 불가면 mode와 무관하게 터치 affordance를 보강하고, 861px 이상 터치 화면은 desktop composition에 터치 보정만 적용한다. Teams narrow는 폭 기준으로 모바일 composition을 받는다. 판별은 중앙 hook/context 하나로 통합한다.
  - 재구성 범위(2-A): 홈, 내 업무, 프로젝트 목록·상세, Pending 목록·상세, 알림의 7개 현장 핵심 route만 모바일 전용 재구성. 생산관리·구매·자재·관리자는 기존 responsive 수준 유지 + shell 전역 기준(page-level overflow 0, safe-area, 44px touch target)과 회귀 없음만 보장.
  - 모바일 홈(3-A): 기존 홈 widget 데이터 원천을 그대로 재사용해 단일 컬럼 우선순위(긴급·차단 → 내 업무 → 담당 프로젝트 → 나머지 요약)로 재배치. 신규 aggregate·API 없음.
  - 하단 tab(4-A): 홈 tab을 첫 슬롯에 둔 5-tab(홈·내 업무·프로젝트·권한 시 Pending·알림)+더보기. 390px 최대 6슬롯의 label 축약·최소 44px 폭·overflow 0을 완료 기준 E2E에 포함.
  - 명시적 제외: 별도 모바일 URL·앱·인증·session, Backend·API·DB·migration·권한·업무 상태 변경, 사진 저장·압축·offline queue·업로드 재시도, Persistent UAT write·runtime handover·실제 provider 발송, 대표 repo·GitHub `main`·push·PR·merge.
- planning으로 넘긴 비차단 미결정 사항: 판별 오차 대비 사용자 수동 "PC 화면으로 보기" toggle의 도입 여부와 저장 위치·유지 기간·초기화 정책. 본 문서 16장에서 권장안과 함께 사용자 결정 항목으로 전달한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

### 재검증한 Repository 기준선 (planning 시점)

- Layout mode 판별은 `frontend/src/App.tsx`의 `useIsMobileViewport()`(단일 `(max-width: 860px)` matchMedia) 하나이며, 내 업무·프로젝트·생산관리·구매·자재·알림 등 약 15개 화면 함수가 개별 호출해 카드 변형을 분기한다. `AppMobileNavigation`도 자체 860px matchMedia listener를 별도로 갖는다. `frontend/src/styles.css`에는 `(max-width: 860px)` block 5개가 있고, `(pointer: coarse)`·`(hover: none)`·orientation query는 frontend 전체에 존재하지 않는다 — capability 보조신호는 전부 신규 작업이다.
- view 상태는 URL pathname 기반(`initialViewFromLocation`/`pathForView`/popstate)이라 mode 판별과 무관하게 현재 화면·deep link 맥락이 URL로 보존된다. 동일 URL 계약의 기술 기반이 이미 성립한다.
- 하단 tab은 TASK-MOBILE-001 실험 + TASK-HOME-001 실험으로 이미 홈 포함 최대 5개 core(`mobilePrimaryNavigationLabels`: 홈·내 업무·프로젝트·Pending·알림)+더보기 6슬롯이며, 권한 파생 `navigationItems` 단일 배열에서 presentation 분할된다. 즉 interview 결정 4-A의 tab 구조는 이 branch에 이미 존재하고, 이번 Task의 tab 작업은 label 축약·44px 폭·overflow 검증과 회귀 보장이다.
- 홈은 `frontend/src/HomePage.tsx`의 widget dashboard 실험이 존재한다: 내 업무 요약(`getMyWorkSummary`), 프로젝트 병목 상위 5(`listProjects` pageSize 5 + bottleneck), Pending 요약(`listPendingIssues`, `Pending.Read` 보유 시), 알림 요약(`getNotificationSummary`) 4개 widget이 각각 독립 loading/ready/empty/hidden/error 상태를 가진다. 결정 3-A의 데이터 원천과 부분 실패 격리 경계가 이미 구현돼 있고, 이번 Task는 모바일 단일 컬럼 우선순위 재배치를 추가한다.
- 더보기 sheet의 focus containment·Esc·복귀, safe-area(`viewport-fit=cover` + `env(safe-area-inset-*)`), ≤860px 44px touch target 보정은 TASK-MOBILE-001 구현으로 존재한다.
- 테스트 기준선: Vitest(`frontend/tests/App.test.tsx`, matchMedia stub 전례), Playwright full-stack spec(mobile-adaptive-navigation·home-dashboard·pending-list·project-bottleneck·project-registration)과 mock-ui smoke, 격리 DB runner(`scripts/e2e-full-stack.sh`). 390px·480px overflow·touch target 실측 패턴이 이미 있다.
- 이 branch는 `experiment/task-mobile-002-mobile-first-experience`이며, canonical Roadmap의 현재 Next Gate는 `TASK-007A Fable deep-interview`다. 본 Task 진행은 interview Task Identity Gate의 `roadmapSequenceMatch: false` + `explicitRoadmapOverrideApproved: true` 기록(실험 경계) 안에서만 유효하고 canonical 승인·merge를 대신하지 않는다.

## 1. 한 줄 목표

현장 사용자가 모바일(≤860px)에서 PC 축소판이 아닌 모바일 전용 정보구조로 첫 화면에서 긴급·차단과 오늘 할 일을 먼저 판단하고, 7개 현장 핵심 화면의 핵심 action을 한 손 thumb zone에서 실행하며, desktop 사용자는 기존 관리형 밀도를 그대로 유지한다.

## 2. 배경과 해결할 업무 문제

- 현재 860px 이하 화면은 하단 tab과 일부 카드 변형을 제공하지만, 다수 화면이 PC의 표·필터·section 순서를 세로로 축소·재배치한 형태다. 사용자는 화면을 길게 스크롤하며 PC용 정보 밀도 안에서 다음 행동을 찾아야 한다.
- 화면 함수 약 15곳이 `isMobile`을 개별 분기해 route별 모바일 패턴이 서로 다르고, capability(터치·hover) 신호를 쓰지 않아 861px 이상 터치 기기는 hover 전제 desktop UI를 그대로 받는다.
- 현재 우회 방식: 세로 스크롤로 PC용 표·필터·상세 section을 차례로 찾아 사용한다.
- 이 기능이 없으면 현장 기능이 늘수록 PC layout 축소 규칙이 화면마다 누적되고, route별 모바일 패턴 편차로 사용성·접근성·검증 비용이 계속 커진다. Roadmap 3.4의 "모바일 최우선 현장 입력" 원칙과의 간극도 커진다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 현장 업무 사용자 | 모바일 첫 화면에서 긴급·내 업무 판단, 핵심 action 실행 | 기존 역할별 범위 | 기존 API와 서버 Policy가 허용한 mutation만 |
| 관리·조회 사용자 | Desktop 표·필터·일괄 조회 유지, 모바일 요약 조회 | 기존 역할별 범위 | 기존 권한과 동일 |
| Read-only·System Administrator | 허용 화면 조회·시스템 관리 진입 | 기존 범위 | 기존 정책과 동일 (모바일 전용 UI가 우회 action을 만들지 않음) |

모바일 composition은 기존 권한 필터(`navigationItems`, `canReadPending` 등)와 서버 Policy 결과만 재사용하며, mode에 따라 노출 데이터·action 권한이 달라지지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 현장 사용자의 모바일 아침 진입 (390px)

1. 사용자가 기존 URL로 로그인하면 layout mode가 `mobile`로 판별되고 모바일 홈이 열린다.
2. 첫 화면 상단에서 긴급·차단(열린 Pending 긴급 건수, 긴급·차단 알림)을 먼저 보고, 이어 내 업무(시작 전·진행 중·차단), 담당 프로젝트 병목 순으로 확인한다.
3. 내 업무 section의 primary action으로 내 업무 화면에 진입해 업무 card의 시작/완료 action을 thumb zone에서 실행한다.
4. 처리 결과는 action 인접 feedback으로 표시되고, 뒤로 가기로 이전 맥락에 복귀한다.

### 시나리오 B — 프로젝트 상세의 단계적 탐색

1. 사용자가 프로젝트 tab → 카드형 목록에서 병목 표시를 보고 프로젝트를 연다.
2. 모바일 상세는 PC section 나열 대신 "지금 필요한 것" 우선 구조로 표시된다: 병목·다음 행동 → 진행 단계 요약 → 상세 정보는 접힌 section을 단계적으로 펼침.
3. 병목의 다음 행동(예: Pending 열기)이 sticky primary action으로 하단 thumb zone에 유지된다.

### 시나리오 C — Teams narrow와 861px 이상 터치 기기

1. Teams narrow pane(폭 ≤860px, 마우스·hover 가능)은 폭 기준으로 모바일 composition을 받고, hover 가능 신호는 터치 affordance 보강을 생략한다.
2. 861px 이상 터치 태블릿은 desktop composition을 유지하되 터치 보정(44px interactive target 등)만 적용된다.
3. 두 경우 모두 같은 URL·권한·데이터이며 화면 구성만 다르다.

### 시나리오 D — mode 전환 중 업무 맥락 보존

1. 사용자가 창 크기 조절 또는 기기 회전으로 861px 경계를 넘으면 mode가 실시간 재판별된다.
2. 현재 view(URL)와 진행 중 입력 상태는 보존되고, composition만 전환된다.
3. 전환 후에도 동일 화면의 loading/error 상태와 배지가 유지된다.

### 시나리오 E — 부분 실패 격리 (모바일 홈)

1. Pending 요약 조회가 실패해도 긴급·차단 section의 알림 측 데이터와 나머지 section(내 업무·담당 프로젝트)은 정상 표시된다.
2. 실패한 section만 인접한 오류 안내와 다시 시도 action을 표시한다(기존 widget 실패 격리 계약 유지).

## 5. 기능 요구사항

### 필수

- [ ] 중앙 layout mode 계약: viewport(≤860px) 주신호 + pointer·hover 보조신호를 판별하는 단일 hook/context를 도입하고, 기존 `useIsMobileViewport` 개별 호출과 `AppMobileNavigation`의 자체 matchMedia를 이 계약으로 통합한다. 반환 형태는 `mode(mobile|desktop)`와 `touch(boolean)` 수준의 최소 계약으로 한다.
- [ ] 터치 affordance 계층: coarse pointer·hover 불가 신호 시 mode와 무관하게 44px 이상 interactive target과 터치 우선 상호작용을 적용한다(CSS `(pointer: coarse)`/`(hover: none)` 병용 가능). 861px 이상 터치 기기는 desktop composition + 터치 보정만.
- [ ] 모바일 shell 기준: 기존 하단 tab(홈 포함 6슬롯)·safe-area·더보기 sheet를 유지하고, 재구성 route에 모바일 page header(화면 제목, 상세 route의 뒤로 가기)와 단일 컬럼 구조를 제공한다. 390px에서 6슬롯 label 축약·슬롯당 최소 44px 폭·overflow 0을 검증한다.
- [ ] 모바일 홈 재구성: 기존 4개 widget 데이터 원천을 재사용해 단일 컬럼 우선순위(긴급·차단 → 내 업무 → 담당 프로젝트 병목 → 나머지 요약)로 재배치한다. 긴급·차단 section은 Pending 요약과 알림 요약의 기존 두 원천을 presentation에서 병치하되 실패는 원천별로 독립 표시한다.
- [ ] 내 업무 모바일 재구성: 요약 지표와 상태 tab을 좁은 폭 기준으로 재구성하고, 업무 card의 시작/완료 action을 thumb zone에 배치한다. 성공·실패 feedback은 action 인접 표시로 유지한다.
- [ ] 프로젝트 목록·상세 모바일 재구성: 목록은 검색·필터를 모바일 패턴(16장 결정 3)으로 정리한 카드 목록, 상세는 병목·다음 행동 우선 + 단계 요약 + 접힘 section의 단계적 펼침 구조. 병목 다음 행동을 sticky primary action으로 제공한다.
- [ ] Pending 목록·상세 모바일 재구성: 긴급 우선 카드 목록과 상세의 허용 action thumb zone 배치. 기존 조치 흐름·권한 gating 무변경.
- [ ] 알림 모바일 재구성: 프로젝트별 묶음·최신순 원칙을 유지한 카드 구조와 44px 이상 읽음 action.
- [ ] mode 재판별 시 업무 맥락 보존: 동일 view(URL)·입력 상태를 유지한다. 입력 상태가 있는 화면은 mount된 component tree를 유지한 presentation 분기로 구현하고, mode 전환이 form 상태를 초기화하지 않는지 검증한다.
- [ ] Desktop 회귀 보존: 861px 이상 composition·정보 밀도·일괄 편집 UX 무변경. 미재구성 route(생산관리·구매·자재·관리자)는 기존 responsive 수준 + shell 전역 기준 + 회귀 없음.
- [ ] URL·계약 보존: `pathForView`/`initialViewFromLocation`/popstate·Teams deep link·기존 API·mutation·배지 계약 무변경.

### 선택

- [ ] 미재구성 route의 심각한 축소형 UX 항목을 Finding/후속 범위 목록으로 기록 (구현하지 않음)
- [ ] mode 판별 matrix(viewport×pointer×hover)의 mock-ui smoke 추가

### 명시적 제외

- [ ] 별도 모바일 URL·앱·인증·session
- [ ] Backend·API·DB·migration·권한·업무 상태 변경 (신규 aggregate 포함)
- [ ] 사진 저장·압축·offline queue·업로드 재시도
- [ ] Persistent UAT write·runtime handover·실제 provider 발송
- [ ] 대표 repo·GitHub `main`·push·PR·merge
- [ ] 생산관리·구매·자재·관리자 route의 모바일 전용 재구성 (후속 범위)
- [ ] 사용자 수동 "PC 화면으로 보기" toggle 구현 (16장 결정 1 — 기본 권장은 후속 분리)

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 모바일 홈 | 로그인·홈 tab | 긴급·차단 → 내 업무 → 담당 프로젝트 병목 → 나머지 요약 (단일 컬럼) | section별 원본 화면 진입, 다시 시도 | section별 독립 loading/empty/error, 실패 인접 재시도 |
| 내 업무 (mobile) | 내 업무 tab·홈 section | 요약 지표, 상태별 업무 card | 업무 시작/완료, 원본 업무 진입 | action 인접 성공/실패 메시지 유지 |
| 프로젝트 목록 (mobile) | 프로젝트 tab | 카드 목록(병목 badge), 검색·필터 | 검색·필터, 상세 진입 | 기존 loading/empty/error 유지 |
| 프로젝트 상세 (mobile) | 목록·deep link | 병목·다음 행동, 단계 요약, 접힘 상세 section | section 펼침, sticky 다음 행동 실행 | 기존 화면 feedback, action 인접 표시 |
| Pending 목록·상세 (mobile) | Pending tab·병목 연결 | 긴급 우선 카드, 상세 조치 정보 | 상세 진입, 허용된 조치 action | 기존 조치 feedback 유지 |
| 알림 (mobile) | 알림 tab | 프로젝트별 묶음·최신순 card | 읽음 처리, 원본 진입 | 기존 feedback 유지 |
| Desktop 전체 | 861px 이상 | 기존 sidebar·표·필터 밀도 | 기존과 동일 | 기존과 동일 (무변경) |

확인할 UX 항목:

- 모바일 첫 화면에서 "지금 무엇이 급한가"가 스크롤 없이 보이는가.
- 각 재구성 화면의 1차 action이 thumb zone(하단 고정 또는 card 인접)에서 44px 이상으로 실행 가능한가.
- 저장·변경 결과가 실행한 action 근처에 보이고 긴 page 상단으로만 가지 않는가.
- 권한 부족·ReviewSafe·오류 상태 표시가 sticky action·하단 tab과 겹치지 않는가.
- 390px·Teams narrow(480px)에서 page-level horizontal overflow 0, focus 순서·screen reader 안내·reduced motion이 성립하는가.
- mode 전환 시 화면이 PC 축소판으로 되돌아가지 않고 입력 상태가 보존되는가.

## 7. 업무 규칙과 불변조건

- 동일 URL·인증/session·Teams deep link·서버 권한 강제·18단계 workflow·기존 mutation·audit 계약을 변경하지 않는다.
- Layout mode는 client runtime presentation 상태일 뿐이며, mode에 따라 노출 데이터 범위·권한·count가 달라지지 않는다.
- 모바일 composition은 기존 화면이 제공하지 않는 새 mutation 진입점을 만들지 않고, 기존 화면이 제공하는 필수 action을 잃지도 않는다(정보·action 보존, 구조만 재배치).
- 신규 영속 data·API·aggregate·상태 전이 없음. section별 부분 실패는 기존 원천 경계대로 독립 표시한다.
- mode 재판별(resize·orientation)은 현재 view(URL)·입력 상태·loading/error 상태를 보존한다.
- Desktop(861px 이상) composition·관리·일괄 편집 UX는 회귀 없이 보존한다.
- 실험 branch 미채택이 rollback이며, 대표 repo·`main`·Persistent UAT·provider는 무변경이다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| layout mode·touch capability | 중앙 hook/context의 client runtime 상태 (비영속) | 신규 (client memory only) | 저장·감사 대상 아님 |
| 홈 widget 요약 (내 업무·병목·Pending·알림) | 기존 4개 조회 원천 | 기존 재사용 | 변경 없음 |
| view 상태 | URL pathname 기반 화면 상태 | 기존 재사용 | 변경 없음 |
| 업무·프로젝트·Pending·알림 데이터 | 화면 데이터와 mutation | 기존 재사용 | 변경 없음 |
| 상세 section 펼침 상태 | 화면별 일시적 UI 상태 (비영속) | 신규 (client memory only) | 저장·감사 대상 아님 |

```text
(신규 업무 상태 전이 없음)
layout mode: viewport·pointer·hover 판별 → mobile | desktop (+touch 보강) → 신호 변경 시 재판별 (view·입력 상태 보존)
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 모든 권한·mutation 차단·업무 불변조건 — 이번 Task는 Backend를 변경하지 않고 기존 계약을 그대로 사용한다.
- 필요한 조회와 mutation: 신규 없음. 기존 내 업무·프로젝트(병목 포함)·Pending·알림 조회와 기존 mutation만 사용한다.
- 권한·validation: 신규 없음. 노출은 기존 권한 필터 재사용, 강제는 기존 서버 Policy 유지.
- transaction·동시성·idempotency: 영향 없음 (presentation-only).
- audit trail: 영향 없음 (새 record·mode 저장 없음).
- 외부 provider 영향: 없음 (실제 발송 금지 유지).

## 10. Frontend 고려사항

- route/component: `frontend/src/App.tsx`에 중앙 layout mode hook/context를 도입하고 기존 `useIsMobileViewport` 호출부(약 15곳)와 `AppMobileNavigation`의 자체 matchMedia를 통합한다. 7개 재구성 route(`home`·`my-work`·`list`·`detail`·`pending`·`pending-detail`·`notifications`)는 기존 component 안에서 mode 기반 presentation 분기로 재구성한다 — 별도 mount tree 분리는 입력 상태 보존 불변조건 때문에 피한다. `HomePage`는 mode에 따라 grid ↔ 단일 컬럼 우선순위 배치를 전환한다.
- loading/empty/error/success: 화면·widget별 기존 상태 계약을 유지하고, 모바일에서는 실행한 action과 실패 section 인접에 표시한다.
- 공통 Action Feedback: 기존 메시지 계약 유지. sticky primary action 영역이 feedback을 가리지 않도록 배치를 검증한다.
- 접근성: 모바일 page header의 heading 구조, 접힘 section의 `aria-expanded`/keyboard 접근, sticky action의 focus 도달, 배지·건수의 screen reader 안내, reduced motion 존중, 색 외 상태 구분 유지.
- 390px/mobile/narrow pane: 860px 주신호 + `(pointer: coarse)`/`(hover: none)` 보조 CSS 계층, `env(safe-area-inset-*)`와 하단 tab·sticky action의 이중 예약(콘텐츠 하단 여백), 6슬롯 tab의 label 축약·44px 폭, 재구성 route와 shell 전역의 page-level horizontal overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 기존 병목 표시(`ProjectBottleneckOverview`)·내 업무 action·알림 묶음·배지 집계를 진입점과 데이터 원천으로 재사용한다.
- 권한/관리자: `navigationItems` 권한 파생과 `canReadPending` 등 기존 필터 재사용. 관리자 화면은 재구성하지 않고 회귀만 확인한다.
- Excel/PDF/첨부: 변경 없음. Desktop 중심 원칙(Roadmap 3.4) 보존, 신규 attachment upload 제외.
- Teams/Mail: Teams narrow 표시 계약(폭 기준 모바일 composition)과 deep link 경로 보존, 발송 변경 없음.
- 삭제·복구/감사: 변경 없음.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 중앙 capability hook/context + 기존 component 내 mode 분기형 모바일 전용 composition (7개 route) | 판별 계약 단일화, 입력 상태·URL 맥락 보존이 구조적으로 성립, 권한·데이터 경로 무변경, 회귀 경계 명확 | `App.tsx` 대형 파일 내 분기 증가 — 화면별 mobile presentation 하위 component 분리로 완화 |
| B | route별 별도 모바일 component tree (mobile 전용 화면 컴포넌트 세트) | 화면 코드 분리가 깔끔 | mode 전환 시 remount로 입력 상태 소실 위험, 권한·데이터 로직 중복과 drift 위험 — interview 불변조건과 충돌 소지 |
| C | CSS 재배치 확장 (현행 접근 연장) | 구현 최소 | 정보 우선순위·단계적 펼침·sticky action 같은 구조 변화를 CSS만으로 달성 불가 — "PC 축소판" 문제 미해소 |

권장안 A는 interview 결정 1-B·2-A·3-A·4-A의 기술적 최소 구현이다. B·C는 기록 목적의 비교 대안이다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic 환경(전용 DB·container)만 사용한다.
- migration 필요 여부: 없음 (Frontend presentation-only slice).
- 외부 발송/실제 데이터 영향: 없음. 실제 Teams/Mail provider 발송 금지 유지.
- runtime 교체 여부: 없음. canonical runtime handover 없음.
- 추가 사용자 승인 필요 작업: push·PR·main merge 미승인. canonical Roadmap 갱신은 실험 밖 별도 승인. 이 실험 결과는 canonical planning·구현 승인이 아니다.

## 14. 검증 계획

- 최소 테스트: `corepack pnpm --dir frontend run lint` / `run typecheck` / `test` / `run build`.
- 단위·컴포넌트(Vitest, matchMedia stub 확장): 중앙 hook의 판별 matrix(≤860px/861px × coarse/fine pointer × hover 유무), mode 전환 시 view·입력 상태 보존, 모바일 홈 우선순위 순서와 section별 독립 실패, 재구성 화면의 mode별 구조·action 노출, 권한별 노출 무변경(Pending 미보유 등), 6슬롯 tab 구성 회귀.
- 영향 영역 회귀: desktop(861px 이상)에서 기존 표·필터·일괄 편집 UX와 loading/empty/error/success·권한·disabled action 무회귀. 기존 full-stack spec(mobile-adaptive-navigation·home-dashboard·pending-list·project-bottleneck·project-registration)과 mock-ui smoke 무회귀.
- E2E(isolated synthetic, `scripts/e2e-full-stack.sh`): desktop·390px·Teams narrow(480px)에서 7개 재구성 route의 page-level horizontal overflow 0, 44px touch target 실측, 하단 tab 6슬롯 label·폭, sticky action과 하단 tab 겹침 없음, resize를 통한 mode 전환 맥락 보존, page별 screenshot 생성.
- PR/CI: 이 실험 branch는 push·PR 미승인이므로 local 검증까지만 수행하고 상태를 보고한다.
- 사용자 검수: page별 synthetic screenshot으로 "PC 축소판이 아닌 모바일 정보 우선순위"와 thumb zone action 배치를 판정한다(자동 검증과 사용자 검수 상태 분리 관리).

## 15. 완료 기준

- 기능/권한/데이터: 중앙 판별 계약으로 mode·touch가 결정되고, 7개 route가 모바일 전용 composition을 제공하며, 신규 API·mutation·migration·권한 변화 0.
- UX: 모바일 첫 화면 우선순위 성립, 재구성 화면의 핵심 action thumb zone 실행, 390px·480px overflow 0·44px target·focus/screen reader/reduced motion 기준 충족, desktop 무회귀.
- 자동 테스트: 14장 최소·영향·E2E 검증 통과, 기존 테스트 무회귀.
- 5종 산출물: implementation report, SOP·User manual(포함 section 가능), Roadmap update(실험 경계 명시), user validation checklist를 `docs/12-task-completion-policy.md` 기준으로 추적.
- 사용자 검수 상태: screenshot 기반 검수 결과 기록 (완료 전에는 `사용자 검수 대기`).
- PR 상태: 적용 없음 (push·PR·merge 미승인, local experiment commit 범위).

중단 조건: 구현 중 신규 API·aggregate·권한·상태 전이·별도 route가 필요해지면 중단하고 재분류한다. mode 전환의 입력 상태 보존이 특정 화면에서 구조적으로 불가능하면 임의 절충하지 않고 blocking으로 보고한다. 문서·구현의 의미 있는 충돌(예: URL·deep link 계약 변경 필요) 발견 시 임의 선택하지 않고 보고한다.

## 16. 미결정 사항

비차단 결정 3건이다. 1번은 interview에서 명시적으로 deferred된 항목이고, 2·3번은 planning 재검증에서 확인된 presentation 세부 정책이다. 각 권장안은 Fable 제안이며 사용자 결정 기록은 Codex가 수행한다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 사용자 수동 "PC 화면으로 보기" toggle 도입과 저장 정책 | A(권장): 이번 실험에서 구현하지 않고 후속 Task로 분리 — 판별 오차 실측 후 저장 위치·유지 기간·초기화 정책을 별도 결정 / B: 이번 범위에 session 한정(비영속) toggle 포함 — 범위·검증 matrix 확대 | 대기 |
| 2 | 861px 이상 터치 기기의 desktop 터치 보정 범위 | A(권장): interactive target 44px 보정과 터치 우선 간격 등 최소 보정만, layout·정보 밀도는 무변경 / B: hover 의존 상호작용의 터치 대체 UI까지 포함 — desktop 회귀 위험 증가 | 대기 |
| 3 | 모바일 목록 화면의 필터·검색 패턴 | A(권장): 프로젝트·Pending 목록은 full-screen filter/search sheet(기존 sheet 접근성 계약 재사용), 내 업무·알림은 기존 간단 필터 유지 / B: 전 화면 inline 필터 유지 — sheet 신규 검증은 없지만 좁은 폭 정보 우선순위가 약해짐 | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 없음.
- Frontend: `frontend/src/App.tsx`(중앙 mode hook/context, 재구성 route presentation 분기, 하위 mobile component 분리), `frontend/src/HomePage.tsx`(모바일 단일 컬럼 우선순위), `frontend/src/PendingPage.tsx`(모바일 composition), `frontend/src/styles.css`(mode·touch affordance 계층, sticky action, 접힘 section, tab label 축약).
- DB/Migration: 없음.
- Tests/Scripts: `frontend/tests/App.test.tsx`(판별 matrix·맥락 보존·구성 테스트), `frontend/e2e/full-stack/`(모바일 재구성 E2E·screenshot), `frontend/e2e/mock-ui/`(선택 smoke).
- Docs: implementation report와 5종 산출물, 실험 경계의 Roadmap update 추적 기록.

## 18. Roadmap 연결

- 선행 Task: 이 branch의 TASK-MOBILE-001(하단 tab shell)·TASK-HOME-001(홈 widget)·007A/007B 실험 구현이 직접 기반이다. canonical main 기준 이들은 Dependency Pending이며, 본 Task 진행은 interview Task Identity Gate의 명시적 재정렬 승인 기록 안의 실험 경계다.
- 후속 Task: 생산관리·구매·자재 dashboard 모바일 재구성(결정 2-A에서 제외), 관리자 모바일 UX(Roadmap 추적 49), 사용자 수동 mode toggle(16장 결정 1), 사진 촬영·업로드(별도 NEW_FEATURE), QR 스캔 랜딩.
- 현재 Go/No-Go: experiment branch 한정 진행. push·PR·main merge는 No-Go(미승인).
- 별도 Task로 분리할 항목: 위 후속 Task 항목 전부와 canonical Roadmap 문구 갱신.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | 실험 branch에서 interview 왕복 없이 Fable 권장안(1-B·2-A·3-A·4-A) 자동 채택, planning부터 연속 진행 지시 | Interview `COMPLETED_CONFIRMED` 기록과 본 planning 작성. push·PR·merge는 미승인 유지 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

승인 후 새 Codex 구현 세션이 사용할 지시문 초안이다. 이 초안 자체는 구현 승인이 아니다.

1. instruction chain gate를 수행하고 `taskType: APPROVED_FEATURE_IMPLEMENTATION`, 현재 experiment branch 기준선을 보고한다.
2. `frontend/src/App.tsx`에 viewport(≤860px) 주신호 + pointer·hover 보조신호의 중앙 layout mode hook/context를 도입하고, 기존 `useIsMobileViewport` 호출부와 `AppMobileNavigation`의 자체 matchMedia를 통합한다. `pathForView`/`initialViewFromLocation`/popstate·Teams deep link·권한 필터는 수정하지 않는다.
3. 홈(`HomePage`)·내 업무·프로젝트 목록·상세·Pending 목록·상세(`PendingPage`)·알림 7개 route를 기존 component 내 mode 분기로 모바일 전용 composition(단일 컬럼 우선순위, 모바일 page header, 접힘 section, sticky primary action, thumb zone action)으로 재구성한다. 별도 mount tree 분리로 입력 상태를 잃지 않게 하고, 신규 조회·mutation·데이터 개념을 추가하지 않는다.
4. 터치 affordance 계층(coarse pointer·hover 불가 시 44px target 보강, 861px 이상 터치 기기는 desktop composition + 16장 결정 2의 승인 범위 보정)과 sticky action·하단 tab·safe-area의 하단 예약을 `frontend/src/styles.css`에 적용한다. 390px 6슬롯 tab의 label 축약과 44px 폭을 보장한다.
5. Vitest에 판별 matrix(viewport×pointer×hover), mode 전환 view·입력 상태 보존, 모바일 홈 우선순위·section 독립 실패, 권한별 노출 무변경 테스트를 추가하고, isolated synthetic full-stack E2E로 desktop·390px·480px overflow 0·44px 실측·mode 전환·page별 screenshot을 생성한다.
6. `corepack pnpm --dir frontend run lint|typecheck|test|build`와 기존 full-stack·mock-ui spec 무회귀를 확인하고, 미실행 항목은 이유와 함께 분리 기록한다.
7. 생산관리·구매·자재·관리자 화면은 회귀 없음만 확인하고 범위 밖 개선은 Finding으로 분리한다. Backend·migration·Persistent UAT·provider·runtime handover에 접근하지 않는다. push·PR·merge는 수행하지 않는다.
8. implementation report와 5종 산출물 상태, 실험 경계의 Roadmap update 추적 항목을 기록하고 고정 10개 항목 완료 보고로 종료한다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 3
