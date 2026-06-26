# 10. 핵심 테스트 시나리오

## 시나리오 1: 프로젝트 등록 기준

영업이 포장방식을 포함한 필수 입력값으로 프로젝트를 등록 → PJT Code 중복은 허용 → PJT Title은 앞뒤 공백 제거, 연속 공백 축소, 대소문자 무시 기준으로 중복 거부 → 면수만큼 P01, P02 제품 Placeholder 생성 → 제품명·패널명은 비어 있음.

## 시나리오 2: 전체 조회와 부서별 입력

Sales, Production Planning, Manufacturing, Quality, Logistics, Read Only가 프로젝트 A/B를 모두 조회 → Manufacturing은 프로젝트 수정 API 거부 → Quality는 제조정보 수정 API 거부 → Sales는 프로젝트 쓰기권한 보유.

## 시나리오 3: 제한 계정 프로젝트 범위

`Project.Read.All`이 없는 제한 계정이 배정 프로젝트 A 조회 성공 → 미배정 프로젝트 B 조회 403 → `UserProjectAccess` 기준이 유지됨.

## 시나리오 4: 민감정보 권한

Sales와 System Administrator는 판매금액과 제조 소요시간 조회 성공 → 설계, 구매, 생산관리, 제조, 품질, 물류, Read Only는 거부 → UI 표시 여부와 관계없이 서버 정책이 차단.

## 시나리오 5: 제조 동시 작업

여러 활성 프로젝트 제품을 통합 선택 → 동일 제조단계만 하나의 작업 묶음으로 최대 50면 저장 → 단계 순서 강제 없음 → 작업 중단 시 사유 필수.

## 시나리오 6: 품질 사진 필수

모든 체크리스트 항목, 부적합 등록, 제조 조치 완료, 재검사 항목에 사진 1장 이상 첨부 → 사진 누락 시 항목 완료와 품질 승인 거부.

## 시나리오 7: 승인 후 정정

입력 담당 부서가 수정·되돌리기 요청 → 변경 대상, 변경 전 값, 변경 후 값, 수정자, 수정일시, 수정 사유 저장 → 뒤 단계 영향은 해당 TASK 기준으로 검증.

## 시나리오 8: 보류·취소

영업이 프로젝트 보류 또는 취소 → 진행 중 업무 중단 → 기존 데이터 조회 가능 → 해제는 영업만 가능.

## 시나리오 9: 논리삭제와 삭제 보관함

영업이 오등록 프로젝트를 삭제 사유와 PJT Title 확인 입력으로 논리삭제 → 일반 목록과 취소 목록에서 제외 → 삭제 보관함에서 Sales와 System Administrator만 조회 → 패널 Placeholder와 감사이력은 보존 → 동일 PJT Title로 신규 프로젝트 등록 가능.

## 시나리오 10: 삭제 동시성

삭제 Guard는 삭제 transaction의 DB connection/transaction을 공유 → Guard 차단 또는 예외 시 삭제 metadata와 감사이력 미저장 → 삭제와 면수증가, 삭제와 삭제 요청이 동시에 들어와도 500 없이 직렬화 가능한 한 결과만 commit → 패널 수, sequence, 감사이력이 실제 DB 상태와 일치.

## 시나리오 11: 목록 stale response

진행·보류·완료·취소·삭제 보관함 탭 또는 검색 조건을 빠르게 변경 → 늦게 도착한 이전 요청의 성공·실패·abort 결과가 현재 탭과 목록 상태를 덮어쓰지 않음.

프로젝트 목록 기본 탭은 전체이며 논리삭제되지 않은 진행·보류·완료·취소 프로젝트를 함께 표시한다. 진행/보류/완료/취소 탭은 보조 필터이고 삭제 보관함은 Sales/System Administrator에게만 보인다. PC 목록은 프로젝트명, 고객사, Code, Item, 면수, 납기일, 상태, 진행률 Header를 sticky로 표시하고, 모바일 목록은 table 축소가 아닌 card layout을 사용한다. 목록 상태는 프로젝트 자체 보류/취소/완료가 우선하며 Active 프로젝트는 활성 패널 workflow_stage로 제조 전~출하 완료를 계산한다. 진행률은 workflow_stage 점수 평균 기반 임시 퍼센트이며 향후 체크리스트 기반 진행률로 교체될 수 있다.

## 시나리오 12: 패널정보 직접 입력과 단위 drift

Design이 StretchWrap 프로젝트의 No.1과 No.2에 같은 PanelName 입력 → 동일명칭 허용 및 No로 구분 → QR Eligible true → PanelInfoCompleted true. WoodenCrate 프로젝트는 PanelName만 입력하면 QR Eligible true, PanelInfoCompleted false → W/H/D 모두 입력 후 완료 true. mm/inch 표시 또는 입력 단위 전환만으로 canonical mm, version, audit이 변하지 않음.

## 시나리오 13: 패널정보 Excel Preview/Apply와 관리자 이력

Preview는 DB 변경 없음 → Apply는 파일 SHA 확인 후 재파싱 → project row lock 이후 최신 PackagingMethod, 상태, 삭제여부, panel status, panel info version 재검증 → Preview 이후 포장방식이나 version이 바뀌면 409와 전체 rollback. Parser는 visible worksheet 선택, hidden/very hidden 제외, 후보 sheet 모호성, 중복/병합 header 오류, 20행 header 검색 제한, ZIP/Workbook 리소스 제한, Formula/macro/externalLinks/OLE 거부, parse gate cancellation, No=501 같은 큰 sequence 매칭을 검증. Excel은 전체 활성 패널을 포함하지 않아도 되며, 파일에 없는 No는 유지하고 빈 editable 행은 Skipped로 처리한다. Panel Name만 입력하면 이름만, W/H/D 모두 입력하면 사이즈만 또는 함께 적용하며 일부 W/H/D는 오류다. Excel Apply 이력은 Import Batch, InputSource, InputUnit, OriginalInputValue와 canonical mm audit 값을 연결하고, System Administrator만 ImportBatchId 또는 CorrelationId 기준으로 그룹화된 전체 이력을 조회한다.

## 시나리오 14: 제품·패널 목록과 Workflow Summary

프로젝트 상세은 입력칸 없는 조회 전용 제품·패널 목록을 표시한다. Header는 No, 패널명, 사이즈, 제품정보, QR, 상태 순서이고 스크롤 시 sticky로 유지한다. 상태 열은 `workflow_stage`를 한글로 표시하며 `Active/Cancelled` 관리상태를 제조·검사·출하 업무상태처럼 변환하지 않는다. Summary는 QR 가능, 제조 완료, 검사 완료 순서로 Backend 계산값을 표시하고, 취소 패널은 모든 분모와 집계에서 제외한다. 제조 완료에는 검사·포장·출하 단계가 포함되고, 검사 완료에는 포장·출하 단계가 포함된다.
