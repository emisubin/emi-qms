# TASK-GOV-CODEX-002 Change 023 — Superpowers의 유용한 원칙만 구체화

## 요청·범위·상태

- 요청일: 2026-09-09. 사용자 지시: “충돌하지 않는 선에서 가치 있는 것만 구체적으로 가져와.”
- [Change 022](gov-codex-002-change-022.md)의 PMS 하네스 재설계에 공개 Superpowers 분석의 필요한 원칙만 통합한다.
- 상태: `DESIGN_REVIEW_COMPLETE`. 문서 반영·parent 대조·독립 내용 검토 완료. 미확보된 자동 증빙은 아래에 구분하며 활성 적용 아님.
- 활성 지침·설정·플러그인·전역 스킬·제품·DB·runtime·원격 게시·배포는 미변경. 완료 local commit은 기존 사용자 승인을 재사용한다.

## 시작 확인

- proposedTaskId / canonicalTaskId: `TASK-GOV-CODEX-002`; taskType: `DOCS_GOVERNANCE`; instructionChainRead: `true`.
- 이번 작성의 blocking instructionConflictCount: `0`. 이전 감사 지적과 현행/제안 차이는 활성 정책을 바꾸지 않고 설계 대상으로만 취급.
- planningOwner / implementationOwner: parent, 문서 전용으로 Sol 위임 대상 아님. verificationOwner: 독립 `gpt-6-astra/high`; 신규 생성 한도로 작성과 분리된 기존 reviewer 맥락에서 이번 변경을 새로 검토.
- roadmapExpectedTaskId: `TASK-GOV-CODEX-002`; roadmapNextGate: 기존 Change 020 검수 대기, Change 022 설계 검토 완료에서 같은 목적을 이어감.
- roadmapSequenceMatch: `false`; explicitRoadmapOverrideApproved: `true` — 기존 governance 우선 진행 승인과 현재 명시 요청 범위. 제품 큐 순서 변경 없음.
- samePurposeMatchCount: `1`; reuseExistingTask: `true`; gateStatus: `PASS_REUSE`.
- experimentStandingInstructionApplies: `false`; experimentLedgerSelectedTask: `NONE`; policyInputResolution: `N/A`.
- branch: `fix/task-gov-codex-002-instruction-clarity`; 시작 HEAD: `fc34c90bc7bef7e98e728f37cb77c7b8a5429381`.
- `origin/main`: `c3a3c79374babc840dca054bd1a05237a2c49685`. 제품 사실은 최초 감사 기준 유지, 이번 운영 관측 아님.
- Root·해당 Task·최신 change·설계/검토·Roadmap·종료/검증/개인정보 지침의 관련 범위를 재확인. 같은 Task 연속이며 docs/development·tasks에 추가 하위 AGENTS 없음.
- 동일 Task/change·Roadmap·local/remote branch·worktree·해당 branch 전체 PR 대조. 동일 목적 1개, PR 없음, Change 023 신규 경로 존재하지 않음 확인.
- 사전 상태: staged 0 / tracked 수정 17 / untracked 32. 기존 WIP 보존, branch/worktree 생성·전환 없음.

## 변경 allowlist와 완료 조건

- `docs/development/pms-harness-redesign.md`: §4.1 작업 묶음/보정, §5.2 원인 조사, §8 테스트·증거·검토, §9.2 출처/제외 기준.
- `docs/development/pms-harness-redesign-review.md`: §10 충돌·필요성 대조와 이번 독립 검토 결과.
- `tasks/gov-codex-002-change-023.md`: 이번 승인·변경·검증·남은 상태.

완료 조건은 독립적인 테스트 기대값·작업 묶음·통합/수정분 검토를 PMS 예시와 실행 기준으로 구체화하고, 새 재승인·전체 회귀 반복·권한 확대·별도 스킬/원장을 추가하지 않는 것이다. parent와 작성 맥락에서 분리된 독립 reviewer가 실제 문서·diff를 확인한다. 문서 형식·참조·범위만 검증하며 제품 테스트를 추가하지 않는다.

## 산출물과 권한

| 항목 | 위치·상태 |
| --- | --- |
| Implementation report | 본 Change 및 검토 보고서 §10 |
| SOP 제안 | 재설계안 §4.1·5.2·8. 활성 SOP 적용은 범위 밖 |
| 사용자 안내 | 재설계안 §9.2 및 최종 설명 |
| Roadmap | 같은 canonical Task 재사용. dirty Roadmap 보존, 제품 순서/상태 변경 없음 |
| 사용자 검수 checklist | 위 완료 조건 및 검토 보고서 §10.2 |

§6.3의 기존 절차 선택 재량 제안을 구체화하는 문서 변경이다. 운영·게시·데이터 권한을 새로 넓히지 않는다. 기존 승인 이력과 사용자 WIP는 유지한다. 활성 적용은 재설계안 §11의 별도 범위·검증 단계이며 이번 완료와 구분한다.

## 검증·재개

- parent: 실제 diff·채택/제외와 PMS 시나리오 대조 완료.
- 독립 내용 검토: `harness_change022_review`, `GO`, 신규 P0/P1/P2/P3 `0/0/0/0`. 요청 `gpt-6-astra/high`, 관측 `NOT_REPORTED`. 기존 독립 맥락을 재사용해 실제 이번 문서·diff·출처를 다시 읽음. 새 spawn 또는 이전 GO 재사용 아님.
- 검토 전후 HEAD·세 파일 SHA256 동일. 검토 후에는 본 Change·검토 보고서에 실제 결과만 기록하고 설계 내용은 고정본 유지.
- 문서 확인: Git whitespace 검사, heading/fence·신규 참조 직접 대조 완료. 전체 diff digest용 보조 명령은 policy/never 거부로 미확보. 자동 링크 검사는 미실행. 우회하거나 해당 자동 증빙을 PASS로 표시하지 않음.
- 제품 테스트·브라우저·DB·플러그인 설치/실행·성능 실측: 문서 전용으로 미실행.
- 완료한 문서 세 파일만 local commit 대상이며 실제 Git 결과는 최종 보고와 Git에 기록. 다른 WIP 상태·경로 목록 49개 동일, 새 runtime·임시 작업 폴더 없음. 후속 활성 적용은 재설계안 §11을 따름.
