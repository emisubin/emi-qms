# TASK-UL891-PRODUCTION-PLAN-001 Change 002 구현 보고 — 기본계획 일괄 입력과 일정표 가독성

상태: `사용자 검수 완료 / main 병합 승인`

## 목적·배경·범위

- 목적: UL891 프로젝트의 생산계획을 모든 활성 세트에 한 번 입력한 뒤 필요한 세트만 개별 수정하고, 일정표의 계획·실적·날짜와 담당자를 한눈에 확인하게 한다.
- 포함: 프로젝트 기본계획 저장·일괄 적용·후속 세트 상속, 명시적 덮어쓰기, 생산계획 조회·수정 탭, Gantt 색·날짜 세로선·담당자 목록, desktop·390px 페이지 구조 보정.
- 제외: 생산계획 항목 구조·실적 원본 변경, 비-UL891 계획 정책, 알림·내 업무·권한 확장, Persistent UAT, 기존 5174/5081 runtime handover, 실제 provider, commit·push·PR·merge.

## 해결한 업무 문제

1. 첫 생산계획도 각 세트를 하나씩 선택해 반복 입력해야 해 세트 수만큼 같은 작업을 수행해야 했다.
2. 계획과 실적 막대 의미가 직관적이지 않고 날짜 경계가 약해 일정 위치를 빠르게 읽기 어려웠다.
3. 일정표 아래에 생산관리 담당자 목록이 없어 계획의 책임자를 화면 밖에서 다시 찾아야 했다.
4. 최초 UI 초안은 `전체 기본계획`에서도 저장되지 않는 프로젝트 공통 전달사항·담당자 입력을 노출하고 `세트 일정 · 0면`처럼 범위를 잘못 설명했다.
5. 실제 390px 화면에서 기존 3열 기본 조건 grid가 유지돼 수정사유와 수정 범위 탭이 화면 밖으로 넘쳤다.

## 구현 결과

1. UL891 생산계획 수정의 첫 탭을 `전체 기본계획`으로 열고 프로젝트 기본값을 한 번 저장할 수 있게 했다.
2. 기본 저장은 각 계획 항목이 완전히 비어 있는 활성 세트만 채운다. 이미 개별 수정한 세트 값은 보존한다.
3. `이미 개별 수정한 세트 일정도 기본계획으로 덮어쓰기`를 명시적으로 선택할 때만 기존 값을 덮어쓴다.
4. 기본계획 저장 뒤 추가되는 새 활성 세트는 최신 기본값을 자동 상속하고 이후에는 독립 수정할 수 있다.
5. 수정 범위를 `계획 구조 → 전체 기본계획 → 개별 세트 일정`으로 분리했다. 공통 전달사항·업무 담당자는 실제 저장되는 `계획 구조`에서만 편집한다.
6. 일정표의 계획 막대는 흰색+검은 테두리, 실적 막대는 검은색으로 표시한다.
7. 기간 길이에 따라 일간(31일 이하)·주간(120일 이하)·월간 세로선을 만들고 주요 날짜선을 더 진하게 표시한다.
8. 생산관리 담당자 목록을 Gantt 바로 아래에 표시한다.
9. mobile에서는 기본 조건과 범위 탭을 1열로 배치하고 checkbox를 18px 고정 크기로 보정해 가로 넘침을 제거했다.

## 기술적 결정과 검토한 대안

- 채택: 프로젝트 기본계획을 세트 overlay와 별도 테이블에 저장한다. 전체 기본값과 개별 수정값의 소유권이 분명하고 이후 생성 세트가 같은 기준을 상속할 수 있다.
- 폐기: 첫 번째 세트 값을 다른 세트로 복사하는 방식. 어느 세트가 기준인지 불명확하고 첫 세트의 개별 수정이 전체 정책으로 오인될 수 있다.
- 채택: 기본 동작은 `빈 항목만 적용`, 명시적 checkbox에서만 overwrite다. 반복 입력을 줄이면서 이미 조정한 세트를 보호한다.
- 폐기: 기본계획 저장 때 모든 세트를 무조건 덮어쓰는 방식. 사용자가 완료한 개별 일정이 예고 없이 유실된다.
- 채택: 프로젝트 공통 전달사항·담당자는 `계획 구조`, 기간·항목별 담당자·인원·코멘트는 기본/세트 일정으로 분리했다. 실제 API 저장 범위와 화면 입력 범위를 일치시킨다.
- 채택: 고정 픽셀 날짜선이 아니라 일정 기간에 따른 일/주/월 간격을 사용해 짧고 긴 프로젝트 모두 과밀하지 않게 한다.

