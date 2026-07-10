# TASK-UAT-HANDOVER-001 Implementation Report

## 1. 목적

패치된 최신 main frontend를 HTTPS Development UAT 5174에 안전하게 인계하고 Backend, PostgreSQL, UAT data와 외부 delivery 상태를 보존한다.

## 2. 배경

PR #24는 Vite/esbuild/Vitest 취약점을 제거했지만 실제 Teams manifest가 사용하는 5174는 원래 Vite 7.3.0 process였다. Patch 검수용 5185를 유지하면서 최신 main의 정확한 tree를 별도 runtime worktree와 candidate port에서 검증한 뒤 frontend만 교체했다.

## 3. Runtime 기준선

- 기존 5174: PID 49563, Vite 7.3.0, 원래 dirty worktree
- Backend 5081: PID 49508
- Preview 5185: PID 7674, Vite 7.3.6
- PostgreSQL: container ID 동일, healthy, restart count 0
- 원래 worktree: tracked 3개/untracked 1개, staged/stash 0/0

## 4. 최신 main SHA

Runtime detached worktree와 Task branch는 `1dcefa1522a2f0c3db785756e043038b7eefb4ac`에서 생성했다. Runtime worktree HEAD와 `origin/main`이 일치하고 working tree는 clean이다.

## 5. Dependency version

- Node.js 24.18.0
- pnpm 11.8.0
- Vite 7.3.6
- esbuild 0.28.1
- Vitest 4.1.0
- `pnpm audit`: Critical/High/Moderate/Low 전체 0

Frozen install 후 dependency/lockfile 변경은 없었다.

## 6. Backend diff 판단

현재 running Backend source 기준 commit `4e8ca11dc0fde0c4b474230be95df94de69db904`와 최신 main 사이에서 `backend/src`, backend appsettings/dependency와 `database/migrations` 변경은 0건이었다. Backend handover가 필요하지 않은 A 판정으로 5081 PID 49508을 유지했다.

## 7. Candidate 5186

최신 main runtime을 HTTPS 5186, strict port, proxy target 5081로 시작했다. Vite 7.3.6 startup, runtime cwd, listener/PID file과 screen ownership을 확인했다.

Root, Teams Activity, admin 3개 route, API 2개와 health 2개가 200이었고 plain HTTP는 HTTPS 성공으로 오판되지 않았다. 7개 route browser smoke, console error 0, 390px overflow 0과 5185 구조 비교를 통과했다.

## 8. Rollback 준비

기존 session `emi-qms-uat-frontend`, PID 49563, 원래 cwd, Vite command, ignored localhost 인증서 경로와 backend proxy를 기록했다. 원래 Vite 7.3.0 dependency와 frontend-only rollback command 구문을 확인했다.

새 5174 실패 시 새 frontend만 종료하고 원래 worktree에서 같은 session/port/proxy로 재기동하는 절차를 준비했다. 실제 rollback은 필요하지 않았다.

## 9. 5174 cutover

기존 listener, cwd, command, screen session을 모두 확인한 뒤 기존 frontend screen만 종료했다. Backend, Preview, candidate와 PostgreSQL이 유지됨을 확인하고 최신 runtime에서 새 `emi-qms-uat-frontend` session을 시작했다.

새 5174는 root, Teams Activity와 ready 200을 확인한 뒤 PID file을 기록했다. Candidate는 cutover 자동 검증과 DB after snapshot 성공 후 종료했다.

## 10. Process/session/PID

- Patched frontend 5174: PID 14859, screen `emi-qms-uat-frontend`
- Backend 5081: PID 49508, 변경 없음
- Preview 5185: PID 7674, 유지
- Candidate 5186: PID 13918, 검증 후 정상 종료

숫자는 이번 session 증빙이며 운영 판단은 PID, port, cwd, command와 screen을 함께 확인한다.

## 11. API/health

5174에서 root, Teams Activity, admin, notification deliveries, manual send, `/api/me`, `/api/projects`, `/health/live`, `/health/ready`가 모두 200이었다. 알림 상세 SPA fallback도 200이다. Proxy target은 기존 Backend 5081이다.

## 12. Browser/mobile

5174/5185/5186의 7개 route에서 heading, navigation, table/card 구조가 일치했다. 빈 화면, server connection error, target-not-found와 console error는 0건이었다. 390px에서 main, my work, Teams Activity와 관리자 화면의 page-level/Teams overflow는 0건이었다.

## 13. Teams 앱 검수

