# TASK-GOV-HISTORY-REWRITE-001 Planning — Public Git history 개인정보 coordinated rewrite

## 1. 상태와 승인 경계

- Task ID: `TASK-GOV-HISTORY-REWRITE-001`
- Task 유형: `SECURITY_HARDENING`
- 작업 경로: Codex-only, Fable 5 미사용
- 기준 main: `3aacf2a54e02`
- branch: `fix/task-gov-history-rewrite-001-coordinated-rewrite`
- planningApproved: true
- implementationApproved: true
- maintenanceApproved: true
- temporaryPrivateApproved: true
- secureBackupApproved: true
- backupRetentionApproved: true
- historyRewriteApproved: true
- forcePushApproved: true
- protectionChangeApproved: false
- supportRequestApproved: true
- contributorRecloneApproved: true
- publicReopenApproved: false
- cleanupApproved: false
- historyRewriteExecuted: true
- repositoryVisibility: `PRIVATE`
- supportCacheStatus: `REMOVED`
- docsPublicationApproved: true
- Persistent UAT·runtime 변경 승인: false

이 문서는 `TASK-GOV-002`에서 선택한 `COORDINATED_HISTORY_REWRITE`의 실행 계획이다. 이 planning 승인만으로 repository visibility 변경, backup 생성, `git filter-repo`, force push, GitHub Support 요청, clone·worktree 정리와 public 재전환을 수행하지 않는다.

## 2. 문제와 완료 목표

Current checkout의 두 문서는 비식별화됐지만 public Git history에는 과거 실제 사용자 이름이 남아 있다. `origin/main`만 바꾸면 다수 published branch가 이전 history를 계속 reachable하게 만들므로 모든 영향 published ref를 같은 maintenance 경계에서 처리해야 한다.

완료 목표는 다음과 같다.

- Current checkout과 모든 published branch·tag history의 private exact matcher 결과가 0이다.
- 영향 ref만 고정된 pre-rewrite SHA를 기준으로 원자성에 가깝게 순차 갱신하고, partial 상태에서는 개발을 재개하지 않는다.
- 현재 published tip tree는 rewrite 전후 동일하다.
- GitHub cached view·PR ref와 외부 clone은 force push만으로 제거됐다고 표현하지 않는다.
- 기존 clone·worktree·automation이 old history를 다시 push할 수 없게 격리하고 fresh clone으로 전환한다.
- Repository protection·ruleset, CI와 visibility를 승인된 상태로 복원한다.
- Runtime, Persistent UAT, migration, source content와 product behavior는 변경하지 않는다.
- P2 `GIT_HISTORY_PERSONAL_DATA_REMAINS`는 all-ref·cache·re-clone 검증과 사용자 검수 전까지 Open으로 유지한다.

## 3. 실제 기준선

### 3.1 Git·GitHub

| 항목 | Read-only 확인값 |
| --- | --- |
| Local main / origin main | `3aacf2a54e02` / `3aacf2a54e02` |
| Main worktree | clean |
| Repository visibility | `PUBLIC` |
| Fork / open PR / tag | `0 / 0 / 0` |
| Collaborator | 1 |
| GitHub workflow | 1 |
| Remote branch ref | 19 |
| 개인정보 history 영향 remote ref | 16 |
| 영향 ref 중 main에 완전히 포함된 ref | 1 |
| 영향 ref 중 main과 분기된 ref | 15 |
| Current remote tip exact match | 0 |
| Pre-planning local branch / 영향 branch | `20 / 19` |
| Pre-planning known worktree / dirty worktree | `24 / 2` |
| Current local tip exact match | 1 |
| Branch protection rule / repository ruleset | `0 / 0` |
| Main protection REST projection | `UNREADABLE_OR_UNSET` |
| Current viewer permission | `ADMIN` |
| `git-filter-repo` | 미설치 |

Branch와 worktree 수는 이 planning branch/worktree 생성 전 snapshot이다. 실행 직전 remote ref, worktree, dirty 상태와 protection을 다시 고정한다.

### 3.2 개인정보 범위

- Current checkout exact match: 0
- Canonical origin main 영향: 1 commit, 2 files
- 기존 비식별화 diff의 removed line: 46
- 기존 보고 occurrence: 51
- Confirmed personal data category: actual user name
- Finding 범주의 confirmed credential·secret: 0
- Finding 범주의 confirmed email·UPN·tenant/client/object ID: 0
- External clone·archive·mirror count: `UNKNOWN`

