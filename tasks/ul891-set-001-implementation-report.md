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

## 10. Change 002 — 패널 상세 업무 허브 완성 (2026-07-22)

### 10.1 구현 결과

- 프로젝트 상세의 9개 탭과 조회 중심 구조는 변경하지 않았다. 복잡한 입력은 기존 별도 편집·업무 화면을 계속 authoritative source로 사용한다.
- 패널 상세를 `요약·설계·자재/키팅·제조·품질·물류·QR/이력` 7개 탭의 패널 중심 업무 허브로 확장했다.
- 기존 자재·제조·품질·물류 조회 API를 합성하되 패널 ID 또는 패널 display code로 직접 연결된 record만 표시한다. 한 부서 조회가 실패해도 다른 탭은 계속 열린다.
- 구매품목·자재 입고와 IQC처럼 현재 패널/BOM 귀속이 없는 데이터는 `프로젝트 공통`으로 명시하고 임의의 패널 데이터처럼 표시하지 않는다.
- 키팅·제조·LQC/OQC/입회/FAT·포장/출발/납품 업무 버튼은 기존 workspace에 `projectId + panelId + stage`를 전달한다. 권한·validation·mutation은 기존 Backend와 업무 화면을 그대로 사용한다.
- 패널 상세 route의 선택 탭을 `?tab=`에 보존해 새로고침 뒤에도 같은 패널 문맥을 유지한다.
- 기존 QR manager에 `focusPanelId` projection을 추가해 패널 상세에서는 선택한 패널 한 대만 표시하고, 프로젝트 설계 탭의 전체 QR 동작은 유지한다.

### 10.2 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | PASS |
| Frontend lint | PASS — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend 전체 unit | PASS — 19 files, `124/124` |
| 신규 패널 업무 허브 unit | PASS — 패널별 키팅·제조 projection, 프로젝트 공통 표시, exact panel deep link |
| Frontend production build | PASS — 기존 chunk-size warning 유지 |
| 기존 18단계 Full-Stack E2E | BLOCKED_BY_TEST_DRIFT — 현재 선택형 키팅 정책과 달리 `키팅 완료 즉시 제조 업무 생성`을 기대해 프로젝트 상세 진척 assertion 전에 중단 |
| 고정 runtime | PASS — Frontend `42983` 200, Backend `41166` ready/database reachable |
| 실제 desktop UI | PASS — synthetic UL891 3사양·6세트·38패널의 P01에서 요약·자재/키팅·QR/이력 탭과 단일 패널 QR projection 확인 |
| `git diff --check` | PASS |

첫 전체 unit 실행에서 기존 관리자 장기 테스트 1건이 5초 timeout에 한 번 걸렸으나, 해당 테스트 단독 재실행과 전체 suite 재실행이 모두 통과했다. 제품 코드 회귀로 재현되지 않았다.

### 10.3 Finding·경계

- Open P0/P1/P2: `0/0/0`
- 신규 migration·Backend API·권한·상태·알림·외부 provider는 추가하지 않았다.
- 대표 repo·`main`·Persistent UAT·push·PR·merge는 변경하지 않았다. main merge 승인 `0/3`을 유지한다.
- 현재 worktree에는 Change 002 시작 전부터 다수의 미커밋 사용자/기존 Task 변경이 겹쳐 있다. `App.tsx`·`styles.css`·`App.test.tsx`의 타 변경을 함께 커밋하지 않기 위해 이번 변경은 자동 commit하지 않았다.

### 10.4 사용자 검수

상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

- [x] P01 요약에서 설계·키팅·제조·품질·물류·QR 상태 구분
- [x] 자재 탭에서 프로젝트 공통 구매/입고와 패널 직접 키팅 구분
- [x] QR 탭에서 다른 37개 패널을 제외하고 P01만 표시
- [x] 키팅 업무 이동 URL에 프로젝트와 패널 ID 보존
- [ ] 사용자가 390px 실제 기기에서 7개 탭과 2열 상태 카드 밀도 확인
- [ ] 각 부서 담당 계정으로 제조·품질·물류 exact target 이동 최종 확인

## 11. Change 003 — 프로젝트 상세 부서 탭 재구성 (2026-07-22)

### 11.1 구현 결과

