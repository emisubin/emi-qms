The baseline is complete: the four Pending semantic codes are fixed in DB check constraint (migration `0029`), backend `PendingIssueTypes`/`IssueTypeLabel`, and frontend `pending.ts`/`PendingPage.tsx`; automation in materials IQC, quality inspections, and manufacturing stop depends on exact codes; ADMIN-002 (`0044`) provides the reusable admin catalog pattern (row-version CAS, append-only audit, lifecycle status, admin scope). Below is the single first planning draft artifact.

---

# TASK-PENDING-TYPE-001 — Pending 유형 관리 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/pending-type-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`
- draftKind: `EXPERIMENT_FIRST_PLANNING`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: Pending 유형의 표시명·정렬·수동 등록 노출을 바꾸려면 Backend 상수, Frontend union/label, DB check constraint를 함께 수정해야 하며, 자동 workflow가 쓰는 semantic code와 관리자가 바꾸는 표시 설정의 경계가 없다.
- 대상 사용자·역할: 관리 권한이 있는 사용자(관리 화면), Pending을 등록·조회하는 전체 업무 사용자(적용 결과).
- 정상 흐름: 관리자가 유형 catalog에서 표시명·순서·수동 등록 노출을 변경 → 새 Pending 등록 option, 목록 filter, 상세, Excel label에 일관되게 반영.
- 예외·복구 흐름: 권한 없음 403, stale version 409, system semantic 파괴 시도 차단, 참조 중 유형 hard delete 금지, 비활성화 후 재활성화 허용.
- 확정한 정책과 명시적 제외: `TASK-007A` 상태·담당·코멘트·재검사·종결 계약 보존, role/permission 편집기 전체·binary 첨부·실제 provider·대표 repo·`main`·Persistent UAT·push·PR·merge 제외.
- planning으로 넘긴 비차단 미결정 사항: interview 4절의 7개 항목(관리 범위, semantic 보존, lifecycle, 권한, 적용 범위, 동시성, 모바일). 본 문서 12절과 16절에서 선택지·권장안을 제시하며, experiment standing instruction에 따라 Codex review 뒤 Fable 2차 기획에서 권장안을 자동 채택한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

관리 권한 사용자가 code와 migration 수정 없이 Pending 유형의 표시명·순서·수동 등록 노출과 수동 전용 custom 유형을 안전하게 관리하고, 그 결과가 Pending 등록·목록·상세·filter·Excel에 단일 catalog source로 일관되게 반영된다.

## 2. 배경과 해결할 업무 문제

- 현재 Pending 유형은 `Nonconformance`, `Punch`, `ManufacturingStop`, `Other` 네 가지가 세 곳에 고정돼 있다: DB `ck_pending_issues_type` check(`database/migrations/0029_pending_list_foundation.sql`), Backend `PendingIssueTypes`·`IssueTypeLabel`(`backend/src/Emi.Qms.Api/Pending/PendingContracts.cs`, `PendingStore.cs`), Frontend `PendingIssueType` union·`typeOptions`(`frontend/src/pending.ts`, `PendingPage.tsx`).
- 표시명 하나를 바꾸는 데에도 코드 수정·배포가 필요하고, Backend label과 Frontend filter label이 서로 다른 하드코딩이라 drift 위험이 있다.
- 자동 생성 흐름(IQC 부적합, 후속 검사 불합격/PUNCH, 제조 중단)은 exact semantic code에 의존하는데, 이 semantic과 사용자-facing 표시 설정이 같은 상수에 묶여 있어 운영 변경이 자동 workflow를 깨뜨릴 수 있다.
- 현장에서 “구매처 반송”, “현장 수리” 같은 수동 세부 유형이 필요해도 현재는 `기타`로만 등록해야 하며 목록·Excel에서 구분되지 않는다.
- 이 기능이 없으면 유형 관련 운영 요구가 생길 때마다 code change Task가 필요하고, Roadmap 추적 항목(부적합 세부 유형 등)의 수용 경로가 없다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| System Administrator (신규 `PendingType.Manage` 보유) | 유형 catalog 조회·표시명/순서/수동 노출 변경·custom 유형 생성·활성/비활성 | 전체 catalog(비활성 포함)와 audit | catalog 설정 전체. 단, system 유형의 code·semantic은 시스템이 잠금 |
| `Pending.Manage` 보유 업무 사용자 | 수동 Pending 등록 시 활성·수동 노출 유형 선택 | 활성 유형 option | 없음(catalog 변경 불가) |
| `Pending.Read` 보유 사용자 | 목록/상세/filter/Excel에서 유형 label 확인 | filter는 전체 유형(비활성 포함, 과거 데이터 조회용) | 없음 |
| 그 외 인증 사용자 | 해당 없음 | 없음 | 없음 |

