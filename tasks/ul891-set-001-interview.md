# TASK-UL891-SET-001 — UL891 세트·개별 패널·부분출하·월별 청구 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `WAIVED_BY_USER_FOR_EXPERIMENT`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 `experiment/*` fast-track에서 사용자-facing interview를 생략한 근거와 사용자가 직접 확정한 UL891 업무 정책을 Fable 1차 기획 입력으로 고정한다. 사용자는 정책 질문 4건에 답한 뒤 이 내용을 기반으로 Fable 기획과 구현까지 완료하도록 명시했다. 이 branch와 대화의 standing instruction에 따라 `Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit`까지 이어간다. 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 포함하지 않는다.

## 1. Task Identity Gate

- proposedTaskId: `TASK-UL891-SET-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `첨부·사진 storage/검역/보존/backup·restore`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-UL891-SET-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_EXPLICIT_UL891_SET_REQUEST`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: UL891 프로젝트를 `세트 사양 → 세트 주문 인스턴스 → 개별 물리 패널` 계층으로 관리하고, 기존 패널 단위 제조·검사·QR·부분출하를 보존하면서 수량 감소에 따른 발주 회수와 월별 세금계산서 발행 요청을 추적한다.
- Root Finding 또는 정책 결정: 현재 프로젝트는 `panel_count`로 평면 패널 P01…PN만 생성해 동일 사양 여러 세트, 세트별 구성 패널, 주문 수량 변경, 월이 다른 부분출하·청구, 발주 후 취소 회수를 표현할 수 없다.
- 변경·검증 경계: UL891에만 additive set structure를 적용하고 프로젝트 상세·패널 상세, 수량 변경, 부분출하 표기, 월별 청구·발주 회수 정책과 API/UI/DB를 구현한다. 기존 개별 패널 실행·품질·QR·Packing Unit·Pending 원자를 재작성하지 않는다.
- 보존할 불변조건: 패널 식별자는 재사용하지 않음, 제조·LQC·OQC·FAT·QR은 개별 패널 원자, 납품된 패널 불변, Packing Unit 출발은 기존 원자성 보존, Backend authoritative validation·권한·감사·멱등성, 기존 migration 수정 금지, 비-UL891 legacy 호환.
- 예상 산출물: Fable 1차 기획 원문, Codex 내용 review, Fable 2차 기획 원문, additive migration·Backend API·Frontend 프로젝트/패널/영업 정산 UI, 자동 검증, desktop/mobile screenshot, 종료 문서와 local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

기존 `TASK-005A`, `TASK-010A`, `TASK-012A`, `TASK-013A`, `TASK-014A`, `TASK-BILLING-REQUEST-001`은 평면 패널과 프로젝트별 1건 정산을 구현했지만 동일 목적의 UL891 세트 구조는 아니다. 코드·문서·branch·worktree·PR에서 동일 목적 후보는 0건이다. Roadmap의 원래 다음 Gate는 첨부·사진 저장 체계지만 사용자는 UL891 정책을 확정한 뒤 이번 기획·구현을 명시적으로 지시했으므로 실험 branch 순서 override로 기록한다.

## 2. 사용자가 확정한 핵심 모델

1. 같은 세트 사양을 여러 개 주문하면 구성 패널 A~G의 이름과 규격은 모든 세트에서 동일하다. 구성 정의는 사양에 한 번 입력하고 실제 패널마다 고유 ID·상태를 가진다.
2. 한 프로젝트에 서로 다른 세트 사양과 주문 수량이 여러 줄 존재할 수 있다. 예: 1번 세트 3개, 2번 세트 5개, 3번 세트 3개.
3. 제조, LQC, OQC, QR, 전진검수와 FAT의 실제 처리 단위는 개별 물리 패널이다.
4. 세트 일부만 출하하는 것을 허용한다. 실제 현장에서도 부분출하가 발생한다.
5. 세트 사양별 판매단가와 납기일은 별도로 입력하지 않는다. 프로젝트 전체 판매액과 프로젝트 납기일을 권위값으로 유지하고 실제 출하일은 패널·출하 단위마다 달라질 수 있다.
6. 프로젝트별 데이터는 프로젝트 상세에서 모두 조회·입력하고, 패널별 데이터는 프로젝트 상세에서 패널 상세로 들어가 조회·입력한다.
7. 비-UL891 Item은 기존 평면 패널 구조를 유지한다. UL891만 세트 구조를 사용한다.

## 3. 주문 수량 변경 정책

