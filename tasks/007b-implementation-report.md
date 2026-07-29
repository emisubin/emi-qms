# TASK-007B — 패널·프로젝트 병목 상태 집계 실험 구현 보고서

> 상태: 실험 구현·자동 검증 완료 / 사용자 검수 대기
> 기준 branch: `experiment/task-007b-bottleneck-status`
> 실험 branch base SHA: `c7abd300722c29a0a378425cfee856b4c5fe4398`
> canonical `main` SHA: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
> canonical 반영: 미승인 — 대표 repo, GitHub `main`, Persistent UAT를 변경하지 않음

## 1. 목적과 범위

프로젝트 목록과 상세에서 “지금 어디를 먼저 확인해야 하는지”를 바로 판단할 수 있도록 프로젝트 단계, 패널 7개 구간과 open Pending을 결합한 병목 요약을 계산형 조회로 구현했다.

### 포함

- 프로젝트 목록의 병목 label·Pending 집계·서버 우선 정렬
- 프로젝트 상세의 `다음 확인 대상`, Pending 집계와 패널 7구간 matrix
- 프로젝트별 Pending 목록 deep link와 필터
- `Pending.Read` 기반 count·정렬 정보 노출 제어
- Desktop·390px 반응형 UX와 synthetic screenshot
- 기존 프로젝트 진행률·18단계 Workflow 계약 보존

### 제외

- 병목 snapshot table, migration과 background aggregate worker
- 사용자 정렬 전환 UI
- 패널 단위 Pending 연결과 예측·추천
- Home widget과 알림 자동 발송
- Persistent UAT write·runtime handover
- 대표 repo·GitHub `main`의 commit·push·PR·merge

## 2. 기획·Review 결정

- Fable 5 read-only runner가 2회 interview 원문과 planning 전문을 작성했다.
- 사용자의 실험 브랜치 지침에 따라 Fable 권장안인 **A. 계산형 조회**를 채택했다.
- 프로젝트 단계와 병목은 같은 의미로 합치지 않고 `현재 단계`와 `다음 확인 대상`으로 분리했다.
- open Pending은 기존 단계 label을 덮어쓰지 않지만, 목록 정렬과 다음 행동에서 우선한다.
- 패널 상태는 실제 저장 정밀도에 맞춰 제조 전·제조 중·제조 완료·검사 중·검사 완료·포장 완료·납품 완료 7구간만 표시한다.
- 페이지네이션 이후 client 정렬을 금지하고 SQL에서 정렬한 뒤 page를 자른다.
- 요청한 `GPT 5.6 Sol`은 현재 실행 환경에 선택 가능한 reviewer로 노출되지 않아 해당 모델이라고 주장하지 않았고, 현재 Codex가 `tasks/007b-review.md`의 내용 review를 수행했다.

## 3. 구현 구조

### Backend

- `ProjectBottleneckDomain`이 lifecycle, 프로젝트 coarse stage, 패널 7구간, unknown 상태와 Pending overlay를 하나의 응답으로 계산한다.
- `GET /api/projects`와 `GET /api/projects/{id}`에 additive `bottleneck`을 반환한다.
- 목록은 lifecycle → permission-aware open Pending → 단계 rank → 납기일 순으로 서버 정렬한다.
- Pending count는 `Pending.Read`가 있을 때만 JSON에 포함하며, 해당 권한이 없으면 Pending 기반 정렬도 적용하지 않는다.
- `GET /api/pending?projectId=<guid>`가 프로젝트별 목록과 집계를 반환한다.

### Frontend

- 프로젝트 목록 Desktop·mobile 카드에 병목 label과 Pending count를 표시한다.
- 프로젝트 상세에 다음 행동, Pending 3종 집계와 7구간 matrix를 표시한다.
- 목록·상세의 Pending action은 `/pending?projectId=...`로 이동한다.
- Pending 화면에 프로젝트 필터를 추가하고 URL 초기값을 선택 상태로 반영한다.
- 로그인 화면에서 확립한 red·white surface, soft-red emphasis와 rounded card를 그대로 사용했다.

