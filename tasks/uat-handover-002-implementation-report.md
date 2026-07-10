# TASK-UAT-HANDOVER-002 Implementation Report

## 1. 목적

Merged main의 full migration ledger Review-safe runtime을 공식 5190/5092로 통제 전환하고, 개인정보를 재노출하지 않는 자동 검증과 rollback 증빙을 남긴다.

## 2. 배경

PR #27은 full-set ledger와 승인 legacy 정책을 병합했지만 기존 5190/5092 process는 latest-only 코드였다. UAT-VERIFY-001은 공식 Review-safe runtime이 merged main과 같아진 뒤 처음부터 다시 실행해야 한다.

## 3. 해결한 P2

- 공식 5190/5092가 latest-only readiness를 사용하던 runtime drift
- browser 검증이 실제 UAT 텍스트를 출력할 수 있던 검증 절차 P2

## 4. Runtime 기준선

- Development: 5174/5081, 변경 가능 UAT
- Preview: 5185
- Existing Review-safe: 5190/5092, old branch
- Candidate: 5191/5093, full-ledger branch
- Main runtime: detached origin/main, handover 후 5190/5092
- PostgreSQL: Persistent UAT, healthy, restart 0

## 5. Candidate/main tree 정합성

PR #27 branch와 origin/main의 전체 Git tree ID가 동일했다. Runtime worktree HEAD는 `864589b1d06edbe6a00b10fc8ce47e0eec7cc858`이며 origin/main과 같다. Runtime/config/script/migration/dependency diff는 0이다.

## 6. Rollback 정보

Existing frontend/backend의 screen, listener PID, PID file, cwd, command, log, certificate path와 proxy target을 기록했다. Rollback은 새 5190/5092만 종료하고 기존 branch의 canonical Review-safe script를 다시 실행하는 절차다. DB rollback은 없다.

## 7. 개인정보 Finding

첫 browser smoke에서 전체 accessibility/DOM 상태가 도구 출력에 포함됐다. 이 문제는 검증 출력 경계의 P2이며 제품 UI, backend 또는 Persistent UAT 손상은 아니다. Repository tracked/staged/untracked leak과 task-owned 임시 artifact는 0이었다.

## 8. 폐기한 raw DOM 검증 방식

다음 접근을 영구 폐기했다.

- DOM/accessibility snapshot 출력
- `innerText`, `textContent`, `outerHTML`, page content 출력
- screenshot 생성
- response body와 console message 원문 출력
- table/card/heading 텍스트 기반 보고

페이지 내부 고정 문자열 비교가 필요할 때는 boolean만 반환한다.

## 9. Redacted output schema

허용 값은 route alias, 정수, boolean, `ReviewSafe`/ledger 상태/failure code 같은 fixed enum뿐이다. Guard는 허용 key, string enum, 길이, newline, HTML, email, GUID, long token, URL/query pattern을 검사한다. Synthetic email, GUID, HTML, long token, free-form string 5개를 모두 차단했다.

Harness는 `/tmp/emi-qms-redacted-browser-smoke`에만 존재하고 tracked/staged 대상이 아니다.

## 10. Candidate 검증

- Runtime: ReviewSafe, ready 200
- Ledger: CompatibleWithApprovedLegacy, 27/28/1, missing/unknown 0
- Schema compatible/read-only/ledger ready: true
- Workers/providers/migration: false
- Mutation: 4 methods와 override 모두 423
- Desktop/mobile: 각각 11/11 성공
- Console/request/overflow/target-not-found: 0

## 11. Current 종료

Existing listener PID와 PID file, cwd, command, screen이 정확히 일치한 뒤 frontend와 backend만 종료했다. 첫 shell ownership 검사에서 `screen -ls | rg -q`와 `pipefail` 조합이 조기 종료됐으나 실제 process는 변경되지 않았다. 문자열 기반 검사로 수정한 두 번째 절차에서만 종료했다.

## 12. Main 5190/5092 기동

Detached main runtime에서 `scripts/dev-uat-review-start-teams-https.sh`를 실행했다.

| Process | PID | CWD alias | Listener | Session |
| --- | ---: | --- | ---: | --- |
| Backend | 95246 | main-runtime/backend | 5092 | `emi-qms-uat-review-backend` |
| Frontend | 95280 | main-runtime/frontend | 5190 | `emi-qms-uat-review-frontend` |

PID file과 listener PID가 일치하고 runtime HEAD는 origin/main이다.

## 13. Full ledger 27/28/1

새 runtime은 `CompatibleWithApprovedLegacy`, expected 27, actual 28, approved legacy 1을 보고한다. Missing/unknown은 0이며 approved legacy marker와 canonical successor를 유지한다.

## 14. DB read-only

Runtime status의 `databaseReadOnly=true`와 targeted DB integration test 32개 중 관련 session/pool/transaction test가 통과했다. Persistent 업무 table에는 write probe를 실행하지 않았다.

## 15. Mutation/worker/provider 차단

- POST/PUT/PATCH/DELETE/method override: 423
- enabled mutation UI control: 0
- mutation worker startup evidence: 0
- actual provider call evidence: 0
- `.env.notify-local`: 미로드

## 16. Desktop/mobile 결과

Candidate와 새 Main 각각 11개 fixed route를 desktop과 390px에서 확인했다. 모든 record는 output guard를 통과했다. Blank, target-not-found, console/request error와 page overflow는 0이며 banner/API card/migration diagnostic은 정상이다. Main/Candidate 구조 차이는 desktop/mobile 모두 0이다.

