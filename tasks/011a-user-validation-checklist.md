# TASK-011A 사용자 검수 체크리스트

- 환경: `experiment/task-011a-manufacturing-work` local isolated/synthetic runtime
- 데이터: synthetic project·panel·role only
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `대기`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] `0034_manufacturing_execution.sql` fresh migration, partial unique·FK·check constraint
- [x] project scope·권한, active panel·키팅·업무 prerequisite
- [x] start·4단계 순차 check·중단·Pending 종결 전 재개 차단·재개·완료
- [x] generic 내 업무 우회 차단과 operation fingerprint replay·stale version conflict
- [x] Panel target 긴급 Pending, 필수 조치 부서와 같은 부서 담당자 검증
- [x] panel별 LQC 업무와 마지막 panel project stage exactly-once
- [x] cancellation·permanent purge, 기존 Pending·자재·workflow 전체 회귀
- [x] Backend 전체 `376/376`, Frontend 전체 `79/79`, lint·typecheck·production build
- [x] disposable Full-Stack E2E `1/1`, isolated DB/container 자동 cleanup
- [x] desktop·390px adaptive 화면, horizontal overflow 0, mobile bottom navigation 0

## 사용자 직접 검수

- [ ] [제조 Queue Desktop 1440](011a-screenshots/01-manufacturing-queue-desktop-1440.png): project rail·상태 count·panel strip·timeline 확인
- [ ] [제조 진행 Mobile 390](011a-screenshots/02-manufacturing-active-mobile-390.png): PC 축소가 아닌 mobile queue·2×2 단계·action 확인
- [ ] [제조 중단 입력 Mobile 390](011a-screenshots/03-manufacturing-stop-sheet-mobile-390.png): 사유·설명·조치 부서·담당자 입력과 touch target 확인
- [ ] [제조 완료 Mobile 390](011a-screenshots/04-manufacturing-completed-mobile-390.png): 원형 완료 상태·4/4 단계·execution log 확인
- [ ] 제조 담당 권한에서는 시작·체크·중단·재개·완료가 보이고 조회 역할에는 action이 없는지 확인
- [ ] 내 업무의 panel 제조 업무가 `제조 화면에서 진행`으로 이동하고 generic 완료가 없는지 확인
- [ ] 이번 실험 결과를 유지·수정·폐기 중 무엇으로 처리할지 결정

## 게시·운영 Gate

- [x] local experiment commit 완료
- [ ] push 승인 — 현재 없음
- [ ] PR 승인 — 현재 없음
- [ ] Persistent UAT migration·runtime handover 승인 — 현재 없음
- [ ] main merge 1차 승인
- [ ] main merge 2차 승인
- [ ] main merge 3차 승인

사용자 직접 검수와 세 번의 merge 승인은 서로 다른 Gate다. 자동 검증 완료는 사용자 검수 또는 게시·merge 승인을 대신하지 않는다.
