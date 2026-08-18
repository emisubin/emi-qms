# TASK-PRODUCTION-CONTROL-001 Change 014 — 기존 Item 제조양식 snapshot 배포 보정

## Task Identity Gate

- proposedTaskId: `TASK-PRODUCTION-CONTROL-001`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: Change 013 배포 후 사용자 운영 검수
- roadmapNextGate: 확인된 운영 제조양식 불일치 보정
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-PRODUCTION-CONTROL-001`
- reuseExistingTask: `true`
- gateStatus: `PASS_REUSE`

## 확인된 문제

- 공개 운영의 Item 제조양식에는 현재 제조 단계가 모두 저장되어 있지만, 정책 변경 전에 생성된 같은 Item 프로젝트는 과거 제조 단계 snapshot을 계속 표시했다.
- Change 013은 제조양식을 저장하는 transaction 안에서만 기존 프로젝트를 동기화했다. 배포 전에 이미 저장된 양식과 프로젝트의 불일치를 배포 migration이 보정하지 않아, 새 코드를 배포한 뒤 양식을 다시 저장하지 않으면 불일치가 남았다.
- 기존 회귀는 `프로젝트 생성 → 새 코드로 제조양식 저장 → 프로젝트 동기화`만 검증했고, `기존 불일치 데이터 → 코드·migration 배포만 수행`하는 경로를 검증하지 않았다.

## 승인 계약

- 사용자는 2026-08-18 원인 보고를 확인한 뒤 수정의 원격 `main` 병합과 Azure 공개배포를 명시 승인했다.
- 사용자 운영 검수는 migration이 적용된 공개 운영에서 수행한다.

## 구현 계약

1. 제조양식 저장 시 동기화 대상은 `project_production_plans` 존재 여부가 아니라 삭제되지 않은 프로젝트의 실제 Item 코드로 판단한다.
2. additive migration `0080`은 활성 Item 제조양식이 있는 삭제되지 않은 기존 프로젝트의 활성 제조 단계 snapshot을 현재 definition·순서·이름으로 보정한다.
3. 기존 definition은 갱신하고 삭제 definition은 비활성 이력으로 보존하며 새 definition은 추가한다.
4. 제조 execution, execution step, 완료 상태와 생산계획 항목·기간·담당자·실적 연결은 변경하지 않는다.
5. migration은 fresh DB와 `0079`까지 적용된 기존 DB 모두에서 실행 가능해야 한다.

## 검증 계약

- 생산계획 행이 없는 같은 Item 프로젝트도 이후 제조양식 저장에서 동기화된다.
- `0079` 상태의 기존 프로젝트 snapshot이 `0080` 적용만으로 현재 양식으로 보정된다.
- 과거 definition은 비활성 이력으로 남고 현재 definition만 활성화된다.
- migration 재실행은 schema ledger 기준 멱등이며 migration catalog의 최신 번호가 `0080`이다.
- Backend Release build, 관련 Backend·migration 회귀, migration fresh/existing과 표준 CI를 통과한다.

## Finding

| Finding ID | 심각도 | 상태 | 원인·영향 | Resolution |
| --- | --- | --- | --- | --- |
| `PC-C014-DEPLOY-BACKFILL-GAP-001` | P1 | `RESOLVED` | 저장 이벤트에만 동기화를 걸어 배포 전부터 어긋난 기존 프로젝트가 배포만으로 보정되지 않았다. | migration `0080`, Item 기준 저장 동기화와 기존 DB 회귀로 보정했다. |

## 게시·복구 경계

- migration은 기존 migration을 수정하지 않는 additive forward-fix다.
- destructive down migration은 만들지 않는다. 문제 발생 시 애플리케이션을 직전 image로 되돌리고, snapshot은 현재 Item 제조양식에서 다시 생성하는 forward-fix를 사용한다.
- 운영 적용 순서는 migration `0080` → Backend → Frontend이며, 적용 후 공개 운영에서 Item 제조양식 단계 수와 프로젝트 실적 선택 단계 수가 일치하는지 개인정보 없는 count로 확인한다.
