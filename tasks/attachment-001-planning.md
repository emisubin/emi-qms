All baseline reads are complete — interview confirmed, ledger priority 0 matches, and the existing IQC/panel-quality photo pattern (DB bytea, MIME sniff, SHA-256, 5MB, immutability triggers, multipart endpoints) plus the Pending transition/re-inspection code are verified. Below is the single primary planning draft.

# TASK-ATTACHMENT-001 — Pending 조치 사진 첨부와 재검사 근거 통합 조회 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/attachment-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 모든 종류의 Pending 조치 완료 시 조치 근거 사진을 선택적으로 첨부할 수 없고, 품질 재검사 화면에서 최초 부적합 근거·사진과 조치 내용·사진을 한 자리에서 대조할 수 없다.
- 대상 사용자·역할: 조치 담당자(조치부서), 품질 재검사 담당자, Pending 조회·코멘트 가능한 전 부서.
- 정상 흐름: 조치 담당자가 조치 중 사진을 선택적으로 추가·삭제 → 조치 완료와 동시에 사진 snapshot 원자 확정 → 재검사 담당자가 원 부적합 근거·사진, 조치 내용·사진, 판정 UI를 위에서 아래 한 흐름으로 확인하고 판정.
- 예외·복구 흐름: 재검사 불합격 시 Pending이 재조치 상태로 돌아가 새 조치 회차가 시작되며, 이전 회차의 확정 사진은 append-only 근거로 보존된다. 사진 없는 조치 완료도 계속 가능하다.
- 확정한 정책과 명시적 제외: ① 조치 사진은 선택 입력 ② JPEG·PNG만, 장당 최대 5MB ③ 재검사 화면은 최초 부적합 근거·사진 → 조치 내용·사진 → 판정 UI의 세로 한 흐름 ④ 원본 사진 덮어쓰기·교체 금지, 이력 근거 보존 ⑤ 조치 사진 확정 권한은 조치 담당자에게만, 다른 부서는 조회·코멘트만 ⑥ 대표 Repository·`main`·Persistent UAT·실제 provider·push·PR·merge 제외.
- planning으로 넘긴 비차단 미결정 사항: 저장 방식 공통화 여부, 회차·건당 상한, 임시 업로드와 원자 확정, 삭제 허용 시점, 읽기 권한 scope, 백업·보존·삭제 lifecycle, 기존 데이터 backfill — 7건 모두 이 문서 12장·16장에서 권장안을 제시하며, experiment standing instruction에 따라 Codex review 후 2차 기획에서 자동 채택 대상이다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

조치 담당자는 Pending 조치 완료 시 근거 사진을 선택적으로 남기고, 품질 재검사 담당자는 최초 부적합 근거·사진과 조치 내용·사진을 같은 화면에서 대조한 뒤 판정할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 조치 담당자는 Pending 조치 내용을 text 코멘트와 조치 완료 사유로만 남긴다. Pending 화면에는 "파일 첨부 준비 중 — 보안 저장·검역 정책 확정 전에는 파일을 받지 않습니다"라는 정책 보류 안내(`frontend/src/PendingPage.tsx` 등록 폼)가 남아 있다.
- 품질 재검사 화면은 부적합 항목의 이전 근거를 text로만 보여준다. 패널 품질 재검사는 항목별 `previousFailureEvidence` 문자열, IQC 재검사는 `FailureReason`·`ActionReason` 문자열만 표시하며(각각 `QualityInspectionsPage.tsx`, `IqcReportWorkspace.tsx`), 최초 부적합 사진과 조치 근거 사진은 어디에도 없다.
- 현재 우회 방식은 사진을 시스템 밖(메신저·개인 저장소)에 두고 재검사자가 별도로 찾아보는 것이어서 근거 유실·대조 누락·판정 지연이 발생한다.
- 기존 IQC 성적서 사진(`iqc_report_photos`)과 패널 품질검사 사진(`panel_quality_report_photos`)은 검사 report에 종속된 증빙이라 Pending 조치 회차의 lifecycle(조치 중 임시 → 조치 완료 확정 → 재조치 회차 반복)을 표현하지 못한다.
- 이 기능이 없으면 조치 완료의 실물 근거가 시스템에 남지 않아 재검사 판정 품질과 감사 추적성이 떨어진다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 조치 담당자 (Pending assignee, `Pending.Manage`) | 조치 중 사진 추가·삭제, 조치 완료로 사진 snapshot 확정 | 담당 Pending 상세·사진 전체 | 본인이 담당한 Pending의 임시(Draft) 사진만 |
| 품질 재검사 담당자 (`Quality.*` + `Pending.Read`) | 재검사 화면에서 원 부적합 근거·사진과 조치 내용·사진 대조, 판정 | 접근 가능 프로젝트의 검사·Pending 근거 | 사진 변경 없음 (판정은 기존 검사 계약) |
| 그 외 전 부서 (`Pending.Read`, 코멘트는 `Pending.Manage`) | Pending 상세에서 확정·임시 사진 조회, 코멘트 작성 | 프로젝트 접근 scope 내 Pending | 사진 변경 없음 |
| system administrator | 기존 Pending 관리 권한과 동일 조회 | 전체 | 사진 확정 권한 없음 (조치 담당자 전용 유지) |

