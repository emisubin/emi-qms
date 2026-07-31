# TASK-ATTACHMENT-001 — Pending 조치 사진 첨부와 재검사 근거 통합 조회 2차 기획 (최종 구현 계약)

> 상태: 2차 기획 — Codex review 반영 완료, 구현 세션 인계용 최종 계약
> 목적: 1차 기획의 승인 범위를 보존하면서 Codex 내용 review의 추가·보류·제거를 확정해, 이 문서 하나로 구현 범위·권한·상태·data lifecycle·UX·검증·제외 범위를 판단할 수 있게 한다

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-ATTACHMENT-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/attachment-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/attachment-001-planning.md` (수정하지 않고 판단 이력으로 보존)
- codexReviewSource: `tasks/attachment-001-review.md` (`secondPlanningRecommendation: PROCEED`)
- approvalChangeSource: `tasks/attachment-001-change-001.md` (`fableSecondPlanningApproved: true`, exact target `docs/45-pending-action-attachment-plan.md`)

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다. 이 문서는 1차 기획·review를 대체하는 요약이 아니라 두 문서의 resolution을 병합한 단일 구현 source of truth다.

## 0. 확정된 기준선

- 사용자가 확인한 업무 문제: 모든 종류의 Pending 조치 완료 시 조치 근거 사진을 선택적으로 첨부할 수 없고, 품질 재검사 화면에서 최초 부적합 근거·사진과 조치 내용·사진을 한 자리에서 대조할 수 없다.
- 대상 사용자·역할: 조치 담당자(Pending assignee), 품질 재검사 담당자, Pending 조회·코멘트 가능한 전 부서.
- 사용자 확정 정책(interview 6개 항목): ① 조치 사진은 선택 입력 ② JPEG·PNG만, 장당 최대 5MB ③ 재검사 화면은 최초 부적합 근거·사진 → 조치 내용·사진 → 판정 UI의 세로 한 흐름 ④ 원본 사진 덮어쓰기·교체 금지, 이력 근거 보존 ⑤ 조치 사진 확정 권한은 조치 담당자에게만, 다른 부서는 조회·코멘트만 ⑥ 대표 Repository·`main`·Persistent UAT·실제 provider·push·PR·merge 제외.
- 비차단 정책 7건(저장 공통화·상한·확정 방식·삭제·읽기 scope·보존·backfill)은 experiment standing instruction에 따라 1차 기획 권장안 + Codex review resolution으로 이 문서 12장에서 확정한다.
- Codex review 결론: 후보 A(전용 bounded table) 채택, 구현을 막는 결정 없음. 유지 7건·추가 6건·보류 4건·제거 2건은 이 문서 본문에 병합했고 12장에 대응표를 남긴다.
- 현재 Repository 재확인 결과(2차 기획 시점): Pending은 `pending_issues`/`pending_comments`/`pending_history`(모두 Pending 삭제 시 cascade)와 `Registered → ActionRequested → InProgress → ReinspectionRequested → Closed` 단선 전이, row lock(`for update`) + `version` CAS를 사용한다. 재검사 불합격 시 `ReopenQualityIssueAfterFailedReinspectionAsync`가 Pending을 `ActionRequested`로 되돌린다. IQC 재검사 근거는 `IqcReinspectionSourceResponse`가 이전 실패 attempt와 `pending_history`의 최신 `ReinspectionRequested` 전환 reason을 동적으로 조합하고, 패널 품질 재검사는 항목별 `PreviousFailureEvidence` 문자열만 제공한다. 기존 사진 저장은 `iqc_report_photos`·패널 품질 사진의 bytea + MIME 제약 + SHA-256 + 5MB + finalize 후 immutability trigger 패턴이며, idempotency는 `panel_quality_operations`·`sales_settlement_operations` 같은 append-only operation receipt 패턴이 확립되어 있다. 최신 migration은 `0065`다. 전역 `UploadSecurityMiddleware`(설정 기반 multipart 검사: 크기 상한, 이미지 metadata 거부, 악성코드 스캔, fail-closed)가 추가되어 있으며 도메인 검증과 독립적으로 동작한다.

Interview 문서에 없는 사용자 답변을 추측하지 않았다. 이 문서는 main merge·Persistent UAT·실제 provider·게시 승인을 부여하지 않는다.

## 1. 한 줄 목표

조치 담당자는 Pending 조치 완료 시 근거 사진을 선택적으로 남기고, 품질 재검사 담당자는 최초 부적합 근거·사진과 확정된 조치 회차의 사유·사진을 같은 화면에서 위→아래 한 흐름으로 대조한 뒤 판정할 수 있다.

## 2. 배경과 해결할 업무 문제

- 조치 담당자는 현재 조치 내용을 text 코멘트와 전환 사유로만 남긴다. Pending 등록 폼에는 "파일 첨부 준비 중 — 보안 저장·검역 정책 확정 전에는 파일을 받지 않습니다" 정책 보류 안내(`frontend/src/PendingPage.tsx`)가 남아 있다.
- 패널 품질 재검사는 항목별 `previousFailureEvidence` 문자열, IQC 재검사는 실패 사유·조치 사유 문자열만 표시하며 최초 부적합 사진과 조치 근거 사진은 어디에도 없다.
- 현재 우회 방식은 사진을 시스템 밖(메신저·개인 저장소)에 두는 것이어서 근거 유실·대조 누락·판정 지연이 발생한다.
- 기존 IQC·패널 품질 사진은 검사 report에 종속된 증빙이라 Pending 조치 회차 lifecycle(조치 중 임시 → 조치 완료 확정 → 재조치 회차 반복)을 표현하지 못한다.
- 이 기능이 없으면 조치 완료의 실물 근거가 시스템에 남지 않아 재검사 판정 품질과 감사 추적성이 떨어진다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 조치 담당자 (Pending assignee, `Pending.Manage`) | `조치 중`에 Draft 사진 추가·삭제, 조치 완료로 사진 snapshot 확정 | 담당 Pending의 Draft + 확정 사진 전체 | 본인이 담당한 Pending의 Draft 사진만 |
| 품질 재검사 담당자 (`Quality.*` + `Pending.Read`) | 재검사 화면에서 원 부적합 근거·사진과 확정 조치 회차 사유·사진 대조, 판정 | 접근 가능 프로젝트의 검사 근거 + 확정(Confirmed) 조치 사진 | 사진 변경 없음 (판정은 기존 검사 계약) |
| 그 외 전 부서 (`Pending.Read`, 코멘트는 `Pending.Manage`) | Pending 상세에서 **확정 사진만** 조회, 코멘트 작성 | 프로젝트 접근 scope 내 Pending의 Confirmed 사진 | 사진 변경 없음 |
| system administrator | 기존 Pending 관리 권한과 동일 조회 | Confirmed 사진 (Draft는 담당자 전용) | 사진 확정 권한 없음 |

Codex review 확정 사항: **확정 전 Draft 사진은 현재 조치 담당자에게만 노출한다.** 1차 기획의 "다른 부서도 확정 전 badge와 함께 Draft 조회" 표시안은 제거한다. 미확정·삭제 가능 사진이 공식 근거처럼 소비되는 혼동과 불필요한 노출을 막기 위함이며, metadata 응답과 content endpoint 모두에 적용한다.

기존 전이 권한 매트릭스는 변경하지 않는다. `조치 완료` 전환은 현재 계약대로 담당자 본인 또는 coordinator가 수행할 수 있고, 그 transaction이 담당자가 올린 Draft를 확정하며 확정자(전환 actor)를 기록한다. Draft의 추가·삭제는 어떤 경우에도 현재 담당자 본인만 가능하다. Backend가 authoritative layer이며 Frontend의 버튼 숨김·비활성화는 보조 수단이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 조치 사진 첨부와 조치 완료

1. 조치 담당자가 배정된 Pending을 열어 상태를 `조치 중`으로 전환한다(기존 흐름).
2. 상세 화면의 조치 사진 영역에서 JPEG/PNG 사진을 장당 5MB 이하로 추가하고, 필요하면 확정 전에 삭제한다. 사진 설명(alt text)을 필수로 입력한다. 각 요청은 `operationId`와 최신 Pending version을 보내며 성공 시 새 version이 반영된 최신 상세를 돌려받아 연속 촬영을 이어간다.
3. `조치 완료`(기존 `InProgress → ReinspectionRequested` 전환)를 누르면 전환 사유와 함께 현재 Draft 사진들이 같은 transaction에서 조치 회차 snapshot으로 원자 확정되고, 그 전환 사유가 회차의 조치 사유 snapshot으로 함께 고정된다.
4. 시스템은 기존과 동일하게 재검사 업무·알림을 생성하고, 확정된 회차 사진과 사유 snapshot은 이후 수정·삭제할 수 없다.

### 시나리오 B — 재검사 화면의 근거 통합 조회와 판정

1. 재검사 담당자가 재검사 요청 업무(패널 품질검사 또는 IQC 재검사)를 연다.
2. 화면 상단부터 ① 최초 부적합 근거(판정 사유·부적합 항목 근거 text)와 당시 확정 report의 사진 ② 최신 확정 조치 회차의 사유 snapshot·확정 조치 사진 ③ 기존 재검사 판정 UI가 세로 한 흐름으로 표시된다.
3. 근거 사진 한 장의 content 로딩이 실패해도 판정 자체는 막히지 않되, 해당 자리에 `사진을 불러오지 못함`을 표시해 누락을 숨기지 않는다.
4. 합격 판정 시 기존 계약대로 Pending이 원자 종결되고, 불합격이면 Pending이 `조치 요청`으로 되돌아간다.

### 시나리오 C — 재조치 회차 반복

1. 재검사 불합격으로 Pending이 `조치 요청`으로 돌아온다(기존 `ReopenQualityIssueAfterFailedReinspectionAsync` 흐름, 무변경).
2. 담당자가 다시 `조치 중`으로 전환하면 새 회차의 빈 Draft 영역이 시작된다. 이전 회차의 확정 사진·사유 snapshot은 회차 label과 함께 읽기 전용으로 계속 표시된다.
3. 두 번째 조치 완료 시 새 회차 snapshot이 확정되고, 재검사 화면에는 최신 회차가 우선 표시되며 이전 회차는 접힌 이력으로 조회할 수 있다.

### 시나리오 D — 사진 없는 조치 완료 (하위 호환)

1. 담당자가 사진 없이 조치 사유만 입력하고 조치 완료한다.
2. 기존과 동일하게 전환·재검사 요청이 진행된다. 사진 0장 회차는 회차 row를 만들지 않으며, 재검사 화면의 조치 사진 영역은 "등록된 조치 사진 없음"으로 표시되고 조치 사유는 기존 `pending_history` 파생 방식(현행 IQC 동작)으로 표시된다.

### 시나리오 E — 차단·복구 경로

- 담당자가 아닌 사용자의 업로드·삭제, `조치 중`이 아닌 상태의 업로드, JPEG/PNG가 아니거나 MIME 위장·5MB 초과 파일, 회차 6장째·회차 15MB 초과·Pending 누적 26장째 업로드, 확정 사진 삭제 시도는 모두 서버가 안정적인 한글 오류 메시지로 거부한다.
- 동일 `operationId` 재시도는 처음과 같은 결과를 반환하고, 다른 operation으로 같은 내용(sha256)의 사진을 다시 올리면 명확한 중복 오류로 차단한다.
- version 충돌(CAS 실패) 시 응답 안내에 따라 최신 상세를 다시 불러와 이어간다. 조치 완료 전환이 실패하면 사진은 Draft로 남는다.

## 5. 기능 요구사항

### 필수

- [ ] Pending 조치 사진의 bounded 저장: DB `bytea`, magic byte sniffing 기반 JPEG/PNG 검증, SHA-256, 장당 5MB — 기존 IQC·패널 품질 사진과 동일 패턴을 Pending 전용 table에 적용.
- [ ] `Draft → Confirmed(round)` lifecycle: `조치 중` 상태에서 현재 담당자만 추가·삭제, `조치 완료` 전환 transaction 안에서 회차 번호와 조치 사유 snapshot을 부여해 원자 확정.
- [ ] 확정 사진 append-only: DB trigger + 서버 검증으로 Confirmed row의 update/delete 차단(기존 finalized 증빙 guard trigger 패턴).
- [ ] 사진 mutation API는 `operationId + expectedPendingVersion`을 필수로 받고 성공 시 Pending version을 증가시키며, 최신 Pending 상세(새 version 포함)를 반환한다. 동일 `operationId` 재시도는 같은 결과를 반환한다(operation receipt).
- [ ] 같은 Pending 안에서 동일 sha256 사진의 별도 operation 업로드는 명확한 중복 오류로 차단.
- [ ] Draft 사진 metadata·content는 현재 담당자에게만 제공. 다른 사용자는 Confirmed만 조회.
- [ ] Pending 상세 API에 회차별 확정 사진 그룹(회차 번호·조치 사유 snapshot·확정자·확정 시각·사진 metadata)과 담당자 전용 Draft 목록·잔여 한도 포함. 사진 content 조회 endpoint 제공(인증 + `Pending.Read` + 프로젝트 접근 scope 검증).
- [ ] 사진 reference DTO는 `sourceKind`·source 식별자(`reportId` 또는 `pendingId`)·`photoId`·metadata만 제공하고 binary·내부 storage 경로를 포함하지 않는다. content는 각 권한 검증 endpoint에서만 반환한다.
- [ ] 패널 품질 재검사 상세에 원 부적합 report 사진 참조와 최신 확정 조치 회차(사유 snapshot·사진 참조) 포함, 화면에 ①부적합 근거·사진 ②조치 내용·사진 ③판정 UI 세로 흐름 표시.
- [ ] IQC 재검사(`IqcReinspectionSourceResponse`)에 원 부적합 report 사진 참조와 최신 확정 조치 회차 참조 추가, `ReinspectionComparison` 영역 확장. 확정 회차가 있으면 그 사유 snapshot을 사용하고, 없으면 기존 `pending_history` 파생 방식을 유지한다.
- [ ] 모든 Pending 유형(Nonconformance·Punch·ManufacturingStop·Other)의 조치 완료에서 동일 동작. 품질 미연결 Pending은 기존 수동 종결 경로를 유지하며 사진은 상세에서 조회.
- [ ] 재검사 근거 사진의 content 로딩 실패는 판정을 막지 않되 `사진을 불러오지 못함`을 화면에 표시.
- [ ] 동일 요청 재시도·동시 요청이 중복 사진·중복 이력을 만들지 않음(Pending row lock + version CAS + operation receipt + sha256 중복 차단).
- [ ] desktop과 390px 모바일에서 업로드·조회·판정 흐름 동작, 접근성(alt text 필수, keyboard, `aria-live` feedback).

### 선택

- [ ] Pending 상세 history 영역에서 회차별 사진 확정 시각·장수 요약 표시(별도 history event 추가 없이 사진 데이터로 파생).
- [ ] 재검사 화면에서 이전 회차 조치 사진 접기/펼치기(기본 접힘).

### 명시적 제외 (Codex review 보류 4건 포함)

- [ ] Pending 등록 시점의 사진·파일 첨부(조치 완료 근거만 이번 범위), 코멘트 첨부.
- [ ] 사진 이외 파일 형식(PDF·문서), Excel/PDF 출력물에 조치 사진 포함.
- [ ] 범용 attachment service와 기존 IQC·패널 품질 사진 table 통합.
- [ ] 외부 object storage/CDN, 바이러스 스캐너 신규 연동, 운영 storage 용량 산정과 restore rehearsal(운영 전환 Task). 기존 전역 `UploadSecurityMiddleware`는 설정으로만 동작하며 이 Task가 새로 연동·활성화하지 않는다.
- [ ] 알림·외부 provider에 사진 첨부 또는 URL 발송, 알림 channel·내용 변경(기존 재검사 요청 알림 유지).
- [ ] 기존 IQC·LQC·OQC 실패 report·사진의 수정 또는 schema 변경.
- [ ] 대표 Repository, `main`, Persistent UAT, 실제 provider, push·PR·merge.

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| Pending 상세 (조치 담당자, `조치 중`) | Pending 목록/내 업무 | Draft 사진 목록(파일명·설명·용량), 회차별 확정 사진, 회차 5장/15MB·누적 25장 잔여 counter | 사진 선택(`capture="environment"`), 설명 필수 입력, 등록·삭제, 조치 완료 | action 바로 아래 성공·오류 메시지, 업로드 중 중복 submit 차단, version 충돌 시 재조회 안내 |
| Pending 상세 (그 외 사용자) | Pending 목록/프로젝트 상세 | 회차별 확정 사진 read-only. **Draft는 표시하지 않음** | 사진 확대 조회, 코멘트 작성(기존) | 확정 근거만 표시됨을 명시("조치 중 사진은 조치 완료 시 공개") |
| 패널 품질 재검사 (`QualityInspectionsPage`) | 재검사 요청 업무/Pending 연결 | ① 최초 부적합 판정 사유·항목별 근거·당시 report 사진 ② 최신 확정 조치 회차 사유·사진(이전 회차 접힘) ③ 기존 판정 UI | 위→아래 확인 후 합격/불합격 판정 | 기존 판정 feedback 유지, 사진 로딩 실패 시 `사진을 불러오지 못함` 표시하되 판정 가능 |
| IQC 재검사 (`IqcReportWorkspace`) | 재검사 업무/Pending 연결 | 기존 `ReinspectionComparison`에 원 부적합 사진·확정 조치 회차 사유·사진 추가 | 부적합 항목 재확인 후 판정 | 기존 feedback 유지, 동일한 로딩 실패 표시 |

확인할 UX 항목:

- 현재 상태 이해 — Draft(담당자 전용)/확정, 회차 번호, 잔여 장수·용량을 badge와 counter로 표시한다.
- 다음 행동 명확성 — 조치 중에는 "사진은 선택 사항이며 조치 완료 시 확정·공개됩니다"를 안내한다.
- 결과 근접 표시 — 기존 공통 action feedback 패턴(`quality-photo`/`iqc-photo` 계열 UI)을 재사용한다.
- 권한·오류 상태 — 비담당자·`조치 중` 아님·한도 초과의 이유를 표시하되 서버 차단이 최종 기준이다. 전역 업로드 보안 검사가 활성화된 환경에서는 그 거부 메시지(문제 형식 응답)를 그대로 표시한다.
- 좁은 화면 — 390px에서 업로드·조회·판정의 page-level horizontal overflow 0을 유지한다.

## 7. 업무 규칙과 불변조건

- 조치 사진은 선택 입력이다. 사진이 없어도 조치 완료·재검사·종결의 기존 계약은 변하지 않는다.
- JPEG/PNG만 허용하고 장당 5MB 이하, 회차당 최대 5장·총 15MB, Pending 한 건 누적 최대 25장이다.
- Draft의 추가·삭제는 해당 Pending의 현재 담당자만, 상태가 `조치 중`일 때만 가능하다. Draft는 담당자에게만 노출된다.
- 사진 mutation은 `operationId + expectedPendingVersion` 없이는 수행되지 않으며, 성공 시 Pending version이 1 증가한다.
- 조치 완료 전환과 사진 확정·조치 사유 snapshot 고정은 하나의 DB transaction이다. 전환이 실패하면 사진은 Draft로 남는다. 전환 권한은 기존 매트릭스(담당자 또는 coordinator) 그대로다.
- 확정된 사진과 회차의 조치 사유 snapshot은 어떤 경로로도 수정·교체·삭제되지 않는다(append-only, DB trigger 강제). 이후 코멘트·이력 변경이 과거 조치 근거를 바꾸지 않는다.
- 기존 IQC·LQC·OQC finalized report·사진·PDF와 그 immutability trigger는 변경하지 않는다. 원 부적합 사진은 복사하지 않고 참조로만 노출한다.
- Pending 상태 전이·권한 매트릭스·재검사 원자 종결(`합격 → Closed`)·재개(`불합격 → ActionRequested`) 계약은 그대로 유지한다. `pending_history` event enum은 확장하지 않는다.
- 사진 content 응답은 인증 + `Pending.Read` + 프로젝트 접근 scope(`ProjectAccessScope` 패턴)를 통과한 요청에만 제공하고 `Cache-Control: private, no-store`를 사용한다. Draft content는 현재 담당자에게만 제공한다. 내부 식별자·raw 오류를 노출하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| Pending 조치 사진 (`pending_action_photos` 후보) | Pending 종속 사진 binary + metadata(display_name slot, normalized_mime, byte_size, sha256, alt_text), 상태(Draft/Confirmed), 확정 회차, 조치 사유 snapshot, 등록자·확정자·시각 | 신규 | Confirmed는 append-only, trigger 보호 |
| 사진 operation receipt (`pending_photo_operations` 후보) | 업로드·삭제의 `operation_id` PK + payload fingerprint + 결과 projection. 동일 operation 재시도 replay, 다른 payload 재사용 거부 | 신규 | append-only trigger (기존 `panel_quality_operations` 패턴) |
| 조치 회차 (action round) | 조치 완료 전환마다 확정 시 부여되는 회차 번호. 사진 row의 확정 column으로 표현하며 별도 table 없음. 사진 0장 회차는 row를 만들지 않음 | 신규(파생 개념) | 회차·사유 snapshot·확정자·확정 시각 기록 |
| Pending issue/comment/history | 기존 상태 전이·사유·코멘트 | 기존 | 변경 없음 |
| IQC/패널 품질 report 사진 | 원 부적합 근거 사진 | 기존 | 읽기 전용 참조만 추가 |

```text
[조치 중] 사진 Draft (현재 담당자만 추가·삭제·조회)
    → 조치 완료 전환 transaction에서 Confirmed(round=N, 조치 사유 snapshot) 원자 확정
    → 재검사 불합격 시 Pending 재개(ActionRequested), 다음 회차 Draft는 빈 상태로 시작
    → Confirmed 사진·사유 snapshot은 Pending 종결 후에도 불변 보존
