# Validation Matrix

## 1. 목적

변경 유형별 최소 검증, 영향 영역 회귀와 게시 전 전체 파이프라인을 한 곳에서 관리한다. 개별 Task는 이 문서를 복사하지 않고 실제 적용 항목, 결과와 미실행 이유만 Implementation report에 기록한다.

## 2. 공통 사전 검증

모든 변경에서 다음을 확인한다.

- 적용되는 `AGENTS.md`, Product Roadmap과 Task 종료 정책 확인
- branch/HEAD/worktree, 기존 동일 목적 작업과 dirty WIP 확인
- 포함·제외 범위와 changed-file allowlist 확정
- `git diff --check`
- 개인정보·secret·generated artifact 검사
- 문서 link/anchor와 사용자 검수 상태 검사

## 3. 변경 유형별 최소 검증

| 변경 유형 | 최소 검증 | 추가 조건 |
| --- | --- | --- |
| 문서·지침만 | diff check, Markdown local link/anchor, heading duplicate, PII/secret, Rules 변경 시 execpolicy | 코드·migration·dependency diff 0 확인 |
| Backend | `dotnet build backend/Emi.Qms.sln --configuration Release`, 관련 filtered tests | public contract, DI, authorization, worker/provider 영향 시 해당 matrix test |
| Frontend | `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build` | 사용자-facing 변경은 browser smoke와 390px 검증 |
| Migration | migration catalog test, existing DB apply, fresh DB apply, rollback/forward-fix 검증 | 기존 migration diff 0, duplicate/missing prefix 0 |
| Script·workflow | shell syntax, actionlint, 실패 경로와 cleanup test | process ownership, strict port, protocol과 DB guard 검증 |
| Authorization | 일반/업무 역할/admin의 allow·deny matrix | list/detail/mutation scope 일치, UI 숨김 외 서버 403 확인 |
| Concurrency | 경쟁 요청, late completion, cancellation, stale recovery | atomicity, lock/CAS/fencing과 audit consistency 확인 |
| Runtime handover | source tree 비교, ownership, rollback, live/ready, before/after snapshot | 기존 runtime과 Persistent UAT 보존, protocol/port 고정 |

Frontend 명령은 Repository root에서 다음 형태를 기준으로 한다.

```bash
corepack pnpm --dir frontend run lint
corepack pnpm --dir frontend run typecheck
corepack pnpm --dir frontend test
corepack pnpm --dir frontend run build
```

## 4. 영향 영역 회귀

변경한 파일 수가 아니라 실제 영향 경계를 기준으로 선택한다.

- API contract 변경: backend endpoint/serialization tests + frontend type/unit tests
- DB store 변경: service/store tests + isolated PostgreSQL integration
- worker/provider 변경: registration, disabled, retry/cancellation, no-call tests
- runtime mode/health 변경: Development/ReviewSafe matrix + ready failure fixtures
- 권한 변경: role별 list/detail/mutation matrix
- 사용자 UX 변경: desktop/390px, loading/empty/error/success, console/request failure
- migration 변경: migration 전체 suite + fresh/existing DB + Full-Stack E2E

Task가 영향 테스트를 생략하면 구체적인 비영향 근거를 Implementation report에 기록한다.

## 5. 게시 전 전체 검증

runtime, dependency, migration, authorization, worker/provider 또는 공통 contract를 변경하는 PR은 원칙적으로 다음을 통과한다.

1. Backend Release build와 전체 tests
2. Frontend lint, typecheck, unit과 build
3. mock UI smoke
4. isolated Full-Stack E2E
5. migration·seed isolation 검증(영향이 있는 경우)
6. Persistent UAT read-only persistence snapshot(영향 가능성이 있는 경우)
7. secret/PII와 changed-file allowlist 검사
8. CI 표준 job

문서 전용 변경은 코드 전체 재실행을 `N/A`로 둘 수 있지만, runtime/dependency/migration diff가 0이라는 근거를 남긴다.

## 6. UI와 browser 증빙

- 정상 route status, expected structure, blank/target-not-found, disabled mutation, console/request error와 overflow를 확인한다.
- 실제 UAT에서는 [Privacy-safe Evidence](privacy-safe-evidence.md)의 fixed projection만 사용한다.
- screenshot과 raw DOM은 기본 증빙이 아니다.
- synthetic isolated environment에서 생성된 실패 artifact도 tracked/staged에 포함하지 않는다.

## 7. 사용자 검수와 CI

자동 검증은 사용자 직접 검수를 대체하지 않는다. 사용자 검수 대기 중 게시가 필요하면 Draft PR로 유지한다. 새 commit이 생기면 최신 head 기준 CI를 다시 확인하고, 실패를 단순 재실행으로 숨기지 않는다.

## 8. 결과 기록

Implementation report에는 다음을 표로 기록한다.

| 검증 | 적용 여부 | 결과 | 근거/미실행 이유 |
| --- | --- | --- | --- |
| 최소 검증 |  |  |  |
| 영향 회귀 |  |  |  |
| 전체 pipeline |  |  |  |
| 사용자 검수 |  |  |  |
