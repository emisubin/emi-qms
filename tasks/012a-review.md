# TASK-012A — LQC·OQC·고객검수·FAT 1차 기획 Codex 내용 Review

> Review 대상: `tasks/012a-planning.md` Fable 5 원문
>
> Review 성격: 사용자 문제·제품 방향·Roadmap·실제 Repository·구현 경계 1회 검토
> 결과: 2차 Fable 기획 전 필수 보정 — 아래 resolution을 최종 구현 계약에 반영

## 1. 총평

패널 단위 공통 검사 attempt 모델, LQC → 제조완료확인 → OQC → 고객검수 → 선택 FAT의 순차 인계, 불합격·PUNCH와 Pending 재검사, 확정 snapshot/PDF, 모바일 queue → panel 집중 화면을 채택한 방향은 TASK-012A의 사용자 문제와 Roadmap에 맞는다. 실제 고객 양식·필수 사진 기준·template 관리·Packing 세부 기능을 보류한 범위도 미확정 정책을 임의 확정하지 않는 최소안이다.

다만 1차 기획대로 구현하면 `quality.inspect`를 가진 다른 품질 담당자가 stage 배정과 무관하게 판정을 확정할 수 있고, generic Pending 상태 전이로 검사 합격 없이 연결 Pending을 닫거나 generic 내 업무 완료로 성적서를 우회할 수 있다. 실패 판정에 Roadmap이 요구하는 조치 담당 부서가 빠졌고, 다음 단계 담당자가 없을 때 report만 확정되어 handoff가 끊길 위험도 있다. 또한 이름이 IQC로 고정된 template catalog를 네 종류 panel 검사에 재사용하면 Material IQC와 panel quality의 lifecycle이 결합된다. 아래 P1·P2 resolution을 2차 기획에 반영하면 실험 구현이 가능하다.

## 2. 기능 판단

### 유지

- panel + stage별 active attempt 1건, 재검사는 새 attempt로 누적하고 finalized report는 수정하지 않는 모델
- LQC 합격 뒤 별도 제조완료확인, 이후 OQC·고객검수·선택 FAT의 panel 단위 즉시 handoff
- LQC/OQC 불합격은 Panel `Nonconformance`, 고객검수/FAT 지적은 Panel `Punch` Pending으로 연결
- Pending `ReinspectionRequested`에서만 새 attempt를 열고 합격 transaction만 연결 Pending을 종결
- 일반 양식 v1, optional 사진, attempt-local item snapshot, 확정 PDF artifact
- 마지막 active panel 합격에서만 project stage event exactly-once, panel 업무는 즉시 다음 단계로 인계
- 전역 `품질` workspace 안의 IQC·LQC·OQC·고객검수·FAT 진입, 모바일 행동 중심·desktop 분할 화면
- 실제 고객 양식, 사진 필수 정책, template 관리자, 외부 provider, Persistent UAT, TASK-013A Packing 상세 제외

### 추가

1. **stage 담당자 기반 mutation 권한**
   - `quality.inspect`와 project scope만으로 판정을 허용하지 않는다. 해당 stage의 정·부 책임자로 프로젝트에 배정되었거나 현재 panel stage work item의 active assignee인 사용자만 검사 mutation을 수행한다.
   - LQC는 `QualityLQC/Secondary`, OQC는 `QualityOQC/Secondary`, 고객검수·FAT는 `QualityCustomerInspection/Secondary`를 사용한다. System Administrator와 다른 품질 단계 담당자의 우회를 허용하지 않는다.
   - 제조완료확인은 `manufacturing.update` + `ManufacturingPrimary/Secondary` 또는 해당 stage 업무 assignee 조건을 같은 방식으로 적용한다.

2. **generic 내 업무 우회 차단**
   - `target_type='Panel'`이며 stage가 `LQC`, `ManufacturingCompleted`, `OQC`, `CustomerInspection`, `FAT`인 업무는 generic `/api/my-work/{id}/start|complete|cancel`에서 conflict로 차단한다.
   - 내 업무 UI에는 generic 완료 버튼을 숨기고 품질 또는 제조 domain 화면으로 이동하는 action만 제공한다. 검사 시작·확정·제조완료확인은 domain transaction만 업무 status를 전이한다.

