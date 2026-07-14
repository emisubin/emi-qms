# TASK-GOV-CODEX-002 — Fable 5 신규 기능 기획과 Codex-only 작업 라우터

## 1. 상태

- Task 유형: `DOCS_GOVERNANCE`
- 구현: 완료
- 자동 검증: 완료
- 사용자 검수: 완료
- 게시·merge: PR #38 squash merge 승인
- Fable 5 호출: 없음
- Change 001: Fable 5 deep-interview+planning Gate 보정·자동 검증·사용자 검수 완료 / merge 승인
- Change 002: Task Identity+Roadmap Sequence Gate 보정·자동 검증·사용자 검수 완료 / merge 승인

## 2. 목표

신규 기능만 Fable 5 기획 단계로 보내고, 기존 기능의 수정·보강·UAT·문서·정리·정책 결정은 Codex-only 조사와 승인 흐름으로 처리한다. 기획, 검토, 구현, 독립 검증과 사용자 승인 경계를 Repository 지침으로 고정한다.

## 3. 기준선

- PR #32가 Root·Backend·Frontend·Scripts 지침, 종료 정책, validation matrix, privacy-safe evidence와 project-local Rules를 main에 이관했다.
- Root `AGENTS.md`에는 Task 유형별 Fable/Codex 라우터가 없었다.
- Fable 전용 `CLAUDE.md`는 main에 없었다.
- 기존 root worktree에 다른 장문 초안이 있으나 과거 UAT WIP와 혼재되어 있고 canonical main 지침을 통째로 대체하므로 본 Task 범위에서 수정·정리하지 않는다.

## 4. 확정 라우팅

- `NEW_FEATURE`: Fable 5 deep-interview → 사용자 요약 확인 → Fable 5 read-only planning → Codex review → 사용자 승인 → 새 Codex 구현 → 분리된 Codex 검증 → 사용자 게시·merge 승인
- `APPROVED_FEATURE_IMPLEMENTATION`: Fable 재호출 없이 Codex-only 구현
- `BUGFIX`, `P2_REMEDIATION`, `SECURITY_HARDENING`, `UAT_RUNTIME`, `DOCS_GOVERNANCE`, `HOUSEKEEPING`, `POLICY_DECISION`: Codex 조사 → 사용자 승인 → 새 Codex 구현 → 분리된 Codex 검증 → 사용자 게시·merge 승인
- Codex-only 조사에서 신규 제품 능력이 필요해지면 `NEW_FEATURE`, 기존 범위의 정책 선택이면 `POLICY_DECISION`으로 재분류하고 중단한다.

## 5. 포함 범위

- Root `AGENTS.md` Task 유형 라우터
- 신규 기능, Codex-only, 수정 요청과 세션 분리 규칙
- Fable 5 read-only 호출 경계
- Fable 5 deep-interview와 사용자 확인 Gate
- 신규 기능 interview template와 artifact 경로
- Fable 전용 `CLAUDE.md`
- planning·review·change·implementation report 역할 구분
- Task 종료 산출물 상태와 Roadmap 추적
- 대표 instruction-chain dry run
- 같은 목적 Task semantic identity와 Roadmap Sequence Gate
- Fixed projection template과 기존 canonical Task 재사용

## 6. 제외 범위

- Backend·Frontend·migration·script·runtime·Persistent UAT 변경
- 실제 신규 기능 기획 또는 구현
- 실제 Fable 5 기획 호출
- 기존 dirty root worktree와 historical branch 정리
- project-local Rules 변경
- 사용자 승인 전 Ready 전환과 merge

## 7. 보존할 불변조건

- main 직접 작업·push 금지와 Task worktree 격리
- Persistent UAT, migration, provider와 runtime 승인 경계
- P0/P1/P2 Finding gate
- 개인정보·secret과 fixed projection
- 명시적 allowlist staging
- 자동 검증과 사용자 검수 상태 분리
- Task 종료 5종 산출물 추적

## 8. 운영 SOP

