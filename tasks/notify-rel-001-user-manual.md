# TASK-NOTIFY-REL-001 User Manual

## 1. 알림 발송 처리란 무엇인가

시스템이 알림 한 건을 준비하고 외부 채널로 전달한 뒤 결과를 기록하는 과정이다. 여러 처리기가 동시에 동작해도 같은 건을 동시에 잡지 않도록 claim과 lease를 사용한다.

## 2. Pending 의미

발송 순서를 기다리거나 일시 오류 후 재시도 시각을 기다리는 상태다. 아직 특정 worker가 소유하지 않는다.

## 3. Processing 의미

한 worker가 현재 처리 권한을 임시로 소유한 상태다. 화면에는 발송 처리 중과 lease 상태가 표시된다.

## 4. Sent 의미

발송 결과가 성공으로 기록된 상태다. dry-run이나 비활성 채널은 별도 상태로 구분될 수 있다.

## 5. Failed 의미

재시도할 수 없는 오류이거나 정해진 재시도 횟수를 모두 사용한 상태다. 관리자는 오류 코드와 시도 이력을 확인한다.

## 6. 처리 중 알림을 수정할 수 없는 이유

Processing row는 한 worker가 소유한다. 이때 확인·제외·재시도를 실행하면 실제 처리 결과와 충돌할 수 있어 checkbox와 관리자 action을 비활성화한다.

## 7. Attempt 이력이란 무엇인가

한 delivery가 몇 번째로 처리됐고 어떤 결과가 났는지 남긴 기록이다. 시도 번호, 결과, 시작/완료 시각, 안전한 오류 정보를 보여준다. 내부 claim token과 worker 원문은 보이지 않는다.

## 8. 처리 지연과 lease 만료 의미

Lease는 worker의 임시 처리 권한 유효기간이다. 만료되면 다른 worker가 회수할 수 있다. Provider 호출 후 만료되면 외부 성공 여부가 불확실할 수 있어 별도 결과로 표시한다.

## 9. 중복 가능성이 완전히 0이 아닌 이유

정상적으로 동시에 처리하는 중복은 차단한다. 다만 외부 채널은 성공했는데 시스템이 그 결과를 저장하기 직전에 중단되면 다음 시도에서 다시 보낼 수 있다. 이를 exactly-once가 아닌 at-least-once라고 한다.

## 10. 관리자 화면에서 확인하는 방법

1. 관리자에서 알림 발송 상태를 연다.
2. 발송 처리 중 filter와 count를 확인한다.
3. lease 유효 또는 만료 안내를 확인한다.
4. row 상세에서 발송 시도 이력을 연다.
5. Processing action이 비활성화됐는지 확인한다.

## 11. 재시도와 실패 구분

일시 오류는 Pending으로 돌아가 재시도 예약이 표시된다. 영구 오류나 횟수 소진은 Failed로 남는다. Processing 상태를 수동 retry하지 않는다.

## 12. 정상 상태

- due delivery가 짧게 Processing을 거쳐 terminal 상태로 이동
- 같은 delivery의 active owner 1개
- attempt 번호가 순서대로 증가
- Processing action disabled
- stale row가 다음 worker poll에서 회수
- 실제 provider call과 audit 경계가 일치

## 13. 오류 발견 시 기록 방법

개인정보를 복사하지 말고 상태, attempt 번호, outcome, 발생 시각 구간, lease 유효/만료 여부, 화면 route alias만 기록한다. 알림 제목·본문·수신자·내부 ID는 보고서에 넣지 않는다.

## 14. 하면 안 되는 작업

- Processing row를 DB에서 직접 변경
- claim token이나 worker 원문 공유
- 같은 알림을 확인 없이 반복 retry
- Candidate에서 실제 Teams/Mail/Channel 활성화
- Persistent UAT에 0028을 수동 적용
- 중복이 절대 없다고 안내

## 15. FAQ

### 데이터가 손상됐다는 뜻인가요?

아니다. 이 Task는 앞으로의 동시 처리와 audit을 보강한다. Persistent UAT는 변경하지 않았다.

### Processing이 오래 보이면 실패인가요?

Lease 만료 전에는 정상일 수 있다. 만료 후 다음 poll에도 남으면 운영자에게 상태와 시각만 전달한다.

### 왜 worker 이름이 보이지 않나요?

내부 인프라와 개인정보 노출을 줄이기 위해 마스킹한다. 운영 추적에는 attempt와 stable code를 사용한다.

### 언제 Persistent UAT에 적용되나요?

본 PR merge 후 별도 TASK-UAT-HANDOVER-003에서 migration과 runtime을 통제 적용한다.

## 16. 사용자 검수 체크리스트

- [x] https://localhost:5192 접속 가능
- [x] Pending/Processing/Sent/Failed 구분
- [x] Processing count/filter 정상
- [x] Processing row action disabled
- [x] attempt 번호와 outcome 이해 가능
- [x] stale lease 안내 이해 가능
- [x] 기존 상태 표시 회귀 없음
- [x] 정상 경쟁 provider 1회 증빙 이해
- [x] late result overwrite 차단 이해
- [x] at-least-once 제한과 exactly-once 미보장 이해
- [x] Persistent UAT 0028 미적용 확인
- [x] 실제 외부 발송 0 확인
- [x] Console 오류와 390px overflow 0 확인
- [x] SOP 실행 가능
- [x] 다음 단계 TASK-UAT-HANDOVER-003 확인
- [x] 신규 기능 개발 No-Go 유지 확인

사용자 검수 완료, PR #30 squash merge 승인, 미체크 항목 0이다. Candidate 확인과 별개로 Persistent UAT는 아직 변경되지 않았으며 다음 TASK-UAT-HANDOVER-003에서만 통제 적용한다.
