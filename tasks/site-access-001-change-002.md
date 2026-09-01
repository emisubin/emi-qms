# TASK-SITE-ACCESS-001 Change 002 — migration 번호 교정·최신 main 통합·게시 승인

- instructionChainRead: `true`
- taskType: `P2_REMEDIATION`
- canonicalTaskId: `TASK-SITE-ACCESS-001`
- latestMainBase: `220d1201c9dbb881fb3e5c5061871fb943c7961b`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- implementationApproved: `true`
- publicationApproved: `true`
- commitApproved: `true`
- pushApproved: `true`
- pullRequestApproved: `true`
- remoteMainMergeApproved: `true`
- persistentUatApproved: `false`
- azureDeploymentApproved: `false`
- approvalSource: `USER_EXPLICIT_2026-09-01`

## 실행 대상

- Repository remote: `origin`
- Target branch: `main`
- Publication branch: `feat/task-site-access-001-visit-history`
- 기준 확인 시각: `2026-09-01 17:07 KST`
- 승인 대상: 이 Task의 제품·테스트·종료 문서 diff와 최신 `main` 통합에만 한정한다. 최종 candidate commit SHA와 PR 번호는 Implementation report에 기록한다.
- 승인 allowlist: [Implementation report의 변경 파일](site-access-001-implementation-report.md#변경-파일)에 열거된 제품 코드·migration·테스트·Task 문서, 본 Change 002와 Roadmap 상태 갱신이다. 이 밖의 제품 의미 변경은 승인에 포함하지 않는다.
- 병합 방식: Ready PR의 필수 CI가 통과한 검증 candidate를 GitHub에서 squash merge한다.
- 통합 방식: publication branch의 사이트 접속 candidate commit에 최신 `origin/main`을 merge하고 중첩 파일을 의미 단위로 해결한다. 공개 PR의 최종 병합만 squash를 사용한다.
- Drift gate: 통합·병합 직전 `origin/main` SHA, 전체 migration 목록, Roadmap 추적 `98`, 동일 목적 코드와 중첩 파일을 다시 확인한다. 기준 SHA에서 움직였으면 새 diff를 재평가·재검증하며, `0085` 또는 추적 `98`이 점유됐거나 승인 allowlist 밖 의미 변경이 필요하면 현재 승인을 확대하지 않고 통합을 중단한다.

## Root Finding

최초 사이트 접속 후보는 기준선 `3590c27b7281e77199c8773495e0bafe6311517a`에서 다음 번호였던 `0084_site_access_sessions.sql`을 사용했다. 그러나 현재 공개본과 같은 최신 원격 `main` `220d1201c9dbb881fb3e5c5061871fb943c7961b`에는 이미 G2의 `0084_g2_delivery_target_defect.sql`이 있다. 기존 migration 번호는 재사용하거나 교체할 수 없고, 오래된 기준선의 후보를 그대로 게시하면 최신 G2 기능을 누락할 수 있다.

`0084_site_access_sessions.sql`은 Git 이력·원격 branch·Persistent UAT·운영 Azure에 게시·적용된 적이 없다. local 검증에서는 삭제되는 일회용 PostgreSQL에만 적용됐으므로 기존 migration을 바꾸는 것이 아니라 미게시 후보의 번호를 교정한다. 기준 확인 시점의 `origin/main`에는 `0085`가 없다.

Roadmap 추적 번호도 최초 후보와 공개 G2가 모두 `97`을 사용했다. 공개된 G2 추적 `97`을 보존하고 미게시 사이트 접속 추적 항목은 다음 빈 번호 `98`로 교정한다. Fable planning의 `97` 표기는 당시 원문으로 보존하고 Change 002와 현행 Roadmap·Implementation report가 교정된 번호의 source다.

## 승인 내용

사용자는 사이트 접속 migration을 `0085`로 변경하고 충돌 여부를 모두 확인해 기존 공개 기능과 충돌하지 않도록 원격 `main`까지 병합하라고 명시 승인했다.

사이트 접속의 사용자 직접 화면 검수는 아직 대기지만, 사용자는 이 상태를 확인한 뒤 코드의 Commit·Push·PR·원격 `main` 병합을 별도로 명시 승인했다. 따라서 병합은 사용자 검수 완료를 의미하지 않으며 Persistent UAT·Azure 배포 승인으로 확대되지 않는다.

## 포함 범위

- `0084_site_access_sessions.sql`을 `0085_site_access_sessions.sql`로 변경하고 테스트·SOP·현재 상태 문서의 참조를 동기화
- 최신 원격 `main`의 G2 migration `0084_g2_delivery_target_defect.sql`, 제품 코드, 테스트와 문서를 보존
- 실제 중첩 파일의 양쪽 계약을 의미 단위로 통합하고 충돌 marker·기능 누락을 검사
- fresh·forward migration, Backend·Frontend 전체 회귀, 사이트 접속·G2 격리 Full-Stack, desktop/mobile 증빙 재검증
- 분리된 Codex 독립 검증과 Open P0/P1/P2 `0/0/0` 확인
- Commit, Push, Ready PR, 필수 CI 통과와 동일 head의 원격 `main` 병합

## 통합 충돌 계약

최초 후보와 기준 `main`이 모두 수정한 파일은 다음 세 곳이다.

- `backend/tests/Emi.Qms.Api.Tests/PostgreSqlMigrationTests.cs`: G2 `0084`와 사이트 접속 `0085`가 순서대로 적용되는 기대를 모두 보존한다. 기준 SHA에서는 적용 후 `schema_migrations`의 max가 `0085_site_access_sessions`이며, 이후 migration이 생기면 latest-main 통합에 맞춰 기대를 다시 검토한다.
- `frontend/src/api.ts`: G2 delivery target·defect API와 사이트 접속 signal/end·감사 조회 계약을 모두 보존한다.
- `docs/00-product-roadmap.md`: G2 공개 완료 기록과 사이트 접속 Change 002·현재 Gate를 모두 보존한다.

충돌 marker가 없다는 사실만으로 통합 완료로 판정하지 않는다. 최신 `main`에서 추가된 G2 파일 전체와 migration `0084_g2_delivery_target_defect.sql`, 사이트 접속 allowlist와 migration `0085_site_access_sessions.sql`이 모두 최종 tree에 있는지 확인한다.

## 검증·게시 Gate

아래 항목은 이미 통과했다는 선언이 아니라 원격 게시 전 필수 조건이다.

- migration: `bash scripts/e2e-backend-tests.sh`로 fresh·기존 DB forward 적용과 전체 Backend 회귀가 모두 PASS
- Frontend: `corepack pnpm --dir frontend test`, `typecheck`, `lint`, `build`가 모두 PASS
- 격리 Full-Stack: `bash scripts/e2e-full-stack.sh site-access-audit.full-stack.spec.ts`와 `bash scripts/e2e-full-stack.sh g2-operations.full-stack.spec.ts`가 각각 PASS
- 화면: 최종 통합 tree에서 사이트 접속 Desktop 1440px·Mobile 390px 증빙과 page overflow `0` 재확인
- 정적 검사: `git diff --check`, conflict marker, secret·개인정보 pattern과 최종 변경 allowlist PASS
- 독립 검증: 분리된 Codex session이 최종 candidate commit SHA를 read-only로 검토하고 Open P0/P1/P2 `0/0/0` 판정
- GitHub: PR head SHA가 검증 candidate와 같고 필수 `CI Gate`가 PASS이며 branch가 최신 target과 충돌 없이 mergeable인지 확인한다. 병합 직전 원격 `main`과 migration 목록을 다시 읽고, GitHub가 검증되지 않은 head 또는 stale target의 병합을 제안하면 실행하지 않는다.
- 병합 후: 원격 `main`의 merge SHA·PR 상태를 확인하고 최종 tree에 G2 `0084`, 사이트 접속 `0085`와 양쪽 API·테스트가 모두 남았는지 read-only로 확인한다. 실패 시 force push·직접 main push 없이 중단한다.

검증 명령·수치·증빙, 독립 검증 판정, candidate commit SHA, PR 번호와 최종 merge SHA의 canonical 기록은 [Implementation report](site-access-001-implementation-report.md)다.

## 제외 범위

- Persistent UAT DB handover와 운영 DB migration
- Azure Backend·Frontend 공개배포
- 실제 Entra·Teams·메일 등 외부 provider mutation
- 과거 접속 소급과 기존 사이트 접속 제품 계약 변경
- 원격 또는 local branch·worktree 정리

## 보존 불변조건

- 공개본의 G2 delivery target·defect metric·Home simulation 기능과 migration `0084`
- 기존 Login/Logout, global mutation, authorization audit 의미와 데이터
- `AuthenticatedIdentity` signal, `Audit.Read.All` 조회, append-only 원장과 개인정보 최소화
- 감사 기록 장애가 화면 이동·업무·로그아웃을 차단하지 않는 best-effort 계약
- PR head와 검증된 commit이 다르거나 원격 main에 새 migration 충돌이 생기면 병합하지 않고 재통합·재검증
- Roadmap 순서 변경은 기존 로그인 검수보다 사이트 접속 기능을 먼저 진행하라는 사용자의 명시적 승인으로만 허용되며, Persistent UAT·Azure 배포 선행조건을 우회하지 않는다.
