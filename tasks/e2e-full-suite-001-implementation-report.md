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

## 13. Change 001 — 역할별 18단계 연속 사용자 검수 (2026-07-19)

### 실행 경계와 결과

- Branch: `experiment/task-home-002-personalized-shell`
- 시작 HEAD: `9250384b330a9f524bd964d01da8f00ed661ab75`
- Task 유형: `UAT_RUNTIME`; 기존 `TASK-E2E-FULL-SUITE-001` 재사용
- 제품 source 변경: 없음
- Mutation: 담당자별 실제 UI 입력만 사용, 실행별 disposable PostgreSQL 한정
- Provider: Teams·Mail·Teams Activity·dispatch·digest·escalation 비활성
- Persistent UAT·대표 repo·`main`·push·PR·merge: 미사용·미변경
- 실행: `bash scripts/e2e-full-stack.sh e2e/full-stack/project-lifecycle-user-validation.full-stack.spec.ts`
- 결과: `1/1 PASS`, 본 시나리오 `42.7s`, 전체 runner `46.9s`, DB drop·container/network 제거 PASS

영업 등록, 생산관리 계획·정담당자 지정, 설계, 구매, 자재 도착, IQC checklist·사진·판정, 입고 확정·마감, 키팅, 제조 시작·4단계 확인, 긴급 Pending 생성, 생산관리 담당 지정·상태 전이·종결, 제조 재개, LQC, 제조 완료 확인, OQC, 전진검수, FAT, 포장, 출발, 납품, 세금계산서 임시 저장과 프로젝트 최종 완료를 모두 담당자 화면에서 수행했다.

최종 합성 프로젝트는 `Completed`, open Pending `0`, 세금계산서 입력 완료 상태였다. screenshot 70장과 UI 검출 결과 JSON 1개를 `tasks/e2e-full-suite-001-lifecycle-screenshots/`에 기록했다.

### 알림·내 업무 검수 결과

| 검수 | 결과 | 판단 |
| --- | --- | --- |
| 프로젝트 생성 알림 | 활성 검수 사용자 10명 중 7명 표시 | `FAIL_EXPECTATION` — 관리자·영업·조회전용 미표시 |
| 다음 담당자 인앱 알림 | 인계 checkpoint 20곳 중 19곳 표시 | `PARTIAL` — 납품 완료 → 영업 정산 알림 미표시 |
| 다음 담당자 내 업무 | 인계 checkpoint 20곳 중 16곳 표시 | `PARTIAL` — 최초 생산계획, IQC, Pending 생산관리, Pending 복귀 제조 미표시 |
| Pending 인앱 알림 | 생산관리 지정과 제조 복귀 모두 표시 | `PASS` |
| 최종 영업 내 업무 | 정산 업무 표시 | `PASS` |

프로젝트 생성 알림은 설계·생산관리·구매·자재·제조·품질·물류에 도착했다. 모든 사용자에게 도착한다는 사용자 기대와 달리 `dev-admin`, `dev-sales`, `dev-viewer`에는 표시되지 않았다.

### Finding

