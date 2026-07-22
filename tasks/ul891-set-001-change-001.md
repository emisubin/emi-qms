# TASK-UL891-SET-001 Change 001 — experiment fast-track과 2차 기획 승인

## 1. 실행 기준

- canonicalTaskId: `TASK-UL891-SET-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `895de8d8666bc588c634ac8bdcb9612f26326335`
- representativeMain: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- interviewSource: `tasks/ul891-set-001-interview.md`
- firstPlanningSource: `tasks/ul891-set-001-planning.md`
- codexReviewSource: `tasks/ul891-set-001-review.md`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/41-ul891-panel-set-plan.md`

사용자는 UL891의 세트 사양·세트 주문 인스턴스·개별 패널 계층, 부분출하, 수량 감소와 발주 회수, 월별 발행 요청 정책을 직접 확정하고 Fable 기획 뒤 구현까지 명시했다. 이 experiment branch의 standing instruction에 따라 interview·중간 승인·권장안 채택 확인 없이 Fable 2-pass 뒤 구현·자동 검증·desktop/mobile screenshot·local commit까지 진행한다. 1차 planning과 Codex review는 수정하지 않고 2차 planning을 최종 구현 source of truth로 사용한다.

## 2. 승인·안전 경계

- planningApproved: `true`
- implementationApproved: `true` — 최종 2차 기획의 blocking decision 0인 experiment 범위
- commitApproved: `true`
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
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 14:49 KST 초기화 | 34% 사용 / 66% 잔여 / 07-25 07:59 KST 초기화 | 68% 사용 / 32% 잔여 / 07-25 07:59 KST 초기화 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 14:49 KST 초기화 | 35% 사용 / 65% 잔여 / 07-25 07:59 KST 초기화 | 69% 사용 / 31% 잔여 / 07-25 07:59 KST 초기화 |
| 2차 planning 직전 | 11% 사용 / 89% 잔여 / 14:49 KST 초기화 | 35% 사용 / 65% 잔여 / 07-25 07:59 KST 초기화 | 69% 사용 / 31% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 11% 사용 / 89% 잔여 / 14:49 KST 초기화 | 35% 사용 / 65% 잔여 / 07-25 07:59 KST 초기화 | 69% 사용 / 31% 잔여 / 초기화 parse 불가 |

## 4. 최종 기획에 요구하는 불변조건

- UL891만 세트 구조를 사용하고 비-UL891·legacy 프로젝트의 평면 패널 흐름은 유지한다.
- 세트 정의와 실제 세트 인스턴스·개별 패널을 분리하고 panel identifier를 재사용하지 않는다.
- 제조·LQC·OQC·전진검수·FAT·QR·물류 실행 원자는 개별 physical panel로 유지한다.
- 세트 일부 출하는 기존 Packing Unit에 eligible panel subset을 담는 방식으로 지원하고 Packing Unit 출발 원자성을 깨지 않는다.
- 납품 패널 snapshot은 불변이며 진행 세트 사양 변경·취소는 명시적 대상·사유·예외 확인을 요구한다.
- 발주일이 있는 품목과 관련된 수량 감소는 차단 대신 영업의 회수 추적 사례를 만든다.
- 세금계산서 발행 요청은 프로젝트×출하 달력월을 key로 여러 건 허용하고 수동 요청 금액 누계가 프로젝트 판매액을 초과하지 않게 서버에서 잠근다.
- 프로젝트 완료는 active panel 납품, 월별 회계 발행 확인, 발주 취소 회수 확인, Open Pending 0을 모두 요구한다.
- Backend가 권한·validation·lifecycle·동시성의 authoritative source이며 조회전용 타부서는 조회만 가능하다.
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 제외한다.

## 5. Fable 1차 planning 결과

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `CREATED_FULL_BASELINE`
- baselineReused: `false`
- driftStatus: `NO_PRIOR_SESSION`
- modelSeconds: `430`
- stdoutBytes: `30587`
- stderrBytes: `0`
- artifactPath: `tasks/ul891-set-001-planning.md`
- artifactWritten: `true`

## 6. Fable 2차 planning 결과

- statusCode: `FABLE_READONLY_OUTPUT_READY`
- sessionMode: `REFRESHED_AFTER_DRIFT`
- baselineReused: `false`
- driftStatus: `SOURCE_OR_CONTRACT_CHANGED`
- modelSeconds: `447`
- stdoutBytes: `37828`
- stderrBytes: `0`
- artifactPath: `docs/41-ul891-panel-set-plan.md`
- artifactWritten: `true`

## 7. 구현 종료

- implementationStatus: `EXPERIMENT_COMPLETE`
- implementationReport: `tasks/ul891-set-001-implementation-report.md`
- userValidation: `PENDING — 고정 실험 runtime 사용자 검수`
- commitStatus: `READY`
- mainMergeApprovalCount: `0/3`
- fableCleanupStatus: `FABLE_TASK_SESSION_CLEANED`
- fableSessionsRemoved: `2`
- fableTranscriptsRemoved: `2`
