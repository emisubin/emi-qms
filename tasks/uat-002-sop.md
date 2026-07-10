# TASK-UAT-002 SOP — Review-safe UAT 운영

## 1. 문서 목적

운영자와 개발자가 persistent UAT를 변경하지 않고 Review-safe UAT를 시작·검증·종료하는 표준 절차다.

## 2. Development UAT와 차이

- Development: HTTPS 5174 / Backend 5081, 저장·수정·worker·승인된 외부 delivery 검수
- Review-safe: HTTPS 5190 / Backend 5092, 조회·검색·필터·상세 검수

Review-safe는 Development UAT를 대체하지 않으며 두 backend를 동시에 사용할 수 있다.

## 3. 사전 확인

1. repository branch/HEAD/working tree를 확인한다.
2. 5092와 5190 listener가 없는지 확인한다.
3. 5174, 5185, 5081 PID를 기록하고 종료하지 않는다.
4. persistent PostgreSQL container가 healthy인지 확인한다.
5. UAT DB가 canonical 이름이고 latest migration이 repository expected와 같은지 읽기 쿼리로 확인한다.
6. ignored localhost certificate/key가 존재하는지만 확인한다. 내용은 출력하지 않는다.
7. `.env.notify-local`을 source/eval/load하지 않는다.

다른 process가 5092/5190을 점유하면 종료하지 말고 ownership과 포트를 보고하고 중단한다.

## 4. HTTPS Review-safe 시작

Repository root에서 실행한다.

```bash
scripts/dev-uat-review-start-teams-https.sh
```

Script는 다음을 강제한다.

- persistent PostgreSQL이 이미 healthy여야 함
- DB 생성·migration·seed·upsert 미실행
- Review-safe enabled, Development/Dev auth
- worker/provider disabled 및 actual client 미등록
- database session read-only
- backend 5092, frontend 5190 strict port
- HTTPS certificate는 primary worktree의 ignored file path만 참조
- 별도 screen/session과 PID/log 사용

## 5. HTTP Review-safe 시작

HTTP 검수가 필요한 경우에만 다음을 사용한다.

```bash
scripts/dev-uat-review-start.sh
```

Backend는 동일한 5092를 사용하고 frontend는 `http://localhost:5190`이다. HTTP/HTTPS는 같은 port를 동시에 사용할 수 없다. 기존 Review-safe session을 ownership 확인 없이 자동 종료하지 않는다.

## 6. Runtime mode 확인

```bash
curl -fsS http://127.0.0.1:5092/api/runtime-mode
```

다음 의미만 확인한다.

- mode: ReviewSafe
- reviewSafe: true
- mutationAllowed/backgroundWorkersEnabled/externalProvidersEnabled/migrationExecutionEnabled: false
- databaseReadOnly: true
- ready: true
- expectedMigration과 actualMigration 일치

응답에 connection string, password, token이 있으면 P0/P1 gate로 즉시 중단한다.

## 7. DB read-only 확인

Runtime endpoint와 `/health/ready`에서 read-only true를 확인한다. 자동 integration test는 별도 임시 DB에서 다음을 검증한다.

- `SHOW transaction_read_only` = on
- SELECT 성공
- INSERT/UPDATE/DELETE 실패
- explicit transaction 실패
- pool 재사용 후에도 on

실제 업무 table에 synthetic write를 실행하지 않는다. 별도 DB role/schema를 현장에서 만들지 않는다.

## 8. Worker/provider 비활성화 확인

1. Review backend log에 delivery/escalation/purge worker startup이 없는지 확인한다.
2. runtime mode가 worker/provider false인지 확인한다.
3. provider credential 설정을 출력하지 않는다.
4. notification delivery status/count를 시작 전후 비교한다.
5. 두 notification interval 이상 관찰한다.

Development 5081 worker의 자연 변화가 있으면 timestamp/source를 구분한다. 원인 불명 변화는 성공으로 기록하지 않는다.

## 9. 조회 기능 확인

- `/`, `/projects`, `/my-work`, `/notifications`
- `/teams/activity`
- `/admin`, `/admin/users`, `/admin/calendar/holidays`
- `/admin/system/notification-deliveries`
- `/admin/system/work-item-escalations`
- `/admin/system/send-notification`

Banner, API/User 카드, 검색/필터/정렬/상세, empty/error 상태를 확인한다. 저장·삭제·발송은 실행하지 않는다.

## 10. Mutation 차단 확인

