# DESIGN-000 Change 006 구현 보고서

## 1. 결과와 상태

- Task 유형: `P2_REMEDIATION`
- 기준 HEAD: `4d15b7cee0d97f1846a1838500f9c9edf11b68bf`
- branch: `fix/design-000-graphite-promotion`
- 상태: `MAIN_MERGED / USER_VALIDATION_COMPLETE`
- 변경 계약: [DESIGN-000 Change 006](design-000-change-006.md)
- Roadmap 재정렬: 사용자가 Azure 비용 Gate를 보류하고 Change 006을 우선하도록 명시 승인
- 사용자 검수: 2026-08-01 완료
- Git 게시: 승격 commit `f68169d`, local `main` merge `f1f94ed` 완료. 2026-08-04 `TASK-EXPERIMENT-PROMOTION-001 Change 002`의 별도 사용자 승인으로 Azure 원격 기준선과 통합해 Ready PR·CI를 거쳐 원격 `main`에 반영

독립 Graphite 실험에서 Fable이 구현한 흑백 wireframe과 프론트엔드 구성 계약을 최신 `main` 기반 승격 branch에 선택적으로 이식했다. 시각 계층, 공통 상태·feedback·dialog·KPI·header, 생산관리와 내 업무·알림의 표 밀도, desktop/mobile 부서 탐색을 통일했다. 업무 선택 전용 페이지는 삭제하고 왼쪽 메뉴의 부서 행을 눌러 실제 업무 링크를 펼치는 흐름으로 단순화했다. 기존 업무·권한·API·DB·workflow와 상태 의미색은 유지했다.

## 2. 목적, 범위와 아키텍처 영향

| 영역 | 결과 |
| --- | --- |
| Frontend·UI/UX | Graphite token·primitive·대표 화면 composition, 표 밀도, 장식용 왼쪽 rail 제거와 부서 navigation 변경 |
| Routing | 업무 선택 전용 component 삭제. legacy 부서 root는 첫 실제 업무로 replace redirect |
| Backend·API | 변경 없음. 기존 읽기·mutation 계약과 개발 사용자 header를 그대로 사용 |
| DB·Migration | 변경 없음. schema·seed·migration을 실행하거나 수정하지 않음 |
| 권한·Workflow | 변경 없음. 실제 child deep link, `aria-current`, 업무·상태 전이를 보존 |
| Excel·PDF·첨부파일 | 변경 없음. export/import, 생성물과 업로드 계약을 수정하지 않음 |
| 배포·Azure·실제 provider | 변경 없음. Azure 비용 Gate는 보류 상태이며 resource·설정·traffic을 조작하지 않음 |

포함 범위는 Change 006 fixed allowlist의 Frontend source·test와 Task 문서뿐이다. 독립 실험의 brief, Fable planning/report, screenshot artifact와 Git history, Figma publish, `App.tsx` route 분할은 제외했다.

## 3. 구현 내용과 주요 결정

| Finding | 구현 결과 |
| --- | --- |
| `DESIGN-GRAPHITE-VISUAL-COHESION` | 흑백 surface·type·spacing·border·control 계층을 Graphite token과 공통 primitive로 통일하고 상태 의미색은 보존 |
| `DESIGN-GRAPHITE-LEFT-RAIL` | 오류/검토 배너를 포함한 장식용 비대칭 왼쪽 rail을 제거하고 일반 1px border·배경·텍스트 tone으로 상태를 표현 |
| `DESIGN-GRAPHITE-TABLE-DENSITY` | 생산계획·제조 투입 desktop header 36~40px·단일 행 44~52px 계약, 내 업무·알림 wrapper 100%·fixed-layout·detail 잔여 폭·action 행 48~52px 계약 추가 |
| `DESIGN-GRAPHITE-DEPARTMENT-NAVIGATION` | `operationalHubConfigs`를 단일 source로 부서 행 전체 click disclosure, 초기 전체 접힘, 단일 펼침과 child 선택 후 mobile drawer 닫힘 구현 |
| `DESIGN-GRAPHITE-WORK-SELECTION` | `DepartmentWorkHub.tsx`와 일반 사용자용 업무 선택 화면·복귀 동선을 삭제하고 legacy 부서 root만 첫 실제 업무 redirect로 보존 |

부서명과 별도 chevron으로 target을 나누지 않는다. 사용자는 부서 행을 눌러 하위 업무를 펼치고 child를 선택해 바로 이동한다. 전용 업무 선택 페이지는 존재하지 않으며 기존 `/production-planning`, `/materials`, `/quality`, `/logistics` deep link는 로그인·shell 준비 뒤 각 부서의 첫 실제 업무로 안전하게 연결된다.

## 4. 변경 파일

