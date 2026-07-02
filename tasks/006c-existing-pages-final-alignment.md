# TASK-006C 기존 페이지 잔여 정렬

## 목적

TASK-006B 이후 남은 기존 페이지의 사용자-facing 용어, 자재 화면 설명, Excel 양식 표시, 내 업무/알림 link fallback을 최종 18단계 업무 흐름과 맞춘다.

## 구현 범위

- 자재 페이지가 현재 제공하는 기능을 구매품목 입고 처리 기준으로 명확히 표시한다.
- `출하 완료`, `출하일` 등 legacy 사용자-facing 용어가 신규 화면과 양식에 남지 않도록 점검한다.
- Excel template과 preview에서 `제품`, `제품명`, `제품구분`, 신규 `RRP` 표시가 없는지 실제 다운로드 파일 기준으로 확인한다.
- 구현된 workflow stage는 실제 입력 페이지로 이동한다.
- 전용 입력 화면이 아직 없는 workflow stage는 프로젝트 상세 workflow 요약으로 안전하게 이동한다.
- 프로젝트 상세 workflow 요약에서 전용 입력 화면이 없는 단계임을 최소 안내한다.
- 설계, 생산관리, 구매 section과 workflow 2~4단계의 연결을 유지한다.

## 제외 범위

- Pending List 공통 모듈
- 품질 부적합, 고객사 PUNCH LIST, 제조 중단
- IQC/LQC/OQC/FAT 체크리스트
- 검사성적서 PDF 출력
- 제조 체크리스트
- 실제 자재 도착/IQC 요청/입고 확정 분리 기능
- 실제 키팅 완료 기능
- 물류 포장 완료, 출발 처리, 납품 완료 기능
- 세금계산서/영업 정산 화면
- Microsoft 365 로그인
- 관리자 기준정보 전체 페이지
- 모든 페이지 Excel export 공통 기능
- QR 이미지 생성/출력
- DB 대량 데이터 보정

## TASK-006B에서 남은 항목

- 현재 자재 페이지가 최종 5~8단계 전체 기능처럼 보이지 않도록 표현 정리
- 미구현 workflow stage link가 프로젝트 상세 workflow 요약으로 안전하게 이동하도록 보강
- Excel 양식의 잔여 legacy 용어 실제 파일 기준 재확인
- `출하 완료` 대신 `납품 완료` 사용자 표시 유지 확인

## 자재 페이지 정렬 방향

현재 `/materials/receipts`는 구매품목 기준 입고 완료 여부, 완료일, 완료 비고를 저장하는 화면이다. 이번 TASK에서는 이를 `자재 입고 처리`로 표시하고, 자재 도착/IQC/입고 확정/키팅 완료를 새 기능으로 분리하지 않는다.

## 납품 용어 정리 방향

사용자-facing 표시는 `납품 완료`를 사용한다. 내부 legacy enum/code는 불필요하게 변경하지 않는다. 구매 Excel의 legacy `출하일`은 import alias로만 유지하고 신규 template에는 노출하지 않는다.

## Excel 양식 최종 점검 방향

실제 다운로드된 `.xlsx`를 열어 header와 안내문을 확인한다.

- 프로젝트 Excel
- 설계/패널정보 Excel
- 생산계획 bulk Excel
- 생산계획 project Excel
- 구매 bulk/project Excel
- 자재 Excel이 존재하는 경우 해당 양식

확인 기준은 `패널`, `패널명`, `Item`, `RPP`, `납기일`, `입고예정일`, `납품 완료`이며, 신규 양식에 `제품`, `제품명`, `제품구분`, `RRP`, `출하 완료`, 불필요한 `출하일`을 표시하지 않는다.

## Workflow 미구현 단계 처리 방향

ProductionPlanning, DesignPanelInfo, ProcurementInfo는 실제 입력 페이지로 이동한다. MaterialArrived 이후 미구현 단계는 `/projects/{projectId}?section=workflow`로 이동시켜 404를 방지하고, 프로젝트 상세 workflow 요약에서 전용 입력 화면은 후속 단계에서 제공된다는 최소 안내를 표시한다.

## 내 업무/알림 링크 보강 방향

- ProductionPlanning: `/projects/{projectId}/production-planning/edit`
- DesignPanelInfo: `/projects/{projectId}/panel-information/edit`
- ProcurementInfo: `/projects/{projectId}/procurement/edit`
- 미구현 stage: `/projects/{projectId}?section=workflow`

## 테스트 계획

- Frontend unit: 자재 페이지 용어, 프로젝트 상세 workflow 안내, 미구현 stage fallback link, 구현 stage edit link 유지
- Backend tests: workflow work item link mapping
- Full-Stack E2E: 자재 페이지 smoke, workflow order, Excel template header smoke
- UAT: 실제 화면과 Excel 다운로드 파일 확인

## 사용자 검수 체크리스트

- [ ] 자재 페이지 용어가 현재 기능 기준으로 명확함
- [ ] 사용자 화면에 `출하 완료` 대신 `납품 완료`가 표시됨
- [ ] Excel 양식에 `제품`, `제품명`, `제품구분` 문구가 없음
- [ ] Excel 양식에 신규 `RRP` 표시가 없음
- [ ] Excel 양식에 불필요한 `출하일` 표시가 없음
- [ ] 내 업무/알림에서 생산관리 업무는 생산계획 수정 페이지로 이동
- [ ] 내 업무/알림에서 설계 업무는 설계 입력 페이지로 이동
- [ ] 내 업무/알림에서 구매 업무는 구매정보 수정 페이지로 이동
- [ ] 미구현 stage link는 404가 아님
- [ ] 프로젝트 상세 section 이름이 workflow 기준과 일치함
- [ ] Console 오류 없음

## 후속 TASK로 넘길 항목

- TASK-008A: 자재 도착 / IQC 요청 / 입고 확정
- TASK-009A: 검사 체크리스트 / IQC 디지털 성적서 / PDF 출력
- TASK-010A: 키팅 완료 / 제조 내 업무 생성
- TASK-011A: 제조 체크리스트 / 작업 시작·종료 / 제조 중단
- TASK-012A: LQC / OQC / 전진검수 / FAT
- TASK-013A: 물류 포장 / 출발 / 납품 완료
- TASK-014A: 영업 정산 / 세금계산서 / 프로젝트 완료
