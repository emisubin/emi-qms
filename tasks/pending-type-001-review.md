# TASK-PENDING-TYPE-001 — Pending 유형 관리 1차 기획 Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/pending-type-001-planning.md`
- reviewStatus: `RESOLVED_FOR_EXPERIMENT_SECOND_PLANNING`
- canonicalMainApproval: false

## 1. 결론

Fable의 권장안 B, 즉 고정 system 유형 4개와 수동 전용 custom 유형을 한 catalog로 관리하는 방향을 유지한다. 표시 설정만 관리하는 안보다 실제 운영의 세부 유형 요구를 수용하면서도 custom을 자동 workflow semantic에 연결하지 않아 `TASK-007A`의 부적합·PUNCH·제조 중단 계약을 보존한다.

다만 1차 기획의 custom code 입력, reorder CAS와 label 이력 경계는 구현 계약으로 쓰기엔 덜 구체적이다. 2차 기획은 관리자가 업무 code를 직접 입력하지 않게 하고, 전체 순서 변경의 원자 CAS, system row DB 불변, 현재 label과 과거 근거의 구분을 명시해야 한다.

## 2. 기능 판단

### 유지

- system 4개와 수동 전용 custom 유형을 한 catalog에 두고 `pending_issues.issue_type`을 immutable code FK로 연결한다.
- `Nonconformance`·`Punch`·`ManufacturingStop` 자동 생성은 catalog 관리와 분리된 기존 exact constant 경로를 유지한다.
- system code·활성 상태와 `Other` 수동 fallback을 잠그고 hard delete를 제공하지 않는다.
- `PendingType.Manage`를 전사 catalog 전용 permission으로 만들고 현재는 System Administrator role에만 seed한다. System Administrator의 업무 Pending mutation 금지는 그대로다.
- 목록·상세·filter·수동 등록 option·선택 Excel의 사용자-facing label을 Backend catalog projection 하나에서 파생한다.
- Desktop에서만 mutation을 제공하고 390px는 read-only catalog 요약으로 단순화한다.
- additive `0045`, fresh와 `0044 → 0045`, isolated Full-Stack, 역할·동시성·자동 생성 회귀를 필수로 둔다.

### 추가

- custom code는 관리자 입력값이 아니라 서버가 `CUSTOM_` prefix와 충돌 불가능한 식별자로 생성한다. 사용자는 표시명·설명만 입력하고 code는 immutable read-only로 본다.
- catalog table의 DB constraint·trigger가 system 4행의 code/is_system/is_active와 `Other`의 manual exposure를 보호하고, 모든 행의 hard delete를 차단한다. FK는 `ON UPDATE RESTRICT ON DELETE RESTRICT`다.
- system row가 없거나 inactive인 schema drift에서는 자동 생성이 silent fallback하지 않고 migration/schema readiness 실패로 드러나야 한다. 정상 운영에서는 system row가 DB trigger 때문에 변경될 수 없어야 한다.
- reorder 요청은 화면에 보이는 전체 catalog의 `(code, expectedRowVersion, newSortOrder)` 집합을 보내고 한 transaction에서 모두 lock·검증·갱신한다. 일부 row만 적용하지 않고 하나라도 stale이면 전체 409다.
- label rename은 기존 Pending code를 바꾸지 않으며 현재 화면·현재 Excel에는 최신 label을 사용한다. 변경 당시의 이전/새 label·flags·순서는 append-only audit에 남겨 과거 근거를 보존한다. Pending 생성 시 label snapshot을 새로 저장하는 범위는 추가하지 않는다.
- catalog option 조회 실패 시 Frontend가 옛 하드코딩 option으로 fallback하지 않는다. 등록은 fail-closed error를 보여 주고 자동 생성 경로는 기존 constant와 FK 불변으로 별도 보존한다.
- 수동 등록은 요청 시점에 catalog row를 다시 확인해 `is_active && is_manual_enabled`를 강제한다. filter는 비활성 유형도 과거 조회를 위해 제공한다.
- `PendingIssueTypes.All`은 system semantic 상수 집합으로만 남기고 custom 허용 여부 검사에 재사용하지 않는다.

### 보류

