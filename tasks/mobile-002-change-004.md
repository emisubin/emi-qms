# TASK-MOBILE-002 Change 004 — 전 화면 모바일 단순화 검수

## 1. Task Identity Gate

- proposedTaskId: `TASK-MOBILE-002`
- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `DESIGN-000`
- roadmapNextGate: `DEFERRED_HOUSEKEEPING`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-MOBILE-002`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true` — 사용자가 완료 모바일 경험의 전 화면 재검수·수정을 직접 요청함
- gateStatus: `PASS_REUSE`

## 2. Purpose identity

- 업무 목표: PC 기능 전체를 모바일에 복제하지 않고 현장 조회·확인·단순 처리에 필요한 정보만 우선 노출한다.
- Root Finding: 기존 Change 002·003으로 화면 구조와 밀도는 개선됐지만, 선택 Excel export, 반복 Home widget, 관리용 보조 설명과 설정 action이 여러 모바일 route에 계속 노출된다.
- 변경·검증 경계: 공통 mobile presentation rule, Home·Sales·대표 운영/관리 route의 가시성·밀도, 390px screenshot과 overflow/accessibility 검수.
- 보존할 불변조건: 같은 URL·API·권한, 서버 mutation gate, 핵심 현장 action, 좌상단 drawer, 44px interactive target, page-level overflow 0, desktop 기능 무변경.
- 예상 산출물: 모바일 기능 분류표, 공통 simple-mode 규칙, 대표 route screenshot, regression tests, local commit.

## 3. 모바일 기능 분류

| 분류 | 모바일 처리 | 예시 |
| --- | --- | --- |
| 지금 판단 | 첫 화면 유지 | 상태·기한·차단·핵심 KPI·다음 action |
| 현장 처리 | 유지 | 업무 시작/완료, 검사, 사진, 입고, 제조·품질·물류 단계 처리 |
| 찾기·이동 | compact 유지 | 검색, 핵심 filter, 좌상단 전체 메뉴, detail 이동 |
| 반복 요약 | 하나로 통합 | Home의 긴급 Pending·알림은 우선 확인 panel에 합치고 별도 widget 반복 제거 |
| PC 관리 | 모바일 기본 화면에서 제외 | Excel 내보내기·대량 작업·목표 편집·전체 field 동시 조회 |
| 보조 설명 | 축약 또는 disclosure | 긴 소개문, 집계 기술 설명, 감사 metadata |

숨김은 권한을 바꾸거나 서버 기능을 제거하지 않는다. 사용자는 desktop에서 기존 기능을 그대로 사용할 수 있고, 모바일 핵심 route와 action은 유지한다.

## 4. 완료 기준

- 390px에서 Home은 부서 핵심/긴급/내 업무/프로젝트 순으로 구성되고 Pending·알림 중복 widget이 없다.
- 전 모바일 route에서 선택 Excel export action이 기본 작업면을 차지하지 않는다.
- Sales mobile은 연간 실제 graph·핵심 KPI 3개·month evidence disclosure를 사용하고 목표 편집은 PC 관리 기능으로 안내한다.
- mobile header의 eyebrow·긴 설명·보조 action 밀도를 줄이되 화면 제목과 오류·권한·action feedback은 보존한다.
- 390px page-level overflow 0, visible interactive target 44px, drawer·focus·safe-area 계약을 유지한다.
- desktop 1440px의 export·관리·전체 정보는 회귀하지 않는다.

## 5. 승인·안전 경계

- planningApproved: `true`
- implementationApproved: `true`
- commitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false` (`0/3`)
- Backend·DB·migration·Persistent UAT·provider·대표 repo·main: 변경 금지
