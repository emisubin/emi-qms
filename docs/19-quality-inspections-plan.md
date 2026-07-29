# TASK-012A — LQC·제조완료확인·OQC·전진검수·FAT 후속 품질 검사 2차 기획 (구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-012A`
- authoringModel: `FABLE_5`

이 문서는 `tasks/012a-interview.md`(확인 완료 interview), `tasks/012a-planning.md`(Fable 1차 기획 원문), `tasks/012a-review.md`(Codex 내용 review), `tasks/012a-change-001.md`(2차 기획 target 승인)를 모두 다시 읽고 작성한 TASK-012A의 authoritative implementation contract다. 1차 기획의 유지 판정 내용은 보존하고, review의 추가·보류·제거 권고와 Finding resolution 11건을 모두 반영했다. 1차 기획과 review 원문은 수정하지 않고 판단 이력으로 보존한다. 이 문서는 experiment branch 구현 계약이며 대표 repo·`main` merge·push·PR·Persistent UAT·실제 provider 승인을 부여하지 않는다.

## 1. 한 줄 목표

품질 담당자가 제조 완료된 panel을 모바일에서 열어 자신이 배정된 stage의 LQC·OQC·전진검수·선택 FAT 성적서를 작성·판정하고, 부적합/PUNCH를 조치 담당 부서가 지정된 Panel Pending 재검사로 연결하며, 합격 panel을 제조완료확인을 거쳐 다음 18단계 업무로 정확히 한 번 인계할 수 있다.

## 2. 확정 기준선과 해결할 업무 문제

- 제조 실행 완료(TASK-011A)는 panel target `LQC` 업무를 `manufacturing:panel:{panelId}:lqc` key로 exactly-once 생성하지만, 검사 record·전용 화면·판정·PUNCH·재검사·다음 업무 handoff가 없는 skeleton이다.
- OQC·전진검수·FAT는 workflow stage 코드(`OQC`, `CustomerInspection`, `FAT`)와 담당 구조(`QualityOQC`, `QualityCustomerInspection` 계열)만 존재한다.
- 18단계 순서 `LQC(10) → 제조 완료(11) → OQC(12) → 전진검수(13) → 선택 FAT(14) → 포장(15)`, `projects.fat_required` 기반 FAT optional, panel coarse stage 전진-only, finalized 증빙 append-only, 부적합/PUNCH의 Pending 재검사 감사는 Roadmap·interview에서 확정된 불변조건이다.
- 재사용 확정 자산: TASK-009A의 bounded 사진·canonical snapshot·저장 PDF·불변 trigger 계약(구조 패턴), TASK-011A의 operation receipt·transaction 동기화·generic 업무 차단·Panel Pending(조치 부서 포함) 패턴, 기존 Pending 상태기계(`Registered → ActionRequested → InProgress → ReinspectionRequested → Closed`), work item idempotency, project access scope와 서버 Policy.
- interview 7장 비차단 결정 6건과 review 5장 자동 채택 8건은 사용자 standing experiment rule에 따라 채택된 상태다. 미확정 실제 양식·필수 사진 위치는 이 Task에서 확정하지 않는다.

## 3. 범위

### 포함

1. panel+stage 공통 quality inspection attempt 모델(stage `LQC|OQC|CustomerInspection|FAT`)과 재검사 attempt 누적, finalized report 불변
2. panel quality 전용 stage template catalog v1 seed와 attempt-local item snapshot 참조 (Material IQC catalog와 분리)
3. stage별 성적서 작성: 체크/값 응답, optional bounded 사진, 최종화 시 canonical snapshot·hash·저장 PDF
4. stage 정·부 담당 또는 current work assignee 기반 mutation 권한 (permission·scope와 AND 결합)
5. 실패/PUNCH 최종화의 필수 조치 담당 부서와 Panel `Nonconformance`/`Punch` Pending·history·assignment·blocking 알림의 원자 생성
6. linked quality Pending의 generic `Closed` 차단과 재검사 합격 transaction만의 종결
7. LQC 합격 → 독립 immutable 제조완료확인(stage 11) → OQC → 전진검수 → 선택 FAT 또는 표준 `PackingCompleted` skeleton의 panel별 즉시 handoff, 담당자 해석 실패 시 전체 rollback
8. terminal Passed attempt·confirmation 집계와 project row lock 기반 last-panel stage event exactly-once, FAT 비대상 프로젝트의 거짓 FAT event 금지
9. panel 품질·확인 stage 업무의 generic `/api/my-work` start/complete/cancel 차단과 domain 화면 안내
10. operation receipt(fingerprint·bounded projection) replay, expected version, row lock 동시성 계약
11. 전역 `품질` workspace(`/quality/inspections`, 기존 `/quality/iqc` 호환)와 모바일 행동 중심 adaptive 화면, 제조 workspace의 완료 확인 card
12. panel/project 취소·approved permanent purge 정합과 additive `0035` migration, isolated 자동 검증·페이지별 desktop/390px screenshot

