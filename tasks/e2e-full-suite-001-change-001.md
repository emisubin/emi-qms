# TASK-E2E-FULL-SUITE-001 Change 001 — 역할별 18단계 연속 사용자 검수

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `BATCHED_FINAL_USER_VALIDATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-E2E-FULL-SUITE-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-E2E-FULL-SUITE-001`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 한 개의 합성 프로젝트를 영업 등록부터 생산관리·설계·구매·자재·IQC·키팅·제조·품질·물류·세금계산서 완료까지 실제 담당자 화면 입력으로 연속 검수한다.
- Root Finding 또는 정책 결정: 기존 `35/35` Full-Stack 회귀는 화면별 시나리오가 분리돼 있어 한 프로젝트의 18단계 연속 인수인계와 단계별 프로젝트 상세 증빙을 한 번에 보여 주지 못했다.
- 변경·검증 경계: 제품 source를 변경하지 않고 전용 Full-Stack E2E 시나리오와 합성 screenshot만 추가한다. Pending 생성·담당 지정·조치·재검사 요청·종결도 담당자 화면에서 수행한다.
- 보존할 불변조건: 실제 사용자·고객·프로젝트 data, Persistent UAT, 실제 provider, 대표 repo, `main`, push·PR·merge를 변경하지 않는다. 모든 쓰기는 실행별 disposable PostgreSQL에서만 수행한다.
- 예상 산출물: 역할별 UI 입력 자동 검수, 18단계와 Pending open/closed의 영업담당자 프로젝트 상세 screenshot, 프로젝트 생성 시 전 사용자 알림 screenshot, 단계별 다음 담당자의 알림·내 업무 screenshot, UX 평가, privacy-safe validation report.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR — 같은 목적의 canonical Task는 `TASK-E2E-FULL-SUITE-001` 한 개

## 사용자 지시와 실행 경계

- 사용자 지시: 실제 업무 담당자처럼 화면에서 입력하고, 단계 전환마다 `dev-sales` 영업담당자 화면으로 프로젝트 상세를 촬영한다.
- 알림 지시: 프로젝트 생성 직후 모든 개발 사용자 계정의 인앱 알림을 확인하고, 담당 지정 이후 각 인계 시점에는 다음 담당자 계정의 `알림`과 `내 업무`를 촬영한다. 대상 프로젝트가 없는 화면도 누락하지 않고 기능 공백 증빙으로 보존한다.
- 검수 대상: 18단계 전체, 제조 중단 Pending open·담당 지정·조치·재검사·종결·재개, 최종 세금계산서와 프로젝트 완료.
- 역할: `dev-sales`, `dev-production`, `dev-design`, `dev-procurement`, `dev-materials`, `dev-quality`, `dev-manufacturing`, `dev-logistics`.
- 제품 수정: 미승인. 실패 시 root cause와 재현 지점까지만 기록한다.
- 외부 provider: disabled.
- Persistent UAT: 미사용.
- local commit: 미요청.
- push·PR·merge: 미승인.
- main merge 승인: `0/3`.
