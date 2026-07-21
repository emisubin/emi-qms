Baseline verification is complete — all eight Root Findings in the interview are confirmed against the current implementation, migration `0049` is the latest applied version, and the change-numbering in the fast-track change document partially mismatches existing `tasks/` files (recorded below as a non-blocking bookkeeping finding). Producing the single first-pass planning draft now.

---

# TASK-WORKFLOW-CONTINUITY-001 — 프로젝트 입력 연속성·품질 재검사 자동 인계 기획안 (Fable 1차)

> 상태: Draft
> 작성 단계: Codex review 전 Fable 1차 기획
> 목적: 사용자 검수 실패로 확정된 14개 업무 결과와 7개 비차단 정책의 구현 계약 초안을 고정한다

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/workflow-continuity-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 실제 담당자가 프로젝트를 처음부터 입력하다 IQC 부적합 Pending에서 더 진행하지 못했다. Pending만 Closed되고 receipt는 `FailedBlocked`, IQC attempt는 `Failed`로 남아 검사·Pending·내 업무·알림·workflow가 서로 다른 상태로 갈라진다.
- 대상 사용자·역할: 영업·생산관리·설계·구매·자재·품질(IQC/LQC/OQC/전진검수·FAT)·제조·물류의 정·부 담당자와 System Administrator(QR 재발급 한정).
- 정상 흐름: 생산계획 확정 → 설계·구매 병행 업무 → 구매 완료 → 자재 도착 등록 → IQC 자동 요청 → 품질 검사 → 합격이면 입고 확정, 부적합이면 linked Pending → 조치 시작·처리 내용·조치 완료 → 품질 정·부 재검사 업무/알림 자동 생성 → 재검사 합격 → Pending·IQC·workflow 원자 갱신.
- 예외·복구 흐름: 중복·동시 호출은 receipt당 활성 요청·panel당 활성 QR·Pending당 재검사 attempt 하나로 수렴, 증빙·기본정보 부족과 stale version은 서버 차단, 기존 `Arrived`·`FailedBlocked` row는 legacy 복구 경로, 잘못 Closed된 IQC Pending의 자동 backfill은 금지.
- 확정한 정책과 명시적 제외: 인터뷰 2장 14개 업무 결과 전부 포함. 실제 Teams/Mail provider, Persistent UAT, QR public domain·물리 검증, template 현업 content, 과거 데이터 자동 backfill, 대표 repo·`main`·push·PR·merge는 제외.
- planning으로 넘긴 비차단 미결정 사항: 인터뷰 4장의 7개 정책. 실험 standing rule에 따라 본 문서의 권장안을 Codex review 후 Fable 2차 기획에서 자동 채택하며 사용자에게 다시 묻지 않는다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

실제 담당자가 별도 IQC 요청·재검사 요청 버튼 없이 생산계획 확정부터 부적합 조치·재검사 합격·최종 완료까지 시스템 인계만 따라 끊김 없이 진행할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 단계마다 다음 부서에 수동으로 신호를 보내는 보조 버튼(`IQC 요청`, `재검사 요청`)을 스스로 찾아 눌러야 하며, Pending 화면에는 일반 종결 경로가 열려 있어 검사 흐름 밖에서 이슈를 닫아버릴 수 있다.
- Repository 대조로 확인한 단절 지점(모두 현재 코드에서 재검증함):
  1. 프로젝트 상세 첫 진입이 `panels`(설계) 탭이다 — `frontend/src/App.tsx`의 `initialSection={view.section ?? 'panels'}`.
  2. 생산계획 완료 시 `WorkflowStore`의 `StageToNextStage`가 설계 하나만 다음 업무로 만든다. 구매는 내 업무를 받지 못한다.
  3. 구매 완료 판정(`ProcurementStatus`)은 required template match 수 또는 품목명 존재 수만 본다. 수량·단위·공급구분·입고예정일 완결성이 없다.
  4. `RegisterArrivalAsync`는 `Arrived`에서 commit하고, IQC 인계는 별도 `RequestIqcAsync` 호출에 의존한다.
  5. IQC 내 업무는 stage `IQC`·target `Inspection`으로 생성되지만 `LinkUrlForWorkItem`에 해당 분기가 없어 `/projects/{id}?section=workflow` fallback으로 간다. Frontend `/quality/iqc` route는 query parameter를 아예 파싱하지 않아 알림의 `?request=` deep link도 무시된다.
  6. `PendingStore.IsQualityInspectionPendingAsync`가 `panel_quality_inspection_attempts`만 검사해 material IQC Pending은 수동 Closed가 가능하다.
  7. Pending `InProgress → ReinspectionRequested` 전이(assignee만 가능)와 material `RequestReinspectionAsync`(자재 화면 버튼)·panel `RequestReinspectionAsync`(품질 화면)가 서로 다른 API·다른 역할의 별도 행동으로 끊겨 있고, panel 재검사 work item은 호출자 본인에게 배정되며 알림을 만들지 않는다.
  8. 부적합 최종화의 사진 요구는 template 항목별 `requires_photo`뿐이고, 부적합 자체에 대한 공통 증빙 gate가 없다.
  9. `PanelQrManager`는 checkbox를 `hasActiveQr`에만 활성화해 미발급 eligible 패널을 선택할 수 없고, 발급은 행별 단건 호출뿐이며, preview는 목록 아래에 한 번만 렌더된다.
