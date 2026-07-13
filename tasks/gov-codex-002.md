# TASK-GOV-CODEX-002 — Fable 5 신규 기능 기획과 Codex-only 작업 라우터

## 1. 상태

- Task 유형: `DOCS_GOVERNANCE`
- 구현: 완료
- 자동 검증: 완료
- 사용자 검수: 완료
- 게시·merge: PR #38 squash merge 승인
- Fable 5 호출: 없음

## 2. 목표

신규 기능만 Fable 5 기획 단계로 보내고, 기존 기능의 수정·보강·UAT·문서·정리·정책 결정은 Codex-only 조사와 승인 흐름으로 처리한다. 기획, 검토, 구현, 독립 검증과 사용자 승인 경계를 Repository 지침으로 고정한다.

## 3. 기준선

- PR #32가 Root·Backend·Frontend·Scripts 지침, 종료 정책, validation matrix, privacy-safe evidence와 project-local Rules를 main에 이관했다.
- Root `AGENTS.md`에는 Task 유형별 Fable/Codex 라우터가 없었다.
- Fable 전용 `CLAUDE.md`는 main에 없었다.
- 기존 root worktree에 다른 장문 초안이 있으나 과거 UAT WIP와 혼재되어 있고 canonical main 지침을 통째로 대체하므로 본 Task 범위에서 수정·정리하지 않는다.

## 4. 확정 라우팅

- `NEW_FEATURE`: Fable 5 read-only planning → Codex review → 사용자 승인 → 새 Codex 구현 → 분리된 Codex 검증 → 사용자 게시·merge 승인
- `APPROVED_FEATURE_IMPLEMENTATION`: Fable 재호출 없이 Codex-only 구현
- `BUGFIX`, `P2_REMEDIATION`, `SECURITY_HARDENING`, `UAT_RUNTIME`, `DOCS_GOVERNANCE`, `HOUSEKEEPING`, `POLICY_DECISION`: Codex 조사 → 사용자 승인 → 새 Codex 구현 → 분리된 Codex 검증 → 사용자 게시·merge 승인
- Codex-only 조사에서 신규 제품 능력이 필요해지면 `NEW_FEATURE`, 기존 범위의 정책 선택이면 `POLICY_DECISION`으로 재분류하고 중단한다.

## 5. 포함 범위

- Root `AGENTS.md` Task 유형 라우터
- 신규 기능, Codex-only, 수정 요청과 세션 분리 규칙
- Fable 5 read-only 호출 경계
- Fable 전용 `CLAUDE.md`
- planning·review·change·implementation report 역할 구분
- Task 종료 산출물 상태와 Roadmap 추적
- 대표 instruction-chain dry run

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

1. 요청의 실제 의미와 Repository 상태를 읽고 `taskType`을 선택한다.
2. `NEW_FEATURE`만 Fable 5 read-only planning으로 보낸다.
3. Fable 실행 경계를 보장할 수 없으면 호출하지 않고 중단한다.
4. Codex가 planning을 실제 코드·Roadmap·Decision Log와 대조해 review한다.
5. 사용자 승인 전에 구현으로 넘어가지 않는다.
6. 승인된 기능 구현과 모든 비신규 작업은 Codex-only 흐름을 사용한다.
7. 실질적 수정 요청은 `tasks/<task-id>-change-###.md`로 계약을 고정한다.
8. 구현과 독립 검증은 별도 Codex 세션을 기본으로 한다.
9. 사용자 검수와 게시·merge 승인을 분리한다.

## 9. 사용자 안내

- 새 업무 흐름·화면·데이터 개념·외부 연동·권한 능력을 요청하면 Fable 기획과 Codex 검토 결과를 먼저 받는다.
- 버그, P2, 보안 보강, UAT, 문서, 정리와 기존 정책 선택은 Fable 없이 Codex가 조사한다.
- “승인된 기능을 구현하라”는 요청은 다시 기획하지 않는다.
- 조사·기획 결과를 승인하기 전에는 source 구현이 시작되지 않는다.
- 구현 뒤에도 독립 검증과 사용자 검수·merge 승인이 별도로 필요하다.

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
