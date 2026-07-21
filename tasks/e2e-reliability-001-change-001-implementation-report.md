# TASK-E2E-RELIABILITY-001 Change 001 구현 보고서 — 구매정보 초기 load 동작 잠금

## 상태와 안전 경계

- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- Task 유형: `BUGFIX`
- canonical Task: `TASK-E2E-RELIABILITY-001 Change 001`
- Fable: `NOT_APPLICABLE` — 기존 readiness·stale-response 계약에서 확인된 P2 잠금 누락을 보정한다.
- Backend·API·DB·migration·권한·workflow·Persistent UAT·실제 provider·대표 repo·`main`: 변경 없음
- commit·push·PR·merge: 미실행, main merge 승인 `0/3`

## 해결한 문제와 구현

- 기존 request-id guard는 stale 응답을 무시했지만, 최신 초기 load 완료 전에 상단 `행 추가·저장·Excel` action이 활성화되어 있었다.
- project와 procurement가 모두 `ready`인 경우에만 네 action을 활성화했다.
- 초기 load 중에는 `프로젝트·구매정보 확인 중에는 입력할 수 없습니다.`를 `role=status`로 표시한다.
- stale 첫 응답 후에도 action이 잠기고, 최신 응답 완료 후에만 table과 action이 열리는 deterministic regression을 고정했다.
- 전체 구매→자재 회귀 검증에서 현재 제품 문구와 다른 기존 E2E selector `+ 도착 등록`을 `도착분 추가`로 동기화했다. 제품 동작 변경은 없다.

## 변경 파일

- `frontend/src/App.tsx`
- `frontend/tests/App.test.tsx`
- `frontend/e2e/full-stack/procurement-initial-readiness.full-stack.spec.ts`
- `frontend/e2e/full-stack/project-registration.full-stack.spec.ts`
- `tasks/e2e-reliability-001-change-001.md`
- `docs/00-product-roadmap.md`

## 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 15 files, 112 tests |
| deterministic stale/latest load regression | `PASS` |
| 격리 focused Full-Stack E2E | `PASS` — 1 scenario |
| 기존 구매→자재·권한·모바일 Full-Stack 회귀 | `PASS` — 1 scenario |
| 잠금 중 action | `PASS` — 행 추가·양식·업로드·저장 disabled |
| 최신 load 후 입력 | `PASS` — 행 1개 추가 |
| 격리 DB·container·network cleanup | `PASS` |

Screenshot은 합성 데이터만 사용해 `/tmp/emi-qms-p2-remediation-evidence/` 안에 생성했다.

## Finding gate

| ID | Severity | 상태 | 해소 |
| --- | --- | --- | --- |
| `PROCUREMENT-INITIAL-LOAD-ACTION-UNLOCKED` | P2 | `RESOLVED` | readiness lock, 안내, stale/latest deterministic regression과 isolated screenshot E2E |

이 Change의 Open P0/P1/P2는 `0/0/0`이다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 초기 잠금→안내→ready 후 행 추가 순서를 본 문서에 고정 |
| User manual | 완료 | 안내 표시 중에는 대기하고, 버튼이 활성화된 후 입력한다. |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` Change 001 |
| User validation checklist | `BATCHED_FINAL` | 자동 검증 완료, 최종 일괄 사용자 검수 대기 |

## Rollback

readiness lock·regression·E2E 변경만 되돌린다. DB·migration·runtime·`main` rollback은 필요 없다.
