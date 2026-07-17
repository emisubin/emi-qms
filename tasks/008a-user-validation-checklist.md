# TASK-008A 사용자 검수 체크리스트

- 환경: `experiment/task-008a-material-receiving` local isolated runtime
- 데이터: synthetic project·item only
- 자동 검증 상태: `완료`
- 사용자 검수 상태: `대기`
- 대표 repo·GitHub main 반영: `없음`
- main merge 승인: `0/3`

## 자동 검증

- [x] `0030_material_receiving_iqc.sql` fresh/existing migration catalog·schema·legacy backfill test
- [x] 도착 등록 → IQC 요청 → 합격 → 입고 확정 → 도착 마감 → derived 완료
- [x] 초과 수량 단건 차단과 동시 도착 경쟁 시 발주 수량 초과 차단
- [x] IQC 부적합 → Urgent·미배정 Pending → 재검사 요청 → 합격과 Pending 원자 종결
- [x] 기존 구매 PATCH·자재 legacy PATCH·Excel apply의 직접 완료값 변경 차단
- [x] 권한, version conflict, 기존 구매·Pending·프로젝트·모바일 경로 회귀
- [x] Frontend typecheck·unit·build, Backend 전체 test, isolated Full-Stack E2E
- [x] 390px 모바일과 desktop에서 자재·IQC 페이지 렌더링 및 horizontal layout 확인

## 사용자 직접 검수

- [ ] [자재 입고 Desktop](008a-screenshots/materials-desktop.jpg): 요약, 검색, 품목별 상태·수량·단계가 이해되는지 확인
- [ ] [자재 입고 Mobile](008a-screenshots/materials-mobile.jpg): PC 축소가 아닌 카드·하단 메뉴·터치 action 구조인지 확인
- [ ] [IQC Desktop](008a-screenshots/iqc-desktop.jpg): 품질 담당의 검사 대기 목록과 상태가 명확한지 확인
- [ ] [IQC Mobile](008a-screenshots/iqc-mobile.jpg): 한 손 사용 기준으로 검사 대기 카드와 필터가 이해되는지 확인
- [ ] isolated runtime에서 도착 등록 sheet의 수량·단위·날짜·메모 입력을 직접 확인
- [ ] 합격/부적합 판정, Pending 연결, 재검사, 입고 확정 action feedback을 직접 확인
- [ ] 이번 실험 결과를 유지·수정·폐기 중 무엇으로 처리할지 결정

## 게시·운영 Gate

- [x] local experiment commit 완료
- [ ] push 승인 — 현재 없음
- [ ] PR 승인 — 현재 없음
- [ ] Persistent UAT migration·runtime handover 승인 — 현재 없음
- [ ] main merge 1차 승인
- [ ] main merge 2차 승인
- [ ] main merge 3차 승인

사용자 직접 검수와 세 번의 merge 승인은 서로 다른 Gate다. 이 체크리스트의 자동 검증 완료는 사용자 검수 또는 게시·merge 승인을 대신하지 않는다.
