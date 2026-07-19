# TASK-EXPORT-001 Change 003 Implementation report

## 1. 요약과 상태

- 목적: 선택 Excel 내보내기에서 사용자가 허용된 컬럼의 부분집합을 고르되 권한·필수 식별 컬럼·workbook·감사 계약을 서버가 최종 보장한다.
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- Task Identity: `PASS_REUSE` — canonical `TASK-EXPORT-001`, `change-003`.
- 최종 구현 source: [2차 기획](../docs/35-selected-export-column-picker-plan.md).
- branch: `experiment/task-home-002-personalized-shell`
- 기준 commit: `f78ec331b63fe3c3043d6d707bab4d08bd138ceb`
- 경계: local experiment commit만 승인. 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider는 미반영이며 main merge 승인 `0/3`이다.

## 2. 해결한 업무 문제

Change 002는 20개 화면의 선택 행을 안전하게 Excel로 내보냈지만 workbook 컬럼은 화면별 고정 전체였다. 이번 변경은 desktop 공통 tray에 컬럼 picker를 추가하고, 화면·사용자별 허용 컬럼을 서버 한 곳에서 산출해 metadata, 요청 검증, workbook 생성과 민감 매출 audit가 서로 어긋나지 않게 했다.

- 필수 행 식별 컬럼은 해제할 수 없다.
- 선택하지 않은 컬럼은 workbook에서 header와 cell 모두 빠진다.
- 사용자가 picker를 열지 않으면 기존 전체 기본 컬럼을 그대로 사용한다.
- stale·조작 요청은 일부 컬럼을 조용히 무시하지 않고 generic 422로 전체 차단한다.
- mobile simple-mode에서는 bulk export와 picker를 계속 노출하지 않는다.
- form template의 별도 custom workbook 경로에는 공통 picker를 붙이지 않는다.

## 3. 구현 결과

### Backend/API

- `GET /api/data-exports/selected/columns?screen=<key>`가 기존 screen별 export permission을 재사용해 `{ key, label, required }` metadata를 서버 순서로 반환한다.
- `POST /api/data-exports/selected`에 optional `columns`를 추가했다. 미전달은 기존 전체 기본 컬럼, 빈 배열·형식 위반·중복·미지원·권한 밖·필수 누락은 422다.
- column key는 ASCII kebab-case, 최대 64 bytes, ordinal exact match이며 trim·case-fold·silent drop을 하지 않는다.
- 20개 화면 필수 컬럼 matrix와 실제 `ExcelColumn` header 일치를 contract test로 고정했다.
- 선택 컬럼은 client 순서가 아니라 server registry 순서로 workbook에 기록된다.
- 프로젝트 매출 audit flag는 단순 권한 보유가 아니라 실제 선택 workbook에 민감 매출 컬럼이 포함됐는지로 기록한다.
- 기존 선택 ID 전부-or-전무 scope 검증, 1,000개 상한, formula-safe writer, 2-slot resource fence와 append-only audit를 보존했다.
- DB·migration 변경은 0건이다.

### Frontend/UX

- 20개 desktop 선택 tray에 lazy-loaded `컬럼 선택` popover를 공통 적용했다.
- 필수 컬럼은 checked+disabled와 `필수` marker로 표시하고, 선택 수를 tray와 panel에서 함께 보여준다.
- 전체 기본 집합과 전체 선택의 의미가 같아 중복 action 두 개를 만들지 않고 `전체 선택 · 기본값 복원` 한 개로 통합했다.
- picker를 닫는 별도 확인 단계 없이 기존 `선택 Excel 내보내기` action으로 즉시 실행할 수 있다.
- `Esc`, 바깥 click, 닫기 button과 trigger focus 복귀를 지원한다.
- 컬럼 422가 발생하면 stale 선택을 기본값으로 초기화하고 명시 안내 후 다음 open에서 metadata를 다시 조회한다.
- 프로젝트도 기존 전용 UI callback 대신 공통 endpoint를 사용하며 legacy backend endpoint 호환은 유지한다.
- mobile 390px에서는 export action과 picker가 없고 현장 핵심 card·선택 동작만 유지된다.

## 4. 변경 파일

- Backend: `DataExports/DataExportEndpointExtensions.cs`, `SelectedExportColumnRegistry.cs`, `SelectedExcelExportService.cs`, `ExcelExportService.cs`.
- Backend tests: `SelectedExportScreenRegistryTests.cs`, `ProjectRegistrationApiTests.cs`.
- Frontend: `SelectedExcelExport.tsx`, `ExcelExportAction.tsx`, `api.ts`, `App.tsx`, `styles.css`.
- Frontend tests: `SelectedExcelExport.test.tsx`, `ExcelExportAction.test.tsx`, `App.test.tsx`.
- Full-Stack E2E: `all-pages-selected-export.full-stack.spec.ts`.
- 기획 이력: interview, Fable 1차 planning, Codex review, Fable 2차 planning과 Change 003 실행 계약.
- 증빙: `tasks/export-001-change-003-screenshots/`의 desktop 20개, mobile 1개, 실제 Excel 2개.

