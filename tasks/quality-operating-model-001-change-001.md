# TASK-QUALITY-OPERATING-MODEL-001 Change 001

## 사용자 요청

사용자가 직접 조사한 실제 품질팀 운영 방식을 읽고, EMI 프로젝트 통합관리시스템의 품질 기능을 어떻게 재구성할지 Codex와 Fable이 서로 간섭하지 않는 독립 의견서 2개를 각각 Markdown으로 작성한다.

## 산출물 승인

- fablePrimaryDraftApproved: true
- fablePrimaryDraftSource: `USER_EXPLICIT_REQUEST`
- fablePrimaryDraftTarget: `docs/46-quality-system-fable-opinion.md`
- codexIndependentOpinionTarget: `docs/47-quality-system-codex-opinion.md`
- implementationApproved: false
- mainMergeApproved: false
- persistentUatApproved: false
- externalProviderApproved: false

## 독립 작성 규칙

1. Codex 의견을 Fable 호출 전에 먼저 고정한다.
2. Fable은 Codex 의견을 읽지 않는다.
3. Fable 결과를 Codex가 review·수정·통합하지 않는다.
4. 두 문서는 사용자 비교 검토용이며 제품 구현 source of truth가 아니다.

## Fable 5시간·주간 사용량

| 측정 시점 | 5시간 현재 세션 | 주간 전체 모델 | 주간 Fable |
| --- | --- | --- | --- |
| 독립 의견 작성 직전 | 34% 사용 / 66% 잔여 / 15:10 KST 초기화 | 13% 사용 / 87% 잔여 / 8월 1일 08:00 KST 초기화 | 24% 사용 / 76% 잔여 / 초기화 시각 미표시 |
| 독립 의견 작성 직후 | 34% 사용 / 66% 잔여 / 15:10 KST 초기화 | 13% 사용 / 87% 잔여 / 8월 1일 08:00 KST 초기화 | 24% 사용 / 76% 잔여 / 초기화 시각 미표시 |

측정값은 Claude `/usage`에 표시된 값을 그대로 기록했으며, 반올림된 비율이므로 호출 전후 값이 같게 보일 수 있다.
