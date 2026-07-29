# TASK-QR-001 — 패널 QR 생성·스캔 랜딩 2차 기획 (최종 구현 계약)

- secondPlanningStatus: `DRAFT_FOR_IMPLEMENTATION`
- sourceTask: `TASK-QR-001`
- authoringModel: `FABLE_5`
- interviewSource: `tasks/qr-001-interview.md` (`COMPLETED_CONFIRMED`, `userConfirmed: true`)
- firstPlanningSource: `tasks/qr-001-planning.md` (byte-for-byte 보존, 수정하지 않음)
- codexReviewSource: `tasks/qr-001-review.md` (`COMPLETED`, `recommendedDisposition: FABLE_SECOND_PLANNING`)
- approvalChange: `tasks/qr-001-change-001.md` (`fableSecondPlanningApproved: true`, exact target `docs/34-qr-scan-landing-plan.md`)

이 문서는 `experiment/*` 2-pass fast-track의 Fable 2차 기획이며 TASK-QR-001의 authoritative 구현 계약이다. 1차 기획의 유지 판정 내용을 보존하고 Codex review의 추가(R1~R7)·보류·제거 resolution을 모두 반영했다. 공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`, `docs/development/validation-matrix.md`, `docs/development/privacy-safe-evidence.md`를 따르며 복사하지 않는다. 이 문서는 local experiment 구현 계약일 뿐 push·PR·`main` merge·Persistent UAT·실제 provider 승인을 부여하지 않는다.

## 1. 한 줄 목표

권한 있는 사용자가 생성 가능한 패널의 실제 QR을 발급·인쇄하고, 현장 사용자가 QR을 스캔하면 인증 뒤 패널 identity를 확인하고 자신의 부서·현재 stage·담당 권한에 맞는 화면으로 3회 이내 조작으로 진입한다.

## 2. 확정 기준선 (재검증 완료)

- Roadmap 8장 계약: QR 생성 가능 = 프로젝트 Active·미삭제 + 패널 Active + 패널명 존재. 생산계획·IQC·현장 부착은 발급 조건이 아니다. 패널당 QR 1개, 민감정보 직접 포함 금지, 발급 후 활성 유지. Roadmap 실행 큐 4.4와 실험 완료 원장 우선순위 1이 이 Task를 첫 미완료 제품 Task로 지정한다.
- `qrEligible`은 `backend/src/Emi.Qms.Api/PanelInformation/PanelInformationDomain.cs`의 `IsQrEligible` 단일 판정 파생값이며, 실제 QR record·token·이미지·landing은 Repository 어디에도 없다.
- Frontend는 `frontend/src/App.tsx`의 `initialViewFromLocation()` 경로 매핑 단일 shell이다. `/projects/{projectId}/panels/{panelId}` 패널 종합현황과 `/manufacturing/work`·`/quality/iqc`·`/quality/inspections`·`/materials/receipts`·`/materials/kitting`·`/logistics`의 `?project=&panel=&stage=` deep-link, `departmentForStageCode`와 18단계 순서 테이블이 이미 존재한다.
- 인증은 `frontend/src/auth.ts`의 `Dev`/`EntraId` 2모드다. EntraId는 MSAL `loginRedirect`이며 현재 경로 복원 장치가 없다. 미인증 상태에서는 shell 전체가 로그인 gate로 막힌다.
- 권한은 `Authorization/QmsPolicies.cs` 정책 상수(서버 강제)를 재사용한다. `AllowAnonymous`는 health/runtime-mode 3개뿐이며 이번 Task에서 늘리지 않는다.
- Migration은 `0046`까지 additive 누적, 신규는 `0047`이다. Backend·Frontend 어디에도 QR 인코더/디코더 라이브러리가 없다.
- 문서·구현 사이 의미 있는 충돌 없음. blocking decision 0.

## 3. 확정 정책 (1차 유지 + review resolution 통합)

1. **명시 발급 유지** — `PanelInfoUpdate` 보유자가 명시적으로 발급한다. eligibility 충족 자동 발급과 일괄 인쇄 중 묵시적 자동 발급은 금지한다(review 제거 항목).
2. **패널당 활성 QR 1개** — 반복·동시 발급은 DB 제약과 transaction으로 하나의 활성 record로 수렴한다. 재출력은 새 token을 만들지 않는다.
3. **Token은 고엔트로피 공개 식별자, 인증수단 아님 (R1)** — 256-bit CSPRNG를 base64url로 인코딩한 opaque token을 사용하고, 동일 QR 재생성을 위해 DB QR record에 원문을 저장한다. token만으로는 어떤 데이터도 반환되지 않으며 모든 resolve는 기존 사용자 인증·project scope를 다시 검사한다. token·scan URL은 application log, audit payload, error message, correlation metadata에 기록하지 않는다 — DB QR record와 권한 있는 이미지 응답에만 존재한다. 고객사·PJT Code·Title·패널명·내부 project/panel UUID는 QR payload에 넣지 않는다.
4. **Anonymous API 없음 유지** — `/q/{token}`의 미로그인 처리는 frontend 인증 gate만으로 수행한다. 미인증 상태에서 token 존재·panel 상태·업무정보를 전혀 노출하지 않는다.
5. **모바일 landing 1화면 유지** — 자동 silent redirect가 아니라 패널 identity 확인 화면을 거쳐 primary action으로 이동한다.
6. **Primary action은 3중 일치 시에만 수정 화면 (R3)** — current stage의 담당 부서 = 사용자 부서이고 해당 기존 write policy를 보유할 때만 기존 업무 deep-link를 primary action으로 제공한다. 불일치·완료 project·관리자·영업·비담당 부서는 패널 종합현황(조회 전용)이 primary action이다. 다중 역할 선택 화면은 현재 identity model이 단일 department code이므로 만들지 않는다.
7. **감사는 서버가 관찰한 사실만 (R2)** — canonical event는 `Issued`, `Rotated`, `ImageRendered`, `PrintSheetRendered`, `ResolveSucceeded`와 유효 token의 bounded 상태 조회(revoked/inactive 계열)뿐이다. `Downloaded`/`Printed` 완료는 서버가 증명할 수 없으므로 기록하지 않는다. unknown/malformed token 스캔은 QR audit table에 저장하지 않는다(저장소 DoS 방지). raw token·scan URL은 event detail에 남기지 않는다.
8. **Rotation은 System Administrator 전용** — 사유 필수, 같은 transaction에서 이전 token `Revoked` + 새 활성 token 발급, 감사 기록. 재출력(동일 token)과 명확히 구분한다.
9. **일괄 인쇄 안전 상한 (R5)** — 같은 project의 기발급 active QR 최대 50개 panel. 서버가 frontend 선택과 무관하게 project scope·active QR·panel 존재를 재검증하고, 일부 stale·권한 실패가 있으면 전체 실패로 처리하며 정확한 대상 수를 다시 안내한다. 서로 다른 project를 한 sheet에 섞지 않는다.
10. **로그인 복귀는 same-origin allowlist (R6)** — `/q/{token}` 경로만 return target으로 허용한다. absolute external URL은 저장·복원하지 않는다.
11. **현장 부착 상태 추적 제외 유지** — 발급 record만 관리하고 IQC 후 부착 규칙은 안내 문구 수준으로 둔다.
12. **완료 project QR 활성 유지** — Roadmap 8.3 계약 불변. 스캔은 어떤 업무 상태도 변경하지 않는다.

## 4. 대상 사용자와 권한

| 사용자/역할 | 행동 | 서버 정책 |
| --- | --- | --- |
| 설계 담당 (`PanelInfoUpdate`) | QR 발급, preview, 재출력, 일괄 인쇄 | 발급 mutation은 `PanelInfoUpdate` |
| 모든 승인된 활성 사내 사용자 | 활성 QR 확인·이미지 조회·재출력·스캔 landing | 인증 + 기존 project scope(read) |
| 제조·품질·물류·자재 현장 사용자 | landing → 3중 일치 시 담당 화면 진입·기존 권한대로 입력 | 기존 write policy (`ManufacturingUpdate`, `QualityInspect`, `LogisticsShip`, `MaterialReceiptUpdate`) — 이번 Task에서 변경 없음 |
| System Administrator | rotation(사유 필수) | 기존 System Administrator 판정. 업무 입력 우회는 없음 |
| 승인 대기·비활성·restricted 사용자 | 차단 | 기존 gate·`Forbid` 유지 |

새 권한 상수를 추가하지 않는다. UI 숨김은 보조 수단이며 서버 policy가 최종 기준이다.

## 5. 데이터 모델과 lifecycle

| 개념 | 내용 | 비고 |
| --- | --- | --- |
| QR record (후보명 `panel_qr_codes`) | id, project_id, panel_id, token(원문, unique), status(`Active`/`Revoked`), issued_by/issued_at_utc, revoked_by/revoked_at_utc/revoke_reason | partial unique index: `(panel_id) where status='Active'`. record 삭제 없음, 상태 전이만 |
| QR event (후보명 `panel_qr_events`) | qr_id, event_type enum(§3-7), actor, occurred_at_utc, 최소 detail(예: sheet panel 수, resolve 상태 enum) | append-only. raw token·scan URL·raw body 금지 |

```text
(없음) ──명시 발급(PanelInfoUpdate)──→ Active ──rotation(관리자·사유)──→ Revoked (+ 새 Active record)
```

- Token 생성: CSPRNG 256-bit → base64url(패딩 없음). 발급 transaction 안에서 insert 충돌 시 기존 활성 record를 재조회해 반환한다(idempotent 수렴).
- Scan URL: `{configured scan origin}/q/{token}`. scan origin은 configuration key로 관리하고 local 기본값은 HTTPS Development frontend origin이다. 운영 domain 값은 운영 전환 Task 범위다.
- Migration: `database/migrations/0047_*.sql` 하나의 additive migration(2 table + index + enum 제약). fresh(0001→0047)와 기존 DB 추가 적용을 모두 검증한다. 정확한 테이블·컬럼명은 Codex 구현 세션이 기존 SQL convention에 맞춰 확정한다.

## 6. API 계약 (후보 경로 — 기존 endpoint extension 패턴 준수)

| Endpoint | 정책 | 계약 |
| --- | --- | --- |
| `GET /api/projects/{projectId}/panels/{panelId}/qr` | 인증 + project scope | 현재 활성 QR record(없으면 명시적 미발급 응답), 발급자·발급시각, scan URL |
| `POST /api/projects/{projectId}/panels/{panelId}/qr` | `PanelInfoUpdate` | `IsQrEligible` 재검증 후 발급. 기활성 시 같은 record로 idempotent 수렴(성공 응답). 비적격은 안정적 4xx + 한글 안내. `Issued` event |
| `POST /api/projects/{projectId}/panels/{panelId}/qr/rotate` | System Administrator | 사유 필수. 같은 transaction에서 이전 `Revoked` + 신규 `Active`. `Rotated` event |
| `GET /api/projects/{projectId}/panels/{panelId}/qr/image?format=svg\|png` | 인증 + project scope | 서버 생성 QR module만 포함(inline active content 없음). `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`. `ImageRendered` event |
| `POST /api/projects/{projectId}/qr/print-sheet` | 인증 + project scope | 요청 panel 목록(≤50, 같은 project) 전체를 서버가 재검증: panel 존재·active QR 보유. 하나라도 실패면 전체 실패 + 실패 사유·정확한 대상 수 안내. 성공 시 sheet 구성 정보 반환, `PrintSheetRendered` event(대상 수 기록) |
| `GET /api/qr/resolve/{token}` | 인증 사용자 | §7 상태 matrix에 따른 정규화 응답. `ResolveSucceeded` 또는 bounded 상태 조회 event |

- 이미지 파일명은 서버가 정의한다: `panel-qr-{safeLabel}.{svg|png}`. `safeLabel`은 패널명을 안전 문자집합(`A-Za-z0-9-_`)으로 정규화·길이 제한한 값이며 비정규화 실패 시 record 단축 식별자를 사용한다. 고객사·PJT Code·Title은 포함하지 않고 formula/header injection이 불가능해야 한다 (R7).
- resolve 응답은 route 판단에 필요한 최소 projection만 담는다: 상태 enum, (허용 상태에서만) 패널 표시명·프로젝트 표시 정보·현재 stage code/명, 제안 route hint, `canEditCurrentStage` boolean. raw 내부 식별자·SQL·stack trace 비노출.
- QR 인코더는 외부 호출 없는 pure .NET 생성 라이브러리를 additive dependency로 추가한다(후보: QRCoder 계열). 테스트 decode용 라이브러리(후보: ZXing 계열)는 backend test 프로젝트에만 추가한다. 정확한 패키지·버전·licence는 Codex가 확인해 확정한다.

## 7. Resolve 상태 matrix와 정보 노출 경계 (R4)

| 상태 | 판정 | 응답·화면 | Identity 노출 | 감사 |
| --- | --- | --- | --- | --- |
| `Ok` | Active token + Active project/panel + scope 통과 | landing: 패널 identity·현재 stage·primary action | 허용(인증·scope 통과) | `ResolveSucceeded` |
| `OkCompletedProject` | Active token + 완료 project | landing 조회 전용, primary action = 종합현황, 쓰기 action 없음 | 허용 | `ResolveSucceeded` |
| `PanelInactiveOrProjectHold` | panel 비활성 또는 project Hold/Cancel | 인증·scope 통과 후 최소 identity + 조회 전용 종합현황 action, 쓰기 없음 | 최소 | bounded 상태 조회 event |
| `Revoked` | 폐기 token | 「더 이상 사용할 수 없는 QR」 안내만. panel/project 표시정보 없음 | 없음 | bounded 상태 조회 event |
| `NotFound` | malformed/unknown token 전부 | 단일 동일 안내(구별 불가) | 없음 | QR event 저장 안 함(기존 보안 telemetry 범위) |
| `ProjectDeleted` | 삭제 project | `ProjectDeletedRead` 없는 사용자에게는 revoked/unknown과 구별되는 업무정보를 주지 않는 일반 안내. 보유 사용자에게는 삭제 상태 안내 + 기존 조회 경로만, 복구 action 없음 | 권한별 차등 | bounded 상태 조회 event |
| `Forbidden` | restricted project 사용자 | 기존 endpoint와 동일 `Forbid`, identity 미반환 | 없음 | 기존 authorization audit 범위 |

## 8. Landing과 primary action 규칙

Landing 한 화면(모바일 최우선): 패널 identity(패널명·프로젝트 표시 정보), 현재 workflow 단계, 내가 할 일 요약, primary action 1개, 보조 link(패널 종합현황).

Primary action 결정(서버 `canEditCurrentStage` + route hint, frontend는 기존 view 매핑 재사용):

| 현재 stage | 담당 부서 | 필요 write policy | 3중 일치 시 deep-link |
| --- | --- | --- | --- |
| MaterialArrived, ReceiptConfirmed | 자재 | `MaterialReceiptUpdate` | `/materials/receipts` (기존 project 파라미터 규약) |
| KittingCompleted | 자재 | `MaterialReceiptUpdate` | `/materials/kitting?project=…&panel=…` |
| ManufacturingWork, ManufacturingCompleted | 제조 | `ManufacturingUpdate` | `/manufacturing/work?project=…&panel=…` |
| IQC | 품질 | `QualityInspect` | `/quality/iqc` |
| LQC, OQC, CustomerInspection, FAT | 품질 | `QualityInspect` | `/quality/inspections?stage=…&project=…&panel=…` |
| PackingCompleted, DepartureProcessed, DeliveryCompleted | 물류 | `LogisticsShip` | `/logistics?stage=…&project=…&panel=…` |
| 위 외 stage(영업·생산관리·설계·구매) 및 모든 불일치·관리자·완료 | — | — | `/projects/{projectId}/panels/{panelId}` 종합현황(조회 전용) |

- stage→부서 판정은 기존 `departmentForStageCode`·stage 순서 계약과 일치해야 하며 새 매핑 체계를 만들지 않는다.
- 사용자 부서가 제조여도 현재 stage가 품질·물류면 제조 수정 화면으로 보내지 않는다. deep-link 진입 후의 실제 쓰기 허용은 항상 기존 서버 policy가 최종 판정한다.

## 9. 미로그인 진입과 로그인 복귀 (R6)

1. `/q/{token}` 진입 시 미인증이면 업무 데이터 없는 기존 인증 shell로 즉시 이동한다. anonymous 데이터 호출은 없다.
2. EntraId 모드: MSAL의 request-start restoration을 우선 사용하고, 부족한 경우에만 sessionStorage에 path/query/hash를 저장한다.
3. 복원 값은 same-origin 상대 경로 + `/q/` prefix + 길이 제한을 검증하고 1회 소비 후 즉시 삭제한다. 검증 실패 시 기본 Home으로 간다. absolute external URL은 저장·복원하지 않는다.
4. Dev auth 모드도 같은 route 계약을 사용하며 동일하게 테스트한다.
5. 복원 후 `history.replaceState`로 landing 경로를 정착시킨다.

## 10. 화면·UX 계약

| 화면 | 진입 | 표시·행동 | 피드백 |
| --- | --- | --- | --- |
| 패널 목록/패널정보 (기존 확장) | `/projects/{id}` panels section·패널 상세 | 기존 `QR 가능` + `발급됨/미발급`, 발급자·발급시각 compact, `QR 발급`(권한자)·`QR 보기`·선택 일괄 인쇄 | action 근처 성공/실패, 중복 submit 잠금, 권한 없으면 비활성 + 이유 |
| QR preview (신규) | `QR 보기` | QR 이미지(`alt` 포함), scan URL, 발급 정보, SVG/PNG 다운로드, 인쇄, (관리자) rotation 사유 입력·확인 | 발급/rotation 결과 안내 |
| 일괄 인쇄 sheet (신규) | 패널 선택 → 인쇄 | 같은 project ≤50개, 패널명 라벨 + 서버 이미지, browser print CSS | stale 시 전체 실패 + 대상 수 재안내 |
| Scan landing (신규, 모바일 최우선) | `/q/{token}` | §7·§8 상태별 화면, primary action 1개 | 상태 구분 한글 안내, loading/error 구분 |

- WITHUS 계열 semantic token·공통 component·얇은 divider·blue accent를 재사용한다. 390px page-level horizontal overflow 0, PC table의 모바일 축소 복제 금지, 키보드/focus/`aria-live` 접근성 유지.

## 11. 업무 규칙과 불변조건

- QR payload는 scan URL(origin + `/q/{token}`)만 포함한다.
- 발급 gate는 기존 `IsQrEligible`과 동일 조건이며 이번 Task에서 조건을 바꾸지 않는다.
- 스캔·landing·resolve는 어떤 업무 상태도 변경하지 않는다.
- 조회는 전 부서·쓰기는 담당 권한, System Administrator 비우회 원칙 유지.
- 발급·rotation·render·resolve 감사는 append-only이며 기존 기록을 덮어쓰지 않는다.
- `main`·대표 repo·Persistent UAT·실제 Entra tenant·실제 provider 불변.

## 12. 검증 계획

- Backend: 발급 성공/비적격/권한 거부, 반복·동시 발급 수렴(경쟁 테스트), rotation 권한·사유 필수·폐기 전이, resolve 상태 matrix 전체(§7의 7개 상태 + `ProjectDeletedRead` 차등), print-sheet 상한·혼합 project 거부·stale 전체 실패, 이미지 header(no-store/nosniff)·파일명 안전성, 감사 event 종류·raw token 부재 검증.
- Decode 계약 테스트: 생성 SVG/PNG를 test-only 디코더로 해독해 정확히 configured scan origin + `/q/{token}`만 들어 있는지 확인 (R7).
- Log/감사 위생: token·scan URL이 log·event detail·오류 메시지에 없음을 테스트로 확인 (R1).
- Migration: 0001→0047 fresh 적용 + 기존 DB 추가 적용, catalog·ledger 정합.
- Frontend: lint·typecheck·build, landing 상태별 rendering·return path 검증(1회 소비·prefix·길이) unit test, Playwright mock-ui spec(발급 UX, landing 정상·예외, 복귀)을 desktop·390px에서 실행.
- 영향 회귀: 패널정보·프로젝트 상세 관련 기존 suite 포함 전체 회귀(현재 기준선 Backend 403·Frontend 110에 신규 추가).
- 증빙: 발급 화면·scan landing의 desktop/mobile privacy-safe screenshot. E2E는 isolated DB·disposable runtime만 사용한다.
- 사용자 검수: `사용자 검수 대기 — 마지막 일괄 검수`로 기록하며 완료로 가장하지 않는다.

## 13. 보류·제외 (재확정)

- 보류(별도 후속): 현장 부착 완료 상태 추적(현장 검수 뒤 change/NEW_FEATURE), 완료 project QR 일괄 비활성(별도 `POLICY_DECISION`), 라벨 template 관리자·프린터 연동(별도 Task), landing의 open Pending 상세 요약(optional P3).
- 제외: 실제 Entra tenant·운영 redirect URI·public domain·외부 QR/short URL SaaS·재고 연동, 스캔에 의한 자동 업무 완료·상태 변경, invalid scan의 QR event 저장, `Downloaded`/`Printed` 완료 기록, 부서만 일치하는 무조건 수정 화면 이동, 일괄 인쇄 중 자동 발급, 다중 역할 선택 화면, push·PR·merge·Persistent UAT·실제 provider.

## 14. 구현 순서와 예상 변경 범위

권장 순서 (Codex review 채택):

1. `0047` additive migration + QR domain·token 규칙 + partial unique·token unique·event enum
2. Backend 발급·조회·rotation·resolve + project scope/상태 matrix + 동시성·감사 테스트
3. QR SVG/PNG renderer + decode·header·payload 테스트
4. 패널 목록/상세의 발급·preview·선택 인쇄 UX
5. `/q/{token}` 인증 복귀·mobile landing·3중 일치 primary action
6. desktop/390px isolated E2E, fresh/existing migration, 전체 영향 회귀, privacy-safe screenshot

예상 변경 범위(확정 allowlist 아님 — 구현 세션이 확정): backend 신규 QR endpoint/store/domain/contract·`Program.cs` 등록·인코더 package·scan origin 설정 key, frontend route/view·landing·preview·인쇄·복귀 로직·패널 화면 확장, `database/migrations/0047_*.sql`, backend/frontend/E2E 테스트, Roadmap·실험 완료 원장 상태 갱신과 5종 산출물.

## 15. 완료 기준과 중단 조건

- 완료: §3 확정 정책과 §6~§10 계약이 구현되고 §12 검증이 전부 통과하며, 동시 발급 수렴·미인증 노출 0·상태 matrix·감사 위생이 테스트로 증명된다. 5종 산출물 상태·위치를 추적하고 local experiment commit 1회로 종료한다.
- 중단: 문서·구현의 의미 있는 충돌 발견, read-only/fast-track 경계 위반 필요, 대표 repo·`main`·Persistent UAT·실제 provider 접근이 필요해지는 경우 즉시 중단하고 보고한다.
- 게시 경계: pushApproved/prApproved/mergeApproved 모두 `false`, `main` merge 승인 `0/3`, Persistent UAT·실제 provider 미승인. 이 문서는 이를 변경하지 않는다.

openBlockingDecisionCount: 0
