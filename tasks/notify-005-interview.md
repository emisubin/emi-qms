# TASK-NOTIFY-005 — 사용자별 알림 설정 Deep Interview

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

- 현재 업무 방식: 모든 인앱 알림은 원본으로 남고, Teams 개인 알림·Teams 통합 채널·메일은 코드와 운영 설정이 정한 channel matrix에 따라 생성된다. 사용자는 자신의 외부 채널 수신 방식을 조정할 수 없다.
- 해결할 문제: 사용자마다 필요한 알림 밀도와 채널이 다른데도 모든 허용 외부 알림이 동일하게 적용되어 소음이 생기고, 반대로 필수 알림을 사용자가 잘못 끌 수 있는 설정을 단순 추가하면 업무 누락 위험이 생긴다.
- 현재 우회 방식: 사용자가 Teams·메일 클라이언트에서 별도로 알림을 줄이거나 운영자에게 요청하지만, 시스템 event별 선택과 감사 이력은 남지 않는다.
- 성공했을 때 사용자가 할 수 있는 일: 인앱 원본과 업무상 필수 알림을 보존하면서 허용된 event·외부 채널의 수신 여부를 직접 확인하고 저장한다. 관리자는 정책상 잠긴 항목과 사용자 선택 상태를 구분해 지원할 수 있다.
- 하지 않을 경우 영향: 외부 알림 소음으로 중요한 알림의 가시성이 떨어지고, 사용자별 요구를 운영자가 수동으로 처리해 정책 drift와 추적 누락이 생긴다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| Active 일반 사용자 | 본인의 알림 설정 조회·허용 항목 변경·기본값 복원 | 본인 preference와 공개 taxonomy | 본인의 변경 가능 외부 채널만 | 변경 전후와 actor audit |
| System Administrator | 사용자 지원용 설정 조회·정책 상태 확인, 필요 시 승인된 범위에서 대리 변경 | active 사용자 preference와 공개 taxonomy | Fable이 권장하는 최소 admin 범위 | 대리 변경 actor·대상·전후 audit |

승인 대기·비활성 사용자는 업무 설정 화면을 사용하지 않는다. Backend가 본인 범위·관리자 범위·필수 항목 잠금의 authoritative source다.

## 3. 정상·예외·복구 흐름

- 정상 흐름: 설정 진입 → event group별 인앱·Teams 개인·메일 상태 확인 → 변경 가능한 외부 채널 선택 → 저장 → 서버 재조회 결과와 action 인접 성공 안내 확인.
- validation 실패: 필수·비지원 조합, stale taxonomy/version 또는 권한 위반을 서버가 차단하고 해당 설정 근처에 한글 오류와 복구 행동을 표시한다.
- 동시 처리·중복: 같은 사용자의 겹친 저장은 lost update를 피하고, audit를 중복 생성하지 않는 최소 concurrency 계약을 Fable이 권장한다.
- 취소·재시도·복구: 저장 전 취소와 서버 기준 다시 불러오기, 사용자 기본값 복원을 제공한다. 실패 시 로컬 편집값과 오류 위치를 유지한다.
- 부분 실패와 rollback: preference 저장과 audit는 한 transaction으로 처리한다. 외부 provider 호출이나 이미 생성된 delivery 변경은 이 action에서 수행하지 않는다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: event/channel taxonomy, 사용자별 override, effective setting, 변경 audit 후보.
- 상태 전이: 기본값 사용 → 사용자 override 저장 → effective setting 계산 → 기본값 복원 시 override 제거 또는 동등한 명시 상태. 구체 모델은 Fable 권장안으로 정한다.
- 보존·감사·삭제: 변경 전후, actor, 대상 사용자와 시각을 append-only로 감사한다. 사용자 삭제 lifecycle과의 연결은 기존 reference·purge 정책을 보존한다.
- attachment·Excel·PDF: N/A. 설정 export/import는 포함하지 않는다.
- 외부 연동·notification: dispatcher와 escalation delivery 생성 전에 effective preference를 적용한다. 실제 Teams/Mail/Activity provider 호출은 검증하지 않고 fake/dry-run만 사용한다.
- migration·기존 데이터: additive migration이 예상된다. 기존 사용자는 명시 override가 없어도 현재 channel matrix와 호환되는 기본값을 유지해야 한다.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: 개인 설정 route와 모바일 상태/계정 sheet 또는 명확한 계정 진입점. 관리자 지원 화면은 기존 사용자 관리 흐름과 중복되지 않는 최소 경로를 사용한다.
- loading·empty·error·success feedback: taxonomy loading, 저장 중, 저장 성공, validation·권한·stale·조회 실패를 구분하고 action 근처 `aria-live`와 재시도를 제공한다.
- 접근성·390px·Teams narrow: PC 표 축소판이 아니라 event group card와 채널별 명확한 control을 사용한다. 잠긴 필수 항목은 disabled 이유를 텍스트로 제공하고 page-level overflow 0을 유지한다.
- UAT와 rollout: isolated PostgreSQL, fake/dry-run provider, desktop·390px screenshot만 사용한다. Persistent UAT migration·runtime handover는 별도 승인 대상이다.
- rollback과 운영자 대응: 아직 Persistent UAT에 적용하지 않는다. 실험 rollback은 branch commit revert이며, migration 운영 rollback은 additive forward-fix를 기본으로 별도 SOP에서 다룬다.

