# TASK-GOV-002 Planning — Git history 개인정보 risk decision

## 1. 상태와 분류

- Task ID: `TASK-GOV-002`
- Task 유형: `POLICY_DECISION`
- 기획 경로: Codex-only, Fable 5 미사용
- 기준 main: `1c7ca6a79726`
- planningApproved: true
- implementationApproved: true
- approvedPolicy: `COORDINATED_HISTORY_REWRITE`
- riskOwner: `Repository owner / security owner`
- historyRewriteApproved: false
- forcePushApproved: false
- repositoryVisibilityChangeApproved: false
- riskAcceptanceApproved: false
- Persistent UAT·runtime 영향: 없음

이 문서는 정책 결정을 위한 계획이다. History rewrite, repository visibility 변경, force push, branch·tag 변경과 개인정보 원문 열람·출력은 승인하지 않는다.

## 2. 문제와 목적

Current checkout은 비식별화됐지만 과거 Git commit의 두 문서에는 실제 사용자 이름이 남아 있다. Repository가 public이므로 Git history를 조회할 수 있는 사용자는 과거 값을 복원할 수 있다.

목적은 다음 두 정책 중 하나를 근거와 책임 경계가 완전한 상태로 결정하는 것이다.

1. 과거 값을 모든 published ref에서 제거하는 coordinated history rewrite
2. Rewrite하지 않고 P2 위험을 명시적으로 수용하는 risk acceptance

Current checkout 비식별화 완료를 history 정리 완료로 간주하지 않는다. 반대로 rewrite를 수행해도 이미 생성된 외부 clone·download를 회수했다고 표현하지 않는다.

## 3. 확인된 기준선

| 항목 | Read-only 확인값 |
| --- | --- |
| Repository visibility | `PUBLIC` |
| GitHub fork count | 0 |
| GitHub에서 열거 가능한 과거 clone 수 | 확인 불가 |
| Current checkout exact match | 0 |
| 기존 비식별화 보고의 실제 이름 | 46 lines, 51 occurrences |
| `origin/main` 영향 | 1 commit, 2 files |
| 전체 origin remote ref | 18 |
| 영향 history를 포함하는 origin remote ref | 15 |
| local branch | 20 |
| 영향 history를 포함하는 local branch | 19 |
| tag | 0 |
| open PR | 0 |
| 기존 Finding 범주의 confirmed secret | 0 |
| 기존 Finding 범주의 confirmed email·UPN·tenant/client/object ID | 0 |

영향 원문, 실제 이름, commit author·committer와 개인 GitHub metadata는 이 문서에 기록하지 않는다. 영향 commit·file count는 과거 비식별화 diff의 제거 line을 memory-only exact matcher로 재검산했다.

GitHub fork count 0은 외부 복제본 0을 의미하지 않는다. Clone, archive download, CI cache, local bundle과 화면 capture는 GitHub Repository metadata만으로 완전하게 열거할 수 없다.

Main protection API 상태는 권한 부족과 미설정을 구분해 확정하지 못했다. Rewrite 실행 계획에서는 실제 ruleset·branch protection·force-push 허용 경계를 별도로 확인해야 한다.

## 4. 보호할 불변조건

- 개인정보 원문을 terminal, tracked 문서, PR body, issue, CI log와 검증 보고에 다시 출력하지 않는다.
- Current checkout의 비식별화 상태를 유지한다.
- Rewrite를 선택해도 main과 모든 published branch에서 원문 exact match가 0이어야 한다.
- Rewrite mapping과 pre-rewrite backup은 Repository 밖의 승인된 보안 위치에만 둔다.
- 일부 ref만 rewrite한 혼합 상태를 완료로 판정하지 않는다.
- History rewrite 중 일반 merge와 push를 차단하는 maintenance 경계를 둔다.
- Rewrite 뒤 기존 clone은 안전한 새 history로 자동 전환됐다고 간주하지 않는다.
- Risk acceptance는 P2 Finding을 조용히 닫는 수단이 아니며 canonical 필수 기록을 모두 충족해야 한다.
- Runtime, Persistent UAT, migration, dependency와 product source는 변경하지 않는다.

