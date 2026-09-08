# 다른 모델로 이 Repository를 사용할 때

공통 실행·승인 규칙은 [AGENTS.md](AGENTS.md)를 따른다. 별도의 Fable 인터뷰·기획·재승인 하네스를 기본 경로로 실행하지 않는다.

사용자가 Claude/Fable을 명시하면 실제 가능한 도구와 지정 모델을 확인하고 해당 요청 범위만 수행한다. 과거 planning의 작성 모델·원문·유효 승인은 보존한다. Codex 모델을 사용할 수 없다는 이유로 외부 CLI를 대체 agent로 실행하지 않는다.

`scripts/run-fable-readonly.sh`와 관련 예전 template 계약은 legacy 이력이다. 현재 기본 흐름에 연결되지 않는다. 사용자가 legacy runner를 요청하면 지원 상태와 기존 fail-closed 계약을 먼저 확인하며, 이름이 read-only라는 이유로 draft·revise·cleanup까지 조회로 취급하지 않는다. runner 보호를 현재 하네스에 맞춘다고 자동 해제하지 않는다.

기존 전문과 승인 이력은 [하네스 v1 보존본](docs/archive/harness-v1-2026-09-09/README.md) 및 각 Task change에서 찾는다. 현재 모델 선택·위임은 [작업 방식](docs/development/codex-model-workflow.md)을 참조한다.
