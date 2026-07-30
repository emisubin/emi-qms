# TASK-QUALITY-OPERATING-MODEL-001 — 구매품 구분 기반 IQC routing·외함 스캔형 IQC 2차 기획 (최종 구현 계약)

> 상태: 구현용 최종 기획 (Fable 2차)
> 작성 단계: Fable 1차 기획과 Codex 내용 review를 반영한 `experiment/*` fast-track 2-pass 최종본
> 목적: 이 문서 하나로 구현 범위·권한·상태·data lifecycle·UX·검증·제외 범위를 판단할 수 있는 구현 source of truth를 고정한다.

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-QUALITY-OPERATING-MODEL-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/quality-operating-model-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/quality-operating-model-001-planning.md` (판단 이력으로 보존, 수정 금지)
- reviewSource: `tasks/quality-operating-model-001-review.md` (`RESOLVED_FOR_SECOND_PLANNING`, 판단 이력으로 보존, 수정 금지)
- approvalSource: `tasks/quality-operating-model-001-change-002.md` (`fableSecondPlanningApproved: true`, `implementationApproved: true`)

이 문서는 해당 실험 Task의 최종 구현 source of truth다. 1차 기획의 승인된 내용은 보존하고, Codex review의 유지 5건·Resolution R1~R7·제거 4건을 모두 구현 계약으로 반영했다. 이 문서는 local experiment 구현·검증만 대상으로 하며 대표 repo·`main` merge·push·PR·Persistent UAT·실제 외부 provider에 대한 어떤 승인도 부여하거나 주장하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 따르며 여기에 복사하지 않는다.

## 1. 한 줄 목표

기능 업데이트 이후 등록되는 프로젝트에서 구매품 구분이 IQC 여부를 결정하고, 외함은 협력사 종이 검사서의 서명 스캔본으로 IQC를 판정하며, 비검사품은 IQC 없이 `검사 대상 아님` 상태에서 입고 확정까지 진행할 수 있다.

## 2. 확정 정책 기준선 (2026-07-30 사용자 확정)

- 전환 기준은 구매품이 아니라 프로젝트다. 기능 업데이트 전에 등록된 프로젝트는 이후 추가되는 구매품·도착분까지 기존 `모든 구매품 IQC` 흐름을 유지하고, 이후 새로 등록되는 프로젝트만 구분 기반 routing을 적용한다. 기존 데이터는 자동 분류·소급 변경하지 않는다.
- 구매 입력에 `구분` 선택값을 추가하고 신규 정책 프로젝트에서는 필수다. 구분 목록은 양식 관리에서 추가·이름 변경·사용 중지하며, 사용된 구분은 삭제하지 않는다. 각 구분에 `IQC 필요/없음`을 1:1로 설정하고 초기값은 외함만 `IQC 필요`다. 도급·사급 여부는 routing에 영향을 주지 않는다.
- 외함 IQC는 시스템 내 검사 항목·검사서를 만들지 않는다. 품질팀이 협력사 종이 수입검사서에 확인·서명한 스캔본(PDF/JPEG/PNG 다중)을 근거로 도착분별 `적합/부적합`을 판정한다. 적합·부적합 모두 스캔본 첨부가 필수이고, 확정 후 정정 불가, 부적합은 기존 Pending 흐름 연결, 재검사는 새 회차 + 새 스캔본 필수, 전 회차 보존이다.
- IQC가 없는 구매품은 도착 등록 후 IQC를 만들지 않고 자재 담당자의 `입고 확정`으로 종료한다. IQC 미실시를 `IQC 합격`으로 표시하지 않고 `검사 대상 아님`으로 구분한다.

제외 범위(변경 금지): LQC·OQC·전진검수·FAT 운영 모델 변경, 협력사용 IQC Excel 양식 생성·배포, 판금류·부스바·명판 신규 검사 정책, 기존 프로젝트·구매품 자동 분류, 기존 상세 IQC(`Detailed`) 계약·데이터 변경, 스캐너 연동·OCR·외부 object storage.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 구매 담당 | 신규 정책 프로젝트 구매품에 구분 필수 선택(직접 입력·Excel preview/apply), 구분 명시적 변경 | 담당 프로젝트 구매 + 활성 구분 목록 | 구매품 구분 snapshot |
| 자재 담당 | 도착 등록, `검사 대상 아님`·`합격` 도착분 입고 확정, 입고 확정 전 도착분 취소 | 담당 프로젝트 자재 | 도착분·입고 확정 |
| 품질 IQC 담당 | 스캔형 IQC 초안(판정+첨부) 작성·확정, 재검사 회차 수행 | 담당 프로젝트 IQC 큐 | 확정 전 초안만 |
| 양식 관리 권한 보유자(`CanManage`) | 구분 추가·이름 변경·사용 중지·`IQC 필요` 설정 | 구분 catalog 전체 | catalog 항목 |
| 모든 관련 역할 | 확정 판정·첨부·회차 이력 조회, 첨부 다운로드 | project scope 내 | 없음(확정 후 불변) |

- catalog 변경은 기존 양식 관리 권한(`CanManage`, `/api/form-templates` 권한 모델)을 사용한다. 구매·자재 화면에서 쓰는 활성 구분 목록 조회는 해당 화면 접근 권한 사용자에게 허용한다(R6, 미결정 1 확정).
- Backend가 routing·필수 첨부·불변성·권한의 authoritative layer다. Frontend 표시만으로 검사 생략을 판단하지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 외함 스캔형 IQC 정상 흐름

1. 구매 담당이 신규 정책 프로젝트 구매품에 구분 `외함`을 선택해 저장한다. 저장 시점의 구분 id·code·표시명·`IQC 필요`가 구매품에 snapshot으로 고정된다.
2. 자재 담당이 도착 등록하면 시스템이 프로젝트 routing 정책과 구매품 snapshot의 `IQC 필요`로 분기해 `ScanBased` IQC attempt와 품질 업무·알림을 생성한다.
3. 품질 담당이 협력사 종이 검사서에 확인·서명한 뒤 스캔본을 업로드하고 `적합`으로 확정한다.
4. 확정과 동시에 판정·첨부·핵심 정보가 불변으로 잠기고, 자재 담당에게 기존 입고 확정 업무가 생성되며 입고 확정으로 종료한다.

### 시나리오 B — 외함 부적합·재검사

1. 품질 담당이 서명 스캔본을 첨부하고 `부적합`으로 확정한다. 도착분은 `FailedBlocked`로 차단된다.
2. 시스템이 기존 Pending 흐름으로 부적합 이슈를 연결한다.
3. 조치 완료 후 재검사를 요청하면 기존 확정본을 고치지 않는 새 attempt 회차가 생성된다.
4. 재검사 회차에도 새 서명 스캔본이 필수이며 최초 회차와 모든 재검사 회차·증빙이 함께 조회된다.

### 시나리오 C — IQC 없는 구매품

1. 구매 담당이 구분 `판금류`(IQC 없음)를 선택해 구매품을 등록한다.
2. 자재 담당이 도착 등록하면 IQC attempt를 만들지 않고 같은 transaction에서 도착분을 `검사 대상 아님`(`InspectionNotRequired`) 상태로 두며, 기존 입고 확정 담당자 해석으로 인앱 알림·내 업무를 생성한다. IQC 담당자가 지정되지 않아도 등록은 성공한다.
3. 업무 상세에는 `IQC 검사 대상이 아닌 도착분입니다. 입고 확정을 진행해 주세요.`와 품목·수량을 표시한다.
4. 자재 담당이 `입고 확정`을 누르면 도착분이 `Confirmed`로 종료되고 해당 업무가 완료되며, 기존 이력·알림·키팅/제조 투입 후속 계약이 그대로 이어진다. 입고 확정 전에는 기존 `Arrived`와 동일하게 취소를 허용한다.

### 시나리오 D — 기존 프로젝트 비회귀

1. 기능 업데이트 전에 등록된 프로젝트(`AllReceipts`)에서 구매·도착을 진행한다.
2. 시스템은 구분 입력을 요구하지 않고, 모든 도착분에 기존 상세(`Detailed`) IQC를 그대로 생성한다. 동작·이벤트·기존 테스트가 달라지지 않는다.

## 5. 기능 요구사항

### 필수

- [ ] 프로젝트 IQC routing 정책 snapshot: migration이 기존 모든 프로젝트 row를 `AllReceipts`로 backfill하고 안전한 legacy default를 유지한다. 정상 사용자 생성 경로인 `ProjectStore.CreateProjectAsync`와 프로젝트 Excel의 `InsertProjectWithPanelsAsync` 두 곳에서만 `CategoryBased`를 명시적으로 기록한다. 생성 후 어떤 경로로도 변경·소급하지 않는다.
- [ ] 구매품 구분 catalog: 양식 관리에서 추가·이름 변경·사용 중지·`IQC 필요` 설정. code 불변·이름 유일·row_version 낙관적 동시성·변경 감사 기록. 사용된 구분은 hard delete 대신 `is_active=false`. 초기 seed는 `외함`(IQC 필요)·`판금류`·`부스바`·`명판`·`기타`(IQC 없음).
- [ ] 구매품 저장 시 routing snapshot 고정: 신규 정책 프로젝트의 구매품 저장 시 구분 id·code·표시명·`requires_iqc`를 함께 snapshot한다. 도착 등록은 catalog를 재조회하지 않고 이 snapshot으로만 routing한다. 구매 담당이 구분을 명시적으로 변경할 때만 새 snapshot으로 갱신하고 전·후 값을 변경 이력에 남긴다. catalog 변경은 이후 새로 저장·변경되는 구매품에만 적용하고 기존 구매품·도착분에 소급하지 않는다.
- [ ] 구분 필수 validation: 신규 정책 프로젝트의 구매 직접 입력과 Excel preview/apply(`ProcurementStore.PreviewExcelAsync`/`ApplyExcelAsync`) 모두에서 구분 필수. 누락·비활성 구분은 필드/행 단위의 이해 가능한 한글 오류로 차단. 기존 프로젝트는 구분을 요구하지 않는다.
- [ ] 도착 등록 routing: `RegisterArrivalAsync` transaction 안에서 분기한다. `IQC 필요` snapshot이면 `ScanBased` attempt·품질 업무·알림 생성. `IQC 없음`이면 attempt 없이 `InspectionNotRequired` 상태 + 입고 확정 업무·알림 생성. 현재 도착 등록의 IQC 담당자 필수 guard는 IQC 필요 경로에만 적용한다. 도급·사급 무관.
- [ ] 스캔형 IQC: 적합/부적합 판정 + PDF/JPEG/PNG 다중 첨부(확정 시 최소 1개, 파일당 10MB, 회차당 10개), 확정 전 초안 판정·첨부 수정 가능, 확정 후 API·DB trigger 이중 불변, 부적합 시 기존 Pending 연결, 재검사는 새 회차 + 새 스캔본 필수, 전 회차 보존. 상세 `iqc_reports` row와 시스템 생성 IQC PDF를 만들지 않는다.
- [ ] 상태·집계 의미 구분: `ReadyToConfirmCount`는 `Passed + InspectionNotRequired`를 포함하고, IQC 대기·합격 집계에는 `InspectionNotRequired`를 포함하지 않는다. 두 경로 모두 `Confirmed`로 종료하고 자재 입고 완료·프로젝트 진행률·생산관리 자동 실적은 `Confirmed` 기준을 유지한다. IQC 단계 표시가 필요한 곳에서는 `검사 대상 아님`을 합격과 색·문구로 구분해 표시한다.
- [ ] 입고 확정·취소: 입고 확정 API는 `Passed`와 `InspectionNotRequired`를 각각 허용하되 이벤트 문구를 구분한다(기존 `IQC 합격 도착분 입고 확정` 유지 + 비검사용 문구 신설). 취소는 기존 `Arrived`에 더해 입고 확정 전 `InspectionNotRequired`에서도 허용한다.
- [ ] 파일 보안: 확장자·client MIME을 신뢰하지 않고 magic byte(`%PDF-`, JPEG SOI, PNG signature)를 검사한다. 허용 외 형식·빈 파일·크기·개수 초과를 서버에서 차단한다. 다운로드는 `nosniff`·안전한 content disposition·DB 저장 정규화 MIME을 사용한다. 원본 파일명은 표시용으로만 정규화·길이 제한하고 경로로 사용하지 않는다.

### 선택

- [ ] 구분 catalog 목록의 사용 중지 항목 접기 표시.
- [ ] 스캔 첨부 미리보기(이미지 inline, PDF는 다운로드) — 기존 첨부 조회 패턴 재사용 범위에서.

### 명시적 제외

- [ ] 2장 제외 범위 전체. 추가로 대표 repo·`main`·push·PR·merge, Persistent UAT migration·runtime handover, 실제 외부 provider 발송.

## 6. 데이터·상태 모델

| 개념 | 계약 | 기존/신규 | 보존·감사 |
| --- | --- | --- | --- |
| `material_categories` | id, 불변 `code`, 표시명, `requires_iqc`, `is_active`, 정렬, `row_version`, 생성·수정 이력 | 신규 (패턴: `0045_pending_issue_type_catalog.sql`) | hard delete 금지, 사용 중지 lifecycle, 이름 유일 |
| `projects.iqc_routing_policy` | `AllReceipts` / `CategoryBased`, 생성 시 1회 고정 | 기존 테이블 신규 열 | migration backfill `AllReceipts`, 불변 |
| `project_procurement_items` 구분 snapshot | category id 참조 + code·표시명·`requires_iqc` snapshot 열(모두 nullable, 신규 정책 프로젝트에서 필수) | 기존 테이블 신규 열 | 기존 데이터 null 유지, 명시적 변경 시에만 갱신 + 전·후 이력 |
| 도착분 상태 | `MaterialReceiptStatuses`에 `InspectionNotRequired` 추가 | 기존 확장 | 기존 이벤트 이력 계약 유지 |
| attempt decision mode | `IqcDecisionModes`에 `ScanBased` 추가 (`material_iqc_attempts.decision_mode`) | 기존 확장 | 회차 번호·Pending 연결·재검사 lineage 재사용 |
| `material_iqc_scan_reports` | attempt와 1:1. 초안/확정 상태, 판정, 판정 사유·비고, version, 확정자·확정일, snapshot hash | 신규 | 확정 후 불변 trigger |
| `material_iqc_scan_attachments` | scan report와 1:N. 원본 파일명, 정규화 MIME, byte 수, SHA-256, content(bytea) | 신규 (패턴: `iqc_report_photos`·`0066_pending_action_photos` bounded DB 저장) | 확정 후 수정·삭제 거부, append-only 증빙 |

```text
[CategoryBased + IQC 필요]  Arrived → IqcRequested(ScanBased 초안) → Passed → Confirmed
                                          └→ FailedBlocked → Pending 조치 → IqcRequested(새 회차, 새 스캔 필수) → …
