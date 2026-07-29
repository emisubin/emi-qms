# TASK-013A Change 001 — Codex review 기반 Fable 2차 기획과 실험 구현 승인

## 1. 사용자 요청 source

사용자는 이 실험 branch의 신규 기능을 `Fable 1차 기획 → Codex review → review 내용을 반영한 Fable 2차 기획 → 2차 기획 기준 Codex 구현` 순서로 진행하도록 명시했다. 인터뷰·채택·중간 확인을 다시 묻지 않고 권장안을 적용해 코드·검증·페이지별 screenshot·local commit까지 완료하며, 대표 repo와 GitHub `main`에는 반영하지 않는다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-013A`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-013a-logistics`
- interviewSource: `tasks/013a-interview.md`
- firstPlanningSource: `tasks/013a-planning.md`
- codexReviewSource: `tasks/013a-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/20-logistics-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 직접 완전히 읽고 review의 유지·추가·보류·제거 판단과 모든 resolution을 authoritative implementation contract로 통합한다.

## 3. 2차 기획 필수 반영사항

- same-project flat Packing Unit과 active membership unique·project별 순번 동시성
- `logistics.ship` + scope + project 물류 정·부 또는 선택 panel 전체 current-work assignee 권한
- same-project unit batch, 선택 unit 전체 panel의 출발·납품 원자 처리와 부분 납품 보류
- 물류 3 stage generic work transition 차단과 `/logistics` deep link
- 포장·납품에서만 coarse panel stage 전진, 출발은 finalized record에서 파생
- 사진/서명 evidence content sniff·size/count·hash·scope·no-store·finalized append-only
- operation fingerprint/replay·expected version·stable row lock·unique constraint
- open project/panel Pending 차단, cancellation과 approved permanent purge 정합
- 다음 물류·영업 담당자 부재 시 현재 finalize 전체 rollback
- active panel finalized relation 집계의 project stage event·Sales skeleton exactly-once
- 모바일 stage → 대상 → 증빙 → 확정 흐름과 desktop queue/detail composition
- 외부 운송·전자서명 생성·부분 납품·정정·정산 상세·Persistent UAT·대표 repo 불변

## 4. 구현·Git 승인 경계

- planningApproved: `true` — 2차 Fable 기획이 Codex review resolution을 모두 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/20-logistics-plan.md`의 최소 계약, experiment branch 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 구현·검증·screenshot·종료 산출물 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 5. Fable 사용량 기록

Claude `/usage` 측정은 Repository mutation 없이 `bash scripts/report-claude-usage.sh`만 사용한다. 2026-07-18 사용자 지시에 따라 앞으로 5시간 현재 세션 사용·잔여·초기화 시각과 주간 전체/Fable 사용·잔여·초기화 시각을 함께 기록한다. 실패한 조회는 수치를 추정하지 않고 종료 상태 그대로 기록한다.

| 측정 시점 | 결과 |
| --- | --- |
| 1차 planning 직전 | 전체 25% 사용·75% 잔여, Fable 50% 사용·50% 잔여 |
| 1차 planning 직후 | 측정 불가 — TUI timeout `exit 23`, 2회 |
| 2차 planning 직전 | 전체 26% 사용·74% 잔여, Fable 52% 사용·48% 잔여 |
| 2차 planning 재시도 직전 | 5시간 세션 0% 사용·100% 잔여·05:20 KST 초기화, 주간 전체 27% 사용·73% 잔여, 주간 Fable 54% 사용·46% 잔여 |
| 2차 planning 직후 | 5시간 세션 0% 사용·100% 잔여·05:20 KST 초기화, 주간 전체 28% 사용·72% 잔여, 주간 Fable 56% 사용·44% 잔여 |

1차 planning runner는 preflight 0초·model 522초·postflight 1초가 걸렸고 `tasks/013a-planning.md` 26,472 bytes를 byte-for-byte 저장했다.

2차 planning 첫 호출은 2026-07-17 22:41 KST에 account session limit(`resets 11:50pm (Asia/Seoul)`)로 fail-closed 중단됐다. model 18초, stdout 61 bytes였고 `docs/20-logistics-plan.md`는 생성되지 않았다. 이는 2차 기획 완료가 아니며 재설정 뒤 동일 승인·target으로 재시도한다.

2차 planning 재시도는 2026-07-18 초기화 뒤 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 성공했다. model 211초, stdout 25,370 bytes, stderr 0이며 `docs/20-logistics-plan.md`를 byte-for-byte 저장했다. 최종 `openBlockingDecisionCount: 0`을 확인해 experiment 구현 Gate를 통과했다.

## 6. 완료 조건

- 2차 Fable 기획 `openBlockingDecisionCount: 0`
- additive migration과 Backend·Frontend·isolated E2E 구현·검증
- 물류 workspace desktop·390px screenshot
- implementation report·SOP·user manual·user validation checklist·Roadmap experiment 상태 갱신
- privacy/secret·diff·Finding gate 통과
- current experiment branch local commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
