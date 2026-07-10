# TASK-E2E-ISOLATION-001 Full-Stack E2E PostgreSQL 물리 격리

## 1. 문제

기존 Full-Stack E2E는 database 이름만 `emi_qms_e2e`로 분리한다. host `psql`이 없으면 persistent UAT와 같은 `emi-qms-postgres` Docker Compose service와 `infrastructure_emi-qms-postgres-data` named volume을 사용한다. 또한 `E2E_DATABASE_NAME`이 UAT 또는 운영성 database 이름이어도 `DROP DATABASE` 전에 거부하지 않는다.

## 2. 재현 조건

- host에 `psql`이 없는 로컬 환경에서 `scripts/e2e-full-stack.sh`를 실행한다.
- `scripts/e2e-db.sh`가 기존 `infrastructure/docker-compose.yml`의 `postgres` service로 fallback한다.
- `E2E_DATABASE_NAME`을 `emi_qms_uat_*`, `emi_qms_dev`, `emi_qms_prod` 등으로 잘못 지정하면 이름 자체를 거부하는 guard 없이 drop 경로에 진입한다.

## 3. 영향

- Full-Stack E2E가 persistent UAT container/network/storage와 물리적으로 분리되지 않는다.
- 환경 변수 오설정 시 UAT 또는 운영성 database 삭제로 이어질 수 있다.
- E2E cleanup의 영향 범위를 container와 storage 수준에서 증명하기 어렵다.

## 4. 목표

- 모든 local/CI Full-Stack E2E에 고유 Compose project의 전용 PostgreSQL을 사용한다.
- PostgreSQL data는 tmpfs에 저장하고 host port는 loopback 동적 port를 사용한다.
- database 이름을 `emi_qms_e2e_*`로 제한하고 SQL/Docker 실행 전에 fail closed한다.
- cleanup은 현재 E2E Compose project의 container/network/tmpfs만 제거한다.
- persistent UAT container, network, volume과 data가 E2E 전후 동일함을 확인한다.
- Testing environment에서 실제 TeamsChannel, TeamsActivity, Mail provider를 비활성화한다.

## 5. 포함 범위

- E2E database name, run ID와 Compose project safety helper
- 전용 E2E PostgreSQL Compose configuration
- Full-Stack E2E startup/health/connection/cleanup
- database reset/drop/assert-dropped safety guard
- cleanup 강제 종료 회귀 검사
- E2E backend external notification provider 강제 비활성화
- CI Full-Stack E2E database 이름 정합성
- persistent UAT before/after read-only snapshot과 물리 격리 검증

## 6. 제외 범위

- persistent UAT Compose service 또는 named volume 변경
- UAT DB drop/truncate/reset과 container 재시작
- application migration 추가 또는 수정
- runtime notification claim/lease와 retry 구현
- 실제 Teams, Graph, SMTP, Webhook 호출
- PR #21에서 확립한 canonical Task 종료 정책 재정의. 본 Task는 정책을 따르고 Roadmap의 구현 상태만 갱신한다.

## 7. 데이터 안전 불변식

1. E2E database 이름은 `emi_qms_e2e_` prefix와 영문 소문자·숫자·underscore만 허용한다.
2. database name guard는 SQL과 Docker 명령보다 먼저 실행한다.
3. E2E Compose project는 `emi-qms-e2e-` prefix와 안전한 run ID를 사용한다.
4. E2E PostgreSQL container ID, network ID와 storage는 UAT와 달라야 한다.
5. E2E PostgreSQL은 PostgreSQL 18 image의 data root인 `/var/lib/postgresql` tmpfs만 사용하고 Docker volume을 mount하지 않는다.
6. cleanup은 명시된 E2E Compose project와 file에만 실행한다.
7. 실패와 SIGINT/SIGTERM에도 E2E 전용 자원만 정리한다.
8. E2E backend는 Testing environment와 external provider disabled/dry-run 설정을 강제한다.

## 8. 예상 수정 파일

- `scripts/lib/e2e-safety.sh`
- `scripts/e2e-db.sh`
- `scripts/e2e-full-stack.sh`
- `scripts/e2e-cleanup-check.sh`
- `scripts/e2e-backend-server.sh`
- `infrastructure/docker-compose.e2e.yml`
- `frontend/e2e/full-stack/project-registration.full-stack.spec.ts`
- `.github/workflows/ci.yml`
- `tasks/e2e-isolation-001.md`
- 완료 시 implementation report, SOP와 user manual

`infrastructure/docker-compose.yml`, application source, dependency와 migration은 수정하지 않는다.

## 9. 테스트 계획

