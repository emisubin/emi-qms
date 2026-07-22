# TASK-UL891-SET-001 — Codex 내용·제품 방향 Review

- reviewOwner: `CODEX`
- reviewTarget: `tasks/ul891-set-001-planning.md`
- reviewRound: 1
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- reviewStatus: `RESOLVED_FOR_SECOND_PLANNING`
- openBlockingDecisionCount: 0

## 1. 총평

1차 기획의 중심 방향은 맞다. 특히 `세트 사양/버전 → 주문 인스턴스 → 기존 physical panel` 계층, 기존 P01 식별자 유지, 비-UL891·legacy 무변환, Packing Unit 원자성 보존, 취소 시 발주 회수 사례 생성, UL891에만 월별 복수 청구를 적용하는 경계는 사용자 문제를 직접 해결하면서 기존 제조·품질·QR·물류 회귀 위험을 가장 작게 만든다.

다만 그대로 구현하면 세 가지 의미 충돌이 생긴다. 첫째, 영업이 code만 만들고 설계가 이름·규격을 완성해야 하는데 1차안의 “불변 버전”만으로는 최초 draft를 수정할 수 없다. 둘째, 월 마감 후에만 요청 확정하는 안은 사용자가 설명한 매월 1일·16일 발행 요청 업무를 지나치게 늦춘다. 셋째, “모든 존재 요청 InvoiceConfirmed”만으로는 판매액 일부가 요청되지 않은 채 프로젝트가 완료될 수 있다. 아래 resolution으로 이 세 점을 보정하면 blocking decision 없이 구현 가능하다.

## 2. 유지

### K1. 세트 계층과 기존 패널 실행 원자

- 유지: 프로젝트별 세트 사양, append-only 사양 버전, 세트 주문 인스턴스, 인스턴스×구성 physical panel 연결.
- 근거: 같은 사양의 반복 주문과 개별 패널 실행이라는 두 요구를 동시에 만족한다.
- 구현 지침: 기존 `panel_placeholders.id/sequence_number/display_code`가 QR·제조·품질·물류의 authoritative atom이다. 세트 label은 별도 projection이고 P01…PN을 바꾸지 않는다.

### K2. UL891-only·legacy flat 보존

- 유지: 신규 세트 구조는 새로 set structure를 명시한 UL891 프로젝트만 사용하고 기존 UL891은 `Legacy flat`으로 유지한다.
- 근거: 기존 UL891 패널의 사양/세트 대응을 안전하게 추론할 source가 없다.

### K3. 선택 취소·결번·발주 회수

- 유지: 사용자가 취소할 세트 인스턴스를 선택하고, 납품 포함은 차단, 진행은 사유+예외 확인, 미착수는 허용, 번호는 재사용하지 않는다.
- 유지: 발주일이 입력된 품목은 가격 없이도 품목 단위 회수 사례로 추적한다.
- 보정: 취소 transaction에서 영업이 선택한 발주 품목마다 recovery case 하나를 만들고, 프로젝트에 발주 품목이 있으나 연결 선택이 0이면 취소를 완료하지 말고 선택을 요구한다. 자동 관계가 없는 legacy에서 임의 품목을 자동 연결하지 않는다.

### K4. 부분출하와 Packing Unit

- 유지: 세트와 Packing Unit을 분리하고 eligible panel subset을 기존 Packing Unit에 담는다.
- 근거: 현재 출발 batch의 atomic membership과 증빙 계약을 보존하면서 실제 일부 출하를 표현한다.

### K5. 프로젝트/패널 상세 정보 구조

- 유지: 프로젝트 상세에는 project-scoped aggregate와 모든 세트/패널 링크, 패널 상세에는 panel-scoped 실행 데이터를 둔다.
- 근거: 사용자가 명시한 ownership 경계이며 기존 탭 구조와도 맞는다.

## 3. 추가

### A1. 사양 버전 lifecycle `Draft → Published → Superseded`

