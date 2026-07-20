# TASK-QR-001 — Codex 내용 Review

## Review 기준

- 대상 원문: `tasks/qr-001-planning.md` (Fable 1차, byte-for-byte 보존)
- interview source: `tasks/qr-001-interview.md`
- 기준 branch/HEAD: `experiment/task-home-002-personalized-shell` / `0b5b40be2b1967ec14a9eab0f05a6f2db4e969b2`
- reviewOwner: `CODEX`
- reviewRound: 1
- instructionChainRead: true
- openBlockingDecisionCount: 0

## 총평

1차 기획은 기존 `qrEligible`에서 실제 QR 발급·인쇄·스캔 landing으로 이어지는 사용자 가치를 정확히 잡았고, 미인증 데이터 노출 0·패널당 활성 QR 1개·기존 쓰기 권한 불변·모바일 landing 우선이라는 제품 방향도 타당하다. 명시 발급과 동일 QR 재출력은 현장 부착 전 대량 token 생성과 라벨 혼선을 줄이므로 유지한다.

다만 감사 이벤트의 사실성, invalid scan의 무제한 저장 위험, 역할/부서만 보고 수정 화면으로 보내는 잘못된 primary action, bulk 인쇄 상한, 삭제·제한 project의 정보 노출 경계를 2차 기획에서 더 명확히 해야 한다. 아래 resolution을 반영하면 blocking decision은 없다.

## 유지

| 항목 | 판단 | 사용자 가치·근거 |
| --- | --- | --- |
| `PanelInfoUpdate` 보유자의 명시 발급 | 유지 | 패널명 입력 책임과 발급 위치가 일치하고 발급 actor를 남길 수 있다. eligibility 충족 즉시 자동 발급보다 현장 라벨 준비 시점이 명확하다. |
| 패널당 활성 QR 1개·반복/동시 발급 수렴 | 유지 | 같은 패널에 서로 다른 QR이 붙는 운영 사고를 DB 제약과 transaction으로 막는다. |
| 서버 QR SVG/PNG + 단일 preview·재출력 | 유지 | scan payload와 파일 생성 계약을 한 곳에서 관리하고 모바일·PC가 같은 결과를 사용한다. |
| anonymous API 없는 `/q/{token}` 인증 gate | 유지 | 미로그인 상태에서 token 존재·panel 상태·업무정보를 전혀 노출하지 않는다. |
| 인증 후 모바일 landing 1화면 | 유지 | 사용자가 대상 패널을 먼저 확인하고 현재 업무로 이동하므로 QR의 ‘대상 확인’ 목적과 3회 이내 진입 목표를 함께 만족한다. |
| 관리자 rotation + 사유 + 이전 QR 폐기 | 유지 | 동일 QR 재출력과 보안 사고 복구를 구분하며 실제 `Revoked` 상태의 운영 경로를 제공한다. |
| 현장 부착 상태 별도 추적 제외 | 유지 | 발급 가능 조건과 IQC 후 실제 부착 규칙을 섞지 않고 이번 MVP를 QR 생성·진입에 집중한다. |

## 추가

### R1. QR token을 ‘비밀 인증수단’이 아니라 고엔트로피 공개 식별자로 명시

- 256-bit CSPRNG base64url token을 사용하고 DB에는 원문을 저장해 동일 QR을 재생성한다.
- QR이 물리적으로 노출되는 특성상 token 자체를 인증수단으로 취급하지 않는다. token만으로는 데이터가 반환되지 않고, 모든 resolve는 기존 사용자 인증·project scope를 다시 검사한다.
- token·scan URL은 application log, audit payload, error message, correlation metadata에 기록하지 않는다. DB QR record와 권한 있는 이미지 응답에만 존재한다.
- customer/project/panel 정보와 내부 project/panel UUID는 QR payload에 넣지 않는다.

### R2. 감사 이벤트를 서버가 실제로 관찰한 사실로 제한

