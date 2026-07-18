# TASK-EXPORT-001 Phase 1 partial 구현 보고서

## 1. 상태

- Task: `TASK-EXPORT-001`
- 유형: `APPROVED_FEATURE_IMPLEMENTATION` (`experiment/*` fast-track의 2차 기획 구현)
- branch: `experiment/task-export-001-excel-export`
- 구현 상태: Phase 1 partial 자동 검증 완료, 사용자 검수 대기
- Git 경계: local experiment commit만 승인. push·PR·merge 미승인, main merge 승인 `0/3`
- 운영 경계: 대표 repo·`main`·Persistent UAT·실제 provider 변경 없음
- 최종 구현 source: [docs/22-excel-export-plan.md](../docs/22-excel-export-plan.md)

## 2. 해결한 업무 문제

조회 결과를 Excel로 옮기기 위해 사용자가 화면 값을 수동 복사해야 했고, 화면마다 별도 구현하면 필터·권한·프로젝트 scope·민감 컬럼이 달라질 위험이 있었다. 이번 Phase 1은 서로 다른 데이터 형태의 프로젝트 목록, 구매 dashboard, 내 업무 3개 화면에 공통 `.xlsx` 생성 경로를 적용해 다음을 구조적으로 고정했다.

- 화면과 같은 filter·sort·scope의 기존 store query 재사용
- 서버 allowlist 컬럼과 매출액 permission 기반 컬럼 omission
- 10,000행 상한과 cap+1 단일 조회
- 문자열 formula injection 차단
- 동시 workbook 2개, 대기 없는 429 resource fence
- 성공 직전 append-only 최소 audit
- desktop·390px에서 동일한 공통 action과 action-near feedback

`TASK-EXPORT-001` 전체 또는 모든 페이지 완료가 아니다. 삭제 보관함, 담당 프로젝트, 나머지 조회 화면과 사용자 컬럼 picker는 후속 범위다.

## 3. 구현 범위와 아키텍처

### Backend/API

- `GET /api/projects/export`: `ProjectRead`, `ProjectAccessScope`, 검색·상태·납기 필터와 기존 `ProjectStore` 단일 SQL 경로를 재사용한다.
- `GET /api/procurement/dashboard/export`: 검색·입고예정일 범위와 보정된 `ProjectRead` + `ProjectAccessScope`를 적용한다.
- `GET /api/my-work/export`: 인증 user id를 서버에서 고정하고 `All/Requested/InProgress/Completed`만 허용한다.
- 오류: 미지원/잘못된 filter와 10,000건 초과는 422, 동시 생성 포화는 429, 권한 없음은 403이다.
- 성공 응답은 `.xlsx`, UTF-8 filename, `X-Export-Row-Count`를 제공한다. 0건도 header-only workbook으로 정상 반환한다.

### 공통 workbook

- reflection/DTO 자동 직렬화 없이 `ExcelColumn<T>`의 명시적 selector를 사용한다.
- 제목·생성시각·bounded filter 요약, bold header, freeze row, auto filter, 제한된 auto width를 공통 적용한다.
- 숫자·날짜·boolean만 typed cell이며 문자열은 text cell로 쓴다. `=`, `+`, `-`, `@`, tab, CR 시작 synthetic 값의 재파싱 결과 formula object는 0개다.
- 프로젝트 매출액·통화는 `Project.SalesAmount.Read` 보유 시에만 열 자체가 존재한다.
- 내 업무는 description, link/target, 내부 work item id를 포함하지 않는다.

### 조회 경로 보정

- 프로젝트: 일반 목록의 max page size 100은 유지하고 export overload만 10,001건을 같은 SQL로 읽는다.
- 구매 dashboard: 기존 `limit 500`을 caller limit+1 구조로 바꾸고 response에 `truncated`를 추가했다. 기존 dashboard와 export 모두 project scope를 적용한다.
- 내 업무: 기존 조회 SQL에 선택적 limit만 추가해 export 전용 SQL을 만들지 않았다.

### DB/Migration

- migration: `0038_data_export_events.sql`
- additive: yes. 기존 migration 수정·번호 재사용 없음.
- 컬럼: actor FK, allowlisted export kind, row count, filter 사용 여부, 민감 매출 컬럼 포함 여부, 성공 시각만 저장한다.
- 검색어·고객·프로젝트명·파일 bytes·cell data는 저장하지 않는다.
- UPDATE/DELETE trigger로 append-only를 강제한다.
- workbook 생성 뒤 성공 응답 직전에 audit insert를 수행하며 insert 실패 시 파일을 반환하지 않는다. workbook과 audit 사이 exactly-once는 보장하거나 주장하지 않는다.
- 적용 환경: fresh/isolated PostgreSQL과 disposable Full-Stack E2E DB만. Persistent UAT 미적용.

