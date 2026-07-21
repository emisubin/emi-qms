# TASK-WORKFLOW-CONTINUITY-001 — 프로젝트 입력 연속성·품질 재검사 자동 인계 2차 기획 (구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-WORKFLOW-CONTINUITY-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/workflow-continuity-001-interview.md`
- firstPlanningSource: `tasks/workflow-continuity-001-planning.md`
- codexReviewSource: `tasks/workflow-continuity-001-review.md`
- approvalChangeSource: `tasks/workflow-continuity-001-change-001.md`
- planningApprovedForExperiment: true
- implementationApprovedForExperiment: true — 본 문서 blocking decision 0 조건 충족
- persistentUatApproved: false
- externalProviderApproved: false
- pushApproved: false / prApproved: false / mergeApproved: false

이 문서는 확인 완료 interview, Fable 1차 기획과 Codex 내용 review를 모두 반영한 이 실험 Task의 최종 구현 source of truth다. 1차 기획과 review는 판단 이력으로 보존하며 수정하지 않는다. 이 문서만으로 구현 범위·권한·상태·data lifecycle·UX·검증·제외 범위를 판단할 수 있어야 하고, 이 문서와 1차 기획이 다르면 이 문서를 따른다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 복사하지 않는다.

## 1. 한 줄 목표

실제 담당자가 별도 `IQC 요청`·`재검사 요청` 버튼 없이 생산계획 확정부터 부적합 조치·재검사 합격·최종 완료까지 시스템 인계만 따라 끊김 없이 진행할 수 있다.

## 2. 해결할 업무 문제와 검증된 단절 지점

사용자가 실제 담당자 관점으로 프로젝트를 처음부터 입력하다 IQC 부적합 Pending에서 더 진행하지 못했다. Pending만 Closed되고 receipt는 `FailedBlocked`, IQC attempt는 `Failed`로 남아 검사·Pending·내 업무·알림·workflow가 서로 다른 상태로 갈라진다.

현재 코드에서 재검증한 단절 지점(2차 기획 시점 기준 모두 유효함):

1. 프로젝트 상세 첫 진입이 설계 탭이다 — `frontend/src/App.tsx`의 `initialSection={view.section ?? 'panels'}`.
2. 생산계획 완료 시 다음 업무는 설계 하나만 생성되고 구매는 내 업무를 받지 못한다.
3. 구매 완료 판정은 required template match 또는 품목명 존재만 보며, 일반 구매품의 발주 수량·단위는 구매 화면 입력이 오히려 validation으로 거부된다(`"일반 구매품의 발주 수량과 단위는 첫 도착 등록에서 입력해 주세요"`).
4. `RegisterArrivalAsync`는 `Arrived`에서 commit하고 IQC 인계는 별도 `RequestIqcAsync` 호출에 의존한다.
5. IQC 내 업무는 target `Inspection`으로 생성되지만 `WorkflowStore.LinkUrlForWorkItem`에 IQC 분기가 없어 프로젝트 workflow fallback으로 간다. 알림은 이미 `/quality/iqc?request={attemptId}`를 생성하지만 Frontend `/quality/iqc` route가 query를 파싱하지 않아 무시된다.
6. `PendingStore.IsQualityInspectionPendingAsync`가 `panel_quality_inspection_attempts`의 `Failed` attempt만 검사해 material IQC Pending은 수동 Closed가 가능하다.
7. Pending `InProgress → ReinspectionRequested` 전이와 material/panel `RequestReinspectionAsync`가 서로 다른 API·다른 역할의 별도 행동으로 끊겨 있고, panel 재검사 work item은 호출자 본인에게 배정되며 알림이 없다.
8. 부적합 최종화의 사진 요구는 template 항목별 `requires_photo`뿐이고 부적합 자체의 공통 증빙 gate가 없다.
9. `PanelQrManager`는 checkbox를 `hasActiveQr`에만 활성화해 미발급 eligible 패널을 선택할 수 없고, 발급은 행별 단건 호출뿐이며 preview는 목록 아래 한 곳에만 렌더된다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 생산관리 정·부 | 생산계획·담당자 확정 | 전 부서 | 생산계획, Pending 관리 |
| 설계 정·부 | 패널명·사이즈 입력(구매와 병행) | 전 부서 | 패널 정보 |
| 구매 정·부 | 강화된 완결성 기준으로 구매정보 완료 | 전 부서 | 구매품목 |
| 자재 정·부 | 도착 등록(IQC 자동 인계), 입고 확정, 키팅 | 전 부서 | 자재 도착·입고·키팅 |
| 품질 단계별 정·부 | IQC/LQC/OQC/전진검수·FAT 검사, 부적합 증빙, 재검사 | 전 부서 | 검사 attempt·report |
| Pending 조치 담당자 | 조치 시작·처리 내용·조치 완료 | 담당 Pending | Pending 전이·comment |
| System Administrator | QR 재발급(사유 필수) | 전체 | QR rotation |

