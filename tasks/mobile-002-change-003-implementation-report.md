# TASK-MOBILE-002 Change 003 구현 보고서 — 좌측 상단 숨김 메뉴와 모바일 Shape System

## 1. 상태와 안전 경계

- Task: `TASK-MOBILE-002 Change 003`
- 유형: `APPROVED_FEATURE_IMPLEMENTATION`
- Branch: `experiment/task-mobile-002-full-mobile-redesign`
- 시작 commit: `8a1afd0fa74ab04614d766d0df7ccb291d501091`
- 대표 repo·`main`·`origin/main`: 변경 없음
- main merge 승인: `0/3`; merge 금지
- Backend·API·DB·migration·Persistent UAT·provider·runtime handover: 변경 없음
- Git 게시: local experiment commit만 수행, push·PR·merge 미승인

## 2. 기획·Fable 경계

- 기준 계약: `tasks/mobile-002-planning.md`, `tasks/mobile-002-review.md`, `tasks/mobile-002-change-002.md`
- 사용자 피드백 고정: `tasks/mobile-002-change-003.md`
- Fable 호출: `NOT_APPLICABLE`
- 근거: 새 업무·데이터·권한 능력이 아니라 기존 모바일 presentation의 navigation과 visual system 수정이다.
- Fable/Claude 사용량: 호출하지 않아 전후 변화 `N/A`

## 3. 해결한 업무 문제

1. 하단 fixed navigation이 모든 화면 위에 계속 남아 실제 콘텐츠를 가리고 하단 여백을 강제했다.
2. 모바일 대부분의 surface가 같은 둥근 직사각형이라 상태·선택·KPI·업무 묶음의 위계가 약했다.
3. 일부 화면은 작은 폭에서 구매 수정 header와 action이 잘리고 IQC checkbox가 과도하게 커 보였다.
4. 사용자 관리 screenshot이 loading 중 캡처되어 실제 정렬을 확인하기 어려웠다.

## 4. 구현 결과

### 왼쪽 상단 숨김 메뉴

- 하단 5개 핵심 tab + 더보기 bar를 DOM에서 제거했다.
- mobile app bar 왼쪽에 45×45px 메뉴 trigger를 배치했다.
- trigger 클릭 시 body portal 기반 왼쪽 drawer가 `100dvh` 전체 높이로 열린다.
- 권한에서 파생된 전체 navigation item을 한 목록으로 표시하고 현재 화면은 red angular item으로 표시한다.
- 메뉴 선택, 닫기, backdrop, Escape로 닫히며 첫 item focus, Tab trap, trigger focus 복귀와 body scroll lock을 제공한다.
- drawer가 sticky app bar의 `backdrop-filter` containing block에 잘리지 않도록 portal을 사용했다.
- desktop sidebar는 변경하지 않았다.

### Shape System

- 각진 직사각형: menu trigger, active menu, page header의 시작 edge, 일부 KPI.
- 타원형: 상태 button, KPI variant, filter·badge.
- 원형: drawer shape marker, count badge, menu close.
- 둥근 직사각형: 일반 업무 card와 form surface.
- 정사각형: drawer marker와 KPI variant.
- 비대칭/절단형: alternating card, 지연·강조 summary와 drawer footer marker.
- 긴 문장 card를 원이나 타원으로 강제하지 않고 짧은 상태·수치에만 해당 도형을 사용했다.

### 타이포·정렬

- mobile app bar를 `menu / brand / status` 3열 기준선으로 맞췄다.
- page header는 각진 leading edge와 18~20px title scale, KPI는 tabular number와 동일 높이를 사용한다.
- KPI·summary·detail cell은 순서별 shape variant를 가지되 8px 계열 간격과 중심선을 유지한다.
- 구매정보 수정 header를 한 열 제목 + 3열 action grid로 재구성해 제목·button clipping을 제거했다.
- IQC toolbar의 checkbox visual을 22px로 줄이고 label을 한 줄 정렬했다.
- 하단 fixed bar 제거에 맞춰 shell bottom reserve를 16px safe-area로 줄이고 sticky action bottom을 10px safe-area로 이동했다.

### 영향 분석

| 영역 | 영향 |
| --- | --- |
| Frontend | mobile drawer, shape/type/alignment CSS, navigation tests |
| Backend·API | N/A — endpoint·request·response·Policy 변경 없음 |
| DB·Migration | N/A — schema·data 변경 없음 |
| 권한 | 기존 `navigationItems` 파생 결과를 그대로 사용 |
| Workflow·Audit | 상태 전이·audit 변경 없음 |
| Excel·PDF·첨부 | 기존 기능·양식·저장 계약 변경 없음 |
| Desktop | 1440px sidebar·table reference 유지 |
| 개인정보·secret | isolated synthetic data screenshot만 사용 |

## 5. 변경 파일

### Product source

- `frontend/src/App.tsx`
- `frontend/src/styles.css`

### 검증