3. **불합격·PUNCH의 조치 담당 부서**
   - 실패 확정 request는 `actionDepartmentCode`를 필수로 받고, 선택한 부서의 active `Pending.Manage` 사용자만 optional assignee 후보가 된다. “귀책부서”가 아니라 “조치 담당 부서”로 표시한다.
   - 검사 report, attempt, Panel target Pending, history, assignment work item과 blocking 알림을 같은 connection/transaction에서 생성한다. public `PendingStore.CreateAsync`의 별도 Project transaction을 호출하지 않는다.
   - stage별 active linked Pending은 1건만 허용하고, 재시도·동시 제출에서 중복 생성하지 않는다.

4. **재검사와 Pending 종결 우회 차단**
   - 검사 실패에 연결된 Panel Pending이 `ReinspectionRequested`이면 generic Pending transition의 `Closed`를 conflict로 차단한다.
   - 재검사 attempt는 연결 Pending `ReinspectionRequested` + 이전 attempt `Failed`를 row lock 뒤 확인해야 생성할 수 있다. 새 attempt 진행 중에도 Pending은 열린 상태로 둔다.
   - 모든 필수 항목을 충족한 재검사 합격 transaction만 report finalize·Pending Closed·history·work item·다음 stage handoff를 원자 처리한다.

5. **handoff 실패 시 전체 rollback**
   - 다음 stage 담당자를 찾지 못하면 report 합격, 제조완료확인 또는 FAT skip을 확정하지 않는다. 사용자가 담당자를 지정한 뒤 다시 시도할 수 있는 conflict를 반환한다.
   - 고객검수 뒤 FAT가 필요한 프로젝트는 FAT 업무, 필요하지 않은 프로젝트는 TASK-013A의 상세 데이터 없이 표준 `PackingCompleted` skeleton 업무만 생성한다. 물류 담당자 해석 실패도 전체 rollback한다.

6. **project 집계 source와 선택 FAT 분기**
   - coarse `panel_placeholders.workflow_stage`만으로 검사 완료를 추정하지 않는다. panel + stage의 terminal Passed attempt와 제조완료확인 record를 집계 source로 사용한다.
   - 취소되지 않은 active panel만 포함하고 project row lock 아래 마지막 panel을 판정한다. stage별 project event idempotency key로 exactly-once를 보장한다.
   - FAT가 필요 없는 프로젝트는 고객검수 합격에서 Packing skeleton으로 바로 인계하고 FAT 완료 event를 거짓으로 만들지 않는다. FAT required 여부는 기존 project snapshot을 사용한다.

7. **제조완료확인의 독립 경계**
   - LQC 합격은 panel stage 11 `ManufacturingCompleted` 업무를 만든다. 제조 담당자의 compact 확인은 별도 immutable confirmation record와 operation receipt로 남기고 TASK-011A execution·step·event를 수정하거나 재개방하지 않는다.
   - 확인 성공 transaction만 stage 11 업무 완료·panel stage 전진·OQC 업무 생성을 수행한다. 품질 workspace에는 읽기 상태만, 제조 workspace에는 실행 action을 배치한다.

8. **panel quality 전용 template catalog**
   - 공통 attempt/report 모델은 유지하되 `iqc_report_template*`를 panel 검사에 재사용하지 않는다. 이름·수명주기가 Material IQC에 고정되어 있어 ADMIN-002와 project purge 경계를 불필요하게 결합한다.
   - `panel_quality_template_versions/items`처럼 stage code를 가진 읽기 전용 seed catalog를 추가하고 report item snapshot이 그 version을 참조한다. 이번 Task에는 template mutation UI/API를 만들지 않는다.

