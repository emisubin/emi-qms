# TASK-OSAN-PROJECT-001 Change 004 — 청주 화면 실물 기준 재사용 보정

## 1. 승인·Gate·기준선

- taskType: `BUGFIX`
- changeStatus: `IMPLEMENTED_AWAITING_USER_VALIDATION`
- instructionChainRead: true
- instructionConflictCount: 0
- taskIdentityGate: `PASS_REUSE`
- canonicalTaskId: `TASK-OSAN-PROJECT-001`
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- roadmapOverrideSource: `USER_EXPLICIT_VISUAL_PARITY_CORRECTION_2026-09-07`
- implementationApproved: true
- implementationApprovalSource: `USER_EXPLICIT_VISUAL_PARITY_CORRECTION_2026-09-07`
- localCommitApproved: true
- localCommitPolicySource: `USER_STANDING_INSTRUCTION_2026-09-07`
- persistentUatMutationApproved: false
- providerMutationApproved: false
- gitPublicationApproved: false
- planningOwner: `GPT_6_ASTRA_HIGH`
- implementationOwnerRequested: `GPT_5_6_SOL_XHIGH`
- verificationOwnerRequested: `FRESH_GPT_6_ASTRA_HIGH`
- taskBranch: `feat/task-osan-project-001-project-registration`
- baselineSha: `2292810a6d18b67f61dcac53ef87b9a923f60fc6`

사용자가 실제 청주와 오산 화면을 눈으로 다시 비교하고 같게 수정하며, 차이가 발생한 원인을 분석해 재발을 막으라고 지시했다. 현재 화면 대조 결과 Change 003은 공통 class와 유사 markup을 사용했지만 전체 목록 구조와 실제 공용 표시 컴포넌트를 재사용하지 않아 화면 구성이 달랐다.

## 2. Purpose identity

- 업무 목표: 오산 프로젝트 목록·상세·진행 현황을 청주 프로젝트 화면의 실제 표시 컴포넌트와 시각 규칙으로 렌더링한다.
- Root Finding: Change 003은 class 이름·단위 테스트 통과를 디자인 동일성의 대리 지표로 사용했고, 실제 청주·오산 동시 화면 비교를 완료 gate로 두지 않았다.
- 변경 경계: Frontend presentation, 공용 표시 컴포넌트 추출·재사용, 관련 component/browser visual contract, Task 기록.
- 보존할 불변조건: 오산 API·DB·권한·등록 양식·7단계 데이터는 유지한다. `진행 관리` 단일 탭을 유지하며 Pending·중단·보류·취소와 청주 전용 업무 mutation은 추가하지 않는다.
- 예상 산출물: 두 사업부가 같은 공용 목록 행·모바일 카드·상세 요약·부서 현황 컴포넌트를 사용하는 코드, 동일 viewport 비교 screenshot, 재발 방지 검증.

## 3. 구현 방향

1. 청주와 오산에 복제된 목록 desktop/mobile markup을 하나의 공용 표시 컴포넌트로 통합한다. 업무 명칭과 값만 adapter로 전달한다.
2. 상세 기본정보와 제조/진행 현황도 실제 공용 표시 컴포넌트를 사용하도록 통합한다. 오산 전용 디자인 CSS를 만들지 않는다.
3. 사용 가능한 기능 차이는 명시적 option으로만 표현한다. 오산에는 Pending·중단·보류·취소·청주 전용 mutation을 표시하지 않는다.
4. 1440px desktop과 390px mobile에서 청주·오산 화면을 같은 실행과 같은 확대율로 캡처한다. 공용 영역의 DOM/class, 주요 computed style, 열·카드 순서와 실제 screenshot을 함께 검토한다.
5. 코드의 class 문자열 일치만으로 시각 동등성을 판정하지 않는다. 쌍으로 된 screenshot이 없으면 완료로 기록하지 않는다.

## 4. 초기 exact allowlist