### Frontend/UI·UX

- 공통 `ExcelExportAction`이 loading, duplicate block, object URL download, 0건, 422, 429, 일반 오류와 `aria-live` feedback을 담당한다.
- 성공 문구는 브라우저 filesystem 저장 완료를 단정하지 않고 `Excel 파일 생성을 완료했습니다`로 고정했다.
- 프로젝트 삭제 보관함과 내 업무 담당 프로젝트 탭에는 action을 표시하지 않는다.
- 390px에서는 알약형 export action, 원형 아이콘, compact scope label, 별도 사각/비대칭 action 형태를 사용하며 기존 좌상단 숨김 메뉴를 보존한다.

## 4. 실제 변경 위치

- Backend 공통: `backend/src/Emi.Qms.Api/DataExports/`
- 기존 조회 재사용/보정: `Projects/ProjectStore.cs`, `Procurement/ProcurementStore.cs`, `Workflow/WorkflowStore.cs`
- Endpoint/DI/CORS: 각 EndpointExtensions와 `Program.cs`
- 사용자 lifecycle: `Admin/AdminScheduledDeletionService.cs`가 `actor_user_id` 참조를 purge-block 판정에 포함
- Migration: `database/migrations/0038_data_export_events.sql`
- Frontend 공통 action/API: `frontend/src/ExcelExportAction.tsx`, `frontend/src/api.ts`
- 세 화면/CSS/type: `frontend/src/App.tsx`, `frontend/src/styles.css`, `frontend/src/projects.ts`
- 자동 검증: `ExcelExportTests.cs`, `ProjectRegistrationApiTests.cs`, `PostgreSqlMigrationTests.cs`, `ExcelExportAction.test.tsx`, `excel-export.full-stack.spec.ts`
- 화면 증빙: `tasks/export-001-screenshots/`

## 5. 기술적 결정과 검토한 대안

| 결정 | 채택 | 보류/제거 대안과 이유 |
| --- | --- | --- |
| 생성 위치 | 서버 `.xlsx` | client JSON→Excel은 permission·scope authoritative 보장이 약해 제거 |
| 조회 | 기존 store SQL + limit parameter | 화면 pagination 반복은 drift 중복·누락 위험으로 제거 |
| 컬럼 | 서버 고정 allowlist | 사용자 picker는 Phase 2로 보류 |
| 감사 | append-only DB event | application log만으로 성공 export audit를 대신하지 않음 |
| 자원 제한 | singleton 2-slot no-wait | 무제한/대기 queue는 request·memory 점유 위험으로 제거 |
| 0건 | header-only 정상 파일 | 오류 처리하면 현재 filter의 유효한 0건 상태를 보존하지 못함 |

## 6. 실행한 검증과 결과

| 검증 | 결과 |
| --- | --- |
| `dotnet build backend/Emi.Qms.sln --no-restore` | PASS, warning 0/error 0 |
| `dotnet build backend/Emi.Qms.sln --configuration Release --no-restore` | PASS, warning 0/error 0 |
| 핵심 export·migration filter 5개 | PASS |
| 사용자 purge/reference 회귀 17개 | PASS |
| `dotnet test backend/Emi.Qms.sln --no-restore` | PASS, 385/385 |
| export endpoint 동시성 429·감사 이벤트 미생성 통합 검증 | PASS, 1/1 |
| `pnpm typecheck` | PASS |
| `pnpm test` | PASS, 88/88 |
| `pnpm build` | PASS. 기존 bundle-size 경고만 존재 |
| `pnpm lint` | error 0, 기존 `main.tsx` fast-refresh warning 1 |
| `bash scripts/e2e-full-stack.sh excel-export.full-stack.spec.ts` | PASS, 1/1; isolated DB 자동 drop 확인 |
| workbook 재파싱 | formula node/object 0, 6회 download |
| 감사 이벤트 E2E | 성공 export 6회에 event 6건, 범위 밖 row count 0 |
| 390px overflow | 세 화면 모두 0 |

자동 검증은 사용자 검수를 대신하지 않는다. Persistent UAT migration/runtime handover와 실제 데이터 export는 승인 범위 밖이어서 실행하지 않았다.