1. 수량 감소는 사용자가 취소할 세트 인스턴스를 직접 선택한다.
2. 미착수 세트 인스턴스는 취소할 수 있다.
3. 구성 패널과 연결된 구매품목 중 발주일이 입력된 항목이 하나라도 있으면 취소를 막지는 않되 `고객 청구·회수 필요` 사례를 생성한다.
4. 이미 진행된 세트 인스턴스는 사유와 예외 확인을 입력해야 취소할 수 있다.
5. 납품된 패널이 포함된 세트 인스턴스는 취소할 수 없다.
6. 취소된 세트·패널 번호와 식별자는 영구 결번이며 재사용하지 않는다.
7. 수량 증가는 프로젝트 완료 전 허용하며 새 세트 인스턴스와 새 패널 ID를 생성한다.
8. 발주된 품목은 개별 가격을 입력하지 않지만 영업이 어떤 취소 세트·품목 때문에 돈을 받아야 하는지 추적할 수 있어야 한다.
9. 회수 상태는 `청구 필요 → 발행요청 반영 → 회계 발행 확인 → 회수 확인`으로 추적한다.

## 4. 세트 사양 변경 정책

1. 세트 사양은 버전으로 관리한다.
2. 미착수 세트 인스턴스는 최신 버전을 적용할 수 있다.
3. 이미 착수한 인스턴스는 당시 snapshot을 유지한다. 새 버전을 적용하려면 대상을 명시적으로 선택하고 변경 사유를 입력한다.
4. 납품된 패널의 사양 snapshot은 변경할 수 없다.
5. 구성 패널 code는 같은 사양 버전 안에서 고유해야 하며 구성 수는 A~G로 하드코딩하지 않고 가변으로 둔다.
6. 영업은 세트 줄·주문 수량·구성 code/개수를 입력하고, 설계는 구성 패널명과 규격을 완성하는 역할 분리를 기본으로 한다.

## 5. 부분출하·Packing Unit 정책

1. 세트는 주문·추적 묶음이지 포장 단위와 동일하지 않다.
2. 기존 Packing Unit의 출발 처리 원자성은 유지한다.
3. 세트 일부만 출하할 때 출하 가능한 개별 패널 subset을 별도 Packing Unit에 담아 출발·납품한다.
4. 프로젝트·세트·패널 상세에서 어떤 세트의 몇 번째 인스턴스와 어떤 구성 패널이 어느 Packing Unit·출하일·납품 상태인지 추적할 수 있어야 한다.

## 6. 월별 세금계산서 발행 요청 정책

1. 기본은 프로젝트별 1건이지만 출하 월이 바뀌면 같은 프로젝트에 여러 건을 만든다.
2. 계산 기간은 매월 1일부터 말일까지의 달력 월이며 Asia/Seoul 기준 실제 출발·출하일을 사용한다.
3. 같은 프로젝트·같은 출하 월의 출하분은 하나의 발행 요청 후보로 합산한다.
4. 발행 요청 금액은 영업이 직접 입력한다.
5. 프로젝트 누적 발행 요청 금액은 프로젝트 판매액을 초과할 수 없다.
6. 화면에서 프로젝트 판매액, 누적 요청액, 남은 요청 가능액과 해당 월 출하 근거를 함께 보여준다.
7. 발주 후 취소 회수 사례는 해당 취소 월의 발행 요청에 선택해 반영할 수 있어야 한다. 출하가 없는 회수 전용 월 후보도 허용한다.
8. 한 달의 뒤늦은 추가 출하 누락을 막기 위해 기본 권장안은 마감된 달만 발행 요청 가능하게 하되, 현재 영업 실무의 1일·16일 요청과 충돌하는지 Fable이 Repository 계약을 대조해 비차단 보정안을 제시한다.

## 7. 프로젝트 완료 조건

프로젝트 완료는 아래를 모두 만족해야 한다.

- 취소되지 않은 모든 active 패널 납품 완료
- 모든 월별 발행 요청의 회계 발행 확인 완료
- 발주 후 취소 회수 사례 전부 회수 확인 완료
- Open Pending 0건

기존 lifecycle fence, optimistic concurrency, idempotency와 감사 기록을 유지하며 완료 뒤 수량·사양·청구 관련 mutation은 차단한다.

## 8. Repository 기준선과 구현 제약

