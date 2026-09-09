# TASK-OSAN-PILOT-001 — 독립 기획 내용 검토

- reviewType: `INDEPENDENT_PLANNING_CONTENT_REVIEW`
- reviewerModelRequested: `gpt-6-astra/high`
- reviewerModelObserved: `NOT_REPORTED`
- taskType: `NEW_FEATURE`
- instructionChainRead: true
- reviewStatus: `COMPLETED_WITH_RESOLUTION_ITEMS`
- reviewResolutionApproved: false
- implementationApproved: false
- 검토일: 2026-09-06

## 검토 결론

승인된 오산 업무를 구현과 검증에 전달하기에 적절한 기획이다. 같은 PMS 주소와 Microsoft 365 조직, 하나의 PostgreSQL 서버와 별도 사업부 업무 DB, 정확히 8개 생성 입력, 수량별 7단계, 전체 포장 시 자동 완료라는 핵심이 일관된다. 기존 청주 업무·G2 보존과 오산의 중단·펜딩·품질/물류 후속 업무 제외도 명확하다.

여섯 개 신규 구현·검증 Task와 기존 Azure 배포 Task의 인계로 나눈 책임도 타당하다. 전체 업무 인터뷰나 기획 재작성은 필요하지 않다. 아래 두 가지 재사용 계약을 review resolution에 명시하면 구현자가 기존 코드의 숨은 정책을 잘못 가져오는 위험을 줄일 수 있다.

이번 검토에서 P0/P1은 발견하지 않았다. P2 두 건은 해당 구현 handoff 전에 resolution을 확정해야 한다. 이는 현재 제품에서 재현된 결함 판정이나 제품 구현 검증 결과가 아니다.

## 유지·추가·보류·제거

| 분류 | 권고와 이유 |
| --- | --- |
| 유지 | 8개 입력, 자유 제품명, 개별 대상, 공통 7단계와 자동 완료. 현장 입력량을 제한하면서 필요한 진행 정보를 얻는다. |
| 유지 | 소속 판별 → 업무 DB → local 권한의 순서와 지정 총괄 전환. 단일 연결 문자열과 해당 DB의 사용자 profile에 의존하는 현재 구조에서 필요한 기반이다. |
| 유지 | 기존 제조 기록·일괄 처리·감사·중복 방지 재사용. 전체 제조/품질 workflow 복제나 전역 명칭 변경을 피한다. |
| 유지 | Task별 자체 검증 후 통합 검증, 기존 배포 Task 재사용. 책임과 운영 승인 경계를 분리한다. |
| 추가 | 프로젝트의 중복 비교 규칙과 기존 Title 제약 처리, 1~6단계의 개별/일괄 처리 의미를 아래 resolution으로 고정한다. |
| 보류 | 단계 정정·완료 취소·프로젝트 재개 UI. 필요 가능성은 있으나 승인된 능력으로 소급 편입하지 않는다. |
| 보류 | 공통 directory의 최종 저장 구조와 bootstrap 방식은 Task 1의 기술 설계·구현 승인으로 확정한다. 세 번째 최소 DB는 사용자 확정 사항이 아니라 기술 권장안이다. |
| 제거 | 초기 시범에 범용 양식 편집기, 검사 결과값·성적서·불량·재작업, cross-business 집계, 신규 외부 알림을 넣는 해석. 기존 7단계 양식 연결·snapshot만으로 목적을 충족한다. |

## Findings와 권장 resolution

### OSAN-REVIEW-001 — P2 / OPEN: 기존 코드 재사용만으로 오산 프로젝트 중복 계약이 정해지지 않는다

**위치:** [기획안 3절](osan-pilot-001-planning.md), [프로젝트 Task](osan-project-001.md)

기획은 오산 내 코드 중복을 차단하고 “기존 프로젝트 코드 정규화 규칙”을 재사용한다고 적는다. 실제 [ProjectInputNormalizer](../backend/src/Emi.Qms.Api/Projects/ProjectInputNormalizer.cs)는 코드에 일반 필수 텍스트 처리를 적용한다. 코드 전용 대소문자·내부 공백 정규화는 확인되지 않았다. 반면 [기존 migration](../database/migrations/0004_project_packaging_soft_delete.sql)은 삭제되지 않은 프로젝트의 정규화 Title을 unique로 제한한다. 코드에는 일반 index가 있다.

그대로 재사용하면 서로 다른 코드에 같은 Title을 입력하는 경우 예상 밖으로 거부하거나, 코드 중복을 DB에서 차단하지 못할 수 있다.

