# TASK-014A — 영업 정산·세금계산서·프로젝트 완료 Codex 내용 Review

- reviewSource: `tasks/014a-planning.md`
- reviewOwner: `CODEX`
- reviewStatus: `COMPLETED`
- reviewRound: 1
- planningOriginalModified: false
- openBlockingDecisionCount: 0

## 1. 총평

Fable 1차 기획은 `TASK-013A`가 만든 `SalesSettlementCompleted` skeleton을 실제 사용자 가치로 닫는 방향이 분명하다. 모든 active panel 납품, 세금계산서 발행 완료, open Pending 0건을 서버가 다시 확인하고 settlement·work item·project·event·인앱 알림을 한 transaction에서 완료하는 핵심은 Roadmap 18장과 맞는다. 회계·국세청 연동, 파일·OCR·수금·복수 청구와 완료 뒤 재오픈을 제외한 범위도 실험 MVP에 적절하다.

다만 현재 Repository와 대조하면 두 가지 무결성 구멍을 먼저 닫아야 한다. 첫째, 완료 transaction의 Pending 0건 조회와 동시에 다른 transaction이 Pending을 생성하면 완료된 프로젝트에 open Pending이 생길 수 있다. 둘째, 현재 `UpdateProjectAsync`는 Completed 상태를 차단하지 않고 `ChangePanelCountAsync`는 Cancelled만 차단하므로 완료 뒤 active panel 추가·기본정보 변경이 가능하다. 이 두 경로는 프로젝트 완료 조건을 사후에 깨므로 Fable 2차 기획의 필수 resolution으로 올린다.

또한 전역 `영업 정산` 메뉴는 승인된 user-flow의 “정산은 영업의 프로젝트 하위” 위치와 충돌한다. 기존 `내 업무`가 대기 queue 역할을 이미 제공하므로, 이번 MVP는 프로젝트 상세의 정산 진입과 내 업무 deep link를 우선해야 한다.

## 2. 사용자 문제와 기대 결과

### 유지

- 정산 skeleton만 존재해 프로젝트가 실제 완료되지 않는 root problem
- 영업 담당자가 납품·Pending·세금계산서 조건을 한 화면에서 확인하고 완료하는 흐름
- generic 내 업무 완료를 차단하고 domain transaction만 완료를 소유하는 구조
- 완료 뒤 read-only와 append-only 이력

### 추가

- 완료 뒤에도 프로젝트 상태를 깨뜨릴 수 있는 기존 mutation을 함께 차단하는 “Completed lifecycle fence”
- Pending 생성과 project 완료 사이의 race를 막는 DB 수준 project lock/trigger 계약
- active panel 0건을 “모두 납품”으로 오인하지 않는 명시적 차단

## 3. 제품 방향·Roadmap 정합성

### 유지

- Roadmap 18단계: `납품 완료 → 영업 정산 대기 → 세금계산서 발행 완료 → 프로젝트 완료`
- 완료 조건 세 가지와 open PUNCH/부적합 차단
- 다른 부서는 납품 완료 뒤 사실상 완료로 보고, 영업만 정산까지 추적하는 역할 차이
- 외부 회계·전자세금계산서 연동 제외

### 제거

- 전역 메뉴의 독립 `영업 정산` 진입. `docs/13-user-flow-baseline.md`에서 정산 위치는 영업의 프로젝트 하위로 확정됐다.

### 대체 권고

- 프로젝트 상세의 `정산·완료` action/section과 `SalesSettlementCompleted` 내 업무 deep link를 primary entry로 사용한다.
- 별도 전역 queue는 만들지 않는다. 대기 탐색은 이미 project target work item을 제공하는 `내 업무`를 재사용한다.
- 완료된 프로젝트는 동일 정산 route에서 read-only 결과를 볼 수 있게 한다.

## 4. 기능별 판정

