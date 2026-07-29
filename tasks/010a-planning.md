# TASK-010A — 패널별 키팅 완료·제조 내 업무 생성 기획안 (Fable 1차 기획)

> 상태: Draft
> 작성 단계: Codex 내용 review 전 (experiment two-pass 1차 기획)
> 목적: 입고 확정된 자재와 패널 제조 투입 사이의 끊긴 인수인계를 패널 단위 키팅 완료 record와 제조 내 업무 exactly-once 생성으로 연결하는 계약 확정

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/010a-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- fastTrackMode: `EXPERIMENT_TWO_PASS`
- sourceTask: `TASK-010A`
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 008A·009A는 구매품목별 도착·IQC·입고 확정(derived `receipt_completed`)까지 관리하지만, 그 자재가 어느 패널의 제조 투입 준비를 충족했는지 기록하고 제조팀에 패널 단위로 넘기는 데이터·화면·transaction이 없다. `panel_placeholders.workflow_stage`는 계속 `BeforeManufacturing`에 머물고 인수인계는 구두·메신저로 이뤄진다.
- 대상 사용자·역할: 자재 정·부 담당(키팅 queue 조회·패널 선택·일괄 완료, `MaterialReceiptUpdate`), 제조 정·부 담당(완료 패널·생성된 제조 내 업무 조회, 키팅 mutation 불가), 생산관리·구매·품질(프로젝트 요약 조회), Read-only·System Administrator(승인된 조회·감사, mutation 우회 금지).
- 정상 흐름: 프로젝트 자재 입고 조건 충족 → 키팅 queue 활성화 → 패널 선택 → 서버 전제조건·상태 재검증 → 패널별 키팅 완료 record 고정 → 패널당 제조 내 업무 1건 생성 → 완료 결과와 다음 행동 표시.
- 예외·복구 흐름: 비활성 프로젝트·취소 패널·패널정보 미완료·입고 준비 미충족·이미 완료·선택 0건·stale 상태·권한/scope 불일치를 서버가 안정적인 한글 오류로 차단. 동일 요청 재시도에도 완료 결과는 하나. 완료 record 되돌리기·삭제 없음.
- 확정한 정책과 명시적 제외: 키팅은 패널 단위, 완료 시 제조 내 업무 생성, 별도 생산 불출 없음, Backend 권한·불변조건 authoritative, stage 전진-only, 중복 방지. 제외 — 제조 실행(`TASK-011A`), BOM·소요량·재고 차감, 완료 되돌리기·재키팅, 첨부·Excel·PDF, 신규 외부 알림 채널, Persistent UAT·대표 repo·`main`·게시.
- planning으로 넘긴 비차단 미결정 사항: 부분 키팅의 최소 의미, readiness 전제, 키팅 상태 model, 일괄 transaction 방식, 제조 내 업무 생성 방식 — 이 문서 12장에서 Repository 근거와 함께 권장안을 확정 대상으로 명시한다(fast-track standing instruction에 따른 권장안 자동 채택). cancellation 패널·비활성 프로젝트 처리도 12장에서 권장안으로 확정한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