| ID | Severity | 상태 | 실제 결과와 영향 | 권장 후속 |
| --- | --- | --- | --- | --- |
| `LIFECYCLE-WORKFLOW-COMPLETION-DIVERGENCE` | P1 | `OPEN` | 프로젝트가 완료·100%인데 Workflow는 14/18·78%이며 생산관리, 자재 도착, IQC, 입고 확정이 미완료로 남는다. 현재 단계도 생산관리로 표시된다. 사용자가 완료 이력을 신뢰할 수 없다. | 네 단계의 성공 mutation과 `StageCompleted` event 연결, 완료 전 일관성 gate와 회귀 E2E 추가 |
| `PROJECT-CREATED-NOTIFICATION-NOT-ALL-USERS` | P2 | `OPEN` | 생성 알림이 10명 중 7명에게만 표시된다. 관리자·생성 영업·조회전용은 프로젝트 생성 사실을 알림에서 볼 수 없다. | “모든 사용자” 수신 정책을 active user 기준으로 확정하고 recipient query 보정 |
| `SALES-SETTLEMENT-HANDOFF-NOTIFICATION-MISSING` | P2 | `OPEN` | 납품 완료 후 영업 내 업무는 생성되지만 인앱 알림은 없다. 영업이 알림만 보면 최종 정산을 놓칠 수 있다. | Delivery 완료 event의 Sales recipient notification 추가 |
| `MY-WORK-HANDOFF-COVERAGE-GAPS` | P2 | `OPEN` | 최초 생산계획, IQC, Pending 조치·복귀 업무가 다음 담당자 `내 업무`에 표시되지 않는다. 알림·Pending·내 업무를 오가야 한다. | 업무 생성 source와 My Work query를 통일하고 Pending projection 정책 추가 |
| `QUALITY-DECISION-ACCESSIBLE-NAME-OVERLAP` | P3 | `OPEN` | 판정 modal에서 선택 버튼과 제출 버튼이 모두 “합격”을 포함해 보조기기·자동화에서 구분이 어렵다. | 선택은 “합격 선택”, 제출은 “합격 확정 및 인계”처럼 accessible name 분리 |
| `FULL-STACK-BASELINE-DRIFT-CURRENT-HEAD` | P2 | `OPEN` | 전체 42개 중 4개 기존 E2E가 현재 UI·공유 fixture와 불일치한다. 홈은 삭제된 `Pending` heading, 모바일 2건은 삭제된 `새로고침` 버튼을 기대하고, 제조는 2개 프로젝트를 전역 locator 하나로 조회한다. 기능 화면은 정상 렌더링됐지만 전체 회귀 신호가 `38/42`로 저하됐다. | 현재 디자인 계약으로 홈·모바일 locator 갱신, 제조 test를 합성 프로젝트로 scope한 뒤 전체 suite 재실행 |

### 실제 담당자 관점 UX 평가

좋았던 점:

- 역할별 입력 권한과 조회 전용 범위가 분명하고, 담당자는 자기 업무 화면에서 실제 입력만으로 최종 완료까지 갈 수 있었다.
- 제조 중단은 사유·조치 부서·담당 지정·상태 변경 이력이 강제돼 현장 문제를 말로만 넘기지 않게 한다.
- IQC와 품질 단계는 checklist, 측정 메모, 사진, 판정 사유를 모두 요구해 감사 증빙 품질이 좋다.
- 최종 정산은 납품 1/1, open Pending 0, 세금계산서 발행일을 한 화면에서 gate로 보여 주어 실수 방지가 좋다.

불편했던 점:

- 가장 심각한 문제는 완료 상태가 서로 모순된다는 것이다. 상단은 완료·100%인데 같은 화면의 Workflow는 14/18·78%라 담당자가 어느 숫자를 믿어야 할지 알 수 없다.
- 알림과 내 업무의 coverage가 다르다. “알림이 왔는데 내 업무가 없음” 또는 “내 업무는 있는데 알림이 없음”이 실제로 발생해 한 화면만 믿고 일할 수 없다.
- 자재 담당자는 도착, IQC 요청, 입고 확정, 입고 마감, 키팅 사이에서 목록을 반복 검색하고 drawer를 다시 열어야 한다. 마감 직후 카드가 목록에서 즉시 사라져 완료 확인 위치도 끊긴다.
- 품질 담당자는 LQC·OQC·전진검수·FAT에서 유사한 checklist·사진·판정 입력을 반복한다. 증빙 강도는 좋지만 공통 정보 재사용이나 다음 단계 진행 표시가 없어 입력 피로가 크다.
- Pending은 별도 화면에서는 잘 작동하지만 `내 업무`와 연결되지 않아 생산관리·제조 담당자가 Pending 메뉴를 따로 확인해야 한다.

제품 수정은 이번 검수 승인 범위가 아니므로 위 Finding은 재현·증빙만 기록했고 구현하지 않았다.

### 전체 Full-Stack 회귀

역할별 연속 시나리오 통과 후 같은 격리 정책으로 `bash scripts/e2e-full-stack.sh` 전체를 실행했다.

