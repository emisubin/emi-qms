# TASK-011A — 제조 작업 시작·종료·중단 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 제조 실행(시작·체크·종료·중단) 기능의 1차 기획 확정을 위한 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/011a-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`
- workflow: `experiment/*` fast-track — Fable 1차 기획 → Codex 내용 review → Fable 2차 기획 → Codex 구현

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 키팅 완료 시 panel별 `ManufacturingWork` 내 업무는 생성되지만(`PanelKittingStore` — idempotency key `kitting:panel:{panelId}:manufacturing`), 업무 link가 키팅 조회 화면(`/materials/kitting?...`)으로 돌아가고, 제조 시작·체크·종료·중단을 입력할 전용 화면·데이터·transaction이 없다. `panel_placeholders.workflow_stage`는 `BeforeManufacturing`에 머문다(현재 어떤 코드도 이 값을 전진시키지 않음을 확인).
- 대상 사용자·역할: 제조 정·부 담당(실행 mutation), 생산관리 정·부 담당(조회·Pending 조치 연결), Pending 조치 담당자(기존 Pending 계약 안의 조치), 품질·자재·영업·조회 역할(조회 전용), System Administrator(무제한 입력 우회 금지).
- 정상 흐름: 키팅 완료·제조 업무 생성 → 제조 queue/deep link 진입 → 작업 시작 → 최소 단계 체크 → 종료 전 서버 재검증 → 완료·panel stage 전진·기존 업무 완료 → 다음 단계 handoff.
- 예외·복구 흐름: 키팅 미완료·비활성 project/panel·업무 미생성·중복·활성 중단·필수 체크 미완료·stale version·권한/scope 불일치를 서버가 한글 오류로 차단. 동일 operation 재시도는 기존 성공 결과를 replay. 종료·stage 전진·업무 완료·handoff는 단일 transaction.
- 확정한 정책과 명시적 제외: 키팅 완료 선행, panel stage 전진-only, 제조 중단은 `ManufacturingStop` Pending·Urgent, 서버 권한 authoritative, append-only 이력. 제외 — LQC/OQC/FAT 상세(`TASK-012A`), 영구 template 관리(`TASK-ADMIN-002`), 현업 미회신 상세 자주순차표 확정, 완료 되돌리기·record 삭제, QR 공개 landing·실제 provider·Persistent UAT·게시.
- planning으로 넘긴 비차단 미결정 사항: 인터뷰 7절의 5개 선택(체크리스트 MVP, 작업 단위, 중단 후 재개, LQC handoff, 진입 구조)은 standing rule에 따라 본 문서의 권장안을 자동 채택한다. 제조 표시/팝업/저장-only 상세 항목·운영 template·LQC 상세 handoff는 Deferred다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

제조 담당자가 모바일에서 키팅 완료된 패널을 선택해 작업을 시작하고 최소 단계를 체크한 뒤 종료하며, 작업 불가 시 제조 중단을 `ManufacturingStop` Pending으로 즉시 전환할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 키팅 완료 후 생성된 제조 내 업무를 받아도 실제 제조 진행을 종이 자주순차표·구두·메신저로 관리하고, 시스템에는 결과 stage만 수동 해석으로 남긴다.
- 제조 착수 여부·실제 시작/종료 시각·중단 원인이 감사 가능한 데이터로 남지 않아 LQC handoff와 프로젝트 병목 판단이 현장 사실을 반영하지 못한다.
- 업무 deep link가 키팅 화면으로 연결되어 제조 담당자가 "무엇을 하면 되는지"를 화면에서 알 수 없다.
- 이 기능이 없으면 Roadmap 14장(제조 기준)의 "디지털 입력 중심 전환" 확정사항과 18단계 프로세스 9단계(제조 작업)가 시스템 상 빈 구간으로 남는다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 제조 정·부 담당 (`manufacturing.update`) | 제조 queue 조회, 시작·체크·종료, 중단 등록·재개 | 기존 project access scope | 키팅 완료 panel의 활성 제조 실행만 |
| 생산관리 정·부 담당 | 프로젝트·패널 진행/중단 조회, Pending 조치 연결 확인 | 기존 project access scope | 제조 실행 mutation 불가. Pending은 기존 `Pending.Manage` 계약만 |
| Pending 조치 담당자 | 중단 사유 확인·조치·상태 전이 | 기존 Pending scope | 기존 Pending 계약(배정·상태·댓글·expected version) |
| 품질·자재·영업·조회 역할 | 허용 프로젝트의 제조 상태 조회 | 기존 project access scope | 제조 mutation 불가 |
| System Administrator | 기준·이력 조회 | 기존 관리자 정책 | 업무 입력 무제한 우회 금지(서버 authorization 동일 적용) |

