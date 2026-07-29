# TASK-QR-001 — 패널 QR 생성·스캔 랜딩 기획안 (Fable 1차)

> 상태: Draft
> 작성 단계: Codex 내용 review 전 Fable 1차 planning
> 목적: `experiment/*` 2-pass fast-track의 1차 기획으로, Codex review 뒤 Fable 2차 기획이 최종 구현 계약을 확정한다

- taskId: `TASK-QR-001`
- taskType: `NEW_FEATURE`
- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/qr-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/validation-matrix.md`, `docs/development/privacy-safe-evidence.md`를 따르며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 설계가 패널명을 입력하면 `QR 생성 가능` 표시만 생기고, 현장에 부착·스캔할 실제 QR과 스캔 후 진입 경로가 없다.
- 대상 사용자·역할: QR 발급·출력 사용자(설계 중심), 현장 스캔 사용자(제조·품질·물류), 조회 중심 사용자(영업·관리자·기타 부서).
- 정상 흐름: 발급 가능 패널 확인 → 발급 → QR preview/다운로드/인쇄 → 현장 스캔 → 인증 → 패널 identity와 역할 기반 landing → 담당 업무 진입.
- 예외·복구 흐름: 잘못된/폐기 token, 삭제 project, 비활성 panel, 접근 제한 사용자, 완료 project, 동시 발급, 재출력, 보안 사고 시 rotation.
- 확정한 정책과 명시적 제외: opaque token만 QR에 포함, 패널당 활성 QR 1개, 인증 후에만 데이터 노출, 역할별 landing, 조회는 전 부서·쓰기는 담당 권한, 스캔은 업무 상태를 바꾸지 않음. 실제 Entra tenant·운영 domain·외부 SaaS·라벨 프린터 연동·`main`·Persistent UAT·실제 provider는 제외.
- planning으로 넘긴 비차단 미결정 사항: 발급 시점·actor, token 수명·rotation, 출력 UX·형식, 미로그인 landing, 로그인 후 route, 현장 부착 상태 추적, 분실·오염 복구(§16의 7개). 이 branch의 standing instruction에 따라 Fable 권장안을 자동 채택한다.

Interview 문서에 없는 사용자 답변을 추측하지 않았다. Interview 완료는 이 planning이나 구현 승인이 아니다.

### 재검증한 Repository 기준선 (확인된 사실)

- Roadmap 8장은 QR 생성 가능 기준(프로젝트 Active·미삭제, 패널 Active, 패널명 존재), 현장 부착 기준(자재 Product Tag → IQC 적합 후 품질 부착), 패널당 1개·민감정보 금지·활성 유지 계약을 확정했다. Roadmap 실행 큐 4.4와 실험 완료 원장 4장 우선순위 1이 모두 QR 스캔 landing을 첫 미완료 제품 Task로 지정한다.
- `qrEligible`은 저장값이 아니라 파생값이다: `backend/src/Emi.Qms.Api/PanelInformation/PanelInformationDomain.cs`의 `IsQrEligible`이 단일 판정 함수이고, 목록/상세/집계(`PanelInformationStore.cs`, `Projects/ProjectStore.cs`)와 backend tests(`PanelInformationApiTests.cs`, `ProjectRegistrationApiTests.cs`)가 이를 소비한다. 실제 QR record·token·이미지·landing은 어디에도 없다.
- Frontend는 `frontend/src/App.tsx`의 `initialViewFromLocation()`이 `window.location.pathname`을 view로 매핑하는 단일 shell이다. `/projects/{projectId}/panels/{panelId}` 패널 종합현황 view, `/manufacturing/work`·`/quality/iqc`·`/quality/inspections`·`/materials/receipts`·`/materials/kitting`·`/logistics`가 `?project=&panel=&stage=` deep-link를 이미 받는다. stage→부서 매핑 함수(`departmentForStageCode`)와 18단계 stage 순서 테이블이 이미 존재한다.
- 인증은 `frontend/src/auth.ts` 기준 `Dev`/`EntraId` 2모드다. EntraId는 MSAL `loginRedirect`이며 redirect URI가 origin 기준이라 현재 경로 복원 장치는 없다(return URL 보존은 신규 최소 능력). 미인증 상태에서는 shell 전체가 로그인 gate로 막혀 업무 데이터가 렌더링되지 않는다.
- Backend 권한은 `Authorization/QmsPolicies.cs`의 정책 상수와 서버 policy 강제 원칙을 따른다. `AllowAnonymous`는 health/runtime-mode 3개뿐이다. 사용자 프로필(`/api/identity` 계열)은 부서 code를 포함한다.
- Migration은 `database/migrations/0046_sales_billing_requests.sql`까지 additive로 누적됐다. 신규는 `0047`이다.
- Backend 패키지에 QR 인코더가 없다(ClosedXML, MailKit, Microsoft.Identity.Web, Npgsql, PDFsharp뿐). Frontend에도 QR 라이브러리가 없다.
- 문서와 구현 사이의 의미 있는 충돌은 확인되지 않았다. blocking decision 0.

## 1. 한 줄 목표

권한 있는 사용자가 생성 가능한 패널의 실제 QR을 발급·인쇄하고, 현장 사용자가 그 QR을 스캔하면 Microsoft 365 인증 뒤 그 패널의 역할별 현재 업무 화면 또는 종합현황으로 3회 이내 조작으로 진입한다.

## 2. 배경과 해결할 업무 문제

- 현재는 패널명 입력 시 `QR 생성 가능/불가` 표시만 있고 부착·스캔할 실물 QR이 없다.
- 현장에서는 패널 식별과 현재 업무 확인을 구두·종이·PC 재조회로 우회하고 있어 대상 착오와 진입 시간 손실이 발생한다.
- 이 기능이 없으면 Roadmap의 "QR 기반 패널 단위 현장 추적" 목적과 MOBILE 계보의 현장 입력 UX가 실물 패널과 연결되지 않는다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 설계 담당 (`PanelInfoUpdate` 보유) | QR 발급, preview, 다운로드/인쇄 | 전체 | QR 발급(record 생성) |
| 품질·자재 등 현장 부착 관련 부서 | 기존 활성 QR 확인·재출력 | 전체 | 없음(발급 record 변경 불가) |
| 제조·품질·물류 현장 사용자 | 스캔 → landing → 담당 업무 진입 | 전체 | 기존 담당 화면의 기존 권한 그대로 |
| 영업·설계·생산관리·구매·자재(비담당 stage) | 스캔 → 패널 종합현황 조회 | 전체 | 없음 |
| System Administrator | rotation(재발급) + 사유 기록 | 전체 | 이전 token 폐기·신규 발급 |
| 승인 대기·비활성 사용자 | 없음 | 기존 gate대로 차단 | 없음 |

- 모든 활성 사내 사용자의 전체 조회·담당 부서만 쓰기 원칙을 그대로 유지한다. QR 스캔은 어떤 업무 상태도 변경하지 않는다.
- 발급을 `PanelInfoUpdate`에 연결하는 근거: QR 발급 가능 상태가 패널정보(패널명) 입력의 파생이고, 발급 UI가 설계의 패널 화면에 놓이며, 새 권한 상수를 늘리지 않는 최소안이다. 재출력·다운로드는 token이 업무정보를 담지 않으므로 전체 조회 원칙에 따라 활성 사용자 모두에게 허용한다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 발급·인쇄 (PC)

1. 설계 담당이 프로젝트 상세 패널 목록에서 `QR 생성 가능` 패널을 확인하고 `QR 발급`을 실행한다.
2. 시스템이 활성 QR record를 생성(이미 있으면 같은 record로 수렴)하고 preview·발급자·발급시각을 표시한다.
3. 사용자는 SVG/PNG 다운로드 또는 인쇄(단일·선택 패널 일괄 sheet)로 현장 부착용 출력물을 얻는다.

### 시나리오 B — 현장 스캔 (모바일)

1. 현장 사용자가 휴대폰 카메라로 QR을 스캔해 `/q/{token}`에 진입한다.
2. 미로그인 상태면 업무 데이터가 없는 안내와 함께 즉시 Microsoft 365 로그인으로 이동하고, 로그인 완료 후 원래 `/q/{token}`으로 복귀한다.
3. 서버가 token·프로젝트·패널·사용자를 검사한 뒤, landing 한 화면에 패널 identity(패널명·프로젝트)·현재 workflow 단계·내가 할 일·primary action을 보여 준다.
4. 제조 사용자는 primary action으로 `/manufacturing/work?project=…&panel=…`에 진입해 기존 권한대로 작업을 입력한다. 영업·관리자는 패널 종합현황으로 이동한다.

### 시나리오 C — 예외·복구

1. 잘못된 token·폐기 token·삭제 project·비활성 panel은 서로 구분되는 안전한 안내(업무정보 노출 없음)로 처리된다.
2. 같은 패널의 반복·동시 발급 요청은 하나의 활성 QR로 수렴하고, 재출력은 새 token을 만들지 않는다.
3. 보안 사고 시 System Administrator가 사유를 기록하고 rotation을 실행하면 이전 token은 `Revoked`가 되고 스캔 시 폐기 안내가 나온다.

## 5. 기능 요구사항

### 필수

- [ ] 패널별 실제 QR record(불투명 임의 token, 패널당 활성 1개, 발급자·발급시각)와 `IsQrEligible` 재사용 발급 gate
- [ ] 동시·반복 발급의 단일 활성 record 수렴(DB unique 제약 + idempotent 처리)
- [ ] 인증 사용자용 QR 이미지 생성(SVG·PNG)과 단일 preview/다운로드/인쇄
- [ ] 선택 패널 일괄 인쇄 sheet(패널명 라벨 + QR, print CSS)
- [ ] `/q/{token}` 스캔 진입: 미로그인 시 데이터 없는 즉시 로그인 유도 + return URL 보존, 로그인 후 복귀
- [ ] 인증 후 resolve API: token 상태·project scope 검사, invalid/revoked/deleted/inactive 구분 응답
- [ ] 모바일 우선 scan landing 화면: 패널 identity·현재 stage·내가 할 일·부서 기반 primary action
- [ ] 부서·현재 stage 기반 기존 화면 deep-link(제조→제조, 품질→해당 검사 stage, 물류→해당 물류 stage, 자재→도착/키팅, 그 외→패널 종합현황)
- [ ] 발급·rotation·다운로드/인쇄·스캔 resolve의 append-only 감사 event
- [ ] System Administrator 전용 rotation(사유 필수, 이전 token 폐기)

### 선택

- [ ] landing에서 해당 패널의 open Pending 존재 표시(기존 집계 재사용 범위에서만)

### 명시적 제외

- [ ] 실제 Microsoft Entra tenant·운영 redirect URI·public domain, 외부 QR/short URL SaaS, 라벨 프린터·재고 연동
- [ ] QR 스캔에 의한 자동 업무 완료·상태 변경, 현장 부착 여부의 별도 상태 추적
- [ ] 프로젝트 완료 후 일괄 비활성 정책 변경(기존 "활성 유지" 계약 유지), 대규모 라벨 template 관리자
- [ ] 대표 repo·`main`·push·PR·merge·Persistent UAT·실제 provider

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 프로젝트 상세 패널 목록/패널정보 (기존 확장) | `/projects/{id}` panels section, 패널 상세 | 기존 `QR 가능` 표시 + `발급됨/미발급`, 발급자·발급시각 compact 표시 | QR 발급, QR 보기, 선택 패널 일괄 인쇄 | action 근처 성공/실패 안내, 중복 submit 잠금 |
| QR preview (신규 modal/panel) | 패널 목록·패널 상세의 `QR 보기` | QR 이미지(대체 텍스트 포함), scan URL, 발급 정보 | SVG/PNG 다운로드, 인쇄, (관리자) rotation | 다운로드/인쇄 audit, rotation 사유 입력·확인 |
| 일괄 인쇄 sheet (신규) | 패널 목록 선택 → 인쇄 | 패널명 라벨 + QR 반복 grid | 브라우저 인쇄 | 인쇄 대상 수 표시 |
| Scan landing (신규, 모바일 최우선) | `/q/{token}` | 패널 identity, 현재 workflow 단계, 내가 할 일 요약, primary action 1개 | 담당 화면 이동 또는 종합현황 이동 | invalid/revoked/deleted/inactive/권한 상태를 구분한 한글 안내 |
| 로그인 gate (기존 재사용) | 미로그인 `/q/{token}` | 기존 인증 shell(업무 데이터 없음) | Microsoft 365 로그인 | 로그인 후 원래 QR 목적지 복귀 |

UX 확인 항목: WITHUS 계열 semantic token·공통 component·얇은 divider·blue accent 재사용, loading/empty/error/success 구분, 키보드/focus, QR 이미지 `alt`, 390px page-level overflow 0, PC table의 모바일 축소 복제 금지(landing 우선).

## 7. 업무 규칙과 불변조건

- QR payload는 `scan URL + opaque token`만 포함한다. 고객사·PJT Code·Title·패널명·내부 UUID를 QR과 미인증 응답에 넣지 않는다.
- 패널당 활성 QR은 항상 최대 1개이고, 발급 gate는 기존 `IsQrEligible` 판정과 동일 조건이다.
- 발급된 QR은 프로젝트 완료 후에도 활성 유지한다(Roadmap 8.3 계약 불변).
- 모든 업무 데이터는 인증·서버 권한 검사 후에만 반환하고, 스캔·landing은 어떤 업무 상태도 변경하지 않는다.
- 조회는 전 부서, 쓰기는 담당 권한이라는 기존 원칙과 System Administrator 비우회 원칙을 유지한다.
- 발급·rotation·출력·resolve는 append-only로 감사하고 승인·완료 기록을 덮어쓰지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 패널 QR record (후보명 `panel_qr_codes`) | panel_id, project_id, opaque token, status(`Active`/`Revoked`), 발급자/발급시각, 폐기자/폐기시각/사유 | 신규 | 활성 1개 partial unique + token unique, soft 상태 전이만 |
| QR 감사 event (후보명 `panel_qr_events`) | 발급/rotation/다운로드·인쇄/스캔 resolve의 actor·시각·결과 enum | 신규 | append-only |
| `qrEligible` 파생 판정 | 발급 gate 조건 | 기존 재사용 | 변경 없음 |
| 패널 workflow 단계 | landing의 현재 단계·deep-link 계산 | 기존 재사용 | 변경 없음 |

```text
(없음) → Active ──rotation(관리자·사유)──→ Revoked (+ 새 Active record)
```

- Token은 CSPRNG 128-bit 이상을 base64url(또는 동등 문자집합)로 인코딩한 값이며 raw로 저장한다. 근거: token 단독으로는 인증 없이는 어떤 데이터도 반환하지 않는 lookup 식별자이고, "재출력은 새 token을 만들지 않는다" 계약상 동일 QR 재생성이 필요하므로 digest-only 저장과 양립하지 않는다. 이 근거는 interview 5장의 "digest 또는 안전한 lookup representation" 중 후자를 선택한 것이다.

## 9. API·Backend 고려사항

- Backend authoritative 규칙: 발급 gate(eligibility), 활성 1개 수렴, 발급/rotation 권한, resolve의 token·project scope·상태 판정, 감사 기록.
- 필요한 조회·mutation (후보 경로, 기존 endpoint extension 패턴 준수):
  - `GET /api/projects/{projectId}/panels/{panelId}/qr` — 현재 활성 QR record 조회 (인증 사용자)
  - `POST /api/projects/{projectId}/panels/{panelId}/qr` — 발급, idempotent 수렴 (`PanelInfoUpdate`)
  - `POST /api/projects/{projectId}/panels/{panelId}/qr/rotate` — 사유 필수 (System Administrator)
  - `GET /api/projects/{projectId}/panels/{panelId}/qr/image?format=svg|png` — 이미지 (인증 사용자, download audit)
  - `GET /api/qr/resolve/{token}` — 인증 사용자 전용 resolve: projectId, panelId, 패널 표시 정보, 현재 stage, 상태 enum(`Ok`/`NotFound`/`Revoked`/`ProjectDeleted`/`PanelInactive` 등)
- Anonymous endpoint는 추가하지 않는다. `/q/{token}`의 미로그인 처리는 frontend 인증 gate만으로 수행하므로 미인증 상태에서 패널 존재 여부조차 조회할 수 없다(interview의 "최소 응답" 후보보다 노출이 더 적은 안).
- 권한·validation: 기존 policy 상수 재사용, 안정적 HTTP status + 한글 메시지, raw SQL·내부 식별자 비노출.
- transaction·동시성·idempotency: 발급은 단일 transaction에서 `insert … on conflict` 또는 partial unique index 충돌 처리로 활성 1개 수렴, rotation은 폐기+발급을 같은 transaction으로 묶고 동시성 테스트를 추가한다.
- audit trail: `panel_qr_events` append-only. resolve 성공/실패도 enum으로 기록한다(현장 대상 확인 추적).
- 외부 provider 영향: 없음. QR 이미지는 서버 내 라이브러리로 생성하며 발송·외부 호출이 없다.
- Scan URL origin은 configuration key(예: QR scan origin)로 두고 local 기본값은 HTTPS Development frontend origin으로 한다. 운영 domain 값은 운영 전환 Task 범위다.

Repository 조사 전 내부 클래스명·컬럼명·SQL 형태는 후보이며 구현 세션에서 기존 convention에 맞춰 확정한다.

## 10. Frontend 고려사항

- route/component: `initialViewFromLocation()`에 `/q/{token}` 패턴 추가(신규 view kind), landing page component, QR preview modal, 일괄 인쇄 sheet. 기존 view 전환·deep-link 파라미터 규약(`?project=&panel=&stage=`)을 재사용한다.
- return URL 보존: EntraId 모드에서 `/q/…` 진입 시 loginRedirect 전에 경로를 sessionStorage에 저장하고 인증 gate 완료 시 복원·`history.replaceState`. Dev 모드는 즉시 landing.
- loading/empty/error/success: resolve 대기, 각 실패 상태 구분 화면, 발급·다운로드 성공 안내, 중복 submit 잠금(기존 Action Feedback 계약 재사용).
- 접근성: QR 이미지 대체 텍스트, landing primary action의 keyboard/focus, `aria-live` 결과 안내.
- 390px/mobile: landing은 한 화면 완결, page-level horizontal overflow 0, PC 발급 table은 기존 반응형 카드 패턴을 따른다.

## 11. 기존 기능과의 연결

- 프로젝트/업무: 패널 종합현황·제조/품질/물류/자재 화면의 기존 deep-link와 stage 매핑(`departmentForStageCode`, stage 순서 테이블)을 그대로 소비한다. workflow 상태 계산을 변경하지 않는다.
- 권한/관리자: 기존 policy 상수와 사용자 부서 code를 재사용한다. rotation만 System Administrator 경계다.
- Excel/PDF/첨부: 영향 없음(QR 이미지는 별도 endpoint). 선택 export 계약을 변경하지 않는다.
- Teams/Mail/알림: 발송 없음. 알림 채널 matrix 불변.
- 삭제·복구/감사: 삭제 project의 token은 resolve에서 구분 안내하고, 복구되면 다시 유효하다(record 삭제·재발급 없음).

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장) | 명시 발급 + backend QR 인코더 패키지(SVG/PNG) + anonymous API 없는 `/q/{token}` 로그인 gate + 인증 resolve + landing 화면 | 감사 가능한 발급 actor, 단일 렌더링 계약, 미인증 노출 0, 기존 화면 재사용 최대 | backend 신규 package 1개 추가 필요 |
| B | eligibility 충족 시 자동 발급 + frontend npm QR 렌더링 + 즉시 자동 redirect landing | 클릭 수 최소 | 발급 actor·감사 부재, 부착 전 대량 token 생성, 클라이언트 렌더링은 다운로드 감사·서버 계약 이원화, 자동 redirect는 대상 확인(패널 identity 확인) 기회를 잃음 |

권장안 A의 세부 선택과 근거는 §16에 기록하며, standing instruction에 따라 자동 채택한다. QR 인코더는 외부 호출 없는 pure 생성 라이브러리(.NET용, 예: QRCoder 계열)를 additive dependency로 추가하고, 테스트에서 decode 검증용 test-only 라이브러리(예: ZXing 계열)를 backend test 프로젝트에만 추가한다. 정확한 패키지·버전은 Codex 구현 세션이 licence·유지 상태를 확인해 확정한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated DB·disposable runtime만 사용한다.
- migration 필요 여부: 있음 — `0047` additive(신규 2 table + index). fresh/기존 DB 모두 검증한다.
- 외부 발송/실제 데이터 영향: 없음. 실제 Entra tenant·provider 호출 없음(Dev auth 모드로 검증, EntraId 경로는 기존 로그인 shell 코드 재사용 범위).
- runtime 교체 여부: 없음. Development 5174/5081 범위.
- 추가 사용자 승인 필요 작업: `main`·push·PR·merge·UAT 승격은 이 Task에 포함되지 않으며 별도 승인(merge 승인 0/3 유지). backend package 추가는 이 planning·review 흐름의 계약에 포함해 진행한다.

## 14. 검증 계획

- 최소 테스트(Backend): 발급 성공/비적격 거부/권한 거부, 반복·동시 발급 수렴, rotation 권한·사유 필수·이전 token 폐기, resolve 상태 matrix(Ok/NotFound/Revoked/ProjectDeleted/PanelInactive/완료 project), 이미지 endpoint 형식, 생성 PNG의 decode 검증(인코딩된 URL == scan origin + `/q/{token}`), payload에 업무정보 부재 검증.
- Migration: 0001→0047 fresh 적용 + 기존 DB 추가 적용, catalog/ledger 정합.
- Frontend: lint·typecheck·build, landing 상태별 rendering unit test, Playwright mock-ui spec(발급 UX, landing 정상·예외, return URL 복원)을 desktop·390px에서 실행.
- 영향 영역 회귀: 패널정보·프로젝트 상세 관련 기존 backend/frontend suite 전체(현재 기준선 Backend 403·Frontend 110에 신규 추가).
- PR/CI: 해당 없음(local experiment commit only). Validation Matrix의 migration 포함 변경 기준을 따른다.
- 사용자 검수: `BATCHED_FINAL` 원칙대로 마지막 일괄 검수 대기로 기록하고, Codex가 발급 화면과 scan landing의 desktop/mobile screenshot을 증빙으로 남긴다.

## 15. 완료 기준

- 기능/권한/데이터: §5 필수 항목 전부 구현, 동시 발급 수렴과 권한 경계가 테스트로 증명됨, QR payload에 opaque token 외 업무정보 없음.
- UX: desktop·390px에서 발급·landing의 loading/empty/error/success와 overflow 0 확인.
- 자동 테스트: backend/frontend/migration/E2E 신규·회귀 전부 통과.
- 5종 산출물: `docs/12-task-completion-policy.md`에 따라 상태·위치 추적(Implementation report 필수).
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`로 기록.
- PR 상태: 해당 없음(local experiment commit only).