Backend가 authoritative layer다. 업로드·삭제·확정은 서버가 담당자 여부·상태·형식·용량을 최종 판정하고, Frontend의 버튼 숨김·비활성화는 보조 수단이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 조치 사진 첨부와 조치 완료

1. 조치 담당자가 배정된 Pending을 열어 상태를 `조치 중`으로 전환한다(기존 흐름).
2. 상세 화면의 조치 사진 영역에서 JPEG/PNG 사진을 장당 5MB 이하로 추가하고, 필요하면 확정 전에 삭제한다. 사진 설명(alt text)을 함께 입력한다.
3. `조치 완료` 버튼(기존 `InProgress → ReinspectionRequested` 전환)을 누르면 조치 사유와 함께 현재 임시 사진들이 같은 transaction에서 조치 회차 snapshot으로 원자 확정된다.
4. 시스템은 기존과 동일하게 재검사 업무·알림을 생성하고, 확정된 회차 사진은 이후 수정·삭제할 수 없다.

### 시나리오 B — 재검사 화면의 근거 통합 조회와 판정

1. 재검사 담당자가 재검사 요청 업무(패널 품질검사 또는 IQC 재검사)를 연다.
2. 화면 상단부터 ① 최초 부적합 근거(판정 사유·부적합 항목 근거 text)와 당시 확정 report의 사진, ② 최신 조치 회차의 조치 사유·코멘트와 확정 조치 사진, ③ 기존 재검사 판정 UI가 세로 한 흐름으로 표시된다.
3. 재검사자가 합격 판정하면 기존 계약대로 Pending이 원자 종결되고, 불합격이면 Pending이 재조치 상태로 돌아간다.

### 시나리오 C — 재조치 회차 반복

1. 재검사 불합격으로 Pending이 `조치 요청`으로 돌아온다(기존 `ReopenQualityIssueAfterFailedReinspectionAsync` 흐름).
2. 조치 담당자가 다시 `조치 중`으로 전환하면 새 조치 회차의 빈 임시 사진 영역이 시작된다. 이전 회차의 확정 사진은 회차 label과 함께 읽기 전용으로 계속 표시된다.
3. 두 번째 조치 완료 시 새 회차 snapshot이 확정되고, 재검사 화면에는 최신 회차가 우선 표시되며 이전 회차도 이력으로 조회할 수 있다.

### 시나리오 D — 사진 없는 조치 완료 (하위 호환)

1. 조치 담당자가 사진 없이 조치 사유만 입력하고 조치 완료한다.
2. 기존과 동일하게 전환·재검사 요청이 진행되고, 재검사 화면의 조치 사진 영역은 "등록된 조치 사진 없음"으로 표시된다.

### 시나리오 E — 차단 경로

- 담당자가 아닌 사용자의 업로드·삭제, `조치 중`이 아닌 상태의 업로드, JPEG/PNG가 아니거나 MIME 위장·5MB 초과 파일, 회차당 6장째 업로드, 확정된 사진의 삭제 시도는 모두 서버가 안정적인 한글 오류 메시지로 거부한다.

## 5. 기능 요구사항

### 필수

