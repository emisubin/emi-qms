# TASK-E2E-FULL-SUITE-001 change-005

## Task Identity Gate

- proposedTaskId: `TASK-E2E-FULL-SUITE-001 change-005`
- taskType: `P2_REMEDIATION`
- instructionChainRead: `true`
- instructionConflictCount: `0`
- roadmapExpectedTaskId: `TASK-E2E-FULL-SUITE-001`
- roadmapNextGate: `USER_VALIDATION_REMEDIATION`
- roadmapSequenceMatch: `true`
- samePurposeMatchCount: `1`
- canonicalTaskId: `TASK-E2E-FULL-SUITE-001`
- reuseExistingTask: `true`
- explicitRoadmapOverrideApproved: `false`
- experimentStandingInstructionApplies: `true`
- experimentLedgerSelectedTask: `TASK-E2E-FULL-SUITE-001`
- policyInputResolution: `N/A`
- gateStatus: `PASS_REUSE`

## Purpose identity

- 업무 목표: 생산관리 탭과 동일하게 각 부서 탭에서 해당 프로젝트에 입력된 부서 업무 데이터 전체를 확인할 수 있게 한다.
- Root Finding 또는 정책 결정: `PROJECT-DEPARTMENT-DATA-PROJECTION-INCOMPLETE` — Change 003은 대표 지표와 요약 행만 표시해 전용 입력 화면의 저장 필드 전체를 프로젝트 상세에서 확인할 수 없다.
- 변경·검증 경계: 영업·설계·구매·자재·제조·품질·물류 입력 계약과 현재 탭 projection을 대조하고, 이미 저장된 데이터를 조회 전용 상세 구조로 추가한다. 기존 담당자 수정 진입과 Backend 권한은 유지한다.
- 보존할 불변조건: 18단계 순서, 부서별 mutation 권한, 담당자 범위, 기존 API 상태 전이, 대표 repo·`main`, Persistent UAT와 실제 provider를 변경하지 않는다.
- 예상 산출물: 부서별 입력 필드 coverage matrix, 누락 projection 구현, unit·isolated lifecycle·desktop/mobile browser 검증, privacy-safe 증빙과 Implementation report 갱신.

## 검색 범위

- [x] `tasks/`의 기존 Task·change·implementation report — 같은 목적은 Change 003 한 건
- [x] Product Roadmap·실험 완료 원장 — 사용자 검수 실패 시 기존 change 재개
- [x] Local/remote branch와 worktree — 현재 experiment branch 한 건, 대표 `main` 별도 보존
- [x] Open/merged PR — 현재 branch 대상 PR 0건

## 사용자 확정 변경

1. 생산관리 탭처럼 각 부서 탭에 해당 부서가 실제 입력한 저장 데이터를 표시한다.
2. 누락된 입력값이 있으면 기존 조회 API를 조합하거나 최소한의 read contract를 추가해 구현한다.
3. 모든 부서는 조회 가능하지만 수정은 기존 담당자·권한·프로젝트 상태 gate를 그대로 사용한다.
4. 구현 뒤 부서별 coverage와 실제 화면을 검증한다.

## 제외 범위

- 신규 업무 입력 필드·상태·권한·외부 연동
- 실제 Teams·메일 provider, Persistent UAT, 대표 repo·`main`, push·PR·merge

## 부서별 입력 데이터 coverage

| 탭 | 입력 데이터 projection | 결과 |
| --- | --- | --- |
| 영업 | 프로젝트 등록·수정값, 판매금액, 납품·FAT, 정산, 회계 발행요청·확인값 | `PASS` |
| 생산관리 | 계획 항목, 일정 캘린더, 부서별 정·부 담당자 | 기존 직접 데이터 구성 유지 |
| 설계 | 패널명, 크기, 설계정보 완료 상태 | 기존 직접 데이터 구성 유지 |
| 구매 | 품목, 공급처, 발주·입고 예정 정보 | 기존 직접 데이터 구성 유지 |
| 자재 | 품목 누계, 도착·IQC·입고 확정 회차, 성적서 상태, 키팅 완료 담당·시각 | `PASS` |
| 제조 | 패널 실행값, checklist 응답, 시작·완료, Pending, 전체 event 이력 | `PASS` |
| 품질 | LQC·OQC·입회검사·FAT별 checklist 응답, 판정·사유, 사진, 차수 이력 | `PASS` |
| 물류 | 포장·출발·납품 입력, 구성 패널·포장단위, 담당·시각, 증빙 metadata | `PASS` |

## 구현·검증 결과

- 공통 부서 입력 record·field·history 컴포넌트를 추가하고 desktop 4열·mobile 2열 정보 구조를 적용했다.
- 완료 프로젝트의 키팅 기록도 특정 프로젝트 상세 조회에서 반환하며, 일반 자재 대기열은 진행 프로젝트만 표시하는 기존 계약을 유지한다.
- 물류는 프로젝트 단위 read-only history endpoint를 추가해 완료 후 queue에서 사라지는 draft·확정 입력과 증빙 metadata를 보존해 표시한다.
- 부서별 수정 진입은 기존 permission·`canMutate` gate를 유지하고 모든 다른 사용자는 조회 전용이다.
- 실제 역할 18단계 lifecycle을 disposable PostgreSQL과 provider-disabled runtime에서 다시 실행해 최종 `18/18`, open Pending `0`, department tab record/field 존재, 자재 키팅 기록과 desktop/mobile overflow `0`을 확인했다.

## 완료 상태

- Finding `PROJECT-DEPARTMENT-DATA-PROJECTION-INCOMPLETE`: `RESOLVED`
- 신규 Open P0/P1/P2: `0/0/0`
- 사용자 직접 검수: `BATCHED_FINAL` — 마지막 일괄 검수 대기
- Git 게시: local experiment worktree 미커밋 상태. 대표 repo·`main`·push·PR·merge 미반영