- 총 `42`개 중 `38 PASS / 4 FAIL`, `4.2m`
- 새 역할별 18단계 연속 시나리오는 전체 suite 안에서도 `45.4s PASS`
- 실패 4건: `home-dashboard`, `manufacturing-work`, `mobile-adaptive-navigation`, `mobile-first-experience`
- failure screenshot 대조 결과, 네 건 모두 현재 화면 기능 실패가 아니라 기존 locator·fixture drift로 분류
- 제조 실패를 단독 disposable DB로 재실행한 결과 `1/1 PASS (3.1s)`여서 제품 결함이 아니라 전체 suite 공유 fixture에 scope하지 않은 테스트 결함임을 확인
- cleanup: DB drop, container·network 제거 PASS

## 14. Change 002 — 알림·내 업무·18단계 완료 정합화와 재검수 (2026-07-19)

### 구현 결과

- 프로젝트 생성 알림 수신자는 영업을 포함한 8개 운영 부서 활성 사용자이며 `system-administrator`, `read-only`는 제외한다.
- 생성 직후 생산관리 전체는 알림으로 인지하되 생산관리 내 업무는 만들지 않는다. 생산계획에서 정·부 담당자를 저장한 뒤 생산관리 정담당자의 첫 내 업무를 생성한다.
- 이후 단계는 정담당자에게 내 업무와 연결 알림, 부담당자에게 참조 알림을 같은 event·고정 idempotency key로 생성한다.
- 납품 완료 뒤 영업 정·부 담당자 알림과 영업 정담당자 내 업무를 생성한다.
- Pending은 프로젝트 조치 부서의 정담당자를 자동 지정하고 정담당자 내 업무, 정·부 담당자 인앱 알림을 함께 생성한다. `PendingAssignment` source는 긴급도와 무관하게 Teams 채널·담당자 메일 전달 후보가 된다.
- 자재 도착→IQC→입고 확정→품목 마감은 한 화면의 연속 단계로 표시하고 각 완료 근거를 18단계 workflow event와 맞췄다.
- 프로젝트 상세에 영업·생산관리·설계·구매·자재·제조·품질·물류 탭을 모두 제공한다.
- 세금계산서 최종 단계는 직접 발행이 아니라 회계팀 발행요청 자료 생성과 발행 확인 입력으로 문구·상태를 정리했다. 신규 선택 Excel 기능의 상세 구현은 `TASK-BILLING-REQUEST-001` 보고서가 source다.

### 역할별 실제 입력 검수

`frontend/e2e/full-stack/project-lifecycle-user-validation.full-stack.spec.ts`가 영업 프로젝트 생성부터 생산관리 계획·정/부 담당자, 설계, 구매, 자재, IQC, 키팅, 제조 Pending 생성·종결·재개, LQC/OQC/전진검수/FAT, 포장·출발·납품, 영업 회계 발행요청 Excel과 최종 완료까지 실제 역할 화면에서 입력한다.

| 검증 | 결과 |
| --- | --- |
| 역할별 18단계 lifecycle | 최종 `1/1 PASS`, `48.9s`; runner `53.9s` |
| 최종 workflow | `18/18`, project `Completed`, open Pending `0` |
| 생성 알림 | 관리자·조회전용 제외 운영 사용자 전원 PASS |
| 생산관리 시작 gate | 지정 전 알림 있음·내 업무 없음, 지정 후 정담당자 내 업무 생성 PASS |
| 단계 인계 | 정담당자 내 업무·알림, 부담당자 참조 recipient 생성 PASS |
| 제조 Pending | 생산관리 담당자 알림·내 업무, TeamsChannel·Mail delivery 후보 2종 PASS |
| 납품→영업 | 영업 담당자 인앱 알림·내 업무 PASS |
| 회계 발행요청 | 출하 프로젝트 선택·Excel 다운로드·정산 화면 요청 상태·최종 완료 PASS |
| Backend Release build | warning 0, error 0 |
| Backend 전체 test | `403/403 PASS`, skipped 0 |
| Frontend unit | `109/109 PASS` |
| Frontend typecheck·production build | PASS; 기존 chunk-size warning 유지 |
| `git diff --check` | PASS |

E2E에서 외부 provider는 계속 비활성·Dry-run이다. 전달 worker만 5초 주기로 켜 `PendingAssignment`의 TeamsChannel·Mail outbox 생성과 dedupe를 검증했으며 실제 Teams·메일은 발송하지 않았다. disposable DB, container와 network는 종료 시 제거됐다.

### 증빙과 Excel 검증

