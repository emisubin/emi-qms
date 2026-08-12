# TASK-PROJECT-PENDING-001 Implementation report

- Task 유형: `APPROVED_FEATURE_IMPLEMENTATION`
- 기준선: `origin/main` `af796547ffb260ae427932a4734894af23c21ae6`
- branch: `feat/task-project-pending-001-basic-info-scope`
- 상태: `Local implementation / automated validation / user validation complete / publication approved`
- 기획: [project-pending-001-planning.md](project-pending-001-planning.md)
- 사용자 검수: [project-pending-001-user-validation-checklist.md](project-pending-001-user-validation-checklist.md)

## 1. 해결한 업무 문제

1. 영업이 프로젝트 기본정보에 사내 업무번호인 LSE TASK NO를 함께 저장하고 이후에도 확인·수정할 수 있게 했다.
2. Pending 첫 화면에서 운영 부서 사용자가 자기 부서의 오픈 항목부터 확인할 수 있게 했다.
3. 오픈과 종결을 별도 상태군으로 분리하고 카드에도 수명주기 표기를 추가해 기존 세부 상태만으로 구분하던 혼란을 줄였다.
4. 특정 프로젝트 안에서는 다른 부서가 조치하는 Pending도 업무 맥락상 보여야 하므로 기본 범위를 전체 부서로 유지했다.

## 2. 포함·제외 범위

### 포함

- nullable LSE TASK NO DB 필드와 migration `0076`
- 프로젝트 생성·수정·상세 API와 화면
- Pending `우리 부서/전체`, `오픈/종결/전체` 조회
- 조치 부서 우선·담당자 부서 fallback 서버 판정
- 기존 Pending dashboard·프로젝트 상세·모바일 필터와 Excel 선택 내보내기 filter 전달
- 관련 backend/frontend/full-stack 회귀

### 제외

- 프로젝트 목록 검색·Excel import/export에 LSE TASK NO 열 추가
- Pending 상태 전이·담당 배정·알림 수신자 변경
- 부서·역할·권한 추가
- 실제 운영 DB migration, provider 호출, Git 게시·main 병합·Azure 공개배포

## 3. 아키텍처와 영향

| 영역 | 변경 | 영향 |
| --- | --- | --- |
| DB/Migration | `projects.lse_task_number varchar(100) null`과 trim/nonblank check 추가 | additive. 기존 프로젝트는 `null`로 유지 |
| Backend Project API | create/update 정규화·저장·상세 직렬화·audit change 추가 | 기존 요청은 필드 생략 가능 |
| Backend Pending API | `scope`, `statusGroup` query 추가, 로그인 사용자 부서 파생 | 미지정/미인식 값은 기존 호환을 위해 `All` 처리 |
| Pending DB 조회 | 조치 부서 우선, 없으면 담당자 부서 사용 | 클라이언트가 부서 코드를 위조할 수 없음 |
| Frontend Project | 기본정보 입력·수정·상세 표시 | 기존 form control과 detail grid 재사용 |
| Frontend Pending | 전체 화면 `우리 부서+오픈`, 프로젝트 화면 `전체+오픈`, 상태군 select와 수명주기 badge | 데스크톱·모바일에서 기존 filter 구조 재사용 |
| UI·UX | 일반 검정 테두리와 흑백 배지만 사용 | 좌측 rail·굵은 강조선·신규 강조색 없음 |
| 권한/Workflow | 기존 `Pending.Read`, Project 권한 유지 | 상태 전이와 workflow 영향 없음 |
| Excel/PDF/첨부 | Pending 선택 내보내기가 현재 filter를 전달 | workbook 형식·PDF·첨부 변경 없음 |

## 4. 실제 변경 파일

