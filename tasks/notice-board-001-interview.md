# TASK-NOTICE-BOARD-001 — Home 공지사항 게시판 Deep Interview

- taskType: `NEW_FEATURE`
- interviewOwner: `WAIVED_BY_USER_FOR_EXPERIMENT`
- orchestrationOwner: `CODEX`
- interviewRound: 0
- interviewStatus: `COMPLETED_CONFIRMED`
- userConfirmed: true
- openBlockingDecisionCount: 0
- planningApproved: false
- implementationApproved: false

이 문서는 `experiment/*` fast-track에서 사용자-facing interview를 생략한 근거와 Fable 1차 기획 입력을 고정한다. 사용자는 Home 상단·중앙을 유지하고 하단 `프로젝트 병목`만 누구나 입력 가능한 공지사항 게시판으로 교체하도록 명시했다. 이 branch와 대화의 standing instruction에 따라 비차단 정책은 Fable 권장안을 자동 채택하고 `Fable 1차 기획 → Codex review → Fable 2차 기획 → Codex 구현·검증·screenshot·local commit`까지 이어간다. 대표 repo·`main`·Persistent UAT·실제 provider·push·PR·merge는 포함하지 않는다.

## 1. Task Identity Gate

- proposedTaskId: `TASK-NOTICE-BOARD-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `NONE`
- roadmapNextGate: `첨부·사진 storage/검역/보존/backup·restore`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `0`
- canonicalTaskId: `TASK-NOTICE-BOARD-001`
- reuseExistingTask: `false`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `USER_EXPLICIT_NOTICE_BOARD_REQUEST`
- policyInputResolution: `FABLE_RECOMMENDATION_AUTO_ADOPT`
- gateStatus: `PASS_CREATE`

### Purpose identity

- 업무 목표: 로그인한 사용자가 Home 하단에서 공지사항을 읽고 직접 작성할 수 있는 전사 게시판을 사용한다.
- Root Finding: 기존 Home 하단의 프로젝트 병목 Top 5는 프로젝트 수가 늘수록 홈의 고정 공간에서 대표성이 떨어지며, 현재 일반 사용자가 직접 작성하는 공용 게시판 능력은 없다.
- 변경·검증 경계: Home 상단 부서 KPI와 중앙 내 업무·Pending·알림은 그대로 유지하고, 하단 병목 widget만 공지 목록·작성 진입점으로 교체한다. 공지 persistence/API/권한/UI와 desktop·390px 검증만 포함한다.
- 보존할 불변조건: 승인 대기·비활성 사용자는 mutation 불가, Backend가 권한과 validation을 강제, 기존 인앱 알림·Teams 채널 공지·외부 delivery와 내 업무를 자동 생성하지 않음, 사용자 identity와 작성 이력을 보존, 기존 migration 수정 금지.
- 예상 산출물: Fable 1차 기획 원문, Codex review, Fable 2차 기획 원문, additive migration·Backend API·Frontend Home 게시판, 권한·validation·동시성·회귀 검증, desktop/mobile screenshot, 종료 문서와 local experiment commit.

### 검색 범위

- [x] `tasks/`의 Task·planning·review·change·implementation report
- [x] Product Roadmap 실행 큐·추적 항목·Decision Log
- [x] Local/remote branch와 worktree
- [x] Open/merged PR

`TASK-HOME-001/002`는 읽기 전용 Home 요약과 개인화 shell만 완료했다. `TASK-NOTIFY-003`의 `ChannelNotice`는 시스템 관리자의 수동 외부 채널 발송이며 일반 사용자의 Home 게시판이 아니다. 동일 목적 Task·branch·worktree·PR은 없고, 사용자의 이번 구체적 구현 지시를 현재 Roadmap보다 우선하는 명시적 실험 순서 변경 승인으로 기록한다.

## 2. 확정된 Repository 기준선

- Home 상단의 로그인 부서별 KPI와 중앙의 내 업무·Pending·알림 widget은 유지한다.
- Home 하단 `프로젝트 병목`은 `listProjects(pageSize=5)`를 읽는 넓은 widget이며 이번 요청에서만 제거 대상이다. 프로젝트 목록·상세의 병목 집계 자체는 유지한다.
- 기존 `ChannelNotice`는 `notifications`와 TeamsChannel delivery를 사용하는 관리자 수동 발송 기능이다. Home 게시판 글을 그 경로에 자동 연결하면 외부 provider 설정과 unread 알림 계약이 섞이므로 기본 재사용 대상으로 보지 않는다.
- 앱은 로그인 완료·active 사용자만 업무 화면에 진입한다. 개발 사용자 전환과 actual 사용자 identity를 분리하는 기존 shell 계약을 유지한다.
- Backend가 mutation 허용과 입력 validation의 authoritative source이며 Frontend는 중복 submit·오류 focus·접근 가능한 feedback을 제공해야 한다.
- 최신 실험 migration은 `0051`; 신규 persistence가 필요하면 additive 다음 번호를 사용하고 Persistent UAT에는 적용하지 않는다.
- 모바일은 PC 표를 축소하지 않고 최신 공지와 작성 행동을 한 열에 배치하며 page-level overflow 0을 지킨다.

## 3. 해결할 문제와 기대 결과

- 현재 문제: Home 하단이 프로젝트 병목 목록에 고정돼 프로젝트가 많아질수록 홈에서 모든 사용자에게 공통으로 가치 있는 정보를 전달하기 어렵다.
- 기대 결과: 로그인한 사용자가 최신 공지를 빠르게 읽고 같은 위치에서 공지를 작성하며, 작성자·부서·작성 시각을 확인할 수 있다.
- 사용자 가치: 별도 외부 채널이나 관리자 요청 없이 조직 공지를 Home에서 공유한다.
- 실패 시 영향: 공지와 외부 알림이 섞여 불필요한 unread·Teams 발송이 생기거나, 익명·무제한 입력과 수정 경계 부재로 책임과 이력이 불명확해질 수 있다.

## 4. Fable 권장안이 확정할 비차단 정책

아래 항목은 사용자에게 다시 묻지 않는다. Fable이 선택지·trade-off와 Repository 근거 권장안을 제시하고 Codex review 뒤 Fable 2차 기획에서 확정한다.

1. v1 동작 범위: 작성·목록·상세만 제공할지, 작성자 수정·삭제와 관리자 moderation까지 포함할지.
2. 표시 정책: Home 최신 몇 건, 본문 preview 길이, 전체 보기/inline 상세와 모바일 구성.
3. 작성 권한: 승인된 active 사용자 전체를 허용하되 read-only 개발 persona와 검수 전환 identity를 어떻게 다룰지.
4. 데이터 lifecycle: 작성자·부서 snapshot, 수정 이력, soft delete·보존과 동시성 방식.
5. 입력 계약: 제목·본문 길이, 공백/개행 정규화, 링크·HTML 처리와 field validation.
6. 알림 경계: 게시판 작성이 인앱 알림·내 업무·Teams·메일을 만들지 않는 기본안과 향후 opt-in 확장 경계.
7. 조회 정책: 최신순 pagination, Home 부분 실패·empty·retry, 전체 게시판 route 필요 여부.

## 5. 포함 후보

- Home 하단 `프로젝트 병목` widget을 공지사항 widget으로 교체
- 최신 공지 목록과 작성자·부서·작성 시각 표시
- 승인된 active 사용자 전체의 공지 작성
- 게시판 목록·상세·작성 UI와 독립 loading·empty·error·success 상태
- Backend authoritative validation과 작성 identity 저장
- additive schema/API, desktop·390px UI와 합성 E2E

## 6. 명시적 제외

- Home 상단 부서 KPI와 중앙 내 업무·Pending·알림 변경
- 프로젝트 목록·상세의 병목 집계 제거 또는 상태 계산 변경
- 공지 작성 시 내 업무·인앱 알림·Teams·메일 자동 발송
- 첨부파일·사진·댓글·반응·읽음 확인 통계
- 실제 provider, 대표 repo·`main`, Persistent UAT migration/runtime handover
- push·PR·merge와 main merge 승인

## 7. 성공 기준

- 승인된 active 사용자는 Home에서 최신 공지를 보고 작성할 수 있고, 비승인·비활성·인증되지 않은 요청은 서버에서 차단된다.
- Home 상단·중앙 widget의 데이터·배치·deep link는 바뀌지 않고 하단 병목 widget만 공지사항으로 교체된다.
- 공지 작성은 인앱 unread, 내 업무 또는 외부 delivery를 생성하지 않는다.
- 제목·본문 validation 실패와 중복 submit은 사용자 행동이 가능한 한글 안내로 처리된다.
- desktop과 390px에서 목록·작성·상세가 읽기 쉽고 page-level horizontal overflow가 없다.
- Backend 전체, Frontend lint/typecheck/unit/build, fresh·existing migration, isolated Full-Stack와 browser screenshot이 통과한다.
- Open P0/P1/P2가 0이고 5종 종료 산출물과 사용자 검수 대기 상태를 추적한다.

## 8. 승인·안전 경계

- planningApprovedForExperiment: `true` — standing instruction과 Fable 권장안 자동 채택 조건
- implementationApprovedForExperiment: `true` — Fable 2차 기획의 blocking decision 0인 범위
- localCommitApproved: `true`
- pushApproved: `false`
- prApproved: `false`
- mergeApproved: `false`
- mainMergeApprovalCount: `0/3`
- persistentUatApproved: `false`
- externalProviderApproved: `false`
