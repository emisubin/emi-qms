# TASK-012A change-005

## Task Identity Gate

- purposeIdentity: LQC·OQC 사진 필수 검사항목에서 항목 판정과 사진 증빙을 같은 자리에서 입력하고 서버가 필수 여부를 검증한다.
- canonicalTask: `TASK-012A`
- taskType: `BUGFIX`
- branch: `experiment/task-home-002-personalized-shell`
- baseHead: `48fe8cd78293`
- instructionChainRead: `true`
- roadmapSequenceMatch: `true`
- gateStatus: `PASS_REUSE`
- approvalSource: 사용자의 “권장 구현 순서대로 구현하자. 시작해” 지시

## 포함 범위

- 검사 양식 API에 `requiresPhoto`를 전달한다.
- LQC·OQC 항목별 판정·비고 아래에 항목 전용 사진 첨부를 배치한다.
- 확정 시 사진 필수 항목의 사진 존재를 서버에서 검증한다.
- 전진검수·FAT의 단일 판정·증빙 흐름은 보존한다.

## 검증

- API 계약·DB 조회·확정 검증 테스트
- 항목별 첨부 UI 테스트
- 품질 검사 전체 회귀 테스트
