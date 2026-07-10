# TASK-E2E-ISOLATION-001 User Manual

## 1. E2E란 무엇인가

E2E(End-to-End) 테스트는 화면, backend와 database를 함께 실행해 실제 업무 흐름처럼 동작하는지 확인하는 자동 테스트다.

## 2. UAT와 E2E의 차이

UAT는 사용자가 화면을 보며 저장·수정·알림을 검수하는 지속 환경이다. E2E는 자동 테스트가 test data를 만들고 종료 후 모두 버리는 임시 환경이다. 두 환경은 data와 실행 자원을 공유하면 안 된다.

## 3. 왜 DB를 분리하는가

E2E는 시작과 종료 과정에서 test database를 생성하고 삭제한다. UAT와 같은 PostgreSQL을 사용하면 설정 실수로 검수 data를 삭제할 위험이 있다. 따라서 E2E마다 별도 PostgreSQL container, network와 memory storage를 만든다.

## 4. 테스트 실행 방법

Repository root에서 다음 명령을 실행한다.

```bash
corepack pnpm --filter emi-qms-frontend run e2e:full-stack
```

DB 이름이나 port는 직접 지정하지 않아도 된다. 실행할 때마다 충돌하지 않는 값이 자동 생성된다.

## 5. 정상 실행 기준

- 전용 PostgreSQL이 healthy가 됨
- 기존 16개 이상 Full-Stack test가 모두 통과함
- 종료 과정에서 E2E database, container와 network가 제거됨
- UAT 화면과 PostgreSQL은 계속 실행됨
- 실제 Teams나 Mail 알림이 발송되지 않음

## 6. 보호 오류가 나오는 이유

`E2E safety check failed`는 UAT 또는 운영성 DB를 보호하기 위해 실행을 멈춘 것이다. 대표 원인은 다음과 같다.

- DB 이름이 `emi_qms_e2e_`로 시작하지 않음
- UAT port를 E2E port로 지정함
- E2E 전용이 아닌 Compose project/service를 지정함
- E2E PostgreSQL에 Docker volume이 연결됨

오류를 우회하지 말고 수동 환경 변수를 제거한 뒤 기본 명령으로 다시 실행한다.

## 7. 하면 안 되는 설정

- `E2E_DATABASE_NAME=emi_qms_uat_005a`
- `E2E_DATABASE_NAME=emi_qms_dev` 또는 운영 DB 이름
- E2E를 기존 `emi-qms-postgres`에 연결
- `infrastructure_emi-qms-postgres-data`를 E2E에 mount
- project 이름 없이 `docker compose down`
- 실제 notification credential을 E2E에 전달

## 8. 실행 중 자원 확인

다음 명령은 E2E 전용 container만 보여 준다.

```bash
docker ps --filter label=emi-qms.e2e-only=true
```

이 목록의 container는 persistent `emi-qms-postgres`와 다른 이름과 ID를 가져야 한다.

## 9. 종료 후 확인

테스트가 끝난 뒤 같은 명령에 해당 실행의 container가 남지 않아야 한다. 자동 cleanup이 실패하면 SOP의 “잔여 자원 정리” 절차를 사용하고 UAT project는 건드리지 않는다.

## 10. UAT data가 유지되는지 확인하는 방법

테스트 전후 UAT의 health, restart count, latest migration과 주요 업무 table count를 비교한다. UAT worker가 실행 중이면 notification은 자연 증가할 수 있으므로 projects, work items와 delivery 상태 변화 여부를 함께 확인한다.

## 11. Host psql이 없는 경우

별도 설치가 필요 없다. E2E script와 Full-Stack test는 전용 PostgreSQL container 안의 `psql`을 사용한다. 기존 UAT container로 fallback하지 않는다.

## 12. 외부 알림

E2E backend는 notification dispatch와 TeamsChannel, TeamsActivity, Mail, escalation을 끈다. Mail provider도 DryRun으로 고정한다. E2E에 `.env.notify-local`을 전달하지 않는다.

## 13. 자주 발생하는 문제

### Docker가 실행되지 않음

Docker Desktop 또는 Docker daemon을 시작하고 다시 실행한다. UAT container를 재시작할 필요는 없다.

### E2E safety 오류

수동으로 지정한 `E2E_*` 환경 변수를 제거한다. 기본 자동 생성값을 사용한다.

### 테스트가 실패했는데 container가 남음

SOP에서 정확한 E2E Compose project 이름을 확인하고 그 project만 cleanup한다. `infrastructure` project를 종료하지 않는다.

### UAT notification count가 바뀜

UAT worker나 사용자 검수 동작에 따른 자연 변경일 수 있다. E2E delivery 또는 project가 UAT에 들어갔는지 별도로 확인하고 바로 삭제하지 않는다.

## 14. FAQ

### HTTP/HTTPS UAT를 꺼야 하나요?

아니다. E2E는 별도 dynamic port와 PostgreSQL을 사용하므로 실행 중인 UAT를 유지할 수 있다.

### E2E data는 어디에 저장되나요?

전용 PostgreSQL container의 memory-backed tmpfs에 저장된다. container가 정리되면 data도 사라진다.

### E2E database 이름을 직접 정할 수 있나요?

가능하지만 `emi_qms_e2e_` prefix와 안전 문자 규칙을 따라야 한다. 일반적으로 자동값 사용을 권장한다.

### 테스트가 실제 Teams에 알림을 보내나요?

아니다. 실제 provider는 Testing environment에서 비활성화된다.

### 두 테스트를 동시에 실행할 수 있나요?

각 실행은 고유 Compose project, database와 dynamic port를 사용한다. 검증에서는 전용 PostgreSQL project 두 개를 동시에 실행해 충돌이 없음을 확인했다.

## 15. 사용자 검수 체크리스트

- [ ] 기본 Full-Stack E2E 명령을 실행할 수 있음
- [ ] E2E와 UAT container가 서로 다름
- [ ] E2E와 UAT network가 서로 다름
- [ ] E2E가 UAT named volume을 사용하지 않음
- [ ] UAT DB 이름이 SQL 전에 거부됨
- [ ] 기존 Full-Stack E2E suite가 통과함
- [ ] 종료 후 E2E container/network/storage가 남지 않음
- [ ] UAT DB와 HTTPS UAT가 유지됨
- [ ] 실제 외부 알림이 발생하지 않음
- [ ] 이 문서의 오류 대응을 이해할 수 있음