권한 검사는 UI 숨김이 아니라 Backend endpoint에서 강제한다. System Administrator의 업무 Pending mutation 우회 금지 규칙은 변경하지 않는다 — 이 Task의 mutation은 업무 Pending이 아니라 기준정보 catalog다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 표시명·순서 변경

1. 관리자가 관리 메뉴의 `Pending 유형 관리` 화면에서 `Other`의 표시명을 `기타(수동)`으로 바꾸고 순서를 마지막으로 이동한다.
2. 시스템이 expectedVersion CAS로 저장하고 audit event를 남긴다.
3. 업무 사용자가 새 Pending 등록·목록 filter·상세·Excel에서 같은 새 label과 순서를 본다. 기존 Pending 행도 같은 catalog label로 표시된다.

### 시나리오 B — 수동 전용 custom 유형 추가

1. 관리자가 `구매처 반송` custom 유형(semantic: 수동 전용)을 생성한다.
2. 시스템이 code 형식·중복·system code 충돌을 검증하고 활성 상태로 저장한다.
3. `Pending.Manage` 사용자가 수동 등록 화면에서 `구매처 반송`을 선택해 등록하고, 목록·filter·Excel에서 해당 label로 구분해 본다. 자동 생성 흐름은 영향받지 않는다.

### 시나리오 C — 비활성화와 과거 데이터 보존

1. 관리자가 더 이상 쓰지 않는 custom 유형을 비활성화한다.
2. 시스템이 신규 수동 등록 option에서 제외하되 기존 Pending의 유형 표시와 filter 조회는 유지한다.
3. 관리자가 필요 시 재활성화한다. hard delete는 어떤 경우에도 제공하지 않는다.

### 시나리오 D — 보호 규칙 차단

1. 누군가 system 유형의 code 변경·삭제·비활성화(자동 생성 차단)를 시도하거나 stale version으로 저장한다.
2. 시스템이 각각 validation/403/409로 차단하고 상태를 바꾸지 않는다.
3. 관리자는 화면에서 실패 사유를 action 근처 feedback으로 확인한다.

## 5. 기능 요구사항

### 필수

- [ ] `pending_issue_type_catalog`(가칭) 단일 catalog: system 4행 seed + custom 행, 표시명·표시 순서·수동 등록 노출·활성 상태·row_version.
- [ ] system 유형 보호: code·semantic 불변, 삭제·비활성 불가(표시명·순서·수동 노출만 변경 가능), `Other`는 수동 fallback으로 수동 노출 해제 불가.
- [ ] custom 유형: 수동 전용 semantic(자동 workflow 연결 없음), code 형식 검증, 생성·rename·reorder·활성/비활성.
- [ ] 신규 `PendingType.Manage` permission과 Backend 강제, System Administrator role에 seed.
- [ ] 단일 label source: Pending 목록/상세/history/filter/Excel의 유형 label을 catalog에서 해석. Backend `IssueTypeLabel`·Frontend `typeOptions` 하드코딩 제거.
- [ ] 수동 등록 option endpoint(활성·수동 노출 유형)와 filter option endpoint(전체 유형).
- [ ] row_version CAS(409), append-only audit event, 참조 여부와 무관한 hard delete 미제공.
- [ ] `pending_issues.issue_type` check constraint를 catalog FK로 대체하는 additive-forward migration `0045`(기존 migration 수정 없음).
- [ ] Desktop 관리 화면 + 모바일 read-only 요약, page-level overflow 0.

### 선택

