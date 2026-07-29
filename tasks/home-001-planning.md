# TASK-HOME-001 — PC·모바일 Home MVP 기획안

> 상태: Draft
> 작성 단계: Codex 구현 프롬프트 작성 전
> 목적: 사용자와 기능 방향을 확정하기 위한 기획 문서

- interviewStatus: `COMPLETED_CONFIRMED`
- interviewSource: `tasks/home-001-interview.md`
- interviewUserConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

공통 개발·안전·검증 규칙은 Root/하위 `AGENTS.md`, `docs/12-task-completion-policy.md`와 `docs/development/` 문서를 참조하며 이 문서에 복사하지 않는다.

## 0. 확인된 deep-interview 기준선

- 사용자가 확인한 업무 문제: 로그인 직후 현재 역할에서 확인해야 할 내 업무, 프로젝트 병목, open Pending, 읽지 않은 알림이 화면별로 분산되어 있어 메뉴를 순차 이동하며 우선순위를 파악해야 한다.
- 대상 사용자·역할: 로그인된 active 사용자 전체. 역할·권한별로 노출 widget이 달라진다.
- 정상 흐름: Home 진입 → 허용 widget 병렬 조회 → 핵심 수치·항목 확인 → 원본 화면으로 이동.
- 예외·복구 흐름: widget별 독립 loading·empty·error 상태와 수동 재시도. 한 widget 실패가 다른 widget과 navigation을 차단하지 않는다.
- 확정한 정책과 명시적 제외 (round 1 blocking 결정 1~5, 모두 권장안 채택):
  1. MVP widget 4종 고정 — 내 업무 요약, 프로젝트 병목 Top-N, open Pending 요약(권한 보유 시), 읽지 않은 알림 요약
  2. 신규 `/home` route 추가와 `/` 기본 화면의 Home 전환, 프로젝트 목록의 `/projects` 분리, Teams context의 `/` → Teams Activity 분기 보존
  3. 모바일에서 Home을 첫 번째 핵심 tab으로 추가(최대 5 tab + 더보기)
  4. 신규 Backend endpoint 없이 기존 API 4종 독립 병렬 호출
  5. 권한 없는 widget 완전 숨김, 표시 widget 0개면 안내 문구와 허용 메뉴 링크
  - 명시적 제외: source data 없는 예측 widget, 신규 mutation·persistence·endpoint·migration·provider 발송, 시각 브랜드 전면 개편, canonical 환경 변경
- planning으로 넘긴 비차단 미결정 사항: 병목 Top-N 기본값, widget 기본 표시 순서, 갱신 정책 3건 (16장)

Interview 문서에 없는 사용자 답변을 추측하지 않는다. Interview 완료는 이 planning이나 구현 승인이 아니다.

### 재검증한 Repository 기준선 (현재 실험 branch)

이 planning 작성 전에 현재 branch `experiment/task-home-001-widget-dashboard`에서 다음을 직접 재확인했다.

- routing: `/` 진입 기본 화면은 프로젝트 목록이고 Teams context(`iframe` 또는 Teams referrer)에서는 `/`가 Teams Activity로 분기한다. `/home`과 프로젝트 목록 전용 경로는 아직 없다. 프로젝트 목록 view의 경로 계산은 fallback `/`에 의존한다.
- 모바일 shell: 하단 tab은 기존 `navigationItems`를 고정 label 집합(내 업무·프로젝트·Pending·알림)으로 presentation 분할하며, 나머지는 접근성 계약(focus containment·Esc·복귀)이 검증된 더보기 sheet로 이동한다. TASK-MOBILE-001 실험 구현·자동 검증 완료 상태다.
- shell summary: 내 업무 요청 건수와 읽지 않은 알림 수를 이미 병렬 조회해 배지로 사용한다. 요약 API는 배지 외 필드(진행 중·차단 건수, 담당 프로젝트 수, 알림 차단 건수)도 반환한다.
- 병목 aggregate: 프로젝트 목록·상세 응답에 additive `bottleneck`(구간 label, 단계 rank, 다음 행동, Pending count)이 있고, 목록은 lifecycle → 권한 인지 open Pending → 단계 rank → 납기일 순으로 서버 정렬된다. `pageSize` query를 지원한다. `Pending.Read`가 없으면 Pending count 필드와 Pending 기반 정렬이 모두 제외된다(TASK-007B `007B-PENDING-LEAK` 해소 계약). TASK-007B 실험 구현·자동 검증 완료 상태다.
- Pending: 목록 API가 open·긴급·기한 초과·재검사 집계 summary를 함께 반환하고 `Pending.Read` 권한으로 차단된다. Frontend navigation도 같은 권한으로 Pending 메뉴를 숨긴다.
- 문서와 구현 사이에 새 blocking 충돌은 발견하지 못했다. canonical Roadmap 순서와의 관계는 18장에 기록한다.

