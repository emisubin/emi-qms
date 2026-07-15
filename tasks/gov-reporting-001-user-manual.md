# TASK-GOV-REPORTING-001 User Manual

## 1. 사용자에게 달라지는 점

제품 기능이나 화면은 바뀌지 않는다. 앞으로 Codex Task는 시작 시 Repository 지침 확인 상태를 먼저 알리고, 종료 시 작업 현황 요약과 기존 10개 항목으로 결과를 보고한다.

## 2. 시작 시 확인할 내용

Codex의 초기 업데이트에서 다음을 확인한다.

- `instructionChainRead=true`
- Task 유형
- 사용 branch/worktree 기준선
- 적용되는 하위 지침 또는 적용 대상 없음 사유

## 3. 종료 시 확인할 내용

먼저 `작업 현황 요약`에서 다음을 확인한다.

- 현재 Task·단계·남은 일
- Commit·Push·PR·Merge 각각의 상태
- 진행 중단·보류 Task와 재개 조건
- 재개 우선순위
- 모든 작업 종료 뒤 Product Roadmap의 다음 Task와 `Next Gate`

완료 보고에 다음 10개 제목이 순서대로 있어야 한다.

1. 수정 요약
2. 수정한 파일
3. 실행한 테스트
4. 테스트 결과
5. Frontend URL
6. Backend URL
7. 수동 검수 체크리스트
8. 미커밋 변경사항
9. 남은 문제
10. 게시 가능 여부

## 4. `N/A`의 의미

문서 Task처럼 Frontend나 Backend를 확인하지 않은 경우 해당 항목은 생략되지 않고 `N/A`와 이유가 표시된다. `N/A`는 성공 확인을 의미하지 않는다.

## 5. 게시 가능 여부

- `GO`: 품질 gate상 게시 가능하지만 실제 Commit·Push·PR·Merge에는 사용자 승인이 필요하다.
- `NO_GO`: Finding, 테스트 실패, 검수 미완료 또는 범위 위반이 있다.
- `N/A`: 게시 대상이 없는 조사·답변 Task다.

## 6. 사용자 검수 체크리스트

- [x] 시작 보고에 instruction chain 확인 상태가 있음
- [x] 완료 보고에 10개 항목이 모두 있음
- [x] 적용 대상이 없는 항목에 `N/A` 사유가 있음
- [x] 실행 테스트와 결과가 분리되어 있음
- [x] 사용자 검수와 자동 검증이 분리되어 있음
- [x] 미커밋·미게시 상태와 남은 문제가 명확함
- [ ] 중단·보류 Task의 중단 단계·사유·재개 조건이 표시됨
- [ ] Commit·Push·PR·Merge 상태를 각각 확인할 수 있음
- [ ] 모든 작업 종료 뒤 Roadmap 다음 Task·Next Gate가 표시됨
- [ ] Finding의 원인·영향·해소 또는 후속 위치를 확인할 수 있음
