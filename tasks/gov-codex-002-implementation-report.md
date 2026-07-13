# TASK-GOV-CODEX-002 Implementation Report

## 1. 해결한 업무 문제

신규 기능 기획과 기존 기능 보강이 같은 프롬프트 흐름에 섞여 불필요한 재기획, 승인 경계 누락과 역할 재귀가 발생할 수 있었다. Task 유형을 먼저 분류하고 신규 기능만 Fable 5 planning으로 보내도록 Repository 지침을 보강했다.

## 2. 실제 변경

- Root `AGENTS.md`: Task 유형, Fable 5 신규 기능 흐름, Codex-only 흐름, 수정 요청, 문서 역할과 세션 분리 추가
- `CLAUDE.md`: Fable 5의 신규 기능 기획 전용 역할, read-only 경계, source of truth와 출력 계약 추가
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

- 사용자 검수: 대기
- 게시·merge: 별도 승인 확인 전 미수행
- 기존 root WIP와 historical branch 정리: 별도 HOUSEKEEPING 승인 대상

## 11. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성됨 / 자동 검증 완료 |
| SOP | `tasks/gov-codex-002.md` 8장 | 작성됨 |
| User manual | `tasks/gov-codex-002.md` 9장 | 작성됨 |
| Roadmap update | `docs/00-product-roadmap.md` | 반영됨 |
| User validation checklist | `tasks/gov-codex-002.md` 13장 | 작성됨 / 사용자 검수 대기 |
