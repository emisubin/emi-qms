# TASK-E2E-ISOLATION-001 SOP

## 1. 문서 목적

Full-Stack E2E를 persistent UAT와 물리적으로 분리된 PostgreSQL에서 실행하고, 시작·검증·cleanup·장애 대응을 동일한 절차로 수행하기 위한 운영 지침이다.

Task 종료 산출물과 사용자 검수 상태는 canonical [Task 종료 및 산출물 정책](../docs/12-task-completion-policy.md)을 따른다.

## 2. 적용 범위

- Local Full-Stack Playwright E2E
- GitHub Actions Full-Stack E2E
- E2E database reset/drop cleanup 검사

Persistent UAT 시작·정지, 데이터 정리와 실제 외부 알림 검수에는 적용하지 않는다.

## 3. 사전 조건

- repository root에서 실행한다.
- Docker와 Docker Compose v2가 실행 가능해야 한다.
- repository 지정 Node.js, pnpm과 .NET SDK가 준비돼 있어야 한다.
- persistent UAT가 실행 중이어도 E2E는 별도 dynamic port를 사용한다.
- 실제 `.env.notify-local`, Graph, SMTP, Webhook credential은 사용하지 않는다.

## 4. Local Full-Stack E2E 시작

```bash
corepack pnpm --filter emi-qms-frontend run e2e:full-stack
```

별도 database 이름을 수동 지정할 필요가 없다. script가 안전한 run ID, Compose project, database와 port를 생성한다.

## 5. 전용 PostgreSQL 확인

E2E가 실행 중일 때 별도 terminal에서 다음처럼 E2E-only label을 확인한다.

```bash
docker ps --filter label=emi-qms.e2e-only=true
```

정상 상태:

- service 이름이 `e2e-postgres`
- project 이름이 `emi-qms-e2e-`로 시작
- persistent `emi-qms-postgres`와 다른 container
- health가 healthy

## 6. DB 이름 규칙

허용 형식은 `emi_qms_e2e_` 뒤에 영문 소문자, 숫자와 underscore가 이어지는 형태다.

허용 예:

- `emi_qms_e2e_local`
- `emi_qms_e2e_run_123`

UAT, development, production, system database 이름과 빈 값은 금지된다. 잘못된 이름은 exit 64와 `E2E safety check failed` 메시지로 SQL 전에 종료된다.

## 7. UAT DB 보호 규칙

- `E2E_DATABASE_NAME`에 `emi_qms_uat_*`, `emi_qms_dev`, `emi_qms_prod`, `postgres`를 지정하지 않는다.
- E2E를 위해 `emi-qms-postgres`를 stop/restart하지 않는다.
- `infrastructure_emi-qms-postgres-data`를 mount하거나 삭제하지 않는다.
- E2E cleanup에 UAT Compose project 이름을 사용하지 않는다.

Guard가 동작하더라도 금지 이름을 정상 절차로 사용하지 않는다.

## 8. E2E 자원 확인

Compose project는 실행별로 고유하다. E2E container의 project와 service label, network, tmpfs는 자동 검사된다.

Storage 정상 기준:

- `/var/lib/postgresql`이 tmpfs
- Docker volume mount 0
- UAT named volume 미사용
- host PostgreSQL port는 127.0.0.1 dynamic port

## 9. Migration과 seed

Full-Stack backend는 `ASPNETCORE_ENVIRONMENT=Testing`과 전용 database endpoint를 받는다. Startup migration과 development test seed는 해당 E2E database에만 적용된다. 기존 migration file과 UAT schema는 변경하지 않는다.

## 10. 외부 provider 차단 확인

E2E backend는 다음을 강제한다.

- Notification dispatch disabled
- Escalation disabled
- TeamsChannel disabled/dry-run
- TeamsActivity disabled/dry-run
- Mail disabled/dry-run, provider `DryRun`

`.env.notify-local`은 E2E script에서 로드하지 않는다. 실제 발송 여부를 확인하기 위한 smoke row도 생성하지 않는다.

## 11. 정상 종료와 cleanup

