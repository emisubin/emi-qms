All baseline evidence is confirmed: the 18-stage workflow map, the 011A panel LQC skeleton handoff, the 009A IQC report/photo/PDF immutability contract, the Pending reinspection gate, `projects.fat_required` optional-FAT handling, and the current global `IQC` navigation entry. I now have everything needed to produce the single first planning draft for TASK-012A.

# TASK-012A — LQC·제조 완료 확인·OQC·전진검수·FAT 후속 품질 검사 기획안

> 상태: Draft
> 작성 단계: Codex 내용 review 전 Fable 1차 기획
> 목적: 제조 완료 이후 panel 단위 품질 검사·판정·Pending 재검사·다음 단계 handoff 계약을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/012a-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 제조 실행 완료 시 panel target `LQC` 내 업무는 생성되지만(설명문이 "상세 검사 화면은 TASK-012A에서 연결됩니다"인 skeleton), LQC 이후 검사 record·전용 화면·판정·PUNCH·재검사·다음 업무 handoff가 없다. generic 내 업무 완료로는 검사 증빙과 Pending 불변조건을 보장할 수 없다.
- 대상 사용자·역할: LQC 정·부(`QualityLQC`/`QualityLQCSecondary`), 제조 정·부(제조 완료 확인), OQC 정·부, 전진검수/FAT 정·부(`QualityCustomerInspection` 계열), Pending 조치 담당자, 생산관리·영업·자재·물류·조회 역할(조회 전용), System Administrator(우회 금지).
- 정상 흐름: panel LQC skeleton → LQC 성적서 합격 → 제조 완료 확인 업무 → 제조 확인 → OQC 합격 → 전진검수 합격 → FAT 필요 시 FAT 합격, 불필요 시 skip → 포장 skeleton handoff.
- 예외·복구 흐름: 부적합/PUNCH는 finalized 실패 report + Panel target Pending으로 차단하고, Pending `ReinspectionRequested` 이후 새 attempt로 재검사한다. 완료 성적서 수정·삭제·단계 후퇴는 없다.
- 확정한 정책과 명시적 제외: Backend authoritative 권한·project scope, 18단계 순서, panel 단위 전진-only, FAT optional, finalized 증빙 append-only, 재검사는 새 attempt. 제외 — 실제 현업 양식·사진 필수 위치 확정, template 관리자(TASK-ADMIN-002), 고객 PDF 양식·전자서명·Excel, 물류 상세(TASK-013A), Persistent UAT·실제 provider·게시.
- planning으로 넘긴 비차단 미결정 사항: 실제 stage별 checklist 문항·필수 사진 위치, 운영 storage·retention, template 관리·활성화 정책, 실제 고객 PDF 양식. interview 7장 선택 6건은 사용자 standing rule에 따라 Fable 권장안 자동 채택으로 기록되어 있다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

