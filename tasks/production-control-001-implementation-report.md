# TASK-PRODUCTION-CONTROL-001 구현 보고 — Item별 생산계획·자동 실적·가로 막대 일정

상태: `기존 기능·Change 010 원격 main 반영 완료 / Change 011 사용자 최종 일괄 검수·게시 승인`

## 기준선과 범위

- instructionChainRead: `true`
- taskType: `BUGFIX` (`Change 010`; 본체는 `APPROVED_FEATURE_IMPLEMENTATION` 이력 유지)
- branch: `fix/task-production-control-001-edit-race-010`
- implementationBaseline: `8d6ae914e8f748430337a5dd0ad79e7565730733`
- latestChange: `tasks/production-control-001-change-010.md`
- originalExperimentBaseline: `de8e05b`
- planningSource: `tasks/production-control-001-planning.md`
- codexReviewSource: `tasks/production-control-001-review.md`
- finalPlanningSource: `docs/43-production-control-plan.md`
- 사용자 확인: Fable Round 8 전체 요약 `요약 확인`
- 포함: Item별 단일 현재 제조·생산계획 양식, 계획 항목별 1:1 실적 연결, 새 프로젝트 snapshot, 프로젝트별 계획 기간·항목·연결 수정, 부서 원본 기반 자동 실적, desktop/mobile 조회 UI
- Change 010 포함: 동일한 현재 양식을 다시 선택하는 후행 상태 동기화가 빠른 편집 진입을 취소하지 않도록 하는 Frontend 최소 보정과 회귀 테스트
- Change 010 제외: Backend·API·DB·migration·권한·양식 내용·화면 디자인·18단계 workflow 계산 변경

## 해결한 업무 문제

기존 생산계획은 항목명·필수 여부·단일 예정일 중심이라 실제 구매·자재·제조·품질·물류 입력과 연결되지 않았다. 생산관리 담당자는 각 부서 화면을 오가며 진행 상태를 수기로 대조해야 했고, 계획 기간과 실적 기간의 겹침·지연도 한눈에 확인할 수 없었다.

이번 구현으로 관리자는 코드 수정 없이 Item별 제조 항목과 생산계획 항목을 구성하고 한 계획 항목을 실적 데이터 하나와 연결할 수 있다. 새 프로젝트는 생성 당시 양식과 연결을 복사해 보존하며, 생산관리 탭은 실제 부서 입력값을 읽어 계획 대비 실적·근거·진행률과 가로 막대 일정을 자동 표시한다.

사용자 검수에서 연결형 양식이 기존 `양식 종류` 밖의 별도 전환 화면으로 분리되고 모든 계획 항목의 연결 입력이 동시에 펼쳐져 있다는 UX 결함이 확인됐다. Change 002에서 두 연결형 양식을 기존 종류 목록에 통합하고 선택한 계획 항목 하나만 펼치는 편집 구조로 보정했다.

Change 003에서는 체크박스 다중 연결과 전체 양식 version 복제가 불필요하다는 사용자 정책을 반영했다.

Change 004에서는 품질 양식도 사용자에게 `v1`, `v2`를 노출하지 않는 현재 양식 하나로 통일했다. 전진검수·FAT와 전역 제조 작업 단계처럼 실제 관리 대상이 아닌 중복 양식을 목록에서 제거하고, IQC·OQC 검사 항목 하나하나를 생산계획 실적에 연결할 수 있게 했다. 아래 문서에서 Change 004와 충돌하는 이전 version·검사 전체 연결 설명은 이 최신 계약으로 대체한다.

Change 005에서는 Item별 제조 항목을 모두 입력해도 `입력값을 확인해 주세요.`만 표시되며 저장되지 않던 결함을 고쳤다. 원인은 현재 생산계획이 참조하는 제조 항목을 교체할 때 새 항목을 저장하기 전에 새 연결을 먼저 요구하는 순서 교착과, Frontend가 HTTP 400의 구체 field 오류를 일반 문구로 덮어쓰는 처리였다. 제조 현재 양식은 먼저 저장하고 기존 프로젝트 snapshot은 그대로 유지한다. 현재 생산계획에서 삭제·교체된 제조 identity를 참조하는 항목만 `연결 재설정 필요`로 표시하며, 이를 다시 연결하기 전까지 새 프로젝트 생성은 fail-closed로 차단한다.

Change 006에서는 OQC 검사 자체의 단계별 판정과 재검사 이력은 유지하되 생산계획 실적 연결은 세부 OQC 항목이 아닌 패널별 `OQC 합격` 최종 사건 하나만 선택하도록 정책을 정리했다. 현재 양식의 OQC 세부 연결만 aggregate로 전환하고 기존 프로젝트에 snapshot된 세부 연결은 조회 호환을 위해 보존한다.

Change 007에서는 연결형 생산관리 탭의 6열 헤더를 밝은 중립 배경·검은 글자로 바꿔 가독성을 회복했다. 계획·실적 일정표에는 전체 기간에서 최대 6개를 자동 산출하는 날짜 축과 동일 위치의 세로 기준선을 추가해 막대 위치를 날짜로 바로 해석할 수 있게 했다.

