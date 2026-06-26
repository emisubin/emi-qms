# TASK-003B-1: Panel Detail UX, Partial Excel Import and Grouped Admin Audit

## 목적

TASK-003B 패널정보 기능을 사용자 업무 화면 기준으로 정리한다. 프로젝트 상세은 조회 전용 제품·패널 목록으로 통합하고, 입력과 Excel 기능은 별도 수정페이지로 분리한다. Excel은 부분 입력을 허용하며, 입력이력은 저장행위 단위로 그룹화해 관리자만 볼 수 있게 한다.

## 조회페이지와 수정페이지 분리

- 프로젝트 상세 조회페이지는 모든 역할에게 고정값만 표시한다.
- 조회페이지에는 입력칸, select, 저장 버튼, disabled input을 렌더링하지 않는다.
- 패널정보 수정권한 사용자는 `패널정보 수정` 버튼으로 `/projects/{projectId}/panel-information/edit` 화면에 진입한다.
- 수정권한이 없는 사용자가 수정 URL 또는 쓰기 API를 직접 호출하면 403이다.

## 입력권한

- `PanelInfo.Update`: Design, Sales, Production Planning
- 조회: 기존 프로젝트 조회 정책
- 관리자 이력: `Audit.Read.All`을 가진 System Administrator만 가능
- System Administrator는 업무 입력버튼이 없고 전체 이력만 조회한다.

## 제품·패널 목록 통합

- 사용자 화면 용어는 `제품·패널 목록`으로 통일한다.
- DB 테이블명 `panel_placeholders`는 변경하지 않는다.
- 목록 Header는 `No`, `패널명`, `사이즈`, `제품정보`, `QR`, `상태` 순서다.
- 패널명이 없으면 `미입력`, 사이즈가 없으면 포장방식에 따라 `미입력` 또는 `선택사항`으로 표시한다.
- 목포장 필수 사이즈 미입력, 제품정보 미입력, QR 생성 불가는 오류 색상과 명확한 텍스트를 함께 표시한다.
- 목록 Header와 수정 Grid Header는 `position: sticky`로 스크롤 중에도 유지한다.

## Product Workflow Stage

패널 관리상태 `Active/Cancelled`는 취소·활성 여부이며 제조·품질·물류 진행상태가 아니다. 제품 업무상태는 별도 `workflow_stage`로 저장한다.

허용값:

- `BeforeManufacturing`: 제조 전
- `ManufacturingInProgress`: 제조 중
- `ManufacturingCompleted`: 제조 완료
- `InspectionInProgress`: 검사 중
- `InspectionCompleted`: 검사 완료
- `PackingCompleted`: 포장 완료
- `ShipmentCompleted`: 출하 완료

이번 TASK에서는 workflow stage를 사용자가 변경하는 운영 API나 화면을 만들지 않는다. 향후 제조·품질·물류 TASK가 해당 값을 갱신한다.

## 프로젝트 Summary

프로젝트 상세 상단 패널 요약은 다음 세 항목만 표시한다.

1. `QR 가능 {qrEligibleCount}/{activePanelCount}`
2. `제조 완료 {manufacturingCompletedCount}/{activePanelCount}`
3. `검사 완료 {inspectionCompletedCount}/{activePanelCount}`

기존 패널정보 입력 완료·미완료 요약은 프로젝트 상세에서 제거하고, 패널정보 수정페이지에만 유지한다. 모든 분모는 활성 패널 수이며 취소 패널은 제외한다.

제조 완료 집계는 `ManufacturingCompleted`, `InspectionInProgress`, `InspectionCompleted`, `PackingCompleted`, `ShipmentCompleted`를 포함한다. 검사 완료 집계는 `InspectionCompleted`, `PackingCompleted`, `ShipmentCompleted`를 포함한다.

## 프로젝트 목록

- 일반 목록 탭 순서는 `전체`, `진행`, `보류`, `완료`, `취소`이며 기본 선택은 `전체`다.
- `전체`는 논리삭제되지 않은 Active, OnHold, Completed, Cancelled 프로젝트를 모두 표시한다.
- 삭제 보관함은 기존처럼 Sales와 System Administrator에게만 표시하며 일반 탭과 분리한다.
- PC 목록 Header는 `프로젝트명`, `고객사`, `Code`, `Item`, `면수`, `납기일`, `상태`, `진행률` 순서이며 sticky로 유지한다.
- Mobile 목록은 table을 축소하지 않고 프로젝트별 card로 표시한다.
- Project Status가 OnHold, Cancelled, Completed이면 업무상태 표시도 각각 `보류`, `취소`, `완료`가 우선한다.
- Active 프로젝트는 활성 패널의 `workflow_stage`로 목록 업무상태를 계산한다. 표시값은 `제조 전`, `제조 중`, `제조 완료`, `검사 중`, `검사 완료`, `출하 준비`, `출하 완료`다.
- 목록 진행률은 활성 패널 `workflow_stage` 점수 평균을 정수 반올림한 임시 요약이다. `BeforeManufacturing=0`, `ManufacturingInProgress=20`, `ManufacturingCompleted=40`, `InspectionInProgress=60`, `InspectionCompleted=75`, `PackingCompleted=90`, `ShipmentCompleted=100`을 사용한다.
- 목록 진행률은 향후 체크리스트 기반 진행률 엔진으로 교체될 수 있다.

