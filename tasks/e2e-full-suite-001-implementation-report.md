# TASK-E2E-FULL-SUITE-001 구현 보고서

## 1. 상태와 안전 경계

- Task 유형: `BUGFIX`; Fable 적용 없음
- Branch: `experiment/task-export-001-all-pages-selected-export`
- 시작 HEAD: `4013c1efd446fc8c19f0c3895f8e7c8d0e7b50c8`
- 기준 `main`·`origin/main`: `b8f3e2104074d05c2e71999c08a7374e8729f68f`, 변경 없음
- Task Identity Gate: `PASS_CREATE`; 사용자의 실험 branch 우선 정비 지시로 `explicitRoadmapOverrideApproved=true`
- 범위: 현재 experiment HEAD의 Full-Stack E2E 기준선 복구와 중복 전체선택 UI 제거
- 제외: Backend 제품 계약, API, DB, migration, dependency, Persistent UAT, 실제 provider, 대표 repo, push·PR·merge
- main merge 승인: `0/3`

## 2. 해결한 업무 문제

이전 선택 프로젝트 export Task에서 전체 Full-Stack suite가 현재 제품 계약과 맞지 않아 `24/34`와 후속 현재 HEAD `25/35` 상태로 남았다. Home·Pending·IQC·모바일·키팅·프로젝트·export 시나리오가 각각 따로 실패해 이후 기능 변경의 실제 회귀 여부를 신뢰하기 어려웠다.

현재 HEAD에서 35개를 다시 실행해 10개 실패를 재현하고 다음처럼 분류했다.

- 최신 Pending·구매 계약과 달라진 합성 fixture
- 통합된 `품질 → IQC`, `자재 → 키팅` 정보구조와 맞지 않는 이전 navigation selector
- 디지털 IQC 성적서 도입 뒤 남은 legacy 간편 판정 절차
- 중복 label과 suite 누적 감사 이벤트에 의존한 strict assertion
- 프로젝트 목록에 공통 선택 tray와 desktop header 전체선택이 동시에 있던 실제 UI 중복

## 3. 구현 결과와 기술적 결정

### 제품 UI

- 프로젝트 목록의 desktop header 전체선택 checkbox를 제거했다.
- row checkbox column 정렬은 빈 header cell로 유지한다.
- 공통 선택 tray의 `전체선택` checkbox만 desktop·mobile의 단일 source로 사용한다.
- 기존 행 열기와 개별 선택, 선택 해제, 선택 export 동작은 보존한다.

### E2E 기준선

- 제조 중단 Pending 생성은 `actionDepartmentCode`와 올바른 담당자 필드를 사용한다.
- 일반 구매 키팅 fixture는 도착 등록 전 구매 수량·단위를 선입력하지 않는다.
- IQC는 공통 메뉴 `품질`에서 검사 단계 `IQC`로 들어가며, checklist·사진·최종확인을 거치는 현재 디지털 성적서 계약을 수행한다.
- 내 업무 선택 export는 담당 Pending을 먼저 생성해 0건 내보내기에 의존하지 않는다.
- export audit는 전체 table 고정 count가 아니라 해당 test 전후 증가분을 검증한다.
- 선택 프로젝트 전체선택은 `선택 프로젝트 내보내기` region으로 scope하고 전역 동일 checkbox 수가 1개임을 확인한다.

Backend 제품 source·API·DB·migration은 변경하지 않았다.

## 4. 실제 변경 파일

### 제품 source

- `frontend/src/App.tsx`

### Frontend unit·Full-Stack E2E

- `frontend/tests/App.test.tsx`
- `frontend/e2e/full-stack/excel-export.full-stack.spec.ts`
- `frontend/e2e/full-stack/home-dashboard.full-stack.spec.ts`
- `frontend/e2e/full-stack/iqc-digital-report.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-adaptive-navigation.full-stack.spec.ts`
- `frontend/e2e/full-stack/mobile-first-experience.full-stack.spec.ts`
- `frontend/e2e/full-stack/panel-kitting.full-stack.spec.ts`
- `frontend/e2e/full-stack/pending-list.full-stack.spec.ts`
- `frontend/e2e/full-stack/project-bottleneck.full-stack.spec.ts`
- `frontend/e2e/full-stack/project-registration.full-stack.spec.ts`
- `frontend/e2e/full-stack/selected-project-export.full-stack.spec.ts`

### Task·Roadmap·증빙

- `tasks/e2e-full-suite-001.md`
- 본 구현 보고서
- `tasks/export-002-implementation-report.md`
- `docs/00-product-roadmap.md`
- Full-Stack E2E가 재생성한 기존 synthetic screenshot·선택 workbook 증빙

## 5. 검증 결과

| 검증 | 결과 |
| --- | --- |
| 최초 현재-HEAD Full-Stack 재현 | `25/35`, 10개 실패 확인 |
| 수정 대상 집중 Full-Stack | 실패 원인을 단계별 재검증한 뒤 최종 전체 suite에서 모두 통과 |
| 최종 전체 Full-Stack E2E | `35/35 PASS`, 1 worker, disposable PostgreSQL |
| E2E cleanup | PASS — DB drop, container·network 제거 |
| Backend Release build | PASS — warning 0, error 0 |
| Backend 전체 test | `388/388 PASS`, skipped 0 |
| Frontend lint | PASS — error 0, 기존 Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend unit | `92/92 PASS` |
| Frontend production build | PASS — 기존 chunk-size warning 유지 |
| `git diff --check` | PASS |

