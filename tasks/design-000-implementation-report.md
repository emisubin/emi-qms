# DESIGN-000 Implementation report — EMI Design Foundation

## 1. 요약과 상태

- 목적: 제공된 reference의 시각 문법을 EMI semantic token과 재사용 가능한 React primitive로 고정한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 계약: [DESIGN-000](design-000.md)
- 적용: shell·Home·Sales 우선 adoption, 공통 mobile simple-mode, desktop/mobile visual baseline.
- 제외: 타사 logo·문구 복제, Figma file/library publish, feature·API·DB·migration 변경, 대표 repo·`main`·게시.

## 2. 해결한 업무 문제

Task별 CSS가 누적되며 같은 heading·surface·toolbar·badge가 서로 다른 color·radius·spacing을 사용했다. reference의 compact white workspace, full-height navigation, blue active state, 낮은 shadow와 조밀한 typography를 semantic foundation으로 분리해 후속 화면이 공통 기준을 재사용하게 했다.

## 3. Token·component catalog

### Token

| 범주 | 구현 기준 |
| --- | --- |
| Color | cool-gray canvas, white surface, primary blue·pale active blue, neutral border, semantic success/warning/danger |
| Typography | compact 11~13px body/control, 18~22px title, tabular metric number |
| Spacing | 4px 기반 compact scale과 page/surface inset |
| Shape | 6~10px control·surface radius, status pill과 avatar/count circle의 역할 제한 |
| Elevation | 1px divider 중심, 작은 card shadow, 강한 장식 shadow 제외 |
| Shell | 192px desktop sidebar, 52px topbar, 390px compact app bar·single column |

### React primitive

| Component | 역할 |
| --- | --- |
| `DsPageHeader` | eyebrow·title·description·action의 공통 heading |
| `DsSurface` | white data/work surface와 접근성 label |
| `DsToolbar` | search·filter·action의 compact control row |
| `DsTabs` | blue active underline·count를 쓰는 view switch |
| `DsBadge` | status·unit·count의 semantic pill |

기존 EMI logo·업무 정보·권한은 유지하고 reference의 구조·밀도·도형·상태 문법만 투영했다.

## 4. 아키텍처와 영향

- `frontend/src/design-system/tokens.css`: semantic CSS variable와 기존 shell class mapping, mobile simple-mode.
- `frontend/src/design-system/components.tsx`: primitive 5종.
- `frontend/src/design-system/index.ts`: stable import boundary.
- `frontend/src/main.tsx`: base style 뒤 token layer 적용.
- Home·Sales는 surface·badge primitive를 실제 채택해 foundation을 증명했다.
- Backend/API/DB/Migration/권한/Workflow: `N/A` — Frontend foundation만 변경.
- Excel/PDF/첨부: 생성 계약 무변경. 모바일에서는 PC 관리 진입점만 단순화했다.

## 5. 실행한 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | `PASS` |
| Frontend lint | `PASS` — error 0, 기존 Fast Refresh warning 1 |
| Frontend unit | `PASS` — 104/104 |
| Frontend build | `PASS` — 기존 large chunk warning 유지 |
| Design·Sales isolated Full-Stack E2E | `PASS` — 1/1 |
| Mobile compact E2E | `PASS` — 1/1 |
| Export desktop/mobile 영향 E2E | `PASS` — 3/3 |
| Visual evidence | `PASS` — DESIGN 6개, MOBILE Change 004 13개 |
| Backend·Persistent UAT | 미실행 — product source·runtime 변경 없음 |

## 6. Screenshot

- `tasks/design-000-screenshots/01-sales-home-desktop-1440.png`
- `tasks/design-000-screenshots/02-sales-kpi-desktop-1440.png`
- `tasks/design-000-screenshots/03-sales-kpi-mobile-390.png`
- `tasks/design-000-screenshots/04-sales-home-mobile-390.png`
- `tasks/design-000-screenshots/05-form-templates-desktop-1440.png`
- `tasks/design-000-screenshots/06-form-templates-mobile-390.png`
- `tasks/mobile-002-change-004-screenshots/*.png`

모든 증빙은 합성 사용자·프로젝트·금액을 사용하며 secret·실제 개인정보를 포함하지 않는다.

