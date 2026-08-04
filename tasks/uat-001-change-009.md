# TASK-UAT-001 Change 009 — 프로젝트 진행률·현재 단계 일치

## 1. Task Identity Gate

- proposedTaskId: `TASK-UAT-001 Change 009`
- taskType: `BUGFIX`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-AZURE-DEPLOY-001`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-UAT-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `false`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

## 2. 승인과 목적

- 사용자는 2026-08-04에 프로젝트 목록의 진행률이 전체 흐름과 다르고, 현재 단계가 실제 도달한 최상위 단계가 아니라 앞쪽 미완료 단계로 표시되는 결함 수정을 요청했다.
- 추가 결정으로 구매정보·자재 도착·입고검사·입고확정은 뒤 단계가 실제로 시작되면 개별 상태를 바꾸지 않고 진행률 계산에서만 완료로 간주한다.
- 기존 `fix/task-uat-001-current-main-handover` branch와 HTTPS 5174·Backend 5081·fresh UAT DB를 재사용한다.
- commit·push·PR·merge, migration·DB data 변경, 실제 provider 발송은 포함하지 않는다.

## 3. Root Finding

- `UAT-WORKFLOW-009-A` P1: 프로젝트 목록 SQL은 전체 17/18단계 중 초기 4단계만 계산해 전체 흐름 71%인 프로젝트를 18%로 표시했다.
- `UAT-WORKFLOW-009-B` P1: 전체 흐름은 가장 앞의 미완료 단계를 현재 단계로 선택해 납품과 영업 인계까지 끝난 프로젝트도 구매정보로 되돌려 표시했다.
- `UAT-WORKFLOW-009-C` P2: 선택적으로 건너뛸 수 있는 구매정보·자재 도착·입고검사·입고확정이 뒤 단계 진행 후에도 진행률 분모에서 미완료로 남았다.

## 4. 승인된 계약

1. 프로젝트 목록 진행률은 전체 흐름의 단일 계산 결과를 사용한다.
2. 현재 단계는 가장 뒤에서 실제 도달한 단계를 사용한다. 가장 뒤의 도달 단계가 완료 상태이면 다음 필수 단계를 현재 단계로 표시한다.
3. 프로젝트 자체의 보류·완료·취소 표시는 workflow 단계보다 우선한다.
4. 구매정보·자재 도착·입고검사·입고확정은 자신보다 뒤의 필수 단계가 시작된 경우 진행률 계산에서만 완료로 센다.
5. 위 네 단계의 실제 상태와 입력 데이터는 변경하지 않는다.

## 5. 구현과 검증

- `WorkflowStore`가 실제 도달 순서 기준으로 현재 단계를 선택하도록 변경했다.
- 진행률용 완료 단계 수에 네 개의 통과 가능 단계를 조건부 포함했다.
- 프로젝트 목록 응답을 전체 흐름 요약으로 정규화하고 보류·완료·취소 상태는 보존했다.
- 물류 완료 후 앞 단계가 남은 회귀에서 실제 단계 상태는 미완료로 유지하면서 진행률만 `47%`, 현재 단계는 `SalesSettlementCompleted`가 되는 것을 검증했다.
- 집중 회귀 `2/2`, 프로젝트 등록·목록·물류 확대 회귀 `77/77`, 패널정보·QR 회귀 `38/38`, Release build warning/error `0/0`을 통과했다.

## 6. 실제 프로젝트 결과와 Runtime 상태

- 변경 전 두 대상 프로젝트: 목록 `18% / 구매정보`, 전체 흐름 `71% / 구매정보`, 실제 완료 `12/17`.
- 실제 stage projection상 구매정보·자재 도착·입고검사·입고확정 이후 제조·품질·물류·납품과 영업 인계 요청까지 도달했다.
- 공식 UAT 실행기가 소유권을 확인한 뒤 5174·5081을 정상 교체했다. 기존 fresh DB를 보존했고 migration·seed·data 변경과 실제 provider 발송은 실행하지 않았다.
- 104·105 모두 실제 API와 HTTPS 5174에서 `16/17`, `94%`, 현재 단계 `SalesSettlementCompleted(영업 / 세금계산서)`로 확인했다.
- 프로젝트 목록도 두 프로젝트 모두 `영업 정산`, `94%`로 전체 흐름과 일치했다.
- 네 단계의 실제 상태는 모두 `PartiallyCompleted`로 유지됐다.
- 104·105 desktop/mobile layout에서 값을 확인했고 browser console error는 `0`건이다.

## 7. Finding 상태

| Finding | 심각도 | 상태 | 해소·후속 |
| --- | --- | --- | --- |
| `UAT-WORKFLOW-009-A` | P1 | `RESOLVED` | 목록을 canonical workflow 요약으로 정규화하고 실제 5174 확인 |
| `UAT-WORKFLOW-009-B` | P1 | `RESOLVED` | 최상위 도달 단계 선택과 두 프로젝트 전체 흐름 확인 |
| `UAT-WORKFLOW-009-C` | P2 | `RESOLVED` | 네 단계의 진행률 전용 완료 보정과 실제 상태 불변 확인 |

## 8. 사용자 검수 Checklist

- [ ] HTTPS 5174 프로젝트 목록에서 두 프로젝트가 `94%`인지 확인한다.
- [ ] 목록 현재 단계가 `세금계산서`인지 확인한다.
- [ ] 각 프로젝트 전체 흐름에서 `16/17`, `94%`, 현재 단계 `세금계산서`인지 확인한다.
- [ ] 구매정보·자재 도착·입고검사·입고확정의 개별 상태와 기존 입력은 그대로인지 확인한다.

자동·Codex browser 검수는 네 항목 모두 통과했다. 위 checklist는 사용자의 최종 화면 검수 상태와 별도로 유지한다.

## 9. 게시 승인

- 2026-08-04 사용자가 현재 수정의 원격 `main` 병합을 명시적으로 승인했다.
- 게시 대상은 이 Change의 Backend·회귀, 생산계획 진행률 막대 Frontend 수정과 관련 Task 기록이다.
- 실제 입력 DB, runtime artifact, 환경 설정과 credential은 게시하지 않는다.