- 이 기능이 없으면 실사용 검수가 IQC 부적합에서 매번 중단되고, 수동 우회는 검사 사실과 Pending·workflow 상태를 영구히 어긋나게 만든다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 생산관리 정·부 | 생산계획·담당자 확정 | 전 부서 | 생산계획, Pending 관리 |
| 설계 정·부 | 패널명·사이즈 입력(구매와 병행) | 전 부서 | 패널 정보 |
| 구매 정·부 | 강화된 완결성 기준으로 구매정보 완료 | 전 부서 | 구매품목 |
| 자재 정·부 | 도착 등록(IQC 자동 인계), 입고 확정, 키팅 | 전 부서 | 자재 도착·입고·키팅 |
| 품질 단계별 정·부 | IQC/후속 검사, 부적합 증빙, 재검사 | 전 부서 | 검사 attempt·report |
| Pending 조치 담당자 | 조치 시작·처리 내용·조치 완료 | 담당 Pending | Pending 전이·코멘트 |
| System Administrator | QR 재발급(사유 필수) | 전체 | QR rotation |

기존 원칙 유지: 담당 부서만 쓰기, 전 부서 조회, 서버 Policy가 authoritative, System Administrator도 업무 입력을 무제한 우회하지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 도착부터 재검사 합격까지 무단절 진행

1. 자재 담당자가 도착 등록을 저장하면 같은 transaction에서 IQC attempt·품질 IQC 정담당 내 업무·정·부 인앱 알림이 생성된다(`IQC 요청` 버튼 없음).
2. 품질 담당자가 내 업무의 `이동`을 누르면 `/quality/iqc`에서 해당 attempt가 선택된 상태로 열린다.
3. 부적합 최종화에는 사진 1장 이상 또는 구조화 근거가 필요하고, linked Pending이 생성·재사용된다.
4. 조치 담당자가 Pending에서 `조치 시작` → 처리 내용 입력 → `조치 완료`를 누르면 같은 transaction/멱등 orchestration에서 재검사 attempt·품질 정담당 내 업무·정·부 알림이 생성된다.
5. 재검사 합격이 attempt·receipt(또는 panel stage)·linked Pending·workflow·work item을 원자적으로 갱신하고, 전체 흐름 탭이 새로고침 후에도 완료 사실을 유지한다.

### 시나리오 B — 병행 활성화와 프로젝트 진입

1. 생산관리 담당자가 생산계획·담당자를 확정한다.
2. 시스템이 설계와 구매 두 개의 내 업무를 함께 생성한다(stage 완료 순서는 불변).
3. 이후 누구든 프로젝트 상세에 진입하면 `전체 흐름` 탭이 먼저 열려 현재 병목과 다음 행동을 본다.

### 시나리오 C — QR 선택 발급과 inline preview

1. 자재/담당자가 패널 QR 화면에서 발급 가능한 미발급 패널 여러 개를 checkbox로 선택한다.
2. `선택 발급`을 누르면 패널별 멱등 batch로 발급되고 발급 n·기존 m·실패 k 요약이 표시된다.
3. `보기`는 해당 행 바로 아래에 preview를 펼치고(aria-controls 연결), 인쇄 선택과 발급 선택이 중복 행동으로 보이지 않는다.

