# TASK-GOV-HISTORY-REWRITE-001 Implementation Report

## 1. 결과

Public Git history에 남아 있던 과거 개인정보를 16개 영향 published ref에서 coordinated rewrite했다. Fresh clone의 published branch·tag history scan, tip tree identity와 object connectivity는 통과했다. GitHub Support가 internal reference 제거와 repository GC 완료를 회신했고 old cached reference는 web fixed projection에서 `REMOVED`로 확인됐다. Repository는 별도 public 재개 결정 전까지 `PRIVATE`로 유지한다.

- History rewrite: 완료
- Production ref update: `16/16`
- Fresh clone validation: PASS
- Independent Codex verification: PASS
- Independent privacy-safe docs rerun: PASS
- GitHub Support cache cleanup: `REMOVED`
- History P2: Resolved
- Public 재개: 미승인·미수행
- Backup 삭제·restore: 미승인·미수행
- 문서 게시: PR #50 Ready / squash merge 승인·실행 중

## 2. 해결한 업무 문제

Current checkout의 비식별화만으로는 다른 published branch와 과거 commit이 개인정보를 계속 reachable하게 만들었다. Main-only rewrite와 private-only containment를 완료로 오해하지 않도록 published ref, GitHub cache, 외부 clone과 local worktree를 서로 다른 제거 경계로 다뤘다.

## 3. 포함·제외 범위

포함:

- Temporary private containment
- Secure mirror backup과 7일 보존
- Isolated rehearsal과 독립 검증
- 영향 ref explicit `force-with-lease`
- Fresh clone all-ref scan과 fsck
- GitHub Support ticket
- Old common repository push quarantine와 fresh canonical clone

제외:

- Product source, Backend, Frontend, API, DB, migration와 runtime 변경
- Persistent UAT write 또는 provider 호출
- Public 재개
- Backup restore·삭제
- Dirty worktree 이전·삭제
- 문서 게시

## 4. 실제 기준선과 영향 범위

| Projection | Result |
| --- | --- |
| Pre-rewrite main short SHA | `3aacf2a54e02` |
| Post-rewrite main short SHA | `486cb4187414` |
| Published branch / tag | `19 / 0` |
| 개인정보 영향 published ref | 16 |
| Unaffected published ref | 3 |
| Replacement pair | 46 |
| Open PR / fork | `0 / 0` |
| Known worktree | 25 |
| Pre-existing dirty worktree preserved | 2 |
| Planning worktree dirty state preserved | 1 |

Actual 개인정보, matching line, old full commit ID, author·committer와 개인 GitHub metadata는 tracked 문서에 기록하지 않았다.

## 5. 기술적 결정과 검토한 대안

선택:

- Private containment 뒤 rewrite
- Repository 밖 encrypted mirror backup
- `git-filter-repo` 2.47과 private replace mapping
- 실제 영향 16개 ref만 scoped rewrite
- Ref별 expected old commit을 고정한 explicit `force-with-lease`
- Old common repository push quarantine와 fresh clone

폐기:

- Main-only rewrite: 다른 branch가 과거 object를 유지함
- `git push --mirror --force`: 예상 밖 ref 갱신·삭제 범위가 큼
- Existing clone reset/rebase: old object 재유입 위험
- 즉시 public 재개: cached pull-request reference가 남아 있음
- Backup 자동 rollback: 개인정보를 다시 published history에 노출할 수 있음

## 6. Rehearsal과 시행착오

첫 disposable rehearsal은 source/target 선택 오류로 changed ref가 0이어서 production 전에 폐기했다. 두 번째 unscoped rehearsal은 필요하지 않은 ref 1개까지 변경해 폐기했다. Remote snapshot에서 실제 영향 ref를 재계산한 뒤 16개 ref만 지정한 scoped rehearsal을 authoritative 결과로 사용했다.

Authoritative rehearsal:

- Changed / expected ref: `16 / 16`
- Missing / extra ref: `0 / 0`
- Unexpected path: 0
- Tip tree mismatch: 0
- Published history exact match: 0
- fsck error: 0

잘못된 두 rehearsal은 disposable clone에만 존재했고 production ref에는 영향이 없다.

## 7. Backup과 복구 경계

- Repository 밖 private directory mode: 0700
- Encrypted backup file mode: 0600
- Encryption: AES-256-CBC + PBKDF2
- Checksum: 일치
- Decrypt qualification: PASS
- Key custody: backup과 분리
- 보존: 생성 시점부터 7일
- Restore: 별도 risk owner 승인 전 금지
- Delete: 보존 기간 종료와 별도 승인 전 금지

Production partial failure의 기본 대응은 private maintenance에서 forward completion이다. Old-history restore는 자동 rollback이 아니다.

## 8. Production ref update

Production 직전 remote ref snapshot 이동, 추가와 누락은 모두 0이었다. 영향 16개 ref에만 expected old commit을 고정한 lease를 적용했고 한 번의 explicit atomic push로 완료했다.

- Completed / failed: `16 / 0`
- Unaffected ref moved: 0
- Tag changed: 0
- Remote ref total: 19
- Repository visibility: `PRIVATE`

## 9. Fresh clone과 cache

Fresh private mirror와 canonical working clone에서 다음을 확인했다.

- Expected remote ref mismatch: 0
- Published history exact match: 0
- Published tip exact match: 0
- Tip tree mismatch: 0
- fsck error: 0

초기 확인 당시 GitHub read-only pull-request cache에는 영향 history reference 22개가 남아 있어 cached commit reference 제거 Support ticket을 생성했다. Support는 이후 internal reference 제거와 repository GC 완료를 회신했다. 인증된 web fixed projection 결과는 completion/follow-up/closed `1/1/1`, old cached reference `REMOVED`, page-not-found `true`다. 실제 ticket·account·commit 원문은 tracked 문서에 기록하지 않았다.

