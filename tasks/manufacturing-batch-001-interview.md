# TASK-MANUFACTURING-BATCH-001 — 다중 패널 조립 단계 일괄 완료 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `WAIVED_BY_USER_FOR_EXPERIMENT`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 `experiment/*` fast-track에서 사용자-facing interview를 생략한 근거와 Fable 1차 기획 입력을 고정한다. 사용자는 프로젝트 전체 흐름의 네 단계 표시명을 바꾸고, 제조팀이 기존 선택 Excel용 패널 checkbox를 이용해 여러 패널의 조립 단계를 한 번에 완료할 수 있게 구현하도록 명시했다. 현장에서는 실제 작업을 한꺼번에 수행하고 상세 입력은 나중에 할 수 있다. 이 branch와 대화의 standing instruction에 따라 비차단 정책은 Fable 권장안을 자동 채택하고 `Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot`까지 이어간다.

## 1. Task Identity Gate

- proposedTaskId: `TASK-MANUFACTURING-BATCH-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `첨부·사진 storage/검역/보존/backup·restore`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-MANUFACTURING-BATCH-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_EXPLICIT_MANUFACTURING_BATCH_REQUEST`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 제조 담당자가 한 프로젝트의 여러 패널을 기존 checkbox로 선택하고 조립 단계를 한 번의 action으로 완료해 반복 입력을 줄인다.
- Root Finding 또는 정책 결정: `TASK-E2E-FULL-SUITE-001 change-006`의 `MULTI-PANEL-REPETITIVE-INPUT-FRICTION` P3. `TASK-011A`는 복수 panel batch 실행을 명시적으로 제외했으며, 현재 별도 구현 Task·branch·PR은 없다.
- 변경·검증 경계: 제조 execution의 조립 의미 단계 일괄 완료 API·권한·transaction·idempotency와 기존 선택 UI 연결, 프로젝트 전체 흐름의 요청된 네 표시명, desktop·390px 검증을 포함한다.
- 보존할 불변조건: 패널별 execution·단계·actor/time audit, 제조 정·부 권한, project scope, Pending 차단, LQC 병행·OQC 인계, panel stage 전진-only, 기존 선택 Excel, 다른 제조 단계와 완료 규칙을 보존한다.
- 예상 산출물: Fable 1차 기획 원문, Codex review, Fable 2차 기획 원문, additive migration이 필요하면 다음 번호의 migration, Backend API·Frontend batch action, 자동 검증, desktop/mobile screenshot, 종료 문서.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

`TASK-011A`는 패널별 제조 시작·단계·중단·재개·완료를 구현했지만 복수 panel batch 실행은 제외했다. `TASK-EXPORT-001`은 제조 화면의 checkbox·선택 Excel만 구현했다. `TASK-E2E-FULL-SUITE-001 change-006`에는 다면 반복 입력 부담이 별도 P3 Finding으로만 남아 있다. 동일 목적의 실행 가능한 Task·branch·worktree·PR은 없으므로 이번 사용자 요청을 명시적인 실험 순서 변경 승인으로 기록한다.

## 2. 확정된 Repository 기준선

- 제조 화면은 프로젝트 안의 패널별 checkbox와 `SelectedExportTray`를 이미 사용한다. 선택 집합은 현재 Excel 내보내기 전용이며 제조 mutation에는 연결되지 않았다.
- 제조 실행은 active template version을 snapshot으로 복사한다. 기본 item code는 `WORK_ORDER`, `MATERIALS`, `MANUFACTURING`, `SELF_CHECK`이고 현재 실행 step snapshot에는 code가 아니라 순서와 표시명만 저장된다.
- 단건 `check-step`은 첫 미완료 단계만 순서대로 허용하며 execution row lock, expected version, operation receipt로 stale·중복을 차단한다.
- 제조 시작과 동시에 패널별 LQC 업무가 생성되고, 제조와 LQC가 모두 끝난 패널만 OQC로 이어진다. batch 조립 완료가 실행 전체 완료나 OQC 인계를 대신하면 안 된다.
- 제조 중단 Pending이 열린 execution은 단계 입력을 차단한다. 읽기 권한과 mutation 권한은 분리하고 Backend가 최종 권한·scope를 강제한다.
- 최신 실험 migration은 미커밋 `0055`; schema 변경이 필요하면 기존 migration을 수정하지 않고 additive `0056`을 사용한다. Persistent UAT에는 적용하지 않는다.
- 고정 사용자 검수 주소는 Frontend `http://127.0.0.1:42983`, Backend `http://127.0.0.1:41166`이다.

## 3. 사용자가 확정한 표시명

프로젝트 상세의 `프로젝트 전체 흐름`에서 다음 네 표시를 정확히 바꾼다. 내부 workflow stage code와 완료 계산은 바꾸지 않는다.

| 기존 표시 | 변경 표시 |
| --- | --- |
| `자재 / 키팅 완료 (선택)` | `자재 / 제조 요청` |
| `물류 / 포장 완료` | `물류 / 포장` |
| `물류 / 납품 완료` | `물류 / 납품` |
| `영업 / 세금계산서 완료` | `영업 / 세금계산서` |

