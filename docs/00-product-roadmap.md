# 제품 방향과 실행 큐

이 문서는 제품의 우선순위·선행조건과 **현재 Task 기록의 위치**를 소유한다. Task의 완료·승인·테스트 수치를 여러 표에 복사하지 않는다. 실행 규칙은 [AGENTS](../AGENTS.md), 업무 계약을 찾는 방법은 [PMS 안내](development/pms-project-guide.md)를 따른다.

## 현재 작업과 다음 제품 작업

| 순서 | 목적과 canonical Task | 선행조건·다음 행동 |
| --- | --- | --- |
| 제품 선행 | [TASK-OSAN-UX-001](../tasks/osan-ux-001.md) — Figma 기반 UI/UX·기능 범위 확정 | 사용자 지정 화면·동작을 기존 제품과 비교해 필요한 기능을 기존/신규 Task에 연결. Figma 지정 대기는 이 Task에서 추적 |
| 제품 1 | [TASK-OSAN-PROGRESS-001](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-progress-001.md) — 진행 기록과 포장 후 자동 완료 | UX-001에서 해당 화면·기능 범위를 확인한 뒤 구현. 이미 배포한 접근/프로젝트 생성 계약과 최신 main·v2 작업 기준을 유지 |
| 제품 2 | [TASK-OSAN-DASHBOARD-001](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-dashboard-001.md) — 전체 프로젝트 현황 | UX-001의 해당 화면과 진행 상태·집계 계약 확정 후 구현 |
| 제품 3 | [TASK-OSAN-VALIDATION-001](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-validation-001.md) — 후속 진행·현황 통합 검증 | 해당 구현과 관련 검증을 모아 사용자 일괄 검수·최종 후보 회귀. 1차 배포 완료 범위를 다시 구현하지 않음 |

2026-09-09 사용자 요청으로 Figma 기반 UX-001을 오산 후속 개발의 선행 범위로 추가했다. 지정된 오산 화면은 Figma가 이전 청주 동일 디자인 기준을 대체하며, 미지정 영역은 기존 구성을 유지한다. 디자인에서 드러난 새 업무 기능은 범위를 확인해 반영한다. 디자인에 의존하지 않는 조사·기준선 준비는 계속할 수 있다.

이번 등록은 미제공 디자인·새 기능의 일괄 구현이나 병합·배포 승인이 아니다. 사용자가 작업을 지정하면 실질 선행조건을 확인해 그 요청을 실행하고, 단순 큐 순서 차이로 재승인을 만들지 않는다. 하네스 재구축의 기록·원격 반영 상태는 [TASK-GOV-CODEX-002](../tasks/gov-codex-002.md)에서 별도로 추적한다.

## 기준선과 배포된 범위

2026-09-09 하네스 재구축에서 원격 main과 `origin/main`이 `c3a3c79374babc840dca054bd1a05237a2c49685`로 일치함을 확인했다. 현재 하네스 branch는 이전 제품 계보를 사용하므로 local 파일만 보고 배포 사실을 되돌리지 않는다.

오산 데이터 분리·접근/승인·프로젝트 생성의 1차 공개 및 Access Change 008 보정은 [main의 rollout 기록](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-pilot-001-rollout-handoff.md)을 기준으로 한다. 진행 mutation·7단계 자동 완료·대시보드는 그 공개 범위에 포함되지 않는다. 해당 기록의 실계정 최종 smoke 미완료를 이 하네스 감사가 대신 완료하지 않는다.

위 링크는 **확인 당시 고정 출처**다. 새 제품 작업에서는 최신 main의 같은 Task 경로·코드를 다시 확인한다. 기존 local 오산 Task WIP는 이력으로 보존됐으며, 이번 변경에서 원격 main의 제품 문서를 오래된 사본으로 덮어쓰지 않았다. Azure·실계정·DB의 현재 상태를 이번에 직접 관측한 것은 아니다.

## 별도 추적

아래 항목은 소유 Task의 현재 기록과 실제 환경을 확인한 뒤 다룬다. 관측/운영 입력을 새 기능 구현 완료와 섞지 않으며, 이 표가 실행 승인을 만들지 않는다.

