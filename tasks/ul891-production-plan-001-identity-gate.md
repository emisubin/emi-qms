# TASK-UL891-PRODUCTION-PLAN-001 — Task Identity Gate

- proposedTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `OPERATIONS_TRANSITION`
- roadmapNextGate: `OPERATIONS_PROMOTION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-UL891-PRODUCTION-PLAN-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_EXPLICIT_UL891_SET_PRODUCTION_PLAN`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: UL891 세트형 프로젝트의 생산관리 탭에서 프로젝트 전체와 세트별 생산계획표·계획/실적 일정표를 전환하고, 각 세트의 계획 기간·담당자·필요 인원·생산관리 코멘트를 독립적으로 관리한다.
- Root Finding 또는 정책 결정: 현재 UL891은 세트 사양·실물 세트 인스턴스·개별 패널 계층을 갖지만 생산계획은 프로젝트 단위 snapshot 하나뿐이다. 서로 다른 세트가 다른 시기에 제조·품질·출하되어도 계획과 실적을 세트별로 비교할 수 없다.
- 변경·검증 경계: UL891 세트 생산계획 data lifecycle, 기존 LinkedV1 계획 항목·실적 연결 재사용, 세트별 계획 CRUD·CAS·audit, 프로젝트 전체 aggregate, 생산계획표·일정표 내부 scope tab, migration·API·Frontend·isolated test·desktop/mobile 증빙을 포함한다.
- 보존할 불변조건: 비-UL891과 기존 평면 UL891은 현재 프로젝트 단위 계획을 유지한다. 세트 사양과 실물 세트 인스턴스, 개별 패널 실행 원자를 섞지 않는다. 제조·LQC·OQC·전진검수·FAT·물류 실적은 개별 패널 원본에서 파생하며 수동 수정하지 않는다. 구매·자재·IQC처럼 세트 귀속이 없는 원본을 임의로 한 세트 데이터로 복제하지 않는다. 기존 프로젝트 snapshot, 권한·audit·18단계 workflow·부분출하·Pending 계약과 대표 repo·`main`·Persistent UAT·실제 provider를 보존한다.
- 예상 산출물: Fable 1차 planning 원문, Codex 내용 review, Fable 2차 planning 원문, additive migration, Backend/Frontend 구현, 자동 검증, desktop/mobile screenshot, Implementation report, Roadmap·완료 원장 update와 local experiment commit.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 중복·순서 판정

- `TASK-UL891-SET-001`은 세트 사양·실물 세트·개별 패널·부분출하를 구현했지만 세트별 생산계획은 포함하지 않는다.
- `TASK-PRODUCTION-CONTROL-001`은 Item별 양식과 프로젝트 단위 계획·자동 실적을 구현했지만 UL891 세트 scope를 포함하지 않는다.
- 두 완료 Task의 단순 재구현이 아니라 두 data model을 연결하는 신규 사용자 능력이며 같은 목적의 Task·branch·worktree·PR은 없다.
- Roadmap 기본 Next Gate는 운영 전환이지만, 사용자는 현재 experiment에서 UL891 세트별 생산계획을 구체적으로 구현하라고 명시했다. 기존 standing instruction의 Roadmap 외 실험 기능 즉시 구현 승인과 이번 exact 요청을 명시적 순서 변경으로 적용한다.

## 실행·게시 경계

- `experiment/*` Fable 2-pass fast-track을 적용한다.
- 현재 dirty WIP를 reset·checkout·정리하지 않고 이번 Task allowlist만 추가한다.
- local experiment commit까지 허용되지만 기존 누적 WIP와 파일 overlap이 안전하지 않으면 commit을 보류하고 이유를 기록한다.
- 대표 repo·`main`, push·PR·merge, Persistent UAT migration·runtime handover와 실제 provider는 제외한다.
- `main` merge 승인: `0/3`.