Private matcher는 비식별화 merge의 두 대상 파일과 removed line만 사용한다. Actual value, matching line, Git author·committer, old SHA와 개인 GitHub metadata는 console·tracked 문서·PR·CI log에 출력하지 않는다.

### 3.3 기준선 해석

- Remote tip 0은 현재 파일 내용이 비식별화됐다는 의미이며, 과거 history 제거 완료를 의미하지 않는다.
- Fork 0과 collaborator 1은 외부 clone·archive download가 없다는 증빙이 아니다.
- 15개 영향 branch는 squash merge 등으로 main과 분기돼 있어 main-only rewrite로 제거할 수 없다.
- Dirty worktree 2개는 사용자의 미게시 작업일 수 있으므로 자동 reset·삭제·patch export를 금지한다.
- Local tip exact match 1은 old local branch를 별도 quarantine·re-clone 대상으로 만든다. Branch명과 원문은 문서화하지 않는다.
- Protection/ruleset은 현재 count 0이지만 실행 직전 API와 web setting을 다시 확인한다. 현재 REST 응답만으로 권한 부족과 미설정을 혼동하지 않는다.

## 4. GitHub 공식 제약

- [GitHub sensitive data removal 지침](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository)은 local rewrite, GitHub ref 갱신, 동료 clone 정리와 재발 방지를 별도 단계로 요구한다.
- Force push 뒤에도 clone·fork, direct old SHA cached view와 PR ref에서 과거 data가 남을 수 있다. GitHub Support가 모든 non-secret 개인정보 cache 제거를 보장한다고 가정하지 않는다.
- [Repository visibility 변경 지침](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/setting-repository-visibility)에 따라 public→private 전환은 public fork 분리, Pages 중단과 일부 기능·보안 설정 변화가 가능하다.
- [Protected branch 지침](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)에 따라 force push 허용은 다른 protection을 자동 우회하지 않으며 old commit 제거·PR 손상 위험이 있다.
- Rewrite 도구는 GitHub가 안내하는 `git-filter-repo` 최신 지원 버전을 fresh task-owned clone에서만 사용한다. 실행 전 최소 버전과 설치 출처를 고정하며, 현재 미설치 상태에서는 rehearsal을 시작하지 않는다.

## 5. 보호할 불변조건

- Main 또는 기존 worktree에서 `git filter-repo`를 실행하지 않는다.
- Repository 밖 task-owned fresh mirror와 rehearsal clone만 사용한다.
- Replace mapping, pre-rewrite backup, filter-repo report와 commit map은 tracked/staged/PR/CI에 넣지 않는다.
- Private artifact directory mode는 0700, artifact mode는 0600으로 고정하고 암호화 key와 backup을 분리한다.
- Current published tip tree exact identity를 rewrite 전후 ref별로 확인한다.
- 영향 16개 remote ref의 expected old SHA가 하나라도 움직이면 push하지 않고 snapshot을 다시 승인받는다.
- Production push는 explicit ref allowlist와 ref별 `--force-with-lease=<ref>:<expected-old-sha>`만 허용한다.
- Production origin에 `git push --mirror`, catch-all `--force`, branch delete와 tag delete를 사용하지 않는다.
- GitHub read-only `refs/pull/*`은 push 대상으로 취급하지 않고 cache·PR 영향으로 별도 처리한다.
- Partial ref rewrite 시 Repository를 private·maintenance 상태로 유지하고 forward completion을 기본으로 한다.
- Pre-rewrite backup restore는 old 개인정보를 재노출하므로 자동 rollback으로 사용하지 않는다.
- Old clone은 pull·merge·rebase로 갱신하지 않고 fresh clone을 기본으로 한다.
- 필요한 미게시 변경은 privacy scan을 통과한 patch만 새 history에 적용하며 old commit을 merge하지 않는다.
- Existing runtime process와 Persistent UAT는 종료·재시작·변경하지 않는다. Runtime이 참조하는 old worktree는 process가 사용하는 동안 삭제하지 않는다.
- Exactly-once, 완전한 외부 clone 회수와 GitHub cache 즉시 삭제를 보장하지 않는다.

## 6. 사용자 결정 선택지

### 6.1 Temporary private containment