1차안의 불변 버전 참조에 아래 lifecycle을 추가한다.

- 영업이 프로젝트 생성 시 사양 v1 `Draft`를 만들고 구성 code·수량을 입력한다.
- 설계는 `Draft`에서 패널명·규격을 채운다. 같은 spec에 Draft는 하나만 허용한다.
- 모든 구성의 필수 설계값이 채워지면 설계 담당자가 `Published`로 확정한다.
- `Published` 구성·버전은 수정하지 않는다. 변경은 다음 `Draft` 버전을 추가한 뒤 publish한다.
- 물리 패널은 Draft 시점에도 생성하되 제조·품질·물류 착수는 소속 version이 Published가 아니면 서버에서 차단한다. 기존 생산계획 조회에는 “설계 사양 미확정” readiness를 표시한다.
- Published 뒤 새 버전을 적용할 때 구성 code 집합이 같으면 인스턴스의 version 참조만 변경한다. code 추가/삭제가 있으면 미착수 인스턴스에 한해 신규 code 패널은 새 ID로 추가하고 제거 code 패널은 Cancelled 처리한다. 진행 인스턴스에는 code 집합이 같은 사양 변경만 사유와 함께 허용하고 구조 변경은 차단한다. 납품 포함은 모두 차단한다.

이 보정이 없으면 “영업 code 입력 → 설계 규격 완성”과 “version 불변”을 동시에 구현할 수 없다.

### A2. 세트 line·instance 상태를 분리

- 세트 사양 줄의 주문 수량은 active instance 집계 projection이며 직접 덮어쓰는 숫자가 아니다.
- instance status는 `Active/Cancelled`; 진행 여부와 납품 여부는 기존 실행 원장에서 계산한다.
- 수량 증가/감소 mutation은 expected active instance count와 operation id를 받아 replay·경쟁을 차단한다.
- 프로젝트 `panel_count`는 active physical panel 수의 호환 projection으로 계속 유지하거나 같은 transaction에서 갱신한다. 기존 소비자가 이 값을 사용하는 동안 divergent count를 허용하지 않는다.

### A3. 월별 canonical ledger + 요청 revision

1차안의 `월 마감 후 Requested`는 제거하고 다음으로 바꾼다.

- canonical key는 `project_id + shipment_month(Asia/Seoul YYYY-MM)` 한 건이다.
- 월 ledger는 첫 출하 또는 회수 사례로 자동/조회 시 생성 가능한 `Open` 상태이며 월 중 출하 근거가 계속 누적된다.
- 영업은 매월 1일·16일 업무 시점에 같은 월 ledger에서 발행요청 자료 revision을 생성·Excel로 내보낼 수 있다. revision은 append-only이며 금액·근거 snapshot을 보존한다.
- 회계 발행 확인 전에는 새 revision이 이전 revision을 대체한다. 회계 확인 뒤 같은 출하 월의 추가 출하가 확인되면 ledger를 `AdjustmentRequired`로 전이하고 사유를 포함한 추가 revision을 허용한다. 이 방식은 월별 한 건이라는 canonical 분류와 1일·16일 운영을 동시에 보존한다.
- 월 마감은 완료 조건이 아니라 누락 점검 기준이다. 마감된 월에 미청구 출하가 있으면 `RequestRequired`, 회계 확인 금액보다 추가 근거가 생기면 `AdjustmentRequired`로 표시한다.
- 발행 요청 금액은 revision에 수동 입력하고, 각 월의 현재 유효 revision 금액 합계가 프로젝트 판매액을 넘지 않도록 project row lock 하에서 검증한다.

### A4. 프로젝트 완료에 `남은 요청 가능액 = 0` 추가

사용자 판매액 전액을 회수해야 하므로 UL891 세트 프로젝트 완료 조건은 다음 다섯 개다.

