# TASK-SALES-KPI-001 Change 002 — 의사결정형 연간 매출 그래프

## 1. Task Identity Gate

- proposedTaskId: `TASK-SALES-KPI-001`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `DESIGN-000`
- roadmapNextGate: `DEFERRED_HOUSEKEEPING`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-SALES-KPI-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true` — 사용자가 완료 기능의 그래프 수정을 직접 요청함
- gateStatus: `PASS_REUSE`

검색 범위는 Task·planning·review·implementation report, Roadmap·실험 완료 원장, local/remote branch와 worktree다. GitHub PR API는 현재 실행 정책상 추가 승인을 요구해 호출하지 않았고, 이 제한은 local experiment 구현·검증의 canonical identity 판정에 영향을 주지 않는다.

## 2. Purpose identity

- 업무 목표: 단순 월 목표선을 의사결정 가치가 있는 월별 실적·목표·달성률 비교로 바꾸고 desktop과 mobile에 같은 실제 12개월 그래프를 제공한다.
- Root Finding: 목표 금액의 높낮이를 연결한 선은 월별 달성 여부나 실적 변동을 직접 보여 주지 못하고, 모바일 4×3 block은 연속 추세를 읽을 수 없다.
- 변경·검증 경계: 기존 Sales KPI 응답을 Frontend 파생 계산으로 시각화하고 Home과 `/sales`의 표시·테스트·synthetic screenshot만 변경한다.
- 보존할 불변조건: 확정 매출 기준, 목표 원장·CAS·감사, 파이프라인 분리, 통화·권한·project scope, Backend·DB·migration 무변경.
- 예상 산출물: benchmark 기록, combo SVG chart, desktop/mobile 화면, tests, implementation report, local experiment commit.

## 3. 채택한 그래프 계약

1. 12개월 x축과 금액 y축을 공유하는 grouped bar로 `확정 매출`과 `월 목표`를 직접 비교한다.
2. 목표가 0보다 큰 실제 경과 월만 `월 달성률 = 확정 매출 / 월 목표 × 100` 선으로 연결한다. 미래 월은 실적 0%로 오인되지 않게 선에서 제외한다.
3. 100% 달성 기준선을 표시하고 선택 월에는 정확한 매출·목표·달성률을 텍스트로 제공한다.
4. 연 누계 매출·등록 목표 누계·연 달성률·잔여/초과액은 KPI 카드에서 유지하고 그래프에 중복하지 않는다.
5. 모바일도 별도 block 카드가 아니라 12개월 전체가 들어가는 실제 SVG 그래프를 사용한다. 월 근거 선택은 그래프의 작은 bar를 touch target으로 만들지 않고 44px month selector로 분리한다.

## 4. 승인·안전 경계

- planningApproved: `true` — 기존 승인 planning + 사용자 직접 변경 요청
- implementationApproved: `true`
- commitApproved: `true` — 검증 뒤 현재 experiment branch local commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false` (`main` 승인 `0/3`)
- Persistent UAT·actual provider·대표 repo·GitHub main: 변경 금지