### DB·migration

- N/A — 기존 `projects`, `panel_placeholders`, `pending_issues`를 조회 시 집계한다.
- schema, migration ledger와 Persistent DB를 변경하지 않았다.

## 4. 실제 변경 파일

| 경로 | 역할 |
| --- | --- |
| `backend/src/Emi.Qms.Api/Projects/ProjectBottleneckDomain.cs` | 병목 계산 domain |
| `backend/src/Emi.Qms.Api/Projects/ProjectContracts.cs` | additive 병목 response contract |
| `backend/src/Emi.Qms.Api/Projects/ProjectStore.cs` | 패널·Pending 집계와 pagination 전 정렬 |
| `backend/src/Emi.Qms.Api/Projects/ProjectEndpointExtensions.cs` | Pending 권한 projection 전달 |
| `backend/src/Emi.Qms.Api/Pending/PendingEndpointExtensions.cs` | `projectId` filter binding |
| `backend/src/Emi.Qms.Api/Pending/PendingStore.cs` | 프로젝트별 Pending 목록·summary |
| `backend/tests/Emi.Qms.Api.Tests/ProjectBottleneckDomainTests.cs` | 단계·Pending·lifecycle·unknown 단위 검증 |
| `frontend/src/projects.ts` | 병목 frontend type |
| `frontend/src/api.ts` | 프로젝트 page size·Pending project filter query |
| `frontend/src/App.tsx` | 목록 badge·상세 overview·deep link·mobile UI |
| `frontend/src/PendingPage.tsx` | 프로젝트 필터와 URL 초기값 |
| `frontend/src/styles.css` | 로그인 기반 병목 Desktop·390px style |
| `frontend/e2e/full-stack/project-bottleneck.full-stack.spec.ts` | 정렬·deep link·상세·390px isolated E2E |
| `tasks/007b-interview*.md` | Fable 질문·요약 원문과 확인 상태 |
| `tasks/007b-planning.md` | Fable 5 primary planning |
| `tasks/007b-review.md` | Codex 내용 review와 resolution |
| `tasks/007b-change-001.md` | 실험 구현 자동 진행·merge 0/3 결정 |
| `tasks/007b-screenshots/*.jpg` | synthetic 화면 증빙 |

## 5. 실행한 검증과 결과

| 검증 | 결과 | 비고 |
| --- | --- | --- |
| `dotnet build backend/Emi.Qms.sln` | PASS | 경고 0, 오류 0 |
| `dotnet build backend/src/Emi.Qms.Api/Emi.Qms.Api.csproj --configuration Release` | PASS | E2E용 최신 Release 생성 |
| `dotnet format backend/Emi.Qms.sln --verify-no-changes --no-restore --verbosity minimal` | PASS | formatting drift 없음 |
| 병목 domain filtered tests | PASS | 6/6 |
| `dotnet test backend/Emi.Qms.sln --configuration Release` | PASS | 368/368, 실패 0, 건너뜀 0 |
| `npm run typecheck` | PASS | TypeScript 오류 0 |
| `npm run lint` | PASS | 오류 0, 기존 `main.tsx` Fast Refresh 경고 1 |
| `npm test` | PASS | 66/66 |
| `npm run build` | PASS | build 성공, 기존 500 kB chunk 경고 유지 |
| `bash scripts/e2e-full-stack.sh e2e/full-stack/project-bottleneck.full-stack.spec.ts` | PASS | 1/1, 격리 PostgreSQL tmpfs 정리 완료 |
| Browser Desktop smoke | PASS | 목록·상세·프로젝트별 Pending 확인 |
| Browser 390px smoke | PASS | horizontal overflow 0 |
| Browser console error | PASS | error 0 |
| Persistent UAT | 미실행 | 실험 범위 밖, 대표 runtime 보존 |
| 실제 provider 발송 | 미실행 | 기능 범위 밖이며 외부 발송 금지 |
| 사용자 직접 검수 | 대기 | 본 화면 증빙으로 판정 예정 |

