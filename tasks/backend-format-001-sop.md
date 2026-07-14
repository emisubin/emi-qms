# TASK-BACKEND-FORMAT-001 SOP

## 1. 목적

Backend import-order baseline이 다시 drift할 때 안전하게 진단하고 복구하는 절차다. 제품 동작 수정 절차가 아니다.

## 2. 정상 기준

Repository root에서 다음 검사가 exit 0이어야 한다.

```bash
dotnet format backend/Emi.Qms.sln --verify-no-changes --no-restore
```

진단 count는 0이어야 하며 formatter 실행 전 dependency restore가 준비돼 있어야 한다.

## 3. 진단 절차

1. 최신 instruction chain과 Backend 지침을 읽는다.
2. Task branch/worktree와 dirty WIP를 확인한다.
3. `--verify-no-changes`와 private report로 diagnostic category·count·file path만 수집한다.
4. 실제 source line이나 개인정보를 증빙으로 출력하지 않는다.
5. 기존 Task allowlist 밖 파일이면 범위를 확대하지 않고 별도 승인받는다.

## 4. 수정 절차

1. Formatter 대상을 확인된 파일로 한정한다.
2. Formatter 실행 후 changed-file allowlist를 확인한다.
3. 파일별 `using` 집합이 전후 동일한지 확인한다.
4. `using` 블록 밖 diff가 0인지 확인한다.
5. 전체 format verify를 다시 실행한다.
6. Backend build·tests와 Task 영향에 맞는 전체 검증을 수행한다.

전체 solution formatter를 무검토로 실행해 unrelated 파일을 함께 commit하지 않는다.

## 5. 실패 대응

- `project.assets.json` 없음: dependency 선언을 바꾸지 않고 표준 `dotnet restore` 후 재실행한다.
- allowlist 밖 변경: formatter 결과를 게시하지 않고 원인을 조사한다.
- `using` 추가·삭제: 단순 ordering Task를 벗어나므로 중단한다.
- build/test 실패: format diff와 무관하다고 추정하지 말고 재현·분류한다.
- format verify exit 2 유지: private report에서 remaining category와 count를 확인한다.

## 6. Rollback

Task C# 9개와 문서 diff만 revert한다. DB·runtime·backup 조치를 하지 않는다.

## 7. 하면 안 되는 작업

- `.editorconfig` 규칙을 완화해 오류를 숨기기
- `git add .` 또는 `git add -A`
- 범위 밖 source refactor
- Persistent UAT write 또는 runtime restart
- raw build/test output에 포함될 수 있는 개인정보·secret을 tracked artifact로 저장
