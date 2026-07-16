# TASK-007A — Pending List 공통 모듈 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `WAIVED_BY_USER_FOR_EXPERIMENT`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 `TASK-007A`의 실험 전용 입력과 deep-interview 생략 근거를 고정한다. 사용자는 2026-07-16 이 worktree에서 인터뷰 없이 기획부터 코딩까지 연속 진행하도록 명시했다. 아래 기준선은 사용자의 제품 답변을 추정한 것이 아니라 Product Roadmap과 승인된 `TASK-USER-FLOW-001` 결과를 planning 입력으로 투영한 것이다. 이 예외는 대표 repo, GitHub main 또는 정식 구현 승인으로 전이되지 않는다.

## Task Identity Gate

- proposedTaskId: `TASK-007A`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-007A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: 부적합·PUNCH·제조 중단·기타 이슈를 공통 상태·조치·코멘트·재검사·종결 흐름으로 관리한다.
- Root Finding 또는 정책 결정: 현재 Pending List가 없어 후속 병목 집계·자재·검사·제조 흐름의 차단 상태를 공통 계약으로 연결할 수 없다.
- 변경·검증 경계: 현재 실험 워크트리 안의 기획·Backend·additive migration·Frontend·isolated test·synthetic screenshot만 포함한다.
- 보존할 불변조건: 대표 repo와 GitHub main, Persistent UAT, 실제 provider, 기존 18단계 stage 번호, 서버 권한 강제, 인앱 알림 원본을 변경하지 않는다.
- 예상 산출물: interview·Fable planning 원문·Codex review·구현·tests·Implementation report·SOP/User manual 포함 section·화면 screenshot.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

동일 목적의 branch·worktree·PR은 없고 Product Roadmap의 canonical Task `TASK-007A` 한 건만 확인했다.

## 사용자 실행 지시

- 사용자 요청일: 2026-07-16
- 실행 형태: 현재 main 기준 별도 실험 워크트리
- 자동 진행 지시: Fable 기획 후 Codex 내용 review를 수행하고 실험 구현·검증·화면 캡처까지 추가 승인 없이 이어서 진행한다.
- 인터뷰 생략 지시: 이 실험 worktree에서는 질문 round와 요약 확인을 생략하고 Repository 기준 권장안으로 곧바로 planning·review·구현한다.
- 제품 결정 위임: 실험 범위 안에서는 Fable의 Repository 근거 권장안을 기본값으로 채택하고, 원본 반영·commit·push·PR·merge는 별도 승인 전 금지한다.
- 게시 경계: 대표 repo와 GitHub main 반영은 미승인이다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자가 실험 전용 interview 생략과 planning→coding 연속 진행을 명시 | Fable planning 생성 시도 |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 내 업무·알림·workflow는 구현되어 있으나 부적합·PUNCH·제조 중단·기타 차단 이슈를 하나의 공통 모듈로 추적하는 화면과 상태 모델은 없다.
- 해결할 문제: 발생 단위·조치 담당·상태·코멘트·재검사·종결을 공통 흐름으로 연결하고 후속 병목 집계와 업무 handoff가 사용할 기반을 만든다.
- 현재 우회 방식: 관련 단계 문서와 알림 원칙만 있고 제품 내 중앙 추적 경로는 없다.
- 성공했을 때 사용자가 할 수 있는 일: 이슈를 등록하고 담당자에게 조치를 요청하며 진행·재검사·종결 이력을 한곳에서 확인한다.
- 하지 않을 경우 영향: 후속 자재·검사·제조 기능이 서로 다른 임시 차단 모델을 만들 가능성이 있다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 생산관리 | 전체 Pending 추적과 조치 업무 연결 | 담당 프로젝트 중심 후보 | 관리·재배정 후보 | 모든 변경 audit 필요 |
| 조치 담당 역할 | 배정 이슈 확인·코멘트·상태 진행 | 담당 Pending | 허용 상태 전이 | 변경자·시각·사유 보존 |
| 이슈 발생 역할 | 부적합·PUNCH·중단 등록 | 발생·담당 범위 | 생성·증빙 | 중복 방지 필요 |
| System Administrator | 운영·감사 확인 | 권한 범위 전체 | 업무 입력 우회 금지 | 관리자 audit 필요 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: 이슈 등록 → 조치 담당 지정 → 조치 요청/진행 → 재검사 요청 또는 종결 → 연계 업무·알림 반영.
- validation 실패: 필수 대상·유형·담당 정보 누락과 잘못된 상태 전이를 서버에서 차단한다.
- 동시 처리·중복: 동일 원인 event의 중복 Pending과 경쟁 상태 전이를 방지한다.
- 취소·재시도·복구: stage 번호를 후퇴시키지 않고 상태 전이와 append-only audit로 복구한다.
- 부분 실패와 rollback: Pending·내 업무·알림 생성이 하나의 transaction 경계인지 Fable이 권장안을 제시한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: Pending issue, 상태 이력, 코멘트, 대상 연결, 조치 담당, notification/work item 연결.
- 상태 전이: Roadmap 초안 `등록 → 조치 요청 → 조치 중 → 재검사 요청 → 종결`을 기준으로 검토한다.
- 보존·감사·삭제: 완료 기록을 직접 덮어쓰거나 삭제하지 않고 변경 이력을 보존한다.
- attachment·Excel·PDF: 첨부 storage가 외부 blocker이므로 text-first 실험과 최소 synthetic attachment 경계를 비교한다.
- 외부 연동·notification: 인앱 원본을 우선하며 actual Teams/Mail 발송은 금지한다.
- migration·기존 데이터: additive migration만 허용하고 기존 migration을 수정하지 않는다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 전용 Pending workspace와 프로젝트/내 업무 contextual 진입을 함께 후보로 검토한다.
- loading·empty·error·success feedback: 기존 공통 Action Feedback과 한글 다음 행동 안내를 사용한다.
- 접근성·390px·Teams narrow: keyboard·label·focus·aria-live와 page-level overflow 0을 검증한다.
- UAT와 rollout: isolated synthetic 환경만 사용하고 Persistent UAT write를 수행하지 않는다.
- rollback과 운영자 대응: 실험 branch 폐기 또는 additive migration forward-fix 계획을 기록한다.