## 1. 한 줄 목표

로그인 직후 사용자가 자신의 역할 범위 안에서 내 업무·프로젝트 병목·open Pending·읽지 않은 알림을 Home 한 화면에서 파악하고 각 원본 화면으로 바로 이동할 수 있다.

## 2. 배경과 해결할 업무 문제

- 현재 사용자는 내 업무, 프로젝트 목록, Pending, 알림 화면을 각각 열어 우선순위를 확인한다.
- 여러 업무 영역의 조망이 분산되어 시간 손실과 우선순위 판단 지연이 발생한다.
- 현재 우회 방식은 하단/사이드 메뉴와 각 dashboard의 순차 이동이다. TASK-MOBILE-001 실험으로 이동 자체는 빨라졌지만 조망 분산은 남아 있다.
- 이 기능이 없으면 로그인 직후 "지금 무엇을 먼저 봐야 하는가"를 시스템이 답하지 못하고 사용자의 탐색 습관에 의존한다.

## 3. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 |
| --- | --- | --- | --- |
| 로그인된 active 사용자 전체 | Home widget 조회, 원본 화면 이동, widget별 재시도 | 기존 API의 본인 권한 범위 그대로 | 없음 — Home은 조회 전용 |
| `Pending.Read` 보유 사용자 | 위에 더해 Pending 요약 widget 조회 | 기존 Pending 목록·집계 계약 | 없음 |
| 권한이 제한된 사용자 | 허용 widget만 표시, 0개면 안내와 허용 메뉴 링크 | 기존 permission projection | 없음 |

권한 판정의 authoritative layer는 계속 Backend다. Frontend의 widget 숨김은 기존 `navigationItems` 권한 파생 규칙과 같은 presentation 필터이며, endpoint 자체의 401/403 응답이 최종 기준이다.

## 4. 핵심 사용자 시나리오

### 시나리오 A — PC 로그인 직후 조망

1. 사용자가 로그인하면 `/`에서 Home이 표시된다(Teams context 제외).
2. 시스템이 허용 widget 4종을 독립 병렬 조회해 widget별 loading → 결과를 표시한다.
3. 사용자는 내 업무 요청 건수, 병목 상위 프로젝트, open Pending 집계, 읽지 않은 알림 수를 확인하고 각 widget의 링크로 원본 화면(`/my-work`, `/projects`·프로젝트 상세, `/pending`, `/notifications`)에 이동한다.

### 시나리오 B — 모바일 현장 재진입

1. 사용자가 모바일(≤860px)에서 하단 첫 번째 `홈` tab을 누른다.
2. 시스템이 Home을 카드 세로 배열로 표시하고 44px touch target과 page overflow 0을 유지한다.
3. 사용자는 widget 카드에서 원본 화면으로 이동하고, 하단 tab으로 언제든 Home에 복귀한다.

### 시나리오 C — 부분 실패와 복구

1. 한 widget의 조회가 실패한다.
2. 시스템은 해당 widget에만 오류 상태와 `재시도`·원본 화면 링크를 표시하고 나머지 widget과 navigation은 정상 유지한다.
3. 사용자는 재시도로 해당 widget만 다시 조회하거나 원본 화면으로 우회한다.

### 시나리오 D — 권한 없는 widget과 empty Home

