# TASK-011A 제조 실행·중단·LQC handoff 구현 보고

## 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-011a-manufacturing-work`
- implementation / automaticValidation: `완료`
- userValidation: `대기`
- commit: `완료 — 이 보고서와 검증 산출물을 포함한 local experiment commit`
- push / PR / merge: `미승인·미실행`
- main merge approval: `0/3`
- Persistent UAT / provider / 대표 repo 영향: `없음`

## Task 목적·기획 source

키팅 완료된 panel을 제조 담당자가 모바일에서 시작하고, 고정 4단계를 순서대로 확인한 뒤 완료하며, 작업 불가 시 Panel target 긴급 Pending으로 중단·조치·재개하는 실행 흐름을 만들었다. 각 panel 제조 완료는 LQC skeleton 업무를 즉시 정확히 한 번 생성한다.

Authoritative implementation contract는 Fable 2차 기획 [docs/18-manufacturing-work-plan.md](../docs/18-manufacturing-work-plan.md)다. Fable 1차 원문은 [011a-planning.md](011a-planning.md), Codex 내용 review와 resolution은 [011a-review.md](011a-review.md), fast-track 승인·사용량은 [011a-change-001.md](011a-change-001.md)에 분리 보존했다.

## 포함·제외 범위

포함:

- 키팅 완료 panel queue·상세와 `/manufacturing/work` 전용 화면
- panel당 active execution 1건, 시작·4단계 순차 체크·중단·재개·완료 event와 actor/time audit
- execution·panel stage·panel 제조 업무의 transaction 동기화와 generic 내 업무 시작/완료 우회 차단
- bounded 중단 사유, 설명, 필수 조치 부서와 선택 담당자를 가진 Panel target `ManufacturingStop`·`Urgent` Pending
- Pending `Closed` 확인 뒤 같은 execution 재개
- panel별 LQC skeleton 업무 exactly-once와 마지막 active panel의 project `ManufacturingWork` stage event exactly-once
- operation receipt의 action·payload fingerprint·성공 projection replay, expected version stale 차단
- panel/project 취소의 active execution terminal 처리와 permanent purge 정합
- own operational time과 cross-user time permission 분리

제외:

- LQC/OQC/FAT 검사 record·성적서·사진·PDF·품질 화면
- 영구 제조 template 관리, 상세 자주순차표와 완료 정정·재작업
- 복수 panel batch 실행, 첨부·QR·Excel과 신규 외부 알림 채널
- Persistent UAT migration·runtime handover, 실제 provider, 대표 repo·`main`, push·PR·merge

## 구현 결정과 영향

### DB·Backend

- additive `0034_manufacturing_execution.sql`에 execution, 4단계 snapshot, append-only event, operation receipt를 추가하고 `pending_issues.action_department_code`를 호환 확장했다. partial unique로 panel당 active execution 한 건을 강제한다.
- `ManufacturingStore`는 project scope를 적용한 queue/detail과 start/check/stop/resume/complete를 제공한다. mutation은 row lock·expected version·operation fingerprint를 다시 확인하고 한 transaction 안에서 execution, panel stage와 정확한 panel work item을 함께 전이한다.
- 중단은 같은 connection/transaction에서 기존 Pending history·assignment artifact를 재사용한다. 조치 부서는 필수이고 assignee가 있으면 해당 부서의 active `Pending.Manage` 사용자인지 서버가 검증한다.
- 완료는 panel target LQC 업무를 `manufacturing:panel:{panelId}:lqc` key로 생성한다. 마지막 active panel에서만 project stage event를 한 번 생성한다.
- generic `/api/my-work/{id}/start|complete|cancel`은 panel `ManufacturingWork`에 conflict와 제조 화면 안내를 반환한다. 내 업무·키팅 생성 link도 제조 화면으로 연결했다.
- panel/project 취소는 active execution을 `Cancelled` terminal event와 함께 정리하고, permanent purge는 operation → event → step → execution 역순으로 정리한다.

### Frontend·적응형 UX

