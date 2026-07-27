# DESIGN-000 Change 002 — Department Input Experience Unification

## Task Identity Gate

- proposedTaskId: `DESIGN-000`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `DESIGN-000`
- roadmapNextGate: `DESIGN-000 CHANGE`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `DESIGN-000`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `DESIGN-000 Change 002`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 부서별로 서로 다른 입력 시작·필드 묶음·선택·저장 방식을 하나의 간단한 입력 경험으로 통일한다.
- Root Finding 또는 정책 결정: 같은 제품 안에서 수정 진입 위치, 입력 순서, 선택 제어, 저장·취소 위치와 완료 문구가 달라 사용자가 화면마다 사용법을 다시 익혀야 한다.
- 변경·검증 경계: 영업, 생산관리, 설계, 구매, 자재, 제조, 품질, 물류, 정산의 기존 입력 UI를 공통 입력 패턴으로 재배치하고 Desktop·390px에서 검증한다.
- 보존할 불변조건: API·DB·권한·업무 상태 전이·필수값·저장 결과·URL·데이터 처리 단위·기존 기능은 변경하지 않는다.
- 예상 산출물: 공통 입력 구성요소와 스타일, 주요 부서 화면 적용, Frontend 검증, Implementation report.

## 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

## 변경 계약

1. 모든 부서 입력은 `대상 선택 → 값 입력/선택 → 저장 결과 확인`의 같은 순서로 읽히게 한다.
2. 입력 영역은 번호, 제목, 짧은 안내를 가진 공통 섹션으로 묶고 필수 여부를 label에서 바로 확인하게 한다.
3. 소수의 상호 배타적 선택은 큰 선택 버튼으로 제공하되 기존 value와 handler를 그대로 사용한다.
4. 주요 저장·취소·뒤로가기 행동은 화면 하단 공통 작업 영역에 모으고 처리 중 중복 실행 차단을 유지한다.
5. 긴 목록·단계·패널·구매품목은 기존 데이터 구조와 처리 단위를 유지하며 해당 행이나 카드 안에서 바로 입력할 수 있게 시각적으로 정리한다.
6. 성공·오류·진행 feedback은 실행한 작업 영역 가까이에 유지하고 기존 `useActionFeedback` 계약을 변경하지 않는다.
7. 흑백, 무그림자, 사각형 기본과 의미 상태색 예외를 유지한다.
8. Desktop과 Mobile 모두 같은 개념 모델을 사용하되 390px에서는 한 열, 큰 터치 영역, 하단 작업 영역으로 재배치한다.

## 제외 범위

- Backend, migration, API contract, permission, notification, workflow와 validation 정책 변경
- 필수 입력값 추가·삭제, enum·상태·문구의 업무 의미 변경
- 기존 페이지·탭·기능 삭제 또는 새로운 제품 기능 추가
- 대표 repo, `main`, push, PR, merge, Persistent UAT

## 변경 allowlist

- `frontend/src/design-system/components.tsx`
- `frontend/src/design-system/index.ts`
- `frontend/src/design-system/wireframe.css`
- `frontend/src/styles.css`
- `frontend/src/App.tsx`
- `frontend/src/MaterialsWorkspace.tsx`
- `frontend/src/ManufacturingPage.tsx`
- `frontend/src/QualityInspectionsPage.tsx`
- `frontend/src/LogisticsPage.tsx`
- `frontend/src/SalesSettlementPage.tsx`
- `frontend/src/SalesBillingRequestPage.tsx`
- 관련 Frontend unit test
- `tasks/design-000-change-002.md`
- `tasks/design-000-change-002-implementation-report.md`
- `docs/00-product-roadmap.md`
- `docs/27-experiment-task-ledger.md`

## 검증 계약

- 공통 구성요소의 접근 가능한 label, keyboard focus와 disabled 상태 unit test
- 기존 부서별 API 호출과 request payload test 회귀
- `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build`
- 실제 Development runtime의 Desktop과 390px 대표 입력 화면에서 구조, page-level overflow, 정상 action feedback, console/request 오류 여부를 privacy-safe projection으로 확인
- `git diff --check`, allowlist, generated artifact, secret·개인정보 검사

## 게시 경계

- 기준 branch: `experiment/task-home-002-personalized-shell`
- 시작 HEAD: `2247643`
- 복구 기준점: `2247643`
- main merge 승인: `0/3`
- 이번 변경은 현재 실험 worktree에만 적용한다.
