# 실험 완료 기록과 후속 범위

이 문서는 실험 계보의 결과 위치를 찾는 인덱스다. 실행 순서는 [Roadmap](00-product-roadmap.md), 새 작업의 절차는 [Root AGENTS](../AGENTS.md)를 따른다. 과거 Fable/Sol 역할·승인 횟수·항상 열어 두는 runtime 절차는 실행하지 않는다.

## 상태를 읽는 방법

- `EXPERIMENT_COMPLETE`: 명시한 실험 scope의 개발·검증 완료. 동일 목적을 다시 기획/구현하지 않는다.
- `EXPERIMENT_SLICE_COMPLETE`: 이름 있는 slice만 완료. 해당 Task에 남은 후속 slice만 다룬다.
- `BATCHED_FINAL`: 마지막 일괄 사용자 검수 대기. 개발 완료와 사용자 검수를 구분한다.
- `USER_VALIDATION_COMPLETE`: 기록된 scope·환경의 사용자 검수 완료. 이후 change나 운영 검수로 확대하지 않는다.
- `PROMOTION_PENDING` 또는 commit/remote 미반영: 구현 재개발 사유가 아니다. 필요한 Git packaging·통합·UAT만 다룬다.

## 보존한 결과 위치

아래는 교체 전 원장의 Task·증거 링크·당시 기록 상태를 보존한 인덱스다. 2026-07-29 일괄 검수와 각 후속 change가 기준이며 **현재 main·배포·실행 상태 표가 아니다**. 최신 상태는 해당 Task의 최신 change와 실제 Git/source를 확인한다. scope 전문과 중간 결정은 [원문](archive/harness-v1-2026-09-09/27-experiment-task-ledger.md.snapshot)에 있다.

