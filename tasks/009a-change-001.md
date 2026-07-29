# TASK-009A Change 001 — Codex review 기반 Fable 2차 기획과 실험 구현 승인

## 1. 사용자 요청 source

사용자는 이 실험 branch의 신규 기능을 `Fable 1차 기획 → Codex review → review 내용을 반영한 Fable 2차 기획 → 2차 기획 기준 Codex 구현` 순서로 진행하도록 명시했다. 인터뷰·채택·중간 확인을 다시 묻지 않고 권장안을 적용해 코드·검증·페이지별 screenshot·local commit까지 완료하며, 대표 repo와 GitHub `main`에는 반영하지 않는다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-009A`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-009a-iqc-digital-report`
- interviewSource: `tasks/009a-interview.md`
- firstPlanningSource: `tasks/009a-planning.md`
- codexReviewSource: `tasks/009a-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/16-iqc-digital-report-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 직접 완전히 읽고 review의 유지·추가·보류·제거 판단과 모든 resolution을 authoritative implementation contract로 통합한다.

## 3. 2차 기획 필수 반영사항

- 기존 attempt의 `Legacy`, 신규·재검사 attempt의 `Detailed` 판정 mode와 `/result` 호환 gate
- permission과 project access scope를 결합한 queue/report/photo/PDF read·download·mutation authorization
- side-effect 없는 GET과 idempotent POST Draft initialize
- report version lock, Draft photo mutation, Finalized append-only
- canonical UTF-8 snapshot + hash와 기존 008A 판정 transaction의 원자성
- post-commit `Pending|Ready|Failed` PDF artifact, snapshot hash별 idempotent bounded retry와 최초 성공 byte 고정
- JPEG/PNG magic-byte, 파일당 5MB·최대 5장·총15MB, 안전한 display metadata
- 필수 외함 사진이 실제 포함된 PDF, PDFsharp 6.2.4, repository 동봉 OFL 한글 font·license·provenance·SHA-256·custom resolver
- request별 download authorization, `private, no-store`, 합성 안전 파일명과 privacy-safe error
- 전용 IQC report frontend component와 desktop·390px·Teams narrow adaptive UI
- 기존 008A 상태·Pending·work item·receipt derived 계약과 migration `0030`·`0031` 불변

## 4. 구현·Git 승인 경계

- planningApproved: `true` — 2차 Fable 기획이 Codex review resolution을 모두 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/16-iqc-digital-report-plan.md`의 최소 계약, experiment branch 한정
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
| 1차 planning 직전 | 16% | 84% | 31% | 69% |
| 1차 planning 직후 | 16% | 84% | 31% | 69% |
| 2차 planning 직전 | 16% | 84% | 32% | 68% |
| 2차 planning 직후 | 17% | 83% | 33% | 67% |

1차 planning은 preflight 1초·model 568초·postflight 0초가 걸렸다. 표시 사용량은 정수 반올림 범위 안에서 변하지 않았다.
2차 planning은 기존 session artifact를 재사용해 preflight 1초·model 218초·postflight 1초가 걸렸고 전체 모델과 Fable 표시 사용량이 각각 1%p 증가했다.

## 6. 완료 조건

- 2차 Fable 기획 `openBlockingDecisionCount: 0`
- additive migration과 Backend·Frontend·isolated E2E 구현·검증
- `/quality/iqc` 작성·완료·legacy 상태의 desktop·390px screenshot
- implementation report·user validation checklist·Roadmap experiment 상태 갱신
- privacy/secret·diff·Finding gate 통과
- current experiment branch local commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
