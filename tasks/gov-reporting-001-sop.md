# TASK-GOV-REPORTING-001 SOP

## 1. Task 시작 절차

1. 현재 filesystem의 Root `AGENTS.md`를 읽는다.
2. 변경 경로에 적용되는 하위 `AGENTS.md`를 읽는다.
3. Product Roadmap, Task 종료 정책, Validation Matrix와 Privacy-safe Evidence를 읽는다.
4. 해당 Task의 planning, review, change와 implementation report가 있으면 읽는다.
5. branch, worktree, HEAD, dirty WIP와 동일 목적 작업을 확인한다.
6. `taskType`을 선택한다.
7. 변경 전에 `instructionChainRead=true`, `taskType`과 기준선을 보고한다.

읽을 수 없는 지침, 의미 있는 충돌 또는 미확인 WIP가 있으면 구현을 시작하지 않는다.

## 2. 재확인 조건

다음 중 하나가 발생하면 instruction chain gate를 다시 수행한다.

- 새 Codex 조사·구현·독립 검증 session 시작
- branch 또는 base 변경
- Root·하위 AGENTS나 canonical 정책 변경
- source-of-truth drift 또는 문서·코드 충돌 발견

같은 Task에서 단순히 다음 turn으로 이어지는 경우에는 위 조건이 없으면 전체 파일을 반복해서 읽을 필요가 없다.

## 3. 완료 보고 작성 절차

최종 응답은 아래 제목과 순서를 그대로 사용한다.

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

적용 대상이 없으면 항목을 삭제하지 않고 `N/A`와 이유를 쓴다.

## 4. 항목별 확인

- 수정 요약: 실제 변경과 미변경 영역을 구분한다.
- 수정한 파일: Task allowlist의 실제 경로만 기록한다.
- 실행한 테스트: 실행한 명령·검증 종류를 기록하고 미실행 항목은 이유를 쓴다.
- 테스트 결과: 성공·실패·미실행을 구분한다.
- Frontend/Backend URL: 실제 확인한 URL만 기록한다.
- 수동 검수 체크리스트: 미체크 항목을 완료로 표시하지 않는다.
- 미커밋 변경사항: changed/staged, commit·push·PR 상태를 기록한다.
- 남은 문제: Finding, blocker, 미검증과 별도 승인 항목을 기록한다.
- 게시 가능 여부: `GO`, `NO_GO`, `N/A`와 근거를 쓴다.

## 5. 금지 사항

- 10개 항목 중 일부 생략
- 과거 Task URL이나 테스트 결과 추정
- 테스트 미실행을 성공으로 표현
- 사용자 검수 대기를 완료로 표현
- 게시 가능 `GO`를 commit·push·merge 승인으로 해석
- 10개 항목으로 Implementation report나 5종 산출물 대체
