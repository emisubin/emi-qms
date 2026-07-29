All baseline checks are complete: the interview is `COMPLETED_CONFIRMED` with decisions 1-B·2-A·3-B·4-A, Roadmap chapter 9 policies match those decisions, and the experiment worktree contains the 007A Pending baseline (project-only linkage, 5 forward-only statuses), the server-side workflow progress with FAT-optional denominator, and coarse 7-band panel stages. Below is the single primary planning draft artifact.

# TASK-007B — 패널·프로젝트 병목 상태 집계 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/007b-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- authoringModel: `FABLE_5`

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 프로젝트 workflow 진행률과 open Pending이 별도 화면에 흩어져 있어 여러 패널 중 어느 단계가 전체 납기를 막는지 즉시 판단할 수 없고, 사용자가 프로젝트 상세·패널 상세·Pending 목록을 오가며 수동으로 병목을 추정한다.
- 대상 사용자·역할: 생산관리(병목 확인과 후속 업무 진입), 일반 업무 역할(담당 프로젝트의 차단 원인 확인), Read-only·관리자 감사 역할(허용 범위의 병목 조회). 세 역할 모두 집계 자체로 조회·변경 권한이 넓어지지 않는다.
- 정상 흐름: 프로젝트 조회 → 패널별 필수 단계와 open Pending 평가 → 대표 병목 표시 → 원본 패널 또는 Pending으로 이동.
- 예외·복구 흐름: 집계 대상 stage·FAT 규칙이 불완전하거나 일부 원본 조회가 실패하면 거짓 완료 대신 설명 가능한 미확정 상태를 표시한다. aggregate는 파생값이며 원본 workflow·Pending을 수정하지 않고, 재조회 시 최신 authoritative snapshot을 반영한다.
- 확정한 정책과 명시적 제외:
  - 결정 1-B: 대표 병목은 항상 가장 뒤처진 필수 단계로 표시하고 open Pending은 별도 차단 배지·건수로 병기한다(Roadmap 9.3·9.6 유지). "다음 확인 대상" 우선순위(목록 정렬·강조·상세 첫 안내)는 open Pending 차단 → 가장 뒤처진 필수 단계 순으로 계산하고 정렬 근거 문구를 함께 표시한다.
  - 결정 2-A: Pending 차단은 프로젝트 수준에서만 반영한다. 패널 병목은 단계 기준으로만 계산하고 Pending은 프로젝트 배지·건수·목록 deep link로 연결한다. Pending model·migration 변경 없음. 패널 단위 차단 귀속(Roadmap 9.5 blocked flag)은 후속 Task.
  - 결정 3-B: `Closed`를 제외한 모든 Pending 상태를 open 차단으로 집계하고(완료 조건 9.7과 동일 정의), `ReinspectionRequested`는 "재검사 대기"로 배지 안에서 구분 표기한다. 긴급도는 병기 정보다.
  - 결정 4-A: 가장 뒤처진 단계에 패널이 여러 면이면 "단계명 + 패널 n면"으로 묶어 표시하고 상세에서 해당 단계 패널 목록으로 진입한다. 1~4단계(프로젝트 단위)가 미완료면 그 프로젝트 단계 자체를 대표 병목으로 표시한다.
  - 명시적 제외: Home widget, 관리자용 Pending 유형 편집, workflow stage 번호·진행률 공식 변경, 패널 단위 Pending 귀속, actual Teams/Mail provider 발송, Persistent UAT migration·write·runtime handover.
- planning으로 넘긴 비차단 미결정 사항: 계산형 조회 우선 원칙 아래 persisted snapshot 필요 여부 비교(이 문서 12절 대안 비교와 16절 사용자 결정 항목으로 전달).

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

## 1. 한 줄 목표

사용자가 프로젝트 목록·상세에서 "어느 단계에서 몇 면이 막혀 있고, open Pending이 몇 건인지"를 설명 가능한 한 상태로 보고, 한 번의 이동으로 해당 패널 목록 또는 Pending 목록에 도달할 수 있게 한다.

