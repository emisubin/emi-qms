# TASK-MOBILE-002 — 모바일 우선 적응형 화면 체계 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 2
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 모바일 우선 적응형 화면 체계를 기획하기 위한 interview source of truth다. 이 실험 branch에서는 사용자의 기존 지시에 따라 질문 왕복을 생략하고 Fable이 제시한 권장안을 자동 채택한 뒤 확인용 요약을 다시 Fable에 검증시킨다. 대표 repo, GitHub `main`, Persistent UAT, provider와 canonical runtime은 변경하지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-MOBILE-002`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-MOBILE-002`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 접속 환경의 화면·입력 능력을 판별해 PC 관리형 화면과 다른 모바일 전용 정보구조·내비게이션·화면 구성을 제공한다.
- Root Finding 또는 정책 결정: `TASK-MOBILE-001`은 공통 하단 내비게이션과 현장 핵심 route 정비만 수행하고 전체 화면의 모바일 전용 재설계를 명시적으로 제외했다. 현재 860px 이하 화면은 여러 PC component를 CSS와 일부 카드 변형으로 재배치하므로 제품의 모바일 최우선 목적에 충분히 맞지 않는다.
- 변경·검증 경계: Frontend presentation과 browser capability 판별만 변경한다. 기존 URL·API·권한·상태 전이·audit·Backend·DB·migration을 보존하고 synthetic isolated 환경에서 desktop·390px·Teams narrow를 검증한다.
- 보존할 불변조건: 같은 URL과 인증/session, 서버 권한 강제, 18단계 workflow, 기존 mutation·deep link, desktop 관리·조회·일괄 편집 UX, page-level overflow 0, 대표 repo와 `main` 무변경.
- 예상 산출물: Fable interview 원문·planning, Codex 내용 review, 모바일 전용 shell과 핵심 화면 구현, 자동 검증, page별 screenshot, implementation report와 local commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 사용자 원요청과 실험 자동 진행 지침 기록 | Fable 질문 생성 |
| 1 | `QUESTIONS_REQUIRED` | 4 | 실험 사전 지시에 따라 Fable 권장안 1-B·2-A·3-A·4-A 자동 채택 | Fable 확인용 요약 생성 |
| 2 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | 추가 blocking 결정 없음. 실험 사전 지시를 사용자 확인 source로 적용 | Planning 진행 |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 동일 URL에서 PC·모바일을 사용하고 860px 이하에서 하단 내비게이션과 일부 card 변형을 제공하지만, 다수 화면은 PC 정보구조와 component 순서를 축소·재배치한 형태다.
- 해결할 문제: 모바일이 이 프로그램의 최우선 목적임에도 좁아진 PC 화면처럼 보여 현장 사용자가 한 손으로 다음 행동을 빠르게 판단·실행하기 어렵다.
- 현재 우회 방식: 화면을 세로로 길게 스크롤하고 PC용 표·필터·상세 section을 차례로 찾아 사용한다.
- 성공했을 때 사용자가 할 수 있는 일: 모바일 진입 시 오늘 할 일과 차단 이슈를 먼저 보고, 화면별 핵심 action을 thumb zone에서 실행하며, 상세 정보는 단계적으로 펼쳐본다. Desktop에서는 기존 관리형 밀도를 유지한다.
- 하지 않을 경우 영향: 현장 기능이 늘수록 PC layout 축소 규칙이 누적되고 route별 모바일 패턴이 달라져 사용성·접근성·검증 비용이 커진다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 현장 업무 사용자 | 내 업무·프로젝트·Pending 확인, 기존 원본 action 실행 | 기존 역할별 범위 | 기존 API와 서버 Policy가 허용한 mutation만 | 모바일 mode로 권한 확대 금지 |
| 관리·조회 사용자 | Desktop에서 표·필터·일괄 조회, 모바일에서 요약 조회 | 기존 역할별 범위 | 기존 권한과 동일 | 기존 audit 보존 |
| Read-only·System Administrator | 허용 화면 조회와 시스템 관리 진입 | 기존 범위 | 기존 정책과 동일 | 모바일 전용 UI가 우회 action을 만들지 않음 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 같은 URL 로그인 → viewport·pointer·hover·orientation·safe-area 능력 판별 → 모바일 전용 또는 desktop composition 선택 → 기존 route와 API로 조회·처리.
- validation 실패: 기존 API validation과 화면별 error contract를 유지하고 모바일에서는 action 인접 feedback으로 표시한다.
- 동시 처리·중복: presentation 변경만 수행하며 기존 idempotency·동시성 계약을 유지한다.
- 취소·재시도·복구: 기존 route/deep link와 browser history를 유지하고 mode 변경 시 동일 업무 맥락을 보존한다.
- 부분 실패와 rollback: 신규 aggregate/API를 만들지 않고 기존 section별 실패를 모바일 composition에서 독립적으로 표시한다. 실험 branch를 채택하지 않는 것이 rollback이다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 신규 영속 data 없음. 기존 view state와 API response를 presentation에서 재사용한다.
- 상태 전이: 신규 업무 상태 전이 없음. layout mode는 client runtime 상태다.
- 보존·감사·삭제: 기존 서버 audit와 lifecycle 무변경.
- attachment·Excel·PDF: 모바일은 현장 action 중심, Desktop은 Excel/PDF·일괄 편집 중심이라는 Roadmap 원칙을 보존한다. 신규 attachment upload는 제외한다.
- 외부 연동·notification: 기존 deep link와 인앱 알림을 보존하고 실제 provider 발송은 제외한다.
- migration·기존 데이터: migration과 data backfill 없음.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 모바일 Home에서 긴급·내 업무·담당 프로젝트를 우선하고 하단 tab, mobile page header, card/stepper, sticky primary action, full-screen filter/search sheet를 검토한다.
- loading·empty·error·success feedback: 모바일의 현재 action 문맥 가까이에 표시하고 긴 page 상단으로만 보내지 않는다.
- 접근성·390px·Teams narrow: 44px 이상 touch target, safe-area, focus·screen reader·reduced motion, page-level overflow 0을 보장한다.
- UAT와 rollout: synthetic isolated desktop·390px·480px 검증과 screenshot만 수행한다. Persistent UAT와 canonical runtime handover는 제외한다.
- rollback과 운영자 대응: experiment branch commit을 채택하지 않으며 기존 desktop composition과 API는 그대로 보존한다.

