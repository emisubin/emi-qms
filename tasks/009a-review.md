# TASK-009A — IQC 디지털 검사성적서·필수 사진·PDF Snapshot 1차 기획 Codex 내용 Review

> Review 대상: `tasks/009a-planning.md` Fable 5 원문
> Review 성격: 사용자 문제·제품 방향·Roadmap·실제 Repository·구현 경계 1회 검토
> 결과: 2차 Fable 기획 전 필수 보정 — 아래 resolution을 최종 구현 계약에 반영

## 1. 총평

versioned 최소 체크리스트, 모바일 Draft/Finalized 흐름, 필수 외함 사진, 최종화 시점 불변 snapshot과 저장된 PDF라는 큰 방향은 유지한다. 단순 합격/부적합 사유만 남기는 TASK-008A를 실제 검사 증빙으로 확장하면서도 LQC/OQC·관리자 template 편집·실제 provider를 후속 Task에 남긴 경계도 적절하다.

다만 1차 기획대로 바로 구현하면 네 가지 핵심 문제가 남는다. 첫째, `ProjectRead` permission만 확인하면 프로젝트 접근 scope를 우회할 수 있다. 둘째, 기능 도입 시점에 이미 `Requested`인 legacy attempt까지 기존 `/result`를 막으면 처리 불가능한 자재가 생긴다. 셋째, GET get-or-create는 read 요청에서 DB를 변경한다. 넷째, 판정 transaction과 PDF renderer 실패의 경계가 모호해 “판정은 고정됐지만 PDF는 아직 없음” 상태를 안전하게 표현·복구하기 어렵다. 이 항목을 2차 기획에서 구체적인 계약으로 고치면 실험 branch의 vertical slice로 구현 가능하다.

## 2. 기능 판단

### 유지

- versioned system template v1과 attempt별 1개 성적서
- `Draft → Finalized` 최소 lifecycle, optimistic version lock과 완료 후 append-only
- isolated experiment에서 사진을 PostgreSQL `bytea`로 보존하는 선택
- 외함 사진 필수, magic-byte 기반 JPEG/PNG 검증과 hash 기록
- 판정·snapshot·기존 008A 상태·Pending·work item을 하나의 transaction에서 처리
- 이미 판정된 legacy attempt를 거짓 성적서로 backfill하지 않는 표시 정책
- snapshot 기반 저장 PDF와 authorized download
- `/quality/iqc` 안의 모바일 우선 `항목 → 사진 → 검토·최종화` 흐름
- LQC/OQC·template 관리 UI·실제 고객 양식·object storage·Persistent UAT 제외

### 추가

1. **프로젝트 접근 scope를 포함한 서버 권한**
   - `ProjectRead` permission 존재만으로는 충분하지 않다. attempt → receipt → procurement item → project를 해석하고, `Project.Read.All` 또는 해당 프로젝트 배정 scope를 함께 검증한다.
   - 성적서·사진·PDF의 모든 조회와 다운로드에 같은 helper를 적용한다. `QualityInspect` mutation도 프로젝트 scope를 추가로 통과해야 한다.
   - queue projection은 사용자가 접근 가능한 프로젝트만 반환하고, ID 추측으로 403/404 차이를 이용한 존재 여부 노출을 만들지 않는다.

2. **도입 시점 legacy Requested attempt 호환 모드**
   - migration `0032`에서 기존 모든 attempt를 구분한다. migration 이전에 존재하던 `Requested` attempt는 `Legacy`, 이후 새로 생성되는 attempt는 `Detailed` 판정 mode를 가진다.
   - 기존 `/result`는 `Legacy + Requested`에서만 계속 동작한다. `Detailed`에서는 409 또는 안정적인 validation error로 성적서 최종화를 안내한다.
   - 이미 판정된 attempt는 기존 사유와 `legacy 최소 판정`만 표시하고 상세 성적서를 생성하지 않는다. 재검사는 신규 attempt이므로 `Detailed`를 사용한다.
   - 시간을 기준으로 추정하지 말고 DB에 명시적인 mode를 저장해 재현 가능하게 한다.

3. **명시적이고 idempotent한 Draft 생성**
   - GET은 DB를 변경하지 않는다. GET은 기존 성적서 또는 현재 template preview만 반환한다.
   - `POST /api/quality/iqc/{attemptId}/reports`가 Draft를 idempotent하게 초기화한다. 동시 생성은 unique attempt key로 같은 성적서를 반환한다.
   - PUT/PATCH response 저장, 사진 POST/DELETE, finalize POST는 모두 `expectedReportVersion`을 사용한다.