신규 권한 능력은 만들지 않는다. 기존 permission code와 담당자 해석(`ResolveAssigneeAsync`, Roadmap 5장 fallback 순서: 단계 정담당 → 부담당 → 영업 정 → 영업 부 → System Administrator)을 재사용한다. 담당 부서만 쓰기, 전 부서 조회, 서버 Policy authoritative 원칙을 유지한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 도착부터 재검사 합격까지 무단절 진행

1. 자재 담당자가 도착 등록을 저장하면 같은 DB transaction에서 상세 IQC attempt, 품질 IQC 정담당 내 업무, 정·부 인앱 알림, receipt `IqcRequested` 전이와 material event가 생성된다. `IQC 요청` 버튼은 정상 흐름에 없다.
2. 품질 담당자가 내 업무 `이동` 또는 알림을 누르면 `/quality/iqc?request={attemptId}`로 이동해 해당 attempt가 선택된 상태로 열린다.
3. 부적합 최종화에는 사진 1장 이상 또는 구조화 근거(6장 정책 3)가 필요하고, linked Pending이 생성·재사용된다.
4. 조치 담당자가 Pending에서 `조치 시작` → 처리 내용 입력 → `조치 완료`를 누르면 같은 transaction에서 Pending `ReinspectionRequested` 전이, 재검사 attempt, 품질 해당 단계 정담당 내 업무, 정담당 배정 알림·부담당 참조 알림, history 기록이 함께 생성된다.
5. 재검사 합격은 attempt·receipt(또는 panel stage)·linked Pending·work item·workflow event를 한 transaction에서 원자 갱신한다. 재검사 부적합은 같은 Pending을 내부 전용 전이로 `InProgress`에 되돌리고 새 Pending을 만들지 않는다.
6. 전체 흐름 탭은 최신 persisted facts로 상태를 계산해 새로고침 후에도 완료 사실을 유지한다.

### 시나리오 B — 병행 활성화와 프로젝트 진입

1. 생산관리 담당자가 생산계획·담당자를 확정한다.
2. 시스템이 설계와 구매 두 개의 내 업무와 각 정/부 알림을 함께 멱등 생성한다. stage 완료 순서와 stage event는 바꾸지 않는다.
3. 이후 프로젝트 상세 진입 시 `전체 흐름` 탭이 기본으로 열려 현재 병목과 다음 행동을 보여 준다. explicit `?section=` query는 그대로 존중한다.

### 시나리오 C — QR 선택 발급과 inline preview

1. 담당자가 패널 QR 화면에서 발급 가능한 미발급 패널을 포함해 여러 패널을 checkbox로 선택한다.
2. `선택 QR 발급`을 누르면 서버가 선택 전체를 사전 검증하고, 하나라도 무효면 발급 0건으로 rollback하며, 모두 유효하면 미발급 패널을 한 transaction에서 발급한다. 이미 발급된 패널은 idempotent success로 집계한다.
3. `보기`는 선택 행 바로 다음 sibling에 preview를 펼친다. 인쇄 선택과 발급 선택이 중복 행동으로 보이지 않는다.

## 5. 기능 요구사항

### 필수

- [ ] 프로젝트 상세 기본 진입 탭을 `workflow`로 변경 — `initialSection` fallback만 바꾸고 explicit section query는 보존
- [ ] 생산계획 완료 시 설계·구매 내 업무·정/부 알림 동시 멱등 생성(stage 완료 순서·stage event 불변)
- [ ] 구매 완료 판정을 6장 정책 1의 완결성 기준으로 교체(기존 `Completed` stage/event 비회귀)
- [ ] 프로젝트 상세 자재 탭 안 `입고 관리`·`키팅 관리` 하위 탭과 품목별 예정·도착·IQC·확정·잔량 비교(모바일은 하위 탭별 compact card)
- [ ] `RegisterArrivalAsync`의 기존 transaction 안에서 IQC attempt·work item·정/부 알림·event까지 생성하고, 정상 흐름의 `IQC 요청` 버튼 제거(`RequestIqcAsync`는 과거 `Arrived` row 복구 전용 멱등 경로로 유지)
- [ ] `LinkUrlForWorkItem`에 IQC 분기 추가 — canonical URL은 기존 알림과 동일한 `/quality/iqc?request={attemptId}`. Frontend `/quality/iqc` route가 `request`를 파싱·보존해 해당 attempt를 선택(권한 없으면 같은 내용 조회 전용, 무효 attempt는 안전한 empty/error). `attempt=` 신규 alias는 만들지 않는다
- [ ] 모든 활성 품질검사(상세 IQC·LQC/OQC/전진검수/FAT)의 부적합 최종화 공통 증빙 gate(6장 정책 3)
- [ ] 검사 연계 Pending의 조치 UI를 `조치 시작`·처리 내용·`조치 완료`로 한정하고 `종결` action 제거 — 일반 수동 Pending의 기존 lifecycle은 보존
- [ ] `조치 완료`(`ReinspectionRequested` 전이)와 같은 DB transaction에서 재검사 attempt·품질 정담당 work item·정담당 배정 알림·부담당 참조 알림 생성(6장 정책 4)
- [ ] `IsQualityInspectionPendingAsync`를 `material_iqc_attempts`와 `panel_quality_inspection_attempts` 두 원장의 linked pending 검사로 확장 — material·panel 모두 일반 `Closed` transition을 항상 서버 차단
- [ ] 재검사 합격의 원자 갱신과 재검사 부적합의 동일 Pending 재사용(6장 정책 5, 중복 Pending 0)
- [ ] Pending comments·status history의 단일 chronological activity timeline(6장 정책 6) — 별도 comments 섹션 제거
- [ ] 전체 흐름의 IQC 상태는 receipt/attempt 최신 facts로, panel 품질 상태는 active panel별 최신 attempt와 StageCompleted event를 함께 검증해 계산 — Closed Pending 자체를 합격 근거로 사용하지 않으며 종결 뒤 `미시작` 회귀 금지
- [ ] QR 발급 가능 패널 checkbox 선택·all-or-nothing batch 발급·행 inline preview(6장 정책 2)