9. **확정 검증과 최소 양식**
   - 공통 판정은 `Passed|Failed`로 저장한다. 고객검수/FAT의 실패 UI는 `PUNCH 발생`으로 표시하고 연결 issue type만 `Punch`로 결정한다.
   - 합격은 모든 required item이 `Pass|NotApplicable`이고 NA에는 사유가 있어야 한다. 불합격은 하나 이상의 `Fail`, 3~1000자 총평, 조치 담당 부서가 필요하다.
   - v1 seed 항목은 LQC(도면·작업기준, 조립, 배선·체결, 표시·마감), OQC(외관·표시, 기능·회로, 구성·치수, 출하준비), 고객검수(검사범위, 지적/PUNCH, 고객 확인 메모), FAT(시험범위, 시험결과, 고객 확인, PUNCH)로 제한한다. 추가 메모와 optional 사진은 허용한다.

10. **operation replay·privacy-safe snapshot**
    - 모든 mutation은 operation ID, action, panel/stage/attempt identity, bounded payload fingerprint와 최소 성공 projection을 보존한다. 같은 payload는 replay, operation 재사용·다른 payload는 conflict다.
    - receipt에 자유 서술·사진 metadata·전체 report snapshot·고객 원문을 복제하지 않는다. report와 PDF에는 업무상 필요한 bounded field만 담고 검증 증빙은 원문을 출력하지 않는다.

11. **취소·영구 purge·불변성**
    - open attempt/work item이 있는 panel 또는 project 취소는 해당 open item을 Cancelled로 정합시키되 finalized report·confirmation·Pending history는 soft-delete lifecycle에서 보존한다.
    - approved permanent project purge는 신규 report child/photo/artifact/attempt/confirmation/receipt를 FK 순서로 정리할 수 있어야 하며 migration integration test로 보장한다. 정상 API에는 finalized report 삭제·수정 경로를 제공하지 않는다.
    - 기존 TASK-009A IQC purge의 범위 밖 결함이 발견되면 이번 migration에서 묵시적으로 바꾸지 말고 별도 Finding으로 기록한다.

12. **명확한 route·모바일 구성**
    - 전역 menu label은 `품질`, 기본 route는 `/quality/inspections`; 기존 `/quality/iqc`는 호환 유지하고 품질 workspace의 IQC tab에서 이동한다.
    - panel 검사 deep link는 `/quality/inspections?stage=...&project=...&panel=...`, 제조완료확인은 `/manufacturing/work?project=...&panel=...`을 사용한다.
    - 390px는 상단 compact queue/filter → 선택 panel 핵심 맥락 → 항목 one-column → 사진/메모 → sticky가 아닌 in-flow 판정 action 순서로 재구성한다. 하단 고정 메뉴는 추가하지 않고 기존 좌상단 숨김 메뉴를 유지한다.

### 보류

- 실제 고객·프로젝트별 양식, template 편집·활성화·migration UI
- stage별 사진 필수 개수·구도·용량 차등 정책과 object storage
- 전자서명, 고객 포털, 외부 공유·실제 notification provider
- 검사 정정·관리자 강제 합격·report 삭제와 stage 후퇴
- TASK-013A Packing record·shipment·logistics 세부 기능
- Persistent UAT migration/runtime, 대표 repo, push, PR, merge

### 제거

- `quality.inspect` 보유자 전체가 project의 모든 품질 stage를 확정하는 방식
- generic 내 업무 완료로 panel 검사·제조완료확인을 우회하는 방식
- generic Pending `Closed`로 검사 합격 없이 품질 Pending을 종결하는 방식
- 실패 판정에서 조치 담당 부서 없이 사용자 assignee만 저장하는 방식
- 다음 stage 담당자가 없어도 report만 finalize하는 방식
- Material IQC 전용 `iqc_report_template*`를 panel 품질검사 catalog로 재사용하는 방식
- FAT 미대상 프로젝트에 거짓 FAT 완료 event를 만드는 방식
- TASK-011A 제조 execution을 stage 11 확인 record로 재사용·수정하는 방식

## 3. 권장 상태·transaction 계약

