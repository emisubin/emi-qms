# TASK-WORKFLOW-CONTINUITY-001 Change 012 구현 보고 — 전 부서 Pending 코멘트·부적합 항목 재검사·LQC 누락 복구

## 해결한 업무 문제

열린 Pending의 코멘트 입력이 조치·품질 참여자에게만 묶여 있어 다른 업무 부서가 일정 영향이나 현장 정보를 남길 수 없었다. LQC·OQC 재검사는 직전 검사에서 적합했던 항목까지 전체 양식을 다시 검사하게 했고, 기존 제조 실행이 이미 시작 또는 완료됐지만 LQC 업무가 없는 패널은 품질 인계 재조정 대상에서도 빠졌다.

## 요청별 구현 결과

1. 영업·설계·생산관리·구매·자재·제조·품질·물류처럼 `Pending.Manage` 권한이 있는 활성 업무 부서는 열린 Pending에 코멘트를 남길 수 있다.
2. 다른 부서의 상태 전이·담당 변경·조치 시작/완료·품질 판정 권한은 확대하지 않았다. 조회 전용 계정은 코멘트 입력 UI와 API 모두 계속 차단한다.
3. LQC·OQC 부적합 재검사는 직전 부적합 성적서의 `Fail` 항목만 표시하고 저장·사진·확정 payload도 그 항목만 허용한다.
4. 재검사 항목에는 이전 부적합 메모 또는 판정 사유를 `이전 부적합 근거`로 함께 표시한다.
5. 재검사에는 직전 부적합 성적서의 양식 version을 그대로 사용하며, 반복 재검사도 최신 직전 실패 항목만 이어받는다.
6. 재검사에서 다시 부적합이면 기존 Pending을 재조치 상태로 되돌리고 같은 조치 부서를 유지한다. 재조치 완료 뒤 새 재검사를 다시 생성할 수 있고, 합격이면 같은 Pending을 닫는다.
7. 제조 실행이 시작됐지만 LQC 업무가 없는 활성 패널을 재조정 후보에 포함했다. LQC 업무와 정·부 알림은 기존 제조 인계와 같은 멱등 키로 생성하며 반복 실행은 중복을 만들지 않는다.
8. 고정 검수 runtime의 지정 프로젝트 2번 패널 누락을 재조정했다. 현재 LQC 검사함에는 표시되고, OQC는 LQC 합격 전이라 열리지 않은 정상 상태다.

## 기술적 결정과 검토한 대안

- 채택: 코멘트 권한은 `Pending.Manage`를 가진 업무 부서 전체로 넓히되 전이 권한 계산은 기존 참여자·부서장·품질 규칙을 유지한다. `Pending.Read`만으로 쓰기를 허용하는 안은 조회 전용 계정까지 쓰기가 열리므로 적용하지 않았다.
- 채택: 재검사 scope는 UI 필터가 아니라 Backend에서 직전 실패 응답의 안정적인 `item_code`를 현재 재검사 양식 항목에 매핑한다. 대상 밖 응답·사진은 400으로 거절한다.
- 채택: 최초 부적합 성적서는 전체 원검사 이력을 그대로 조회하고, 조치 완료 뒤 생성된 재검사부터 실패 항목 scope를 적용한다.
- 채택: 재검사 양식은 최신 활성 양식이 아니라 직전 실패 성적서의 version을 재사용해 검사 도중 양식 변경으로 항목이 바뀌는 것을 막는다.
- 채택: 동일 Pending에 여러 실패 재검사 이력을 허용하도록 단일 연결 고유 index를 비고유 이력 index로 교체한다. 한 번만 재검사할 수 있게 만드는 제한은 실제 재조치 순환과 맞지 않아 제거했다.
- 채택: LQC 누락 복구는 새 품질 attempt를 미리 만들지 않고 정상 제조 시작과 같은 LQC 업무를 복구한다. 품질 담당자가 검사 시작을 누를 때 기존 정상 경로로 attempt·report가 생성된다.

## 아키텍처와 영향

- Pending: 코멘트 작성 가능 여부를 참여자 여부가 아니라 업무 쓰기 권한과 열린 상태로 계산한다.
- 품질: LQC·OQC 재검사 scope, 이전 근거 표시, 반복 실패·재조치·합격 종결을 같은 Pending 수명주기로 유지한다.
- Workflow: 제조 실행이 있으나 LQC 업무가 없는 패널을 검사함 진입 재조정에서 멱등 복구한다.
- DB: migration `0055_panel_quality_reinspection_history.sql`이 반복 재검사 이력 index를 적용한다.
- 권한: 기존 `Pending.Manage`, `Quality.Inspect`, 프로젝트 조회 scope를 유지한다.
- 실제 provider·Persistent UAT·대표 repo·`main`: 변경하거나 호출하지 않았다.

## 변경 파일

- Backend: `PendingContracts.cs`, `PendingEndpointExtensions.cs`, `PendingStore.cs`, `QualityInspectionContracts.cs`, `QualityInspectionStore.cs`
- Database: `database/migrations/0055_panel_quality_reinspection_history.sql`
- Frontend: `PendingInspectionContext.tsx`, `QualityInspectionsPage.tsx`, `qualityInspections.ts`, `styles.css`
- Tests: `ProcurementApiTests.cs`, `PostgreSqlMigrationTests.cs`, `QualityInspectionsPage.test.tsx`, `quality-inspections.full-stack.spec.ts`
- Task·governance: Change 012 계약, 본 구현 보고, 사용자 검수 체크리스트, Product Roadmap, 실험 완료 원장