### 명시적 제외 (1차 기획·review 유지)

- 실제 현업 LQC/OQC/전진검수/FAT 상세 양식·사진 필수 위치·개수·구도 확정, template 편집·활성화 UI/API(TASK-ADMIN-002)
- 실제 고객 PDF 양식·전자서명·고객 포털·외부 공유·Excel export
- 완료 성적서 수정·삭제·재발행·관리자 강제 합격·stage 후퇴·검사 정정
- PUNCH 항목별 독립 Pending 개별 종결 관리 고도화(실패 최종화 1건당 aggregated Pending 1건, 상세는 성적서 응답·사유에 기록)
- object storage·CDN·image transcoding, 신규 외부 알림 채널·실제 provider delivery
- TASK-013A Packing record·shipment·물류 세부, 영업 정산
- TASK-011A manufacturing execution·step·event의 수정·재개방·재사용
- TASK-009A IQC 계약의 범위 밖 수정(결함 발견 시 별도 Finding으로만 기록)
- Persistent UAT migration·runtime handover, 대표 repo·`main`·push·PR·merge

### 제거 확정 (review 제거 판정의 계약화)

- `quality.inspect` 보유만으로 모든 품질 stage를 판정하는 방식
- generic 내 업무 완료·generic Pending `Closed`로 검사·확인·재검사를 우회하는 방식
- 조치 담당 부서 없는 실패 판정, 다음 담당자 부재 상태의 report 단독 finalize
- `iqc_report_template*`의 panel 검사 재사용, FAT 비대상 프로젝트의 FAT 완료 event

## 4. 역할·권한 계약

모든 검증은 서버 transaction 안에서 수행하며 UI 숨김으로 대체하지 않는다.

| 행동 | 필요 permission | 추가 필수 조건 (AND) |
| --- | --- | --- |
| 검사 시작·응답·사진·finalize·재검사 시작·PDF retry | `quality.inspect` + project access scope | 해당 stage의 정·부 책임자로 project에 배정(LQC=`QualityLQC/Secondary`, OQC=`QualityOQC/Secondary`, 전진검수·FAT=`QualityCustomerInspection/Secondary`) **또는** 해당 panel stage work item의 현재 active assignee |
| 제조완료확인 | `manufacturing.update` + project access scope | `ManufacturingPrimary/Secondary` 배정 또는 해당 확인 업무의 active assignee |
| 실패 판정의 optional 조치 담당자 지정 | 위 mutation 권한 | 지정 사용자는 선택한 조치 담당 부서 소속의 active `Pending.Manage` 보유자 (0034 검증 패턴 재사용) |
| Pending 조치·상태 전이·댓글 | 기존 `Pending.Manage` 계약 | 품질 연결 Pending의 `Closed` 전이는 generic 경로에서 conflict |
| queue·상세·finalized 증빙·사진·PDF 조회 | 기존 project access scope (+ 품질 workspace 접근) | 검사 mutation 불가 역할은 조회 전용 |
| System Administrator | 기존 관리자 정책 | stage 담당 조건을 우회하지 않음. 이력·조회 중심 |

다른 품질 stage 담당자는 자신의 stage가 아닌 검사 mutation을 수행할 수 없다(`012A-STAGE-AUTHORIZATION` resolution).

## 5. 데이터·상태 모델 계약 (`0035` additive migration)

기존 migration은 수정하지 않고 번호를 재사용하지 않으며, 기존 panel에 attempt/report를 backfill하지 않는다. 최종 물리 이름의 기계적 세부는 구현에서 조정할 수 있으나 아래 계약(관계·제약·불변성)은 고정이다.

### 5.1 panel quality template catalog (Material IQC와 분리)