1. active physical panel 전량 납품
2. Open Pending 0
3. 모든 shipment/recovery month ledger의 최신 revision이 회계 발행 확인
4. 모든 발주 취소 recovery case가 회수 확인
5. 프로젝트 판매액 - 월별 최신 유효 발행요청 금액 합계 = 0

판매액이 미입력·0 이하이면 기존 영업 정산 validation과 같은 방식으로 완료를 차단한다. 금액 초과뿐 아니라 미청구 잔액도 완료를 막아야 한다.

### A5. 회수 상태 자동 연계

- `청구 필요`: case 생성 직후.
- `발행요청 반영`: 최신 monthly request revision에 case가 포함될 때 자동 전이.
- `회계 발행 확인`: 그 revision이 확인될 때 자동 전이.
- `회수 확인`: 영업이 확인일·비고와 함께 수동 전이.
- 이전 단계로 임의 되돌리지 않는다. revision 대체/조정은 audit event로 남기고 이미 확인된 case의 상태를 조용히 낮추지 않는다.

### A6. 명시적 패널 상세 탭 구성

패널 상세는 단순 요약이 아니라 다음 조회 projection을 가져야 한다.

- 기본/세트: project, spec/version, instance number, component code/name/specification, active/cancelled
- 설계: 기존 패널 정보와 spec snapshot
- 구매·자재: 이 패널/세트에 직접 연결된 항목이 없으면 프로젝트 공통임을 명시하고 project tab deep link
- 제조: 제조 상태·4단계 입력·담당자·최근 event
- 품질: IQC(직접 연관 시), LQC, OQC, 고객검수/FAT, 판정·Pending·evidence
- 물류: Packing Unit, 출발일, 납품일·상태
- QR: 발급 상태·발급/보기 action

담당자 mutation은 기존 각 workspace/deep link가 authoritative하며 v1 패널 상세에 중복 입력 form을 새로 복제하지 않는다. 패널 상세는 해당 업무의 정확한 route·target으로 이동시킨다.

### A7. 구조 변경과 downstream readiness

- UL891 세트 프로젝트는 모든 active instance가 Published version을 참조해야 생산계획의 제조 투입 readiness가 충족된다.
- 구성 패널 증감으로 생성/취소된 physical panel은 기존 assignee/work item/quality/logistics cleanup 패턴을 같은 transaction에서 적용한다.
- set dependency가 생긴 뒤 project item 또는 structure mode 변경을 서버에서 차단하고 행동 가능한 한글 오류를 반환한다.

### A8. 감사·privacy-safe evidence

- spec publish/version apply, instance increase/cancel, recovery transition, monthly revision/create/confirm/adjust의 actor·reason·before/after bounded projection을 전용 audit/operation table에 기록한다.
- 실제 사용자 이름·메일·원본 코멘트·첨부를 screenshot·report·test artifact에 남기지 않는다.

## 4. 보류

### D1. 구매품목 자동 인스턴스 연결

- 보류: 발주 입력 시 procurement item을 세트/패널에 자동 귀속하는 v2.
- 이유: 현재 구매 입력·Excel·자재 입고·IQC 계약 전체를 확장해야 하며, 사용자가 v1에 확정한 것은 발주일 품목의 회수 추적이지 자동 BOM 귀속이 아니다.
- v1: 취소 시 발주일 입력 품목의 명시적 선택을 필수로 하고 품목별 회수 case를 생성한다.

### D2. 세트 단가·납기·구성 원가

- 보류/범위 제외: 사용자가 프로젝트 전체 판매액·납기일을 authoritative로 확정했다.

### D3. 알림·내 업무 신규 종류

- 보류: 세트 생성·version publish·청구 revision·recovery 전용 새 알림/work item.
- 이유: 이번 사용자 계약에 신규 알림 대상·시점이 없고 실제 provider는 제외됐다. 기존 downstream workflow notification은 유지한다.

### D4. 전 프로젝트 월별 청구 전환

- 보류: 비-UL891과 legacy UL891은 기존 반월 batch·단일 settlement를 유지한다.

