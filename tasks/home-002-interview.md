# TASK-HOME-002 — 개인화 Home·프로필 셸 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `FABLE_5`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false
- fastTrackSource: `USER_EXPLICIT_EXPERIMENT_RULE`

이 문서는 `experiment/*` fast-track 예외에 따라 사용자-facing interview 없이 작성한 기획 source다. 사용자는 이 실험 계보에서 신규 기능을 인터뷰·중간 확인 없이 권장안으로 기획·검토·구현하고, 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge를 제외하도록 명시했다.

## 0. Task Identity Gate

- proposedTaskId: `TASK-HOME-002`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-UX-001 A2`
- roadmapNextGate: `TASK_UX_001_A2_FABLE_2_PASS_PLANNING`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-HOME-002`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 모든 업무 화면의 공통 셸에 로그인 사용자의 사진·부서·이름과 계정 popover를 제공하고, Home에서 로그인 사용자의 부서에 맞는 핵심 지표를 우선 표시한다.
- Root Finding 또는 정책 결정: 현재 상단은 개발 사용자 selector와 중복 자재 shortcut이 차지하고 실제 로그인 사용자 맥락이 약하다. 기존 Home은 공통 4개 widget만 있어 부서별 첫 판단을 지원하지 않는다. 프로필 사진 업로드는 신규 binary lifecycle·보안 경계가 필요하다.
- 변경·검증 경계: 공통 desktop/mobile shell, 본인 profile photo 조회·교체, logout 진입점, dev selector 재배치, Home 부서 지표, synthetic isolated Backend/Frontend/E2E 검증과 screenshot.
- 보존할 불변조건: 기존 HOME-001의 내 업무·병목·Pending·알림 widget과 permission redaction, 동일 URL 모바일 적응형, Backend 권한 authoritative, Entra actual/effective user 분리, 운영 dev selector 비노출, 18단계 업무 규칙, 대표 repo·`main`·Persistent UAT·실제 provider·게시 제외.
- 예상 산출물: Fable 1차 planning, Codex review, Fable 2차 planning, 구현·검증·desktop/mobile screenshot, implementation report·SOP·User manual·Roadmap update·user validation checklist, local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

동일 목적 Task·branch·worktree·open/merged PR은 0건이다. `TASK-HOME-001`은 공통 4-widget read-only Home, `DESIGN-001`은 기존 로그인 시각 언어의 전체 화면 통일로 완료됐으며 이번 신규 프로필·부서 지표 능력과 목적이 다르다. 사용자의 “먼저 홈 화면 개선부터” 요청을 현재 experiment의 Roadmap 순서 override로 기록한다.

## 1. Round 기록

| Round | Fable 상태 | 질문 수 | 사용자 답변 기록 | 다음 단계 |
| ---: | --- | ---: | --- | --- |
| 0 | `COMPLETED_CONFIRMED` | 0 | 사용자 명시 요구와 experiment 권장안 자동 채택 | Fable 1차 planning |

## 2. 업무 문제와 기대 결과

- 현재 공통 상단의 개발 사용자 selector는 검수 목적과 실제 로그인 사용자 맥락을 섞어 보여 준다.
- 사용자는 현재 로그인한 사람의 프로필 사진, 부서명과 이름을 모든 업무 화면에서 즉시 확인할 수 없다.
- 로그아웃 진입점과 본인 프로필 사진 변경 경로가 계정 메뉴로 묶여 있지 않다.
- Desktop 왼쪽 고정 메뉴가 viewport 높이를 완전히 사용하지 않고, 개발 사용자 변경 control이 업무 header에 있어 실제 사용자 정보와 경쟁한다.
- Home은 모든 사용자에게 동일한 4개 공통 widget만 보여 부서별로 가장 먼저 볼 수치가 무엇인지 답하지 못한다.
- 성공하면 사용자는 어느 화면에서든 자신의 로그인 맥락을 확인하고 계정 메뉴에서 로그아웃·사진 변경을 수행하며, Home 첫 화면에서 자신의 부서 핵심 지표와 다음 행동을 본다.

## 3. 사용자 명시 요구

### 공통 셸

- 전체 업무 페이지의 오른쪽 위에 로그인 사용자의 프로필 사진, 부서명, 이름을 표시한다.
- 프로필 사진을 누르면 사진·부서명·이름·로그아웃 버튼이 있는 popover를 연다.
- popover 안의 프로필 사진을 누르면 새 사진을 선택·업로드할 수 있다.
- 현재 개발 사용자 변경 control은 Desktop 왼쪽 고정 메뉴의 맨 아래로 옮긴다.
- Desktop 왼쪽 고정 메뉴는 viewport의 위·아래를 꽉 채운다.
- 오른쪽 위의 자재 shortcut은 중복 메뉴이므로 삭제한다. 왼쪽/모바일 navigation의 자재 메뉴는 유지한다.

