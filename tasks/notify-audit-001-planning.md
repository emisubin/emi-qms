All baseline reading is complete. The interview gate is confirmed (`COMPLETED_CONFIRMED`, `userConfirmed: true`, 0 blocking decisions), the roadmap/ledger name this as the priority-1 remaining task, and I've verified the audit table (`0041`), the preference store, the admin permission policy, the selected-Excel registry pattern, and the pagination precedent. Below is the single first planning draft for the runner to record.

---

# TASK-NOTIFY-AUDIT-001 — 관리자 알림 설정 감사 조회 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/notify-audit-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: `TASK-NOTIFY-005`가 저장한 알림 설정 변경 감사 원장(`user_notification_preference_audit_events`)을 관리자가 화면에서 조회할 수 없어, 설정 문의·오작동 의심·관리자 대리 변경 확인 때 DB를 직접 봐야 한다.
- 대상 사용자·역할: System Administrator 전용. 기존 `QmsPolicies.AdminUsersRead`를 재사용하고 그 외 역할·비인증 요청은 서버에서 차단한다.
- 정상 흐름: 관리자 메뉴 → 감사 조회 화면 → 기본 기간 요약 → 필터/검색 → 상세 행 확인 → checkbox 전체/부분 선택 → 선택 Excel 내보내기.
- 예외·복구 흐름: empty 상태는 명확한 안내와 필터 초기화 action 제공, 비활성/표시정보 없는 사용자도 원장 표시가 깨지지 않음, query/export 오류는 action 근처 한글 안내와 필터·선택 상태 보존.
- 확정한 정책과 명시적 제외: 감사 조회는 read-only이며 preference·delivery 상태를 변경하지 않음. audit 수정·삭제, taxonomy 편집, 조직 기본값, 일반 사용자 audit 열람, provider delivery 감사 통합, 실제 provider·Persistent UAT·push·PR·merge는 제외. 기존 migration `0041`은 수정하지 않음.
- planning으로 넘긴 비차단 미결정 사항: 기본 기간·pagination, 요약 카드 구성, 필터·검색 범위, 모바일 정보 밀도, Excel column, 개인정보 최소화 범위 6건. 모두 experiment fast-track standing instruction에 따라 Fable 권장안 자동 채택 대상이다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

### 배치·Task identity 경계

사용자는 2026-07-20에 남은 작업 1번(이 Task)과 3번(terminal Failed 수동 재처리, `TASK-NOTIFY-REPROCESS-001`)을 한 개발 배치로 지시했다. 이 기획은 `TASK-NOTIFY-AUDIT-001`만 다루며 Task identity·planning·Finding·검증 산출물은 서로 분리한다. Migration 번호 등 공유 자원은 구현 시점의 실제 다음 번호를 사용한다(현재 기준선 `0047`).

## 1. 한 줄 목표

System Administrator가 사용자별 알림 설정 변경 이력을 기간·행동·알림 종류·사용자 기준으로 검색·요약해 한글로 이해하고, 선택한 행만 Excel로 보존할 수 있다.

## 2. 배경과 해결할 업무 문제

- `TASK-NOTIFY-005`는 `Save`/`Reset`/`AdminSave`/`AdminReset` 중 실제 값이 바뀐 항목만 fixed-field append-only audit로 저장한다(`database/migrations/0041_user_notification_preferences.sql`, `NotificationPreferenceStore.InsertAuditAsync`).
- 조회 API·화면이 없어 관리자는 "왜 이 사용자에게 Teams 알림이 안 갔나", "누가 언제 대리 변경했나"를 확인하려면 DB 직접 조회가 필요하다.
- 이 기능이 없으면 운영 문의 대응이 개발자 의존이 되고, 관리자 대리 변경 책임 추적이 화면에서 불가능하다.
- 저장 원장·권한·선택 Excel 패턴이 모두 구현돼 있어 read-only 조회 계층만 추가하면 된다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| System Administrator | 감사 목록·요약 조회, 필터·검색, 선택 Excel | 전체 사용자의 알림 설정 변경 원장 | 없음 (read-only) |
| 일반 역할 | 없음 | 차단 (`403`) | 없음 |
| 비인증 | 없음 | 차단 (`401`) | 없음 |

- 본인 설정 화면(`/notification-settings`)은 이 Task에서 변경하지 않으며, 일반 사용자는 자신의 audit도 열람하지 않는다(interview 확정).
- 서버 권한은 목록·요약·Excel 모두 기존 `QmsPolicies.AdminUsersRead`로 강제하고 UI 숨김에 의존하지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 사용자 문의 대응