대표 test endpoint에 placeholder body로 POST/PUT/PATCH/DELETE를 보내 423과 `UatReviewReadOnly`를 확인한다. query string, JSON content type, method override도 우회되지 않아야 한다. 실제 업무 ID, 개인정보 또는 실제 발송 payload를 사용하지 않는다.

Frontend에서는 mutation control이 disabled이고 가까운 title/안내가 표시돼야 한다. 서버 차단이 최종 기준이다.

## 11. Schema mismatch 대응

Expected/actual migration이 다르면:

1. live 200 여부와 ready 503을 구분한다.
2. 자동 migration하지 않는다.
3. Review-safe를 데이터 검수 완료로 사용하지 않는다.
4. Development UAT의 정상 migration 절차가 필요한지 별도 승인받는다.
5. mismatch 원인과 version만 기록하고 credential은 기록하지 않는다.

## 12. Port/process ownership

Screen:

- `emi-qms-uat-review-backend`
- `emi-qms-uat-review-frontend`

PID files:

- `/tmp/emi-qms-uat-review-backend.pid`
- `/tmp/emi-qms-uat-review-frontend.pid`

PID file만 종료 근거로 사용하지 않는다. listener PID, cwd가 Task worktree 내부인지, command가 API/Vite인지 모두 확인한다. 5174/5185/5081과 다른 PID인지 확인한다.

## 13. 서버 종료

사용자 승인 또는 명시적 운영 지시가 있을 때만 Review-safe session을 종료한다.

1. 5092/5190 listener ownership을 재확인한다.
2. Review-safe screen 두 개만 정상 종료한다.
3. Screen 종료 뒤 5092/5190 listener가 실제로 사라졌는지 확인한다.
4. Child listener가 남으면 PID file만 믿지 말고 PID/cwd/command를 다시 확인한다. Review-safe worktree 소유가 모두 일치할 때만 해당 child에 SIGTERM을 보낸다. 불일치하면 종료하지 않는다.
5. 5174/5185/5081과 PostgreSQL은 유지한다.
6. listener 제거와 기존 runtime health를 확인한다.
7. PID/log 정리는 내용 노출 없이 Review-safe file만 대상으로 한다.

## 14. 장애 대응

- port occupied: 다른 process를 종료하지 않고 중단
- ready schema mismatch: 자동 복구 금지
- database_not_read_only: P2 이상으로 중단, 조회 검수 금지
- banner 없음/runtime lookup 실패: frontend는 mutation fail-closed인지 확인하고 원인 수정 전 검수 중단
- worker/provider log: 즉시 Review-safe server만 중단하고 delivery/DB 영향 조사
- 5174/5081/PostgreSQL 영향: 재시작하지 말고 PID/container 상태를 보존해 보고

## 15. Rollback

Review-safe는 별도 port/session이므로 rollback은 Review-safe 5092/5190만 종료하는 것이다. Development 5174/5081은 전환 대상이 아니다. 코드 rollback은 merge 전 branch 폐기 또는 commit revert/forward-fix로 처리하며 DB rollback은 없다.

## 16. 보안 주의사항

- `.env`, `.env.notify-local`, certificate/key 내용을 출력하지 않는다.
- Authorization header, token, client secret, webhook, SMTP password를 log/document에 기록하지 않는다.
- 실제 사용자 이름/회사 이메일/UPN 대신 역할명 또는 검수 사용자 A/B를 사용한다.
- 실제 provider smoke와 delivery row 생성을 하지 않는다.

## 17. 금지사항

- UAT DB drop/truncate/reset
- persistent volume/container restart
- migration/seed/master upsert
- Development worker/server 종료
- 실제 Teams/Mail/Channel 발송
- hard delete, test data 생성
- unrelated process 종료

## 18. 사용자 검수 체크리스트

- [ ] 5190 접속과 banner 표시
- [ ] 조회·검색·필터·정렬·상세 이동
- [ ] 저장/수정/삭제/복구/상태 action disabled 및 이유
- [ ] 읽음/발송/retry/acknowledge/dismiss disabled
- [ ] 직접 mutation API 423
- [ ] DB read-only와 schema ready
- [ ] migration/seed/worker/provider 미실행
- [ ] Development 5174/5081과 Preview 5185 유지
- [ ] console error와 390px overflow 없음
- [ ] 본 SOP 순서로 재현 가능

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기**

## 19. 변경 이력

| 날짜 | 버전 | 내용 |
|---|---|---|
| 2026-07-10 | 1.0 | TASK-UAT-002 Review-safe UAT 최초 작성 |