| Task | 증거·scope 위치 | 당시 기록 상태 |
| --- | --- | --- |
| `TASK-007A` | [구현 보고서](../tasks/007a-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-007B` | [구현 보고서](../tasks/007b-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-MOBILE-001` | [구현 보고서](../tasks/mobile-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-HOME-001` | [구현 보고서](../tasks/home-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-HOME-002` | [본체](../tasks/home-002-implementation-report.md), [Change 002](../tasks/home-002-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-NOTICE-BOARD-001` | [구현 보고서](../tasks/notice-board-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-MOBILE-002` | [1차](../tasks/mobile-002-implementation-report.md), [Change 002](../tasks/mobile-002-change-002-implementation-report.md), [Change 003](../tasks/mobile-002-change-003-implementation-report.md), [Change 004](../tasks/mobile-002-change-004-implementation-report.md), [Change 005](../tasks/mobile-002-change-005-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `DESIGN-001` | [구현 보고서](../tasks/design-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-008A` | [구현 보고서](../tasks/008a-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-008B` | [구현 보고서](../tasks/008b-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-009A` | [본체](../tasks/009a-implementation-report.md), [Change 003](../tasks/009a-change-003.md), [통합 구현 보고](../tasks/workflow-continuity-001-change-017-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-010A` | [본체](../tasks/010a-implementation-report.md), [Change 003](../tasks/010a-change-003-implementation-report.md), [Change 004](../tasks/010a-change-004-implementation-report.md), [Change 005](../tasks/010a-change-005.md), [통합 구현 보고](../tasks/workflow-continuity-001-change-017-implementation-report.md), [후속 전체 회귀](../tasks/e2e-full-suite-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-011A` | [본체](../tasks/011a-implementation-report.md), [Change 002](../tasks/011a-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-MANUFACTURING-BATCH-001` | [Change 003](../tasks/manufacturing-batch-001-change-003.md), [구현 보고서](../tasks/manufacturing-batch-001-implementation-report.md), [검수 체크리스트](../tasks/manufacturing-batch-001-user-validation-checklist.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-012A` | [구현 보고서](../tasks/012a-implementation-report.md), [Change 003](../tasks/012a-change-003.md), [Change 004](../tasks/012a-change-004-implementation-report.md), [Change 005](../tasks/012a-change-005.md), [통합 구현 보고](../tasks/workflow-continuity-001-change-017-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-013A` | [구현 보고서](../tasks/013a-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-014A` | [구현 보고서](../tasks/014a-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-EXPORT-001` | [Change 002](../tasks/export-001-implementation-report.md), [Change 003](../tasks/export-001-column-picker-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-EXPORT-002` | [구현 보고서](../tasks/export-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-E2E-FULL-SUITE-001` | [구현 보고서](../tasks/e2e-full-suite-001-implementation-report.md), [Change 008](../tasks/e2e-full-suite-001-change-008-implementation-report.md), [Change 009](../tasks/e2e-full-suite-001-change-009-implementation-report.md), [Change 010](../tasks/e2e-full-suite-001-change-010-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-BILLING-REQUEST-001` | [구현 보고서](../tasks/billing-request-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-UX-001 A1` | [구현 보고서](../tasks/ux-001-implementation-report.md) | `EXPERIMENT_SLICE_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-UX-001 A2` | [구현 보고서](../tasks/ux-001-a2-implementation-report.md) | `EXPERIMENT_SLICE_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-NOTIFY-005` | [구현 보고서](../tasks/notify-005-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-SALES-KPI-001` | [본체](../tasks/sales-kpi-001-implementation-report.md), [Change 002](../tasks/sales-kpi-001-change-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-ADMIN-002` | [구현 보고서](../tasks/admin-002-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-PRODUCTION-CONTROL-001` | [최종 기획](43-production-control-plan.md), [Change 006](../tasks/production-control-001-change-006.md), [Change 007](../tasks/production-control-001-change-007.md), [Change 008](../tasks/production-control-001-change-008.md), [Change 009](../tasks/production-control-001-change-009.md), [구현 보고서](../tasks/production-control-001-implementation-report.md), [통합 구현 보고](../tasks/workflow-continuity-001-change-017-implementation-report.md), [검수 체크리스트](../tasks/production-control-001-user-validation-checklist.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `DESIGN-000` | [본체](../tasks/design-000-implementation-report.md), [Change 001](../tasks/design-000-change-001-implementation-report.md), [Change 002](../tasks/design-000-change-002-implementation-report.md), [Change 003](../tasks/design-000-change-003-implementation-report.md), [Change 004](../tasks/design-000-change-004-implementation-report.md), [Change 005](../tasks/design-000-change-005-implementation-report.md), [Change 006 계약](../tasks/design-000-change-006.md), [Change 006 구현 보고](../tasks/design-000-change-006-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE`; Change 006 `MAIN_MERGED / USER_VALIDATION_COMPLETE` |
| `TASK-PENDING-TYPE-001` | [구현 보고서](../tasks/pending-type-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-QR-001` | [구현 보고서](../tasks/qr-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-NOTIFY-AUDIT-001` | [구현 보고서](../tasks/notify-audit-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-NOTIFY-REPROCESS-001` | [구현 보고서](../tasks/notify-reprocess-001-implementation-report.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE` |
| `TASK-WORKFLOW-CONTINUITY-001` | [본체](../tasks/workflow-continuity-001-implementation-report.md), [Change 005](../tasks/workflow-continuity-001-change-005-implementation-report.md), [Change 006](../tasks/workflow-continuity-001-change-006-implementation-report.md), [Change 007](../tasks/workflow-continuity-001-change-007-implementation-report.md), [Change 008](../tasks/workflow-continuity-001-change-008-implementation-report.md), [Change 009](../tasks/workflow-continuity-001-change-009-implementation-report.md), [Change 010](../tasks/workflow-continuity-001-change-010-implementation-report.md), [Change 011](../tasks/workflow-continuity-001-change-011-implementation-report.md), [Change 012](../tasks/workflow-continuity-001-change-012-implementation-report.md), [Change 013](../tasks/workflow-continuity-001-change-013-implementation-report.md), [Change 014](../tasks/workflow-continuity-001-change-014-implementation-report.md), [Change 015](../tasks/workflow-continuity-001-change-015-implementation-report.md), [Change 016](../tasks/workflow-continuity-001-change-016-implementation-report.md), [Change 017](../tasks/workflow-continuity-001-change-017-implementation-report.md) | `LOCAL_MAIN_MERGED / REMOTE_UNPUBLISHED` |
| `TASK-ATTACHMENT-001` | [최종 기획](45-pending-action-attachment-plan.md), [구현 보고서](../tasks/attachment-001-implementation-report.md), [검수 체크리스트](../tasks/attachment-001-user-validation-checklist.md) | `LOCAL_MAIN_MERGED / REMOTE_UNPUBLISHED` |
| `TASK-QUALITY-OPERATING-MODEL-001` | [최종 기획](48-enclosure-iqc-routing-plan.md), [구현 보고서](../tasks/quality-operating-model-001-implementation-report.md), [검수 체크리스트](../tasks/quality-operating-model-001-user-validation-checklist.md) | `LOCAL_MAIN_MERGED / REMOTE_UNPUBLISHED` |
| `TASK-UL891-SET-001` | [최종 기획](41-ul891-panel-set-plan.md), [Change 005](../tasks/ul891-set-001-change-005-implementation-report.md), [Change 006](../tasks/ul891-set-001-change-006-implementation-report.md), [Change 007](../tasks/ul891-set-001-change-007-implementation-report.md), [Change 008](../tasks/ul891-set-001-change-008-implementation-report.md), [Change 009](../tasks/ul891-set-001-change-009-implementation-report.md), [구현 보고서](../tasks/ul891-set-001-implementation-report.md), [검수 체크리스트](../tasks/ul891-set-001-change-009-user-validation-checklist.md) | `EXPERIMENT_COMPLETE / USER_VALIDATION_COMPLETE / MAIN_MERGED`; PR #65 merge commit `79b90b8` |

## 남은 범위 찾기

운영 첨부 storage·scanner·backup/restore는 `TASK-ATTACHMENT-001`의 운영 후속이다. 운영 hosting·redirect·Teams/provider·migration은 기존 UAT/Azure Task에서 실제 남은 범위를 확인한다. 이미 공개된 환경을 과거 “운영 전환 Task ID 미정” 문구 때문에 처음부터 만들지 않는다.

양식 content, 품질 후속 운영 모델, Pending·물류·정산 정책, 관리자 기준정보, 사용자 lifecycle, 알림 event/기한, 달력, 보존·감사, break-glass 입력은 원문 §4.1에 보존했다. P3·최적화 후보는 §5에 있다. 미확정 입력을 완료 기능의 결함이나 새 실행 승인으로 바꾸지 않는다.

## 재개·검수·승격

같은 scope의 수정 요청은 기존 Task/change로 진행한다. 새 업무 능력은 범위를 구분한다. 실험에서 인터뷰·중간 승인 생략과 권장안 채택의 standing instruction이 유효하면 같은 Task·branch·환경·안전 경계 안에서 계속 적용하며, 모델 이름 변경만으로 같은 승인을 다시 요구하지 않는다.

기존 41166/42983 실험 runtime과 5174 승격은 원문 §6 및 `TASK-EXPERIMENT-PROMOTION-001`의 당시 실행 이력이다. 지금 실행 중이거나 새 handover가 승인됐다는 뜻이 아니다. 실제 사용자 검수와 main·UAT·운영 반영은 각 대상의 증거로 판정한다. `BATCHED_FINAL`을 이유로 완료 기능을 다시 만들지 않는다.
