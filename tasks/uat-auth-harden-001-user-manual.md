# TASK-UAT-AUTH-HARDEN-001 User Manual

## 1. 무엇이 적용됐나요?

서로 다른 관리자가 동시에 사용자 비활성화, System Administrator 역할 제거 또는 삭제를 요청해도 마지막 active System Administrator가 사라지지 않도록 하는 보호가 Development UAT에 적용됐습니다.

## 2. 화면이 바뀌나요?

아니요. 새 화면, 버튼, 입력 항목과 API response shape는 추가되지 않았습니다. 기존 사용자 관리 화면과 HTTP 400 안내를 그대로 사용합니다.

## 3. 동시 변경 시 어떤 결과가 나오나요?

두 요청을 모두 허용하면 관리자가 0명이 되는 경우 안전한 범위의 요청만 성공합니다. 마지막 관리자를 제거하게 되는 요청은 기존 업무 오류로 거부됩니다.

거부된 요청은 사용자 상태, 역할과 삭제 상태를 일부만 변경하지 않고 전체 rollback됩니다.

## 4. 오류가 표시되면 어떻게 하나요?

마지막 System Administrator를 변경하려 했다면 먼저 다른 active Entra 사용자를 System Administrator로 지정하고 실제 로그인·권한을 확인한 뒤 다시 시도합니다.

DB 오류, timeout 또는 일반 서버 오류는 마지막 관리자 업무 오류와 다릅니다. 반복되면 운영자에게 문의하고 강제로 role 또는 DB row를 수정하지 않습니다.

## 5. 삭제와 purge는 어떻게 보호되나요?

정상 삭제에서는 삭제 예약 transaction이 마지막 administrator를 먼저 보호합니다. Purge 단계의 추가 guard는 삭제 lifecycle marker와 active administrator 상태가 비정상적으로 함께 존재할 때 physical deletion을 막는 defense-in-depth입니다.

Due purge에서 이 방어가 작동하면 batch 전체가 rollback됩니다. 이는 이미 손상된 데이터를 자동 복구한다는 뜻은 아닙니다.

## 6. 인증 정책이 바뀌나요?

아니요. Microsoft Entra 인증, 승인 대기, Dev persona read-only, bootstrap과 복구 정책은 변경되지 않았습니다. Dev persona와 승인 대기 사용자는 active Entra administrator count에 포함되지 않습니다.

## 7. 운영 데이터로 직접 시험했나요?

아니요. 실제 Persistent UAT의 유일 administrator를 비활성화·role 제거·삭제하는 시험은 수행하지 않았습니다. Break-glass 경로가 증명되지 않아 계속 `NO_GO`입니다.

동시성·실패·purge 검증은 실제 HTTP와 PostgreSQL transaction을 사용하되 synthetic identity와 isolated DB에서 수행했습니다. Persistent UAT에서는 read-only snapshot과 runtime 적용 후 불변성만 확인했습니다.

## 8. 사용자가 별도로 해야 할 작업

일상 사용을 위해 새 설정을 할 필요는 없습니다. 관리자 변경 전에 다음만 확인합니다.

1. 최소 두 명의 실제 active Entra System Administrator가 준비됐는지 확인합니다.
2. 새 관리자가 실제 로그인하고 관리 기능을 사용할 수 있는지 확인합니다.
3. 그 뒤 기존 관리자를 비활성화하거나 역할을 제거합니다.

## 9. 하면 안 되는 작업

- 마지막 administrator를 강제로 비활성화
- Direct SQL로 user/role 상태 변경
- 오류가 난 삭제·purge row를 임의 삭제
- Backup을 자동으로 restore
- 검증 목적으로 실제 알림 또는 provider 호출 생성

## 10. 현재 검수 환경

- Development: `https://localhost:5174`
- Review-safe: `https://localhost:5190`
- Preview 5185: maintenance 격리로 DOWN

Development는 latest-main이며 정상 worker/provider 정책을 사용합니다. Review-safe는 계속 DB read-only와 mutation 차단 상태입니다.

## 11. 알려진 제한

- Application이 지원하는 mutation 경로를 보호하며 Direct SQL을 DB trigger로 차단하지 않습니다.
- Persistent live last-admin mutation은 별도 break-glass·복구 증빙 전까지 No-Go입니다.
- Review-safe는 이번 Task에서 source를 교체하지 않고 기존 fallback으로 유지했습니다.

## 12. 사용자 검수 체크리스트

- [ ] Development 5174가 정상적으로 열림
- [ ] 관리자 사용자 화면의 기존 목록·역할 표시가 정상임
- [ ] Review-safe 5190이 조회 전용으로 유지됨
- [ ] 마지막 administrator 변경 요청이 거부될 수 있음을 이해함
- [ ] 거부 요청은 partial update 없이 rollback됨을 확인함
- [ ] Persistent live user/role/deletion mutation이 수행되지 않았음을 확인함
- [ ] Direct SQL·자동 backup restore 금지를 확인함
- [ ] PR Ready·merge 여부를 별도로 승인함
