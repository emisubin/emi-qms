# TASK-009A — IQC 디지털 검사성적서·필수 사진·PDF Snapshot 기획안 (Fable 1차 기획)

> 상태: Draft
> 작성 단계: Codex 내용 review 전 (experiment two-pass 1차 기획)
> 목적: TASK-008A의 최소 IQC 판정을 versioned 체크리스트, 필수 외함 사진, 불변 snapshot과 PDF 출력을 갖춘 디지털 검사성적서로 확장하는 계약 확정

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/009a-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- fastTrackMode: `EXPERIMENT_TWO_PASS`
- sourceTask: `TASK-009A`
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 현재 `/quality/iqc`는 도착 건·검사 차수에 대해 합격/부적합과 3자 이상 사유 1개만 저장한다. 어떤 항목을 확인했는지, 어떤 사진을 근거로 판정했는지 재현할 수 없고, 승인 시점 성적서를 다시 출력할 수 없다.
- 대상 사용자·역할: 품질 IQC 담당(성적서 작성·사진 등록·최종 판정, `QualityInspect`), 자재 담당(요청·재검사·합격 후 입고 확정, 검사 내용 변경 불가), 생산관리·구매·Pending 담당(조회), Read-only·System Administrator(승인된 조회·감사, mutation 우회 금지).
- 정상 흐름: IQC 요청 → 성적서 작성 → 필수 항목·외함 사진 검증 → 합격/부적합 최종화 → 008A `Passed/FailedBlocked`·Pending·work item 원자 처리 → 읽기 전용 성적서·PDF 제공.
- 예외·복구 흐름: 필수 항목 미입력·필수 사진 누락·허용되지 않은 파일 형식과 용량·결과-항목 불일치의 field-level 한글 오류 차단, optimistic lock 기반 경쟁 저장/최종화 차단, 완료 성적서 수정·삭제 금지, 재검사는 새 attempt·새 성적서 instance.
- 확정한 정책과 명시적 제외: 품질 권한 authoritative, 외함 사진 필수, 완료 후 append-only, 재검사는 새 attempt, 008A 상태·Pending transaction·stage 전진-only 보존. 제외 — LQC/OQC/전진검수/FAT(`TASK-012A`), 관리자 template 편집 UI(`TASK-ADMIN-002`), 실제 고객 양식·전자서명·Excel, 외부 object storage·CDN·virus scanner·실제 provider, Persistent UAT·대표 repo·`main`·게시.
- planning으로 넘긴 비차단 미결정 사항: 초기 template 구조·항목, 작성/최종화 lifecycle, 사진 experimental storage, PDF snapshot 생성·보존 방식, 008A 완료 attempt 호환 — 이 문서 12장에서 Repository 근거와 함께 권장안을 확정 대상으로 명시한다(fast-track standing instruction에 따른 권장안 자동 채택).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