- [ ] custom 유형 비고(설명) 필드와 관리 화면 표시.
- [ ] 관리 화면 선택 Excel 내보내기(catalog 목록) — ADMIN-002 패턴 재사용.

### 명시적 제외

- [ ] `TASK-007A` 상태 전이·담당·코멘트·재검사·종결 재구현
- [ ] custom 유형을 자동 생성 semantic(부적합/PUNCH/제조 중단)에 연결하는 기능
- [ ] role/permission 편집기 전체, binary 첨부 storage, 실제 provider
- [ ] 대표 repo·`main`·Persistent UAT migration/runtime handover, push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| Pending 유형 관리 (신규 `/pending-types`) | 관리 메뉴(`양식 관리` 인접) | 유형 목록: 표시명, code, system/custom badge, 활성 상태, 수동 노출, 순서, 사용 건수 | rename, reorder(위/아래 이동), 수동 노출 toggle, custom 생성, 활성/비활성 | 공통 Action Feedback으로 저장 성공·409 재조회 안내·validation 오류를 action 근처 표시 |
| Pending 등록 modal (기존 `PendingPage.tsx`) | 기존 진입 유지 | 활성·수동 노출 유형 option(순서 반영) | 유형 선택(기존과 동일) | 기존 feedback 유지 |
| Pending 목록/상세/filter (기존) | 기존 진입 유지 | catalog label, filter는 전체 유형 | 기존과 동일 | 기존과 동일 |
| 모바일 유형 관리 | 같은 URL 적응형 | 유형·상태 read-only card 요약 | mutation 미제공, “PC에서 관리” 안내 | 해당 없음 |

확인할 UX 항목:

- system 유형과 custom 유형이 시각적으로 구분되고, 잠긴 항목(비수정 필드)이 disabled 사유와 함께 보이는가.
- 비활성 유형이 filter에서 “(비활성)” 표시로 과거 데이터 조회를 방해하지 않는가.
- reorder·rename 결과가 저장 직후 목록에 반영되고 409 시 새로고침 유도가 명확한가.
- 390px에서 관리 화면 card 요약이 overflow 없이 표시되는가.

## 7. 업무 규칙과 불변조건

- 자동 생성 코드 경로(`MaterialsStore`, `QualityInspectionStore`, 제조 중단의 `PendingStore` 경로)는 계속 exact semantic code(`Nonconformance`/`Punch`/`ManufacturingStop`)를 사용하며 catalog 상태와 무관하게 항상 성공해야 한다.
- system 유형 4개의 code와 semantic은 rename·delete·remap·비활성화할 수 없다. 관리 가능한 값은 표시명·순서·수동 등록 노출뿐이며, `ManufacturingStop` 수동 등록의 조치 담당 부서 필수 규칙은 유지한다.
- `Other`는 항상 활성·수동 노출 상태를 유지한다(수동 등록 fallback 보장).
- custom 유형은 수동 등록 전용이며 어떤 자동 workflow·재검사·차단 규칙에도 연결되지 않는다.
- 기존 Pending 행의 `issue_type` 값은 어떤 관리 작업으로도 변경되지 않는다(silent remap 금지). label은 조회 시 catalog에서 해석하고, label 변경 이력은 append-only audit이 근거를 보존한다.
- hard delete는 참조 여부와 무관하게 제공하지 않는다. 비활성화만 허용한다.
- 모든 catalog mutation은 Backend에서 permission·CAS·validation을 강제하고 DB constraint가 마지막 방어선이다.
- 이미 `main`에 반영됐거나 실험 계보에 존재하는 migration `0001`~`0044`는 수정하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| Pending 유형 catalog 행 | code, 표시명, 표시 순서, is_system, 수동 노출, is_active, row_version | 신규 (system 4행 seed) | row_version CAS, 변경은 audit event로 추적 |
| Pending issue의 유형 참조 | `pending_issues.issue_type` → catalog code FK | 기존 컬럼 + 신규 FK | 값 불변(remap 금지), check constraint 제거는 FK로 대체 |
| 유형 audit event | actor, action(enum), 대상 code, 변경 전/후 값 | 신규 append-only | 삭제·수정 불가, 개인 식별 원문 없이 user id 참조 |
| system semantic | `Nonconformance`/`Punch`/`ManufacturingStop`/`Other` 상수 | 기존 유지 | 코드 상수와 catalog `is_system` 행이 1:1 |