| 파일 | 역할 |
| --- | --- |
| `frontend/src/App.tsx` | desktop/mobile 부서 disclosure, legacy root redirect, 생산계획·제조 투입·내 업무·알림 table composition |
| `frontend/src/DepartmentWorkHub.tsx` | 업무 선택 전용 component 삭제 |
| `frontend/src/HomePage.tsx` | Graphite Home hierarchy와 공통 composition |
| `frontend/src/MaterialsWorkspace.tsx` | 자재 업무 layout·상태 composition 통일 |
| `frontend/src/NotificationPreferenceAuditPage.tsx` | 감사 화면 상태·표면 composition 통일 |
| `frontend/src/OperationalProjectDashboard.tsx` | 대표 업무 dashboard composition 통일 |
| `frontend/src/PendingPage.tsx` | Pending 화면 Graphite 상태·구성 통일 |
| `frontend/src/PendingTypeManagementPage.tsx` | Pending 유형 관리 구성 통일 |
| `frontend/src/design-system/components.tsx` | 공통 상태·feedback·dialog·KPI·header primitive |
| `frontend/src/design-system/index.ts` | Graphite primitive export |
| `frontend/src/design-system/wireframe.css` | Graphite token, desktop/mobile layout, table density와 left-rail 제거 계약 |
| `frontend/e2e/mock-ui/panel-kitting-smoke.spec.ts` | 현재 부서 navigation·대표 route mock smoke |
| `frontend/tests/App.test.tsx` | 선택 화면 제거와 기존 업무 흐름 회귀 |
| `frontend/tests/App.navigation.test.tsx` | desktop/mobile disclosure·child route·drawer 회귀 |
| `frontend/tests/design-system-state.test.tsx` | 공통 상태·feedback·dialog·KPI·header 회귀 |
| `frontend/tests/graphite-table-contracts.test.ts` | 행 높이·열 너비·비대칭 rail CSS 계약 |
| `tasks/design-000-change-006.md` | Task identity, allowlist와 구현·검증 계약 |
| 본 문서 | 실제 구현·검증·Finding·rollback·검수 원장 |
| `docs/00-product-roadmap.md` | 사용자 검수·local merge 승인과 다음 Azure Gate 상태 |
| `docs/27-experiment-task-ledger.md` | 완료 실험과 Graphite 승격 상태 분리 |

## 5. 자동·브라우저 검증

| 검증 | 결과 |
| --- | --- |
| Frontend lint | `PASS` — error 0, warning 0 |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 25 files, `170/170` |
| Frontend production build | `PASS` — 기존 500KB 초과 chunk warning 유지 |
| mock E2E | 승격 frontend를 임시 격리 port에서 `PASS 4/4`; 임시 config와 덮어쓴 screenshot 원복 |
| desktop browser | 1440×900 생산계획·제조 투입, 초기 부서 4개 접힘, 부서 행 click 뒤 child 직접 이동, page overflow 0, 업무 선택 hub 0 |
| 390×844 browser | 초기 접힘, 단일 부서 펼침, child 직접 이동 뒤 drawer 닫힘과 업무 선택 hub 0을 Fable 구현 검증 및 mock E2E로 고정 |
| decorative rail contract | 장식용 asymmetric left border·inset shadow·`::before` rail 0; timeline/grid/Gantt의 의미 선은 보존 |
| Backend readiness | 기존 격리 검수 Backend ready HTTP 200. Backend source·process는 변경하지 않음 |
| source equivalence | 실험 저장소의 최신 Fable 구현과 승격 allowlist byte equivalence 확인 |
| dependency·Backend·DB·migration·deploy diff | `0` |

연결된 검수 Backend의 일부 목록은 빈 fixture였으므로 실제 데이터가 많은 행의 수치 실측은 CSS contract test와 mock E2E로 보완했다. 사용자는 2026-08-01 실제 검수 결과를 완료로 확정했다.

## 6. Finding gate와 잔여 위험

| Finding | Severity | 상태 | 원인·영향과 처리 |
| --- | --- | --- | --- |
| `DESIGN-GRAPHITE-VISUAL-COHESION` | P2 | `RESOLVED` | Graphite token·primitive·대표 화면 구성을 fixed allowlist로 이식 |
| `DESIGN-GRAPHITE-LEFT-RAIL` | P2 | `RESOLVED` | 장식용 왼쪽 강조 rail을 제거하고 focused contract test로 고정 |
| `DESIGN-GRAPHITE-TABLE-DENSITY` | P2 | `RESOLVED` | 표별 행·열 drift를 CSS contract와 집중 test로 고정 |
| `DESIGN-GRAPHITE-DEPARTMENT-NAVIGATION` | P2 | `RESOLVED` | 클릭형 부서 disclosure와 desktop/mobile 규칙 구현 |
| `DESIGN-GRAPHITE-WORK-SELECTION` | P2 | `RESOLVED` | 전용 선택 page 삭제, child 직접 이동과 legacy redirect 검증 |
| `DESIGN-PROMOTION-SEQUENCE-001` | P2 | `RESOLVED` | 사용자 명시 재정렬 승인과 Decision Log로 Azure Gate 선행 예외 해소 |
| `DESIGN-PROMOTION-E2E-PORT-REUSE` | P2 | `RESOLVED` | 승격 source용 임시 격리 port에서 `4/4` 재검증하고 임시 artifact 제거 |
| `DESIGN001-F01` | P3 | `BACKLOG` | 기존 `App.tsx`와 production chunk가 500KB 초과. route split은 Change 006 제외 범위 |