## 7. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `D000-TOKEN-DRIFT` | P2 | `RESOLVED` | 화면별 color·spacing·radius가 임의 증가 | semantic token layer 도입 |
| `D000-PRIMITIVE-DUPLICATION` | P2 | `RESOLVED` | page header·surface·toolbar·tab·badge 중복 | 공통 React primitive 5종 구현 |
| `D000-FIGMA-LIBRARY` | P3 | `BACKLOG` | code foundation과 별도로 Figma library가 아직 없음 | 실제 Figma publish 요청 시 별도 housekeeping |
| `M2-APP-MODULE-SIZE` | P3 | `BACKLOG` | 대형 route component 유지보수 비용 | 기존 mobile backlog 유지 |

Open P0/P1/P2는 `0/0/0`이다.

## 8. SOP

1. 새 화면은 `design-system`의 token과 primitive를 먼저 사용한다.
2. color·radius·shadow를 직접 추가하기 전에 기존 semantic token의 역할과 일치하는지 확인한다.
3. danger·success는 semantic state에만, primary blue는 active·main action에 사용한다.
4. desktop은 1440px full-height sidebar와 compact workspace, mobile은 390px single column·44px target·overflow 0을 확인한다.
5. token 변경 시 Home·Sales·양식 관리 desktop/mobile screenshot과 Frontend 전체 회귀를 실행한다.

## 9. User manual

사용자 동작은 바뀌지 않는다. 파란색은 현재 위치·주 action, 옅은 파랑은 선택 상태, 빨강·초록은 위험·성공 상태를 뜻한다. Mobile에서는 같은 시각 언어를 사용하되 관리 기능보다 현장 판단과 다음 action이 먼저 보인다.

## 10. User validation checklist

### 자동 검증

- [x] token stylesheet import·typecheck·build
- [x] 공통 primitive 5종과 Home·Sales adoption
- [x] desktop full-height sidebar·compact white workspace
- [x] mobile app bar·single column·44px target·overflow 0
- [x] Home·Sales·양식 관리 desktop/mobile screenshot

### 사용자 직접 검수

- [ ] reference와 색감·도형·목록·경계·낮은 그림자·배열 느낌 비교
- [ ] desktop Home·영업·양식 관리의 정보 밀도 확인
- [ ] mobile Home·영업·운영·관리 화면의 동일한 디자인 느낌 확인
- [ ] active blue와 semantic danger/success 구분 확인

상태: `자동 검증 완료 · 사용자 검수 대기 — 마지막 일괄 검수`.

## 11. Rollback·복구

experiment commit revert로 token·primitive·adoption·증빙을 함께 되돌린다. feature·DB·migration 변경이 없어 data rollback은 없다. 기존 screen-specific CSS는 token layer 아래에 남아 있어 forward-fix도 가능하다.

## 12. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서 8장 |
| User manual | 완료 | 본 문서 9장 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md) DESIGN-000·Decision Log |
| User validation checklist | 자동 완료·사용자 대기 | 본 문서 10장 |

## 13. 시행착오 및 폐기한 접근

- 기존 `styles.css` 전체를 한 번에 이관하는 방식은 회귀 범위가 커서 폐기하고 token layer + 우선 화면 adoption으로 시작했다.
- reference의 타사 logo·콘텐츠를 그대로 복제하지 않고 EMI 정보 구조와 기능을 보존한 채 시각 문법을 투영했다.
- 모든 상태를 rounded card로 통일하지 않고 status pill, count circle, data surface 역할을 분리했다.

## 14. 사용자 검수 결과와 남은 항목

- 자동·격리 visual 검증: 완료
- 사용자 직접 검수: 대기
- Figma library publish: 범위 밖 P3 후보
- 대표 repo·main·Persistent UAT·게시: 미반영

## 15. Change 004 — PC UX/UI 평가 반영

- 계약: [Change 004](design-000-change-004.md)
- 구현 보고: [Change 004 Implementation report](design-000-change-004-implementation-report.md)
- 결과: Change 003 PC 사용자 평가의 P1·P2를 공통 breadcrumb·permission/prerequisite banner·empty state·secondary tools·selection mode와 compact PC layout으로 보정했다.
- 검증: Frontend 136/136, typecheck, lint error 0, build, 1280×720 핵심 동선과 390×844 overflow 0.
- 상태: `EXPERIMENT_COMPLETE / USER_VALIDATION_PENDING`
