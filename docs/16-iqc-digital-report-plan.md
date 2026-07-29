# TASK-009A — IQC 디지털 검사성적서·필수 사진·PDF Snapshot 2차 기획 (구현 계약)

> 상태: 구현 source of truth (experiment two-pass 2차 기획)
> 목적: Fable 1차 기획(`tasks/009a-planning.md`)의 승인된 방향과 Codex 내용 review(`tasks/009a-review.md`)의 모든 resolution을 하나의 구현 가능한 최종 계약으로 통합한다.

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-009A`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/009a-interview.md`
- firstPlanningSource: `tasks/009a-planning.md`
- codexReviewSource: `tasks/009a-review.md`
- approvalChangeSource: `tasks/009a-change-001.md`
- fastTrackMode: `EXPERIMENT_TWO_PASS`

이 문서는 `experiment/task-009a-iqc-digital-report` branch 한정 구현 계약이다. 대표 repo, GitHub `main`, push·PR·merge, Persistent UAT migration·write·runtime handover, 실제 provider 발송은 어떤 것도 승인하지 않는다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 따르며 여기에 복사하지 않는다.

## 1. 한 줄 목표

품질 IQC 담당자가 모바일에서 요청 건의 검사 항목을 체크·입력하고 필수 외함 사진을 등록해 판정하며, 완료된 성적서를 읽기 전용 원본과 동일 snapshot PDF로 언제든 다시 확인할 수 있게 한다.

## 2. 배경과 결정 요약

TASK-008A는 `material_receipts`·`material_iqc_attempts`·`material_receipt_events` 원장, `Requested → Passed/Failed` attempt 상태, 부적합 시 Pending 생성·재검사 gate, `materials:iqc:{attemptId}` idempotency 기반 work item을 구현했지만 판정 근거는 자유 형식 사유 1개뿐이다. Roadmap 13·19장은 IQC 체크·값 입력·외함 사진 필수·성적서 PDF snapshot을 확정했다.

1차 기획에서 유지가 확정된 방향(모두 이 계약에 포함):

- versioned system template v1과 attempt별 최대 1개 성적서
- `Draft → Finalized` 최소 lifecycle, optimistic version lock, 완료 후 append-only
- isolated experiment PostgreSQL `bytea` 사진 보존, magic-byte JPEG/PNG 검증과 hash 기록
- 판정·snapshot·008A 상태·Pending·work item의 단일 transaction 처리
- legacy 판정을 거짓 성적서로 backfill하지 않는 표시 정책
- snapshot 기반 저장 PDF와 authorized download
- `/quality/iqc` 안의 모바일 우선 `항목 → 사진 → 검토·최종화` 흐름
- LQC/OQC·template 관리 UI·실제 고객 양식·object storage·Persistent UAT 제외

Codex review resolution으로 이번 계약에서 바뀐 것(1차 기획 대비):

| Review Finding | 이 계약의 확정 |
| --- | --- |
| `009A-READ-SCOPE` (P1) | 모든 read·download·mutation에 permission + 프로젝트 접근 scope를 함께 강제(4장) |
| `009A-LEGACY-BYPASS` (P1) | attempt에 명시적 `Legacy`/`Detailed` 판정 mode 저장, `/result`는 Legacy에서만 유지(5장) |
| `009A-FINALIZE-PDF-BOUNDARY` (P1) | PDF 렌더링을 판정 transaction에서 분리한 post-commit `Pending/Ready/Failed` artifact(9장) |
| `009A-GET-MUTATION` (P2) | GET은 무변경, Draft는 idempotent POST initialize(7장) |
| `009A-PHOTO-BOUNDS` (P2) | 파일당 5MB·report당 5장·총 15MB, normalized MIME, 합성 display name(8장) |
| `009A-PDF-EVIDENCE` (P2) | 필수 외함 사진을 PDF에 실제 포함, hash-only fallback 제거(9장) |
| `009A-FONT-DETERMINISM` (P2) | PDFsharp `6.2.4` + repository 동봉 OFL 한글 font + custom resolver + 고정 metadata(9장) |
| `009A-SNAPSHOT-CANONICAL` (P2) | 서버 생성 canonical UTF-8 JSON exact byte의 SHA-256(9장) |
| `009A-DOWNLOAD-GUARD` (P2) | 요청별 재인가, `private, no-store`, 합성 파일명, safe error(10장) |
| `009A-FRONTEND-BOUNDARY` (P3) | 전용 `IqcReportWorkspace` component/type/API module 분리(11장) |

