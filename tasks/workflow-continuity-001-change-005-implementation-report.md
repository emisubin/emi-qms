# TASK-WORKFLOW-CONTINUITY-001 Change 005 구현 보고서

- taskType: `BUGFIX`
- branch: `experiment/task-home-002-personalized-shell`
- baseExperimentCommit: `895de8d8666bc588c634ac8bdcb9612f26326335`
- implementationStatus: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- instructionChainRead: `true`
- fableInvocationCount: `0`
- userValidationCompleted: `false` — 마지막 일괄 검수 대기
- mainMergeApprovalCount: `0/3`
- persistentUatApplied: `false`
- actualProviderCallCount: `0`
- localCommitCreated: `false`

## 1. 사용자 수정필요사항별 완료 결과

1. **코멘트와 이력을 하단 전체 폭으로 통합**
   - 기존 오른쪽 sticky 열을 제거하고 발생 내용·조치 영역 아래에 `코멘트와 처리 이력`을 전체 폭으로 배치했다.
   - 새 코멘트 작성기를 활동 목록 위에 크게 두고, 상태 변경·담당 변경·사유·코멘트를 하나의 시간순 목록으로 유지했다.
2. **조치 완료 시 품질 재검사 업무·알림 자동 생성**
   - 조치 완료(`ReinspectionRequested`) 시 기존 Pending 조치 업무를 완료 처리한다.
   - 같은 transaction에서 기존 IQC/패널 품질 재검사 생성기를 호출해 품질 정·부 내 업무와 인앱 알림을 멱등 생성한다.
3. **품질 화면에서 조치 내용 확인·재검사 코멘트 작성**
   - IQC와 패널 품질 재검사 화면에 연결 Pending의 조치·코멘트·상태 이력을 표시한다.
   - 품질 역할 사용자는 검사 Pending에 직접 `재검사 코멘트`를 추가할 수 있고, 2차 이상 IQC 판정 사유도 같은 명칭과 명확한 합격·불합격 action으로 표시한다.
4. **불합격 재조치 반복과 합격 해제**
   - 재검사 불합격은 새 Pending을 만들지 않고 같은 Pending을 `조치 중`으로 되돌리고 기존 조치 업무를 재개한다.
   - 현재 정 담당자와 설정된 부 담당자에게 version별 멱등 재조치 알림을 발행한다.
   - 다시 조치 완료하면 다음 재검사 회차·업무·알림이 생성되고, 합격 시 같은 Pending과 조치 업무·검사 차단이 종결된다.
5. **재검사 업무 발견과 Pending 해제 경로 명확화**
   - Pending 상세의 `품질 재검사 열기`가 현재 열려 있는 정확한 IQC 회차로 이동한다.
   - 내 업무와 알림은 `재검사 · P-0003 · 부스바 · 500 ea (2차)`처럼 Pending 번호·품목·수량·차수를 표시하며 raw UUID·URL을 사용자 설명에서 제거했다.
   - IQC 검사함은 `Pending 재검사`와 `일반 IQC`를 분리하고 재검사 카드에 Pending 번호를 표시한다.
   - 최종확인에는 남은 검사항목·사진·재검사 코멘트 조건을 함께 표시하고, 조건이 충족되기 전 합격·불합격 버튼을 비활성화한다.
6. **부적합 항목 한정 재검사와 근거·조치 비교**
   - 직전 실패한 상세 IQC 성적서의 `Fail` 항목만 새 재검사 성적서의 조회·저장·최종화 범위로 제한했다.
   - 재검사에서는 `적합/부적합` 두 판정만 허용하고, 서버도 `해당없음`과 정상 항목 재입력을 거부한다.
   - 판정 영역 바로 위에 이전 `부적합 근거`, Pending의 `조치 내용`, 대상 항목을 함께 표시한다. 대상에 사진 필수 항목이 없으면 사진 단계를 생략한다.

## 2. 해결한 업무 문제와 Root Finding

