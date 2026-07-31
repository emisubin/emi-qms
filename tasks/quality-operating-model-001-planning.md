# TASK-QUALITY-OPERATING-MODEL-001 — 구매품 구분 기반 IQC routing·외함 스캔형 IQC 기획안 (Fable 1차)

> 상태: Draft
> 작성 단계: Codex review 전 Fable 1차 기획
> 목적: 2026-07-30 사용자 확정 정책을 구현 가능한 계약으로 구체화하고, `experiment/*` 2-pass fast-track의 Codex review·Fable 2차 기획 입력을 제공한다.

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/quality-operating-model-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false (기획 문서 자체는 구현 승인을 부여하지 않는다. 사용자의 fast-track 구현 승인은 `tasks/quality-operating-model-001-change-002.md`에 별도로 기록되어 있다.)
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 따르며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 현재 시스템은 모든 구매품 도착분에 상세 IQC를 자동 생성하지만, 실제 품질팀은 외함만 공식 수입검사를 수행하고 그 검사서도 협력사가 작성한 종이 원본에 품질팀이 확인·서명하는 방식이다. 나머지 구매품은 IQC 없이 입고된다.
- 대상 사용자·역할: 구매 담당(구분 입력), 자재 담당(도착 등록·입고 확정), 품질 IQC 담당(스캔 등록·판정), 양식 관리 권한 보유자(구분 catalog 관리).
- 정상 흐름: 신규 정책 프로젝트에서 구매품에 구분을 필수 선택 → 도착 등록 시 구분의 IQC 필요 여부로 분기 → 외함은 서명 스캔본 첨부 기반 적합/부적합 판정 → 적합 후 입고 확정. IQC 없는 구분은 도착 즉시 `검사 대상 아님`으로 입고 확정 업무에 연결.
- 예외·복구 흐름: 외함 부적합 확정 시 기존 Pending 조치 흐름으로 연결하고, 재검사는 기존 판정을 고치지 않는 새 회차로 생성하며 회차마다 새 서명 스캔본이 필수다.
- 확정한 정책과 명시적 제외: 전환 기준은 프로젝트 단위(기능 업데이트 이후 등록 프로젝트만 구분 기반 routing), 기존 프로젝트·데이터 소급 변경 금지, 도급·사급 무관 routing, 확정 후 불변·회차 보존. LQC/OQC/전진검수/FAT 운영 모델 변경, 협력사용 Excel 양식 생성·배포, 판금류·부스바·명판 신규 검사 정책, 기존 데이터 자동 분류는 제외.
- planning으로 넘긴 비차단 미결정 사항: 16장 6건. 모두 권장안을 포함하며 fast-track standing instruction에 따라 2차 기획에서 권장안 자동 채택 대상이다.

## 1. 한 줄 목표

기능 업데이트 이후 등록되는 프로젝트에서 구매품 구분이 IQC 여부를 결정하고, 외함은 협력사 종이 검사서의 서명 스캔본으로 IQC를 판정하며, 비검사품은 IQC 없이 `검사 대상 아님` 상태로 입고 확정까지 진행할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 `RegisterArrival`은 도착 등록과 같은 transaction에서 무조건 상세(`Detailed`) IQC attempt·품질 업무를 생성한다(`backend/src/Emi.Qms.Api/Materials/MaterialsStore.cs`). 실제로 검사하지 않는 품목에도 IQC 업무가 쌓여 품질팀 업무 목록이 실제 업무와 어긋난다.
- 외함은 시스템의 상세 체크리스트가 아니라 협력사 종이 검사서에 품질팀이 확인·서명하는 방식이므로, 현재 `MATERIAL_IQC` 상세 성적서 입력은 실제 증빙(서명 스캔본)과 이중 작업이 된다.
- 우회 방식: 품질팀이 시스템 IQC를 형식적으로 통과 처리하거나 종이 검사서를 시스템 밖에 별도 보관한다. 검사 증빙과 시스템 기록이 분리된다.
- 방치 시 영향: IQC 미실시 품목이 시스템상 "IQC 합격"으로 보여 품질 기록의 신뢰가 무너지고, 외함 서명 원본이 시스템 밖에만 남아 감사 추적이 끊긴다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 구매 담당 | 신규 정책 프로젝트 구매품에 구분 필수 선택(직접 입력·Excel) | 담당 프로젝트 구매 | 구매품 구분 값 |
| 자재 담당 | 도착 등록, `검사 대상 아님` 도착분 입고 확정 | 담당 프로젝트 자재 | 도착분·입고 확정 |
| 품질 IQC 담당 | 스캔형 IQC 초안 작성(판정+첨부), 확정, 재검사 회차 수행 | 담당 프로젝트 IQC 큐 | 확정 전 초안만 |
| 양식 관리 권한 보유자 | 구매품 구분 추가·이름 변경·사용 중지·`IQC 필요` 설정 | 구분 catalog | catalog 항목 |
| 모든 관련 역할 | 확정된 판정·첨부·회차 이력 조회 | project scope 내 | 없음(확정 후 불변) |

