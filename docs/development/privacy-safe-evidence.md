# Privacy-safe Evidence

## 1. 목적

실제 UAT, DB, browser, Git과 GitHub를 검증하면서 개인정보·업무 원문·credential을 출력하거나 tracked artifact로 남기지 않는 공통 증빙 규칙이다.

## 2. 허용 증빙

가능하면 다음 값만 보고한다.

- boolean
- integer와 aggregate count
- 사전에 정의한 fixed enum 또는 stable failure code
- HTTP status
- commit SHA, branch name과 Repository 내부 file path
- 익명 역할명 또는 `검수 사용자 A/B`
- timestamp 원문 대신 changed/unchanged boolean(원문이 불필요한 경우)

자유 형식 문자열은 필요한 Repository 문서 설명과 마스킹된 synthetic fixture로 제한한다.

## 3. 출력 금지

- 실제 사용자 이름, 회사 이메일/UPN, 사번, 전화번호와 개인 계정명
- 고객·프로젝트·업무·알림 제목/본문과 recipient 원문
- tenant/client/object ID, GUID/UUID와 DB row identifier
- token, password, webhook, Authorization header, connection string과 private key
- raw DB row, raw API/request/response body
- raw DOM, `innerText`, `textContent`, `outerHTML`, accessibility snapshot와 screenshot
- browser console/request message 원문
- Git author/committer와 GitHub actor/reviewer/assignee/participant metadata
- cookie, localStorage와 sessionStorage

이미 노출된 값을 Finding 보고에서 다시 인용하지 않는다.

## 4. Git과 GitHub projection

Git 기준선은 SHA, branch, file path, clean/dirty, staged/unstaged/untracked count와 ahead/behind count만 사용한다. author/committer placeholder가 포함된 `git log`, 기본 `git show`, `git blame`와 `git shortlog`를 증빙용으로 사용하지 않는다.

PR 조회는 다음 필드로 제한한다.

- number, state, isDraft
- mergeStateStatus, mergeable
- baseRefName, headRefName, headRefOid
- changedFiles와 changed-file path
- check status/conclusion count

기본 `gh` table, raw API, PR body/comments/commit metadata는 필요성과 사용자 승인이 없으면 출력하지 않는다.

## 5. Browser projection

실제 UAT browser harness는 route URL 대신 fixed alias를 사용하고 다음 필드만 출력한다.

- status, pageLoaded, expectedStructurePresent
- runtime/banner/diagnostic present boolean
- mutation control/disabled/enabled count
- consoleErrorCount, requestFailureCount
- horizontalOverflowPixels
- blankPage, targetNotFoundPresent
- fixed failureCode

화면 문자열을 확인해야 하면 harness 내부에서 예상 고정 문자열과 비교하고 결과 boolean만 반환한다.

## 6. DB와 runtime projection

- table별 aggregate, status별 count, orphan/invalid candidate count만 기록한다.
- 식별자가 필요한 내부 분석은 출력하지 않고 최종 보고에는 masked alias만 사용한다.
- before/after는 row count, max timestamp changed boolean, container/volume/PID unchanged boolean으로 비교한다.
- shared Development worker의 자연 변화를 분리할 수 없으면 성공으로 표시하지 않는다.

## 7. Output allowlist guard

검증 harness 또는 metadata projection은 출력 전에 allowlist schema를 검증한다. 다음 패턴이 있으면 원문을 폐기하고 stable failure code만 보고한다.

- 비허용 key
- 이메일과 회사 domain
- GUID/UUID와 token-like 긴 문자열
- HTML tag, query string과 multiline 자유 문자열
- 실제 이름으로 보이는 비고정 문자열
- 80자를 초과하는 비허용 문자열

Guard 자체는 synthetic email, GUID, HTML, long token과 free-form string negative fixture로 검증한다. 실제 UAT 값을 negative test에 사용하지 않는다.

## 8. 임시 artifact

- Task가 만든 `/tmp` 또는 test artifact만 ownership을 확인한 뒤 삭제할 수 있다.
- 파일 내용 대신 category, count, tracked/staged 여부만 확인한다.
- screenshot, DOM/console/response dump, test-results와 browser report가 Repository에 tracked/staged되지 않았는지 검사한다.
- ownership이 불명확한 파일, 다른 Task WIP와 Codex transcript는 삭제하지 않는다.

## 9. Finding 처리

PII/secret 또는 raw evidence가 tracked/staged/PR에 포함되면 P2 이상으로 분류하고 게시·merge를 중단한다. 검증 절차에서만 출력된 경우에도 절차 Finding으로 기록하고 projection·guard를 보정한 후 사용자 승인 범위에서 처음부터 gate를 재실행한다.
