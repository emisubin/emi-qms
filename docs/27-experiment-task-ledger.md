# 실험 브랜치 Task 완료 원장

## 1. 목적과 기준선

이 문서는 현재 `experiment/*` 계보에서 이미 구현한 Task와 남은 Task를 구분하는 선택 기준이다. 새 대화나 새 Task를 시작할 때 Product Roadmap과 함께 이 원장을 먼저 읽어, 완료된 기능을 다시 기획하거나 다시 구현하지 않는다.

- 감사 기준일: `2026-07-29`
- 감사 branch: `experiment/task-home-002-personalized-shell`
- 감사 HEAD: 누적 실험 checkpoint `b911947` 계보와 `TASK-EXPERIMENT-PROMOTION-001` 승격 검증 변경(Git history와 구현 보고서가 source of truth)
- 대표 repository·GitHub `main`: `TASK-EXPERIMENT-PROMOTION-001` Ready PR과 CI를 통한 승격 대상
- Git 게시 경계: 사용자의 서로 분리된 `main` merge 승인 `3/3`과 실험 사용자 검수 완료가 기록됐다. direct push 없이 Ready PR·CI 성공 뒤 merge한다.
- 운영 경계: 기존 공식 UAT DB는 보존 이름으로 격리하고 같은 공식 이름의 fresh DB에 migration `0001`~`0064`를 적용했다. 실제 provider는 계속 비활성화한다.

## 2. 상태 정의

| 상태 | 의미 | 다음 Task 선택 규칙 |
| --- | --- | --- |
| `EXPERIMENT_COMPLETE` | 승인된 실험 범위의 구현·필수 자동 검증·Finding gate·종료 산출물이 완료됨 | 같은 목적을 다시 Fable에 보내거나 재구현하지 않는다. 사용자가 명시적으로 수정을 요청한 경우에만 기존 Task의 다음 `change-###` 또는 bugfix로 재개한다 |
| `EXPERIMENT_SLICE_COMPLETE` | 명시된 phase 또는 slice는 완료됐지만 같은 Task family의 별도 후속 범위가 남음 | 완료 slice는 건드리지 않고 이름이 지정된 후속 범위만 시작한다 |
| `USER_VALIDATION_COMPLETE` | 사용자가 실험 계보의 마지막 일괄 검수를 완료했다고 명시함 | 검수 대기를 이유로 Task를 다시 열지 않는다. 이후 확인된 결함만 기존 Task의 change 또는 bugfix로 처리한다 |
| `IMPLEMENTED / COMMIT_PENDING` | 제품 구현·필수 자동 검증·증빙은 끝났지만 기존 미커밋 WIP와 변경 파일이 겹쳐 독립 local commit을 안전하게 만들 수 없음 | 기능을 다시 기획·구현하지 않는다. 기존 WIP를 보존한 채 누적 commit 승인 또는 clean 기반 재적용으로 Git packaging만 해결한다 |
| `DEFERRED` | 기획·구현을 시작하지 않았거나 정책·외부 입력을 기다림 | blocker와 Roadmap 순서를 확인한 뒤 신규 Task gate를 수행한다 |
| `READY / FAST_TRACK` | 이름 있는 다음 제품 Task이며, 남은 비차단 정책은 Fable 2-pass 권장안으로 확정할 수 있음 | 사용자의 “다음 작업 시작” 지시로 별도 승인 없이 기획·review·2차 기획·구현·검증·screenshot·local commit까지 진행한다 |
| `PROMOTION_PENDING` | 실험 구현은 끝났지만 대표 repo·`main`·UAT에 반영하지 않음 | 기능을 재구현하지 않고 별도 승격·통합·UAT Task로 다룬다. `main` merge는 분리 승인 3회가 필요하다 |

`BATCHED_FINAL`은 과거 사용자 검수 전 단계의 역사적 표기다. 2026-07-29 사용자가 이 원장의 실험 계보 전체 검수를 완료했다고 명시했으므로 현재 표의 완료 scope는 `USER_VALIDATION_COMPLETE`로 승격했다. 이후 실패가 발견되면 완료 Task를 새 Task로 복제하지 않고 기존 Task의 change 또는 bugfix로 재개한다.

## 3. 완료된 실험 Task

아래 Task는 현재 실험 계보에서 다시 기획하거나 다시 구현하지 않는다.

