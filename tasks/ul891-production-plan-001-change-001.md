# TASK-UL891-PRODUCTION-PLAN-001 — change-001

- changeType: `EXPERIMENT_FAST_TRACK_SECOND_PLANNING_APPROVAL`
- taskType: `NEW_FEATURE`
- source: `USER_EXPLICIT_EXPERIMENT_RULE`
- requestedAt: `2026-07-28`
- branch: `experiment/task-home-002-personalized-shell`
- baseHead: `a7651b5c266d73be48e76861a02910435c1371fe`
- instructionChainRead: `true`
- taskIdentityGate: `PASS_CREATE`
- roadmapSequenceMatch: `true`
- explicitRoadmapOverrideApproved: `true`
- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/44-ul891-set-production-plan.md`
- implementationApproved: `true`
- localCommitApproved: `true`
- pushApproved: `false`
- pullRequestApproved: `false`
- persistentUatApproved: `false`
- mainMergeApprovalCount: `0/3`

## 사용자 요청

UL891은 프로젝트 생성 시 실제 세트 인스턴스와 개별 패널이 생성되므로 생산계획도 실제 세트 단위로 입력·조회할 수 있어야 한다. 프로젝트 상세의 생산관리 탭에서 `전체`와 세트를 전환하면 생산계획표와 계획·실적 일정표가 함께 같은 범위로 전환되어야 한다.

사용자는 이 experiment branch의 신규 기능에 대해 사용자-facing interview와 중간 승인을 생략하고 `Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot → local commit`을 연속 실행하도록 명시했다. 이 change는 그 standing instruction에 따른 2차 기획 및 구현 승인 기록이다.

## Fable 1차 기획 사용량

### 1차 기획 호출 직전

- Claude 5시간 세션: 사용 `5%`, 잔여 `95%`, 초기화 `2026-07-28 16:10 KST`
- 주간 전체 모델: 사용 `8%`, 잔여 `92%`, 초기화 `2026-08-01 08:00 KST`
- 주간 Fable: 사용 `15%`, 잔여 `85%`

### 1차 기획 호출 직후

- Claude 5시간 세션: 사용 `5%`, 잔여 `95%`, 초기화 `2026-07-28 16:10 KST`
- 주간 전체 모델: 사용 `15%`, 잔여 `85%`, 초기화 `2026-08-01 08:00 KST`
- 주간 Fable: 사용 `23%`, 잔여 `77%`

## Codex review resolution

1. 세트 계획 단위는 세트 사양이 아니라 실제 실물 세트 인스턴스로 확정한다.
2. `전체`는 활성 세트들의 읽기 전용 집계이며 같은 내용을 별도로 이중 입력하지 않는다.
3. 프로젝트 공통 생산계획 항목과 실적 연결은 한 벌만 유지하고, 세트별 기간·담당자·필요 인원·코멘트는 별도 scope/value overlay로 저장한다.
4. 기존 프로젝트의 항목·필수 여부·순서·실적 연결 편집 능력은 `계획 구조` 편집으로 보존하고, 세트별 값은 `세트 일정` 편집으로 분리한다.
5. 제조·LQC·OQC·전진검수·FAT·포장·출발·납품은 선택 세트의 패널만 집계한다. 구매·자재·IQC는 프로젝트 공통 source로 표시하고 전체 집계에서 중복하지 않는다.
6. 기존 UL891 세트형 LinkedV1 프로젝트는 기존 공통 계획 값을 각 세트 scope에 복사해 backfill한다. 이후 추가된 세트는 빈 일정 값으로 시작한다. Legacy와 비-UL891은 기존 프로젝트 단위 계획을 유지한다.
7. 생산계획 완료 판정은 화면, 프로젝트 목록, 전체 흐름과 Excel에서 같은 모델 분기를 사용한다. UL891 세트형은 모든 활성 세트의 필수 계획 항목 기간이 입력되어야 완료다.
8. 취소 세트의 값과 audit는 보존하되 조회 전용으로 전환하고 활성 집계에서 제외한다.
9. Fable 1차 기획의 plan item 복제안은 실적 연결 FK와 기존 편집 계약을 깨므로 채택하지 않는다.
10. Codex review의 blocking decision과 open P0/P1/P2는 모두 0이다.

## 구현 허용 범위

- additive migration과 기존 UL891 세트형 프로젝트 backfill
- Backend 세트 scope 조회·값 저장·권한·CAS·audit·실적 filtering·aggregate
- 신규 프로젝트 및 후속 세트 추가 시 scope 생성
- 프로젝트 상세 생산관리 탭의 전체/세트 selector와 생산계획표·일정표 동시 전환
- 기존 프로젝트 공통 계획 구조 편집과 세트 일정 편집 분리
- Workflow·목록·Excel 완료 판정 정합
- Backend/Frontend/migration/isolated Full-Stack 회귀 검증
- desktop·390px privacy-safe screenshot
- 이 experiment branch의 local commit

## 제외 및 안전 경계

- 대표 repository, GitHub `main`, push, PR, merge
- Persistent UAT migration·runtime handover
- 실제 Teams·메일·provider
- master 양식 version 정책 재설계
- 비-UL891과 평면 UL891의 기존 프로젝트 단위 계획 변경
- 패널별 제조·품질·물류 처리 단위 변경

## 2차 기획 완료 조건

- Fable은 1차 기획, Codex review와 현재 Repository를 직접 다시 읽는다.
- 최종 문서는 `docs/44-ul891-set-production-plan.md`에 별도 원문으로 기록한다.
- review resolution을 반영하고 blocking decision이 0이면 별도 사용자 확인 없이 구현으로 이어간다.

## Fable 2차 기획 사용량

### 2차 기획 호출 직전

- Claude 5시간 세션: 사용 `23%`, 잔여 `77%`, 초기화 `2026-07-28 16:10 KST`
- 주간 전체 모델: 사용 `9%`, 잔여 `91%`, 초기화 `2026-08-01 08:00 KST`
- 주간 Fable: 사용 `17%`, 잔여 `83%`

### 2차 기획 호출 직후

- Claude 5시간 세션: 사용 `23%`, 잔여 `77%`, 초기화 `2026-07-28 16:10 KST`
- 주간 전체 모델: 사용 `17%`, 잔여 `83%`, 초기화 `2026-08-01 08:00 KST`
- 주간 Fable: 사용 `10%`, 잔여 `90%`

주간 Fable 표시는 호출 직전 `17% 사용`에서 호출 직후 `10% 사용`으로 감소했다. 같은 CLI의 `/usage` 재측정 원문을 그대로 기록하며, 재계산 또는 계량 갱신으로 보이는 이 역행값을 임의 보정하지 않는다.
