# TASK-ADMIN-003 구현 보고서 — 사용자 부서·역할·부서장 연결 보정

## 1. 요약과 상태

- 목적: 운영 사용자 관리에서 표준 부서를 빠짐없이 선택하고, 부서에 맞는 역할과 부서장·양식관리 권한을 한 번에 지정한다.
- 상태: 로컬 구현·자동 검증·사용자 검수 완료 / Git 게시·main 병합·공개배포 승인
- Task 유형: `P2_REMEDIATION`
- Branch/base: `fix/task-admin-003-user-departments` / `origin/main` `7c05175001d9e0beb23a161639c846f98e05dbb7`
- Git 게시: commit·push·PR·merge 실행 대기. Azure DB와 운영 앱은 아직 미변경이며 사용자 승인 범위에서 게시·배포한다.

## 2. 해결한 업무 문제

운영 migration은 설계·구매·자재 3개 부서만 만들고 개발 seed만 10개 부서를 가지고 있어, 실제 사용자 등록 화면에서 선택 가능한 부서가 적었다. 부서와 역할도 별개 입력이라 관리자가 같은 의미를 두 번 선택해야 했고 서로 어긋날 수 있었다. 양식관리 부서장 지정은 별도 화면에만 있어 사용자 승인 시 한 번에 처리할 수 없었다.

이번 보정으로 표준 10개 부서를 운영 schema에 추가하고 이름을 한글로 통일했다. 사용자 관리에서 부서를 바꾸면 기본 역할을 즉시 선택하며, 서버도 그 역할을 빠뜨릴 수 없게 강제한다. `부서장` 체크는 복수 지정이 가능하고 품질·제조·생산관리 부서에서는 기존 양식관리 승인 binding과 자동 동기화한다.

## 3. 포함·제외 범위

### 포함

- 표준 10개 부서 생성·한글명·정렬 순서 보정
- 부서별 기본 역할 mapping과 API 응답
- 사용자 관리의 부서 선택 → 기본 역할 자동 선택
- 서버 저장 시 기본 역할 강제 포함
- 사용자별 부서장 체크·표시, 한 부서 복수 부서장
- 품질·제조·생산관리 부서장의 기존 양식관리 binding·audit 동기화
- 기존 활성 양식관리자의 부서장 상태 backfill

### 제외

- 역할·권한 master 편집, 신규 역할 생성
- 양식 종류·양식 내용 변경
- Entra·Microsoft 365 설정 변경
- 운영 DB migration 적용, Azure 앱 교체, 실제 사용자 데이터 수정
- Git commit·push·PR·merge

## 4. 아키텍처와 영향

### DB·Migration

- `0072_user_department_role_heads.sql`은 기존 migration을 바꾸지 않는 additive migration이다.
- 없는 표준 부서를 추가하고 기존 표준 code의 표시명·정렬을 한글 기준으로 갱신한다. 비활성·삭제 예약 상태는 되살리지 않는다.
- `qms_users.is_department_head`를 `false` 기본값으로 추가한다.
- 현재 부서와 일치하는 활성 양식관리 binding이 있던 사용자는 `is_department_head=true`로 이관한다.
- rollback은 migration 삭제나 DB reset이 아니라 새 migration을 통한 forward-fix를 사용한다.

### Backend·API·권한

- `DepartmentIdentityPolicy`가 표준 부서 code의 기본 역할과 양식관리 domain mapping을 한 곳에서 관리한다.
- 사용자 저장 시 서버가 선택 부서의 기본 역할을 반드시 포함한다.
- 부서장 상태와 양식관리 binding 지정·해제·audit를 사용자 저장 transaction 안에서 함께 처리한다.
- 기존 양식관리 화면에서 지정·해제해도 사용자 관리의 부서장 체크 상태가 같은 transaction에서 동기화된다.
- 기존 System Administrator 전용 사용자 관리 권한과 마지막 활성 System Administrator 보호를 유지한다.
- 한 부서 여러 부서장은 사용자별 active binding unique 경계로 지원한다.