자재 담당자가 모바일에서 입고 준비가 끝난 프로젝트의 패널을 선택해 한 번에 키팅 완료하고, 제조 담당자가 패널별 내 업무를 중복 없이 받아 제조 투입 가능 상태를 시스템 데이터로 확인할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- Roadmap 18단계의 7번(입고 확정)까지는 이 실험 계보에 구현되어 있다. `material_receipts` 상태 machine, `material_arrivals_closed_at_utc` 입고 마감, trigger로 보호되는 derived `receipt_completed` 단일 진실, `material_receipt_events` append-only 원장이 존재한다(migration `0030`~`0032`).
- 8단계(키팅 완료)는 workflow 기반만 있다. `work_items.target_type`에 `'Panel'`이, workflow stage catalog에 `KittingCompleted`(자재 담당, 다음 단계 `ManufacturingWork`)가 이미 정의되어 있지만, 실제 키팅 데이터·화면·mutation·제조 handoff는 없다.
- `panel_placeholders.workflow_stage`의 CHECK 제약(migration `0007`)에는 키팅 값 자체가 없다(`BeforeManufacturing` 다음이 바로 `ManufacturingInProgress`). 키팅 완료를 이 컬럼에 우겨넣으면 기존 제약·소비자와 충돌한다 — 별도 완료 record가 필요한 구조적 근거다.
- 방치하면 입고 확정 자재가 있어도 “어느 패널이 제조 투입 가능한가”가 감사 가능한 데이터로 남지 않고, 제조 내 업무를 수동으로 만들면 같은 패널에 중복 업무가 생길 수 있다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 자재 담당 (`MaterialReceiptUpdate` policy) | 키팅 queue 조회, 패널 선택, 단건·일괄 키팅 완료 | 기존 project access 범위 | 활성 프로젝트·활성 패널의 키팅 완료 생성만 (수정·삭제 없음) |
| 제조 담당 (`manufacturing.update` 권한 보유자) | 생성된 패널별 제조 내 업무·키팅 완료 상태 조회 | 기존 project access 범위 | 키팅 mutation 불가 |
| 생산관리·구매·품질 (`ProjectRead` 권한) | 프로젝트별 입고/키팅 준비·완료 요약 조회 | 기존 프로젝트 접근 범위 | 키팅 mutation 없음 |
| Read-only·System Administrator | 승인된 조회·감사 | 기존 정책 범위 | 업무 mutation 우회 금지 |

신규 permission·policy는 추가하지 않는다. 키팅 mutation은 기존 `MaterialReceiptUpdate` policy(자재 도착~입고 마감과 동일 경계), 조회는 인증 + `ProjectRead`와 project scope를 서버에서 강제한다. Roadmap 5장 확정대로 자재 정·부 담당의 키팅 소유를 그대로 따른다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 모바일 일괄 키팅 완료와 제조 handoff

1. 자재 담당이 `패널 키팅` 화면(또는 “패널 키팅” 내 업무의 deep link)을 열면, 접근 가능한 프로젝트별로 입고 준비 상태(전체 구매품목 대비 입고 확정 완료 수)와 키팅 대기/완료 패널 수가 요약 카드로 보인다.
2. 준비 완료 프로젝트를 열면 활성 패널이 한 열 카드로 나열되고(완료 패널은 완료 표시), 선택 mode에서 일부 또는 전체 패널을 체크한 뒤 `선택 패널 키팅 완료`를 누른다.
3. 서버가 같은 transaction에서 프로젝트 활성·패널 활성·패널정보 완료·입고 readiness·미완료 여부를 재검증하고, 선택 패널 전부에 대해 불변 완료 record를 고정하며 패널당 제조 내 업무 1건을 생성한다. 하나라도 실패하면 전체 rollback과 실패 사유를 반환한다.
4. 화면은 action 인접에 “N개 패널 키팅 완료 · 제조 내 업무 N건 생성”을 표시하고, 제조 담당자는 자신의 내 업무 목록에서 패널별 `제조 작업` 업무를 확인한다.

### 시나리오 B — 부분 키팅과 잔여 패널 처리

1. 준비 완료 프로젝트에서 자재 담당이 일부 패널(예: 3면 중 2면)만 선택해 완료한다.
2. 남은 패널은 계속 키팅 대기로 표시되고, 이후 별도 요청으로 완료할 수 있다. 이미 완료된 패널은 선택 대상에서 제외되며 다시 완료를 시도하면 “이미 키팅 완료된 패널” 오류로 차단된다.
3. 프로젝트의 마지막 활성 패널이 완료되는 시점에 프로젝트 workflow의 `KittingCompleted` 단계 완료가 기록되고, “패널 키팅” 내 업무가 완료 처리되며, 생산관리·제조 참조 대상에게 일괄 묶음 인앱 참조 알림 1건이 생성된다.

### 시나리오 C — 동시 요청·재시도·차단 경로

