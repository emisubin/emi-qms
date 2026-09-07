# TASK-OSAN-PROJECT-001 — 오산 프로젝트 등록과 진행 대상 생성

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- status: `USER_VALIDATION_COMPLETE`
- userValidationStatus: `COMPLETE`
- userValidationSource: `USER_EXPLICIT_2026-09-07_CURRENT_TASK_VALIDATION_COMPLETE`
- parentTask: `TASK-OSAN-PILOT-001`
- implementationApproved: true
- implementationApprovalSource: `USER_EXPLICIT_APPROVAL_2026-09-07`
- predecessorValidationDisposition: `TASK_2_USER_VALIDATION_DEFERRED_TO_FINAL_BATCH_BY_USER`
- localCommitPolicy: `AUTO_ON_IMPLEMENTATION_COMPLETE`
- runtimeMutationApproved: false
- gitPublicationApproved: false
- 선행조건: 충족 — TASK-OSAN-ISOLATION-001 완료, TASK-OSAN-ACCESS-001 자동 검증·parent review·fresh 독립 검증 GO 및 local commit `2e29938`; 사용자는 Task 2 직접 검수를 마지막 일괄 검수로 미루고 이 Task 구현을 승인함

## 목적과 계약

8개 정보만으로 프로젝트와 수량별 진행 대상을 원자적으로 만들고 중간 업무 없이 진행 준비를 끝낸다.

[승인된 업무 기획](osan-pilot-001-planning.md), [독립 review resolution](osan-pilot-001-review.md), [상위 Task·순서](osan-pilot-001.md)를 함께 따른다. 이 파일은 별도 신규 인터뷰나 기획 재작성 요청이 아니다. 기획 문서화 승인은 실제 구현·runtime 실행 승인이 아니다.

## 포함 범위

- Title·코드·거래처·PO No·W/O No·납기일·자유 제품명·수량의 오산 생성 계약·화면·목록·상세를 추가한다.
- PO/W/O 선택·중복 허용, 나머지 필수, 오산 내 코드 중복 차단·양의 정수 수량을 적용한다. 기존 코드 정규화·안전 한도를 확인해 exact handoff에 기록한다.
- 청주 Item/FAT/포장방식/영업·생산관리 담당자 입력이나 완화된 청주 API로 오산 생성을 처리하지 않는다.
- 오산 공통 양식과 7단계를 연결하고 제품명마다 Item을 생성하지 않는다. 프로젝트·대상 N개·진행 단계 snapshot·접근 연결을 같은 transaction으로 저장한다.
- 생성 후 진행 대기 화면으로 연결하되 실제 작업 시작은 기록하지 않는다. 생성 시 설계·구매·품질·물류·펜딩 work item을 만들지 않는다.
- 오산 프로젝트 생성의 인앱 이벤트는 business-unit/local 프로젝트 접근과 수신자 정책을 적용한다.

## 조사·변경 경계

Projects contracts/store/endpoints, common progress template adapter·snapshot/업무 profile, Frontend 생성·목록·상세, migrations·tests.

실행 시 최신 instruction chain·Task identity·Roadmap·branch/runtime 상태를 읽고 exact 파일 allowlist와 검증 명령을 고정한다. 기존 WIP가 남은 현 branch에서 제품 개발을 자동 시작하거나 사용자의 WIP를 정리하지 않는다.

## 완료 기준

