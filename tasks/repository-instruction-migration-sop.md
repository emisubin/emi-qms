# TASK-GOV-CODEX-001 SOP

## 1. 목적

Repository 지침, 정책과 Rules를 변경할 때 중복·충돌·누락 없이 검증하는 반복 절차다.

## 2. Instruction source 확인

1. Root `AGENTS.md`를 읽는다.
2. 변경 경로의 가장 가까운 하위 `AGENTS.md`를 읽는다.
3. 종료 정책과 Product Roadmap을 읽는다.
4. Validation Matrix와 Privacy-safe Evidence를 확인한다.
5. 실제 코드·설정·tests와 충돌하면 변경하지 않는다.

## 3. 규칙 배치 판단

- 여러 경로에 적용되는 판단: Root `AGENTS.md`
- Backend/Frontend/Scripts 전용 판단: 하위 `AGENTS.md`
- 종료·검수·Finding: 종료 정책
- 제품 결정과 Task 순서: Roadmap
- 테스트 선택: Validation Matrix
- 증빙 출력: Privacy-safe Evidence
- 위험 command prefix: `.codex/rules`
- 한 Task에서만 다른 내용: Task 문서/prompt

## 4. Rules 수정

1. Rules가 명령 통제인지 확인한다. 개발 설계 판단은 넣지 않는다.
2. 가장 좁은 argument prefix를 사용한다.
3. forbidden에는 안전한 대안을 justification에 기록한다.
4. `match`와 `not_match`를 모두 추가한다.
5. overlapping rule의 가장 restrictive decision을 확인한다.
6. shell wrapper는 wrapper 자체를 prompt하되 내부 명령의 완전한 semantic 차단으로 해석하지 않는다.

## 5. Rules 검증

```bash
codex execpolicy check --pretty \
  --rules .codex/rules/project-safety.rules \
  -- git add .
```

대표 forbidden, prompt와 unrelated command를 각각 검사한다. `bash/zsh/sh -c/-lc` wrapper가 prompt되는지와 내부 command 분해 여부를 별도로 기록한다. 결과의 `decision`과 matching rule을 확인하고 parse error가 있으면 게시하지 않는다.

## 6. 새 session 검증

Project-local Rules와 AGENTS는 새 session에서 확인한다. project `.codex` layer가 trusted인지 확인하고 read-only Codex session에서 Root와 backend/frontend/scripts 경로별 적용 source를 보고하게 한다. 파일 수정 권한은 주지 않는다.

## 7. 대표 Task dry run

신규 기능 template에 가상의 목표·범위·완료 기준만 제공한다. Codex가 별도 장문 공통 블록 없이 다음을 찾아내는지 확인한다.

- canonical docs 선행 확인
- feature worktree
- 영역별 테스트
- Finding/user validation gate
- privacy-safe evidence
- allowlist staging

## 8. 문서 검증

- `git diff --check`
- local Markdown link와 anchor
- duplicate heading
- Root와 Roadmap의 공통 규칙 중복 감소
- runtime/migration/dependency diff 0
- secret/PII와 generated artifact 0

## 9. 사용자 검수와 게시

자동 검증과 사용자 검수 상태를 분리한다. 사용자가 지침 구조, Rules decision과 template를 확인하기 전에는 사용자 검수 완료로 표시하지 않는다. TASK-GOV-CODEX-001은 사용자 검수와 Draft PR 게시 승인을 받았으며 merge는 별도 명시 요청 전까지 수행하지 않는다.

## 10. Rollback

Rules parse/load 실패 또는 지침 충돌 시 변경 파일만 되돌리고 기존 runtime·DB를 건드리지 않는다. 이미 시작된 session은 이전 instruction을 유지할 수 있으므로 수정 후 새 session에서 재검증한다.

## 11. 금지사항

- 기존 dirty worktree에서 migration 작업 수행
- global/user Rules를 Repository 정책 대신 강제 변경
- Roadmap 제품 결정을 AGENTS로 이동
- historical Task 문서를 일괄 rewrite
- match test 없이 forbidden rule 추가
- 실제 PII/secret을 test fixture로 사용

## 12. 사용자 검수 체크리스트

Task 정의 문서 8장의 checklist를 사용한다.

## 13. 변경 이력

| 날짜 | 변경 |
| --- | --- |
| 2026-07-11 | 최초 작성 |
| 2026-07-11 | 사용자 검수 완료, shell wrapper prompt와 제한사항 반영 |