## 4. 해결할 문제와 기대 결과

- 현재 문제: 실제 현장에서 여러 패널의 조립을 한 번에 끝내도 시스템에서는 패널을 하나씩 열어 동일 단계를 반복 확인해야 한다.
- 기대 결과: 제조 담당자가 한 프로젝트 안의 패널 여러 개를 기존 checkbox로 선택하고 `선택 패널 조립 완료`를 실행하면 대상 패널의 조립 의미 단계가 한 번에 기록된다.
- 사용자 가치: 물리적 batch 작업과 디지털 입력 단위를 맞춰 다면 프로젝트의 반복 클릭과 패널 전환 비용을 줄인다.
- 실패 시 영향: 일부 패널만 성공하거나 잘못된 template 단계가 완료되거나, 중단·권한·stale 상태를 건너뛰면 실행 이력과 품질 인계가 신뢰할 수 없게 된다.

## 5. Fable 권장안이 확정할 비차단 정책

아래 항목은 사용자에게 다시 묻지 않는다. Fable이 선택지·trade-off와 Repository 근거 권장안을 제시하고 Codex review 뒤 Fable 2차 기획에서 확정한다.

1. “조립 단계”의 안정적인 식별 방법: template item code snapshot 추가, 현재 label/순서 기반 식별 또는 별도 고정 단계.
2. 선행 단계가 미완료인 선택 패널의 처리: 조립만 독립 기록, 선행 단계도 함께 완료, 또는 대상 제외·사유 표시.
3. batch 원자성: 선택 전체 전부 성공/전부 실패, 사전검증 뒤 가능 대상만 성공, 또는 사용자 확인 단계.
4. 이미 조립 완료·완료 execution·중단·시작 전 패널의 처리와 결과 피드백.
5. 작업은 먼저 완료하고 상세 입력은 나중에 하는 경우 actor/time audit와 추후 보완 가능한 데이터 경계.
6. batch operation id, payload fingerprint, row locking·정렬과 경쟁 요청 처리.
7. desktop·mobile에서 Excel 선택 tray와 제조 batch action의 배치, action 가시성, disabled reason과 접근성.

## 6. 포함 후보

- 기존 제조 패널 checkbox·전체선택과 같은 선택 집합 재사용
- 선택 패널 조립 단계 batch 완료 action
- 제조 담당 정·부 권한과 project access scope의 서버 재검증
- 선택한 모든 panel/execution/template 단계의 사전검증
- idempotent batch receipt, transaction·row lock, actor/time·event audit
- batch 성공·실패·제외 사유의 action 인접 한글 피드백
- 프로젝트 전체 흐름의 네 표시명 변경
- desktop·390px UI, Backend·Frontend·isolated Full-Stack 검증

## 7. 명시적 제외

- 제조 실행 전체 일괄 시작·종료, 중단·재개 일괄 처리
- LQC/OQC/전진검수/FAT·물류의 다중 패널 batch
- 선택 Excel 계약·column picker·workbook 형식 변경
- 제조 template 관리 UI와 운영 양식 content 재설계
- 완료 제조 실행의 되돌리기·기록 삭제·관리자 강제 정정
- 실제 provider, 대표 repo·`main`, Persistent UAT migration/runtime handover
- push·PR·merge와 main merge 승인

## 8. 성공 기준

- 제조 담당자가 같은 프로젝트의 2개 이상 패널을 기존 checkbox로 선택해 조립 단계를 한 번에 완료할 수 있다.
- 서버가 권한·project scope·panel/execution 상태·조립 의미 단계·Pending·stale 상태를 전부 재검증한다.
- 실패 시 부분 반영 없이 사용자에게 행동 가능한 한글 사유를 제공하거나, Fable 최종 권장안이 명시한 안전한 부분 성공 계약을 정확히 지킨다.
- 같은 operation id 재시도는 중복 단계 event 없이 성공 결과를 replay하고 다른 payload 재사용은 conflict다.
- 조립 batch는 execution 전체 완료, 제조 업무 완료, LQC·OQC 상태를 임의 전진시키지 않는다.
- 프로젝트 전체 흐름 네 표시가 요청한 정확한 문구로 보이고 내부 stage code·집계 결과는 유지된다.
- desktop과 390px에서 선택·전체선택·Excel·batch action이 구분되고 page-level horizontal overflow가 없다.
- Backend 전체, Frontend lint/typecheck/unit/build, migration, isolated Full-Stack와 browser screenshot이 통과한다.
- Open P0/P1/P2가 0이고 종료 산출물과 사용자 검수 대기 상태를 추적한다.

## 9. 승인·안전 경계

- planningApprovedForExperiment: `true` — standing instruction과 Fable 권장안 자동 채택 조건
- implementationApprovedForExperiment: `true` — Fable 2차 기획의 blocking decision 0인 범위
- localCommitApproved: `true` — 기존 dirty WIP와 겹치지 않는 exact allowlist를 안전하게 분리할 수 있을 때만
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
