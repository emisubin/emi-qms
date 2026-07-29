# TASK-ADMIN-002 — 검사·제조 양식 무코드 관리 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-ADMIN-002`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/admin-002-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`, blocking 0)
- firstPlanningSource: `tasks/admin-002-planning.md` (판단 이력으로 보존, 수정 없음)
- codexReviewSource: `tasks/admin-002-review.md` (`CONDITIONAL_KEEP_WITH_RESOLUTIONS`, Finding 8건 전부 본 문서에 반영)
- approvalChange: `tasks/admin-002-change-001.md` (`fableSecondPlanningApproved: true`, target `docs/33-form-template-management-plan.md`)
- planningApproved: true — Change 001의 experiment fast-track 범위
- implementationApproved: true — 본 문서의 blocking decision 0 조건, local experiment commit까지만

이 문서는 TASK-ADMIN-002의 최종 구현 source of truth다. 1차 기획의 유지 확정 내용을 보존하고 Codex review의 추가·보류·제거 권고와 Finding 8건의 resolution을 구현 가능한 계약으로 통합했다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 참조하며 복사하지 않는다. 이 문서는 대표 repo·`main` merge·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다.

## 1. 한 줄 목표

System Administrator와 지정된 부서 양식 관리자(부서장)가 코드 배포 없이 양식 관리 화면에서 검사 5종·제조 작업 단계 양식을 새 version으로 편집·활성화하고, 기존 성적서·검사·제조실행은 원래 version/snapshot 그대로 재현된다.

## 2. 확정 기준선과 해결할 업무 문제

- IQC 양식은 migration `0032`의 `iqc_report_templates`·`iqc_report_template_versions`·`iqc_report_template_items` seed, 패널 품질(LQC/OQC/CustomerInspection/FAT) 양식은 `0035`의 `panel_quality_template_versions/items` seed로만 존재하며 전용 관리 UI와 draft/publish lifecycle이 없다.
- 제조 시작 4단계 이름은 `ManufacturingStore`의 static `StepNames` 배열에 hard-code되어 실행 시작 시 `panel_manufacturing_execution_steps`로 복제되고, `0034`의 check constraint가 sequence를 1–4로 고정한다. 단계 문구·수 변경에 코드 배포가 필요하다.
- 기존 quality version schema의 activation check는 `is_active=false`이면 `activated_at_utc`가 null이어야 해서, 과거 활성 version의 이력을 보존하는 Archived 상태와 충돌한다(Review P1). 상태 column 추가만으로는 부족하며 constraint 교체가 필요하다.
- 사용자·부서 기반은 이미 존재한다(`qms_users.department_id`, `departments`). generic 부서장 role은 없으므로 명시적 binding으로 표현한다.
- interview 확정 정책: 관리자 전 범위, 지정 부서장 자기 부서 범위, 서버 authoritative, used/active version 불변, 활성화는 새 업무에만 적용, 대표 repo·`main`·Persistent UAT·provider 제외.

## 3. 대상 사용자와 권한 (Review resolution 반영)

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| System Administrator | 전체 양식 조회, draft 편집·활성화, 양식 관리자 지정·해제 | 전 domain 전 양식 | 전 범위 |
| 품질 양식 관리자 (active `Quality` binding) | 품질 양식 조회, draft 편집·활성화 | 품질 domain 양식만 | IQC/LQC/OQC/CustomerInspection/FAT |
| 제조 양식 관리자 (active `Manufacturing` binding) | 제조 단계 양식 조회, draft 편집·활성화 | 제조 domain 양식만 | 제조 작업 단계 template |
| 일반 사용자 | 운영 화면에서 active version 사용(기존과 동일) | 기존 업무 scope | 관리 API 전체 403 |

