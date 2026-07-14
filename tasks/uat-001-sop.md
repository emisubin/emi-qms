# TASK-UAT-001 SOP

## 0. 현재 운영 계약 — Change 001

- Development UAT frontend는 `https://localhost:5174` 하나만 사용한다.
- HTTP 5174 시작은 폐기됐으며 `scripts/dev-uat-start.sh`를 일반 검수 주소 복구에 사용하지 않는다.
- 시작·복구는 `scripts/dev-uat-start-teams-https.sh`의 HTTPS certificate, strict port와 Backend 5081 proxy 계약을 따른다.
- Frontend-only handover에서는 Backend 5081과 Persistent PostgreSQL을 재시작하지 않는다. 실제 delivery 검수는 별도 승인된 Backend-only handover로 분리한다.
- 현재 Backend 5081은 Notification Delivery Worker만 활성이고 Escalation·Purge worker는 비활성이다. Teams Activity channel은 actual mode로 활성화돼 있으므로 신규 actual 발송은 수신자·채널·건수를 명시한 승인 후에만 수행한다.
- Design preview 5176과 Review-safe 5190/5092는 별도 runtime으로 보존한다.
- Change 001의 실제 결과와 cleanup 범위는 [Change 문서](uat-001-change-001.md)를 따른다.

## 1. 문서 목적

EMI Development UAT를 HTTP 또는 HTTPS mode로 안전하게 시작·전환·진단하고 persistent UAT data와 외부 provider 설정을 보호하기 위한 운영 절차다. Task 종료 상태는 canonical [Task 종료 및 산출물 정책](../docs/12-task-completion-policy.md)을 따른다.

## 2. 적용 범위

- 일반 HTTP Development UAT
- Teams Activity 검수용 HTTPS Development UAT
- Backend 5081, frontend 5174, persistent PostgreSQL 상태 확인
- Startup migration과 master-data upsert

Review-safe mode와 운영 배포에는 적용하지 않는다.

## 3. HTTP와 HTTPS Development UAT 차이

| 구분 | 폐기된 HTTP | 현재 HTTPS |
| --- | --- | --- |
| 용도 | 운영하지 않음 | 로그인·일반 기능·알림·Teams Activity 검수 |
| 시작 script | 사용 금지 | `scripts/dev-uat-start-teams-https.sh` |
| URL | N/A | `https://localhost:5174` |
| 저장/수정 | N/A | 가능 |
| worker/provider | N/A | Delivery worker 활성, Escalation·Purge 비활성, Teams Activity actual channel 활성 |

Change 001 이후 HTTPS만 운영한다.

## 4. HTTP 서버 시작 절차

Change 001 이후 HTTP 5174는 시작하지 않는다. 일반 기능, 로그인과 알림 검수를 모두 HTTPS 5174에서 수행한다. HTTP listener가 있으면 소유권을 확인한 frontend-only HTTPS handover로 복구한다.

## 5. HTTPS 서버 시작 절차

1. `.certs` certificate/key 존재 여부만 확인한다.
2. `.env.notify-local`은 승인된 local 위치에 두며 내용을 terminal에 출력하지 않는다.
3. 다음을 실행한다.

```bash
scripts/dev-uat-start-teams-https.sh
```

4. 값 없이 notification key의 `configured`/`missing` 상태를 확인한다.
5. `https://localhost:5174`, `/teams/activity`, health proxy와 backend live/ready를 확인한다.

## 6. HTTPS-only 복구 절차

1. 현재 protocol과 검수 중인 사용자가 없는지 확인한다.
2. Persistent UAT DB snapshot과 PostgreSQL restart count를 기록한다.
3. HTTPS startup 또는 승인된 frontend-only handover를 한 번 실행한다.
4. Script가 기존 listener와 screen session의 repository ownership을 확인하도록 둔다.
5. HTTPS는 200, HTTP는 실패하는지 확인한다.
6. 저장·수정·실제 알림이 필요한 검수는 사용자 승인 범위를 다시 확인한다.

Unexpected process 오류가 나오면 해당 process를 임의 종료하지 않는다.

## 7. 인증서 준비

Local certificate가 없을 때만 다음 절차를 사용한다.