```

- 신규 table은 `pending_issues`를 참조하며(`on delete cascade`), 삭제 lifecycle은 기존 `pending_comments`/`pending_history`와 동일하게 프로젝트 purge에 따른 Pending 삭제에서만 함께 제거된다. 운영 중 확정 사진의 삭제 경로는 없다.
- Draft row는 회차·사유 snapshot·확정자·확정 시각이 null이고, 확정 update 한 번(Draft → Confirmed)만 허용한다. Confirmed row의 update/delete는 trigger가 차단하고 Draft delete는 허용한다.
- 검증 constraint는 `iqc_report_photos` 패턴을 따른다: display_name slot(회차/Draft 집합 내 `photo-[1-5]` 형식 후보), mime `image/jpeg|image/png`, `byte_size` 1~5,242,880 + `octet_length(content) = byte_size`, sha256 hex, alt_text 1~200자. 같은 Pending 안 sha256 중복 차단은 unique index 후보로 구현 조사에서 확정한다. column·constraint 세부 명칭은 구현 조사에서 확정하되 이 계약의 의미를 바꾸지 않는다.

## 9. API·Backend 계약

- Backend가 authoritative한 규칙: 담당자·상태·형식·용량·장수·중복·확정 후 불변·Draft 비공개의 전부.
- 조회와 mutation (기존 `MaterialsEndpointExtensions`/`QualityInspectionEndpointExtensions` 사진 endpoint 형식 재사용):
  - `POST /api/pending/{pendingId}/photos` — multipart `[FromForm]`: `operationId`(uuid), `expectedPendingVersion`(int), `altText`, `photo`(IFormFile). `RequestSizeLimit` 6MB, `DisableAntiforgery`, `Pending.Manage` + 현재 담당자 + `조치 중` 검증. 성공 시 version 증가 후 최신 `PendingDetailResponse` 반환.
  - `DELETE /api/pending/{pendingId}/photos/{photoId}` — `operationId + expectedPendingVersion` 필수, Draft만, 현재 담당자만. 성공 시 version 증가 후 최신 상세 반환.
  - `GET /api/pending/{pendingId}/photos/{photoId}/content` — 인증 + `Pending.Read` + 프로젝트 접근 scope 검증 후 binary 반환(기존 `GetPhotoContentAsync` + `Cache-Control: private, no-store` 패턴). Draft content는 현재 담당자 외 404/거부.
  - `PendingDetailResponse` 확장 — 회차별 확정 사진 그룹(회차 번호, 조치 사유 snapshot, 확정자 표시명, 확정 시각, 사진 metadata 목록)과 담당자 전용 Draft 목록·잔여 한도. Draft 목록은 담당자가 아닌 actor에게 비어 있는 것이 아니라 제공되지 않는 구조로 설계한다.
  - 사진 reference DTO(신규, 재검사 화면 공용) — `sourceKind`(`PanelQualityReport` | `IqcReport` | `PendingAction` 후보), source 식별자, `photoId`, `displayName`, `normalizedMime`, `byteSize`, `altText`만 포함. binary·경로 미포함, content는 각 도메인의 권한 검증 endpoint URL로만 접근.
  - 패널 품질 재검사 상세 확장 — 원 부적합 확정 report의 판정 사유·항목 근거(기존)와 그 report 사진 참조 + 연결 Pending의 최신 확정 조치 회차(사유 snapshot·사진 참조).
  - IQC `IqcReinspectionSourceResponse` 확장 — 이전 실패 report 사진 참조 + 최신 확정 조치 회차 참조. 확정 회차가 있으면 `ActionReason`은 그 snapshot을 사용하고, 없으면 기존 `pending_history` 조회를 유지한다(하위 호환).
- 권한·validation: 업로드·삭제는 현재 assignee 본인 + `Pending.Manage`. content-type 위장은 magic byte sniffing으로 차단(기존 IQC/패널 검증 방식과 동일 로직을 Pending store에 구현). 전역 `UploadSecurityMiddleware`는 활성 환경에서 추가 방어층으로 동작할 뿐 이 도메인 검증을 대체하거나 전제하지 않는다.
- transaction·동시성·idempotency: Pending row `for update` lock + `ExpectedVersion` CAS. 사진 mutation은 operation receipt로 동일 `operationId` 재시도를 replay하고, 같은 `operationId`에 다른 payload fingerprint가 오면 거부한다. 같은 Pending의 동일 sha256 별도 operation은 중복 오류. 조치 완료는 기존 `TransitionAsync` transaction 안에서 Draft 일괄 확정 + 회차 부여(잠금 하 `max(round)+1`) + 전환 reason snapshot 저장을 수행한다.
- audit trail: 사진 row와 operation receipt 자체가 등록자·확정자·시각·hash를 가진 감사 기록이다. `pending_history` event enum은 확장하지 않는다.
- 외부 provider 영향: 없음. 알림 내용·channel 변경 없음.

상기 이름은 조사 기반 후보이며, 구현 세션이 내부 클래스명·컬럼명·SQL 형태를 확정하되 이 장의 계약 의미를 바꾸지 않는다.

## 10. Frontend 계약

- route/component: `PendingPage.tsx` 상세 sheet에 조치 사진 영역 추가(담당자 uploader + 회차별 확정 grid), 등록 폼의 "파일 첨부 준비 중" 정책 보류 안내를 "사진 첨부는 조치 단계에서 가능합니다" 안내로 정정. `QualityInspectionsPage.tsx` 재검사 상단에 근거 통합 영역, `IqcReportWorkspace.tsx`의 `ReinspectionComparison` 확장. 사진 표시·업로드·인증 fetch는 기존 `QualityPhotoEvidence`/`PhotoEvidence`(`iqc-photo` 계열) component 패턴을 재사용한다.
- loading/empty/error/success: "사진 없음"과 로딩 실패를 구분하고, 실패 시 `사진을 불러오지 못함` placeholder를 표시하되 판정·저장을 막지 않는다.
- 공통 Action Feedback: 업로드·삭제·조치 완료 버튼 아래 tone 있는 메시지, 진행 중 disabled, version 충돌 시 재조회 유도.
- 접근성: alt text 필수 입력, `aria-live` 안내, keyboard 조작 회귀.
- 390px/mobile: `capture="environment"` 입력, 사진 grid 세로 재배치, overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 조치 완료 → 재검사 업무·알림 생성 계약(`EnsurePendingReinspectionAsync`) 불변. 내 업무 deep link 불변.
- 권한/관리자: `Pending.Read`/`Pending.Manage`와 `ProjectAccessScope` 재사용. 신규 permission 없음.
- Excel/PDF/첨부: Pending 선택 Excel에 사진을 포함하지 않는다. 기존 IQC·품질 PDF 불변.
- Teams/Mail: 변경 없음.
- 삭제·복구/감사: 프로젝트 soft-delete·purge 시 Pending cascade lifecycle을 따른다. 백업은 동일 PostgreSQL bytea 저장으로 기존 DB backup 경계에 포함된다.

## 12. 확정된 정책 결정과 Codex review 대응

비차단 정책 7건의 확정(1차 권장안 + review resolution, Repository 근거와 trade-off 유지):

1. 저장 공통화 — Pending 전용 bounded table로 패턴만 재사용(후보 A). 기존 finalized 증빙 table·store 무수정. sniff/size 검증 로직의 도메인별 반복은 기존 구조와 동일한 수준의 의도된 비용이다.
2. 상한 — 회차당 5장·15MB(기존 IQC/품질과 동일), Pending 누적 25장.
3. 임시 업로드·원자 확정 — Draft 업로드 + 조치 완료 transaction 내 확정. row lock + version CAS + operation receipt + sha256 중복 차단.
4. 삭제 — Draft만 담당자가 삭제, 확정 후 append-only를 DB trigger로 강제.
5. 읽기 scope — Confirmed는 Pending 상세 조회 권한(`Pending.Read`) + content endpoint의 프로젝트 접근 scope. **Draft는 현재 담당자 전용**(1차 기획의 전체 공개 표시안 제거).
6. 백업·보존 — 동일 PostgreSQL bytea로 기존 DB backup 경계에 포함, 보존은 Pending lifecycle을 따름. 운영 storage 용량·restore rehearsal은 운영 전환 Task로 이관.
7. migration/backfill — additive 신규 migration만, backfill 없음. 기존 text-only Pending·기존 재검사는 사진 0장·기존 사유 표시로 자연 호환.

Codex review 대응표:

| Review 항목 | 이 문서의 반영 위치 |
| --- | --- |
| 유지 1~7 (lifecycle·상한·권한·참조 노출·0장 호환·content 검증·알림 불변) | 4~9장 전체에 보존 |
| 추가 1 — Draft 담당자 전용 노출 | 3·6·7·9장 |
| 추가 2 — `operationId + expectedPendingVersion` 필수, version 증가와 최신 상세 반환 | 5·7·9장 |
| 추가 3 — binary·경로 없는 reference DTO | 5·9장 |
| 추가 4 — 전환 reason의 회차 snapshot 고정 | 4·7·8·9장 |
| 추가 5 — 동일 operation replay, 동일 hash 별도 operation 중복 오류 | 4·5·9장 |
| 추가 6 — 로딩 실패 표시하되 판정 비차단 | 4·6·10장 |
| 보류 1~4 (통합 attachment·외부 storage/스캐너·등록/코멘트 첨부·알림 발송) | 5장 명시적 제외 |
| 제거 1 — 타 부서 Draft 조회 표시안 | 3·6장에서 제거 완료 |
| 제거 2 — 조치 사유 동적 조합 | 8·9장 snapshot으로 대체(무사진 회차만 기존 파생 유지) |

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. 격리된 실험·테스트 DB에만 적용한다.
- migration 필요 여부: additive 신규 1건 — 현재 최신 `0065` 다음 번호(구현 시점 재확인). 사진 table + operation receipt + index + immutability trigger. 기존 table·번호 무수정, forward-fix 원칙, fresh DB와 기존 실험 DB 양쪽 검증.
- 외부 발송/실제 데이터 영향: 없음. 실제 provider 비활성 유지.
- runtime 교체 여부: 없음. 고정 사용자 검수 runtime의 HMR/`dotnet watch` 갱신만 사용한다.
- 추가 사용자 승인 필요 작업: local commit·push·PR·merge·Persistent UAT 반영은 이번 범위에 포함하지 않으며 각각 별도 승인 경계를 유지한다. 이 2차 기획은 fast-track 규칙상 구현 진행 조건(blocking decision 0)을 충족할 뿐 게시·merge·Persistent UAT 승인을 부여하지 않는다.

## 14. 검증 계획

- 최소 테스트: 신규 migration의 fresh + 기존 실험 DB 적용, constraint·immutability trigger·operation receipt append-only 검증(`PostgreSqlMigrationTests` 패턴). Pending store 단위 — 형식·MIME 위장·5MB 초과·회차 6장째·회차 15MB 초과·누적 26장째·비담당자·상태 위반·확정 후 삭제 차단·Draft 삭제·조치 완료 원자 확정·사유 snapshot 고정·동일 `operationId` 재시도 replay·동일 hash 중복 오류·동시 업로드/전환 경쟁·version CAS 충돌.
- Draft 비공개 검증: 담당자 외 actor의 상세 응답에 Draft 미포함, Draft content 접근 거부.
- 영향 영역 회귀: 패널 품질 재검사 합격 원자 종결·불합격 재개, IQC 재검사 종결·기존 사유 파생 하위 호환, 사진 0장 조치 완료, Pending 목록·상세·코멘트·선택 Excel 기존 테스트. Backend 전체·Frontend 전체(vitest)·typecheck·lint·production build.
- PR/CI: 이번 범위는 local 실험 개발이며 push·PR 없음. isolated Full-Stack E2E lifecycle에 조치 사진 round-trip(업로드 → 확정 → 재검사 화면 노출)을 추가해 실행한다.
- 사용자 검수: 고정 검수 runtime에서 시나리오 A~E를 desktop·390px로 확인하는 user validation checklist를 작성하고, fast-track 규칙에 따라 페이지별 desktop/mobile screenshot을 증빙으로 남긴다. 상태는 `사용자 검수 대기 — 마지막 일괄 검수`로 관리한다.

## 15. 완료 기준과 중단 조건

- 기능/권한/데이터: interview 성공 기준 전체 충족 — 선택적 사진 첨부·원자 확정, 재검사 화면 통합 조회, 서버 차단(형식·위장·용량·권한·확정 후 삭제·Draft 비공개), 중복 없는 재시도·동시성, 기존 text-only Pending·검사 사진·PDF 비회귀.
- UX: desktop·390px에서 업로드·조회·판정 가능, overflow 0, console error 0.
- 자동 테스트: 신규·영향 테스트와 전체 회귀 green, migration fresh 적용 성공.
- 5종 산출물: implementation report에서 상태·위치 추적(SOP·user manual은 기존 Pending·품질 문서 section 갱신 가능, Roadmap·실험 원장 update, user validation checklist).
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- PR 상태: N/A — local experiment 범위.
- 중단 조건: 기존 finalized 증빙 table 수정이 불가피해지거나, Pending 상태 전이·전환 권한 계약을 바꿔야 하거나, 대표 repo·`main`·Persistent UAT·실제 provider 경계를 넘어야 하면 구현을 중단하고 보고한다.

## 16. 예상 변경 범위 (확정 allowlist가 아닌 조사 대상)

- Backend: `backend/src/Emi.Qms.Api/Pending/`(store·contracts·endpoints), `QualityInspections/` 재검사 상세 조회, `Materials/` IQC 재검사 source 조회.
- Frontend: `frontend/src/PendingPage.tsx`, `QualityInspectionsPage.tsx`, `IqcReportWorkspace.tsx`(필요 시 `MaterialsWorkspace.tsx`), `api.ts`·`qualityInspections.ts` type, `styles.css`.
- DB/Migration: `database/migrations/`에 additive 신규 1건(사진 table·operation receipt·index·trigger).
- Tests/Scripts: `backend/tests/Emi.Qms.Api.Tests/` Pending·품질·migration 테스트, frontend vitest, isolated Full-Stack E2E 시나리오.
- Docs: Roadmap·실험 원장 상태 갱신, implementation report와 5종 산출물.

## 17. Roadmap 연결

- 선행 Task: `TASK-007A`(Pending), `TASK-009A`(IQC 사진), `TASK-012A`(검사·재검사 계약), `TASK-011A`(제조 중단 Pending) — 모두 완료 상태이며 재구현하지 않는다.
- 후속 Task: 운영 전환 Task(운영 storage 용량·backup 운영·restore rehearsal — Roadmap 추적 항목 73의 운영 측면), 코멘트·등록 시점 첨부가 필요해지면 별도 `NEW_FEATURE`.
- 현재 Go/No-Go: 실험 원장 남은 Task 우선순위 0(첨부·사진 storage/검역/보존)과 일치. Go — 이 2차 기획이 해당 실험 Task의 최종 구현 source of truth다.

## 18. Codex 구현 지시문

1. 새 구현 세션에서 instruction chain gate를 수행하고 `taskType: APPROVED_FEATURE_IMPLEMENTATION`으로 시작하며, 이 문서를 최종 계약으로 사용한다. 1차 기획·review는 수정하지 않는다.
2. additive migration을 다음 번호로 추가한다: 조치 사진 table(bytea·mime·size·sha256·alt·slot·Draft/Confirmed·회차·조치 사유 snapshot·등록/확정자·시각), operation receipt table, 조회 index, Confirmed update/delete 차단 trigger(Draft→Confirmed 확정 update 1회만 허용), operation receipt append-only trigger. fresh DB와 기존 실험 DB 양쪽에 검증한다.
3. Pending store에 업로드(담당자·`조치 중`·형식 sniffing·5MB·회차 5장/15MB·누적 25장·sha256 중복 차단), Draft 삭제, content 조회(scope + Draft 담당자 전용)를 구현한다. 모든 mutation은 `operationId + expectedPendingVersion`을 요구하고 row lock + CAS + receipt replay로 idempotent하게 처리하며 성공 시 version을 올리고 최신 상세를 반환한다.
4. 기존 `TransitionAsync`의 `InProgress → ReinspectionRequested` transaction에 Draft 일괄 확정(잠금 하 회차 부여)과 전환 reason snapshot 저장을 포함한다. 전이 권한·재검사 업무/알림 계약은 변경하지 않는다.
5. `PendingDetailResponse`에 회차별 확정 사진 그룹과 담당자 전용 Draft·잔여 한도를 추가하고, 패널 품질 재검사 상세와 IQC `ReinspectionSource`에 binary 없는 reference DTO로 원 부적합 report 사진과 최신 확정 조치 회차(사유 snapshot·사진)를 추가한다. 기존 finalized table·PDF·알림 계약은 수정하지 않는다.
6. Frontend: Pending 상세에 조치 사진 UI(담당자 uploader·확정 회차 grid·Draft 비공개), 등록 폼 안내 문구 정정, 두 재검사 화면에 ①부적합 근거·사진 ②조치 내용·사진 ③판정 UI 세로 흐름과 로딩 실패 표시를 구성한다. 기존 사진 component·action feedback 패턴을 재사용하고 390px을 검증한다.
7. 14장의 검증 계획을 실행해 차단 경로·동시성·Draft 비공개·snapshot 불변을 포함한 전체 회귀 green을 확인한 뒤, 페이지별 desktop/mobile screenshot, implementation report와 5종 산출물 추적, user validation checklist를 작성한다. local commit 이후의 Git 게시·Persistent UAT·실제 provider는 별도 승인 경계를 따른다.

---

이 2차 기획에서 사용자 결정이 필요한 미결 항목은 없다. 의미 있는 Repository 충돌이나 안전상 blocking decision은 발견되지 않았다.

openBlockingDecisionCount: 0
