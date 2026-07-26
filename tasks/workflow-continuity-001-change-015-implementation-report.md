# TASK-WORKFLOW-CONTINUITY-001 Change 015 구현 보고

상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`

## 해결한 업무 문제

1. 상세 IQC·LQC·OQC에서 검사 항목 결과와 무관하게 합격·부적합 선택을 함께 보여 사용자가 모순된 판정을 시도할 수 있었다.
2. IQC·LQC·OQC·전진검수·FAT 재검사에서 다시 부적합이면 공통 Pending 로직이 `조치 요청`을 건너뛰고 곧바로 `조치 중`으로 전이해 조치 담당자가 업무 시작 전 상태를 확인할 수 없었다.
3. IQC 합격 뒤 자재 담당자에게 생성되는 입고 확정 내 업무가 프로젝트·품목·수량·도착일을 여러 줄로 반복해 목록에서 핵심 요청을 빠르게 읽기 어려웠다.

## 구현 결과

1. 상세 IQC·LQC·OQC 체크리스트의 최종 판정을 항목 결과에서 자동 계산한다.
   - 검사 가능한 항목 중 `Fail`이 하나라도 있으면 부적합 확정 동작만 표시한다.
   - `Fail`이 없으면 합격 확정 동작만 표시한다.
   - 상세 IQC Backend도 부적합 항목이 없는 `Failed` 요청을 거부한다.
   - 전진검수·FAT는 항목별 체크리스트가 없는 패널 통합 판정이므로 명시적인 적합·부적합 선택을 유지한다.
2. 모든 품질 재검사 실패가 같은 공통 전이를 사용한다.
   - IQC 기존 판정, IQC 상세 성적서, LQC, OQC, 전진검수, FAT 모두 `ReinspectionRequested → ActionRequested`로 돌아간다.
   - 같은 Pending 업무를 `Requested`로 재활성화하고 시작·완료 시각을 초기화한다.
   - 새 Pending이나 중복 업무를 만들지 않고 같은 Pending의 정·부 조치 담당자에게 버전 기반 멱등 알림을 보낸다.
3. 자재 입고 확정 내 업무 설명을 다음 한 줄로 축약했다.
   - `IQC 합격 도착분의 입고 확정을 진행해 주세요. (품목명 수량 단위)`
   - 알림 제목·본문과 전용 화면 바로가기는 변경하지 않았다.

## 전체 영향

- Backend: 공통 Pending 상태 전이, 상세 IQC 판정 불변조건, 입고 확정 업무 설명.
- Frontend: 상세 IQC와 패널 체크리스트 품질검사의 파생 판정·단일 확정 동작.
- DB·Migration: `N/A` — schema와 기존 데이터를 변경하지 않는다.
- API: endpoint·request·response 계약은 유지하고 허용 상태 전이와 검증만 강화했다.
- 권한·Workflow: 기존 품질 판정 권한과 Pending 조치 권한을 유지한다.
- 알림: 기존 정·부 수신자·멱등 키·outbox 정책을 유지한다.
- Excel·PDF·첨부파일: `N/A` — 생성·다운로드·보존 계약을 변경하지 않는다.

## 기술적 결정과 검토한 대안

- 채택: 재검사 실패 시 기존 Pending 업무를 `Requested`로 재활성화한다. 같은 결함의 조치 이력과 코멘트를 한 Pending에 유지하고 중복 업무를 막을 수 있다.
- 제거: `InProgress`로 직접 복귀하는 기존 동작. 조치 담당자의 명시적인 `조치 시작` 단계를 없애므로 폐기했다.
- 보류: 재검사 실패마다 새 Pending·새 업무를 만드는 방식. 이력·코멘트 분리와 중복 알림 위험 때문에 적용하지 않았다.
- 채택: 체크리스트 판정은 응답에서 자동 도출하고 전진검수·FAT 통합 판정만 사용자가 직접 선택한다.

## 시행착오 및 폐기한 접근

- 품질 판정 dialog의 두 선택 버튼 중 반대 결과를 비활성화하는 방식은 화면에 모순된 동작이 계속 남아 사용자의 “하나만 표시” 요구를 충족하지 못해 폐기했다.
- 입고 업무 설명에 전용 화면 URL을 함께 저장하던 방식은 `targetType`과 `targetId`로 바로가기를 계산하는 현재 Workflow 계약에서 불필요해 제거했다.
- Frontend 집중 테스트 첫 실행은 작업 디렉터리를 이미 `frontend/`로 둔 상태에서 `frontend/tests/...` 경로를 다시 붙여 대상 파일을 찾지 못했다. 즉시 `tests/...` 경로로 바로잡아 같은 테스트를 통과시켰다.

## 변경 파일

- Backend
  - `backend/src/Emi.Qms.Api/Pending/PendingStore.cs`
  - `backend/src/Emi.Qms.Api/Materials/IqcReportStore.cs`
  - `backend/src/Emi.Qms.Api/Materials/MaterialsStore.cs`
- Frontend
  - `frontend/src/IqcReportWorkspace.tsx`
  - `frontend/src/QualityInspectionsPage.tsx`
  - `frontend/src/styles.css`
- Tests
  - `backend/tests/Emi.Qms.Api.Tests/ProcurementApiTests.cs`
  - `frontend/tests/IqcReportWorkspace.test.tsx`
  - `frontend/tests/QualityInspectionsPage.test.tsx`
- Task·governance
  - Change 015 계약, 본 구현 보고, 사용자 검수 체크리스트, Product Roadmap, 실험 완료 원장

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-015-F01` | P1 | Resolved | 체크리스트 응답과 반대되는 최종 판정 선택이 함께 노출돼 확정 오류와 사용자 혼선을 만들었다. | 응답에서 결과를 파생해 맞는 확정 동작 하나만 표시하고 상세 IQC 서버 불변조건을 대칭으로 강화했다. |
| `WF-015-F02` | P1 | Resolved | 공통 재검사 실패 로직이 조치 요청을 건너뛰고 Pending과 업무를 `InProgress`로 직접 전이했다. | 같은 Pending을 `ActionRequested`, 같은 업무를 `Requested`로 재활성화하고 정·부 담당자 알림을 다시 연결했다. |
| `WF-015-F03` | P2 | Resolved | 입고 확정 업무 설명이 핵심 요청 외 메타데이터를 여러 줄로 반복했다. | 품목명·수량·단위만 괄호에 담은 한 줄 설명으로 축약했다. |

