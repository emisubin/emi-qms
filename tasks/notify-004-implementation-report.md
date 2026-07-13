# TASK-NOTIFY-004 Implementation Report

## 1. 목적과 배경

Terminal Failed delivery 수동 재처리가 기존 P2의 필수 보정인지 운영 편의 기능인지 조사하고, 승인된 `POLICY_CORRECTION_AND_DEFER` 정책을 Repository 문서에 반영한다.

Roadmap의 기존 계약은 automatic retry 후 최종 실패가 관리자 페이지에 보여야 한다는 것이었다. Runtime은 이를 충족하지만 TASK-NOTIFY-REL-001 SOP가 존재하지 않는 “승인된 관리자 retry 절차”를 가리켜 문서와 구현이 어긋났다.

## 2. 포함·제외 범위

포함 범위는 정책 결정, terminal Failed/Pending retry 설명 정정, Roadmap 상태·tracking·Decision Log와 5종 산출물이다.

Backend, Frontend, DB, migration, API, UI, runtime configuration, Persistent UAT write와 provider call은 제외했다. Excel, PDF와 첨부파일 영향은 `N/A`다.

## 3. 확인한 실제 구현

- Retry endpoint는 `RetryPendingDeliveriesAsync`를 호출한다.
- Store는 Pending이 아니면 retry를 skip한다.
- Frontend는 선택 항목이 모두 Pending일 때만 재발송 action을 활성화한다.
- Claim query는 attempt count가 retry limit보다 작은 Pending 또는 stale Processing만 선택한다.
- Attempt `(delivery, attempt number)`는 unique다.
- Failed는 permanent error 또는 retry limit 소진 결과다.
- Provider-call-start 이후 crash ambiguity가 있어 외부 전달은 at-least-once다.

## 4. Root cause

Root cause는 `FAILED_RETRY_DOCUMENTATION_DRIFT`다. Claim/lease 구현 문서가 향후 운영 절차를 선행해서 작성됐지만 실제 Failed retry state transition, retry budget과 append-only admin action 계약은 구현되지 않았다.

Automatic retry나 terminal Failed runtime 자체의 결함은 발견하지 않았다.

## 5. 기술적 결정과 검토한 대안

### 선택: POLICY_CORRECTION_AND_DEFER

Failed를 terminal로 유지하고 문서만 실제 코드에 맞춘다. 관리자는 attempt와 오류를 확인하고 acknowledge/dismiss한다. 수동 재처리는 업무 필요성이 확인될 때 별도 NEW_FEATURE로 기획한다.

### 폐기: Broad in-place requeue

Failed를 Pending으로 바꾸는 것만으로 retry limit 소진 row가 claim되지 않는다. Attempt count 초기화는 기존 unique lineage와 누적 의미를 훼손하므로 폐기했다.

### 보류: Restricted retry

Retry budget이 남고 provider ambiguity가 없는 일부 failure만 허용할 수 있지만 전체 terminal Failed를 해결하지 못한다. Non-retryable 분류를 관리자가 뒤집는 정책과 audit가 필요해 별도 신규 기능 범위다.

### 보류: Full manual reprocessing

Manual retry generation, append-only admin action, 원본·새 cycle lineage, duplicate-risk acknowledgement와 반복 제한이 필요하다. Additive migration 가능성이 있어 이번 P2 문서 정정에 포함하지 않았다.

## 6. 변경 파일과 역할

- `tasks/notify-004-planning.md`: 조사, 대안, 승인 정책
- `tasks/notify-004.md`: Task 계약과 검수 checklist
- `tasks/notify-004-implementation-report.md`: 실제 결정·변경·검증 원장
- `tasks/notify-004-sop.md`: 운영 확인·대응·금지사항
- `tasks/notify-004-user-manual.md`: 사용자 관점 설명
- `tasks/notify-rel-001-sop.md`: 구현되지 않은 retry 절차 문구 정정
- `docs/00-product-roadmap.md`: 상태, 실행 큐, tracking과 Decision Log

## 7. 아키텍처·계약 영향