- 단계별 프로젝트 상세 20장, 생성 수신자 10장, 인계 알림·내 업무 화면은 Repository가 아닌 `/tmp/emi-qms-lifecycle-evidence`에만 생성했다.
- 발행요청 workbook은 `발행요청` 단일 시트 `A1:O6`, filter `A5:O6`, 5행 고정, 공급가액 숫자값 `125000000`, 회계팀 기입란 2개로 확인했다.
- ZIP/XML integrity와 SHA-256을 확인했고 Excel에서 한 화면 캡처 후 통합문서와 Excel process를 모두 종료했다.

### Finding resolution과 UX 평가

| ID | 상태 | 해소 |
| --- | --- | --- |
| `LIFECYCLE-WORKFLOW-COMPLETION-DIVERGENCE` | `RESOLVED` | 자재 3단계 event와 생산계획 readiness를 연결해 최종 `18/18` |
| `PROJECT-CREATED-NOTIFICATION-NOT-ALL-USERS` | `RESOLVED_BY_POLICY` | 사용자 확정대로 관리자·조회전용 제외 운영 사용자 전원 |
| `SALES-SETTLEMENT-HANDOFF-NOTIFICATION-MISSING` | `RESOLVED` | 납품 완료 영업 정·부 알림과 정담당자 내 업무 |
| `MY-WORK-HANDOFF-COVERAGE-GAPS` | `RESOLVED` | 연결 work item·notification과 Pending projection 통일 |
| `FULL-STACK-BASELINE-DRIFT-CURRENT-HEAD` | `SUPERSEDED_BY_CURRENT_BASELINE` | Backend·Frontend 전체 회귀와 현재 lifecycle 재통과 |

실제 입력 UX는 이전보다 업무 인계 신뢰성이 높고 자재 화면도 끊김이 줄었다. 다만 생산계획 편집 화면은 초기 API 응답이 안정되기 전에 매우 빠르게 입력하면 늦은 초기 응답이 값을 덮을 수 있어 E2E는 `networkidle` 뒤 입력한다. 일반 수기 입력에서는 재현 가능성이 낮지만 입력 disable/loading 경계 보강은 P3 후속 후보다. 품질 단계별 반복은 단계 담당자와 검사 항목이 다르다는 사용자 결정에 따라 유지했다.

Open P0/P1/P2는 `0/0/0`이다. 사용자 직접 검수는 `BATCHED_FINAL`로 마지막 일괄 검수 대기이며 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider는 미반영이다.

## 15. Change 003 — 부서 실데이터·알림 누적·생산계획 입력 잠금 (2026-07-20)

### 구현 결과

- 생산관리·설계·구매의 직접 데이터 구성을 기준으로 영업·자재·제조·품질·물류 탭도 프로젝트별 실제 정산, 입고·키팅, 제조 패널, 단계별 검사, 포장·출발·납품 지표와 행을 표시한다.
- 모든 부서는 조회할 수 있고, 담당 권한과 API `canMutate`가 모두 충족될 때만 해당 프로젝트가 유지된 업무 화면을 수정 진입점으로 표시한다. 자재는 프로젝트 Code 검색과 완료 포함 상태를 자동 적용한다.
- 완료 프로젝트에서 물류 작업 queue가 비어도 프로젝트 완료 계약, 패널 수, 포장방식과 납품장소를 사용해 포장·출발·납품 실적을 0/0으로 오해하지 않게 표시한다.
- 알림은 최근 3건 우선 표시와 이전 알림 접기, 프로젝트별 모두 읽음, 상세·이동 시 해당 알림 자동 읽음을 제공한다. 알림 원문·audit 이력은 삭제하지 않는다.
- 생산계획 수정은 프로젝트·계획·담당자 초기 응답이 모두 준비될 때까지 fieldset과 저장·Excel 작업을 잠그고, request generation으로 늦게 도착한 과거 응답을 폐기한다.

### 검증

| 검증 | 결과 |
| --- | --- |
| Frontend lint | PASS — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend unit | `109/109 PASS` |
| Frontend production build | PASS — 기존 chunk-size warning 유지 |
| Backend Release build | warning 0, error 0 |
| 프로젝트별 알림 read-all 통합 test | `1/1 PASS`; 다른 프로젝트 unread 보존 |
| 실제 역할 lifecycle | isolated PostgreSQL·provider disabled에서 두 차례 `1/1 PASS`; 최종 18/18, open Pending 0 |
| 부서 탭 visual | desktop 8개, mobile 5개 직접 데이터 탭, 390px horizontal overflow 0 |
| `git diff --check` | PASS |