- `ManufacturingPage.tsx`, `manufacturing.ts`, 전용 API helper와 `/manufacturing/work?project=...&panel=...` view를 분리했다. global `제조` 메뉴는 조회 가능한 역할에 보이고 mutation action은 `manufacturing.update` 역할에만 렌더링한다.
- mobile은 project 가로 queue → compact panel strip → focus card → 2×2 단계 card → action 순서다. 시작 전 각진 사각, 진행 rounded+progress, 중단 타원, 완료 원형 check로 상태를 구분하고 좌상단 숨김 menu를 유지한다. bottom navigation은 추가하지 않았다.
- 중단 입력은 `MobileSheet`에서 사유·설명·부서·담당자를 받는다. 실패 재시도는 같은 payload일 때 같은 operation id를 유지하고 입력이 달라지면 새 id를 만든다.
- desktop은 project rail, 상태별 count, panel strip, 실행 단계와 timeline을 동시에 표시한다.
- Pending list/detail에는 panel 표시 코드와 조치 담당 부서를 추가했고, 일반 Pending target 호환을 유지했다.

## 해결한 업무 문제

- 키팅 뒤 생성되던 제조 내 업무가 실제 실행 기록과 연결되지 않던 공백을 panel 단위 실행으로 닫았다.
- generic 업무 완료로 checklist·stage·LQC handoff를 건너뛰는 우회를 서버에서 차단했다.
- 제조 중단을 일반 Project Pending이 아닌 정확한 panel·조치 부서·긴급도와 연결해 현장 복구 경로를 명확히 했다.
- 먼저 완료된 panel의 LQC를 프로젝트 마지막 panel까지 지연하지 않고 즉시 생성한다.

## 기술적 결정과 검토한 대안

- 영구 template table 대신 execution 생성 시 고정 4단계 snapshot을 저장했다. 상세 항목이 확정되지 않은 상태에서 template 관리 범위를 앞당기지 않기 위해서다.
- 기존 Project target `PendingStore.CreateAsync` 호출 대신 동일 transaction helper를 추가했다. 독립 transaction 호출은 execution Blocked와 Pending 생성이 갈라질 수 있어 제외했다.
- project 단위 LQC handoff 대신 panel별 LQC 업무 + 마지막 panel project event를 사용했다. 품질 인수 지연과 workflow 진행률 중복을 함께 피한다.
- event의 operation id unique만 쓰는 안 대신 별도 receipt에 payload hash와 성공 projection을 저장해 응답 유실 후 동일 성공을 replay하도록 했다.

## 시행착오 및 폐기한 접근

- 최초 execution lock 쿼리는 aggregate `GROUP BY`와 `FOR UPDATE`를 함께 사용했다. PostgreSQL 잠금 제약을 피하도록 step count를 lateral aggregate로 분리했다.
- Pending 응답 projection 확장 뒤 `ReadIssueForUpdateAsync`가 이전 column 순서를 유지해 상태 전이가 500을 반환하는 문제가 targeted integration에서 발견됐다. update projection도 같은 22개 field 계약으로 맞추고 전체 Pending 회귀를 통과했다.
- Full-Stack synthetic fixture는 미등록 Item, 과도한 구매 입력, mobile에서 숨겨진 desktop 개발 사용자 select를 차례로 사용해 실패했다. 등록 Item·최소 구매 계약을 사용하고 desktop에서 역할을 전환한 뒤 390px로 이동하도록 고쳤다.
- 중단 상태 증빙 한 장은 브라우저 캡처 세션의 확대 상태가 섞여 검수 증빙에서 제거했다. 최종 tracked 증빙은 정상 queue·진행·중단 입력·완료 4장이다.

## 실제 변경 파일과 역할

- DB: `database/migrations/0034_manufacturing_execution.sql`
- Backend: `Manufacturing/ManufacturingContracts.cs`, `ManufacturingEndpointExtensions.cs`, `ManufacturingStore.cs`, Pending target/department 확장, Workflow generic bypass, Kitting link, Project cancel/purge, DI
- Frontend: `ManufacturingPage.tsx`, `manufacturing.ts`, API·route·navigation·내 업무 action, Pending panel/department context, adaptive CSS
- Tests: migration test, 제조/Pending/LQC integration, frontend unit, disposable Full-Stack E2E spec
- 기획·검토: interview, Fable 1차 planning, Codex review, Change 001, Fable 2차 planning
- 증빙: [011a-screenshots](011a-screenshots), 이 보고서와 user validation checklist