1. 두 자재 담당자가 같은 패널을 포함해 동시에 일괄 완료를 요청하면, 완료 record의 패널당 unique 제약과 row lock으로 한 요청만 성공하고 다른 요청은 “이미 완료된 패널 포함” 충돌로 전체 거부된다. 제조 내 업무는 패널당 1건만 존재한다.
2. network 실패 후 같은 선택을 재시도하면 이미 완료된 패널이 감지되어 중복 record·중복 업무가 생기지 않는다.
3. 입고 readiness 미충족 프로젝트, 취소 패널, 보류·취소·삭제 프로젝트, 권한 없는 사용자, 접근 범위 밖 프로젝트의 요청은 각각 구분된 한글 오류로 차단된다. 완료 record는 어떤 경로로도 수정·삭제되지 않는다.

## 5. 기능 요구사항

### 필수

- [ ] 패널별 불변 키팅 완료 record(`panel_kitting_completions`): 패널당 최대 1건(unique), actor·시각·batch correlation·readiness snapshot 보존, update/delete 없음
- [ ] 프로젝트 입고 readiness의 서버 authoritative 판정: 프로젝트에 구매품목이 1개 이상 존재하고 모든 (취소되지 않은) 구매품목의 derived `receipt_completed`가 true — 사급(`CustomerSupplied`) 품목 포함
- [ ] 키팅 queue 조회 API: 프로젝트별 readiness 요약 + 활성 패널의 대기/완료 상태, `ProjectRead` + project scope 강제
- [ ] 단건·일괄 키팅 완료 mutation: 선택 패널 전부 재검증 후 all-or-nothing 단일 transaction, `MaterialReceiptUpdate` policy
- [ ] 패널당 제조 내 업무 exactly-once 생성: `target_type='Panel'`, stage `ManufacturingWork`, 책임 `ManufacturingPrimary`, 고정 idempotency key(`kitting:panel:{panelId}:manufacturing` 형태), 기존 unique `idempotency_key` + `on conflict do nothing` 패턴 재사용
- [ ] 입고 readiness 최초 충족 시 자재 담당 “패널 키팅” 내 업무 자동 생성(18단계 7→8 handoff, idempotency key `materials:kitting:{projectId}` 형태), 마지막 패널 완료 시 해당 업무 완료 처리
- [ ] 마지막 활성 패널 완료 시 프로젝트 workflow `KittingCompleted` 단계 완료 event 기록(기존 `project_workflow_events` 기록 경로 재사용)
- [ ] 일괄 완료당 인앱 참조 알림 1건(생산관리·제조 참조, Roadmap 6.5.5 묶음 원칙) — 신규 채널·delivery 없음
- [ ] 모바일 우선 adaptive `패널 키팅` 화면: 프로젝트 요약 → 패널 카드 선택 → 일괄 완료, deep link(`?project=`·`?panel=`)로 대상 프로젝트·패널 focus, desktop·390px·Teams narrow overflow 0
- [ ] additive migration 1건(`0033`, 현재 최신 `0032` 이후), 기존 데이터 완료 추정 backfill 없음, fresh/existing isolated 검증
- [ ] Backend 권한·transaction·동시성 tests, Frontend lint·typecheck·unit·build, isolated Full-Stack E2E, 페이지별 desktop·390px screenshot

### 선택

- [ ] queue의 “전체 선택(준비된 패널)” 편의 action — 선택 UX 구현 중 판단에 위임
- [ ] 프로젝트 상세 Workflow tab에 키팅 완료 패널 수 표기 — 기존 projection에 additive 필드로 수용 가능하면 포함

### 명시적 제외