### Frontend·UI·UX

- 기존 흑백 wireframe 사용자 관리 표 구조와 input·checkbox 규격을 재사용했다.
- `부서장` 열과 편집 체크박스를 추가했다.
- 부서 선택 시 이전 표준 부서 역할을 새 기본 역할로 교체하고 `자동` 표시와 비활성 checkbox로 사용자가 실수로 해제하지 못하게 했다.
- 부서 미지정이면 부서장 체크를 해제·비활성화한다.
- 기존 모바일 핵심 열/전체 필드 전환과 table 내부 가로 스크롤 계약을 유지한다.

### 기존 기능·파일 영향

- Workflow 18단계, 프로젝트·검사·제조·알림, Excel/PDF/첨부 계약은 변경하지 않는다.
- 사용자 Excel 내보내기 컬럼은 변경하지 않는다.
- 기존 역할 복수 선택은 유지하며, 표준 부서의 기본 역할만 서버가 필수로 보장한다.
- 기존 부서 삭제 lifecycle·참조 무결성·마지막 관리자 보호는 그대로다.

## 5. 기술적 결정과 검토한 대안

- 부서장을 새 영구 역할로 만들지 않았다. 부서 이동 뒤 과권한이 남지 않도록 사용자 상태와 현재 부서에 묶인 기존 양식관리 binding을 재사용했다.
- 양식관리 binding만으로 부서장을 표현하는 안은 영업·설계처럼 현재 관리 양식이 없는 부서장을 저장할 수 없어 제외했다. 사용자에 일반 부서장 상태를 저장하고, 양식 대상 부서에서만 binding을 추가한다.
- Frontend만 역할을 자동 선택하는 안은 직접 API 요청에서 역할 누락이 가능해 제외했다. Frontend 편의와 Backend 강제를 함께 적용했다.
- 한 부서 1인 제한은 사용자 결정과 다르므로 적용하지 않았다.

## 6. 시행착오 및 폐기한 접근

- 전체 solution `dotnet format --verify-no-changes`는 변경하지 않은 기존 대형 store와 test 파일의 baseline formatting 문제까지 보고했다. 범위 밖 파일은 수정하지 않고 이번 변경 C# allowlist만 다시 검증해 통과시켰다.
- 사용자 관리 표에 새 열을 넣을 때 기존 mobile 핵심 열 숨김 규칙의 마지막 작업 열을 보존해야 했다. 새 부서장 열은 전체 필드 보기에서 표시되고 작업 열은 계속 마지막 열로 유지되도록 기존 구조 안에 배치했다.

## 7. 변경 파일

- `database/migrations/0072_user_department_role_heads.sql`: 표준 부서·한글명·부서장 schema/backfill
- `backend/src/Emi.Qms.Api/Identity/DepartmentIdentityPolicy.cs`: 부서→기본 역할·양식 domain 정책
- `backend/src/Emi.Qms.Api/Identity/SeedIdentityData.cs`, `DevelopmentIdentitySeeder.cs`: 개발 기준·실행 seed 부서 한글명
- `backend/src/Emi.Qms.Api/Identity/UserAdministrationStore.cs`: 역할 자동 지정·부서장/binding transaction
- `backend/src/Emi.Qms.Api/Identity/DbIdentityStore.cs`: 부서장 상태 조회
- `backend/src/Emi.Qms.Api/Identity/IUserAdministrationStore.cs`: 사용자 관리 요청·응답 내부 계약
- `backend/src/Emi.Qms.Api/Identity/IdentityEndpointExtensions.cs`: API의 부서장·기본 역할 응답
- `backend/src/Emi.Qms.Api/Admin/FormTemplateStore.cs`: 기존 양식관리 지정과 부서장 상태 양방향 동기화
- `frontend/src/identity.ts`, `frontend/src/App.tsx`: API type·자동 역할·부서장 UI
- `backend/tests/.../IdentityInfrastructureTests.cs`: 복수 부서장·역할 자동 지정·양식 scope 회귀
- `backend/tests/.../PostgreSqlMigrationTests.cs`: fresh/existing `0072`·한글 부서·backfill 회귀
- `frontend/tests/App.test.tsx`: 사용자 관리 UI 회귀
- `tasks/admin-003-change-001.md`: 승인 계약과 Task gate
- 이 보고서, Product Roadmap: 종료 산출물·상태