## 실행한 자동 테스트와 결과

- Backend Release build: `PASS`, warning 0 / error 0
- `0034` fresh PostgreSQL migration targeted: `1/1 PASS`
- 제조 start/check/stop/Pending/resume/complete/LQC integration targeted: `1/1 PASS`
- Backend 전체: `376/376 PASS` (4분 38초)
- Frontend 전체 unit: `79/79 PASS`
- Frontend lint: `PASS` (error 0, 기존 `main.tsx` Fast Refresh warning 1)
- Frontend typecheck + production build: `PASS` (기존 large chunk warning만 존재)
- Disposable Full-Stack E2E: `1/1 PASS` (시작 → 4단계 → 중단 → Pending 종결 → 재개 → 완료 → LQC, 8.1초; DB/container 자동 삭제 확인)
- Browser visual QA: desktop·390px 4장, desktop/mobile horizontal overflow 0, mobile bottom navigation 0, 좌상단 menu trigger 존재

미실행:

- Persistent UAT migration·runtime·실사용자 검증: 승인 범위 밖
- CI·GitHub PR·실제 provider: 게시·외부 실행 미승인
- 사용자 직접 검수: checklist 작성 후 대기

## 개인정보·secret 검토

- screenshot, API와 E2E는 synthetic project·panel·역할 사용자만 사용했다.
- Persistent UAT, 실제 고객·사용자·업무 원문은 읽거나 기록하지 않았다.
- tracked diff에 credential, token, private key, tenant/client/object ID를 추가하지 않았다.
- 실제 provider delivery는 0건이며 Full-Stack worker/provider는 disabled였다.

## Finding gate

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `011A-DIRECT-WORK-BYPASS` | P1 | `RESOLVED` | generic 내 업무가 checklist와 LQC를 건너뜀 | panel ManufacturingWork generic mutation conflict |
| `011A-WORK-EXECUTION-DIVERGENCE` | P1 | `RESOLVED` | execution·panel·업무 status 분리 가능 | start/complete 단일 transaction 동기화 |
| `011A-PENDING-TARGET-DEPARTMENT` | P1 | `RESOLVED` | 중단 Pending이 Project target이고 부서가 없음 | Panel target, 필수 부서, 같은 부서 assignee 검증 |
| `011A-PANEL-LQC-HANDOFF` | P1 | `RESOLVED` | 먼저 완료된 panel의 LQC 지연 | panel LQC exactly-once, project event는 last panel |
| `011A-PENDING-UPDATE-PROJECTION` | P1 | `RESOLVED` | Pending update row reader의 이전 column 순서로 500 발생 | update projection 정렬, targeted·전체 회귀 통과 |
| `011A-REPLAY-CONTRACT` | P2 | `RESOLVED` | 성공 응답 유실·payload reuse 구분 불충분 | operation action·fingerprint·projection receipt |
| `011A-CANCELLATION-LIFECYCLE` | P2 | `RESOLVED` | panel 취소 뒤 active execution 고아 가능 | Cancelled terminal event와 purge 순서 보정 |
| `011A-WORKTIME-VISIBILITY` | P2 | `RESOLVED` | 제조 자기 시각과 cross-user time permission 충돌 | 자기 event time만 허용, 기존 all-time permission 유지 |
| `011A-CHECKLIST-REDUNDANCY` | P3 | `RESOLVED` | 시작 action과 `시작 확인` 단계 중복 | 작업지시/자재/수행/자체 확인 4단계 |

Open P0/P1/P2/P3: `0/0/0/0`.

## Fable 사용량

Claude `/usage` 정수 반올림 기준이다.

| 시점 | 전체 사용/잔여 | Fable 사용/잔여 |
| --- | --- | --- |
| 1차 기획 직전 | 21% / 79% | 41% / 59% |
| 1차 기획 직후 | 22% / 78% | 43% / 57% |
| 2차 기획 직전 | 22% / 78% | 43% / 57% |
| 2차 기획 직후 | 22% / 78% | 43% / 57% |