Change 008에서는 조회 제목을 `생산계획표`로 바꾸고, 프로젝트 생산계획 항목마다 선택 담당자·필요 인원·생산관리 코멘트를 저장하고 조회할 수 있게 했다. 담당자는 활성 업무 담당자 후보 전체에서 중복 없이 선택하며, 필요 인원은 선택값이지만 입력하면 1~999명의 정수만 허용한다. 이 정보는 일정 계획용 metadata이므로 내 업무·알림 수신자·권한·실적 계산은 변경하지 않는다. 자동 실적 연결은 계속 저장·계산하되 조회 표에서는 숨기고 담당자·필요 인원·코멘트 열로 대체했다.

Change 010에서는 양식 카탈로그 로드 직후 사용자가 `수정`을 누르면 같은 version을 다시 적용하는 후행 효과가 편집 상태를 닫을 수 있던 경쟁을 제거했다. Item 또는 양식 domain이 실제로 달라질 때만 새 현재 양식을 선택하고, 이미 선택된 같은 version에는 행·편집 상태 초기화를 반복하지 않는다.

## 구현 결과

### 양식과 권한

1. 양식 종류는 `자재 수입검사`, `LQC 검사`, `OQC 자체검수`, `Item별 제조 양식`, `생산계획·실적 연결` 5개만 표시한다.
   - 전진검수와 FAT는 패널별 통합 적합/부적합 판정이므로 항목 양식에서 제거했다.
   - 전역 `제조 작업 단계`는 Legacy 프로젝트와 최초 Item별 seed에 필요한 내부 기준만 보존하고 사용자 관리 목록에서 제거했다.
2. System Administrator와 지정된 제조/생산계획 양식 관리자가 권한 범위의 양식을 편집한다.
3. IQC·LQC·OQC와 Item별 제조·생산계획 양식은 모두 현재 양식 하나만 조회하고 `수정 → 저장/취소`로 관리한다. 사용자는 version 번호, 초안, 활성화, 보관을 다루지 않는다.
4. 생산계획 항목 하나에는 source 하나만 1:1로 연결한다.
   - 제조: 해당 Item 제조 양식의 불변 `definition_key`
   - IQC: 현재 검사 양식의 검사 항목 `definition_key`
   - OQC: 패널별 `OQC 합격` 최종 사건
   - LQC: 해당 Item 제조 단계의 `definition_key`
   - 구매·자재·전진검수·FAT·물류: 서버가 제공하는 고정 사건 catalog
5. 기본은 조회 상태이며 `수정 → 저장/취소`로 바꾸고, 저장할 때 새 version을 생성하지 않는다.
6. 생산계획 항목은 이름·필수 여부·연결 실적을 한 줄로 표시하고, 선택한 항목 하나만 펼쳐 드롭다운에서 실적 하나를 편집한다.
7. 제조 항목은 `No. / 제조 항목 / 구분 / 관리` 헤더에 맞춘 행 구조로 표시한다.
8. 품질 성적서가 참조하는 과거 내부 snapshot은 증빙 정합성을 위해 삭제하지 않는다. 이 내부 snapshot은 사용자에게 version으로 노출하지 않으며 새 검사는 저장 시점의 현재 양식을 사용한다.
9. 제조 항목명·구분을 수정할 때 기존 행의 불변 identity를 유지하므로 생산계획 연결은 그대로 유지한다. 연결된 행 자체를 삭제하고 새 행으로 교체한 경우에만 현재 생산계획에서 다시 연결한다.
10. 제조 양식 변경은 저장 이후 새 프로젝트에만 적용된다. 이미 생성된 프로젝트의 제조 항목과 연결은 생성 당시 snapshot으로 고정된다.

### 새 프로젝트와 기존 프로젝트

1. migration 당시 기존 프로젝트는 `Legacy`로 고정한다.
2. Item에 유효한 Active 제조 양식과 Active 생산계획 양식이 모두 있을 때만 이후 새 프로젝트를 `LinkedV1`로 생성한다.
3. 프로젝트 생성 transaction에서 현재 양식 두 개와 1:1 연결 유효성을 다시 확인하고 제조 항목·계획 항목·연결을 한 세트로 snapshot한다.
4. 일부 snapshot만 남는 프로젝트는 허용하지 않는다.
5. 기존 프로젝트, 양식이 없는 Item과 양식 활성화 전 생성 프로젝트는 기존 단일 예정일·캘린더 화면을 그대로 사용한다.
6. bulk 생성과 UL891 생성도 같은 snapshot 규칙을 사용한다.

### 프로젝트 생산계획

