# TASK-UX-001 A2 Action Feedback 확대 Implementation Report

## 1. 상태와 기준선

| 항목 | 결과 |
| --- | --- |
| Canonical Task | `TASK-UX-001`, A2 slice |
| Task 유형 | `NEW_FEATURE` → experiment Fable 2-pass fast-track 구현 |
| Branch | `experiment/task-home-002-personalized-shell` |
| 시작 HEAD | `4c44a9c29eb660052e871e9a45d746a3a19d3a85` |
| 최종 구현 source | [Fable 2차 기획](../docs/31-action-feedback-a2-plan.md) |
| 자동 검증 | 완료 |
| 사용자 검수 | `사용자 검수 대기 — 마지막 일괄 검수` |
| Local commit | 이 보고서를 포함한 experiment local commit |
| Push / PR / Merge | 미승인·미실행 |
| `main` merge 승인 | `0/3` |
| Persistent UAT / 실제 provider | 미승인·미변경 |

이 문서는 experiment 코드와 isolated synthetic 검증 결과만 기록한다. 대표 repo, `main`, Persistent UAT, 실제 provider 또는 사용자 직접 검수를 완료로 표현하지 않는다.

## 2. 해결한 업무 문제와 범위

A1의 구조화 feedback이 내 업무·알림에만 적용되어 생산계획·구매·자재·IQC·키팅·패널·Excel에서는 처리 중·성공·부분 성공·실패를 문자열로 추측하거나, 편집 화면을 벗어난 뒤 저장 결과를 잃거나, mutation 성공 뒤 refresh 실패를 전체 실패로 오인할 수 있었다.

포함 범위는 기존 A1 hook/component 재사용, 생산계획·구매·패널 편집과 Excel dialog·양식 다운로드, 자재 도착·입고 확정·IQC·키팅, IQC 성적서, 공통 선택 Excel export, field 오류 focus와 설명 연결, post-mutation preserve refresh·generation guard, desktop·390px synthetic browser 증빙이다.

제외 범위는 Backend/API/DB/migration/권한·업무 상태 변경, 전역 toast/store·query framework, A2 밖 화면의 문자열 tone 전수 정리, 실제 Excel 앱 제어, 대표 repo·`main`·Persistent UAT·provider·push·PR·merge다.

## 3. Fable 5 기획과 Claude 사용량

Fable은 `scripts/run-fable-readonly.sh`의 read-only 경계에서 1차 planning과 review 기반 2차 planning에만 사용했다. 2차 문서의 `openBlockingDecisionCount`는 `0`이며 구현 source로 채택했다.

| 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable | 초기화 |
| --- | --- | --- | --- | --- |
| 1차 직전 | 사용 0% / 잔여 100% | 사용 14% / 잔여 86% | 사용 28% / 잔여 72% | session 05:49 KST, 전체 07-25 07:59 KST, Fable parse 불가 |
| 1차 직후 | 사용 0% / 잔여 100% | 사용 14% / 잔여 86% | 사용 28% / 잔여 72% | 동일 |
| 2차 직전 | 사용 8% / 잔여 92% | 사용 29% / 잔여 71% | 사용 14% / 잔여 86% | 동일 |
| 2차 직후 | 사용 8% / 잔여 92% | 사용 29% / 잔여 71% | 사용 20% / 잔여 80% | 동일 |

1차 호출은 328초, 2차 호출은 185초였고 2차는 같은 Task session을 재사용했다. 측정값이 감소한 구간도 `/usage`에 표시된 실제 값을 그대로 기록했다.
구현·검증 종료 후 cleanup은 `FABLE_TASK_SESSION_CLEANED`로 Task 전용 session·transcript 각 1개를 제거했다.

## 4. 아키텍처와 주요 결정

