# TASK-009A Change 002 — IQC 근거·정확한 업무 이동·재검사 종결

## 사용자 검수 실패

- IQC 내 업무가 특정 검사 대신 프로젝트 상세로 이동했다.
- 부적합 근거가 부족해도 판정할 수 있었고, 재검사 알림·업무와 workflow 상태가 일치하지 않았다.

## 승인된 변경

- IQC 업무·알림의 canonical link를 `/quality/iqc?request={attemptId}`로 통일하고 해당 검사를 자동 선택한다.
- 부적합 최종화는 사진 1장 이상 또는 30자 이상의 구체 사유를 서버에서 요구한다.
- 재검사 요청은 Pending 조치 완료 transaction에 결합하고 중복 호출은 같은 attempt를 반환한다.
- 재검사 합격 시 receipt, Pending, 업무와 IQC workflow를 함께 완료하며 완료 stage는 미시작으로 회귀하지 않는다.

## 경계

- 이미 final 상태인 과거 검사 snapshot은 바꾸지 않는다.
- 사진 binary 저장소 정책은 별도 후속 범위다.