품질 담당자가 제조 완료된 panel을 모바일에서 열어 LQC·OQC·전진검수·선택 FAT 성적서를 작성·판정하고, 부적합/PUNCH를 Pending 재검사로 연결하며, 합격 panel을 다음 18단계 업무로 정확히 한 번 인계할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 제조 완료 후 LQC~FAT 검사 결과를 종이·사진·메신저로 관리하고, 시스템의 LQC 업무는 링크가 임시 workflow fallback인 skeleton으로만 존재한다.
- 시간 손실·누락 지점: 검사 결과와 판정 근거가 시스템 밖에 있어 부적합·PUNCH 이력, 재검사 여부, 다음 단계 인계 시점이 추적되지 않는다. generic 내 업무 완료로 단계를 건너뛸 수 있다.
- 현재 우회 방식: 검사·고객 지적을 수기로 관리하고 workflow는 stage 이름만 표시한다.
- 이 기능이 없으면 18단계 중 10~14단계가 시스템 밖에 남고, 프로젝트 진행률·Pending 차단·물류 인계(TASK-013A)가 실제 검사 근거 없이 진행된다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| LQC 정·부 담당 | LQC queue 조회, 성적서 작성·사진·최종 판정, 재검사 시작 | 기존 project access scope | 활성 LQC attempt/draft report만 (`quality.inspect`) |
| 제조 정·부 담당 | LQC 합격 panel의 제조 완료 확인 | 기존 project access scope | 제조 완료 확인 업무만 (`manufacturing.update`) |
| OQC 정·부 담당 | OQC 성적서 작성·판정·재검사 | 기존 project access scope | 활성 OQC attempt만 (`quality.inspect`) |
| 전진검수/FAT 정·부 담당 | 전진검수·FAT 결과, PUNCH 등록, 재검사 | 기존 project access scope | 활성 CustomerInspection/FAT attempt만 (`quality.inspect`) |
| Pending 조치 담당자 | 부적합/PUNCH 조치·상태 전이·댓글 | 기존 Pending scope | 기존 Pending 계약 범위 (`Pending.Manage`) |
| 생산관리·영업·자재·물류·조회 역할 | 검사 상태·finalized 증빙·PDF 조회 | 기존 project access scope | 검사 mutation 불가 |
| System Administrator | 기준·이력 조회 | 기존 관리자 정책 | 검사·제조 확인 입력 무제한 우회 금지, 서버 authorization 유지 |

모든 mutation·조회·사진/PDF download는 UI 숨김이 아니라 서버 Policy(`quality.inspect`, `manufacturing.update`, project access scope)로 강제한다. 이는 기존 IQC 성적서(TASK-009A)와 제조 실행(TASK-011A)에서 확인된 계약의 연장이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — LQC 합격과 제조 완료 확인

1. LQC 담당자가 내 업무의 `LQC 입력 · <panel 표시코드>` deep link 또는 전역 `품질` 메뉴 LQC tab에서 panel을 연다.
2. 시스템이 활성 attempt가 없으면 attempt 1과 draft 성적서를 idempotent하게 생성하고, stage system template 항목(체크·값·선택 사진)을 보여준다.
3. 담당자가 항목을 확인하고 사유를 입력해 `합격` 최종화하면, 시스템이 한 transaction에서 report를 finalize(canonical snapshot·hash)하고 attempt를 `Passed`로 전이하며 LQC 업무를 완료하고 제조 정담당자에게 panel target `제조 완료 확인` 업무를 정확히 한 번 생성한다. 모든 active panel이 LQC를 통과하면 project `LQC` stage event를 exactly-once 기록한다.
4. 제조 담당자가 제조 화면의 완료 확인 카드(또는 내 업무 link)에서 `제조 완료 확인`을 실행하면, 시스템이 해당 업무를 완료하고 OQC 담당자에게 panel target `OQC` 업무를 정확히 한 번 생성한다.
5. 사용자는 finalize 직후 판정 결과·PDF 상태·다음 담당 단계를 action 인접 영역에서 확인한다.

### 시나리오 B — OQC 부적합과 Pending 재검사

1. OQC 담당자가 OQC 성적서에서 실패 항목과 사유를 입력해 `부적합` 최종화한다.
2. 시스템이 한 transaction에서 report를 finalize하고 attempt를 `FailedBlocked`로 전이하며, panel target `Nonconformance` Pending(긴급)을 생성·연결하고 현재 OQC 업무를 완료한다. panel은 OQC 단계에서 차단된다.
3. 조치 담당자가 Pending을 조치하고 상태를 `재검사 요청`으로 전이한다.
4. OQC 담당자가 재검사 시작을 실행하면, 시스템이 연결 Pending의 `ReinspectionRequested` 상태를 검증한 뒤 새 attempt 2와 새 OQC 업무를 생성한다. 기존 실패 report·사진·PDF는 불변으로 남는다.
5. 재검사 합격 최종화 시 같은 transaction에서 연결 Pending을 종결하고 다음 단계(전진검수) 업무를 생성한다.