1. Instruction chain과 Roadmap 실행 큐를 읽고 현재 `Next Gate`를 확인한다.
2. 요청의 goal·Finding·변경 경계·불변조건·산출물로 purpose identity를 만들고 기존 Task·branch·worktree·PR을 검색한다.
3. Task Identity Gate가 `PASS_REUSE`면 기존 Task의 다음 change를 사용하고, `PASS_CREATE`일 때만 새 Task ID와 자원을 만든다.
4. Roadmap 순서가 다르거나 같은 목적 후보가 모호하면 사용자 재정렬 결정 전 중단한다.
5. 요청의 실제 의미와 Repository 상태를 읽고 `taskType`을 선택한다.
6. `NEW_FEATURE`면 Fable 5가 관련 질문을 1~3개씩 작성하고 선택지·영향·권장안을 설명한다.
7. Codex는 질문을 사용자에게 전달하고 답변을 `tasks/<task-id>-interview.md`에 의미 변경 없이 기록한다.
8. Fable 5가 누적 답변을 읽어 추가 질문 또는 확인용 요약을 작성한다.
9. 사용자가 Fable 요약을 확인하고 blocking decision이 0일 때만 Fable 5 planning을 시작한다.
10. Fable 실행 경계를 보장할 수 없으면 호출하지 않고 중단한다.
11. Codex가 planning을 실제 코드·Roadmap·Decision Log·interview와 대조해 review한다.
12. 사용자 승인 전에 구현으로 넘어가지 않는다.
13. 승인된 기능 구현과 모든 비신규 작업은 Codex-only 흐름을 사용한다.
14. 실질적 수정 요청은 `tasks/<task-id>-change-###.md`로 계약을 고정한다.
15. 구현과 독립 검증은 별도 Codex 세션을 기본으로 한다.
16. 사용자 검수와 게시·merge 승인을 분리한다.

## 9. 사용자 안내

- 새 업무 흐름·화면·데이터 개념·외부 연동·권한 능력을 요청하면 Fable 5가 업무 맥락을 먼저 interview한다. 선택 사항은 쉬운 비교와 권장안을 제공한다.
- Codex는 질문·답변을 전달·기록하며 사용자가 interview 요약을 확인한 뒤 Fable planning과 Codex 검토 결과를 받는다.
- 버그, P2, 보안 보강, UAT, 문서, 정리와 기존 정책 선택은 Fable 없이 Codex가 조사한다.
- “승인된 기능을 구현하라”는 요청은 다시 기획하지 않는다.
- 조사·기획 결과를 승인하기 전에는 source 구현이 시작되지 않는다.
- 구현 뒤에도 독립 검증과 사용자 검수·merge 승인이 별도로 필요하다.
- 새 Task를 시작하기 전 기존 같은 목적의 Task와 Roadmap의 현재 순서를 대조한다. 같은 목적이면 기존 Task를 이어가고, 순서가 다르면 이유와 선택지를 먼저 안내한다.

## 10. Rollback

`AGENTS.md`의 Task 라우터 section과 `CLAUDE.md`를 함께 revert한다. Code, DB, migration와 runtime rollback은 없다. 기존 PR #32 지침 구조는 변경하지 않는다.

## 11. Findings

- 신규 P0/P1/P2: 없음
- 기존 root 장문 초안: 경쟁 WIP로 보존하며 본 Task에 포함하지 않음
- `docs/task-close-process-guidelines`: remote에는 있으나 PR 없는 historical branch이며 별도 cleanup 승인 전 유지

## 12. 5종 산출물 상태

| 산출물 | Canonical 위치 | 상태 |
| --- | --- | --- |
| Implementation report | `tasks/gov-codex-002-implementation-report.md` | 작성됨 / 자동 검증 완료 |
| SOP | 이 문서 8장 | 작성됨 |
| User manual | 이 문서 9장 | 작성됨 |
| Roadmap update | `docs/00-product-roadmap.md` | 반영됨 |
| User validation checklist | 이 문서 13장 | 사용자 검수 완료 |

## 13. 사용자 검수 체크리스트

- [x] `NEW_FEATURE`만 Fable 5로 라우팅되는지 확인
- [x] 승인된 기능 구현과 BUGFIX/P2/SECURITY/UAT/DOCS/HOUSEKEEPING/POLICY가 Codex-only인지 확인
- [x] Fable이 Repository 파일과 Codex workflow를 재귀 실행하지 않는지 확인
- [x] planning·review·change·implementation report 역할이 구분되는지 확인
- [x] 사용자 승인 전 구현, Ready 전환과 merge가 금지되는지 확인
- [x] 기존 Repository 안전·Finding·5종 산출물 규칙이 유지되는지 확인
- [x] `NEW_FEATURE`에서 Fable 5 deep-interview와 사용자 요약 확인 후에만 Fable 5 planning이 시작되는지 확인
- [x] Interview 완료가 planning·implementation 승인으로 오인되지 않는지 확인
- [x] 같은 목적의 다른 Task 이름이 제안돼도 기존 canonical Task를 재사용하는지 확인
- [x] Roadmap의 현재 Next Gate와 다른 Task가 사용자 재정렬 승인 없이 시작되지 않는지 확인
- [x] 새 채팅에서도 instruction chain과 Task Identity Gate가 첫 변경 전에 다시 실행되는지 확인
