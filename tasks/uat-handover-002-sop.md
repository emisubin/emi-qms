# TASK-UAT-HANDOVER-002 SOP — Privacy-safe Review-safe runtime handover

## 1. Handover 목적

Merged main의 Review-safe backend/frontend를 Persistent UAT의 공식 5092/5190 주소로 안전하게 전환하고 rollback 가능한 증빙을 남긴다.

## 2. Current / Candidate / Main 구분

- Current: 5190/5092, 현재 공식 주소
- Candidate: 5191/5093, 변경 전 비교·rollback 보조
- Main runtime: detached origin/main checkout, 전환 대상
- Development: 5174/5081, 변경 가능한 별도 UAT
- Preview: 5185

## 3. 선행 문서 확인

AGENTS, Task 종료 정책, Roadmap, UAT-002, DB-MIGRATION-001과 해당 tests를 읽는다. UAT-VERIFY 상태와 origin/main SHA도 확인한다.

## 4. Runtime tree 비교

1. Candidate branch와 origin/main의 tree ID를 비교한다.
2. backend/frontend/config/script/runtime file diff를 확인한다.
3. 차이가 있으면 Current를 유지하고 handover를 중단한다.
4. Detached runtime HEAD가 origin/main인지 확인한다.

## 5. 개인정보 안전 browser 검증 규칙

- 실제 route 대신 fixed alias를 사용한다.
- 페이지 내부 값은 boolean/count로만 투영한다.
- migration 상태와 runtime mode는 fixed enum만 허용한다.
- 출력 전 schema allowlist guard를 통과시킨다.
- 실제 text를 디버깅 출력하지 않는다.

## 6. 금지된 browser output

- DOM/accessibility snapshot, page content
- `innerText`, `textContent`, `outerHTML`
- screenshot
- title/heading/table/card 원문
- response/request body
- console message와 stack trace 원문
- cookie/localStorage/sessionStorage
- 사용자·프로젝트·알림·이메일·GUID·credential

## 7. 허용 output schema

- route alias와 HTTP status
- page/structure/banner/diagnostic/API health boolean
- runtime/ledger/failure fixed enum
- migration, mutation control, console/request, overflow count
- blank/target-not-found boolean

Guard는 extra key, free-form string, email, GUID, HTML, long token, query URL과 80자 초과 문자열을 차단한다. 실패 시 `OUTPUT_REDACTION_FAILED` 외 원문을 출력하지 않는다.

## 8. Candidate 검증

1. 5093 live/ready 200을 확인한다.
2. ReviewSafe, Compatible, 27/28/1, missing/unknown 0을 projection한다.
3. DB read-only와 workers/providers/migration disabled를 확인한다.
4. Mutation 4 method와 method override 423을 확인한다.
5. Fixed route 11개를 desktop/390px로 검증한다.
6. Console/request/overflow/enabled mutation이 모두 0인지 확인한다.
7. Current와 비교해 migration diagnostic 외 차이가 0인지 확인한다.

## 9. Rollback 준비

Current의 screen, listener PID, PID file, cwd, command, log, certificate path, proxy target과 시작 script를 기록한다. Credential과 connection string은 기록하지 않는다.

## 10. Ownership 검사

Frontend와 backend 각각 listener 1개, PID file 일치, expected worktree cwd, Vite/API command, canonical screen session을 모두 확인한다. 하나라도 다르면 종료하지 않는다.

## 11. Current 종료

1. Frontend 5190 screen만 정상 종료한다.
2. listener가 남으면 기록 PID/cwd/command가 모두 일치할 때만 SIGTERM한다.
3. Backend 5092에 같은 절차를 적용한다.
4. 5174/5081, 5185, 5191/5093와 PostgreSQL PID/health를 재확인한다.

## 12. Main backend/frontend 기동

Detached main runtime root에서 다음을 실행한다.

```bash
scripts/dev-uat-review-start-teams-https.sh
```

