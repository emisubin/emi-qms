# TASK-008A 자재 도착·분할 입고 구현 보고

## 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-008a-material-receiving`
- implementation: `완료`
- automaticValidation: `완료`
- userValidation: `대기`
- commit: `완료 — local experiment commit`
- push / PR / merge: `미승인·미실행`
- main merge approval: `0/3`
- Persistent UAT / provider / 대표 repo 영향: `없음`

## Task 목적·배경

구매품목의 `receipt_completed` boolean 하나로 표현하던 입고를 도착 건 단위의 forward-only 흐름으로 일반화했다. 자재 담당은 분할 도착을 등록하고 IQC를 요청하며, 품질 담당은 최소 합격·부적합을 판정한다. 부적합은 기존 Pending에 원자적으로 연결하고, 모든 유효 도착 건이 확정된 뒤 자재 담당이 도착 마감을 실행해야만 기존 완료 projection이 true가 된다.

구현 source of truth는 Fable 2차 기획 [docs/14-material-receiving-plan.md](../docs/14-material-receiving-plan.md)와 Codex review [008a-review.md](008a-review.md)다. 1차 Fable 원문은 [008a-planning.md](008a-planning.md)에 보존했다.

## 포함·제외 범위

포함:

- 주문 수량·단위 pair, 분할 도착 등록·Arrived 취소·도착 마감
- 도착 건별 IQC 요청·재검사·최소 합격/부적합 판정·입고 확정
- IQC attempt별 Pending 연결, 반복 부적합 이력, 재검사 합격과 Pending 종결의 단일 transaction
- IQC·입고 확정 내 업무와 deep link, 기존 완료값 derived projection
- additive `0030` migration, legacy 완료 건 backfill, direct writer 차단
- `/materials/receipts`, `/quality/iqc`의 desktop·모바일 적응형 UI

제외:

- 상세 IQC 체크리스트·사진·PDF(`TASK-009A`), 키팅(`TASK-010A`), 사급(`TASK-008B`)
- 구매 Excel 주문 수량·단위 열, reverse transition, 실제 Teams/Mail/Activity 발송
- Persistent UAT migration·runtime handover, push·PR·merge, 대표 repo 변경

## 전체 아키텍처와 영향

### DB·Migration

- `0030_material_receiving_iqc.sql`은 기존 migration을 수정하지 않는 additive migration이다.
- 구매품목에 주문 수량·단위와 도착 마감 actor/time을 추가했다.
- `material_receipts`, `material_iqc_attempts`, `material_receipt_events`를 추가하고 수량·단위 pair, 상태, version, 판정 actor/time, 취소 사유와 open attempt uniqueness를 DB constraint로 고정했다.
- 기존 완료 품목은 `is_legacy=true`, `Confirmed` 도착 건과 event로 idempotent backfill한다.
- `receipt_completed` 직접 변경은 DB trigger가 차단하고 Materials transaction의 local setting에서만 derived projection을 갱신한다.
- rollback은 destructive down migration이 아니라 신규 forward-fix migration으로 수행한다. Persistent DB 적용 전에는 backup·restore rehearsal과 별도 승인이 필요하다.

### Backend·API·권한·Workflow

- `MaterialsStore`가 품목 row lock과 transaction을 소유한다. 도착 수량 합, IQC 판정, Pending·work item·event, derived 완료를 한 transaction 경계에서 처리한다.
- 동시 분할 도착은 품목 row lock 뒤 기존 유효 수량을 다시 합산해 발주 수량 초과를 차단한다.
- `PendingStore`에 동일 connection/transaction을 받는 자재 부적합 생성·종결 helper를 추가했다.
- 자재 API는 조회, 도착 등록·취소, IQC 요청·재검사, 입고 확정, 도착 마감을 제공한다. 품질 API는 IQC queue와 판정을 제공한다.
- 자재 mutation은 기존 `MaterialReceiptUpdate`, 품질 조회·판정은 신규 `QualityInspect` policy를 사용한다. System Administrator 우회 권한은 추가하지 않았다.
- IQC 업무는 `Inspection`, 입고 확정 업무는 `ProcurementItem` target과 도착 건별 idempotency key를 사용한다. 실제 delivery/provider row는 만들지 않는다.

