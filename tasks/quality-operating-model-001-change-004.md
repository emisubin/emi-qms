# TASK-QUALITY-OPERATING-MODEL-001 Change 004 — Item별 LQC 운영 상태·검사 양식과 프로젝트 snapshot

## Task Identity Gate

- proposedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapNextGate: `Front Door domain Approved 대기 → managed TLS·route`
- roadmapSequenceMatch: false
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: true
- experimentStandingInstructionApplies: false
- experimentLedgerSelectedTask: `NONE`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

사용자는 Front Door 외부 검증 대기 중 5개 기능 작업을 순서대로 계획·구현하도록 승인했고, 첫 작업으로 LQC 운영 중지와 재개를 선택할 수 있게 하라고 명시했다. 이어서 전역 스위치는 제외하고 `Item별 운영 상태`, `Item별 검사 항목`, `프로젝트 생성 시점 고정`을 확정했으며 Fable을 호출하지 말고 Codex가 직접 계획·구현하라고 지시했다.

## 분류와 승인 경계

- 기존 `TASK-QUALITY-OPERATING-MODEL-001`의 품질 운영 모델을 확정된 사용자 결정에 맞춰 구현하므로 `APPROVED_FEATURE_IMPLEMENTATION`으로 처리한다.
- Item별 상태·양식·프로젝트 snapshot이라는 신규 계약은 사용자가 이 Change에서 직접 확정했다. 사용자 지시에 따라 Fable은 호출하지 않는다.
- implementationApproved: true
- localCommitApproved: true
- pushApproved: false
- mainMergeApproved: false
- persistentUatApproved: false
- azureRuntimeApproved: false
- externalProviderApproved: false

2026-08-06 사용자는 Change 004·005 현재 상태를 먼저 local checkpoint commit으로 보존한 뒤 Azure 공개배포 Task를 우선 재개하라고 명시했다. 이 승인은 push·PR·`main` merge·Persistent DB·Azure 반영을 포함하지 않는다.

## 사용자 문제와 Root Finding

현재 시스템은 LQC 단계와 양식이 전 프로젝트 공통이다. 전역으로 중단하면 설정 변경 전 프로젝트까지 영향을 받고, 공통 양식 하나로는 Item별로 다른 LQC 검사를 정의할 수 없다. 반대로 Frontend에서 메뉴만 숨기면 제조 완료와 LQC 합격 joint gate가 남아 OQC 인계·진행률·생산계획 실적이 멈춘다.

따라서 운영 설정은 새 프로젝트의 기본값으로만 사용하고, 프로젝트 생성 시점에 LQC 적용 여부와 Item별 양식을 불변 snapshot으로 고정해야 한다.

## 확정 정책

### 1. Item별 운영 상태

- 전역 LQC 스위치는 두지 않는다.
- `양식 관리 > LQC 검사`에서 Item을 선택하고 해당 Item의 `운영 중 / 운영 중지`를 관리한다.
- 운영 상태 변경은 시스템 관리자만 가능하다. 품질 양식 관리자는 상태를 조회하고 검사 항목만 수정할 수 있다.
- 전체 Item의 초기 운영 상태는 현장 결정을 반영해 `운영 중지`다.
- 상태 변경에는 변경자·시각·이전값·새값을 append-only audit으로 남기며 optimistic concurrency를 적용한다.

### 2. Item별 LQC 검사 양식

- 각 Item은 독립된 현재 LQC 검사 항목 집합을 가진다.
- 기존 `수정 → 저장/취소`, 항목 순서·필수·사진 필수·응답 형식 UI를 그대로 재사용한다.
- 초기 Item별 양식은 기존 공통 LQC 양식을 복제해 시작한다.
- 양식 저장은 이후 생성되는 프로젝트에만 적용한다. 기존 프로젝트와 이미 시작된 검사 보고서의 template snapshot은 바꾸지 않는다.

### 3. 프로젝트 생성 시점 snapshot

- 새 프로젝트는 프로젝트 Item에 연결된 `LQC 운영 여부`와 `현재 LQC 양식 version`을 생성 transaction에서 함께 저장한다.
- 프로젝트의 LQC 운영 여부와 양식 version은 생성 후 변경할 수 없다.
- 설정을 켜거나 꺼도 이전 프로젝트는 영향을 받지 않는다.
- migration 적용 전에 생성된 기존 프로젝트는 당시 계약인 `LQC 운영 중`과 기존 공통 LQC 양식을 snapshot한다. 기존 진행 중·확정 LQC 업무와 증빙도 취소·삭제하지 않는다.

### 4. 운영 중 프로젝트 흐름

- 제조 시작 시 LQC 담당자를 확인하고 LQC 업무·알림을 생성한다.
- 제조 완료 + LQC 합격이 모두 확인되면 OQC를 연다.
- 전체 흐름·진행률·현재 단계·필수 담당자에 LQC를 포함한다.
- LQC 검사는 프로젝트에 snapshot된 Item별 양식을 사용한다.

### 5. 운영 중지 프로젝트 흐름

- 제조 시작 시 LQC 담당자를 요구하거나 LQC 업무·알림을 만들지 않는다.
- 제조 단계가 모두 완료되면 LQC 합격 없이 제조완료확인과 OQC 업무를 정확히 한 번 생성한다.
- 제조완료확인은 `ManufacturingOnly`와 실제 제조 execution을 기록하고 가짜 LQC 합격 record/event를 만들지 않는다.
- 전체 흐름·진행률·현재 단계·필수 담당자에서 LQC를 제외한다.
- 프로젝트에 이미 snapshot된 `LQC_PASSED` 생산계획 연결은 같은 제조 definition의 제조 완료 실적으로 대체하고 `LQC 운영 중지 · 제조 단계 완료로 대체`라고 표시한다.