권한은 UI 숨김이 아니라 서버 Policy로 강제한다. mutation은 `ManufacturingUpdate` policy + project access scope, 조회는 기존 project read scope를 사용한다. `Manufacturing.WorkTime.Read`(기존 permission)는 시작/종료 시각 등 작업 시간 상세의 조회 노출 범위 제어에 재사용을 검토한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 모바일 정상 제조 수행

1. 제조 담당자가 내 업무의 `제조 작업 · <패널>` deep link 또는 좌상단 숨김 메뉴의 `제조` 진입점으로 제조 queue에 들어간다.
2. 시스템이 키팅 완료·제조 업무가 생성된 패널을 프로젝트별로 보여주고, deep link면 해당 패널에 focus한다.
3. 담당자가 `작업 시작`을 누르면 서버가 키팅 완료·활성 상태·중복 실행을 재검증하고 execution을 생성하며 panel stage를 `ManufacturingInProgress`로 전진시킨다.
4. 담당자가 고정 최소 단계(시작 확인 → 도면·자재 확인 → 작업 수행 → 자체 확인)를 순서대로 체크한다. 각 체크는 actor·시각과 함께 저장된다.
5. 모든 단계 체크 후 `작업 종료`를 누르면 서버가 단일 transaction으로 execution 완료, panel stage `ManufacturingCompleted` 전진, 기존 `ManufacturingWork` 내 업무 완료를 처리하고, 프로젝트의 마지막 패널이면 `ManufacturingWork` stage 완료 event와 LQC skeleton 업무 handoff까지 수행한다.
6. 담당자는 완료 상태와 다음 패널 안내를 action 근처에서 확인한다.

### 시나리오 B — 제조 중단과 조치 연결

1. 작업 중 자재·인원 문제로 작업이 불가하면 담당자가 `제조 중단`을 누른다.
2. 시스템이 사유 구분·설명·조치 담당자 입력을 받고, 단일 transaction으로 execution을 `Blocked`로 전환하고 stop event를 append하며 `ManufacturingStop`·`Urgent` Pending을 panel target으로 생성한다. 조치 담당자를 지정하면 기존 Pending 배정 계약대로 조치 업무와 차단 알림이 생성되고, 생산관리 담당자에게 참조 알림이 남는다(외부 delivery는 기존 outbox 기록까지, 실제 provider 미실행).
3. 조치 담당자가 Pending 상세에서 조치를 진행해 종결하면, 제조 담당자가 제조 화면에서 `작업 재개`를 눌러 같은 execution을 이어서 진행한다. 재개 event가 append되고 stage 번호는 후퇴하지 않는다.

### 시나리오 C — desktop 진행 조회

1. 생산관리·조회 역할이 desktop에서 제조 화면을 연다.
2. 시스템이 프로젝트별 패널 진행(시작 전/진행 중/중단/완료), 시작·종료 시각, 활성 중단 Pending 연결을 요약과 상세로 보여준다.
3. 조회 역할에는 mutation action이 노출되지 않고 서버도 거부한다.

## 5. 기능 요구사항

### 필수