| 선택 | 노출 표면 | 실행 중 위험 | 부작용 | 권장 |
| --- | --- | --- | --- | --- |
| A. Rewrite 전 private, cache 판단까지 유지 | 새 익명 접근 차단 | 가장 낮음 | visibility 기능·fork·Pages·ruleset 영향 확인 필요 | **권장** |
| B. Public 유지 후 maintenance만 적용 | 기존 공개 지속 | partial push 중 old/new 혼합 history 공개 | visibility 부작용 없음 | 비권장 |

권장안은 A다. Repository를 private로 바꾼 뒤 collaborator·automation 접근과 Actions를 확인하고, all-ref 검증·Support 결과·old clone 격리가 끝날 때까지 public으로 되돌리지 않는다.

### 6.2 Secure backup 보존 기간

| 선택 | 복구 여유 | 개인정보 보유 위험 | 권장 |
| --- | --- | --- | --- |
| A. 최종 검증 후 7일 제한 보존 | 짧은 안정화 기간 확보 | 제한된 기간 동안 암호화 backup 유지 | **권장** |
| B. 최종 검증 직후 삭제 | 최소 | 실행 오류 사후 복구 여유 없음 | 대안 |
| C. 30일 이상 보존 | 큼 | 불필요한 장기 개인정보 보유 | 비권장 |

권장안은 A다. Mode 0600 암호화 backup을 Repository 밖 승인 위치에 두고, 7일 후 risk owner의 명시 승인으로 삭제한다. 보존 기간 중 restore는 별도 승인 없이는 금지한다.

### 6.3 Production push 방식

| 선택 | Ref 이동 감지 | 예상 밖 ref 영향 | 권장 |
| --- | --- | --- | --- |
| A. 16개 영향 ref explicit allowlist + ref별 force-with-lease | 강함 | 낮음 | **권장** |
| B. `git push --force --mirror` | Maintenance 중 원격 이동 보호가 약함 | hidden·unintended ref 갱신·삭제 가능 | 비권장 |

Official guide의 mirror push 예시는 general procedure지만, 이 Repository에서는 ref별 expected old SHA를 고정하는 A를 사용한다. Fresh mirror clone은 조사·rehearsal source로만 허용한다.

### 6.4 Cached view와 Support

| 선택 | Old SHA·PR cache 처리 | 완료 판단 | 권장 |
| --- | --- | --- | --- |
| A. GitHub Support 요청 + web 재검증 | 가능한 server-side cleanup 요청 | Support 수용 여부와 residual risk 기록 | **권장** |
| B. Ref rewrite만 수행 | cache·PR ref 잔존 가능 | P2 완료 근거 부족 | 비권장 |

GitHub Support가 실제 사용자 이름을 sensitive-data cleanup 대상으로 수용하지 않을 수 있다. 거절되면 risk owner가 cached-view residual risk, private 유지 또는 public 재개를 별도 결정한다.

### 6.5 Old clone 정책

| 선택 | 재유입 위험 | 운영 부담 | 권장 |
| --- | --- | --- | --- |
| A. Contributor·automation fresh clone 의무화 | 가장 낮음 | 모든 known clone·worktree 재구성 필요 | **권장** |
| B. 기존 clone fetch/reset | old object와 잘못된 merge·push 위험 | 낮음 | 비권장 |

Known worktree는 하나의 common repository를 공유할 수 있으므로 worktree 개수와 clone 개수를 동일하게 보지 않는다. Dirty worktree 2개의 미게시 변경은 원 소유자가 검수하고, patch에 개인정보와 old-history parent가 없을 때만 새 clone에 적용한다.

### 6.6 권장 승인 묶음

다음 다섯 항목을 함께 선택하는 것을 권장한다.

1. Temporary private containment
2. Encrypted backup 7일 제한 보존
3. Explicit affected-ref force-with-lease
4. GitHub Support/cache 검증
5. Contributor·automation fresh clone 의무화

Public 재개는 위 절차의 자동 일부가 아니다. Support 결과와 검증 보고를 확인한 뒤 별도 승인한다.

## 7. 실행 전 준비

### 7.1 Authority·maintenance gate

