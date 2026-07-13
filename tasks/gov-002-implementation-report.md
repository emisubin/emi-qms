# TASK-GOV-002 Implementation Report

## 1. 결과

Public Git history에 남은 실제 사용자 이름 P2를 read-only로 재조사하고 `COORDINATED_HISTORY_REWRITE`를 승인 정책으로 확정했다. 실제 history rewrite, force push와 visibility 변경은 수행하지 않았다.

- Policy decision: `COORDINATED_HISTORY_REWRITE`
- Risk owner: `Repository owner / security owner`
- Policy documentation: 완료
- Independent Codex verification: 1차 P2 2건 정정 후 최종 PASS
- User artifact validation: 완료
- PR #41: Draft 게시·Ready 전환·squash merge 승인
- Rewrite execution: 별도 `TASK-GOV-HISTORY-REWRITE-001`
- Finding status: rewrite 실행 전까지 Open P2

## 2. 배경과 범위

BASELINE-GOV-001에서 current checkout의 두 문서를 비식별화했지만 과거 commit은 유지했다. TASK-UAT-AUTH-HARDEN-001이 완료된 뒤 Roadmap 순서에 따라 이 risk decision을 최우선 P2로 진행했다.

포함 범위는 visibility, 영향 commit/file/ref, fork/open PR과 clone 확인 한계를 privacy-safe aggregate로 조사하고 정책·책임·후속 실행 경계를 문서화하는 것이다.

Product code, runtime, Persistent UAT, Git history와 remote ref mutation은 제외했다.

## 3. Read-only 조사 결과

| Projection | Result |
| --- | --- |
| Repository visibility | `PUBLIC` |
| Fork/open PR/tag | `0/0/0` |
| Current checkout exact match | 0 |
| Origin main 영향 | 1 commit, 2 files |
| 영향 remote ref | 15/18 |
| 영향 local branch | 19/20 |
| External clone count | Unknown |

과거 보고의 46 lines, 51 occurrences를 원문 없이 기준으로 사용했다. 제거 line exact matcher는 private memory에서만 실행했고 match text·commit author·개인 GitHub metadata를 출력하지 않았다.

## 4. 정책 결정

Risk acceptance 대신 coordinated rewrite를 선택했다.

선택 이유:

- Repository가 public이다.
- Confirmed personal data가 origin main history에 남아 있다.
- Fork와 open PR이 0인 현재가 향후보다 조정 비용이 낮다.
- Current checkout replacement가 이미 검증됐다.

Private-only는 노출 표면 완화일 뿐 old history 제거가 아니므로 단독 해결책으로 선택하지 않았다. Repository 삭제·재생성은 integration·audit 손실이 Finding 규모에 비례하지 않아 폐기했다.

## 5. 영향 영역

- Backend/API/authorization: N/A — 변경 없음
- Frontend/UI: N/A — 변경 없음
- DB/migration: N/A — 변경 없음
- Runtime/provider/worker: N/A — 변경 없음
- Excel/PDF/attachment: N/A — 변경 없음
- Git/GitHub: 정책 문서만 변경. Remote history mutation 0
- 개인정보: actual value를 재출력하지 않고 aggregate만 기록

## 6. 변경 파일

- `tasks/gov-002-planning.md` — 승인 정책과 실행 경계
- `tasks/gov-002.md` — Task 계약과 checklist
- `tasks/gov-002-implementation-report.md` — 조사·결정 원장
- `tasks/gov-002-sop.md` — 운영·후속 rewrite 절차
- `tasks/gov-002-user-manual.md` — Repository 사용자 안내
- `docs/00-product-roadmap.md` — P2 실행 순서와 Decision Log

## 7. 후속 rewrite 경계

`TASK-GOV-HISTORY-REWRITE-001`에서만 maintenance, secure mirror, private mapping, all-ref force push, cache 처리와 re-clone을 실행한다.