- DB: `database/migrations/0076_project_lse_task_number.sql`
- Backend Project: `ProjectContracts.cs`, `ProjectInputNormalizer.cs`, `ProjectStore.cs`
- Backend Pending: `PendingContracts.cs`, `PendingEndpointExtensions.cs`, `PendingStore.cs`
- Backend export: `SelectedExcelExportService.cs`
- Frontend: `App.tsx`, `PendingPage.tsx`, `OperationalProjectDashboard.tsx`, `api.ts`, `pending.ts`, `projects.ts`, `styles.css`
- 자동 검증: `ProjectRegistrationApiTests.cs`, `PostgreSqlMigrationTests.cs`, `App.test.tsx`, `project-registration-smoke.spec.ts`
- Task 문서: identity gate, planning, implementation report, user validation checklist와 Product Roadmap

## 5. 기술적 결정과 검토한 대안

- 별도 `우리 부서 Pending` 메뉴 대신 기존 Pending 첫 조회 조건을 사용했다. 같은 데이터의 중복 메뉴와 사용법 분리를 피하고 두 select만으로 범위를 바꿀 수 있다.
- 부서 코드를 화면에서 서버로 보내는 방식을 사용하지 않았다. 서버가 인증 사용자의 부서를 직접 조회해 권한·데이터 기준이 흔들리지 않는다.
- Pending 조치 부서가 있으면 이를 우선한다. 조치 부서가 없는 과거·일반 항목만 지정 담당자 부서를 fallback으로 사용한다.
- 프로젝트 단위 Pending 화면까지 우리 부서로 제한하는 안은 폐기했다. 프로젝트 진행을 볼 때 타 부서 조치 항목이 숨겨지는 회귀가 확인돼 프로젝트 context는 `전체 + 오픈`으로 고정했다.
- LSE TASK NO를 필수값으로 두지 않았다. 기존 프로젝트·Excel 등록과의 호환을 보존하면서 영업이 필요한 건에만 입력할 수 있다.

## 6. 시행착오 및 폐기한 접근

- 처음 Full-Stack 회귀에서 4개 Pending 시나리오가 실패했다. 원인은 프로젝트별 Pending 화면에도 `우리 부서` 기본값을 적용해 타 부서 조치 항목이 가려진 것이었다.
- 전체 Pending 화면과 프로젝트별 화면의 목적을 분리해 전자는 `우리 부서`, 후자는 `전체` 기본값을 사용하도록 수정했다.
- 보정 뒤 실패했던 4개와 관련 모바일 2개, 총 6개 시나리오를 새 isolated DB에서 다시 실행해 모두 통과했다.
- 전체흐름 검사가 재생성한 기존 screenshot·xlsx 증빙은 이번 제품 변경이 아니므로 Task 변경 목록에서 제거했다.

## 7. 실행한 검증과 결과

| 검증 | 결과 | 근거 |
| --- | --- | --- |
| Backend Release build·전체 test | PASS | `518/518`, PostgreSQL migration suite 포함 |
| Frontend lint | PASS | error `0`; 기존 `main.tsx` Fast Refresh warning `1` 유지 |
| Frontend typecheck | PASS | error `0` |
| Frontend unit 전체 | PASS | `211/211` |
| Frontend production build | PASS | build 완료; 기존 bundle size warning 유지 |
| Mock UI 핵심 | PASS | 프로젝트 LSE와 Pending desktop 흐름 `2/2` |
| Isolated Full-Stack 전체 | PASS after resolution | 최초 `56/60`, 프로젝트 범위 결함 보정 후 실패 4개+관련 2개 `6/6` |
| 390px Pending 회귀 | PASS | 필터 sheet, 프로젝트 이동, overflow 검증 포함 |
| Diff whitespace | PASS | `git diff --check` |

미실행 검증:

- 실제 운영/UAT DB 적용: 사용자 승인 범위가 local 구현과 자동 검증까지이며 공개배포는 우선순위 3과 일괄 진행하도록 보류했다.
- 실제 사용자 검수: 2026-08-12 사용자가 화면 검수를 완료했다.
- 외부 알림/provider: 이번 변경은 알림 정책을 수정하지 않는다.

