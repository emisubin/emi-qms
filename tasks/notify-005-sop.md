# TASK-NOTIFY-005 SOP — 사용자별 알림 설정

## 1. 적용 범위

이 SOP는 experiment branch에서 구현된 사용자별 외부 알림 opt-out의 개발·격리 검증 절차다. Persistent UAT 적용, 대표 runtime 교체, 실제 Teams/Mail 발송과 `main` 반영은 포함하지 않는다.

## 2. 안전 경계

- DB migration은 `0041_user_notification_preferences.sql` 하나이며 additive 3-table 구조다.
- 검증 DB 이름은 `emi_qms_e2e_*`, 전용 Compose project, tmpfs storage를 사용한다.
- `Notifications:Teams/Mail/TeamsActivity`와 worker는 격리 검증에서 disabled 또는 dry-run으로 유지한다.
- 5081/5174, 대표 repo, Persistent UAT DB를 재시작·수정하지 않는다.
- 기존 migration을 rollback하거나 수정하지 않는다. 오류 시 신규 forward-fix migration을 만든다.

## 3. 개발 검증 절차

1. 현재 branch와 HEAD, dirty 범위를 확인한다.
2. `dotnet build backend/Emi.Qms.sln -c Release --no-restore`를 실행한다.
3. preference·migration filtered tests를 실행한다.
4. `dotnet test backend/Emi.Qms.sln -c Release --no-restore` 전체 회귀를 실행한다.
5. `pnpm --dir frontend lint`, `typecheck`, `test`, `build`를 실행한다.
6. `scripts/e2e-full-stack.sh` 또는 같은 안전 guard를 사용하는 전용 runtime에서 UI를 확인한다.
7. 본인·관리자 화면을 desktop과 390 viewport로 확인하고 `document.scrollWidth <= viewport client width`를 검증한다.
8. 테스트 runtime, database, container/network를 종료하고 삭제 확인한다.

## 4. 기능 점검

- GET 본인/관리자 응답은 7개 taxonomy와 version을 반환한다.
- 변경 가능 항목은 `자동 단계 업무 생성`, `예정일 임박 D-1`, `일일 업무 요약` 3개다.
- 저장·복원은 `expectedVersion`이 일치할 때만 성공한다.
- no-op은 `changed=false`, version·audit 불변이다.
- opt-out delivery는 삭제하지 않고 `SuppressedByUserPreference`, attempt 0, next attempt 없음으로 기록한다.
- urgent, L1~L3, TeamsChannel, 인앱, 관리자 수동·테스트 발송은 preference 영향을 받지 않는다.

## 5. 장애·복구

- 409: 화면의 로컬 선택을 유지하고 다시 불러온 뒤 재저장한다.
- 400 locked/unsupported: payload taxonomy와 UI version을 대조한다.
- 403: 본인 endpoint 사용 여부 또는 `AdminUsersRead` 권한을 확인한다.
- 404/409 inactive: 관리자 사용자 목록에서 대상 상태를 새로고침한다.
- Migration 실패: Persistent DB를 되돌리지 말고 원인 수정 후 신규 additive forward-fix를 만든다.
- Provider 호출 흔적이 발견되면 검증을 즉시 중단하고 격리 설정·worker activation을 재확인한다.

## 6. 운영 반영 전 별도 Gate

- 사용자 검수 완료
- `main` merge 분리 승인 3/3
- push/PR/merge 각각 별도 승인
- Persistent UAT migration 적용·runtime handover·rollback rehearsal 승인
- 실제 provider 미호출 smoke 기준 승인