1. `LinkedV1` 생산관리 탭은 `계획 항목 / 계획 기간 / 실적 기간 / 진행 / 상태 / 실적 연결` 6열 표를 표시한다.
2. 생산관리 정·부 담당자는 프로젝트 계획 항목, 필수 여부, 계획 시작·종료, 비고와 연결만 수정한다.
3. 프로젝트 제조 snapshot과 자동 실적 값은 읽기 전용이다.
4. 항목을 펼치면 부서 원본 fact별 대상, 시작·완료일과 현재 상태를 근거로 확인한다.
5. 계획과 실적은 같은 시간축의 가로 막대로 표시한다.
6. 모바일은 표 축소판이 아니라 항목별 카드, 근거 펼침과 실제 가로 막대 일정으로 재구성했다.

### 자동 실적 규칙

1. 실적은 별도 복사본으로 저장하지 않고 조회 시 원본 데이터에서 계산한다.
2. 연결 대상 전체의 최초 유효 처리일을 실적 시작, 전체 완료일을 실적 종료로 사용한다.
3. 진행률은 완료 대상 수 / 전체 적용 대상 수로 계산한다.
4. IQC는 구매품목 도착분, 제조·LQC·OQC·전진검수·FAT·물류는 개별 패널을 실제 처리 단위로 유지한다.
5. LQC 결과에는 제조 단계 `definition_key`를 함께 고정해 이후 양식명·순서 변경으로 연결이 흔들리지 않게 했다.
6. IQC는 연결한 검사 항목이 적합 또는 해당 없음이고 해당 검사 전체가 합격 확정되었을 때 그 항목의 실적 완료로 계산한다.
7. OQC는 세부 항목 수와 관계없이 패널의 OQC 전체 합격이 확정되었을 때 완료로 계산한다. 기존 프로젝트에 snapshot된 OQC 세부 항목 연결은 과거 의미를 바꾸지 않고 항목별 projection으로 계속 읽는다.
8. FAT 비필수 프로젝트는 FAT 연결이 snapshot에 있어도 실적 분모와 화면의 유효 연결 요약에서 제외한다.
9. source 이력이 아직 없는 경우 500 오류가 아니라 `대기`, 0%와 근거 0건을 반환한다.
10. 기존 18단계 workflow와 프로젝트 대표 진행률은 변경하지 않았다.

## DB·Backend·API

### Migration

- `0058_production_control_linked_plans.sql`
  - Item별 제조/생산계획 template version·항목·connection
  - 프로젝트 model version·template snapshot·project override
  - additive migration이며 기존 production plan을 `Legacy`로 보존
- `0059_production_control_lqc_identity.sql`
  - LQC 응답에 제조 단계 `manufacturing_definition_key` 추가
  - 기존 응답을 파괴하거나 의미를 추측해 일괄 변환하지 않음
- `0060_production_control_single_current.sql`
  - 제조·생산계획 양식을 Item별 현재 양식 하나로 단일화
  - 마스터 계획 항목의 실적 연결을 최대 1개로 제한
  - 프로젝트가 참조하지 않는 이전 version 삭제
  - 프로젝트가 FK로 참조하는 이전 version은 작은 식별 행만 남기고 중복 항목 payload 삭제
- `0061_quality_current_forms_and_stage_links.sql`
  - IQC·OQC 검사 항목에 snapshot을 넘어 유지되는 불변 `definition_key` 추가
  - 품질 양식의 남은 Draft를 정리하고 현재 Active 양식 하나만 관리 대상으로 고정
  - 생산계획 master·project 연결이 IQC·OQC 검사 항목을 참조할 수 있도록 제약 확장
  - 과거 전체 합격 연결과 완료 성적서 참조는 호환 보존
- `0062_production_control_oqc_aggregate_link.sql`
  - 현재 생산계획 양식의 OQC 세부 항목 연결을 `OQC 합격` aggregate 연결로 정리
  - 새 현재 양식에서 OQC `source_definition_key` 저장 차단
  - 기존 프로젝트의 OQC 세부 연결 snapshot은 변경하지 않고 조회 호환 보존
- `0063_production_plan_item_staffing.sql`
  - 프로젝트 생산계획 항목에 선택 담당자와 필요 인원 nullable metadata 추가
  - 기존 프로젝트 값은 미지정으로 보존하고 필요 인원 `1~999` DB 제약과 담당자 FK 적용

Rollback은 destructive down migration 대신 배포 전 DB backup과 application forward-fix를 사용한다. 이미 생성된 `LinkedV1` snapshot을 Legacy로 재해석하거나 삭제하지 않는다.

### 주요 API

- `/api/production-control/templates`
- `/api/production-control/templates/manufacturing/{productTypeId}/current`
- `/api/production-control/templates/planning/{productTypeId}/current`
- `/api/production-control/templates/{domain}/{productTypeId}/versions/{versionId}` — 내부 row identity는 유지하지만 새 version은 만들지 않고 현재 행을 직접 저장
- `/api/form-templates/{family}/{templateKey}/current` — IQC·LQC·OQC 현재 양식 조회/저장
- 기존 `/api/projects/{projectId}/production-planning` GET/PATCH는 model version에 따라 Legacy/LinkedV1 계약을 반환한다.

서버는 template 관리 권한, 프로젝트 담당자 수정 권한, 제조 참조 존재, 정확히 1개 연결, 동시 수정 충돌과 project snapshot 불변조건을 강제한다.