| 항목 | 소유 기록·선행조건 |
| --- | --- |
| 실제 계정의 부서 이동·오산 승인·총괄 양 사업부 입력 확인 | 최신 `TASK-OSAN-ACCESS-001` change와 rollout의 사용자 smoke |
| 사용자가 나중에 직접 삭제할 legacy 테스트 자원 | 기존 `TASK-OSAN-ISOLATION-001`·통합 검증 기록. 자동 정리 재시도 없이 사용자 수동 처리 추적 |
| 운영 첨부 storage·scanner·backup/restore, 실제 알림·인증 복구 | [실험 잔여 범위 안내](27-experiment-task-ledger.md)와 해당 ATTACHMENT/NOTIFY/AUTH/UAT 운영 Task. 대상·영향의 별도 승인 |
| CI 사용량·실패/fallback 관찰 | [TASK-CI-COST-001](../tasks/ci-cost-001.md). 실행 비용 관찰을 전체 회귀 반복으로 대체하지 않음 |
| CI E2E script 분류 우선순위 검토 후보 | [설계의 정적 관찰](development/pms-harness-redesign.md). 하네스 파일 분류 외 제품 CI 경로는 이번에 미변경, 실제 영향 확인 후 같은 CI Task에서 보정 |
| 과거 사용자 검수·운영 승격·제품 정책 입력·P3 | 아래 보존된 원장/Decision Log와 해당 Task 최신 change를 대조. 오래된 대기 문구만으로 재실행하지 않음 |

## 제품 계약과 이력

청주의 18단계 업무, QR/패널·권한·Pending·양식·진행률·정산 계약은 하네스 절차 간소화의 대상이 아니다. [제품 안내](development/pms-project-guide.md)가 업무별 승인 자료를 연결한다.

- [실험 완료·후속 범위](27-experiment-task-ledger.md): 완료 기능·slice와 검수/승격 기록 찾기.
- [교체 직전 local Roadmap 원문](archive/harness-v1-2026-09-09/00-product-roadmap.md.snapshot): 기존 제품 방향·추적 대상·Decision Log·용어·승인 원문을 보존. 현재 실행 큐 아님.
- [확인 당시 main Roadmap](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/docs/00-product-roadmap.md): 원격의 기존 제품·배포 계보 대조용. 내부의 오래된 실행 규칙은 현행 AGENTS를 대체하지 않음.
- [하네스 v1 보존 목록](archive/harness-v1-2026-09-09/README.md), [재설계 승인·적용 기록](../tasks/gov-codex-002-change-024.md).

기존 원장의 항목을 없어진 업무나 승인 취소로 간주하지 않는다. 예전 Roadmap의 절/anchor 참조는 보존본의 같은 제목·Task ID로 찾는다. 새 기록은 Task의 현재 상태 한 곳을 가리키며 이전 실행 규칙을 복사하지 않는다.

## 과거 문서 링크 안내

아래 anchor는 기존 보고서의 링크를 보존하는 안내다. 현재 실행 큐가 아니며 당시 Roadmap 전문은 위 보존본에서 확인한다.

<a id="task-014a-영업-정산--세금계산서--프로젝트-완료"></a>

- TASK-014A: [정산 구현 기록](../tasks/014a-implementation-report.md).

<a id="task-uat-handover-001-patched-frontend-uat-runtime-handover"></a>

- TASK-UAT-HANDOVER-001: [handover 기록](../tasks/uat-handover-001.md).

<a id="task-uat-001-https-development-uat-안정화"></a>

- TASK-UAT-001: [HTTPS UAT 기록](../tasks/uat-001-https-dev-stability.md).

<a id="task-e2e-isolation-001-full-stack-e2e-postgresql-물리-격리"></a>

- TASK-E2E-ISOLATION-001: [격리 기록](../tasks/e2e-isolation-001.md).

<a id="task-ci-cost-001-github-actions-minute-최적화"></a>

- TASK-CI-COST-001: [CI 비용 작업](../tasks/ci-cost-001.md).

<a id="task-frontend-sec-001-frontend-dependency-security-remediation"></a>

- TASK-FRONTEND-SEC-001: [보안 보정 기록](../tasks/frontend-sec-001.md).

<a id="task-azure-pilot-001-서비스-중립-공개-파일럿-준비"></a>

- TASK-AZURE-PILOT-001: [파일럿 구현 기록](../tasks/azure-pilot-001-implementation-report.md).

<a id="task-013a-물류-포장--출발--납품-완료"></a>

- TASK-013A: [물류 구현 기록](../tasks/013a-implementation-report.md).