- [ ] 키팅 완료 + 활성 `ManufacturingWork` 내 업무가 있는 panel 기준의 제조 queue API·화면 (project access scope 적용)
- [ ] panel당 1건의 활성 manufacturing execution 시작 (시작 시각·actor 기록, panel stage `BeforeManufacturing → ManufacturingInProgress` 전진)
- [ ] execution 생성 시점의 고정 최소 단계 snapshot 4단계와 단계별 체크(체크 actor·시각 기록)
- [ ] 작업 종료: 전체 체크 완료·비중단 상태·expected version 재검증 후 execution 완료, panel stage `ManufacturingCompleted` 전진, 기존 panel 제조 업무 완료를 단일 transaction으로 처리
- [ ] 프로젝트의 모든 활성 panel 제조 완료 시 같은 transaction에서 `ManufacturingWork` StageCompleted event 1회 생성(중복 방지 row lock)과 LQC skeleton 내 업무·참조 알림 handoff
- [ ] 제조 중단: 사유 구분·설명·조치 담당자 선택 입력, execution `Blocked` 전환, `ManufacturingStop`·`Urgent` Pending(panel target) 생성·연결을 단일 transaction으로 처리
- [ ] 중단 조치 종결 후 같은 execution의 명시적 재개(append-only stop/resume event)
- [ ] 모든 mutation의 client operation id 기반 성공 replay와 expected version 기반 stale conflict, 한글 오류 메시지
- [ ] 취소 panel/project lifecycle 정합(비활성 대상 mutation 차단, purge 시 신규 record 선행 정리 — `0033` 방식과 동일)
- [ ] 신규 제조 업무 deep link를 제조 화면으로 변경하고, 기존 `ManufacturingWork` 업무의 키팅 링크도 frontend에서 제조 화면으로 라우팅
- [ ] 모바일 우선 adaptive 제조 화면(queue → panel focus → 시작/체크/종료·중단 한 흐름)과 desktop 진행 요약·상세 composition

### 선택

- [ ] 중단 이력·재개 이력의 execution 타임라인 표시 (desktop 상세)
- [ ] 제조 queue의 프로젝트별 완료/잔여 count 뱃지

### 명시적 제외

