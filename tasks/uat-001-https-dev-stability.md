# TASK-UAT-001 HTTPS Development UAT 안정화

## 1. 목적

일반 HTTP Development UAT와 Teams 검수용 HTTPS Development UAT가 같은 frontend port `5174`를 안전하게 교체 사용하도록 startup 계약을 확립한다. Persistent UAT data와 실행 중인 HTTPS session을 보존하면서 strict port, process ownership, protocol readiness, notification dotenv loading과 master-data transaction의 P2를 해소한다.

## 2. 배경

기존 startup은 backend readiness는 확인했지만 frontend가 지정 port와 protocol로 실제 준비됐는지 확인하지 않았다. Vite가 다른 port로 자동 이동할 수 있었고 5174 listener 또는 screen session이 이 repository 소유인지 확인하는 기준도 부족했다. HTTPS wrapper의 notification 설정 전달과 여러 master-data statement의 원자성도 명확한 검증 계약이 필요했다.

원래 `task/uat-001-https-dev-stability` worktree의 미커밋 WIP와 실행 중인 HTTPS UAT는 변경하지 않았다. WIP를 checksum이 있는 외부 임시 patch로 백업하고 최신 main `45fd61c2c9119cb0905a75c9005f123c404a1a31` 기반 별도 worktree에 적용했다.

## 3. 해결한 P2

1. Vite frontend 5174 strict port 부재
2. listener, PID file과 screen session의 repository ownership 확인 부재
3. HTTP/HTTPS protocol readiness 오판 가능성
4. frontend port 설정 미사용 또는 script/config 불일치
5. HTTPS wrapper notification env loading 정합성
6. UAT master-data SQL block 부분 성공 가능성

원래 WIP 리뷰에서 repo 경로와 이름만 비슷한 sibling directory까지 소유로 오인할 수 있는 prefix 경계 문제를 추가로 확인했다. Integration worktree에서는 repo root 자체 또는 실제 하위 경로만 소유로 인정하도록 보강했다.

## 4. 사용자 결정

- HTTPS UAT는 Review-safe mode가 아닌 Development mode다.
- 일반 개발은 HTTP UAT, Teams Activity 검수는 HTTPS UAT를 사용한다.
- 저장·수정·worker·승인된 실제 알림 검수가 가능한 환경으로 유지한다.
- Review-safe UAT는 `TASK-UAT-002`로 분리한다.
- 작업 중 원래 HTTPS backend/frontend/PostgreSQL을 종료하거나 재시작하지 않는다.
- 신규 외부 알림 smoke는 이번 자동 검증에서 생성하지 않는다.
- 사용자 검수 완료 전에는 Draft PR만 허용한다.

## 5. 포함 범위

- `frontend/vite.config.ts` env port와 `strictPort`
- `scripts/dev-uat-start.sh` port, protocol, process/PID/session ownership과 readiness
- `scripts/dev-uat-start-teams-https.sh` HTTPS certificate와 notification env 연결
- `.env.notify-local`의 literal parser와 `Notifications__` namespace filter
- notification link BaseUrl의 mode별 runtime fallback
- workflow/production-planning master-data transaction
- persistent UAT read-only 검증과 isolated E2E 회귀
- 5종 종료 산출물과 Roadmap 상태

## 6. 제외 범위

- Review-safe UAT
- frontend dependency security
- notification claim/lease와 retry lineage
- escalation starvation
- 마지막 System Administrator 동시성
- Git history 개인정보 처리
- 실패 delivery 수동 재처리 고도화
- 사용자별 알림 설정
- 신규 업무 기능
- UAT DB reset, data hard delete, container restart와 volume 삭제

## 7. HTTP/HTTPS 사용 정책

| mode | 시작 script | frontend | readiness |
| --- | --- | --- | --- |
| HTTP Development UAT | `scripts/dev-uat-start.sh` | `http://localhost:5174` | HTTP root/health 성공, HTTPS 성공을 요구하지 않음 |
| HTTPS Development UAT | `scripts/dev-uat-start-teams-https.sh` | `https://localhost:5174` | 신뢰된 인증서 HTTPS root/Teams/health 성공, HTTP 성공 시 mismatch 실패 |