```bash
brew install mkcert
mkcert -install
mkdir -p .certs
mkcert -key-file .certs/localhost-key.pem -cert-file .certs/localhost.pem localhost 127.0.0.1 ::1
```

Certificate와 private key는 local ignored file이며 commit하지 않는다. 내용, fingerprint 외 민감 metadata와 key 원문을 문서나 log에 복사하지 않는다.

## 8. PostgreSQL 상태 확인

```bash
docker inspect -f '{{.State.Health.Status}} restart={{.RestartCount}}' emi-qms-postgres
docker volume inspect infrastructure_emi-qms-postgres-data >/dev/null
```

정상 기준은 `healthy`, 불필요한 restart 증가 없음, persistent volume 존재다. UAT DB 이름은 `emi_qms_uat_005a`다.

## 9. Backend/frontend health 확인

```bash
curl -fsS http://127.0.0.1:5081/health/live >/dev/null
curl -fsS http://127.0.0.1:5081/health/ready >/dev/null
curl -fsS https://localhost:5174/health/live >/dev/null
```

HTTPS curl은 신뢰된 local certificate 기준으로 실행한다. 정상 검수에서 `--insecure`를 사용해 certificate 문제를 숨기지 않는다.

## 10. 5174 port 충돌 진단

```bash
lsof -nP -iTCP:5174 -sTCP:LISTEN
```

- Expected repository Vite면 startup script가 안전하게 mode를 교체할 수 있다.
- 다른 cwd/command면 script가 중단한다.
- 5175 등 fallback port로 이동하지 않는다.
- 다른 process를 수동 kill하지 말고 소유자와 목적을 확인한다.

## 11. PID/session 확인

```bash
screen -ls | grep 'emi-qms-uat-'
cat /tmp/emi-qms-dev-uat-backend.pid
cat /tmp/emi-qms-dev-uat-frontend.pid
lsof -tiTCP:5081 -sTCP:LISTEN
lsof -tiTCP:5174 -sTCP:LISTEN
```

PID file과 실제 listener PID가 같아야 한다. Stale PID file은 process 종료 근거로 사용하지 않으며 startup 성공 시 실제 listener PID로 갱신된다. Screen cwd가 repository root 또는 실제 하위 경로가 아니면 종료를 거부한다.

## 12. Dotenv loading 주의사항

- `.env.notify-local`을 shell에서 `source` 또는 `eval`하지 않는다.
- Startup script의 literal parser만 사용한다.
- `Notifications__` namespace 밖 key는 HTTPS notification loading에서 무시한다.
- 설정 값은 출력하지 않고 `configured`/`missing`만 확인한다.
- Links BaseUrl이 없으면 mode별 frontend URL fallback을 사용한다.

## 13. Notification worker 확인

Backend process는 다음 hosted service를 등록한다.

- `NotificationDeliveryWorker`
- `NotificationEscalationWorker`
- `AdminDeletionPurgeWorker`

Daily Digest는 delivery dispatcher 흐름에 포함된다. Pending/Failed 상태는 read-only query로 관찰하고 새 smoke row를 만들지 않는다. Actual 발송 검수는 수신자·채널·data 변경에 대한 사용자 승인을 별도로 확인한다.

2026-07-14 승인 smoke에서는 기존 `TeamsActivityDisabled` terminal 2건을 audit로 보존하고 HTTPS 5174에서 신규 ManualTest Teams Activity 1건만 생성했다. 동일 delivery의 retry lineage는 `RetryScheduled`, `RetryScheduled`, `Sent`이며 신규 delivery를 반복 생성하지 않았다. Provider `Sent`와 Teams client 실제 표시를 각각 확인했다.

## 14. DB 보존 원칙

- UAT DB drop/truncate/reset 금지
- Persistent container stop/restart 금지
- Named volume 삭제 금지
- 테스트 data hard delete 금지
- Startup migration은 기존 migration file을 수정하지 않고 미적용 file만 적용
- Master-data upsert는 transaction 전체 성공 또는 rollback

## 15. Isolated E2E 사용 원칙

Full-Stack E2E는 다음 명령만 사용한다.

