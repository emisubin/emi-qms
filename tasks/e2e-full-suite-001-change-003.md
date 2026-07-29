# TASK-E2E-FULL-SUITE-001 change-003

## Gate

- instructionChainRead: true
- taskType: P2_REMEDIATION
- canonicalTaskId: TASK-E2E-FULL-SUITE-001
- gateStatus: PASS_REUSE
- purposeIdentity: 프로젝트 상세 부서 탭의 실데이터·담당자 수정 진입, 알림 누적 완화, 생산계획 초기 로딩 중 입력 덮어쓰기 방지
- roadmapSequenceMatch: false
- explicitRoadmapOverrideApproved: true
- overrideSource: 사용자가 현재 experiment branch에서 생산관리·설계·구매 탭을 기준으로 나머지 부서 탭과 두 번째 UX Finding을 즉시 보정하도록 명시
- sourceBranch: experiment/task-home-002-personalized-shell
- sourceHead: 9250384b330a9f524bd964d01da8f00ed661ab75
- mainMutationAllowed: false
- remoteMutationAllowed: false
- persistentUatAllowed: false
- actualProviderAllowed: false

## 확인된 원인

1. 생산관리·설계·구매 탭은 프로젝트별 API 데이터를 직접 표시하지만 영업·자재·제조·품질·물류 탭은 workflow 단계 집계만 재사용해 실제 부서 데이터가 보이지 않는다.
2. 알림은 전체 읽음과 개별 읽음만 있어 프로젝트 단위 정리와 오래된 목록 접기가 없고, 상세/이동으로 확인해도 읽음 처리가 별도라 미확인 알림이 누적된다.
3. 생산계획 수정 화면은 여러 초기 API 응답을 기다리는 동안 입력 잠금과 stale response 폐기 경계가 없어 늦은 초기 응답이 빠른 입력을 덮어쓸 수 있다.

## 승인된 변경 범위

1. 영업·자재·제조·품질·물류 탭에서 해당 프로젝트의 실제 정산·입고/키팅·제조·검사·출하 데이터를 직접 표시한다.
2. 기존 서버 권한과 각 데이터의 `canMutate`를 source of truth로 사용해 담당자는 프로젝트가 유지된 전용 편집/업무 화면으로 즉시 이동하고, 비담당자는 조회 전용 안내를 본다.
3. 알림은 이력을 삭제하지 않고 프로젝트별 최근 항목 우선 접기, 프로젝트별 모두 읽음, 상세/이동 시 자동 읽음으로 누적 부담을 줄인다.
4. 생산계획 수정은 최신 초기 요청이 모두 완료될 때까지 입력을 잠그고 이전 요청 응답을 폐기한다.

## 불변조건과 제외 범위

- 모든 부서는 데이터를 조회할 수 있지만 mutation은 기존 Backend 권한과 담당자 범위 밖으로 확대하지 않는다.
- workflow, work item, notification, Pending 상태 전이와 idempotency 계약을 변경하지 않는다.
- 알림 원문과 audit 이력은 삭제하지 않는다.
- 신규 DB migration, 실제 Teams·메일 provider 호출, 대표 repo, `main`, push, PR, merge와 Persistent UAT는 제외한다.

## 검증 계획

- Backend Release build와 알림 endpoint 관련 테스트
- Frontend lint, typecheck, unit, production build
- isolated synthetic full-stack에서 프로젝트 상세 모든 부서 탭의 직접 데이터, 담당자 작업 진입, 알림 정리와 생산계획 loading lock 검증
- desktop·390px 화면의 구조, overflow, console/request failure 확인과 privacy-safe screenshot 생성