## 5. 실행한 검증

| 검증 | 결과 |
| --- | --- |
| Backend Release build | `PASS` — warning 0, error 0 |
| Backend 전체 tests | `PASS` — 401/401, skipped 0, 8분 43초 |
| Backend column picker targeted tests | `PASS` — 6/6 |
| 변경 DataExports·registry test `dotnet format --verify-no-changes` | `PASS` |
| Frontend lint | `PASS` — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 14 files, 109/109 |
| Frontend production build | `PASS` — 기존 chunk-size warning 유지 |
| disposable Full-Stack E2E | `PASS` — 1/1, 54.2초 |
| 20개 화면 metadata/picker | `PASS` — 누락 0, 각 화면 필수 잠금 1개 이상 |
| workbook XML | `PASS` — 선택 header 수 일치, formula node 0 |
| mobile 390px | `PASS` — picker/export 미노출, horizontal overflow 0 |
| 실제 Microsoft Excel | `PASS` — 프로젝트 15열·관리자 사용자 7열 직접 확인, 모든 workbook과 Excel 종료 |
| Persistent UAT·대표 runtime | 미실행 — 승인 범위 밖 |

첫 E2E는 프로젝트의 legacy custom callback을 공통 picker 제외 대상으로 잘못 해석해 실패했고, 제품 경계를 `form-templates`만 제외하도록 수정했다. 두 번째 E2E는 namespace prefix가 있는 worksheet XML의 cell을 test regex가 세지 못해 실패했으며 namespace-tolerant 검증으로 보정했다. 제품 workbook의 formula·header 계약은 최종 E2E에서 통과했다. 전체 Frontend 최초 실행의 1개 실패는 과거 프로젝트 전용 endpoint를 기대하던 회귀 test였고 공통 endpoint 요청 계약으로 갱신한 뒤 109/109를 통과했다.

## 6. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `COLUMN-METADATA-DRIFT` | P1 | `RESOLVED` | metadata·검증·workbook·audit가 별도 컬럼 목록을 가지면 권한 drift 가능 | `GetEffectiveColumns` 결과와 실제 `ExcelColumn`을 네 지점에서 재사용 |
| `COLUMN-REQUIRED-MATRIX-UNSPECIFIED` | P2 | `RESOLVED` | 필수 행 식별 컬럼이 화면별로 불명확 | 20개 screen matrix와 header contract test 고정 |
| `COLUMN-KEY-BOUNDARY-INCOMPLETE` | P2 | `RESOLVED` | 임의 key·중복·대소문자 보정이 silent mismatch를 만들 수 있음 | ASCII kebab-case·64 bytes·ordinal exact·중복/개수 검증 |
| `COLUMN-STALE-FALLBACK-AMBIGUOUS` | P2 | `RESOLVED` | stale custom 선택이 다른 파일로 조용히 fallback할 위험 | 422 전체 차단, 기본값 초기화·명시 안내·cache 폐기 |
| `COLUMN-POPOVER-EXTRA-STEP` | P3 | `RESOLVED` | 닫기/확인 후 export를 강제하면 반복 작업 증가 | 기존 export action에서 즉시 실행 |
| `FABLE-FIRST-PLAN-PREFACE` | P3 | `RESOLVED` | 1차 원문에 기획 외 preface가 포함됨 | 원문은 보존하고 Codex review에서 지적, 2차 최종 계약은 metadata/H1부터 시작 |
| `COLUMN-PICKER-CUSTOM-CALLBACK-DETECTION` | P3 | `RESOLVED` | 프로젝트 legacy callback 때문에 picker가 빠진 최초 E2E | 프로젝트를 공통 endpoint로 통합하고 form template만 제외 |
| `COLUMN-PICKER-XLSX-XML-NAMESPACE` | P3 | `RESOLVED` | test regex가 prefix cell을 누락 | namespace-tolerant header count 검증 |
| `COLUMN-PICKER-LEGACY-UNIT-CONTRACT` | P3 | `RESOLVED` | 기존 App test가 과거 프로젝트 endpoint를 기대 | 공통 selected export request 계약으로 갱신 |
| `BACKEND-FORMAT-EXPERIMENT-BASELINE` | P3 | `BACKLOG` | 범위 밖 `LogisticsStore`·`PanelKittingStore`·`ProjectStore`와 기존 대형 API test의 누적 whitespace/import-order 때문에 solution 전체 format verify 실패 | 이번 변경 DataExports·registry test는 별도 include 검증 PASS. 전체 정리는 조건부 housekeeping으로 분리 |

Open P0/P1/P2 Finding은 `0/0/0`이다. P3는 해결 5건과 조건부 backlog 1건이며 완료·local commit을 차단하지 않는다.

## 7. Privacy·secret 검토

- screenshot·workbook은 disposable runtime의 synthetic 사용자·고객·프로젝트만 사용했다.
- raw API/DB response, authorization header, tenant/object id, credential·secret을 추적 산출물에 넣지 않았다.
- column key·선택 집합은 audit나 log에 새로 저장하지 않는다.
- `.env`, certificate, dependency, lockfile, migration 변경은 없다.