1. 관리자가 "D-1 알림이 안 온다"는 문의를 받고 감사 조회 화면을 연다.
2. 시스템이 기본 기간(최근 30일)의 요약 카드와 최신순 목록을 보여준다.
3. 관리자가 사용자 검색어를 입력하면 해당 사용자가 대상 또는 변경 주체인 행만 남는다.
4. "예정일 임박 D-1 · Teams 개인 알림 · 켬 → 끔 · 사용자 직접 변경 · 시각"을 확인하고, 사용자가 직접 끈 것임을 안내한다.

### 시나리오 B — 관리자 대리 변경 책임 확인

1. 관리자가 기간을 지정하고 행동 필터를 "관리자 대리 변경"으로 선택한다.
2. 시스템이 같은 필터 기준의 요약 수치와 목록을 함께 갱신한다.
3. 관리자가 근거가 필요한 행을 checkbox로 선택하고 선택 Excel을 내려받아 보존한다.

### 시나리오 C — 조건에 맞는 변경이 없음

1. 관리자가 좁은 필터 조합을 적용한다.
2. 시스템이 "조건에 맞는 변경 이력이 없습니다"와 필터 초기화 버튼을 표시한다.
3. 초기화하면 기본 기간 상태로 돌아간다.

## 5. 기능 요구사항

### 필수

- [ ] System Administrator 전용 감사 목록 API: 기간·행동·알림 종류·사용자 검색 필터, 고정 최신순 정렬, page 기반 pagination, 같은 필터의 요약 수치를 한 응답으로 반환
- [ ] actor/target의 현재 표시명·부서·활성 상태 projection (기존 user projection 규칙, credential·provider payload·raw internal error 미포함)
- [ ] action·알림 종류·채널·변경 전후의 한글 label registry (server 기준, list·summary·Excel 공용)
- [ ] 신규 관리자 화면 `/admin/system/notification-preference-audit`: 요약 카드, compact filter bar, 감사 table(desktop)/카드(mobile), loading·empty·error 상태
- [ ] 기존 checkbox 전체선택 + 선택 Excel 단일 action 패턴 재사용 (신규 screen key 등록)
- [ ] server allowlist 밖 필터·정렬·column·ID fail-closed 처리
- [ ] 비활성·표시정보 결손 사용자의 안전한 표시 (원장 행 자체는 항상 표시)

### 선택

- [ ] 관리자 사용자 알림 설정 화면(`/admin/users/{id}/notification-settings`)에서 해당 사용자를 대상 필터로 미리 채운 감사 조회 진입 link
- [ ] 요약 카드 클릭 시 해당 행동 필터 적용 (shortcut)

### 명시적 제외

- [ ] audit 행 수정·삭제·보정, preference taxonomy 편집, 조직 기본값
- [ ] 일반 사용자의 본인 audit 열람 화면
- [ ] provider delivery attempt·메일/Teams 본문 감사와의 통합
- [ ] preference 저장·reset·suppression gate 로직 변경
- [ ] 실제 사용자·운영 DB·Persistent UAT·actual provider·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 알림 설정 변경 이력 (desktop) | 관리자 시스템 메뉴 (기존 `/admin/system/*` 그룹) | 요약 카드 4개, filter bar, table: 시각·대상·변경 주체·행동·알림 종류·채널·변경 결과·버전 | 필터·검색·페이지 이동, 행 선택, 선택 Excel | `useActionFeedback` 기반 조회·export 성공/실패 한글 안내, 필터·선택 상태 보존 |
| 알림 설정 변경 이력 (390px) | 동일 route 적응형 | 변경 주체→대상·알림 종류·변경 결과·시각 중심 카드, 요약 카드 2×2, 필터는 접이식 영역 | 카드 선택 checkbox, 선택 Excel tray | 동일 |

확인할 UX 항목:

- 기본 진입만으로 최근 30일 요약과 최신 변경이 보이는가?
- "켬 → 끔", "관리자 대리 변경" 등 한글 label만으로 변경 내용을 이해할 수 있는가?
- 요약 수치와 목록·Excel 행이 같은 필터 기준으로 일치하는가?
- 권한 부족·오류·empty 상태가 명확한가?
- 390px에서 horizontal overflow 0이고 핵심 정보·선택·export가 가능한가?
- keyboard focus, label, `aria-live`가 기존 관리자 화면 수준으로 제공되는가?
- DESIGN-000 semantic token과 기존 관리자 알림 화면의 thin divider·blue accent를 재사용하는가?

