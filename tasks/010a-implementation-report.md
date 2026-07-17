# TASK-010A 패널별 키팅·제조 내 업무 연결 구현 보고

## 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-010a-panel-kitting`
- implementation / automaticValidation: `완료`
- userValidation: `대기`
- commit: `완료 — local experiment commit`
- push / PR / merge: `미승인·미실행`
- main merge approval: `0/3`
- Persistent UAT / provider / 대표 repo 영향: `없음`

## Task 목적·기획 source

자재 담당자가 입고 조건을 충족한 프로젝트의 활성 패널을 일부 또는 일괄 선택해 키팅 완료하고, 같은 transaction에서 패널별 제조 내 업무를 정확히 한 번 생성하도록 했다.

Authoritative implementation contract는 Fable 2차 기획 [docs/17-panel-kitting-plan.md](../docs/17-panel-kitting-plan.md)다. Fable 1차 원문은 [010a-planning.md](010a-planning.md), Codex 내용 review와 resolution은 [010a-review.md](010a-review.md), fast-track 승인·사용량은 [010a-change-001.md](010a-change-001.md)에 분리 보존했다.

## 포함·제외 범위

포함:

- active 구매품목 1건 이상과 전체 `receipt_completed=true`를 공통 readiness로 사용하는 키팅 queue
- 활성 패널 선택 단위의 부분·일괄 완료와 panel당 불변 completion
- client `operationId` 기반 동일 payload 성공 replay와 다른 payload conflict
- project·panel lock, 제조 담당자 필수, panel별 제조 업무·마지막 stage event·참조 알림의 단일 transaction
- 입고 확정·도착 마감 readiness hook, 패널 취소 시 열린 제조 업무 취소, 관리자 permanent purge 정합
- project permission·scope를 적용한 API와 desktop·390px 적응형 전용 페이지

제외:

- BOM·패널별 자재 allocation·재고 차감·완료 정정
- 제조 체크리스트·작업 시작/종료·중단과 실제 제조 입력 화면
- 외부 알림 provider, Persistent UAT migration·runtime handover
- 대표 repo·`main`, push·PR·merge

## 구현 결정과 영향

### DB·Backend

- additive `0033_panel_kitting_handoff.sql`에 operation 단위 batch와 panel 단위 completion을 분리했다. operation UUID와 panel UUID unique constraint, 정렬된 panel set fingerprint, readiness aggregate snapshot으로 replay·중복·감사 경계를 고정했다.
- `GET /api/materials/kitting`은 프로젝트 접근 범위 안의 readiness와 panel 상태를 반환한다. `POST /api/materials/kitting/complete`는 `MaterialReceipt.Update`와 같은 scope를 함께 확인한다.
- mutation은 project를 먼저 잠근 뒤 payload·패널정보·기존 완료·readiness·제조 담당자를 재검증한다. 동일 operation+동일 payload는 저장 결과를 replay하고, 다른 payload 또는 다른 operation의 완료 panel은 conflict로 돌린다.
- panel completion, `ManufacturingWork` 업무와 operation당 묶음 인앱 참조 알림을 한 transaction으로 처리한다. 마지막 panel에서는 `KittingCompleted` 성공 event와 키팅 업무 완료도 같은 경계에 포함한다. 제조 담당자를 찾지 못하면 전체 rollback한다.
- generic `WorkflowStore.CompleteStageAsync`는 호출하지 않아 프로젝트 단위 제조 업무와 panel별 제조 업무가 중복되지 않는다.
- `ConfirmAsync`와 `CloseArrivalsAsync` 두 readiness 전환 경로에서 키팅 업무를 idempotent하게 보장했다. panel 취소는 Requested/InProgress 제조 업무만 Cancelled로 바꾸고 completion은 보존한다. permanent purge는 신규 completion·batch를 선행 정리한다.

### Frontend·적응형 UX

- 대형 자재 workspace에 키팅 상태를 누적하지 않고 `PanelKittingPage.tsx`, `panelKitting.ts`, 전용 API helper로 분리했다.
- `/materials/kitting?project=...` route, 좌상단 숨김 global menu와 업무 deep link를 연결했다. 모바일 bottom navigation은 추가하지 않았다.
- desktop은 프로젝트 rail·readiness summary·panel grid를, 모바일은 가로 project queue·핵심 readiness·2열 compact panel card·선택 action을 사용한다. PC 화면을 축소한 표 구조를 사용하지 않는다.
- 각진 직사각형·둥근 직사각형·정사각형·soft shape, 원형 count/progress와 타원형 상태를 함께 사용했다. 390px에서 page horizontal overflow는 0이고 menu trigger는 45×45px다.
- 선택 내용이 바뀌기 전 실패 재시도에는 같은 operationId를 유지하고, 선택이 바뀌면 새 operationId를 생성한다.