### Frontend·UI/UX

- `/materials/receipts`는 요약, 검색, 품목 카드, 도착 건 단계와 상태별 action을 제공한다.
- `/quality/iqc`는 품질 담당의 검사 queue와 합격·부적합 판정 sheet를 제공한다.
- 390px에서는 표를 축소하지 않고 전용 header, 가로 요약 rail, 카드, bottom sheet, 하단 공통 메뉴로 재구성한다.
- 기존 구매 편집 화면의 입고 완료 checkbox는 derived badge로 바꾸고 완료 mutation 필드는 전송하지 않는다.
- 모바일 적응형 전환에 맞춰 기존 E2E의 상태 sheet 계정 전환, 모바일 제목, 검색·필터 sheet와 뒤로가기 계약을 갱신했다.

### Excel·PDF·첨부·기존 회귀

- Excel: 새 열은 추가하지 않았다. 기존 Excel 또는 direct PATCH가 입고 완료를 변경하려 하면 validation error로 차단한다.
- PDF·첨부: `N/A` — 이번 최소 IQC 범위에 파일 저장·생성 기능이 없다.
- 기존 회귀: 구매 목록·dashboard·Excel, Pending, 프로젝트 desktop/mobile, 생산계획과 홈 경로를 전체 E2E로 확인했다.

## 실제 변경 파일과 역할

- DB: `database/migrations/0030_material_receiving_iqc.sql`
- Backend 신규: `backend/src/Emi.Qms.Api/Materials/MaterialsContracts.cs`, `MaterialsEndpointExtensions.cs`, `MaterialsStore.cs`
- Backend 연동: authorization policy, `Program.cs`, `PendingStore.cs`, Procurement endpoint/store
- Frontend 신규: `frontend/src/materials.ts`, `MaterialsWorkspace.tsx`
- Frontend 연동: `App.tsx`, `api.ts`, `projects.ts`, `styles.css`
- Tests: `PostgreSqlMigrationTests.cs`, `ProcurementApiTests.cs`, `App.test.tsx`, full-stack E2E 4개 spec
- 기획·검토: interview·Fable raw round·1차 planning·Codex review·Change 001·Fable 2차 planning
- 증빙: `tasks/008a-screenshots/*.jpg`, 이 보고서와 user validation checklist

## 실행한 자동 테스트와 결과

- Backend targeted `ProcurementApiTests.MaterialReceipt_*`: `3/3 PASS`
- Backend 전체: `370/370 PASS`(4분 23초)
- Backend Release build: `PASS`, warning 0 / error 0
- Frontend typecheck: `PASS`
- Frontend lint: `PASS`(error 0, 기존 `main.tsx` Fast Refresh warning 1)
- Frontend unit: `76/76 PASS`
- Frontend production build: `PASS`(기존 대형 chunk warning)
- Isolated Full-Stack E2E: `23/23 PASS`, 전용 PostgreSQL DB·tmpfs 사용 후 drop/compose cleanup 완료
- Browser visual QA: 자재·IQC 각 desktop/mobile 4장 확인, API/DB reachable, synthetic data only
- `git diff --check`: `PASS`

미실행:

- Persistent UAT migration·runtime·사용자 계정 검증: 승인 범위 밖이므로 미실행
- 실제 Teams/Mail/Activity 발송: 기능 제외이며 provider disabled
- CI·GitHub PR: push·PR 미승인으로 미실행
- 사용자 직접 action 검수: 스크린샷 handoff 후 사용자 검수 대기

## 개인정보·secret 검토

- 스크린샷과 E2E는 합성 프로젝트·업체·역할 계정만 사용했다.
- Persistent UAT, 실제 고객·사용자·알림 원문을 읽거나 기록하지 않았다.
- tracked diff에는 credential, token, private key, tenant/client/object ID를 추가하지 않았다.
- API·DB 검증 결과는 count, status, synthetic identifier로 제한했다.