Backend가 routing·필수 첨부·불변성·권한의 authoritative layer다. Frontend 표시만으로 검사 생략을 판단하지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 외함 스캔형 IQC 정상 흐름

1. 구매 담당이 신규 정책 프로젝트에 구매품을 등록하며 구분 `외함`을 선택한다.
2. 자재 담당이 도착 등록하면 시스템이 프로젝트 routing 정책과 구분의 `IQC 필요`를 확인해 스캔형 IQC attempt와 품질 업무를 생성한다.
3. 품질 담당이 협력사 종이 검사서를 확인·서명한 뒤 스캔본(PDF/JPEG/PNG 다중)을 업로드하고 `적합`을 선택해 확정한다.
4. 확정과 동시에 판정·첨부가 불변으로 잠기고, 자재 담당에게 기존 입고 확정 업무가 생성된다.

### 시나리오 B — 외함 부적합·재검사

1. 품질 담당이 서명 스캔본을 첨부하고 `부적합`으로 확정한다.
2. 시스템이 기존 Pending 흐름으로 부적합 이슈를 연결하고 도착분을 차단 상태로 둔다.
3. 조치 완료 후 재검사를 요청하면 기존 판정을 유지한 채 새 회차 attempt가 생성된다.
4. 재검사 회차에도 새 서명 스캔본 첨부가 필수이며, 최초 회차와 모든 재검사 회차가 함께 조회된다.

### 시나리오 C — IQC 없는 구매품

1. 구매 담당이 구분 `판금류`(IQC 없음)를 선택해 구매품을 등록한다.
2. 자재 담당이 도착 등록하면 IQC attempt를 만들지 않고 도착분을 `검사 대상 아님`으로 표시하며 입고 확정 업무를 생성한다.
3. 자재 담당이 `입고 확정`을 누르면 도착분이 확정되고 기존 이력·알림·후속(키팅/제조 투입) 계약이 그대로 이어진다.

### 시나리오 D — 기존 프로젝트 비회귀

1. 기능 업데이트 전에 등록된 프로젝트에서 구매·도착을 진행한다.
2. 시스템은 구분 입력을 요구하지 않고, 모든 도착분에 기존 상세 IQC를 그대로 생성한다.

## 5. 기능 요구사항

### 필수