**권장 resolution:** Task 3에 오산 코드의 비교값·DB unique 보장과 Title 제약의 profile 분리를 명시한다. 기본 권장안은 기존 코드 입력처럼 앞뒤 공백을 제거한 값을 비교하고, 승인되지 않은 Title 중복 금지를 오산에 자동 추가하지 않는 것이다. 청주의 기존 Title 제약은 보존한다. 같은 Title/다른 코드, 같은 코드/다른 Title, 코드 앞뒤 공백, 동시 생성 시나리오를 검증한다.

**결정 구분:** 현재 구현 사실의 정정과 승인된 코드 중복 차단의 기술적 완결이다. 대소문자 무시·내부 공백 통합이나 오산 Title 중복 금지를 새로 선택한다면 별도 업무 정책으로 resolution에 드러내야 한다. 전체 인터뷰를 다시 열 사유는 아니다.

**해소 위치:** TASK-OSAN-PROJECT-001 구현 handoff와 테스트 계약.

### OSAN-REVIEW-002 — P2 / OPEN: 기존 개별·일괄 처리의 순서 차이가 기획에서 드러나지 않는다

**위치:** [기획안 4절](osan-pilot-001-planning.md), [진행 Task](osan-progress-001.md)

실제 [ManufacturingStore의 개별 처리](../backend/src/Emi.Qms.Api/Manufacturing/ManufacturingStore.cs)는 첫 미완료 단계만 허용한다. 일괄 처리는 선택한 단계의 존재·미완료 여부를 확인하지만 그 앞 단계 완료를 요구하지 않는다. 기획은 기존 두 기능을 재사용하면서 포장에만 앞 6단계 완료 조건을 명시한다.

구현자가 “7단계 순서”를 모든 동작의 강제 순서로 해석하면 기존 일괄 기능을 축소할 수 있고, 반대로 개별 처리도 임의 단계 완료로 넓힐 수 있다.

**권장 resolution:** 기존 재사용을 기준으로 “개별은 다음 미완료 단계, 1~6단계 일괄은 선택한 단계만, 포장은 두 방식 모두 앞 6단계 완료 필수”라고 명시한다. 서버 요청에 포함된 대상 중 조건 불충족이 있으면 전체 rollback하며, 화면에서 미리 제외한 대상은 실행 전에 구분해 보여 준다.

**결정 구분:** 기존 기능 의미를 명시하는 clarification이다. 개별 처리의 순서 해제 또는 모든 일괄 처리의 순서 강제는 별도 업무 정책 변경이므로 자동 채택하지 않는다.

**해소 위치:** TASK-OSAN-PROGRESS-001 구현 handoff와 개별·일괄 성공/거부 테스트.

### OSAN-REVIEW-003 — P3 / BACKLOG: 잘못된 진행 입력의 운영상 처리 방식을 개통 전에 정해야 한다

**위치:** [기획안 4절](osan-pilot-001-planning.md), [통합 검증 Task](osan-validation-001.md)

수량이나 진행 단계를 잘못 입력했을 때 현재 기획은 정정 기능을 제외하고 최소 보정 계약을 별도 승인 대상으로 남긴다. 특히 마지막 포장 오입력은 프로젝트 상태와 알림까지 변경한다. 이는 초기 시범에서 사용자 신뢰에 직접 영향을 줄 수 있지만, 이번 검토를 근거로 재개 기능을 추가할 수는 없다.

**권장 resolution:** TASK-OSAN-VALIDATION-001의 운영 준비 backlog에 “오입력 발견 시 신고·담당자·변경 금지 경계 안내”를 연결한다. 실제 정정 mutation이 필요하면 권한·사유·감사·완료 상태·알림 처리까지 포함한 최소 정책을 별도 승인받는다. 직접 DB 수정이나 프로젝트 재생성을 기본 복구 방법으로 제시하지 않는다.

**결정 구분:** 안내와 책임 연결은 문서 보완이다. 정정·완료 취소 능력 추가는 새로운 사용자 정책·제품 능력이며 현재 승인 범위 밖이다.

## 구현 가능성과 운영 부담

현재 연결 제공자와 다수 store가 singleton으로 등록되고, Entra 인증 후 profile 생성·조회가 업무 DB에 연결된다. 따라서 DB 두 개를 설정에 추가하는 수준으로 끝낼 수 없다는 기획 판단은 타당하다. Task 1은 이 프로젝트에서 가장 영향 범위가 큰 작업이다. 모든 소비자의 연결 경계를 먼저 조사하고, 요청·worker·migration·health·bootstrap별 실패 차단을 검증해야 한다.

공통 directory는 업무 DB를 읽기 전에 소속을 결정하는 데 합리적인 제안이다. 다만 추가 schema·migration·bootstrap·장애 의존성이 생긴다. 계획처럼 membership과 총괄 지정만 보유하고 업무 역할·프로젝트 원본을 모으지 않는 최소 범위를 유지하는 것이 적절하다.

