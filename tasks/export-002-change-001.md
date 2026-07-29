# TASK-EXPORT-002 Change 001 — 실험 fast-track과 Fable 2차 기획 승인 경계

## 1. 사용자 요청 source

사용자는 이 실험 branch의 신규 기능을 `Fable 1차 기획 → Codex review → review 기반 Fable 2차 기획 → Codex 구현` 순서로 진행하고, 인터뷰·채택·중간 확인 없이 권장안을 적용해 검증·페이지 screenshot·실제 Excel screenshot·local commit까지 완료하도록 명시했다. 2026-07-18 사용자가 “여러 프로젝트 선택해서 내보내는 기능도 있어? 없다면 만들고난 후 페이지와 엑셀파일 스크린샷보여줘. 그리고 켠 엑셀 파일은 다시 꺼.”라고 직접 요청해 `TASK-EXPORT-002`를 진행한다.

## 2. Task와 기획 source

- canonicalTaskId: `TASK-EXPORT-002`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-export-002-selected-project-export`
- interviewSource: `tasks/export-002-interview.md`
- firstPlanningSource: `tasks/export-002-planning.md`
- codexReviewSource: `tasks/export-002-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/23-selected-project-export-plan.md`

1차 Fable 원문과 Codex review는 수정하지 않는다. 2차 Fable 기획은 두 문서를 완전히 읽고 review의 유지·추가·보류·제거와 Finding resolution을 authoritative implementation contract로 통합한다.

## 3. 구현·Git 승인 경계

- planningApproved: `true` — 2차 기획이 review resolution을 반영하고 blocking decision 0인 조건
- implementationApproved: `true` — `docs/23-selected-project-export-plan.md`의 최소 계약, experiment branch 한정
- userValidationCompleted: `false`
- commitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 4. Fable 사용량 기록

Claude `/usage`는 Repository mutation 없이 `bash scripts/report-claude-usage.sh`로 측정한다. reporter가 제공하지 못한 값은 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 17:40 KST 초기화 | 4% 사용 / 96% 잔여 / 07-25 07:59 KST 초기화 | 8% 사용 / 92% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 17:40 KST 초기화 | 16% 사용 / 84% 잔여 / 07-25 08:00 KST 초기화 | 11% 사용 / 89% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 16% 사용 / 84% 잔여 / 17:39 KST 초기화 | 6% 사용 / 94% 잔여 / 07-25 07:59 KST 초기화 | 11% 사용 / 89% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 16% 사용 / 84% 잔여 / 17:40 KST 초기화 | 6% 사용 / 94% 잔여 / 07-25 08:00 KST 초기화 | 11% 사용 / 89% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 16% 사용 / 84% 잔여 / 17:40 KST 초기화 | 6% 사용 / 94% 잔여 / 07-25 08:00 KST 초기화 | 11% 사용 / 89% 잔여 / 초기화 parse 불가 |

1차 planning runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 실행됐다. model 425초, stdout 27,512 bytes, stderr 0이며 `tasks/export-002-planning.md`를 byte-for-byte 저장했다.

2차 planning runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`로 성공했다. model 185초, stdout 22,341 bytes, stderr 0이며 `docs/23-selected-project-export-plan.md`를 byte-for-byte 저장했다. 최종 문서의 `openBlockingDecisionCount`는 0이다.

## 5. Codex review Finding resolution

| ID | Severity | 2차 기획 요구 상태 | Resolution |
| --- | --- | --- | --- |
| `SELECTED-EXPORT-ATOMIC-SCOPE` | P1 | `REQUIRED` | 동일 scope query·requested count exact match·generic 422·file/audit 0건 |
| `SELECTED-EXPORT-ROW-KEYBOARD-CONFLICT` | P2 | `REQUIRED` | checkbox와 행 click/Enter/Space event 완전 분리 |
| `SELECTED-EXPORT-STABLE-BODY-VALIDATION` | P2 | `REQUIRED` | string ID 수동 parse·empty/duplicate/101건 모두 stable 422 |
| `SELECTED-EXPORT-AUDIT-MIGRATION` | P2 | `REQUIRED` | 0038 수정 없이 additive migration으로 `ProjectsSelected`와 민감 컬럼 check 확장 |
| `SELECTED-EXPORT-ERROR-RECOVERY` | P2 | `REQUIRED` | 선택 422는 목록 reload/reselect, 기존 전체 필터 422 문구는 보존 |
| `SELECTED-EXPORT-REQUEST-SNAPSHOT` | P2 | `REQUIRED` | 실행 시 ID snapshot·진행 중 selection 잠금·실패 시 선택 유지 |
| `SELECTED-EXPORT-SELECTION-LIFECYCLE` | P2 | `REQUIRED` | filter/tab/date/reload/new response 초기화·성공/실패 유지·Deleted 제외 |
| `SELECTED-EXPORT-MOBILE-ACTION-POSITION` | P3 | `REQUIRED` | fixed bottom이 아닌 card list 위 compact inline tray |
| `SELECTED-EXPORT-EXISTING-ACTION-REGRESSION` | P3 | `REQUIRED` | helper/action optional 확장·기존 GET export 회귀 유지 |

## 6. 완료 조건

- 1차 Fable planning과 Codex 내용 review 보존
- 2차 Fable planning `openBlockingDecisionCount: 0`
- 선택 3건 중 2건 export, 권한/scope/stale/audit/formula safety와 기존 export 회귀 검증
- 프로젝트 목록 desktop·390px mobile screenshot
- 실제 생성된 선택 Excel workbook screenshot과 workbook close 확인
- implementation report·Roadmap 상태 갱신
- privacy/secret·diff·Finding gate 통과와 local experiment commit
- 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0