중단 조건: 문서·구현의 의미 있는 충돌 발견, read-only/fast-track 경계 위반 필요, 대표 repo·`main`·Persistent UAT·실제 provider 접근이 필요해지는 경우 즉시 중단하고 보고한다.

## 16. 미결정 사항 (비차단 — Fable 권장안 자동 채택)

| 번호 | 질문 | 권장안 | 근거 요약 | 사용자 결정 |
| ---: | --- | --- | --- | --- |
| 1 | 발급 시점·actor | `PanelInfoUpdate` 보유자의 명시 발급 | 감사 가능한 actor, 부착 전 대량 발급 방지, 신규 권한 상수 불필요 | 자동 채택 |
| 2 | token·URL 수명 | 영구 opaque raw token 저장, 완료 후 활성 유지 | Roadmap 8.3 계약 불변, 재출력 동일 token 요구, token 단독 무권한 | 자동 채택 |
| 3 | 생성·출력 UX | server-side SVG/PNG + 단일 preview/다운로드/인쇄 + 선택 일괄 인쇄 sheet | 단일 렌더링 계약·다운로드 감사, 외부 SaaS 불요 | 자동 채택 |
| 4 | 미로그인 landing | anonymous API 없이 즉시 로그인 redirect + return URL 보존 | 미인증 노출 0, 기존 인증 shell 재사용 | 자동 채택 |
| 5 | 로그인 후 route | landing 1화면(identity 확인) + 부서·현재 stage 기반 primary action deep-link | 대상 확인 수단 유지, 3회 이내 진입 충족, 기존 route 재사용. 사용자 부서가 단일이므로 별도 역할 선택 UX 불요, 관리자·비담당 부서는 종합현황 | 자동 채택 |
| 6 | 현장 부착 상태 | 별도 부착 상태 추적 없음(발급 record만), IQC 부착 규칙은 안내 문구 | 발급 조건과 부착 조건 분리 계약 유지, MVP 최소화 | 자동 채택 |
| 7 | 분실·오염 복구 | 재출력은 동일 token, rotation은 System Administrator + 사유 필수 + 이전 token `Revoked` + 감사 | interview 복구 요구의 최소 구현, revoked 상태의 실제 발생 경로 확보 | 자동 채택 |

