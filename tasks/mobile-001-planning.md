Baseline revalidation is complete (roadmap queue and TASK-MOBILE-001 entry, interview rounds 1–2, shell navigation/badge/routing code, CSS breakpoints, dialog pattern, vitest/Playwright conventions, viewport meta). Below is the single primary planning draft artifact.

# TASK-MOBILE-001 — 동일 URL 적응형 현장 UX 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/mobile-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 기존 URL·인증·Teams deep link·서버 권한을 유지한 채 390px와 Teams narrow에서 현장 사용자가 내 업무·Pending·프로젝트 핵심 업무를 찾고 원본 업무 화면으로 진입하는 시간을 줄인다.
- 대상 사용자·역할: 현장 업무 사용자(각 부서 담당자), 생산관리, Read-only·System Administrator. 모두 기존 역할·권한 범위를 그대로 사용한다.
- 정상 흐름: 같은 URL 로그인 → 모바일 하단 내비게이션 → 현장 핵심 화면(내 업무·Pending·프로젝트) → 기존 원본 route에서 조회·처리 → 이전 맥락으로 복귀.
- 예외·복구 흐름: 원본 화면의 기존 inline validation·서버 오류를 그대로 사용한다. 한 요약 데이터 실패가 전체 내비게이션을 막지 않고, 네트워크 실패 후 같은 route와 사용 맥락에서 재시도할 수 있다.
- 확정한 정책과 명시적 제외 (interview 결정 1-A·2-A·3-A·4-A):
  - 사진 업로드는 storage·보존·검역·backup 정책이 external blocker(Roadmap 추적 항목 73)로 미확정이므로 binary 저장과 client 업로드 계약을 모두 제외하고 정책 확정을 선행조건으로 하는 별도 NEW_FEATURE Task로 분리한다.
  - 공통 모바일 내비게이션은 하단 고정 tab bar + 권한 기반 "더보기" sheet로 한다.
  - 신규 요약 화면은 만들지 않고 기존 내 업무·Pending·프로젝트 화면을 모바일 우선으로 정비하며, 요약 조망은 TASK-HOME-001에 남긴다.
  - 390px·Teams narrow 완료 기준은 shell 전역 + 현장 핵심 화면(내 업무, Pending 목록·상세, 프로젝트 목록·상세·병목)으로 한정하고 관리자·설정 화면은 회귀 없음만 확인한다.
  - 별도 모바일 URL·별도 인증/session, 공용 태블릿·공용 기기 mode, sessionStorage 강제 정책, 실제 Teams/Mail provider 발송, Persistent UAT migration·write·runtime handover는 제외한다.
- planning으로 넘긴 비차단 미결정 사항: ① 권한별 tab 구성, ② safe-area와 하단 요소 겹침 회피, ③ 더보기 sheet 열림·focus 순서, ④ Roadmap의 TASK-MOBILE-001 "사진 압축·재시도" 문구와 1-A 결정의 정합화. 본 문서 16장에서 권장안과 함께 사용자 결정 항목으로 전달한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

### 재검증한 Repository 기준선 (planning 시점)

