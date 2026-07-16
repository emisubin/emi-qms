# TASK-MOBILE-002 — 모바일 우선 적응형 화면 체계 Codex 내용 Review

> Review 대상: `tasks/mobile-002-planning.md` Fable 5 원문
> Review 성격: 제품 방향·사용자 가치·범위·구현 경계 1회 검토
> 결과: 조건부 승인 — 아래 resolution을 구현 계약에 추가

## 1. 총평

Fable의 기본 방향은 유지한다. User-Agent나 OS 이름을 추측하지 않고 viewport를 구조 선택의 주신호로, pointer·hover capability를 터치 보정의 보조신호로 쓰는 방식은 같은 URL·권한·API를 보존하면서 모바일을 별도 제품 경험으로 구성하는 최소안이다. 홈·내 업무·프로젝트 목록·상세·Pending 목록·상세·알림을 1차 vertical slice로 선정한 것도 실제 현장 사용 가치가 높다.

다만 content 7개 route만 바꾸고 현재 PC형 topbar·system strip을 유지하면 사용자가 계속 "PC 화면을 줄인 느낌"을 받는다. 전역 모바일 shell과 권한별 action parity를 필수 계약에 추가해야 구현 승인이 가능하다. 요청된 `GPT 5.6 Sol` model selector는 현재 환경에 제공되지 않아 해당 모델을 사용했다고 주장하지 않는다. 대신 별도 read-only reviewer session이 Fable 원문과 Repository를 독립 검토했고 본 review에 Finding을 반영했다.

## 2. 기능 판단

### 유지

- `≤860px = mobile composition`, `≥861px = desktop composition`의 구조 경계
- viewport와 pointer·hover capability 분리, UA sniffing 금지
- 같은 URL·인증·session·deep link·API·권한·audit 보존
- 모바일 Home의 `긴급·차단 → 내 업무 → 프로젝트 → 기타` 정보 우선순위
- 홈·내 업무·프로젝트 목록·상세·Pending 목록·상세·알림 7개 route 우선 구현
- 기존 권한 기반 `navigationItems`와 서버 Policy 재사용
- Desktop 관리·표·일괄 편집 UX 보존

### 추가

- PC형 topbar와 system strip을 모바일에서는 compact app bar와 상태 sheet로 재구성한다. ReviewSafe·검수 계정·로그아웃·API/DB/User 상태를 숨기지 않고 mobile status surface로 이동한다.
- 중앙 provider 결과를 `data-layout-mode="mobile|desktop"`, `data-touch-optimized="true|false"`로 shell DOM에 전달해 React 구조와 CSS가 같은 source를 사용한다.
- `pointer: coarse`뿐 아니라 `any-pointer: coarse`, `hover: none`을 touch 보정 신호에 포함한다.
- Project·Pending 상세에서 desktop 권한으로 가능한 action을 모바일에서도 잃지 않는다. 대표 action은 thumb zone에 두고 나머지는 mobile action group에 유지한다.
- Project·Pending full-screen filter sheet는 draft state, 적용, 취소 시 복원, 초기화, focus 이동·trap·trigger 복귀와 `100dvh`를 계약으로 한다.
- desktop/mobile 권한별 action parity, 390px phone, 480px Teams narrow, 1024px coarse-pointer desktop 조합을 검증한다.

### 보류

- 사용자 수동 `PC 화면으로 보기` toggle과 저장·유지·초기화 정책
- 생산관리·구매·자재·관리자 route의 모바일 전용 재구성
- 사진·offline queue·업로드 재시도
- 별도 모바일 URL·앱·인증
- Backend·DB·migration 변경

### 제거

- User-Agent 문자열 기반 기기 판별
- 모바일·desktop별 데이터 조회 또는 권한 로직 복제
- CSS로 PC section 순서만 세로 재배치하는 접근
- mode 전환 시 stateful 화면 전체를 remount하는 구조

