# TASK-UL891-SET-001 구현 보고서

## 1. 종료 상태

- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- branch: `experiment/task-home-002-personalized-shell`
- implementationStatus: `EXPERIMENT_COMPLETE`
- userValidationStatus: `PENDING — 고정 실험 runtime에서 사용자 직접 검수`
- finalPlanningSource: `docs/41-ul891-panel-set-plan.md`
- localCommitApproved: `true`
- pushApproved / prApproved / mergeApproved: `false / false / false`
- mainMergeApprovalCount: `0/3`
- representativeRepoMainChanged: `false`
- persistentUatChanged: `false`
- actualProviderCalled: `false`

## 2. 구현 결과

### 2.1 세트 주문과 개별 패널

- 신규 UL891 프로젝트는 `세트 사양 → 주문 세트 인스턴스 → 개별 물리 패널` 계층으로 생성한다.
- 같은 사양을 여러 세트 주문하면 A~G 등 구성 code의 패널명·규격은 version에 한 번 저장하고 각 실물 세트가 snapshot으로 참조한다.
- 프로젝트 생성 뒤에도 영업이 새 세트 사양·수량·구성 code를 추가할 수 있다. 기존 spec·instance·panel 번호는 유지하고 새 번호를 뒤에 이어 만든다.
- 사양별 수량 증가는 Published version에서 새 instance·새 panel ID를 만들고, 수량 감소는 사용자가 instance를 직접 선택한다. 취소 번호와 panel ID는 재사용하지 않는다.
- 납품 패널을 포함한 instance는 취소할 수 없고, 이미 착수한 instance는 사유와 예외 확인을 요구한다.
- 발주일이 입력된 구매품목이 있는 instance 취소는 관련 품목을 선택하게 하고 `BillingRequired → AppliedToRequest → InvoiceConfirmed → Recovered` 회수 사례를 만든다.

### 2.2 설계 version과 실행 단위

- 사양 version은 `Draft → Published → Superseded`로 관리한다. Published 구성은 DB guard로 불변이며 새 변경은 새 Draft에서 수행한다.
- 설계는 구성 code별 패널명·규격·치수를 입력한다. 목포장은 세 치수를 모두 입력해야 publish할 수 있다.
- UL891 패널은 소속 사양이 Published 상태가 아니면 제조 시작을 서버에서 차단한다.
- 제조·키팅·LQC·OQC·전진검수·FAT·QR·Packing Unit·부분출하는 기존 개별 `panel_placeholder`를 실행 원자로 유지한다.
- 프로젝트 상세에는 세트 aggregate를, 패널 상세에는 정확한 SET 번호·실물 세트 번호·사양 version·구성 code·공통 규격을 표시한다.

### 2.3 월별 부분출하 발행요청과 완료 gate

- 세금계산서 발행요청은 프로젝트×출하 달력월(Asia/Seoul 1일~말일)별 canonical ledger로 관리한다.
- 같은 월의 출하 패널과 Packing Unit·출발일을 revision snapshot에 보존하고, 회계 확인 뒤 추가 근거가 생기면 `AdjustmentRequired`를 계산한다.
- 영업이 요청 금액을 직접 입력하며 모든 월의 최신 revision 합계가 프로젝트 판매액을 초과하지 않도록 project row lock 아래 차단한다.
- 화면은 판매액·현재 요청 합계·잔액, 월별 출하 근거, 회계 확인과 발주 취소 회수 상태를 함께 표시한다.
- UL891 완료는 active 패널 전량 납품, Open Pending 0, 모든 출하 월의 최신 ledger 회계 확인, 회수 사례 전부 Recovered, 확인 금액 합계=프로젝트 판매액을 모두 요구한다.
- 기존 UL891 평면 프로젝트와 비-UL891 프로젝트는 `FlatPanel`로 유지하고 기존 반월 발행요청·정산 경로를 그대로 사용한다.

### 2.4 동시성·감사·삭제

- migration `0053_ul891_panel_sets_monthly_billing.sql`은 nullable/additive schema, FK·unique·check, Published/revision/operation append-only guard를 추가한다.
- mutation은 project/spec row lock, expected count/version, operationId fingerprint receipt와 감사 이벤트를 사용한다.
- 같은 operationId의 같은 payload는 replay하고 다른 payload는 conflict로 차단한다.
- 프로젝트 purge transaction은 신규 세트·청구·회수 원장을 FK 역순으로 정리하며 일반 UPDATE/DELETE는 guard가 차단한다.

## 3. 사용자 확정 정책 반영표