### 보류 (이번 구현에서 제외, 근거 기록)

- [ ] Pending timeline filter chip — 항목 수가 실제로 많다는 사용자 검수 근거 전까지 보류
- [ ] 이미 잘못 Closed된 운영 IQC Pending의 자동 backfill·재개 action — experiment fixture 복구와 문서화만 허용, 운영 mutation은 별도 UAT/정책 Task로 분리
- [ ] 자재 하위 탭 모바일 지연 경고 badge — 선택 범위, 필수 아님

### 명시적 제거 (review 확정)

- [ ] Frontend 정상 흐름의 `IQC 요청` 버튼과 `재검사 요청` 버튼
- [ ] 검사 연계 Pending의 `종결` action
- [ ] QR batch의 부분 성공·실패 혼합 결과
- [ ] 구매 완료에서 일반 구매품 수량·단위를 요구하는 안(1차 기획 정책 1의 해당 부분 폐기)
- [ ] `/quality/iqc?attempt=` 신규 query alias

### 명시적 제외 (interview 확정)

- [ ] 실제 Teams/Mail provider 호출, Persistent UAT runtime/migration
- [ ] QR public domain·실제 프린터·물리 부착 검증
- [ ] 품질 template 현업 content 확정, object storage·바이러스 검사 신규 도입
- [ ] 과거 운영 데이터 자동 backfill·파괴적 보정
- [ ] 대표 repo·`main`·push·PR·merge

## 6. 확정 비차단 정책 7건 (Codex review 반영 최종본)

실험 standing rule에 따라 아래 7건을 확정한다. 사용자 재질문은 없다.

### 정책 1 — 구매 완료 최소 필수 field (1차 안 수정)

1차 기획의 "모든 활성 품목 수량+단위 필수"는 현재 저장 계약과 충돌한다(일반 구매품 수량·단위는 첫 도착 등록에서 입력하도록 `ProcurementStore`가 강제). 채택안은 다음 AND 조건이다.

1. 활성 구매품목 1개 이상
2. 모든 활성 품목 공통: 발주품목명, 유효한 공급구분, 입고예정일
3. 일반 구매품(`Purchased`): 업체명·발주일 필수, 수량·단위는 요구하지 않음
4. 사급품(`CustomerSupplied`): 제공 예정 수량>0과 단위 필수, 업체명·발주일은 요구하지 않음
5. 해당 ITEM code의 required template가 있으면 모든 required row가 active confirmed 품목과 match
6. 자재 도착·IQC·입고 확정 등 후속 부서 단계는 완료 조건에서 제외
7. 이미 `Completed`인 procurement stage/event는 새 계산으로 회귀시키지 않음

Trade-off: 수량 완결성은 도착 등록 시점으로 미뤄지지만, 구매 단계 차단(전 프로젝트 영구 blocked)을 피하고 입고 관리 비교표에 필요한 예정일·공급구분은 보장한다.

### 정책 2 — QR 선택 batch 발급 (1차 안 수정)

1차 기획의 패널별 부분 성공 응답을 폐기하고 기존 선택 인쇄와 같은 all-or-nothing 안전 모델을 채택한다.

