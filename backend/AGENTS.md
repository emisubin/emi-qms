# Backend 작업 계약

[Root 지침](../AGENTS.md)을 적용하고 이 영역의 변경에 필요한 항목만 확인한다.

## 구현 경계

- .NET solution은 `backend/Emi.Qms.sln`, API는 `backend/src/Emi.Qms.Api`, 테스트는 `backend/tests/Emi.Qms.Api.Tests`에 있다. endpoint → service → store/provider 책임과 기존 convention을 따른다.
- 업무 규칙·권한·validation의 최종 판단은 서버에서 한다. UI 숨김이나 클라이언트 역할 값으로 서버 허용 범위를 넓히지 않는다.
- 사용자 오류는 안정적인 HTTP status/code와 행동 가능한 메시지로 반환한다. secret·SQL·stack trace·내부 진단을 API 응답에 노출하지 않는다.
- 범위 밖 refactor나 사용처 없는 추상화를 추가하지 않는다.

## 데이터와 권한

- Directory·청주·오산의 identity, membership, local profile/role 경계를 지킨다. 잘못된 사업부나 준비되지 않은 DB를 다른 사업부 DB로 fallback하지 않는다.
- 승인 readiness와 실제 접근 허용을 일치시키고, 부서 자동 역할·명시 역할·총괄 역할의 출처를 보존한다. 권한 변경은 서버의 조회·입력 양쪽에서 검증한다.
- main에 반영된 migration은 수정하지 않는다. 새 additive migration과 실제 catalog·ledger·schema compatibility를 함께 판단한다. 서로 다른 DB/identity의 version 숫자가 같아야 한다고 가정하지 않는다.
- 한 업무 동작의 여러 write는 필요한 transaction 경계로 묶는다. 경쟁 가능한 check/write에는 lock·atomic update·constraint·idempotency 등 실제 보장과 반례 검증을 둔다.
- 외부 provider와 DB 사이의 보장 수준·재전송·응답 불명확 상태를 구분한다. 분산 transaction을 근거 없이 exactly-once라고 설명하지 않는다.

## 실행과 검증

- ReviewSafe의 startup/API/worker/provider mutation 차단과 malformed 안전 설정의 fail-closed를 유지한다. DI 등록·설정·실제 enable 상태가 일치해야 한다.
- secret은 승인된 env/secret 저장소에서 주입하고 값 대신 설정 이름만 문서화한다.
- [검증표](../docs/development/validation-matrix.md)에서 영향받는 성공·거부·실패·경쟁 경로를 선택한다. 실제 내부 권한/분기를 검증하고 외부 provider는 fake/dry-run으로 대체한다.
- DB 검증은 isolated synthetic 환경에서 한다. Persistent UAT의 write probe나 운영 데이터 정정은 일반 테스트에 포함하지 않는다.
