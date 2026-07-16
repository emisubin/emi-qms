# TASK-MOBILE-001 — 동일 URL 적응형 현장 UX Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 2
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5 deep-interview를 round별로 고정한다. 사용자는 현재 실험 branch에서 별도 interview 왕복 없이 Fable 권장안을 자동 채택하고 planning·review·구현·검증·screenshot까지 연속 진행하도록 명시했다. Codex는 Fable 질문을 바꾸거나 새 답을 만들지 않고, Fable이 제시한 권장 선택지만 이 사전 사용자 지시에 따라 기록한다. Interview 완료는 canonical main의 planning 또는 구현 승인이 아니다.

## Task Identity Gate

- proposedTaskId: `TASK-MOBILE-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-MOBILE-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 기존 URL과 인증·deep link를 유지하면서 390px와 Teams narrow에서 현장 사용자가 핵심 업무를 빠르게 찾고 처리할 수 있는 적응형 UX 기반을 제공한다.
- Root Finding 또는 정책 결정: 현재 화면별 responsive 처리는 존재하지만 공통 모바일 내비게이션과 현장 우선 행동 구조가 명시적 제품 계약으로 연결되지 않았고, 사진 storage·압축·재시도 정책은 미확정이다.
- 변경·검증 경계: 현재 experiment worktree 안의 공통 responsive navigation, 현장 핵심 action, 접근성·overflow, isolated tests와 synthetic screenshot만 포함한다. Fable은 사진 기능을 blocker가 해소되지 않은 상태에서 구현할지 실제 업무 Task로 분리할지 비교한다.
- 보존할 불변조건: 동일 URL·기존 인증·Teams deep link, 서버 권한, 18단계·Pending·병목 계약, 대표 repo·GitHub main, Persistent UAT와 실제 provider를 변경하지 않는다.
- 예상 산출물: Fable interview 원문·planning 원문, Codex review, 구현·tests, implementation report와 페이지 screenshot.

### 동일 목적 검색 결과

- `tasks/`, Product Roadmap, Decision Log와 사용자 흐름 문서: Product Roadmap의 canonical `TASK-MOBILE-001` 한 건만 확인했다.
- local/remote branch와 worktree: 동일 목적 없음.
- GitHub open/closed PR과 remote branch: 동일 목적 없음. exact 검색의 무관한 과거 PR 한 건 외 semantic 일치 결과는 0건이다.

### 사용자 실행 경계

- 사용자는 이 experiment worktree에서 신규 기능을 interview 없이 Fable 권장안→Codex review→구현→검증→screenshot까지 연속 진행하도록 명시했다.
- 이 사전 지시는 Fable이 비교 후 제시하는 권장안을 실험 기본값으로 채택하는 사용자 결정이다. Fable이 권장하지 않은 선택지를 Codex가 대신 고르지 않는다.
- local experiment 변경과 commit만 허용 범위이며 push·PR·merge는 미승인이다.
- 대표 repo와 GitHub main merge는 같은 merge 대상에 대한 명시적 승인 3회 전에는 금지하며 현재 승인 횟수는 `0/3`이다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 사전 사용자 지시: Fable 권장안 자동 채택 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 4 | 사전 사용자 지시에 따라 Fable 권장안 `1-A · 2-A · 3-A · 4-A` 채택 — [Fable 원문](mobile-001-interview-round-1-fable.md) | Fable 확인 요약 생성 |
| 2 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | 사전 사용자 지시에 따라 권장안 확인 요약을 승인 — [Fable 확인 요약 원문](mobile-001-interview-round-2-fable.md) | Fable planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: PC 중심 화면을 같은 URL에서 모바일로 열 수 있고 일부 카드형 반응형 처리가 있으나, 현장 사용자가 좁은 화면에서 핵심 업무·Pending·프로젝트를 빠르게 오가는 공통 경로와 행동 우선순위가 화면별로 분산돼 있다.
- 해결할 문제: 기존 인증·URL·권한을 유지하면서 390px와 Teams narrow에서 핵심 업무를 찾고 원본 업무로 진입하는 시간을 줄인다.
- 현재 우회 방식: 화면별 메뉴를 열고 긴 목록·상세를 이동하며 PC용 정보 밀도 안에서 필요한 action을 찾는다.
- 성공했을 때 사용자가 할 수 있는 일: 모바일에서 공통 내비게이션과 현장 우선 action을 이용해 내 업무, Pending, 프로젝트·병목 원본으로 한 손 흐름에 가깝게 이동한다.
- 하지 않을 경우 영향: 현장 입력 Task가 추가될수록 각 화면이 서로 다른 모바일 패턴을 만들고 사진·재시도 정책까지 중복 구현할 위험이 커진다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 현장 업무 사용자 | 내 업무·Pending·담당 프로젝트 확인과 원본 action 진입 | 기존 역할별 범위 | 원본 업무의 기존 권한만 | 모바일 shell로 권한 확대 금지 |
| 생산관리 | 병목·차단 현황 확인과 후속 화면 이동 | 기존 프로젝트·Pending 범위 | 기존 mutation만 | 기존 audit·권한 유지 |
| Read-only·System Administrator | 허용된 화면 조회 | 기존 조회 범위 | 없음 | 모바일 action이 새 mutation을 열지 않음 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 같은 URL 로그인 → 모바일 내비게이션 → 현장 핵심 업무 요약 → 기존 원본 route에서 조회·처리 → 이전 맥락으로 복귀.
- validation 실패: 기존 원본 화면의 inline validation과 서버 오류를 그대로 사용한다.
- 동시 처리·중복: 공통 shell은 새로운 mutation source가 아니며 기존 idempotency·version 계약을 우회하지 않는다.
- 취소·재시도·복구: 네트워크 실패 후 같은 route와 사용 맥락을 유지하고 다시 시도할 수 있어야 한다.
- 부분 실패와 rollback: 한 요약 데이터 실패가 전체 내비게이션을 막지 않으며 거짓 완료나 권한 밖 count를 표시하지 않는다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 기존 project, work item, Pending, 병목 aggregate를 재사용하는 방향을 우선한다.
- 상태 전이: 모바일 shell 자체 상태 전이는 두지 않고 원본 업무 상태만 사용한다.
- 보존·감사·삭제: 새 업무 record를 만들지 않으면 기존 audit 원본을 그대로 사용한다.
- attachment·Excel·PDF: 사진 storage·형식·크기·검역·보존·backup·restore·압축·재시도 정책은 Roadmap external blocker다. 안전한 공통 기반과 실제 업로드 능력을 분리할지 Fable이 비교한다.
- 외부 연동·notification: 기존 Teams deep link와 동일 URL을 보존하며 신규 provider 발송은 포함하지 않는다.
- migration·기존 데이터: 가능하면 migration 없는 Frontend/common-contract slice를 우선 검토한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 같은 route 안의 mobile navigation, 현재 위치, 내 업무·Pending·프로젝트의 현장 우선 진입.
- loading·empty·error·success feedback: 화면별 데이터 실패와 shell 동작 가능 여부를 분리한다.
- 접근성·390px·Teams narrow: keyboard focus, 44px 이상 touch target, 색 외 text label, safe area, page-level horizontal overflow 0을 검토한다.
- UAT와 rollout: isolated synthetic 환경만 사용하며 Persistent UAT와 canonical runtime은 변경하지 않는다.
- rollback과 운영자 대응: mobile-specific presentation을 제거해도 기존 desktop route·API·data는 보존된다.

## 6. 포함·제외 후보

### 포함 후보

- 동일 URL responsive navigation
- 390px·Teams narrow 공통 layout 기준
- 현장 핵심 action과 현재 맥락 유지
- 접근성·safe-area·overflow 0
- 기존 내 업무·Pending·프로젝트 병목 연결

### 제외 후보

- 별도 모바일 URL 또는 별도 인증/session
- 공용 태블릿·공용 기기 mode
- sessionStorage 강제 정책
- 실제 Teams/Mail provider 발송
- Persistent UAT migration·write·runtime handover
- 정책 미확정 상태의 binary 사진 저장

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 사진 업로드 능력의 이번 Task 포함 여부 | A: 사진 제외·정책 확정 뒤 별도 Task / B: 저장 없는 client 계약 / C: 임시 storage 실제 업로드 | A | Fable 권장안 A 채택 | No |
| 2 | 공통 모바일 내비게이션 패턴 | A: 하단 고정 tab bar+더보기 / B: hamburger drawer / C: 현행 상단 grid | A | Fable 권장안 A 채택 | No |
| 3 | 현장 우선 진입층 범위 | A: 신규 요약 없이 기존 핵심 화면 정비+배지 / B: 신규 현장 요약 화면 | A | Fable 권장안 A 채택 | No |
| 4 | 390px·Teams narrow 완료 화면 범위 | A: shell 전역+현장 핵심 화면 / B: 전체 route 일괄 정비 | A | Fable 권장안 A 채택 | No |

## 8. 성공 기준

- 업무 결과: 390px·Teams narrow에서 사용자가 내 업무·Pending·프로젝트 핵심 화면에 동일 URL로 빠르게 진입한다.
- 권한·데이터 불변조건: 모바일 shell이 원본 권한·상태·audit보다 넓은 정보나 mutation을 제공하지 않는다.
- 자동 검증: desktop·390px·Teams narrow, keyboard·touch target, route·back context, page-level overflow 0을 검증한다.
- 사용자 검수: synthetic screenshot으로 주요 페이지의 내비게이션·가독성·현장 action을 확인한다.

## 9. Fable 확인용 요약

- 해결할 문제: 동일 URL·인증·Teams deep link와 서버 권한을 유지한 채 390px·Teams narrow의 현장 핵심 업무 진입 시간을 줄인다.
- 확정 범위: 하단 고정 tab bar와 권한 기반 더보기 sheet, 기존 내 업무·Pending·프로젝트 화면의 모바일 우선 정비, shell safe-area·overflow 0.
- 확정 제외: 사진 저장·client 업로드 계약, 신규 Home형 요약, 별도 모바일 URL·session, Persistent UAT와 실제 provider.
- 비차단 planning 항목: 권한별 tab 구성, safe-area와 기존 하단 요소 겹침 회피, 더보기 focus 순서, Roadmap의 사진 문구와 실험 결정 차이 기록.
- Fable 판정: `COMPLETED_CONFIRMED` — [Round 2 확인 요약 원문](mobile-001-interview-round-2-fable.md)

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 요약을 planning 입력으로 사용하는 데 동의한다.

### 실험 실행 승인 기록

- 확인일: 2026-07-17
- 사용자 결정: 현재 experiment branch에서는 interview 질문을 다시 묻지 않고 Fable 권장안을 그대로 채택하며 planning·review·구현·검증·screenshot까지 별도 승인 없이 진행한다.
- 적용 경계: 현재 실험 branch와 isolated runtime에만 적용하며 대표 repo, push, PR과 main merge에는 적용하지 않는다.
- experimentalImplementationApproved: true
- mainMergeApprovalCount: 0/3