## 7. 시행착오 및 폐기한 접근

1. 첫 Full-Stack E2E는 `Release --no-build` runtime이 이전 binary를 사용해 신규 endpoint가 404가 되었고 download 대기가 timeout 됐다. 제품 코드 결함이 아니라 stale Release artifact임을 error feedback과 runner 계약으로 확인한 뒤 Release build를 갱신하고 동일 격리 E2E를 재실행해 통과했다.
2. 첫 모바일 screenshot에서 내 업무 `새로고침` 글자가 기존 모바일 header의 흰색 text 규칙에 가려졌다. export action 전용 adaptive selector를 강화해 글자와 border를 복구하고 6개 screenshot을 재생성했다.
3. E2E가 생성한 `.xlsx`는 검증용 임시 artifact이므로 tracked evidence에서 제거하고 download 임시 경로에서 XML만 검사하도록 변경했다.

## 8. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소 위치 |
| --- | --- | --- | --- | --- |
| `EXPORT-PROCUREMENT-SCOPE` | P1 | RESOLVED | 인증만으로 전체 구매 project summary 노출 가능 | dashboard와 export에 `ProjectRead`·scope 적용 |
| `EXPORT-PROJECT-PAGING-EQUIVALENCE` | P2 | RESOLVED | page 반복 시 drift 위험 | 기존 single-query overload |
| `EXPORT-PROCUREMENT-SILENT-CAP` | P2 | RESOLVED | 500개 초과가 조용히 잘림 | limit+1 + `truncated` |
| `EXPORT-RESOURCE-FENCE` | P2 | RESOLVED | 동시 생성 무제한 위험 | singleton 2-slot, 즉시 429, lease test |
| `EXPORT-AUDIT-DURABILITY` | P2 | RESOLVED | 성공 대량 export 추적 불가 | migration 0038 + insert 실패 시 파일 미반환 |
| `EXPORT-FORMULA-SAFETY` | P2 | RESOLVED | spreadsheet formula injection | text writer + marker별 재파싱 |
| `EXPORT-SENSITIVE-COLUMN-ALLOWLIST` | P2 | RESOLVED | 매출/내부 식별자 과다 노출 | explicit selector와 permission column omission |
| `EXPORT-UI-SUCCESS-SEMANTICS` | P3 | RESOLVED | filesystem 저장 완료 오인 | 생성 완료 문구로 제한 |
| `EXPORT-USER-PURGE-ACTOR-REFERENCE` | P1 | RESOLVED | export audit만 참조하는 사용자 purge가 FK 오류로 500이 될 수 있음 | `actor_user_id`를 공통 purge-block reference에 추가하고 audit-only user 회귀 검증 |

Open P0/P1/P2/P3 Finding은 0개다. Phase 2 잔여 범위는 미해결 Finding이 아니라 명시적 제외 범위다.

## 9. Privacy·secret 검토

- tracked screenshot과 E2E는 synthetic customer/project만 사용한다.
- raw DOM/API/DB response, authorization header, tenant/object id, credential, secret을 산출물에 기록하지 않았다.
- audit schema는 raw filter/customer/project/file/cell 값을 저장하지 않는다.
- `.env`, certificate, dependency/lockfile 변경 없음.

## 10. 운영·검수 SOP

상태: `포함됨` — 이 section이 canonical SOP다.

1. 운영 적용 전 approved branch의 `0038` additive migration과 backup/forward-fix 계획을 별도 UAT 승인으로 검토한다.
2. isolated DB에서 migration ledger가 `0038_data_export_events`인지 확인한다.
3. 권한별로 프로젝트·구매·내 업무에서 현재 filter를 적용한 뒤 Excel 내보내기를 실행한다.
4. 매출 권한 없는 역할에서 매출액·통화 열이 존재하지 않는지 확인한다.
5. 0건, 10,000건 초과, 동시 요청 포화, audit 실패를 검증한다.
6. Persistent UAT handover는 별도 승인 후에만 수행한다. 실패 시 runtime을 이전 commit으로 되돌리되 이미 적용된 migration 0038을 삭제/수정하지 않고 additive forward-fix를 사용한다.

## 11. 사용자 매뉴얼

상태: `포함됨` — 이 section이 canonical user manual이다.