| Task | 완료된 범위 | 자동 검증·증빙 source | 실험 상태 |
| --- | --- | --- | --- |
| `TASK-007A` | 공통 Pending 등록·배정·조치·재검사·종결·댓글·감사 이력 | [구현 보고서](../tasks/007a-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-007B` | 패널·프로젝트 병목 단계, open Pending 결합 집계와 목록·상세 표시 | [구현 보고서](../tasks/007b-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-MOBILE-001` | 동일 URL 적응형 현장 UX와 좁은 화면 navigation 기준 | [구현 보고서](../tasks/mobile-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-HOME-001` | 내 업무·병목·Pending·알림 Home widget dashboard | [구현 보고서](../tasks/home-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-HOME-002` | actual 사용자 프로필 shell·본인 사진·9개 부서 Home 핵심 지표·desktop/mobile navigation 재구성·Change 002 전 부서 운영 메뉴 조회와 compact reference design | [본체](../tasks/home-002-implementation-report.md), [Change 002](../tasks/home-002-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-NOTICE-BOARD-001` | Home 상단·중앙은 보존하고 하단 병목 widget을 전사 공지 최신 5건으로 교체. 전체 목록·상세·작성·author-only soft delete·멱등 등록·plain text와 desktop/mobile 구성 | [구현 보고서](../tasks/notice-board-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-MOBILE-002` | 모바일 우선 정보 재배치, 전체 화면 개편, 좌상단 drawer, 모바일 shape·타이포 체계, Change 004 현장 판단 우선 단순화, Change 005 의미 기반 도형 통일 | [1차](../tasks/mobile-002-implementation-report.md), [Change 002](../tasks/mobile-002-change-002-implementation-report.md), [Change 003](../tasks/mobile-002-change-003-implementation-report.md), [Change 004](../tasks/mobile-002-change-004-implementation-report.md), [Change 005](../tasks/mobile-002-change-005-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `DESIGN-001` | 로그인 shell을 기준으로 전체 업무 화면 시각 통일 | [구현 보고서](../tasks/design-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-008A` | 자재 도착·분할 입고·IQC 요청·입고 확정 | [구현 보고서](../tasks/008a-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-008B` | 사급 자재 제공·입고·잔량·마감 추적 | [구현 보고서](../tasks/008b-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-009A` | IQC 디지털 성적서, 사진, 판정 snapshot과 PDF. Change 003에서 사진 필수 항목의 판정·근거 바로 아래에 항목 전용 사진 입력을 배치하고 Backend 확정 검증을 유지 | [본체](../tasks/009a-implementation-report.md), [Change 003](../tasks/009a-change-003.md), [통합 구현 보고](../tasks/workflow-continuity-001-change-017-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-010A` | 패널별 선택형 키팅 완료 알림, 생산계획·제조 투입 2탭 분리와 제조 정/부 내 업무 원자 인계. Change 005에서 8단계를 `생산관리 / 제조 요청`으로 바꾸고 최초 입고 확정의 제조 투입 판단·선택 키팅 인계와 패널별 제조 요청 진행률을 고정 | [본체](../tasks/010a-implementation-report.md), [Change 003](../tasks/010a-change-003-implementation-report.md), [Change 004](../tasks/010a-change-004-implementation-report.md), [Change 005](../tasks/010a-change-005.md), [통합 구현 보고](../tasks/workflow-continuity-001-change-017-implementation-report.md), [후속 전체 회귀](../tasks/e2e-full-suite-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-011A` | 제조 시작·4단계 실행·중단 Pending·재개·LQC 인계·Change 002 연속 click 직렬 저장 | [본체](../tasks/011a-implementation-report.md), [Change 002](../tasks/011a-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-MANUFACTURING-BATCH-001` | 기존 선택 Excel checkbox로 같은 프로젝트 제조 진행 패널을 선택하고 양식의 모든 제조 단계 중 원하는 단계 한 건만 원자적으로 일괄 확인하며 앞뒤 단계·제조 완료·LQC/OQC는 보존. Change 003에서 일반/조립 사용자 구분 제거 | [Change 003](../tasks/manufacturing-batch-001-change-003.md), [구현 보고서](../tasks/manufacturing-batch-001-implementation-report.md), [검수 체크리스트](../tasks/manufacturing-batch-001-user-validation-checklist.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-012A` | IQC는 구매품목 도착분, LQC·OQC는 Checklist, 전진검수·FAT는 패널 통합 판정으로 처리하며 모든 부적합을 Pending과 연결하고 적합 재검사에서 원자 종결. Change 005에서 사진 필수 LQC·OQC 항목의 판정·근거 바로 아래에 항목 전용 사진 입력과 서버 확정 검증을 추가 | [구현 보고서](../tasks/012a-implementation-report.md), [Change 003](../tasks/012a-change-003.md), [Change 004](../tasks/012a-change-004-implementation-report.md), [Change 005](../tasks/012a-change-005.md), [통합 구현 보고](../tasks/workflow-continuity-001-change-017-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-013A` | 포장 단위·출발·납품과 필수 증빙·정산 인계 | [구현 보고서](../tasks/013a-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-014A` | 세금계산서 draft·정산·프로젝트 완료와 lifecycle fence | [구현 보고서](../tasks/014a-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-EXPORT-001` | 업무 12개·관리자 8개 화면의 checkbox 전체선택·단일 선택 Excel 내보내기와 server allowlist 기반 필수 잠금 column picker | [Change 002](../tasks/export-001-implementation-report.md), [Change 003](../tasks/export-001-column-picker-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-EXPORT-002` | 여러 프로젝트 선택 Excel 내보내기 | [구현 보고서](../tasks/export-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-E2E-FULL-SUITE-001` | 현재 계약 기준 전체 회귀 복구와 실제 역할 18단계 lifecycle. Change 008에서 최신 업무 계약, Change 009에서 고정 검수 runtime 직접 실행, Change 010에서 현재 프로젝트 우선 목록·파생 품질 판정·물류 1회 확정·정산 저장 UI로 일반·stress 기준선을 갱신 | [구현 보고서](../tasks/e2e-full-suite-001-implementation-report.md), [Change 008](../tasks/e2e-full-suite-001-change-008-implementation-report.md), [Change 009](../tasks/e2e-full-suite-001-change-009-implementation-report.md), [Change 010](../tasks/e2e-full-suite-001-change-010-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-BILLING-REQUEST-001` | 출하 프로젝트 선택, 회계팀 세금계산서 발행요청 batch·재다운로드 Excel, 정산 발행요청 gate와 회계 발행 확인 | [구현 보고서](../tasks/billing-request-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-UX-001 A1` | 공통 action feedback, 내 업무와 알림의 처리 중·성공·부분 성공·실패 UX | [구현 보고서](../tasks/ux-001-implementation-report.md) | `EXPERIMENT_SLICE_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-UX-001 A2` | 생산계획·구매·자재·IQC·키팅·패널·선택 Excel의 구조화 action feedback과 오류 focus | [구현 보고서](../tasks/ux-001-a2-implementation-report.md) | `EXPERIMENT_SLICE_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-NOTIFY-005` | 사용자별 알림 preference, 필수 잠금, sparse opt-out, audit와 suppression gate | [구현 보고서](../tasks/notify-005-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-SALES-KPI-001` | 영업 Home·전용 화면의 12개월 확정 매출/목표/달성률 graph, 금액 KPI, 월별 근거와 관리자 목표 CAS | [본체](../tasks/sales-kpi-001-implementation-report.md), [Change 002](../tasks/sales-kpi-001-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-ADMIN-002` | 고정 6종 검사·제조 양식의 무코드 version 관리, 지정 부서장 scope, 제조 snapshot과 선택 Excel | [구현 보고서](../tasks/admin-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-PRODUCTION-CONTROL-001` | 기존 프로젝트 Legacy/snapshot 불변 보존, Item별 단일 현재 제조·생산계획 양식과 항목별 1:1 실적 연결, 새 프로젝트 transaction snapshot, 프로젝트별 계획 기간·연결 수정, 구매·자재·제조·품질·물류 원본 기반 자동 실적·근거·계획/실적 가로 막대 일정. Change 006에서 OQC 연결을 패널별 최종 `OQC 합격` 한 건으로 단순화하고 Change 007에서 계획표 헤더·날짜 축, Change 008에서 항목별 담당자·필요 인원·생산관리 코멘트와 8열 생산계획표를 보정. Change 009에서 공통 코멘트를 `생산관리 전체 전달사항`으로 명확히 하고 생산계획표 위 조회 위치를 고정 | [최종 기획](43-production-control-plan.md), [Change 006](../tasks/production-control-001-change-006.md), [Change 007](../tasks/production-control-001-change-007.md), [Change 008](../tasks/production-control-001-change-008.md), [Change 009](../tasks/production-control-001-change-009.md), [구현 보고서](../tasks/production-control-001-implementation-report.md), [통합 구현 보고](../tasks/workflow-continuity-001-change-017-implementation-report.md), [검수 체크리스트](../tasks/production-control-001-user-validation-checklist.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `DESIGN-000` | reference 기반 CSS semantic token·공통 React primitive, Change 001 black & white wireframe, Change 002 전 부서 입력 흐름 통일, Change 003 다중 업무의 `업무 선택→KPI·프로젝트→단일 프로젝트 입력` 분리·제조/품질 세로 패널 탐색·Pending KPI/프로젝트 목록과 상세 격리, Change 004 PC 평가 기반 UX 보정, Change 005 물류·정산 좁은 입력 header와 검은 표면 글자 대비 보정. Change 006은 독립 Graphite 실험의 공통 시각·구성, 장식용 왼쪽 rail 제거, table density, 클릭형 desktop/mobile 부서 disclosure와 업무 선택 전용 page 삭제를 최신 main 기반으로 선택 이식 | [본체](../tasks/design-000-implementation-report.md), [Change 001](../tasks/design-000-change-001-implementation-report.md), [Change 002](../tasks/design-000-change-002-implementation-report.md), [Change 003](../tasks/design-000-change-003-implementation-report.md), [Change 004](../tasks/design-000-change-004-implementation-report.md), [Change 005](../tasks/design-000-change-005-implementation-report.md), [Change 006 계약](../tasks/design-000-change-006.md), [Change 006 구현 보고](../tasks/design-000-change-006-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE`; Change 006 `USER_VALIDATION_COMPLETE / LOCAL_MAIN_MERGE_APPROVED / REMOTE_UNPUBLISHED` |
| `TASK-PENDING-TYPE-001` | system semantic 보호, 사용자 유형 lifecycle·정렬·label, system administrator 권한·CAS·audit, Pending 생성·필터·상세·선택 Excel 연동과 desktop/mobile 관리 화면 | [구현 보고서](../tasks/pending-type-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-QR-001` | 패널별 명시 QR 발급·SVG/PNG·선택 인쇄, 인증 모바일 scan landing·현재 담당 업무 routing, 관리자 사유 기반 rotation과 append-only audit | [구현 보고서](../tasks/qr-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-NOTIFY-AUDIT-001` | 관리자 알림 설정 변경 이력의 기간·행동·알림 종류·사용자/부서 조회, 요약, desktop/mobile UI와 선택 Excel | [구현 보고서](../tasks/notify-audit-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-NOTIFY-REPROCESS-001` | terminal Failed 알림의 generation 기반 수동 재처리, CAS·원자 배치·중복 위험 확인·append-only event와 관리자 UI | [구현 보고서](../tasks/notify-reprocess-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-WORKFLOW-CONTINUITY-001` | 실제 담당자 부서 인계를 하나의 연속 흐름으로 보정. Change 005~008에서 IQC·Pending·알림·입고 연속성, Change 009에서 패널 제조·LQC 병행과 패널별 품질·물류 인계, Change 010에서 18단계 실데이터·누락 인계 재조정, Change 011에서 LQC·OQC 응답·판정 원자 확정과 dialog 오류 복구, Change 012에서 전 부서 Pending 코멘트·부적합 항목 전용 재검사·LQC 누락 복구, Change 013에서 재검사 최종 전체 결과 합성·물류 증빙 선첨부 1회 확정·미완료 초안 자동 복구, Change 014에서 상세·전체 흐름 진행률 단일화와 공급유형별 구매 완료 정책 정합, Change 015에서 체크리스트 파생 판정·재검사 실패 조치 재요청·입고 업무 요약, Change 016에서 패널별 출발·납품 선택과 부분 출하 상태 정합, Change 017에서 업무 상태·정확 이동·Pending 참조 알림·생산관리 제조 요청 인계를 고정했다 | [본체](../tasks/workflow-continuity-001-implementation-report.md), [Change 005](../tasks/workflow-continuity-001-change-005-implementation-report.md), [Change 006](../tasks/workflow-continuity-001-change-006-implementation-report.md), [Change 007](../tasks/workflow-continuity-001-change-007-implementation-report.md), [Change 008](../tasks/workflow-continuity-001-change-008-implementation-report.md), [Change 009](../tasks/workflow-continuity-001-change-009-implementation-report.md), [Change 010](../tasks/workflow-continuity-001-change-010-implementation-report.md), [Change 011](../tasks/workflow-continuity-001-change-011-implementation-report.md), [Change 012](../tasks/workflow-continuity-001-change-012-implementation-report.md), [Change 013](../tasks/workflow-continuity-001-change-013-implementation-report.md), [Change 014](../tasks/workflow-continuity-001-change-014-implementation-report.md), [Change 015](../tasks/workflow-continuity-001-change-015-implementation-report.md), [Change 016](../tasks/workflow-continuity-001-change-016-implementation-report.md), [Change 017](../tasks/workflow-continuity-001-change-017-implementation-report.md) | `LOCAL_MAIN_MERGED / REMOTE_UNPUBLISHED` |
| `TASK-ATTACHMENT-001` | Pending 조치 중 담당자 전용 Draft 사진, 조치 완료 transaction의 회차별 확정·사유 snapshot, append-only 근거와 IQC·LQC·OQC 재검사의 최초 부적합→조치→판정 통합 조회 | [최종 기획](45-pending-action-attachment-plan.md), [구현 보고서](../tasks/attachment-001-implementation-report.md), [검수 체크리스트](../tasks/attachment-001-user-validation-checklist.md) | `LOCAL_MAIN_MERGED / REMOTE_UNPUBLISHED` |
| `TASK-QUALITY-OPERATING-MODEL-001` | 신규 프로젝트의 구매품 구분 snapshot 기반 외함 IQC routing, 비외함 `검사 대상 아님` 입고 확정, 서명 스캔본 기반 회차별 외함 적합·부적합·Pending 재검사 | [최종 기획](48-enclosure-iqc-routing-plan.md), [구현 보고서](../tasks/quality-operating-model-001-implementation-report.md), [검수 체크리스트](../tasks/quality-operating-model-001-user-validation-checklist.md) | `LOCAL_MAIN_MERGED / REMOTE_UNPUBLISHED` |
| `TASK-UL891-SET-001` | UL891 세트 사양 version·주문 instance·개별 physical panel, 부분출하·발주 회수·달력월 발행요청. Change 002~004에서 패널 허브·부서 패널 현황·실제 진척 KPI, Change 005에서 프로젝트 상세 8개 탭 공통 정렬, Change 006에서 종결 Pending 오표시·현재 단계 열, Change 007에서 설계 조회·별도 수정·저장 피드백, Change 008에서 현재값 최종 저장·불필요 규격 제거·설계 완료 기준을 보정했다 | [최종 기획](41-ul891-panel-set-plan.md), [Change 005](../tasks/ul891-set-001-change-005-implementation-report.md), [Change 006](../tasks/ul891-set-001-change-006-implementation-report.md), [Change 007](../tasks/ul891-set-001-change-007-implementation-report.md), [Change 008](../tasks/ul891-set-001-change-008-implementation-report.md), [구현 보고서](../tasks/ul891-set-001-implementation-report.md), [검수 체크리스트](../tasks/ul891-set-001-change-008-user-validation-checklist.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |

### 3.1 누적 local commit으로 완료된 최신 실험 Task

2026-07-29 승격 기준선은 Backend `430/430`, Frontend `142/142`, isolated Full-Stack E2E `55/55`, Mock UI E2E `4/4`, fresh migration `0001`~`0064`다. 아래 문단의 과거 테스트 수치는 각 변경 시점의 역사적 기준선이다.

| Task | 구현된 범위 | 자동 검증·증빙 source | 현재 상태 |
| --- | --- | --- | --- |
| `TASK-UL891-PRODUCTION-PLAN-001` | Ul891Set+LinkedV1의 실제 실물 세트별 생산계획 기간·담당자·필요 인원·코멘트 overlay, 전체/세트 범위의 생산계획표·일정표 동시 전환, 세트 패널 실적·프로젝트 공통 근거 분리, 생성·추가·취소 lifecycle과 완료 판정 정합 | [최종 기획](44-ul891-set-production-plan.md), [구현 보고서](../tasks/ul891-production-plan-001-implementation-report.md), [검수 체크리스트](../tasks/ul891-production-plan-001-user-validation-checklist.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` — 누적 checkpoint `e6f3fa6`, Backend `430/430`, Frontend `142/142`, migration `0064`, PC·390px·일반/stress lifecycle 증빙 완료 |

현재 worktree의 최신 누적 자동 기준선은 Backend 전체 `430/430`, Frontend 전체 `140/140`, migration `0064`까지의 fresh PostgreSQL 적용이다. DESIGN-000 Change 003은 생산관리·자재·품질·물류의 업무 선택을 프로젝트와 분리하고, 업무별 KPI·프로젝트 목록에서 선택한 한 프로젝트만 입력하도록 고정했으며 제조·패널 품질검사의 패널 목록을 왼쪽 세로 구조로 통일했다. 기존 API·권한·저장·상태 전이와 알림·내 업무 deep link는 유지했고 Frontend 전체 `136/136`, production build, PC 고정 검수 runtime의 3단계 동선과 console error 0을 통과했다. 모바일은 사용자 요청에 따라 UX/UI 평가 대상에서 제외했다. DESIGN-000 Change 002는 영업·생산관리·설계·구매·자재·제조·품질·물류·정산의 입력을 기존 기능·권한·API·상태 전이를 유지한 채 `대상 확인→값 입력→저장` 공통 흐름과 번호 section·한 번 선택·하단 action으로 통일했고, Frontend 집중 `85/85`·당시 전체 `134/134`, desktop·390px overflow 0·console error 0을 통과했다. `TASK-WORKFLOW-CONTINUITY-001` Change 016은 출발·납품 queue와 batch의 authoritative membership을 Packing Unit 전체에서 선택 패널로 전환하고, 기존 unit 기록은 panel membership으로 backfill해 호환한다. 같은 Packing Unit의 두 패널 중 하나만 출발·납품하면 다른 패널이 대기 상태로 남고, 마지막 패널 납품에서만 영업 정산 업무가 생성된다. Backend 집중 `3/3`·전체 `424/424`, Frontend 집중 `5/5`·당시 전체 `135/135`, isolated Full-Stack `1/1`, 390px overflow 0·고정 검수 runtime console error 0을 통과했다. `TASK-MANUFACTURING-BATCH-001` Change 002는 사용자 의미 정정에 따라 Claude/Fable 없이 기존 선택 Excel checkbox로 선택한 제조 진행 패널의 조립 의미 단계 한 건만 원자적으로 확인하고, 조립 전·후 다른 제조 단계와 제조 완료·LQC 인계는 기존 패널별 흐름으로 남긴다. 영향 Backend `1/1`, Frontend `4/4`, isolated Full-Stack `1/1`과 desktop·390px 증빙을 통과했다. 모든 범위는 exact allowlist·충돌·privacy 검토를 거쳤으며 실제 provider·Persistent UAT·대표 runtime으로 우회하지 않았다.

`TASK-MANUFACTURING-BATCH-001` Change 003은 Change 002의 조립 전용 오해를 사용자 검수 실패로 다시 열어, 제조 양식의 `일반/조립` 사용자 구분을 제거하고 모든 제조 단계 중 선택한 한 단계만 여러 패널에 원자 적용한다. 단계 순번·표시명을 함께 검증하고 앞뒤 단계·실행 완료·LQC/OQC는 보존한다. Backend 전체 `427/427`, Frontend 전체 `140/140`, isolated Full-Stack `1/1`, desktop·390px 증빙을 통과했으며 open P0/P1/P2는 `0/0/0`이다. 새 migration 없이 과거 role·receipt schema를 호환 보존했고 Change 003은 누적 checkpoint `e6f3fa6`에 포함됐으며 최종 사용자 검수만 대기한다.

TASK-PRODUCTION-CONTROL-001은 migration `0058`의 Item별 제조·생산계획·연결과 프로젝트 snapshot, migration `0059`의 LQC 제조 단계 불변 identity를 추가했다. 유효한 두 현재 양식이 저장된 뒤 생성되는 프로젝트만 `LinkedV1`이 되고 기존 프로젝트와 양식 없는 Item은 생성 당시 Legacy/snapshot UI·데이터를 유지한다. 연결형 프로젝트는 생산관리 탭의 생산계획표, 항목별 자동 근거와 계획/실적 가로 막대 일정으로 구매·자재·제조·품질·물류 원본 사실을 조회 시 집계하며 FAT 비필수 프로젝트는 FAT를 분모에서 제외한다. Change 002는 연결형 양식 2개를 기존 `양식 종류`에 통합하고 제조 표 정렬과 생산계획 항목 단일 펼침 편집으로 정보구조 결함을 해결했다. Change 003~004는 version 누적을 제거한 단일 현재 양식, 항목별 1:1 실적 드롭다운과 IQC·OQC 단계 연결로 정리했다. Change 005는 제조 항목 교체 시 저장·연결의 순서 교착과 일반 오류 문구를 해결하고, 기존 프로젝트 snapshot 불변·끊긴 현재 연결 안내·재연결 전 신규 생성 차단을 검증했다. Change 006은 OQC 검사 내부 단계는 그대로 두되 생산계획 실적 연결을 세부 항목이 아닌 패널별 최종 `OQC 합격` 한 건으로 바꿨다. migration `0062`는 현재 양식만 aggregate로 정리하고 기존 프로젝트의 세부 OQC snapshot은 보존한다. Change 007은 6열 헤더를 밝은 중립색으로 보정하고 가로 막대 위에 최대 6개 날짜 축과 동일 위치 기준선을 추가했다. Change 008은 migration `0063`으로 항목별 선택 담당자와 필요 인원을 nullable metadata로 추가하고 기존 비고 용어를 생산관리 코멘트로 통일했다. 조회는 내부 실적 연결 열을 숨긴 8열 생산계획표로 바꾸되 원본 자동 실적·업무·알림 계약은 유지했다. Backend·Frontend·migration 회귀와 고정 검수 runtime을 기준으로 사용자 최종 일괄 검수만 남긴다.

DESIGN-000 Change 004는 Change 003의 PC 사용자 평가를 구현 기준으로 삼아 프로젝트·구매·자재의 첫 프로젝트 행을 1280×720 안으로 올리고, 프로젝트 상세 핵심정보·병목을 압축해 부서 탭을 첫 화면과 sticky 위치에 고정했다. 왼쪽 메뉴는 `내 업무 / 부서 업무 / 공통 조회 / 관리`로 구분하고, 공통 breadcrumb·조회 전용/선행조건 banner·empty state·secondary tools·selection mode를 도입했다. 제조·품질의 현재 패널 선택과 batch/export checkbox를 분리하고, 생산계획 긴 입력은 접기형 단계, 양식 관리는 text preview와 draft input으로 분리했다. Frontend `136/136`, typecheck, lint error 0, production build와 1280×720·390×844 browser overflow 0을 통과했으며 Backend·API·DB·workflow는 변경하지 않았다.

DESIGN-000 Change 006은 독립 Graphite 실험의 Frontend source·test만 최신 main 기반 승격 branch에 이식하고 실험 문서·screenshot·Git history를 제외했다. 장식용 왼쪽 강조 rail을 제거하고 표 밀도를 통일했으며, 왼쪽 메뉴는 desktop·390px 모두 처음 접힌 부서 행을 눌러 한 부서만 펼치고 child로 실제 업무에 직접 이동한다. `DepartmentWorkHub.tsx`와 업무 선택 전용 page는 삭제했고 legacy 부서 root만 첫 실제 업무 replace redirect로 보존했다. Frontend lint error/warning 0, typecheck, unit `170/170`, production build와 격리 mock E2E `4/4`를 통과했다. 사용자는 2026-08-01 검수를 완료하고 local `main` merge를 승인했으며 Backend·DB·migration·배포·원격 게시에는 변경이 없다.

TASK-UL891-SET-001 Change 007은 신규 UL891 프로젝트 상세 설계 탭을 세트 공통 사양·실물 세트·개별 패널·QR 조회 전용으로 만들고 중복 평면 패널 설계 영역을 제거했다. 권한이 있는 설계 담당자는 단일 `수정` 버튼으로 별도 입력 화면에 진입하며 `임시저장`·`저장`의 진행·결과를 해당 action 바로 아래에서 확인한다. 비-UL891·legacy 설계, 내부 Draft/Published 계약, Backend·DB·workflow는 유지했고 Frontend `138/138`, typecheck, lint error 0, production build와 desktop·390px overflow 0·browser warning/error 0을 통과했다.

사용자 검수에서 Change 007의 최종 `저장`이 현재 form을 먼저 갱신하지 않는 결함과 `규격`을 Publish·설계 완료에 필수로 요구하는 잘못된 조건이 확인됐다. Change 008은 `UpdateDraft → Publish`를 단일 저장 action으로 직렬화하고 완료 조건을 패널명·포장방식별 치수로 통일했으며 UL891 화면에서 규격을 제거했다. 기존 API·DB 호환 필드는 유지하고 격리 API 회귀·Frontend `138/138`·desktop 실제 화면을 통과했으며 사용자 재검수만 남는다.

DESIGN-000 Change 003의 최신 보완은 Pending의 불필요한 단일 업무 선택을 제거하고 KPI·프로젝트 목록으로 바로 시작하게 했다. 프로젝트 상세는 해당 프로젝트의 이슈만 compact하게 표시하고 프로젝트 ID 기준으로 state를 재초기화해 다른 프로젝트의 비동기 응답 혼입을 차단했다. xAI 6건과 별도 ps26-004 1건의 실제 검수 데이터로 격리, 뒤로가기와 browser console warning/error 0을 확인했다.

Change 014 이후 누적 자동 기준선은 Backend 전체 `424/424`, Frontend 전체 `130/130`이다. 프로젝트 상세 진행률은 전체 흐름 값을 단일 원본으로 사용하고, 구매 완료는 공급유형별 필수 입력과 required template match를 ProjectStore·WorkflowStore에 동일 적용하며 기존 완료 이벤트의 비회귀를 유지한다.

## 4. 남은 제품 개발 Task

아래는 완료 Task의 재구현이 아니라 별도 후속 범위다. `우선순위`는 현재 실험 계보의 권장 순서다. 이 branch의 standing instruction 아래에서 사용자가 “다음 작업 시작”이라고 하면 가장 높은 우선순위의 이름 있는 미완료 제품 Task를 선택하고, 비차단 정책은 Fable 권장안으로 자동 채택해 결과까지 진행한다.

| 우선순위 | Task·후속 범위 | 현재 상태 | 시작 조건·주의 |
| --- | --- | --- | --- |
| 0 | 운영 첨부 storage 용량·scanner 활성화·backup/restore rehearsal — `TASK-ATTACHMENT-001` 후속 운영 범위 | `DEFERRED / SECURITY_AND_OPERATIONS` | Pending 조치 사진의 bounded DB 저장은 완료했다. Persistent UAT·실제 scanner·복구 훈련은 운영 전환 승인 뒤 별도 수행한다 |
| 1 | 운영 전환 — Task ID 미정 | `DEFERRED / UAT_RUNTIME` | hosting/domain, redirect URI, Teams catalog, 실제 provider, migration·rollback, 교육 승인이 필요하다 |

### 4.1 아직 독립 Task로 승격되지 않은 Roadmap 입력

다음은 Roadmap 추적 항목에는 남아 있지만 아직 실행 가능한 독립 Task ID·planning이 없는 결정 또는 현업 입력이다. “다음 작업”만으로 임의 구현하지 않고, 사용자가 해당 묶음을 요청하거나 위의 named Task가 시작될 때 scope를 확정한다.

`TASK-QUALITY-OPERATING-MODEL-001`의 외함 IQC slice는 2026-07-30 사용자 정책 확정과 [최종 2차 기획](48-enclosure-iqc-routing-plan.md)에 따라 구현·자동 검증을 완료했다. 신규 프로젝트는 구매품 구분 snapshot으로 외함만 스캔형 IQC를 수행하고 비외함은 `검사 대상 아님` 뒤 자재 입고 확정으로 간다. 기존 프로젝트는 모든 구매품 상세 IQC를 그대로 유지한다. LQC·OQC·전진검수·FAT·도면 revision의 후속 운영 모델은 이 완료 slice에 포함하지 않으며 현업 추가 확정 전 별도 Task로 남긴다.

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

2026-07-29 사용자는 같은 고정 runtime에서 진행한 실험 계보의 마지막 일괄 검수를 모두 완료했다고 확정했다. `BATCHED_FINAL`은 과거 검수 전 단계의 역사적 기록일 뿐 현재 완료 scope의 상태가 아니며, 검수 대기나 승격을 이유로 기존 기능을 다시 선택하지 않는다.

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

대표 repo·`main` 승격은 `TASK-EXPERIMENT-PROMOTION-001`에서 현재 experiment commit 계보, migration `0001`~`0064`, 전체 자동 회귀, fresh 공식 UAT handover와 rollback을 검증한다. 사용자의 사용자 검수 완료와 서로 분리된 merge 승인 `3/3`이 기록됐으며, direct push 없이 Ready PR의 CI가 성공한 경우에만 merge한다.

## 7. 중복 실행 방지 체크리스트

새 Task를 시작하기 전에 다음을 순서대로 확인한다.

- [ ] 이 원장의 완료 표에서 같은 purpose가 있는지 확인한다.
- [ ] `EXPERIMENT_COMPLETE`면 Fable·새 planning·새 구현을 시작하지 않는다.
- [ ] `EXPERIMENT_SLICE_COMPLETE`면 완료 slice를 제외하고 명시된 후속 범위만 purpose identity로 사용한다.
- [ ] 사용자의 수정 요청이면 새 Task ID를 만들기 전에 기존 Task의 다음 change인지 확인한다.
- [ ] P3·외부 입력·운영 승격을 완료 기능 재구현 사유로 사용하지 않는다.
- [ ] 실험 완료, 사용자 검수, 대표 repo 반영, UAT 적용과 `main` merge 상태를 서로 분리해 보고한다.