## 2. 배경과 해결할 업무 문제

- 현재 업무: 프로젝트 목록은 서버가 계산한 대표 업무 상태와 진행률을 표시하고, 프로젝트 상세의 Workflow 화면은 18단계 stage별 상태를, `/pending` workspace는 open Pending 목록·집계를 각각 제공한다.
- 시간 손실·누락 지점: 세 화면의 정보가 결합되지 않아 "진행률이 높은데도 차단 이슈가 있는 프로젝트", "여러 패널 중 가장 뒤처진 단계"를 찾으려면 화면을 오가며 수동 대조해야 한다.
- 현재 우회 방식: 프로젝트 상세, 패널 목록, Pending 목록을 순차 확인하는 수동 판단.
- 기능이 없을 때의 영향: 진행률 수치만 보고 중요한 차단 이슈를 놓치거나, 여러 패널 중 지연 원인을 찾는 데 반복적인 탐색 시간이 든다.

### 확인된 구현 기준선 (Repository 재검증 결과)

- 프로젝트 workflow는 서버가 18단계 stage 상태와 필수 단계 기반 진행률을 계산하며, FAT 불필요 프로젝트는 FAT 단계를 분모에서 제외한다. 완료되지 않은 가장 이른 필수 단계를 현재 단계로 반환한다.
- 프로젝트 목록 응답은 이미 프로젝트 단위 대표 업무 상태와 진행률 필드를 포함한다. 이 목록 계산은 1~4단계 gate까지만 세분 판정하고 그 이후는 하나의 값으로 묶는다.
- 패널은 개별 18단계 번호가 아니라 7개 구간값(제조 전 → 제조 중 → 제조 완료 → 검사 중 → 검사 완료 → 포장 완료 → 출하 완료)의 workflow 구간을 가진다. 즉 Roadmap 9.2의 "패널별 18단계 현재 단계"를 패널 원본 데이터만으로 완전한 단계 번호 하나로 환원할 수는 없다.
- 실험 Pending(TASK-007A)은 forward-only 상태 5종(`Registered → ActionRequested → InProgress → ReinspectionRequested → Closed`), 긴급도 2종(`Normal/Urgent`), 프로젝트 단위 연결만 가지며, 목록 API 필터는 상태·유형·긴급도뿐이고 프로젝트 필터는 아직 없다. 권한은 `Pending.Read`/`Pending.Manage`로 분리되어 있다.
- Frontend는 수동 pathname router와 단일 App 구조이며 `/pending`, `/pending/{id}` route와 프로젝트 목록·상세의 상태·진행률 표시가 이미 있다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 생산관리 | 프로젝트·패널 병목 확인, 차단 우선 정렬로 다음 확인 대상 선정, Pending·패널로 이동 | 기존 프로젝트·Pending 조회 범위 | 집계 자체는 변경 없음 (원본 workflow·Pending 권한은 기존 규칙 유지) |
| 일반 업무 역할 | 담당 프로젝트의 대표 병목과 차단 원인 확인 | 기존 프로젝트·Pending 조회 범위 | 집계 자체는 변경 없음 |
| Read-only·System Administrator | 허용 범위의 병목·차단 현황 감사 조회 | 기존 조회 권한 범위 | 없음 (집계로 mutation 권한 확대 금지) |

권한 규칙: 병목 aggregate의 workflow 파생 필드는 기존 프로젝트 조회 권한을, Pending 파생 필드(open 건수·재검사 대기·긴급 병기)는 `Pending.Read`를 각각 따른다. 권한이 없는 원본에서 파생된 값은 응답에서 제외하고 화면은 "표시할 수 없음"이 아닌 해당 정보 미제공 상태로 처리해 원본보다 넓은 정보를 노출하지 않는다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — 생산관리가 다음 확인 대상을 찾는다