1. Risk owner 역할의 실행 승인자를 기록한다.
2. Repository setting 변경·ref force push·Support 요청 권한을 실제 계정에서 확인한다.
3. 일반 merge, push, branch 생성과 automation push를 중단한다.
4. Open PR 0과 active writer 0을 다시 확인한다.
5. Visibility, fork, collaborator, branch, tag, ruleset, protection과 Actions 상태를 count-only snapshot으로 고정한다.
6. Remote ref allowlist와 각 expected old SHA를 private manifest에 기록한다.
7. 모든 collaborator와 runtime·automation owner가 freeze를 확인했는지 count로 기록한다.
8. Dirty worktree owner가 미게시 변경 보존 방법을 확인하기 전 rewrite를 시작하지 않는다.

### 7.2 도구 gate

- `git-filter-repo` 최소 지원 버전 2.47 이상
- 설치 source는 official `newren/git-filter-repo` release 또는 신뢰된 package manager
- Tool version과 checksum·package source를 private evidence에 고정
- Git과 GitHub CLI 인증·admin 권한 확인
- Encrypted backup 도구와 key custody 확인
- Raw tool output은 private collector에 redirect하고 console에는 boolean·count·fixed enum만 출력

현재 `git-filter-repo`가 설치돼 있지 않으므로 installation·qualification 완료 전 실행은 `NO_GO`다. 이 planning은 tool 설치를 승인하지 않는다.

## 8. 단계별 실행 계획

### Phase A — Maintenance와 containment

1. Final preflight에서 main, remote ref와 setting snapshot을 다시 확인한다.
2. Merge·push·automation freeze를 시작한다.
3. 승인된 경우 Repository를 `PRIVATE`로 전환한다.
4. Visibility 전환 후 collaborator 접근, Actions와 settings drift를 read-only로 확인한다.
5. Remote ref가 snapshot 뒤 이동했거나 알 수 없는 fork·mirror가 발견되면 중단한다.

성공 기준:

- `maintenanceActive=true`
- `remoteRefMovedCount=0`
- `openPrCount=0`
- `visibilityContainment`이 승인값과 일치
- `collaboratorAcknowledgementCount`가 기준선과 일치

### Phase B — Secure backup과 fresh mirror

1. Repository 밖 0700 directory를 준비한다.
2. Fresh `--mirror` clone을 task-owned 위치에 생성한다.
3. 모든 remote branch·tag·published ref와 object connectivity를 확인한다.
4. Private pre-rewrite mirror backup을 암호화하고 mode 0600·non-empty·checksum을 확인한다.
5. Backup key는 backup과 분리된 승인 위치에 둔다.
6. Current remote tips, ref allowlist와 expected old SHA를 private manifest로 고정한다.
7. Replace mapping은 current anonymous value를 canonical replacement로 사용해 별도 mode 0600 파일로 만든다.
8. Raw mapping, SHA map, backup 경로와 checksum 원문은 console·tracked 문서에 출력하지 않는다.

성공 기준:

- `freshMirror=true`
- `objectConnectivityValid=true`
- `backupEncrypted=true`
- `backupModeValid=true`
- `backupChecksumValid=true`
- `replacementMappingTracked=false`
- `remoteRefSnapshotComplete=true`

### Phase C — Isolated rewrite rehearsal

1. Secure mirror에서 별도 disposable rehearsal clone을 만든다.
2. `git-filter-repo --sensitive-data-removal --replace-text`를 rehearsal clone에서만 실행한다.
3. Changed-ref count가 예상 영향 ref와 일치하는지 확인한다.
4. Unexpected branch·tag·path·binary change count를 확인한다.
5. 모든 pre-rewrite remote tip tree와 대응 post-rewrite tip tree가 동일한지 확인한다.
6. 영향 published ref history exact match 0을 private matcher로 검증한다.
7. `git fsck`와 full object connectivity를 확인한다.
8. Commit map·changed refs·raw log는 private evidence로만 유지한다.
9. Rehearsal clone을 production origin에 push하지 않는다.

성공 기준:

- `affectedPublishedRefMatchCount=0`
- `unexpectedChangedRefCount=0`
- `unexpectedChangedPathCount=0`
- `tipTreeMismatchCount=0`
- `fsckErrorCount=0`
- `rawArtifactTrackedCount=0`

### Phase D — Production ref update

1. Production push 직전 origin을 다시 fetch한다.
2. 모든 대상 ref가 private manifest의 expected old SHA와 동일한지 확인한다.
3. Protection/ruleset이 force push를 막으면 승인된 범위에서만 일시 변경하고 원 설정을 기록한다.
4. 영향 16개 ref를 explicit allowlist 순서로 ref별 force-with-lease push한다.
5. 각 push 뒤 completed, pending, failed count만 기록한다.
6. Unaffected ref와 tag는 변경하지 않는다.
7. 하나라도 실패하면 maintenance·private 상태를 유지하고 추가 일반 push를 차단한다.