- [ ] 프로젝트 생성 시 IQC routing 정책을 snapshot으로 고정한다: 기존 프로젝트 `모든 구매품 IQC`, 기능 이후 신규 프로젝트 `구분 기반 IQC`. 이후 변경·소급하지 않는다.
- [ ] 구매품 구분 catalog: 양식 관리에서 추가·이름 변경·사용 중지. 사용된 구분은 삭제 대신 사용 중지. 구분별 `IQC 필요/없음` 1:1 설정.
- [ ] 신규 정책 프로젝트의 구매 입력(직접 입력·Excel preview/apply)에서 구분 필수. 누락 시 이해 가능한 한글 오류로 차단.
- [ ] 도착 등록 routing: 신규 정책 프로젝트에서 `IQC 필요` 구분은 스캔형 IQC attempt 생성, `IQC 없음` 구분은 attempt 없이 `검사 대상 아님` 상태와 입고 확정 업무 생성. 도급·사급 무관.
- [ ] 스캔형 IQC: 적합/부적합 판정 + PDF/JPEG/PNG 다중 첨부(적합·부적합 모두 최소 1개 필수), 확정 전 초안 수정 가능, 확정 후 DB trigger·API 이중 불변, 부적합 시 기존 Pending 연결, 재검사는 새 회차 + 새 스캔본 필수, 전 회차 보존.
- [ ] IQC 미실시를 IQC 합격으로 표시하지 않는다. 목록·상세·집계·완료 판정에서 `검사 대상 아님`을 별도 구분으로 표시하고, 프로젝트 입고 마감·완료 판정과 생산관리 자동 실적 집계가 IQC 없는 도착분을 "IQC 미완료"로 오판하지 않게 한다.
- [ ] IQC 없는 도착분의 도착 등록은 IQC 담당자 부재로 차단하지 않는다(현재 도착 등록의 IQC 담당자 필수 guard는 IQC 필요 경로에만 적용).

### 선택

- [ ] 구분 catalog 목록의 사용 중지 항목 접기 표시.
- [ ] 스캔 첨부 미리보기(이미지 inline, PDF는 다운로드) — 기존 첨부 조회 패턴 재사용 범위에서.

### 명시적 제외

- [ ] LQC·OQC·전진검수·FAT 운영 모델 변경, 협력사용 IQC Excel 양식 생성·배포, 판금류·부스바·명판 신규 검사 정책, 기존 프로젝트·구매품 자동 분류, 기존 상세 IQC(`Detailed`)·기존 데이터 변경, 대표 repo·`main`·push·PR·merge, Persistent UAT migration·runtime handover, 실제 외부 provider 발송.

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 양식 관리 › 구매품 구분 | 기존 양식 관리 화면(`frontend/src/FormTemplateManagementPage.tsx`)의 신규 section | 구분 목록, `IQC 필요/없음`, 사용 중, 정렬 | 추가, 이름 변경, 사용 중지, IQC 필요 설정 | 저장 action 바로 아래 결과·오류(기존 Action Feedback 계약) |
| 구매 입력 | 기존 구매 탭 | 신규 정책 프로젝트에서 구분 선택 필드(필수 표시) | 구분 선택, Excel preview에서 구분 열 확인 | 누락 시 필드 단위 한글 오류 |
| 자재 도착·입고 | 기존 자재 workspace(`frontend/src/MaterialsWorkspace.tsx`) | 도착분 상태에 `검사 대상 아님` 뱃지, 입고 확정 버튼 | 도착 등록, 입고 확정 | 기존 도착·확정 피드백 재사용 |
| 품질 IQC 큐·상세 | 기존 IQC workspace(`frontend/src/IqcReportWorkspace.tsx`) 내 스캔형 분기 | 판정 선택, 첨부 목록(파일명·크기), 회차 이력 | 초안 저장, 첨부 추가/삭제(초안만), 확정 | 확정 성공·불변 안내, 첨부 형식·크기 오류 |
| Pending | 기존 Pending 화면 | 외함 부적합 이슈(기존 계약) | 기존 조치·재검사 요청 | 기존 피드백 |

확인할 UX 항목:

- `검사 대상 아님`과 `IQC 합격`이 색·문구로 명확히 구분되는가.
- 스캔형 상세에서 "협력사 종이 검사서에 서명 후 스캔본을 올린다"는 다음 행동이 안내되는가.
- 확정 후 화면이 조회 전용임을 명시하는가(기존 확정 검사 화면 패턴).
- 390px에서 첨부 업로드·판정·확정이 가능한가(기존 mobile sheet·adaptive layout 재사용).

## 7. 업무 규칙과 불변조건

