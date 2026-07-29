# TASK-HOME-001 — PC·모바일 Home 위젯 대시보드 실험 구현 보고서

> 상태: 실험 구현·자동 검증·독립 read-only 재검토 완료 / 사용자 검수 대기
> 기준 branch: `experiment/task-home-001-widget-dashboard`
> 실험 branch base SHA: `f8d719302f59eae9a4b9292dff5928558d0fda67`
> canonical `main` SHA: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
> canonical 반영: 미승인 — 대표 repo, GitHub `main`, Persistent UAT, provider와 canonical runtime을 변경하지 않음

## 1. 목적과 범위

로그인 직후 현재 역할에서 확인할 내 업무, 프로젝트 병목, Pending과 알림을 한 화면에서 파악하고 원본 업무 화면으로 이동할 수 있는 read-only Home을 구현했다.

### 포함

- 비-Teams `/`·`/home` Home과 프로젝트 목록 `/projects` 분리
- 내 업무·프로젝트 병목 Top 5·Pending·알림 widget
- widget별 독립 loading·empty·error·재시도와 원본 화면 이동
- `Pending.Read` 기반 widget·요청·프로젝트 action redaction
- Desktop sidebar와 모바일 첫 `홈` tab, 390px overflow·44px touch target
- login 화면의 red·white surface를 잇는 PC·mobile responsive design
- 기존 프로젝트 목록 bookmark consumer의 `/projects` 회귀 수정

### 제외

- Backend endpoint·domain·DB·migration·provider 변경
- `/api/home` aggregate, 자동 polling, 예측·추천, 사용자 widget 설정
- 대표 repo·GitHub `main`·Persistent UAT·5081/5174 runtime handover
- push·PR·merge

## 2. 기획·Review 결정

- Fable 5 read-only runner가 2회 interview 원문과 planning 전문을 작성했다.
- standing experiment 지침에 따라 권장안 `1-A · 2-A · 3-A · 4-A · 5-A`와 Top 5, `내 업무→프로젝트 병목→Pending→알림`, 진입 조회+수동 재시도·polling 없음 정책을 자동 채택했다.
- 신규 aggregate 없이 기존 API를 widget별로 독립 호출하고 서버의 007B 병목 순서를 그대로 표시한다.
- 알려진 `Pending.Read` 미보유는 endpoint 호출 전 숨긴다. 실제 호출된 endpoint의 403은 설정·권한 drift를 숨기지 않도록 해당 widget의 권한 오류와 재시도로 표시한다.
- Home은 shell h1 아래 h2, widget은 h3로 구성했다.
- 요청한 `GPT 5.6 Sol` selector는 환경에 없어 해당 모델을 사용했다고 주장하지 않는다. Codex 내용 review와 별도 read-only sub-agent review를 수행했다.

## 3. 구현 구조

### Routing·shell

- Teams context의 `/`는 기존 Teams Activity 분기를 먼저 적용한다.
- 일반 `/`와 `/home`은 Home, `/projects`는 기존 프로젝트 목록으로 해석한다.
- `pathForView`는 Home `/`, 프로젝트 목록 `/projects`를 명시적으로 생성한다.
- 사용자 전환 시 Home에서는 Home을 유지하고, 그 외 기존 업무 화면에서는 안전한 프로젝트 목록 reset을 유지한다.
- Desktop·mobile navigation의 첫 항목은 `홈`이며 mobile core는 홈·내 업무·프로젝트·권한 보유 Pending·알림 최대 5개다.

### Widget 상태·권한

- 각 widget이 별도 callback·state·retry를 소유해 한 API 실패가 다른 widget을 막지 않는다.
- effective user ID를 request context로 사용하고 widget별 generation counter로 사용자 전환 전 지연 응답을 폐기한다.
- Pending 권한이 없으면 `/api/pending`을 호출하지 않고 widget을 렌더링하지 않는다.
- Backend 007B permission projection을 상속하며, Frontend도 권한이 없을 때 Pending action text를 방어적으로 redaction한다.
- 프로젝트 병목은 `pageSize=5` 요청 결과를 client 재정렬 없이 표시한다.