제조 시작은 현재 생산관리 투입 요청·양식·일부 설계 및 LQC 계약과 연결되며, 완료는 OQC와 후속 업무로 이어진다. 기획의 오산 profile과 별도 생성 계약은 이러한 의존성을 우회하면서 청주 동작을 보존하기 위한 실질적 변경이다. 명칭만 바꾸는 기능으로 산정해서는 안 된다.

## 권장 개발 순서와 인계

등록된 순서인 **분리 기반 → 접근 관리 → 프로젝트 생성 → 진행 → 현황판 → 통합 검증 → 기존 Azure 배포 Task의 개통 change**를 유지한다.

Task 1의 승인된 기술 계약, Task 3의 중복 규칙 resolution, Task 4의 단계 처리 resolution만 각 handoff에 추가하면 된다. 하위 Task마다 업무 인터뷰·primary draft·기획 review를 반복할 필요가 없다. 구현 시작 시 최신 instruction chain·identity·정확한 변경 범위와 실행 승인을 확인하는 절차는 유지한다.

현재 다음 Gate는 review resolution 확인과 TASK-OSAN-ISOLATION-001의 구현 범위 승인이다. 이 review는 제품 구현·DB 생성·운영 전환·Git 게시 승인이 아니다.

## 기준선과 검토 한계

- branch: `fix/task-gov-codex-002-instruction-clarity`
- HEAD: `574cea66f602eb65eb1d110801d331151731b0a6`
- 작업공간: 기존 dirty canonical clone, 변경하지 않음.
- 적용 지침: Root·Backend·Frontend AGENTS, 종료 정책, Validation Matrix, Privacy-safe Evidence. Scripts mutation과 experiment branch 작업은 비적용.
- 독립 review 파일은 검토 시점에 미생성 상태이며 parent가 반환 결과를 기록한다. 별도 오산 change 파일은 없고 승인 출처는 상위 Task와 interview에 기록되어 있다.
- SHA-256 고정 대상: 오산 문서 11개, Root/Backend/Frontend 지침 3개, Roadmap·종료 정책·Validation Matrix·Privacy-safe Evidence 4개, 총 18개.
- 고정된 각 파일의 SHA-256 manifest 전후 동일: `true`.
- 동일 manifest의 SHA-256: `28da4e485883788e24da9a51cc189d45f1b5e1925c7387df3eec2c8a420336bd`.
- 제품 코드·DB·runtime·provider·Git mutation: 없음.
- 코드 실행 테스트, 구현 독립 검증, 실제 Azure 용량·과금·복구 상태 확인: 미실행.

이 결과는 문서의 업무 가치·범위·의존성·인계 가능성과 관련 코드의 정적 대조에 한정한다. 사용자 문서 결과 검수와 향후 제품 검증은 별도로 남는다.

## Parent 기록과 문서 resolution

위 내용은 독립 reviewer의 반환 전문이다. 개인 로컬 경로를 tracked 문서에 남기지 않도록 링크만 저장소 상대 경로로 바꾸고 로컬 줄 위치 suffix를 제거했다. 본문·판정·권고는 수정하지 않았다. 위 OPEN 상태는 독립 검토 당시 상태이며 아래 표가 문서 보완 후의 최신 추적이다.

| ID | 문서 상태 | 반영과 남은 실행 |
| --- | --- | --- |
| OSAN-REVIEW-001 | RESOLVED — 문서 명확화 | Task 3에 trimmed code의 DB unique·청주 Title 제약 보존/오산 profile 분리·동시 생성 테스트를 고정했다. 실제 구현·검증은 미실행이며 resolution 사용자 확인은 대기다. |
| OSAN-REVIEW-002 | RESOLVED — 문서 명확화 | Task 4에 개별 next·1~6 일괄 선택·포장 전 6개 완료·요청 전체 rollback 계약과 테스트를 고정했다. 실제 구현·검증은 미실행이며 resolution 사용자 확인은 대기다. |
| OSAN-REVIEW-003 | BACKLOG | Task 6과 운영 handoff에 오입력 신고/책임/변경 금지 안내를 연결했다. 정정·재개 기능 자체는 추가하지 않는다. |

문서화 요청 범위 안에서 구현자의 임의 해석을 줄이는 technical clarification을 기록했다. 승인된 primary draft를 자동 재작성하거나 새 사용자 권한을 실행하지 않았다. 리뷰 결과와 Task의 세부 계약을 과거 사용자가 이미 승인한 것으로 소급 표시하지 않는다. 새로운 정정·완료 취소 능력이나 권한 확대는 이 resolution으로 승인되지 않는다.

현재 open 문서 P0/P1/P2는 0/0/0, 운영 준비 backlog P3는 1이다. 이는 제품 결함이 수정·검증됐다는 뜻이 아니며, 제품 구현 전 review resolution과 Task 1의 기술·실행 범위를 함께 확인한다.