- **`ADMIN-002-BINDING-SCOPE` (P2) resolution 확정**: 양식 관리자 binding은 `(user_id, department_id, domain)` 단위다. `domain`은 `Quality`/`Manufacturing` 고정 enum이며, active binding 중복만 partial unique index로 차단하고 해제는 soft revoke(해제자·시각 보존)로 처리한다. 지정 시 대상 사용자의 활성 상태와 `department_id` 일치를 검증하고, **관리 mutation 시에도** active binding 존재와 사용자의 현재 부서 일치를 다시 검증해 부서 이동 drift를 차단한다. 부서당 복수 지정을 허용하고 자기 자신 지정도 허용하되 감사 기록을 남긴다(1차 기획 미결정 2 권장안 유지).
- **제거 항목 반영**: 부서 양식 관리자는 목록·상세 모두 자기 domain 범위만 본다. 다른 domain template의 상세 metadata 조회도 403이다.
- binding 지정·해제는 System Administrator만 가능하다. 권한 판정은 서버가 authoritative하며 UI 노출은 보조 수단이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 품질 부서장이 OQC 양식 문구 수정

1. active `Quality` binding 보유자가 `양식 관리`에서 OQC template의 active version을 연다.
2. `새 draft 만들기`로 active version 항목이 복제된 Draft version이 생성된다.
3. 항목명·안내문·응답형식·필수·사진·길이·순서를 수정해 저장하면 서버가 항목 규칙 전체를 검증한다(Draft에서만 저장 허용, 한 transaction full replacement).
4. `활성화` 확인 시 서버가 단일 transaction으로 기존 Active를 Archived로 보관하고 Draft를 Active로 전환하며 감사 row를 남긴다.
5. 이후 새로 생성되는 OQC 검사부터 새 version이 적용되고, 기존 성적서는 원래 version으로 계속 재현된다.

### 시나리오 B — 관리자가 제조 관리자를 지정하고 제조 단계 변경

1. System Administrator가 관리자 지정 영역에서 제조 부서 사용자에게 `Manufacturing` binding을 지정한다.
2. 지정된 사용자가 제조 작업 단계 template의 Draft를 만들어 단계 이름·수(1–10개)를 수정하고 활성화한다.
3. 이후 새로 시작하는 제조 작업은 static 배열이 아니라 활성 template version의 단계를 snapshot으로 복제하고, 실행 row에 source `template_version_id`를 기록한다. 진행 중·완료 실행의 단계는 바뀌지 않는다.

### 시나리오 C — 동시 활성화 충돌 복구

1. 두 관리자가 같은 template에서 각각 활성화를 시도한다.
2. 먼저 도착한 요청이 성공하고, 나중 요청은 expected `row_version` 불일치로 409와 최신 상태 재조회 안내를 받는다. active 유일성은 partial unique index가 최종 방어한다.

### 시나리오 D — active version 부재 fence (`ADMIN-002-NEW-EXECUTION-FENCE` resolution)

1. IQC 성적서 생성·품질 검사 attempt 생성·제조 작업 시작은 각 family의 active version을 row lock으로 읽는다.
2. active version이 없으면 신규 생성만 409와 한글 안내로 차단하고, 기존 report/attempt/execution 조회·진행은 불변이다. seed와 활성화 transaction이 active 공백을 만들지 않으므로 정상 운영에서는 발생하지 않는 방어선이다.

### 시나리오 E — 권한 없음

1. 일반 사용자에게는 `양식 관리` 메뉴가 보이지 않고, URL 직접 진입·API 직접 호출은 서버가 403을 반환한다.
2. 화면은 권한 부족 상태를 조회 오류와 구분해 안내한다.

## 5. 기능 요구사항

### 필수

- [ ] additive migration `0044` — lifecycle·binding·감사·제조 template/provenance·불변 guard trigger·seed·constraint 완화 (7장)
- [ ] 품질 5종 + 제조 작업 단계를 family discriminator로 통합 노출하는 관리 API와 공통 access evaluator (8장)
- [ ] Draft 생성(active 복제) → 항목 full replacement 저장 → 서버 validation → 원자적 활성화 → Draft 취소(Archived) lifecycle
- [ ] Active/Archived version·item의 DB guard trigger 불변 보장 (`ADMIN-002-DB-IMMUTABILITY` P1)
- [ ] `ManufacturingStore` static 단계 배열 제거, 실행 시작 시 활성 template version snapshot + `template_version_id` provenance 기록, 단계 수 기반 완료 판정·문구 일반화
- [ ] System Administrator의 양식 관리자 지정·해제(soft revoke)와 mutation 시 binding·부서 재검증
- [ ] 일반 사용자·타 domain 관리자의 관리 API 403(목록·상세 포함)
- [ ] expected `row_version` 동시성 제어(409)와 template당 active 최대 1개 불변
- [ ] 관리 mutation 전건의 append-only 감사 기록(actor·행위·대상 template/version·시각)
- [ ] adaptive desktop 3영역 / mobile drill-in 관리 UI, loading/empty/error/success·field error focus·활성화 확인·409 복구 안내
- [ ] version 목록 checkbox 선택·전체선택 Excel 내보내기 — 기존 전역 선택 export 계약·audit 패턴 재사용 (`ADMIN-002-EXPORT-CONTRACT` P3, 선택 항목에서 필수로 승격)

