# TASK-HOME-001 — PC·모바일 Home MVP Codex 내용 Review

- reviewOwner: `CODEX`
- reviewSource: `tasks/home-001-planning.md`
- reviewStatus: `RESOLVED_AFTER_INDEPENDENT_REREVIEW`
- experimentalImplementationApprovalSource: `USER_STANDING_EXPERIMENT_DIRECTIVE`
- canonicalMainApproval: false
- mainMergeApprovalCount: 0/3

## 결론

Fable의 권장안은 현재 실험 branch의 TASK-007B 병목 aggregate와 TASK-MOBILE-001 navigation을 재사용해 “지금 무엇을 먼저 봐야 하는가”를 한 화면에서 답한다. 신규 Backend aggregate를 만들지 않고 widget별 기존 API를 독립 호출하므로 권한과 부분 실패 경계도 명확하다. 독립 read-only 재검토에서 기존 P2가 모두 해소됐고 새 P0~P3가 없어 실험 구현은 `GO`다.

## 유지

- 현재 source data가 있는 내 업무·프로젝트 병목·Pending·알림 4종만 활성화한다.
- `/`를 Home으로 전환하고 프로젝트 목록은 `/projects`로 분리하며 Teams context의 `/` 분기는 보존한다.
- Home은 read-only presentation이며 기존 API·permission·원본 화면을 authoritative source로 유지한다.
- 권한 없는 widget은 잠금 카드나 count 암시 없이 완전히 숨긴다.
- widget별 loading·empty·error·재시도를 독립 상태로 구현한다.

## 추가

- 경로 변경은 unit test뿐 아니라 프로젝트 등록·병목·mobile E2E의 기존 `/` 목록 진입을 `/projects`로 갱신해 실제 bookmark 계약을 고정한다.
- 사전에 권한이 없음을 아는 Pending widget만 endpoint 호출 없이 숨긴다. 실제로 호출한 endpoint의 403은 숨기지 않고 해당 widget의 권한 오류·재시도로 표시한다.
- 병목 Top 5는 서버 응답 순서를 그대로 표시하고 client 재정렬을 금지한다.
- Home에서 프로젝트 개별 상세와 각 원본 workspace로 이동하는 버튼을 모두 제공한다.
- 모바일 5 core tab+더보기의 각 button을 390px에서 실제 44×44px 이상으로 측정한다.

## 보류

- shell 배지와 Home이 내 업무·알림 summary를 각각 조회하는 중복은 MVP 실측 성능 문제가 확인될 때 공통 query/cache 후속으로 다룬다.
- Pending summary-only endpoint와 `/api/home` aggregate는 현재 목록 응답의 실측 비용이 문제로 확인될 때만 별도 Task로 검토한다.
- widget 사용자 설정·예측·추천·자동 polling·dashboard별 신규 집계는 제외한다.

## 제거

- 권한 없는 업무의 잠금 widget.
- Home에서 원본 수치를 재계산하거나 별도 persistence·mutation·audit를 만드는 설계.
- `/`와 `/home`의 역할을 다르게 만드는 1회성 redirect.

## 구현 순서

1. `home` view와 `/`·`/home`·`/projects` 경로 계약 및 기존 E2E bookmark 수정
2. 별도 `HomePage` component의 4개 독립 widget 상태·재시도·권한 숨김 구현
3. sidebar·모바일 첫 `홈` navigation과 responsive style
4. unit·full-stack E2E·기존 007A/007B/MOBILE 회귀
5. Desktop·390px·권한 미보유 screenshot과 implementation report

## Finding과 resolution

| ID | Severity | 상태 | 내용 | Resolution |
| --- | --- | --- | --- | --- |
| `HOME-ROUTE-REGRESSION` | P2 | `RESOLVED` | `/` 의미 변경이 기존 프로젝트 목록 smoke·bookmark를 깨뜨릴 수 있음 | `/projects` 명시 route, popstate unit, 기존 project registration 16/16·mock smoke·007B·MOBILE route consumer 회귀 검증 |
| `HOME-PARTIAL-FAILURE` | P2 | `RESOLVED` | 단일 Promise 조합은 한 API 실패로 Home 전체를 막음 | widget별 state·loader·retry 분리와 한 widget 503→재시도 회복 unit test |
| `HOME-PERMISSION-LEAK` | P2 | `RESOLVED` | 권한 미보유 actor에게 Pending widget·호출·프로젝트 카드 action으로 존재를 암시할 위험 | `Pending.Read` 사전 필터, `/api/pending` 미호출, project action 방어적 redaction을 unit·isolated browser E2E로 검증 |
| `HOME-CLIENT-SORT` | P2 | `RESOLVED` | Home에서 병목을 다시 정렬하면 007B 서버 pagination 계약이 깨짐 | `pageSize=5` 서버 순서를 그대로 사용하고 client sort 없음 |
| `HOME-IDENTITY-STALE-RESPONSE` | P2 | `RESOLVED` | 개발 사용자·Entra 검수 사용자 전환 직전의 느린 응답이 새 actor의 widget을 덮을 위험 | effective user request context와 widget별 generation guard, 지연 응답 unit test 추가 |
| `HOME-FORBIDDEN-SEMANTICS` | P2 | `RESOLVED` | 호출된 endpoint의 403까지 숨기면 권한 drift·설정 오류를 정상 hidden 상태로 오인 | 사전 권한 필터는 Pending에만 적용하고 시도된 403은 widget 내부 권한 오류로 유지 |
| `HOME-ROOT-ROUTE-CONSUMERS` | P2 | `RESOLVED` | 기존 project-list `/` consumer가 Home 전환 후 엉뚱한 화면에서 동작할 위험 | unit의 `/`·`/home`·`/projects`·popstate와 기존 project E2E 모든 목록 진입을 `/projects`로 고정 |
| `HOME-PENDING-PROJECTION` | P2 | `RESOLVED` | Pending widget을 숨겨도 병목 카드의 count/action/sort text가 남으면 간접 노출 | Backend 007B permission projection 상속 + Frontend action redaction 방어 + 권한 제한 E2E |
| `HOME-HEADING-HIERARCHY` | P2 | `RESOLVED` | shell h1 아래 Home/widget heading level이 중복되면 접근성 구조가 흐려짐 | shell h1 유지, Home h2, widget h3로 구현·role test |
| `HOME-SUMMARY-DUPLICATION` | P3 | `BACKLOG` | shell badge와 Home summary의 초기 중복 GET 가능 | 자동 polling 금지, isolated E2E에서 호출 수·응답 확인; 실측 문제 시 query/cache 후속 |
| `HOME-PENDING-PAYLOAD` | P3 | `BACKLOG` | Pending summary 확인을 위해 목록 payload 전체를 조회 | 실측 비용이 확인되면 summary-only endpoint를 별도 Task로 검토 |
| `HOME-REVIEWER-AVAILABILITY` | P3 | `BACKLOG` | 요청한 GPT 5.6 Sol selector가 환경에 없음 | Codex 내용 review와 별도 read-only sub-agent review, canonical 채택 전 요구 모델 제공 시 재검토 |

## 구현 판정

현재 experiment branch 구현과 독립 read-only 재검토는 `GO`다. Fable deferred 권장안은 Top 5, `내 업무→프로젝트 병목→Pending→알림`, 진입 시 조회+widget별 수동 재시도·자동 polling 없음으로 채택한다. 이 판정은 push·PR·대표 repo 또는 main merge 승인이 아니다.
