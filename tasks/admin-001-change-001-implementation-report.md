# TASK-ADMIN-001 Change 001 구현 보고

상태: `IMPLEMENTED / AUTOMATED_VALIDATION_COMPLETE / USER_VALIDATION_COMPLETE / PUBLICATION_APPROVED`

## 해결한 업무 문제

1. 관리자 홈의 승인 대기 사용자 수는 정확했지만 버튼이 전체 사용자 관리로 이동해 실제 승인 대기자를 다시 찾아야 했다.
2. `발송 완료`, `마지막 일일 요약`, `최근 기준정보 변경`은 관리자 조치보다 운영 통계 성격이 강해 관리자 홈의 우선순위를 흐렸다.
3. `최근 기준정보 변경` 수치는 모든 시스템 변경을 뜻하지 않고 일부 관리자 기준정보 변경 로그의 최근 7일 건수여서 KPI 이름만으로 범위를 오해할 수 있었다.

## 구현 결과

1. 관리자 홈은 승인 대기, 실패·대기·처리 중 알림과 진행 중 에스컬레이션처럼 조치가 필요한 KPI만 표시한다.
2. 제거한 3개 KPI 집계는 Backend 관리자 홈 조회에서도 계산하지 않는다.
3. 승인 대기 카드의 `승인 대기 사용자 보기` 버튼은 `/admin/users?filter=approval-pending`으로 이동한다.
4. Backend가 활성 Entra 사용자 중 역할이 없는 사용자만 반환하며 Frontend도 같은 조건을 방어적으로 적용한다.
5. 승인 대기 목록에서 부서·역할을 지정하면 기존 저장 API를 그대로 사용하고, 승인 완료된 사용자는 현재 필터 목록에서 사라진다.
6. 일반 사용자 관리 메뉴는 query filter 없이 기존 전체 목록을 유지한다.
7. 승인 대기자가 없으면 요청된 빈 상태 문구만 표시하고 불필요한 일괄 작업·표 영역은 숨긴다.

## 기술적 결정과 검토한 대안

- 선택: URL query `filter=approval-pending`과 Backend 필터를 함께 사용했다. 링크 공유·새로고침 후에도 화면 의미가 유지되고, 불필요한 전체 사용자 전송을 피할 수 있다.
- 보강: Frontend에서도 `approvalPending`을 다시 확인한다. 저장 API가 전체 snapshot을 반환해도 방금 승인한 사용자가 필터 화면에서 즉시 사라지게 하기 위함이다.
- 제외한 대안: Frontend에서만 전체 사용자를 받아 숨기는 방식은 데이터가 많아질수록 비효율적이고 API 의미가 불분명해 채택하지 않았다.
- 제외한 대안: 승인 대기 전용 새 페이지와 새 관리 API를 별도로 만드는 방식은 기존 사용자 승인 UI·권한을 중복시키므로 채택하지 않았다.

## 영역별 영향

- Backend/API: 관리자 홈 응답에서 제거 KPI 필드 3개를 제외하고 `/api/admin/users`에 선택형 승인 대기 query를 추가했다.
- Frontend/UI·UX: 기존 관리자 카드·표·문구·wireframe 규격을 재사용하고 새 강조선이나 별도 디자인 체계를 추가하지 않았다.
- 권한: 기존 `AdminUsersRead`·관리자 mutation 정책을 그대로 사용한다. 권한 확대가 없다.
- DB/Migration: schema 변경과 migration이 없다. 제거 KPI 원본 데이터도 보존한다.
- Workflow: 사용자 승인 저장 흐름은 기존 API를 재사용하며 프로젝트·품질·제조·알림 workflow에 영향이 없다.
- Excel/PDF/첨부: 변경 없음. 일반 사용자 관리의 기존 선택 export는 전체 목록에서 유지된다.

## 실제 변경 파일