- `panel_quality_template_versions`: stage code별 version, stage당 active version 1건(partial unique). 이번 Task는 읽기 전용 system seed만 두고 mutation UI/API를 만들지 않는다.
- `panel_quality_template_items`: version별 item_code·display_order·label·guidance·response_type(`Check|Text`)·is_required·requires_photo(전 항목 `false`)·max_text_length. 제약은 0032 item 계약과 동등 수준.
- v1 seed 항목(전 항목 사진 선택, stage당 마지막 항목은 optional `Text` 메모):
  - LQC: 도면·작업기준 일치 / 조립 상태 / 배선·체결 / 표시·마감 + 추가 메모
  - OQC: 외관·표시 / 기능·회로 / 구성·치수 / 출하 준비 + 추가 메모
  - 전진검수(`CustomerInspection`): 검사 범위 확인 / 지적·PUNCH 여부 / 고객 확인 메모(Text)
  - FAT: 시험 범위 / 시험 결과 / 고객 확인 / PUNCH 여부 + 추가 메모

### 5.2 attempt·report·증빙

- `panel_quality_inspection_attempts`: project·panel·stage_code·attempt_number·status(`Requested|InProgress|Passed|Failed|Cancelled`)·linked pending_issue_id·현재 stage work item 연결·version·actor/시각.
  - partial unique: panel+stage당 active(`Requested|InProgress`) attempt 최대 1건. unique: (panel, stage, attempt_number).
  - `Failed`는 연결 open Pending과 1:1이며 종결 전까지 stage 차단을 의미한다. `Cancelled`는 panel/project 취소의 terminal 상태다.
- `panel_quality_reports`: attempt당 1건(unique), template_version 참조, `Draft → Finalized`, result `Passed|Failed`, 사유, snapshot_text·snapshot_sha256, pdf_status(`Pending|Ready|Failed`)·오류 코드·시각, 생성·확정 actor. 0032의 lifecycle check(Draft에는 확정 필드 전부 null, Finalized에는 전부 필수) 계약을 동등하게 적용한다.
- `panel_quality_report_responses`·`panel_quality_report_photos`: 0032 계약 재사용 — `Pass|Fail|NotApplicable`/Text 응답, JPEG/PNG ≤5MB·report당 최대 5장·alt text, draft에서만 등록/삭제.
- `panel_quality_report_pdf_artifacts`: snapshot hash 연결 immutable artifact(UPDATE/DELETE 차단 trigger).
- guard trigger: finalized report의 core field·responses·photos 불변, PDF artifact 불변 — 0032 trigger 패턴을 신규 table에 복제한다. 정상 API에는 finalized 삭제·수정 경로가 없다.
- 고객검수/FAT의 UI 실패 표기는 `PUNCH 발생`이되 저장 result는 공통 `Failed`, 연결 Pending issue type만 `Punch`다.

### 5.3 제조완료확인 record

- `panel_manufacturing_completion_confirmations`: project·panel(unique)·근거 LQC 합격 attempt 참조·확인 actor/시각·확인 업무 연결. immutable(수정·삭제 없음). TASK-011A `panel_manufacturing_executions`·steps·events는 읽기만 하고 수정·재개방하지 않는다(`012A-MANUFACTURING-CONFIRM-BOUNDARY`).

### 5.4 operation receipt·Pending·purge

- `panel_quality_operations`: operation_id(pk)·action(`Start|SaveResponses|AddPhoto|RemovePhoto|Finalize|RequestReinspection|ConfirmManufacturingCompleted|RetryPdf`)·panel/stage/attempt identity·payload_fingerprint(sha256)·bounded 성공 projection(jsonb). 자유 서술·사진 metadata·전체 snapshot·고객 원문을 receipt에 복제하지 않는다(`012A-REPLAY-PRIVACY`).
- Pending은 기존 `pending_issues` 재사용: target_type `Panel`, issue_type `Nonconformance`(LQC/OQC)·`Punch`(전진검수/FAT), priority `Urgent`, `action_department_code`(0034 컬럼) 필수 — 품질 실패 경로의 필수화는 서버 계약으로 강제하고 기존 IQC/제조 row와 충돌하는 DB check 변경은 하지 않는다.
- approved permanent project purge는 신규 table을 FK 역순(operations → pdf artifacts → photos → responses → reports → attempts → confirmations)으로 정리하고 migration integration test로 보장한다(`012A-PURGE-IMMUTABILITY`).

### 5.5 상태 흐름 (canonical)