## Frontend·UX

- 양식 관리 화면에서 Item, 제조 양식, 생산계획·연결을 순서대로 선택한다.
- 연결형 양식도 다른 검사·제조 양식과 같은 `양식 종류` 목록에서 선택한다.
- 생산계획 편집은 요약 행에서 필요한 항목 하나만 `편집`해 반복 연결 목록이 화면을 점유하지 않는다.
- 모든 사용자용 version·초안·활성화·보관 UI를 제거하고 현재 양식의 조회/수정/저장 상태와 적용 원칙을 고정 표시한다.
- 실적 후보는 체크박스 묶음이 아니라 단일 드롭다운으로 표시한다. 제조·LQC·IQC는 세부 단계를 선택하고 OQC·전진검수·FAT는 최종 합격 사건 한 건을 선택한다.
- 제조 입력은 빈 항목명·100자 초과를 저장 전에 행 단위의 구체 문구로 안내하며, 서버 validation도 일반 문구로 숨기지 않는다. `TASK-MANUFACTURING-BATCH-001` Change 003부터 일반/조립 사용자 구분은 제거되고 등록한 모든 제조 단계가 선택 일괄 완료 대상이다.
- 제조 항목 교체로 현재 생산계획 연결이 끊기면 양식 저장 성공 뒤 끊긴 항목 수를 안내하고, 생산계획 요약 행과 드롭다운에 `연결 재설정 필요`를 표시한다.
- 프로젝트 생산관리 탭은 조회 중심이며 단일 `생산계획 수정`으로 별도 입력 상태에 진입한다. 조회 제목은 `생산계획표`이고 `계획 항목·계획 기간·실적 기간·진행·상태·담당자·필요 인원·코멘트`를 표시한다.
- 생산계획 수정은 항목별 담당자·필요 인원·생산관리 코멘트를 입력한다. 실적 연결 설정은 편집 화면에만 남아 자동 실적 계산 기준을 보존한다.
- 흑백 wireframe과 사각형 원칙을 유지하고 상태 의미색만 사용한다.
- 전역 wireframe 규칙이 간트 막대를 투명하게 만드는 충돌을 정보 표시 예외로 수정했다.

## 기술적 결정과 검토한 대안

| 결정 | 채택 이유 | 보류·폐기한 대안 |
| --- | --- | --- |
| 원본 fact 조회 시 실적 projection | 부서 원본과 생산계획 복사본의 불일치·재조정 제거 | 실적 날짜를 별도 저장하고 event마다 갱신 |
| Legacy/LinkedV1 명시 분기 | 기존 프로젝트 UX·양식 불변 보존 | migration 뒤 모든 프로젝트를 새 model로 전환 |
| 프로젝트 생성 시 두 양식 원자 snapshot | 양식 변경 후에도 생성 당시 계약 유지 | 실행 시점마다 현재 Active 양식 재조회 |
| 불변 `definition_key` 연결 | 제조 항목명·순서 전면 변경 허용 | 이름 또는 sequence 기반 연결 |
| 기존 양식 관리 확장 | 관리자와 지정 부서장이 익숙한 권한·version 패턴 재사용 | 별도 독립 관리자 앱 |
| 단일 현재 양식 직접 수정 | 불필요한 전체 복제와 v2·v3 누적 방지 | 편집마다 전체 version snapshot 추가 |
| 계획 항목과 실적 1:1 | 입력 밀도와 자동 실적 기준을 명확히 함 | 모든 source를 반복 표시하는 다중 체크 |
| 품질 이력용 내부 snapshot 보존 | 완료 성적서의 당시 항목·판정을 나중 편집으로 바꾸지 않음 | 과거 품질 template/version 물리 삭제 |
| 전역 제조 양식 사용자 목록 제거 | 실제 신규 프로젝트는 Item별 제조 양식을 사용하므로 이중 관리 방지 | 전역·Item별 제조 양식 동시 노출 |

## 시행착오 및 폐기한 접근

1. LQC 완료를 최신 전체 검사 상태만으로 연결하려던 방식은 어떤 제조 단계의 LQC인지 보존하지 못해 폐기했다. 응답 확정 시 `definition_key`를 함께 저장하도록 보완했다.
2. 검사 시도가 없는 OQC source의 SQL boolean이 `NULL`이 되어 계획 조회가 500을 반환했다. 미시작을 `false`로 안전하게 projection하도록 수정했다.
3. FAT 비필수 프로젝트에서 raw snapshot 연결명을 그대로 표시하면 분모와 연결 수가 달라 보였다. 실제 적용 근거의 고유 source명을 우선 표시하도록 정리했다.
4. wireframe 전역 규칙이 `<i>` 기반 간트 막대 배경을 투명하게 덮었다. 정보 전달 막대만 명시적 흑백 예외로 고정했다.
5. 양식 편집용 article과 프로젝트 조회용 row가 같은 `.production-control-plan-row` class를 사용해 조회 표의 6열 grid가 편집 화면에 잘못 적용됐다. 편집 전용 class로 분리하고, 항목 하나만 펼치는 구조로 반복 노출도 함께 제거했다.