1. 영업 탭에서 상단 프로젝트 기본정보와 중복되던 고객사·Item·PJT Code·납기일 등의 입력 요약을 제거했다. 세트 주문 관리와 정산·월별 발행요청은 고유 업무이므로 유지했다.
2. 프로젝트 상세 탭을 `전체 흐름 → 생산관리 → 설계 → 구매 → 제조 → 품질 → 물류 → 영업`으로 재배치했다. 영업 탭은 유효 부서가 영업팀인 사용자에게만 마지막에 표시하며, 비영업 사용자의 직접 `?section=sales` 접근은 전체 흐름으로 정규화한다.
3. 프로젝트 상세의 자재 탭을 제거했다. 기존 자재 독립 업무·패널 상세 자재/키팅·서버 권한은 유지하고, 이전 `?section=materials` 링크는 구매로 정규화한다.
4. 구매 탭은 각 구매품목의 자재 결과를 `입고 확정` 또는 `미확정`으로만 보여 준다. 입고 회차·IQC·Pending·키팅 상세는 복제하지 않는다.
5. 제조·품질·물류 탭을 활성 패널 전체의 현황 표/모바일 카드로 교체했다. 기록이 없는 패널도 `미시작`으로 포함하고, 차단/Pending을 우선해 현재 단계를 계산한다.
6. 각 패널 행/카드는 패널 상세의 정확한 `?tab=manufacturing|quality|logistics`로 연결한다. 기존 담당자 mutation workspace와 Backend 권한 계약은 변경하지 않았다.

### 11.2 검증

| 검증 | 결과 |
| --- | --- |
| Frontend typecheck | PASS |
| Frontend lint | PASS — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend 전체 unit | PASS — 19 files, `125/125` |
| 신규 프로젝트 상세 unit | PASS — 탭 순서·영업 부서 제한·legacy query 정규화·구매 입고확정·3개 패널 deep link |
| Frontend production build | PASS — 기존 chunk-size warning 유지 |
| 영향 Full-Stack E2E | PASS — 격리 PostgreSQL에서 구매·자재 추적 `1/1`, IQC Pending·재검사·프로젝트 탭 연속성 `1/1` |
| 고정 runtime | PASS — Frontend `42983` 200, Backend `41166` process 유지 |
| 실제 desktop UI | PASS — 영업 사용자 8개 탭, 구매 입고확정, 제조·품질·물류 각 38개 패널과 P01 exact tab deep link 확인 |
| 실제 비영업 UI | PASS — `dev-design`에서 영업·자재 탭 미노출, 직접 영업 query가 전체 흐름으로 정규화 |
| 실제 390px UI | PASS — 제조·품질·물류가 표 축소본이 아닌 패널 카드 projection으로 표시 |

### 11.3 Finding·경계

- Open P0/P1/P2: `0/0/0`
- P3: 기존 frontend 단일 bundle의 500 kB 초과 warning은 유지한다.
- 격리 Full-Stack 첫 실행에서 부분 일치 탭 selector와 이전 IQC 재검사 UI 기대값이 실패했다. 제품 결함이 아니라 현재 `부적합 항목만 재검사` 계약과 어긋난 테스트를 exact selector·1개 항목·전용 재검사 완료 동작으로 갱신한 뒤 두 흐름을 모두 통과했다.
- Backend API·DB·migration·workflow·알림·mutation 권한은 변경하지 않았다.
- 대표 repo·`main`·Persistent UAT·push·PR·merge는 변경하지 않았다. main merge 승인 `0/3`을 유지한다.
- 현재 worktree에는 Change 003 시작 전부터 `App.tsx`·`styles.css`·tests를 포함한 미커밋 변경이 겹쳐 있어 자동 commit하지 않는다.

### 11.4 사용자 검수

상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

- [x] 영업 사용자 탭 순서와 영업 탭 마지막 배치
- [x] 비영업 사용자 영업 탭 차단과 legacy query 정규화
- [x] 구매품목 입고확정/미확정 단순 표시
- [x] 제조·품질·물류 38패널 목록과 exact panel detail tab 연결
- [x] 390px 전용 패널 카드 구조
- [ ] 사용자가 실제 업무 데이터의 상태 문구와 정보 밀도 최종 확인

## 12. Change 004 — 프로젝트 상세 패널 진척률·부서 KPI (2026-07-22)

### 12.1 구현 결과

1. 제조·품질·물류의 desktop 패널 목록을 `No · 패널명 · 핵심정보 · 진행률` 네 열로 통일했다. 진행률은 숫자 `%`와 상태 tone을 채운 사각형 막대를 함께 표시하고 패널 상세 exact tab 연결을 유지했다.
2. 제조는 시작된 실행의 고정 단계 수, 미착수 패널은 활성 제조 양식 단계 수를 분모로 사용한다. KPI는 `착수 대기 · 제조 중 · 중단 · 완료 · 진행률`이며 착수 대기는 제조 전 활성 패널 전체를 포함한다.
3. 품질은 패널별 `OQC Check 항목 + 전진검수 1 + 선택 FAT 1`을 분모로 계산한다. LQC는 별도 완료 KPI이고 진척률 분모에는 넣지 않는다. FAT 비필수 프로젝트는 `FAT 완료 없음`으로 표시한다.
4. 물류는 패널별 `포장 · 출발 · 납품` 3단계를 각각 1로 계산하고, 확정 이력의 패널 code를 중복 제거해 단계별 완료 면수를 집계한다.
5. Project detail API에 활성 제조 단계 수와 활성 OQC Check 항목 수를 추가했다. 실행·검사가 시작된 패널은 해당 회차에 고정된 실제 단계/항목 수를 우선 사용해 이후 양식 version 변경으로 과거 진척이 흔들리지 않게 했다.
6. 390px에서는 표 축소본 대신 패널명·핵심정보·진척률을 한 장에 보여 주는 전용 카드 projection을 유지했다.

