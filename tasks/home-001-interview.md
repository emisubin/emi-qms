# TASK-HOME-001 — PC·모바일 Home MVP Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 2
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

## 0. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `QUESTIONS_REQUIRED` | 0 | 사용자 standing experiment directive: interview 왕복 없이 Fable 권장안을 자동 채택 | Fable 질문·권장안 생성 |
| 1 | `QUESTIONS_REQUIRED` | 5 | `tasks/home-001-interview-round-1-fable.md`; 권장안 1-A·2-A·3-A·4-A·5-A 자동 채택 | Fable 확인용 요약 생성 |
| 2 | `SUMMARY_CONFIRMATION_REQUIRED` | 0 | `tasks/home-001-interview-round-2-fable.md`; standing directive로 요약 확인, deferred 권장안 자동 채택 | Fable planning 작성 |

## 1. 업무 문제와 기대 결과

- 현재 업무 방식: 사용자가 내 업무·프로젝트·Pending·알림 화면을 각각 열어 우선순위를 확인한다.
- 해결할 문제: 로그인 직후 현재 역할에서 확인할 핵심 업무와 프로젝트 병목을 한 화면에서 파악하기 어렵다.
- 현재 우회 방식: 하단/사이드 메뉴와 각 dashboard를 순차 이동한다.
- 성공했을 때 사용자가 할 수 있는 일: 현재 source data로 제공 가능한 widget을 Home에서 확인하고 원본 화면으로 이동한다.
- 하지 않을 경우 영향: 모바일 navigation은 빨라졌지만 여러 업무 영역의 조망은 계속 분산된다.

## 2. 대상 사용자와 권한

| 역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 승인·감사 요구 |
| --- | --- | --- | --- | --- |
| 로그인된 active 사용자 | 역할별 Home widget 조회와 원본 화면 이동 | 기존 API와 permission 범위 | Home 자체 mutation 없음 | 기존 권한·audit 계약 유지 |

## 3. 정상·예외·복구 흐름

- 정상 흐름: Home 진입 → 허용 widget 조회 → 핵심 수치·항목 확인 → 원본 화면 이동.
- validation 실패: N/A — Home은 조회 전용.
- 동시 처리·중복: widget 조회만 수행하며 원본 mutation을 만들지 않는다.
- 취소·재시도·복구: widget별 재시도 또는 원본 화면 이동을 제공한다.
- 부분 실패와 rollback: 한 widget 실패가 다른 widget과 navigation을 차단하지 않아야 한다.

## 4. Data·integration·lifecycle

- 신규 또는 기존 data 개념: widget slot presentation만 신규, 업무 data는 기존 source 재사용.
- 상태 전이: 없음.
- 보존·감사·삭제: 신규 persistence 없음.
- attachment·Excel·PDF: 없음.
- 외부 연동·notification: 신규 발송 없음.
- migration·기존 데이터: migration 없음 권장.

## 5. UX와 운영 적용

- 진입 화면과 핵심 행동: Home을 PC sidebar·모바일 핵심 navigation에 추가하고 widget에서 원본 route로 이동.
- loading·empty·error·success feedback: widget별 독립 상태.
- 접근성·390px·Teams narrow: page overflow 0, keyboard/heading/link 계약과 44px touch target.
- UAT와 rollout: isolated synthetic E2E·screenshot 후 사용자 검수.
- rollback과 운영자 대응: 실험 branch를 대표 repo에 적용하지 않으면 됨.

## 6. 포함·제외 범위

### 포함

- 현재 source data 기반 PC·모바일 Home
- widget slot과 TASK-007B aggregate 재사용
- widget별 loading·empty·error
- 역할·권한별 노출과 원본 화면 deep link

### 제외

- source data가 없는 예측 widget
- 신규 mutation·Backend persistence·migration·provider
- 시각 브랜드 전면 개편

## 7. 선택과 결정

| 번호 | 질문 | 선택지 비교 | 권장안 | 사용자 결정 | Blocking |
| ---: | --- | --- | --- | --- | --- |
| 1 | MVP widget 구성 | A 4종 / B 2종 / C dashboard 확대 | A — 내 업무·프로젝트 병목·Pending·알림 4종 | A | Yes |
| 2 | Home 진입 경로 | A `/` Home·`/projects` 분리 / B `/home`만 / C 로그인 직후 1회 | A — `/` Home, `/projects` 목록, Teams context 보존 | A | Yes |
| 3 | 모바일 Home 위치 | A 첫 핵심 tab / B 기본 진입만 / C 알림 대체 | A — Home 첫 tab, 기존 핵심 tab·알림 유지 | A | Yes |
| 4 | widget 조회 방식 | A 기존 API 독립 병렬 / B 신규 `/api/home` | A — Frontend 조합, Backend 무변경 | A | Yes |
| 5 | 권한·전체 empty | A 권한 없는 slot 숨김 / B 잠금 카드 | A — 숨김, 0개면 허용 메뉴 안내 | A | Yes |

## 8. Fable 확인용 요약

- 해결할 문제: 분산된 현재 업무·병목·알림 정보를 로그인 직후 역할 범위 안에서 조망한다.
- 권장 범위: 현재 data source 기반 read-only widget-slot Home.
- 확정한 정책: 4종 widget, `/` Home·`/projects` 분리, 모바일 Home 첫 tab, 기존 API 독립 조회, 권한 없는 widget 숨김을 채택한다.
- 명시적 제외: 예측, 신규 persistence·mutation·provider, canonical 환경 변경.
- Deferred 비차단 결정: 병목 Top 5, widget 순서 내 업무→프로젝트 병목→Pending→알림, 진입 시 조회+widget별 수동 재시도·자동 polling 없음.
- Fable 판정: `COMPLETED_CONFIRMED`

## 9. 성공 기준

- 업무 결과: Home에서 현재 확인할 핵심 항목과 다음 원본 화면을 찾을 수 있다.
- 권한·데이터 불변조건: 기존 API·permission·원본 route가 authoritative source다.
- 자동 검증: 권한·부분 실패·empty/error·desktop/390px·overflow·deep link.
- 사용자 검수: synthetic screenshot으로 widget 가치·밀도·순서를 확인한다.

## 10. 사용자 확인

- [x] 업무 문제와 기대 결과가 정확하다.
- [x] 대상 역할과 권한이 정확하다.
- [x] 정상·예외·복구 흐름이 정확하다.
- [x] 포함·제외 범위가 정확하다.
- [x] Blocking 결정이 남아 있지 않다.
- [x] Fable 5가 작성한 이 요약을 planning 입력으로 사용하는 데 동의한다.

확인 근거: 사용자는 이 실험 branch에서 interview 없이 Fable 권장안을 자동 채택하고 기획·review·구현·검증·결과 보고까지 진행하도록 standing directive를 제공했다.
