# TASK-E2E-FULL-SUITE-001 Change 011 구현 보고서

## 1. 결과와 범위

- Task 유형: `BUGFIX`
- 변경 계약: [Change 011](e2e-full-suite-001-change-011.md)
- 상태: 구현·로컬 자동 검증 완료, PR 최신 head CI 대기
- 제품 영향: 없음. Full-Stack E2E의 route와 readiness assertion만 변경했다.
- 제외: Backend·API·DB·migration·dependency·runtime·Persistent UAT·Azure 운영 release·실제 provider.

PR #75의 Full-Stack E2E가 suite 누적 프로젝트 수에 따라 같은 위치에서 반복 실패하던 결함을 해소했다. 테스트는 자신이 생성한 프로젝트 ID로 Pending 화면을 열고 그 프로젝트 제목을 확인하므로 전역 100개 목록의 정렬·페이지 크기에 의존하지 않는다.

## 2. 기술적 결정과 대안

- 채택: 이미 제품과 다른 E2E가 사용하는 프로젝트별 route `/pending?projectId=<id>`를 사용한다.
- 제거: 전역 dashboard의 목록 한도를 늘리거나 새 프로젝트가 첫 페이지에 오도록 정렬을 바꾸는 제품 수정. 이번 검증 목적과 무관하고 실제 사용자 계약을 바꿀 수 있다.
- 제거: timeout 증가 또는 재시도. 누락된 항목은 기다려도 나타나지 않으므로 원인을 숨긴다.
- 보존: screenshot, 이후 자재 도착·IQC 자동 인계 검증과 disposable E2E 격리.

## 3. 변경 파일

| 파일 | 역할 |
| --- | --- |
| `frontend/e2e/full-stack/workflow-continuity-change-003.full-stack.spec.ts` | Pending 프로젝트 hub 확인을 생성 프로젝트 scope로 고정 |
| `tasks/e2e-full-suite-001-change-011.md` | 승인 범위·원인·불변조건 기록 |
| 본 문서 | 구현·검증·Finding·산출물 기록 |
| `docs/00-product-roadmap.md` | canonical Task·추적·결정 이력 동기화 |

## 4. 검증 결과

| 검증 | 적용 | 결과 |
| --- | --- | --- |
| `git diff --check` | 필수 | `PASS` |
| Frontend lint | 영향 회귀 | `PASS`, error `0`, 기존 warning `1` |
| Frontend typecheck | 영향 회귀 | `PASS` |
| Frontend unit | 영향 회귀 | `175/175 PASS` |
| Frontend build | 영향 회귀 | `PASS`, 기존 large chunk warning 유지 |
| 변경 spec targeted 반복 | 직접 회귀 | 격리 실행 `3/3 PASS`, 실행별 DB·container·network cleanup `PASS` |
| PR #75 Frontend·Backend·Full-Stack CI | 게시 Gate | 진행 중 |
| Persistent UAT·실제 provider | 제외 | `N/A` — test-only 수정이고 승인 범위 밖 |

## 5. Finding gate

| ID | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `E2E-PENDING-GLOBAL-PAGINATION-COUPLING` | P2 | `LOCAL_RESOLVED_CI_PENDING` | 전역 100개 프로젝트 목록에서 새 프로젝트가 제외돼 전체 CI가 결정적으로 실패 | 프로젝트별 canonical route와 제목 readiness로 변경, targeted 격리 실행 `3/3`; PR 전체 CI 대기 |

Open P0/P1은 `0/0`이다. P2는 PR 최신 head의 전체 CI가 통과할 때 `RESOLVED`로 닫는다.

## 6. 개인정보·secret·artifact

- 실제 사용자·고객·프로젝트·계정·identifier·credential은 문서와 출력에 기록하지 않았다.
- 검증은 synthetic fixture와 fixed count/status projection만 사용한다.
- browser screenshot·trace·test-results는 tracked/staged 대상에서 제외한다.

## 7. Rollback과 SOP

- Rollback은 해당 spec의 프로젝트별 route와 heading assertion을 이전 두 줄로 되돌리는 test-only revert다.
- DB·migration·runtime rollback은 없다.
- 전체 suite에서 같은 실패가 재발하면 timeout을 늘리지 않고 프로젝트별 route 적용 여부와 생성 ID 전달을 먼저 확인한다.

## 8. 사용자 검수 체크리스트

- [x] 제품 화면·기능·권한·DB가 변경되지 않는 범위를 확인했다.
- [x] 변경 spec targeted 반복 실행이 통과했다.
- [x] Frontend 기본 검증이 통과했다.
- [ ] PR #75 최신 head의 CI 3종이 모두 통과했다.
- [ ] PR #75가 원격 `main`에 병합됐다.

사용자 직접 화면 검수는 `적용 대상 아님`이다. 제품 UI가 바뀌지 않는 자동검증 전용 수정이다.

## 9. 종료 산출물 5종

| 산출물 | 상태 | canonical 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | 본 문서 `## 7. Rollback과 SOP` |
| User manual | `N/A` | 제품 사용법 변경 없음 |
| Roadmap update | 작성됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 작성됨·로컬 자동 검증 완료·CI 대기 | 본 문서 `## 8. 사용자 검수 체크리스트` |

## 10. 해결한 업무 문제와 남은 항목

전체 회귀가 제품 결함이 아닌 전역 목록 페이지 제한으로 실패해 Azure Change 018의 게시가 막혔다. 프로젝트별 route로 검증 대상을 정확히 고정했다. 남은 항목은 자동 검증, PR CI와 승인된 `main` merge이며 실제 Azure 운영 release는 계속 별도 명시 실행이다.