| Finding | 심각도 | 상태 | 원인·영향 | 해소 |
| --- | --- | --- | --- | --- |
| `PENDING-ACTIVITY-RIGHT-RAIL` | P1 | `RESOLVED` | comment와 history 데이터는 합쳤지만 desktop CSS가 오른쪽 sticky rail을 유지해 사용자가 코멘트를 찾기 어려웠다. | 단일 열·하단 전체 폭 activity와 상단 composer로 재배치했다. |
| `PENDING-ACTION-WORK-STAYED-IN-PROGRESS` | P1 | `RESOLVED` | 조치 완료 뒤 품질 재검사로 인계돼도 기존 Pending 업무가 계속 진행 중으로 남았다. | `ReinspectionRequested` 전이에서 조치 업무를 완료하고 불합격 때만 재개하도록 맞췄다. |
| `REINSPECTION-FAIL-NO-REACTION-NOTIFICATION` | P1 | `RESOLVED` | 재검사 불합격은 Pending·업무 상태만 되돌리고 조치 담당자 알림을 다시 만들지 않았다. | 같은 transaction에서 기존 업무를 재개하고 정·부 담당자에게 version별 재조치 알림을 생성한다. |
| `QUALITY-REINSPECTION-CONTEXT-MISSING` | P2 | `RESOLVED` | 품질 화면에서 이전 조치 내용과 코멘트를 볼 수 없고 재검사 메모를 Pending에 남길 연결 UI가 없었다. | IQC·패널 품질 화면에 공통 Pending context·comment component를 연결했다. |
| `REINSPECTION-WORK-INDISTINGUISHABLE` | P1 | `RESOLVED` | 내 업무·알림 제목이 모두 `IQC 판정 · 부스바`여서 최초 검사와 P-0002/P-0003 재검사를 구분할 수 없었다. | 업무·알림 제목에 Pending 번호·품목·수량·차수를 기록하고 기존 data를 migration으로 보정했으며 전환 runtime을 위한 client projection도 적용했다. |
| `PENDING-DETAIL-NO-QUALITY-CTA` | P1 | `RESOLVED` | Pending이 `재검사 요청`이 되면 전이 버튼이 없어지고 정확한 품질 검사로 이동할 방법도 없었다. | 열려 있는 연결 IQC 회차를 detail contract에 투영하고 `품질 재검사 열기` CTA를 추가했다. |
| `IQC-REINSPECTION-MIXED-WITH-INTAKE` | P2 | `RESOLVED` | 재검사 두 건이 일반 IQC 5건 사이에 섞여 우선순위를 파악하기 어려웠다. | 검사함 상단에 Pending 재검사 전용 그룹과 건수를 배치했다. |
| `IQC-FINALIZE-BLOCKER-HIDDEN` | P2 | `RESOLVED` | 해제 action은 최종 단계에만 있고 실행 불가 이유가 버튼과 떨어져 있었다. | 최종확인에 3개 완료 조건을 상시 표시하고 충족 전 action을 명시적으로 비활성화했다. |
| `IQC-REINSPECTION-REPEATED-FULL-TEMPLATE` | P1 | `RESOLVED` | 2차 검사도 최초 검사와 같은 전체 양식을 다시 요구해 품질 담당자가 이미 적합한 항목까지 재입력해야 했고 이전 근거·조치를 나란히 볼 수 없었다. | 직전 Fail 항목만 server-side scope로 투영하고 적합/부적합만 허용하며 근거·조치 비교 카드를 추가했다. |

## 3. 기술적 결정과 보존한 불변조건

- 부적합 회차마다 Pending을 새로 만드는 방식을 채택하지 않았다. 하나의 부적합은 하나의 Pending identity와 history를 계속 사용한다.
- 재검사 불합격 때 새 조치 업무를 중복 생성하지 않고 기존 Pending 업무를 `InProgress`로 되돌린다.
- 재조치 알림은 `pending:{pendingId}:reopened:v{version}` 키로 멱등 처리하고 현재 assignee를 정 담당자로 보존하며 프로젝트 부 담당자가 있으면 함께 수신한다.
- 품질 역할의 검사 Pending comment 권한만 확장했다. 일반 Pending의 담당자·등록자·생산관리 권한 계약과 종결 후 comment 금지는 유지한다.
- 상세 IQC 재검사의 scope는 직전 실패 회차의 item code를 현재 template version에 매핑한다. 실패 항목이 없는 legacy 판정에는 억지 scope를 만들지 않고 기존 전체 재검사 흐름을 보존한다.
- `0050_iqc_reinspection_work_labels.sql`은 기존 재검사 업무·알림의 사용자 표시만 보정하며 상태·수신자·이력은 변경하지 않는다. 실제 Teams/Mail provider 발송은 실행하지 않고 기존 인앱 알림 원장만 사용했다.

## 4. 주요 변경 파일

