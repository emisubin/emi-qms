# TASK-AZURE-PILOT-001 SOP

## 1. 적용 범위

이 SOP는 특정 Azure 서비스를 고르기 전에 공통으로 사용할 Production 설정, image, Entra와 migration 절차를 설명한다. 실제 Azure resource 생성, domain 연결, managed DB, WAF, SIEM과 traffic 전환은 포함하지 않는다.

## 2. 사전 원칙

- 실제 운영 env/secret 파일은 Repository 밖 승인된 secret 저장소에서 관리한다.
- tenant/client/email/password/certificate 원문을 terminal transcript, Task 문서와 PR에 넣지 않는다.
- Production에서는 Development authentication, 사용자 전환과 개발 data seed를 모두 끈다.
- 기존 migration file을 수정하거나 migration rollback SQL을 실행하지 않는다.
- migration이 실패하면 application을 시작하지 않는다.

## 3. 필수 설정

서비스 선정 후 다음 값을 운영 담당자가 준비한다.

- public HTTPS host
- 서로 다른 Entra API app과 SPA app identifier
- API app의 `access_as_user` delegated scope와 audience
- verified Entra domain
- `SSL Mode=VerifyFull` managed PostgreSQL connection string secret
- 서로 다른 비상 관리자 두 명
- 최근 90일 이내 restore 검증 시각
- security log/alert sink
- TLS certificate/key와 syslog CA

Legacy `ENTRA_CLIENT_ID`는 사용하지 않는다.

## 4. 사전점검

```bash
bash scripts/production-preflight.sh /secure/path/emi-qms-production.env
```

TLS 파일까지 준비됐으면 다음 검사를 추가한다.

```bash
PRODUCTION_PREFLIGHT_VALIDATE_TLS=true \
  bash scripts/production-preflight.sh /secure/path/emi-qms-production.env
```

정상 결과는 `productionPreflight=true`, `entraSplit=true`, `composeConfig=true`, `migrationJob=true`다. 실패 시 stable failure code를 기준으로 설정을 고치며 검사를 끄거나 값을 우회하지 않는다.

## 5. Image build와 검사

서비스와 실행 architecture가 정해진 뒤 해당 platform을 명시해 Backend·Frontend image를 빌드한다. Release에는 full commit SHA로 불변 tag를 사용하고 base image digest pin을 유지한다.

Image scan에서 Critical/High가 나오면 게시하지 않는다. 이전 P3 Low finding도 최종 architecture와 승인된 scanner로 다시 확인한다.

## 6. Migration rehearsal

Repository의 disposable DB 검증은 다음과 같이 실행한다.

```bash
bash scripts/test-production-migration-image.sh <backend-image-ref>
```

정상 기준:

- fresh apply 성공
- 같은 image의 existing apply 성공
- migration ledger Exact
- disposable DB/container/network cleanup 성공

실제 운영 DB에는 이 검증 script를 사용하지 않는다.

## 7. 운영 migration과 application 기동

사전점검과 승인된 backup/restore 증빙이 완료된 후 새 release image로 migration을 먼저 실행한다.

```bash
docker compose \
  --env-file /secure/path/emi-qms-production.env \
  -f infrastructure/docker-compose.production.yml \
  --profile operations \
  run --rm migration
```

명령이 성공하고 ledger가 `Exact` 또는 승인된 historical-compatible 상태이며 schema compatibility가 준비된 경우에만 application을 기동한다.

```bash
docker compose \
  --env-file /secure/path/emi-qms-production.env \
  -f infrastructure/docker-compose.production.yml \
  up -d --wait
```

Migration을 동시에 실행해도 advisory lock이 순서를 보장하지만 운영자는 release당 한 번만 실행한다.

## 8. 기동 후 확인

실제 host에서 다음을 확인한다.

- HTTPS만 응답
- 허용 host root와 `/health/live`, `/health/ready` 정상
- 잘못된 Host와 HTTP 차단
- Microsoft 365 로그인과 로그아웃 뒤 cache 차단
- 업무 역할별 조회·수정 권한
- clean upload 성공, malware/unscannable/metadata upload 차단
- rate-limit 발생과 정상 복구
- security alert sink 수신
- worker/provider는 승인된 범위만 활성

Health 응답에 환경, DB, migration 또는 secret 원문을 추가하지 않는다.

## 9. 실패 대응

### Preflight 실패

실패 code에 해당하는 설정만 수정한다. env 파일을 shell source하지 않고 identifier 원문을 공유하지 않는다.

### Migration 실패

1. application 기동을 중단한다.
2. ledger count/status와 실패 code만 기록한다.
3. 기존 migration은 수정·삭제하지 않는다.
4. additive forward-fix migration을 만들고 전체 검증을 다시 수행한다.
5. 운영 DB restore가 필요하면 선택된 managed DB의 승인된 복구 절차를 사용한다.

### Application readiness 실패

새 release로 traffic을 보내지 않는다. 이전 verified image가 기존 schema와 호환되는지 확인한 후 application만 rollback한다. DB migration은 되돌리지 않는다.

## 10. 서비스 선정 후 추가해야 할 SOP

다음 항목은 별도 `UAT_RUNTIME` change와 사용자 승인 후 이 SOP에 연결한다.

- Azure resource topology와 network
- registry/OIDC release 권한
- domain/TLS 갱신
- managed DB backup/PITR/restore
- WAF/DDoS/rate-limit edge
- SIEM retention/alert on-call
- blue/green 또는 revision rollback
- pilot 종료, data export·retention·삭제
