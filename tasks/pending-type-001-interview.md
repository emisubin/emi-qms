# TASK-PENDING-TYPE-001 — Pending 유형 관리 실험 입력

- taskType: `NEW_FEATURE`
- interviewOwner: `WAIVED_BY_USER_FOR_EXPERIMENT`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 `experiment/*` fast-track에서 사용자-facing interview를 생략한 근거와 Fable 1차 기획 입력을 고정한다. 사용자는 이 branch와 대화에서 신규 기능을 인터뷰·중간 승인 없이 `Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit`까지 진행하고, 미확정 비차단 정책은 Fable 권장안으로 자동 채택하도록 명시했다. 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 포함하지 않는다.

## 1. Task Identity Gate

- proposedTaskId: `TASK-PENDING-TYPE-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `Pending 유형 관리자 화면 (ID 미정)`
- roadmapNextGate: `EXPERIMENT_LEDGER_PRIORITY_1`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-PENDING-TYPE-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `Pending 유형 관리자 화면`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 관리 권한이 있는 사용자가 code와 migration을 직접 수정하지 않고 Pending 유형의 사용자 표시와 사용 가능 범위를 안전하게 관리한다.
- Root Finding: 현재 네 유형과 label이 Backend·Frontend·DB check에 고정돼 표시명·순서·수동 등록 노출을 운영에서 바꿀 수 없고, 자동 workflow가 사용하는 semantic code와 관리자가 바꾸는 표시 설정의 경계가 없다.
- 변경·검증 경계: 기존 `TASK-007A` 상태·담당·코멘트·재검사·종결 계약은 보존하고, Pending 유형 관리용 additive schema/API/권한/UI와 기존 생성·조회 연동만 포함한다.
- 보존할 불변조건: 자동 부적합·PUNCH·제조 중단 semantic code, 18단계 workflow, 과거 Pending 의미와 audit, System Administrator의 업무 mutation 우회 금지, Backend 권한 authoritative, 기존 migration 수정 금지.
- 예상 산출물: Fable 1차 기획 원문, Codex review, Fable 2차 기획 원문, Backend·migration·Frontend 구현, 역할·동시성·회귀 검증, desktop/mobile screenshot, 종료 문서와 local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

`TASK-007A`와 `TASK-ADMIN-001/002`는 Pending 유형 관리 화면을 명시적으로 제외·후속 분리했다. 동일 목적 구현 Task·branch·worktree·PR은 없으며 Roadmap 3.4와 완료 원장 우선순위 1이 이 신규 purpose 하나를 가리킨다.

## 2. 확정된 Repository 기준선

- 현재 Pending semantic code는 `Nonconformance`, `Punch`, `ManufacturingStop`, `Other` 네 가지다.
- DB `pending_issues.issue_type` check, Backend `PendingIssueTypes`, Frontend union/label이 같은 고정 집합을 사용한다.
- IQC·후속 품질·제조 자동 생성은 각각 `Nonconformance`, `Punch`, `ManufacturingStop` exact code에 의존한다.
- `TASK-007A`의 상태 전이, 담당자, 코멘트, history, work item·인앱 알림 연결은 완료됐고 다시 구현하지 않는다.
- `Pending.Read`와 `Pending.Manage`가 분리돼 있으며 System Administrator와 read-only role은 업무 Pending mutation을 우회하지 않는다.
- `TASK-ADMIN-002`에는 System Administrator 전체 관리와 지정 부서장 자기 부서 scope, current-department drift fence, lifecycle·audit·CAS를 구현한 선행 패턴이 있다. 이 패턴을 그대로 복사할지 Pending 전용 권한을 둘지는 Fable이 권장한다.
- 모든 내부 부서는 업무 메뉴를 조회할 수 있지만 입력은 기존 담당 permission과 actor 규칙으로 제한된다.
- 최신 migration은 `0044`; 새 migration은 additive next 번호를 사용하고 Persistent UAT에는 적용하지 않는다.
- 모바일은 PC 표를 축소 복제하지 않고 핵심 조회·간단 행동만 제공하며 page-level overflow 0을 지킨다.

## 3. 해결할 문제와 기대 결과

- 현재 문제: 표시명·정렬·수동 등록 노출을 바꾸려면 code와 DB constraint를 함께 수정해야 한다.
- 사용자 가치: 관리 화면에서 안전한 범위의 유형 설정을 변경하고 새 Pending 등록 화면에 일관되게 반영할 수 있다.
- 시스템 가치: 자동 workflow semantic과 사용자-facing catalog를 분리해 관리 변경이 부적합·PUNCH·제조 중단 연결을 깨뜨리지 않는다.
- 실패 시 영향: 유형을 비활성화하거나 이름을 바꾼 뒤 기존 Pending의 의미가 사라지거나 자동 생성·필터·Excel label이 서로 달라질 수 있다.

## 4. Fable 권장안이 확정할 비차단 정책

아래 항목은 사용자에게 다시 묻지 않는다. Fable이 선택지·trade-off와 Repository 근거 권장안을 제시하고 Codex review 뒤 Fable 2차 기획에서 확정한다.

1. 관리 범위: 고정 네 유형의 label·순서·수동 등록 노출만 관리할지, 사용자 정의 수동 유형 추가를 허용할지.
2. semantic 보존: 자동 workflow가 쓰는 system type을 잠그고 custom type을 어떤 안정된 semantic에 연결할지.
3. lifecycle: 비활성화·재활성화·삭제 금지·과거 표시 snapshot·참조 중 보호 정책.
4. 권한: System Administrator 전용, 지정 Pending 유형 관리자, 또는 부서장 범위 중 최소 안전안.
5. 적용 범위: 수동 등록 option·목록 filter·상세·Excel·audit에 catalog label을 어떻게 일관되게 적용할지.
6. 동시성: reorder·activate/deactivate·rename 경쟁을 위한 row version/CAS와 audit 단위.
7. 모바일: 관리 mutation을 어디까지 제공하고 어떤 요약·상태만 남길지.

## 5. 포함 후보

- Pending 유형 catalog 목록·정렬·표시명·활성 상태 관리
- 선택된 권장안이 허용하는 custom/manual 유형 생성 또는 명시적 제외
- system semantic type 보호와 기존/자동 Pending 호환
- 전용 manage permission과 server-side scope 검사
- append-only audit, optimistic concurrency, 참조 중 hard delete 금지
- Pending 생성·필터·상세·선택 Excel label의 단일 catalog source
- Desktop 관리 화면과 모바일 우선 단순 화면

## 6. 명시적 제외

- `TASK-007A` 상태 전이·담당·코멘트·재검사·종결 재구현
- role/permission 편집기 전체
- binary 첨부 storage·검역·보존·backup
- 실제 Teams/Mail/Activity provider
- 대표 repo·`main`·Persistent UAT migration/runtime handover
- push·PR·merge와 main merge 승인

## 7. 성공 기준

- system semantic을 변경·삭제해 자동 workflow를 깨뜨릴 수 없다.
- 허용된 관리자는 유형 설정을 code 수정 없이 변경하고 새 수동 등록·목록/상세/filter/export에서 같은 결과를 본다.
- 과거 Pending 의미와 표시 근거가 보존되고 hard delete나 silent remap이 없다.
- 권한 없음·scope drift·stale version·잘못된 순서/중복·system type 파괴 시도가 안정된 403/409/validation으로 차단된다.
- Backend 전체, Frontend lint/typecheck/unit/build, fresh·existing migration, isolated Full-Stack와 desktop/390px browser 검증이 통과한다.
- Open P0/P1/P2가 0이고 synthetic screenshot과 5종 종료 산출물이 있다.

## 8. 승인·안전 경계

- planningApprovedForExperiment: `true` — standing instruction과 Fable 권장안 자동 채택 조건
- implementationApprovedForExperiment: `true` — Fable 2차 기획의 blocking decision 0인 범위
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
