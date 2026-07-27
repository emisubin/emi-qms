# TASK-PRODUCTION-CONTROL-001 — Item별 생산계획·자동 실적·가로 막대 일정 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-PRODUCTION-CONTROL-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/production-control-001-interview.md` (Round 8 `COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/production-control-001-planning.md` (보존, 수정하지 않음)
- codexReviewSource: `tasks/production-control-001-review.md` (`RESOLVED_FOR_SECOND_PLANNING`, R1~R9 전부 본 문서에 반영)
- approvalChangeSource: `tasks/production-control-001-change-001.md` (`fableSecondPlanningApproved: true`, target `docs/43-production-control-plan.md`)
- branchScope: `experiment/*` fast-track — local experiment commit만. 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 제외
- planningApproved: `true` (`USER_EXPLICIT_EXPERIMENT_RULE_AFTER_CONFIRMED_INTERVIEW`)
- implementationApproved: `true` (`USER_EXPLICIT_EXPERIMENT_RULE_AFTER_CONFIRMED_INTERVIEW`)

이 문서는 실험 fast-track 계약에 따라 1차 기획의 확정 내용을 보존하고 Codex 내용 review의 필수 resolution(R1~R9)·제거 항목·§8 resolution을 모두 통합한 이 Task의 최종 구현 source of truth다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 따르며 여기에 복사하지 않는다. 이 문서는 main merge, Persistent UAT 적용, 실제 provider 발송, 게시 승인을 부여하지 않는다.

## 1. 한 줄 목표

새 양식(LinkedV1)으로 생성된 프로젝트의 상세 생산관리 탭 한곳에서, 관리자가 코드 수정 없이 구성한 Item별 계획 항목의 계획 기간(생산관리 담당자 입력)과 실적 기간(부서 실데이터에서 결정적으로 자동 계산), 진행률, 지연·차단 상태를 6열 표와 계획/실적 가로 막대 일정으로 비교할 수 있다.

## 2. 확정 기준선 (interview Round 8, 변경 불가)

- 기존 프로젝트는 단일 예정일·체크 캘린더 화면과 생성 당시 양식을 그대로 유지한다. legacy `planned_date` 복사와 과거 기록 이름 exact-match 연결은 적용 대상이 없어 범위에서 제외 확정됐다.
- 새 양식 version으로 생성되는 프로젝트부터 계획·실적 표, 가로 막대 일정, 자동 실적 연결을 적용한다.
- 생산계획 항목과 제조 항목은 관리자가 자유롭게 구성하는 Item별 versioned 양식이며, 이름·순서가 아닌 immutable identity로 연결을 유지한다.
- 생산계획 항목 하나는 여러 실적 source(제조 항목 identity 또는 고정 사건 code)에 연결할 수 있다.
- 계획 기간은 생산관리 정·부 담당자만 입력하고(같은 날 허용, 역전 저장 불가), 실적 기간은 부서 authoritative data에서 자동 파생하며 어떤 역할도 직접 수정할 수 없다.
- 구매·자재·IQC는 구매품목·도착분 단위, 제조·LQC·OQC·전진검수·FAT·물류는 개별 physical panel 단위라는 확정 처리 단위를 보존한다. OQC는 단계별 판정, 전진검수·FAT는 패널별 aggregate 판정 1회, FAT optional·부분 입고·부분 출하 의미를 보존한다.
- Open Pending은 과거 실적 삭제·workflow 후퇴 없이 현재 `차단` 상태와 근거로 표시한다.
- 표·상세 펼침·가로 막대 일정은 같은 Backend projection을 사용한다.
- 기존 18단계 전체 흐름 진행률 공식은 변경하지 않고 일정 진행률과 분리한다.
- `work_items.due_date` 자동 동기화와 지연 외부 알림(인앱·Teams·메일)은 제외한다.

## 3. Review resolution 통합 상태

Codex review의 필수 항목은 아래 절에 계약으로 통합했다. 요약이 아니라 본문 규칙이 구현 기준이다.

