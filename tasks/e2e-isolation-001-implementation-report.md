# TASK-E2E-ISOLATION-001 Implementation Report

## 1. 목적

Full-Stack E2E를 persistent UAT PostgreSQL과 container, network, storage 수준에서 분리하고 database 이름 오설정이 UAT DB 삭제로 이어지지 않도록 fail-closed safety guard를 확립한다.

## 2. 발견 배경

기존 E2E는 database 이름만 분리했다. host `psql`이 없으면 `infrastructure/docker-compose.yml`의 `emi-qms-postgres` service로 fallback하여 persistent UAT named volume을 공유했다. `E2E_DATABASE_NAME`도 안전한 prefix 검증 없이 `DROP DATABASE`에 사용됐다.

## 3. 위험 시나리오

1. 로컬 개발자가 host `psql` 없이 Full-Stack E2E를 실행한다.
2. E2E가 persistent UAT PostgreSQL container로 fallback한다.
3. 환경 변수에 UAT 또는 운영성 database 이름이 잘못 지정된다.
4. cleanup의 `DROP DATABASE ... WITH (FORCE)`가 UAT 또는 운영성 database를 대상으로 실행된다.

기본값만 사용할 때도 E2E write와 cleanup이 UAT container/volume에 의존한다는 구조적 P2가 있었다.

## 4. 기존 구조

- Local: host `psql` 우선, 없으면 persistent Compose `postgres` service
- CI: job 전용 PostgreSQL을 사용하지만 E2E database 이름은 strict prefix 규칙과 불일치
- Storage: E2E database가 persistent UAT named volume 안에 저장됨
- Cleanup: database만 drop하며 server/container/network/storage ownership을 확인하지 않음
- Full-Stack spec의 직접 SQL 조회도 같은 persistent Compose fallback을 사용함

## 5. 최종 격리 구조

- 각 실행마다 안전한 timestamp/process/random run ID 생성
- Compose project: `emi-qms-e2e-<run-id>`
- Database: `emi_qms_e2e_<run-id>`
- Service: `e2e-postgres`
- Network: Compose project 전용 default network
- Host binding: `127.0.0.1` dynamic port
- Storage: PostgreSQL 18 data root `/var/lib/postgresql` tmpfs
- Backend/frontend ports: 사용 가능한 동적 port, UAT port 5081/5174 거부

## 6. DB-name guard

`scripts/lib/e2e-safety.sh`는 database 이름을 `^emi_qms_e2e_[a-z0-9_]+$`로 제한한다. 빈 값, UAT, development, production, system database 이름은 exit 64로 종료한다. `scripts/e2e-db.sh`는 이 guard를 Docker/SQL command보다 먼저 실행한다.

SQL identifier는 허용 문자 집합을 통과한 뒤 double quote로 감싸며 `ON_ERROR_STOP=1`을 유지한다.

검증 결과:

- Valid: 2/2 통과
- Invalid: 9/9가 Docker가 없는 PATH에서도 exit 64
- Valid probe는 이후 Docker 단계까지 진행해 guard 순서를 확인
- command-substitution 형태 run ID: 실행되지 않고 exit 64

## 7. Container/network/storage isolation

검증 중 UAT와 E2E container ID 및 network ID가 서로 달랐다. E2E service에는 Docker volume mount가 0건이며 tmpfs만 적용됐다. UAT named volume `infrastructure_emi-qms-postgres-data`는 E2E에 mount되지 않았다.

PostgreSQL 18 image는 `/var/lib/postgresql` VOLUME을 선언한다. 처음 하위 `/var/lib/postgresql/data`만 tmpfs로 지정했을 때 익명 parent volume이 생기는 것을 확인했다. 이를 폐기하고 parent data root 전체를 tmpfs로 덮어 anonymous volume 생성을 차단했다.

동시에 두 E2E Compose project를 시작했을 때 container와 network가 충돌하지 않았고 한 project cleanup이 다른 project에 영향을 주지 않았다.

## 8. Persistent UAT 보호

E2E 전후 확인:

- UAT container ID: 동일
- UAT Compose project/network/named volume: 동일
- UAT health: healthy
- UAT restart count: 0 유지
- Latest migration: `0027_notification_access_scope_and_manual_work_items` 유지
- Projects 22, work items 37, deliveries 92와 delivery status count 유지

검증 중 UAT notification과 recipient가 각각 7건 증가했다. 모두 같은 시각의 `Automatic`/`Reference`/`RecipientOnly` row이며 delivery 증가는 0이었다. 실행 중인 UAT worker의 자연 변경으로 분리했고 E2E project가 UAT DB를 참조한 증거는 없었다.

## 9. Local/CI 실행 차이

Local과 CI 모두 `scripts/e2e-full-stack.sh`가 전용 E2E Compose PostgreSQL을 생성한다. CI의 database 이름은 `emi_qms_e2e_ci`로 strict prefix와 일치시켰다. CI job에 기존 bootstrap PostgreSQL이 남아 있지만 Full-Stack E2E script는 dynamic endpoint로 환경을 덮어써 해당 service를 사용하지 않는다. Docker-in-Docker는 추가하지 않았다.

## 10. Cleanup

- 정상, 실패, SIGINT와 SIGTERM에 EXIT cleanup 적용
- backend/frontend port close 확인
- E2E database drop/assert-dropped
- 현재 Compose project에만 `down --volumes --remove-orphans`
- 연결 중인 database도 `WITH (FORCE)` cleanup 확인
- attached ephemeral volume, project container/network/volume 잔여 검사

