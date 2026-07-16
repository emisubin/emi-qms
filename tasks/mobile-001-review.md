# TASK-MOBILE-001 — 동일 URL 적응형 현장 UX Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/mobile-001-planning.md`
- reviewStatus: `RESOLVED_FOR_EXPERIMENT_IMPLEMENTATION`
- experimentalImplementationApprovalSource: `USER_STANDING_EXPERIMENT_DIRECTIVE`
- canonicalMainApproval: false
- mainMergeApprovalCount: 0/3

## 결론

Fable의 권장안은 현재 Repository와 사용자 문제에 맞다. 이미 같은 URL·URL 기반 view·Teams deep link와 화면별 모바일 카드가 있으므로 새 모바일 앱이나 Home형 요약을 만들 필요가 없다. 이번 실험은 기존 `navigationItems`를 단일 source로 유지하면서 상단 grid를 하단 tab bar와 더보기 sheet로 교체하고, safe-area·focus·touch target·overflow 계약을 보강하는 Frontend-only vertical slice로 구현한다.

## 유지

- 사진 저장·압축·재시도를 미확정 storage 정책과 함께 추정 구현하지 않고 별도 NEW_FEATURE로 분리한다.
- 핵심 tab은 내 업무·프로젝트·Pending(권한 보유 시)·알림으로 구성하고 나머지는 권한 기반 더보기에 둔다.
- 신규 현장 요약 화면을 만들지 않아 `TASK-HOME-001`과 중복을 피한다.
- 기존 URL router, popstate, Teams deep link, API·권한·상태·audit를 변경하지 않는다.
- 861px 이상 desktop sidebar와 화면 구조는 그대로 보존한다.

## 추가

- tab과 더보기 항목은 기존 `navigationItems`에서 파생하고 권한 조건을 복제하지 않는다.
- 더보기는 `aria-modal`만 선언하는 수준을 넘어서 실제 focus containment, 첫 항목 focus, Esc·배경 닫기, trigger focus 복귀와 body scroll lock을 구현한다.
- 더보기 항목이 현재 route이면 더보기 trigger에 `aria-current="page"`를 표시한다.
- 모바일 배지는 시각 숫자뿐 아니라 screen reader가 건수를 읽을 수 있게 대체 텍스트를 제공한다.
- tab bar는 viewport safe-area를 반영하고 문서 하단에 실제 높이만큼 여백을 예약해 마지막 action을 가리지 않는다.

## 보류

- 사진 촬영·client 압축·offline queue·업로드 재시도는 storage·검역·보존·backup·restore 정책 확정 뒤 별도 Task로 기획한다.
- 관리자·설정 화면의 전면 모바일 재설계는 현장 핵심 경로 검수 뒤 후속 UX Task로 둔다.
- Home형 통합 요약과 QR scan landing은 기존 Roadmap Task에 남긴다.

## 제거

- 별도 모바일 URL, 인증, session 또는 persistence.
- tab 전환을 위한 신규 router·API·Backend·migration.
- 실제 기능이 없는 사진 placeholder upload control.
- 현재 mobile grid와 bottom tab bar를 동시에 노출하는 중복 navigation.

## 구현 순서

1. `AppMobileNavigation`을 권한 파생 tab bar와 접근 가능한 더보기 sheet로 교체
2. safe-area·고정 bar·콘텐츠 하단 여백·44px 이상 touch target CSS 적용
3. viewport meta에 `viewport-fit=cover` 추가
4. Vitest로 tab 구성·현재 위치·sheet focus/Esc/복귀·route 전환 검증
5. Frontend 전체 검증과 기존 007A/007B full-stack 회귀
6. 390px·Teams narrow synthetic browser 검수와 screenshot

## Finding과 resolution

| ID | Severity | 상태 | 내용 | Resolution |
| --- | --- | --- | --- | --- |
| `MOBILE-SHEET-FOCUS` | P2 | `RESOLVED` | 기존 dialog pattern은 focus 이동·trap·복귀를 제공하지 않아 modal 선언과 실제 keyboard 동작이 어긋날 수 있음 | 모바일 더보기 component 안에 focus containment·Esc·복귀를 구현하고 unit test 추가 |
| `MOBILE-SAFE-AREA` | P2 | `RESOLVED` | fixed bottom navigation이 iOS/Teams 하단 영역이나 마지막 action을 가릴 수 있음 | `viewport-fit=cover`, safe-area padding과 shell bottom reservation 적용 |
| `MOBILE-PERMISSION-DRIFT` | P2 | `RESOLVED` | core/더보기 메뉴를 별도 목록으로 작성하면 기존 권한 조건과 drift 가능 | 기존 `navigationItems` 한 배열에서 label 기준으로 presentation만 분할 |
| `MOBILE-BADGE-A11Y` | P3 | `RESOLVED` | 기존 badge가 `aria-hidden`이라 모바일 우선순위가 screen reader에 전달되지 않음 | 모바일 button에 visually hidden 건수 텍스트 추가 |
| `MOBILE-PHOTO-BLOCKER` | P2 | `RESOLVED` | 사진 정책 미확정 상태에서 upload 계약을 만들면 보안·보존 정책을 선점 | 이번 Task에서 binary·client upload 모두 제외하고 후속 NEW_FEATURE로 추적 |
| `MOBILE-CORE-TOUCH-TARGET` | P2 | `RESOLVED` | 최초 구현은 하단 내비게이션만 44px 이상을 보장해 현장 핵심 화면의 기존 42px action 계약을 놓침 | 독립 read-only 검증에서 발견 후 ≤860px 일반 action과 Pending 뒤로가기를 44px 이상으로 보정하고 full-stack E2E에 route별 bounding-box 검증 추가 |

## 구현 판정

현재 experiment branch 구현은 `GO`다. 사용자는 Fable 권장안·Codex review·구현을 별도 승인 왕복 없이 진행하도록 명시했다. 이 판정은 push, PR, 대표 repo 또는 main merge 승인이 아니다.
