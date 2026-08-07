# TASK-E2E-FULL-SUITE-001 Change 012 구현 보고서

## 1. 결과와 범위

- Task 유형: `BUGFIX`
- 변경 계약: [Change 012](e2e-full-suite-001-change-012.md)
- 기준 `origin/main`: `7a8d241d56e2f94b33c3125dd34d95ef4a7158f0`
- 상태: 구현·로컬 전체 자동 검증·PR #77 최신 head CI·원격 `main` 병합·merge SHA push CI 완료
- 제품 영향: 프로젝트별 Pending route의 프로젝트 메타데이터 loading·error·retry와 제목·코드 source만 변경
- 제외: Backend·API·DB·migration·dependency·runtime·Persistent UAT·Azure 운영 release·실제 provider

PR #76의 최신 head CI는 Frontend·Backend·Full-Stack `3/3`을 통과했고 Change 018 source와 Change 011을 원격 `main`에 병합했다. 그러나 merge SHA의 push CI에서 전체 Full-Stack은 `55/56`으로 같은 제목 assertion이 다시 실패했다. 프로젝트별 Pending API는 정확히 필터링됐지만 화면 제목이 전역 최근 100개 프로젝트 목록 또는 첫 Pending에 의존하는 두 번째 원인이 남아 있었다.

이번 변경은 URL의 프로젝트 ID로 기존 프로젝트 상세 API를 직접 읽는다. 대상 프로젝트가 전역 목록 밖이고 Pending이 0건이어도 실제 제목·코드를 표시하며, 정확한 메타데이터를 읽지 못하면 일반 제목으로 대체하지 않고 오류와 재시도를 제공한다.

## 2. 수정 전 재현과 Root cause

| 항목 | 확인 결과 |
| --- | --- |
| PR #76 최신 head CI | Frontend·Backend·Full-Stack `3/3 PASS` |
| PR #76 merge | 원격 `main` 반영 완료 |
| merge SHA push CI | Frontend·Backend `PASS`, Full-Stack `55/56 FAIL` |
| 실패 조건 | suite 앞부분이 만든 합성 프로젝트가 100개를 넘고 대상 프로젝트의 Pending이 0건인 상태 |
| Pending API | `projectId`로 정확히 필터링 |
| 제목 source | `listProjects(... pageSize: 100)` 또는 `items[0]` |
| 사용자 영향 | 정확한 프로젝트별 URL인데도 `Pending 프로젝트`·`프로젝트` 일반 표기로 보일 수 있음 |

수정 전 회귀 test는 실제 프로젝트 제목을 찾지 못해 실패했다. timeout·재시도·목록 크기 확대는 사용하지 않았다.

## 3. 구현 결정

1. 프로젝트별 route에서 `getProject(developmentUserKey, initialProjectId)`로 정확한 프로젝트를 조회한다.
2. 화면 제목·코드는 exact project response만 사용한다.
3. Pending 목록·선택 Excel filter도 별도 로컬 복사본이 아니라 현재 `initialProjectId`를 직접 사용해 화면 전환 뒤 이전 프로젝트 범위가 남지 않게 한다.
4. 조회 중에는 기존 Pending loading을 표시한다.
5. 조회 실패는 기존 오류 안내와 `다시 시도`로 복구하며 generic heading을 렌더링하지 않는다.
6. 전역 Pending dashboard의 100개 목록·정렬·KPI와 Pending 등록·필터·상태 전이는 변경하지 않는다.
7. 기존 실제 역할 Full-Stack spec에서 같은 프로젝트별 화면을 desktop과 390px로 확인하고 가로 넘침 `0`을 검증한다.

## 4. 변경 파일

| 파일 | 역할 |
| --- | --- |
| `frontend/src/PendingPage.tsx` | exact project metadata loading·error·retry와 제목·코드 source |
| `frontend/tests/App.test.tsx` | 목록 밖·Pending 0건과 metadata 실패→retry 회귀 |
| `frontend/e2e/full-stack/workflow-continuity-change-003.full-stack.spec.ts` | desktop·390px 정확한 제목과 overflow 회귀 |
| `tasks/e2e-full-suite-001-change-011-implementation-report.md` | PR #76·main push 결과와 후속 Finding 연결 |
| 본 문서·Change 012 | 승인·구현·검증·rollback 기록 |
| `tasks/azure-deploy-001-implementation-report.md` | Change 018 source 게시와 운영 release 분리 상태 |
| `docs/00-product-roadmap.md` | Task·추적·Decision Log 동기화 |

## 5. 검증 결과

