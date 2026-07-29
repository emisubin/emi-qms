# TASK-NOTIFY-REPROCESS-001 — Codex 1차 기획

- author: `CODEX_FALLBACK_PLANNING`
- interviewSource: `tasks/notify-reprocess-001-interview.md`
- openBlockingDecisionCount: `0`

## 권장안

- 동일 delivery에 `current_generation`과 `generation_attempt_count`를 추가하고 기존 전역 `attempt_count`·attempt 번호는 유지한다.
- attempt 원장에는 generation을 기록한다. 새 generation은 generation별 자동 retry budget을 0부터 시작하지만 전역 attempt 번호를 초기화하지 않는다.
- terminal `Failed`만 재처리하며 expected generation CAS, claim 없음, 관리자 권한을 transaction에서 재검증한다.
- 사유는 10~500자 필수, 중복 가능성 확인은 항상 필수, generation은 최대 5로 제한한다.
- 선택 재처리는 최대 100건이며 하나라도 stale·부적격이면 전체를 변경하지 않는 all-or-nothing 계약을 사용한다.
- append-only event에 actor·사유·이전/새 generation·이전 오류·이전 handling·중복 확인을 남기고 현재 handling은 `Open`으로 되돌린다.
- 기존 `/retry`는 Pending의 시각 앞당김 의미를 유지하고 `/reprocess-failed`를 별도로 추가한다.
- 실제 provider는 호출하지 않고 migration·store/API·desktop/mobile UI·fake/dry-run tests까지만 구현한다.
