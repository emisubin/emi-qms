# TASK-MOBILE-002 Change 003 — 좌측 상단 숨김 메뉴와 모바일 Shape System

## 1. 사용자 요청과 승인 source

- 모바일 하단 고정 메뉴를 제거한다.
- 모바일 왼쪽 위 버튼을 누르면 메뉴가 나타나고, 다시 닫거나 화면을 선택하면 숨겨지는 구조로 바꾼다.
- 모바일 글씨·도형 크기와 정렬을 다듬어 작은 화면의 정보 밀도와 가독성을 함께 개선한다.
- 둥근 직사각형만 반복하지 않고 각진 직사각형, 타원, 원, 둥근 직사각형, 정사각형을 일관된 역할로 사용한다.
- 이 실험 branch에서는 별도 확인 없이 구현·검증·screenshot·local commit까지 진행한다.
- 대표 repo와 GitHub `main`에는 반영하지 않는다. main merge 승인은 현재 `0/3`이다.

## 2. Task Identity Gate와 유형

- instructionChainRead: `true`
- proposedTaskId: `TASK-MOBILE-002`
- canonicalTaskId: `TASK-MOBILE-002`
- samePurposeMatchCount: `1`
- reuseExistingTask: `true`
- decision: `PASS_REUSE`
- next change: `Change 003`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- roadmapExpectedTaskId: `TASK-007A`
- roadmapNextGate: `TASK-007A_FABLE_DEEP_INTERVIEW`
- roadmapSequenceMatch: `false`
- explicitRoadmapOverrideApproved: `true` — 사용자의 experiment branch 즉시 실행 지침
- Fable: `NOT_APPLICABLE` — 신규 업무 능력이 아니라 기존 승인 모바일 기능의 사용자 피드백 수정이다.

## 3. Purpose identity

- 업무 목표: 모바일의 지속 노출 하단 navigation을 왼쪽 상단 숨김 drawer로 교체하고 시각 체계를 정돈한다.
- Root Finding: 하단 bar가 콘텐츠를 가리고 화면을 앱처럼 고정하며, 카드 전반의 동일한 round rectangle 반복이 정보 위계를 약하게 만든다.
- 변경·검증 경계: Frontend presentation, 접근성, unit·isolated E2E·synthetic screenshot과 Task 문서만 변경한다.
- 보존할 불변조건: URL, API, 권한, Workflow, Desktop composition, 44px touch target, safe area, page overflow 0.
- 예상 산출물: source·test, 페이지별 390px screenshot, implementation report, local experiment commit.

## 4. 모바일 navigation 계약

1. `mobile-app-bar`의 가장 왼쪽에 44×44px 메뉴 버튼을 둔다.
2. 메뉴는 기본적으로 숨기고 버튼 클릭 시 왼쪽 drawer로 나타난다.
3. drawer는 권한으로 파생된 모든 navigation item을 한 목록에 표시하고 현재 화면을 명확히 표시한다.
4. 메뉴 선택, 닫기 버튼, backdrop, Escape로 닫을 수 있다.
5. 열릴 때 첫 메뉴로 focus하고 Tab focus를 drawer 안에 가두며 닫은 뒤 trigger로 focus를 복귀한다.
6. drawer가 열려 있는 동안 body scroll을 잠그고 `100dvh`·safe-area를 적용한다.
7. desktop sidebar는 변경하지 않는다.

## 5. Shape System

| 도형 | 역할 | 적용 예 |
| --- | --- | --- |
| 각진 직사각형 | 강한 구조·현재 위치·핵심 action | 메뉴 trigger, active drawer item, 중요 header 일부 |
| 타원형 | 선택·상태·filter | 상태 button, badge, tab/filter control |
| 원형 | count·긴급 신호·단일 icon | notification count, drawer 장식, 순위 marker |
| 모서리 둥근 직사각형 | 일반 업무 묶음·form | 기본 업무 card, search/filter surface |
| 정사각형 | 짧은 KPI·식별 marker | 요약 수치, drawer navigation marker |
| 비대칭/절단형 | 예외·강조 card | 지연·경고 KPI, 교차 카드 변형 |

도형은 임의 장식이 아니라 정보 역할에 따라 반복한다. 긴 문장이 들어가는 card를 억지 원이나 타원으로 만들지 않는다.

## 6. 타이포·정렬 계약

- app bar와 page header의 기준선을 8px 계열 간격으로 정렬한다.
- page title 18~20px, section title 14~16px, body 11~13px, helper 10~11px 범위를 유지한다.
- KPI 수치와 label은 시각 중심을 맞추고 카드별 높이를 동일하게 유지한다.
- 긴 제목은 두 줄 이내, 보조 설명은 한 줄 이내로 제한하되 상세 접근 경로를 보존한다.
- 작은 시각 크기와 별개로 모든 button은 최소 44×44px hit area를 유지한다.

## 7. 포함·제외

### 포함

- 모바일 app bar 메뉴 trigger와 왼쪽 drawer
- 하단 fixed navigation 제거와 이에 종속된 하단 여백·sticky action 위치 보정
- 모바일 전역 shape·type·alignment token과 대표 workspace 적용
- navigation accessibility unit/E2E, 390px 화면별 screenshot, 1440px desktop 회귀

### 제외

- Backend·API·DB·migration·permission·workflow 변경
- Persistent UAT·실제 provider·runtime handover
- URL·인증·session 변경
- 대표 repo·`origin/main`·push·PR·merge

## 8. 승인 상태

- planningApproved: `true` — 사용자 직접 변경 요청과 기존 TASK-MOBILE-002 계약
- implementationApproved: `true` — 본 Change 003 범위 한정
- userValidationCompleted: `false`
- commitApproved: `true` — 검증 완료 뒤 local experiment commit
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false` (`0/3`)

## 9. 완료 기준

- 390px에서 하단 fixed navigation이 존재하지 않고 왼쪽 상단 trigger가 44×44px 이상이다.
- drawer의 open/close/navigation/focus trap/Escape/focus restore와 권한별 menu가 동작한다.
- sticky action이 화면 하단 safe area를 침범하지 않는다.
- 원·타원·정사각형·각진/둥근 직사각형·비대칭 도형이 대표 화면에서 역할별로 확인된다.
- 모바일 typography, KPI/card 정렬, overflow 0과 visible button 44×44px을 통과한다.
- desktop sidebar·화면 구조가 유지된다.
- typecheck, lint, unit, build, isolated E2E와 page screenshot 검수가 통과한다.