Rewrite는 일반적인 rollback이 불가능하다. Pre-rewrite backup을 복원하면 개인정보를 다시 공개할 수 있으므로 자동 restore를 금지하고 별도 risk owner 승인을 요구한다.

## 8. 검증

실행:

- Git/main/remote/same-purpose preflight
- History privacy-safe aggregate scan
- Repository visibility·fork·open PR projection
- Changed-file allowlist 검사
- Markdown heading·local link 검사
- Secret/PII/GUID/absolute path scan
- Staged·commit·push·PR 0 확인

미실행:

- Backend/Frontend test: product source 변경 없음
- Migration test: migration 변경 없음
- Runtime/UAT: 명시적 제외
- Rewrite rehearsal: 별도 Task planning·승인 전 금지

## 9. 개인정보와 보안

실제 이름, email·UPN, object ID, raw Git author/committer, PR participant와 old line을 문서·console에 기록하지 않았다. Confirmed credential·secret은 Finding 범위에서 0이다.

Repository가 public이므로 rewrite 완료 전까지 historical exposure는 계속 true다. Fork count 0은 clone·download 0 증빙이 아니다.

## 10. Findings와 제한

- P2 `GIT_HISTORY_PERSONAL_DATA_REMAINS`: 정책 선택 완료, 실행 미완료
- Main protection API: 권한 부족과 미설정을 구분하지 못해 후속 Task preflight에서 재확인
- External clone/download: 완전 inventory 불가
- Rewrite 뒤 GitHub cache 제거: Support 또는 visibility 조치가 필요할 수 있음
- 신규 P0/P1/P2: 0

## 11. 해결한 업무 문제

Current checkout 비식별화와 public history 노출을 같은 완료 상태로 오해하지 않도록 분리하고, 위험 수용 대신 실제 history remediation을 후속 실행 Gate로 확정했다.

## 12. 기술적 결정과 검토한 대안

- Coordinated all-ref rewrite를 선택했다.
- Main-only rewrite는 다른 branch가 old blob을 reachable하게 유지하므로 폐기했다.
- Private-only는 containment로만 허용하고 완료안으로 사용하지 않는다.
- Delete-and-recreate는 audit·integration 손실 때문에 폐기했다.
- Risk acceptance는 선택하지 않았지만 canonical 대안으로 비교했다.

## 13. 시행착오 및 폐기한 접근

- GitHub fork 0을 외부 clone 0으로 해석하는 접근을 폐기했다.
- Current checkout scan 0을 history scan 0으로 간주하지 않았다.
- Raw `git log`, `git show`, author·committer와 matching line 출력은 privacy-safe evidence 정책 때문에 사용하지 않았다.
- Main ref만 대상으로 하는 rewrite 계획은 15개 remote ref 영향 때문에 폐기했다.

## 14. 사용자 검수 결과와 남은 항목

사용자는 planning의 권장 정책 `COORDINATED_HISTORY_REWRITE`와 5종 산출물을 검수하고 PR #41의 Ready 전환·squash merge를 승인했다.

남은 실행은 별도 `TASK-GOV-HISTORY-REWRITE-001` planning과 maintenance·force-push 승인이다. Rewrite 완료 전 P2는 Open이며 신규 기능 개발은 No-Go다.

## 15. Rollback·복구

현재 문서 변경은 commit revert로 복구할 수 있다. Remote history는 변경하지 않았으므로 Git rollback은 필요 없다.

후속 rewrite 중 partial push가 발생하면 maintenance를 유지하고 forward completion 또는 명시적 old-history 복구를 risk owner가 결정한다. Old-history 복구는 개인정보 재노출 가능성이 있어 자동 수행하지 않는다.

## 16. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | `tasks/gov-002-sop.md` | 작성 완료 |
| User manual | `tasks/gov-002-user-manual.md` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 정책·후속 Gate 반영 |
| User validation checklist | `tasks/gov-002.md` 13장 | 사용자 검수 완료 |