### 명시적 제외

- [ ] **새 양식 종류 생성** — v1 관리 대상은 `IQC/LQC/OQC/CustomerInspection/FAT/Manufacturing` 6종 고정 catalog다. 종류 신설은 form-builder 범위의 후속 기능이다 (`ADMIN-002-CATALOG-BOUNDARY` resolution)
- [ ] Word/PDF/Excel 양식 import, drag-and-drop form builder, 조건식·계산식·전자서명, arbitrary response type (Review 보류 — 후속 기능)
- [ ] 실제 운영 양식 내용의 확정·대량 입력과 Persistent UAT 적용 (외부 회신/별도 승인 후속 Task)
- [ ] 기존 완료 report/attempt/execution row의 소급 변경 — rewrite 0건
- [ ] 사용자 계정·조직 IAM 재설계, generic 부서장 role 신설
- [ ] 외부 provider 발송, Persistent UAT migration·runtime handover, 대표 repo·`main`·push·PR·merge

## 6. 업무 규칙과 불변조건

- 한 번이라도 활성화된 version과 그 item은 수정·삭제할 수 없다. 변경은 항상 새 Draft version으로만 한다. API 검사에 더해 DB trigger가 최종 방어한다.
- template당 Active version은 항상 최대 1개다(기존 partial unique index 계약 유지).
- 활성화는 이후 새로 생성되는 report/attempt/execution에만 적용된다. 기존 데이터의 version 참조와 snapshot은 절대 바뀌지 않는다.
- version·item content의 hard delete는 없다. Draft 취소도 Archived 보관이다(1차 기획 미결정 3 권장안 유지).
- 항목 규칙은 기존 DB 계약과 일치시킨다: 응답형식 `Check`/`Text`, `requires_photo`는 `Check`만, `Text` 길이 1–2000, label 1–200, guidance 1–500, item_code/`display_order` 중복 금지. 검사 항목은 template당 최대 50개, 제조 단계는 1–10개다(1차 기획 미결정 4 권장안 유지, `0034` sequence constraint를 1–10으로 additive 완화).
- 제조실행 완료 판정과 안내 문구는 고정 4단계 상수가 아니라 실행 snapshot의 단계 수 기준으로 일반화한다.
- 모든 관리 mutation(draft 생성·저장·활성화·취소, binding 지정·해제)은 같은 transaction에서 append-only 감사 row를 남긴다.
- 관리 API의 조회·변경 판정은 서버가 authoritative다: System Administrator 전 범위, active binding 보유자는 자기 domain만.

## 7. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| IQC template/version/item | `iqc_report_templates`·versions·items — 성적서가 version FK 참조 | 기존 유지 | table·FK 불변, additive column만 |
| 패널 품질 template version/item | `panel_quality_template_versions/items` — stage_code 4종 | 기존 유지 | 동일 |
| 제조 단계 template/version/step | 신규 3-table 계보(template·versions·step items). v1 seed는 현행 4단계 문구 그대로 Active | 신규 | seed 문구는 `ManufacturingStore` 현행 배열과 동일 |
| version lifecycle | 품질 2계보·제조 version에 `lifecycle_status`(`Draft`/`Active`/`Archived`), `row_version`, 생성·수정 actor/시각, `archived_at_utc` additive 추가 | 신규 column | 기존 activation check를 lifecycle 기반 constraint로 교체 |
| 제조실행 provenance | `panel_manufacturing_executions.template_version_id` nullable FK | 신규 column | 기존 실행 null legacy 유지, 신규 실행 필수 기록 |
| 양식 관리자 binding | `(user_id, department_id, domain)` + 지정/해제 actor·시각, active만 partial unique | 신규 table | soft revoke, 이력 전건 보존 |
| 양식 관리 감사 | append-only 감사 table + guard trigger (`admin_master_change_logs`·`0037`/`0042` 검증 패턴 준용) | 신규 table | update/delete 금지 |

