# TASK-GOV-CODEX-001 Implementation Report

## 1. 목적과 배경

공통 개발 원칙을 Task별 장문 프롬프트에서 제거할 수 있도록 durable instruction source와 command safety layer를 구축한다. 판단 규칙과 명령 통제를 분리해 변경 이유와 적용 범위를 명확히 한다.

## 2. 포함·제외 범위

지침·정책·개발 문서·Rules·template만 변경한다. runtime code, DB, migration, dependency, 기존 runtime과 historical Task 산출물은 변경하지 않는다.

## 3. 기존 중복 inventory

- Root `AGENTS.md`: 전역 원칙과 port/DB/test 세부사항 혼재
- 종료 정책: Finding/산출물/검수 canonical 역할은 명확하지만 테스트·증빙 문서 link 부재
- Roadmap 27장: 제품 불변조건과 Git/DB/게시 절차 중복
- 하위 지침·project-local Rules·개발 validation/privacy 문서 부재
- 신규 기능 template: 공통 안전 경계를 매번 자유 형식으로 반복할 가능성

## 4. 이관 아키텍처

`AGENTS hierarchy → completion/roadmap canonical documents → development guides → project-local execpolicy`의 네 층으로 구성했다. Task template는 이 source들을 참조하고 Task 고유 결정만 기록한다.

## 5. Root와 하위 AGENTS

Root에는 source-of-truth, conflict stop, worktree/scope, data/migration, Finding, privacy, validation/publishing 원칙만 남겼다. .NET/DB transaction, React/UX, shell/process ownership은 각 영역의 하위 지침으로 이동했다.

## 6. 종료 정책과 Roadmap

종료 정책은 Validation Matrix와 Privacy-safe Evidence를 canonical link로 연결했다. Roadmap 27장은 개발 절차를 링크로 대체하고 공식 명칭, 18단계, 권한, 인증, 알림, 영업일과 관리자 정책 같은 제품 불변조건만 유지했다.

## 7. Validation과 privacy 문서

Validation Matrix는 문서, backend, frontend, migration, script, authorization, concurrency와 handover 유형을 분류한다. Privacy-safe Evidence는 Git/GitHub/browser/DB의 boolean·integer·fixed-enum projection과 output guard, artifact ownership을 정의한다.

## 8. Codex Rules

공식 `prefix_rule` Starlark 형식과 `codex execpolicy check`를 사용한다. `git add .`, hard reset/clean, force push의 대표 prefix, volume delete, direct drop/history prune은 forbidden이다. merge, direct main push, direct SQL, raw PR view, runtime/branch/worktree/container lifecycle과 shell wrapper는 prompt다.

Prefix rule은 argument prefix 기반이다. 현재 CLI check에서 `bash -lc` 내부의 단순 chain도 개별 project rule로 분해되지 않았으므로 wrapper 자체를 prompt한다. 이 prompt는 내부 semantic SQL, 모든 flag 순열 또는 승인 뒤 실행될 모든 동작을 완전 분석한다는 보장이 아니다. 해당 경계는 Root/Script 지침과 approved scripts의 fail-closed guard가 보완한다.

## 9. 신규 기능 template

공통 금지사항 전체를 복사하는 대신 Task 고유 Persistent UAT·migration·provider·runtime 영향을 기록한다. 사용자 시나리오, 권한, UX, 상태 모델, 대안, 미결정 사항과 완료 기준에 집중한다.

## 10. 자동 검증

| 검증 | 상태 | 결과 |
| --- | --- | --- |
| diff/changed scope | 통과 | 14개 allowlist 파일, runtime/migration/dependency/실행 script 변경 0 |
| Markdown link/anchor/heading | 통과 | missing link 0, missing anchor 0, duplicate heading 0 |
| execpolicy inline/unit checks | 통과 | direct 18/18 일치, shell wrapper 4개가 prompt로 gate됨, 내부 semantic 완전 차단은 미보장 |
| 새 session instruction source | 통과 | Root+Backend, Root+Frontend, Root+Scripts 경로별 load 확인 |
| representative Task dry run | 통과 | canonical docs/worktree/area validation/Finding/privacy/allowlist 6개 적용 |
| secret/PII | 통과 | added email/GUID/private key/bearer/webhook/secret assignment 0 |

