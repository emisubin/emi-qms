# TASK-BILLING-REQUEST-001 Change 001 — experiment fast-track과 2차 기획 승인

## 실행 기준

- canonicalTaskId: `TASK-BILLING-REQUEST-001`
- taskType: `NEW_FEATURE`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `9250384b330a9f524bd964d01da8f00ed661ab75`
- representativeMain: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/billing-request-001-interview.md`
- firstPlanningSource: `tasks/billing-request-001-planning.md`
- codexReviewSource: `tasks/billing-request-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/33-billing-request-plan.md`

사용자는 영업 담당자가 매월 1일·16일 해당 기간의 출하 완료 프로젝트를 선택해 회계팀 세금계산서 발행요청 Excel을 만드는 기능을 인터뷰·중간 승인 없이 구현하고 결과까지 보여 달라고 명시했다. 1차 Fable 원문과 Codex review는 수정하지 않으며, review 기반 2차 Fable 기획을 최종 구현 source of truth로 사용한다.

## 승인·안전 경계

- planningApproved: `true` — 2차 기획 blocking decision 0 조건
- implementationApproved: `true` — 최종 2차 기획의 experiment 범위
- commitApproved: `true` — 종료 산출물·검증·screenshot 완료 뒤 local commit만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## Codex review 자동 채택 resolution

- 후보 기준은 최종 납품일이 아니라 모든 active panel의 Finalized `DepartureProcessed`와 출발일이다.
- 한 project가 여러 출발 batch로 나뉘면 첫 출발일·마지막 출발일을 snapshot하고 마지막 출발일을 기간 기준으로 사용한다.
- project별 기존 요청은 다시 선택할 수 없으며 revision·Superseded·취소는 이번 MVP에서 제외한다.
- desktop 기간 보정은 시작≤종료≤오늘·최대 31일, mobile은 추천 반월 기간을 우선한다.
- 발행요청은 project 완료가 아니다. 기존 014A는 `회계팀 발행 확인일 입력 → project 완료`로 문구와 상태 의미를 교정한다.
- workbook에는 Repository 근거 열과 `회계팀 기입란`으로 표시된 빈 발행일·세금계산서 번호 열만 포함한다.

## Claude 사용량 기록

Claude `/usage`는 `bash scripts/report-claude-usage.sh`의 privacy-safe projection만 기록한다. raw TUI와 계정 식별자는 저장하지 않으며 값 변화가 비단조여도 표시된 수치를 그대로 기록한다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 05:20 KST 초기화 | 25% 사용 / 75% 잔여 / 07-25 07:59 KST 초기화 | 49% 사용 / 51% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 05:19 KST 초기화 | 25% 사용 / 75% 잔여 / 07-25 07:59 KST 초기화 | 49% 사용 / 51% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 planning 직전 | 9% 사용 / 91% 잔여 / 05:19 KST 초기화 | 50% 사용 / 50% 잔여 / 07-25 07:59 KST 초기화 | 15% 사용 / 85% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 9% 사용 / 91% 잔여 / 05:19 KST 초기화 | 50% 사용 / 50% 잔여 / 07-25 07:59 KST 초기화 | 21% 사용 / 79% 잔여 / 초기화 parse 불가 |

1차 runner는 `CREATED_FULL_BASELINE`, `baselineReused=false`, `driftStatus=NO_PRIOR_SESSION`으로 성공했고 model 410초, stdout 26,895 bytes, stderr 0이었다. 원문은 `tasks/billing-request-001-planning.md`에 byte-for-byte 저장됐다.

2차 runner는 review/change 추가를 예상 source drift로 판정해 `REFRESHED_AFTER_DRIFT`, `baselineReused=false`, `driftStatus=SOURCE_OR_CONTRACT_CHANGED`로 기준선을 갱신했다. model 296초, stdout 29,082 bytes, stderr 0이며 최종 원문은 `docs/33-billing-request-plan.md`에 byte-for-byte 저장됐다.