```text
[custom 유형] 생성(Active) → (rename/reorder/노출 toggle)* → Inactive ⇄ Active   (hard delete 없음)
[system 유형] seed(Active 고정) → (표시명/순서/수동 노출 변경)*                  (상태 전이 없음)
```

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: permission, system 보호, `Other` fallback, code 형식·중복, CAS, FK 무결성, 수동 등록 시 “활성+수동 노출” 재검증(옵션 UI 숨김에 의존하지 않음).
- 필요한 조회와 mutation(신규 `/api/pending-types` 그룹, 정확한 형태는 구현 조사에서 확정):
  - 관리 목록 조회(비활성·사용 건수 포함, `PendingType.Manage`)
  - 수동 등록 option 조회·filter option 조회(`Pending.Read`)
  - custom 생성 / rename·수동 노출 변경 / reorder / activate·deactivate (`PendingType.Manage`, expectedVersion 필수)
- 권한·validation: `QmsPermissions`에 `PendingType.Manage` 추가, migration에서 permission row·System Administrator role grant seed. 표시명 길이·공백, code 정규식(ADMIN-002의 `^[A-Z0-9_]...` 패턴 준용), 순서 중복은 validation 오류.
- transaction·동시성·idempotency: reorder는 전체 순서 집합을 한 transaction으로 적용, row별 expectedVersion 불일치 시 409. `0044`의 row_version trigger 패턴을 재사용한다.
- audit trail: `form_template_audit_events` 패턴(append-only, 제한된 jsonb detail)을 Pending 유형 전용 table로 재사용.
- 기존 코드 수정 지점: `PendingStore.IssueTypeLabel`·목록/상세/summary 조회를 catalog join으로 전환, 수동 create validation을 `PendingIssueTypes.All` 상수 검사에서 catalog 조회로 전환(자동 생성 경로는 상수 유지), `SelectedExcelExportService`의 Pending 유형 label을 같은 catalog 해석으로 통일.
- 외부 provider 영향: 없음. 알림 문구는 기존 생성 흐름의 label 사용 방식을 따르며 신규 발송 경로를 만들지 않는다.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다.

## 10. Frontend 고려사항

- route/component: `App.tsx` view union에 `pending-types` 추가, 관리 메뉴에 `Pending 유형 관리` 항목, 신규 `PendingTypeManagementPage.tsx`와 `pendingTypes.ts`(API·type). `FormTemplateManagementPage.tsx`의 구조(권한 prop, `data-mobile-experience`, panel 패턴)를 재사용한다.
- 기존 화면 변경: `pending.ts`의 union을 string 기반 code + label 응답 신뢰로 완화하고, `PendingPage.tsx`의 하드코딩 `typeOptions`를 option endpoint 조회로 대체(`ManufacturingStop` 조치 담당 부서 필수 분기는 semantic code 기준 유지).
- loading/empty/error/success: 관리 목록 loading skeleton, custom 0건 empty 문구, 409/403/validation 구분 표시.
- 공통 Action Feedback: TASK-UX-001 A1/A2의 구조화 feedback 패턴을 mutation별로 적용.
- 접근성: 목록 button에 label, reorder는 위/아래 button(드래그 전용 금지), 상태 badge에 텍스트 병기.
- 390px/mobile/narrow pane: card 요약 read-only, page-level horizontal overflow 0, 의미 기반 shape 체계(TASK-MOBILE-002) 준수.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: Pending 생성·전이·알림 계약(`TASK-007A`)은 그대로 두고 label 해석만 catalog로 바꾼다. `TASK-007B` 집계는 open 상태 기준이라 유형 catalog 변경의 영향이 없음을 회귀로 확인한다.
- 권한/관리자: `PendingType.Manage`는 ADMIN 계열 화면 gate 패턴을 따르고 role 편집기는 만들지 않는다.
- Excel/PDF/첨부: 선택 Excel의 Pending 유형 column은 column allowlist(`SelectedExportColumnRegistry`) 변경 없이 label 값의 source만 catalog로 바뀐다.
- Teams/Mail: 변경 없음.
- 삭제·복구/감사: hard delete 미제공, audit event는 관리자 감사 조회 후속 Task에서 노출 가능하도록 append-only로 저장한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A | 고정 4유형의 표시 설정만 관리(신규 유형 없음) | 최소 위험, FK 전환 불필요 | “세부 유형” 요구를 다시 code change로 처리, catalog 가치 절반 |
| B (권장) | 고정 4유형 표시 설정 + 수동 전용 custom 유형, `issue_type`을 catalog FK로 전환 | 실제 운영 요구(세부 유형) 수용, 자동 semantic과 완전 분리, ADMIN-002 검증된 패턴 재사용 | migration에서 check → FK 전환 필요, 기존 화면 label source 전환 회귀 범위 |
| C | custom 유형을 system semantic(부적합 등)에 연결 가능 | 세부 유형이 자동 흐름 통계에 합산 | 재검사·차단·집계 의미가 복잡해지고 잘못된 연결 시 workflow 오염 — 이번 범위에서 위험 대비 가치 낮음 |