## 5. 기능 요구사항

### 필수

- [ ] 프로젝트 상세 기본 진입 탭을 `workflow`(전체 흐름)로 변경
- [ ] 생산계획 완료 시 설계·구매 내 업무 동시 생성(기존 idempotency key 계약 유지, stage 완료 순서 불변)
- [ ] 구매 완료 판정에 활성 품목 완결성 기준 추가(7장 정책 1 권장안)
- [ ] 프로젝트 상세 자재 탭에 `입고 관리`·`키팅 관리` 하위 탭과 품목별 예정·도착·IQC·확정·잔량 비교
- [ ] 도착 등록 transaction 안 IQC 자동 인계(attempt·work item·알림), 별도 `IQC 요청` 버튼 제거
- [ ] `LinkUrlForWorkItem` IQC 분기 추가와 `/quality/iqc`의 attempt 선택 deep link(권한 없으면 조회 전용, 잘못된 attempt는 안전한 empty/error)
- [ ] 모든 활성 품질검사(재료 IQC·panel LQC/OQC/전진검수/FAT)의 부적합 최종화 공통 증빙 gate
- [ ] 검사 연계 Pending의 조치 UI를 `조치 시작`·처리 내용·`조치 완료`로 한정하고 임의 종결 경로 제거
- [ ] `조치 완료`(=`ReinspectionRequested` 전이)와 같은 transaction/멱등 orchestration에서 재검사 attempt·품질 정담당 work item·정·부 알림 생성
- [ ] `IsQualityInspectionPendingAsync`에 `material_iqc_attempts` 포함(수동 Closed 우회 서버 차단)
- [ ] 재검사 합격의 원자 갱신과 재검사 부적합의 동일 Pending 재사용(중복 Pending 0)
- [ ] Pending comment·상태 history의 단일 chronological activity timeline
- [ ] 전체 흐름의 IQC·후속 품질 상태를 최신 persisted facts로 계산하고 종결 뒤 `미시작` 회귀 금지
- [ ] QR 발급 가능 패널 checkbox 선택·batch 발급·행 inline preview

### 선택

- [ ] 자재 하위 탭의 모바일 compact card에 지연 경고 badge
- [ ] Pending timeline의 이벤트 유형 filter chip

### 명시적 제외

