# TASK-UX-001 A1 Action Feedback Implementation Report

## 1. 상태와 기준선

| 항목 | 결과 |
| --- | --- |
| Task | `TASK-UX-001` A1 |
| Task 유형 | `NEW_FEATURE` → experiment 2-pass fast-track 구현 |
| Branch | `experiment/task-ux-001-action-feedback` |
| 구현 기준 | [Fable 2차 기획](../docs/25-action-feedback-a1-plan.md) |
| 자동 검증 | 완료 |
| 사용자 검수 | `사용자 검수 대기` |
| Local commit | 이 보고서를 포함한 experiment local commit |
| Push / PR / Merge | 미승인·미실행 |
| `main` merge 승인 | `0/3` |
| Persistent UAT / 실제 provider | 미승인·미변경 |

이 문서는 experiment 코드 구현과 isolated 자동 검증 결과를 기록한다. 대표 저장소, `main`, live UAT 적용 또는 사용자 직접 검수를 완료로 표현하지 않는다.

## 2. 목적, 배경과 범위

내 업무와 알림의 mutation action이 단일 문자열 결과만 표시해 사용자가 처리 중인지, 중복 클릭됐는지, mutation은 성공했지만 목록 갱신만 실패했는지 구분하기 어려운 문제를 해결했다.

포함 범위는 공통 scope 기반 feedback hook, 기존 `ActionFeedback` focus/live-region 확장, 내 업무의 시작·이동/완료, 알림의 개별·전체 읽음, post-mutation refresh, request generation guard, unit/integration/isolated E2E와 desktop·390px screenshot이다.

제외 범위는 A2 생산계획·구매·자재·패널·Excel 확대, Backend/API/DB/migration/권한·업무 규칙 변경, 전역 toast·feedback store, 자동 소멸, target-not-found 신규 상태, 대표 저장소·`main`, Persistent UAT mutation·runtime handover와 실제 provider다.

## 3. Fable 5 기획과 Claude 사용량

Fable은 `scripts/run-fable-readonly.sh`의 read-only 경계에서만 실행했다. 1차 원문과 2차 원문은 byte-for-byte artifact로 보존했고 Codex review 7건은 2차 기획에서 모두 반영됐다. `openBlockingDecisionCount`는 `0`이다.

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 1차 planning 직전 | 0% 사용 / 100% 잔여 / 23:59 KST 초기화 | 9% 사용 / 91% 잔여 / 07-25 08:00 KST 초기화 | 18% 사용 / 82% 잔여 / 초기화 parse 불가 |
| 1차 planning 직후 | 0% 사용 / 100% 잔여 / 23:59 KST 초기화 | 10% 사용 / 90% 잔여 / 07-25 08:00 KST 초기화 | 19% 사용 / 81% 잔여 / 초기화 parse 불가 |
| 2차 planning 직전 | 11% 사용 / 89% 잔여 / 23:59 KST 초기화 | 10% 사용 / 90% 잔여 / 07-25 07:59 KST 초기화 | 19% 사용 / 81% 잔여 / 초기화 parse 불가 |
| 2차 planning 직후 | 11% 사용 / 89% 잔여 / 23:59 KST 초기화 | 10% 사용 / 90% 잔여 / 07-25 07:59 KST 초기화 | 19% 사용 / 81% 잔여 / 초기화 parse 불가 |
| 구현 종료 최신 조회 | 19% 사용 / 81% 잔여 / 23:59 KST 초기화 | 11% 사용 / 89% 잔여 / 07-25 07:59 KST 초기화 | 21% 사용 / 79% 잔여 / 초기화 parse 불가 |

1차 runner는 `CREATED_FULL_BASELINE`, model 263초, stdout 22,453 bytes, stderr 0이었다. 2차 runner는 `RESUMED_ARTIFACT_PREFLIGHT`, `baselineReused=true`, `driftStatus=UNCHANGED`, model 116초, stdout 17,223 bytes, stderr 0이었다. 종료 cleanup은 `FABLE_TASK_SESSION_CLEANED`로 Task 전용 session·transcript 각 1개만 제거했다.

## 4. 아키텍처와 업무 계약

### 공통 feedback contract

`useActionFeedback`은 scope별 `loading | success | error | partial`, busy 집합과 최신 feedback을 관리한다. busy ref를 state보다 먼저 동기 갱신해 같은 event tick의 중복 호출도 차단한다. 개별 알림과 전체 읽음은 exact scope와 prefix conflict로 양방향 잠금하고 서로 다른 행은 병렬로 처리할 수 있다.