Production에 catch-all mirror push를 사용하지 않는다. Actual 영향 ref 수는 final preflight에서 다시 계산하며 16과 다르면 사용자 재승인 전 push하지 않는다.

성공 기준:

- `affectedRefCompletedCount=affectedRefExpectedCount`
- `affectedRefFailedCount=0`
- `unaffectedRefChangedCount=0`
- `tagChangedCount=0`
- `remoteTipTreeMismatchCount=0`

### Phase E — Fresh clone·GitHub·CI 검증

1. 별도 fresh clone에서 main·branch·tag를 fetch한다.
2. 모든 origin ref history exact match 0을 확인한다.
3. Current tip tree와 main content가 pre-rewrite 기준과 동일한지 확인한다.
4. Old SHA direct access와 closed PR reference/cache를 web에서 fixed projection으로 확인한다.
5. 필요 정보만으로 GitHub Support cache purge 요청을 제출한다. PR·issue에 개인정보 원문을 붙이지 않는다.
6. Branch protection·ruleset과 visibility 외 설정을 preflight snapshot으로 복원한다.
7. Backend, Frontend, Full-Stack E2E CI가 rewritten head에서 성공하는지 확인한다.
8. Secret/PII scan과 tracked/staged raw artifact 0을 확인한다.

성공 기준:

- `freshCloneHistoryMatchCount=0`
- `freshCloneTipTreeMismatchCount=0`
- `cachedViewStatus`가 `REMOVED`, `SUPPORT_PENDING` 또는 risk owner가 명시 승인한 fixed enum
- `branchProtectionRestored=true`
- `repositoryRulesetRestored=true`
- `ciSuccessCount=3`
- `ciFailedOrPendingCount=0`

`SUPPORT_PENDING` 상태에서는 public 재개와 P2 종료를 자동 수행하지 않는다.

### Phase F — Old clone·worktree 격리와 재동기화

1. Known local clone, common worktree repository, automation과 deployment checkout을 inventory한다.
2. Old clone의 push credential 또는 push remote를 차단한다.
3. Fresh clone에서 필요한 branch·worktree를 새 history 기준으로 다시 만든다.
4. Dirty worktree는 원 소유자 승인과 privacy scan 뒤 patch 단위로 이전한다.
5. Old commit을 merge·cherry-pick하지 않는다. 필요한 diff만 검토해 새 commit으로 적용한다.
6. Running runtime이 old worktree path를 사용하면 process가 살아 있는 동안 해당 path를 삭제하지 않는다.
7. Runtime 재시작 없이 가능한 push quarantine을 먼저 적용하고, runtime source handover가 필요하면 별도 승인 Task로 분리한다.
8. Contributor와 automation의 fresh-clone 완료 count를 기록한다.

성공 기준:

- `knownOldClonePushEnabledCount=0`
- `knownFreshCloneReadyCount=expectedCount`
- `dirtyWorkLostCount=0`
- `oldHistoryReintroducedCount=0`
- `runtimeRestartCount=0`
- `PersistentUatMutationCount=0`

### Phase G — Visibility·backup·Task 종료

1. Support/cache 결과와 residual risk를 risk owner가 검수한다.
2. Public 재개 여부를 별도 승인받는다.
3. Public 재개 시 visibility 영향과 settings를 다시 확인한다.
4. 7일 보존 기간 종료 뒤 secure backup 삭제를 별도 승인받는다.
5. Private mapping, rehearsal clone, raw log와 commit map을 승인대로 삭제한다.
6. 5종 산출물과 Roadmap에서 P2 상태를 갱신한다.
7. 사용자 검수와 독립 Codex 검증 전 PR Ready·Merge와 P2 Closed를 수행하지 않는다.

## 9. Partial failure·rollback matrix