| Review 항목 | 반영 위치 |
| --- | --- |
| R1 명시적 model version 분기 | §5 |
| R2 생산계획·제조 Active version 호환성 fail-closed | §6.4, §7.2 |
| R3 전용 저장 계약과 domain 확장 | §6.5, §11 |
| R4 프로젝트 생성 단일 helper·원자성 | §7.1 |
| R5 제조 execution의 snapshot identity 보존 | §8 |
| R6 LQC parameterized source | §9.3 |
| R7 다중 source 분모·중복 제거 | §9.2 |
| R8 프로젝트별 수정 경계 | §10 |
| R9 활성화 validation·사용자 안내 | §6.4, §12 |
| §5 제거 항목(snapshot 유무 분기, backfill, ordinal 연결, 공용 DTO, 키팅, 조용한 fallback) | §5, §6.5, §7.2, §16 |
| §8 resolution(키팅 제외, 발주 완료 기준) | §6.3 |

## 4. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| System Administrator | 전체 양식·연결·관리자 지정·audit 관리, 생산계획 항목·실적 연결 관리 | 전사 관리자 범위 | 양식 관리만. 업무 입력 금지 불변조건 유지 |
| 제조 양식 관리자(사람 단위 binding) | Item별 제조 항목 template 구성·게시 | 양식 관리 Manufacturing 영역 | 제조 항목 초안·정렬·사용 종료·게시 |
| 생산계획 양식 관리자(사람 단위 binding) | Item별 생산계획 항목·다중 실적 연결 구성·게시 | 양식 관리 신설 ProductionPlanning 영역 | 생산계획 항목·연결 초안·게시 |
| 생산관리 정·부 담당자 | 프로젝트 계획 기간 입력, 프로젝트 전용 항목·연결 revision, 실적·근거 확인 | 기존 project scope | 프로젝트 snapshot 안의 계획·항목·연결만(§10 경계) |
| 구매·자재·제조·품질·물류 담당자 | 자기 입력의 실적 반영 확인 | 기존 project scope | 기존 부서 업무 화면 입력만 |
| 영업·설계·회계 등 기타 부서 | 계획·실적 조회 | 기존 project scope | 없음(기존 `DsReadOnlyBanner` 의미 유지) |

Backend가 모든 권한·업무 불변조건의 authoritative layer다. 기존 `form_template_manager_bindings` 사람 단위 지정과 `ProductionPlanningPrimary/Secondary` responsibility 체계를 재사용한다.

## 5. 명시적 model version 분기 (R1 — `snapshot 존재 여부` 판정 제거)

- 프로젝트 생산계획에 명시적 model version 값 `LEGACY` / `LINKED_V1`을 저장한다. 새 화면·자동 실적·새 판정의 유일한 분기 기준은 이 값이며, snapshot 행 존재 여부·항목 이름·생성 시점으로 추측하지 않는다.
- 근거: 현행 `ProjectStore.CreateInitialProductionPlanFromTemplateAsync`는 인식 가능한 Item의 모든 프로젝트에 legacy `project_production_plans`·items를 이미 생성하므로 “snapshot 존재 = 새 화면” 판정은 기존 프로젝트를 오분류한다.
- `0058` migration 시점의 모든 기존 plan은 `LEGACY`로 고정한다. migration 이후에도 해당 Item에 유효하게 게시된 LinkedV1 양식 세트(생산계획 Active + 참조 무결한 제조 Active)가 없으면 현행 legacy 생성 경로를 그대로 사용하고 model version을 `LEGACY`로 저장한다.
- 유효한 LinkedV1 세트가 게시된 Item에서 이후 생성되는 프로젝트만 `LINKED_V1`로 snapshot한다.
- `LEGACY` 프로젝트는 화면·데이터·판정(`ProductionPlanningDomain.CalculateStatus`의 현행 `PlannedDate` 기반 판정)·Excel 경로를 전부 현행 그대로 유지한다. Frontend·Backend 분기와 회귀 테스트는 model version 값만 사용한다.

## 6. Item별 양식 관리 (template family 2개)

### 6.1 구조