```text
Draft(생성·편집·저장 가능) → Active(불변, 신규 업무에 적용) → Archived(불변, 이력 조회)
Draft → (취소 시) Archived
```

- **`ADMIN-002-ACTIVATION-CONSTRAINT` (P1) resolution**: 기존 `is_active`/`activated_at_utc` 동시 충족 check를 제거하고 `lifecycle_status` 기반 constraint로 교체한다 — `Active`는 `is_active=true`·`activated_at_utc not null`, `Draft`는 `is_active=false`·activation/archive metadata null, `Archived`는 `is_active=false`이며 과거 `activated_at_utc`를 **보존할 수 있다**. 활성화 transaction은 기존 Active를 Archived로 전환할 때 `activated_at_utc`를 지우지 않고 `archived_at_utc`를 기록한다.
- **제거 항목 반영(backfill)**: `activated_at_utc` 추정 backfill 표현을 제거한다. 현재 데이터 사실(모든 기존 version row는 seed된 `is_active=true` v1)에 맞춰 명시적으로 backfill한다 — `is_active=true` → `Active`, 그 외 row는 존재 시 보수적으로 `Archived`(편집 재개 금지, 활성 이력 주장 없음).
- **`ADMIN-002-MIGRATION-ORDER` (P2) resolution**: 이 Task의 migration은 `database/migrations/0044_form_template_management.sql`(권장명) 1건으로 고정한다. `0043`은 TASK-SALES-KPI-001 예약이다. 현재 최신 `0042` 이후 additive만 추가하고 기존 migration은 수정하지 않으며 fresh/existing DB를 모두 검증한다.
- **`ADMIN-002-DB-IMMUTABILITY` (P1) resolution**: 세 family 모두에 guard trigger를 추가한다 — version row는 delete 전면 금지, content column 변경은 Draft에서만 허용, 상태 전이는 허용 경로(Draft→Active, Active→Archived, Draft→Archived)와 전이 metadata·`row_version` 갱신만 허용. item/step row의 insert/update/delete는 부모 version이 Draft일 때만 허용한다. `0032`의 finalized-report guard 패턴을 준용한다.
- 신규 table·column 명칭은 제안이며 구현 시 기존 naming convention에 정확히 맞춘다.

## 8. API 계약 (Backend authoritative)

경로 명칭은 제안이며 등록·정책 연결은 기존 `QmsPolicies`/`PermissionRequirement`/`Program.cs` 패턴을 따른다. 공통 access evaluator가 System Administrator role 또는 active binding(domain 일치 + 현재 부서 일치)을 판정한다.

- `GET /api/form-templates` — 허용 범위의 통합 catalog 목록: family discriminator(`IqcReport`/`PanelQualityStage`/`Manufacturing`), 종류·표시명·소유 domain, active version 번호·활성일, draft 존재 여부. 관리 권한이 전혀 없으면 403.
- `GET /api/form-templates/{family}/{templateKey}/versions` — version 목록(번호·`lifecycle_status`·활성/보관 시각·항목 수)과 상세 조회. 범위 밖 template은 목록·상세 모두 403.
- `POST .../versions` — 새 Draft 생성(active 항목 복제). template당 미완 Draft 수 제한은 두지 않되 expected `row_version`으로 중복 생성 경쟁을 409 처리한다.
- `PUT .../versions/{versionId}/items` — Draft 한정 항목 full replacement 저장. 단일 transaction, 6장 validation 전체, expected `row_version` CAS, 실패 시 field 단위 한글 메시지.
- `POST .../versions/{versionId}/activate` — 단일 transaction 활성화: template row lock → Draft 검증 → 기존 Active를 Archived로 보관(활성 이력 보존) → Draft를 Active 전환 → 감사 기록. stale은 409.
- `POST .../versions/{versionId}/cancel` — 미사용 Draft를 Archived로 보관.
- `GET /api/form-templates/my-scope` — 현재 사용자의 관리 가능 여부·domain 목록 projection(navigation 노출용, 서버 판정과 항상 동일 근거).
- `GET/POST /api/form-templates/managers`, `POST .../managers/{bindingId}/revoke` — System Administrator 전용 binding 목록·지정·soft revoke. 지정 시 대상 사용자 활성·부서 일치 검증.
- 운영 소비 경로 변경: IQC 성적서 생성·품질 attempt 생성·제조 시작 store가 active version을 row lock으로 조회하고 부재 시 409. 제조 시작은 snapshot 복제와 `template_version_id` 기록을 같은 transaction에서 수행한다.
- 오류 계약: validation 400, 권한 403, stale/부재 409를 안정적 status와 한글 메시지로 반환하고 raw SQL·내부 식별자를 노출하지 않는다.