## 5. 위험 평가

### 5.1 확인된 위험

- Repository가 public이므로 과거 commit과 blob에 대한 접근 가능성이 현재 존재한다.
- 영향은 실제 사용자 이름이며 current checkout에서 이미 제거됐다.
- Origin의 다수 branch가 같은 과거 history를 포함해 main만 rewrite하면 원문 blob이 계속 reachable할 수 있다.
- Local branch와 worktree가 많아 rewrite 뒤 잘못된 push로 old history가 재유입될 가능성이 있다.

### 5.2 확인할 수 없는 위험

- 과거 clone·archive download 수
- 외부 CI cache, mirror와 개인 backup 존재 여부
- GitHub cache에서 old commit/blob가 제거되는 정확한 시점
- 실제 이름 당사자의 공개 동의 또는 조직 개인정보 보존 정책

### 5.3 현재 분류

- Finding: `GIT_HISTORY_PERSONAL_DATA_REMAINS`
- Severity: `P2`
- Exposure surface: `PUBLIC_HISTORY`
- Credential compromise: false
- Current checkout exposure: false
- Historical exposure: true
- Decision owner: `Repository owner / security owner`

## 6. 대안 비교

| 대안 | 과거 GitHub 접근 경로 감소 | 외부 clone 회수 | Git 영향 | 운영 복잡도 | P2 종료 조건 |
| --- | --- | --- | --- | --- | --- |
| A. `COORDINATED_HISTORY_REWRITE` | 높음 | 불가 | 모든 descendant SHA·영향 ref 변경 | 높음 | 전체 published ref 검증, 재동기화와 사용자 승인 |
| B. `ACCEPT_WITH_CONTROLS` | 없음 | 불가 | 없음 | 낮음 | risk owner·근거·영향·완화·재검토·후속 Task 승인 |
| C. `MAKE_PRIVATE_ONLY` | 향후 익명 접근 감소 | 불가 | Git SHA 변화 없음 | 중간 | 단독으로 P2 해결 아님. A 또는 B와 결합 필요 |
| D. `DELETE_AND_RECREATE_REPOSITORY` | 높음 | 불가 | Repository identity·integration 전체 단절 | 매우 높음 | 권장하지 않음 |

### 6.1 A — Coordinated history rewrite

장점:

- GitHub에서 현재 reachable한 main·branch history의 개인정보 원문을 제거할 수 있다.
- Current checkout과 published history의 비식별화 계약이 일치한다.

위험:

- 영향 commit 이후 모든 descendant SHA가 바뀐다.
- 15개 origin ref와 19개 local branch의 정리·대체 정책이 필요하다.
- 오래된 clone·branch가 force push되면 제거한 history가 재유입될 수 있다.
- CI link, 문서의 short SHA, open/closed PR reference와 audit link가 끊길 수 있다.
- Secure pre-rewrite backup 자체가 개인정보 보유물이 된다.

### 6.2 B — Accept with controls

장점:

- SHA, branch, worktree와 downstream clone을 중단하지 않는다.
- Rewrite 실수와 old-history 재유입 위험이 없다.

위험:

- Public history에서 과거 이름을 계속 조회할 수 있다.
- Repository가 public인 동안 접근 표면이 유지된다.
- 당사자 요청, 조직 정책 또는 공개 범위 변경 시 다시 결정해야 한다.

필수 기록:

- 승인된 risk owner
- 수용 근거
- 영향과 예상 노출 범위
- 현재 완화책
- 재검토 날짜 또는 event trigger
- 후속 Task ID
- 사용자 명시 승인

### 6.3 C — Make private only

Visibility 변경은 즉시 노출 표면을 줄이는 containment가 될 수 있지만 이미 복제된 history와 GitHub 내부 old object를 제거하지 않는다. 단독 완료안으로 사용하지 않고 A 실행 전 containment 또는 B의 완화책으로만 검토한다.