- 생산계획 항목 template와 제조 항목 template를 Item scope의 별도 family 2개로 구현한다(1차 기획 후보 A 유지). 기존 form template의 Draft→Active→Archived lifecycle·manager binding·audit 패턴을 재사용하되 저장 계약은 전용으로 분리한다(§6.5).
- 현행 전역 `PANEL_MANUFACTURING` template는 유지하고(legacy 경로), Item별 제조 template를 신설 확장한다. 기존 `생산계획 단계 설정` 기능은 양식 관리 화면으로 통합한다.

### 6.2 Immutable identity (`definition_key`)

- 모든 template 항목(생산계획 항목·제조 단계)에 server-generated `definition_key`를 부여한다. 요청자가 key를 생성·변경할 수 없다.
- 새 draft를 기존 version에서 복제할 때 `definition_key`를 유지하고, 진짜 신규 항목만 새 key를 발급한다. 이름·순서 변경에도 연결이 유지되고, 항목 삭제 시 그 key를 참조하던 연결은 `연결 안 됨`으로 드러나 재연결을 안내한다.

### 6.3 고정 실적 사건 catalog (Backend 고정 registry, DB seed 아님)

10개 사건 code로 확정한다(review §8 resolution — `키팅 완료`는 이번 catalog에서 제외).

발주 완료, 전체 입고 확정, IQC 합격, LQC 합격, OQC 합격, 전진검수 합격, FAT 합격, 포장 완료, 출발 처리, 납품 완료.

- `발주 완료`는 기존 구매품목의 발주일이 유효하게 입력·저장된 시점을 실적 사실로 사용한다.
- code 의미는 변경 금지이며, `LQC 합격`은 §9.3의 parameterized source로만 연결한다.

### 6.4 활성화 validation과 호환성 fail-closed (R2·R9)

- 생산계획 양식 Active 전 필수 검증: Item scope 일치, 1개 이상 항목, `definition_key`·순서 중복 없음, 필수 항목의 source 연결 존재, 모든 제조 참조 `definition_key`가 같은 Item의 현재 Active 제조 version에 존재, 지원되는 source code·parameter만 사용, 연결 중복 없음.
- 제조 양식 Active 전 필수 검증: Item scope 일치, 1개 이상 단계, immutable `definition_key`, 순서 범위·중복 없음.
- 참조가 끊긴 조합은 조용히 legacy로 fallback하지 않고 양식 활성화 또는 프로젝트 생성을 명확한 한글 오류로 차단한다. 이미 생성된 프로젝트 snapshot은 영향받지 않는다.
- 게시 안내 고정 문구: “기존 프로젝트는 바뀌지 않으며, 이후 생성되는 프로젝트부터 적용됩니다.” 유효한 LinkedV1 양식이 없는 Item은 기존과 같은 legacy 방식으로 프로젝트가 생성된다는 안내를 양식 관리와 프로젝트 생성 화면에 표시한다.

### 6.5 전용 저장 계약과 domain 확장 (R3)

- `FormTemplateManagementPage` 안에 영역을 통합하는 UX는 유지하되, 기존 검사형 `SaveFormTemplateItemRequest` DTO를 공용으로 확장하지 않는다. Item별 제조 template와 생산계획 template·connection은 전용 contract/store/endpoint로 구현한다.
- `ProductionPlanning` domain 추가 시 `NormalizeDomain`(현행 `Quality`/`Manufacturing`만 허용), system-admin scope, manager candidate 허용 부서 매핑을 명시적으로 추가한다. 현행 `domain == "Quality" ? "quality" : "manufacturing"` 이항 분기를 그대로 확장하면 생산계획 관리자가 제조 부서로 잘못 제한되므로 domain별 명시 매핑으로 교체한다(생산계획 domain의 후보는 생산관리 부서 기준).

## 7. 프로젝트 snapshot

### 7.1 생성 경로 단일화와 원자성 (R4)

