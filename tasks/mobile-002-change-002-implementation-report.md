# TASK-MOBILE-002 Change 002 구현 보고서 — 전체 모바일 화면 전면 개편

## 1. 상태와 안전 경계

- Task: `TASK-MOBILE-002 Change 002`
- 유형: `APPROVED_FEATURE_IMPLEMENTATION`
- Branch: `experiment/task-mobile-002-full-mobile-redesign`
- 시작 commit: `ad89a2ea79ba1af93a0c9e4d12e50b86442052bc`
- 대표 repo·`main`·`origin/main`: 변경 없음
- main merge 승인: `0/3`; merge 금지
- Backend·API·DB·migration·Persistent UAT·provider·runtime handover: 변경 없음
- Git 게시: local experiment commit만 수행, push·PR·merge 미승인

## 2. 기획·Fable 경계

- 기준 계약: `tasks/mobile-002-planning.md`, `tasks/mobile-002-review.md`, `tasks/mobile-002-change-001.md`
- 사용자 피드백 고정: `tasks/mobile-002-change-002.md`
- Fable 호출: `NOT_APPLICABLE`
- 근거: 신규 제품 능력이 아니라 이미 승인·구현된 모바일 기능의 범위 확장과 사용성 수정이므로 `APPROVED_FEATURE_IMPLEMENTATION`으로 분류했다. 기존 Fable primary planning과 Codex review를 재사용하고 Codex가 실제 화면 대조·구현·검증을 수행했다.
- Fable/Claude 사용량: 호출하지 않아 전후 변화 `N/A`

## 3. 해결한 문제

1. 1차 구현의 core 7개 route 밖에 있던 생산·구매·자재·IQC·Teams·관리 화면은 PC 정보를 한 열 카드로 모두 펼쳐 모바일 첫 화면이 길었다.
2. 큰 hero, 큰 card padding, 반복 설명과 field parity가 실제 목록·예외·action을 첫 viewport 밖으로 밀어냈다.
3. 이번 구현은 모바일 정보를 `상태/예외 → 핵심 수치 → 다음 action → 상세` 순서로 다시 배치했다.

## 4. 구현 결과

### 전역 mobile shell과 밀도

- mobile app bar와 bottom navigation을 축소해 콘텐츠 영역을 늘렸다.
- page title 19~21px, 본문 12~13px, label/helper 10~11px 수준의 mobile token을 적용했다.
- page gap 10px, 일반 card padding 10~12px, radius 12~16px로 축소했다.
- 비상호작용 badge·KPI·설명은 작게 만들었고 모든 visible button은 최소 44×44px hit area를 유지했다.
- 큰 gradient hero를 compact white task header 또는 짧은 red focus surface로 바꿨다.

### 업무 정보 구조

- Home: hero·긴급 요약을 축소해 첫 viewport에 내 업무와 프로젝트 시작 부분까지 노출했다.
- My Work·Project·Pending·Notifications: 기존 mobile composition을 compact token으로 재정렬했다.
- Production: 4개 KPI, 검색, 첫 프로젝트의 납기·면수·계획 상태와 action을 한 viewport에 배치했다.
- Procurement: 지연·미완료 KPI와 첫 프로젝트의 미완료/완료/최근 입고를 먼저 표시하고 고객사·Code·Item은 `프로젝트 정보`로 내렸다.
- Procurement read: 입고예정·입고상태·공급 책임을 우선하고 기술 담당·발주일·통상납기는 `발주 상세`로 내렸다.
- Materials: 요약 4개, 검색·공급 filter, 첫 자재의 입고예정·미도착·처리대기와 주요 action이 첫 viewport에 들어온다. 전체 수량·업체는 `입고 상세`에서 확인한다.
- IQC: 검사 대기 수, 완료 포함 toggle과 검사 card를 compact composition으로 바꿨다.
- Teams Activity: 긴 제목을 `업무 피드`로 줄이고 반복 안내를 모바일에서 숨겨 KPI와 실제 feed를 앞당겼다.
- Admin dashboard: 발송 실패와 진행 중 escalation을 먼저 배치하고 설명문을 축약했다.
- Admin table: 기본은 사용자/구분/작업 등 핵심 열만 표시하고 `모든 관리 필드 보기`를 누르면 상태·부서·역할·감사/기술 열을 가로로 확인할 수 있게 했다.
- Project create/edit, 생산·구매 settings/edit, panel edit, Excel preview, notification detail은 전역 compact form/card/table token을 적용했다.

