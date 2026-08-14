# TASK-PROJECT-ASSIGNEE-DELEGATION-001 — Implementation Report

## 1. 상태와 범위

- 상태: 사용자 검수·원격 main 병합·Azure 공개배포 완료
- 사용자 검수: 설계 부서장 실제 화면 확인·최종 게시 승인
- Git 게시·병합·운영 배포: PR #101·exact main SHA·Azure 운영 release 완료
- 기준 branch: `feat/task-production-control-001-unified-project-plans`
- 기준선 HEAD: `4520e641b98c1c464243e9988b1a373d57d49bed`
- 기획 source: [Codex 기획](project-assignee-delegation-001-planning.md)

## 2. 해결한 업무 문제

생산관리팀이 각 부서에 연락해 프로젝트 담당자를 취합한 뒤 모든 담당자를 대신 입력하던 병목을 제거했다. 생산관리 이외 활성 부서장은 프로젝트의 `생산계획 수정` 진입점에서 자기 부서 담당자만 직접 지정한다. 생산계획과 전체 담당자 수정 권한은 기존 생산관리 사용자에게 유지된다.

## 3. 구현 결과

### Backend·권한

- 자기 부서 담당자 범위만 조회·저장하는 `GET/PATCH /api/projects/{projectId}/production-planning/department-assignees`를 추가했다.
- 서버가 활성 프로젝트, 활성 사용자, 부서장 여부, 지원 부서, 책임 구분과 후보 사용자의 실제 부서를 저장 시점에 검증한다.
- 일반 사용자, System Administrator/read-only 역할, 생산관리 부서장과 다른 부서 책임 구분은 축소 API에서 차단한다.
- 영업·설계·구매·자재·제조·물류는 정·부 2명, 품질은 IQC·LQC·OQC·전진검수/FAT 정·부 8명으로 제한한다.
- 기존 row version 동시성, 변경·해제 사유, field-level 감사이력과 담당자 지정 후속 알림 생성을 재사용했다.

### Frontend·UI/UX

- 생산관리 사용자는 기존 전체 생산계획 수정 화면을 그대로 사용한다.
- 다른 부서장은 프로젝트·Code·본인 부서, 자기 부서 담당자, 수정사유와 저장·취소만 본다.
- 생산계획 항목, 계획일, 실적 연결, Excel과 다른 부서 담당자는 부서장 축소 화면에 렌더링하지 않는다.
- 프로젝트 상세 조회 화면의 전체 생산계획·전체 담당자 표시는 그대로 보존했다.
- 기존 흑백 wireframe의 입력 흐름, 일반 테두리, 담당자 카드와 action bar를 재사용했으며 새로운 강조선이나 색상 강조를 추가하지 않았다.

### 프로젝트 생성 알림

- 기존 프로젝트 생성 전체 알림 뒤에 생산관리 이외 지원 부서의 활성 부서장에게 `프로젝트 담당자를 지정해 주세요.` 원본 알림을 한 건 추가한다.
- 링크는 같은 프로젝트의 생산계획 수정 route이며 로그인한 부서장의 실제 소속으로 화면 범위를 결정한다.
- `WorkAssignment` 인앱 원본을 사용해 기존 Teams Activity와 PWA push 파생 전달을 그대로 사용한다.
- 프로젝트 생성 전체 공지 메일과 생산관리 생산계획 업무는 변경하거나 중복 생성하지 않는다.
- 프로젝트별 idempotency key로 재처리 중복을 막는다.

### DB·Migration·외부 산출물

- DB schema와 migration: 변경 없음. 기존 `project_assignees`, 감사이력과 알림 원장을 재사용한다.
- Excel/PDF/첨부파일: 변경 없음.
- 실제 외부 provider 발송: 수행하지 않음. 기존 dispatcher 계약을 자동 테스트로 대조했다.

## 4. 주요 변경 위치

- Backend 계약·권한·저장: `backend/src/Emi.Qms.Api/ProductionPlanning/`
- 프로젝트 생성 요청 알림: `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs`
- Backend 권한·알림 검증: `backend/tests/Emi.Qms.Api.Tests/ProductionPlanningApiTests.cs`
- Frontend API·화면 분기: `frontend/src/api.ts`, `frontend/src/projects.ts`, `frontend/src/App.tsx`
- Frontend 화면 검증: `frontend/tests/App.test.tsx`
- 제품 기준 동기화: `docs/00-product-roadmap.md`

## 5. 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend Release build | 통과, warning 0 / error 0 |
| 신규 Backend 집중 테스트 | 통과 `1/1` — 자기 부서 저장, 교차 부서 차단, 일반 사용자 차단, 품질 8개 범위, 생성 요청·후속 알림·감사이력 |
| 관련 기존 Backend 회귀 | 통과 `3/3` — 프로젝트 생성 기존 알림, 생산계획 저장 후속 업무 중복 방지, 기존 전체 담당자 저장·상태·감사이력 |
| Frontend typecheck | 통과 |
| Frontend lint | 통과, 기존 `main.tsx` Fast Refresh warning 1건만 유지 |
| 신규 Frontend 집중 테스트 | 통과 `1/1` |
| Frontend `App.test.tsx` | 통과 `85/85` |
| Frontend production build | 통과, 기존 chunk-size warning만 유지 |
| 실제 5174 desktop | 설계 부서장 축소 화면과 생산관리 전체 화면 분기 확인 |
| 실제 5174 390px | 가로 넘침 없음, 한 열 입력과 기존 흑백 wireframe 확인 |
| 실제 화면 console | error·warning 0건 |
| `git diff --check` | 통과 |

