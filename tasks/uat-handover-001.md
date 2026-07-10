# TASK-UAT-HANDOVER-001: Patched frontend UAT runtime handover

## 1. 목적

PR #24에서 보정한 frontend dependency를 최신 `origin/main`에 고정된 runtime worktree로 인계하고, 기존 Backend 5081과 persistent UAT PostgreSQL을 재시작하지 않은 채 HTTPS UAT 5174 frontend만 교체한다.

## 2. 배경

기존 5174는 Vite 7.3.0을 사용하는 원래 dirty worktree에서 실행 중이었고, 사용자 검수를 마친 5185 Preview는 Vite 7.3.6을 사용했다. Teams manifest가 5174를 가리키므로 PR #24 merge 후 실제 UAT runtime을 통제된 절차로 교체해야 했다.

## 3. 패치 전 runtime

- Frontend: HTTPS 5174, Vite 7.3.0, PID 49563
- Source: 원래 dirty worktree의 `frontend`
- Backend: 5081, PID 49508
- Preview: HTTPS 5185, PID 7674
- PostgreSQL: persistent UAT container, healthy, restart count 0

## 4. 패치 후 runtime

- Runtime commit: `1dcefa1522a2f0c3db785756e043038b7eefb4ac`
- Frontend: HTTPS 5174, Vite 7.3.6, PID 14859
- Source: detached `uat-runtime-main` worktree의 `frontend`
- esbuild 0.28.1, Vitest 4.1.0
- Backend 5081과 PostgreSQL은 기존 process를 유지한다.

PID는 현재 검수 세션의 증빙이며 재기동 후 달라질 수 있다. 소유권 판단에는 PID뿐 아니라 listener port, process command, cwd와 screen session을 함께 사용한다.

## 5. 포함 범위

- 최신 main detached runtime worktree와 문서 전용 Task worktree 분리
- Frozen install, audit, frontend lint/typecheck/unit/build
- Backend runtime/migration diff 확인
- HTTPS 5186 candidate 검증
- 기존 5174 ownership 확인과 frontend-only cutover
- 5174/5185/5186 화면 비교, 390px와 console 검증
- UAT DB 전후 read-only snapshot과 rollback 절차

## 6. 제외 범위

- Backend/worker/PostgreSQL 재시작
- Migration, seed/master upsert, DB reset 또는 데이터 정리
- 실제 TeamsActivity, TeamsChannel, Mail 신규 발송
- Review-safe UAT, notification reliability, 신규 기능 구현
- 원래 dirty worktree와 frontend-sec worktree 정리

## 7. Candidate 검증

최신 main runtime을 HTTPS 5186, screen `emi-qms-uat-main-candidate`로 먼저 실행했다. Root, Teams Activity, admin, notification delivery, manual send, `/api/me`, `/api/projects`, live/ready가 모두 200이었다. Console error, 빈 화면, target-not-found와 390px page-level/Teams overflow는 0건이었다.

5185와 5186의 7개 주요 route에서 heading, navigation, table과 card 구조가 일치했다. Candidate 검증 후 5174 cutover가 성공해 5186 session/listener/PID file을 제거했다.

## 8. Rollback 설계

Cutover 전에 기존 session, PID, cwd, Vite command, 인증서 경로, proxy target과 health를 기록했다. 새 5174가 실패하면 새 frontend session만 종료하고 원래 worktree의 frontend를 같은 HTTPS/5174/proxy 설정으로 재기동한다. Backend와 PostgreSQL은 rollback 대상이 아니다.

이번 cutover는 성공해 rollback을 실행하지 않았다. Rollback 명령 구문과 기존 Vite 7.3.0 dependency 존재 여부는 static 확인했다.

## 9. 5174 cutover

기존 PID 49563이 원래 repository `frontend` cwd의 Vite process이고 screen `emi-qms-uat-frontend`가 소유함을 확인한 뒤 해당 frontend session만 정상 종료했다. 최신 runtime worktree에서 같은 session 이름으로 HTTPS 5174를 시작하고 listener PID 14859를 canonical PID file에 기록했다.