### Desktop 보존

- 모든 전면 개편 CSS는 `.app-shell[data-layout-mode='mobile']` 아래에 한정했다.
- 1440px Procurement reference에서 기존 sidebar, KPI, filter와 desktop table을 확인했다.
- URL·API request/response·permission·workflow·audit 계약은 변경하지 않았다.

## 5. 변경 파일

### Product source

- `frontend/src/App.tsx`
- `frontend/src/MaterialsWorkspace.tsx`
- `frontend/src/styles.css`

### 검증

- `frontend/tests/App.test.tsx`
- `frontend/e2e/full-stack/mobile-first-experience.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-compact-workspaces.full-stack.spec.ts`

### Task artifact

- `tasks/mobile-002-change-002.md`
- `tasks/mobile-002-change-002-implementation-report.md`
- `tasks/mobile-002-implementation-report.md`
- `tasks/mobile-002-change-002-screenshots/*.png`

## 6. 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | `PASS` |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend unit | `PASS` — 3 files, 76 tests |
| Production build | `PASS` — 기존 large chunk warning 유지 |
| Change 002 isolated full-stack E2E | `PASS` — 1 scenario, 390px 주요 workspace + 1440px desktop |
| TASK-008B 사급 흐름 회귀 E2E | `PASS` — 1 scenario |
| 390px horizontal overflow | `PASS` — major workspace 모두 0px |
| 390px touch target | `PASS` — visible button 모두 44×44px 이상 |
| 격리 DB cleanup | `PASS` — 전용 PostgreSQL tmpfs/container/network 제거 |
| `git diff --check` | `PASS` |

검증 중 최초 Change 002 E2E는 사용자 관리 화면이 공통 `AdminPageShell`을 사용하지 않아 mobile field toggle이 없는 문제를 찾았다. `AdminUsersPage`에 동일한 핵심/전체 열 전환을 추가하고 재실행해 통과했다. Unit의 기존 `입고예정일` label assertion도 새 compact label `입고예정` 계약으로 갱신한 뒤 전체 76개가 통과했다.

## 7. Screenshot

모든 screenshot은 격리된 E2E DB의 synthetic data만 사용했다.

1. `01-home-mobile-390.png`
2. `02-project-list-mobile-390.png`
3. `03-production-planning-mobile-390.png`
4. `04-procurement-dashboard-mobile-390.png`
5. `05-project-procurement-mobile-390.png`
6. `06-procurement-edit-mobile-390.png`
7. `07-material-receiving-mobile-390.png`
8. `08-iqc-mobile-390.png`
9. `09-teams-activity-mobile-390.png`
10. `10-admin-dashboard-mobile-390.png`
11. `11-admin-users-priority-mobile-390.png`
12. `12-admin-users-all-fields-mobile-390.png`
13. `13-procurement-desktop-reference-1440.png`

위치는 `tasks/mobile-002-change-002-screenshots/`다.

## 8. Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `M2C2-ROUTE-PARITY` | P1 | `RESOLVED` | core 7개 밖 화면이 PC field stack이라 모바일 사용성이 단절됨 | 생산·구매·자재·IQC·Teams·Admin과 form/detail 전역 mobile composition 확장 |
| `M2C2-FIRST-VIEWPORT` | P1 | `RESOLVED` | hero·KPI·설명만 보이고 실제 업무가 늦게 나타남 | shell/header/card/KPI 밀도 축소, task-first 순서 적용 |
| `M2C2-PROGRESSIVE-DISCLOSURE` | P2 | `RESOLVED` | 구매·자재 card가 모든 field를 기본 펼침 | priority grid + `프로젝트 정보`/`발주 상세`/`입고 상세` 도입 |
| `M2C2-ADMIN-DATA` | P2 | `RESOLVED` | 관리자 표는 전체 열 동시 노출 또는 정보 유실 위험 | 핵심 열 기본값 + 명시적 전체 열 toggle 구현 |
| `M2C2-TOUCH-SAFETY` | P2 | `RESOLVED` | 시각 밀도 축소가 action hit area까지 줄일 위험 | 390px visible button 44×44px E2E 실측 |
| `M2C2-DESKTOP-GUARD` | P2 | `RESOLVED` | 전역 CSS가 desktop 구조를 바꿀 위험 | `data-layout-mode='mobile'` scope와 1440px reference 검증 |
| `M2-APP-MODULE-SIZE` | P3 | `BACKLOG` | 대형 `App.tsx`가 유지보수 비용을 높임 | `BACKLOG-MOBILE-002-APP-SPLIT`; 이번 UI 개편과 분리 유지 |

