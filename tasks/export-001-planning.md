# TASK-EXPORT-001 — 모든 페이지 Excel 출력 공통 기능 기획안

> 상태: Draft
> 작성 단계: Codex 내용 review 전 Fable 1차 기획
> 목적: 공통 Excel export 계약과 우선 화면 vertical slice의 구현 방향 확정

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/export-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다. 이 문서는 `experiment/*` fast-track의 1차 기획이며, Codex 내용 review 뒤 승인된 별도 target의 2차 기획이 최종 구현 계약이 된다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 주요 조회 화면의 데이터를 Excel로 전달하려면 수작업 복사 또는 입력용 양식 오용이 필요하고, 현재 조회 조건·권한·민감 필드 규칙이 파일에 동일하게 적용된다는 공통 보장이 없다.
- 대상 사용자·역할: 조회 권한이 있는 업무 역할 전체, 조회 전용 역할, System Administrator. 역할별 신규 변경 권한은 없다.
- 정상 흐름: 화면 진입 → 검색·filter·sort 적용 → export action → Backend가 scope·필터·컬럼을 재검증 → bounded workbook 생성 → 안전한 파일명으로 다운로드 → 완료 feedback.
- 예외·복구 흐름: 미지원 화면·필터, 권한 없음, row 제한 초과, 잘못된 날짜 범위, 요청 취소를 안정적인 한글 오류로 반환한다. 생성 실패 시 부분 파일을 제공하지 않고, 브라우저 다운로드 실패를 성공으로 기록하지 않는다.
- 확정한 정책과 명시적 제외: Backend authoritative, 현재 조회 조건 반영, allowlisted 컬럼, 민감정보·내부 ID·secret 배제, formula injection 방어, read-only, import 계약 불변. import 변경·CSV·PDF·ZIP·batch·외부 발송·Persistent UAT·게시는 제외.
- planning으로 넘긴 비차단 미결정 사항: 첫 화면 조합, 신규 permission 필요성, 제한된 컬럼 선택, row/concurrency 제한, audit persistence — 5건. standing rule에 따라 아래 권장안을 자동 채택 대상으로 제시한다.

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

허용된 조회 화면에서 사용자가 현재 검색·필터와 자신의 조회 권한 범위를 그대로 반영한 안전한 `.xlsx` 파일을 한 번의 action으로 내려받을 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 화면 표를 브라우저에서 복사하거나 입력용 template 파일을 오용해 조회 결과를 보고서로 옮긴다.
- 이 과정에서 날짜·상태 표기 불일치, 컬럼 누락, 권한 밖 데이터 혼입, 파일 형식 오류가 반복된다.
- 화면별 임시 export를 개별 구현하면 필터 반영·민감 필드 제외·formula 방어·파일명 규칙이 화면마다 달라져 보안·운영 위험이 커진다.
- Roadmap 19장은 "모든 주요 페이지 Excel 출력 + 현재 조회 조건 반영 + 공통 export 구조"를 요구하지만, 현재 Repository의 Excel 코드는 전부 입력용(template 생성·preview/apply parser)이며 조회 결과 export 엔드포인트는 0건이다(Backend `export` 검색 결과는 ProductionPlanning의 무관한 언급 1건뿐).

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 업무 역할(영업·생산관리·구매·자재·제조·품질·물류) | 담당 화면의 현재 조회 조건 export | 해당 화면 list API와 동일한 read permission + project scope | 없음 (read-only) |
| 조회 전용 역할 | 허용된 조회 결과 export | 기존 read scope 이하, 민감 컬럼은 별도 permission 없으면 제외 | 없음 |
| System Administrator | 동일 export (업무 화면 기준) | 기존 read 정책. 업무 mutation 우회 없음 | 없음 |

