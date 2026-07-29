# TASK-WORKFLOW-CONTINUITY-001 Change 004 구현 보고서

- taskType: `BUGFIX`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `23eed3b1a2f21c5450e0c56a5479c5f5ecf9b05e`
- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- instructionChainRead: `true`
- fableInvocationCount: `0`
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- mainMergeApprovalCount: `0/3`
- persistentUatApplied: `false`
- actualProviderCallCount: `0`

## 1. 사용자 수정필요사항별 완료 결과

1. **발주 수량은 구매팀 입력으로 고정**
   - 구매 화면의 발주 수량·단위를 필수 업무로 명시하고 구매 workflow 완료 조건에도 포함했다.
   - 자재 도착 화면과 API에서 발주 수량·단위 입력을 제거했다. 구매팀 값이 없으면 자재 담당자가 대신 채우지 못하고 구매 탭에서 먼저 입력하라는 정확한 안내를 받는다.
2. **구매품 저장 직후 자재 담당자에게 실제 자동 등록**
   - 프로젝트 자재 정·부 담당자를 우선하고, 지정이 없으면 관리자·조회전용을 제외한 자재 역할 사용자를 선택한다.
   - 구매 신규·변경 transaction은 실제 수신자가 없으면 성공으로 끝내지 않는다. 유효 수신자가 있으면 해당 자재 사용자의 내 업무와 인앱 알림을 함께 생성한다.
3. **품질검사 전체 흐름과 이전 누락 도착분 복구**
   - 도착 등록 전에 IQC 수신 가능 여부를 확인하고, receipt·IQC 회차·품질 내 업무·알림을 한 흐름으로 만든다.
   - 품질 검사함의 기존 누락분 복구가 일시 실패해도 현재 검사 목록은 계속 열리며 별도 경고를 표시한다.
   - 신규 도착, 이전 누락 도착분 reconciliation, IQC 시작·체크리스트·사진·합격/Pending·조치·재검사·합격·PDF까지 자동 검증했다.

## 2. 해결한 업무 문제와 Root Finding

| Finding | 심각도 | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `ARRIVAL-PURCHASE-QUANTITY-OWNERSHIP` | P1 | `RESOLVED` | 첫 도착 화면이 자재 담당자에게 발주 수량까지 입력시켜 구매·자재 책임과 원본 수량이 섞였다. | 도착 contract와 UI에서 발주 수량을 제거하고 구매 입력 전 도착을 차단했다. |
| `MATERIALS-FALLBACK-SELECTED-ADMIN` | P1 | `RESOLVED` | permission fallback이 사용자 ID순 첫 계정인 `dev-admin`을 골라 자재 내 업무가 생성돼도 실제 자재 사용자에게 보이지 않았다. | 관리자·조회전용을 제외하고 `materials` 역할을 최우선 선택하도록 수정했다. |
| `STALE-REPRESENTATIVE-RUNTIME` | P1 | `RESOLVED` | 사용자가 보던 5174/5081 process는 이 experiment worktree가 아니라 대표 저장소에서 7월 14~15일에 시작된 실행본이었다. Change 003의 IQC reconciliation API도 그 backend에 없었다. | 대표 runtime을 수정·재시작하지 않고 disposable experiment Full-Stack에서 현재 branch 전체 흐름을 검증했다. 운영/대표 runtime 전환은 별도 승인 경계로 유지한다. |
| `IQC-RECOVERY-COUPLED-TO-QUEUE` | P2 | `RESOLVED` | 누락분 reconciliation 실패가 품질 검사함의 현재 목록 조회까지 막을 수 있었다. | 복구 실패를 경고로 분리하고 현재 queue 조회는 계속 수행한다. |

## 3. 기술적 결정과 검토한 대안

- 구매·자재가 같은 수량을 각각 입력하는 방식은 채택하지 않았다. `project_procurement_items.order_quantity/order_unit`을 구매팀이 관리하는 단일 원본으로 유지했다.
- 수신자가 없을 때 조용히 저장하는 방식은 채택하지 않았다. 사용자에게 담당자 지정 방법을 안내하고 구매 transaction을 실패시켜 거짓 성공을 차단했다.
- IQC를 나중에 비동기로 만들고 도착만 먼저 저장하는 방식은 현재 범위에서 채택하지 않았다. 기존 DB transaction과 idempotency contract를 유지해 receipt와 IQC 인계를 함께 보장했다.
- DB schema·migration은 변경하지 않았다. Excel/PDF contract 중 IQC PDF는 회귀 검증만 했으며 파일 형식 변경은 없다.

## 4. 주요 변경 파일

- Backend: `MaterialsContracts.cs`, `MaterialsStore.cs`, `ProcurementStore.cs`, `WorkflowStore.cs`
- Backend test: `ProcurementApiTests.cs`, `ProductionPlanningApiTests.cs`
- Frontend: `App.tsx`, `MaterialsWorkspace.tsx`, `materials.ts`
- Frontend test: `App.test.tsx`
- Full-Stack: `workflow-continuity-change-004.full-stack.spec.ts`, `workflow-continuity.full-stack.spec.ts`, `iqc-digital-report.full-stack.spec.ts`
- Governance: Change 004 계약, 이 보고서, Product Roadmap, 실험 완료 원장

## 5. 자동 검증 결과