4. **판정과 PDF 생성의 실패 경계**
   - 같은 DB transaction에는 report Finalized, canonical snapshot, snapshot hash, attempt/receipt 판정, Pending, work item과 event만 포함한다.
   - PDF binary 렌더링은 commit 뒤 수행한다. renderer 실패가 확정 판정을 rollback하거나 성적서를 다시 Draft로 만들지 않는다.
   - report는 `pdfStatus=Pending|Ready|Failed`, 안전한 일반 오류 코드와 마지막 시도 시각을 가진다. snapshot hash별 artifact는 최대 1개이며 retry는 idempotent하다.
   - 첫 성공 뒤에는 저장된 동일 byte만 제공한다. 다운로드 요청은 Pending/Failed를 명확히 구분하고 서버가 bounded retry할 수 있지만 사용자 요청 하나가 무한 재시도를 만들면 안 된다.

5. **사진 저장·용량·개인정보 guard**
   - 운영 부담을 고려해 파일당 최대 5MB, report당 최대 5장, report 전체 최대 15MB로 줄인다. 필수 외함 사진 1장 이상을 포함한다.
   - client MIME과 파일 확장자를 신뢰하지 않고 magic-byte에서 normalized MIME을 정한다. 원본 경로·raw filename은 저장하지 않거나 안전한 display name으로 정규화한다.
   - 이미지 byte, size, normalized MIME, SHA-256, item 연결, bounded 대체 설명만 snapshot에 포함한다. Draft에서만 삭제 가능하며 Finalized 뒤 binary도 불변이다.

6. **PDF의 증빙 가치와 결정성**
   - PDF에는 필수 외함 사진을 실제로 포함한다. 사진 hash 목록만 표시하는 fallback은 사용자에게 검사 증빙으로 충분하지 않으므로 제거한다.
   - PDFsharp `6.2.4`를 명시적으로 사용하고, Core build의 custom font resolver로 repository 동봉 OFL 한글 font를 로드한다. font binary·license·source provenance·SHA-256을 Repository에 기록한다.
   - PDF metadata에 렌더링 현재 시각을 넣지 않는다. snapshot의 확정 시각만 사용하고, 항목·사진 순서를 고정한다.
   - font의 라이선스와 한글 렌더링을 확인하지 못하면 임의 system font fallback 없이 구현을 중단한다.

7. **canonical snapshot과 schema validation**
   - JSONB의 DB 표현에 hash를 맡기지 않는다. 서버가 고정 property/array 순서·UTF-8·null 처리로 canonical JSON text를 만들고 그 exact byte의 SHA-256을 저장한다.
   - template item 유형은 이번 범위에서 `Check`와 `Text`만 허용한다. `Check`는 `Pass|Fail|NotApplicable`, `NotApplicable`은 비고 필수다. `Text`의 필수·최대 길이를 DB와 서버에서 함께 제한한다.
   - template·version·item seed는 system-owned이며 이번 Task에서 편집 API를 제공하지 않는다.

8. **다운로드 안전 계약**
   - 사진/PDF content endpoint는 매 요청마다 authorization을 다시 수행하고 `private, no-store` cache 정책을 사용한다.
   - `Content-Disposition` 파일명은 합성된 안전한 이름을 사용하며 프로젝트명·사용자 입력·raw 식별자를 직접 넣지 않는다.
   - raw DB error, binary hash 전체와 내부 storage metadata를 client 오류 응답에 노출하지 않는다.

9. **Frontend 모듈 경계**
   - 이미 큰 `MaterialsWorkspace.tsx`에 전체 편집기를 추가하지 않는다. `IqcReportWorkspace.tsx`와 관련 type/API helper로 분리하고 기존 `MaterialIqcPage`는 queue·선택 orchestration만 담당한다.
   - 모바일은 한 화면의 핵심 행동 하나를 기준으로 항목·사진·검토를 나누고, 완료 성적서는 읽기 전용 compact summary로 제공한다. Desktop도 같은 기능을 공유하되 단순 모바일 확대판이 되지 않게 한다.

### 보류

- object storage·CDN·virus scanner와 image transcoding pipeline. 실험에서는 제한된 JPEG/PNG 원본을 DB에 저장한다.
- 사용자·고객별 template 편집, 실제 검사 항목 master와 승인 workflow.
- LQC/OQC·전진검수·FAT, 고객 양식·전자서명·Excel 내보내기.
- PDF 재발행 이력·관리자 강제 재생성·batch regeneration.
- Persistent UAT migration·runtime handover와 실제 고객 데이터 검수.

### 제거

