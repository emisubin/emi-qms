# TASK-E2E-RELIABILITY-001 SOP

## 1. 목적

구매정보 편집 row readiness flake를 재현·진단하고 stale async response 회귀를 안전하게 검증한다.

## 2. 진단 순서

1. Root와 Frontend·Scripts instruction chain을 읽는다.
2. Full-Stack E2E가 전용 PostgreSQL container/network/tmpfs를 사용하는지 확인한다.
3. 실패가 새 행 count 전인지, row input 준비 전인지 구분한다.
4. 동일 source의 CI 성공·실패와 StrictMode load 중첩을 대조한다.
5. timeout을 늘리기 전에 stale response가 edit state를 덮어쓰는지 deterministic test로 검증한다.

## 3. 회귀 검증

1. 지연된 편집 load 두 개를 생성한다.
2. 이전 응답만 완료했을 때 편집 table이 노출되지 않는지 확인한다.
3. 최신 응답 완료 뒤 table이 노출되는지 확인한다.
4. `행 추가` 1회 뒤 row가 정확히 1개 증가하는지 확인한다.
5. 새 row의 input 8개가 visible·enabled 상태인지 확인한다.
6. 대상 E2E를 반복한 뒤 전체 Full-Stack E2E를 실행한다.

## 4. 금지 사항

- timeout만 확대해 race를 숨기지 않는다.
- 실패 시 `행 추가`를 다시 눌러 성공 처리하지 않는다.
- fixed sleep을 readiness 계약으로 사용하지 않는다.
- Persistent UAT DB나 Development runtime을 E2E에 사용하지 않는다.
- 실제 provider credential을 E2E에 주입하지 않는다.

## 5. 실패 대응

- stale 응답 뒤에도 table이 표시되면 request-id 발급·비교 위치를 확인한다.
- row count가 정확히 1 증가하지 않으면 button 중복 action과 load 재초기화를 확인한다.
- input count가 8이 아니면 화면 구조 변경인지 unintended incomplete render인지 구분한다.
- isolated DB cleanup 실패 시 Task-owned 자원만 정리하고 Persistent UAT는 건드리지 않는다.

## 6. Rollback

Frontend source와 관련 unit/E2E 변경을 같은 commit 단위로 revert한다. Migration, data restore와 runtime restart는 필요하지 않다.
