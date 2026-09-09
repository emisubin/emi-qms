# Backend AGENTS.md

이 파일은 `backend/` 아래 작업에 적용되며 Root [AGENTS.md](../AGENTS.md)를 보완한다.

## 구조와 의존 방향

- .NET solution은 `backend/Emi.Qms.sln`, API project는 `backend/src/Emi.Qms.Api`, tests는 `backend/tests/Emi.Qms.Api.Tests`를 기준으로 한다.
- endpoint/composition root, domain service, store/provider와 contract의 책임을 분리하고 기존 namespace와 directory convention을 따른다.
- 새로운 abstraction은 실제로 둘 이상의 소비자 또는 명확한 테스트 경계가 있을 때만 추가한다.
- unrelated refactor, solution/namespace rename과 backend stack 전환을 Task 범위에 섞지 않는다.

## API, 권한과 validation

- Backend가 업무 규칙, 권한과 mutation 허용 여부의 authoritative source다.
- Frontend의 숨김·비활성화는 보조 수단이며 authorization policy와 서버 validation을 대체하지 않는다.
- 입력 validation은 안정적인 HTTP status, error code와 사용자 행동이 가능한 한글 메시지로 반환한다.
- raw SQL, stack trace, credential, connection string과 내부 식별자를 API 응답에 노출하지 않는다.
- ReviewSafe에서는 startup mutation, mutation API, background worker와 actual provider 차단 정책을 유지한다.

## DB, transaction과 동시성

- 이미 main에 반영된 SQL migration은 수정하지 않는다. 신규 migration은 다음 번호의 additive migration으로 추가한다.
- migration catalog, live ledger와 schema compatibility를 함께 검증하며 latest version 비교만으로 준비 상태를 판정하지 않는다.
- 여러 write가 하나의 업무 동작이면 같은 connection/transaction 경계를 사용한다.
- check-then-write 경쟁이 가능한 흐름은 row lock, atomic update, unique constraint, idempotency 또는 fencing을 사용하고 동시성 테스트를 추가한다.
- 외부 provider transaction과 DB transaction을 exactly-once로 표현하지 않는다. 실제 보장 수준과 crash ambiguity를 문서화한다.
- Persistent UAT에서는 Task가 명시적으로 승인하지 않은 write probe를 실행하지 않는다.

## Configuration과 provider

- 실제 secret은 configuration key 이름만 문서화하고 값은 approved secret/env 위치에서 주입한다.
- malformed 안전 설정을 silent fallback으로 활성화하지 않는다.
- mutation worker와 provider는 effective enable 상태가 DI registration과 runtime status에 일치해야 한다.
- 테스트와 E2E에서는 fake/dry-run provider를 사용하고 실제 Teams/Mail/Channel 발송을 하지 않는다.

## Backend 검증

- 최소 build, 영향 test, 전체 regression과 migration 추가 검증 기준은 [Validation Matrix](../docs/development/validation-matrix.md)를 따른다.
- 새 authorization, transaction, concurrency, worker 또는 provider 경계에는 성공·차단·경쟁·실패/취소 경로 테스트를 포함한다.
- test fixture는 isolated DB와 synthetic data만 사용하고 Persistent UAT를 cleanup 대상으로 사용하지 않는다.