## 아키텍처·영향

| 영역 | 영향 |
| --- | --- |
| DB/Migration | `0068` additive. 계획별 기본 header/value 테이블 추가, 기존 UL891 LinkedV1 프로젝트 계획값 backfill |
| Backend/API | `PATCH /api/projects/{projectId}/production-planning/set-defaults`, CAS·권한·validation·audit, 빈 세트 적용·overwrite·후속 세트 상속 |
| Frontend/UI·UX | 3단 수정 범위, 기본계획 editor, 실제 저장 범위 안내, Gantt 색·세로선·담당자 목록, 390px 1열 구조 |
| 권한/Workflow | 기존 생산관리 수정 권한과 프로젝트 Active gate 유지. 실적 원본·내 업무·알림 정책 변경 없음 |
| Excel/PDF/첨부 | N/A — 파일 입출력과 첨부 계약을 변경하지 않음 |
| 비-UL891 회귀 | 기존 프로젝트 단위·Legacy 계획 UI/API 흐름 유지 |

## 주요 변경 파일

- `database/migrations/0068_ul891_current_design_and_plan_defaults.sql`: 프로젝트 기본계획 테이블과 기존 값 backfill.
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningContracts.cs`: 기본계획 request/response.
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningEndpointExtensions.cs`: set-defaults route.
- `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningStore.cs`: 기본 저장, 빈 세트/overwrite, 후속 세트 상속, CAS·audit.
- `frontend/src/App.tsx`, `api.ts`, `styles.css`, `design-system/wireframe.css`: 수정 범위와 Gantt·담당자·반응형 UI.
- `backend/tests/Emi.Qms.Api.Tests/Ul891SetApiTests.cs`, `PostgreSqlMigrationTests.cs`: 적용·보존·상속·migration 회귀.
- `frontend/tests/App.test.tsx`와 `frontend/e2e/full-stack/ul891-user-corrections.full-stack.spec.ts`: 화면 계약·실제 desktop/mobile 동선.

## 시행착오 및 폐기한 접근

1. 첫 Backend 통합 검증에서 audit entity type을 새 이름으로 기록해 DB 제약에 걸렸다. 기존 허용 계약인 `ProductionPlanItem`으로 맞추고 전체 Backend 회귀로 확인했다.
2. 최초 기본계획 editor가 세트 editor 제목을 재사용해 `0면`으로 표시됐다. 프로젝트 전체 기본값 전용 제목·상속 안내로 교체했다.
3. 기본/개별 탭에 공통 전달사항과 업무 담당자 editor가 남아 있었지만 해당 저장 API는 그 값을 저장하지 않았다. 입력을 가장하지 않도록 `계획 구조에서 수정` 안내로 분리했다.
4. 디자인 시스템의 범용 input 최소 높이가 overwrite checkbox에도 적용돼 크게 늘어났다. 해당 checkbox만 18px 고정 크기로 제한했다.
5. mobile E2E에서 문서 폭이 viewport보다 넓은 문제가 발견됐다. overflow를 숨기지 않고 원인 element를 추적해 기존 3열 `.production-edit-controls`를 900px 이하에서 1열로 바꿨다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `UL891-PLAN-C002-F01` | P1 | RESOLVED | 모든 세트의 동일 초기 계획을 반복 입력해야 했다. | 프로젝트 기본계획·빈 세트 일괄 적용·후속 세트 상속을 구현했다. |
| `UL891-PLAN-C002-F02` | P2 | RESOLVED | 막대 색·날짜 경계·담당자가 한눈에 읽히지 않았다. | 계획 흰색/실적 검은색, 적응형 세로선, Gantt 아래 담당자 목록을 적용했다. |
| `UL891-PLAN-C002-F03` | P2 | RESOLVED | 기본 탭이 저장하지 않는 공통 입력과 잘못된 `0면` 범위를 노출했다. | 공통 편집은 계획 구조로 제한하고 기본계획 전용 제목·설명을 적용했다. |
| `UL891-PLAN-C002-F04` | P2 | RESOLVED | 실제 wireframe CSS에서 overwrite checkbox가 과도하게 커졌다. | 전용 fixed-size checkbox와 mobile 구조를 적용했다. |
| `UL891-PLAN-C002-F05` | P2 | RESOLVED | 390px에서 기존 3열 기본 조건이 viewport 밖으로 넘쳤다. | 900px 이하 1열 grid와 overflow element E2E 검증을 추가했다. |