- Backend: `PendingContracts.cs`, `PendingEndpointExtensions.cs`, `PendingStore.cs`, `MaterialsContracts.cs`, `MaterialsStore.cs`, `IqcReportContracts.cs`, `IqcReportStore.cs`
- Backend test: `ProcurementApiTests.cs`
- Frontend: `App.tsx`, `pending.ts`, `materials.ts`, `PendingPage.tsx`, `PendingInspectionContext.tsx`, `pendingTimeline.ts`, `MaterialsWorkspace.tsx`, `IqcReportWorkspace.tsx`, `QualityInspectionsPage.tsx`, `styles.css`
- Frontend test: `PendingInspectionContext.test.tsx`
- Migration: `database/migrations/0050_iqc_reinspection_work_labels.sql`
- Governance: Change 005 계약, 이 보고서, Product Roadmap, 실험 완료 원장

## 5. 자동 검증 결과

- Backend Debug/Release build: 경고 `0`, 오류 `0`
- Backend `ProcurementApiTests`: `21/21` 통과
- 핵심 lifecycle: 최초 부적합→Pending 조치 완료→품질 재검사 업무·알림→재검사 불합격→같은 Pending 재개·재조치 알림→재조치 완료→3차 검사 합격→Pending 종결 `1/1` 통과
- Frontend production build: 통과
- Frontend unit 전체: `116/116` 통과
- Frontend lint: error `0`, 기존 `main.tsx` Fast Refresh warning `1`
- `git diff --check`: 통과
- 추가 재검사 발견성 회귀: Backend lifecycle `1/1`, migration 적용 `1/1`, Frontend component `1/1`, Frontend/Backend build 경고·오류 `0` 통과
- 상세 IQC 재검사 scope 회귀: 직전 `ITEM_SPEC` 단일 부적합→근거·조치 projection→해당없음 거부→단일 적합 판정만으로 합격·Pending 해제 `1/1` 통과
- xAI 부스바 현재 data 확인: P-0002 700ea·P-0003 500ea가 품질 내 업무·읽지 않은 알림·IQC 재검사 그룹에 각각 1건으로 투영되고, P-0002 상세 CTA가 정확한 2차 attempt로 이동함을 DOM으로 확인
- Backend test는 기존 tmpfs 격리 PostgreSQL의 별도 test DB에서 수행하고 종료 시 DB를 삭제했다. 외부 provider는 호출하지 않았다.

## 6. 화면 검증과 증빙

Repository에는 복사하지 않고 채팅 보고용 `/tmp`에 생성했다.

- `/tmp/emi-qms-change-005-pending-mobile.png` — 모바일 하단 통합 activity
- `/tmp/emi-qms-change-005-pending-desktop-activity.png` — desktop 발생 내용 아래 전체 폭 activity
- `/tmp/emi-qms-change-005-quality-comment.png` — 품질 담당자의 큰 comment composer
- `/tmp/emi-qms-change-005-iqc-reinspection.png` — IQC 2차 재검사에서 조치 내용·comment context
- `/tmp/emi-qms-change-005-iqc-decision.png` — `재검사 코멘트`, `불합격 · 재조치 요청`, `합격 · Pending 해제`
- `/tmp/emi-qms-xai-reinspection-my-work.png` — xAI P-0002/P-0003 내 업무 식별 정보
- `/tmp/emi-qms-xai-pending-reinspection-link.png` — Pending 상세의 정확한 품질 재검사 CTA
- `/tmp/emi-qms-xai-iqc-reinspection-queue-final2.png` — Pending 재검사 2건과 일반 IQC 5건 분리
- `/tmp/emi-qms-xai-reinspection-decision.png` — 해제 전 남은 3개 조건과 비활성화된 판정 action
- `/tmp/emi-qms-reinspection-scoped-mobile.png` — 모바일에서 부적합 근거·조치 내용·단일 재검사 항목과 2개 판정만 표시

DOM 위치 검증에서 desktop `pending-detail-main` 하단은 `530.85px`, activity 상단은 `548.85px`로 확인돼 오른쪽 열이 아니라 아래쪽 순차 배치임을 확인했다.

## 7. SOP — 담당자 사용 절차

1. 조치 담당자는 Pending에서 `조치 시작` 후 처리 내용을 입력하고 `조치 완료`한다.
2. 품질 정·부 담당자는 자동 생성된 알림 또는 내 업무의 정확한 재검사 링크를 연다.
   - 제목에서 Pending 번호·품목·수량·차수를 확인하거나 Pending 상세의 `품질 재검사 열기`를 사용한다.