## 3. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `M2-GLOBAL-SHELL` | P1 | `RESOLVED` | PC형 topbar·system strip이 남으면 "PC 축소판" 문제를 해소하지 못함 | compact mobile app bar와 status sheet를 필수 구현 계약에 추가 |
| `M2-ACTION-PARITY` | P2 | `RESOLVED` | 대표 sticky action만 강조하면 수정·상태 변경·삭제 등 기존 action 유실 가능 | 역할별 desktop/mobile action parity unit·E2E를 완료 기준에 추가 |
| `M2-HYBRID-TOUCH` | P2 | `RESOLVED` | `pointer: coarse`만으로 hybrid touch 장치를 놓칠 수 있음 | `any-pointer: coarse`와 `hover: none`을 중앙 touch 판별에 포함 |
| `M2-BREAKPOINT-DRIFT` | P2 | `RESOLVED` | React hook과 CSS media block이 다른 구조를 선택할 위험 | 중앙 provider의 DOM data attribute를 structural source로 사용 |
| `M2-SCOPE-LABEL` | P2 | `RESOLVED` | 사용자 표현은 전체 모바일 재구성이지만 Fable 범위는 핵심 7개 route | 이번 결과를 `모바일 우선 1차 vertical slice`로 명시하고 잔여 route를 후속 추적 |
| `M2-FILTER-SHEET` | P2 | `RESOLVED` | full-screen filter의 적용·취소·focus semantics가 없음 | draft→적용, 취소 복원, 초기화, focus trap·복귀, `100dvh`를 구현 계약에 추가 |
| `M2-APP-MODULE-SIZE` | P3 | `BACKLOG` | 13k line `App.tsx`에 분기를 계속 추가하면 유지보수 부담 증가 | adaptive provider와 공통 mobile component를 별도 파일로 분리 |

Planning 품질 기준 Open P0/P1/P2는 `0/0/0`이다. P3는 별도 component 분리로 이번 구현에서 가능한 만큼 줄이고, 대규모 App 분할은 후속 housekeeping 후보로 유지한다.

## 4. 채택한 비차단 결정

| 번호 | Fable 권장안 | Review resolution |
| ---: | --- | --- |
| 1 | 수동 mode toggle은 후속 분리 | 채택. 이번 구현에 영속·session toggle 없음 |
| 2 | 861px 이상 touch 기기는 44px target·간격 최소 보정 | 채택. 정보 밀도와 desktop layout은 무변경 |
| 3 | Project·Pending에 full-screen filter/search sheet | 채택. 공통 accessible mobile filter component로 구현 |

## 5. 승인 가능한 최소 구현 계약

1. 중앙 provider 하나가 `layoutMode`와 `touchOptimized`를 계산한다. 구조 mode는 viewport, touch 보정은 `any-pointer`·`pointer`·`hover`를 사용하고 UA sniffing·기기 정보 저장을 금지한다.
2. 모바일 전역 app bar/status surface와 핵심 7개 route를 PC 축소판이 아닌 별도 정보 우선순위로 구현한다.
3. 모바일에서도 현재 역할이 desktop에서 사용할 수 있는 기존 action을 잃지 않는다. 서버 권한은 기존 Policy가 계속 강제한다.
4. mode 전환 중 URL, 입력값, 선택 filter, loading/error state를 보존한다.
5. Desktop 861px 이상 UI와 표·일괄 편집 구조는 변경하지 않는다.
6. 390px phone, 480px Teams narrow, 1024px coarse-pointer desktop, desktop 1440px을 검증한다.
7. touch target 44px, safe-area, `dvh`, focus, screen reader, reduced motion, sticky action과 bottom tab 비중첩을 검증한다.
8. 모바일 7개 route와 desktop 대응 화면 screenshot을 생성하고 잔여 route를 완료로 주장하지 않는다.

## 6. 권장 구현 순서

1. `AdaptiveLayoutProvider`와 공통 mobile shell primitives
2. compact mobile app bar/status sheet와 하단 navigation 통합
3. 모바일 Home 우선순위
4. 내 업무·알림 action card
5. Project·Pending 공통 filter sheet와 목록
6. Project·Pending 상세의 next action·section·action parity
7. unit·E2E·desktop 회귀·screenshot·독립 검증
