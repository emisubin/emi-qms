# TASK-USER-FLOW-001 — 웹사이트 전체 유저플로우 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 3
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: true
- implementationApproved: true

이 문서는 Fable 5가 사용자와 진행하는 deep-interview를 round별로 고정한다. Codex는 Fable 질문과 사용자 답변을 전달·기록하지만 업무 질문을 대신 만들거나 답하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## Task Identity Gate

- proposedTaskId: `TASK-USER-FLOW-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `0.6_NEW_FEATURE_GO_NO_GO`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-USER-FLOW-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 현재 구현과 향후 Roadmap 기능을 연결하는 역할별 웹사이트 전체 유저플로우, 화면·내비게이션, 정상·예외·복구 경로의 기획 기준선을 수립한다.
- Root Finding 또는 정책 결정: 동일 목적 canonical Task가 없으며, 사용자가 0.6 신규 기능 GO와 TASK-007A보다 이 설계를 먼저 수행하는 Roadmap 재정렬을 명시 승인했다.
- 변경·검증 경계: Fable 5 deep-interview, 사용자 요약 확인, Fable 5 planning, Codex Repository 대조 review와 Roadmap 추적까지만 포함한다.
- 보존할 불변조건: 기존 인증·권한·18단계 업무·내 업무·알림 계약, Backend authoritative policy, 5174·5176·5081 runtime, API·DB·migration·provider와 후속 Task의 별도 승인 Gate를 보존한다.
- 예상 산출물: 이 interview 문서, `tasks/user-flow-001-planning.md`, `tasks/user-flow-001-review.md`, Product Roadmap update.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | N/A — 초기 기준선 작성 | Fable Round 1 |
| 1 | `QUESTIONS_REQUIRED` | 3 | 답변 기록 완료 — `C/B/C` | Fable Round 2 |
| 2 | `QUESTIONS_REQUIRED` | 3 | 답변 기록 완료 — `B/B/C` | Change 008 적용 뒤 Fable Round 3 |
| 3 | `COMPLETED_CONFIRMED` | 0 | 전체 요약 동의 | Fable planning |

### Fable 호출 상태

- 필수 CLI option 지원 확인: 9/9
- Codex exec 내부 실제 Fable process 시작: true
- 사용자 terminal의 Claude CLI process 필요: false
- Fable model 응답 생성: true
- 최신 Fable 질문 수: 0
- 누적 Fable 질문 수: 6
- stable blocker: `NONE`
- 해소 근거: CLI가 Fable 5로 표시하는 `fable` alias와 fail-closed read-only runner로 Round 1 상태 계약을 통과했다.
- 처리 원칙: 필수 read-only option을 완화하거나 Codex가 Fable 대신 업무 질문을 만들지 않는다.

### Fable 실행 정책 — Change 008

- 사용자 승인: 같은 Task의 첫 호출에서 전체 Repository 기준선을 조사하고, 이후 round는 Task-scoped private session을 재개해 반복 조사 시간을 줄인다.
- canonical source: 누적 질문·답변·결정은 이 interview 문서이며 session memory는 가속 cache일 뿐이다. 매 round 최신 interview를 다시 읽는다.
- drift guard: base HEAD, instruction·runner contract 또는 예상 밖 제품 source가 바뀌면 session을 재사용하지 않고 전체 기준선을 갱신한다.
- 질문 수: 품질과 구체성을 낮추지 않으며 서로 관련된 질문을 round당 1~5개까지 묶는다. 수를 채우기 위한 질문은 추가하지 않는다.
- planning 경계: planning 시작 전 최신 interview·Roadmap·관련 구현과 tests를 다시 확인한다.
- 종료 경계: Task 종료 시 해당 Task 소유의 private session state와 transcript만 runner cleanup으로 제거한다.
- Round 3 측정 해석: Round 1·2는 비영구 호출이었으므로 Round 3은 기존 transcript 재개가 아니라 최신 canonical interview에서 만드는 1회성 경량 부트스트랩이다. Round 3에서 만든 session은 후속 round와 planning에서 재개한다.
- Round 3 측정: 첫 호출은 `BOOTSTRAPPED_FROM_INTERVIEW`, drift `INTERVIEW_BASELINE`, preflight/model/postflight `1/129/0초`였다. Cleanup 선검증 보강 뒤 최종 runner contract refresh는 `REFRESHED_AFTER_DRIFT`, `0/135/0초`였다. 두 호출 모두 stderr `0`, private artifact mode `0700/0600`, 질문·planning `0/0`, 확인용 요약 계약을 통과했다. 사용자 답변 뒤 첫 true-resume 호출을 최종 성능 비교로 남긴다.
- Planning resume 측정: `RESUMED_PLANNING_PREFLIGHT`, baseline reused, drift `UNCHANGED`, preflight/model/postflight `1/264/0초`, stderr `0`, planning `DRAFT`, 사용자 결정 3건. Session 재개와 drift guard는 입증했지만 21KB planning 생성·최신 구현 preflight로 총시간 단축은 입증하지 못했다.

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: Repository에는 18단계 업무 프로세스와 개별 화면·기능 기준이 있으나 웹사이트 전체 역할별 유저플로우 계약은 별도 canonical 문서로 확정되지 않았다.
- 해결할 문제: 앞으로 어떤 기능을 추가 개발해야 하는지 명확하게 알 수 없다.
- 현재 우회 방식: Roadmap을 기준선으로 사용하고 개별 화면은 구현 시점마다 별도로 기획했다.
- 성공했을 때 사용자가 할 수 있는 일: 전체 유저플로우를 기준으로 앞으로 추가 개발할 기능과 방향을 명확하게 판단할 수 있다.
- 하지 않을 경우 영향: 추가 개발 대상을 명확히 정하지 못한 상태가 계속된다.
- 산출물 1차 용도: 후속 Task 기획 기준선과 사용자 온보딩·교육을 겸한다.
- 첫 사용자: 사용자 본인.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 영업 | 역할별 전체 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 설계 | 역할별 전체 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 생산관리 | 역할별 전체 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 구매 | 역할별 전체 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 자재 | 역할별 전체 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 제조 | 역할별 전체 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 물류 | 역할별 전체 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 품질 IQC | 검사 단계별 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 품질 LQC | 검사 단계별 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 품질 OQC | 검사 단계별 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| 품질 전진검수·FAT | 검사 단계별 여정 | 기존 권한 계약 | 기존 권한 계약 | 기존 Backend 권한 계약 보존 |
| System Administrator | 관리자 여정 | 관리자 계약 | 관리자 계약 | 업무 권한 무제한 우회 금지 |
| 승인 대기 사용자 | 로그인·승인 대기·역할 부여 후 진입 | 승인 대기 범위 | 변경 불가 | 승인·역할 부여 이력 보존 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 미확정 — Fable 5 질문 대상
- validation 실패: 미확정 — Fable 5 질문 대상
- 동시 처리·중복: 미확정 — Fable 5 질문 대상
- 취소·재시도·복구: 미확정 — Fable 5 질문 대상
- 부분 실패와 rollback: 미확정 — Fable 5 질문 대상
- 설계 상세도: 구현 구간은 실제 경로 기준 상세 flow로, 미구현 구간은 진입점·핵심 행동·완료 시 다음 담당자 연결 수준으로 설계한다. 세부 화면·양식·정책은 해당 후속 Task의 planning 승인 항목으로 위임한다.
- 예외·복구 coverage: 공통 예외 4종을 한 번 정의하고 구현 구간은 실제 경로 기준 단계별 예외를 상세히 기록한다. 미구현 구간은 차단 시 Pending 연결과 다음 담당자 알림 수준까지만 기록한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: 기획 단계에서는 확정하지 않으며 실제 Repository와 사용자 결정을 대조한다.
- 상태 전이: 기존 18단계와 확정 상태를 임의 변경하지 않는다.
- 보존·감사·삭제: 기존 정책을 보존하고 추가 필요성은 interview에서 분리한다.
- attachment·Excel·PDF: 현재 Roadmap의 미확정 정책을 임의 확정하지 않는다.
- 외부 연동·notification: 기존 채널 matrix와 provider/event coverage를 보존한다.
- migration·기존 데이터: 이번 기획 실행에서는 변경하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 목표 메뉴 골격을 정의하고 각 메뉴에 구현·미구현 상태와 도입 Task를 연결한다. 미구현 메뉴는 해당 기능 Task 승인·구현 전까지 Frontend에 노출하지 않는다.
- loading·empty·error·success feedback: 기존 공통 기준을 보존한다.
- 접근성·390px·Teams narrow: 기존 동일 URL·overflow 0 원칙을 보존하며 상세 우선순위는 interview에서 확인한다.
- UAT와 rollout: planning에서 검증 전략만 제안하며 runtime mutation은 별도 승인 대상이다.
- rollback과 운영자 대응: 이번 기획 문서 변경은 Git revert 대상이며 제품 rollback은 구현 Task에서 별도 설계한다.

## 6. 포함·제외 범위

### 포함

- Fable 5 deep-interview와 사용자 확인용 요약
- 역할별 웹사이트 전체 여정과 화면·내비게이션 구조 기획
- 내 업무·알림·deep link 진입과 정상·예외·복구 흐름 기획
- 권한·접근성·desktop/390px 기준과 후속 Task 연결 기준
- Fable 5 planning과 Codex Repository 대조 review
- 사용자 본인이 처음 사용할 수 있는 역할별 여정 요약과 온보딩·교육 관점
- Planning·review 승인 뒤 `docs/` 아래 유지 가능한 canonical 유저플로우 문서 생성과 후속 화면·흐름 변경 Task의 동시 갱신 규칙

### 제외

- Frontend·Backend 제품 코드 구현
- Figma 시각 디자인과 화면별 pixel 구현
- API·DB·migration·runtime configuration·provider 변경
- Persistent UAT write와 실제 외부 발송
- TASK-007A와 후속 기능의 planning·implementation 자동 승인
- commit·push·PR·merge와 worktree 정리

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 유저플로우 산출물이 해결할 업무 문제와 1차 용도 | A. 후속 Task 기획 기준선 / B. 사용자 온보딩·교육 / C. A+B 겸용 | A | C — 앞으로 무엇을 추가 개발해야 하는지 명확하지 않아 유저플로우로 방향을 잡고자 함. 첫 사용자는 사용자 본인 | No |
| 2 | 역할별 flow의 역할 구분 단위 | A. 부서 8역할+관리자 / B. 부서 7역할+품질 4역할+관리자+승인 대기 / C. 구현 역할만 상세 | B | B | No |
| 3 | 미구현 기능 구간의 설계 상세도 | A. 전후 연결만 / B. 전 구간 동일 상세 / C. 중간 상세도와 후속 Task 위임 | C | C | No |
| 4 | 유저플로우 계약의 canonical 위치와 유지 방식 | A. planning 자체 / B. 승인 후 `docs/` canonical 문서 생성·후속 Task 갱신 / C. 별도 DOCS_GOVERNANCE로 유보 | B | B | No |
| 5 | 목표 내비게이션 골격과 미구현 메뉴 처리 | A. 현재 메뉴 유지 / B. 목표 골격+구현 상태·도입 Task 병기, 미구현 메뉴 미노출 / C. 진입점 유형만 표준화 | B | B | No |
| 6 | 예외·복구 흐름의 coverage 단위 | A. 공통 예외 4종만 / B. 18단계 전수 상세 / C. 공통 예외+구현 구간 상세+미구현 구간 중간 상세 | C | C | No |

### Round 1 — 사용자 답변

1. `C` — 현재 어려움은 앞으로 어떤 것을 추가로 개발해야 하는지 명확하게 알 수 없는 것이다. 이번에 방향을 잡아가기 위해 유저플로우를 기획했다. 첫 사용자는 사용자 본인이다.
2. `B`
3. `C`

### Round 1 — Fable 질문 원문

#### 질문 1 — 이 유저플로우 산출물이 해결할 업무 문제와 1차 용도

현재 Repository에는 18단계 프로세스와 화면별 기준은 있으나 "역할별 전체 웹사이트 여정" canonical 문서가 없습니다. 이 산출물이 없어서 지금 실제로 겪는 어려움은 무엇이고, 완성되면 누가 가장 먼저 어떤 용도로 사용합니까?

- 필요한 이유: interview 1절(해결할 문제, 현재 우회 방식, 성공 시 가능한 일, 미수행 시 영향)이 전부 미확정입니다. 이 답이 산출물의 상세도, 형식과 성공 기준 전체를 결정합니다.
- 답변이 바꾸는 범위: 성공 기준(9절), 포함·제외 범위(6절), planning의 완료 기준.
- 선택지 비교:
  - A. 후속 Task 기획 기준선 — TASK-007A 이후 신규 기능 planning들이 화면·내비게이션·진입 경로를 이 문서에 맞추도록 하는 내부 설계 계약. 장점은 Roadmap 큐와 직결되고 범위가 명확한 것이며, 단점은 최종 사용자 교육에는 부족한 것입니다.
  - B. 사용자 온보딩·교육 문서 — 실무자가 자기 역할의 처음부터 끝까지 여정을 이해하는 안내 중심. 장점은 운영 전환 준비이며, 단점은 미구현 기능이 많아 지금 쓰면 금방 낡는 것입니다.
  - C. A+B 겸용 — 설계 계약을 기본으로 하되 역할별 여정 요약을 사용자 언어로 병기. 장점은 재사용이며, 단점은 분량과 유지비 증가입니다.
- 권장안: A. Roadmap 0.7의 위치(TASK-007A의 선행 의존성)와 "기획 기준선 수립"이라는 purpose identity에 부합합니다. 교육 문서는 운영 전환 Task(6.1)에서 후행하는 것이 안전합니다.

#### 질문 2 — 역할별 flow의 역할 구분 단위

역할별 여정을 어떤 단위로 나눠 설계합니까?

- 필요한 이유: interview 2절(대상 사용자와 권한)이 미확정입니다. 역할 단위가 flow 개수, 화면·내비게이션 설계 분량과 후속 Task 연결 기준을 직접 결정합니다.
- 답변이 바꾸는 범위: 2절 역할 표, 3절 정상·예외·복구 흐름의 전개 단위, planning의 핵심 시나리오 수.
- 선택지 비교:
  - A. 부서 8역할(영업·설계·생산관리·구매·자재·제조·품질·물류) + System Administrator — 품질은 단일 역할로 통합. 장점은 단순하고 9개 여정인 것이며, 단점은 품질의 IQC/LQC/OQC/전진검수·FAT 진입 시점과 대상 단위 차이가 뭉개지는 것입니다.
  - B. 부서 7역할 + 품질 검사 단계별 4역할(IQC/LQC/OQC/전진검수·FAT) + System Administrator + 승인 대기 사용자 — 확정된 responsibility_type 구조와 동일한 단위. 장점은 기존 권한·담당자 계약과 1:1로 맞고 후속 검사 Task와 직접 연결되는 것이며, 단점은 여정 수 증가입니다.
  - C. 구현된 화면이 있는 역할만 상세, 나머지는 공통 여정 1개로 축약. 장점은 분량 최소이며, 단점은 전체 유저플로우 목적에 미달하고 미구현 역할 연결 기준을 만들지 못하는 것입니다.
- 권장안: B. Roadmap 5절의 확정된 담당자 구조, fallback 규칙과 18단계 담당 부서 매핑이 이미 이 단위로 정의되어 있어 임의 재해석 없이 재사용할 수 있습니다. 승인 대기 사용자 여정(로그인→승인 대기→역할 부여 후 진입)은 인증 확정 계약이 있으므로 소형 여정 1개로 포함하는 것을 권장합니다.

#### 질문 3 — 미구현 기능 구간의 설계 상세도

현재 구현 화면(프로젝트~구매·자재 입고, 내 업무, 알림, 관리자)과 미구현 구간(검사·제조·물류·정산·Pending List·Home)의 상세도를 어떻게 다르게 설계합니까?

- 필요한 이유: 이 Task는 TASK-007A 등 후속 NEW_FEATURE보다 먼저 수행되며, 각 후속 기능은 별도 Fable deep-interview와 planning 승인 Gate를 따로 가집니다. 여기서 미구현 화면을 과도하게 확정하면 후속 planning과 충돌하고, 너무 얕으면 "현재와 향후를 연결"하는 목적을 달성하지 못합니다.
- 답변이 바꾸는 범위: 3절 흐름 상세도, 5절 진입 화면·내비게이션 설계 범위, 6절 포함·제외 경계, 후속 Task 승인 Gate와의 충돌 위험.
- 선택지 비교:
  - A. 구현 구간만 상세 flow, 미구현 구간은 단계명·담당 역할·전후 연결만 표기. 장점은 후속 planning과 충돌하지 않는 것이며, 단점은 내비게이션·메뉴 구조를 미리 잡을 수 없어 연결 기준 역할이 약한 것입니다.
  - B. 전 구간을 동일 상세도로 화면·행동 단위까지 설계. 장점은 완결성이며, 단점은 Pending 첨부·검사 양식·포장 기준 등의 미확정 정책을 임의 확정해 Roadmap 원칙과 후속 Gate를 침범하는 것입니다.
  - C. 구현 구간은 실제 경로 기준 상세 flow, 미구현 구간은 "진입점·핵심 행동·완료 시 다음 담당자 연결" 수준의 중간 상세도로 설계하고, 세부 화면·양식·정책은 해당 후속 Task의 planning 승인 항목으로 명시 위임. 장점은 내비게이션·메뉴·deep link 골격은 확정하면서 미확정 정책을 침범하지 않는 것이며, 단점은 상세도 경계를 문서에 명확히 적어야 하는 것입니다.
- 권장안: C. Roadmap의 "미확정사항은 구현하지 않거나 추적 대상에 남긴다" 원칙과 후속 Task별 별도 승인 Gate를 보존하면서 전체 여정 연결 기준을 만들 수 있는 균형점입니다.

### Round 2 — Fable 질문 원문

#### 질문 1 — 유저플로우 계약의 canonical 위치와 유지 방식

승인된 유저플로우가 최종적으로 어디에 살아 있는 문서로 존재해야 합니까?

- 필요한 이유: 현재 Task Identity Gate의 예상 산출물은 interview·planning·review·Roadmap update뿐입니다. Round 1에서 이 산출물의 용도를 "후속 Task 기획 기준선 + 사용자 온보딩·교육 겸용"으로 확정했으므로, 일회성 승인 문서인 `tasks/` planning 안에만 두면 후속 기능이 구현될 때마다 낡아가는 문제가 생깁니다. 이 답이 산출물 형태, 포함 범위와 완료 기준, 후속 Task가 이 계약을 참조·갱신하는 방식을 결정합니다.
- 답변이 바꾸는 범위: 예상 산출물 목록, planning의 완료 기준, 후속 Task의 문서 갱신 의무, Roadmap 추적 항목.
- 선택지 비교:
  - A. `tasks/user-flow-001-planning.md` 자체를 기준선으로 사용 — 별도 문서를 만들지 않습니다. 장점은 승인 경계가 단순하고 이번 Task 산출물 목록과 정확히 일치하는 것이며, 단점은 후속 기능 구현·변경을 반영할 canonical 위치가 없고 온보딩 용도로 찾기 어려운 것입니다.
  - B. Planning·review 승인 후 이 Task의 구현 단계에서 `docs/` 아래 canonical 유저플로우 문서를 생성 — Roadmap이 이를 참조하고, 후속 Task가 화면·흐름을 바꾸면 이 문서도 함께 갱신합니다. 장점은 기준선+온보딩 겸용 목적과 기존 Roadmap 원칙에 부합하는 것이며, 단점은 산출물과 유지 규칙이 하나 늘어나는 것입니다.
  - C. 이번에는 planning까지만 확정하고 canonical docs 승격 여부는 별도 후속 `DOCS_GOVERNANCE` Task로 유보. 장점은 이번 범위 최소화이며, 단점은 온보딩 용도가 당분간 미충족 상태로 남고 결정을 한 번 더 반복하는 것입니다.
- 권장안: B. Round 1에서 확정한 겸용 목적을 실제로 달성하는 유일한 형태이고, 문서 생성만 있는 구현이므로 code·runtime·migration 경계를 침범하지 않습니다.

#### 질문 2 — 목표 내비게이션 골격과 미구현 메뉴 처리

현재 구현된 공통 메뉴는 내 업무·프로젝트·생산관리·구매·자재(권한 조건부)·알림·관리자 7종이고 Home은 없습니다. 향후 Roadmap에는 Home, Pending List, 검사, 제조, 물류, 정산이 예정되어 있습니다. 이 문서에서 목표 메뉴 골격을 어디까지 확정합니까?

- 필요한 이유: Round 1에서 "내비게이션·메뉴·deep link 골격은 확정"하기로 했으나, 그 골격이 현재 메뉴의 연장인지 목표 상태의 재설계인지는 미확정입니다. 진입 화면과 핵심 행동이 비어 있으며, 이 답이 13개 역할 여정의 진입점 표기 전체를 결정합니다.
- 답변이 바꾸는 범위: 진입 화면·내비게이션 설계, 각 여정의 시작점 표기, 후속 Task의 메뉴 위치 연결 기준.
- 선택지 비교:
  - A. 현재 7개 메뉴를 유지하고 미구현 기능은 기존 메뉴와 프로젝트 상세 하위 경로로 연결. 장점은 현재 구현과 완전히 일치하는 것이며, 단점은 검사·제조·물류 담당자의 역할별 진입 메뉴가 없어 프로젝트 탐색에 의존하고 Home MVP 연결 기준을 만들지 못하는 것입니다.
  - B. 목표 메뉴 골격을 확정하되 단계 표기를 병기 — Home(미구현), 내 업무, 프로젝트, 부서 업무 메뉴, 알림, 관리자로 목표 구조를 정의하고 각 메뉴에 구현·미구현 상태와 도입 Task를 연결합니다. 미구현 메뉴는 해당 Task 승인 전까지 노출하지 않습니다. 장점은 후속 Task가 메뉴 위치를 재논의하지 않아도 되는 연결 기준이 생기는 것이며, 단점은 Home·Pending 화면 세부는 비워 둔 채 위치만 확정해야 하는 것입니다.
  - C. 메뉴 골격 확정을 포기하고 진입점 유형(내 업무 카드, 알림 deep link, 메뉴 직접 진입) 3종만 공통 패턴으로 표준화. 장점은 충돌 위험 최소이며, 단점은 Round 1에서 확정한 골격 결정과 어긋나고 전체 유저플로우 목적에 미달하는 것입니다.
- 권장안: B. Round 1 결정과 정합하며, 위치·상태·도입 Task만 확정하고 화면 세부는 후속 planning에 위임하므로 후속 승인 Gate를 침범하지 않습니다.

#### 질문 3 — 예외·복구 흐름의 coverage 단위

13개 역할 여정 각각에 예외·복구 경로를 어느 수준까지 포함합니까?

- 필요한 이유: validation 실패, 동시 처리, 취소·재시도, 부분 실패가 모두 미확정입니다. 전체 유저플로우 문서에서 예외 흐름은 분량을 가장 크게 좌우하며, 검사 양식·Pending 첨부 등 미확정 정책과의 충돌 위험이 가장 높은 영역입니다.
- 답변이 바꾸는 범위: 정상·예외·복구 흐름 전체, 각 여정의 예외 경로 표기, planning의 핵심 시나리오 수와 후속 Task 위임 목록.
- 선택지 비교:
  - A. 공통 예외 4종만 한 번 정의하고 모든 여정이 참조 — 권한 없음·승인 대기 진입, 담당자 부재 fallback, Pending 차단, 알림·deep link 진입 실패. 장점은 분량 최소와 정책 충돌 없음이며, 단점은 구현된 화면의 실제 validation·복구 경로가 문서에 남지 않는 것입니다.
  - B. 18단계 각 단계의 validation·동시성·복구까지 전수 설계. 장점은 완결성이며, 단점은 미구현 양식과 정책을 임의 확정해 Round 1 결정과 충돌하는 것입니다.
  - C. 공통 예외 4종을 한 번 정의하고, 구현 구간은 실제 경로 기준 단계별 예외를 상세히, 미구현 구간은 "차단 시 Pending 연결과 다음 담당자 알림" 수준만 표기. 장점은 Round 1의 상세도 이원화 결정과 정확히 맞는 것이며, 단점은 구현 구간의 실제 validation 경로를 Repository와 대조하는 작성 비용입니다.
- 권장안: C. 확정된 fallback 규칙·Pending List 원칙·알림 채널 matrix를 재사용하고 미확정 정책은 위임하는 균형점입니다.

### Round 2 이후 확인 예정

- "현재 우회 방식"과 성공 기준 세부 문구는 Round 1·2 답변을 반영한 다음 Fable round의 확인용 요약에서 사용자 확인 항목으로 제시한다. 답변을 추측해 채우지 않는다.

### Round 2 — 사용자 답변

1. `B`
2. `B`
3. `C`

## 8. Fable Round 3 확인용 요약

### Round 3 판정 근거

- 7절 결정 6건이 모두 사용자 답변으로 확정됐고 Blocking `No`, `openBlockingDecisionCount: 0`이다.
- Roadmap 0.7에서 이 Task가 현재 실행 가능한 canonical Task이며 TASK-007A보다 먼저 수행하는 재정렬이 명시 승인되어 있다.
- Round 2 이후 확인 예정 항목에 따라 현재 우회 방식과 성공 기준 문구는 아래 확인 항목 A·B로 제시하며, 사용자 확인 전에는 답변으로 기록하지 않는다.
- 접근성·390px 상세 우선순위는 신규 결정이 아니라 기존 원칙 보존과 Roadmap 1.3 `TASK-MOBILE-001` 위임으로 정리해 확인 항목 C로 제시한다. 질문 수를 채우기 위한 추가 질문은 만들지 않는다.

### 8.1 업무 문제와 기대 결과

- 해결할 문제: 앞으로 어떤 기능을 추가 개발해야 하는지 명확하게 알 수 없다.
- 성공 시: 역할별 전체 유저플로우를 기준으로 추가 개발 대상과 방향을 명확하게 판단할 수 있다.
- 1차 용도: 후속 Task 기획 기준선과 사용자 온보딩·교육 겸용이다. 첫 사용자는 사용자 본인이다.

### 8.2 대상 역할과 권한 — 13개 여정

- 부서 7역할(영업·설계·생산관리·구매·자재·제조·물류), 품질 검사 단계별 4역할(IQC·LQC·OQC·전진검수/FAT), System Administrator와 승인 대기 사용자를 포함한다.
- 기존 Backend 권한 계약을 그대로 보존하며 이 문서가 권한을 새로 정의하지 않는다.

### 8.3 산출물과 canonical 위치

- 이번 Task 산출물은 이 interview 문서, `tasks/user-flow-001-planning.md`, `tasks/user-flow-001-review.md`와 Product Roadmap update다.
- Planning·review 승인 뒤 이 Task의 구현 단계에서 `docs/` 아래 canonical 유저플로우 문서를 생성하고, 후속 Task가 화면·흐름을 바꾸면 함께 갱신한다.

### 8.4 내비게이션 골격과 미구현 처리

- 목표 메뉴 골격(Home·내 업무·프로젝트·부서 업무·알림·관리자)을 확정하되 각 메뉴에 구현·미구현 상태와 도입 Task를 병기한다. 미구현 메뉴는 해당 Task 승인·구현 전까지 Frontend에 노출하지 않는다.
- 진입점 유형은 내 업무 카드, 알림 deep link와 메뉴 직접 진입을 공통 패턴으로 사용한다.

### 8.5 흐름 상세도와 예외·복구 coverage

- 구현 구간은 실제 경로 기준 상세 flow, 미구현 구간은 진입점·핵심 행동·완료 시 다음 담당자 연결 수준의 중간 상세도로 설계하고 세부 화면·양식·정책은 후속 Task planning에 위임한다.
- 공통 예외 4종(권한 없음·승인 대기 진입, 담당자 부재 fallback, Pending 차단, 알림·deep link 진입 실패)을 한 번 정의하고, 구현 구간은 단계별 예외 상세, 미구현 구간은 Pending 연결과 다음 담당자 알림 수준까지만 기록한다.

### 8.6 데이터·연동·불변조건

- 기존 인증·권한·18단계 업무·내 업무·알림 계약, Backend authoritative policy, 알림 채널 matrix와 provider/event coverage를 보존한다.
- API·DB·migration·runtime·provider 변경, Persistent UAT write와 실제 외부 발송은 이번 Task에 포함하지 않는다.

### 8.7 포함·제외 범위

- 포함: 역할별 전체 여정과 화면·내비게이션 구조 기획, 내 업무·알림·deep link 진입과 정상·예외·복구 흐름 기획, planning과 Codex review, 승인 뒤 `docs/` canonical 문서 생성·갱신 규칙.
- 제외: 제품 코드 구현, Figma 시각 디자인, API·DB·migration·runtime·provider 변경, 후속 Task 자동 승인, commit·push·PR·merge.

### 8.8 사용자 확인 항목

#### A. 현재 우회 방식 문구

사용자 확정 문구: “기준선은 Roadmap이었고, 개별 화면은 그때그때 기획해서 만들었다.”

#### B. 성공 기준 세부 문구

사용자 확정: 13개 여정 단위, 목표 메뉴 골격과 구현 상태 병기, 공통 예외 4종과 이원화 coverage, 후속 Task 연결·`docs/` canonical 갱신 규칙을 포함한 전체 요약에 동의했다.

#### C. 접근성·390px 처리 방식

사용자 확정: 기존 동일 URL·overflow 0 원칙을 보존해 각 여정에 명시하고, 역할·화면별 narrow UX 상세와 우선순위는 Roadmap 1.3 `TASK-MOBILE-001`에 위임한다.

### 8.9 확인 체크리스트

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할 13개 여정과 권한 보존이 정확하다.
- [x] 정상·예외·복구 흐름의 상세도·coverage가 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] 확인 항목 A·B·C에 답했다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] 이 요약을 planning 입력으로 사용하는 데 동의한다.

- Deferred 비차단 결정: 없음
- 다음 단계: Interview가 `COMPLETED_CONFIRMED`, `userConfirmed: true`, `openBlockingDecisionCount: 0`으로 확정됐으므로 Fable planning을 시작할 수 있다. 이 요약 확인은 planning·구현 승인이 아니다.
- Fable 판정: `SUMMARY_CONFIRMATION_REQUIRED`

### Round 3 — 사용자 1차 답변

1. 현재 우회 방식 정정: 기준선은 Roadmap이었고, 개별 화면은 그때그때 기획해서 만들었다.
2. 성공 기준 확인 보류: 13개 역할이 무엇인지 설명을 요청했다.
3. 접근성·390px 처리 방식: 동의.
4. 전체 요약 확인 보류: 전체 요약을 들은 뒤 동의 여부를 판단하며, 추가하거나 삭제할 부분이 있을 수 있다.

- 후속 설명: 13개는 신규 권한 role이 아니라 영업·설계·생산관리·구매·자재·제조·물류 7개, IQC·LQC·OQC·전진검수/FAT 4개, System Administrator 1개와 승인 대기 사용자 1개의 유저플로우 여정 단위임을 설명했다.
- 최종 답변: 전체 요약 동의.
- 현재 판정: 사용자 요약 확인 완료. Planning을 시작할 수 있다.

## 9. 성공 기준

- 업무 결과: 사용자 확인을 거친 역할별 전체 유저플로우와 후속 기능 연결 기준이 planning에 명확히 정의된다.
- 권한·데이터 불변조건: 기존 Backend 권한, 18단계 업무와 확정된 데이터·알림 정책을 임의 변경하지 않는다.
- 자동 검증: 문서 diff·link·privacy·allowlist와 Codex Repository 대조 review를 통과한다.
- 사용자 검수: Fable 확인용 요약과 planning·review resolution을 사용자가 각각 명시 승인한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

사용자 확인으로 다음 상태를 확정했다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: true`
- `implementationApproved: true`