1. 생산관리 사용자가 프로젝트 목록을 연다.
2. 시스템이 각 프로젝트에 대표 병목(단계명 + 패널 n면), 진행률, open Pending 차단 배지(건수·재검사 대기 구분·긴급 병기)를 표시하고, 목록을 "open Pending 차단 → 가장 뒤처진 필수 단계" 우선순위로 정렬하며 정렬 근거 문구를 함께 보여 준다.
3. 사용자는 최상단 프로젝트의 차단 배지를 통해 그 프로젝트로 필터된 Pending 목록으로 이동해 조치 상태를 확인한다.

### 시나리오 B — 프로젝트 상세에서 병목 원본으로 진입한다

1. 사용자가 프로젝트 상세를 연다.
2. 시스템이 상세 상단에 "다음 확인 대상" 안내(open Pending이 있으면 Pending 우선, 없으면 대표 병목 단계)와 단계별 패널 분포 matrix(어느 구간에 몇 면이 있는지)를 표시한다.
3. 사용자는 대표 병목 항목에서 해당 단계 구간의 패널 목록으로, 차단 배지에서 프로젝트 필터된 Pending 목록으로 이동한다.

### 시나리오 C — 계산 불가 상황이 거짓 완료로 보이지 않는다

1. 활성 패널이 없거나 일부 원본 조회가 실패한 프로젝트를 사용자가 조회한다.
2. 시스템이 대표 병목 대신 "병목 없음(모든 필수 단계 완료)", "데이터 없음", "일부 계산 불가(미확정)" 중 해당 상태를 짧은 text label과 사유로 구분 표시한다.
3. 사용자는 미확정 상태의 사유 안내를 보고 원본 화면에서 실제 상태를 확인한다.

## 5. 기능 요구사항

### 필수

- [ ] 상태 matrix: 프로젝트 단위 단계(1~4)의 완료 판정, 패널 workflow 구간(7종)과 18단계 필수 단계의 대응 관계, open Pending 반영 규칙을 하나의 고정 문서화된 매핑으로 정의하고 Backend가 이 매핑만으로 대표 병목을 계산한다.
- [ ] 패널·프로젝트 대표 병목 aggregate: 1~4단계 미완료면 그 프로젝트 단계를, 5단계 이후면 가장 뒤처진 패널 구간을 "단계(구간)명 + 패널 n면"으로 계산한다. 동률 패널은 개별 지목 없이 묶는다.
- [ ] open Pending 차단 반영: `Closed` 제외 전부를 open으로 집계하고 재검사 대기(`ReinspectionRequested`) 건수를 구분하며 긴급(`Urgent`) 건수를 병기한다. 프로젝트 수준 귀속만 사용한다.
- [ ] "다음 확인 대상" 우선순위: open Pending 차단 → 가장 뒤처진 필수 단계 순. 프로젝트 목록 정렬·강조와 상세 첫 안내에 적용하고 정렬 근거 문구를 표시한다.
- [ ] 기존 진행률 공식과 FAT optional 분모 재사용: 진행률 수치는 기존 계산 결과를 그대로 병기하고 재구현하지 않는다.
- [ ] 프로젝트 목록·상세의 설명 가능한 병목 표시와 원본 deep link: 대표 병목 → 해당 구간 패널 목록, 차단 배지 → 프로젝트 필터된 Pending 목록.
- [ ] Pending 목록 조회의 additive 프로젝트 필터(읽기 전용 query 필터와 화면 필터): deep link 착지에 필요한 최소 확장이며 Pending model·상태·권한은 변경하지 않는다.
- [ ] 미확정 상태: 알 수 없는 구간값, 필수 단계 목록 불완전, 일부 원본 조회 실패 시 거짓 완료 대신 사유가 있는 미확정 표시.
- [ ] 권한 경계: workflow 파생 필드는 프로젝트 조회 권한, Pending 파생 필드는 `Pending.Read`를 따르고 부족하면 해당 필드를 제외한다.

### 선택

- [ ] 프로젝트 목록의 병목 우선 정렬을 사용자가 기존 정렬(상태 → 납기일)로 전환할 수 있는 정렬 선택 UI. 기본값은 결정 1-B의 차단 우선 정렬이다.