권장 권한 모델(비차단 결정 2): 신규 `data.export` permission을 만들지 않고 각 화면 list endpoint와 동일한 permission·scope 검사를 export endpoint에 그대로 적용한다. 근거: 권한 원칙(서버 Policy 강제, 최소 권한)과 기존 구조 — 프로젝트 목록은 `projects.read` + `GetProjectAccessScope`, 민감 컬럼은 `Project.SalesAmount.Read` 같은 별도 read permission으로 이미 분리되어 있어 이중 정책을 만들 이유가 없다. export는 "화면에서 볼 수 있는 것 이하"만 내려받는 기능이므로 화면 read 권한이 곧 export 권한이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 프로젝트 목록 export

1. 사용자가 프로젝트 목록에서 검색어·상태·납기일 범위를 적용한다.
2. filter/action 영역의 `Excel 다운로드`를 누르면 현재 조건 요약과 함께 생성이 시작되고 버튼은 중복 실행이 차단된다.
3. Backend가 같은 list query 경로로 권한·scope·필터를 재검증해 workbook을 생성하고, 사용자는 `EMI_프로젝트_<일시>.xlsx`를 내려받는다. 매출액 컬럼은 `Project.SalesAmount.Read` 보유자에게만 포함된다.

### 시나리오 B — 권한·제한 예외

1. 조회 전용 사용자가 구매 dashboard에서 export를 실행하면 화면과 같은 데이터 범위의 파일만 생성된다.
2. 결과가 row 제한을 초과하면 파일 대신 "조회 조건을 좁혀 다시 시도해 주세요" 한글 안내를 action 근처에 표시한다.
3. 생성 중 서버 오류가 나면 부분 파일 없이 실패 feedback만 표시하고, 조건을 유지한 채 재시도할 수 있다.

### 시나리오 C — 내 업무 export

1. 사용자가 내 업무 목록에서 상태 필터를 적용하고 export를 실행한다.
2. Backend는 인증 사용자 본인 기준의 work item만 조회해 workbook을 생성한다. 다른 사용자의 업무는 어떤 필터로도 포함되지 않는다.

## 5. 기능 요구사항

### 필수

- [ ] 공통 export 기반: workbook 생성(제목·생성일시·적용 필터 요약·header·형식화된 값), 파일명 규칙, `.xlsx` content type 응답, formula injection 방어, row 제한, 취소 전파
- [ ] 화면별 adapter: allowlisted 컬럼 정의, 기존 list query parser·store 조회 재사용, list endpoint와 동일한 권한·scope
- [ ] 우선 화면 3개 vertical slice: 프로젝트 목록, 구매 dashboard, 내 업무 (권장안, 비차단 결정 1)
- [ ] Frontend 공통 export action: 진행 중 중복 차단, 성공·실패·0건·제한 초과 feedback, desktop·390px 적응형 배치
- [ ] 권한 거부·필터 오류·제한 초과의 안정적 한글 오류 응답
- [ ] export 실행의 privacy-safe 서버 로그(actor id, 화면 유형, row count, bounded filter projection)

### 선택

- [ ] 0건일 때 header-only 파일 제공 대신 사전 안내 (v1은 header-only 파일 허용)
- [ ] per-instance 동시 export 제한(포화 시 한글 busy 안내)

### 명시적 제외

- [ ] Excel import 계약 변경, CSV·PDF·ZIP·정기 batch·이메일/Teams 발송
- [ ] 사용자 정의 수식·pivot·chart·macro·template designer·컬럼 선택 UI
- [ ] export 파일 영구 저장, 외부 storage·회계·BI·Graph 파일 연동
- [ ] 우선 3개 외 화면 adapter (자재·제조·품질·물류·정산·알림·Pending 등은 후속 adapter 추가로 확장)
- [ ] Persistent UAT·실제 업무 데이터 export, 대표 repo·`main`·push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 프로젝트 목록 | 기존 프로젝트 메뉴 | filter/action 영역의 export 버튼과 현재 조건·row 제한 안내 | Excel 다운로드 | 생성 중 spinner·중복 차단, 완료·오류·제한 초과를 버튼 근처에 표시 |
| 구매 dashboard | 기존 구매 메뉴 | 검색어·입고예정일 범위 요약과 export 버튼 | Excel 다운로드 | 동일 공통 feedback |
| 내 업무 | 기존 내 업무 메뉴 | 상태 필터 요약과 export 버튼 | Excel 다운로드 | 동일 공통 feedback |
| 모바일(390px) | 동일 URL | PC action bar 축소가 아니라 기존 `MobileSheet` 패턴의 compact export sheet: 현재 범위 요약 + 실행 + 완료 상태 | export 실행 | `aria-live` 안내, page-level overflow 0 |

