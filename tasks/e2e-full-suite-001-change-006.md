# TASK-E2E-FULL-SUITE-001 change-006 — 12면 혼합 자재·반복 Pending 실사용 심화 검수

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001 change-006`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-E2E-FULL-SUITE-001`
- roadmapNextGate: `BATCHED_FINAL_USER_VALIDATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-E2E-FULL-SUITE-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-E2E-FULL-SUITE-001`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 12면 프로젝트를 실제 역할 화면에서 처음부터 끝까지 입력하고, 일반 구매품과 사급품의 혼합 입고, 7월 15~20일 사급 분할·지연 입고, 제조 3일 소요와 반복 Pending, 최종 회계 발행 확인·프로젝트 완료를 연속 검수한다.
- Root Finding 또는 정책 결정: 기존 Change 001~005는 단일 패널 정상 흐름의 정합성을 증명했지만 다면 프로젝트에서 반복 입력 부담, 사급 지연 추적·알림, 반복 제조 중단과 부서 KPI의 실무 유용성을 평가하지 않았다.
- 변경·검증 경계: 제품 source·정책을 수정하지 않고 전용 isolated Full-Stack UAT 시나리오, 합성 screenshot과 부서별 UX/KPI Finding만 추가한다.
- 보존할 불변조건: 18단계, 역할별 mutation 권한, 담당자 정·부 구조, Pending 상태 전이, 알림 idempotency, 대표 repo·`main`, Persistent UAT와 실제 provider를 변경하지 않는다.
- 예상 산출물: 12면 UI 입력 회귀, 혼합 자재 7회 입고·IQC·확정, 6건 제조 Pending·TeamsChannel/Mail 후보 검증, 12면 3일 제조 duration projection, 최종 18/18·open Pending 0, 부서별 dashboard screenshot과 최소 3개 UX/KPI 평가.

## 사용자 시나리오

1. 영업이 12면 프로젝트를 생성한다.
2. 생산관리에서 일정과 전 부서 정·부 담당자를 지정한다.
3. 설계가 12면 패널명·W/H/D를 입력한다.
4. 구매가 일반 구매품 1종과 사급품 1종을 등록한다.
5. 사급품은 제공 예정일보다 늦은 7월 15~20일에 2 EA씩 6회 입고하고, 일반 구매품은 12 EA 1회 입고한다.
6. 모든 7개 도착분을 자재→IQC→자재 입고 확정으로 처리하고, 사급 잔량·지연 badge와 지연 알림 여부를 확인한다.
7. 12면 키팅 뒤 6개 패널에서 제조 중단 Pending을 반복 생성하고 생산관리 담당자가 모두 종결한다.
8. 제조 UI 상태 전이는 담당자가 수행하고, 격리 DB의 완료 실행 시간을 3일 duration으로 projection해 12면 전부 확인한다.
9. 12면 LQC·제조 완료 확인·OQC·전진검수·FAT, 일괄 포장·출발·납품, 영업 회계 발행요청과 최종 완료를 처리한다.
10. 업무 peak 시점의 8개 운영 부서 Home KPI와 핵심 업무 화면을 촬영해 실무 유용성을 평가한다.

## 승인·운영 경계

- investigationApproved: `true`
- validationApproved: `true`
- productImplementationApproved: `false` — 발견한 제품 결함은 수정하지 않고 Finding으로 보고
- localCommitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 검증 기준

- 모든 mutation은 실행별 disposable PostgreSQL에서만 수행한다.
- 역할별 핵심 입력은 UI를 사용하며, 3일 경과는 장시간 대기를 대신하는 isolated DB 시간 projection으로만 보조한다.
- Pending 6건 각각 생산관리 인앱·내 업무와 TeamsChannel/Mail delivery 후보를 생성하되 provider는 disabled/dry-run 상태로 유지한다.
- 사급 예정일 지연 알림이 없으면 성공으로 보정하지 않고 count `0`과 UI 증거를 Finding으로 기록한다.
- 최종 프로젝트 `Completed`, workflow `18/18`, active panel `12`, open Pending `0`, 제조 duration 3일 panel `12`를 확인한다.
- screenshot과 workbook은 `/tmp` Task-owned 경로에만 만들고 tracked/staged하지 않는다.