품질 IQC 담당자가 모바일에서 요청 건의 검사 항목을 체크·입력하고 필수 외함 사진을 등록해 판정하며, 완료된 성적서를 읽기 전용 원본과 동일 snapshot PDF로 언제든 다시 확인할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- TASK-008A가 `material_receipts`·`material_iqc_attempts`·`material_receipt_events` 원장과 `Requested → Passed/Failed` attempt 상태, 부적합 시 Pending 생성·재검사 gate, `materials:iqc:{attemptId}` idempotency 기반 work item 처리를 이미 구현했다(migration `0030`, 이 실험 계보의 `0031`까지 적용).
- 그러나 판정 근거는 자유 형식 사유 1개뿐이다. Roadmap 13장은 “IQC 체크, 값 입력, 외함 사진 필수, 성적서 PDF 출력”을 확정했고, 19장은 “PDF는 승인 또는 출력 시점 데이터의 snapshot”과 “필수 사진 미첨부 시 저장 차단”을 확정했다.
- 현재 우회 방식은 화면의 안내문(“품명·수량·외관·식별 정보를 확인한 뒤 판정하세요”)을 보고 확인 결과를 사유 텍스트에 뭉쳐 적는 것이다. 항목별 결과·사진 증빙·성적서 출력이 모두 불가능하다.
- 방치하면 부적합 조치·재검사·입고 확정의 품질 증빙이 분리된 채 남고, 고객·감사 대응 시 판정 당시 상태를 재현할 수 없다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 품질 IQC 담당 (`QualityInspect` policy) | 성적서 draft 작성·저장, 사진 등록·삭제(draft), 최종 합격/부적합 판정, 완료 성적서·PDF 조회 | 기존 IQC queue 범위 | 현재 `Requested` attempt의 성적서만 |
| 자재 담당 (`MaterialReceiptUpdate` policy) | 기존 IQC 요청·재검사 요청·합격 후 입고 확정, 판정 결과·성적서 상태 조회 | 기존 자재 범위 | 검사 내용 변경 불가(기존 계약 유지) |
| 생산관리·구매·Pending 담당 (`ProjectRead` 권한) | 완료 성적서·사진·PDF 읽기 전용 조회 | 기존 프로젝트 접근 범위 | 검사 mutation 없음 |
| Read-only·System Administrator | 승인된 조회·감사 | 기존 정책 범위 | 업무 mutation 우회 금지 |

신규 permission·policy는 추가하지 않는다. 성적서 mutation은 `QualityInspect`, 성적서·사진·PDF 읽기는 인증 + `ProjectRead` 권한을 서버에서 강제하며 다운로드도 authorization을 통과해야 한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 모바일 성적서 작성과 합격 판정

1. 품질 담당이 `/quality/iqc`에서 `Requested` 카드(또는 work item deep link)를 열면 시스템이 현재 활성 template version 기준의 성적서 draft를 get-or-create로 연다.
2. 한 열 단계형 화면에서 항목별 적합/부적합/해당없음을 체크하고 필요한 값·비고를 입력하며, 외함 항목에 camera/file input으로 JPEG/PNG 사진을 등록한다. 각 저장은 report version으로 경쟁을 차단하고 action 인접 feedback을 보여준다.
3. 검토 단계에서 종합 판정 사유(3~1,000자)를 입력하고 `합격 · 최종화`를 누른다. 서버가 필수 항목 완료·외함 사진 존재·결과 일관성·receipt version을 검증한 뒤, 기존 008A 판정 transaction(attempt `Passed`, receipt `Passed`, 재검사 합격 시 Pending 종결, IQC work item 완료, 입고 확정 work item 생성, event 기록)과 함께 성적서를 Finalized·불변 snapshot으로 고정한다.
4. 자재 담당은 기존 흐름대로 입고 확정하고, 관련 역할은 완료 성적서를 읽기 전용으로 열어 동일 snapshot PDF를 내려받는다.

### 시나리오 B — 부적합 판정과 재검사 성적서 분리

1. 검사 중 항목 하나가 부적합이면 해당 항목에 부적합 사유·사진을 남기고 종합 판정을 `부적합 · 입고 차단`으로 최종화한다.
2. 서버가 기존 계약대로 attempt `Failed`, receipt `FailedBlocked`, Pending 부적합 생성(또는 재사용)을 같은 transaction에서 처리하고 성적서를 Finalized로 고정한다.
3. 조치 후 자재 담당이 재검사를 요청하면 새 attempt가 생기고, 품질 담당은 새 성적서 instance를 처음부터 작성한다. 이전 attempt의 성적서·사진·PDF는 그대로 보존·조회된다.

### 시나리오 C — 완료 성적서·legacy 판정 조회

1. `/quality/iqc`에서 판정 완료 포함을 켜면 완료 attempt 카드에 성적서 여부가 표시된다.
2. 상세 성적서가 있는 attempt는 읽기 전용 성적서(항목·사진·판정·검사자·시각·template version)와 PDF 다운로드를 제공한다.
3. 이 기능 이전에 판정된 attempt는 `legacy 최소 판정` 표시와 기존 사유만 보여주고, 상세 성적서를 거짓으로 backfill하지 않는다.
4. 저장·최종화 경쟁은 409와 최신 상태 재조회 안내로 처리한다. 이미 최종화된 성적서에 대한 mutation은 모두 차단된다.