| 기능 | 판정 | 근거·resolution |
| --- | --- | --- |
| settlement Draft/Completed record | 유지 | invoice 입력과 경쟁 복구·감사를 project 기본정보와 분리 |
| invoice 발행일 필수·번호/메모 선택 | 유지 | 최소 업무 근거이며 전체 회계정보보다 bounded |
| 모든 active panel 납품 검증 | 유지+강화 | active panel count가 1개 이상이고 각 panel이 Finalized `DeliveryCompleted` batch의 결과에 속해야 함 |
| open Pending 0건 | 유지+강화 | target type 제한 없이 `pending_issues.project_id`의 모든 non-Closed row를 차단 |
| `sales.settle` 전용 permission | 유지 | `projects.update`보다 최소 권한에 적합 |
| System Administrator permission 부여 | 제거 | 조회는 `projects.read.all`로 충분하며 업무 mutation permission 자체를 주지 않음 |
| 영업 담당/current assignee actor gate | 유지 | endpoint permission·project scope·store actor 교집합 유지 |
| 전역 정산 queue/menu | 제거 | 승인된 project subordinate 위치 및 기존 내 업무 queue와 중복 |
| 프로젝트 상세/내 업무 deep link | 추가 | `/projects/{projectId}/settlement` 또는 동등한 project-context route로 고정 |
| 완료 알림 | 유지 | 외부 provider 없이 인앱 원본만, business 원문·invoice number를 payload에 복제하지 않음 |
| 완료 뒤 재오픈·수정발행 | 보류 | 별도 정책 Task 없이는 append-only 완료를 깨뜨림 |
| 완료 후 project 일반 수정 | 이번 Task에서 차단 | 완료 조건의 사후 훼손을 막는 필수 lifecycle 보호 |

## 5. 필수 Repository resolution

### `014A-PENDING-COMPLETION-RACE`

- Severity: `P1`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: completion이 Pending 0건을 읽은 직후 concurrent Pending insert가 성공하면 `Completed` project에 open Pending이 생긴다.
- Resolution: project completion과 모든 Pending insert가 동일 project row를 같은 순서로 lock해야 한다. 가장 좁은 방안은 additive migration의 `pending_issues` INSERT trigger가 project row를 `FOR UPDATE`로 읽고 `Completed/Cancelled/deleted`를 거부하는 것이다. completion은 project row를 먼저 lock한 뒤 Pending을 조회한다. trigger가 먼저 오면 completion이 기다렸다가 새 Pending을 보고 실패하고, completion이 먼저 오면 insert가 기다렸다가 Completed를 보고 실패한다. manual·material·quality 등 모든 Pending 생성 경로에 공통 적용돼야 한다.

### `014A-POST-COMPLETION-MUTATION`

- Severity: `P1`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: 현재 project 기본정보 수정은 Completed를 차단하지 않고 panel count 변경은 Cancelled만 차단한다. 완료 뒤 active panel 추가 또는 담당·납기 변경이 가능하다.
- Resolution: `UpdateProjectAsync`, `ChangePanelCountAsync`와 상태 mutation에서 `Completed`를 서버에서 차단하고 안정적인 409를 반환한다. project row lock 뒤 상태를 확인해야 completion과 경쟁해도 일관된다. 기존 Completed 삭제 차단은 유지한다.

### `014A-DELIVERY-SOURCE-OF-TRUTH`

- Severity: `P2`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: `logistics_delivery_results` 존재만 세면 draft/cancelled owner 또는 비활성 membership 해석이 섞일 수 있다.
- Resolution: active panel 1개 이상, Finalized `DeliveryCompleted` batch, active batch-unit·packing-unit-panel membership과 delivery result의 결합으로 각 active panel의 납품 완료를 판정한다. count equality만 보지 말고 `NOT EXISTS` 미완료 panel로 검증한다.

### `014A-PENDING-SCOPE`

- Severity: `P2`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: project/panel target만 검사하면 같은 project의 자재·검사 등 다른 target type open Pending을 놓쳐 Roadmap의 “open Pending 0건”과 달라진다.
- Resolution: `pending_issues.project_id=@project_id and status<>'Closed'` 전체를 차단하고 UI에는 aggregate count와 Pending project filter link만 표시한다. raw Pending title은 정산 API·알림에 복제하지 않는다.

### `014A-NAVIGATION-SCOPE`

- Severity: `P2`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: 전역 메뉴는 확정된 project subordinate 위치와 중복 navigation을 만든다.
- Resolution: project detail action/section + 내 업무 deep link만 구현하고 global navigation item은 추가하지 않는다. desktop/mobile 모두 같은 project-context route를 사용한다.

