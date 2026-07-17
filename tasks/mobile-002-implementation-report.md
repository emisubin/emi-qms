# TASK-MOBILE-002 구현 보고서 — 모바일 우선 적응형 화면 1차 Vertical Slice

> 최신 후속 구현: [TASK-MOBILE-002 Change 002 전체 모바일 화면 전면 개편](mobile-002-change-002-implementation-report.md). 1차 7개 route 범위를 생산·구매·자재·IQC·Teams·관리 화면까지 확장했으며, 아래 본문은 최초 vertical slice 당시의 기록으로 보존한다.

## 1. 상태와 안전 경계

- Task: `TASK-MOBILE-002`
- 유형: `NEW_FEATURE` — 업무 데이터 능력 추가가 아니라 사용자에게 새 모바일 화면 구성을 제공하는 실험 기능
- Branch: `experiment/task-mobile-002-mobile-first-experience`
- 시작 commit: `f18a4f50a9d203a34b6332ea4e95c4dfc8959f64`
- 대표 `main`·`origin/main`: `b8f3e2104074d05c2e71999c08a7374e8729f68f` 유지
- Backend·API·DB·migration·Persistent UAT·provider·runtime handover: 변경 없음
- Git 게시: local experiment commit만 허용. Push·PR·merge 미승인, main merge 승인 `0/3`

## 2. Fable 사용량 전후

Claude `/usage`의 주간 정수 반올림 projection이다. 계정 식별자와 raw TUI는 tracked artifact에 기록하지 않았다.

| 측정 시점 | 전체 모델 사용 | 전체 모델 잔여 | Fable 사용 | Fable 잔여 |
| --- | ---: | ---: | ---: | ---: |
| Fable interview·planning 전 | 8% | 92% | 15% | 85% |
| Fable interview 2회·planning 1회 후 | 10% | 90% | 19% | 81% |
| 변화 | +2%p | -2%p | +4%p | -4%p |

- 전체 모델 reset projection: `Jul 18 08:00 (Asia/Seoul)`
- Fable reset projection: 현재 TUI parser에서 `UNAVAILABLE_TUI_PARSE`
- 후속 Task 측정 도구: `bash scripts/report-claude-usage.sh`
- 최근 재검증: 전체 모델 10% 사용/90% 잔여, Fable 19% 사용/81% 잔여, `projectionStatus=READY`

## 3. 구현한 적응형 판별

- 구조 판별은 User-Agent·OS·기기 이름이 아니라 `matchMedia('(max-width: 860px)')`를 사용한다.
- `≤860px`은 모바일 전용 composition, `≥861px`은 기존 desktop composition이다.
- 터치 보정은 구조 판별과 분리해 `any-pointer: coarse`, `pointer: coarse`, `hover: none` 중 하나라도 참이면 적용한다.
- 중앙 `AdaptiveLayoutProvider` 결과를 shell의 `data-layout-mode`와 `data-touch-optimized`에 투영해 React 구조와 CSS가 같은 source를 사용한다.
- mode 전환 시 provider 아래 화면을 remount하지 않아 URL·입력값·filter state를 보존한다.
- 기기 정보·UA 문자열을 저장하거나 서버로 보내지 않는다.

## 4. 구현 결과

### 전역 모바일 shell

- desktop sidebar·topbar·system strip을 모바일에서 compact app bar와 권한 기반 bottom navigation으로 대체했다.
- API·DB·User, 검수 전용 상태, 개발/검수 사용자 전환, 로그아웃을 접근 가능한 status sheet로 이동했다.
- status/filter sheet는 dialog semantics, Escape, focus trap, 최초 focus, trigger focus 복귀, backdrop close, body scroll lock, safe-area와 `100dvh`를 제공한다.

### 핵심 7개 route