### Finding

| ID | Severity | 상태 | 해소 |
| --- | --- | --- | --- |
| `PROJECT-DEPARTMENT-WORKFLOW-ONLY` | P2 | `RESOLVED` | 5개 부서 탭을 부서 API 실데이터 projection으로 교체 |
| `NOTIFICATION-ACCUMULATION` | P2 | `RESOLVED` | 이력 보존형 접기·프로젝트 단위 정리·열람 자동 읽음 |
| `PRODUCTION-INITIAL-RESPONSE-OVERWRITE` | P2 | `RESOLVED` | 초기 입력 잠금과 stale request generation guard |
| `LOGISTICS-COMPLETED-QUEUE-EMPTY` | P2 | `RESOLVED` | 완료 프로젝트 실적 fallback과 포장·납품 context 표시 |

Open P0/P1/P2는 `0/0/0`이다. 합성 screenshot은 `/tmp/emi-qms-change-003-evidence-final`에만 있고 tracked/staged하지 않았다. 사용자 직접 검수는 `BATCHED_FINAL`로 남으며 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 변경하지 않았다.

## 16. Change 004 — 프로젝트 전체 흐름·생산계획 정보 우선순위 (2026-07-20)

### 구현 결과

- 18단계 workflow를 프로젝트 공통 본문에서 제거하고 `전체 흐름` 탭 안에서만 표시한다.
- 전체 흐름은 진행률 bar, 현재·다음 단계, 6열×3행 단계 카드와 현재 단계 강조를 갖는 전용 보드로 정리했다.
- 생산관리 탭은 계획 KPI 다음에 계획 항목표와 캘린더를 먼저 표시하고 담당자 지정 현황을 그 아래에 배치한다.
- 일반 부서 담당자는 부서명·정담당자·부담당자를 한 행에 표시하고 품질은 네 검사 책임을 한 행 안의 하위 열로 압축했다.
- workflow 집계·상태 API, 담당 권한, Active 프로젝트 수정 gate는 변경하지 않았다.

### 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | PASS |
| Frontend unit | `109/109 PASS`; 전체 흐름 전용 렌더링과 일정→담당자 DOM 순서 회귀 조건 포함 |
| Frontend lint | PASS — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend production build | PASS — 기존 chunk-size warning 유지 |
| 실제 역할 lifecycle | isolated PostgreSQL·provider disabled에서 `1/1 PASS`, `48.4s`; 최종 18/18, open Pending 0 |
| 생산관리 계정 visual | `전체 흐름` 18개 카드와 `생산관리` 일정 우선·담당자 한 행 구성을 desktop에서 확인 |
| `git diff --check` | PASS |

### UX 평가와 Finding

전체 흐름은 부서 입력과 분리되어 프로젝트 진행 확인 목적이 명확해졌고, 18단계가 6열×3행으로 한 화면에 정돈돼 인계 순서를 빠르게 훑을 수 있다. 생산관리 탭도 사용 빈도가 높은 일정이 담당자 목록보다 먼저 보여 작업 판단 순서가 자연스럽다. 담당자 영역은 일반 부서 7행과 품질 1행으로 줄어 이전 카드 격자보다 세로 길이가 작고 정·부 비교가 쉽다.

다만 프로젝트 상세 상단의 `다음 확인 대상` 블록이 여전히 커서 1050px desktop 첫 화면에서는 생산관리의 계획표가 아래로 밀린다. 이번 Change는 사용자 확정 범위인 탭 내부 순서에 한정했다.

| ID | Severity | 상태 | 원인·영향 | 후속 |
| --- | --- | --- | --- | --- |
| `PROJECT-DETAIL-TOP-SUMMARY-DENSITY` | P3 | `BACKLOG` | 공통 요약과 `다음 확인 대상`의 높이 때문에 탭 핵심 본문이 첫 화면 아래로 밀림 | 상단 KPI·병목 요약의 접기 또는 compact variant를 별도 UX change에서 검토 |

