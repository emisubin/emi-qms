# TASK-007A — Pending List 실험 구현 보고서

> 상태: 실험 구현·자동 검증 완료 / 사용자 검수 대기
> 기준 branch: `experiment/task-007a-pending-list`
> 기준 base SHA: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
> canonical 반영: 미승인 — 대표 repo와 GitHub `main`은 변경하지 않음

## 1. 목적과 범위

프로젝트의 부적합·PUNCH·제조 중단·기타 이슈를 공통 Pending으로 등록하고, 담당 지정·조치·재검사·종결·코멘트·감사 이력을 한곳에서 추적하는 vertical slice를 실험 worktree에 구현했다.

### 포함

- `/pending` 목록·집계·필터·등록 dialog
- `/pending/{id}` 상세·다음 행동·담당 변경·코멘트·감사 timeline
- `Registered → ActionRequested → InProgress → ReinspectionRequested → Closed` forward-only 상태 전이
- `Pending.Read`/`Pending.Manage`, 생성자·담당자·생산관리 actor 규칙
- 기존 프로젝트·내 업무·인앱 알림 연결
- optimistic version, transaction, idempotency key
- Desktop·390px 반응형 UX와 synthetic screenshot

### 제외

- binary 첨부, Excel, PDF
- 프로젝트 상세 안의 별도 Pending tab
- Teams/Mail/provider 실제 발송
- Persistent UAT migration·write·runtime handover
- 유형 관리자, 상태 되돌리기, hard delete
- 대표 repo·GitHub `main`의 commit·push·PR·merge

## 2. 전체 아키텍처와 영향

### DB와 Migration

- `database/migrations/0029_pending_list_foundation.sql`
- `pending_issues`, `pending_comments`, `pending_history`를 추가한다.
- 기존 `work_items`, `notifications`, `notification_recipients`를 재사용한다.
- additive·forward-only migration이며 기존 migration 번호나 내용을 변경하지 않는다.
- 기존 데이터 backfill과 destructive DDL은 없다.

### Backend와 API

- `GET /api/pending`
- `POST /api/pending`
- `GET /api/pending/{id}`
- `POST /api/pending/{id}/transition`
- `POST /api/pending/{id}/assignee`
- `POST /api/pending/{id}/comments`
- `GET /api/pending/assignees`

생성·배정·상태 전이는 transaction 안에서 snapshot, history, work item과 인앱 notification을 함께 갱신한다. version 비교로 경쟁 mutation을 409로 차단하고, 기존 `내 업무` start/complete API가 Pending의 재검사 절차를 우회하지 못하도록 서버에서 409를 반환한다.

### 권한

- 모든 활성 내부 역할과 System Administrator·Read-only: `Pending.Read`
- 업무 역할: `Pending.Manage`
- System Administrator·Read-only: mutation 권한 없음
- permission 통과 후에도 생성자·현재 담당자·생산관리 역할을 개별 mutation에서 확인한다.

### Frontend와 UX

- 기존 수동 router와 공통 shell에 Pending workspace와 상세 route를 추가했다.
- 목록은 진행 중·긴급·기한 초과·재검사 대기·종결 집계와 상태·유형·긴급도 필터를 제공한다.
- 상세는 현재 상태에 맞는 다음 행동 하나를 우선 표시하고 코멘트와 append-only 이력을 함께 보여 준다.
- Pending work item은 `내 업무`에서 자동 시작하거나 완료하지 않고 `Pending 열기`로 상세 화면에 이동한다.
- 390px에서는 navigation, KPI, 필터, 카드와 상세가 한 열로 전환된다.

### 기존 기능·파일 유형 영향

| 영역 | 영향 |
| --- | --- |
| 프로젝트 | Pending이 project FK와 deep link를 사용하며 기존 project state는 변경하지 않음 |
| 내 업무 | 담당 지정 시 Pending target work item 생성, 상태 변경은 Pending 상세에서만 수행 |
| 알림 | 인앱 원본과 recipient만 생성, delivery queue/provider 호출 없음 |
| Excel | N/A — import/export 계약 변경 없음 |
| PDF | N/A — 생성·snapshot 계약 변경 없음 |
| 첨부 | N/A — 보안 저장·검역·보존·복구 정책 미확정으로 binary 입력 자체를 구현하지 않음 |

## 3. 실제 변경 파일

