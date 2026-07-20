# TASK-NOTIFY-AUDIT-001 — 알림 설정 감사 조회 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5_EXPERIMENT_FAST_TRACK`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 현재 `experiment/*` branch에서 사용자가 요청한 관리자 알림 설정 감사 조회 기능의 interview source of truth다. 사용자는 `Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot → local commit`까지 인터뷰·중간 승인 없이 진행하도록 명시했다. 비차단 제품 선택은 Fable 권장안을 자동 채택한다. 대표 repo·GitHub `main`·Persistent UAT·실제 provider·push·PR·merge는 제외한다.

## Task Identity Gate

- proposedTaskId: `TASK-NOTIFY-AUDIT-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-NOTIFY-AUDIT-001`
- roadmapNextGate: `TASK-NOTIFY-005 후속 — 관리자 preference 감사 조회 UI`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-NOTIFY-AUDIT-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-NOTIFY-005 후속 — 관리자 preference 감사 조회 UI`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: System Administrator가 사용자별 알림 설정 변경 감사 원장을 검색·필터·요약하고 필요한 행을 선택 Excel로 내보내 운영 문의와 변경 책임을 확인한다.
- Root Finding 또는 정책 결정: `TASK-NOTIFY-005`는 fixed-field append-only audit를 저장하지만 조회 API·화면은 P3 후속으로 분리했다. 저장 원장을 코드·DB 직접 조회 없이 관리자 화면에서 이해할 능력이 없다.
- 변경·검증 경계: 기존 audit 원장의 read-only query/API, 안정적 pagination·filter·summary, 관리자 desktop/390px UI, 기존 선택 Excel 패턴, 권한·비활성 사용자·empty/error 처리와 isolated DB/E2E를 포함한다.
- 보존할 불변조건: audit append-only·원문 불변, 본인/일반 역할 audit 조회 금지, 사용자 preference 저장·reset·suppression gate 불변, Backend authoritative, actual provider call 0, `main`·Persistent UAT 불변.
- 예상 산출물: Fable 2-pass planning, Codex review, read API/UI/선택 Excel/tests, desktop/mobile screenshot, Implementation report와 5종 종료 산출물, local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR·Issue

`TASK-NOTIFY-005` review와 구현 보고서는 감사 UI를 별도 신규 기능으로 명시했고 현재 구현은 audit row 저장까지만 포함한다. 같은 목적의 Task·branch·PR·Issue는 0건이다.

## 사용자 실행 지시

- 요청일: 2026-07-20
- 요청: 남은 작업 1번(관리자 알림 설정 감사 조회 UI)과 3번(terminal Failed 수동 재처리)을 한꺼번에 진행한다.
- 배치 경계: 두 기능은 한 개발 배치로 진행하되 Task identity·기획·Finding·검증 결과는 분리한다.
- 게시 경계: local experiment commit만 승인. main merge 승인 `0/3`.

## 1. 업무 문제와 기대 결과

- 현재 audit table은 actor, target, action, delivery type, channel, old/new value, resulting version, time을 고정 필드로 보존한다.
- 관리자는 설정 문의·오작동 의심·관리자 대리 변경 확인 때 DB를 직접 봐야 한다.
- 성공 결과는 관리자가 기간·행동·알림 종류·채널·대상/변경 주체 기준으로 원장을 찾고, 요약 수치와 변경 전후를 한글로 이해하며, 선택한 결과만 Excel로 보존하는 것이다.

## 2. 확정된 Repository 계약

- 권한은 기존 `QmsPolicies.AdminUsersRead`를 재사용하고 System Administrator 외 접근은 서버에서 `403`으로 차단한다.
- audit는 `Save`, `Reset`, `AdminSave`, `AdminReset`과 실제 값이 바뀐 항목만 기록한다. no-op은 audit와 version을 바꾸지 않는다.
- supported pair는 자동 단계 업무 생성/Teams 개인, D-1/Teams 개인, 일일 요약/Mail 3종이다.
- 실제 사용자 표시정보는 인증된 관리자 화면에서 기존 user projection 규칙으로만 제공하고 API에 credential·provider payload·raw internal error를 추가하지 않는다.
- 감사 조회는 read-only이며 preference나 delivery 상태를 변경하지 않는다.
- 신규 조회 화면도 기존 전체선택 checkbox + 선택 Excel 하나의 action 패턴을 따른다.

## 3. Fable이 권장할 비차단 선택

| 번호 | 결정 대상 | 비교 경계 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- |
| 1 | 기본 기간·pagination | 최근 30/90일, cursor/page 방식, page size | Fable 권장안 자동 채택 | No |
| 2 | 요약 카드 | 전체 변경·관리자 대리·사용자 직접·opt-out/reset 수치 | Fable 권장안 자동 채택 | No |
| 3 | 필터·검색 | action·delivery type·channel·actor/target·기간 | Fable 권장안 자동 채택 | No |
| 4 | 모바일 정보 밀도 | 핵심 변경 카드·필터 sheet·선택 export 노출 범위 | Fable 권장안 자동 채택 | No |
| 5 | Excel column | 필수 식별·변경·시각 열과 optional actor/target/version | Fable 권장안 자동 채택 | No |
| 6 | 개인정보 최소화 | 관리자 화면 표시명·부서 snapshot/current join 범위 | Fable 권장안 자동 채택 | No |

## 4. 정상·예외·복구 흐름

- 정상: 관리자 메뉴 → 감사 조회 → 기본 기간 요약 → 필터/검색 → 상세 행 확인 → checkbox 전체선택/부분선택 → 선택 Excel.
- empty: 조건에 맞는 변경이 없음을 명확히 표시하고 필터 초기화 action을 제공한다.
- stale/삭제 사용자: audit FK와 삭제 guard를 보존하며 현재 사용자 표시정보가 없을 때도 원장 자체는 깨지지 않는 안전한 표시를 사용한다.
- 권한: 일반 역할과 비인증 요청은 목록·요약·Excel 모두 차단한다.
- 실패: query/export 오류는 action 근처에 한글 안내하고 필터·선택 상태를 보존한다.

## 5. Data·API·감사

- 기존 `user_notification_preference_audit_events`를 authoritative source로 사용한다.
- list·summary·선택 Excel은 같은 server filter/label registry를 사용해 count와 row가 drift하지 않게 한다.
- server allowlist 밖 정렬·column·ID는 fail-closed한다.
- 화면 조회 자체를 다시 동일 audit table에 기록해 self-noise를 만들지 않는다. 필요 시 기존 HTTP security telemetry만 사용한다.
- migration이 필요한지는 실제 query index와 pagination 계약을 기준으로 Fable/Codex가 결정하되 기존 `0041`은 수정하지 않는다.

## 6. UX·모바일·접근성

- Desktop은 요약 카드, compact filter bar, scan 가능한 감사 table을 사용한다.
- 모바일은 PC table 축소가 아니라 변경 주체·대상·알림·변경 결과·시각 중심 카드로 재배치한다.
- loading·empty·error·success, keyboard/focus, label, `aria-live`, 390px horizontal overflow 0을 포함한다.
- DESIGN-000 semantic token과 기존 관리자 알림 화면의 thin divider·blue accent를 재사용한다.

## 7. 포함·제외

### 포함

- System Administrator 전용 감사 list·summary·filter·pagination
- 변경 전/후·actor/target·action·taxonomy 한글 표시
- checkbox 전체선택·선택 Excel
- desktop/390px 화면과 isolated DB/API/E2E

### 제외

- audit 수정·삭제, preference taxonomy 편집, 조직 기본값, 일반 사용자 audit 열람
- provider delivery attempt·메일/Teams 본문 감사와의 통합
- 실제 사용자·운영 DB·Persistent UAT·provider·push·PR·merge

## 8. 성공 기준

- 관리자만 같은 filter 기준의 summary·list·선택 Excel을 사용할 수 있다.
- no-op이 audit에 나타나지 않고 기존 3종 설정의 변경 전후·주체·대상·시각이 일치한다.
- large list pagination·empty/error·invalid filter가 안정적으로 동작한다.
- desktop과 390px에서 핵심 정보·선택 상태·export feedback이 명확하고 overflow가 없다.
- Backend/Frontend/isolated Full-Stack 검증과 privacy-safe screenshot이 통과한다.

## 9. Fable 확인용 요약

- 저장 원장은 구현 완료지만 관리자 조회 능력은 없다.
- 기존 fixed-field audit와 admin permission을 재사용한다.
- 조회·요약·선택 Excel만 추가하고 preference mutation·provider는 변경하지 않는다.
- `interviewStatus: COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`.