## 7. 업무 규칙과 불변조건

- 감사 원장은 append-only·원문 불변이다. 이 기능은 어떤 mutation도 수행하지 않는다.
- 조회·요약·Excel은 항상 같은 server filter·label registry를 통과해 수치와 행이 drift하지 않는다.
- 화면 조회 행위를 같은 audit table에 다시 기록하지 않는다(self-noise 금지). 필요 시 기존 HTTP security telemetry만 사용한다.
- Backend가 권한·필터 allowlist·column allowlist의 authoritative layer다.
- 사용자 preference 저장·reset·no-op·version·suppression gate 계약(`NotificationPreferenceStore`)은 변경하지 않는다.
- audit FK(`on delete restrict`)와 사용자 lifecycle purge guard를 보존하며 삭제 정책을 바꾸지 않는다.
- API 응답에 credential, provider payload, raw internal error를 추가하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `user_notification_preference_audit_events` | actor·target·action·delivery type·channel·old/new·resulting version·occurred_at 고정 필드 원장 | 기존 (`0041`) | append-only, 수정·삭제 금지, authoritative source |
| `qms_users` / `departments` | actor·target의 현재 표시명·부서·활성 상태 join | 기존 | 표시용 read-only join |
| 감사 조회 query index | 기간 정렬 목록용 `occurred_at_utc desc` 계열 index | 신규 (additive migration, 권장) | 기존 index·table 불변, forward-fix 원칙 |
| 선택 Excel 감사 | 기존 selected export audit 계약 (`data_export_events` 계열) | 기존 재사용 | 신규 screen key로 기록 |

상태 전이는 없다. 조회 화면의 상태는 `loading → loaded(list+summary) | empty | error`뿐이다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 권한(`AdminUsersRead`), 필터·정렬·pagination allowlist, 한글 label registry, Excel column allowlist·필수 잠금.
- 필요한 조회와 mutation:
  - `GET /api/admin/notification-preference-audit` — 유일한 신규 조회. 응답: `items[] + page + pageSize + totalCount + summary`. mutation 없음.
  - 선택 Excel은 기존 generic selected export 경로에 신규 screen `admin-notification-preference-audit`를 등록해 재사용한다(`SelectedExportScreens`, `SelectedExcelExportService`, `SelectedExportColumnRegistry`, `RequiresAdminUsersRead`).
- 권한·validation: 비인증 `401`, 비관리자 `403`. 필터는 allowlist 기반 fail-closed — action 4종, 지원 pair 3종, 기간 `from<=to`·최대 366일, page≥1, pageSize allowlist(예: 20/50/100). 위반은 `422` 한글 안내.
- item projection: audit 고정 필드 + actor/target의 `displayName·departmentName·isActive` + 한글 label. 정렬은 `occurred_at_utc desc, id desc` 고정이며 클라이언트 정렬 키를 받지 않는다.
- summary: 같은 WHERE 절에서 전체 변경·사용자 직접(Save/Reset)·관리자 대리(AdminSave/AdminReset)·알림 끔 전환(`new_value=false`) count를 단일 요청으로 계산해 목록과 drift를 차단한다.
- transaction·동시성·idempotency: read-only 단건 조회라 별도 lock 불필요. Excel은 기존 `ExcelExportConcurrencyGate`·전부-or-전무 검증 패턴을 따른다.
- audit trail: 조회 자체는 audit 기록 없음(확정). 선택 Excel은 기존 export audit 계약으로 기록.
- 외부 provider 영향: 없음. provider call 0 유지.
- Migration: 기존 index는 `(target_user_id, occurred_at)`·`(actor_user_id, occurred_at)`뿐이라 무필터 기간 목록은 정렬 index가 없다. additive 신규 migration(구현 시점 다음 번호, 기준선 `0048`)으로 `occurred_at_utc desc, id` index 추가를 권장한다. `0041` 수정 금지. 대안(§12) 참조 — 최종 확정은 Codex review·2차 기획.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 추가로 확정하지 않는다.

## 10. Frontend 고려사항