| 상황 | 즉시 조치 | 자동 허용 | 별도 승인 필요 | 공개 상태 |
| --- | --- | --- | --- | --- |
| Containment 실패 | Rewrite 시작 금지 | 설정 read-only 재확인 | visibility 재시도 | 기존 상태 유지 |
| Backup·mapping 검증 실패 | Rehearsal 금지 | task-owned 불완전 artifact 삭제 | 새 backup 생성 | private 유지 권장 |
| Rehearsal mismatch | Production push 금지 | disposable clone 폐기 | mapping·scope 재계획 | private 유지 |
| Ref가 snapshot 뒤 이동 | Push 금지 | fetch·count 재확인 | 새 snapshot·allowlist 승인 | private 유지 |
| 일부 ref push 실패 | 일반 개발 동결 | 성공/실패 count 확인 | forward completion 또는 old ref 복구 | private 유지 |
| Unexpected ref/path 변경 | 즉시 push 중단 | local rehearsal 폐기 | 재설계 | private 유지 |
| Old history 재유입 | Push 차단·원 clone 격리 | 영향 aggregate 확인 | 재-rewrite | private 유지 |
| Cache·PR ref 잔존 | Support 요청 준비 | fixed projection 재검증 | Support 제출·residual risk 수용 | private 유지 권장 |
| CI 실패 | Public 재개 금지 | read-only log 조사 | fix Task 또는 rollback 결정 | private 유지 |
| Old clone 전환 실패 | Push credential 차단 | fresh clone 재시도 | dirty work 이전 | private 유지 |

Old-history backup restore는 모든 경우 자동 조치가 아니다. Restore하면 개인정보가 다시 published ref에 노출될 수 있으므로 risk owner의 구체적인 ref·목적·기간 승인이 필요하다. 기본 rollback은 production push 전에는 rehearsal clone 폐기, production partial push 뒤에는 private 상태에서 forward completion이다.

## 10. 중단 조건

- Actual secret, credential, email·UPN, tenant/client/object ID가 추가로 발견됨
- 알 수 없는 fork, mirror, downstream automation 또는 write-capable clone 발견
- Final remote ref count·영향 ref count가 승인 snapshot과 다름
- Branch protection·ruleset과 force-push authority를 확정할 수 없음
- Current published tip exact match가 0이 아님
- Dirty worktree owner와 보존 경계를 확정할 수 없음
- `git-filter-repo` 최소 버전·출처를 검증할 수 없음
- Secure backup encryption, mode, key custody 또는 retention을 보장할 수 없음
- Rehearsal changed ref·path 또는 tip tree가 예상과 다름
- Explicit force-with-lease를 적용할 수 없음
- Partial ref 상태에서 public 개발을 재개해야 함
- GitHub cache residual risk와 public 재개 owner가 불명확함
- Runtime·Persistent UAT 변경이 필요함
- Raw 개인정보, old SHA, path, author·committer 또는 GitHub 개인 metadata가 console·tracked artifact에 노출됨
- 신규 P0/P1/P2 발견

중단 조건 발생 시 이를 우회하거나 자동 복구하지 않는다. Repository를 private·maintenance 상태로 유지하고 Finding, 실제 영향과 추가 승인 항목을 보고한다.

## 11. 검증 matrix

### 11.1 Rewrite 전

- Main/origin main 일치와 clean source
- Visibility·fork·collaborator·open PR·branch·tag·ruleset·protection snapshot
- Published ref와 expected old SHA private manifest
- Current checkout·remote tip exact match 0
- Backup encryption·mode·checksum
- Tool version·source qualification
- Dirty worktree preservation acknowledgement

### 11.2 Rehearsal

- Changed ref count와 allowlist 일치
- Current tip tree identity 100%
- All affected history exact match 0
- Unexpected path·binary·LFS impact 0
- `git fsck` error 0
- Raw artifact tracked·staged 0

### 11.3 Production 이후

- Remote all-ref exact match 0
- Fresh clone all-ref exact match 0
- Unaffected ref 이동 0
- Old history reintroduction 0
- Cached view·closed PR fixed projection
- Branch protection·ruleset·visibility 승인 상태
- Backend·Frontend·Full-Stack E2E CI 3종 success
- Secret/PII scan 0
- Contributor·automation fresh clone 완료
- Runtime listener·PID와 Persistent UAT 상태 불변
- Backup retention·deletion 승인 일치

## 12. 5종 산출물과 Git 전략

실행 승인 뒤 다음을 작성한다.

- `tasks/gov-history-rewrite-001.md`
- `tasks/gov-history-rewrite-001-implementation-report.md`
- `tasks/gov-history-rewrite-001-sop.md`
- `tasks/gov-history-rewrite-001-user-manual.md`
- `docs/00-product-roadmap.md`