- Frontend shell은 `frontend/src/App.tsx` 단일 파일 중심이다. 데스크톱 sidebar(`AppNavigation`)와 860px 이하에서 콘텐츠 상단에 표시되는 2열 grid형 `AppMobileNavigation`이 같은 `navigationItems` 배열을 사용한다.
- `navigationItems`는 권한 조건으로 필터링된 최대 8개 항목(내 업무·프로젝트·Pending·생산관리·구매·자재·알림·관리자)이며, 내 업무(요청 count)와 알림(미읽음 count) 배지가 이미 존재한다. 배지 집계 실패 시 0으로 fallback하고 내비게이션은 계속 동작한다.
- view 상태는 URL pathname 기반(`initialViewFromLocation`/`pathForView`/`popstate`)으로 해석되어 동일 URL·Teams deep link(`/teams/activity` 계열, Teams context 감지) 계약이 성립한다.
- `useIsMobileViewport()`는 `max-width: 860px` matchMedia 기반이며 다수 화면이 카드형 모바일 변형을 개별 구현한다. 430px 이하에서 내비게이션 버튼 최소 높이가 42px로 44px touch target 기준에 미달하고, safe-area(`env(safe-area-inset-*)`) 처리는 없으며 `frontend/index.html` viewport meta에 `viewport-fit=cover`가 없다.
- 재사용 가능한 dialog 패턴(`DialogBackdrop`: `role="dialog"`, `aria-modal`, backdrop 닫기)이 존재한다. 화면 하단에 상시 고정되는 기존 UI 요소는 확인되지 않았다(dialog는 overlay 방식).
- 테스트 기준선: Vitest + Testing Library(`frontend/tests/App.test.tsx`, matchMedia stub 사용 전례 있음), Playwright `frontend/e2e/`(auth-shell, mock-ui smoke, full-stack: project-registration·pending-list·project-bottleneck).
- 이 experiment branch에는 Pending workflow와 프로젝트 병목 표시 실험 구현이 존재한다(`PendingPage`, `ProjectBottleneckOverview`/`ProjectBottleneckBadge`, 관련 full-stack spec). canonical main 기준 선행 Task(TASK-007A·007B)는 Roadmap상 아직 Dependency Pending이며, 이 실험 진행은 interview의 Task Identity Gate에 `explicitRoadmapOverrideApproved: true`로 기록된 experiment 경계 안에서만 유효하다.

## 1. 한 줄 목표

현장 사용자가 390px·Teams narrow에서 동일 URL로 로그인해 하단 고정 내비게이션과 배지로 내 업무·Pending·프로젝트 핵심 화면에 한 손 흐름으로 빠르게 진입하고 이전 맥락으로 복귀할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 PC 중심 화면을 같은 URL에서 모바일로 열 수 있고 일부 카드형 반응형 처리가 있으나, 공통 모바일 내비게이션이 콘텐츠 상단 2열 grid(최대 8개 항목)라 진입 시 콘텐츠가 아래로 밀리고 한 손 엄지 도달 범위를 벗어난다.
- 화면별로 모바일 패턴이 분산되어 있어 핵심 업무·Pending·프로젝트를 오가는 공통 경로와 행동 우선순위가 없고, 좁은 화면에서 touch target 미달(42px)과 safe-area 미처리가 남아 있다.
- 현재 우회 방식: 화면별 메뉴를 열고 긴 목록·상세를 스크롤하며 PC용 정보 밀도 안에서 필요한 action을 찾는다.
- 이 기능이 없으면 현장 입력 Task(자재·제조·검사·물류)가 추가될수록 각 화면이 서로 다른 모바일 패턴과 사진·재시도 정책을 중복 구현할 위험이 커진다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 현장 업무 사용자 | 내 업무·Pending·담당 프로젝트 확인과 원본 action 진입 | 기존 역할별 범위 | 원본 업무의 기존 권한만 |
| 생산관리 | 병목·차단 현황 확인과 후속 화면 이동 | 기존 프로젝트·Pending 범위 | 기존 mutation만 |
| Read-only·System Administrator | 허용된 화면 조회 | 기존 조회 범위 | 없음 (모바일 shell이 새 mutation을 열지 않음) |

모바일 shell은 `navigationItems`의 기존 권한 필터(`Pending.Read`, 자재·관리자 접근 조건 등)를 그대로 재사용하며 권한 확대·축소를 만들지 않는다. 권한 강제는 계속 서버 Policy가 authoritative다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 현장 사용자의 내 업무 처리 (390px)

1. 사용자가 기존 URL로 로그인하면 860px 이하에서 하단 고정 tab bar가 표시된다.
2. 내 업무 tab의 배지(요청 count)를 보고 tab을 눌러 `/my-work`로 이동한다.
3. 업무 항목에서 원본 업무 화면(기존 route)으로 진입해 기존 권한 범위에서 처리한다.
4. 브라우저/기기 뒤로 가기로 이전 목록 맥락에 복귀한다.