1. `Pending.Read`가 없는 사용자가 Home에 진입한다.
2. 시스템은 Pending widget을 렌더링하지 않고(잠금 카드 없음) 나머지 widget만 표시한다. endpoint가 403을 반환한 widget도 오류가 아니라 숨김으로 처리한다.
3. 표시할 widget이 0개인 사용자에게는 안내 문구와 현재 허용된 메뉴 링크를 표시한다. 현재 권한 구조상 내 업무·알림은 모든 active 사용자에게 제공되므로 이 상태는 방어적 계약이다.

## 5. 기능 요구사항

### 필수

- [ ] `/home` route 신설과 `/` 기본 화면의 Home 전환. Teams context의 `/` → Teams Activity 분기는 무변경 보존
- [ ] 프로젝트 목록의 `/projects` 경로 분리와 기존 프로젝트 상세·편집 deep link 무변경 보존
- [ ] widget 4종: 내 업무 요약(요청·진행 중·차단 건수), 프로젝트 병목 Top-N(서버 정렬 상위 N의 `bottleneck` label·다음 행동 재사용), open Pending 요약(open·긴급·기한 초과·재검사 집계, `Pending.Read` 보유 시), 읽지 않은 알림 요약(unread·차단 건수)
- [ ] widget별 독립 loading·empty·error 상태와 수동 재시도, 부분 실패 격리
- [ ] widget별 원본 화면 deep link (`/my-work`, `/projects`, 프로젝트 상세, `/pending`, `/notifications`)
- [ ] 권한 없는 widget 완전 숨김과 0-widget 안내
- [ ] PC sidebar navigation과 모바일 하단 tab 첫 항목에 `홈` 추가(모바일 최대 5 core tab + 더보기), 44px touch target·390px overflow 0 재검증
- [ ] 기존 권한·audit·API 계약 무변경 (Backend 코드 변경 없음)

### 선택

- [ ] 병목 widget 항목에서 프로젝트 상세로 바로가는 개별 링크(목록 링크에 더해)
- [ ] Home 진입 시 shell 배지 갱신과 조회 결과의 시각적 일관성 확인

### 명시적 제외

- [ ] source data가 없는 예측·추천 widget
- [ ] 신규 Backend endpoint·mutation·persistence·migration·provider 발송
- [ ] widget 사용자 정의(순서 변경·숨김 설정)와 자동 polling
- [ ] 시각 브랜드 전면 개편, 관리자·생산관리·구매 dashboard의 신규 요약 집계
- [ ] canonical repo·`main`·Persistent UAT·5174 runtime 변경

## 6. 화면·UX 기획

| 화면 | 진입 경로 | 표시 정보 | 사용자 행동 | 성공/실패 피드백 |
| --- | --- | --- | --- | --- |
| Home (PC) | `/` 또는 `/home`, sidebar `홈` | widget 4종 카드 grid, 각 widget 제목·핵심 수치·요약 항목·원본 링크 | widget 링크로 원본 이동, 실패 widget 재시도 | widget별 skeleton/loading, empty 문구, error+재시도 |
| Home (모바일 ≤860px) | 하단 첫 tab `홈`, `/` 진입 | 동일 widget의 카드 세로 배열 | 카드 tap 이동, 재시도 | 동일. 44px target, safe-area 준수 |
| 프로젝트 목록 | `/projects`, sidebar·tab `프로젝트` | 기존 목록 화면 그대로 | 기존과 동일 | 기존과 동일 |
| empty Home | 표시 widget 0개 | 안내 문구와 허용 메뉴 링크 | 허용 메뉴로 이동 | 정상 상태로 표시(오류 아님) |

확인할 UX 항목:

- 사용자가 현재 상태를 이해할 수 있는가 — widget별 상태가 서로 섞이지 않고 각 카드 안에서 완결된다.
- 다음 행동이 명확한가 — 모든 widget에 원본 화면 링크가 있고 병목 widget은 `bottleneck.nextActionLabel`을 재사용한다.
- 저장·변경 결과가 action 근처에 보이는가 — N/A. Home은 mutation이 없다.
- 권한 부족·검수 전용·오류 상태가 명확한가 — 권한 부족은 숨김(존재 암시 없음), 오류는 카드 내부 표기, ReviewSafe 배너 등 기존 shell 계약은 그대로 유지된다.
- 좁은 화면에서도 핵심 행동이 가능한가 — 390px·Teams narrow에서 overflow 0, tab 5+더보기 폭과 label 축약을 실측 검증한다.