- [ ] 실제 Teams/Mail provider 호출, Persistent UAT runtime/migration
- [ ] QR public domain·실제 프린터·물리 부착 검증
- [ ] 품질 template 현업 content 확정, object storage·바이러스 검사 신규 도입
- [ ] 과거 운영 데이터 자동 backfill·파괴적 보정
- [ ] 대표 repo·`main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 프로젝트 상세 전체 흐름 | 프로젝트 클릭 기본 진입 | 18단계 진행·병목·오픈 Pending | 단계별 상세 이동 | 최신 facts 기반 상태, 회귀 없음 |
| 자재 탭 > 입고 관리 | 프로젝트 상세 자재 탭 | 품목별 예정·도착·IQC·확정·잔량 | 도착 등록(IQC 자동 인계), legacy 복구 | action feedback + 다음 인계 안내 |
| 자재 탭 > 키팅 관리 | 프로젝트 상세 자재 탭 | 패널별 키팅 준비·완료 | 키팅 처리(기존 기능 재사용) | 기존 계약 유지 |
| Pending 상세 | 내 업무·알림·Pending 목록 | 발생 내용, 단일 activity timeline | 조치 시작→처리 내용→조치 완료 | 조치 완료 시 "재검사 업무가 생성되었습니다" |
| `/quality/iqc` | 내 업무 `이동`, 알림 deep link | 해당 attempt 선택 상태 | 성적서 작성·부적합 증빙·최종화 | 권한 없으면 조회 전용, 무효 attempt는 empty/error |
| 패널 QR | 프로젝트 상세 설계/QR 영역 | 발급·가능·불가 상태, 선택 수 | 선택 발급, 선택 인쇄판, 행 inline `보기` | 발급 n·기존 m·실패 k 요약 |

확인할 UX 항목: 다음 행동이 한 화면에 하나로 명확한가, 저장 결과가 action 근처에 보이는가, 권한 부족·조회 전용·오류 상태가 구분되는가, 390px에서 page-level horizontal overflow 0과 44px touch target, checkbox label·disabled reason·screen reader announcement, inline preview의 `aria-controls`/focus 이동.

- 모바일 자재 하위 탭은 PC 표 축소가 아니라 `입고 관리`는 다음 행동·지연·IQC 상태, `키팅 관리`는 패널 준비 상태 compact card로 제공한다.
- Pending 화면에서 상태 select·임의 종결 버튼을 제거하고 현재 가능한 하나의 행동만 보여 준다(현재 구현도 next-action 단일 버튼이므로 `Closed` 노출 제거와 label 정리가 핵심).

## 7. 업무 규칙과 불변조건

- 18단계 workflow 완료 순서는 바꾸지 않는다. 설계·구매의 "선행 활성화"(내 업무 생성)와 "stage 완료"는 분리된 개념이다.
- 검사에서 생성된 Pending은 재검사 합격에서만 종결된다. material·panel 모두 서버에서 수동 Closed를 차단한다.
- 단계는 전진만 한다. 부적합은 차단 flag·Pending으로 표현하고 단계 번호를 되돌리지 않는다(Roadmap 9.5).
- 동일 이벤트 재실행 시 내 업무·알림 중복 0(`idempotency_key` 계약 유지).
- 상태·attempt·work item·notification·event는 transaction 경계 안에서 함께 성공하거나 rollback한다. 실제 provider 발송 성공은 포함하지 않고 기존 outbox(`notification_deliveries`) 계약을 보존한다.
- 인앱이 알림 원본이고 내 업무가 authoritative다. Pending history는 append-only다.
- 패널당 활성 QR 하나, opaque token, 민감정보 미포함.
- 이미 적용된 migration(`0001`~`0049`)은 수정·재번호하지 않는다. 신규 schema는 `0050`부터 additive로만 추가한다.
- 구매 완료 조건은 자재 도착·IQC·입고 확정 등 다음 부서 소유 단계를 요구하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| `material_receipts` | 도착분 상태(`Arrived→IqcRequested→Passed/FailedBlocked→…`) | 기존 | 이벤트 append-only 유지 |
| `material_iqc_attempts` | IQC 시도(회차·판정) | 기존 | Pending 종결 보호 검사 대상에 추가 |
| `panel_quality_inspection_attempts` | LQC/OQC/전진검수/FAT 시도 | 기존 | 기존 linked Pending 계약 유지 |
| `pending_issues` + history/comments | 조치 lifecycle | 기존 | timeline은 기존 두 원장의 병합 조회, 새 mutation 없음 |
| 재검사 handoff orchestration | `조치 완료` 전이와 attempt·work item·알림 생성 결합 | 신규(행동) | Pending history와 workflow event에 사유 기록 |
| 부적합 증빙 gate | 사진 또는 구조화 근거 판정 | 신규(검증 규칙) | 기존 report photo/응답 snapshot 재사용, 신규 저장소 없음 |
| QR batch 발급 | 패널 집합 멱등 발급 | 신규(행동) | 기존 append-only QR audit 재사용 |

```text
[material] Arrived ──(도착 등록과 동일 tx)──> IqcRequested ──> Passed ──> 입고 확정
                                                └─> FailedBlocked + Pending(Registered→…→InProgress)