- 요청: 같은 project의 distinct panel 1~50개(기존 인쇄 상한과 일치)
- 서버 사전 검증: project/panel active, panel name 존재, 요청 count 일치, project scope와 issue 권한
- 이미 활성 QR이 있는 패널은 idempotent success — 새 token을 만들지 않음
- 검증 실패가 하나라도 있으면 발급 0건 rollback과 선택 수·현재 가능 수 안내
- 모두 유효하면 미발급 패널을 한 transaction에서 발급, 응답·UI는 `requested/newlyIssued/alreadyIssued`만 표시(partial 실패 count 없음)
- 패널당 활성 QR unique 제약이 동시성 안전판, append-only QR audit 재사용
- Frontend 선택 집합은 `qrEligible || hasActiveQr`. primary action은 미발급 eligible 포함 시 `선택 QR 발급`, 전부 발급된 선택이면 `선택 인쇄판`으로 전환해 같은 의미의 두 action이 동시에 보이지 않게 한다

Trade-off: 부분 성공 대비 재시도 단위는 커지지만 사용자 확인 비용이 낮고 기존 인쇄 계약과 일관된다.

### 정책 3 — 부적합 증빙 최소 모델 (1차 안 유지·정밀화)

- `Failed` 최종화는 (report 사진 1장 이상) 또는 (trim 기준 30자 이상의 구체 판정 근거 텍스트) 중 하나를 서버가 요구한다. 핵심 Backend 계약은 길이·snapshot 존재이고, 단순 결과명·반복 문자 억제는 Frontend hint로 보조한다.
- template 항목별 `requires_photo`는 별도 AND 조건으로 유지한다. 항목 사진 필수는 긴 텍스트로 대체할 수 없다.
- 상세 IQC·panel 검사와 신규 legacy 간편 판정 최종화 모두에 적용하되, 과거 finalized snapshot은 소급 변경·차단하지 않는다. 신규 저장소·컬럼 없이 기존 photo 테이블과 판정 사유 필드를 재사용한다.

### 정책 4 — 조치 완료 자동 재검사 handoff (1차 안 유지·transaction 계약 확정)

- `PendingStore.TransitionAsync`가 보유한 connection/transaction 안에서 대상 판별(material receipt / panel stage) 후 내부 helper를 호출한다. Frontend 순차 API 호출과 별도 commit 두 번은 금지한다. 순환 DI를 피하기 위해 helper는 기존 store의 static/internal SQL 경로를 재사용할 수 있다.
- Material: linked failed attempt/receipt를 `FOR UPDATE`로 잠그고 receipt `FailedBlocked`·Pending `InProgress`를 확인한 뒤 다음 attempt number의 Detailed attempt, IQC work item, 정담당 배정 알림, 부담당 참조 알림, receipt `IqcRequested`, material event를 생성한다.
- Panel: linked failed attempt와 active template를 잠그고 다음 attempt/report를 만든다. work item은 호출자 본인이 아니라 stage별 품질 정담당(fallback 순서)에게 `Requested`로 배정하고 부담당은 참조 알림을 받는다(현행 본인 배정·무알림 결함 교정).
- idempotency key는 `pending:{id}:reinspection:{attemptNumber}` 계열의 결정적 키로 반복·동시 호출을 하나로 수렴시킨다.
- 품질 정·부 담당을 fallback 끝까지 해석할 수 없으면 Pending 전이까지 rollback하고 담당자 설정 안내를 반환한다. 알림 없는 재검사 상태를 만들지 않는다. 같은 원칙을 도착 자동 IQC에도 적용해 부분 상태 대신 도착 등록 자체를 rollback한다(생산계획 필수 정담당 계약상 정상 프로젝트에서는 발생하지 않아야 한다).

### 정책 5 — 재검사 부적합 반복 (1차 안 유지)

- 같은 open Pending을 재사용한다. 재검사 부적합 최종화 transaction에서 Pending을 내부 전용 전이로 `InProgress`에 되돌리고 Pending 업무도 다시 `InProgress`로 열며, history에 검사 회차·사유를 기록한다. 새 Pending 0.
- 기존 단방향 `NextStatuses` 사슬에는 `ReinspectionRequested → InProgress`(검사 부적합 최종화 전용, 서버 내부) 하나만 추가하고 사용자용 임의 역방향 전이는 노출하지 않는다. Closed 후 재오픈안은 append-only 해석을 해쳐 비채택.

### 정책 6 — Pending activity timeline (1차 안 유지·정밀화)

- comments와 status history의 저장 원장은 그대로 두고, detail 응답 또는 Frontend projection에서 UTC timestamp 오름차순 + stable tie-breaker로 하나의 시간순 timeline을 만든다.
- `조치 완료`의 처리 내용은 status transition reason에 저장해 timeline 항목으로 바로 나타난다. 일반 협업 comment 입력은 timeline composer로 유지하되 별도 comments 카드는 제거한다.
- `조치 완료` label에는 재검사 요청을 뜻한다는 보조 설명을 붙이고, 성공 feedback은 실제 생성된 검사 업무 기준으로 표시한다("재검사 업무가 생성되었습니다").

