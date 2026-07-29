# TASK-WORKFLOW-CONTINUITY-001 Change 003 구현 보고서

- taskType: `P2_REMEDIATION`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `1d83dd2680b71e4c88b2a23462e0c700ab727dac`
- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- instructionChainRead: `true`
- fableInvocationCount: `0`
- fableWaiverSource: 사용자가 신규 기능이 아닌 수정필요사항은 하나의 Task로 Codex가 바로 구현하라고 명시
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- mainMergeApprovalCount: `0/3`
- persistentUatApplied: `false`
- actualProviderCallCount: `0`

## 1. 사용자 수정필요사항별 완료 결과

1. **도급 구매품 입력 오류를 한눈에 확인**
   - 저장 오류를 `공급 유형 · 품목명 · 해당 그룹의 행 번호 · 문제 필드 · 해결 방법`으로 묶은 상단 오류 패널을 추가했다.
   - 오류가 난 행과 입력칸을 붉게 강조하고 첫 오류 입력칸으로 자동 이동한다.
   - 예: 발주 단위 누락이면 `도급 구매품 1번째 행`, 품목명, `발주 단위`, 수량·단위를 함께 입력해야 한다는 해결 방법을 동시에 표시한다.
2. **구매품 신규·변경을 자재 담당자에게 자동 인계**
   - 직접 저장과 Excel 반영 모두 실제 신규/변경 품목마다 자재 정·부 담당자 각각의 내 업무를 생성한다.
   - 정·부 담당자를 recipient로 묶은 인앱 알림도 생성한다. 같은 품목·같은 version 재시도는 중복 생성하지 않고, 실제 다음 변경은 새 인계로 남긴다.
3. **자재 메뉴에서 입고 관리·키팅 먼저 선택**
   - 왼쪽 전역 `자재` 메뉴는 더 이상 입고 화면으로 바로 가지 않는다.
   - 자재 업무 첫 화면에서 `입고 관리` 또는 `패널 키팅`을 먼저 선택하고, 그 다음 프로젝트를 고르게 했다.
4. **도착 등록의 실제 IQC 인계 보장**
   - 도착 저장 transaction에서 receipt, IQC 검사 회차, 품질 정·부 내 업무와 인앱 알림을 함께 생성한다.
   - 화면은 서버 응답의 `iqcAttemptId`와 `IqcRequested`를 확인한 경우에만 성공 메시지를 표시한다. 문구만 성공하고 실제 인계가 없는 상태를 차단했다.
5. **운영 페이지를 프로젝트 우선 구조로 통일**
   - 전역 자재·제조·품질·물류·Pending 메뉴의 첫 화면을 프로젝트 목록으로 통일했다.
   - 사용자는 프로젝트를 선택한 뒤 해당 부서 화면에서 조회·수정한다. 내 업무·알림의 정확한 작업 링크는 중간 선택 없이 바로 해당 프로젝트 업무로 이동한다.
6. **이미 도착했지만 IQC가 누락된 자재 복구**
   - `Arrived`인데 IQC 회차가 없는 유효 도착분을 품질 검사함 진입 시 자동 대조한다.
   - 누락된 검사 회차, 품질 정·부 내 업무와 알림을 idempotent하게 복구하고 현재 프로젝트 IQC 검사함에 표시한다.

## 2. Root Finding과 해소

| Finding | 심각도 | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `PROCUREMENT-VALIDATION-LOCATION-OPAQUE` | P2 | `RESOLVED` | 서버 오류가 화면의 공급 유형·행·필드와 연결되지 않아 사용자가 수정 위치를 추측해야 했다. | 구조화 오류 해석, 행/필드 강조와 focus를 추가했다. |
| `PROCUREMENT-MATERIALS-HANDOFF-MISSING` | P2 | `RESOLVED` | 구매 저장 이후 자재팀 인계 event가 없어 변경 사실을 수동 확인해야 했다. | 신규·변경 version별 정·부 내 업무와 알림을 추가했다. |
| `IQC-HANDOFF-POSTCONDITION-MISSING` | P1 | `RESOLVED` | 화면은 도착 API 성공만으로 IQC 자동 인계를 표시해 실제 attempt 누락을 검출하지 못했다. | 응답 postcondition 검증과 정·부 work 생성을 추가했다. |
| `IQC-ORPHAN-ARRIVAL-UNRECOVERED` | P1 | `RESOLVED` | 기존 부분 상태의 `Arrived` receipt는 검사함에서 자동 복구되지 않았다. | 검사함 진입 전 reconciliation endpoint를 추가했다. |
| `OPERATIONAL-PAGES-NOT-PROJECT-FIRST` | P2 | `RESOLVED` | 전역 부서 메뉴가 전체 queue를 바로 열어 어떤 프로젝트를 수정하는지 불명확했다. | 공통 project-first hub와 부서별 workspace 선택을 추가했다. |
| `PROCUREMENT-ARRIVAL-LOCK-DEADLOCK` | P1 | `RESOLVED` | 구매 project→item lock과 도착 item→project FK key-share 순서가 동시 실행에서 교착될 수 있었다. | project lock을 `FOR NO KEY UPDATE`로 좁혀 구매 직렬화는 유지하면서 FK key-share와 공존시켰다. |