- 프로젝트 routing 정책은 생성 시 1회 고정하며 이후 어떤 경로로도 바꾸지 않는다(기존 `LinkedV1`/Legacy snapshot 원칙과 동일).
- 기존 프로젝트의 구매·도착·IQC 계약은 byte 하나도 달라지지 않는다: 구분 미입력 허용, 상세 IQC 자동 생성 유지.
- 확정된 스캔형 판정·첨부·회차는 수정·삭제 불가(기존 `iqc_reports` finalize trigger + API guard 패턴 재사용). 재검사는 항상 새 attempt.
- 적합·부적합 모두 확정 시점에 서명 스캔본 첨부 ≥ 1을 서버가 강제한다.
- 사용된 구분은 실제 삭제하지 않고 사용 중지한다. 사용 중지 구분은 신규 구매 입력에서 선택 불가, 기존 기록 표시는 유지.
- 도착분의 routing 결과(IQC 대상/비대상)는 도착 등록 시점에 확정·기록하며, 이후 구분의 `IQC 필요` 설정 변경은 소급되지 않는다.
- `검사 대상 아님`은 완료 판정에서 "IQC가 끝난 것"이 아니라 "IQC가 없는 것"으로 집계하고, 입고 확정 완료가 해당 도착분의 종료 조건이다.
- Pending 재검사 이력, 패널·구매품목 단위, 담당자 권한·project scope, 알림·내 업무·workflow 계약은 기존 그대로 보존한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 구매품 구분 catalog | code·표시명·정렬·사용 중·`IQC 필요`·row_version | 신규 (패턴: `database/migrations/0045_pending_issue_type_catalog.sql`) | 삭제 금지, 사용 중지 lifecycle, 이름 유일성 |
| 프로젝트 IQC routing 정책 | `AllReceipts` / `CategoryBased`, 생성 시 snapshot | 신규 열 (기존 project snapshot 원칙) | 불변, 기존 프로젝트는 backfill `AllReceipts` |
| 구매품목 구분 참조 | nullable 참조, 신규 정책 프로젝트에서 필수 | 기존 구매품목에 신규 열 | 기존 데이터 null 유지, 소급 금지 |
| 도착분 상태 | 기존 `Arrived→IqcRequested→Passed/FailedBlocked→Confirmed`에 `검사 대상 아님`(입고 확정 대기) 추가 | 기존 확장 | 기존 이벤트 이력 계약 유지 |
| IQC attempt decision mode | 기존 `Legacy`/`Detailed`에 `ScanBased` 추가 (`material_iqc_attempts.decision_mode`) | 기존 확장 | 회차 번호·Pending 연결·재검사 lineage 재사용 |
| 스캔형 판정·첨부 | attempt별 초안 판정 + 다중 첨부(bytea, sha256, byte 크기), 확정 snapshot | 신규 (패턴: `iqc_report_photos`·`0066_pending_action_photos` bounded DB 저장) | 확정 후 불변 trigger, append-only |

```text
[IQC 필요]   Arrived → IqcRequested(ScanBased 초안) → Passed → Confirmed
                                   └→ FailedBlocked → Pending 조치 → IqcRequested(새 회차) → …
[IQC 없음]   Arrived → 검사 대상 아님(입고 확정 대기) → Confirmed
[기존 프로젝트] 현재 Detailed 흐름 그대로
```

## 9. API·Backend 고려사항

- Backend authoritative 규칙: routing 분기, 구분 필수 validation, 첨부 형식(magic byte: `%PDF`/JPEG/PNG — 기존 `IqcReportStore` 사진 sniffing과 `LogisticsStore` PDF 판별 재사용)·크기·개수 한도, 확정 불변성, 재검사 회차 생성.
- 필요한 조회·mutation(내부 명칭은 구현 조사에서 확정):
  - 구분 catalog 목록/추가/이름 변경/사용 중지/`IQC 필요` 설정 — 기존 양식 관리 endpoint 군에 추가.
  - 구매 저장·Excel apply의 구분 validation.
  - 도착 등록 routing(기존 `RegisterArrivalAsync` transaction 내 분기), `검사 대상 아님` 도착분 입고 확정.
  - 스캔형 attempt 상세 조회, 초안 판정 저장, 첨부 추가/삭제(초안만), 확정, 첨부 다운로드.