- [ ] Pending 조치 사진의 bounded 저장: DB `bytea` 저장, MIME sniffing으로 JPEG/PNG 검증, SHA-256, 장당 5MB 제한 — 기존 IQC·패널 품질 사진과 동일 패턴.
- [ ] 임시(Draft) → 확정(Confirmed) lifecycle: `조치 중` 상태에서 담당자만 추가·삭제, `조치 완료` 전환 transaction 안에서 회차 번호를 부여해 원자 확정.
- [ ] 확정 사진 append-only: DB trigger와 서버 검증으로 확정 후 update/delete 차단 (기존 `guard_finalized_iqc_report_children` 패턴).
- [ ] Pending 상세 API에 회차별 확정 사진·임시 사진 metadata 포함, 사진 content 조회 endpoint 제공 (프로젝트 접근 scope + `Pending.Read` 검증).
- [ ] 패널 품질 재검사 상세에 원 부적합 report 사진 참조와 최신 조치 회차의 사유·사진 포함, 화면에 ①부적합 근거·사진 ②조치 내용·사진 ③판정 UI 세로 흐름 표시.
- [ ] IQC 재검사(`IqcReinspectionSourceResponse`)에 원 부적합 사진 참조와 조치 사진 참조 추가, `ReinspectionComparison` 영역 확장.
- [ ] 모든 Pending 유형(Nonconformance·Punch·ManufacturingStop·Other)의 조치 완료에서 동일하게 동작. 품질 미연결 Pending은 기존 수동 종결 경로를 유지하며 사진은 상세에서 조회.
- [ ] 동일 요청 재시도·동시 요청이 중복 사진·중복 이력을 만들지 않음 (Pending row lock + version CAS + 내용 hash 기반 중복 판정).
- [ ] desktop과 390px 모바일에서 업로드·조회·판정 흐름 동작, 접근성(alt text 필수, keyboard, `aria-live` feedback).

### 선택

- [ ] Pending 상세 history 영역에서 회차별 사진 확정 시각·장수 요약 표시 (별도 history event 추가 없이 사진 데이터로 파생).
- [ ] 재검사 화면에서 이전 회차 조치 사진 접기/펼치기.

### 명시적 제외

- [ ] Pending 등록 시점의 사진·파일 첨부 (조치 완료 근거만 이번 범위).
- [ ] 코멘트 첨부, 사진 이외 파일 형식(PDF·문서), Excel/PDF 출력물에 조치 사진 포함.
- [ ] 기존 IQC·LQC·OQC 실패 report·사진의 수정 또는 schema 변경.
- [ ] 외부 object storage, CDN, 운영 storage 용량 설계와 restore rehearsal (운영 전환 Task).
- [ ] 알림 channel·내용 변경 (기존 재검사 요청 알림 유지).
- [ ] 대표 Repository, `main`, Persistent UAT, 실제 provider, push·PR·merge.

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| Pending 상세 (조치 담당자, `조치 중`) | Pending 목록/내 업무 | 임시 사진 목록(파일명·설명·용량), 회차별 확정 사진, 5장/15MB 잔여 안내 | 사진 선택(카메라 허용 `capture`), 설명 입력, 등록·삭제, 조치 완료 | action 바로 아래 성공·오류 메시지, 업로드 중 중복 submit 차단 |
| Pending 상세 (그 외 사용자) | Pending 목록/프로젝트 상세 | 회차별 확정 사진 read-only, 임시 사진은 "확정 전" badge와 함께 조회 | 사진 확대 조회, 코멘트 작성(기존) | 권한 없음·확정 전 상태를 명확히 구분 표시 |
| 패널 품질 재검사 (QualityInspectionsPage) | 재검사 요청 업무/Pending "품질 재검사 열기" | ① 최초 부적합 판정 사유·항목별 근거·당시 report 사진 ② 조치 회차 사유·확정 사진 ③ 기존 판정 UI | 위→아래 확인 후 합격/불합격 판정 | 기존 판정 feedback 유지, 근거 로딩 실패 시 판정은 가능하되 근거 미로딩 안내 |
| IQC 재검사 (IqcReportWorkspace) | 재검사 업무/Pending | 기존 `ReinspectionComparison`에 원 부적합 사진·조치 사진 추가 | 부적합 항목 재확인 후 판정 | 기존 feedback 유지 |

확인할 UX 항목:

