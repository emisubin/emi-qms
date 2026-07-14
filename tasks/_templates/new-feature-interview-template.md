# <TASK-ID> — <신규 기능명> Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `QUESTIONS_REQUIRED`
- userConfirmed: false
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 Fable 5가 사용자와 진행하는 deep-interview를 round별로 고정한다. Codex는 Fable 질문과 사용자 답변을 전달·기록하지만 업무 질문을 대신 만들거나 답하지 않는다. Interview 완료는 planning 또는 구현 승인이 아니다.

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 대기 | Fable 질문 생성 |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식:
- 해결할 문제:
- 현재 우회 방식:
- 성공했을 때 사용자가 할 수 있는 일:
- 하지 않을 경우 영향:

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
|  |  |  |  |  |

## 3. 정상·예외·복구 흐름

- 정상 흐름:
- validation 실패:
- 동시 처리·중복:
- 취소·재시도·복구:
- 부분 실패와 rollback:

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념:
- 상태 전이:
- 보존·감사·삭제:
- attachment·Excel·PDF:
- 외부 연동·notification:
- migration·기존 데이터:

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동:
- loading·empty·error·success feedback:
- 접근성·390px·Teams narrow:
- UAT와 rollout:
- rollback과 운영자 대응:

## 6. 포함·제외 범위

### 포함

- <포함 항목>

### 제외

- <제외 항목>

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 |  |  |  | 대기 | Yes |

## 8. Fable 확인용 요약

- 해결할 문제:
- 권장 범위:
- 확정한 정책:
- 명시적 제외:
- Deferred 비차단 결정:
- Fable 판정: `QUESTIONS_REQUIRED`

## 9. 성공 기준

- 업무 결과:
- 권한·데이터 불변조건:
- 자동 검증:
- 사용자 검수:

## 10. 사용자 확인

- [ ] 업무 문제와 기대 결과가 정확하다.
- [ ] 대상 역할과 권한이 정확하다.
- [ ] 정상·예외·복구 흐름이 정확하다.
- [ ] 포함·제외 범위가 정확하다.
- [ ] Blocking 결정이 남아 있지 않다.
- [ ] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

사용자 확인 후에만 다음 상태로 바꾼다.

- `interviewStatus: COMPLETED_CONFIRMED`
- `userConfirmed: true`
- `openBlockingDecisionCount: 0`
- `planningApproved: false`
- `implementationApproved: false`