Deferred 후속(이 Task 범위 아님): 완료 프로젝트 QR 일괄 비활성 정책, 운영 scan domain·운영 Entra 설정, 라벨 template 고도화.

## 17. 예상 변경 범위 (확정 allowlist 아님 — 조사 대상)

- Backend: 신규 `PanelQr`(가칭) endpoint/store/domain/contract, `Program.cs` endpoint 등록, QR 인코더 package 추가, scan origin configuration key.
- Frontend: `frontend/src/App.tsx` route·view 추가, landing/preview/인쇄 component, 패널 목록·상세의 QR 상태·action, return URL 보존, 관련 CSS(기존 semantic token 재사용).
- DB/Migration: `database/migrations/0047_*.sql` (QR record + event, index).
- Tests/Scripts: backend API·동시성·decode 테스트, migration 테스트 확장, frontend unit·Playwright spec.
- Docs: Roadmap 21·23장과 실험 완료 원장의 상태 갱신, 5종 산출물.

## 18. Roadmap 연결

- 선행 Task: MOBILE-001/002, 제조·품질·물류·패널 상세 계보 — experiment 완료 확인.
- 후속 Task: 운영 전환(운영 domain·Entra), 완료 후 QR 비활성 정책(별도 POLICY_DECISION), 라벨 template 고도화.
- 현재 Go/No-Go: 완료 원장 우선순위 1 + standing instruction으로 fast-track Go. `roadmapSequenceMatch: true`.
- 별도 Task로 분리할 항목: §16 Deferred 3건.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-20 | experiment fast-track 지시(인터뷰·중간 승인 생략, 권장안 자동 채택, local commit까지) | 비차단 7건을 §16 자동 채택으로 기록, 게시 경계 유지 |

## 20. Codex 구현 지시문 초안

1. instruction chain gate 재수행 후 이 planning의 Codex 내용 review를 `tasks/qr-001-review.md`에 1회 작성한다(유지/추가/보류/제거·resolution).
2. Review를 입력으로 승인된 second-planning 절차에 따라 Fable 2차 기획을 별도 target에 생성한다. 2차 기획의 blocking decision이 0이면 구현으로 진행한다.
3. 구현 순서 권장: migration 0047 → backend domain/store/endpoint + 단위·API·동시성 테스트 → QR 인코더·이미지 endpoint + decode 테스트 → frontend 발급 UX → `/q/{token}`·return URL·landing → 일괄 인쇄 → 전체 회귀 → desktop/390px screenshot.
4. 검증은 §14 전체를 isolated DB·disposable runtime에서 실행하고, 실패·미실행을 성공으로 기록하지 않는다.
5. Implementation report·5종 산출물·완료 원장/Roadmap 상태 갱신 후 승인된 allowlist 경로만 stage해 local experiment commit 1회로 종료한다. push·PR·merge·UAT·실제 provider는 수행하지 않는다.

---

- `planningStatus: DRAFT`
- `implementationApproved: false`
- `userDecisionRequiredCount: 0`
