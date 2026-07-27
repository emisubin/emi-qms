# TASK-PRODUCTION-CONTROL-001 구현 보고 — Item별 생산계획·자동 실적·가로 막대 일정

상태: `실험 구현·Change 002 자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

## 기준선과 범위

- instructionChainRead: `true`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-home-002-personalized-shell`
- implementationBaseline: `de8e05b`
- latestChange: `tasks/production-control-001-change-002.md`
- latestChangeBaseline: `f8eb6ce`
- planningSource: `tasks/production-control-001-planning.md`
- codexReviewSource: `tasks/production-control-001-review.md`
- finalPlanningSource: `docs/43-production-control-plan.md`
- 사용자 확인: Fable Round 8 전체 요약 `요약 확인`
- 포함: Item별 제조·생산계획 양식 version, 다중 실적 연결, 새 프로젝트 snapshot, 프로젝트별 계획 기간·항목·연결 수정, 부서 원본 기반 자동 실적, desktop/mobile 조회 UI
- 제외: 기존 프로젝트 소급 전환, Persistent UAT, 실제 provider, 대표 repo·`main`, push·PR·merge, 18단계 workflow 계산 변경

## 해결한 업무 문제

기존 생산계획은 항목명·필수 여부·단일 예정일 중심이라 실제 구매·자재·제조·품질·물류 입력과 연결되지 않았다. 생산관리 담당자는 각 부서 화면을 오가며 진행 상태를 수기로 대조해야 했고, 계획 기간과 실적 기간의 겹침·지연도 한눈에 확인할 수 없었다.

이번 구현으로 관리자는 코드 수정 없이 Item별 제조 항목과 생산계획 항목을 구성하고 한 계획 항목에 여러 부서 실적을 연결할 수 있다. 새 프로젝트는 생성 당시 양식과 연결을 복사해 보존하며, 생산관리 탭은 실제 부서 입력값을 읽어 계획 대비 실적·근거·진행률과 가로 막대 일정을 자동 표시한다.

사용자 검수에서 연결형 양식이 기존 `양식 종류` 밖의 별도 전환 화면으로 분리되고 모든 계획 항목의 연결 입력이 동시에 펼쳐져 있다는 UX 결함이 확인됐다. Change 002에서 두 연결형 양식을 기존 종류 목록에 통합하고 선택한 계획 항목 하나만 펼치는 편집 구조로 보정했다.

## 구현 결과

### 양식과 권한

1. 기존 `양식 종류` 목록에 `Item별 제조 양식`, `생산계획·실적 연결`을 같은 1차 탐색 항목으로 추가했다.
2. System Administrator와 지정된 제조/생산계획 양식 관리자가 권한 범위의 양식을 편집한다.
3. 제조 양식과 생산계획 양식은 Item별 `Draft → Active → Archived` version으로 관리한다.
4. 생산계획 항목 하나에 여러 source를 연결한다.
   - 제조: 해당 Item 제조 양식의 불변 `definition_key`
   - 구매·자재·품질·물류: 서버가 제공하는 고정 사건 catalog
5. 사용 중 version은 조회 전용이며 편집용 초안을 만들어 변경한다.
6. 생산계획 항목은 이름·필수 여부·연결 수·부서 요약을 한 줄로 표시하고, 선택한 항목 하나만 펼쳐 편집한다.
7. 제조 항목은 `No. / 제조 항목 / 구분 / 관리` 헤더에 맞춘 행 구조로 표시한다.

### 새 프로젝트와 기존 프로젝트

1. migration 당시 기존 프로젝트는 `Legacy`로 고정한다.
2. Item에 유효한 Active 제조 양식과 Active 생산계획 양식이 모두 있을 때만 이후 새 프로젝트를 `LinkedV1`로 생성한다.
3. 프로젝트 생성 transaction에서 양식 두 개와 연결 유효성을 다시 확인하고 제조 항목·계획 항목·연결을 한 세트로 snapshot한다.
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
6. FAT 비필수 프로젝트는 FAT 연결이 snapshot에 있어도 실적 분모와 화면의 유효 연결 요약에서 제외한다.
7. source 이력이 아직 없는 경우 500 오류가 아니라 `대기`, 0%와 근거 0건을 반환한다.
8. 기존 18단계 workflow와 프로젝트 대표 진행률은 변경하지 않았다.