### `014A-ADMIN-LEAST-PRIVILEGE`

- Severity: `P2`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: System Administrator에 `sales.settle`을 부여하면 store actor gate가 있어도 불필요한 mutation capability가 생긴다.
- Resolution: 신규 permission은 `sales` 업무 role에만 seed한다. System Administrator는 기존 전체 project read/audit로 조회하고 settlement mutation은 permission 단계에서 403이다.

### `014A-INVOICE-DATE-BOUNDARY`

- Severity: `P2`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: 발행 완료 조건인데 미래 발행일이 허용되면 아직 발행하지 않은 invoice로 project 완료가 가능하다.
- Resolution: 발행일은 project 생성일보다 이르지 않고 Asia/Seoul 업무일 기준 오늘보다 늦지 않게 한다. 번호·메모 길이와 trim/blank를 server/client 양쪽에서 bounded validation한다.

### `014A-ZERO-PANEL-VACUOUS-COMPLETION`

- Severity: `P2`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: active panel 0건에서 `NOT EXISTS incomplete`만 사용하면 완료 조건이 참이 될 수 있다.
- Resolution: completion과 detail projection 모두 `activePanelCount > 0`을 별도 조건으로 강제한다.

### `014A-NOTIFICATION-BOUNDARY`

- Severity: `P2`
- 상태: `RESOLUTION_REQUIRED`
- 원인·영향: invoice 번호나 Pending 원문을 알림에 복제하면 business-sensitive data가 채널 확장 시 노출되고 recipient 집계가 과도해질 수 있다.
- Resolution: 알림은 project 완료의 고정 category·deep link만 보유하고 invoice/Pending 원문을 넣지 않는다. recipient는 active distinct project assignee와 sales owner 중 actor 제외로 제한하며 recipient unique/idempotency를 사용한다. 실제 provider queue는 생성하지 않는다.

## 6. 우선 구현 순서

1. additive `0037` migration: settlement·operation receipt·projects 완료 metadata·immutability·Pending insert lifecycle trigger·permission seed
2. Backend domain: project-first lock, actor/scope, detail/draft/finalize, delivery/Pending/invoice 조건, replay
3. 완료 lifecycle fence: ProjectStore Completed mutation 차단, WorkflowStore generic 차단/deep link, cancel/purge 정합
4. project-context Frontend: 완료 조건 → invoice 입력 → 최종 확인, completed read-only
5. authorization·concurrency·fresh/existing migration·full-stack E2E
6. desktop·390px screenshot, implementation report, local experiment commit

## 7. 사용자 가치·운영 부담 판단

- 최소 record와 project-context 화면은 18단계를 실제로 닫는 직접 가치가 있다.
- 전역 queue, 외부 회계 연동, invoice 파일, 수금·채권은 운영 부담이 커 이번 MVP에서 제거·보류하는 것이 맞다.
- Pending trigger와 Completed mutation 차단은 부가기능이 아니라 완료 불변조건을 보존하는 필수 안전 경계다.
- invoice identifier는 선택값으로 두고 발행일을 authoritative 완료 근거로 사용하면 현장 입력 부담과 감사 근거를 균형 있게 맞춘다.

## 8. 2차 기획 반영 계약

Fable 2차 기획은 다음을 최종 구현 source에 명시해야 한다.

- global settlement menu/queue 제거, project detail + My Work deep link 고정
- System Administrator `sales.settle` seed 제거, read-only 유지
- project-first lock order와 Pending INSERT DB fence
- target type 무관 project open Pending 전체 차단
- Finalized delivery relation + active panel 1개 이상 검증
- Completed 뒤 project edit/panel/status mutation 차단
- invoice 발행일의 KST today·project creation date boundary
- notification payload/recipient/provider 경계
- 위 Finding 8건의 상태를 `RESOLVED_IN_PLAN`으로 전환하고 구현·test 위치를 제시

이 review의 blocking decision은 0이다. 모든 항목은 사용자 standing experiment rule 안에서 기존 확정 정책을 보존하는 구현 resolution이며, 별도 제품 선택을 요구하지 않는다.
