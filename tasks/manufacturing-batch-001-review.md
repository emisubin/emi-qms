# TASK-MANUFACTURING-BATCH-001 — Codex 1차 기획 내용 Review

- reviewTarget: `tasks/manufacturing-batch-001-planning.md`
- reviewOwner: `CODEX`
- reviewRound: 1
- reviewStatus: `RESOLVED_FOR_SECOND_PLANNING`
- taskType: `NEW_FEATURE`
- blockingDecisionCount: 0
- implementationApproved: false

## 1. 총평

1차 기획은 사용자가 요구한 두 결과를 정확히 분리했다. 네 workflow 표시명은 presentation-only 변경으로 한정하고, 제조 batch는 기존 패널별 실행·단계 event·권한·Pending·LQC/OQC 인계를 보존한 채 조립 의미 단계만 일괄 확인한다. 특히 기존 Excel checkbox를 공유하되 mutation action을 별도 영역으로 구분하고, 시작 전·중단·완료 패널을 자동 시작하거나 건너뛰지 않는 방향은 현장 속도와 데이터 안전의 균형이 좋다.

다만 2차 기획에서는 audit correlation, 조립 단계 식별 불가 처리, batch가 선행 단계까지 확인할 때의 정확한 version/event 계산, Frontend 선택 집합의 재동기화 계약을 더 명확히 고정해야 한다. 아래 보강을 적용하면 사용자 추가 결정 없이 구현 가능하다.

## 2. 사용자 문제·기대 결과 검토

- 문제 정의: `MULTI-PANEL-REPETITIVE-INPUT-FRICTION`의 제조 vertical slice를 직접 해결한다. 실제 여러 패널을 한꺼번에 작업한 뒤 시스템에서 패널마다 같은 단계를 반복 입력하는 비용을 줄이는 목표가 명확하다.
- 기대 결과: 기존 선택 checkbox를 그대로 활용하고, 조립 단계만 batch 처리하며 나머지 제조·품질 흐름을 임의 완료하지 않는 결과가 사용자 요청과 맞다.
- 표시명: 사용자가 지정한 정확 문구를 stage code 기준으로 매핑하고 `KittingCompleted`의 `(선택)` 접미사만 화면에서 숨기는 방안이 적절하다. 내부 `is_optional`, stage code, 집계와 업무 제목은 유지해야 한다.
- 제품 방향: Roadmap의 패널 단위 제조 실행 원칙을 유지하면서 PC의 일괄 입력 역할을 강화한다. 모바일에서도 같은 action은 제공하되 PC 표를 축소하지 않고 선택 요약·확인 sheet를 한 열로 배치하는 방향이 맞다.

## 3. 기능별 판단

### 유지

1. `ManufacturingUpdate` + project scope + 각 panel `canMutate`의 서버 재검증.
2. 단일 프로젝트 안의 선택만 허용하고 전부 성공/전부 실패하는 transaction.
3. `InProgress`, open Pending 없음, 조립 단계 미완료, expected version 일치를 모든 대상에서 확인.
4. 조립 단계 전의 미확인 단계도 순서대로 함께 확인하되 확인 sheet에 그 사실을 명시.
5. 시작 전 패널 자동 시작 금지. 제조 시작은 LQC 업무 생성 부작용이 있으므로 별도 단건 action으로 유지.
6. 이미 조립 완료·완료 execution·중단·식별 불가 패널은 Frontend에서 사유를 보여 제외하고, 서버에 전송된 대상은 다시 전부 검증.
7. 기존 checkbox·전체선택·선택 Excel을 그대로 유지하고 제조 mutation bar를 시각적으로 분리.
8. workflow 네 표시명의 stage code 기반 presentation-only override.

### 추가