## 17. Persistent UAT 전후

Handover 관찰 구간에서 schema_migrations 28, canonical/legacy 27/1, 핵심 table count, delivery status와 maximum timestamp가 모두 동일했다. Container/volume은 동일하고 restart count는 0이다. Development worker에 귀속할 자연 변화도 없었다.

## 18. 테스트 결과

| 검증 | 결과 |
| --- | --- |
| `git diff --check`, actionlint | 성공 |
| frozen pnpm install / audit | 성공 / advisory 0 |
| frontend lint | error 0, 기존 warning 1 |
| frontend typecheck / unit / build | 성공 / 59/59 / 성공 |
| backend Release build | warning/error 0 |
| Review-safe + migration targeted | 32/32 |
| Candidate desktop/mobile | 11/11 + 11/11 |
| Post-cutover desktop/mobile | 11/11 + 11/11 |
| Output negative guard | 5/5 차단 |
| Mutation | 5/5 423 |
| Persistent UAT | 전후 동일 |

PR #27 최신 main CI의 Backend, Frontend, Full-Stack E2E도 모두 성공했다. Runtime code를 변경하지 않은 문서 PR의 표준 CI는 새 head에서 다시 확인한다.

## 19. Secret/PII

Tracked 산출물에는 실제 사용자명, 고객/프로젝트명, 알림 원문, 이메일/UPN, GUID, token, credential, certificate/key, response body 또는 console 원문을 기록하지 않았다. 첫 raw DOM 출력 Finding을 명시적으로 기록하되 노출 텍스트를 재인용하지 않았다.

## 20. 제한사항

- Browser 검증은 정적 selector와 fixed enum contract에 의존하므로 UI 구조 변경 시 harness 갱신이 필요하다.
- Candidate는 사용자 검수와 UAT-VERIFY 완료 전 유지한다.
- 전체 backend 311개와 Full-Stack E2E는 PR #27 main CI 증빙을 사용했다.

## 21. Rollback

1. 새 5190/5092 ownership과 PID를 확인한다.
2. 새 canonical Review-safe session만 종료한다.
3. Existing `uat-002-review-safe` branch의 HTTPS script를 실행한다.
4. Old latest-only status, read-only, 423와 기존 runtime 보존을 확인한다.
5. DB/ledger는 수정하지 않는다.

## 22. 후속 Task

1. 사용자 검수와 본 Draft PR merge
2. UAT-VERIFY-001 최신 main에서 처음부터 재실행
3. TASK-NOTIFY-REL-001
4. TASK-NOTIFY-ESC-001
5. TASK-AUTH-HARDEN-001
6. TASK-GOV-002

## 23. 해결한 업무 문제

공식 Review-safe 주소가 최신 migration 검증 정책을 사용하게 해 감사 기준선의 false-ready 가능성을 제거했다. 검증 증빙도 실제 업무 데이터를 노출하지 않는 형태로 표준화했다.

## 24. 기술적 결정과 검토한 대안

| 결정 | 대안 | 이유 |
| --- | --- | --- |
| Detached main runtime | Task docs branch에서 실행 | 문서 commit과 runtime source 분리 |
| Candidate 선검증 | 바로 5190 교체 | rollback과 사전 비교 근거 확보 |
| Fixed projection | raw DOM/screenshot | 개인정보 재노출 방지 |
| Runtime/DB 다층 gate | UI banner 확인만 | 서버·DB 기준의 fail-closed 보장 |
| Existing branch rollback | ledger 수정 | DB 감사 이력 보존 |

## 25. 시행착오 및 폐기한 접근

- Raw DOM/accessibility 출력은 개인정보 경계를 보장할 수 없어 폐기했다.
- Candidate route curl loop에서 zsh 예약 배열명 `path`를 사용해 command lookup이 깨졌다. 실제 HTTP 요청이 실행되지 않았음을 확인하고 안전한 변수명으로 재실행해 11/11을 확인했다.
- 첫 screen ownership shell은 pipefail/SIGPIPE 조합으로 실제 종료 전에 중단됐다. 모든 PID가 그대로임을 확인한 뒤 문자열 기반 검사로 재실행했다.
- 새 worktree의 첫 `dotnet build --no-restore`는 assets 파일이 없어 실패했다. 정상 restore 후 warning/error 0으로 통과했다.

## 26. 사용자 검수 결과와 남은 항목

상태:

- Checklist 작성됨
- 자동 검증 완료
- 사용자 검수 완료
- PR #28 병합 승인

검수 사용자 A는 2026-07-11 새 5190의 주요 조회 화면, Review-safe banner, 27/28/1 호환 상태, mutation action 비활성화, 5191과의 기능·구조 동등성, 개인정보 안전 보고 정책, SOP와 User manual을 직접 확인하고 PR #28 병합을 승인했다. Mutation API 423, DB read-only, worker/provider 미실행, console/request error 0, 390px overflow 0, Development·Preview·Persistent UAT 보존은 자동 증빙을 함께 사용했다. UAT-VERIFY-001은 merge 뒤 별도 Task에서 처음부터 실행한다.

## 27. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 본 문서 | 작성 완료 |
| SOP | `tasks/uat-handover-002-sop.md` | 작성 완료 |
| User manual | `tasks/uat-handover-002-user-manual.md` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` | 작성 완료 |
| User validation checklist | `tasks/uat-handover-002.md` 22절 | Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 완료 / PR #28 병합 승인 |