### 정책 7 — 기존 endpoint·row 호환 (1차 안 유지·경계 축소)

- `RequestIqcAsync`와 material/panel `RequestReinspectionAsync`는 제거하지 않고 신규 orchestration과 같은 내부 helper를 호출하는 멱등 legacy 복구 경로로 전환한다. 별도 두 번째 attempt를 만들지 않는다.
- 과거 `Arrived` row는 입고 관리 화면의 legacy `IQC 요청` action으로, 과거 `FailedBlocked + ReinspectionRequested` row는 handoff 멱등 재실행으로 복구한다.
- 이미 잘못 Closed된 IQC Pending은 자동 backfill하지 않는다. 운영 데이터 복구 action은 review 결정에 따라 이번 범위에서 제외하고 experiment fixture 복구·문서화만 수행하며 별도 UAT/정책 Task로 추적한다(1차 기획의 `재검사 재개` 운영 action 제안은 보류로 강등).

## 7. 업무 규칙과 불변조건

- 18단계 workflow 완료 순서는 바꾸지 않는다. 설계·구매의 선행 활성화(내 업무 생성)와 stage 완료는 분리된 개념이다.
- 검사에서 생성된 Pending은 재검사 합격 transaction에서만 종결된다. material·panel 모두 수동 Closed를 서버에서 차단한다.
- 단계는 전진만 한다. 부적합은 차단 flag·Pending으로 표현하고 단계 번호를 되돌리지 않는다.
- 동일 이벤트 재실행 시 내 업무·알림 중복 0(`idempotency_key` 계약 유지).
- 상태·attempt·work item·notification·event는 transaction 경계 안에서 함께 성공하거나 rollback한다. 실제 provider 발송 성공은 포함하지 않고 기존 outbox(`notification_deliveries`) 계약을 보존한다. `재검사 요청`은 채널 matrix상 인앱 즉시·메일 미발송을 유지한다.
- 인앱이 알림 원본이고 내 업무가 authoritative다. Pending history·material event·workflow event·QR audit는 append-only다.
- 패널당 활성 QR 하나, opaque token, 민감정보 미포함.
- 이미 적용된 migration `0001`~`0049`는 수정·재번호하지 않는다. 신규 schema가 필요하면 `0050`부터 additive로만 추가한다(현재 안은 신규 테이블 없이 가능할 것으로 판단하며 필요 시 purge cascade 포함을 함께 검토).
- 구매 완료 조건은 다음 부서 소유 단계를 요구하지 않는다.
- 일반 수동 Pending의 기존 lifecycle·권한은 변경하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `material_receipts` | 도착분 상태(`Arrived→IqcRequested→Passed/FailedBlocked→…`) | 기존 | event append-only 유지 |
| `material_iqc_attempts` | IQC 시도(회차·판정) | 기존 | Pending 종결 보호 검사 대상에 추가 |
| `panel_quality_inspection_attempts` | LQC/OQC/전진검수/FAT 시도 | 기존 | 기존 linked Pending 계약 유지 |
| `pending_issues` + history/comments | 조치 lifecycle | 기존 | timeline은 두 원장의 병합 조회, 저장 구조 불변 |
| 재검사 handoff orchestration | `조치 완료` 전이와 attempt·work item·알림 결합 | 신규(행동) | history·workflow event에 actor·사유 기록 |
| 부적합 증빙 gate | 사진 또는 구조화 근거 판정 | 신규(검증 규칙) | 기존 photo/응답 snapshot 재사용, 신규 저장소 없음 |
| QR batch 발급 | 패널 집합 all-or-nothing 멱등 발급 | 신규(행동) | 기존 append-only QR audit 재사용 |

```text
[material] Arrived ──(도착 등록과 동일 tx: attempt+work item+정/부 알림+event)──> IqcRequested ──> Passed ──> 입고 확정
                                                                                    └─> FailedBlocked + linked Pending(Registered→…→InProgress)
Pending InProgress ──"조치 완료"(동일 tx: 전이+재검사 attempt+work item+정/부 알림)──> ReinspectionRequested
재검사 합격 ──(동일 tx)──> attempt Passed + receipt/panel stage 갱신 + Pending Closed + work item 완료 + workflow event
재검사 부적합 ──(동일 tx)──> 같은 Pending을 내부 전용 전이로 InProgress 복귀 + Pending 업무 재개 + history 회차·사유 기록 (새 Pending 0)
```

## 9. Backend 구현 계약