1. **Batch audit correlation**: 1차 기획의 신규 operation row만으로는 어떤 `StepChecked` event가 어느 batch에서 발생했는지 직접 연결되지 않는다. additive `0056`에서 `panel_manufacturing_events.batch_operation_id` nullable FK를 추가하고, batch가 만든 모든 단계 event에 operation id를 기록한다. 기존 단건 event는 null을 유지한다.
2. **재현 가능한 replay projection**: operation table에는 `operation_id`, `project_id`, `actor_user_id`, payload fingerprint, 완료 panel 수, 실제 확인한 step 수를 저장한다. replay 응답은 이 저장값으로 만들고 현재 상태를 재계산하지 않는다.
3. **단계 식별 계약**: execution의 immutable `template_version_id`와 그 version의 `item_code='MANUFACTURING'` item `display_order`를 snapshot step `sequence_number`에 대응한다. `template_version_id`가 null이거나 code가 없거나 중복/순서 불일치면 추측하지 않고 `ASSEMBLY_STEP_UNRESOLVED`로 제외·거부한다.
4. **Version 계산**: 한 execution에서 선행 2단계와 조립 1단계를 함께 확인하면 단계별 event 3건을 남기고 execution version도 3 증가시켜 기존 단건 `check-step`과 같은 optimistic concurrency 의미를 유지한다. 응답 후 queue/detail을 새로 읽는다.
5. **결정적 잠금 순서**: project row를 먼저 잠그고, request의 panel id를 정렬·중복 제거 검증한 뒤 execution과 steps를 panel id 오름차순으로 잠근다. 모든 검증을 끝낸 뒤 update한다.
6. **Frontend 선택 동기화**: project가 바뀌거나 queue refresh로 visible id가 달라지면 현재 `useSelectedRows` 계약대로 숨은 선택을 제거한다. batch 성공 후 선택은 clear하고, 실패 후에는 유지해 사용자가 제외 사유를 보고 수정할 수 있게 한다.
7. **오류 크기 제한**: 서버 자유문자 원문 대신 안정적인 panel display code + fixed reason code/한글 reason projection을 최대 20개까지 반환하고 나머지는 count로 요약한다. 내부 ID·SQL·stack은 노출하지 않는다.
8. **확인 sheet 내용**: “조립 단계까지의 미완료 선행 단계도 같은 시각·요청자로 함께 확인됩니다”를 명시하고, 대상 수·함께 확인될 단계 수·제외 수를 보여준다.

### 보류

1. 작업시간·실제 완료시각 소급 입력과 사후 상세값 보정. 현재 제조 step은 check audit만 가지며 이번 사용자 요청은 batch 완료 입력이다. 별도 data lifecycle과 정정 감사 정책이 필요한 후속 능력이다.
2. 품질·물류·설계의 다중 패널 batch. 같은 P3 Finding의 다른 vertical slice지만 이번 요청 범위를 넓히지 않는다.
3. 완료 실행 되돌리기·재작업. append-only 완료 불변조건과 충돌하므로 별도 정책 Task가 필요하다.

### 제거

1. **선택 action 전용 확인을 위해 별도 desktop modal을 만드는 안**: 기존 `MobileSheet`를 desktop/mobile 공통으로 재사용하면 focus·escape·backdrop 계약을 보존할 수 있다.
2. **label 문자열 또는 3번째 순서 고정으로 조립 단계를 찾는 안**: 관리자 template label/order 변경 시 잘못된 단계를 완료할 수 있어 제거한다.
3. **부분 성공 API**: 선택 결과가 불명확해지고 재시도·감사·사용자 설명이 복잡해진다. Frontend는 사전 제외, 서버 payload는 원자 처리로 고정한다.
4. **batch에서 제조 전체 완료 또는 LQC/OQC 인계 실행**: 사용자가 요청하지 않았고 패널별 품질 조건을 우회하므로 제거한다.

## 4. 실제 Repository 경계 대조

