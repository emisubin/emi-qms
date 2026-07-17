# TASK-008B 사용자 검수 체크리스트

- 환경: `experiment/task-008b-customer-supplied-materials` local isolated runtime
- 데이터: synthetic project·item only
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `대기`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] `0031_customer_supplied_materials.sql` fresh/existing migration과 conditional DB CHECK
- [x] 신규 Purchased 기본값, 기존 direct PATCH omitted-preserve, Excel supply/measurement 보존
- [x] 사급 pair·변경 사유·old/new audit, 공급 유형 변경 gate·수량 floor·단위 고정
- [x] 예정/도착/확정/미도착/처리대기 projection과 제공 지연 기준
- [x] 사급 부족 마감 차단, 예정량 정정 뒤 전량 마감
- [x] 공급 기준 update와 도착 등록 경쟁의 row-lock 직렬화
- [x] 기존 구매·자재·IQC·Pending·일반 구매품 회귀
- [x] Backend 전체, Frontend lint·typecheck·unit·build, isolated Full-Stack E2E
- [x] desktop·390px 구매 조회/수정·자재·IQC와 모바일 horizontal overflow 0

## 사용자 직접 검수

- [ ] [구매 조회 Desktop](008b-screenshots/01-procurement-read-desktop.png): 사급 책임과 제공 예정 수량·단위가 읽기 쉬운지 확인
- [ ] [구매 수정 Desktop](008b-screenshots/02-procurement-edit-desktop.png): 공급 방식·수량·단위 입력 구조 확인
- [ ] [자재 입고 Desktop](008b-screenshots/03-materials-desktop.png): filter·제공 지연·다섯 수량 projection 확인
- [ ] [IQC Desktop](008b-screenshots/04-iqc-desktop.png): 사급 badge와 검사 수량 확인
- [ ] [구매 조회 Mobile](008b-screenshots/05-procurement-read-mobile-390.png): PC 축소가 아닌 사급 카드 구조인지 확인
- [ ] [구매 수정 Mobile](008b-screenshots/06-procurement-edit-mobile-390.png): 세로 입력과 터치 영역 확인
- [ ] [자재 입고 Mobile](008b-screenshots/07-materials-mobile-390.png): 요약 rail·filter·카드 우선 구조 확인
- [ ] [IQC Mobile](008b-screenshots/08-iqc-mobile-390.png): 한 손 사용 기준 카드·하단 메뉴 확인
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
