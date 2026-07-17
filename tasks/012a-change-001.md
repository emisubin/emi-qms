# TASK-012A Change 001 — Codex review 기반 Fable 2차 기획과 실험 구현 승인

## 1. 사용자 요청 source

사용자는 이 실험 branch의 신규 기능을 `Fable 1차 기획 → Codex review → review 내용을 반영한 Fable 2차 기획 → 2차 기획 기준 Codex 구현` 순서로 진행하도록 명시했다. 인터뷰·채택·중간 확인을 다시 묻지 않고 권장안을 적용해 코드·검증·페이지별 screenshot·local commit까지 완료하며, 대표 repo와 GitHub `main`에는 반영하지 않는다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-012A`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-012a-quality-inspections`
- interviewSource: `tasks/012a-interview.md`
- firstPlanningSource: `tasks/012a-planning.md`
- codexReviewSource: `tasks/012a-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/19-quality-inspections-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 직접 완전히 읽고 review의 유지·추가·보류·제거 판단과 모든 resolution을 authoritative implementation contract로 통합한다.

## 3. 2차 기획 필수 반영사항

- panel + stage별 attempt/report와 재검사 attempt 누적, finalized report 불변
- stage 정·부 담당 또는 current work assignee + permission + scope 기반 mutation 권한
- panel LQC·ManufacturingCompleted·OQC·CustomerInspection·FAT generic work transition 차단
- linked quality Pending generic close 차단, 합격 재검사 transaction만 종결
- 실패/PUNCH의 필수 조치 담당 부서, optional 같은 부서 assignee, Panel Pending/history/work/notification 원자 생성
- 다음 stage 담당자 부재 시 report finalize·제조확인·FAT skip 전체 rollback
- LQC pass → 독립 제조완료확인 → OQC → 고객검수 → 선택 FAT 또는 Packing skeleton panel별 즉시 handoff
- terminal attempt/confirmation 집계와 project lock을 통한 last-panel stage event exactly-once
- TASK-011A manufacturing execution을 수정하지 않는 별도 immutable 제조완료확인
- Material IQC와 분리된 panel quality stage template seed/catalog·attempt-local snapshot
- required item 판정 규칙, 최소 v1 양식, optional bounded 사진, 공통 panel quality PDF
- operation fingerprint/replay·expected version·privacy-safe success projection
- cancellation·approved permanent purge와 immutability 정합, TASK-009A 범위 밖 수정 금지
- `/quality/inspections` adaptive workspace, 기존 `/quality/iqc` 호환, 제조확인 `/manufacturing/work` card
- 실제 고객 양식·template 편집·필수 사진·외부 provider·Persistent UAT·대표 repo 불변

## 4. 구현·Git 승인 경계

- planningApproved: `true` — 2차 Fable 기획이 Codex review resolution을 모두 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/19-quality-inspections-plan.md`의 최소 계약, experiment branch 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 구현·검증·screenshot·종료 산출물 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 5. Fable 사용량 기록

Claude `/usage` 측정은 repository mutation 없이 `bash scripts/report-claude-usage.sh`만 사용한다.

| 측정 시점 | 결과 |
| --- | --- |
| 1차 planning 직전 | 측정 불가 — TUI timeout `exit 23`, 3회 |
| 1차 planning 직후 | 측정 불가 — TUI timeout `exit 23`, 2회 |
| 2차 planning 직전 | 전체 24% 사용·76% 잔여, Fable 48% 사용·52% 잔여 |
| 2차 planning 직후 | 전체 24% 사용·76% 잔여, Fable 48% 사용·52% 잔여 |

1차 planning runner는 preflight 1초·model 435초·postflight 1초가 걸렸고 `tasks/012a-planning.md` 30,042 bytes를 byte-for-byte 저장했다. 사용량 측정 실패는 수치를 추정하거나 성공으로 기록하지 않으며 Fable readonly planning 결과와 Repository 변경 경계에는 영향을 주지 않았다.

2차 planning 직전에는 세 번 연속 READY projection을 확인했다. 첫 결과는 전체 24%·Fable 47%, 이어진 두 결과는 전체 24%·Fable 48%였으며, 실행에 가장 가까운 마지막 값을 위 표에 기록했다. Fable reset 문자열만 마지막 두 번 `UNAVAILABLE_TUI_PARSE`였고 사용·잔여 비율은 정상 projection되었다.

2차 planning은 기존 Fable session artifact를 재사용해 preflight 0초·model 221초·postflight 0초가 걸렸고 `docs/19-quality-inspections-plan.md` 24,058 bytes를 byte-for-byte 저장했다. 직후 세 번 모두 전체 24%·Fable 48%로 동일했으며, 정수 반올림 범위에서 표시 사용량 증가는 없었다.

## 6. 완료 조건

- 2차 Fable 기획 `openBlockingDecisionCount: 0`
- additive migration과 Backend·Frontend·isolated E2E 구현·검증
- 품질 workspace와 제조완료확인의 desktop·390px screenshot
- implementation report·SOP·user manual·user validation checklist·Roadmap experiment 상태 갱신
- privacy/secret·diff·Finding gate 통과
- current experiment branch local commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