강제 connection cleanup과 SIGTERM exit 143 cleanup을 검증했다. 최종 E2E container/network 잔여는 0건이다.

## 11. 테스트 결과

| 검사 | 결과 |
| --- | --- |
| `git diff --check` | 통과 |
| actionlint | 통과 |
| Bash syntax / shellcheck | 통과 |
| Compose config | 통과 |
| DB-name/run ID guard | 통과 |
| Physical isolation / concurrent projects | 통과 |
| Force/SIGTERM cleanup | 통과 |
| Migration tests | 16/16 통과 |
| Backend 전체 tests | 295/295 통과 |
| Frontend unit tests | 57/57 통과 |
| Mock UI smoke | 1/1 통과 |
| Full-Stack E2E | 16/16 통과 |

기존 warning은 frontend Fast Refresh 1건, build chunk size와 Playwright color 환경 warning이다. 신규 warning 또는 failure는 없다.

## 12. 보안

- E2E script는 `.env.notify-local`을 읽지 않는다.
- 고정 test-only placeholder credential만 전용 container에 사용한다.
- 실제 credential, token, Webhook URL, certificate를 log 또는 문서에 기록하지 않았다.
- Testing backend는 Dispatch, Escalation, TeamsChannel, TeamsActivity와 Mail을 disabled로 강제한다.
- Mail provider와 모든 channel dry-run 설정을 방어적으로 지정한다.
- Full-Stack E2E는 persistent UAT DB에 notification delivery를 생성하지 않았다.

## 13. 제한사항

- GitHub Actions 실제 결과는 Draft PR 생성 후 확정한다.
- 사용자 검수는 대기 상태다.
- CI job의 기존 bootstrap PostgreSQL 제거는 안전성과 무관한 P3 최적화로 남긴다.
- 동적 application port 선택에는 bind 전 짧은 race window가 있으나 고유 Compose/DB 자원과 UAT port denylist가 데이터 격리를 보장한다.

## 14. 후속 Task

- `TASK-FRONTEND-SEC-001`: Vite/esbuild dependency security
- `TASK-UAT-002`: Review-safe UAT
- `TASK-NOTIFY-REL-001`: delivery claim/lease
- `TASK-NOTIFY-ESC-001`: escalation starvation
- `TASK-AUTH-HARDEN-001`: last administrator concurrency
- P3 backlog: CI Full-Stack job의 사용하지 않는 bootstrap PostgreSQL 제거

## 15. 해결한 업무 문제

테스트가 검수용 persistent data를 공유한다는 운영 위험을 제거했다. 개발자는 host 도구 설치 여부와 무관하게 같은 명령으로 안전한 Full-Stack E2E를 실행할 수 있고, 잘못된 DB 이름은 data command 전에 차단된다.

## 16. 기술적 결정과 대안

- 선택: 실행별 Compose project + tmpfs PostgreSQL
- 대안 1: UAT server 안의 별도 database 유지 — physical isolation과 오설정 blast radius를 해결하지 못해 폐기
- 대안 2: 독립 named volume — 격리는 가능하지만 cleanup과 잔여 volume 관리가 필요해 tmpfs보다 불리
- 대안 3: host `psql` 필수화 — 환경 의존성이 커지고 Docker-only 개발 환경을 지원하지 못해 폐기
- 결정: local과 CI 모두 동일한 전용 Compose 실행 경로를 사용하고, CI의 기존 service는 연결 대상에서 제외

## 17. 시행착오 및 폐기한 접근

PostgreSQL 18에서 `/var/lib/postgresql/data`만 tmpfs로 지정하면 image의 parent VOLUME 선언 때문에 익명 volume이 생성됐다. 실제 mount를 검사해 이를 발견했고 `/var/lib/postgresql` 전체 tmpfs로 변경했다. Bash exported function을 Docker spy로 사용한 초기 검사도 shell function import 출력 때문에 모호해 폐기하고, Docker가 없는 PATH에서 exit code와 진행 순서를 검증했다.

## 18. 사용자 검수 결과와 남은 항목

- Checklist 작성됨
- 자동 검증 완료
- 사용자 검수 대기
- 사용자 검수 완료/실패: 아직 판정하지 않음
- Roadmap update: Pending — PR #21이 canonical Roadmap/governance 영역을 소유함

사용자는 SOP 명령을 직접 실행하고 container/network/storage 분리, guard 오류 문구와 cleanup 결과를 확인해야 한다.

- [ ] Full-Stack E2E가 persistent UAT PostgreSQL container를 사용하지 않음
- [ ] E2E container/network가 UAT와 다름
- [ ] E2E storage가 UAT named volume을 사용하지 않음
- [ ] UAT database 이름이 SQL 전에 거부됨
- [ ] `emi_qms_e2e_*` database 이름만 허용됨
- [ ] Full-Stack E2E 16개 이상이 통과함
- [ ] 종료 후 E2E container/network/storage가 정리됨
- [ ] E2E 전후 UAT health, schema와 업무 data가 유지됨
- [ ] 실제 외부 notification provider 호출이 없음
- [ ] host `psql` 없이 실행 가능함
- [ ] SOP와 User manual을 사용자가 이해하고 실행할 수 있음
