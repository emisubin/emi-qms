Baseline verification is complete: the single 860px viewport hook (`useIsMobileViewport`), the `navigationItems`-derived bottom navigation, and the home widget dashboard experiment are all present on this branch, matching the round 1 baseline claims. The canonical interview now records all four round-1 decisions as adopted (1-B, 2-A, 3-A, 4-A) with `Blocking: No`, `openBlockingDecisionCount: 0`, and one explicitly deferred non-blocking decision. All nine interview dimensions (problem, roles/permissions, flows, data lifecycle, audit, UX/accessibility/narrow, integration, UAT/rollout/rollback, success criteria) are filled, so round 2 is a confirmation summary, not further questions.

---

# TASK-MOBILE-002 — Fable 5 Deep Interview Round 2 (확인용 요약)

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 2

기준선 재확인: 현재 branch의 layout mode 판별은 여전히 `(max-width: 860px)` 단일 viewport matchMedia hook 하나와 다수 화면의 개별 `isMobile` 사용, 860/861px CSS media query로 구성되어 있고 pointer·hover·orientation 신호는 없다. TASK-MOBILE-001의 하단 tab shell(권한 파생 `navigationItems` 단일 배열), 홈 widget dashboard 실험, 병목 상태 실험이 모두 존재한다. Round 1의 4개 blocking 결정은 interview 문서 7장에 사용자 결정으로 기록되었고, 채택된 결정과 현재 구현 기준선 사이에 새 충돌은 발견되지 않았다. 추가 blocking 질문은 없으며 아래 요약의 사용자 확인만 남았다.

## 확인용 요약

**해결할 문제.** 현재 모바일 화면 다수가 PC 정보구조를 축소·재배치한 형태라서, 모바일이 최우선인 현장 사용자가 한 손으로 다음 행동을 빠르게 판단·실행하기 어렵다. 이를 현장 행동 중심의 모바일 전용 composition으로 교체한다.

**확정한 정책 (Round 1 결정 4건).**

1. **Layout mode 판별 — B안 채택.** viewport가 주신호다. 860px 이하는 모바일 composition, 861px 이상은 desktop composition을 받는다. pointer·hover는 보조신호로, coarse pointer이거나 hover가 불가하면 mode와 무관하게 터치 affordance(44px target, 터치 우선 상호작용)를 강화하고, 861px 이상 터치 화면은 desktop composition에 터치 보정만 적용한다. Teams narrow는 폭 기준으로 모바일 composition을 받는다. resize·orientation 변경 시 실시간 재판별하되 현재 view·입력 상태 등 업무 맥락을 보존한다. 판별은 중앙 hook/context 하나로 통합한다.
2. **재구성 화면 범위 — A안 채택.** 홈, 내 업무, 프로젝트 목록·상세, Pending 목록·상세, 알림의 7개 현장 핵심 route만 모바일 전용 재구성한다. 생산관리·구매·자재·관리자 화면은 기존 responsive 수준을 유지하고 shell 전역 기준(page-level overflow 0, safe-area, 44px touch target)과 회귀 없음만 보장한다.
3. **모바일 홈 — A안 채택.** 기존 홈 widget 데이터 원천을 그대로 재사용해 단일 컬럼 우선순위(긴급·차단 → 내 업무 → 담당 프로젝트 → 나머지 요약)로 재배치한다. 신규 aggregate·API 없음. section별 부분 실패는 기존 경계대로 독립 표시한다.
4. **하단 tab — A안 채택.** 홈 tab을 첫 슬롯에 추가한 5-tab(홈·내 업무·프로젝트·권한 시 Pending·알림)+더보기 구성. 390px에서 최대 6슬롯의 label 축약, 최소 44px 폭, overflow 0을 완료 기준 E2E에 포함한다.

**보존 불변조건.** 동일 URL·인증/session, 서버 권한 강제와 18단계 workflow, 기존 API·mutation·deep link·audit, desktop 관리·조회·일괄 편집 UX, 대표 repo·GitHub `main` 무변경. 모바일 mode가 권한을 확대하거나 우회 action을 만들지 않는다.

**명시적 제외.** 별도 모바일 URL·앱·인증·session, Backend·API·DB·migration·권한·업무 상태 변경, 사진 저장·압축·offline queue·업로드 재시도, Persistent UAT write·runtime handover·실제 provider 발송, push·PR·merge.

**Deferred 비차단 결정 (planning 사용자 결정 항목으로 전달).** 판별 오차 대비 사용자 수동 "PC 화면으로 보기" toggle의 도입 여부와 저장 위치·유지 기간·초기화 정책.

**검증과 성공 기준.** unit·typecheck·lint·build, desktop·390px·Teams narrow(480px) synthetic isolated E2E, overflow·touch target·focus·mode 판별 자동 검증, page별 screenshot으로 "PC 축소판이 아닌 모바일 정보 우선순위"를 사용자 검수. Desktop composition과 mode 전환 회귀를 함께 확인한다.

**사용자 확인 요청.** interview 문서 10장의 확인 항목 6개(업무 문제, 역할·권한, 정상·예외·복구 흐름, 포함·제외 범위, blocking 결정 없음, 이 요약을 planning 입력으로 사용)에 동의하면 interview를 `COMPLETED_CONFIRMED`, `userConfirmed: true`로 전환하고 planning 단계로 진행한다. 수정할 항목이 있으면 해당 결정 번호와 함께 회신한다.

---

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
