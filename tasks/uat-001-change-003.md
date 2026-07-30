# TASK-UAT-001 Change 003 — 공개 배포 P0 노출 차단

## 1. Task Identity Gate

- proposedTaskId: `TASK-UAT-001 Change 003`
- taskType: `SECURITY_HARDENING`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `운영 전환 Scope Review`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `운영 전환 — Task ID 미정`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## 2. Purpose identity

- 업무 목표: 공개 배포 사전감사에서 확인한 P0 개발 서버 source exposure와 PostgreSQL 전 인터페이스 노출을 차단한다.
- Root Finding:
  - `SEC-PUBLIC-001`: Vite Development server의 `/src`와 `/@fs`를 공개 인터넷에 노출할 수 있다.
  - `SEC-PUBLIC-002`: 공통 Docker Compose가 PostgreSQL 5432를 모든 host interface에 publish한다.
- 변경·검증 경계: Vite Development/preview listener를 loopback으로 fail-closed 고정하고 `server.fs.allow`를 Frontend와 dependency 경로로 제한한다. 공통 PostgreSQL publish 주소를 IPv4 loopback으로 제한하고 현재 Persistent UAT container를 데이터 보존형으로 재생성해 실제 listener를 확인한다.
- 보존할 불변조건: Persistent UAT volume·DB·aggregate, Entra 설정, 실제 provider 비활성 상태, 기존 5174/5084 source와 인증 계약, migration·seed·data reset 금지.
- 예상 산출물: Frontend/Vite 설정, Compose 설정, 공격 재검사, privacy-safe runtime 전후 projection과 완료 보고.

## 3. 사용자 승인과 게시 경계

- 사용자는 2026-07-29에 공개 배포 사전감사의 OPEN P0 전체 해결을 명시 승인했다.
- local experiment 구현·검증과 P0 runtime 적용을 포함한다.
- 데이터 삭제·초기화·migration, 실제 Teams/Mail 발송, commit, push, PR과 merge는 포함하지 않는다.

## 4. 완료 기준

- `vite --host 0.0.0.0`과 다른 non-loopback listen 요청이 startup 전에 실패한다.
- 정상 HTTPS 5174는 loopback에서 계속 동작한다.
- Vite `/@fs`를 통한 Backend source, DB schema와 Repository instruction 접근이 `403` 또는 미노출 상태다.
- Compose PostgreSQL published host IP가 `127.0.0.1`이다.
- 현재 PostgreSQL listener가 `127.0.0.1:5432`에만 존재하고 wildcard IPv4/IPv6 listener가 없다.
- container volume identity, DB aggregate와 health가 handover 전후 동일하다.
- Backend/Frontend 자동 회귀와 익명 인증 차단을 유지한다.

## 5. Rollback

- Vite 회귀 시 Change 003의 Frontend 설정만 이전 상태로 되돌리고 기존 loopback UAT command로 재기동한다.
- PostgreSQL handover 실패 시 같은 named volume과 기존 image/environment를 사용해 이전 container definition으로 복원한다. 데이터 drop, volume 제거와 신규 초기화는 수행하지 않는다.

## 6. 구현 및 검증 결과

- `frontend/package.json`의 기본 Development command를 `127.0.0.1` listener로 고정했다.
- Vite `configResolved` guard가 Development와 preview의 non-loopback host override를 startup 전에 거절한다.
- Vite file-system allowlist를 Frontend root와 workspace dependency root로 제한했다.
- loopback 후보 root와 Frontend source는 정상 응답하고 Backend source, DB migration과 Repository instruction의 `/@fs` 요청은 모두 `403`이다.
- 공통 Compose PostgreSQL publish address를 `127.0.0.1:${DATABASE_PORT}:5432`로 제한했다.
- 기존 container의 DB 이름·사용자·credential을 값 비출력으로 승계하고 같은 named volume으로 PostgreSQL container만 재생성했다.
- handover 전후 DB aggregate `13/2/0`, volume identity와 restart count `0`이 동일하고 health는 `healthy`다.
- 실제 listener는 wildcard IPv4/IPv6에서 `127.0.0.1:5432` 하나로 바뀌었다.
- Backend live/ready, HTTPS 5174 root는 `200`이고 5174의 Backend `/@fs` source 요청은 `403`이다.
- Frontend lint는 error 0·기존 warning 1, typecheck·build와 unit 142/142를 통과했다.
- migration, seed, data reset과 실제 provider 발송은 실행하지 않았다.

## 7. Finding 상태

- `SEC-PUBLIC-001` P0: `RESOLVED` — Development/preview external binding fail-closed와 Repository `/@fs` 차단을 실제 5174에서 확인했다.
- `SEC-PUBLIC-002` P0: `RESOLVED` — Compose와 실제 runtime PostgreSQL 모두 IPv4 loopback publish만 사용한다.
- 공개 배포 전체 판정은 P1 운영 hosting·security header·rate limit·upload quarantine·Production Entra·backup/monitoring 해소 전까지 `NO_GO`다.