제거 확정: GET get-or-create, permission-only 무범위 조회, 기존 `Requested` attempt까지의 `/result` 일괄 차단, transaction 내 PDF 렌더링, PDF hash-only 사진 fallback, 8MB×10장 한도, system font fallback과 렌더링 시각 metadata.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 서버 강제 |
| --- | --- | --- |
| 품질 IQC 담당 | Draft 초기화·항목 저장·사진 등록/삭제·최종화·PDF retry | `QualityInspect` policy + 해당 프로젝트 접근 scope |
| 자재 담당 | 기존 도착·IQC 요청·재검사·입고 확정, 결과·성적서 상태 조회 | 기존 `MaterialReceiptUpdate` 계약 유지, 검사 내용 변경 불가 |
| 생산관리·구매·Pending 담당·Read-only | 완료 성적서·사진·PDF 읽기 전용 조회 | 인증 + `ProjectRead` 계열 권한 + 프로젝트 접근 scope |
| System Administrator | 승인된 조회·감사 | 업무 mutation 우회 없음 |

신규 permission·policy는 추가하지 않는다.

## 4. 프로젝트 접근 scope 계약 (P1 resolution)

- 서버는 attempt → receipt → procurement item → project를 해석하는 공용 scope helper를 도입하고, 기존 `ProjectReadAll` 권한 + 프로젝트 key claim 검증 패턴(`ProjectAccessScope`·`CanAccessProject` 계열)을 재사용한다.
- 성적서·사진·PDF의 모든 GET·download와 모든 mutation은 policy 통과 후 이 scope 검증을 추가로 통과해야 한다. `QualityInspect` mutation도 예외가 아니다.
- `/api/quality/iqc/` queue projection은 호출자가 접근 가능한 프로젝트의 attempt만 반환한다.
- scope 밖 resource ID 요청은 존재 여부를 노출하지 않도록 일관되게 404로 응답한다(403/404 차이로 추측 가능한 존재 신호를 만들지 않는다).
- 매 요청마다 authorization을 다시 수행하며 캐시된 인가 결과를 binary 응답에 재사용하지 않는다.

## 5. 판정 mode와 상태 계약

```text
판정 mode (material_iqc_attempts.decision_mode):
  migration 0032 이전 존재 attempt = 'Legacy'   ← migration이 명시적으로 기록
  migration 이후 신규·재검사 attempt = 'Detailed' ← 시간 추정이 아닌 저장 값

Detailed report:
  (없음) --POST initialize(idempotent)--> Draft --POST finalize--> Finalized(불변)
                │                                                   ├─ pdfStatus: Pending
                ├─ PUT responses (expectedReportVersion)            ├─ pdfStatus: Ready
                └─ POST/DELETE photo (Draft only)                   └─ pdfStatus: Failed --bounded retry--> Ready

attempt:
  Requested --Detailed report finalize transaction--> Passed / Failed
  Requested --Legacy 기존 /result------------------> Passed / Failed
```

- 기존 `/api/quality/iqc/{attemptId}/result`는 `Legacy` + `Requested`에서만 기존 계약 그대로 동작한다. `Detailed` attempt 호출은 성적서 최종화를 안내하는 안정적 field-level 한글 validation 오류로 거부한다.
- 이미 판정된 attempt는 기존 사유와 `legacy 최소 판정` 표시만 제공하고 상세 성적서를 생성·backfill하지 않는다.
- 재검사는 항상 새 attempt(`Detailed`)·새 성적서로 시작하고 이전 성적서·사진·PDF를 보존한다.
- Finalized와 attempt 판정은 같은 transaction이므로 한쪽만 성공할 수 없다. PDF status는 판정을 되돌리지 않는 별도 derived artifact 상태다.
- attempt·receipt 상태 machine, Pending 생성·종결·재검사 gate, `receipt_completed` derived 단일 진실, `0030`·`0031` migration은 변경하지 않는다.

## 6. 데이터 모델과 migration `0032`

additive migration `0032` 1건. 기존 migration 수정·번호 재사용 없음. rollback은 forward-fix 원칙으로 문서화한다.