- [x] 입력 8개만 표시, 제품명 자유 입력, PO/W/O 빈값·앞자리 0·기호 보존.
- [x] 수량 1/N·0·음수·소수·한도 초과, 공백 필수값·오산 중복 코드 검증.
- [x] 중복 요청·생성 중 실패에서 전체 rollback; 다른 사업부의 동일 코드 생성 허용.
- [x] N개 대상과 각 7단계가 정확하고 청주 선행 업무·외부 알림이 생성되지 않음.
- [x] 생성자는 접근 연결을 얻고 등록은 `Project.Create`와 `projects.read`를 모두 요구한다. 진행 mutation은 이번 Task에 열지 않았으며 Task 4에서 별도 권한으로 구현한다.
- [x] 생성 뒤 접근이 회수되면 GET과 동일 operation replay가 모두 `403`이고 detail과 관련 row를 노출·변경하지 않으며, 접근 복원 뒤 동일 project replay가 성공한다. `Project.Read.All`은 기존처럼 접근행 없이 허용한다.
- [x] 목록과 상세의 프로젝트 코드는 대소문자와 내부 공백을 화면에서도 그대로 구분해 표시한다.
- [x] Change 002에서 목록과 상세를 청주형으로 정렬하고, Change 003에서 별도 오산 UI를 제거했다. Change 004에서는 목록 행·카드, 상세 요약과 부서 현황의 같은 React 표시 컴포넌트를 직접 사용하도록 통합했다. Change 005에서는 목록 page header, 검색·납기 filter, KPI, 상태 tab과 content 순서까지 공용 composition으로 통합하고 1440×900·390×844 양쪽 화면을 눈으로 비교했다. 오산은 승인된 `전체·시작 전·완료` 조회만 제공하며 Pending·보류·취소·삭제·Excel·선택 내보내기와 mutation은 추가하지 않는다. 단일 `진행 관리` tab에서 생성 직후 상태는 `시작 전`, 진행은 현재 단계와 `완료 수/7`로 요약하며 desktop/mobile에서 코드 대소문자·내부 공백을 보존한다.

## 다음 Task에 전달할 내용

### Review resolution — OSAN-REVIEW-001

기존 입력 정규화는 코드의 앞뒤 공백 제거이며, 기존 DB의 unique는 코드가 아니라 활성 Title에 적용됨을 확인했다. 오산 코드의 unique 비교값은 앞뒤 공백을 제거한 원문으로 고정하고 대소문자·내부 공백을 추가 정규화하지 않는다. 오산 DB/profile에 해당 비교값의 DB unique를 추가해 동시 생성도 차단한다. 완료 프로젝트를 포함한 동일 오산 코드의 중복을 허용하지 않는다.

청주 Title unique 계약은 유지하고, 오산에는 승인되지 않은 Title 중복 금지를 자동 적용하지 않는다. 기존 migration을 수정하지 않고 새 migration/별도 오산 생성 모델로 profile 경계를 보장한다. 같은 Title/다른 코드 허용, 같은 코드/다른 Title 거부, 앞뒤 공백·대소문자·내부 공백 비교, 동시 생성에서 한 요청만 성공하는 테스트를 추가한다. 이 문장은 review resolution 작성 당시의 구현 전 계약이며, 실제 구현·검증 결과는 아래 구현 보고에서 추적한다.

프로젝트·대상·snapshot·profile 생성 계약을 Task 4에 전달한다. 이 Task 완료를 사용자 진행 기능 전체 완료로 표시하지 않는다.

실제 구현 결과·SOP·사용자 안내·검수 checklist·Roadmap 상태는 [구현 보고](osan-project-001-implementation-report.md)에서 추적한다. Change 001의 Sol xhigh 구현과 parent 검토 뒤 fresh GPT-6 High 최종 검증이 반환한 제품 P2를 보정했고 전체 자동 검증을 통과했다. Change 002의 별도 오산 UI는 Change 003에서 제거했으며, Change 004에서 목록 행·카드와 상세 표시 영역을 공용화했다. 사용자 검수에서 목록 page 전체 구성이 다름을 확인한 뒤 Change 005로 제목, 검색·납기 filter, KPI, 상태 tab과 목록 순서까지 같은 composition으로 통합했다. 같은 run의 desktop/mobile paired screenshot 8개와 열린 local 화면을 눈으로 확인했고 fresh GPT-6 High가 `PASS / GO`, 최종 open P0/P1/P2/P3 `0/0/0/0`을 반환했다. 사용자는 2026-09-07 Change 005가 반영된 현재 Task 3 화면의 검수 완료를 명시했다. Push·PR·merge·Persistent UAT·provider·운영 적용은 별도 승인이다.