- `frontend/tests/App.test.tsx`
- `frontend/e2e/full-stack/mobile-first-experience.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-adaptive-navigation.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-compact-workspaces.full-stack.spec.ts`
- `frontend/e2e/full-stack/home-dashboard.full-stack.spec.ts`

### Task artifact

- `tasks/mobile-002-change-003.md`
- `tasks/mobile-002-change-003-implementation-report.md`
- `tasks/mobile-002-implementation-report.md`
- `tasks/mobile-002-change-003-screenshots/*.png`

## 6. 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | `PASS` |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend unit | `PASS` — 3 files, 76 tests |
| Production build | `PASS` — 기존 large chunk warning 유지 |
| Change 003 isolated full-stack E2E | `PASS` — 1 scenario, 주요 mobile workspace 12개 + drawer + desktop |
| 기존 mobile/home 영향 E2E | `PASS` — 4 scenarios |
| 390px horizontal overflow | `PASS` — 0px |
| visible button touch target | `PASS` — 44×44px 이상, trigger 45×45px |
| drawer shape 종류 | `PASS` — circle·square·oval·rounded·angular 5종 |
| KPI computed shape | `PASS` — 3종 이상 border geometry |
| 격리 DB cleanup | `PASS` — 전용 PostgreSQL tmpfs/container/network 제거 |

## 7. Screenshot

모든 screenshot은 격리된 E2E DB의 synthetic data만 사용했다.

1. `01-home-mobile-390.png`
2. `01b-left-menu-drawer-mobile-390.png`
3. `02-project-list-mobile-390.png`
4. `03-production-planning-mobile-390.png`
5. `04-procurement-dashboard-mobile-390.png`
6. `05-project-procurement-mobile-390.png`
7. `06-procurement-edit-mobile-390.png`
8. `07-material-receiving-mobile-390.png`
9. `08-iqc-mobile-390.png`
10. `09-teams-activity-mobile-390.png`
11. `10-admin-dashboard-mobile-390.png`
12. `11-admin-users-priority-mobile-390.png`
13. `12-admin-users-all-fields-mobile-390.png`
14. `13-procurement-desktop-reference-1440.png`

위치는 `tasks/mobile-002-change-003-screenshots/`다.

## 8. Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `M2C3-BOTTOM-NAV-OCCLUSION` | P1 | `RESOLVED` | 하단 fixed bar가 콘텐츠와 sticky action 공간을 계속 점유 | bottom navigation 제거, top-left drawer와 bottom reserve 보정 |
| `M2C3-DRAWER-CONTAINING-BLOCK` | P2 | `RESOLVED` | sticky app bar의 backdrop filter가 fixed drawer 높이를 app bar 안으로 제한 | drawer를 `document.body` portal로 이동 |
| `M2C3-SHAPE-MONOTONY` | P2 | `RESOLVED` | 동일 round rectangle 반복으로 정보 위계 약화 | 역할 기반 6종 shape system 적용과 E2E geometry 검증 |
| `M2C3-PROCUREMENT-HEADER-CLIP` | P2 | `RESOLVED` | 수정 header가 action row와 폭을 경쟁해 제목·button이 잘림 | mobile 전용 title/action grid 적용 |
| `M2C3-IQC-CHECKBOX-ALIGNMENT` | P2 | `RESOLVED` | 전역 input width가 checkbox와 label을 크게 밀어냄 | 22px checkbox + compact inline label 적용 |
| `M2C3-TOUCH-SUBPIXEL` | P2 | `RESOLVED` | 44px trigger가 browser DPR 계산에서 43.999px로 측정 | trigger를 명시적 45px로 보정 후 영향 E2E 4/4 재통과 |
| `M2-APP-MODULE-SIZE` | P3 | `BACKLOG` | 대형 `App.tsx` 유지보수 비용 | `BACKLOG-MOBILE-002-APP-SPLIT`; 이번 UI 수정과 분리 유지 |

Open P0/P1/P2는 `0/0/0`이다.

## 9. 종료 산출물 5종 추적

| 산출물 | 상태 | canonical 위치 |
| --- | --- | --- |
| Implementation report | `작성 완료` | 본 문서와 `tasks/mobile-002-implementation-report.md` 최신 링크 |
| SOP | `작성 완료` | 본 문서 `## 10. SOP` |
| User manual | `작성 완료` | 본 문서 `## 11. User manual` |
| Roadmap update | `N/A` | experiment-only. canonical Roadmap 실행 큐와 `Next Gate`를 변경하지 않음 |
| User validation checklist | `자동 검증 완료 · 사용자 검수 대기` | 본 문서 `## 12. User validation checklist` |

## 10. SOP

