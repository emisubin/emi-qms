# TASK-011A Change 001 — Codex review 기반 Fable 2차 기획과 실험 구현 승인

## 1. 사용자 요청 source

사용자는 이 실험 branch의 신규 기능을 `Fable 1차 기획 → Codex review → review 내용을 반영한 Fable 2차 기획 → 2차 기획 기준 Codex 구현` 순서로 진행하도록 명시했다. 인터뷰·채택·중간 확인을 다시 묻지 않고 권장안을 적용해 코드·검증·페이지별 screenshot·local commit까지 완료하며, 대표 repo와 GitHub `main`에는 반영하지 않는다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-011A`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-011a-manufacturing-work`
- interviewSource: `tasks/011a-interview.md`
- firstPlanningSource: `tasks/011a-planning.md`
- codexReviewSource: `tasks/011a-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/18-manufacturing-work-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 직접 완전히 읽고 review의 유지·추가·보류·제거 판단과 모든 resolution을 authoritative implementation contract로 통합한다.

## 3. 2차 기획 필수 반영사항

- 키팅 완료 + 활성 panel 제조 업무를 시작 전제로 재검증하고 panel당 active execution 1건 보장
- 고정 최소 checklist는 `작업지시·도면 확인 → 자재·부품 확인 → 제조 작업 수행 → 자체 확인`
- generic 내 업무 start/complete/cancel에서 panel `ManufacturingWork`를 차단하고 제조 domain API만 전이
- start/complete에서 execution·panel stage·정확한 panel 제조 업무 status를 단일 transaction으로 동기화
- 중단 사유 bounded enum, 설명, 필수 조치 담당 부서와 부서 내 assignee, Panel target `ManufacturingStop`·`Urgent` Pending
- 기존 Pending history·assignment work·blocking notification을 같은 transaction에서 생성하고 active Pending 1건 보장
- Pending Closed 뒤 같은 execution의 append-only resume, stage 후퇴 금지
- panel 완료마다 panel target LQC skeleton 업무 exactly-once, 검사 data는 생성하지 않음
- 마지막 active panel에서만 project `ManufacturingWork/StageCompleted` event exactly-once
- operation id + action/payload fingerprint + success projection replay와 expected version stale conflict
- active execution의 panel/project cancellation terminal 정합과 approved permanent purge 회귀
- 제조 담당자의 own operational time과 cross-user `Manufacturing.WorkTime.Read` 노출 구분
- project scope·assignee failure rollback·privacy-safe 응답, System Administrator 업무 입력 우회 금지
- 전용 Manufacturing Backend module과 `/manufacturing/work` 모바일/desktop adaptive page·내 업무 deep link
- 품질 상세·전체 자주순차표·template 관리·첨부·provider·Persistent UAT·대표 repo 불변

## 4. 구현·Git 승인 경계

- planningApproved: `true` — 2차 Fable 기획이 Codex review resolution을 모두 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/18-manufacturing-work-plan.md`의 최소 계약, experiment branch 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 구현·검증·screenshot·종료 산출물 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 5. Fable 사용량 기록

Claude `/usage` 퍼센트는 정수 반올림 값이다.

| 측정 시점 | 전체 모델 사용 | 전체 모델 잔여 | Fable 사용 | Fable 잔여 |
| --- | ---: | ---: | ---: | ---: |
| 1차 planning 직전 | 21% | 79% | 41% | 59% |
| 1차 planning 직후 | 22% | 78% | 43% | 57% |
| 2차 planning 직전 | 22% | 78% | 43% | 57% |
| 2차 planning 직후 | 22% | 78% | 43% | 57% |

1차 planning은 preflight 0초·model 572초·postflight 0초가 걸렸고, 전체 모델 사용량은 1%p·Fable 사용량은 2%p 증가했다.
2차 planning은 기존 session artifact를 재사용해 preflight 1초·model 212초·postflight 0초가 걸렸고, 표시 사용량은 정수 반올림 범위에서 변하지 않았다. 직전 사용량 첫 측정은 TUI timeout(exit 23)으로 실패했으며 repository mutation 없이 즉시 재측정해 READY projection을 확보한 뒤 2차 planning을 호출했다.

## 6. 완료 조건

- 2차 Fable 기획 `openBlockingDecisionCount: 0`
- additive migration과 Backend·Frontend·isolated E2E 구현·검증
- `/manufacturing/work` queue·시작·체크·중단·재개·종료의 desktop·390px screenshot
- implementation report·SOP·user manual·user validation checklist·Roadmap experiment 상태 갱신
- privacy/secret·diff·Finding gate 통과
- current experiment branch local commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
