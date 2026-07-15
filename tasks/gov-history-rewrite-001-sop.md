# TASK-GOV-HISTORY-REWRITE-001 SOP

## 1. 목적

Published Git history 개인정보 rewrite 뒤 cache, old clone, visibility와 encrypted backup을 안전하게 관리하는 운영 절차다. 제품 runtime이나 Persistent UAT 운영 절차를 대체하지 않는다.

## 2. 현재 상태

- Repository: `PUBLIC`
- Published ref rewrite: 완료
- Fresh clone validation: PASS
- GitHub Support cache cleanup: `REMOVED`
- Old common repository push: quarantine
- Encrypted pre-rewrite backup: 7일 제한 보존
- Public 재개: 사용자 승인·수행 완료
- Default branch `main`: active required-pull-request ruleset 적용, approving review·required status check 강제 0
- Backup 삭제: 별도 승인 필요

## 3. Support cache 확인

1. Support ticket 상태를 인증된 GitHub Support 화면에서 확인한다.
2. Raw DOM, 전체 accessibility snapshot, commit URL, full commit ID와 개인 metadata를 console이나 문서에 출력하지 않는다.
3. Dialog-scoped prompt와 button만 읽고 boolean·count·fixed enum으로 project한다.
4. Support가 완료를 알리면 private fresh clone과 web fixed projection에서 affected cached reference가 제거됐는지 재검증한다.
5. 결과를 `REMOVED`, `SUPPORT_PENDING`, `SUPPORT_REJECTED`, `UNKNOWN` 중 하나로 기록한다.
6. `REMOVED`가 아니면 public 재개와 P2 Closed를 자동 수행하지 않는다.

현재 결과는 Support completion/follow-up/closed `1/1/1`, old cached reference `REMOVED`다. 같은 reference가 다시 노출될 때만 Support 후속 문의를 재개한다.

GitHub CLI 또는 독립 검증에서도 raw stderr·traceback을 terminal에 연결하지 않는다. 실패 출력은 private collector에서 stable enum·count로 변환하고, projector가 실패하면 원문 없이 `privacySafeEvidenceGuard=FAIL`만 출력한다.

## 4. Fresh clone 전환

1. Rewrite 이전 clone에서 fetch·reset·rebase·merge로 history를 재사용하지 않는다.
2. Rewritten origin에서 fresh clone을 만든다.
3. Fresh clone의 default branch와 origin main이 일치하고 working tree가 clean인지 확인한다.
4. 필요한 미게시 변경은 원 소유자가 검수하고 개인정보 scan을 통과한 diff만 새 commit으로 다시 적용한다.
5. Old commit merge·cherry-pick과 old branch force push를 금지한다.
6. Existing runtime이 old worktree를 사용하면 runtime handover 승인 전 해당 worktree를 삭제하지 않는다.

## 5. Old common repository quarantine

- Fetch는 조사 목적으로 유지할 수 있다.
- Push URL은 rewrite quarantine sentinel을 유지한다.
- Old clone에서 push를 다시 활성화하려면 risk owner가 fresh-clone 전환과 WIP 이전을 먼저 승인해야 한다.
- `git push --mirror`, catch-all `--force`와 old history 복구 push를 금지한다.

## 6. Public 재개 Gate — 완료 기록

Public 재개 승인 당시 다음 항목을 모두 확인했다.

- Published branch·tag history exact match 0
- Fresh clone tip tree mismatch 0
- GitHub cache status `REMOVED` 또는 residual risk의 명시적 사용자 수용
- CI Backend·Frontend·Full-Stack E2E success
- Known old clone push enabled 0
- Repository settings drift 검토 완료
- Encrypted backup 보존·삭제 경계 확인
- P0/P1과 신규 P2 0
- Risk owner의 public 재개 명시 승인 완료

Public 재개는 history rewrite의 자동 후속 단계가 아니며, closure와 PR #50 merge 뒤 사용자가 별도로 수행했다. 현재 visibility나 required-pull-request ruleset을 변경하려면 다시 명시적 승인을 받는다.

## 7. Backup 보존·삭제

- Backup과 key는 서로 분리된 private 위치에 둔다.
- Directory 0700, file 0600을 유지한다.
- 보존 기간은 생성 시점부터 7일이다.
- 보존 중 checksum과 decrypt 가능 여부만 fixed projection으로 확인한다.
- Restore는 old 개인정보를 다시 노출할 수 있으므로 자동 rollback으로 사용하지 않는다.
- 7일 뒤에도 별도 삭제 승인이 없으면 임의 삭제하지 않고 risk owner에게 결정을 요청한다.
- 삭제 승인 후 backup, checksum, key와 task-owned raw mapping을 범위별로 삭제하고 retained count 0을 확인한다.

## 8. CI 실패 대응

- 후속 게시·merge를 보류한다.
- Rewritten tip tree와 source diff가 0인지 먼저 확인한다.
- Timing/flaky failure는 source를 수정하지 않고 failed job 한 번 재실행해 재현 여부를 확인할 수 있다.
- 동일 실패가 재실행에서도 반복되면 flake로 수용하지 않고 별도 bugfix Task로 분리한다.
- 구매정보 동적 행 input 준비 predicate 반복 실패는 `TASK-E2E-RELIABILITY-001`과 PR #43에서 해소됐다.
- Rewrite 당시 CI 성공 전 P2 Closed와 public 재개를 금지했다. 현재 후속 CI가 실패하면 해당 게시·merge를 보류한다.

## 9. Partial failure와 복구

| 상황 | 조치 |
| --- | --- |
| Support pending/rejected 또는 reference 재노출 | Private containment 필요성 검토, residual risk 결정 또는 Support 후속 요청 |
| Old history 재유입 | 해당 clone push 격리, 영향 ref 재조사, 재-rewrite 별도 승인 |
| Old clone에만 WIP 존재 | 삭제 금지, privacy-safe patch 이전 별도 승인 |
| Public 재개 뒤 cache 발견 | 즉시 private containment 검토, Support와 risk owner 보고 |
| Backup integrity 실패 | Restore 금지, private 유지, 별도 복구 결정 |
| CI 반복 실패 | 후속 게시·merge 금지, 별도 fix Task |

## 10. 금지 작업

- Old history backup 자동 restore
- Old clone merge·rebase·cherry-pick으로 fresh history 갱신
- Explicit lease 없는 force push
- Support ticket·PR·issue에 실제 개인정보 원문 첨부
- 전체 browser DOM/snapshot 또는 raw GitHub metadata 출력
- 승인 없는 visibility·main required-PR 변경, backup 삭제와 worktree 정리