- 사용자가 현재 상태를 이해할 수 있는가 — 임시/확정, 회차 번호, 잔여 장수·용량을 badge와 counter로 표시한다.
- 다음 행동이 명확한가 — 조치 중에는 "사진은 선택 사항이며 조치 완료 시 확정됩니다"를 안내한다.
- 저장·변경 결과가 action 근처에 보이는가 — 기존 공통 action feedback 패턴(`quality-photo` 계열 UI)을 재사용한다.
- 권한 부족·검수 전용·오류 상태가 명확한가 — ReviewSafe·비담당자에게는 업로드 control을 비활성화하고 이유를 표시하되 서버 차단이 최종 기준이다.
- 좁은 화면에서도 핵심 행동이 가능한가 — 390px에서 업로드·조회·판정의 page-level horizontal overflow 0을 유지한다.

## 7. 업무 규칙과 불변조건

- 조치 사진은 선택 입력이다. 사진이 없어도 조치 완료·재검사·종결의 기존 계약은 변하지 않는다.
- JPEG/PNG만 허용하고 장당 5MB 이하, 회차당 최대 5장·총 15MB, Pending 한 건 누적 최대 25장이다(12장 권장안).
- 임시 사진의 추가·삭제는 해당 Pending의 현재 담당자만, 상태가 `조치 중`일 때만 가능하다.
- 조치 완료 전환과 사진 확정은 하나의 DB transaction이다. 전환이 실패하면 사진은 임시 상태로 남는다.
- 확정된 사진은 어떤 경로로도 수정·교체·삭제되지 않는다(append-only, DB trigger 보강).
- 기존 IQC·LQC·OQC finalized report·사진·PDF와 그 immutability trigger는 변경하지 않는다. 원 부적합 사진은 복사하지 않고 참조로 노출한다.
- Pending 상태 전이(`Registered → ActionRequested → InProgress → ReinspectionRequested → Closed`)와 권한 매트릭스, 재검사 원자 종결·재개 계약은 그대로 유지한다.
- 사진 content 응답은 인증·프로젝트 접근 scope·`Pending.Read`를 통과한 요청에만 제공하며 내부 식별자·raw 오류를 노출하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| Pending 조치 사진 (`pending_action_photos` 후보) | Pending에 종속된 사진 binary + metadata (mime, byte_size, sha256, alt_text, display_name slot, 상태, 회차) | 신규 | 확정 후 append-only, trigger 보호 |
| 조치 회차 (action round) | 조치 완료 전환마다 1씩 증가하는 회차 번호. 확정 시 부여 | 신규(파생 개념, 별도 table 없음) | 회차·확정 시각·확정자 기록 |
| Pending issue/comment/history | 기존 상태 전이·사유·코멘트 | 기존 | 변경 없음 (history event enum 미확장) |
| IQC/패널 품질 report 사진 | 원 부적합 근거 사진 | 기존 | 읽기 전용 참조만 추가 |

```text
[조치 중] 사진 Draft (담당자 추가·삭제 가능)
    → 조치 완료 전환 transaction에서 Confirmed(round=N) 확정
    → 재검사 불합격 시 Pending 재개, 다음 회차 Draft는 빈 상태로 시작
    → Confirmed 사진은 Pending 종결 후에도 불변 보존
```

- 신규 table은 `pending_issues`를 참조하며 삭제 lifecycle은 기존 `pending_comments`/`pending_history`와 동일하게 Pending 삭제(프로젝트 purge)에 따라 함께 제거된다.
- Draft 상태 row는 회차 null, 확정 시 회차·확정자·확정 시각을 채운다. column·constraint 세부는 구현 조사에서 확정하되 `iqc_report_photos`의 검증 패턴(display_name slot, mime, size, sha256, alt_text)을 따른다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 담당자·상태·형식·용량·장수·확정 후 불변의 전부.
- 필요한 조회와 mutation:
  - `POST /api/pending/{pendingId}/photos` — multipart `IFormFile` + 설명 + expected version (기존 품질 사진 endpoint 형식 재사용, `MaterialsEndpointExtensions`/`QualityInspectionEndpointExtensions` 패턴).
  - `DELETE /api/pending/{pendingId}/photos/{photoId}` — Draft만 허용.
  - `GET /api/pending/{pendingId}/photos/{photoId}/content` — scope 검증 후 binary 반환 (`GetPhotoContentAsync` 패턴).
  - `PendingDetailResponse` 확장 — 회차별 확정 사진·임시 사진 metadata와 잔여 한도.
  - 품질 재검사 상세 확장 — 패널: 원 부적합 report 사진 참조 + 최신 조치 회차 사유·사진. IQC: `IqcReinspectionSourceResponse`에 동일 참조 추가.
