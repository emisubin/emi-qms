# TASK-012A LQC·OQC·고객검수·FAT 구현 보고

## 상태

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- branch: `experiment/task-012a-quality-inspections`
- implementation / automaticValidation: `완료`
- userValidation: `대기`
- commit: `완료 — 이 보고서와 검증 산출물을 포함한 local experiment commit`
- push / PR / merge: `미승인·미실행`
- main merge approval: `0/3`
- Persistent UAT / provider / 대표 repo 영향: `없음`

## Task 목적·기획 source

제조 완료 panel을 LQC부터 제조완료확인, OQC, 고객검수와 선택 FAT까지 단계별 담당자가 모바일에서 검사하고, 불합격·PUNCH를 정확한 조치 부서의 Pending 및 재검사와 연결하는 품질 실행 흐름을 만들었다. 확정된 성적서·사진 snapshot·PDF는 이후 수정되지 않는다.

Authoritative implementation contract는 Fable 2차 기획 [docs/19-quality-inspections-plan.md](../docs/19-quality-inspections-plan.md)다. Fable 1차 원문은 [012a-planning.md](012a-planning.md), Codex 내용 review와 resolution은 [012a-review.md](012a-review.md), fast-track 승인·사용량은 [012a-change-001.md](012a-change-001.md)에 분리 보존했다.

## 포함·제외 범위

포함:

- panel LQC·OQC·고객검수·선택 FAT의 queue, stage work, attempt, item response와 확정 report
- LQC 합격 뒤 TASK-011A 제조 execution을 수정하지 않는 별도 immutable 제조완료확인
- stage별 일반 v1 template seed와 attempt-local item snapshot
- JPEG/PNG optional 사진: 항목 연결, 파일당 5MB, attempt당 5개·총 15MB, 확정 뒤 불변
- 확정 PDF artifact, 다운로드와 실패 시 bounded 재시도
- LQC/OQC `Nonconformance`, 고객검수/FAT `Punch` Panel Pending과 재검사
- stage 책임자/current work assignee·permission·scope 권한, generic work/Pending 우회 차단
- operation receipt·expected version·payload fingerprint·bounded replay
- panel/project 취소의 open attempt/work terminal 정리와 approved permanent purge 순서
- `/quality/inspections` desktop·390px 적응형 workspace와 제조 화면 확인 card

제외:

- 실제 고객·프로젝트별 검사 양식, template 편집 UI, 필수 사진 위치 정책
- 전자서명, 고객 포털, 외부 공유·실제 notification provider
- 확정 성적서 수정·삭제·강제 합격·stage 후퇴
- TASK-013A 포장·출발·납품 상세
- Persistent UAT migration·runtime handover, 대표 repo·`main`, push·PR·merge

## 구현 결정과 영향

### DB·Backend

- additive `0035_panel_quality_inspections.sql`에 stage template version/item, attempt·item snapshot/response·photo·report·PDF artifact, 제조완료확인과 operation receipt를 추가했다. panel+stage active attempt, report/confirmation uniqueness와 finalized child 불변을 DB에서도 강제한다.
- `QualityInspectionStore`는 queue/detail, start/save/photo/finalize/reinspection/confirmation/PDF를 같은 domain 경계에서 처리한다. 모든 mutation은 scope·권한·stage 책임·정확한 current work·expected version·operation fingerprint를 transaction 안에서 다시 검증한다.
- 실패 확정은 필수 조치 부서와 선택된 같은 부서 담당자를 검증하고 Panel Pending·history·assignment work와 report를 원자 생성한다. linked Pending은 일반 `Closed`가 차단되며 합격 재검사만 다음 handoff와 함께 닫는다.
- LQC 합격은 제조완료확인 업무, 확인은 OQC, 이후 고객검수와 선택 FAT 또는 `PackingCompleted` skeleton으로 panel별 인계한다. 다음 담당자를 찾지 못하면 현재 확정까지 모두 rollback한다.
- panel 품질·제조확인 업무의 generic start/complete/cancel을 차단했다. 취소는 open attempt/work를 `Cancelled`로 맞추되 finalized report·confirmation·Pending history는 보존한다.
- 사진은 JPEG/PNG content sniffing과 bounded size/count를 적용하고 확정 snapshot에는 파일명·크기·hash 등 필요한 metadata만 포함한다. PDF는 panel 품질 전용 renderer로 생성하며 artifact 실패가 report 확정을 되돌리지는 않는다.