### Responsive design

- Home hero·widget·metric·프로젝트 card를 login 기반 red·white, soft-red wash, rounded surface로 구성했다.
- Desktop은 2열+병목 wide card, 860px 이하는 1열, 430px 이하는 widget action을 full-width로 바꾼다.
- 390px에서 page horizontal overflow 0과 하단 navigation 44×44px 이상을 실측했다.

## 4. 실제 변경 파일

| 경로 | 역할 |
| --- | --- |
| `frontend/src/HomePage.tsx` | Home 4 widget, 독립 상태·재시도·generation guard·permission redaction |
| `frontend/src/App.tsx` | Home view, route, sidebar·mobile navigation, 사용자 전환 context |
| `frontend/src/styles.css` | login 기반 Home Desktop·mobile design |
| `frontend/tests/App.test.tsx` | route·popstate·권한·403·부분 실패·retry·stale response unit test |
| `frontend/e2e/full-stack/home-dashboard.full-stack.spec.ts` | Home deep link·mobile·permission projection·screenshot E2E |
| `frontend/e2e/full-stack/mobile-adaptive-navigation.full-stack.spec.ts` | 프로젝트 목록 `/projects`와 artifact opt-in 회귀 |
| `frontend/e2e/full-stack/project-bottleneck.full-stack.spec.ts` | 프로젝트 목록 `/projects` 회귀 |
| `frontend/e2e/full-stack/project-registration.full-stack.spec.ts` | 기존 프로젝트 목록 진입점 `/projects` 이전 |
| `frontend/e2e/mock-ui/project-registration-smoke.spec.ts` | mock 목록 진입점 `/projects` 이전 |
| `tasks/home-001-interview*.md` | Fable 질문·확인 원문과 권장안 채택 기록 |
| `tasks/home-001-planning.md` | Fable 5 primary planning 원문 |
| `tasks/home-001-review.md` | Codex 내용 review와 독립 review Finding resolution |
| `tasks/home-001-change-001.md` | 실험 구현·local commit과 canonical 차단 경계 |
| `tasks/home-001-screenshots/*.png` | synthetic Home 화면 3장 |

## 5. 실행한 검증과 결과

| 검증 | 결과 | 비고 |
| --- | --- | --- |
| Frontend typecheck | PASS | TypeScript 오류 0 |
| Frontend lint | PASS | 오류 0, 기존 `main.tsx` Fast Refresh 경고 1 |
| Frontend 전체 unit test | PASS | 73/73 |
| Frontend build | PASS | build 성공, 기존 500 kB chunk 경고 유지 |
| TASK-HOME-001 full-stack E2E | PASS | 1/1, Desktop·390px·권한 제한 presentation·deep link·screenshot |
| TASK-MOBILE-001 회귀 | PASS | 1/1, route·touch target·overflow |
| TASK-007B 회귀 | PASS | 1/1, 서버 정렬·Pending drill-down·상세 |
| TASK-007A 회귀 | PASS | 2/2, lifecycle·390px |
| 프로젝트 등록 전체 full-stack 회귀 | PASS | 16/16 |
| mock UI smoke | PASS | 1/1 |
| `git diff --check` | PASS | whitespace 오류 없음 |
| 격리 resource cleanup | PASS | 각 run의 database·container·network 제거 확인 |
| Backend 전체 test | 미실행 | production Backend 변경 없음; 007B 기존 permission projection 계약 재사용 |
| Persistent UAT·provider | 미실행 | 실험 범위 밖이며 canonical 환경 보존 |
| 사용자 직접 검수 | 대기 | 아래 screenshot과 checklist로 판정 예정 |

## 6. Privacy·Secret 검토

- screenshot과 E2E는 고정 개발 역할과 synthetic 프로젝트·Pending만 사용했다.
- 실제 사용자·회사·고객·tenant/client/object ID, credential, token과 provider payload를 tracked 산출물에 기록하지 않았다.
- `.env*`, 인증서, dependency와 Persistent UAT data를 변경하지 않았다.