권장안은 B다. C의 semantic 연결은 실측 요구가 확인될 때 별도 NEW_FEATURE로 분리한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증한다.
- migration 필요 여부: additive `0045` 1건 — catalog table·seed 4행·audit table·permission seed·`ck_pending_issues_type` check를 FK로 대체. fresh와 기존(`0044`까지 적용) DB 모두 검증하고 rollback은 forward-fix 원칙을 따른다.
- 외부 발송/실제 데이터 영향: 없음.
- runtime 교체 여부: 없음(5174 Development runtime은 현재 branch 반영 원칙 유지).
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo 반영·Persistent UAT 적용은 이 Task 범위 밖이며 별도 승인 전 수행하지 않는다.

## 14. 검증 계획

- 최소 테스트: Backend API test — 권한(관리/비관리/미인증), system 보호(code 변경·비활성 시도 차단), `Other` fallback, custom 생성·중복 code, rename·reorder CAS 409, option endpoint의 활성·수동 노출 필터링, 수동 create의 비활성 유형 거부, 자동 생성 경로 회귀.
- 영향 영역 회귀: Pending 목록/상세/summary label, 선택 Excel Pending label, `TASK-007B` 집계, Frontend lint/typecheck/unit/build, 기존 Backend 전체 suite(`401/401` 기준선) 유지.
- migration 검증: fresh 전체 적용과 기존 `0044 → 0045` upgrade, 기존 pending 데이터 보존 확인.
- PR/CI: 이 experiment 범위는 local commit까지이며 PR/CI는 승격 Task에서 수행.
- 사용자 검수: desktop/390px synthetic screenshot을 포함해 `사용자 검수 대기 — 마지막 일괄 검수`로 기록한다.

## 15. 완료 기준