### 명시적 제외

- [ ] Home widget (TASK-HOME-001 범위)
- [ ] 관리자용 Pending 유형 편집
- [ ] workflow stage 번호·진행률 공식 변경
- [ ] 패널 단위 Pending 귀속과 Roadmap 9.5 패널 blocked flag (007A 후속 확장 Task)
- [ ] 패널 원본 model에 18단계 번호를 추가하는 데이터 확장
- [ ] actual Teams/Mail provider 발송, 신규 알림 생성
- [ ] Persistent UAT migration·write·runtime handover
- [ ] 대표 repo·GitHub main의 push·PR·merge

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| 프로젝트 목록 | 기존 목록 메뉴 | 기존 상태·진행률 + 대표 병목(단계명·패널 n면) badge + 차단 badge(open n건 · 재검사 대기 n건 · 긴급 n건) + 정렬 근거 문구 | badge/link로 상세·Pending 이동, 정렬 전환(선택 요구사항) | loading/empty/error 구분, 병목 없음·데이터 없음·미확정을 각각 다른 짧은 label로 표시 |
| 프로젝트 상세 | 목록에서 진입 | 상단 "다음 확인 대상" 안내(차단 우선), 단계별 패널 분포 matrix, 차단 badge | 대표 병목 → 해당 구간 패널 목록, 차단 badge → 프로젝트 필터된 Pending 목록 | 원본 조회 일부 실패 시 미확정 사유 표시, 거짓 완료 금지 |
| Pending 목록 | 상세·목록의 차단 badge deep link | 기존 목록·집계 + 프로젝트 필터 적용 상태 표시 | 필터 해제·변경, 기존 Pending 행동 | 기존 PendingPage feedback 계약 유지 |

확인할 UX 항목:

- 사용자가 현재 상태를 이해할 수 있는가 — 대표 병목·차단·미확정을 색이 아닌 짧은 한글 label로 구분한다.
- 다음 행동이 명확한가 — "다음 확인 대상" 안내가 항상 하나의 focus 가능한 link를 제공한다.
- 저장·변경 결과가 action 근처에 보이는가 — 이 기능은 조회 전용이므로 mutation feedback은 원본 화면 계약을 따른다.
- 권한 부족·검수 전용·오류 상태가 명확한가 — Pending 파생 정보가 권한으로 제외된 경우와 조회 오류를 구분한다.
- 좁은 화면에서도 핵심 행동이 가능한가 — 390px·Teams narrow에서 badge와 link가 한 열 layout으로 동작하고 page-level horizontal overflow가 없어야 한다.

## 7. 업무 규칙과 불변조건

- 프로젝트 상태는 서버 계산값이며 어떤 사용자도 직접 변경할 수 없다(Roadmap 9.1). aggregate는 조회 시점 파생값으로 원본 workflow·panel·Pending 데이터를 수정하지 않는다.
- 기존 18단계 번호, 필수 단계 판정, 진행률 공식과 FAT optional 분모를 그대로 재사용하며 변경하지 않는다.
- 대표 병목의 "표시"는 항상 가장 뒤처진 필수 단계(또는 1~4 프로젝트 단계)이고, open Pending은 상태값을 대체하지 않는 별도 배지다. 프로젝트 상태값을 "중단"으로 바꾸지 않는다(9.6).
- open 판정은 `Closed` 제외 전부이며 프로젝트 완료 조건(9.7)의 open 정의와 항상 일치한다.
- 단계는 전진만 한다는 원칙(9.5)을 훼손하는 어떤 되돌림·보정도 aggregate에서 수행하지 않는다.
- 계산 불가능한 입력(알 수 없는 구간값, 필수 단계 집합 불완전, 원본 조회 실패)은 완료·정상으로 간주하지 않고 미확정으로 구분한다.
- Pending forward-only 상태·권한·audit 계약(TASK-007A)을 변경하지 않으며, aggregate 경로로 mutation 권한이 확대되지 않는다.
- Backend가 권한과 판정의 authoritative layer다. Frontend badge는 서버 응답 값만 표시하고 자체 재계산하지 않는다.

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 프로젝트 workflow stage 상태·진행률 | 18단계 stage별 상태, 필수 단계·FAT 분모, 현재 단계 | 기존 | 기존 workflow event가 audit 원본 |
| 패널 workflow 구간 | 패널당 7종 구간값(제조 전~출하 완료) | 기존 | 기존 panel audit 유지 |
| Pending 이슈 | 프로젝트 연결, 상태 5종, 긴급도 2종 | 기존 | 기존 Pending history가 audit 원본 |
| 병목 aggregate | 대표 병목(단계 또는 구간 + 패널 수), open/재검사 대기/긴급 건수, 다음 확인 대상, 미확정 사유 | 신규(파생, 저장하지 않음 — 권장안 기준) | 파생값이므로 자체 audit 없음. 근거는 원본 이력 |
| 상태 matrix 매핑 | 패널 구간 ↔ 18단계 필수 단계 구간, 1~4단계 판정, open 판정 predicate의 고정 정의 | 신규(코드·문서 상수) | 매핑 변경은 Task change로 추적 |

