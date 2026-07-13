# TASK-GOV-ROADMAP-001 — PR #38 이후 Roadmap 동기화

## 1. 상태

- Task 유형: `DOCS_GOVERNANCE`
- 구현: 완료
- 자동 검증: 완료
- 사용자 검수: 대기
- 게시: Draft PR
- Fable 5 호출: 없음

## 2. 목표

PR #38 이후 실제 main의 완료·진행·차단 상태를 Product Roadmap에 맞추고, 남은 P2 Gate와 이후 신규 기능을 상태·의존성·외부 blocker·승인 Gate 순으로 재정렬한다.

## 3. 기준선과 source of truth

- 기준 main: `3378998c1097`
- PR #34, #35, #36, #37, #38의 merge 결과와 해당 Task 산출물을 확인했다.
- PR #37의 `PURGE_GUARD_PREDICATE_UNREACHABLE` REDESIGN과 due purge 전체 batch rollback 결정을 유지한다.
- 기존 dirty main worktree는 변경하지 않고 최신 `origin/main`에서 분리한 Task worktree를 사용한다.
- 미래 신규 기능의 canonical planning·review 파일은 아직 없으며 planning 승인과 implementation 승인은 모두 `false`다.

## 4. 포함 범위

- Roadmap 21~25장의 완료·진행 상태, 실행 큐, 추적 대상과 Decision Log 동기화
- 남은 P2 Gate 분리
- 향후 신규 기능의 dependency 중심 실행 순서
- Roadmap 갱신 SOP와 사용자 안내

## 5. 제외 범위

- Backend·Frontend·migration·dependency·script 변경
- Runtime·Persistent UAT·provider·backup 변경
- 신규 기능 planning 또는 구현
- 공용 태블릿·공용 기기 mode·별도 session 정책·sessionStorage 강제 정책
- 알림 채널 matrix 변경
- 사용자 검수 전 Ready 전환과 merge

## 6. 남은 P2 Gate

1. `TASK-UAT-AUTH-HARDEN-001`: privacy-safe evidence gate, isolated Phase A/B, Phase C/D runtime handover. Persistent live user/role/deletion mutation은 `NO_GO`다.
2. `TASK-GOV-002`: Git history 개인정보 risk owner·공개 범위·rewrite 여부 결정. 조사·승인 전 rewrite, filter-repo와 force push를 금지한다.
3. `TASK-NOTIFY-004` 잔여 범위: terminal `Failed` delivery의 관리자 수동 재처리가 P2인지 운영 편의 기능인지 판정한다.
4. 전체 P0/P1/P2 재평가 뒤 사용자 승인으로 신규 기능 Go/No-Go를 결정한다.

## 7. 승인된 실행 순서

- Phase 1: TASK-007A → TASK-007B → TASK-MOBILE-001 → TASK-HOME-001
- Phase 2: TASK-008A → TASK-008B → TASK-009A → TASK-010A
- Phase 3: TASK-011A → TASK-012A → TASK-ADMIN-002 → Pending 유형 관리자 화면 검토
- Phase 4: TASK-013A → TASK-014A → TASK-EXPORT-001 → QR scan landing 검토
- Phase 5: DESIGN-000 → DESIGN-001 이후 화면 통일
- Phase 6: hosting·domain·redirect URI·Teams catalog·provider·data migration·storage·pilot·교육을 포함한 운영 전환

이 순서는 개별 신규 기능의 planning·implementation 승인이 아니다. 모든 `NEW_FEATURE`는 Fable 5 planning, Codex review와 사용자 승인을 별도로 거친다.

## 8. 보존한 정책

- 기능 개발을 시각 디자인보다 먼저 진행하되 기능 Task에서도 loading·empty·error·success feedback, 접근성, 한글 안내, 390px/Teams narrow와 overflow 0을 유지한다.
- 모바일은 동일 URL 적응형 rendering을 기본으로 한다.
- Home은 widget-slot 구조로 현재 data가 있는 항목부터 활성화한다.
- TASK-008A와 TASK-010A는 별도 planning·구현·검증·rollback 단위다.
- 긴급·차단 메일, 에스컬레이션 메일과 Daily Digest 역할을 포함한 현재 알림 채널 matrix는 별도 `POLICY_DECISION` 없이 변경하지 않는다.

## 9. Planning 상태 확인

Repository에는 향후 큐의 `tasks/<task-id>-planning.md`와 Codex review가 없다. 파일 존재만으로 승인 상태를 추정하지 않았으며 모든 미래 기능의 `planningApproved=false`, `implementationApproved=false`를 유지한다.

## 10. Findings

- Roadmap의 TASK-NOTIFY-004가 완료된 claim/lease·retry·attempt·starvation까지 계획으로 남겨 둔 drift를 정정했다.
- TASK-GOV-CODEX-002는 PR #38 merge 승인 상태가 아니라 실제 merge 완료 상태로 정정했다.
- TASK-AUTH-HARDEN-001 코드·Change 001 완료와 남은 controlled UAT를 분리했다.
- 기존 import-order 위반 9건은 범위 밖 format debt/P3 후보이며 P2로 승격하지 않았다.
- 신규 P0/P1/P2: 없음.

## 11. 자동 검증

- 변경 파일 5개 / allowlist 위반 0 / 삭제 0
- `git diff --check` 통과
- Markdown local link·anchor·중복 heading 오류 0
- secret/PII 후보 0
- 기존 Decision Log 누락 0 / 새 행 8
- 추적 번호 중복 0 / 마지막 번호 76
- 알림 채널 matrix와 27장 canonical 지침 변경 0
- 공용 태블릿·공용 기기 tracking/Decision Log 추가 0
- Backend·Frontend·migration·dependency·script·runtime 변경 0

## 12. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Task | 이 문서 | 작성됨 |
| Implementation report | `tasks/gov-roadmap-001-implementation-report.md` | 작성됨 |
| SOP | `tasks/gov-roadmap-001-sop.md` | 작성됨 |
| User manual | `tasks/gov-roadmap-001-user-manual.md` | 작성됨 |
| Roadmap update | `docs/00-product-roadmap.md` | 반영됨 |

## 13. 사용자 검수 체크리스트

- [ ] 완료된 PR #34~#38과 controlled UAT 상태가 실제와 일치한다.
- [ ] PR #37의 purge REDESIGN이 유지되고 정책 대기로 되돌아가지 않았다.
- [ ] 남은 P2 Gate가 auth controlled UAT, Git history risk decision과 Failed manual retry 범위로 구분됐다.
- [ ] 미래 기능 순서와 의존성이 실무 우선순위에 맞다.
- [ ] 미래 기능 planning·implementation 승인이 아직 `false`임이 명확하다.
- [ ] 공용 태블릿·공용 기기 session 정책이 신규 범위로 추가되지 않았다.
- [ ] 현재 알림 채널 matrix가 변경되지 않았다.
- [ ] 사용자 승인 전 PR이 Draft로 유지되고 merge되지 않는다.