Open P0/P1/P2: `0/0/0`.

## 자동 검증

| 검증 | 결과 |
| --- | --- |
| .NET Release build | PASS — warning/error `0/0` |
| Backend 전체 격리 PostgreSQL 회귀 | PASS — `482/482` |
| Backend UL891 기본계획 집중 회귀 | PASS — `1/1`, 빈 세트 적용·개별 보존·명시적 overwrite·후속 세트 상속·권한·stale CAS 포함 |
| Frontend 전체 unit | PASS — 22 files, `145/145` |
| Frontend typecheck | PASS |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend production build | PASS — 기존 500kB 초과 chunk warning 유지 |
| 격리 Full-Stack Chromium | PASS — `1/1`, Gantt 색·세로선·담당자 위치·기본 탭·desktop/390px·가로 넘침 0 |
| Git whitespace 검사 | PASS — `git diff --check` |

## 개인정보·secret 검토

- 검증은 격리 PostgreSQL과 명백한 개발 역할 계정을 사용하고 종료 시 DB/container/network를 제거했다.
- 문서에는 실제 고객·프로젝트·사용자 식별자, token, secret, tenant/provider 값을 기록하지 않았다.
- 외부 알림·실제 provider 호출과 Persistent UAT 데이터 변경은 수행하지 않았다.

## SOP — 적용·복구

1. 운영 적용 승인을 받은 경우 Backend/Frontend보다 먼저 additive migration `0068`을 적용한다.
2. UL891 LinkedV1 생산계획마다 기본계획 header/value가 생성됐는지 확인한다.
3. 검수 프로젝트에서 기본계획을 저장해 빈 활성 세트만 채워지고 개별 수정 세트가 보존되는지 확인한다.
4. 새 세트를 하나 추가해 기본계획 상속을 확인한 뒤 개별 세트 일정 수정이 독립적으로 저장되는지 확인한다.
5. 운영 적용 후 문제는 기본값·세트 overlay를 삭제하지 않고 forward-fix migration/code로 보정한다. 기존 계획·실적 이력은 유지한다.

## User manual — 사용자 사용 방법

1. 생산관리에서 프로젝트를 선택하고 `생산계획 수정`을 누른다.
2. 처음 열리는 `전체 기본계획`에 기간·항목 담당자·필요 인원·생산관리 코멘트를 입력하고 저장한다.
3. 기본 저장은 비어 있는 활성 세트만 채운다. 이미 수정한 세트도 바꾸려면 overwrite checkbox를 직접 선택한다.
4. `세트 일정`에서 필요한 세트를 선택해 그 세트만 다시 조정한다.
5. 프로젝트 공통 전달사항이나 업무 담당자를 바꾸려면 `계획 구조` 탭에서 수정한다.
6. 조회 화면에서는 흰색 막대를 계획, 검은색 막대를 실적으로 읽고 세로선을 날짜 경계로 사용한다. 생산관리 담당자는 일정표 바로 아래에서 확인한다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 desktop·390px 시각 검토는 완료했다.
- 2026-08-04 사용자가 누적 일정표·생산계획 수정을 확인하고 main 병합을 승인했다.
- Persistent UAT migration, 기존 5174/5081 runtime, 실제 provider는 승인 범위 밖이라 미실행이다.
- commit·push·PR·merge는 사용자 승인을 받았으며 게시 절차에서 실행한다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 작성됨 | 본 문서 `SOP — 적용·복구` |
| User manual | 작성됨 | 본 문서 `User manual — 사용자 사용 방법` |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 사용자 검수 완료 / main 병합 승인 | `tasks/ul891-production-plan-001-change-002-user-validation-checklist.md` |