- 권한: 구분 관리는 양식 관리 권한, 스캔형 판정은 기존 품질 IQC 담당 권한, 입고 확정은 기존 자재 권한과 project scope를 그대로 사용. 신규 역할 없음.
- transaction·동시성: 도착 등록·확정·재검사 요청은 기존 단일 transaction + `version`/`row_version` CAS 패턴 유지. 확정은 첨부 존재를 같은 transaction에서 재검증.
- audit trail: 기존 material receipt 이벤트(`InsertEventAsync`)에 routing·확정·재검사 이벤트를 추가 기록. 구분 catalog 변경은 기존 기준정보 변경 이력 패턴을 따른다.
- 외부 provider 영향: 없음(알림은 기존 인앱·work item 경로 재사용, 실제 발송 없음).

## 10. Frontend 고려사항

- route/component: 신규 route 없음. `FormTemplateManagementPage`(구분 section), 구매 입력 폼, `MaterialsWorkspace`, `IqcReportWorkspace`(decision mode 분기)에 추가. contracts는 `frontend/src/api.ts`·`frontend/src/iqc-report.ts` 확장.
- loading/empty/error/success: 기존 workspace 패턴과 공통 Action Feedback(A1/A2) 계약 재사용. 저장·확정 결과는 해당 action 바로 아래 표시.
- 접근성·390px: 기존 adaptive layout·MobileSheet 재사용, 첨부 목록은 좁은 화면에서 세로 나열, overflow 0 유지.
- 기존 상세 IQC 화면은 변경하지 않고 `decisionMode`로 분기만 추가한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 스캔형 attempt는 기존 `materials:iqc:*` work item·알림 생성(`CreateIqcWorkItemAsync`)을, IQC 없는 도착분은 기존 입고 확정 담당 해석(`ResolveMaterialConfirmationAssigneesAsync`)을 재사용.
- Pending: 부적합→Pending→조치→재검사(`EnsurePendingReinspectionAsync`) 기존 lineage 그대로. TASK-ATTACHMENT-001의 조치 사진·재검사 근거 통합 조회와 회차 연결 유지.
- 생산관리 자동 실적: `LinkedV1` 프로젝트의 IQC 기반 실적 projection이 `검사 대상 아님` 도착분을 미완료로 집계하지 않는지 구현 조사에서 대조(입고 확정 완료를 근거로 사용).
- Excel/PDF/첨부: 구매 Excel preview/apply에 구분 열, 자재·IQC 선택 export 열에 구분·`검사 대상 아님` 표시 추가 검토. 스캔 첨부는 DB bounded 저장.
- 삭제·복구/감사: 기존 admin 삭제 lifecycle·감사 계약 변경 없음.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 기존 IQC attempt 축을 확장: `decision_mode='ScanBased'` 추가, 도착분 상태 1개 추가, 구분 catalog 신설 | Pending·재검사 lineage·work item·알림·불변 trigger를 그대로 재사용, 회차·이력 화면 일관, 변경 표면 최소 | `MaterialsStore` 분기 복잡도 증가 — 집중 테스트 필요 |
| B | 스캔형 IQC를 별도 모듈·별도 테이블 축으로 신설 | 기존 IQC와 격리 | 재검사·Pending·업무·집계 로직 중복 구현, 회차 이력 이원화, 비용·회귀 위험 큼 |
| C | 품목명 문자열로 IQC 대상 자동 판별 | 입력 부담 없음 | 자유 입력 오분류 위험 — 두 독립 의견서 모두 반대, 사용자도 구분 선택안을 확정 |

권장안 A. 사용자 확정 정책(구분 선택·프로젝트 snapshot)과 기존 불변조건 재사용 근거가 일치한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. experiment worktree·전용 DB만 사용.
- migration 필요 여부: 신규 additive migration 1건(`0067`, 현재 최신 `0066` 다음). fresh DB와 기존 DB 모두 검증. `main` 반영 migration 수정·번호 재사용 금지.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 호출 금지 유지.
- runtime 교체 여부: 없음(기존 development runtime 규칙 내 확인).
- 추가 사용자 승인 필요 작업: local commit(`localCommitApproved: false` 상태), 대표 repo·`main`·push·PR·merge, Persistent UAT 적용은 각각 별도 승인 전 수행하지 않는다.