### 6.4 D — Delete and recreate

Issue·PR·Actions·integration·Repository identity와 audit continuity 손실이 커서 이 Finding 규모에 비례하지 않는다. A를 기술적으로 수행할 수 없고 별도 조직 결정이 있는 경우에만 다시 검토한다.

## 7. 권장안

권장 정책은 `A. COORDINATED_HISTORY_REWRITE`다.

근거:

- Repository가 현재 public이다.
- Confirmed personal data가 origin main history에 남아 있다.
- Fork count가 0이고 open PR이 0인 현재가 향후보다 rewrite 영향 조정이 쉽다.
- Current checkout은 이미 비식별화돼 replace 기준을 고정할 수 있다.

다만 A의 실제 실행은 이 Task의 정책 승인만으로 시작하지 않는다. `TASK-GOV-HISTORY-REWRITE-001`을 별도 `HOUSEKEEPING` 또는 `SECURITY_HARDENING` 실행 Task로 만들고 maintenance, visibility, secure backup, all-ref force push, contributor 재동기화와 GitHub cache 처리 승인을 각각 받아야 한다.

사용자가 조직 정책상 실제 이름의 과거 공개 위험을 수용하려면 B를 선택할 수 있다. 이 경우 canonical P2 risk acceptance 필드가 하나라도 빠지면 `TASK-GOV-002`는 완료되지 않는다.

## 8. TASK-GOV-002 포함 범위

- History 개인정보 범주와 영향 commit·file·ref aggregate 재검증
- Repository visibility, fork, branch, tag와 open PR projection
- Clone·mirror·cache의 확인 가능 범위와 한계 기록
- A/B/C/D 비교
- Risk owner와 정책 선택 기록
- 선택 결과의 완화책, 재검토 trigger와 후속 Task 확정
- 5종 종료 산출물과 Roadmap Decision Log 갱신
- 독립 Codex read-only 검증 계획

## 9. TASK-GOV-002 제외 범위

- `git filter-repo` 실행
- History rewrite와 commit/blob 삭제
- Main·branch·tag force push
- Repository visibility 변경
- Branch protection·ruleset 변경
- Branch, worktree, clone, backup과 Actions artifact 정리
- GitHub Support 요청
- Runtime, DB, migration, dependency와 product source 변경
- `TASK-GOV-HISTORY-REWRITE-001` 실행
- `TASK-NOTIFY-004` 조사 또는 신규 기능 시작

## 10. 정책 결정 절차

### Phase A — Read-only inventory

1. Main, origin/main과 worktree clean 상태를 확인한다.
2. Current checkout exact match 0을 확인한다.
3. Published main·branch·tag별 영향 count를 재검산한다.
4. Visibility, fork, open PR, ruleset·protection 확인 가능성을 기록한다.
5. Actual values는 private in-memory matcher로만 검사하고 출력하지 않는다.

### Phase B — Risk owner decision

사용자가 다음을 결정한다.

- risk owner 역할
- Repository를 public으로 유지할지 여부
- A rewrite 또는 B risk acceptance
- A 선택 시 visibility containment 필요 여부
- B 선택 시 재검토 trigger와 후속 Task

### Phase C — Decision documentation

- 선택 정책과 근거를 Task, Implementation report와 Roadmap Decision Log에 기록한다.
- SOP에는 선택 정책의 운영 절차와 금지사항을 기록한다.
- User manual은 product UI 변경 `N/A`와 Repository 사용자 조치를 설명한다.
- User validation checklist는 정책 선택과 영향 이해를 분리해 확인한다.

### Phase D — Independent verification

분리된 Codex 검증 세션이 raw metadata를 출력하지 않고 다음을 확인한다.

- 근거 count와 visibility 상태
- 선택안과 canonical P2 gate의 일치
- 승인되지 않은 rewrite·force push·visibility change 0
- 5종 산출물 상태·링크
- secret/PII와 raw Git metadata 노출 0