## Finding과 resolution

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `WF-012-F01` | P1 | Resolved | Pending 코멘트가 조치·품질 참여자 판정에 묶여 타 부서 협업 정보가 기록되지 않았다. | `Pending.Manage` 업무 부서 전체에 열린 Pending 코멘트를 허용하고 조회 전용 UI·API 차단을 유지했다. |
| `WF-012-F02` | P1 | Resolved | LQC·OQC 재검사가 전체 양식을 다시 노출해 적합 항목까지 중복 검사했다. | 직전 실패 응답의 `Fail` 항목만 서버에서 scope화하고 대상 밖 write를 거절한다. |
| `WF-012-F03` | P1 | Resolved | 제조 실행은 있으나 LQC 업무가 없는 패널이 재조정 후보에서 빠졌다. | 제조 `InProgress·Blocked·Completed` 패널의 LQC 업무 누락을 멱등 복구한다. |
| `WF-012-F04` | P1 | Resolved | 요청 상태 재검사를 바로 확정하면 시작 시각 DB 조건을 위반했고, 동일 Pending의 두 번째 실패 재검사는 고유 index에 막혔다. | 확정 transaction에서 시작 정보를 보정하고 반복 재검사 이력을 허용하는 migration을 적용했다. |
| `WF-012-F05` | P2 | Resolved | 최초 부적합 성적서도 재검사로 오인하면 이전 실패 항목이 없어 상세 조회가 실패할 수 있었다. | 최초 실패 성적서는 전체 원검사로 유지하고 후속 attempt에만 재검사 scope를 적용했다. |
| `WF-012-F06` | P2 | Resolved | read-only 사용자도 Backend projection상 코멘트 form을 볼 수 있는 권한 표현 불일치가 있었다. | actor의 실제 `Pending.Manage` 여부를 `canComment`에 반영하고 API에서도 이중 차단했다. |

Open P0/P1/P2: `0/0/0`.

## 검증

- Backend Release build: 경고 0, 오류 0.
- Backend 집중 회귀: LQC·OQC 재검사 2건과 누락 인계 재조정 1건, `3/3` 통과.
- Migration upgrade 집중 회귀: `2/2` 통과.
- Backend 전체 회귀: `423/423` 통과.
- Frontend typecheck: 통과.
- Frontend 집중 unit: `3/3` 통과.
- Frontend 전체 unit: 19 files, `128/128` 통과.
- Frontend lint: error 0, 기존 `src/main.tsx` Fast Refresh warning 1.
- Frontend production build: 통과, 기존 500kB 초과 chunk warning 유지.
- Isolated Full-Stack Change 012: OQC 부적합→Pending 조치→실패 항목 전용 재검사→대상 밖 write 거절→합격 종결 `1/1` 통과.
- Isolated Full-Stack 품질 전체: 제조/LQC/OQC 병행, Aggregate Pending, OQC·FAT 병행, 원자 확정과 Change 012를 합쳐 `5/5` 통과.
- 고정 검수 재조정: 최초 LQC 복구 `1`, OQC 복구 `0`, 미해결 `0`; 반복 재실행의 전체 복구 `0`.
- 지정 프로젝트 2번 패널: LQC 검사함 표시 `true`, OQC 표시 `false`로 단계 조건 정상.
- 고정 Frontend·Backend live/ready: HTTP 200.
- `git diff --check`: 통과.

분리 Codex 검증 session은 현재 사용자 요청 없는 agent 생성을 금지하는 실행 규칙 때문에 만들지 않았다. 대신 최종 diff·권한 경계·migration·고정 runtime projection을 같은 session에서 read-only 재검토했다.

## 개인정보·secret 검토

- 고정 runtime 검증은 복구 개수와 단계 존재 여부만 출력했다.
- 보고서에는 실제 프로젝트명·고객명·사용자명·UUID·업무 원문·token·Authorization header를 기록하지 않았다.
- 실제 Teams·메일 provider를 호출하지 않았다.

## 사용자 검수 결과와 남은 항목

- 자동 검증과 고정 runtime 누락 복구 완료.
- 사용자 검수 상태: `사용자 검수 대기`.
- 대표 repo·`main`·Persistent UAT·실제 provider 검증은 승인 범위 밖이다.

## Rollback·forward-fix

- 코드·문서 변경은 Change 012 변경분을 되돌린다.
- migration rollback이 필요하면 새 forward migration으로 비고유 이력 index를 제거하고, 동일 Pending에 두 번째 이상 연결된 attempt가 없는지 먼저 확인한 뒤 고유 index를 복원한다.
- 기존 검사·Pending 이력은 삭제하지 않는다.

## 종료 산출물 5종

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 본 문서에 포함 | `기술적 결정`, `Rollback·forward-fix` |
| User manual | 본 문서·체크리스트에 포함 | `요청별 구현 결과`, Change 012 사용자 검수 체크리스트 |
| Roadmap update | 갱신됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 작성됨 / 사용자 검수 대기 | `tasks/workflow-continuity-001-change-012-user-validation-checklist.md` |

## 변경·게시 경계

- local experiment 구현·검증과 고정 검수 runtime의 누락 복구만 수행했다.
- 사용자가 이번 요청에서 commit을 지시하지 않아 변경은 미커밋 상태다.
- push·PR·대표 repo·`main`·Persistent UAT·실제 provider는 수행하지 않았다.
- `main` merge 승인: `0/3`.
