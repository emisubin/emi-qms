# TASK-012A Change 004 구현 보고 — 모든 품질검사 Pending 판정 수명주기

## 구현 결과

1. 패널 품질 회차에 additive `decision_mode`를 추가했다. 신규 LQC·OQC는 `Checklist`, 신규 전진검수·FAT는 `Aggregate`로 저장한다.
2. `Aggregate` 검사는 항목별 응답 저장을 거부하고 패널 단일 적합·부적합, 판정 근거, 사진 증빙과 Pending 후속만 사용한다. 기존 finalized 전진검수·FAT는 읽기 전용 이력으로 보존한다.
3. `Checklist` 검사는 기존 필수 항목 입력과 부적합 항목 검증을 유지한다. IQC는 구매품 도착분별 별도 엔진을 그대로 사용한다.
4. 모든 검사에서 부적합 최종화가 Pending을 만들거나 기존 Pending을 재개하고, 적합 재검사 최종화가 검사 결과 `Passed`와 Pending `Closed`를 같은 transaction에서 기록한다.
5. 조치 완료가 자동 생성한 재검사 회차를 품질 담당자가 시작할 때 회차도 `Requested → InProgress`로 바꾸고 시작자·시각을 함께 기록한다. 이 누락 때문에 재검사 적합 확정이 DB constraint에서 500으로 실패하던 결함을 해소했다.
6. 처리되지 않은 API 예외는 사용자에게 기존 generic 500만 반환하면서 server log에 method·path와 stack을 남겨 같은 유형의 불변조건 실패를 개인정보 노출 없이 진단할 수 있게 했다.

## migration

- `0054_panel_quality_decision_modes.sql`
- 기존 null/신규 기본값은 `Checklist`이며, 진행 중인 전진검수·FAT 중 항목 응답이 없는 회차만 안전하게 `Aggregate`로 보정한다.
- 허용값은 `Checklist | Aggregate` check constraint로 제한한다.

## 검증

- 패널 품질 E2E: LQC Checklist·제조 확인·OQC 인계 `PASS`.
- Aggregate 재검사 E2E: 전진검수 부적합 → Pending 1건 → 조치 완료 → 재검사 시작 → 적합 → 같은 Pending 종결 `1/1 PASS`.
- IQC 연속성 E2E: 도착분 부적합 → Pending → 자동 재검사 → 적합 → Pending·workflow 종결 `1/1 PASS`.
- 실제 역할 18단계 E2E: IQC·LQC·OQC·전진검수·FAT와 최종 정산까지 `1/1 PASS`, 최종 open Pending `0`.

## Finding·경계

- `012A-AGGREGATE-DECISION`: `RESOLVED`.
- Open P0/P1/P2: `0/0/0`.
- 실제 양식 content·PDF layout 변경, Persistent migration과 실제 provider 호출은 범위 밖이다.
- 사용자 직접 검수는 대기한다. 대표 repo·`main`·push·PR·merge는 변경하지 않았고 main merge 승인 `0/3`이다.