## 8. SOP

1. desktop에서 대상 화면의 행을 한 개 이상 선택하고 `컬럼 선택`을 연다.
2. 필수 컬럼이 checked+disabled인지, 권한 밖 민감 컬럼이 metadata 자체에 없는지 확인한다.
3. 비필수 컬럼을 해제하고 picker가 열린 상태에서 기존 export action을 실행한다.
4. Excel에서 선택한 header만 서버 고정 순서로 존재하고 선택 행만 포함됐는지 확인한다.
5. 오래 열린 client의 stale key·필수 누락·중복 key 요청이 generic 422, file/audit 0인지 확인한다.
6. mobile 390px에서 bulk export/picker가 없고 horizontal overflow가 없는지 확인한다.
7. 운영 적용은 별도 UAT 승인 뒤 수행한다. rollback은 experiment commit revert이며, 이번 변경에는 DB rollback이 없다.

## 9. User manual

1. desktop 목록에서 내보낼 항목을 checkbox로 선택한다.
2. `컬럼 선택`을 눌러 파일에 필요한 항목만 남긴다. `필수` 컬럼은 행 식별에 필요해 해제할 수 없다.
3. 기본 구성이 필요하면 `전체 선택 · 기본값 복원`을 누른다.
4. 팝업을 따로 확인·닫지 않아도 `선택 Excel 내보내기`를 바로 누를 수 있다.
5. 컬럼 구성이 바뀌었다는 안내가 나오면 선택이 기본값으로 초기화된 것이므로 picker를 다시 열어 최신 목록을 확인한다.
6. 모바일은 현장 핵심 확인용이라 Excel 내보내기는 desktop에서 수행한다.

## 10. User validation checklist

### 자동 검증

- [x] 20개 desktop 화면에 동일 picker 적용
- [x] 화면별 필수 컬럼 잠금과 server 422 검증
- [x] 권한 없는 매출 컬럼 metadata·요청·workbook·audit 차단
- [x] picker 미사용 기존 client의 전체 기본 컬럼 호환
- [x] 선택 header 수·server 순서·formula-safe workbook
- [x] stale 422 초기화·metadata 재조회
- [x] form template custom export 제외
- [x] mobile 390px simple-mode와 overflow 0
- [x] 실제 Microsoft Excel 2개 확인 후 종료

### 사용자 직접 검수

- [ ] 프로젝트·자재·관리자 화면에서 picker의 밀도와 용어를 확인한다.
- [ ] 필수 컬럼이 업무상 충분하고 과하지 않은지 확인한다.
- [ ] 비필수 컬럼을 해제한 실제 Excel 파일이 기대 순서인지 확인한다.
- [ ] 모바일에서 내보내기 미노출이 현장 사용 흐름에 적합한지 확인한다.

상태: `자동 검증 완료 · 사용자 검수 대기 — 마지막 일괄 검수`.

## 11. Fable/Claude 사용량과 session 정리

| 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 56% 사용 / 44% 잔여 / 13:30 KST 초기화 | 20% 사용 / 80% 잔여 / 07-25 08:00 KST | 40% 사용 / 60% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 56% / 44% / 13:30 KST | 20% / 80% / 07-25 08:00 KST | 40% / 60% / 초기화 parse 불가 |
| 2차 planning 직전 | 73% / 27% / 13:29 KST | 22% / 78% / 07-25 07:59 KST | 42% / 58% / 초기화 parse 불가 |
| 2차 planning 직후 | 73% / 27% / 13:29 KST | 22% / 78% / 07-25 07:59 KST | 42% / 58% / 초기화 parse 불가 |

- 1차 runner: `CREATED_FULL_BASELINE`, 444초, stdout 25,979 bytes, stderr 0.
- 2차 runner: `RESUMED_ARTIFACT_PREFLIGHT`, 255초, stdout 36,214 bytes, stderr 0, `openBlockingDecisionCount: 0`.
- cleanup: `FABLE_TASK_SESSION_CLEANED`, session 1·transcript 1 제거, missing 0.

## 12. Rollback·복구

- local experiment rollback은 Change 003 commit을 revert해 metadata endpoint, optional request, picker UI와 tests를 함께 제거한다.
- `columns` 미전달 호환 경로와 legacy project backend endpoint가 남아 있어 이전 client는 계속 동작한다.
- migration이 없으므로 data rollback·forward-fix는 적용 대상이 아니다.

## 13. 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서 8장 |
| User manual | 완료 | 본 문서 9장 |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md) TASK-EXPORT-001 Change 003 |
| User validation checklist | 자동 완료·사용자 대기 | 본 문서 10장 |

## 14. 남은 항목

- 사용자 직접 검수는 마지막 일괄 검수에 포함한다.
- preset 저장·컬럼 재정렬·이름 변경·계산식·multi-sheet·form template picker는 명시적 제외다.
- 대표 repo·`main`·Persistent UAT·push·PR·merge는 별도 승격 Task이며 main merge 승인 `0/3`이다.