```text
LQC work Requested
  --start--> attempt InProgress + LQC work InProgress
  --finalize Passed--> report Finalized + LQC work Completed
                       + ManufacturingCompleted confirmation work Requested
  --finalize Failed--> report Finalized + LQC work Completed
                       + Panel Nonconformance Pending

ManufacturingCompleted work Requested
  --confirm--> immutable confirmation + work Completed + OQC work Requested

OQC / CustomerInspection / FAT:
  --Passed--> report Finalized + current work Completed + next stage work Requested
  --Failed--> report Finalized + current work Completed + Panel Nonconformance|Punch Pending

linked Pending ReinspectionRequested + previous Failed
  --request reinspection--> new attempt + new stage work Requested; Pending stays open
  --reinspection Passed--> report Finalized + Pending Closed + next stage handoff atomically

CustomerInspection Passed:
  FAT required  -> FAT work
  FAT not required -> PackingCompleted skeleton work (no FAT completion event)
```

- 각 mutation은 project/panel/current work/attempt/Pending row lock, scope·stage 담당·expected version·operation fingerprint를 재검증한 뒤 하나의 transaction으로 처리한다.
- report가 finalized되면 item response·photo metadata·PDF artifact를 수정하지 않는다. PDF 생성 실패는 확정 결과를 거짓 rollback하지 않고 bounded `PdfPending|PdfReady|PdfFailed` artifact 상태와 재시도 경계를 둔다.

## 4. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `012A-STAGE-AUTHORIZATION` | P1 | `RESOLVED_FOR_REDRAFT` | permission만으로 다른 품질 stage 판정 우회 가능 | stage 정·부 배정 또는 current work assignee + permission + scope 검증 |
| `012A-DIRECT-WORK-BYPASS` | P1 | `RESOLVED_FOR_REDRAFT` | generic 내 업무가 report·confirmation 없이 완료 가능 | panel 품질/확인 stage generic transition 차단, domain API만 전이 |
| `012A-PENDING-ACTION-OWNER` | P1 | `RESOLVED_FOR_REDRAFT` | 실패 확정에 조치 담당 부서·원자 Pending artifact 계약 누락 | actionDepartmentCode 필수, 같은 transaction의 Panel Pending/history/work/notification |
| `012A-REINSPECTION-CLOSE-BYPASS` | P1 | `RESOLVED_FOR_REDRAFT` | generic Pending Closed가 합격 없이 품질 문제를 종결 가능 | linked quality Pending generic close 차단, 성공 재검사만 종결 |
| `012A-HANDOFF-ROLLBACK` | P1 | `RESOLVED_FOR_REDRAFT` | assignee 부재 시 finalized report와 다음 업무가 분리될 수 있음 | 다음 담당자 해석 실패 시 report/confirm/skip 전체 rollback |
| `012A-PROJECT-STAGE-SOURCE` | P2 | `RESOLVED_FOR_REDRAFT` | coarse panel stage만으로 last-panel 판정 시 재검사·FAT 분기 오류 | terminal attempt/confirmation 집계 + project lock + stage event idempotency |
| `012A-MANUFACTURING-CONFIRM-BOUNDARY` | P2 | `RESOLVED_FOR_REDRAFT` | stage 11이 기존 제조 execution과 섞이면 완료 이력 변조 | 별도 immutable confirmation, TASK-011A record 불변 |
| `012A-TEMPLATE-BOUNDARY` | P2 | `RESOLVED_FOR_REDRAFT` | IQC 이름의 catalog 재사용이 서로 다른 lifecycle을 결합 | panel quality 전용 stage catalog와 attempt-local snapshot |
| `012A-REPLAY-PRIVACY` | P2 | `RESOLVED_FOR_REDRAFT` | 성공 replay snapshot이 고객·검사 원문을 중복 보존할 위험 | bounded fingerprint/result projection, 자유 서술·사진 원문 미복제 |
| `012A-PURGE-IMMUTABILITY` | P2 | `RESOLVED_FOR_REDRAFT` | finalized 불변 trigger와 approved permanent purge가 충돌 가능 | 정상 mutation 불변 + 명시 purge 경로·FK 순서 migration test |
| `012A-FAT-PACKING-BOUNDARY` | P3 | `RESOLVED_FOR_REDRAFT` | FAT 미대상 분기에서 TASK-013A 범위를 침범할 수 있음 | 표준 Packing skeleton만 생성, 세부 데이터 제외 |

