# TASK-MOBILE-002 Change 005 — 의미 기반 도형 통일

## 1. Task Identity Gate

- proposedTaskId: `TASK-MOBILE-002`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-EXPORT-001`
- roadmapNextGate: `OPTIONAL_COLUMN_PICKER_USER_REQUEST`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-MOBILE-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true` — 사용자가 완료된 모바일 디자인의 도형 의미 통일을 직접 요청함
- gateStatus: `PASS_REUSE`

검색 범위는 Roadmap·실험 완료 원장·TASK-MOBILE-002 Change 003~004·DESIGN-000·Frontend source/test·local/remote branch와 worktree다. GitHub PR 조회는 현재 실행 정책상 승인 없이 호출할 수 없어 local/remote ref와 tracked source를 기준으로 중복을 판정했다.

## 2. Purpose identity

- 업무 목표: 모바일에서 같은 의미의 정보와 상태가 같은 도형 문법을 사용하도록 전 화면 시각 체계를 통일한다.
- Root Finding: menu·프로젝트·패널·KPI·검사 항목에 도형이 index와 `nth-child` 순서로 배정돼 같은 상태가 서로 다른 도형으로 보이고, 다른 상태가 우연히 같은 도형을 사용한다.
- 변경·검증 경계: DESIGN-000 semantic shape token, mobile navigation·KPI·project/panel/status/action surface, 390px 대표 route와 desktop 회귀.
- 보존할 불변조건: 업무 데이터·상태·권한·action·URL·API·DB·migration, 44px target, overflow 0, desktop 기능.
- 예상 산출물: 의미→도형 mapping, random/index shape 제거, component·CSS·test, desktop/mobile screenshot, implementation report, local experiment commit.

## 3. Semantic shape 계약

| 의미 | 도형 | 적용 예시 |
| --- | --- | --- |
| 정보·업무 묶음 | 8px 둥근 직사각형 `surface` | card, field group, list row |
| 이동·일반 조작 | 6px 직사각형 `control` | menu icon, tab, button, input |
| 현재 선택·진행 | blue rounded square `active` | active route, 선택 panel/project |
| 상태·분류 | 타원형 `status` | badge, filter, short state label |
| 개수·순번 | 원형 `count` | count, step number, avatar |
| 주의·차단 | 우상단 절단형 `warning` | blocked, Pending, failed |
| 완료·성공 | 원형 marker `success` + surface | completed/pass/confirmed |

긴 문장과 여러 field를 담는 card 자체를 원이나 타원으로 만들지 않는다. `success`와 `count` 원형은 compact marker에 사용하고 content container는 `surface`를 유지한다.

## 4. 완료 기준

- menu·project·panel card의 도형이 배열 index가 아니라 semantic state/role에서 결정된다.
- `nth-child`로 임의 변형하던 mobile card·tab·KPI·검사 항목은 동일 역할끼리 같은 geometry를 사용한다.
- warning·success·active·status·count가 대표 route에서 같은 computed geometry로 검증된다.
- 390px overflow 0, 주요 target 44px, desktop 기능·layout 회귀 없음.
- Backend·DB·migration·Persistent UAT·대표 repo·main·push·PR·merge는 변경하지 않는다.

## 5. 승인·게시 경계

- planningApproved: `true` — 기존 MOBILE-002·DESIGN-000 계약 + 사용자 직접 수정 요청
- implementationApproved: `true`
- commitApproved: `true` — 현재 experiment branch local commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false` — main merge 승인 `0/3`
