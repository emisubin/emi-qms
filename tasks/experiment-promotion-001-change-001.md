# TASK-EXPERIMENT-PROMOTION-001 Change 001

## 사용자 결정과 승인

- 사용자는 기존 대표 데이터와 실험 데이터 보존이 필요 없으며 빈 데이터베이스에서 다시 시작한다고 확정했다.
- 사용자는 실험 계보의 마지막 일괄 사용자 검수를 모두 완료했다고 확정했다.
- 사용자는 `main` 병합을 세 번째로 분리 승인했다. 현재 병합 승인 상태는 `3/3`이다.
- 승인 범위는 local DB handover, 전체 검증, branch push, Ready PR, CI 통과 후 `main` merge와 local main 동기화를 포함한다.
- 실제 Teams·메일 provider 발송, production hosting·domain 전환과 branch/worktree 삭제는 포함하지 않는다.

## DB handover 계약

- 공식 UAT DB `emi_qms_uat_005a`는 Repository 불변조건상 drop·truncate·reset하지 않는다.
- 기존 공식 UAT DB는 `emi_qms_uat_005a_archive_b8f3e210`으로 이름을 바꿔 복구 가능한 상태로 보존한다.
- 같은 공식 이름 `emi_qms_uat_005a`의 새 빈 DB를 만들고 migration `0001`~`0064`, 기준 master data와 개발 검수 사용자를 적용한다.
- experiment DB `emi_qms_experiment_validation_41164`는 소유 runtime을 정상 종료한 뒤 drop·recreate하고 migration `0001`~`0064`와 synthetic 개발 검수 seed를 적용한다.
- 두 runtime의 actual provider와 mutation worker는 기존 검수용 비활성·dry-run 경계를 유지한다.

## 게시 계약

- 현재 experiment HEAD는 latest `origin/main`의 직계 후손이어야 한다.
- 게시 전 Backend 전체, Frontend lint·typecheck·unit·build, fresh migration과 isolated Full-Stack 회귀를 통과한다.
- 변경 파일·secret·개인정보·generated artifact를 재검사하고 승격 문서만 명시적으로 stage한다.
- experiment branch를 push하고 Ready PR을 생성한다.
- GitHub 표준 CI가 최신 head에서 모두 성공하고 mergeable 상태일 때만 PR을 merge한다.
- direct `main` push는 사용하지 않는다.

## Rollback

- PR merge 전: branch와 새 DB를 보존하고 merge를 중단한다.
- 공식 UAT cutover 실패: 새 `emi_qms_uat_005a`를 별도 실패 이름으로 격리하고, 보존한 `emi_qms_uat_005a_archive_b8f3e210`을 공식 이름으로 복구한다.
- PR merge 후 코드 문제: Git revert 또는 forward-fix PR을 사용한다.
- DB 문제: Git revert만으로 DB가 복구되지 않으므로 보존 DB 재전환 또는 fresh DB 재생성을 사용한다.