mutation 뒤 refresh는 `Promise<boolean>` 결과를 기다린다. mutation 성공·refresh 실패는 실패로 되돌리지 않고 `partial`로 표시한다. `ApiError.status` 403·404·409와 그 밖의 오류를 구조적으로 분류해 권한 확인, 최신 상태 확인 또는 재시도를 안내하며 문자열 포함 판정은 제거했다.

### 내 업무와 알림

내 업무·알림의 `load`는 결과를 반환하고 `replace`와 `preserve` 모드를 구분한다. 최초 조회·탭 전환·수동 새로고침은 loading state로 교체하고 post-mutation refresh는 기존 ready data와 선택을 유지한다. generation 번호가 오래된 응답의 화면 덮어쓰기를 차단한다.

행이 살아 있는 loading/error는 카드·table action 인접 영역에 표시한다. 완료·읽음 성공이나 refresh 부분 성공처럼 active tab에서 행이 사라질 수 있는 결과는 페이지 contextual region에 항목 label과 함께 보존한다. 전체 읽음 feedback은 header action group 옆에 유지한다.

error와 partial은 `tabIndex=-1`의 안정된 feedback anchor로 focus를 옮기며 success는 focus를 강제하지 않는다. error는 `role=alert`, 나머지는 `role=status`와 polite live region을 사용한다. 모바일 390px에서 header action 대비와 행 feedback의 line-clamp를 별도로 해제해 다음 행동 문장이 잘리지 않게 했다.

## 5. 영향 범위

| 영역 | 영향 |
| --- | --- |
| Frontend | 공통 hook, 내 업무·알림 action/load, feedback 배치·스타일·테스트 |
| Backend / API | 변경 없음. 기존 응답·status 계약만 사용 |
| DB / Migration | 변경 없음 |
| 권한 / Workflow | 변경 없음 |
| Excel | export 기능 변경 없음, 선택 상태 보존 회귀 검증 |
| PDF / 첨부파일 | `N/A` — 범위에 포함되지 않음 |
| 외부 알림 provider | 변경·호출 없음 |

## 6. 실제 변경 파일

| 역할 | 파일 |
| --- | --- |
| Fast-track source | `tasks/ux-001-interview.md`, `ux-001-planning.md`, `ux-001-review.md`, `ux-001-change-001.md`, `docs/25-action-feedback-a1-plan.md` |
| 공통 contract | `frontend/src/useActionFeedback.ts` |
| 화면 연동·접근성 | `frontend/src/App.tsx` |
| adaptive style | `frontend/src/styles.css` |
| Unit·integration | `frontend/tests/useActionFeedback.test.tsx`, `frontend/tests/App.test.tsx` |
| Isolated browser E2E | `frontend/e2e/full-stack/action-feedback.full-stack.spec.ts` |
| Visual evidence | `tasks/ux-001-screenshots/*.png` 9개 |
| Roadmap·종료 원장 | `docs/00-product-roadmap.md`, 이 보고서 |

## 7. 실행한 검증과 결과

| 검증 | 결과 | 근거 |
| --- | --- | --- |
| Frontend lint | PASS — error 0, 기존 `main.tsx` Fast Refresh warning 1 | `pnpm lint` |
| Frontend typecheck | PASS | `pnpm typecheck` |
| Frontend unit/integration | PASS — 11 files, `99/99` | `pnpm test` |
| Frontend production build | PASS — 기존 500kB chunk warning 유지 | `pnpm build` |
| TASK-UX-001 Full-Stack E2E | PASS — `3/3` | 전용 PostgreSQL container/network/tmpfs, 임의 backend/frontend port, provider 전부 disabled |
| 영향 Full-Stack E2E | PASS — `3/3` | `mobile-first-experience` 2건 + `all-pages-selected-export` 1건; 기존 모바일 screenshot 14개 backup/byte-equality 복원 |
| Visual QA | PASS | 내 업무 desktop·mobile 정상/partial과 loading, 알림 mobile 정상/loading/error·desktop row 제거 뒤 success |
| Mobile overflow | PASS — 390px page-level overflow 0 | E2E assertion |
| Error/partial focus | PASS | E2E `toBeFocused` |
| Browser error signal | PASS | 합성 500·409는 request console error 각 1건, page error 0; success console/page error 0 |
| Git whitespace | PASS | `git diff --check` |
| Backend test | 미실행 / `N/A` | Backend/API/DB 변경 없음 |
| CI | 미실행 / `N/A` | push·PR 미승인 experiment local 범위 |
| Persistent UAT | 미실행 / `N/A` | 명시적 제외. 대표 5174/5081 process와 DB 보존 |