## 14. 검증 계획

- 최소 테스트(Backend): 신규 정책 프로젝트 routing 분기(외함→ScanBased, 비검사→attempt 없음), 구분 필수 validation, 확정 시 첨부 필수·형식·크기·개수, 확정 후 불변(UPDATE/DELETE 거부), 부적합→Pending→재검사 새 회차+새 스캔 필수, 기존 프로젝트 Detailed 비회귀, migration fresh+기존 DB(`PostgreSqlMigrationTests` 확장).
- 최소 테스트(Frontend): 구분 catalog 관리 UI, 구매 구분 필수 오류, 스캔형 판정·첨부·확정 화면, `검사 대상 아님` 표시.
- 영향 영역 회귀: Backend 전체(현 기준선 `430/430`)·Frontend 전체(`142/142`) 무손실 + 신규 테스트. 자재 집계·프로젝트 완료 판정·생산관리 자동 실적 관련 기존 테스트 통과.
- 격리 Full-Stack: 시나리오 A~D를 전용 DB E2E 1건 이상으로 검증.
- 사용자 검수: 양식 관리·구매·자재·IQC 화면의 desktop 1440·mobile 390 screenshot과 checklist. 사용자 검수 완료 전 완료로 표기하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 5장 필수 요구사항 전부 서버 강제 + 기존 프로젝트 비회귀.
- UX: desktop·390px 핵심 동선, overflow 0, console error 0.
- 자동 테스트: Backend·Frontend 전체 green, 신규 migration fresh 적용.
- 5종 산출물: Implementation report·SOP·User manual·Roadmap/원장 update·User validation checklist를 종료 정책에 따라 추적.
- 사용자 검수 상태: `BATCHED_FINAL` 규칙에 따라 별도 상태로 유지.
- PR 상태: 해당 없음(fast-track은 local 범위, 게시 승인 별도).

중단 조건: 확정 불변 trigger·기존 IQC 계약과 해소 불가능한 충돌 발견, Repository source 간 의미 있는 충돌, fast-track 제외 경계(대표 repo·`main`·Persistent UAT·실제 provider)를 넘어야만 진행 가능한 경우.

## 16. 미결정 사항 (비차단 — 권장안 자동 채택 대상)

| 번호 | 질문 | 선택지 | 권장안 | 사용자 결정 |
| ---: | --- | --- | --- | --- |
| 1 | 구분 catalog 관리 권한 | ① 기존 양식 관리 권한(CanManage) ② system administrator 전용 | ① — 인터뷰가 관리 위치를 양식 관리로 확정했고 기존 권한 모델 재사용 | Deferred |
| 2 | 초기 seed 구분 | ① 외함(IQC 필요)+판금류·부스바·명판·기타(IQC 없음) ② 외함·기타만 | ① — 현장 조사의 실제 품목 반영, 초기 운영값 정책과 일치 | Deferred |
| 3 | `IQC 필요` 변경 적용 시점 | ① 이후 도착 등록분부터(도착 시 snapshot) ② 미확정 구매품에도 소급 | ① — 기존 snapshot 불변 원칙과 일관, 소급 오분류 방지 | Deferred |
| 4 | IQC 없는 도착분 표현 | ① 별도 도착분 상태 신설 ② `Arrived` 유지+표시 flag | ① — 현재 `Arrived`는 transient라 의미 중복, 집계·이벤트가 명확 | Deferred |
| 5 | 스캔 첨부 한도 | ① 파일당 10MB·회차당 10개 ② 사진과 동일 5MB | ① — 스캔 PDF는 사진보다 크고, bounded DB 저장 원칙 유지 | Deferred |
| 6 | 구매 Excel의 구분 처리 | ① 신규 정책 프로젝트 Excel에 구분 열 추가·누락 시 preview 오류 ② Excel은 구분 없이 저장 후 개별 입력 | ① — "구분 필수" 정책을 입력 경로 전체에 동일 적용 | Deferred |

