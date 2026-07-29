# TASK-HOME-002 Change 003 구현 보고서 — 자재 Home 사급 제공 지연 KPI

## 상태와 경계

- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- Task 유형: `P2_REMEDIATION`
- 기준 branch/commit: `experiment/task-home-002-personalized-shell` / `6cce87759d6c609c925ff7820c9d18825f8f9f1d`
- Fable: `NOT_APPLICABLE` — 신규 사용자 능력이 아니라 확정된 사급 제공 지연 표시 계약을 Home에 보정한다.
- 대표 repo·`main`·Persistent UAT·실제 provider·DB migration: 변경 없음
- commit·push·PR·merge: 미실행, main merge 승인 `0/3`

## 구현 결과

- 자재 Home의 첫 번째 핵심 지표를 `사급 제공 지연`으로 교체했다.
- 집계 규칙은 `CustomerSupplied`, 제공 예정일 경과, 취소 제외 누적 도착량이 예정 수량보다 적은 active 품목으로 고정했다.
- 지표를 누르면 `/materials/receipts?risk=customer-supply-overdue`로 이동하고 `제공 지연` 필터가 즉시 적용된다.
- IQC·키팅 KPI는 유지했고, 도착 등록 대기 수치는 기존 자재 화면 summary에서 계속 보인다.
- 새 상태·알림·worker·권한·외부 연동은 추가하지 않았다.

## 변경 파일

- `backend/src/Emi.Qms.Api/Home/HomeMetricsStore.cs`
- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`
- `frontend/src/App.tsx`
- `frontend/src/MaterialsWorkspace.tsx`
- `frontend/tests/App.test.tsx`
- `frontend/e2e/full-stack/customer-supplied-materials.full-stack.spec.ts`
- `tasks/home-002-change-003.md`

## 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | `PASS` |
| Frontend unit | `PASS` — 15 files, 112 tests |
| 격리 Full-Stack E2E | `PASS` — 1 scenario, fresh PostgreSQL schema |
| Home 집계 | `PASS` — 사급 지연 품목 1건 |
| deep-link·필터 | `PASS` — URL·active filter·대상 card 확인 |
| 전량 도착 후 지연 해제 | `PASS` |
| 격리 DB·container·network cleanup | `PASS` |

Screenshot은 합성 데이터만 사용해 `/tmp/emi-qms-p2-remediation-evidence/` 안에 생성했고 Repository에 추적하지 않았다.

## Finding gate

| ID | Severity | 상태 | 해소 |
| --- | --- | --- | --- |
| `MATERIAL-HOME-KPI-OMITS-CUSTOMER-SUPPLY-RISK` | P2 | `RESOLVED` | 자재 Home 지연 집계, 화면 이동, 전용 필터와 fresh-schema E2E 증빙 |

이 Change의 Open P0/P1/P2는 `0/0/0`이다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 본 문서 |
| SOP | 완료 | 본 문서의 구현 규칙·검증 절차 |
| User manual | 완료 | Home의 `사급 제공 지연` 지표를 누르면 지연 잔량 목록이 열린다. |
| Roadmap update | `N/A` | 기존 Task의 P2 Change로 실행 큐·Next Gate 불변 |
| User validation checklist | `BATCHED_FINAL` | 실험 branch 정책에 따라 마지막 일괄 검수 대기 |

## Rollback

이 Change의 source·test·artifact diff만 되돌린다. DB·migration·외부 provider·`main`은 변경되지 않아 data rollback은 필요 없다.