heading 구조(h1 유지, widget은 h2), keyboard 접근과 링크 계약은 기존 화면 규칙을 따른다.

## 7. 업무 규칙과 불변조건

- Home은 조회 전용이다. 어떤 widget도 mutation을 발생시키지 않고 원본 데이터의 소유권은 기존 화면·API에 남는다.
- 기존 API·permission·원본 route가 authoritative source다. Home은 어떤 수치도 재계산하지 않고 서버 응답을 그대로 표시한다.
- 권한 없는 widget은 count·순서·잠금 카드 어떤 형태로도 존재를 암시하지 않는다(007B Pending 노출 차단 계약 상속).
- 한 widget의 실패·지연이 다른 widget, shell navigation, 원본 화면 이동을 차단하지 않는다.
- Teams context의 `/` → Teams Activity 분기와 기존 알림 deep link 계약은 변경하지 않는다.
- 기존 프로젝트 상세·패널·편집 경로(`/projects/...`)의 의미는 변경하지 않는다. 변경되는 것은 `/`의 기본 화면과 목록 전용 경로 신설뿐이다.
- 병목 Top-N의 순서는 서버 정렬 결과를 그대로 사용하고 client에서 재정렬하지 않는다(007B pagination-sort 계약 상속).

## 8. 데이터와 상태 모델

| 개념 | 설명 | 기존/신규 | 보존·감사 요구 |
| --- | --- | --- | --- |
| 내 업무 요약 | 요청·진행 중·완료·차단 건수와 담당 프로젝트 수 | 기존 (my-work summary API) | 기존 계약 유지 |
| 프로젝트 병목 목록 | 서버 정렬 목록의 상위 N과 additive `bottleneck` | 기존 (TASK-007B aggregate) | 기존 계약 유지 |
| Pending 집계 | open·긴급·기한 초과·재검사 건수 | 기존 (Pending 목록 summary) | 기존 계약·권한 유지 |
| 알림 요약 | 읽지 않은·차단 건수 | 기존 (notification summary API) | 기존 계약 유지 |
| widget slot presentation 상태 | widget별 조회 상태와 노출 여부 | 신규 (Frontend in-memory 전용) | persistence·audit 없음 |

```text
widget별: 숨김(권한 없음) | loading → ready | empty | error(재시도 가능)
```

신규 persistence, 상태 전이, attachment, Excel/PDF, migration은 없다.

## 9. API·Backend 고려사항

- Backend가 authoritative해야 하는 규칙: 권한 projection(특히 `Pending.Read`의 count·정렬 차단), 병목 계산과 서버 정렬, 각 summary 수치.
- 필요한 조회와 mutation: 조회 4종만 사용한다 — 내 업무 summary, 알림 summary, 프로젝트 목록(`pageSize` 상위 N), Pending 목록 집계. mutation 없음.
- 권한·validation: 기존 endpoint 권한 계약을 그대로 사용한다. Backend 코드·정책 변경 없음.
- transaction·동시성·idempotency: N/A — read-only 병렬 조회만 있고 재시도는 동일 GET 재호출이다.
- audit trail: 신규 audit 없음. 기존 조회 audit 정책이 있다면 그대로 적용된다.
- 외부 provider 영향: 없음.

신규 endpoint(`/api/home` 등)는 만들지 않는다. 병렬 호출 4건의 실측 성능 문제가 확인될 때만 후속 Task에서 aggregate endpoint를 별도 검토한다(12장 B안 기각 근거).

## 10. Frontend 고려사항

