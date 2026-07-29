# TASK-010A Change 004 — 생산관리 메뉴 2탭 분리

## Task Identity Gate

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- canonicalTaskId: `TASK-010A`
- reuseExistingTask: `true`
- purposeIdentity: 생산관리 화면에 섞여 있는 생산계획 관리와 제조 투입 요청을 별도 탭으로 분리해 업무 목적과 action을 명확히 한다.
- gateStatus: `PASS_REUSE`
- explicitRoadmapOverrideApproved: `true` — experiment branch standing instruction과 사용자의 직접 수정 요청

## 구현 계약

- 생산관리 화면 최상단에 `생산계획`, `제조 투입` 두 탭을 제공한다.
- 생산계획 탭에는 KPI, 일정·담당자 조회/수정, 설정, Excel 기능만 표시한다.
- 제조 투입 탭에는 프로젝트 목록, 패널 선택, 키팅·입고 참고 상태와 제조 투입 요청만 표시한다.
- 탭 전환 시 펼친 프로젝트를 닫아 서로 다른 업무 영역의 상세 상태가 섞이지 않게 한다.
- 모바일도 같은 두 탭을 사용하되 표 축소가 아니라 기존 프로젝트 카드와 패널 카드 구조를 유지한다.
- Backend·DB·migration·알림 정책은 Change 003 계약을 그대로 사용하고 변경하지 않는다.

## 검증

- 두 탭의 접근성 role·선택 상태와 기능 분리를 unit test로 확인한다.
- 생산계획 탭에서 제조 요청이 보이지 않고 제조 투입 탭에서 Excel·계획 KPI가 보이지 않는지 확인한다.
- 실제 생산관리 UI에서 제조 투입 요청→제조 실행 Full-Stack을 재검증한다.
- Desktop 1440px·Mobile 390px screenshot과 horizontal overflow를 확인한다.

## 게시 경계

- local experiment source·test·screenshot·문서만 변경한다.
- commit·push·PR·merge·Persistent UAT·기존 42981/41164 runtime은 변경하지 않는다.
- main merge 승인 수는 `0/3`이다.
