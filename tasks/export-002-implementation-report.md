# TASK-EXPORT-002 구현 보고서 — 선택 프로젝트 Excel 내보내기

## 1. 상태와 안전 경계

- Task: `TASK-EXPORT-002`
- 유형: `NEW_FEATURE` — `experiment/*` Fable 2-pass fast-track
- Branch: `experiment/task-export-002-selected-project-export`
- 시작 commit: `5eeb1fd1f3e369b5054e1c4b3cd8b1c2bd60f1e4`
- 기준 `main`·`origin/main`: `b8f3e2104074d05c2e71999c08a7374e8729f68f`, 변경 없음
- Task Identity Gate: `PASS_CREATE`; 동일 purpose Task·branch·worktree·PR 0건
- Roadmap Sequence Gate: canonical 다음 Task는 `TASK-007A`이지만 사용자의 experiment fast-track 재정렬 승인으로 `explicitRoadmapOverrideApproved=true`
- 현재 단계: 구현·자동 검증·screenshot 완료, 사용자 검수 대기
- Git 경계: local experiment commit만 승인. push·PR·merge 미승인, main merge 승인 `0/3`
- Persistent UAT·실제 provider·대표 repo·runtime handover: 변경 없음

## 2. 기획과 Fable 경계

- Interview source: `tasks/export-002-interview.md`
- Fable 1차 planning 원문: `tasks/export-002-planning.md`
- Codex review: `tasks/export-002-review.md`
- Fable 2차 planning 원문·최종 구현 계약: `docs/23-selected-project-export-plan.md`
- 승인 고정: `tasks/export-002-change-001.md`
- Fable Task session cleanup: `FABLE_TASK_SESSION_CLEANED`; session 1개와 transcript 1개를 Repository 밖 private state에서 제거
- 1차·2차 Fable 원문은 Codex가 수정하지 않았다.

### Claude/Fable 사용량

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 17:40 KST 초기화 | 4% 사용 / 96% 잔여 / 07-25 07:59 KST 초기화 | 8% 사용 / 92% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 17:40 KST 초기화 | 16% 사용 / 84% 잔여 / 07-25 08:00 KST 초기화 | 11% 사용 / 89% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 16% 사용 / 84% 잔여 / 17:39 KST 초기화 | 6% 사용 / 94% 잔여 / 07-25 07:59 KST 초기화 | 11% 사용 / 89% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 16% 사용 / 84% 잔여 / 17:40 KST 초기화 | 6% 사용 / 94% 잔여 / 07-25 08:00 KST 초기화 | 11% 사용 / 89% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 16% 사용 / 84% 잔여 / 17:40 KST 초기화 | 6% 사용 / 94% 잔여 / 07-25 08:00 KST 초기화 | 11% 사용 / 89% 잔여 / 초기화 parse 불가 |

Reporter가 동일 주간 구간에서 1차 직후 16%와 2차 직전 6%를 반환했다. 값을 보정하거나 추정하지 않고 실제 출력대로 보존했다.

## 3. 해결한 업무 문제

기존에는 현재 검색·필터 결과 전체를 Excel로 내보낼 수만 있어, 사용자가 필요한 프로젝트 몇 개만 전달하려면 파일을 연 뒤 불필요한 행을 직접 지워야 했다. 프로젝트 목록에서 여러 항목을 명시적으로 선택하고 기존 권한·파일 형식을 그대로 적용한 단일 workbook으로 내려받도록 해 이 수작업을 제거했다.

## 4. 구현 결과와 아키텍처 영향

### Frontend·UI/UX

- Desktop 프로젝트 목록 첫 열에 개별 checkbox를 추가하고 공통 선택 tray의 `전체선택` 하나로 visible list 전체 선택·indeterminate를 제공한다.
- Mobile은 desktop table 축소판이 아닌 카드별 checkbox와 목록 위 compact inline tray를 사용한다. fixed bottom action은 만들지 않았다.
- 선택 수, `선택 Excel`, `선택 해제`를 표시하며 0건이면 내보내기를 비활성화한다.
- 검색·상태 tab·납기 조건·새 목록 response에서 선택을 초기화하고 export 성공·실패 때는 재시도를 위해 선택을 유지한다.
- export 진행 중 selection을 잠그고 실행 시점 ID snapshot만 POST한다.
- checkbox click/Space와 기존 행 상세 click/Enter/Space를 분리했다.
- 선택 422에는 “목록을 새로고침한 뒤 다시 선택”을 안내하고 기존 전체 filter export의 오류 문구는 보존했다.