## 5. 기능 요구사항

### 필수

- [ ] versioned IQC 체크리스트 template 모델과 초기 system template v1 seed(8.1장 항목), 사용된 version 불변
- [ ] attempt별 성적서(1 attempt = 최대 1 report): Draft 저장, 항목 response(적합/부적합/해당없음·값·비고), report version optimistic lock
- [ ] 사진 upload: JPEG/PNG magic-byte 검증, 파일당 8MB·요청 크기 제한·report당 최대 10장, sha256 기록, Draft에서만 추가·삭제
- [ ] 최종화: 필수 항목 완료 + `requires_photo` 항목 사진 ≥1 + 결과 일관성 + 종합 사유(3~1,000자, 기존 attempt.reason 계약 재사용) + `ExpectedReceiptVersion`·report version 검증을 기존 008A 판정 transaction과 원자 처리
- [ ] 최종화 시 불변 snapshot(JSON + sha256) 저장, 이후 성적서·항목·사진의 모든 변경 차단(append-only)
- [ ] snapshot 기반 PDF artifact 생성·저장·authorized 다운로드(저장된 byte만 제공, 동일 snapshot 재현 보장)
- [ ] 기존 최소 판정 endpoint(`/api/quality/iqc/{attemptId}/result`)는 신규 판정 경로에서 성적서 최종화로 대체하고, 직접 호출은 한글 안내와 함께 거부(외함 사진 필수 정책 우회 차단)
- [ ] legacy 판정 attempt 호환 projection(`hasDetailedReport` 등)과 `legacy 최소 판정` 표시, backfill 없음
- [ ] `/quality/iqc` 모바일 우선 단계형 작성 UI(항목 → 사진 → 검토·최종화), 완료 성적서 읽기 전용 화면, desktop·390px·Teams narrow
- [ ] additive migration 1건(`0032`, 현재 최신 `0031` 이후), fresh/existing isolated 검증
- [ ] Backend·Frontend·isolated Full-Stack E2E(합성 JPEG/PNG fixture)와 페이지별 desktop·390px screenshot

### 선택

- [ ] 완료 성적서 화면의 사진 원본 크기 보기(lightbox) — 기본은 목록·확대 링크로 충분하면 구현 중 UX 판단에 위임
- [ ] PDF 내 사진 축소 포함 — 권장하되 크기·결정성 문제가 확인되면 사진 hash 목록 표기로 대체 가능

### 명시적 제외