## 실행 결과

- isolated Full-Stack UI: `1/1 PASS`, `2.2m`
- 12면, 일반 구매품 1종, 사급품 1종, 사급 분할 입고 6회, 제조 Pending 6건을 실제 역할 화면에서 입력했다.
- 사급 지연 전용 알림은 `0건`이었다. 자재 사용자에게 표시된 9건은 도착·IQC·입고 확정 업무 알림이며 예정일 초과를 직접 알리지 않았다.
- 제조 Pending 6건은 생산관리 인앱 알림·내 업무에 반영됐고, 각 이벤트에 `TeamsChannel`과 `Mail` delivery 후보가 생성됐다. 실제 provider 발송은 승인 경계에 따라 실행하지 않았다.
- 12면 모두 제조 duration 3일 projection, 품질 4단계, 물류 3단계, 회계 발행요청 자료 생성을 거쳐 workflow `18/18`, open Pending `0`, 프로젝트 `Completed`를 확인했다.
- 상세 증거·부서별 평가는 [Change 006 사용자 평가](e2e-full-suite-001-change-006-user-evaluation.md)에 기록했다.

## Finding gate

| ID | Severity | 상태 | 원인·영향 |
| --- | --- | --- | --- |
| `MATERIAL-CUSTOMER-SUPPLY-OVERDUE-NOTIFICATION-MISSING` | P1 | `OPEN` | 사급 예정일이 지나고 잔량이 있어도 예정일 초과 이벤트가 생성되지 않아 자재 사용자가 화면을 계속 열어봐야 한다. |
| `MATERIAL-HOME-KPI-OMITS-CUSTOMER-SUPPLY-RISK` | P2 | `RESOLVED` | `TASK-HOME-002 Change 003`에서 사급 지연 품목을 Home에 집계하고 전용 필터로 연결했다. |
| `PROCUREMENT-INITIAL-LOAD-ACTION-UNLOCKED` | P2 | `RESOLVED` | `TASK-E2E-RELIABILITY-001 Change 001`에서 최신 초기 load 전 행 추가·저장·Excel을 잠그고 regression을 고정했다. |
| `MANUFACTURING-RAPID-STAGE-SAVE-LOSS` | P2 | `RESOLVED` | `TASK-011A Change 002`에서 synchronous mutation fence·저장 안내·선택 잠금을 적용하고 3-click/1-POST E2E를 고정했다. |
| `MULTI-PANEL-REPETITIVE-INPUT-FRICTION` | P3 | `BACKLOG` | 설계·제조·품질·물류에서 12면 반복 입력과 다음 패널 이동 비용이 크다. |

Open P0/P1/P2는 `0/1/0`이다. 따라서 Change 006 검수 실행은 완료됐지만 신규 알림 능력인 P1은 사용자 지시에 따라 Fable 제외·보류했다. 게시·merge gate는 계속 `NO_GO`다.

## 2026-07-21 후속 상태

- 비-Fable P2 3건은 각 canonical Task Change에서 구현·문서화·isolated E2E까지 완료했다.
- Backend Release build warning/error `0/0`, Frontend lint error `0`, unit `113/113`, typecheck·production build·`git diff --check` PASS.
- 사급 지연 자동 알림 P1과 다면 bulk·복제·다음 패널 동선 P3은 신규 제품 능력이어 사용자의 Fable 제외 지시 범위에서 보류했다.
- local commit·push·PR·merge·Persistent UAT·실제 provider는 미실행이며 `main` merge 승인은 `0/3`이다.
