# TASK-009A IQC 디지털 검사성적서 구현 보고

## 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-009a-iqc-digital-report`
- implementation / automaticValidation: `완료`
- userValidation: `대기`
- commit: `완료 — local experiment commit`
- push / PR / merge: `미승인·미실행`
- main merge approval: `0/3`
- Persistent UAT / provider / 대표 repo 영향: `없음`

## Task 목적·기획 source

품질 IQC 담당자가 모바일에서 검사 항목을 기록하고 필수 외함 사진을 등록해 판정하며, 완료 성적서를 수정 불가능한 snapshot과 동일한 저장 PDF로 다시 확인할 수 있게 한다.

Authoritative implementation contract는 Fable 2차 기획 [docs/16-iqc-digital-report-plan.md](../docs/16-iqc-digital-report-plan.md)다. Fable 1차 원문은 [009a-planning.md](009a-planning.md), Codex 내용 review와 resolution은 [009a-review.md](009a-review.md), fast-track 승인·사용량은 [009a-change-001.md](009a-change-001.md)에 분리 보존했다.

## 포함·제외 범위

포함:

- system-owned IQC template v1, `Check|Text` 항목과 attempt별 단일 성적서
- 기존 attempt `Legacy`, 신규·재검사 `Detailed`, Legacy `/result` 호환 gate
- side-effect 없는 preview GET, idempotent Draft initialize, optimistic report version
- 외함 사진 필수, JPEG/PNG magic-byte, 5MB/장·5장·총15MB, 안전한 합성 이름
- Finalized canonical UTF-8 snapshot·SHA-256과 기존 008A 판정/Pending/work item transaction 통합
- post-commit `Pending|Ready|Failed` PDF와 최초 성공 artifact의 DB·API 불변성
- PDFsharp 6.2.4, 동봉 Noto Sans KR OFL font, 한글 checklist·실제 사진 PDF
- permission + 프로젝트 scope read/mutation/download, binary `private, no-store`
- desktop detail과 390px 모바일 3단계 적응형 UI

제외:

- LQC/OQC/FAT, 관리자 template 편집, 실제 고객 양식·전자서명·Excel export
- object storage·CDN·virus scanner·image transcoding, 실제 provider
- Persistent UAT migration·runtime handover, 대표 repo·`main`, push·PR·merge

## 구현 결정과 영향

### DB·Backend

- additive `0032_iqc_digital_reports.sql`은 기존 attempt를 `Legacy`로 backfill한 뒤 신규 기본값을 `Detailed`로 고정한다. template/version/item, report/response/photo/PDF artifact 7개 테이블과 lifecycle·용량·hash constraint, 완료 증빙·PDF artifact 불변 trigger를 추가했다.
- queue·report·photo·PDF 저장소는 attempt에서 project까지 추적해 `ProjectAccessScope`를 적용한다. scope 밖 ID는 일관된 not-found로 처리하고 binary 응답은 매 요청 재인가한다.
- 상세 성적서 GET은 현재 template preview만 반환하며 row를 만들지 않는다. initialize POST는 attempt unique key로 idempotent하고 response/photo mutation은 report version과 Draft 상태를 함께 검증한다.
- 최종화는 canonical snapshot, 성적서·attempt·receipt 판정, 부적합 Pending, work item과 event를 하나의 transaction에 고정한다. PDF 렌더링 실패는 판정을 되돌리지 않고 별도 상태로 남긴다.
- 기존 간편 판정은 migration 이전 attempt에서만 유지하고 신규 Detailed attempt의 `/result` 우회를 서버에서 차단했다.

### PDF·font·다운로드

- PDFsharp `6.2.4`와 repository 동봉 Noto Sans KR variable font만 사용하는 custom resolver를 구현했다. OS font fallback과 렌더링 현재 시각 metadata를 사용하지 않는다.
- font 출처·OFL·SHA-256을 `Assets/Fonts`에 기록했다. binary SHA-256은 `194018e6b2b293a7964f037b25c0249ce1418bc9ab3c971060a03aa57861e252`다.
- PDF는 확정 header·항목·판정·실제 사진을 포함하고 최초 성공 byte를 저장해 반복 다운로드에 같은 byte를 반환한다. 사진/PDF는 안전한 합성 파일명과 `private, no-store`만 사용한다.

