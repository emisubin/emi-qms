# TASK-USER-FLOW-001 — Codex Repository 대조 Review

> 이 문서는 구현 가능성과 Repository 충돌을 확인한 보조 기술 review다. 최신 사용자 목적에 따른 주 review는 `tasks/user-flow-001-preview-review.md`의 내용·제품 방향 review이며, 두 문서가 다르면 Change 002와 내용 review를 우선 적용한다.

- reviewOwner: `CODEX`
- planningSource: `tasks/user-flow-001-planning.md`
- interviewSource: `tasks/user-flow-001-interview.md`
- reviewStatus: `TECHNICAL_FEASIBILITY_SUPPLEMENT`
- planningApproved: true
- implementationApproved: true
- openP0Count: 0
- openP1Count: 0
- openP2Count: 0
- openP3Count: 0

## 1. 검토 범위

- 사용자 확인이 끝난 Round 1~3 interview와 Fable planning 초안
- Product Roadmap의 18단계, responsibility type, fallback, 알림·후속 Task 계약
- Frontend route·공통 navigation·내 업무·승인 대기 화면
- Backend project 생성, workflow handoff, Entra 승인 대기와 현재 role·permission seed
- 기존 `docs/02-business-flow.md`, `docs/04-permission-matrix.md`

제품 source, API, DB, migration, runtime과 provider는 변경하지 않았다.

## 2. Repository와 일치한 핵심 계약

| Planning 계약 | Repository 대조 결과 |
| --- | --- |
| 13개는 신규 권한 role이 아니라 유저플로우 여정 단위 | Roadmap의 부서·품질 responsibility type과 일치 |
| 프로젝트 생성 뒤 생산관리 업무와 부서 참조 알림 연결 | Project 생성 endpoint가 첫 workflow stage를 완료하고 `WorkflowStore`가 Production Planning 업무와 전체 부서 참조 알림을 생성 |
| 내 업무·알림·Teams Activity·관리자 deep link | Frontend의 `/my-work`, `/notifications`, `/teams/activity` 상세, `/admin` 계열 route와 일치 |
| 현재 navigation | 내 업무·프로젝트·생산관리·구매·알림은 공통, 자재·관리자는 권한에 따라 조건부 표시하는 실제 구현과 일치 |
| 승인 대기 사용자 | Entra 활성 사용자에게 active role이 없을 때 approval pending으로 판정하고 Frontend가 업무 화면 대신 승인 대기 화면을 표시하는 계약과 일치 |
| 미구현 기능의 Task 연결 | `TASK-007A`, `TASK-MOBILE-001`, `TASK-HOME-001`, `TASK-009A/011A/012A/013A/014A` Roadmap 순서와 일치 |
| 제품 구현 제외 | 현재 Task 승인 경계와 일치 |

## 3. Findings

### P2-001 — 프로젝트 생성 진입점이 실제 Frontend와 다름

- Planning 위치: 4절 시나리오 A 1단계
- 초안 표현: 영업 담당자가 `프로젝트` 또는 `내 업무` 카드로 진입해 프로젝트를 생성한다.
- 실제 상태: `내 업무`는 생성된 work item을 시작하고 해당 `linkUrl` 또는 프로젝트 상세로 이동한다. 새 프로젝트 생성은 프로젝트 목록의 생성 action에서 시작한다.
- 영향: Canonical 유저플로우가 존재하지 않는 시작 경로를 안내하면 첫 사용자와 후속 화면 Task가 잘못된 UX를 기준으로 삼는다.
- 권장 resolution: “영업 담당자가 `프로젝트` 메뉴의 프로젝트 목록에서 생성 action으로 진입한다. 생성 뒤 생산관리 담당자의 `내 업무`와 참조 알림이 만들어진다”로 수정한다. `내 업무`는 프로젝트 생성 이후 담당 업무 진입점으로만 기록한다.
- 사용자 정책 변경: 없음 — 실제 구현에 맞춘 정확성 보정이다.
- 해소 상태: `RESOLVED` — 승인된 planning과 Fable 작성 preview 모두 프로젝트 목록의 생성 action만 신규 프로젝트 진입점으로 기록한다.

### 조건부 게시 Finding — 기존 업무 흐름·권한 문서가 현재 구현과 충돌함

- Planning 위치: 2절 기존 기준선 설명, 11절 기존 문서 연결, 16절 결정 2
- 실제 상태 1: `docs/02-business-flow.md`는 설계 → 생산계획 순서와 제조 10개 단계 중심의 과거 개괄을 유지하지만 현재 18단계와 Backend workflow는 프로젝트 생성 → 생산계획 → 설계 → 구매 순서다.
- 실제 상태 2: `docs/04-permission-matrix.md`는 Design·Procurement role이 아직 없다고 쓰지만 현재 seed와 runtime schema에는 Design·Procurement·Materials role과 관련 permission이 존재한다.
- 영향: 새 `docs/13`을 canonical로 선언하고 기존 문서에 link만 붙이면 Repository 안에 상충하는 source of truth가 계속 남는다. 다만 Change 002에 따라 현재 문서는 개인 참고 자료이며 canonical 게시 대상이 아니므로 현재 Task의 P2나 개인 사용 blocker로 판정하지 않는다.
- 권장 resolution: 나중에 사용자가 `docs/13`의 외부 공유나 canonical 게시를 선택한 경우에만 별도 `DOCS_GOVERNANCE` 범위에서 `docs/02-business-flow.md`와 `docs/04-permission-matrix.md`를 정렬한다.
- 대안: 별도 `P2_REMEDIATION`을 먼저 완료한 뒤 canonical 유저플로우를 게시한다. 제품 변경은 없지만 순차 Task가 하나 늘어난다.
- 사용자 정책 변경: 없음 — 실제 구현과 문서의 drift 보정이다. 다만 구현 allowlist 확장 승인이 필요하다.