Script는 ReviewSafe, DB session read-only, migration/seed/worker/provider disabled, Backend 5092, HTTPS Frontend 5190 strict port와 ignored localhost certificate path를 강제한다.

## 13. Full ledger 확인

Runtime endpoint를 allowlist projection해 다음을 확인한다.

- CompatibleWithApprovedLegacy
- canonical/live/legacy 27/28/1
- missing/unknown 0
- schema compatible/ledger ready true
- live/ready 200

Version 목록과 response body 전체는 출력하지 않는다.

## 14. Read-only/mutation 차단

- runtime `databaseReadOnly=true`
- POST/PUT/PATCH/DELETE/method override 423
- worker/provider/migration false
- backend log는 금지된 worker/provider pattern의 count만 확인
- 실제 업무 ID나 payload를 사용하지 않음

## 15. Redacted browser matrix

Desktop과 390px에서 fixed alias 11개를 검증한다. 모든 record가 guard를 통과해야 하며 page/structure/banner/diagnostic/API health는 true, blank/target-not-found/error/request/overflow/enabled mutation은 0 또는 false여야 한다. Main/Candidate difference count도 0이어야 한다.

## 16. Persistent UAT 전후

SELECT aggregate만 사용한다.

- ledger/canonical/legacy count
- 핵심 table row count와 delivery status
- active escalation count
- max timestamp 변경 여부
- container/volume 동일 여부와 restart count

실제 row text나 identifier는 출력하지 않는다. 원인 불명 변화가 있으면 P2로 중단한다.

## 17. Rollback

1. 새 Main 5190/5092 ownership을 확인한다.
2. 새 두 session만 종료한다.
3. Existing branch의 Review-safe HTTPS script를 실행한다.
4. live/ready, DB read-only, 423와 기존 환경 보존을 확인한다.
5. ledger row나 DB schema를 변경하지 않는다.

## 18. 장애 대응

| 증상 | 조치 |
| --- | --- |
| Output guard 실패 | 원문 출력 없이 즉시 중단 |
| Candidate/Main 차이 | Current 유지, runtime diff 조사 |
| Ownership 불일치 | process 종료 금지 |
| 새 ready 503 | 새 runtime만 rollback |
| 27/28/1 또는 schema 실패 | DB 수정 없이 rollback |
| read-only/423 실패 | 새 runtime만 rollback, Finding 기록 |
| DB/container 변화 | process 상태 보존 후 원인 조사 |

## 19. 금지사항

- Persistent UAT write/reset, migration/ledger 변경
- Development/Preview/Candidate/PostgreSQL 재시작
- 실제 external provider 발송
- raw DOM/text/screenshot/response/console 원문 출력
- `.env`, token, credential, certificate/key 출력
- unrelated process 종료
- main 직접 push와 force push

## 20. 사용자 검수 체크리스트

- [x] 5190 접속과 Review-safe banner
- [x] Compatible 27/28/1 표시
- [x] 주요 조회/검색/필터/정렬/상세
- [x] mutation action disabled와 423
- [x] DB read-only와 worker/provider disabled
- [x] 5191과 기능·구조 일치
- [x] console error와 390px overflow 없음
- [x] 개인정보 원문이 자동 검증 보고에 없음
- [x] Development/Preview/Persistent UAT 유지
- [x] SOP rollback 절차 재현 가능

현재 상태: **Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #28 병합 승인**

검수 증빙: 검수 사용자 A / 2026-07-11 / Current Review-safe 5190, Candidate 5191 및 본 SOP / 주요 화면·호환 상태·잠금 UX·구조 동등성·개인정보 안전 절차와 rollback 절차 검수 완료 및 PR #28 병합 승인. 서버·DB 방어와 보존 항목은 자동 증빙을 함께 사용했다.

## 21. 변경 이력

| 날짜 | 버전 | 내용 |
| --- | --- | --- |
| 2026-07-10 | 1.0 | Privacy-safe Review-safe runtime handover SOP 최초 작성 |
| 2026-07-11 | 1.1 | 사용자 검수 완료와 PR #28 병합 승인 반영 |