- `useActionFeedback`의 기존 scope·busy ref·conflict·partial 계약을 유지하고 필요한 화면에서만 `setFeedback`을 공개했다. DOM focus 책임은 hook이 아닌 form helper에 유지했다.
- 같은 tick 중복 Excel 요청은 state보다 먼저 갱신되는 ref fence로 차단한다. 서버 파일 생성과 브라우저 다운로드 trigger를 분리해 후자만 실패하면 `partial`로 표시하고 object URL은 항상 해제한다.
- 생산계획·구매·패널 편집 성공은 부모 callback으로 프로젝트 상세의 contextual feedback에 보존한다. success는 focus를 강제하지 않고 error/partial만 안정된 feedback anchor에 focus한다.
- `FormErrorSummary`는 최초 유효 오류 field로 이동하고 `FieldErrorMessage`는 `aria-invalid`·`aria-describedby`를 연결한다. field마다 `role=alert`를 반복하지 않는다.
- 자재·IQC·키팅은 scope별 action 잠금과 post-mutation `load(true)`를 사용한다. 오래된 응답은 generation 번호로 폐기하며 refresh만 실패하면 mutation을 되돌리지 않고 `partial`로 남긴다.
- 모바일 전체 버튼은 최소 44px touch target을 유지하되 카드·글자·간격은 compact token으로 유지했다.

## 5. 영향 범위

| 영역 | 영향 |
| --- | --- |
| Frontend | 공통 feedback, 3개 편집기·3개 Excel dialog, 자재/IQC/키팅, IQC 성적서, 선택 export, adaptive style·tests |
| Backend / API | 변경 없음. 기존 status·validation 계약 사용 |
| DB / Migration | 변경 없음 |
| 권한 / Workflow | 변경 없음. 조회/입력 권한 계약 유지 |
| Excel | 생성과 client trigger 단계 분리, 422/429 안내 보존, object URL cleanup |
| PDF | IQC PDF 재생성 feedback만 구조화, PDF 계약 변경 없음 |
| 첨부파일 | IQC 사진 action feedback만 구조화, storage 계약 변경 없음 |
| 외부 provider | 변경·호출 없음 |

## 6. 실제 변경 파일

