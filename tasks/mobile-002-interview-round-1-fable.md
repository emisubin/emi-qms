Baseline is refreshed from the current branch: the TASK-MOBILE-001 bottom tab shell, the home widget dashboard experiment and the bottleneck overview are all present; layout mode today is a single `(max-width: 860px)` matchMedia hook plus five 860/861px CSS blocks with no pointer·hover·orientation signal; interview §7 already registers decision 1 (layout mode 판별 방식) as blocking. Below is the requested round 1 artifact.

# TASK-MOBILE-002 — Fable 5 Deep Interview Round 1

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 1

기준선 요약: 현재 branch는 TASK-MOBILE-001의 하단 고정 tab(내 업무·프로젝트·권한 시 Pending·알림)과 더보기 sheet, safe-area·44px touch target을 이미 포함하고, 홈(widget dashboard 실험)·생산관리·구매·자재·관리자 메뉴가 권한 기반 `navigationItems` 한 배열에서 파생된다. layout mode 판별은 `(max-width: 860px)` 단일 viewport 신호(공용 hook 1개 + CSS media query 5블록)뿐이며 pointer·hover·orientation 신호는 사용하지 않는다. 다수 화면은 여전히 PC 정보구조를 축소·재배치한 형태다. 아래 4개 질문은 모두 이 Task의 mode 판별 계약과 모바일 전용 재구성 범위를 확정하는 blocking 결정이다.

### 질문 1 — layout mode 판별 신호를 어떻게 조합할지

- 필요한 이유: interview 문서 7장 결정 1번이 이 항목을 blocking으로 등록했다. 포함 범위는 "viewport·pointer·hover 등 capability 기반 판별"이지만, 신호를 어떻게 조합하느냐에 따라 Teams narrow(좁지만 마우스·hover 가능)와 861px 이상 터치 태블릿의 화면이 달라지고, 기존 desktop·E2E 회귀 경계도 달라진다.
- 답변이 바꾸는 범위: 중앙 layout mode 판별 계약(단일 hook/context), 모바일 composition 적용 조건, 터치 affordance(44px·sticky action·sheet) 적용 조건, resize·orientation 변경 시 재판별 동작, mode 판별 자동 검증 시나리오.
- 선택지 비교:
  1. **A안 — 현행 viewport 단일 신호(≤860px) 유지, 판별만 중앙 hook으로 통합.** 장점: 기존 860px 계약과 E2E 기준선이 그대로 유지되어 회귀 위험이 가장 작다. 단점: 861px 이상 터치 태블릿이 hover 전제 desktop UI를 그대로 받고, 터치 여부와 무관하게 같은 affordance를 적용해 "능력 판별"이라는 이번 Task 목적을 부분적으로만 달성한다.
  2. **B안 — viewport를 주신호(≤860px이면 모바일 composition), pointer·hover를 보조신호로 조합.** coarse pointer·hover 불가면 mode와 무관하게 터치 affordance(44px target, 터치 우선 상호작용)를 강화하고, 861px 이상 터치 태블릿은 desktop composition에 터치 보정만 적용한다. Teams narrow는 폭이 구속 조건이므로 지금처럼 모바일 composition을 받는다. 장점: 기존 860px 경계와 회귀 기준선을 보존하면서 능력 신호를 실질적으로 사용한다. 단점: mode(2종)×터치(2종) 조합이 생겨 검증 matrix가 A안보다 커진다.
  3. **C안 — B안 + 사용자 수동 전환 toggle("PC 화면으로 보기").** 장점: 판별이 틀린 기기에서 사용자가 즉시 벗어날 수 있다. 단점: 전환값의 저장 위치·유지 기간·초기화 정책이라는 새 정책 결정이 필요하고, 두 composition을 모든 화면에서 상호 전환 가능하게 유지해야 하므로 이번 실험 범위가 커진다.
- 권장안: **B안.** 동일 URL·회귀 보존 불변조건과 "capability 기반 판별" 포함 범위를 모두 충족하는 최소 조합이다. 수동 전환 toggle은 비차단 후속 결정으로 deferred 기록을 권장한다. mode는 resize·orientation 변경 시 실시간 재판별하되, 3장 확정대로 동일 업무 맥락(현재 view·입력 상태)을 보존한다.

### 질문 2 — 모바일 전용 재구성을 적용할 화면 범위

- 필요한 이유: "PC 축소판이 아닌 모바일 전용 정보구조"를 모든 route에 적용하면 관리자·일괄 편집 화면까지 재설계 대상이 되어 실험 크기와 검증 대상이 급증한다. 완료 기준(9장)의 E2E·screenshot 대상을 확정하려면 이 경계가 먼저 필요하다.
- 답변이 바꾸는 범위: route별 모바일 component 신규 작성 범위, 자동 검증·screenshot 목록, 잔여 화면의 후속 Task 분리 방식.
- 선택지 비교:
  1. **A안 — 현장 핵심 화면만 모바일 전용 재구성: 홈, 내 업무, 프로젝트 목록·상세, Pending 목록·상세, 알림. 생산관리·구매·자재·관리자는 기존 responsive 수준을 유지하고 회귀 없음만 확인.** 장점: Roadmap 3.4(모바일은 현장 입력·체크 중심, PC는 관리·일괄 편집 중심) 및 관리자 모바일 UX 미확정(추적 49)과 일치하고 Task 크기가 예측 가능하다. 단점: 내 업무에서 deep link로 진입하는 일부 업무 화면(예: 자재 도착 등록)은 기존 축소형 UX로 남는다.
  2. **B안 — A안 + 생산관리·구매·자재 dashboard까지 모바일 전용 재구성.** 장점: 내 업무에서 이어지는 부서 업무 화면까지 일관된 모바일 패턴이 된다. 단점: 표·필터 중심 화면의 재설계가 포함되어 범위가 크게 늘고, 해당 화면들의 desktop 일괄 편집 UX 보존 검증도 함께 커진다.
  3. **C안 — 관리자 포함 전체 route 일괄 재구성.** 장점: 화면별 편차가 사라진다. 단점: 관리자 화면은 Roadmap상 PC 중심이며 모바일 고도화가 미확정 추적 항목이라 확정사항 관리 원칙과 충돌하고 실험 목적 대비 비용이 과다하다.