### Frontend·적응형 UX

- 전역 IQC 단일 label을 `품질`로 확장하고 `/quality/inspections?stage=...&project=...&panel=...` workspace를 추가했다. 기존 `/quality/iqc`는 호환 유지한다.
- mobile은 stage tab → compact project queue → panel 핵심 맥락 → one-column 항목 → 사진 → 판정 순서로 다시 구성했다. PC 내용을 단순 축소하지 않고 현장 action을 우선하며 좌상단 숨김 menu와 in-flow action을 사용한다.
- 상태 chip은 타원형, score·step은 원형, queue는 각진 카드, 실행 영역은 둥근 카드로 구분해 직사각형 한 종류에 의존하지 않는다. 390px에서 글자·도형·간격을 줄이되 touch action과 핵심 정보 밀도를 함께 유지했다.
- 사진 선택·preview·삭제, 항목 Pass/Fail/NA, 메모 저장, 확정 sheet, Pending·재검사·성적서 PDF 이력을 한 화면에 연결했다.
- 제조 화면에는 LQC 통과 panel만 보이는 제조완료확인 card와 OQC 전달 action을 추가했다.

## 해결한 업무 문제

- 제조 뒤 품질 단계를 coarse project 상태만으로 추정하던 공백을 panel별 검사 attempt와 확정 report로 닫았다.
- 품질 permission만 가진 사용자가 다른 stage를 확정하거나 generic 내 업무/Pending으로 report를 우회하는 경로를 서버에서 차단했다.
- 불합격을 단순 메모가 아니라 조치 부서·담당자·재검사 lifecycle이 있는 Panel Pending으로 연결했다.
- LQC 합격과 OQC 사이 제조 책임 확인을 별도 불변 record로 남겨 기존 제조 실행 이력을 변조하지 않는다.
- 실제 필수 사진 정책이 미확정인 동안에도 optional 증빙과 교체 가능한 versioned 일반 양식을 제공한다.

## 시행착오 및 폐기한 접근

- 초기 구현 계약 대조에서 사진/PDF route가 schema에만 있고 실행 경로가 빠진 것을 발견했다. 사진 add/delete/content와 PDF retry/download를 추가하고 실제 PNG upload·download를 Full-Stack으로 검증했다.
- 첫 Full-Stack 실행은 신규 endpoint가 없는 이전 Release binary를 `--no-build`로 기동해 404가 발생했다. Release build를 다시 만든 뒤 같은 isolated 흐름을 통과했다.
- 최초 screenshot 단계는 데이터 준비 전 loading 화면을 포착했다. queue readiness와 action enable 조건을 기다리도록 E2E를 보정해 실제 LQC·제조확인 상태만 최종 산출물로 남겼다.
- 최종 lifecycle audit에서 project 취소가 open 품질 attempt까지 terminal 처리하지 않는 공백을 발견했다. cancellation helper를 추가하고 OQC 진행 중 취소·LQC 확정 증빙 보존을 E2E에 포함했다.

## 실제 변경 파일과 역할

- DB: `database/migrations/0035_panel_quality_inspections.sql`
- Backend: `QualityInspections/*`, Pending transaction/종결 guard, Workflow generic bypass/deep link, Project cancellation/purge, Program DI
- Frontend: `QualityInspectionsPage.tsx`, `qualityInspections.ts`, API·route·품질 navigation, 제조완료확인 card, adaptive CSS
- Tests: migration test, frontend unit, disposable Full-Stack 품질 흐름 spec
- 기획·검토: interview, Fable 1차 planning, Codex review, Change 001, Fable 2차 planning
- 증빙: [012a-screenshots](012a-screenshots), 이 보고서와 user validation checklist

## 실행한 자동 테스트와 결과

- Backend Release build: `PASS`, warning 0 / error 0
- `0035` fresh PostgreSQL migration suite: `26/26 PASS`
- Backend 전체: `376/376 PASS`
- Frontend 전체 unit: `80/80 PASS`
- Frontend lint: `PASS` (error 0, 기존 `main.tsx` Fast Refresh warning 1)
- Frontend typecheck + production build: `PASS` (기존 large chunk warning만 존재)
- Disposable Full-Stack E2E: `1/1 PASS` (project 생성 → 키팅 → 제조 → LQC+PNG+PDF → 제조확인 → OQC → project 취소·보존, 9.8초; DB/container/network 자동 삭제 확인)
- Browser visual QA: 품질·제조확인 desktop/390px와 mobile menu, mobile horizontal overflow 0, bottom navigation 0