## Finding gate

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `008A-SINGLE-TRUTH-WRITERS` | P1 | `RESOLVED` | 기존 writer가 완료값을 바꾸면 신규 상태와 충돌 | API validation + store validation + DB trigger로 차단 |
| `008A-ATOMIC-INTEGRATION` | P1 | `RESOLVED` | Pending/업무가 별도 transaction이면 반쪽 상태 가능 | Materials transaction owner와 transaction-aware Pending helper 적용 |
| `008A-REPEAT-NONCONFORMANCE` | P2 | `RESOLVED` | 단일 Pending FK는 반복 cycle 손실 | IQC attempt별 nullable Pending 참조 |
| `008A-PENDING-CLOSURE` | P2 | `RESOLVED` | 재검사 합격 뒤 open Pending 모순 | 합격과 Pending Closed를 원자 처리 |
| `008A-QUANTITY-UNIT` | P2 | `RESOLVED` | pair·정밀도·단위 혼합 불명확 | numeric(18,3), pair constraint, 품목 단위 상속 |
| `008A-BACKWARD-TRANSITIONS` | P2 | `RESOLVED` | reverse action은 감사 이력 훼손 | Arrived 취소 외 reverse transition 제거 |
| `008A-WORK-ITEM-TARGET` | P2 | `RESOLVED` | 분할 건 업무가 서로 덮일 위험 | Inspection/ProcurementItem target + receipt idempotency |
| `008A-OVER-RECEIPT-RACE` | P1 | `RESOLVED` | 초기 구현에 누적 도착 수량 상한 검사가 빠짐 | 품목 row lock 후 합산 검증과 동시 경쟁 test 추가 |
| `008A-MOBILE-E2E-CONTRACT` | P2 | `RESOLVED` | 기존 E2E가 새 상태 sheet·모바일 제목·filter sheet를 인식하지 못함 | 모바일 전용 helper와 adaptive accessible-name 계약 갱신 |
| `008A-SCREENSHOT-PORT-COLLISION` | P2 | `RESOLVED` | 최초 5092가 대표 repo의 기존 Review-safe runtime과 충돌 | 423 fail-closed로 mutation 0을 확인하고 전용 5093/5186 isolated runtime으로 전환 |
| `008A-FABLE-ROUND-NUMBER` | P3 | `RESOLVED` | 2차 Fable 첫 시도에서 round 번호 계약 불일치 | runner가 fail-closed, 잘못된 artifact 미생성 후 올바른 round로 재호출 |

Open P0/P1/P2/P3: `0/0/0/0`.

## Fable 사용량

Claude `/usage` 정수 반올림 기준이다.

| 시점 | 전체 사용/잔여 | Fable 사용/잔여 |
| --- | --- | --- |
| 최초 호출 전 | 10% / 90% | 19% / 81% |
| 1차 기획 직전 | 10% / 90% | 19% / 81% |
| 1차 기획 직후 | 13% / 87% | 25% / 75% |
| 2차 기획 직전 | 13% / 87% | 25% / 75% |
| 2차 기획 직후 | 13% / 87% | 25% / 75% |

2차 호출의 증가는 정수 반올림 구간 안이라 표시 퍼센트가 변하지 않았다.

## 운영 SOP — 실험 검수용

1. 이 branch를 isolated DB와 external provider disabled 상태에서 실행한다.
2. 자재 담당으로 자재 입고 페이지에서 첫 도착의 발주 수량·단위와 도착 수량을 입력한다.
3. IQC 요청 후 품질 담당이 `/quality/iqc`에서 합격 또는 부적합과 사유를 기록한다.
4. 부적합이면 연결 Pending을 배정·조치하고 `ReinspectionRequested`로 전이한 뒤 자재 담당이 재검사를 요청한다.
5. 합격 도착분을 자재 담당이 확정한다. 모든 도착분이 확정/취소 상태일 때 도착 마감을 실행한다.
6. 충돌 시 최신 목록을 다시 불러오고 action을 재시도한다. 완료 boolean을 구매/Excel/legacy API에서 직접 바꾸지 않는다.
7. 운영 적용 전 backup·restore rehearsal, migration 0030 dry run, worker/provider gate와 별도 사용자 승인을 수행한다.