### Frontend·적응형 UX

- 대형 Materials 화면에 편집기를 넣지 않고 `IqcReportWorkspace.tsx`, `iqc-report.ts`, 전용 API helper로 분리했다.
- desktop은 검사함 옆 독립 detail workspace를 사용하고, 모바일은 좌상단 숨김 메뉴에서 IQC 진입 후 `검사항목 → 사진 → 최종확인` 한 화면 한 행동 구조를 사용한다.
- 390px에서 PC 표를 축소하지 않고 compact card·단계 rail·원형 번호·타원형 판정 button·각진 progress strip·비대칭 완료 seal을 조합했다. page horizontal overflow는 0이다.
- 완료 성적서는 읽기 전용 항목·사진·PDF 상태를, Legacy 건은 소급 생성 없는 기존 판정 안내를 표시한다.

## 해결한 업무 문제

- 기존 IQC는 합격·부적합과 자유 형식 사유만 남아 어떤 항목과 외함 상태를 확인했는지 재검수할 근거가 부족했다.
- 신규 Detailed attempt는 필수 checklist와 사진 없이는 판정할 수 없고, 완료 뒤 snapshot·사진·PDF를 같은 증빙으로 보존한다.
- 모바일 현장 담당자는 PC 표를 줄인 화면 대신 좌상단 메뉴와 세 단계 전체 화면 흐름으로 핵심 action에 집중할 수 있다.

## 기술적 결정과 검토한 대안

- GET get-or-create 대신 조회와 idempotent initialize를 분리해 read 요청의 DB mutation을 제거했다.
- 판정 transaction 안에서 PDF까지 렌더링하는 안은 lock·rollback 위험 때문에 폐기하고, 판정은 원자적으로 확정하되 PDF는 post-commit 상태로 분리했다.
- OS font와 hash-only 사진 PDF 대신 동봉 OFL 한글 font와 실제 사진 embed를 선택해 환경 drift와 증빙 가치 저하를 막았다.
- 실험 단계의 object storage 대신 bounded PostgreSQL `bytea`를 사용했다. 운영 storage·scanner·transcoding은 별도 planning 대상이다.

## 시행착오 및 폐기한 접근

- 최초 Full-Stack E2E는 Debug만 최신이고 실행기가 `Release --no-build`를 사용해 이전 바이너리를 띄웠다. 최신 Release build를 선행한 뒤 endpoint·PDF 흐름을 재검증했다.
- 검사 시작 직후 E2E가 비동기 화면 전환 전에 항목 수를 읽어 0개로 처리했다. 단계 heading을 명시적으로 기다리는 사용자 동선 기반 검증으로 고쳤다.
- 모바일 URL 직접 진입은 기존 적응형 초기화가 프로젝트 화면으로 정규화했다. 실제 UX 계약대로 좌상단 메뉴에서 IQC를 선택해 검증했다.
- 최종 성적서 한 장만으로 PDF 상태가 viewport 밖에 가려져, 확정 요약과 PDF 준비 상태를 별도 screenshot으로 분리했다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 synthetic screenshot 9장 시각 검수는 완료했다.
- 사용자 직접 검수는 아직 수행하지 않았으며 [009a-user-validation-checklist.md](009a-user-validation-checklist.md) 상태는 `사용자 검수 대기`다.
- Persistent UAT·실제 고객 양식·실제 외함 사진·운영 font/PDF 출력 품질은 승인 범위 밖이라 검증하지 않았다.
- push·PR·merge는 미승인이고 main merge 승인 수는 `0/3`이다.

## 실제 변경 파일과 역할

