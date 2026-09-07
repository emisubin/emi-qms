# TASK-OSAN-ISOLATION-001 Change 003 — 패키징 검사 자원 자동 정리

- taskType: `BUGFIX`
- status: `CODE_REVIEW_CLEARANCE_RUNTIME_VALIDATION_DEFERRED`
- approvalSource: 사용자 “그렇게 해” — 패키징 검사도 생성부터 삭제까지 한 실행에서 책임지도록 보정
- runtimeValidationApprovalSource: 사용자 “승인 시작하라” — 2026-09-06 Change 003 Docker 동적 검증 실행 승인
- approvalDate: 2026-09-06
- gateStatus: `PASS_REUSE`
- roadmapSequenceMatch: true
- productionRuntimeMutationApproved: false
- gitPublicationApproved: false
- parentSessionModel: `GPT-5`
- implementerModelRequested: `gpt-5.6-sol/xhigh`
- implementerModelObserved: `NOT_REPORTED`
- verifierModelRequested: `gpt-6-astra/high`
- verifierModelObserved: `NOT_REPORTED`
- codeReviewStatus: `CODE_REVIEW_CLEARANCE`
- dynamicValidationStatus: `DEFERRED_TO_TASK_OSAN_VALIDATION_001`
- latestRuntimeValidationAttempt: `REJECTED_BEFORE_PROCESS_START_2026-09-06`
- legacyCleanupOwner: `USER_MANUAL_ACTION_PLANNED`
- legacyCleanupResult: `PENDING_USER_REPORT`
- p2RiskAcceptanceSource: 사용자 “나중에 삭제할게. 추적만 해줘.” 및 “아니 이거 자동 정리 안된다고 했잖아.” — 2026-09-06 실행환경에서 불가능한 자동 정리 검증을 추적 항목으로 이관
- p2RiskOwner: `USER_PRODUCT_OWNER`
- p2AcceptedImpact: 정상·실패·TERM에서 wrapper가 자동 cleanup한다는 실제 Docker 증거가 아직 없음
- p2Mitigation: 고유 run ID·owner label·ownership fail-closed 코드와 정적 검사·fresh GPT-6 code review를 유지하고 실제 운영 증거로 사용하지 않음
- p2ReviewPoint: `TASK-OSAN-VALIDATION-001` 격리 통합 검증, Azure·Persistent UAT 개통 전
- p2FollowUpTask: `TASK-OSAN-VALIDATION-001`
- runtimeValidationDirectionModelRequested: `gpt-6-astra/high`
- runtimeValidationDirectionModelObserved: `NOT_REPORTED`

## 확인된 원인

기존 `scripts/test-business-unit-isolation.sh`와 `scripts/test-production-migration-image.sh`가 만든 DB·실행 container·network는 각 trap에서 정상 정리됐다. 남은 `catalog-check` container와 image는 production image의 directory migration 포함 여부를 별도 일회성 명령으로 확인하면서 생성됐고 기존 하네스의 resource scope에 들어가지 않았다.

현재 삭제 명령이 실행되지 않는 직접 원인은 Codex 자동 승인 정책이다. 별도 검사 자원이 하네스 밖에 남을 수 있었던 절차상 원인은 생성·검사·정리를 하나의 소유권 경계로 묶지 않은 것이다.

## 승인된 구현 범위

- 신규 `scripts/test-business-unit-production-image.sh`가 고유 run ID·label을 가진 image와 검사 container를 직접 생성한다.
- production image 안의 business·directory migration catalog를 Repository 원본과 비교하고 기존 `scripts/test-production-migration-image.sh`의 fresh/existing DB 적용 검증을 재사용한다. 이 하위 검사가 만드는 migration container도 같은 run ID·label·cleanup 계약에 포함한다.
- 성공, 명시적 실패와 `INT`·`TERM` 종료에서 trap이 해당 run이 만든 container, image와 임시 파일만 정리하고 잔여 여부를 검사한다.
- 실행 전 같은 이름의 자원이 있으면 덮어쓰거나 삭제하지 않고 중단한다. 다른 Task·사용자 Docker 자원은 정리하지 않는다.
- 실패 주입 검증은 외부 provider와 Persistent UAT를 사용하지 않는 synthetic 전용 경로로 제한한다.

제품 source, 기존 migration, Backend/Frontend, 실제 Azure·Persistent UAT·provider와 Git 게시·병합은 변경하지 않는다. 과거 삭제가 거부된 `emi-qms-osan-isolation-001-catalog-check`와 `emi-qms-osan-isolation-001-test:manifest62`는 차단된 삭제를 새 스크립트로 우회하지 않고 그대로 둔다.