### 시나리오 C — 전진검수 PUNCH와 FAT skip

1. 전진검수 담당자가 panel 검수 결과에서 PUNCH를 확인하고 `PUNCH 차단`으로 최종화하면, panel target `Punch` Pending이 생성되고 panel이 차단된다. 조치·재검사 흐름은 시나리오 B와 같다.
2. 전진검수 합격 시 시스템이 project `fat_required`를 확인한다. FAT 필요 프로젝트는 전진검수/FAT 담당자에게 panel target `FAT` 업무를 생성하고, FAT 불필요 프로젝트는 FAT를 건너뛰어 물류 정담당자에게 panel target `포장` skeleton 업무를 생성하며 panel coarse stage를 `InspectionCompleted`로 전진한다.
3. FAT 필요 프로젝트에서는 FAT 합격 시 같은 포장 skeleton handoff가 실행된다. FAT PUNCH는 시나리오 C-1과 동일하게 처리한다.

## 5. 기능 요구사항

### 필수

- [ ] panel+stage 공통 quality inspection attempt 모델: stage `LQC|OQC|CustomerInspection|FAT`, panel+stage당 active attempt 최대 1건, 재검사는 attempt 번호 증가
- [ ] stage별 소수 항목의 일반 system template(v1) snapshot 기반 성적서: 체크/값 입력, 선택 사진(bounded JPEG/PNG), 최종화 시 canonical snapshot·hash·저장 PDF — TASK-009A IQC 계약 재사용
- [ ] finalize 판정: `합격` / `부적합`(LQC·OQC) 또는 `PUNCH 차단`(전진검수·FAT), 사유 필수, finalized 이후 append-only 불변
- [ ] 부적합→`Nonconformance`, PUNCH→`Punch` panel target Pending 생성·연결, `ReinspectionRequested` 검증 후 새 attempt·새 업무 생성, 재검사 합격 시 같은 transaction에서 Pending 종결
- [ ] LQC 합격 → 제조 완료 확인 업무(18단계 11번, 제조 담당) → 확인 시 OQC 업무 생성. 11번을 자동 skip하지 않는다
- [ ] 전진검수/FAT 합격 → FAT 필요 여부에 따른 FAT 업무 또는 포장 skeleton 업무 handoff, FAT 비대상 자동 skip
- [ ] panel별 다음 업무는 즉시(exactly-once, idempotency key) 생성하고, project stage event는 해당 stage를 모든 active panel이 통과할 때 exactly-once 기록
- [ ] generic `/api/my-work` start/complete/cancel이 panel 품질·제조 완료 확인 업무를 우회하지 못하게 conflict 안내(TASK-011A `ManufacturingWork` 차단 패턴 확장)
- [ ] 전역 `IQC` 메뉴를 전역 `품질` 진입으로 개편: IQC(기존 workspace 재사용)와 LQC/OQC/전진검수/FAT stage tab, 내 업무 deep link는 정확한 stage·panel을 연다
- [ ] 모바일 우선 adaptive 품질 화면(단계 queue → panel focus → 항목→사진/근거→판정), desktop stage queue+detail 병렬, 390px/Teams narrow page-level overflow 0
- [ ] 모든 판정 transaction: report finalize·attempt 전이·panel coarse stage·업무 완료·Pending 또는 다음 업무·project event를 하나의 transaction으로 처리, PDF rendering은 판정 후 derived artifact 상태(`Pending/Ready/Failed`+retry)로 분리
- [ ] optimistic version·row lock·operation idempotency(동일 payload 재시도는 동일 결과 replay, TASK-011A operation receipt 패턴)

### 선택

- [ ] 제조 완료 확인 카드에 LQC 성적서 PDF 바로보기 link
- [ ] 품질 stage tab별 상태 count badge

### 명시적 제외