## 7. Finding

| ID | Severity | 상태 | 원인·영향 | 해소·후속 위치 |
| --- | --- | --- | --- | --- |
| `HOME-ROUTE-REGRESSION` | P2 | `RESOLVED` | `/` 의미 전환이 기존 목록 bookmark를 깨뜨릴 위험 | `/projects` 명시, unit+16 project E2E+mock+007B+MOBILE 회귀 |
| `HOME-PARTIAL-FAILURE` | P2 | `RESOLVED` | 단일 결합 요청은 한 실패로 Home 전체를 막음 | widget별 state·retry와 503→retry unit |
| `HOME-IDENTITY-STALE-RESPONSE` | P2 | `RESOLVED` | 이전 actor 응답이 새 Home을 덮을 위험 | effective user context·generation guard·deferred unit |
| `HOME-FORBIDDEN-SEMANTICS` | P2 | `RESOLVED` | 호출된 403을 숨기면 권한 drift를 정상 상태로 오인 | 사전 Pending filter와 attempted 403 error를 분리 |
| `HOME-PENDING-PROJECTION` | P2 | `RESOLVED` | count·action·정렬을 통한 Pending 간접 노출 위험 | 007B Backend permission projection 상속, Home 요청 생략·action redaction unit/E2E |
| `HOME-HEADING-HIERARCHY` | P2 | `RESOLVED` | Home/widget heading 중복 위험 | shell h1→Home h2→widget h3 |
| `HOME-SUMMARY-DUPLICATION` | P3 | `BACKLOG` | shell과 Home이 my-work·notification summary를 각각 조회 | polling 없음; 실측 문제 시 공통 query/cache 후속 |
| `HOME-PENDING-PAYLOAD` | P3 | `BACKLOG` | Pending summary에 목록 payload 전체를 사용 | 실측 비용이 확인되면 summary-only endpoint 후속 |
| `HOME-REVIEWER-AVAILABILITY` | P3 | `BACKLOG` | 요청한 reviewer model selector가 환경에 없음 | 독립 Codex read-only review 완료; canonical 채택 전 요구 모델 제공 시 재검토 |

Open P0/P1/P2는 `0/0/0`이다. P3 backlog는 실험 local commit을 막지 않으며 canonical 채택 전 재평가한다.

## 8. Rollback·복구

- 대표 repo와 `main`은 보존됐으므로 실험을 채택하지 않으면 이 branch commit만 보관하거나 승인 범위에서 worktree를 정리한다.
- Backend·migration·Persistent data·provider mutation이 없어 운영 data rollback은 없다.
- route 회귀 시 이 실험 commit을 대표 repo에 적용하지 않는 것이 기본 rollback이다.

## 9. SOP

1. branch가 `experiment/task-home-001-widget-dashboard`인지 확인한다.
2. Frontend typecheck·lint·unit·build를 실행한다.
3. `bash scripts/e2e-full-stack.sh e2e/full-stack/home-dashboard.full-stack.spec.ts`를 실행한다.
4. 007A·007B·MOBILE·project registration 회귀를 실행한다.
5. 각 E2E 종료 시 전용 DB·container·network 제거를 확인한다.
6. Desktop `/`, mobile `/`, `/home`, `/projects`와 원본 deep link를 synthetic data로 검수한다.
7. 대표 repo·`main`·Persistent UAT·provider·canonical runtime에는 적용하지 않는다.

## 10. User manual

1. 로그인 후 `홈`에서 오늘 확인할 업무를 본다.
2. `내 업무`의 시작 전·진행 중·차단·담당 프로젝트 수를 확인한다.
3. `프로젝트 병목`의 서버 우선순위 Top 5에서 프로젝트를 눌러 상세로 이동한다.
4. 권한이 있으면 `Pending`의 open·긴급·기한 초과·재검사 수를 확인한다.
5. `알림`에서 읽지 않음·긴급/차단 수를 확인한다.
6. 각 card의 `전체 보기`, `원본 화면 열기`, 프로젝트 항목으로 기존 업무 화면을 연다.
7. 오류 card는 `다시 시도`로 해당 widget만 갱신한다.