Pending InProgress ──"조치 완료"(동일 tx: attempt+work item+알림)──> ReinspectionRequested
재검사 합격 ──(동일 tx)──> attempt Passed + receipt/panel 갱신 + Pending Closed + work item 완료
재검사 부적합 ──(동일 tx)──> 같은 Pending을 InProgress로 되돌림(신규 전이, history 기록), 새 Pending 0
```

재검사 부적합 반복을 위해 `ReinspectionRequested → InProgress`(검사 부적합 최종화 전용, 서버 내부 전이) 하나가 기존 단방향 `NextStatuses` 사슬에 추가된다. 사용자용 임의 역방향 전이는 열지 않는다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 조치 UI 한정·수동 종결 차단·증빙 gate·병행 활성화·완결성 판정 전부 서버 검증이 원본이고 UI 숨김은 보조다.
- 필요한 조회와 mutation(조사 대상, 확정 allowlist 아님):
  - `WorkflowStore`: 생산계획 완료 시 다음 stage 배열 처리(설계+구매), `LinkUrlForWorkItem` IQC 분기(`/quality/iqc?attempt={id}` 형태), `ProcurementStatus` 완결성 기준.
  - `MaterialsStore`: `RegisterArrivalAsync`가 같은 transaction에서 `CreateIqcAttemptAsync` 경로를 호출. 기존 `RequestIqcAsync`·`RequestReinspectionAsync`는 legacy 복구용 멱등 경로로 유지.
  - `PendingStore`: `IsQualityInspectionPendingAsync` 확장, `ReinspectionRequested` 전이 시 대상별(material/panel) handoff orchestration 호출, timeline 조회, allowedTransitions에서 검사 연계 Pending의 `Closed` 제외.
  - `QualityInspectionStore`/`IqcReportStore`: finalize의 공통 증빙 gate, 재검사 부적합 시 Pending 되돌림, 재검사 work item을 품질 단계 정담당(fallback 순서)에 배정하고 정·부 알림 생성.
  - QR: 발급 batch endpoint(패널 ID 집합, 패널별 멱등 처리, 요약 응답).
- 권한·validation: 기존 permission code(`quality.inspect`, `Pending.Manage`, `MaterialReceipt.Update` 등)와 담당자 해석(`ResolveAssigneeAsync`, Roadmap 5장 fallback 순서)을 재사용한다. 신규 권한 능력은 만들지 않는다.
- transaction·동시성·idempotency: 재검사 handoff는 `pending:{id}:reinspection:{attemptNumber}` 형태의 결정적 키, QR batch는 패널별 활성 QR unique 제약으로 수렴, CAS(version/row_version) 계약 유지.
- audit trail: Pending history·material receipt event·workflow event·QR audit 모두 append-only 유지, 자동 인계도 actor·사유를 기록.
- 외부 provider 영향: 없음. 인앱 원본과 기존 outbox 계약만 사용한다. `재검사 요청`은 채널 matrix상 인앱 즉시·메일 미발송을 유지한다.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 이 문서로 확정하지 않는다(위 명칭은 현재 코드에서 확인한 재사용 후보다).

## 10. Frontend 고려사항

- route/component: `frontend/src/App.tsx`(기본 진입 탭, `/quality/iqc` query 파싱, 자재 하위 탭 라우팅), `MaterialsWorkspace.tsx`(도착 등록 후 IQC 요청 버튼 제거·legacy 복구 action), `PendingPage.tsx`(단일 next action label·timeline 병합), `PanelQrManager.tsx`(선택 모델·batch·inline preview), 기존 `MaterialIqcPage`·`PanelKittingPage` 재사용.
- loading/empty/error/success: 기존 AsyncState·action feedback 패턴(TASK-UX-001 A1/A2 계약)을 재사용하고 새 화면도 4상태를 모두 갖춘다.
- 공통 Action Feedback: 자동 인계 결과("IQC 검사 대기로 넘겼습니다", "재검사 업무가 생성되었습니다")를 저장 버튼 근처에 표시한다.
- 접근성: checkbox label·disabled 사유 표기, inline preview `aria-controls`·focus 관리, timeline의 시간순 낭독, role=status 유지.
- 390px/mobile/narrow pane: 하위 탭 compact card, Pending 단일 행동 버튼 hit area 44px, QR 목록 카드형 유지, page-level horizontal overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 내 업무·알림 생성은 기존 `work_items`/`notifications` 계약과 idempotency key 패턴을 재사용한다. 자동 인계로 인해 기존 알림 preference(NOTIFY-005)·감사(NOTIFY-AUDIT-001)·재처리(NOTIFY-REPROCESS-001) 계약을 바꾸지 않는다.
- 권한/관리자: 신규 권한·역할 없음. template 관리(TASK-ADMIN-002)의 `requires_photo` 계약은 유지하고 그 위에 부적합 공통 gate를 추가한다.
- Excel/PDF/첨부: IQC·panel 성적서 PDF와 photo snapshot 계약 유지. 선택 Excel export 화면 등록은 변경 없음.
- Teams/Mail: outbox까지만. 채널 matrix 변경 없음.
- 삭제·복구/감사: purge cascade에 신규 테이블이 생기면 포함(현재 안은 신규 테이블 없이 가능할 것으로 보이며, 필요 시 `0050` additive와 purge 경로 추가를 함께 검토).

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 기존 store 확장: 같은 transaction 내 orchestration 함수 호출, 신규 테이블 없이 행동·검증만 추가 | 기존 계약·감사·purge 영향 최소, migration 없거나 최소 | store 간 호출 결합도 증가 — 기존 `MaterialsStore`↔`PendingStore` 상호 호출 패턴이 이미 있어 수용 가능 |
| B | 별도 handoff orchestrator service + event 테이블 신설 | 결합도 낮음, 재처리 관측 용이 | 신규 상태 원장 추가로 정합성 검증 면적 확대, 이번 범위에 과설계 |
| C | Frontend에서 순차 API 2회 호출(조치 완료 후 재검사 요청) | 구현 최소 | 부분 실패로 현재 결함(끊긴 상태)이 재현됨 — 인터뷰 확정 9 위반, 채택 불가 |

권장안 A. 근거: 인터뷰가 "같은 transaction 또는 멱등 orchestration"을 확정했고, 현재 코드가 이미 도착·IQC·Pending을 단일 transaction에서 교차 갱신하는 패턴(`RecordIqcResultAsync` → `pendingStore.CreateOrReuse…`)을 사용한다.

### 7장 비차단 정책 권장안 (Fable 2차에서 자동 채택 대상)

1. **구매 완료 최소 필수 field** — 권장: 활성 품목 공통 필수 = 발주품목명·공급구분·수량+단위·입고예정일. 도급(구매) 품목 추가 필수 = 업체명·발주일. 사급(`CustomerSupplied`)은 업체명·발주일 면제(고객 제공 성격), 기술 담당·이슈는 선택. required template setting이 있으면 template match 조건과 AND. 이미 `Completed` 판정(또는 StageCompleted 이벤트 존재)인 기존 프로젝트는 회귀시키지 않는다. 대안(현행+수량만 추가)은 예정일 없는 품목이 입고 관리 비교표를 무의미하게 만들어 비권장.
2. **QR batch 발급** — 권장: 단일 checkbox 선택 모델 + `선택 발급`·`선택 인쇄판` 두 action. 발급은 서버 batch endpoint에서 패널별 멱등 처리(이미 활성 QR은 skip-as-success로 집계), 상한 50(기존 인쇄 상한과 일치), 응답은 발급 n·기존 m·실패 k와 실패 행. all-or-nothing rollback은 부분 재시도 UX가 나빠 비권장. 활성 QR unique 제약이 동시성 안전판이다.
3. **부적합 증빙 최소 모델** — 권장: 부적합 최종화 시 (report 사진 1장 이상) 또는 (구조화 근거 텍스트 30자 이상) 중 하나를 서버에서 요구. 기존 photo 테이블·판정 사유 필드 재사용, 신규 저장소·컬럼 없음. legacy 간편 판정(`DecisionMode=Legacy`) attempt는 소급 차단하지 않고 신규 최종화에만 적용. 사진 무조건 필수안은 template의 항목별 사진 계약과 충돌하고 현장 예외가 있어 비권장.
4. **조치 완료 자동 handoff** — 권장: `ReinspectionRequested` 전이 transaction 안에서 대상 판별(material receipt / panel stage) 후 재검사 attempt·품질 해당 단계 정담당 work item(Blocking)·정담당 배정 알림·부담당 참조 알림을 생성. 담당 부재 시 Roadmap 5장 fallback 순서. idempotency key는 `pending:{id}:reinspection:{n}` 계열로 재실행 수렴. panel 재검사 work item의 현행 "호출자 본인 배정"을 정담당 배정으로 교정한다.
5. **재검사 부적합 반복** — 권장: 같은 open Pending 재사용. 재검사 부적합 최종화 transaction에서 Pending을 `InProgress`로 되돌리고(전용 내부 전이, history에 회차·사유 기록) 새 Pending을 만들지 않는다. Closed 후 재오픈안은 append-only 해석을 해쳐 비권장.
6. **Pending activity timeline** — 권장: comments와 history를 서버(또는 detail 응답 가공)에서 시간 오름차순 단일 timeline으로 병합해 표시하고, 전이 reason(처리 내용)을 timeline 항목으로 노출. filter chip(전체/상태/코멘트)은 선택 범위. 기존 두 원장의 저장 구조는 바꾸지 않는다.
7. **기존 endpoint·row 호환** — 권장: `RequestIqcAsync`·material `RequestReinspectionAsync`를 제거하지 않고 신규 orchestration과 같은 내부 경로를 쓰는 멱등 legacy 복구 action으로 유지. 기존 `Arrived` row는 입고 관리 화면의 legacy `IQC 요청` action으로, 기존 `FailedBlocked + ReinspectionRequested` row는 재검사 handoff 멱등 재실행으로 복구. 이미 잘못 Closed된 IQC Pending은 자동 backfill하지 않고, open Pending이 없는 `FailedBlocked` receipt에 한해 명시적 `재검사 재개` action이 새 attempt를 생성하며 이벤트에 legacy 복구 사유를 남기는 운영/fixture 복구 경계를 둔다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. 검증은 disposable runtime·전용 DB만 사용.
- migration 필요 여부: 원칙적으로 불필요(행동·검증 추가 중심). 조사 중 컬럼이 필요해지면 `0050`부터 additive로만 추가하고 fresh/existing 모두 검증. `0001`~`0049` 불변.
- 외부 발송/실제 데이터 영향: 실제 provider 호출 없음. outbox row 생성까지만.
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: push·PR·merge·대표 repo 반영·Persistent UAT — 모두 이 Task 범위 밖(기존 change-001 경계 유지). local experiment commit만 승인됨.
- Bookkeeping finding (비차단, Codex review에서 확정 요청): `tasks/workflow-continuity-001-change-001.md`의 기존 Task change 번호 중 일부가 현재 `tasks/` 파일과 어긋난다 — `TASK-007A`는 change 파일이 없는데 `Change 004`로, `TASK-009A`·`TASK-012A`는 `change-001.md`가 이미 있는데 `Change 001`로 기재되어 있다(`TASK-008A Change 002`·`TASK-QR-001 Change 002`·`TASK-E2E-FULL-SUITE-001 Change 007`은 일치). 권장: Codex가 "다음 사용 가능 번호"(007A→001, 009A→002, 012A→002)로 정정 기록한다. 제품 정책에는 영향이 없다.

## 14. 검증 계획

- 최소 테스트: 병행 활성화(설계·구매 work item 2건·중복 0), 도착→IQC 자동 인계 transaction, material Pending 수동 Closed 차단, `조치 완료` handoff 멱등성(반복·동시 호출 수렴), 재검사 합격 원자 갱신·부적합 Pending 재사용, 증빙 gate(사진/텍스트/누락), 구매 완결성 판정(도급/사급/기존 Completed 비회귀), QR batch(혼합 선택·상한·중복), IQC deep link 분기.
- 영향 영역 회귀: Backend 전체 test(현재 기준선 410건 + 신규), Frontend lint/typecheck/unit(111건 + 신규)/build, migration fresh/existing isolated 검증.
- Full-Stack: 기존 실제 역할 lifecycle E2E를 확장해 도착→IQC 부적합→조치 시작→조치 완료(자동 재검사)→재검사 합격→입고 확정→후속 단계→최종 완료를 한 시나리오로 검증. QR isolated E2E에 선택 batch 발급·inline preview 추가.
- 사용자 검수: desktop과 390px 페이지별 screenshot(전체 흐름 첫 진입, 자재 하위 탭 2종, Pending 상세 timeline, IQC deep link 선택 상태, QR 선택 발급·preview). 상태는 `사용자 검수 대기 — 마지막 일괄 검수`로 유지.

## 15. 완료 기준

- 기능/권한/데이터: 인터뷰 8장 성공 기준 전부 — 수동 우회 버튼 없이 무단절 진행, 수동 종결 서버·UI 불가, 조치 완료 1회로 정·부 알림·단일 내 업무, 재검사 합격 후 정합 유지·`미시작` 회귀 없음, 구매 완결성·중복 차단 없음, QR batch·inline preview keyboard/mobile 동작.
- UX: 4상태 feedback·접근성·390px overflow 0.
- 자동 테스트: Backend 전체·Frontend lint/typecheck/unit/build·migration fresh/existing·Full-Stack lifecycle·QR isolated E2E 통과.
- 5종 산출물: Implementation report에 상태·위치 추적(SOP/manual은 기존 문서 갱신 또는 N/A 사유 기록).
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- PR 상태: 없음(local experiment commit만).

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1~7 | 12장의 비차단 정책 7건 | 각 항목의 권장안·대안 참조 | 재질문 없음 — 실험 standing rule에 따라 Codex review를 반영한 Fable 2차 기획이 권장안을 자동 채택 |

사용자에게 새로 물을 blocking 결정은 없다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Workflow/WorkflowStore.cs`, `Materials/MaterialsStore.cs`, `Materials/IqcReportStore.cs`, `Pending/PendingStore.cs`(+contracts), `QualityInspections/QualityInspectionStore.cs`, Procurement 완료 facts 산출부, QR 발급 endpoint/store
- Frontend: `App.tsx`(진입 탭·라우팅·deep link·자재 하위 탭), `MaterialsWorkspace.tsx`, `PendingPage.tsx`, `PanelQrManager.tsx`, `api.ts`, 관련 CSS
- DB/Migration: 기본 없음, 필요 시 `0050` additive
- Tests/Scripts: Backend targeted·통합 test, Frontend unit, Full-Stack lifecycle·QR E2E 확장
- Docs: 관련 기존 Task change 문서(13장 번호 정정 포함), Roadmap·완료 원장 동기화, Implementation report