Open P0/P1/P2: `0/0/0`. P3는 기존 stable label로 추적한다.

## 7. 개인정보·secret·artifact 검토

- 테스트 결과는 역할·count·boolean·dimension·HTTP status projection만 기록했다.
- 실제 사용자명, 이메일/UPN, tenant/client/object ID, token, Authorization header와 고객·프로젝트 원문을 기록하지 않았다.
- 계획 밖 screenshot, Playwright 임시 config와 테스트가 덮어쓴 기존 screenshot은 Repository 변경에서 제거·원복했다.
- dependency·Backend·DB·migration·deploy diff는 0건이다.

## 8. SOP, rollback과 복구

### 로컬 검수 SOP

1. 기존 격리 Backend readiness를 확인한다.
2. 승격 worktree에서 `VITE_AUTH_MODE=Dev`, frontend port 5197, 해당 Backend proxy target으로 Vite를 기동한다.
3. `http://127.0.0.1:5197/production-planning/releases`에서 Graphite 표면과 왼쪽 부서 disclosure를 확인한다.
4. 부서 행을 눌러 child가 나타나고, child를 누르면 전용 선택 page 없이 정확 업무로 이동하는지 확인한다.
5. 내 업무·알림과 390px drawer에서 행·열·drawer close를 확인한다.

### Rollback

Change 006 local merge commit을 revert하면 Frontend source·test·문서만 원복된다. DB·migration·Backend·외부 provider rollback은 적용 대상이 아니다. 원격·배포에는 아직 반영하지 않는다.

## 9. 사용자 매뉴얼과 검수 체크리스트

- [x] desktop 왼쪽 메뉴의 부서 행을 눌러야 하위 업무가 펼쳐지고 한 번에 한 부서만 열리는지 확인
- [x] 별도 chevron target과 업무 선택 전용 page가 없고 child가 실제 업무로 직접 이동하는지 확인
- [x] 390px 메뉴는 처음 접혀 있고 child 선택 뒤 drawer가 닫히는지 확인
- [x] 생산계획·제조 투입 목록의 header와 일반 행이 과도하게 높거나 줄바꿈되지 않는지 확인
- [x] 내 업무·알림의 detail·action 열 너비와 그룹별 열 정렬을 확인
- [x] 오류/검토 배너와 카드·상태·hero에 장식용 왼쪽 강조 rail이 남지 않았는지 확인
- [x] Home·Pending·관리 화면의 흑백 wireframe이 같은 시각 계층으로 보이는지 확인

상태: `자동 검증 완료 / 사용자 검수 완료 / local·원격 main 반영 완료`.

## 10. 개발 블로그 소재

### 10.1 해결한 업무 문제

흑백 wireframe의 장식 drift를 줄이고, 자주 보는 표의 정보 밀도와 다중 업무 부서의 진입 시간을 함께 개선했다. 상태·feedback·dialog·KPI·header와 navigation 규칙을 하나의 frontend contract로 묶었다.

### 10.2 기술적 결정과 검토한 대안

별도 부서 선택 화면을 유지하지 않고 왼쪽 메뉴의 부서 행을 disclosure로 사용했다. 부서 단위 정보가 필요한 새 화면을 만들지 않으면서 반복 사용자가 실제 업무로 바로 이동하고, legacy root만 호환 redirect로 남긴다.

### 10.3 시행착오 및 폐기한 접근

별도 chevron과 부서명 navigation을 나눈 split-target, active route 자동 펼침, 항상 펼침 방식은 최종 사용자 요청과 맞지 않아 폐기했다. 최종 계약은 초기 접힘과 부서 행 click disclosure다. 기본 mock E2E의 기존 server 재사용 문제는 승격 source 전용 임시 port로 분리해 해결했다.

### 10.4 사용자 검수 결과와 남은 항목

사용자는 2026-08-01 업무 선택 전용 page 삭제, 클릭형 왼쪽 메뉴와 Graphite 디자인의 검수를 완료하고 local `main` 병합을 승인했다. 승격 commit `f68169d`를 local `main` merge `f1f94ed`로 반영했고, 2026-08-04 별도 `1단계` 승인에 따라 Azure 원격 기준선과 통합해 Ready PR·CI를 거쳐 원격 `main`에 반영했다. 실제 Azure 재배포는 별도 경계로 남아 있다.

## 11. 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | 8절 |
| User manual | 포함됨 | 9절 |
| Roadmap update | 작성됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 완료 | 9절 |
