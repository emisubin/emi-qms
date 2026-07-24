# TASK-NOTICE-BOARD-001 Change 001 — experiment fast-track과 2차 기획 승인

## 1. 실행 기준

- canonicalTaskId: `TASK-NOTICE-BOARD-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `895de8d8666bc588c634ac8bdcb9612f26326335`
- representativeMain: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/notice-board-001-interview.md`
- firstPlanningSource: `tasks/notice-board-001-planning.md`
- codexReviewSource: `tasks/notice-board-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/38-home-notice-board-plan.md`

사용자는 Home 상단·중앙은 유지하고 하단 `프로젝트 병목`만 누구나 입력 가능한 공지사항 게시판으로 교체하도록 명시했다. 이 experiment branch의 standing instruction에 따라 interview·중간 승인·권장안 채택 확인 없이 Fable 2-pass 뒤 구현·자동 검증·desktop/mobile screenshot·local commit까지 진행한다. 1차 planning과 Codex review는 수정하지 않고 2차 planning을 최종 구현 source of truth로 사용한다.

## 2. 승인·안전 경계

- planningApproved: `true` — 사용자 직접 요청과 Fable 2차 기획의 blocking decision 0 조건
- implementationApproved: `true` — 최종 2차 기획의 experiment 범위
- commitApproved: `true` — 검증·screenshot·종료 산출물 완료 뒤 local commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 3. Claude 사용량 기록

Claude `/usage`는 `bash scripts/report-claude-usage.sh`의 privacy-safe projection만 기록한다. raw TUI와 계정 식별자는 저장하지 않으며 실패 시 값을 추정하지 않는다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 00:29 KST 초기화 | 32% 사용 / 68% 잔여 / 07-25 08:00 KST 초기화 | 64% 사용 / 36% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 00:30 KST 초기화 | 33% 사용 / 67% 잔여 / 07-25 08:00 KST 초기화 | 66% 사용 / 34% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 12% 사용 / 88% 잔여 / 00:30 KST 초기화 | 33% 사용 / 67% 잔여 / 07-25 08:00 KST 초기화 | 66% 사용 / 34% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 12% 사용 / 88% 잔여 / 00:30 KST 초기화 | 33% 사용 / 67% 잔여 / 07-25 08:00 KST 초기화 | 66% 사용 / 34% 잔여 / 초기화 parse 불가 |

## 4. 최종 기획에 요구하는 불변조건

- Home 상단 부서 KPI와 중앙 내 업무·Pending·알림의 데이터·배치·deep link를 변경하지 않는다.
- 프로젝트 목록·상세 병목 집계는 유지하고 Home 하단 소비만 공지로 교체한다.
- 공지는 전용 persistence/API를 사용하고 `notifications`·`work_items`·delivery·provider를 생성하지 않는다.
- default operational authorization을 사용해 승인 대기·비활성·미인증 사용자를 서버에서 차단한다.
- client는 작성자 identity를 전달하지 않고 서버가 effective user와 현재 부서를 snapshot으로 저장한다.
- author별 request id unique로 작성 재시도를 멱등 처리한다.
- 작성자 본인 soft delete만 허용하고 원문·작성/삭제 identity와 시각을 보존한다.
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 제외한다.

## 5. Fable 1차 planning 결과

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `CREATED_FULL_BASELINE`
- baselineReused: `false`
- driftStatus: `NO_PRIOR_SESSION`
- modelSeconds: `506`
- stdoutBytes: `26125`
- stderrBytes: `0`
- artifactPath: `tasks/notice-board-001-planning.md`
- artifactWritten: `true`

## 6. Fable 2차 planning 결과

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `REFRESHED_AFTER_DRIFT`
- baselineReused: `false`
- driftStatus: `SOURCE_OR_CONTRACT_CHANGED`
- modelSeconds: `270`
- stdoutBytes: `23482`
- stderrBytes: `0`
- artifactPath: `docs/38-home-notice-board-plan.md`
- artifactWritten: `true`

## 7. 구현 종료

- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- implementationReport: `tasks/notice-board-001-implementation-report.md`
- automatedValidation: Backend `418/418`, Frontend `119/119`, Full-Stack `1/1`
- screenshots: `tasks/notice-board-001-screenshots/` 7개
- userValidation: `사용자 검수 대기 — 마지막 일괄 검수`
- commitStatus: `DEFERRED_PREEXISTING_DIRTY_BASE` — migration 0050·0051 및 shared-file 선행 WIP를 임의 포함하지 않음
- mainMergeApprovalCount: `0/3`
- 구현 종료 usage: 5시간 22% 사용/78% 잔여, 주간 전체 34% 사용/66% 잔여, Fable 67% 사용/33% 잔여