Full-Stack E2E는 예약된 5174·5081·5432를 거부하는 safety guard 아래 실행했다. 각 실행의 임시 DB·container·network는 성공 뒤 삭제 검증됐고 외부 provider는 모두 disabled였다.

## 8. Finding과 resolution

| ID | Severity | 상태 | 원인·영향과 해소 위치 |
| --- | --- | --- | --- |
| `UX-A1-ROW-FEEDBACK-DISAPPEARS` | P1 | `RESOLVED` | active tab에서 행 제거 시 결과 소실 → page contextual feedback |
| `UX-A1-REFRESH-FIRE-AND-FORGET` | P1 | `RESOLVED` | mutation 뒤 refresh 결과 미관찰 → boolean 반환·partial 분리 |
| `UX-A1-REFRESH-RESPONSE-RACE` | P1 | `RESOLVED` | 탭 전환 응답 역전 → generation guard |
| `UX-A1-BULK-ROW-CONFLICT` | P2 | `RESOLVED` | 전체/개별 읽음 경쟁 → exact/prefix 양방향 conflict |
| `UX-A1-TONE-AND-GUIDANCE` | P2 | `RESOLVED` | 한글 문자열 포함 판정 → `ApiError.status` mapper |
| `UX-A1-FOCUS-ANCHOR` | P2 | `RESOLVED` | 제거되는 행 focus 유실 → row/contextual stable anchor |
| `UX-A1-SELECTION-REGRESSION` | P2 | `RESOLVED` | refresh 실패 시 ready data·선택 손실 → preserve mode |
| `UX-A1-MOBILE-HEADER-ACTION-CONTRAST` | P2 | `RESOLVED` | 기존 mobile header override가 신규 nested button을 흰색으로 만듦 → final scoped contrast style |
| `UX-A1-MOBILE-FEEDBACK-CLAMP` | P2 | `RESOLVED` | 기존 card description 1줄 clamp가 feedback까지 상속 → direct feedback clamp 해제 |
| `DESIGN001-F01` | P3 | `BACKLOG` | 기존 production chunk 500kB warning. 기능 실패는 없고 후속 bundle splitting 범위 |
| `DESIGN001-F02` | P3 | `BACKLOG` | 기존 `main.tsx` Fast Refresh warning 1건. 이번 Task 이전 baseline |

Open P0/P1/P2는 `0/0/0`이다. P3 두 건은 기존 `DESIGN001` backlog에 연결했다.

## 9. 개인정보·secret과 artifact 검토

E2E와 screenshot은 고정 synthetic project/work/notification 값만 사용했다. 실제 사용자·회사 계정·고객·프로젝트·알림 원문, tenant/object ID, credential, token, raw API/DB body를 tracked 문서에 기록하지 않았다. screenshot 7개는 isolated runtime의 합성 화면이며 대표 UAT를 촬영하지 않았다.

dependency, migration, env, certificate, secret, generated build output와 Playwright HTML report는 Task staging 대상이 아니다.

## 10. SOP

1. 사용자가 내 업무의 `이동` 또는 `작업 완료`, 알림의 `읽음` 또는 `전체 읽음`을 실행한다.
2. 실행한 행 또는 header action에서 처리 중 label과 feedback을 확인한다. 같은 대상 버튼은 완료될 때까지 잠긴다.
3. 성공이면 목록 상단의 항목명 포함 결과를 확인한다. 성공 행이 현재 tab에서 사라지는 것은 정상이다.
4. 노란색 부분 성공이면 mutation은 완료된 것이므로 같은 action을 다시 누르지 않고 `새로고침`으로 최신 목록만 읽는다.
5. 빨간색 오류면 focus된 안내의 권한 확인·새로고침·재시도 지침을 따른다. 409는 최신 목록을 확인한 뒤 재시도한다.
6. 사용자가 수동 새로고침하거나 tab을 바꾸면 이전 feedback은 초기화된다.

장애 시 DB 상태를 직접 수정하지 않는다. 반복되는 403은 담당자·관리자에게 권한을 확인하고, 404·409는 최신 목록을 다시 읽는다. refresh만 실패한 partial은 mutation 재실행으로 중복 처리하지 않는다.

## 11. User manual

