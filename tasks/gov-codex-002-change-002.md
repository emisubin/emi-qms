# TASK-GOV-CODEX-002 Change 002 — Task Identity와 Roadmap Sequence Gate

## 1. 사용자 발화 기준 증상

Roadmap의 일반 단계명인 “전체 P0/P1/P2 재평가”를 기존 `TASK-GOV-FINDING-GATE-001`과 대조하기 전에 `TASK-GOV-P2-GATE-001`이라는 별도 Task 이름으로 표현했다. 실제로 두 번째 Task·branch·worktree는 생성되지 않았지만, 이름만으로 작업을 시작하면 같은 목적의 Task가 중복되고 Roadmap 순서와 무관한 작업이 갑자기 시작될 수 있다.

## 2. 기대 동작

모든 새 Task는 목적의 semantic identity와 Roadmap의 현재 `Next Gate`를 먼저 확인한다. 같은 목적의 Task가 있으면 기존 Task를 재사용하고, Roadmap 순서와 다르면 명시적 재정렬 승인 전에는 새 Task·branch·worktree·파일을 만들지 않는다.

## 3. 확인된 원인

- 기존 지침은 동일 목적 branch/worktree/PR 확인을 요구하지만 Task ID를 만들기 전에 목표·Finding·변경 경계·산출물을 함께 비교하는 fixed gate가 없다.
- Roadmap의 설명형 queue label과 canonical Task ID를 구분하는 규칙이 없다.
- External blocker가 있는 앞선 Task를 건너뛸 때 필요한 병렬 진행·순서 변경 승인 기록이 명시되지 않았다.

## 4. 포함 범위

- Root `AGENTS.md`의 Task Identity Gate
- Roadmap status·dependency·external blocker·Next Gate 기반 Sequence Gate
- 같은 목적의 기존 Task 재사용과 ambiguous fail-closed 규칙
- Task ID 합성 금지와 base/Roadmap drift 시 gate 재실행
- Fixed projection template
- 기존 Task·Implementation report·Roadmap 상태 갱신

## 5. 제외 범위

- 중앙 Task registry 또는 별도 database
- 제품 코드, migration, dependency, script, runtime과 Persistent UAT
- 기존 Task ID·branch·worktree 자동 rename 또는 정리
- 실제 Fable 5 호출
- Commit, push, PR과 merge
- Fable 5의 round당 질문 수 변경

## 6. 영향 파일

- `AGENTS.md`
- `tasks/_templates/task-identity-gate-template.md`
- `tasks/gov-codex-002.md`
- `tasks/gov-codex-002-implementation-report.md`
- `docs/00-product-roadmap.md`
- 이 change 문서

## 7. 보존할 불변조건

- `NEW_FEATURE`만 Fable 5 deep-interview·planning으로 라우팅한다.
- Instruction chain, Finding, 사용자 승인, Git·runtime·Persistent UAT 경계를 유지한다.
- Roadmap 순서 변경은 사용자 승인과 Roadmap 기록 없이 추정하지 않는다.
- 확인된 P0/P1/P2가 있어도 이름을 임의 생성하거나 별도 Task를 자동 시작하지 않는다.

## 8. 검증 방법

- 같은 목적·다른 ID → `PASS_REUSE`
- 같은 ID·다른 목적 → 기존 Task 재사용 금지
- 같은 목적 후보 2개 → `BLOCKED_AMBIGUOUS`
- 같은 ID·다른 목적 → `BLOCKED_ID_COLLISION`
- 같은 목적 0개·Roadmap 일치 → `PASS_CREATE`
- 같은 목적 0개·Roadmap 불일치 → `BLOCKED_SEQUENCE`
- External blocker 뒤 Task·승인된 병렬 진행 없음 → `BLOCKED_SEQUENCE`
- 기존 Task 재사용이라도 Roadmap 불일치·override 없음 → `BLOCKED_SEQUENCE`
- Generic queue label에서 Task ID 합성 → 금지
- Markdown link·anchor·heading, diff, secret/PII와 changed-file allowlist
- Backend·Frontend·migration·runtime diff 0

## 9. Fable 5 질문 제한 분석

현재 제한은 interview 전체 질문 수가 아니라 한 round에 사용자에게 제시하는 질문 수 1~3개다.

제한을 완전히 없애면 한 번에 넓은 맥락을 수집하고 숙련 사용자가 관련 결정을 일괄 답변해 왕복 횟수를 줄일 수 있다. 반면 질문 과부하, 일부 답변 누락, 서로 모순되는 선택, 얕은 답변, privacy상 불필요한 정보 수집과 잘못된 interview 완료 판정 위험이 커진다.

권장안은 전체 round 수와 총 질문 수에는 상한을 두지 않되, 한 round는 기본 1~3개를 유지하는 것이다. 사용자가 일괄 답변을 명시적으로 원하고 질문들이 하나의 결정 묶음으로 강하게 연결된 경우에만 별도 승인으로 최대 5개까지 허용하는 적응형 방식이 차선이다. 이번 Change에서는 현재 1~3개 규칙을 변경하지 않는다.

## 10. 사용자 승인 상태

- approved: true
- approvedAt: 2026-07-14
- selectedOption: `B_TASK_IDENTITY_GATE`
- roadmapSequenceGateApproved: true
- fableQuestionLimitChangeApproved: false
- publishingApproved: true
