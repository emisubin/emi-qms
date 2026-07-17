# TASK-010A 사용자 검수 체크리스트

- 환경: `experiment/task-010a-panel-kitting` local isolated/synthetic runtime
- 데이터: synthetic project·panel·role only
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `대기`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] `0033_panel_kitting_handoff.sql` migration과 operation·panel unique 계약
- [x] readiness, panel 정보, 제조 담당자, project 상태와 scope gate
- [x] 동일 operation+payload 성공 replay와 다른 payload·완료 panel conflict
- [x] panel completion·제조 업무·operation별 참조 알림, 마지막 stage event·키팅 업무 transaction
- [x] Confirm·CloseArrivals readiness hook, panel 취소와 permanent purge lifecycle
- [x] Backend 키팅·migration·purge targeted `3/3`, 전체 `375/375`
- [x] Frontend 전체 unit `77/77`, lint·typecheck·production build
- [x] desktop·390px adaptive 화면, 모바일 horizontal overflow 0, 좌상단 `자재` 통합 menu와 다양한 shape
- [ ] mock·isolated Full-Stack Playwright runtime — terminal 실행 정책 제한으로 미실행

## 사용자 직접 검수

- [ ] [키팅 Desktop 1440](010a-screenshots/01-panel-kitting-desktop-1440.jpg): project rail·readiness·panel grid·완료 상태 확인
- [ ] [키팅 Mobile 390](010a-screenshots/02-panel-kitting-mobile-390.jpg): PC 축소가 아닌 mobile project queue·2열 compact card 확인
- [ ] [선택 action Mobile 390](010a-screenshots/03-panel-kitting-selected-mobile-390.jpg): 선택 수·완료 action의 크기와 정렬 확인
- [ ] [자재 내부 키팅 진입 Desktop](010a-screenshots/05-materials-kitting-entry-desktop-1440.jpg): 독립 전역 메뉴 없이 자재 화면 안의 `패널 키팅` action 확인
- [ ] [좌상단 메뉴 Mobile 390](010a-screenshots/04-panel-kitting-menu-mobile-390.jpg): bottom navigation·독립 `키팅` 메뉴 없음과 `자재` active 상태 확인
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