### 시나리오 B — 생산관리의 병목 확인

1. 생산관리 사용자가 프로젝트 tab으로 프로젝트 목록을 연다.
2. 목록·상세의 기존 병목 표시에서 차단 원인을 확인한다.
3. 병목의 Pending 연결로 Pending 상세에 진입해 기존 화면에서 조치를 확인한다.

### 시나리오 C — 권한이 적은 역할의 축소 내비게이션

1. `Pending.Read`가 없는 사용자가 로그인하면 Pending tab이 표시되지 않는다.
2. 더보기 sheet에도 권한 없는 항목(자재·관리자 등)은 나타나지 않는다.
3. Read-only 상태에서 shell의 어떤 요소도 새로운 mutation 진입점을 만들지 않는다.

### 시나리오 D — 더보기 sheet와 keyboard 접근

1. 사용자가 더보기 버튼을 누르면(또는 keyboard로 활성화하면) 하단 sheet가 열리고 첫 항목에 focus가 이동한다.
2. 항목 선택·Esc·배경 탭으로 sheet가 닫히고 focus가 더보기 버튼으로 복귀한다.
3. 현재 위치가 sheet 내부 항목이면 더보기 버튼이 활성 상태로 표시된다.

### 시나리오 E — 부분 실패 격리

1. 내 업무/알림 요약 집계가 실패하면 배지는 0으로 표시된다(기존 fallback 유지).
2. tab bar와 화면 전환은 계속 동작하고, 각 화면은 자체 loading/error 상태를 표시한다.

## 5. 기능 요구사항

### 필수

- [ ] 860px 이하에서 하단 고정 tab bar 제공: 핵심 항목 + 더보기 버튼, 현재 위치 `aria-current` 표시, 기존 배지(내 업무·알림) 재사용
- [ ] 더보기 sheet: 나머지 권한 필터링 항목 노출, `role="dialog"`·`aria-modal`, focus 이동·복귀, Esc·배경 닫기
- [ ] 기존 상단 2열 grid 모바일 내비게이션을 tab bar로 대체 (desktop sidebar와 861px 이상 layout은 무변경)
- [ ] safe-area 처리: tab bar `env(safe-area-inset-bottom)` 반영, viewport meta `viewport-fit=cover` 보완, 콘텐츠 하단이 tab bar에 가려지지 않도록 하단 여백 예약
- [ ] 대상 화면(내 업무, Pending 목록·상세, 프로젝트 목록·상세·병목)과 shell 전역에서 390px·Teams narrow page-level horizontal overflow 0
- [ ] 내비게이션·현장 핵심 화면의 touch target 44px 이상 (기존 430px 이하 42px 버튼 보정 포함)
- [ ] URL·view 계약 보존: `pathForView`/`initialViewFromLocation`/popstate 의미 무변경, Teams deep link 경로 무변경

### 선택

- [ ] 현장 핵심 화면의 기존 카드형 변형에서 주요 action의 시인성·순서 미세 정비 (신규 데이터·mutation 없이 presentation만)
- [ ] mock-ui smoke에 390px·Teams narrow overflow 검증 spec 추가

### 명시적 제외

- [ ] 사진 촬영·업로드·압축·재시도 (binary 저장과 client 계약 모두 — 별도 NEW_FEATURE로 분리)
- [ ] 신규 Home형 요약 화면 (TASK-HOME-001 범위)
- [ ] 별도 모바일 URL·별도 인증/session, 공용 태블릿·공용 기기 mode, sessionStorage 강제 정책
- [ ] 실제 Teams/Mail provider 발송, Persistent UAT migration·write·runtime handover
- [ ] 관리자·설정 화면의 모바일 재설계 (회귀 없음만 확인)
- [ ] 신규 API·DB·권한·상태 전이

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 공통 shell (≤860px) | 모든 기존 route | 하단 tab bar(핵심 항목+배지+현재 위치), 더보기 버튼 | tab 전환, 더보기 열기 | 배지 실패 시 0 표시, 화면 전환은 유지 |
| 더보기 sheet | tab bar 더보기 버튼 | 나머지 권한 항목 목록 | 항목 선택, Esc/배경 닫기 | 선택 즉시 이동·닫힘, focus 복귀 |
| 내 업무 | tab bar | 기존 내 업무 목록(카드형) | 업무 열기 → 원본 route | 기존 loading/empty/error 유지 |
| Pending 목록·상세 | tab bar(권한 보유 시)·병목 연결 | 기존 Pending 정보 | 상세 진입, 기존 조치 흐름 | 기존 화면 피드백 유지 |
| 프로젝트 목록·상세·병목 | tab bar | 기존 목록·병목 표시 | 상세·병목 원본 진입 | 기존 화면 피드백 유지 |

