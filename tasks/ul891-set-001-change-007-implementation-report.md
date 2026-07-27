# TASK-UL891-SET-001 Change 007 구현 보고 — UL891 설계 조회·수정 분리

상태: `사용자 검수 실패 확인 / Change 008 보정 완료`

## 해결한 업무 문제

신규 UL891 프로젝트의 설계 탭에는 세트 사양 편집기와 일반 평면 패널 설계 영역이 함께 보여 같은 설계정보를 두 번 입력해야 하는 것처럼 보였다. 편집 control이 프로젝트 상세에 직접 노출됐고 `Draft 저장`·`사양 확정`이라는 내부 용어와 부족한 완료 안내 때문에 사용자는 저장 결과를 즉시 판단하기 어려웠다.

## 요청별 구현 결과

1. 신규 UL891 프로젝트의 상세 설계 탭을 조회 전용으로 변경했다. 세트 사양·저장 version·공통 패널명/규격/치수·실물 세트·개별 패널은 그대로 조회한다.
2. UL891 세트 공통 사양과 중복되던 일반 평면 패널 설계 영역·`패널명·사이즈 수정` 버튼은 UL891 상세에서 제거했다.
3. 프로젝트 단위 패널 QR 목록·일괄 선택·발급·인쇄 기능은 설계 입력 영역과 분리해 그대로 유지했다.
4. 수정 권한이 있고 프로젝트가 진행 중이면 설계 조회 상단에 `수정` 버튼 한 개만 표시한다.
5. `수정`은 기존 설계 수정 route를 재사용하되 UL891이면 세트 사양 전용 전체 폭 입력 화면을 연다. 비-UL891과 legacy UL891은 기존 평면 패널 수정 화면을 유지한다.
6. 사용자 표시 명칭을 `Draft 저장 → 임시저장`, `사양 확정 → 저장`, `확정 버전 → 저장된 버전`, `새 버전 생성 → 새 수정본 만들기`로 변경했다.
7. 내부 API·DB의 `Draft / Published / Superseded` 상태, Published 불변성, 제조 시작 기준과 동시성 계약은 변경하지 않았다.
8. 실행 중에는 `임시저장 중`·`저장 중`, 성공 뒤에는 해당 세트 action 바로 아래에 성공 문구를 표시한다. 실패도 같은 위치에 표시한다.

## SOP

1. 프로젝트 상세에서 `설계` 탭을 연다.
2. 저장된 세트 공통 사양과 실물 세트·패널을 조회한다.
3. 변경이 필요하면 권한이 있는 설계 담당자가 상단 `수정`을 누른다.
4. 계속 편집할 내용은 `임시저장`한다. 임시저장 성공 안내를 확인한 뒤 같은 화면에서 작업을 이어갈 수 있다.
5. 제조 기준으로 사용할 사양은 `저장`한다. 저장 완료 안내가 표시되고 해당 version은 `저장 완료`로 조회된다.
6. 이미 저장된 사양을 바꾸려면 `새 수정본 만들기`로 새 임시저장 version을 만든다. 기존 저장 version은 직접 덮어쓰지 않는다.
7. 설계 탭으로 돌아와 최신 저장 상태를 조회한다. QR 발급은 같은 상세 설계 탭의 별도 `패널 QR` 영역에서 수행한다.

## 사용자 화면 안내

- 프로젝트 상세 설계 탭은 조회 화면이다. 입력창이 보이지 않는 것이 정상이다.
- `수정` 버튼은 설계 수정 권한과 진행 중 프로젝트 조건을 모두 만족할 때만 보인다.
- `임시저장`은 나중에 계속 수정할 수 있는 상태다.
- `저장`은 제조에 적용할 사양을 저장하는 동작이다.
- 일반 프로젝트는 기존 `패널명·사이즈 수정`과 Excel 입력 방식을 그대로 사용한다.

## 변경 파일

