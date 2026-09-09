# TASK-GOV-CODEX-002 Change 024 — PMS 하네스 v2 재구축

## 요청과 기준선

- 2026-09-09 사용자: “재설계안을 토대로 하네스를 전체적으로 처음부터 재설계해. 원래 있던 하네스를 수정하는 방식으로 진행하지말고 처음부터 다시 만들어.”
- 승인 범위: Change 021~023의 [재설계안](../docs/development/pms-harness-redesign.md)을 활성 Repository 하네스로 교체하고 검증·범위 내 보정·완료 local commit까지 수행. 제품 구현, 전역 스킬/설정, DB/runtime, 외부 provider, push·PR·merge·배포·자원 정리는 제외한다.
- 시작 HEAD: `86dfd9f3887841723525d1b8a55fbe8cffe0b627`, branch: `fix/task-gov-codex-002-instruction-clarity`. 조회한 원격 main 및 origin/main: `c3a3c79374babc840dca054bd1a05237a2c49685`.
- 사전 WIP: tracked 수정 17 / untracked 32 / staged 0. 기존 제품·runner WIP는 그대로 두며, 교체하는 하네스 24개 파일은 변경 직전 원문을 [보존본](../docs/archive/harness-v1-2026-09-09/README.md)으로 남긴다.
- instructionChainRead=true, taskType=DOCS_GOVERNANCE, gateStatus=PASS_REUSE, samePurposeMatchCount=1, canonicalTaskId=TASK-GOV-CODEX-002. Root 및 backend/frontend/scripts 지침, Roadmap, 종료/검증/개인정보 정책, 현재 Task·Change023·재설계안 확인.
- roadmapSequenceMatch=false / explicitRoadmapOverrideApproved=true: 기존 하네스 우선 진행 승인과 이번 같은 목적의 명시 요청. 제품 큐의 상호 순서는 변경하지 않음. 같은 목적 Task·branch·worktree·전체 PR 대조 결과 재사용 대상 하나, 이 branch PR 없음. 새 branch/worktree 없음.

## 보존과 폐기

| 보존할 계약 | 새 구조에서의 위치 |
| --- | --- |
| 사용자 요청과 유효 승인, main·운영·데이터 보호 | Root AGENTS |
| 실제 PMS 권한·3-DB·업무 상태·UI 재사용 | 하위 AGENTS 및 제품 지식의 Task/ADR 안내 |
| 변경에 대응하는 반례·화면 관찰·독립 검토 | 검증표와 완료 정책 |
| 승인·개발·배포 이력 및 교체 전 WIP | 기존 change와 비활성 원문 보존본 |
| 기존 CI gate, DB identity/ownership 안전 script | 구현 보존. 하네스 파일의 변경 분류만 좁게 추가 |

고정 모델 사슬, 모든 작업 인터뷰/중복 승인, 매 Task 전체 기록 재독, 5종 산출물·10개 보고 제목 강제, 모든 수정의 전체 회귀·추가 reviewer, 과거 runtime 항상 갱신, legacy Fable 기본 호출은 새 구조에 넣지 않는다. 옛 문장을 덧붙여 예외 처리하는 대신 활성 문서를 새 원문으로 교체한다.

## 구현 방향과 분담

Parent는 짧은 Root를 시작점으로 필요한 문서만 읽는 구조를 작성한다. 상세한 업무 계약은 해당 제품 Task/ADR로 연결하고, 현재 상태는 Task 하나가 소유하며 Roadmap은 실행 순서와 링크를 소유한다. 최신 main의 오산 배포 사실과 이 오래된 checkout의 구현을 구분한다.

설정·실행 정책·CI 분류의 bounded 구현은 `harness_v2_runtime`에게 요청 모델 `gpt-5.6-sol/xhigh`로 위임한다. 도구는 실행 모델을 반환하지 않아 관측값은 `NOT_REPORTED`다. Parent가 실제 결과를 통합 확인하고, 작성/구현과 분리된 GPT-6 High reviewer가 전체 최종 변경을 검토한다. 이번 작업 뒤에는 승인된 새 정책에 따라 GPT-6 직접 구현과 선택적 Sol 위임을 사용할 수 있다.

### Implementation Direction Brief — 실행 설정과 CI

- 현재 문제: 모든 코딩 Sol xhigh 고정, 일반 PR 조회·shell wrapper prompt, 광범위 legacy runner allow, force push 옵션 순서 누락, 하네스 파일을 알 수 없는 제품 변경으로 분류해 전체 회귀/배포 대상 확대 가능.
- 방식: 공식 지원 config로 6 Medium 기본값과 필요한 때만 위임. 전역 sandbox/approval 설정은 건드리지 않음. 명확한 파괴/게시 경계는 유지하고 불필요한 명령군 허용·차단을 제거. Prefix가 SQL 의미·모든 Git 인자 순서를 이해한다는 주장은 금지. 특히 직접 SQL은 안전한 읽기만 판별할 수 없으면 보수적 prompt를 유지하고 이유를 기록.
- allowlist: `.codex/config.toml`, `.codex/rules/project-safety.rules`, `scripts/classify-change-scope.sh`, `scripts/test-change-scope.sh`.
- CI: 위 config/rules의 정확한 경로는 policy 검증 대상으로, `docs/archive/harness-v1-2026-09-09/*.snapshot`은 비실행 보존본으로 분류. 알 수 없는 경로의 fail-safe와 기존 제품·Azure·E2E 분류는 보존. 일반 scripts/e2e 분기 순서의 별도 제품 영향 보정은 이번에 포함하지 않음.
- 완료: 명확한 조회에 새 project prompt 없음, main/force/WIP/영구 DB 보호 유지, 관련 명령 변형의 판정과 prefix 한계 명시. 하네스 변경만으로 제품 테스트·배포 플래그가 켜지지 않고 실제 제품 혼합 변경은 보존.
- 검증: 실제 `codex execpolicy check`로 대표 허용/거부/승인 사례를 평가(대상 명령 실행 금지), config 구문·지원키 확인, 분류의 harness-only/mixed/unknown 반례와 기존 분류 tests. 전체 제품 회귀·DB·runtime 실행 없음.
- 반환: 실제 diff와 검증 명령/결과, 한계·미해결 Finding. 범위·기능/운영 권한 확대나 정책 거부가 있으면 그 부분만 parent에 반환. 재위임·commit·게시 금지.