## 3. 구현 결정

- 새 table이나 복제 data를 만들지 않고 기존 `project_procurement_items.id`를 구매·도착·IQC의 단일 identity로 유지했다.
- 알림·내 업무 수신자는 프로젝트의 `MaterialsPrimary/Secondary`, `QualityIQC/Secondary`를 우선한다. 담당자가 없을 때만 기존 permission fallback을 사용한다.
- reconciliation은 모든 유효 누락 도착분을 복구하지만 같은 receipt에는 한 번만 생성된다.
- DB schema와 migration, 외부 Teams·Mail provider, 권한 확대는 포함하지 않았다.

## 4. 주요 변경 파일

- Backend: `MaterialsContracts.cs`, `MaterialsEndpointExtensions.cs`, `MaterialsStore.cs`, `ProcurementStore.cs`
- Backend test: `ProcurementApiTests.cs`
- Frontend: `App.tsx`, `DepartmentProjectHub.tsx`, `MaterialsWorkspace.tsx`, `api.ts`, `materials.ts`, `styles.css`
- Frontend test: `App.test.tsx`
- Full-Stack: `workflow-continuity-change-003.full-stack.spec.ts`, 기존 `workflow-continuity.full-stack.spec.ts`
- Governance: Change 003 계약, 이 보고서, Product Roadmap, 실험 완료 원장

## 5. 자동 검증 결과

- Backend Release build: 경고 `0`, 오류 `0`
- Backend 전체 isolated PostgreSQL: `413/413` 통과
- Backend 집중 구매·자재·IQC: `20/20` 통과
- Frontend typecheck: 통과
- Frontend lint: error `0`, 기존 `main.tsx` Fast Refresh warning `1`
- Frontend unit: `115/115` 통과
- Frontend production build: 통과, 기존 500 kB 초과 chunk warning 유지
- 신규 Full-Stack: `1/1` 통과
- 연관 Full-Stack 묶음: 최초 `5/6` 통과 후 프로젝트 query가 추가된 정상 URL을 구형 test가 허용하지 않아 assertion을 갱신했고, 실패 spec 재실행 `1/1` 통과
- `git diff --check`: 통과
- desktop·390px page overflow: `0`
- 모든 E2E는 synthetic data, 임시 PostgreSQL과 외부 provider 비활성 상태에서 실행 후 자동 정리했다.

## 6. 시행착오와 독립 검증에서 발견한 사항

- Backend 전체 회귀 전 집중 동시성 test가 실제 `40P01` 교착을 재현했다. 단순 재시도로 숨기지 않고 lock 강도를 보정한 뒤 집중 test `20/20`과 전체 `413/413`으로 재검증했다.
- 기존 Full-Stack은 IQC 주소가 `/quality/iqc?request=...`라고 고정했다. 새 프로젝트 범위 주소 `/quality/iqc?project=...&request=...`가 실제 기대 동작이므로 제품을 되돌리지 않고 검수 계약을 갱신했다.
- 신규 E2E 작성 중 test 변수 오기와 모바일에서 숨겨진 개발 사용자 selector 접근을 수정했다. 제품 Finding은 아니며 최종 실제 UI 시나리오는 통과했다.

## 7. 시각 증빙

Repository에는 복사하지 않고 `/tmp/workflow-continuity-change-003-screenshots/`에 privacy-safe synthetic 화면을 생성했다.

- `01-purchased-input-exact-error-desktop.png`
- `02-materials-project-hub-desktop.png`
- `03-manufacturing-project-hub-desktop.png`
- `04-quality-project-hub-desktop.png`
- `05-logistics-project-hub-desktop.png`
- `06-pending-project-hub-desktop.png`
- `07-quality-iqc-recovered-desktop.png`
- `08-materials-project-hub-mobile-390.png`
- `09-quality-iqc-recovered-mobile-390.png`

