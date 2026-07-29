# TASK-NOTIFY-REPROCESS-001 — Codex 내용 Review

- reviewSource: `tasks/notify-reprocess-001-planning.md`
- reviewer: `CODEX`
- conclusion: `APPROVE_WITH_RESOLUTIONS`
- openBlockingDecisionCount: `0`

## 유지·추가·보류·제거

- 유지: generation lineage, append-only event, admin-only, expected-generation 동시성, 실제 provider 0.
- 추가: 전역 attempt 번호와 generation attempt count 분리, batch 100건 all-or-nothing, always-on duplicate-risk acknowledgement, 최대 generation 5, 이전 handling snapshot.
- 보류: provider별 idempotency 조회·취소, Sent 재발송, 운영 queue replay.
- 제거: 기존 Failed 행을 attempt 0으로 단순 초기화하는 방식.

## Finding resolution

- `REPROCESS_RETRY_BUDGET_RESET`: generation count 분리로 해소.
- `REPROCESS_DUPLICATE_AMBIGUITY`: 항상 확인+attempt의 provider-call 시각 보존으로 해소.
- `REPROCESS_CONCURRENT_ADMIN`: expected generation과 row lock으로 해소.
- `REPROCESS_PARTIAL_BATCH`: all-or-nothing validation으로 해소.