- 권한·validation: 업로드·삭제는 현재 assignee 본인 + `Pending.Manage`. 조회는 기존 `GetDetailAsync`의 프로젝트 접근 scope와 동일. content-type 위장은 magic byte sniffing으로 차단 (`IqcReportStore`·`ProfileImageValidator`의 기존 방식과 동일 로직을 Pending store에 자체 구현).
- transaction·동시성·idempotency: Pending row `select ... for update` + `ExpectedVersion` CAS. 동일 내용(sha256+설명) 재업로드는 중복 생성 없이 안내. 조치 완료 전환은 기존 `TransitionAsync` transaction에 사진 확정을 포함해 원자화. 회차 부여는 잠금 하에 `max(round)+1`.
- audit trail: 사진 row 자체가 등록자·확정자·시각·hash를 가진 감사 기록이다. `pending_history`의 event enum은 확장하지 않는다.
- 외부 provider 영향: 없음. 알림 내용·channel 변경 없음.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다(상기 이름은 조사 기반 후보다).

## 10. Frontend 고려사항

- route/component: `PendingPage.tsx` 상세 sheet에 조치 사진 영역 추가, `QualityInspectionsPage.tsx` 재검사 상단에 근거 통합 영역, `IqcReportWorkspace.tsx`의 `ReinspectionComparison` 확장. 사진 표시·업로드는 기존 `QualityPhotoEvidence`/`iqc-photo` 계열 component 패턴을 재사용한다.
- loading/empty/error/success: 사진 로딩 실패와 "사진 없음"을 구분하고, 근거 로딩 실패가 판정 자체를 막지 않게 한다.
- 공통 Action Feedback: 업로드·삭제·조치 완료 버튼 아래 tone 있는 메시지, 업로드 중 disabled.
- 접근성: 사진 설명(alt) 필수 입력, `aria-live` 안내, keyboard 조작 회귀.
- 390px/mobile/narrow pane: 현장 촬영을 위한 `capture="environment"` 입력, 사진 grid 세로 재배치, overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 조치 완료 → 재검사 업무·알림 생성 계약(`EnsurePendingReinspectionAsync`) 불변. 내 업무 deep link 불변.
- 권한/관리자: `Pending.Read`/`Pending.Manage`와 프로젝트 접근 scope 재사용. 신규 permission 없음.
- Excel/PDF/첨부: Pending 선택 Excel에 사진은 포함하지 않는다(장수 요약 column 추가 여부는 구현 중 비확장 원칙 유지). 기존 IQC·품질 PDF 불변.
- Teams/Mail: 변경 없음.
- 삭제·복구/감사: 프로젝트 soft-delete·purge 시 Pending과 함께 사진이 제거되는 기존 cascade lifecycle을 따른다. 확정 사진은 운영 중 삭제 경로가 없다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | Pending 전용 bounded table + Draft/Confirmed·회차 lifecycle. 검증 로직은 IQC/패널 패턴을 Pending store에 자체 구현. 재검사 화면은 원본 사진을 참조로 노출 | 기존 finalized 증빙 table·store 무변경, 검증된 패턴 재사용, 조치 lifecycle을 정확히 표현, 회귀 반경 최소 | sniff/size 검증 로직이 도메인별로 반복됨 (기존 구조와 동일한 수준) |
| B | 범용 공통 attachment service/table로 IQC·패널·Pending 사진을 통합 | 장기적 중복 제거 | finalized IQC·패널 사진의 이관 또는 이중화 필요 — "기존 report·사진 무수정" 불변조건과 충돌, 범위·위험 과대 |
| C | 사진을 조치 완료 시점 1회 업로드로만 받고 Draft 단계 생략 | 구현 단순 | 현장에서 여러 장을 순차 촬영·검토·삭제하는 흐름 불가, 대용량 단일 요청 실패 시 조치 완료 전체 재시도 부담 |

비차단 정책 7건에 대한 권장안(2차 기획 자동 채택 대상):