- 권장안: **A안.** 이번 실험의 목적(모바일 첫 화면에서 우선 업무 판단과 한 손 실행)을 검증하는 데 필요한 최소 route 집합이고, 미재구성 화면은 shell 전역 기준(overflow 0·safe-area·touch target)만 보장한 뒤 후속 범위로 추적한다.

### 질문 3 — 모바일 홈을 어떻게 구성할지

- 필요한 이유: interview 5장은 모바일 Home에서 "긴급·내 업무·담당 프로젝트 우선"을 검토 대상으로 두었고, 현재 branch에는 홈 widget dashboard 실험이 이미 존재한다. 모바일 첫 화면의 데이터 원천과 신규 조회 여부가 부분 실패 처리와 Backend 무변경 경계를 정한다.
- 답변이 바꾸는 범위: 모바일 진입 첫 화면, 홈 화면의 모바일 전용 composition 설계, 신규 조회 조합 여부(제외 범위의 Backend·API 무변경과 직결), section별 부분 실패 표시 설계.
- 선택지 비교:
  1. **A안 — 기존 홈 widget 데이터 원천을 그대로 재사용해 모바일에서는 단일 컬럼 우선순위(긴급·차단 → 내 업무 → 담당 프로젝트 → 나머지 요약)로 재배치.** 장점: 신규 aggregate·API 없이 presentation만 바꿔 제외 범위(Backend 무변경)와 부분 실패 경계를 기존 그대로 유지하고, TASK-HOME-001 계열 실험과 데이터 원천이 일치한다. 단점: 모바일 우선순위가 기존 widget 데이터가 제공하는 범위로 제한된다.
  2. **B안 — 모바일에서는 홈을 노출하지 않고 내 업무를 모바일 시작 화면으로 사용, 홈은 desktop 전용 유지.** 장점: 모바일 홈 설계 비용이 없고 현장 사용자의 첫 행동(내 업무)에 즉시 도달한다. 단점: "오늘 할 일과 차단 이슈를 먼저 본다"는 이 Task의 기대 결과 중 프로젝트·긴급 조망이 사라지고, desktop과 모바일의 진입 구조가 달라져 동일 URL 맥락 보존 검증이 복잡해진다.
- 권장안: **A안.** 이 Task의 성공 기준(모바일 첫 화면에서 우선 업무 판단)을 직접 충족하면서 Backend·API 무변경 제외 범위를 지키는 유일한 조합이다.

### 질문 4 — 하단 tab 구성을 홈 중심으로 개편할지

- 필요한 이유: TASK-MOBILE-001은 핵심 tab을 내 업무·프로젝트·권한 시 Pending·알림 4개로 확정했고 홈은 그 뒤 실험에서 추가되었다. 질문 3에서 모바일 홈을 채택하면 홈의 tab 배치가 thumb zone 진입 구조를 정하고, 390px에서 tab 폭·label 가독성 검증 대상이 달라진다.
- 답변이 바꾸는 범위: 하단 tab 슬롯 구성과 순서, 더보기 sheet 항목, 390px tab 폭·touch target 검증, TASK-MOBILE-001 tab 회귀 테스트 갱신 범위.
- 선택지 비교:
  1. **A안 — 홈 tab을 첫 슬롯에 추가한 5-tab(홈·내 업무·프로젝트·권한 시 Pending·알림) + 더보기.** 장점: 모바일 첫 화면(홈)과 실행 화면(내 업무)을 모두 한 번의 탭으로 오가고 기존 tab의 배지·권한 파생 구조를 그대로 유지한다. 단점: 390px에서 최대 6개 슬롯이 되어 tab당 폭이 줄어들므로 label 축약과 44px 폭 검증이 필요하다.
  2. **B안 — 기존 4-tab 유지, 홈은 더보기 sheet에 배치.** 장점: TASK-MOBILE-001 tab 계약과 검증을 그대로 재사용한다. 단점: 모바일 첫 화면으로 채택한 홈이 두 동작 뒤에 있어 질문 3의 목적과 상충한다.
  3. **C안 — 홈이 알림 슬롯을 대체한 5개 슬롯(홈·내 업무·프로젝트·권한 시 Pending·더보기), 알림은 더보기로 이동.** 장점: 슬롯 수가 늘지 않는다. 단점: 읽지 않은 알림 배지가 하단 bar에서 사라져 참조 알림 인지가 늦어지고, MOBILE-001에서 확정한 핵심 4개 구성을 되돌린다.
- 권장안: **A안.** 홈을 진입 조망, 내 업무를 실행 시작점으로 병립시키는 구조가 이 Task의 정보 우선순위와 가장 일치한다. 390px에서 6슬롯의 label 축약·최소 44px 폭·overflow 0을 완료 기준 E2E에 포함할 것을 전제로 한다.

---

- interviewStatus: QUESTIONS_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
