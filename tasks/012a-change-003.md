# TASK-012A Change 003 — 패널 품질검사 판정 단위 정합성

## Task Identity Gate

- proposedTaskId: `TASK-012A`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-012A`
- roadmapNextGate: `USER_VALIDATION_BATCHED_FINAL`
- roadmapSequenceMatch: `false`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-012A`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `true`
- experimentStandingInstructionApplies: `true`
- policyInputResolution: `USER_DECISION`
- gateStatus: `PASS_REUSE`

### Purpose identity

- 업무 목표: IQC·OQC·전진검수·FAT의 처리 단위와 판정 모델을 사용자 확정 정책에 맞춰 대조한다.
- Root Finding 또는 정책 결정: IQC는 구매 품목 도착분, OQC·전진검수·FAT는 개별 패널 단위다. OQC만 항목별 적합·부적합을 가지며 전진검수·FAT는 패널당 통합 판정 1회다.
- 변경·검증 경계: 현재 Frontend·Backend·DB 계약을 대조하고, 프로젝트 상세 진척 계산은 확정 단위를 즉시 반영한다. 전진검수·FAT 입력 모델 변경은 확인된 P2 후속 범위로 분리한다.
- 보존할 불변조건: 완료 검사 이력·PDF·증빙의 append-only 성격, Pending 재검사 연결, 패널별 원자성과 서버 권한을 유지한다.
- 예상 산출물: 구현 정합성 표, 확인된 차이와 수정 방안, 프로젝트 상세 품질 진척 계산 반영, 후속 P2 추적.

## 1. 확정 정책과 현재 구현

| 검사 | 확정 처리 단위 | 확정 판정 모델 | 현재 구현 | 판정 |
| --- | --- | --- | --- | --- |
| IQC | 구매 품목의 개별 도착분 | 도착분 검사 성적서 | `material_receipts`와 IQC attempt/report가 구매 품목 도착분에 연결됨 | 일치 |
| OQC | 개별 패널 | OQC 항목별 적합·부적합 | 패널+OQC attempt와 template item별 `Pass/Fail/NotApplicable` 응답 | 일치 |
| 전진검수 | 개별 패널 | 단계 없는 통합 적합·부적합 1회 | 패널 단위이지만 2개 Check 항목+메모를 요구하는 공통 체크리스트 | 불일치 |
| FAT | 개별 패널 | 단계 없는 통합 적합·부적합 1회 | 패널 단위이지만 4개 Check 항목+메모를 요구하는 공통 체크리스트 | 불일치 |

## 2. 프로젝트 상세에 즉시 반영할 계산 계약

- IQC는 패널 품질 진척률에서 제외한다.
- OQC 분모는 해당 패널 검사 양식의 Check 항목 수다. 검사가 아직 시작되지 않았으면 현재 활성 OQC 양식의 Check 항목 수를 사용한다.
- OQC 분자는 적합·부적합·해당없음 중 하나가 저장된 Check 항목 수다.
- 전진검수는 패널당 1단위, FAT는 필수 프로젝트에서만 패널당 1단위다.
- 품질 전체 진척률은 모든 활성 패널의 `(OQC 완료 항목 + 전진검수 완료 + FAT 완료) / (OQC 항목 + 전진검수 1 + 선택 FAT 1)` 합계로 계산한다.
- LQC는 별도 완료 KPI로 표시하되 위 진척률 분모에는 포함하지 않는다.

## 3. 확인된 P2와 권장 수정안

### `012A-AGGREGATE-DECISION` — OPEN P2

- 현재 전진검수·FAT는 OQC와 같은 다항목 성적서 입력 UI와 Backend validation을 사용한다.
- API에 `decisionMode: Checklist | Aggregate`를 명시하고 OQC는 `Checklist`, 전진검수·FAT는 `Aggregate`로 고정한다.
- Aggregate 단계의 새 검사 회차는 항목별 응답 저장을 받지 않고 패널 단위 적합·부적합, 판정 사유, 증빙과 PUNCH/Pending 연결만 저장한다.
- 기존에 최종화된 전진검수·FAT 성적서는 이력 보존을 위해 legacy read-only로 유지한다. 신규 회차부터 Aggregate 계약을 적용한다.
- Frontend는 Aggregate 단계에서 체크리스트를 숨기고 단일 판정·사유·증빙·Pending 후속 흐름만 표시한다.
- Backend는 Aggregate 단계에 item response mutation이 오면 거부하고, 최종화 시 단일 판정 필수조건을 검증한다.
- Migration은 파괴적 변환 대신 양식/회차의 decision mode를 additive하게 도입하고 기존 finalized data를 재작성하지 않는다.

## 4. 제외 범위

- 이번 change에서 전진검수·FAT 저장 모델, 기존 성적서, PDF와 재검사 상태 전이를 즉시 바꾸지 않는다.
- 대표 repo·`main`·Persistent UAT·실제 알림 provider·push·PR·merge는 변경하지 않는다.