## 6. 포함·제외 범위

### 포함

- 사용자 agent 문자열이 아닌 viewport·pointer·hover 등 capability 기반 layout mode 판별
- 모바일 전용 application shell과 정보 우선순위
- 기존 주요 route를 PC 축소판이 아닌 모바일 card·step·sheet·sticky action 구조로 재구성
- Desktop composition 보존과 mode 전환 회귀 검증
- page별 synthetic screenshot

### 제외

- 별도 모바일 URL·앱·인증·session
- Backend·API·DB·migration·권한·업무 상태 변경
- 사진 저장·압축·offline queue·업로드 재시도
- Persistent UAT write·runtime handover·실제 provider
- 대표 repo, GitHub `main`, push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | layout mode 판별 신호 조합 | A: viewport만 / B: viewport 주신호+pointer·hover 보조 / C: B+수동 전환 | B | Fable 권장안 B 자동 채택. ≤860px는 모바일 composition, coarse pointer·hover 없음은 touch affordance 보강, 861px 이상 터치 화면은 desktop composition+touch 보정 | No |
| 2 | 모바일 전용 재구성 화면 범위 | A: 현장 핵심 7개 route / B: A+생산관리·구매·자재 / C: 관리자 포함 전체 route | A | Fable 권장안 A 자동 채택. 홈·내 업무·프로젝트 목록·상세·Pending 목록·상세·알림을 전용 재구성하고 나머지는 shell 기준과 회귀를 보장 | No |
| 3 | 모바일 Home 구성 | A: 기존 widget data를 긴급·차단→내 업무→담당 프로젝트→나머지 순으로 재배치 / B: Home 숨김·내 업무 시작 | A | Fable 권장안 A 자동 채택 | No |
| 4 | 하단 tab 구성 | A: 홈·내 업무·프로젝트·권한 시 Pending·알림+더보기 / B: 기존 4-tab·홈은 더보기 / C: 알림을 더보기로 이동 | A | Fable 권장안 A 자동 채택. 390px 6-slot 폭·label·44px·overflow를 검증 | No |

## 8. Fable 확인용 요약

- 해결할 문제: PC 화면 축소형 모바일 UX를 현장 행동 중심의 모바일 전용 composition으로 교체한다.
- 권장 범위: viewport 주신호+pointer·hover touch 보정, mobile shell, 홈·내 업무·프로젝트·Pending·알림의 모바일 전용 component, desktop 보존, synthetic 검증.
- 확정한 정책: ≤860px mobile composition, 861px 이상 desktop composition, resize·orientation 재판별 중 업무 맥락 보존, 기존 widget data 재사용, 홈 포함 핵심 tab, 같은 URL·API·권한·상태·audit 유지, main 무변경.
- 명시적 제외: 별도 앱/인증, Backend·DB, 미확정 사진 업로드, Persistent UAT·provider·게시.
- Deferred 비차단 결정: 판별 오차를 위한 사용자 수동 "PC 화면으로 보기" toggle의 저장·유지 정책은 후속 검토한다.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 현장 사용자가 모바일 첫 화면에서 우선 업무를 판단하고 핵심 action을 한 손 흐름으로 실행한다.
- 권한·데이터 불변조건: 기존 서버 권한·API·업무 상태·audit와 desktop UX가 보존된다.
- 자동 검증: unit·typecheck·lint·build, desktop·390px·Teams narrow E2E, overflow·touch·focus·mode 판별.
- 사용자 검수: page별 screenshot으로 PC 축소판이 아닌지와 모바일 정보 우선순위를 확인한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

확인 source: 사용자는 이 실험 branch와 대화에서 신규 작업을 인터뷰 없이 Fable 권장안으로 바로 기획·review·구현하고 결과물을 보여주도록 지시했으며, 이번 요청에서도 모바일 전용 재구성을 Fable 기획부터 진행하도록 명시했다.