## 6. Privacy·Secret 검토

- E2E와 screenshot은 고정 개발 역할과 synthetic 고객사·프로젝트·Pending만 사용했다.
- 실제 계정, 고객, tenant/client/object ID, credential, token, provider payload와 raw runtime log를 tracked 산출물에 기록하지 않았다.
- `.env*`, 인증서, dependency cache와 Persistent UAT data를 변경하지 않았다.

## 7. Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `007B-PANEL-GRANULARITY` | P2 | `RESOLVED` | 실제 패널에는 18단계 정밀도가 없어 잘못된 세부 단계 표시 위험 | 7개 고정 구간과 coarse project stage만 표시 |
| `007B-PAGINATION-SORT` | P2 | `RESOLVED` | client 정렬은 page 밖 우선 프로젝트를 놓침 | SQL 정렬 후 pagination 적용, E2E 순서 검증 |
| `007B-PENDING-LEAK` | P2 | `RESOLVED` | 권한 없는 actor에게 count 또는 순서로 Pending 존재가 노출될 수 있음 | endpoint permission을 field·정렬 양쪽에 적용 |
| `007B-SNAPSHOT` | P3 | `RESOLVED` | persisted aggregate는 stale·migration·이중 source 위험 | 계산형 조회 채택, migration 없음 |
| `007B-SORT-TOGGLE` | P3 | `BACKLOG` | 기존 정렬 전환 UI는 MVP 필수 아님 | 실제 검수 후 후속 change 후보 |
| `007B-REVIEWER-AVAILABILITY` | P3 | `BACKLOG` | 요청한 `GPT 5.6 Sol` model selector가 환경에 없어 해당 reviewer 검증을 증명할 수 없음 | Codex review로 실험 진행, canonical 채택 시 사용 가능한 독립 reviewer로 재검토 |
| `007B-INDEPENDENT-SESSION` | P3 | `BACKLOG` | 이번 대화는 단일 Codex session에서 구현 후 별도 read-only 검증 pass를 수행 | canonical 게시 전 분리된 검증 session 필요 |

Open P0/P1/P2는 `0/0/0`이다. P3 backlog는 실험 결과 사용을 막지 않지만 canonical 게시 전 재평가한다.

## 8. Rollback·복구

- 실험이 채택되지 않으면 대표 repo와 `main`은 이미 보존돼 있으므로 이 branch의 commit reachable 여부와 worktree 미사용 상태를 확인한 뒤 승인 범위에서만 정리한다.
- Persistent DB와 provider mutation이 없어 운영 data rollback은 없다.
- 계산 query에 성능 문제가 발견되면 schema를 즉시 추가하지 않고 query plan·실측을 먼저 수집한 뒤 새 Task에서 snapshot 대안을 검토한다.

## 9. SOP

1. branch가 `experiment/task-007b-bottleneck-status`인지 확인한다.
2. Backend Release build·format·전체 test를 실행한다.
3. Frontend typecheck·lint·unit·build를 실행한다.
4. `bash scripts/e2e-full-stack.sh e2e/full-stack/project-bottleneck.full-stack.spec.ts`를 실행한다.
5. E2E 종료 시 DB·container·network 정리를 확인한다.
6. `/`, `/projects/{id}`, `/pending?projectId={id}`와 390px를 synthetic data로 검수한다.
7. Persistent UAT·5081·5174와 대표 repo에는 적용하지 않는다.

## 10. User manual