| 역할 | 파일 |
| --- | --- |
| Fast-track source | `tasks/ux-001-a2-interview.md`, `ux-001-a2-planning.md`, `ux-001-a2-review.md`, `ux-001-a2-change-001.md`, `docs/31-action-feedback-a2-plan.md` |
| 공통 contract·선택 export | `frontend/src/useActionFeedback.ts`, `frontend/src/ExcelExportAction.tsx` |
| 생산·구매·패널·복귀 feedback·field 오류 | `frontend/src/App.tsx` |
| 자재·IQC·키팅 | `frontend/src/MaterialsWorkspace.tsx`, `IqcReportWorkspace.tsx`, `PanelKittingPage.tsx` |
| Adaptive style | `frontend/src/styles.css` |
| Unit/integration | `frontend/tests/App.test.tsx`, `ExcelExportAction.test.tsx`, `PanelKittingPage.test.tsx` |
| Browser E2E | `frontend/e2e/mock-ui/ux-a2-feedback-smoke.spec.ts`, `panel-kitting-smoke.spec.ts`, `frontend/e2e/full-stack/home-dashboard.full-stack.spec.ts` |
| Visual evidence | `tasks/ux-001-a2-screenshots/*.jpg` 5개 |
| 상태 문서 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md`, 이 보고서 |

## 7. 실행한 검증과 결과

| 검증 | 결과 | 근거 |
| --- | --- | --- |
| Frontend lint | PASS — error 0, 기존 `main.tsx` Fast Refresh warning 1 | `npm run lint` |
| Frontend typecheck | PASS | `npm run typecheck` |
| Frontend unit/integration | PASS — 12 files, `104/104` | `npm test -- --run` |
| Frontend production build | PASS — 기존 500kB chunk warning 유지 | `npm run build` |
| Mock browser E2E | PASS — `3/3` | panel kitting, project registration, UX-A2 visual |
| Full-Stack E2E | PASS — 최종 `38/38` | isolated PostgreSQL Compose project·tmpfs, provider disabled |
| 회귀 실패 보정 | PASS — 중간 `34/38`의 HOME 조회 기대값 1건·44px touch target 3건을 새 계약에 맞춰 수정 후 targeted `5/5` 및 전체 재실행 | Full-Stack E2E |
| Visual QA | PASS — desktop 4장, mobile 1장 | synthetic panel editor/error/Excel error/kitting success |
| Mobile overflow | PASS — 390px page-level overflow 0 | mock E2E assertion |
| Backend unit / migration | `N/A` | Backend/API/DB/migration diff 없음 |
| CI | `N/A` | push·PR 미승인 local experiment |
| Persistent UAT | `N/A` | 명시적 제외, 대표 runtime·DB 미변경 |

## 8. Finding과 resolution

| ID | Severity | 상태 | 원인·영향과 해소 |
| --- | --- | --- | --- |
| `UX-A2-FOCUS-RESPONSIBILITY` | P1 | `RESOLVED` | form focus를 hook에 넣지 않고 App form helper에 유지 |
| `UX-A2-RETURN-FEEDBACK-LOSS` | P1 | `RESOLVED` | 편집기 이탈 후 성공 유실을 부모 one-shot contextual feedback으로 보존 |
| `UX-A2-EXPORT-STAGE-AMBIGUITY` | P1 | `RESOLVED` | blob 생성과 client trigger 분리, partial·URL revoke·ref fence 적용 |
| `UX-A2-GENERATION-SCOPE` | P2 | `RESOLVED` | 자재/IQC post-mutation preserve refresh에만 generation guard 적용 |
| `UX-A2-LIVE-DUPLICATION` | P2 | `RESOLVED` | 요약 alert 1개와 field description 분리 |
| `HOME-READ-SCOPE-REGRESSION` | P2 | `RESOLVED` | 과거 E2E가 부서 밖 Pending 숨김을 기대 → 전 부서 조회 계약으로 갱신 |
| `MOBILE-TOUCH-TARGET-REGRESSION` | P2 | `RESOLVED` | compact shell이 button 36px로 축소 → 시각 밀도는 유지하고 mobile min-height 44px 복구 |
| `DESIGN001-F01` | P3 | `BACKLOG` | 기존 production chunk 500kB warning, bundle splitting 후속 |
| `DESIGN001-F02` | P3 | `BACKLOG` | 기존 `main.tsx` Fast Refresh warning 1건 |

Open P0/P1/P2는 `0/0/0`이다.

## 9. 개인정보·secret과 artifact

화면·E2E는 합성 역할·프로젝트·패널만 사용했다. 실제 사용자, 회사 계정, 고객·프로젝트 원문, credential, token, raw DB/API body는 tracked 문서에 기록하지 않았다. 대표 UAT 화면은 촬영하지 않았다. dependency, migration, env, certificate, build output와 실패 시 Playwright artifact는 staging하지 않는다.

## 10. SOP

1. action 버튼을 누르면 버튼 인접 영역의 파란 처리 중 상태를 확인하고 같은 버튼을 반복 클릭하지 않는다.
2. 초록 성공은 저장과 최신 화면 확인이 끝난 상태다. 편집 화면에서 돌아온 경우 프로젝트 상세 위 결과를 확인하고 `확인`으로 닫는다.
3. 노란 부분 성공은 업무 mutation은 끝났지만 최신 목록 또는 다운로드 시작만 실패한 상태다. mutation을 반복하지 말고 목록 새로고침 또는 다운로드만 다시 시도한다.
4. 빨간 오류는 안내된 권한·최신 상태·연결 상태를 확인한다. 입력 오류면 자동 이동한 첫 field부터 수정한다.
5. 자재·IQC·키팅의 같은 대상 또는 충돌하는 bulk/row action은 처리 종료 전까지 잠긴다.
6. 장애 시 DB를 직접 고치지 않는다. 403은 권한 확인, 404/409는 최신 목록 확인, 422는 입력/선택 조건 보정, 429는 잠시 후 재시도한다.

## 11. User manual

- 색상 의미: 파랑 처리 중, 초록 완료, 노랑 처리 완료·화면 갱신/다운로드 시작 미완료, 빨강 처리 실패.
- Excel의 노란 안내는 파일 생성 자체는 성공한 상태이므로 원본 업무를 다시 저장하지 않는다.
- 편집기 저장 후 프로젝트 상세 상단의 결과는 다른 페이지로 이동하거나 `확인`을 누르기 전까지 남는다.
- 모바일은 좌측 상단 메뉴 버튼으로 전체 업무를 열고, 모든 부서는 운영 메뉴를 조회할 수 있다. 입력 버튼은 기존 담당 권한이 없으면 표시되지 않거나 비활성이다.
- validation 오류는 첫 오류 입력으로 이동하며 각 입력에 오류 설명이 연결된다.

## 12. User validation checklist

상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

### 자동 검증 완료

- [x] 생산계획·구매·패널 편집 성공이 복귀 후 contextual feedback에 남음
- [x] 자재·IQC·키팅의 중복 submit 차단, refresh partial과 stale 응답 차단
- [x] Excel 422·429 안내, client trigger partial, object URL revoke
- [x] field summary에서 첫 오류 focus, `aria-invalid`·`aria-describedby`
- [x] desktop·390px synthetic 화면과 page overflow 0
- [x] 전 부서 운영 메뉴 조회와 기존 mutation 권한 분리
- [x] 대표 repo·`main`·Persistent UAT·provider 미변경

### 사용자 직접 확인 대기

- [ ] 데스크톱 패널 입력에서 오류 안내가 입력 목록과 자연스럽게 연결되는지 확인
- [ ] Excel dialog의 빨간 안내가 다음 행동을 이해하기 쉬운지 확인
- [ ] 모바일 390px의 카드 밀도와 44px 터치 영역이 편한지 확인
- [ ] 키팅 성공 안내가 완료 패널·남은 패널과 함께 이해되는지 확인
- [ ] 생산계획·구매·자재·IQC의 실제 업무 문구가 현업 용어와 맞는지 마지막 일괄 확인

## 13. Rollback과 복구

대표 repo와 `main`에는 변경이 없어 원본 rollback은 없다. 실험 폐기 시 이 branch를 merge하지 않는다. 코드 단위 취소는 이 local experiment commit을 별도 승인 아래 revert한다. Backend/DB/migration/provider 보상은 필요하지 않다.

## 14. 기술적 결정과 대안

전역 toast/store 대신 기존 A1 scope hook을 확대해 화면 맥락과 접근성을 유지했다. query framework 도입, form 전체 재설계, 20개 export 화면 반복 구현은 비용과 범위 팽창 때문에 제외했다. field focus는 공통 hook에 DOM 의존을 추가하지 않고 form layer에서 처리했다.

## 15. 시행착오 및 폐기한 접근

초기 전체 Full-Stack에서는 과거 “부서 밖 Pending 숨김” assertion과 compact HOME가 만든 36px 버튼 때문에 4건이 실패했다. 기능을 되돌리지 않고 사용자 최신 요구인 전 부서 조회를 E2E 계약에 반영하고, compact 시각 크기는 유지하면서 touch target만 44px로 복구했다. 대표 5174는 Entra 로그인, 기존 5081은 DB schema 비호환 상태여서 검증에 사용하지 않고 isolated mock/full-stack runtime만 사용했다.

## 16. 사용자 검수 결과와 남은 항목

자동·visual 검증은 완료했고 사용자 직접 검수는 마지막 일괄 검수로 남았다. 전역 toast/store, A2 밖 잔여 문자열 tone 정리, CI, push, PR, merge, 대표 repo, Persistent UAT와 실제 provider는 범위 밖이다. 다음 optional Roadmap 범위는 사용자가 원할 때만 시작하는 `TASK-EXPORT-001` column picker다.

## 17. 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | [10장](#10-sop) | 작성 완료 |
| User manual | [11장](#11-user-manual) | 작성 완료 |
| Roadmap update | [Product Roadmap](../docs/00-product-roadmap.md), [실험 완료 원장](../docs/27-experiment-task-ledger.md) | A2 완료 반영 |
| User validation checklist | [12장](#12-user-validation-checklist) | 자동 검증 완료 / 사용자 검수 대기 |