- [ ] 실제 현업 LQC/OQC/FAT 상세 양식·사진 필수 위치의 임의 확정(모든 v1 항목은 사진 선택)
- [ ] 관리자 template 편집·version activation(TASK-ADMIN-002)
- [ ] 실제 고객 PDF 양식·전자서명·승인 workflow·Excel export
- [ ] 완료 성적서 수정·삭제·재발행·관리자 강제 합격·stage 되돌리기
- [ ] 독립 PUNCH 항목별 개별 Pending 관리 고도화(아래 12장 권장안 참조)
- [ ] object storage·CDN·image transcoding, 신규 외부 알림 채널·실제 provider delivery
- [ ] 물류 포장 상세(TASK-013A), 영업 정산, Persistent UAT migration·runtime handover, 대표 repo·`main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 품질 stage queue (mobile) | 좌상단 숨김 메뉴 `품질` → stage tab | stage별 대기/진행/차단/완료 panel card, 프로젝트 묶음, 상태 도형 | stage 전환, panel 선택 | queue loading/empty/error와 재시도 |
| panel 검사 focus (mobile) | queue의 panel card, 내 업무 deep link | 현재 stage·attempt 번호·차단 Pending·항목 진행률 | 검사 시작, 항목 체크·값 입력, 사진 추가, 판정 sheet 열기 | 저장 중·저장됨·오류를 항목 인접 표시, 중복 submit 차단 |
| 판정 sheet (mobile, `MobileSheet` 재사용) | focus 화면 `판정` | 필수 누락 항목, 사유 입력, 합격/부적합(또는 PUNCH) | 최종화 실행 | 판정 결과·Pending 생성·다음 담당 단계·PDF 상태를 action 인접 표시 |
| 품질 workspace (desktop) | 전역 `품질` | stage rail + panel queue + 성적서 detail·이력·PDF 병렬 | 조회, 검사 입력, 판정, PDF 조회/재시도 | 동일 피드백 + finalized 성적서 read-only 표시 |
| 제조 완료 확인 카드 | `제조` 화면·내 업무 link | LQC 합격 panel 목록, LQC 판정 요약 | `제조 완료 확인` 실행 | 확인 완료와 OQC 인계 안내 |
| Pending 연동 | 기존 Pending 목록/상세 | panel 표시 코드·유형(`Nonconformance`/`Punch`)·재검사 상태 | 기존 Pending 계약 그대로 | 기존 피드백 유지 |

확인할 UX 항목:

- 사용자가 panel의 현재 검사 단계·차단 사유를 첫 화면에서 이해할 수 있는가
- 판정 후 "다음이 누구의 업무인지"가 action 근처에 표시되는가
- 권한 부족·조회 전용·이미 판정됨·선행 단계 미완료 오류가 한글로 해당 위치에 표시되는가
- 390px·Teams narrow에서 44px touch target·page-level overflow 0·좌상단 숨김 메뉴가 유지되는가
- 상태 도형 체계(시작 전 각진 사각·진행 rounded·차단 타원·완료 원형)가 제조 화면과 일관되는가 — PC 축소가 아닌 현장 행동 재구성 원칙(mobile-002 도형 체계) 유지

## 7. 업무 규칙과 불변조건

- 18단계 순서 보존: `LQC(10) → 제조 완료 확인(11) → OQC(12) → 전진검수(13) → FAT(14, 선택) → 포장(15)`. 선행 stage 미통과 panel에는 후행 attempt·업무를 만들 수 없고, 11번 확인을 자동 skip하지 않는다.
- panel 전진-only: `panel_placeholders.workflow_stage`는 `ManufacturingCompleted → InspectionInProgress → InspectionCompleted`로만 전진한다. 부적합/PUNCH 실패는 coarse stage를 후퇴시키지 않고 Pending으로 차단 상태를 표현한다.
- panel+stage당 active attempt 최대 1건(partial unique), 다음 업무·Pending·project event는 idempotency key·ensure 패턴으로 중복 생성을 차단한다.
- finalized report·response·사진·snapshot·PDF는 DB trigger 수준까지 불변(append-only)이며(0032 IQC guard trigger 패턴 재사용), 재검사는 항상 새 attempt다.
- 부적합/PUNCH attempt는 open Pending과 1:1로 연결되고, 새 attempt 시작은 연결 Pending의 `ReinspectionRequested` 상태를 서버가 검증한다. 재검사 합격은 같은 transaction에서 연결 Pending을 종결한다.
- FAT는 `projects.fat_required`가 true인 프로젝트에서만 생성한다. FAT 비대상 프로젝트에서 FAT attempt·업무 생성은 서버가 거부한다.
- project stage event는 stage별로 모든 active panel 통과 시 exactly-once 생성하며, panel 취소·project 취소 시 활성 attempt는 terminal 처리하고 집계에서 제외한다(011A 취소·purge 패턴 준용).
- 판정 core transaction과 PDF rendering을 분리한다. PDF 실패는 판정을 되돌리지 않고 `Failed`+재시도로 관리한다.
- System Administrator를 포함해 누구도 검사 mutation 권한·project scope·단계 순서를 우회하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| panel quality inspection attempt | panel×stage×attempt 번호, 상태, 연결 Pending, version | 신규 | append-only 이력, terminal 후 불변 |
| stage system template snapshot | stage별 소수 일반 항목 v1 (체크/값, `requires_photo`=false) | 신규 데이터(기존 template 구조 재사용 후보) | version 고정, finalize 시 snapshot 참조 |
| inspection report·response·사진 | attempt당 성적서 1건, 항목 응답, 선택 사진(JPEG/PNG ≤5MB) | 신규(0032 계약 복제·일반화) | finalized 후 trigger 불변 |
| canonical snapshot·PDF artifact | finalize 시 text snapshot+sha256, 저장 PDF | 신규(0032 계약 재사용) | PDF artifact immutable trigger |
| 제조 완료 확인 업무 | 18단계 11번 panel target work item | 기존 work_items 재사용 | 기존 업무 이력·idempotency |
| Pending link | `Nonconformance`/`Punch` panel target, 재검사 상태 | 기존 pending_issues 재사용 | 기존 history append-only |
| operation receipt | mutation replay·fingerprint | 신규(011A 패턴 재사용) | append-only |
| project workflow event | stage 통과 exactly-once event | 기존 재사용 | append-only |

```text
attempt:  Requested → InProgress(draft 성적서) → Passed
                                   └→ FailedBlocked ─(Pending ReinspectionRequested)→ 새 attempt Requested
