# TASK-NOTIFY-POLICY-001 — Codex 기획 내용 검토

- reviewOwner: `CODEX`
- reviewedArtifact: `tasks/notify-policy-001-planning.md`
- reviewStatus: `COMPLETED`
- verdict: `APPROVE_WITH_RESOLUTIONS`
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

## 1. 결론

Fable 기획의 큰 방향과 구현 순서는 유지한다. 사용자가 확정한 수신자·채널 정책, 외부 발송 실패 격리, Teams 공용 채널 중단, 부서장 fallback, 일정 동기화와 PWA의 인앱 원본 일치가 제품 방향과 맞는다.

다만 Fable이 이미 확정된 18단계 채널과 기존 미완료 업무 처리를 다시 미결정으로 올렸고, 부서장 fallback과 일정 연결의 실제 코드 영향 범위를 좁게 잡았다. 아래 resolution을 기획과 함께 구현 계약으로 사용해야 하며 Fable 원문은 수정하지 않는다.

## 2. 유지

| 항목 | 판단 | 근거 |
| --- | --- | --- |
| 이벤트별 수신자·채널 매트릭스 | 유지 | 사용자 7개 round 결정과 일치한다. 단, 18단계 표시는 3.1 resolution을 적용한다. |
| Teams 공용 채널 신규 delivery 중단 | 유지 | 현재 긴급 알림의 공용 채널 소음을 제거하되 과거 이력·handler·설정은 보존할 수 있다. |
| 복수 부서장에게 개별 업무 표시 후 동기화 종료 | 유지 | 기존 단일 `assigned_user_id` 모델을 전면 교체하지 않고 사용자 요구를 충족한다. |
| 원본 일정 저장 시 `due_date` 즉시 동기화 | 유지 | 주기 worker보다 반영 지연과 D-1 오판 위험이 작다. |
| 실제 provider·Persistent UAT·배포 별도 승인 | 유지 | local 구현·자동 검증과 운영 발송 경계를 분리한다. |
| 신규 화면 없음 | 유지 | 기존 알림 센터·내 업무·알림 설정 화면으로 검수할 수 있다. |

## 3. 반드시 추가·수정할 resolution

### 3.1 18단계는 사용자 결정대로 `메일만`

- Fable 표의 `18단계 최종 완료 — 인앱 O(기록 원본)`은 사용자 결정과 충돌한다.
- 사용자 화면의 인앱 수신자, Teams Activity, PWA push는 만들지 않는다.
- Backend에는 감사·재시도 근거가 되는 내부 notification/event 원본을 남기되 `RecipientOnly` + 인앱 recipient 0명으로 사용자에게 노출하지 않는다.
- 그 원본에서 활성 영업부서 사용자 전원에게 Mail delivery만 생성한다.
- 따라서 Fable 16장 2번은 선택 질문이 아니며 위 내용으로 확정한다.

### 3.2 부서장 fallback은 한 Store가 아니라 모든 업무 생성 경로에 적용

- 현재 담당자 해석은 `WorkflowStore` 외에도 품질, 자재, 물류와 관련 전용 업무 Store에 분산되어 있다. 일부 경로는 프로젝트 담당자 다음에 일반 역할 사용자 또는 시스템 관리자를 한 명 선택한다.
- 공통 resolver가 `업무 책임 → 부서`를 해석하고 `프로젝트 정/부담당 → 해당 부서 활성 부서장 전원`을 반환하도록 중앙화한다.
- 적용 대상을 `WorkflowStore`에 한정하지 않고 실제 `work_items`를 생성하는 품질·자재·구매·제조·물류 경로 전체에서 검색·검증한다.
- 해당 부서에 활성 부서장이 0명이면 영업·관리자에게 넘기거나 조용히 누락하지 않는다. 이전 단계 mutation을 명확한 validation/conflict로 중단하고, 사용자에게 어느 부서의 부서장 등록이 필요한지 표시한다.

### 3.3 복수 부서장 업무의 데이터·idempotency 계약 고정

- 기존 `work_items.idempotency_key`는 논리 업무 한 건 기준이므로 부서장별 개별 row 생성 시 충돌한다.
- additive migration으로 논리 fallback 묶음 식별자를 추가하고, 부서장별 row는 `논리 키 + 사용자 식별자`로 중복을 막는다.
- 한 명의 완료가 같은 묶음의 `Requested/InProgress` row를 같은 transaction에서 종료해야 한다. 최초 처리자와 나머지 자동 종료 사유를 감사 가능하게 남긴다.
- 전용 화면이 단계의 모든 업무를 일괄 완료하는 기존 경로도 같은 묶음 규칙과 충돌하지 않게 해야 한다.
- 진행률·업무 수 집계는 fallback 복제 row 수로 부풀지 않도록 `논리 묶음 1건`으로 계산하거나 기존 단계 상태 계산이 row 수에 의존하지 않음을 테스트로 증명한다.

### 3.4 예정일 source mapping과 backfill은 이미 결정됨

- 생산 업무의 완료 기한은 현재 계획 모델의 `planned_end_date`를 사용한다. 시작일은 업무 시작 참고값이며 `due_date`로 쓰지 않는다.
- 구매·자재 입고 업무는 해당 구매품의 `expected_receipt_date`를 사용한다. 프로젝트 단위 입고 업무처럼 여러 구매품을 대표하는 업무는 아직 미입고인 필수 구매품 중 가장 이른 입고예정일을 사용한다.
- 정확한 source relation을 찾을 수 없는 업무는 `null`을 유지하고 프로젝트 납기일로 대체하지 않는다.
- 기존 미완료 업무도 정확한 원본이 연결되는 경우에만 1회 backfill한 뒤 원본 변경을 따라 갱신한다. 완료·취소 업무와 연결 불명확 업무는 수정하지 않는다.
- 따라서 Fable 16장 3번은 사용자 Round 7 결정에 따라 권장안 A로 확정한다.