- `frontend/src/App.tsx`
- `frontend/src/styles.css` (공용화에 꼭 필요한 경우만)
- `frontend/tests/OsanProjectRegistration.test.tsx`
- `frontend/tests/App.test.tsx`
- `frontend/e2e/mock-ui/osan-project-registration.spec.ts`
- `frontend/AGENTS.md`
- `tasks/osan-project-001-change-004.md`
- `tasks/osan-project-001-implementation-report.md`
- `tasks/osan-project-001.md`
- `docs/00-product-roadmap.md`

구현자가 product/test 파일을 수정하고 Parent가 Frontend 지침과 Task 기록을 갱신한다. Backend, API, database, migration, dependency, lockfile, 실제 provider와 Persistent UAT는 변경하지 않는다.

## 5. 완료 조건

- 청주와 오산이 목록 desktop/mobile과 상세 summary/status board의 같은 React 표시 컴포넌트를 직접 사용한다.
- 청주 화면의 기존 결과와 상호작용은 회귀하지 않는다.
- 오산의 승인된 명칭·필드·단일 탭·7단계 계약은 유지되고 금지 기능은 나타나지 않는다.
- 같은 viewport의 청주·오산 screenshot을 Parent가 눈으로 비교해 공용 영역의 간격, 테두리, 표·카드 구조와 반응형 전환이 같음을 확인한다.
- component test, 관련 청주 회귀, 전체 Frontend test, lint, typecheck, build와 mock browser 검증을 통과한다.
- Parent 실제 diff review와 fresh GPT-6 High 독립 검증에서 open P0/P1/P2가 0이다.

## 6. 게시 경계

검증과 review를 통과한 exact allowlist는 별도 질문 없이 local commit한다. Push, PR, merge, `main`, Persistent UAT와 실제 provider는 승인 범위 밖이다.

## 7. 구현 결과

- 청주와 오산의 desktop/mobile 목록을 `ProjectListPresentation`, 상세 요약을 `ProjectSummaryPresentation`, 부서 현황을 `ProjectDepartmentStatusBoard`로 통합했다. 두 화면은 비슷한 markup을 따로 유지하지 않고 같은 React 표시 컴포넌트를 직접 호출한다.
- 프로젝트명·코드·거래처·제품명·수량·납기일·상태·진행률과 허용 action만 adapter·option으로 전달한다. 청주의 선택·내보내기·다중 부서 tab·업무 action은 보존하고, 오산은 승인된 단일 `진행 관리` tab과 읽기 전용 현황만 유지한다.
- 오산 현황 행은 실제 action이 없으므로 desktop `div`, mobile `article`로 렌더링해 클릭·hover·focus affordance를 제거했다. 청주 현황 행은 기존 `button`과 keyboard/click 동작을 유지한다.
- 공용 목록 행 최소 높이를 60px로 고정하고 header/body의 공통 8개 열 기준선을 맞췄다. 청주의 실제 선택 checkbox는 별도 위치를 유지하며 오산에 가짜 checkbox 공간이나 기능을 추가하지 않았다.
- 프로젝트 코드 표시는 공용 `.project-code-value`로 통합하고 대소문자와 내부 연속 공백 보존 계약을 유지했다.
- `frontend/AGENTS.md`에 기존 화면과 같게 만드는 변경의 완료 gate를 추가했다. 실제 공용 컴포넌트 재사용, 의도된 기능 차이 사전 기록, 같은 실행·viewport·확대율의 양쪽 screenshot과 사람의 시각 비교를 요구한다. 이 규칙은 품질 검증을 강화하며 변경·게시 권한을 넓히지 않는다.

## 8. 차이가 발생한 원인과 재발 방지

Change 003은 공통 class와 유사한 DOM을 각각의 청주·오산 render branch에 복제했다. Component test도 class 존재와 text 같은 대리 지표만 확인했고 browser test는 오산 화면만 캡처했다. 이 상태에서 코드 모양이 비슷하다는 이유로 실제 화면까지 같다고 판단했다.

이번 같은 실행의 양쪽 비교가 다음 차이를 드러냈다.

