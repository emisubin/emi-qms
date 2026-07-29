# TASK-MOBILE-002 Change 002 — 전체 모바일 화면 정보 구조·밀도 전면 개편

## 1. 사용자 요청과 승인 source

- 현재 모바일 화면은 PC의 정보를 모두 세로로 펼친 반응형 화면에 가까워 실제 모바일 사용자가 보기 어렵다.
- 모바일 사용자가 이동 중 가장 먼저 판단하고 처리할 내용을 기준으로 모든 현재 화면의 정보 순서와 구성을 다시 만든다.
- 글씨와 비상호작용 도형을 줄여 첫 화면에 핵심 내용을 더 많이 담되 가독성과 터치 안전성을 보존한다.
- 필요하면 화면 구성뿐 아니라 모바일 디자인도 바꿀 수 있다.
- 이 실험 branch에서는 추가 인터뷰·중간 승인 없이 구현, 검증, screenshot과 local commit까지 진행한다.
- 대표 repo와 GitHub `main`에는 반영하지 않는다. main merge 승인은 현재 `0/3`이다.

## 2. Task Identity Gate와 유형

- canonical Task: `TASK-MOBILE-002`
- samePurposeMatchCount: `1`
- decision: `PASS_REUSE`
- next change: `Change 002`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true` — 사용자의 experiment branch 즉시 실행 지침 재확인
- Fable: `NOT_APPLICABLE` — 신규 제품 능력 기획이 아니라 기존 승인 기능의 사용자 피드백 수정이며, 기존 Fable planning·Codex review·구현 결과가 기준선이다.

## 3. Root cause

1. 1차 구현은 Home, My Work, Project list/detail, Pending list/detail, Notifications 7개 route만 모바일 전용 composition으로 만들었다.
2. 생산계획·구매·자재·IQC·관리 화면은 PC용 field/card를 모바일에서 한 열로 펼쳐 정보량은 유지했지만 우선순위를 만들지 못했다.
3. 큰 hero, 큰 card padding, 큰 수치와 반복 설명이 첫 viewport를 차지해 실제 업무 목록과 action이 늦게 나타난다.
4. 모바일에서 필요한 것은 PC의 동시 가시성 전체가 아니라 `현재 상태 → 예외/기한 → 다음 action → 필요한 상세` 순서다.

## 4. 모바일 정보 계층 계약

### Tier A — 첫 viewport

- 현재 위치를 알려 주는 짧은 제목
- 긴급·미완료·기한·차단 같은 핵심 수치 최대 2~4개
- 지금 처리할 대표 목록 또는 다음 action 1개
- 검색·filter는 한 줄 trigger 또는 compact control로 노출

### Tier B — 기본 스크롤

- 업무 판단에 필요한 식별자, 상태, 담당, 기한과 수량
- 각 카드의 대표 action
- 반복 항목은 작은 card/list 또는 가로 summary strip으로 제공

### Tier C — 필요할 때 펼치기

- 설명문, 감사·기술 metadata, 전체 보조 field, bulk/Excel/admin 보조 action
- `상세 정보`, `추가 작업`, sheet 또는 accordion을 통해 계속 찾을 수 있게 한다.
- PC 정보의 모든 field를 모바일 첫 화면에 동시에 펼치는 parity는 요구하지 않는다. 권한과 authoritative data 자체는 제거하지 않는다.

## 5. 모바일 시각 밀도 계약

- app bar: 약 52px 수준의 compact header
- page title: 19~21px, 본문: 12~13px, label/helper: 10~11px
- 기본 page gap: 10~12px, card padding: 10~12px, card radius: 12~16px
- KPI는 가능한 경우 2~4열 compact strip으로 표시하고 큰 설명문은 숨기거나 축약한다.
- 비상호작용 badge·도형·여백은 축소한다.
- 상호작용 요소는 시각적으로 작게 보이더라도 최소 44×44px hit area를 유지한다.
- 390px 기준 수평 overflow 없이 하단 navigation과 주요 action이 겹치지 않아야 한다.

## 6. 포함 범위

- 전역 mobile shell, Home, My Work, Project list/detail/create/edit, Pending list/detail, Notifications
- Teams Activity와 notification/delivery detail
- 생산계획 dashboard/settings/edit/read/calendar
- 구매 dashboard/settings/edit/read
- 자재 입고, IQC, 관련 action sheet
- Panel 정보/detail/edit와 mobile Excel preview
- 관리자 dashboard/users/departments/holidays/permission/audit/work history/manual notification/delivery/escalation 화면
- desktop 861px 이상 composition 보존
- synthetic isolated E2E와 route/module별 mobile screenshot

## 7. 제외·불변조건

- Backend·API·DB·migration·workflow state·permission·audit contract 변경 없음
- Persistent UAT·실제 provider·runtime handover 변경 없음
- URL과 desktop 기능 삭제 없음
- 대표 repo, `origin/main`, GitHub main, push, PR, merge 변경 없음
- main merge 승인 `0/3`; 3회 별도 승인 전 merge 금지

## 8. 승인 상태

- planningApproved: `true` — 사용자 직접 변경 요청과 기존 planning 기준
- implementationApproved: `true` — 본 change 범위 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 검증 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false` (`0/3`)

## 9. 완료 기준

- 390px에서 core 7개와 생산·구매·자재·IQC·Teams·Admin 대표 화면이 모바일 전용 밀도와 정보 순서를 사용한다.
- 첫 viewport에 제목, 요약/긴급 신호, 검색·filter 또는 실제 첫 업무 카드가 들어온다.
- 카드 한 개가 PC field 전체 때문에 viewport 대부분을 차지하지 않는다.
- 보조 정보와 action은 유실하지 않고 progressive disclosure로 접근할 수 있다.
- 44px touch target, keyboard/focus, safe area, horizontal overflow 계약을 보존한다.
- 1440px desktop reference의 구조·기능이 유지된다.
- typecheck, lint, unit, build, isolated E2E와 screenshot 검수가 통과한다.