- catalog 자체 선택 Excel 내보내기는 사용자 핵심 문제와 무관하고 이미 관리 화면에 row 수가 작으므로 이번 v1에서 보류한다.
- 부서장 위임·role/permission 편집 UI는 catalog가 전사 공통이라 보류한다. 전용 permission은 향후 role grant를 가능하게 하지만 이번 UI는 만들지 않는다.
- custom 유형을 system semantic으로 묶는 기능, 통계 roll-up과 자동 재검사 연결은 실측 요구가 생기면 별도 NEW_FEATURE로 다룬다.
- audit 전용 관리자 조회 화면은 기존 관리자 audit 후속과 함께 분리하고 이번 화면에는 최근 변경 요약만 필요한 경우에 한해 제공한다.

### 제거

- 관리자가 custom code를 직접 입력하는 UX와 code rename 기능을 제거한다. 사용자-facing 의미는 label이고 code는 시스템 식별자다.
- custom 유형의 별도 semantic 필드와 system semantic remap 기능을 제거한다. custom은 항상 수동 전용이다.
- 참조 건수 0일 때 hard delete를 허용하는 예외를 제거한다.
- mobile mutation control과 desktop table 축소 복제를 제거한다.

## 3. Finding과 Resolution

| ID | Severity | Finding | 2차 기획 Resolution |
| --- | --- | --- | --- |
| `PENDING-TYPE-AUTOMATION-FK-FENCE` | P1 | check→FK 전환 뒤 system catalog row가 수정·삭제·비활성화되면 IQC·품질·제조 자동 생성이 실패할 수 있다. | system 4행 DB trigger/constraint, FK restrict, `Other` manual fallback 잠금, 자동 생성 exact constant 회귀를 최종 계약에 포함한다. |
| `PENDING-TYPE-CUSTOM-CODE-INPUT` | P2 | 관리자가 stable code를 입력하면 중복·명명 drift·업무 원문 노출과 향후 rename 요구가 생긴다. | 서버 생성 immutable `CUSTOM_*` code, UI read-only, 사용자 입력은 label·설명으로 제한한다. |
| `PENDING-TYPE-REORDER-CAS` | P2 | row별 CAS라는 표현만으로는 reorder 일부 성공·중복 순서·동시 rename과의 경쟁을 막는 request contract가 불명확하다. | 전체 집합 lock·expected row versions·atomic all-or-nothing update·stale 전체 409를 명시한다. |
| `PENDING-TYPE-LABEL-HISTORY` | P2 | “실시간 label + audit”이 기존 Pending의 code 불변과 과거 표시 근거를 어떻게 함께 보존하는지 모호하다. | 현재 UI/Excel은 최신 label, immutable code는 issue에 유지, before/after label·flags·order는 append-only audit에 남기며 issue label snapshot은 제외한다. |
| `PENDING-TYPE-FALLBACK-DRIFT` | P2 | catalog 조회 실패 때 Frontend가 하드코딩 option으로 fallback하면 단일 source와 server validation이 다시 갈라진다. | 수동 등록 option은 fail-closed, filter도 catalog 응답 사용, 자동 생성만 기존 constant 경로로 분리한다. |
| `PENDING-TYPE-EXPORT-SCOPE` | P3 | catalog 자체 선택 Excel은 핵심 관리 요구보다 범위를 늘린다. | Pending 업무 선택 Excel label 연동만 유지하고 catalog export는 보류한다. |

## 4. 권장 개발 순서

1. `0045` catalog·system seed·permission·audit·FK·immutability trigger와 fresh/upgrade migration test.
2. `PendingTypeStore` 조회·create/update/reorder·CAS·audit와 수동 등록 validation, system 자동 생성 회귀.
3. Pending 목록/detail/filter/selected Excel을 catalog label projection으로 통합.
4. Desktop 관리 화면, mobile read-only 요약과 기존 Pending 등록 option 연동.
5. Backend 전체·Frontend 전체·isolated Full-Stack·desktop/390px screenshot·종료 산출물.

## 5. 2차 기획 판정

- 권장안 자동 채택: `GO`
- openBlockingDecisionCount 기대값: `0`
- 대표 repo·`main`·Persistent UAT·provider·push·PR·merge: 제외