- 현황 행의 native element가 청주는 `button`, 오산은 별도 구조여서 radius와 높이가 달랐다.
- desktop 목록 행이 청주 약 60px, 오산 약 42px로 달랐다.
- header와 body의 열 시작점이 2px 어긋났다.
- 최초 비교 증빙은 한쪽 상세만 펼쳐진 상태여서 같은 상태의 화면을 비교하지 못했다.

재발 방지를 위해 표시 구현을 공용 컴포넌트 하나로 만들고, browser test가 1440×900과 390×844에서 양쪽을 같은 run으로 캡처하도록 바꿨다. DOM contract, computed style, 행 높이, 열 위치와 header/body 정렬을 비교하고, Parent와 fresh verifier가 8개 screenshot을 직접 눈으로 확인했다. 승인된 기능 차이 때문에 전체 page section 수는 다르므로 공용 표시 영역의 동일성과 의도된 업무 차이를 구분해 보고한다.

## 9. 검증 결과

- Product/test reviewed digest: `243ce46643c51fd6d4f69300516fd84cacf6527a8c194ee936df7e9d2d41bc9c`
- 집중 component: `2 files / 97/97 PASS`
- Frontend 전체: `36 files / 292/292 PASS`
- Lint: 오류 0, 기존 `frontend/src/main.tsx` Fast Refresh warning 1
- Typecheck: `PASS`
- Production build: `PASS`, 기존 chunk-size warning만 유지
- Paired production-preview Chromium: `1/1 PASS`
- Browser 관찰: console error 0, request failure 0, unexpected mock request 0, page horizontal overflow 0
- Privacy-safe paired evidence: desktop 1440×900·mobile 390×844의 청주/오산 목록·상세 8개, ignored test output에 보관하고 Git에는 포함하지 않음
- Parent visual review: 공용 목록 행·카드, 상세 요약, 현황 표·카드의 간격·테두리·구조·반응형 전환 일치 확인
- Fresh verifier requested: `GPT_6_ASTRA_HIGH`
- Fresh verifier observed: `NOT_REPORTED`
- Fresh verifier result: `PASS / GO`
- Final open findings: `P0 0 / P1 0 / P2 0 / P3 0`
- `git diff --check`: `PASS`

## 10. 검증 중 해소한 Finding

| Finding | 심각도 | 상태 | 해소 |
| --- | --- | --- | --- |
| `OSAN-UI-PARITY-01` | P2 | `RESOLVED` | Action이 없는 오산 현황을 native button으로 표시하던 구조를 비상호작용 `div`/`article`로 바꾸고 hover·focus·pointer가 없음을 검증했다. |
| `OSAN-UI-PARITY-02` | P2 | `RESOLVED` | 청주·오산 목록 행 높이와 header/body 열 위치가 달랐다. 공용 60px 행과 열 geometry를 적용하고 paired browser assertion을 추가했다. |
| `OSAN-UI-PARITY-03` | P3 | `RESOLVED` | 공용화 중 청주 panelName null fallback이 달라졌다. Desktop code fallback과 mobile `패널명 미입력`을 복원하고 양쪽 회귀를 검증했다. |
| `OSAN-UI-EVIDENCE-01` | P3 | `RESOLVED` | 첫 paired screenshot에서 오산 상세만 펼쳐져 있었다. 양쪽 `details.open=false`를 assert한 뒤 다시 캡처했다. |
| `OSAN-UI-DOC-AUTHORITY-01` | P2 | `RESOLVED` | Frontend 지침 절 자체에 승인 권한 불변을 명시하고 현재 Task 승인 출처·exact allowlist·상위 승인 경계를 그대로 따르도록 보정했다. |
| `OSAN-UI-DOC-SCOPE-01` | P3 | `RESOLVED` | 누적 보고서의 청주 화면 무변경 문장을 Change 001 시점으로 한정하고, Change 004에서는 presentation을 변경했지만 shell·API·업무 interaction을 보존했다고 정정했다. |

Change 004는 Frontend 표시·검증·지침·Task 기록에 한정했다. Backend, API, database, migration, 권한, lockfile, 실제 provider와 Persistent UAT는 변경하지 않았다.
