# TASK-DB-MIGRATION-001 User Manual — Migration 이력 상태 확인

## 1. Migration 이력이란 무엇인가

시스템이 DB 구조를 어떤 순서로 준비했는지 남기는 적용 기록이다. 코드 repository에는 적용할 파일 목록이 있고 DB에는 실제 적용된 이름 목록이 있다.

## 2. 파일 수와 DB 기록 수가 다른 이유

현재 repository에는 27개가 있고 UAT DB에는 28개가 있다. Teams Activity 기능을 merge하기 전 시험하던 동일 SQL이 `0020` 이름으로 먼저 적용됐고, 최종 repository에서는 ADMIN migration과 번호가 겹쳐 `0023`으로 정리됐기 때문이다.

## 3. 승인된 과거 marker란 무엇인가

과거에 적용한 사실을 보존하는 추가 기록이다. 다음을 확인한 한 건만 승인한다.

- 과거 SQL과 현재 0023 SQL이 동일
- 현재 canonical 27개가 모두 적용됨
- TeamsActivity DB 구조가 현재 코드와 같음
- 다른 알 수 없는 기록은 없음

승인은 이름이 비슷하다는 추측이 아니라 코드와 자동 테스트로 제한한다.

## 4. 현재 데이터가 손상됐다는 뜻인가

아니다. 현재 28번째 기록은 같은 schema 변경이 과거 이름으로도 기록된 감사 이력이다. 업무 data 손상이나 migration 누락을 뜻하지 않는다. 이 Task는 그 사실을 자동으로 구분하고 설명하기 위한 작업이다.

## 5. 상태 의미

| 표시 | 의미 |
| --- | --- |
| Exact | repository와 DB 이력이 정확히 같음 |
| Compatible | 승인된 과거 기록이 추가로 있지만 현재 구조와 호환됨 |
| Mismatch | 누락·알 수 없는 추가·구조 불일치가 있어 검수 준비 안 됨 |
| Unavailable | DB 또는 migration 파일을 확인할 수 없음 |

Mismatch와 Unavailable에서는 Ready가 실패하며 검수를 계속하지 않는다.

## 6. 접속 주소

- Candidate Review-safe: https://localhost:5191
- Current Review-safe: https://localhost:5190
- Development UAT: https://localhost:5174
- Security Preview: https://localhost:5185

이번 Task 사용자 검수는 5191을 사용한다.

## 7. 화면에서 상태 확인 방법

1. https://localhost:5191 에 접속한다.
2. 상단의 검수 전용 읽기 모드 banner를 확인한다.
3. `Migration 이력은 현재 repository와 호환됩니다` 문구를 확인한다.
4. Canonical 27, Live 28, 승인된 과거 marker 1을 확인한다.
5. 주요 조회 화면이 열리는지 확인한다.

전체 version 목록은 일반 화면에 표시하지 않는다.

## 8. Ready 실패 의미

Ready 503은 시스템이 중단됐다는 뜻만은 아니다. 검수에 필요한 이력 또는 DB 구조가 확인되지 않아 안전하게 차단했다는 뜻이다. 원인 코드를 운영자에게 전달하고 marker를 직접 수정하지 않는다.

## 9. 하면 안 되는 작업

- DB 기록을 삭제하거나 이름 변경
- migration 파일 번호 변경
- UAT DB reset
- PostgreSQL/volume 삭제
- 검수 화면에서 저장·발송 우회
- `.env`나 인증서 내용 공유

## 10. 관리자에게 전달할 정보

다음만 전달한다.

- 접속 URL과 확인 시각
- Exact/Compatible/Mismatch/Unavailable
- Canonical/Live/Legacy 개수
- stable reason code
- Ready HTTP status

Password, connection string, 사용자 개인정보, token은 전달하지 않는다.

## 11. 데이터가 보존되는 방식

- legacy marker를 삭제하지 않음
- Persistent UAT에 INSERT/UPDATE/DELETE/DDL을 실행하지 않음
- candidate DB session read-only
- 저장·발송 API 423 차단
- worker와 외부 provider 미실행
- isolated test DB에서만 mismatch fixture 생성

## 12. FAQ

### 왜 28개를 27개로 줄이지 않나요?

과거 적용 이력도 감사 정보이므로 삭제하지 않는다. 현재 코드와 동일한 변경임을 검증하고 호환으로 처리하는 편이 안전하다.

### 29개가 되면 자동 승인되나요?

아니다. 현재 승인 marker 외 추가 기록은 Mismatch와 Ready 503이다.

### 이름이 비슷하면 승인되나요?

아니다. exact 이름, successor, SQL 역사, schema probe가 모두 필요하다.

### 이 화면에서 데이터를 저장할 수 있나요?

아니다. Review-safe는 조회 전용이다. 저장이 필요하면 승인된 Development UAT 절차를 사용한다.

### 언제 5190으로 바뀌나요?

이 Task merge 후 controlled Review-safe handover를 별도로 수행한다.

## 13. 사용자 검수 체크리스트

- [ ] 5191 접속 정상
- [ ] Review-safe banner 확인
- [ ] Compatible 표시 확인
- [ ] 27/28/1 의미 이해
- [ ] 데이터 손상 의미가 아님을 이해
- [ ] marker 미삭제 확인
- [ ] mismatch는 503임을 이해
- [ ] DB read-only와 저장 차단 확인
- [ ] SOP 이해
- [ ] UAT-VERIFY 재개 순서 이해
- [ ] 전체 신규 기능 No-Go 확인