- Frontend: `frontend/src/App.tsx`
- UL891 화면: `frontend/src/Ul891SetWorkspace.tsx`
- Style: `frontend/src/styles.css`
- Tests: `frontend/tests/App.test.tsx`, `frontend/tests/Ul891SetWorkspace.test.tsx`
- Task·governance: Change 007 계약, 본 보고서, 사용자 검수 체크리스트, Product Roadmap, 실험 완료 원장

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `UL891-007-F01` | P2 | Resolved | 프로젝트 상세에서 세트 공통 설계와 평면 패널 설계가 함께 노출돼 중복 입력처럼 보였다. | 구조 mode를 먼저 판별해 신규 UL891은 세트 조회만, 일반/legacy는 기존 평면 설계를 표시한다. |
| `UL891-007-F02` | P2 | Resolved | 상세 화면 안에서 직접 수정돼 조회와 편집 경계가 불명확했다. | 상세를 조회 전용으로 만들고 단일 `수정` 버튼으로 별도 입력 route에 진입시킨다. |
| `UL891-007-F03` | P2 | Resolved | 내부 상태 용어와 저장 결과 안내 부족으로 동작 완료를 판단하기 어려웠다. | 사용자 용어를 `임시저장`·`저장`으로 바꾸고 실행 action 바로 아래에 loading·success·error를 표시한다. |

Open P0/P1/P2: `0/0/0`.

## 검증

| 검증 | 결과 |
| --- | --- |
| Frontend lint | PASS — error 0, 기존 `src/main.tsx` Fast Refresh warning 1 |
| Frontend typecheck | PASS |
| Frontend 전체 unit | PASS — 22 files, `138/138` |
| UL891 집중 unit | PASS — 조회 전용·수정 진입·임시저장/저장 feedback |
| 일반 패널 회귀 unit | PASS — 기존 수정·Excel 양식 경로 유지 |
| Frontend production build | PASS — 기존 500kB 초과 chunk warning 유지 |
| 고정 runtime health | PASS — Frontend `42983`, Backend `41166`, database reachable |
| 실제 desktop UI | PASS — 상세 입력 0, 일반 수정 버튼 0, UL891 수정 버튼 1, QR 유지, horizontal overflow 0 |
| 비설계 사용자 UI | PASS — 상세 입력 0, 수정 버튼 0, 조회 전용 안내 1 |
| 실제 390px UI | PASS — 조회·수정 화면 적응형 배치, horizontal overflow 0 |
| Browser console | PASS — warning/error 0 |

브라우저 검수는 기존 검수 데이터를 조회만 했고 저장 action은 실행하지 않았다. 임시저장·저장의 요청/응답·성공 feedback은 unit에서 가짜 응답으로 검증했다.

## 독립 재검토

사용자 요청 없는 하위 agent 생성을 금지하는 실행 규칙 때문에 별도 agent를 만들지 않았다. 구현 종료 뒤 같은 session에서 Task 계약, 실제 diff, 일반/UL891 분기, 권한, QR 보존, 전체 회귀와 browser 결과를 read-only로 다시 대조했다.

## 개인정보·secret 검토

- 보고서에는 실제 프로젝트명·고객명·사용자명·UUID·업무 원문을 기록하지 않았다.
- 실제 검수 결과는 control 개수·overflow·오류 유무만 비식별 projection으로 기록했다.
- Teams·메일 등 실제 provider는 호출하지 않았다.

## Rollback·forward-fix

- Change 007의 Frontend 분기·표시·test·문서만 이전 상태로 되돌릴 수 있다.
- Backend·DB·migration·세트 version·패널·QR 데이터는 변경하지 않았으므로 data rollback은 없다.
- 후속 수정에서도 `UL891 세트 공통 설계`와 `일반 평면 패널 설계`를 다시 같은 상세 화면에 중복 노출하지 않는다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `SOP` |
| User manual | 본 문서에 포함 | `사용자 화면 안내` |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/ul891-set-001-change-007-user-validation-checklist.md` |

## 변경·게시 경계

- 현재 experiment worktree와 고정 검수 runtime만 갱신·검증했다.
- 기존 DESIGN-000 Change 004 미커밋 변경을 보존했다.
- commit·push·PR·대표 repo·`main`·실제 provider는 변경하지 않았다.
- `main` merge 승인: `0/3`.

## 사용자 검수 후속

사용자 검수에서 `저장`이 현재 form 값을 반영하지 않고 기존 Draft를 바로 Publish하는 결함과 불필요한 `규격` 필수 조건이 확인됐다. 원인·보정·재검수 기준은 [Change 008 구현 보고](ul891-set-001-change-008-implementation-report.md)와 [Change 008 사용자 검수 체크리스트](ul891-set-001-change-008-user-validation-checklist.md)로 승계한다.