1차 기획 model 실행은 572초, 2차 기획 model 실행은 212초가 걸렸다. 2차 직전 첫 usage 조회는 exit 23으로 timeout됐고 즉시 read-only 재시도해 같은 기준값을 확인했다.

## 운영 SOP — 실험 검수용

1. 이 branch를 isolated DB와 external provider disabled 상태에서 실행하고 `0034` migration 적용 상태를 확인한다.
2. 자재 화면에서 대상 panel 키팅을 완료해 panel 제조 업무가 생성됐는지 확인한다.
3. 제조 담당은 좌상단 `제조` 또는 내 업무의 `제조 화면에서 진행`으로 들어가 project와 panel을 선택한다.
4. `제조 시작` 후 작업지시·도면 → 자재·부품 → 제조 작업 → 자체 확인 순으로 체크한다.
5. 작업 불가 시 `작업 중단`에서 사유·설명·조치 부서와 선택 담당자를 입력하고 긴급 Pending을 생성한다. Pending이 `Closed`가 된 뒤 재개한다.
6. 네 단계 완료 뒤 `제조 완료 · LQC 전달`을 실행하고 panel status, LQC 업무와 마지막 panel project stage를 확인한다.
7. stale/conflict는 최신 상세를 다시 불러오고 같은 입력의 통신 오류 retry는 그대로 재시도한다.
8. Persistent 적용은 별도 backup·restore rehearsal, migration·runtime handover 승인을 거친다.

## User manual — 역할별 사용법

- 제조 담당 Mobile: 좌상단 메뉴 → `제조` → project card → panel → 시작 → 4단계 확인 → 완료. 문제가 있으면 `작업 중단` sheet에서 Pending을 연결한다.
- 제조 담당 Desktop: `제조` → 왼쪽 project queue → panel strip → focus card action. 오른쪽 execution log에서 actor·시간·중단 이력을 확인한다.
- 생산관리/Pending 담당: 제조 상태를 조회하고 중단 Pending에서 담당 지정·조치·종결한다. 종결 전에는 제조 재개가 차단된다.
- 조회 역할: 접근 가능한 project의 제조 상태만 확인한다. mutation button은 표시되지 않는다.
- 내 업무: panel 제조 업무는 generic 완료하지 않고 `제조 화면에서 진행`으로 이동한다.

## Rollback·forward-fix

- local code는 이 experiment branch의 후속 commit으로 보정하거나 branch를 폐기할 수 있고 main에는 영향이 없다.
- Persistent DB에 `0034`를 적용한 뒤 destructive down rollback은 하지 않는다. write를 중단하고 backup 기반 isolated 복구를 검증한 뒤 additive forward-fix migration을 작성한다.
- execution/event/Pending/work item audit는 수정·삭제하지 않는다. 완료 정정·재작업은 별도 정책과 신규 기능으로 계획한다.

## 사용자 검수 결과와 남은 항목

- backend·frontend·disposable Full-Stack 자동 검증과 synthetic desktop/390px 브라우저 시각 검수를 완료했다.
- 사용자 직접 검수는 아직 수행하지 않았으며 [011a-user-validation-checklist.md](011a-user-validation-checklist.md)는 `사용자 검수 대기`다.
- Persistent UAT·실제 provider·GitHub는 승인 범위 밖이라 실행하지 않았다.

## 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | 이 문서 `운영 SOP — 실험 검수용` | 실험 검수용 완료, 운영 handover 미승인 |
| User manual | 이 문서 `User manual — 역할별 사용법` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` TASK-011A section | 실험 구현·검수 대기 기록, canonical queue 불변 |
| User validation checklist | [011a-user-validation-checklist.md](011a-user-validation-checklist.md) | 자동 검증 완료·사용자 검수 대기 |

## 남은 항목

- 사용자 screenshot·실제 action 검수
- push·PR·merge, Persistent UAT와 실제 provider는 미승인·미실행
- main merge 승인 `0/3`
- canonical Product Roadmap 다음 Gate는 계속 `TASK-007A` Fable deep-interview