- route/component:
  - view 종류에 `home`을 추가하고, 경로 해석에서 비-Teams context의 `/`와 `/home`을 Home으로, 프로젝트 목록을 `/projects`로 매핑한다. 경로 생성의 fallback(`/`)이 목록이 아니라 Home을 가리키게 되는 영향을 함께 정리한다.
  - Home 화면 component를 신규 작성하되 기존 `LoadState` 패턴, 카드 스타일과 login 기반 red·white surface를 재사용한다. shell(`App.tsx`)의 `navigationItems` 첫 항목에 `홈`을 추가하고 모바일 core label 집합에 `홈`을 더해 5 tab + 더보기 구성으로 만든다.
- loading/empty/error/success: widget별 독립 `LoadState`와 재시도 버튼. 403은 오류가 아니라 숨김으로 매핑한다. shell 배지 조회(기존 2건)와 Home widget 조회는 목적이 다르므로 분리 유지하되 중복 부하는 진입 시 1회 조회 원칙으로 관리한다.
- 공통 Action Feedback: mutation이 없으므로 재시도 버튼의 진행 중 중복 클릭 차단만 적용한다.
- 접근성: widget 카드 heading 계층, 링크·버튼 role, 배지 건수의 screen reader 텍스트(기존 방식 재사용), 더보기 sheet 계약 무변경.
- 390px/mobile/narrow pane: page-level horizontal overflow 0, 하단 tab 6버튼(5+더보기) 폭·44px bounding-box 실측, safe-area 예약 유지.

## 11. 기존 기능과의 연결

- 프로젝트/업무/알림: Home은 내 업무·프로젝트 병목·알림의 read-only 전면 요약이며 원본 화면 계약을 바꾸지 않는다. 병목 widget은 TASK-007B의 `bottleneck` label·다음 행동·서버 정렬을 그대로 소비한다.
- 권한/관리자: 기존 permission 파생 규칙을 재사용한다. 관리자 메뉴·화면은 변경하지 않는다.
- Excel/PDF/첨부: 영향 없음.
- Teams/Mail: 발송 영향 없음. Teams context routing 분기만 회귀 보존 대상이다.
- 삭제·복구/감사: 영향 없음.
- TASK-MOBILE-001: 하단 tab 구성이 4→5 core tab으로 바뀌므로 MOBILE-001의 touch target·overflow E2E 방식을 재사용해 재검증한다.

## 12. 후보 구현안과 대안

| 후보 | 설명 | 장점 | 단점·위험 |
| --- | --- | --- | --- |
| A (권장·interview 확정) | Frontend 조합 Home: 기존 API 4종 독립 병렬 호출, `/` 기본 화면 전환 + `/projects` 분리, 모바일 첫 tab | Backend 무변경, 007B 권한·정렬 계약 상속, widget 독립 실패가 구조적으로 충족, 로그인 직후 조망이라는 문제 정의를 직접 해결 | Home 진입 시 병렬 호출 4건, `/` bookmark 의미 변화로 회귀 검증 필요 |
| B (기각) | 신규 `GET /api/home` aggregate endpoint 통합 조회 | 호출 1건, payload 최적화 | 신규 Backend surface에 권한 projection·부분 실패 의미 중복 구현, 단일 응답 실패가 전체 widget을 차단하는 구조적 위험 |
| C (기각) | `/` 유지 + 메뉴로만 Home 진입(또는 로그인 직후 1회 redirect) | URL 계약 무변경으로 위험 최소 | 로그인 직후 조망이 수동 이동에 의존해 기대 결과가 약해지고, redirect 절충안은 진입 동선이 이원화됨 |

권장안 A는 interview blocking 결정 1~5로 이미 사용자 확정되었다.

## 13. Task 고유 안전 경계

- Persistent UAT 영향: 없음. 이 Task는 실험 branch `experiment/task-home-001-widget-dashboard` 전용이며 Persistent UAT·5081·5174·대표 repo·GitHub `main`을 변경하지 않는다.
- migration 필요 여부: 없음. schema·migration ledger 무변경.
- 외부 발송/실제 데이터 영향: 없음. E2E는 격리 synthetic DB만 사용한다.
- runtime 교체 여부: 없음.
- 추가 사용자 승인 필요 작업: 실험 결과의 canonical repo 채택·commit·push·PR·merge는 이 planning 범위 밖이며 별도 승인이 필요하다. rollback은 실험 branch를 대표 repo에 적용하지 않는 것으로 충분하다.