## 17. 예상 변경 범위 (확정 allowlist가 아닌 조사 대상)

- Backend: `Materials/MaterialsStore.cs`·`MaterialsContracts.cs`, 스캔형 IQC store/endpoint(신규 파일 또는 `IqcReportStore` 인접), `Procurement/*`(구분 입력·Excel), `Admin/FormTemplate*`(구분 catalog), Workflow 완료 판정 대조.
- Frontend: `FormTemplateManagementPage.tsx`, 구매 입력 화면, `MaterialsWorkspace.tsx`, `IqcReportWorkspace.tsx`, `api.ts`·`iqc-report.ts`, 관련 styles.
- DB/Migration: `database/migrations/0067_*.sql` 1건(additive: catalog, 프로젝트 정책 열, 구매품 구분 열, attempt mode 확장, 도착분 상태 확장, 스캔 첨부 테이블·불변 trigger).
- Tests/Scripts: Backend 통합 테스트, Frontend unit, 격리 Full-Stack E2E 시나리오 추가.
- Docs: 2차 기획(`docs/48-enclosure-iqc-routing-plan.md`, runner 별도 승인 target), SOP·User manual, Roadmap·실험 원장 갱신.

## 18. Roadmap 연결

- 선행 Task: TASK-008A(도착·분할 입고), TASK-009A(상세 IQC), TASK-007A(Pending), TASK-ADMIN-002(양식 관리), TASK-ATTACHMENT-001(조치 증빙) — 모두 experiment 완료 상태이며 재구현하지 않는다.
- 후속 Task: LQC 기준·성적서, 프로젝트별 OQC 적용·N/A 정책, 고객용 성적서 묶음, 도면 revision 연결(Roadmap 추적 항목 2·4·11~14)은 본 Task에 포함하지 않는다.
- 현재 Go/No-Go: 인터뷰 확정 정책과 change-002 승인 기준 Go. 원장 4.1의 "정책 결정 전 구현 금지" 문구는 2026-07-30 사용자 확정으로 해소되었으며, 구현 시 원장·Roadmap 상태 동기화가 필요하다.
- 별도 Task로 분리할 항목: 협력사용 Excel 검사 양식 배포, 판금류·부스바·명판 기록 정책, 운영 storage·scanner 범위.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-30 | 현장 조사 기반 확정 정책과 기획·구현 시작 명시 | 인터뷰 `2026-07-30 구현 확정 정책` 전체를 5·7·8장 계약으로 반영 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

(fast-track에서는 위 체크 대신 change-002 승인과 2차 기획의 `openBlockingDecisionCount: 0`이 진행 조건이다.)

## 21. Codex 구현 지시문 초안

1. Change-002 구현 계약 10개 항목과 이 기획 5·7·8장을 구현 범위로 사용하고, 16장 6건은 2차 기획의 확정값을 따른다.
2. migration `0067` 1건으로 additive schema를 추가하고 fresh·기존 DB에서 검증한다. 기존 migration을 수정하지 않는다.
3. `RegisterArrivalAsync` transaction 안에서 routing을 분기하되, 기존 프로젝트(`AllReceipts`) 경로의 동작·이벤트·테스트를 변경하지 않는다.
4. 스캔형 확정은 기존 `iqc_reports` finalize 패턴(단일 transaction, trigger+API 이중 불변, sha256·byte 크기 기록)을 재사용하고, 첨부 magic byte 검증은 기존 JPEG/PNG sniffing과 `%PDF` 판별을 따른다.
5. 부적합·재검사는 `EnsurePendingReinspectionAsync`·attempt 회차 축을 재사용하고 새 lineage를 만들지 않는다.
6. 검증은 14장 계획을 따르며, 실행하지 않은 검증을 성공으로 기록하지 않는다. 실제 provider·Persistent UAT·게시 경계를 넘지 않는다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 6