## Excel 부분 입력 규칙

- Excel 파일은 전체 활성 패널을 포함하지 않아도 된다.
- 파일에 없는 Panel No는 기존값을 유지한다.
- editable 셀이 전부 빈 행은 `Skipped`이며 DB 변경, audit, version 증가가 없다.
- `Panel Name` 빈 셀은 기존 PanelName 유지다. Excel로 삭제하지 않는다.
- W/H/D가 모두 빈 셀인 경우 기존 size 유지다.
- W/H/D 일부만 입력하면 row error다.
- W/H/D 모두 입력하면 size 변경 의도로 처리한다.
- PanelName만 입력하거나 Size만 입력할 수 있다.
- WoodenCrate에서도 부분 Excel 저장을 허용하고 완료여부는 저장 후 최신 전체 데이터로 계산한다.

## 입력이력 그룹화

Field-level audit row는 유지한다. 사용자 화면과 grouped history API는 저장행위 한 건으로 묶는다.

그룹 기준:

1. `ImportBatchId`
2. `CorrelationId`
3. legacy null이면 개별 event

그룹 시각은 해당 그룹의 최신 `ChangedAtUtc`로 고정하며 최신 그룹 우선으로 정렬한다.

## 관리자 전용 이력

`Audit.Read.All`은 System Administrator에만 부여한다. 모든 일반 history API는 이 권한을 요구한다. 비관리자는 프로젝트 상세에서 이력 UI를 보지 못하고 직접 API 호출 시 403이다.

## 상태 한글화

- Active: 진행
- OnHold: 보류
- Completed: 완료
- Cancelled: 취소
- 삭제 보관함: 삭제

API enum은 유지하고 Frontend 중앙 표시 함수로 한글화한다.

## API

- `GET /api/projects/{projectId}/panel-information`
- `PATCH /api/projects/{projectId}/panel-information`
- `GET /api/projects/{projectId}/panel-information/history` 관리자 전용 grouped history
- `GET /api/projects/{projectId}/audit-history` 관리자 전용 project audit
- `GET /api/projects/{projectId}/panel-information/import/template?unit=mm|inch`
- `POST /api/projects/{projectId}/panel-information/import/preview`
- `POST /api/projects/{projectId}/panel-information/import/apply`

## 화면

- 프로젝트 상세: 기본정보, 상태, 제품·패널 목록, 관리자 전용 전체 이력
- 패널정보 수정페이지: 직접 입력 grid/card, Excel 양식 다운로드, Excel 업로드/Preview/Apply
- Excel Preview Dialog: Preview 상단 sticky action bar에 신규/변경/동일/건너뜀/오류 수, 수정사유, `Excel 저장` 버튼을 표시한다.
- 제품 상세: 고정값 표시
- 모바일: 조회 카드와 수정 카드 분리

## Migration

`database/migrations/0006_admin_audit_access.sql`을 추가한다. 기존 0001~0005는 수정하지 않는다. `Audit.Read.All` Permission을 추가하고 System Administrator에만 연결한다.

`database/migrations/0007_panel_workflow_stage.sql`을 추가한다. 기존 0001~0006은 수정하지 않는다. `panel_placeholders.workflow_stage`를 추가하고 기본값 `BeforeManufacturing`, 허용값 check constraint, 프로젝트별 조회 index를 둔다.

## 테스트

- Backend: Audit 권한, grouped history, Excel partial preview/apply, workflow stage, project summary, migration 0006/0007
- Frontend: 조회페이지 입력 제거, 수정페이지 이동, 권한별 버튼, 한글 상태, sticky header, preview action bar, grouped history, skipped preview
- Full-Stack: 조회·수정 분리, 제품 목록 통합, workflow summary, partial Excel, preview action bar, 관리자 grouped history

## 사용자 직접 검수 절차

자동 검증 후 Docker PostgreSQL, Development Seed, Backend, Frontend를 실행 상태로 유지한다. 테스트 프로젝트 `UAT-3B1-현재시각`과 partial Excel 샘플을 준비하고 브라우저를 열어 사용자가 직접 시나리오를 확인한다.