1. Home: `긴급·차단 → 내 업무 → 프로젝트 → Pending → 알림` 순서의 현장 command center
2. My Work: 오늘 업무 hero, 우선순위 요약, thumb-size action cards
3. Project List: 현장 hero, full-screen draft filter, mobile project cards
4. Project Detail: 병목·다음 action 우선, 권한별 기존 수정·보류·취소·삭제 action 유지
5. Pending List: 긴급/기한 초과 우선, full-screen filter, mobile issue cards
6. Pending Detail: 상태·긴급도·기한과 다음 action 우선
7. Notifications: 읽지 않음·긴급/차단 우선 summary와 mobile cards

480px Teams narrow와 1440px desktop reference도 검증했다. 생산관리·구매·자재·관리자 route의 전용 mobile composition은 이번 1차 slice의 완료 범위가 아니다.

### 전체 영향 분석

| 영역 | 영향 |
| --- | --- |
| Frontend | adaptive provider, mobile shell, 핵심 7개 route presentation, accessible sheet 추가 |
| Backend | N/A — endpoint·Policy·runtime source 변경 없음 |
| API | request/response shape와 호출 경로 변경 없음 |
| DB·Migration | N/A — schema·data·migration 변경 없음 |
| 권한 | 기존 navigation permission과 서버 Policy 재사용; 권한 확대 없음 |
| Workflow·Audit | 상태 전이·audit 계약 변경 없음; 기존 action을 모바일에 동일하게 노출 |
| Excel | N/A — Excel import/export 동작·양식 변경 없음 |
| PDF | N/A — PDF 기능과 산출물 변경 없음 |
| 첨부파일 | N/A — upload·download·보존 정책 변경 없음 |
| 기존 기능 회귀 | 861px 이상 desktop 구조 보존, 1440px Home/Project reference와 unit/build로 검증 |
| 개인정보·secret | synthetic data만 사용; raw usage TUI는 소유 temp에서 삭제하고 비율 projection만 기록 |

## 5. 변경 파일

### Product source

- `frontend/src/adaptive-layout.tsx`
- `frontend/src/MobileSheet.tsx`
- `frontend/src/App.tsx`
- `frontend/src/HomePage.tsx`
- `frontend/src/PendingPage.tsx`
- `frontend/src/styles.css`

### 검증·운영 보조

- `frontend/tests/adaptive-layout.test.tsx`
- `frontend/tests/App.test.tsx`
- `frontend/e2e/full-stack/mobile-first-experience.full-stack.spec.ts`
- `scripts/report-claude-usage.sh`

### Task artifact

- `tasks/mobile-002-interview.md`
- `tasks/mobile-002-interview-round-1-fable.md`
- `tasks/mobile-002-interview-round-2-fable.md`
- `tasks/mobile-002-planning.md`
- `tasks/mobile-002-review.md`
- `tasks/mobile-002-change-001.md`
- `tasks/mobile-002-implementation-report.md`
- `tasks/mobile-002-screenshots/*.png`

## 6. 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | PASS |
| Frontend lint | PASS — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend unit | PASS — 3 files, 76 tests |
| Production build | PASS — 기존 large chunk warning 유지 |
| TASK-MOBILE-002 isolated full-stack E2E | PASS — 2 scenarios |
| Shell syntax | PASS |
| Usage privacy-safe projection | PASS |
| Fable private session cleanup | PASS — session 3개, transcript 3개 제거 |
| `git diff --check` | PASS |
| 분리된 Codex 구현 검증 | PASS — Open P0/P1/P2 `0/0/0` |

E2E는 실행별 전용 PostgreSQL container/network/tmpfs를 사용했고 종료 후 DB·container·network를 제거했다. Persistent UAT를 사용하거나 변경하지 않았다.

검증한 계약:

- 390px core route 7개와 480px Teams narrow
- mobile/desktop layout attribute와 desktop navigation 보존
- app bar·bottom navigation·44px touch target·horizontal overflow 없음
- Project·Pending filter의 draft/apply/cancel/reset, focus 이동·복귀
- mobile status sheet의 시스템 상태·계정 surface
- mobile Project action group의 수정·보류·취소·삭제 parity
- viewport/touch capability matrix와 mode 전환 중 child input state 보존