### 12.2 검증

| 검증 | 결과 |
| --- | --- |
| Backend project detail 계약 | PASS — 제조/OQC 활성 단계 수 `4/4` |
| Backend 전체 test | PASS — `420/420` |
| Frontend 대상 unit | PASS — 제조·품질·물류 exact header, KPI, 부분 진척 계산 |
| Frontend 전체 unit | PASS — `19 files, 125/125` |
| Frontend typecheck | PASS |
| Frontend lint | PASS — error 0, 기존 `main.tsx` Fast Refresh warning 1 |
| Frontend production build | PASS — 기존 chunk-size warning 유지 |
| 고정 runtime | PASS — Frontend `42983` 200, Backend `41166` ready/database reachable |
| 실제 desktop UI | PASS — 38면 FAT 대상 프로젝트에서 제조 `0/4`, 품질 `0/6`, 물류 `0/3`과 5개/4개 KPI 확인 |
| 실제 390px UI | PASS — 전용 카드, 진행 막대, horizontal overflow 0 |

### 12.3 Finding·경계

- Change 004 자체 Open P0/P1/P2: `0/0/0`.
- 품질 진척 계산은 사용자가 확정한 단위를 반영했지만 현재 전진검수·FAT 입력 화면과 Backend는 여전히 여러 체크항목을 요구한다. 입력 모델 보정은 [TASK-012A Change 003](012a-change-003.md)의 `012A-AGGREGATE-DECISION` OPEN P2이며 이 Change의 읽기 전용 현황판 범위 밖이다.
- 기존 전체 lifecycle E2E는 구매 편집기의 현재 빈 상태 구조까지 보정한 뒤 구매·IQC·키팅을 통과했으나, 과거 `키팅 완료 → 제조 업무 자동 생성` 정책 문구에서 다시 중단됐다. 제품은 현재 `키팅=선택 알림`, `생산관리 제조 투입 요청=제조 업무 생성` 계약이므로 테스트 시나리오 갱신이 필요하다. 격리 DB·컨테이너는 두 실행 모두 자동 삭제됐다.
- 대표 repo·`main`·Persistent UAT·push·PR·merge는 변경하지 않았다. main merge 승인 `0/3`을 유지한다.
- 여러 기존 Task의 미커밋 변경이 같은 worktree와 `App.tsx`·`styles.css` 등에 겹쳐 있어 자동 commit하지 않았다.

### 12.4 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 12장 | 작성 완료 |
| SOP | 이 문서 계산 규칙 + Change 004 계약 | 검수용 완료 |
| User manual | 사용자 검수 체크리스트 Change 004 | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` Decision Log | 반영 완료 |
| User validation checklist | `tasks/ul891-set-001-user-validation-checklist.md` | 사용자 검수 대기 |

## 13. Change 007 — UL891 설계 조회·수정 화면 분리 (2026-07-27)

- 신규 UL891 프로젝트 상세 설계 탭은 세트 공통 사양·저장 version·실물 세트·개별 패널·QR을 조회만 한다.
- 중복되던 일반 평면 패널 설계 영역은 UL891 상세에서 제거하고, 권한이 있는 사용자는 단일 `수정` 버튼으로 별도 전체 폭 입력 화면에 진입한다.
- 사용자 표시 명칭은 `임시저장`·`저장`·`새 수정본 만들기`·`저장된 버전`으로 정리하고 해당 action 바로 아래에 실행 결과를 표시한다.
- 내부 Draft/Published 계약, Backend·DB·제조 기준·QR 원자와 비-UL891 기존 설계 경로는 변경하지 않았다.
- 상세 구현·검증·SOP·사용자 안내는 [Change 007 구현 보고](ul891-set-001-change-007-implementation-report.md)와 [사용자 검수 체크리스트](ul891-set-001-change-007-user-validation-checklist.md)를 따른다.

## 14. Change 008 — 저장 오류·불필요 규격 제거 (2026-07-27)

- 최종 `저장`은 현재 form의 패널명·치수를 Draft에 먼저 갱신한 뒤 같은 version을 Publish한다.
- Publish와 패널 `설계 입력 완료` 조건에서 사용자 입력값이 아닌 규격을 제외하고 패널명·포장방식별 치수 기준으로 통일했다.
- UL891 조회·수정·패널 세트 문맥의 규격 표시·입력칸을 제거하되 기존 API·DB 호환 필드는 유지한다.
- 상세 구현·검증·SOP·사용자 안내는 [Change 008 구현 보고](ul891-set-001-change-008-implementation-report.md)와 [사용자 검수 체크리스트](ul891-set-001-change-008-user-validation-checklist.md)를 따른다.
