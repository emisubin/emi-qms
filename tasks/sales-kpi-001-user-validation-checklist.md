# TASK-SALES-KPI-001 사용자 검수 체크리스트

- 환경: `experiment/task-home-002-personalized-shell` isolated/synthetic runtime
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] 영업 Home과 영업 전용 화면이 동일한 연간 KPI aggregate 사용
- [x] 확정 세금계산서만 실적에 포함하고 파이프라인은 달성률에서 제외
- [x] 12개월 목표 미등록·등록 누계·잔여/초과 금액 처리
- [x] 목표 관리 권한·CAS·audit와 일반 사용자 mutation 차단
- [x] Backend 398/398, Frontend 104/104, 격리 Full-Stack 1/1
- [x] Desktop·390px graph와 page horizontal overflow 0

## 사용자 직접 검수

- [ ] [영업 Home Desktop](sales-admin-002-screenshots/01-sales-home-desktop-1440.png): 첫 panel의 연간 graph·금액 카드 확인
- [ ] [영업 전용 Desktop](sales-admin-002-screenshots/02-sales-kpi-desktop-1440.png): 5개 KPI·월 graph·근거 영역 확인
- [ ] [영업 전용 Mobile](sales-admin-002-screenshots/03-sales-kpi-mobile-390.png): 4×3 월 block과 금액 가독성 확인
- [ ] [영업 Home Mobile](sales-admin-002-screenshots/04-sales-home-mobile-390.png): 핵심 KPI 우선 배치와 drawer navigation 확인
- [ ] 표시 금액·목표 정책을 유지·수정할지 결정

## 게시·운영 Gate

- [x] local experiment commit 완료 — 이 checklist를 포함한 최종 experiment commit으로 고정
- [ ] push 승인 — 현재 없음
- [ ] PR 승인 — 현재 없음
- [ ] Persistent UAT migration·runtime handover 승인 — 현재 없음
- [ ] main merge 1차 승인
- [ ] main merge 2차 승인
- [ ] main merge 3차 승인
