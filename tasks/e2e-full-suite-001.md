# TASK-E2E-FULL-SUITE-001 — 실험 계보 전체 Full-Stack 회귀 안정화

## 현재 상태

- 단계: 구현·자동 검증·local commit 준비 완료, 사용자 검수 handoff
- 자동 검증: Backend 388/388, Frontend 92/92, Full-Stack E2E 35/35
- Fable: 적용 없음 — 기존 계약 회복 `BUGFIX`
- 대표 repo·`main`·Persistent UAT·실제 provider: 변경 없음
- Git 게시: local experiment commit만 승인, push·PR·merge 미승인, main merge 승인 `0/3`

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK_007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-E2E-FULL-SUITE-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_CREATE`

## Purpose identity

- 업무 목표: 현재 실험 HEAD에서 전체 Full-Stack E2E를 재실행하고, `24/34`로 남았던 비관련 10개 실패와 이후 추가된 시나리오의 모든 불일치를 제품 계약 기준으로 해소한다.
- Root Finding 또는 정책 결정: `FULL-STACK-BASELINE-UNRELATED-FAILURES` / `BACKLOG-E2E-FULL-SUITE-EXISTING-FAILURES`.
- 변경·검증 경계: Home, Pending, IQC, mobile navigation, kitting/procurement, bottleneck, project registration과 이후 export 시나리오의 제품 결함·fixture drift·strict selector drift를 재현해 최소 보정하고 현재 HEAD 전체 suite를 통과시킨다.
- 보존할 불변조건: 기존 업무·권한·migration history, 대표 repo·`main`, Persistent UAT, 실제 provider와 기존 runtime을 변경하지 않는다. E2E는 disposable PostgreSQL·provider disabled 경계만 사용한다.
- 예상 산출물: 실패 분류와 resolution, 필요한 source/test 보정, 전체 Backend·Frontend·migration·Full-Stack 검증, privacy-safe Implementation report, Roadmap backlog 갱신과 local experiment commit.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR — same-purpose 검색 결과 0건

## 승인·실행 경계

- investigationApproved: `true`
- implementationApproved: `true`
- validationApproved: `true`
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
- fableRequired: `false` — 기존 계약을 복구하는 `BUGFIX`

사용자의 `experiment/*` standing instruction과 이번 “권장 다음작업 전체 완료” 지시는 canonical Roadmap 순서를 바꾸지 않으면서 이 실험 계보의 누적 회귀를 먼저 해소하는 명시적 override다.

## 사용자 검수 체크리스트

### 자동 검증 완료

- [x] 프로젝트 목록에 같은 의미의 전체선택 checkbox가 한 개만 표시됨
- [x] 기존 10개 실패 시나리오와 추가 export 시나리오를 포함한 Full-Stack E2E `35/35`
- [x] 제조 중단 Pending fixture가 최신 조치 담당 부서 계약을 사용함
- [x] 일반 구매 fixture가 구매 단계 수량·단위 불변조건을 지킴
- [x] IQC 이동이 `품질 → IQC` 통합 메뉴와 디지털 성적서 절차를 사용함
- [x] 선택 export 감사 검증이 suite 순서와 무관한 증가분을 확인함
- [x] disposable PostgreSQL·container·network cleanup 완료

### 사용자 검수 대기

- [ ] Desktop 프로젝트 목록에서 전체선택 checkbox가 선택 tray에만 한 번 보이는지 확인
- [ ] 대표 repo와 `main`에 변경이 없는지 확인