- [ ] 제조 체크리스트·작업 시작/종료·제조 중단(`TASK-011A`)과 `panel_placeholders.workflow_stage` 전이 변경
- [ ] BOM, 패널별 자재 소요량 master, 패널 내부 자재 allocation, 창고 위치·재고 차감·생산 불출
- [ ] 키팅 완료 되돌리기·관리자 강제 정정·재키팅 workflow(후속 정책 결정 전 미구현)
- [ ] 첨부·사진·Excel·PDF, 신규 외부 알림 채널·Teams/Mail/Activity delivery 생성
- [ ] `0030`~`0032` migration과 008A·008B·009A 상태 machine 수정
- [ ] Persistent UAT write·migration 적용·runtime handover, 대표 repo·GitHub `main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 키팅 queue (신규 `/materials/kitting`) | 자재 메뉴·“패널 키팅” 내 업무 deep link | 프로젝트 카드: 입고 준비(완료 품목 수/전체), 키팅 대기·완료 패널 수, 준비 미충족 사유 요약 | 프로젝트 선택 | 기존 `LoadState` loading/empty/error 구분, 준비 미충족 프로젝트는 비활성 안내 |
| 프로젝트 키팅 상세 (같은 route의 선택 상태) | queue 카드·deep link `?project=` | 활성 패널 카드(표시 코드·패널명·키팅 상태), 선택 수, 완료 전제 요약 | 패널 선택/해제, `선택 패널 키팅 완료` | 선택 0건 비활성, 저장 중 중복 submit 차단, action 인접 성공 요약(완료 패널 수·생성 업무 수), 실패 시 패널 단위 사유 한글 표시 |
| 완료 상태·handoff 확인 | 완료 직후·`?panel=` deep link | 완료 패널의 actor·시각, 생성된 제조 내 업무 안내 | 조회만 | 이미 완료 패널은 완료 badge와 완료 정보 표시 |
| 제조 내 업무 (기존 내 업무 화면) | 내 업무 목록 | `제조 작업 · {패널 표시 코드}` 업무와 deep link | 기존 내 업무 흐름 | 기존 화면 계약 유지, 신규 화면 없음 |

확인할 UX 항목:

- 프로젝트가 왜 아직 준비되지 않았는지(미완료 구매품목 수)가 화면 안에서 읽히는가?
- 선택 mode에서 현재 몇 개를 선택했고 완료 시 무슨 일이 생기는지(제조 업무 생성)가 action 근처에 보이는가?
- 완료·이미 완료·준비 미충족·권한 부족이 서로 구분되는가?
- 390px·Teams narrow에서 PC table 축소가 아닌 한 열 카드 composition과 44px touch target, page-level horizontal overflow 0을 유지하는가? (기존 `useAdaptiveLayout`·`MobileSheet`·MaterialsWorkspace 카드 패턴 재사용)

## 7. 업무 규칙과 불변조건

- 입고 확정 상태는 계속 authoritative다. derived `receipt_completed`와 그 보호 trigger, `0030`~`0032` 상태 machine을 변경·우회하지 않으며, 키팅 readiness는 이를 읽기만 한다.
- 키팅 완료는 활성 프로젝트의 활성·패널정보 완료 패널에만, 입고 readiness 충족 시에만 가능하다. 서버가 mutation transaction 안에서 전부 재검증한다.
- 키팅 완료는 전진-only·불변이다. 완료 record의 수정·삭제·되돌리기 경로를 제공하지 않는다. 정정 정책은 이번 Task에서 추가하지 않는다.
- 패널당 완료 record 1건, 제조 내 업무 1건이 exactly-once 불변조건이다. unique 제약과 고정 idempotency key가 동시·재시도 요청에서 이를 보장한다.
- 일괄 완료는 all-or-nothing이다. 성공·실패가 혼재한 부분 커밋을 만들지 않는다.
- `panel_placeholders.workflow_stage`는 이번 Task에서 전이·제약을 변경하지 않는다. 키팅 완료 후에도 패널은 `BeforeManufacturing`에 머물며, 제조 시작 전이는 `TASK-011A` 소유다.
- 취소된 패널은 queue·선택 대상에서 제외한다. 이미 완료된 패널이 이후 취소되어도 완료 record와 제조 내 업무는 삭제하지 않는다(이력 보존, 업무 취소는 기존 내 업무 정책 범위).
- 권한·project scope는 UI 숨김이 아니라 서버 policy로 강제하고, System Administrator도 업무 mutation을 우회하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 키팅 완료 record | 패널당 1건: project·panel FK, unique(panel), 완료 actor·시각, batch correlation id, readiness snapshot(구매품목 수·완료 수·검증 시각 등 aggregate JSON) | 신규 1 table | append-only, update/delete 없음 |
| 프로젝트 입고 readiness | 모든 구매품목 `receipt_completed` 충족 여부 | 기존 데이터의 읽기 전용 판정(신규 저장 없음) | mutation 시 transaction 내 재검증 |
| 제조 내 업무 | `work_items` `target_type='Panel'`·stage `ManufacturingWork`·고정 idempotency key | 기존 table 재사용(스키마 변경 없음) | 기존 unique key·이력 유지 |
| “패널 키팅” 내 업무 | readiness 충족 시 자재 담당 업무, 마지막 패널 완료 시 완료 처리 | 기존 table 재사용 | 기존 idempotency 패턴 |
| workflow 단계 완료 | 마지막 패널 완료 시 `KittingCompleted` StageCompleted event | 기존 `project_workflow_events` 재사용 | append-only |
| 참조 알림 | 일괄 완료당 인앱 1건(묶음) | 기존 notifications 재사용 | 기존 idempotency 패턴 |

```text
패널 키팅: (대기: record 없음) → KittingCompleted(불변 record)   ← 전진-only, 패널당 1회
프로젝트: 구매품목 전체 receipt_completed → 키팅 가능 → 마지막 패널 완료 시 KittingCompleted 단계 완료
```

Migration: `0033` additive 1건 — 신규 table·CHECK·unique·index만 포함한다. 기존 migration·trigger·상태 machine은 수정하지 않고, 기존 패널을 완료로 추정 backfill하지 않는다. rollback은 destructive down이 아니라 forward-fix 원칙으로 문서화한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 7장 invariant 전부(권한·scope, readiness 재검증, 패널 상태, exactly-once, all-or-nothing, 전진-only).
- 필요한 조회와 mutation(초안, 구현 시 기존 convention에 맞춰 확정):
  - GET 키팅 queue(인증 + `ProjectRead`, project scope): 프로젝트 readiness 요약 + 활성 패널 키팅 상태
  - POST 키팅 완료(`MaterialReceiptUpdate`): project id + panel id 목록(1건 이상), 단건은 목록 1개로 동일 경로 처리
- 권한·validation: 신규 policy 없음. 오류는 안정적 status와 사용자 행동 가능한 한글 메시지(“입고가 완료되지 않은 구매품목이 있습니다”, “이미 키팅 완료된 패널이 포함되어 있습니다” 등). 응답에 내부 식별자·raw SQL을 노출하지 않는다.
- transaction·동시성·idempotency: 단일 transaction에서 대상 패널 row lock(`for update`) → 프로젝트·패널·readiness 재검증 → 완료 record insert(unique(panel_id)가 최종 방어선) → 패널별 제조 work item insert(`on conflict (idempotency_key) do nothing`) → “패널 키팅” 업무 완료 → 마지막 패널이면 StageCompleted event·묶음 알림. 어느 검증이든 실패하면 전체 rollback. 동시 이중 제출·재시도 테스트를 포함한다. `MaterialsStore`의 기존 소유 경계(row lock, `ResolveAssigneeAsync`의 responsibility+permission fallback, `CompleteWorkItemAsync`, 고정 idempotency key 규약)를 재사용한다.
- audit trail: 완료 record 자체가 actor·시각·batch·readiness snapshot을 보존하고, workflow event·work item·notification이 기존 append-only 계약으로 남는다. 별도 event table은 추가하지 않는다(최소 조합 — 12장 3-A).
- 제조 내 업무 deep link: 제조 입력 화면이 없으므로(011A) 당분간 키팅 화면의 대상 패널 조회(`/materials/kitting?project=…&panel=…`)로 연결하고, `TASK-011A`가 제조 화면으로 대체한다.
- 외부 provider 영향: 없음. Teams/Mail/Activity delivery를 생성하지 않는다.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 이 문서가 최종 확정하지 않으며, 구현 시 기존 소유 경계(자재 흐름은 `Materials` 계열, workflow event·work item 규약은 기존 패턴)에 맞춘다.

## 10. Frontend 고려사항

- route/component: 신규 pathname `/materials/kitting`을 기존 `App.tsx` pathname routing에 추가하고, `MaterialsWorkspace.tsx` 패턴(adaptive 카드·`LoadState`·query param deep link)을 따르는 키팅 page를 구성한다. `materials.ts`·`api.ts` type 확장.
- loading/empty/error/success: 기존 `LoadState`·`StateMessage`·action 인접 feedback 재사용. 일괄 완료는 저장 중 disabled와 결과 요약 표시.
- 공통 Action Feedback: 중복 submit 차단, 실패 사유 field/카드 인접 표시, `aria-live` 계약 유지.
- 접근성: 패널 선택 checkbox의 명시적 label, 색상 외 상태 텍스트, keyboard 접근, 44px hit area.
- 390px/mobile/narrow pane: 한 열 카드·선택 mode·하단 고정 action 영역, page-level horizontal overflow 0, 기존 모바일 shell(top drawer·shape system) 규약 유지.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 18단계 7→8→9 handoff를 기존 work item·workflow event·인앱 알림 규약으로 연결한다. 신규 알림 유형·채널 없음, 일괄 묶음 1건 원칙(6.5.5) 준수.
- 권한/관리자: 기존 `MaterialReceiptUpdate`·`ProjectRead`·`manufacturing.update` 재사용, 신규 권한 없음.
- Excel/PDF/첨부: 영향 없음(이번 Task 제외).
- Teams/Mail: 영향 없음(발송·delivery 생성 없음).
- 삭제·복구/감사: 완료 record append-only. 프로젝트 보류·취소·삭제 시 신규 완료 mutation은 차단하되 기존 record·업무는 보존(기존 soft-delete·업무 정책 범위).

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| 1-A (권장) | 부분 키팅 = 패널 선택 단위 완료, 패널 내부 자재 allocation 제외 | 현재 데이터로 증명 가능, BOM 없는 상태에서 거짓 정밀도 없음, Roadmap “부분/일괄” 충족 | 자재별 부분 준비 표현은 후속(BOM 확정 후) |
| 1-B | 패널 내부 자재별 부분 키팅 | 정밀해 보임 | 패널-자재 소요량 master가 없어 임의 매핑 창작 — 이력 보존 원칙 위반 위험 |
| 2-A (권장) | readiness = 프로젝트의 모든 구매품목 derived `receipt_completed`(≥1개 존재, 사급 포함) | 입고 마감+전량 확정을 이미 검증된 단일 진실로 재사용, 제조 투입 의미가 명확 | 보수적 — 품목 일부만 필요한 패널도 대기(1-A와 같은 근거로 수용) |
| 2-B | 도착 건 1건 확정 시 허용 | 빠름 | 누락 위험, “제조 투입 가능” 의미 약화 |
| 3-A (권장) | 패널당 불변 완료 record 1 table + 기존 event·work item 재사용, panel stage 불변 | unique로 exactly-once 보장, batch·snapshot 감사, `0007` CHECK·기존 소비자 무변경 | 조회 시 join 1회 추가 |
| 3-B | `panel_placeholders`에 컬럼 추가 또는 stage enum 확장 | 단순 | 공유 CHECK 제약 변경이 011A 이후 전이 계약과 얽히고 batch·감사 근거 약함 |
| 4-A (권장) | 일괄 완료 all-or-nothing 단일 transaction | 결과가 명확, 재선택·감사 UX 단순, 인터뷰 권장안 일치 | 대량 선택 시 한 패널 문제로 전체 거부(사유를 패널 단위로 안내해 완화) |
| 4-B | 부분 성공 허용 | 대량 작업 빠름 | 성공·실패 혼재 결과 해석·재시도 UX 복잡 |
| 5-A (권장) | 제조 내 업무 = 패널당 1건, `target_type='Panel'` + 고정 idempotency key, 기존 assignee 해석(책임자 우선, `manufacturing.update` 보유자 fallback) 재사용 | 패널 추적·deep link·중복 방지와 일치, 008A 패턴 그대로 | 패널 수만큼 업무 생성(제조 실무 단위와 일치하므로 수용) |
| 5-B | 프로젝트당 제조 업무 1건 | 업무 수 적음 | 패널 단위 추적 불가 — 데이터 단위 원칙 위반 |
| 6-A (권장) | 취소 패널 제외·비활성 프로젝트 mutation 차단·완료 record 영구 보존, 과거 완료는 완료 badge로 상시 조회 | 이력 보존 원칙과 일치, 추가 상태 없음 | 취소 패널의 기생성 제조 업무 정리는 기존 업무 취소 정책에 위임 |
| 6-B | 취소·삭제 시 완료 record 정리 | 화면 단순 | 감사 이력 파괴 — 채택 불가 |

권장안 1-A·2-A·3-A·4-A·5-A·6-A를 채택한다(사용자 fast-track 지시에 따른 권장안 자동 채택). readiness 충족 시 자재 “패널 키팅” 내 업무 자동 생성은 18단계 표의 7번 “다음 내 업무” 확정사항을 구현하는 것으로 별도 선택지가 아니다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL·synthetic 데이터만 사용한다.
- migration 필요 여부: `0033` additive 1건. 기존 migration 불변. 실 DB 적용은 별도 사용자 승인.
- 외부 발송/실제 데이터 영향: 없음. 인앱 원본만 생성하고 provider·delivery는 만들지 않는다.
- runtime 교체 여부: 없음. experiment branch 내 isolated 검증만 수행한다.
- 추가 사용자 승인 필요 작업: 실 DB migration 적용·Persistent UAT handover, 대표 repo 반영(push·PR·merge — main merge는 분리된 승인 3회 전 금지), 정정·재키팅 정책의 별도 NEW_FEATURE planning.

## 14. 검증 계획

- 최소 테스트: Backend Release build. targeted tests — queue·mutation 권한 allow/deny(자재/제조/조회 역할/Read-only/System Administrator)와 project scope, readiness 판정(품목 0건·일부 미완료·전체 완료·사급 포함), 취소 패널·패널정보 미완료·비활성 프로젝트 차단, 이미 완료 재요청 차단, 일괄 all-or-nothing rollback, 동시 이중 제출 시 완료 record·제조 업무 각 1건, 재시도 idempotency, readiness 충족 시 “패널 키팅” 업무 1회 생성, 마지막 패널 완료 시 StageCompleted event·업무 완료·묶음 알림 1건, migration catalog + fresh/existing isolated apply.
- 영향 영역 회귀: 008A 도착·IQC·입고 확정·마감, 008B supplyType, 009A 성적서 최종화 filtered tests와 work item idempotency 회귀.
- PR/CI: 대표 repo PR 없음(실험 branch). Frontend lint/typecheck/unit/build와 isolated Full-Stack E2E(입고 확정 → queue 활성 → 일부 선택 완료 → 제조 내 업무 확인 → 잔여 완료 → 단계 완료 + 기존 자재·품질 spec)를 local에서 통과시킨다.
- 사용자 검수: desktop·390px에서 queue, 프로젝트 키팅 상세(선택 mode), 완료 결과, 제조 내 업무 화면의 synthetic screenshot을 보고한다. 자동 검증 완료와 사용자 검수 완료는 별도 상태로 기록하고 사용자 검수 완료로 표시하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 시나리오 A~C가 서버 authoritative로 동작하고 7장 invariant 위반 시도가 모두 차단·테스트된다. 008A·008B·009A 회귀 0.
- UX: 6장 확인 항목과 390px·Teams narrow page-level overflow 0.
- 자동 테스트: 14장 전체 PASS, 미실행 항목은 이유와 함께 기록.
- 5종 산출물: implementation report에서 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기`로 handoff.
- PR 상태: N/A — 실험 branch local commit까지가 범위.