### Home

- 기존 HOME-001 공통 4개 widget은 재구현하지 않고 보존한다.
- 로그인 사용자의 부서에 따라 가장 중요한 부서 핵심 지표와 다음 행동을 Home 상단에 추가한다.
- 부서가 없거나 알 수 없고 허용 지표가 없으면 임의 다른 부서 지표를 보여 주지 않고 공통 Home만 유지한다.
- System Administrator는 업무 부서를 가장하지 않고 시스템 운영 지표 또는 명확한 공통 상태를 사용한다.

### Mobile

- 구체 구현안은 Fable·Codex 권장안을 자동 채택한다.
- 동일 URL 적응형 계약, 390px overflow 0, keyboard/focus와 44px touch target을 유지한다.
- Desktop popover를 단순 축소하지 않고 mobile account sheet 또는 동등한 모바일 전용 구성을 우선 검토한다.
- 개발 사용자 변경은 mobile drawer의 하단 검수 영역에 두고 실제 로그인 사용자 card와 시각적으로 분리한다.

### 디자인 reference

사용자가 제공한 2개의 reference 화면에서 다음 구성 원칙을 가져오되 EMI red·white 색감과 공식 제품명을 유지한다.

- 얇고 단정한 top header, viewport 전체 높이의 밝은 sidebar, 넓은 content canvas.
- active navigation은 옅은 브랜드색 배경의 단순한 pill/rounded rectangle로 강조.
- 화면 제목·filter/tab·content card의 명확한 수직 hierarchy와 과도한 장식이 없는 여백.
- 얇은 border, 낮은 shadow, compact control, 선명한 상태 badge와 정돈된 grid/table/card 정렬.
- reference의 파란색은 EMI red·soft red로 치환하고 기존 로그인 shell의 브랜드 자산은 보존한다.
- 이번 Task의 전면 수정 범위는 공통 shell과 Home layout이다. 모든 업무 화면의 정보 구조·기능을 다시 설계하지 않는다.

## 4. 대상 사용자와 권한

| 사용자/역할 | 필요한 행동 | 조회 범위 | 변경 범위 | 권한·감사 요구 |
| --- | --- | --- | --- | --- |
| Active 로그인 사용자 | 본인 사용자 정보 확인, account menu 열기, 본인 사진 변경, 로그아웃 | 본인 actual/effective profile과 허용 Home 지표 | 본인 profile photo만 | Backend self-scope 강제, 교체 audit 또는 동등한 추적 |
| 승인 대기 Entra 사용자 | 본인 프로필·로그아웃 | 본인 profile만 | Fable 권장안에 따른 사진 변경 허용 여부 | 업무 data 접근 차단 유지 |
| System Administrator 검수 사용자 전환 | actual 로그인 사용자와 effective 업무 persona를 혼동 없이 확인 | actual account와 effective 업무 context | 본인 actual profile photo만 | dev persona impersonation 확장 금지 |
| Dev mode 사용자 | synthetic profile·부서·이름 확인, dev selector 사용 | 선택한 dev persona | 실제 Entra profile을 만들지 않음 | Development/Testing 전용 |

## 5. Data·storage·lifecycle

- 기존 `/api/me`의 display name, department, actual/effective user projection을 우선 재사용한다.
- profile photo는 신규 binary 저장 능력이다. Fable은 저장 위치, 허용 format/크기/dimension, decode·검역, 교체·삭제, cache, 권한, audit, 사용자 삭제 lifecycle, backup·restore와 rollback을 최소 안전 vertical slice로 제안해야 한다.
- Roadmap의 사진 storage·검역·보존·backup blocker를 무시하지 않는다. 실험 범위에서 안전하게 닫을 수 없으면 `openBlockingDecisionCount`를 1 이상으로 남긴다.
- 부서 지표는 확인 가능한 기존 source를 재사용하는 것을 우선하고, 여러 화면의 payload를 무차별 병렬 호출하지 않는다. 필요하면 permission-aware read-only aggregate endpoint를 검토한다.
- Home 지표는 사용자 부서/권한 밖 count·title·next action을 노출하지 않는다.
- profile photo 변경은 외부 provider 발송을 만들지 않는다.

