# 다른 모델로 이 Repository를 사용할 때

공통 실행·승인 규칙은 [AGENTS.md](AGENTS.md)를 따른다. 별도의 Fable 인터뷰·기획·재승인 하네스를 기본 경로로 실행하지 않는다.

사용자가 Claude/Fable을 명시하면 실제 가능한 도구와 지정 모델을 확인하고 해당 요청 범위만 수행한다. 과거 planning의 작성 모델·원문·유효 승인은 보존한다. Codex 모델을 사용할 수 없다는 이유로 외부 CLI를 대체 agent로 실행하지 않는다.

`scripts/run-fable-readonly.sh`와 예전 template 계약은 보존 이력이며 현재 작업 지시·실행 경로에서 제외한다. 모델을 Claude/Fable로 지정해도 옛 인터뷰·승인·runner 절차를 복원하지 않고 동일한 v2 하네스를 따른다. 보존한 runner와 기존 사용자 WIP는 삭제하거나 보호를 해제하지 않는다.

기존 전문과 승인 이력은 [하네스 v1 보존본](docs/archive/harness-v1-2026-09-09/README.md) 및 각 Task change에서 찾는다. 현재 모델 선택·위임은 [작업 방식](docs/development/codex-model-workflow.md)을 참조한다.