## 6. 포함·제외 범위

### 포함

- 고정된 event/channel taxonomy와 필수·선택 가능 정책
- 본인 preference 조회·수정·기본값 복원 API와 사용자 설정 화면
- 최소 관리자 지원 조회·대리 변경 범위에 대한 Fable 권장안
- dispatcher·escalation의 외부 delivery 생성 전 effective preference 적용
- 기본값 호환, audit, concurrency·validation·권한 검증
- Desktop·390px 페이지 screenshot과 관련 Backend·Frontend·isolated Full-Stack 검증

### 제외

- 인앱 notification 원본 opt-out 또는 읽음 계약 변경
- 법적·업무상 필수 알림 해제
- Teams 통합 채널 공지를 개인 설정으로 끄는 기능
- provider 신뢰성·terminal Failed 재처리 재구현
- 신규 외부 채널, 운영 URL·manifest·secret 변경
- 이미 생성된 delivery 취소·삭제·재작성
- 설정 Excel export/import, 조직 단위 캠페인·quiet hours
- 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | event taxonomy 세분화 | raw delivery type / 사용자 이해 중심 group / 개별 notification type | 현재 delivery type을 안정적 내부 key로 쓰되 사용자-facing group과 정책 metadata를 분리하는 최소안 검토 | Fable 권장안 자동 채택 | No |
| 2 | 필수 알림 범위 | 모두 선택 가능 / 긴급·에스컬레이션 잠금 / 채널별 세부 잠금 | 인앱 전체와 업무상 필수 긴급·상위 에스컬레이션을 잠그고 소음성 요약·예고·참조만 opt-out 후보로 검토 | Fable 권장안 자동 채택 | No |
| 3 | 기본값 표현 | 모든 사용자 row seed / sparse override / 계산된 snapshot | 기존 사용자 호환과 taxonomy 확장을 고려한 sparse override + 서버 effective projection 우선 검토 | Fable 권장안 자동 채택 | No |
| 4 | 관리자 범위 | read-only / 대리 변경 / 전역 정책 편집 | 사용자 지원을 위한 조회·명시적 대리 변경까지만 검토하고 taxonomy 전역 편집은 제외 | Fable 권장안 자동 채택 | No |
| 5 | concurrency | last-write-wins / version token / row lock | 단일 사용자 저장의 version token 또는 동등한 stale 차단과 transaction audit 권장 | Fable 권장안 자동 채택 | No |

## 8. Fable 확인용 요약

- 해결할 문제: 외부 알림 소음을 사용자가 허용 범위에서 조절하면서 인앱 원본·필수 업무 알림·기존 채널 정책을 보존한다.
- 권장 범위: 고정 taxonomy, sparse user override, effective setting API, 본인 설정 UI, 최소 admin 지원, dispatcher·escalation 적용, audit를 하나의 vertical slice로 완결한다.
- 확정한 정책: 인앱 opt-out 금지, 필수 알림 해제 금지, TeamsChannel 개인 opt-out 금지, 기존 사용자의 기본 delivery 호환, Backend authoritative, 실제 provider·Persistent UAT 제외.
- 명시적 제외: 신규 채널, quiet hours, 이미 생성된 delivery 변경, provider reliability, Excel, main·게시.
- Deferred 비차단 결정: 조직 단위 default 정책 편집, taxonomy 관리자 편집, 설정 import/export와 quiet hours.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: 사용자가 변경 가능한 외부 알림을 저장·재로그인 후 다시 확인하고 기본값으로 복원한다.
- 권한·데이터 불변조건: 인앱 원본과 필수 조합은 잠기며 Backend가 강제한다. preference와 audit는 원자 저장되고 기존 사용자 기본 delivery가 유지된다.
- 자동 검증: Backend build·전체/targeted/migration/authorization, Frontend lint·typecheck·unit·build, 관련 isolated Full-Stack E2E, fake/dry-run delivery 생성 matrix, desktop·390px overflow와 action feedback.
- 사용자 검수: 개인 설정과 관리자 지원 화면의 페이지별 screenshot으로 검수 대기 전환.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] 실험 standing instruction에 따라 Fable 권장안을 planning 입력으로 자동 채택한다.