## 18. Roadmap 연결

- 선행 Task: `TASK-007A`·`TASK-008A`·`TASK-009A`·`TASK-012A`·`TASK-QR-001`·`TASK-E2E-FULL-SUITE-001`(모두 `EXPERIMENT_COMPLETE`, 재구현 금지 — 이번 결함·화면 수정은 각 Task의 다음 change로 추적).
- 후속 Task: 첨부·사진 storage 정책(원장 우선순위 1), 운영 전환.
- 현재 Go/No-Go: 사용자 검수 실패와 명시 지시에 따른 explicit roadmap override가 interview에 기록됨(`gateStatus: PASS_CREATE`).
- 별도 Task로 분리할 항목: template 현업 content, 알림 채널 matrix 변경, 과거 데이터 일괄 보정.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-20 | 사용자 검수 실패와 14개 확정 결과, 실험 fast-track 지시 | interview `COMPLETED_CONFIRMED` 입력으로 이 1차 기획 작성 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인 — 실험 규칙상 Codex review + Fable 2차 기획(blocking 0)으로 대체
- [ ] 포함·제외 범위 승인 — 동일
- [ ] 시나리오와 권한·업무 규칙 승인 — 동일
- [ ] UI/UX 방향 승인 — 동일
- [ ] Task 고유 안전 경계 승인 — 동일
- [ ] 검증·사용자 체크리스트 승인 — 동일
- [ ] Codex 구현 프롬프트 작성 승인 — Fable 2차 기획 완료 후