## 6. 포함·제외 범위

### 포함

- Pending 목록·상세·생성·상태·조치 담당·코멘트·재검사 요청·종결
- 프로젝트·패널·구매품목·제조 단계 연결을 수용할 확장 가능한 대상 모델
- 내 업무·인앱 알림 연결과 중복 방지
- 역할별 조회·mutation 권한
- Desktop·390px 화면과 synthetic screenshot

### 제외

- 검사별 상세 체크리스트·PDF 성적서
- 자재 도착·IQC·키팅·제조·물류의 실제 업무 화면 구현
- actual Teams/Mail/Channel 발송
- Persistent UAT migration·write·runtime handover
- 사용자별 알림 preference와 관리자 Pending 유형 편집

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 전용 workspace와 contextual 진입의 관계 | 한쪽만 제공하면 탐색 또는 현장 맥락이 약해짐 | 전용 `/pending` + 연결 대상 deep link | Repository 권장안 자동 채택 | No |
| 2 | 첨부 storage 미확정 상태의 MVP | binary upload는 운영 정책을 선결로 요구 | text-first + 첨부 계약은 보류 | Repository blocker를 우선 적용 | No |
| 3 | 기본 상태 흐름 | 단순 open/close는 조치·재검사 책임을 표현하지 못함 | 등록→조치 요청→조치 중→재검사 요청→종결 | Roadmap 상태 모델 채택 | No |
| 4 | 알림 채널 | 실제 provider는 실험·운영 위험이 큼 | 인앱 원본만 생성 | 기존 알림 원칙 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 공통 Pending 추적·조치·재검사·종결 기반 부재.
- 권장 범위: 전용 Pending workspace, 생성·담당·상태·코멘트·재검사·종결, 연결 대상, 내 업무·인앱 알림 기반.
- 확정한 정책: stage 번호 전진-only, 서버 권한 authoritative, 인앱 알림 원본, actual provider·Persistent UAT 금지.
- 명시적 제외: 검사 상세·후속 업무 단계 구현·원본 게시.
- Deferred 비차단 결정: 운영 attachment storage·backup·restore.
- interview 판정: `COMPLETED_CONFIRMED` — 사용자 명시적 실험 전용 생략 지시에 따른 waiver이며 Fable interview 완료를 뜻하지 않는다.

## 9. 성공 기준

- 업무 결과: 등록된 이슈가 담당자 조치·재검사 또는 종결까지 추적된다.
- 권한·데이터 불변조건: 허용 역할만 mutation하고 모든 상태 변경·코멘트를 감사할 수 있다.
- 자동 검증: Backend·migration·authorization·transaction, Frontend lint/typecheck/unit/build, isolated E2E, desktop/390px smoke.
- 사용자 검수: 이번 실험은 각 화면 screenshot 보고로 대체하며 실제 사용자 검수 완료로 표시하지 않는다.

## 10. 사용자 확인

- [x] 실험 범위에서 Fable 권장안과 Codex review resolution을 별도 승인 없이 구현하도록 지시했다.
- [x] 사용자 명시적 interview 생략 지시 기록
- [x] Blocking 결정 0 확인
- [x] Fable planning 입력 상태 확정

Planning 직전 목표 상태:

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`