Review 기준 Open P0/P1/P2는 `0/0/0`이다. 이는 Fable 2차 기획이 위 resolution을 반영한다는 조건부 판정이며 아직 코드 구현 완료 판정이 아니다.

## 5. 자동 채택할 비차단 결정

| 항목 | 채택안 | 근거 |
| --- | --- | --- |
| 검사 데이터 | panel + stage + attempt + immutable report | 재검사 누적과 단계별 감사·동시성 경계가 명확함 |
| template | panel quality 전용 v1 seed + report snapshot | IQC lifecycle 분리, 실제 양식 미확정 상태에서 교체 가능 |
| 실패 연결 | LQC/OQC Nonconformance, 고객/FAT Punch + 조치 담당 부서 | Roadmap 용어와 실제 후속 행동 일치 |
| 제조확인 | 별도 stage 11 immutable confirmation | 제조 실행 불변성을 보존하고 OQC 책임을 분리 |
| FAT 분기 | project FAT flag snapshot, 미대상은 바로 Packing skeleton | 선택 단계와 거짓 완료 event 방지 |
| 사진 | 단계 공통 optional·bounded metadata | 증빙 확장 가능성을 두되 미확정 필수 정책은 만들지 않음 |
| 진입 | 전역 `품질` + IQC/검사 tab + stage filter | 별도 stage menu 난립 없이 현장 queue를 통합 |
| 모바일 | queue→panel→items→evidence→judgment | PC 정보 전체를 축소하지 않고 현장 핵심 행동에 집중 |

## 6. 2차 기획이 고정할 최소 구현 계약

1. additive `0035`는 panel quality template/attempt/item response/photo/report/PDF/operation receipt, manufacturing confirmation, Pending link와 constraint/index를 추가하고 기존 migration을 수정하지 않는다.
2. stage별 담당자·scope·permission·current work를 transaction 안에서 검증하고 generic work/Pending close 우회를 차단한다.
3. 실패 finalize는 조치 담당 부서와 Panel Pending/history/assignment artifacts를 report와 같은 transaction에서 생성한다.
4. 재검사는 linked Pending `ReinspectionRequested`에서 새 attempt/work를 열고, 합격 transaction만 Pending을 닫고 다음 stage로 인계한다.
5. LQC pass → 제조완료확인 → OQC → 고객검수 → 선택 FAT/바로 Packing skeleton의 panel별 handoff를 담당자 실패 rollback과 idempotency로 구현한다.
6. finalized report·confirmation은 정상 API에서 불변이며 approved permanent purge와 cancellation 회귀를 검증한다.
7. 공통 PDF는 기존 009A font/license 자산을 재사용하되 report model을 복제하지 않고 panel quality 전용 renderer로 bounded snapshot을 출력한다.
8. Frontend는 `/quality/inspections` adaptive workspace와 제조완료확인 card, 품질 menu/deep link/generic action guard를 구현한다.
9. migration fresh/existing, stage role/scope, replay/stale/concurrency, pass/fail/reinspection, Pending close guard, FAT skip, last-panel event, cancellation/purge와 009A/011A 회귀를 검증한다.

## 7. 권장 구현 순서

1. `0035` schema·seed·constraint·purge migration tests
2. generic work/Pending close guards와 shared transaction-safe Pending helper
3. Quality Inspection store의 attempt/item/photo/finalize/reinspection/PDF
4. manufacturing confirmation·stage별 assignee handoff·last-panel event/FAT skip
5. `/quality/inspections` API·Frontend adaptive workspace·global quality menu/deep links
6. Backend targeted/전체, Frontend unit/lint/build, isolated Full-Stack E2E
7. desktop·390px 페이지별 screenshot, 종료 산출물·Roadmap experiment 상태·local commit

## 8. 판정

위 resolution을 반영한 Fable 2차 기획을 구현 source of truth로 사용하면 실험 branch 구현은 `GO`다. 이 판정은 대표 repo, push, PR, merge 또는 Persistent UAT 승인이 아니다.