- GET get-or-create에 의한 read-side mutation.
- `ProjectRead` permission 존재만 확인하는 무범위 조회.
- migration 시점 기존 `Requested` attempt까지 기존 `/result`를 일괄 차단하는 방식.
- DB transaction 안에서 PDF 렌더링까지 완료하려는 방식.
- 필수 외함 사진 대신 PDF에 hash 목록만 남기는 fallback.
- 파일당 8MB × report당 10장으로 최대 80MB를 허용하는 초기 한도.
- OS에 설치된 system font fallback과 렌더링 시각에 따라 바뀌는 PDF metadata.

## 3. 권장 상태·전이 계약

```text
판정 mode:
  migration 이전 기존 attempt = Legacy
  migration 이후 신규/재검사 attempt = Detailed

Detailed report:
  (없음) --POST initialize--> Draft --POST finalize--> Finalized
                                 │                      ├─ PDF Pending
                                 │                      ├─ PDF Ready
                                 │                      └─ PDF Failed --bounded retry--> Ready
                                 └─ PUT response / POST·DELETE photo

attempt:
  Requested --Detailed finalize transaction--> Passed / Failed
  Requested --Legacy /result-----------------> Passed / Failed
```

- Finalized와 attempt 판정은 같은 transaction이므로 한쪽만 성공할 수 없다.
- PDF status는 판정 상태를 되돌리지 않는 별도 derived artifact 상태다.
- Detailed attempt는 `/result`로 판정할 수 없고, Legacy attempt는 빈 detailed report를 만들지 않는다.
- 재검사는 새 attempt·새 Detailed report로 시작해 이전 report·사진·PDF를 그대로 보존한다.

## 4. Finding과 Resolution

| ID | Severity | 상태 | 원인·영향 | 2차 기획 Resolution |
| --- | --- | --- | --- | --- |
| `009A-READ-SCOPE` | P1 | `RESOLVED_FOR_REDRAFT` | permission만 확인하면 다른 프로젝트의 검사 증빙을 ID로 조회할 수 있음 | 모든 queue/report/photo/PDF read와 mutation에 project scope helper 적용 |
| `009A-FINALIZE-PDF-BOUNDARY` | P1 | `RESOLVED_FOR_REDRAFT` | PDF 실패가 판정 transaction과 섞이면 불일치·장시간 lock 또는 잘못된 rollback 발생 | 판정+snapshot atomic, PDF post-commit 상태·hash별 idempotent retry로 분리 |
| `009A-LEGACY-BYPASS` | P1 | `RESOLVED_FOR_REDRAFT` | 기존 Requested까지 `/result`를 막으면 처리 불가능한 자재 발생 | 명시적 Legacy/Detailed mode, 신규 attempt만 Detailed, Legacy endpoint 호환 유지 |
| `009A-GET-MUTATION` | P2 | `RESOLVED_FOR_REDRAFT` | GET get-or-create가 read-only 기대·cache·재시도 계약 위반 | idempotent POST initialize와 side-effect 없는 GET으로 분리 |
| `009A-PHOTO-BOUNDS` | P2 | `RESOLVED_FOR_REDRAFT` | 80MB/report 상한과 raw filename은 DB/PDF 부담·개인정보 위험 | 5MB/장·5장·총15MB, magic MIME, 안전한 display name과 bounded metadata |
| `009A-PDF-EVIDENCE` | P2 | `RESOLVED_FOR_REDRAFT` | 사진 hash만 있는 PDF는 필수 사진 증빙 가치가 낮음 | 필수 외함 사진을 PDF에 포함하고 저장 byte를 재사용 |
| `009A-FONT-DETERMINISM` | P2 | `RESOLVED_FOR_REDRAFT` | Linux/macOS Core build는 system font 의존 시 한글 실패·PDF drift 가능 | PDFsharp 6.2.4 + 동봉 OFL 한글 font + custom resolver + 고정 metadata |
| `009A-SNAPSHOT-CANONICAL` | P2 | `RESOLVED_FOR_REDRAFT` | 직렬화 순서가 바뀌면 동일 의미 snapshot의 hash가 달라짐 | 고정 순서 canonical UTF-8 JSON exact byte를 hash·저장 |
| `009A-DOWNLOAD-GUARD` | P2 | `RESOLVED_FOR_REDRAFT` | attachment endpoint가 scope/cache/filename을 놓치면 binary 증빙 노출 가능 | 요청별 scope auth, private/no-store, 합성 파일명, safe error 고정 |
| `009A-FRONTEND-BOUNDARY` | P3 | `RESOLVED_FOR_REDRAFT` | 대형 workspace 파일 확장은 회귀와 모바일 조정 비용 증가 | 전용 IQC report component/type/API module로 분리 |

Review 기준 Open P0/P1/P2는 `0/0/0`이다. 이는 2차 Fable 기획이 위 resolution을 반영한다는 조건부 판정이며 아직 코드 구현 완료 판정이 아니다.