## 7. 스크린샷

Synthetic test data만 사용했다.

1. `01-home-mobile-390.png`
2. `02-my-work-mobile-390.png`
3. `03-project-filter-sheet-mobile-390.png`
4. `04-project-list-mobile-390.png`
5. `05-project-detail-mobile-390.png`
6. `06-pending-filter-sheet-mobile-390.png`
7. `07-pending-list-mobile-390.png`
8. `08-pending-detail-mobile-390.png`
9. `09-notifications-mobile-390.png`
10. `10-status-sheet-mobile-390.png`
11. `11-teams-activity-mobile-480.png`
12. `12-home-desktop-reference-1440.png`
13. `13-project-list-desktop-reference-1440.png`
14. `14-coarse-pointer-desktop-1024.png`

## 8. Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `M2-GLOBAL-SHELL` | P1 | `RESOLVED` | PC shell이 남으면 축소판 인상을 제거할 수 없음 | mobile app bar·status sheet·bottom navigation 구현 |
| `M2-ACTION-PARITY` | P2 | `RESOLVED` | 모바일 강조 action만 남으면 권한 action 유실 가능 | Project mobile action group과 unit assertion 추가; 기존 서버 권한 재사용 |
| `M2-HYBRID-TOUCH` | P2 | `RESOLVED` | 단일 pointer query는 hybrid 장치를 놓침 | 3개 capability query matrix 구현·unit 검증 |
| `M2-BREAKPOINT-DRIFT` | P2 | `RESOLVED` | React/CSS 구조 mode 불일치 위험 | 중앙 provider와 DOM data attribute 적용 |
| `M2-FILTER-SHEET` | P2 | `RESOLVED` | 적용/취소·focus 계약 없는 sheet는 현장 사용성이 낮음 | 공통 accessible `MobileSheet`와 draft filter 구현 |
| `M2-SHEET-FOCUS-CHURN` | P2 | `RESOLVED` | inline close callback 변경이 입력 중 focus effect를 재실행할 수 있음 | 최신 callback은 ref로 유지하고 effect를 open lifecycle에만 결합; 날짜/select focus 유지 검증 추가 |
| `M2-FILTER-PRIMARY-WIDTH` | P2 | `RESOLVED` | 3개 footer action 중 대표 적용 버튼이 반폭으로 배치됨 | 마지막 primary action을 2열 전체 span으로 변경하고 screenshot 재검수 |
| `M2-COARSE-TOUCH-WIDTH` | P2 | `RESOLVED` | coarse desktop에서 높이만 44px이고 좁은 button 폭은 보장하지 않음 | touch-optimized button min-width 44px와 1024px visible-button bounds 검증 추가 |
| `M2-STICKY-NAV-OVERLAP` | P2 | `RESOLVED` | sticky primary와 fixed bottom navigation의 실제 좌표 검증이 없음 | 390px Project detail에서 scroll 후 `action.bottom <= nav.top` E2E 실측 |
| `M2-HOME-SOURCE-ERROR` | P2 | `RESOLVED` | 모바일 우선 요약의 source 실패가 `-`로만 보일 수 있음 | 해당 요약 바로 아래 오류·재시도 안내 표시와 unit 검증 추가 |
| `M2-USAGE-RAW-IDENTIFIER` | P2 | `RESOLVED` | 최초 수동 TUI 확인 중 계정 식별자가 transient tool output에 노출될 수 있음 | tracked 파일 미기록, 전용 temp raw 삭제·fixed percentage projection script 적용 |
| `M2-SCOPE-LABEL` | P2 | `RESOLVED` | 전체 제품 완료로 오해할 위험 | 모든 산출물에서 핵심 7 route 1차 vertical slice로 명시 |
| `M2-ARTIFACT-GATE` | P1 | `RESOLVED` | 종료 5종 산출물·고정 블로그 section·사용자 checklist 추적이 처음 report에 부족함 | 본 report의 5종 추적표, SOP, User manual, Roadmap update, checklist와 고정 section으로 보강 |
| `M2-APP-MODULE-SIZE` | P3 | `BACKLOG` | 대형 `App.tsx` 분기가 유지보수 비용을 높임 | `BACKLOG-MOBILE-002-APP-SPLIT`: provider와 sheet는 분리 완료, route component 대규모 분할은 후속 housekeeping 후보 |
| `M2-SOL-SELECTOR` | P3 | `BACKLOG` | 요청한 GPT 5.6 Sol selector가 현재 환경에 없음 | `BACKLOG-MOBILE-002-SOL-REVIEW`: 사용했다고 주장하지 않고 별도 read-only reviewer로 대체; selector 제공 시 재검토 |

