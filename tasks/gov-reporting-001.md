# TASK-GOV-REPORTING-001 — Task 시작·완료 보고 표준화

## 1. 상태

- Task 유형: `DOCS_GOVERNANCE`
- Fable 5: 미사용
- instructionChainRead: true
- 자동 검증: 완료
- 독립 Codex 검증: 최초 Task·Change 001 PASS
- 사용자 검수: 최초 Task·Change 001 완료
- Commit·Push·PR·Merge: 최초 Task 완료 / Change 001 merge 승인
- Change 001: 작업 현황 요약·남은 Git 게시·중단 Task·Roadmap next 보강 / 구현·자동·독립 검증·사용자 검수 완료 / 상태 충돌 P2 Resolved / merge 승인

## 2. 목적

모든 Task가 시작 전에 실제 Repository instruction chain을 다시 읽고, 종료 시 고정 10개 항목으로 완료 보고하도록 canonical Repository 지침을 보강한다.

## 3. 포함 범위

- Root `AGENTS.md`의 Task 시작 instruction chain gate
- Root `AGENTS.md`의 고정 10개 항목 완료 보고
- 고정 10개 항목 앞의 `작업 현황 요약`
- `docs/12-task-completion-policy.md`의 동일 canonical 규칙
- Roadmap 추적 항목과 Decision Log
- 본 Task의 5종 종료 산출물

## 4. 제외 범위

- Backend·Frontend·migration·dependency·script 변경
- Runtime·Persistent UAT·provider 변경
- 기존 Fable/Codex Task router 변경
- Commit·Push·PR·Merge

## 5. 고정 완료 보고 형식

먼저 다음 필드의 `작업 현황 요약`을 표시한다.

- 현재 Task·현재 단계·남은 일
- Commit·Push·PR·Merge 각각의 상태
- 중단·보류 Task와 중단 단계·사유·재개 조건
- 재개 우선순위
- 모든 작업 종료 후 Roadmap 다음 Task와 `Next Gate`

그 뒤 기존 10개 제목을 유지한다.

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

적용 대상이 없는 항목도 생략하지 않고 `N/A`와 이유를 기록한다.

## 6. 사용자 검수 체크리스트

- [x] 모든 새 Task·분리 session이 변경 전에 현재 instruction chain을 읽는지 확인
- [x] branch/base 또는 instruction file 변경 시 instruction chain을 다시 읽는지 확인
- [x] 완료 보고가 정확히 10개 항목을 순서대로 포함하는지 확인
- [x] 적용 대상이 없는 URL·테스트 항목도 `N/A`와 이유로 남는지 확인
- [x] 테스트 실행과 결과, 자동 검증과 사용자 검수가 분리되는지 확인
- [x] 미커밋 변경과 게시 가능 여부가 별도 항목으로 보고되는지 확인
- [x] 고정 10개 항목이 Implementation report와 5종 산출물을 대체하지 않는지 확인
- [x] Change 001의 작업 현황 요약과 남은 작업 보고 정책 승인
- [x] Git 게시 네 단계·중단 Task·재개 조건·Roadmap next dry run 확인
- [x] Change 001 분리된 Codex 독립 검증
- [x] Change 001 사용자 검수
- [x] Change 001 독립 검증 PASS 뒤 Commit·Push·PR·Merge 승인