신규 Open P0/P1/P2는 `0/0/0`이다.

합성 screenshot은 `/tmp/emi-qms-change-004-evidence`에만 있고 tracked/staged하지 않았다. 사용자 직접 검수는 `BATCHED_FINAL`로 남으며 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 변경하지 않았다.

## 17. Change 005 — 프로젝트 상세 전 부서 입력 데이터 완전 projection (2026-07-20)

### 구현 결과

- 영업 탭은 프로젝트 생성·수정 입력 13개와 납품 후 정산·회계 발행요청·발행 확인 입력 12개를 프로젝트 안에서 직접 표시한다.
- 자재 탭은 구매 품목 누계, 입고 회차별 수량·도착일·메모·확정·취소·버전, IQC 성적서·판정·Pending, 패널 키팅 완료 담당·시각을 표시한다.
- 제조 탭은 패널별 실행 상태와 checklist 전체 응답, 시작·완료·중단·재개 event와 연결 Pending을 표시한다.
- 품질 탭은 LQC·OQC·입회검사·FAT의 검사 차수, 성적서·PDF·최종 판정·사유, checklist 응답·메모·guidance, 사진 metadata와 검사 이력을 표시한다.
- 물류 탭은 새 프로젝트 history read API로 포장→출발→납품의 구성 패널·포장단위, 메모·규격·중량·출발일, 등록·확정·취소 담당·시각과 증빙 metadata를 표시한다.
- 생산관리·설계·구매의 기존 직접 데이터 화면은 유지했다. 수정은 기존 담당자·permission·프로젝트 상태 gate를 충족한 사용자만 전용 업무 화면에서 수행하고, 탭 본문은 모든 부서에 조회 전용으로 제공한다.

### 실제 검수 중 추가 보정

첫 lifecycle 실행은 최신 Debug build와 달리 E2E가 이전 Release `--no-build` binary를 사용해 새 물류 history route가 404가 됐다. 최신 Release build 후 같은 시나리오를 재실행해 통과했으며 제품 코드 결함이 아니었다.

완료 프로젝트에서 자재 receipt는 표시됐지만 기존 키팅 queue가 `Active` 프로젝트만 반환해 키팅 완료 기록이 빠지는 결함을 추가로 확인했다. 일반 queue는 Active-only를 유지하고 `projectId`가 명시된 프로젝트 상세 조회만 완료 프로젝트 이력을 포함하도록 보정했다. 물류 history는 화면에서 포장→출발→납품 업무 순서로 정렬했다.

### 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | PASS |
| Frontend unit | `110/110 PASS`; 다섯 직접 데이터 탭의 입력·이력 projection 회귀 포함 |
| Frontend lint | error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend production build | PASS; 기존 chunk-size warning 유지 |
| Backend Debug·Release build | warning 0, error 0 |
| Backend 직접 범위 | ProjectRegistration `74/74`, 완료 프로젝트 키팅 이력 `1/1` PASS |
| 실제 역할 lifecycle | 최종 `1/1 PASS`, `52.9s`; 최종 18/18·open Pending 0, 탭별 마지막 입력 이력 load gate 포함 |
| 부서 탭 visual | desktop 8개, mobile 5개; 실제 record·field 존재, 자재 키팅 포함, 390px horizontal overflow 0 |
| disposable runtime cleanup | DB drop, container·network 제거 PASS |
| `git diff --check` | PASS |

### Finding

| ID | Severity | 상태 | 해소 |
| --- | --- | --- | --- |
| `PROJECT-DEPARTMENT-DATA-PROJECTION-INCOMPLETE` | P2 | `RESOLVED` | 다섯 요약 탭을 저장 field·checklist·event·evidence record로 확장 |
| `COMPLETED-PROJECT-KITTING-HISTORY-HIDDEN` | P2 | `RESOLVED` | 특정 프로젝트 조회에서 완료 프로젝트 키팅 기록 포함 |
| `LOGISTICS-COMPLETED-INPUT-HISTORY-MISSING` | P2 | `RESOLVED` | 프로젝트 scope history endpoint와 read-only projection 추가 |