확인할 UX 항목:

- 현재 위치가 tab bar에서 항상 보이는가 (`aria-current`·활성 스타일, sheet 내부 항목이면 더보기 활성).
- 다음 행동이 명확한가 — 핵심 화면의 1차 action이 390px에서 스크롤 없이 또는 최소 스크롤로 도달 가능한가.
- 저장·변경 결과는 기존 원본 화면의 action 근처 피드백을 그대로 사용한다.
- 권한 부족·검수 전용(ReviewSafe)·오류 상태의 기존 표시가 tab bar와 겹치지 않는가.
- 좁은 화면(390px·Teams narrow)에서 keyboard focus 순서와 44px touch target이 성립하는가.

## 7. 업무 규칙과 불변조건

- 동일 URL·기존 인증·Teams deep link·서버 권한·18단계·Pending·병목 계약을 변경하지 않는다.
- 모바일 shell은 새로운 mutation source가 아니며 원본 권한·상태·audit보다 넓은 정보나 count를 노출하지 않는다. 배지는 기존 내 업무 요청 count·미읽음 알림 count 집계만 재사용한다.
- 한 요약 데이터 실패가 전체 내비게이션을 막지 않으며 거짓 완료나 권한 밖 count를 표시하지 않는다.
- 861px 이상 desktop layout·동작은 회귀 없이 보존한다.
- migration 없는 Frontend/common-contract slice로 유지한다.
- mobile-specific presentation을 제거해도 기존 desktop route·API·data는 보존된다(rollback 안전).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 내 업무·알림 배지 집계 | shell badge 상태 (요청 count, 미읽음 count) | 기존 재사용 | 변경 없음 |
| project·work item·Pending·병목 aggregate | 화면 데이터 | 기존 재사용 | 변경 없음 |
| view 상태 | URL pathname 기반 화면 상태 | 기존 재사용 | 변경 없음 |
| 더보기 sheet 열림 상태 | shell의 일시적 UI 상태 (비영속) | 신규 (client memory only) | 저장·감사 대상 아님 |

