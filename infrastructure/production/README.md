# Production security handover

이 구성은 Development server를 공개하지 않고, 한 개의 TLS reverse proxy만 외부에 노출한다. Backend와 ClamAV는 외부 port를 publish하지 않는 internal network에 둔다.

## 배포 전 필수 조건

1. `PUBLIC_HOST`의 DNS와 공인 TLS 인증서를 준비한다. 인증서의 SAN이 domain과 일치하고 private key가 짝이 맞아야 하며, 만료가 30일보다 적게 남으면 container가 시작되지 않는다.
2. Entra SPA redirect URI를 정확히 `https://PUBLIC_HOST`로 등록하고 tenant, client, audience, delegated scope와 verified domain을 입력한다.
3. application DB 계정은 최소 권한으로 만들고 connection string에 `SSL Mode=VerifyFull`을 사용한다.
4. 최근 90일 안에 실제 backup으로 별도 환경 restore를 성공시킨 뒤 그 UTC 시각을 `RESTORE_VERIFIED_AT_UTC`에 기록한다.
5. 서로 다른 두 비상 관리자 email을 secret file 한 줄에 쉼표 또는 세미콜론으로 기록한다.
6. container security log가 TLS syslog로 승인된 SIEM·alert sink에 전달되도록 주소와 CA 인증서를 설정하고 경보 수신을 시험한다.
7. TLS key, DB connection string과 관리자 목록 secret file은 Repository 밖에 두고 운영자만 읽을 수 있게 한다.

위 조건 중 하나라도 빠지면 Compose 환경 확장 또는 Backend Production startup이 실패한다. 이 실패를 설정을 끄는 방식으로 우회하면 안 된다.

## 검증

```sh
docker compose \
  --env-file /secure/path/emi-qms-production.env \
  -f infrastructure/docker-compose.production.yml \
  config
```

실제 기동 뒤에는 허용된 host의 HTTPS root와 `/health/live`, `/health/ready`만 확인한다. 잘못된 Host, HTTP, scanner 장애 upload와 허용량 초과 요청은 각각 차단되어야 한다. 실제 Teams·Mail 발송, migration과 data reset은 별도 운영 승인 대상이다.

## Cache와 보안 header 기준

- Backend 응답은 인증 여부와 무관하게 `private, no-store`를 사용한다. 공유 PC와 브라우저 cache에 업무 응답을 남기지 않는다.
- HTML shell은 `no-cache`로 매번 최신 배포 여부를 확인한다.
- fingerprint가 포함된 `/assets/`만 1년 cache한다.
- HTML과 asset 응답 모두 HSTS, CSP, nosniff, referrer, permissions와 cross-origin header를 유지해야 한다.
- Nginx `location` 안에 별도 `add_header`를 추가하면 상위 보안 header 상속이 끊길 수 있다. 새 header는 server-level 정책에 추가하거나 합성 TLS 검사를 먼저 보강한다.

## 외부 artifact 고정과 갱신 절차

Production Dockerfile과 Compose image는 읽기 쉬운 tag 뒤에 multi-platform digest를 함께 기록한다. CI의 GitHub Action은 full commit SHA, PostgreSQL service는 tag+digest를 사용한다. Tag만 남기거나 digest·SHA를 임의 제거하지 않는다.

갱신할 때는 다음 순서를 지킨다.

1. Docker Hub, Microsoft Artifact Registry와 GitHub의 공식 배포 정보에서 대상 tag·commit을 확인한다.
2. 실제 대상 platform을 포함하는 manifest digest 또는 Action tag가 가리키는 full commit SHA를 기록한다.
3. Production image를 다시 build하고 Compose·Actionlint·Repository immutable-reference 회귀를 실행한다.
4. Backend·Frontend 전체 회귀, isolated Full-Stack E2E, package audit와 container vulnerability scan을 다시 통과한다.
5. Critical·High·Medium Finding이 있으면 갱신을 게시하지 않는다. 수정본이 없는 Low는 CVE·영향 image·재검사 시점을 P3 backlog로 기록한다.
6. 운영 handover 직전에 같은 digest를 다시 scan하고, 결과와 rollback digest를 배포 기록에 남긴다.

TLS 인증서의 만료·hostname·private key 일치는 고정된 `alpine/openssl` one-shot validator가 확인한다. Frontend image 안에서 가변 package repository를 조회하지 않으며 validator 성공 전에는 Frontend를 시작하지 않는다.

2026-07-29 기준 Frontend와 ClamAV image에는 동일한 libxml2 Low 2건이 남지만 scanner가 제공하는 수정 버전이 없다. `SEC-PUBLIC-014` P3로 추적하며 운영 handover와 다음 digest 갱신 전에 재검사한다.

Scanner가 별도 분류하지 못한 `CVE-2026-11979`와 `CVE-2026-58055`는 각각 `xmlcatalog --shell`과 `nghttpx` 실행 경로에 한정된다. 두 final image에 해당 실행 파일이 없음을 확인해 `SEC-PUBLIC-015`를 `RESOLVED_NOT_AFFECTED`로 기록했다. Base image나 package 구성이 바뀌면 이 판정을 재사용하지 않고 다시 확인한다.
