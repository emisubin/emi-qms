# PMS 업무·코드·환경 찾기

항상 읽는 사전이 아니라 변경할 업무의 근거를 찾는 안내다. 실행 원칙은 [Root](../../AGENTS.md), 순서는 [Roadmap](../00-product-roadmap.md), 구체적인 현재 범위·검증은 해당 Task가 소유한다.

## 기준선

하네스 재구축 시 확인한 원격 main은 `c3a3c79374babc840dca054bd1a05237a2c49685`다. 이 하네스 branch의 제품 코드는 그 main과 다르다. 다음 제품 구현은 최신 source와 해당 Task를 확인해 시작하며 이 문서의 당시 관측을 현재 운영 검증으로 쓰지 않는다.

## 구조

| 영역 | 먼저 찾을 곳 |
| --- | --- |
| 화면·API client | `frontend/src`, `frontend/package.json`, [Frontend AGENTS](../../frontend/AGENTS.md) |
| endpoint/service/store/provider | `backend/src/Emi.Qms.Api`, [Backend AGENTS](../../backend/AGENTS.md) |
| Backend tests | `backend/tests/Emi.Qms.Api.Tests` |
| SQL migration | `database/migrations`와 해당 branch의 Directory/business catalog·identity 검사 |
| CI·실행·배포 | `.github/workflows`, `scripts`, `infrastructure`, [Scripts AGENTS](../../scripts/AGENTS.md) |
| 설계 결정·현재 작업 | `docs/adr`, 해당 `tasks/<purpose>`의 최신 change |

## 청주 업무 계약

| 변경할 업무 | 확인할 승인 자료 |
| --- | --- |
| 업무·패널·QR·18단계 | [확정 요구사항](../11-confirmed-requirements-baseline.md), [업무 흐름](../02-business-flow.md), [QR 단위 ADR](../adr/0002-product-and-qr-unit.md), 해당 Task 최신 change |
| 권한·승인 이력 | [권한 문서](../04-permission-matrix.md), [이력 ADR](../adr/0003-immutable-approval-history.md), ADMIN/Identity의 최신 Task |
| 자재·구매·IQC | [자재 계획](../14-material-receiving-plan.md), [IQC](../16-iqc-digital-report-plan.md), [외함 routing](../48-enclosure-iqc-routing-plan.md) 및 품질 운영 모델 Task |
| 제조·생산관리·양식 | [제조](../18-manufacturing-work-plan.md), [생산관리](../43-production-control-plan.md), [양식 관리](../33-form-template-management-plan.md), PRODUCTION-CONTROL/ADMIN 최신 change |
| 품질·Pending·물류·정산 | [검사](../19-quality-inspections-plan.md), [물류](../20-logistics-plan.md), [정산](../21-sales-settlement-plan.md), WORKFLOW-CONTINUITY·각 Task 최신 change |

단계·QR 기준, 승인 이력, 필수 업무 기반 진행률, 기존 프로젝트 snapshot·legacy 호환을 하네스 정리 때문에 바꾸지 않는다. 예전 제품 기획과 후속 승인/코드가 다르면 그 업무의 최신 결정을 확인한다. 특히 제조 양식의 snapshot 보존과 기존 프로젝트 동기화 문구는 적용 project/version을 구분하기 전 임의로 하나로 통일하지 않는다.

## 사업부와 오산

확인한 main의 [오산 인계](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-pilot-001-rollout-handoff.md), [접근 Change008](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-access-001-change-008.md), [프로젝트 Change005](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-project-001-change-005.md)가 후속 작업의 출발 근거다. 최신 main에서 같은 경로를 다시 확인한다.

- Directory·청주·오산 DB를 분리하고 서버는 공유 가능하다. 준비 실패·잘못된 사업부를 다른 DB로 fallback하지 않는다.
- 기존 관리자 사용자 관리에서 활성·사업부·부서·역할·부서장을 처리한다. 부서 자동 역할, 명시 역할, 총괄 역할의 출처를 보존하고 부서 이동의 잔여 자동 권한을 회수한다.
- 총괄 관리자는 여러 명이며 양 사업부 데이터 조회·입력이 가능해야 한다. 일반 사용자는 한 사업부만 지정한다. 승인 readiness·local profile/permission·Directory membership의 일관성을 지킨다.
- 단일 사업부 자동 진입, 허용된 총괄의 우측 상단 전환, 기존 승인 대기 화면, 청주 관리자 메뉴를 재사용한다.
- 프로젝트 생성 입력은 title·프로젝트 코드·거래처·PO No·W/O No·납기일·제품명 자유 텍스트·수량이다. 오산 목록·상세는 청주 공용 UI를 재사용하고 상세 부서 탭은 진행 관리다.
- 오산은 “제조 단계” 대신 “진행 단계”라고 부른다. 입고검사 → 배치검사 → 배선검사 → 8계통 → 동작검사 → 출하검사 → 포장 순서다. Pending·중단·별도 관리 메뉴는 추가하지 않는다.

아래는 **후속 진행 Task에서 구현할 계약**이며 이미 배포한 동작이 아니다. 개별 진행은 다음 미완료 단계, 1~6단계 일괄 처리는 선택 단계만 적용한다. 포장은 앞 6단계가 필요하고 하나라도 권한·조건·version 위반이면 일괄 요청 전체 rollback이다. 마지막 대상 포장과 프로젝트 자동 완료는 같은 transaction이어야 한다. 재개방 등 미확정 정책은 하네스가 결정하지 않는다. [진행 Task](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-progress-001.md)를 확인한다.

감사 당시 Directory ledger `0004`, business `0088`, identity contract `0001`/`0086`은 종류가 다른 값이다. 이후 번호나 현재 readiness를 고정하지 말고 실제 catalog·ledger·identity 의미를 비교한다.

## 환경과 안전한 재개

| 목적 | 먼저 읽을 자료 |
| --- | --- |
| 기존 수동 UAT | [UAT SOP](../../tasks/uat-001-sop.md)와 최신 UAT/handover change. 5081/5174의 실제 protocol·source·owner 확인 |
| HTTPS Development | [HTTPS 안정화 기록](../../tasks/uat-001-https-dev-stability.md). 5174 branch 전환은 HMR/reload 확인, 설정·dependency·기동 조건 변경 또는 실제 갱신 실패 때만 재시작 판단 |
| 격리 E2E | [격리 SOP](../../tasks/e2e-isolation-001-sop.md), [검증표](validation-matrix.md), 실행할 script의 실제 DB guard |
| 과거 실험 검수 | [실험 원장](../27-experiment-task-ledger.md). 과거 주소의 상시 재시작 지시로 해석하지 않음 |
| Azure 공개·복구 | [배포 SOP](../../tasks/azure-deploy-001-sop.md)와 최신 AZURE-DEPLOY change의 source·migration·승인·rollback |

Node/pnpm/.NET 버전은 `.node-version`, root/frontend package, backend 설정을 확인한다. 이미 있는 계정/DB/서버를 새 bootstrap로 초기화하지 않는다. 실행 중 Backend의 binary·worker가 branch 전환을 따라간다고 가정하지 않는다. 사용자가 나중에 삭제할 컨테이너와 소유 불명 자원은 자동 정리하지 않는다.