| 개념 | 내용 | 보존·감사 |
| --- | --- | --- |
| `material_iqc_attempts.decision_mode` | additive 컬럼: 기존 행을 `'Legacy'`로 명시 backfill 후 default `'Detailed'`·NOT NULL·CHECK | 판정 경로 재현 가능 |
| template / version / item | system template v1 + version(사용 후 불변) + 항목(순서·이름·안내·`Check|Text` 유형·필수·`requires_photo`·Text 최대 길이) | seed는 system-owned, 이번 Task에 편집 API 없음 |
| 성적서(report) | attempt당 1건(unique), `Draft|Finalized`, `version >= 1` optimistic lock, 종합 판정·사유(최종화 시 3~1,000자), actor·시각, canonical snapshot text + `snapshot_sha256`, `pdf_status Pending|Ready|Failed`·안전 오류 코드·마지막 시도 시각 | Finalized 후 전체 불변 |
| 항목 response | report × template 항목 unique, `Pass|Fail|NotApplicable`(Check)·text 값(Text)·비고, 길이 제한은 DB CHECK + 서버 이중 강제 | Finalized 후 불변 |
| 사진 | report·항목 연결, 합성 display name, normalized MIME(`image/jpeg|image/png`), `byte_size <= 5MB` CHECK, SHA-256, bounded 대체 설명, `bytea` content, actor·시각 | Draft에서만 추가·hard delete, Finalized 후 binary 포함 불변 |
| PDF artifact | report당 최대 1개(unique), snapshot hash 기록, content `bytea`·크기·SHA-256·생성 시각·고정 generator 식별 | 최초 성공 byte가 canonical, 삭제·교체 없음 |

system template v1 seed 항목(1차 기획 확정, 상세 현업 항목은 deferred):

| 순서 | 항목 | 유형 | 필수 | requires_photo |
| ---: | --- | --- | --- | --- |
| 1 | 품명·규격이 발주 정보와 일치 | Check | 필수 | — |
| 2 | 도착 수량이 등록 수량과 일치 | Check | 필수 | — |
| 3 | 외관 손상·오염 없음 | Check | 필수 | — |
| 4 | 식별 표시(라벨·명판) 확인 | Check | 필수 | — |
| 5 | 외함 상태 확인 | Check | 필수 | 필수(외함 사진) |
| 6 | 측정값·특이사항 | Text | 선택 | — |

font asset: repository에 OFL 한글 font binary(예: Noto Sans KR 계열)를 동봉하고 license 전문·출처·SHA-256을 함께 기록한다.

## 7. API 계약

모든 endpoint는 3·4장 권한+scope를 강제하고, 오류는 안정적 status와 field-level 한글 메시지를 사용한다. GET은 DB를 변경하지 않는다.

| Endpoint | 동작 | 계약 |
| --- | --- | --- |
| GET `/api/quality/iqc/` | queue 조회 | scope 필터, `decisionMode`·성적서 상태·`pdfStatus` additive 필드 |
| GET `/api/quality/iqc/{attemptId}/report` | 성적서 또는 template preview 조회 | 무변경. 기존 성적서(+response·사진 metadata) 또는 `Detailed`·`Requested`면 현재 template preview 반환, `canEdit` flag 포함 |
| POST `/api/quality/iqc/{attemptId}/reports` | Draft 초기화 | idempotent: attempt unique key 충돌 시 기존 성적서 반환. `Detailed` + `Requested` + `QualityInspect`만 |
| PUT `/api/quality/iqc/reports/{reportId}/responses` | 항목 일괄 저장 | `expectedReportVersion` 필수, Draft only |
| POST `/api/quality/iqc/reports/{reportId}/photos` | 사진 upload | multipart + `expectedReportVersion`, Draft only, 8장 guard 전체 적용, 요청 크기 제한(기존 `RequestSizeLimitAttribute` 패턴) |
| DELETE `/api/quality/iqc/reports/{reportId}/photos/{photoId}` | 사진 삭제 | `expectedReportVersion`, Draft only |
| POST `/api/quality/iqc/reports/{reportId}/finalize` | 최종화 | result·종합 사유·`expectedReportVersion`·`ExpectedReceiptVersion`, 12장 invariant 검증 후 단일 transaction |
| GET `/api/quality/iqc/reports/{reportId}/photos/{photoId}/content` | 사진 다운로드 | 10장 다운로드 안전 계약 |
| GET `/api/quality/iqc/reports/{reportId}/pdf` | PDF 다운로드 | `Ready`: 저장 byte 제공. `Pending`: 202 + 상태. `Failed`: 409 + 안전 오류 코드. 상태를 명확히 구분 |
| POST `/api/quality/iqc/reports/{reportId}/pdf/retry` | PDF 재생성 | `Failed`에서만, snapshot hash별 idempotent, bounded(사용자 요청 1회가 무한 재시도를 만들지 않음) |
| POST `/api/quality/iqc/{attemptId}/result` (기존) | Legacy 판정 | `Legacy` + `Requested`만 기존 계약 유지. `Detailed`는 validation 거부 |