두 mode는 동시에 실행하지 않고 같은 5174를 교체한다. 실제 전환은 사용자 검수 항목이며 이번 자동 검증에서는 현재 5174 session을 유지했다.

## 8. Strict port

- Vite는 `VITE_DEV_SERVER_PORT`를 실제 `server.port`로 사용한다.
- `strictPort=true`이며 script 실행 인자에도 `--strictPort`를 지정한다.
- Manual UAT는 `FRONTEND_PORT=5174`만 허용한다.
- 5184 격리 검사에서 다른 process가 port를 점유하면 Vite가 non-zero로 종료했고 5185 listener를 만들지 않았다.

## 9. Process ownership

- 5174 listener PID의 cwd와 command가 repository frontend/Vite인지 확인한다.
- backend 5081도 repository backend process인지 확인한다.
- screen session daemon cwd가 repo root 또는 실제 하위 경로일 때만 종료한다.
- repo 이름과 비슷한 sibling 경로는 소유로 인정하지 않는다.
- unexpected process는 종료하지 않고 fail closed한다.
- startup 성공 후 PID file은 launcher가 아닌 실제 listener PID로 덮어쓴다. 기존 stale PID file은 종료 근거로 사용하지 않는다.

## 10. Protocol readiness

- HTTP mode는 HTTP root 200과 HTTPS 실패를 확인했다.
- HTTPS mode는 신뢰된 local certificate로 HTTPS root와 `/health/live` proxy 200, HTTP 실패를 확인했다.
- 현재 live HTTPS UAT에서 root, `/teams/activity`, `/admin`, `/api/me`, `/api/projects`, backend live/ready가 200이다.

## 11. Dotenv loading

- `.env.notify-local`을 `source` 또는 `eval`하지 않는다.
- key는 `Notifications__` namespace만 허용한다.
- 값은 `export "key=value"`로 literal 전달하며 공백, `&`, 세미콜론과 command-substitution 형태를 실행하지 않는다.
- log에는 값 대신 `configured`/`missing`만 출력한다.
- 현재 local 설정은 dispatch와 TeamsChannel/TeamsActivity/Mail actual 조건이 구성돼 있다. 실제 값은 출력하거나 문서화하지 않았다.
- `Notifications__Links__BaseUrl` 미설정 시 HTTPS mode는 `https://localhost:5174`를 runtime fallback으로 사용한다.

## 12. Master-data transaction

- 두 master-data block 모두 `psql -v ON_ERROR_STOP=1`, `BEGIN`, `COMMIT`을 사용한다.
- 업무 SQL statement 자체는 변경하지 않고 transaction 경계만 추가했다.
- 전용 E2E PostgreSQL에서 각 block의 commit 직전 오류를 주입한 결과 workflow와 production-planning 관련 schema/data snapshot이 모두 원상 유지됐다.

## 13. Persistent UAT와 isolated E2E

- Persistent UAT는 `emi_qms_uat_005a`와 기존 named volume을 유지한다.
- Full-Stack E2E는 PR #22의 실행별 전용 PostgreSQL container/network/tmpfs와 `emi_qms_e2e_*` DB를 사용한다.
- UAT-001 변경은 E2E isolation script와 Compose file을 수정하거나 우회하지 않는다.
- Full-Stack E2E 16개 후 UAT container ID, restart count, schema/count와 delivery status가 유지됐고 E2E 잔여 자원은 0건이었다.

## 14. 데이터 보존 원칙

- UAT DB drop/truncate/reset 금지
- Persistent UAT container stop/restart 금지
- Persistent volume 삭제 금지
- 테스트 data hard delete 금지
- 신규 actual notification smoke 금지
- 실행 중 worker의 자연 변경 가능성은 자동 검증 변화와 분리해 보고

