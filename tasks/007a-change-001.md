# TASK-007A Change 001 — 검사 Pending 연속 조치

## 사용자 검수 실패

- IQC 부적합 뒤 수동 종결이 가능했고, 재검사 업무·알림이 누락돼 전체 흐름이 중단됐다.
- 댓글과 상태 이력이 분리돼 조치 경과를 한눈에 확인하기 어려웠다.

## 승인된 변경

- 검사 연계 Pending은 `조치 시작 → 처리 내용 → 조치 완료`만 제공한다.
- 조치 완료는 종결이 아니라 같은 transaction의 재검사 업무·정/부 알림 생성이다.
- 재검사 부적합은 같은 Pending을 다시 열고, 합격 transaction만 Pending·업무를 종결한다.
- comment와 상태 변경을 하나의 시간순 activity timeline으로 투영한다.

## 경계

- 일반 수동 Pending lifecycle은 유지한다.
- 실제 Teams/Mail provider 발송과 Persistent UAT 적용은 포함하지 않는다.