- [ ] LQC·OQC·전진검수·FAT 전체(`TASK-012A`)
- [ ] 관리자 template 편집·version 운영 UI(`TASK-ADMIN-002`) — 이번에는 system template seed와 조회만
- [ ] 실제 고객 PDF 양식 확정, 전자서명·승인 workflow, Excel import/export
- [ ] 외부 object storage·CDN·virus scanner 운영 연동, 실제 provider 발송, 신규 알림 유형
- [ ] 008A 도착·재검사·입고 확정·마감 상태 machine과 `0030`·`0031` migration 수정
- [ ] Persistent UAT write·migration 적용·runtime handover, 대표 repo·GitHub `main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| IQC 검사함 (기존 `/quality/iqc`) | 품질 메뉴·work item deep link | 기존 카드 + 성적서 상태(미작성/작성 중/완료/legacy) | 카드 선택으로 작성 또는 조회 진입 | 기존 LoadState·오류 패턴 유지 |
| 성적서 작성 (`/quality/iqc` 내 attempt 선택 상태) | 검사함 카드 | 프로젝트·품목·차수 context, 단계형 항목 목록, 항목별 체크·값·비고·사진, 진행 요약 | 항목 입력, 사진 등록·삭제, draft 저장, 검토·최종화 | action 인접 저장 확인, field-level 한글 오류·첫 오류 focus·`aria-live`, upload 진행·실패·재시도, 중복 submit 차단, 409 재조회 안내 |
| 완료 성적서 조회 | 검사함·자재 카드의 결과 링크 | 읽기 전용 항목·사진·판정·검사자·시각·template version, PDF 다운로드 | 조회·다운로드만 | 권한 부족·미생성 상태 구분 안내 |
| legacy 판정 표시 | 완료 attempt | `legacy 최소 판정` badge + 기존 사유 | 조회만 | 상세 성적서가 없는 이유 안내 |

확인할 UX 항목:

- 작성 중 어디까지 입력했고 최종화에 무엇이 남았는지(필수 미완료 수·사진 누락)가 화면 안에서 읽히는가?
- 사진 등록이 44px hit area·camera/file input label·대체 텍스트와 함께 모바일에서 동작하는가?
- 합격/부적합 버튼이 결과 일관성 조건과 함께 명확히 구분되는가?
- 390px·Teams narrow에서 PC table 축소가 아닌 한 열 단계형 composition을 유지하고 page-level horizontal overflow 0인가? (기존 `AdaptiveLayoutProvider`·`MobileSheet`·MaterialsWorkspace 카드 패턴 재사용)

## 7. 업무 규칙과 불변조건

- attempt `Requested → Passed/Failed`와 receipt 상태 machine, Pending 생성·종결·재검사 gate, `receipt_completed` derived 단일 진실은 변경하지 않는다. 성적서 상태는 이를 우회하는 별도 판정 경로가 아니다.
- 성적서 최종화만이 신규 attempt의 판정 수단이다. 판정과 snapshot 고정은 하나의 transaction에서 처리되어 “판정은 됐는데 성적서가 없는” 상태를 만들지 않는다.
- `Requested` attempt에만 draft 작성·사진 등록이 가능하고, 작성자는 `QualityInspect` 권한이어야 한다.
- 최종화 조건: 필수 항목 전부 응답(해당없음은 비고 필수), `requires_photo` 항목 사진 ≥1, 합격은 필수 항목 부적합 0건일 때만, 부적합은 부적합 항목 ≥1 또는 종합 사유로 근거 명시.
- Finalized 성적서·항목 response·사진·snapshot·PDF는 append-only다. hard delete와 덮어쓰기를 제공하지 않는다(사진 삭제는 Draft에서만).
- 사용된 template version은 불변이며, 성적서는 항상 자신이 사용한 version과 snapshot으로 렌더링된다.
- 사진은 서버에서 MIME whitelist + magic byte + 크기·개수 제한을 통과해야 저장되고, 조회·다운로드도 서버 authorization을 통과해야 한다.
- 이미 판정된 attempt를 상세 성적서로 backfill하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| IQC template·version·항목 | system template v1 + version별 항목(순서·이름·안내·응답 유형·필수·`requires_photo`) | 신규 3 table | 사용된 version 불변, seed는 migration |
| 성적서(report) | attempt당 1건, `Draft → Finalized`, report version optimistic lock | 신규 table | Finalized 후 불변, actor·시각 기록 |
| 항목 response | report × template 항목, 적합/부적합/해당없음·값·비고 | 신규 table | Finalized 후 불변 |
| 사진 | report(항목 연결)별 binary + metadata(파일명·MIME·크기·sha256) | 신규 table (bytea) | Draft에서만 추가·삭제, Finalized 후 불변 |
| final snapshot | 최종화 시점 template·항목·response·사진 hash·판정·actor의 JSON + sha256 | 신규 컬럼(report) | append-only, PDF·조회의 단일 근거 |
| PDF artifact | snapshot에서 생성한 PDF binary + sha256 | 신규 table | 저장된 byte만 제공, 재생성은 snapshot 기반 |
| attempt·receipt·Pending·event 원장 | `material_iqc_attempts` 등 008A 계약 | 기존 재사용(스키마 변경 없음) | 기존 append-only 유지 |

```text
성적서: (없음) → Draft → Finalized(불변)
attempt: (기존 그대로) Requested → Passed / Failed   ← 성적서 Finalized와 같은 transaction
```

### 8.1 초기 system template v1 항목 (권장안, Roadmap 13장·현행 안내문 근거)

| 순서 | 항목 | 유형 | 필수 | requires_photo |
| ---: | --- | --- | --- | --- |
| 1 | 품명·규격이 발주 정보와 일치 | 체크 | 필수 | — |
| 2 | 도착 수량이 등록 수량과 일치 | 체크 | 필수 | — |
| 3 | 외관 손상·오염 없음 | 체크 | 필수 | — |
| 4 | 식별 표시(라벨·명판) 확인 | 체크 | 필수 | — |
| 5 | 외함 상태 확인 | 체크 | 필수 | 필수(외함 사진) |
| 6 | 측정값·특이사항 | 값 입력 | 선택 | — |

상세 현업 항목은 미확정(추적 12번)이므로 최소 확정 범위만 seed하고, 실제 항목 확정은 deferred 사용자 결정으로 남긴다.

Migration: `0032` additive 1건 — 신규 table·컬럼·CHECK·index·system template seed만 포함한다. `0030`·`0031`과 기존 migration은 수정하지 않으며 기존 데이터 이동·backfill이 없다. rollback은 destructive down이 아니라 forward-fix 원칙으로 문서화한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 7장 invariant 전부(권한, 최종화 조건, 사진 guard, 불변성, version 검증).
- 필요한 조회와 mutation(초안, 구현 시 기존 convention에 맞춰 확정):
  - GET 성적서 get-or-create(`Requested` attempt, `QualityInspect`): template version·기존 draft 반환
  - PUT draft 저장(항목 response 일괄, expected report version)
  - POST 사진 upload(multipart, Draft only) / DELETE 사진(Draft only)
  - POST 최종화(result, 종합 사유, expected report version + `ExpectedReceiptVersion`)
  - GET 완료 성적서 읽기 전용 조회·사진 content·PDF 다운로드(인증 + `ProjectRead`)
  - 기존 `/api/quality/iqc/` queue·자재 목록 projection에 성적서 상태·`hasDetailedReport` additive 필드
  - 기존 `/result` endpoint는 성적서 최종화 안내와 함께 validation 거부로 전환
- 권한·validation: 신규 policy 없음. 오류는 안정적 status와 field-level 한글 메시지. upload는 기존 Excel import 패턴(`RequestSizeLimitAttribute`, metadata 검증)을 사진용 magic-byte 검증으로 확장 재사용.
- transaction·동시성·idempotency: 최종화는 기존 `MaterialsStore.RecordIqcResultAsync`의 row lock·`ExpectedReceiptVersion`·Pending·work item(`materials:iqc:{attemptId}` 완료, 합격 시 확정 work item 생성)·event 로직을 transaction owner로 유지하고, 성적서 검증·snapshot 고정을 같은 transaction에 결합한다. 동일 최종화 중복 호출은 attempt status gate와 report 상태로 차단되어 snapshot·PDF가 중복 생성되지 않는다. draft 저장·사진 추가 vs 최종화 경쟁은 report version으로 차단하고 테스트한다.
- audit trail: attempt·receipt·Pending·event의 기존 append-only 기록에 더해 성적서 자체가 actor·시각·version·snapshot을 보존한다. 신규 event type은 추가하지 않는다(기존 `IqcPassed`/`IqcFailed` 재사용).
- PDF: 최종화 commit 후 snapshot에서 생성해 저장하고, 생성 실패 시 판정은 유지한 채 다운로드 시점에 snapshot 기반 재생성·저장한다. 저장 성공 이후에는 항상 저장된 byte만 제공해 동일 snapshot 재현을 보장한다. 라이브러리는 MIT license 계열(PdfSharp 6.x 등) + repository 동봉 OFL 한글 font를 권장하고, .NET 10 호환·한글 렌더링은 Codex review에서 실현 가능성을 대조한다.
- 외부 provider 영향: 없음. 신규 알림·delivery를 만들지 않는다.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 이 문서가 최종 확정하지 않으며, 구현 시 기존 소유 경계(판정 transaction은 `MaterialsStore`, template·성적서·사진·PDF는 신규 Quality 계열 store)에 맞춘다.

## 10. Frontend 고려사항

- route/component: 신규 route 없음. `/quality/iqc`의 `MaterialIqcPage`(`MaterialsWorkspace.tsx`)를 단계형 작성·조회 화면으로 확장하고 `materials.ts`·`api.ts` type을 확장한다.
- loading/empty/error/success: 기존 `LoadState`·`StateMessage`·action 인접 feedback 재사용. upload는 진행·실패·재시도 상태를 별도 표시.
- 공통 Action Feedback: 중복 submit 차단, 첫 오류 focus, `aria-live` 기존 계약 유지.
- 접근성: 항목 체크는 색상 외 텍스트 라벨, 사진에 대체 설명 입력, camera/file input의 명시적 label, 44px hit area.
- 390px/mobile/narrow pane: 한 열 단계형 composition과 기존 모바일 카드·`MobileSheet` 패턴 재사용, page-level horizontal overflow 0.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 기존 IQC work item deep link·완료 처리와 합격 시 입고 확정 work item 생성을 그대로 재사용한다. 신규 알림 유형 없음.
- 권한/관리자: 기존 policy 재사용. template 관리 UI는 `TASK-ADMIN-002`로 위임하고 이번에는 seed·조회만.
- Excel/PDF/첨부: Excel 계약 불변. 사진·PDF는 이 Task의 신규 첨부 경계이며 Roadmap 19장(필수 사진 저장 차단, PDF snapshot) 확정을 구현한다.
- Teams/Mail: 영향 없음(발송 없음).
- 삭제·복구/감사: 성적서·사진·snapshot·PDF append-only, 기존 원장 계약 유지.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| 1-A (권장) | versioned 최소 system template(3 table + seed) | 항목 확정(추적 12)·ADMIN-002 편집을 additive로 수용, 성적서가 version을 참조해 재현 가능 | 고정 컬럼보다 초기 구현량 증가 |
| 1-B | 성적서에 고정 컬럼으로 항목 하드코딩 | 가장 빠름 | 항목 변경마다 migration, ADMIN-002·012A 확장 불가 — Roadmap `requires_photo` template 권고와 충돌 |
| 2-A (권장) | 최소 Draft/Finalized 2상태 + report version lock | attempt 상태 authoritative 유지, 모바일 중단·재개 복구, stale 관리 단순 | Draft 방치 가능(재검사 시 새 attempt로 자연 해소) |
| 2-B | draft 없이 한 번에 제출 | 상태 최소 | 모바일 사진 다건 입력 중 유실 위험, upload를 제출과 원자 처리해야 함 |
| 3-A (권장) | 사진 DB bytea + magic byte·크기·개수 guard | isolated PostgreSQL 안에서 transaction·권한·backup 경계 일치, 별도 filesystem ownership 계약 불필요, E2E 격리 용이 | DB 용량 부담 — 실험 범위에선 수용, 운영 object storage 전환은 deferred(추적 73) |
| 3-B | filesystem 저장 + 경로 참조 | DB 가벼움 | 원자성·ownership·backup·경로 탈출 방지 계약을 새로 만들어야 하며 실험 격리 원칙과 충돌 |
| 4-A (권장) | 최종화 시 JSON snapshot 고정 + PDF 생성·저장, 실패 시 snapshot 기반 지연 생성, 이후 저장 byte만 제공 | drift 0(저장 byte 단일 진실), 판정 원자성과 binary 렌더링 분리, PDF signature·일관성 테스트 용이 | PDF 용량·font 동봉 필요, MIT 라이브러리 한글 렌더링 검증 필요 |
| 4-B | 요청 시마다 재생성 | 저장 공간 절약 | renderer·font 변경 시 drift — “동일 snapshot 재현” 요구와 충돌 |
| 5-A (권장) | 기존 판정 attempt는 `legacy 최소 판정` 표시, 신규 요청부터 상세 성적서 | 거짓 증빙 없음, 과거 이력 접근 유지 | 화면에 두 표현 공존 |
| 5-B | legacy를 빈 성적서로 backfill | 표현 통일 | 확인하지 않은 항목을 기록으로 남기는 거짓 증빙 — 이력 보존 원칙 위반 |

권장안 1-A·2-A·3-A·4-A·5-A를 채택한다(사용자 fast-track 지시에 따른 권장안 자동 채택). 기존 `/result` 최소 판정 경로의 신규 사용 차단도 이 선택의 일부다 — 유지하면 외함 사진 필수 확정 정책을 우회하는 판정 경로가 남는다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated PostgreSQL·synthetic 데이터·합성 JPEG/PNG만 사용한다.
- migration 필요 여부: `0032` additive 1건. 기존 migration 불변. 실 DB 적용은 별도 사용자 승인.
- 외부 발송/실제 데이터 영향: 없음. 실제 사진·고객 데이터를 사용하지 않는다.
- runtime 교체 여부: 없음. experiment branch 내 isolated 검증만 수행한다.
- 추가 사용자 승인 필요 작업: 실 DB migration 적용, PDF 신규 dependency의 대표 repo 반영(push·PR·merge — main merge는 분리된 승인 3회 전 금지), Persistent UAT handover, 운영 storage·retention 정책.

## 14. 검증 계획

- 최소 테스트: Backend Release build. targeted tests — draft 권한 allow/deny(품질/자재/Read-only/System Administrator), 필수 항목·해당없음 비고·사진 누락 최종화 차단, 결과 일관성, 사진 magic byte·MIME·크기·개수 guard, Draft-only 사진 삭제, Finalized 불변성, report version·receipt version 경쟁(저장 vs 최종화, 이중 최종화), 합격/부적합 최종화의 attempt·receipt·Pending·work item 원자성, legacy attempt projection, `/result` 거부, PDF `%PDF` signature·저장 byte 재다운로드 일치·snapshot sha256 일관성. migration catalog + fresh/existing isolated apply.
- 영향 영역 회귀: 008A 도착·재검사·입고 확정·마감·취소 filtered tests, 008B supplyType projection, Pending 연동, work item idempotency 회귀.
- PR/CI: 대표 repo PR 없음(실험 branch). Frontend lint/typecheck/unit/build와 isolated Full-Stack E2E(요청→작성→사진→합격→입고 확정, 부적합→Pending→재검사→새 성적서, 완료 조회·PDF 다운로드 + 기존 자재·구매·Pending spec)를 local에서 통과시킨다.
- 사용자 검수: desktop·390px에서 검사함, 작성 단계별, 완료 성적서, legacy 표시 화면의 synthetic screenshot과 synthetic PDF sample을 보고한다. 자동 검증 완료와 사용자 검수 완료는 별도 상태로 기록하고 사용자 검수 완료로 표시하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 시나리오 A~C가 서버 authoritative로 동작하고 7장 invariant 위반 시도가 모두 차단·테스트된다. 008A·008B 회귀 0.
- UX: 6장 확인 항목과 390px page-level overflow 0.
- 자동 테스트: 14장 전체 PASS, 미실행 항목은 이유와 함께 기록.
- 5종 산출물: implementation report에서 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기`로 handoff.
- PR 상태: N/A — 실험 branch local commit까지가 범위.