## 15. 검증 결과

- Bash 3.2 syntax, shellcheck, actionlint, `git diff --check`: 통과
- Port/ownership/dotenv/transaction helper 검사: 통과
- HTTP/HTTPS 5184 격리 readiness와 strict-port: 통과
- Backend Release build: warning/error 0
- Migration 16/16, Authorization 47/47, Notification 62/62, Backend 295/295
- Frontend lint/typecheck/unit 57/57/build, mock UI 1/1
- Full-Stack E2E 16/16, persistent UAT snapshot 유지
- Browser: main/my work/notification/Teams/admin/delivery/send route, console error 0
- Narrow viewport: 실제 content width 375px에서 main/Teams/admin overflow 0

기존 warning은 frontend Fast Refresh 1건, build chunk-size와 Playwright color 환경 warning이다.

## 16. 남은 P2

- `TASK-FRONTEND-SEC-001`
- `TASK-UAT-002`
- `UAT-VERIFY-001`
- `TASK-NOTIFY-REL-001`
- `TASK-NOTIFY-ESC-001`
- `TASK-AUTH-HARDEN-001`
- `TASK-GOV-002`
- 실패 delivery 수동 재처리 고도화
- 사용자별 알림 설정

## 17. 후속 Task 순서

1. `TASK-FRONTEND-SEC-001`
2. `TASK-UAT-002`
3. `UAT-VERIFY-001`
4. `TASK-NOTIFY-REL-001`
5. `TASK-NOTIFY-ESC-001`
6. `TASK-AUTH-HARDEN-001`

`TASK-NOTIFY-REL-001`과 `TASK-NOTIFY-ESC-001`은 Roadmap의 `TASK-NOTIFY-004` umbrella 범위 중 P2 remediation을 분리한 실행 Task다. `TASK-AUTH-HARDEN-001`은 기존 `TASK-AUTH-001`의 실행 ID다.

## 18. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 완료`.

- [x] `https://localhost:5174` 접속 가능
- [x] `/teams/activity` 접속 가능
- [x] `/admin` 접속 가능
- [x] API/User 카드 정상
- [x] 프로젝트·업무·알림 조회 가능
- [x] 저장/수정 가능한 Development mode임
- [x] 수동 알림 발송 화면 사용 가능
- [x] Teams Activity actual 설정 configured
- [x] notification worker 실행 중
- [x] Pending delivery 장시간 방치 없음
- [x] HTTP/HTTPS 전환 시 5174 충돌 없음
- [x] frontend가 다른 port로 자동 이동하지 않음
- [x] 타 process 점유 시 해당 process를 종료하지 않고 실패함
- [x] 기존 persistent UAT DB 유지
- [x] E2E가 persistent UAT DB/volume을 사용하지 않음
- [x] Full-Stack E2E 통과
- [x] Console 오류 없음
- [x] Teams narrow pane/mobile overflow 없음
- [x] SOP를 따라 직접 서버 시작 가능
- [x] User manual이 비개발자에게 이해 가능함

검수 증빙: Task 승인자 / 2026-07-10 / PR #23 및 HTTPS Development UAT / 승인 / 현재 대화의 명시적 검수·병합 승인. 저장·수정과 실제 외부 알림 신규 smoke는 자동 검증에서 실행하지 않았으며, 사용자 검수와 자동 검증 결과를 구분해 기록한다.

## 19. 5종 산출물 상태

- Implementation report: [TASK-UAT-001 Implementation Report](uat-001-implementation-report.md), 작성 완료
- SOP: [TASK-UAT-001 SOP](uat-001-sop.md), 작성 완료
- User manual: [TASK-UAT-001 User Manual](uat-001-user-manual.md), 작성 완료
- Roadmap update: [Product Roadmap TASK-UAT-001](../docs/00-product-roadmap.md#task-uat-001-https-development-uat-안정화), 작성 완료
- User validation checklist: 이 문서 18장, 작성됨 / 자동 검증 완료 / 사용자 검수 완료
