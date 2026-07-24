# 실험 브랜치 Task 완료 원장

## 1. 목적과 기준선

이 문서는 현재 `experiment/*` 계보에서 이미 구현한 Task와 남은 Task를 구분하는 선택 기준이다. 새 대화나 새 Task를 시작할 때 Product Roadmap과 함께 이 원장을 먼저 읽어, 완료된 기능을 다시 기획하거나 다시 구현하지 않는다.

- 감사 기준일: `2026-07-21`
- 감사 branch: `experiment/task-home-002-personalized-shell`
- 감사 HEAD: TASK-HOME-002 최종 local commit 계보(Git history와 구현 보고서가 source of truth)
- 대표 repository·GitHub `main`: 이 원장의 완료 판정 대상이 아니며 현재 실험 결과가 반영되지 않음
- Git 게시 경계: 실험 local commit만 완료. push·PR·merge 미승인, `main` merge 승인 `0/3`
- 운영 경계: Persistent UAT migration·runtime handover와 실제 provider 미적용

## 2. 상태 정의

| 상태 | 의미 | 다음 Task 선택 규칙 |
| --- | --- | --- |
| `EXPERIMENT_COMPLETE` | 승인된 실험 범위의 구현·필수 자동 검증·Finding gate·종료 산출물이 완료됨. 사용자 직접 검수는 `BATCHED_FINAL`로 마지막에 일괄 수행할 수 있음 | 같은 목적을 다시 Fable에 보내거나 재구현하지 않는다. 사용자가 명시적으로 수정을 요청한 경우에만 기존 Task의 다음 `change-###` 또는 bugfix로 재개한다 |
| `EXPERIMENT_SLICE_COMPLETE` | 명시된 phase 또는 slice는 완료됐지만 같은 Task family의 별도 후속 범위가 남음 | 완료 slice는 건드리지 않고 이름이 지정된 후속 범위만 시작한다 |
| `DEFERRED` | 기획·구현을 시작하지 않았거나 정책·외부 입력을 기다림 | blocker와 Roadmap 순서를 확인한 뒤 신규 Task gate를 수행한다 |
| `READY / FAST_TRACK` | 이름 있는 다음 제품 Task이며, 남은 비차단 정책은 Fable 2-pass 권장안으로 확정할 수 있음 | 사용자의 “다음 작업 시작” 지시로 별도 승인 없이 기획·review·2차 기획·구현·검증·screenshot·local commit까지 진행한다 |
| `PROMOTION_PENDING` | 실험 구현은 끝났지만 대표 repo·`main`·UAT에 반영하지 않음 | 기능을 재구현하지 않고 별도 승격·통합·UAT Task로 다룬다. `main` merge는 분리 승인 3회가 필요하다 |

`BATCHED_FINAL`은 사용자 검수를 완료했다고 가장하는 상태가 아니다. 실험 개발 완료 판정과 Task 선택에서만 완료로 취급하며, 사용자 검수 checklist에는 계속 `사용자 검수 대기 — 마지막 일괄 검수`를 기록한다. 사용자 검수에서 실패가 발견되면 완료 Task를 새 Task로 복제하지 않고 기존 Task의 change 또는 bugfix로 재개한다.

## 3. 완료된 실험 Task

아래 Task는 현재 실험 계보에서 다시 기획하거나 다시 구현하지 않는다.