## 14. 검증 계획

- 최소 테스트: Frontend typecheck·lint·unit·build. 신규 unit test — `/`·`/home`·`/projects` 경로 해석과 생성, 권한별 widget 노출·숨김, widget 부분 실패 격리(개별 조회 mock 실패), 0-widget 안내.
- 영향 영역 회귀:
  - 신규 isolated full-stack E2E 1본: `/` → Home 렌더링, widget 수치와 원본 화면 정합, 역할 전환에 따른 Pending widget 숨김, widget deep link, `/projects` 목록, 기존 프로젝트 상세 deep link 보존, 390px overflow 0과 하단 tab(5+더보기) 44px bounding-box, synthetic screenshot(Desktop·390px, 권한 있는/없는 역할).
  - 기존 실험 E2E 회귀: Pending(007A), project-bottleneck(007B), mobile-adaptive-navigation(MOBILE-001) spec 재실행.
  - Teams context `/` 분기는 unit 수준(경로 해석)과 기존 Teams Activity 화면 smoke로 보존을 확인한다.
- PR/CI: 실험 branch local commit까지가 기본 범위다. 게시·PR·merge는 별도 사용자 승인 대상.
- 사용자 검수: synthetic screenshot으로 widget 가치·밀도·순서를 확인하고, checklist는 자동 검증과 사용자 직접 확인을 분리해 `docs/12-task-completion-policy.md` 상태로 추적한다.

## 15. 완료 기준

- 기능/권한/데이터: widget 4종이 역할 권한에 맞게 노출·숨김되고, 모든 수치가 원본 화면과 일치하며, mutation·신규 persistence가 없다.
- UX: `/` 진입 조망, widget별 독립 상태·재시도, 원본 deep link, 390px·Teams narrow overflow 0, 44px touch target, keyboard/heading 계약 충족.
- 자동 테스트: Frontend unit 전체 PASS, 신규 Home E2E PASS, 007A·007B·MOBILE-001 회귀 E2E PASS, Backend 전체 test는 Backend 무변경 확인 후 기존 통과 상태 유지.
- 5종 산출물: implementation report·SOP·user manual·Roadmap update(실험 전용이면 N/A 근거 기록)·user validation checklist의 상태와 위치 추적.
- 사용자 검수 상태: `자동 검증 완료` 후 `사용자 검수 대기`로 handoff. 검수 완료는 사용자 기록으로만 전환한다.
- PR 상태: N/A — 실험 branch 범위. 게시가 요청되면 별도 승인·gate를 따른다.

중단 조건: 구현 중 Backend 변경이 필요해지거나, 권한 없는 사용자에게 widget 존재가 노출되는 결함이 재현되거나, `/` 전환이 Teams context·기존 deep link 계약과 해소 불가능하게 충돌하면 구현을 중단하고 충돌 내용을 보고한다.

## 16. 미결정 사항

Interview에서 명시적으로 deferred된 비차단 결정 3건이다. 권장안 채택 여부를 사용자 결정으로 기록한 뒤 구현한다.

| 번호 | 질문 | 선택지 | 사용자 결정 |
| ---: | --- | --- | --- |
| 1 | 병목 widget Top-N 기본값 | 서버 정렬 상위 5 (권장) / 3 / 10 | 대기 |
| 2 | widget 기본 표시 순서 | 내 업무 → 프로젝트 병목 → Pending → 알림 (권장) / 다른 고정 순서 | 대기 |
| 3 | 갱신 정책 | 진입 시 조회 + widget별 수동 재시도, 자동 polling 없음 (권장) / 주기적 자동 갱신 | 대기 |

## 17. 예상 변경 범위

이 목록은 확정 allowlist가 아니라 조사 대상이다.

