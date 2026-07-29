All baseline checks pass: roadmap chapter 9 policies, the TASK-007B roadmap entry, and the 007A Pending implementation (5 forward-only statuses, Normal/Urgent priority, project-only linkage) all match what round 1 asserted, and all four blocking decisions are recorded as resolved (1-B, 2-A, 3-B, 4-A). No new blocking question remains, so round 2 is the confirmation summary.

---

# TASK-007B — 패널·프로젝트 병목 상태 집계 Deep Interview Round 2 (확인 요약)

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- interviewRound: 2
- interviewSource: `tasks/007b-interview.md`

## Round 2 기준선 재확인

Round 1의 blocking 결정 4건이 모두 사용자 결정(`1-B · 2-A · 3-B · 4-A`, 2026-07-16 사용자의 권장안 일괄 채택 지시)으로 기록되었고, 다음을 현재 Repository에서 표적 재확인했다. 새로운 blocking 질문은 없다.

- Product Roadmap 9장: 대표 상태는 병목 기준 + 진행률 병기(9.3), 1~4단계는 프로젝트 단위(9.4), 단계 전진만·차단 flag(9.5), open Pending은 배지·건수 표시이며 상태를 "중단"으로 바꾸지 않음(9.6), 완료 조건은 open Pending 0건 포함(9.7). 채택된 결정과 충돌 없음.
- 현재 experiment 구현: Pending 상태 5종(`Registered → ActionRequested → InProgress → ReinspectionRequested → Closed`)과 긴급도 2종(`Normal/Urgent`), 프로젝트 단위 연결만 존재. 결정 2-A(프로젝트 수준 귀속)와 3-B(open 판정 + 재검사 대기 구분)를 현 데이터로 구현 가능함을 확인.
- Roadmap 23장 실행 큐: `TASK-007B`가 canonical Task이며 interview 문서의 Task Identity Gate는 `PASS_REUSE`, 명시적 순서 변경 승인 기록 있음.

## 확인 요약

### 해결할 문제

프로젝트 workflow 진행률과 open Pending이 별도 화면에 흩어져 있어, 여러 패널 중 어느 단계가 전체 납기를 막는지 즉시 판단할 수 없다. 사용자는 프로젝트 상세·패널 상세·Pending 목록을 오가며 수동으로 병목을 추정한다.

### 확정한 범위와 정책

1. **표시와 우선순위 (결정 1-B)**: 대표 병목은 항상 가장 뒤처진 필수 단계로 표시하고 open Pending은 별도 차단 배지·건수로 병기한다(Roadmap 9.3·9.6 유지). 다만 "다음 확인 대상"의 우선순위(목록 정렬·강조·상세 첫 안내)는 open Pending 차단 → 가장 뒤처진 필수 단계 순으로 계산하며, 정렬 근거를 설명하는 문구를 함께 표시한다.
2. **Pending 차단 귀속 (결정 2-A)**: Pending 차단은 프로젝트 수준에서만 반영한다. 패널 병목은 단계 기준으로만 계산하고, Pending은 프로젝트 배지·건수·목록 deep link로 연결한다. Pending model·migration 변경은 없으며, 패널 단위 차단 귀속(Roadmap 9.5 blocked flag)은 TASK-007A 후속 확장 Task로 남긴다.
3. **open 판정 기준 (결정 3-B)**: `Closed`를 제외한 모든 Pending 상태를 open 차단으로 집계해 완료 조건(9.7)의 open 정의와 일치시키되, `ReinspectionRequested`는 "재검사 대기"로 배지 안에서 구분 표기한다. 긴급도는 병기 정보로만 표시한다.
4. **동률과 프로젝트 단위 단계 (결정 4-A)**: 가장 뒤처진 단계에 패널이 여러 면이면 개별 지목 없이 "단계명 + 패널 n면"으로 묶어 표시하고 상세에서 해당 단계 패널 목록으로 진입한다. 1~4단계(프로젝트 단위)가 미완료면 그 프로젝트 단계 자체를 대표 병목으로 표시한다.
5. **불변조건**: 기존 18단계 번호, 진행률 공식, FAT optional 분모, Pending forward-only 상태·권한·audit을 재사용하고 aggregate는 파생값으로서 원본 workflow·Pending을 변경하지 않는다. 원본보다 넓은 정보를 노출하지 않으며 mutation 권한을 확대하지 않는다.
6. **예외 처리**: 집계 대상 stage·FAT 규칙이 불완전하거나 일부 원본 조회가 실패하면 거짓 완료 대신 설명 가능한 미확정 상태를 표시한다. 재조회 시 최신 authoritative snapshot을 반영한다.

### 명시적 제외

Home widget, 관리자용 Pending 유형 편집, workflow stage 번호·진행률 공식 변경, 패널 단위 Pending 귀속(후속 Task), actual Teams/Mail provider 발송, Persistent UAT migration·write·runtime handover.

### Deferred 비차단 결정

- 계산형 조회 우선 원칙 아래 persisted snapshot 필요 여부 비교는 planning에서 Fable이 대안 비교로 제시하고 사용자 결정 항목으로 전달한다.

### 검증 계획 방향

- 자동 검증: 필수 단계 partial/all, open Pending 차단(재검사 대기 구분 포함), FAT optional 분모, 여러 패널 동률 aggregate, authorization scope를 isolated tests로 검증한다.
- 사용자 검수: Desktop·390px synthetic screenshot으로 용어·우선순위·밀도를 확인한다. Persistent UAT와 실제 provider는 사용하지 않는다.

## 사용자 확인 요청

interview 문서 10절의 확인 항목 6건을 검토하고, 이 요약을 planning 입력으로 사용하는 데 동의하는지 확인해 달라. 확인 시 interview 문서는 `interviewStatus: COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`으로 갱신되며, 이는 planning·구현 승인이 아니다.

---

- interviewStatus: SUMMARY_CONFIRMATION_REQUIRED
- planningStatus: NOT_STARTED
- implementationApproved: false
