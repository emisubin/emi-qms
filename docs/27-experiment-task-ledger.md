# 실험 브랜치 Task 완료 원장

## 1. 목적과 기준선

이 문서는 현재 `experiment/*` 계보에서 이미 구현한 Task와 남은 Task를 구분하는 선택 기준이다. 새 대화나 새 Task를 시작할 때 Product Roadmap과 함께 이 원장을 먼저 읽어, 완료된 기능을 다시 기획하거나 다시 구현하지 않는다.

- 감사 기준일: `2026-07-18`
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
| `TASK-HOME-002` | actual 사용자 프로필 shell·본인 사진·9개 부서 Home 핵심 지표·desktop/mobile navigation 재구성 | [구현 보고서](../tasks/home-002-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-MOBILE-002` | 모바일 우선 정보 재배치, 전체 화면 개편, 좌상단 drawer, 모바일 shape·타이포 체계 | [1차](../tasks/mobile-002-implementation-report.md), [Change 002](../tasks/mobile-002-change-002-implementation-report.md), [Change 003](../tasks/mobile-002-change-003-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `DESIGN-001` | 로그인 shell을 기준으로 전체 업무 화면 시각 통일 | [구현 보고서](../tasks/design-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-008A` | 자재 도착·분할 입고·IQC 요청·입고 확정 | [구현 보고서](../tasks/008a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-008B` | 사급 자재 제공·입고·잔량·마감 추적 | [구현 보고서](../tasks/008b-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-009A` | IQC 디지털 성적서, 사진, 판정 snapshot과 PDF | [구현 보고서](../tasks/009a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-010A` | 패널별 키팅 완료와 제조 내 업무 원자 인계 | [구현 보고서](../tasks/010a-implementation-report.md), [후속 전체 회귀](../tasks/e2e-full-suite-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-011A` | 제조 시작·4단계 실행·중단 Pending·재개·LQC 인계 | [구현 보고서](../tasks/011a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-012A` | LQC·OQC·고객검수·선택 FAT, 사진·PDF·재검사 | [구현 보고서](../tasks/012a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-013A` | 포장 단위·출발·납품과 필수 증빙·정산 인계 | [구현 보고서](../tasks/013a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-014A` | 세금계산서 draft·정산·프로젝트 완료와 lifecycle fence | [구현 보고서](../tasks/014a-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-EXPORT-001` | 업무 12개·관리자 8개 화면의 checkbox 전체선택과 단일 선택 Excel 내보내기 | [구현 보고서 Change 002](../tasks/export-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-EXPORT-002` | 여러 프로젝트 선택 Excel 내보내기 | [구현 보고서](../tasks/export-002-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-E2E-FULL-SUITE-001` | 현재 계약 기준 전체 Full-Stack 회귀 복구, Backend `388/388`, Frontend `92/92`, Full-Stack `35/35` | [구현 보고서](../tasks/e2e-full-suite-001-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |
| `TASK-UX-001 A1` | 공통 action feedback, 내 업무와 알림의 처리 중·성공·부분 성공·실패 UX | [구현 보고서](../tasks/ux-001-implementation-report.md) | `EXPERIMENT_SLICE_COMPLETE / BATCHED_FINAL` |
| `TASK-NOTIFY-005` | 사용자별 알림 preference, 필수 잠금, sparse opt-out, audit와 suppression gate | [구현 보고서](../tasks/notify-005-implementation-report.md) | `EXPERIMENT_COMPLETE / BATCHED_FINAL` |

현재 계보의 최신 누적 자동 기준선은 `TASK-HOME-002`의 Backend `395/395`, Frontend `102/102`, migration `0042` fresh 적용과 Full-Stack `38/38`다. 개인화 Home·프로필은 추가로 사진 lifecycle·9부서 SQL 통합 검증과 desktop/mobile 6개 synthetic screenshot을 완료했다.

## 4. 남은 제품 개발 Task

아래는 완료 Task의 재구현이 아니라 별도 후속 범위다. `우선순위`는 현재 실험 계보의 권장 순서이며, 사용자 명시 요청이 있으면 Roadmap override를 기록하고 해당 항목을 진행한다.

| 우선순위 | Task·후속 범위 | 현재 상태 | 시작 조건·주의 |
| --- | --- | --- | --- |
| 1 | `TASK-UX-001 A2` — 생산계획·구매·자재·패널·Excel 화면 action feedback 확대 | `DEFERRED / READY_FOR_PLANNING` | A1을 재구현하지 않고 A2 화면만 Fable 2-pass로 기획한다 |
| 2 | `TASK-EXPORT-001` 후속 — 사용자 column picker | `DEFERRED / OPTIONAL` | 현재 20개 화면 선택 export는 완료 상태로 보존한다. 사용자가 컬럼 선택 기능을 원할 때만 다음 change로 시작한다 |
| 3 | `DESIGN-000` — Figma Variables·CSS token·공통 component foundation | `DEFERRED` | 기능 동작을 바꾸지 않는 housekeeping 범위로 제한한다 |
| 4 | `TASK-ADMIN-002` — 검사·제조 template 관리 | `DEFERRED / EXTERNAL_INPUT` | 실제 IQC/LQC/OQC/제조 양식과 운영 template이 안정된 뒤 시작한다 |
| 5 | Pending 유형 관리자 화면 — Task ID 미정 | `DEFERRED / POLICY_INPUT` | 유형·권한 정책을 확정한 뒤 새 canonical ID를 부여한다. `TASK-007A` 본체를 다시 만들지 않는다 |
| 6 | QR 스캔 landing — Task ID 미정 | `DEFERRED / POLICY_INPUT` | 공개 또는 인증 landing 정책과 QR 운영 경계를 먼저 확정한다 |
| 7 | `TASK-NOTIFY-005` 후속 — 관리자 preference 감사 조회 UI | `DEFERRED / NEW_FEATURE` | 감사 원장은 구현 완료. 조회·필터·요약 UI만 별도 기획한다 |
| 8 | 첨부·사진 storage/검역/보존/backup·restore — Task ID 미정 | `DEFERRED / SECURITY_AND_OPERATIONS` | `TASK-007A`와 모바일 기능을 재구현하지 않고 binary 저장 능력만 별도 기획한다 |
| 9 | terminal `Failed` delivery 수동 재처리 — Task ID 미정 | `DEFERRED / OPTIONAL_NEW_FEATURE` | retry generation·append-only audit·provider 중복 위험을 포함해 별도 기획한다 |
| 10 | 운영 전환 — Task ID 미정 | `DEFERRED / UAT_RUNTIME` | hosting/domain, redirect URI, Teams catalog, 실제 provider, migration·rollback, 교육 승인이 필요하다 |

### 4.1 아직 독립 Task로 승격되지 않은 Roadmap 입력

다음은 Roadmap 추적 항목에는 남아 있지만 아직 실행 가능한 독립 Task ID·planning이 없는 결정 또는 현업 입력이다. “다음 작업”만으로 임의 구현하지 않고, 사용자가 해당 묶음을 요청하거나 위의 named Task가 시작될 때 scope를 확정한다.

| 묶음 | 남은 입력·능력 | 연결할 기존/후속 Task |
| --- | --- | --- |
| 검사·제조 양식 | IQC/LQC/OQC 상세 checklist, 값 입력 방식, 필수 사진 위치, 검사성적서 PDF 양식, 제조 화면·popup·저장-only 항목, LQC 요청 기준 | `TASK-009A`, `TASK-011A`, `TASK-012A`의 양식 change 또는 `TASK-ADMIN-002` |
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
- `TASK-010A`: 마지막 미완료 panel 취소 시 stage 완료 정책
- `DESIGN-001`: production chunk 분할, `main.tsx` Fast Refresh warning 정리
- `TASK-013A`: 운영 증빙 저장 용량·보존 정책
- canonical 승격 전 요구 reviewer/Fable provenance 보강: 기능 재구현이 아니라 승격 검증 범위로 처리

## 6. 마지막 일괄 사용자 검수와 승격 경계

사용자 직접 화면·Excel·PDF 검수는 완료 Task 전체를 마지막에 한 번에 수행한다. 이 대기 상태 때문에 완료 Task를 다음 개발 Task로 다시 선택하지 않는다.

검수 실패 시 처리 순서는 다음과 같다.

1. 실패한 화면과 기대 동작을 해당 기존 Task에 연결한다.
2. 신규 능력이 아니면 다음 `change-###` 또는 `BUGFIX`로 수정한다.
3. 신규 상태·권한·외부 연동이 필요하면 별도 `NEW_FEATURE`로 분류한다.
4. 다른 완료 Task를 함께 재기획하지 않는다.

대표 repo·`main`으로 옮기려면 별도 승격 Task에서 현재 experiment commit 계보, migration `0030`~`0042`, 전체 자동 회귀, Persistent UAT handover와 rollback을 다시 검증한다. 이는 완료 기능의 재개발이 아니다. `main` merge는 사용자가 merge를 요청하더라도 서로 분리된 승인 3회가 기록되기 전까지 수행하지 않는다.

## 7. 중복 실행 방지 체크리스트

새 Task를 시작하기 전에 다음을 순서대로 확인한다.

- [ ] 이 원장의 완료 표에서 같은 purpose가 있는지 확인한다.
- [ ] `EXPERIMENT_COMPLETE`면 Fable·새 planning·새 구현을 시작하지 않는다.
- [ ] `EXPERIMENT_SLICE_COMPLETE`면 완료 slice를 제외하고 명시된 후속 범위만 purpose identity로 사용한다.
- [ ] 사용자의 수정 요청이면 새 Task ID를 만들기 전에 기존 Task의 다음 change인지 확인한다.
- [ ] P3·외부 입력·운영 승격을 완료 기능 재구현 사유로 사용하지 않는다.
- [ ] 실험 완료, 사용자 검수, 대표 repo 반영, UAT 적용과 `main` merge 상태를 서로 분리해 보고한다.
