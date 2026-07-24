# TASK-WORKFLOW-CONTINUITY-001 Change 008 구현 보고 — 알림 상세 분리와 부분 입고 표시 보정

## 요청별 구현 결과

1. 기존 알림·내 업무 제목은 원래 열에 유지하고, PC 표 맨 오른쪽에 `상세 내용` 열을 추가해 기존 본문과 구매 변경값을 옮겼다. 아래쪽에 별도 상세 박스는 만들지 않았으며 값 변화는 `7/22 → 7/23`처럼 실제 화살표를 사용한다.
2. IQC 합격 알림 문구는 유지했다. `7/23 도착분이 IQC 합격했습니다. 입고 확정을 진행해 주세요.`를 포함한 기존 본문은 맨 오른쪽 `상세 내용` 열에 표시한다.
3. 입고 확정 projection 메모가 없는 기존 부분 입고도 구매 조회에서 확정 수량을 집계해 `부분 입고 4/10 EA`처럼 표시한다.
4. 자재 입고 현황의 잔여 수량을 `예정 수량 - 도착 수량`이 아니라 `예정 수량 - 입고 확정 수량`으로 수정했다. 전량 도착했지만 4/10만 확정된 경우 `확정 4 EA · 잔여 6 EA`로 표시한다.
5. Pending 알림의 사유도 PC 표 맨 오른쪽 `상세 내용` 열에 표시한다.
6. 내 업무 desktop 표도 같은 맨 오른쪽 열 구조를 사용한다. mobile card와 알림 상세 화면은 표 열을 만들지 않고 기존 위치에 평면 본문만 표시한다.
7. 자재 발주품목 행 자체를 누르면 도착·IQC 이력이 바로 아래에 열린다. 작은 이력 summary control은 제거했고 Enter·Space keyboard 조작을 지원한다. checkbox·도착입력·이력 내부 button은 행 toggle과 충돌하지 않는다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-008-F01` | P1 | Resolved | 변경 상세를 기존 알림·업무 제목 셀 아래에 쌓아 표 구조와 정보 위계가 맞지 않았다. | 제목은 기존 열에 남기고 PC 표의 작업 열 다음 맨 오른쪽에 `상세 내용` 열을 추가해 본문과 변경값을 이동했다. |
| `WF-008-F02` | P1 | Resolved | desktop 내 업무에는 설명 전용 열이 없고 IQC 합격 안내가 제목 아래 두 번째 줄로 표시됐다. | 알림·내 업무가 동일한 맨 오른쪽 상세 열을 사용하고 모바일·상세 화면에서는 별도 박스 없이 평면 본문으로 표시한다. |
| `WF-008-F03` | P1 | Resolved | `remainingQuantity`가 confirmed가 아니라 arrived를 차감해 전량 도착 즉시 잔여가 0이 됐다. | 잔여는 confirmed 기준으로 계산하고 사급 지연 판정만 별도 arrival remaining을 유지했다. |
| `WF-008-F04` | P1 | Resolved | 과거 부분 입고는 `receipt_completion_note`가 null이면 구매 화면에서 미확정으로 보였다. | 구매 read projection이 기존 메모를 우선 사용하되 null이면 Confirmed 도착분을 집계해 부분 입고 문구를 계산한다. |
| `WF-008-F05` | P2 | Resolved | 도착·IQC 이력이 작은 summary control 뒤에 숨겨져 품목과 이력의 관계가 약했다. | 품목 행 전체를 accessible disclosure trigger로 바꾸고 이력은 같은 행 바로 아래에 표시한다. |

Open P0/P1/P2: `0/0/0`.

## 변경 파일

- Backend: `backend/src/Emi.Qms.Api/Procurement/ProcurementStore.cs`, `backend/src/Emi.Qms.Api/Materials/MaterialsStore.cs`
- Frontend: `frontend/src/App.tsx`, `frontend/src/MaterialsWorkspace.tsx`, `frontend/src/styles.css`
- Tests: `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`, `frontend/tests/App.test.tsx`, 관련 Full-Stack E2E 계약
- Task artifacts: `tasks/workflow-continuity-001-change-008.md`, 본 구현 보고

## 검증

- Backend 구매·자재 집중 회귀: `ProcurementApiTests` — `23/23` 통과
- Backend 전체 회귀: `420/420` 통과
- Frontend 전체 unit: `19` files, `126/126` 통과
- Frontend App 집중 unit: `71/71` 통과
- Frontend typecheck: 통과
- Frontend lint: error `0`, 기존 `src/main.tsx` Fast Refresh warning `1`
- Frontend build: 통과, 기존 chunk-size warning만 존재
- Git whitespace: `git diff --check` — 통과
- 고정 Frontend `http://127.0.0.1:42983` — HTTP `200`
- 고정 Backend `http://127.0.0.1:41166/health/ready` — `status=ok`, `database.isReady=true`

## 검수·게시 경계

- 사용자 수동 검수는 대기 중이다.
- local experiment 작업만 수행했으며 commit·push·PR·대표 repo·`main`·Persistent UAT·실제 provider 호출은 하지 않았다.
- `main` merge 승인: `0/3`.