## 16. 미결정 사항

Blocking 결정은 없다. 아래는 명시적으로 deferred된 비차단 사용자 결정 항목이다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 실제 현업 IQC 항목·고객 PDF 양식 확정(추적 11·12) | system template v1 유지 / 회신 반영 새 version | 대기 (품질 회신) |
| 2 | 운영 사진 storage·virus scan·장기 retention·backup(추적 73) | DB 유지 / object storage 전환 | 대기 (운영 전환 전) |
| 3 | 관리자 template 편집·version 운영 UI | `TASK-ADMIN-002` planning | 대기 (Roadmap) |
| 4 | `0032` 실 DB 적용·Persistent UAT handover와 PDF dependency 대표 repo 반영 시점 | 별도 승인 절차 | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 신규 Quality 계열 store·contracts·endpoints(template·report·사진·PDF), `Materials/MaterialsStore.cs`(최종화 결합·projection·`/result` 거부), `Materials/MaterialsContracts.cs`, PDF dependency와 font asset
- Frontend: `MaterialsWorkspace.tsx`, `materials.ts`, `api.ts`, `App.tsx`(조회 연결), `styles.css`
- DB/Migration: `database/migrations/0032_*.sql` 신규 1건
- Tests/Scripts: `PostgreSqlMigrationTests.cs`, Materials/Quality Backend tests 신규·확장, Frontend unit, Full-Stack E2E spec과 합성 이미지 fixture
- Docs: Roadmap TASK-009A 실험 상태 기록(canonical 큐 불변), interview·planning·review·implementation report

