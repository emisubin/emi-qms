# TASK-UX-001 — 기존 업무 화면 Action Feedback UX 확대 Deep Interview

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

이 문서는 `experiment/*` fast-track 예외에 따라 사용자-facing interview 없이 작성한 기획 source다. 사용자는 이 실험 계보에서 신규 기능을 인터뷰·중간 확인 없이 권장안으로 기획·검토·구현하고, 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge를 제외하도록 반복해서 명시했다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | experiment standing instruction 자동 채택 | Fable 1차 planning |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 내 업무와 알림 화면에서 업무 완료·업무 화면 이동·개별 읽음·전체 읽음을 실행한다. 일부 공통 `ActionFeedback`과 화면별 message가 공존한다.
- 해결할 문제: action 결과가 화면 상단의 문자열 메시지로만 표시되거나, 진행 중 중복 실행이 차단되지 않고, 성공·오류 판정이 문자열 포함 여부에 의존한다. 사용자는 어느 action이 처리 중인지, 실패 뒤 무엇을 해야 하는지 즉시 알기 어렵다.
- 현재 우회 방식: action 뒤 목록 갱신을 기다리거나 상단 메시지를 찾아보고 다시 실행한다.
- 성공했을 때 사용자가 할 수 있는 일: 내 업무·알림의 action 위치에서 처리 중·성공·실패와 다음 행동을 즉시 확인하고, 중복 요청 없이 안전하게 재시도한다.
- 하지 않을 경우 영향: 모바일 현장 사용자가 action 결과를 놓치거나 같은 요청을 반복할 수 있고, keyboard·screen reader 사용자는 결과와 오류 위치를 일관되게 파악하기 어렵다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 업무 담당 사용자 | 내 업무 시작·완료·업무 화면 이동 | 기존 본인 업무 scope | 기존 허용 action만 | 기존 Backend 권한·업무 이력 유지 |
| 알림 수신 사용자 | 개별 읽음·전체 읽음·상세/업무 이동 | 기존 본인 알림 scope | 기존 읽음 상태만 | 기존 Backend 권한·읽음 기록 유지 |

새 권한, 역할, API 업무 능력은 추가하지 않는다. Backend를 권한·validation의 authoritative source로 유지한다.

## 3. 정상·예외·복구 흐름

- 정상 흐름: action 실행 → 해당 control만 진행 중/비활성 → 성공 결과를 action 근처에 표시 → 최신 목록·badge 재조회 → 사용자가 다음 행동을 선택한다.
- validation 실패: 오류 tone, 한글 원인과 다음 행동을 action 근처에 표시하고 해당 feedback 또는 첫 오류로 focus를 이동한다.
- 동시 처리·중복: 같은 action의 진행 중 중복 submit을 차단한다. 서로 다른 row action의 병렬 허용 여부는 Fable 권장안으로 정한다.
- 취소·재시도·복구: 실패 시 현재 tab·selection·사용자 context를 유지하고 동일 action을 다시 시도할 수 있다.
- 부분 실패와 rollback: mutation 성공 뒤 refresh가 실패한 경우 mutation 실패로 오인하지 않고 부분 성공 또는 최신 상태 재조회 안내를 제공하는 방안을 Fable이 검토한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: Frontend의 구조화된 feedback state와 action busy state 후보. 제품 DB 개념 추가는 기본 범위가 아니다.
- 상태 전이: `idle → loading → success | error | partial`과 명시적 dismiss/reset 후보.
- 보존·감사·삭제: feedback은 화면 session 상태이며 영구 저장하지 않는다. 기존 Backend audit·work item·notification 기록을 변경하지 않는다.
- attachment·Excel·PDF: A1에서는 N/A. 기존 선택 Excel action은 회귀 검증만 한다.
- 외부 연동·notification: 실제 Teams/Mail/Activity provider 호출이나 신규 notification event는 포함하지 않는다.
- migration·기존 데이터: migration 없음이 권장 기준이며 필요성이 발견되면 blocking decision으로 처리한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: `/my-work`의 이동·완료, `/notifications`의 개별 읽음·전체 읽음. Desktop과 모바일 적응형 layout을 모두 다룬다.
- loading·empty·error·success feedback: 공통 계약으로 tone·role·`aria-live`·다음 행동·진행 중 label을 명시한다.
- 접근성·390px·Teams narrow: keyboard, label, focus order, screen-reader announcement, 390px page-level overflow 0을 유지한다. 모바일은 PC 화면 축소판으로 만들지 않는다.
- UAT와 rollout: isolated synthetic test와 local browser screenshot만 사용한다. Persistent UAT는 변경하지 않는다.
- rollback과 운영자 대응: Frontend-only 되돌림이 기본이다. API/DB 변경이 필요하면 별도 경계로 분리한다.

## 6. 포함·제외 범위

### 포함

- A1 공통 feedback contract와 재사용 가능한 Frontend component/hook 또는 동등한 최소 구조
- 내 업무·알림 action 인접 loading/success/error/partial, 중복 submit 차단, focus와 `aria-live`
- loading·empty·error·authorization denied·target-not-found 다음 행동 구분 보강
- Desktop·390px browser 검증과 두 페이지 screenshot
- 관련 Frontend unit·isolated Full-Stack E2E 회귀

### 제외

- A2 생산계획·구매·자재·패널·Excel 화면 전면 확대
- 업무 규칙·권한·API·DB·migration의 신규 능력
- 알림 delivery 재처리, 사용자별 알림 설정, 실제 provider
- 대표 repo·`main`·Persistent UAT·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | 공통 feedback 상태의 최소 구조 | component만 / hook+component / 전역 toast store | A1 두 화면의 동시 action·focus를 감당하는 최소 hook+component를 우선 검토 | Fable 권장안 자동 채택 | No |
| 2 | 성공 뒤 refresh 실패 표시 | 전체 실패 / 성공만 / partial 분리 | mutation 성공과 refresh 실패를 구분하는 partial 안내 | Fable 권장안 자동 채택 | No |
| 3 | 행 action 병렬성 | 화면 전체 잠금 / 해당 행 잠금 / 무제한 | 동일 행 잠금, bulk action은 관련 scope 잠금 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 내 업무·알림 action의 결과·진행·복구 안내를 action 위치에서 일관되게 제공한다.
- 권장 범위: A1 공통 계약과 두 핵심 화면만 완결하고 A2를 분리한다.
- 확정한 정책: Backend 권한·업무 규칙 보존, 모바일 우선 적응형 UX, 중복 submit 차단, 접근 가능한 feedback, isolated 검증.
- 명시적 제외: A2 전면 확대, 신규 Backend/DB/provider 능력, main과 게시.
- Deferred 비차단 결정: A2 대상 화면별 확대 순서와 전역 toast 필요성.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 사용자가 내 업무·알림 action의 처리 중·성공·실패·부분 성공과 다음 행동을 action 근처에서 확인한다.
- 권한·데이터 불변조건: 기존 API·권한·업무 상태 전이·audit 불변, 중복 요청 방지, main/Persistent/provider 변경 0.
- 자동 검증: Frontend 최소·전체 regression, 관련 isolated Full-Stack E2E, desktop·390px loading/empty/error/success와 overflow.
- 사용자 검수: 페이지별 screenshot으로 검수 대기 전환.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] 실험 standing instruction에 따라 Fable 권장안을 planning 입력으로 자동 채택한다.
