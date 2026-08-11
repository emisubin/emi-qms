# TASK-NOTICE-EDITOR-001 Change 003 — Full-Stack 수정 완료 단언 보정

## 확인된 증상과 원인

- 제품은 공지 수정 저장과 상세 복귀를 정상 완료했지만 E2E가 `/수정/` 부분 문자열 하나를 찾으며 제목·작성 metadata·상태·버튼 네 요소와 동시에 일치해 strict locator 오류가 발생했다.

## 최소 변경

- 제품 UI·API·DB는 변경하지 않는다.
- E2E는 저장 뒤의 정확한 상태 문구 `공지 수정 내용을 저장했습니다.`를 단일 요소로 확인한다.

## 검증

- 새 공지 편집·첨부 Full-Stack 시나리오를 집중 재실행한다.
- 통합 Full-Stack 전체 회귀에서 동일 strict locator 오류가 없는지 확인한다.