## 9. 화면·UX 계약

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 양식 관리 목록 | 운영 navigation `양식 관리`(my-scope 기반 노출) | 허용 범위의 6종 catalog, 소유 domain, active version·활성일, draft 여부 | template 선택, (관리자) 관리자 지정 영역 이동 | 권한 없음 시 메뉴 미노출 + 직접 진입 403 안내 |
| version 목록·상세 | 목록에서 template 선택 | version 번호, Draft/Active/Archived, 활성·보관 시각, 항목 수, checkbox 선택 export | active 상세, 새 draft 생성, draft 선택, 선택 Excel 내보내기 | draft 생성·export 성공/실패를 action 근처 표시 |
| draft 항목 편집 | version 목록에서 Draft 선택 | 항목명·안내문·응답형식(Check/Text)·필수·사진·길이·순서 | 항목 추가·수정·삭제·순서 변경(위/아래 버튼), 저장, 활성화, 취소 | 저장 중·성공·실패, field 오류 첫 항목 focus, 활성화 확인 dialog("기존 active는 삭제가 아니라 보관됩니다"), 409 시 최신 재조회 안내 |
| 관리자 지정 (System Administrator 전용) | 양식 관리 화면 내 전용 영역 | domain별 현재 관리자, 지정·해제 이력 | 사용자 검색·지정·해제 | 지정/해제 성공·실패 feedback, 감사 반영 |

UX 불변조건:

- desktop은 좌측 template 종류, 가운데 version/항목, 우측 편집 panel 3영역. WITHUS 참고 방향(흰 바탕, 얇은 divider, compact tab/search/filter, 낮은 그림자, 파란 active state)을 따른다.
- mobile(390px)은 `양식 → 버전 → 항목` 순차 drill-in으로 전환하고 PC 3열 축소·page-level horizontal overflow를 금지하며 핵심 touch target 44px, `useAdaptiveLayout` 재사용.
- Active/Archived 편집 진입 시 "이 version은 사용 중이라 수정할 수 없습니다" 안내를 명시한다. 활성화는 파괴적으로 보이는 행동과 구분된 확인 절차를 가진다.
- keyboard 접근, label 연결, `aria-live` 상태 안내, 비-드래그 순서 변경 수단을 제공한다.
- 공통 action feedback은 기존 `useActionFeedback` 계약(처리 중 중복 submit 차단, action 근처 성공/실패)을 재사용한다.

## 10. Frontend 구현 계약

- `App.tsx`: navigation에 `양식 관리` 항목과 route를 기존 View/`pathForView` 패턴으로 추가한다. 노출 gate는 `my-scope` projection 결과이며 서버 판정이 authoritative다.
- 신규 양식 관리 page component(기존 페이지 단위 패턴)와 `api.ts` client 확장, `styles.css` 확장. 신규 dependency·lockfile 변경 금지.
- 선택 export는 기존 `ExcelExportAction`·선택 export registry 계약에 version 목록 화면을 등록해 재사용한다.
- loading/empty/error/success 4상태를 목록·version·편집·활성화 각각에서 구분하고, 403 권한 상태와 조회 오류를 구분해 표시한다.

## 11. 기존 기능과의 연결

- 운영 화면(IQC 성적서, 품질 검사, 제조 작업)은 계속 active version만 소비하며 화면 계약은 바뀌지 않는다. 새 알림 event·Teams/Mail 발송은 추가하지 않는다.
- 사용자·부서 master는 TASK-ADMIN-001 계보 기능을 재사용하고 재구현하지 않는다. 양식 관리자 binding만 추가한다.
- 기존 IQC/품질 PDF snapshot·사진·finalized 불변 계약(`0032`/`0035` trigger)은 변경하지 않는다.
- 삭제·복구: hard delete를 도입하지 않고 Archived 보관과 append-only 감사로 처리한다.