## 11. User validation checklist

상태: `자동 검증 완료` / `사용자 검수 대기`

### 자동 확인

- [x] `/`·`/home` Home과 `/projects` 목록·popstate
- [x] 4개 widget과 원본 deep link
- [x] widget 부분 실패·재시도와 이전 actor 지연 응답 폐기
- [x] Pending 권한 제한 시 widget·API request·프로젝트 action 숨김
- [x] Desktop full-page와 390px full-page, overflow 0·mobile nav touch target
- [x] Frontend 73/73·Home 1/1·MOBILE 1/1·007B 1/1·007A 2/2·project 16/16·mock 1/1

### 사용자 직접 확인

- [ ] Home widget 순서와 Top 5가 실제 업무 우선순위에 맞다.
- [ ] login 화면과 이어지는 red·white design이 자연스럽다.
- [ ] Desktop card 밀도와 Mobile 세로 길이가 적절하다.
- [ ] 권한 없는 Pending이 완전히 숨겨진 상태가 기대와 맞다.
- [ ] 이 실험을 계속 수정할지 대표 repo 채택 후보로 둘지 결정한다.

## 12. 화면 증빙

- [Desktop Home · 1440px full page](home-001-screenshots/01-home-desktop-1440.png)
- [Mobile Home · 390px full page](home-001-screenshots/02-home-mobile-390.png)
- [Pending 권한 제한 Home · 390px full page](home-001-screenshots/03-home-without-pending-permission-390.png)

## 13. 5종 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | `tasks/home-001-implementation-report.md` |
| SOP | 작성됨 | 이 문서 `9. SOP` |
| User manual | 작성됨 | 이 문서 `10. User manual` |
| Roadmap update | N/A — 실험 전용 | canonical Roadmap과 대표 repo 보존 지침에 따라 미수정 |
| User validation checklist | 자동 검증 완료·사용자 검수 대기 | 이 문서 `11. User validation checklist` |

## 14. 시행착오와 해소

- 첫 Home E2E는 권한 제한 actor로 `dev-sales`를 사용했지만 현재 canonical 10개 내부 역할은 모두 `Pending.Read`를 가져 시나리오가 성립하지 않았다. presentation 검증은 `/api/me` permission projection을 제한하고, Backend count·정렬 보장은 기존 007B 계약을 재사용하는 것으로 분리했다.
- 격리 DB의 role permission을 제거해 실제 actor를 만들려는 시도는 Development auth가 `InMemoryIdentityStore`를 사용해 claims에 반영되지 않음을 확인하고 즉시 제거했다. production test hook이나 synthetic backend role은 Frontend-only 범위를 넓히므로 추가하지 않았다.
- mobile full-page screenshot은 fixed bottom navigation이 viewport 위치에 한 번 보이는 Playwright 특성이 있으나 실제 390px viewport에서는 하단 고정과 overflow 0을 별도로 검증했다.
- 기존 MOBILE screenshot을 회귀 실행이 덮어쓰지 않도록 해당 spec의 artifact 생성은 `MOBILE_SCREENSHOT_DIR`가 있을 때만 수행하도록 제한했다.
- 모든 full-stack run은 성공·실패와 무관하게 전용 database·container·network를 정리했다.

## 15. 사용자 검수 결과와 Roadmap

자동 검증과 synthetic 화면 캡처는 완료했으나 사용자 직접 검수는 대기다. 대표 repo와 canonical Roadmap은 변경하지 않았으므로 공식 다음 Gate는 계속 `TASK-007A Fable deep-interview → planning → Codex review → 사용자 승인`이다. 이 실험 결과와 local commit은 canonical 승인이나 merge를 대신하지 않는다.