`/api/materials/receipts` 목록의 attempt projection에 `decisionMode`·`hasDetailedReport`·성적서 상태를 additive로 노출한다. 신규 알림 유형·외부 delivery는 만들지 않으며, 기존 IQC work item 완료와 합격 시 입고 확정 work item 생성(`materials:iqc:{attemptId}` idempotency)을 그대로 재사용한다.

## 8. 사진 upload·보존 계약

- 한도: 파일당 최대 5MB, report당 최대 5장, report 전체 최대 15MB. 최종화 시 `requires_photo` 항목마다 사진 1장 이상.
- client MIME·확장자를 신뢰하지 않는다. magic byte(JPEG `FF D8 FF`, PNG 8-byte signature)로 normalized MIME을 결정하고 불일치·기타 형식은 거부한다.
- raw filename을 저장하지 않는다. `photo-<n>.<jpg|png>` 형태의 합성 display name과 bounded 대체 설명(접근성용, 길이 제한)만 저장한다.
- snapshot에는 이미지 byte 자체가 아니라 사진의 size·normalized MIME·SHA-256·항목 연결·대체 설명만 포함한다.
- Draft에서만 추가·삭제할 수 있고 Finalized 후에는 binary를 포함해 불변이다.

## 9. Canonical snapshot과 PDF artifact 계약

최종화 transaction(원자적, 여기까지만 포함):

1. 12장 invariant 서버 검증
2. canonical snapshot 생성 — 서버가 고정 property·array 순서, UTF-8, 명시적 null 처리로 canonical JSON text를 직렬화하고 그 exact byte의 SHA-256을 저장한다. JSONB DB 표현에 hash를 맡기지 않는다.
3. report `Finalized` 고정 + `pdf_status = Pending`
4. attempt `Passed/Failed`·receipt 상태·(부적합) Pending 생성/(재검사 합격) 종결·IQC work item 완료·합격 시 입고 확정 work item·event 기록 — 기존 `MaterialsStore` 판정 transaction owner에 결합

PDF 렌더링(post-commit, transaction 밖):

- renderer 실패는 확정 판정을 rollback하거나 성적서를 Draft로 되돌리지 않는다. `pdf_status`를 `Failed` + 안전한 일반 오류 코드 + 마지막 시도 시각으로 기록한다.
- snapshot hash별 artifact는 최대 1개이며 retry는 idempotent하다. 최초 성공 이후에는 항상 저장된 동일 byte만 제공한다.
- PDF에는 성적서 header(프로젝트 코드·품목·차수·판정·검사자·시각·template version), 항목 결과와 필수 외함 사진을 실제 포함한다(저장된 사진 byte 재사용, JPEG 직접 embed). hash 목록만 남기는 fallback은 사용하지 않는다.
- 결정성: PDFsharp `6.2.4`를 명시적으로 사용하고 Core build custom font resolver로 repository 동봉 OFL 한글 font만 로드한다. OS system font fallback을 사용하지 않는다. PDF metadata에는 렌더링 현재 시각이 아니라 snapshot의 확정 시각만 넣고 항목·사진 순서를 고정한다.
- 중단 조건: font license·provenance·SHA-256 또는 실제 한글 렌더링을 검증하지 못하면 임의 fallback 없이 구현을 중단하고 Finding으로 보고한다.

## 10. 다운로드 안전 계약

