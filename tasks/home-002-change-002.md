# TASK-HOME-002 Change 002 — 전 부서 업무 메뉴 조회와 reference 기반 shell 보정

## 1. Task Identity Gate

- proposedTaskId: `TASK-HOME-002`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-UX-001 A2`
- roadmapNextGate: `TASK_UX_001_A2_FABLE_2_PASS_PLANNING`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-HOME-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_REUSE`

사용자 최종 검수에서 공통 왼쪽 메뉴가 부서 권한에 따라 숨겨져 있고 reference보다 card·shadow·여백이 커서 정보 밀도와 시각 구조가 기대와 다르다는 Finding이 확인됐다. 완료된 `TASK-HOME-002`를 새 Task로 복제하지 않고 다음 change로 재개한다. 사용자 요청은 이 change를 `TASK-UX-001 A2`보다 먼저 완료하는 명시적 실험 순서 승인이다.

### Purpose identity

- 업무 목표: 모든 active 부서 사용자가 전체 업무 흐름 메뉴를 발견하고 자신의 project access 범위에서 조회하되, 담당 권한이 없는 저장·상태 변경·업로드·Excel mutation은 사용할 수 없게 한다. 공통 shell과 Home·목록 시각 구조는 사용자 reference의 compact한 정보 밀도와 정렬을 EMI 색감으로 재구현한다.
- Root Finding 또는 정책 결정: `navigationItems`가 `Pending.Read`, 자재·제조·품질·물류 mutation permission으로 메뉴 자체를 숨겨 Product Roadmap 3.5의 “나머지 부서는 조회만 가능” 원칙과 충돌한다. 기존 HOME-002 시각 결과는 큰 status card·강한 shadow·두꺼운 강조선 때문에 reference의 얇은 header/sidebar·낮은 shadow·compact 목록 구조를 충분히 살리지 못했다.
- 변경·검증 경계: 공통 desktop/mobile 업무 navigation, 필요한 read endpoint의 `projects.read`·project access scope, 각 화면 mutation UI gate, 공통 shell·Home·목록 surface CSS, unit/backend/full-stack/browser 검증과 privacy-safe screenshot.
- 보존할 불변조건: actual/effective identity 분리, Backend mutation policy authoritative, project access scope, 개인정보·관리자 역할 경계, HOME-001 widget과 HOME-002 profile lifecycle, 동일 URL 모바일 적응형, 대표 repo·`main`·Persistent UAT·provider·push·PR·merge 제외.
- 예상 산출물: change 문서, 구현·자동 검증·desktop/mobile screenshot, implementation report·Roadmap·완료 원장·checklist 갱신, experiment local commit.

### 검색 범위

- [x] `tasks/`의 HOME-002·UX-001 planning/review/change/implementation report
- [x] Product Roadmap 실행 큐·3.5 권한 원칙·추적 항목·Decision Log
- [x] 실험 완료 원장과 현재 source/test
- [x] Local/remote branch와 worktree
- [x] Open/merged PR fixed projection

동일 목적 canonical Task는 `TASK-HOME-002` 1건이다. 같은 목적의 open PR은 없고 현재 worktree는 clean한 이름 있는 experiment branch다.

## 2. 기준선과 승인 경계