### 6. 조회·복구·기존 이력

- LQC 화면은 프로젝트별 snapshot 상태를 표시한다. 운영 중 프로젝트는 입력 가능하고 운영 중지 프로젝트는 과거 이력 조회만 가능하다.
- 누락 업무 reconciliation도 프로젝트 snapshot을 기준으로 LQC 또는 OQC direct handoff를 복구한다.
- 기존 LQC 성적서·응답·사진·PDF·Pending·재검사·담당자 이력은 보존한다.
- OQC 이후 전진검수·FAT·물류, 권한, 멱등성과 확정 증빙 불변 계약은 변경하지 않는다.

### 7. UI·UX

- 기존 Graphite 양식 관리 화면, catalog, editor, 상태 badge, feedback component를 재사용한다.
- LQC editor 상단에 Item 선택 목록과 해당 Item의 상태 스위치를 둔다. 전체 스위치는 표시하지 않는다.
- 시스템 관리자가 아니면 스위치는 disabled 상태로 보이고 관리자 전용임을 안내한다.
- 스위치 변경 전에는 “기존 프로젝트에는 영향이 없고 이후 생성 프로젝트부터 적용”됨을 화면에 표시한다.
- desktop과 390px에서 기존 hierarchy·간격·focus·loading/error/success 규칙을 유지한다.

## 기술 결정

- `workflow_stages.is_active`는 LQC 전역 운영 상태로 사용하지 않고 단계 catalog는 활성 상태로 유지한다.
- Item별 current 설정과 audit을 별도 테이블로 저장한다.
- `projects`에 LQC 적용 여부와 template version snapshot을 추가하고 DB trigger로 불변성을 보장한다.
- 품질·제조·workflow·생산계획은 전역 상태가 아니라 프로젝트 snapshot을 조회한다.
- 기존 프로젝트는 migration 시 `운영 중 + 기존 공통 LQC 양식`으로 backfill한다.

## 구현 순서

1. migration `0070`: Item별 설정·양식·audit·프로젝트 snapshot·제조 인계 audit
2. 프로젝트 직접 생성·Excel 생성 transaction에 Item 설정 snapshot
3. 제조·품질·workflow·진행률·생산계획을 프로젝트별 상태로 전환
4. Item별 LQC 설정·양식 Backend API와 관리자 권한·동시성
5. 기존 양식 관리 디자인 안의 Item selector·상태 스위치·editor
6. migration fresh/existing, 권한·snapshot 불변·활성/중지 혼재 프로젝트, Backend·Frontend 전체 회귀
7. 구현 보고·사용자 검수 checklist·Roadmap 동기화

## 예상 변경 allowlist

- DB: `database/migrations/0070_lqc_operating_suspension.sql`
- Backend: `Admin/FormTemplate`, `Projects`, `Workflow`, `Manufacturing`, `QualityInspections`, `ProductionPlanning` 관련 계약·store·tests
- Frontend: 양식 관리·품질·생산계획 관련 기존 component/type/API/test
- Docs: 본 Change, Roadmap, implementation report, user validation checklist

## 제외 범위

- IQC 구매품 구분별 양식 기능(2번 작업)
- LSE TASK NO(3번 작업)
- 부서 Pending·상태 구분(4번 작업)
- 설계 도번·필수값·패널 묶음(5번 작업)
- LQC 세부 판정 방식 자체의 변경, OQC·전진검수·FAT 내용 변경
- 기존 프로젝트별 LQC 설정 수동 변경
- 기존 확정 품질 기록 삭제·자동 합격·Pending 자동 종결
- Azure runtime·DB 적용, Front Door·TLS·public traffic, 실제 provider
- commit·push·PR·main merge

## 검증 계약

- migration fresh 적용과 기존 프로젝트 `운영 중 + 공통 양식` backfill
- Item 상태 off/on 변경 뒤 각각 생성한 프로젝트의 snapshot이 다르고 이후 설정 변경에도 불변
- Item A/B의 검사 항목과 운영 상태가 서로 섞이지 않음
- 일반 품질 양식 관리자 상태 변경 403, 시스템 관리자 변경 성공, stale version 409, audit 기록
- 운영 중지 프로젝트: LQC 업무 0, 제조 완료 후 OQC exactly-once, 진행률에서 LQC 제외
- 운영 중 프로젝트: 기존 제조+LQC joint gate와 프로젝트 snapshot 양식 사용
- 활성·중지 프로젝트가 동시에 있을 때 queue·reconciliation·목록·상세·생산계획이 각각 올바른 상태 사용
- 기존 확정 LQC report/PDF/Pending/reinspection 조회 보존
- Frontend unit, typecheck, lint, production build, Backend Release build와 전체 tests
- desktop·390px의 Item selector·스위치·editor, loading/empty/error/success, focus와 overflow 확인

## 중단 조건

- 기존 프로젝트의 생성 당시 LQC 상태를 사실과 다르게 추론해야 하는 경우
- 프로젝트 snapshot 없이 현재 Item 설정을 runtime에서 다시 읽어야만 구현되는 경우
- 기존 확정 LQC 증빙을 삭제·변경해야 하는 경우
- 범위 밖 Azure runtime 또는 Persistent DB mutation이 필요한 경우