## 변경 allowlist와 검증

- 신규: `scripts/test-business-unit-production-image.sh`
- 최소 보정: `scripts/test-production-migration-image.sh` — wrapper가 전달한 경우에만 migration container에 고유 이름·소유 label을 적용하고 종료 trap에서 회수한다. 기존 단독 실행의 fresh/existing migration 의미는 유지한다.
- 재사용·변경 금지: `scripts/lib/e2e-safety.sh`, `backend/Dockerfile.production`
- Parent 기록: 이 change, Task, implementation report, 상위 Task와 Roadmap의 오산 상태

최소 검증은 shell syntax, static shell 검사, image build·directory catalog exact 비교, packaged business migration fresh/existing 적용, 성공 cleanup과 의도적 실패 cleanup이다. 고유 label 기준 container·image·DB·network·volume·임시 파일 잔여 0을 확인한다. 제품 source가 바뀌지 않으면 Backend·Frontend·전체 Full-Stack은 재실행하지 않는다.

## Docker 동적 검증 Implementation Direction Brief

- canonical Task/change와 승인: `TASK-OSAN-ISOLATION-001 / Change 003`, 사용자 2026-09-06 “승인 시작하라”. 기존 isolated test runtime과 해당 실행이 만든 자원의 자동 정리만 승인한다.
- 현재 상태와 원인: wrapper의 정적 검사와 GPT-6 코드 검토는 통과했지만 Docker 정상·주입 실패·TERM 실행이 자동 승인 검토에서 시작 전에 거부돼 cleanup 동작을 실제로 확인하지 못했다.
- 권장 실행 방식: `/private/tmp/emi-osan-isolation-001`의 현재 feature worktree에서 고유한 안전한 `E2E_RUN_ID`를 실행별로 사용한다. 먼저 syntax·ShellCheck와 관련 Docker preflight를 확인하고, 같은 wrapper를 정상 모드, `after-inspection-container`, `wait-after-inspection-container` 순서로 실행한다. TERM 검증은 signal-ready marker를 확인한 wrapper PID에만 `TERM`을 보내고 다른 process를 종료하지 않는다.
- exact mutation allowlist: wrapper가 해당 run ID로 만든 image, catalog·migration·Compose container, E2E DB, network, volume과 task-owned 임시 폴더의 생성·검사·정리. 기존 legacy `emi-qms-osan-isolation-001-catalog-check`, `emi-qms-osan-isolation-001-test:manifest62`, Persistent UAT, 다른 container·image·network·volume, 제품 source와 Git 상태는 변경하지 않는다.
- 보존 불변조건: 고유 owner/run label과 이름을 확인한 자원만 제거하고, 조회 실패·ownership 불일치·이름 충돌은 성공으로 바꾸지 않는다. 실제 provider를 비활성화하고 secret·connection string 원문을 출력하거나 보고서에 기록하지 않는다.
- 완료 조건: 정상 실행은 build·business/directory catalog·fresh/existing migration evidence가 PASS이고 exit `0`이어야 한다. 주입 실패는 예상 marker와 exit `97`, TERM은 ready marker와 exit `143`이어야 한다. 세 실행 모두 cleanup의 temp/container/image/database/Compose container/network/volume 값이 각각 `0`이고, 종료 후 같은 owner/run label·exact name의 잔여가 `0`이어야 한다.
- 테스트·증거: `/bin/bash -n`, `shellcheck -x`, 세 동적 실행의 종료 코드와 privacy-safe PASS/cleanup projection, 실행 전후 해당 run ID 자원 count, `git diff --check`와 staged `0`을 기록한다. 제품 source가 바뀌지 않으면 Backend·Frontend·Full-Stack은 재실행하지 않고 그 이유를 기록한다.
- 반환 조건: 스크립트 결함이 확인되면 범위 내 최소 보정안을 parent에 먼저 보고하고 같은 Sol이 허용된 두 script만 수정·재검증한다. 다른 runtime·사용자 Docker 자원, provider, Persistent UAT, Azure, Git 게시나 legacy 2개 cleanup이 필요하면 해당 작업을 실행하지 않고 parent에 반환한다.

## 2026-09-06 승인 후 재실행 결과

사용자의 “승인 시작하라”를 근거로 Sol Extra high에 위 Brief와 Docker 실행 범위를 전달했다. 첫 Docker 사전 조회가 process 시작 전에 자동 정책의 `approval required by policy, but AskForApproval is set to Never`로 거부됐다. 우회하지 않았고 정상·주입 실패·TERM 실행, 자원 생성과 cleanup은 모두 시작되지 않았다.