## 11. 개인정보·secret

실제 사용자, 회사 계정, 고객/프로젝트/알림 원문과 credential을 추가하지 않는다. 공식 문서 URL과 synthetic 명령 예시만 사용한다.

## 12. Rollback

이 변경은 문서와 Rules만 포함한다. rollback은 이 Task의 changed files를 되돌리는 방식이며 runtime/DB rollback은 적용 대상이 아니다. Rules parsing 문제가 있으면 PR을 merge하지 않고 `.rules`를 수정한 뒤 execpolicy 검증을 재실행한다.

## 13. 제한사항

- Project-local Rules는 project `.codex` layer가 trusted일 때 새 Codex session에서 로드된다.
- Prefix match는 semantic command parser가 아니므로 지침과 script guard를 대체하지 않는다.
- Global Codex MCP 설정은 선택적 로컬 도구이며 Repository 변경·완료 조건에 포함하지 않는다.
- 기존 historical Task 프롬프트는 감사 이력으로 유지하며 자동 축약하지 않는다.

## 14. 후속 Task

- Draft PR CI와 changed-file gate 확인 후 merge는 별도 사용자 승인
- 이후 신규 Task부터 template 기반으로 공통 블록 제거
- 기존 장문 prompt의 일괄 정리는 별도 승인된 문서 migration으로만 수행

## 15. 해결한 업무 문제

공통 규칙을 여러 Task prompt에서 반복 관리하던 비용과 규칙 drift 위험을 줄이고, 개발자가 작업 위치에 맞는 지침과 검증 기준을 빠르게 찾을 수 있게 했다.

## 16. 기술적 결정과 검토한 대안

- 단일 거대 Root 지침 대신 nested AGENTS를 선택했다. 작업 경로별 context가 명확하다.
- Rules에 개발 판단까지 넣지 않았다. Rules는 command prefix enforcement에만 사용한다.
- 모든 테스트 명령을 Root에 유지하는 대안은 중복과 빠른 노후화 때문에 폐기했다.
- Roadmap 27장 삭제 대신 제품 불변조건을 남겼다. Roadmap의 제품 source-of-truth 역할을 보존한다.

## 17. 시행착오 및 폐기한 접근

Codex manual helper는 응답 무결성 header 부재로 사용할 수 없었다. 공식 Rules 페이지와 현재 CLI의 `codex execpolicy` 도움말로 문법과 테스트 명령을 교차 확인했다. `prefix_rule`로 arbitrary SQL/flag 위치를 정규식처럼 차단하려는 접근은 실제 언어가 exact argument prefix 기반이므로 채택하지 않았다.

첫 새-session load 검사에서는 Codex CLI의 자체 banner가 요청한 boolean 외 runtime metadata를 출력했다. 제품·Repository 결함이나 tracked leak은 아니지만 이 Task가 새로 정의한 privacy-safe evidence 기준의 검증 절차 P2로 분류했다. 이후 child process stdout/stderr를 메모리에서 캡처하고 allowlisted boolean만 출력하는 harness로 교체했으며 Backend/Frontend/Scripts와 representative Task를 처음부터 재검증했다. 원문 artifact와 Repository/staged leak은 0이다.

## 18. 사용자 검수 결과와 남은 항목

Checklist 작성, 자동 검증과 사용자 검수를 완료했다. 사용자는 instruction chain, Rules decision과 wrapper 제한, privacy-safe evidence, template, SOP/User manual 및 Draft PR 게시를 승인했다. Merge는 승인 범위가 아니다. 해결된 검증 절차 P2 외 신규 미해결 P0/P1/P2는 없다.

## 19. 주요 파일 목록

- `AGENTS.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, `scripts/AGENTS.md`
- `docs/development/validation-matrix.md`
- `docs/development/privacy-safe-evidence.md`
- `.codex/rules/project-safety.rules`
- `tasks/_templates/new-feature-planning-template.md`
- Task 4종 문서와 Roadmap/종료 정책 update

## 20. 5종 산출물

Task 정의 문서의 7장 표를 canonical 상태 목록으로 사용한다.
