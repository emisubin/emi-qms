# TASK-PRODUCTION-CONTROL-001 — Task Identity Gate

- instructionChainRead: `true`
- taskType: `NEW_FEATURE`
- proposedTaskId: `TASK-PRODUCTION-CONTROL-001`
- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- branch: `experiment/task-home-002-personalized-shell`
- worktree: current experiment worktree
- baselineHead: `de8e05bc0383ebf5abbdcfd95cab3d5d85c9f5ce`
- roadmapExpectedTaskId: `ATTACHMENT-STORAGE-OR-OPERATIONS-TRANSITION`
- roadmapNextGate: `OPERATIONS_PROMOTION`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- samePurposeMatchCount: `0`
- reuseExistingTask: `false`
- policyInputResolution: `USER_DECISION_REQUIRED`
- gateStatus: `PASS_CREATE`
- mainMergeApprovalCount: `0/3`

## Purpose identity

- 업무 목표: 프로젝트 상세 생산관리 탭에서 Item별 고정 계획 항목의 계획 시작·종료 기간과 각 부서 실데이터에서 자동 집계한 실적 시작·종료 기간을 한눈에 비교하고, 같은 정보를 가로 막대 일정표로 확인한다.
- Root Finding: 기존 생산계획은 프로젝트별 항목명 snapshot, 단일 예정일과 비고만 저장한다. 구매·자재·제조·품질·물류에는 실제 처리 이력이 존재하지만 생산계획 항목과 연결할 안정적인 semantic key 및 자동 실적 projection이 없어 생산관리 탭만으로 전체 진행을 판단할 수 없다.
- 변경 경계: Item별 계획 milestone 정의·snapshot, 계획 시작/종료 입력, 부서 원본 데이터 binding, 실적 기간·진행률·일정 상태 파생, 계획/실적 이중 가로 막대 캘린더, 기존 계획 backfill, Backend·Frontend·migration·tests·privacy-safe screenshot을 포함한다.
- 보존할 불변조건: 부서 원본 데이터가 실적의 source of truth이고 자동 실적은 수동 수정하지 않는다. 18단계 workflow 순서와 전체 진행률 공식은 변경하지 않는다. Pending은 단계나 과거 실적을 되돌리지 않고 현재 차단 상태로 표시한다. IQC는 구매품목 도착분, 제조·LQC·OQC·전진검수·FAT·물류는 개별 패널 단위를 유지한다. FAT optional, 부분 입고·부분 출하와 기존 audit·권한·template snapshot을 보존한다.
- 예상 산출물: Fable 1차 기획 원문, Codex 내용 review, Fable 2차 기획 원문, additive migration, Backend/Frontend 구현, 기존 데이터 재조정, 자동 검증, desktop/mobile screenshot, Implementation report, Roadmap·실험 완료 원장 update와 local experiment commit.

## 중복·순서 조사

- 기존 `TASK-005A`는 Item 기반 생산계획 snapshot과 단일 예정일·담당자·체크형 캘린더를 구현했지만 부서 실데이터 binding과 실적 기간 projection은 포함하지 않는다.
- `TASK-010A Change 004`는 생산계획과 제조 투입의 화면 분리이며 이번 목적과 다르다.
- `TASK-WORKFLOW-CONTINUITY-001 Change 010`은 18단계 실데이터 상태 집계와 누락 인계를 보정하지만 생산계획 milestone 기간과 계획/실적 캘린더를 만들지 않는다.
- Task 문서, Roadmap·Decision Log·추적 항목, local/remote branch, worktree와 전체 PR에서 같은 purpose identity를 가진 후보는 확인되지 않았다.
- Roadmap의 기본 다음 제품 후보는 첨부 storage 또는 운영 전환이지만, 사용자가 이번 신규 기능의 최종 목표를 확정하고 즉시 시작하도록 명시했다. 현재 experiment standing instruction에 따른 명시적 순서 변경으로 기록하며 대표 repo·`main`·Persistent UAT·실제 provider 경계는 변경하지 않는다.

## 사용자 결정 후보

1. Item별 고정 milestone은 stable semantic code와 template version으로 관리하고 프로젝트에는 snapshot을 저장한다.
2. 계획 기간은 시작일·종료일을 사용하며 같은 날을 허용하고 `시작일 ≤ 종료일`을 강제한다.
3. 실적 시작은 연결된 실제 처리의 최초 유효 시각, 실적 종료는 해당 milestone의 모든 활성 대상 완료 시각으로 자동 계산한다.
4. 프로젝트 표는 milestone을 한 행으로 집계하고 구매품목·패널별 근거는 행 펼침에서 제공한다.
5. 계획은 외곽선 막대, 실적은 채운 막대로 같은 날짜 축에 표시하며 오늘선·휴일·지연 상태를 함께 제공한다.
6. 일정 진행률은 기존 18단계 전체 흐름 진행률과 분리한다. Pending은 과거 실적을 삭제하지 않고 차단 상태와 근거를 표시한다.
7. 기존 알려진 계획 항목은 semantic code로 backfill하고, 매칭되지 않는 사용자 추가 항목은 수동 항목으로 보존하되 자동 실적은 `연결 안 됨`으로 표시한다.
8. 생산계획·구매 예정일의 `work_items.due_date` 자동 동기화와 외부 에스컬레이션은 기존 미확정 정책을 보존해 이번 Task에서 제외한다.

사용자는 이번 Task에서는 위 권장안을 자동 채택하지 않고 직접 결정한다고 명시했다. Fable 5 interview가 선택지와 권장안을 제시하고 사용자 답변·요약 확인 뒤에만 planning을 시작한다.

## 실행·게시 경계

- Fable interview → 사용자 직접 결정·요약 확인 → Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit 순서를 적용한다.
- 현재 dirty experiment WIP를 reset·checkout·정리하지 않고, 이번 Task allowlist만 추적한다.
- 대표 repo, `main`, push, PR, merge, Persistent UAT migration·runtime handover와 실제 Teams/Mail provider는 제외한다.
