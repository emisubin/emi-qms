# TASK-G2-OPERATIONS-002 Change 001 — 홈 직접 입력 박스 가운데 정렬

- taskType: `APPROVED_FEATURE_IMPLEMENTATION`
- status: `IMPLEMENTED`
- approvedSource: `USER_EXPLICIT_REQUEST`
- 작성일: 2026-09-01

## 요청

G2 홈 생산 현황표에서 바로 입력하는 박스 자체를 각 날짜 셀의 가운데에 정렬한다.

## 변경 범위

- `.g2-preview-input`을 block 요소로 만들고 좌우 자동 여백을 적용한다.
- 숫자의 시각적 중심을 밀어내는 browser 기본 증감 버튼을 해당 입력칸에서만 숨긴다.
- `type="number"`, 0 이상 정수 제한과 모바일 숫자 키패드 계약은 유지한다.
- 입력 숫자와 placeholder의 기존 가운데 정렬, 예상 파랑과 휴일 빨강 우선 규칙은 유지한다.
- 생산/출하 관리 등 다른 입력 화면은 변경하지 않는다.

## 검증

- Frontend 전체 unit `241/241` 통과
- Frontend typecheck 통과
- Frontend production build 통과, 기존 대형 chunk warning만 유지
- 최종 isolated Full-Stack `1/1` 통과, `appearance: textfield`와 셀 좌우 여백 차이 `1px` 이하 자동 확인
- 열린 검수 서버에서 `display: block`, `text-align: center`, `appearance: textfield`와 증감 버튼 제거를 시각 확인
- 입력은 계속 `type="number"`, `inputmode="numeric"`이며 synthetic 값 `24`가 박스 중앙에 표시됨을 확인