| 경로 | 역할 |
| --- | --- |
| `database/migrations/0029_pending_list_foundation.sql` | Pending schema·permission·role seed |
| `backend/src/Emi.Qms.Api/Pending/PendingContracts.cs` | API DTO·mutation result·상수 |
| `backend/src/Emi.Qms.Api/Pending/PendingStore.cs` | validation·권한 actor·transaction·상태·audit·work item·notification |
| `backend/src/Emi.Qms.Api/Pending/PendingEndpointExtensions.cs` | Pending endpoint와 authorization mapping |
| `backend/src/Emi.Qms.Api/Program.cs` | store 등록과 endpoint mapping |
| `backend/src/Emi.Qms.Api/Identity/QmsPermissions.cs` | Pending permission code |
| `backend/src/Emi.Qms.Api/Identity/SeedIdentityData.cs` | 개발 seed 역할별 permission |
| `backend/src/Emi.Qms.Api/Workflow/WorkflowStore.cs` | Pending deep link와 공용 work item 상태 우회 차단 |
| `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs` | migration 0029 schema·ledger·permission 검증 |
| `frontend/src/pending.ts` | Pending 타입·label·상태 helper |
| `frontend/src/api.ts` | Pending API client |
| `frontend/src/PendingPage.tsx` | 목록·등록·상세 UI |
| `frontend/src/App.tsx` | route·navigation·내 업무 Pending 전용 이동 |
| `frontend/src/styles.css` | Desktop·390px Pending 스타일 |
| `frontend/e2e/full-stack/pending-list.full-stack.spec.ts` | isolated full-stack·권한·상태·work item guard·390px 검증 |
| `tasks/007a-interview.md` | 실험 전용 interview waiver와 Task Identity Gate |
| `tasks/007a-planning.md` | 실험 기획 baseline |
| `tasks/007a-review.md` | Codex 내용 review와 resolution |
| `tasks/007a-screenshots/*.jpg` | synthetic 화면 증빙 |

## 4. 자동 검증·UAT·사용자 검수

| 검증 | 결과 | 비고 |
| --- | --- | --- |
| `dotnet restore backend/Emi.Qms.sln` | PASS | dependency restore 완료 |
| `dotnet build backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release --no-restore --disable-build-servers` | PASS | 경고 0, 오류 0 |
| `dotnet format backend/Emi.Qms.sln --verify-no-changes --no-restore --verbosity minimal` | PASS | 변경 필요 파일 0 |
| `dotnet test backend/Emi.Qms.sln --configuration Release --no-restore --disable-build-servers` | PASS | 362/362, 실패 0, 건너뜀 0 |
| migration 0029 + authorization filtered tests | PASS | 48/48 |
| `corepack pnpm --dir frontend run typecheck` | PASS | TypeScript 오류 0 |
| `corepack pnpm --dir frontend run lint` | PASS | 오류 0, 기존 `main.tsx` Fast Refresh 경고 1 |
| `corepack pnpm --dir frontend test` | PASS | 66/66 |
| `corepack pnpm --dir frontend run build` | PASS | build 성공, 기존 500 kB chunk 경고 유지 |
| `bash scripts/e2e-full-stack.sh e2e/full-stack/pending-list.full-stack.spec.ts` | PASS | 2/2, isolated PostgreSQL tmpfs 정리 완료 |
| Browser desktop smoke | PASS | 목록·등록 dialog·상세를 synthetic data로 확인 |
| Browser 390px smoke | PASS | page overflow 0, 모바일 화면 캡처 |
| Persistent UAT | 미실행 | 사용자 승인 범위 밖이며 이번 실험의 명시적 제외 |
| 실제 provider 발송 | 미실행 | 인앱 원본만 범위에 포함, 실제 외부 발송 금지 |
| 사용자 직접 검수 | 대기 | 화면 증빙 제공 후 사용자 판정 필요 |

코드 구현과 isolated 자동 검증은 완료됐지만 live UAT 검증이나 사용자 검수 완료를 뜻하지 않는다.

## 5. Privacy·Secret 검토

- 검증은 고정 개발 역할과 synthetic 프로젝트·Pending만 사용했다.
- 실제 고객·계정·tenant/client/object ID, credential, token, provider payload를 산출물에 기록하지 않았다.
- Browser screenshot은 synthetic data만 포함한다.
- `.env*`, 인증서, dependency cache와 runtime log는 변경 allowlist에 포함하지 않았다.

