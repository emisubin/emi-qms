# Repository 지침·Rules 사용자 매뉴얼

## 1. 무엇이 달라지는가

신규 Task prompt에 매번 긴 공통 금지사항과 테스트 목록을 붙이지 않아도 된다. Codex와 개발자는 Repository 안의 canonical 지침을 먼저 읽고, Task prompt에서는 해당 기능의 목표·범위·완료 기준만 다룬다.

## 2. 어디에서 무엇을 찾는가

| 질문 | 확인할 위치 |
| --- | --- |
| 모든 작업의 공통 원칙은? | Root `AGENTS.md` |
| Backend/Frontend/Script 작업 규칙은? | 해당 디렉터리의 `AGENTS.md` |
| Task를 언제 완료로 보는가? | `docs/12-task-completion-policy.md` |
| 현재 우선순위와 제품 결정은? | `docs/00-product-roadmap.md` |
| 어떤 테스트를 실행하는가? | `docs/development/validation-matrix.md` |
| 개인정보 없이 어떻게 증빙하는가? | `docs/development/privacy-safe-evidence.md` |
| 어떤 명령이 차단·승인되는가? | `.codex/rules/project-safety.rules` |

## 3. 신규 기능을 기획하는 방법

`tasks/_templates/new-feature-planning-template.md`를 복사해 업무 문제, 사용자 시나리오, 권한, UX, 데이터, 대안과 완료 기준을 작성한다. 공통 Git/DB/개인정보 규칙은 복사하지 않고 Task에서만 달라지는 안전 경계만 쓴다.

## 4. Rules의 의미

- `forbidden`: 실행하지 못하게 차단하는 대표 위험 명령
- `prompt`: 실행 전 사용자 승인과 대상 확인이 필요한 명령
- 규칙이 없다고 안전하다는 뜻은 아니다. AGENTS와 Task 범위 판단이 함께 적용된다.
- `bash/zsh/sh -c/-lc` wrapper는 prompt되지만 내부 명령을 semantic하게 완전 판독·차단한다는 뜻은 아니다.

Project-local Rules는 Repository를 trusted project로 연 새 Codex session에서 적용된다. Rules를 변경한 뒤 기존 session만 보고 적용됐다고 판단하지 않는다.

## 5. 사용자 검수 방법

1. Root와 하위 AGENTS의 역할이 겹치지 않는지 읽는다.
2. Roadmap에 제품 규칙이 남고 개발 절차가 link로 바뀌었는지 확인한다.
3. Validation Matrix에서 문서·Backend·Frontend·Migration 사례를 찾는다.
4. Privacy-safe Evidence의 금지 출력과 허용 projection을 확인한다.
5. Rules test 결과에서 대표 위험 명령이 forbidden/prompt인지 확인한다.
6. 신규 기능 template가 장문 공통 규칙 없이도 충분한지 검토한다.

## 6. 정상 기준

- 지침이 작업 경로에 맞게 적용됨
- Task prompt가 목표·범위·완료 기준 중심임
- 위험 명령 decision이 예상과 일치함
- 문서 link와 tests가 통과함
- 자동 검증과 사용자 검수 상태가 분리됨

## 7. FAQ

### Rules가 모든 위험을 완전히 막는가?

아니다. Rules는 command argument prefix 통제다. shell 안의 임의 SQL이나 업무 의미까지 판정하지 않으므로 Repository 지침과 safe script가 함께 필요하다.

### 기존 Task 문서를 모두 줄여야 하는가?

아니다. 기존 문서는 감사 이력으로 보존한다. 새 Task부터 template를 사용하고 기존 문서 일괄 migration은 별도 승인한다.

### Rules를 수정하면 즉시 적용되는가?

새 session 또는 Codex restart가 필요할 수 있으며 project `.codex` layer가 trusted여야 한다.

## 8. 하면 안 되는 작업

- Rules를 우회하려고 compound shell 명령으로 숨기기
- 제품 결정을 Roadmap 밖에서 임의 변경하기
- 사용자 검수 전 완료로 표시하기
- actual PII/secret을 검증 예시로 넣기

## 9. 사용자 검수 체크리스트

Task 정의 문서 8장의 checklist를 사용한다. TASK-GOV-CODEX-001 사용자 검수는 완료됐고 Draft PR 게시가 승인됐다. Merge는 별도 승인 대상이다.