## 해결한 업무 문제

- 입고 완료 뒤 제조 인수인계를 담당자가 수동으로 해석하던 공백을 panel별 실행 가능한 업무로 연결했다.
- network 응답 유실 시 중복 완료 또는 모호한 conflict 대신 같은 성공 결과를 복구할 수 있다.
- 프로젝트 마지막 panel 완료가 동시에 요청되어도 stage event와 묶음 알림을 중복 생성하지 않는다.
- 모바일 현장 담당자는 좌상단 메뉴에서 키팅 화면으로 들어가 한 화면 안에서 준비 상태·panel 선택·완료 action을 확인할 수 있다.

## 사용자 검수 결과와 남은 항목

- backend·frontend 자동 검증과 synthetic API 기반 desktop/390px 브라우저 시각 검수를 완료했다.
- 사용자 직접 검수는 아직 수행하지 않았으며 [010a-user-validation-checklist.md](010a-user-validation-checklist.md)는 `사용자 검수 대기`다.
- terminal의 Docker/Playwright 실행은 현재 세션 실행 정책이 승인을 요구했지만 approval policy가 `Never`여서 시작되지 않았다. E2E spec은 작성했으나 성공으로 기록하지 않는다.
- Persistent UAT·실제 사용자·실제 provider·GitHub는 승인 범위 밖이라 실행하지 않았다.

## 실제 변경 파일과 역할

- DB: `database/migrations/0033_panel_kitting_handoff.sql`
- Backend: `PanelKittingContracts.cs`, `PanelKittingEndpointExtensions.cs`, `PanelKittingStore.cs`, Materials readiness hook, Project purge/cancel lifecycle, Workflow link·stage 연결, DI
- Frontend: `PanelKittingPage.tsx`, `panelKitting.ts`, API·route/menu·adaptive CSS
- Tests: migration/API integration, frontend unit, mock visual·isolated full-stack Playwright spec
- 기획·검토: interview, Fable 1차 planning, Codex review, Change 001, Fable 2차 planning
- 증빙: `tasks/010a-screenshots/*.jpg`, 이 보고서와 user validation checklist

## 실행한 자동 테스트와 결과

- Backend Release build: `PASS`, warning 0 / error 0
- Backend 키팅·migration·purge targeted: `3/3 PASS`
- Backend 전체: `375/375 PASS`
- Frontend 키팅 unit: `PASS`; 실패 후 같은 operationId 재시도 포함
- Frontend 전체 unit: `77/77 PASS`
- Frontend lint: `PASS`(error 0, 기존 `main.tsx` Fast Refresh warning 1)
- Frontend typecheck + production build: `PASS`(기존 대형 chunk warning만 존재)
- Browser visual QA: desktop·390px 4장, 모바일 horizontal overflow 0, bottom navigation 0, 4종 card shape와 좌상단 menu active 상태 확인

미실행:

- mock·isolated Full-Stack Playwright runtime: terminal 실행 정책 제한으로 미실행
- Persistent UAT migration·runtime·실사용자 검증: 승인 범위 밖
- CI·GitHub PR·provider: 게시·외부 실행 미승인

## 개인정보·secret 검토

- screenshot과 UI 검증은 합성 프로젝트·panel·역할 사용자만 사용했다.
- Persistent UAT, 실제 고객·사용자·업무 원문은 읽거나 기록하지 않았다.
- tracked diff에는 credential, token, private key, tenant/client/object ID를 추가하지 않았다.

## Finding gate

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `010A-WORKFLOW-DUPLICATE` | P1 | `RESOLVED` | generic stage 완료 시 project 제조 업무 중복 | event 직접 1회 생성, panel work만 생성 |
| `010A-RETRY-AMBIGUITY` | P1 | `RESOLVED` | 성공 응답 유실 후 retry 결과 불명확 | operationId·payload fingerprint 성공 replay |
| `010A-ORPHAN-HANDOFF` | P1 | `RESOLVED` | 제조 담당자 없이 완료만 고정 | 담당자 없으면 transaction 전체 rollback |
| `010A-READ-SCOPE` | P1 | `RESOLVED` | scope 밖 project·panel 노출 | queue/mutation 모두 project scope 적용 |
| `010A-PURGE-LIFECYCLE` | P2 | `RESOLVED` | 신규 restrict FK가 기존 purge 방해 | purge transaction에서 completion·batch 선행 삭제 |
| `010A-READINESS-HOOK` | P2 | `RESOLVED` | 일부 입고 경로에서 키팅 업무 누락 | Confirm·CloseArrivals 공통 hook |
| `010A-CANCELLED-PANEL-WORK` | P2 | `RESOLVED` | 취소 panel의 열린 제조 업무 잔존 | Requested/InProgress 업무 Cancelled 처리 |
| `010A-STAGE-RACE` | P2 | `RESOLVED` | 동시 마지막 batch의 event 중복 | project row lock·기존 event 확인 |
| `010A-MODULE-BOUNDARY` | P3 | `RESOLVED` | 대형 store/workspace 확장 | 전용 backend/frontend module 분리 |
| `010A-CANCEL-LAST-PANEL-STAGE` | P3 | `DEFERRED — TASK-010A backlog` | 마지막 미완료 활성 panel이 취소로 사라지면 후속 batch가 없어 stage 완료 event가 자동 생성되지 않음 | 기획의 명시적 보류대로 panel 취소 경계의 stage 판정은 추가하지 않고 후속 정책 대상으로 추적 |