Open P0/P1/P2는 `0/0/0`이다.

## 9. 종료 산출물 5종 추적

| 산출물 | 상태 | canonical 위치 |
| --- | --- | --- |
| Implementation report | `작성 완료` | 본 문서와 `tasks/mobile-002-implementation-report.md` 최신 링크 |
| SOP | `작성 완료` | 본 문서 `## 10. SOP` |
| User manual | `작성 완료` | 본 문서 `## 11. User manual` |
| Roadmap update | `N/A` | experiment-only. canonical Roadmap 실행 큐와 `Next Gate`는 변경하지 않음 |
| User validation checklist | `자동 검증 완료 · 사용자 검수 대기` | 본 문서 `## 12. User validation checklist` |

## 10. SOP

1. 모바일 구조 확인은 390×844에서 수행한다.
2. 첫 viewport에 제목, 핵심 KPI/검색, 첫 업무 또는 대표 action이 들어오는지 확인한다.
3. `프로젝트 정보`, `발주 상세`, `입고 상세`, `모든 관리 필드 보기`를 열어 보조 정보가 유실되지 않았는지 확인한다.
4. 모든 visible button이 44×44px 이상이고 document horizontal overflow가 0인지 실측한다.
5. 1440px desktop에서 sidebar, desktop table, 기존 action이 유지되는지 확인한다.
6. E2E는 `bash scripts/e2e-full-stack.sh e2e/full-stack/mobile-compact-workspaces.full-stack.spec.ts`로 실행하고 전용 DB·container·network가 제거됐는지 확인한다.

## 11. User manual

- 모바일 첫 화면은 모든 PC field를 펼치지 않고 현재 상태·지연·미완료·다음 action을 먼저 보여 준다.
- 구매 프로젝트의 고객사·Code·Item은 `프로젝트 정보`에서 확인한다.
- 구매 품목의 기술 담당·업체·통상납기·발주일은 `발주 상세`에서 확인한다.
- 자재 품목의 공급 책임·전체 수량은 `입고 상세`에서 확인한다.
- 관리자 표는 기본적으로 핵심 열만 보인다. 상태·부서·역할·감사/기술 열은 `모든 관리 필드 보기`를 눌러 가로로 확인하고 `핵심 열로 보기`로 돌아온다.
- 861px 이상 desktop은 기존 동시 정보 밀도와 table 구조를 유지한다.

## 12. User validation checklist

### 자동 검증

- [x] 390px Home·Project·Production·Procurement·Materials·IQC·Teams·Admin 대표 화면 확인
- [x] 첫 viewport task-first 정보 순서 확인
- [x] 구매·자재 progressive disclosure 확인
- [x] Admin 핵심/전체 열 toggle 확인
- [x] visible button 44×44px 이상 확인
- [x] horizontal overflow 0px 확인
- [x] 1440px desktop composition 확인
- [x] typecheck·lint·unit·build·isolated E2E·사급 회귀 확인

### 사용자 직접 검수

- [ ] 글씨가 작아졌지만 현장에서 읽기 편한지 확인
- [ ] Home에서 내 업무·프로젝트가 충분히 빨리 보이는지 확인
- [ ] 생산계획에서 KPI와 첫 프로젝트가 한 화면에서 판단 가능한지 확인
- [ ] 구매에서 미완료·지연과 입고예정이 우선순위에 맞는지 확인
- [ ] 자재에서 미도착·처리대기와 도착 등록 action을 바로 찾을 수 있는지 확인
- [ ] IQC 검사 card 정보량이 적절한지 확인
- [ ] Admin 핵심 열 기본값과 전체 열 전환이 실제 관리 업무에 충분한지 확인
- [ ] desktop 화면에 회귀가 없는지 확인

## 13. Rollback

- 본 local experiment commit을 revert하면 Change 002 source, test, artifact를 한 번에 되돌릴 수 있다.
- Backend·DB·migration 변경이 없어 data rollback은 필요 없다.
- 대표 repo와 GitHub main에는 변경이 없으므로 main rollback은 `N/A`다.