웹 Teams Activity와 TeamsJS 외부 fallback은 정상이다. 실제 Teams client의 기존 Activity 알림 클릭, 우측 앱 화면과 narrow pane은 사용자 검수 대기다. 신규 actual 알림은 생성·발송하지 않았다.

## 14. DB before/after

전환 전후 PostgreSQL ID, health, restart count 0과 persistent volume이 동일했다. Migration, 9개 핵심 table count, delivery status, Pending/Failed, active escalation과 delivery 최대 시각 snapshot이 완전히 일치했다.

DB/migration/seed/master 변경은 없으며 rollback 대상도 없다.

## 15. 외부 provider 미호출

Frontend GET과 browser read-only navigation만 수행했다. Notification delivery 생성, manual send action, TeamsActivity/TeamsChannel/Mail 호출은 수행하지 않았다. Delivery count/status와 최대 created/updated/sent 시각이 동일한 점을 함께 확인했다.

## 16. 제한사항

- 실제 Teams client 검수는 미실행이며 사용자 checklist에 남긴다.
- 저수준 request-failed listener는 별도 계측하지 않았고 expected API/health 200, console error 0과 UI state로 검증했다.
- Backend/Full-Stack E2E는 PR #24 최신 main CI 성공을 사용하고 이번 frontend-only handover에서 재실행하지 않았다.
- Git unreachable-object maintenance warning은 별도 repository housekeeping 대상이다.

## 17. 후속 Task

`TASK-UAT-002` → `UAT-VERIFY-001` → `TASK-NOTIFY-REL-001` → `TASK-NOTIFY-ESC-001` → `TASK-AUTH-HARDEN-001` → `TASK-GOV-002` 순서를 권장한다.

## 18. 해결한 업무 문제

보안 패치가 merge됐어도 실제 Teams/UAT runtime이 취약한 이전 Vite를 계속 사용하는 간극을 해소했다. Backend와 persistent DB를 건드리지 않고 frontend만 교체해 검수 연속성과 rollback 가능성을 함께 확보했다.

## 19. 기술적 결정과 대안

- 선택: detached main runtime과 Task branch 분리 — 문서 commit이 runtime source를 바꾸지 않게 한다.
- 대안: Task branch에서 직접 실행 — 문서 변경과 runtime SHA가 섞여 폐기했다.
- 선택: 5186 candidate 후 5174 cutover — 즉시 교체보다 사전 검증과 rollback 판단이 명확하다.
- 대안: 전체 UAT startup script 실행 — Backend/migration/seed를 재실행하므로 범위 밖이다.
- 선택: 기존 5185 유지 — 사용자 비교와 rollback 판단에 유용하다.

## 20. 시행착오 및 폐기한 접근

- macOS 기본 `screen`은 `-Logfile` 옵션을 지원하지 않아 process 생성 전 실패했다. `/tmp` 직접 redirection으로 교정했다.
- 첫 cutover orchestration은 zsh test 구문 오류로 ownership gate 전에 중단됐다.
- 두 번째 시도는 `pipefail`과 `grep -q`의 SIGPIPE 때문에 ownership 단계에서 중단됐다. Listener가 그대로임을 확인하고 단계별 Bash 검증으로 교체했다.
- 새 5174 기동 command는 서비스 gate를 통과했지만 startup log가 즉시 flush되지 않아 post-check 종료 코드가 1이었다. PID/cwd/route와 지연 후 Vite 7.3.6 log를 직접 확인해 서비스 성공과 검증식 오류를 분리했다.

실패를 성공으로 숨기지 않았고, 모든 실패는 기존 또는 새 runtime 상태를 다시 확인한 뒤 다음 단계로 진행했다.

## 21. 사용자 검수 결과와 남은 항목

- Checklist 작성됨
- 자동 검증 완료
- 사용자 검수 대기
- 실제 Teams client와 기존 Activity 알림 상세 이동 확인이 남아 있다.
- 5174/5185는 사용자 검수 동안 유지한다.

5종 산출물:

- Implementation report: 이 문서, 작성 완료
- SOP: [TASK-UAT-HANDOVER-001 SOP](uat-handover-001-sop.md), 작성 완료
- User manual: [TASK-UAT-HANDOVER-001 User Manual](uat-handover-001-user-manual.md), 작성 완료
- Roadmap update: [Product Roadmap](../docs/00-product-roadmap.md#task-uat-handover-001-patched-frontend-uat-runtime-handover), 작성 완료
- User validation checklist: [Task 정의 16장](uat-handover-001.md#16-사용자-검수-체크리스트), Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기