## 8. 개인정보·secret과 생성물 점검

- 테스트 데이터는 합성 사용자·프로젝트만 사용했다.
- 실제 사용자 이름, 회사 메일, 전화번호, tenant/client ID, secret, token과 connection string을 문서·변경 파일에 추가하지 않았다.
- Full-Stack이 갱신한 기존 screenshot·xlsx와 임시 browser artifact는 이번 allowlist에서 제거했다.
- migration·runtime 이미지·환경변수는 아직 운영에 적용하지 않았다.

## 9. Finding gate

- `PROJECT_CONTEXT_DEPARTMENT_FILTER` — P2 / `RESOLVED`: 특정 프로젝트 Pending에서 타 부서 항목이 숨겨질 수 있던 초기 구현. 프로젝트 context 기본 범위를 전체로 분리하고 Full-Stack 6개를 재검증했다.
- Open P0/P1/P2: `0/0/0`
- 기존 baseline warning: `frontend/src/main.tsx` Fast Refresh warning과 큰 bundle warning은 이번 diff가 만든 Finding이 아니며 제품 동작을 차단하지 않는다.

## 10. SOP

### 운영 적용

1. 공개배포 승인 후 migration `0076`을 Backend 교체 전에 적용한다.
2. Backend를 먼저 교체하고 ready 상태와 latest migration이 `0076`인지 확인한다.
3. Frontend를 교체하고 프로젝트 생성·상세와 Pending query를 smoke한다.
4. 전체 Pending은 운영 사용자에서 `우리 부서 + 오픈`, 프로젝트 Pending은 `전체 + 오픈`인지 확인한다.

### 장애·복구

- migration은 additive이므로 이전 앱으로 rollback해도 새 nullable 열은 무시된다.
- 운영 중 열을 drop하지 않는다. 제약이나 직렬화 문제가 있으면 앱 rollback 후 새 migration으로 forward-fix한다.
- Pending query를 생략한 구버전 client는 서버 기본 `All + All`로 계속 동작한다.

## 11. User manual

### LSE TASK NO

1. 영업 사용자가 `프로젝트 > 신규 프로젝트`를 연다.
2. `프로젝트 기본 정보`의 `LSE TASK NO`에 사내 번호를 입력한다. 필수값은 아니다.
3. 등록 후 프로젝트 상세에서 `기본정보 전체 보기`를 열면 확인할 수 있다.
4. `수정`에서 계속 바꾸거나 지울 수 있다. 빈 값은 `-`로 표시된다.

### Pending

1. 전체 Pending에 들어가면 운영 부서 사용자는 자기 부서의 오픈 항목부터 본다.
2. `조회 범위`에서 `우리 부서/전체`, `처리 상태`에서 `오픈/종결/전체`를 선택한다.
3. 특정 프로젝트에서 Pending을 열면 프로젝트 전체 조치 내용을 놓치지 않도록 전체 부서의 오픈 항목부터 본다.
4. 카드의 `오픈/종결` 표기와 옆의 세부 상태를 함께 확인한다.

## 12. 사용자 검수 결과와 남은 항목

- 자동 검증: 완료
- 사용자 직접 검수: 완료
- Commit/Push/PR/Merge: 단일 통합 게시 승인 / 실행 결과 대기
- 공개배포: 우선순위 3과 관리자 화면 변경을 포함한 단일 운영 배포 승인
- 다음 작업: 통합 PR의 CI Gate 뒤 main 병합과 Azure 공개배포

## 13. 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성 완료 | 본 문서 |
| SOP | 작성 완료 | 본 문서 10장 |
| User manual | 작성 완료 | 본 문서 11장 |
| Roadmap update | 반영 완료 | `docs/00-product-roadmap.md` 23장·25장 |
| User validation checklist | 사용자 검수 완료 | `tasks/project-pending-001-user-validation-checklist.md` |