panel coarse: ManufacturingCompleted → InspectionInProgress → InspectionCompleted (전진-only)
report:   Draft → Finalized(Passed|Failed) / PDF: Pending → Ready|Failed(재시도)
```

## 9. API·Backend 고려사항

Repository 조사로 확인한 기존 계약을 우선 재사용하되, 최종 내부 클래스·컬럼명은 Codex 구현에서 확정한다.

- Backend가 authoritative해야 하는 규칙: stage 순서·선행조건, active attempt 단일성, FAT 필요 여부, finalized 불변, Pending 재검사 gate, panel/project scope와 권한, exactly-once handoff.
- 필요한 조회: stage별 queue(project scope 필터), panel 검사 상세(활성 attempt·template·이력·Pending), finalized 성적서·사진 content·PDF. 필요한 mutation: 검사 시작(attempt+draft ensure), 응답 저장, 사진 등록/삭제(draft 한정), finalize, 재검사 시작, PDF retry, 제조 완료 확인.
- 권한·validation: 품질 mutation `quality.inspect`, 제조 완료 확인 `manufacturing.update`, 조회·download는 project access scope. 오류는 항목 인접 표시 가능한 한글 field error로 반환한다(기존 `WorkflowMutationResult`/validation 계약 준용).
- transaction·동시성·idempotency: row lock+expected version, operation id·payload fingerprint receipt(011A `panel_manufacturing_operations` 패턴), work item·Pending·event idempotency key(예: `quality:panel:{panelId}:{stage}:...` 계열 — 확정은 구현에서), 동일 성공 replay.
- audit trail: attempt event 또는 상태 이력, finalize actor/시각, Pending history·업무 이력 재사용. 완료 판정과 사용자 검수 상태를 혼동하지 않는다.
- 외부 provider 영향: 없음. 기존 인앱 work item·notification 경로만 재사용하고 실제 Teams/Mail provider는 disabled 상태를 유지한다.
- generic 내 업무 우회 차단: 011A의 panel `ManufacturingWork` conflict 처리를 LQC·ManufacturingCompleted·OQC·CustomerInspection·FAT panel 업무로 확장하고 품질/제조 화면 안내를 반환한다.
- 담당자 해석: 011A `ResolveQualityAssigneeAsync`의 responsibility+`quality.inspect` fallback 패턴을 stage별 responsibility(`QualityOQC`, `QualityCustomerInspection` 등)로 일반화한다. 제조 완료 확인은 `ManufacturingPrimary/Secondary` 해석을 재사용한다.

## 10. Frontend 고려사항

- route/component: 전역 nav의 `IQC` 항목을 `품질`로 개편하고 view kind를 확장(IQC tab은 기존 `IqcReportWorkspace` 재사용, panel stage tab은 신규 품질 검사 컴포넌트). `제조` 화면에 완료 확인 카드 추가. 내 업무 link URL을 stage·panel 정확 deep link로 교체.
- loading/empty/error/success: queue·상세·finalize·PDF 각각 상태를 갖고, 실패 재시도는 동일 operation id 유지(입력 변경 시 새 id — 011A 패턴).
- 공통 Action Feedback: 판정·확인 버튼 인접에 결과·다음 행동 표시, 진행 중 중복 submit 차단.
- 접근성: 44px touch target, 작은 보조 글씨는 라벨·대비 유지, 오류 한글 안내와 항목 인접 배치.
- 390px/mobile/narrow pane: compact stage rail·card·sheet 구성, page-level horizontal overflow 0, 좌상단 숨김 메뉴 유지, bottom navigation 미추가.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 기존 work_items·notification 원본 구조와 stage 담당·fallback 규칙 재사용. project workflow 요약·진행률은 stage event로 자동 반영.
- 권한/관리자: 기존 `quality.inspect`·`manufacturing.update`·`Pending.*`·project scope 재사용, 신규 권한 코드 추가 없음(권장). 관리자 template 편집은 TASK-ADMIN-002로 유지.
- Excel/PDF/첨부: 0032의 bounded 사진·canonical snapshot·PDFsharp 저장 PDF·immutability trigger 계약 재사용. Excel 없음.
- Teams/Mail: 신규 채널 없음. provider disabled 검증 유지.
- 삭제·복구/감사: panel/project 취소 시 활성 attempt terminal 처리, permanent purge는 신규 table을 FK 역순으로 기존 승인된 purge 경로에 편입(011A 패턴).

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | panel+stage 공통 attempt/report 신규 table + 기존 template table 구조 재사용(신규 stage template 코드 4종 seed), IQC와는 데이터 분리 | 4개 stage 일관 처리, 구매품목 FK와 비결합, TASK-ADMIN-002가 하나의 template 구조를 관리 | 신규 table 수 증가, template table의 `iqc_` 명명과 실제 범위의 불일치(문서화 필요) |
| B | `material_iqc_attempts`/`iqc_reports`를 nullable FK로 억지 확장해 panel 검사 수용 | 신규 table 최소 | 구매품목 receipt FK·상태기계와 결합, check constraint·불변 trigger 복잡화, 회귀 위험 큼 |
| C | stage별(LQC/OQC/CI/FAT) 독립 table 4벌 | stage별 자유도 | 중복 4배, 재검사·Pending·PDF 계약 사본 유지보수 부담 |

PUNCH 표현(권장): 실패 최종화 1건당 aggregated `Punch` Pending 1건을 생성하고 PUNCH 상세 항목은 성적서 응답·사유에 기록한다. PUNCH 항목별 독립 Pending·개별 종결 관리는 실제 고객 양식 회신 후 후속으로 분리한다 — 근거: Pending 차단 불변조건을 단순하게 보존하면서 미확정 고객 양식을 선확정하지 않는다.

권장안 채택 기록: interview 7장 결정 1·2·3·4·5·6과 위 후보 A는 사용자 standing experiment rule("비차단 선택은 Fable 권장안 자동 채택")에 따라 채택된 상태로 Codex review에 전달한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL·synthetic data만 사용한다.
- migration 필요 여부: 필요. 현재 latest `0034` 다음의 additive migration 1건(신규 attempt/report/template seed/operation table과 guard trigger). 기존 migration 수정·번호 재사용 없음, 기존 panel에 가짜 attempt/report backfill 없음.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 비활성 유지, 실제 고객·사용자 데이터 미사용.
- runtime 교체 여부: 없음. experiment worktree 내 실행만.
- 추가 사용자 승인 필요 작업: push·PR·merge(승인 0/3), Persistent UAT migration·runtime handover, 실제 provider — 모두 이 Task 범위 밖.
- fast-track 우회 금지 조건: Repository 계약 충돌, 18단계 순서·FAT optional·전진-only·finalized 불변·Pending 감사 위반, secret/개인정보 노출이 발견되면 blocking decision으로 반환하고 진행하지 않는다.

## 14. 검증 계획

- 최소 테스트: migration fresh/existing DB, stage별 합격·부적합·재검사·FAT skip/필요 분기, active attempt 단일성·idempotent replay·stale version, finalized 불변 trigger, 권한·scope(mutation/read/download), generic 업무 우회 차단.
- 영향 영역 회귀: Backend 전체 test(기존 376+ 유지), Pending·제조·IQC·workflow 회귀, Frontend lint·typecheck·unit·production build.
- PR/CI: 게시 미승인이므로 local 검증만. disposable Full-Stack E2E 1본 — LQC 합격 → 제조 완료 확인 → OQC 부적합 → Pending 재검사 → 합격 → 전진검수 합격 → FAT skip → 포장 skeleton(FAT 필요 변형 포함 여부는 구현에서 판단).
- 사용자 검수: 페이지별 desktop·390px screenshot과 checklist를 보고하되 `사용자 검수 대기`로 유지하고 완료로 표시하지 않는다. Teams narrow 포함 page-level overflow 0 확인.

## 15. 완료 기준

- 기능/권한/데이터: 5장 필수 항목 전부, 7장 불변조건의 서버 강제, Open P0/P1 0.
- UX: 6장 화면·피드백·overflow 기준 충족.
- 자동 테스트: 위 검증 계획 전부 PASS, 미실행 항목은 이유와 함께 기록.
- 5종 산출물: implementation report·SOP·user manual·Roadmap 실험 상태 기록·user validation checklist 추적 가능.
- 사용자 검수 상태: `사용자 검수 대기`.
- PR 상태: 없음(local experiment commit까지만, main merge 승인 0/3).

중단 조건: 13장 fast-track 우회 금지 조건 발생, `0034` 이후 schema 충돌, 기존 불변 trigger·purge 계약과의 비호환 발견.

## 16. 미결정 사항

비차단 deferred 항목으로, 구현을 막지 않되 후속 사용자 결정으로 전달한다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 실제 LQC/OQC/전진검수/FAT checklist 상세 문항과 필수 사진 위치 | 현업 회신 양식 → 신규 template version / v1 일반 항목 유지 | 대기(현업 회신) |
| 2 | 실제 고객 검사성적서 PDF 양식·전자서명 | 회신 양식 반영 후속 Task / 내부 snapshot PDF 유지 | 대기 |
| 3 | template 관리·version activation 정책 | TASK-ADMIN-002 범위·시점 | 대기 |
| 4 | 운영 사진 storage·retention·백업 정책 | DB bytea 유지 / object storage 이관 | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `Quality`(신규) 또는 `Materials`/`Manufacturing` 인접 module — inspection contracts/store/endpoints/PDF renderer 재사용 wiring, Workflow generic bypass 확장, Manufacturing 완료 확인 mutation, Pending helper 재사용, purge 경로, DI.
- Frontend: `App.tsx` navigation·view·deep link, 신규 품질 검사 페이지 컴포넌트·API helper·type, `ManufacturingPage.tsx` 완료 확인 카드, adaptive CSS.
- DB/Migration: `database/migrations/0035_*.sql` (additive).
- Tests/Scripts: backend targeted+전체, frontend unit, disposable Full-Stack E2E spec.
- Docs: Roadmap TASK-012A 실험 상태 기록, Task 산출물(`tasks/012a-*`), 2차 기획 target(`docs/19-*` 계열, runner 승인 경로).

## 18. Roadmap 연결

- 선행 Task: TASK-009A(IQC 성적서 계약)·TASK-011A(제조 실행·LQC skeleton) — 이 experiment 계보에 구현 완료, 사용자 검수 대기.
- 후속 Task: TASK-013A(물류 포장 상세), TASK-ADMIN-002(template 관리), PUNCH 항목별 관리 고도화(후속 후보).
- 현재 Go/No-Go: experiment fast-track 범위에서 Go. canonical 실행 큐의 `Dependency Pending`과 다음 `TASK-007A` Gate는 변경하지 않는다(사용자 standing 재정렬 승인은 interview에 기록됨).
- 별도 Task로 분리할 항목: 16장 미결정 4건과 물류·정산·Excel export.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | 사용자 standing experiment rule: interview 왕복·중간 승인 생략, 비차단 선택 Fable 권장안 자동 채택 | interview `COMPLETED_CONFIRMED` 기준선과 7장 결정 6건을 planning 입력으로 사용 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

experiment fast-track에서는 위 체크가 사용자 개별 승인 대신 Codex 내용 review → Fable 2차 기획 → blocking decision 0 확인으로 대체되며, main merge·게시 승인을 부여하지 않는다.

## 21. Codex 구현 지시문 초안

1. `0035` additive migration: panel quality inspection attempt/report/response/photo/PDF/operation table, stage system template v1 seed(모든 항목 사진 선택), finalized 불변·PDF immutable guard trigger, active attempt partial unique. 기존 migration 무수정.
2. Backend: stage 공통 store — queue/detail 조회(project scope), 시작·응답·사진·finalize·재검사·PDF retry mutation을 row lock+expected version+operation receipt로 구현. finalize transaction에 attempt 전이·panel coarse stage 전진·업무 완료·Pending 생성/종결·다음 업무 exactly-once·project stage event ensure를 포함하고 PDF rendering은 분리.
3. 제조 완료 확인 mutation(`manufacturing.update`)과 OQC handoff, generic my-work 우회 차단 확장, panel/project 취소·purge 정합.
4. Frontend: 전역 `품질` 진입(IQC tab 재사용 + stage tab 신규), mobile 단계 queue→panel focus→판정 sheet, desktop rail+detail, 제조 완료 확인 카드, 내 업무 deep link 교체, 도형·overflow·접근성 기준 준수.
5. 검증: 14장 계획 실행, 페이지별 desktop/390px screenshot, implementation report·5종 산출물·local experiment commit. 실패·미실행은 그대로 보고한다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 4