## 8. 실행한 검증과 결과

| 검증 | 적용 여부 | 결과 | 근거/미실행 이유 |
| --- | --- | --- | --- |
| `git diff --check` | 적용 | 통과 | whitespace 오류 0 |
| Backend Release build | 적용 | 통과 | warning/error `0/0` |
| 검수 runtime 재빌드·API smoke | 적용 | 통과 | 실행 seed 한글명 보정 후 표준 부서 10개·수정 가능 합성 사용자 2명 확인 |
| 변경 C# allowlist format | 적용 | 통과 | 변경 파일만 `--verify-no-changes` |
| Backend 핵심 권한·복수 부서장 test | 적용 | 통과 | `1/1` |
| Migration fresh/existing targeted | 적용 | 통과 | `3/3` |
| Backend 전체 | 적용 | 통과 | `493/493` |
| Frontend lint | 적용 | 통과 | error 0, 기존 Fast Refresh warning 1 |
| Frontend typecheck | 적용 | 통과 | error 0 |
| Frontend unit | 적용 | 통과 | `190/190` |
| Frontend production build | 적용 | 통과 | 기존 chunk-size warning 유지 |
| 격리 Full-Stack Chromium | 적용 | 통과 | 모바일 관리자 workspace `1/1`, 임시 DB·container·network 제거 완료 |
| Persistent UAT·Azure | 미적용 | N/A | 운영 mutation은 승인 범위 밖 |
| PR CI | 적용 | 보정 후 재실행 | PR #93 첫 Backend 전체 검사에서 공지 작성자 부서명을 기존 영문 `Sales`로 기대한 회귀 1개만 실패해 승인된 한글명 `영업`으로 test contract를 갱신했다. |

## 9. 개인정보·Secret 검토

- 자동 검증은 합성 사용자와 `.invalid` 주소, 격리 PostgreSQL만 사용했다.
- 실제 사용자 이름·이메일·전화번호, tenant/client/object id, token·connection string을 문서나 diff에 기록하지 않았다.
- 실제 Teams·Mail·외부 provider 호출은 0건이다.

## 10. Finding gate

| Finding | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `ADMIN003-MISSING-PRODUCTION-DEPARTMENTS` | P2 | RESOLVED | 운영 migration에 표준 부서 3개만 있어 사용자 등록 목록이 불완전 | `0072`에서 표준 10개와 한글명을 idempotent 보강, fresh/existing 검증 |
| `ADMIN003-DEPARTMENT-ROLE-DRIFT` | P2 | RESOLVED | 부서와 역할을 따로 저장해 누락·불일치 가능 | UI 자동 선택 + Backend 필수 역할 포함 |
| `ADMIN003-HEAD-FORM-SCOPE-DIVERGENCE` | P2 | RESOLVED | 사용자 관리와 양식관리 부서장 지정이 분리 | 동일 transaction의 flag·binding·audit 양방향 동기화 |
| `ADMIN003-DEVELOPMENT-SEED-NAME-DRIFT` | P2 | RESOLVED | migration 적용 뒤 개발 seed가 부서명을 다시 영문으로 덮어씀 | 실행 seed도 표준 한글명으로 통일하고 검수 API에서 10개 한글명 확인 |
| `ADMIN003-CI-DEPARTMENT-NAME-DRIFT` | P2 | RESOLVED | PR #93의 기존 공지 API 회귀 한 곳이 작성자 부서명을 영문 `Sales`로 고정해 승인된 한글명 `영업`을 실패로 판정 | 제품 코드·응답은 유지하고 해당 test expectation만 최신 부서 표시명 계약으로 갱신 |