```text
LQC work Requested
  --검사 시작--> attempt InProgress + LQC work InProgress
  --finalize Passed--> report Finalized + attempt Passed + LQC work Completed
                       + 제조완료확인 work Requested (exactly-once)
  --finalize Failed--> report Finalized + attempt Failed + LQC work Completed
                       + Panel Nonconformance Pending(조치 담당 부서 필수)

제조완료확인 work Requested
  --confirm--> immutable confirmation + work Completed + OQC work Requested

OQC / CustomerInspection / FAT:
  --Passed--> report Finalized + 현재 work Completed + 다음 stage work Requested
  --Failed--> report Finalized + 현재 work Completed + Panel Nonconformance|Punch Pending

linked Pending ReinspectionRequested + 직전 attempt Failed (row lock 검증)
  --재검사 시작--> 새 attempt Requested→InProgress + 새 stage work; Pending은 open 유지
  --재검사 Passed--> report Finalized + Pending Closed + 다음 stage handoff (원자)

CustomerInspection Passed:
  fat_required=true  -> FAT work Requested
  fat_required=false -> PackingCompleted skeleton work Requested (FAT event 미생성)

panel coarse stage(전진-only): ManufacturingCompleted --첫 검사 시작--> InspectionInProgress
  --전진검수 합격(FAT 불요) 또는 FAT 합격--> InspectionCompleted
```

## 6. Transaction·불변조건 계약

1. **원자성**: 각 mutation은 project/panel/현재 work/attempt/연결 Pending row lock, project scope·stage 담당·permission·expected version·operation fingerprint를 재검증한 뒤 하나의 transaction으로 report finalize·attempt 전이·panel coarse stage·업무 완료·Pending 생성/종결·다음 업무·project event를 처리한다. PDF rendering만 판정 뒤 derived 상태(`Pending|Ready|Failed`+재시도)로 분리하고, PDF 실패는 판정을 되돌리지 않는다.
2. **판정 규칙**: 합격은 모든 required item이 `Pass|NotApplicable`이고 `NotApplicable`에는 항목 사유가 있어야 한다. 불합격은 하나 이상의 `Fail`, 3~1000자 총평, 조치 담당 부서가 필수다(`012A-PENDING-ACTION-OWNER`). 사유·오류는 항목 인접 표시 가능한 한글 field error로 반환한다.
3. **실패 artifact 원자 생성**: 실패 finalize는 같은 connection/transaction에서 Panel Pending·history·(지정 시) assignment·blocking 알림 artifact를 생성한다. 별도 transaction의 public `PendingStore.CreateAsync`를 호출하지 않는다(0034 제조 중단 helper 패턴 재사용). stage별 active linked Pending은 1건만 허용하고 재시도·동시 제출에서 중복 생성하지 않는다.
4. **handoff 실패 전체 rollback**: 다음 stage 담당자(제조·OQC·전진검수/FAT·물류)를 해석하지 못하면 report 합격·제조완료확인·FAT skip을 확정하지 않고 담당자 지정 후 재시도를 안내하는 conflict를 반환한다(`012A-HANDOFF-ROLLBACK`). 담당자 해석은 011A `ResolveQualityAssigneeAsync`의 responsibility+permission fallback 패턴을 stage responsibility별로 일반화한다.
5. **재검사 gate**: 새 attempt는 연결 Pending `ReinspectionRequested` + 직전 attempt `Failed`를 row lock 뒤 확인해야 생성된다. 재검사 진행 중 Pending은 open을 유지하고, 재검사 합격 transaction만 Pending `Closed`·history·다음 handoff를 원자 처리한다. 품질 연결 Pending의 generic `Closed` 전이는 conflict로 차단한다(`012A-REINSPECTION-CLOSE-BYPASS`).
6. **generic 우회 차단**: `target_type='Panel'`이고 stage가 `LQC|ManufacturingCompleted|OQC|CustomerInspection|FAT`인 업무는 generic `/api/my-work/{id}/start|complete|cancel`에서 conflict를 반환하고 품질/제조 화면으로 안내한다. domain transaction만 업무 status를 전이한다(`012A-DIRECT-WORK-BYPASS`).
7. **project stage event**: coarse panel stage가 아니라 stage별 terminal `Passed` attempt와 confirmation record를 집계 source로 사용한다. 취소되지 않은 active panel만 포함하고 project row lock 아래 마지막 panel을 판정하며 project+stage idempotency key로 exactly-once를 보장한다. FAT 비대상 프로젝트는 전진검수 합격에서 Packing skeleton으로 바로 인계하고 FAT event를 만들지 않는다(`012A-PROJECT-STAGE-SOURCE`, `012A-FAT-PACKING-BOUNDARY`). FAT 필요 여부는 기존 project snapshot(`fat_required`)을 사용한다.
8. **idempotency key 계약**: 다음 업무·event ensure는 안정 key를 사용한다 — 제조완료확인 `quality:panel:{panelId}:manufacturing-completed`, OQC 이후 stage 업무 `quality:panel:{panelId}:{stage}:attempt:{n}`, Packing skeleton `quality:panel:{panelId}:packing`, project stage event `quality:project:{projectId}:stage:{stage}`. attempt 1의 LQC 업무는 기존 `manufacturing:panel:{panelId}:lqc` 업무를 그대로 전이한다.
9. **취소 정합**: panel/project 취소는 open attempt를 `Cancelled` terminal로 정리하고 open 품질/확인 업무를 기존 취소 경로로 정합시키되 finalized report·confirmation·Pending history는 보존한다(011A 패턴 준용).
10. **재시도 계약**: 같은 operation id+같은 payload는 저장된 성공 projection replay, 같은 id+다른 payload는 conflict다. stale expected version은 최신 재조회를 안내한다.