## 4. Fable 미결정 3건에 대한 Codex 권고

### 결정 1 — Canonical 파일명

- A. `docs/13-user-flow-baseline.md`
- B. `docs/user-flow.md`
- Codex 권고: A. 현재 `docs/01`~`docs/12` 번호 체계 다음에 위치해 탐색과 인용이 명확하다.
- 사용자 결정: A.

### 결정 2 — 기존 문서와의 관계

Fable의 원래 A안인 “`docs/02` 존치+link만 추가”는 P2-002를 해결하지 못하므로 그대로 승인할 수 없다.

- A. `docs/13` 상세 canonical 생성 + `docs/02` current overview 정렬 + `docs/04` current permission matrix 정렬을 즉시 함께 수행
- B. `docs/13` 구현 전에 별도 P2 remediation으로 `docs/02`·`docs/04`를 먼저 정렬
- C. `docs/13` 미게시 preview를 먼저 만들고 사용자 검수 뒤 같은 Task에서 `docs/02`·`docs/04`를 정렬. P2 해소 전 완료·게시 금지
- Codex 권고: C를 안전한 단계형 구현으로 수용한다. 사용자가 새 기준선의 내용과 표현을 먼저 확인할 수 있고, 기존 문서는 그때까지 보존하면서 최종 게시 전 충돌을 해소한다.
- 사용자 결정: C.

### 결정 3 — Pending List·정산 메뉴 위치

- A. Pending List는 내 업무·프로젝트 하위 공통 진입, 정산은 영업의 프로젝트 하위에 위치만 표기
- B. 위치를 확정하지 않고 도입 Task에 전부 위임
- Codex 권고: A. 세부 화면이나 정책은 확정하지 않으면서 후속 navigation 재논의를 줄일 수 있다.
- 사용자 결정: A.

## 5. Canonical 문서 구현 권고

- 문서 1개에 공통 진입점, 목표 메뉴, 공통 예외, 현재·목표 상태와 13개 여정을 둔다.
- 전체 18단계 handoff는 master Mermaid flow 1개로 보여주고 13개 여정은 compact table과 필요한 예외만 기록한다. 13개 diagram을 각각 만들지는 않는다.
- 실제 구현 route와 현재 가능한 action은 code-derived evidence로 표시한다.
- 미구현 행동은 `예정`, 도입 Task와 위임 결정으로 명확히 구분한다.
- Backend policy·Roadmap·canonical user-flow 중 충돌이 생기면 Backend와 승인된 최신 Task 계약을 우선 확인하고 문서를 같은 Task에서 갱신한다.
- Preview 전문은 Fable 5가 direct write하고 GPT-5.6 SOL은 `tasks/user-flow-001-preview-review.md`에만 Finding을 기록한다. Codex는 원문을 patch하지 않으며 수정은 Fable revise로 수행한다.

## 6. 과거 단계형 구현안과 최신 결정

### Phase A — Preview

- `docs/13-user-flow-baseline.md` — 신규 canonical 후보 preview
- Task·Roadmap 추적 문서
- 기존 `docs/02-business-flow.md`·`docs/04-permission-matrix.md` 변경 금지

### Phase B — Change 002로 보류

- 개인 참고 문서 사용에는 실행하지 않는다.
- 외부 공유 또는 canonical 게시를 사용자가 별도로 선택할 때만 `docs/02`·`docs/04` 정렬을 재검토한다.
- Codex review는 Fable 원문 보정을 자동 실행하지 않는다.

Frontend·Backend·API·DB·migration·dependency·runtime configuration·provider는 allowlist에서 제외한다.

## 7. 검증 계획

- `docs/13`의 18단계 순서와 Backend `StageToNextStage`·Roadmap 대조
- 13개 여정의 responsibility type·다음 담당자·fallback 대조
- 현재 route·menu·action을 Frontend source와 대조하고 미구현 route 노출 0 확인
- `docs/02`·`docs/04`의 stale assertion 제거와 `docs/13` 관계 확인
- Markdown local link·anchor·duplicate heading·Mermaid syntax 검사
- Privacy·secret·절대 경로·raw evidence 0
- 제품 source·migration·runtime configuration diff 0

## 8. 승인 전 필요한 사용자 응답

세 결정은 `1A/2C/3A`로 완료됐고 Fable 5가 preview 전문을 작성했다. Change 002에 따라 GPT-5.6 SOL이 내용·제품 방향 review를 한 번 수행했으며, Fable 재작성과 추가 review는 요청·권고하지 않았다. Phase B와 게시는 현재 목적에서 제외한다.

## 9. Review 판정

- P0/P1: `0/0`
- P2: `0` — `docs/02`·`docs/04` drift는 향후 canonical 게시를 선택할 때만 별도 governance Finding으로 재평가
- P3: `0`
- planningReviewComplete: true
- planningApproved: true
- implementationApproved: true
- reviewGate: `CONTENT_REVIEW_COMPLETE`