- shell syntax, shellcheck, actionlint와 Compose config
- valid/invalid database name guard와 SQL-before-fail spy
- 전용 PostgreSQL health와 동적 loopback port
- UAT/E2E container, Compose project, network와 storage 비교
- migration tests와 backend 전체 tests를 전용 PostgreSQL endpoint에서 실행
- 기존 mock UI와 Full-Stack E2E suite
- cleanup, 강제 connection cleanup, 실패/SIGTERM cleanup
- 두 전용 E2E PostgreSQL을 동시에 시작해 project/resource 충돌 부재 확인
- persistent UAT before/after row/schema/delivery 상태와 restart count 비교
- external provider 설정, secret/PII와 변경 범위 검사

## 10. 구현 결과

- E2E run ID, database 이름, Compose project와 port를 fail-closed helper로 검증한다.
- database 이름은 `emi_qms_e2e_*`만 허용하며 invalid name은 Docker/SQL 호출 전 exit 64로 종료한다.
- 전용 `e2e-postgres` service는 고유 Compose project/network, loopback dynamic port와 `/var/lib/postgresql` tmpfs를 사용한다.
- Docker volume mount를 모두 거부해 PostgreSQL 18 image의 익명 volume 자동 생성도 차단한다.
- Full-Stack E2E와 database 조회 fallback 모두 전용 Compose project/service만 사용한다.
- backend는 Testing environment와 external notification provider disabled/dry-run 설정을 강제한다.
- 정상 종료, failure, SIGINT/SIGTERM cleanup은 현재 E2E Compose project만 `down --volumes`하고 잔여 container/network/storage를 검사한다.

## 11. 검증 결과

- DB-name guard: valid 2건 통과, invalid 9건이 Docker/SQL 전 exit 64
- run ID injection guard: 통과
- Compose config, bash syntax, shellcheck, actionlint: 통과
- container/network/storage isolation: 통과
- 동시 전용 PostgreSQL project 2개 기동: 통과
- 강제 connection cleanup과 SIGTERM cleanup: 통과
- Migration tests: 16/16 통과
- Backend 전체 tests: 295/295 통과
- Frontend unit tests: 57/57 통과
- Mock UI smoke: 1/1 통과
- Full-Stack E2E: 16/16 통과
- E2E cleanup 후 E2E container/network: 0
- Persistent UAT: container ID, network, named volume, schema, 업무 row와 restart count 유지
- UAT notification/recipient가 Automatic Reference 7건 증가했지만 delivery row/status는 변화하지 않았다. 실행 중인 UAT worker의 자연 변경으로 E2E project와 무관하며 별도 보고한다.
- External provider: Dispatch/Escalation/Teams/TeamsActivity/Mail disabled, mail provider DryRun, `.env.notify-local` 참조 0

기존 warning은 frontend Fast Refresh warning 1건, build chunk size warning과 Playwright `NO_COLOR`/`FORCE_COLOR` warning이다. 신규 test failure는 없다.

## 12. 남아 있는 항목

- 사용자 checklist는 작성됐으며 사용자 검수 대기다.
- CI Full-Stack job의 기존 bootstrap PostgreSQL은 전용 E2E script가 사용하지 않는 중복 자원으로 남아 있다. 동작·안전 문제는 아니며 후속 P3 최적화 대상이다.

## 13. 산출물 상태

- Implementation report: `tasks/e2e-isolation-001-implementation-report.md`
- SOP: `tasks/e2e-isolation-001-sop.md`
- User manual: `tasks/e2e-isolation-001-user-manual.md`
- Roadmap update: 작성 완료 — [Product Roadmap TASK-E2E-ISOLATION-001](../docs/00-product-roadmap.md#task-e2e-isolation-001-full-stack-e2e-postgresql-물리-격리)
- User validation checklist: 작성됨, 사용자 검수 대기

## 14. 사용자 검수 체크리스트

- [ ] Full-Stack E2E가 persistent UAT PostgreSQL container를 사용하지 않음
- [ ] E2E container와 UAT container가 서로 다름
- [ ] E2E network와 UAT network가 서로 다름
- [ ] E2E storage가 UAT named volume을 사용하지 않음
- [ ] E2E database 이름에 UAT 이름을 넣으면 SQL 실행 전에 거부됨
- [ ] E2E database 이름은 `emi_qms_e2e_*`만 허용됨
- [ ] 기존 Full-Stack E2E suite가 통과함
- [ ] E2E 종료 후 전용 container/network/storage가 정리됨
- [ ] E2E 종료 후 UAT DB row count와 schema가 유지됨
- [ ] UAT PostgreSQL은 E2E 전후 healthy 상태임
- [ ] E2E에서 실제 Teams/Mail/TeamsChannel 발송이 발생하지 않음
- [ ] host `psql`이 없어도 전용 E2E PostgreSQL로 실행 가능
- [ ] SOP를 따라 E2E를 직접 실행할 수 있음
- [ ] User manual이 비개발자도 이해 가능함
