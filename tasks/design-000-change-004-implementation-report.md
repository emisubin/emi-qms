# DESIGN-000 Change 004 Implementation report — PC UX/UI 평가 반영

## 1. 요약과 상태

- Task: `DESIGN-000`
- Change: `change-004`
- Task 유형: `P2_REMEDIATION`
- 기준 HEAD: `de8e05b`
- 상태: `EXPERIMENT_COMPLETE / USER_VALIDATION_PENDING`
- 평가 기준: [PC UX/UI 사용자 관점 평가서](design-000-change-003-pc-ux-ui-evaluation.md)
- 변경 계약: [Change 004](design-000-change-004.md)
- 구현 범위: PC 정보 우선순위, 탐색, 읽기 전용 안내, 선택 의미, 입력 단계와 조회·편집 구분
- 제외: API·DB·migration·권한 계산·업무 상태 전이·알림 수명주기·신규 모바일 설계·대표 repo·`main`·게시

## 2. 해결한 사용자 문제

평가서의 P1·P2 항목을 현재 기능을 유지한 상태에서 다음과 같이 보정했다.

| 평가 Finding | 구현 결과 |
| --- | --- |
| 프로젝트·구매·자재 목록이 첫 화면 아래에 있음 | PC KPI·검색·필터·내보내기 영역의 높이와 간격을 압축했다. 1280×720에서 프로젝트·구매·자재 모두 실제 프로젝트 행이 첫 화면에 표시된다. |
| 프로젝트 상세의 부서 탭 도달이 늦음 | 기본정보를 핵심 6개 값과 `기본정보 전체 보기`로 분리하고 병목을 compact summary로 바꿨다. 부서 탭은 첫 화면 안에 배치하고 sticky 처리했다. |
| 권한 부족과 선행조건 부족을 구분하기 어려움 | `DsReadOnlyBanner`에 `permission`·`prerequisite` 의미를 분리하고 생산관리·구매·자재·제조·품질·물류에 공통 문구를 적용했다. |
| 현재 패널 선택과 다중 선택 checkbox가 혼재 | 제조·품질의 checkbox를 기본 상태에서 숨기고 `패널 선택 작업` 또는 `검사 패널 Excel 내보내기`를 눌렀을 때만 선택 모드·선택 수·종료 action을 표시한다. |
| 영업 홈에서 긴 그래프가 당일 업무보다 먼저 보임 | `내 업무 / 긴급 Pending / 긴급·차단 알림` 3개 우선 업무를 매출 그래프 위에 배치했다. |
| 뒤로가기·경로 표현이 화면마다 다름 | 공통 `DsBreadcrumbs`를 만들고 프로젝트 상세과 주요 프로젝트형 대시보드에 목적지가 보이는 경로를 적용했다. |
| 긴 생산계획 입력에서 시작 순서가 불분명 | 계획일과 담당자 구역을 접기형 단계로 바꾸고 validation focus가 오류가 있는 접힌 구역을 자동으로 연 뒤 입력으로 이동하게 했다. |
| 양식 조회가 disabled input처럼 보임 | 조회 모드는 일반 텍스트 preview, 편집 모드는 실제 input과 `초안 편집 중` 안내로 분리했다. |
| 빈 상태가 다음 행동을 안내하지 않음 | 공통 `DsEmptyState`를 도입하고 검색 초기화·신규 프로젝트·선행조건 안내를 연결했다. |
| Excel·고급 기능이 기본 action과 경쟁 | 프로젝트·생산관리·구매의 Excel·설정 기능을 `추가 기능` details 안으로 이동했다. |
| 장식성 영문과 오래된 안내가 남음 | 업무 정보에 기여하지 않는 영문 eyebrow를 한국어로 바꾸고 `후속 단계에서 제공됩니다` stale 안내를 제거했다. |

## 3. 공통 UX 기반

추가한 공통 component는 다음과 같다.

| Component | 용도 |
| --- | --- |
| `DsBreadcrumbs` | `업무 선택 → 업무 → 프로젝트` 이동 목적지 표시 |
| `DsReadOnlyBanner` | 권한 기반 조회 전용과 업무 선행조건 대기 구분 |
| `DsEmptyState` | 빈 이유, 다음 행동과 복구 action 제공 |
| `DsSecondaryTools` | Excel·설정 등 보조 기능을 기본 action과 분리 |
| `DsSelectionModeBar` | 현재 패널 탐색과 다중 선택 mode 분리 |
| `DsInputSection(collapsible)` | 긴 입력 구역의 progressive disclosure |

왼쪽 메뉴는 PC에서 `내 업무 / 부서 업무 / 공통 조회 / 관리` 네 그룹으로 분리했다. 현재 로그인 부서 메뉴를 `부서 업무` 첫 위치에 두고 다른 부서 메뉴는 제거하지 않은 채 `공통 조회`에 유지했다.

## 4. 화면별 결과

### Home

- PC 상단에 당일 행동 3개를 배치했다.
- 영업팀에서는 이 영역이 연간 매출 그래프보다 먼저 나온다.
- 공지사항 위치와 부서별 KPI·매출 계산은 변경하지 않았다.