- 직접 생성, Excel 대량 생성, UL891 set 생성 등 모든 프로젝트 생성 경로가 같은 `CreateInitialProductionControlSnapshotAsync`(단일 helper)를 호출한다.
- 프로젝트·패널·UL891 set 구조, LinkedV1 생산계획 snapshot, 제조 snapshot, 연결 snapshot과 audit를 같은 project creation transaction에서 저장한다. 유효한 LinkedV1 pair가 있으면 전부 snapshot되거나 프로젝트 생성 전체가 실패한다. 일부만 만들어진 중간 상태를 허용하지 않는다.
- 프로젝트 삭제 lifecycle과 격리 purge에 신규 snapshot·connection·revision 테이블을 포함한다(기존 cascade·soft-delete 계약 준수).

### 7.2 Snapshot 시점 잠금 (R2)

- 프로젝트 생성 transaction에서 해당 Item의 Active 생산계획 version과 Active 제조 version을 함께 잠그고, 연결 무결성을 다시 검증한 뒤 정확한 두 version ID·항목·연결을 snapshot한다.
- 검증 실패 시 legacy fallback 없이 명확한 한글 오류로 생성을 차단한다(활성화 검증과 이중 방어).
- snapshot은 생성 시점 version으로 고정되며 이후 template 수정은 기존 프로젝트 의미를 바꾸지 않는다.

## 8. 제조 execution의 snapshot identity 보존 (R5)

- `LINKED_V1` 프로젝트의 패널 제조 시작은 전역 Active 제조 양식을 다시 읽지 않고 프로젝트 제조 snapshot(당시 project revision의 표시명·순서 포함)을 사용한다.
- `panel_manufacturing_execution_steps`에 project manufacturing step `definition_key`를 snapshot한다.
- `LEGACY` 프로젝트는 현행 전역 template version 경로를 그대로 유지한다.
- 기존 일괄 조립 단계 완료 로직의 `MANUFACTURING` item code 의존을 LinkedV1에서는 제거한다. 의미 역할이 필요한 단계는 별도 `step_role` 또는 선택된 definition으로 명시하고 이름·순서 추측을 사용하지 않는다.

## 9. 실적 projection (조회 시 결정적 파생, 저장하지 않음)

### 9.1 기본 계산 규칙 (확정 유지)

- 부서 원본 데이터가 실적의 유일한 source of truth다. projection은 조회 시 같은 원본 facts에 대해 항상 같은 결과를 반환한다(멱등, 비저장). 표·펼침·일정이 단일 projection 조회 API를 공유한다.
- 실적 날짜는 담당자가 입력한 실제 업무 날짜(도착일·출발일 등)를 우선하고, 없으면 시스템 확정 시각의 Asia/Seoul 날짜를 사용한다.
- 재검사 실적 기간은 최초 검사 시작부터 최종 합격까지다. 원본 수정·취소·Pending 해제 후에는 최신 유효 사실로 재계산하되 원본 audit를 삭제·수정하지 않는다.

### 9.2 다중 source 분모·중복 제거 (R7)

- 계획 항목의 target instance key는 최소 `(source_code, source_definition_key?, target_type, target_id)`로 고유화한다.
- 진행률 = 연결된 source별 완료 target instance 수의 합 / 전체 활성 target instance 수의 합. 서로 다른 업무 단위의 수량을 직접 더하지 않으며 수량은 근거로만 표시한다.
- 같은 패널에 OQC·FAT 두 source가 연결되면 서로 다른 두 target instance로 계산하고, 같은 source 중복은 unique constraint로 한 번만 계산한다.
- 실적 시작 = target instance의 최초 유효 사실, 실적 종료 = 모든 활성 target instance가 완료된 마지막 사실. 일부 완료 시 종료일은 비우고 `완료 수/전체 수`를 표시한다.
- target instance가 0개인 연결은 완료로 보지 않고 `착수 전` 또는 `연결 안 됨`으로 구분한다.

### 9.3 LQC parameterized source (R6)