- 현재 프로젝트 생성은 `panel_count`로 `panel_placeholders` P01…PN을 만든다.
- QR, 제조, 품질, 물류, Pending의 downstream 참조는 개별 `panel_placeholder`를 실행 원자로 사용한다. 이를 세트 aggregate 하나로 합치지 않는다.
- 현재 프로젝트 상세 탭은 전체 흐름·영업·생산관리·설계/패널·구매·자재·제조·품질·물류이고 패널 상세는 기본 설계 요약 중심이다.
- 현재 `TASK-BILLING-REQUEST-001`은 프로젝트별 1건과 전체 출하 완료를 전제로 하므로 월별 복수 요청으로 additive 확장해야 한다.
- 현재 `TASK-014A` 정산·완료는 프로젝트 단일 정산을 전제로 하므로 새 월별 청구·회수 gate와 모순 없이 호환 계층을 정해야 한다.
- UL891에서 세트 종속 데이터가 생긴 뒤 FlatPanel 구조로 바꾸거나 다른 Item으로 변경하는 동작은 차단한다.
- 기존 legacy 프로젝트와 비-UL891 프로젝트는 migration 후에도 조회·수정·workflow가 회귀하지 않아야 한다.

## 9. Fable이 최종 기획에서 결정할 비차단 항목

아래는 사용자에게 다시 묻지 않는다. Fable 1차 기획이 대안과 권장안을 제시하고 Codex review 뒤 2차 기획에서 Repository에 맞게 확정한다.

1. 세트 사양·버전·구성·인스턴스와 기존 패널의 최소 additive schema 및 snapshot 방식.
2. 프로젝트 생성 시 UL891 초안 입력과 영업→설계 역할 분리 UX.
3. 기존 UL891 legacy 프로젝트의 migration/표시 정책.
4. 월 중 1일·16일 실무를 지원하면서 월별 1건 불변조건을 지키는 `draft/lock/회계 확인` lifecycle.
5. 구매품목과 취소 세트 연결의 v1 최소 모델. 직접 연결 정보가 없는 legacy 구매품목의 보수적 처리.
6. 진행 시작·납품 완료 판정에 재사용할 기존 authoritative event와 race-safe 수량 변경 방식.
7. 프로젝트 상세와 패널 상세의 desktop/mobile 정보 구조, 조회전용 타부서와 담당자 수정 권한.
8. 기존 단일 settlement와 새 월별 billing request의 compatibility/rollout 경계.

## 10. 포함 범위

- UL891 세트 사양·버전·구성 패널·주문 세트 인스턴스 관리
- 기존 physical panel 생성·고유 식별자와 세트/구성 연결
- 수량 증가·선택 감소와 진행/납품/발주 회수 규칙
- 프로젝트 상세의 세트·패널 추적 및 패널 상세의 부서별 패널 데이터
- 부분출하와 Packing Unit/출하 근거 projection
- 월별 복수 세금계산서 발행 요청, 금액 cap·잔액, 발주 후 취소 회수 추적
- 프로젝트 완료 gate 확장
- Backend authoritative validation·권한·동시성·감사, desktop·390px UX, 자동 검증

## 11. 명시적 제외

- 세트별 판매단가·세트별 납기일·구성 패널별 원가 계산
- ERP/회계 실제 API 발행, 실제 Teams/Mail provider 호출
- 기존 Packing Unit 출발 원자성 제거
- 비-UL891 Item의 세트 구조 전환
- 대표 repo·`main`, Persistent UAT migration/runtime handover, push·PR·merge

## 12. 성공 기준

- 영업이 한 UL891 프로젝트에 여러 세트 사양과 수량을 저장하고, 설계가 구성 패널명·규격을 완성하면 세트 인스턴스별 실제 패널이 고유 ID로 추적된다.
- 개별 패널의 제조·LQC·OQC·FAT·QR·Packing Unit·출하 이력은 기존 원자성을 유지하면서 프로젝트→세트→패널 및 패널 상세에서 조회된다.
- 선택 수량 감소는 진행/납품 상태에 맞게 차단·사유 요구되고, 발주일 입력 품목이 있으면 청구·회수 사례가 누락 없이 생성된다.
- 같은 프로젝트의 다른 출하 월마다 발행 요청을 만들 수 있고 월 근거·수동 금액·누적/잔액·초과 차단이 동작한다.
- 프로젝트 완료는 납품·회계 발행 확인·발주 취소 회수·Open Pending gate를 모두 통과해야 한다.
- 비-UL891·legacy flow가 회귀하지 않고 desktop과 390px에서 핵심 행동에 page-level overflow가 없다.
- Backend 전체, Frontend lint/typecheck/unit/build, fresh·existing migration, isolated Full-Stack와 screenshot 검증을 통과한다.
- Open P0/P1/P2가 0이고 종료 산출물과 사용자 검수 handoff 상태를 추적한다.

## 13. 승인·안전 경계

- planningApprovedForExperiment: `true`
- implementationApprovedForExperiment: `true` — Fable 2차 기획의 blocking decision 0인 범위
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