### 프로젝트·구매·자재 목록

- project/search/filter/KPI/export vertical rhythm을 PC 전용으로 줄였다.
- 프로젝트 Excel 양식·업로드와 구매 Excel·설정은 `추가 기능`으로 이동했다.
- 자재 hero·검색·KPI·filter 간격을 줄여 실제 프로젝트 두 행을 첫 화면에 표시했다.
- 구매 화면은 grid gap을 4px로 조정해 실제 프로젝트 두 행을 720px 안에 표시했다.

### 프로젝트 상세

- 상태·고객사·Item·납기일·면수·진행률을 핵심정보로 유지했다.
- Code·영업담당자·포장·납품장소·FAT·판매금액은 펼침 정보로 이동했다.
- 병목은 큰 카드 대신 판단과 이동 action을 유지한 compact summary로 바꿨다.
- 부서 탭을 sticky 처리해 긴 부서 데이터 중에도 이동할 수 있게 했다.

### 생산관리·제조·품질·물류

- 프로젝트형 대시보드에 공통 breadcrumb·empty state·read-only 안내를 적용했다.
- 제조와 품질은 현재 패널을 여는 action과 Excel·일괄 처리를 위한 checkbox mode를 분리했다.
- 품질·제조의 업무 선행조건 안내는 권한 안내와 다른 `대기` 문법을 사용한다.
- 물류의 아이콘 단독 뒤로가기는 이동 목적지가 보이는 문구로 바꿨다.

### 양식 관리

- 활성 버전 조회 시 disabled input을 렌더링하지 않는다.
- 조회 상태는 항목명·안내·응답·필수·사진 조건을 텍스트 preview로 보여 준다.
- 편집을 시작한 초안만 input과 저장 action을 보여 주며 상단에 편집 상태를 표시한다.

## 5. Browser 검증

고정 검수 Frontend `http://127.0.0.1:42983`, Backend `http://127.0.0.1:41166`에서 실제 화면을 확인했다.

| 화면·조건 | 확인 결과 |
| --- | --- |
| 프로젝트 목록 1280×720 | 첫 행 `492.9px`, 수평 넘침 없음 |
| 구매 목록 1280×720 | 첫 행 `579.4px`, 둘째 행 하단 `714.4px`, 수평 넘침 없음 |
| 자재 입고 1280×720 | 첫 행 `575px`, 둘째 행 `642px`, 수평 넘침 없음 |
| 프로젝트 상세 1280×720 | 부서 탭 `503.4px`, 실제 탭 내용 `569.4px`, 병목 높이 `199.5px` |
| 제조 상세 | 기본 checkbox 0개, 선택 mode 시작 뒤 10개와 선택 목적·선택 수 표시 |
| 품질 LQC dashboard | 첫 프로젝트 행 `511.3px`, 수평 넘침 없음 |
| 품질 LQC 상세 | 세로 패널 목록 유지, permission·prerequisite 안내 분리, 수평 넘침 없음 |
| 양식 관리 활성 버전 조회 | disabled input 0개, text preview 6개, 편집 button 활성 |
| 영업 Home | 우선 업무 하단 `299.1px`, 매출 graph 시작 `323.1px`, 수평 넘침 없음 |
| 390×844 Project·Home 회귀 | mobile layout 유지, 좌상단 menu 유지, 수평 넘침 없음 |

Browser console error는 0건이었고 각 화면의 API 기반 목록·상세 loading이 정상 완료됐다. 검수 데이터는 local development fixture이며 외부 provider·Persistent UAT는 호출하지 않았다.

## 6. 자동 검증

| 검증 | 결과 |
| --- | --- |
| `npm run typecheck` | `PASS` |
| `npm run lint` | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| `npm test -- --run` | `PASS` — 22 files, 136/136 |
| `npm run build` | `PASS` — 기존 large chunk warning 유지 |
| `git diff --check` | `PASS` |
| Backend test | 미실행 — Backend source·API·DB·migration 변경 없음 |