Planning은 현재 파일을 사용한다. 실행 전에는 commit·push·PR을 만들지 않는다. Rewrite 완료 뒤 문서 PR을 게시할 때도 history rewrite force push와 일반 Task 문서 push를 같은 명령으로 섞지 않는다.

예상 문서 commit:

- `docs: record coordinated git history rewrite`

문서 PR은 rewritten main을 base로 생성하며 사용자 검수 전 Draft를 유지한다. History rewrite 자체는 PR merge로 수행할 수 없으므로 별도 force-push 승인 원장을 Implementation report에 기록한다.

## 13. 독립 검증 계획

실행 Codex와 분리된 새 Codex 검증 session이 다음을 확인한다.

- Published ref matcher 0과 fresh clone 결과
- Tip tree identity와 unexpected changed ref/path 0
- GitHub visibility·protection·ruleset 복원
- Cache·PR ref·Support 상태가 문서 표현과 일치
- Old clone push 차단과 fresh clone 완료
- Raw mapping·backup·log tracked/staged 0
- Runtime·Persistent UAT 변경 0
- 5종 산출물, 사용자 검수 상태와 P2 종료 Gate

독립 검증이 실패하면 public 재개, PR Ready·Merge와 P2 종료를 수행하지 않는다.

## 14. 사용자 승인 필요 항목

실행 전 사용자가 각각 승인해야 한다.

- [x] Planning과 영향 ref scope 승인
- [x] Repository maintenance·merge/push freeze 승인
- [x] Temporary public→private containment 승인
- [x] Secure encrypted backup 생성 승인
- [x] Backup 7일 제한 보존 승인 (삭제는 별도 승인)
- [x] `git-filter-repo` 설치·qualification·isolated rehearsal 승인
- [x] Private replacement mapping 생성 승인
- [x] 영향 ref explicit force-with-lease production push 승인
- [ ] Branch protection·ruleset 임시 변경·복원 승인
- [x] GitHub Support cache purge 요청 승인
- [x] Contributor·automation old clone 격리와 fresh clone 승인
- [ ] Dirty worktree patch 이전 승인
- [ ] Public 재개 조건과 residual cache risk 승인
- [ ] Task-owned raw artifact·backup 최종 삭제 승인
- [x] 5종 산출물 commit·push·PR·squash merge 승인

위 체크 상태가 현재 승인 경계다. 문서 게시·merge는 승인됐지만 protection 변경, dirty worktree patch 이전, public 재개와 backup 삭제는 승인되지 않았다.

## 15. Go/No-Go

### Planning Go

- `TASK-GOV-002` 정책과 risk owner가 확정됐다.
- Current checkout과 remote tip exact match는 0이다.
- 모든 영향 published ref를 식별할 privacy-safe matcher가 있다.
- Fresh mirror, private mapping, rehearsal, ref별 lease push와 fresh clone 검증 경로가 있다.
- Runtime·Persistent UAT를 변경하지 않고 Git history를 처리할 수 있다.

따라서 실행 계획은 `GO_FOR_USER_DECISION`이다.

### Execution result와 남은 No-Go

승인된 권장 묶음의 history rewrite 실행은 완료됐다. Published branch·tag 검증은 통과했고 fresh clone과 old common repository push quarantine도 완료했다.

GitHub Support는 internal reference 제거와 repository GC 완료를 회신했다. 인증된 web fixed projection에서 completion/follow-up/closed `1/1/1`, old cached reference `REMOVED`를 확인했으므로 `GIT_HISTORY_PERSONAL_DATA_REMAINS` P2 closure 조건은 충족했다. 이 결과는 public 재개, backup 삭제 또는 외부 clone 완전 회수를 자동 승인하지 않는다.

다음은 계속 `NO_GO`다.

- 명시적 risk owner 승인 없는 repository public 재개
- 7일 보존 기간 전 또는 별도 승인 없는 encrypted backup 삭제
- Dirty worktree patch 이전과 old worktree 정리
- 확인할 수 없는 외부 clone이 모두 회수됐다는 단정

### 전체 신규 기능

History P2는 해소됐다. 신규 기능 개발 여부는 `TASK-GOV-FINDING-GATE-001`의 전체 P0/P1/P2 재평가와 사용자 Go/No-Go 결정에서 확정한다.