1. 프로젝트·구매·내 업무 화면에서 필요한 상태·검색·날짜 조건을 먼저 적용한다.
2. `Excel 내보내기`를 누른다. 생성 중에는 버튼이 비활성화된다.
3. `Excel 파일 생성을 완료했습니다` 또는 `0건 파일을 생성했습니다` 안내를 확인한다.
4. 10,000건 초과 안내가 나오면 검색·상태·날짜 조건을 좁혀 재시도한다.
5. 잠시 후 재시도 안내가 나오면 다른 파일 생성이 끝난 뒤 같은 조건으로 다시 누른다.
6. 삭제 보관함과 담당 프로젝트 탭은 Phase 1 대상이 아니다.

## 12. 사용자 검수 체크리스트

상태: `Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기`

- [ ] 프로젝트 목록에서 현재 검색·상태·납기 filter가 파일 row와 일치한다.
- [ ] 매출 권한 역할에서는 매출액·통화가 있고, 미권한 역할에서는 열 자체가 없다.
- [ ] 구매 dashboard의 현재 검색·입고예정일 filter와 project scope가 파일과 일치한다.
- [ ] 내 업무 파일에는 본인 업무만 있고 description/link/내부 ID가 없다.
- [ ] 0건 파일 안내와 header-only workbook을 확인한다.
- [ ] desktop과 390px에서 내보내기 버튼·범위·결과 안내를 확인한다.
- [ ] 삭제 보관함·담당 프로젝트 탭에서 버튼이 표시되지 않는다.

자동 검증 screenshot:

- `tasks/export-001-screenshots/01-projects-desktop-1440.png`
- `tasks/export-001-screenshots/02-procurement-desktop-1440.png`
- `tasks/export-001-screenshots/03-my-work-desktop-1440.png`
- `tasks/export-001-screenshots/04-projects-mobile-390.png`
- `tasks/export-001-screenshots/05-procurement-mobile-390.png`
- `tasks/export-001-screenshots/06-my-work-mobile-390.png`

## 13. 사용자 검수 결과와 남은 항목

- 사용자 직접 검수: 대기
- 자동 screenshot 검수: 완료
- Phase 2: 나머지 주요 조회 화면, 삭제 보관함, 담당 프로젝트, 사용자 컬럼 picker
- 운영 전: Persistent UAT migration/runtime handover와 실제 계정 권한 matrix 검수
- 게시: push·PR·merge 미승인. main merge 승인 `0/3`

## 14. Rollback과 forward-fix

- local experiment rollback: 현재 branch의 TASK-EXPORT-001 commit을 revert하면 endpoint·UI·tests가 제거되고 기존 import/list 기능은 유지된다.
- migration 0038이 아직 Persistent UAT에 적용되지 않았으므로 현재 운영 rollback 대상은 없다.
- 향후 적용 뒤에는 0038 파일을 수정·삭제하지 않는다. schema 보정은 0039 이후 additive forward-fix로 수행한다.
- audit insert 문제 시 파일 반환이 실패하도록 유지하며 audit을 우회하지 않는다.

## 15. 5종 종료 산출물 추적

| 산출물 | 상태 | canonical 위치 |
| --- | --- | --- |
| Implementation report | 작성 완료 | 이 문서 전체 |
| SOP | 작성 완료 | 이 문서 `10. 운영·검수 SOP` |
| User manual | 작성 완료 | 이 문서 `11. 사용자 매뉴얼` |
| Roadmap update | Phase 1 partial·사용자 검수 대기 반영 | `docs/00-product-roadmap.md` TASK-EXPORT-001 |
| User validation checklist | 작성·자동 검증 완료·사용자 검수 대기 | 이 문서 `12. 사용자 검수 체크리스트` |

## 16. Fable/Claude 사용량과 session 정리

| 시점 | 5시간 세션 | 주간 전체 | 주간 Fable |
| --- | --- | --- | --- |
| Fable 1차 직전 | 30% 사용 / 70% 잔여 | 3% / 97% | 5% / 95% |
| Fable 1차 직후 | 30% / 70% | 3% / 97% | 5% / 95% |
| Fable 2차 직전 | 43% / 57% | 4% / 96% | 7% / 93% |
| Fable 2차 직후 | 43% / 57% | 4% / 96% | 7% / 93% |
| 구현 종료 | 54% / 46% | 4% / 96% | 8% / 92% |

초기화 시각과 상세 runner 결과는 `tasks/export-001-change-001.md`에 기록했다. Fable private session/transcript는 runner cleanup으로 제거 완료했다.
