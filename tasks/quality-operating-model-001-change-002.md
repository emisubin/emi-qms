# TASK-QUALITY-OPERATING-MODEL-001 Change 002

## Task Identity Gate

- proposedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- taskType: `NEW_FEATURE`
- instructionChainRead: true
- instructionConflictCount: 0
- roadmapExpectedTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- roadmapNextGate: `확정된 품질 운영 정책 기획·구현`
- roadmapSequenceMatch: true
- samePurposeMatchCount: 1
- canonicalTaskId: `TASK-QUALITY-OPERATING-MODEL-001`
- reuseExistingTask: true
- explicitRoadmapOverrideApproved: false
- experimentStandingInstructionApplies: true
- policyInputResolution: `USER_CONFIRMED`
- gateStatus: `PASS_REUSE`

## 사용자 승인 범위

사용자는 기능 업데이트 이후 생성되는 프로젝트에 한해 구매품 구분으로 IQC 여부를 결정하고, 외함은 서명 스캔본 기반 IQC를 수행하며, 비검사품은 IQC 없이 자재 입고 확정으로 연결하는 기능의 기획과 구현을 시작하도록 명시했다.

## 구현 계약

1. 프로젝트에 IQC routing 정책 snapshot을 저장한다.
2. 기존 프로젝트는 `모든 구매품 IQC`, 신규 프로젝트는 `구분 기반 IQC`를 사용한다.
3. 양식 관리에서 구매품 구분과 구분별 IQC 필요 여부를 관리한다.
4. 신규 정책 프로젝트의 구매 입력에는 구분 선택을 필수로 한다.
5. 외함 도착분은 PDF·JPEG·PNG 다중 첨부와 적합·부적합 판정을 제공하는 스캔형 IQC를 생성한다.
6. 적합·부적합 모두 서명 스캔본을 필수로 하고, 확정 후 수정할 수 없으며, 재검사는 새 회차로 보존한다.
7. 외함 부적합은 기존 Pending·조치·재검사 흐름에 연결한다.
8. IQC가 없는 품목은 도착 후 `검사 대상 아님`으로 표시하고 자재 입고 확정 업무로 바로 연결한다.
9. 도급·사급 구분은 IQC routing에 영향을 주지 않는다.
10. 기존 상세 IQC와 기존 프로젝트 데이터는 그대로 보존한다.

## 제외 범위

- LQC·OQC·전진검수·FAT의 추가 운영 모델 변경
- 협력사용 IQC Excel 양식 생성·배포
- 판금류·부스바·명판의 신규 검사 정책
- 기존 프로젝트·구매품 자동 분류
- 대표 repo·`main`·push·PR·merge
- Persistent UAT migration·runtime handover
- Teams·메일 등 실제 외부 provider 발송

## Fable fast-track 승인

- fableSecondPlanningApproved: true
- fableSecondPlanningSource: `USER_EXPLICIT_EXPERIMENT_RULE`
- fableSecondPlanningTarget: `docs/48-enclosure-iqc-routing-plan.md`
- implementationApproved: true
- localCommitApproved: false
- mainMergeApproved: false
- persistentUatApproved: false
- externalProviderApproved: false

## Fable 5시간·주간 사용량

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| Fable 1차 기획 직전 | 0% 사용 / 100% 잔여 / 20:39 KST 초기화 | 14% 사용 / 86% 잔여 / 8월 1일 07:59 KST 초기화 | 26% 사용 / 74% 잔여 / 8월 1일 07:59 KST 초기화 |
| Fable 1차 기획 직후 | 0% 사용 / 100% 잔여 / 20:40 KST 초기화 | 14% 사용 / 86% 잔여 / 8월 1일 07:59 KST 초기화 | 27% 사용 / 73% 잔여 / 8월 1일 07:59 KST 초기화 |
| Fable 2차 기획 직전 | 0% 사용 / 100% 잔여 / 20:40 KST 초기화 | 14% 사용 / 86% 잔여 / 8월 1일 07:59 KST 초기화 | 27% 사용 / 73% 잔여 / 8월 1일 07:59 KST 초기화 |
| Fable 2차 기획 직후 | 9% 사용 / 91% 잔여 / 20:40 KST 초기화 | 27% 사용 / 73% 잔여 / 8월 1일 08:00 KST 초기화 | 21% 사용 / 79% 잔여 / 초기화 시각 파싱 불가 |

2차 기획 직후 Claude `/usage` TUI가 직전 측정과 비교해 주간 전체 사용량은 증가했지만 주간 Fable 사용량은 감소한 값을 표시했다. 측정값을 임의 보정하지 않고 화면 projection 결과를 그대로 기록한다.

## 검증 계약

- 기존 프로젝트는 기존 상세 IQC 흐름을 유지한다.
- 신규 프로젝트에서 외함은 도급·사급 모두 스캔형 IQC로 연결된다.
- 신규 프로젝트에서 IQC가 없는 구분은 IQC 업무를 만들지 않고 자재 입고 확정 업무를 만든다.
- 신규 정책 프로젝트의 구분 누락 구매 저장은 사용자에게 이해 가능한 오류로 차단한다.
- 서명 스캔본은 다중 PDF·JPEG·PNG를 지원하고 파일 형식·크기·실제 signature를 검증한다.
- 확정 검사와 첨부는 수정·삭제되지 않는다.
- 부적합·조치·재검사에서 각 회차 증빙이 모두 남는다.
- 양식 관리 권한·비활성화·참조 보존을 검증한다.
- Backend·Frontend 자동 테스트와 desktop·390px 핵심 화면을 검증한다.
