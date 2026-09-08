# 하네스 v1 보존본 — 2026-09-09

이 폴더는 **비활성 원문 이력**이다. 여기에 있는 승인 절차·모델 지정·runtime 명령·“현재/다음” 상태를 새 작업에 적용하지 않는다. 현행 규칙은 [Root AGENTS](../../../AGENTS.md), 전환 요청과 범위는 [Change 024](../../../tasks/gov-codex-002-change-024.md)가 소유한다.

아래 24개 파일을 새 하네스 작성 전에 현재 filesystem에서 그대로 복사했다. 시작 HEAD는 `86dfd9f3887841723525d1b8a55fbe8cffe0b627`이며 **HEAD의 파일만이 아니라 이전 미커밋 하네스 변경도 포함**한다. 따라서 이 보존본을 해당 commit의 원문이라고 부르지 않는다.

| 당시 Repository 경로 | 복사한 원문 |
| --- | --- |
| `AGENTS.md` | [원문](root-AGENTS.md.snapshot) |
| `backend/AGENTS.md` | [원문](backend-AGENTS.md.snapshot) |
| `frontend/AGENTS.md` | [원문](frontend-AGENTS.md.snapshot) |
| `scripts/AGENTS.md` | [원문](scripts-AGENTS.md.snapshot) |
| `CLAUDE.md` | [원문](CLAUDE.md.snapshot) |
| `README.md` | [원문](README.md.snapshot) |
| `START_HERE.md` | [원문](START_HERE.md.snapshot) |
| `.codex/config.toml` | [원문](config.toml.snapshot) |
| `.codex/rules/project-safety.rules` | [원문](project-safety.rules.snapshot) |
| `docs/00-product-roadmap.md` | [원문](00-product-roadmap.md.snapshot) |
| `docs/27-experiment-task-ledger.md` | [원문](27-experiment-task-ledger.md.snapshot) |
| `docs/12-task-completion-policy.md` | [원문](12-task-completion-policy.md.snapshot) |
| `docs/development/codex-model-workflow.md` | [원문](codex-model-workflow.md.snapshot) |
| `docs/development/privacy-safe-evidence.md` | [원문](privacy-safe-evidence.md.snapshot) |
| `docs/development/validation-matrix.md` | [원문](validation-matrix.md.snapshot) |
| `docs/development/design-screen-promotion.md` | [원문](design-screen-promotion.md.snapshot) |
| `tasks/_templates/new-feature-interview-template.md` | [원문](new-feature-interview-template.md.snapshot) |
| `tasks/_templates/new-feature-planning-template.md` | [원문](new-feature-planning-template.md.snapshot) |
| `tasks/_templates/task-identity-gate-template.md` | [원문](task-identity-gate-template.md.snapshot) |
| `tasks/gov-codex-002.md` | [원문](gov-codex-002.md.snapshot) |
| `tasks/gov-codex-002-implementation-report.md` | [원문](gov-codex-002-implementation-report.md.snapshot) |
| `tasks/gov-reporting-001.md` | [원문](gov-reporting-001.md.snapshot) |
| `tasks/gov-reporting-001-sop.md` | [원문](gov-reporting-001-sop.md.snapshot) |
| `tasks/gov-reporting-001-user-manual.md` | [원문](gov-reporting-001-user-manual.md.snapshot) |

원문의 상대 링크는 당시 표의 Repository 경로를 기준으로 해석한다. `.snapshot`은 자동 instruction/config/실행 정책이 아니며, 새 문서의 Markdown 링크 대상으로 내부 anchor를 재해석하지 않는다. 과거 문서의 같은 제목·Task ID로 검색해 승인·업무 계약·시행착오를 확인한다.

원문은 rollback/reference 자료다. 새 Root를 예전 전체 파일로 무조건 덮어쓰거나 승인 이력을 새로운 실행 권한으로 이전하지 않는다. 복구 요청이 있으면 현재 WIP·후속 변경·적용 범위를 확인해 해당 문서/설정만 검토한다.

보존 파일의 복사 명령은 성공했으나 전체 SHA256 수집은 실행 정책의 `approval required by policy, but AskForApproval is set to Never`로 거부돼 별도 checksum manifest는 없다. 원격 main Roadmap을 이 폴더로 저장하는 별도 명령도 거부되어 원격 사본은 포함하지 않는다. 원격 제품 기준은 [Roadmap의 고정 출처](../../00-product-roadmap.md)에서 찾는다. 이 두 행동을 다른 도구로 재시도하지 않았다.
