# TASK-UAT-001 Change 001 — HTTPS-only Development UAT와 불필요 포트 정리

## 1. Task Identity Gate

- proposedTaskId: `TASK-UAT-001 Change 001`
- taskType: `UAT_RUNTIME`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-GOV-HISTORY-REWRITE-001`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

History Support 대기 중 병렬 실행은 사용자가 명시 승인했다. 기존 history P2, public 재개와 Finding gate 상태는 변경하지 않는다.

## 2. 증상과 기대 동작

Development frontend 5174가 HTTP로 실행돼 Teams Activity와 HTTPS 알림 검수 주소가 사라졌고, 과거 격리 검증용 PostgreSQL container 세 개가 동적 host port를 계속 점유했다.

현재 운영 계약은 다음과 같다.

- Development UAT는 `https://localhost:5174` 하나로 로그인, 일반 기능, 알림과 Teams Activity를 제공한다.
- HTTP 5174는 운영하지 않으며 HTTPS와 동시에 기동하지 않는다.
- 5174는 same-origin `/api`·`/health` proxy를 통해 기존 Backend 5081을 사용한다.
- Design experiment 5176, Review-safe 5190/5092, Backend 5081과 Persistent UAT PostgreSQL 5432를 보존한다.
- 비영구 격리 DB port 51061, 51642, 55433과 단독 소유 network만 정리한다.

## 3. 변경 경계와 불변조건

- 최초 단계는 Frontend-only protocol handover이며 Backend, API, DB schema, migration, provider 설정과 제품 코드를 변경하지 않는다.
- Entra 환경값, tenant/client 식별자, token과 certificate private key를 출력하거나 tracked 문서에 기록하지 않는다.
- 최초 Frontend handover에서는 Backend 5081과 Review-safe 5092, frontend 5190, design preview 5176을 종료·재시작하지 않는다.
- Persistent UAT container, named volume과 restart count를 보존한다.
- branch/worktree 구조, commit, push와 PR을 변경하지 않는다.
- 실제 Microsoft 로그인, 저장·수정과 실제 외부 알림 발송은 최초 자동 검증에서 수행하지 않는다.

후속 사용자 검수에서 Microsoft 365 로그인을 확인했다. 사용자는 Backend 5081의 Notification Delivery Worker 활성화·재시작과 당시 Pending Teams Activity의 provider 처리를 승인한 뒤, 기존 `TeamsActivityDisabled` terminal 2건을 audit로 보존하면서 Teams Activity channel 활성화와 5174의 신규 ManualTest Teams Activity 1건 actual 발송을 추가 승인했다. 이 후속 승인에도 Delivery Worker만 활성, Escalation·Purge 비활성, 다른 runtime·Persistent DB·volume 보존과 provider 설정 파일 미변경을 적용했다.

## 4. 실행 결과

- HTTPS 5186 candidate에서 trusted certificate, root, notification, Teams Activity, live/ready와 API proxy를 검증한 뒤 candidate를 종료했다.
- 정확한 기존 5174 screen, Vite command와 repository frontend cwd를 확인한 뒤 HTTP frontend만 정상 종료했다.
- 기존 Entra 환경의 필수 key를 원문 출력 없이 승계하고 redirect URI를 HTTPS 5174로, API를 same-origin proxy로 고정해 HTTPS 5174를 기동했다.
- HTTPS root, notification, Teams Activity, live/ready와 runtime API는 200이고 HTTP 5174는 실패한다.
- 최초 Frontend handover에서는 Backend 5081과 PostgreSQL을 재시작하지 않았고 Persistent UAT health와 restart count를 유지했다.
- 후속 승인으로 Backend 5081만 재기동해 Notification Delivery Worker를 활성화했다. runtime은 `Development/ready`, delivery worker enabled, Escalation·Purge disabled, external provider capability enabled다.
- 최초 worker handover 전 Teams Activity는 Pending 1건, due 1건, attempt 0건이고 다른 Pending channel은 0건이었다. 해당 건은 `TeamsActivityDisabled`로 terminal 종료됐고, 이후 같은 원인의 terminal 2건은 상태 변경 없이 audit로 보존했다.
- 추가 승인에 따라 Backend 5081만 다시 재기동해 Teams Activity channel을 actual mode로 활성화했다. Graph credential과 Teams app 설정은 local ignored dotenv에서 값 출력 없이 전달했으며 설정 파일 자체는 변경하지 않았다.
- 로그인된 5174 수동 발송 화면에서 Mail을 제외하고 Teams Activity 수신자 1명만 선택해 신규 `ManualTest` delivery 1건을 생성했다. 신규 delivery 행은 정확히 1건이다.
- 첫 두 시도는 최초 channel restart에서 Graph credential이 전달되지 않아 `TeamsActivityGraphConfigMissing`으로 retry 예약됐다. 5081만 보정 재기동한 뒤 같은 delivery의 세 번째 시도가 `Sent`로 완료됐고 추가 delivery는 생성하지 않았다.
- 기존 `TeamsActivityDisabled` terminal 2건은 그대로 보존됐고 최종 Pending/Processing은 0/0이다. `Sent`는 Microsoft Graph가 actual 요청을 수락한 provider 결과이며 사용자가 Teams client Activity Feed의 실제 표시까지 확인했다.
- 승인된 격리 DB container 세 개는 mount 0, 각 network container count 1을 확인한 뒤 정상 종료·제거했다. 단독 network 세 개도 제거했고 obsolete container/network count는 0이다.

