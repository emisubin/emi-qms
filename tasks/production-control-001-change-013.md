# TASK-PRODUCTION-CONTROL-001 Change 013 — 구매품 구분별 실적 연결·제조양식 즉시 공통 적용

## Task Identity Gate

- proposedTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `POLICY_DECISION / APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: 후속 제품 기능
- roadmapNextGate: 사용자 지정 오류·정책 수정
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 생산계획의 발주·입고 실적을 구매품 구분별로 연결하고, Item별 제조양식을 프로젝트 생성 시점 snapshot이 아니라 저장 즉시 같은 Item의 모든 프로젝트에 공통 적용한다.
- 정책 변경: 사용자가 2026-08-18 기존 “이후 생성 프로젝트만 적용” 계약 중 제조양식 부분을 명시적으로 철회했다. 생산계획 항목·기간·연결은 계속 프로젝트 전용이며 제조 실행 완료 이력은 불변이다.
- 변경 경계: 생산계획 source catalog·validation·evidence, 연결 constraint additive migration, 제조 current 저장 transaction과 프로젝트 제조 snapshot 동기화, 관련 화면 안내·회귀만 포함한다.
- 보존할 불변조건: 기존 전체 구매품 발주·입고 연결은 호환 유지한다. 구분별 연결은 저장된 구매품 구분 snapshot ID로 계산한다. 이미 시작·완료된 제조 execution과 체크 이력은 바꾸지 않는다.

## 승인 계약

- 사용자는 2026-08-18 정리된 3번과 4번 수정의 구현을 순서대로 명시 승인했다.
- 운영 DB·runtime, 원격 게시·병합·배포는 이번 구현 범위에 포함하지 않는다.
- 사용자는 자동검증 결과와 사용자 검수 미완료 상태를 보고받은 뒤 2026-08-18 Change 012·013, 품질 Change 007과 Infra Change 001의 원격 `main` 병합·Azure 공개배포를 명시 승인했다. 사용자 운영 검수는 배포 후 Gate로 남긴다.

## 구현 계약

1. `발주 완료`와 `전체 입고 확정`은 기존 `전체 구매품` 선택과 활성 구매품 구분별 선택을 함께 제공한다.
2. 구분별 연결은 `project_procurement_items.material_category_id`가 선택 구분과 같은 활성 품목만 근거·분모로 사용한다.
3. 구분이 없는 기존 구매품은 전체 연결에만 포함한다.
4. Item별 제조양식 저장 transaction에서 같은 Item의 기존 방식·연결형 프로젝트 모두에 제조 snapshot을 현재 definition·순서·이름으로 동기화한다.
5. 기존 definition은 이름·순서만 갱신하고 삭제 definition은 비활성화하며 새 definition은 추가한다.
6. 기존 panel manufacturing execution과 execution step은 수정하지 않는다. 기존 방식 프로젝트도 동기화 이후 다음 제조 시작부터 Item 공통 양식을 사용한다.
7. 생산계획 양식·프로젝트별 생산계획 구조와 실적 연결은 계속 각각의 저장 범위를 유지한다.

## 검증 계약

- 전체 발주·입고 연결 기존 회귀 유지
- 구분별 발주·입고가 해당 구분 구매품만 분모·근거로 계산
- 잘못되거나 비활성인 구분 definition 저장 차단
- 제조양식 저장 전후 기존·신규 프로젝트 snapshot 동기화
- 시작·완료된 제조 execution 불변
- Backend 전체, Frontend 전체, migration fresh/existing과 격리 Full-Stack 검증

## Finding

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `PC-C013-CATEGORY-EVIDENCE-GAP-001` | P1 | `RESOLVED` | 발주·입고 실적은 프로젝트 전체 구매품만 집계해 구매품 구분별 계획을 만들 수 없었다. | 선택형 구매품 구분 definition·evidence filter와 전체 구매품 호환 회귀를 추가했다. |
| `PC-C013-MANUFACTURING-SNAPSHOT-DRIFT-001` | P1 | `RESOLVED` | 제조양식 저장 뒤 기존 프로젝트가 생성 당시 snapshot을 계속 사용해 Item 공통 제조 기준과 달랐다. | 제조양식 저장 transaction에서 모든 같은 Item 프로젝트 snapshot을 동기화하고 완료 execution 불변을 검증했다. |