## 5. 자동 채택할 비차단 결정

| 항목 | 채택안 | 근거 |
| --- | --- | --- |
| 기존 Requested 처리 | `Legacy` mode 유지 | 도입 시점에 진행 중인 자재를 막지 않고 거짓 상세 성적서를 만들지 않음 |
| 신규·재검사 처리 | `Detailed` mode | 외함 사진 필수 우회를 명확히 차단 |
| Draft 생성 | idempotent POST | GET side effect 제거와 동시 생성 안전성 |
| 사진 한도 | 5MB/장·5장·총15MB | 모바일 업로드와 DB/PDF 부담 사이의 실험 기준 |
| PDF 사진 | 필수 외함 사진 실제 포함 | hash-only보다 사람이 검수 가능한 증빙 제공 |
| PDF 실패 | 판정 유지 + 별도 status/retry | 업무 판정 원자성과 렌더러 가용성을 분리 |
| PDF 도구 | PDFsharp 6.2.4 + 동봉 OFL 한글 font | .NET 10 Core build·Unicode·Linux/macOS font resolver 경계가 명시됨 |
| 최종 파일 이름 | 합성 안전 이름 | 사용자 입력·고객 정보의 header 노출 방지 |

## 6. 2차 기획이 고정할 최소 구현 계약

1. additive `0032` migration은 template/version/item, report/response/photo/PDF artifact와 attempt 판정 mode를 추가한다. 기존 migration은 수정하지 않는다.
2. 기존 attempt는 Legacy, 이후 신규·재검사 attempt는 Detailed다. `/result`는 Legacy에서만 유지되고 Detailed는 report 최종화만 허용한다.
3. GET은 조회만 하며, Draft는 idempotent POST로 생성한다. report·photo·finalize mutation은 version과 server validation을 사용한다.
4. Finalized snapshot과 008A 판정 transaction은 원자적이다. PDF는 post-commit `Pending|Ready|Failed` artifact로 분리하고 hash별 bounded retry를 지원한다.
5. report/photo/PDF의 모든 read·download·mutation은 permission과 project scope를 함께 검증한다.
6. 사진은 JPEG/PNG magic-byte, 파일당 5MB·최대 5장·총15MB를 적용하고 필수 외함 사진을 저장 PDF에 포함한다.
7. canonical snapshot, PDFsharp 6.2.4, repository 동봉 OFL 한글 font와 결정적 renderer를 검증한다. provenance나 한글 렌더링이 실패하면 구현을 중단한다.
8. Frontend는 전용 report workspace로 분리하고 desktop·390px 모바일·Teams narrow에서 작성·완료·legacy·PDF 상태를 제공한다.
9. migration fresh/existing/legacy, 권한 matrix, GET 무변경, 동시 Draft/최종화, upload guard, PDF 실패/retry/동일 byte, 기존 008A 회귀와 isolated full-stack flow를 검증한다.

## 7. 권장 구현 순서

1. `0032` schema·Legacy/Detailed migration·seed·constraint와 migration test
2. project scope helper·report contract/store·canonical snapshot
3. Draft·response·photo·finalize API와 기존 008A transaction 통합
4. PDFsharp·OFL 한글 font feasibility proof, deterministic renderer와 artifact retry
5. 기존 queue projection·Legacy `/result` 회귀·download guard
6. 전용 frontend report workspace·모바일 단계 UI·desktop detail
7. Backend targeted/전체, Frontend unit/build, isolated full-stack E2E
8. desktop·390px screenshot, implementation report와 독립 검증

## 8. 외부 근거 확인

- PDFsharp 6.2.4 NuGet package는 .NET 10을 포함한 target을 제공하며 MIT license다: <https://www.nuget.org/packages/PDFSharp>
- PDFsharp Core build는 Linux/macOS에서 custom font resolver와 font byte 제공이 필요하다: <https://docs.pdfsharp.net/PDFsharp/Topics/Fonts/Font-Resolving.html>
- Noto Sans CJK KR은 한국어/Hangul을 지원하며 Noto font는 OFL로 배포된다: <https://notofonts.github.io/noto-docs/specimen/NotoSansCJKkr/>, <https://notofonts.github.io/noto-docs/website/use/>

위 자료는 라이브러리·font 선택의 실현 가능성 근거일 뿐, Repository 동봉 binary의 실제 license·SHA-256·한글 출력 검증을 대신하지 않는다.

## 9. 판정

위 resolution을 반영한 Fable 2차 기획을 구현 source of truth로 사용하면 실험 branch 구현은 `GO`다. 이 판정은 대표 repo, push, PR, merge 또는 Persistent UAT 승인이 아니다.
