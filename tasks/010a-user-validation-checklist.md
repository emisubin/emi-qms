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
- [x] mock UI와 isolated Full-Stack Playwright — Change 003 기준 생산관리 제조 투입·제조 실행·선택형 키팅 `2/2`, 전용 PostgreSQL tmpfs 정리 완료

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

## Change 003 — 선택형 키팅·생산관리 제조 투입 요청

### 자동 검증

- [x] 키팅 완료 전·후·미실시 패널을 생산관리 담당자가 제조 투입 요청 가능
- [x] 제조 정·부 담당자에게 패널별 내 업무와 인앱 알림 생성
- [x] 동일 operation 재시도 시 업무·알림 중복 생성 방지
- [x] 제조 담당자가 키팅 미보고 패널의 요청된 업무를 시작 가능
- [x] 키팅 완료 알림 자체는 제조 업무를 만들지 않음
- [x] 기존 미완료 키팅 내 업무 취소 migration과 영구 삭제 lifecycle
- [x] Desktop 1440px·Mobile 390px adaptive 화면과 선택 action 확인

### 사용자 직접 검수 — 마지막 일괄 대기

- [ ] [생산관리 제조 투입 요청 Desktop](010a-change-003-screenshots/01-manufacturing-release-desktop-1440.jpg): 키팅·입고 참고 정보와 선택 가능한 패널 확인
- [ ] [요청 완료 Desktop](010a-change-003-screenshots/02-manufacturing-release-success-desktop-1440.jpg): 선택 패널만 요청 완료로 전환되는지 확인
- [ ] [생산관리 Mobile 390](010a-change-003-screenshots/03-manufacturing-release-mobile-390.jpg): 모바일 카드·선택·요청 action 확인
- [ ] 자재에서 키팅 완료 알림을 등록하지 않아도 생산관리 요청과 제조 시작이 가능한지 실제 사용자 흐름 확인
- [ ] 이번 Change 003을 유지·수정·폐기 중 무엇으로 처리할지 결정

### 게시·운영 Gate

- [ ] local commit — 사용자 별도 요청 전 미실행
- [ ] push·PR — 승인 없음
- [ ] Persistent UAT migration·runtime handover — 승인 없음
- [ ] main merge 1·2·3차 승인 — 현재 `0/3`

## Change 004 — 생산관리 2탭 업무 분리

- 사용자 검수 Frontend: `http://127.0.0.1:42982`
- 사용자 검수 Backend: `http://127.0.0.1:41165`
- 격리 DB: experiment 전용, migration `51/51`, 외부 알림 발송 비활성

### 자동 검증

- [x] 기본 진입은 `생산계획` 탭이며 KPI·Excel·일정·담당자만 표시
- [x] `제조 투입` 탭에는 계획 KPI·Excel·계획 수정 없이 패널 선택과 투입 요청만 표시
- [x] 탭 전환 시 펼친 프로젝트가 닫혀 서로 다른 상세 상태가 섞이지 않음
- [x] Desktop 1440px 표와 Mobile 390px 카드 모두 두 탭과 목적별 action 표시
- [x] 생산관리 제조 투입→제조 정/부 업무 생성→키팅 미보고 제조 실행·완료 Full-Stack `1/1`
- [x] Frontend 전체 unit `116/116`, typecheck, lint error `0`, production build

### 사용자 직접 검수 — 마지막 일괄 대기

- [ ] [생산계획 Desktop](010a-change-004-screenshots/01-production-planning-tab-desktop-1440.jpg): KPI·Excel·계획 프로젝트만 보이는지 확인
- [ ] [제조 투입 Desktop](010a-change-004-screenshots/02-manufacturing-release-tab-desktop-1440.jpg): 패널 선택·키팅/입고 참고·투입 요청만 보이는지 확인
- [ ] [투입 성공 Desktop](010a-change-004-screenshots/03-manufacturing-release-success-desktop-1440.jpg): 선택한 패널만 요청 완료로 바뀌는지 확인
- [ ] [생산계획 Mobile 390](010a-change-004-screenshots/04-production-planning-tab-mobile-390.jpg): KPI·검색·프로젝트 카드 흐름 확인
- [ ] [제조 투입 Mobile 390](010a-change-004-screenshots/05-manufacturing-release-tab-mobile-390.jpg): 카드 확장·패널 선택 흐름과 가로 넘침 없음 확인
- [ ] 이번 Change 004를 유지·수정·폐기 중 무엇으로 처리할지 결정

### 게시·운영 Gate

- [ ] local commit — 사용자 별도 요청 전 미실행
- [ ] push·PR — 승인 없음
- [ ] Persistent UAT migration·runtime handover — 해당 없음/승인 없음
- [ ] main merge 1·2·3차 승인 — 현재 `0/3`
