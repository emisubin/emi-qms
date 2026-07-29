# TASK-E2E-FULL-SUITE-001 Change 007 — 실제 담당자 흐름 연속성 검수

## 검수 계약

- 프로젝트 상세 첫 진입은 `전체 흐름`이다.
- 생산계획·담당자 저장 직후 설계와 구매 정담당 업무가 함께 생성된다.
- 구매 완료는 활성 품목 1건 이상과 다음 handoff data를 요구한다.
  - 공통: 품목명, 공급구분, 입고예정일
  - 구매품: 업체명, 발주일
  - 사급품: 제공 예정 수량과 단위
  - required template가 있으면 모든 필수 row match
- 구매 완료는 후속 도착·IQC를 기다리지 않으며 한 번 완료된 stage는 회귀하지 않는다.
- 실제 역할 UI로 `도착 → 자동 IQC → 부적합 → Pending 조치 → 자동 재검사 → 합격`을 검증한다.
- 품질 내 업무·알림은 exact IQC를 열고, 중복 handoff 0건과 workflow 완료 상태를 확인한다.
- QR 선택 일괄 발급·행 inline preview와 모바일 자재 하위 탭 overflow 0을 검증한다.

## 실행 경계

- synthetic data와 disposable PostgreSQL만 사용한다.
- 대표 repo·main·Persistent UAT·실제 provider는 변경하지 않는다.