- `backend/src/Emi.Qms.Api/Admin/AdminMasterDataContracts.cs`: 관리자 홈 응답 정리.
- `backend/src/Emi.Qms.Api/Admin/AdminMasterDataStore.cs`: 제거 KPI 집계 중단.
- `backend/src/Emi.Qms.Api/Identity/IdentityEndpointExtensions.cs`: 승인 대기 query 필터.
- `backend/tests/Emi.Qms.Api.Tests/ProjectRegistrationApiTests.cs`: 관리자 API 회귀.
- `frontend/src/App.tsx`: route·KPI·필터 화면·빈 상태.
- `frontend/src/api.ts`, `frontend/src/projects.ts`: Frontend API·type 계약.
- `frontend/tests/App.test.tsx`: 관리자 홈·승인 대기 이동 회귀.
- `docs/00-product-roadmap.md`, `tasks/admin-001-change-001*.md`: Task·검수·상태 기록.

## 시행착오 및 폐기한 접근

- 초기 Backend 집중 테스트를 Debug 산출물에 `--no-build`로 실행해 test runner 인수 오류가 발생했다. Release build 산출물과 정확한 test filter로 바로잡아 `2/2` 통과했으며 제품 결함은 아니었다.
- 기존 review-safe 고정 포트가 다른 worktree runtime에서 사용 중이었다. 해당 process를 종료하지 않고 이 Task 전용 포트와 synthetic DB로 격리했다.

## 보존한 기능

- 알림 발송 상태 상세와 완료 발송 데이터
- Daily Digest 기능과 발송 이력
- 기준정보 변경 이력 메뉴·API·데이터
- 기존 사용자 승인, 부서·역할·부서장 지정과 관리자 권한 정책
- 기존 DB schema와 migration

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `ADMIN-001-C001-F01` | P2 | Resolved | 승인 대기 KPI가 전체 사용자 목록으로 이동해 관리자가 실제 대상을 구분하기 어려웠다. | 전용 query route와 서버 필터를 추가했다. |
| `ADMIN-001-C001-F02` | P2 | Resolved | 조치 가치가 낮거나 범위가 모호한 KPI 3개가 관리자 홈의 우선순위를 흐렸다. | 카드와 전용 집계 필드를 제거하되 원본 기능·데이터·조회 메뉴는 보존했다. |

Open P0/P1/P2: `0/0/0`.

## 실행한 검증

- Backend Release build: 통과, 경고 0, 오류 0.
- Backend 관리자 API 집중 테스트: `2/2` 통과.
  - 관리자 홈의 조치형 KPI 응답 계약
  - 활성 Entra·역할 없음 승인 대기 필터
- Frontend lint: error 0, 기존 Fast Refresh warning 1.
- Frontend typecheck: 통과.
- Frontend 전체 unit: `29 files / 210 tests` 통과.
- Frontend production build: 통과, 기존 chunk-size warning 유지.
- 격리 Browser desktop 검증:
  - 관리자 홈의 조치형 KPI 5개만 표시되고 제거 대상 3개가 보이지 않음.
  - 승인 대기 0명일 때 지정 빈 상태 문구 표시.
  - synthetic 활성 Entra 사용자 2명 중 역할 없는 1명만 승인 대기 목록에 표시.
  - 일반 사용자 관리에서는 두 사용자가 모두 표시.
  - Console error 0.
- 격리 Browser mobile 390×844 검증: 승인 대기 제목·빈 상태 표시, page-level horizontal overflow 0.
- `git diff --check`: 통과.

## 최종 read-only 재검증

- 승인 계약과 최종 diff를 다시 대조해 포함 범위 밖 알림·일일 요약·변경 이력 삭제가 없음을 확인했다.
- 일반 사용자 관리 route는 query 없는 `/admin/users`로 남고 승인 대기 카드에서만 filter가 붙는 것을 확인했다.
- Backend와 Frontend가 같은 `approvalPending` 판정을 사용하며 서버 권한 정책을 우회하지 않음을 확인했다.
- 새로운 migration, dependency, secret, 외부 provider 호출과 운영 mutation이 없음을 확인했다.

## 미실행 항목

