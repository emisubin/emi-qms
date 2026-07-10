# TASK-UAT-HANDOVER-001 SOP

## 1. 문서 목적

최신 main frontend를 HTTPS UAT 5174로 인계할 때 Backend와 persistent PostgreSQL을 보존하고, 실패 시 이전 frontend로 복구하는 표준 절차를 제공한다.

## 2. 적용 범위

Frontend-only dependency/runtime handover에 적용한다. Backend, worker, migration, seed와 DB reset이 필요한 변경에는 이 절차만으로 진행하지 않는다.

## 3. Handover 전 확인

1. `origin/main` SHA와 승인된 patch 범위를 확인한다.
2. 원래 worktree와 모든 별도 worktree의 status를 기록한다.
3. 5174/5185/5081 listener PID, cwd, command와 screen session을 기록한다.
4. PostgreSQL ID, health, restart count, volume과 UAT DB snapshot을 기록한다.
5. Running backend commit과 main의 backend/migration diff가 0인지 확인한다.
6. Backend 차이가 있으면 frontend cutover를 중단하고 별도 승인을 요청한다.

## 4. Candidate 서버 시작

최신 main detached worktree에서 frozen install과 audit를 완료한다. Ignored localhost 인증서의 경로만 사용하고 내용을 출력하지 않는다.

Candidate는 다음 계약을 사용한다.

- HTTPS 5186, host 127.0.0.1, strict port
- `VITE_DEV_SERVER_PORT=5186`
- `VITE_DEV_PROXY_TARGET=http://127.0.0.1:5081`
- `VITE_DEV_HTTPS=true`
- 인증서/key path env 설정
- `.env.notify-local` 미로드
- screen `emi-qms-uat-main-candidate`
- PID file `/tmp/emi-qms-uat-main-candidate.pid`

## 5. Candidate 검증

Root, Teams Activity, admin, delivery monitor, manual send, `/api/me`, `/api/projects`, live/ready를 GET으로 확인한다. Browser에서 main, my work, notifications, Teams Activity와 admin route를 확인하고 console error와 390px overflow를 검사한다.

5185 Preview와 heading, navigation, table/card 구조를 비교한다. 저장·수정·삭제·발송 action을 실행하지 않는다.

## 6. Rollback 정보 기록

다음을 cutover 전에 기록한다.

- 기존 frontend screen ref와 listener PID
- process cwd와 Vite command
- 기존 worktree 경로와 설치된 Vite version
- backend proxy target
- ignored certificate/key path
- canonical frontend PID file
- root, Teams Activity와 backend ready 결과

Rollback command는 기존 worktree에서 HTTPS 5174, strict port와 동일 proxy를 사용해 frontend만 재기동해야 한다.

## 7. 기존 frontend ownership 확인

Listener PID만 신뢰하지 않는다. 다음이 모두 일치해야 한다.

1. 5174 listener PID
2. repository frontend cwd
3. Vite command와 `--port 5174 --strictPort`
4. `emi-qms-uat-frontend` screen session
5. Backend PID와 다른 process

하나라도 다르면 process를 종료하지 말고 중단한다.

## 8. 기존 frontend 종료

소유권이 확인된 기존 frontend screen에 정상 종료를 요청한다. 5174 listener와 해당 screen이 사라졌는지 확인한다. Backend 5081, Preview 5185와 PostgreSQL이 유지되지 않으면 새 frontend를 시작하지 않는다.

## 9. 최신 main frontend 5174 기동

Detached runtime worktree에서 다음 계약으로 실행한다.

- screen `emi-qms-uat-frontend`
- HTTPS 5174, host 127.0.0.1, strict port
- proxy target 5081
- trusted localhost certificate path
- canonical PID file `/tmp/emi-qms-dev-uat-frontend.pid`
- startup log `/tmp/emi-qms-uat-main-frontend.log`

Listener PID, PID file, runtime cwd, Vite startup version과 runtime SHA를 확인한다.

## 10. Cutover 검증

5174 root, Teams Activity, admin, delivery monitor, manual send, API/health를 확인한다. Plain HTTP가 HTTPS 성공으로 판정되지 않아야 한다. 5174/5185/5186의 주요 화면 구조, console과 390px overflow를 비교한다.

## 11. Teams 앱 검수

Teams manifest가 5174를 가리키는지 확인하고 기존 Activity 알림을 선택한다. 우측 EMI 앱, 알림 상세, 로그인/권한 안내와 narrow pane을 확인한다. 신규 actual 알림은 별도 승인 없이는 생성하지 않는다.

## 12. DB 보호 확인

전환 전후 같은 read-only query를 실행해 container ID/health/restart, migration, 핵심 table count, delivery status/최대 시각과 active escalation을 비교한다. Frontend-only handover에서 차이가 있으면 worker 자연 변경과 handover 영향을 분리해 조사한다.

## 13. Rollback 절차

다음 조건이면 rollback한다: 새 5174 startup 실패, API/health 실패, Teams Activity 빈 화면, fatal console error, 주요 route 회귀 또는 Backend/PostgreSQL 영향.

1. 새 5174 frontend의 PID/cwd/session 소유권을 확인한다.
2. 새 frontend session만 종료한다.
3. 기존 worktree에서 동일 HTTPS/port/proxy 설정으로 frontend만 재기동한다.
4. 새 PID, root/Teams/API/health와 Backend/PostgreSQL 보존을 확인한다.
5. Finding과 rollback 원인을 기록한다.

Backend와 PostgreSQL을 rollback 명목으로 재시작하지 않는다.

## 14. Candidate/Preview 정리

5174 자동 검증과 DB after snapshot이 성공한 경우에만 5186 candidate를 종료하고 PID file을 제거한다. 5185 Preview는 사용자 검수와 PR merge가 끝날 때까지 유지한다.

## 15. 장애 대응

- 5174 점유: unrelated process를 종료하지 말고 PID/cwd/command를 보고한다.
- TLS protocol error: HTTPS env와 인증서 파일 존재·권한·localhost 유효성을 확인한다.
- API proxy failure: Backend 5081 health를 GET으로 확인하되 재시작하지 않는다.
- DB 차이: write action을 중단하고 delivery/workflow 자연 변경 여부를 조사한다.
- Teams 빈 화면: web route, TeamsJS fallback, 로그인/권한 안내와 console을 확인한다.

## 16. 금지사항

- Backend/worker/PostgreSQL restart
- UAT DB drop/truncate/reset, volume 삭제
- Migration, seed/master upsert
- 실제 external notification 신규 발송
- 원래 dirty worktree 수정·stash·reset
- `.env` 또는 인증서/key 원문 출력
- 소유권이 불명확한 process 종료

## 17. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 완료`.

- [x] 5174 main/project/work/admin 정상
- [x] Teams Activity 웹 화면 정상
- [x] Teams client 기존 Activity 알림 상세 정상
- [x] 5185 대비 기능·style 회귀 없음
- [x] Console error와 narrow overflow 없음
- [x] Backend/PostgreSQL 미재시작 확인
- [x] 신규 external notification 미발송 확인
- [x] Rollback 절차를 따라갈 수 있음

## 18. 변경 이력

- 2026-07-10: 최신 main Vite 7.3.6 frontend를 5174에 controlled handover하고 절차 최초 작성
- 2026-07-10: Teams client·화면·문서 검수 완료와 PR #25 병합 승인 반영