## 8. SOP — 실제 담당자 사용 절차

1. 구매 담당자가 도급/사급 품목을 저장한다. 오류가 있으면 상단 안내와 강조된 입력칸을 고친다.
2. 자재 정·부 담당자는 알림 또는 내 업무에서 변경된 구매품을 확인한다.
3. 왼쪽 자재 메뉴에서는 먼저 입고 관리 또는 패널 키팅을 선택하고 프로젝트를 연다.
4. 자재 담당자가 실제 도착분을 등록하면 별도 IQC 요청 없이 품질 정·부 담당자의 검사함·내 업무·알림으로 넘어간다.
5. 품질 담당자는 품질 메뉴에서 프로젝트를 선택해 IQC를 판정한다. 기존 누락 도착분도 이 진입 시 자동 복구된다.

## 9. User manual — 오류와 프로젝트 화면 읽기

- 붉은 오류 요약의 `저장하지 못한 위치`가 수정할 공급 유형과 행이다.
- `문제 필드`는 실제 입력칸 이름이고 `해결 방법`은 저장 조건이다. 해당 입력칸에도 같은 안내가 표시된다.
- 전역 부서 메뉴의 프로젝트 목록은 조회 대상을 고르는 화면이다. 프로젝트를 열어야 해당 프로젝트 데이터 수정 화면으로 이동한다.
- 알림·내 업무의 `이동`은 이미 프로젝트가 정해진 업무이므로 목록을 거치지 않는다.

## 10. 사용자 검수 체크리스트

상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

- [ ] 도급 구매품의 단위만 비웠을 때 정확한 행·필드·해결 방법과 입력칸 강조 확인
- [ ] 품목 신규 저장과 다음 변경에서 자재 정·부 담당자 각각의 내 업무·알림 확인
- [ ] 전역 자재에서 입고 관리/패널 키팅 선택 후 프로젝트 선택 확인
- [ ] 도착 등록 직후 품질 정·부 내 업무·알림과 프로젝트 IQC 검사함 확인
- [ ] 자재·제조·품질·물류·Pending이 모두 프로젝트 목록에서 시작하는지 확인
- [ ] 기존 도착분 중 IQC 누락 건이 검사함 진입 후 한 번만 복구되는지 확인
- [ ] 모바일 390px에서 가로 잘림 없이 자재 선택·프로젝트 목록·IQC 카드 확인

## 11. 개인정보·secret 검토

- synthetic project와 `dev-*` 역할 계정만 사용했다.
- 실제 이름, 이메일, 고객 정보, token, secret, provider payload는 code·문서·screenshot에 기록하지 않았다.
- 신규 환경 변수·인증서·외부 연결을 추가하지 않았다.

## 12. Finding, 잔여 위험과 후속

- Open P0/P1/P2: `0/0/0`
- P3 backlog: 대형 `App.tsx` route 분리, production bundle code-splitting, 기존 `main.tsx` Fast Refresh warning.
- 사용자 검수는 실험 branch 정책에 따라 마지막에 일괄 수행한다.
- 대표 repo 승격, Persistent UAT와 실제 provider 검증은 별도 운영 전환 Task와 승인이 필요하다.

## 13. Rollback과 복구

DB migration이 없으므로 이 experiment local commit을 revert하면 code·test·문서 변경이 함께 되돌아간다. 이미 생성된 work/notification/IQC 데이터는 기존 schema와 idempotency key를 사용하므로 schema rollback이 필요 없다. 운영 승격 후 문제가 생기면 품목 identity와 기존 도착·검사 기록은 보존하고 UI·handoff projection을 forward-fix한다.

## 14. 종료 산출물 추적

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 전체 |
| SOP | 완료 | 이 문서 `8. SOP` |
| User manual | 완료 | 이 문서 `9. User manual` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md` Task row·Decision Log |
| User validation checklist | 작성·자동 검증 완료, 사용자 검수 대기 | 이 문서 `10. 사용자 검수 체크리스트` |

## 15. 게시 경계

- experiment local commit: 승인됨
- push / PR / merge: 미승인
- 대표 repo / GitHub `main`: 변경하지 않음
- `main` merge 승인: `0/3`
- Persistent UAT / 실제 provider: 미승인·미적용