## User manual — 역할별 사용법

- 자재 담당: `자재` 메뉴 → 품목 카드의 `+ 도착 등록` → 수량·단위·날짜 저장 → 도착 건을 열어 `IQC 요청` → 합격 뒤 `입고 확정` → 모든 건 처리 뒤 `입고 마감`.
- 품질 담당: `IQC` 메뉴 → 검사 대기 카드 선택 → 합격/부적합과 3자 이상의 사유 입력. 부적합이면 생성된 Pending에서 조치 흐름을 확인한다.
- 생산관리/Pending 담당: 자동 생성된 Urgent Pending을 배정하고 조치한 뒤 재검사 요청 상태로 전환한다. 자재 상태를 직접 변경하지 않는다.
- 모바일: 상단 `상태`에서 개발 계정을 전환하고 하단 메뉴로 이동한다. 상세 입력·판정은 bottom sheet에서 수행한다.
- 오류 복구: “다른 사용자가 먼저 변경”이면 sheet를 닫고 새로고침한다. 초과 수량은 발주 수량과 기존 도착분을 확인하고 올바른 수량으로 다시 입력한다.

## Rollback·복구·forward-fix

- local code는 이 experiment commit을 역참조하거나 후속 commit으로 보정할 수 있다. main에는 반영되지 않는다.
- migration 0030을 Persistent DB에 적용한 뒤 table/column을 drop하는 down rollback은 수행하지 않는다. 문제 시 write를 중단하고 backup에서 isolated 복구 검증 후 additive forward-fix migration을 작성한다.
- derived projection 문제가 생기면 direct boolean write를 다시 열지 않고 receipt/attempt/event 원장을 기준으로 forward-fix한다.

## 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | 이 문서 `운영 SOP — 실험 검수용` | 실험 검수용 완료, 운영 handover 미승인 |
| User manual | 이 문서 `User manual — 역할별 사용법` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` TASK-008A section | experiment 구현·검수 대기 기록 완료, canonical queue 불변 |
| User validation checklist | [008a-user-validation-checklist.md](008a-user-validation-checklist.md) | 자동 검증 완료·사용자 검수 대기 |

## 1. 해결한 업무 문제

완료 checkbox를 수동으로 바꾸던 흐름을 분할 도착·IQC·부적합·재검사·확정의 추적 가능한 원장으로 바꿨다. 현장 사용자는 현재 상태와 다음 action을 모바일 카드에서 바로 확인할 수 있다.

## 2. 기술적 결정과 검토한 대안

- 별도 Pending API self-call 대신 같은 DB transaction의 internal helper를 선택했다.
- 도착 건의 단일 Pending FK 대신 attempt별 참조로 반복 cycle을 보존했다.
- silent ignore 대신 API·DB의 명시적 direct-write 차단을 선택했다.
- 자동 도착 마감 대신 자재 담당의 명시적 forward action을 유지했다.

## 3. 시행착오 및 폐기한 접근

- 1차 기획의 단일 writer·transaction·반복 cycle 누락은 Codex review 뒤 2차 Fable 기획에서 폐기했다.
- 대표 repo의 기존 Review-safe 5092 runtime과 포트가 겹친 첫 screenshot 시도는 423으로 fail-closed 됐고, 전용 5093/5186 isolated runtime으로 전환했다. 기존 process는 변경·종료하지 않았다.
- PC selector를 모바일에서 강제로 조작하던 E2E는 상태 sheet 기반 helper로 교체했다.
- 독립 검토에서 누락된 초과 수량 검사를 발견해 row-lock 합산 검증으로 보정했다.

## 4. 사용자 검수 결과와 남은 항목

- 자동 검증과 visual QA는 완료했다.
- 사용자의 스크린샷·실제 action 검수는 대기 중이다.
- push·PR·merge, Persistent UAT와 실제 provider 검증은 승인 범위 밖이며 미실행이다.
- canonical Roadmap 다음 Gate는 여전히 `TASK-007A` Fable deep-interview다.