## 주요 변경 위치

- Backend: `backend/src/Emi.Qms.Api/ProductionPlanning/`, `ProjectStore.cs`, `ManufacturingStore.cs`, `QualityInspectionStore.cs`, `Ul891SetStore.cs`, `Program.cs`
- Migration: `database/migrations/0058_production_control_linked_plans.sql`, `0059_production_control_lqc_identity.sql`, `0060_production_control_single_current.sql`, `0061_quality_current_forms_and_stage_links.sql`, `0062_production_control_oqc_aggregate_link.sql`, `0063_production_plan_item_staffing.sql`
- Frontend: `frontend/src/ProductionControlTemplateWorkspace.tsx`, `productionControlTemplates.ts`, `FormTemplateManagementPage.tsx`, `App.tsx`, `api.ts`, `projects.ts`, `styles.css`, `design-system/wireframe.css`
- Tests: `backend/tests/Emi.Qms.Api.Tests/ProductionPlanningApiTests.cs`, `PostgreSqlMigrationTests.cs`, `frontend/tests/App.test.tsx`, `frontend/tests/FormTemplateManagementPage.test.tsx`, `frontend/e2e/full-stack/sales-kpi-form-templates.full-stack.spec.ts`

## 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend Release solution build | PASS — 경고 0, 오류 0 |
| Backend 전체 회귀 | PASS — `429/429` |
| 생산계획 API·Migration 집중 회귀 | PASS — 단일 현재 양식 직접 저장, 1:1 검증, 새 프로젝트 snapshot과 `0060` upgrade 포함 `57/57` |
| 품질 현재 양식·단계 연결 Backend 회귀 | PASS — 현재 양식 저장, 권한, `0061` migration, IQC·OQC 단계 연결과 기존 aggregate 호환 포함 `57/57` |
| Backend 단일 양식 고위험 테스트 | PASS — 직접 저장·다중 연결 거부·반복 생성 시 동일 양식 유지 `1/1` |
| Backend 제조 항목 교체·snapshot 회귀 | PASS — 제조 저장, 기존 프로젝트 불변, 재연결 전 새 프로젝트 차단, 재연결 후 신규 snapshot `1/1` |
| Migration `0060` upgrade 테스트 | PASS — Draft/Active 단일화·이전 payload 정리·1:1 연결 `1/1` |
| Migration `0062` OQC aggregate upgrade | PASS — 현재 양식의 OQC 세부 key는 제거하고 기존 프로젝트 snapshot은 보존 `1/1` |
| Migration `0063` 계획 항목 인력 metadata upgrade | PASS — 기존 행 nullable 보존, 담당자 FK, 필요 인원 유효값·0 거부 `1/1` |
| Frontend production build | PASS — 기존 500kB 초과 chunk warning만 유지 |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend 전체 unit | PASS — 22 files, `140/140` (`testTimeout=30000`) |
| Frontend 품질·생산계획 양식 집중 unit | PASS — 현재 양식 3종, 중복 양식 제거, IQC 세부 단계와 OQC aggregate 드롭다운 |
| Frontend 제조 저장 오류 집중 unit | PASS — 구체 입력 validation, 서버 field 오류 노출, 끊긴 연결 안내·재설정 표시 `3/3` |
| 실제 Backend API | PASS — template catalog의 OQC `definitionKind=None`·세부 정의 0건, LinkedV1 계획 GET/PATCH, source 이력 없음 안전 응답 |
| Desktop browser | PASS — `생산계획표` 8열 표·근거·가로 막대와 항목별 담당자·필요 인원·코멘트 입력, 가로 overflow 없음 |
| Desktop 생산계획 양식 browser | PASS — OQC 선택지 `품질 · OQC 합격` 1개, OQC 세부 선택지 0개, 전진검수·FAT aggregate 선택지 유지 |
| Desktop 생산관리 탭 browser | PASS — 밝은 헤더 `rgb(241, 241, 241)`·검은 글자, 날짜 축 6개, 라벨 겹침·잘림·가로 overflow·console error 없음 |
| Mobile 390×844 browser | PASS — 계획 카드·근거·실제 가로 막대, 가로 overflow 없음 |
| Mobile 390px 일정표 browser | PASS — 날짜 축 6개, 라벨 겹침·잘림·가로 overflow 없음 |
| Change 010 양식 관리 집중 unit | PASS — `3/3`, 같은 suite 연속 실행 `10/10` |
| Change 010 Frontend 전체 unit | PASS — 25 files, `177/177` |
| Change 010 Frontend lint·typecheck·build | PASS — lint error 0·기존 Fast Refresh warning 1, typecheck 성공, production build 성공·기존 chunk 경고 유지 |
| Change 010 Full-Stack browser | PASS — desktop에서 `수정` 뒤 두 animation frame 이후에도 `저장`이 유지되는 회귀를 포함해 PR #81 Full-Stack `57/57` 통과 |
| Change 010 표준 CI | PASS — PR #81 Backend·Frontend·Full-Stack `3/3`, merge SHA main CI run `31152166786` `3/3` |
| Change 010 Git 게시 | PASS — PR #81 squash merge, `main` SHA `d86e9f0cd417ddca445d9980188375c847d057bb` |