## 11. Rewrite 선택 시 후속 실행 Task

후속 Task ID: `TASK-GOV-HISTORY-REWRITE-001`

### 11.1 실행 전 승인

- Repository maintenance 시작
- 일반 push·merge freeze
- Repository visibility 변경 여부
- Private secure mirror/bundle 생성과 보존 기간
- `git filter-repo` replace mapping 사용
- Main과 영향 remote branch force push
- Tag rewrite가 필요한 경우 tag force push
- Branch protection·ruleset 임시 변경과 복원
- GitHub Support cache purge 요청 여부
- 모든 contributor·automation의 old clone 폐기와 re-clone
- 완료 후 secure backup 삭제 또는 제한 보존

### 11.2 권장 기술 경계

- Fresh task-owned mirror clone에서만 실행한다.
- Replacement mapping은 Repository 밖 mode 0600 파일로 만들고 commit하지 않는다.
- Current checkout의 익명 표현을 canonical replacement로 사용한다.
- Main만이 아니라 영향이 있는 모든 published ref를 같은 실행에서 rewrite한다.
- Rewrite 전후 commit/file/ref count는 aggregate로 비교한다.
- Old commit SHA와 actual value는 console·문서·PR에 출력하지 않는다.
- Partial force push가 발생하면 일반 개발을 재개하지 않는다.

### 11.3 Rewrite 성공 기준

- Current checkout match 0
- Origin main history match 0
- 모든 origin branch history match 0
- Tag history match 0
- GitHub web에서 known old commit/blob 접근 불가 또는 Support 확인
- Open PR·branch protection·Actions 정상화
- Fresh clone validation 성공
- 기존 clone 재사용 금지 공지 완료
- Pre-rewrite backup 보존·삭제 상태 승인대로 일치
- Raw mapping·log artifact tracked/staged 0

## 12. Risk acceptance 선택 시 필수 통제

- Risk owner를 역할로 명시한다.
- Repository public 유지 근거를 기록한다.
- 실제 이름 이외의 email·ID·secret 노출이 없음을 다시 검증한다.
- Current checkout PII scan을 게시 gate로 유지한다.
- 새 PII가 발견되면 즉시 게시 중단하는 output guard를 유지한다.
- Repository visibility 확대, 외부 공개, 당사자 삭제 요청 또는 보안 정책 변경을 재검토 trigger로 둔다.
- 재검토 Task ID와 목표 시점을 기록한다.
- Risk acceptance가 old clone 회수를 의미하지 않음을 기록한다.

## 13. Rollback과 장애 경계

정책 문서 작성은 문서 commit revert로 되돌릴 수 있다. 실제 rewrite는 일반 rollback을 사용하지 않는다.

| 상황 | 자동 조치 | 사용자 승인 필요 | 금지 |
| --- | --- | --- | --- |
| Inventory count 불일치 | 정책 결정 중단, private evidence 폐기 | 범위 재확정 | 추정 완료 |
| Raw PII console 노출 | 출력 guard 보정 후 처음부터 재실행 | 게시 재승인 | 원문 재인용 |
| Partial force push | Maintenance 유지, 영향 ref 격리 | forward completion 또는 old history 복구 결정 | 일반 merge 재개 |
| Old branch 재유입 | 해당 ref push 차단 | ref 삭제·재rewrite 승인 | 무단 branch 삭제 |
| Rewrite 후 CI 실패 | New history 유지, 원인 조사 | forward-fix 또는 명시적 history 복구 | 자동 old-history restore |
| GitHub cache 잔존 | 공개 접근 제한 검토 | Support 요청·visibility 변경 | 완료 과장 |

Old history를 restore하면 개인정보를 다시 공개할 수 있으므로 secure backup restore는 자동 rollback이 아니다.

## 14. 검증 계획

### TASK-GOV-002 정책 문서 검증