- branch: `experiment/task-home-002-personalized-shell`
- changeBaseCommit: `8f1ce8ad86acc66f0db7cfc891f80539ee313a71`
- representativeMainCommit: `b8f3e2104074d05c2e71999c08a7374e8729f68f`
- implementationApproved: `true` — 이 문서의 fixed 범위에 한정
- commitApproved: `true` — experiment local commit만
- userValidationCompleted: `false`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`

## 3. 전 부서 조회 계약

### 항상 표시할 업무 메뉴

active 사용자에게 다음 업무 navigation을 같은 순서로 표시한다.

1. 홈
2. 내 업무
3. 프로젝트
4. Pending
5. 생산관리
6. 구매
7. 자재
8. 제조
9. 품질
10. 물류
11. 알림

`관리자`는 부서 업무 메뉴가 아니라 사용자·권한·감사 개인정보를 다루는 시스템 역할 메뉴이므로 기존 `System Administrator`/관리자 read permission 경계를 유지한다. 일반 부서에 관리자 데이터 read 권한을 확대하지 않는다.

### 조회와 입력 분리

- 모든 active 업무 사용자는 기존 `projects.read`와 project access scope 안에서 업무 목록·상세를 조회한다.
- 현재 read endpoint가 mutation permission을 요구하는 경우 GET만 `projects.read`와 기존 scope로 분리한다. POST/PUT/PATCH/DELETE 정책은 변경하지 않는다.
- 화면의 저장·완료·등록·검사·발송·설정·업로드·삭제·선택 export 등 mutation action은 기존 capability boolean과 ReviewSafe gate로 숨김 또는 비활성화하고 “조회만 가능합니다”를 표시한다.
- 임의 project read-all, 다른 담당 프로젝트, 관리자 사용자·감사 정보, 삭제 데이터·매출 민감 column 권한은 확대하지 않는다.
- 메뉴 노출은 권한을 대신하지 않으며 직접 API mutation은 기존 server policy가 최종 차단한다.

## 4. 디자인 보정 계약

사용자가 제공한 두 reference에서 다음 시각 문법을 가져오고 브랜드 blue 대신 EMI red·soft red를 사용한다.

- 전체 높이 흰 sidebar와 얇은 오른쪽 divider, 작은 logo/브랜드 lockup, compact icon+label menu.
- active menu는 낮은 채도의 soft-red rounded rectangle 하나로 표시하고 두꺼운 세로선·강한 떠오름 효과를 제거한다.
- top header는 낮은 높이, 얇은 아래 divider, 작은 actual-user account control로 구성한다.
- API/Database/User 개발 상태는 사용자 업무보다 앞서는 대형 card가 아니라 접을 수 있거나 낮은 보조 strip으로 축소한다.
- Home hero와 widget은 큰 독립 panel을 반복하지 않고 title/tabs/filter/list/card가 같은 정렬선에 놓이는 compact workspace로 재배치한다.
- border는 1px, shadow는 낮고 좁게, radius는 과도하게 크지 않게 통일한다. 상태 badge·pill·원형 avatar·각진 table/list row를 혼합해 hierarchy를 만든다.
- desktop은 넓은 canvas와 compact table/list, mobile은 별도 drawer와 2~3열 핵심 metric·stacked list를 사용하며 PC 축소판으로 만들지 않는다.
- 전체 업무 페이지의 domain 정보 구조를 재설계하지 않되 공통 `page-header`, `card`, `table`, `filter`, `button` surface가 reference와 같은 시각 밀도를 갖도록 shared style을 보정한다.

## 5. 검증과 완료 기준

- sales·quality·read-only synthetic persona에서 11개 업무 메뉴가 모두 보이고 각 destination이 blank/403 없이 조회된다.
- 타 부서 persona에서 mutation control이 없거나 disabled이며 직접 mutation API는 403/기존 정책으로 차단된다.
- Pending·자재(IQC 포함) GET은 `projects.read`와 project access scope를 벗어나지 않는다.
- 관리자 메뉴와 관리자 read API는 일반 부서에 열리지 않는다.
- desktop·390px에서 sidebar/drawer, account UI, Home, 대표 list/table의 overflow 0·focus·contrast를 검증한다.
- Backend·Frontend 전체 suite, fresh migration baseline, Full-Stack E2E를 통과하고 Open P0/P1/P2가 0이다.
- screenshot과 종료 산출물을 갱신하고 experiment local commit만 수행한다.

## 6. 제외 범위

- 신규 업무 상태·DB concept·migration·external provider.
- 사용자·권한·관리자 개인정보의 일반 부서 공개.
- project access scope 또는 매출·삭제·audit 민감 권한 확대.
- Figma asset 복제, reference 상표·색상 복사.
- 대표 repo·`main`·Persistent UAT·runtime handover·push·PR·merge.