- 기능/권한/데이터: 5절 필수 항목 전부 구현, system semantic 파괴 불가, 과거 의미 보존, 403/409/validation 차단 확인.
- UX: desktop 관리 화면과 mobile read-only 요약, page-level overflow 0, 공통 Action Feedback 적용.
- 자동 테스트: Backend 전체, Frontend lint/typecheck/unit/build, fresh·기존 migration, isolated Full-Stack 영향 시나리오 통과.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist의 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수` (완료로 표기하지 않음).
- PR 상태: 해당 없음(local experiment commit까지).

중단 조건: 자동 생성 경로가 catalog 상태에 의존하게 되는 설계 충돌, 기존 migration 수정이 불가피해지는 경우, Repository source 간 의미 있는 충돌 발견 시 구현을 중단하고 보고한다.

## 16. 미결정 사항

experiment standing instruction에 따라 아래 항목은 사용자 재질문 없이 Codex review 뒤 Fable 2차 기획에서 권장안을 확정한다(`FABLE_RECOMMENDATION_AUTO_ADOPT`). 안전 blocking 충돌이 발견되면 자동 채택하지 않는다.

| 번호 | 질문 | 선택지 | 권장안 | 사용자 결정 |
| ---: | --- | --- | --- | --- |
| 1 | 관리 범위 | A 고정 4유형 표시 설정만 / B 고정 4유형 + 수동 전용 custom / C custom의 semantic 연결 허용 | B — 세부 유형 요구를 수용하면서 자동 semantic과 분리 | 자동 채택 예정 |
| 2 | semantic 보존 | system code 완전 잠금 / 표시 설정만 개방 | system 4행 code·semantic·활성 잠금 + 표시명·순서·수동 노출만 개방, custom은 수동 전용 | 자동 채택 예정 |
| 3 | lifecycle | 비활성화만 / soft delete / label snapshot 저장 | 비활성화·재활성화만, hard delete 없음, label은 catalog 실시간 해석 + append-only audit로 과거 근거 보존 | 자동 채택 예정 |
| 4 | 권한 | System Administrator 전용 role 검사 / 신규 전용 permission / 부서장 binding | 신규 `PendingType.Manage` permission을 System Administrator에 seed — catalog는 전사 공통이라 부서 scope 불필요, 후속 위임은 permission grant로 확장 가능 | 자동 채택 예정 |
| 5 | 적용 범위 | label을 화면별 개별 관리 / 단일 catalog source | 단일 catalog source: Backend 조회·Excel·Frontend option 모두 catalog 해석, 하드코딩 제거 | 자동 채택 예정 |
| 6 | 동시성 | catalog 전역 version / row별 row_version CAS | `0044` 패턴의 row별 row_version + trigger, reorder는 전체 집합 transaction | 자동 채택 예정 |
| 7 | 모바일 | mutation 전체 제공 / 일부 toggle 제공 / read-only | read-only 요약만 제공, 관리 mutation은 desktop 전용(관리자 모바일 고도화는 기존 ADMIN 후속 항목) | 자동 채택 예정 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Pending/`(store·contracts·endpoints의 label·validation·option), 신규 PendingType store/contracts/endpoints, `Identity/QmsPermissions.cs`, `DataExports/SelectedExcelExportService.cs`, `Program.cs` 등록.
- Frontend: 신규 `PendingTypeManagementPage.tsx`·`pendingTypes.ts`, `App.tsx`(route·관리 메뉴), `PendingPage.tsx`, `pending.ts`, `api.ts`, `styles.css`.
- DB/Migration: 신규 `database/migrations/0045_*.sql` 1건(additive + check→FK 전환).
- Tests/Scripts: Backend API tests 신규·기존 Pending/export 회귀, Frontend unit, Full-Stack 영향 시나리오.
- Docs: Roadmap의 TASK-PENDING-TYPE-001 상태, 완료 원장, Task 종료 산출물.

## 18. Roadmap 연결

- 선행 Task: `TASK-007A`(완료), `TASK-ADMIN-002` 패턴(완료), Task identity `PASS_CREATE`.
- 후속 Task: 부적합 조치 세부 유형 정책 change(`TASK-007A` 계열), 관리자 audit 조회 UI(NOTIFY-005 후속과 동일 계열), custom semantic 연결(필요 시 별도 NEW_FEATURE), 승격·UAT Task.
- 현재 Go/No-Go: Roadmap 실행 큐 3.4·완료 원장 우선순위 1과 일치 — Go(Fable 2-pass 계약 범위 내).
- 별도 Task로 분리할 항목: 첨부 storage, QR landing, 운영 전환, role 편집기.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-19 | experiment fast-track standing instruction과 2-pass 승인(change-001) | interview 생략 근거·비차단 정책 7건을 Fable 권장안 자동 채택 경로로 고정 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

Codex 구현 지시문 초안(2차 기획 확정 후 사용): migration `0045`로 catalog·audit·permission을 additive 생성하고 `ck_pending_issues_type`을 FK로 대체한 뒤, Backend에서 `PendingType.Manage` 강제·system 보호·CAS·단일 label 해석을 구현하고, Frontend에 `/pending-types` 관리 화면과 catalog 기반 option을 연결한다. 자동 생성 경로 상수와 `TASK-007A` 계약은 수정하지 않는다. 검증은 14절, 경계는 13절을 따른다.

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 7