- `git diff --check`
- Markdown heading·local link·anchor 검사
- Secret/PII scan
- Raw Git author/committer·actual identity 출력 0
- 변경 파일 allowlist
- Backend·Frontend·migration·dependency·script diff 0
- Runtime·Persistent UAT 변화 0
- 기존 Decision Log 행 수정·삭제 0
- 5종 산출물 상태·위치 추적
- 독립 Codex read-only review

### 후속 rewrite Task 검증

- Fresh mirror preflight
- Replacement matcher qualification
- All-ref before/after aggregate
- Current checkout·main·remote branch·tag exact match 0
- Fresh clone build 또는 문서-only smoke
- Open PR, ruleset, branch protection와 Actions 상태
- Raw artifact retained/tracked/staged 0
- Old clone 재유입 방지 rehearsal

Product code가 바뀌지 않으므로 Backend·Frontend 전체 테스트는 TASK-GOV-002 정책 결정 단계에서 `N/A`다. Rewrite 실행 단계에서는 새 history의 tree가 rewrite 전 current tree와 동일한지 tree digest로 확인하고 CI 범위를 별도 승인한다.

## 15. 예상 영향 파일과 산출물

Planning 단계 변경 파일:

- `tasks/gov-002-planning.md`

승인 후 TASK-GOV-002 예상 5종 산출물:

- `tasks/gov-002.md`
- `tasks/gov-002-implementation-report.md`
- `tasks/gov-002-sop.md`
- `tasks/gov-002-user-manual.md`
- `docs/00-product-roadmap.md`

User manual의 product UI 변경은 `N/A`이며 Repository 사용자가 선택 정책과 재동기화 영향을 이해하는 checklist를 포함한다.

## 16. 완료 기준

- 영향 범주와 aggregate가 원문 노출 없이 검증됨
- Repository public 범위와 clone 확인 한계가 기록됨
- 승인된 risk owner가 지정됨
- A 또는 B가 사용자에게 명시적으로 선택됨
- A 선택 시 별도 rewrite Task와 승인 matrix가 확정됨
- B 선택 시 canonical P2 risk acceptance 필드가 모두 기록됨
- 5종 종료 산출물의 상태·위치가 추적됨
- 독립 Codex 검증 통과
- 승인되지 않은 rewrite·force push·visibility change 0
- 신규 P0/P1/P2 0
- 사용자 검수 전 Draft, 별도 승인 전 Ready·merge 금지

## 17. 중단 조건

- Confirmed secret, credential, email·UPN, tenant/client/object ID가 새로 발견됨
- Current checkout에 actual PII가 다시 존재함
- 영향 ref를 aggregate로 확정할 수 없음
- Raw identity를 출력하지 않고 검사할 수 없음
- Repository visibility 또는 branch protection 상태가 정책 선택을 바꿀 정도로 불명확함
- Risk owner를 지정할 수 없음
- A 선택 시 all-ref rewrite·re-clone 조정 권한이 없음
- B 선택 시 canonical risk acceptance 필드를 충족할 수 없음
- 범위 밖 product code·runtime·DB 변경 필요
- 신규 P0/P1/P2 발견

중단 시 rewrite, force push, visibility 변경, branch/tag 삭제, commit·push·PR을 수행하지 않는다.

## 18. 사용자 결정 항목

사용자는 2026-07-13에 권장안 A `COORDINATED_HISTORY_REWRITE`를 승인했다. Risk owner는 기존 governance 기준의 역할인 `Repository owner / security owner`로 확정한다.

확정되지 않은 실행 항목은 다음과 같다.

1. Rewrite 전 repository private containment 여부
2. Maintenance·force push·secure backup·re-clone 공지의 별도 승인 방식
3. Branch protection·ruleset 변경과 GitHub Support 요청 여부
4. Secure pre-rewrite backup의 보존 기간과 삭제 승인

정책 선택은 완료됐지만 실제 rewrite는 별도 `TASK-GOV-HISTORY-REWRITE-001` 계획과 사용자 승인을 받은 뒤에만 수행한다.
