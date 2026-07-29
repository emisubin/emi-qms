# TASK-PRODUCTION-CONTROL-001 — Fable 1차 기획 Codex Review

- reviewType: `CONTENT_PRODUCT_REPOSITORY`
- planningSource: `tasks/production-control-001-planning.md`
- interviewSource: `tasks/production-control-001-interview.md`
- reviewedBranch: `experiment/task-home-002-personalized-shell`
- reviewedHead: `de8e05bc0383ebf5abbdcfd95cab3d5d85c9f5ce`
- reviewStatus: `RESOLVED_FOR_SECOND_PLANNING`
- openBlockingDecisionCount: 0

## 1. 결론

1차 기획의 핵심 사용자 가치와 방향은 유지한다. 사용자가 코드 수정 없이 Item별 제조·생산계획 양식과 다중 실적 연결을 구성하고, 새 양식으로 생성된 프로젝트에서 부서 실데이터가 계획 실적으로 자동 반영되는 구조는 현재 제품 방향과 일치한다.

다만 실제 Repository는 이미 프로젝트 생성 시 legacy 생산계획 snapshot을 만들며, 현행 제조 양식은 전역 Active version을 패널 제조 시작 시 복사한다. 따라서 1차 기획의 `snapshot 존재 여부 = 새 화면` 판정과 일반 form-template DTO의 단순 확장은 그대로 구현하면 기존 프로젝트 오분류와 양식 조합 race를 만들 수 있다. 아래 resolution을 2차 기획의 필수 계약으로 추가한다.

## 2. 유지

| 항목 | 판정 | 근거 |
| --- | --- | --- |
| Item별 제조·생산계획 양식과 Draft → Active → Archived | 유지 | 기존 양식 관리의 version·audit·권한 패턴을 재사용할 수 있고 항목 전면 교체 요구를 충족한다. |
| 프로젝트 생성 시 양식·연결 snapshot | 유지 | 기존 프로젝트 불변과 새 프로젝트 자동 생성 원칙을 동시에 지키는 핵심이다. |
| 프로젝트별 계획 항목·기간·연결 revision | 유지 | 현장별 예외를 template 원본에 역전파하지 않고 처리한다. |
| 조회 시 부서 원본에서 실적 projection 계산 | 유지 | mutation 경로 누락과 저장 projection drift를 피하고 사용자 확정 정책과 일치한다. |
| 기존 프로젝트의 legacy 화면 유지 | 유지 | 기존 행에는 새 연결이 없으므로 빈 자동 실적 화면으로 잘못 전환하지 않는다. |
| 6열 표·근거 펼침·계획/실적 막대·390px 카드 | 유지 | 한 화면 판단과 좁은 화면 단순화를 함께 충족한다. |
| 18단계 workflow 진행률과 일정 진행률 분리 | 유지 | 기존 완료 판정 회귀를 막는다. |
| `키팅 완료` 제외 | 유지 | 키팅은 선택 업무이며 사용자가 확인한 최소 고정 사건 목록에 포함되지 않는다. |

## 3. 필수 추가·수정

### R1 — 새 화면 분기는 명시적인 model version으로 고정

- `snapshot 존재 여부`로 새·기존 화면을 나누지 않는다. 현재 `ProjectStore.CreateInitialProductionPlanFromTemplateAsync`가 이미 모든 인식 가능한 Item 프로젝트에 legacy `project_production_plans`와 items를 생성하기 때문이다.
- 프로젝트 생산계획에 `Legacy` 또는 `LinkedV1` 같은 명시적 model version을 저장한다.
- migration 당시 모든 기존 plan은 `Legacy`로 고정한다. migration 이후라도 해당 Item에 게시된 LinkedV1 양식 세트가 없으면 현행 legacy 생성 경로를 사용한다.
- Item별 LinkedV1 생산계획 양식과 유효한 제조 양식이 모두 게시된 뒤 생성되는 프로젝트만 `LinkedV1`로 snapshot한다.
- Frontend·Backend 분기와 회귀 테스트는 이 model version만 사용한다.

### R2 — 생산계획 Active version과 제조 Active version의 호환성을 fail-closed 검증

- 생산계획 연결은 제조 단계 이름·순서가 아니라 server-generated `definition_key`를 참조한다.
- 제조 draft를 기존 version에서 복제하면 `definition_key`를 유지하고, 진짜 신규 단계만 새 key를 발급한다. 요청자가 key를 임의 생성·변경하지 못하게 한다.
- 생산계획 양식 활성화 시 현재 Item의 Active 제조 version에 모든 참조 `definition_key`가 존재하는지 검증한다.
- 프로젝트 생성 transaction에서 Active 생산계획 version과 Active 제조 version을 함께 잠그고 연결을 다시 검증한 뒤 정확한 두 version ID와 항목·연결을 snapshot한다.
- 연결이 끊긴 조합은 legacy로 조용히 fallback하지 않고 양식 활성화 또는 프로젝트 생성을 명확한 한글 오류로 차단한다. 이미 생성된 프로젝트 snapshot은 영향받지 않는다.

