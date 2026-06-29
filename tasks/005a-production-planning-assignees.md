# TASK-005A: Production Planning and Project Assignee Management

## 목적

프로젝트 단위 생산계획과 프로젝트별 업무 담당자를 관리한다. 생산계획은 구매 앞 단계에 위치하며, 향후 알림 기능의 수신자 기준을 저장한다.

## 구현범위

- 생산관리 메뉴와 `/production-planning` 업무 페이지
- 프로젝트 상세 생산계획 조회 section
- `/projects/{projectId}/production-planning/edit` 수정 페이지
- 프로젝트 Item 기반 생산계획 템플릿
- 생산관리팀용 Item별 생산계획 단계 설정 페이지
- 프로젝트 단위 생산계획 예정일 입력
- 기본 템플릿 외 프로젝트별 사용자 추가 계획 행
- 여러 프로젝트 생산계획 Excel 양식 다운로드, Preview, Apply
- 구매/생산관리/제조/품질/물류 담당자 지정
- 담당자 미지정 시 알림 fallback 계산
- 관리자 전용 grouped history
- PC table / Mobile card 화면 분리

## 제외범위

실제 알림 발송, QR 이미지 생성·출력, 제조 작업 체크리스트, 제조 시작·종료, 품질 검사, 물류 출하, 구매 기능 재구현, ECOUNT 연동, Microsoft Entra ID, PWA Service Worker는 구현하지 않는다.

## 변경된 업무 흐름

영업 → 설계 → 생산계획 → 구매 → 제조 → 품질 → 물류.

구매 데이터 구조와 기존 구매 기능은 유지하며, 생산계획을 구매 앞 단계로 추가한다.

## 생산계획 프로젝트 단위 원칙

생산계획은 프로젝트 단위로 관리한다. 패널별 생산계획은 이번 TASK에서 만들지 않는다. 한 프로젝트는 하나의 생산계획을 가진다.

## Item별 계획 템플릿

기준 Item은 `UL67`, `UL891`, `UL508A`, `IEC`, `LLP`, `RRP`로 확정한다. 프로젝트 생성/수정의 Item은 자유입력이 아니라 이 기준 Item 중 하나를 선택한다. 생산계획 수정 페이지는 프로젝트 Item을 자동 사용하며, 별도 Item select를 제공하지 않는다.

각 Item은 초기 임시 템플릿으로 `자재 입고`, `조립 시작`, `배선`, `검사 준비` step을 가진다. 계획 항목명은 Item별 템플릿 step으로 관리하며, 프로젝트별 custom item을 추가할 수 있다.

Production Planning 권한 사용자는 `/production-planning/settings`에서 Item별 생산계획 단계의 순서, 단계명, 필수 여부, 사용 여부를 설정할 수 있다. 설정 저장은 해당 Item의 새 active template version을 만들며, 이미 작성된 프로젝트의 `project_production_plan_items.step_name_snapshot`과 필수 여부 snapshot은 자동 변경하지 않는다. 설정 변경은 이후 새로 작성되는 생산계획 또는 아직 생산계획이 없는 프로젝트가 처음 작성될 때부터 적용된다.

## 계획 항목

프로젝트 계획 항목은 template step의 snapshot 이름, 필수 여부, 예정일, 비고를 저장한다. 템플릿 이름이 변경돼도 기존 프로젝트 계획 의미가 흔들리지 않게 한다. 사용자가 추가한 custom item은 `template_step_id = null`, `is_required = false`로 저장하며 항목명이 비어 있거나 같은 계획 안에서 중복되면 저장하지 않는다.

## 담당자 지정

담당자 유형은 `Procurement`, `ProductionPlanning`, `Manufacturing`, `Quality`, `Logistics`다. 각 후보는 해당 역할의 활성 사용자로 제한한다. 미지정은 허용한다.

## 알림 수신자 fallback 원칙