## 7. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소·후속 |
| --- | --- | --- | --- | --- |
| `D000-C4-LIST-BELOW-FOLD` | P1 | `RESOLVED` | 요약·filter·export 누적으로 실제 업무가 첫 화면 아래에 위치 | 프로젝트·구매·자재 PC vertical density 조정, 720px 실측 |
| `D000-C4-DETAIL-LATE-TABS` | P1 | `RESOLVED` | 반복 기본정보와 큰 병목이 부서 data 진입을 지연 | 핵심정보 6개, 펼침 상세, compact 병목, sticky tabs |
| `D000-C4-PERMISSION-AMBIGUITY` | P1 | `RESOLVED` | disabled action만으로 권한과 선행조건 구분 불가 | 공통 permission·prerequisite banner 적용 |
| `D000-C4-SELECTION-AMBIGUITY` | P1 | `RESOLVED` | 패널 navigation과 batch/export checkbox 목적 혼재 | 명시적 selection mode와 선택 수·종료 action |
| `D000-C4-HOME-PRIORITY` | P1 | `RESOLVED` | 영업 graph가 당일 행동보다 먼저 노출 | 우선 업무 3개를 graph 위로 이동 |
| `D000-C4-FORM-VIEW-EDIT` | P2 | `RESOLVED` | 조회 상태의 disabled input이 오류·권한 부족처럼 보임 | text preview와 draft edit mode 분리 |
| `D000-C4-COMMON-NAV-EMPTY` | P2 | `RESOLVED` | 화면별 뒤로가기·빈 상태·보조 기능 표현 불일치 | breadcrumb·empty state·secondary tools 공통화 |
| `D000-C4-APP-SPLIT` | P3 | `BACKLOG` | 대형 `App.tsx`와 누적 stylesheet가 유지보수·bundle 비용 증가 | `DESIGN-001` 또는 별도 housekeeping에서 route split·code splitting |
| `D000-C4-ADMIN-IA` | P3 | `BACKLOG` | 관리자 home 카드 우선순위·보조 menu 개선은 P1/P2 입력 속도와 별도 범위 | 후속 관리자 IA change에서 수행 |

Open P0/P1/P2는 `0/0/0`이다.

## 8. SOP

1. 새 PC 프로젝트형 화면은 `breadcrumb → page header → permission banner → compact KPI/filter → project list` 순서를 사용한다.
2. 1280×720에서 첫 실제 project row와 수평 overflow를 확인한다.
3. 현재 입력 대상 선택과 batch/export 선택은 같은 control로 만들지 않는다.
4. 타 부서 조회는 `DsReadOnlyBanner(kind="permission")`, 선행조건 대기는 `kind="prerequisite"`를 사용한다.
5. Excel·설정은 핵심 업무 action이 아니면 `DsSecondaryTools`에 둔다.
6. 긴 입력은 기존 저장 책임을 유지하면서 `DsInputSection(collapsible)`로 분리하고 validation focus가 접힘을 열 수 있어야 한다.
7. 조회 상태는 disabled form으로 표현하지 않고 text preview를 사용한다.

## 9. User manual

- 왼쪽 메뉴는 현재 내 업무, 로그인 부서 업무, 다른 부서 조회, 관리 순서로 나뉜다.
- 다른 부서 화면 상단의 `조회 전용입니다.`는 오류가 아니라 조회만 가능한 현재 권한을 뜻한다.
- `대기` 안내는 권한 문제가 아니라 앞 업무가 끝나야 입력을 시작할 수 있다는 뜻이다.
- 제조·품질에서 패널 행을 누르면 현재 입력할 패널이 열린다.
- 여러 패널을 Excel로 내보내거나 일괄 처리하려면 `패널 선택 작업` 또는 `검사 패널 Excel 내보내기`를 먼저 눌러 checkbox mode를 시작한다.
- 생산계획의 접힌 구역은 제목을 눌러 열 수 있고, 입력 오류가 있으면 저장 시 해당 구역이 자동으로 열린다.
- 양식의 사용 중 버전은 읽기용 preview이며 `편집`을 누른 뒤 만들어진 초안에서만 입력할 수 있다.

## 10. User validation checklist

### 자동·브라우저 검증

- [x] 1280×720 프로젝트·구매·자재에 실제 프로젝트 행 표시
- [x] 프로젝트 상세 부서 탭이 첫 화면 안에 표시되고 sticky 동작
- [x] 읽기 전용과 선행조건 안내가 다른 문구·표식으로 표시
- [x] 제조·품질의 현재 패널 선택과 다중 선택 mode 분리
- [x] 영업 Home 우선 업무가 연간 graph보다 먼저 표시
- [x] 생산관리 접기형 입력과 오류 구역 자동 펼침
- [x] 양식 조회 preview와 초안 편집 input 분리
- [x] 프로젝트·Home 390px 수평 overflow 없음
- [x] Frontend 136/136·typecheck·lint(error 0)·build

### 사용자 직접 검수

- [ ] 현재 로그인 부서가 `부서 업무`에서 먼저 보이는지 확인
- [ ] 프로젝트·구매·자재에서 첫 화면의 정보량과 행 높이가 실무에 적당한지 확인
- [ ] 프로젝트 상세 핵심정보·병목·부서 탭의 우선순위 확인
- [ ] 타 부서 조회 전용 안내와 선행조건 대기 안내의 차이 확인
- [ ] 제조·품질 다중 선택 시작·종료가 오해 없이 동작하는지 확인
- [ ] 생산계획 접기와 양식 조회·편집 전환 확인

상태: `자동 검증 완료 · 사용자 검수 대기`.

## 11. Rollback

`DESIGN-000 change-004`의 Frontend와 문서 변경만 revert한다. API·DB·migration·업무 상태 전이를 바꾸지 않았으므로 data rollback은 없다.

## 12. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서 8장 |
| User manual | 완료 | 본 문서 9장 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md) DESIGN-000·Decision Log |
| User validation checklist | 자동 완료·사용자 대기 | 본 문서 10장 |
