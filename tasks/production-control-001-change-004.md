# TASK-PRODUCTION-CONTROL-001 Change 004 — 품질 현재 양식과 단계별 실적 연결 정리

## 1. 실행 기준

- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- startHead: `a7651b5`
- roadmapSequenceMatch: `true`
- source: 사용자 명시 정리·수정 요청

## 2. 확인된 문제

1. 자재 수입검사·LQC·OQC가 `v1`, `v2` 버전 목록과 초안·활성화 흐름을 계속 노출해, 단일 현재 양식 정책과 사용 방법이 다르다.
2. 전진검수와 FAT는 이미 패널 단위 통합 적합/부적합 판정으로 구현되어 항목별 양식이 필요하지 않지만 양식 종류에 남아 있다.
3. 전역 `제조 작업 단계`와 Item별 제조 양식이 함께 노출되어 같은 제조 단계를 두 곳에서 관리하는 것처럼 보인다.
4. 실제 신규 프로젝트는 Item별 제조 양식을 사용하고, 전역 제조 양식은 Legacy 프로젝트와 최초 seed 호환용이다.
5. 생산계획 실적 연결에서 제조와 LQC는 세부 단계를 선택하지만 IQC와 OQC는 검사 전체 결과만 선택해, IQC·OQC 검사 항목별 실적일 연결 정책을 충족하지 않는다.

## 3. 변경 계약

- 양식 종류에는 사용자가 실제 관리해야 하는 `자재 수입검사`, `LQC 검사`, `OQC 자체검수`, `Item별 제조 양식`, `생산계획·실적 연결`만 표시한다.
- 전진검수·FAT 양식과 전역 제조 작업 단계는 관리 목록에서 제거한다.
  - 전진검수·FAT의 실제 검사는 항목 없이 패널 통합 적합/부적합 판정을 유지한다.
  - 전역 제조 양식의 기존 데이터는 Legacy 실행과 최초 Item별 양식 seed 호환을 위해 읽기 전용으로 보존한다.
- IQC·LQC·OQC도 버전 번호·초안·활성화 UI를 제거하고 `조회 → 수정 → 저장/취소`의 현재 양식 하나만 표시한다.
- 품질 검사 이력은 양식 version FK를 사용하므로, 화면에 version을 노출하지 않더라도 이미 사용된 내부 snapshot은 보존한다.
- 현재 품질 양식 저장 시 새 업무에 적용할 내부 snapshot을 원자적으로 교체하고, 검사 항목의 불변 `definition_key`를 같은 의미의 다음 snapshot으로 승계한다.
- IQC와 OQC 생산계획 실적 연결은 드롭다운에서 검사 항목 하나를 선택한다.
- IQC·OQC 검사 항목 실적은 해당 항목의 검사 응답이 적합 또는 해당없음이고 검사 전체가 합격 확정되었을 때 완료로 계산한다.
- 기존의 IQC·OQC 전체 합격 연결값은 조회 호환을 유지하되, 새 양식과 프로젝트 수정에서는 항목 선택을 필수로 한다.

## 4. 보존 조건

- 완료된 IQC·LQC·OQC 성적서, 재검사와 Pending 이력의 template item 참조를 삭제하거나 재해석하지 않는다.
- 기존 프로젝트의 Item별 제조·생산계획 snapshot을 변경하지 않는다.
- Legacy 프로젝트와 전역 제조 양식 기반 실행을 깨지 않는다.
- 전진검수·FAT의 패널 단위 적합/부적합 및 증빙 요구를 유지한다.
- main, 대표 repo, Persistent UAT와 실제 provider를 변경하지 않는다.

## 5. 검증 계약

- migration에서 품질 항목 `definition_key`, IQC·OQC 단계 연결 제약과 기존 데이터 보존을 검증한다.
- Backend에서 현재 양식 저장, 단계 catalog, 잘못된 단계 연결 거부와 단계별 실적 projection을 검증한다.
- Frontend에서 v1/v2·전진검수·FAT·전역 제조 메뉴 제거, 현재 양식 수정/저장과 IQC·OQC 항목 드롭다운을 검증한다.
- Backend·Frontend 관련 회귀, frontend lint/build와 고정 PC 검수 주소의 실제 화면을 확인한다.

## 6. 안전 경계

- implementationApproved: `true`
- implementationApprovalSource: `USER_EXPLICIT_FIX_REQUEST`
- commitApproved: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