## 12. Review Finding Resolution 대조표

| Finding | Severity | 본 문서 반영 위치 |
| --- | --- | --- |
| `ADMIN-002-ACTIVATION-CONSTRAINT` | P1 | 7장 — lifecycle 기반 constraint 교체, Archived의 활성 이력 보존, 명시적 backfill |
| `ADMIN-002-DB-IMMUTABILITY` | P1 | 6·7장 — 세 family version/item guard trigger, delete 전면 금지, Draft 한정 편집 |
| `ADMIN-002-MANUFACTURING-PROVENANCE` | P2 | 7·8장 — execution nullable `template_version_id` FK, 신규 실행 필수 기록, legacy null 유지 |
| `ADMIN-002-BINDING-SCOPE` | P2 | 3장 — `(user, department, domain)` binding, soft revoke, active unique, 지정·mutation 양쪽 부서 재검증 |
| `ADMIN-002-CATALOG-BOUNDARY` | P2 | 5장 — v1은 6종 고정 catalog의 version/item 관리, 새 종류 생성 제외 |
| `ADMIN-002-MIGRATION-ORDER` | P2 | 7장 — ADMIN-002 migration `0044` 고정(`0043`은 영업 KPI 예약) |
| `ADMIN-002-NEW-EXECUTION-FENCE` | P2 | 4·8장 — 세 family 모두 active version row lock 조회, 부재 시 신규 생성 409, 기존 업무 불변 |
| `ADMIN-002-EXPORT-CONTRACT` | P3 | 5·9장 — version 목록 checkbox·전체선택·선택 Excel 내보내기를 필수로 포함 |

Review의 유지 6개 항목(family adapter, Draft→Active→Archived lifecycle과 불변, 권한 모델, 제조 v1 seed·snapshot, desktop 3영역·mobile drill-in·feedback·keyboard, 선택 export 계약)은 1차 기획 내용 그대로 보존했다. 보류 항목(import·arbitrary type·조건식·전자서명·새 종류 생성, 실제 양식 대량 입력·Persistent UAT)은 5장 제외 목록에, 제거 항목(activated_at_utc 추정 backfill, 타 부서 상세 조회 허용 해석)은 3·7장에 반영했다. 1차 기획 미결정 1~5는 standing rule에 따라 권장안(family adapter, 복수 지정 binding, Archived 보관, 항목 50·단계 1–10, 선택 export 포함)을 채택했다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 disposable Full-Stack E2E DB만 사용한다.
- migration: additive `0044` 1건. 기존 migration 수정·번호 재사용 없음. fresh/existing 모두 검증하고 rollback은 forward-fix 원칙.
- 데이터: 기존 report/attempt/execution row rewrite 0건, 실제 운영 양식 내용 변경·입력 0건. seed는 현행 문구 복제만 한다. 테스트 데이터는 isolated DB에만 생성한다.
- runtime 교체: 없음. Development runtime 검증만 수행한다.
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo 반영·Persistent UAT 적용·`main` merge(승인 0/3). Change 001로 승인된 범위는 검증·screenshot·종료 문서 완료 뒤 local experiment commit까지다.

## 14. 검증 계획

- Backend 신규 테스트: 권한 matrix(관리자/자기 domain 관리자/타 domain 관리자/일반 사용자 — 목록·상세·mutation 각각 403 경계 포함), binding lifecycle(지정·중복 지정 차단·soft revoke·부서 drift 차단), version lifecycle(draft 생성·full replacement 저장·validation 경계·활성화·취소·Archived 이력 보존), 불변 trigger(Active/Archived version·item 직접 update/delete 차단), 동시성(중복 활성화·stale 저장 409, active 유일성), active 부재 fence(세 family 신규 생성 409·기존 업무 불변), 제조 snapshot·provenance(신규 실행 FK 기록·legacy null·단계 수 가변 완료 판정), migration fresh/existing·backfill·seed 검증.
- 회귀: IQC 성적서 생성, 품질 attempt 생성, 제조 시작·완료 흐름과 Backend 전체 suite. Frontend lint(error 0)·typecheck·unit·build 전체.
- isolated Full-Stack E2E: 관리자 지정 → 부서장 draft 편집·활성화 → 새 업무에 새 version 적용·기존 snapshot 불변 확인, 무권한 403, 선택 export, cleanup.
- 증빙: desktop/390px privacy-safe synthetic screenshot(목록·version·편집·관리자 지정·권한 없음 상태). `docs/development/privacy-safe-evidence.md` projection 규칙을 따르고 raw body·식별자를 남기지 않는다.
- 사용자 검수: `BATCHED_FINAL` — checklist에 `사용자 검수 대기 — 마지막 일괄 검수`를 유지하고 완료로 가장하지 않는다.

