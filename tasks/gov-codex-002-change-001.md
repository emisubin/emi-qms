# TASK-GOV-CODEX-002 Change 001 — 신규 기능 Deep Interview Gate

## 1. 사용자 발화 기준 증상

현재 라우터는 `NEW_FEATURE`를 판정하면 Codex가 바로 Fable 5 planning을 호출한다. Fable 5가 사용자 업무를 직접 interview하는 단계가 없어 실제 업무 맥락과 선택지가 충분히 확인되기 전에 planning이 시작될 수 있다.

## 2. 기대 동작

신규 기능은 Fable 5가 deep-interview와 선택지 비교·권장안을 진행하고, 사용자가 Fable 요약을 확인한 뒤 Fable 5가 planning까지 작성한다. Codex는 안전한 호출, 질문·답변 relay와 Repository 기록을 담당한다.

## 3. 확인된 원인

Root `AGENTS.md`의 신규 기능 순서, `CLAUDE.md` source of truth와 planning template에 interview artifact와 완료 Gate가 없다.

## 4. 포함 범위

- Fable 5 deep-interview 질문·선택지·권장안 규칙
- Codex의 비개입 relay·기록 경계
- `tasks/<task-id>-interview.md` artifact와 template
- 사용자 확인과 blocking decision Gate
- Fable 5의 confirmed interview 입력·fail-closed 규칙
- 기존 Task·Implementation report·Roadmap 상태 갱신

## 5. 제외 범위

- 실제 신규 기능 interview 또는 Fable 호출
- 제품 코드, migration, dependency, script, runtime과 Persistent UAT
- 기존 planning 승인 상태 변경
- Commit, push, PR과 merge

## 6. 영향 파일

- `AGENTS.md`
- `CLAUDE.md`
- `tasks/_templates/new-feature-interview-template.md`
- `tasks/_templates/new-feature-planning-template.md`
- `tasks/gov-codex-002.md`
- `tasks/gov-codex-002-implementation-report.md`
- `docs/00-product-roadmap.md`
- 이 change 문서

## 7. 보존할 불변조건

- `NEW_FEATURE`만 Fable 5로 라우팅한다.
- Fable은 read-only이며 Repository 파일과 Codex workflow를 재귀 실행하지 않는다.
- Interview 확인은 planning·implementation 승인과 분리한다.
- 사용자 승인 전 source 구현, 게시와 merge를 수행하지 않는다.

## 8. 검증 방법

- 9개 taskType route dry run
- Interview 미완료 상태에서 Fable `QUESTIONS_REQUIRED` 또는 `SUMMARY_CONFIRMATION_REQUIRED`
- 사용자 확인·blocking decision 0인 interview에서만 planning `DRAFT`
- Markdown link·anchor·heading, diff, secret/PII와 changed-file allowlist
- Backend·Frontend·migration·runtime diff 0

## 9. 기존 계약 변경 여부

Fable을 planning 전용에서 deep-interview+planning owner로 확장하고 Codex는 orchestration·review 역할을 유지한다. 제품 정책과 기능 범위는 변경하지 않는다.

## 10. 사용자 승인 상태

- approved: true
- approvedAt: 2026-07-14
- publishingApproved: true
- userCorrection: Fable 5가 deep-interview와 planning을 모두 담당