- DB: `database/migrations/0032_iqc_digital_reports.sql`
- Backend: Materials contracts/endpoints/store, `IqcReportContracts.cs`, `IqcReportStore.cs`, `IqcPdfRenderer.cs`, DI·project package 설정
- Font: `Assets/Fonts/NotoSansKR-Variable.ttf`, `OFL.txt`, `PROVENANCE.md`
- Frontend: `IqcReportWorkspace.tsx`, `iqc-report.ts`, Materials orchestration·API·type·adaptive CSS
- Tests: migration/API integration, 기존 008A·008B E2E 회귀, 신규 `iqc-digital-report.full-stack.spec.ts`
- 기획·검토: interview, Fable 1차 planning, Codex review, Change 001, Fable 2차 planning
- 증빙: `tasks/009a-screenshots/*.png`, 이 보고서와 user validation checklist

## 실행한 자동 테스트와 결과

- Backend Release build: `PASS`, warning 0 / error 0
- Backend 신규·migration targeted: `PASS`; preview GET 무변경, initialize idempotency, Detailed 우회 차단, 필수 응답·사진, magic-byte 거부, Finalized 불변, PDF 동일 byte·cache guard 포함
- Backend 기존 008A legacy regression targeted: `6/6 PASS`
- Backend 전체: `373/373 PASS`
- Frontend lint: `PASS`(error 0, 기존 `main.tsx` Fast Refresh warning 1)
- Frontend unit: `76/76 PASS`
- Frontend typecheck + production build: `PASS`(기존 대형 chunk warning만 존재)
- Playwright full-stack spec discovery: `26 tests / 9 files`, `PASS`
- TASK-009A + TASK-008B isolated Full-Stack E2E: `2/2 PASS`, 전용 PostgreSQL DB·container cleanup 완료
- Browser visual QA: desktop 6장·390px 모바일 3장 확인, 모바일 horizontal overflow 0

미실행:

- Persistent UAT migration·runtime·실사용자 검증: 승인 범위 밖
- 실제 provider·고객 데이터·고객 PDF 양식: 범위 밖
- CI·GitHub PR: push·PR 미승인
- 사용자 직접 action 검수: screenshot handoff 후 대기

## 개인정보·secret 검토

- screenshot·E2E·PDF 검증은 합성 프로젝트·품목·역할 계정과 repository의 공개 logo fixture만 사용했다.
- Persistent UAT, 실제 고객·사용자·알림 원문은 읽거나 기록하지 않았다.
- raw filename은 저장하지 않고 합성 이름으로 대체했다. tracked diff에는 credential, token, private key, tenant/client/object ID를 추가하지 않았다.

## Finding gate

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `009A-READ-SCOPE` | P1 | `RESOLVED` | 무범위 증빙 조회 가능성 | queue/report/photo/PDF 전체에 project scope 적용 |
| `009A-FINALIZE-PDF-BOUNDARY` | P1 | `RESOLVED` | renderer 실패가 판정 원자성 훼손 | 판정 transaction과 post-commit artifact 상태 분리 |
| `009A-LEGACY-BYPASS` | P1 | `RESOLVED` | 기존 Requested 처리 중단 또는 신규 우회 | 명시적 Legacy/Detailed mode와 endpoint gate |
| `009A-GET-MUTATION` | P2 | `RESOLVED` | 조회가 Draft를 생성 | preview GET과 idempotent initialize POST 분리 |
| `009A-PHOTO-BOUNDS` | P2 | `RESOLVED` | 무제한 binary·raw filename 부담 | magic-byte·5/5/15MB·합성 이름 |
| `009A-PDF-EVIDENCE` | P2 | `RESOLVED` | hash-only PDF의 증빙 가치 부족 | 실제 필수 사진과 항목을 저장 PDF에 포함 |
| `009A-FONT-DETERMINISM` | P2 | `RESOLVED` | system font 의존 한글·byte drift | 고정 PDFsharp·동봉 OFL font·resolver·metadata |
| `009A-SNAPSHOT-CANONICAL` | P2 | `RESOLVED` | 직렬화 순서에 따른 hash drift | 고정 순서 exact UTF-8 text와 SHA-256 |
| `009A-DOWNLOAD-GUARD` | P2 | `RESOLVED` | binary cache·filename·인가 노출 | 요청별 scope, private/no-store, 합성 파일명 |
| `009A-PDF-ARTIFACT-MUTATION` | P2 | `RESOLVED` | DB 직접 update/delete 시 최초 PDF byte 교체 가능 | artifact update/delete 차단 trigger 추가 |
| `009A-FRONTEND-BOUNDARY` | P3 | `RESOLVED` | Materials workspace 비대화 | 전용 component/type/API module 분리 |