### 문서 allowlist

- Root `AGENTS.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, `scripts/AGENTS.md`, `README.md`, `START_HERE.md`, `CLAUDE.md`.
- `docs/00-product-roadmap.md`, `docs/27-experiment-task-ledger.md`, `docs/12-task-completion-policy.md`.
- `docs/development/{codex-model-workflow,validation-matrix,privacy-safe-evidence,design-screen-promotion}.md`.
- 새 `docs/development/pms-project-guide.md`, `docs/development/harness-v2-verification.md`, `docs/archive/harness-v1-2026-09-09/`의 index·snapshot.
- `tasks/_templates/`의 기존 세 template 교체 및 새 `task-template.md`.
- `tasks/gov-codex-002.md`, `tasks/gov-codex-002-implementation-report.md`, 본 Change024.
- `tasks/gov-reporting-001{,-sop,-user-manual}.md`: 이전 보고 규정은 보존본으로 옮기고 현행 정책 링크만 유지.
- 재설계안/검토안은 당시 제안 원문을 유지한다. 실제 활성화 결과는 본 Change에 연결한다.

## 완료 조건과 첫 검증 경로

1. 짧은 새 Root에서 범위가 분명한 수정 → 관련 검증 → 위험에 맞는 review → 자동 local commit까지 중복 질문 없이 이어갈 수 있다.
2. 권한·DB·운영·main 사례에서는 명확한 승인과 필요한 독립 검토가 유지된다. 상위 실행 제한을 문서가 해제하지 않는다.
3. 완료 실험/이미 배포한 오산 기능을 새로 구현하지 않으며, 진행·대시보드는 미구현 후속 계약으로 남는다.
4. 개인 스킬 자동 연쇄·CLI agent 우회·무한 재시도·전체 회귀 중복을 기본 절차에 넣지 않는다.
5. 원문 보존·문서 참조·config/rules/CI 집중 검증과 독립 검토를 완료하고, 실제 사용 미검증과 사용자 검수·게시 상태를 구분한다.

## 실행 결과와 재개

- 현재: **활성 하네스 재구축·집중 검증·독립 검토 완료**. 상세 근거와 미확인 범위는 [검증 보고](../docs/development/harness-v2-verification.md)가 소유한다.
- 산출물: 26개 활성/작업 문서, archive index와 원문 24개, config/rules/분류 코드·tests 4개로 총 55개 scoped 경로. Root는 교체 전 295줄에서 51줄, Roadmap은 2,411줄에서 호환 anchor 포함 82줄로 재작성. 줄 수는 비용·성능 개선 실측이 아니다.
- 검사: script 문법·ShellCheck 각 2개 PASS, 분류 45 assertions PASS, config parse·대표 execpolicy 판정·diff 확인. Parent는 실제 diff와 결과를 읽었으며 제품 전체 회귀를 반복하지 않음.
- 독립 검토: 작성과 분리된 기존 `governance_audit` 맥락에서 이번 기준선을 새로 확인. 요청 GPT-6 High, 관측 `NOT_REPORTED`. 계약 충족/품질 `GO/GO`, 신규 actionable P0/P1/P2/P3 `0/0/0/0`. 신규 spawn은 한도상 불가했고 과거 GO를 재사용하지 않음.
- 제약: 일부 archive/문서 보조 명령과 reset 개별 policy 평가는 실행 도구가 거부해 재시도하지 않음. 자동 전체 링크·원문 전수 checksum·새 Codex 실행에서의 적용·비용/시간 개선·제품/운영 동적 검증은 미확인. 이를 PASS 또는 새 운영 승인으로 표시하지 않음.
- 사용자 검수: 결과 설명 및 새 세션 사용 확인 대기. 원격 반영·배포: 미수행, 이번 범위 제외.
- Git: 본 Change를 포함한 완료 local commit으로 기록하며 실제 SHA는 Git 이력과 최종 보고에서 확인. 기존 제품/runner WIP는 commit에 포함하지 않음. Push·PR·merge 없음.
- 자원: 새 제품 runtime·DB·worktree 없음. 분류 검사의 전용 합성 Git fixture 생성·commit·cleanup만 기존 test 내부에서 수행. 사용자 보류 자원은 추적 유지, 원문 snapshot은 Repository 이력으로 보존.
- 재개: 사용자는 새 Codex 실행에서 프로젝트의 기본값과 실제 다음 작업을 확인할 수 있음. 제품 기능은 최신 main과 Task 상태를 대조해 [Roadmap](../docs/00-product-roadmap.md)의 오산 진행 Task로 이어가며, 이번 하네스 변경의 원격 반영·배포 승인은 별도.
