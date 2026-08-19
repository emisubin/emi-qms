# TASK-G2-OPERATIONS-001 — Task Identity Gate

- proposedTaskId: `TASK-G2-OPERATIONS-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-QMS-PLATFORM-001`
- roadmapNextGate: `SLICE_1_IMPLEMENTATION`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-G2-OPERATIONS-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 기존 PMS 데이터와 연결하지 않는 독립 G2 업무공간에서 일별 오전·오후 생산 대수, 일일 출하 대수와 제조 출근 인원을 입력·관리하고 홈에서 그래프와 표로 함께 확인한다.
- Root Finding 또는 정책 결정: 현재 원격 `main`에는 이 세 일일 수치를 독립적으로 기록·정정·집계하는 G2 메뉴와 데이터 계약이 없다. 사용자는 현재 승인된 `TASK-QMS-PLATFORM-001` Slice 1과 G2를 동시 진행하도록 명시적으로 승인했다.
- 변경·검증 경계: 현재 단계는 Fable 5 deep-interview, primary planning과 Codex 내용 review다. 제품 코드·DB·migration·runtime·Persistent UAT·공개 배포·Git 게시는 포함하지 않는다.
- 보존할 불변조건: 기존 프로젝트·생산계획·제조·물류·근태 데이터와 연결하거나 소급 변경하지 않고, 운영 인증·서버 권한·ReviewSafe 차단·기존 메뉴와 모바일 접근성 계약을 유지한다.
- 예상 산출물: `tasks/g2-operations-001-interview.md`, Fable round 원문, `tasks/g2-operations-001-planning.md`, `tasks/g2-operations-001-review.md`, Product Roadmap update.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR — GitHub connector는 private repository 검색 권한 오류가 있어 privacy-safe `gh` projection으로 보완했으며 동일 목적 PR은 0건이다.

## Roadmap Sequence 결정

- 사용자 승인 일자: 2026-08-18
- 사용자 발화: `동시 진행할거야.`
- 결정: `TASK-QMS-PLATFORM-001` Slice 1을 중단·변경하지 않고 `TASK-G2-OPERATIONS-001`의 신규 기능 기획을 병렬 실행한다.
- 구현·commit·push·PR·merge·Persistent UAT·공개 배포 승인은 포함하지 않는다.

## 기준선과 적용 지침

- 기준 branch: `feat/task-g2-operations-001`
- 기준 SHA: `28991aecbeaeeeff6e636f002761825b666d7a5e`
- 기준 remote: 최신 `origin/main`과 일치
- 적용 지침: Root `AGENTS.md`, `frontend/AGENTS.md`, `backend/AGENTS.md`, `scripts/AGENTS.md`
- 작업공간 목적: QMS Slice 1과 사용자 WIP를 보존하면서 G2 기획을 병렬 격리
- 작업공간 owner: `TASK-G2-OPERATIONS-001` Codex 기획 세션
- 예상 종료 시점: Fable interview·planning·Codex review와 사용자 resolution 완료 시점
- cleanup 경계: 자동 정리하지 않는다. clean·process 미사용·commit reachable·사용자 승인 조건을 모두 확인한 뒤에만 `git worktree remove`를 사용할 수 있다.

## 운영 기준 확인

- Azure 공개 root 익명 상태: `401`
- 최신 운영 release: `31786040822`, `completed/success`
- 운영 release source SHA: `58c089993587deea30513cb6edee0b8396a1d474`
- 현재 원격 `main`과 운영 source의 제품 코드 차이: `0`; 운영 결과 문서만 후속 갱신됨
- 운영 인증·업무 data mutation: `0`

## Finding

| ID | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `G2-PRIVACY-EVIDENCE-001` | P2 | `RESOLVED` | 첫 운영 browser 확인에서 fixed projection보다 자세한 인증 redirect metadata가 transient tool output에 포함됐다. Repository·tracked/staged artifact·사용자 업무 데이터 변경은 없었다. | 해당 원문을 증빙에서 폐기하고 허용 key·boolean·HTTP status·SHA만 남기는 projection으로 Gate와 운영 확인을 처음부터 재실행했다. |

## 다음 Gate

1. Fable 5 deep-interview round 1
2. 사용자 답변 기록과 추가 round 또는 확인용 요약
3. 사용자 요약 확인
4. Fable primary planning 1회
5. Codex 내용 review 1회
6. planning·review resolution의 별도 사용자 승인
