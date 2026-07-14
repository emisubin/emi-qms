# TASK-BACKEND-FORMAT-001 Planning — Backend import-order baseline 정리

## 1. 상태와 Task 분류

- Task ID: `TASK-BACKEND-FORMAT-001`
- Task 유형: `HOUSEKEEPING`
- planningDrafted: true
- planningApproved: true
- implementationApproved: true
- Fable 5 호출: false
- 사용자 우선순위 변경: 승인됨
- 구현: 완료
- 사용자 검수·Commit·Push·PR·Merge: 승인됨

이 Task는 기존 Backend C# 파일의 `using` 순서를 Repository `.editorconfig`와 일치시키는 형식 정리다. 신규 사용자 기능, 정책, API, DB, runtime 또는 업무 동작을 추가하지 않는다.

## 2. Task Identity Gate

- proposedTaskId: `TASK-BACKEND-FORMAT-001`
- taskType: `HOUSEKEEPING`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapExpectedTaskId: `TASK-GOV-HISTORY-REWRITE-001`
- roadmapNextGate: `TASK-GOV-FINDING-GATE-001`
- roadmapSequenceMatch: false
- samePurposeMatchCount: 0
- canonicalTaskId: `TASK-BACKEND-FORMAT-001`
- reuseExistingTask: false
- explicitRoadmapOverrideApproved: true
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 전체 Backend format 검사를 실패시키는 기존 import-order 9건을 제거한다.
- Root Finding: Repository baseline의 `dotnet format --verify-no-changes`가 `IMPORTS=9`로 exit 2를 반환한다.
- 변경·검증 경계: 확인된 C# 9개 파일의 `using` 순서와 관련 검증·Task 문서만 다룬다.
- 보존할 불변조건: 실행 코드, API, DB, migration, dependency, runtime, 권한과 업무 정책은 바꾸지 않는다.
- 예상 산출물: format-clean Backend diff, 자동 검증, Task 종료 5종 산출물과 Roadmap 상태 갱신.

동일 목적 Task·branch·worktree·open PR은 확인되지 않았다. 현재 Roadmap의 선행 P2는 GitHub Support 외부 회신을 기다리고 있지만, 사용자가 2026-07-14 이 P3 format debt를 먼저 처리하도록 명시적으로 순서를 변경했다. 사용자는 planning 검토 뒤 권장안 A 전체 구현을 승인했고, 자동·독립 검증 결과를 확인한 뒤 사용자 검수와 Commit·Push·PR·Merge를 승인했다.

## 3. 확인된 문제

최신 `origin/main`에서 전체 format 검사를 읽기 전용으로 재현했다.

- formatExitCode: 2
- diagnosticCategory: `IMPORTS`
- diagnosticCount: 9
- affectedFileCount: 9
- trackedDiffAfterProbe: 0
- untrackedArtifactAfterProbe: 0

Repository `.editorconfig`는 system namespace를 먼저 정렬하도록 설정돼 있지만, 아래 파일의 `using` 순서가 현재 기준과 다르다.

## 4. 영향 파일

구현 allowlist는 다음 9개 C# 파일로 제한한다.

- `backend/src/Emi.Qms.Api/Authorization/EntraClaimsTransformation.cs`
- `backend/src/Emi.Qms.Api/Calendar/AdminCalendarHolidayStore.cs`
- `backend/src/Emi.Qms.Api/DatabaseMigrationRunner.cs`
- `backend/src/Emi.Qms.Api/Identity/DbIdentityStore.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryEndpointExtensions.cs`
- `backend/src/Emi.Qms.Api/Notifications/NotificationDeliveryStore.cs`
- `backend/tests/Emi.Qms.Api.Tests/PanelInformationApiTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`
- `backend/tests/Emi.Qms.Api.Tests/QmsWebApplicationFactory.cs`

Planning 단계의 문서 allowlist는 이 파일과 `docs/00-product-roadmap.md`다. 구현 종료 시 필요한 Task 산출물은 12장의 문서 전략을 따른다.

## 5. Root cause

기존 Task들이 각자 변경 파일의 format 위반 0만 확인하고 범위 밖 파일을 수정하지 않으면서, 과거에 누적된 9개 import-order 차이가 Repository baseline에 남았다. 이는 코드 동작 결함이 아니라 canonical formatter와 현재 파일 순서의 drift다.

다음은 원인이 아니다.

- 잘못된 namespace 또는 누락된 dependency
- API·authorization·notification 동작 오류
- migration·runtime 설정 문제
- 사용되지 않는 `using` 추가·삭제 요구

## 6. 대안 비교

| 대안 | 방법 | 장점 | 위험 | 판정 |
| --- | --- | --- | --- | --- |
| A. Formatter를 9개 파일에 한정 | Repository `dotnet format`을 allowlist 파일에만 적용하고 diff guard 수행 | canonical 도구와 일치하고 재현 가능 | 다른 형식 변경이 섞이지 않는지 diff 검증 필요 | **권장** |
| B. 수동 재정렬 | 각 `using` 블록을 사람이 직접 정렬 | 변경을 눈으로 통제하기 쉬움 | 오정렬·누락과 재실행 실패 가능 | 비권장 |
| C. `.editorconfig` 완화 | import 정렬 규칙을 비활성화 | 현재 오류가 사라짐 | Repository 표준을 약화하고 debt를 숨김 | 제외 |

권장안은 A다. Formatter 실행 뒤 각 파일의 `using` 집합이 동일하고 순서만 달라졌는지 검증한다. Formatter가 9개 외 파일 또는 `using` 블록 밖을 바꾸면 적용하지 않고 원인을 조사한다.