## 7. API 계약 (경로는 기존 `/api` 컨벤션, 최종 세부는 구현에서 고정)

- 조회: stage별 queue(project scope 필터, panel·상태·차단 Pending 요약), panel 검사 상세(활성 attempt·template snapshot·응답·사진·이력·연결 Pending), finalized 성적서·사진 content·PDF download(모두 scope 검증).
- mutation: 검사 시작(attempt+draft report ensure), 응답 저장, 사진 등록/삭제(draft 한정), finalize(result·사유·실패 시 조치 담당 부서·optional 담당자), 재검사 시작, PDF retry, 제조완료확인.
- 모든 mutation은 4장 권한 계약과 6장 transaction 계약을 따르고 `WorkflowMutationResult` 계열의 한글 오류·validation 계약을 재사용한다.
- 신규 permission 코드는 추가하지 않는다. 외부 provider 호출은 없으며 기존 인앱 work item·notification 원본 경로만 사용한다.

## 8. Frontend·UX 계약

- 전역 menu label은 `품질`(기존 `IQC` 항목 대체, 기존 gating 재사용), 기본 route `/quality/inspections`. 기존 `/quality/iqc`는 호환 유지하고 품질 workspace의 IQC tab으로 연결한다(기존 `IqcReportWorkspace` 재사용).
- deep link: panel 검사 `/quality/inspections?stage=...&project=...&panel=...`, 제조완료확인 `/manufacturing/work?project=...&panel=...`. 내 업무의 품질·확인 업무 link를 이 deep link로 교체하고, generic 완료 버튼은 숨기고 domain 화면 이동 action만 제공한다.
- 390px mobile 구성(위→아래): compact stage queue/filter → 선택 panel 핵심 맥락(표시코드·stage·attempt·차단 Pending) → 항목 one-column(체크/값) → 사진/메모 → sticky가 아닌 in-flow 판정 action. `MobileSheet`로 판정 입력(사유·실패 시 조치 담당 부서·optional 담당자)을 받는다. 하단 고정 메뉴는 추가하지 않고 좌상단 숨김 메뉴를 유지하며 page-level horizontal overflow 0, 44px touch target을 지킨다.
- 상태 도형은 제조 화면 체계와 일관: 시작 전 각진 사각, 진행 rounded, 차단 타원, 완료 원형.
- desktop은 stage rail + panel queue + 성적서 detail·이력·PDF 병렬 표시, finalized는 read-only로 구분한다.
- 제조 workspace(`ManufacturingPage`)에 LQC 합격 panel의 완료 확인 card(LQC 판정 요약 + `제조 완료 확인` action)를 추가한다. 품질 workspace에는 확인 상태를 읽기로만 표시한다.
- loading/empty/error/success 상태와 판정 결과·Pending 생성·다음 담당 단계·PDF 상태를 action 인접에 표시하고, 진행 중 중복 submit을 차단한다. 실패 재시도는 동일 payload일 때 동일 operation id를 유지한다.
- Pending 목록/상세는 기존 panel 표시 코드·조치 담당 부서 표시를 재사용하고 `Nonconformance`/`Punch`·재검사 상태를 구분 표시한다. “귀책부서” 표현은 사용하지 않는다.

