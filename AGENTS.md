# AGENTS.md

## 프로젝트와 지침 범위

이 Repository는 EMI 프로젝트 통합관리시스템을 개발한다. Root 지침은 Repository 전체에 적용하며, `backend/`, `frontend/`, `scripts/` 아래에서는 각 하위 `AGENTS.md`가 영역별 규칙을 추가한다.

## Source of truth

작업을 시작하기 전에 다음 순서로 실제 Repository 상태를 확인한다.

1. 현재 경로에 적용되는 `AGENTS.md` 계층
2. [Product Roadmap](docs/00-product-roadmap.md)의 제품 방향, Task 상태와 선행 의존성
3. [Task 종료 및 산출물 정책](docs/12-task-completion-policy.md)의 Finding·검수·게시 gate
4. 실제 코드, migration, 설정과 tests
5. 현재 branch, worktree, runtime과 DB 상태

대화 기록이나 기억을 canonical source로 사용하지 않는다. 문서끼리 또는 문서와 구현 사이에 의미 있는 충돌이 있으면 한쪽을 임의 선택하거나 수정하지 말고 위치, 영향과 선택지를 보고한 뒤 중단한다.

## 작업 격리와 범위

- `main`에서 직접 개발하거나 push하지 않는다.
- Task별 branch와 전용 worktree를 사용한다.
- 기능 개발은 `feat/<task-id>-<short-name>`, 버그 수정은 `fix/<task-id>-<short-name>`, 디자인 실험은 `experiment/<purpose>` 형식을 사용한다.
- 기존 dirty worktree, stash, branch, runtime과 사용자가 만든 WIP를 임의 수정·정리·재시작하지 않는다.
- 시작 전 동일 목적 branch/worktree/PR과 현재 diff를 확인한다.
- 승인된 포함 범위만 변경하고 범위 밖 개선은 Finding 또는 후속 Task로 분리한다.
- Commit, push, PR, merge와 branch/worktree 정리는 사용자의 명시적 요청 범위에서만 수행한다.

## 데이터와 migration 안전

- Persistent UAT DB를 drop, truncate, reset하지 않고 persistent volume을 삭제하지 않는다.
- 이미 `main`에 반영된 migration은 수정하거나 번호를 재사용하지 않는다.
- 신규 migration은 feature branch에서 additive·forward-fix 원칙으로 작성하고 기존 DB와 fresh DB를 모두 검증한다.
- Persistent UAT write, migration 적용, 실제 provider 발송과 runtime 교체는 Task 범위와 사용자 승인이 명확할 때만 수행한다.
- E2E는 Persistent UAT와 분리된 전용 DB·container·storage를 사용한다.

## Finding과 완료 판정

- P0/P1은 미해결 상태에서 완료·게시·merge할 수 없다.
- P2는 먼저 해결한다. Risk acceptance는 canonical 종료 정책의 필수 기록과 사용자 승인이 모두 있을 때만 허용한다.
- P3는 후속 Task 또는 backlog에 연결한다.
- 확인하지 않았거나 실행하지 않은 결과를 성공으로 기록하지 않는다.
- 자동 검증 완료와 사용자 검수 완료를 별도 상태로 관리한다.
- Task 종료 시 5종 산출물의 위치와 상태를 [Task 종료 및 산출물 정책](docs/12-task-completion-policy.md)에 따라 추적한다.
- Implementation report는 실제 변경, 결정, 검증, 미실행 항목과 rollback을 기록하는 기술 원장이다.

## 개인정보와 secret

- 실제 사용자·회사 계정·고객·프로젝트·업무·알림 원문, tenant/client/object ID, credential과 secret을 tracked 파일이나 보고에 기록하지 않는다.
- raw DOM, API/DB response body, console 원문과 Git/GitHub 개인 metadata를 검증 증빙으로 출력하지 않는다.
- 검증 결과는 가능한 경우 boolean, integer, fixed enum, aggregate와 익명 역할명으로 기록한다.
- 상세 규칙과 output guard는 [Privacy-safe Evidence](docs/development/privacy-safe-evidence.md)를 따른다.

## 검증과 게시

- 변경 유형별 최소·영향·전체 테스트는 [Validation Matrix](docs/development/validation-matrix.md)를 따른다.
- Task allowlist의 개별 경로만 stage한다. `git add .`와 `git add -A`를 사용하지 않는다.
- stage 후 cached file 목록, 삭제, migration, dependency, env/certificate, generated artifact와 secret/PII 포함 여부를 재검증한다.
- 사용자 검수 대기 상태에서 PR이 필요하면 Draft로 유지한다.
- CI 실패, Finding gate 위반, 범위 밖 변경 또는 secret/PII가 있으면 게시·merge를 중단한다.

## 영역별 지침

- Backend: [backend/AGENTS.md](backend/AGENTS.md)
- Frontend: [frontend/AGENTS.md](frontend/AGENTS.md)
- Scripts: [scripts/AGENTS.md](scripts/AGENTS.md)