- Backend가 authoritative한 규칙: 조치 UI 한정, 수동 종결 차단, 증빙 gate, 병행 활성화, 구매 완결성 판정, QR 사전 검증 전부 서버 검증이 원본이고 UI 숨김은 보조다.
- `WorkflowStore`: 생산계획 완료의 다음 stage 처리에 설계+구매 병행 활성화 추가(구매 `Requested` work item·정/부 알림 멱등 생성, stage event 불변), `LinkUrlForWorkItem`에 IQC 분기(`/quality/iqc?request={attemptId}`), 전체 흐름 상태 계산을 최신 persisted facts 기준으로 보정.
- `ProcurementStore`(완료 facts 산출부): 6장 정책 1의 AND 조건으로 교체, 기존 Completed 비회귀 guard.
- `MaterialsStore`: `RegisterArrivalAsync` 기존 transaction 안 IQC 자동 인계, `RequestIqcAsync`·`RequestReinspectionAsync`를 같은 내부 helper 기반 멱등 legacy 경로로 전환.
- `PendingStore`: `IsQualityInspectionPendingAsync` 두 원장 확장, allowedTransitions에서 검사 연계 Pending의 `Closed` 제외, `TransitionAsync` 내 handoff helper 호출(정책 4), 내부 전용 `ReinspectionRequested → InProgress` 전이(정책 5), timeline 병합 projection(정책 6).
- `QualityInspectionStore`/`IqcReportStore`: finalize 공통 증빙 gate(정책 3), 재검사 부적합 시 Pending 복귀, panel 재검사 work item의 정담당 배정·정/부 알림 교정, 재검사 합격의 원자 close 경로.
- QR: 발급 batch endpoint — 같은 project distinct panel 1~50, all-or-nothing 사전 검증, `requested/newlyIssued/alreadyIssued` 응답(정책 2).
- transaction·동시성·idempotency: `FOR UPDATE` 잠금, 결정적 idempotency key, 패널당 활성 QR unique 제약, CAS(version/row_version) 계약 유지.
- audit: 자동 인계에도 actor·사유를 append-only 원장에 기록한다.
- 외부 provider 영향: 없음. outbox row 생성까지만.

위 클래스·컬럼 명칭은 현재 코드에서 확인한 재사용 대상이며, 구현 중 세부 구조가 다르면 이 문서의 계약(transaction 경계·권한·상태)을 우선한다.

## 10. Frontend 구현 계약

- `frontend/src/App.tsx`: 기본 진입 탭 `workflow`(explicit section query 보존), `/quality/iqc`의 `request` query 파싱·보존, 자재 하위 탭 라우팅.
- `frontend/src/MaterialsWorkspace.tsx`(및 자재 탭 구성): 정상 흐름 `IQC 요청` 버튼 제거, legacy 복구 action 분리 표기, `입고 관리`·`키팅 관리` 하위 탭과 품목별 예정·도착·IQC·확정·잔량 비교, 기존 `MaterialIqcPage`·`PanelKittingPage` 재사용.
- `PendingPage.tsx`: 검사 연계 Pending의 단일 next action(`조치 시작`→처리 내용→`조치 완료`), `종결` 노출 제거, 병합 timeline과 composer, `조치 완료`=재검사 요청 보조 설명.
- `frontend/src/PanelQrManager.tsx`: 선택 모델 `qrEligible || hasActiveQr`, primary action 전환(`선택 QR 발급`/`선택 인쇄판`), batch 결과 요약, 행 다음 sibling inline preview(`aria-expanded`/`aria-controls`, 닫기 시 focus 복귀, object URL 해제).
- `api.ts`와 관련 CSS: 신규 batch·timeline·deep link 계약 반영.
- loading/empty/error/success 4상태와 기존 Action Feedback 패턴(TASK-UX-001 A1/A2 계약)을 모든 신규·변경 화면에 적용한다. 자동 인계 결과는 저장 버튼 근처에 표시한다("IQC 검사 대기로 넘겼습니다", "재검사 업무가 생성되었습니다").

## 11. UX·접근성·모바일

- 자재 하위 탭 모바일은 PC 표 축소가 아니라 `입고 관리`는 다음 행동·지연·IQC 상태, `키팅 관리`는 패널 준비 상태의 compact card를 제공한다.
- QR 선택은 checkbox label·disabled 사유·선택 수·발급 결과를 명시하고 keyboard/focus와 44px touch target을 보존한다.
- Pending 화면은 현재 가능한 하나의 행동만 보여 주고 timeline은 상태·comment·검사 근거를 시간순으로 낭독 가능하게 한다.
- IQC deep link는 attempt를 직접 선택하고, 권한 없으면 같은 내용을 조회 전용으로, 무효 attempt는 안전한 empty/error로 처리한다.
- desktop과 390px에서 page-level horizontal overflow 0, conflict 복구, `role=status`/screen reader announcement를 검증한다.

## 12. 기존 기능과의 연결