Backend 전체 회귀는 통합 PostgreSQL 시나리오와 migration `0063` upgrade를 포함해 `429/429`을 통과했다. Change 008 Frontend 전체 `140/140`·실제 고정 검수 화면도 별도로 통과했으며 새 open P0/P1/P2 Finding은 남지 않았다.

## Fable 5 사용량

| 시점 | 5시간 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 기획 직전 | 사용 70% / 잔여 30%, 20:00 초기화 | 사용 6% / 잔여 94% | 사용 11% / 잔여 89% |
| 1차 기획 직후 | 사용 70% / 잔여 30%, 20:00 초기화 | 사용 6% / 잔여 94% | 사용 11% / 잔여 89% |
| 2차 기획 직전 | 사용 79% / 잔여 21% | 사용 6% / 잔여 94% | 사용 12% / 잔여 88% |
| 2차 기획 직후 | 사용 86% / 잔여 14% | 사용 7% / 잔여 93% | 사용 13% / 잔여 87% |

## Finding과 잔여 위험

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution·후속 |
| --- | --- | --- | --- | --- |
| `PC-001-F01` | P1 | Resolved | source 이력이 없는 품질 연결의 nullable 완료값이 조회 500을 만들었다. | `NULL`을 미시작으로 projection하고 통합 회귀에 포함했다. |
| `PC-001-F02` | P1 | Resolved | LQC와 제조 단계의 안정적 identity가 없어 이름 변경 시 잘못 연결될 수 있었다. | migration `0059`와 finalize 저장으로 불변 key를 보존한다. |
| `PC-001-F03` | P2 | Resolved | 전역 wireframe 규칙이 계획 막대를 투명하게 만들었다. | 생산계획 간트 막대를 정보 표시 예외로 고정하고 desktop/mobile 실제 화면을 확인했다. |
| `PC-001-F05` | P2 | Resolved | 연결형 양식이 기존 종류 밖에 분리되고 편집용 row가 조회용 6열 CSS와 충돌해 초안의 정렬과 작업 대상 구분이 무너졌다. | 양식 종류에 두 항목을 통합하고 편집 전용 class·단일 펼침 편집·정렬된 제조 표로 보정했다. |
| `PC-001-F06` | P2 | Resolved | 계획 항목마다 모든 source 체크박스가 반복되고 편집할 때 전체 양식 version이 계속 누적됐다. | 1:1 드롭다운과 단일 현재 양식 직접 저장으로 변경하고 이전 중복 payload를 migration에서 정리했다. |
| `PC-001-F07` | P2 | Resolved | IQC·LQC·OQC에 version 관리가 남고 전진검수·FAT·전역 제조 양식이 함께 노출되어 관리 대상이 중복됐다. | 품질 현재 양식 UX로 통일하고 실제 편집 대상 5개만 양식 종류에 남겼다. |
| `PC-001-F08` | P1 | Resolved | IQC·OQC 생산계획 연결이 검사 전체 합격만 지원해 당시 요청한 항목별 계획 실적을 계산할 수 없었다. | 품질 항목 불변 key와 단계별 projection을 추가했다. 이후 OQC 연결 정책은 `PC-001-F10`에서 aggregate로 변경했다. |
| `PC-001-F09` | P1 | Resolved | 연결된 제조 항목 교체 시 제조 저장 전에 생산계획 재연결을 요구하는 순서 교착이 발생했고, Frontend가 구체 validation을 일반 문구로 숨겼다. | 제조 양식을 먼저 저장하고 현재 계획의 끊긴 연결을 명시적으로 재설정하게 했으며, 기존 프로젝트 snapshot과 새 프로젝트 fail-closed 경계를 회귀 검증했다. |
| `PC-001-F10` | P2 | Resolved | OQC 내부 검사 항목이 생산계획 연결 option으로 모두 노출되어 전진검수·FAT와 다른 과도한 입력을 요구했다. | OQC를 패널별 최종 합격 단일 사건으로 바꾸고 현재 양식만 migration으로 정리했으며 기존 프로젝트 세부 snapshot은 호환 보존했다. |
| `PC-001-F11` | P2 | Resolved | 계획 대비 실적 헤더의 검은 채움이 표 가독성을 낮추고 일정 막대에는 위치를 해석할 날짜 축이 없었다. | 밝은 중립 헤더와 최대 6개 날짜 축·동일 위치 세로 기준선을 추가하고 desktop·390px 겹침·잘림·overflow를 검증했다. |
| `PC-001-F12` | P2 | Resolved | 생산계획 항목의 계획 담당자·필요 인원을 기록할 수 없고 조회 표가 내부 실적 연결 설정을 노출해 현장 배치 계획을 바로 읽기 어려웠다. | nullable 담당자·필요 인원 metadata와 생산관리 코멘트를 저장·이력화하고, 조회 표는 8열 생산계획표로 바꾸되 자동 실적 연결 계약은 유지했다. |
| `CI-FRONTEND-FORM-TEMPLATE-001` | P2 | Resolved | 같은 현재 version을 다시 선택하는 후행 효과가 `editing=false`를 적용해 빠른 `수정` 직후 저장 버튼을 없앨 수 있었고 Frontend CI에서 두 차례 재현됐다. | 동일 version 재선택을 생략하고 집중 unit `3/3`·연속 `10/10`·전체 `177/177`, PR·main 표준 CI `3/3`으로 고정했다. |
| `CI-FULLSTACK-FORM-TEMPLATE-FIXTURE-001` | P2 | Resolved | 첫 Change 010 PR Full-Stack은 현재 제조 양식 없이 `수정`을 기다렸고, 두 번째 실행은 reload 뒤 유지된 관리자 값을 다시 선택해 정상 사용자 전환 규칙으로 목록 route로 이동했다. | UI로 현재 양식을 준비하고 reload 뒤 양식 관리 route를 확인하되 유지된 사용자를 다시 선택하지 않는 self-contained fixture로 보정했다. |
| `PC-001-F04` | P3 | Backlog | 대규모 프로젝트에서 조회 시 실적 projection 비용이 증가할 수 있다. | 실제 성능 측정에서 병목이 확인될 때 query 최적화 또는 파생 cache Task로 분리한다. |