## 6. Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `007A-FABLE-OUTPUT` | P3 | `BACKLOG` | Fable read-only planning이 contract-invalid로 artifact를 생성하지 못해 canonical 신규 기능 절차를 충족하지 않음 | 실험은 Codex fallback으로만 진행; 대표 repo 채택 전 Roadmap의 Fable·사용자 gate 재수행 |
| `007A-ATTACHMENT-POLICY` | P2 | `RESOLVED` | 첨부 저장·검역·권한·보존·복구 정책 미정 | binary 첨부를 범위에서 제거하고 UI에 정책 blocker 표시 |
| `007A-ADMIN-OVERRIDE` | P2 | `RESOLVED` | 관리자 mutation 허용 시 업무 책임 경계 우회 | 관리자에는 `Pending.Read`만 부여하고 E2E 403 확인 |
| `007A-WORKITEM-DIVERGENCE` | P2 | `RESOLVED` | 공용 내 업무 start/complete가 Pending 재검사 흐름을 건너뛸 수 있었음 | UI `Pending 열기`, API 409 guard, E2E 회귀 추가 |
| `007A-PROJECT-TAB` | P3 | `BACKLOG` | 전용 workspace와 프로젝트 tab의 navigation 중복 가능성 | 이번 실험은 project deep link만 제공; 실제 사용 후 TASK-007A 후속 범위에서 재평가 |

Open P0/P1/P2는 `0/0/0`이다. P3는 canonical 채택 gate와 후속 UX 판단에 연결돼 있다.

## 7. Known issue·잔여 위험·운영 적용 전 checklist

- 대표 repo 채택 전 canonical Fable planning·Codex review resolution·사용자 구현 승인이 필요하다.
- Persistent UAT에는 migration 0029가 적용되지 않았다.
- attachment external blocker가 남아 있으므로 파일 근거가 필요한 실제 운영은 text comment만 사용할 수 있다.
- actual notification provider, 운영 backup/restore와 rollback rehearsal은 검증하지 않았다.
- PR/CI는 생성하지 않았으므로 GitHub pipeline 결과가 없다.

운영 적용 전에는 최신 `origin/main` 기준 rebase/충돌 검토, fresh·existing DB migration rehearsal, Persistent UAT backup, runtime handover 승인, 사용자 역할별 검수와 별도 게시 승인을 수행해야 한다.

## 8. Rollback·복구·forward-fix

### 실험이 채택되지 않은 경우

대표 repo와 `main`은 이미 보존돼 있다. commit reachable·worktree clean·runtime 미사용을 확인한 뒤 사용자 승인 범위에서 실험 branch/worktree만 정리한다.

### 코드 채택 전 검수 실패

실험 diff를 수정하거나 폐기한다. Persistent DB와 provider를 변경하지 않았으므로 운영 data rollback은 없다.

### migration 적용 후 문제 발견

0029는 additive migration이므로 이미 적용한 migration 파일을 수정하거나 번호를 재사용하지 않는다. 운영 data를 임의 drop하지 않고 새 번호의 forward-fix migration으로 schema·constraint·permission을 보정한다. runtime은 사용자 승인된 이전 release로 handover하되 새 table은 미사용 상태로 보존한다.

## 9. SOP

### isolated 재검증

1. branch가 `experiment/task-007a-pending-list`이고 대표 repo가 아닌지 확인한다.
2. Backend restore·Release build·전체 test를 실행한다.
3. Frontend typecheck·lint·unit·build를 실행한다.
4. `bash scripts/e2e-full-stack.sh e2e/full-stack/pending-list.full-stack.spec.ts`로 tmpfs PostgreSQL full-stack을 실행한다.
5. 종료 로그에서 test 통과와 DB/container/network cleanup을 확인한다.
6. Persistent UAT와 5081/5174 runtime에는 적용하지 않는다.

### 장애 대응

- 409 version 충돌: 상세를 다시 불러오고 최신 version에서 재시도한다.
- 공용 내 업무 start/complete 409: `Pending 열기`로 이동해 상태 변경 사유를 남긴다.
- migration 실패: 기존 migration을 편집하지 않고 ledger와 실패 statement를 확인해 새 forward-fix를 설계한다.
- provider 문제: 이번 기능은 provider queue를 생성하지 않으므로 unrelated delivery worker를 재시작하지 않는다.

## 10. User manual

1. 왼쪽 업무 메뉴에서 `Pending`을 연다.
2. `+ Pending 등록`에서 프로젝트, 유형, 긴급도, 제목과 상세 내용을 입력한다.
3. 담당자를 바로 선택하면 `조치 요청`, 비워 두면 `등록` 상태로 생성된다.
4. 목록의 상태·유형·긴급도 필터와 KPI로 필요한 건을 찾는다.
5. 상세에서 현재 상태에 표시되는 다음 행동을 수행하고 3자 이상의 변경 사유를 남긴다.
6. 담당자는 `조치 시작` 후 코멘트로 근거를 남기고 `재검사 요청`을 수행한다.
7. 생성자 또는 생산관리는 재검사 결과를 확인해 `종결`한다.
8. 잘못된 기록은 삭제하거나 상태를 되돌리지 않고 새 코멘트 또는 후속 Pending으로 정정한다.
9. `내 업무`에 표시된 Pending은 `Pending 열기`로 상세에 들어가 처리한다.
10. 파일은 첨부하지 않고 보안 정책 확정 전까지 코멘트에 근거를 기록한다.