1. 390×844에서 app bar 왼쪽 메뉴 button이 보이고 하단 fixed menu가 없는지 확인한다.
2. 메뉴를 열어 현재 route, 권한별 전체 item, 5종 shape marker를 확인한다.
3. Tab·Shift+Tab·Escape·backdrop·item 선택과 focus 복귀를 확인한다.
4. Home·Project·Production·Procurement·Materials·IQC·Teams·Admin에서 shape 역할, title·KPI 정렬과 overflow 0을 확인한다.
5. 구매정보 수정 action grid와 IQC 완료 포함 checkbox 정렬을 확인한다.
6. 1440px에서 desktop sidebar·table이 유지되는지 확인한다.
7. E2E는 `bash scripts/e2e-full-stack.sh e2e/full-stack/mobile-compact-workspaces.full-stack.spec.ts`로 실행한다.

## 11. User manual

- 모바일에서는 왼쪽 위 빨간 메뉴 button을 눌러 전체 업무 메뉴를 연다.
- 메뉴에서 화면을 선택하면 이동과 동시에 drawer가 닫힌다.
- 메뉴를 닫으려면 `×`, 바깥 어두운 영역 또는 Escape를 사용한다.
- 하단 고정 메뉴는 더 이상 없으므로 콘텐츠를 화면 끝까지 사용할 수 있다.
- 원은 count/긴급 신호, 타원은 상태·선택, 정사각형은 짧은 KPI, 각진 도형은 현재 위치·강한 action, 둥근 card는 일반 업무 묶음을 뜻한다.
- 861px 이상 desktop에서는 기존 왼쪽 sidebar를 그대로 사용한다.

## 12. User validation checklist

### 자동 검증

- [x] 하단 fixed navigation DOM 0 확인
- [x] top-left trigger 45×45px 확인
- [x] drawer 전체 높이·open/close·focus trap·focus restore 확인
- [x] 권한별 전체 navigation과 active item 확인
- [x] circle·square·oval·rounded·angular 5종 drawer marker 확인
- [x] 대표 KPI 3종 이상 computed geometry 확인
- [x] 390px overflow 0·visible button 44×44px 이상 확인
- [x] 구매 수정 header와 IQC toolbar 정렬 확인
- [x] 1440px desktop reference 확인
- [x] typecheck·lint·unit·build·isolated E2E·영향 E2E 확인

### 사용자 직접 검수

- [ ] 왼쪽 상단 메뉴 button 위치와 크기가 한 손 사용에 적절한지 확인
- [ ] drawer의 메뉴 순서·shape marker·현재 화면 강조가 이해하기 쉬운지 확인
- [ ] 하단 menu가 사라져 실제 콘텐츠가 더 편하게 보이는지 확인
- [ ] Home·Project·Production·Procurement·Materials·IQC·Teams·Admin의 글씨 크기와 정렬 확인
- [ ] 원·타원·정사각형·각진/둥근 직사각형이 과하지 않고 역할별로 구분되는지 확인
- [ ] 1440px desktop 화면에 회귀가 없는지 확인

## 13. Rollback

- 본 local experiment commit을 revert하면 Change 003 source, test와 artifact를 함께 되돌릴 수 있다.
- Backend·DB·migration 변경이 없어 data rollback은 필요 없다.
- 대표 repo와 GitHub main에는 변경이 없으므로 main rollback은 `N/A`다.

## 14. 해결한 업무 문제

모바일 화면 하단을 계속 차지하던 navigation을 사용자가 필요할 때만 여는 왼쪽 drawer로 바꾸고, 동일한 둥근 card 반복을 역할 기반 shape system으로 교체했다.

## 15. 기술적 결정과 검토한 대안

- 채택: app bar trigger + body portal drawer. 상단 위치와 전체 높이·focus 접근성을 함께 보장한다.
- 폐기: 하단 bar를 축소해서 유지하는 안. 콘텐츠 가림과 fixed reserve 문제가 남는다.
- 폐기: drawer를 sticky app bar 안의 fixed child로 두는 안. backdrop-filter containing block 때문에 높이가 잘렸다.
- 채택: 짧은 KPI·상태에만 원·타원·정사각형을 적용하고 긴 업무 내용은 card 가독성을 우선한다.

## 16. 시행착오 및 폐기한 접근

- 최초 drawer는 app bar 내부에서 렌더돼 screenshot에서 header 높이만 보였다. body portal로 옮기고 전체 높이 screenshot을 재생성했다.
- 정확히 44px인 trigger가 Playwright 좌표에서 43.999px로 측정됐다. CSS·브라우저 DPR 오차를 흡수하도록 45px로 보정했다.
- 기존 IQC checkbox는 전역 input width 때문에 사각형이 크게 늘어났다. checkbox 전용 크기와 label grid를 추가했다.

## 17. 사용자 검수 결과와 남은 항목

- 자동 화면·시각 검수: 완료
- 사용자 수동 검수: 대기 — 위 checklist를 완료로 주장하지 않음
- Persistent runtime handover: 미실행·범위 밖
- P3: `BACKLOG-MOBILE-002-APP-SPLIT`
- Roadmap canonical Next Gate: `TASK-007A`