상태 표현(파생값이므로 독립 상태 전이 없음):

```text
원본(workflow·panel·Pending) 조회 성공
  → 병목 있음(단계·구간 + 패널 n면 [+ open Pending 차단])
  → 병목 없음(모든 필수 단계 완료)
  → 데이터 없음(활성 패널 0 등)
원본 조회 실패·매핑 불가 → 미확정(사유 코드)
```

패널 구간과 18단계의 대응은 단계 "구간"으로만 설명한다(예: 검사 진행 구간은 자체검수~FAT 범위). 구간을 임의로 단일 단계 번호로 환원해 표시하지 않고, 구간 label과 프로젝트 workflow stage 상태를 함께 사용해 표시 단계를 결정한다. 이 대응표 전체가 "상태 matrix" 산출물이며 구현 전 문서로 고정한다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 대표 병목 계산, open 판정, 다음 확인 대상 우선순위, 권한별 필드 제외. Frontend는 표시만 담당한다.
- 필요한 조회와 mutation: 조회 전용. (1) 프로젝트 목록 응답에 병목 aggregate 필드 추가, (2) 프로젝트 상세(또는 기존 workflow 조회)에 패널 분포 matrix·다음 확인 대상 추가, (3) Pending 목록 조회에 additive 프로젝트 필터. 신규 mutation 없음.
- 권한·validation: 기존 프로젝트 조회 authorization과 `Pending.Read`를 재사용한다. 새 permission code를 만들지 않는다. 필터 파라미터는 서버에서 검증하고 잘못된 값은 안정적인 400 계약으로 반환한다.
- transaction·동시성·idempotency: 조회 전용이므로 mutation transaction이 없다. workflow·Pending 갱신 직후 재조회가 최신 committed 상태를 반영하도록 단일 조회 경로에서 함께 읽는다. 저장하지 않으므로 stale snapshot 무효화 문제가 없다(권장안 기준).
- audit trail: 파생값은 audit를 새로 만들지 않는다. 근거 추적은 기존 workflow event·panel audit·Pending history를 사용한다.
- 외부 provider 영향: 없음. 알림·delivery·worker 경로를 호출하지 않는다.

Repository 조사 전 내부 클래스명, 컬럼명과 SQL 형태를 확정하지 않는다. 기존 프로젝트 목록·workflow·Pending 조회 경로와 계약 패턴(mutation result, label 병기, 한글 label)을 재사용한다.

## 10. Frontend 고려사항