## 9. 검증 계획

- migration: `0035` fresh/existing DB 적용, seed 정합, guard trigger, purge FK 순서 integration test.
- Backend targeted: stage 담당/scope/permission 거부(타 stage 담당 우회 포함), pass/fail/재검사 전이, 조치 담당 부서·assignee 검증, linked Pending generic close 차단, generic work 우회 차단, handoff 담당자 부재 rollback, active attempt 단일성·replay·다른 payload conflict·stale version, FAT skip/필요 분기, last-panel project event exactly-once, 취소·purge 정합.
- 회귀: Backend 전체 test(009A IQC·011A 제조·Pending·workflow 포함), Frontend lint·typecheck·unit·production build.
- isolated Full-Stack E2E(disposable DB, provider disabled): LQC 합격 → 제조완료확인 → OQC 부적합 → Pending 조치·재검사 요청 → 재검사 합격 → 전진검수 합격 → FAT 비대상 Packing skeleton 확인. FAT 필요 분기는 최소 targeted integration으로 검증한다.
- 시각 검증: 품질 workspace·제조완료확인 card의 페이지별 desktop·390px screenshot, Teams narrow 포함 page-level overflow 0.
- 사용자 검수: checklist를 작성하되 `사용자 검수 대기`로 유지하고 완료로 표시하지 않는다. 미실행 검증은 이유와 함께 그대로 보고한다.

## 10. 안전 경계

- Persistent UAT 영향 없음. isolated PostgreSQL·synthetic data·disabled provider만 사용한다.
- migration은 additive `0035` 1건. 기존 migration 무수정, 번호 재사용·backfill 없음. 적용 후 복구는 additive forward-fix만 사용한다.
- 게시 경계: local experiment commit까지만(change-001 승인). push·PR·merge·대표 repo·`main` 반영 없음, main merge 승인 `0/3`, Persistent UAT·실제 provider 미승인.
- privacy: 실제 사용자·고객·프로젝트 식별자, raw API/DB body, credential을 코드·문서·증빙에 기록하지 않는다. receipt·PDF·snapshot은 bounded field만 담는다.
- 중단 조건: 18단계 순서·FAT optional·전진-only·finalized 불변·Pending 감사 위반, `0034` 이후 schema 충돌, 기존 불변 trigger·purge 계약 비호환, secret/개인정보 노출이 발견되면 fast-track으로 우회하지 않고 blocking decision으로 반환한다. TASK-009A 범위 밖 결함은 별도 Finding으로만 기록한다.

## 11. 구현 순서 (Codex 지시)

1. `0035` schema·seed·constraint·guard trigger·purge migration과 migration tests
2. generic work/Pending close guard 확장과 transaction-safe Pending helper 재사용 wiring
3. Quality inspection store: attempt/응답/사진/finalize/재검사/PDF(009A font 자산 재사용, 전용 renderer)
4. 제조완료확인 record·stage별 assignee handoff·last-panel project event·FAT skip
5. `/quality/inspections` API·adaptive workspace·전역 `품질` menu·deep link·제조 확인 card
6. Backend targeted/전체, Frontend unit/lint/typecheck/build, isolated Full-Stack E2E
7. desktop·390px 페이지별 screenshot, implementation report·SOP·user manual·user validation checklist·Roadmap experiment 상태 기록, local experiment commit

## 12. 완료 기준

- 3장 포함 범위 전부와 4·5·6장 계약의 서버 강제, Open P0/P1 `0/0`, P2는 해결 또는 canonical 정책의 기록 요건 충족
- 8장 UX 계약과 overflow·접근성 기준 충족
- 9장 검증 전부 실행·결과 기록(미실행은 이유 명시), 5종 종료 산출물 위치·상태 추적 가능
- 사용자 검수 상태 `사용자 검수 대기` 유지, 게시 상태는 local commit까지만

## 13. 후속으로 넘기는 비차단 결정 (deferred)

| 번호 | 항목 | 후속 위치 |
| ---: | --- | --- |
| 1 | 실제 stage별 checklist 문항·필수 사진 위치·개수 | 현업 회신 후 신규 template version |
| 2 | 실제 고객 성적서 PDF 양식·전자서명 | 별도 후속 Task |
| 3 | template 관리·version activation 정책 | TASK-ADMIN-002 |
| 4 | 운영 사진 storage·retention·백업 정책 | 운영 정책 결정 |

openBlockingDecisionCount: 0