### Codex 구현 지시문 초안 (2차 기획 확정 후 사용)

1. 이 planning과 Codex review, Fable 2차 기획(`docs/37-workflow-continuity-plan.md`)을 source of truth로 읽고, 12장 정책 7건의 확정안을 그대로 구현한다.
2. Backend부터: 수동 종결 차단 확장 → 도착-IQC 동일 transaction 인계 → `조치 완료` handoff orchestration → 재검사 부적합 Pending 재사용 → 증빙 gate → 병행 활성화·구매 완결성 → deep link 분기 → QR batch 순으로, 각 단계마다 targeted test를 추가·통과시킨다.
3. Frontend는 진입 탭·자재 하위 탭·Pending 단일 행동/timeline·IQC deep link·QR 선택 발급을 구현하고 기존 action feedback·접근성 계약을 재사용한다.
4. Full-Stack lifecycle E2E를 부적합→조치→자동 재검사→합격 경로로 확장하고 desktop/390px screenshot을 페이지별로 남긴다.
5. 관련 기존 Task의 다음 change 문서(13장 번호 정정 반영)·Implementation report·완료 원장·Roadmap을 동기화한 뒤 승인된 allowlist만 stage해 local experiment commit 한다. push·PR·merge·Persistent UAT·실제 provider는 수행하지 않는다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 0