- route/component: `/admin/system/notification-preference-audit`를 기존 `/admin/system/*` 관리자 그룹(`frontend/src/App.tsx`)에 추가. 신규 page component는 기존 관리자 알림 화면 패턴을 따른다.
- loading/empty/error/success: skeleton 또는 로딩 안내, empty + 필터 초기화, 오류 시 action 근처 한글 안내와 필터·선택 보존.
- 공통 Action Feedback: `useActionFeedback` 재사용(조회 실패·export 성공/실패), `aria-live` 포함.
- 선택·Excel: `SelectionCheckbox`·`SelectedExportTray`(`frontend/src/SelectedExcelExport.tsx`)와 `selectedExportPageRegistry`에 `{ route, screen: 'admin-notification-preference-audit', selectionKey: 'auditEventId', area: 'admin' }` 추가. column picker는 server effective registry를 그대로 사용.
- 접근성: filter control label, table header scope, checkbox `aria-label`, focus 이동.
- 390px/mobile/narrow pane: PC table 축소가 아니라 변경 주체·대상·알림·변경 결과·시각 중심 카드 재배치, page-level horizontal overflow 0, 요약 카드 2×2, 필터 접이식.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 알림 preference 저장 기능(`TASK-NOTIFY-005`)의 원장을 읽기만 한다. 알림 발송·suppression 동작 불변.
- 권한/관리자: `QmsPolicies.AdminUsersRead` 재사용. 관리자 사용자 화면과 표시명 projection 규칙 공유.
- Excel/PDF/첨부: 기존 선택 Excel 공통 계약(TASK-EXPORT-001 Change 002/003)에 screen 1개 추가. PDF·첨부 없음.
- Teams/Mail: 영향 없음. actual provider call 0.
- 삭제·복구/감사: audit FK restrict와 사용자 삭제 보류 guard를 그대로 둔다. 비활성·삭제 보류 사용자는 badge로 표시하되 원장 행은 항상 표시한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 단일 list+summary GET, page 기반 pagination, additive 정렬 index migration 1개, 기존 selected export screen 등록 | 기존 `ProjectListResponse`·export 패턴과 일치, 수치·행 drift 원천 차단, 대량 원장에서도 안정 | migration 1개 추가로 fresh/기존 DB 검증 비용 |
| B | summary 별도 endpoint + cursor(keyset) pagination, migration 없음 | 요청 분리로 응답 단순, index 재사용 시 migration 0 | list/summary 이중 요청의 filter drift 위험, cursor는 repo에 전례 없음, 무필터 기간 정렬은 여전히 index 부재 |
| C | 관리자 사용자 상세 안에 사용자별 이력 tab만 추가 | 최소 구현 | 전체 기간·행동 기준 조회와 요약·Excel 불가, interview 성공 기준 미충족 |

권장안은 A다. 비차단 세부 선택 6건은 fast-track standing instruction에 따라 아래 권장값을 자동 채택하고 §16에 근거를 남긴다.

1. 기본 기간·pagination: 최근 30일 기본, preset 7/30/90일+사용자 지정(최대 366일), page/pageSize/totalCount 방식, 기본 pageSize 50 (allowlist 20/50/100).
2. 요약 카드: 전체 변경·사용자 직접·관리자 대리·알림 끔 전환 4개, 목록과 같은 필터로 단일 응답 계산.
3. 필터·검색: 기간 + 행동 4종 + 알림 종류 3종(pair key) + 사용자 검색 1개(대상 또는 변경 주체 표시명 부분 일치). v1에서 대상/주체 분리 picker는 제외.
4. 모바일 정보 밀도: 핵심 변경 카드(주체→대상, 알림 종류, 켬/끔 전환, 시각, 대리 badge), 접이식 필터, 선택·Excel tray 유지.
5. Excel column: 필수 잠금 — 시각·대상 사용자·변경 주체·행동·알림 종류·변경 결과. optional — 채널, 변경 전, 변경 후, 결과 버전, 대상 부서. server registry 기준.
6. 개인정보 최소화: 현재 시점 표시명·부서명·활성 상태 join만 표시. 이메일/UPN·계정 식별자·snapshot 신설 없음. 표시정보 결손 시 "알 수 없는 사용자"류 안전 표시.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated DB·runtime만 사용, Persistent UAT migration·handover 제외.
- migration 필요 여부: additive index migration 1개 권장(§9). `0041` 등 기존 migration 수정 금지, forward-fix 원칙.
- 외부 발송/실제 데이터 영향: 없음. actual provider call 0, 실제 사용자 데이터 미사용(synthetic fixture).
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: push·PR·`main` merge(승인 `0/3`)·Persistent UAT 적용. fast-track은 local experiment commit까지만 승인됨.

## 14. 검증 계획

