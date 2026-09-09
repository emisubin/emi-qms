# TASK-GOV-CODEX-002 Change 022 — OmO·CodexClaw 분석을 PMS 설계에 반영

## 요청과 범위

- 요청일: 2026-09-09
- 사용자 지시: “분석 결과 반영해”. 앞선 GitHub 분석의 선택적 도입 권고에 따라 기존 재설계 Markdown을 갱신한다.
- 상태: `DESIGN_REVIEW_COMPLETE` — 문서 반영·parent 대조·fresh 내용 검토 완료. 활성 적용 아님.
- 이전 설계·승인 이력: [Change 021](gov-codex-002-change-021.md). 새 Task를 만들거나 과거 승인 기록을 재작성하지 않는다.
- 활성 AGENTS·모델 설정·실행 정책 변경, 플러그인 설치·전역 스킬 편집, 제품·DB·runtime·원격 게시·배포는 범위 밖이다.
- 완료 local commit은 기존 사용자 자동 commit 승인을 적용한다.

## 시작 확인과 동일 목적

- proposedTaskId / canonicalTaskId: `TASK-GOV-CODEX-002`; taskType: `DOCS_GOVERNANCE`.
- instructionChainRead: `true`; 이번 문서 작성의 blocking instructionConflictCount: `0`. 기존 감사 지적은 설계 제안으로만 다룬다.
- planningOwner / implementationOwner: parent, 문서만 편집하므로 Sol 위임 대상 아님. verificationOwner: fresh `gpt-6-astra/high`.
- roadmapExpectedTaskId: `TASK-GOV-CODEX-002`; roadmapNextGate: 기존 Change 020 검수 대기와 Change 021 설계 완료에서 같은 governance 목적을 이어감.
- roadmapSequenceMatch: `false`; explicitRoadmapOverrideApproved: `true` — 기존 governance 우선 진행 승인과 현재 명시 요청 범위. 제품 실행 큐를 재정렬하지 않음.
- samePurposeMatchCount: `1`; reuseExistingTask: `true`; gateStatus: `PASS_REUSE`.
- experimentStandingInstructionApplies: `false`; experimentLedgerSelectedTask: `NONE`; policyInputResolution: `N/A`.
- branch: `fix/task-gov-codex-002-instruction-clarity`; 시작 HEAD: `02d53ca71ebb41621d169aa0297e7b8d120887ea`.
- `origin/main`: `c3a3c79374babc840dca054bd1a05237a2c49685`. 제품 사실은 Change 021의 감사 기준을 보존하며 운영 재검증을 주장하지 않음.
- 적용 지침: Root 및 기존 governance instruction chain. docs/development·tasks에 추가 하위 AGENTS 없음. 같은 Task 연속이며 관련 지침·기록·범위 재확인.
- 검색: 동일 Task/change·Roadmap 기존 승인·local/remote branch·worktree 확인. 현재 branch의 GitHub 전체 PR 조회 결과 없음.
- 사전 상태: staged 0 / tracked 수정 17 / untracked 32. 기존 WIP 보존, branch/worktree 전환·생성 없음.

## 변경 allowlist와 완료 조건

1. `docs/development/pms-harness-redesign.md`: 작업 영향 분류, 규범 강도, 위임 종료 조건, 프로젝트별 기억, 조건 발동·실화면 검증, 재시도·자원·hook 경계와 출처 반영.
2. `docs/development/pms-harness-redesign-review.md`: 반영/제외 이유, PMS 충돌·과잉 여부와 이번 독립 검토 결과 기록.
3. `tasks/gov-codex-002-change-022.md`: 이번 요청·범위·검증·남은 상태 기록.

완료 조건은 유용한 원칙이 기존 설계에 통합되고, 불필요한 승인·새 기록·설치 의무가 생기지 않으며, 현행 권한과 PMS 업무 계약을 보존하는 것이다. parent와 fresh reviewer가 실제 문서·diff를 읽고 범위 내 Finding을 보정한다. 형식·로컬 링크·정확한 변경 범위를 확인한다. 문서 전용이므로 제품 전체 회귀는 실행하지 않는다.

## 산출물·권한·재개

| 항목 | 위치와 상태 |
| --- | --- |
| 실행 규칙/SOP 제안 | 재설계안 §3~10. 활성 SOP는 미변경 |
| 사용자 안내 | 재설계안 §4·6·9.1·11과 최종 설명 |
| Implementation report | 본 Change 및 검토 보고서 §9 |
| Roadmap | 기존 canonical Task 재사용. dirty Roadmap 보존, 제품 순서/상태 변경 없음 |
| 검수 checklist | 위 완료 조건 및 검토 보고서 §9.2~9.3 |

실제 변경 권한은 위 문서와 완료 local commit에 한정된다. 제안 적용 시 불필요한 강제 절차를 줄여 에이전트의 절차 선택 재량이 넓어짐은 재설계안 §6.3에 명시한다. 설치·교차 프로젝트 기억·공유 자원 정리·운영·게시 권한은 추가하지 않는다.

이번 문서 반영·검토는 완료했다. 진행 중 서버·DB·임시 작업 폴더를 만들지 않았다. 원복 필요 시 이번 세 파일의 diff만 대상으로 하며 기존 WIP와 Change 021 이력은 보존한다. 후속 활성 적용은 설계안 §11의 별도 범위 확인·검증 단계다.

## 검증 결과

- parent 내용 대조: 완료. 부서 이동 예시에서 자동 권한·명시 역할을 구분하고 오산 자동 완료를 미래 검증 계약으로 유지.
- fresh 독립 검토: `harness_change022_review`, `GO`, 신규 P0/P1/P2/P3 `0/0/0/0`. 요청 `gpt-6-astra/high`, 실제 관측 `NOT_REPORTED`. 실제 세 파일·diff와 고정 출처를 확인했고 세 파일 해시는 검토 전후 일치.
- 형식·링크: Git whitespace 검사 통과. 로컬 신규 참조·heading/fence 직접 확인, 외부 고정 링크 다섯 개 조회 확인. reviewer의 Python 자동 링크/형식 집계는 policy/never 거부로 미실행. 우회하거나 자동 PASS로 기록하지 않음.
- 결과 기록: 독립 검토 이후 본 Change와 검토 보고서의 결과·상태만 갱신. 설계 본문은 검토된 그대로 유지. 완료된 세 Markdown만 local commit 대상으로 하며 실제 실행 결과는 Git과 최종 보고에 남김.
- 제품 테스트·브라우저·DB·외부 플러그인 실행: 문서 전용으로 미실행. 활성 적용·사용자 검토·원격 반영·배포와 문서 완료를 구분.
