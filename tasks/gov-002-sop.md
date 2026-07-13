# TASK-GOV-002 SOP — Git history 개인정보 정책 운영

## 1. 목적

Git history 개인정보 Finding을 원문 재노출 없이 조사하고, 승인된 `COORDINATED_HISTORY_REWRITE` 정책과 후속 실행 경계를 유지한다.

## 2. 현재 상태

- Current checkout: 비식별화 완료
- Public history: P2 Open
- 승인 정책: coordinated all-ref rewrite
- 실제 rewrite: 미실행
- Risk owner: `Repository owner / security owner`

## 3. Read-only 점검 절차

1. Main과 origin/main 일치를 확인한다.
2. 동일 목적 branch/worktree/PR을 확인한다.
3. 비식별화 commit의 removed line을 private memory matcher로만 구성한다.
4. Current checkout, main, remote branch와 tag의 match count를 aggregate한다.
5. Visibility, fork, open PR와 ref count를 fixed projection으로 기록한다.
6. Matching line, actual identity, author·committer와 participant를 출력하지 않는다.
7. Private matcher artifact는 결과 집계 뒤 삭제한다.

## 4. 출력 규칙

허용:

- Boolean, integer, fixed enum
- Short SHA
- Repository 내부 path
- Branch/ref aggregate

금지:

- Actual name, email·UPN와 identity ID
- Matching line·token
- Raw `git log`, `git show`, `git blame`, `git shortlog`
- GitHub actor·reviewer·assignee metadata
- Credential, connection string와 absolute local path

## 5. Rewrite 실행 전 Gate

다음은 `TASK-GOV-HISTORY-REWRITE-001`의 별도 승인이 있어야 한다.

- Maintenance 시작과 merge/push freeze
- Visibility containment
- Secure mirror/bundle 생성
- Private replacement mapping
- Main·branch·tag force push
- Ruleset·branch protection 임시 변경
- GitHub Support cache purge 요청
- Contributor·automation re-clone 공지
- Backup 보존·삭제

하나라도 미승인이면 rewrite를 시작하지 않는다.

## 6. 권장 rewrite 순서

1. Fresh task-owned mirror clone을 만든다.
2. Origin ref와 protection/ruleset을 privacy-safe projection으로 고정한다.
3. Repository 밖 mode 0600 replacement mapping을 만든다.
4. Secure pre-rewrite backup과 checksum을 생성한다.
5. `git filter-repo` dry-run 또는 isolated rehearsal을 수행한다.
6. Current tree가 rewrite 전후 동일한지 확인한다.
7. 모든 영향 published ref를 같은 maintenance에서 force push한다.
8. Fresh clone에서 main·branch·tag match 0을 검증한다.
9. CI, ruleset와 branch protection을 복원한다.
10. Old clone 폐기·re-clone 완료를 확인한다.
11. Cache 잔존 시 visibility 제한 또는 GitHub Support를 진행한다.
12. 승인된 보존 기간에 따라 secure backup을 제한 보존하거나 삭제한다.

이 순서는 실행 계획 초안이며 후속 Task 승인 전 명령을 수행하지 않는다.

## 7. Partial rewrite 대응

- 일반 merge와 push freeze를 유지한다.
- 완료된 ref와 미완료 ref count를 aggregate한다.
- Old branch의 재push를 차단한다.
- Risk owner가 forward completion 또는 old-history 복구를 결정한다.
- Old-history restore가 개인정보 재노출임을 명시한다.
- 임의 force push, branch 삭제 또는 backup restore를 수행하지 않는다.

## 8. Contributor 재동기화

Rewrite 완료 뒤 기존 clone에서 pull/rebase를 권장하지 않는다. Fresh clone을 기본으로 하며 필요한 미게시 work는 patch를 개인정보·old commit reference 없이 검토한 뒤 새 history에 적용한다.

Automation, scheduled job, deployment와 local worktree가 old SHA를 push하지 않도록 credential·remote 사용을 일시 중단하고 새 clone으로 교체한다.

## 9. 검증 지표

- currentCheckoutMatchCount
- originMainHistoryMatchCount
- originRemoteRefMatchCount
- tagHistoryMatchCount
- affectedRefCompletedCount
- freshCloneValidation
- oldCloneRetiredCount
- rawArtifactTrackedCount
- rawArtifactRetainedCount
- branchProtectionRestored
- ciSuccessCount

## 10. 금지사항

- 승인 전 `git filter-repo`
- Main 직접 push 또는 일부 ref만 force push
- Raw PII 출력
- Old clone에서 일반 push
- Pre-rewrite backup 자동 restore
- 승인 없는 branch/tag/worktree 삭제
- Rewrite 완료 전 P2 Closed 표시

## 11. 정책 재검토 Trigger

- Actual secret·email·ID 추가 발견
- Fork 또는 downstream mirror 발견
- Repository visibility 변경
- 당사자 삭제 요청
- Old history 재유입
- GitHub cache에서 old object 접근 지속
- Rewrite 실행 권한 또는 ruleset 제약 변경

## 12. 사용자 검수 전 게시 Gate

- 5종 산출물 link 유효
- Decision Log 신규 행만 추가
- Secret/PII candidate 0
- Product code/runtime/DB diff 0
- User checklist 완료 상태 명시
- Draft PR만 허용

## 13. PR #41 검수 상태

- 사용자 검수: 완료
- CI: Backend·Frontend·Full-Stack E2E 성공
- Ready 전환·squash merge: 승인
- 실제 history rewrite·force push·visibility 변경: 미승인·미실행
