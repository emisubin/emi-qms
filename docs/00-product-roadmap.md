# 제품 방향과 실행 큐

이 문서는 제품의 우선순위·선행조건과 **현재 Task 기록의 위치**를 소유한다. Task의 완료·승인·테스트 수치를 여러 표에 복사하지 않는다. 실행 규칙은 [AGENTS](../AGENTS.md), 업무 계약을 찾는 방법은 [PMS 안내](development/pms-project-guide.md)를 따른다.

## 현재 작업과 다음 제품 작업

오산 후속 개발의 전체 실행 순서는 아래와 같다. 순서 번호는 실행 단계를 뜻하며 같은 Task를 검수·배포 단계마다 새 ID로 복제하지 않는다.

| 순서 | 목적과 canonical Task | 선행조건·다음 행동 |
| --- | --- | --- |
| 준비 | 최신 제품 코드 + 하네스 v2의 작업 기준 준비 — 기존 GOV/UX 작업의 준비 범위 | 최신 main과 local commit·WIP·runtime source를 확인하고 안전하게 통합. 새 초기화·기존 WIP 정리·원격 merge를 자동 수행하지 않음. Figma 지정과 독립적으로 조사 가능 |
| 1 | [TASK-OSAN-UX-001](../tasks/osan-ux-001.md) — 지정 Figma 구현 기준·기능 범위 확정 | 원본 치수·자산·화면 상태와 현재 구현 차이를 확보하고 사진 세부 설계를 정리. 합의된 예외 외 100% 동일 구현 기준 적용 |
| 2 | [TASK-DESIGN-LOGIN-001 Change 011](../tasks/design-login-001-change-011.md) — 공통 PC·모바일 로그인 | PC·모바일 원본 보정과 직접 검증 완료. 최종 동일성·일괄 검수는 Change 011의 남은 검증을 따름. 로그인·승인·재인증 동작 보존 |
| 3 | [TASK-OSAN-PROGRESS-001](../tasks/osan-progress-001.md) — 모바일 진행 상세·완료 | 2026-09-09 6건 보정 사용자 검수 완료([기록](../tasks/osan-photo-001.md)). 보존 계약: 작업 시작 제거, 개별·일괄 1~6단계 순서 무관 완료, 포장은 앞 6단계 완료 필수, 전체 대상 포장 후 프로젝트 자동 완료. 목록 집계·대상별 상세 연결·Figma 패널 선택 보정 |
| 4 | [TASK-OSAN-PHOTO-001](../tasks/osan-photo-001.md) — 완료 증빙 사진 | 촬영·첨부·미리보기·영구 저장·완료 후 재조회, 진행 상세·완료 처리와 통합. DB 원본·제한은 승인된 PHOTO 계약 적용, 일괄 대상별 기록과 사진 조회 연결 |
| 5 | [TASK-OSAN-DASHBOARD-001](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-dashboard-001.md) — 모바일 진행 현황 | 요약·목록·검색·필터·진행률·페이지 이동·상세 연결. 진행 상태 원본과 권한 경계 유지 |
| 6 | [TASK-OSAN-VALIDATION-001](https://github.com/emisubin/emi-qms/blob/c3a3c79374babc840dca054bd1a05237a2c49685/tasks/osan-validation-001.md) — 통합·Figma 동일성 확인·일괄 사용자 검수 | 구현 중 직접 검증 증거를 재사용하고 기능 연결·동일 폭/상태의 디자인 차이를 확인. 마지막에 일괄 사용자 검수 |
| 7 | TASK-OSAN-VALIDATION-001의 최종 후보 회귀 | 사용자 검수 보정 후 전체 회귀 책임 실행 1회. 각 구현 중 영향 검증과 required CI는 생략하지 않으며 변경·실패·새 위험의 영향 범위만 추가 검증 |
| 8 | 명시 승인 후 원격 main 병합 | 자동 검증·사용자 검수 뒤 해당 병합의 명시 승인 1회. 이번 요청에는 push·병합 미포함 |
| 9 | TASK-AZURE-DEPLOY-001 후속 change — 승인된 공개배포 | 별도 배포 승인 후 source·DB 호환성·migration·복구 경계를 확인하고 배포 및 사용자 동작 확인. 이번 요청에는 미포함 |

현재 다음 구현은 순서 5의 모바일 진행 현황 보정이다. 로그인·진행·사진의 기존 구현을 다시 만들지 않고 각 Task의 최종 검증 잔여는 순서 6으로 연결한다.

기존 프로젝트 생성·PC 목록·상세 보정은 실제 필요한 경우에만 TASK-OSAN-PROJECT-001 후속 change로 연결한다. 지정하지 않은 PC 프로젝트 화면은 재설계하지 않고 같은 목적의 Task를 중복 생성하지 않는다. 사진 설계는 UX/진행 계약 확인과 함께 준비하되, 단계 설명용 안내 사진과 완료 증빙 사진을 구분한다.

데이터 분리 `TASK-OSAN-ISOLATION-001`, 승인·접근 `TASK-OSAN-ACCESS-001`, 프로젝트 생성 `TASK-OSAN-PROJECT-001`의 1차 공개 범위는 아래 배포 근거로 보존한다. 위 순서는 그 범위의 재개발을 포함하지 않는다. 기존 접근 기능의 실제 계정 검수 잔여와 사용자가 직접 정리할 자원은 별도 추적 항목을 유지한다.

2026-09-09 사용자 승인으로 지정 Figma·공통 로그인·사진 기능을 포함한 실행 순서를 갱신했다. 지정된 오산 화면은 Figma가 이전 청주 동일 디자인 기준을 대체하며, 미지정 영역은 기존 구성을 유지한다. 디자인에서 드러난 새 업무 기능은 범위를 확인해 반영한다. 디자인에 의존하지 않는 조사·기준선 준비는 계속할 수 있다.

최초 재개 요청의 로드맵·문서 정리 뒤, 2026-09-09 사용자가 권장 사진 설계의 구현을 승인했다. 현재 구현 범위와 검증·남은 일은 [PHOTO-001](../tasks/osan-photo-001.md), 공통 로그인 보정은 [Change 011](../tasks/design-login-001-change-011.md)에 기록한다. 사진과 같은 트랜잭션으로 처리할 진행 완료 및 필수 화면을 함께 연결하며, 원격 반영·운영 DB 적용·배포는 미포함이다. 위 실행 순서와 Figma 동일성 완료 기준은 유지한다. 하네스 재구축의 기록·원격 반영 상태는 [TASK-GOV-CODEX-002](../tasks/gov-codex-002.md)에서 별도로 추적한다.

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
