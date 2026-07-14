# TASK-GOV-CODEX-002 Implementation Report

## 1. 해결한 업무 문제

신규 기능 기획과 기존 기능 보강이 같은 프롬프트 흐름에 섞여 불필요한 재기획, 승인 경계 누락과 역할 재귀가 발생할 수 있었다. Task 유형을 먼저 분류하고 신규 기능만 Fable 5 planning으로 보내도록 Repository 지침을 보강했다.

## 2. 실제 변경

- Root `AGENTS.md`: Task 유형, Fable 5 신규 기능 흐름, Codex-only 흐름, 수정 요청, 문서 역할과 세션 분리 추가
- `CLAUDE.md`: Fable 5의 신규 기능 기획 전용 역할, read-only 경계, source of truth와 출력 계약 추가
- Root `AGENTS.md`: 새 Task 생성 전 semantic identity와 Roadmap Sequence Gate 추가
- Task template: fixed projection과 `PASS_REUSE`·`PASS_CREATE`·blocked 상태 추가
- Task·Implementation report와 Roadmap: 종료 산출물·상태 추적

Backend, Frontend, migration, dependency, script, runtime과 Persistent UAT source diff는 없다.

## 3. 기술적 결정과 대안

### 선택안

PR #32의 66줄 Root 지침을 유지하고 Task router section만 추가했다. `CLAUDE.md`는 Fable 역할만 담고 Codex workflow 실행 책임을 갖지 않는다.

### 폐기한 대안

- 기존 root worktree의 336줄 `AGENTS.md` 전체 사용: canonical main 구조를 중복하고 과거 UAT WIP와 혼재해 폐기
- 경미한 수정의 무승인 즉시 처리: canonical 승인·종료 정책과 충돌해 폐기
- `docs/tasks/` 신규 경로: 기존 `tasks/` convention과 충돌해 폐기
- Fable output을 tracked planning 파일로 직접 redirect: 단일 작성자와 privacy-safe 검증 경계를 우회해 폐기

## 4. Fable 5 안전 경계

- model: `fable-5`
- Repository read-only 도구만 허용
- safe mode, plan permission, session persistence·slash command 비활성화, 빈 strict MCP config
- stdout/stderr는 Repository 밖 private artifact로 capture
- 호출 옵션이 지원되지 않거나 read-only를 보장할 수 없으면 fail-closed
- Fable은 planning 전문만 반환하며 Codex가 검증 후 `apply_patch`로 기록

실제 Fable 기획 호출은 이번 DOCS_GOVERNANCE Task 범위에서 수행하지 않았다.

## 5. 검증 결과

- 별도 Codex read-only session 대표 route: 9/9
- Planning/review path, Fable 재귀 차단과 승인 전 구현 금지: 통과
- Router static contract: 11/11
- Claude CLI의 Fable read-only 필수 option 지원: 8/8
- `git diff --check`: 통과
- actionlint: 통과
- Markdown local link·anchor·heading: 오류 0
- Secret/PII candidate: 0
- Changed-file allowlist: 5개 일치 / 범위 밖 0 / 삭제 0
- Backend·Frontend·migration·script·runtime 변경: 0
- Fable 실제 기획 호출, DB write, runtime restart와 provider 호출: 0

## 6. 영향

- Backend/API/DB/Migration/UI/UX: N/A — 관련 파일 변경 없음
- Excel/PDF/첨부파일/notification workflow: N/A — 관련 파일 변경 없음
- Runtime/Persistent UAT/provider: N/A — 기동·write·호출 없음
- 사용자 영향: Task 요청의 기획·승인·검증 순서만 명확해지며 제품 화면 변화 없음

## 7. 개인정보·secret

실제 identity, credential, DB/API/browser 원문을 사용하지 않는다. Dry run 결과는 fixed enum·boolean·count만 기록한다.

## 8. 시행착오 및 폐기한 접근

동일 목적의 두 초안이 서로 다른 worktree에 존재했다. 파일 수가 많은 기존 root 초안을 기준으로 삼지 않고, 최신 main에서 시작한 전용 worktree 후보를 canonical safety 구조에 최소 추가하는 방식으로 선택했다.

첫 Codex session dry run은 모호한 “purge guard 수정” 문구를 P2가 아닌 일반 bugfix로 해석해 exact match가 8/9였다. 지침 결함으로 단정하지 않고 입력을 “확인된 P2 Finding”으로 명확히 하고 route enum을 고정해 재실행했으며 9/9를 확인했다. 실제 Task 분류는 요청 문구뿐 아니라 Repository Finding 상태를 함께 사용한다.

## 9. Rollback

Root Task router section, `CLAUDE.md`, Task 문서와 Roadmap entry를 함께 revert한다. Runtime·DB rollback은 없다.

## 10. 사용자 검수 결과와 남은 항목

- 사용자 검수: 완료
- 게시·merge: PR #38 squash merge 승인
- 기존 root WIP와 historical branch 정리: 별도 HOUSEKEEPING 승인 대상

