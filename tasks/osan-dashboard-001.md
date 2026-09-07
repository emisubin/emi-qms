# TASK-OSAN-DASHBOARD-001 — 전체 프로젝트 진행현황 대시보드

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- status: `PLANNED`
- parentTask: `TASK-OSAN-PILOT-001`
- implementationApproved: false
- runtimeMutationApproved: false
- gitPublicationApproved: false
- 선행조건: TASK-OSAN-PROGRESS-001 완료와 이 Task 구현 승인

## 목적과 계약

오산 전체 프로젝트의 단계별 부분 완료와 최종 완료를 한 화면에서 확인하게 한다.

[승인된 업무 기획](osan-pilot-001-planning.md), [독립 review resolution](osan-pilot-001-review.md), [상위 Task·순서](osan-pilot-001.md)를 함께 따른다. 이 파일은 별도 신규 인터뷰나 기획 재작성 요청이 아니다. 기획 문서화 승인은 실제 구현·runtime 실행 승인이 아니다.

## 포함 범위

- 사업부+프로젝트 권한 scope의 서버 aggregate 조회와 전체/시작 전/진행 중/완료 카드를 제공한다.
- 프로젝트 코드·Title·거래처·제품명·수량·납기일·상태·진행률·7단계별 완료 N/전체 N 표를 구현한다.
- Title·코드·거래처·제품명·PO/W/O 검색, 상태 필터, 미완료 우선·납기일 정렬과 pagination을 제공한다.
- 권한+검색 기준 카드 집계와 상태 필터 목록을 구분하고 현재 페이지의 행만으로 전체 집계를 계산하지 않는다.
- 진행률은 완료 단계/(N×7), 미완료 표시는 최대 99%, 전체 포장 완료만 100%로 처리한다.
- 프로젝트 선택 시 진행 관리로 이동하고 저장 후 최신 집계를 조회한다. desktop 표·390px 요약/펼침을 기존 디자인 체계로 구현한다.

## 조사·변경 경계

사업부-aware 프로젝트 aggregate API, Frontend 오산 home/dashboard와 검색/상태 query·tests.

실행 시 최신 instruction chain·Task identity·Roadmap·branch/runtime 상태를 읽고 exact 파일 allowlist와 검증 명령을 고정한다. 기존 WIP가 남은 현 branch에서 제품 개발을 자동 시작하거나 사용자의 WIP를 정리하지 않는다.

## 완료 기준

- [ ] 1개·여러 대상·여러 페이지·검색 결과에서 단계별 분자/분모와 카드 수 일치.
- [ ] 시작 버튼 직후 진행 중/0%, 마지막 포장 직전 미완료/최대99%, 최종 완료100%.
- [ ] 청주 데이터·권한 밖 프로젝트 count·검색 힌트 누출 0.
- [ ] loading/empty/error/retry·지연 응답·전환 cache·keyboard·390px 페이지 가로 넘침 0.
- [ ] 실제 단계 mutation은 진행 관리에만 있고 현황판에 중단·펜딩·별도 완료 버튼 없음.

## 다음 Task에 전달할 내용

Synthetic 데이터의 기대 집계와 사용자 화면 checklist를 Task 6에 전달한다.

실제 구현 결과·SOP·사용자 안내·검수 checklist·Roadmap 상태는 이 Task의 구현 보고에서 추적한다. 현재는 구현/테스트 미실행, 사용자 검수 적용 전이다. Sol xhigh가 승인 범위의 구현·테스트·범위 내 보정을 맡고 parent 및 fresh GPT-6 High가 검토한다. 모든 품질·Git·운영 gate는 Root 지침을 따른다.