Open P0/P1/P2: `0/0/0`.

## 개인정보·secret 검토

- 문서와 캡처는 개발 역할 계정과 합성 프로젝트만 사용한다.
- token, password, Authorization header, tenant/client/object id, webhook URL과 실제 고객·사용자 개인정보를 기록하지 않았다.
- 실제 provider 발송과 외부 mutation은 실행하지 않았다.

## 사용자 사용 방법

### 새 양식 준비

1. 관리자 또는 지정 양식 관리자로 `양식 관리`에 들어간다.
2. `양식 종류`에서 `Item별 제조 양식`을 선택하고 Item을 고른다.
3. `수정`을 눌러 제조 항목과 조립 의미 단계를 구성하고 `저장`한다.
4. `양식 종류`에서 `생산계획·실적 연결`을 선택하고 같은 Item을 고른다.
5. `수정`을 누른 뒤 필요한 계획 항목 행을 하나씩 펼쳐 이름·필수 여부를 정하고, 드롭다운에서 제조·LQC·IQC 세부 단계 또는 `OQC 합격`을 포함한 고정 부서 사건 하나를 선택한 뒤 `저장`한다.
6. 이후 새로 생성한 해당 Item 프로젝트부터 연결형 생산계획이 적용된다.

### 프로젝트 계획과 실적 확인

1. 프로젝트 상세의 `생산관리` 탭을 연다.
2. `생산계획 수정`에서 각 항목의 계획 시작·종료, 담당자, 필요 인원, 생산관리 코멘트와 필요한 프로젝트 전용 연결을 저장한다.
3. 조회 화면의 `생산계획표`에서 계획·실적 기간, 진행률, 상태, 담당자, 필요 인원과 코멘트를 확인한다.
4. 행 또는 모바일 `근거 N건`을 눌러 어떤 품목·패널이 완료 또는 대기인지 확인한다.
5. 하단 가로 막대 위 날짜 축을 기준으로 계획과 실적의 겹침·지연을 비교한다.

## 사용자 검수 결과와 남은 항목

- 기존 생산계획 기능의 자동·사용자 검수와 원격 main 승격은 완료됐다.
- Change 010은 새로운 화면·정책·입력 방법을 추가하지 않고 기존 `수정 → 저장` 동작의 간헐적 초기화만 제거한다. 로컬 자동 검증, PR #81 표준 CI와 merge SHA main CI를 모두 통과했다.
- 운영 양식 content 변경, Persistent UAT data mutation과 실제 provider는 Change 010 범위 밖이다.

## 안전·게시 경계

- Change 010은 최신 `origin/main`에서 분리한 bugfix branch에서만 구현했다.
- Persistent UAT, Azure runtime, DB와 실제 provider는 변경하지 않는다.
- 사용자는 Change 010 commit·push·PR·main merge와 이후 문서 PR #80 병합까지 명시 승인했다.
- 운영 Azure 재배포는 이번 승인 범위에 포함되지 않는다.
- 따라서 원격 `main`에는 Change 010이 포함되었지만 현재 Azure 운영 image는 Change 019 release 기준선을 유지한다. 재배포는 별도 승인 후 수동 운영 release로 수행한다.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | 본 문서 `사용자 사용 방법` |
| User manual | 포함됨 | 본 문서 `사용자 사용 방법` |
| Roadmap update | Change 011 사용자 최종 일괄 검수·게시 승인까지 갱신됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 기존 기능·Change 010 완료 / Change 011 최종 게시 승인 | `tasks/production-control-001-user-validation-checklist.md` |

