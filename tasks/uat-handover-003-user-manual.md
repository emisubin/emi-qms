# TASK-UAT-HANDOVER-003 User Manual

## 1. Runtime handover란 무엇인가

DB의 알림 처리 구조와 실행 중인 서버를 최신 main 버전으로 안전하게 교체하는 작업이다. 기존 데이터를 초기화하지 않고 backup과 read-only 환경을 유지하며 진행한다.

## 2. 무엇이 달라졌나요

알림 발송 대기는 worker가 claim/lease로 한 번에 하나의 소유권을 갖는다. 운영 화면에서 Pending과 Processing을 구분하고 발송 시도 이력을 볼 수 있다.

## 3. 28/29/1의 의미

- Canonical 28: Repository의 공식 migration 수
- Live 29: DB에 보존된 전체 migration 기록 수
- Approved legacy 1: 삭제하지 않고 감사 이력으로 유지하는 승인된 과거 marker

이 차이는 데이터 손상을 뜻하지 않는다.

## 4. 접속 주소

- Development: https://localhost:5174
- Review-safe: https://localhost:5190

Development는 정상 업무 검수용이다. Review-safe는 조회 전용이며 저장·삭제·발송을 차단한다.

## 5. 사용자가 확인할 화면

- 프로젝트와 내 업무
- 알림 목록
- Teams Activity
- 관리자 Dashboard
- 알림 발송 모니터
- 수동 발송 화면

## 6. Processing과 Attempt

Processing은 worker가 lease로 처리 중인 상태다. 이 상태에서는 임의 retry·확인·제외를 하지 않는다. Attempt 이력은 각 시도의 시작, provider 경계와 결과를 설명한다.

## 7. 가능한 작업과 금지 작업

사용자 검수 중 조회·검색·필터·정렬·상세 이동은 가능하다. 별도 지시 전에는 저장, 수정, 삭제, 수동 발송과 테스트 알림 생성을 하지 않는다.

## 8. External provider 정책

Task automation은 신규 테스트 발송을 만들지 않았다. 관찰 중 사용자가 직접 실행한 수동 발송 1건은 fail-stop으로 provider 호출 전에 보존됐다. 사용자 확인 후 정상 worker가 단일 claim·attempt로 처리해 Sent가 됐으며, 이 1건 외 provider 호출은 0이다.

## 9. At-least-once 의미

정상 worker 경쟁에서는 한 delivery를 한 worker만 처리한다. 다만 provider 성공 직후 DB 저장 전에 process가 종료되면 재시도될 수 있어 exactly-once는 보장하지 않는다.

## 10. Review-safe 역할

Review-safe는 DB read-only, mutation 423, worker/provider disabled 상태다. Development 이상 시에도 조회 가능한 안전 환경으로 유지한다.

## 11. Backup과 데이터 보존

Migration 직전 fresh backup을 만들고 isolated DB에서 restore를 검증했다. Backup은 자동 복구에 사용하지 않으며 별도 승인 없이는 restore·삭제하지 않는다.

## 12. 오류가 보이면

- Ready 실패: 서버와 DB 준비 상태를 관리자에게 전달
- Processing 장기 지속: lease 만료 여부를 확인하고 임의 retry 금지
- Provider-start 증가: Development 사용 중지 후 운영자에게 전달
- 화면 오류: route alias와 status만 전달하고 화면 원문이나 개인정보를 복사하지 않음

## 13. FAQ

### Live 기록이 29개면 migration이 하나 더 실행된 것인가요?

아니다. Canonical 28개와 승인된 legacy marker 1개를 합친 수다.

### Processing이 보이면 실패인가요?

아니다. 정상 처리 중일 수 있다. Lease와 attempt를 확인한다.

### 테스트 발송을 눌러도 되나요?

별도 승인 없이 누르면 안 된다. 이번 handover에서는 사용자가 직접 실행했다고 확인한 기존 1건만 정상 처리했고 새 테스트 발송은 허용하지 않는다.

### 관찰 중 사용자 활동이 생기면 데이터 오류인가요?

항상 그런 것은 아니다. 이번에는 사용자가 의도적으로 실행한 단일 활동으로 확인돼 `AUTHORIZED_USER_ACTIVITY`로 기록했다. 시스템은 먼저 Development를 안전 중단하고, 사용자 확인과 중복·lineage 검증 후에만 재개한다.

### Candidate와 backup은 언제 정리하나요?

사용자 검수와 PR merge 후 별도 승인된 정리 Task에서 처리한다.

## 14. 사용자 검수 체크리스트

- [x] Development 접속 정상
- [x] 프로젝트·업무·알림·관리자 조회 정상
- [x] Processing filter와 attempt 이력 이해 가능
- [x] 승인된 사용자 발송 1건의 fail-stop과 단일 Sent lineage 이해
- [x] Review-safe 접속 정상
- [x] Ledger 28/29/1 이해
- [x] 승인된 delivery 1건 외 provider 호출 0 확인
- [x] Backup과 Persistent UAT 보존 확인
- [x] At-least-once 제한 이해
- [x] SOP 이해 가능
- [x] 다음 TASK-NOTIFY-ESC-001 확인

현재 상태: 사용자 검수 완료 / PR #33 squash merge 승인 / 미체크 항목 0.