### R3 — generic form-template 화면은 통합하되 저장 계약은 전용으로 분리

- `FormTemplateManagementPage` 안에 영역을 통합하는 UX는 유지한다.
- 기존 `SaveFormTemplateItemRequest`는 검사형 응답 필드 중심이고 Item scope·다중 연결·제조 definition을 표현하지 못한다. 이를 무리하게 모든 family의 공용 DTO로 확장하지 않는다.
- 기존 manager binding·lifecycle·audit 패턴을 재사용하되, Item별 제조 template와 생산계획 template·connection은 전용 contract/store/endpoint로 구현한다.
- `ProductionPlanning` domain을 추가할 때 `NormalizeDomain`, system-admin scope, candidate의 허용 부서 매핑을 명시적으로 추가한다. 현행 `Quality ? quality : manufacturing` 분기를 그대로 확장하면 생산계획 관리자가 제조 부서로 잘못 제한된다.

### R4 — 프로젝트 생성 경로를 하나의 helper로 수렴

- 직접 생성과 Excel 대량 생성 모두 같은 `CreateInitialProductionControlSnapshotAsync`를 호출한다.
- 프로젝트·패널·UL891 set 구조, LinkedV1 생산계획 snapshot, 제조 snapshot과 audit를 같은 project creation transaction에 저장한다.
- 중간 일부만 만들어진 프로젝트를 허용하지 않는다. 유효한 LinkedV1 pair가 있으면 전부 snapshot하거나 전체 생성이 실패해야 한다.
- 프로젝트 삭제 lifecycle·격리 purge에 신규 snapshot·connection 테이블을 포함한다.

### R5 — 제조 execution은 project snapshot identity를 보존

- LinkedV1 프로젝트의 패널 제조 시작은 전역 Active 제조 양식을 다시 읽지 않고 프로젝트 제조 snapshot을 사용한다.
- `panel_manufacturing_execution_steps`에 project manufacturing step `definition_key`를 snapshot한다. 표시명과 순서는 당시 project revision을 보존한다.
- legacy 프로젝트는 지금처럼 기존 전역 template version 경로를 유지한다.
- 기존 일괄 조립 단계 완료 로직이 `MANUFACTURING` item code에 의존하므로, LinkedV1에서는 의미 역할을 별도 `step_role` 또는 선택된 definition으로 명시하고 이름·순서 추측을 제거한다.

### R6 — LQC는 고정 사건 code와 제조 단계 identity를 함께 사용

- `LQC 합격`을 프로젝트 전체의 단일 boolean으로만 연결하지 않는다. LQC는 제조 중간 단계 검사이므로 `LQC_PASSED + manufacturing_definition_key` 형태의 parameterized source를 지원한다.
- 제조 단계 완료와 해당 단계 LQC 완료는 동일한 project manufacturing definition을 기준으로 집계한다.
- OQC는 기존 단계별 최종 판정, 전진검수·FAT는 패널별 aggregate 최종 판정이라는 확정 단위를 유지한다.
- IQC는 도착분·구매품목, 제조 이후 품질·물류는 physical panel 단위를 유지한다.

### R7 — 다중 source 진행률의 분모와 중복 제거 규칙 고정

- 계획 항목의 target instance key는 최소 `(source_code, source_definition_key?, target_type, target_id)`로 고유화한다.
- 진행률은 연결된 source별 target instance 완료 수의 합 / 전체 활성 target instance 수의 합으로 계산한다. 수량을 서로 다른 업무 단위에 직접 더하지 않는다.
- 같은 패널에 OQC·FAT 두 source를 연결했다면 서로 다른 두 target instance로 계산하고, 같은 source가 중복 저장된 경우에는 unique constraint로 한 번만 계산한다.
- 실적 시작은 target instance의 최초 유효 사실, 실적 종료는 모든 target instance가 완료된 마지막 사실이다.
- source target이 0개인 연결은 완료로 보지 않고 `착수 전` 또는 `연결 안 됨`으로 구분한다.

### R8 — 프로젝트별 수정 경계 명확화

- 생산관리 정·부 담당자는 프로젝트의 생산계획 항목, 계획 시작·종료·비고와 연결만 수정한다.
- 프로젝트 제조 snapshot의 단계 정의 자체는 이번 Task에서 수정하지 않는다. 프로젝트 연결 선택지는 해당 프로젝트에 snapshot된 제조 definition과 고정 사건 catalog로 제한한다.
- project revision은 optimistic concurrency와 audit를 유지하고 template 원본·다른 프로젝트에 영향을 주지 않는다.
- 자동 실적 시작·종료와 원본 부서 사실은 모든 역할에서 직접 수정 불가다.

### R9 — 활성화·validation과 사용자 안내