## Change 011 — 모든 프로젝트의 전용 계획·실적 연결과 조회 화면 단일화

### 승인 계약과 구현

- 양식 관리의 Item별 생산계획·실적 연결은 저장 뒤 생성되는 프로젝트의 초기 snapshot에만 적용하며 기존 프로젝트를 소급 변경하지 않는다.
- 기존 `Legacy` 프로젝트도 프로젝트 안에서 기존 행을 유지한 채 계획 시작·종료와 항목별 실적 데이터 하나를 저장한다. 기존 단일 예정일은 첫 진입에서 같은 날의 시작·종료로 정규화한다.
- Legacy 제조 실적은 저장 당시 양식 version이 달라도 안정된 제조 항목 코드가 같으면 과거·현재 실행 근거를 함께 projection한다. 과거 프로젝트에 세부 제조 identity가 남지 않은 LQC는 오연결 방지를 위해 신규 연결 선택만 중지한다.
- 생산계획 header가 아직 없는 오래된 프로젝트에도 실적 선택 목록과 제조 항목을 제공하고, 첫 저장 transaction에서 프로젝트 전용 Legacy 계획을 만든다.
- 프로젝트 조회는 model version과 관계없이 `연결 실적`을 포함한 단일 생산계획표와 계획 흰색·실적 검은색 2중 막대 일정표를 사용한다. 날짜별 체크형 캘린더와 해당 영업일 조회 요청은 실제 사용자 경로에서 제거했다.
- 미저장 추가 행은 삭제 시 즉시 제거하고, 저장된 행은 비활성화 대상으로 보존한다. 화면 저장 전과 서버 저장 시 활성 행 순서를 `1..N`으로 다시 부여하며 삭제 행은 필수값·연결·순번 검증에서 제외한다.
- 반복 저장에서 비활성 행의 과거 순번과 활성 행의 새 순번이 충돌하지 않도록 현재 활성 행만 사용 가능한 임시 순번 구간으로 옮긴 뒤 최종 `1..N`을 적용한다.
- UL891 세트 공통 구조·전체 기본계획·세트별 일정 overlay와 프로젝트 snapshot 불변조건은 유지했다.
- DB schema와 migration은 변경하지 않았다.

### 변경 위치

- Backend: `backend/src/Emi.Qms.Api/ProductionPlanning/ProductionPlanningStore.cs`
- Backend 회귀: `backend/tests/Emi.Qms.Api.Tests/ProductionPlanningApiTests.cs`
- Frontend: `frontend/src/App.tsx`, `frontend/src/styles.css`
- Frontend unit·Full-Stack 회귀: `frontend/tests/App.test.tsx`, `frontend/e2e/full-stack/project-registration.full-stack.spec.ts`, `project-lifecycle-user-validation.full-stack.spec.ts`, `project-lifecycle-stress-user-validation.full-stack.spec.ts`
- 계약: `tasks/production-control-001-change-011.md`

### 자동 검증

| 검증 | 결과 |
| --- | --- |
| Backend Release build | PASS — 경고 0, 오류 0 |
| 생산계획 Backend API 전체 class | PASS — `26/26` |
| Legacy 기간·연결·기존 제조 실적·행 삭제 순번 집중 회귀 | PASS |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend 전체 unit | PASS — `29 files`, `213/213` |
| Frontend production build | PASS — 기존 500kB 초과 chunk warning만 유지 |
| Full-Stack 회귀 source 동기화 | PASS — 단일 예정일·체크 달력 기대를 기간·1:1 연결·2중 막대 기준으로 갱신 |
| `git diff --check` | PASS |

Change 011 Open P0/P1/P2는 `0/0/0`이다. 사용자는 실제 5174 화면 확인 뒤 2026-08-14 원격 `main` 병합과 Azure 공개배포를 명시 승인했다. 표준 전체 Backend·Frontend·Full-Stack 검증은 Ready PR의 필수 `CI Gate`에서 실행한다.

### 사용자 검수·게시 승인

1. 기존 프로젝트 생산계획 수정에서 각 행의 시작·종료일과 연결할 실적을 저장한다.
2. 여러 행을 추가하고 중간 행을 삭제한 뒤 다시 추가·저장해 오류 없이 순번이 1부터 연속인지 확인한다.
3. 조회 화면에 날짜별 체크 달력이 없고, `연결 실적` 열과 계획 흰색·실적 검은색 막대가 표시되는지 확인한다.
4. 새 프로젝트는 생성 당시 양식 기본값으로 시작하고, 기존 프로젝트의 수정값은 다른 프로젝트와 양식 관리 기본값을 바꾸지 않는지 확인한다.

### 게시 경계

- 사용자는 다른 추가 작업과 함께 최종 일괄 검수한 뒤 2026-08-14 commit·push·PR·main merge와 Azure 공개배포를 명시 승인했다.
- 병합 전 Ready PR 최신 head의 필수 `CI Gate`, 병합 뒤 exact `main` SHA의 승인형 Azure release와 공개 보안 smoke를 완료해야 한다.