신규 Open P0/P1/P2는 `0/0/0`이다. 합성 screenshot과 workbook은 사용자 지시대로 Repository가 아닌 `/tmp/emi-qms-lifecycle-evidence`에서 채팅 증빙으로만 사용했다. 사용자 직접 검수는 `BATCHED_FINAL`로 마지막 일괄 검수 대기이며 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 변경하지 않았다.

## 18. Change 006 — 12면 혼합 자재·반복 Pending 실사용 심화 검수 (2026-07-20)

### 검수 결과

- 영업→생산관리→설계→구매→자재→제조→품질→물류→영업 회계 발행요청을 실제 역할 UI로 처리했다.
- 일반 구매품 12 EA와 고객 사급품 12 EA를 혼합하고, 사급품은 제공 예정일 7월 14일보다 늦은 7월 15~20일에 2 EA씩 6회 입고했다.
- 12면 중 6면에 제조 Pending을 생성해 생산관리 인앱·내 업무와 TeamsChannel/Mail delivery 후보를 확인하고 모두 종결했다.
- 12면 제조 duration 3일 projection, 품질 4단계, 물류 3단계와 회계 발행요청 workbook을 거쳐 최종 workflow `18/18`, open Pending `0`, 프로젝트 `Completed`를 확인했다.
- isolated Full-Stack UI `1/1 PASS`, `2.2m`; disposable DB·container·network cleanup PASS.

### Finding과 gate

| ID | Severity | 상태 | 결과 |
| --- | --- | --- | --- |
| `MATERIAL-CUSTOMER-SUPPLY-OVERDUE-NOTIFICATION-MISSING` | P1 | `OPEN` | 사급 지연 전용 알림 0건. 도착·IQC·입고 확정 알림은 있으나 예정일 초과를 알리지 않음. |
| `MATERIAL-HOME-KPI-OMITS-CUSTOMER-SUPPLY-RISK` | P2 | `RESOLVED` | `TASK-HOME-002 Change 003`에서 사급 지연 품목을 Home에 집계하고 전용 필터로 연결. |
| `PROCUREMENT-INITIAL-LOAD-ACTION-UNLOCKED` | P2 | `RESOLVED` | `TASK-E2E-RELIABILITY-001 Change 001`에서 최신 초기 load 전 행 추가·저장·Excel을 잠그고 regression을 고정. |
| `MANUFACTURING-RAPID-STAGE-SAVE-LOSS` | P2 | `RESOLVED` | `TASK-011A Change 002`에서 synchronous mutation fence·저장 안내·선택 잠금을 적용하고 3-click/1-POST E2E를 고정. |
| `MULTI-PANEL-REPETITIVE-INPUT-FRICTION` | P3 | `BACKLOG` | 12면 반복 입력과 다음 패널 이동 비용이 큼. |

Open P0/P1/P2는 `0/1/0`이며 신규 알림 능력인 P1은 사용자 지시에 따라 Fable 제외·보류했다. 게시·merge gate는 `NO_GO`다. 상세 부서별 평가는 [Change 006 사용자 평가](e2e-full-suite-001-change-006-user-evaluation.md), 실행 계약은 [Change 006](e2e-full-suite-001-change-006.md)에 기록했다. 합성 evidence는 `/tmp/emi-qms-stress-lifecycle-evidence`에만 있으며 Repository에 tracked/staged하지 않았다. 대표 repo·`main`·Persistent UAT와 실제 provider는 변경하지 않았다.

### 2026-07-21 비-Fable P2 후속 검증

- `TASK-HOME-002 Change 003`: 자재 Home 사급 제공 지연 KPI·deep-link·전용 필터, isolated E2E `1/1 PASS`.
- `TASK-E2E-RELIABILITY-001 Change 001`: 구매 초기 action readiness lock, focused E2E `1/1` 및 구매→자재·권한·모바일 회귀 `1/1 PASS`.
- `TASK-011A Change 002`: 제조 동일 tick 3-click/1-POST 직렬화, 4단계·Pending·LQC E2E `1/1 PASS`.
- 합동 검증: Backend Release build warning/error `0/0`, Frontend lint error `0`(기존 warning 1), unit `113/113`, typecheck·production build·`git diff --check` PASS.
- screenshot은 `/tmp/emi-qms-p2-remediation-evidence`, E2E PostgreSQL·container·network은 모두 제거했다.