Open P0/P1/P2/P3: `0/0/0/1`. P3는 게시 차단 Finding이 아니며 TASK-010A backlog에 연결했다.

## Fable 사용량

Claude `/usage` 정수 반올림 기준이다.

| 시점 | 전체 사용/잔여 | Fable 사용/잔여 |
| --- | --- | --- |
| 1차 기획 직전 | 18% / 82% | 35% / 65% |
| 1차 기획 직후 | 18% / 82% | 35% / 65% |
| 2차 기획 직전 | 19% / 81% | 37% / 63% |
| 2차 기획 직후 | 20% / 80% | 40% / 60% |

1차 기획은 582초, 2차 기획은 368초가 걸렸다.

## 운영 SOP — 실험 검수용

1. 이 branch를 isolated DB와 external provider disabled 상태에서 실행한다.
2. 자재 도착·IQC를 처리해 프로젝트의 모든 활성 구매품목을 입고 완료한다.
3. 자재 담당은 좌상단 menu 또는 내 업무에서 `키팅`을 열고 준비 완료 프로젝트를 선택한다.
4. panel 정보와 상태를 확인한 뒤 제조로 넘길 활성 panel을 선택하고 키팅 완료한다.
5. 성공 요약의 완료 panel 수와 생성 제조 업무 수를 확인한다. 통신 오류 후 선택이 같으면 그대로 재시도한다.
6. 마지막 panel 완료 뒤 키팅 업무 종료와 stage 완료 여부를 확인한다. panel 취소가 필요하면 열린 제조 업무가 Cancelled인지 함께 확인한다.
7. Persistent DB 적용은 별도 backup·restore rehearsal과 runtime handover 승인을 거친다.

## User manual — 역할별 사용법

- 자재 담당 Desktop: `키팅` → 왼쪽 project → readiness 확인 → panel 선택 → `N면 키팅 완료` → 결과 확인.
- 자재 담당 Mobile: 좌상단 메뉴 → `키팅` → 위쪽 project card → panel card 선택 → 화면 안쪽 완료 action.
- 제조 담당: 키팅 완료된 panel마다 생성된 `제조 작업` 내 업무를 확인한다. 제조 입력은 후속 TASK-011A 범위다.
- 읽기 역할: 접근 가능한 프로젝트의 readiness·완료 상태만 조회하며 완료 action은 표시·허용되지 않는다.
- 오류 복구: 입고 미완료·panel 정보 미완료·담당자 미지정 문구에 따라 선행정보를 보완한다. 이미 다른 요청이 완료한 경우 최신 목록을 다시 불러온다.

## Rollback·forward-fix

- local code는 이 experiment commit의 후속 commit으로 보정할 수 있으며 main에는 반영되지 않는다.
- Persistent DB에 `0033`을 적용한 뒤 destructive down rollback은 하지 않는다. write를 중단하고 backup 기반 isolated 복구를 검증한 뒤 additive forward-fix migration을 작성한다.
- 정상 업무의 completion은 수정·삭제 API가 없다. 잘못된 완료 정정은 기존 row 변경이 아니라 별도 정책·신규 기능으로 계획한다.

## 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | 이 문서 `운영 SOP — 실험 검수용` | 실험 검수용 완료, 운영 handover 미승인 |
| User manual | 이 문서 `User manual — 역할별 사용법` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` TASK-010A section | 실험 구현·검수 대기 기록, canonical queue 불변 |
| User validation checklist | [010a-user-validation-checklist.md](010a-user-validation-checklist.md) | 자동 검증 완료·사용자 검수 대기 |

## 남은 항목

- 사용자 screenshot·실제 action 검수
- terminal 정책이 허용되는 환경의 mock·isolated Full-Stack Playwright runtime 검증
- 마지막 미완료 활성 panel이 취소로 사라지는 경우의 stage 완료 정책(TASK-010A P3 backlog)
- push·PR·merge, Persistent UAT와 실제 provider는 미승인·미실행
- canonical Roadmap 다음 Gate는 계속 `TASK-007A` Fable deep-interview
