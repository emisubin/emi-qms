# TASK-GOV-CODEX-002 Change 021 — PMS 하네스 재설계 문서와 독립 검토

## 요청과 범위

- 요청일: 2026-09-09
- 사용자 요청: “모델 호출, 실행환경 … 개발 방식 … 품질과 권한을 지키는 장치 … 프로젝트 지식 … 완료 검증”을 PMS에 맞게 재설계하고, Markdown 작성 후 충돌·필요성·과잉 여부를 다시 분석해 보고.
- 이해한 목표: 불필요한 절차를 줄이면서 데이터·권한·운영 안전을 보존하는, 실제 적용 가능한 하네스 교체 설계와 검토 결과를 작성한다.
- 상태: `DESIGN_REVIEW_COMPLETE` — 문서 설계와 독립 내용 검토 완료. 활성 하네스 적용은 아님.
- 현재 적용 범위 질문은 답변 미수신. 명시적으로 승인된 문서 설계·재검토를 완료하는 범위로 진행하며 기존 WIP는 보존한다.
- local commit: 완료된 승인 범위 자동 commit에 대한 기존 사용자 승인을 적용. 원격 push·PR·merge·배포는 이번 요청 범위에 없음.

## 시작 확인

- proposedTaskId / canonicalTaskId: `TASK-GOV-CODEX-002`
- taskType: `DOCS_GOVERNANCE`
- instructionChainRead: `true`
- instructionConflictCount: 활성 문서 간 복수 충돌을 감사 대상으로 확인. 영향받는 실행 정책을 임의 선택해 적용하지 않음.
- planningOwner / implementationOwner: parent, 문서 전용이므로 Sol 구현 위임 대상 아님.
- verificationOwner: fresh `gpt-6-astra/high`, 도구가 반환하는 실제 모델은 별도 기록.
- roadmapExpectedTaskId: `TASK-GOV-CODEX-002`
- roadmapNextGate: 기존 Change 020 검수 대기. 현재 사용자가 같은 governance 재설계를 명시 요청.
- roadmapSequenceMatch: `false` — 제품 실행 큐의 다음 기능을 선택한 작업이 아님.
- explicitRoadmapOverrideApproved: `true` — 기존 governance 우선 진행 승인 및 이번 동일 목적 재설계 명시 요청 범위.
- samePurposeMatchCount: `1`; reuseExistingTask: `true`; gateStatus: `PASS_REUSE`.
- experimentStandingInstructionApplies: `false`; experimentLedgerSelectedTask: `NONE`; policyInputResolution: `N/A`.
- branch: `fix/task-gov-codex-002-instruction-clarity`
- 작업 폴더 HEAD: `574cea66f602eb65eb1d110801d331151731b0a6`
- 조회한 원격 main: `c3a3c79374babc840dca054bd1a05237a2c49685`
- 사전 상태: staged 0 / tracked 수정 17 / untracked 32. 새 branch/worktree를 생성하거나 전환하지 않음.
- 적용 하위 지침: 새 문서는 docs/development와 tasks 경로이며 추가 AGENTS 없음. backend/frontend/scripts AGENTS는 변경 대상 규칙의 감사 자료로 읽음.

동일 목적 검색은 Roadmap·governance Task/change·현재 branch/worktree 및 관련 open/merged PR을 대조했다. 새로운 제품 Task를 생성하거나 완료된 오산 기능을 다시 선택하지 않는다. Roadmap·종료 정책·검증표·개인정보 지침·현재 Task/change/report·실험 원장·관련 하위 지침을 확인했다.

## 변경 allowlist

- `docs/development/pms-harness-redesign.md`
- `docs/development/pms-harness-redesign-review.md`
- `tasks/gov-codex-002-change-021.md`

세 파일은 신규 문서다. 기존 AGENTS·로드맵·모델 설정·스크립트·사용자 WIP·전역 스킬을 변경하지 않는다. 제품 코드·DB·runtime·Azure·provider·원격 Git mutation을 하지 않는다.

## 설계 및 완료 조건

1. 모든 요청 영역을 PMS의 최신 main 구현·승인된 후속 계약과 대조해 설계한다.
2. 지침 문제는 인용·경로·행동 영향·구체 편집안·권한 확대 여부를 보고한다.
3. Root 교체 원문과 파일별 적용 경계를 제공한다. 운영/병합 승인과 데이터·권한 보호를 유지한다.
4. 독립 reviewer가 설계의 충돌·과잉·누락을 읽기 전용으로 검토한다. 발견한 범위 내 문제는 보정한다.
5. Markdown의 링크·형식·변경 범위를 확인한다. 문서 전용이므로 제품 전체 회귀와 runtime 실행은 하지 않는다.

## 산출물과 현재 정책 추적

기존 종료 정책의 상태 추적은 이 문서 한 곳에서 충족한다. 새 하네스의 간소화 제안을 현재 완료 검증 생략에 사용하지 않는다.

| 산출물 | 처리 |
| --- | --- |
| SOP | 재설계안 §3~10에 교체 실행 규칙을 수록. 활성 SOP 적용은 범위 밖 |
| 사용자 안내 | 재설계안 §4·6·11 및 최종 보고에 모델 선택·승인·적용 상태 설명 |
| Implementation report | 본 Change와 별도 감사/재검토 보고서가 실제 산출·검증·제약을 소유 |
| Roadmap | 기존 canonical Task 재사용. 최신 main과 다른 기존 dirty Roadmap은 보존하고 적용 때의 정합성 절차를 설계안에 기록 |
| 검수 checklist | 위 완료 조건 및 검토 보고서의 시나리오/검증 결과 |

## 권한 영향과 복구

이번 실제 작성은 문서 세 파일과 그 완료 local commit만 해당한다. 제안의 모델·순서·질문·검증·최소 증거 조회 재량 확대는 재설계안 §6.3에 명시했다. 현재 도구 거부를 우회하거나 활성 권한을 넓히지 않았다.

문서 제안의 철회는 이 세 파일의 변경만 대상으로 한다. 기존 WIP·과거 승인 기록·제품·DB·runtime을 되돌리거나 삭제하지 않는다.

## 검증·남은 상태

- 지침 감사와 제품/runtime 구조 감사: 각각 읽기 전용 독립 조사 완료. 두 agent 모두 요청 `gpt-6-astra/high`, 관측 `NOT_REPORTED`.
- 최종 설계 독립 검토: `GO`. `harness_design_review`가 최초 P2 1건/P3 2건을 찾았고 보정한 해당 문구를 다시 확인해 open P0/P1/P2/P3 `0/0/0/0`. 요청 `gpt-6-astra/high`, 관측 `NOT_REPORTED`.
- parent 확인: Root 교체 원문의 영구 데이터 보호·테스트 격리·개별 경로 staging 보존, 비용 표현의 관측 수준, 제안/활성 적용 구분과 요청 영역 coverage 확인.
- 문서 구조/링크/범위: 참조 파일·heading/fence와 신규 세 파일 범위를 직접 대조. Python 자동 링크/형식 검사 실행은 policy/never 거부로 미실행. 자동 PASS로 대체하지 않음.
- Git staged whitespace 검사 통과. staged 대상은 allowlist의 신규 Markdown 세 개뿐임을 확인.
- local commit: 이 완료된 문서 세 파일만 대상. 실제 실행 결과는 Git 기록과 최종 보고에 남김.
- 활성 지침/설정 적용, 사용자 검토, 원격 반영, 배포: 미실행. 문서 완성과 구분.
