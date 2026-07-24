# TASK-E2E-FULL-SUITE-001 Change 008 구현 보고 — 확정 정책 E2E 갱신

## 바꾼 E2E 계약

1. 발주 수량·단위는 구매팀이 입력하고 자재 도착에는 도착 수량만 입력한다.
2. 키팅은 선택형 준비 현황 공유로만 확인하고, 생산관리의 패널별 제조 투입 요청 뒤 제조 업무가 생성되는지 검증한다.
3. LQC·OQC는 Checklist, 전진검수·FAT는 Aggregate 판정으로 입력한다.
4. 프로젝트 상세 첫 진입은 전체 흐름이며 현재 8개 탭만 desktop·390px에서 캡처하고 overflow를 확인한다.
5. 정산 발행요청 기간은 실제 출발일이 포함된 달력월로 계산하고 출발 panel만 선택한다.
6. 품질 전용 E2E에 Aggregate 부적합→Pending→재검사 적합→같은 Pending 종결 계약을 추가했다.
7. 단일 `App.tsx`를 여러 화면으로 전환하는 jsdom 통합 테스트가 기본 5초를 0.1~0.3초 초과해 기능 assertion 전에 간헐 실패하므로, 제품 timeout과 무관한 Vitest 상한만 10초로 조정했다.

## 실행 결과

- 실제 역할 18단계 전체 lifecycle: `1/1 PASS`, 약 `2.1m`, 최종 workflow 완료·open Pending 0·회계 발행요청 workbook 생성.
- 12면 혼합 자재·6회 분할 입고·제조 Pending 6건 stress lifecycle: `1/1 PASS`, 약 `2.3m`, 최종 workflow `18`, open Pending `0`, 프로젝트 완료.
- 품질 Aggregate Pending 회귀: `1/1 PASS`.
- IQC Pending 연속성 회귀: `1/1 PASS`.
- Backend 전체 `420/420`, Frontend 전체 `125/125`, typecheck·production build·lint error 0·`git diff --check`를 통과했다.
- synthetic DB·container·network는 각 실행 뒤 삭제했다.
- 화면 증빙은 Repository 밖 `/tmp/emi-qms-lifecycle-evidence`에 보관했다.

## 경계

- 실제 provider·Persistent UAT·대표 repo·`main`·push·PR·merge는 변경하지 않았다.
- 사용자 직접 검수는 고정 runtime에서 대기한다. main merge 승인 `0/3`이다.

## 사용자 검수 후 회귀 보강

- 프로젝트 진행률 `100%`를 숫자만 확인하던 검증을 실제 막대 채움 폭과 불투명 배경까지 확인하도록 강화했다.
- 강화된 E2E 첫 실행에서 세금계산서 권장기간 초기 조회가 사용자 직접기간 조회보다 늦게 끝나 최신 화면을 덮어쓸 수 있는 race를 발견했다.
- `SalesBillingRequestPage`는 요청 순번을 비교해 최신 조회 결과만 적용한다. 사용자가 선택한 출하 월 범위가 이전 권장기간 응답으로 되돌아가지 않는다.
- 보정 후 전체 역할 18단계 E2E를 다시 실행해 `1/1 PASS`했고, 새 진행률 실제 채움 assertion도 통과했다.