- 생산계획 양식 Active 전 필수 검증: Item scope, 1개 이상 항목, 중복 definition/순서 없음, 필수 항목의 source 연결 여부, 제조 참조 존재, 지원 source code/parameter, 연결 중복 없음.
- 제조 양식 Active 전 필수 검증: Item scope, 1개 이상 단계, immutable definition, 순서 범위와 중복 없음.
- 새 version 게시 안내에는 “기존 프로젝트는 바뀌지 않음 / 이후 생성 프로젝트부터 적용”을 고정 문구로 제공한다.
- 유효한 LinkedV1 양식이 없는 Item은 기존 프로젝트와 같은 legacy 방식으로 생성된다는 점을 양식 관리와 프로젝트 생성 화면에 표시한다.

## 4. 보류

| 항목 | 판정 | 이유 |
| --- | --- | --- |
| 최초 계획 baseline 비교 | 보류 | 기존 audit로 변경 이력 확인이 가능하며 이번 핵심 가치에 필수 아님 |
| 일/주/월 zoom | 보류 | 하루 축 + 오늘 자동 이동으로 1차 사용성 충족 |
| 대상별 Gantt 막대 | 보류 | 펼침 근거 표와 중복되고 대량 패널에서 화면이 과밀해짐 |
| 구매 수량 불일치 경고 | 보류 | 별도 정책·알림 능력이며 자동 실적 연결과 분리 가능 |
| 기존 프로젝트 LinkedV1 이전 | 보류 | 사용자 결정상 기존 화면·양식 유지 |
| 새 표 Excel export | 보류 | 신규 export 능력이며 이번 조회·입력 핵심과 분리 |
| projection 저장·cache | 보류 | 단일 프로젝트 조회 실측 병목이 확인될 때 P3로 검토 |

## 5. 제거

- `snapshot 존재 여부 = 새 화면` 판정.
- legacy `planned_date` backfill과 과거 이름 exact-match 자동 연결.
- ordinal·표시명 기반 제조/LQC 연결.
- 일반 form-template item DTO에 생산계획·제조·연결 필드를 모두 얹는 단일 저장 계약.
- `키팅 완료` source의 이번 catalog 포함.
- 유효하지 않은 LinkedV1 template 조합에서 일부 snapshot 생성 또는 조용한 fallback.

## 6. 권장 구현 순서

1. `0058` additive schema: explicit model version, Item별 template/version/definition/connection, project snapshot/revision, execution step identity와 제약.
2. 전용 양식 Backend: manager domain 확장, Item별 제조·생산계획 Draft/Active/Archived, immutable key 복제, 활성화 검증.
3. 프로젝트 생성 transaction 통합: direct/bulk 공용 helper, LinkedV1/Legacy 명시 분기, snapshot·audit.
4. 제조 start의 LinkedV1 project snapshot 사용과 LQC definition identity 연결.
5. 부서 source adapter와 단일 deterministic projection.
6. 프로젝트 계획 revision API와 권한·audit.
7. 양식 관리 통합 UI, 프로젝트 수정 UI, 6열 표·펼침·Gantt·390px 카드.
8. migration fresh/existing, Backend/Frontend 전체 회귀, isolated E2E와 screenshot.

## 7. 검증 강화

- 기존 migration DB의 모든 프로젝트가 `Legacy`로 남고 기존 화면·계산이 byte-level contract상 변하지 않는지 확인.
- LinkedV1 양식 없는 Item의 새 프로젝트가 legacy로 생성되는지, 유효한 양식 활성화 뒤 새 프로젝트만 LinkedV1인지 확인.
- 제조 version 교체로 production-plan connection이 끊긴 상태에서 활성화/프로젝트 생성이 fail-closed하는지 확인.
- direct·Excel·UL891 set 프로젝트 생성이 같은 snapshot 계약을 따르는지 확인.
- 이름·순서 변경 후 immutable identity로 연결이 유지되고 삭제 시 새 version 활성화가 차단되는지 확인.
- LQC가 제조 단계 identity별로 시작·합격 집계되고 제조/OQC gate가 회귀하지 않는지 확인.
- mixed source의 target instance 중복 제거·부분 완료·Pending·재검사·FAT optional·취소 제외를 각각 독립 fixture로 검증.
- 기존 전역 제조 template 기반 실행·일괄 조립·품질·물류·18단계 진행률 회귀 0.

## 8. 2차 기획 resolution

- `키팅 완료`는 이번 source catalog에서 제외한다.
- `발주 완료`는 기존 구매품목의 발주일이 유효하게 입력·저장된 시점을 실적 사실로 사용한다.
- 위 R1~R9는 사용자 확정 정책의 의미를 바꾸지 않는 구현 안전 resolution이며 2차 기획에 모두 반영한다.
- blocking 사용자 결정은 남지 않았다. experiment fast-track의 Fable 2차 기획으로 진행할 수 있다.

