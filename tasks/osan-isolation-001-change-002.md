# TASK-OSAN-ISOLATION-001 Change 002 — 잔여 테스트 안정성 마무리

- taskType: `BUGFIX`
- status: `IMPLEMENTED_VERIFIED_USER_VALIDATION_PENDING`
- approvalSource: 사용자 “이번 1단계에 남은 마무리 작업 진행하라”
- approvalDate: 2026-09-06
- gateStatus: `PASS_REUSE`
- roadmapSequenceMatch: true
- productionRuntimeMutationApproved: false
- gitPublicationApproved: false
- parentSessionModel: `GPT-5`
- implementerModelRequested: `gpt-5.6-sol/xhigh`
- implementerModelObserved: `NOT_REPORTED`
- verifierModelRequested: `gpt-6-astra/high`
- verifierModelObserved: `NOT_REPORTED`

## 목적과 범위

Change 001의 최초 Frontend unit 2건과 Full-Stack 1건이 대상 재실행에서 통과한 상태를 마무리한다. 제품 동작 결함인지 aggregate 부하에서의 테스트 동기화 문제인지 구분하고, 승인된 세 테스트 파일만 최소 보정한다.

- `frontend/tests/App.test.tsx`
- `frontend/tests/G2Navigation.test.tsx`
- `frontend/e2e/full-stack/project-registration.full-stack.spec.ts`

제품 source, Backend, migration, 전역 test timeout과 업무 assertion은 변경하지 않는다. 실제 Azure·Persistent UAT·provider, Commit·Push·PR·Merge와 원격 `main` 병합은 포함하지 않는다. 검사 container와 image cleanup은 승인 범위지만 자동 승인 정책을 우회하지 않는다.

## 원인과 변경

변경 전 두 Frontend 파일을 함께 반복했을 때 10회 중 9회 통과, 1회 실패해 aggregate 부하에 따른 변동성을 재현했다. 일관된 제품 결함이나 mock 누수는 확인되지 않았다.

- 한 테스트에 묶인 독립 App route 계약을 세 테스트로 나눠 각 계약이 기존 10초 안에서 끝나게 했다.
- G2 첫 화면은 동일한 `G2 홈` 제목을 해당 assertion에서 최대 5초 기다린다. 전역 timeout은 그대로다.
- 프로젝트 재활성 검증은 확인 클릭 전에 해당 POST와 프로젝트 상세 GET 응답 대기를 등록하고, 두 응답 성공과 최종 `진행` badge를 확인한다. 재시도·mock 상태 변경은 추가하지 않았다.

## 검증 결과

| 검증 | 결과 |
| --- | --- |
| 변경 후 선택 테스트 stress | 32회 × 4개 사례 = 128/128 PASS, 가장 느린 tests 구간 4.40초 |
| 두 Frontend 파일 전체 | 90/90 PASS, 23.46초 |
| 재활성 Full-Stack 시나리오 | fresh synthetic 환경에서 3/3 PASS, 49.9초, exit 0 |
| Frontend 전체 unit | 33 files, 252/252 PASS, 34.81초, exit 0 |
| Frontend typecheck | PASS, 17.81초, exit 0 |
| 변경 3파일 lint·diff check | PASS |
| 독립 정적 검토 | PASS, 신규 P0/P1/P2/P3 없음 |

합성 DB·실행 container·network는 정리됐고 staged file, tracked screenshot 변경, 신규 screenshot·trace·video와 실행 중 test process는 0이다. 제품 source가 바뀌지 않아 Backend 575/575와 Full-Stack 전체 64개는 다시 실행하지 않았으며 Change 001의 실제 결과를 보존한다. Frontend build도 제품 source·설정·의존성 변경이 없어 이번에는 재실행하지 않았다. 최초 실패 이력도 삭제하지 않는다.

`OSAN-TEST-TIMING`은 `P3 / RESOLVED`다. 테스트 실행 단위와 비동기 응답 대기를 보정했고 기존 assertion을 유지한 반복 검증과 전체 Frontend unit이 통과했다. 개별 최초 실패의 환경 원인을 모두 확정했다는 의미는 아니다.

## 남은 경계

사용자 직접 검수는 계속 대기다. 검사 container와 image 정리를 재시도했으나 container 삭제가 자동 승인 검토에서 `approval required by policy, but AskForApproval is set to Never`로 거부되어 둘 다 보존했다. 우회하지 않았으며 `OSAN-LOCAL-ARTIFACT-CLEANUP / P3 / BLOCKED_TOOL_APPROVAL`을 유지한다.

현재 세션 parent는 GPT-5로 실행됐다. 구현은 지정된 Sol xhigh context, 독립 검토는 별도 GPT-6 High context에 요청했고 두 도구 모두 실제 모델 관측값을 반환하지 않아 `NOT_REPORTED`로 기록한다.