## 6. 정상·예외·복구 흐름

- 정상: 페이지 진입 → 오른쪽 위 사용자 identity 표시 → avatar 선택 → account menu → 사진 변경 또는 로그아웃.
- 사진 변경: 파일 선택 → client/server validation → 업로드 중 중복 차단 → 성공 시 새 사진 즉시 반영 → 실패 시 기존 사진 보존과 action 인접 오류·재시도.
- 취소: file picker 또는 account menu를 닫으면 기존 사진과 업무 화면 상태를 유지한다.
- fallback: 사진이 없거나 load 실패면 사용자 이름 기반 이니셜 avatar를 표시한다.
- Home: 로그인 사용자 부서 확인 → 허용된 부서 지표 로드 → metric card 선택 시 기존 원본 업무 화면으로 이동.
- 부분 실패: 사용자 사진 또는 부서 지표 실패가 공통 navigation과 기존 HOME-001 widget을 막지 않는다.
- identity 전환: dev/effective user 변경 시 이전 사진·지표의 늦은 응답을 폐기한다.

## 7. 포함·제외 범위

### 포함

- 공통 desktop/mobile 로그인 사용자 identity surface와 account menu/sheet.
- 본인 profile photo 선택·검증·업로드·조회·fallback.
- 로그아웃 action의 기존 MSAL/dev 계약 재사용.
- dev selector의 Desktop sidebar 맨 아래와 mobile drawer 하단 재배치.
- viewport full-height Desktop sidebar, 중복 자재 top shortcut 제거.
- 부서별 Home 핵심 metric과 원본 route deep link.
- 공통 shell·Home의 reference 기반 layout 재설계.
- loading·empty·error·success, 접근성, 권한, identity stale response, Desktop·390px 검증.

### 제외

- 모든 업무 페이지의 기능·정보 구조 재설계.
- 관리자 대리 profile photo 변경, 사용자 directory/gallery.
- Microsoft Graph profile photo 동기화와 신규 Graph permission.
- 실제 provider, 운영 storage service, Persistent UAT 적용·migration handover.
- 대표 repo·`main`·push·PR·merge.
- 기존 HOME-001 widget 사용자 설정·예측·추천.

## 8. 선택과 결정

| 번호 | 주제 | 권장안 자동 채택 범위 | Blocking |
| ---: | --- | --- | --- |
| 1 | profile photo storage·lifecycle | Repository와 실험 안전 경계에 맞는 최소 persistence·validation·교체·삭제 정책을 Fable이 제안 | 안전 경계를 닫지 못하면 Yes |
| 2 | 부서 지표 source | 기존 API 재사용과 작은 aggregate endpoint를 비교해 권한·호출 비용이 나은 안을 채택 | No |
| 3 | Desktop account menu | avatar trigger + 이름/부서 요약 + logout + photo change | No |
| 4 | Mobile account UI | 상단 avatar trigger + bottom sheet, dev selector는 drawer footer 분리 | No |
| 5 | 디자인 밀도 | reference의 밝은 full-height shell·compact hierarchy를 EMI red 계열로 적용 | No |

## 9. 성공 기준

- 모든 active 업무 route에서 로그인 사용자 사진/부서/이름이 표시되고 account menu가 keyboard·pointer로 동작한다.
- 본인 사진 변경은 안전 validation과 self-scope를 통과한 경우만 성공하며 실패·취소 시 기존 사진이 보존된다.
- logout은 기존 인증 정책을 우회하지 않는다.
- dev selector는 desktop sidebar/mobile drawer footer에만 있고 운영에서는 노출되지 않는다.
- 중복 자재 top shortcut이 제거되고 navigation의 자재 메뉴는 유지된다.
- Home은 department별 핵심 metric을 permission-aware하게 표시하고 기존 4개 widget을 보존한다.
- Desktop과 390px에서 overflow 0, focus 복귀, Escape/바깥 클릭 닫기, 44px target과 reduced motion을 검증한다.
- synthetic isolated 환경의 screenshot과 자동 테스트를 완료하고 P0/P1/P2 0 상태로 local commit한다.

## 10. 사용자 확인

- [x] 요청한 공통 셸·프로필·Home·디자인 목표가 기록됐다.
- [x] 모바일 상세안은 Fable·Codex 권장안 자동 채택으로 위임됐다.
- [x] 기존 완료 Home·Design 범위와 신규 능력을 분리했다.
- [x] 대표 repo·`main`·Persistent UAT·실제 provider·게시를 제외했다.
- [x] 실험 Roadmap override가 명시됐다.