- [ ] LQC/OQC/FAT 상세 검사성적서·사진·PDF·검사 데이터 생성 (`TASK-012A`)
- [ ] 영구 제조 template 관리 UI·version activation (`TASK-ADMIN-002`)
- [ ] 현업 미회신 상세 자주순차표 전체 항목의 임의 확정 (표시/팝업/저장-only 구분 포함)
- [ ] 완료 stage 되돌리기·관리자 강제 정정·실행 record 삭제
- [ ] 프로젝트 단위 일괄 시작/종료 입력(Roadmap 14장의 "프로젝트별 단계 입력"은 상세 항목 회신 후 후속 검토)
- [ ] QR 공개 landing, 신규 외부 알림 채널, 실제 provider delivery
- [ ] Persistent UAT migration·write·runtime handover, 대표 repo·`main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 제조 queue (모바일) | 좌상단 숨김 메뉴 `제조`, 내 업무 deep link | 프로젝트 가로 queue, 패널 compact card(상태 도형: 시작 전=각진 사각, 진행 중=둥근 사각+원형 progress, 중단=타원 경고, 완료=원형 체크), 잔여/완료 count | 프로젝트 선택, 패널 focus | queue loading/empty/error, 새로고침 |
| 패널 제조 실행 (모바일) | queue에서 패널 선택, deep link focus | 패널명·display code, 현재 단계 stepper, 시작/종료 시각, 중단 상태와 Pending 링크 | 작업 시작, 단계 체크(44px target), 작업 종료, 제조 중단, 작업 재개 | 저장 중 disable, 성공/차단/stale conflict를 action 바로 아래 표시, 중복 submit 차단 |
| 제조 중단 입력 (모바일 sheet) | 실행 화면 `제조 중단` | 사유 구분, 설명, 조치 담당자 선택 | 등록 → Pending 생성 확인 → Pending 상세 링크 | 성공 시 Blocked 상태와 Pending 번호 표시 |
| 제조 진행 (desktop) | 전역 `제조` 메뉴 | 프로젝트·패널 진행 요약, 실행 타임라인(시작·체크·중단·재개·종료), 활성 중단 Pending | 조회, (제조 권한 시) 동일 mutation | 동일 feedback + 권한 없음 상태 명시 |

확인할 UX 항목:

- 모바일은 PC 축소가 아닌 현장 행동 재구성: 한 화면에서 "지금 이 패널에서 다음에 누를 버튼"이 항상 하나로 명확해야 한다.
- 작은 글씨의 보조 정보(시각·actor)와 도형 기반 상태 언어(원형·타원·각진/둥근 사각형)로 정보 밀도를 확보한다 (`PanelKittingPage`·mobile-002 기준 재사용).
- 390px·Teams narrow에서 page-level horizontal overflow 0, 좌상단 숨김 메뉴 유지, bottom navigation 미추가.
- 권한 부족·조회 전용·중단 차단 상태를 버튼 비활성+설명으로 구분한다.
- stale conflict 시 "최신 내용을 다시 불러와 주세요" 안내와 재조회 action을 함께 제공한다.

## 7. 업무 규칙과 불변조건

- 키팅 완료(`panel_kitting_completions` 존재)가 제조 시작의 서버 측 전제조건이다. 화면 진입 여부와 무관하게 mutation에서 재검증한다.
- panel `workflow_stage`는 `BeforeManufacturing → ManufacturingInProgress → ManufacturingCompleted` 전진-only이며 후퇴 UPDATE를 만들지 않는다. 중단은 stage 후퇴가 아니라 execution의 `Blocked` 상태와 활성 Pending으로 표현한다.
- panel당 활성(미완료) execution은 정확히 1건이다(DB partial unique). 완료된 execution은 불변이다.
- 시작·체크·중단·재개·종료는 append-only event로 남기고 hard delete·덮어쓰기하지 않는다. 정정·삭제 기능을 제공하지 않는다.
- 종료는 모든 단계 체크 완료 + `Blocked` 아님 + expected version 일치일 때만 허용한다.
- 종료 시 execution 완료, panel stage 전진, panel 제조 업무 완료, (마지막 panel이면) project stage event·LQC handoff·참조 알림은 하나의 transaction이거나 전부 rollback한다. 중단 시 execution 전환·stop event·Pending 생성·알림도 동일하다.
- 활성 `ManufacturingStop` Pending이 연결된 execution은 재개 전까지 체크·종료를 차단한다. 재개는 연결 Pending이 `Closed`일 때만 허용한다(아래 12절 권장안 3).
- 동일 operation id 재시도는 저장된 성공 결과를 반환하고, 같은 operation id를 다른 payload에 재사용하면 conflict를 반환한다(`0033` batch replay 계약과 동일한 방식).
- 프로젝트 단위 `ManufacturingWork` StageCompleted event는 마지막 panel 완료 transaction에서 project row lock + 기존 event 확인으로 정확히 1회만 생성한다(`010A-STAGE-RACE` 해법 재사용).
- generic `WorkflowStore.CompleteStageAsync`는 자체 connection/transaction을 열므로 호출하지 않고, LQC skeleton 업무·참조 알림 생성을 종료 transaction 안에 inline으로 구현한다(`WorkflowStore`의 `StageResponsibilities` 상 LQC 담당은 `QualityLQC → QualityLQCSecondary → Quality` fallback — 010A가 제조 담당 resolve에 쓴 fallback 패턴 재사용).
- Pending 생성·배정·상태 전이·댓글은 기존 `PendingStore` 계약(append-only history, expected version, 배정 시 조치 업무·차단 알림)을 재사용하며 새 Pending 상태를 만들지 않는다.
- System Administrator 포함 모든 mutation은 동일한 서버 authorization·검증을 통과한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| panel manufacturing execution | panel당 1건 활성. 상태·시작/종료 actor·시각·version | 신규 (`panel_manufacturing_executions`) | 완료 후 불변, hard delete 금지, purge 시 선행 정리 |
| execution step snapshot | 생성 시점에 고정 4단계를 ordered snapshot으로 복사 | 신규 (`panel_manufacturing_execution_steps`) | 체크 actor·시각 보존, template 참조 없음 |
| execution event | Started/StepChecked/Stopped/Resumed/Completed append-only, operation id unique | 신규 (`panel_manufacturing_events`) | append-only, idempotency 원본 |
| stop–Pending 연결 | stop event와 execution이 `ManufacturingStop` Pending id를 참조 | 신규 컬럼/FK | 활성 중단 1건 불변조건의 근거 |
| panel workflow_stage | 기존 `panel_placeholders.workflow_stage` 전진 | 기존 (0007) | 전진-only, 후퇴 금지 |
| `ManufacturingWork` 내 업무 | 010A가 생성한 panel 업무의 완료 처리 | 기존 (`work_items`) | 기존 idempotency key로 조회·완료 |
| `ManufacturingStop` Pending | panel target·Urgent·기존 상태 모델 | 기존 (0029) | append-only history·version 유지 |
| project workflow event | `ManufacturingWork` StageCompleted 1회 | 기존 (`project_workflow_events`) | 중복 생성 금지 |

```text
execution: (없음) → InProgress → Blocked ⇄(resume) InProgress → Completed
panel.workflow_stage: BeforeManufacturing → ManufacturingInProgress → ManufacturingCompleted (전진-only)
Pending(ManufacturingStop): Registered/ActionRequested → … → Closed (기존 모델 그대로)
```

migration은 latest `0033` 다음의 additive `0034` 1건으로 한정하고, 기존 panel의 시작/완료 추정 backfill은 하지 않는다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 7절 불변조건 전체(키팅 전제, 전진-only, 활성 execution 1건, 체크 완료 검증, transaction 경계, 권한·scope).
- 필요한 조회와 mutation (신규 `Manufacturing` module, `0030`~`0033`의 module 분리 패턴 준수):
  - `GET /api/manufacturing/queue` — 프로젝트별 제조 대상 panel과 execution 상태 (project read scope)
  - `GET /api/manufacturing/panels/{panelId}` — 패널 실행 상세(단계·event 타임라인·활성 Pending)
  - `POST /api/manufacturing/executions/start`
  - `POST /api/manufacturing/executions/{id}/check-step`
  - `POST /api/manufacturing/executions/{id}/stop`
  - `POST /api/manufacturing/executions/{id}/resume`
  - `POST /api/manufacturing/executions/{id}/complete`
- 권한·validation: mutation은 `ManufacturingUpdate` policy + project access scope. 조회는 기존 project read scope. 키팅 미완료·비활성 project/panel·제조 업무 미생성·중복 시작·활성 중단·미완료 체크·stale version을 각각 안정적인 한글 메시지로 구분한다.
- transaction·동시성·idempotency: mutation마다 project/panel/execution row lock → 재검증 → 변경. operation id unique + 저장 결과 replay, expected version conflict. 마지막 panel 판정은 lock 하에서 활성 panel 수와 완료 execution 수를 재계산한다.
- audit trail: event append-only + execution/step의 actor·시각 컬럼. 취소 panel의 열린 제조 업무는 기존 010A 취소 경로가 처리하므로, 011A는 취소 panel의 execution mutation 차단과 purge 선행 정리만 추가한다.
- 외부 provider 영향: 중단 시 기존 인앱 알림 원본과 delivery outbox 기록까지만. Teams/Mail/Activity 실제 provider는 실행하지 않는다(격리 환경 disabled 유지).

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다(위 이름은 제안이며 Codex가 기존 관례에 맞춰 확정한다).

## 10. Frontend 고려사항

- route/component: 신규 view kind `manufacturing`(pathname `/manufacturing/work`, query `project`·`panel`), `ManufacturingPage.tsx` + 전용 API helper·type 파일로 분리(`PanelKittingPage.tsx` 분리 패턴). `App.tsx`에 권한 조건부 전역 `제조` 메뉴 추가.
- loading/empty/error/success: `LoadState` 패턴 재사용. queue empty("제조 대상 패널이 없습니다"), 저장 중, 성공, 차단, stale conflict를 action 인접 표시.
- 공통 Action Feedback: 실패 후 동일 선택 재시도는 같은 operation id 유지, 선택 변경 시 새 operation id(`PanelKittingPage`의 operation receipt 패턴 재사용).
- 접근성: 44px touch target, stepper의 상태를 색+도형+텍스트로 중복 표현, 버튼 비활성 사유 텍스트 제공.
- 390px/mobile/narrow pane: `useAdaptiveLayout`(≤860px) 기준 adaptive 분기, `MobileSheet`로 중단 입력, page-level overflow 0, Teams narrow 확인.
- deep link 정합: 신규 제조 업무 description 링크를 `/manufacturing/work?project=...&panel=...`로 변경(Backend `PanelKittingStore`의 생성 문구 수정). 기존 업무 호환을 위해 내 업무의 `ManufacturingWork` stage 업무 링크를 frontend에서 제조 화면으로 매핑한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 010A panel 업무를 완료 상태로 닫고, 마지막 panel에서 프로젝트 workflow 진행률·현재 단계가 LQC로 이동한다. LQC 업무는 skeleton(업무·알림)만 생성하고 검사 데이터는 만들지 않는다.
- 권한/관리자: 기존 `manufacturing.update`·`Manufacturing.WorkTime.Read` permission과 `ManufacturingUpdate` policy 재사용. 신규 permission 추가는 하지 않는 방향을 우선 검토한다.
- Excel/PDF/첨부: 이번 범위에서 없음(자주순차표 파일·사진 첨부는 제외).
- Teams/Mail: 신규 채널 없음. 중단 Pending의 기존 차단 알림·outbox 계약만 통과한다.
- 삭제·복구/감사: project/panel 취소·purge lifecycle에 신규 테이블 FK 정합 추가(`010A-PURGE-LIFECYCLE` 방식). 완료 이력은 취소 후에도 조회 가능하게 보존한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| 1-A (권장) | 고정 최소 4단계 snapshot(시작 확인·도면/자재 확인·작업 수행·자체 확인)을 execution 생성 시 복사 | 완료 근거와 체크 이력 확보, 현업 회신 후 template 교체 가능 | 상세 자주순차표 미반영(의도된 Deferred) |
| 1-B | 단계 없는 자유 시작/종료만 | 구현 최소 | "완료" 근거 부재, 012A·ADMIN-002 기반 약화 |
| 2-A (권장) | panel execution 1건 + 내부 ordered step, 활성 1건 unique | 현장 추적 단위와 일치, 동시성 경계 단순 | 프로젝트 일괄 입력은 후속 |
| 2-B | 프로젝트 단위 실행 | 입력 횟수 감소 | 패널 추적·중단 표현 불가(Roadmap 데이터 단위 원칙 위반) |
| 3-A (권장) | 같은 execution에 append-only stop/resume event, 활성 중단 Pending 1건, 재개는 Pending `Closed` 후 | 누적 이력·시간 표현 용이, 조치 완료가 재개를 강제해 감사 정합 | Pending 종결 지연 시 현장 대기 발생(차단 아닌 운영 trade-off로 기록) |
| 3-B | 중단마다 새 execution | 실행별 이력 단순 | 반복 중단 시 이력 분절, panel당 1건 불변조건과 충돌 |
| 4-A (권장) | 종료 시 업무 완료·stage 전진·LQC skeleton 업무까지만 원자 처리, LQC 상세 데이터 미생성 | 012A 경계 보존, handoff 가시성 유지 | LQC 업무 link는 임시로 프로젝트 workflow 화면 fallback |
| 4-B | LQC 검사 데이터 선생성 | 012A 선반영 | 미확정 양식 임의 확정 위험(제외 원칙 위반) |
| 5-A (권장) | 권한 조건부 전역 `제조` 메뉴 + 내 업무 deep link가 같은 전용 화면으로 수렴 | 현장 queue 접근성과 업무 흐름 모두 충족, user-flow baseline의 "제조 진입점 예정"과 일치 | 메뉴 1개 증가(010A Change 002의 자재 통합과 달리 제조는 소속 workspace가 없어 신설이 타당) |
| 5-B | 내 업무 deep link 전용 | 메뉴 불변 | 전체 queue 조회 부재, 생산관리 조회 동선 없음 |

5개 선택 모두 인터뷰 standing rule에 따라 권장안을 자동 채택한다. 근거와 trade-off는 위와 같이 보존한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL·synthetic data만 사용하고 Persistent UAT migration·write·runtime handover를 실행하지 않는다.
- migration 필요 여부: additive `0034` 1건. 기존 `0007`~`0033` 수정·번호 재사용 금지, backfill 금지, fresh/existing 모두 검증.
- 외부 발송/실제 데이터 영향: 실제 provider 호출 0. 실제 사용자·고객·프로젝트 원문 미사용.
- runtime 교체 여부: 없음. 대표 repo·GitHub `main`·push·PR·merge 미승인, main merge 승인 `0/3`.
- 추가 사용자 승인 필요 작업: 없음(2차 기획 blocking decision 0이면 fast-track 계약대로 Codex 구현·검증·screenshot·local commit까지 진행). Repository 충돌, secret/개인정보, 키팅 전제·전진-only·Pending 감사 위반이 발견되면 fast-track을 중단하고 blocking decision으로 반환한다.

## 14. 검증 계획

- 최소 테스트: `0034` fresh/existing migration, 시작·체크·종료·중단·재개 happy path integration, 키팅 미완료·중복 시작·미완료 체크 종료·Blocked 중 종료·stale version·scope 밖 접근 거부, operation replay와 다른 payload conflict.
- 영향 영역 회귀: Backend 전체 test(010A 키팅·purge·취소 포함), 마지막 panel 동시 종료의 stage event 단일 생성, Pending 기존 계약 회귀, Frontend 전체 unit·lint·typecheck·build.
- PR/CI: 게시 미승인 — local 검증만. isolated Full-Stack E2E(전용 DB·provider disabled)에 제조 시나리오 spec 추가.
- 사용자 검수: synthetic 데이터 기반 desktop·390px(가능하면 Teams narrow) 페이지별 screenshot 보고. 사용자 직접 검수 완료로 표시하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 5절 필수 항목 전부 구현, 7절 불변조건이 서버에서 강제됨, 감사 record가 append-only로 남음.
- UX: 모바일 한 흐름(시작→체크→종료·중단), desktop 진행 조회, overflow 0, feedback 항목 충족.
- 자동 테스트: 위 14절 통과. 미실행 항목은 이유와 함께 미실행으로 보고한다.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist의 상태·위치 추적(`docs/12-task-completion-policy.md`).
- 사용자 검수 상태: `사용자 검수 대기`로 종료(자동 검증과 분리 관리).
- PR 상태: 없음 — local experiment commit만.
- 중단 조건: 키팅 전제·전진-only·Pending 감사 무결성·secret/개인정보와 충돌하는 구현이 필요해지는 경우, 문서·구현의 의미 있는 충돌 발견, isolated 경계를 벗어나는 검증이 필요한 경우 — 진행을 멈추고 blocking decision으로 보고한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 인터뷰 7절 선택 1~5 (체크리스트 MVP·작업 단위·재개·LQC handoff·진입 구조) | 12절 후보 비교 | standing rule로 권장안 자동 채택 (비차단) |
| 2 | 제조 화면 표시/팝업/저장-only 상세 항목 (Roadmap 추적 7~9) | 현업 회신 후 확정 | 대기 — 후속 회신, 이번 Task는 고정 4단계 snapshot |
| 3 | 제조 완료 시 LQC 자동 요청 기준 상세와 012A 검사 handoff (Roadmap 추적 10) | 012A planning에서 확정 | 대기 — 이번 Task는 skeleton 업무까지만 |
| 4 | 운영 template 관리·version activation | `TASK-ADMIN-002` | 대기 — Deferred |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 신규 `Manufacturing` module(contracts·endpoints·store·DI), `PanelKittingStore`의 신규 업무 deep link 문구, Pending 재사용 helper 연결, project/panel purge·취소 lifecycle 정합.
- Frontend: `ManufacturingPage.tsx`(신규)·전용 API/type 파일, `App.tsx` view/route/menu, 내 업무 링크 매핑, adaptive CSS.
- DB/Migration: `database/migrations/0034_*.sql` (additive).
- Tests/Scripts: Backend integration·migration test, Frontend unit, isolated E2E spec, screenshot 증빙.
- Docs: Roadmap 실험 상태 갱신, Task 5종 산출물. (Roadmap 갱신은 구현 단계에서 Codex가 수행하며 이 planning은 Repository를 수정하지 않는다.)

## 18. Roadmap 연결

- 선행 Task: `TASK-010A`(키팅·제조 업무 생성, 실험 완료), `TASK-007A`(Pending List, 실험 완료), `TASK-006A` workflow skeleton.
- 후속 Task: `TASK-012A`(LQC/OQC/FAT 상세), `TASK-ADMIN-002`(template 관리), `TASK-010A` backlog P3(`010A-CANCEL-LAST-PANEL-STAGE` — 011A에서도 동일 정책 보류 유지).
- 현재 Go/No-Go: canonical 큐는 `TASK-007A` Gate·`Dependency Pending` 유지. 이번 진행은 2026-07-17 실험 재정렬 승인에 따른 `experiment/*` fast-track이며 canonical queue·대표 repo·`main`·Persistent UAT·provider를 변경하지 않는다.
- 별도 Task로 분리할 항목: 프로젝트 단위 일괄 작업 입력, 제조 사진 첨부, QR 스캔 진입, 상세 자주순차표 반영.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | experiment fast-track standing rule로 interview 왕복·중간 승인 생략, 권장안 자동 채택, TASK-011A 즉시 진행 | 인터뷰 `COMPLETED_CONFIRMED` 기준 1차 planning 작성 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

(fast-track 계약상 위 체크는 Codex review와 Fable 2차 기획으로 대체 진행되며, 이 문서 자체는 구현 승인이 아니다.)

## 21. Codex 구현 지시문 초안

1. `0034` additive migration으로 execution·step·event 테이블과 활성 execution partial unique, stop–Pending 연결, purge 정합 FK를 추가한다. 기존 migration을 수정하지 않는다.
2. Manufacturing module을 `0030`~`0033`과 같은 분리 구조(contracts/endpoints/store)로 만들고, 모든 mutation에 project lock → 재검증 → operation replay → expected version 순서를 적용한다.
3. 종료 transaction에서 execution 완료·panel stage 전진·`kitting:panel:{panelId}:manufacturing` 업무 완료·(마지막 panel 시) `ManufacturingWork` StageCompleted 1회·LQC skeleton 업무·참조 알림을 원자 처리한다. `WorkflowStore.CompleteStageAsync`는 호출하지 않는다.
4. 중단 transaction에서 `PendingStore`의 IQC 부적합 helper 패턴을 따라 `ManufacturingStop`·`Urgent`·panel target Pending을 생성·연결하고, 조치 담당자 지정 시 기존 배정 artifacts 경로를 재사용한다.
5. Frontend는 `/manufacturing/work` 전용 페이지(모바일 우선 adaptive)와 권한 조건부 전역 `제조` 메뉴, 내 업무 deep link 매핑을 추가하고, operation receipt 재시도 패턴을 재사용한다.
6. 검증은 14절 계획을 따르고, 미실행 항목은 성공으로 기록하지 않는다. Persistent UAT·실제 provider·게시는 실행하지 않는다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 3