- 사진·PDF content endpoint는 매 요청 authorization(권한+scope)을 다시 수행하고 `Cache-Control: private, no-store`를 사용한다.
- `Content-Disposition` 파일명은 합성된 안전한 이름(예: `iqc-report-<차수>.pdf`)만 사용하고 프로젝트명·사용자 입력·raw 식별자를 넣지 않는다.
- raw DB error, binary hash 전문, 내부 storage metadata를 client 오류 응답에 노출하지 않는다.

## 11. Frontend 계약

- 신규 `IqcReportWorkspace.tsx`와 전용 type/API helper module로 분리한다. `MaterialsWorkspace.tsx`의 `MaterialIqcPage`는 queue·선택 orchestration만 유지하고 편집기를 담지 않는다.
- 모바일(390px·Teams narrow): 한 화면 핵심 행동 하나 기준의 `항목 → 사진 → 검토·최종화` 단계, 44px hit area, camera/file input의 명시적 label, 사진 대체 설명 입력, page-level horizontal overflow 0. 기존 `AdaptiveLayoutProvider`·`MobileSheet`·LoadState·action 인접 feedback·중복 submit 차단·첫 오류 focus·`aria-live` 계약 재사용.
- Desktop: 같은 기능을 공유하는 detail 구성(단순 모바일 확대판 금지).
- 완료 성적서: 읽기 전용 compact summary + 사진 + PDF 상태 UI(`Ready` 다운로드 / `Pending` 안내 / `Failed` 재시도 버튼).
- Legacy attempt: `legacy 최소 판정` badge와 기존 사유 표시.
- 진행 요약: 최종화까지 남은 필수 항목 수·사진 누락을 화면 안에서 보여준다.

## 12. 업무 규칙과 불변조건

- 성적서 최종화만이 `Detailed` attempt의 판정 수단이다. `Legacy`는 기존 `/result`만 사용한다.
- 최종화 조건: 필수 항목 전부 응답, `NotApplicable`은 비고 필수, `requires_photo` 항목 사진 ≥1, 합격은 필수 항목 `Fail` 0건일 때만, 부적합은 `Fail` 항목 ≥1 또는 종합 사유로 근거 명시, 종합 사유 3~1,000자(기존 attempt.reason CHECK 재사용), `expectedReportVersion`·`ExpectedReceiptVersion` 일치.
- `Requested`·`Detailed` attempt에만 Draft 초기화·저장·사진 mutation이 가능하다.
- Finalized 성적서·response·사진·snapshot·PDF는 append-only다. hard delete·덮어쓰기를 제공하지 않는다.
- 사용된 template version은 불변이고 성적서는 항상 자신의 snapshot으로 렌더링된다.
- 이중 최종화·경쟁 저장은 attempt gate + report version + receipt version으로 차단되어 snapshot·PDF가 중복 생성되지 않는다.
- Text 항목 유형은 이번 범위에서 `Check`·`Text` 두 가지만 허용하고 길이·필수를 DB와 서버가 함께 제한한다.

## 13. 포함·제외 범위

### 포함

`0032` migration과 seed, Legacy/Detailed mode, 성적서 Draft/Finalize API, 사진 upload·보존, canonical snapshot, PDF artifact·retry·다운로드, scope 통합 authorization, queue·자재 projection 확장, 전용 frontend workspace, 검증 전체.

### 명시적 제외

- LQC·OQC·전진검수·FAT(`TASK-012A`), 관리자 template 편집·version 운영 UI(`TASK-ADMIN-002`)
- 실제 고객 PDF 양식·전자서명·승인 workflow·Excel import/export
- object storage·CDN·virus scanner·image transcoding pipeline, 실제 provider 발송, 신규 알림 유형
- PDF 재발행 이력·관리자 강제 재생성·batch regeneration
- 008A/008B 상태 machine·`0030`·`0031` 수정, Persistent UAT migration·write·runtime handover
- 대표 repo·GitHub `main`·push·PR·merge

## 14. 검증 계획