## 11. User validation checklist

상태: `자동 검증 완료` / `사용자 검수 대기`

### 자동 확인

- [x] migration 0029 fresh DB 적용·ledger·permission
- [x] 생성→담당→조치 시작→코멘트→재검사→종결·audit
- [x] viewer/admin mutation 403
- [x] 공용 내 업무 Pending start 409·완료 버튼 미노출
- [x] Desktop 목록·등록 dialog·상세 렌더링
- [x] 390px page overflow 0
- [x] 전체 Backend 362/362·Frontend 66/66

### 사용자 직접 확인

- [ ] 목록 KPI와 필터가 현업 용어에 맞다.
- [ ] 등록 dialog의 필수값과 첨부 보류 안내가 이해된다.
- [ ] 담당자에게 보이는 다음 행동과 코멘트 흐름이 자연스럽다.
- [ ] 재검사·종결 책임 경계가 실제 운영 방식에 맞다.
- [ ] Desktop와 모바일 화면 밀도·가독성이 적절하다.
- [ ] 이 실험을 대표 repo로 채택할지 결정한다.

## 12. 화면 증빙

- [Desktop Pending 목록](007a-screenshots/01-pending-list-desktop.jpg)
- [Pending 등록 dialog](007a-screenshots/02-pending-create-dialog.jpg)
- [Desktop Pending 상세](007a-screenshots/03-pending-detail-desktop.jpg)
- [390px Pending 목록](007a-screenshots/04-pending-list-mobile-390.jpg)

## 13. 5종 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | `tasks/007a-implementation-report.md` |
| SOP | 작성됨 | 이 문서 `9. SOP` |
| User manual | 작성됨 | 이 문서 `10. User manual` |
| Roadmap update | N/A — 실험 전용 | 대표 repo와 canonical 상태 보존이 사용자 요구이므로 `docs/00-product-roadmap.md`는 수정하지 않음. 이 문서가 비적용 사유를 추적함 |
| User validation checklist | 자동 검증 완료·사용자 검수 대기 | 이 문서 `11. User validation checklist` |

## 14. 해결한 업무 문제

부서별로 흩어질 수 있는 예외 이슈를 하나의 상태·담당·조치·재검사·감사 계약으로 연결했다. 담당 지정이 기존 내 업무와 인앱 알림으로 이어져 등록만 되고 방치되는 CRUD를 피했다.

## 15. 기술적 결정과 검토한 대안

- 전용 workspace와 deep link를 채택하고 프로젝트 tab은 사용성 확인 뒤로 보류했다.
- open/close 대신 5단계 forward-only 상태로 조치와 재검사 책임을 분리했다.
- binary 첨부를 추정 구현하지 않고 text-first로 제한했다.
- 관리자 편의를 위해 업무 mutation을 우회시키지 않고 감사 조회만 허용했다.
- 공용 work item API와 Pending 상태를 양방향 동기화하는 대신 Pending을 단일 상태 source로 두고 상세 화면에서만 전이하도록 했다.

## 16. 시행착오 및 폐기한 접근

- Fable read-only planning은 contract-invalid로 산출물을 만들지 못해 canonical 문서라고 가장하지 않고 Codex fallback 실험 기획으로 분리했다.
- 초기 store query의 존재하지 않는 사용자 column 참조를 제거했다.
- 등록 option을 `Promise.all`로 묶던 방식을 `Promise.allSettled`로 바꿔 한 endpoint 실패가 전체 option을 숨기지 않게 했다.
- 공유 mutation reset이 사용자가 입력 중인 상태 사유를 지우던 race를 operation별 reset으로 분리했다.
- 첫 work item guard E2E는 실제 생성 title prefix를 반영하지 않아 실패했고, 실제 API 계약 기준 title로 수정한 뒤 2/2를 재통과했다.
- 샌드박스 내부 MSBuild named pipe 실패는 권한 경계 밖에서 동일 명령을 재실행해 362/362 결과로 확정했다.
- 샌드박스 내부 `dotnet format` build host도 같은 named pipe 제한으로 실패해 동일한 읽기 전용 명령을 허용된 경계에서 재실행하고 PASS를 확정했다.

## 17. 사용자 검수 결과와 남은 항목

자동 검증과 synthetic 화면 캡처는 완료했다. 사용자 직접 검수와 대표 repo 채택 여부는 아직 결정되지 않았다. canonical Product Roadmap의 다음 Gate는 계속 `TASK-007A Fable deep-interview → planning → Codex review → 사용자 승인`이며, 이번 실험 결과는 그 승인을 대신하지 않는다.
