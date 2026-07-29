Baseline is established: the shell already has a desktop sidebar plus an inline 2-column mobile nav block at ≤860px, URL-path-based view routing with Teams deep-link parsing, per-page `useIsMobileViewport()` card variants, and the photo storage policy remains an external blocker (Roadmap tracking item 73). Below is the requested round 1 artifact.

# TASK-MOBILE-001 — Fable 5 Deep Interview Round 1

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 1

기준선 요약: 현재 shell은 860px 이하에서 상단 2열 grid 형태의 공통 모바일 내비게이션을 콘텐츠 위에 표시하고, view 상태는 URL pathname 기반으로 해석되어 동일 URL·Teams deep link 계약이 이미 성립한다. 다수 화면이 `useIsMobileViewport()` 기반 카드형 모바일 변형을 개별 구현하고 있으며, 사진 storage·압축·재시도 정책은 Roadmap 추적 항목의 external blocker로 미확정이다. 아래 4개 질문은 모두 이 Task의 범위 경계를 확정하는 blocking 결정이다.

### 질문 1 — 사진 업로드 능력을 이번 Task에 포함할지, blocker 해소 후 별도 Task로 분리할지

- 필요한 이유: Roadmap의 TASK-MOBILE-001 포함 범위에는 "사진 압축·재시도"가 있으나, 첨부 storage·보안·backup·보존 정책은 미확정 external blocker다. 이 결정이 이번 Task의 최대 범위 경계를 정한다.
- 답변이 바꾸는 범위: 구현 범위, migration·storage 필요 여부, 테스트 계획(업로드 실패·재시도 검증 포함 여부), 후속 Task 분리 여부.
- 선택지 비교:
  1. **A안 — 사진 제외, 공통 UX 기반만 구현 후 사진은 정책 확정 뒤 별도 Task로 분리.** 장점: blocker 미확정 상태에서 binary 저장을 만들지 않아 정책 역행 위험이 없고, 이번 Task가 migration 없는 Frontend/common-contract slice로 유지된다. 단점: "현장 입력·사진 업로드 기반 제공"이라는 Roadmap 목적 중 사진 부분이 후속으로 이월된다.
  2. **B안 — 실제 저장 없이 client 압축·재시도 계약(공통 component 계약)까지만 이번 Task에 포함.** 장점: 후속 사진 Task의 UI 계약을 선반영한다. 단점: 실제 storage 정책이 압축 포맷·크기 상한을 바꿀 수 있어 재작업 위험이 있고, 저장 불가능한 UI가 미완성 기능으로 노출될 수 있다.
  3. **C안 — 임시 storage를 정해 실제 업로드까지 구현.** 장점: 목적을 한 번에 달성한다. 단점: 미확정 정책(보존·검역·backup·restore)을 임의 확정하게 되어 Roadmap 확정사항 관리 원칙과 충돌하고, 인터뷰 문서의 명시적 제외 항목("정책 미확정 상태의 binary 사진 저장")과도 충돌한다.
- 권장안: **A안.** 인터뷰 문서의 제외 후보와 Roadmap blocker 상태에 부합하며, 사진 업로드는 storage 정책 확정을 선행조건으로 하는 별도 NEW_FEATURE Task로 분리한다.

### 질문 2 — 공통 모바일 내비게이션 패턴 선택

- 필요한 이유: 현재 모바일 내비게이션은 콘텐츠 상단의 2열 grid 블록(최대 8개 항목)이라 진입 시 콘텐츠가 아래로 밀리고, 한 손 사용 시 엄지 도달 범위 밖이다. "현장 한 손 흐름"이라는 기대 결과에 직접 영향을 준다.
- 답변이 바꾸는 범위: shell 구조, safe-area 처리, 접근성(keyboard focus·44px touch target) 검증 대상, 각 화면의 세로 공간 예산.
- 선택지 비교:
  1. **A안 — 하단 고정 tab bar(내 업무·Pending·프로젝트·알림 등 핵심 4~5개) + "더보기" sheet에 나머지 메뉴.** 장점: 엄지 도달 범위 안에서 핵심 업무 전환이 가장 빠르고 현재 위치가 항상 보인다. 배지(내 업무·알림 count) 노출과도 잘 맞는다. 단점: safe-area(bottom inset) 처리와 화면 하단 고정 요소와의 겹침 검증이 필요하다.
  2. **B안 — topbar의 hamburger drawer로 전체 메뉴 이동.** 장점: 구조 변경이 작고 메뉴 수 제한이 없다. 단점: 매 전환마다 열기 동작이 추가되어 핵심 업무 전환이 느려지고, 현재 위치·배지가 접힌 상태에서 보이지 않는다.
  3. **C안 — 현행 상단 grid 유지 + 항목 축약·접기만 보정.** 장점: 변경 최소. 단점: 콘텐츠 밀림과 한 손 도달 문제를 해소하지 못해 Task 목적 달성이 약하다.