| 정책 | 구현 |
| --- | --- |
| 같은 세트 사양 반복 주문은 동일 이름·규격 | spec version 구성 1회 입력 + instance snapshot |
| 제조·검사·FAT·QR은 개별 패널 | 기존 panel ID 실행 원자 유지 |
| 세트 일부 출하 허용 | 개별 패널 subset을 기존 Packing Unit에 포함 |
| 발주 뒤 수량 감소도 고객 회수 추적 | ordered 품목 선택 + recovery lifecycle |
| 전진검수/FAT는 개별 패널 | 기존 inspection target 유지 |
| 가격·납기는 프로젝트 기준, 출하일은 달라질 수 있음 | 프로젝트 판매액·납기일 유지 + 패널별 출하 월 projection |
| 부분출하 발행요청은 월 1일~말일 | project×calendar month ledger |
| 수량 감소·진행 예외·납품 차단·ID 결번 | instance 선택 취소와 lifecycle fence |
| 사양 version·납품 snapshot 불변 | Draft/Published/Superseded + DB guard |

## 4. 주요 변경 파일

- DB: `database/migrations/0053_ul891_panel_sets_monthly_billing.sql`
- Backend: `backend/src/Emi.Qms.Api/Ul891Sets/*`, 프로젝트 생성·제조 readiness·영업 정산 gate 연계
- Frontend: `frontend/src/Ul891SetWorkspace.tsx`, `frontend/src/ul891Sets.ts`, 프로젝트 등록·상세·패널 상세·API·적응형 CSS 연계
- Tests: `backend/tests/Emi.Qms.Api.Tests/Ul891SetApiTests.cs`, migration ledger 기대값, `frontend/tests/Ul891SetWorkspace.test.tsx`

## 5. 자동 검증

| 검증 | 결과 |
| --- | --- |
| Backend build | PASS — 경고 0, 오류 0 |
| UL891 + 관련 migration 회귀 | PASS — 5/5 |
| Frontend UL891 unit | PASS — 3/3 |
| Frontend task lint | PASS — 오류·경고 0 |
| Frontend production build | PASS — build 성공, 기존 chunk-size warning만 유지 |
| Frontend 전체 unit | PASS — 18 files, 122/122 |
| Backend 전체 test | PASS — 420/420 |
| 고정 runtime health | PASS — Frontend 200, Backend ready/database reachable |
| 실제 UI/API UAT | PASS — 신규 프로젝트, 3개 사양·6세트·38패널, Published 2·Draft 1, 새 사양 추가 성공 |

## 6. 화면 증빙

- `tasks/ul891-set-001-screenshots/01-project-create-desktop.png` — UL891 여러 세트 사양 프로젝트 등록
- `tasks/ul891-set-001-screenshots/02-sales-sets-desktop.png` — 세트별 실물 패널 영업 화면
- `tasks/ul891-set-001-screenshots/03-monthly-billing-desktop.png` — 월별 부분출하 발행요청·판매액·잔액
- `tasks/ul891-set-001-screenshots/04-design-published-desktop.png` — Published 공통 사양과 instance별 panel
- `tasks/ul891-set-001-screenshots/05-panel-detail-desktop.png` — 개별 패널의 SET/instance/version context
- `tasks/ul891-set-001-screenshots/06-design-mobile.png` — 390px 설계 적응형 카드
- `tasks/ul891-set-001-screenshots/07-sales-mobile.png` — 390px 영업 세트 추적
- `tasks/ul891-set-001-screenshots/08-add-set-spec-desktop.png` — 프로젝트 생성 뒤 새 세트 사양 추가

## 7. Finding·차이·후속

- Open P0/P1/P2: `0/0/0`
- P3: 기존 단일 frontend bundle이 500 kB를 넘는 Vite warning은 유지한다. 이 Task에서 route splitting을 확대하지 않았다.
- Fable final plan의 신규 알림 종류·실제 회계/ERP provider·비-UL891 월별 청구 전환은 명시적 제외를 유지했다.
- 실제 provider, Persistent UAT, 대표 repo, `main`, push·PR·merge는 변경하지 않았다.

## 8. Fable 사용량과 cleanup

| 시점 | 5시간 세션 | 주간 전체 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 직전 | 0% 사용 / 100% 잔여 | 34% / 66% | 68% / 32% |
| 1차 직후 | 0% 사용 / 100% 잔여 | 35% / 65% | 69% / 31% |
| 2차 직전 | 11% 사용 / 89% 잔여 | 35% / 65% | 69% / 31% |
| 2차 직후 | 11% 사용 / 89% 잔여 | 35% / 65% | 69% / 31% |

- cleanup: `FABLE_TASK_SESSION_CLEANED`
- sessionsRemoved: `2`
- transcriptsRemoved: `2`

## 9. Rollback

- local experiment commit을 revert하고 runtime을 이전 reachable commit으로 되돌린다.
- migration `0053`은 적용 뒤 down migration을 제공하지 않는다. Persistent UAT에는 적용하지 않았으며 승격 시 fresh backup·isolated rehearsal·forward fix 원칙을 사용한다.
