# TASK-010A Change 004 구현 보고서 — 생산관리 2탭 업무 분리

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `895de8d8666bc588c634ac8bdcb9612f26326335`
- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- instructionChainRead: `true`
- fableInvocationCount: `0` — 신규 기능이 아닌 TASK-010A 화면 구조 보정
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- mainMergeApprovalCount: `0/3`
- localCommitCreated: `false`

## 1. 수정 요약

- 생산관리 전역 화면을 `생산계획`과 `제조 투입` 두 탭으로 분리했다.
- 생산계획 탭에는 KPI, 검색, 선택 Excel, 계획 조회·수정, 단계 설정만 유지했다.
- 제조 투입 탭에는 프로젝트 목록과 패널 선택, 키팅·입고 참고 상태, 제조 투입 요청만 유지했다.
- 탭 전환 시 열린 프로젝트를 닫아 서로 다른 업무 상태가 섞이지 않게 했다.
- Mobile은 PC 표 축소가 아니라 기존 KPI·프로젝트·패널 카드 흐름을 탭별로 단순화했다.

## 2. 수정한 파일

- 화면·스타일: `frontend/src/App.tsx`, `frontend/src/styles.css`
- 사용자 검수 runtime launcher: `scripts/dev-experiment-validation-backend.sh`, `scripts/dev-experiment-validation-frontend.sh`
- unit·browser: `frontend/tests/App.test.tsx`, `frontend/e2e/mock-ui/panel-kitting-smoke.spec.ts`, `frontend/e2e/full-stack/manufacturing-work.full-stack.spec.ts`
- 계약·상태: `tasks/010a-change-004.md`, 이 보고서, 사용자 검수 checklist, Product Roadmap, 실험 완료 원장
- Backend·DB·migration·알림 정책은 변경하지 않았다.

## 3. 실행한 테스트

- Frontend typecheck
- Frontend lint
- Frontend 전체 unit
- Frontend production build
- Change 004 mock visual Playwright
- 제조 투입→제조 업무 생성→제조 완료 isolated Full-Stack Playwright
- Desktop 1440px·Mobile 390px screenshot과 horizontal overflow 확인
- `git diff --check`

## 4. 테스트 결과

- typecheck: 통과
- lint: error `0`, 기존 `main.tsx` Fast Refresh warning `1`
- unit: `116/116` 통과
- production build: 통과, 기존 large chunk warning만 존재
- mock visual: `1/1` 통과
- actual Full-Stack: `1/1` 통과
- page-level horizontal overflow: `0`
- Backend test: `N/A` — server·contract·migration 변경 없음. Change 003의 Backend 전체 `416/416` 기준선을 유지한다.
- Full-Stack의 PostgreSQL DB·container·network는 실행별로 격리 생성하고 종료 후 삭제했다.

## 5. Frontend URL

- 사용자 검수 전용 Frontend: `http://127.0.0.1:42982`
- 기존 42981 runtime은 종료·재시작하지 않고 보존했다.
- 42982는 현재 experiment worktree source를 제공하고 41165 Backend만 바라본다.

## 6. Backend URL

- 사용자 검수 전용 Backend: `http://127.0.0.1:41165`
- 기존 41164 runtime은 종료 권한 경계 때문에 보존했지만 연결 DB가 없어 검수 URL로 사용하지 않는다.
- 별도 DB `emi_qms_experiment_validation_41164`에 migration `51/51`, latest `0051`을 적용하고 개발용 계정 seed를 완료했다.
- 외부 Teams·메일·Activity provider와 background mutation worker는 비활성화했다.

## 7. 수동 검수 체크리스트

- [ ] 생산계획 탭에 제조 투입 패널이 보이지 않는지 확인
- [ ] 제조 투입 탭에 KPI·Excel·생산계획 수정이 보이지 않는지 확인
- [ ] 두 탭을 오갈 때 펼친 프로젝트가 닫히는지 확인
- [ ] Mobile 390px에서 두 탭과 패널 선택이 가로 넘침 없이 동작하는지 확인
- [ ] 사용자 검수는 마지막 일괄 검수에서 진행

## 8. 미커밋 변경사항

- Change 004 source·test·문서·screenshot은 현재 실험 worktree에만 있다.
- 같은 worktree의 TASK-WORKFLOW-CONTINUITY-001 Change 005와 TASK-010A Change 003 미커밋 변경을 보존했다.
- 사용자 검수용 42982/41165 runtime과 별도 experiment DB가 실행 중이다.
- commit은 사용자의 별도 요청 전 수행하지 않았다.

## 9. 남은 문제

- Open P0/P1/P2: `0/0/0`
- 마지막 panel 키팅 완료 취소 시 workflow stage 표시 정책은 기존 P3 후속으로 남는다.
- 실제 사용자 검수는 `BATCHED_FINAL` 정책에 따라 마지막에 일괄 진행한다.

## 10. 게시 가능 여부

- local experiment 결과: 게시 후보 가능
- commit·push·PR: 미실행
- 대표 repo·GitHub `main`: 미변경
- Persistent UAT·실제 provider: 미변경
- local user-validation runtime: 42982/41165 준비 완료
- main merge 승인: `0/3`
- rollback: Frontend 탭 분리 diff만 되돌리면 되며 DB rollback은 없다.

## 화면 증빙

- [생산계획 Desktop 1440](010a-change-004-screenshots/01-production-planning-tab-desktop-1440.jpg)
- [제조 투입 Desktop 1440](010a-change-004-screenshots/02-manufacturing-release-tab-desktop-1440.jpg)
- [제조 투입 성공 Desktop 1440](010a-change-004-screenshots/03-manufacturing-release-success-desktop-1440.jpg)
- [생산계획 Mobile 390](010a-change-004-screenshots/04-production-planning-tab-mobile-390.jpg)
- [제조 투입 Mobile 390](010a-change-004-screenshots/05-manufacturing-release-tab-mobile-390.jpg)

| 종료 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP·User manual | Change 003 유지 | `tasks/010a-change-003-implementation-report.md` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 자동 검증 완료, 사용자 검수 대기 | `tasks/010a-user-validation-checklist.md` |
