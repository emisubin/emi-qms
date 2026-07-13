# TASK-E2E-RELIABILITY-001 Implementation Report

## 1. 결과

구매정보 편집 화면의 겹친 load에서 이전 응답이 사용자 입력 state를 덮어쓰는 race를 request-id guard로 차단했다. E2E는 임의 sleep, 재클릭과 분리된 input count polling 대신 새 행 하나의 준비 계약을 직접 검증한다.

## 2. 해결한 업무 문제와 Root cause

- Finding: `FULL_STACK_E2E_PROCUREMENT_EDIT_ROW_RACE`
- 분류: `STALE_ASYNC_RESPONSE_OVERWRITES_EDIT_STATE`
- React StrictMode에서 편집 component의 load effect가 중복 실행될 수 있었다.
- 첫 응답이 편집 table을 노출한 뒤 사용자가 추가한 row를 늦은 응답의 `setRows`가 제거할 수 있었다.
- 기존 test retry는 source race를 제거하지 못하고 두 번째 `행 추가`로 중복 행 가능성을 만들었다.

수정 전 deterministic unit regression은 첫 응답만 완료해도 빈 편집 table이 노출됨을 재현해 실패했다. 수정 후 같은 test는 최신 요청이 끝날 때까지 table을 노출하지 않고 통과한다.

## 3. 기술적 결정과 검토한 대안

- `ProcurementEditPage.load`마다 증가하는 request id를 발급한다.
- success와 failure callback 모두 현재 id와 일치할 때만 state를 반영한다.
- 기존 panel edit와 project edit에서 사용하는 Repository 내부 request-id 패턴을 재사용한다.
- E2E helper는 새 row count를 정확히 `initial + 1`로 확인하고 해당 row의 input 8개를 검증한다.
- E2E에서 버튼 재클릭, 250ms sleep과 15초 input-count polling을 제거했다.

- timeout 확대: race를 늦출 뿐 stale response overwrite를 해결하지 않아 폐기했다.
- 실패 시 행 추가 재클릭: 중복 row를 만들 수 있어 폐기했다.
- API로 row 생성 후 UI 조회: 직접 입력 UI 계약을 검증하지 못해 폐기했다.
- AbortController 도입: 현재 API helper signature 확대가 필요해 최소 변경 원칙상 선택하지 않았다.

## 4. 시행착오 및 폐기한 접근

- 새 worktree의 첫 E2E 실행은 Release binary가 없어 test 진입 전 실패했다. 이를 결함 재현 증빙에서 제외하고 Release build 후 재실행했다.
- 현재 source의 대상 E2E는 로컬 3/3과 후속 main CI에서 통과했다. 간헐 timing 문제이므로 이것만으로 해결로 오판하지 않고, 두 HTTP 응답 순서를 직접 제어하는 regression test를 추가했다.
- regression test는 수정 전 stale 첫 응답이 빈 edit table을 노출하는 동작을 확정적으로 재현했다.
- 기존 timeout 확대와 버튼 재클릭은 race를 해결하지 않고 중복 입력 위험이 있어 제거했다.

## 5. 실제 변경 파일과 역할

- `frontend/src/App.tsx`: 구매정보 edit load의 최신 request guard
- `frontend/tests/App.test.tsx`: StrictMode stale-response deterministic regression
- `frontend/e2e/full-stack/project-registration.full-stack.spec.ts`: 단일 click과 exact row readiness
- `docs/00-product-roadmap.md`: P2 상태와 검증 결과
- `tasks/e2e-reliability-001*.md`: Task·Implementation report·SOP·User manual

## 6. 영향

- Frontend source: stale response 차단, 표시·API contract 변경 없음
- Frontend test/E2E: deterministic regression과 readiness 보강
- Backend/API/DB/Migration: 변경 없음
- Runtime/Persistent UAT/provider: 변경 없음
- 권한·workflow·Excel/PDF/첨부: 변경 없음. 기존 구매 Excel과 자재 입고 흐름은 전체 E2E에서 회귀 검증

## 7. 검증

| 검증 | 결과 |
| --- | --- |
| 수정 전 deterministic regression | FAIL 재현 |
| 수정 후 targeted unit regression | PASS |
| 대상 Full-Stack E2E 수정 전 반복 | 3/3 PASS — timing flake 특성 확인 |
| 대상 Full-Stack E2E 수정 후 반복 | PASS, 20/20 |
| Frontend lint | PASS, error 0·기존 Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend unit | PASS, 62/62 |
| Frontend build | PASS, 기존 chunk-size warning 1 |
| Backend Release build | PASS, warning/error 0/0 — E2E prerequisite |
| Full-Stack E2E 전체 | PASS, 16/16 |
| isolated E2E cleanup | PASS, Task-owned DB/container/network 잔여 0 |
| actionlint | PASS |
| git diff·문서 link·heading·secret/PII·allowlist | PASS, 오류·후보·범위 밖 변경 0 |
| 독립 Codex 검증 | PASS_WITH_LIMITATION, read-only static diff·계약·문서 검토 PASS; `git diff --check`는 구현 세션의 PASS 증빙 사용 |

## 8. 개인정보와 보안

Synthetic E2E data만 사용한다. Persistent UAT를 조회하거나 변경하지 않으며 actual provider credential과 호출이 없다.

## 9. Rollback

Frontend request-id guard, unit regression과 E2E helper 변경을 함께 revert한다. DB·migration·runtime rollback은 없다.

## 10. Findings

- P0/P1: 0
- 해결 대상 P2: `FULL_STACK_E2E_PROCUREMENT_EDIT_ROW_RACE`
- 신규 P2/P3: 0
- 독립 검증 user-review gate: GO — 사용자 검수·병합 승인 완료

## 11. 사용자 검수 결과와 남은 항목

사용자가 자동 검증 결과와 수동 검수 checklist를 승인하고 Commit·Push·PR·squash merge를 명시적으로 승인했다.

## 12. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 자동 검증 완료 |
| SOP | `tasks/e2e-reliability-001-sop.md` | 자동 검증 완료 |
| User manual | `tasks/e2e-reliability-001-user-manual.md` | 사용자 검수 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 자동 검증 완료 |
| User validation checklist | `tasks/e2e-reliability-001.md` 8장 | 사용자 검수 완료 |