| Task | 완료된 범위 | 자동 검증·증빙 source | 실험 상태 |
| --- | --- | --- | --- |
| `TASK-007A` | 공통 Pending 등록·배정·조치·재검사·종결·댓글·감사 이력 | [구현 보고서](../tasks/007a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-007B` | 패널·프로젝트 병목 단계, open Pending 결합 집계와 목록·상세 표시 | [구현 보고서](../tasks/007b-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-MOBILE-001` | 동일 URL 적응형 현장 UX와 좁은 화면 navigation 기준 | [구현 보고서](../tasks/mobile-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-HOME-001` | 내 업무·병목·Pending·알림 Home widget dashboard | [구현 보고서](../tasks/home-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-HOME-002` | actual 사용자 프로필 shell·본인 사진·9개 부서 Home 핵심 지표·desktop/mobile navigation 재구성·Change 002 전 부서 운영 메뉴 조회와 compact reference design | [본체](../tasks/home-002-implementation-report.md), [Change 002](../tasks/home-002-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-NOTICE-BOARD-001` | Home 상단·중앙은 보존하고 하단 병목 widget을 전사 공지 최신 5건으로 교체. 전체 목록·상세·작성·author-only soft delete·멱등 등록·plain text와 desktop/mobile 구성 | [구현 보고서](../tasks/notice-board-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-MOBILE-002` | 모바일 우선 정보 재배치, 전체 화면 개편, 좌상단 drawer, 모바일 shape·타이포 체계, Change 004 현장 판단 우선 단순화, Change 005 의미 기반 도형 통일 | [1차](../tasks/mobile-002-implementation-report.md), [Change 002](../tasks/mobile-002-change-002-implementation-report.md), [Change 003](../tasks/mobile-002-change-003-implementation-report.md), [Change 004](../tasks/mobile-002-change-004-implementation-report.md), [Change 005](../tasks/mobile-002-change-005-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `DESIGN-001` | 로그인 shell을 기준으로 전체 업무 화면 시각 통일 | [구현 보고서](../tasks/design-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-008A` | 자재 도착·분할 입고·IQC 요청·입고 확정 | [구현 보고서](../tasks/008a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-008B` | 사급 자재 제공·입고·잔량·마감 추적 | [구현 보고서](../tasks/008b-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-009A` | IQC 디지털 성적서, 사진, 판정 snapshot과 PDF | [구현 보고서](../tasks/009a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-010A` | 패널별 선택형 키팅 완료 알림, 생산계획·제조 투입 2탭 분리와 제조 정/부 내 업무 원자 인계 | [본체](../tasks/010a-implementation-report.md), [Change 003](../tasks/010a-change-003-implementation-report.md), [Change 004](../tasks/010a-change-004-implementation-report.md), [후속 전체 회귀](../tasks/e2e-full-suite-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-011A` | 제조 시작·4단계 실행·중단 Pending·재개·LQC 인계·Change 002 연속 click 직렬 저장 | [본체](../tasks/011a-implementation-report.md), [Change 002](../tasks/011a-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-012A` | IQC는 구매품목 도착분, LQC·OQC는 Checklist, 전진검수·FAT는 패널 통합 판정으로 처리하며 모든 부적합을 Pending과 연결하고 적합 재검사에서 원자 종결 | [구현 보고서](../tasks/012a-implementation-report.md), [Change 003](../tasks/012a-change-003.md), [Change 004](../tasks/012a-change-004-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-013A` | 포장 단위·출발·납품과 필수 증빙·정산 인계 | [구현 보고서](../tasks/013a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-014A` | 세금계산서 draft·정산·프로젝트 완료와 lifecycle fence | [구현 보고서](../tasks/014a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-EXPORT-001` | 업무 12개·관리자 8개 화면의 checkbox 전체선택·단일 선택 Excel 내보내기와 server allowlist 기반 필수 잠금 column picker | [Change 002](../tasks/export-001-implementation-report.md), [Change 003](../tasks/export-001-column-picker-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-EXPORT-002` | 여러 프로젝트 선택 Excel 내보내기 | [구현 보고서](../tasks/export-002-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-E2E-FULL-SUITE-001` | 현재 계약 기준 전체 회귀 복구와 실제 역할 18단계 lifecycle. Change 008에서 최신 업무 계약으로 갱신하고 Change 009에서 고정 검수 runtime의 macOS 직접 실행을 제공 | [구현 보고서](../tasks/e2e-full-suite-001-implementation-report.md), [Change 008](../tasks/e2e-full-suite-001-change-008-implementation-report.md), [Change 009](../tasks/e2e-full-suite-001-change-009-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-BILLING-REQUEST-001` | 출하 프로젝트 선택, 회계팀 세금계산서 발행요청 batch·재다운로드 Excel, 정산 발행요청 gate와 회계 발행 확인 | [구현 보고서](../tasks/billing-request-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-UX-001 A1` | 공통 action feedback, 내 업무와 알림의 처리 중·성공·부분 성공·실패 UX | [구현 보고서](../tasks/ux-001-implementation-report.md) | `EXPERIMENT_SLICE_COMPLETE / BATCHED_FINAL` |
| `TASK-UX-001 A2` | 생산계획·구매·자재·IQC·키팅·패널·선택 Excel의 구조화 action feedback과 오류 focus | [구현 보고서](../tasks/ux-001-a2-implementation-report.md) | `EXPERIMENT_SLICE_COMPLETE / BATCHED_FINAL` |
| `TASK-NOTIFY-005` | 사용자별 알림 preference, 필수 잠금, sparse opt-out, audit와 suppression gate | [구현 보고서](../tasks/notify-005-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-SALES-KPI-001` | 영업 Home·전용 화면의 12개월 확정 매출/목표/달성률 graph, 금액 KPI, 월별 근거와 관리자 목표 CAS | [본체](../tasks/sales-kpi-001-implementation-report.md), [Change 002](../tasks/sales-kpi-001-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-ADMIN-002` | 고정 6종 검사·제조 양식의 무코드 version 관리, 지정 부서장 scope, 제조 snapshot과 선택 Excel | [구현 보고서](../tasks/admin-002-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `DESIGN-000` | reference 기반 CSS semantic token·공통 React primitive와 Change 001 전역 black & white·무그림자·사각형 wireframe, semantic status color 예외, desktop/mobile visual baseline | [본체](../tasks/design-000-implementation-report.md), [Change 001](../tasks/design-000-change-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_PENDING` |
| `TASK-PENDING-TYPE-001` | system semantic 보호, 사용자 유형 lifecycle·정렬·label, system administrator 권한·CAS·audit, Pending 생성·필터·상세·선택 Excel 연동과 desktop/mobile 관리 화면 | [구현 보고서](../tasks/pending-type-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-QR-001` | 패널별 명시 QR 발급·SVG/PNG·선택 인쇄, 인증 모바일 scan landing·현재 담당 업무 routing, 관리자 사유 기반 rotation과 append-only audit | [구현 보고서](../tasks/qr-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-NOTIFY-AUDIT-001` | 관리자 알림 설정 변경 이력의 기간·행동·알림 종류·사용자/부서 조회, 요약, desktop/mobile UI와 선택 Excel | [구현 보고서](../tasks/notify-audit-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-NOTIFY-REPROCESS-001` | terminal Failed 알림의 generation 기반 수동 재처리, CAS·원자 배치·중복 위험 확인·append-only event와 관리자 UI | [구현 보고서](../tasks/notify-reprocess-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-WORKFLOW-CONTINUITY-001` | 실제 담당자 부서 인계를 하나의 연속 흐름으로 보정. Change 005~008에서 IQC·Pending·알림·입고 연속성, Change 009에서 패널 제조·LQC 병행과 패널별 품질·물류 인계, Change 010에서 18단계 실데이터·누락 인계 재조정, Change 011에서 LQC·OQC 응답·판정 원자 확정과 dialog 오류 복구를 고정했다 | [본체](../tasks/workflow-continuity-001-implementation-report.md), [Change 005](../tasks/workflow-continuity-001-change-005-implementation-report.md), [Change 006](../tasks/workflow-continuity-001-change-006-implementation-report.md), [Change 007](../tasks/workflow-continuity-001-change-007-implementation-report.md), [Change 008](../tasks/workflow-continuity-001-change-008-implementation-report.md), [Change 009](../tasks/workflow-continuity-001-change-009-implementation-report.md), [Change 010](../tasks/workflow-continuity-001-change-010-implementation-report.md), [Change 011](../tasks/workflow-continuity-001-change-011-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-UL891-SET-001` | UL891 세트 사양 version·주문 instance·개별 physical panel, 부분출하·발주 회수·달력월 발행요청. Change 002~004에서 패널 허브·부서 패널 현황·실제 진척 KPI, Change 005에서 프로젝트 상세 8개 탭의 공통 정렬을 완성 | [최종 기획](41-ul891-panel-set-plan.md), [Change 005](../tasks/ul891-set-001-change-005-implementation-report.md), [구현 보고서](../tasks/ul891-set-001-implementation-report.md), [검수 체크리스트](../tasks/ul891-set-001-user-validation-checklist.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_PENDING` |

현재 계보의 최신 누적 자동 기준선은 Backend 전체 `421/421`, Frontend 전체 `127/127`, migration `0054`까지의 fresh PostgreSQL 적용이다. TASK-WORKFLOW-CONTINUITY-001 Change 011은 LQC·OQC 체크리스트 응답과 판정을 한 transaction으로 확정해 거절 시 응답·version을 롤백하고 dialog 내부 상세 오류·중복 클릭 차단·409 복구를 고정했으며 기존 품질 연속성 포함 isolated Full-Stack `4/4`를 통과했다. Change 010은 18단계 실데이터 부분완료 집계, 선택형 키팅의 `키팅 완료 OR 제조 투입 요청` 조건과 누락 품질 인계 재조정을 고정했다. 고정 검수 DB에서 OQC 누락 1건을 복구했고 반복 실행 0건, 18단계 허용 상태 위반 0건을 확인했다. Change 009는 제조 시작 즉시 LQC 생성·미래 단계 잠금·제조와 LQC 공동 완료 OQC 자동 인계·OQC 후 전진검수와 필수 FAT 동시 인계·패널별 포장/출발/납품 증빙 E2E `4/4`를 isolated PostgreSQL에서 통과했다. TASK-E2E-FULL-SUITE-001 Change 008은 실제 역할 18단계 lifecycle `1/1`과 12면·6회 분할 입고·Pending 6건 stress lifecycle `1/1`을 현재 정책으로 통과했다. 실제 provider·Persistent UAT·대표 runtime으로 우회하지 않았다.

## 4. 남은 제품 개발 Task

아래는 완료 Task의 재구현이 아니라 별도 후속 범위다. `우선순위`는 현재 실험 계보의 권장 순서다. 이 branch의 standing instruction 아래에서 사용자가 “다음 작업 시작”이라고 하면 가장 높은 우선순위의 이름 있는 미완료 제품 Task를 선택하고, 비차단 정책은 Fable 권장안으로 자동 채택해 결과까지 진행한다.

| 우선순위 | Task·후속 범위 | 현재 상태 | 시작 조건·주의 |
| --- | --- | --- | --- |
| 1 | 첨부·사진 storage/검역/보존/backup·restore — Task ID 미정 | `DEFERRED / SECURITY_AND_OPERATIONS` | `TASK-007A`와 모바일 기능을 재구현하지 않고 binary 저장 능력만 별도 기획한다 |
| 2 | 운영 전환 — Task ID 미정 | `DEFERRED / UAT_RUNTIME` | hosting/domain, redirect URI, Teams catalog, 실제 provider, migration·rollback, 교육 승인이 필요하다 |

### 4.1 아직 독립 Task로 승격되지 않은 Roadmap 입력

다음은 Roadmap 추적 항목에는 남아 있지만 아직 실행 가능한 독립 Task ID·planning이 없는 결정 또는 현업 입력이다. “다음 작업”만으로 임의 구현하지 않고, 사용자가 해당 묶음을 요청하거나 위의 named Task가 시작될 때 scope를 확정한다.

| 묶음 | 남은 입력·능력 | 연결할 기존/후속 Task |
| --- | --- | --- |
| 검사·제조 양식 content | IQC/LQC/OQC 상세 checklist, 값 입력 방식, 필수 사진 위치, 검사성적서 PDF layout, 제조 화면·popup·저장-only 항목, LQC 요청 기준 | 관리 기능은 `TASK-ADMIN-002`로 완료. 실제 content는 `TASK-009A`, `TASK-011A`, `TASK-012A` 또는 `TASK-ADMIN-002`의 content change로 입력하며 관리 기능을 재구현하지 않음 |
| Pending·물류·정산 정책 | 부적합 반송/현장수리 세부 유형, 포장 규격·중량 등 실제 입력, 정산 세부 항목 | `TASK-007A`, `TASK-013A`, `TASK-014A`의 정책 change |
| 관리자 기준정보 | Item, 포장방식과 `size_required`, 생산계획 단계, 구매 필수 항목, role/permission 편집, 관리자 모바일 고도화 | ADMIN 후속 Task. 완료된 `TASK-ADMIN-001`을 재구현하지 않음 |
| 사용자 lifecycle | 퇴사·부서이동 시 미완료 업무 이관, dev user 업무의 실계정 수동 이관 | INFRA/ADMIN 후속 정책 Task |
| 알림 event·기한 | 자동 단계 handoff·긴급/PUNCH·재검사·프로젝트 완료의 Activity Feed coverage, `work_items.due_date` 동기화, due date 없는 기존 업무, 에스컬레이션 기한 설정 UI, Daily Digest HTML 개선 | NOTIFY 후속 신규 기능 또는 정책 Task |
| 알림 운영 | Gmail 공식 발송 수단 적합성, 운영 Teams Webhook 재발급, 운영 manifest/catalog URL 전환 | 운영 전환 Task |
| 달력 운영 | 공휴일 service key, 회사 휴일 연간 입력, 자체 근무일 override 필요성 | CALENDAR 후속 또는 운영 전환 Task |
| 보존·감사 | 삭제 보류·purge 모니터링 정책, 전체 field-level audit 확대 | 운영 고도화/Audit 후속 Task |
| 인증 복구 | break-glass 계정과 복구 절차 증명 | AUTH/UAT 운영 Task. 증명 전 Persistent live last-admin mutation 금지 |

이 표는 미확정 입력을 완료 기능의 결함으로 바꾸지 않는다. 실제 양식이나 정책을 받았을 때 기존 기능의 변경이면 해당 Task change로, 새 관리자 화면·외부 연동·상태 능력이면 별도 `NEW_FEATURE`로 분류한다.

## 5. 조건부 backlog

다음 항목은 확인된 P3 또는 최적화 후보이며 현재 완료 Task를 자동으로 다시 열지 않는다. 사용자가 요청하거나 실측 문제가 확인될 때만 기존 Task의 change·housekeeping·bugfix로 전환한다.

- `TASK-007A`: 프로젝트 tab과 전용 workspace navigation 중복 재평가
- `TASK-007B`: 정렬 전환 UI
- `TASK-HOME-001`: shell/Home 공통 query cache, Pending summary-only endpoint
- `TASK-MOBILE-002`: 대형 `App.tsx` route component 분할, 수동 desktop mode persistence
- `TASK-010A`: 선택형 키팅 알림에서 마지막 미완료 panel 취소 시 참고 stage 완료 표시 정책
- `DESIGN-001`: production chunk 분할, `main.tsx` Fast Refresh warning 정리
- Backend experiment formatting: 범위 밖 `LogisticsStore`·`PanelKittingStore`·`ProjectStore`와 대형 API test의 누적 `dotnet format` drift 정리. 기능 build/test는 통과하며 수정 시 별도 HOUSEKEEPING scope로 제한
- `TASK-013A`: 운영 증빙 저장 용량·보존 정책
- canonical 승격 전 요구 reviewer/Fable provenance 보강: 기능 재구현이 아니라 승격 검증 범위로 처리

## 6. Task별 사용자 검수 runtime과 승격 경계

2026-07-21 사용자 지시에 따라 이후 실험 Task는 완료할 때마다 같은 고정 runtime에서 사용자 검수를 handoff한다. 기존 `BATCHED_FINAL` 표기는 과거 완료 scope의 사용자 직접 검수가 아직 끝나지 않았다는 역사적 상태로 유지하되, 새 Task를 다시 선택하는 근거로 사용하지 않는다.

- Frontend: `http://127.0.0.1:42983`
- Backend: `http://127.0.0.1:41166`
- 실행 source: 현재 `experiment/task-home-002-personalized-shell` worktree의 최신 source
- 직접 실행: Repository root의 `사용자-검수-서버-실행.command`를 macOS Finder에서 더블클릭한다.
- 통합 launcher: `scripts/start-experiment-validation.sh`
- 개별 실행 script: `scripts/dev-experiment-validation-frontend.sh`, `scripts/dev-experiment-validation-backend.sh`
- 안전 경계: 실제 Teams·메일 provider, delivery/digest/escalation worker와 purge worker는 비활성화한다.
- 주소 정책: Task별로 새 사용자 검수 포트를 만들지 않는다. 고정 포트 점유 또는 restart 실패 시 다른 포트로 우회하지 않고 원인·ownership을 보고한다.
- 갱신 정책: Frontend는 Vite HMR, Backend는 `dotnet watch` full restart를 사용해 같은 주소에서 현재 source를 반영한다. Task 종료 때 health·root·대표 API를 다시 확인하고 두 server를 열어 둔다.
- E2E 분리: isolated Full-Stack E2E의 임시 port·DB는 사용자 검수 runtime으로 보고하지 않고 실행 후 정리한다.
- DB 표기: `emi_qms_experiment_validation_41164`의 suffix는 기존 데이터 보존을 위한 historical label이며 현재 Backend 주소와 무관하다.

검수 실패 시 처리 순서는 다음과 같다.

1. 실패한 화면과 기대 동작을 해당 기존 Task에 연결한다.
2. 신규 능력이 아니면 다음 `change-###` 또는 `BUGFIX`로 수정한다.
3. 신규 상태·권한·외부 연동이 필요하면 별도 `NEW_FEATURE`로 분류한다.
4. 다른 완료 Task를 함께 재기획하지 않는다.

대표 repo·`main`으로 옮기려면 별도 승격 Task에서 현재 experiment commit 계보, migration `0030`~`0049`, 전체 자동 회귀, Persistent UAT handover와 rollback을 다시 검증한다. 이는 완료 기능의 재개발이 아니다. `main` merge는 사용자가 merge를 요청하더라도 서로 분리된 승인 3회가 기록되기 전까지 수행하지 않는다.

## 7. 중복 실행 방지 체크리스트

새 Task를 시작하기 전에 다음을 순서대로 확인한다.

- [ ] 이 원장의 완료 표에서 같은 purpose가 있는지 확인한다.
- [ ] `EXPERIMENT_COMPLETE`면 Fable·새 planning·새 구현을 시작하지 않는다.
- [ ] `EXPERIMENT_SLICE_COMPLETE`면 완료 slice를 제외하고 명시된 후속 범위만 purpose identity로 사용한다.
- [ ] 사용자의 수정 요청이면 새 Task ID를 만들기 전에 기존 Task의 다음 change인지 확인한다.
- [ ] P3·외부 입력·운영 승격을 완료 기능 재구현 사유로 사용하지 않는다.
- [ ] 실험 완료, 사용자 검수, 대표 repo 반영, UAT 적용과 `main` merge 상태를 서로 분리해 보고한다.