Open P0/P1/P2/P3: `0/0/0/0`.

## Fable 사용량

Claude `/usage` 정수 반올림 기준이다.

| 시점 | 전체 사용/잔여 | Fable 사용/잔여 |
| --- | --- | --- |
| 1차 기획 직전 | 16% / 84% | 31% / 69% |
| 1차 기획 직후 | 16% / 84% | 31% / 69% |
| 2차 기획 직전 | 16% / 84% | 32% / 68% |
| 2차 기획 직후 | 17% / 83% | 33% / 67% |

1차 기획은 568초, 2차 기획은 218초가 걸렸다.

## 운영 SOP — 실험 검수용

1. 이 branch를 isolated DB와 external provider disabled 상태에서 실행한다.
2. 자재 담당이 기존 008A 흐름으로 도착을 등록하고 IQC를 요청한다.
3. 품질 담당은 `IQC` 검사함에서 `신규 성적서` 건을 열어 검사를 시작한다.
4. 필수 항목을 판정하고 외함 전체를 다시 확인할 수 있는 JPEG/PNG 사진과 설명을 등록한다.
5. 최종확인에서 누락 0건과 종합 사유를 확인한 뒤 합격 또는 부적합으로 확정한다.
6. 완료 성적서의 PDF 상태가 `Ready`면 저장한다. `Failed`면 판정은 유지한 채 PDF 재시도만 실행한다.
7. 충돌 시 최신 성적서를 다시 불러온다. Persistent DB 적용은 별도 backup·restore rehearsal과 승인을 거친다.

## User manual — 역할별 사용법

- 품질 담당 Desktop: `IQC` → 요청 카드 → `검사 시작` → 항목 판정 → `저장하고 사진 등록` → 사진 선택·설명 → 최종확인 → 판정 확정 → PDF 저장.
- 품질 담당 Mobile: 좌상단 메뉴 → `IQC` → 한 건 카드 → 전체 화면 성적서 → 검사항목·사진·최종확인 단계.
- 자재 담당: 기존 자재 입고에서 IQC 요청·재검사·입고 확정을 계속 사용하며 성적서 판정을 직접 우회하지 않는다.
- 읽기 역할: 완료 성적서·사진·PDF만 프로젝트 접근 범위 안에서 조회한다.
- Legacy: 기존 요청·판정 건은 `LEGACY` 안내와 기존 사유만 확인하며 빈 상세 성적서를 만들지 않는다.

## Rollback·forward-fix

- local code는 이 experiment commit의 후속 commit으로 보정할 수 있으며 main에는 반영되지 않는다.
- Persistent DB에 `0032`을 적용한 뒤 destructive down rollback은 하지 않는다. write를 중단하고 backup 기반 isolated 복구를 검증한 뒤 additive forward-fix migration을 작성한다.
- Finalized snapshot·사진·PDF artifact는 직접 수정·삭제하지 않는다. renderer 보정이 필요하면 기존 byte를 교체하지 않는 별도 version/reissue 신규 기능으로 계획한다.

## 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | 이 문서 `운영 SOP — 실험 검수용` | 실험 검수용 완료, 운영 handover 미승인 |
| User manual | 이 문서 `User manual — 역할별 사용법` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` TASK-009A section | 실험 구현·검수 대기 기록, canonical queue 불변 |
| User validation checklist | [009a-user-validation-checklist.md](009a-user-validation-checklist.md) | 자동 검증 완료·사용자 검수 대기 |

## 남은 항목

- 사용자 screenshot·실제 action 검수
- push·PR·merge, Persistent UAT와 실제 provider는 미승인·미실행
- canonical Roadmap 다음 Gate는 계속 `TASK-007A` Fable deep-interview