실제 알림은 발송하지 않는다. 조회 응답에는 다음 순서의 fallback 기준을 포함한다.

1. 지정 담당자
2. 프로젝트 영업담당자
3. System Administrator

## 권한

- 조회: 모든 활성 내부 사용자
- 수정: `ProductionPlan.Update`, Production Planning only
- System Administrator: 조회와 관리자 이력만 가능, 업무 입력 불가
- 관리자 이력: `Audit.Read.All`, System Administrator only

## 조회/수정 페이지 분리

프로젝트 상세 생산계획 section은 조회 전용이다. input, disabled input, 저장 버튼은 표시하지 않는다. Production Planning 사용자에게만 `생산계획 수정` 버튼을 표시한다.

## PC/Mobile 화면

PC는 table/grid 중심, Mobile은 card 중심으로 표시한다. 같은 데이터와 API를 사용하되 레이아웃 컴포넌트를 분리한다.

프로젝트 상세 생산관리 section은 담당자 카드 다음에 생산계획표를 먼저 표시하고, 그 아래에 캘린더 표를 표시한다. `/production-planning` 업무 페이지의 프로젝트 펼침 영역에는 캘린더를 표시하지 않는다.

생산계획 항목은 예정일이 있는 항목을 먼저 예정일 오름차순으로 표시하고, 같은 날짜는 sequence 순서로 표시한다. 예정일이 없는 항목은 아래에 sequence 순서로 표시한다.

캘린더는 시작예정일(가장 빠른 planned date)부터 종료예정일(가장 늦은 planned date)까지 연속 날짜 열을 만들고, 왼쪽 첫 열은 생산단계, 해당 날짜 셀은 체크 표시로 보여준다. 날짜가 없는 항목은 캘린더 아래 `날짜 미입력 생산단계` 영역에 표시하고 필수 항목 미입력은 강조한다. 토요일은 파란색, 일요일과 `system_holidays`의 active 공휴일/국경일/회사 휴무일은 빨간색으로 표시한다. 공휴일/국경일은 운영에서 관리형 데이터로 다루며, 최신화는 공공데이터포털 한국천문연구원 특일 정보 계열 공식 API의 공휴일 정보와 국경일 정보를 기준으로 동기화할 수 있는 backend 구조를 둔다. 운영 API key는 코드나 Git에 넣지 않고 `KOREAN_HOLIDAY_SERVICE_KEY` 또는 운영 secret store에서 공급한다. service key가 없으면 동기화는 실패 대신 한글 안내를 반환하며, CI/E2E는 외부 공휴일 API를 호출하지 않는다. UAT 수동 검수 화면에는 `검수공휴일` 같은 테스트용 공휴일을 기본 노출하지 않는다. 테스트용 공휴일은 E2E/test setup 안에서만 주입한다.

## API

- `GET /api/production-planning/summary`
- `GET /api/production-planning/projects`
- `GET /api/production-planning/product-types`
- `POST /api/production-planning/product-types`
- `GET /api/production-planning/settings/templates`
- `PATCH /api/production-planning/settings/templates/{productTypeId}`
- `GET /api/system/holidays`
- `POST /api/system/holidays/sync/kr`
- `GET /api/production-planning/import/template`
- `POST /api/production-planning/import/preview`
- `POST /api/production-planning/import/apply`
- `GET /api/projects/{projectId}/production-planning`
- `PATCH /api/projects/{projectId}/production-planning`
- `GET /api/projects/{projectId}/production-planning/history`
- `GET /api/projects/{projectId}/production-planning/export-template`

## Migration

`database/migrations/0009_production_planning_assignees.sql`을 추가한다. 기존 0001~0008은 수정하지 않는다.

0009는 생산계획 권한, 생산계획/담당자 테이블, 생산계획 Excel import batch 테이블, 기준 Item 6개와 초기 임시 템플릿을 idempotent하게 생성한다.