```text
(신규 업무 상태 전이 없음 — 원본 업무 상태만 사용)
sheet: 닫힘 → 열림 → (항목 선택 | Esc | 배경) → 닫힘
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 모든 권한·mutation 차단·업무 불변조건 — 이번 Task는 Backend를 변경하지 않고 기존 계약을 그대로 사용한다.
- 필요한 조회와 mutation: 신규 없음. 기존 내 업무 요약·알림 요약·화면별 조회만 사용한다.
- 권한·validation: 신규 없음. shell 노출은 기존 권한 필터 재사용, 강제는 기존 서버 Policy 유지.
- transaction·동시성·idempotency: 영향 없음 (mutation 무변경).
- audit trail: 영향 없음 (새 record 없음).
- 외부 provider 영향: 없음 (실제 발송 금지 유지).

## 10. Frontend 고려사항

- route/component: `frontend/src/App.tsx`의 `AppMobileNavigation`을 하단 고정 tab bar + 더보기 sheet 구조로 교체하고, `navigationItems` 단일 배열에서 핵심 tab과 더보기 항목을 파생한다(권한 로직 단일 유지). sheet는 기존 `DialogBackdrop` 패턴의 접근성 계약을 따른다. route 계약(`pathForView` 등)은 무변경.
- loading/empty/error/success: 화면별 기존 상태를 유지하고, shell 배지 실패는 0 fallback으로 격리한다.
- 공통 Action Feedback: 기존 원본 화면 계약 유지. shell은 새 feedback 채널을 만들지 않는다.
- 접근성: `aria-current`, sheet `role="dialog"`·`aria-modal`·focus 이동/복귀·Esc, 더보기 버튼 `aria-expanded`, 색 외 text label, 배지의 스크린리더 안내(현행 `aria-hidden` 배지에 대한 대체 텍스트 검토).
- 390px/mobile/narrow pane: breakpoint는 기존 860px matchMedia 기준을 재사용한다. tab bar 버튼 최소 높이 48px(44px 이상), 430px 이하 42px 보정, `env(safe-area-inset-bottom)` + `viewport-fit=cover`, 콘텐츠 하단 여백 예약, 대상 route page-level horizontal overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 기존 내 업무·알림 배지와 화면, 프로젝트 목록·상세·병목 표시, Pending 목록·상세를 진입점으로 연결만 한다.
- 권한/관리자: 기존 권한 필터 재사용. 관리자 화면은 더보기 sheet 항목으로만 노출하고 재설계하지 않는다.
- Excel/PDF/첨부: 변경 없음. 사진·첨부는 명시적 제외.
- Teams/Mail: Teams deep link·narrow pane 표시 계약 보존, 발송 변경 없음.
- 삭제·복구/감사: 변경 없음.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 하단 고정 tab bar + 권한 기반 더보기 sheet, 기존 화면 모바일 우선 정비, shell 전역+현장 핵심 화면 완료 기준 | 한 손 도달·현재 위치·배지 노출 최적, presentation-only로 URL·권한 계약 무변경, 실패 격리 단순 | safe-area·겹침·focus 관리 신규 검증 필요 |
| B | topbar hamburger drawer로 전체 메뉴 이동 | 구조 변경 최소, 항목 수 제한 없음 | 매 전환마다 열기 동작 추가, 현재 위치·배지가 접혀 보이지 않음 |
| C | 현행 상단 grid 유지 + 축약 보정 | 변경 최소 | 콘텐츠 밀림·엄지 도달 문제 미해소, Task 목적 달성 약함 |
| D | 신규 현장 요약 화면 추가 | 조망 한 화면 | TASK-HOME-001과 목적 중복, 신규 조회·부분 실패 경계 확대 |

권장안 A는 interview 결정 1-A·2-A·3-A·4-A로 이미 사용자 채택이 기록되었다(실험 사전 지시에 따른 권장안 채택). B·C·D는 기록 목적의 비교 대안이다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic 환경만 사용하며 Persistent UAT DB·runtime을 읽기 포함 변경하지 않는다.
- migration 필요 여부: 없음 (Frontend-only slice).
- 외부 발송/실제 데이터 영향: 없음. 실제 Teams/Mail provider 발송 금지 유지.
- runtime 교체 여부: 없음. canonical runtime handover 없음.
- 추가 사용자 승인 필요 작업: push·PR·main merge는 미승인(merge 승인 `0/3`). canonical main의 Roadmap 문구 갱신은 이 실험 branch 밖 작업으로 별도 승인 필요(16장 항목 4). 이 experiment 결과는 canonical main의 planning·구현 승인이 아니다.

## 14. 검증 계획

- 최소 테스트: `corepack pnpm --dir frontend run lint` / `run typecheck` / `test` / `run build`.
- 단위·컴포넌트(Vitest, 기존 matchMedia stub 패턴 재사용): 권한별 tab·더보기 구성(Pending 미보유 시 미노출, 관리자 조건 노출), 배지 표시와 실패 시 0 fallback, `aria-current` 현재 위치, 더보기 sheet 열림/닫힘·focus 이동·복귀·Esc, tab 전환 시 URL push와 뒤로 가기 맥락 복귀, Teams deep link 초기 view 회귀.
- 영향 영역 회귀(사용자 UX 변경): desktop(861px 이상)과 390px에서 loading/empty/error/success·권한·disabled action, 기존 mock-ui smoke·full-stack spec(project-registration·pending-list·project-bottleneck) 무회귀.
- overflow·narrow 검증: isolated synthetic 환경의 Playwright에서 390px과 Teams narrow 근사 viewport로 대상 route(내 업무, Pending 목록·상세, 프로젝트 목록·상세)의 page-level horizontal overflow 0, tab bar 표시·전환을 확인한다.
- PR/CI: 이 실험 branch는 push·PR 미승인이므로 local 검증까지만 수행하고 상태를 보고한다.
- 사용자 검수: synthetic screenshot으로 하단 내비게이션·배지·더보기 sheet·현장 핵심 화면의 가독성과 action 진입을 확인한다(자동 검증 완료와 사용자 검수 상태는 분리 관리).

## 15. 완료 기준

- 기능/권한/데이터: 860px 이하 전 route에서 tab bar 동작, 권한 필터 무변경, 신규 API·mutation·migration 0.
- UX: 390px·Teams narrow에서 내 업무·Pending·프로젝트 진입이 하단 내비게이션으로 가능하고 현재 위치·배지가 보이며, 대상 화면 page-level horizontal overflow 0, touch target 44px 이상, sheet keyboard 접근 성립.
- 자동 테스트: 위 14장 최소·영향 검증 통과, 기존 테스트 무회귀.
- 5종 산출물: implementation report(경로·상태), SOP·User manual(해당 시 포함 section), Roadmap update(실험 경계 명시), user validation checklist를 `docs/12-task-completion-policy.md` 기준으로 추적.
- 사용자 검수 상태: synthetic screenshot 기반 검수 결과 기록 (완료 전에는 `사용자 검수 대기`).
- PR 상태: 적용 없음 (push·PR·merge 미승인, local experiment commit 범위).

중단 조건: 구현 중 신규 API·권한·상태 전이·별도 route가 필요해지면 중단하고 재분류한다. 사진 업로드 능력이 범위에 다시 필요해지면 중단하고 별도 Task로 보고한다. 문서와 구현의 의미 있는 충돌(예: URL·deep link 계약 변경 필요)이 발견되면 임의 선택하지 않고 blocking으로 보고한다.

## 16. 미결정 사항

Interview에서 명시적으로 deferred된 비차단 결정 4건이다. 각 권장안은 Fable 제안이며, 실험 사전 지시(권장안 자동 채택)의 기록은 Codex가 수행한다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | tab bar 핵심 항목 구성과 권한별 축소 배치 | A(권장): 내 업무·프로젝트·Pending(권한 보유 시)·알림 4개 + 더보기. `Pending.Read` 미보유 시 3개 tab + 더보기로 자동 축소, 더보기 항목이 0개면 더보기 버튼 숨김 / B: 역할별 고정 preset 별도 정의(관리 비용 증가) | 대기 |
| 2 | safe-area와 하단 겹침 회피 방식 | A(권장): tab bar에 `env(safe-area-inset-bottom)` padding + `viewport-fit=cover` 보완 + 콘텐츠 하단 여백(tab bar 높이만큼) 예약, dialog overlay는 tab bar보다 위 z-index로 유지 / B: tab bar를 스크롤 시 숨김 처리(구현·접근성 복잡도 증가) | 대기 |
| 3 | 더보기 sheet 열림·focus 규칙 | A(권장): 하단 sheet, 열림 시 첫 항목 focus, 항목 선택·Esc·배경으로 닫힘, trigger 버튼 focus 복귀, `aria-expanded` 표시 / B: 전체 화면 메뉴 page로 전환(뒤로 가기 history 오염 위험) | 대기 |
| 4 | Roadmap "사진 압축·재시도" 문구와 1-A 결정의 정합화 | A(권장): canonical main Roadmap 갱신은 이 실험 범위 밖이므로 차이를 implementation report와 Roadmap update 추적 항목으로 기록하고, main 반영은 별도 승인 시 Decision Log 누적 방식으로 수행 / B: 이번 실험 branch에서 Roadmap 문서를 직접 수정(실험/canonical 경계 혼선 위험) | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 없음.
- Frontend: `frontend/src/App.tsx`(shell 내비게이션·sheet·현장 핵심 화면 presentation 정비), `frontend/src/styles.css`(tab bar·safe-area·touch target·overflow), `frontend/index.html`(viewport meta).
- DB/Migration: 없음.
- Tests/Scripts: `frontend/tests/App.test.tsx`(shell·권한·focus·route 테스트), `frontend/e2e/mock-ui/`(390px·Teams narrow overflow smoke, 선택).
- Docs: implementation report와 5종 산출물, Roadmap update 추적 기록(16장 항목 4 결정에 따름).

## 18. Roadmap 연결

- 선행 Task: TASK-007A(Pending List)·TASK-007B(병목 집계) — canonical main 기준 Dependency Pending. 이 experiment branch에는 해당 실험 구현이 존재하며, 본 Task는 그 위의 presentation slice다. 사진 storage 정책(추적 항목 73)은 external blocker로 이번 범위에서 분리했다.
- 후속 Task: 사진 촬영·업로드·압축·재시도 별도 NEW_FEATURE(storage·보존·검역·backup 정책 확정 선행), TASK-HOME-001(요약 조망), QR 스캔 랜딩.
- 현재 Go/No-Go: experiment worktree 한정 진행 승인(`experimentalImplementationApproved: true`). push·PR·main merge는 No-Go(승인 `0/3`).
- 별도 Task로 분리할 항목: 사진 업로드 전체, 관리자·설정 화면 모바일 재설계, Roadmap canonical 문구 갱신(별도 승인).

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | 실험 branch에서 interview 왕복 없이 Fable 권장안 자동 채택, planning·review·구현·검증·screenshot 연속 진행 | Interview 결정 1-A·2-A·3-A·4-A 채택 기록, 본 planning 작성. push·PR·merge는 미승인 유지 |

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
2. `frontend/src/App.tsx`의 `AppMobileNavigation`을 하단 고정 tab bar + 더보기 sheet로 교체한다. tab·sheet 항목은 기존 `navigationItems` 배열에서 파생하고 권한 로직을 복제하지 않는다. `pathForView`/`initialViewFromLocation`/popstate와 Teams deep link 경로는 수정하지 않는다.
3. `frontend/src/styles.css`에서 860px 이하 tab bar 스타일, `env(safe-area-inset-bottom)`, 콘텐츠 하단 여백, 44px 이상 touch target(430px 이하 42px 보정 포함)을 적용하고, `frontend/index.html` viewport meta에 `viewport-fit=cover`를 보완한다.
4. 내 업무, Pending 목록·상세, 프로젝트 목록·상세·병목 화면의 기존 카드형 변형에서 390px·Teams narrow page-level horizontal overflow 0과 핵심 action 시인성을 presentation 범위에서 정비한다. 신규 조회·mutation·데이터 개념을 추가하지 않는다.
5. Vitest에 권한별 내비게이션 구성, 배지 fallback, sheet focus·Esc·복귀, route push·뒤로 가기 맥락, Teams deep link 회귀 테스트를 추가하고, isolated synthetic 환경에서 390px·Teams narrow overflow 검증과 screenshot을 생성한다.
6. `corepack pnpm --dir frontend run lint|typecheck|test|build`와 기존 e2e spec 무회귀를 확인하고, 미실행 항목은 이유와 함께 분리 기록한다.
7. 관리자·설정 화면은 회귀 없음만 확인하고 범위 밖 개선은 Finding으로 분리한다. 사진·provider·Persistent UAT·migration·runtime handover에 접근하지 않는다. push·PR·merge는 수행하지 않는다.
8. implementation report와 5종 산출물 상태, Roadmap update 추적 항목(사진 문구 정합화 포함)을 기록하고 고정 10개 항목 완료 보고로 종료한다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 4