Open P0/P1/P2는 `0/0/0`이다.

## 9. 종료 산출물 5종 추적

| 산출물 | 상태 | canonical 위치 |
| --- | --- | --- |
| Implementation report | `작성 완료` | `tasks/mobile-002-implementation-report.md` 전체 |
| SOP | `작성 완료` | 본 문서 `## 10. SOP` |
| User manual | `작성 완료` | 본 문서 `## 11. User manual` |
| Roadmap update | `N/A` | 본 문서 `## 12. Roadmap update` — experiment-only라 canonical Roadmap 변경 금지 |
| User validation checklist | `자동 검증 완료 · 사용자 검수 대기` | 본 문서 `## 13. User validation checklist` |

## 10. SOP

### 후속 Fable 호출 전후 사용량 측정

1. Fable 호출 전에 repository root에서 `bash scripts/report-claude-usage.sh`를 실행한다.
2. `projectionStatus=READY`인지 확인하고 전체 모델/Fable used·remaining 비율만 Task change 또는 report에 기록한다.
3. 승인된 read-only Fable runner로 interview/planning을 수행한다.
4. planning 직후 같은 usage script를 다시 실행해 전후 변화량을 기록한다.
5. raw TUI·계정 식별자·session transcript를 tracked 문서에 복사하지 않는다.

### 모바일 자동 검증

1. `frontend`에서 typecheck, lint, unit, build를 실행한다.
2. repository root에서 `bash scripts/e2e-full-stack.sh e2e/full-stack/mobile-first-experience.full-stack.spec.ts`를 실행한다.
3. `tasks/mobile-002-screenshots/`의 390/480/1024/1440 evidence를 확인한다.
4. E2E runner가 전용 DB·container·network를 정리했는지 성공 로그로 확인한다.

Persistent runtime handover SOP는 `N/A`다. 이 Task는 5081/5174를 교체하거나 Persistent UAT를 변경하지 않는다.

## 11. User manual

- 화면 폭이 860px 이하이면 별도 선택 없이 모바일 전용 구성이 열린다. 861px 이상이면 기존 desktop 화면이 유지된다.
- 상단 `상태` 버튼에서 API·DB·현재 사용자와 개발/검수 계정 동작을 확인한다.
- 하단 탭은 Home·내 업무·프로젝트·Pending·알림을 제공하고, 역할별 추가 메뉴는 `더보기`에서 연다.
- Home은 긴급 Pending·차단 알림을 먼저 보여 준다.
- Project/Pending 목록의 `검색·필터`를 열어 조건을 고르고 `조건 적용`을 누른다. `취소`는 기존 적용 조건을 유지하고 `초기화`는 draft 조건만 비운다.
- Project 상세의 `프로젝트 작업`에서 현재 권한이 허용한 수정·상태 변경·삭제 action을 실행한다.
- 브라우저 폭을 바꿔도 같은 URL과 입력 중 상태가 유지된다. 이번 버전에는 수동 `PC 화면으로 보기` toggle이 없다.