Open P0/P1/P2: `0/0/0`. 기존 Frontend Fast Refresh·chunk-size warning과 전체 repository formatting baseline은 이번 diff에서 증가시키지 않았으며 범위 밖 파일은 수정하지 않았다.

## 11. SOP — 시스템 관리자 운영 절차

1. `관리자 → 사용자 관리`로 이동한다.
2. 대상 Entra 사용자의 `수정`을 누른다.
3. 부서를 선택한다. 해당 기본 역할이 `자동`으로 선택되는지 확인한다.
4. 부서장이라면 `부서장 → 지정`을 체크한다.
5. `저장`을 누른다. 같은 부서의 다른 부서장도 같은 방식으로 추가할 수 있다.
6. 부서장 해제 시 체크를 끄고 저장한다. 부서 변경 시 새 부서와 역할·양식 범위가 함께 갱신된다.
7. 운영 배포 시 migration `0072`를 Backend보다 먼저 적용한 뒤 Backend→Frontend 순서로 교체한다.

## 12. User manual — 사용자 관리 화면

- 부서 목록에는 `관리, 영업, 설계, 생산관리, 구매, 자재, 제조, 품질, 물류, 조회 전용`이 표시된다.
- 부서를 선택하면 기본 역할이 자동으로 체크되며 직접 해제할 수 없다.
- 다른 추가 역할은 기존과 같이 선택할 수 있다.
- 부서장 체크는 한 부서에서 여러 사용자에게 할 수 있다.
- 품질·제조·생산관리 부서장은 양식관리 대상 범위의 승인 권한도 함께 가진다.
- 사용자 비활성화·삭제 예약과 마지막 시스템 관리자 보호는 기존과 같다.

## 13. 사용자 검수 체크리스트

- 자동 검증 상태: `완료`
- 사용자 직접 검수 상태: `사용자 검수 완료`

- [x] 사용자 관리에서 표준 부서 10개가 모두 한글로 표시된다.
- [x] 승인 대기 사용자의 부서를 선택하면 기본 역할이 자동 선택된다.
- [x] 자동 역할은 체크 해제할 수 없고 저장 후에도 유지된다.
- [x] 같은 부서 사용자 2명 이상을 부서장으로 저장할 수 있다.
- [x] 부서장 사용자가 자기 부서 양식관리 화면에 접근할 수 있다.
- [x] 한 명의 부서장을 해제해도 같은 부서의 다른 부서장은 유지된다.
- [x] 부서 변경 후 이전 부서 양식 권한이 남지 않는다.
- [x] Desktop과 Mobile 전체 필드 보기에서 부서장 열과 작업 버튼이 겹치지 않는다.

## 14. 사용자 검수 결과와 남은 항목

- 자동 검증과 격리 browser 검증은 완료했다.
- 실제 사용자 화면 검수는 2026-08-11 완료했다.
- Git 게시, CI, main 병합과 운영 migration·배포는 사용자가 함께 실행하도록 승인했으며 아직 수행 전이다.
- 사용자 검수에서 문제가 확인되면 `TASK-ADMIN-003`의 다음 change로 보정한다.

## 15. Rollback·forward-fix

- 코드: 게시 전에는 이 branch의 변경만 폐기하면 된다. 게시 후에는 해당 commit을 revert한다.
- DB: 적용된 `0072`를 수정·삭제하거나 DB를 초기화하지 않는다. 부서명·flag·binding 문제는 다음 additive migration으로 forward-fix한다.
- 잘못 지정된 부서장은 사용자 관리에서 체크 해제해 기존 soft-revoke·audit 경로로 복구한다.

## 16. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 1~10장 |
| SOP | 완료 | 이 문서 11장 |
| User manual | 완료 | 이 문서 12장 |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` TASK-ADMIN-003 행·Decision Log |
| User validation checklist | 사용자 검수 완료 | 이 문서 13장 |