Playwright 종료 후 script가 자동으로 다음을 수행한다.

1. E2E backend/frontend port 종료 확인
2. E2E database drop 및 부재 확인
3. 현재 E2E Compose project만 종료
4. E2E container/network/ephemeral storage 부재 확인

정상 종료 후 `docker ps --filter label=emi-qms.e2e-only=true`에 해당 실행 자원이 남지 않아야 한다.

## 12. Cleanup 회귀 검사

연결 중인 database 강제 cleanup까지 확인하려면 다음을 실행한다.

```bash
scripts/e2e-cleanup-check.sh
```

이 검사도 고유 Compose project와 tmpfs만 사용하고 종료 후 자원을 제거한다.

## 13. 실패 대응

`E2E safety check failed`가 나오면 이름, project 또는 reserved port 설정을 임의 우회하지 않는다. 환경 override를 제거하고 기본 자동 생성값으로 다시 실행한다.

PostgreSQL health 실패 시:

1. Docker daemon 상태 확인
2. E2E-only container의 health 상태 확인
3. UAT container를 재시작하지 않음
4. 해당 E2E project만 cleanup

## 14. 잔여 자원 정리

자동 cleanup이 실패한 경우 출력된 정확한 E2E project 이름을 확인한 뒤 다음 형식으로 해당 project만 정리한다.

```bash
docker compose \
  --project-name <emi-qms-e2e-run-id> \
  --file infrastructure/docker-compose.e2e.yml \
  down --volumes --remove-orphans
```

`<emi-qms-e2e-run-id>`를 실제 E2E project로 교체한다. `infrastructure`, UAT project 또는 project 이름 없는 `docker compose down`은 금지한다.

## 15. SIGINT/SIGTERM 대응

Ctrl+C 또는 종료 signal을 받으면 EXIT cleanup이 동일하게 실행된다. cleanup 중 다시 종료하지 말고 container/network removal 완료를 기다린다. Signal cleanup 검사는 exit 130/143과 잔여 자원 0을 기준으로 한다.

## 16. CI 경로

CI도 같은 전용 Compose file과 tmpfs 구조를 사용한다. CI의 `E2E_DATABASE_NAME`은 `emi_qms_e2e_ci`다. 기존 CI bootstrap PostgreSQL이 존재하더라도 Full-Stack E2E script가 제공하는 dynamic endpoint만 사용한다. Docker-in-Docker는 사용하지 않는다.

## 17. Persistent UAT 확인

E2E 전후 최소한 다음을 read-only로 확인한다.

- `emi-qms-postgres` health
- restart count
- container/network ID
- persistent named volume
- UAT latest migration과 주요 table count
- notification delivery status count

UAT worker가 실행 중이면 notification 자연 변경이 가능하다. 업무 row와 delivery 변화, E2E 식별자 유입 여부를 분리해 분석한다.

## 18. 금지사항

- UAT DB drop/truncate/reset
- UAT container restart/stop
- UAT named volume 삭제
- database guard 우회
- project 이름 없는 generic Compose cleanup
- 실제 Teams, Graph, SMTP, Webhook 호출
- 실제 credential, `.env` 또는 certificate 내용 출력

## 19. 사용자 검수 체크리스트

검수 상태: 2026-07-10 사용자 검수 완료. 증빙 유형은 대화의 명시적 merge 승인이다.

- [x] 기본 명령으로 E2E를 시작할 수 있음
- [x] UAT와 다른 E2E container/network가 보임
- [x] E2E storage가 tmpfs이고 Docker volume을 사용하지 않음
- [x] UAT 이름을 지정하면 SQL 전에 거부됨
- [x] E2E 종료 후 E2E-only 자원이 남지 않음
- [x] E2E 전후 UAT health와 data가 유지됨
- [x] 실제 외부 알림이 발생하지 않음

## 20. 변경 이력

- 2026-07-10: TASK-E2E-ISOLATION-001 최초 작성 — dedicated Compose/tmpfs, DB-name guard와 scoped cleanup 도입
