# TASK-OSAN-PROJECT-001 — 오산 프로젝트 등록과 진행 대상 생성

현재 후속 작업: [Change 007 — 미리보기 편집·부분 등록·중복 확인](osan-project-001-change-007.md). 사용자가 Change006 검수 중 요청한 보정을 진행한다. 아래 최초 등록 기능의 검수 완료와 구분한다.

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

## 2026-09-09 후속 change — 목록·상세 시각 스타일 통일

사용자 승인: ‘표, 정보와 같은 모든 요소의 배치는 그대로 하되 디자인만 다른 화면과 맞추기’. 이 보정의 사용자 검수는 아직 대기이며 위 최초 구현의 검수 완료와 구분한다.

- 오산 list/detail 경로에만 theme 표식을 두고 별도 `osan-project-theme.css`를 적용했다. 기존 공통 목록·정보·대상 표 컴포넌트와 모든 필드/이동/업무 동작은 그대로 사용한다. 청주·홈·진행 화면에는 적용하지 않는다.
- #282828 텍스트, #DA2127 주요 버튼·진행 막대, 흰 배경·#EEE 테두리, 6.8/8/15px 모서리와 기존 오산 요약 카드 그림자로 맞췄다. 파란 진행 중 배지는 중립 색상으로 통일하되 상태 문구와 완료 의미는 보존한다.
- display/grid/order/position/margin/padding/width/height 및 글꼴 크기·행간을 변경하지 않았다. 모바일 상단의 줄 배치와 기존 로고도 위치·정보 보존을 위해 유지한다.
- PC1440에서 제목/검색3개/요약3개/상태탭의 수정 전후 좌표·크기·글꼴이 모두 같음을 실제 DOM으로 비교했다. PC 상세 표 및 모바일390 목록·상세를 눈으로 확인했고, 상세 진행 막대 rgb(218,33,39), 가로넘침없음, 상세→목록 복귀를 확인했다.
- 프런트엔드만 변경하며 검수 Vite에 자동 반영된다. 홈 납기 조건 API 재시작 정책 차단은 별개로 남아 있고, 재시작·DB변경을 시도하지 않았다. 원격 반영·배포 없음.

검증: 기존 프로젝트 등록·목록·상세·대상 연결 테스트12/12 통과, TypeScript 포함 build 및 최종 CSS bundle build 통과. 기존 bundle 크기 경고는 유지한다. 작은 가역 스타일 보정으로 직접 검증하며 별도 신규 테스트·전체 회귀는 추가하지 않았다. 해당 스타일·scope·기록만 로컬 커밋한다.

## 2026-09-09 후속 change — 홈·현황·프로젝트 공통 페이지 구성

최신 사용자 지시는 요소별 색상 보정에서 나아가 홈을 기준으로 제목→설명→KPI→검색·필터→프로젝트 목록의 구성과 디자인을 동일하게 재사용하는 것이다. 앞의 전체 배치 불변 지시 중 상단 페이지 구성은 이 승인으로 갱신하며, 목록 본문의 표/필드와 상세 화면은 보존한다.

`OsanListFrame`을 추출하여 홈·진행 현황·프로젝트 메뉴가 같은 제목·설명·4개 KPI·검색·필터·목록 제목을 렌더한다. 설명 영역의 최소 높이와 공통 간격을 통일했다. 신규 프로젝트는 기존 권한의 제목 옆 action으로 유지하고, 기존 프로젝트의 납기 범위/상태 필터는 공통 필터 영역에서 유지한다. 프로젝트 8열 표와 모바일 정보 필드, 조회 API, 생성·상세·대상 연결은 기존 것을 사용한다. 청주 공통 구성은 변경하지 않는다. 오산 프로젝트 모바일 헤더도 홈·현황의 같은 EMI 로고·한 줄 표시를 사용한다.

PC1440에서 세 화면의 제목/설명/KPI/검색/목록 제목 y좌표와 높이가 실제로 동일했다(157/176/223/306/350, 높이8/36/52/26/8). 모바일390 프로젝트의 공통 구성·날짜 필터·가로넘침없음을 확인했다. 목록 theme는 공통 컨트롤을 덮지 않고 표/정보 카드에만 적용한다.

기존 구성에 의존하던 테스트를 새 공통 순서·4 KPI·상태 select에 맞춰 갱신했고, 검색·날짜·상태·생성·상세와 Dashboard/사업부 경계 테스트40/40 통과했다. 최초 실행의6실패는 폐기된 composition selector 및 중복 제목 기대였으며 보정 후 통과했다. 제목은 ‘프로젝트’, 목록 제목은 ‘프로젝트 목록’으로 구분했다. 독립 검토의 새 P0–P2 없음, 상세한 시각 확인은 직접 수행했다. 이전 홈 납기 API 재시작 차단은 유지하며 이번 서버 변경/재시작은 없다.

최종 확인: TypeScript 포함 build, 변경 컴포넌트/테스트 ESLint, 최종 CSS build 통과. 독립 reviewer 최종 GO. 모바일 날짜 라벨 줄바꿈도 보정 후 확인했다. 지정 파일만 로컬 커밋하며 원격 반영·배포는 하지 않는다.

## 2026-09-09 후속 change — 상세 상단 정보 표현 개편

사용자가 상세 기본 정보의 표를 제거하고 새로운 표현으로 바꾸되 아래 진행관리를 유지하도록 승인했다. 공통 `OsanPageHeading`을 분리해 목록과 상세가 제목/설명 표면을 재사용한다. 상세 상단은 상태·프로젝트명·코드의 식별 영역과 ‘프로젝트 정보(거래처·제품명·수량) / 문서 정보(PO·W/O) / 납기일’의 경계선 없는 정보 영역으로 변경했다. PC는 가로3영역, 모바일은 세로로 읽는다. 프로젝트 코드의 공백/대소문자와 모든 입력값을 보존하며 전체 정보 펼침 표와 상단 ‘진행 현황 열기’ 버튼을 제거했다.

추가 사용자 수정: 납기일 아래 전체 진행률을 제거하고 ‘납품 정보’를 ‘프로젝트 정보’로 변경했다. 정보 제목14px, 정보 본문15px로 확대했다. 하단 진행관리의 KPI·대상 목록·진행률·대상별 상세 연결은 유지한다. 모바일 상세 헤더도 기존 오산 목록/홈의 EMI 헤더에 맞췄다.

검증: PC1440·모바일390에서 기본 정보에 table없음, 상단 진행률/진행 현황 버튼없음, 모든 기본 필드와 하단 진행관리 표시를 확인했다. 가로 넘침없음. 기존 표 selector 테스트를 새로운 정보 영역과 필드/버튼 계약으로 보정하고 공백 검증을 정상화한 뒤 프로젝트/대시보드20개 통과. TypeScript 포함 build 통과(기존 bundle 경고 유지). 서버/DB/권한 변경없고 홈 API 재시작 차단은 별개로 유지한다. 지정 변경만 로컬 커밋하며 원격 반영·배포는 하지 않는다.

최종 추가 수정: 오산에는 단일 진행관리만 있으므로 ‘진행 관리’ 탭과 tablist/tabpanel 연결 속성을 제거했다. 기존 진행관리 제목·KPI·대상 표/카드·상세 연결은 유지한다. 실제 화면에서 탭0개, 진행관리 영역과 대상2개 유지 확인. 표 개편부터 이 추가 수정까지 같은 상세 보정 범위로 묶어 로컬 커밋한다.