## 18. Roadmap 연결

- 선행 Task: TASK-007A(Pending)·008A(자재·IQC 최소 판정)·008B(사급) — 이 실험 계보에 구현·자동 검증 완료(사용자 검수 대기, canonical 미반영). 실험 순서 재정렬은 interview Task Identity Gate에 `explicitRoadmapOverrideApproved: true`로 기록됨.
- 후속 Task: `TASK-010A`(키팅), `TASK-012A`(후속 품질 — 이 template·성적서·사진·PDF 기반 재사용), `TASK-ADMIN-002`(template 관리). canonical 실행 큐의 `Dependency Pending`과 다음 `TASK-007A` Gate는 변경하지 않는다.
- 현재 Go/No-Go: 이 문서는 1차 기획이다. Codex 내용 review → Fable 2차 기획(별도 승인된 target) 뒤에만 구현이 시작된다.
- 별도 Task로 분리할 항목: 16장 1~3번.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-17 | 사용자 experiment fast-track 지시(인터뷰 생략·권장안 자동 채택·local commit까지) | 비차단 정책 5건을 12장 권장안 1-A·2-A·3-A·4-A·5-A로 확정 대상 기록 |

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
2. `0032` additive migration으로 template·성적서·항목 response·사진·PDF artifact 스키마와 system template v1 seed를 추가한다. `0030`·`0031` 포함 기존 migration은 수정하지 않는다.
3. Backend: 성적서 get-or-create·draft 저장·사진 upload/삭제·최종화·읽기 전용 조회·PDF 다운로드를 7·9장 invariant대로 구현한다. 최종화는 기존 008A 판정 transaction owner에 결합하고, `/result` 직접 판정은 한글 안내와 함께 거부한다. MIT license PDF 라이브러리와 OFL 한글 font의 .NET 10 호환을 먼저 확인하고, 불가하면 구현을 멈추고 Finding으로 보고한다.
4. Frontend: `/quality/iqc`의 단계형 작성(항목→사진→검토·최종화), 완료 성적서 읽기 전용·PDF 링크, legacy 표시를 desktop·390px에서 구현하고 기존 LoadState·Action Feedback·MobileSheet 패턴을 재사용한다.
5. 검증: 14장 계획을 실행하고 실행/미실행을 분리해 implementation report에 기록한다. 합성 이미지·synthetic 데이터만 사용하고 Persistent UAT write, 실제 provider 발송, 대표 repo 게시를 수행하지 않는다.
6. 16장 deferred 항목을 임의 결정하지 않고 5장 제외 범위를 추가하지 않는다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 4