3. 재검사 화면 상단의 `조치 내용과 재검사 코멘트`에서 조치 이력과 comment를 확인하고 필요한 검사 메모를 추가한다.
4. 이전에 부적합이었던 항목만 `적합/부적합`으로 다시 판정한다. 해당 항목이 사진 필수일 때만 사진을 등록하고 `재검사 코멘트`에 판정 근거를 작성한다.
5. 불합격이면 같은 Pending이 조치 담당자에게 돌아가며, 합격이면 Pending과 검사 차단이 해제된다.

## 8. User manual — 화면과 복구 안내

- Pending의 comment는 별도 오른쪽 상자에 있지 않고 상세 화면 가장 아래 `코멘트와 처리 이력`에 있다.
- 품질 재검사 화면의 첫 카드에서 조치 내용과 이전 comment를 확인할 수 있다. `코멘트 등록`은 검사 판정과 별도로 즉시 Pending history에 남는다.
- `불합격 · 재조치 요청`은 새 Pending을 생성하지 않는다. 같은 번호의 Pending이 조치 담당자 내 업무에서 다시 진행 중으로 보인다.
- `합격 · Pending 해제`는 필수 검사 항목·사진·재검사 코멘트를 완료해야 실행할 수 있다.
- 품질 검사함 상단 `Pending 재검사`는 해제가 필요한 회차만 우선 표시하고, `일반 IQC`와 섞이지 않는다.
- 재검사 성적서의 비교 카드에서 이전 부적합 근거와 조치 완료 내용을 먼저 확인한다. 화면에는 실패했던 항목만 나오며 `해당없음`은 제공되지 않는다.

## 9. 사용자 검수 체크리스트

상태: `자동 검증 완료 / 사용자 검수 대기 — 마지막 일괄 검수`

- [ ] desktop Pending에서 activity가 오른쪽이 아닌 발생 내용 아래 전체 폭인지 확인
- [ ] mobile Pending에서 comment와 상태 이력이 한 카드 안에 순차 표시되는지 확인
- [ ] 조치 완료 직후 품질 정·부 담당자의 알림과 내 업무에 같은 재검사 회차가 한 번만 생기는지 확인
- [ ] 품질 재검사 화면에서 조치 comment 확인과 재검사 comment 등록이 가능한지 확인
- [ ] 내 업무·알림·IQC 검사함에서 P-0002 700ea와 P-0003 500ea가 서로 구분되는지 확인
- [ ] Pending 상세의 `품질 재검사 열기`가 같은 번호의 정확한 2차 검사로 이동하는지 확인
- [ ] 최종확인에서 검사항목·사진·코멘트가 남아 있을 때 판정 버튼이 비활성화되고 이유가 표시되는지 확인
- [ ] 2차 상세 IQC에서 직전 부적합 항목만 보이고, 이전 근거·조치가 함께 표시되며 `적합/부적합`만 선택 가능한지 확인
- [ ] 재검사 불합격 뒤 같은 Pending 번호가 조치 담당자 내 업무로 돌아오고 재조치 알림이 오는지 확인
- [ ] 다시 조치 완료·재검사 합격 후 Pending이 종결되고 업무가 사라지는지 확인

## 10. 잔여 위험, 게시 경계와 Rollback

- Open P0/P1/P2: `0/0/0`. P3는 기존 대형 bundle code-splitting과 `main.tsx` Fast Refresh warning이다.
- 41164 기존 실험 backend process 종료는 원격 실행 정책에서 차단됐다. 새 Backend contract와 migration은 격리 test host에서 검증했고, 42981은 이전 backend와도 동작하는 client projection을 포함해 현재 xAI data 화면을 검증했다. 다음 정상 backend 재시작부터 새 server-side 제목과 detail projection이 기준이 된다.
- local commit, push, PR, merge는 수행하지 않았다. 대표 repo·GitHub `main`, Persistent UAT, 실제 provider는 미변경이며 `main` merge 승인은 `0/3`이다.
- migration과 변경 파일을 함께 되돌리되 이미 적용된 표시 보정은 상태·수신자·history에 영향을 주지 않으므로 삭제하지 않고 forward-fix한다.

| 종료 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 완료 | 이 문서 |
| SOP | 완료 | `7. SOP` |
| User manual | 완료 | `8. User manual` |
| Roadmap update | 완료 | `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md` |
| User validation checklist | 작성·자동 검증 완료, 사용자 검수 대기 | `9. 사용자 검수 체크리스트` |
