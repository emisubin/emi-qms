# TASK-011A Change 002 구현 보고서 — 제조 단계 저장 실행 잠금

## 상태와 경계

- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- Task 유형: `BUGFIX`
- canonical Task: `TASK-011A Change 002`
- Fable: `NOT_APPLICABLE` — 확정된 순차 mutation·중복 submit 차단 계약의 P2 결함을 보정한다.
- Backend·API·DB·migration·권한·Pending·LQC·Persistent UAT·실제 provider·대표 repo·`main`: 변경 없음
- commit·push·PR·merge: 미실행, main merge 승인 `0/3`

## 해결한 문제와 구현

- `savingAction` state가 render되기 전 같은 event tick에 여러 click이 들어오면 mutation handler가 중복 시작할 수 있었다.
- React state보다 먼저 반영되는 `mutationInFlightRef`로 제조 화면 전체 mutation을 직렬화했다.
- request 성공 후 queue·detail refresh까지 끝나야 잠금을 해제하며, 저장 중에는 제조 action·project·panel 선택을 막는다.
- focus card에 `aria-busy`를 추가하고 `제조 단계를 저장하는 중입니다. 완료될 때까지 잠시 기다려 주세요.`를 노출했다.
- 같은 1단계 버튼을 동일 tick에 3회 눌러도 server POST는 1건만 발생하고, refresh 후 2단계만 활성화된다.

## 변경 파일

- `frontend/src/ManufacturingPage.tsx`
- `frontend/tests/ManufacturingPage.test.tsx`
- `frontend/e2e/full-stack/manufacturing-work.full-stack.spec.ts`
- `tasks/011a-change-002.md`
- `docs/00-product-roadmap.md`
- `docs/27-experiment-task-ledger.md`

## 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | `PASS` |
| Manufacturing unit | `PASS` — 3/3 |
| 동일 tick 3회 click | `PASS` — check-step POST 1건 |
| 격리 Full-Stack E2E | `PASS` — 1 scenario |
| 4단계·Pending·재개·완료·LQC 회귀 | `PASS` |
| 390px 화면 | `PASS` — 저장 중·4/4 screenshot, overflow 0 |
| 격리 DB·container·network cleanup | `PASS` |

Screenshot은 합성 데이터만 사용해 `/tmp/emi-qms-p2-remediation-evidence/` 안에 생성했다.

## Finding gate

| ID | Severity | 상태 | 해소 |
| --- | --- | --- | --- |
| `MANUFACTURING-RAPID-STAGE-SAVE-LOSS` | P2 | `RESOLVED` | synchronous ref fence, 저장 중 피드백·선택 잠금, 3-click/1-POST E2E |

이 Change의 Open P0/P1/P2는 `0/0/0`이다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 한 단계 저장 완료→다음 단계 활성 순서를 본 문서에 고정 |
| User manual | 완료 | `확인 중…`과 저장 안내가 보이면 다음 단계가 열릴 때까지 기다린다. |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` TASK-011A Change 002 |
| User validation checklist | `BATCHED_FINAL` | 자동 검증 완료, 최종 일괄 사용자 검수 대기 |

## Rollback

Frontend fence·feedback·test diff만 되돌린다. DB·migration·runtime·`main` rollback은 필요 없다.