### 3.5 기한·Digest의 기존 계약을 불필요하게 다시 만들지 않음

- D-1은 기존 `BusinessDayCalculator`의 이전 영업일 규칙을 재사용한다. L1은 기한을 넘긴 첫 평가이며 L2·L3 신규 발송만 중단한다.
- L2·L3 코드·과거 상태·감사 이력은 삭제하지 않고 신규 평가·delivery 생성과 설정 노출을 중단한다.
- Daily Digest의 기존 `내 담당 프로젝트 요약`은 실제 내용이므로 유지한다. 활성 프로젝트 담당자라는 이유만으로 digest 대상이 되는 현재 동작은 빈 메일 결함이 아니다.
- 이번 보정은 한국 업무일 기준 비업무일 미발송과, 렌더링할 section이 실제로 0개일 때 미생성되는지를 확인하는 범위로 제한한다.

### 3.6 Pending 종결과 일괄 알림의 기준 고정

- Pending 종결 수신자는 종결 시점의 현재 역할을 다시 계산하지 않고, 등록 알림에 저장된 `notification_recipients` snapshot을 사용한다.
- 일괄 업무는 패널별 `work_items`를 유지하되 같은 transaction/correlation의 프로젝트·단계·수신자 조합당 notification 원본을 한 건만 만든다.
- Teams와 PWA는 그 notification recipient를 파생하므로 채널별로 별도의 묶음 판단을 만들지 않는다.

### 3.7 PWA branch 통합 순서

- `TASK-PWA-PUSH-001` local commit은 현재 이 branch 기준선에 포함되지 않는다. 두 Task 모두 notification delivery 경로를 수정하므로 정책 Task 구현·검증을 먼저 완료한 뒤 PWA commit을 최신 정책 branch에 rebase 또는 제한적으로 이식하고 통합 회귀한다.
- 이 review는 push·PR·merge·main 반영을 승인하지 않는다.

## 4. 보류

| 항목 | 보류 사유 | 재개 조건 |
| --- | --- | --- |
| 실제 Teams·메일·PWA provider smoke | 실제 수신자·운영 외부 상태를 변경한다. | 자동 검증·사용자 검수 뒤 별도 provider 승인 |
| Azure 공개 배포 | 현재는 기능 기획·구현 승인 전이다. | main merge와 공개배포 명시 승인 |
| 알림 세부 문구 고도화 | 사용자가 후속 기획으로 분리했다. | 별도 Task 승인 |

Fable 16장 1번은 비차단 deferred 운영 항목으로 유지하며 현재 구현 결정을 요구하지 않는다.

## 5. 제거

| 항목 | 제거 이유 |
| --- | --- |
| 18단계 영업팀 인앱 노출 | “영업팀 전체 메일만”이라는 사용자 결정 위반 |
| Daily Digest에서 담당 프로젝트 요약 제거 | 기존 확정 기능이며 실제 digest 내용이다. 이번 정책의 “빈 내용 미발송”과 충돌하지 않는다. |
| 기존 L2·L3 schema·이력 물리 삭제 | 향후 감사·rollback을 훼손하며 운영 중단에는 필요하지 않다. |
| 공유 work item 1건 + 다중 owner 전면 모델 변경 | 모든 업무 조회·권한을 바꾸는 과도한 범위다. |

## 6. 권장 개발 순서

1. 이벤트·수신자·채널 매트릭스와 17/18단계 분리
2. Pending 등록·종결·재검사·제조 중단 통합과 일괄 알림 묶음
3. 공통 담당자 resolver와 복수 부서장 fallback 묶음 migration·완료 동기화
4. 일정 source mapping·미완료 backfill·저장 시점 동기화
5. L0/L1·Digest·preference catalog 정리
6. Backend/Frontend/Full-Stack 회귀와 독립 검증
7. PWA commit 통합 후 인앱 원본과 push 수신자·묶음 일치 회귀

## 7. 완료 조건 보정

- 채널별 event matrix test가 18단계의 인앱·Teams·PWA 0건과 영업팀 Mail delivery를 확인한다.
- 모든 업무 생성 resolver가 관리자·영업 fallback을 사용하지 않는다는 search/test evidence를 남긴다.
- 부서장 0명·1명·여러 명, 동시 완료, 전용 화면 일괄 완료와 진행률 회귀를 포함한다.
- 일정 source가 명확한 신규·기존 미완료 업무의 생성·변경·삭제(null 전환), 완료 이력 불변을 검증한다.
- 평일/주말/등록 공휴일, 내용 있음/없음 Digest를 검증한다.
- 실제 외부 provider 발송 수는 자동 검증에서 0이어야 한다.

## 8. 최종 판단

- 기획 방향: 승인 권고
- Codex resolution 반영 필요: Yes
- 추가 사용자 정책 질문: 없음
- 구현 시작 조건: 사용자가 Fable 기획과 이 review resolution을 승인해야 함
- 실제 provider·Persistent UAT·push·PR·merge·배포: 미승인

- reviewResolutionStatus: `READY_FOR_USER_APPROVAL`
- implementationApproved: false