## 7. 구현 계획

구현은 승인 후 분리된 Codex 세션에서 수행한다.

1. 최신 instruction chain, `origin/main`, Task branch/worktree와 동일 목적 작업을 다시 확인한다.
2. 전체 format probe가 `IMPORTS=9`, 영향 파일 9개인지 재확인한다.
3. Repository formatter를 4장의 정확한 allowlist에만 적용한다.
4. changed files가 9개인지 확인한다.
5. 각 diff hunk가 파일 상단 `using` 순서 변경에만 해당하는지 확인한다.
6. 파일별 `using` 항목의 추가·삭제가 0인지 확인한다.
7. 전체 format verify가 exit 0, diagnostic 0인지 확인한다.
8. 10장의 자동 검증을 수행한다.
9. 5종 종료 산출물과 Roadmap을 실제 결과로 갱신한다.
10. 분리된 Codex 세션에서 diff와 검증을 독립 확인한다.
11. 사용자 검수 후 별도 승인 범위에서만 commit·push·Draft PR·Ready·merge를 수행한다.

## 8. 포함 범위

- 확인된 Backend C# 9개 파일의 import ordering
- full-solution format baseline 복구
- Backend build와 전체 tests
- Repository 표준 CI 확인
- Task 종료 문서와 Roadmap 상태 갱신
- 독립 Codex 검증

## 9. 제외 범위

- namespace, type, method, statement 또는 test assertion 변경
- unused import 추가·삭제를 위한 별도 리팩터링
- `.editorconfig` 변경
- Frontend·migration·dependency·lockfile·script 변경
- API·DB·runtime·Persistent UAT 변경
- Development·Review-safe·Candidate 재시작
- Git history, Support ticket, backup과 기존 WIP 정리
- 신규 기능 또는 다른 P3 debt 처리

## 10. 검증 계획

### 10.1 형식·diff guard

- `dotnet format backend/Emi.Qms.sln --verify-no-changes --no-restore`: exit 0
- format diagnostic count: 0
- changed C# file count: 9
- allowlist 밖 변경: 0
- `using` 추가·삭제: 0
- `using` 블록 밖 source 변경: 0
- `git diff --check`: PASS

### 10.2 Backend 회귀

- Backend Release build
- Backend 전체 tests
- 영향 파일이 포함된 기존 authorization·calendar·migration·identity·notification·panel·procurement tests 결과 확인

별도의 신규 동작 테스트는 추가하지 않는다. 변경이 import 순서뿐이라는 diff guard와 기존 전체 tests로 검증한다.

### 10.3 게시 전 검증

- Frontend lint·typecheck·unit·build
- isolated Full-Stack E2E
- actionlint
- Markdown link·anchor·heading
- secret/PII와 generated artifact scan
- changed-file allowlist와 삭제 파일 0
- GitHub Backend·Frontend·Full-Stack E2E CI success

Persistent UAT snapshot과 browser smoke는 실행 코드와 runtime artifact가 바뀌지 않는 순수 import-order Task이므로 `N/A`로 계획한다. 실제 diff가 이 전제를 벗어나면 N/A 판정을 취소하고 Task를 중단한다.

## 11. 완료 기준

- 전체 format command exit 0
- `IMPORTS` diagnostic 0
- 정확히 9개 파일의 import order만 변경
- `using` 집합과 실행 코드는 불변
- Backend Release build와 전체 tests 성공
- 게시 전 전체 검증과 CI 성공
- Migration·API·Frontend·dependency·runtime configuration 변경 0
- Persistent UAT write와 runtime restart 0
- 신규 P0/P1/P2 0
- 독립 Codex 검증 완료
- 사용자 검수 대기 상태의 Draft PR 준비

## 12. 문서·Git 전략

Planning 위치:

- `tasks/backend-format-001-planning.md`

구현 종료 시 5종 산출물:

- `tasks/backend-format-001.md`
- `tasks/backend-format-001-implementation-report.md`
- `tasks/backend-format-001-sop.md`
- `tasks/backend-format-001-user-manual.md` — 사용자 기능 변화가 없어 N/A 사유와 검수 checklist만 기록
- `docs/00-product-roadmap.md`

Branch:

- `fix/task-backend-format-001-import-order`

권장 commit:

- `style: normalize backend import order`

게시 단계는 별도 사용자 승인 전 수행하지 않는다. Force push, rebase, main 직접 push와 기존 worktree 정리를 하지 않는다.

## 13. Rollback

9개 파일의 import-order diff와 Task 문서 변경만 revert하면 된다. DB, migration, runtime, backup 또는 data rollback은 없다.

## 14. 사용자 승인 필요 항목

- 권장안 A: formatter를 정확한 9개 파일에 한정 적용
- 10장의 게시 전 전체 검증 수행
- 5종 종료 산출물 작성
- 구현 후 분리된 Codex 독립 검증

현재 사용자 승인 상태:

- Planning 작성: true
- 우선순위 변경: true
- Implementation: true
- Commit·Push·PR: true
- Ready·Merge: true

## 15. Go/No-Go

- Planning: `APPROVED`
- Implementation: `AUTOMATED_VALIDATION_COMPLETE`
- Independent validation: `COMPLETE`
- User validation: `COMPLETE`
- Runtime·Persistent UAT: `NO_CHANGE`
- 전체 신규 기능: 기존 history P2와 Finding gate가 닫힐 때까지 `NO_GO`