1. 저장 공통화 — Pending 전용 bounded table로 패턴만 재사용(후보 A). 기존 store 무수정.
2. 상한 — 회차당 5장·15MB(기존 IQC/품질과 동일), Pending 한 건 누적 25장.
3. 임시 업로드·원자 확정 — Draft 상태 업로드 + 조치 완료 transaction 내 확정, row lock + version CAS + sha256 중복 판정.
4. 삭제 — Draft만 담당자가 삭제 가능, 확정 후 append-only를 DB trigger로 강제.
5. 읽기 scope — Pending 상세와 동일한 프로젝트 접근 scope + `Pending.Read`. 임시 사진도 조회는 허용(확정 전 badge), 변경은 담당자 전용.
6. 백업·보존 — 동일 PostgreSQL bytea 저장으로 기존 DB backup 경계에 포함. 보존은 Pending lifecycle을 따르고 프로젝트 purge에서 함께 제거. 운영 storage 용량·restore rehearsal은 운영 전환 Task로 이관.
7. migration/backfill — additive 신규 table만 추가, backfill 없음. 기존 text-only Pending·기존 재검사는 사진 0장으로 자연 표시.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. 격리된 실험·테스트 DB에만 적용한다.
- migration 필요 여부: additive 신규 migration 1건 (현재 최신 `0065` 다음 번호, 구현 시점 재확인). 기존 table·번호 무수정, forward-fix 원칙.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 비활성 유지.
- runtime 교체 여부: 없음. 고정 사용자 검수 runtime의 HMR/`dotnet watch` 갱신만 사용.
- 추가 사용자 승인 필요 작업: local commit·push·PR·merge·Persistent UAT 반영은 이번 범위에 포함하지 않으며 각각 별도 승인 경계를 유지한다 (`localCommitApproved: false`).

## 14. 검증 계획

- 최소 테스트: 신규 migration의 fresh+기존 DB 적용, 신규 table constraint·immutability trigger 검증 (`PostgreSqlMigrationTests` 패턴). Pending store 단위 — 형식·MIME 위장·5MB·6장째·담당자 아님·상태 위반·확정 후 삭제 차단, Draft 삭제, 조치 완료 원자 확정, 동일 요청 재시도·동시 업로드/전환 경쟁.
- 영향 영역 회귀: 패널 품질 재검사 합격 원자 종결·불합격 재개, IQC 재검사 종결, 사진 0장 조치 완료 하위 호환, Pending 목록·상세·코멘트·Excel 기존 테스트. Backend 전체·Frontend 전체(vitest)·typecheck·lint·production build.
- PR/CI: 이번 범위는 local 실험 개발이며 push·PR 없음. isolated Full-Stack E2E lifecycle에 조치 사진 round-trip을 추가해 실행한다.
- 사용자 검수: 고정 검수 runtime에서 시나리오 A~E를 desktop·390px로 확인하는 user validation checklist를 작성하고, fast-track 규칙에 따라 페이지별 desktop/mobile screenshot을 증빙으로 남긴다. 상태는 `사용자 검수 대기 — 마지막 일괄 검수`로 관리한다.

## 15. 완료 기준