### API·Backend·권한

- `POST /api/projects/export/selected`와 JSON `{ "projectIds": ["..."] }` 계약을 추가했다.
- 문자열 ID를 수동 parse해 missing·empty·malformed·duplicate·101건 이상을 안정적인 422로 반환한다.
- `ProjectRead`, 기존 project scope, soft-delete 제외 조건을 한 query에 적용한다.
- requested unique count와 조회 count가 정확히 일치할 때만 workbook을 만들며 scope 밖·삭제·stale ID가 하나라도 있으면 generic 422, file 0건, audit 0건으로 끝난다.
- 기존 workbook builder·allowlist·typed value·formula safety·2-slot resource fence를 재사용한다.
- 매출 권한이 없으면 매출 컬럼 자체를 제외한다.

### DB·Migration·Audit

- Migration: `0039_selected_project_export_audit.sql`
- 방식: additive·forward-fix. 이미 반영된 `0038`은 수정하지 않았다.
- export kind constraint에 `ProjectsSelected`를 추가하고 민감 매출 컬럼 flag를 `Projects`와 `ProjectsSelected`에만 허용하도록 check constraint를 재생성한다.
- 성공한 export에만 kind, 선택 row count, `filtersApplied=false`, 민감 매출 컬럼 포함 여부를 append-only audit에 기록한다.
- 요청 ID·프로젝트명·파일 bytes는 audit에 기록하지 않는다.

### Excel·기존 기능 회귀

- 기존 프로젝트 sheet·컬럼 순서·형식과 formula-safe text를 재사용한다.
- 요약은 식별자를 넣지 않고 `선택 프로젝트 N건`만 표시한다.
- 기존 프로젝트 filter 전체 export, 구매 dashboard export, 내 업무 export는 optional helper 확장으로 기존 GET 동작을 유지한다.
- PDF·첨부파일: `N/A` — 이번 Task에서 변경하지 않았다.

## 5. 실제 변경 파일

### Product source

- `backend/src/Emi.Qms.Api/DataExports/DataExportEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/DataExports/ExcelExportService.cs`
- `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs`
- `database/migrations/0039_selected_project_export_audit.sql`
- `frontend/src/App.tsx`
- `frontend/src/ExcelExportAction.tsx`
- `frontend/src/api.ts`
- `frontend/src/styles.css`

### 검증

- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/ProjectRegistrationApiTests.cs`
- `frontend/tests/App.test.tsx`
- `frontend/tests/ExcelExportAction.test.tsx`
- `frontend/e2e/full-stack/selected-project-export.full-stack.spec.ts`
- `frontend/e2e/full-stack/excel-export.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-compact-workspaces.full-stack.spec.ts`

### Task·기획·증빙

- `docs/00-product-roadmap.md`
- `docs/23-selected-project-export-plan.md`
- `tasks/export-002-interview.md`
- `tasks/export-002-planning.md`
- `tasks/export-002-review.md`
- `tasks/export-002-change-001.md`
- `tasks/export-002-implementation-report.md`
- `tasks/export-002-screenshots/*.png`

## 6. 실행한 검증과 결과

| 검증 | 결과 |
| --- | --- |
| Backend Release build | `PASS` — warning 0, error 0 |
| Backend 전체 test | `PASS` — 385/385 |
| 선택 export focused API·workbook·permission·audit test | `PASS` |
| PostgreSQL migration focused test | `PASS` — 39 migrations, latest `0039` |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 90/90 |
| Frontend production build | `PASS` — 기존 large bundle warning 유지 |
| 선택 export isolated Full-Stack E2E | `PASS` — desktop·390px, 3건 중 2건 선택, workbook·audit 확인 |
| 기존 전체 Excel export 영향 E2E | `PASS` |
| mobile compact 영향 E2E | `PASS` — 선택 action 포함 visible button 44px 이상 |
| Full Full-Stack suite | `PARTIAL` — 24/34 pass; 아래 Task 비관련 기존 10개 시나리오 실패 |
| 실제 `.xlsx` 압축 무결성 | `PASS` — `unzip -t`, 오류 0 |
| 실제 Microsoft Excel 시각 확인 | `PASS` — 2개 선택 row·`선택 프로젝트 2건` 확인 후 workbook 닫음 |

Full suite의 나머지 실패는 Home/Pending 생성, IQC menu, mobile navigation, kitting procurement, pending strict label, bottleneck, project registration IQC 시나리오에 분포했다. 선택 export 시나리오에서 처음 발견한 기존 export strict selector와 mobile touch target은 이 Task 안에서 수정한 뒤 관련 3개 E2E를 모두 재통과했다. 나머지 10개는 선택 export source·route·workbook과 무관해 범위를 확대하지 않았다.

### 미실행 검증

- Persistent UAT migration·runtime handover: 미실행 — experiment 승인 범위 밖이며 사용자도 main·Persistent UAT 반영을 승인하지 않았다.
- 실제 조직 사용자·고객 data 검수: 미실행 — privacy-safe isolated synthetic data만 사용했다.
- CI: 미실행 — push·PR이 미승인이라 remote CI가 생성되지 않았다.
- Standalone spreadsheet artifact loader: 현재 제공 도구에 dependency loader가 없어 사용하지 못했다. 대신 application의 ClosedXML test, ZIP/XML 검사와 실제 Microsoft Excel 표시를 함께 검증했다.

## 7. Screenshot과 생성 파일

모든 화면과 workbook은 격리 E2E의 synthetic project data만 사용했다.

| 증빙 | 크기·상태 | 위치 |
| --- | --- | --- |
| Desktop 선택 화면 | 1440×1541 | `tasks/export-002-screenshots/01-selected-projects-desktop-1440.png` |
| Mobile 선택 화면 | 390×1745 | `tasks/export-002-screenshots/02-selected-projects-mobile-390.png` |
| 실제 Excel 화면 | 1393×768 | `tasks/export-002-screenshots/03-selected-projects-excel.png` |
| 실제 `.xlsx` | 7,779 bytes, ZIP 무결성 PASS | `tasks/export-002-screenshots/selected-projects.xlsx` |

Excel screenshot을 생성한 뒤 `Cmd+W`로 workbook을 닫고 Microsoft Excel이 최근 파일 시작 화면만 표시하는 상태를 확인했다. screenshot과 synthetic `.xlsx`는 사용자 검수 증빙으로 저장소에 함께 보존한다.

## 8. 개인정보·secret 검토

- screenshot·workbook·E2E는 synthetic project code·title만 사용했다.
- 실제 사용자·고객·프로젝트·tenant/client/object ID, token, secret, Authorization 원문을 tracked 문서에 기록하지 않았다.
- 422 응답은 누락·scope 밖·삭제 ID를 구분하거나 원문 ID를 노출하지 않는다.
- stage 전후 explicit allowlist와 cached diff에 대해 secret·PII·migration·generated artifact를 재검사한다.

## 9. Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `SELECTED-EXPORT-ATOMIC-SCOPE` | P1 | `RESOLVED` | 일부 ID만 조회해 파일을 만들면 scope 정보 노출·불완전 파일 위험 | 단일 scope query, exact count, generic 422, file/audit 0건 |
| `SELECTED-EXPORT-ROW-KEYBOARD-CONFLICT` | P2 | `RESOLVED` | checkbox Space가 행 상세 열기로 전파될 수 있음 | interactive target guard와 checkbox event 분리 |
| `SELECTED-EXPORT-STABLE-BODY-VALIDATION` | P2 | `RESOLVED` | framework binding에 따라 400/422가 흔들릴 수 있음 | 문자열 배열 수동 validation과 stable 422 |
| `SELECTED-EXPORT-AUDIT-MIGRATION` | P2 | `RESOLVED` | 0038 constraint가 신규 audit kind를 거부 | additive `0039`로 kind·민감 flag constraint 확장 |
| `SELECTED-EXPORT-ERROR-RECOVERY` | P2 | `RESOLVED` | 기존 422 hint가 stale selection 복구에 부정확 | 선택 action 전용 reload/reselect hint |
| `SELECTED-EXPORT-REQUEST-SNAPSHOT` | P2 | `RESOLVED` | export 도중 선택 변경 시 화면과 POST body 불일치 | 실행 snapshot과 selection lock |
| `SELECTED-EXPORT-SELECTION-LIFECYCLE` | P2 | `RESOLVED` | filter 변경 뒤 보이지 않는 ID가 남을 수 있음 | 새 response·filter/tab/date/reload에서 clear, export 후 유지 |
| `SELECTED-EXPORT-MOBILE-ACTION-POSITION` | P3 | `RESOLVED` | fixed bottom action이 mobile 내용을 가릴 수 있음 | 카드 목록 위 compact inline tray |
| `SELECTED-EXPORT-EXISTING-ACTION-REGRESSION` | P3 | `RESOLVED` | POST 지원이 기존 GET export를 깨뜨릴 수 있음 | optional backward-compatible 확장과 기존 E2E 재통과 |
| `FULL-STACK-BASELINE-UNRELATED-FAILURES` | P3 | `RESOLVED` | 이 Task 당시 전체 34개 중 다른 업무 영역 10개 시나리오가 현재 기준선과 불일치 | 후속 `TASK-E2E-FULL-SUITE-001`에서 최신 계약으로 보정하고 현재 suite `35/35` 통과 |

Open P0/P1/P2는 `0/0/0`이다.

## 10. 종료 산출물 5종 추적

| 산출물 | 상태 | canonical 위치 |
| --- | --- | --- |
| Implementation report | `작성 완료` | 본 문서 |
| SOP | `작성 완료` | 본 문서 `## 11. SOP` |
| User manual | `작성 완료` | 본 문서 `## 12. User manual` |
| Roadmap update | `작성 완료` | `docs/00-product-roadmap.md`의 실행 큐·TASK-EXPORT-002·추적 89·Decision Log |
| User validation checklist | `자동 검증 완료 · 사용자 검수 대기` | 본 문서 `## 13. User validation checklist` |

## 11. SOP

1. 프로젝트 목록에서 2개 이상의 non-deleted 프로젝트를 선택한다.
2. 선택 tray의 count와 tray `전체선택`·indeterminate 상태를 확인하고 같은 의미의 checkbox가 한 개뿐인지 확인한다.
3. `선택 Excel`을 실행하고 download 중 checkbox·선택 해제가 잠기는지 확인한다.
4. workbook의 프로젝트 sheet가 선택한 row만 포함하고 요약이 `선택 프로젝트 N건`인지 확인한다.
5. 매출 권한이 없는 역할에서는 매출 컬럼 자체가 없는지 확인한다.
6. stale·scope 밖·삭제 ID가 섞인 요청은 generic 422이며 파일과 audit가 생성되지 않는지 확인한다.
7. 정상 요청은 audit kind `ProjectsSelected`, 선택 row count와 민감 컬럼 flag를 기록하는지 확인한다.
8. 관련 격리 E2E는 `bash scripts/e2e-full-stack.sh e2e/full-stack/selected-project-export.full-stack.spec.ts`로 실행한다.

운영 migration 적용은 이 실험의 범위가 아니다. 향후 별도 승인 시 기존 `0038`을 수정하지 않고 `0039`를 순서대로 적용하며, 문제가 있으면 history rewrite 대신 새 additive forward-fix migration을 만든다.

## 12. User manual

- 프로젝트 목록에서 필요한 행 또는 모바일 카드의 checkbox를 누른다.
- 현재 화면에 표시된 항목을 모두 고르려면 목록 위 선택 영역의 `전체선택` checkbox를 사용한다.
- 목록 위 `N개 선택` 영역에서 `선택 Excel`을 누르면 고른 프로젝트만 한 파일에 담긴다.
- 선택을 처음부터 다시 하려면 `선택 해제`를 누른다.
- 내보내기 성공·실패 후에도 선택은 유지되므로 같은 파일을 다시 받거나 오류를 복구할 수 있다.
- “목록이 변경되었습니다” 안내가 나오면 목록을 새로고침한 뒤 다시 선택한다.
- 검색, 상태 tab 또는 납기 조건을 바꾸면 이전 선택은 안전하게 초기화된다.
- 삭제 보관함에서는 선택 내보내기를 제공하지 않는다.

## 13. User validation checklist

### 자동 검증

- [x] Desktop 개별 선택·현재 목록 전체 선택·indeterminate 확인
- [x] Mobile 카드 checkbox·inline tray·44px touch target·horizontal overflow 없음 확인
- [x] 3건 중 2건만 workbook에 포함되고 비선택 1건이 제외됨을 확인
- [x] missing·empty·malformed·duplicate·101건 이상 stable 422 확인
- [x] scope 밖·stale·삭제 선택의 generic 422, file 0건, audit 0건 확인
- [x] 매출 권한별 workbook column omission 확인
- [x] formula-like text가 수식 cell이 되지 않음을 확인
- [x] 성공 audit `ProjectsSelected`·row count·sensitive flag 확인
- [x] 기존 전체 filter Excel export 회귀 확인
- [x] 실제 Microsoft Excel 화면 확인과 workbook close 확인

### 사용자 직접 검수

- [ ] Desktop에서 checkbox·선택 tray의 위치와 조작감 확인
- [ ] Mobile에서 카드 선택과 inline tray의 글씨·도형·정렬 확인
- [ ] 제공된 실제 `.xlsx`가 원하는 2개 프로젝트만 담는지 확인
- [ ] 기존 `Excel 내보내기`와 신규 `선택 Excel`의 차이가 직관적인지 확인
- [ ] 실패 후 선택 유지와 `선택 해제` 복구 흐름 확인

## 14. Rollback·복구

- local experiment commit을 revert하면 source·test·Task artifact를 함께 되돌릴 수 있다.
- `0039`가 Persistent UAT에 적용되지 않았으므로 현재 data rollback은 `N/A`다.
- 향후 적용 뒤 문제가 발생하면 `0039` history를 수정하지 않고 새 additive migration으로 이전 constraint를 복원하거나 수정한다.
- 대표 repo·GitHub main에는 반영하지 않았으므로 main rollback은 `N/A`다.

## 15. 기술적 결정과 검토한 대안

- 채택: 현재 visible list 최대 100건을 명시 선택하고 server가 전부-or-전무로 재검증한다. 사용자 의도와 권한 경계를 모두 단순하게 유지한다.
- 보류: pagination 전체·filter 결과 전체를 server-side 선택. 보이지 않는 항목의 동시 변경과 대량 job 정책이 필요하다.
- 채택: 기존 workbook builder와 permission-based column allowlist 재사용. 별도 multi-sheet report를 만드는 것보다 형식 일관성과 회귀 위험이 낮다.
- 폐기: 누락 ID만 제외하고 부분 파일 생성. 사용자가 불완전한 파일을 완전한 결과로 오해할 수 있다.

## 16. 시행착오 및 폐기한 접근

- 전체 E2E에서 기존 `Excel 내보내기`와 `선택 Excel`이 부분 문자열 selector로 충돌했다. 기존 selector를 정확한 accessible name으로 고정했다.
- mobile compact 검증에서 기존 project export button이 44px 기준보다 작았다. 공통 export action의 mobile 최소 높이를 44px로 보정하고 관련 E2E를 재실행했다.
- 실제 Excel screenshot 후 workbook이 열린 채 남지 않도록 close 동작과 Excel start 화면을 별도로 확인했다.

## 17. 사용자 검수 결과와 남은 항목

- 자동 검증: 완료
- 사용자 직접 검수: 대기 — 위 체크리스트를 완료로 주장하지 않음
- 관련 Open P0/P1/P2: `0/0/0`
- P3 backlog: `BACKLOG-E2E-FULL-SUITE-EXISTING-FAILURES`
- Persistent UAT·push·PR·merge: 미승인·미실행
- Roadmap canonical 다음 Task·Next Gate: `TASK-007A` Fable deep-interview