- `Issued`, `Rotated`, `ImageRendered`, `PrintSheetRendered`, `ResolveSucceeded`만 canonical audit event로 둔다.
- 브라우저가 파일을 저장했는지 또는 실제 프린터로 인쇄했는지는 서버가 알 수 없으므로 `Downloaded`, `Printed` 완료로 기록하지 않는다.
- `NotFound` random token 스캔을 매번 append-only table에 넣으면 저장소 DoS가 가능하다. invalid unauthenticated/unknown-token 요청은 QR audit table에 저장하지 않고 기존 보안 telemetry 범위로 남긴다. 인증된 유효 token의 성공 resolve와 유효하지만 revoked/inactive인 상태 조회만 bounded event로 기록한다.
- raw token·scan URL은 QR event detail에 남기지 않는다.

### R3. primary action은 ‘부서 + 현재 stage + 기존 담당 권한’이 모두 맞을 때만 수정 화면

- 사용자 부서가 제조라고 해서 현재 stage가 품질·물류인 패널을 제조 수정 화면으로 보내지 않는다.
- current stage의 담당 부서와 현재 사용자의 부서가 일치하고 해당 기존 write policy가 있을 때 기존 업무 deep-link를 primary action으로 제공한다.
- 일치하지 않거나 완료 project·관리자·영업·비담당 부서는 패널 종합현황을 primary action으로 제공하고 각 부서 data는 조회 전용으로 유지한다.
- 다중 역할 선택 화면은 현재 identity model이 단일 department code이므로 이번 범위에서 만들지 않는다.

### R4. resolver의 정보 노출 상태를 scope에 맞게 정규화

- malformed/unknown token은 모두 동일 `NotFound` 응답·화면으로 처리한다.
- revoked token은 panel/project 표시정보 없이 `더 이상 사용할 수 없는 QR`만 보여 준다.
- deleted project는 `ProjectDeletedRead` 권한이 없는 사용자에게 revoked/unknown과 구별되는 업무정보를 주지 않는다. 해당 권한 사용자에게도 별도 복구 action은 제공하지 않는다.
- inactive panel 또는 project hold/cancel은 인증·scope 통과 후에만 최소 identity와 조회 전용 종합현황 action을 보여 주며 쓰기 action은 제공하지 않는다.
- restricted project 사용자는 기존 endpoint와 동일하게 `Forbid` 처리하고 identity를 반환하지 않는다.

### R5. 선택 인쇄의 안전 상한과 stale 재검증

- 일괄 인쇄는 현재 project의 최대 50개 panel로 제한한다. frontend 선택과 무관하게 서버가 project scope·active QR·panel 존재를 재검증한다.
- 미발급/비적격 panel을 묵시적으로 자동 발급하지 않는다. 설계 담당자가 먼저 명시 발급한 active QR만 인쇄 대상이다.
- 일부 stale/권한 실패가 있으면 전체 실패로 처리하고 정확한 대상 수를 다시 안내한다. 서로 다른 project를 한 sheet로 섞지 않는다.
- print sheet는 browser print CSS로 구성하되 QR image source는 server-generated SVG/PNG endpoint를 사용한다.

### R6. 로그인 복귀 경로를 same-origin allowlist로 제한

- `/q/{token}` 경로만 QR return target으로 허용하고 absolute external URL은 저장·복원하지 않는다.
- EntraId 모드는 MSAL이 제공하는 request-start restoration을 우선 사용하고, Repository에서 부족한 경우 sessionStorage에 path/query/hash만 저장한다.
- 복원 값은 `/q/` prefix와 길이 제한을 검증하고 1회 소비 후 삭제한다. Dev auth mode에서도 같은 route contract를 테스트한다.

### R7. QR image·cache·파일명 계약

