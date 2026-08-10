# TASK-QUALITY-OPERATING-MODEL-001 Change 005 — 구매품 구분별 IQC 운영 방식·검사 양식 구현 승인

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
- gateStatus: `PASS_REUSE`

사용자는 Azure 외부 도메인 검증 대기 중 승인한 5개 기능 작업의 두 번째 작업으로 구매품 구분별 IQC 기능을 선택했다. Fable deep-interview 요약과 planning을 확인하고 Codex review의 구조적 보완 및 아래 UX 결정까지 승인한 뒤 Repository 구현 규칙을 다시 읽고 기능 구현·검증까지 완료하라고 명시했다.

## 승인 상태

- planningApproved: true
- reviewResolutionApproved: true
- implementationApproved: true
- localCommitApproved: true
- pushApproved: false
- prApproved: false
- mainMergeApproved: false
- persistentUatApproved: false
- azureRuntimeApproved: false
- externalProviderApproved: false

2026-08-06 사용자는 Change 004·005 현재 상태를 먼저 local checkpoint commit으로 보존한 뒤 Azure 공개배포 Task를 우선 재개하라고 명시했다. 사용자 검수는 계속 대기이며, 이 승인은 push·PR·`main` merge·Persistent DB·Azure 반영을 포함하지 않는다.

2026-08-05 사용자는 Repository의 일반 branch 규칙을 확인한 뒤 이미 완료된 Fable 1차 기획과 Codex review를 최종 기획 계약으로 승인하고, 추가 Fable 호출 없이 Codex가 구현·검증을 완료하라고 명시했다. Codex가 절차 확인 전에 시작했던 IQC 제품 코드 변경은 전부 원복했으며 기존 Change 004 변경은 보존한 상태에서 구현을 새로 시작한다.

## 사용자 결정

1. 기존 구매품 구분 관리에서는 IQC 상태를 조회 전용으로 표시하고, 실제 검사 설정은 양식 관리의 `구매품별 IQC 양식`에서만 변경한다.
2. 새 구매품 구분은 항상 `검사 없음`으로 생성한다.
3. 비활성 구분도 IQC 양식 관리에 표시하고 `비활성` badge와 함께 설정·양식을 편집할 수 있게 한다.

## 구현 계약

1. 구분별 IQC 설정을 검사 여부·방식의 유일한 쓰기 source로 두고 기존 `material_categories.requires_iqc`를 독립 mutation에서 제거한다.
2. 설정·양식 변경은 활성 품질 domain 양식 관리자와 시스템 관리자만 가능하다. 일반 품질 사용자의 구분 metadata 관리 권한은 유지하되 IQC 설정은 바꿀 수 없다.
3. 상세형은 현재 항목이 1개 이상일 때만 활성화하고, 활성 상세형의 빈 양식 저장을 차단한다.
4. 구매품을 저장하거나 도착 전 구분을 명시적으로 다시 저장할 때 검사 여부·방식을 snapshot한다. 기존 저장 구매품에는 설정 변경을 소급하지 않는다.
5. 도착 등록은 snapshot으로 `검사 없음`·`ScanBased`·`Detailed`를 분기한다.
6. CategoryBased Detailed 성적서는 최초 초기화 시 구매품에 snapshot된 category id의 현재 양식 version을 고정하며 전역 양식으로 fallback하지 않는다.
7. Legacy `AllReceipts` 프로젝트는 전역 `MATERIAL_IQC` Detailed 양식을 유지한다.
8. Detailed 재검사는 원 회차의 실패 항목 snapshot을, ScanBased 재검사는 새 스캔본을 사용하는 기존 불변조건을 유지한다.
9. 설정과 양식 변경은 append-only audit과 optimistic concurrency를 사용한다.
10. 기존 Graphite 양식 관리의 catalog·selector·editor·feedback·mobile 규칙을 재사용한다.

## 포함 범위

- additive migration `0071`
- 구분별 설정·상세 양식 API와 권한·감사·동시성
- 구분 metadata mutation과 검사 설정 mutation 분리
- 구매품 검사 방식 snapshot과 도착 3-way 분기
- category id 기반 Detailed 성적서 양식 고정
- 기존 양식 관리 화면 안의 구매품 구분 검색·설정·항목 editor
- Backend·Frontend·migration·desktop·390px 검증과 Task 문서 동기화

## 제외 범위

- 판금류·부스바·명판의 실제 검사 항목 입력과 활성화
- LQC Change 004 재작업, LSE TASK NO, 부서 Pending, 설계 도번·묶음
- 기존 확정 IQC·PDF·첨부·Pending·재검사 기록 수정·삭제
- 범용 검사 엔진, OCR·scanner·협력사 Excel 연동
- commit·push·PR·main 병합, Persistent DB·Azure runtime·실제 provider