## DB·Backend·API

### Migration

- `0058_production_control_linked_plans.sql`
  - Item별 제조/생산계획 template version·항목·connection
  - 프로젝트 model version·template snapshot·project override
  - additive migration이며 기존 production plan을 `Legacy`로 보존
- `0059_production_control_lqc_identity.sql`
  - LQC 응답에 제조 단계 `manufacturing_definition_key` 추가
  - 기존 응답을 파괴하거나 의미를 추측해 일괄 변환하지 않음

Rollback은 destructive down migration 대신 배포 전 DB backup과 application forward-fix를 사용한다. 이미 생성된 `LinkedV1` snapshot을 Legacy로 재해석하거나 삭제하지 않는다.

### 주요 API

- `/api/production-control/templates`
- `/api/production-control/templates/{productTypeId}/manufacturing`
- `/api/production-control/templates/{productTypeId}/plans`
- 기존 `/api/projects/{projectId}/production-planning` GET/PATCH는 model version에 따라 Legacy/LinkedV1 계약을 반환한다.

서버는 template 관리 권한, 프로젝트 담당자 수정 권한, Active 전 유효성, 제조 참조 존재, 중복 연결, version 충돌과 project snapshot 불변조건을 강제한다.

## Frontend·UX

- 양식 관리 화면에서 Item, 제조 양식, 생산계획·연결을 순서대로 선택한다.
- 연결형 양식도 다른 검사·제조 양식과 같은 `양식 종류` 목록에서 선택한다.
- 생산계획 편집은 요약 행에서 필요한 항목 하나만 `편집`해 반복 연결 목록이 화면을 점유하지 않는다.
- 사용 중/초안 상태와 “이후 새 프로젝트부터 적용” 원칙을 화면에 고정 표시한다.
- 프로젝트 생산관리 탭은 조회 중심이며 단일 `생산계획 수정`으로 별도 입력 상태에 진입한다.
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

## 시행착오 및 폐기한 접근

1. LQC 완료를 최신 전체 검사 상태만으로 연결하려던 방식은 어떤 제조 단계의 LQC인지 보존하지 못해 폐기했다. 응답 확정 시 `definition_key`를 함께 저장하도록 보완했다.
2. 검사 시도가 없는 OQC source의 SQL boolean이 `NULL`이 되어 계획 조회가 500을 반환했다. 미시작을 `false`로 안전하게 projection하도록 수정했다.
3. FAT 비필수 프로젝트에서 raw snapshot 연결명을 그대로 표시하면 분모와 연결 수가 달라 보였다. 실제 적용 근거의 고유 source명을 우선 표시하도록 정리했다.
4. wireframe 전역 규칙이 `<i>` 기반 간트 막대 배경을 투명하게 덮었다. 정보 전달 막대만 명시적 흑백 예외로 고정했다.
5. 양식 편집용 article과 프로젝트 조회용 row가 같은 `.production-control-plan-row` class를 사용해 조회 표의 6열 grid가 편집 화면에 잘못 적용됐다. 편집 전용 class로 분리하고, 항목 하나만 펼치는 구조로 반복 노출도 함께 제거했다.

## 주요 변경 위치