## 16. 미결정 사항

Blocking 결정은 없다. 아래는 명시적으로 deferred된 비차단 사용자 결정 항목이다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 실제 BOM·패널별 자재 소요량 master와 패널 내부 자재 allocation 도입 | 현행 패널 단위 유지 / BOM 확정 후 별도 planning | 대기 (현업 회신) |
| 2 | 잘못된 키팅 완료의 정정·재키팅·관리자 강제 정정 운영 정책 | 별도 NEW_FEATURE planning | 대기 |
| 3 | 창고 위치·재고 차감·생산 불출 정책 | 1차 시스템 제외 유지 / 후속 검토 | 대기 (Roadmap 확정: 1차 제외) |
| 4 | `0033` 실 DB 적용·Persistent UAT handover·대표 repo 반영 시점 | 별도 승인 절차 | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Materials/` 키팅 store·contracts·endpoints(신규 또는 기존 파일 확장), `MaterialsStore.cs`(readiness 충족 시 키팅 업무 생성 hook), workflow event·notification 재사용 연결
- Frontend: `MaterialsWorkspace.tsx` 또는 신규 키팅 page component, `materials.ts`, `api.ts`, `App.tsx`(route·메뉴), `styles.css`
- DB/Migration: `database/migrations/0033_*.sql` 신규 1건
- Tests/Scripts: `PostgreSqlMigrationTests.cs`, Materials/Workflow Backend tests 신규·확장, Frontend unit, Full-Stack E2E spec
- Docs: Roadmap TASK-010A 실험 상태 기록(canonical 큐 불변), interview·planning·review·implementation report

## 18. Roadmap 연결

- 선행 Task: TASK-008A(입고 원장·derived 완료)·008B(사급)·009A(IQC 성적서) — 이 실험 계보에 구현·자동 검증 완료(사용자 검수 대기, canonical 미반영). 실험 순서 재정렬은 interview Task Identity Gate에 `explicitRoadmapOverrideApproved: true`로 기록됨.
- 후속 Task: `TASK-011A`(제조 체크리스트·작업 시작/종료·중단 — 이 Task의 제조 내 업무·패널 stage 전이를 이어받음), `TASK-007B`(병목 집계), 정정·재키팅 정책 Task. canonical 실행 큐의 `Dependency Pending`과 다음 `TASK-007A` Gate는 변경하지 않는다.
- 현재 Go/No-Go: 이 문서는 1차 기획이다. Codex 내용 review → Fable 2차 기획(별도 승인된 target) 뒤에만 구현이 시작된다.
- 별도 Task로 분리할 항목: 16장 1~3번.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | 사용자 experiment fast-track 지시(인터뷰 생략·권장안 자동 채택·local commit까지) | 비차단 정책 5건 + 취소·비활성 처리 1건을 12장 권장안 1-A·2-A·3-A·4-A·5-A·6-A로 확정 대상 기록 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

2차 기획 확정 전 참고용 초안이며 구현 승인이 아니다.

1. instruction chain gate를 수행하고 `taskType: APPROVED_FEATURE_IMPLEMENTATION`, branch 기준선을 보고한다.
2. `0033` additive migration으로 패널 키팅 완료 record 스키마(unique(panel_id)·CHECK·index)만 추가한다. `0030`~`0032` 포함 기존 migration·trigger·`0007` CHECK 제약은 수정하지 않고 backfill하지 않는다.
3. Backend: queue 조회와 all-or-nothing 일괄 완료 mutation을 7·9장 invariant대로 구현한다. row lock·재검증·완료 record·패널별 제조 work item(`on conflict do nothing`)·“패널 키팅” 업무 완료·마지막 패널 StageCompleted event·묶음 인앱 알림을 하나의 transaction에서 처리하고, readiness 충족 시 “패널 키팅” 업무 생성을 기존 입고 확정·마감 transaction 경계에 결합한다. 기존 `ResolveAssigneeAsync`·`CompleteWorkItemAsync`·idempotency key 규약을 재사용한다.
4. Frontend: `/materials/kitting` adaptive 화면(프로젝트 요약 → 패널 카드 선택 → 일괄 완료)과 deep link를 desktop·390px에서 구현하고 기존 `LoadState`·Action Feedback·모바일 카드 패턴을 재사용한다.
5. 검증: 14장 계획을 실행하고 실행/미실행을 분리해 implementation report에 기록한다. synthetic 데이터만 사용하고 Persistent UAT write, 실제 provider 발송, 대표 repo 게시를 수행하지 않는다.
6. 16장 deferred 항목을 임의 결정하지 않고 5장 제외 범위를 추가하지 않는다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 4
