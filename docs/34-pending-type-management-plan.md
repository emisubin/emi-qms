# TASK-PENDING-TYPE-001 — Pending 유형 관리 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-PENDING-TYPE-001`
- authoringModel: `FABLE_5`
- taskType: `NEW_FEATURE`
- interviewSource: `tasks/pending-type-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/pending-type-001-planning.md` (보존, 수정 없음)
- codexReviewSource: `tasks/pending-type-001-review.md` (Finding 6건 전부 본 계약에 반영)
- approvalChange: `tasks/pending-type-001-change-001.md` (`fableSecondPlanningApproved: true`, exact target 승인)

이 문서는 `experiment/*` fast-track의 Fable 2차 기획으로, TASK-PENDING-TYPE-001의 최종 구현 source of truth다. 1차 기획과 Codex review는 판단 이력으로 보존하며 이 문서 하나로 구현 범위·권한·상태·data lifecycle·UX·검증·제외 범위를 판단한다. 이 문서는 실험 branch local 구현 계약일 뿐이며 대표 repo·`main` merge·push·PR·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/` 문서를 따르고 여기에 복사하지 않는다.

## 1. 한 줄 목표

관리 권한 사용자가 code·migration 수정 없이 Pending 유형의 표시명·순서·수동 등록 노출과 수동 전용 custom 유형을 안전하게 관리하고, 그 결과가 Pending 등록·목록·상세·filter·선택 Excel에 단일 catalog source로 일관되게 반영되며, 자동 workflow semantic은 어떤 관리 작업으로도 깨지지 않는다.

## 2. 확정된 Repository 기준선 (2차 기획 시점 재확인)

아래 사실은 현재 branch의 실제 코드에서 재확인했다.

- Pending 유형 4종(`Nonconformance`, `Punch`, `ManufacturingStop`, `Other`)이 세 곳에 고정돼 있다: DB `ck_pending_issues_type` check(`database/migrations/0029_pending_list_foundation.sql`), Backend `PendingIssueTypes` 상수와 `IssueTypeLabel` switch(`backend/src/Emi.Qms.Api/Pending/PendingContracts.cs`, `PendingStore.cs`), Frontend `PendingIssueType` union과 하드코딩 `typeOptions`(`frontend/src/pending.ts`, `PendingPage.tsx`).
- 자동 생성 경로는 exact code에 의존한다: 자재 IQC 부적합은 `CreateOrReuseMaterialNonconformanceAsync`(SQL literal `'Nonconformance'`), 제조 중단은 `CreateManufacturingStopAsync`(SQL literal `'ManufacturingStop'`), 후속 품질 검사 실패는 `PendingIssueTypes.Nonconformance`/`Punch` 상수를 사용하며 호출자는 `MaterialsStore`, `ManufacturingStore`, `QualityInspectionStore`다.
- 수동 등록 validation은 `PendingIssueTypes.All` 상수 검사를 사용하고, `ManufacturingStop` 수동 등록에는 조치 담당 부서 필수 규칙이 있다(`PendingStore.cs`의 create validation).
- 선택 Excel의 Pending 유형 column은 `SelectedExcelExportService`가 Backend 응답의 `IssueTypeLabel` 값을 그대로 사용한다. 즉 Backend projection이 catalog 기반으로 바뀌면 Excel은 추가 수정 없이 따라온다(회귀 확인 대상).
- 권한은 `Pending.Read`/`Pending.Manage`가 분리돼 있고 endpoint에서 permission claim 검사로 강제한다(`PendingEndpointExtensions.cs`의 `HasPermission` 패턴). permission seed 패턴은 migration `0043`(`Sales.Target.Manage`)에 있다.
- `TASK-ADMIN-002`(migration `0044`)에 row_version 증가 guard trigger, append-only audit table(`form_template_audit_events`)과 update/delete 차단 trigger의 검증된 패턴이 있다.
- Frontend 관리 화면 선례: `FormTemplateManagementPage.tsx`, `App.tsx`의 view union·`/form-templates` route·관리 메뉴(`양식 관리`) 등록 패턴.
- 최신 migration은 `0044`, 누적 자동 기준선은 Backend `401/401`, Frontend `109/109`, fresh 및 `0042 → 0044` upgrade다.
- Roadmap 실행 큐 3.4와 완료 원장 우선순위 1이 이 Task를 가리키며 상태는 `IN_PROGRESS / FABLE_2_PASS`다.

문서와 구현 사이의 의미 있는 충돌은 발견하지 못했다.

## 3. 확정 정책 (비차단 7건 — standing instruction에 따른 권장안 채택)

Interview 4절의 7개 비차단 항목을 1차 기획 권장안과 Codex review resolution을 반영해 아래와 같이 확정한다. 모두 기존 보안·권한·workflow 불변조건을 보존한다.

| # | 항목 | 확정 내용 | 근거·trade-off |
| ---: | --- | --- | --- |
| 1 | 관리 범위 | **B안**: 고정 system 4유형의 표시 설정 관리 + 수동 전용 custom 유형 추가, `pending_issues.issue_type`을 catalog FK로 전환 | 표시 설정만 관리하는 A안은 “구매처 반송” 같은 세부 유형 요구를 다시 code change로 되돌린다. custom의 자동 semantic 연결(C안)은 재검사·차단·집계 의미를 오염시킬 위험이 커 별도 NEW_FEATURE로 분리 |
| 2 | semantic 보존 | system 4행의 code·`is_system`·`is_active`는 DB trigger로 불변. 관리 가능한 값은 표시명·순서·수동 노출뿐. custom은 항상 수동 전용이며 semantic 필드 자체를 두지 않는다 | 자동 생성 경로가 catalog 상태와 무관하게 항상 성공해야 함. custom semantic 필드는 review에서 제거 판정 |
| 3 | lifecycle | custom은 `Active ⇄ Inactive` 전이만 존재. 모든 행(참조 0건 포함)에 hard delete 미제공. label은 조회 시 catalog 실시간 해석, 변경 전/후 값은 append-only audit이 근거 보존. Pending 행별 label snapshot은 만들지 않는다 | 참조 0건 hard delete 예외는 review에서 제거 판정. snapshot 저장은 범위 초과 |
| 4 | 권한 | 신규 `PendingType.Manage` permission(전사 catalog 전용)을 System Administrator role에만 seed. 부서 scope 없음. System Administrator의 업무 Pending mutation 우회 금지는 그대로 | catalog는 전사 공통 기준정보라 ADMIN-002식 부서장 scope 불필요. 향후 위임은 permission grant로 확장 가능하되 role 편집 UI는 보류 |
| 5 | 적용 범위 | Backend catalog projection 하나에서 목록·상세·filter·수동 등록 option·선택 Excel label을 전부 파생. `IssueTypeLabel` switch와 Frontend `typeOptions` 하드코딩 제거. Frontend는 catalog 조회 실패 시 하드코딩 fallback 없이 fail-closed | fallback 허용 시 단일 source와 server validation이 다시 갈라짐(review P2) |
| 6 | 동시성 | `0044` 패턴의 row별 `row_version` + 증가 guard trigger. 단건 mutation은 `expectedRowVersion` 필수·불일치 409. reorder는 전체 catalog의 `(code, expectedRowVersion, newSortOrder)` 집합을 한 transaction에서 lock·검증·전량 적용하며 하나라도 stale이면 전체 409 | row별 CAS만으로는 부분 적용·순서 중복·동시 rename 경쟁을 못 막음(review P2) |
| 7 | 모바일 | 390px는 read-only card 요약만 제공, 관리 mutation은 desktop 전용. mutation control의 모바일 축소 복제는 만들지 않는다 | 관리자 모바일 고도화는 기존 ADMIN 후속 항목 |

추가 확정(review `추가` 판정 반영): custom code는 관리자가 입력하지 않는다. 서버가 `CUSTOM_` prefix의 충돌 불가능한 immutable 식별자를 생성하고, 사용자는 표시명(필수)과 설명(선택)만 입력하며 code는 read-only로만 표시한다. code rename 기능은 존재하지 않는다.

## 4. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| `PendingType.Manage` 보유자 (seed: System Administrator) | catalog 조회, 표시명·설명·순서·수동 노출 변경, custom 생성, custom 활성/비활성 | 전체 catalog(비활성 포함), 유형별 사용 건수 | catalog 표시 설정 전체. system 행의 code·semantic·활성 상태와 `Other` 수동 노출은 시스템이 잠금 |
| `Pending.Manage` 보유 업무 사용자 | 수동 Pending 등록 시 유형 선택 | 활성·수동 노출 유형 option | 없음(catalog 변경 불가) |
| `Pending.Read` 보유 사용자 | 목록/상세/filter/Excel에서 유형 label 확인 | filter option은 비활성 포함 전체 유형(과거 데이터 조회용) | 없음 |
| 그 외 인증 사용자 | 해당 없음 | 없음 | 없음 |

- 모든 검사는 UI 숨김이 아니라 Backend endpoint의 permission claim 검사로 강제한다(기존 `HasPermission` 패턴).
- 이 Task의 mutation 대상은 업무 Pending이 아니라 기준정보 catalog다. `TASK-007A`의 상태·담당·actor 규칙과 `Pending.Manage` 계약은 변경하지 않는다.

## 5. 핵심 사용자 시나리오

### 시나리오 A — 표시명·순서 변경
1. 관리자가 관리 메뉴 `Pending 유형 관리`(`/pending-types`)에서 `Other`의 표시명을 바꾸고 순서를 이동한다.
2. rename은 해당 행 `expectedRowVersion` CAS로, reorder는 전체 집합 원자 계약으로 저장되고 각각 audit event가 남는다.
3. 업무 사용자는 등록 option·목록·상세·filter·선택 Excel에서 같은 새 label·순서를 본다. 기존 Pending 행도 같은 catalog label로 표시된다(code 불변).

### 시나리오 B — custom 유형 추가
1. 관리자가 표시명 `구매처 반송`(설명 선택)을 입력해 custom 유형을 생성한다. code는 서버가 `CUSTOM_*`로 생성하며 화면에는 read-only로 표시된다.
2. 시스템이 표시명 중복·길이·공백을 검증하고 활성·수동 노출 상태로 저장한다.
3. `Pending.Manage` 사용자가 수동 등록에서 이 유형을 선택하고, 목록·filter·Excel에서 구분해 본다. 자동 생성 흐름은 영향받지 않는다.

### 시나리오 C — 비활성화와 과거 보존
1. 관리자가 custom 유형을 비활성화한다.
2. 신규 수동 등록 option에서 제외되지만 기존 Pending의 유형 표시와 filter 조회(`(비활성)` 표기)는 유지된다.
3. 필요 시 재활성화한다. hard delete는 어떤 행에도 제공하지 않는다.

### 시나리오 D — 보호 규칙 차단
1. system 행의 code 변경·비활성화·삭제, `Other` 수동 노출 해제, stale version 저장, 비활성 유형으로의 수동 등록을 시도한다.
2. 시스템이 각각 validation 오류(400)/403/409로 차단하고 상태를 바꾸지 않으며, DB trigger·FK가 마지막 방어선으로 동일 시도를 차단한다.
3. 실패 사유는 공통 Action Feedback으로 action 근처에 표시된다.

### 시나리오 E — catalog 조회 실패(fail-closed)
1. 수동 등록 화면에서 유형 option 조회가 실패한다.
2. Frontend는 옛 하드코딩 option으로 fallback하지 않고 유형 선택을 비활성화한 채 오류와 재시도를 보여 준다. 자동 생성 경로는 catalog 조회와 무관하게 기존 상수·FK 경로로 동작한다.

## 6. 기능 요구사항

### 필수
- [ ] `pending_issue_type_catalog`(가칭) 단일 catalog table: `code`(PK), `display_name`, `description`(null 허용), `sort_order`(unique), `is_system`, `is_manual_enabled`, `is_active`, `row_version`, 생성·수정 시각. system 4행 seed(현재 label 부적합/PUNCH/제조 중단/기타, 현재 동작 보존을 위해 4행 모두 수동 노출 활성).
- [ ] system 보호를 DB에서 강제: system 행의 `code`/`is_system`/`is_active` 변경 차단 trigger, `Other` 행의 `is_manual_enabled` 해제 차단, catalog 전 행 delete 차단 trigger, `row_version` 증가 guard(`0044` 패턴).
- [ ] `pending_issues.issue_type` → catalog `code` FK(`ON UPDATE RESTRICT ON DELETE RESTRICT`)로 `ck_pending_issues_type` check 대체. 기존 행 값은 불변(remap 금지).
- [ ] custom 유형: 서버 생성 immutable `CUSTOM_*` code, 표시명·설명 입력, rename(표시명)·reorder·수동 노출 toggle·활성/비활성. 표시명은 trim 후 중복 금지.
- [ ] 신규 `QmsPermissions.PendingTypeManage`(`PendingType.Manage`)와 endpoint 강제, migration에서 permission row와 System Administrator role grant seed(`0043` 패턴).
- [ ] 단일 label source: 모든 Pending 응답 projection(목록·상세·summary·history 표시용)의 `IssueTypeLabel`을 catalog join 해석으로 전환하고 `IssueTypeLabel` switch 하드코딩 제거. 선택 Excel은 응답 label을 그대로 사용하므로 column allowlist 변경 없이 회귀로 확인.
- [ ] option endpoint 2종: 수동 등록 option(활성+수동 노출, 순서 정렬)과 filter option(비활성 포함 전체, `isActive` 포함). 수동 Pending create는 요청 시점에 catalog를 재조회해 `is_active && is_manual_enabled`를 서버에서 재검증.
- [ ] reorder 원자 계약: 전체 catalog의 `(code, expectedRowVersion, newSortOrder)` 집합 수신, 한 transaction에서 전량 lock·검증·적용, 부분 적용 없음, stale 1건이라도 전체 409, 결과 순서 중복 금지.
- [ ] append-only audit table(가칭 `pending_issue_type_audit_events`): actor user id, action enum, 대상 code, 변경 전/후 제한 필드(jsonb), 발생 시각. update/delete 차단 trigger. 개인 식별 원문 저장 금지.
- [ ] `PendingIssueTypes.All`은 자동 생성 semantic 상수 집합으로만 유지하고 수동 create·filter 허용값 검사에서 제거(catalog 조회로 대체). 자동 생성 경로의 exact 상수·SQL literal은 수정하지 않는다.
- [ ] Desktop 관리 화면(`/pending-types`) + 모바일 read-only card 요약, page-level horizontal overflow 0, 공통 Action Feedback 적용.
- [ ] Frontend fail-closed: catalog option 조회 실패 시 하드코딩 fallback 금지, 등록 유형 선택 비활성화와 오류·재시도 표시.

### 선택
- [ ] 관리 화면의 최근 변경 요약(감사 원장 직접 조회 UI가 아니라 최소 표시). 구현 부담이 크면 생략하고 후속 audit 조회 Task로 넘긴다.

### 명시적 제외
- [ ] `TASK-007A` 상태 전이·담당·코멘트·재검사·종결 재구현
- [ ] custom 유형의 자동 semantic(부적합/PUNCH/제조 중단) 연결, 통계 roll-up, 자동 재검사 연결
- [ ] catalog 자체의 선택 Excel 내보내기(review 보류 판정)
- [ ] 관리자 code 직접 입력·code rename, 참조 0건 hard delete 예외, mobile mutation
- [ ] role/permission 편집기 전체, 부서장 위임 UI, binary 첨부 storage, 실제 provider
- [ ] 대표 repo·`main`·Persistent UAT migration/runtime handover, push·PR·merge

## 7. 업무 규칙과 불변조건

1. 자동 생성 경로(자재 IQC 부적합, 후속 품질 부적합/PUNCH, 제조 중단)는 계속 exact semantic code를 사용하며 catalog의 표시 설정과 무관하게 항상 성공해야 한다. system row가 없거나 비활성인 schema drift 상황에서는 silent fallback 없이 FK/조회 실패로 드러나야 하고, 정상 운영에서는 DB trigger 때문에 그 상태가 될 수 없다.
2. system 4행의 code·semantic·활성 상태는 rename·delete·remap·비활성화할 수 없다. 관리 가능 값은 표시명·설명·순서·수동 노출뿐이고, `Other`는 항상 활성·수동 노출을 유지한다(수동 등록 fallback 보장). `ManufacturingStop` 수동 등록의 조치 담당 부서 필수 규칙은 semantic code 기준으로 유지한다.
3. custom 유형은 수동 등록 전용이며 어떤 자동 workflow·재검사·차단 규칙에도 연결되지 않는다.
4. 기존 Pending 행의 `issue_type` code는 어떤 관리 작업으로도 변경되지 않는다. 현재 화면·현재 Excel은 최신 catalog label을 사용하고, 변경 당시의 이전/새 값은 append-only audit이 근거를 보존한다. Pending 행별 label snapshot은 저장하지 않는다.
5. hard delete는 참조 여부와 무관하게 어떤 행에도 제공하지 않는다.
6. 모든 catalog mutation은 Backend에서 permission·CAS·validation을 강제하고 DB constraint·trigger가 마지막 방어선이다. Frontend 숨김·비활성화는 보조 수단이다.
7. 이미 존재하는 migration `0001`~`0044`는 수정하지 않는다. 신규 migration은 additive `0045` 1건이다.
8. System Administrator의 업무 Pending mutation 우회 금지, `Pending.Read`/`Pending.Manage` actor 규칙, `TASK-007A`의 상태·알림·work item 계약은 변경하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| Pending 유형 catalog 행 | code, 표시명, 설명, 순서, is_system, 수동 노출, 활성, row_version | 신규(system 4행 seed) | row_version CAS + 증가 guard, delete 차단, system 필드 불변 trigger |
| Pending issue 유형 참조 | `pending_issues.issue_type` → catalog code FK(RESTRICT/RESTRICT) | 기존 컬럼 + 신규 FK(check 대체) | 값 불변, silent remap 금지 |
| 유형 audit event | actor, action enum, 대상 code, before/after 제한 jsonb, 시각 | 신규 append-only | update/delete 차단 trigger, 개인 식별 원문 금지 |
| system semantic 상수 | `PendingIssueTypes` 4종 | 기존 유지 | 자동 생성 전용, catalog `is_system` 행과 1:1 |

```text
[custom 유형] 생성(Active) → (표시명/설명/순서/수동 노출 변경)* → Inactive ⇄ Active   (delete 없음)
[system 유형] seed(Active 고정) → (표시명/설명/순서/수동 노출* 변경)                 (상태 전이 없음, Other는 수동 노출 고정)
```

컬럼·제약의 정확한 SQL 명칭은 구현 시 `0044` 패턴에 맞춰 확정하되, 위 필드 집합·불변조건·trigger 동작은 계약으로 고정한다.

## 9. API·Backend 계약

신규 `/api/pending-types` group (기존 Pending endpoint의 인증·permission 검사 패턴 재사용):

| 동작 | 권한 | 계약 요점 |
| --- | --- | --- |
| 관리 목록 조회 | `PendingType.Manage` | 비활성 포함 전체 + 유형별 사용 건수 + row_version |
| 수동 등록 option 조회 | `Pending.Read` | `is_active && is_manual_enabled`, sort_order 정렬 |
| filter option 조회 | `Pending.Read` | 전체 유형 + `isActive`, sort_order 정렬 |
| custom 생성 | `PendingType.Manage` | 입력은 표시명(필수)·설명(선택)만. 서버가 `CUSTOM_*` code 생성, 표시명 trim·중복·길이 validation |
| 표시명/설명/수동 노출 변경 | `PendingType.Manage` | `expectedRowVersion` 필수, 불일치 409. system 행은 표시명·설명·순서·수동 노출만, `Other` 수동 노출 해제는 validation 오류 |
| reorder | `PendingType.Manage` | 전체 집합 `(code, expectedRowVersion, newSortOrder)`, 단일 transaction all-or-nothing, stale 시 전체 409, 순서 중복 validation |
| custom 활성/비활성 | `PendingType.Manage` | `expectedRowVersion` 필수. system 행 대상이면 validation 오류 |

- 오류 의미: 미인증 401, permission 없음 403, validation(형식·중복·system 보호·비활성 유형 수동 등록) 400 ValidationProblem, 대상 code 없음 404, stale version 409. 기존 endpoint의 오류 응답 형식을 따른다.
- 수동 Pending create(`PendingStore` create validation)는 `PendingIssueTypes.All` 상수 검사 대신 같은 transaction/요청 범위의 catalog 조회로 `존재 && is_active && is_manual_enabled`를 재검증한다. 목록 filter의 유형 정규화도 catalog 기준으로 전환한다.
- 모든 Pending 응답 projection의 `IssueTypeLabel`을 catalog join으로 해석하고 `IssueTypeLabel` switch를 제거한다. `SelectedExcelExportService`의 Pending column 정의는 변경하지 않는다(응답 label을 그대로 사용).
- 자동 생성 method(`CreateOrReuseMaterialNonconformanceAsync`, `CreateManufacturingStopAsync`, 품질 검사 Pending 생성)는 수정하지 않고 회귀 테스트로 보호한다.
- audit: 모든 mutation이 성공 transaction 안에서 audit event 1건 이상을 남긴다(reorder는 변경 행 요약 1건 허용). 외부 provider 발송 경로는 만들지 않는다.

## 10. Frontend 계약

- `App.tsx`: view union에 `pending-types`, path `/pending-types`, 관리 메뉴에 `Pending 유형 관리` 항목(`양식 관리` 인접). 접근 gate는 기존 관리 화면 패턴을 따르되 최종 강제는 Backend다.
- 신규 `PendingTypeManagementPage.tsx` + `pendingTypes.ts`(type·API): `FormTemplateManagementPage.tsx`의 구조(권한 prop, `data-mobile-experience`, panel·card 패턴)를 재사용한다. 목록에 표시명, code(read-only), system/custom badge, 활성 상태, 수동 노출, 순서, 사용 건수를 표시하고 rename·설명·수동 노출 toggle·위/아래 reorder·custom 생성·활성/비활성을 제공한다. 잠긴 필드는 disabled 사유를 함께 보여 준다.
- `PendingPage.tsx`: 하드코딩 `typeOptions` 제거. 등록 modal은 수동 등록 option endpoint, filter는 filter option endpoint(비활성은 `(비활성)` 병기)를 사용한다. `ManufacturingStop` 조치 담당 부서 필수 분기는 semantic code 기준으로 유지한다. option 조회 실패 시 fail-closed(선택 비활성 + 오류·재시도), 하드코딩 fallback 금지.
- `pending.ts`: `PendingIssueType` union을 string code 기반으로 완화하고 semantic 분기용 상수(`ManufacturingStop` 등)만 유지한다.
- 상태 표시: loading skeleton, custom 0건 empty 문구, 403/409/validation 구분 표시, 공통 Action Feedback(TASK-UX-001 A1/A2 패턴)을 mutation별 action 근처에 적용한다.
- 접근성·모바일: reorder는 위/아래 button(드래그 전용 금지), 상태 badge에 텍스트 병기, 390px는 read-only card 요약과 “관리 변경은 PC에서” 안내, page-level horizontal overflow 0, 의미 기반 shape 체계(TASK-MOBILE-002)와 DESIGN-000 token 준수.

## 11. 기존 기능과의 연결

- `TASK-007A`/`TASK-007B`: 상태 전이·담당·코멘트·알림·집계 계약 불변. 007B 집계는 open 상태 기준이므로 catalog 변경 무영향을 회귀로 확인한다.
- 선택 Excel(`TASK-EXPORT-001/002`): column allowlist(`SelectedExportColumnRegistry`) 변경 없음. label 값 source만 Backend projection을 통해 catalog로 바뀐다.
- Teams/Mail/Activity: 변경 없음. 신규 발송 경로 없음.
- 감사: audit는 append-only 저장까지가 이번 범위다. 관리자 audit 조회 UI는 완료 원장 우선순위 3 계열 후속 Task로 분리한다.

## 12. Codex Review Finding Resolution (6/6)

| Finding ID | Severity | 본 계약의 해소 위치 | 상태 |
| --- | --- | --- | --- |
| `PENDING-TYPE-AUTOMATION-FK-FENCE` | P1 | 7절 1~2항, 6절 필수(system trigger·FK RESTRICT·`Other` 잠금), 15절 자동 생성 회귀 | RESOLVED_IN_CONTRACT |
| `PENDING-TYPE-CUSTOM-CODE-INPUT` | P2 | 3절 추가 확정, 9절 custom 생성 계약(서버 생성 `CUSTOM_*`, UI read-only) | RESOLVED_IN_CONTRACT |
| `PENDING-TYPE-REORDER-CAS` | P2 | 6절·9절 reorder 원자 계약(전체 집합, all-or-nothing, 전체 409) | RESOLVED_IN_CONTRACT |
| `PENDING-TYPE-LABEL-HISTORY` | P2 | 3절 lifecycle, 7절 4항(최신 label + immutable code + append-only audit, snapshot 제외) | RESOLVED_IN_CONTRACT |
| `PENDING-TYPE-FALLBACK-DRIFT` | P2 | 6절·10절 fail-closed(하드코딩 fallback 금지), 자동 생성 경로 분리 | RESOLVED_IN_CONTRACT |
| `PENDING-TYPE-EXPORT-SCOPE` | P3 | 6절 명시적 제외(catalog export 보류), 18절 후속 backlog | DEFERRED_BY_DESIGN |

Review의 `유지` 판정 7건은 3~11절에 그대로 보존했고, `제거` 판정 4건(code 직접 입력·custom semantic 필드·참조 0건 delete 예외·mobile mutation)은 계약에서 제외했다.

## 13. 구현 순서

1. Migration `0045`: catalog table·system 4행 seed·audit table·`PendingType.Manage` permission과 role grant seed·`ck_pending_issues_type` → FK 전환·불변 trigger. fresh 전체 적용과 기존 `0044 → 0045` upgrade(기존 Pending 데이터 보존) 검증.
2. Backend: PendingType store/contracts/endpoints(조회·create·update·reorder·activate/deactivate·CAS·audit), `QmsPermissions` 추가, 수동 create·filter validation의 catalog 전환, 전체 projection label의 catalog join 전환, 자동 생성 회귀 테스트.
3. Pending 소비 경로 통합 확인: 목록/상세/summary/선택 Excel label, `TASK-007B` 집계 회귀.
4. Frontend: `/pending-types` 관리 화면, 모바일 read-only 요약, `PendingPage` option 연동과 fail-closed, `App.tsx` route·메뉴.
5. 검증 마감: Backend 전체·Frontend lint/typecheck/unit/build·isolated Full-Stack 영향 시나리오·desktop/390px synthetic screenshot·5종 종료 산출물·local commit.

## 14. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL과 disposable Full-Stack E2E DB에서만 검증한다.
- Migration: additive `0045` 1건. 기존 migration 수정·번호 재사용 금지, rollback은 forward-fix 원칙.
- 외부 발송·실제 데이터·runtime 교체: 없음. 5174 Development runtime은 현재 branch 반영 원칙을 유지한다.
- Git 경계: 이 Task는 local experiment commit까지다. push·PR·merge·대표 repo·`main`(승인 0/3)·Persistent UAT 적용은 별도 승인 전 수행하지 않는다.
- 사용자 검수: `사용자 검수 대기 — 마지막 일괄 검수` 상태로 기록하며 완료로 표기하지 않는다.

## 15. 검증 계획

- Backend API 테스트(신규): 권한 3면(관리 permission 보유/미보유/미인증), system 보호(code·활성 변경, 삭제, `Other` 수동 노출 해제 차단), custom 생성(표시명 중복·길이, 서버 code 생성·불변), rename·toggle·activate/deactivate CAS 409, reorder 원자성(부분 stale 전체 409, 순서 중복 거부), option endpoint 필터링(활성·수동 노출/전체), 수동 create의 비활성·수동 미노출 유형 거부, audit event 기록.
- 자동 생성 회귀: 자재 IQC 부적합·품질 부적합/PUNCH·제조 중단 생성이 catalog 표시 설정 변경과 무관하게 성공, FK 무결성 확인.
- DB 레벨: trigger가 직접 SQL의 system 변조·delete·row_version 비증가 update를 차단.
- 영향 회귀: Pending 목록/상세/summary/filter label, 선택 Excel Pending label, `TASK-007B` 집계, Backend 전체 suite(`401/401` + 신규), Frontend lint/typecheck/unit/build(`109/109` + 신규).
- Migration: fresh 전체 적용과 기존 `0044 → 0045` upgrade(기존 Pending 행 보존·FK 충족).
- Full-Stack(isolated): 관리자 catalog 변경 → 업무 사용자 등록·목록 반영 시나리오 1건 이상, desktop·390px screenshot과 mobile overflow 0 확인.
- PR/CI: 해당 없음(local commit 범위). 승격 Task에서 수행.

## 16. 완료 기준과 중단 조건

완료 기준:

- 6절 필수 항목 전부 구현, system semantic 파괴 불가·과거 의미 보존·403/409/validation 차단을 테스트로 확인.
- 단일 catalog source 반영(등록·목록·상세·filter·Excel 일치), 하드코딩 label 제거, fail-closed 동작 확인.
- 15절 자동 검증 전체 통과, open P0/P1/P2 0건(P3는 backlog 연결).
- desktop/390px synthetic screenshot과 5종 종료 산출물 상태·위치 추적, 완료 원장·Roadmap 상태 갱신, local experiment commit.

중단 조건: 자동 생성 경로가 catalog 표시 상태에 의존하게 되는 설계 충돌, 기존 migration 수정이 불가피한 경우, Repository source 간 의미 있는 충돌 발견, fast-track 제외 경계(대표 repo·`main`·Persistent UAT·실제 provider·destructive operation)를 넘어야 하는 경우 — 구현을 중단하고 보고한다.

## 17. Codex 구현 지시문 (최종)

migration `0045`로 catalog·audit·permission을 additive 생성하고 system 불변 trigger와 `issue_type` FK(RESTRICT)로 `ck_pending_issues_type`을 대체한다. Backend에 `PendingType.Manage` 강제, 서버 생성 `CUSTOM_*` code, row_version CAS와 reorder 원자 계약, append-only audit, 단일 catalog label projection과 수동 create의 catalog 재검증을 구현한다. Frontend에 `/pending-types` desktop 관리 화면과 mobile read-only 요약을 추가하고 `PendingPage`의 유형 option을 catalog endpoint로 전환하되 fail-closed를 지킨다. 자동 생성 상수·SQL literal, `TASK-007A` 계약, 기존 migration은 수정하지 않는다. 검증은 15절, 경계는 14절, 순서는 13절을 따른다. 2차 기획 blocking decision이 0이므로 standing instruction에 따라 구현·검증·screenshot·implementation report·local commit까지 연속 진행한다.

## 18. 보류·후속 항목 (이번 범위 아님)

- catalog 자체 선택 Excel 내보내기(P3 backlog)
- 관리자 audit 조회 UI(완료 원장 우선순위 3 계열 후속)
- custom 유형의 semantic 연결·통계 roll-up(실측 요구 확인 시 별도 NEW_FEATURE)
- 부서장 위임·role/permission 편집 UI, 관리자 모바일 고도화(ADMIN 후속)
- 승격·UAT·게시(별도 승인 3회 경계 유지)

## 19. 최종 상태

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- reviewFindingsResolved: `6/6`
- implementationApprovedScope: experiment local commit 한정(change-001), 대표 repo·`main`·Persistent UAT·provider·push·PR·merge 미승인
- openBlockingDecisionCount: 0