## 15. 완료 기준과 중단 조건

- 기능/권한/데이터: 5장 필수 항목 전부 구현. 범위 밖 mutation 0, 기존 row rewrite 0, hard delete 경로 0, 감사 누락 0, hard-coded 제조 단계 상수 참조 0.
- UX: desktop 3영역·mobile drill-in에서 4상태·오류 focus·활성화 확인·44px target·overflow 0 충족.
- 자동 테스트: Backend·Frontend 전체와 신규 테스트, migration fresh/existing, isolated Full-Stack E2E 통과.
- 산출물: `docs/12-task-completion-policy.md`의 5종 산출물(implementation report·SOP·user manual·Roadmap/실험 원장 update·user validation checklist) 상태·위치 추적.
- Git: allowlist 경로만 stage한 local experiment commit까지. PR 상태 N/A, 게시 승인 없음.
- 중단 조건: 문서·구현의 의미 있는 충돌, 미확정 신규 정책 필요, P0/P1 Finding, 기존 finalized/snapshot 불변 계약 변경이 불가피해지는 경우, `0044` 번호가 이미 선점된 상태 발견 — 구현을 멈추고 보고한다.

## 16. Codex 구현 지시문 (최종)

1. `database/migrations/0044_form_template_management.sql`을 작성한다: 품질 2계보 version에 `lifecycle_status`·`row_version`·actor/시각·`archived_at_utc` additive 추가와 activation check의 lifecycle 기반 교체, 현재 데이터 사실 기반 명시적 backfill, 제조 template/version/step table 신설과 현행 4단계 v1 Active seed, `panel_manufacturing_executions.template_version_id` nullable FK, execution step sequence constraint 1–10 완화, `(user, department, domain)` 관리자 binding table(active partial unique·soft revoke), append-only 감사 table, 세 family의 Active/Archived 불변 guard trigger. 기존 migration은 수정하지 않고 fresh/existing을 검증한다.
2. Backend에 공통 access evaluator(System Administrator 또는 active binding + 부서 재검증)와 family adapter 관리 API(catalog·version·draft 생성·항목 full replacement·활성화·취소·my-scope·관리자 지정/해제)를 기존 endpoints/store/contracts·`QmsPolicies` 패턴으로 추가한다. 모든 mutation은 단일 transaction + expected `row_version` CAS + 같은 transaction 감사 기록이며 400/403/409 한글 응답을 따른다.
3. IQC 성적서 생성·품질 attempt 생성·제조 시작을 active version row-lock 조회로 연결한다: 부재 시 신규 생성 409, 제조는 static 배열을 제거하고 snapshot 복제와 provenance FK 기록을 같은 transaction에서 수행하며 완료 판정·문구를 단계 수 기반으로 일반화한다. 기존 snapshot 회귀 테스트를 함께 작성한다.
4. Frontend에 `양식 관리` navigation·route·page(목록/version/편집 3영역, mobile drill-in, 관리자 지정 영역, `useActionFeedback`·`useAdaptiveLayout` 재사용)와 version 목록 선택 export 등록을 구현한다. 신규 dependency를 추가하지 않는다.
5. Validation Matrix에 따라 14장의 신규·회귀·E2E·screenshot 검증을 수행하고, 5종 산출물과 Roadmap·실험 원장 update 후 allowlist 경로만 stage해 local experiment commit까지만 진행한다. push·PR·merge·Persistent UAT·실제 provider·`main` merge는 금지한다(승인 0/3).

---

openBlockingDecisionCount: 0
