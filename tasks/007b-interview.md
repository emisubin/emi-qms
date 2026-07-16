# TASK-007B — 패널·프로젝트 병목 상태 집계 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 2
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 사용자와 진행하는 deep-interview를 round별로 고정한다. Codex는 Fable 질문과 사용자 답변을 전달·기록하지만 업무 질문을 대신 만들거나 답하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## Task Identity Gate

- proposedTaskId: `TASK-007B`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-007B`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: Pending 차단 상태와 필수 workflow 진행률을 이용해 패널과 프로젝트의 대표 병목 상태를 계산하고 사용자가 즉시 다음 확인 대상을 찾게 한다.
- Root Finding 또는 정책 결정: 현재 프로젝트 진행률과 Pending은 별도로 보이므로 여러 패널 중 어느 단계가 전체 납기를 막는지 한눈에 판단할 수 없다.
- 변경·검증 경계: 현재 experiment worktree 안의 상태 matrix, Backend aggregate, 프로젝트 화면 표시, isolated tests와 synthetic screenshot만 포함한다.
- 보존할 불변조건: 기존 18단계 번호와 진행률 공식, Pending forward-only 상태·권한·audit, 대표 repo·GitHub main, Persistent UAT와 실제 provider를 변경하지 않는다.
- 예상 산출물: Fable interview 원문·planning 원문, Codex review, 구현·tests, implementation report와 페이지 screenshot.

### 동일 목적 검색 결과

- `tasks/`, Product Roadmap, Decision Log와 사용자 흐름 문서: Product Roadmap의 canonical `TASK-007B` 한 건만 확인.
- local/remote branch와 worktree: 동일 목적 없음.
- GitHub open/closed PR과 remote branch: 동일 목적 없음.

### 사용자 실행 경계

- 사용자는 현재 experiment worktree에서 다음 Task를 Fable 기획, Codex review, 구현까지 연속 진행하도록 요청했다.
- 사용자는 2026-07-16 별도 승인 왕복 없이 Fable 기획·review·구현 목표까지 진행하라고 다시 명시했다. 이 지시는 Round 1에서 Fable이 제시한 권장안 전체를 실험 기본값으로 채택하는 사용자 결정으로 기록한다.
- local experiment 변경과 commit만 허용 범위이며 push·PR·merge는 미승인이다.
- 대표 repo와 GitHub main merge는 같은 merge 대상에 대한 명시적 승인 3회 전에는 금지하며 현재 승인 횟수는 `0/3`이다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 대기 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 4 | 사용자가 별도 승인 왕복 없이 Fable 권장안을 채택하도록 명시하여 `1-B · 2-A · 3-B · 4-A`로 기록 — [Fable 원문](007b-interview-round-1-fable.md) | Fable 확인 요약 생성 |
| 2 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | 확인 대기 — [Fable 확인 요약 원문](007b-interview-round-2-fable.md) | 사용자가 요약 확인 후 Fable planning |
| 2 확인 | `COMPLETED_CONFIRMED` | 0 | 사용자가 요약을 본 뒤 권장안 채택·review·구현을 별도 승인 없이 진행하라고 명시 | Fable planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 프로젝트 workflow 진행률과 Pending 목록을 각각 확인할 수 있지만 패널·프로젝트 대표 병목을 계산하는 공통 aggregate는 없다.
- 해결할 문제: 여러 패널의 필수 단계 진행과 open Pending 차단을 하나의 설명 가능한 상태로 집계해 우선 확인 대상을 찾는다.
- 현재 우회 방식: 사용자가 프로젝트 상세, 패널 상세와 Pending 목록을 오가며 수동 판단한다.
- 성공했을 때 사용자가 할 수 있는 일: 프로젝트 목록·상세에서 병목 단계와 Pending 차단 여부를 보고 해당 패널 또는 Pending으로 이동한다.
- 하지 않을 경우 영향: 진행률이 높아도 중요한 차단 이슈를 놓치거나 여러 패널 중 지연 원인을 찾는 시간이 계속 든다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 생산관리 | 프로젝트·패널 병목 확인과 후속 업무 진입 | 기존 프로젝트 조회 범위 | 집계 자체는 변경 없음 | 원본 workflow·Pending 권한 유지 |
| 일반 업무 역할 | 담당 프로젝트의 차단 원인 확인 | 기존 프로젝트·Pending 조회 범위 | 집계 자체는 변경 없음 | 접근 불가 원본 정보 노출 금지 |
| Read-only·관리자 감사 역할 | 허용 범위의 병목 조회 | 기존 조회 권한 범위 | 없음 | 집계로 mutation 권한 확대 금지 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 프로젝트 조회 → 패널별 필수 단계와 open Pending 평가 → 대표 병목 표시 → 원본 패널 또는 Pending으로 이동.
- validation 실패: 집계 대상 stage·FAT 적용 규칙이 불완전하면 거짓 완료를 표시하지 않고 설명 가능한 미확정 상태가 필요하다.
- 동시 처리·중복: workflow 또는 Pending 갱신 직후 재조회에서 최신 authoritative snapshot을 반영해야 한다.
- 취소·재시도·복구: aggregate는 파생값이며 원본 workflow·Pending을 수정하지 않는다.
- 부분 실패와 rollback: 한 원본 조회 실패가 전체 프로젝트를 잘못 정상으로 표시하지 않도록 한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 기존 project/panel workflow 진행, FAT 필요 여부, open Pending과 신규 파생 bottleneck aggregate.
- 상태 전이: aggregate 자체의 독립 상태 전이는 두지 않고 원본 변화에서 재계산하는 방향을 우선 검토한다.
- 보존·감사·삭제: 파생값이므로 audit 원본은 기존 workflow와 Pending history를 사용한다.
- attachment·Excel·PDF: 이번 집계에는 포함하지 않는다.
- 외부 연동·notification: 신규 실제 발송은 포함하지 않는다.
- migration·기존 데이터: 계산형 조회를 우선하며 persisted snapshot 필요 여부는 Fable이 비교한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 프로젝트 목록·상세와 패널 목록에서 대표 병목 badge, 차단 근거와 원본 deep link 후보.
- loading·empty·error·success feedback: 병목 없음, 데이터 없음, 일부 계산 불가와 open Pending 차단을 구분한다.
- 접근성·390px·Teams narrow: 색만으로 상태를 전달하지 않고 짧은 text label과 focus 가능한 link를 제공한다.
- UAT와 rollout: isolated synthetic 환경만 사용하며 Persistent UAT와 canonical runtime은 변경하지 않는다.
- rollback과 운영자 대응: 파생 aggregate와 UI를 제거해도 원본 workflow·Pending data는 보존된다.

## 6. 포함·제외 범위

### 포함

- 상태 matrix
- open Pending 차단 반영
- 패널·프로젝트 대표 병목 aggregate
- 기존 진행률 공식과 FAT optional 분모 재사용
- 프로젝트 목록·상세의 설명 가능한 병목 표시와 원본 진입

### 제외

- Home widget
- 관리자용 Pending 유형 편집
- workflow stage 번호·진행률 공식 변경
- actual Teams/Mail provider 발송
- Persistent UAT migration·write·runtime handover

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | open Pending 차단과 뒤처진 단계의 우선순위 | 단계 표시를 유지하면서 다음 확인 대상은 open Pending을 우선 | B | Fable 권장안 B 채택 | No |
| 2 | Pending 차단의 귀속 수준 | 프로젝트 수준 집계 또는 Pending 원본 model 확장 | A | Fable 권장안 A 채택 | No |
| 3 | 차단으로 계산할 open Pending | Closed 제외 전체를 open으로 두고 재검사 대기를 구분 | B | Fable 권장안 B 채택 | No |
| 4 | 여러 패널 동률과 프로젝트 단위 단계 규칙 | 단계명과 패널 수로 묶고 프로젝트 단계 1~4를 우선 판정 | A | Fable 권장안 A 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: workflow 진행률과 Pending을 따로 확인해 대표 지연 원인을 찾는 시간이 든다.
- 권장 범위: 설명 가능한 상태 matrix, 패널·프로젝트 aggregate, 프로젝트 화면의 원본 deep link.
- 확정한 정책: 기존 stage 번호·진행률·FAT 분모·권한을 재사용하고 집계가 원본을 변경하지 않는다.
- 명시적 제외: Home, Pending 관리자 편집, 실제 provider와 Persistent UAT.
- Deferred 비차단 결정: 없음 — Fable 확인 전.
- Fable 판정: `COMPLETED_CONFIRMED` — [Round 2 확인 요약 원문](007b-interview-round-2-fable.md)

## 9. 성공 기준

- 업무 결과: 사용자가 프로젝트 화면에서 대표 병목과 차단 원인을 이해하고 원본 업무로 이동한다.
- 권한·데이터 불변조건: 원본보다 넓은 정보를 노출하지 않고 workflow·Pending mutation 권한을 바꾸지 않는다.
- 자동 검증: 필수 단계 partial/all, open Pending, FAT optional 분모, 여러 패널 aggregate와 authorization을 검증한다.
- 사용자 검수: Desktop·390px synthetic screenshot을 보고 용어·우선순위·밀도를 확인한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

사용자 확인 후에만 다음 상태로 바꾼다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`

### 실험 실행 승인 기록

- 확인일: 2026-07-16
- 사용자 결정: 현재 experiment branch에서는 Fable 권장안을 그대로 채택하고 planning·review·구현·검증·screenshot까지 별도 승인 없이 진행한다.
- 적용 경계: 현재 실험 branch와 isolated runtime에만 적용하며 대표 repo, push, PR과 main merge에는 적용하지 않는다.
- experimentalImplementationApproved: true
- mainMergeApprovalCount: 0/3