## 5. Design experiment 동기화 원칙

Port가 source를 동기화하지 않는다. 5176은 별도 worktree이므로 main 변경을 자동으로 받지 않는다.

1. 별도 승인으로 현재 디자인 변경을 독립 commit에 고정하고 worktree를 clean 상태로 만든다.
2. main 변경이 게시될 때 디자인 branch에서 `origin/main`을 merge한다.
3. Git은 main의 비충돌 변경을 가져오고 디자인 commit을 유지한다.
4. 공통 `App.tsx`, `main.tsx`, `styles.css`의 같은 줄이 바뀌면 main 기능 변경과 디자인 shell을 함께 보존하도록 수동 해결한다.
5. 장기적으로 auth shell component와 style을 전용 파일로 분리해 공통 기능 코드와의 충돌 범위를 줄인다.

현재 디자인 worktree는 dirty하고 미게시 상태이므로 commit, merge, rebase와 자동 동기화를 수행하지 않았다.

## 6. 자동 검증

- trusted HTTPS: root, notifications, Teams Activity, health live/ready, runtime API 200
- protocol mismatch: HTTP 5174 실패
- browser: desktop/390px × root/notifications/Teams Activity 6/6, HTTPS·root·login action 확인, console/request error 0, horizontal overflow 0
- runtime: Development 5081 ready/delivery worker enabled/Escalation·Purge disabled, Review-safe 5092 ready, 5176/5190 200
- delivery: 기존 terminal audit `Disabled 2/TeamsActivityDisabled` 보존, 신규 ManualTest delivery `1`, attempt lineage `RetryScheduled/RetryScheduled/Sent`, 최종 `Sent/attempt 3/error 없음`, 전체 Pending/Processing `0/0`
- persistence: PostgreSQL running/healthy, restart count 0, named volume 보존
- cleanup: obsolete container 0, obsolete network 0, candidate 5186 listener 0
- Git: 제품 source, Backend, migration, dependency와 lockfile 변경 0

## 7. 사용자 검수와 게시 상태

상태: `자동 검증 완료`, `사용자 검수 완료`, `Microsoft Graph actual 발송 검수 완료`, `Teams client 수신 검수 완료`, `PR #48 squash merge 승인`.

- [x] `https://localhost:5174`에서 실제 Microsoft 365 로그인
- [x] 로그인 상태 유지와 재인증
- [x] 알림 목록과 Teams Activity 기존 항목 조회
- [x] Backend 5081 Notification Delivery Worker 활성 handover
- [x] 기존 `TeamsActivityDisabled` terminal 2건 audit 보존
- [x] Teams Activity channel actual 활성화와 신규 ManualTest 1건 생성
- [x] Microsoft Graph actual 처리 — 신규 delivery `Sent`
- [x] Teams client Activity Feed에서 신규 알림 표시 확인
- [x] `http://127.0.0.1:5176` 디자인 preview 보존
- [x] `https://localhost:5190` Review-safe 보존

구현·검수 단계에서는 Git 게시를 수행하지 않았다. 이후 사용자 승인에 따라 승인된 문서 6개만 commit·push하고 Draft PR #48을 생성했다. 사용자가 잔여 검수 2건을 모두 확인하고 squash merge를 승인했으며, 최종 게시 상태는 PR을 source of truth로 확인한다.

## 8. 게시 격리

- purpose: 현재 canonical clone branch의 범위 밖 governance commit을 게시하지 않고 Change 001 문서 6개만 `origin/main` 기준으로 검증·게시
- owner: `TASK-UAT-001 Change 001` 게시·독립 검증 session
- base: `origin/main` `9f62cf4`
- expected end: 독립 검증 통과와 승인된 PR merge 완료 시점
- cleanup boundary: merge 승인은 branch/worktree 정리 승인으로 확대하지 않으며, 게시 worktree는 별도 정리 승인 전 보존