- 사용자 수동 검수: 2026-08-12 사용자가 전용 검수 화면을 확인해 완료했다.
- 구현 session과 분리된 Codex 검증 session: 현재 사용자 요청은 구현·검수본 준비 범위이며 별도 agent 위임 요청이 없어 실행하지 않았다. 게시 전 별도 검증 Gate에서 최종 diff와 CI 결과를 다시 확인한다.
- Full-Stack E2E 전체: 이번 변경 계약은 관리자 API 집중 통합 테스트와 Frontend 전체 회귀로 직접 검증했으며 게시 전 CI Gate에서 변경 인지 정책에 따라 실행한다.
- Persistent UAT·운영 배포: 사용자 승인 범위 밖이므로 수행하지 않았다.

## 개인정보·secret 검토

- 문서와 테스트에는 synthetic 사용자명과 `example.invalid`만 사용했다.
- 실제 사용자, 회사 계정, tenant/client/object ID, token, password와 provider 설정을 변경 파일에 기록하지 않았다.
- `.env`, 인증서, browser raw DOM과 runtime log는 Git 변경 범위에 포함하지 않았다.

## 사용자 검수 결과와 남은 항목

- 자동·격리 브라우저 검증은 완료했다.
- 사용자 직접 검수를 `tasks/admin-001-change-001-user-validation-checklist.md` 기준으로 완료했다.
- 사용자가 LSE TASK NO·부서 Pending과 설계 열반 검수본을 포함한 단일 PR·main 병합·운영 배포를 승인했다.

## Known issue·잔여 위험·후속

- Open P0/P1/P2는 없다.
- 기존 Frontend Fast Refresh warning 1건과 bundle chunk-size warning은 이번 변경과 무관한 기존 경고다.
- 게시 전에는 변경 인지 CI의 Backend·Frontend·Full-Stack 판단과 필수 `CI Gate`를 통과해야 한다.
- 사용자 검수 뒤 Product Roadmap에서 승인된 통합 게시 작업으로 이어간다.

## 검수 runtime

- Frontend: `http://127.0.0.1:42985/admin`
- Backend: `http://127.0.0.1:41168`
- 이번 Task 전용 synthetic DB에만 최신 migration과 검수용 사용자를 만들었다.
- 기존 Persistent UAT·운영 DB, 다른 검수 서버와 실제 provider는 변경하지 않았다.

## 사용자 확인 방법

1. 관리자 홈에서 제거 대상 3개 카드가 보이지 않는지 확인한다.
2. `승인 대기 사용자 보기`를 눌러 승인 대기 사용자만 표시되는지 확인한다.
3. 관리자 메뉴의 일반 `사용자 관리`를 열어 전체 사용자가 다시 표시되는지 확인한다.
4. 승인 대기자가 없는 환경에서는 지정한 빈 상태 문구가 표시되는지 확인한다.

## Rollback

- 관리자 홈 응답 필드·카드와 승인 대기 query route 변경만 역변경한다.
- DB·migration 변경이 없어 데이터 rollback은 필요하지 않다.

## 게시 경계

- 현재 전용 worktree에서만 구현·검증했다.
- Commit, push, PR, merge, Persistent UAT와 운영 배포는 수행하지 않았다.

## Task 종료 산출물 상태

| 산출물 | 상태 | Canonical 위치 |
| --- | --- | --- |
| Finding 기록 | 완료 | 이 문서 `Finding과 resolution` |
| 사용자 검수 체크리스트 | 사용자 검수 완료 | [사용자 검수 체크리스트](admin-001-change-001-user-validation-checklist.md) |
| Implementation report | 완료 | [Implementation report](admin-001-change-001-implementation-report.md) |
| SOP | N/A | migration·운영 설정·새 작업 절차가 없는 UI/API 최소 보정이며 rollback은 이 문서 `Rollback`에 기록 |
| User manual | 포함 | 이 문서 `사용자 확인 방법`과 [사용자 검수 체크리스트](admin-001-change-001-user-validation-checklist.md) |
| Roadmap update | 완료 | [Product Roadmap](../docs/00-product-roadmap.md)의 실행 큐·TASK-ADMIN-001·Decision Log |
