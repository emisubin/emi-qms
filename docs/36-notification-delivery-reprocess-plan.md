# 최종 실패 알림 수동 재처리 — Codex 2차 기획

- Task: `TASK-NOTIFY-REPROCESS-001`
- 작성자: `CODEX_SECOND_PLANNING`
- 근거: `tasks/notify-reprocess-001-interview.md`, `tasks/notify-reprocess-001-planning.md`, `tasks/notify-reprocess-001-review.md`
- blockingDecisionCount: `0`

## 최종 구현 계약

System Administrator가 terminal `Failed` delivery를 기존 시도 이력을 보존한 채 새 retry generation으로 재처리한다.

- `0049` additive migration으로 delivery generation·generation attempt count, attempt generation, append-only reprocess event를 추가한다.
- worker의 retry limit은 현재 generation attempt count에 적용하고 전역 attempt number는 계속 증가한다.
- `POST /api/admin/notification-deliveries/reprocess-failed`는 최대 100건의 `{deliveryId, expectedGeneration}`과 10~500자 사유, `duplicateRiskAcknowledged=true`를 요구한다.
- 입력 전체를 고유 ID로 정규화하고 row lock 후 `Failed`, claim 없음, expected generation 일치, generation<5를 모두 검증한다. 하나라도 실패하면 `409`이며 상태·event 모두 변경하지 않는다.
- 성공 시 새 generation, generation attempt count 0, Pending/즉시 실행, handling Open으로 전환하고 append-only event를 같은 transaction에 기록한다.
- 목록·상세에는 generation과 generation별 시도·재처리 event를 표시한다. Desktop은 선택 재처리, 모바일은 상세 단일 재처리를 우선한다.
- 중복 가능성을 없앴다고 표현하지 않으며 실제 provider는 호출하지 않는다.

## 검증

- 관리자/비관리자 권한, terminal 상태 allowlist, 사유·확인·최대 generation, stale expected generation, concurrent 요청 1회 성공, batch atomicity를 검증한다.
- 기존 Pending retry와 automatic retry·claim/lease·attempt lineage 회귀를 검증한다.
- Desktop/390px에서 generation·중복 경고·사유·상세 이력이 명확해야 한다.