| 검증 | 적용 | 결과 |
| --- | --- | --- |
| 수정 전 deterministic unit | 직접 재현 | `FAIL`, exact project heading 없음 |
| Frontend lint | 필수 | `PASS`, error `0`, 기존 warning `1` |
| Frontend typecheck | 필수 | `PASS` |
| Frontend unit | 필수 | `177/177 PASS` |
| Frontend build | 필수 | `PASS`, 기존 large chunk warning 유지 |
| 대상 Full-Stack 반복 | 직접 회귀 | 수정·mobile readiness 보정 뒤 `3/3 PASS` |
| 대상 desktop·390px | 사용자 UX | 실제 제목 표시·horizontal overflow `0` |
| 전체 Full-Stack | 게시 전 | `56/56 PASS`, 원래 실패 순번 54 통과 |
| Full-Stack isolation cleanup | 필수 | DB drop·container·network 제거 `PASS` |
| Backend Release build | 전체 pipeline | warning/error `0/0` |
| Backend 전체 test | 전체 pipeline | `486/486 PASS`, skipped `0` |
| PR #77 최신 head CI | 게시 Gate | Frontend·Backend·Full-Stack `3/3 PASS` |
| PR #77 원격 `main` 병합 | 게시 Gate | `PASS`, merge SHA `32c62f9a7c030410e2ebd060fc70b40376546945` |
| merge SHA push CI | 최종 Gate | run `31137268487`, Frontend·Backend·Full-Stack `3/3 PASS` |
| `git diff --check` | 필수 | `PASS` |
| Persistent UAT·실제 provider | 제외 | `N/A` — 영향·승인 범위 밖 |

390px 첫 반복에서 responsive state 적용 전 overflow를 측정해 일시적으로 `191px`을 관찰했다. 캡처된 최종 mobile 화면에는 실제 넘침이 없었고, mobile layout class가 활성화된 뒤 측정하도록 readiness를 고정한 후 반복 `3/3`과 전체 suite가 통과했다. 제품 CSS를 우회하거나 overflow를 숨기지 않았다.

## 6. Finding gate

| ID | Severity | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `E2E-PENDING-GLOBAL-PAGINATION-COUPLING` | P2 | `RESOLVED` | 전역 목록에서 생성 프로젝트를 찾던 test 결함 | Change 011 프로젝트별 route, PR #76 최신 head CI 통과 |
| `PENDING-SCOPED-DEEP-LINK-METADATA-FALLBACK` | P2 | `RESOLVED` | exact route 제목이 최근 100개 목록·첫 Pending에 의존 | exact project API·fail-closed retry, 전체 `56/56`, PR #77·merge SHA CI `3/3` 통과 |
| `PENDING-MOBILE-READINESS-MEASUREMENT-RACE` | P3 | `RESOLVED_TEST` | viewport 변경 직후 desktop state에서 overflow 측정 가능 | mobile layout 활성 확인 뒤 overflow 측정 |

Open P0/P1/P2/P3는 `0/0/0/0`이다. P2는 PR #77 최신 head와 merge SHA push CI가 모두 통과해 `RESOLVED`로 닫았고, mobile readiness P3도 test 보정으로 해소했다.

## 7. 개인정보·secret·artifact

- 검증은 synthetic fixture와 count/status/SHA projection만 사용했다.
- 실제 사용자·고객·프로젝트·계정·identifier·credential과 외부 provider payload를 기록하지 않았다.
- Playwright screenshot·video·error context와 test-results는 tracked/staged하지 않는다.

## 8. SOP와 Rollback

1. 프로젝트별 Pending route에서 exact project detail과 Pending list가 각각 성공하는지 확인한다.
2. 프로젝트가 전역 첫 100개 목록 밖이고 Pending 0건이어도 실제 제목·코드를 확인한다.
3. project detail 실패 시 generic 제목이 아니라 오류·재시도를 확인한다.
4. desktop·390px 제목, filter와 empty state, overflow `0`을 확인한다.
5. 전체 Full-Stack에서 후반부 Change 003 spec이 통과하는지 확인한다.

Rollback은 `PendingPage.tsx`의 exact metadata state/effect와 두 회귀 test를 함께 revert한다. API·DB·migration·runtime rollback은 없다.

## 9. User manual과 사용자 검수 체크리스트

프로젝트의 Pending 화면을 열면 URL에 지정된 프로젝트 제목과 코드가 항상 표시된다. Pending이 아직 없어도 프로젝트 이름은 유지된다. 프로젝트 정보를 일시적으로 읽지 못하면 `다시 시도`를 눌러 같은 화면에서 복구한다.

- [x] 목록 밖·Pending 0건 프로젝트 제목·코드 자동 검증
- [x] project metadata 오류·재시도 자동 검증
- [x] desktop·390px 정확한 제목과 overflow `0`
- [x] Frontend·Backend·Full-Stack 전체 로컬 검증
- [x] PR #77 최신 head CI `3/3`
- [x] 원격 `main` 병합과 merge SHA push CI `3/3`

별도 사용자 수기 입력 검수는 `N/A`다. 새 입력 능력이나 화면 구조를 추가하지 않고 기존 프로젝트별 link를 복구한다.

## 10. 종료 산출물과 남은 항목

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | `## 8` |
| User manual | 포함됨 | `## 9` |
| Roadmap update | 작성됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 자동·GitHub 검증 완료 | `## 9` |

Change 012의 구현·검증·게시 Gate는 완료됐다. 남은 항목은 현재 Task 범위 밖인 Change 018의 실제 Azure 운영 release뿐이며 계속 별도 명시 실행으로 유지한다.
