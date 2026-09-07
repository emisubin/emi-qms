# 오산 시범 운영 — 기존 Azure 배포 Task로의 인계

- canonicalTaskId: `TASK-AZURE-DEPLOY-001`
- sourceTask: `TASK-OSAN-PILOT-001`
- taskType: `UAT_RUNTIME`
- status: `PLANNED_HANDOFF`
- reuseExistingTask: true
- productionDeploymentApproved: false
- migrationExecutionApproved: false
- gitPublicationApproved: false

## 목적과 기존 Task 재사용

[오산 기획](osan-pilot-001-planning.md)의 운영 개통은 기존 Azure 배포 Task의 책임이다. 같은 목적의 TASK-OSAN-ROLLOUT 같은 별도 Task를 만들지 않는다. 현재 마지막 확인 change는 [Change 030](azure-deploy-001-change-030.md)이며, 실행 시 최신 identity gate를 확인해 그 시점의 다음 change 번호를 사용한다. 지금 다음 번호를 예약하거나 과거 배포 승인을 재사용하지 않는다.

## 선행조건

[통합 검증 Task](osan-validation-001.md) 통과, 기획·review resolution·코드 검수, 필요한 사용자 검수, 해당 Git 게시·main 병합과 운영 개통 범위의 명시 승인. 한 메시지가 해당 범위를 명시하면 동일 승인을 반복 요청하지 않는다.

## 개통 범위

- 실제 기존 PostgreSQL 서버의 capacity·backup·network·identity·secret 참조를 read-only 점검하고 확인 결과로 필요한 자원 변경만 확정한다.
- 같은 서버에 오산 업무 DB와 승인된 최소 공통 directory, 계정·권한·schema를 준비한다. 새 PostgreSQL 서버는 이 기획의 기본 범위가 아니다.
- 기존 청주 DB/사용자 ID/업무 데이터와 G2를 보존하고 확인된 기존 계정의 membership만 연결한다.
- exact source의 호환 가능한 app/migration을 승인된 순서로 적용한다. 오산 capability는 준비 완료 전 비활성 상태를 유지한다.
- 지정 총괄·오산 관리자의 실제 소속/권한을 승인된 private 입력으로 연결하고 오산 외부 provider는 계속 비활성화한다.
- 청주 확인 → 제한된 오산 사용자 확인 → 개통의 순서로 진행한다. 실제 사용자를 대신해 검수 완료로 기록하지 않는다.

## 실패·복구와 완료 증빙

OSAN-REVIEW-003의 오입력 신고·담당자·변경 금지 안내를 [통합 검증 Task](osan-validation-001.md)에서 인수한다. 실제 정정/재개는 사용자 권한과 기록 보존 계약의 별도 승인 없이는 수행하지 않는다.

한 DB 실패를 다른 DB로 fallback하지 않는다. 오산 개통 실패 시 오산 진입/작업을 차단하고 청주 호환성을 확인한다. directory 도입 전 코드로 되돌릴 수 있는지와 실제 migration 호환성을 사전에 검증하며 단순 image rollback만으로 복구된다고 가정하지 않는다.

데이터 복구는 승인된 대상 DB에 한정한다. 전체 서버 복구본을 운영 청주·오산에 일괄 덮어쓰지 않는다. source·configuration·DB compatibility·worker ownership·rollback을 기존 배포 SOP와 연결해 확인한다.

완료 증빙은 실제 적용 source, DB별 ledger/권한·health projection, 청주 회귀, 오산 1/N 대상 전체 완료, 외부 provider 비활성, 사용자 검수와 후속 관찰이다. Secret·개인 식별자·업무 원문은 tracked 기록에 넣지 않는다.