확인할 UX 항목: 현재 어떤 조건·컬럼이 파일에 들어가는지 사용자가 실행 전에 알 수 있는가, 진행·완료·실패가 action 근처에 보이는가, 권한 없는 사용자는 버튼이 숨김이어도 서버가 차단하는가, keyboard/focus 접근이 가능한가.

## 7. 업무 규칙과 불변조건

- export 결과 row 집합은 같은 사용자·같은 필터의 화면 list 조회 결과와 동일해야 한다(페이지 나눔 없이 row 제한 이내 전체). 별도 SQL을 새로 쓰지 않고 같은 store 조회 경로를 재사용해 이 동등성을 구조로 보장한다.
- Backend가 permission·scope·필터·컬럼의 authoritative layer다. Frontend 버튼 숨김은 보조 수단이다.
- 민감 컬럼(예: 매출액, 제조 작업시간)은 해당 read permission(`Project.SalesAmount.Read`, `Manufacturing.WorkTime.Read`) 보유자에게만 포함되고, 없으면 컬럼 자체를 제외한다(빈 값이 아니라 미포함).
- 내부 GUID·raw enum·secret·개인 식별 원문(이메일·사번 등)은 컬럼 allowlist에 넣지 않는다. 사용자 표시는 화면과 같은 표시명·표시 코드만 사용한다.
- 모든 텍스트 셀은 formula-safe로 기록한다. `=`, `+`, `-`, `@`, tab/CR로 시작하는 값이 수식으로 해석되지 않도록 셀을 명시적 text 타입으로 강제하고, 사용 중인 ClosedXML 버전의 leading-`=` 동작을 테스트로 고정한다.
- export는 domain write 0, 알림 0, workflow 상태 전이 0이다. 중복 실행해도 업무 데이터가 변하지 않는다.
- row 제한 초과 시 부분 파일을 반환하지 않는다. 실패한 생성은 성공 로그를 남기지 않는다.
- 날짜는 `yyyy-mm-dd` DateFormat, 상태는 화면과 같은 한글 label로 기록해 화면과 파일의 의미가 같아야 한다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| Export definition (화면별 adapter) | 화면 유형, 컬럼 allowlist, 필터 계약, 권한 정책의 코드 정의 | 신규 (코드, DB 아님) | 코드 리뷰로 추적 |
| Export 요청 | HTTP GET 1회의 일시적 요청. 영속 상태 없음 | 신규 (비영속) | 서버 로그의 privacy-safe projection |
| Workbook 응답 | bounded `.xlsx` byte 응답 | 신규 (비영속) | 저장하지 않음 |
| 조회 데이터 | 프로젝트·구매품목·work item 등 기존 read model | 기존 | 변경 없음 (read-only) |

```text
요청 수신 → 권한·scope·필터 재검증 → (실패: 한글 오류 반환) → bounded 조회 → (row 초과: 안내 반환) → workbook 생성 → 파일 응답 + 로그
```