- 파란색: 처리 중이며 같은 버튼을 다시 누를 필요가 없다.
- 초록색: 처리와 최신 목록 확인이 모두 끝났다.
- 노란색: 업무 처리는 끝났지만 최신 목록만 불러오지 못했다. `새로고침`을 누른다.
- 빨간색: 업무 처리가 끝나지 않았다. 안내된 권한·최신 상태·연결 상태를 확인한 뒤 다시 시도한다.
- 알림 `전체 읽음` 진행 중에는 개별 읽음을, 개별 읽음 진행 중에는 전체 읽음을 실행할 수 없다. `상세`와 `이동`은 계속 사용할 수 있다.

## 12. User validation checklist

상태: `사용자 검수 대기`

### 자동 검증 완료

- [x] 내 업무 desktop 정상·processing·partial과 기존 행·선택 보존
- [x] 알림 mobile 390px 정상·processing·409 error, 안내 전체 표시와 focus
- [x] 알림 desktop 성공 뒤 unread 행 제거와 contextual 결과 보존
- [x] 동일 scope 중복 submit과 개별/전체 읽음 양방향 conflict
- [x] success/error/partial 구조 판정과 reset
- [x] desktop·390px page-level horizontal overflow 0
- [x] 대표 5174/5081과 Persistent UAT 미변경

### 사용자 직접 확인 대기

- [ ] desktop 내 업무 부분 성공 안내가 처리 완료와 새로고침 필요를 명확히 구분하는지 확인
- [ ] mobile 알림에서 처리 중·오류 feedback이 행과 자연스럽게 연결되는지 확인
- [ ] 390px의 글씨·버튼·카드 밀도와 header action 대비가 편한지 확인
- [ ] desktop 알림에서 행이 사라진 뒤 성공 결과 위치가 이해되는지 확인

## 13. Rollback과 복구

대표 저장소와 `main`에는 변경이 없으므로 원본 rollback 작업은 없다. 실험 결과를 폐기하려면 이 branch를 merge하지 않고 보존하거나 별도 승인 아래 정리한다. 코드 단위 rollback이 필요하면 local experiment commit을 revert하고 Backend/DB/migration/provider 보상은 수행하지 않는다.

## 14. 해결한 업무 문제

사용자가 action 결과를 색상 문자열에 의존해 추측하거나, 처리된 행이 사라져 성공 여부를 잃거나, refresh 장애를 mutation 실패로 오인해 같은 업무를 다시 실행하는 문제를 해결했다. 모바일에서도 오류와 다음 행동을 잘리지 않은 행 맥락에서 확인할 수 있다.

## 15. 기술적 결정과 검토한 대안

- 전역 toast/store 대신 두 화면 내부 scope hook을 선택해 A1 범위를 고정했다.
- mutation 성공·refresh 실패를 error로 합치지 않고 partial로 분리했다.
- 모든 화면을 동시에 잠그지 않고 같은 scope와 알림 bulk/row conflict만 잠갔다.
- 성공 자동 소멸은 사용자가 결과를 읽기 전에 사라질 수 있어 제거했다.
- Backend field-error 확장과 target-not-found 신규 상태는 A1 계약에 필요하지 않아 보류했다.

## 16. 시행착오 및 폐기한 접근

첫 visual capture에서 기존 mobile header의 흰색 button 규칙과 card description의 1줄 clamp가 신규 action button·feedback에 상속되는 것을 발견했다. 기능 테스트만 통과한 상태를 유지하지 않고 selector를 신규 action 범위로 좁혀 대비와 전문 표시를 보정한 뒤 isolated E2E와 screenshot을 다시 생성했다.

대표 5174/5081은 canonical clone과 Persistent UAT process임을 process cwd로 확인했다. 이 runtime을 재시작하거나 branch를 바꾸는 접근은 폐기하고 전용 tmpfs PostgreSQL과 임의 port의 isolated full-stack runner를 사용했다.

## 17. 사용자 검수 결과와 남은 항목

자동 검증과 visual QA는 완료됐고 사용자 직접 screenshot 검수는 대기 중이다. A2 업무 화면 확대, CI, push, PR, merge, Persistent UAT와 대표 저장소 반영은 포함하지 않았다. `main` merge 승인 횟수는 `0/3`이다.

## 18. 5종 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | [10장](#10-sop) | 작성 완료 |
| User manual | [11장](#11-user-manual) | 작성 완료 |
| Roadmap update | [Product Roadmap](../docs/00-product-roadmap.md) | experiment A1 상태 반영 완료 |
| User validation checklist | [12장](#12-user-validation-checklist) | 자동 검증 완료 / 사용자 검수 대기 |