- route/component: 신규 route 없음. 기존 프로젝트 목록·상세 화면과 PendingPage에 badge·안내·필터를 추가한다. 수동 pathname router 계약을 유지하고 deep link는 기존 `/pending`과 프로젝트 상세 경로에 query·상태로 연결한다.
- loading/empty/error/success: 병목 없음, 데이터 없음, 미확정(사유), open Pending 차단을 서로 다른 label로 구분하고 loading·조회 오류와 혼동하지 않는다.
- 공통 Action Feedback: 조회 전용 기능이므로 mutation feedback 계약 변경 없음. 필터 적용·해제 상태를 목록 근처에 표시한다.
- 접근성: badge는 색+text label을 함께 사용하고, "다음 확인 대상" link는 keyboard focus·명확한 이름을 가진다. 정렬 근거 문구는 screen reader가 읽을 수 있는 위치에 둔다.
- 390px/mobile/narrow pane: badge·matrix가 한 열 card로 전환되고 page-level horizontal overflow 0을 유지한다. 표 형태 matrix는 필요 시 내부 scroll을 사용한다.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: 프로젝트 목록·상세 표시를 확장하고 내 업무·알림 생성 경로는 변경하지 않는다. 신규 알림을 만들지 않는다.
- 권한/관리자: 기존 permission code 재사용. 관리자·Read-only는 조회만 가능하며 aggregate로 mutation이 열리지 않는다.
- Excel/PDF/첨부: 영향 없음 — import/export·첨부 계약을 변경하지 않는다.
- Teams/Mail: 영향 없음 — delivery·provider 경로를 사용하지 않는다.
- 삭제·복구/감사: soft-delete된 프로젝트는 기존 목록 규칙을 따르며 aggregate 대상에서 제외된다. 감사 원본은 기존 이력을 사용한다.

## 12. 후보 구현안과 대안

### 12.1 aggregate 저장 방식 (interview에서 deferred된 비차단 결정)

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A | 계산형 조회: 목록·상세 조회 시점에 원본에서 매번 파생 계산하고 저장하지 않는다 | migration 0건, 원본과 항상 일치(9.1의 서버 도출 원칙과 부합), rollback이 코드 제거로 끝남, 갱신 직후 재조회 요구를 자동 충족 | 프로젝트·패널 수가 크게 늘면 목록 조회 비용 증가. 다만 현재 목록 조회도 유사한 파생 계산을 이미 수행 중 |
| B | persisted snapshot: 파생 결과를 별도 table에 저장하고 workflow·Pending 변경 시 재계산 | 대량 데이터에서 목록 조회가 빠름, Home widget 등 후속 재사용 용이 | additive migration·재계산 trigger·stale 위험·이중 상태 관리가 추가되고 "aggregate 자체의 독립 상태 전이를 두지 않는다"는 interview 방향과 어긋남. 이번 실험 범위 대비 과설계 |

**권장안: A (계산형 조회).** 현재 데이터 규모와 실험 경계에서 저장형의 이점이 없고, 원본 미변경·즉시 일관성·rollback 단순성이 모두 A에 유리하다. 성능 한계가 실측되면 후속 Task에서 B를 별도 승인으로 도입한다. 최종 확정은 16절 사용자 결정 1번이다.

### 12.2 API 표면 (구현 세부 대안, 사용자 결정 불필요)

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A | 기존 프로젝트 목록·상세 응답에 aggregate 필드를 추가 | 화면당 요청 1회 유지, 기존 권한 경계 재사용, 목록 정렬을 서버가 일관 계산 | 목록 응답 계약이 커져 기존 소비자 호환성 확인 필요 |
| B | 별도 병목 aggregate endpoint를 신설하고 화면에서 병합 | 기존 계약 불변 | 요청 2회·클라이언트 병합 정렬로 authoritative 정렬 원칙이 약해지고 권한 분기 중복 |

