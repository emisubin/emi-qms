# TASK-WORKFLOW-CONTINUITY-001 Change 007 구현 보고 — 구매 변경 상세 인계와 자재 입고 단순화

## 구현 결과

1. 구매 담당자가 구매품을 신규 등록하거나 변경하면 자재 정·부 담당자의 알림과 내 업무에 실제 변경 필드가 함께 기록된다.
   - 신규 입력은 품목명·구분·발주수량·입고예정일·이슈사항 등 입력된 주요 정보를 표시한다.
   - 변경은 바뀐 필드만 `입고예정일 변경 7/22 -> 7/23`, `이슈사항 변경 - -> 하루 늦게 들어오기로 함` 형식으로 표시한다.
   - 빈 값은 `-`, 날짜는 `M/d`, 수량은 단위를 포함한 값으로 정규화한다.
   - 동일 품목 버전 재처리는 기존 멱등키를 유지해 중복 알림과 업무를 만들지 않는다.
2. 자재 입고 관리 화면을 프로젝트 우선 구조로 바꿨다.
   - 첫 화면은 프로젝트를 한 행씩 표시하고 선택한 프로젝트 바로 아래에 구매품을 한 행씩 펼친다.
   - 각 구매품 행의 `도착입력`을 누르면 도착 수량·도착일·비고·저장·취소 입력부가 해당 행 아래에 열린다.
   - 저장된 도착분은 기존 transaction 안에서 도착분별 IQC 회차, 품질 정·부 업무와 알림을 생성한다.
3. IQC 합격 후 자재 정·부 담당자 각각에게 `입고 확정` 내 업무를 만들고 두 담당자 모두에게 알림을 보낸다.
4. 자재 담당자가 입고 확정하면 별도 `품목 입고 마감` 없이 누적 확정 수량을 계산한다.
   - 발주·제공 예정 수량 미만이면 구매정보에 `부분 입고 확정수량/예정수량 단위`로 표시하고 추가 도착을 허용한다.
   - 예정 수량에 도달했고 미확정 도착분이 없으면 품목을 자동 마감하고 `입고 완료` projection을 갱신한다.
   - 기존 수동 마감 API는 하위 호환을 위해 유지하지만 정상 화면에서는 노출하지 않는다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-007-F01` | P1 | Resolved | 구매→자재 인계가 품목 버전만 구분하고 실제 변경 필드를 알림·업무 설명에 전달하지 않았다. | 신규·변경 필드 집합을 안정된 표시 형식으로 직렬화해 자재 인계 메시지와 내 업무 설명에 포함했다. |
| `WF-007-F02` | P1 | Resolved | IQC 합격 후 입고 확정 업무가 자재 정 담당자 한 명에게만 생성됐다. | 자재 정·부 각각의 멱등 업무를 생성하고 두 담당자를 알림 수신자로 고정했다. |
| `WF-007-F03` | P1 | Resolved | 수량 충족 뒤에도 별도 수동 마감이 필요해 입고 완료 projection이 늦거나 누락될 수 있었다. | 입고 확정 transaction에서 누적 수량과 활성 도착분을 검사해 전량이면 자동 마감하고 부분이면 부분 입고 projection을 저장한다. |
| `WF-007-F04` | P2 | Resolved | 자재 화면이 품목별 대형 카드를 최상위로 렌더링해 프로젝트와 품목 수가 늘수록 탐색 비용이 커졌다. | 프로젝트 행→구매품 행→행 내부 도착 입력의 3단 압축 구조로 재구성하고 모바일도 동일한 정보 위계로 맞췄다. |

Open P0/P1/P2: `0/0/0`.

## 변경 파일

- Backend: `backend/src/Emi.Qms.Api/Procurement/ProcurementStore.cs`, `backend/src/Emi.Qms.Api/Materials/MaterialsStore.cs`
- Frontend: `frontend/src/MaterialsWorkspace.tsx`, `frontend/src/DepartmentProjectHub.tsx`, `frontend/src/App.tsx`, `frontend/src/styles.css`
- Tests: `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`, 관련 Frontend unit·Full-Stack E2E 계약
- Task artifacts: `tasks/workflow-continuity-001-change-007.md`, 본 구현 보고

## 검증

- Backend build: `dotnet build Emi.Qms.sln --no-restore` — 성공, warning/error `0/0`
- Backend 전체 회귀: `dotnet test Emi.Qms.sln --no-restore --logger "console;verbosity=normal"` — `420/420` 통과
- Frontend unit: `pnpm run test` — `19` files, `125/125` 통과
- Frontend App unit: `pnpm exec vitest run tests/App.test.tsx` — `70/70` 통과
- Frontend typecheck: `pnpm run typecheck` — 통과
- Frontend lint: `pnpm run lint` — error `0`, 기존 `src/main.tsx` Fast Refresh warning `1`
- Frontend build: `pnpm run build` — 통과, 기존 chunk-size warning만 존재
- Git whitespace: `git diff --check` — 통과
- 고정 runtime:
  - Frontend `http://127.0.0.1:42983` — HTTP `200`
  - Backend `http://127.0.0.1:41166/health/ready` — `status=ok`, `database.isReady=true`

## 검수·게시 경계

- 인앱 브라우저는 localhost 접근 보안 정책에 의해 차단되어 이번 change의 신규 화면 screenshot은 생성하지 못했다. 고정 runtime의 HTTP·ready 상태와 자동화 검증은 정상이다.
- 사용자 수동 검수는 대기 중이다.
- local experiment 작업만 수행했으며 commit·push·PR·대표 repo·`main`·Persistent UAT·실제 provider 호출은 하지 않았다.
- `main` merge 승인: `0/3`.