미실행:

- Persistent UAT migration·runtime·실사용자 검증: 승인 범위 밖
- CI·GitHub PR·실제 provider: 게시·외부 실행 미승인
- 사용자 직접 검수: checklist 작성 후 대기

## 개인정보·secret 검토

- screenshot, API와 E2E는 synthetic project·panel·역할 사용자만 사용했다.
- Persistent UAT, 실제 고객·사용자·검사·Pending 원문은 읽거나 기록하지 않았다.
- tracked diff에 credential, token, private key, tenant/client/object ID를 추가하지 않았다.
- 실제 provider delivery는 0건이며 Full-Stack worker/provider는 disabled였다.

## Finding gate

| ID | Severity | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `012A-STAGE-AUTHORIZATION` | P1 | `RESOLVED` | 다른 품질 stage 판정 우회 | 책임자/assignee + permission + scope 검증 |
| `012A-DIRECT-WORK-BYPASS` | P1 | `RESOLVED` | generic 업무가 report·confirmation 우회 | panel 품질/확인 generic mutation conflict |
| `012A-PENDING-ACTION-OWNER` | P1 | `RESOLVED` | 실패 조치 부서와 원자 artifact 누락 | 필수 조치 부서, 같은 transaction의 Panel Pending |
| `012A-REINSPECTION-CLOSE-BYPASS` | P1 | `RESOLVED` | 합격 없이 linked Pending 종결 | generic close 차단, 합격 재검사만 종결 |
| `012A-HANDOFF-ROLLBACK` | P1 | `RESOLVED` | 다음 담당자 부재 시 확정/업무 분리 | assignee 해석 실패 시 전체 rollback |
| `012A-PHOTO-EXECUTION-GAP` | P1 | `RESOLVED` | 계획된 증빙 schema에 실행 route 누락 | bounded upload/delete/content·불변·E2E 추가 |
| `012A-PROJECT-STAGE-SOURCE` | P2 | `RESOLVED` | coarse panel stage 집계 오류 | terminal attempt/confirmation + project lock |
| `012A-MANUFACTURING-CONFIRM-BOUNDARY` | P2 | `RESOLVED` | 기존 제조 이력 변조 위험 | 별도 immutable confirmation |
| `012A-TEMPLATE-BOUNDARY` | P2 | `RESOLVED` | IQC catalog lifecycle 결합 | panel quality 전용 stage catalog |
| `012A-REPLAY-PRIVACY` | P2 | `RESOLVED` | receipt 원문 복제 위험 | bounded fingerprint/result projection |
| `012A-CANCEL-LIFECYCLE` | P2 | `RESOLVED` | project 취소 뒤 open attempt 고아 가능 | attempt/work Cancelled, finalized 증빙 보존 E2E |
| `012A-PURGE-IMMUTABILITY` | P2 | `RESOLVED` | 불변 trigger와 approved purge 충돌 | purge bypass·FK 순서 migration test |
| `012A-FAT-PACKING-BOUNDARY` | P3 | `RESOLVED` | TASK-013A 세부 범위 침범 | 표준 Packing skeleton만 생성 |

Open P0/P1/P2/P3: `0/0/0/0`.

## Fable 사용량

Claude `/usage` 정수 반올림 기준이며 실패한 측정은 추정하지 않았다.

| 시점 | 전체 사용/잔여 | Fable 사용/잔여 |
| --- | --- | --- |
| 1차 기획 직전 | 측정 불가 — TUI timeout `exit 23` 3회 | 측정 불가 |
| 1차 기획 직후 | 측정 불가 — TUI timeout `exit 23` 2회 | 측정 불가 |
| 2차 기획 직전 | 24% / 76% | 48% / 52% |
| 2차 기획 직후 | 24% / 76% | 48% / 52% |

1차 기획 model 실행은 435초, 2차 기획 model 실행은 221초가 걸렸다. 2차 직전 첫 값은 Fable 47%였고 이어진 두 값은 48%였으므로 실행에 가장 가까운 마지막 값을 기록했다. 2차 직후 세 번은 모두 같은 비율이었다.

## 운영 SOP — 실험 검수용