- `LQC 합격`은 프로젝트 전체 boolean이 아니라 `LQC_PASSED + manufacturing_definition_key` 형태의 parameterized source로 연결한다.
- 제조 단계 완료와 해당 단계 LQC 완료는 동일한 project manufacturing definition을 기준으로 집계한다.
- OQC는 단계별 최종 판정, 전진검수·FAT는 패널별 aggregate 최종 판정 1회, IQC는 도착분·구매품목 단위, 제조 이후 품질·물류는 physical panel 단위를 유지한다.

### 9.4 일정 상태 집합 (1차 기획 권장안 확정)

`계획 미입력`, `착수 전`, `진행 중`, `지연`(계획 종료 초과 미완료), `계획 내 완료`, `지연 완료`, `차단`, `연결 안 됨`, `해당 없음`. 기존 `StatusChip`·semantic 상태색 token을 재사용하고, 상태는 색+채움 패턴+텍스트를 병용한다. Open Pending 연결 대상이 있으면 `차단`과 근거를 우선 표시하고 과거 실적을 유지한다. FAT 불필요 프로젝트의 FAT 항목은 `해당 없음`으로 분모·지연에서 제외하고, 취소 패널·품목은 분모 제외·이력 보존한다. 일정 상태·진행률은 18단계 workflow 상태·전체 진행률과 분리하고 그 공식을 변경하지 않는다.

## 10. 프로젝트별 수정 경계 (R8)

- 생산관리 정·부 담당자는 프로젝트의 생산계획 항목(전용 항목 추가 포함), 계획 시작·종료·비고, source 연결만 수정한다.
- 프로젝트 제조 snapshot의 단계 정의 자체는 이번 Task에서 수정하지 않는다. 프로젝트 연결 선택지는 해당 프로젝트에 snapshot된 제조 definition과 고정 사건 catalog로 제한한다.
- project revision은 optimistic concurrency(stale 409, 기존 새로고침 안내 문구)와 field-level audit(기존 생산계획 `InsertAuditAsync` 패턴 확장)를 유지하고, template 원본·다른 프로젝트에 영향을 주지 않는다.
- 자동 실적 시작·종료와 원본 부서 사실은 모든 역할에서 직접 수정 불가다. 필수 항목의 계획 시작·종료가 모두 입력되면 생산계획 단계 `계획 완료` — `LINKED_V1`에만 적용한다.

## 11. API·Backend 계약

- 신규·확장 endpoint: Item별 제조 template CRUD·게시, 생산계획 template·연결 CRUD·게시(전용 contract), 프로젝트 생산관리 projection 조회 1개(표·펼침·일정 공용), 프로젝트 계획 기간·전용 항목·연결 revision 저장, manager binding의 `ProductionPlanning` domain.
- 프로젝트 생성 transaction 내 snapshot 생성(§7). 양식은 기존 `FOR UPDATE`+row_version, 계획 revision은 기존 optimistic row_version 계약을 재사용한다.
- audit: 양식은 `form_template_audit_events` 패턴, 프로젝트 계획 변경은 기존 field-level audit 패턴을 새 항목·연결 변경에 확장. 원본 부서 audit는 불변.
- 부서 원본 mutation 성공 후 projection 조회 실패는 원본을 롤백하지 않는다. 화면은 `저장 완료·일정 새로고침 실패`로 구분하고 재조회로 복구한다.
- 외부 provider 영향 없음. 재사용 확인 대상: `FormTemplateStore` versioning·binding·audit, `ProductionPlanningStore` audit·concurrency, `BusinessDayCalculator`/`BusinessCalendarStore`, 부서 fact store(Materials·Manufacturing·QualityInspections·Logistics·Procurement).

## 12. 화면·UX 계약