새 runtime은 Vite 7.3.6 startup log, strict port, runtime cwd와 `origin/main` SHA가 일치한다. Backend 5081과 Preview 5185 PID는 변하지 않았다.

## 10. Backend/PostgreSQL 보존

기존 running commit과 최신 main 사이의 `backend/src`, backend dependency/appsettings와 `database/migrations` diff는 0건이었다. 따라서 Backend 5081을 재시작하지 않았다.

PostgreSQL container ID, persistent volume, health와 restart count 0은 전환 전후 동일하다. Migration 실행, seed/master upsert와 data mutation은 수행하지 않았다.

## 11. Teams 검수

자동 검증에서 5174 `/teams/activity`와 알림 상세 route fallback, API/health proxy를 확인했다. 브라우저 밖 TeamsJS 환경에서도 빈 화면 없이 web fallback이 표시된다. 실제 Teams client에서 기존 Activity 알림을 선택하는 검수는 사용자 대기 상태다. 신규 실제 알림은 발송하지 않는다.

## 12. DB 전후 결과

전환 전후 snapshot은 완전히 일치했다.

- Migration 28, latest `0027_notification_access_scope_and_manual_work_items`
- Projects 22, work items 37
- Notifications 89, recipients 163, deliveries 92
- Escalations 2, users 14, departments 12, holidays 6
- Delivery: Disabled 1, DryRunSent 6, Failed 20, Sent 59, Suppressed 6
- Pending 0, active escalation 0
- Delivery max created/updated/sent 시각 동일

## 13. 제한사항

- 실제 Teams client 내부 Activity 클릭과 narrow pane 검수는 사용자 확인이 필요하다.
- Browser 도구는 console error와 expected proxy 응답을 확인했지만 저수준 network event listener를 별도로 계측하지 않았다.
- 5185 Preview와 두 legacy worktree 정리는 사용자 검수와 handover PR merge 이후 별도 수행한다.
- Git automatic maintenance가 unreachable loose object 경고를 표시했지만 Task runtime과 repository content에는 영향이 없으며 임의 정리하지 않았다.

## 14. 후속 Task

1. `TASK-UAT-002`
2. `UAT-VERIFY-001`
3. `TASK-NOTIFY-REL-001`
4. `TASK-NOTIFY-ESC-001`
5. `TASK-AUTH-HARDEN-001`
6. `TASK-GOV-002`

전체 신규 기능 개발 No-Go를 유지한다.

## 15. 5종 산출물 상태

- Implementation report: [TASK-UAT-HANDOVER-001 Implementation Report](uat-handover-001-implementation-report.md), 작성 완료
- SOP: [TASK-UAT-HANDOVER-001 SOP](uat-handover-001-sop.md), 작성 완료
- User manual: [TASK-UAT-HANDOVER-001 User Manual](uat-handover-001-user-manual.md), 작성 완료
- Roadmap update: [Product Roadmap TASK-UAT-HANDOVER-001](../docs/00-product-roadmap.md#task-uat-handover-001-patched-frontend-uat-runtime-handover), 작성 완료
- User validation checklist: 이 문서 16장, Checklist 작성됨 / 자동 검증 완료 / 사용자 검수 대기

## 16. 사용자 검수 체크리스트

상태: `Checklist 작성됨`, `자동 검증 완료`, `사용자 검수 대기`.

- [ ] 5174 메인 화면 정상
- [ ] 프로젝트, 내 업무와 관리자 화면 정상
- [ ] Teams Activity 웹 화면 정상
- [ ] API/User 카드 정상
- [ ] Teams 앱 내부 화면 정상
- [ ] 기존 Activity 알림 클릭 후 상세 이동 정상
- [ ] 5185 Preview와 기능·스타일 차이 없음
- [ ] Console 오류 없음
- [ ] Teams narrow pane overflow 없음
- [ ] SOP를 따라 handover/rollback 절차를 이해할 수 있음
- [ ] User manual이 비개발자에게 이해 가능함
- [ ] Backend와 PostgreSQL이 재시작되지 않았음을 이해함
- [ ] 신규 외부 알림을 발송하지 않았음을 확인함