- Backend: `backend/src/Emi.Qms.Api/ProductionPlanning/`, `ProjectStore.cs`, `ManufacturingStore.cs`, `QualityInspectionStore.cs`, `Ul891SetStore.cs`, `Program.cs`
- Migration: `database/migrations/0058_production_control_linked_plans.sql`, `0059_production_control_lqc_identity.sql`
- Frontend: `frontend/src/ProductionControlTemplateWorkspace.tsx`, `productionControlTemplates.ts`, `FormTemplateManagementPage.tsx`, `App.tsx`, `api.ts`, `projects.ts`, `styles.css`, `design-system/wireframe.css`
- Tests: `backend/tests/Emi.Qms.Api.Tests/ProductionPlanningApiTests.cs`, `PostgreSqlMigrationTests.cs`, `frontend/tests/App.test.tsx`, `frontend/tests/FormTemplateManagementPage.test.tsx`

## 검증 결과

| 검증 | 결과 |
| --- | --- |
| Backend Release solution build | PASS — 경고 0, 오류 0 |
| 생산계획 API·LinkedV1 통합 회귀 | PASS — 활성화 전 Legacy, 활성화 뒤 새 프로젝트 LinkedV1, 기존 프로젝트 불변, 연결 projection 포함 `21/21` |
| Migration 전체 회귀 | PASS — fresh/existing 적용·catalog 최신 번호 `0059` 포함 `34/34` |
| Frontend production build | PASS — 기존 500kB 초과 chunk warning만 유지 |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend 전체 unit | PASS — 22 files, `139/139` (`testTimeout=30000`) |
| 실제 Backend API | PASS — template catalog, LinkedV1 계획 GET/PATCH, source 이력 없음 안전 응답 |
| Desktop browser | PASS — 양식 관리와 프로젝트 6열 표·근거·가로 막대 |
| Mobile 390×844 browser | PASS — 계획 카드·근거·실제 가로 막대, 가로 overflow 없음 |

Backend 전체 회귀는 5분 동안 완료 출력 없이 대기해 현재 실행을 중단했으며 성공으로 기록하지 않는다. 대신 solution build, 생산계획 API·신규 고위험 통합 `21/21`과 migration 최신 번호 회귀를 분리 실행했다. 이 환경 문제는 제품 Finding이 아니며 대표 승격 전 clean runner 전체 회귀가 필요하다.

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
3. `편집`을 눌러 제조 항목과 조립 의미 단계를 구성해 활성화한다.
4. `양식 종류`에서 `생산계획·실적 연결`을 선택하고 같은 Item을 고른다.
5. `편집`을 누른 뒤 필요한 계획 항목 행을 하나씩 펼쳐 이름·필수 여부와 제조 단계 또는 고정 부서 사건을 연결해 활성화한다.
6. 이후 새로 생성한 해당 Item 프로젝트부터 연결형 생산계획이 적용된다.

### 프로젝트 계획과 실적 확인

1. 프로젝트 상세의 `생산관리` 탭을 연다.
2. `생산계획 수정`에서 각 항목의 계획 시작·종료와 필요한 프로젝트 전용 연결을 저장한다.
3. 조회 화면에서 계획 기간, 자동 실적 기간, 진행률과 상태를 확인한다.
4. 행 또는 모바일 `근거 N건`을 눌러 어떤 품목·패널이 완료 또는 대기인지 확인한다.
5. 하단 가로 막대에서 계획과 실적의 겹침·지연을 비교한다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 합성 데이터 브라우저 검수는 완료했다.
- 사용자 직접 검수는 실험 branch 정책에 따라 마지막 일괄 검수로 남긴다.
- 운영 양식 content 입력, Persistent UAT migration, clean runner Backend 전체 회귀와 대표 repo 승격은 현재 Task 범위 밖이다.

## 안전·게시 경계

- 고정 검수 runtime: Frontend `http://127.0.0.1:42983`, Backend `http://127.0.0.1:41166`
- local experiment commit만 허용한다.
- 대표 repo·`main`·Persistent UAT·실제 provider는 변경하지 않는다.
- push·PR·merge는 실행하지 않는다.
- `main` merge 승인: `0/3`.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | 본 문서 `사용자 사용 방법` |
| User manual | 포함됨 | 본 문서 `사용자 사용 방법` |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성됨 / 마지막 일괄 검수 대기 | `tasks/production-control-001-user-validation-checklist.md` |
