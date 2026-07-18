# TASK-UX-001 A2 — 업무 화면 Action Feedback 확대 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- fastTrackSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- canonicalTaskId: `TASK-UX-001`
- completedSliceExcluded: `A1`

이 문서는 `experiment/*` fast-track 예외에 따라 사용자-facing interview 없이 작성한 A2 기획 source다. 사용자는 이 실험 계보에서 신규 기능을 인터뷰·중간 확인 없이 권장안으로 기획·검토·구현하고 결과까지 보여 주며, 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge를 제외하도록 반복해서 명시했다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | experiment standing instruction 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: A1의 공통 `useActionFeedback`·`ActionFeedback` 계약은 내 업무와 알림에 적용됐다. 생산계획·구매·자재·패널·선택 Excel 화면은 저장·검사·업로드·내보내기 결과를 화면별 message 또는 분산된 상태로 보여 준다.
- 해결할 문제: 어느 action이 처리 중인지, 어떤 field를 고쳐야 하는지, mutation은 성공했지만 refresh/download가 실패했는지를 action 위치에서 일관되게 파악하기 어렵다. 일부 화면은 중복 submit·bulk/row 충돌과 focus 안내가 공통 계약에 포함되지 않는다.
- 현재 우회 방식: 상단 message를 찾고, 목록을 다시 불러오거나 버튼을 재클릭하고, 오류 field를 눈으로 훑는다.
- 성공했을 때 사용자가 할 수 있는 일: 대상 화면에서 action 인접 loading/success/error/partial과 다음 행동을 확인하고, 첫 field 오류로 이동하며, 선택·필터·스크롤 context를 잃지 않고 재시도한다.
- 하지 않을 경우 영향: 모바일 현장 사용자의 반복 요청·오입력 가능성이 남고, keyboard·screen reader 사용자는 결과와 오류 위치를 놓칠 수 있다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 생산관리 담당 | 생산계획 저장·담당자/일정 입력 | 기존 project scope | 기존 `production-planning.update` 범위 | 기존 Backend 권한·audit 유지 |
| 구매 담당 | 구매 정보 저장·Excel preview/apply | 기존 project scope | 기존 구매 mutation 범위 | 기존 validation·audit 유지 |
| 자재·품질 담당 | 자재 입고·IQC·키팅 관련 action | 기존 project scope | 기존 담당 mutation 범위 | 다른 부서는 조회만 가능 유지 |
| 설계 담당 | 패널 정보 저장·Excel preview/apply | 기존 project scope | 기존 패널 mutation 범위 | 기존 목포장 size validation 유지 |
| 조회·내보내기 사용자 | 선택 row Excel 내보내기 | 기존 조회 scope | export 생성만 | 기존 server-side selection·권한 재검증 유지 |

새 권한·역할·업무 상태를 만들지 않는다. Backend를 권한·validation의 authoritative source로 유지한다.

## 3. 정상·예외·복구 흐름

- 정상 흐름: action 실행 → 해당 action scope만 busy/disabled → action 인접 성공 안내 → 필요한 목록/summary만 재조회 → 선택·필터·스크롤 context 유지.
- field validation 실패: field 아래 한글 오류와 요약 feedback을 함께 표시하고 첫 invalid field로 focus한다. `aria-describedby`와 live announcement를 연결한다.
- 동시 처리·중복: 동일 action scope의 중복 submit을 차단한다. bulk·row 또는 preview·apply처럼 같은 data를 바꾸는 action conflict는 양방향으로 차단한다.
- 취소·재시도·복구: 실패 시 입력값·선택·filter를 보존하고 같은 action을 재시도할 수 있다. 403/404/409/network는 A1의 구조화 guidance를 재사용한다.
- 부분 실패와 rollback: mutation 성공 뒤 refresh 또는 client download가 실패하면 mutation 실패로 되돌려 표시하지 않고 partial과 다음 행동을 안내한다. 서버 mutation rollback 정책은 변경하지 않는다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: A1의 화면 session feedback/busy state를 재사용·확장한다. 제품 DB 개념은 추가하지 않는다.
- 상태 전이: A1의 `idle → loading → success | error | partial`을 유지한다.
- 보존·감사·삭제: feedback은 영구 저장하지 않는다. 기존 audit·export event·업무 이력을 변경하지 않는다.
- attachment·Excel·PDF: 기존 Excel preview/apply/download의 결과 UX만 다룬다. workbook 내용·export row cap·선택 재검증은 변경하지 않는다.
- 외부 연동·notification: 신규 notification event와 실제 provider 호출은 제외한다.
- migration·기존 데이터: migration 없음. 필요성이 발견되면 구현을 중단한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 생산계획, 구매, 자재 입고/IQC/키팅, 프로젝트 패널 정보, 공통 선택 Excel action.
- loading·empty·error·success feedback: A1 공통 hook/component와 guidance mapper를 재사용하며 중복 상단 banner는 제거하거나 contextual region 하나로 통합한다.
- 접근성·390px·Teams narrow: keyboard, label, focus order, field error 연결, `role=status/alert`, 390px page-level overflow 0. 모바일은 action과 feedback을 card/section 안에 배치한다.
- UAT와 rollout: isolated synthetic tests와 local browser screenshot만 사용한다. Persistent UAT와 대표 runtime은 변경하지 않는다.
- rollback과 운영자 대응: Frontend-only revert가 기본이다. API 계약 보정·migration 필요 시 blocking decision으로 전환한다.

