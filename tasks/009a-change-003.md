# TASK-009A change-003

## Task Identity Gate

- purposeIdentity: IQC 사진 필수 검사항목에서 판정과 동시에 사진을 첨부하게 입력 흐름을 수정한다.
- canonicalTask: `TASK-009A`
- taskType: `BUGFIX`
- branch: `experiment/task-home-002-personalized-shell`
- baseHead: `48fe8cd78293`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- approvalSource: 사용자의 “권장 구현 순서대로 구현하자. 시작해” 지시

## 포함 범위

- 사진 필수 IQC 항목의 판정·근거 바로 아래에 항목 전용 첨부 UI를 배치한다.
- 모든 항목 입력 뒤 별도 사진 단계로 이동하는 흐름을 제거한다.
- 기존 서버 측 사진 필수 검증과 항목별 사진 연결은 유지한다.

## 검증

- 필수 사진 누락 시 확정 차단
- 항목별 업로드·삭제·재조회
- IQC 화면 테스트와 frontend 회귀 테스트
