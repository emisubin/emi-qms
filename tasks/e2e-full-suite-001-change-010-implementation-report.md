# TASK-E2E-FULL-SUITE-001 Change 010 구현 보고서

## 1. 결과와 상태

- Task 유형: `P2_REMEDIATION`
- 기준 HEAD: `a7651b5c266d73be48e76861a02910435c1371fe`
- 상태: `EXPERIMENT_COMPLETE / BATCHED_FINAL`
- 변경 계약: [Change 010](e2e-full-suite-001-change-010.md)
- 대표 repo·`main`·Persistent UAT·실제 provider 영향: 없음
- main merge 승인: `0/3`

일반 1면과 12면 stress 실제 역할 lifecycle이 현재 UI를 그대로 따라 다시 완주하도록 회귀 기준선을 복구했다. 제품을 과거 UI로 되돌리지 않고, 검사가 현재 확정된 프로젝트 우선 목록·접기 입력·파생 품질 판정·물류 1회 저장/확정·정산 저장을 사용한다.

## 2. 갱신한 사용자 동선

| 영역 | 현재 E2E 계약 |
| --- | --- |
| 프로젝트 등록 | FAT는 native select가 아니라 선택 그룹의 `필요` 버튼으로 입력 |
| 생산관리 | `/production-planning/plans`와 `/releases` 업무 페이지 사용, 접힌 생산계획표·담당자 section을 연 뒤 입력 |
| 자재 | 프로젝트 행을 먼저 펼치고 구매품목을 선택, `도착분 저장` 사용, 완료 품목은 `완료 포함`으로 재확인 |
| IQC | IQC 프로젝트 목록에서 프로젝트를 먼저 연 뒤 구매품목 도착분 검사 |
| LQC·OQC | 항목 결과로 자동 계산된 파생 합격을 확인하고 확정; 전진검수·FAT만 통합 판정 선택 |
| 물류 | 패널을 선택하고 증빙을 먼저 첨부한 뒤 `포장/출발/납품 저장 및 확정` 한 번으로 처리 |
| 정산 | `발행 확인 저장`으로 회계 확인값을 저장하고 최종 완료 |

## 3. 검증 결과

| 시나리오 | 결과 |
| --- | --- |
| 일반 실제 역할 lifecycle | `PASS 1/1`, 52.1초 |
| 일반 workflow | 완료 stage `18`, open Pending `0`, project `Completed` |
| 세금계산서 | 선택 발행요청 workbook 생성 후 발행 확인·프로젝트 완료 |
| 12면 stress lifecycle | `PASS 1/1`, 1.9분 |
| stress 자재 | 사급 분할 도착 `6회`, 도급 도착 `1회` |
| stress Pending | 반복 제조 Pending `6건`, 외부 delivery channel 2종, 종료 후 open `0` |
| stress workflow | 완료 stage `18`, project `Completed` |
| E2E isolation | 실행별 PostgreSQL DB·container·network 생성 후 모두 제거 |
| 실제 provider·고정 DB | 호출·변경 없음 |

## 4. 변경 파일

| 파일 | 역할 |
| --- | --- |
| `frontend/e2e/full-stack/project-lifecycle-user-validation.full-stack.spec.ts` | 현재 일반 18단계 UI·layout·대비 회귀 |
| `frontend/e2e/full-stack/project-lifecycle-stress-user-validation.full-stack.spec.ts` | 현재 12면·분할입고·반복 Pending UI 회귀 |

## 5. Finding gate

| Finding | Severity | 상태 |
| --- | --- | --- |
| `E2E-LIFECYCLE-UI-CONTRACT-DRIFT` | P2 | `RESOLVED` |

Open P0/P1/P2: `0/0/0`.

## 6. 사용자 검수

자동 회귀는 완료했다. 실험 branch 정책에 따라 사용자 직접 검수는 마지막 일괄 검수로 유지한다.

- [ ] 일반 프로젝트 생성부터 세금계산서 완료까지 주요 화면 재확인
- [ ] 12면·분할 입고·반복 Pending 프로젝트에서 담당자 동선 확인
- [ ] 물류 1회 저장/확정과 정산 발행 확인 저장 확인

## 7. Rollback

두 lifecycle spec과 Change 010 문서만 되돌린다. 제품 코드·DB·migration rollback은 없다.

## 8. 종료 산출물

| 산출물 | 상태 | 위치 |
| --- | --- | --- |
| Implementation report | 작성됨 | 본 문서 |
| SOP | 포함됨 | 2·3·7절 |
| User manual | 포함됨 | 2·6절 |
| Roadmap update | 작성됨 | `docs/00-product-roadmap.md` |
| User validation checklist | 포함됨 / 마지막 일괄 검수 | 6절 |
