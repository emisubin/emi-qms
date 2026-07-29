# DESIGN-000 Change 004 — PC UX/UI 평가 반영

## Task identity gate

- instructionChainRead: true
- taskType: P2_REMEDIATION
- canonicalTaskId: DESIGN-000
- changeId: change-004
- purposeIdentity: Change 003 PC 사용자 관점 평가에서 확인된 정보 우선순위·입력 밀도·권한 안내·선택 방식·공통 탐색 문제를 기존 기능과 상태 전이 변경 없이 보정한다.
- samePurposeMatchCount: 1
- canonicalReuseTarget: DESIGN-000
- roadmapSequenceMatch: true
- explicitRoadmapOverrideApproved: false
- gateStatus: PASS_REUSE
- branch: experiment/task-home-002-personalized-shell
- baselineHead: de8e05b
- mainMergeApprovalCount: 0/3

## 사용자 요청

`tasks/design-000-change-003-pc-ux-ui-evaluation.md`를 기준으로 PC 버전 UX/UI를 전체적으로 수정한다.

## Root finding

- 프로젝트·구매·자재의 첫 목록 행이 1280×720 첫 화면 아래에 있어 조회 업무의 시작이 늦다.
- 프로젝트 상세의 기본정보·병목 카드가 탭보다 먼저 많은 공간을 사용해 실제 부서 데이터 진입이 늦다.
- 읽기 전용 사용자는 권한 부족과 업무 선행조건 미충족을 구분하기 어렵다.
- 제조·품질의 현재 패널 선택과 Excel/일괄 처리 선택이 같은 체크박스 문법을 사용해 클릭 목적이 불명확하다.
- 영업 홈은 긴 그래프가 긴급 업무·Pending·알림보다 먼저 나와 사용자의 당일 행동 우선순위와 어긋난다.
- 화면마다 뒤로가기·빈 상태·보조 기능·입력 단계의 표현이 달라 학습 부담이 누적된다.

## 변경 계약

### 포함

- 공통 경로 안내, 읽기 전용 안내, 빈 상태와 보조 기능 표현
- PC 왼쪽 메뉴의 `내 업무 / 부서 업무 / 공통 조회 / 관리` 정보 구조
- 프로젝트·구매·자재 목록의 첫 화면 밀도 개선
- 프로젝트 상세 기본정보·병목 요약 압축과 sticky 부서 탭
- 제조·품질의 현재 패널 탐색과 다중 선택 모드 분리
- 생산계획 입력의 단계별 progressive disclosure와 저장 위치 유지
- 양식 관리의 조회 모드와 편집 모드 구분
- 영업 홈의 긴급 업무·Pending·알림 우선 배치
- 한국어 우선 레이블과 상태 의미 외 장식 문구 정리
- 관련 단위 테스트, 브라우저 시각 검증과 privacy-safe 증빙

### 제외

- API, DB, migration, 권한 계산, 업무 상태 전이, 알림 수명주기와 기존 입력 데이터 계약 변경
- 모바일 화면의 신규 설계
- Figma·외부 provider·Persistent UAT
- `App.tsx` 모듈 분해와 code splitting 같은 DESIGN-001 장기 구조 개선
- 대표 저장소·`main`·push·PR·merge

## 불변조건

- 현재 기능, 입력값, 저장·확정 API 호출과 deep link를 유지한다.
- 모든 부서는 기존 조회 권한을 유지하고 입력 가능 여부는 현재 권한 계산 결과를 따른다.
- 상태 의미 색상과 텍스트를 함께 제공하며 나머지는 흑백 와이어프레임을 유지한다.
- 프로젝트별 데이터와 패널별 데이터의 확정 경계를 변경하지 않는다.
- Excel 내보내기와 일괄 처리 기능은 삭제하지 않고 명시적 선택 모드 안으로 정리한다.
- 390px 기존 적응형 동작에 회귀를 만들지 않는다.

## 검증 계획

- 변경 컴포넌트 단위 테스트
- `npm run lint`
- `npm run typecheck`
- `npm test -- --run`
- `npm run build`
- 고정 검수 주소에서 1280×720 PC 핵심 동선 시각 검증
- 프로젝트·구매·자재 첫 목록 행, 프로젝트 상세 sticky 탭, 읽기 전용 안내, 제조·품질 선택 모드, 영업 홈 정보 순서 확인
- 390px에서 기존 모바일 동선과 수평 넘침 회귀 확인