1. 이 branch를 isolated DB와 external provider disabled 상태에서 실행하고 `0035` migration 적용 상태를 확인한다.
2. 자재 키팅과 제조를 완료해 panel LQC 업무를 생성하고 좌상단 `품질` → 검사 workspace로 이동한다.
3. stage·project·panel을 선택하고 검사를 시작해 필수 항목을 Pass/Fail/NA로 기록한다. NA는 사유를 입력하고 필요한 JPEG/PNG 증빙을 첨부한다.
4. 합격은 모든 필수 항목을 충족한 뒤 확정한다. 실패는 총평·조치 담당 부서와 선택 담당자를 입력해 `Nonconformance` 또는 `Punch` Pending을 함께 만든다.
5. 실패 조치 뒤 Pending을 `ReinspectionRequested`로 바꾸고 재검사를 연다. 합격 재검사가 Pending 종결과 다음 stage handoff를 함께 수행한다.
6. LQC 합격 뒤 제조 담당은 제조 화면의 별도 card에서 완료를 확인하고 OQC로 전달한다. OQC → 고객검수 → 선택 FAT/포장 skeleton을 같은 방식으로 진행한다.
7. 성적서 이력에서 PDF를 확인한다. stale/conflict는 최신 상세를 다시 불러오고 통신 오류의 동일 입력은 같은 operation으로 재시도한다.
8. Persistent 적용은 별도 backup·restore rehearsal, migration·runtime handover 승인을 거친다.

## User manual — 역할별 사용법

- 품질 담당 Mobile: 좌상단 메뉴 → `품질` → 검사 단계 → project/panel → 시작 → 항목 → 사진 → 저장/확정. 불합격은 조치 부서와 연결한다.
- 품질 담당 Desktop: stage tab과 왼쪽 queue로 대상을 고르고 가운데 항목·사진·판정, 오른쪽 Pending·성적서/PDF 이력을 확인한다.
- 제조 담당: LQC 합격 후 제조 화면의 `제조 완료 확인 · OQC 전달`을 실행한다. 기존 제조 execution은 다시 열리지 않는다.
- Pending 담당: 연결 품질 Pending을 조사하고 `ReinspectionRequested`까지 전이한다. 일반 종결은 차단되며 합격 재검사로 닫힌다.
- 조회 역할: 접근 가능한 project의 검사 결과와 성적서만 확인한다. 자기 stage가 아닌 mutation action은 표시되지 않는다.

## Rollback·forward-fix

- local code는 이 experiment branch의 후속 commit으로 보정하거나 branch를 폐기할 수 있고 main에는 영향이 없다.
- Persistent DB에 `0035`를 적용한 뒤 destructive down rollback은 하지 않는다. write를 중단하고 backup 기반 isolated 복구를 검증한 뒤 additive forward-fix migration을 작성한다.
- finalized report·response·photo·PDF snapshot과 confirmation을 수정·삭제하지 않는다. 정정·재작업은 별도 정책과 신규 기능으로 계획한다.

## 사용자 검수 결과와 남은 항목

- backend·frontend·disposable Full-Stack 자동 검증과 synthetic desktop/390px 브라우저 시각 검수를 완료했다.
- 사용자 직접 검수는 아직 수행하지 않았으며 [012a-user-validation-checklist.md](012a-user-validation-checklist.md)는 `사용자 검수 대기`다.
- Persistent UAT·실제 provider·GitHub는 승인 범위 밖이라 실행하지 않았다.

## 5종 종료 산출물

| 산출물 | 위치 | 상태 |
| --- | --- | --- |
| Implementation report | 이 문서 | 작성 완료 |
| SOP | 이 문서 `운영 SOP — 실험 검수용` | 실험 검수용 완료, 운영 handover 미승인 |
| User manual | 이 문서 `User manual — 역할별 사용법` | 작성 완료 |
| Roadmap update | `docs/00-product-roadmap.md` TASK-012A section | 실험 구현·검수 대기 기록, canonical queue 불변 |
| User validation checklist | [012a-user-validation-checklist.md](012a-user-validation-checklist.md) | 자동 검증 완료·사용자 검수 대기 |

## 남은 항목

- 사용자 screenshot·실제 action 검수
- push·PR·merge, Persistent UAT와 실제 provider는 미승인·미실행
- main merge 승인 `0/3`
- canonical Product Roadmap 다음 Gate는 계속 `TASK-007A` Fable deep-interview