상태 전이·migration·신규 테이블은 없다(권장안, 비차단 결정 5). 영속 export audit가 필요해지면 별도 additive migration Task로 분리한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 화면과 동일한 permission·scope·필터 재검증, 컬럼 allowlist, row 제한, formula-safe 직렬화.
- 필요한 조회와 mutation: mutation 없음. 신규 GET endpoint 3개 — 프로젝트 목록 export(`/api/projects` 계열), 구매 dashboard export(`/api/procurement/dashboard` 계열), 내 업무 export(`/api/my-work` 계열). 각 endpoint는 해당 list endpoint와 같은 query parser(`ParseProjectListQuery` 등)·store 조회·scope helper(`GetProjectAccessScope`, `CanReadSalesAmount`)를 재사용하고 paging 대신 row 제한(cap+1 조회로 초과 감지)을 적용한다.
- 공통 기반: 신규 `ExcelExport` 모듈(공통 workbook builder + 화면별 definition). 기존 `CalendarHolidayExcelParser.CreateTemplate`의 ClosedXML 스타일 관례(제목 merge, bold header, FreezeRows, AutoFilter, AdjustToContents)와 `Results.File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName)` 응답 패턴을 재사용한다.
- 권한·validation: list endpoint와 동일한 `.RequireAuthorization`/명시적 permission 검사. 미지원 필터·잘못된 날짜 범위는 기존 `Results.ValidationProblem` 관례의 한글 메시지.
- transaction·동시성·idempotency: read-only 단일 조회이므로 transaction 불요. `CancellationToken` 전파로 취소를 지원하고, per-instance 동시 export 제한(선택 요구사항)은 포화 시 파일 없이 busy 안내를 반환한다.
- audit trail: 신규 테이블 없이 구조적 서버 로그(actor user id, 화면 유형 enum, row count, filter 사용 여부 projection)만 남긴다. payload·파일 원문은 기록하지 않는다.
- 외부 provider 영향: 없음. 발송 0.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태를 확정하지 않으며, 위 이름은 제안이다.

## 10. Frontend 고려사항

- route/component: 신규 route 없음. 공통 export hook/버튼 component를 만들어 3개 화면의 filter/action 영역에 배치한다. `frontend/src/api.ts`의 기존 blob 다운로드 패턴(`fetchWithAuth` → blob → objectURL → anchor.download, Content-Disposition 파일명)을 재사용한다.
- loading/empty/error/success: 생성 중 버튼 비활성·spinner, 0건 안내(또는 header-only 파일), 권한 거부·row 제한·서버 오류·다운로드 완료를 구분해 action 근처에 표시한다.
- 공통 Action Feedback: 기존 화면들의 action feedback 관례를 따르고 화면별 상이한 임시 UI를 만들지 않는다.
- 접근성: 버튼 label·`aria-live` 상태 안내·keyboard focus 유지.
- 390px/mobile/narrow pane: PC 표 축소 금지. 기존 `MobileSheet`·adaptive-layout 패턴으로 export 범위 요약과 실행·완료 feedback을 재배치하고 page-level overflow 0을 유지한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 프로젝트 목록·내 업무 조회 계약을 read-only로 재사용. 알림 생성 없음.
- 권한/관리자: `QmsPermissions`/`QmsPolicies`와 scope helper 재사용. 신규 permission·role 없음.
- Excel/PDF/첨부: 기존 import(template/preview/apply) 계약은 불변. 공통 export는 별도 모듈로 추가되며 제거 시 기존 조회·import가 그대로 동작한다(rollback 경계).
- Teams/Mail: 영향 없음.
- 삭제·복구/감사: 삭제 화면·admin 이력 export는 범위 밖. export 실행 로그만 추가.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 공통 workbook builder + 화면별 definition, 화면 read 권한 재사용, bounded 동기 생성, 고정 allowlist 컬럼, 로그 기반 audit, 우선 3화면 | 최소 권한과 화면-파일 동등성을 구조로 보장, migration 0, 후속 화면은 definition 추가만으로 확장 | 초대형 조회는 제한에 걸림(안내로 완화), 화면별 definition 유지 비용 |
| B | 화면별 개별 export 구현 | 화면당 초기 구현 단순 | 필터·민감 필드·formula·파일명 규칙이 흩어짐 — interview가 지적한 root 문제 재생산 |
| C | 비동기 job + 파일 storage + 이력 테이블 | 대용량·재다운로드 지원 | migration·storage·정리 정책 등 범위 과대, 현재 요구(현재 조건 즉시 다운로드) 대비 과설계 |