- 기능/권한/데이터: 성공 기준 전체 충족 — 선택적 사진 첨부·원자 확정, 재검사 화면 통합 조회, 서버 차단(형식·위장·용량·권한·확정 후 삭제), 중복 없는 재시도·동시성, 기존 text-only Pending·검사 사진·PDF 비회귀.
- UX: desktop·390px에서 업로드·조회·판정 가능, overflow 0, console error 0.
- 자동 테스트: 신규·영향 테스트와 전체 회귀 green, migration fresh 적용 성공.
- 5종 산출물: implementation report에 상태·위치를 추적 (SOP·user manual은 기존 Pending·품질 문서의 section 갱신 가능, Roadmap·원장 update, user validation checklist).
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`로 기록.
- PR 상태: N/A — local experiment 범위, push·PR·merge 제외.
- 중단 조건: 기존 finalized 증빙 table 수정이 불가피해지거나, Pending 상태 전이 계약을 바꿔야 하거나, Persistent UAT·실제 provider 경계를 넘어야 하면 구현을 중단하고 보고한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1~7 | 12장의 비차단 정책 7건 (공통화·상한·확정 방식·삭제·scope·보존·backfill) | 각 항목 권장안 명시 | 불필요 — experiment standing instruction에 따라 Codex review 후 2차 기획에서 권장안 자동 채택 |

사용자 승인 대기 항목은 없다. Codex review에서 의미 있는 충돌이나 안전 blocking decision이 발견되면 자동 채택하지 않고 보고한다.

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: `backend/src/Emi.Qms.Api/Pending/` (store·contracts·endpoints), `QualityInspections/` 재검사 상세 조회, `Materials/` IQC 재검사 source 조회.
- Frontend: `frontend/src/PendingPage.tsx`, `QualityInspectionsPage.tsx`, `IqcReportWorkspace.tsx`(필요 시 `MaterialsWorkspace.tsx`), `api.ts`·`qualityInspections.ts` type, `styles.css`.
- DB/Migration: `database/migrations/`에 additive 신규 1건 (사진 table·index·immutability trigger).
- Tests/Scripts: `backend/tests/Emi.Qms.Api.Tests/` Pending·품질·migration 테스트, frontend vitest, isolated Full-Stack E2E 시나리오.
- Docs: Roadmap·실험 원장 상태 갱신, implementation report와 5종 산출물.

## 18. Roadmap 연결

- 선행 Task: `TASK-007A`(Pending), `TASK-009A`(IQC 사진), `TASK-012A`(검사·재검사 계약), `TASK-011A`(제조 중단 Pending) — 모두 `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE`이며 재구현하지 않는다.
- 후속 Task: 운영 전환 Task(운영 storage 용량·backup 운영·restore rehearsal, Roadmap 추적 항목 73의 운영 측면), 코멘트·등록 시점 첨부가 필요해지면 별도 `NEW_FEATURE`.
- 현재 Go/No-Go: 실험 원장 남은 Task 우선순위 0과 일치 (`roadmapSequenceMatch: true`). Go — 단, 이 planning은 구현 승인이 아니며 Codex review와 2차 기획 gate를 거친다.
- 별도 Task로 분리할 항목: 운영 storage·restore rehearsal, Pending Excel에 사진 포함, 알림 내용 확장.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-30 | 사용자 요청: 모든 Pending 조치 완료 시 선택적 사진 첨부, 재검사 화면에서 부적합 근거·조치 근거 통합 확인 (interview `COMPLETED_CONFIRMED`) | 이 1차 기획 전체 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

1. 2차 기획 확정 후 새 구현 세션에서 instruction chain gate를 수행하고 `taskType: APPROVED_FEATURE_IMPLEMENTATION`으로 시작한다.
2. additive migration을 다음 번호로 추가한다: Pending 조치 사진 table (bytea·mime·sha256·alt·slot·Draft/Confirmed·회차·확정자·확정 시각), 조회 index, 확정 row update/delete 차단 trigger. fresh DB와 기존 실험 DB 양쪽에 검증한다.
3. Pending store에 업로드(담당자·`조치 중`·형식·5MB·회차 5장/15MB·누적 25장 검증, magic byte sniffing, sha256 중복 판정), Draft 삭제, content 조회를 구현하고, 기존 `TransitionAsync`의 `InProgress → ReinspectionRequested` transaction에 Draft 일괄 확정(회차 부여)을 포함한다. Pending row lock과 `ExpectedVersion` CAS를 유지한다.
4. `PendingDetailResponse`에 회차별 사진 metadata를 추가하고, 패널 품질 재검사 상세와 IQC `ReinspectionSource`에 원 부적합 report 사진 참조와 최신 조치 회차 사유·사진을 추가한다. 기존 finalized table·PDF·알림 계약은 수정하지 않는다.
5. Frontend: Pending 상세의 정책 보류 안내를 조치 사진 UI로 대체하고(등록 폼 안내는 조치 단계 첨부로 문구만 정정), 재검사 두 화면에 ①부적합 근거·사진 ②조치 내용·사진 ③판정 UI 세로 흐름을 구성한다. 기존 사진 component·action feedback 패턴을 재사용하고 390px을 검증한다.
6. 14장의 검증 계획을 실행하고, 차단 경로·동시성 테스트를 포함한 전체 회귀 green을 확인한 뒤 페이지별 desktop/mobile screenshot, implementation report와 5종 산출물 추적, user validation checklist를 작성한다. local commit 이후의 Git 게시는 별도 승인 경계를 따른다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 0