생산계획 단계 설정 변경 이력은 `production_plan_template_audit_events`에 저장한다. 프로젝트 생산계획 snapshot과 별도 이력이며, 기존 프로젝트 계획 행을 자동 수정하지 않는다.

## Excel 양식 열너비

Project, Panel Information, Procurement, Production Planning Excel 양식은 다운로드 시 header와 예시/현재 데이터 길이를 기준으로 `AdjustToContents` 후 최소/최대 너비를 보정한다. 날짜·숫자 열은 과도하게 넓히지 않고, 이슈사항·비고·계획 항목 같은 긴 텍스트 열은 다른 열보다 넓게 설정한다. 헤더 freeze와 autofilter는 유지한다.

## 후속 관리자 기준정보 페이지

이번 TASK에서는 전체 관리자 기준정보 페이지를 구현하지 않는다. 후속 후보 `TASK-ADMIN-001: Master Data and Calendar Administration`에서 공휴일 지정, Item 추가/수정/삭제, 생산계획 단계 template 관리, 기타 select 기준정보, 사용 여부, 이력을 통합 관리한다.

## UAT DB 보호

수동 검수는 `scripts/dev-uat-start.sh`가 사용하는 고정 DB `emi_qms_uat_005a`를 기준으로 한다. 이 스크립트는 DB가 없을 때만 생성하고 기존 수동 검수 데이터를 drop/truncate하지 않는다. Full-Stack E2E는 별도 임시 DB를 사용하며 테스트 종료 후 삭제된다.

## 테스트

- Backend PostgreSQL 통합 테스트: 권한, 상태 판정, 담당자 지정, fallback, history, custom item, template settings, Excel import, Excel template width, migration
- Frontend 테스트: 메뉴, section 순서, 조회/수정 분리, 프로젝트 Item select, Item 자동 표시, PC/Mobile, 담당자 fallback, 행 추가, template settings, 캘린더 표
- Full-Stack E2E: 생산관리 페이지, 상세 section, 직접 입력, 권한, 관리자 이력, 모바일

## 사용자 직접 검수 절차

- [ ] 공통 메뉴에 생산관리가 있음
- [ ] 프로젝트 상세 section 순서가 제품 목록 / 생산관리 / 구매
- [ ] 생산계획 페이지에 KPI 카드가 있음
- [ ] 생산계획은 프로젝트 단위로 보임
- [ ] 프로젝트 생성/수정 Item이 UL67, UL891, UL508A, IEC, LLP, RRP select로 표시됨
- [ ] 생산계획 단계 설정 페이지에서 Item별 단계/필수/사용 여부 수정 가능
- [ ] 설정 이후 새 생산계획에 최신 단계가 반영되고 기존 snapshot은 유지됨
- [ ] 생산계획 수정 페이지에서 프로젝트 Item이 자동 표시됨
- [ ] 행 추가로 사용자 추가 계획 항목 저장 가능
- [ ] 생산계획 Excel 양식 다운로드와 업로드 가능
- [ ] 프로젝트 상세 생산관리 section에서 생산계획표 아래 캘린더 형식 표 표시
- [ ] 생산관리 업무 페이지 펼침 영역에는 캘린더 미표시
- [ ] 검수공휴일 문구가 UAT 화면에 보이지 않음
- [ ] 생산계획 항목이 예정일 빠른 순으로 표시됨
- [ ] Excel 양식 다운로드 열너비가 보기 좋게 표시됨
- [ ] 캘린더가 시작예정일부터 종료예정일까지 연속 날짜를 표시
- [ ] 캘린더 주말/공휴일 색상 표시
- [ ] 예정일 입력 가능
- [ ] 구매/생산관리/제조/품질/물류 담당자 지정 가능
- [ ] 담당자 미지정 시 영업담당자 fallback 안내
- [ ] dev-production만 수정 가능
- [ ] 저장 후 조회화면 이동
- [ ] Admin만 이력 조회
- [ ] PC table / Mobile card
- [ ] Console 오류 없음