권장안은 A다. 대용량 요구가 실제로 확인되면 C를 별도 Task로 분리한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic runtime·DB만 사용한다.
- migration 필요 여부: 없음 (audit 로그 기반 권장안 채택 시). audit 테이블이 필요해지면 별도 승인·additive migration으로 분리한다.
- 외부 발송/실제 데이터 영향: 없음. 실제 사용자·고객·프로젝트 원문으로 export를 실행하지 않는다.
- runtime 교체 여부: 없음. canonical runtime·5174 handover 불포함.
- 추가 사용자 승인 필요 작업: push·PR·merge(미승인, main merge 0/3), Persistent UAT 반영, 우선 3화면 외 확장 화면 적용.

## 14. 검증 계획

- 최소 테스트(Backend): 화면별 export의 권한 거부(permission 없음·scope 밖 프로젝트), 필터 동등성(같은 필터의 list와 export row 일치), 민감 컬럼 포함/제외(`Project.SalesAmount.Read` 유무), row 제한 초과 안내, formula-safe(선행 `=`/`+`/`@` 값이 text로 저장됨을 workbook 재파싱으로 확인), content type·파일명, 0건 처리, 잘못된 날짜 범위 422.
- 최소 테스트(Frontend): export hook/버튼의 loading·중복 차단·성공·실패 상태 unit test, lint·typecheck·build.
- 영향 영역 회귀: 기존 import(template/preview/apply)와 3개 화면 list 조회 회귀, isolated Full-Stack E2E에서 다운로드 파일 검사 1개 이상.
- PR/CI: 이번 Task는 local experiment commit까지. push·PR 없음.
- 사용자 검수: 3개 화면의 desktop·390px synthetic screenshot을 보고하되 사용자 검수 완료로 표시하지 않는다(`사용자 검수 대기`).

## 15. 완료 기준

- 기능/권한/데이터: 3개 화면에서 현재 필터·scope 기준 `.xlsx` 다운로드, 서버 read permission·scope 강제, allowlisted 컬럼, domain write 0, raw PII/secret/내부 GUID 0.
- UX: desktop·390px에서 export action·feedback 동작, page-level overflow 0.
- 자동 테스트: Backend build·상기 테스트, Frontend lint·typecheck·unit·build, isolated E2E 다운로드 검사 통과.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update·user validation checklist 상태·위치 추적.
- 사용자 검수 상태: `사용자 검수 대기`로 종료.
- PR 상태: 없음(local experiment commit만).

중단 조건: 권한·scope 우회, 민감 필드·내부 식별자 노출, formula injection 미방어, unbounded 자원 사용, import 계약 회귀 또는 Repository 계약 충돌이 발견되면 fast-track을 중단하고 blocking으로 보고한다.

## 16. 미결정 사항

standing rule에 따라 아래 권장안은 2차 기획에서 자동 채택 대상이며, 각 항목은 사용자 결정 기록 대상으로 남긴다.