- Backend: 없음 (변경 금지 경계).
- Frontend: `frontend/src/App.tsx`(view·경로 해석/생성·navigation·모바일 core label), 신규 Home 화면 component(파일 분리 여부는 구현 시 결정), `frontend/src/styles.css`(Home 카드·모바일 tab 폭), `frontend/src/api.ts`·`frontend/src/projects.ts`는 기존 함수·type 재사용으로 무변경이 목표.
- DB/Migration: 없음.
- Tests/Scripts: `frontend/tests/App.test.tsx` 확장 또는 신규 test, 신규 `frontend/e2e/full-stack/home-*.full-stack.spec.ts`, 기존 E2E 회귀 실행.
- Docs: `tasks/home-001-*` interview·planning·review·change·implementation report·screenshots. canonical `docs/00-product-roadmap.md`는 실험 범위에서 수정하지 않는다.

## 18. Roadmap 연결

- 선행 Task: TASK-007B 병목 aggregate와 TASK-MOBILE-001 하단 tab shell. 두 실험 모두 이 branch 계열에 구현·자동 검증 완료(사용자 검수 대기) 상태이며 Home은 그 결과물을 소비만 한다.
- 후속 Task: 기능 Task 완료에 따른 widget 단계적 활성화(예: 자재·제조·물류 요약), 실측 성능 근거가 생길 때의 aggregate endpoint 검토, DESIGN-001 계열의 Home 화면 통일.
- 현재 Go/No-Go: canonical Roadmap의 실행 큐에서 TASK-HOME-001은 `Dependency Pending`(1.4)이고 canonical Next Gate는 TASK-007A다. 이 planning은 사용자 standing experiment directive에 따라 실험 branch 범위에서만 진행하며, canonical 순서·Roadmap·Decision Log를 변경하지 않는다. 실험 결과의 canonical 채택은 별도 사용자 승인 대상이다.
- 별도 Task로 분리할 항목: 예측·추천 widget, widget 사용자 설정, `/api/home` aggregate, dashboard별 신규 집계.

## 19. 사용자 검토 기록

| 일자 | 요청/결정 | 반영 내용 |
| --- | --- | --- |
| (대기) | 16장 결정 3건과 이 planning 승인 | 대기 |

## 20. 최종 승인 상태

- [ ] 기능 목표와 업무 문제 승인
- [ ] 포함·제외 범위 승인
- [ ] 시나리오와 권한·업무 규칙 승인
- [ ] UI/UX 방향 승인
- [ ] Task 고유 안전 경계 승인
- [ ] 검증·사용자 체크리스트 승인
- [ ] Codex 구현 프롬프트 작성 승인

## 21. Codex 구현 지시문 초안

사용자 승인과 16장 결정 3건 기록 후, 새 Codex 구현 세션이 다음 순서로 실행한다.

1. 실험 branch에서 instruction chain gate를 수행하고 Backend 무변경 경계를 확인한다.
2. 경로 계약을 먼저 구현한다: 비-Teams `/`·`/home` → Home, 프로젝트 목록 → `/projects`, 경로 생성 fallback 정리, Teams context 분기와 기존 `/projects/...` deep link 무변경을 unit test로 고정한다.
3. Home 화면을 구현한다: widget 4종을 기존 API 함수(내 업무 summary, 알림 summary, `pageSize` 상위 N 프로젝트 목록의 `bottleneck`, Pending 목록 summary)로 독립 병렬 조회하고, widget별 `LoadState`·재시도·403 숨김·0-widget 안내를 적용한다. mutation·신규 endpoint를 추가하지 않는다.
4. navigation을 갱신한다: sidebar와 모바일 core tab 첫 항목에 `홈`을 추가하고 5 core tab + 더보기 구성에서 44px·390px overflow를 실측한다.
5. 검증한다: Frontend typecheck·lint·unit·build, 신규 Home full-stack E2E, 007A·007B·MOBILE-001 회귀 E2E, Desktop·390px synthetic screenshot을 수행하고 privacy-safe 형식으로 기록한다.
6. implementation report와 5종 산출물 상태를 작성하고 `자동 검증 완료 / 사용자 검수 대기`로 handoff한다. canonical repo·Roadmap·Persistent UAT·provider는 건드리지 않는다.

---

- planningStatus: DRAFT
- implementationApproved: false
- userDecisionRequiredCount: 3