```bash
corepack pnpm --filter emi-qms-frontend run e2e:full-stack
```

실행별 전용 PostgreSQL/container/network/tmpfs와 `emi_qms_e2e_*` DB를 사용한다. UAT container/volume fallback은 금지되고 actual provider는 Testing에서 disabled/dry-run이다.

## 16. Startup 실패 대응

1. 출력된 실패 단계만 확인한다.
2. Port 소유권 실패면 process를 종료하지 않는다.
3. PostgreSQL health 실패면 UAT container를 재시작하지 말고 Docker 상태와 log를 읽기 전용으로 조사한다.
4. Master-data SQL 실패면 transaction rollback 여부를 확인하고 statement를 반복 적용하지 않는다.
5. Env/certificate 원문을 진단 log에 복사하지 않는다.

## 17. Protocol 오류 대응

- `ERR_SSL_PROTOCOL_ERROR`: HTTPS URL에 HTTP server가 있는지 현재 mode를 확인한다.
- HTTP에서 TLS 응답: 기대 mode가 잘못됐거나 이전 HTTPS session이 남은 상태다.
- HTTPS certificate 오류: certificate trust와 hostname을 확인한다. `--insecure`로 성공 처리하지 않는다.
- Root는 성공하지만 proxy가 실패하면 backend 5081 live/ready와 `VITE_DEV_PROXY_TARGET`을 확인한다.

## 18. 서버 종료/재시작

이번 Task 수행 중에는 원래 UAT를 종료하거나 재시작하지 않는다. 이후 운영자가 명시적으로 종료해야 할 때는 검수 사용자와 actual provider queue를 먼저 확인하고 정확한 repository screen session만 종료한다. Generic process kill, Docker Compose down과 volume 삭제는 사용하지 않는다.

재시작은 목적 mode startup script로 수행하며, 기존 repo-owned session의 안전한 교체는 script에 맡긴다.

## 19. 보안 주의사항

- Env/secret/token/webhook/Authorization header 출력 금지
- Certificate private key 출력·commit 금지
- 실제 사용자 이름·회사 이메일/UPN을 검수 증빙에 기록하지 않음
- External notification은 사용자 승인 없이 생성하지 않음
- Console screenshot이나 log에 개인정보가 포함되면 tracked 문서에 첨부하지 않음

## 20. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 완료`.

- [x] HTTP startup과 HTTPS startup을 SOP 순서로 각각 실행 가능
- [x] Mode 전환 후 5174가 기대 protocol만 응답
- [x] 다른 process 점유 시 종료하지 않고 실패
- [x] HTTPS Teams Activity와 관리자 화면 정상
- [x] 저장·수정 가능한 Development mode 확인
- [x] 실제 알림은 별도 승인 범위에서만 검수하는 원칙 확인
- [x] 승인된 신규 ManualTest Teams Activity 1건의 Microsoft Graph actual `Sent` 확인
- [x] Teams client Activity Feed 표시 확인
- [x] UAT DB/schema/data와 persistent volume 유지
- [x] E2E가 UAT 자원을 사용하지 않음
- [x] 오류 대응과 금지사항을 이해함

검수 증빙: Task 승인자 / 2026-07-10 / PR #23 및 HTTPS Development UAT / 승인 / 현재 대화의 명시적 검수·병합 승인.

## 21. 변경 이력

- 2026-07-10: TASK-UAT-001 최초 작성 — strict port/ownership/readiness/dotenv/transaction 도입
- 2026-07-10: 최신 main `45fd61c` 통합, E2E isolation 연계와 당시 사용자 검수 대기 상태 반영(역사적 기록)
- 2026-07-10: 사용자 검수 완료와 PR #23 squash merge 승인 반영
- 2026-07-14: Change 001에서 Development UAT를 HTTPS 5174 하나로 통일하고 HTTP 시작 절차를 폐기. 자동 검증 완료 / 사용자 검수 대기
- 2026-07-14: Backend 5081 Delivery Worker만 활성 유지, Teams Activity actual channel 활성화, 기존 terminal 2건 보존과 신규 ManualTest 1건 Graph `Sent`·Teams client 표시 검수 완료. 잔여 사용자 검수 2건 대기, merge 승인