- SVG/PNG 응답은 `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`를 사용하고 inline active content 없이 QR module만 생성한다.
- 파일명은 formula/헤더 injection을 막는 server-defined 안전한 `panel-qr-{displayCode}.{ext}` 패턴을 사용하며 customer/project title을 포함하지 않는다.
- SVG/PNG decode test에서 정확히 configured scan origin + `/q/{token}`만 들어 있는지 확인한다.

## 보류

| 항목 | 보류 이유 | 후속 조건 |
| --- | --- | --- |
| QR 현장 부착 완료 상태 | 발급·스캔과 다른 운영 lifecycle이며 IQC 담당·부착 증빙 정책이 필요하다. | 실제 현장 검수 뒤 별도 change/NEW_FEATURE |
| 완료 project QR 일괄 비활성 | Roadmap 8.3의 활성 유지 계약을 바꾼다. | 별도 `POLICY_DECISION` |
| 라벨 template 관리자·프린터 연동 | 다양한 용지·DPI·프린터 운영이 필요해 MVP 비용이 크다. | 현장 출력 요구 수집 뒤 별도 Task |
| open Pending 상세 요약 | landing 핵심 진입을 복잡하게 하고 현재 stage/action만으로 MVP 가치가 충분하다. | QR landing 사용성 검수 뒤 optional P3 |

## 제거

| 항목 | 제거 이유 |
| --- | --- |
| 모든 invalid scan을 append-only QR event로 저장 | 공격자가 random token으로 저장소를 무한 증가시킬 수 있고 사용자 가치가 없다. |
| 서버 event명을 실제 `Downloaded`/`Printed` 완료로 기록 | HTTP 응답과 browser print sheet 생성만으로 실제 저장·인쇄 완료를 증명할 수 없다. |
| 부서만 일치하면 무조건 부서 수정 화면으로 이동 | 현재 workflow stage·담당 권한을 무시해 잘못된 입력을 유도할 수 있다. |
| 미발급 panel의 일괄 인쇄 중 자동 발급 | 명시 발급 actor·시점 원칙과 stale 재검증을 깨뜨린다. |

## 권장 개발 순서

1. `0047` additive migration과 QR domain/token 규칙, partial unique·token unique·event enum
2. backend 발급·조회·rotation·resolve와 project scope/상태 matrix, 동시성·감사 테스트
3. QR SVG/PNG renderer와 decode·header·payload 테스트
4. 패널 목록/상세의 발급·preview·선택 인쇄 UX
5. `/q/{token}` 인증 복귀·mobile landing·assignment-aware primary action
6. desktop/390px isolated E2E, fresh/existing migration, 전체 영향 회귀, privacy-safe screenshot

## 2차 기획 Resolution

Fable 2차 기획은 다음을 authoritative하게 통합해야 한다.

1. 명시 발급·동일 active QR·anonymous API 없음·모바일 landing은 유지한다.
2. 256-bit opaque public token은 원문 저장하되 log/audit/error에는 절대 남기지 않고 인증수단으로 사용하지 않는다.
3. QR audit은 `Issued/Rotated/ImageRendered/PrintSheetRendered/ResolveSucceeded`와 유효 token의 bounded 상태 조회만 기록하며 unknown token은 저장하지 않는다.
4. primary action은 current stage 담당 부서·사용자 부서·기존 write policy가 모두 맞을 때만 수정 화면이고 나머지는 종합현황이다.
5. deleted/revoked/restricted 상태에서 panel/project identity 노출을 제한한다.
6. 선택 인쇄는 같은 project·기발급 active QR 최대 50개, stale 발견 시 전체 실패다.
7. return path는 same-origin `/q/` prefix·길이 제한·1회 소비로 고정한다.
8. QR image는 no-store/nosniff·안전한 파일명과 exact payload decode test를 갖춘다.
9. 현장 부착 상태·완료 후 폐기·label template/프린터·Pending 상세는 보류한다.

- reviewStatus: `COMPLETED`
- userDecisionRequiredCount: 0
- openBlockingDecisionCount: 0
- recommendedDisposition: `FABLE_SECOND_PLANNING`