| 화면 | 진입 경로 | 핵심 계약 |
| --- | --- | --- |
| 양식 관리 — 생산계획 영역(신설) | 기존 양식 관리 페이지 | Item별 항목·version·연결 편집, `연결 안 됨` 경고, 활성화 validation 오류의 한글 안내, 게시 고정 문구(§6.4) |
| 양식 관리 — 제조 영역(Item 확장) | 같은 페이지 | Item 선택 추가, 기존 UX·저장 패턴 유지, 기존 `생산계획 단계 설정` 진입점은 이동 안내로 통합 |
| 프로젝트 상세 생산관리 탭(`LINKED_V1`) | 프로젝트 상세 | KPI → 6열 표(항목명/필수/계획 기간/실적 기간/진행률/일정 상태) → 계획/실적 가로 막대 일정 → 담당자. 행 펼침은 한 번에 한 항목(구매품목·패널별 근거·Pending). 조회 화면에 input 미노출, `계획 수정`으로 별도 route 이동 |
| 프로젝트 상세 생산관리 탭(`LEGACY`) | 동일 | 현행 예정일 표+체크 캘린더 그대로(코드 경로 유지, 회귀 0) |
| 계획 수정 화면(기존 route 확장) | 생산관리 탭 → `계획 수정` | 기간·비고·전용 항목·연결 수정, field 인접 한글 validation, 409 안내, 기존 Action Feedback 계약 |

- 가로 막대 일정: 하루 단위 고정 축, 항목 열 고정, 가로 스크롤, 열 때 오늘 위치 자동 이동. 계획 막대는 흑백 와이어프레임 외곽선·패턴, 실적 막대는 상태 의미색 채움. 오늘 기준선·주말·공휴일은 기존 `BusinessDayCalculator`/business-days API 기준. 실적 종료 전 진행 중 막대는 최초 시작일~오늘(또는 최신 유효 처리일)로 표시하되 종료일로 저장하지 않는다. 지연 구간·지연 일수는 텍스트 병기.
- 접근성: 6열 표를 공식 대체 수단으로 유지, 막대별 텍스트 설명(aria-label), 색 단독 판단 금지, keyboard 행 펼침.
- 390px: 항목별 카드(상태·계획/실적 기간·지연 텍스트·축약 두 줄 막대), page-level 가로 스크롤 없음.
- 외부 chart 라이브러리 미도입, 흑백 와이어프레임·사각형 기본 디자인과 기존 `DsReadOnlyBanner`·`DsEmptyState`·loading/error 패턴 유지.

## 13. 데이터·Migration

- `database/migrations/0058_*.sql` additive migration 1건(현재 최신 0057 다음 번호): 프로젝트 생산계획 model version 컬럼(기존 행 `LEGACY` 고정), Item별 생산계획·제조 template/version/definition(`definition_key`), 연결 정의, 프로젝트 snapshot·revision, execution step identity 컬럼과 unique 제약(§9.2).
- 기존 테이블 destructive 변경·번호 재사용 금지. fresh/existing DB 모두 검증, rollback은 forward-fix 원칙.
- 기존 `project_production_plans`/`project_production_plan_items` 데이터는 변경·이전하지 않는다.

## 14. Task 고유 안전 경계

- Persistent UAT 영향 없음(격리 DB·runtime만 사용). runtime 교체 없음. 외부 발송·실제 provider 호출 없음.
- local experiment commit만 기승인(`commitApproved: true`). push·PR·merge·대표 repo·Persistent UAT는 별도 승인 필요, `mainMergeApprovalCount 0/3` 유지.
- 실제 회사별 양식 내용 확정·운영 template 일괄 입력은 범위 밖(운영 데이터 입력은 사용자 몫).

## 15. 검증 계획

- Backend 신규: 양식 versioning·3개 관리 주체 권한·domain 매핑(생산계획 관리자 후보가 제조 부서로 제한되지 않음), `definition_key` 복제 유지·삭제 시 활성화 차단, 활성화·생성 fail-closed(제조 version 교체로 연결이 끊긴 조합), 프로젝트 생성 원자성(direct·Excel·UL891 set 동일 계약), 계획 validation(역전·같은 날·stale 409), projection 규칙별 독립 fixture(부분 완료, 다중 source target instance 중복 제거, LQC definition별 집계, 재검사, Pending 차단, FAT 해당 없음, 취소 제외, target 0개).
- 비회귀: 기존 migration DB의 모든 프로젝트가 `LEGACY`로 남고 기존 화면·판정·Excel이 계약상 불변임을 명시적 테스트로 고정. LinkedV1 양식 없는 Item의 새 프로젝트는 legacy 생성. 전역 제조 template 실행·일괄 조립·품질·물류·18단계 진행률 회귀 0.
- 전체: Backend 전체 test suite, Frontend lint/typecheck/unit/build(새 표·Gantt·390px 카드·model version 분기 unit test 포함), migration fresh/existing, isolated Full-Stack 시나리오, desktop/390px privacy-safe screenshot.
- 사용자 검수: user validation checklist를 작성하고 `사용자 검수 대기 — 마지막 일괄 검수`로 추적(완료로 가장하지 않음).