전체 `ProductionPlanningApiTests` class 재실행은 중단했다. 이 branch에서 이미 검증된 Change 011의 기존 DB 회귀 전체를 다시 반복하면 사용자 요청과 Validation Matrix의 변경 영향 선택 원칙에 비해 시간이 과도해져, 신규 권한·알림 경계의 집중 테스트 `1/1`과 관련 기존 회귀 `3/3`을 선택 실행하는 방식으로 전환했다. 중단한 실행을 통과로 기록하지 않는다.

## 6. Finding·보안·개인정보

- Open P0/P1/P2: `0/0/0`
- 부서 권한은 UI 숨김이 아니라 서버 mutation에서 재검증한다.
- 사용자 후보는 자기 부서 활성 사용자만 반환한다.
- secret, token, 실제 사용자 개인정보와 외부 발송 증빙은 추가하지 않았다.
- 운영 release와 public security smoke가 통과했으며 Open P0/P1/P2는 `0/0/0`이다.

## 7. 기술적 결정과 검토한 대안

- 기존 전체 생산계획 API에 클라이언트 필터만 추가하는 방법은 다른 부서 값 위조 요청을 막지 못해 제거했다.
- 부서별 새 담당자 테이블 대신 기존 책임 구분·row version·감사이력을 재사용해 데이터 이중화를 피했다.
- 담당자 지정 요청을 별도 완료형 내 업무로 만들면 새 상태·종결·에스컬레이션 정책이 필요하므로 이번 범위에서는 인앱 원본 알림으로 제한했다.

## 8. 시행착오 및 폐기한 접근

- Backend 전체 생산계획 테스트 class를 한 번에 다시 실행했으나 기존 DB 회귀가 장시간 반복되어 중단했다. 신규 계약의 집중 테스트와 관련 회귀로 검증 범위를 좁혔다.
- 5174는 현재 HTTP 개발 runtime으로 실행 중이어서 HTTPS 접근을 폐기하고 실제 실행 주소인 HTTP 5174로 화면 검증했다.

## 9. 사용자 검수 결과와 남은 항목

- Codex 화면 검증: 완료
- 사용자 검수: 설계 부서장 실제 화면 확인 뒤 2026-08-14 원격 `main` 병합·Azure 공개배포 승인
- 게시 결과: PR #101 필수 CI run `31772777562`, main SHA `8b19483e40655ce99c13cb470217ccddf444b1c0`, main push CI run `31774158616`, Azure release run `31774236257` 모두 통과
- 운영 결과: Backend·Frontend ready, public security smoke 통과, database migration 없음
- 남은 작업: 운영 관찰. 별도 기능 변경이나 재배포 대기 없음
- 사용자 검수 위치: [체크리스트](project-assignee-delegation-001-user-validation-checklist.md)

## 10. SOP

- 운영 설정 변경과 migration은 필요 없다.
- 기능을 되돌릴 때는 부서장 축소 route/API와 생성 요청 알림 호출을 함께 되돌리고 생산관리 전체 편집 계약은 유지한다.
- 운영 배포 뒤에는 생산관리 전체 편집, 설계·품질 부서장 축소 편집, 일반 사용자 직접 접근 차단, 프로젝트 생성 요청 알림 원장·Teams/PWA 파생 delivery를 순서대로 확인한다.
- 권한 장애 시 사용자 부서, 활성 여부와 `is_department_head`를 먼저 확인하고, 다른 부서 사용자 지정 오류는 후보 사용자 소속을 확인한다.

## 11. User Manual

- 생산관리: 기존처럼 프로젝트 상세에서 `생산계획 수정`을 눌러 계획·실적 연결과 전체 담당자를 관리한다.
- 다른 부서장: 같은 버튼을 누르면 본인 부서 담당자만 보인다. 정·부 담당자를 선택하고 필요한 경우 수정사유를 쓴 뒤 `담당자 저장`을 누른다.
- 품질 부서장: IQC·LQC·OQC·전진검수/FAT의 정·부 담당자를 같은 화면에서 지정한다.
- 일반 사용자: 프로젝트 상세 조회에서 계획과 담당자를 확인할 수 있지만 담당자 수정 버튼은 표시되지 않는다.

## 12. Rollback·Forward-fix

- schema 변경이 없어 DB rollback은 필요 없다.
- 코드 rollback 시 새 endpoint, 축소 화면 분기와 생성 요청 알림을 같은 변경 단위로 되돌린다.
- 이미 저장된 담당자 변경은 기존 프로젝트 담당자 데이터이므로 자동 삭제하지 않는다. 잘못 지정된 값은 기존 감사이력과 수정사유를 남겨 다시 지정한다.

## 13. 5종 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 자동 검증·사용자 검수·운영 게시 완료 | 이 문서 |
| SOP | 작성 완료 | 이 문서 `10. SOP` |
| User manual | 작성 완료 | 이 문서 `11. User Manual` |
| Roadmap update | 원격 main·Azure 운영 게시 완료 반영 | `docs/00-product-roadmap.md` |
| User validation checklist | 설계 실제 화면 확인·운영 게시 완료 | `tasks/project-assignee-delegation-001-user-validation-checklist.md` |