- Backend Release build: 경고 `0`, 오류 `0`
- Backend 전체 isolated PostgreSQL: `414/414` 통과
- Frontend typecheck·production build: 통과
- Frontend lint: error `0`, 기존 `main.tsx` Fast Refresh warning `1`
- Frontend unit: `115/115` 통과
- Full-Stack 핵심 묶음: Change 004, workflow continuity, procurement trace, IQC 디지털 성적서 desktop/mobile `4/4` 통과
- 최초 전체 Backend 회귀에서 구형 생산관리 fixture 1건이 발주 수량 없는 완료를 기대해 실패했다. 구매팀 입력을 포함하도록 고친 뒤 해당 test `1/1`, 전체 `414/414`로 재검증했다.
- 최초 IQC E2E는 현재의 project-first 품질 화면이 아니라 폐기된 단계 navigation을 기다려 timeout이 났다. 실제 사용자 경로인 프로젝트 선택으로 검수 계약을 갱신한 뒤 `1/1` 통과했다.
- 모든 Full-Stack과 Backend test는 synthetic data, 임시 PostgreSQL, 외부 provider 비활성 상태에서 실행 후 자동 정리했다.

## 6. 시각 증빙

Repository에는 복사하지 않고 `/tmp/workflow-continuity-change-004-screenshots/`에 privacy-safe synthetic 화면을 생성했다.

- `01-procurement-owned-quantity-desktop.png`
- `02-materials-my-work-after-purchase-desktop.png`
- `03-materials-notification-after-purchase-desktop.png`
- `04-material-arrival-only-desktop.png`
- `05-quality-iqc-current-and-recovered-desktop.png`
- `06-quality-iqc-current-and-recovered-mobile-390.png`

## 7. SOP — 실제 담당자 사용 절차

1. 구매 담당자가 구매 탭에서 품목, 발주 수량과 단위를 저장한다.
2. 자재 담당자는 자동 생성된 인앱 알림 또는 내 업무의 `구매품 신규/변경 확인`을 연다.
3. 자재 담당자는 발주 수량을 다시 입력하지 않고 실제 도착 수량·도착일·비고만 등록한다.
4. 품질 담당자는 자동 생성된 IQC 알림·내 업무에서 프로젝트의 IQC 검사함으로 이동해 검사한다.
5. 부적합이면 Pending 조치·재검사 업무가 이어지고, 재검사 합격으로 검사와 workflow가 종결된다.

## 8. User manual — 오류와 복구 안내

- 자재 화면의 `구매팀 입력이 필요합니다`는 구매 탭에 발주 수량 또는 단위가 없다는 뜻이다. 자재 화면에서는 고칠 수 없다.
- 구매 저장 시 `자재 담당자가 없어 구매품을 인계할 수 없습니다`가 나오면 생산관리에서 자재 정·부 담당자를 지정한다.
- 품질 검사함에서 누락분 자동 복구 경고가 보여도 현재 검사 목록은 계속 사용할 수 있다. 재진입 시 동일 receipt는 중복 없이 다시 대조된다.

## 9. 사용자 검수 체크리스트

상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

- [ ] 구매 담당자가 발주 수량·단위를 저장하고 자재 담당자는 해당 값을 수정할 수 없는지 확인
- [ ] 구매품 신규·변경 직후 자재 정·부 담당자의 내 업무와 알림 확인
- [ ] 자재 도착 화면에 발주 수량 입력이 없고 도착 수량만 있는지 확인
- [ ] 도착 등록 직후 품질 정·부 담당자의 IQC 내 업무·알림·검사함 확인
- [ ] 이전 누락 도착분이 프로젝트 IQC 검사함에 한 번만 복구되는지 확인
- [ ] IQC 합격과 Pending→조치 완료→재검사→합격, 사진·PDF 확인
- [ ] 모바일 390px에서 IQC 카드가 잘리지 않는지 확인

## 10. 개인정보·secret, 잔여 위험과 게시 경계

- synthetic project와 `dev-*` 역할 계정만 사용했다. 실제 고객·사용자 정보, token, secret, provider payload는 기록하지 않았다.
- Open P0/P1/P2: `0/0/0`. P3 backlog는 대형 `App.tsx` 분리, production bundle code-splitting, 기존 Fast Refresh warning이다.
- 현재 5174/5081 대표 runtime은 이 branch 결과가 아니므로 이번 검증 URL로 보고하지 않는다. 대표 runtime 교체는 이 Task 범위 밖이다.
- experiment local commit만 승인됐다. push·PR·merge, 대표 repo·GitHub `main`, Persistent UAT, 실제 Teams/Mail provider는 미승인·미적용이다. `main` merge 승인은 `0/3`이다.

## 11. Rollback과 5종 종료 산출물

DB migration이 없으므로 experiment local commit을 revert하면 code·test·문서가 함께 되돌아간다. 이미 생성된 구매·도착·IQC·업무·알림은 기존 schema와 idempotency key를 사용하므로 schema rollback은 없다. 운영 승격 뒤 문제가 생기면 기존 기록은 보존하고 수신자 projection과 화면을 forward-fix한다.

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 전체 |
| SOP | 완료 | 이 문서 `7. SOP` |
| User manual | 완료 | 이 문서 `8. User manual` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` Task row·Decision Log |
| User validation checklist | 작성·자동 검증 완료, 사용자 검수 대기 | 이 문서 `9. 사용자 검수 체크리스트` |