## 16. 명시적 제외 (변경 불가)

- `snapshot 존재 여부 = 새 화면` 판정, legacy `planned_date` backfill, 과거 이름 exact-match 자동 연결, ordinal·표시명 기반 제조/LQC 연결, 일반 form-template item DTO 공용 확장, `키팅 완료` source, 유효하지 않은 LinkedV1 조합의 부분 snapshot·조용한 fallback.
- 기존 프로젝트 화면 변경·데이터 이전·자동 실적 소급, 18단계 workflow·전체 진행률 공식 변경, 부서 원본 화면·확정 상태 전이 재구현.
- `work_items.due_date` 자동 동기화, 지연 에스컬레이션 알림, ERP/MES/회계 연동, 새 표 Excel export(후속), baseline 비교·일/주/월 zoom·대상별 막대·구매 수량 경고·기존 프로젝트 전환 기능(후속 Task), projection 저장·cache(실측 병목 시 P3).
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge.

## 17. 구현 순서 (Codex 구현 계약)

1. `0058` additive migration: model version, template/version/definition/connection, project snapshot/revision, execution identity·제약.
2. 양식 Backend: `ProductionPlanning` domain·candidate 매핑 확장, Item별 제조·생산계획 Draft/Active/Archived 전용 store/endpoint, `definition_key` 복제, 활성화 검증(fail-closed).
3. 프로젝트 생성 통합: direct/bulk/UL891 공용 snapshot helper, LinkedV1/Legacy 명시 분기, transaction 원자성·audit·삭제 lifecycle.
4. 제조 start의 LinkedV1 snapshot 사용, execution `definition_key` 기록, `step_role` 명시(이름·순서 추측 제거).
5. 부서 source adapter와 단일 deterministic projection API(§9 규칙 전부).
6. 프로젝트 계획 revision API·권한·audit(§10 경계).
7. Frontend: 양식 관리 통합 UI, model version 분기 생산관리 탭(6열 표·펼침·Gantt·오늘선·휴일), 390px 카드, 계획 수정 확장. legacy 화면 코드 경로 유지.
8. 검증 전체(§15)와 desktop/390px screenshot, Implementation report·5종 산출물·실험 완료 원장 update, local experiment commit.

## 18. 완료 기준

- §5~§13의 계약 전부 구현: 관리자가 코드 수정 없이 항목 전면 교체 가능, `LINKED_V1` 프로젝트에서 부서 입력만으로 실적·진행률 자동 반영, `LEGACY` 프로젝트 화면·데이터 무변경.
- 표·Gantt가 같은 projection으로 날짜·상태·진행률 일치, 접근성·390px·흑백 와이어프레임 충족.
- §15 자동 검증 전체 통과, Open P0/P1/P2 = 0.
- 5종 산출물 상태·위치 추적, 사용자 검수 상태 `사용자 검수 대기 — 마지막 일괄 검수`, Git 게시는 local commit까지(push·PR·merge 승인 대기 유지).

## 19. 남은 결정 상태

1차 기획 §16-1(사건 catalog)은 review §8 resolution으로 확정됐다(10개 사건, 키팅 제외, 발주 완료 = 발주일 유효 저장). 남은 항목은 모두 후속 Task 범위 결정(비차단)이며 이번 구현을 차단하지 않는다: baseline 비교, 일/주/월 zoom, 대상별 막대, 구매 수량 경고, 기존 프로젝트 전환, 새 표 export, projection cache.

- openBlockingDecisionCount: 0