## 12. Roadmap update

`N/A — experiment-only branch`다. Product Roadmap의 canonical 실행 큐와 `Next Gate`는 `TASK-007A`로 유지한다. 본 Task가 메인 또는 Roadmap 상태를 변경했다고 주장하지 않는다.

## 13. User validation checklist

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 대기`.

### 자동 검증

- [x] 390px Home·My Work·Project list/detail·Pending list/detail·Notifications 구조 확인
- [x] Project·Pending filter sheet 적용·취소·초기화·focus lifecycle 확인
- [x] Project·Pending 허용 action 노출 확인
- [x] 480px Teams narrow horizontal overflow 없음
- [x] 1024px coarse-pointer desktop composition과 44px touch target 확인
- [x] 1440px desktop Home·Project 구조 보존 확인
- [x] typecheck·lint·unit·build·isolated E2E·privacy-safe usage projection 확인

### 사용자 직접 검수

- [ ] Home에서 긴급 업무 순서가 실제 현장 우선순위와 맞는지 확인
- [ ] 내 업무 카드와 하단 navigation을 한 손으로 사용하기 쉬운지 확인
- [ ] Project 목록에서 요약 띠 다음에 실제 프로젝트 카드가 충분히 빨리 보이는지 확인
- [ ] Project 상세의 병목·다음 action·보조 action 순서 확인
- [ ] Pending 목록/상세의 긴급도·기한·조치 흐름 확인
- [ ] 알림의 읽지 않음·긴급 신호 우선순위 확인
- [ ] 상태 sheet에서 시스템 상태와 계정 동작 확인
- [ ] desktop 화면이 기존 업무 밀도를 유지하는지 확인

## 14. 해결한 업무 문제

모바일에서 PC 화면을 단순히 좁혀 보이던 구조를 제거하고, 현장 사용자가 긴급·차단·다음 action을 먼저 판단할 수 있는 전용 정보 구조와 navigation을 제공했다.

## 15. 기술적 결정과 검토한 대안

- 채택: viewport로 composition을 선택하고 pointer/hover capability로 touch target만 보정한다.
- 폐기: User-Agent sniffing은 hybrid 장치 오판과 유지보수 비용 때문에 사용하지 않았다.
- 보류: 수동 desktop toggle은 persistence 정책이 필요해 후속 범위로 분리했다.
- 보존: URL·API·권한·Workflow·audit는 한 벌만 유지해 모바일/desktop 계약 drift를 막았다.

## 16. 시행착오 및 폐기한 접근

- full-page mobile screenshot은 fixed bottom navigation과 full-screen sheet를 실제 viewport와 다르게 보이게 해 viewport screenshot으로 교체했다.
- 처음에는 mobile Project KPI가 1열로 누적돼 실제 카드가 늦게 보였다. compact horizontal summary strip으로 바꿨다.
- inline close callback을 focus effect dependency로 둔 초안은 입력 중 focus 재설정 위험이 있어 callback ref 방식으로 교체했다.
- status card 2열은 긴 값이 잘려 390px에서 1열 상태 카드로 바꿨다.

## 17. 사용자 검수 결과와 남은 항목

- 자동 화면·시각 검수: 완료
- 사용자 수동 검수: 대기 — 위 checklist의 미체크 항목을 완료로 주장하지 않음
- 별도 persistent frontend/backend runtime: 기동하지 않음
- 후속 mobile route: 생산관리·구매·자재·관리자
- 후속 정책: 수동 PC 보기 toggle, offline/photo/upload retry
- Roadmap canonical Next Gate: `TASK-007A`

## 18. Rollback

실험 branch의 TASK-MOBILE-002 local commit을 기준으로 revert하면 된다. Backend·DB·migration·runtime handover가 없어 별도 data rollback은 필요하지 않다. 대표 repo와 `main`은 이 Task 시작 전후 동일하다.