Docker를 제외한 syntax·ShellCheck·diff 검증도 하나의 후속 실행 요청이 같은 정책에 의해 process 시작 전에 거부돼 새 결과를 만들지 못했다. 기존 정적 검사와 GPT-6 코드 검토 결과만 유효하며 동적 검증 P2는 계속 `BLOCKED_TOOL_APPROVAL`이다.

사용자는 legacy 컨테이너와 이미지를 직접 삭제하겠다고 했다. 이 수동 정리는 `PENDING_USER_REPORT`이며 완료로 기록하지 않는다. 두 legacy 자원 삭제는 P3 정리만 해소하고 wrapper의 정상·실패·TERM 자동 cleanup 검증을 대신하지 않는다.

## 사용자 정정과 P2 이관

사용자는 “나중에 삭제할게. 추적만 해줘.”라고 했고, 다음 작업 안내에서 Docker 자동 정리 검증을 다시 선행 Gate로 제시하자 “아니 이거 자동 정리 안된다고 했잖아.”라고 정정했다. 따라서 현재 Codex 실행환경에서 수행할 수 없는 동적 검증을 Task 2의 선행 차단에서 내리고 `TASK-OSAN-VALIDATION-001`의 격리 통합 검증으로 이관한다.

이 결정은 wrapper의 자동 cleanup 성공을 의미하지 않는다. 실제 Docker 정상·실패·TERM 증거가 없다는 P2 영향은 유지하고, 고유 run ID·owner label·ownership fail-closed와 기존 정적·독립 코드 검토를 완화책으로 사용한다. `TASK-OSAN-VALIDATION-001`에서 실행 가능한 환경을 사용해 재검토하며 Azure·Persistent UAT 개통 전에는 반드시 결과를 확정한다. Legacy 두 자원의 사용자 수동 삭제는 별도 P3로 계속 추적한다.

## 구현 결과

- 신규 wrapper가 canonical E2E Compose 파일, 검증된 run ID와 고유 이름을 사용해 image·catalog 검사 container·fresh/existing migration container·임시 폴더를 한 실행 범위로 묶는다.
- image와 container는 생성 시도 전에 cleanup 대상으로 등록한다. 실제 삭제 직전에는 owner와 run ID label을 다시 검사하며, 이름 충돌이나 소유권 불일치는 삭제하지 않고 실패한다.
- Docker 조회 실패는 잔여 `0`으로 바꾸지 않고 `UNKNOWN`과 non-zero cleanup으로 처리한다. Label 조회와 exact-name 조회의 full ID 합집합으로 label 누락이나 중복 집계도 확인한다.
- 기존 production migration 검사는 wrapper ownership context가 있을 때만 두 migration container에 고유 이름·label·EXIT/INT/TERM cleanup을 적용한다. Standalone fresh/existing 적용과 기존 출력 세 줄은 유지한다.
- production image의 business·directory migration 디렉터리는 Repository 원본과 재귀 비교한다.

## 검증 결과와 Finding

macOS `/bin/bash` 3.2 syntax, `shellcheck -x`, `git diff --check`는 통과했고 두 스크립트 mode는 `755`, staged 파일은 0이다. 제품 source가 바뀌지 않아 Backend·Frontend·Full-Stack은 다시 실행하지 않았다.

Fresh GPT-6 High 최종 read-only 검토는 생성 전 claim, 조회 실패 `UNKNOWN`, child container 회수, canonical Compose 고정, Bash 3.2 빈 배열, residual full ID의 여섯 Finding이 모두 코드에서 해소됐다고 판정했다. 최종 판정은 `CODE_REVIEW_CLEARANCE`이며 새 P0/P1/P2/P3는 없다. 검토자는 두 변경 스크립트와 safety/Dockerfile 4파일 digest 전후 동일을 확인했지만 전체 worktree manifest는 자동 승인 검토가 거부해 확인하지 못했다.

실제 Docker 정상 실행·명시적 실패·TERM cleanup과 최종 잔여 0 검증은 첫 실행이 자원을 만들기 전에 자동 승인 검토에서 `approval required by policy, but AskForApproval is set to Never`로 거부되어 미실행이다. 같은 삭제를 다른 명령·API로 우회하지 않았다. 따라서 코드 검토는 통과했지만 Change 003의 자동 검증 완료는 보류한다. 기존 legacy container와 image 2개도 그대로 보존한다.