- 권장안: **A안.** 동일 URL·view 구조를 유지한 presentation 변경만으로 구현 가능하고, 현장 한 손 흐름·현재 위치 표시·배지 노출 요구를 가장 직접적으로 충족한다. 나머지 메뉴는 "더보기"에서 기존 권한 조건 그대로 노출한다.

### 질문 3 — 현장 우선 진입을 내비게이션만으로 할지, 현장 요약 진입층까지 포함할지

- 필요한 이유: 인터뷰 문서의 정상 흐름은 "모바일 내비게이션 → 현장 핵심 업무 요약 → 원본 route 진입"인데, "요약"을 신규 화면으로 만들지 기존 화면 재사용으로 할지가 데이터 범위와 실패 처리 경계를 정한다.
- 답변이 바꾸는 범위: 신규 조회 조합 여부, 부분 실패 처리(한 요약 실패가 내비게이션을 막지 않아야 함) 설계, TASK-HOME-001과의 경계.
- 선택지 비교:
  1. **A안 — 신규 요약 화면 없이 기존 화면(내 업무·Pending·프로젝트) 자체를 모바일 우선으로 정비하고, 내비게이션 배지(내 업무·알림 count)로 우선순위를 전달.** 장점: 새 aggregate·조회가 없어 권한·부분 실패 경계가 기존 그대로이고, TASK-HOME-001(PC·모바일 Home MVP)과 중복되지 않는다. 단점: 한 화면에서 여러 영역을 가로지르는 조망은 제공하지 않는다.
  2. **B안 — 모바일 진입 시 내 업무·오픈 Pending·담당 프로젝트 병목을 묶은 신규 현장 요약 화면 추가.** 장점: 현장 조망이 한 화면에 모인다. 단점: TASK-HOME-001의 widget-slot Home과 목적이 겹쳐 동일 목적 중복 위험이 있고, 신규 조회 조합의 권한·부분 실패 검증 범위가 커진다.
- 권장안: **A안.** Roadmap이 Home MVP를 별도 canonical Task로 이미 정의하고 있으므로, 이번 Task는 공통 내비게이션·기존 화면의 현장 우선 정비·배지 기반 우선순위 전달까지로 한정하고 요약 조망은 TASK-HOME-001에 남긴다.

### 질문 4 — 390px·Teams narrow 완료 기준을 적용할 화면 범위

- 필요한 이유: 전 화면 일괄 정비는 관리자·설정 화면까지 포함해 범위가 크게 늘어난다. 이번 Task의 자동 검증·screenshot 검수 대상을 확정해야 완료 기준이 성립한다.
- 답변이 바꾸는 범위: 테스트 matrix(어떤 route에 overflow 0·touch target·keyboard 검증을 적용하는지), synthetic screenshot 대상 목록, 잔여 화면의 후속 처리 방식.
- 선택지 비교:
  1. **A안 — shell 전역 기준(내비게이션·safe-area·overflow 0) + 현장 핵심 화면(내 업무, Pending 목록·상세, 프로젝트 목록·상세·병목)만 이번 Task의 완료 기준으로 확정. 관리자·설정 화면은 회귀 없음만 확인.** 장점: 현장 사용 빈도가 높은 경로에 검증을 집중하고 Task 크기가 예측 가능하다. 단점: 일부 화면은 기존 수준의 모바일 품질로 남는다.
  2. **B안 — 전체 route를 이번 Task에서 일괄 정비·검증.** 장점: 화면별 편차가 없어진다. 단점: 관리자 화면 등 PC 중심 화면까지 포함되어 범위가 커지고, 화면별 카드 전환 재작업이 사진 Task 등 후속과 충돌할 수 있다.
- 권장안: **A안.** Roadmap의 모바일 원칙(현장 입력 중심은 모바일, 관리·일괄 편집은 PC)과 일치하며, 미정비 화면은 Finding 또는 후속 Task로 추적한다.

---

- interviewStatus: QUESTIONS_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