## 6. 포함·제외 범위

### 포함

- A1 공통 feedback hook/component/guidance를 A2 화면에 재사용하기 위한 최소 확장
- 생산계획·구매·자재·패널의 저장/preview/apply/action 인접 feedback, field error·focus·`aria-live`
- 공통 선택 Excel 내보내기의 busy·성공·실패·partial과 selection 보존
- duplicate submit, 관련 scope conflict, stale response guard가 필요한 화면의 context 보존
- desktop·390px synthetic browser 검증과 페이지별 대표 screenshot
- 관련 Frontend unit·mock UI·가능한 isolated Full-Stack 회귀

### 제외

- 완료된 A1 내 업무·알림의 재구현
- 업무 규칙·권한·상태 전이·API 기능·DB·migration의 신규 능력
- Excel column picker·multi-sheet·row cap 변경
- 알림 delivery 재처리·사용자 preference·실제 provider
- 전역 toast store, 전체 `StateMessage` 재설계, 무관 화면 redesign
- 대표 repo·`main`·Persistent UAT·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | A2 적용 깊이 | 화면별 임시 message / A1 공통 계약 재사용 / 전역 상태 store | A1 공통 계약 재사용 + 필요한 최소 extension | Fable 권장안 자동 채택 | No |
| 2 | field 오류 표현 | 상단 요약만 / field만 / 요약+field 연결 | 요약+field 오류, 첫 invalid focus | Fable 권장안 자동 채택 | No |
| 3 | Excel 성공 기준 | 클릭 즉시 성공 / server blob 수신 / 브라우저 저장 완료 추정 | server blob 수신까지 성공, client trigger 예외는 partial/error로 구조화 | Fable 권장안 자동 채택 | No |
| 4 | 화면별 확대 순서 | 코드 순서 / 사용자 위험 순서 / 전면 동시 수정 | mutation·bulk 위험 순서로 vertical slice 후 공통 회귀 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: A2 업무 화면의 분산 feedback·field 오류·중복 action을 A1 공통 계약으로 통일한다.
- 권장 범위: 생산계획·구매·자재·패널·선택 Excel에 action 인접 상태, field focus/announcement, context 보존을 완결한다.
- 확정한 정책: 기존 Backend 권한·validation·audit 보존, A1 재사용, mobile task-first, isolated 검증, 권장안 자동 채택.
- 명시적 제외: A1 재구현, 신규 Backend/DB/provider/Excel 기능, main과 게시.
- Deferred 비차단 결정: 전역 toast와 다른 관리자/Pending 화면 전면 확대.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 대상 action의 처리 중·성공·실패·부분 성공과 다음 행동이 action 근처에 나타나며 field 오류는 첫 invalid field로 이어진다.
- 권한·데이터 불변조건: 기존 API·권한·상태 전이·audit·선택 export contract 불변, 중복 요청 차단, context 보존, main/Persistent/provider 변경 0.
- 자동 검증: Frontend lint·typecheck·unit·build, 관련 mock/isolated E2E, desktop·390px loading/error/success/partial·overflow.
- 사용자 검수: 페이지별 대표 screenshot으로 `사용자 검수 대기 — 마지막 일괄 검수` 전환.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] 실험 standing instruction에 따라 Fable 권장안을 planning 입력으로 자동 채택한다.
