# TASK-GOV-HISTORY-REWRITE-001 — Coordinated Git history rewrite

## 1. 상태

- Task 유형: `SECURITY_HARDENING`
- Planning: 승인 완료
- 승인된 실행 묶음: 완료
- Published branch·tag rewrite: 완료
- Repository visibility: `PRIVATE`
- GitHub cached pull-request reference: `REMOVED`
- GitHub Support internal reference removal·repository GC: 완료
- Public 재개: 미승인
- Encrypted backup 삭제: 미승인
- 문서 commit·push·PR·merge: 승인 완료
- 사용자 검수: 완료
- 기존 실행 독립 검증: PASS
- Support closure 독립 검증: PASS
- 문서 사용자 검수 Gate: 완료
- P2 `GIT_HISTORY_PERSONAL_DATA_REMAINS`: Resolved
- P2 `FULL_STACK_E2E_PROCUREMENT_EDIT_ROW_RACE`: Resolved — PR #43
- 신규 기능 개발: `GO_FOR_USER_DECISION` — 별도 사용자 승인 대기

## 2. 목적

Current checkout에서 비식별화된 과거 개인정보가 published Git history에서 계속 도달 가능했던 P2를 coordinated all-ref rewrite로 제거한다. Current tip tree와 제품 동작은 바꾸지 않고 Git history, GitHub cache와 old clone 재유입 경계를 분리해 검증한다.

## 3. 승인된 범위

- Repository maintenance와 temporary public→private containment
- Repository 밖 encrypted pre-rewrite mirror backup과 7일 제한 보존
- `git-filter-repo` 2.47 qualification과 isolated rehearsal
- Private replacement mapping
- 영향 published ref explicit allowlist와 ref별 `force-with-lease`
- Fresh clone all-ref 검증
- GitHub Support cached commit reference 제거 요청
- Old common repository push quarantine와 fresh canonical clone
- 독립 Codex 검증
- 5종 산출물의 로컬 작성
- 5종 산출물의 commit·push·PR·squash merge

## 4. 승인되지 않은 범위

- Repository public 재개
- Encrypted backup 삭제·restore
- Dirty worktree patch 이전 또는 old worktree 삭제
- Runtime·Persistent UAT·migration·product source 변경

## 5. 실행 결과

| 항목 | 결과 |
| --- | --- |
| Published branch / tag | `19 / 0` |
| 영향 ref / 완료 ref | `16 / 16` |
| 예상 밖 ref 변경 | 0 |
| Current tip tree mismatch | 0 |
| Fresh-clone published history exact match | 0 |
| Fresh-clone current tip exact match | 0 |
| Fresh-clone fsck error | 0 |
| Private replacement pair | 46 |
| Backup encryption·mode·checksum·decrypt | PASS |
| Old common repository push quarantine | 완료 |
| Fresh canonical clone | 준비 완료 |
| GitHub Support ticket | 생성 완료 |
| Support completion / follow-up / closed projection | `1 / 1 / 1` |
| Old cached reference fixed projection | `REMOVED` |
| Affected cached pull-request history | 22 |
| Runtime restart / Persistent UAT mutation | `0 / 0` |
| Actual provider call | 0 |

## 6. Finding gate

- P0/P1: 0
- History rewrite 무결성 신규 P2: 0
- 해결된 P2 `GIT_HISTORY_PERSONAL_DATA_REMAINS`: 영향 published ref `16/16` rewrite, fresh-clone 검증, GitHub Support의 internal reference 제거·repository GC 완료 회신과 old cached reference `REMOVED` fixed projection으로 closure 조건을 충족했다.
- 해결된 P2 `FULL_STACK_E2E_PROCUREMENT_EDIT_ROW_RACE`: `TASK-E2E-RELIABILITY-001`에서 보정·검증 후 PR #43으로 병합됐다.
- 해결된 절차 P2 `PRIVACY_SAFE_EVIDENCE_OUTPUT_VIOLATION`: 기존 2건 보정 뒤 Support closure 확인에서 raw page snapshot 1건이 fixed-projection 경계를 다시 벗어났다. Tracked leak·external write·secret 노출은 0이다. 브라우저 수집을 즉시 중단하고 completion/follow-up/closed `1/1/1`, cached view `REMOVED` fixed projection으로 처음부터 재검증했다.
- External clone·download 완전 회수: 증명 불가
- Support fixed-schema 재검증: privacy guard PASS, 오류 0, unresolved history P2 0
- Support closure 문서의 분리된 Codex 독립 검증: PASS, P0/P1/P2/P3 `0/0/0/0`, merge Gate GO

## 7. 사용자 검수 체크리스트

- [x] Repository가 public 재개 승인 전까지 private인지 확인
- [x] Published branch·tag history exact match 0과 tip tree mismatch 0 결과 확인
- [x] 16개 영향 ref만 갱신되고 예상 밖 ref 이동이 0인지 확인
- [x] Fresh canonical clone 사용과 old common repository push quarantine 확인
- [x] Encrypted backup을 제한 보존하고 별도 승인 전 삭제·restore하지 않는 정책 확인
- [x] Support 회신과 old cached reference fixed projection이 `REMOVED`이며 history P2가 해소됐는지 확인
- [x] Runtime·Persistent UAT·product source 변경 0 확인
- [x] Public 재개·backup 삭제는 별도 승인으로 유지하고 문서 commit·push·PR·merge는 승인했음을 확인

## 8. 다음 Gate

1. 승인된 5종 산출물 commit·push·PR·squash merge
2. 신규 기능 Go/No-Go 사용자 결정
3. Risk owner의 public 재개 결정
4. 보존 기간 경과 뒤 backup 삭제 별도 승인
5. Deferred worktree cleanup 별도 승인