## 5. 제거

### R1. “월 마감 후에만 발행 요청 확정”

- 제거: 사용자 업무는 매월 1일·16일 발행 요청이며 월이 바뀔 때 프로젝트 요청을 분리하는 정책이다. 마감 후에만 요청하면 업무를 늦춘다.
- 대체: 월별 ledger + append-only revision + 추가 출하 adjustment.

### R2. 신규 기능에서 인앱 알림/outbox 자동 연결

- 제거: 1차안 9장의 “기존 outbox 재사용” 문구를 이번 구현 필수로 해석하지 않는다. 사용자 확정 범위에 없고 알림 피로·중복 계약을 만들 수 있다.

### R3. 패널 상세에 모든 부서 mutation form 복제

- 제거: 동일 데이터를 여러 form에서 수정하게 하면 validation·권한·저장 결과가 갈라진다. 패널 상세는 통합 projection과 정확한 업무 deep link를 제공하고 기존 담당 workspace가 입력 source다.

## 6. 권장 구현 순서

1. migration/schema: spec Draft/Published version, component, instance, panel link, operation/audit, recovery case, monthly ledger/revision/evidence relation.
2. Backend set aggregate: 조회, 신규 UL891 생성, draft 설계·publish, 수량 증가/선택 취소, version apply와 lifecycle/권한/concurrency.
3. Backend monthly billing/recovery: 후보 projection, revision, Excel-compatible data, confirmation, recovery cascade, completion five-gate.
4. Frontend project detail: 영업 set order/수량·recovery, 설계 spec/version publish, 세트/패널 matrix와 패널 상세 projection/deep links.
5. Frontend monthly billing: 월 ledger, revision, shipment/recovery evidence, 판매액·현재 요청·잔액, 회계 확인·adjustment 안내.
6. migration·Backend·Frontend·Full-Stack 회귀와 desktop/mobile visual evidence.

## 7. 2차 기획 필수 resolution

Fable 2차 기획은 아래를 모두 최종 계약으로 명시해야 한다.

- `Draft → Published → Superseded` version lifecycle과 Published 전 downstream start 차단
- component code 집합이 바뀌는 version apply의 unstarted-only reconcile 규칙
- canonical 월 ledger + append-only request revision + 월중 1일/16일 + late shipment adjustment
- 완료 5조건, 특히 남은 요청액 0
- 발주일 품목 선택 필수와 품목별 recovery case/자동 상태 cascade
- physical panel ID·P01 유지, project panel_count 호환 일치
- legacy·비-UL891 기존 경로 보존
- 패널 상세 projection/deep link와 중복 form 금지
- 신규 전용 notification/work item은 범위 제외

## 8. Finding과 판정

| Finding | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| F-UL891-001 | P1 | RESOLVED_FOR_PLAN | 불변 version만으로 최초 설계 입력 불가 | Draft/Published lifecycle 추가 |
| F-UL891-002 | P1 | RESOLVED_FOR_PLAN | 월 마감 후 확정은 1일·16일 업무와 충돌 | 월 ledger/revision/adjustment로 대체 |
| F-UL891-003 | P1 | RESOLVED_FOR_PLAN | 요청 존재/확인만으로 미청구 잔액이 남은 완료 가능 | 완료 gate에 잔액 0 추가 |
| F-UL891-004 | P2 | RESOLVED_FOR_PLAN | 구성 code 변경 시 physical panel reconcile 미정 | 미착수만 구조 변경, ID 재사용 금지 |
| F-UL891-005 | P2 | RESOLVED_FOR_PLAN | 알림·패널 중복 form이 사용자 범위를 넓힘 | 둘 다 범위 제외/기존 deep link 재사용 |

Open P0/P1/P2: `0/0/0`. 위 resolution은 사용자가 이미 확정한 정책 안의 구현 상세이며 신규 사용자 결정이 필요하지 않다.