| 영역 | 영향 |
| --- | --- |
| Backend | 변경 없음 |
| Frontend | 변경 없음 |
| DB/Migration | 변경 없음 |
| API | 변경 없음 |
| UI·UX | 변경 없음 |
| 권한 | 변경 없음 |
| Worker/provider | 변경 없음 |
| Workflow | 변경 없음 |
| Excel/PDF/첨부 | 변경 없음 |

## 8. 검증

| 검증 | 적용 | 결과 | 근거 |
| --- | --- | --- | --- |
| Instruction chain | Yes | PASS | Root·종료 정책·Roadmap·validation·privacy와 관련 Task/code 대조 |
| `git diff --check` | Yes | PASS | 문서 whitespace 오류 0 |
| Markdown heading/link/anchor | Yes | PASS | 중복 heading·깨진 local link 0 |
| Secret/PII | Yes | PASS | 후보 0 |
| Changed-file allowlist | Yes | PASS | 승인 문서만 변경 |
| Backend·Frontend·migration diff | Yes | PASS | 변경 0 |
| Backend/Frontend/E2E | No | N/A | 코드·runtime 변경 없는 정책 문서 Task |
| Independent Codex verification | Yes | PASS | 분리된 read-only 검증에서 계약·diff·링크·Finding 재확인 |
| User validation | Yes | PENDING | checklist 미체크 |

## 9. 개인정보·secret

실제 notification, recipient, provider payload, credential, DB row와 Git 개인 metadata를 사용하지 않았다. Repository 코드와 고정 상태명·aggregate만 증빙으로 사용했다.

## 10. Rollback

게시 전에는 이 branch의 문서 diff를 폐기하면 된다. 게시 후에는 문서 commit revert 또는 forward correction을 사용한다. Runtime과 DB가 바뀌지 않아 backup restore, down migration과 provider compensation은 필요 없다.

## 11. Known issue와 후속 Task

- Terminal Failed manual reprocessing은 구현되지 않았으며 의도된 정책이다.
- 업무 필요성이 확인되면 NEW_FEATURE로 다시 기획한다.
- TASK-GOV-HISTORY-REWRITE-001 WIP와 외부 cache blocker는 별도 상태로 유지한다.
- Phase 0 전체 P0/P1/P2 재평가는 history rewrite gate와 본 Task 사용자 검수 이후 진행한다.

## 12. 해결한 업무 문제

운영 문서가 제공하지 않는 Failed retry 기능을 사용하도록 안내하는 문제를 제거했다. 관리자가 안전하지 않은 DB 변경이나 반복 재발송을 시도하지 않도록 automatic retry, terminal Failed와 acknowledge/dismiss 경계를 명확히 했다.

## 13. 시행착오 및 폐기한 접근

상태만 Failed에서 Pending으로 바꾸는 최소안을 검토했으나 retry-limit claim 조건과 attempt unique 때문에 실제 재처리가 되지 않거나 이력을 훼손할 수 있어 폐기했다.

초기 Markdown 검증 one-liner는 로컬 Ruby가 지원하지 않는 collection method를 사용해 실행 자체가 실패했다. 문서 원문을 변경하지 않고 shell·Perl 기반 fixed-count checker로 교체해 diff, link, anchor와 heading 검증을 다시 완료했다.

## 14. 사용자 검수 결과와 남은 항목

사용자는 Option A 정책, 문서 정정 구현과 Draft 게시를 승인했다. 자동 문서 검증과 분리된 Codex read-only 검증은 PASS이며 내용 검수 checklist는 대기 상태다. Ready 전환과 merge 승인은 받지 않았다.

## 15. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | [SOP](notify-004-sop.md) | 작성 완료 |
| User manual | [User manual](notify-004-user-manual.md) | 작성 완료 |
| Roadmap update | [Product Roadmap](../docs/00-product-roadmap.md) | 반영 완료 |
| User validation checklist | [Task 11장](notify-004.md#11-사용자-검수-체크리스트) | 사용자 검수 대기 |