Persistent UAT·실제 provider·CI는 승인 범위 밖이라 실행하지 않았다. 실제 Runtime URL도 새로 기동하거나 확인하지 않았다.

## 6. 시행착오 및 폐기한 접근

- 첫 집중 실행은 내 업무가 0건이라 선택 tray가 없었다. 빈 전체 export를 되살리지 않고 담당 Pending을 생성해 실제 선택 흐름을 검증했다.
- IQC checklist는 검사 시작 API 완료 전 카드 수를 읽어 0개로 판단했다. 임의 sleep 대신 `검사항목` 단계가 나타나는 계약을 기다렸다.
- audit 총건수 고정 assertion은 앞 test의 정상 이벤트를 실패로 오판했다. test 시작 전 count 대비 정확한 증가분으로 교체했다.
- 기존 Task screenshot을 검증 중 재생성하지 않는 방향도 검토했으나 suite 자체가 canonical capture를 수행하므로 synthetic evidence를 현재 결과로 갱신했다.

## 7. Finding

| ID | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `FULL-STACK-BASELINE-UNRELATED-FAILURES` | P3 | `RESOLVED` | 10개 시나리오가 최신 계약·정보구조와 불일치해 전체 회귀 신뢰도 저하 | 현재 계약 fixture·selector·IQC 절차 보정, 전체 `35/35` |
| `PROJECT-SELECTION-DUPLICATE-SELECT-ALL` | P2 | `RESOLVED` | 공통 tray와 desktop header에 같은 전체선택이 두 번 노출 | header action 제거, tray 한 개와 unit/E2E 단일성 검증 |
| `E2E-AUDIT-ORDER-COUPLING` | P3 | `RESOLVED` | audit 고정 count가 test 순서에 의존 | 시작 count 대비 증가분 검증 |
| `E2E-IQC-ASYNC-START-RACE` | P3 | `RESOLVED` | report 초기화 전 checklist count 조회 | `검사항목` heading readiness 대기 |

Open P0/P1/P2/P3는 `0/0/0/0`이다.

## 8. 개인정보·secret·Rollback

- E2E와 screenshot·workbook은 synthetic data만 사용했다.
- 실제 사용자·고객·프로젝트·tenant/client/object ID, credential, provider payload를 tracked 증빙에 기록하지 않았다.
- Rollback은 `App.tsx`의 header action 변경과 이 Task의 unit/E2E 기준선 변경을 함께 revert한다. DB·migration·runtime rollback은 없다.

## 9. 종료 산출물 5종

| 산출물 | 상태 | canonical 위치 |
| --- | --- | --- |
| Implementation report | 작성 완료 | 본 문서 |
| SOP | 작성 완료 | 본 문서 `## 10. SOP` |
| User manual | 작성 완료 | 본 문서 `## 11. User manual` |
| Roadmap update | 작성 완료 | `docs/00-product-roadmap.md`의 Task·추적 90 |
| User validation checklist | 자동 검증 완료·사용자 검수 대기 | `tasks/e2e-full-suite-001.md` |

## 10. SOP

1. Repository root에서 `bash scripts/e2e-full-stack.sh`를 실행한다.
2. runner가 `emi_qms_e2e_*` DB, 전용 container·network와 tmpfs를 사용한다는 시작 문구를 확인한다.
3. 35개 시나리오가 모두 통과하는지 확인한다.
4. 종료 시 DB drop, container·network 제거가 출력되는지 확인한다.
5. export test를 다른 spec과 함께 실행할 때 audit는 test 전후 증가분으로 검증한다.
6. 제품 계약이 바뀌면 fixture만 임의 완화하지 말고 API·UI source와 통과 중인 최신 scenario를 먼저 대조한다.

Persistent UAT DB나 실행 중 runtime을 Full-Stack E2E에 재사용하지 않는다.

## 11. User manual

- 프로젝트 목록의 `전체선택`은 선택 내보내기 영역에 한 번만 표시된다.
- 개별 프로젝트는 각 행·카드 checkbox로 고른다.
- 현재 목록을 모두 고르려면 목록 위 선택 영역의 `전체선택`을 사용한다.
- `선택 Excel 내보내기`는 선택된 항목만 포함하며, 선택하지 않으면 실행할 수 없다.
- IQC는 공통 메뉴 `품질`을 누른 뒤 품질 단계의 `IQC`로 이동한다.
- 키팅은 별도 공통 메뉴가 아니라 `자재` 안에서 사용한다.

## 12. 사용자 검수 결과와 남은 항목

자동 검증은 완료됐다. 사용자의 이번 “전체 완료” 지시로 구현·자동 검증·local commit 범위는 승인됐지만, 화면 직접 확인은 아직 별도 완료로 간주하지 않고 사용자 검수 대기 상태로 둔다. push·PR·merge, 대표 repo·main·Persistent UAT 반영은 승인되지 않았다.