Open P0/P1/P2: `0/0/0`.

## 실행한 검증

- Backend 집중 통합 회귀: 상세 IQC·기존 IQC·LQC·OQC 반복 재검사 `4/4` 통과.
- Backend 전체 회귀: `424/424` 통과.
- Backend Release build: 경고 0, 오류 0.
- Frontend 집중 unit: `5/5` 통과.
- Frontend 전체 unit: 20 files, `132/132` 통과.
- Frontend typecheck: 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend production build: 통과, 기존 500kB 초과 chunk warning 유지.
- 고정 검수 runtime:
  - Frontend root HTTP 200.
  - Backend `/health/ready` status `ok`, database reachable.
  - 기존 Frontend `42983`, Backend `41166`을 유지했다.
- Privacy-safe browser smoke:
  - 품질 역할에서 품질 프로젝트·IQC/후속검사 선택 화면 로드 확인.
  - desktop 로드와 390px viewport에서 horizontal overflow 0.
  - browser console error 0.
- `git diff --check`: 통과.

## 미실행 검증과 이유

- 실제 Teams·메일 발송: 실제 provider 승인 범위 밖이며 검수 runtime에서 모두 비활성화했다.
- Persistent UAT 적용·실데이터 mutation: 사용자 승인 범위 밖이다.
- 사용자 수동 판정·재조치 입력: 마지막 일괄 검수 정책에 따라 체크리스트로 남겼다.
- 공개 screenshot 저장: 실제 검수 DB의 업무명이 노출될 수 있어 비식별 DOM projection과 390px overflow·console 결과를 동등 시각 증빙으로 사용했다.

## 개인정보·secret 검토

- 보고서에는 실제 프로젝트·고객·사용자 식별 원문, secret, connection string을 기록하지 않았다.
- 자동 통합 테스트는 격리된 synthetic DB를 사용했다.
- 고정 검수 browser 결과는 역할명·화면명·집계값만 기록했다.
- 실제 provider와 Persistent UAT를 호출하지 않았다.

## 사용자 검수 결과와 남은 항목

- 자동 검증: 완료.
- 사용자 검수 상태: `사용자 검수 대기 — 마지막 일괄 검수`.
- Commit: 사용자 승인에 따라 현재 누적 experiment checkpoint에 포함한다.
- 대표 repo·`main`·Persistent UAT·실제 provider: 승인 범위 밖.

## SOP·사용자 확인 방법

1. 고정 Frontend `http://127.0.0.1:42983`에서 품질 역할로 상세 IQC 또는 LQC/OQC 초안을 연다.
2. 모든 체크 항목을 적합으로 입력한 뒤 최종확인에서 합격 확정만 보이는지 확인한다.
3. 한 항목을 부적합으로 바꿔 최종확인에서 부적합 확정만 보이는지 확인한다.
4. 재검사에서 다시 부적합 처리한 뒤 같은 Pending이 `조치 요청`, 조치 담당자의 내 업무가 `시작 전`인지 확인한다.
5. 자재 역할의 내 업무에서 IQC 합격 입고 확정 상세가 한 줄 요약인지 확인한다.

## Rollback·forward-fix

- Frontend 파생 판정·단일 동작 표시를 Change 015 이전으로 되돌린다.
- 공통 Pending 재검사 실패 전이를 `InProgress`로 되돌릴 수 있으나 조치 시작 단계가 다시 사라지므로 권장하지 않는다.
- 입고 확정 업무 설명만 이전 다중 행 형식으로 독립 복원할 수 있다.
- DB schema·기존 Pending·검사 결과·업무 데이터를 변경하는 migration이 없어 데이터 rollback은 없다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `SOP·사용자 확인 방법`, `Rollback·forward-fix` |
| User manual | 본 문서·체크리스트에 포함 | `SOP·사용자 확인 방법`, Change 015 사용자 검수 체크리스트 |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/workflow-continuity-001-change-015-user-validation-checklist.md` |

## 변경·게시 경계

- local experiment 코드·문서, 자동 검증과 고정 검수 runtime health 확인만 수행했다.
- Commit·push·PR·merge는 수행하지 않았다.
- 대표 repo·`main`·Persistent UAT·실제 provider는 변경하지 않았다.
- `main` merge 승인: `0/3`.
