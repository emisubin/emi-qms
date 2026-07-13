# TASK-E2E-RELIABILITY-001 — 구매정보 편집 행 준비성 안정화

## 1. 상태

- Task 유형: `BUGFIX`
- Fable 5: 미사용
- instructionChainRead: true
- 구현 승인: 완료 — 사용자가 권장 최소안 일괄 승인
- 자동 검증: 완료
- 사용자 검수: 완료
- Commit·Push·PR·Merge: 승인

## 2. 증상

Full-Stack E2E의 구매정보 직접 입력 시나리오가 CI에서 새 행의 input을 15초 동안 찾지 못해 두 번 연속 실패했다. 같은 source의 후속 main CI와 로컬 반복에서는 통과해 timing 의존 flake로 분류했다.

## 3. 확인된 원인

`ProcurementEditPage`는 React 개발 모드의 effect 재실행으로 동일 load가 겹칠 수 있지만 최신 요청만 반영하는 guard가 없었다. 먼저 끝난 응답이 편집 표를 표시한 직후 사용자가 행을 추가하면, 늦은 응답이 `rows`를 다시 초기화해 추가 행을 제거할 수 있었다.

기존 E2E는 행 증가를 먼저 관찰한 뒤 input을 별도 polling했고, 첫 대기 실패 시 `행 추가`를 다시 눌렀다. 이 방식은 transient row 소실을 막지 못하고 중복 행까지 만들 수 있었다.

## 4. 승인된 최소 수정

- 구매정보 편집 load에 기존 Repository 패턴과 같은 request-id stale-response guard를 적용한다.
- 최신 load가 완료되기 전에는 편집 표가 준비된 것으로 표시하지 않는다.
- E2E는 `행 추가`를 한 번만 실행하고 정확히 한 행 증가, 새 행 표시와 input 8개 준비를 하나의 결정적 경계로 검증한다.
- 지연된 두 load를 제어하는 StrictMode frontend regression test를 추가한다.

## 5. 포함 범위

- 구매정보 편집 load의 stale response 차단
- 구매정보 직접 입력 Full-Stack E2E의 결정적 row readiness
- Frontend StrictMode 회귀 test
- 5종 종료 산출물과 Roadmap

## 6. 제외 범위

- Backend·API·DB·migration 변경
- 구매정보 업무 정책 또는 화면 문구 변경
- Runtime·Persistent UAT 변경
- dependency·lockfile·script 변경
- Commit·Push·PR·Merge

## 7. 완료 조건

- 수정 전 regression test가 stale 첫 응답으로 편집 표가 조기 표시되는 문제를 재현한다.
- 수정 후 stale 응답은 무시되고 최신 응답 뒤에만 편집 입력이 열린다.
- 대상 Full-Stack E2E 반복과 전체 suite가 통과한다.
- Frontend lint, typecheck, unit와 build가 통과한다.
- Persistent UAT와 실행 중 runtime은 변경되지 않는다.

## 8. 사용자 검수 체크리스트

- [x] 구매정보 수정 화면에서 `행 추가` 1회가 빈 행 1개만 추가함
- [x] 통상납기와 발주일을 입력하고 저장할 수 있음
- [x] 기존 구매정보가 늦은 load 때문에 사라지거나 되돌아가지 않음
- [x] 화면 문구·권한·모바일 카드 동작에 회귀가 없음
- [x] Backend·DB·migration 변경이 없음을 확인함