| 번호 | 질문 | 선택지 | 권장안과 근거 | 사용자 결정 |
| ---: | --- | --- | --- | --- |
| 1 | 첫 구현 화면 조합 | 1~4개 화면 | 프로젝트 목록(프로젝트 단위·민감 컬럼 gate) + 구매 dashboard(구매품목 단위·날짜 범위 필터) + 내 업무(사용자 scope) 3개 — 서로 다른 데이터 형태로 공통성을 증명하는 최소 조합 | 권장안 자동 채택 예정 |
| 2 | 권한 모델 | 신규 `data.export` vs 화면 read 재사용 | 화면 list endpoint와 동일한 permission·scope 재사용, 신규 permission 없음 | 권장안 자동 채택 예정 |
| 3 | 컬럼 선택 | 고정 allowlist vs 제한된 선택 | v1은 서버 고정 allowlist. 제한된 컬럼 선택 UI는 Deferred | 권장안 자동 채택 예정 |
| 4 | 대용량·자원 제한 | 무제한 동기 / bounded 동기 / 비동기 job | bounded 동기: row 제한(권장 10,000, 구현 시 상수로 확정), 취소 전파, 선택적 per-instance 동시 실행 제한. 초과 시 조건 축소 안내 | 권장안 자동 채택 예정 |
| 5 | 감사 수준 | 없음 / 로그 / DB 영속 | privacy-safe 서버 로그(actor·화면·row count·filter projection). DB 영속 audit와 migration은 Deferred | 권장안 자동 채택 예정 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 신규 `ExcelExport` 공통 모듈, Projects·Procurement·Workflow(내 업무) endpoint 확장에 export GET 추가.
- Frontend: `api.ts` export 함수, 공통 export 버튼/hook component, 3개 화면 action 영역과 mobile sheet 배치.
- DB/Migration: 없음.
- Tests/Scripts: Backend export 테스트, Frontend unit, isolated E2E 다운로드 시나리오.
- Docs: Roadmap 실험 상태 갱신, 5종 산출물.

## 18. Roadmap 연결

- 선행 Task: 실험 계보 `TASK-014A`까지의 화면·데이터 안정화(충족). canonical 큐의 `TASK-EXPORT-001`은 4.3 `Deferred`이며, 2026-07-18 실험 재정렬 승인으로 현재 실험 계보에서만 진행한다.
- 후속 Task: 나머지 후보 화면(자재·제조·검사·물류·정산·알림·Pending 등) adapter 확장, 필요 시 영속 export audit·비동기 대용량 export, 제한된 컬럼 선택 UI.
- 현재 Go/No-Go: 실험 branch 한정 Go. canonical 큐·`TASK-007A` Next Gate·대표 repo·`main`·Persistent UAT·provider는 불변.
- 별도 Task로 분리할 항목: audit persistence migration, 화면 전체 롤아웃, CSV/PDF 등 다른 형식.

## 19. Codex 구현 지시문 초안

1. 공통 `ExcelExport` 모듈을 추가한다: workbook builder(제목·생성일시·필터 요약·header·date/number/status 형식·FreezeRows·AutoFilter), formula-safe text 강제와 ClosedXML 버전 동작 고정 테스트, row 제한, 파일명 규칙, `.xlsx` 파일 응답 helper.
2. 프로젝트 목록·구매 dashboard·내 업무 3개 export GET endpoint를 각 화면 list endpoint와 같은 파일에 추가하고, 같은 query parser·store 조회·permission·scope helper를 재사용한다. 새 SQL 경로를 만들지 않는다.
3. 민감 컬럼은 기존 민감 read permission 보유 시에만 definition에 포함한다. 내부 GUID·raw enum·개인 원문은 컬럼에 넣지 않는다.
4. Frontend에 공통 export hook/버튼을 추가해 3개 화면 desktop action 영역과 390px mobile sheet에 배치하고, loading·중복 차단·성공·실패·제한 초과 feedback을 구현한다.
5. 14장의 Backend·Frontend·isolated E2E 검증을 실행하고, 화면별 desktop·390px screenshot을 privacy-safe하게 수집한다.
6. import 계약·기존 조회·migration ledger를 변경하지 않았음을 diff로 확인하고, 5종 산출물과 Roadmap 실험 상태를 갱신한 뒤 local experiment commit까지만 수행한다.

## 20. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-18 | experiment fast-track standing rule로 interview 생략·권장안 자동 채택·`TASK-EXPORT-001` 진행 지시 | 이 1차 기획 작성. 비차단 5건은 16장 권장안으로 제시 |

## 21. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

이 문서는 Fable 1차 기획 draft이며 구현 승인이 아니다. Codex 내용 review와 승인된 2차 기획이 뒤따른다.

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 5
