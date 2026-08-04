# TASK-UL891-PRODUCTION-PLAN-001 Change 003 구현 보고 — 기본계획 저장 검증과 계획 구조 한 행 편집

상태: `사용자 재검수 완료 / main 병합 승인`

## 목적·포함 범위

- 사용자 검수에서 `전체 기본계획` 저장 시 이미 선택된 실적 연결을 다시 선택하라는 오류가 발생한 원인을 해소한다.
- 계획 구조의 실적 연결 선택을 필수 checkbox 오른쪽으로 옮기고 desktop 한 행과 정상 checkbox 크기를 제공한다.
- 기존 구조 연결, 세트별 일정 API, 권한·CAS·audit, 비-UL891 흐름은 변경하지 않는다.
- Backend·DB·migration 변경은 없다.

## 원인과 구현 결과

1. 실제 API에서 공통 구조 `items`는 각 연결 1개를 가지지만 일정 전용 `setDefault.items`와 세트 scope row는 `connections: []`다.
2. Frontend 공통 `LINKED_V1` 검증이 편집 mode를 구분하지 않아 기본계획·세트 일정도 연결 1개를 요구했고, 저장 요청 전 오류를 만들었다.
3. 검증에 `validateConnections` context를 추가해 비-세트 Linked 계획과 UL891 계획 구조에서만 연결을 검사한다. 기본계획·개별 세트 일정은 기간·인원 등 일정값만 검사하고 각 전용 API로 저장한다.
4. 기존 test fixture가 기본계획에도 구조 연결을 복사해 결함을 가렸으므로 실제 API와 같이 `connections: []`로 바꿨다.
5. 계획 구조 desktop row는 `순번 | 계획 항목 | 필수 | 실적 연결 | 삭제` 순서로 통합했다. 반복되던 별도 전체 너비 연결 fieldset을 제거했다.
6. 필수 checkbox는 범용 input 크기를 상속하지 않도록 20×20px로 고정하고 수평 중앙 정렬했다.
7. 900px 이하에서는 구조 row를 명시적 1열로 전환해 실적 선택의 intrinsic width가 viewport를 넓히지 않게 했다.

## 변경 파일

- `frontend/src/App.tsx`: 편집 mode별 연결 검증과 계획 구조 inline connection editor.
- `frontend/src/styles.css`: desktop 한 행, 20px checkbox, 390px 1열·overflow 보정.
- `frontend/tests/App.test.tsx`: 실제 API와 같은 기본계획 fixture, 저장·DOM 순서 회귀.
- `frontend/e2e/full-stack/ul891-user-corrections.full-stack.spec.ts`: 연결 유지, checkbox 크기, desktop 오른쪽 배치 회귀.
- `tasks/ul891-production-plan-001-change-003.md`, 본 보고서와 사용자 검수 체크리스트.
- `docs/00-product-roadmap.md`: Change 003 상태·결정 이력.

## 검증 결과

| 검증 | 결과 |
| --- | --- |
| Frontend 전체 unit | PASS — `145/145` |
| Frontend typecheck | PASS |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend production build | PASS — 기존 500kB 초과 chunk warning 유지 |
| 실행 중 격리 검수 서버 기본계획 저장 | PASS — 연결 재선택 오류 없이 저장 성공 |
| 실행 중 격리 검수 서버 구조 연결 | PASS — 3개 연결값 유지 |
| desktop 실제 배치 | PASS — row 3개, checkbox `20×20`, 실적 select가 오른쪽에 위치, page overflow 없음 |
| 390px 실제 배치 | PASS — checkbox `20×20`, 실적 select 261px, overflow element `0` |
| 새 격리 DB Full-Stack Chromium | PASS — `1/1`, 실패 시 격리 자원 정상 정리 후 test locator 보정·재실행 |
| Git whitespace 검사 | PASS — `git diff --check` |

## Finding과 resolution

| Finding ID | 심각도 | 상태 | Resolution |
| --- | --- | --- | --- |
| `UL891-PLAN-C003-F01` | P1 | RESOLVED | 기본계획·세트 일정에서 구조 연결 검증을 생략하고 일정 전용 API를 호출한다. |
| `UL891-PLAN-C003-F02` | P2 | RESOLVED | 구조 실적 선택을 필수 checkbox 오른쪽 desktop 한 행으로 이동했다. |
| `UL891-PLAN-C003-F03` | P2 | RESOLVED | checkbox를 20×20px로 고정하고 390px 구조 grid를 1열로 제한했다. |
| `UL891-PLAN-C003-F04` | P2 | RESOLVED | 실제 API와 다른 test fixture를 보정해 P1 회귀를 검출한다. |

Open P0/P1/P2: `0/0/0`.

## 개인정보·게시·복구 경계

- 격리 synthetic 프로젝트와 tmpfs PostgreSQL만 사용했고 외부 provider는 비활성화했다.
- Full-Stack 검증 자원은 성공·실패 run 모두 제거됐다. 사용자 검수용 5175/5082 서버는 계속 유지한다.
- commit·push·PR·merge, 대표 repo·`main`, Persistent UAT와 실제 provider는 실행하지 않았다.
- Frontend-only forward fix이므로 DB rollback은 없으며 문제가 있으면 해당 UI·검증 diff만 되돌릴 수 있다.