- 내 업무·알림은 기존 `work_items`/`notifications` 계약과 idempotency key 패턴을 재사용하며 NOTIFY-005 preference·NOTIFY-AUDIT-001 감사·NOTIFY-REPROCESS-001 재처리 계약을 바꾸지 않는다.
- template 관리(TASK-ADMIN-002)의 `requires_photo` 계약은 유지하고 그 위에 공통 gate를 추가한다.
- IQC·panel 성적서 PDF와 photo snapshot 계약, 선택 Excel export 등록은 변경 없음.
- Teams/Mail은 outbox까지만, 채널 matrix 변경 없음.
- purge cascade는 신규 테이블이 생기는 경우에만 확장을 검토한다.

## 13. Codex review Finding resolution 반영표

| Review 항목 | 처분 | 본 문서 반영 위치 |
| --- | --- | --- |
| 기본 `전체 흐름` — fallback만 변경 | 반영 | 5장 필수 1, 10장 |
| 병행 활성화 — work item·알림만 멱등 생성 | 반영 | 4장 B, 9장 |
| 구매 완료 조건 — 수량·단위 요구 폐기, 7개 AND 조건 | 반영(1차 안 수정) | 6장 정책 1 |
| 자재 하위 탭 | 반영 | 5장, 10~11장 |
| 도착→IQC 동일 transaction, legacy 복구 한정 | 반영 | 4장 A, 6장 정책 7, 9장 |
| deep link `request` canonical, `attempt` alias 제거 | 반영(1차 안 정정) | 5장 필수 6 |
| 증빙 gate — requires_photo AND 유지, 소급 없음 | 반영 | 6장 정책 3 |
| 조치 UI 한정 — 검사 연계 Pending만 | 반영 | 5장, 7장 |
| handoff 단일 DB transaction, Frontend 순차 호출 금지 | 반영 | 6장 정책 4 |
| 재검사 부적합 — 같은 Pending·업무 재개, 내부 전용 전이 | 반영 | 6장 정책 5 |
| timeline — 원장 불변, UTC+tie-breaker 병합, comments 섹션 제거 | 반영 | 6장 정책 6 |
| 전체 흐름 정합성 — facts 기반, Closed Pending 비근거 | 반영 | 5장 필수 13 |
| QR batch — all-or-nothing·idempotent·`requested/newlyIssued/alreadyIssued` | 반영(1차 안 수정) | 6장 정책 2 |
| inline preview 접근성·object URL 해제 | 반영 | 10장 |
| 추가: 담당 부재 시 전이·도착까지 rollback | 반영 | 6장 정책 4 |
| 추가: material·panel 각각의 lifecycle E2E | 반영 | 15장 |
| 보류: filter chip, 잘못 Closed 운영 복구, 실제 발송 | 반영 | 5장 보류 |
| 제거 5건 | 반영 | 5장 명시적 제거 |
| Bookkeeping 번호 정정 | 반영 | 17장 |
| 권장 구현 순서 | 반영 | 15장 |

## 14. Task 고유 안전 경계

- Persistent UAT 영향: 없음. 검증은 disposable runtime·전용 DB만 사용한다.
- migration: 원칙적으로 불필요. 필요 시 `0050`부터 additive로만 추가하고 fresh/existing 모두 검증한다. `0001`~`0049` 불변.
- 외부 발송·실제 데이터 영향: 없음. outbox row 생성까지만.
- runtime 교체: 없음.
- 승인 경계: local experiment commit만 승인됨. push·PR·merge·대표 repo·Persistent UAT·실제 provider·게시는 이 문서가 승인하지 않으며 `main` merge 승인은 0/3이다. 이 문서는 게시·merge·Persistent UAT 승인 상태를 변경하지 않는다.

## 15. 검증 계획과 구현 순서

구현 순서(review 확정):

1. 재현 tests를 먼저 실패로 고정 — material IQC Pending 수동 Closed 우회, 재검사 알림·내 업무 누락, IQC deep link fallback, workflow 상태 회귀.
2. Backend 종결 보호 확장과 material/panel 재검사 transaction helper, 반복 부적합·합격 정합성.
3. 도착→IQC 자동 transaction과 legacy endpoint 호환.
4. 품질 증빙 gate와 deep link.
5. 생산계획 설계·구매 병행 활성화와 구매 완료 facts.
6. QR all-or-nothing batch API.
7. Frontend 기본 탭, 자재 하위 탭, Pending action/timeline, IQC deep link, QR 선택·inline preview.
8. targeted → 전체 Backend/Frontend → isolated lifecycle/QR E2E → desktop/390px screenshot.

최소 테스트: 병행 활성화(설계·구매 work item 2건·중복 0), 도착→IQC 동일 transaction, material·panel Pending 수동 Closed 차단, handoff 멱등성(반복·동시 호출 수렴)과 담당 부재 rollback, 재검사 합격 원자 갱신·부적합 동일 Pending 재사용, 증빙 gate(사진/텍스트/누락/requires_photo AND), 구매 완결성(일반/사급/template match/기존 Completed 비회귀), QR batch(혼합 선택·상한·stale 전체 실패·idempotent 재실행), IQC deep link(`request` 파싱·권한 없음 조회 전용·무효 attempt).