**권장안: A.** 정렬·강조가 서버 계산이어야 하는 결정 1-B와 기존 응답 확장 패턴(목록에 이미 파생 상태·진행률 존재)에 부합한다. 기존 응답 type 소비자의 호환성 확인과 test fixture 갱신을 구현 범위에 포함한다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. isolated synthetic 환경(tmpfs PostgreSQL full-stack script)만 사용한다.
- migration 필요 여부: 권장안 A 기준 0건. 사용자 결정 1번이 B(persisted snapshot)로 바뀌면 additive migration 설계와 별도 승인이 필요하며 이 planning 범위를 벗어난다.
- 외부 발송/실제 데이터 영향: 없음. 알림·delivery·provider를 생성·호출하지 않는다.
- runtime 교체 여부: 없음. canonical runtime과 대표 repo를 변경하지 않는다.
- 추가 사용자 승인 필요 작업: push·PR·GitHub main merge(현재 미승인, main merge 승인 0/3), Persistent UAT 적용, persisted snapshot 전환. 실험 branch 안의 구현·local commit은 interview에 기록된 사용자 실행 경계로 승인되어 있다.

## 14. 검증 계획

- 최소 테스트 (isolated Backend integration):
  - 1~4단계 미완료 프로젝트의 대표 병목이 프로젝트 단계로 판정되는지
  - 필수 단계 partial/all과 FAT optional 분모(FAT 필요/불필요 프로젝트)의 진행률·병목 일치
  - 여러 패널 동률 시 "구간 + 패널 n면" 묶음과 가장 뒤처진 구간 선정
  - open Pending 판정: `Closed` 제외 전부 open, 재검사 대기 구분, 긴급 병기, 차단 우선 정렬 반영
  - 미확정 판정: 알 수 없는 구간값·활성 패널 0·원본 부분 실패에서 거짓 완료가 없는지
  - authorization: 프로젝트 조회 권한·`Pending.Read` 유무별 필드 제외, Pending 프로젝트 필터의 권한·validation
- 영향 영역 회귀: Backend 전체 test, 기존 프로젝트 목록·workflow·Pending test 회귀, Frontend typecheck·lint·unit·build, 기존 Pending full-stack E2E 회귀와 신규 병목 E2E(목록 badge·정렬·deep link·390px overflow 0).
- PR/CI: 이번 실험 경계에서는 생성하지 않는다(별도 승인 대상). local 검증 결과를 implementation report에 기록한다.
- 사용자 검수: Desktop·390px synthetic screenshot으로 용어(병목·차단·재검사 대기·미확정 label), 우선순위(정렬·첫 안내), 화면 밀도를 확인한다. Persistent UAT와 실제 provider는 사용하지 않는다.

## 15. 완료 기준

- 기능/권한/데이터: 5절 필수 요구사항이 서버 authoritative로 동작하고, 원본 model·권한·audit 변경이 diff에 없음이 확인된다.
- UX: 목록·상세·Pending deep link가 Desktop과 390px에서 동작하고 병목 없음/데이터 없음/미확정/차단이 구분 표시된다.
- 자동 테스트: 14절 최소 테스트와 회귀가 모두 통과하고 미실행 항목은 사유와 함께 분리 기록된다.
- 5종 산출물: `docs/12-task-completion-policy.md`의 상태·위치 추적 규칙을 따른다(실험 전용 항목은 N/A 사유 기록).
- 사용자 검수 상태: 자동 검증 완료와 사용자 검수 완료를 별도 상태로 기록하고, screenshot 기반 사용자 판정 전에는 완료로 선언하지 않는다.
- PR 상태: N/A — push·PR·merge 미승인. 대표 repo 채택은 canonical gate(승인 3회 포함)를 별도로 따른다.

중단 조건:

- 구현 중 패널 원본에 18단계 번호 추가, Pending model 확장, 진행률 공식 변경이 필요하다고 판단되면 구현을 중단하고 범위 재분류(NEW_FEATURE 확장 또는 후속 Task)를 보고한다.
- 상태 matrix가 Roadmap 9장과 충돌하는 판정을 요구하면 임의 선택하지 않고 blocking decision으로 보고한다.
- 기존 목록 응답 소비자와의 호환성 회귀가 해소되지 않으면 게시 판단 전에 중단하고 보고한다.

