# TASK-UL891-SET-001 Change 008 구현 보고 — 저장 오류·불필요 규격 제거

상태: `자동 검증 완료 / 사용자 재검수 대기`

## 해결한 문제

1. 별도 설계 입력 화면의 `저장`은 화면의 현재 값을 먼저 Draft에 반영하지 않고 서버에 남아 있던 Draft를 바로 Publish했다. 사용자가 `임시저장`을 먼저 누르지 않으면 입력값이 비어 있는 것으로 판정될 수 있었다.
2. Backend Publish 완료 조건과 패널의 `설계 입력 완료` 계산이 사용자 입력값이 아닌 `규격`을 필수로 요구했다.
3. 조회·수정·패널 문맥에 불필요한 `규격`이 새 입력값처럼 노출됐다.

## 구현 결과

1. `저장` 한 번으로 현재 화면의 패널명·치수를 Draft에 갱신한 뒤 같은 version을 Publish한다. 별도의 `임시저장` 선행 동작은 필요하지 않다.
2. Backend Publish는 구성 패널명과 포장방식별 치수만 검증한다. 목포장은 W·H·D가 모두 필요하고, 그 외 포장방식은 기존 치수 선택 규칙을 유지한다.
3. Publish 또는 version 적용 뒤 패널의 `설계 입력 완료`도 같은 기준으로 계산한다. 규격이 없어도 유효한 이름·치수면 제조 준비 조건을 만족한다.
4. UL891 주문 안내, 프로젝트 상세 조회 표, 별도 수정 화면, 패널 상세의 세트 문맥에서 `규격` 표시·입력칸을 제거했다.
5. 기존 API·DB의 `panelSpecification` 필드는 과거 데이터 호환을 위해 유지한다. 기존 값을 삭제하는 migration이나 일괄 data mutation은 실행하지 않았다.

## 사용자 사용 방법

1. 프로젝트 상세의 `설계` 탭에서 `수정`을 누른다.
2. 임시로 계속 작업하려면 `임시저장`을 누른다.
3. 최종 반영하려면 현재 값 입력 후 바로 `저장`을 누른다. 현재 값 저장과 최종 반영이 한 번에 끝난다.
4. 구성 패널마다 패널명과 포장방식에 필요한 치수를 입력한다. 별도 규격은 입력하지 않는다.

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `UL891-008-F01` | P1 | Resolved | 최종 저장이 현재 form을 반영하지 않아 사용자가 올바르게 입력해도 서버의 빈 Draft가 검증됐다. | `UpdateDraft → Publish`를 한 action으로 직렬 실행한다. |
| `UL891-008-F02` | P1 | Resolved | Publish와 `panel_info_completed`가 불필요한 규격을 요구해 저장 또는 제조 인계를 막았다. | 두 계산을 패널명·포장방식별 치수 기준으로 통일했다. |
| `UL891-008-F03` | P2 | Resolved | 화면에 규격이 신규 입력값처럼 노출됐다. | UL891 사용자 화면에서 규격을 제거하고 호환 필드만 내부에 보존했다. |

Open P0/P1/P2: `0/0/0`.

## 검증

| 검증 | 결과 |
| --- | --- |
| Backend UL891 실제 API 통합 회귀 | PASS — 규격 `null`, 패널명·치수 유효 상태에서 Draft 갱신·Publish·패널 설계 완료 `1/1` |
| Backend 전체 회귀 | PASS — `424/424` |
| Frontend UL891 집중 unit | PASS — `4/4`, 저장 요청 순서 `PUT → POST`, 규격 미노출 |
| Frontend 전체 unit | PASS — 22 files, `138/138` |
| Frontend typecheck | PASS |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend production build | PASS — 기존 500kB 초과 chunk warning 유지 |
| 실제 desktop 조회 UI | PASS — 3개 세트 표 모두 `Code · 패널명 · W×H×D`, 규격 0 |
| 실제 desktop 수정 UI | PASS — Draft 1개에 패널명 3개·치수 입력·`임시저장`·`저장`, 규격 0 |
| Browser console | PASS — warning/error 0 |

## 안전·게시 경계

- 실제 검수 데이터의 저장 버튼은 누르지 않았다. 영속 데이터 변경은 격리 API 통합 테스트에서만 수행하고 자동 폐기했다.
- migration·실제 provider·대표 repo·`main`은 변경하지 않았다.
- commit·push·PR·merge는 실행하지 않았다.
- `main` merge 승인: `0/3`.

## 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `사용자 사용 방법` |
| User manual | 본 문서에 포함 | `사용자 사용 방법` |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성됨 / 재검수 대기 | `tasks/ul891-set-001-change-008-user-validation-checklist.md` |