## 10.1 Change 001 — Deep Interview Gate

사용자 정정에 따라 신규 기능 workflow 앞에 Fable 5 deep-interview를 추가했다. Fable 5가 질문을 1~3개씩 진행하고 선택지의 영향과 권장안을 설명한다. Codex는 질문·답변 relay와 privacy-safe 기록만 담당한다. 사용자 확인이 끝난 interview artifact와 blocking decision 0이 있을 때만 Fable 5가 planning을 시작한다.

- Interview 위치: `tasks/<task-id>-interview.md`
- Template: `tasks/_templates/new-feature-interview-template.md`
- Fable interview 상태: `QUESTIONS_REQUIRED` → `SUMMARY_CONFIRMATION_REQUIRED` → `COMPLETED_CONFIRMED`
- Session persistence 없이 interview 문서를 round별 source of truth로 재사용
- Interview 완료 시에도 `planningApproved=false`, `implementationApproved=false`
- 실제 신규 기능 interview와 Fable 호출: 0
- 제품 코드·runtime·Persistent UAT 영향: 0
- 사용자 검수: 완료
- 게시·merge 승인: 완료

Change 001 자동 검증 결과:

- 변경 파일 8, allowlist 위반 0, staged 0
- `git diff --check` 통과
- Markdown missing link/anchor·duplicate heading `0/0/0`
- Added secret/PII candidate `0/0`
- Task type enum 9개 누락 0, `NEW_FEATURE` 전용 Fable rule 1
- Fable interview-before-planning ordering: true
- Fable interview state case 4/4 통과
- Recursive workflow block 확인, `docs/tasks/` 생성 0
- Backend·Frontend·migration·dependency·script·runtime diff 0
- 실제 Fable 호출, runtime·Persistent UAT mutation 0
- 독립 Codex 검증: 별도 세션 대기

## 10.2 Change 002 — Task Identity와 Roadmap Sequence Gate

동일 목적의 Task를 다른 이름으로 중복 생성하거나 Roadmap 순서를 건너뛰는 문제를 막기 위해 새 Task 자원 생성 전 fail-closed gate를 추가했다.

- Purpose identity: 업무 목표, root Finding, 변경·검증 경계, 보존 불변조건과 예상 산출물
- 검색 범위: Task 산출물, Roadmap·Decision Log·추적 항목, branch, worktree와 open/merged PR
- 같은 목적 하나+Roadmap 일치 또는 승인된 override: 기존 canonical Task와 다음 `change-###` 재사용
- 같은 목적 둘 이상: `BLOCKED_AMBIGUOUS`
- 같은 ID·다른 목적: `BLOCKED_ID_COLLISION`
- 같은 목적 없음+Roadmap 일치: `PASS_CREATE`
- 재사용·신규 생성 모두 Roadmap 불일치: 명시적 재정렬 승인과 Roadmap 기록 전 `BLOCKED_SEQUENCE`
- 일반 queue label에서 Task ID 합성 금지
- Base·Roadmap·instruction·PR drift 시 gate 재실행

이번 문제에서는 `TASK-GOV-P2-GATE-001`이 물리 Task로 생성되지는 않았고 기존 `TASK-GOV-FINDING-GATE-001`의 목적을 잘못 축약한 별칭이었다. 기존 canonical Task를 재사용하는 것으로 정정한다.

Fable 5 질문 제한은 전체 질문 수 제한이 아니라 round당 1~3개 제한이다. 제한 해제는 왕복을 줄일 수 있으나 누락·모순·피로·과수집과 잘못된 완료 판정 위험이 커 현재 규칙을 유지했다. Adaptive 최대 5개는 별도 승인 후보로 남긴다.

Change 002 자동 검증 결과:

- Task Identity decision table: 8/8
- Static contract: 9/9
- `git diff --check`: 통과
- Markdown 10개 파일 local link·anchor·duplicate heading: `0/0/0`
- Secret/PII candidate: email·UUID·token `0/0/0`
- Changed-file allowlist: 10개 일치 / 범위 밖 0 / staged 0
- Backend·Frontend·migration·dependency·script·runtime 변경: 0
- Noncanonical `TASK-GOV-P2-GATE-001` Task file·branch·worktree: `0/0/0`
- Round당 Fable 질문 수 규칙: AGENTS·CLAUDE 각 1개 / adaptive 최대 5개 적용 0
- 제품 코드·runtime·Persistent UAT 영향: 0
- 실제 Fable 호출: 0
- 사용자 승인: B안과 Roadmap Sequence Gate 승인
- 사용자 검수: 완료
- 게시·merge 승인: 완료

## 11. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성됨 / 자동 검증 완료 |
| SOP | `tasks/gov-codex-002.md` 8장 | 작성됨 |
| User manual | `tasks/gov-codex-002.md` 9장 | 작성됨 |
| Roadmap update | `docs/00-product-roadmap.md` | 반영됨 |
| User validation checklist | `tasks/gov-codex-002.md` 13장 | 사용자 검수 완료 |