[CategoryBased + IQC 없음]  Arrived → InspectionNotRequired ─(입고 확정)→ Confirmed
                                          └(확정 전)→ Cancelled
[AllReceipts 기존 프로젝트]  현재 Detailed 흐름 그대로 (변경 없음)
```

- 스캔형 조회 응답은 decision mode에 따라 상세형 report id/status와 스캔형 report id/status/attachment count를 명시적으로 구분해 반환한다. 스캔형에 상세 `iqc_reports` row나 시스템 생성 PDF를 연결하지 않는다.
- 자재 orphan 보정 sweep(`Arrived` 상태에서 attempt 없는 도착분 탐지)은 `InspectionNotRequired` 도착분을 대상으로 삼지 않아야 하며, routing 분기 후 상태가 분리되므로 회귀 테스트로 이를 고정한다.
- 개발 seed·테스트 fixture는 routing 정책을 명시적으로 선택하는 helper를 사용해 DB default에 의존하지 않는다.

## 7. API·Backend 계약

- 구분 catalog: 기존 `/api/form-templates` endpoint 군에 구분 목록/추가/이름 변경/사용 중지/`IQC 필요` 설정을 추가(`CanManage`). 구매·자재 화면용 활성 목록 조회는 화면 접근 권한으로 허용. CAS는 `row_version`, 변경은 기존 기준정보 감사 패턴을 따른다.
- 구매: 저장·수정·Excel apply에서 신규 정책 프로젝트의 구분 필수 validation과 snapshot 기록. project detail/procurement 응답이 routing mode와 구분 snapshot을 반환해 Frontend·Backend가 같은 기준을 사용한다.
- 자재: `RegisterArrivalAsync` transaction 내 routing 분기, `InspectionNotRequired` 도착분 입고 확정·취소 허용, 이벤트 문구 구분, `ReadyToConfirmCount` 확장. 비검사 경로 업무는 기존 `ResolveMaterialConfirmationAssigneesAsync`, IQC 경로 업무는 기존 `CreateIqcWorkItemAsync`를 재사용한다.
- 스캔형 IQC: attempt 상세 조회, 초안 판정 저장, 첨부 추가/삭제(초안만), 확정, 첨부 다운로드. 확정은 단일 transaction에서 첨부 존재·형식·한도를 재검증하고 version CAS를 사용한다. 부적합·재검사는 `EnsurePendingReinspectionAsync`와 attempt 회차 축을 재사용하며 새 lineage를 만들지 않는다.
- magic byte 검증은 기존 `IqcReportStore`의 JPEG/PNG sniffing과 `LogisticsStore`의 `%PDF` 판별 패턴을 재사용한다. 한도는 파일당 10MB·회차당 10개.
- audit: 기존 material receipt 이벤트(`InsertEventAsync`)에 routing 결과·확정·재검사 이벤트를 기록한다.
- 외부 provider 영향: 없음. 알림은 기존 인앱·work item 경로만 사용한다.

## 8. Frontend·UX 계약

| 화면 | 진입 경로 | 표시·행동 | 피드백 |
| --- | --- | --- | --- |
| 양식 관리 › 구매품 구분 | `FormTemplateManagementPage.tsx` 신규 section | 구분 목록·`IQC 필요`·사용 중·정렬, 추가/이름 변경/사용 중지/IQC 설정 | 저장 action 바로 아래 결과·오류(기존 Action Feedback 계약) |
| 구매 입력 | 기존 구매 탭 | 신규 정책 프로젝트에서 구분 필수 필드, Excel preview에 구분 열·행 오류 | 필드/행 단위 한글 오류 |
| 자재 도착·입고 | `MaterialsWorkspace.tsx` | `검사 대상 아님` 뱃지(합격과 색·문구 구분), 입고 확정·취소 | 기존 도착·확정 피드백 재사용 |
| 품질 IQC 큐·상세 | `IqcReportWorkspace.tsx` 내 `decisionMode` 분기 | 판정 선택, 첨부 목록(파일명·크기), 회차 이력, "협력사 종이 검사서에 서명 후 스캔본을 올려 주세요" 안내 | 확정 성공·불변 안내, 첨부 형식·크기·개수 오류 |
| Pending | 기존 Pending 화면 | 외함 부적합 이슈(기존 계약) | 기존 피드백 |

- 신규 route 없음. contracts는 `frontend/src/api.ts`·`frontend/src/iqc-report.ts` 확장. 기존 상세 IQC 화면은 변경하지 않고 분기만 추가한다.
- 확정 후 화면은 조회 전용임을 명시한다(기존 확정 검사 화면 패턴). 390px에서 첨부 업로드·판정·확정이 가능해야 하며 기존 adaptive layout·MobileSheet를 재사용하고 overflow 0을 유지한다.

## 9. 확정된 미결정 사항 (1차 기획 16장 6건 — standing instruction에 따른 권장안 채택)

| 번호 | 항목 | 확정값 | 근거 |
| ---: | --- | --- | --- |
| 1 | 구분 catalog 관리 권한 | 기존 양식 관리 권한(`CanManage`) + 활성 목록은 화면 접근 사용자 조회 허용 | 인터뷰가 관리 위치를 양식 관리로 확정, 기존 권한 모델 재사용 (review R6 동일) |
| 2 | 초기 seed 구분 | `외함`(IQC 필요)·`판금류`·`부스바`·`명판`·`기타`(IQC 없음) | 현장 조사 실제 품목 + 미분류 대비 `기타` (review 유지 4 동일) |
| 3 | `IQC 필요` 변경 적용 시점 | 구매품 저장 시 snapshot으로 대체 확정 — catalog 변경은 이후 새로 저장·변경되는 구매품에만 적용 | review R1이 1차 안(도착 시 snapshot)의 오분류 위험을 지적, 발주 당시 정책 재현 가능 |
| 4 | IQC 없는 도착분 표현 | 별도 상태 `InspectionNotRequired` 신설 | `Arrived`는 transient라 의미 중복, 집계·이벤트 명확 (review 유지 3 동일) |
| 5 | 스캔 첨부 한도 | 파일당 10MB·회차당 10개 | 스캔 PDF는 사진보다 크고 bounded DB 저장 원칙 유지 (review 유지 5 동일) |
| 6 | 구매 Excel의 구분 처리 | 신규 정책 프로젝트 Excel에 구분 열 추가, 누락·비활성 시 preview/apply 오류 | "구분 필수"를 입력 경로 전체에 동일 적용 (review 유지 4 동일) |

## 10. Codex review 반영표

| 항목 | 반영 위치 | 반영 내용 |
| --- | --- | --- |
| 유지 1~5 | 5·6·7장 | 프로젝트 단위 전환·attempt lineage 재사용·별도 비검사 상태·전 입력 경로 구분 일관·스캔 증빙 계약을 필수 요구사항으로 고정 |
| R1 | 5장 snapshot, 9장 3번 | routing snapshot을 도착 시점이 아닌 구매품 저장 시점으로 고정, 명시적 변경만 재snapshot + 이력 |
| R2 | 6·7장 | `material_iqc_scan_reports`/`material_iqc_scan_attachments` 분리, `iqc_reports` row·시스템 PDF 미생성, 응답의 mode별 명시 구분 |
| R3 | 5장 routing 정책, 6장 | backfill + legacy default 유지, `CreateProjectAsync`·`InsertProjectWithPanelsAsync`만 `CategoryBased` 명시, 응답에 routing mode, 테스트 helper의 명시적 정책 선택 |
| R4 | 시나리오 C, 5·7장 | 비검사 도착 transaction에서 입고 확정 업무·알림 실제 생성, 고정 안내 문구, IQC 담당자 부재 시에도 성공, 확정 시 업무 완료 |
| R5 | 5장 상태·집계, 6장 | `ReadyToConfirmCount = Passed + InspectionNotRequired`, IQC 집계 제외, 종료는 `Confirmed` 통일, 이벤트 문구 구분, 확정 전 취소 허용 |
| R6 | 3장, 7장 | `CanManage` 관리 / 화면 접근자 조회 분리, hard delete 금지, code 불변·이름 유일·row_version·감사, snapshot 덕분에 과거 표시 불변 |
| R7 | 5장 파일 보안, 7장 | magic byte 검증, 빈 파일·한도 차단, `nosniff`·안전 disposition·정규화 MIME, 파일명 표시용 한정 |
| 제거 4건 | 전체 | 스캔형에 상세 template·시스템 PDF 연결 금지, 품목명 문자열 자동 판별 금지, catalog 소급 금지, 비검사품 `IQC 합격` 저장 금지 |

## 11. 변경 allowlist

- Backend: `backend/src/Emi.Qms.Api/Materials/MaterialsStore.cs`·`MaterialsContracts.cs`·`MaterialsEndpointExtensions.cs`(해당 시), 스캔형 IQC store/endpoint(신규 파일 또는 `IqcReportStore` 인접), `Procurement/*`(구분 입력·Excel·응답), `Projects/ProjectStore.cs`(정책 명시·응답), `Admin/FormTemplate*`(구분 catalog), Workflow 완료 판정 대조 범위.
- Frontend: `frontend/src/FormTemplateManagementPage.tsx`, 구매 입력 화면, `frontend/src/MaterialsWorkspace.tsx`, `frontend/src/IqcReportWorkspace.tsx`, `frontend/src/api.ts`·`frontend/src/iqc-report.ts`, 관련 styles·tests.
- DB/Migration: `database/migrations/0067_*.sql` 1건(additive: catalog+seed, 프로젝트 정책 열+backfill, 구매품 snapshot 열, attempt mode 확장, 도착분 상태 확장, 스캔 report/attachment 테이블·불변 trigger). 현재 최신 `0066` 다음 번호이며 기존 migration 수정·번호 재사용 금지.
- Tests/Scripts: Backend 통합 테스트(`PostgreSqlMigrationTests` 포함), Frontend unit, 격리 Full-Stack E2E 시나리오.
- Docs: SOP·User manual·Roadmap·실험 원장 갱신(구현 단계 산출물). 이 목록 밖 변경은 Finding 또는 후속 Task로 분리한다.

## 12. 검증 계획과 회귀 matrix

- Backend 신규: 신규 정책 프로젝트 routing 분기(외함→`ScanBased`, 비검사→attempt 없음+업무 생성), 구매품 저장 snapshot·명시적 변경 이력, catalog 변경 비소급, 구분 필수(직접 입력·Excel preview/apply), IQC 담당자 부재 시 비검사 도착 성공·IQC 필요 경로 차단 유지, 확정 시 첨부 필수·magic byte·10MB·10개 한도, 확정 후 불변(API 거부 + trigger UPDATE/DELETE 거부), 부적합→Pending→재검사 새 회차+새 스캔 필수·전 회차 보존, `InspectionNotRequired` 입고 확정·취소·이벤트 문구, `ReadyToConfirmCount`·IQC 집계 구분, orphan sweep 비대상 확인, migration fresh+기존 DB.
- Backend 회귀: 기존 프로젝트 `AllReceipts` 경로의 Detailed 흐름·이벤트·기존 테스트 무변경, 자재 집계·프로젝트 완료 판정·생산관리 자동 실적(`Confirmed` 기준) 기존 테스트 통과. 전체 기준선(현재 430/430) 무손실.
- Frontend: 구분 catalog 관리 UI, 구매 구분 필수 오류, 스캔형 판정·첨부·확정 화면, `검사 대상 아님` 표시·다운로드. 전체 기준선(현재 142/142) 무손실.
- 격리 Full-Stack E2E: 시나리오 A~D를 Persistent UAT와 분리된 전용 DB에서 1건 이상.
- 사용자 검수: 양식 관리·구매·자재·IQC 화면의 desktop 1440·mobile 390 screenshot과 checklist. `BATCHED_FINAL` 규칙에 따라 자동 검증 완료와 사용자 검수 완료를 별도 상태로 유지하며, 사용자 검수 완료 전에 완료로 표기하지 않는다.

## 13. 완료 기준과 중단 조건

- 완료: 5장 필수 요구사항 전부 서버 강제, 기존 프로젝트 비회귀, desktop·390px 핵심 동선 overflow 0·console error 0, Backend·Frontend 전체 green, migration `0067` fresh·기존 DB 적용, 5종 산출물(Implementation report·SOP·User manual·Roadmap/원장 update·User validation checklist)을 `docs/12-task-completion-policy.md`에 따라 추적.
- 중단: 확정 불변 trigger·기존 IQC 계약과 해소 불가능한 충돌, Repository source 간 의미 있는 충돌, fast-track 제외 경계(대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider)를 넘어야만 진행 가능한 경우. 이때 blocking decision을 0으로 가장하지 않고 보고 후 중단한다.

## 14. 안전 경계와 승인 상태

- Persistent UAT 영향 없음(experiment worktree·전용 DB만 사용), runtime 교체 없음, 실제 provider 호출 없음.
- `implementationApproved: true`(change-002의 fast-track 승인). `localCommitApproved: false` — local commit은 fast-track 계약에 따르되 change-002 기록 상태를 확인한 뒤 수행한다. `mainMergeApproved: false`, `persistentUatApproved: false`, `externalProviderApproved: false` — 이 문서는 해당 승인들을 부여하지 않는다.
- 구현 시 Roadmap 추적 항목(2·4·11~14 연계)과 실험 원장 4.1의 상태 동기화를 산출물 범위에 포함한다. LQC·OQC·전진검수·FAT·고객용 성적서·도면 revision 연결은 후속 Task로 유지한다.

## 15. 구현 순서와 Codex 지시문

1. migration `0067`: catalog+seed, `projects.iqc_routing_policy` backfill, 구매품 snapshot 열, `ScanBased`·`InspectionNotRequired` 확장, 스캔 report/attachment 테이블·불변 trigger. fresh·기존 DB 검증.
2. catalog API·권한·CAS·감사 (`/api/form-templates` 군).
3. 구매 직접 입력·Excel의 구분 필수 validation과 저장 snapshot·변경 이력, 프로젝트 생성 두 경로의 `CategoryBased` 명시와 응답 확장.
4. 자재 도착 routing 분기, 비검사 업무·알림 생성, 입고 확정·취소·집계·이벤트 문구.
5. 스캔형 IQC report·attachment·초안·확정 불변·다운로드 보안(magic byte·한도·`nosniff`).
6. Pending 부적합·재검사 lineage 연결(`EnsurePendingReinspectionAsync` 재사용, 새 lineage 금지).
7. Frontend 양식 관리·구매·자재·IQC 분기와 Action Feedback·390px.
8. 12장 검증 matrix 실행. 실행하지 않은 검증을 성공으로 기록하지 않는다.

구현은 이 문서를 유일한 구현 계약으로 사용하고, 1차 기획·review와 이 문서가 충돌하면 이 문서를 따르되 충돌 사실을 Implementation report에 기록한다. 허용 범위 밖 개선은 Finding으로 분리한다.

openBlockingDecisionCount: 0