- 최소 테스트 (Backend integration): 관리자 200 / 일반 역할 403 / 비인증 401, 기간·행동·알림 종류·사용자 검색 필터 정확성, pagination 경계(page 밖 empty), summary와 목록 count 일치, allowlist 밖 필터·pageSize `422` fail-closed, 비활성 사용자 표시 안전성, 신규 index migration의 fresh+기존 DB 적용.
- Frontend unit: 목록·요약·필터·empty·error 렌더, 선택·export tray 연동, label 표시.
- Excel: 신규 screen의 column registry·필수 잠금·선택 ID 전부-or-전무 검증, export audit 기록.
- 영향 영역 회귀: 기존 preference API·delivery 테스트(변경 없음 확인), selected export 회귀, Backend/Frontend 전체 suite(기준선 406/406·110/110 계보).
- isolated Full-Stack E2E: 관리자가 save/reset·대리 변경으로 audit 행을 만든 뒤 목록·요약·필터·선택 Excel까지 1개 흐름, 390px overflow 0 확인.
- PR/CI: fast-track 범위 밖(local commit only).
- 사용자 검수: privacy-safe desktop/390px screenshot과 checklist 작성 후 `사용자 검수 대기 — 마지막 일괄 검수`.

## 15. 완료 기준

- 기능/권한/데이터: 관리자만 같은 필터 기준의 summary·list·선택 Excel을 사용할 수 있고, no-op이 원장에 나타나지 않으며 3종 설정의 변경 전후·주체·대상·시각이 일치한다.
- UX: desktop·390px에서 핵심 정보·선택 상태·export feedback이 명확하고 horizontal overflow 0.
- 자동 테스트: §14의 Backend/Frontend/E2E 통과, 기존 회귀 무손상.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- PR 상태: 없음(local experiment commit only).

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 기본 기간·pagination 방식 | 30일 기본+page 방식(권장) / 90일 / cursor | 자동 채택 — standing instruction, §12 권장값 |
| 2 | 요약 카드 구성 | 4카드 단일 응답(권장) / 별도 endpoint | 자동 채택 — 동일 |
| 3 | 필터·검색 범위 | 통합 사용자 검색 1개(권장) / 대상·주체 분리 picker | 자동 채택 — 동일 |
| 4 | 모바일 정보 밀도 | 핵심 변경 카드+접이식 필터(권장) / table 축소 | 자동 채택 — 동일 |
| 5 | Excel column 구성 | 필수 6종 잠금+optional 5종(권장) | 자동 채택 — 동일 |
| 6 | 개인정보 표시 범위 | 현재 표시명·부서·활성 badge(권장) / 표시명만 | 자동 채택 — 동일 |
| 7 | 정렬 index migration 추가 여부 | additive 신규 index(권장) / migration 없이 기존 index만 | 자동 채택 대상 — Codex review·2차 기획에서 실측 근거로 최종 확정 |

사용자에게 새로 물을 blocking decision은 없다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Notifications/` 신규 audit query store·contracts·endpoint, `DataExports/SelectedExcelExportService.cs`·`SelectedExportColumnRegistry.cs` screen 추가, `Program.cs` DI·mapping
- Frontend: 신규 감사 조회 page component, `App.tsx` route·관리자 메뉴, `api.ts`·`selectedExportRegistry.ts`, `styles.css`
- DB/Migration: additive index migration 1개(구현 시점 다음 번호)
- Tests/Scripts: Backend integration·migration tests, frontend unit tests, isolated Full-Stack E2E 시나리오
- Docs: Roadmap 추적 항목 91·실험 원장 갱신, 5종 산출물

## 18. Roadmap 연결

- 선행 Task: `TASK-NOTIFY-005` (`EXPERIMENT_COMPLETE / BATCHED_FINAL`), TASK-EXPORT-001 Change 002/003 선택 Excel 공통 계약
- 후속 Task: provider delivery 감사 통합·전체 field-level audit 확장(추적 48), 운영 승격·Persistent UAT 적용
- 현재 Go/No-Go: Go — 실험 원장 우선순위 1, Roadmap 추적 항목 91과 일치, 같은 목적의 중복 Task 0건
- 별도 Task로 분리할 항목: `TASK-NOTIFY-REPROCESS-001`(같은 배치, 별도 기획), 일반 사용자 본인 audit 열람(미요청)

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-20 | 남은 작업 1번·3번 일괄 진행, 인터뷰·중간 승인 생략, Fable 권장안 자동 채택, local experiment commit까지 | fast-track interview 확정, 본 1차 기획 작성 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 0