## 16. 미결정 사항

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 병목 aggregate를 계산형 조회로 유지할지, persisted snapshot으로 저장할지 (interview에서 deferred된 비차단 결정) | A: 계산형 조회(권장, migration 0건) / B: persisted snapshot(additive migration·재계산 정책·별도 승인 필요) | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 프로젝트 목록·상세(또는 workflow) 조회 경로의 aggregate 계산과 응답 확장, Pending 목록 조회의 프로젝트 필터, 상태 matrix 상수·판정 로직.
- Frontend: 프로젝트 목록·상세의 badge·정렬·안내·matrix 표시, PendingPage 프로젝트 필터 수용, 관련 type·label helper.
- DB/Migration: 권장안 A 기준 없음.
- Tests/Scripts: Backend integration test 추가, Frontend unit test, full-stack E2E spec 추가. 기존 e2e script 재사용.
- Docs: 상태 matrix 정의 문서화(이 Task 산출물 내), implementation report·screenshot. Roadmap 갱신은 실험 경계상 대표 repo 채택 시점으로 유보하고 사유를 추적한다.

## 18. Roadmap 연결

- 선행 Task: TASK-007A Pending List(이 experiment worktree의 실험 구현이 기준선). canonical Roadmap 큐의 순서 예외는 interview Task Identity Gate에 명시적 승인으로 기록되어 있다.
- 후속 Task: 패널 단위 Pending 귀속·blocked flag(9.5) 확장, TASK-HOME-001(이 aggregate 재사용), TASK-MOBILE-001, persisted snapshot 전환(결정 1번이 B일 경우).
- 현재 Go/No-Go: 실험 branch 한정 GO(사용자 2026-07-16 실행 경계). 대표 repo·main 반영은 NO — 승인 0/3.
- 별도 Task로 분리할 항목: Home widget, Pending 유형 관리자 화면, 패널 단위 차단 귀속, 운영 UAT 적용.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| 2026-07-16 | Round 1 권장안 일괄 채택 지시(1-B · 2-A · 3-B · 4-A) | 7절 확정 정책과 5·6절 요구사항에 반영 |
| 2026-07-16 | Round 2 확인 요약 승인, planning·review·구현을 별도 승인 왕복 없이 실험 경계에서 진행 | interview `COMPLETED_CONFIRMED`, 이 planning 작성. push·PR·main merge는 계속 미승인(0/3) |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

아래 초안은 사용자 결정 1번 확정과 Codex 내용 review 이후에만 유효하다.

1. 새 구현 세션에서 instruction chain gate를 수행하고 branch가 experiment worktree인지, 대표 repo가 아닌지 확인한다.
2. 상태 matrix(패널 구간 ↔ 18단계 필수 단계 구간, 1~4단계 프로젝트 판정, open Pending predicate)를 먼저 고정 정의하고, 매핑 불가 입력의 미확정 사유 코드를 함께 정의한다.
3. Backend: 기존 프로젝트 목록·상세 조회 경로에 계산형 aggregate를 추가한다. 기존 진행률·필수 단계·FAT 분모 계산을 재사용하고 재구현하지 않는다. 권한별 필드 제외(프로젝트 조회 권한, `Pending.Read`)를 서버에서 강제한다.
4. Backend: Pending 목록 조회에 additive 프로젝트 필터를 추가하되 model·상태·권한·audit 계약을 변경하지 않는다.
5. 목록 정렬을 서버에서 "open Pending 차단 → 가장 뒤처진 필수 단계 → 기존 정렬"로 계산하고 정렬 근거를 응답에 포함한다.
6. Frontend: 프로젝트 목록·상세에 badge·다음 확인 대상 안내·패널 분포 matrix·deep link를 추가하고, PendingPage가 프로젝트 필터를 수용하게 한다. 색만으로 상태를 전달하지 않는다.
7. 14절 검증 계획의 isolated 테스트와 회귀를 실행하고, Desktop·390px synthetic screenshot을 생성한다. Persistent UAT·실제 provider·push·PR·merge는 수행하지 않는다.
8. 미실행 검증과 Finding을 implementation report에 분리 기록하고 사용자 검수 대기로 종료한다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 1