## 10. Old clone·worktree 처리

- Fresh canonical clone을 rewritten main에서 생성했다.
- 기존 common repository의 fetch URL은 유지하고 push URL만 quarantine했다.
- 기존 25개 worktree와 runtime source path는 삭제·reset하지 않았다.
- Dirty worktree 3개 중 사용자 WIP 2개와 planning 변경 1개를 보존했다.
- Dirty diff 이전은 별도 승인 전 수행하지 않는다.
- Old commit merge·cherry-pick은 금지하며 필요한 변경은 privacy scan 뒤 patch 단위로 다시 작성한다.

## 11. 검증

| 검증 | 결과 |
| --- | --- |
| Tool version/source qualification | PASS |
| Encrypted backup mode/checksum/decrypt | PASS |
| Scoped rehearsal | PASS |
| Independent Codex production push gate | GO |
| Independent docs user-review gate | GO |
| Production affected ref completion | `16/16` |
| Fresh clone history/tip/tree/fsck | PASS |
| Frontend CI | PASS |
| Backend CI | PASS |
| Full-Stack E2E CI | 초기 FAIL 2회 → `TASK-E2E-RELIABILITY-001` 보정·PR #43 병합 후 전체 16/16 PASS |
| Runtime listener·health pre/post | 불변 |
| Runtime restart | 0 |
| Persistent UAT mutation | 0 |
| Provider call | 0 |

초기 Full-Stack E2E는 구매정보 직접 입력 시 동적 행 input이 준비되기를 기다리는 기존 predicate에서 최초 run과 failed-job 재실행이 동일하게 실패했다. Rewritten tip tree는 pre-rewrite tip tree와 동일해 history rewrite가 source를 변경한 결과가 아니었으며, `TASK-E2E-RELIABILITY-001`에서 stale-response race를 보정했다. 대상 20/20과 전체 16/16 검증 뒤 PR #43으로 병합되어 이 P2는 해소됐다.

## 12. 개인정보와 evidence

Replacement mapping, backup, commit map, Support payload와 raw logs는 Repository 밖 mode 0600 private artifact로 유지하고 tracked/staged 상태는 0이다. 기존 Support browser 전체 DOM projection 1회와 독립 검증 GitHub 조회 실패 traceback 1회는 collector/projector 보정 뒤 처음부터 재검증했다. 이번 Support closure에서 raw page snapshot 1건, publication push 결과에서 원격 account path가 포함된 원문 1건이 fixed-projection 경계를 다시 벗어났다. Tracked leak·secret은 0이다. Support는 completion/follow-up/closed `1/1/1`과 cached reference `REMOVED`, GitHub publication은 PR number·state·check count만 반환하도록 보정했다. 절차 Finding은 재발 기록과 fixed projection 재검증을 완료한 뒤 Resolved로 유지한다.

## 13. 영향

- Backend/API/권한: N/A — source 변경 없음
- Frontend/UI: N/A — source 변경 없음
- DB/migration: N/A — 변경 없음
- Runtime/provider/worker: N/A — restart·configuration·호출 없음
- Excel/PDF/attachment: N/A — 변경 없음
- Git/GitHub: published history와 repository visibility 변경

## 14. Findings와 잔여 위험

- P0/P1: 0
- 해결된 P2 `FULL_STACK_E2E_PROCUREMENT_EDIT_ROW_RACE`: `TASK-E2E-RELIABILITY-001` 보정·자동 검증·PR #43 병합 완료
- 해결된 절차 P2 `PRIVACY_SAFE_EVIDENCE_OUTPUT_VIOLATION`: 기존 2건과 closure·publication 재발 2건을 기록하고 fixed projector로 gate 재실행. Tracked leak·secret 0
- 해결된 P2 `GIT_HISTORY_PERSONAL_DATA_REMAINS`: published ref `16/16`, Support internal reference 제거·GC 완료, cached view `REMOVED`
- GitHub cached view removal: 완료
- External clone/archive 완전 inventory: 불가능
- Private 전환 뒤 branch protection rule은 0이며 repository ruleset API는 현재 plan에서 재판정할 수 없었다. Rewrite를 위해 protection 설정을 변경하지 않았고 public 재개 전 settings를 다시 확인한다.
- Public 재개: risk owner 결정 대기
- Backup 삭제: 7일 보존 뒤 별도 승인 대기
- PostgreSQL restart counter: 후속 Finding Gate read-only 재확인에서 0
- Support closure fixed-projection 집계: privacy guard PASS, validation error 0, unresolved history P2 0
- Support closure 문서의 분리된 Codex 독립 검증: PASS, findings P0/P1/P2/P3 `0/0/0/0`, merge Gate GO

## 15. 사용자 검수 결과와 남은 항목

사용자 검수와 문서 commit·push·PR·squash merge 승인을 완료했다. Public 재개와 backup 삭제는 현재 승인에 포함되지 않았다. History·E2E P2는 해소됐고 `TASK-GOV-FINDING-GATE-001` 재평가 결과 Open P0/P1/P2는 `0/0/0`, 신규 기능은 `GO_FOR_USER_DECISION`이다.

## 16. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | `tasks/gov-history-rewrite-001-sop.md` | 작성 완료 |
| User manual | `tasks/gov-history-rewrite-001-user-manual.md` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 로컬 갱신 완료 |
| User validation checklist | `tasks/gov-history-rewrite-001.md` 7장 | 사용자 검수 완료 |