- `ManufacturingPage.tsx`에는 이미 `useSelectedRows`, `SelectionCheckbox`, `SelectedExportTray`가 있어 선택 상태를 새로 만들 필요가 없다.
- 단건 mutation fence는 `mutationInFlightRef`와 `savingAction`으로 구현돼 있다. batch도 같은 fence를 사용해야 하며 별도 동시 실행 state를 만들지 않는다.
- `ManufacturingStore.StartAsync`는 active template label만 step snapshot에 저장하고 execution에는 `template_version_id`를 저장한다. immutable version item code와 sequence를 join해 조립 의미를 찾을 수 있어 step schema에 item code를 중복 저장할 필요는 없다.
- 기존 `panel_manufacturing_operations`는 단건 replay projection용이며 action별 execution identity를 전제로 한다. 여러 execution을 묶는 batch는 별도 operation table이 더 단순하고 기존 단건 unique/fingerprint 계약을 건드리지 않는다.
- `panel_manufacturing_events`에는 operation correlation이 없으므로 위의 nullable FK 추가가 필요하다.
- workflow label은 `App.tsx`의 `displayWorkflowStageLabel`과 `(선택)` suffix 렌더링 지점에서 stage code override를 적용할 수 있다. Backend workflow stage name·DB seed를 변경하면 내 업무·알림 등 범위가 넓어지므로 금지한다.
- 최신 WIP migration `0055`를 수정하지 않고 새 additive `0056`을 사용해야 한다.

## 5. 권장 개발 순서

1. additive `0056` batch operation + event correlation schema와 migration test.
2. Backend contract·endpoint·store의 validation, lock, replay, 단계별 update/event/version.
3. Backend 성공·권한·scope·혼합 부적격·stale·replay·payload mismatch·동시 경쟁 tests.
4. Frontend additive types/API와 선택 분류·action bar·확인 sheet·feedback.
5. workflow 표시명 exact mapping과 unit tests.
6. isolated Full-Stack에서 여러 패널 batch → 나머지 자체 확인 → 제조 완료 → 기존 LQC/OQC 인계 보존 검증.
7. desktop·390px screenshot, 전체 regression, 종료 문서와 고정 검수 runtime 확인.

## 6. 완료 기준 보정

- 선택한 eligible panel 2개 이상에서 조립 단계까지 필요한 step이 원자적으로 확인된다.
- 같은 batch 안에서도 패널별 시작 위치가 달라질 수 있으며 실제 새로 확인된 step 수만큼 event와 version이 증가한다.
- batch event는 `batch_operation_id`로 operation과 연결되고 단건 event는 기존처럼 null이다.
- 이미 조립 완료 등 Frontend 제외 패널은 서버 payload에 포함되지 않지만, 포함된 payload가 stale/부적격이면 전체 rollback한다.
- batch 뒤 execution status는 계속 `InProgress`; 자체 확인·제조 완료·LQC/OQC 인계는 기존 계약대로 별도 수행한다.
- 표시명 exact text 4건과 Kitting optional suffix 미표시를 단언하고, 내부 `is_optional`·진행률은 회귀 검증한다.
- Open P0/P1/P2가 0이어야 구현 완료로 판정한다.

## 7. Review Resolution

| 항목 | 판정 | 2차 기획 지시 |
| --- | --- | --- |
| 사용자 목표·범위 | 유지 | 제조 조립 vertical slice와 표시명 4건만 구현 |
| 조립 식별 | 유지·보강 | immutable template version item code + sequence, 추측 금지 |
| 선행 단계 | 유지 | 조립까지 미완료 단계 함께 확인, sheet에서 명시 |
| 원자성 | 유지 | Frontend 사전 제외 + 서버 payload 전부 성공/실패 |
| audit | 추가 | event nullable batch FK와 replay projection |
| version | 추가 | 새로 확인한 step 수만큼 증가 |
| 사후 상세 입력 | 보류 | 별도 data lifecycle Task |
| 부분 성공·자동 시작·전체 완료 | 제거 | 부작용·인계 우회 금지 |
| 표시명 | 유지 | Frontend stage code exact override, 내부 source 불변 |

최종 판단: `GO_TO_SECOND_PLANNING`. 사용자 추가 결정은 필요하지 않으며 `blockingDecisionCount=0`이다.