1. 프로젝트 목록의 `병목 구간`을 보고 우선 확인 프로젝트를 찾는다.
2. open Pending이 있으면 목록 상단에 우선 배치되며 `Pending 확인`으로 해당 프로젝트 이슈만 연다.
3. 프로젝트 상세의 `다음 확인 대상`에서 권장 행동을 확인한다.
4. `프로젝트 Pending 열기`는 project filter가 선택된 Pending 목록으로 이동한다.
5. 패널 7구간 matrix에서 각 구간의 면수를 확인하고 버튼으로 패널 영역을 연다.
6. `일부 계산 불가`가 표시되면 추정하지 말고 패널 원본 상태를 확인한다.

## 11. User validation checklist

상태: `자동 검증 완료` / `사용자 검수 대기`

### 자동 확인

- [x] open Pending 프로젝트가 같은 lifecycle 안에서 우선 정렬됨
- [x] project stage와 Pending next attention이 분리되어 표시됨
- [x] 패널 7구간과 면수가 상세에 표시됨
- [x] 프로젝트별 Pending deep link·filter·summary가 연결됨
- [x] Desktop 목록·상세·Pending 화면 렌더링
- [x] 390px horizontal overflow 0
- [x] Backend 368/368·Frontend 66/66·Full-Stack E2E 1/1

### 사용자 직접 확인

- [ ] `병목 구간`과 `다음 확인 대상` 용어가 현업에서 이해하기 쉽다.
- [ ] open Pending 우선 정렬이 실제 업무 우선순위에 맞다.
- [ ] 7개 구간이 너무 거칠거나 과도하지 않다.
- [ ] Desktop와 390px 화면 밀도·가독성이 적절하다.
- [ ] 이 실험을 계속 수정할지 또는 대표 repo 채택 후보로 둘지 결정한다.

## 12. 화면 증빙

- [Desktop 프로젝트 목록](007b-screenshots/01-project-list-desktop.jpg)
- [Desktop 프로젝트 상세 병목 카드](007b-screenshots/02-project-detail-desktop.jpg)
- [프로젝트 필터 Pending 목록](007b-screenshots/03-pending-project-filter.jpg)
- [390px 프로젝트 상세 병목 카드](007b-screenshots/04-project-detail-mobile-390.jpg)

## 13. 5종 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | `tasks/007b-implementation-report.md` |
| SOP | 작성됨 | 이 문서 `9. SOP` |
| User manual | 작성됨 | 이 문서 `10. User manual` |
| Roadmap update | N/A — 실험 전용 | canonical roadmap 보존이 사용자 요구이므로 `docs/00-product-roadmap.md`는 수정하지 않음 |
| User validation checklist | 자동 검증 완료·사용자 검수 대기 | 이 문서 `11. User validation checklist` |

## 14. 시행착오와 해소

- 첫 backend build는 저장소 root가 아닌 실제 `backend/Emi.Qms.sln` 위치로 바로잡았다.
- 첫 E2E는 server가 `Release --no-build`의 이전 binary를 사용해 `bottleneck`이 없었고, Release build 후 같은 E2E를 재실행해 통과했다.
- 단위 테스트의 첫 재실행은 `--no-build`로 이전 test binary를 사용했고, 최신 build 포함 실행에서 6/6을 확인했다.
- 전체 backend test를 중복 실행했을 때 대기 시간이 늘어 중복 프로세스를 정리한 뒤 단일 Release 실행으로 368/368을 확정했다.
- screenshot host 종료 직후 child server가 잠시 남아 해당 실험 process만 종료하고 port·Docker resource 정리를 재확인했다.

## 15. 사용자 검수 결과와 Roadmap

자동 검증과 synthetic 화면 캡처는 완료했으나 사용자 직접 검수는 아직 대기다. 대표 repo와 canonical Roadmap은 변경하지 않았으므로 공식 다음 Gate는 계속 `TASK-007A Fable deep-interview → planning → Codex review → 사용자 승인`이다. 이 실험 branch의 007A·007B 결과는 canonical 승인이나 merge를 대신하지 않는다.
