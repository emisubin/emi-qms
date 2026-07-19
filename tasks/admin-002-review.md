# TASK-ADMIN-002 — Codex 기획 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/admin-002-planning.md`
- reviewScope: 사용자 문제·제품 방향·Repository 경계·권한·version lifecycle·운영 부담·UX
- reviewedAt: `2026-07-19`
- overallDecision: `CONDITIONAL_KEEP_WITH_RESOLUTIONS`

## 1. 총평

기존 IQC·panel quality table/FK를 보존하고 관리 API에서 family adapter로 통합하는 안은 역사 데이터 안전성과 구현 비용의 균형이 좋다. 제조의 hard-coded 단계만 versioned source로 옮기고 실행 시 snapshot을 계속 만드는 방향도 사용자 문제를 정확히 해결한다. System Administrator 전 범위와 명시적으로 지정된 부서 양식 관리자 자기 부서 범위라는 권한 모델도 generic role 추정보다 안전하다.

다만 기존 품질 version schema는 `is_active=false`이면 `activated_at_utc=null`이어야 해서 과거 활성 version을 Archived로 보존할 수 없다. 상태 column만 추가해서는 해결되지 않으며 constraint 교체·DB immutable trigger·activation transaction을 함께 설계해야 한다. 제조 실행에는 어느 template version에서 snapshot을 만들었는지 FK가 필요하고, 양식 종류 자체를 새로 만드는 기능과 기존 종류의 내용 관리 범위를 구분해야 한다.

## 2. 기능 판단

### 유지

- IQC·panel quality 기존 table/FK를 유지하는 family adapter 방식을 채택한다.
- Draft → Active → Archived lifecycle과 active/used version 불변을 유지한다.
- System Administrator 전 범위, 지정 부서 양식 관리자 자기 부서 범위, 일반 사용자 403을 유지한다.
- 제조 active template v1을 현행 네 단계 문구로 seed하고 새 실행 시 step snapshot을 생성한다.
- desktop 3영역, mobile `양식 → 버전 → 항목` drill-in, action feedback·keyboard 순서 이동을 유지한다.
- template version 선택 export와 전체선택을 기존 전역 UX 계약에 맞춰 포함한다.

### 추가

- IQC/panel quality version에 `lifecycle_status`, `row_version`, creator/updater, archived/activated metadata를 additive하게 보강하고 기존 activation check를 Draft/Active/Archived 의미에 맞게 교체한다.
- IQC/panel quality/manufacturing version과 item에 DB trigger를 추가해 Active/Archived row와 item의 update/delete를 차단한다. API 검사만으로 불변조건을 대신하지 않는다.
- 제조 execution에 nullable `template_version_id` FK를 추가한다. 기존 실행은 null legacy snapshot으로 유지하고 새 실행은 active version FK와 snapshot을 같은 transaction에서 기록한다.
- 양식 관리자 binding은 `(user_id, department_id, domain)` 단위로 복수 지정 가능하고 soft revoke하며, active 중복만 unique index로 차단한다. department와 user.department_id 일치는 지정 시와 mutation 시 모두 확인한다.
- 관리 가능한 종류는 이번 버전에서 `IQC/LQC/OQC/CustomerInspection/FAT/Manufacturing`의 기존 catalog로 고정한다. 새 양식 종류 생성은 form-builder 범위이므로 제공하지 않되, 각 종류의 version·항목은 code change 없이 관리한다.
- 항목 전체 저장은 한 transaction의 full replacement로 처리하되 Draft에서만 허용하고 item code/order validation 후 audit를 남긴다.
- active version 부재는 새 report/execution 시작을 409로 차단하고 기존 업무·snapshot은 계속 조회 가능하게 한다.

### 보류

- Word/Excel import, arbitrary response type, 조건식·전자서명, 새 양식 종류 생성은 후속 기능으로 보류한다.
- 실제 운영 양식 내용 대량 입력과 Persistent UAT 적용은 외부 회신/별도 승인 후속 Task로 보류한다.

### 제거

- 기존 inactive row를 `activated_at_utc`만 보고 Archived로 backfill한다는 표현을 제거한다. 기존 schema상 inactive row는 활성 이력을 보존할 수 없으므로 현재 데이터 사실에 맞춘 명시적 backfill을 사용한다.
- 부서장이 다른 부서 template의 상세 metadata까지 볼 수 있다는 해석을 제거한다. 목록·상세 모두 자기 소유 범위로 제한한다.

## 3. Finding과 Resolution

| ID | Severity | Finding | 최종 2차 기획 Resolution |
| --- | --- | --- | --- |
| `ADMIN-002-ACTIVATION-CONSTRAINT` | P1 | 기존 quality activation check는 비활성 version의 activation timestamp 보존을 금지해 Archived lifecycle과 충돌한다. | `lifecycle_status` 기반 constraint로 교체하고 archive metadata를 보존한다. |
| `ADMIN-002-DB-IMMUTABILITY` | P1 | 서버 검사만으로는 active/used version·item의 직접 DB 변경을 막지 못한다. | 세 family 모두 Active/Archived version/item update/delete guard trigger를 추가한다. |
| `ADMIN-002-MANUFACTURING-PROVENANCE` | P2 | 제조 snapshot에 source template version FK가 없어 어느 양식에서 생성됐는지 추적할 수 없다. | execution에 nullable legacy-compatible FK를 추가하고 신규 실행에는 필수로 기록한다. |
| `ADMIN-002-BINDING-SCOPE` | P2 | 단순 user-department binding은 domain·department membership drift를 충분히 막지 못한다. | domain을 포함하고 지정·사용 시 user의 활성 department 일치와 soft revoke를 검증한다. |
| `ADMIN-002-CATALOG-BOUNDARY` | P2 | “코드 없이 양식 관리”가 새 양식 종류 생성까지로 오인될 수 있다. | v1은 6개 기존 종류의 version/item 관리로 명시하고 새 종류 생성은 제외한다. |
| `ADMIN-002-MIGRATION-ORDER` | P2 | 영업 KPI와 같은 next migration 번호 제안이 충돌한다. | ADMIN-002 migration을 `0044`로 고정한다. |
| `ADMIN-002-NEW-EXECUTION-FENCE` | P2 | active template 부재 시 제조만 언급되고 IQC/panel quality 신규 report 생성 차단이 명확하지 않다. | 세 family 모두 active version을 row lock으로 읽고 없으면 409, 기존 업무 불변으로 고정한다. |
| `ADMIN-002-EXPORT-CONTRACT` | P3 | 전역 “모든 페이지 선택 내보내기” 계약을 선택 항목으로 남겼다. | version 목록 checkbox·전체선택·선택 Excel 내보내기를 포함한다. |

## 4. 권장 개발 순서

1. `0044` lifecycle·binding·audit·manufacturing template/provenance·DB trigger migration.
2. 공통 access evaluator와 family adapter API, lifecycle·concurrency·권한 tests.
3. 기존 IQC/panel report 생성 및 Manufacturing start를 active version source로 연결하고 snapshot 회귀 테스트.
4. desktop/mobile 양식 관리 UI, 관리자 지정, 선택 export.
5. isolated full-stack·screenshot·종료 문서화.

## 5. 최종 구현 계약에 필요한 항목

- `openBlockingDecisionCount: 0`
- 위 Finding 8건의 resolution과 구체적 validation/test 위치 통합
- 기존 report/attempt/execution row rewrite 0, 실제 운영 양식 내용 변경 0
- representative repo·`main`·Persistent UAT·provider·push·PR·merge 제외