- Migration: catalog 정합, fresh/existing isolated apply, 기존 attempt `Legacy`·신규 `Detailed` 배정, seed·constraint 검증.
- 권한·scope matrix: 품질/자재/Read-only/System Administrator × in-scope/out-of-scope — mutation·read·download 전부, scope 밖 404 일관성.
- API 계약: GET 무변경(DB delta 0), 동시 POST initialize 단일 성적서, 저장 vs 최종화 경쟁, 이중 최종화 차단, `Detailed` `/result` 거부와 `Legacy` `/result` 회귀.
- Upload guard: 크기·개수·총량·magic byte·MIME 불일치·Draft-only 삭제·Finalized 불변.
- 최종화: 필수 항목·비고·사진 누락 차단, 결과 일관성, attempt·receipt·Pending·work item·event 원자성(부적합 Pending 생성, 재검사 합격 Pending 종결 포함).
- Snapshot·PDF: canonical byte hash 안정성, `%PDF` signature, 저장 byte 재다운로드 일치, `Failed → retry → Ready` idempotency, PDF 내 외함 사진 포함, font 렌더링 한글 검증.
- 회귀: 008A 도착·재검사·확정·마감·취소, 008B supplyType, Pending·work item idempotency 기존 filtered tests.
- Frontend: lint·typecheck·unit·build. isolated Full-Stack E2E(합성 JPEG/PNG fixture) — 요청→작성→사진→합격→입고 확정, 부적합→Pending→재검사→새 성적서, 완료 조회·PDF, legacy 표시 + 기존 spec.
- 산출물: `/quality/iqc` 작성·완료·legacy 상태의 desktop·390px synthetic screenshot과 synthetic PDF sample. 자동 검증 완료와 사용자 검수 완료는 별도 상태로 기록하며 사용자 검수 완료로 표시하지 않는다.

## 15. 구현 순서

1. `0032` schema·Legacy/Detailed backfill·seed·constraint와 migration tests
2. project scope helper·report contracts/store·canonical snapshot 직렬화
3. Draft·response·photo·finalize API와 기존 008A 판정 transaction 통합
4. PDFsharp `6.2.4`·동봉 OFL 한글 font feasibility proof(실패 시 중단·Finding), deterministic renderer와 artifact·retry
5. queue·자재 projection, Legacy `/result` 회귀, 다운로드 안전 계약
6. 전용 frontend report workspace·모바일 단계 UI·desktop detail·PDF 상태 UI
7. Backend targeted/전체, Frontend unit/build, isolated Full-Stack E2E
8. desktop·390px screenshot, implementation report·5종 산출물, local experiment commit

## 16. 안전 경계와 승인 상태

- 이 계약의 구현·검증·screenshot·local commit은 `tasks/009a-change-001.md`의 experiment 한정 승인 범위 안에서만 수행한다.
- pushApproved·prApproved·mergeApproved: `false`, mainMergeApprovalCount `0/3`, persistentUatApproved·externalProviderApproved: `false`. 이 문서는 게시·main merge·Persistent UAT를 승인하지 않는다.
- isolated PostgreSQL·synthetic 데이터·합성 이미지·provider disabled만 사용한다. 실제 사진·고객 데이터를 사용하지 않는다.
- 구현 중 이 계약과 Repository의 의미 있는 충돌, unsafe upload 경계, secret/개인정보 위험이 확인되면 fast-track으로 우회하지 않고 중단·보고한다.

## 17. Deferred 비차단 사용자 결정 (구현 차단 아님)

| 번호 | 항목 | 후속 위치 |
| ---: | --- | --- |
| 1 | 실제 현업 IQC 항목·고객 PDF 양식 확정(추적 11·12) — 확정 시 새 template version | 품질 회신 / `TASK-012A` |
| 2 | 운영 사진 storage·virus scan·장기 retention·backup(추적 73) | 운영 전환 전 결정 |
| 3 | 관리자 template 편집·version 운영 UI | `TASK-ADMIN-002` |
| 4 | `0032` 실 DB 적용·Persistent UAT handover·PDF dependency 대표 repo 반영 시점 | 별도 승인 절차 |

## 18. 완료 기준

- 5·7·12장 계약이 서버 authoritative로 동작하고 위반 시도가 모두 차단·테스트된다. 008A·008B 회귀 0.
- 11장 UX 확인 항목과 390px page-level overflow 0.
- 14장 검증 전체 PASS, 미실행 항목은 이유와 함께 implementation report에 기록.
- 5종 산출물 상태·위치 추적, 사용자 검수 상태는 `사용자 검수 대기`로 handoff.
- current experiment branch local commit까지. 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider 변경 0.

---

openBlockingDecisionCount: 0