영향 영역 회귀: Backend 전체 test(기준선 410건 + 신규), Frontend lint/typecheck/unit(111건 + 신규)/build, migration fresh/existing isolated 검증.

Full-Stack: 실제 역할 lifecycle E2E를 확장해 material IQC와 panel 품질 각각에 대해 부적합→조치 시작→조치 완료(자동 재검사)→재검사 합격 경로를 한 번씩 검증하고, 도착→합격→입고 확정→후속 단계→최종 완료를 한 시나리오로 잇는다. QR isolated E2E에 선택 batch 발급·inline preview를 추가한다.

사용자 검수: desktop과 390px 페이지별 screenshot(전체 흐름 첫 진입, 자재 하위 탭 2종, Pending 상세 timeline, IQC deep link 선택 상태, QR 선택 발급·preview). 상태는 `사용자 검수 대기 — 마지막 일괄 검수`로 유지하며 이 문서는 사용자 검수 완료를 주장하지 않는다.

## 16. 완료 기준

- 기능/권한/데이터: interview 8장 성공 기준 전부 — 수동 우회 버튼 없는 무단절 진행, 수동 종결 서버·UI 불가, 조치 완료 1회로 정·부 알림과 단일 내 업무, 재검사 합격 후 정합 유지·`미시작` 회귀 없음, 구매 완결성 보장과 중복 차단 없음, QR batch·inline preview의 keyboard·mobile 동작.
- UX: 4상태 feedback·접근성·390px overflow 0.
- 자동 테스트: Backend 전체, Frontend lint/typecheck/unit/build, migration fresh/existing, Full-Stack lifecycle(2 경로)·QR isolated E2E 통과.
- 5종 산출물: Implementation report에 상태·위치 추적(SOP/manual은 기존 문서 갱신 또는 N/A 사유 기록).
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- PR 상태: 없음(local experiment commit만).

## 17. 예상 변경 범위와 bookkeeping

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Workflow/WorkflowStore.cs`, `Materials/MaterialsStore.cs`, `Materials/IqcReportStore.cs`, `Pending/PendingStore.cs`(+contracts), `QualityInspections/QualityInspectionStore.cs`, `Procurement/ProcurementStore.cs`의 완료 facts 산출부, `PanelQr` endpoint/store와 관련 tests
- Frontend: `App.tsx`, `MaterialsWorkspace.tsx`, `PendingPage.tsx`, `PanelQrManager.tsx`, `api.ts`, 관련 CSS·unit tests
- DB/Migration: 기본 없음, 필요 시 `0050` additive
- Tests/Scripts: Backend targeted·통합, Frontend unit, Full-Stack lifecycle·QR E2E 확장
- Docs: 기존 Task change 문서, Roadmap·실험 완료 원장 동기화, Implementation report

기존 Task change 번호는 review 정정안을 확정한다: `TASK-007A Change 001`, `TASK-008A Change 002`, `TASK-009A Change 002`, `TASK-012A Change 002`, `TASK-QR-001 Change 002`, `TASK-E2E-FULL-SUITE-001 Change 007`. 제품 정책 영향은 없다.

## 18. Codex 구현 지시문 (확정)

1. 이 문서를 최종 구현 source of truth로 사용하고, 6장 정책 7건과 13장 resolution 반영표를 그대로 구현한다. 1차 기획·review와 다른 부분은 이 문서를 따른다.
2. 15장 구현 순서대로 진행하며 각 단계마다 targeted test를 추가·통과시킨다. 재현 tests를 먼저 실패로 고정한 뒤 수정한다.
3. Backend 불변조건(transaction 경계, 멱등 key, 담당 부재 rollback, 수동 종결 차단, 증빙 gate, all-or-nothing QR batch)을 서버에서 강제하고 UI는 보조로만 숨긴다.
4. Frontend는 기본 탭·자재 하위 탭·Pending 단일 행동/timeline·`request` deep link·QR 선택 발급을 구현하고 기존 action feedback·접근성 계약을 재사용한다.
5. Full-Stack lifecycle E2E를 material·panel 두 부적합→조치→자동 재검사→합격 경로로 확장하고 desktop/390px screenshot을 페이지별로 남긴다.
6. 17장 번호로 기존 Task change 문서·Implementation report·완료 원장·Roadmap을 동기화한 뒤 승인된 allowlist만 stage해 local experiment commit 한다. push·PR·merge·Persistent UAT·실제 provider·게시는 수행하지 않는다.

## 19. 결정 상태

review resolution은 모두 반영되었고, 비차단 정책 7건은 실험 standing rule에 따라 본 문서에서 확정했다. Repository source 간 의미 있는 충돌과 안전상 blocking decision은 없다. 사용자에게 새로 물을 항목은 없다.

openBlockingDecisionCount: 0
